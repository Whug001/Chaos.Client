#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Options;

/// <summary>
///     Options → Chat sub-panel on the _nsett prefab. Four CustomCheckBox radio rows bound to the Task 6
///     SettingKey.ChatFilterMode choice (UserOption 7): a click routes SelectChoice → UserChoiceSelected →
///     WorldScreen.SendSetUserOption, and ValueChanged refreshes the selected row when the server echoes.
///     Slides out from MainOptions' left edge like the other sub-panels; the Task 6 provisional F4 rows
///     are gone (both chat prefs live in SettingSection.Chat, which F4 does not render).
/// </summary>
public sealed class ChatOptionsControl : PrefabPanel
{
    private const int CONTENT_LEFT = 22;
    private const int CONTENT_TOP = 28;
    private const int CONTENT_RIGHT = 22;
    private const int ROW_HEIGHT = 21;
    private const int HEADER_HEIGHT = 16;
    private const int SECTION_GAP = 4;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly CustomCheckBox[] ModeRows;
    private readonly UserOptions Options;
    private SlideAnimator Slide;
    private int SlideAnchorY;
    private bool SlideMode;

    public UIButton? OkButton { get; }

    public ChatOptionsControl(UserOptions options)
        : base("_nsett", false)
    {
        Options = options;
        Name = "ChatOptions";
        Visible = false;
        UsesControlStack = true;

        OkButton = CreateButton("OK");

        if (OkButton is not null)
            OkButton.Clicked += Close;

        var contentW = Width - CONTENT_LEFT - CONTENT_RIGHT;
        var choices = SettingDefinitions.ByKey(SettingKey.ChatFilterMode).Choices!;

        AddChild(
            new UILabel
            {
                Name = "Header_Chat",
                X = CONTENT_LEFT,
                Y = CONTENT_TOP,
                Width = contentW,
                Height = HEADER_HEIGHT,
                PaddingLeft = 0,
                PaddingTop = 0,
                ForegroundColor = LegendColors.White,
                Text = "Chat filter"
            });

        ModeRows = new CustomCheckBox[choices.Count];

        for (var i = 0; i < choices.Count; i++)
        {
            var index = i;
            var row = new CustomCheckBox
            {
                Name = $"cb_ChatFilterMode_{index}",
                X = CONTENT_LEFT,
                Y = CONTENT_TOP + HEADER_HEIGHT + SECTION_GAP + (index * ROW_HEIGHT),
                Width = contentW,
                Height = ROW_HEIGHT,
                Text = choices[index]
            };
            row.Clicked += () => Options.SelectChoice(SettingKey.ChatFilterMode, index);
            ModeRows[index] = row;
            AddChild(row);
        }

        AddChild(
            new UILabel
            {
                Name = "Hint_Chat",
                X = CONTENT_LEFT,
                Y = CONTENT_TOP + HEADER_HEIGHT + SECTION_GAP + (choices.Count * ROW_HEIGHT) + SECTION_GAP,
                Width = contentW,
                Height = ROW_HEIGHT,
                PaddingLeft = 0,
                PaddingTop = 0,
                ForegroundColor = TextColors.Default,
                Text = "Applies live; saved on your character."
            });

        RefreshSelection();

        Options.ValueChanged += OnValueChanged;

        //UserOptions is the source of truth (same convention as SettingsControl): re-seed the selected
        //row each time the panel opens, since hide clears interaction state.
        VisibilityChanged += visible =>
        {
            if (visible)
                RefreshSelection();
        };

        if (OkButton is not null)
        {
            OkButton.X = Width - OkButton.Width - OK_RIGHT_MARGIN;
            OkButton.Y = Height - OkButton.Height - OK_BOTTOM_MARGIN;
        }
    }

    private void OnValueChanged(SettingKey key, bool _)
    {
        if (key == SettingKey.ChatFilterMode)
            RefreshSelection();
    }

    private void RefreshSelection()
    {
        var selected = Options.ChoiceValue(SettingKey.ChatFilterMode);

        for (var i = 0; i < ModeRows.Length; i++)
            ModeRows[i].Checked = i == selected;
    }

    private void Close()
    {
        if (SlideMode)
        {
            InputDispatcher.Instance?.RemoveControl(this);
            Slide.SlideOut();
        } else
        {
            Hide();
            OnClose?.Invoke();
        }
    }

    public override void Hide()
    {
        InputDispatcher.Instance?.RemoveControl(this);

        if (SlideMode)
            Slide.Hide(this);
        else
            Visible = false;
    }

    public event CloseHandler? OnClose;

    public void SetSlideAnchor(int anchorX, int anchorY)
    {
        Slide.SetSlideAnchor(anchorX, Width);
        SlideAnchorY = anchorY;
    }

    /// <summary>Shows immediately at top-center of screen (hotkey mode).</summary>
    public override void Show()
    {
        this.CenterHorizontallyOnScreen();
        Y = 0;
        InputDispatcher.Instance?.PushControl(this);
        Visible = true;
        SlideMode = false;
    }

    /// <summary>Slides out from the left edge of MainOptionsControl (button mode).</summary>
    public void SlideIn()
    {
        if (Visible)
            return;

        Y = SlideAnchorY;
        InputDispatcher.Instance?.PushControl(this);
        Slide.SlideIn(this);
        SlideMode = true;
    }

    public override void Update(GameTime gameTime)
    {
        if (!Visible || !Enabled)
            return;

        if (Slide.Update(gameTime, this))
        {
            OnClose?.Invoke();

            return;
        }

        base.Update(gameTime);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (Slide.Sliding)
            return;

        if (e.Keycode == Keycode.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
