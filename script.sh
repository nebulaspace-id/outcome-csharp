#!/usr/bin/env bash
#
# NebulaSpace Outcome — build & test script.
#
# Usage:
#   ./script.sh                 # build (Release) + run all tests
#   ./script.sh --watch         # build, then re-run tests on file changes (needs inotifywait)
#   ./script.sh --no-test       # build only
#   ./script.sh --pack          # build + tests, then pack NuGet packages
#   ./script.sh --warn-as-error # treat warnings as errors (CI style)
#   ./script.sh --config Debug  # use a specific configuration (default: Release)
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION="$ROOT/outcome-csharp.slnx"

CONFIG="Release"
WARN_AS_ERROR=false
PACK=false
WATCH=false
RUN_TEST=true

for arg in "$@"; do
    case "$arg" in
        --watch)        WATCH=true ;;
        --no-test)      RUN_TEST=false ;;
        --pack)         PACK=true ;;
        --warn-as-error) WARN_AS_ERROR=true ;;
        --config)       echo "error: --config requires a value (e.g. --config Debug)" >&2; exit 1 ;;
        --config=*)     CONFIG="${arg#*=}" ;;
        -c=*)           CONFIG="${arg#*=}" ;;
        -h|--help)
            sed -n '2,10p' "$0"
            exit 0
            ;;
        -*)
            echo "error: unknown option: $arg" >&2
            sed -n '2,10p' "$0" >&2
            exit 1
            ;;
        *)
            echo "error: unexpected argument: $arg" >&2
            sed -n '2,10p' "$0" >&2
            exit 1
            ;;
    esac
done

cd "$ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "error: 'dotnet' CLI not found on PATH." >&2
    exit 1
fi

if [ ! -f "$SOLUTION" ]; then
    echo "error: solution not found: $SOLUTION" >&2
    exit 1
fi

echo "==> Building '$CONFIG' ..."
if [ "$WARN_AS_ERROR" = true ]; then
    dotnet build "$SOLUTION" -c "$CONFIG" -warnaserror
else
    dotnet build "$SOLUTION" -c "$CONFIG"
fi

if [ "$RUN_TEST" = true ]; then
    echo "==> Running tests ..."
    dotnet test "$SOLUTION" -c "$CONFIG" --no-build
fi

if [ "$PACK" = true ]; then
    echo "==> Packing NuGet packages ..."
    dotnet pack "$SOLUTION" -c "$CONFIG" --no-build -o "$ROOT/artifacts"
    echo "==> Packages written to $ROOT/artifacts"
fi

if [ "$WATCH" = true ]; then
    if ! command -v inotifywait >/dev/null 2>&1; then
        echo "warning: 'inotifywait' not found — watch mode unavailable (install inotify-tools)." >&2
        exit 0
    fi
    echo "==> Watching for changes (Ctrl+C to stop) ..."
    while inotifywait -q -r -e modify -e create -e delete \
        --exclude '/(bin|obj|artifacts)/' "$ROOT/src" "$ROOT/tests"; do
        echo "==> Change detected — rebuilding ..."
        dotnet build "$SOLUTION" -c "$CONFIG"
        dotnet test "$SOLUTION" -c "$CONFIG" --no-build
    done
fi

echo "==> Done."
