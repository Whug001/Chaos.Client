#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The rename prompt: a dimmed sheet over the window with a centred box holding a 12-character text box and
///     Confirm and Cancel. Enter (or Confirm) sends when the name passes the house-pet rule and Escape (or Cancel)
///     cancels; an invalid name stays open and shows why. A snapshot that takes the monster away closes the prompt
///     without disturbing text the player has typed.
/// </summary>
public sealed class TamerRenamePrompt : UIPanel
{
    private const int BOX_HEIGHT = 130;
    private const int BOX_WIDTH = 360;
    private const int BUTTON_TOP = 92;
    private const int BUTTON_WIDTH = 90;
    private const int ERROR_TOP = 62;
    private const int PADDING = 10;
    private const int TEXT_BOX_TOP = 32;
    private const int TEXT_BOX_WIDTH = 200;
    private const int TITLE_TOP = 10;

    private readonly UIPanel Box;
    private readonly CustomTextBox NameBox;
    private readonly UILabel ErrorLabel;
    private Guid MonsterId;

    public TamerRenamePrompt(int width, int height)
    {
        Name = "TamerRename";
        Width = width;
        Height = height;
        Visible = false;
        BackgroundColor = new Color(0, 0, 0, 150);

        Box = new UIPanel
        {
            X = (width - BOX_WIDTH) / 2,
            Y = (height - BOX_HEIGHT) / 2,
            Width = BOX_WIDTH,
            Height = BOX_HEIGHT,
            BackgroundColor = new Color(28, 20, 12),
            BorderColor = LegendColors.Gold
        };
        AddChild(Box);

        var title = new UILabel
        {
            X = PADDING,
            Y = TITLE_TOP,
            Width = BOX_WIDTH - (2 * PADDING),
            Height = TextRenderer.CHAR_HEIGHT,
            Text = TamerStableText.Rename,
            ForegroundColor = LegendColors.Gold,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };
        Box.AddChild(title);

        NameBox = new CustomTextBox
        {
            X = (BOX_WIDTH - TEXT_BOX_WIDTH) / 2,
            Y = TEXT_BOX_TOP,
            Width = TEXT_BOX_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = TamerStableText.MAX_NAME_LENGTH,
            ColorCodesEnabled = false,
            IsSelectable = true,
            IsTabStop = true
        };
        Box.AddChild(NameBox);

        ErrorLabel = new UILabel
        {
            X = PADDING,
            Y = ERROR_TOP,
            Width = BOX_WIDTH - (2 * PADDING),
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.Red,
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        Box.AddChild(ErrorLabel);

        var confirm = new CustomButton(TamerStableText.Confirm, BUTTON_WIDTH)
        {
            X = (BOX_WIDTH - ((2 * BUTTON_WIDTH) + 10)) / 2,
            Y = BUTTON_TOP
        };
        confirm.Clicked += TrySubmit;
        Box.AddChild(confirm);

        var cancel = new CustomButton(TamerStableText.Cancel, BUTTON_WIDTH)
        {
            X = confirm.X + BUTTON_WIDTH + 10,
            Y = BUTTON_TOP
        };
        cancel.Clicked += Hide;
        Box.AddChild(cancel);
    }

    /// <summary>A valid rename. The prompt has already closed.</summary>
    public event Action<TamerActionArgs>? Confirmed;

    /// <summary>Opens the prompt prefilled with the monster's current name.</summary>
    public void Open(Guid monsterId, string currentName)
    {
        MonsterId = monsterId;
        NameBox.Text = currentName ?? string.Empty;
        NameBox.CursorPosition = NameBox.Text.Length;
        ErrorLabel.Text = string.Empty;
        Visible = true;
        NameBox.IsFocused = true;
    }

    /// <summary>Closes the prompt, cancelling or after a valid send. Blurs the text box.</summary>
    public void Hide()
    {
        NameBox.IsFocused = false;
        MonsterId = Guid.Empty;
        Visible = false;
    }

    /// <summary>While open, a snapshot must still hold the monster; otherwise the prompt closes.</summary>
    public void Refresh(TamerStableState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!Visible)
            return;

        if (state.Monsters.All(monster => monster.Id != MonsterId))
            Hide();
    }

    /// <summary>An invalid name stays open and shows why; a valid one is sent and the prompt closes.</summary>
    public void TrySubmit()
    {
        if (!Visible)
            return;

        var action = TamerStableState.RenameAction(MonsterId, NameBox.Text);

        if (action is null)
        {
            ErrorLabel.Text = TamerStableText.InvalidName;
            return;
        }

        Hide();
        Confirmed?.Invoke(action);
    }
}
