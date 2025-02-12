using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxNext.Skills;

[Serializable, NetSerializable]
public enum Skills : byte
{
    ShuttleControl,
    ComplexBuilding,
    DeviceBuilding,
    CyborgBuilding,
    ResearchAndDevelopment,
    AdvancedChemistry,
    Surgery,
    Shooting,
    ComplexDisassembly,
    MedicalEquipment,
    Butchering
}
