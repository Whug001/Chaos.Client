namespace Chaos.Client.Systems.Karting;

/// <summary>Which kart a spectating player follows. Candidates come in place order, leader first.</summary>
public sealed class KartSpectator
{
    public byte? Target { get; private set; }

    public byte? Resolve(IReadOnlyList<byte> candidates, byte? own)
    {
        if (Target is { } target && candidates.Contains(target))
            return Target;

        if (own is { } ownIndex && candidates.Contains(ownIndex))
            Target = ownIndex;
        else
            Target = candidates.Count > 0 ? candidates[0] : null;

        return Target;
    }

    public byte? Cycle(IReadOnlyList<byte> candidates, byte? own, int direction)
    {
        if (Resolve(candidates, own) is not { } current)
            return null;

        var position = candidates.ToList().IndexOf(current);
        Target = candidates[((position + direction) % candidates.Count + candidates.Count) % candidates.Count];

        return Target;
    }

    public void Clear() => Target = null;
}
