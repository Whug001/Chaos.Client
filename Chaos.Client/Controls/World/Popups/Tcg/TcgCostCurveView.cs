#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>How many cards in the deck cost 1, 2, 3, 4, 5 and 6 or more, as bars scaled to the tallest.</summary>
public sealed class TcgCostCurveView : UIElement
{
    private const int BAR_GAP = 8;
    private const int LABEL_HEIGHT = 14;
    private static readonly Color Back = new(0, 0, 0, 120);

    private int[] Buckets = new int[TcgDeckDraft.CURVE_BUCKETS];

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), Back);

        var count = Buckets.Length;
        var barWidth = (Width - ((count + 1) * BAR_GAP)) / count;
        var maxHeight = Height - LABEL_HEIGHT - TextRenderer.CHAR_HEIGHT - 4;
        var tallest = Math.Max(1, Buckets.Max());
        var baseline = ScreenY + Height - LABEL_HEIGHT;

        for (var i = 0; i < count; i++)
        {
            var x = ScreenX + BAR_GAP + (i * (barWidth + BAR_GAP));
            var height = Buckets[i] == 0 ? 0 : Math.Max(2, Buckets[i] * maxHeight / tallest);
            var label = TcgDeckDraft.BucketLabel(i);

            if (height > 0)
            {
                DrawRectClipped(spriteBatch, new Rectangle(x, baseline - height, barWidth, height), LegendColors.Gold);

                var number = Buckets[i].ToString();
                DrawTextClipped(spriteBatch, new Vector2(x + ((barWidth - TextRenderer.MeasureWidth(number)) / 2), baseline - height - TextRenderer.CHAR_HEIGHT - 1), number, LegendColors.White, false);
            }

            DrawTextClipped(spriteBatch, new Vector2(x + ((barWidth - TextRenderer.MeasureWidth(label)) / 2), baseline + 1), label, LegendColors.Silver, false);
        }
    }

    public void SetCurve(int[] buckets) => Buckets = buckets.Length == TcgDeckDraft.CURVE_BUCKETS ? buckets : new int[TcgDeckDraft.CURVE_BUCKETS];
}
