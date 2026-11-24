#region
using Chaos.Client.Rendering.CustomEmotes;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Which of the ten Crane Arcade prize emotes the current character has won. Empty at login and on
///     disconnect; the server sends the owned bytes in an <c>EmoteUnlocks</c> packet at world entry and
///     again each time the character wins one. Nothing else is ever locked.
/// </summary>
public sealed class EmoteUnlockState
{
    /// <summary>Shown wherever a locked emote refuses to play. Identical to the server's refusal line.</summary>
    public const string LOCKED_MESSAGE = "Win this emote at the Crane Arcade.";

    private readonly HashSet<byte> Unlocked = [];

    /// <summary>Raised whenever the set changes, so an open wheel or catalog redraws at once.</summary>
    public event Action? Changed;

    /// <summary>Replaces the whole set with the listed bytes. Unknown bytes are kept but never lock anything.</summary>
    public void Replace(IEnumerable<byte> bodyAnimations)
    {
        Unlocked.Clear();

        foreach (var bodyAnimation in bodyAnimations)
            Unlocked.Add(bodyAnimation);

        Changed?.Invoke();
    }

    /// <summary>Empties the set, relocking every prize emote. Call on logout or character switch.</summary>
    public void Clear()
    {
        Unlocked.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    ///     True only for a registry emote with <see cref="ICustomEmote.RequiresUnlock" /> whose byte is not
    ///     in the set. Retail emotes, older drawn emotes and unknown bytes are never locked.
    /// </summary>
    public bool IsLocked(int bodyAnimation)
    {
        if (!CustomEmoteRegistry.TryGet(bodyAnimation, out var emote) || !emote.RequiresUnlock)
            return false;

        return !Unlocked.Contains((byte)bodyAnimation);
    }
}
