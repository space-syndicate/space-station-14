examinable-satiation-component-examine-hunger-full = { CAPITALIZE(SUBJECT($entity)) } { CONJUGATE-BASIC($entity, "выглядят", "выглядит") }
    { GENDER($entity) ->
    [male] сытым
    [female] сытой
    [epicene] сытыми
    *[neuter] сытым
}!
examinable-satiation-component-examine-hunger-okay  = { CAPITALIZE(SUBJECT($entity)) } { CONJUGATE-BASIC($entity, "выглядят", "выглядит") }
    { GENDER($entity) ->
    [male] довольным
    [female] довольной
    [epicene] довольными
    *[neuter] довольным
}.
examinable-satiation-component-examine-hunger-concerned = { CAPITALIZE(SUBJECT($entity)) } { CONJUGATE-BASIC($entity, "выглядят", "выглядит") }
    { GENDER($entity) ->
    [male] проголодавшимся
    [female] проголодавшейся
    [epicene] проголодавшимися
    *[neuter] проголодавшимся
}.
examinable-satiation-component-examine-hunger-desperate = { CAPITALIZE(SUBJECT($entity)) } { CONJUGATE-BASIC($entity, "выглядят", "выглядит") }
    { GENDER($entity) ->
    [male] изголодавшимся
    [female] изголодавшейся
    [epicene] изголодавшимися
    *[neuter] изголодавшимся
}!
examinable-satiation-component-examine-hunger-none = { CAPITALIZE(SUBJECT($entity)) }, похоже, не { CONJUGATE-BASIC($entity, "голодают", "голодает") }.

examinable-satiation-component-examine-thirst-full = { CAPITALIZE(SUBJECT($entity)) } { CONJUGATE-BASIC($entity, "выглядят", "выглядит") }
    { GENDER($entity) ->
    [male] перепившим!
    [female] перепившей!
    [epicene] перепившими!
    *[neuter] перпивше!
}!
examinable-satiation-component-examine-thirst-okay = { CAPITALIZE(SUBJECT($entity)) } { CONJUGATE-BASIC($entity, "выглядят", "выглядит") }
    { GENDER($entity) ->
    [male] напоенным
    [female] напоенной
    [epicene] напоенными
    *[neuter] напоено
}.
examinable-satiation-component-examine-thirst-concerned = { CAPITALIZE(SUBJECT($entity)) } { CONJUGATE-BASIC($entity, "выглядят", "выглядит") }
    { GENDER($entity) ->
    [male] иссохшим
    [female] иссохшей
    [epicene] иссохшими
    *[neuter] иссохшим
}.
examinable-satiation-component-examine-thirst-desperate = { CAPITALIZE(SUBJECT($entity)) } { CONJUGATE-BASIC($entity, "выглядят", "выглядит") }
    { GENDER($entity) ->
    [male] обезвоженным
    [female] обезвоженной
    [epicene] обезвоженными
    *[neuter] обезвожено
}!
examinable-satiation-component-examine-thirst-none = { CAPITALIZE(SUBJECT($entity)) } похоже не { CONJUGATE-BASIC($entity, "испытывают", "испытывает") } жажду.
