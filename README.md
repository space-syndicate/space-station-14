<div class="header" align="center">
<img alt="Space Station 14" width="880" height="300" src="https://raw.githubusercontent.com/space-wizards/asset-dump/de329a7898bb716b9d5ba9a0cd07f38e61f1ed05/github-logo.svg">
</div>

Space Station 14 это ремейк SS13, который работает на собственном движке [Robust Toolbox](https://github.com/space-wizards/RobustToolbox), написанном на C#.

Это репозиторий первого русскоязычного сервера по Space Station 14, целью которого является полный перевод игры на русский язык, поддержка актуальных изменений из основного репозитория, а так же добавление собственных изменений по необходимости.

## Ссылки

[Наш Discord](https://discord.station14.ru) | [Наша Вики](https://station14.ru/) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Клиент без Steam](https://spacestation14.io/about/nightlies/) | [Основной репозиторий](https://github.com/space-wizards/space-station-14)

## Документация

На официальном сайте с [документацией](https://docs.spacestation14.io/) имеется вся необходимая информация о контенте SS14, движке, дизайне игры и многом другом. Также имеется много информации для начинающих разработчиков.

Кроме того, ознакомьтесь со следующими ресурсами, содержащими информацию о лицензировании и указании авторства:
- [Robust Generic Attribution](https://docs.spacestation14.com/en/specifications/robust-generic-attribution.html)
- [Robust Station Image](https://docs.spacestation14.com/en/specifications/robust-station-image.html)

## Контрибьют

Мы рады принять вклад от любого человека. Заходите в Discord, если хотите помочь. У нас есть [список проблем](https://github.com/space-syndicate/space-station-14-content/issues), которые нужно решить, и любой может за них взяться. Не бойтесь просить о помощи!
Только убедитесь, что ваши изменения и PRы соответствуют [руководству по контрибьюту](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).

## Политика к коду сгенерированным ИИ
Не принимаются материалы, созданные с помощью ИИ без должных усилий и проверки. К ним относятся, в частности:

- Любой код (включая YAML), сгенерированный через GitHub Copilot, ChatGPT и им подобными.
- Изображения, аудиофайлы и другие ресурсы, созданные с помощью ИИ.
- Автоматически сгенерированная документация, отчеты об ошибках (issue reports) или описания запросов на слияние.

## Сборка

1. Склонируйте этот репозиторий:
```shell
git clone https://github.com/space-syndicate/space-station-14.git
```
2. Запустите `RUN_THIS.py` для инициализации подмодулей и скачивания движка:
```shell
cd space-station-14
python RUN_THIS.py
```
3. Скомпилируйте проект используя `dotnet build`

[Более подробная инструкция по запуску проекта.](https://station14.ru/wiki/%D0%97%D0%B0%D0%BF%D1%83%D1%81%D0%BA_%D0%BB%D0%BE%D0%BA%D0%B0%D0%BB%D1%8C%D0%BD%D0%BE%D0%B3%D0%BE_%D1%81%D0%B5%D1%80%D0%B2%D0%B5%D1%80%D0%B0)

## Лицензия

Весь код репозитория лицензирован под [MIT](https://github.com/space-syndicate/space-station-14/blob/master/LICENSE.TXT).

Большинство ассетов лицензированы под [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/), если не указано иное. Ассеты имеют свою лицензию и авторские права в файле метаданных. [Пример](https://github.com/space-syndicate/space-station-14/blob/master/Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).

Обратите внимание, что некоторые ассеты лицензированы на некоммерческой основе [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) или аналогичной некоммерческой лицензией, и их необходимо удалить, если вы хотите использовать этот проект в коммерческих целях.
