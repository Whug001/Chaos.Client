using Chaos.Client.Controls.World.Popups.TurkeyShoot;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    //built on the first Open, so players who never hunt never load its art
    private TurkeyShootControl? TurkeyShootWindow;

    private void WireTurkeyShoot() => Game.Connection.OnTurkeyShootDisplay += HandleTurkeyShootDisplay;

    private void UnwireTurkeyShoot()
    {
        Game.Connection.OnTurkeyShootDisplay -= HandleTurkeyShootDisplay;

        if (TurkeyShootWindow is not null)
        {
            TurkeyShootWindow.ShootRequested -= SendTurkeyShot;
            TurkeyShootWindow.Closed -= SendTurkeyShootLeave;
        }
    }

    private void HandleTurkeyShootDisplay(TurkeyShootDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (TurkeyShootWindow is null)
        {
            if (args.Type != TurkeyShootDisplayType.Open)
                return;

            TurkeyShootWindow = new TurkeyShootControl(Game.SoundSystem)
            {
                ZIndex = 2
            };

            TurkeyShootWindow.ShootRequested += SendTurkeyShot;
            TurkeyShootWindow.Closed += SendTurkeyShootLeave;
            Root.AddChild(TurkeyShootWindow);
        }

        switch (args.Type)
        {
            case TurkeyShootDisplayType.Open:
                TurkeyShootWindow.Open(args);

                break;
            case TurkeyShootDisplayType.Close:
                TurkeyShootWindow.CloseFromServer();

                break;
            default:
                //a hunter who closed the window still gets the round's last messages
                if (TurkeyShootWindow.Visible)
                    TurkeyShootWindow.Apply(args);

                break;
        }
    }

    private void SendTurkeyShot(TurkeyShootActionArgs args) => Game.Connection.SendTurkeyShootAction(args);

    private void SendTurkeyShootLeave()
        => Game.Connection.SendTurkeyShootAction(new TurkeyShootActionArgs { Type = TurkeyShootActionType.Leave });
}
