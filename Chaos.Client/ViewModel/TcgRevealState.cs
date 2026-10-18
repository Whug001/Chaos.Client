#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>The glow a face-down card shows through its back.</summary>
public enum TcgRevealGlow : byte
{
    None,
    Rare,
    Legendary,
    Foil,

    /// <summary>The legendary glow with a rainbow edge.</summary>
    LegendaryFoil,

    Mythic,

    /// <summary>The Mythic glow with a rainbow edge.</summary>
    MythicFoil
}

/// <summary>
///     The pack reveal's timeline: a short tear, then the cards slide out one after another; a card flips when clicked
///     once it has arrived, and Reveal all flips the rest in quick order. Time only moves in <see cref="Update" />, so
///     tests can step it.
/// </summary>
public sealed class TcgRevealState
{
    public const double FLIP_MS = 160;
    public const double REVEAL_ALL_STEP_MS = 120;
    public const double SLIDE_MS = 300;
    public const double SLIDE_STAGGER_MS = 60;
    public const double TEAR_MS = 200;

    private readonly List<(int Index, double At)> Scheduled = [];
    private readonly Dictionary<string, TcgCatalogCardView> Views = new(StringComparer.OrdinalIgnoreCase);

    //when each card's flip started, NaN while face down
    private double[] FlipStarts = [];
    private List<TcgPackCardView> PackCards = [];

    public bool AllFaceUp => (PackCards.Count > 0) && Enumerable.Range(0, PackCards.Count).All(IsFaceUp);
    public IReadOnlyList<TcgPackCardView> Cards => PackCards;
    public double ElapsedMs { get; private set; }
    public bool IsActive => PackCards.Count > 0;
    public double TearProgress => Math.Clamp(ElapsedMs / TEAR_MS, 0, 1);

    public static double ArrivalMs(int index) => TEAR_MS + (index * SLIDE_STAGGER_MS) + SLIDE_MS;

    public void Clear()
    {
        Views.Clear();
        PackCards = [];
        FlipStarts = [];
        Scheduled.Clear();
        ElapsedMs = 0;
    }

    /// <summary>0 while face down, rising to 1 when the face shows.</summary>
    public double FlipProgress(int index)
        => double.IsNaN(FlipStarts[index]) ? 0 : Math.Clamp((ElapsedMs - FlipStarts[index]) / FLIP_MS, 0, 1);

    public TcgRevealGlow Glow(int index)
    {
        if (!double.IsNaN(FlipStarts[index]))
            return TcgRevealGlow.None;

        if (PackCards[index].Rarity == TcgRarity.Mythic)
            return PackCards[index].Foil ? TcgRevealGlow.MythicFoil : TcgRevealGlow.Mythic;

        var card = PackCards[index];

        if (card.Rarity == TcgRarity.Legendary)
            return card.Foil ? TcgRevealGlow.LegendaryFoil : TcgRevealGlow.Legendary;

        if (card.Foil)
            return TcgRevealGlow.Foil;

        return card.Rarity == TcgRarity.Rare ? TcgRevealGlow.Rare : TcgRevealGlow.None;
    }

    public bool HasArrived(int index) => SlideProgress(index) >= 1;

    public bool IsFaceUp(int index) => FlipProgress(index) >= 1;

    /// <summary>Flips every face-down card in order, one every <see cref="REVEAL_ALL_STEP_MS" />, none before it has arrived.</summary>
    public void RevealAll()
    {
        Scheduled.Clear();
        var step = 0;

        for (var i = 0; i < PackCards.Count; i++)
            if (double.IsNaN(FlipStarts[i]))
                Scheduled.Add((i, Math.Max(ArrivalMs(i), ElapsedMs) + (step++ * REVEAL_ALL_STEP_MS)));
    }

    public bool ShowsNew(int index) => IsFaceUp(index) && PackCards[index].NewToYou;

    /// <summary>0 at the window centre, 1 in the card's place.</summary>
    public double SlideProgress(int index) => Math.Clamp((ElapsedMs - TEAR_MS - (index * SLIDE_STAGGER_MS)) / SLIDE_MS, 0, 1);

    /// <summary>Starts a reveal, replacing any reveal still open.</summary>
    public void Start(TcgCollectionDisplayArgs args)
    {
        Clear();

        foreach (var view in args.Cards)
            Views[view.Key] = view;

        PackCards = args.Pack.ToList();
        FlipStarts = Enumerable.Repeat(double.NaN, PackCards.Count).ToArray();
    }

    public bool TryFlip(int index)
    {
        if ((index < 0) || (index >= PackCards.Count) || !double.IsNaN(FlipStarts[index]) || !HasArrived(index))
            return false;

        FlipStarts[index] = ElapsedMs;

        return true;
    }

    public void Update(double elapsedMs)
    {
        ElapsedMs += elapsedMs;

        for (var i = Scheduled.Count - 1; i >= 0; i--)
        {
            (var index, var at) = Scheduled[i];

            if (at > ElapsedMs)
                continue;

            //started at its scheduled time, not this frame, so the order holds even after a long frame
            if (double.IsNaN(FlipStarts[index]))
                FlipStarts[index] = at;

            Scheduled.RemoveAt(i);
        }
    }

    public TcgCatalogCardView? View(int index) => Views.GetValueOrDefault(PackCards[index].Key);
}
