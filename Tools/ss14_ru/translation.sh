#!/usr/bin/env sh

if [ "$(dirname $0)" != "." ]; then
    cd "$(dirname $0)"
fi

pip install -r requirements.txt --no-warn-script-location
python3 ./yamlextractor.py
python3 ./keyfinder.py
python3 ./clean_empty.py
