#!/usr/bin/env bash
set -euo pipefail

die()
{
    echo "ERREUR outils multimedia : $*" >&2
    exit 1
}

[ "$#" -eq 2 ] ||
    die "Usage : $0 repertoire-captvty repertoire-preparation"

CAPTVTY_DIR="$(realpath -- "$1")"
STAGING_DIR="$(realpath -m -- "$2")"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

[ -d "$CAPTVTY_DIR/bin" ] ||
    die "repertoire Captvty invalide"

valid_mp4box()
{
    local candidate="$1"
    local version

    [ -n "$candidate" ] && [ -x "$candidate" ] ||
        return 1

    version="$("$candidate" -version 2>&1)" ||
        return 1

    grep -Fq "MP4Box - GPAC version" <<< "$version"
}

echo "=== Verification de mkvmerge ==="

[ -x /usr/bin/mkvmerge ] ||
    die "installer le paquet mkvtoolnix"

WRAPPER="$SCRIPT_DIR/tools/mkvmerge-wrapper.sh"

"$WRAPPER" --ui-language fr --version ||
    die "wrapper mkvmerge incompatible"

echo "=== Recherche de MP4Box ==="

MP4BOX=""

for candidate in \
    "$CAPTVTY_DIR/bin/mp4box/mp4box" \
    "$(command -v MP4Box || true)" \
    "$(command -v mp4box || true)"
do
    if valid_mp4box "$candidate"; then
        MP4BOX="$candidate"
        break
    fi
done

DEST="$STAGING_DIR/bin/mp4box/mp4box"

if [ -n "$MP4BOX" ]; then
    echo "Reutilisation de $MP4BOX"
    install -D -m 755 "$MP4BOX" "$DEST"
else
    echo "Compilation native de MP4Box"
    bash "$SCRIPT_DIR/tools/build-mp4box.sh" "$DEST"
fi

valid_mp4box "$DEST" ||
    die "MP4Box invalide apres preparation"

install -D -m 755 "$WRAPPER" \
    "$STAGING_DIR/bin/mkvtoolnix/mkvmerge"

echo "=== Outils multimedia prepares ==="
