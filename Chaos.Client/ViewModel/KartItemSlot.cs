#region
using Chaos.DarkAges.Karting;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>The visible roulette kind and its caption, without access to the hidden roll result.</summary>
public static class KartItemSlot
{
    public const int CYCLE_FRAMES = 3;

    public static KartItemKind ShownKind(KartItemKind held, byte rollTicks, long frame)
        => rollTicks > 0 ? KartItems.All[(int)(frame / CYCLE_FRAMES % KartItems.All.Count)] : held;

    public static string Text(KartItemKind held, byte rollTicks, long frame)
    {
        var shown = ShownKind(held, rollTicks, frame);

        return shown == KartItemKind.None ? "-" : KartItems.Name(shown);
    }

    public static string WispText(int wisps) => $"Wisps {Math.Clamp(wisps, 0, 10)}/10";
}
