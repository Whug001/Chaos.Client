#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>The drawing's 32 swatches in two columns. A click picks a swatch; a double-click asks to change its colour.</summary>
public sealed class ArtSwatches : UIElement
{
    private const int CELL_WIDTH = 20;
    private const int CELL_HEIGHT = 14;
    private const int COLUMNS = 2;
    private const int STEP_X = CELL_WIDTH + 2;
    private const int STEP_Y = CELL_HEIGHT + 1;
    private const int ROWS = PixelDrawing.COLOURS / COLUMNS;

    private readonly ArtEditor Editor;
    private int LastClick = -1;
    private int PreviousClick = -1;

    public ArtSwatches(ArtEditor editor)
    {
        Editor = editor;
        Width = (COLUMNS * STEP_X) - 2;
        Height = (ROWS * STEP_Y) - 1;
    }

    public event Action<int>? EditRequested;
    public event Action<int>? Picked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        for (var i = 0; i < PixelDrawing.COLOURS; i++)
        {
            var bounds = new Rectangle(ScreenX + ((i % COLUMNS) * STEP_X), ScreenY + ((i / COLUMNS) * STEP_Y), CELL_WIDTH, CELL_HEIGHT);
            DrawRectClipped(spriteBatch, bounds, Editor.Drawing.Palette[i]);
            DrawBorder(spriteBatch, bounds, i == Editor.Current ? Color.White : Color.Black);
        }
    }

    public override void OnClick(ClickEvent e)
    {
        e.Handled = true;
        PreviousClick = LastClick;
        LastClick = -1;

        if (e.Button != MouseButton.Left)
            return;

        if (SwatchAt(e.ScreenX, e.ScreenY) is { } swatch)
        {
            LastClick = swatch;
            Picked?.Invoke(swatch);
        }
    }

    //the dispatcher raises Click and then DoubleClick for the second release, and pairs any two clicks on this element
    public override void OnDoubleClick(DoubleClickEvent e)
    {
        e.Handled = true;

        if ((e.Button == MouseButton.Left) && (LastClick >= 0) && (PreviousClick == LastClick))
            EditRequested?.Invoke(LastClick);

        LastClick = -1;
        PreviousClick = -1;
    }

    private int? SwatchAt(int screenX, int screenY)
    {
        var x = screenX - ScreenX;
        var y = screenY - ScreenY;

        if ((x < 0) || (y < 0))
            return null;

        var column = x / STEP_X;
        var row = y / STEP_Y;

        return (column < COLUMNS) && (row < ROWS) ? (row * COLUMNS) + column : null;
    }
}
