New-Item -ItemType Junction -Path "Resources\Prototypes\SalutResourses" -Target "SalutResourses\Prototypes"
New-Item -ItemType Junction -Path "Resources\Textures\ASalut" -Target "SalutResourses\Textures\ASalut"
New-Item -ItemType Junction -Path "Resources\Locale\ru-RU\ASalut" -Target "SalutResourses\Locale\ru-RU\ASalut"
Remove-Item -Path "Resources\Locale\ru-RU\Job" -Recurse
Remove-Item -Path "Resources\ConfigPresets" -Recurse
Remove-Item -Path "Resources\Credits\Patrons.yml" -Recurse
Remove-Item -Path "Resources\ServerInfo" -Recurse
New-Item -ItemType Junction -Path "Resources\Locale\ru-RU\Job" -Target "SalutResourses\Locale\ru-RU\Job"
New-Item -ItemType Junction -Path "Resources\ServerInfo" -Target "SalutResourses\ServerInfo"
Remove-Item "bin\Content.Server\server_config.toml" -Recurse
Copy-Item "SalutResourses\ServerConfigs\server_config.toml" "bin\Content.Server\server_config.toml"
