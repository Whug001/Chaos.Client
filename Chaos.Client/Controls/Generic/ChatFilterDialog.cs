#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Data;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.Generic;

/// <summary>
///     First-run chat-filter dialog. Shown once per player after the login sync reports them unconfigured;
///     Continue saves the picked mode and flips the configured flag through the Task 6 explicit-Set path.
///     Step-1 decision (Task 7): OkPopupMessageControl is the base pattern — UIPanel plus a DlgBack2.spf
///     tiled background, butt001.epf buttons, and control-stack modal Show/Hide. TextPopupControl was
///     rejected: it is a fixed full-screen scrolled reader with Close-only semantics and no option rows.
/// </summary>
public sealed class ChatFilterDialog : UIPanel
{
    private const int TILES_WIDE = 4;
    private const int CONTENT_PADDING = 6;
    private const int BUTTON_MARGIN = 1;

    private const int TITLE_HEIGHT = 16;
    private const int TITLE_GAP = 4;
    private const int EXPLANATION_HEIGHT = 36;
    private const int ROWS_GAP = 6;
    private const int ROW_HEIGHT = 21;
    private const int AFTER_ROWS_GAP = 8;
    private const int BOTTOM_MARGIN = 4;

    //butt001.epf frame indices — 2 frames per button (normal/pressed), same OK pair OkPopupMessageControl uses.
    private const int CONTINUE_NORMAL = 15;
    private const int CONTINUE_PRESSED = 16;

    private const string TITLE = "Customize Your Chat Experience";
    private const string EXPLANATION = "Chat can include slurs and profanity — pick how you want it filtered.";

    /// <summary>Index into the ChatFilterMode choices preselected when the dialog opens (Fantasy).</summary>
    public const int DefaultSelection = 1;

    private readonly CustomCheckBox[] ModeRows;
    private readonly UIButton ContinueButton;

    private int PendingSelection = DefaultSelection;

    public ChatFilterDialog()
    {
        Visible = false;
        UsesControlStack = true;

        //load background tile
        using var bgTile = DataContext.UserControls.GetSpfImage("DlgBack2.spf");

        if (bgTile is null)
            throw new InvalidOperationException("Failed to load DlgBack2.spf");

        //button textures first: the interior height is exact, not a guess.
        var cache = UiRenderer.Instance!;
        var continueNormalTex = cache.GetEpfTexture("butt001.epf", CONTINUE_NORMAL);
        var continuePressedTex = cache.GetEpfTexture("butt001.epf", CONTINUE_PRESSED);
        var buttonHeight = continueNormalTex.Height;

        var interiorHeight = CONTENT_PADDING
                             + TITLE_HEIGHT
                             + TITLE_GAP
                             + EXPLANATION_HEIGHT
                             + ROWS_GAP
                             + (4 * ROW_HEIGHT)
                             + AFTER_ROWS_GAP
                             + buttonHeight
                             + BOTTOM_MARGIN;

        var borderSize = DialogFrame.BORDER_SIZE;
        var interiorWidth = bgTile.Width * TILES_WIDE - 23;
        var totalWidth = borderSize + interiorWidth + borderSize;
        var totalHeight = borderSize + interiorHeight + borderSize;

        //composite tiled background with border
        using var composite = DialogFrame.Composite(bgTile, totalWidth, totalHeight);

        if (composite is null)
            throw new InvalidOperationException("Failed to composite dialog background");

        Width = totalWidth;
        Height = totalHeight;
        this.CenterOnScreen();
        Background = TextureConverter.ToTexture2D(composite);

        var contentX = borderSize + CONTENT_PADDING;
        var contentWidth = interiorWidth - CONTENT_PADDING * 2;
        var y = borderSize + CONTENT_PADDING;

        //title — manually centered (UILabel alignment is left-biased like OkPopupMessageControl's label).
        var titleWidth = TextRenderer.MeasureWidth(TITLE);
        AddChild(
            new UILabel
            {
                Name = "Title",
                X = contentX + ((contentWidth - titleWidth) / 2),
                Y = y,
                Width = titleWidth,
                Height = TITLE_HEIGHT,
                PaddingLeft = 0,
                PaddingTop = 0,
                ForegroundColor = Color.White,
                Text = TITLE
            });
        y += TITLE_HEIGHT + TITLE_GAP;

        AddChild(
            new UILabel
            {
                Name = "Explanation",
                X = contentX,
                Y = y,
                Width = contentWidth,
                Height = EXPLANATION_HEIGHT,
                PaddingLeft = 0,
                PaddingTop = 0,
                WordWrap = true,
                ForegroundColor = Color.White,
                VerticalAlignment = VerticalAlignment.Top,
                Text = EXPLANATION
            });
        y += EXPLANATION_HEIGHT + ROWS_GAP;

        //mode picker — labels come from the same Choices the options panel renders, so the rows can
        //never drift from the stored values. Nothing is written until Continue: the Task 6 cache keeps
        //its Unfiltered default while the player decides.
        var choices = SettingDefinitions.ByKey(SettingKey.ChatFilterMode).Choices!;
        ModeRows = new CustomCheckBox[choices.Count];

        for (var i = 0; i < choices.Count; i++)
        {
            var index = i;
            var row = new CustomCheckBox
            {
                Name = $"mode_{index}",
                X = contentX,
                Y = y + (index * ROW_HEIGHT),
                Width = contentWidth,
                Height = ROW_HEIGHT,
                Text = choices[index]
            };
            row.Clicked += () =>
            {
                PendingSelection = index;
                RefreshPending();
            };
            ModeRows[index] = row;
            AddChild(row);
        }

        var buttonY = totalHeight - borderSize - buttonHeight - BUTTON_MARGIN + 5;
        var continueX = totalWidth - borderSize - BUTTON_MARGIN + 4 - continueNormalTex.Width;

        ContinueButton = new UIButton
        {
            Name = "Continue",
            X = continueX,
            Y = buttonY,
            Width = continueNormalTex.Width,
            Height = buttonHeight,
            NormalTexture = continueNormalTex,
            PressedTexture = continuePressedTex
        };
        ContinueButton.Clicked += OnContinueClicked;
        AddChild(ContinueButton);

        RefreshPending();
    }

    /// <summary>Whether the first-run dialog should appear (unconfigured and not already shown this session).</summary>
    public static bool ShouldShowDialog(bool hasConfigured, bool shownThisSession)
        => !hasConfigured && !shownThisSession;

    public event ChatFilterHandler? OnContinue;

    public void Show()
    {
        //Fantasy preselected, Unfiltered beside it: a suggestion, not a save — the stored default stays
        //Unfiltered until Continue sends through the Task 6 path.
        PendingSelection = DefaultSelection;
        RefreshPending();
        InputDispatcher.Instance!.PushControl(this);
        Visible = true;
    }

    public void Hide()
    {
        InputDispatcher.Instance!.RemoveControl(this);
        Visible = false;
    }

    private void OnContinueClicked()
    {
        var selection = PendingSelection;
        Hide();
        OnContinue?.Invoke(selection);
    }

    private void RefreshPending()
    {
        for (var i = 0; i < ModeRows.Length; i++)
            ModeRows[i].Checked = i == PendingSelection;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode is Keycode.Enter or Keycode.Space)
        {
            ContinueButton.PerformClick();
            e.Handled = true;
        } else if (e.Keycode == Keycode.Escape)
        {
            //swallowed on purpose: this is a must-answer first-run choice, and Escape must not
            //save (Continue-only saves) nor strand the player unconfigured.
            e.Handled = true;
        }
    }
}
