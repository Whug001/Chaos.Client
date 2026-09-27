using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Controls.World.Popups.Lockpicking;

/// <summary>
///     The lockpicking window: a title coloured by difficulty, a recessed window holding the lock, a row with the
///     pick count and the turn key, a message line and Close. It reuses the ornate dialog frame, like
///     <see cref="Wheel.GildedSpindleControl" />. It is a pure renderer: the server judges every turn, and this
///     control only animates the answer. The world screen opens it on the server's Open display.
/// </summary>
public sealed class LockpickControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 236;
    private const int FRAME_BOTTOM_BORDER = 47;
    private const int CONTENT_LEFT = 20;
    private const int CONTENT_RIGHT = 20;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int TITLE_TOP = 8;
    private const int WINDOW_TOP = 26;
    private const int WINDOW_SIZE = 196;
    private const int WINDOW_PADDING = (WINDOW_SIZE - LockFaceControl.SIZE) / 2;
    private const int WINDOW_X = (PANEL_WIDTH - WINDOW_SIZE) / 2;
    private const int INFO_TOP = WINDOW_TOP + WINDOW_SIZE + 8;
    private const int MESSAGE_TOP = INFO_TOP + 16;
    private const int PANEL_HEIGHT = MESSAGE_TOP + TextRenderer.CHAR_HEIGHT + 1 + FRAME_BOTTOM_BORDER;
    private const float CLOSE_MESSAGE_SECONDS = 1f;
    private const int SOUND_JAM = 9;

    private static readonly SKColor RecessedFillColor = new(10, 8, 5, 255);

    private readonly LockpickAnimator Animator = new();
    private readonly LockFaceControl Face;
    private readonly UILabel CountLabel;
    private readonly UILabel MessageLabel;
    private readonly SoundSystem SoundSystem;
    private readonly UILabel TitleLabel;
    private float CloseAfterSeconds = -1f;
    private bool ShowedTimeout;

    /// <summary>A turn started, at this pick angle.</summary>
    public event Action<byte>? TurnRequested;

    /// <summary>The window closed, for any reason. The world screen tells the server.</summary>
    public event Action? Closed;

    public LockpickControl(SoundSystem soundSystem)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(soundSystem);

        SoundSystem = soundSystem;
        Name = "Lockpick";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = new UILabel
        {
            X = 0,
            Y = TITLE_TOP,
            Width = PANEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(TitleLabel);

        //recessed window, added before the face so it draws underneath it
        AddChild(
            new UIPanel
            {
                X = WINDOW_X,
                Y = WINDOW_TOP,
                Width = WINDOW_SIZE,
                Height = WINDOW_SIZE,
                Background = DialogFrame.BuildRecessedTexture(RecessedFillColor, WINDOW_SIZE, WINDOW_SIZE),
                IsHitTestVisible = false
            });

        Face = new LockFaceControl(Animator)
        {
            X = WINDOW_X + WINDOW_PADDING,
            Y = WINDOW_TOP + WINDOW_PADDING
        };
        AddChild(Face);

        CountLabel = new UILabel
        {
            X = CONTENT_LEFT,
            Y = INFO_TOP,
            Width = (PANEL_WIDTH / 2) - CONTENT_LEFT,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(CountLabel);

        AddChild(
            new UILabel
            {
                X = PANEL_WIDTH / 2,
                Y = INFO_TOP,
                Width = (PANEL_WIDTH / 2) - CONTENT_RIGHT,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Right,
                ForegroundColor = LegendColors.Gray,
                Text = "Space: turn",
                IsHitTestVisible = false
            });

        MessageLabel = new UILabel
        {
            X = CONTENT_LEFT,
            Y = MESSAGE_TOP,
            Width = PANEL_WIDTH - CONTENT_LEFT - CONTENT_RIGHT,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(MessageLabel);
    }

    /// <summary>Repaints from <see cref="WorldState.Lockpick" /> (already filled by ApplyOpen) and shows the window.</summary>
    public override void Show()
    {
        Animator.Reset();
        CloseAfterSeconds = -1f;
        ShowedTimeout = false;

        var state = WorldState.Lockpick;
        TitleLabel.Text = state.Title;

        TitleLabel.ForegroundColor = state.Difficulty switch
        {
            LockpickDifficulty.Easy   => LegendColors.Lime,
            LockpickDifficulty.Medium => LegendColors.CanaryYellow,
            _                         => LegendColors.Red
        };

        RefreshCount();
        SetMessage("Move the mouse to set the pick.");

        base.Show();
    }

    public override void Hide()
    {
        var wasVisible = Visible;

        Animator.Reset();
        CloseAfterSeconds = -1f;

        base.Hide();
        WorldState.Lockpick.Clear();

        if (wasVisible)
            Closed?.Invoke();
    }

    /// <summary>The server's answer to the turn in progress (<see cref="WorldState.Lockpick" /> is already updated).</summary>
    public void OnTurnResult(LockpickTurnOutcome outcome, byte turnPercent)
    {
        Animator.ApplyResult(outcome, turnPercent);
        RefreshCount();

        switch (outcome)
        {
            case LockpickTurnOutcome.Jammed:
                SoundSystem.PlaySound(SOUND_JAM);
                SetMessage("The pick strains...");

                break;

            case LockpickTurnOutcome.Opened:
                SetMessage("The lock clicks open!");

                break;

            case LockpickTurnOutcome.Broke:
                SetMessage("Your lockpick broke!");

                break;
        }

        //a late Opened/Broke reply can arrive after the animator's own 2-second timeout has already
        //returned it to Idle (or sent it back toward Idle via Returning). The animation can no longer show
        //the result, so close the window the same way a server Close does, with the outcome's message
        //already set above
        if ((outcome is LockpickTurnOutcome.Opened or LockpickTurnOutcome.Broke)
            && (Animator.State is LockpickAnimState.Idle or LockpickAnimState.Returning))
            CloseAfterSeconds = CLOSE_MESSAGE_SECONDS;
    }

    /// <summary>The server ended the session. A reason is shown for a moment first.</summary>
    public void OnServerClose(string? reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            Hide();

            return;
        }

        SetMessage(reason);
        CloseAfterSeconds = CLOSE_MESSAGE_SECONDS;
    }

    /// <summary>Ticked by the world screen, in seconds.</summary>
    public void Update(float seconds)
    {
        if (!Visible)
            return;

        Animator.Update(seconds);

        if (Animator.TimedOut && !ShowedTimeout)
        {
            ShowedTimeout = true;
            SetMessage("No answer from the server.");
        }

        if (Animator.State == LockpickAnimState.Finished)
        {
            Hide();

            return;
        }

        if (CloseAfterSeconds >= 0f)
        {
            CloseAfterSeconds -= seconds;

            if (CloseAfterSeconds < 0f)
                Hide();
        }
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        var center = Face.CenterScreen;
        Animator.SetPick(LockpickGeometry.PickDegreesFrom(e.ScreenX - center.X, e.ScreenY - center.Y));

        base.OnMouseMove(e);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (e.Button == MouseButton.Right)
        {
            TryTurn(false);
            e.Handled = true;

            return;
        }

        base.OnMouseDown(e);
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button == MouseButton.Right)
        {
            Animator.ReleaseTurn();
            e.Handled = true;

            return;
        }

        base.OnMouseUp(e);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        switch (e.Keycode)
        {
            case Keycode.Escape:
                Hide();
                e.Handled = true;

                return;

            case Keycode.Space:
                TryTurn(e.IsRepeat);
                e.Handled = true;

                return;

            case Keycode.Left:
                Animator.NudgePick(-LockpickAnimator.PICK_STEP_DEGREES);
                e.Handled = true;

                return;

            case Keycode.Right:
                Animator.NudgePick(LockpickAnimator.PICK_STEP_DEGREES);
                e.Handled = true;

                return;
        }

        base.OnKeyDown(e);
    }

    public override void OnKeyUp(KeyUpEvent e)
    {
        if (e.Keycode == Keycode.Space)
        {
            Animator.ReleaseTurn();
            e.Handled = true;

            return;
        }

        base.OnKeyUp(e);
    }

    private void RefreshCount() => CountLabel.Text = $"Lockpicks: {WorldState.Lockpick.LockpickCount}";

    private void SetMessage(string text) => MessageLabel.Text = text;

    private void TryTurn(bool isRepeat)
    {
        if (!Animator.PressTurn(isRepeat))
            return;

        ShowedTimeout = false;
        SetMessage(string.Empty);
        TurnRequested?.Invoke((byte)MathF.Round(Animator.PickDegrees));
    }
}
