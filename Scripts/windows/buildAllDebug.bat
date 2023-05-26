@echo off
cd ../../
if exist sloth.txt (
    call python RUN_THIS.py
    call git submodule update --init --recursive
    call dotnet build -c Debug
) else (
    exit
)
pause
