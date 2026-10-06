using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Systems.College;

/// <summary>The debate side markers for the players in the room, by entity id. Each DebateMarks message replaces them all.</summary>
public sealed class DebateMarkTable
{
    private readonly Dictionary<uint, CollegeDebateMarkInfo> Marks = [];

    public IReadOnlyDictionary<uint, CollegeDebateMarkInfo> All => Marks;
    public int Count => Marks.Count;

    public void Set(IReadOnlyList<CollegeDebateMarkInfo> marks)
    {
        Marks.Clear();

        foreach (var mark in marks)
            if (mark.Side != DebateSide.None)
                Marks[mark.EntityId] = mark;
    }

    public void Clear() => Marks.Clear();
}
