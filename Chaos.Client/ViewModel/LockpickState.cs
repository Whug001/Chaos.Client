using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>
///     What the server told the lockpick window: the lock's difficulty, the title and the rogue's lockpick count.
///     The sweet spot and the pick's wear are never sent, so they are not here.
/// </summary>
public sealed class LockpickState
{
    public LockpickDifficulty Difficulty { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int LockpickCount { get; private set; }

    public void ApplyOpen(LockpickDisplayArgs args)
    {
        Difficulty = args.Difficulty;
        Title = args.Title ?? string.Empty;
        LockpickCount = args.LockpickCount;
    }

    public void ApplyTurnResult(LockpickDisplayArgs args) => LockpickCount = args.LockpickCount;

    public void Clear()
    {
        Difficulty = LockpickDifficulty.Easy;
        Title = string.Empty;
        LockpickCount = 0;
    }
}
