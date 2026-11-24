# Résolution du chemin final après multiplexage

Captvty 3.0.1.28 — CLI 2.9.1 — Debian 13 — 24 novembre 2026.
Commit : `a150e36`.

## 1. Symptôme

Après téléchargement de Jossi (1/6), le moteur avait produit un MKV de 446910600 octets, mais le provider annonçait un chemin MP4 inexistant. Le code retour était 0. Il fallait distinguer le succès du téléchargement de la validité du chemin annoncé.

## 2. Analyse du provider

Dans `captvty-provider-28.cs`, RunGet lance le téléchargement via `_nC::_tlB(selected,false)`, construit un objet `_NhA`, appelle `_RSb::_Utb(item)` et interroge `_NSB` toutes les secondes. Les lignes GETSTATE et DOWNLOAD sont transmises à la CLI. Celle-ci affiche le chemin reçu, sans le reconstruire. T18 utilise un mécanisme distinct (CAPTVTY_FILE).

L'ancien DownloadPath appelait `_OOA`. SanitizeDownloadedFile normalisait le nom seulement si le fichier existait. Un chemin périmé pouvait donc être annoncé sans correction.

## 3. Analyse IL

Outils de captvty-lab : dump-type.exe, dump-method-il.exe, find-field-refs.exe, find-callers.exe.

```bash
LAB="$HOME/captvty-lab/tools"
TARGET="$HOME/Téléchargements/captvty-3.0.1.28"
ENGINE="$TARGET/Captvty-cli-engine-28-progress.exe"
mono "$LAB/dump-type.exe" "$ENGINE" '_NhA'
mono "$LAB/dump-type.exe" "$ENGINE" '_NhA/_WYA'
mono "$LAB/dump-method-il.exe" "$ENGINE" '_NhA' '_OOA'
mono "$LAB/dump-method-il.exe" "$ENGINE" '_NhA' '_OEA'
mono "$LAB/dump-method-il.exe" "$ENGINE" '_NhA/_mIB' MoveNext
mono "$LAB/dump-method-il.exe" "$ENGINE" '_P4A/_K9A' MoveNext
```

Éléments identifiés :

| Élément | Rôle observé |
|---|---|
| `_NhA::_LE` | Liste de `_NhA/_WYA` |
| `_NhA::_0XA` | Objet de verrouillage |
| `_NhA::_zmB` | État |
| `_OOA` | Chemin temporaire à l'état 5, sinon chemin principal selon le drapeau `_GhB` |
| `_OEA` | Liste de chemins, ou chemin temporaire |
| `_WYA::_5M` | Chemin |
| `_WYA::_OT` | Drapeau booléen |
| `_WYA::_xuA` | Modification du drapeau |

`_NhA/_mIB::MoveNext` appelle `_EMb`, `_sab`, `_NNB`, ajoute des éléments à `_LE` et modifie les drapeaux principaux notamment aux offsets IL_0B66 et IL_0BFF. Plusieurs sites définissent l'état 7. `_pzb` appelle `_P4A::_Bu`, `_Clb` retourne `_f4A`. Des sites de modification ont été repérés dans `_P4A/_K9A::MoveNext` aux offsets IL_00EA, IL_0184 et IL_0343.

Ces observations ne démontrent pas à elles seules une course : le contrôle de flux est obfusqué.

## 4. Instrumentation temporaire

Le provider a été instrumenté pour afficher `_OOA`, `_OEA` et File.Exists à chaque changement d'état. Journaux locaux : `/tmp/captvty-path-debug-jossi-2.log` et `/tmp/captvty-path-debug-jossi-3.log`.

| État | Observation |
|---|---|
| 0 | Chemin vide |
| 1 | Chemin temporaire non encore existant |
| 5 | Fichier temporaire existant |
| 8 | MP4 existant |
| 7 | `_OOA` retourne le MKV existant ; `_OEA` contient aussi l'ancien MP4 disparu |

La première tentative Jossi (2/6) a échoué avec une exception HashSet indépendante. Les tentatives suivantes ont réussi. L'anomalie initiale du chemin n'a pas été reproduite sous instrumentation. Une influence du timing reste une hypothèse. Le provider non instrumenté a été restauré avant le correctif.

## 5. Correctif

Deux méthodes ont été ajoutées : IsFinalMediaFile et ResolveFinalDownloadPath.

IsFinalMediaFile vérifie l'existence du fichier, exclut les noms contenant `.tmp.` ou `.part.` et accepte les extensions multimédias mp4, mkv, ts, m2ts, avi, wmv, flv, webm, mov, mpg, mpeg, mp3, m4a, aac, wav, flac, ogg, wma.

ResolveFinalDownloadPath privilégie `_OOA` si son chemin est valide. Sinon, il examine `_OEA` et ne retient qu'un candidat valide distinct. Il réessaie toutes les 250 ms pendant cinq secondes au maximum. Les exceptions de résolution sont interceptées. Aucun balayage arbitraire du répertoire de téléchargement n'est effectué.

À l'état 7, le provider résout le chemin, normalise le nom et vérifie à nouveau l'existence. En cas d'échec, il retourne 10 au lieu d'annoncer un succès trompeur.

La normalisation conserve lettres, chiffres, tirets, underscores et points ; les autres caractères sont remplacés par des underscores. Les collisions sont évitées par les suffixes `_2`, `_3`, etc.

## 6. Tests indépendants

Depuis le dépôt :

```bash
mcs -out:/tmp/test-final-path.exe tests/test-final-path.cs
```

Depuis le répertoire Captvty :

```bash
MONO_PATH="$PWD:$PWD/bin:$PWD/bin/cefsharp" \
    mono /tmp/test-final-path.exe "$PWD/captvty-provider-28.exe"
```

Le test utilise la réflexion, des objets simulant `_OOA` et `_OEA`, et des fichiers temporaires.

Cas couverts : chemins principaux MKV, MP4 et MP3 ; MP4 disparu avec MKV disponible ; exclusion des VTT et fichiers temporaires ; création différée (1200 ms) ; absence de fichier valide ; liste nulle ; candidats ambigus. Les dix tests ont réussi, y compris après installation définitive 2.9.1.

## 7. Validation réelle

Jossi (4/6) a produit un MKV final normalisé de 442992808 octets, HEVC 1920x1080, durée 1436,12 s, cinq pistes AAC et quatre sous-titres. Code retour 0.

Jossi (5/6) et (6/6) ont également produit des MKV. Des collisions de noms ont entraîné des suffixes `_2`. Les fichiers de même taille avaient des SHA-256 différents ; une différence de métadonnées Matroska est possible, mais non démontrée. Aucun doublon n'a été supprimé sur cette seule base.

Le test définitif Wolf Biermann a confirmé le chemin final MP4 existant, code retour 0, taille 48550985 octets.

## 8. Limites

L'anomalie initiale n'a pas été reproduite sous instrumentation. Un MP4 principal encore existant à l'état 7 pourrait être retenu même si une transformation ultérieure était en cours. Une piste audio isolée peut aussi être candidate selon les extensions acceptées. La résolution refuse les candidats multiples lorsqu'elle utilise la liste de secours, plutôt que de deviner. Les tests ne prouvent pas la correction de toutes les courses possibles.

- [README](../README.md)
- [Outils multimédias](MEDIA-TOOLS.md)
- [HashSet Arte](ARTE-HASHSET-3.0.1.28.md)
