#!/usr/bin/env bash
set -euo pipefail

YTDLP_REQUIRED="2026.10.26"
CURL_CFFI_REQUIRED="0.17.0"

die()
{
    echo "ERREUR : $*" >&2
    exit 1
}

[ "$#" -eq 1 ] ||
    die "Usage : $0 repertoire-Captvty"

[ -d "$1" ] ||
    die "Repertoire introuvable : $1"

CAPTVTY_DIR="$(cd "$1" && pwd)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

YTDLP_DEST="$CAPTVTY_DIR/.ytdlp-venv"
YTDLP_OLD="$CAPTVTY_DIR/.ytdlp-venv.old"

# Refuser les chemins inattendus.
for path in "$YTDLP_DEST" "$YTDLP_OLD"; do
    if [ -L "$path" ] ||
       { [ -e "$path" ] && [ ! -d "$path" ]; }; then
        die "chemin inattendu : $path"
    fi
done

# Recuperation apres une installation interrompue.
if [ -d "$YTDLP_OLD" ]; then
    if [ -d "$YTDLP_DEST" ]; then
        die "venv et sauvegarde presents : verification manuelle necessaire"
    fi

    echo "Restauration de la sauvegarde precedente..."
    mv -T "$YTDLP_OLD" "$YTDLP_DEST"
fi

# Preparer Python avant de toucher au venv existant.
PYTHON_BIN="$("$SCRIPT_DIR/prepare-python.sh" "$CAPTVTY_DIR")" ||
    die "aucun Python compatible disponible"

echo "Python selectionne : $PYTHON_BIN"

VENV_PYTHON="$YTDLP_DEST/bin/python3"
YTDLP="$YTDLP_DEST/bin/yt-dlp"

validate_venv()
(
    "$VENV_PYTHON" -I -c '
import sys
from pathlib import Path

assert sys.version_info >= (3, 11)
assert Path(sys._base_executable).resolve() == Path(sys.argv[1]).resolve()
assert Path(sys.prefix).resolve() == Path(sys.argv[2]).resolve()
assert sys.prefix != sys.base_prefix

cfg = Path(sys.prefix, "pyvenv.cfg").read_text()
settings = dict(
    (k.strip().lower(), v.strip().lower())
    for k, v in (line.split("=", 1) for line in cfg.splitlines() if "=" in line)
)
assert settings.get("include-system-site-packages") == "false"
' "$PYTHON_BIN" "$YTDLP_DEST" ||
        die "Python ou isolation du venv incompatible"

    printf "  OK  Python et isolation du venv\n"

    "$VENV_PYTHON" -I -m pip check ||
        die "dependances Python incompatibles"

    YTDLP_VERSION="$("$YTDLP" --no-config --version)" ||
        die "execution de yt-dlp impossible"

    [ "$YTDLP_VERSION" = "$YTDLP_REQUIRED" ] ||
        die "version yt-dlp inattendue : $YTDLP_VERSION"

    CURL_CFFI_VERSION="$("$VENV_PYTHON" -I -c \
        'import curl_cffi; print(curl_cffi.__version__)')" ||
        die "chargement de curl_cffi impossible"

    [ "$CURL_CFFI_VERSION" = "$CURL_CFFI_REQUIRED" ] ||
        die "version curl_cffi inattendue : $CURL_CFFI_VERSION"

    TARGETS="$("$YTDLP" --no-config --list-impersonate-targets 2>&1)" ||
        die "lecture des profils impersonation impossible"

    grep -Eq '^Chrome-.*curl_cffi$' <<< "$TARGETS" ||
        die "impersonation Chrome indisponible"

    printf "  OK  yt-dlp %s\n" "$YTDLP_VERSION"
    printf "  OK  curl_cffi %s\n" "$CURL_CFFI_VERSION"
    printf "  OK  impersonation Chrome disponible\n"

)

if [ -d "$YTDLP_DEST" ]; then
    echo "Verification du venv existant..."
    if validate_venv; then
        echo "  OK  venv T18 reutilise"
        exit 0
    fi
    echo "Venv non conforme : reconstruction necessaire."
fi

ACTIVE=0
HAD_OLD=0
COMMITTED=0

cleanup()
{
    local rc=$?

    trap - EXIT
    trap '' INT TERM HUP

    if [ "$ACTIVE" -eq 1 ] &&
       [ "$COMMITTED" -eq 0 ]; then

        echo "Installation T18 interrompue ou en echec." >&2

        if [ -d "$YTDLP_OLD" ]; then
            rm -rf -- "$YTDLP_DEST" &&
                mv -T "$YTDLP_OLD" "$YTDLP_DEST" || {
                    echo "ERREUR : restauration impossible" >&2
                    exit 1
                }

            echo "Ancien venv restaure." >&2

        elif [ "$HAD_OLD" -eq 0 ]; then
            rm -rf -- "$YTDLP_DEST" || exit 1
        fi
    fi

    exit "$rc"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP

if [ -d "$YTDLP_DEST" ]; then
    HAD_OLD=1
fi

ACTIVE=1

if [ "$HAD_OLD" -eq 1 ]; then
    mv -T "$YTDLP_DEST" "$YTDLP_OLD"
fi

echo "Creation de l'environnement Python T18..."

"$PYTHON_BIN" -I -m venv "$YTDLP_DEST" ||
    die "creation du venv impossible"

"$VENV_PYTHON" -I -m pip install \
    --only-binary=:all: \
    "yt-dlp==$YTDLP_REQUIRED" \
    "curl_cffi==$CURL_CFFI_REQUIRED" ||
    die "installation de yt-dlp/curl_cffi impossible"

validate_venv || die "venv invalide"

COMMITTED=1

rm -rf -- "$YTDLP_OLD"

printf "  OK  %s\n" ".ytdlp-venv"
