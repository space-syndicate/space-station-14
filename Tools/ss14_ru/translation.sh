#!/usr/bin/env sh

# make sure to start from script dir
if [ "$(dirname "$0")" != "." ]; then
    cd "$(dirname "$0")"
fi

# pick Python and Pip in a way that respects a venv if active
if command -v python3 >/dev/null 2>&1; then
    PY=python3
else
    PY=python
fi

if "$PY" -m pip --version >/dev/null 2>&1; then
    PIP="$PY -m pip"
elif command -v pip >/dev/null 2>&1; then
    PIP=pip
elif command -v pip3 >/dev/null 2>&1; then
    PIP=pip3
else
    echo "Warning: pip not found; skipping dependency install" >&2
    PIP=""
fi

if [ -n "$PIP" ]; then
    $PIP install -r requirements.txt --no-warn-script-location
fi

$PY ./yamlextractor.py
$PY ./keyfinder.py
$PY ./clean_duplicates.py
$PY ./clean_empty.py
