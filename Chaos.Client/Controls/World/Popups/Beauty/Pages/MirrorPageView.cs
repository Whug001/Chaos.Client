#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>
///     The shell's change funnels, handed to every page so every change still goes through
///     <see cref="BeautyShopControl" />. <see cref="Select" /> is for selections and page steps (it tears down a stale
///     confirm and repaints); <see cref="Hover" /> only repaints.
/// </summary>
public sealed record MirrorActions(
    Action<Action<ViewModel.BeautyShop>> Select,
    Action<Action<ViewModel.BeautyShop>> Hover,
    HeadThumbnailRenderer Thumbnails);

/// <summary>
///     One page of the mirror. The shell builds every page once, shows one at a time and calls <see cref="Refresh" />
///     on the visible one after every change.
/// </summary>
public abstract class MirrorPageView : UIPanel
{
    protected MirrorActions Actions { get; }

    protected MirrorPageView(MirrorActions actions, int width, int height)
    {
        Background = null;
        Actions = actions;
        Width = width;
        Height = height;
        Visible = false;
    }

    /// <summary>Repaints the page from <see cref="Collections.WorldState.BeautyShop" />.</summary>
    public abstract void Refresh();

    protected UILabel AddLabel(int x, int y, int width, Color? color = null, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color ?? LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(label);

        return label;
    }

    /// <summary>The look thumbnails are built on: the selected (not hovered) values, so cells never flicker while the player hovers.</summary>
    protected static AislingAppearance BaseLook(ViewModel.BeautyShop vm)
        => MirrorLooks.Bare(vm.Gender, vm.HairStyle, vm.HairColor, vm.BodyColor, vm.FaceSprite);

    /// <summary>What a caption says an item costs: "current" for the player's own, "free" when it costs nothing, else the price.</summary>
    protected static string CostText(bool isCurrent, int cost)
        => isCurrent ? "current"
            : cost == 0 ? "free"
            : cost.ToString("N0");

    /// <summary>1-based position of the matching entry in <paramref name="items" />, or "-" when none matches.</summary>
    protected static string Position<T>(IReadOnlyList<T> items, Func<T, bool> isCurrent)
    {
        for (var i = 0; i < items.Count; i++)
            if (isCurrent(items[i]))
                return (i + 1).ToString();

        return "-";
    }

    protected static string FaceName(ViewModel.BeautyShop vm, int sprite)
        => vm.Faces.FirstOrDefault(f => f.Sprite == sprite)?.Name ?? $"Face {sprite}";
}
