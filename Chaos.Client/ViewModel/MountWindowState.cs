using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>The mount window's selection, page, cloak color and facing. No graphics, so it is testable.</summary>
public sealed class MountWindowState
{
    public const int COLUMNS = 4;
    public const int ROWS = 3;
    public const int PAGE_SIZE = COLUMNS * ROWS;
    public const int FACING_COUNT = 4;

    private int SelectedIndex = -1;

    public IReadOnlyList<MountWindowMount> Mounts { get; private set; } = [];
    public IReadOnlyList<DisplayColor> Colors { get; private set; } = [DisplayColor.Default];
    public DisplayColor Color { get; private set; }
    public int Page { get; private set; }
    public int PageCount => Math.Max(1, (Mounts.Count + PAGE_SIZE - 1) / PAGE_SIZE);

    /// <summary>Down=0, Right=1, Up=2, Left=3, the order of GuildCloakPreview's facing table.</summary>
    public int Facing { get; private set; }

    public MountWindowMount? Selected => (SelectedIndex >= 0) && (SelectedIndex < Mounts.Count) ? Mounts[SelectedIndex] : null;

    public IReadOnlyList<MountWindowMount> PageItems => Mounts.Skip(Page * PAGE_SIZE).Take(PAGE_SIZE).ToList();

    public void Open(MountWindowDisplayArgs args)
    {
        Mounts = args.Mounts;
        Colors = args.Colors.Count > 0 ? args.Colors : [DisplayColor.Default];

        var current = Mounts.ToList().FindIndex(m => m.Mount == args.CurrentMount);
        SelectedIndex = Mounts.Count == 0 ? -1 : Math.Max(current, 0);
        Page = SelectedIndex < 0 ? 0 : SelectedIndex / PAGE_SIZE;
        Color = Colors.Contains(args.CurrentColor) ? args.CurrentColor : DisplayColor.Default;
        Facing = 0;
    }

    public void Select(MountWindowMount mount)
    {
        var index = Mounts.ToList().IndexOf(mount);

        if (index >= 0)
            SelectedIndex = index;
    }

    public void SetColor(DisplayColor color)
    {
        if (Colors.Contains(color))
            Color = color;
    }

    public void StepPage(int delta) => Page = Math.Clamp(Page + delta, 0, PageCount - 1);

    //Down -> Left -> Up -> Right
    public void Turn() => Facing = (Facing + FACING_COUNT - 1) % FACING_COUNT;
}
