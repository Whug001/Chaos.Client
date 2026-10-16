#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Bank;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.CommissionBoard;

/// <summary>One commission board list row: an optional icon, the description or item name on up to two lines, and a short note on the right.</summary>
public sealed class CommissionBoardRowView : UIPanel
{
    public const int HEIGHT = 30;

    private const int ICON_SIZE = 24;
    private const int ICON_X = 3;
    private const int TEXT_X_ICON = 32;
    private const int TEXT_X_PLAIN = 4;
    private const int NOTE_RIGHT = 4;
    private const int NOTE_GAP = 6;
    private const int MAX_TITLE_LINES = 2;

    //the quest log's selected-row colors, so the two lists read alike
    private static readonly Color SelectedColor = new(156, 96, 12, 180);
    private static readonly Color SelectedNoteColor = new(255, 230, 186);

    private readonly UIImage IconImage;
    private readonly UILabel NoteLabel;
    private readonly UILabel TitleLabel;

    public CommissionBoardRowView(int width)
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

        TitleLabel = new UILabel
        {
            Y = 1,
            Height = HEIGHT - 2,
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
        AddChild(TitleLabel);
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

    /// <summary>Shows a row; a null icon hides the icon and gives the title the full width.</summary>
    public void Bind(Texture2D? icon, string title, string note)
    {
        IconImage.Texture = icon;
        IconImage.Visible = icon is not null;

        var textX = icon is null ? TEXT_X_PLAIN : TEXT_X_ICON;
        var noteWidth = note.Length == 0 ? 0 : TextRenderer.MeasureWidth(note) + 2;

        NoteLabel.Width = noteWidth;
        NoteLabel.X = Width - NOTE_RIGHT - noteWidth;
        NoteLabel.Text = note;
        TitleLabel.X = textX;
        TitleLabel.Width = Math.Max(TextRenderer.CHAR_WIDTH, NoteLabel.X - textX - (noteWidth > 0 ? NOTE_GAP : 0));
        TitleLabel.Text = FitToLines(TitleLabel, title);
    }

    private static string FitToLines(UILabel label, string text)
    {
        var innerWidth = label.Width - label.PaddingLeft - label.PaddingRight;
        var lines = TextRenderer.WrapText(text, innerWidth);

        if (lines.Count <= MAX_TITLE_LINES)
            return text;

        var kept = string.Join(' ', lines.Take(MAX_TITLE_LINES - 1));
        var tail = string.Join(' ', lines.Skip(MAX_TITLE_LINES - 1));

        return kept + '\n' + BankItemRow.FitToLabel(label, tail);
    }
}
