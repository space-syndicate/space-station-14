### Loc for the pneumatic cannon.

pneumatic-cannon-component-verb-gas-tank-name = Извлечь газовый баллон
pneumatic-cannon-component-verb-eject-items-name = Извлечь все предметы

## Shown when inserting items into it

pneumatic-cannon-component-insert-item-success = Вы вставляете { $item } в { $cannon }.
pneumatic-cannon-component-insert-item-failure = Похоже, что { $item } не помещается в { $cannon }.

## Shown when trying to fire, but no gas

pneumatic-cannon-component-fire-no-gas = { CAPITALIZE($cannon) } щелкает, но никакого газа не выходит.

## Shown when changing the fire mode or power.

pneumatic-cannon-component-change-fire-mode =
    { $mode ->
        [All] You loosen the valves to fire everything at once.
       *[Single] You tighten the valves to fire one item at a time.
    }
pneumatic-cannon-component-change-power =
    { $power ->
        [High] You set the limiter to maximum power. It feels a little too powerful...
        [Medium] You set the limiter to medium power.
       *[Low] You set the limiter to low power.
    }

## Shown when inserting/removing the gas tank.

pneumatic-cannon-component-gas-tank-insert = Вы устанавливаете { $tank } в { $cannon }.
pneumatic-cannon-component-gas-tank-remove = Вы берёте { $tank } из { $cannon }.
pneumatic-cannon-component-gas-tank-none = В { $cannon } нет баллона!

## Shown when ejecting every item from the cannon using a verb.

pneumatic-cannon-component-ejected-all = Вы извлекаете всё из { $cannon }.

## Shown when being stunned by having the power too high.

pneumatic-cannon-component-power-stun = { CAPITALIZE($cannon) } сбивает вас с ног!
