#region
using Chaos.Client.Controls.World.Popups.TowerLeaderboard;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The Endless Tower leaderboard window.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private TowerLeaderboardControl? TowerLeaderboard;

    private void WireTowerLeaderboard() => Game.Connection.OnTowerLeaderboard += HandleTowerLeaderboard;

    private void UnwireTowerLeaderboard()
    {
        Game.Connection.OnTowerLeaderboard -= HandleTowerLeaderboard;

        if (TowerLeaderboard is not null)
            TowerLeaderboard.SeasonRequested -= SendTowerLeaderboardRequest;
    }

    private void HandleTowerLeaderboard(TowerLeaderboardArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (TowerLeaderboard is null)
        {
            TowerLeaderboard = new TowerLeaderboardControl(Game.AislingRenderer)
            {
                ZIndex = 2
            };

            TowerLeaderboard.SeasonRequested += SendTowerLeaderboardRequest;
            Root.AddChild(TowerLeaderboard);
        }

        TowerLeaderboard.Show(args);
    }

    private void SendTowerLeaderboardRequest(TowerLeaderboardRequestArgs args) => Game.Connection.SendTowerLeaderboardRequest(args);
}
