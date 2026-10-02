# Spotlight Chairs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A director or admin can start Spotlight Chairs on the haunted theatre stage, and the last person standing on a spotlight center wins.

**Architecture:** `SpotlightChairs` holds the sweep math, the chair rules, and the messages. `SpotlightChairsGame` counts the seconds and says who is still in. `SuomiTheatreMapScript` applies that to the live lights, walks people off, and pays the winner. Thulin's Theatre Options only shows the menu while the Halloween window is open on `suomi_theatre_halloween`.

**Tech Stack:** C# / TUnit / FluentAssertions / Moq on Chaos-Server. Unora JSON for the dialogs and the emblem, plus one PNG.

**Spec:** `docs/superpowers/specs/2026-10-01-spotlight-chairs-design.md`

## Global Constraints

- Stage rectangle is `(0, 12, 9, 9)`: tiles x 0–8, y 12–20. At most 8 lights, so a game starts with 2 to 9 people on the stage.
- Sweep speed is 3. Periods for speeds 1–5 are `16000, 11000, 8000, 5500, 4000` ms. Phase is `(elapsed % (long)period) / period`. `there = phase < 0.5 ? phase * 2 : 2 - phase * 2`. `eased = there * there * (3 - 2 * there)`. Position lerps from the near end to the far end.
- The client's fixture, from `(0, 12)` to `(8, 12)` at speed 3: x is 0 at 0 ms, about 4 at 2000 ms, 8 at 4000 ms, 0 at 8000 ms, and still under 1 at 500 ms. At 6000 ms the same sweep is exactly tile `(4, 12)`.
- A point halfway between two tiles belongs to the tile closer to the sweep's far end.
- Light `i` sweeps row `y = 12 + i`, x 0→8 when `i` is even and x 8→0 when `i` is odd. White, medium, brightness 80, beam on, no effect.
- Moving phase is a whole number of seconds from 6 through 10. Pause on the frozen chairs is 4 seconds. The map tick is 1 second.
- First person onto a chair tile stays (earliest visit, then lower aisling id). An empty chair saves nobody. Everyone else in the set is walked off.
- Messages, exact: "Only the director can start Spotlight Chairs." / "Spotlight Chairs is only played during Halloween." / "Spotlight Chairs is already going." / "Spotlight Chairs needs at least two people on the stage." / "Spotlight Chairs can only seat nine people. Move someone off the stage." / "The lights are moving." / "The lights have stopped." / "You missed the light." / "The spotlight game is using the lights." / "The director called off Spotlight Chairs." / "{Name} won Spotlight Chairs." / "You already found candy today."
- Candy is `halloweencandy`, count 10, timed event `spotlightchairsCandy` for 24 hours. Participation mark key `spotlightchairs`, text "Took the stage for Spotlight Chairs". Winner mark key `spotlightchairsWin`, text "Won Spotlight Chairs". Both `MarkIcon.Yay`, `MarkColor.White`, `AddOrAccumulate`.
- Emblem key `spotlightchairs`, name `Spotlight`, art `257`, source legend mark `spotlightchairs`.
- Production Halloween check is `EventPeriod.IsSpecificEventActive(DateTime.UtcNow, map.LoadedFromInstanceId, EventType.Halloween)`. Tests pass a bool into the start and tick methods. `EventPeriod` reads the real clock, so a test must not depend on today's date.
- Do not commit unrelated dirty files (god-mode theatre edits, haunted-theatre wall edits, maze flags, guild cloak, fishing).
- No client code change. Do not run `Tools/Emblems/build.py --apply`.

**User decisions (already made):** Spotlights are the chairs, and the chair is the tile at the center of the beam when the light stops. Each round uses one fewer light than the people still in. An empty beam saves nobody, and the first person onto a shared center keeps it. Players on a center stay on the stage; everyone else is walked off. A director or an admin starts it after putting people on the stage. The winner gets the announcement, 10 Halloween candy once a day, and a winner's legend mark. Everyone who started gets a participation legend mark, which unlocks the emblem. The round, the rewards, and the hosting rules in the spec are approved, including the Yes/No prompts and the emblem name Spotlight at art 257.

---

### Task 1: Sweep math and the round clock

**Goal:** A pure game object can lay out the lights, freeze them on chair tiles, and say who stays in, with no map and no items.

**Files:**
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/Theatre/SpotlightChairs.cs`
- Test: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/Theatre/SpotlightChairsTests.cs`

**Acceptance Criteria:**
- [ ] Speed-3 sweep from `(0, 12)` to `(8, 12)` matches the client fixture, and 6000 ms lands on tile `(4, 12)`
- [ ] A position of 4.5 toward far end 8 is tile 5, and toward far end 0 is tile 4
- [ ] Two people on one chair: the earlier visit stays; an empty chair saves nobody
- [ ] A 2-person game rolled as 6 seconds walks off the person who missed `(4, 12)`, then the remaining person wins after the 4 second pause
- [ ] Start is refused for a non-director, outside Halloween, while running, and for 1 or 10 people

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/SpotlightChairsTests/*"` (from `Chaos.Client/Chaos-Server`) → all tests in `SpotlightChairsTests` pass

**Steps:**

- [ ] **Step 1: Write the failing test**

Create `Tests/Chaos.Tests/Theatre/SpotlightChairsTests.cs`:

```csharp
#region
using Chaos.Geometry;
using Chaos.Services.Theatre;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Theatre;

public sealed class SpotlightChairsTests
{
    [Test]
    public void Sweep_matches_the_client_fixture_and_six_seconds_is_tile_4()
    {
        SpotlightChairs.Position(0, 12, 8, 12, 0).X.Should().BeApproximately(0f, 0.001f);
        SpotlightChairs.Position(0, 12, 8, 12, 2000).X.Should().BeApproximately(4f, 0.001f);
        SpotlightChairs.Position(0, 12, 8, 12, 4000).X.Should().BeApproximately(8f, 0.001f);
        SpotlightChairs.Position(0, 12, 8, 12, 8000).X.Should().BeApproximately(0f, 0.001f);
        SpotlightChairs.Position(0, 12, 8, 12, 500).X.Should().BeLessThan(1f);

        var tile = SpotlightChairs.ChairTile(0, 12, 8, 12, 6000);
        tile.X.Should().Be(4);
        tile.Y.Should().Be(12);
    }

    [Test]
    public void A_halfway_point_belongs_to_the_tile_closer_to_the_far_end()
    {
        SpotlightChairs.Axis(4.5f, 8f).Should().Be(5);
        SpotlightChairs.Axis(4.5f, 0f).Should().Be(4);
        SpotlightChairs.Axis(4f, 8f).Should().Be(4);
    }

    [Test]
    public void The_earlier_visit_keeps_a_shared_chair_and_an_empty_chair_saves_nobody()
    {
        var chairs = new Point[] { new(4, 12) };
        var people = new SpotlightVisit[]
        {
            new(2, new Point(4, 12), 0),
            new(9, new Point(4, 12), 50),
            new(3, new Point(1, 12), 0)
        };

        var saved = SpotlightChairs.SavedIds(chairs, people);

        saved.Should().Equal(2u);
    }

    [Test]
    public void Two_players_resolve_to_one_winner_after_the_pause()
    {
        var game = new SpotlightChairsGame();
        var stage = new SpotlightVisit[] { new(1, new Point(4, 12), 0), new(2, new Point(1, 12), 0) };

        game.TryStart(true, true, false, stage, () => 6).Should().Be(SpotlightStartRefusal.None);

        SpotlightOutcome? last = null;

        for (var second = 0; second < 6; second++)
            last = game.Tick(stage, halloweenStill: true);

        last!.WalkOff.Should().Equal(2u);
        last.HouseMessage.Should().Be("The lights have stopped.");
        last.WinnerId.Should().BeNull();

        var winnerOnly = stage.Take(1).ToArray();

        for (var second = 0; second < 3; second++)
        {
            last = game.Tick(winnerOnly, halloweenStill: true);
            last!.WinnerId.Should().BeNull();
            last.Ended.Should().BeFalse();
        }

        last = game.Tick(winnerOnly, halloweenStill: true);
        last!.WinnerId.Should().Be(1u);
        last.Ended.Should().BeTrue();
        last.RestoreSaved.Should().BeTrue();
    }

    [Test]
    [Arguments(false, true, false, 2, SpotlightStartRefusal.NotDirector)]
    [Arguments(true, false, false, 2, SpotlightStartRefusal.OutsideWindow)]
    [Arguments(true, true, true, 2, SpotlightStartRefusal.AlreadyRunning)]
    [Arguments(true, true, false, 1, SpotlightStartRefusal.TooFew)]
    [Arguments(true, true, false, 10, SpotlightStartRefusal.TooMany)]
    public void Start_is_refused(bool canDirect, bool halloween, bool running, int count, SpotlightStartRefusal expected)
    {
        var game = new SpotlightChairsGame();
        var people = Enumerable.Range(1, count)
                               .Select(id => new SpotlightVisit((uint)id, new Point(1, 12), 0))
                               .ToArray();

        game.TryStart(canDirect, halloween, running, people, () => 6).Should().Be(expected);
        SpotlightChairs.RefusalText(expected).Should().NotBeNullOrEmpty();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `Chaos.Client/Chaos-Server`:

`dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/SpotlightChairsTests/*"`

Expected: FAIL because `SpotlightChairs` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Chaos/Services/Theatre/SpotlightChairs.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Services.Theatre;

public enum SpotlightStartRefusal
{
    None,
    NotDirector,
    OutsideWindow,
    AlreadyRunning,
    TooFew,
    TooMany
}

public readonly record struct SpotlightVisit(uint Id, Point Tile, long EnteredAtMs);

public sealed record SpotlightOutcome(
    bool RestoreSaved,
    bool ApplyGameLights,
    IReadOnlyList<StageLightInfo> GameLights,
    IReadOnlyList<uint> WalkOff,
    uint? WinnerId,
    string? HouseMessage,
    bool Ended);

public static class SpotlightChairs
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = StageLighting.MAX_LIGHTS + 1;
    public const int MinSweepSeconds = 6;
    public const int MaxSweepSeconds = 10;
    public const int PauseSeconds = 4;
    public const byte MotionSpeed = 3;
    public const string CandyTemplateKey = "halloweencandy";
    public const int CandyCount = 10;
    public const string CandyEvent = "spotlightchairsCandy";
    public const string ParticipationKey = "spotlightchairs";
    public const string ParticipationText = "Took the stage for Spotlight Chairs";
    public const string WinnerKey = "spotlightchairsWin";
    public const string WinnerText = "Won Spotlight Chairs";
    public const string MissedText = "You missed the light.";
    public const string LightsBusyText = "The spotlight game is using the lights.";
    public const string CalledOffText = "The director called off Spotlight Chairs.";
    public const string CandyWaitText = "You already found candy today.";
    public const string MovingText = "The lights are moving.";
    public const string StoppedText = "The lights have stopped.";

    private static readonly float[] SweepMs = [16000, 11000, 8000, 5500, 4000];

    public static string RefusalText(SpotlightStartRefusal refusal) => refusal switch
    {
        SpotlightStartRefusal.NotDirector    => "Only the director can start Spotlight Chairs.",
        SpotlightStartRefusal.OutsideWindow  => "Spotlight Chairs is only played during Halloween.",
        SpotlightStartRefusal.AlreadyRunning => "Spotlight Chairs is already going.",
        SpotlightStartRefusal.TooFew         => "Spotlight Chairs needs at least two people on the stage.",
        SpotlightStartRefusal.TooMany        => "Spotlight Chairs can only seat nine people. Move someone off the stage.",
        _                                    => ""
    };

    public static string WinnerAnnouncement(string name) => $"{name} won Spotlight Chairs.";

    public static (float X, float Y) Position(float ax, float ay, float bx, float by, long elapsedMs, byte speed = MotionSpeed)
    {
        var period = SweepMs[Math.Clamp((int)speed, 1, 5) - 1];
        var phase = elapsedMs % (long)period / period;
        var there = phase < 0.5f ? phase * 2f : 2f - (phase * 2f);
        var eased = there * there * (3f - (2f * there));

        return (ax + ((bx - ax) * eased), ay + ((by - ay) * eased));
    }

    public static int Axis(float pos, float far)
    {
        var lower = (int)Math.Floor(pos);
        var frac = pos - lower;

        if (frac < 0.5f)
            return lower;

        if (frac > 0.5f)
            return lower + 1;

        var higher = lower + 1;

        return Math.Abs(far - higher) <= Math.Abs(far - lower) ? higher : lower;
    }

    public static Point ChairTile(float ax, float ay, float bx, float by, long elapsedMs)
    {
        var (x, y) = Position(ax, ay, bx, by, elapsedMs);

        return new Point(Axis(x, bx), Axis(y, by));
    }

    public static Point ChairTile(StageLightInfo light, long elapsedMs)
        => ChairTile(
            light.X / (float)StageLightInfo.UNITS_PER_TILE,
            light.Y / (float)StageLightInfo.UNITS_PER_TILE,
            light.X2 / (float)StageLightInfo.UNITS_PER_TILE,
            light.Y2 / (float)StageLightInfo.UNITS_PER_TILE,
            elapsedMs);

    public static IReadOnlyList<uint> SavedIds(IReadOnlyList<Point> chairs, IReadOnlyList<SpotlightVisit> people)
    {
        var saved = new List<uint>();

        foreach (var chair in chairs)
        {
            SpotlightVisit? best = null;

            foreach (var person in people)
            {
                if ((person.Tile.X != chair.X) || (person.Tile.Y != chair.Y))
                    continue;

                if (best is null
                    || (person.EnteredAtMs < best.Value.EnteredAtMs)
                    || ((person.EnteredAtMs == best.Value.EnteredAtMs) && (person.Id < best.Value.Id)))
                    best = person;
            }

            if (best is { } winner)
                saved.Add(winner.Id);
        }

        return saved;
    }

    public static IReadOnlyList<StageLightInfo> LayLights(int playerCount)
    {
        var lights = new List<StageLightInfo>();
        var chairs = playerCount - 1;

        for (var i = 0; i < chairs; i++)
        {
            var y = (ushort)((12 + i) * StageLightInfo.UNITS_PER_TILE);
            var left = (ushort)0;
            var right = (ushort)(8 * StageLightInfo.UNITS_PER_TILE);
            var even = (i % 2) == 0;

            lights.Add(
                new StageLightInfo
                {
                    X = even ? left : right,
                    Y = y,
                    X2 = even ? right : left,
                    Y2 = y,
                    R = 255,
                    G = 255,
                    B = 255,
                    Size = StageLightSize.Medium,
                    Brightness = 80,
                    Beam = true,
                    Effect = StageLightEffect.None,
                    Motion = StageLightMotion.Sweep,
                    MotionSpeed = MotionSpeed
                });
        }

        return lights;
    }
}

public sealed class SpotlightChairsGame
{
    private readonly Dictionary<uint, long> EnteredAt = [];
    private readonly HashSet<uint> Participants = [];
    private readonly Dictionary<uint, Point> Tiles = [];
    private Func<int> Roll = () => SpotlightChairs.MinSweepSeconds;
    private long ClockMs;
    private long SweepElapsedMs;
    private int SweepSeconds;
    private int SecondsLeft;
    private bool Sweeping;
    private bool Pausing;

    public bool Running { get; private set; }

    public SpotlightStartRefusal TryStart(
        bool canDirect,
        bool halloween,
        bool alreadyRunning,
        IReadOnlyList<SpotlightVisit> onStage,
        Func<int> rollSeconds)
    {
        if (!canDirect)
            return SpotlightStartRefusal.NotDirector;

        if (!halloween)
            return SpotlightStartRefusal.OutsideWindow;

        if (alreadyRunning || Running)
            return SpotlightStartRefusal.AlreadyRunning;

        if (onStage.Count < SpotlightChairs.MinPlayers)
            return SpotlightStartRefusal.TooFew;

        if (onStage.Count > SpotlightChairs.MaxPlayers)
            return SpotlightStartRefusal.TooMany;

        Running = true;
        Participants.Clear();
        EnteredAt.Clear();
        Tiles.Clear();
        ClockMs = 0;
        Roll = rollSeconds;

        foreach (var person in onStage)
            Note(person);

        BeginSweep(Roll());

        return SpotlightStartRefusal.None;
    }

    public SpotlightOutcome Tick(IReadOnlyList<SpotlightVisit> onStage, bool halloweenStill)
    {
        if (!Running)
            return Idle();

        if (!halloweenStill)
            return CallOff(announce: false);

        ClockMs += 1000;
        NoteAll(onStage);
        DropMissing(onStage);

        if (Participants.Count == 0)
            return Finish();

        //during a sweep, one person left wins on this tick. during the pause, that person waits out the frozen chairs.
        if (Sweeping && (Participants.Count == 1))
            return Finish();

        if (Sweeping)
        {
            SweepElapsedMs += 1000;

            if (SweepElapsedMs < (SweepSeconds * 1000L))
                return Idle();

            return Freeze(onStage);
        }

        SecondsLeft--;

        if (SecondsLeft > 0)
            return Idle();

        if (Participants.Count <= 1)
            return Finish();

        BeginSweep(Roll());

        return new SpotlightOutcome(false, true, SpotlightChairs.LayLights(Participants.Count), [], null, SpotlightChairs.MovingText, false);
    }

    public SpotlightOutcome CallOff(bool announce)
    {
        Running = false;
        Sweeping = false;
        Pausing = false;
        Participants.Clear();

        return new SpotlightOutcome(true, false, [], [], null, announce ? SpotlightChairs.CalledOffText : null, true);
    }

    public IReadOnlyList<StageLightInfo> OpeningLights => SpotlightChairs.LayLights(Participants.Count);

    private void BeginSweep(int seconds)
    {
        SweepSeconds = seconds;
        SweepElapsedMs = 0;
        Sweeping = true;
        Pausing = false;
        SecondsLeft = seconds;
    }

    private SpotlightOutcome Freeze(IReadOnlyList<SpotlightVisit> onStage)
    {
        Sweeping = false;
        Pausing = true;
        SecondsLeft = SpotlightChairs.PauseSeconds;

        var lights = SpotlightChairs.LayLights(Participants.Count);
        var chairs = lights.Select(light => SpotlightChairs.ChairTile(light, SweepElapsedMs)).ToArray();
        var standing = Participants
                       .Select(id => new SpotlightVisit(id, Tiles[id], EnteredAt[id]))
                       .ToArray();
        var saved = SpotlightChairs.SavedIds(chairs, standing).ToHashSet();
        var walkOff = Participants.Where(id => !saved.Contains(id)).ToArray();

        Participants.Clear();

        foreach (var id in saved)
            Participants.Add(id);

        var still = lights.Select(
                                (light, index) => light with
                                {
                                    Motion = StageLightMotion.Still,
                                    X = (ushort)(chairs[index].X * StageLightInfo.UNITS_PER_TILE),
                                    Y = (ushort)(chairs[index].Y * StageLightInfo.UNITS_PER_TILE)
                                })
                            .ToArray();

        return new SpotlightOutcome(false, true, still, walkOff, null, SpotlightChairs.StoppedText, false);
    }

    private SpotlightOutcome Finish()
    {
        uint? winner = Participants.Count == 1 ? Participants.First() : null;
        Running = false;
        Sweeping = false;
        Pausing = false;

        return new SpotlightOutcome(true, false, [], [], winner, null, true);
    }

    private void NoteAll(IReadOnlyList<SpotlightVisit> onStage)
    {
        foreach (var person in onStage)
            if (Participants.Contains(person.Id))
                Note(person);
    }

    private void Note(SpotlightVisit person)
    {
        Participants.Add(person.Id);

        if (!Tiles.TryGetValue(person.Id, out var tile) || (tile.X != person.Tile.X) || (tile.Y != person.Tile.Y))
        {
            Tiles[person.Id] = person.Tile;
            EnteredAt[person.Id] = ClockMs;
        }
    }

    private void DropMissing(IReadOnlyList<SpotlightVisit> onStage)
    {
        var present = onStage.Select(person => person.Id).ToHashSet();

        foreach (var id in Participants.Where(id => !present.Contains(id)).ToArray())
        {
            Participants.Remove(id);
            Tiles.Remove(id);
            EnteredAt.Remove(id);
        }
    }

    private static SpotlightOutcome Idle()
        => new(false, false, [], [], null, null, false);
}
```

`TryStart` stores the roller and calls it at the start of every sweep, including later rounds. After `BeginSweep`, the script reads `OpeningLights` and sends `SpotlightChairs.MovingText`. `OpeningLights` is valid immediately after `TryStart` returns `None`, while the starting set is still intact.

The 2-player test calls `Tick` six times. The sixth tick freezes (`SweepElapsedMs` becomes 6000) on tile `(4, 12)`. The next three ticks are the pause, still with only the winner on stage, and must not name a winner yet. The fourth pause tick ends the game and names that winner. A sweep that drops to one person wins on that same tick; the pause is only the four seconds after a freeze.

- [ ] **Step 4: Run the test to verify it passes**

Same command as step 2. Expected: all `SpotlightChairsTests` pass.

---

### Task 2: Run the game on the theatre map

**Goal:** The theatre map starts the game, swaps in the chair lights, walks the losers off, pays the winner, and puts the old lights back.

**Files:**
- Modify: `Chaos.Client/Chaos-Server/Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs`
- Modify: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/Theatre/SuomiTheatreMapScriptTests.cs` (constructor only)
- Test: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/Theatre/SpotlightChairsMapTests.cs`

**Acceptance Criteria:**
- [ ] A director starts a 2-person game, the miss is walked out of the set, and the person on `(4, 12)` wins after the pause
- [ ] The winner's legend count goes up, they receive 10 Halloween candy, and a second win inside the timed event skips the candy
- [ ] Both starters have the participation mark before the first freeze
- [ ] `HandleLightingEdit` during a game sends "The spotlight game is using the lights." and does not add a light
- [ ] Calling the game off, and a tick with Halloween no longer active, restores the scene saved at the start and names no winner
- [ ] Existing `SuomiTheatreMapScriptTests` still construct the script

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/(SpotlightChairsMapTests)|(SuomiTheatreMapScriptTests)/*"` (from `Chaos.Client/Chaos-Server`) → both classes pass

**Steps:**

- [ ] **Step 1: Write the failing map test**

Create `Tests/Chaos.Tests/Theatre/SpotlightChairsMapTests.cs`. Use `MockMapInstance.Create`, `MockAisling.Create`, and `MockItem.Create` the same way `SuomiTheatreMapScriptTests` and `AislingTests.GiveItemOrSendToBank_ShouldAddToInventory_WhenCanCarry` do. Set `UserStatSheet.SetMaxWeight(50)` on anyone who should receive candy.

```csharp
private static SuomiTheatreMapScript Create(MapInstance map)
    => new(
        map,
        new Mock<IEffectFactory>().Object,
        new Mock<IStorage<TheatreLightingScenes>>().Object,
        new Mock<ISimpleCache>().Object,
        new Mock<ILogger<SuomiTheatreMapScript>>().Object,
        ItemFactory());

private static IItemFactory ItemFactory()
{
    var factory = new Mock<IItemFactory>();

    factory.Setup(f => f.Create(SpotlightChairs.CandyTemplateKey))
           .Returns(() =>
           {
               var item = MockItem.Create("Halloween Candy");
               item.Count = SpotlightChairs.CandyCount;

               return item;
           });

    return factory.Object;
}
```

Place two aislings at `(4, 12)` and `(1, 12)` and set the starter's `Trackers.Enums` to `TheatreRoles.Director`. Call `script.TryStartSpotlight(director, halloweenActive: true, () => 6)`. Assert refusal is `None`, both legends contain key `spotlightchairs`, and `script.Lighting.Lights` has one sweep light.

Tick six times with `script.TickSpotlight(halloweenStill: true)`. The aisling at `(1, 12)` is out of `script.SpotlightParticipants`. Tick three more times and assert there is still no `spotlightchairsWin` mark. Tick a fourth time. The aisling at `(4, 12)` has legend key `spotlightchairsWin` and an inventory item named `Halloween Candy` with count 10. `script.Lighting.Lights` is empty again (the saved scene had no lights).

Start a second game with the same winner still holding timed event `spotlightchairsCandy` and assert `Inventory` does not gain a second stack: give the candy event before the second win, or run the whole game twice and count inventory slots named Halloween Candy. One stack of 10 is the expected count after two wins.

A separate test calls `HandleLightingEdit` with `StageLightingAction.AddLight` while the game is running and asserts the orange-bar setup on the client mock received a message containing "The spotlight game is using the lights." and `Lighting.Lights.Count` is unchanged.

A separate test calls `script.CallOffSpotlight(announce: true)` and asserts `Lighting.Lights` is back to the pre-game list. Another calls `TickSpotlight(halloweenStill: false)` during a game and asserts the same restore and a null winner path: no `spotlightchairsWin` mark.

Update `SuomiTheatreMapScriptTests.CreateScript` to pass `new Mock<IItemFactory>().Object` as the new last argument so the old tests compile.

- [ ] **Step 2: Run the test to verify it fails**

`dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/SpotlightChairsMapTests/*"`

Expected: FAIL because `TryStartSpotlight` does not exist.

- [ ] **Step 3: Wire the map script**

Add `IItemFactory ItemFactory` as the last constructor parameter and store it. Add `using Chaos.Services.Factories.Abstractions;` if it is not already imported.

Add fields:

```csharp
private SpotlightChairsGame? Spotlight;
private StageLightingScene? SpotlightSaved;

public IReadOnlyCollection<uint> SpotlightParticipants => Spotlight is null ? [] : Spotlight.ParticipantIds;
```

Expose the set from `SpotlightChairsGame`:

```csharp
public IReadOnlyCollection<uint> ParticipantIds => Participants.ToArray();
```

Add these methods:

```csharp
public SpotlightStartRefusal TryStartSpotlight(Aisling starter)
    => TryStartSpotlight(
        starter,
        EventPeriod.IsSpecificEventActive(DateTime.UtcNow, Subject.LoadedFromInstanceId, EventType.Halloween),
        () => Random.Shared.Next(SpotlightChairs.MinSweepSeconds, SpotlightChairs.MaxSweepSeconds + 1));

public SpotlightStartRefusal TryStartSpotlight(Aisling starter, bool halloweenActive, Func<int> rollSeconds)
{
    var onStage = OnStage();
    var game = Spotlight ?? new SpotlightChairsGame();
    var refusal = game.TryStart(CanDirect(starter), halloweenActive, Spotlight is { Running: true }, onStage, rollSeconds);

    if (refusal != SpotlightStartRefusal.None)
    {
        starter.SendOrangeBarMessage(SpotlightChairs.RefusalText(refusal));

        return refusal;
    }

    Spotlight = game;
    SpotlightSaved = Lighting.ToScene("spotlight-chairs", FollowPosition);

    foreach (var person in onStage)
        if (Subject.TryGetEntity<Aisling>(person.Id, out var aisling))
            GrantParticipation(aisling);

    ApplyGameLights(game.OpeningLights);
    Announce(SpotlightChairs.MovingText);

    return SpotlightStartRefusal.None;
}

public void TickSpotlight(bool halloweenStill)
{
    if (Spotlight is not { Running: true } game)
        return;

    ApplyOutcome(game.Tick(OnStage(), halloweenStill));
}

public void CallOffSpotlight(bool announce)
{
    if (Spotlight is not { Running: true } game)
        return;

    ApplyOutcome(game.CallOff(announce));
}

private void ApplyOutcome(SpotlightOutcome outcome)
{
    foreach (var id in outcome.WalkOff)
        WalkOff(id);

    if (outcome.ApplyGameLights)
        ApplyGameLights(outcome.GameLights);

    if (outcome.HouseMessage is { } text)
        Announce(text);

    if (outcome.WinnerId is uint winnerId)
        PayWinner(winnerId);

    if (outcome.RestoreSaved && (SpotlightSaved is { } saved))
    {
        Lighting.ApplyScene(saved);
        SyncDarknessAndBroadcast(FADE_SCENE);
        SpotlightSaved = null;
    }

    if (outcome.Ended)
        Spotlight = null;
}
```

`OnStage` returns `SpotlightVisit` for every aisling `StageRectangle.ContainsPoint` accepts, with `EnteredAtMs` 0. The game tracks the real visit time itself.

`ApplyGameLights` clears `Lighting` and `TryAddLight`s each light, then `Broadcast(FADE_EDIT)`.

`WalkOff` finds the aisling, sends `SpotlightChairs.MissedText`, and warps them with the same search as `SuomiTheatreScript.HandleLeaveStage`: `SpiralSearch(6)`, outside `StageRectangle`, `IsWalkable` for that aisling, and no `ReactorTile` on the point. If the search is empty, leave them on the tile. They are already out of the set.

`GrantParticipation` and `PayWinner`:

```csharp
private static void GrantParticipation(Aisling aisling)
    => aisling.Legend.AddOrAccumulate(
        new LegendMark(
            SpotlightChairs.ParticipationText,
            SpotlightChairs.ParticipationKey,
            MarkIcon.Yay,
            MarkColor.White,
            1,
            GameTime.Now));

private void PayWinner(uint id)
{
    if (!Subject.TryGetEntity<Aisling>(id, out var winner))
        return;

    Announce(SpotlightChairs.WinnerAnnouncement(winner.Name));
    winner.Legend.AddOrAccumulate(
        new LegendMark(
            SpotlightChairs.WinnerText,
            SpotlightChairs.WinnerKey,
            MarkIcon.Yay,
            MarkColor.White,
            1,
            GameTime.Now));

    if (winner.Trackers.TimedEvents.HasActiveEvent(SpotlightChairs.CandyEvent, out _))
    {
        winner.SendOrangeBarMessage(SpotlightChairs.CandyWaitText);

        return;
    }

    var candy = ItemFactory.Create(SpotlightChairs.CandyTemplateKey);
    candy.Count = SpotlightChairs.CandyCount;
    winner.GiveItemOrSendToBank(candy);
    winner.Trackers.TimedEvents.AddEvent(SpotlightChairs.CandyEvent, TimeSpan.FromHours(24), true);
}
```

`Announce` sends `SendActiveMessage` to every aisling on `Subject`.

In `Update`, inside the interval block, before `TryMoveToOpenTheatre`:

```csharp
if (Spotlight is { Running: true } && TryMoveToOpenTheatre())
{
    CallOffSpotlight(announce: false);

    return;
}
```

Keep the existing `if (TryMoveToOpenTheatre()) return;` for the case where no game is running. When a game is running, the combined branch above already returns. Do not tick the game on the same interval that moved everyone off.

When the interval elapses and nobody was moved, call `TickSpotlight` with the same Halloween bool `TryMoveToOpenTheatre()` used internally. Read that bool once:

```csharp
var halloween = EventPeriod.IsSpecificEventActive(DateTime.UtcNow, SuomiTheatre.HALLOWEEN_ID, EventType.Halloween);

if (TryMoveToOpenTheatre(halloween))
{
    if (Spotlight is { Running: true })
        CallOffSpotlight(announce: false);

    return;
}

TickSpotlight(halloween);
```

`TryMoveToOpenTheatre()` already computes that bool. Replace the parameterless call in `Update` with this so the game and the swap share one answer. Do not change `TryMoveToOpenTheatre(bool)` itself.

At the top of `HandleLightingEdit`, after the `CanDirect` check succeeds, if `Spotlight is { Running: true }`:

```csharp
source.SendOrangeBarMessage(SpotlightChairs.LightsBusyText);

return;
```

That refuses every lighting action, including save and delete, so the chair lights cannot be stored as a scene.

- [ ] **Step 4: Run the tests to verify they pass**

The verify command for this task. Expected: `SpotlightChairsMapTests` and `SuomiTheatreMapScriptTests` pass.

If `MockItem.Create` does not leave `Count` settable, set the count in the factory setup after checking `Item.Count`'s setter. If `GiveItemOrSendToBank` banks the item because weight is still 0, the test forgot `SetMaxWeight(50)`.

---

### Task 3: Thulin's menu and the emblem

**Goal:** A director on the haunted theatre during Halloween sees Start and Stop on Thulin's Theatre Options, and the participation mark has an emblem template.

**Files:**
- Modify: `Chaos.Client/Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs`
- Create: `Unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_spotlightstart.json`
- Create: `Unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_spotlightstop.json`
- Create: `Unora/Data/Configuration/Templates/Emblems/spotlightchairs.json`
- Create: `Unora/Tools/Emblems/art/custom/embl257.png`

**Acceptance Criteria:**
- [ ] `suomitheatre_options` shows "Start Spotlight Chairs" only when the map script reports the game is available and not running, and "Stop Spotlight Chairs" only while it is running
- [ ] Yes on the start dialog calls `TryStartSpotlight(source)` and Yes on the stop dialog calls `CallOffSpotlight(true)`
- [ ] No on either dialog closes without starting or stopping
- [ ] `spotlightchairs.json` uses art 257 and legend mark `spotlightchairs`
- [ ] `embl257.png` exists, is at most 11×11, and contains no pure-black pixel

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/SpotlightChairsTests/*"` (from `Chaos.Client/Chaos-Server`) still passes, and the two new dialog files are valid JSON with template keys `suomitheatre_spotlightstart` and `suomitheatre_spotlightstop`

**Steps:**

- [ ] **Step 1: Add the menu choice to the pure type and test it**

In `SpotlightChairs`:

```csharp
public static string? MenuOption(bool canDirect, bool halloween, bool running)
{
    if (!canDirect || !halloween)
        return null;

    return running ? "Stop Spotlight Chairs" : "Start Spotlight Chairs";
}
```

Add a test in `SpotlightChairsTests`:

```csharp
    [Test]
    public void Menu_option_follows_the_director_the_window_and_the_game()
    {
        SpotlightChairs.MenuOption(false, true, false).Should().BeNull();
        SpotlightChairs.MenuOption(true, false, false).Should().BeNull();
        SpotlightChairs.MenuOption(true, true, false).Should().Be("Start Spotlight Chairs");
        SpotlightChairs.MenuOption(true, true, true).Should().Be("Stop Spotlight Chairs");
    }
```

Run the Task 1 filter. Expected: pass.

- [ ] **Step 2: Inject the option and handle Yes**

On `SuomiTheatreMapScript` add:

```csharp
public bool SpotlightAvailable
    => EventPeriod.IsSpecificEventActive(DateTime.UtcNow, Subject.LoadedFromInstanceId, EventType.Halloween);

public bool SpotlightRunning => Spotlight is { Running: true };
```

In `SuomiTheatreScript.OnDisplaying`, inside `case "suomitheatre_options":`, after the script is known to be a `SuomiTheatreMapScript`:

```csharp
var spotlight = SpotlightChairs.MenuOption(
    SuomiTheatreMapScript.CanDirect(source),
    theatreScript.SpotlightAvailable,
    theatreScript.SpotlightRunning);

if (spotlight is not null)
{
    var key = theatreScript.SpotlightRunning ? "suomitheatre_spotlightstop" : "suomitheatre_spotlightstart";

    if (!Subject.HasOption(spotlight))
        Subject.AddOption(spotlight, key);
}
```

The options case must resolve `theatreScript` the same way `thulin_initial` does (`source.MapInstance.Script.Is<SuomiTheatreMapScript>`). If that case does not have it yet, add that lookup and return when it fails.

In `OnNext`:

```csharp
case "suomitheatre_spotlightstart":
    if ((optionIndex == 1) && source.MapInstance.Script.Is<SuomiTheatreMapScript>(out var startScript))
        startScript.TryStartSpotlight(source);

    break;

case "suomitheatre_spotlightstop":
    if ((optionIndex == 1) && source.MapInstance.Script.Is<SuomiTheatreMapScript>(out var stopScript))
        stopScript.CallOffSpotlight(announce: true);

    break;
```

Option index 1 is Yes, matching `HandleLightsOn`. Index 2 is No and does nothing.

- [ ] **Step 3: Add the dialog templates, the emblem, and the image**

`suomitheatre_spotlightstart.json`, copied from `suomitheatre_turnlightson.json` with text "Start Spotlight Chairs?" and template key `suomitheatre_spotlightstart`. Yes and No both use dialog key `Close`. Script key `suomitheatre`.

`suomitheatre_spotlightstop.json` is the same with text "Call off Spotlight Chairs?" and template key `suomitheatre_spotlightstop`.

`Unora/Data/Configuration/Templates/Emblems/spotlightchairs.json`:

```json
{
  "key": "spotlightchairs",
  "art": 257,
  "name": "Spotlight",
  "description": "Took the stage for Spotlight Chairs.",
  "source": { "legendMark": "spotlightchairs" }
}
```

From `Unora`, write the image with the emblem tool's Pillow install:

```python
from PIL import Image
img = Image.new("RGBA", (11, 11), (0, 0, 0, 0))
for x in range(3, 8):
    for y in range(3, 8):
        img.putpixel((x, y), (255, 196, 40, 255))
img.save(r"Tools/Emblems/art/custom/embl257.png")
```

Do not run `build.py --apply`. The emblem cell stays empty until a setoa.dat launcher patch ships. The mark still grants.

- [ ] **Step 4: Re-run the pure tests**

Task 1's verify command. Expected: pass, including the new menu test.

---

### Task 4: Commit the full implementation

**Goal:** The Spotlight Chairs changes are one commit in each repo that they touch, and unrelated dirty files stay unstaged.

**Files:**
- Modify: the files created or edited in Tasks 1–3, and the spec and this plan under `Chaos.Client/docs/superpowers/`

**Acceptance Criteria:**
- [ ] Chaos-Server `HEAD` commit contains only the Spotlight Chairs server and test files
- [ ] Unora `HEAD` commit contains only the two dialog templates, `spotlightchairs.json`, and `embl257.png`
- [ ] Chaos.Client `HEAD` commit contains the spec, this plan, and the tasks file, added with `git add -f` because `docs/` is gitignored
- [ ] `git status` in each repo shows the unrelated pre-existing dirty files still unstaged

**Verify:** `git log -1 --name-only` in `Chaos.Client/Chaos-Server`, `Unora`, and `Chaos.Client` → each commit lists only its Spotlight Chairs paths

**Steps:**

- [ ] **Step 1: Review the three diffs**

In each repo, `git status` and `git diff`. Stage nothing that is not listed below.

- [ ] **Step 2: Commit Chaos-Server**

From `Chaos.Client/Chaos-Server`:

```powershell
git add Chaos/Services/Theatre/SpotlightChairs.cs Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs Tests/Chaos.Tests/Theatre/SpotlightChairsTests.cs Tests/Chaos.Tests/Theatre/SpotlightChairsMapTests.cs
git commit -m "Add Spotlight Chairs, a director-started stage game for the Halloween theatre."
```

- [ ] **Step 3: Commit Unora**

From `Unora`:

```powershell
git add Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_spotlightstart.json Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_spotlightstop.json Data/Configuration/Templates/Emblems/spotlightchairs.json Tools/Emblems/art/custom/embl257.png
git commit -m "Add the Spotlight Chairs dialogs and emblem."
```

- [ ] **Step 4: Commit the spec and plan**

From `Chaos.Client`:

```powershell
git add -f docs/superpowers/specs/2026-10-01-spotlight-chairs-design.md docs/superpowers/plans/2026-10-01-spotlight-chairs.md docs/superpowers/plans/2026-10-01-spotlight-chairs.md.tasks.json
git commit -m "Record the Spotlight Chairs design and plan."
```

- [ ] **Step 5: Confirm the commits**

`git status` and `git log -1 --name-only` in all three repos. Unrelated dirty files remain unstaged. Do not push.
