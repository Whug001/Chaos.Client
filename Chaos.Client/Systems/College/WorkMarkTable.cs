using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Systems.College;

/// <summary>The line over each player busy on a class hand-in, such as "Drawing", by entity id. Each WorkMarks message replaces them all.</summary>
public sealed class WorkMarkTable
{
    private readonly Dictionary<uint, string> Marks = [];

    public IReadOnlyDictionary<uint, string> All => Marks;
    public int Count => Marks.Count;

    public void Set(IReadOnlyList<CollegeWorkMarkInfo> marks)
    {
        Marks.Clear();

        foreach (var mark in marks)
            if (mark.Text.Length > 0)
                Marks[mark.EntityId] = mark.Text;
    }

    public void Clear() => Marks.Clear();
}
