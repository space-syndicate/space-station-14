defusable-examine-defused = { CAPITALIZE($name) } [color=lime]обезврежена[/color].
defusable-examine-live = { CAPITALIZE($name) } [color=red]тикает[/color] и [color=red]{ $time }[/color] осталось секунд.
defusable-examine-live-display-off = { CAPITALIZE($name) } [color=red]тикает[/color] и таймер похоже выключен.
defusable-examine-inactive = { CAPITALIZE($name) } [color=lime]неактивна[/color], но всё еще может взорваться.
defusable-examine-bolts =
    Болты { $down ->
        [true] [color=red]опущены[/color]
       *[false] [color=green]подняты[/color]
    }.
