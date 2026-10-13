namespace Chaos.Client.ViewModel.College;

/// <summary>The Teacher's Hangman turn order: who is ticked and in what order. Everyone starts ticked.</summary>
public sealed class TurnOrderList
{
    private readonly List<(string Name, bool Ticked)> Items;

    public TurnOrderList(IEnumerable<string> names) => Items = names.Select(n => (n, true)).ToList();

    public IReadOnlyList<(string Name, bool Ticked)> Rows => Items;
    public int Selected { get; private set; } = -1;
    public int TickedCount => Items.Count(i => i.Ticked);
    public IReadOnlyList<string> TickedNames => Items.Where(i => i.Ticked).Select(i => i.Name).ToList();

    /// <summary>Keeps the order and ticks of names still offered, drops the rest and appends new names ticked at the end.</summary>
    public void Reconcile(IReadOnlyList<string> names)
    {
        var selected = Selected >= 0 ? Items[Selected].Name : null;
        var offered = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        Items.RemoveAll(i => !offered.Contains(i.Name));

        foreach (var name in names)
            if (!Items.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                Items.Add((name, true));

        Selected = selected is null ? -1 : Items.FindIndex(i => i.Name.Equals(selected, StringComparison.OrdinalIgnoreCase));
    }

    public void Select(int index) => Selected = (index >= 0) && (index < Items.Count) ? index : -1;

    public void Toggle(int index)
    {
        if ((index < 0) || (index >= Items.Count))
            return;

        Items[index] = (Items[index].Name, !Items[index].Ticked);
    }

    public void TickAll()
    {
        for (var i = 0; i < Items.Count; i++)
            Items[i] = (Items[i].Name, true);
    }

    public void Shuffle(Random random)
    {
        var selected = Selected >= 0 ? Items[Selected].Name : null;

        for (var i = Items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (Items[i], Items[j]) = (Items[j], Items[i]);
        }

        Selected = selected is null ? -1 : Items.FindIndex(i => i.Name == selected);
    }

    public bool MoveUp() => (Selected > 0) && Move(Selected, Selected - 1);

    public bool MoveDown() => (Selected >= 0) && (Selected < Items.Count - 1) && Move(Selected, Selected + 1);

    /// <summary>Moves a row to another place and selects it there.</summary>
    public bool Move(int from, int to)
    {
        if ((from < 0) || (from >= Items.Count) || (to < 0) || (to >= Items.Count) || (from == to))
            return false;

        var item = Items[from];
        Items.RemoveAt(from);
        Items.Insert(to, item);
        Selected = to;

        return true;
    }
}
