#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Crane;
using Chaos.Client.ViewModel;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The crane prize reveal, and the operator's steering controls.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private CraneRevealControl? CraneReveal;

    //the operator's steering controls: the state the server's CraneControls packet drives, and its HUD (built on first use)
    private readonly CraneControlsState CraneControls = new();
    private CraneControlsHud? CraneControlsPanel;

    //when the last steering arrow went to the server, to pace a held key
    private long LastCraneSteerMs;

    /// <summary>
    ///     Whether the player is steering the crane claw now. The server consumes their walks and turns as steering and
    ///     their spacebar as the drop, so nothing is predicted locally (see <see cref="CraneSteeringRules" />).
    /// </summary>
    private bool CraneSteering => CraneControls.Active;

    private void WireCrane()
    {
        Game.Connection.OnCranePrizeReveal += HandleCranePrizeReveal;
        Game.Connection.OnCraneControls += HandleCraneControls;
    }

    private void UnwireCrane()
    {
        Game.Connection.OnCranePrizeReveal -= HandleCranePrizeReveal;
        Game.Connection.OnCraneControls -= HandleCraneControls;
        ResetCraneControls();
    }

    /// <summary>What <paramref name="input" /> does right now, steering or not.</summary>
    private CraneInputOutcome CraneInputFor(CraneInput input) => CraneSteeringRules.Decide(CraneSteering, input);

    /// <summary>Whether queued walks and path steps are held back because the player is steering the claw.</summary>
    private bool CraneHoldsSteps() => CraneInputFor(CraneInput.QueuedOrPathStep) == CraneInputOutcome.Suppress;

    /// <summary>Whether right-click pathing, turning and follow-assail are off because the player is steering.</summary>
    private bool CraneHoldsPointer() => CraneInputFor(CraneInput.PointerMove) == CraneInputOutcome.Suppress;

    private void HandleCraneControls(CraneControlsArgs args)
    {
        CraneControls.Apply(args);

        if (CraneControls.Active)
        {
            //a step or path started before steering must not go out as steering
            Pathfinding.Clear();
            QueuedWalkDirection = null;
        }

        PlaceCraneControls();
    }

    /// <summary>
    ///     Sends an arrow press as steering: a bare turn the server consumes to move the claw. The character never
    ///     steps or turns locally, so the server's answer (its true facing) changes nothing on screen. A held key is
    ///     paced; the keycap lights on every press.
    /// </summary>
    private void SteerCrane(Direction direction)
    {
        Pathfinding.Clear();
        QueuedWalkDirection = null;
        CraneControls.Press(CraneSteeringRules.KeyFor(direction));

        var now = Environment.TickCount64;

        if (!CraneSteeringRules.ShouldSendSteer(now, LastCraneSteerMs))
            return;

        LastCraneSteerMs = now;
        Game.Connection.Turn(direction);
    }

    /// <summary>Hides the steering controls: the map changed, or the world screen is going away.</summary>
    private void ResetCraneControls()
    {
        CraneControls.Clear();
        LastCraneSteerMs = 0;

        if (CraneControlsPanel is not null)
            CraneControlsPanel.Visible = false;
    }

    /// <summary>
    ///     Shows the controls at the bottom centre of the world view while steering, above the orange bar when that
    ///     overlaps the view; hides them otherwise. Called on every packet and every frame, since the HUD can switch size.
    /// </summary>
    private void PlaceCraneControls()
    {
        if (!CraneControls.Active)
        {
            if (CraneControlsPanel is not null)
                CraneControlsPanel.Visible = false;

            return;
        }

        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (CraneControlsPanel is null)
        {
            CraneControlsPanel = new CraneControlsHud(CraneControls)
            {
                ZIndex = 2
            };

            Root.AddChild(CraneControlsPanel);
        }

        var viewport = WorldHud.ViewportBounds;
        var bottom = viewport.Bottom - CRANE_CONTROLS_MARGIN;

        //an orange bar drawn over the bottom of the view keeps its strip
        var bar = WorldHud.OrangeBarBounds;

        if ((bar != viewport) && bar.Intersects(viewport) && (bar.Top > viewport.Top + CraneControlsHud.PANEL_HEIGHT))
            bottom = Math.Min(bottom, bar.Top - CRANE_CONTROLS_MARGIN);

        CraneControlsPanel.X = viewport.X + ((viewport.Width - CraneControlsHud.PANEL_WIDTH) / 2);
        CraneControlsPanel.Y = bottom - CraneControlsHud.PANEL_HEIGHT;
        CraneControlsPanel.Visible = true;
    }

    private const int CRANE_CONTROLS_MARGIN = 8;

    private void HandleCranePrizeReveal(CranePrizeRevealArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (CraneReveal is null)
        {
            CraneReveal = new CraneRevealControl(Game.CreatureRenderer, Root.Width, Root.Height)
            {
                ZIndex = 50
            };

            Root.AddChild(CraneReveal);

            //the winner's own line arrives when they turn the card over, never before (the room hears it from the server)
            CraneReveal.Flipped += prize => WorldState.Chat.AddOrangeBarMessage(CraneRevealRules.WinLine(prize));
        }

        //a step or turn queued before the reveal must not go out as steering while it plays
        Pathfinding.Clear();
        QueuedWalkDirection = null;

        //a second reveal while one is open replaces it
        CraneReveal.Start(args);
    }
}
