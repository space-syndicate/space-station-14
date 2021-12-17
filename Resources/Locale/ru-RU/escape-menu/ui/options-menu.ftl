## General stuff

ui-options-title = Игровые настройки
ui-options-tab-graphics = Графика
ui-options-tab-controls = Управление
ui-options-tab-audio = Аудио

ui-options-apply = Применить
ui-options-reset-all = Сбросить всё

## Audio menu

ui-options-master-volume = Основная громкость:
ui-options-midi-volume = MIDI (Инструменты) громкость:
ui-options-ambient-hum = Окружающий шум
ui-options-lobby-music = Музыка в лобби
ui-options-volume-label = Громкость
ui-options-volume-percent = { TOSTRING($volume, "P0") }

## Graphics menu

ui-options-show-held-item = Показать удерживаемый элемент рядом с курсором?
ui-options-vsync = Вертикальная синхронизация
ui-options-fullscreen = Полный экран
ui-options-lighting-label = Качество освещения:
ui-options-lighting-very-low = Очень низкое
ui-options-lighting-low = Низкое
ui-options-lighting-medium = Среднее
ui-options-lighting-high = Высокое
ui-options-scale-label = Масштаб UI:
ui-options-scale-auto = Автоматическое ({ TOSTRING($scale, "P0") })
ui-options-scale-75 = 75%
ui-options-scale-100 = 100%
ui-options-scale-125 = 125%
ui-options-scale-150 = 150%
ui-options-scale-175 = 175%
ui-options-scale-200 = 200%
ui-options-hud-theme = Тема HUD:
ui-options-hud-theme-default = По умолчанию
ui-options-hud-theme-modernized = Модернизированный
ui-options-hud-theme-classic = Классический
ui-options-vp-stretch = Растягивание области просмотра для соответствия игровому окну
ui-options-vp-scale = Исправить масштаб области просмотра: x{ $scale }
ui-options-vp-integer-scaling = Предпочитать целочисленное масштабирование (может вызвать черные полосы/обрезку)
ui-options-vp-integer-scaling-tooltip = Если эта опция включена, область просмотра будет масштабироваться с помощью целочисленного значения
                                        при определенных разрешениях. Хотя это приводит к получению четких текстур, это также часто
                                        означает появление черных полос в верхней/нижней части экрана или то, что часть
                                        часть области просмотра не видна.
ui-options-vp-low-res = Низкое разрешение
ui-options-fps-counter = Показать счетчик FPS

## Controls menu

ui-options-binds-reset-all = Сбросить ВСЕ привязки
ui-options-binds-explanation = ЛКМ чтобы изменить кнопку, ПКМ чтобы убрать кнопку
ui-options-unbound = Пусто
ui-options-bind-reset = Сбросить
ui-options-key-prompt = Нажмите кнопку...

ui-options-header-movement = Перемещение
ui-options-header-interaction-basic = Базовые взаимодействия
ui-options-header-interaction-adv = Продвинутые взаимодействия
ui-options-header-ui = Интерфейс
ui-options-header-misc = Разное
ui-options-header-hotbar = Хотбар
ui-options-header-map-editor = Редактор карт
ui-options-header-dev = Разработка

ui-options-function-move-up = Двигаться вверх
ui-options-function-move-left = Двигаться влево
ui-options-function-move-down = Двигаться вниз
ui-options-function-move-right = Двигаться вправо
ui-options-function-walk = Ходить

ui-options-function-use = Использовать
ui-options-function-wide-attack = Широкая атака
ui-options-function-activate-item-in-hand = Активировать предмет в руке
ui-options-function-alt-activate-item-in-hand = Альтернативная активация предмета в руке
ui-options-function-activate-item-in-world = Активировать предмет в мире
ui-options-function-alt-activate-item-in-world = Альтернативная активация предмета в мире
ui-options-function-drop = Бросить предмет
ui-options-function-examine-entity = Изучить предмет
ui-options-function-swap-hands = Поменять руки местами

ui-options-function-smart-equip-backpack = Умное сняряжение к рюкзаку
ui-options-function-smart-equip-belt = Умное снаряжение на пояс
ui-options-function-throw-item-in-hand = Бросить предмет
ui-options-function-try-pull-object = Тянуть предмет
ui-options-function-move-pulled-object = Переместить вытянутый предмет
ui-options-function-release-pulled-object = Освободить вытянутый объект
ui-options-function-point = Указать на местоположение

ui-options-function-focus-chat-input-window = Сфокусироваться на чат
ui-options-function-focus-local-chat-window = Сфокусироваться на чат (IC)
ui-options-function-focus-radio-window = Сфокусироваться на чат (Радио)
ui-options-function-focus-ooc-window = Сфокусироваться на чат (OOC)
ui-options-function-focus-admin-chat-window = ФСфокусироваться на чат (Администратор)
ui-options-function-focus-dead-chat-window = Сфокусироваться на чат (Мертвые)
ui-options-function-focus-console-chat-window = Сфокусироваться на чат (Консоль)
ui-options-function-cycle-chat-channel-forward = Циклический канал (Вперед)
ui-options-function-cycle-chat-channel-backward = Циклический канал (Назад)
ui-options-function-open-character-menu = Открыть меню символов
ui-options-function-open-context-menu = Открыть контекстное меню
ui-options-function-open-crafting-menu = Открыть меню крафта
ui-options-function-open-inventory-menu = Открыть инвентарь
ui-options-function-open-info = Открыть информацию о сервере
ui-options-function-open-abilities-menu = Открыть меню действий
ui-options-function-open-entity-spawn-window = Открыть меню порождения сущностей
ui-options-function-open-sandbox-window = Открыть меню песочницы
ui-options-function-open-tile-spawn-window = Открыть меню спавна плитки
ui-options-function-open-admin-menu = Открыть меню администратора

ui-options-function-take-screenshot = Сделать снимок экрана
ui-options-function-take-screenshot-no-ui = Сделать снимок экрана (Без UI)

ui-options-function-editor-place-object = Разместить объект
ui-options-function-editor-cancel-place = Отменить размещение
ui-options-function-editor-grid-place = Разместить в сетке
ui-options-function-editor-line-place = Разместить линию
ui-options-function-editor-rotate-object = Повернуть

ui-options-function-open-abilities-menu = Открыть меню действий
ui-options-function-show-debug-console = Открыть консоль
ui-options-function-show-debug-monitors = Отладка
ui-options-function-hide-ui = Скрыть пользовательский интерфейс

ui-options-function-hotbar1 = Слот хотбара 1
ui-options-function-hotbar2 = Слот хотбара 2
ui-options-function-hotbar3 = Слот хотбара 3
ui-options-function-hotbar4 = Слот хотбара 4
ui-options-function-hotbar5 = Слот хотбара 5
ui-options-function-hotbar6 = Слот хотбара 6
ui-options-function-hotbar7 = Слот хотбара 7
ui-options-function-hotbar8 = Слот хотбара 8
ui-options-function-hotbar9 = Слот хотбара 9
ui-options-function-hotbar0 = Слот хотбара 0
ui-options-function-loadout1 = Загрузить хотбар 1
ui-options-function-loadout2 = Загрузить хотбар 2
ui-options-function-loadout3 = Загрузить хотбар 3
ui-options-function-loadout4 = Загрузить хотбар 4
ui-options-function-loadout5 = Загрузить хотбар 5
ui-options-function-loadout6 = Загрузить хотбар 6
ui-options-function-loadout7 = Загрузить хотбар 7
ui-options-function-loadout8 = Загрузить хотбар 8
ui-options-function-loadout9 = Загрузить хотбар 9
