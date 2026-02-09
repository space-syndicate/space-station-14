game-ticker-restart-round = Перезапуск смены...
game-ticker-start-round = Смена начинается с...
game-ticker-start-round-cannot-start-game-mode-fallback = Не удалось запустить режим { $failedGameMode }! Запускаем { $fallbackMode }...
game-ticker-start-round-cannot-start-game-mode-restart = Не удалось запустить режим { $failedGameMode }! Перезапуск раунда...
game-ticker-start-round-invalid-map = Выбранная карта { $map } не подходит для игрового режима { $mode }. Игровой режим может не функционировать как задумано...
game-ticker-unknown-role = Неизвестный
game-ticker-delay-start = Начало смены было отложено на { $seconds } секунд.
game-ticker-pause-start = Начало смены было приостановлено.
game-ticker-pause-start-resumed = Отсчёт начала смены возобновлён.
game-ticker-player-join-game-message = Добро пожаловать на Project Utopia 14! ВАЖНО! Если вы играете на нашем сервере впервые, обязательно прочтите правила и сюжет. Это необходимо для корректной отыгровки повышенного РП. Подробнее вы можете узнать в нашем дискорде
game-ticker-get-info-text = [color=white]!Обнаружен беженец![/color] Добро пожаловать на - [color=pink]Project Utopia 14![/color]
    Номер текущей смены: [color=white]#{ $roundId }[/color]
    Текущее количество игроков: [color=white]{ $playerCount }[/color]
    Текущая карта: [color=white]{ $mapName }[/color]
    Текущий режим игры: [color=red]{ $gmTitle }[/color]
    >[color=white]{ $desc }[/color]
game-ticker-get-info-preround-text = Добро пожаловать на [color=pink]Project Utopia 14![/color]
    Текущий раунд: [color=white]#{ $roundId }[/color]
    Текущее количество игроков: [color=white]{ $playerCount }[/color] ([color=white]{ $readyCount }[/color] { $readyCount ->
        [one] готов
       *[other] готовы
    })
    Текущая карта: [color=white]{ $mapName }[/color]
    Текущий режим игры: [color=white]{ $gmTitle }[/color]
    >[color=yellow]{ $desc }[/color]
game-ticker-no-map-selected = [color=red]Карта ещё не выбрана![/color]
game-ticker-player-no-jobs-available-when-joining = При попытке присоединиться к игре ни одной роли не было доступно.
# Displayed in chat to admins when a player joins
player-join-message = Игрок { $name } зашёл!
player-first-join-message = Игрок { $name } зашёл на сервер впервые.
# Displayed in chat to admins when a player leaves
player-leave-message = Игрок { $name } вышел!
latejoin-arrival-announcement =
    { $character } ({ $job }) { GENDER($entity) ->
        [male] пробудился..
        [female] пробудилась..
        [epicene] пробудились..
       *[neuter] пробудилось..
    } на станции!
latejoin-arrival-announcement-special = Внимание! { $job } { $character } приступает к работе!
latejoin-arrival-sender = Станции
latejoin-arrivals-direction = Вскоре прибудет шаттл, который доставит вас на станцию.
latejoin-arrivals-direction-time = Шаттл, который доставит вас на станцию, прибудет через { $time }.
latejoin-arrivals-dumped-from-shuttle = Таинственная сила не позволяет вам улететь на шаттле прибытия.
latejoin-arrivals-teleport-to-spawn = Таинственная сила телепортирует вас с шаттла прибытия. Удачной смены!
preset-not-enough-ready-players = Не удалось запустить пресет { $presetName }. Требуется { $minimumPlayers } игроков, но готовы только { $readyPlayersCount }.
preset-no-one-ready = Не удалось запустить режим { $presetName }. Нет готовых игроков.
game-run-level-PreRoundLobby = Предраундовое лобби
game-run-level-InRound = В раунде
game-run-level-PostRound = После раунда
