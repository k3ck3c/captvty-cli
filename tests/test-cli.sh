#!/usr/bin/env bash

set -u

CLI="${CLI:-./captvty-cli.exe}"
MONO="${MONO:-mono}"

PASS=0
FAIL=0

run_test()
{
    name="$1"
    expected_rc="$2"
    expected_text="$3"
    shift 3

    set +e
    output=$("$MONO" "$CLI" "$@" 2>&1)
    rc=$?
    set -e

    if [ "$rc" -eq "$expected_rc" ] &&
       grep -Fq -- "$expected_text" <<<"$output"
    then
        printf 'PASS  %s\n' "$name"
        PASS=$((PASS + 1))
    else
        printf 'FAIL  %s\n' "$name"
        printf '      commande :'
        printf ' %q' "$MONO" "$CLI" "$@"
        printf '\n'
        printf '      rc attendu=%s obtenu=%s\n' "$expected_rc" "$rc"
        printf '      texte attendu: %s\n' "$expected_text"
        printf '      sortie:\n%s\n' "$output"
        FAIL=$((FAIL + 1))
    fi
}

run_cached_search_without_runtime_test()
{
    name="search cache sans runtime"
    tmp=$(mktemp -d)

    cp "$CLI" "$tmp/captvty-cli.exe"

    cat > "$tmp/captvty-cache.json" <<'EOF'
{
  "version": 2,
  "programs": [
    {"channel":"TEST TV","title":"TEST-CACHE émission","subtitle":"Sous-titre"}
  ]
}
EOF

    set +e
    output=$(
        cd "$tmp" &&
        "$MONO" ./captvty-cli.exe search "TEST-CACHE" 2>&1
    )
    rc=$?
    set -e

    rm -rf "$tmp"

    if [ "$rc" -eq 0 ] &&
       grep -Fq -- "TEST TV - TEST-CACHE émission" <<<"$output" &&
       grep -Fq -- "1 chaîne(s)" <<<"$output"
    then
        printf 'PASS  %s\n' "$name"
        PASS=$((PASS + 1))
    else
        printf 'FAIL  %s\n' "$name"
        printf '      rc attendu=0 obtenu=%s\n' "$rc"
        printf '      sortie:\n%s\n' "$output"
        FAIL=$((FAIL + 1))
    fi
}

run_missing_dependency_test()
{
    name="dépendance CefSharp.Core manquante"
    tmp=$(mktemp -d)

    cp "$CLI" "$tmp/captvty-cli.exe"
    cp Captvty-cli-engine.exe "$tmp/"
    cp captvty-provider.exe "$tmp/"
    touch "$tmp/CefSharp.dll"
    touch "$tmp/CefSharp.WinForms.dll"

    set +e
    output=$(
        cd "$tmp" &&
        "$MONO" ./captvty-cli.exe get "France 2" "x" "y" high 2>&1
    )
    rc=$?
    set -e

    rm -rf "$tmp"

    if [ "$rc" -eq 2 ] &&
       grep -Fq -- "ERREUR: dépendance runtime introuvable: CefSharp.Core.dll" <<<"$output"
    then
        printf 'PASS  %s\n' "$name"
        PASS=$((PASS + 1))
    else
        printf 'FAIL  %s\n' "$name"
        printf '      rc attendu=2 obtenu=%s\n' "$rc"
        printf '      sortie:\n%s\n' "$output"
        FAIL=$((FAIL + 1))
    fi
}

run_audio_size_test()
{
    name="estimation taille avec pistes audio"
    tmp=$(mktemp -d)

    cp "$CLI" "$tmp/captvty-cli.exe"
    cp tests/fake-audio-provider.cs "$tmp/"

    touch "$tmp/Captvty-cli-engine.exe"
    touch "$tmp/CefSharp.dll"
    touch "$tmp/CefSharp.Core.dll"
    touch "$tmp/CefSharp.WinForms.dll"

    mcs -out:"$tmp/captvty-provider.exe" "$tmp/fake-audio-provider.cs"

    set +e
    output=$(
        cd "$tmp" &&
        "$MONO" ./captvty-cli.exe info "TEST TV" "audio" 2>&1
    )
    rc=$?
    set -e

    rm -rf "$tmp"

    count104=$(grep -Fc -- "~104 Mo (estimé)" <<<"$output" || true)
    count129=$(grep -Fc -- "~129 Mo (estimé)" <<<"$output" || true)

    if [ "$rc" -eq 0 ] &&
       [ "$count104" -eq 2 ] &&
       [ "$count129" -eq 1 ]
    then
        printf 'PASS  %s\n' "$name"
        PASS=$((PASS + 1))
    else
        printf 'FAIL  %s\n' "$name"
        printf '      rc attendu=0 obtenu=%s\n' "$rc"
        printf '      ~104 Mo attendu 2 fois, obtenu %s\n' "$count104"
        printf '      ~129 Mo attendu 1 fois, obtenu %s\n' "$count129"
        printf '      sortie:\n%s\n' "$output"
        FAIL=$((FAIL + 1))
    fi
}

echo "=== Tests Captvty CLI ==="

# ------------------------------------------------------------
# Tests purs du parseur CLI : aucun accès réseau nécessaire.
# ------------------------------------------------------------

run_test \
    "aucun argument" \
    2 \
    "Usage:"


run_test \
    "commande inconnue" \
    2 \
    "Commande inconnue: turlututu" \
    turlututu


run_test \
    "search sans texte" \
    2 \
    'Usage: mono captvty-cli.exe search [--live] "texte"' \
    search


run_test \
    "list sans arguments" \
    2 \
    'Usage: mono captvty-cli.exe list "chaîne" "texte"' \
    list


run_test \
    "info sans arguments" \
    2 \
    'Usage: mono captvty-cli.exe info ["chaîne"] "texte"' \
    info


run_test \
    "get sans arguments" \
    2 \
    'Usage: mono captvty-cli.exe get "chaîne" "titre" "sous-titre" high|low|N' \
    get


# ------------------------------------------------------------
run_test \
    "--version" \
    0 \
    "captvty-cli 2.6.0" \
    --version


# Tests de non-régression --timeout.
#
# Ces tests DOIVENT échouer avec la v2.5.0 actuelle :
# --timeout a disparu accidentellement du parseur.
# ------------------------------------------------------------

run_test \
    "--timeout non numérique" \
    2 \
    "ERREUR: --timeout attend un nombre de secondes >= 0" \
    --timeout toto list "France 2" journal


run_test \
    "--timeout négatif" \
    2 \
    "ERREUR: --timeout attend un nombre de secondes >= 0" \
    --timeout -1 list "France 2" journal


run_test \
    "--timeout sans valeur" \
    2 \
    "ERREUR: --timeout attend un nombre de secondes >= 0" \
    --timeout

run_test \
    "--timeout 0 accepté" \
    2 \
    "Commande inconnue: turlututu" \
    --timeout 0 turlututu

run_test \
    "--timeout 60 accepté" \
    2 \
    "Commande inconnue: turlututu" \
    --timeout 60 turlututu

run_missing_dependency_test
run_cached_search_without_runtime_test
run_audio_size_test

echo
echo "=== Résultat ==="
echo "PASS: $PASS"
echo "FAIL: $FAIL"

if [ "$FAIL" -ne 0 ]; then
    exit 1
fi

exit 0
