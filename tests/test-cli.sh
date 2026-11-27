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

    cat > "$tmp/captvty-cache-28.json" <<'EOF'
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
    touch "$tmp/Captvty-cli-engine-28-progress.exe"
    touch "$tmp/captvty-provider-28.exe"
    mkdir -p "$tmp/bin/cefsharp"
    touch "$tmp/bin/cefsharp/CefSharp.dll"
    touch "$tmp/bin/cefsharp/CefSharp.WinForms.dll"

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


run_broken_pipe_tests()
{
    local tmp name rc output expected marker
    local -a status

    tmp=$(mktemp -d)

    cp "$CLI" "$tmp/captvty-cli.exe"

    touch "$tmp/Captvty-cli-engine-28-progress.exe"

    mkdir -p "$tmp/bin/cefsharp"

    touch "$tmp/bin/cefsharp/"{CefSharp.dll,CefSharp.Core.dll,CefSharp.WinForms.dll}

    if ! mcs -out:"$tmp/captvty-provider-28.exe" \
        tests/fake-get-provider.cs
    then
        printf 'FAIL  compilation faux provider get\n'
        FAIL=$((FAIL + 1))
        rm -rf "$tmp"
        return
    fi

    for name in normal pipe closed timeout diskfull pipe-timeout external-cwd
    do
        rm -f "$tmp/fake-download-complete.txt"
        rm -f "$tmp/stdout.txt" "$tmp/stderr.txt"

        set +e

        case "$name" in
            normal)
                (
                    cd "$tmp" &&
                    "$MONO" ./captvty-cli.exe get \
                        "TEST TV" "Test vidéo" "Test" high
                ) > "$tmp/stdout.txt" 2> "$tmp/stderr.txt"

                rc=$?
                expected=0
                marker=present
                ;;

            pipe)
                (
                    cd "$tmp" &&
                    "$MONO" ./captvty-cli.exe get \
                        "TEST TV" "Test vidéo" "Test" high
                ) 2> "$tmp/stderr.txt" |
                    head -3 > "$tmp/stdout.txt"

                status=("${PIPESTATUS[@]}")
                rc=${status[0]}
                expected=0
                marker=present
                ;;

            closed)
                (
                    cd "$tmp" &&
                    "$MONO" ./captvty-cli.exe get \
                        "TEST TV" "Test vidéo" "Test" high
                ) > >(:) 2> "$tmp/stderr.txt"

                rc=$?
                expected=141
                marker=absent
                ;;

            timeout)
                (
                    cd "$tmp" &&
                    "$MONO" ./captvty-cli.exe --timeout 1 get \
                        "TEST TV" "Test vidéo" "Test" high
                ) > "$tmp/stdout.txt" 2> "$tmp/stderr.txt"

                rc=$?
                expected=1
                marker=absent
                ;;

            diskfull)
                (
                    cd "$tmp" &&
                    "$MONO" ./captvty-cli.exe get \
                        "TEST TV" "Test vidéo" "Test" high
                ) > /dev/full 2> "$tmp/stderr.txt"

                rc=$?
                expected=1
                marker=absent
                ;;

            external-cwd)
                mkdir -p "$tmp/caller"
                (
                    cd "$tmp/caller" &&
                    "$MONO" "$tmp/captvty-cli.exe" get \
                        "TEST TV" "Test vidéo" "Test" high
                ) > "$tmp/stdout.txt" 2> "$tmp/stderr.txt"

                rc=$?
                expected=0
                marker=present
                ;;

            pipe-timeout)
                (
                    cd "$tmp" &&
                    "$MONO" ./captvty-cli.exe --timeout 1 get \
                        "TEST TV" "Test vidéo" "Test" high
                ) 2> "$tmp/stderr.txt" |
                    head -3 > "$tmp/stdout.txt"

                status=("${PIPESTATUS[@]}")
                rc=${status[0]}
                expected=1
                marker=absent
                ;;
        esac

        set -e

        local actual_marker=absent

        if [ -f "$tmp/fake-download-complete.txt" ]; then
            actual_marker=present
        fi

        if [ "$rc" -eq "$expected" ] &&
           [ ! -f "$tmp/caller/fake-download-complete.txt" ] &&
           [ "$actual_marker" = "$marker" ] &&
           ! grep -Eq 'Unhandled Exception|FATAL UNHANDLED' \
               "$tmp/stderr.txt"
        then
            printf 'PASS  broken pipe : %s\n' "$name"
            PASS=$((PASS + 1))
        else
            printf 'FAIL  broken pipe : %s\n' "$name"
            printf '      rc attendu=%s obtenu=%s\n' \
                "$expected" "$rc"
            printf '      témoin attendu=%s obtenu=%s\n' \
                "$marker" "$actual_marker"
            cat "$tmp/stderr.txt"
            FAIL=$((FAIL + 1))
        fi
    done

    rm -rf "$tmp"
}

run_audio_size_test()
{
    name="estimation taille avec pistes audio"
    tmp=$(mktemp -d)

    cp "$CLI" "$tmp/captvty-cli.exe"
    cp tests/fake-audio-provider.cs "$tmp/"

    touch "$tmp/Captvty-cli-engine-28-progress.exe"
    mkdir -p "$tmp/bin/cefsharp"
    touch "$tmp/bin/cefsharp/CefSharp.dll"
    touch "$tmp/bin/cefsharp/CefSharp.Core.dll"
    touch "$tmp/bin/cefsharp/CefSharp.WinForms.dll"

    mcs -out:"$tmp/captvty-provider-28.exe" "$tmp/fake-audio-provider.cs"

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

run_channel_search_tests()
{
    local tmp
    local saved_cli="$CLI"

    tmp=$(mktemp -d)

    cp "$CLI" "$tmp/captvty-cli.exe"

    cat > "$tmp/captvty-cache-28.json" <<'EOF'
{
  "version": 2,
  "programs": [
    {"channel":"France 2","title":"TEST Série A","subtitle":"Episode 1"},
    {"channel":"France 2","title":"TEST Série A","subtitle":"Episode 2"},
    {"channel":"France 2","title":"TEST Série B","subtitle":"Episode 1"},
    {"channel":"France 3","title":"TEST Série C","subtitle":"Episode 1"}
  ]
}
EOF

    pushd "$tmp" >/dev/null
    CLI="./captvty-cli.exe"

    run_test \
        "search cache plusieurs titres même chaîne" \
        0 \
        "France 2 - TEST Série B" \
        search TEST

    run_test \
        "search cache regroupement des épisodes" \
        0 \
        "France 2 (2) - TEST Série A" \
        search TEST

    run_test \
        "search --channel France 2" \
        0 \
        "France 2 (2) - TEST Série A" \
        search --channel "France 2" TEST

    run_test \
        "search --channel insensible à la casse" \
        0 \
        "France 2 - TEST Série B" \
        search --channel "france 2" TEST

    run_test \
        "search --channel exclut les autres chaînes" \
        0 \
        "aucun résultat" \
        search --channel "France 2" "Série C"

    popd >/dev/null
    CLI="$saved_cli"
    rm -rf "$tmp"
}

run_t18_network_tests()
{
    echo
    echo "=== Tests réseau T18 ==="

    name="T18 update"
    set +e
    output=$("$MONO" "$CLI" update T18 2>&1)
    rc=$?
    set -e

    if [ "$rc" -eq 0 ] &&
       grep -Fq -- "T18" <<<"$output" &&
       grep -Eq 'T18.*OK \([1-9][0-9]*\)' <<<"$output"
    then
        printf 'PASS  %s\n' "$name"
        PASS=$((PASS + 1))
    else
        printf 'FAIL  %s\n' "$name"
        printf '      rc=%s\n' "$rc"
        printf '      sortie:\n%s\n' "$output"
        FAIL=$((FAIL + 1))
        return
    fi

    local cache title subtitle selected

    cache="$(dirname "$CLI")/captvty-cache-28.json"

    if ! selected=$(python3 - "$cache" <<'PYTHON'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as f:
    data = json.load(f)

for p in data["programs"]:
    if (p["channel"] == "T18"
        and p.get("title", "").strip()
        and p.get("subtitle", "").strip()):
        print(p["title"])
        print(p["subtitle"])
        break
else:
    sys.exit("Aucun épisode T18 disponible")
PYTHON
    ); then
        printf 'FAIL  Sélection dynamique T18\n'
        FAIL=$((FAIL + 1))
        return
    fi

    title=$(sed -n '1p' <<<"$selected")
    subtitle=$(sed -n '2p' <<<"$selected")

    printf '      Titre      : %s\n' "$title"
    printf '      Sous-titre : %s\n' "$subtitle"

    name="T18 list"
    set +e
    output=$("$MONO" "$CLI" list T18 "$title" 2>&1)
    rc=$?
    set -e

    if [ "$rc" -eq 0 ] &&
       grep -Fq -- "$subtitle" <<<"$output"
    then
        printf 'PASS  %s\n' "$name"
        PASS=$((PASS + 1))
    else
        printf 'FAIL  %s\n' "$name"
        printf '      rc=%s\n' "$rc"
        printf '      sortie:\n%s\n' "$output"
        FAIL=$((FAIL + 1))
    fi

    name="T18 info"
    set +e
    output=$("$MONO" "$CLI" info T18 "$title - $subtitle" 2>&1)
    rc=$?
    set -e

    if [ "$rc" -eq 0 ] &&
       grep -Fq -- "$subtitle" <<<"$output" &&
       grep -Eq -- '[0-9]+x[0-9]+[[:space:]]+[0-9]+p' <<<"$output"
    then
        printf 'PASS  %s\n' "$name"
        PASS=$((PASS + 1))
    else
        printf 'FAIL  %s\n' "$name"
        printf '      rc=%s\n' "$rc"
        printf '      sortie:\n%s\n' "$output"
        FAIL=$((FAIL + 1))
    fi

    name="T18 get échec résolution propre"
    set +e
    output=$(
        CAPTVTY_TEST_T18_RESOLVE_FAIL=1 \
        "$MONO" "$CLI" \
            get T18 \
            "$title" \
            "$subtitle" \
            low 2>&1
    )
    rc=$?
    set -e

    if [ "$rc" -eq 7 ] &&
       grep -Fq -- \
           "Première résolution T18 en échec:" <<<"$output" &&
       grep -Fq -- \
           "Résolution T18 en échec après deux essais:" <<<"$output" &&
       ! grep -Fq -- "Unhandled Exception" <<<"$output"
    then
        printf 'PASS  %s\n' "$name"
        PASS=$((PASS + 1))
    else
        printf 'FAIL  %s\n' "$name"
        printf '      rc attendu=7 obtenu=%s\n' "$rc"
        printf '      sortie:\n%s\n' "$output"
        FAIL=$((FAIL + 1))
    fi

    name="T18 get échec récupération propre"
    set +e
    output=$(
        CAPTVTY_TEST_T18_FETCH_FAIL=1 \
        "$MONO" "$CLI" \
            get T18 \
            "$title" \
            "$subtitle" \
            low 2>&1
    )
    rc=$?
    set -e

    if [ "$rc" -eq 7 ] &&
       grep -Fq --            "Recherche T18 impossible:" <<<"$output" &&
       grep -Fq --            "[TEST] échec simulé de récupération T18" <<<"$output" &&
       ! grep -Fq -- "Unhandled Exception" <<<"$output" &&
       ! grep -Fq -- "FATAL UNHANDLED EXCEPTION" <<<"$output"
    then
        printf 'PASS  %s\n' "$name"
        PASS=$((PASS + 1))
    else
        printf 'FAIL  %s\n' "$name"
        printf '      rc attendu=7 obtenu=%s\n' "$rc"
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
    'Usage: mono captvty-cli.exe search [--live] [--channel "chaîne"] "texte"' \
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
    "captvty-cli 2.9.3" \
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

run_test \
    "search --channel sans valeur" \
    2 \
    "ERREUR: --channel attend un nom de chaîne" \
    search --channel

run_test \
    "search --channel suivi d'une option" \
    2 \
    "ERREUR: --channel attend un nom de chaîne" \
    search --channel --live TEST

run_test \
    "search --channel sans texte" \
    2 \
    "Usage:" \
    search --channel "France 2"

run_channel_search_tests


run_missing_dependency_test
run_cached_search_without_runtime_test
run_audio_size_test
run_broken_pipe_tests

if [ "${TEST_NETWORK:-0}" = "1" ]; then
    run_t18_network_tests
else
    echo
    echo "Tests réseau T18 ignorés (TEST_NETWORK=1 pour les activer)"
fi

echo
echo "=== Résultat ==="
echo "PASS: $PASS"
echo "FAIL: $FAIL"

if [ "$FAIL" -ne 0 ]; then
    exit 1
fi

exit 0
