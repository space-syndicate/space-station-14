## Strings for the "grant_connect_bypass" command.

cmd-grant_connect_bypass-desc = Временно разрешить пользователю обход стандартных проверок при подключении.
cmd-grant_connect_bypass-help =
    Использование: grant_connect_bypass <пользователь> [длительность в минутах]
    Временно предоставляет пользователю возможность обходить стандартные ограничения на подключение.
    Этот обход действует только на текущем игровом сервере и истекает через (по умолчанию) 1 час.
    Пользователь сможет подключиться независимо от вайтлиста, паник-режима или лимита игроков.
cmd-grant_connect_bypass-arg-user = <пользователь>
cmd-grant_connect_bypass-arg-duration = [длительность в минутах]
cmd-grant_connect_bypass-invalid-args = Ожидалось 1 или 2 аргумента
cmd-grant_connect_bypass-unknown-user = Не удалось найти пользователя '{ $user }'
cmd-grant_connect_bypass-invalid-duration = Неправильная длительность: '{ $duration }'
cmd-grant_connect_bypass-success = Пользователю '{ $user }' успешно выдан обход
