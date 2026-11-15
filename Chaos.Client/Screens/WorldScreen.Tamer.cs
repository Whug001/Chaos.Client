#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Tamer;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     The stable window: built on first use, fed by TamerDisplay. A snapshot always follows a catalog on open,
///     so the window opens on the first snapshot after a catalog and otherwise only refreshes while visible.
///     A Ranked display goes to <c>WorldState.TamerRanked</c> and refreshes a visible window; it never opens one.
///     The battle window: built on first use, fed by TamerBattleDisplay parts through the assembly; a ready fight
///     opens it with a new playback, a broken fight shows the orange bar, and it stays open across map changes
///     but closes on logout (ResetAll clears the assembly and playback). It draws above the stable window and is
///     kept above it on the control stack, so with both open the keys go to the battle window. While the battle
///     window is up the stable window sends nothing but Close, and opening it drops any mouse capture the stable
///     window holds, so a press made before it opened clicks nothing on release.
/// </summary>
public sealed partial class WorldScreen
{
    private TamerStableControl? TamerStableWindow;
    private TamerBattleControl? TamerBattleWindow;

    private void WireTamer()
    {
        Game.Connection.OnTamerDisplay += HandleTamerDisplay;
        Game.Connection.OnTamerBattleDisplay += HandleTamerBattleDisplay;
    }

    private void UnwireTamer()
    {
        Game.Connection.OnTamerDisplay -= HandleTamerDisplay;
        Game.Connection.OnTamerBattleDisplay -= HandleTamerBattleDisplay;

        if (TamerStableWindow is not null)
        {
            TamerStableWindow.ActionRequested -= SendTamerAction;
            TamerStableWindow.Closed -= OnTamerStableClosed;
        }

        if (TamerBattleWindow is not null)
        {
            TamerBattleWindow.Closed -= OnTamerBattleClosed;
            TamerBattleWindow.Hide();
        }
    }

    private bool EnsureTamerWindow()
    {
        if (TamerStableWindow is not null)
            return true;

        var window = new TamerStableControl(Game.CreatureRenderer) { ZIndex = 2 };

        window.ActionRequested += SendTamerAction;
        window.Closed += OnTamerStableClosed;

        Root!.AddChild(window);

        TamerStableWindow = window;

        return true;
    }

    private void HandleTamerDisplay(TamerDisplayArgs args)
    {
        if (Root is null)
            return;

        switch (args.Kind)
        {
            case TamerDisplayKind.Catalog:
                WorldState.TamerStable.ApplyCatalog(args);

                if (TamerStableWindow?.Visible ?? false)
                    TamerStableWindow.Refresh();

                break;
            case TamerDisplayKind.Snapshot:
                WorldState.TamerStable.ApplySnapshot(args);

                var shouldOpen = WorldState.TamerStable.ConsumeCatalogJustArrived();

                if (TamerStableWindow?.Visible ?? false)
                    TamerStableWindow.Refresh();
                else if (shouldOpen)
                {
                    if (!EnsureTamerWindow())
                        return;

                    TamerStableWindow!.Open();
                    KeepTamerBattleOnTop();
                }

                break;
            case TamerDisplayKind.Close:
                var wasOpen = TamerStableWindow?.Visible ?? false;

                WorldState.TamerStable.ClearCatalogJustArrived();
                TamerStableWindow?.CloseFromServer();

                if (wasOpen && !string.IsNullOrEmpty(args.CloseReason))
                    WorldState.Chat.AddOrangeBarMessage(args.CloseReason);

                break;
            case TamerDisplayKind.Ranked:
                //kept for the Ranked tab whether or not it is showing; a ranked display never opens a closed window
                WorldState.TamerRanked.Apply(args);

                if (TamerStableWindow?.Visible ?? false)
                    TamerStableWindow.Refresh();

                break;
        }
    }

    /// <summary>
    ///     Showing the stable window puts it on top of the control stack, but an open battle window draws above it.
    ///     Putting the battle window back on top makes the keys follow what is drawn: Escape closes the battle first.
    /// </summary>
    private void KeepTamerBattleOnTop()
    {
        if (TamerBattleWindow is { Visible: true } battle)
            InputDispatcher.Instance?.PushControl(battle);
    }

    private void OnTamerStableClosed() => WorldState.TamerStable.ClearCatalogJustArrived();

    private bool EnsureTamerBattleWindow()
    {
        if (TamerBattleWindow is not null)
            return true;

        //above the stable window, which has the same rectangle and the screen's ZIndex 2 (as the card windows use 2, 3, 4)
        var window = new TamerBattleControl(Game.CreatureRenderer) { ZIndex = 3 };

        window.Closed += OnTamerBattleClosed;

        Root!.AddChild(window);

        TamerBattleWindow = window;

        return true;
    }

    private void HandleTamerBattleDisplay(TamerBattleDisplayArgs args)
    {
        if (Root is null)
            return;

        var result = WorldState.TamerBattleAssembly.Accept(args);

        switch (result.Status)
        {
            case TamerBattleAcceptance.Ready:
                if (result.Fight is not null)
                {
                    WorldState.TamerBattle = new TamerBattlePlayback(result.Fight);

                    if (!EnsureTamerBattleWindow())
                        return;

                    TamerBattleWindow!.Open(WorldState.TamerBattle);

                    //a button pressed on the stable window before the battle covered it must not click on release
                    if (TamerStableWindow is not null)
                        InputDispatcher.Instance?.ReleaseCaptureWithin(TamerStableWindow);
                }

                break;
            case TamerBattleAcceptance.Broken:
                WorldState.Chat.AddOrangeBarMessage(TamerBattleText.BrokenMessage);
                break;
            case TamerBattleAcceptance.Waiting:
            default:
                break;
        }
    }

    private void OnTamerBattleClosed()
    {
        //closing the battle window tells the server nothing; the playback stays until a new fight replaces it
    }

    //every stable window request, from any tab, goes through here; under the battle window only Close goes
    private void SendTamerAction(TamerActionArgs args)
    {
        if (!TamerStableSendGuard.Allows(args, TamerBattleWindow?.Visible ?? false))
            return;

        if (args.Type == TamerActionType.Close)
            WorldState.TamerStable.ClearCatalogJustArrived();

        Game.Connection.SendTamerAction(args);
    }
}
