#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     A tune's Melody, Bass and Drums grids stacked, with the bar numbers above (composer) and a playhead line while it
///     plays. It shows <see cref="Document" /> when set, otherwise <see cref="Data" /> (the reader). While
///     <see cref="Editable" />, the left button places a note (dragging lengthens it) or removes the note under it, and the
///     right button removes.
/// </summary>
public sealed class TuneGrid : UIElement
{
    private const int SMALL_TEXT_HEIGHT = 6;

    private static readonly Color MelodyColour = new(232, 180, 90);
    private static readonly Color BassColour = new(111, 168, 220);
    private static readonly Color DrumsColour = new(215, 112, 112);
    private static readonly Color RowDark = new(29, 26, 22);
    private static readonly Color RowLight = new(33, 30, 25);
    private static readonly Color HomeRow = new(48, 43, 33);
    private static readonly Color BarLine = new(106, 88, 56);
    private static readonly Color BeatLine = new(52, 46, 37);
    private static readonly Color BeatBarFill = new(20, 18, 16);
    private static readonly Color PlayheadColour = new(255, 255, 255, 210);
    private static readonly string[] BarNumbers = ["1", "2", "3", "4", "5", "6", "7", "8"];

    private readonly Dictionary<string, Texture2D?> SmallText = new(StringComparer.Ordinal);
    private bool Placing;

    public TuneGrid(TuneGridLayout layout)
    {
        Layout = layout;
        Width = layout.Width;
        Height = layout.Height;
    }

    public TuneGridLayout Layout { get; }

    /// <summary>The tune being edited (the composer).</summary>
    public TuneDocument? Document { get; set; }

    /// <summary>A tune to show when there is no <see cref="Document" /> (the reader).</summary>
    public TuneData? Data { get; set; }

    public bool Editable { get; set; }

    /// <summary>The layer Copy and Clear act on; outlined in its colour while editable.</summary>
    public TuneLayer SelectedLayer { get; set; } = TuneLayer.Melody;

    /// <summary>The playhead in columns (fractions allowed), or null when nothing plays.</summary>
    public double? Playhead { get; set; }

    /// <summary>Raised with a new note's layer and row, for its preview.</summary>
    public event Action<TuneLayer, int>? NotePlaced;

    /// <summary>Raised after any press, drag or release that may have changed the tune or the selected layer.</summary>
    public event Action? Edited;

    /// <summary>Raised with a bar (0-7) when its number is clicked.</summary>
    public event Action<int>? BarClicked;

    private TuneScale Scale => Document?.Scale ?? Data?.Scale ?? TuneScale.Major;

    private IReadOnlyList<TuneNote> Notes => Document?.Notes ?? Data?.Notes ?? Array.Empty<TuneNote>();

    public static Color ColourOf(TuneLayer layer)
        => layer switch
        {
            TuneLayer.Melody => MelodyColour,
            TuneLayer.Bass   => BassColour,
            _                => DrumsColour
        };

    public override void Dispose()
    {
        foreach (var texture in SmallText.Values)
            texture?.Dispose();

        SmallText.Clear();
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        if (Layout.BeatBarHeight > 0)
            DrawBeatBar(spriteBatch);

        var scale = Scale;

        foreach (var layer in TuneGridLayout.Layers)
            DrawLayer(spriteBatch, layer, scale);

        foreach (var note in Notes)
            DrawRectClipped(
                spriteBatch,
                new Rectangle(
                    ScreenX + Layout.GridX + (note.Start * Layout.CellWidth) + 1,
                    ScreenY + Layout.RowTop(note.Layer, note.Row) + 1,
                    (note.Length * Layout.CellWidth) - 1,
                    Layout.CellHeight - 1),
                ColourOf(note.Layer));

        if (Playhead is { } column)
            DrawRectClipped(
                spriteBatch,
                new Rectangle(ScreenX + Layout.GridX + (int)(column * Layout.CellWidth), ScreenY, 1, Height),
                PlayheadColour);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (!Editable || Document is null || Placing || ((e.Button != MouseButton.Left) && (e.Button != MouseButton.Right)))
            return;

        e.Handled = true;
        var x = e.ScreenX - ScreenX;
        var y = e.ScreenY - ScreenY;

        if (Layout.BarAt(x, y) is { } bar)
        {
            if (e.Button == MouseButton.Left)
                BarClicked?.Invoke(bar);

            return;
        }

        if (!Layout.TryHitCell(x, y, out var layer, out var row, out var column))
            return;

        SelectedLayer = layer;

        if ((e.Button == MouseButton.Right) || Document.NoteAt(layer, row, column) is not null)
            Document.Remove(layer, row, column);
        else if (Document.BeginPlace(layer, row, column))
        {
            Placing = true;
            NotePlaced?.Invoke(layer, row);
        }

        Edited?.Invoke();
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Placing)
            return;

        e.Handled = true;

        //the button came up where no mouse-up reached this grid (e.g. the window lost focus mid-drag)
        if (!InputBuffer.IsLeftButtonHeld)
        {
            Finish();

            return;
        }

        Document!.DragTo(Layout.ColumnAt(e.ScreenX - ScreenX));
        Edited?.Invoke();
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (!Placing)
            return;

        e.Handled = true;
        Finish();
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();

        if (Placing)
            Finish();
    }

    private void Finish()
    {
        Placing = false;
        Document?.EndPlace();
        Edited?.Invoke();
    }

    private void DrawBeatBar(SpriteBatch spriteBatch)
    {
        DrawRectClipped(
            spriteBatch,
            new Rectangle(ScreenX + Layout.GridX, ScreenY, Layout.GridWidth, Layout.BeatBarHeight),
            BeatBarFill);

        for (var bar = 0; bar < BarNumbers.Length; bar++)
            DrawTextClipped(
                spriteBatch,
                new Vector2(ScreenX + Layout.GridX + (bar * TuneGridLayout.COLUMNS_PER_BAR * Layout.CellWidth) + 3, ScreenY),
                BarNumbers[bar],
                LegendColors.Gray);
    }

    private void DrawLayer(SpriteBatch spriteBatch, TuneLayer layer, TuneScale scale)
    {
        var rows = CollegeProtocol.TuneRows(layer);
        var layerTop = ScreenY + Layout.LayerTop(layer);
        var layerHeight = Layout.LayerHeight(layer);
        var gridLeft = ScreenX + Layout.GridX;

        for (var row = 0; row < rows; row++)
        {
            var top = ScreenY + Layout.RowTop(layer, row);
            var home = (layer != TuneLayer.Drums) && TuneScales.IsHome(scale, row);
            var fill = home ? HomeRow : row % 2 == 0 ? RowLight : RowDark;
            DrawRectClipped(spriteBatch, new Rectangle(gridLeft, top, Layout.GridWidth, Layout.CellHeight), fill);

            if (Layout.LabelWidth > 0)
                DrawRowName(
                    spriteBatch,
                    TuneScales.RowName(scale, layer, row),
                    top + ((Layout.CellHeight - SMALL_TEXT_HEIGHT) / 2),
                    home || (layer == TuneLayer.Drums) ? ColourOf(layer) : LegendColors.Gray);
        }

        for (var column = 0; column <= CollegeProtocol.TUNE_STEPS; column += 2)
            DrawRectClipped(
                spriteBatch,
                new Rectangle(gridLeft + (column * Layout.CellWidth), layerTop, 1, layerHeight),
                column % TuneGridLayout.COLUMNS_PER_BAR == 0 ? BarLine : BeatLine);

        if (Editable && (layer == SelectedLayer))
        {
            var colour = ColourOf(layer);
            var outline = new Rectangle(gridLeft - 1, layerTop - 1, Layout.GridWidth + 2, layerHeight + 2);
            DrawRectClipped(spriteBatch, new Rectangle(outline.X, outline.Y, outline.Width, 1), colour);
            DrawRectClipped(spriteBatch, new Rectangle(outline.X, outline.Bottom - 1, outline.Width, 1), colour);
            DrawRectClipped(spriteBatch, new Rectangle(outline.X, outline.Y, 1, outline.Height), colour);
            DrawRectClipped(spriteBatch, new Rectangle(outline.Right - 1, outline.Y, 1, outline.Height), colour);
        }
    }

    //row names are 6 px tall (the 12 px font does not fit a 7 px row), right-aligned in the label column
    private void DrawRowName(SpriteBatch spriteBatch, string name, int y, Color colour)
    {
        if (!SmallText.TryGetValue(name, out var texture))
            SmallText[name] = texture = TextRenderer.BuildSmallText(TextureConverter.Device, name, SMALL_TEXT_HEIGHT);

        if (texture is not null)
            DrawTexture(spriteBatch, texture, new Vector2(ScreenX + Layout.LabelWidth - texture.Width - 2, y), colour);
    }
}
