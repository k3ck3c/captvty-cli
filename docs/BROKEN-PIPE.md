# Gestion du broken pipe sous Mono et Linux

Captvty CLI 2.9.1 — Debian 13 — 24 novembre 2026.
Commit : `1185187`.

## 1. Symptôme

```bash
mono captvty-cli.exe list "T18" "" | head -40
```

Lorsque head termine avant la CLI, Mono peut lever une System.IO.IOException : `Write fault on path .../[Unknown]`, suivie d'une exception non interceptée. La pile passait par Console.WriteLine, CaptvtyCli.RunList et CaptvtyCli.Main.

Le paramètre de recherche vide est obligatoire pour list. Pour limiter l'affichage tout en consommant le flux complet, `sed -n '1,40p'` est une alternative à head.

## 2. Expériences indépendantes

Un programme C# écrivant 100000 lignes, avec interception de IOException et retour 42, a permis de distinguer :

| Destination | HResult observé | Retour du test |
|---|---|---:|
| head -5 | 0x8007001D | 42 |
| /dev/full | 0x80070027 | 42 |
| /dev/null | aucune erreur | 0 |

0x8007001D est une erreur d'écriture générique, pas une preuve suffisante de EPIPE.

Un second programme a appelé `signal(13, IntPtr.Zero)` via P/Invoke libc. Le gestionnaire précédent valait 0x1 (SIG_IGN). Avec SIGPIPE rétabli, le tube fermé produisait PIPESTATUS 141 0 ; /dev/full conservait 42 et /dev/null 0.

Cette solution globale a été écartée : elle modifierait aussi le comportement des autres tubes du processus et potentiellement celui des processus enfants.

## 3. Identification du tube fermé

```bash
readlink /proc/$$/fd/1
```

La destination peut être un terminal, un fichier, /dev/full ou un tube anonyme `pipe:[...]`.

Un troisième programme a utilisé poll via P/Invoke libc, avec une structure contenant int fd, short events, short revents. Le descripteur était 1, events=4 (POLLOUT), timeout=0.

| Destination | poll rc | revents | POLLERR |
|---|---:|---|---|
| Tube fermé | 1 | 0x000C | oui |
| /dev/full | 1 | 0x0004 | non |

Le correctif combine donc le HResult générique, la nature du descripteur et l'état du tube.

## 4. Implémentation

`stdout-pipe.cs` définit StdoutPipeClosedException (dérivée de IOException) et StdoutPipeWriter (dérivé de TextWriter).

Le wrapper intercepte les erreurs de Write, WriteLine et Flush. Il ne transforme une IOException en StdoutPipeClosedException que si :

- HResult vaut 0x8007001D ;
- readlink sur `/proc/self/fd/1` identifie un tube anonyme `pipe:[...]` ;
- poll sur fd 1 signale POLLERR.

Sinon l'exception originale est propagée. Aucun changement global de SIGPIPE n'est effectué.

Main configure UTF-8 sans BOM, installe le wrapper Console.Out, appelle RunMain et vide la sortie. StdoutPipeClosedException entraîne un retour explicite 141 ; les autres IOException entraînent un diagnostic et un retour 1. Les douze interceptions typées Exception du code CLI ont été adaptées pour ne pas absorber StdoutPipeClosedException.

L'initialisation de Console.Out est elle-même protégée : une première expérience avec UTF-8 et BOM hors du try avait provoqué une exception sur /dev/full pendant SetupStreams/AutoFlush.

## 5. Téléchargement déjà lancé

RunGetWorker traite la sortie du provider de manière asynchrone. Les callbacks conservent les données reçues et utilisent deux indicateurs, stdoutClosed et stdoutError.

Si le tube se ferme pendant l'affichage de la progression, les affichages suivants sont ignorés mais le provider continue. Après WaitForExit et vidage des callbacks, le résultat du téléchargement est conservé. Une erreur d'écriture ordinaire est propagée après une sortie normale du worker. En cas de timeout, le worker est arrêté ; le diagnostic stdoutError n'est pas nécessairement conservé dans cette branche.

Si le tube est déjà fermé avant le lancement, la CLI peut retourner 141 sans démarrer le téléchargement.

## 6. Test autonome

```bash
mcs -out:/tmp/test-stdout-pipe.exe \
    stdout-pipe.cs tests/test-stdout-pipe.cs
mono /tmp/test-stdout-pipe.exe | head -5
echo "${PIPESTATUS[@]}"
mono /tmp/test-stdout-pipe.exe | head -0
echo "${PIPESTATUS[@]}"
mono /tmp/test-stdout-pipe.exe > /dev/full
echo "$?"
mono /tmp/test-stdout-pipe.exe > /dev/null
echo "$?"
```

Résultats observés : 141 0, 141 0, 42, 0. Le retour 42 est propre au test autonome, pas à la CLI.

## 7. Tests de non-régression

`tests/fake-get-provider.cs` émet GETMEDIA, dix états de progression espacés de 200 ms, crée le marqueur `fake-download-complete.txt`, puis émet les états 5, 8, 7 et DOWNLOAD. Le test ne télécharge aucune vidéo.

Les six cas intégrés à `tests/test-cli.sh` couvrent :

| Cas | Comportement attendu |
|---|---|
| normal | succès, marqueur présent |
| pipe | téléchargement poursuivi, marqueur présent |
| closed | retour 141, pas de marqueur |
| timeout | retour 1, pas de marqueur |
| diskfull | retour 1, pas de marqueur |
| pipe-timeout | timeout malgré la fermeture du tube |

Les tests vérifient également l'absence d'exception non interceptée. Le cas closed utilise une substitution de processus fermée ; sa synchronisation peut théoriquement dépendre du timing. Le cas diskfull ne valide pas une erreur survenue tardivement dans le callback.

```bash
CLI="/chemin/vers/captvty-cli.exe" bash tests/test-cli.sh
```

Résultat définitif : 29 tests hors réseau réussis, dont ces six nouveaux tests ; 34 avec les cinq tests réseau T18.

## 8. Validation réelle

```bash
mono captvty-cli.exe list "T18" "" | head -5
echo "${PIPESTATUS[@]}"
```

Résultat définitif : `141 0`, sans exception non interceptée. Les essais de développement avec head -0 ont également retourné 141 0, /dev/full a retourné 1 et /dev/null 0. Le flux normal était identique hors suppression du BOM (207 lignes lors de la comparaison).

Le code 141 est explicite, et non la conséquence d'un signal reçu. Avec `set -o pipefail`, le pipeline est donc considéré en échec si la CLI retourne 141, même lorsque head réussit.

## 9. Limites

La détection est spécifique aux tubes anonymes Linux et à /proc. Les FIFO nommées et autres systèmes n'ont pas été validés. La poursuite du téléchargement a été testée avec le worker simulé du provider Captvty ; elle ne doit pas être généralisée sans analyse aux providers spécialisés, notamment T18, ni à update. Les erreurs d'écriture tardives dans le callback n'ont pas été testées directement.

- [README](../README.md)
- [Chemin final](FINAL-DOWNLOAD-PATH.md)
