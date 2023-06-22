-create-3rd-person =
    { $chance ->
        [1] Создаёт
       *[other] создать
    }
-cause-3rd-person =
    { $chance ->
        [1] Вызывает
       *[other] вызывать
    }
-satiate-3rd-person =
    { $chance ->
        [1] Насыщает
       *[other] насыщать
    }
reagent-effect-guidebook-create-entity-reaction-effect =
    { $chance ->
        [1] Создаёт
       *[other] создать
    } { $amount ->
        [1] { INDEFINITE($entname) }
       *[other] { $amount } { MAKEPLURAL($entname) }
    }
reagent-effect-guidebook-explosion-reaction-effect =
    { $chance ->
        [1] Вызывает
       *[other] вызывать
    } взрыв
reagent-effect-guidebook-foam-area-reaction-effect =
    { $chance ->
        [1] Создаёт
       *[other] создать
    } большое количество пены
reagent-effect-guidebook-foam-area-reaction-effect =
    { $chance ->
        [1] Создаёт
       *[other] создать
    } большое количество дыма
reagent-effect-guidebook-satiate-thirst =
    { $chance ->
        [1] Утоляет
       *[other] утолять
    } { $relative ->
        [1] жажду средне
       *[other] жажду на { NATURALFIXED($relative, 3) }x от обычного
    }
reagent-effect-guidebook-satiate-hunger =
    { $chance ->
        [1] Насыщает
       *[other] насыщать
    } { $relative ->
        [1] голод средне
       *[other] голод на { NATURALFIXED($relative, 3) }x от обычного
    }
reagent-effect-guidebook-health-change =
    { $chance ->
        [1]
            { $healsordeals ->
                [heals] Излечивает
                [deals] Наносит
               *[both] Изменяет здоровье на
            }
       *[other]
            { $healsordeals ->
                [heals] излечивать
                [deals] наносить
               *[both] изменяет здоровье на
            }
    } { $changes }
reagent-effect-guidebook-status-effect =
    { $type ->
        [add]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { MANY("second", $time) }, эффект накапливается
       *[set]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { MANY("second", $time) }, эффект не накапливается
        [remove]
            { $chance ->
                [1] Удаляет
               *[other] удаляет
            } { NATURALFIXED($time, 3) } { MANY("second", $time) } от { LOC($key) }
    }
reagent-effect-guidebook-activate-artifact =
    { $chance ->
        [1] Пытается
       *[other] пытаться
    } активировать артефакт
reagent-effect-guidebook-set-solution-temperature-effect =
    { $chance ->
        [1] Устанавливает
       *[other] устанавливает
    } температуру раствора точно { NATURALFIXED($temperature, 2) }k
reagent-effect-guidebook-adjust-solution-temperature-effect =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Добавляет
               *[-1] Удаляет
            }
       *[other]
            { $deltasign ->
                [1] добавляет
               *[-1] удаляет
            }
    } тепло из раствора, пока температура не достигнет { $deltasign ->
        [1] не более { NATURALFIXED($maxtemp, 2) }k
       *[-1] не менее { NATURALFIXED($mintemp, 2) }k
    }
reagent-effect-guidebook-adjust-reagent-reagent =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Добавляет
               *[-1] Удаляет
            }
       *[other]
            { $deltasign ->
                [1] добавляет
               *[-1] удаляет
            }
    } { NATURALFIXED($amount, 2) }ед. от { $reagent } { $deltasign ->
        [1] к
       *[-1] из
    } раствора
reagent-effect-guidebook-adjust-reagent-group =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Добавляет
               *[-1] Удаляет
            }
       *[other]
            { $deltasign ->
                [1] добавляет
               *[-1] удаляет
            }
    } { NATURALFIXED($amount, 2) }ед реагентов в группе { $group } { $deltasign ->
        [1] к
       *[-1] из
    } раствора
reagent-effect-guidebook-adjust-temperature =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Добавляет
               *[-1] Удаляет
            }
       *[other]
            { $deltasign ->
                [1] добавляет
               *[-1] удаляет
            }
    } { POWERJOULES($amount) } тепла { $deltasign ->
        [1] к телу
       *[-1] из тела
    }, в котором он метабилизируется
reagent-effect-guidebook-chem-cause-disease =
    { $chance ->
        [1] Вызывает
       *[other] вызывать
    } болезнь { $disease }
reagent-effect-guidebook-chem-cause-random-disease =
    { $chance ->
        [1] Вызывает
       *[other] вызывать
    } болезнь { $diseases }
reagent-effect-guidebook-jittering =
    { $chance ->
        [1] Вызывает
       *[other] вызывать
    } тряску
reagent-effect-guidebook-chem-clean-bloodstream =
    { $chance ->
        [1] Очищает
       *[other] очищать
    } кровеносную систему от других веществ
reagent-effect-guidebook-cure-disease =
    { $chance ->
        [1] Излечивает
       *[other] излечить
    } болезнь
reagent-effect-guidebook-cure-eye-damage =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Излечивает
               *[-1] Наносит
            }
       *[other]
            { $deltasign ->
                [1] излечить
               *[-1] наносить
            }
    } повреждения глаз
reagent-effect-guidebook-chem-vomit =
    { $chance ->
        [1] Вызывает
       *[other] вызывать
    } vomiting
reagent-effect-guidebook-create-gas =
    { $chance ->
        [1] Создаёт
       *[other] создать
    } { $moles } { $moles ->
        [1] моль
       *[other] моль
    } of { $gas }
reagent-effect-guidebook-drunk =
    { $chance ->
        [1] Вызывает
       *[other] вызывать
    } опьянение
reagent-effect-guidebook-electrocute =
    { $chance ->
        [1] Бьёт током
       *[other] бить током
    } употребившего в течении { NATURALFIXED($time, 3) } { MANY("second", $time) }
reagent-effect-guidebook-extinguish-reaction =
    { $chance ->
        [1] Гасит
       *[other] гасить
    } огонь
reagent-effect-guidebook-flammable-reaction =
    { $chance ->
        [1] Повышает
       *[other] повышать
    } воспламеняемость
reagent-effect-guidebook-ignite =
    { $chance ->
        [1] Поджигает
       *[other] поджигать
    } употребившего
reagent-effect-guidebook-make-sentient =
    { $chance ->
        [1] Делает
       *[other] делать
    } употребившего разумным
reagent-effect-guidebook-modify-bleed-amount =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Усиливает
               *[-1] Ослабляет
            }
       *[other]
            { $deltasign ->
                [1] усиливать
               *[-1] ослаблять
            }
    } кровотечение
reagent-effect-guidebook-modify-blood-level =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Повышает
               *[-1] Понижает
            }
       *[other]
            { $deltasign ->
                [1] повышать
               *[-1] понижать
            }
    } уровень крови в организме
reagent-effect-guidebook-paralyze =
    { $chance ->
        [1] Парализует
       *[other] парализовать
    } употребившего минимум на { NATURALFIXED($time, 3) } { MANY("second", $time) }
reagent-effect-guidebook-movespeed-modifier =
    { $chance ->
        [1] Делает
       *[other] делать
    } скорость передвижения { NATURALFIXED($walkspeed, 3) }x от стандартной минимум на { NATURALFIXED($time, 3) } { MANY("second", $time) }
reagent-effect-guidebook-reset-narcolepsy =
    { $chance ->
        [1] Предотвращает
       *[other] предотвращать
    } приступы нарколепсии
reagent-effect-guidebook-wash-cream-pie-reaction =
    { $chance ->
        [1] Смывает
       *[other] смывать
    } кремовый пирог с лица
reagent-effect-guidebook-missing =
    { $chance ->
        [1] Вызывает
       *[other] вызывать
    } неизвестный эффект, так как никто еще не написал об этом эффекте
