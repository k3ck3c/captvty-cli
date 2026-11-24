#!/usr/bin/env bash
set -euo pipefail

VERSION="2.9.0"

die()
{
    echo "ERREUR : $*" >&2
    exit 1
}

echo "Captvty CLI $VERSION - installation"
echo

# Répertoire Captvty : argument ou saisie interactive
if [ "$#" -gt 1 ]; then
    die "Usage : $0 [répertoire-Captvty]"
fi

if [ "$#" -eq 1 ]; then
    CAPTVTY_DIR="$1"
else
    printf "Répertoire de l'installation originale de Captvty 3.0.1.28 : "
    read -r CAPTVTY_DIR
fi

[ -n "$CAPTVTY_DIR" ] ||
    die "Aucun répertoire indiqué."

CAPTVTY_DIR="${CAPTVTY_DIR/#\~/$HOME}"

[ -d "$CAPTVTY_DIR" ] ||
    die "Répertoire introuvable : $CAPTVTY_DIR"

CAPTVTY_DIR="$(cd "$CAPTVTY_DIR" && pwd)"

echo
echo "Vérification de $CAPTVTY_DIR..."

check_file()
{
    [ -f "$CAPTVTY_DIR/$1" ] ||
        die "$1 est absent de $CAPTVTY_DIR"
    printf "  OK  %s\n" "$1"
}

check_dir()
{
    [ -d "$CAPTVTY_DIR/$1" ] ||
        die "$1/ est absent de $CAPTVTY_DIR"
    printf "  OK  %s/\n" "$1"
}

check_command()
{
    command -v "$1" >/dev/null 2>&1 ||
        die "commande '$1' introuvable"
    printf "  OK  %s : %s\n" "$1" "$(command -v "$1")"
}
check_command sha256sum
check_file "Captvty.exe"
CAPTVTY_30128_SHA256="13e04a10367e2e7c7492104a81d110470d8def2cd965037d69d2d5d634f756a7"
CAPTVTY_SHA256=$(sha256sum "$CAPTVTY_DIR/Captvty.exe" | awk '{print $1}')
if [ "$CAPTVTY_SHA256" != "$CAPTVTY_30128_SHA256" ]; then
    echo
    echo "ERREUR : Captvty.exe ne correspond pas au binaire"
    echo "         Captvty 3.0.1.28 attendu par Captvty CLI 2.9.0."
    echo
    echo "SHA256 attendu : $CAPTVTY_30128_SHA256"
    echo "SHA256 trouvé  : $CAPTVTY_SHA256"
    echo
    echo "Utilisez une installation originale de Captvty 3.0.1.28."
    exit 1
fi
echo "  OK  Captvty 3.0.1.28 vérifié (SHA256)"
check_file "Captvty.exe.config"
check_dir  "bin"
check_command mono
check_command mcs
check_command python3
check_command curl
check_command ffmpeg
check_command ffprobe
CECIL=$(find /usr/lib/mono/gac/Mono.Cecil \
    -name Mono.Cecil.dll 2>/dev/null |
    sort -V |
    tail -1)
[ -n "$CECIL" ] ||
    die "Mono.Cecil.dll introuvable"
printf "  OK  Mono.Cecil : %s\n" "$CECIL"
echo
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo
echo "Vérification des sources Captvty CLI..."
for f in \
    patch-dpi-28.cs \
    patch-28-progress.cs \
    patch-arte-hashset-28.cs \
    captvty-provider-28.cs \
    captvty-cli-v2.9.0.cs \
    captvty-tui.py \
    install-media-tools.sh \
    tools/build-mp4box.sh \
    tools/mkvmerge-wrapper.sh
do
    [ -f "$SCRIPT_DIR/$f" ] || die "source manquante : $f"
    printf "  OK  %s\n" "$f"
done

echo
echo "Préparation de la construction..."
BUILD_DIR="$(mktemp -d)"
trap 'rm -rf "$BUILD_DIR"' EXIT
printf "  Répertoire temporaire : %s\n" "$BUILD_DIR"

echo
echo "Compilation des outils de patch..."

for patch in patch-dpi-28 patch-28-progress patch-arte-hashset-28
do
    mcs -r:"$CECIL" \
        -out:"$BUILD_DIR/$patch.exe" \
        "$SCRIPT_DIR/$patch.cs"
done

echo "  OK  outils de patch compilés"

echo
echo "Construction du moteur Captvty CLI..."

mono "$BUILD_DIR/patch-dpi-28.exe" \
    "$CAPTVTY_DIR/Captvty.exe" \
    "$BUILD_DIR/Captvty-cli-engine-28.exe"

mono "$BUILD_DIR/patch-28-progress.exe" \
    "$BUILD_DIR/Captvty-cli-engine-28.exe" \
    "$BUILD_DIR/Captvty-cli-engine-28-progress-tmp.exe"

mono "$BUILD_DIR/patch-arte-hashset-28.exe" \
    "$BUILD_DIR/Captvty-cli-engine-28-progress-tmp.exe" \
    "$BUILD_DIR/Captvty-cli-engine-28-progress.exe"

echo "  OK  moteur Captvty CLI construit"

echo
echo "Compilation du provider..."

mcs -r:System.Core -r:System.Web.Extensions \
    -out:"$BUILD_DIR/captvty-provider-28.exe" \
    "$SCRIPT_DIR/captvty-provider-28.cs"

echo "  OK  provider compilé"

echo
echo "Compilation de Captvty CLI $VERSION..."
mcs -r:System.Core -r:System.Web.Extensions \
    -out:"$BUILD_DIR/captvty-cli.exe" \
    "$SCRIPT_DIR/captvty-cli-v2.9.0.cs"
echo "  OK  Captvty CLI $VERSION compilé"



echo
echo "Vérification du binaire construit..."
BUILT_VERSION="$(mono "$BUILD_DIR/captvty-cli.exe" --version | sed 's/^\xEF\xBB\xBF//')"
[ "$BUILT_VERSION" = "captvty-cli $VERSION" ] || \
    die "version inattendue : $BUILT_VERSION"
printf "  OK  %s\n" "$BUILT_VERSION"

echo
echo "Preparation des outils multimedia..."

bash "$SCRIPT_DIR/install-media-tools.sh" \
    "$CAPTVTY_DIR" "$BUILD_DIR"

echo
echo "Installation dans $CAPTVTY_DIR..."

INSTALL_FILES=(
    Captvty-cli-engine-28-progress.exe
    captvty-provider-28.exe
    captvty-cli.exe
    bin/mp4box/mp4box
    bin/mkvtoolnix/mkvmerge
)

BACKUP_DIR="$CAPTVTY_DIR/captvty-cli-backup-$(date +%Y%m%d-%H%M%S)"
BACKUP_CREATED=0

# Sauvegarde d'une éventuelle installation précédente.
BACKUP_FILES=("${INSTALL_FILES[@]}")

if [ "$SCRIPT_DIR" != "$CAPTVTY_DIR" ]; then
    BACKUP_FILES+=(captvty-tui.py)
fi

for f in "${BACKUP_FILES[@]}"
do
    if [ -e "$CAPTVTY_DIR/$f" ] || [ -L "$CAPTVTY_DIR/$f" ]; then
        if [ "$BACKUP_CREATED" -eq 0 ]; then
            mkdir "$BACKUP_DIR"
            BACKUP_CREATED=1
        fi
        mkdir -p "$BACKUP_DIR/$(dirname "$f")"
        cp -a "$CAPTVTY_DIR/$f" "$BACKUP_DIR/$f"
    fi
done

if [ "$BACKUP_CREATED" -ne 0 ]; then
    printf "  Sauvegarde : %s\n" "$BACKUP_DIR"
fi

# Installation des fichiers construits.
for f in "${INSTALL_FILES[@]}"
do
    DEST="$CAPTVTY_DIR/$f"
    mkdir -p "$(dirname "$DEST")"
    TEMP_DEST="$(mktemp "$(dirname "$DEST")/.captvty-install.XXXXXX")"

    if ! cp -p "$BUILD_DIR/$f" "$TEMP_DEST"; then
        rm -f "$TEMP_DEST"
        die "copie impossible : $f"
    fi

    if ! mv -fT "$TEMP_DEST" "$DEST"; then
        rm -f "$TEMP_DEST"
        die "installation impossible : $f"
    fi
    printf "  OK  %s\n" "$f"
done

# Le TUI est un script source, il n'est pas compilé.
if [ "$SCRIPT_DIR" != "$CAPTVTY_DIR" ]; then
    cp "$SCRIPT_DIR/captvty-tui.py" "$CAPTVTY_DIR/captvty-tui.py"
fi

chmod +x "$CAPTVTY_DIR/captvty-tui.py"
printf "  OK  %s\n" "captvty-tui.py"

echo
echo "Installation du support T18..."

YTDLP_DEST="$CAPTVTY_DIR/.ytdlp-venv"
YTDLP_OLD="$CAPTVTY_DIR/.ytdlp-venv.old"

rm -rf "$YTDLP_OLD"

if [ -d "$YTDLP_DEST" ]; then
    mv "$YTDLP_DEST" "$YTDLP_OLD"
fi

restore_ytdlp()
{
    rm -rf "$YTDLP_DEST"
    if [ -d "$YTDLP_OLD" ]; then
        mv "$YTDLP_OLD" "$YTDLP_DEST"
    fi
}

if ! python3 -m venv "$YTDLP_DEST"; then
    restore_ytdlp
    die "impossible de créer l'environnement Python T18"
fi

if ! "$YTDLP_DEST/bin/pip" install \
    'yt-dlp==2026.10.26' \
    'curl_cffi==0.17.0'; then
    restore_ytdlp
    die "installation de yt-dlp/curl_cffi impossible"
fi

YTDLP_VERSION="$("$YTDLP_DEST/bin/yt-dlp" --version)" || {
    restore_ytdlp
    die "impossible d'exécuter yt-dlp"
}

if [ "$YTDLP_VERSION" != "2026.10.26" ]; then
    restore_ytdlp
    die "version yt-dlp inattendue : $YTDLP_VERSION"
fi

CURL_CFFI_VERSION="$("$YTDLP_DEST/bin/python3" -c \
    'import curl_cffi; print(curl_cffi.__version__)')" || {
    restore_ytdlp
    die "impossible de charger curl_cffi"
}

if [ "$CURL_CFFI_VERSION" != "0.17.0" ]; then
    restore_ytdlp
    die "version curl_cffi inattendue : $CURL_CFFI_VERSION"
fi

if ! "$YTDLP_DEST/bin/yt-dlp" --list-impersonate-targets 2>&1 |
    grep -Eq '^Chrome-.*curl_cffi$'; then
    restore_ytdlp
    die "impersonation Chrome via curl_cffi indisponible"
fi

printf "  OK  yt-dlp %s\n" "$YTDLP_VERSION"
printf "  OK  curl_cffi %s\n" "$CURL_CFFI_VERSION"
printf "  OK  impersonation Chrome disponible\n"

rm -rf "$YTDLP_OLD"

printf "  OK  %s\n" ".ytdlp-venv"

echo
echo "Installation terminée."
echo "Captvty CLI $VERSION est installé dans : $CAPTVTY_DIR"
