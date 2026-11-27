using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>The Effects window's tabs, selection, pages, current picks and facing. No graphics, so it is testable.</summary>
public sealed class EffectWindowState
{
    public const int COLUMNS = 3;
    public const int ROWS = 2;
    public const int PAGE_SIZE = COLUMNS * ROWS;
    public const int FACING_COUNT = 4;
    public const string SOLD_BY = "Sold by Illustria in Abel.";

    /// <summary>The first tile of each tab. Always owned.</summary>
    public static readonly EffectWindowEntry NoneEntry = new() { Effect = 0, Name = "None", Owned = true };

    //indexed by EffectWindowKind: login, death, chat bubble (drawn by the bubble partial, unused here), death sound
    private readonly IReadOnlyList<EffectWindowEntry>[] Lists = [[NoneEntry], [NoneEntry], [NoneEntry], [NoneEntry]];
    private readonly int[] SelectedIndex = new int[4];
    private readonly int[] Pages = new int[4];

    public EffectWindowKind Tab { get; private set; }
    public byte CurrentLogin { get; private set; }
    public byte CurrentDeath { get; private set; }
    public byte CurrentDeathSound { get; private set; }

    /// <summary>Down=0, Right=1, Up=2, Left=3, the order of MountPreview's facing table.</summary>
    public int Facing { get; private set; }

    public IReadOnlyList<EffectWindowEntry> Entries => Lists[(int)Tab];
    public int Page => Pages[(int)Tab];
    public int PageCount => Math.Max(1, (Entries.Count + PAGE_SIZE - 1) / PAGE_SIZE);
    public IReadOnlyList<EffectWindowEntry> PageItems => Entries.Skip(Page * PAGE_SIZE).Take(PAGE_SIZE).ToList();
    public EffectWindowEntry? Selected => SelectedIndex[(int)Tab] < Entries.Count ? Entries[SelectedIndex[(int)Tab]] : null;
    public byte Current
        => Tab switch
        {
            EffectWindowKind.Death      => CurrentDeath,
            EffectWindowKind.DeathSound => CurrentDeathSound,
            _                           => CurrentLogin
        };

    /// <summary>On the death sound tab, the sound id of the selected tile (the default sound for its first tile).</summary>
    public int? SoundToPreview => (Tab == EffectWindowKind.DeathSound) && Selected is { Animation: > 0 } entry ? entry.Animation : null;
    public bool CanChoose => Selected is { Owned: true };

    public string StatusText
        => Selected is { Owned: false } ? SOLD_BY : $"Current: {Entries.FirstOrDefault(e => e.Effect == Current)?.Name ?? NoneEntry.Name}";

    public void Open(EffectWindowDisplayArgs args)
    {
        Lists[(int)EffectWindowKind.LoginLogout] = [NoneEntry, .. args.LoginEffects];
        Lists[(int)EffectWindowKind.Death] = [NoneEntry, .. args.DeathEffects];
        Lists[(int)EffectWindowKind.DeathSound] =
            [new EffectWindowEntry { Effect = 0, Name = "Default", Animation = args.DefaultDeathSound, Owned = true }, .. args.DeathSounds];
        CurrentLogin = args.CurrentLogin;
        CurrentDeath = args.CurrentDeath;
        CurrentDeathSound = args.CurrentDeathSound;
        SelectCurrent(EffectWindowKind.LoginLogout, CurrentLogin);
        SelectCurrent(EffectWindowKind.Death, CurrentDeath);
        SelectCurrent(EffectWindowKind.DeathSound, CurrentDeathSound);
        Tab = EffectWindowKind.LoginLogout;
        Facing = 0;
    }

    public void Update(EffectWindowDisplayArgs args)
    {
        CurrentLogin = args.CurrentLogin;
        CurrentDeath = args.CurrentDeath;
        CurrentDeathSound = args.CurrentDeathSound;
    }

    public void SetTab(EffectWindowKind tab) => Tab = tab;

    public void Select(EffectWindowEntry entry)
    {
        var index = Entries.ToList().IndexOf(entry);

        if (index >= 0)
            SelectedIndex[(int)Tab] = index;
    }

    public bool IsCurrent(EffectWindowEntry entry) => entry.Effect == Current;

    public void StepPage(int delta) => Pages[(int)Tab] = Math.Clamp(Page + delta, 0, PageCount - 1);

    //Down -> Left -> Up -> Right
    public void Turn() => Facing = (Facing + FACING_COUNT - 1) % FACING_COUNT;

    private void SelectCurrent(EffectWindowKind tab, byte current)
    {
        var index = Math.Max(Lists[(int)tab].ToList().FindIndex(e => e.Effect == current), 0);
        SelectedIndex[(int)tab] = index;
        Pages[(int)tab] = index / PAGE_SIZE;
    }
}
