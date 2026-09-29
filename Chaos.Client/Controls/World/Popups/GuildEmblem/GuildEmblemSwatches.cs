#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildEmblem;

/// <summary>
///     The emblem editor's color row: a see-through box, then six color boxes. Clicking a box selects it; clicking the
///     selected color, or an empty box (which adds a gray color), asks to open the color picker.
/// </summary>
public sealed class GuildEmblemSwatches : UIElement
{
    public const int BOX = 22;
    public const int COUNT = GuildEmblemProtocol.MAX_COLORS + 1;
    public const int GAP = 3;

    /// <summary>The whole row's width, for laying out the window around it.</summary>
    public const int TOTAL_WIDTH = (COUNT * BOX) + ((COUNT - 1) * GAP);

    private static readonly Color CheckDark = new(52, 52, 52);
    private static readonly Color CheckLight = new(96, 96, 96);

    private readonly GuildEmblemEditorModel Model;

    public GuildEmblemSwatches(GuildEmblemEditorModel model)
    {
        Model = model;
        Width = TOTAL_WIDTH;
        Height = BOX;
    }

    /// <summary>Raised with a color number (1-based) when the picker should open for it.</summary>
    public event Action<int>? EditRequested;

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        var colors = Model.Design.Colors;

        for (var i = 0; i < COUNT; i++)
        {
            var box = BoxBounds(i);

            if (i == 0)
            {
                //see-through: a two-by-two checker
                var half = BOX / 2;
                DrawRectClipped(spriteBatch, new Rectangle(box.X, box.Y, half, half), CheckLight);
                DrawRectClipped(spriteBatch, new Rectangle(box.X + half, box.Y, BOX - half, half), CheckDark);
                DrawRectClipped(spriteBatch, new Rectangle(box.X, box.Y + half, half, BOX - half), CheckDark);
                DrawRectClipped(spriteBatch, new Rectangle(box.X + half, box.Y + half, BOX - half, BOX - half), CheckLight);
                DrawBorder(spriteBatch, box, Model.SelectedColor == 0 ? Color.White : LegendColors.Gray);

                continue;
            }

            if (i <= colors.Count)
            {
                DrawRectClipped(spriteBatch, box, new Color(colors[i - 1].R, colors[i - 1].G, colors[i - 1].B));
                DrawBorder(spriteBatch, box, i == Model.SelectedColor ? Color.White : LegendColors.Gray);
            } else
            {
                DrawBorder(spriteBatch, box, LegendColors.Gray);
                DrawTextClipped(spriteBatch, new Vector2(box.X + 8, box.Y + 5), "+", LegendColors.Gray, false);
            }
        }
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        for (var i = 0; i < COUNT; i++)
        {
            if (!BoxBounds(i).Contains(e.ScreenX, e.ScreenY))
                continue;

            if (i == 0)
                Model.SelectColor(0);
            else if (i > Model.Design.Colors.Count)
            {
                if (Model.AddColor(new GuildCloakColor(128, 128, 128)))
                    EditRequested?.Invoke(Model.SelectedColor);
            } else if (i == Model.SelectedColor)
                EditRequested?.Invoke(i);
            else
                Model.SelectColor(i);

            return;
        }
    }

    private Rectangle BoxBounds(int index) => new(ScreenX + (index * (BOX + GAP)), ScreenY, BOX, BOX);
}
