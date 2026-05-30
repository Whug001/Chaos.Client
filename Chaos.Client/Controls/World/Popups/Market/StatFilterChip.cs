#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     A small removable stat-filter pill (e.g. "STR"). Renders a dlgframe-composited pill background with the stat
///     caption and a scarlet remove "x"; clicking anywhere on the chip raises <see cref="Removed" />. The pill sizes
///     itself to its caption. A threshold editor (e.g. "STR &gt;= 3") is a deferred feature.
/// </summary>
public sealed class StatFilterChip : UIPanel
{
    private const int CHIP_HEIGHT = 20;
    private const int INNER_PAD = 6;  //caption inset from the pill's left/right edges
    private const int REMOVE_GAP = 6; //gap between the caption and the remove "x"

    private static readonly SKColor FillColor = new(10, 8, 5, 255);
    private static readonly Color RemoveColor = LegendColors.Scarlet;

    /// <summary>The stat this chip filters on (e.g. "STR"); used by the owner to dedupe chips.</summary>
    public string StatKey { get; }

    public StatFilterChip(string statKey)
    {
        StatKey = statKey;
        Height = CHIP_HEIGHT;

        var textWidth = TextRenderer.MeasureWidth(statKey);
        var removeWidth = TextRenderer.MeasureWidth("x");
        Width = INNER_PAD + textWidth + REMOVE_GAP + removeWidth + INNER_PAD;

        //the pill background is a unique texture sized to this chip; UIPanel.Draw paints it and UIPanel.Dispose frees it.
        Background = BuildPill(Width, Height);

        AddChild(MakeLabel(INNER_PAD, textWidth, statKey, LegendColors.White));
        AddChild(MakeLabel(INNER_PAD + textWidth + REMOVE_GAP, removeWidth, "x", RemoveColor));
    }

    /// <summary>Raised when the chip is clicked (the whole pill is the remove target for now).</summary>
    public event ClickedHandler? Removed;

    public override void OnClick(ClickEvent e)
    {
        Removed?.Invoke();
        e.Handled = true;
    }

    //zero-padding, no-ellipsis label sized to exactly its text — UILabel's default 1px padding + ellipsis truncation
    //would otherwise clip a caption/"x" sized to its raw measured width.
    private static UILabel MakeLabel(int x, int width, string text, Color color)
        => new()
        {
            X = x,
            Y = (CHIP_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            PaddingLeft = 0,
            PaddingRight = 0,
            PaddingTop = 0,
            PaddingBottom = 0,
            TruncateWithEllipsis = false,
            ForegroundColor = color,
            Text = text,
            IsHitTestVisible = false
        };

    private static Texture2D BuildPill(int w, int h)
    {
        using var frame = DialogFrame.Composite(FillColor, w, h); //SKImage? — may be null

        var info = new SKImageInfo(
            w,
            h,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        if (frame is not null)
            canvas.DrawImage(frame, 0, 0);
        else
            canvas.Clear(FillColor); //fallback if dlgframe.epf failed to load

        using var snapshot = surface.Snapshot();

        return TextureConverter.ToTexture2D(snapshot); //uses static TextureConverter.Device
    }
}
