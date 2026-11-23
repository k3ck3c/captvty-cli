# Investigations historiques M6+ et DRM

Ces investigations ont été réalisées avec Captvty 3.0.1.26.

Le fichier patch-m6.cs est conservé dans le dépôt.
Sa compatibilité avec Captvty 3.0.1.28 n'a pas été validée.

Vérifier si un replay utilise des DRM

Pour les chaînes utilisant le backend M6+, un média peut être correctement identifié et téléchargé par Captvty tout en restant protégé par DRM.

Vérification à partir des métadonnées

Avec le moteur instrumenté Captvty-cli-engine-m6-dump.exe, les métadonnées du replay sont enregistrées dans /tmp/m6.json.

Pour afficher les types de flux disponibles et savoir si leur URL indique une protection DRM :

<pre>jq -r '
  .clips[0].assets[]?
  | [
      (.video_container // ""),
      (.video_quality // ""),
      ((.full_physical_path // "") | test("drm"; "i"))
    ]
  | @tsv
' /tmp/m6.json | sort -u
<strong>
m3u8    hd    true
m3u8    sd    true
mpd     hd    true
mpd     sd    true
</strong>
</pre>

true indique ici que l'URL du média contient drm.

Pour afficher également les URL complètes :
<pre>
jq -r '
  .clips[0].assets[]?
  | [
      (.video_container // ""),
      (.video_quality // ""),
      (.full_physical_path // "")
    ]
  | @tsv
' /tmp/m6.json
<strong>
_drm_software.m3u8
_drm_software.mpd
</strong>
</pre>

## Vérification d'un fichier .ts téléchargé

Un téléchargement terminé avec succès ne signifie pas nécessairement que la vidéo est déchiffrée et lisible.

On peut examiner les flux avec ffprobe :
<pre>
ffprobe -v error \
  -show_entries stream=index,codec_name,codec_tag_string,codec_tag \
  -of compact \
  "Vidéos/fichier.ts"
</pre>
Sur les flux HLS protégés observés lors des tests M6+, le flux vidéo utilise le type MPEG-TS 0xDB, correspondant à HLS Sample Encryption.

Ainsi, un fichier .ts peut avoir une structure MPEG-TS valide, contenir des pistes vidéo et audio, et avoir été entièrement téléchargé, tout en conservant une vidéo protégée.

Résultats observés

Au cours des tests effectués avec Captvty 3.0.1.26, des replays de M6, W9 et 6ter ont fourni exclusivement des variantes DRM dans les métadonnées examinées, en HLS (m3u8) comme en DASH (mpd), en SD comme en HD.

Ces observations décrivent les replays testés et peuvent évoluer si les plateformes modifient leurs systèmes de diffusion.

Savoir si une vidéo est avec DRM
