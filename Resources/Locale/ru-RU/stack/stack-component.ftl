### UI

# Показывается, когда стек исследуется в диапазоне деталей
comp-stack-examine-detail-count = {$count ->
    [one] Есть [color={$markupCountColor}]{$count}[/color] вещь.
    * [other] There are [color={$markupCountColor}]{$count}[/color] things
} в стеке.

# Контроль состояния стека
comp-stack-status = Count: [color=white]{$count}[/color]

### Взаимодействие Сообщения

# Показывается при попытке добавить в переполненный стек
comp-stack-already-full = Стек уже заполнен.

# Показывается, когда стек становится полным
comp-stack-becomes-full = Стек стал полным.

# Текст, связанный с разделением стека
comp-stack-split = Вы разделили стек.
comp-stack-split-halve = Разделить пополам
comp-stack-split-too-small = Стек слишком мал для разделения.
