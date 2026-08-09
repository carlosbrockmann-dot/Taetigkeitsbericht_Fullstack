#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ACTIVATE_SCRIPT="$REPO_ROOT/.venv/bin/activate"
PYTHON_EXE="$REPO_ROOT/.venv/bin/python"

if [[ ! -f "$ACTIVATE_SCRIPT" ]]; then
  echo "Virtuelle Umgebung nicht gefunden: $ACTIVATE_SCRIPT" >&2
  exit 1
fi
if [[ ! -f "$PYTHON_EXE" ]]; then
  echo "Python in virtueller Umgebung nicht gefunden: $PYTHON_EXE" >&2
  exit 1
fi

# shellcheck disable=SC1091
source "$ACTIVATE_SCRIPT"

cleanup() {
  if declare -F deactivate >/dev/null; then
    deactivate
  fi
}

trap cleanup EXIT

cd "$REPO_ROOT"
"$PYTHON_EXE" "$SCRIPT_DIR/main.py"
