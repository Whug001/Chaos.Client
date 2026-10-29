#region
using Chaos.DarkAges.Karting;
#endregion

namespace Chaos.Client.Systems.Karting;

public static class KartHazardVisuals
{
    public static KartHazardPhase Phase(KartTimedHazard hazard, uint clockStep, uint? goStep)
        => goStep is { } go ? KartHazardSchedule.State(hazard, (long)clockStep - go) : KartHazardPhase.Dormant;
}
