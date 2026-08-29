#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     A fixed 10-column grid of colour swatches with the same hover/select behaviour as
///     <see cref="ThumbnailStrip{T}" />'s cells. The colour list never changes after the panel opens, so
///     <see cref="SetColors" /> only builds the swatch children once and restyles them on every later call.
/// </summary>
public sealed class SwatchGrid : UIPanel
{
    public const int SWATCH = 12;
    public const int GAP = 2;
    public const int COLUMNS = 10;

    private readonly List<Swatch> Swatches = [];

    public event Action<DisplayColor>? Hovered;
    public event Action? HoverCleared;
    public event Action<DisplayColor>? Selected;

    public static int WidthFor(int columns) => (columns * (SWATCH + GAP)) - GAP;
    public static int HeightFor(int count) => (((count + COLUMNS - 1) / COLUMNS) * (SWATCH + GAP)) - GAP;

    public SwatchGrid() => Background = null;

    public void SetColors(IReadOnlyList<DisplayColor> colors, DisplayColor selected, Func<DisplayColor, Color> swatchColor)
    {
        //rebuild only when the colour list changes (it doesn't after Open); otherwise just restyle
        if (Swatches.Count != colors.Count)
        {
            foreach (var old in Swatches)
            {
                Children.Remove(old);
                old.Dispose();
            }

            Swatches.Clear();

            for (var i = 0; i < colors.Count; i++)
            {
                var swatch = new Swatch
                {
                    X = (i % COLUMNS) * (SWATCH + GAP),
                    Y = (i / COLUMNS) * (SWATCH + GAP),
                    Width = SWATCH,
                    Height = SWATCH
                };
                swatch.Hovered += c => Hovered?.Invoke(c);
                swatch.HoverCleared += () => HoverCleared?.Invoke();
                swatch.Clicked += c => Selected?.Invoke(c);
                Swatches.Add(swatch);
                AddChild(swatch);
            }

            Width = WidthFor(COLUMNS);
            Height = HeightFor(colors.Count);
        }

        for (var i = 0; i < colors.Count; i++)
        {
            Swatches[i].Color = colors[i];
            Swatches[i].Fill = swatchColor(colors[i]);
            Swatches[i].IsSelected = colors[i] == selected;
        }
    }

    private sealed class Swatch : UIElement
    {
        public DisplayColor Color;
        public Microsoft.Xna.Framework.Color Fill;
        public bool IsSelected;
        private bool IsHovered;

        public event Action<DisplayColor>? Hovered;
        public event Action? HoverCleared;
        public event Action<DisplayColor>? Clicked;

        public override void OnMouseEnter()
        {
            IsHovered = true;
            Hovered?.Invoke(Color);
        }

        public override void OnMouseLeave()
        {
            IsHovered = false;
            HoverCleared?.Invoke();
        }

        public override void OnClick(ClickEvent e)
        {
            Clicked?.Invoke(Color);
            e.Handled = true;
        }

        /// <summary>Clears transient hover state when the grid (or an ancestor) is hidden.</summary>
        public override void ResetInteractionState() => IsHovered = false;

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            //primes ClipRect -- InputDispatcher.HitTest's ContainsPoint check depends on it. Without this,
            //ClipRect stays at its default (empty) value forever, ContainsPoint always fails, and no swatch can
            //ever be hovered or clicked (the click falls through to the grid panel, whose default OnClick
            //swallows it).
            base.Draw(spriteBatch);

            var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
            DrawRect(spriteBatch, bounds, Fill);

            if (IsSelected)
            {
                DrawBorder(spriteBatch, bounds, LegendColors.Gold);
                DrawBorder(spriteBatch, new Rectangle(bounds.X + 1, bounds.Y + 1, Width - 2, Height - 2), LegendColors.Gold);
            } else if (IsHovered)
                DrawBorder(spriteBatch, bounds, LegendColors.Silver);
            else
                DrawBorder(spriteBatch, bounds, LegendColors.AlmostBlack);
        }
    }
}
