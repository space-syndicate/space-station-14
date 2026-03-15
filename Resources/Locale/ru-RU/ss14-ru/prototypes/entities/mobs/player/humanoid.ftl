ent-RandomHumanoidSpawnerDeathSquad = Агент Эскадрона смерти
    .suffix = Роль ОБР, Эскадрон смерти
    .desc = { "" }

# ERT Leader
ent-RandomHumanoidSpawnerERTLeader = ОБР лидер
    .suffix = Роль ОБР, Базовый
    .desc = { "" }
ent-RandomHumanoidSpawnerERTLeaderEVA = ОБР лидер
    .suffix = Роль ОБР, ВКД
    .desc = { ent-RandomHumanoidSpawnerERTLeader.desc }
ent-RandomHumanoidSpawnerERTLeaderArmed = { ent-RandomHumanoidSpawnerERTLeaderEVA }
    .suffix = Роль ОБР, Вооружен, ВКД
    .desc = Вооружен XL8, 4 запасных магазина разного типа.

# ERT Chaplain
ent-RandomHumanoidSpawnerERTChaplain = ОБР священник
    .suffix = Роль ОБР, Базовый
    .desc = { ent-RandomHumanoidSpawnerERTLeader.desc }
ent-RandomHumanoidSpawnerERTChaplainEVA = ОБР священник
    .suffix = Роль ОБР, ВКД
    .desc = { ent-RandomHumanoidSpawnerERTChaplain.desc }

# ERT Janitor
ent-RandomHumanoidSpawnerERTJanitor = ОБР уборщик
    .suffix = Роль ОБР, Базовый
    .desc = { ent-RandomHumanoidSpawnerERTLeader.desc }
ent-RandomHumanoidSpawnerERTJanitorEVA = ОБР уборщик
    .suffix = Роль ОБР, ВКД
    .desc = { ent-RandomHumanoidSpawnerERTJanitor.desc }

# ERT Engineer
ent-RandomHumanoidSpawnerERTEngineer = ОБР инженер
    .suffix = Роль ОБР, Базовый
    .desc = { ent-RandomHumanoidSpawnerERTLeader.desc }
ent-RandomHumanoidSpawnerERTEngineerEVA = { ent-RandomHumanoidSpawnerERTEngineer }
    .suffix = Роль ОБР, ВКД
    .desc = { ent-RandomHumanoidSpawnerERTEngineer.desc }
ent-RandomHumanoidSpawnerERTEngineerArmed = { ent-RandomHumanoidSpawnerERTEngineer }
    .suffix = Роль ОБР, Вооружен, ВКД
    .desc = Вооружен Силовиком, имеет детонационный шнур и коробку детонаторов.

# ERT Security
ent-RandomHumanoidSpawnerERTSecurity = ОБР офицер безопасности
    .suffix = Роль ОБР, Базовый
    .desc = { ent-RandomHumanoidSpawnerERTLeader.desc }
ent-RandomHumanoidSpawnerERTSecurityEVA = ОБР офицер безопасности
    .suffix = Роль ОБР, ВКД
    .desc = { ent-RandomHumanoidSpawnerERTSecurity.desc }
ent-RandomHumanoidSpawnerERTSecurityArmedRifle = { ent-RandomHumanoidSpawnerERTSecurityEVA }, Стрелок
    .suffix = Роль ОБР, Вооружен, ВКД
    .desc = Вооружен Лектером, 4 запасных магазина различного типа, Лазерная пушка и переносной зарядник.
ent-RandomHumanoidSpawnerERTSecurityArmedGrenade = { ent-RandomHumanoidSpawnerERTSecurityEVA }, Гренадер
    .suffix = Роль ОБР, Вооружен, ВКД
    .desc = Вооружен Гидрой с осколочными снарядами, имеет в запасе 6 фугасных, 3 ЭМИ и светошумовых снаряда.
ent-RandomHumanoidSpawnerERTSecurityArmedVanguard = { ent-RandomHumanoidSpawnerERTSecurityEVA }, Авангард
    .suffix = Роль ОБР, Вооружен, ВКД
    .desc = Вооружен WT550, 4 запасных магазина, 3 телескопических щита.
ent-RandomHumanoidSpawnerERTSecurityArmedShotgun = { ent-RandomHumanoidSpawnerERTSecurityEVA }, Сапёр
    .suffix = Роль ОБР, Вооружен, ВКД
    .desc = Вооружен Силовиком, 3 коробки различной дроби, осколочной гранатой, детонационным шнуром и коробкой детонаторов.

# ERT Medic
ent-RandomHumanoidSpawnerERTMedical = ОБР медик
    .suffix = Роль ОБР, Базовый
    .desc = { ent-RandomHumanoidSpawnerERTLeader.desc }
ent-RandomHumanoidSpawnerERTMedicalEVA = ОБР медик
    .suffix = Роль ОБР, ВКД
    .desc = { ent-RandomHumanoidSpawnerERTMedical.desc }
ent-RandomHumanoidSpawnerERTMedicalArmed = ОБР медик
    .suffix = Роль ОБР, Вооружен, ВКД
    .desc = Вооружен Лектером, 4 запасных магазина разного типа.

# CBURN
ent-RandomHumanoidSpawnerCBURNUnit = Агент РХБЗЗ
    .suffix = Роль ОБР
    .desc = { "" }

# misc
ent-RandomHumanoidSpawnerCentcomOfficial = Представитель Центком
    .desc = { "" }
ent-RandomHumanoidSpawnerSyndicateAgent = Агент Синдиката
    .desc = { "" }
ent-RandomHumanoidSpawnerNukeOp = Ядерный оперативник
    .desc = { "" }
ent-RandomHumanoidSpawnerCluwne = Клувень
    .suffix = Спавнит клувеня
    .desc = { "" }
