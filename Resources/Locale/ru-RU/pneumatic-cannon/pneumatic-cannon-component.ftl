### Замок для пневматической пушки.

pneumatic-cannon-component-verb-gas-tank-name = Выбросить газовый баллон
pneumatic-cannon-component-verb-eject-items-name = Выбросить все предметы.

## Показывается при вставке предметов в пушку

pneumatic-cannon-component-insert-item-success = Вы вставили { THE($item) } в { THE($cannon) }.
pneumatic-cannon-component-insert-item-failure = Вы не можете вставить { THE($item) } в { THE($cannon) }.

## Показывается при попытке выстрела, но нет газа

pneumatic-cannon-component-fire-no-gas = { CAPITALIZE(THE($cannon)) } щелкает, но газ не выходит.

## Показывается при изменении режима огня или мощности.

pneumatic-cannon-component-change-fire-mode = { $mode ->
    [All] Вы ослабляете клапаны, чтобы стрелять всем сразу.
    * [Single] Вы затягиваете клапаны, чтобы стрелять по одному предмету за раз.
}

pneumatic-cannon-component-change-power = { $power -> ...
    [Высокая] Вы устанавливаете ограничитель на максимальную мощность. Это кажется слишком мощным...
    [Средний] Вы устанавливаете ограничитель на среднюю мощность.
    * [Низкая] Вы устанавливаете ограничитель на низкую мощность.
}

## Показывается при установке/извлечении бензобака.

pneumatic-cannon-component-gas-tank-insert = Вы устанавливаете { THE($tank) } на { THE($cannon) }.
pneumatic-cannon-component-gas-tank-remove = Вы снимаете { THE($tank) } с { THE($cannon) }.
pneumatic-cannon-component-gas-tank-none = На { THE($cannon) } нет бензобака!

## Показывается при выбрасывании всех предметов из пушки с помощью глагола.

pneumatic-cannon-component-ejected-all = Вы выбросили все из { THE($cannon) }.

## Показывается при оглушении из-за слишком высокой мощности.

pneumatic-cannon-component-power-stun = Чистая сила { THE($cannon)} сбивает вас с ног!

