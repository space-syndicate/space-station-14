ent-BaseDefibrillator = defibrillator
    .desc = CLEAR! Zzzzat!
ent-Defibrillator = { ent-['BaseDefibrillator', 'PowerCellSlotMediumItem'] }

  .desc = { ent-['BaseDefibrillator', 'PowerCellSlotMediumItem'].desc }
ent-DefibrillatorEmpty = { ent-Defibrillator }
    .suffix = Empty
    .desc = { ent-Defibrillator.desc }
ent-DefibrillatorOneHandedUnpowered = { ent-BaseDefibrillator }
    .suffix = One-Handed, Unpowered
    .desc = { ent-BaseDefibrillator.desc }
