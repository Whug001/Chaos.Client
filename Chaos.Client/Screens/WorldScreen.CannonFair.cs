using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Hud;
using Chaos.Client.Controls.World.Popups.CannonFair;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private CannonFairControl? CannonFairWindow;

#if DEBUG
    private CannonFairLocalTest? CannonFairTest;
#endif

    private void WireCannonFair()
    {
        Game.Connection.OnCannonFairDisplay += HandleCannonFairDisplay;
        WorldState.CannonFair.Confirm = id => Game.Connection.SendCannonFairAction(new CannonFairActionArgs { Kind = CannonFairActionKind.RecordingReady, VolleyId = (ushort)id });
        WorldState.CannonFair.LockIn = (angle, power) => Game.Connection.SendCannonFairAction(new CannonFairActionArgs
        {
            Kind = CannonFairActionKind.LockIn,
            AngleTenths = (short)MathF.Round(angle * 10f),
            PowerThousandths = (short)MathF.Round(power * 1000f)
        });
#if DEBUG
        ChatInputControl.CannonFairTestRequested += OpenCannonFairTest;
#endif
    }

    private void UnwireCannonFair()
    {
        Game.Connection.OnCannonFairDisplay -= HandleCannonFairDisplay;
        WorldState.CannonFair.Confirm = null;
        WorldState.CannonFair.LockIn = null;
#if DEBUG
        ChatInputControl.CannonFairTestRequested -= OpenCannonFairTest;
        CannonFairTest = null;
#endif

#if DEBUG
        if (CannonFairWindow is not null)
            CannonFairWindow.LeaveRequested -= OnCannonFairLeaveRequested;
#else
        if (CannonFairWindow is not null)
            CannonFairWindow.LeaveRequested -= SendCannonFairLeave;
#endif
    }

    private void HandleCannonFairDisplay(CannonFairDisplayArgs args)
    {
        if (Root is null)
            return;

        WorldState.CannonFair.Apply(args, Environment.TickCount64);

        if (args.Kind == CannonFairMessageKind.Open)
        {
#if DEBUG
            CannonFairTest = null;
#endif
            EnsureCannonFairWindow();
            CannonFairWindow!.Open(WorldState.CannonFair);
        } else if (args.Kind == CannonFairMessageKind.Close)
            CannonFairWindow?.CloseFromServer();
    }

    private void EnsureCannonFairWindow()
    {
        if (CannonFairWindow is not null)
            return;

        CannonFairWindow = new CannonFairControl(Game.AislingRenderer, Game.CreatureRenderer) { ZIndex = 2 };
#if DEBUG
        CannonFairWindow.LeaveRequested += OnCannonFairLeaveRequested;
#else
        CannonFairWindow.LeaveRequested += SendCannonFairLeave;
#endif
        Root!.AddChild(CannonFairWindow);
    }

    private void SendCannonFairLeave()
        => Game.Connection.SendCannonFairAction(new CannonFairActionArgs { Kind = CannonFairActionKind.Leave });

#if DEBUG
    private void OnCannonFairLeaveRequested()
    {
        if (CannonFairTest is not null)
        {
            CloseCannonFairTest();

            return;
        }

        SendCannonFairLeave();
    }

    private void CloseCannonFairTest()
    {
        CannonFairTest?.Stop();
        CannonFairTest = null;
        CannonFairWindow?.CloseFromServer();
    }

    private void OpenCannonFairTest()
    {
        if (Root is null)
            return;

        WorldHud.ChatInput.Unfocus();
        EnsureCannonFairWindow();

        var session = new CannonFairSession();
        CannonFairTest = new CannonFairLocalTest(session);
        session.LockIn = CannonFairTest.OnLockIn;
        CannonFairWindow!.Open(session);
        CannonFairTest.Start();
    }
#endif
}
