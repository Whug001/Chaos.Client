#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The battle window: a framed 620 by 440 panel titled with the fight's title line. A stub for now: it holds
///     the playback and advances it with real elapsed time. The board, rosters and log come in the window task.
///     Escape closes; a new fight replaces the one on screen. Borrows the <c>_nsett</c> prefab for its OK button
///     only, as the stable window does.
/// </summary>
public sealed class TamerBattleControl : FramedDialogPanelBase
{
    public const int PANEL_HEIGHT = 440;
    public const int PANEL_WIDTH = 620;
    public const int PANEL_X = 10;
    public const int PANEL_Y = 6;

    private const int LEFT = 20;
    private const int TITLE_TOP = 8;
    private const int STATUS_TOP = 40;

    private readonly UILabel TitleLabel;
    private readonly UILabel StatusLabel;

    private TamerBattlePlayback? Playback;

    /// <summary>The window was hidden, by the player. Closing tells the server nothing.</summary>
    public event Action? Closed;

    public TamerBattleControl()
        : base("_nsett", false)
    {
        Name = "Battle";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        X = PANEL_X;
        Y = PANEL_Y;

        OkButton = CreateCloseButton(Hide, 20, 3);

        TitleLabel = Label(0, TITLE_TOP, PANEL_WIDTH, string.Empty, LegendColors.Gold, HorizontalAlignment.Center);
        AddChild(TitleLabel);

        StatusLabel = Label(LEFT, STATUS_TOP, PANEL_WIDTH - (2 * LEFT), string.Empty, LegendColors.Gray, HorizontalAlignment.Left);
        AddChild(StatusLabel);
    }

    /// <summary>Shows the fight with a fresh playback.</summary>
    public void Open(TamerBattlePlayback playback)
    {
        Playback = playback ?? throw new ArgumentNullException(nameof(playback));
        TitleLabel.Text = playback.Fight.Title ?? string.Empty;
        Show();
        Refresh();
    }

    /// <summary>Shows the latest clock. Called every update while visible.</summary>
    public void Refresh()
    {
        if (!Visible || (Playback is null))
            return;

        StatusLabel.Text = $"{TamerBattleText.VersusText(Playback.Fight.AttackerName, Playback.Fight.DefenderName)}  {TamerBattleText.ClockText(Playback.ClockTicks)}s";
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Playback is not null)
        {
            Playback.Advance(gameTime.ElapsedGameTime);
            Refresh();
        }

        base.Update(gameTime);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();
        Closed?.Invoke();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private static UILabel Label(int x, int y, int width, string text, Color color, HorizontalAlignment alignment)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            Text = text,
            ForegroundColor = color,
            HorizontalAlignment = alignment,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
}
