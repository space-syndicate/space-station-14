agent-id-new = { $number ->
    [0] { CAPITALIZE($card) } не дала новых доступов.
    [one] { CAPITALIZE($card) } дала один новый доступ.
    [few] { CAPITALIZE($card) } дала { $number } новых доступа.
   *[other] { CAPITALIZE($card) } дала { $number } новых доступов.
}

agent-id-card-current-name = Имя:
agent-id-card-current-job = Должность:
agent-id-card-job-icon-label = Иконка:
agent-id-menu-title = ID карта Агента