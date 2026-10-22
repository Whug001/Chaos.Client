namespace Chaos.Client.Rendering;

/// <summary>
///     The guild hall editor's white-on-transparent glyphs in Assets/GuildHallIcons: tab icons, tool icons, the header
///     check and the set-row lock. Drawn tinted (silver normally, dark on a selected gold button), so the white source
///     keeps its soft alpha. Each icon is "guildhallicon.&lt;key&gt;.png".
/// </summary>
public static class GuildHallIconAssets
{
    public static string ResourceName(string key) => $"guildhallicon.{key}.png";

    public static Stream Open(string resourceName)
        => typeof(GuildHallIconAssets).Assembly.GetManifestResourceStream(resourceName)
           ?? throw new InvalidOperationException($"missing embedded guild hall icon {resourceName}");
}
