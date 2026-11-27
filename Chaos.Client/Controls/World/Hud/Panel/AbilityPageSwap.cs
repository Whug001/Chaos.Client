#region
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Hud.Panel;

/// <summary>
///     The swap cell in the last slot of skill and spell pages 1 and 2 (slots 36 and 72). It never holds an ability;
///     dropping an ability on it asks the server to move it to the first free slot of the other page.
/// </summary>
public static class AbilityPageSwap
{
    private const int PAGE_SIZE = 36;
    private const string ICON_RESOURCE = "page-swap.png";

    /// <summary>True for the last cell of page 1 (offset 0) or page 2 (offset 36). Page 3 has no swap cell.</summary>
    public static bool IsSwapSlot(int slotOffset, byte slot) => (slotOffset is 0 or PAGE_SIZE) && (slot == slotOffset + PAGE_SIZE);

    public static string Label(byte slot) => slot == PAGE_SIZE ? "Drop here to move to page 2" : "Drop here to move to page 1";

    /// <summary>A fresh copy of the swap icon (Tools/PanelIcons/make_icon.py); each cell owns and disposes its own.</summary>
    public static Texture2D CreateIcon()
    {
        using var stream = typeof(AbilityPageSwap).Assembly.GetManifestResourceStream(ICON_RESOURCE)
                           ?? throw new InvalidOperationException($"Missing embedded resource {ICON_RESOURCE}");

        return Texture2D.FromStream(ChaosGame.Device, stream, DefaultColorProcessors.PremultiplyAlpha);
    }
}
