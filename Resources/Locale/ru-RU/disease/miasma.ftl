ammonia-smell = Что-то резко попахивает!!
perishable-1 = [color=green]{ CAPITALIZE(SUBJECT($target)) } выглядит ещё свежо.[/color]
perishable-2 = [color=orangered]{ CAPITALIZE(SUBJECT($target)) } выглядит не особо свежо.[/color]
perishable-3 = [color=red]{ CAPITALIZE(SUBJECT($target)) } выглядит совсем не свежо.[/color]
rotting-rotting = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { $gender ->
        [male] гниёт
        [female] гниёт
        [epicene] гниют
       *[neuter] гниёт
    }![/color]
perishable-1-nonmob = [color=green]{ CAPITALIZE(SUBJECT($target)) } still looks fresh.[/color]
perishable-2-nonmob = [color=orangered]{ CAPITALIZE(SUBJECT($target)) } looks somewhat fresh.[/color]
perishable-3-nonmob = [color=red]{ CAPITALIZE(SUBJECT($target)) } doesn't look very fresh.[/color]
rotting-bloated = [color=orangered]{ CAPITALIZE(SUBJECT($target)) } { $gender ->
        [male] вздулся
        [female] вздулась
        [epicene] вздулись
       *[neuter] вздулось
    }![/color]
rotting-extremely-bloated = [color=red]{ CAPITALIZE(SUBJECT($target)) } сильно { $gender ->
        [male] вздулся
        [female] вздулась
        [epicene] вздулись
       *[neuter] вздулось
    }![/color]
rotting-rotting-nonmob = [color=orange]{ CAPITALIZE(SUBJECT($target)) } is rotting![/color]
rotting-bloated-nonmob = [color=orangered]{ CAPITALIZE(SUBJECT($target)) } is bloated![/color]
rotting-extremely-bloated-nonmob = [color=red]{ CAPITALIZE(SUBJECT($target)) } is extremely bloated![/color]
