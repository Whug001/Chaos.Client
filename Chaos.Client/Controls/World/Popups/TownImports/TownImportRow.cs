#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Bank;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.TownImports;

/// <summary>
///     One item row in the town import windows: the item's icon, its name (up to two lines) and, on the right in gray, its
///     price or a short note. Rows are recycled by the lists, which re-bind them with <see cref="SetItem" />.
/// </summary>
public sealed class TownImportRow : UIPanel
{
    /// <summary>The row's fixed height: the 32 px icon plus a pixel above and below.</summary>
    public const int HEIGHT = 34;

    private const int ICON_SIZE = 32;
    private const int ICON_X = 1;
    private const int NAME_X = 38;
    private const int NOTE_RIGHT = 4;
    private const int NOTE_GAP = 6;
    private const int MAX_NAME_LINES = 2;
    private const int NAME_HEIGHT = (MAX_NAME_LINES * TextRenderer.CHAR_HEIGHT) + 2; //+2 for UILabel's own padding

    //the quest log's selected-row colors, so the two lists read alike
    private static readonly Color SelectedColor = new(156, 96, 12, 180);
    private static readonly Color SelectedNoteColor = new(255, 230, 186);

    private readonly UIImage IconImage;
    private readonly UILabel NameLabel;
    private readonly UILabel NoteLabel;

    public TownImportRow(int width)
    {
        Width = width;
        Height = HEIGHT;

        IconImage = new UIImage
        {
            X = ICON_X,
            Y = (HEIGHT - ICON_SIZE) / 2,
            Width = ICON_SIZE,
            Height = ICON_SIZE,
            IsHitTestVisible = false
        };

        NameLabel = new UILabel
        {
            X = NAME_X,
            Y = (HEIGHT - NAME_HEIGHT) / 2,
            Width = width - NAME_X - NOTE_RIGHT,
            Height = NAME_HEIGHT,
            WordWrap = true,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        NoteLabel = new UILabel
        {
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };

        AddChild(IconImage);
        AddChild(NameLabel);
        AddChild(NoteLabel);
    }

    /// <summary>Highlights the row as the list's current pick.</summary>
    public bool Selected
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            BackgroundColor = value ? SelectedColor : null;
            NoteLabel.ForegroundColor = value ? SelectedNoteColor : LegendColors.Gray;
        }
    }

    /// <summary>Raised when the row is left-clicked.</summary>
    public event Action? Clicked;

    public override void Dispose()
    {
        //the icon is UiRenderer's shared cached texture: detach it so UIImage.Dispose does not free it
        IconImage.Texture = null;
        base.Dispose();
    }

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    /// <summary>
    ///     Shows an item; <paramref name="note" /> replaces the price text when given. A null item clears and hides the row. The
    ///     price shows only when it is above zero.
    /// </summary>
    public void SetItem(TownImportItemInfo? item, string? note = null)
    {
        Visible = item is not null;

        if (item is null)
        {
            IconImage.Texture = null;
            NameLabel.Text = string.Empty;
            NoteLabel.Text = string.Empty;

            return;
        }

        //the icon is a shared cached texture owned by UiRenderer: bind it, never dispose it
        IconImage.Texture = UiRenderer.Instance!.GetItemIcon(item.PanelSprite, item.Color);

        var noteText = note ?? (item.Price > 0 ? TownImportText.PriceText(item.Price) : string.Empty);
        var noteWidth = noteText.Length == 0 ? 0 : TextRenderer.MeasureWidth(noteText) + 2; //+2 for UILabel's own padding

        //the note takes what it needs from the right; the name wraps in the rest
        NoteLabel.Width = noteWidth;
        NoteLabel.X = Width - NOTE_RIGHT - noteWidth;
        NoteLabel.Text = noteText;
        NameLabel.Width = Math.Max(TextRenderer.CHAR_WIDTH, NoteLabel.X - NAME_X - (noteWidth > 0 ? NOTE_GAP : 0));
        NameLabel.Text = FitToLines(NameLabel, item.Name);
    }

    /// <summary>
    ///     Wraps <paramref name="text" /> to at most <see cref="MAX_NAME_LINES" /> lines of <paramref name="label" />, folding the
    ///     rest into an ellipsis on the last line (a wrapped <see cref="UILabel" /> clips extra lines silently).
    /// </summary>
    private static string FitToLines(UILabel label, string text)
    {
        var innerWidth = label.Width - label.PaddingLeft - label.PaddingRight;
        var lines = TextRenderer.WrapText(text, innerWidth);

        if (lines.Count <= MAX_NAME_LINES)
            return text;

        var kept = string.Join(' ', lines.Take(MAX_NAME_LINES - 1));
        var tail = string.Join(' ', lines.Skip(MAX_NAME_LINES - 1));

        return kept + '\n' + BankItemRow.FitToLabel(label, tail);
    }
}
