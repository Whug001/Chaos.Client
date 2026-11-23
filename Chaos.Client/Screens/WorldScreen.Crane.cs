#region
using Chaos.Client.Controls.World.Popups.Crane;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The crane prize reveal.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private CraneRevealControl? CraneReveal;

    private void WireCrane() => Game.Connection.OnCranePrizeReveal += HandleCranePrizeReveal;

    private void UnwireCrane() => Game.Connection.OnCranePrizeReveal -= HandleCranePrizeReveal;

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
        }

        //a step or turn queued before the reveal must not go out as steering while it plays
        Pathfinding.Clear();
        QueuedWalkDirection = null;

        //a second reveal while one is open replaces it
        CraneReveal.Start(args);
    }
}
