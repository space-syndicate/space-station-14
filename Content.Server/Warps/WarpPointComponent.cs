namespace Content.Server.Warps
{
    /// <summary>
    /// Allows ghosts etc to warp to this entity by name.
    /// </summary>
    [RegisterComponent]
    public sealed partial class WarpPointComponent : Component
    {
        // Corvax-Next-Warper-Start: Unique (across all loaded maps) identifier for teleporting to warp points.
		[ViewVariables(VVAccess.ReadWrite)] [DataField("id")] public string? ID { get; set; }
        [ViewVariables(VVAccess.ReadWrite), DataField]
        public string? Location;
		// Corvax-Next-Warper-End

        /// <summary>
        ///     If true, ghosts warping to this entity will begin following it.
        /// </summary>
        [DataField]
        public bool Follow;
    }
}
