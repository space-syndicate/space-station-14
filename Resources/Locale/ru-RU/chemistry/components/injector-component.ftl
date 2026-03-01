## UI

injector-volume-transfer-label = Volume: [color=white]{$currentVolume}/{$totalVolume}u[/color]
    Mode: [color=white]{$modeString}[/color] ([color=white]{$transferVolume}u[/color])
injector-volume-label =
    Объём: [color=white]{ $currentVolume }/{ $totalVolume }[/color]
    Режим: [color=white]{ $modeString }[/color] ([color=white]{ $transferVolume } ед.[/color])
injector-toggle-verb-text = Toggle Injector Mode

## Entity

injector-component-inject-mode-name = inject
injector-component-draw-mode-name = draw
injector-component-dynamic-mode-name = dynamic
injector-component-mode-changed-text = Now {$mode}
injector-component-transfer-success-message = Вы перемещаете { $amount } ед. в { $target }.
injector-component-transfer-success-message-self = Вы перемещаете в себя { $amount } ед.
injector-component-inject-success-message = Вы вводите { $amount } ед. в { $target }!
injector-component-inject-success-message-self = Вы вводите в себя { $amount } ед.!
injector-component-draw-success-message = Вы набираете { $amount } ед. из { $target }.
injector-component-draw-success-message-self = Вы набираете из себя { $amount } ед.

## Fail Messages

injector-component-target-already-full-message = { CAPITALIZE($target) } полон!
injector-component-target-already-full-message-self = Вы уже полны!
injector-component-target-is-empty-message = { CAPITALIZE($target) } пуст!
injector-component-target-is-empty-message-self = Вы пусты!
injector-component-cannot-toggle-draw-message = Больше не набрать!
injector-component-cannot-toggle-inject-message = Нечего вводить!
injector-component-cannot-toggle-dynamic-message = Can't toggle dynamic!
injector-component-empty-message = {CAPITALIZE(THE($injector))} is empty!
injector-component-blocked-user = Protective gear blocked your injection!
injector-component-blocked-other = {CAPITALIZE(THE(POSS-ADJ($target)))} armor blocked {THE($user)}'s injection!
injector-component-cannot-transfer-message = Вы не можете ничего переместить в { $target }!
injector-component-cannot-transfer-message-self = Вы не можете ничего переместить в себя!
injector-component-cannot-inject-message = Вы не можете ничего ввести в { $target }!
injector-component-cannot-inject-message-self = Вы не можете ничего себе ввести!
injector-component-cannot-draw-message = Вы не можете ничего набрать из { $target }!
injector-component-cannot-draw-message-self = Вы не можете ничего набрать из себя!
injector-component-ignore-mobs = This injector can only interact with containers!

## mob-inject doafter messages

injector-component-needle-injecting-user = You start injecting the needle.
injector-component-needle-injecting-target = {CAPITALIZE(THE($user))} is trying to inject a needle into you!
injector-component-needle-drawing-user = You start drawing the needle.
injector-component-needle-drawing-target = {CAPITALIZE(THE($user))} is trying to use a needle to draw from you!
injector-component-spray-injecting-user = You start preparing the spray nozzle.
injector-component-spray-injecting-target = {CAPITALIZE(THE($user))} is trying to place a spray nozzle onto you!

## Target Popup Success messages
injector-component-feel-prick-message = You feel a tiny prick!
