# Lobby server hub

The lobby server hub displays up to 10 launcher connection buttons from
`Resources/ServerInfo/Corvax/lobby-servers.yml`. The connected game server
proxies current player counts and map names from the public SS14 hub. Entries
are automatically ordered by player count, highest first.

Configure the two groups with entries in display order:

```yml
primary:
  - address: ss14://example.org:1212
subprojects:
  - address: ss14://events.example.org:1212
```

Only absolute `ss14://` and `ss14s://` addresses are accepted. Extra entries
after the tenth are ignored. If the list is empty or contains no valid entries,
the whole block is hidden. Group titles are localized and do not belong in the
YAML. The connected server is omitted when its `hub.server_url` matches a list
entry (a trailing slash is ignored). Display names, player counts, limits, and
maps are loaded from the public Hub. Until a Hub response arrives, the button
uses the address without its URI scheme as a fallback label.

## Porting to another fork

1. Copy this directory, `Content.Shared/Corvax/Lobby`,
   `Content.Server/Corvax/Lobby`, the config file, and the two
   `server-hub.ftl` files.
2. Add the `corvaxLobby` namespace and one `<corvaxLobby:LobbyServerHub />`
   element to that fork's `LobbyGui.xaml`.
3. Keep `IGameController.Redial`; it lets the launcher fetch the target
   server's content build before reconnecting.
