#region
using Chaos.Client.Controls.World.Popups.TownBallot;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The ballot window.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private TownBallotControl? TownBallot;

    private void WireTownBallot() => Game.Connection.OnTownBallot += HandleTownBallot;

    private void UnwireTownBallot()
    {
        Game.Connection.OnTownBallot -= HandleTownBallot;

        if (TownBallot is not null)
            TownBallot.InteractionRequested -= SendTownBallotInteraction;
    }

    private void HandleTownBallot(TownBallotArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (TownBallot is null)
        {
            TownBallot = new TownBallotControl(Game.AislingRenderer)
            {
                ZIndex = 2
            };

            TownBallot.InteractionRequested += SendTownBallotInteraction;
            Root.AddChild(TownBallot);
        }

        foreach (var candidate in args.Candidates)
            RequestGuildEmblem(candidate.GuildEmblemId);

        if (args.Type == TownBallotType.Open)
            TownBallot.Open(args);
        else
            TownBallot.Refresh(args);
    }

    private void SendTownBallotInteraction(TownBallotInteractionArgs args) => Game.Connection.SendTownBallotInteraction(args);
}
