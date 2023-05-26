@echo off
copy ../config/configDev.toml server_config.toml
move server_config.toml ../../bin/Content.Server/