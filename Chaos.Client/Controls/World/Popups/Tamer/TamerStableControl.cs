#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The stable window frame only (Task 2 stub): the Creatures and Squads tabs arrive in Task 3. Opens on the
///     first snapshot after the catalog, refreshes while visible, and hides without echoing Close on a server close.
///     Borrows the <c>_nsett</c> prefab for its OK button only, as the card binder does.
/// </summary>
public sealed class TamerStableControl : FramedDialogPanelBase
{
    public const int PANEL_HEIGHT = 440;
    public const int PANEL_WIDTH = 620;
    public const int PANEL_X = 10;
    public const int PANEL_Y = 6;

    private const int LEFT = 20;
    private const int TITLE_TOP = 8;
    private const int STATUS_TOP = 60;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly UILabel StatusLabel;
    private bool ClosedByServer;

    /// <summary>A stable window request (Close from the Close button or Escape).</summary>
    public event Action<TamerActionArgs>? ActionRequested;

    /// <summary>The player closed the window (Close or Escape). Fires on a server close too; the Close request is not sent then.</summary>
    public event Action? Closed;

    public TamerStableControl()
        : base("_nsett", false)
    {
        Name = "Stable";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        X = PANEL_X;
        Y = PANEL_Y;

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        AddChild(
            new UILabel
            {
                X = 0,
                Y = TITLE_TOP,
                Width = PANEL_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                Text = TamerStableText.WindowTitle,
                ForegroundColor = LegendColors.Gold,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            });

        StatusLabel = new UILabel
        {
            X = LEFT,
            Y = STATUS_TOP,
            Width = PANEL_WIDTH - (2 * LEFT),
            Height = TextRenderer.CHAR_HEIGHT,
            Text = TamerStableText.Opening,
            ForegroundColor = LegendColors.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };
        AddChild(StatusLabel);
    }

    public void Open()
    {
        Show();
        Refresh();
    }

    /// <summary>Shows the latest state. Called after every catalog and snapshot while the window is open.</summary>
    public void Refresh()
    {
        if (!Visible)
            return;

        var state = WorldState.TamerStable;

        if (!state.HasCatalog)
        {
            StatusLabel.Text = TamerStableText.Opening;
            return;
        }

        if (!state.HasSnapshot)
        {
            StatusLabel.Text = TamerStableText.Opening;
            return;
        }

        StatusLabel.Text = state.Monsters.Count == 0
            ? TamerStableText.EmptyStable
            : TamerStableText.CountLine(state.Monsters.Count, state.StableCapacity);
    }

    /// <summary>The server closed the window: hide without sending Close back.</summary>
    public void CloseFromServer()
    {
        ClosedByServer = true;
        Hide();
        ClosedByServer = false;
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();

        if (!ClosedByServer)
            ActionRequested?.Invoke(TamerStableState.CloseAction());

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
}
