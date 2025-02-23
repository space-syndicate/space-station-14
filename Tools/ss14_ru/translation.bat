@echo off

call pip install -r requirements.txt --no-warn-script-location
call py ./yamlextractor.py
call py ./keyfinder.py
call py ./clean_duplicates.py
call py ./clean_empty.py

PAUSE
