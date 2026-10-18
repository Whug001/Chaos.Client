namespace Chaos.Client.Systems;

/// <summary>Entity ids of players on this map who are in a card match; the server sends the full list each time.</summary>
public sealed class TcgBattleMarkTable
{
    private readonly HashSet<uint> Ids = [];

    public IReadOnlyCollection<uint> All => Ids;

    public int Count => Ids.Count;

    public bool Contains(uint id) => Ids.Contains(id);

    public void Set(IReadOnlyList<uint> ids)
    {
        Ids.Clear();
        Ids.UnionWith(ids);
    }

    public void Clear() => Ids.Clear();
}
