#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
: "${CLI:?Indiquer CLI=/chemin/vers/captvty-cli.exe}"
CLI=$(realpath -- "$CLI")
export MONO=${MONO:-mono}
MCS=${MCS:-mcs}
TMP=$(mktemp -d)
trap 'rm -rf -- "$TMP"' EXIT
cd "$ROOT"
for name in test-text-matching test-text-matching-escape; do
    "$MCS" -r:System.Core -out:"$TMP/$name.exe" \
        text-matching.cs "tests/$name.cs"
    "$MONO" "$TMP/$name.exe"
done
"$MCS" -out:"$TMP/test-quote.exe" tests/test-quote.cs
"$MONO" "$TMP/test-quote.exe" "$CLI"
printf '\nToutes les suites TextMatching et Quote ont réussi.\n'
