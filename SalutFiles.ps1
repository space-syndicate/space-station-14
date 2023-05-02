New-Item -ItemType Junction -Path "Resources\Prototypes\SalutResourses" -Target "SalutResourses\Prototypes"
New-Item -ItemType Junction -Path "Resources\Textures\ASalut" -Target "SalutResourses\Textures\ASalut"
New-Item -ItemType Junction -Path "Resources\Locale\ru-RU\ASalut" -Target "SalutResourses\Locale\ru-RU\ASalut"
Remove-Item -Path "C:\Users\rg10008\source\GameComplect\space-station-14\Resources\Locale\ru-RU\Job" -Recurse
New-Item -ItemType Junction -Path "Resources\Locale\ru-RU\Job" -Target "SalutResourses\Locale\ru-RU\Job"
