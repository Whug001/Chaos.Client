#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Models;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Bank;

/// <summary>
///     A single row in the bank's item list: an item icon, a name, and a right-aligned stack count. Shares its icon
///     size, row height, and red-text selection highlight with <see cref="Market.MarketListingRow" />.
/// </summary>
/// <remarks>
///     The icon/name/count children are display-only (<see cref="UIElement.IsHitTestVisible" /> = false) so the row panel
///     itself stays the deepest hit-test target — clicks land on the row, not a child label. The row never disposes its
///     icon: item icons are shared cached textures owned by <see cref="UiRenderer" />.
/// </remarks>
public sealed class BankItemRow : UIPanel
{
    public const int ICON_SIZE = 32;
    public const int ROW_HEIGHT = 32; //flush to the icon — no vertical padding around the row

    private const int ICON_X = 0; //flush to the column edge — the name needs the room more than the gutter does
    private const int ICON_TEXT_GAP = 8;

    //space reserved on the right for the stack count — bank counts can reach 5 digits.
    private const int COUNT_RESERVE = 36;

    //two lines is what an icon-tall row affords, and it is enough for every real item name at this column width.
    private const int MAX_NAME_LINES = 2;
    private const int NAME_HEIGHT = MAX_NAME_LINES * TextRenderer.CHAR_HEIGHT + 2; //+2 for UILabel's own padding

    private static readonly Color SelectedTextColor = LegendColors.Scarlet;

    private readonly UILabel CountLabel;
    private readonly UIImage IconImage;
    private readonly UILabel NameLabel;

    //the bound entry — the double-click withdraw is built from it.
    private BankItemEntry? Entry;

    //what was under the cursor when the button went down. A drag only begins 4px later, and a refresh (or a wheel-page)
    //can re-bind this pooled row in between — the drop must withdraw what was actually picked up.
    private BankItemEntry? PressedEntry;
    private Texture2D? PressedTexture;

    public bool IsSelected
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;

            var color = value ? SelectedTextColor : LegendColors.White;
            NameLabel.ForegroundColor = color;
            CountLabel.ForegroundColor = color;
        }
    }

    public BankItemRow(int contentWidth)
    {
        Width = contentWidth;
        Height = ROW_HEIGHT;

        IconImage = new UIImage
        {
            X = ICON_X,
            Y = (ROW_HEIGHT - ICON_SIZE) / 2,
            Width = ICON_SIZE,
            Height = ICON_SIZE,
            IsHitTestVisible = false
        };

        //a wrapped label centers a one-line name in its box, so short and long names still share a baseline block.
        NameLabel = new UILabel
        {
            X = ICON_X + ICON_SIZE + ICON_TEXT_GAP,
            Y = (ROW_HEIGHT - NAME_HEIGHT) / 2,
            Width = contentWidth - (ICON_X + ICON_SIZE + ICON_TEXT_GAP) - COUNT_RESERVE,
            Height = NAME_HEIGHT,
            WordWrap = true,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        CountLabel = new UILabel
        {
            X = contentWidth - COUNT_RESERVE - 4,
            Y = (ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = COUNT_RESERVE,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        AddChild(IconImage);
        AddChild(NameLabel);
        AddChild(CountLabel);
    }

    /// <summary>
    ///     Ellipsizes <paramref name="text" /> to <paramref name="label" />'s inner width, producing exactly what
    ///     <see cref="UILabel" />'s own ellipsis branch would draw — but once, at bind time. That branch re-runs its
    ///     substring-and-measure walk every frame for as long as the text overflows, so a label bound to text that already
    ///     fits never pays for it.
    /// </summary>
    internal static string FitToLabel(UILabel label, string text)
    {
        var innerWidth = label.Width - label.PaddingLeft - label.PaddingRight;

        if (TextRenderer.MeasureWidth(text) <= innerWidth)
            return text;

        var maxWidth = innerWidth - TextRenderer.MeasureWidth("...");
        var width = 0;
        var length = 0;

        while (length < text.Length)
        {
            var next = width + TextRenderer.MeasureCharWidth(text[length]);

            if (next > maxWidth)
                break;

            width = next;
            length++;
        }

        return length > 0 ? string.Concat(text.AsSpan(0, length), "...") : "...";
    }

    /// <summary>
    ///     Wraps <paramref name="text" /> to at most <see cref="MAX_NAME_LINES" /> lines of <paramref name="label" />, folding
    ///     everything past the last one into an ellipsis.
    /// </summary>
    /// <remarks>
    ///     <see cref="UILabel" /> draws only as many wrapped lines as it is tall and clips the rest silently, so a name too
    ///     long even for two lines has to be cut here — where the cut can be shown.
    /// </remarks>
    private static string FitToLines(UILabel label, string text)
    {
        var innerWidth = label.Width - label.PaddingLeft - label.PaddingRight;
        var lines = TextRenderer.WrapText(text, innerWidth);

        if (lines.Count <= MAX_NAME_LINES)
            return text;

        //the overflow is re-joined onto the last visible line and ellipsized as one, so the "..." lands where the name
        //actually stops rather than at the end of a line that happened to fit
        var kept = string.Join(' ', lines.Take(MAX_NAME_LINES - 1));
        var tail = string.Join(' ', lines.Skip(MAX_NAME_LINES - 1));

        return kept + '\n' + FitToLabel(label, tail);
    }

    public event ClickedHandler? Clicked;

    //fired when the cursor enters the row — the bank window populates the detail pane from this.
    public event ClickedHandler? Hovered;

    /// <summary>Raised with (item name, banked stack count) when the row is double-clicked. No amount is chosen yet.</summary>
    public event Action<string, int>? WithdrawGestured;

    public void ClearEntry()
    {
        Entry = null; //a pooled hidden row that kept its entry would still drag into a phantom withdraw
        PressedEntry = null;
        PressedTexture = null;
        IconImage.Texture = null;
        IconImage.Visible = false;
        NameLabel.Text = string.Empty;
        CountLabel.Text = string.Empty;
    }

    public void SetEntry(BankItemEntry entry)
    {
        Entry = entry;

        //the icon is a shared cached texture owned by UiRenderer — bind it, never dispose it.
        IconImage.Texture = UiRenderer.Instance!.GetItemIcon(entry.Sprite, entry.Color);
        IconImage.Visible = true;

        CountLabel.Text = entry.Count.ToString();

        //cut at the first newline so a multi-line name can't spend the row's two lines on its own line breaks, then wrap
        //what's left. FitToLines is done once here rather than by the label's own overflow branch every frame.
        var name = entry.Name;
        var newlineIndex = name.IndexOf('\n');

        if (newlineIndex >= 0)
            name = name[..newlineIndex];

        NameLabel.Text = FitToLines(NameLabel, name);
    }

    public override void OnClick(ClickEvent e)
    {
        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        if ((e.Button != MouseButton.Left) || (Entry is null))
            return;

        WithdrawGestured?.Invoke(Entry.Name, Entry.Count);
        e.Handled = true;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        PressedEntry = Entry;
        PressedTexture = IconImage.Texture;

        base.OnMouseDown(e); //UIPanel marks the press handled
    }

    //the payload comes off the press, not the current binding — see PressedEntry.
    public override void OnDragStart(DragStartEvent e)
    {
        //the drag threshold is button-agnostic; only a left drag withdraws.
        if ((e.Button != MouseButton.Left) || (PressedEntry is null))
            return;

        e.Payload = new BankDragPayload
        {
            ItemName = PressedEntry.Name,
            Count = PressedEntry.Count,
            GhostTexture = PressedTexture
        };

        PressedEntry = null;
        PressedTexture = null;
    }

    public override void OnMouseEnter() => Hovered?.Invoke();
}
