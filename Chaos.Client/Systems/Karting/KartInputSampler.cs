#region
using Chaos.Client.Definitions;
using Chaos.Client.Systems.KeyBinds;
using Chaos.DarkAges.Karting;
#endregion

namespace Chaos.Client.Systems.Karting;

public sealed class KartInputSampler
{
    private ushort NextSequence;

    public static bool IsChordHeld(Scancode key, KeyModifiers modifiers, Func<Scancode, bool> isKeyHeld, KeyModifiers currentModifiers)
        => isKeyHeld(key) && ((currentModifiers & modifiers) == modifiers);

    public KartInput Sample(uint tick, Func<GameAction, bool> isHeld, bool typing)
    {
        var flags = KartInputFlags.None;

        if (!typing)
        {
            if (isHeld(GameAction.KartAccelerate))
                flags |= KartInputFlags.Accelerate;
            if (isHeld(GameAction.KartBrake))
                flags |= KartInputFlags.Brake;
            if (isHeld(GameAction.KartSteerLeft))
                flags |= KartInputFlags.Left;
            if (isHeld(GameAction.KartSteerRight))
                flags |= KartInputFlags.Right;
            if (isHeld(GameAction.KartDrift))
                flags |= KartInputFlags.Drift;
            if (isHeld(GameAction.KartUseItem))
                flags |= KartInputFlags.Item;
        }

        return new KartInput(NextSequence++, tick, flags);
    }

    public KartInput Sample(uint tick, KeyBindingTable bindings, Func<Scancode, KeyModifiers, bool> isKeyHeld, bool typing)
        => Sample(tick, action =>
        {
            foreach (var chord in bindings.ChordsFor(action))
                if (KeyChord.IsModifierKey(chord.Key) && chord.Modifiers == KeyModifiers.None
                        ? isKeyHeld(chord.Key, KeyModifiers.None) || isKeyHeld(chord.Key, KeyModifiers.Shift)
                        : isKeyHeld(chord.Key, chord.Modifiers))
                    return true;

            return false;
        }, typing);

    public void Reset() => NextSequence = 0;
}
