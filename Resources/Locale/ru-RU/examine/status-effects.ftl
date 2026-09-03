status-effect-examine-adrenaline = [color=red]Каждая часть {POSS-ADJ($target)} тела выглядит напряженной.[/color]
status-effect-examine-drunk = [color=brown]{ CAPITALIZE(SUBJECT($target)) } { GENDER($target) ->
    [male]пьян...
    [female]пьяна...
    [epicence]пьяно...
    *[neuter]пьяны...
}[/color]
status-effect-examine-seeing-rainbow = [color=lightgreen]{ CAPITALIZE(SUBJECT($target)) } { GENDER($target) ->
    [male]смотрит
    [female]смотрит
    [epicence]смотрит
    *[neuter]смотрят
} на вещи, которых нет.[/color]
status-effect-examine-stunned = [color=yellow]{ CAPITALIZE(POSS-ADJ($target)) } { GENDER($target) ->
    [male]выглядит изможденным и неспособным
    [female]выглядит изможденным и неспособным
    [epicence]выглядит изможденным и неспособным
    *[neuter]выглядят изможденными и неспособными
} двигаться.[/color]
status-effect-examine-temporary-blindness = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } взгляд расфокусировался. Похоже на проблемы со зрением.[/color]
