#region
using Chaos.Client.Controls.World;
using Chaos.Client.Controls.World.Popups.HallBattle;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Guild hall battles: the war room, the challenge and invite pop-up, and the in-match bar.</summary>
public sealed partial class WorldScreen
{
    //each built on first use
    private HallBattleBarControl? HallBattleBar;
    private HallBattleOfferWindow? HallBattleOffer;
    private WarRoomControl? WarRoom;

    //the map the hall battle bar was last shown on: a map change anywhere else clears the bar
    private short HallBattleBarMapId = -1;

    private void WireHallBattle() => Game.Connection.OnHallBattleDisplay += HandleHallBattleDisplay;

    private void UnwireHallBattle()
    {
        Game.Connection.OnHallBattleDisplay -= HandleHallBattleDisplay;

        if (WarRoom is not null)
            WarRoom.Action -= SendHallBattleAction;

        if (HallBattleOffer is not null)
            HallBattleOffer.Action -= SendHallBattleAction;
    }

    private void SendHallBattleAction(HallBattleActionArgs args) => Game.Connection.SendHallBattleAction(args);

    private void HandleHallBattleDisplay(HallBattleDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        switch (args.Kind)
        {
            case HallBattleDisplayKind.Board:
            case HallBattleDisplayKind.GuildDetail:
            case HallBattleDisplayKind.Team:
                EnsureWarRoom().Apply(args);

                break;

            case HallBattleDisplayKind.Offer:
                EnsureHallBattleOffer().ShowOffer(args);

                break;

            case HallBattleDisplayKind.Close:
                HallBattleOffer?.Close();

                //the team page is stale once the battle is gone
                if (WarRoom is { Visible: true, Page: WarRoomPage.Team })
                    WarRoom.Hide();

                break;

            case HallBattleDisplayKind.Bar:
                EnsureHallBattleBar().Show(args);
                HallBattleBarMapId = CurrentMapId;

                break;

            case HallBattleDisplayKind.BarClear:
                HallBattleBar?.Clear();
                HallBattleBarMapId = -1;

                break;
        }
    }

    private WarRoomControl EnsureWarRoom()
    {
        if (WarRoom is not null)
            return WarRoom;

        WarRoom = new WarRoomControl(Root!.Width, Root.Height) { ZIndex = 50 };
        WarRoom.Action += SendHallBattleAction;
        Root.AddChild(WarRoom);

        return WarRoom;
    }

    private HallBattleOfferWindow EnsureHallBattleOffer()
    {
        if (HallBattleOffer is not null)
            return HallBattleOffer;

        HallBattleOffer = new HallBattleOfferWindow(Root!.Width, Root.Height) { ZIndex = 55 };
        HallBattleOffer.Action += SendHallBattleAction;
        Root.AddChild(HallBattleOffer);

        return HallBattleOffer;
    }

    private HallBattleBarControl EnsureHallBattleBar()
    {
        if (HallBattleBar is not null)
            return HallBattleBar;

        HallBattleBar = new HallBattleBarControl(Root!.Width) { ZIndex = 55 };
        Root.AddChild(HallBattleBar);

        return HallBattleBar;
    }
}
