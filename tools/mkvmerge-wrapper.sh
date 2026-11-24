#!/bin/bash

args=()

while (( $# > 0 )); do
    case "$1" in
        --ui-language)
            if [[ "${2:-}" == "fr" ]]; then
                args+=(--ui-language fr_FR)
                shift 2
            else
                args+=("$1")
                shift
            fi
            ;;
        --default-track)
            args+=(--default-track-flag)
            shift
            ;;
        *)
            args+=("$1")
            shift
            ;;
    esac
done

exec /usr/bin/mkvmerge "${args[@]}"
