#!/usr/bin/env bash
set -euo pipefail

UV_VERSION="0.13.10"
PYTHON_VERSION="3.13.18"

die()
{
    echo "ERREUR : $*" >&2
    exit 1
}

log()
{
    echo "$*" >&2
}

[ "$#" -eq 1 ] ||
    die "Usage : $0 repertoire-Captvty"

[ -d "$1" ] ||
    die "Répertoire introuvable : $1"

CAPTVTY_DIR="$(cd "$1" && pwd)"
RUNTIME="$CAPTVTY_DIR/.python-runtime"

python_compatible()
(
    local probe

    "$1" -I -c '
import sys
import venv
import ensurepip
sys.exit(0 if sys.version_info >= (3, 11) else 1)
' >/dev/null 2>&1 || return 1

    probe="$(mktemp -d)" || return 1
    trap 'rm -rf "$probe"' EXIT

    "$1" -I -m venv "$probe/venv" \
        >/dev/null 2>&1 || return 1

    "$probe/venv/bin/python3" -I -m pip --version \
        >/dev/null 2>&1 || return 1

    return 0
)

# Priorité au Python disponible dans le PATH.
SYSTEM_PYTHON="$(command -v python3 || true)"

if [ -n "$SYSTEM_PYTHON" ] &&
    python_compatible "$SYSTEM_PYTHON"; then

    log "  OK  Python système : $("$SYSTEM_PYTHON" --version)"
    printf '%s\n' "$SYSTEM_PYTHON"
    exit 0
fi

log "  Python système incompatible ou incomplet."
log "  Recherche de Python privé $PYTHON_VERSION..."

# Réutilisation du Python privé.
PRIVATE_PYTHON=""

if [ -d "$RUNTIME/python" ]; then
    PRIVATE_PYTHON="$(find "$RUNTIME/python" \
        -path "*/cpython-$PYTHON_VERSION-*/bin/python3.13" \
        -type f -print -quit)"
fi

if [ -n "$PRIVATE_PYTHON" ] &&
    python_compatible "$PRIVATE_PYTHON"; then

    log "  OK  Python privé réutilisé"
    printf '%s\n' "$PRIVATE_PYTHON"
    exit 0
fi

# Installation temporaire de uv.
case "$(uname -m)" in
    x86_64)
        UV_TARGET="x86_64-unknown-linux-gnu"
        UV_SHA256="bf65ac1a0af10f92c522d91ac096c7c0a39657647b155577e7836c2cbbb4526c"
        ;;
    *)
        die "architecture non prise en charge pour Python privé : $(uname -m)"
        ;;
esac

mkdir -p "$RUNTIME"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

ARCHIVE="uv-$UV_TARGET.tar.gz"
URL="https://github.com/astral-sh/uv/releases/download/$UV_VERSION/$ARCHIVE"

log "  Téléchargement de uv $UV_VERSION..."

curl -fsSL "$URL" -o "$TMP_DIR/$ARCHIVE" ||
    die "téléchargement de uv impossible"

printf '%s  %s\n' "$UV_SHA256" "$TMP_DIR/$ARCHIVE" |
    sha256sum -c - >&2 ||
    die "SHA256 de uv incorrect"

log "  OK  archive uv vérifiée"

tar -xzf "$TMP_DIR/$ARCHIVE" \
    -C "$TMP_DIR" "uv-$UV_TARGET/uv" ||
    die "extraction de uv impossible"

UV="$TMP_DIR/uv-$UV_TARGET/uv"

[ -x "$UV" ] ||
    die "binaire uv introuvable"

UV_ACTUAL="$("$UV" --version)"

[[ "$UV_ACTUAL" == "uv $UV_VERSION "* ]] ||
    die "version de uv inattendue : $UV_ACTUAL"

# Neutralisation de la configuration uv externe.
for var in ${!UV_@}; do
    unset "$var"
done

export UV_PYTHON_INSTALL_DIR="$RUNTIME/python"
export UV_CACHE_DIR="$TMP_DIR/uv-cache"

log "  Installation de Python $PYTHON_VERSION..."

"$UV" --no-config python install "$PYTHON_VERSION" --no-bin >&2 ||
    die "installation de Python privé impossible"

PRIVATE_PYTHON="$("$UV" --no-config python find "$PYTHON_VERSION" \
    --managed-python)"

python_compatible "$PRIVATE_PYTHON" ||
    die "Python privé incompatible"

log "  OK  Python privé : $("$PRIVATE_PYTHON" --version)"

printf '%s\n' "$PRIVATE_PYTHON"
