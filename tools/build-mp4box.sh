#!/usr/bin/env bash
set -euo pipefail

GPAC_TAG="v26.07.0"
GPAC_COMMIT="a07cbfff238a331233e11e916f9fb185d5da8604"
GPAC_URL="https://github.com/gpac/gpac.git"

die()
{
    echo "ERREUR MP4Box : $*" >&2
    exit 1
}

[ "$#" -eq 1 ] ||
    die "Usage : $0 chemin-executable-destination"

DEST="$(realpath -m -- "$1")"

for cmd in git gcc g++ make pkg-config install
do
    command -v "$cmd" >/dev/null 2>&1 ||
        die "commande manquante : $cmd"
done

for lib in zlib freetype2 libpng
do
    pkg-config --static --exists "$lib" ||
        die "bibliothèque de développement manquante : $lib"
done

BUILD_DIR="$(mktemp -d)"
trap 'rm -rf "$BUILD_DIR"' EXIT

echo "Récupération de GPAC $GPAC_TAG..."

git clone --depth 1 --branch "$GPAC_TAG" \
    "$GPAC_URL" "$BUILD_DIR/gpac"

cd "$BUILD_DIR/gpac"

ACTUAL_COMMIT="$(git rev-parse HEAD)"

[ "$ACTUAL_COMMIT" = "$GPAC_COMMIT" ] ||
    die "commit GPAC inattendu : $ACTUAL_COMMIT"

echo "Configuration de GPAC..."

./configure --static-bin

echo "Compilation de MP4Box..."

make -j"${GPAC_JOBS:-4}"

MP4BOX="$PWD/bin/gcc/MP4Box"

[ -x "$MP4BOX" ] ||
    die "MP4Box absent après compilation"

VERSION_OUTPUT="$("$MP4BOX" -version 2>&1)" ||
    die "MP4Box ne fonctionne pas"

grep -Fq "GPAC version 26.07" <<< "$VERSION_OUTPUT" ||
    die "version GPAC inattendue"

install -D -m 755 "$MP4BOX" "$DEST"

echo
echo "MP4Box installé : $DEST"
sha256sum "$DEST"
