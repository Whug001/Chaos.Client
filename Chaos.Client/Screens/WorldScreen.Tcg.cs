#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     The card collection windows (binder, deck builder, pack reveal): built on first use, fed by TcgCollectionDisplay.
///     Old game files without the card art get a message and a Close instead, as crafting does.
/// </summary>
public sealed partial class WorldScreen
{
    private const string TCG_ART_MISSING_TEXT = "Update your game files to use the card binder.";

    //built on first use
    private TcgBinderControl? TcgBinderWindow;
    private TcgDeckBuilderControl? TcgDeckBuilderWindow;
    private TcgPackRevealControl? TcgPackRevealWindow;

    private bool AnyTcgWindowVisible
        => (TcgBinderWindow?.Visible ?? false) || (TcgDeckBuilderWindow?.Visible ?? false) || (TcgPackRevealWindow?.Visible ?? false);

    private bool EnsureTcgWindows()
    {
        if ((TcgBinderWindow is not null) && (TcgDeckBuilderWindow is not null) && (TcgPackRevealWindow is not null))
            return true;

        if (!TcgCardFace.ArtPresent())
            return false;

        var binder = new TcgBinderControl(Game.CreatureRenderer)
        {
            ZIndex = 2
        };

        var builder = new TcgDeckBuilderControl(Game.CreatureRenderer)
        {
            ZIndex = 3
        };

        var reveal = new TcgPackRevealControl(Game.CreatureRenderer)
        {
            ZIndex = 4
        };

        binder.ActionRequested += SendTcgCollectionAction;
        binder.EditDeckRequested += OpenTcgDeckBuilder;
        binder.Closed += OnTcgBinderClosed;
        binder.FindMatchClicked += SendTcgRankedJoin;
        binder.RankedTabOpened += SendTcgRankedInfo;
        builder.ActionRequested += SendTcgCollectionAction;

        Root!.AddChild(binder);
        Root.AddChild(builder);
        Root.AddChild(reveal);

        TcgBinderWindow = binder;
        TcgDeckBuilderWindow = builder;
        TcgPackRevealWindow = reveal;

        return true;
    }

    private void HandleTcgCollectionDisplay(TcgCollectionDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (args.Kind == TcgCollectionDisplayKind.Close)
        {
            var wasOpen = AnyTcgWindowVisible;
            HideTcgWindows();

            if (wasOpen && (args.Message.Length > 0))
                WorldState.Chat.AddOrangeBarMessage(args.Message);

            return;
        }

        if (args.Kind == TcgCollectionDisplayKind.Snapshot)
        {
            WorldState.TcgCollection.Apply(args);

            //a snapshot after a change or a pack only updates a binder that is already open
            if (!args.OpenWindow && !(TcgBinderWindow?.Visible ?? false))
                return;
        }

        if (!EnsureTcgWindows())
        {
            WorldState.Chat.AddOrangeBarMessage(TCG_ART_MISSING_TEXT);
            SendTcgCollectionAction(TcgCollectionState.CloseAction());

            return;
        }

        if (args.Kind == TcgCollectionDisplayKind.PackOpened)
        {
            TcgPackRevealWindow!.Start(args);

            return;
        }

        if (TcgBinderWindow!.Visible)
            TcgBinderWindow.Refresh();
        else
            TcgBinderWindow.Open();

        TcgDeckBuilderWindow!.OnSnapshot();
    }

    private void HideTcgWindows()
    {
        TcgDeckBuilderWindow?.CloseFromServer();
        TcgBinderWindow?.CloseFromServer();
        TcgPackRevealWindow?.Hide();
    }

    /// <summary>
    ///     The card keyword under the cursor, for the hover box: the reveal first, then the binder. Skipped while the
    ///     deck builder covers the point or a question is asked over a card window. Null when the windows are not built.
    /// </summary>
    private string? TcgKeywordAt(int mouseX, int mouseY)
    {
        if ((TcgBinderWindow is null) || (TcgDeckBuilderWindow is null) || (TcgPackRevealWindow is null))
            return null;

        if (TcgBinderWindow.HasOpenQuestion || TcgDeckBuilderWindow.HasOpenQuestion)
            return null;

        if (TcgPackRevealWindow.HoveredKeyword(mouseX, mouseY) is { } revealed)
            return revealed;

        if (TcgDeckBuilderWindow.Visible && TcgDeckBuilderWindow.ScreenBounds.Contains(mouseX, mouseY))
            return TcgDeckBuilderWindow.HoveredKeyword(mouseX, mouseY);

        return TcgBinderWindow.HoveredKeyword(mouseX, mouseY);
    }

    private void OnTcgBinderClosed() => TcgDeckBuilderWindow?.CloseFromServer();

    private void OpenTcgDeckBuilder(TcgDeckDraft draft) => TcgDeckBuilderWindow?.Open(draft);

    private void SendTcgCollectionAction(TcgCollectionActionArgs args) => Game.Connection.SendTcgCollectionAction(args);

    private void SendTcgRankedJoin(string deckName) => Game.Connection.SendTcgAction(TcgRankedState.JoinAction(deckName));

    private void SendTcgRankedInfo() => Game.Connection.SendTcgAction(TcgRankedState.InfoAction());

    private void UnwireTcg()
    {
        Game.Connection.OnTcgCollectionDisplay -= HandleTcgCollectionDisplay;
        Game.Connection.OnTcgDisplay -= OnTcgDisplay;
        UnwireTcgRanked();

        if (TcgBinderWindow is not null)
        {
            TcgBinderWindow.ActionRequested -= SendTcgCollectionAction;
            TcgBinderWindow.EditDeckRequested -= OpenTcgDeckBuilder;
            TcgBinderWindow.Closed -= OnTcgBinderClosed;
            TcgBinderWindow.FindMatchClicked -= SendTcgRankedJoin;
            TcgBinderWindow.RankedTabOpened -= SendTcgRankedInfo;
        }

        if (TcgDeckBuilderWindow is not null)
            TcgDeckBuilderWindow.ActionRequested -= SendTcgCollectionAction;
    }

    private void WireTcg()
    {
        Game.Connection.OnTcgCollectionDisplay += HandleTcgCollectionDisplay;
        Game.Connection.OnTcgDisplay += OnTcgDisplay;
        WireTcgRanked();
    }
}
