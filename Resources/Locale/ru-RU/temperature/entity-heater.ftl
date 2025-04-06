-entity-heater-setting-name =
    { $setting ->
        [off] выкл
        [low] слабый
        [medium] средний
        [high] высокий
       *[other] неизвестный
    }
entity-heater-examined = Установлен на { $setting ->
    [off] [color=gray]{ -entity-heater-setting-name(setting: "off") }[/color]
    [low] [color=yellow]{ -entity-heater-setting-name(setting: "low") }[/color]
    [medium] [color=orange]{ -entity-heater-setting-name(setting: "medium") }[/color]
    [high] [color=red]{ -entity-heater-setting-name(setting: "high") }[/color]
   *[other] [color=purple]{ -entity-heater-setting-name(setting: "other") }[/color]
}.
entity-heater-switch-setting = Переключить на { -entity-heater-setting-name(setting: $setting) }
entity-heater-switched-setting = Переключен на { -entity-heater-setting-name(setting: $setting) }.
