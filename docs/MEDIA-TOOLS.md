# Outils multimédias natifs sous Linux

Captvty 3.0.1.28 — Captvty CLI 2.9.1 — Debian 13 — 24 novembre 2026.
Commit : `bd4bf39`.

## 1. Diagnostic initial

Le téléchargement Arte de Jossi avait récupéré les données vidéo et audio, mais le traitement final échouait dans Process.Start, avec un code retour 255. Le moteur cherchait `bin/mp4box/mp4box`, alors que la distribution Windows fournissait MP4Box.exe. Le paquet GPAC était indisponible dans les dépôts APT configurés lors des essais.

L'analyse du moteur 3.0.1.26 avait identifié le lanceur générique `_Xj::_8Y` et sa machine d'état `_Xj/_rmb::MoveNext`, appelés depuis `_af::_qzB` et `_af/_DTB::MoveNext`. Ces noms sont historiques et ne doivent pas être transposés sans vérification à la 3.0.1.28.

Plutôt que de modifier le lanceur générique ou de simuler MP4Box, nous avons retenu un véritable exécutable Linux, afin de conserver le multiplexage normal.

## 2. Compilation de GPAC

Dépôt : https://github.com/gpac/gpac

- Tag : `v26.07.0`
- Commit vérifié : `a07cbfff238a331233e11e916f9fb185d5da8604`
- Script : `tools/build-mp4box.sh`

```bash
apt-cache policy gpac
command -v MP4Box
command -v mp4box
sudo apt install git build-essential pkg-config \
    zlib1g-dev libfreetype-dev libpng-dev
for lib in zlib freetype2 libpng; do
    pkg-config --static --exists "$lib" && echo "$lib : OK"
done
```

Le script crée un répertoire temporaire, récupère le tag avec un clone de profondeur 1, vérifie le commit, configure et compile GPAC :

```bash
./configure --static-bin
make -j"${GPAC_JOBS:-4}"
```

Le binaire `bin/gcc/MP4Box` est vérifié, installé avec les permissions 755 et son SHA-256 est calculé. Le répertoire temporaire est supprimé par un trap. Le parallélisme est configurable via GPAC_JOBS (4 par défaut).

Résultat observé : `MP4Box - GPAC version 26.07-rev0-ga07cbff-HEAD`, compilation MINI, exécutable statique selon ldd.

SHA-256 du binaire construit sur la machine de test :

```
f42fe36e037c14f5ae32df54e6c7de61d5a3a44807d49c92d8c915f75f3a50b7
```

Cette empreinte ne garantit pas une construction reproductible octet pour octet sur une autre machine.

## 3. MKVToolNix

```bash
sudo apt install mkvtoolnix
/usr/bin/mkvmerge --version
```

Version testée : `mkvmerge v92.0 ('Everglow') 64-bit`.

Le wrapper `tools/mkvmerge-wrapper.sh` est installé sous `bin/mkvtoolnix/mkvmerge` et utilise `/usr/bin/mkvmerge`.

## 4. Préparation et installation

`install-media-tools.sh` reçoit le répertoire Captvty et un répertoire de préparation. Il examine dans l'ordre :

1. `bin/mp4box/mp4box` déjà installé ;
2. `MP4Box` dans PATH ;
3. `mp4box` dans PATH ;
4. compilation de GPAC si aucun candidat valide.

La validation exige un exécutable, le succès de `-version` et la présence de `MP4Box - GPAC version`. Le binaire retenu est copié dans le répertoire de préparation et vérifié. Cette vérification ne garantit pas toutes les fonctions de GPAC. Le script vérifie aussi `/usr/bin/mkvmerge` et le wrapper avec `--ui-language fr --version`.

L'installateur prépare les outils avant leur installation. Les six fichiers concernés sont :

```
Captvty-cli-engine-28-progress.exe
captvty-provider-28.exe
captvty-cli.exe
captvty-tui.py
bin/mp4box/mp4box
bin/mkvtoolnix/mkvmerge
```

Captvty.exe original reste intact. Les fichiers existants sont sauvegardés avec leurs chemins relatifs et leurs éventuels liens symboliques. L'installation utilise des fichiers temporaires puis un renommage atomique par fichier, et non une transaction atomique globale.

## 5. Vérification locale

```bash
TARGET="$HOME/Téléchargements/captvty-3.0.1.28"
cd "$TARGET"
find bin -type f -name mp4box | head -1
find bin -type f -name mkvmerge | head -1
file bin/mp4box/mp4box bin/mkvtoolnix/mkvmerge
bin/mp4box/mp4box -version
bin/mkvtoolnix/mkvmerge --ui-language fr --version
ldd bin/mp4box/mp4box
sha256sum bin/mp4box/mp4box
```

La première réinstallation a réussi en 8,493 s, avec réutilisation de MP4Box et empreinte inchangée. La réinstallation définitive 2.9.1 a réussi en 9,057 s, code 0, avec sauvegarde `captvty-cli-backup-20261124-192108`.

## 6. Validation Arte multipiste

Le premier épisode de Jossi a produit les états 0, 1, 5, 8, 7 et le fichier :

```
Jossi (1-6)_Arte_2026_11_12_20_59_FR-DE-FR-FR-DE_ST_1080p_4208k.mkv
```

| Caractéristique | Mesure |
|---|---:|
| Taille | 446910600 octets |
| Durée | 1339,120 s |
| Résolution | 1920x1080 |
| Vidéo H264 | 1 piste |
| Audio AAC | 5 pistes |
| Sous-titres | 4 pistes |
| Total | 10 pistes |

MPV identifie les sous-titres WebVTT : français et allemand, dont variantes forcées et pour malentendants. Le provider annonçait initialement un MP4 alors que seul le MKV final existait. Voir [Résolution du chemin final](FINAL-DOWNLOAD-PATH.md).

La commande exacte du premier téléchargement n'a pas été conservée dans ce document. Pour reproduire, consulter le catalogue avec `list "Arte" "Jossi"` et fournir le titre et le sous-titre éditorial exacts à `get`.

```bash
VIDEO=$(find "$TARGET/Vidéos" -type f -name 'Jossi*1-6*.mkv' | head -1)
printf '%s\n' "$VIDEO"
ffprobe -v error -show_entries format=duration,size,bit_rate \
    -of default=noprint_wrappers=1 "$VIDEO"
ffprobe -v error -show_entries \
    stream=index,codec_type,codec_name,width,height:stream_tags=language,title \
    -of json "$VIDEO"
mkvmerge -J "$VIDEO"
mpv --no-config "$VIDEO"
```

## 7. Incident MPV indépendant

La lecture initiale présentait une image noire ou figée. `mpv --no-config` fonctionnait, avec ou sans `--hwdec=no`. La désactivation du décodage matériel seule ne suffisait pas. `/etc/mpv/mpv.conf` était absent. Les options locales fs, keep-open, sub-font-size et sub-pos réintroduites avec --no-config fonctionnaient.

Le fichier `~/.config/mpv/input.conf` contenait les raccourcis des essais CStar :

```
RIGHT frame-step
LEFT frame-back-step
```

`mpv --input-conf=/dev/null "$VIDEO"` fonctionnait. Le renommage suivant a rétabli la lecture normale :

```bash
mv ~/.config/mpv/input.conf ~/.config/mpv/input.conf.disabled
```

Pour réutiliser les raccourcis CStar :

```bash
mpv --input-conf="$HOME/.config/mpv/input.conf.disabled" "$VIDEO"
```

Le mécanisme exact de l'incident n'a pas été établi. Aucun changement du MKV ou du moteur n'a été nécessaire.

## 8. Validation MP4 définitive

```bash
time mono captvty-cli.exe get \
    "Arte" \
    "Documentaire : Wolf Biermann, un chansonnier au destin hors pair" \
    "" high
```

Le paramètre sous-titre vide est obligatoire. Son omission retourne 2.

Résultat : MP4, 48550985 octets, 129,880 s, HEVC 1920x1080, une piste AAC, aucun sous-titre, 18,069 s, code retour 0. L'estimation de 74 Mo n'était pas une garantie de taille finale.

## 9. Limites et références

Les essais ne couvrent pas toutes les fonctions GPAC, chaînes, distributions ou architectures. ffprobe vérifie les métadonnées et pistes, pas le décodage exhaustif.

- [README](../README.md)
- [HashSet Arte](ARTE-HASHSET-3.0.1.28.md)
- [Chemin final](FINAL-DOWNLOAD-PATH.md)
