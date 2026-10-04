namespace Chaos.Client.Rendering;

/// <summary>
///     The Josephine's Mirror backdrop pictures embedded in this assembly: Assets/MirrorBackdrops, cut from real maps by
///     Tools/MirrorBackdrops. Each place has "mirrorbackdrop.&lt;key&gt;.png" and "mirrorbackdrop.&lt;key&gt;-thumb.png".
/// </summary>
public static class MirrorBackdropAssets
{
    public static string ResourceName(string key) => $"mirrorbackdrop.{key}.png";

    public static string ThumbnailResourceName(string key) => $"mirrorbackdrop.{key}-thumb.png";

    public static Stream Open(string resourceName)
        => typeof(MirrorBackdropAssets).Assembly.GetManifestResourceStream(resourceName)
           ?? throw new InvalidOperationException($"missing embedded mirror backdrop {resourceName}");
}
