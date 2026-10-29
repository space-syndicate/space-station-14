# Refill Messages
comp-entity-provider-cannot-receive = { CAPITALIZE($refillTarget) } не может быть пополнен!
comp-entity-provider-cannot-transfer = { CAPITALIZE($provider) } не может быть использован для пополнения!
comp-entity-provider-full = { CAPITALIZE(THE($provider)) } уже полон!

# Ejection Messages
comp-entity-provider-no-ejected = Здесь нечего извлекать!

# Menu Messages
comp-entity-provider-select-entity = Выбрать { $entity }.
comp-entity-provider-eject-all-specified-entities = Извлечь все { $entity }.
comp-entity-provider-select-new-active = Выбрать содержащийся предмет.

# Examine Description

comp-entity-provider-no-stored-entities = Пустой.
comp-entity-provider-has-entities = Содержит:
comp-entity-provider-entity-listing = { $amount ->
    [one] [color=yellow]{ $amount }[/color] [color=gray]{ $name }[/color]
    *[other] [color=yellow]{ $amount }[/color] [color=gray]{ $name }[/color]
}
