# Installation du runtime Python pour T18 — Captvty CLI 2.9.2

## Origine et périmètre

Le problème signalé par Pulsar concerne l'installation de yt-dlp et curl_cffi sur une distribution ancienne. Ubuntu 22.04.5 LTS sert de banc d'essai pour la famille de distributions dont Linux Mint 21.3 dérive ; la distribution exacte de Pulsar et le fonctionnement sur Mint restent à confirmer.

La version 2.9.2 conserve le moteur Captvty 3.0.1.28, le provider et les patches de la 2.9.1. Le diff des deux fichiers C# de la CLI ne montre que le changement de la chaîne retournée par `--version`.

## Dépendances et choix technique

| Paquet | Version validée | Python minimal déclaré |
|---|---|---|
| yt-dlp | 2026.10.26 | 3.11 |
| curl_cffi | 0.17.0 | 3.10 |
| cffi | 2.1.1 | 3.10 |
| certifi | 2026.11.20 | 3.7 |
| pycparser | 3.12 | 3.11 |

Les deux premiers paquets sont épinglés par l'installateur ; les dépendances transitives sont les versions observées lors des essais, non un verrouillage complet.

Sur Ubuntu 22.04, Python 3.10.12 ne permet pas d'installer la version demandée de yt-dlp. L'essai pip a retourné `No matching distribution found for yt-dlp==2026.10.26`.

Le choix retenu est de conserver venv et pip sur toutes les distributions. uv sert uniquement à obtenir un interpréteur privé si nécessaire. L'installation n'exige pas de modifier `/usr/bin/python3`, d'ajouter un PPA ou d'installer uv globalement. Le TUI conserve son interpréteur système.

## Organisation

```text
install.sh
  tools/install-ytdlp.sh REPERTOIRE_CAPTVTY
    tools/prepare-python.sh REPERTOIRE_CAPTVTY
      Python système compatible ? -> utiliser celui-ci
      Sinon Python privé compatible présent ? -> réutiliser
      Sinon -> télécharger uv, vérifier, installer Python privé
    sauvegarder le venv existant
    créer .ytdlp-venv au chemin définitif
    installer les wheels et vérifier les dépendances
    succès -> supprimer la sauvegarde
    échec pris en charge -> restaurer la sauvegarde
```

### Sélection de Python

`prepare-python.sh` vérifie Python >= 3.11, les imports venv et ensurepip, puis crée réellement un venv temporaire et vérifie que pip fonctionne. Un simple numéro de version ne suffit donc pas. Un faux interpréteur annonçant une version compatible mais incapable de créer le venv a été rejeté lors d'un essai.

Le script renvoie le chemin de l'interpréteur sur stdout et les diagnostics sur stderr. Si le Python système ne convient pas, il recherche le runtime privé CPython 3.13.18 dans `.python-runtime/python/`. À défaut, il installe ce runtime avec uv 0.13.10. Le téléchargement automatique de secours est limité à Linux x86_64.

### Intégrité du téléchargement

Archive uv : `uv-x86_64-unknown-linux-gnu.tar.gz`, release 0.13.10.

Source : https://github.com/astral-sh/uv/releases/tag/0.13.10

SHA-256 attendu :

```text
bf65ac1a0af10f92c522d91ac096c7c0a39657647b155577e7836c2cbbb4526c
```

L'empreinte calculée a été comparée au fichier de checksum officiel et au digest de l'API GitHub. Le script contrôle cette empreinte avant extraction et vérifie la version de uv. Un essai avec une empreinte volontairement incorrecte a échoué avec le code 1. Le répertoire temporaire a été nettoyé.

Les variables `UV_*` héritées sont supprimées avant configuration des répertoires privés ; `--no-config` évite les fichiers de configuration uv. Un essai avec des variables UV parasites a confirmé l'installation du bon runtime x86_64. uv est extrait temporairement et n'est pas conservé dans le répertoire Captvty.

### Audit du contrôle Python dans uv

Sources uv 0.13.10, commit `e3e7c1eacb980339352a5ad69affcd3b6c206c92`.

Le catalogue `crates/uv-python-managed/download-metadata.json` contient l'entrée `cpython-3.13.18-linux-x86_64-gnu`, build 20261124 :

- Archive : `cpython-3.13.18+20261124-x86_64-unknown-linux-gnu-install_only_stripped.tar.gz`.
- SHA-256 : `b56ceb4fc1d8f0c49b7bd4dc91cd176bad7b86a88b9fc509bae6b10b3ee36692`.

`build.rs` compresse le catalogue, intégré au binaire par `include_bytes!` dans `downloads.rs`. Dans `extract_reader` (lignes 748–776 du commit audité), un HashReader calcule SHA-256 pendant l'extraction temporaire ; une divergence entraîne `Error::HashMismatch` avant validation du résultat. Les chemins de téléchargement direct et de lecture d'archive en cache utilisent cette fonction. Le test source `test_should_try_next_url_hash_mismatch` vérifie qu'une divergence ne déclenche pas un autre miroir.

Cet audit est une lecture du code source, pas un test d'injection d'une archive Python corrompue ni une vérification indépendante de signatures. Le test source uv mentionné n'a pas été exécuté. uv adapte également les chemins sysconfig du runtime à son emplacement local.

## Installation et restauration du venv

`install-ytdlp.sh` prépare Python avant de déplacer le venv existant. Il refuse les chemins venv ou sauvegarde qui sont des liens symboliques ou des objets autres que des répertoires. Si seule `.ytdlp-venv.old` existe, elle est restaurée ; si les deux existent, le script refuse de continuer sans vérification manuelle.

Le venv est créé directement sous `.ytdlp-venv`, car ses scripts contiennent des chemins absolus. Les paquets sont installés avec pip et `--only-binary=:all:`. Les contrôles portent sur `pip check`, les versions de yt-dlp et curl_cffi et la présence d'une cible Chrome dans `yt-dlp --no-config --list-impersonate-targets`.

Les traps EXIT, INT, TERM et HUP assurent le nettoyage et la restauration dans les cas pris en charge. Le venv précédent est conservé sous `.ytdlp-venv.old` jusqu'à validation. En cas d'échec sans ancien venv, le nouveau venv incomplet est supprimé. Cette restauration concerne le venv, pas l'ensemble des composants installés par install.sh.

## Installation et diagnostic

Depuis le dépôt, après installation des dépendances système :

```bash
bash install.sh /chemin/vers/Captvty
```

Pour préparer uniquement T18 depuis les sources :

```bash
bash tools/install-ytdlp.sh /chemin/vers/Captvty
```

Dans le répertoire Captvty :

```bash
python3 --version
.ytdlp-venv/bin/python3 --version
cat .ytdlp-venv/pyvenv.cfg
.ytdlp-venv/bin/python3 -m pip check
.ytdlp-venv/bin/yt-dlp --no-config --version
.ytdlp-venv/bin/yt-dlp --no-config --list-impersonate-targets
```

En cas de présence simultanée des deux répertoires venv, examiner leur contenu avant toute suppression. Ne pas déplacer le runtime privé ou le venv sans reconstruire l'environnement.

## Essais unitaires et manuels de l'installation Python

Les essais suivants ont été réalisés pendant le développement ; les injections de panne ne font pas partie des suites automatisées de 57 tests.

| Essai | Résultat |
|---|---|
| Debian, installation neuve avec Python 3.13.5 | OK, 9,508 s |
| Debian, remplacement du venv | OK, 8,987 s |
| Échec pip avec venv fonctionnel | Code 1, ancien venv restauré |
| Échec pip sans ancien venv | Code 1, cible nettoyée |
| Sauvegarde seule puis échec pip | Restauration correcte |
| Venv et sauvegarde présents | Refus, les deux conservés |
| SIGTERM au shell avant pip | Code 143, restauration |
| SIGINT au shell avant pip | Code 130, restauration |
| SIGTERM au processus pip via strace | Code 1, restauration |
| Ubuntu, installation neuve avec runtime privé | OK, 17,562 s |
| Ubuntu, réinstallation du support T18 | OK, 9,672 s |
| Requête HTTPS avec curl_cffi et impersonation Chrome | HTTP 200, 577 octets |

Les essais de restauration ont contrôlé le fichier témoin, l'inode de pyvenv.cfg, le SHA-256 de l'ancien yt-dlp, sa version et pip check. Les temps des essais Python bénéficient de caches de paquets ; ils ne sont pas des mesures universelles.

## Validation intégrée de la 2.9.2

Date : 25 novembre 2026. Commits `dd49ffb` et `eaf1f59`.

| Environnement | Debian 13 | Ubuntu 22.04.5 LTS |
|---|---|---|
| Python système | 3.13.5 | 3.10.12 |
| Python T18 | 3.13.5 | 3.13.18 privé |
| Mono.Cecil | 0.11.1.0 | 0.11.0.0 |
| mkvmerge | 92 | 65.0.0 |
| Installation complète | 11,781 s (réutilisation) | 152,459 s (neuve) |
| Réinstallation Ubuntu | — | 12,840 s |

Ubuntu : conteneur ubuntu:22.04, glibc 2.35, Mono/mcs 6.8.0.105, FFmpeg 4.4.2, GCC 11.4.0. Les dépendances système ont été installées en root, puis Captvty CLI construit et installé par l'utilisateur non privilégié captvty (UID 1000). MP4Box a été compilé lors de la première installation. Le runtime privé occupait environ 111 Mio et le venv 82 Mio. Sous Debian, le venv seul occupait environ 79 Mio lors de l'essai isolé.

L'archive originale Captvty 3.0.1.28 utilisée sous Ubuntu a été copiée depuis Debian et vérifiée :

```text
526d4823b459a710805ac8e5fa66496001891eddb70dde65edbbed1cf665f1ff  captvty-3.0.1.28.zip
13e04a10367e2e7c7492104a81d110470d8def2cd965037d69d2d5d634f756a7  Captvty.exe
```

### Suites de tests

| Suite | Debian | Ubuntu | Temps Debian / Ubuntu (s) |
|---|---:|---:|---:|
| CLI hors réseau | 29/29 | 29/29 | inclus ci-dessous |
| CLI réseau T18 | 5/5 | 5/5 | 61,186 / 59,526 (CLI complète) |
| HashSet concurrent | 5/5 | 5/5 | 8,505 / 7,411 |
| Résolution du chemin final | 10/10 | 10/10 | 16,304 / 16,309 |
| TUI | 8/8 | 8/8 | 0,004 / 0,006 (temps rapporté par la suite) |
| Total | 57/57 | 57/57 | |

Voir les commandes de reproduction dans le README, section Tests automatisés. Les cinq tests réseau comprennent notamment la mise à jour, la liste, info et les échecs simulés de résolution et de récupération. Ils ne téléchargent pas une vidéo complète.

### Téléchargement réel T18 sous Ubuntu

```bash
mono captvty-cli.exe update T18
mono captvty-cli.exe list T18 ""
mono captvty-cli.exe info T18 "Face à face - Risque imminent"
time mono captvty-cli.exe get T18 "Face à face" "Risque imminent" high
```

Catalogue : 203 replays. Résolution : 1080p, 6222 kb/s annoncé. Téléchargement : 9 min 27,411 s, code retour 0. Fichier `Face_à_face_Risque_imminent_T18_1080p.mp4` : H.264 1920x1080, AAC, durée 2889,287007 s, taille 1 712 491 974 octets. Aucun fichier temporaire correspondant restant. Les caractéristiques concordent avec le téléchargement historique Debian 2.9.1 ; aucun SHA-256 comparatif des vidéos ni décodage intégral n'a été réalisé.

Pendant le téléchargement, aucune progression n'était affichée. L'activité réelle a été confirmée par la croissance du fichier `.mp4.part` et la présence de fragments. Ce défaut d'affichage reste à analyser séparément.

La réinstallation complète Ubuntu a confirmé la réutilisation de MP4Box et du Python privé, la reconstruction du venv depuis les paquets en cache et un code retour 0. Les quatre avertissements CS0649 habituels de TV5MONDE+ sont toujours présents.

## Limites

- Linux Mint 21.3 n'a pas été testé directement ; Ubuntu Docker ne remplace pas un essai sur la machine de Pulsar.
- Le secours Python privé est limité à x86_64 ; aucune validation ARM ni distribution plus ancienne n'a été effectuée.
- Les signaux INT et TERM au shell ont été testés avant pip ; TERM au processus pip a été testé séparément. Signal au shell pendant pip, arrêt simultané des processus, SIGKILL et installations concurrentes non validés.
- Les dépendances transitives pip ne sont pas épinglées ni vérifiées par un manifeste local d'empreintes. La configuration pip héritée n'est pas entièrement neutralisée.
- Pas de validation exhaustive de toutes les chaînes, ni de toutes les qualités vidéo.
- La migration depuis d'anciens moteurs modifiés ou shims n'a pas été validée.
