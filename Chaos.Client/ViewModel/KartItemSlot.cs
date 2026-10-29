#region
using Chaos.DarkAges.Karting;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>The visible roulette kind and its caption, without access to the hidden roll result.</summary>
public static class KartItemSlot
{
    public const int CYCLE_MILLISECONDS = 50;

    public static KartItemKind ShownKind(KartItemKind held, byte rollTicks, long elapsedMilliseconds)
        => rollTicks > 0
            ? KartItems.All[(int)(Math.Max(0, elapsedMilliseconds) / CYCLE_MILLISECONDS % KartItems.All.Count)]
            : held;

    public static string Text(KartItemKind held, byte rollTicks, long elapsedMilliseconds)
    {
        var shown = ShownKind(held, rollTicks, elapsedMilliseconds);

        return shown == KartItemKind.None ? "-" : KartItems.Name(shown);
    }

    public static string WispText(int wisps) => $"Wisps {Math.Clamp(wisps, 0, 10)}/10";
}
