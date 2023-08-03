# MindComponent localization

comp-mind-ghosting-prevented = Вы не можете стать призраком в данный момент.

## Messages displayed when a body is examined and in a certain state

comp-mind-examined-catatonic = { CAPITALIZE(SUBJECT($ent)) } в кататоническом ступоре. Стрессы жизни в глубоком космосе, должно быть, оказались слишком тяжелы для { OBJECT($ent) }. Восстановление маловероятно.
comp-mind-examined-dead =
    {$gender ->
        [male] Он мёртв
        [female] Она мертва
        [epicene] Они мертвы
        *[other] Оно мертво
comp-mind-examined-ssd = { CAPITALIZE(SUBJECT($ent)) } рассеяно смотрит в пустоту и ни на что не реагирует. { CAPITALIZE(SUBJECT($ent)) } может скоро придти в себя.
comp-mind-examined-dead-and-ssd = { CAPITALIZE(POSS-ADJ($ent)) } душа покинула тело и пропала. Восстановление маловероятно.
