#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Decides whether the client may send an emote. A locked Crane Arcade prize emote may be neither
///     sent nor played locally until it is won; every other emote is always free.
/// </summary>
public static class EmoteSendRules
{
    public static bool MaySend(EmoteUnlockState unlocks, BodyAnimation animation)
        => !unlocks.IsLocked((int)animation);
}
