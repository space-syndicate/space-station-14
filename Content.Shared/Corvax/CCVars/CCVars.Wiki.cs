using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Enables automatic client-side entity screenshot generation.
    /// </summary>
    public static readonly CVarDef<bool> EntityScreenshotGeneratorEnabled =
        CVarDef.Create("autogen.entity_screenshot.enabled", false, CVar.CLIENTONLY);

    /// <summary>
    /// Output directory for generated entity screenshots inside UserData.
    /// </summary>
    public static readonly CVarDef<string> EntityScreenshotOutputPath =
        CVarDef.Create("autogen.entity_screenshot.output_path", "/Textures/Entities", CVar.CLIENTONLY);

    /// <summary>
    /// Prefix for prototype folders included in entity_project generation.
    /// </summary>
    public static readonly CVarDef<string> EntityProjectFolderPrefix =
        CVarDef.Create("autogen.entity_project.folder_prefix", string.Empty, CVar.ARCHIVE);

    /// <summary>
    /// Prototype folder name to exclude from entity_project generation.
    /// </summary>
    public static readonly CVarDef<string> EntityProjectExcludedCoreProjectFolder =
        CVarDef.Create("autogen.entity_project.excluded_core_project_folder", string.Empty, CVar.ARCHIVE);

    /// <summary>
    /// Name of the project, which is clearly worth noting for the purpose of organizing the wiki's content.
    /// </summary>
    public static readonly CVarDef<string> WikiProjectPrefix =
        CVarDef.Create("autogen.wiki.project_prefix", string.Empty, CVar.SERVERONLY | CVar.ARCHIVE);
}
