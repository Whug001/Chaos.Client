#region
using System.Diagnostics.CodeAnalysis;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Whether a request from the stable window (any tab, its prompts and its questions) may be sent. While the battle
///     window is up it covers the stable window, so nothing under it can have been meant: a click captured before the
///     battle opened, or a Yes held down, is dropped. Close always goes, so the server never keeps a stable session
///     open for a window the client has hidden. Pure: no textures.
/// </summary>
public static class TamerStableSendGuard
{
    public static bool Allows([NotNullWhen(true)] TamerActionArgs? action, bool battleVisible)
    {
        if (action is null)
            return false;

        return (action.Type == TamerActionType.Close) || !battleVisible;
    }
}
