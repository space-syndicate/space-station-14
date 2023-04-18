namespace Content.Shared.Sirena.Clothing
{
    [RegisterComponent]
    public sealed class ClothingGrantTagComponent : Component
    {
        [DataField("tag", required: true), ViewVariables(VVAccess.ReadWrite)]
        public string Tag = "";

        [ViewVariables(VVAccess.ReadWrite)]
        public bool IsActive = false;
    }
}
