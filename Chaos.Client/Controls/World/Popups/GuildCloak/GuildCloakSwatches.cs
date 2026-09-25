#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     The editor's six color boxes. Clicking a color selects it; clicking the selected color, or an empty box (which adds a
///     gray color), asks to open the color picker.
/// </summary>
public sealed class GuildCloakSwatches : UIElement
{
    public const int BOX = 22;
    public const int COLUMNS = 2;
    public const int GAP = 4;
    public const int ROWS = (GuildCloakProtocol.MAX_COLORS + COLUMNS - 1) / COLUMNS;

    /// <summary>The whole grid's size, for laying out the window around it.</summary>
    public const int TOTAL_WIDTH = (COLUMNS * BOX) + ((COLUMNS - 1) * GAP);

    public const int TOTAL_HEIGHT = (ROWS * BOX) + ((ROWS - 1) * GAP);

    private readonly GuildCloakEditorModel Model;

    public GuildCloakSwatches(GuildCloakEditorModel model)
    {
        Model = model;
        Width = TOTAL_WIDTH;
        Height = TOTAL_HEIGHT;
    }

    /// <summary>Raised with a color number (1-based) when the picker should open for it.</summary>
    public event Action<int>? EditRequested;

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        var colors = Model.Design.Colors;

        for (var i = 0; i < GuildCloakProtocol.MAX_COLORS; i++)
        {
            var box = BoxBounds(i);

            if (i < colors.Count)
            {
                DrawRectClipped(spriteBatch, box, new Color(colors[i].R, colors[i].G, colors[i].B));
                DrawBorder(spriteBatch, box, i + 1 == Model.SelectedColor ? Color.White : LegendColors.Gray);
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

        for (var i = 0; i < GuildCloakProtocol.MAX_COLORS; i++)
        {
            if (!BoxBounds(i).Contains(e.ScreenX, e.ScreenY))
                continue;

            var number = i + 1;

            if (number > Model.Design.Colors.Count)
            {
                if (Model.AddColor(new GuildCloakColor(128, 128, 128)))
                    EditRequested?.Invoke(Model.SelectedColor);
            } else if (number == Model.SelectedColor)
                EditRequested?.Invoke(number);
            else
                Model.SelectColor(number);

            return;
        }
    }

    private Rectangle BoxBounds(int index)
        => new(
            ScreenX + ((index % COLUMNS) * (BOX + GAP)),
            ScreenY + ((index / COLUMNS) * (BOX + GAP)),
            BOX,
            BOX);
}
