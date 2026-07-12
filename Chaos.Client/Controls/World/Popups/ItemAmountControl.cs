#region
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     Item amount popup using litemex prefab. Shown when a stackable item needs a quantity —
///     exchange, market listing, or bank deposit.
/// </summary>
public sealed class ItemAmountControl : PrefabPanel
{
    //butt001.epf frame indices (3 per button: normal, pressed, disabled)
    private const int OK_NORMAL = 15;
    private const int OK_PRESSED = 16;
    private const int OK_DISABLED = 17;
    private const int CANCEL_NORMAL = 21;
    private const int CANCEL_PRESSED = 22;

    private UILabel? TitleLabel { get; }

    /// <summary>
    ///     The 1-based inventory slot of the stackable item being prompted for.
    /// </summary>
    public byte ItemSlot { get; private set; }

    /// <summary>
    ///     The banked item's display name. The bank has no slots — it keys by name — so
    ///     <see cref="ItemAmountPurpose.BankWithdraw" /> reads this where the other purposes read <see cref="ItemSlot" />.
    /// </summary>
    public string ItemName { get; private set; } = string.Empty;

    /// <summary>
    ///     What the confirmed amount will be used for. Set by <see cref="ShowFor" />, so the popup can never be opened
    ///     without declaring intent — the confirm handler routes on this rather than on ambient screen state.
    /// </summary>
    public ItemAmountPurpose Purpose { get; private set; } = ItemAmountPurpose.Exchange;

    public UITextBox? AmountTextBox { get; }
    public UIButton? CancelButton { get; }
    public UIButton? OkButton { get; }

    public ItemAmountControl()
        : base("litemex")
    {
        Name = "ItemAmount";
        Visible = false;
        UsesControlStack = true;

        var cache = UiRenderer.Instance!;

        //ok button — positioned from prefab rect, textured from butt001.epf
        var okRect = GetRect("OK");
        OkButton = new UIButton
        {
            Name = "OK",
            X = okRect.X,
            Y = okRect.Y,
            Width = okRect.Width,
            Height = okRect.Height,
            NormalTexture = cache.GetEpfTexture("butt001.epf", OK_NORMAL),
            PressedTexture = cache.GetEpfTexture("butt001.epf", OK_PRESSED),
            DisabledTexture = cache.GetEpfTexture("butt001.epf", OK_DISABLED),
            Enabled = false
        };
        AddChild(OkButton);

        //cancel button
        var cancelRect = GetRect("Cancel");
        CancelButton = new UIButton
        {
            Name = "Cancel",
            X = cancelRect.X,
            Y = cancelRect.Y,
            Width = cancelRect.Width,
            Height = cancelRect.Height,
            NormalTexture = cache.GetEpfTexture("butt001.epf", CANCEL_NORMAL),
            PressedTexture = cache.GetEpfTexture("butt001.epf", CANCEL_PRESSED)
        };
        AddChild(CancelButton);

        AmountTextBox = CreateTextBox("Text", 5);

        TitleLabel = CreateLabel("Title");
        TitleLabel?.ForegroundColor = Color.White;

        OkButton.Clicked += Confirm;
        CancelButton.Clicked += Cancel;
    }

    private void Cancel() => Hide();

    private void Confirm()
    {
        var text = AmountTextBox?.Text ?? string.Empty;

        Hide();

        if (!uint.TryParse(text, out var amount) || (amount == 0))
            return;

        OnConfirm?.Invoke(amount);
    }

    /// <summary>
    ///     Fired when the user confirms an item amount. Parameter is the parsed amount.
    /// </summary>
    public event AmountConfirmedHandler? OnConfirm;

    /// <summary>
    ///     Fired after the popup transitions from visible to hidden via any close path (OK, Cancel, ESC).
    ///     Used to clear ambient state set while the popup was open (e.g. the HUD description bar).
    /// </summary>
    public event Action? Closed;

    public override void Hide()
    {
        var wasVisible = Visible;

        base.Hide();

        if (wasVisible)
            Closed?.Invoke();
    }

    public override void Update(GameTime gameTime)
    {
        if (!Visible)
            return;

        base.Update(gameTime);

        OkButton?.Enabled = !string.IsNullOrEmpty(AmountTextBox?.Text);
    }

    /// <summary>
    ///     Opens the popup for a specific purpose. Purpose is mandatory: this one control serves trading, market
    ///     listings, and bank deposits, and a caller that forgot to declare its intent would silently inherit the
    ///     previous caller's — routing an exchange into the bank.
    /// </summary>
    /// <param name="purpose">What the confirmed amount will be used for.</param>
    /// <param name="slot">The 1-based inventory slot of the stackable item.</param>
    public void ShowFor(ItemAmountPurpose purpose, byte slot)
    {
        ItemSlot = slot;
        ItemName = string.Empty;

        Open(purpose);
    }

    /// <summary>Opens the prompt for a name-keyed purpose (the bank).</summary>
    public void ShowFor(ItemAmountPurpose purpose, string itemName)
    {
        ItemSlot = 0;
        ItemName = itemName;

        Open(purpose);
    }

    //each overload clears the key it does not use: a name left over from a withdraw riding into the next exchange is
    //the same footgun Purpose itself closed.
    private void Open(ItemAmountPurpose purpose)
    {
        Purpose = purpose;

        TitleLabel?.Text = purpose switch
        {
            ItemAmountPurpose.MarketListing => "How many will you list?",
            ItemAmountPurpose.BankDeposit   => "How many will you deposit?",
            ItemAmountPurpose.BankWithdraw  => "How many will you withdraw?",
            _                               => "How many will you give?"
        };

        if (AmountTextBox is not null)
        {
            AmountTextBox.Text = string.Empty;
            AmountTextBox.IsFocused = true;
        }

        OkButton?.Enabled = false;

        Show();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        switch (e.Keycode)
        {
            case Keycode.Escape:
                Hide();
                e.Handled = true;

                break;

            case Keycode.Enter:
                Confirm();
                e.Handled = true;

                break;
        }
    }
}