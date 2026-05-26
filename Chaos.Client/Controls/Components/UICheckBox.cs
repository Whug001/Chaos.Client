#region
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.Components;

/// <summary>
///     A labeled checkbox: a small framed box (dlgframe border over a dark fill, with a drawn X when checked) followed by a
///     caption. The whole widget is a single click target — clicking the box or the text raises <see cref="Clicked" />. When
///     disabled it renders at 50% brightness (box texture and caption color) and is non-interactive. The four box textures
///     (checked/unchecked × normal/dim) are built once and shared across all instances.
/// </summary>
public sealed class UICheckBox : UIPanel
{
    /// <summary>Box edge length in pixels.</summary>
    public const int CHECKBOX_SIZE = 18;

    /// <summary>Gap in pixels between the box and the caption.</summary>
    public const int CAPTION_GAP = 6;

    private static Texture2D? SharedUnchecked;
    private static Texture2D? SharedChecked;
    private static Texture2D? SharedUncheckedDisabled;
    private static Texture2D? SharedCheckedDisabled;

    private readonly UIButton Box;
    private readonly UILabel Caption;
    private Color NormalForeground = TextColors.Default;

    public UICheckBox()
    {
        EnsureTextures();

        Box = new UIButton
        {
            Name = "box",
            X = 0,
            Y = 0,
            Width = CHECKBOX_SIZE,
            Height = CHECKBOX_SIZE,
            NormalTexture = SharedUnchecked,
            SelectedTexture = SharedChecked,
            IsHitTestVisible = false
        };

        Caption = new UILabel
        {
            Name = "caption",
            X = CHECKBOX_SIZE + CAPTION_GAP,
            Y = 0,
            PaddingLeft = 0,
            PaddingRight = 0,
            PaddingTop = 0,
            PaddingBottom = 0,
            ForegroundColor = NormalForeground,
            IsHitTestVisible = false
        };

        AddChild(Box);
        AddChild(Caption);
    }

    /// <summary>Caption text.</summary>
    public string Text
    {
        get => Caption.Text;
        set => Caption.Text = value;
    }

    /// <summary>Checked state, backed by the inner box's selected texture.</summary>
    public bool Checked
    {
        get => Box.IsSelected;
        set => Box.IsSelected = value;
    }

    /// <summary>Caption color when enabled. The disabled state renders this at 50% brightness.</summary>
    public Color ForegroundColor
    {
        get => NormalForeground;

        set
        {
            NormalForeground = value;
            Caption.ForegroundColor = value;
        }
    }

    public event ClickedHandler? Clicked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        //layout from the consumer-set Width/Height (set after construction)
        Box.Y = (Height - CHECKBOX_SIZE) / 2;
        Caption.Width = Math.Max(0, Width - CHECKBOX_SIZE - CAPTION_GAP);
        Caption.Height = Height;

        //reflect enabled-state into the children (cheap no-ops when unchanged)
        Box.Enabled = Enabled;
        Box.DisabledTexture = Checked ? SharedCheckedDisabled : SharedUncheckedDisabled;

        var targetForeground = Enabled ? NormalForeground : Dim(NormalForeground);

        if (Caption.ForegroundColor != targetForeground)
            Caption.ForegroundColor = targetForeground;

        base.Draw(spriteBatch);
    }

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void Dispose()
    {
        //box textures are shared statics — detach them before base disposes the child button
        Box.NormalTexture = null;
        Box.SelectedTexture = null;
        Box.DisabledTexture = null;
        Box.PressedTexture = null;
        Box.HoverTexture = null;

        base.Dispose();
    }

    private static Color Dim(Color c)
        => new(
            (byte)(c.R / 2),
            (byte)(c.G / 2),
            (byte)(c.B / 2),
            c.A);

    private static void EnsureTextures()
    {
        if (SharedUnchecked is { IsDisposed: false }
            && SharedChecked is { IsDisposed: false }
            && SharedUncheckedDisabled is { IsDisposed: false }
            && SharedCheckedDisabled is { IsDisposed: false })
            return;

        SharedUnchecked?.Dispose();
        SharedChecked?.Dispose();
        SharedUncheckedDisabled?.Dispose();
        SharedCheckedDisabled?.Dispose();

        SharedUnchecked = BuildBox(false, false);
        SharedChecked = BuildBox(true, false);
        SharedUncheckedDisabled = BuildBox(false, true);
        SharedCheckedDisabled = BuildBox(true, true);
    }

    private static Texture2D BuildBox(bool withX, bool dim)
    {
        //dlgframe border over a near-black fill — same utility the tooltip uses
        using var frame = DialogFrame.Composite(
            new SKColor(
                10,
                8,
                5,
                255),
            CHECKBOX_SIZE,
            CHECKBOX_SIZE);

        var info = new SKImageInfo(
            CHECKBOX_SIZE,
            CHECKBOX_SIZE,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        if (frame is not null)
            canvas.DrawImage(frame, 0, 0);
        else
            canvas.Clear(
                new SKColor(
                    10,
                    8,
                    5,
                    255));

        if (withX)
        {
            using var paint = new SKPaint();

            paint.Color = new SKColor(233, 223, 194);
            paint.StrokeWidth = 2;
            paint.IsAntialias = false;
            paint.Style = SKPaintStyle.Stroke;

            const int PAD = 4;

            canvas.DrawLine(
                PAD,
                PAD,
                CHECKBOX_SIZE - PAD,
                CHECKBOX_SIZE - PAD,
                paint);

            canvas.DrawLine(
                CHECKBOX_SIZE - PAD,
                PAD,
                PAD,
                CHECKBOX_SIZE - PAD,
                paint);
        }

        using var snapshot = surface.Snapshot();

        if (!dim)
            return TextureConverter.ToTexture2D(snapshot);

        //50%-brightness variant: redraw the snapshot through an RGB-scaling color matrix (alpha row left at 1).
        using var dimSurface = SKSurface.Create(info);
        using var dimPaint = new SKPaint();

        //@formatter:off
        dimPaint.ColorFilter = SKColorFilter.CreateColorMatrix([
                                                                   0.5f, 0f, 0f, 0f, 0f,
                                                                   0f, 0.5f, 0f, 0f, 0f,
                                                                   0f, 0f, 0.5f, 0f, 0f,
                                                                   0f, 0f, 0f, 1f, 0f
                                                               ]);
        //@formatter:on

        dimSurface.Canvas.DrawImage(
            snapshot,
            0,
            0,
            dimPaint);

        using var dimSnapshot = dimSurface.Snapshot();

        return TextureConverter.ToTexture2D(dimSnapshot);
    }
}