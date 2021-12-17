### Сообщения о взаимодействии

# Показывается при ремонте чего-либо
comp-repairable-repair = Вы ремонтируете {PROPER($target) ->
  [true] {""}
  *[false] the{" "}
}{$target} with {PROPER($tool) ->
  [true] {""}
  *[false] the{" "}
}{$tool}
