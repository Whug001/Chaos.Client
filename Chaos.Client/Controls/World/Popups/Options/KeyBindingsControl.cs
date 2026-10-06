#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.Systems.KeyBinds;
#endregion

namespace Chaos.Client.Controls.World.Popups.Options;

/// <summary>
///     The key bindings window (F12): every bindable action by category, each with a primary and a secondary key. Click a
///     key, then press the new one. Changes take effect at once and are saved to the character's settings when the window
///     closes.
/// </summary>
/// <remarks>
///     Layout: the title across the top; the category buttons down the left; on the right the column captions, a page of
///     rows (action name, primary key, secondary key) and the page row under them. Under both sit the status line and
///     Reset All, then the frame's ornate bottom border with the Close button.
/// </remarks>
public sealed class KeyBindingsControl : GuildCloakDialogBase
{
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private const int LEFT = 20;
    private const int GAP = 10;
    private const int TITLE_TOP = 10;
    private const int CAPTION_TOP = 40;
    private const int CATEGORY_TOP = 56;
    private const int CATEGORY_WIDTH = 100;
    private const int CATEGORY_STEP = CustomButton.HEIGHT + 4;
    private const int ROWS_TOP = 56;
    private const int ROW_HEIGHT = CustomButton.HEIGHT + 2;
    private const int PAGE_SIZE = 11;
    private const int NAME_WIDTH = 170;
    private const int KEY_WIDTH = 110;
    private const int KEY_GAP = 4;
    private const int SMALL_BUTTON = 28;
    private const int RESET_WIDTH = 120;
    private const int BORDER_GAP = 4;

    private const string HINT = "Click a key, then press the new one. Esc cancels, Backspace clears.";

    private readonly CustomButton[] CategoryButtons;
    private readonly CustomButton NextPageButton;
    private readonly UILabel PageLabel;
    private readonly CustomButton PrevPageButton;
    private readonly Row[] Rows = new Row[PAGE_SIZE];
    private readonly UILabel StatusLabel;

    private GameActionCategory Category = GameActionCategory.Movement;
    private bool Changed;
    private int Page;
    private (GameAction Action, int Slot)? Pending;

    public KeyBindingsControl()
        : base("_nsett", false)
    {
        Name = "KeyBindings";
        Visible = false;
        UsesControlStack = true;

        var listLeft = LEFT + CATEGORY_WIDTH + GAP;
        var primaryLeft = listLeft + NAME_WIDTH + KEY_GAP;
        var secondaryLeft = primaryLeft + KEY_WIDTH + KEY_GAP;
        var pageTop = ROWS_TOP + (PAGE_SIZE * ROW_HEIGHT) + 4;
        var statusTop = pageTop + CustomButton.HEIGHT + 8;
        var resetTop = statusTop + TextRenderer.CHAR_HEIGHT + 6;

        Width = secondaryLeft + KEY_WIDTH + LEFT;
        Height = resetTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Caption("Key Bindings", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("CATEGORY", LEFT, CAPTION_TOP, CATEGORY_WIDTH, color: LegendColors.Gray);
        Caption("ACTION", listLeft, CAPTION_TOP, NAME_WIDTH, color: LegendColors.Gray);
        Caption("PRIMARY", primaryLeft, CAPTION_TOP, KEY_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        Caption("SECONDARY", secondaryLeft, CAPTION_TOP, KEY_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        var categories = Enum.GetValues<GameActionCategory>();
        CategoryButtons = new CustomButton[categories.Length];

        for (var i = 0; i < categories.Length; i++)
        {
            var category = categories[i];

            CategoryButtons[i] = AddButton(
                GameActions.CategoryName(category),
                CATEGORY_WIDTH,
                LEFT,
                CATEGORY_TOP + (i * CATEGORY_STEP),
                () => ShowCategory(category));
        }

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var top = ROWS_TOP + (i * ROW_HEIGHT);
            var index = i;

            Rows[i] = new Row(
                Caption(string.Empty, listLeft, top + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2), NAME_WIDTH),
                AddButton(string.Empty, KEY_WIDTH, primaryLeft, top, () => StartCapture(index, 0)),
                AddButton(string.Empty, KEY_WIDTH, secondaryLeft, top, () => StartCapture(index, 1)));
        }

        PrevPageButton = AddButton("<", SMALL_BUTTON, listLeft, pageTop, () => TurnPage(-1));
        NextPageButton = AddButton(">", SMALL_BUTTON, listLeft + SMALL_BUTTON + 4, pageTop, () => TurnPage(1));

        PageLabel = Caption(
            string.Empty,
            listLeft + (2 * SMALL_BUTTON) + 10,
            pageTop + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            NAME_WIDTH,
            color: LegendColors.Gray);

        StatusLabel = Caption(HINT, LEFT, statusTop, Width - (2 * LEFT), color: LegendColors.Gray);
        AddButton("Reset All", RESET_WIDTH, LEFT, resetTop, ResetAll);

        Refresh();
    }

    private IReadOnlyList<GameAction> Actions => GameActions.InCategory(Category).ToList();

    private int PageCount => Math.Max(1, (Actions.Count + PAGE_SIZE - 1) / PAGE_SIZE);

    public override void Show()
    {
        SetStatus(HINT, LegendColors.Gray);
        Refresh();
        base.Show();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        CancelCapture();
        base.Hide();

        if (!Changed)
            return;

        Changed = false;
        ClientSettings.Save();
    }

    /// <summary>
    ///     Takes every key while open, so nothing reaches the game behind the window. Waiting for a key, the next one is
    ///     bound (Escape cancels, Backspace or Delete clears); otherwise Escape or F12 closes the window.
    /// </summary>
    public override void OnKeyDown(KeyDownEvent e)
    {
        //alt+enter still resizes the window
        if ((e.Scancode == Scancode.Enter) && e.Alt)
            return;

        e.Handled = true;

        if (Pending is not { } pending)
        {
            if (e.Scancode is Scancode.Escape or Scancode.F12)
                Hide();

            return;
        }

        //a modifier on its own is the start of a chord: wait for the key that goes with it
        if (KeyChord.IsModifierKey(e.Scancode))
            return;

        if (e.Scancode == Scancode.Escape)
        {
            CancelCapture();
            SetStatus(HINT, LegendColors.Gray);

            return;
        }

        if ((e.Scancode is Scancode.Back or Scancode.Delete) && (e.Modifiers & (KeyModifiers.Shift | KeyModifiers.Ctrl | KeyModifiers.Alt)) == 0)
        {
            KeyBindings.Current.Clear(pending.Action, pending.Slot);
            Changed = true;
            CancelCapture();
            SetStatus($"{GameActions.NameOf(pending.Action)} has no {SlotName(pending.Slot)} key now.", LegendColors.White);

            return;
        }

        var chord = KeyChord.From(e);
        var result = KeyBindings.Current.Bind(pending.Action, pending.Slot, chord);

        if (!result.Bound)
        {
            SetStatus(
                result.ClashesWith is { } clash
                    ? $"{GameActions.NameOf(clash)} uses {KeyNames.Get(chord.Key)}. Each note needs its own key."
                    : $"{chord.DisplayName} cannot be bound. Press another key.",
                LegendColors.Red);

            return;
        }

        Changed = true;
        CancelCapture();

        if (result.TakenFrom is { } taken)
            SetStatus($"{chord.DisplayName} was taken from {GameActions.NameOf(taken)}.", LegendColors.Gold);
        else
            SetStatus($"{GameActions.NameOf(pending.Action)} is now {chord.DisplayName}.", LegendColors.White);
    }

    private static string SlotName(int slot) => slot == 0 ? "primary" : "secondary";

    private void StartCapture(int rowIndex, int slot)
    {
        var index = (Page * PAGE_SIZE) + rowIndex;
        var actions = Actions;

        if (index >= actions.Count)
            return;

        Pending = (actions[index], slot);
        KeyBindings.IsCapturing = true;
        SetStatus($"Press a key for {GameActions.NameOf(actions[index])}. Esc cancels, Backspace clears.", LegendColors.Gold);
        Refresh();
    }

    private void CancelCapture()
    {
        if (Pending is null)
            return;

        Pending = null;
        KeyBindings.IsCapturing = false;
        Refresh();
    }

    private void ShowCategory(GameActionCategory category)
    {
        CancelCapture();
        Category = category;
        Page = 0;
        SetStatus(HINT, LegendColors.Gray);
        Refresh();
    }

    private void TurnPage(int delta)
    {
        CancelCapture();
        Page = Math.Clamp(Page + delta, 0, PageCount - 1);
        Refresh();
    }

    private void ResetAll()
    {
        CancelCapture();
        KeyBindings.Current.ResetToDefaults();
        Changed = true;
        SetStatus("Every key is back to its default.", LegendColors.White);
        Refresh();
    }

    private void SetStatus(string text, Microsoft.Xna.Framework.Color color)
    {
        StatusLabel.Text = text;
        StatusLabel.ForegroundColor = color;
    }

    private void Refresh()
    {
        var categories = Enum.GetValues<GameActionCategory>();

        for (var i = 0; i < CategoryButtons.Length; i++)
            CategoryButtons[i].Selected = categories[i] == Category;

        var actions = Actions;
        var table = KeyBindings.Current;

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var index = (Page * PAGE_SIZE) + i;
            var row = Rows[i];
            var visible = index < actions.Count;

            row.Name.Visible = visible;
            row.Primary.Visible = visible;
            row.Secondary.Visible = visible;

            if (!visible)
                continue;

            var action = actions[index];
            row.Name.Text = GameActions.NameOf(action);
            ShowKey(row.Primary, action, 0);
            ShowKey(row.Secondary, action, 1);
        }

        void ShowKey(CustomButton button, GameAction action, int slot)
        {
            var waiting = Pending is { } pending && (pending.Action == action) && (pending.Slot == slot);
            button.Caption = waiting ? "Press a key..." : table.Get(action, slot).DisplayName;
            button.Selected = waiting;
        }

        var paged = PageCount > 1;
        PrevPageButton.Visible = paged;
        NextPageButton.Visible = paged;
        PageLabel.Visible = paged;
        PrevPageButton.Enabled = Page > 0;
        NextPageButton.Enabled = Page < (PageCount - 1);
        PageLabel.Text = $"Page {Page + 1} of {PageCount}";
    }

    private sealed record Row(UILabel Name, CustomButton Primary, CustomButton Secondary);
}
