#!/usr/bin/env bash
set -euo pipefail

VERSION="2.6.0"

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
    printf "Répertoire de l'installation originale de Captvty 3.0.1.26 : "
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

check_file "Captvty.exe"
check_file "Captvty.exe.config"
check_dir  "bin"

check_command mono
check_command mcs

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
    patch-mono-compat.cs \
    patch-captvty-cli.cs \
    patch-bfmtv-hashset.cs \
    patch-m6.cs \
    captvty-provider.cs \
    CefSharp-stub.cs \
    CefSharp-Core-stub.cs \
    CefSharp-WinForms-stub.cs \
    captvty-cli-v2.6.0.cs \
    captvty-tui.py
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

mcs -r:"$CECIL" -r:System.Windows.Forms -r:System.Drawing \
    -out:"$BUILD_DIR/patch-mono-compat.exe" \
    "$SCRIPT_DIR/patch-mono-compat.cs"

mcs -r:"$CECIL" \
    -out:"$BUILD_DIR/patch-captvty-cli.exe" \
    "$SCRIPT_DIR/patch-captvty-cli.cs"

mcs -r:"$CECIL" \
    -out:"$BUILD_DIR/patch-bfmtv-hashset.exe" \
    "$SCRIPT_DIR/patch-bfmtv-hashset.cs"

mcs -r:"$CECIL" \
    -out:"$BUILD_DIR/patch-m6.exe" \
    "$SCRIPT_DIR/patch-m6.cs"

echo "  OK  outils de patch compilés"

echo
echo "Construction du moteur Captvty CLI..."

mono "$BUILD_DIR/patch-mono-compat.exe" \
    "$CAPTVTY_DIR/Captvty.exe" \
    "$BUILD_DIR/Captvty-mono-clean.exe"

mono "$BUILD_DIR/patch-captvty-cli.exe" \
    "$BUILD_DIR/Captvty-mono-clean.exe" \
    "$BUILD_DIR/Captvty-cli-engine-unpatched.exe"

mono "$BUILD_DIR/patch-bfmtv-hashset.exe" \
    "$BUILD_DIR/Captvty-cli-engine-unpatched.exe" \
    "$BUILD_DIR/Captvty-cli-engine.exe"

mono "$BUILD_DIR/patch-m6.exe" \
    "$BUILD_DIR/Captvty-cli-engine.exe" \
    "$BUILD_DIR/Captvty-cli-engine-m6.exe"

echo "  OK  moteur Captvty CLI construit"

echo
echo "Compilation du provider..."
mcs -r:System.Web.Extensions \
    -out:"$BUILD_DIR/captvty-provider.exe" \
    "$SCRIPT_DIR/captvty-provider.cs"
echo "  OK  provider compilé"

echo
echo "Compilation des shims CefSharp..."
mcs -target:library \
    -out:"$BUILD_DIR/CefSharp.dll" \
    "$SCRIPT_DIR/CefSharp-stub.cs"

mcs -target:library \
    -out:"$BUILD_DIR/CefSharp.Core.dll" \
    -r:"$BUILD_DIR/CefSharp.dll" \
    -r:System.Net.Http.dll \
    "$SCRIPT_DIR/CefSharp-Core-stub.cs"

mcs -target:library \
    -out:"$BUILD_DIR/CefSharp.WinForms.dll" \
    -r:"$BUILD_DIR/CefSharp.dll" \
    -r:"$BUILD_DIR/CefSharp.Core.dll" \
    "$SCRIPT_DIR/CefSharp-WinForms-stub.cs"
echo "  OK  shims CefSharp compilés"

echo
echo "Compilation de Captvty CLI $VERSION..."
mcs -r:"$CECIL" \
    -out:"$BUILD_DIR/captvty-cli.exe" \
    "$SCRIPT_DIR/captvty-cli-v2.6.0.cs"
echo "  OK  Captvty CLI $VERSION compilé"



echo
echo "Vérification du binaire construit..."
BUILT_VERSION="$(mono "$BUILD_DIR/captvty-cli.exe" --version | sed 's/^\xEF\xBB\xBF//')"
[ "$BUILT_VERSION" = "captvty-cli $VERSION" ] || \
    die "version inattendue : $BUILT_VERSION"
printf "  OK  %s\n" "$BUILT_VERSION"

echo
echo "Installation dans $CAPTVTY_DIR..."

INSTALL_FILES=(
    Captvty-cli-engine.exe
    Captvty-cli-engine-m6.exe
    captvty-provider.exe
    captvty-cli.exe
    CefSharp.dll
    CefSharp.Core.dll
    CefSharp.WinForms.dll
)

BACKUP_DIR="$CAPTVTY_DIR/captvty-cli-backup-$(date +%Y%m%d-%H%M%S)"
BACKUP_CREATED=0

# Sauvegarde d'une éventuelle installation précédente.
for f in "${INSTALL_FILES[@]}" captvty-tui.py
do
    if [ -e "$CAPTVTY_DIR/$f" ]; then
        if [ "$BACKUP_CREATED" -eq 0 ]; then
            mkdir "$BACKUP_DIR"
            BACKUP_CREATED=1
        fi
        cp -a "$CAPTVTY_DIR/$f" "$BACKUP_DIR/"
    fi
done

if [ "$BACKUP_CREATED" -ne 0 ]; then
    printf "  Sauvegarde : %s\n" "$BACKUP_DIR"
fi

# Installation des fichiers construits.
for f in "${INSTALL_FILES[@]}"
do
    cp "$BUILD_DIR/$f" "$CAPTVTY_DIR/$f"
    printf "  OK  %s\n" "$f"
done

# Le TUI est un script source, il n'est pas compilé.
cp "$SCRIPT_DIR/captvty-tui.py" "$CAPTVTY_DIR/captvty-tui.py"
chmod +x "$CAPTVTY_DIR/captvty-tui.py"
printf "  OK  %s\n" "captvty-tui.py"

echo
echo "Installation terminée."
echo "Captvty CLI $VERSION est installé dans : $CAPTVTY_DIR"
