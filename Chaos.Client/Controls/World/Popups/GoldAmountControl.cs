#region
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     Gold drop/exchange popup using _nmoney prefab. Stores the pending drop target (entity or tile)
///     so the caller only needs to show it and subscribe to OnConfirm.
/// </summary>
public sealed class GoldAmountControl : PrefabPanel
{
    private UILabel? TitleLabel { get; }

    /// <summary>
    ///     What the confirmed amount will be used for. Set by <see cref="ShowFor" />, so the popup can never be opened
    ///     without declaring intent — the confirm handler routes on this rather than on ambient screen state.
    /// </summary>
    public GoldAmountPurpose Purpose { get; private set; } = GoldAmountPurpose.Drop;

    /// <summary>
    ///     Entity ID to drop gold on, or null for ground drop.
    /// </summary>
    public uint? TargetEntityId { get; private set; }

    public int TargetTileX { get; private set; }
    public int TargetTileY { get; private set; }
    public UITextBox? AmountTextBox { get; }
    public UIButton? CancelButton { get; }
    public UIButton? OkButton { get; }

    public GoldAmountControl()
        : base("_nmoney")
    {
        Name = "GoldExchange";
        Visible = false;
        UsesControlStack = true;

        OkButton = CreateButton("OK");
        CancelButton = CreateButton("Cancel");
        AmountTextBox = CreateTextBox("Text", 10);

        //replace any existing text display with a label showing the prompt
        TitleLabel = CreateLabel("Title");

        TitleLabel?.ForegroundColor = Color.White;

        if (OkButton is not null)
            OkButton.Clicked += Confirm;

        if (CancelButton is not null)
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
    ///     Fired when the user confirms a gold amount. Parameter is the parsed amount.
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

    public override void Show()
    {
        if (AmountTextBox is not null)
        {
            AmountTextBox.Text = string.Empty;
            AmountTextBox.IsFocused = true;
        }

        base.Show();
    }

    /// <summary>
    ///     Opens the popup for a specific purpose. Purpose is mandatory: this one control serves dropping, trading, and
    ///     both bank directions, and a caller that forgot to declare its intent would silently inherit the previous
    ///     caller's — routing a gold drop into the bank.
    /// </summary>
    /// <param name="purpose">What the confirmed amount will be used for.</param>
    /// <param name="entityId">The entity to drop gold on, or null for a ground drop.</param>
    /// <param name="tileX">The tile to drop gold on.</param>
    /// <param name="tileY">The tile to drop gold on.</param>
    public void ShowFor(
        GoldAmountPurpose purpose,
        uint? entityId = null,
        int tileX = 0,
        int tileY = 0)
    {
        Purpose = purpose;
        TargetEntityId = entityId;
        TargetTileX = tileX;
        TargetTileY = tileY;

        TitleLabel?.Text = purpose switch
        {
            GoldAmountPurpose.BankDeposit  => "Gold amount to deposit?",
            GoldAmountPurpose.BankWithdraw => "Gold amount to withdraw?",
            _                              => "Gold amount to drop?"
        };

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