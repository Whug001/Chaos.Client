# Mirror Maze Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Halloween mirror maze behind the Suomi theatre: a 58x58 map whose inside walls are client-drawn mirror panels in four styles, with three mirror tricks, three key pieces, a mirror-door heart and a daily candy prize.

**Architecture:** Unora holds the map, a `mirrors.json` beside it listing every mirror face, and the content. The server loads `mirrors.json`, validates it against the map's walls, sends it to each player on entry (`MirrorLayout` packet), and picks players for "your double steps out" (`MirrorDouble` packet). Mirror doors, wisps and the prize chest are ordinary reactor and merchant scripts backed by one testable rules class. The client keeps the layout in `WorldState.Mirrors`, draws each visible character once per frame into an atlas, composes all mirror glass into one render target before the world pass, and pastes each wall tile's slice of it, with a frame sprite, right after that tile's foreground.

**Tech Stack:** C# / .NET 10 (Chaos-Server; Chaos.Client with MonoGame), TUnit + FluentAssertions + Moq, Python 3 + Pillow (Unora tools), DALib (map rendering for review).

**Spec:** `docs/superpowers/specs/2026-09-30-mirror-maze-design.md` in Chaos.Client, branch `feat/mirror-maze` (commits 2e07d72 and 13ed17a). **Section 8, "Amendments made while planning", overrides the spec body** wherever they differ. Read it first.

## Global Constraints

- **Worktrees.** All on branch `feat/mirror-maze`:
  - Server: `C:\Users\Michael\Documents\GitHub\worktrees\mirror-maze-server` (SERVER below), created in Task 0 from server `master`.
  - Client: `C:\Users\Michael\Documents\GitHub\worktrees\mirror-maze-client` (CLIENT below). It already exists and holds the spec.
  - Unora: `C:\Users\Michael\Documents\GitHub\worktrees\mirror-maze-unora` (UNORA below), created in Task 0 from Unora `main`.
  - The server repo is the client's submodule: `C:\Users\Michael\Documents\GitHub\Chaos.Client\Chaos-Server`.
  - The local game data folder is `C:\Users\Michael\Documents\Unora\Unora Files` (`acclib.paths.DEFAULT_DATA_DIR`).
- **Client builds against SERVER.** Set `UnoraServerPath` for every client build and test run. Bash: prefix with `UnoraServerPath=/c/Users/Michael/Documents/GitHub/worktrees/mirror-maze-server`.
- **Shared checkouts.** Other Claude sessions use these repos at the same time.
  - Stage by explicit path only. Never `git add -A`, stash, reset or clean.
  - Never commit `launchSettings.json` or a local `StagingDirectory` in `appsettings.json`.
  - `docs/` is git-ignored in Chaos.Client; plans and specs need `git add -f`.
- **Commits.** At-end strategy. Implementers do **not** commit. Task 19 makes one commit per repo.
- **Building.**
  - Stop any running `Chaos.exe` or `Chaos.Client.exe` before building; they lock `bin`. Ask the user to stop them. Don't kill them.
  - Never build the server and client solutions at the same time.
- **Tests.**
  - Test projects are TUnit executables: `dotnet run --project <proj> -- --no-ansi [--treenode-filter "/*/*/Class/*"]`. Never `dotnet test`.
  - `Chaos.Tests` already fails two tests on master: GiveAbility and OnItemDroppedOn stackable. They are not regressions.
- **Code style.**
  - Read code with Serena (`get_symbols_overview`, `find_symbol`) and edit with its symbol tools where they fit. Worktree paths under `C:\Users\Michael\Documents\GitHub\worktrees\` work with Serena, relative to `C:\Users\Michael\Documents\GitHub`.
  - Match the surrounding code: `#region` using blocks, PascalCase private fields, `///` summaries on public types, `//lowercase` inline comments.
- **Opcodes** (next free on 2026-09-30; 141 and 142 are skipped on purpose because their history is unknown):
  - `ServerOpCode.MirrorLayout = 146`
  - `ServerOpCode.MirrorDouble = 147`
  - Before Task 1, confirm no branch took them: `git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server grep -n "= 146,\|= 147," $(git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server for-each-ref --format='%(refname)' refs/heads) -- Chaos.Networking.Abstractions/Definitions/Enums.cs` → no output.
- **`CLIENT_VERSION`** goes up by one from master's value (764 → 765 on 2026-09-30; the Halloween effects merge may already have taken 765). At commit time (Task 19) it must still be master's value + 1.
- **Wire formats** (all integers big-endian, as `SpanWriter` writes them):
  - `MirrorLayout`: `u16 segmentCount`, then per segment: `string8 id`, `u8 x`, `u8 y`, `u8 side` (0 North, 1 West), `u8 length`, `u8 style` (0 Glass, 1 Funhouse, 2 Haunted, 3 Endless, 4 Window), `u8 funhouse` (0 None, 1 Tall, 2 Wide, 3 Wave), `u16 partnerIndex` (`0xFFFF` = none); then `u8 stretchCount`, and per stretch `u8 x`, `u8 y`, `u8 width`, `u8 height`. An empty layout (0 segments, 0 stretches) means "this map has no mirrors".
  - `MirrorDouble`: `u32 entityId`, `u16 segmentIndex`.
- **Face geometry** (shared by the client, the art script and the generator). Tile-local pixels, origin at the tile's `Camera.TileToWorld` corner; a tile is 56x27.
  - North face = the wall tile's left half, columns `u` 0–27 at local x `u`. Its base row is `13 + u / 2` (integer division).
  - West face = the wall tile's right half, columns `v` 0–27 at local x `28 + v`. Its base row is `26 - v / 2`.
  - A panel fills rows `base - 75` to `base` (76 rows) in each column.
  - Glass fills columns 3–24 of a face, rows `base - 69` to `base - 6` (64 rows).
  - The frame sprite canvas for either face is 28 wide and 89 tall and is drawn at tile-local y `-62` (x `0` for north, `28` for west). Canvas row `r` = local row `r - 62`.
- **Mirror looks** (from the spec, section 4.6, with the amendments):
  - Reflected when standing within 4 tiles in front of a face run and within the run's span ±1 tile.
  - Glass: opacity 0.6, tint `rgb(190,205,255)`, glint period 9 s. Funhouse: tall 0.75x / 1.5x, wide 1.35x / 0.7x, wave 1x; ripple `sin(t*5 + row*0.18) * 3` px per 2-pixel row; tint pink `rgb(255,200,235)` (tall), green `rgb(200,255,225)` (wide), lavender `rgb(230,210,255)` (wave). Haunted: tint `rgb(185,225,215)`, slot length 17 s, slip start 2–8 s into the slot, slip lasts 3 s, kinds Lag (1.1 s behind), Stare, Ghost (opacity 0.2–0.5 at 1 Hz, tint `rgb(170,255,190)`). Endless: copy k = 0–3, `0.9k` tiles further back, scale `0.86^k`, opacity `0.6 × 0.62^k`, odd k faces the original way. Window: shows what stands in front of the partner, with reflected facing, opacity 0.7.
  - Sprite cap 40 per frame, nearest to the local player first.
  - Double: haunted segments only; server picks every 20–40 s one player within 3 tiles in front, at most once per 2 minutes each. Client: climb 0.6 s, follow 6 s at 0.45 s behind, fade 1 s; opacity up to 0.45; tint `rgb(200,230,255)`.
  - Dark stretch: 95% black over its tiles, a soft glow one tile across at each player's feet, and the mirrors inside drawn again at full brightness.
- **Maze rules:**
  - Timed events: `mirrormaze_key_funhouse`, `mirrormaze_key_endless`, `mirrormaze_key_haunted` (24 h each), `mirrormaze_daily_cd` (24 h). All `autoConsume: true`.
  - Prize: 20 `halloweencandy`. First time ever: legend mark key `mirrormaze`, text "Saw through the Mirror Maze", `MarkIcon.Victory`, `MarkColor.LightPurple`, count 1.
  - Thulin trade: 100 `halloweencandy` → 1 `macabrebox`, only while `EventPeriod.IsSpecificEventActive(DateTime.UtcNow, "suomi_mirror_maze", EventType.Halloween)` is true.
- **Map:** instance `suomi_mirror_maze`, template `10231`, **58x58** (19x19 cells of 2x2 floor plus 1-tile wall lines; the spec's "about 60x60"), file `lod10231.map`, built only by `Tools/MirrorMaze/generate_maze.py`. Floor `11056` / `11462` by `(x + y) % 2`. Inside walls: background `11507` (dark, so the layout shows in the map editor), foreground `1` in the slot(s) that face open floor (or `lfg = 1` if neither does). Outer north wall (row 0) `lfg` cycles `10903, 10904, 10905` by `x % 3`; outer west wall (column 0) `rfg` cycles `10888, 10887, 10886` by `y % 3`. Outer south and east walls: foreground `1`.
- **Messages (exact text):**
  - Mirror door prompt (dialog `mirrordoor_step`): "Your reflection beckons. Step through the glass?" with options "Step through" and "Stay".
  - Outside the window: "The glass is only cloudy tonight."
  - Heart door, missing pieces: "The glass will not let you pass. Missing: {list}." where `{list}` joins the missing wing names ("funhouse", "endless", "haunted") with ", ".
  - Heart door, already claimed today: "You have already seen through the maze today. Come back tomorrow."
  - Wisp, new piece: "The wisp presses a shard of the {wing} mirror into your hand." Wisp, already held: "You already hold a shard of the {wing} mirror."
  - Chest, success: "You find {n} pieces of candy in the chest!" plus, the first time: "Your reflection bows to you." Chest, on cooldown: "The chest is empty. Come back tomorrow." Chest, no pieces: "The chest will not open without all three shards."
  - Thulin option "Trade candy"; dialog `thulin_tradecandy`: "100 pieces of candy for a Macabre Box?" with "Trade" and "Not now". Not enough: "You need 100 pieces of candy." Done: "Thulin hands you a Macabre Box."

**User decisions (already made):**
- The haunted theatre replaces the normal theatre during the Halloween window (sub-project 2, not this plan).
- Build order: maze first, then the theatre, then the games.
- All four mirror styles; plan B layout (glass lobby, funhouse/endless/haunted wings, locked heart with three key pieces).
- Prize: daily candy, plus a legend mark and emblem the first time ever.
- Tricks: your double steps out, window mirrors, dark stretch. Not mirror doors as shortcuts.
- Reflections are drawn by the client from a server-sent mirror list (approach 1).
- Inside maze walls are client-drawn mirror panels; the outer back walls keep the mansion wallpaper ("Mirror panels").
- Art: existing game art plus a few new pieces, shipped in a launcher patch.

---

### Task 0: Create the worktrees

**Goal:** SERVER and UNORA worktrees exist on `feat/mirror-maze`, and the client builds against SERVER.

**Files:**
- Create: the two worktree folders (no source changes)

**Acceptance Criteria:**
- [ ] SERVER is at server `master` (f4802ac09 on 2026-09-30, or later) on `feat/mirror-maze`
- [ ] UNORA is at Unora `main` (301b78ed3 or later) on `feat/mirror-maze`
- [ ] CLIENT is on `feat/mirror-maze`, rebased onto current client `main`, with the two spec commits on top
- [ ] `dotnet build Chaos.Client.slnx` in CLIENT prints `0 Error(s)` with `UnoraServerPath` set to SERVER

**Verify:** `git -C <each worktree> log --oneline -1` shows those heads, and the client build prints `0 Error(s)`.

**Steps:**

- [ ] **Step 1: Create the worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/mirror-maze-server -b feat/mirror-maze master
git -C Unora -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/mirror-maze-unora -b feat/mirror-maze main
```

- [ ] **Step 2: Bring CLIENT up to date**

CLIENT was created from client `main` at 9e0de8e and holds only the spec commits. Client `main` has moved since (e9ad694, the Halloween effects merge, or later). Rebase the branch onto it:

```bash
git -C /c/Users/Michael/Documents/GitHub/worktrees/mirror-maze-client rebase main
```

Expected: `Successfully rebased`. The spec commits touch only `docs/`, so there are no conflicts.

- [ ] **Step 3: Check the heads**

Run: `for w in mirror-maze-server mirror-maze-client mirror-maze-unora; do git -C /c/Users/Michael/Documents/GitHub/worktrees/$w log --oneline -1; done`
Expected: the server master head, the rebased spec amendment commit ("Amend the mirror maze spec with planning findings"), and the Unora main head.

- [ ] **Step 4: Build the client against SERVER**

Ask the user to stop any running client or `Chaos.exe` first.

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/mirror-maze-client
UnoraServerPath=/c/Users/Michael/Documents/GitHub/worktrees/mirror-maze-server dotnet build Chaos.Client.slnx
```

Expected: `0 Error(s)`.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/mirror-maze-server log --oneline -1", "acceptanceCriteria": ["SERVER at server master on feat/mirror-maze", "UNORA at Unora main on feat/mirror-maze", "CLIENT on feat/mirror-maze with the spec commits", "client builds against SERVER with 0 errors"], "modelTier": "mechanical"}
```

---

### Task 1: Mirror packets

**Goal:** The `MirrorLayout` and `MirrorDouble` packets exist with their enums and opcodes, round-trip through their converters, and `CLIENT_VERSION` is one higher.

**Files:**
- Modify: `SERVER/Chaos.DarkAges/Definitions/Enums.cs` (three enums after `LockpickTurnOutcome`)
- Modify: `SERVER/Chaos.Networking.Abstractions/Definitions/Enums.cs` (`ServerOpCode.MirrorLayout = 146`, `ServerOpCode.MirrorDouble = 147`)
- Modify: `SERVER/Chaos.DarkAges/Definitions/CONSTANTS.cs` (`CLIENT_VERSION = 765`)
- Create: `SERVER/Chaos.Networking/Entities/Server/MirrorLayoutArgs.cs`
- Create: `SERVER/Chaos.Networking/Converters/Server/MirrorLayoutConverter.cs`
- Create: `SERVER/Chaos.Networking/Entities/Server/MirrorDoubleArgs.cs`
- Create: `SERVER/Chaos.Networking/Converters/Server/MirrorDoubleConverter.cs`
- Test: `SERVER/Tests/Chaos.Tests/Networking/MirrorPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] A layout with segments of every side, style and funhouse kind, partners and dark stretches round-trips
- [ ] An empty layout round-trips
- [ ] A double round-trips
- [ ] An unknown side, style or funhouse byte throws `ArgumentOutOfRangeException` on read
- [ ] More than 255 dark stretches throws `ArgumentOutOfRangeException` on write
- [ ] `CLIENT_VERSION` is one more than master's value when SERVER was created

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorPacketConverterTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Check the opcodes are still free**

Run the opcode check from Global Constraints. Expected: no output. If 146 or 147 is taken, stop and ask the coordinator.

- [ ] **Step 2: Add the enums**

In `SERVER/Chaos.DarkAges/Definitions/Enums.cs`, directly after the closing brace of `public enum LockpickTurnOutcome`, insert:

```csharp

/// <summary>Which face of a wall tile a mirror run covers.</summary>
public enum MirrorSide : byte
{
    /// <summary>The wall tile's left face (left foreground slot). It faces +y, and the run goes along +x.</summary>
    North = 0,

    /// <summary>The wall tile's right face (right foreground slot). It faces +x, and the run goes along +y.</summary>
    West = 1
}

/// <summary>How a mirror run draws its reflections.</summary>
public enum MirrorStyle : byte
{
    Glass = 0,
    Funhouse = 1,
    Haunted = 2,
    Endless = 3,

    /// <summary>Shows what stands in front of its partner run instead of a reflection.</summary>
    Window = 4
}

/// <summary>How a funhouse mirror bends its reflections. <see cref="None" /> for every other style.</summary>
public enum MirrorFunhouse : byte
{
    None = 0,
    Tall = 1,
    Wide = 2,
    Wave = 3
}
```

- [ ] **Step 3: Add the opcodes**

In `SERVER/Chaos.Networking.Abstractions/Definitions/Enums.cs`, after `TownBallot = 145,` in `ServerOpCode`, insert:

```csharp

    /// <summary>
    ///     Every mirror run and dark stretch on the player's current map. Sent on entry; empty for maps without mirrors.
    ///     <br />
    ///     Hex value: 0x92
    /// </summary>
    MirrorLayout = 146,

    /// <summary>
    ///     A player's reflection climbs out of a haunted mirror and follows them for a few seconds. Visual only.
    ///     <br />
    ///     Hex value: 0x93
    /// </summary>
    MirrorDouble = 147,
```

- [ ] **Step 4: Bump the client version**

In `SERVER/Chaos.DarkAges/Definitions/CONSTANTS.cs`, raise `CLIENT_VERSION` by one (764 → 765 on 2026-09-30). The Halloween effects branch was being merged into master on 2026-09-30; if master is already at 765, go to 766 and note it in the task close.

- [ ] **Step 5: Write the failing tests**

Create `SERVER/Tests/Chaos.Tests/Networking/MirrorPacketConverterTests.cs`:

```csharp
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;
using FluentAssertions;

namespace Chaos.Tests.Networking;

public class MirrorPacketConverterTests
{
    private static readonly Encoding Enc = Encoding.GetEncoding(949);

    private static T RoundTrip<T>(PacketConverterBase<T> converter, T original) where T: class, IPacketSerializable
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        converter.Serialize(ref writer, original);
        var bytes = writer.ToSpan()
                          .ToArray();

        var reader = new SpanReader(Enc, bytes);

        return converter.Deserialize(ref reader);
    }

    private static Exception? CatchRead<T>(PacketConverterBase<T> converter, byte[] bytes) where T: class, IPacketSerializable
    {
        try
        {
            var reader = new SpanReader(Enc, bytes);
            converter.Deserialize(ref reader);
        } catch (Exception ex)
        {
            return ex;
        }

        return null;
    }

    //one segment with the given side/style/funhouse bytes, no stretches
    private static byte[] OneSegmentBytes(byte side, byte style, byte funhouse)
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        writer.WriteUInt16(1);
        writer.WriteString8("a");
        writer.WriteByte(1);
        writer.WriteByte(2);
        writer.WriteByte(side);
        writer.WriteByte(3);
        writer.WriteByte(style);
        writer.WriteByte(funhouse);
        writer.WriteUInt16(MirrorSegmentInfo.NO_PARTNER);
        writer.WriteByte(0);

        return writer.ToSpan()
                     .ToArray();
    }

    [Test]
    public async Task Layout_round_trips_with_every_kind_of_segment_and_stretches()
    {
        var original = new MirrorLayoutArgs
        {
            Segments =
            [
                new MirrorSegmentInfo { Id = "lobby-n-1", X = 12, Y = 3, Side = MirrorSide.North, Length = 4, Style = MirrorStyle.Glass },
                new MirrorSegmentInfo
                {
                    Id = "fun-w-2",
                    X = 4,
                    Y = 20,
                    Side = MirrorSide.West,
                    Length = 3,
                    Style = MirrorStyle.Funhouse,
                    Funhouse = MirrorFunhouse.Tall
                },
                new MirrorSegmentInfo { Id = "h", X = 9, Y = 50, Side = MirrorSide.North, Length = 2, Style = MirrorStyle.Haunted },
                new MirrorSegmentInfo { Id = "e", X = 50, Y = 9, Side = MirrorSide.West, Length = 5, Style = MirrorStyle.Endless },
                new MirrorSegmentInfo
                {
                    Id = "win-a",
                    X = 30,
                    Y = 8,
                    Side = MirrorSide.North,
                    Length = 3,
                    Style = MirrorStyle.Window,
                    PartnerIndex = 5
                },
                new MirrorSegmentInfo
                {
                    Id = "win-b",
                    X = 30,
                    Y = 12,
                    Side = MirrorSide.North,
                    Length = 3,
                    Style = MirrorStyle.Window,
                    PartnerIndex = 4
                }
            ],
            DarkStretches =
            [
                new MirrorStretchInfo { X = 22, Y = 48, Width = 12, Height = 2 },
                new MirrorStretchInfo { X = 1, Y = 2, Width = 3, Height = 4 }
            ]
        };

        RoundTrip(new MirrorLayoutConverter(), original)
            .Should()
            .BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Empty_layout_round_trips()
    {
        var original = new MirrorLayoutArgs();

        var read = RoundTrip(new MirrorLayoutConverter(), original);

        read.Segments
            .Should()
            .BeEmpty();

        read.DarkStretches
            .Should()
            .BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Double_round_trips()
    {
        var original = new MirrorDoubleArgs { EntityId = 0xDEADBEEF, SegmentIndex = 513 };

        RoundTrip(new MirrorDoubleConverter(), original)
            .Should()
            .BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unknown_side_style_or_funhouse_throws_on_read()
    {
        CatchRead(new MirrorLayoutConverter(), OneSegmentBytes(2, 0, 0))
            .Should()
            .BeOfType<ArgumentOutOfRangeException>();

        CatchRead(new MirrorLayoutConverter(), OneSegmentBytes(0, 5, 0))
            .Should()
            .BeOfType<ArgumentOutOfRangeException>();

        CatchRead(new MirrorLayoutConverter(), OneSegmentBytes(0, 1, 4))
            .Should()
            .BeOfType<ArgumentOutOfRangeException>();

        CatchRead(new MirrorLayoutConverter(), OneSegmentBytes(1, 4, 0))
            .Should()
            .BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task More_than_255_stretches_throws_on_write()
    {
        var args = new MirrorLayoutArgs
        {
            DarkStretches = Enumerable.Range(0, 256)
                                      .Select(_ => new MirrorStretchInfo { Width = 1, Height = 1 })
                                      .ToList()
        };

        Exception? caught = null;

        try
        {
            var writer = new SpanWriter(Enc, usePooling: false);
            new MirrorLayoutConverter().Serialize(ref writer, args);
        } catch (Exception ex)
        {
            caught = ex;
        }

        caught.Should()
              .BeOfType<ArgumentOutOfRangeException>();

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 6: Run the tests to see them fail**

Run: from SERVER, `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorPacketConverterTests/*"`
Expected: build error, `MirrorLayoutArgs` and the converters don't exist.

- [ ] **Step 7: Write the args**

Create `SERVER/Chaos.Networking/Entities/Server/MirrorLayoutArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>One run of mirror faces along a wall.</summary>
public sealed record MirrorSegmentInfo
{
    /// <summary><see cref="PartnerIndex" /> when the run has no window partner.</summary>
    public const ushort NO_PARTNER = ushort.MaxValue;

    /// <summary>The run's id from mirrors.json. The haunted slip schedule hashes it.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The first wall tile's x.</summary>
    public byte X { get; set; }

    /// <summary>The first wall tile's y.</summary>
    public byte Y { get; set; }

    /// <summary>Which face of the wall tiles the run covers, and so which way it runs.</summary>
    public MirrorSide Side { get; set; }

    /// <summary>How many wall tiles the run covers.</summary>
    public byte Length { get; set; }

    public MirrorStyle Style { get; set; }

    /// <summary>How a funhouse run bends reflections. <see cref="MirrorFunhouse.None" /> for other styles.</summary>
    public MirrorFunhouse Funhouse { get; set; }

    /// <summary>For a window run, the index of its partner in the layout's segment list. Otherwise <see cref="NO_PARTNER" />.</summary>
    public ushort PartnerIndex { get; set; } = NO_PARTNER;
}

/// <summary>A rectangle of floor tiles drawn nearly black, where only the mirrors shine.</summary>
public sealed record MirrorStretchInfo
{
    public byte X { get; set; }
    public byte Y { get; set; }
    public byte Width { get; set; }
    public byte Height { get; set; }
}

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.MirrorLayout" /> packet: every mirror run and dark
///     stretch on the player's current map. Empty lists mean the map has no mirrors.
/// </summary>
public sealed record MirrorLayoutArgs : IPacketSerializable
{
    public IReadOnlyList<MirrorSegmentInfo> Segments { get; set; } = [];
    public IReadOnlyList<MirrorStretchInfo> DarkStretches { get; set; } = [];
}
```

Create `SERVER/Chaos.Networking/Entities/Server/MirrorDoubleArgs.cs`:

```csharp
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.MirrorDouble" /> packet: the reflection of
///     <see cref="EntityId" /> climbs out of mirror run <see cref="SegmentIndex" /> and follows them for a few seconds.
/// </summary>
public sealed record MirrorDoubleArgs : IPacketSerializable
{
    public uint EntityId { get; set; }

    /// <summary>Index into the last <see cref="MirrorLayoutArgs.Segments" /> the client was sent.</summary>
    public ushort SegmentIndex { get; set; }
}
```

- [ ] **Step 8: Write the converters**

Create `SERVER/Chaos.Networking/Converters/Server/MirrorLayoutConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="MirrorLayoutArgs" /></summary>
public sealed class MirrorLayoutConverter : PacketConverterBase<MirrorLayoutArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.MirrorLayout;

    /// <inheritdoc />
    public override MirrorLayoutArgs Deserialize(ref SpanReader reader)
    {
        var segmentCount = reader.ReadUInt16();
        var segments = new List<MirrorSegmentInfo>(segmentCount);

        for (var i = 0; i < segmentCount; i++)
            segments.Add(
                new MirrorSegmentInfo
                {
                    Id = reader.ReadString8(),
                    X = reader.ReadByte(),
                    Y = reader.ReadByte(),
                    Side = ReadEnum<MirrorSide>(reader.ReadByte(), 1, "mirror side"),
                    Length = reader.ReadByte(),
                    Style = ReadEnum<MirrorStyle>(reader.ReadByte(), 4, "mirror style"),
                    Funhouse = ReadEnum<MirrorFunhouse>(reader.ReadByte(), 3, "funhouse kind"),
                    PartnerIndex = reader.ReadUInt16()
                });

        var stretchCount = reader.ReadByte();
        var stretches = new List<MirrorStretchInfo>(stretchCount);

        for (var i = 0; i < stretchCount; i++)
            stretches.Add(
                new MirrorStretchInfo
                {
                    X = reader.ReadByte(),
                    Y = reader.ReadByte(),
                    Width = reader.ReadByte(),
                    Height = reader.ReadByte()
                });

        return new MirrorLayoutArgs
        {
            Segments = segments,
            DarkStretches = stretches
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, MirrorLayoutArgs args)
    {
        var segments = args.Segments ?? [];
        var stretches = args.DarkStretches ?? [];

        if (segments.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(args), $"A mirror layout holds at most {ushort.MaxValue} segments.");

        if (stretches.Count > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(args), $"A mirror layout holds at most {byte.MaxValue} dark stretches.");

        writer.WriteUInt16((ushort)segments.Count);

        foreach (var segment in segments)
        {
            writer.WriteString8(segment.Id ?? string.Empty);
            writer.WriteByte(segment.X);
            writer.WriteByte(segment.Y);
            writer.WriteByte((byte)segment.Side);
            writer.WriteByte(segment.Length);
            writer.WriteByte((byte)segment.Style);
            writer.WriteByte((byte)segment.Funhouse);
            writer.WriteUInt16(segment.PartnerIndex);
        }

        writer.WriteByte((byte)stretches.Count);

        foreach (var stretch in stretches)
        {
            writer.WriteByte(stretch.X);
            writer.WriteByte(stretch.Y);
            writer.WriteByte(stretch.Width);
            writer.WriteByte(stretch.Height);
        }
    }

    private static T ReadEnum<T>(byte value, byte max, string what) where T: struct, Enum
        => value > max
            ? throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown {what}.")
            : (T)Enum.ToObject(typeof(T), value);
}
```

Create `SERVER/Chaos.Networking/Converters/Server/MirrorDoubleConverter.cs`:

```csharp
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="MirrorDoubleArgs" /></summary>
public sealed class MirrorDoubleConverter : PacketConverterBase<MirrorDoubleArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.MirrorDouble;

    /// <inheritdoc />
    public override MirrorDoubleArgs Deserialize(ref SpanReader reader)
        => new()
        {
            EntityId = reader.ReadUInt32(),
            SegmentIndex = reader.ReadUInt16()
        };

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, MirrorDoubleArgs args)
    {
        writer.WriteUInt32(args.EntityId);
        writer.WriteUInt16(args.SegmentIndex);
    }
}
```

Converters are found by reflection (the same way `SetMapEffectsConverter` is). Check with `grep -rn "SetMapEffectsConverter" SERVER --include=*.cs`: if it is registered by name anywhere besides its own file, register the two new converters the same way.

- [ ] **Step 9: Run the tests to see them pass**

Run: the Verify command. Expected: 5 passed.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking/Entities/Server/MirrorLayoutArgs.cs", "Chaos.Networking/Converters/Server/MirrorLayoutConverter.cs", "Chaos.Networking/Entities/Server/MirrorDoubleArgs.cs", "Chaos.Networking/Converters/Server/MirrorDoubleConverter.cs", "Tests/Chaos.Tests/Networking/MirrorPacketConverterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MirrorPacketConverterTests/*\"", "acceptanceCriteria": ["full layout round-trips", "empty layout round-trips", "double round-trips", "unknown side/style/funhouse throws ArgumentOutOfRangeException on read", "more than 255 stretches throws on write", "CLIENT_VERSION one higher than master"], "modelTier": "mechanical"}
```

---

### Task 2: Mirror layout parsing and validation

**Goal:** `MirrorLayout.Parse` turns a `mirrors.json` string into validated segments and dark stretches, skipping and describing each bad entry, and `ToArgs` turns the result into a `MirrorLayoutArgs`.

**Files:**
- Create: `SERVER/Chaos/Services/MirrorMaze/MirrorLayout.cs`
- Test: `SERVER/Tests/Chaos.Tests/MirrorMaze/MirrorLayoutTests.cs`

**Acceptance Criteria:**
- [ ] A file with one segment of each style, a valid window pair and a stretch parses with no problems, and `ToArgs` gives the partners each other's index
- [ ] Each of these is skipped with one problem line naming the segment: unknown style, unknown side, funhouse without a kind, length 0, a wall tile off the map, a wall tile that isn't a wall, a front tile that is a wall, a duplicate id, a window without a partner, a window whose partner doesn't name it back, a window pair with different sides or lengths, a window pair more than 10 tiles apart
- [ ] A stretch outside the map, or 0 wide or high, is skipped with a problem line
- [ ] Invalid JSON gives an empty layout and one problem line
- [ ] `IsInFront` is true only within the given depth in front and within the span ± margin, for both sides

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorLayoutTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SERVER/Tests/Chaos.Tests/MirrorMaze/MirrorLayoutTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Networking.Entities.Server;
using Chaos.Services.MirrorMaze;
using FluentAssertions;

namespace Chaos.Tests.MirrorMaze;

public class MirrorLayoutTests
{
    private const int SIZE = 40;

    //walls: row 5 (x 0-39) and column 20 (y 10-39). everything else is floor.
    private static bool IsWall(int x, int y) => (y == 5) || ((x == 20) && (y >= 10));

    private static MirrorLayout Parse(string json, out List<string> problems)
        => MirrorLayout.Parse(json, SIZE, SIZE, IsWall, out problems);

    private static string Seg(string body) => $$"""{ "segments": [ {{body}} ] }""";

    [Test]
    public async Task Parses_every_style_a_window_pair_and_a_stretch()
    {
        const string JSON = """
        {
          "segments": [
            { "id": "g", "x": 0, "y": 5, "side": "north", "length": 3, "style": "glass" },
            { "id": "f", "x": 4, "y": 5, "side": "north", "length": 2, "style": "funhouse", "funhouse": "tall" },
            { "id": "h", "x": 20, "y": 10, "side": "west", "length": 4, "style": "haunted" },
            { "id": "e", "x": 7, "y": 5, "side": "North", "length": 1, "style": "ENDLESS" },
            { "id": "wa", "x": 10, "y": 5, "side": "north", "length": 2, "style": "window", "partner": "wb" },
            { "id": "wb", "x": 14, "y": 5, "side": "north", "length": 2, "style": "window", "partner": "wa" }
          ],
          "darkStretches": [ { "x": 21, "y": 30, "width": 5, "height": 2 } ]
        }
        """;

        var layout = Parse(JSON, out var problems);

        problems.Should()
                .BeEmpty();

        layout.Segments
              .Select(s => s.Style)
              .Should()
              .Equal(
                  MirrorStyle.Glass,
                  MirrorStyle.Funhouse,
                  MirrorStyle.Haunted,
                  MirrorStyle.Endless,
                  MirrorStyle.Window,
                  MirrorStyle.Window);

        layout.Segments[1]
              .Funhouse
              .Should()
              .Be(MirrorFunhouse.Tall);

        layout.Segments[2]
              .Side
              .Should()
              .Be(MirrorSide.West);

        layout.DarkStretches
              .Should()
              .ContainSingle()
              .Which
              .Should()
              .Be(new MirrorStretch(21, 30, 5, 2));

        var args = layout.ToArgs();

        args.Segments[4]
            .PartnerIndex
            .Should()
            .Be(5);

        args.Segments[5]
            .PartnerIndex
            .Should()
            .Be(4);

        args.Segments[0]
            .PartnerIndex
            .Should()
            .Be(MirrorSegmentInfo.NO_PARTNER);

        args.DarkStretches
            .Should()
            .ContainSingle();

        await Task.CompletedTask;
    }

    [Test]
    [Arguments("""{ "id": "a", "x": 0, "y": 5, "side": "north", "length": 1, "style": "shiny" }""", "style")]
    [Arguments("""{ "id": "a", "x": 0, "y": 5, "side": "up", "length": 1, "style": "glass" }""", "side")]
    [Arguments("""{ "id": "a", "x": 0, "y": 5, "side": "north", "length": 1, "style": "funhouse" }""", "funhouse")]
    [Arguments("""{ "id": "a", "x": 0, "y": 5, "side": "north", "length": 0, "style": "glass" }""", "length")]
    [Arguments("""{ "id": "a", "x": 38, "y": 5, "side": "north", "length": 4, "style": "glass" }""", "off the map")]
    [Arguments("""{ "id": "a", "x": 0, "y": 6, "side": "north", "length": 1, "style": "glass" }""", "not a wall")]
    [Arguments("""{ "id": "a", "x": 20, "y": 4, "side": "north", "length": 1, "style": "glass" }""", "not a wall")]
    [Arguments("""{ "id": "a", "x": 19, "y": 5, "side": "west", "length": 1, "style": "glass" }""", "blocked")]
    [Arguments("""{ "id": "a", "x": 0, "y": 5, "side": "north", "length": 1, "style": "window" }""", "partner")]
    [Arguments("""{ "id": "", "x": 0, "y": 5, "side": "north", "length": 1, "style": "glass" }""", "id")]
    public async Task Skips_a_bad_segment_with_one_problem(string segment, string reasonContains)
    {
        var layout = Parse(Seg(segment), out var problems);

        layout.Segments
              .Should()
              .BeEmpty();

        problems.Should()
                .ContainSingle()
                .Which
                .Should()
                .Contain("segment 0")
                .And
                .Contain(reasonContains);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Skips_the_second_of_two_segments_with_the_same_id()
    {
        var layout = Parse(
            """
            { "segments": [
              { "id": "a", "x": 0, "y": 5, "side": "north", "length": 1, "style": "glass" },
              { "id": "A", "x": 2, "y": 5, "side": "north", "length": 1, "style": "glass" } ] }
            """,
            out var problems);

        layout.Segments
              .Should()
              .ContainSingle()
              .Which
              .X
              .Should()
              .Be(0);

        problems.Should()
                .ContainSingle()
                .Which
                .Should()
                .Contain("segment 1")
                .And
                .Contain("duplicate");

        await Task.CompletedTask;
    }

    [Test]
    [Arguments("""{ "id": "wb", "x": 14, "y": 5, "side": "north", "length": 2, "style": "window", "partner": "zz" }""")]
    [Arguments("""{ "id": "wb", "x": 14, "y": 5, "side": "north", "length": 3, "style": "window", "partner": "wa" }""")]
    [Arguments("""{ "id": "wb", "x": 20, "y": 12, "side": "west", "length": 2, "style": "window", "partner": "wa" }""")]
    [Arguments("""{ "id": "wb", "x": 30, "y": 5, "side": "north", "length": 2, "style": "window", "partner": "wa" }""")]
    public async Task Skips_both_halves_of_a_broken_window_pair(string second)
    {
        var json = $$"""
                     { "segments": [
                       { "id": "wa", "x": 10, "y": 5, "side": "north", "length": 2, "style": "window", "partner": "wb" },
                       {{second}} ] }
                     """;

        var layout = Parse(json, out var problems);

        layout.Segments
              .Should()
              .BeEmpty();

        problems.Should()
                .HaveCount(2)
                .And
                .AllSatisfy(p => p.Should()
                                  .Contain("partner"));

        await Task.CompletedTask;
    }

    [Test]
    [Arguments(39, 0, 2, 1)]
    [Arguments(0, 0, 0, 1)]
    [Arguments(0, 0, 1, 0)]
    [Arguments(-1, 0, 1, 1)]
    public async Task Skips_a_bad_stretch(int x, int y, int width, int height)
    {
        var layout = Parse($$"""{ "darkStretches": [ { "x": {{x}}, "y": {{y}}, "width": {{width}}, "height": {{height}} } ] }""", out var problems);

        layout.DarkStretches
              .Should()
              .BeEmpty();

        problems.Should()
                .ContainSingle()
                .Which
                .Should()
                .Contain("stretch 0");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Invalid_json_gives_an_empty_layout_and_one_problem()
    {
        var layout = Parse("{ not json", out var problems);

        layout.Segments
              .Should()
              .BeEmpty();

        layout.DarkStretches
              .Should()
              .BeEmpty();

        problems.Should()
                .ContainSingle()
                .Which
                .Should()
                .Contain("not valid JSON");

        await Task.CompletedTask;
    }

    [Test]
    public async Task IsInFront_for_a_north_run()
    {
        var run = new MirrorSegment("n", 10, 5, MirrorSide.North, 3, MirrorStyle.Glass, MirrorFunhouse.None, null);

        run.IsInFront(new Point(10, 6), 4, 1)
           .Should()
           .BeTrue();

        run.IsInFront(new Point(13, 9), 4, 1)
           .Should()
           .BeTrue();

        run.IsInFront(new Point(9, 6), 4, 0)
           .Should()
           .BeFalse();

        run.IsInFront(new Point(10, 10), 4, 1)
           .Should()
           .BeFalse();

        run.IsInFront(new Point(10, 5), 4, 1)
           .Should()
           .BeFalse();

        run.IsInFront(new Point(14, 6), 4, 1)
           .Should()
           .BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task IsInFront_for_a_west_run()
    {
        var run = new MirrorSegment("w", 20, 10, MirrorSide.West, 2, MirrorStyle.Glass, MirrorFunhouse.None, null);

        run.IsInFront(new Point(21, 10), 3, 0)
           .Should()
           .BeTrue();

        run.IsInFront(new Point(23, 11), 3, 0)
           .Should()
           .BeTrue();

        run.IsInFront(new Point(24, 11), 3, 0)
           .Should()
           .BeFalse();

        run.IsInFront(new Point(21, 12), 3, 0)
           .Should()
           .BeFalse();

        run.IsInFront(new Point(21, 12), 3, 1)
           .Should()
           .BeTrue();

        run.IsInFront(new Point(19, 10), 3, 1)
           .Should()
           .BeFalse();

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: the Verify command. Expected: build error, `Chaos.Services.MirrorMaze` doesn't exist.

- [ ] **Step 3: Write `MirrorLayout`**

Create `SERVER/Chaos/Services/MirrorMaze/MirrorLayout.cs`:

```csharp
#region
using System.Text.Json;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Services.MirrorMaze;

/// <summary>One validated run of mirror faces. <see cref="X" />, <see cref="Y" /> is its first wall tile.</summary>
public sealed record MirrorSegment(
    string Id,
    int X,
    int Y,
    MirrorSide Side,
    int Length,
    MirrorStyle Style,
    MirrorFunhouse Funhouse,
    string? PartnerId)
{
    /// <summary>The wall tiles the run covers, in order.</summary>
    public IEnumerable<Point> WallTiles()
    {
        for (var i = 0; i < Length; i++)
            yield return Side == MirrorSide.North ? new Point(X + i, Y) : new Point(X, Y + i);
    }

    /// <summary>The floor tile directly in front of a wall tile of this run.</summary>
    public Point FrontOf(Point wall) => Side == MirrorSide.North ? new Point(wall.X, wall.Y + 1) : new Point(wall.X + 1, wall.Y);

    /// <summary>
    ///     True when <paramref name="point" /> is 1 to <paramref name="depth" /> tiles in front of the run and level
    ///     with it, allowing <paramref name="margin" /> extra tiles past each end.
    /// </summary>
    public bool IsInFront(IPoint point, int depth, int margin)
        => Side == MirrorSide.North
            ? (point.Y > Y) && (point.Y <= Y + depth) && (point.X >= X - margin) && (point.X <= X + Length - 1 + margin)
            : (point.X > X) && (point.X <= X + depth) && (point.Y >= Y - margin) && (point.Y <= Y + Length - 1 + margin);
}

/// <summary>A rectangle of floor tiles drawn nearly black, where only the mirrors shine.</summary>
public sealed record MirrorStretch(int X, int Y, int Width, int Height);

/// <summary>
///     A map's mirrors, read from the <c>mirrors.json</c> beside its <c>reactors.json</c>. Every entry is checked
///     against the map: a bad entry is skipped and described, and never stops the rest from loading.
/// </summary>
public sealed class MirrorLayout
{
    /// <summary>Window partners may be at most this many tiles apart, so clients can see both.</summary>
    public const int MAX_WINDOW_DISTANCE = 10;

    public static MirrorLayout Empty { get; } = new([], []);

    public IReadOnlyList<MirrorStretch> DarkStretches { get; }
    public IReadOnlyList<MirrorSegment> Segments { get; }

    private MirrorLayout(IReadOnlyList<MirrorSegment> segments, IReadOnlyList<MirrorStretch> darkStretches)
    {
        Segments = segments;
        DarkStretches = darkStretches;
    }

    /// <summary>
    ///     Parses and validates <paramref name="json" /> against a map of the given size, where
    ///     <paramref name="isWall" /> says whether a tile blocks. Every skipped entry adds one line to
    ///     <paramref name="problems" />.
    /// </summary>
    public static MirrorLayout Parse(
        string json,
        int mapWidth,
        int mapHeight,
        Func<int, int, bool> isWall,
        out List<string> problems)
    {
        problems = [];

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        } catch (JsonException ex)
        {
            problems.Add($"mirrors.json is not valid JSON: {ex.Message}");

            return Empty;
        }

        using (document)
        {
            var root = document.RootElement;
            var candidates = new List<(int Index, MirrorSegment Segment)>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if ((root.ValueKind == JsonValueKind.Object) && root.TryGetProperty("segments", out var segmentsElement))
            {
                var index = 0;

                foreach (var element in segmentsElement.EnumerateArray())
                {
                    var reason = TryReadSegment(element, out var segment);

                    if ((reason is null) && !seenIds.Add(segment!.Id))
                        reason = "duplicate id";

                    reason ??= CheckAgainstMap(segment!, mapWidth, mapHeight, isWall);

                    if (reason is null)
                        candidates.Add((index, segment!));
                    else
                        problems.Add($"segment {index} ({IdOf(element)}): {reason}");

                    index++;
                }
            }

            var segments = DropBrokenWindows(candidates, problems);
            var stretches = new List<MirrorStretch>();

            if ((root.ValueKind == JsonValueKind.Object) && root.TryGetProperty("darkStretches", out var stretchesElement))
            {
                var index = 0;

                foreach (var element in stretchesElement.EnumerateArray())
                {
                    var stretch = new MirrorStretch(
                        IntOf(element, "x"),
                        IntOf(element, "y"),
                        IntOf(element, "width"),
                        IntOf(element, "height"));

                    if ((stretch.X < 0)
                        || (stretch.Y < 0)
                        || (stretch.Width < 1)
                        || (stretch.Height < 1)
                        || (stretch.X + stretch.Width > Math.Min(mapWidth, 256))
                        || (stretch.Y + stretch.Height > Math.Min(mapHeight, 256)))
                        problems.Add($"stretch {index}: must be at least 1x1 and inside the map");
                    else if (stretches.Count == byte.MaxValue)
                        problems.Add($"stretch {index}: a map holds at most {byte.MaxValue} dark stretches");
                    else
                        stretches.Add(stretch);

                    index++;
                }
            }

            return new MirrorLayout(segments, stretches);
        }
    }

    /// <summary>The packet for this layout. Window partners become indices into the segment list.</summary>
    public MirrorLayoutArgs ToArgs()
    {
        var indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < Segments.Count; i++)
            indexById[Segments[i].Id] = i;

        return new MirrorLayoutArgs
        {
            Segments = Segments.Select(s => new MirrorSegmentInfo
                               {
                                   Id = s.Id,
                                   X = (byte)s.X,
                                   Y = (byte)s.Y,
                                   Side = s.Side,
                                   Length = (byte)s.Length,
                                   Style = s.Style,
                                   Funhouse = s.Funhouse,
                                   PartnerIndex = (s.PartnerId is not null) && indexById.TryGetValue(s.PartnerId, out var partner)
                                       ? (ushort)partner
                                       : MirrorSegmentInfo.NO_PARTNER
                               })
                               .ToList(),
            DarkStretches = DarkStretches.Select(s => new MirrorStretchInfo
                                         {
                                             X = (byte)s.X,
                                             Y = (byte)s.Y,
                                             Width = (byte)s.Width,
                                             Height = (byte)s.Height
                                         })
                                         .ToList()
        };
    }

    private static string? TryReadSegment(JsonElement element, out MirrorSegment? segment)
    {
        segment = null;

        if (element.ValueKind != JsonValueKind.Object)
            return "is not an object";

        var id = StringOf(element, "id");

        if (string.IsNullOrWhiteSpace(id) || (id.Length > 40))
            return "id must be 1 to 40 characters";

        MirrorSide side;

        switch (StringOf(element, "side")
                    ?.ToLowerInvariant())
        {
            case "north":
                side = MirrorSide.North;

                break;
            case "west":
                side = MirrorSide.West;

                break;
            default:
                return "side must be north or west";
        }

        if (!Enum.TryParse<MirrorStyle>(StringOf(element, "style"), true, out var style) || !Enum.IsDefined(style))
            return "style must be glass, funhouse, haunted, endless or window";

        var funhouse = MirrorFunhouse.None;

        if (style == MirrorStyle.Funhouse)
            if (!Enum.TryParse(StringOf(element, "funhouse"), true, out funhouse)
                || !Enum.IsDefined(funhouse)
                || (funhouse == MirrorFunhouse.None))
                return "a funhouse segment needs funhouse: tall, wide or wave";

        var length = IntOf(element, "length");

        if (length is < 1 or > 255)
            return "length must be 1 to 255";

        var partner = StringOf(element, "partner");

        if ((style == MirrorStyle.Window) && string.IsNullOrWhiteSpace(partner))
            return "a window segment needs a partner";

        segment = new MirrorSegment(
            id,
            IntOf(element, "x"),
            IntOf(element, "y"),
            side,
            length,
            style,
            funhouse,
            style == MirrorStyle.Window ? partner : null);

        return null;
    }

    private static string? CheckAgainstMap(MirrorSegment segment, int mapWidth, int mapHeight, Func<int, int, bool> isWall)
    {
        //packets carry coordinates as single bytes
        var width = Math.Min(mapWidth, 256);
        var height = Math.Min(mapHeight, 256);

        foreach (var wall in segment.WallTiles())
        {
            var front = segment.FrontOf(wall);

            if ((wall.X < 0) || (wall.Y < 0) || (front.X >= width) || (front.Y >= height) || (wall.X >= width) || (wall.Y >= height))
                return $"tile ({wall.X}, {wall.Y}) or the floor in front of it is off the map";

            if (!isWall(wall.X, wall.Y))
                return $"tile ({wall.X}, {wall.Y}) is not a wall";

            if (isWall(front.X, front.Y))
                return $"the floor in front of ({wall.X}, {wall.Y}) is blocked";
        }

        return null;
    }

    private static List<MirrorSegment> DropBrokenWindows(List<(int Index, MirrorSegment Segment)> candidates, List<string> problems)
    {
        var byId = candidates.ToDictionary(c => c.Segment.Id, c => c.Segment, StringComparer.OrdinalIgnoreCase);
        var kept = new List<MirrorSegment>();

        foreach ((var index, var segment) in candidates)
        {
            if (segment.Style != MirrorStyle.Window)
            {
                kept.Add(segment);

                continue;
            }

            if (!byId.TryGetValue(segment.PartnerId!, out var partner)
                || (partner.Style != MirrorStyle.Window)
                || !string.Equals(partner.PartnerId, segment.Id, StringComparison.OrdinalIgnoreCase)
                || (partner.Side != segment.Side)
                || (partner.Length != segment.Length)
                || (Math.Max(Math.Abs(partner.X - segment.X), Math.Abs(partner.Y - segment.Y)) > MAX_WINDOW_DISTANCE))
            {
                problems.Add(
                    $"segment {index} ({segment.Id}): window partner must name it back, share its side and length, and be within {
                        MAX_WINDOW_DISTANCE} tiles");

                continue;
            }

            kept.Add(segment);
        }

        return kept;
    }

    private static string IdOf(JsonElement element) => element.ValueKind == JsonValueKind.Object ? StringOf(element, "id") ?? "?" : "?";

    private static int IntOf(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : -1;

    private static string? StringOf(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.String) ? value.GetString() : null;
}
```

Notes:
- `IntOf` returns −1 for a missing number, so a missing `x`, `y` or `length` fails the bounds or length checks.
- A window pair is checked from both ends. When one half is broken, both halves fail, because each needs the other to name it back from the candidate list. If only the broken half had been dropped first, the survivor's check would still fail: `byId` holds every candidate, and the survivor's partner is wrong. Both halves show up as problems.

- [ ] **Step 4: Run the tests to see them pass**

Run: the Verify command. Expected: all pass (1 + 10 + 1 + 4 + 4 + 1 + 2 = 23 tests).

```json:metadata
{"files": ["Chaos/Services/MirrorMaze/MirrorLayout.cs", "Tests/Chaos.Tests/MirrorMaze/MirrorLayoutTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MirrorLayoutTests/*\"", "acceptanceCriteria": ["valid file parses with no problems and ToArgs links window partners by index", "each bad segment is skipped with one problem line", "bad stretches skipped", "invalid JSON gives empty layout and one problem", "IsInFront correct for north and west"], "modelTier": "mechanical"}
```

---

### Task 3: Double scheduler

**Goal:** `MirrorDoubleScheduler` decides, from the clock, the players' positions and the layout, when a reflection steps out and whose.

**Files:**
- Create: `SERVER/Chaos/Services/MirrorMaze/MirrorDoubleScheduler.cs`
- Test: `SERVER/Tests/Chaos.Tests/MirrorMaze/MirrorDoubleSchedulerTests.cs`

**Acceptance Criteria:**
- [ ] Nothing is picked before the first 20–40 s interval ends
- [ ] After it, a player 1–3 tiles in front of a haunted run is picked with that run's index
- [ ] Players in front of non-haunted runs, or 4+ tiles away, are never picked
- [ ] A picked player isn't picked again within 2 minutes; another player can be
- [ ] Each new interval is 20–40 s, from the injected random source

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorDoubleSchedulerTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SERVER/Tests/Chaos.Tests/MirrorMaze/MirrorDoubleSchedulerTests.cs`:

```csharp
using Chaos.Services.MirrorMaze;
using FluentAssertions;

namespace Chaos.Tests.MirrorMaze;

public class MirrorDoubleSchedulerTests
{
    private static readonly DateTime T0 = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    //segment 0: glass, north at row 5, x 0-3. segment 1: haunted, north at row 5, x 10-13.
    private static readonly MirrorLayout Layout = MirrorLayout.Parse(
        """
        { "segments": [
          { "id": "g", "x": 0, "y": 5, "side": "north", "length": 4, "style": "glass" },
          { "id": "h", "x": 10, "y": 5, "side": "north", "length": 4, "style": "haunted" } ] }
        """,
        30,
        30,
        (_, y) => y == 5,
        out _);

    //always returns the lowest value, so intervals are 20 s and the first candidate wins
    private static int Lowest(int min, int max) => min;

    [Test]
    public async Task Picks_nothing_before_the_first_interval_ends()
    {
        var scheduler = new MirrorDoubleScheduler(T0, Lowest);

        scheduler.Tick(T0.AddSeconds(19), [(1u, 11, 6)], Layout)
                 .Should()
                 .BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Picks_a_player_in_front_of_a_haunted_run()
    {
        var scheduler = new MirrorDoubleScheduler(T0, Lowest);

        scheduler.Tick(T0.AddSeconds(20), [(7u, 11, 8)], Layout)
                 .Should()
                 .Be((7u, 1));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Ignores_players_at_other_mirrors_or_too_far_away()
    {
        var scheduler = new MirrorDoubleScheduler(T0, Lowest);

        scheduler.Tick(T0.AddSeconds(20), [(1u, 1, 6), (2u, 11, 9), (3u, 20, 6)], Layout)
                 .Should()
                 .BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Waits_two_minutes_before_picking_the_same_player_again()
    {
        var scheduler = new MirrorDoubleScheduler(T0, Lowest);

        scheduler.Tick(T0.AddSeconds(20), [(7u, 11, 6)], Layout)
                 .Should()
                 .Be((7u, 1));

        scheduler.Tick(T0.AddSeconds(40), [(7u, 11, 6)], Layout)
                 .Should()
                 .BeNull();

        scheduler.Tick(T0.AddSeconds(60), [(7u, 11, 6), (8u, 12, 7)], Layout)
                 .Should()
                 .Be((8u, 1));

        scheduler.Tick(T0.AddSeconds(141), [(7u, 11, 6)], Layout)
                 .Should()
                 .Be((7u, 1));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Each_interval_comes_from_the_random_source()
    {
        var asked = new List<(int Min, int Max)>();

        var scheduler = new MirrorDoubleScheduler(
            T0,
            (min, max) =>
            {
                asked.Add((min, max));

                return max - 1;
            });

        //first interval is 40 s (max - 1 = 40 when the range is 20..41)
        scheduler.Tick(T0.AddSeconds(39), [(7u, 11, 6)], Layout)
                 .Should()
                 .BeNull();

        scheduler.Tick(T0.AddSeconds(40), [(7u, 11, 6)], Layout)
                 .Should()
                 .NotBeNull();

        asked.Should()
             .Contain((20, 41));

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: the Verify command. Expected: build error, `MirrorDoubleScheduler` doesn't exist.

- [ ] **Step 3: Write the scheduler**

Create `SERVER/Chaos/Services/MirrorMaze/MirrorDoubleScheduler.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
#endregion

namespace Chaos.Services.MirrorMaze;

/// <summary>
///     Decides when a player's reflection climbs out of a haunted mirror, and whose. Pure: the map script feeds it the
///     clock and the players' positions, and sends the packet it asks for.
/// </summary>
public sealed class MirrorDoubleScheduler
{
    public const int MIN_INTERVAL_SECONDS = 20;
    public const int MAX_INTERVAL_SECONDS = 40;

    /// <summary>A player must stand 1 to this many tiles in front of a haunted run.</summary>
    public const int RANGE = 3;

    public static readonly TimeSpan PlayerCooldown = TimeSpan.FromMinutes(2);

    private readonly Dictionary<uint, DateTime> LastPickedUtc = [];

    /// <summary>Returns a value from min (inclusive) to max (exclusive), like <see cref="Random.Next(int, int)" />.</summary>
    private readonly Func<int, int, int> NextInt;

    private DateTime NextPickUtc;

    public MirrorDoubleScheduler(DateTime nowUtc, Func<int, int, int> nextInt)
    {
        NextInt = nextInt;
        NextPickUtc = nowUtc + NextInterval();
    }

    /// <summary>
    ///     Returns the player whose double steps out now and the index of the haunted run it comes from, or null.
    /// </summary>
    public (uint EntityId, int SegmentIndex)? Tick(DateTime nowUtc, IReadOnlyList<(uint Id, int X, int Y)> players, MirrorLayout layout)
    {
        if (nowUtc < NextPickUtc)
            return null;

        NextPickUtc = nowUtc + NextInterval();

        foreach (var id in LastPickedUtc.Where(kvp => nowUtc - kvp.Value >= PlayerCooldown)
                                         .Select(kvp => kvp.Key)
                                         .ToList())
            LastPickedUtc.Remove(id);

        var candidates = new List<(uint Id, int SegmentIndex)>();

        foreach ((var id, var x, var y) in players)
        {
            if (LastPickedUtc.ContainsKey(id))
                continue;

            var point = new Point(x, y);

            for (var i = 0; i < layout.Segments.Count; i++)
            {
                var segment = layout.Segments[i];

                if ((segment.Style == MirrorStyle.Haunted) && segment.IsInFront(point, RANGE, 0))
                {
                    candidates.Add((id, i));

                    break;
                }
            }
        }

        if (candidates.Count == 0)
            return null;

        var pick = candidates[NextInt(0, candidates.Count)];
        LastPickedUtc[pick.Id] = nowUtc;

        return pick;
    }

    private TimeSpan NextInterval() => TimeSpan.FromSeconds(NextInt(MIN_INTERVAL_SECONDS, MAX_INTERVAL_SECONDS + 1));
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: the Verify command. Expected: 5 passed.

```json:metadata
{"files": ["Chaos/Services/MirrorMaze/MirrorDoubleScheduler.cs", "Tests/Chaos.Tests/MirrorMaze/MirrorDoubleSchedulerTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MirrorDoubleSchedulerTests/*\"", "acceptanceCriteria": ["nothing before first interval", "picks player in front of haunted run with its index", "ignores other mirrors and far players", "2-minute per-player cooldown", "intervals 20-40 s from the random source"], "modelTier": "mechanical"}
```

---

### Task 4: Maze progress rules

**Goal:** `MirrorMazeProgress` holds every maze rule that touches a player's save: taking key pieces, checking the heart's mirror door, and claiming the prize with its first-time legend mark.

**Files:**
- Create: `SERVER/Chaos/Services/MirrorMaze/MirrorMazeProgress.cs`
- Test: `SERVER/Tests/Chaos.Tests/MirrorMaze/MirrorMazeProgressTests.cs`

**Acceptance Criteria:**
- [ ] `TakeKeyPiece` gives a piece once per 24 h per wing and reports `AlreadyHeld` after
- [ ] `MissingPieces` lists the wings without an active piece, in funhouse, endless, haunted order
- [ ] `CheckHeart` returns `AlreadyClaimedToday` while `mirrormaze_daily_cd` is active, else `MissingPieces` while any piece is missing, else `Open`
- [ ] `ClaimPrize` returns `NoPieces` or `AlreadyClaimed` without changing anything; otherwise it starts the daily cooldown, removes all three pieces, and returns `ClaimedFirstTime` with the legend mark the first time, `Claimed` after
- [ ] `JoinWings` gives "funhouse, haunted" for those two wings

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorMazeProgressTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SERVER/Tests/Chaos.Tests/MirrorMaze/MirrorMazeProgressTests.cs`:

```csharp
using Chaos.Services.MirrorMaze;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;

namespace Chaos.Tests.MirrorMaze;

public class MirrorMazeProgressTests
{
    private static void TakeAll(Chaos.Models.World.Aisling aisling)
    {
        MirrorMazeProgress.TakeKeyPiece(aisling, MirrorWing.Funhouse);
        MirrorMazeProgress.TakeKeyPiece(aisling, MirrorWing.Endless);
        MirrorMazeProgress.TakeKeyPiece(aisling, MirrorWing.Haunted);
    }

    [Test]
    public async Task A_wing_gives_its_piece_once()
    {
        var aisling = MockAisling.Create();

        MirrorMazeProgress.TakeKeyPiece(aisling, MirrorWing.Endless)
                          .Should()
                          .Be(KeyPieceResult.Given);

        MirrorMazeProgress.TakeKeyPiece(aisling, MirrorWing.Endless)
                          .Should()
                          .Be(KeyPieceResult.AlreadyHeld);

        aisling.Trackers
               .TimedEvents
               .HasActiveEvent(MirrorMazeProgress.KeyEventId(MirrorWing.Endless), out _)
               .Should()
               .BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task MissingPieces_lists_wings_in_order()
    {
        var aisling = MockAisling.Create();
        MirrorMazeProgress.TakeKeyPiece(aisling, MirrorWing.Endless);

        MirrorMazeProgress.MissingPieces(aisling)
                          .Should()
                          .Equal(MirrorWing.Funhouse, MirrorWing.Haunted);

        MirrorMazeProgress.JoinWings(MirrorMazeProgress.MissingPieces(aisling))
                          .Should()
                          .Be("funhouse, haunted");

        await Task.CompletedTask;
    }

    [Test]
    public async Task CheckHeart_follows_cooldown_then_pieces()
    {
        var aisling = MockAisling.Create();

        MirrorMazeProgress.CheckHeart(aisling)
                          .Should()
                          .Be(HeartCheck.MissingPieces);

        TakeAll(aisling);

        MirrorMazeProgress.CheckHeart(aisling)
                          .Should()
                          .Be(HeartCheck.Open);

        aisling.Trackers.TimedEvents.AddEvent(MirrorMazeProgress.DAILY_COOLDOWN_EVENT, TimeSpan.FromHours(24), true);

        MirrorMazeProgress.CheckHeart(aisling)
                          .Should()
                          .Be(HeartCheck.AlreadyClaimedToday);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ClaimPrize_without_pieces_changes_nothing()
    {
        var aisling = MockAisling.Create();
        MirrorMazeProgress.TakeKeyPiece(aisling, MirrorWing.Funhouse);

        MirrorMazeProgress.ClaimPrize(aisling)
                          .Should()
                          .Be(PrizeResult.NoPieces);

        aisling.Trackers
               .TimedEvents
               .HasActiveEvent(MirrorMazeProgress.DAILY_COOLDOWN_EVENT, out _)
               .Should()
               .BeFalse();

        MirrorMazeProgress.MissingPieces(aisling)
                          .Should()
                          .HaveCount(2);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ClaimPrize_first_time_marks_the_legend_then_later_does_not()
    {
        var aisling = MockAisling.Create();
        TakeAll(aisling);

        MirrorMazeProgress.ClaimPrize(aisling)
                          .Should()
                          .Be(PrizeResult.ClaimedFirstTime);

        aisling.Legend
               .ContainsKey(MirrorMazeProgress.LEGEND_KEY)
               .Should()
               .BeTrue();

        MirrorMazeProgress.MissingPieces(aisling)
                          .Should()
                          .HaveCount(3);

        MirrorMazeProgress.ClaimPrize(aisling)
                          .Should()
                          .Be(PrizeResult.AlreadyClaimed);

        //a day later: cooldown gone, pieces collected again
        aisling.Trackers.TimedEvents.ForceRemoveEvent(MirrorMazeProgress.DAILY_COOLDOWN_EVENT);
        TakeAll(aisling);

        MirrorMazeProgress.ClaimPrize(aisling)
                          .Should()
                          .Be(PrizeResult.Claimed);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: the Verify command. Expected: build error, `MirrorMazeProgress` doesn't exist.

- [ ] **Step 3: Write the rules**

Create `SERVER/Chaos/Services/MirrorMaze/MirrorMazeProgress.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.Legend;
using Chaos.Models.World;
using Chaos.Time;
#endregion

namespace Chaos.Services.MirrorMaze;

/// <summary>The three maze wings, each with a wisp holding one key piece.</summary>
public enum MirrorWing
{
    Funhouse,
    Endless,
    Haunted
}

public enum KeyPieceResult
{
    Given,
    AlreadyHeld
}

public enum HeartCheck
{
    Open,
    MissingPieces,
    AlreadyClaimedToday
}

public enum PrizeResult
{
    NoPieces,
    AlreadyClaimed,
    Claimed,
    ClaimedFirstTime
}

/// <summary>
///     The maze's rules that touch a player's save. Key pieces and the daily prize are timed events; the first clear is
///     a legend mark, which also grants the Mirror Walker emblem. Candy is given by the caller.
/// </summary>
public static class MirrorMazeProgress
{
    public const string DAILY_COOLDOWN_EVENT = "mirrormaze_daily_cd";
    public const string LEGEND_KEY = "mirrormaze";
    public const string LEGEND_TEXT = "Saw through the Mirror Maze";
    public const int PRIZE_CANDY = 20;
    public const string CANDY_TEMPLATE_KEY = "halloweencandy";

    public static readonly TimeSpan PieceDuration = TimeSpan.FromHours(24);
    public static readonly TimeSpan DailyCooldown = TimeSpan.FromHours(24);

    public static IReadOnlyList<MirrorWing> AllWings { get; } = [MirrorWing.Funhouse, MirrorWing.Endless, MirrorWing.Haunted];

    public static string KeyEventId(MirrorWing wing) => $"mirrormaze_key_{NameOf(wing)}";

    /// <summary>The wing's name as players read it: "funhouse", "endless" or "haunted".</summary>
    public static string NameOf(MirrorWing wing) => wing.ToString()
                                                         .ToLowerInvariant();

    public static string JoinWings(IEnumerable<MirrorWing> wings) => string.Join(", ", wings.Select(NameOf));

    public static KeyPieceResult TakeKeyPiece(Aisling aisling, MirrorWing wing)
    {
        if (aisling.Trackers.TimedEvents.HasActiveEvent(KeyEventId(wing), out _))
            return KeyPieceResult.AlreadyHeld;

        aisling.Trackers.TimedEvents.AddEvent(KeyEventId(wing), PieceDuration, true);

        return KeyPieceResult.Given;
    }

    public static IReadOnlyList<MirrorWing> MissingPieces(Aisling aisling)
        => AllWings.Where(wing => !aisling.Trackers.TimedEvents.HasActiveEvent(KeyEventId(wing), out _))
                   .ToList();

    public static HeartCheck CheckHeart(Aisling aisling)
    {
        if (aisling.Trackers.TimedEvents.HasActiveEvent(DAILY_COOLDOWN_EVENT, out _))
            return HeartCheck.AlreadyClaimedToday;

        return MissingPieces(aisling).Count > 0 ? HeartCheck.MissingPieces : HeartCheck.Open;
    }

    /// <summary>
    ///     Claims today's prize: starts the daily cooldown, spends the three pieces, and the first time ever adds the
    ///     legend mark. Changes nothing unless the heart would be <see cref="HeartCheck.Open" />.
    /// </summary>
    public static PrizeResult ClaimPrize(Aisling aisling)
    {
        switch (CheckHeart(aisling))
        {
            case HeartCheck.AlreadyClaimedToday:
                return PrizeResult.AlreadyClaimed;
            case HeartCheck.MissingPieces:
                return PrizeResult.NoPieces;
        }

        aisling.Trackers.TimedEvents.AddEvent(DAILY_COOLDOWN_EVENT, DailyCooldown, true);

        foreach (var wing in AllWings)
            if (aisling.Trackers.TimedEvents.HasActiveEvent(KeyEventId(wing), out _))
                aisling.Trackers.TimedEvents.ForceRemoveEvent(KeyEventId(wing));

        if (aisling.Legend.ContainsKey(LEGEND_KEY))
            return PrizeResult.Claimed;

        aisling.Legend.AddOrAccumulate(
            new LegendMark(
                LEGEND_TEXT,
                LEGEND_KEY,
                MarkIcon.Victory,
                MarkColor.LightPurple,
                1,
                GameTime.Now));

        return PrizeResult.ClaimedFirstTime;
    }
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: the Verify command. Expected: 5 passed. If `AddOrAccumulate` sends a client packet through `aisling.Client`, the `MockAisling` client mock absorbs it.

```json:metadata
{"files": ["Chaos/Services/MirrorMaze/MirrorMazeProgress.cs", "Tests/Chaos.Tests/MirrorMaze/MirrorMazeProgressTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MirrorMazeProgressTests/*\"", "acceptanceCriteria": ["pieces once per 24h per wing", "MissingPieces in wing order", "CheckHeart cooldown then pieces", "ClaimPrize NoPieces/AlreadyClaimed change nothing; claim sets cooldown, clears pieces, first time adds legend mark", "JoinWings formatting"], "modelTier": "mechanical"}
```

---

### Task 5: Mirror map script and the Halloween window

**Goal:** Any map with the `mirrormap` script key loads its `mirrors.json` once, sends the layout to each player on entry, and, when it has haunted runs, sends "your double steps out" events to nearby players. The maze joins the Halloween event window.

**Files:**
- Modify: `SERVER/Chaos/Collections/MapInstance.cs` (add `SourceDirectory` after `BaseInstanceId`)
- Modify: `SERVER/Chaos/Services/Storage/ExpiringMapInstanceCache.cs` (`InnerLoadFromFile` sets it)
- Modify: `SERVER/Chaos/Networking/Abstractions/IChaosWorldClient.cs` (two send methods)
- Modify: `SERVER/Chaos/Networking/ChaosWorldClient.cs` (implement them)
- Modify: `SERVER/Chaos/Models/World/EventPeriod.cs` (`suomi_mirror_maze` in the `EventType.Halloween` list)
- Create: `SERVER/Chaos/Scripting/MapScripts/Temuair/Events/MirrorMapScript.cs`
- Test: `SERVER/Tests/Chaos.Tests/MirrorMaze/MirrorMapScriptTests.cs`

**Acceptance Criteria:**
- [ ] A map loaded from a folder knows that folder in `SourceDirectory`
- [ ] Entering a map whose folder has a valid `mirrors.json` sends one `MirrorLayout` with its segments
- [ ] Entering a map with no `mirrors.json`, or no `SourceDirectory`, sends an empty layout and logs one warning
- [ ] The layout is read from disk once per map instance, not on every entry
- [ ] `EventPeriod.IsSpecificEventActive(new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc), "suomi_mirror_maze", EventType.Halloween)` is true, and for 2026-11-10 it is false
- [ ] The server solution builds with `0 Error(s)`

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorMapScriptTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Remember where each map was loaded from**

In `SERVER/Chaos/Collections/MapInstance.cs`, directly after `public string? BaseInstanceId { get; set; }`, insert:

```csharp

    /// <summary>
    ///     The folder this instance was loaded from (the one holding its instance.json), or null for maps not loaded
    ///     from disk. Map scripts read extra per-map files from here, e.g. mirrors.json.
    /// </summary>
    public string? SourceDirectory { get; set; }
```

In `SERVER/Chaos/Services/Storage/ExpiringMapInstanceCache.cs`, in `InnerLoadFromFile`, directly after `mapInstance.BaseInstanceId = baseInstanceId;`, insert:

```csharp
        mapInstance.SourceDirectory = directory;
```

- [ ] **Step 2: Add the send methods**

In `SERVER/Chaos/Networking/Abstractions/IChaosWorldClient.cs`, after `void SendStageLightingBoard(StageLightingBoardArgs args);`, insert:

```csharp

    /// <summary>Sends every mirror run and dark stretch on the player's current map (empty for maps without mirrors).</summary>
    void SendMirrorLayout(MirrorLayoutArgs args);

    /// <summary>Tells the client a player's reflection is climbing out of a haunted mirror.</summary>
    void SendMirrorDouble(MirrorDoubleArgs args);
```

In `SERVER/Chaos/Networking/ChaosWorldClient.cs`, after `public void SendStageLightingBoard(StageLightingBoardArgs args) => Send(args);`, insert:

```csharp

    /// <inheritdoc />
    public void SendMirrorLayout(MirrorLayoutArgs args) => Send(args);

    /// <inheritdoc />
    public void SendMirrorDouble(MirrorDoubleArgs args) => Send(args);
```

- [ ] **Step 3: Add the maze to the Halloween window**

In `SERVER/Chaos/Models/World/EventPeriod.cs`, in `GetAllEvents()`, find the `EventType.Halloween` entry (comment `//Halloween Count/ess / Scare Senaan`). Its map list is `"hm_road", "secludedcave", "macabre_yard"`. Add `"suomi_mirror_maze"` as a fourth entry. Do not change the `EventType.HalloweenNight` entry.

- [ ] **Step 4: Write the failing tests**

Create `SERVER/Tests/Chaos.Tests/MirrorMaze/MirrorMapScriptTests.cs`:

```csharp
using Chaos.Collections;
using Chaos.Definitions;
using Chaos.Models.Map;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.Networking.Entities.Server;
using Chaos.Scripting.MapScripts.Temuair.Events;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Chaos.Tests.MirrorMaze;

public class MirrorMapScriptTests
{
    private static (MirrorMapScript Script, Mock<ILogger<MirrorMapScript>> Logger, MapInstance Map) Make(string? mirrorsJson)
    {
        var map = MockMapInstance.Create(width: 10, height: 10);

        //row 2, x 1-4: walls (foreground 1 is a wall in sotp)
        for (var x = 1; x <= 4; x++)
            map.Template.Tiles[x, 2] = new Tile(0, 1, 0);

        if (mirrorsJson is not null)
        {
            var dir = Directory.CreateTempSubdirectory("mirrormap-test-")
                               .FullName;

            File.WriteAllText(Path.Combine(dir, "mirrors.json"), mirrorsJson);
            map.SourceDirectory = dir;
        }

        var logger = new Mock<ILogger<MirrorMapScript>>();

        return (new MirrorMapScript(map, logger.Object), logger, map);
    }

    private static List<MirrorLayoutArgs> SentLayouts(Aisling aisling)
    {
        var sent = new List<MirrorLayoutArgs>();

        Mock.Get(aisling.Client)
            .Setup(c => c.SendMirrorLayout(It.IsAny<MirrorLayoutArgs>()))
            .Callback<MirrorLayoutArgs>(sent.Add);

        return sent;
    }

    [Test]
    public async Task Sends_the_layout_on_entry()
    {
        (var script, _, var map) = Make("""{ "segments": [ { "id": "a", "x": 1, "y": 2, "side": "north", "length": 4, "style": "glass" } ] }""");
        var aisling = MockAisling.Create(map);
        var sent = SentLayouts(aisling);

        script.OnEntered(aisling);

        sent.Should()
            .ContainSingle()
            .Which
            .Segments
            .Should()
            .ContainSingle()
            .Which
            .Length
            .Should()
            .Be(4);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sends_an_empty_layout_and_warns_once_without_a_file()
    {
        (var script, var logger, var map) = Make(null);
        var first = MockAisling.Create(map);
        var second = MockAisling.Create(map);
        var sentFirst = SentLayouts(first);
        var sentSecond = SentLayouts(second);

        script.OnEntered(first);
        script.OnEntered(second);

        sentFirst.Should()
                 .ContainSingle()
                 .Which
                 .Segments
                 .Should()
                 .BeEmpty();

        sentSecond.Should()
                  .ContainSingle();

        logger.Invocations
              .Count(i => (i.Method.Name == nameof(ILogger.Log)) && Equals(i.Arguments[0], LogLevel.Warning))
              .Should()
              .Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reads_the_file_once()
    {
        (var script, _, var map) = Make("""{ "segments": [ { "id": "a", "x": 1, "y": 2, "side": "north", "length": 1, "style": "glass" } ] }""");
        var first = MockAisling.Create(map);
        SentLayouts(first);
        script.OnEntered(first);

        File.Delete(Path.Combine(map.SourceDirectory!, "mirrors.json"));

        var second = MockAisling.Create(map);
        var sent = SentLayouts(second);
        script.OnEntered(second);

        sent.Should()
            .ContainSingle()
            .Which
            .Segments
            .Should()
            .ContainSingle();

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_maze_is_open_during_halloween_only()
    {
        EventPeriod.IsSpecificEventActive(new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc), "suomi_mirror_maze", EventType.Halloween)
                   .Should()
                   .BeTrue();

        EventPeriod.IsSpecificEventActive(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), "suomi_mirror_maze", EventType.Halloween)
                   .Should()
                   .BeFalse();

        await Task.CompletedTask;
    }
}
```

Note: `EventPeriod` computes its dates from `DateTime.UtcNow`'s year. The window test only holds while the current year's (or previous year's) October dates are the ones checked. If it fails because the run date is in a different year, change the two dates to the current year's October 15 and November 10.

- [ ] **Step 5: Run the tests to see them fail**

Run: the Verify command. Expected: build error, `MirrorMapScript` doesn't exist.

- [ ] **Step 6: Write the map script**

Create `SERVER/Chaos/Scripting/MapScripts/Temuair/Events/MirrorMapScript.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Models.World;
using Chaos.Models.World.Abstractions;
using Chaos.Networking.Entities.Server;
using Chaos.Scripting.MapScripts.Abstractions;
using Chaos.Services.MirrorMaze;
using Chaos.Time;
using Chaos.Time.Abstractions;
#endregion

namespace Chaos.Scripting.MapScripts.Temuair.Events;

/// <summary>
///     Gives a map mirrors. Reads the mirrors.json beside the map's instance.json once, sends it to every player who
///     enters, and on maps with haunted mirrors lets a player's reflection climb out now and then.
/// </summary>
public class MirrorMapScript : MapScriptBase
{
    public const string FILE_NAME = "mirrors.json";

    /// <summary>Players who can see the double: everyone within this many tiles of its owner.</summary>
    public const int DOUBLE_VIEW_RANGE = 15;

    private readonly ILogger<MirrorMapScript> Logger;
    private readonly IIntervalTimer TickTimer = new IntervalTimer(TimeSpan.FromSeconds(1));
    private MirrorLayoutArgs? ArgsCache;
    private MirrorDoubleScheduler? Doubles;
    private MirrorLayout? LayoutCache;

    /// <summary>The map's mirrors, read on first use.</summary>
    public MirrorLayout Layout => LayoutCache ??= Load();

    public MirrorMapScript(MapInstance subject, ILogger<MirrorMapScript> logger)
        : base(subject)
        => Logger = logger;

    public override void OnEntered(Creature creature)
    {
        if (creature is Aisling aisling)
            aisling.Client.SendMirrorLayout(ArgsCache ??= Layout.ToArgs());
    }

    public override void Update(TimeSpan delta)
    {
        TickTimer.Update(delta);

        if (!TickTimer.IntervalElapsed)
            return;

        var layout = Layout;

        if (!layout.Segments.Any(segment => segment.Style == MirrorStyle.Haunted))
            return;

        var aislings = Subject.GetEntities<Aisling>()
                              .ToList();

        if (aislings.Count == 0)
            return;

        Doubles ??= new MirrorDoubleScheduler(DateTime.UtcNow, Random.Shared.Next);

        var pick = Doubles.Tick(
            DateTime.UtcNow,
            aislings.Select(aisling => (aisling.Id, aisling.X, aisling.Y))
                    .ToList(),
            layout);

        if (pick is null)
            return;

        var owner = aislings.First(aisling => aisling.Id == pick.Value.EntityId);

        var args = new MirrorDoubleArgs
        {
            EntityId = owner.Id,
            SegmentIndex = (ushort)pick.Value.SegmentIndex
        };

        foreach (var watcher in Subject.GetEntitiesWithinRange<Aisling>(owner, DOUBLE_VIEW_RANGE))
            watcher.Client.SendMirrorDouble(args);
    }

    private MirrorLayout Load()
    {
        var directory = Subject.SourceDirectory;
        var path = directory is null ? null : Path.Combine(directory, FILE_NAME);

        if ((path is null) || !File.Exists(path))
        {
            Logger.LogWarning("Map {@MapInstanceId} has the mirror script but no {@FileName}", Subject.InstanceId, FILE_NAME);

            return MirrorLayout.Empty;
        }

        var layout = MirrorLayout.Parse(
            File.ReadAllText(path),
            Subject.Template.Width,
            Subject.Template.Height,
            (x, y) => Subject.Template.IsWall(new Point(x, y)),
            out var problems);

        foreach (var problem in problems)
            Logger.LogWarning("Map {@MapInstanceId} {@FileName}: {@Problem}", Subject.InstanceId, FILE_NAME, problem);

        return layout;
    }
}
```

If `MapScriptBase` has no `Update(TimeSpan)` to override, look at how `SuomiTheatreMapScript.Update` is declared and match it.

- [ ] **Step 7: Run the tests to see them pass**

Run: the Verify command. Expected: 4 passed. Then run `dotnet build Chaos.slnx` from SERVER. Expected: `0 Error(s)`.

```json:metadata
{"files": ["Chaos/Collections/MapInstance.cs", "Chaos/Services/Storage/ExpiringMapInstanceCache.cs", "Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos/Networking/ChaosWorldClient.cs", "Chaos/Models/World/EventPeriod.cs", "Chaos/Scripting/MapScripts/Temuair/Events/MirrorMapScript.cs", "Tests/Chaos.Tests/MirrorMaze/MirrorMapScriptTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MirrorMapScriptTests/*\"", "acceptanceCriteria": ["SourceDirectory set on load", "layout sent on entry", "empty layout and one warning without a file", "file read once per instance", "suomi_mirror_maze in the Halloween window", "server builds with 0 errors"], "modelTier": "standard"}
```

---

### Task 6: Mirror doors, wisps, the prize chest and Thulin's trade

**Goal:** The four scripts players touch: mirror doors that ask "Step through the glass?", wisps that give key pieces, the prize chest, and Thulin's candy-for-Macabre-Box trade.

**Files:**
- Create: `SERVER/Chaos/Services/MirrorMaze/MirrorDoorTrip.cs`
- Create: `SERVER/Chaos/Scripting/ReactorTileScripts/Temauir/MirrorMaze/MirrorDoorScript.cs`
- Create: `SERVER/Chaos/Scripting/DialogScripts/Temuair/Events/HalloweenEvents/MirrorDoorDialogScript.cs`
- Create: `SERVER/Chaos/Scripting/MerchantScripts/Events/Halloween/MirrorMazeWispScript.cs`
- Create: `SERVER/Chaos/Scripting/MerchantScripts/Events/Halloween/MirrorMazeChestScript.cs`
- Create: `SERVER/Chaos/Scripting/DialogScripts/Temuair/Suomi/ThulinCandyTradeScript.cs`
- Modify: `SERVER/Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusCheckTimedEventsScript.cs` (four display entries)

**Acceptance Criteria:**
- [ ] `mirrordoor` (reactor): on a walk-on by a player facing `Facing`, shows dialog `mirrordoor_step` carrying a `MirrorDoorTrip`; does nothing for other facings or non-players; refuses with the cloudy message outside the window when `HalloweenOnly` (admins pass); for `HeartEntrance` refuses with the cooldown or missing-pieces message
- [ ] `mirrordoordialog`: option 1 on `mirrordoor_step` warps the player to the trip's destination if they are still on the trip's map within 1 tile of the door, re-checking the heart for heart trips; option 2 closes
- [ ] `mirrormazewisp`: a click within 4 tiles gives the wing's piece or says it is already held
- [ ] `mirrormazechest`: a click within 2 tiles claims the prize, gives 20 `halloweencandy`, and says the exact messages from Global Constraints
- [ ] `thulincandytrade`: adds "Trade candy" to `thulin_initial` during the window; option 1 on `thulin_tradecandy` swaps 100 `halloweencandy` for one `macabrebox`
- [ ] Terminus's timed-events list shows the three shards and the daily prize under "Other"
- [ ] The server solution builds with `0 Error(s)`, and the full `Chaos.Tests` run shows no new failures

**Verify:** from SERVER: `dotnet build Chaos.slnx` → `0 Error(s)`; then `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi` → only the two known failures

**Steps:**

- [ ] **Step 1: The trip record**

Create `SERVER/Chaos/Services/MirrorMaze/MirrorDoorTrip.cs`:

```csharp
#region
using Chaos.Geometry;
#endregion

namespace Chaos.Services.MirrorMaze;

/// <summary>
///     What a mirror door's "Step through the glass?" dialog carries: where the door is, where it leads, and whether
///     it is the heart's door (which needs all three key pieces).
/// </summary>
public sealed record MirrorDoorTrip(string DoorMapInstanceId, Point DoorTile, Location Destination, bool HeartEntrance);
```

- [ ] **Step 2: The mirror door reactor**

Create `SERVER/Chaos/Scripting/ReactorTileScripts/Temauir/MirrorMaze/MirrorDoorScript.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Definitions;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Models.World;
using Chaos.Models.World.Abstractions;
using Chaos.Scripting.ReactorTileScripts.Abstractions;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.MirrorMaze;
#endregion

namespace Chaos.Scripting.ReactorTileScripts.Temauir.MirrorMaze;

/// <summary>
///     The floor tile in front of a mirror door. A player who steps onto it facing the mirror is asked "Step through the
///     glass?". Walking past sideways does nothing.
/// </summary>
public class MirrorDoorScript : ConfigurableReactorTileScriptBase
{
    public const string DIALOG_KEY = "mirrordoor_step";
    public const string MAZE_INSTANCE_ID = "suomi_mirror_maze";

    private readonly IDialogFactory DialogFactory;
    private readonly IMerchantFactory MerchantFactory;

    #region ScriptVars
    /// <summary>The direction a player faces when looking into this mirror.</summary>
    protected Direction Facing { get; init; }

    /// <summary>Where the mirror leads.</summary>
    protected Location Destination { get; init; } = null!;

    /// <summary>Closed (cloudy) outside the Halloween window. Used by the theatre's mirror.</summary>
    protected bool HalloweenOnly { get; init; }

    /// <summary>The heart's door: needs all three key pieces and no prize claimed today.</summary>
    protected bool HeartEntrance { get; init; }
    #endregion

    public MirrorDoorScript(ReactorTile subject, IDialogFactory dialogFactory, IMerchantFactory merchantFactory)
        : base(subject)
    {
        DialogFactory = dialogFactory;
        MerchantFactory = merchantFactory;
    }

    public override void OnWalkedOn(Creature source)
    {
        if (source is not Aisling aisling || (aisling.Direction != Facing))
            return;

        if (HalloweenOnly
            && !aisling.IsAdmin
            && !EventPeriod.IsSpecificEventActive(DateTime.UtcNow, MAZE_INSTANCE_ID, EventType.Halloween))
        {
            aisling.SendOrangeBarMessage("The glass is only cloudy tonight.");

            return;
        }

        if (HeartEntrance && !MirrorDoorRules.CanEnterHeart(aisling))
            return;

        var merchant = MerchantFactory.Create("blank_merchant", Map, Point);
        var dialog = DialogFactory.Create(DIALOG_KEY, merchant);
        dialog.Context = new MirrorDoorTrip(Map.InstanceId, Point, Destination, HeartEntrance);
        dialog.Display(aisling);
    }
}

/// <summary>The heart door's check, shared by the reactor and the dialog's re-check.</summary>
public static class MirrorDoorRules
{
    /// <summary>Returns true when the heart is open to <paramref name="aisling" />, otherwise tells them why not.</summary>
    public static bool CanEnterHeart(Aisling aisling)
    {
        switch (MirrorMazeProgress.CheckHeart(aisling))
        {
            case HeartCheck.AlreadyClaimedToday:
                aisling.SendOrangeBarMessage("You have already seen through the maze today. Come back tomorrow.");

                return false;
            case HeartCheck.MissingPieces:
                aisling.SendOrangeBarMessage(
                    $"The glass will not let you pass. Missing: {MirrorMazeProgress.JoinWings(MirrorMazeProgress.MissingPieces(aisling))}.");

                return false;
            default:
                return true;
        }
    }
}
```

If the `Direction` type in reactor scripts lives in a different namespace, follow `WarpScript`'s or `Creature`'s `using` lines. `Point` here is `Chaos.Geometry.Point`, which `ConfigurableReactorTileScriptBase.Point` already is.

- [ ] **Step 3: The door dialog script**

Create `SERVER/Chaos/Scripting/DialogScripts/Temuair/Events/HalloweenEvents/MirrorDoorDialogScript.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Extensions.Geometry;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Scripting.ReactorTileScripts.Temauir.MirrorMaze;
using Chaos.Services.MirrorMaze;
using Chaos.Storage.Abstractions;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.Events.HalloweenEvents;

/// <summary>"Step through the glass?" Option 1 warps the player through the mirror; option 2 closes.</summary>
public class MirrorDoorDialogScript : DialogScriptBase
{
    private readonly ISimpleCache SimpleCache;

    public MirrorDoorDialogScript(Dialog subject, ISimpleCache simpleCache)
        : base(subject)
        => SimpleCache = simpleCache;

    public override void OnNext(Aisling source, byte? optionIndex = null)
    {
        if (!Subject.Template.TemplateKey.Equals(MirrorDoorScript.DIALOG_KEY, StringComparison.OrdinalIgnoreCase))
            return;

        if ((optionIndex != 1) || Subject.Context is not MirrorDoorTrip trip)
        {
            Subject.Close(source);

            return;
        }

        //the answer must come from the door: same map, at most one tile away
        if (!source.MapInstance.InstanceId.Equals(trip.DoorMapInstanceId, StringComparison.OrdinalIgnoreCase)
            || (source.ManhattanDistanceFrom(trip.DoorTile) > 1))
        {
            Subject.Close(source);

            return;
        }

        if (trip.HeartEntrance && !MirrorDoorRules.CanEnterHeart(source))
        {
            Subject.Close(source);

            return;
        }

        Subject.Close(source);
        source.TraverseMap(SimpleCache.Get<MapInstance>(trip.Destination.Map), trip.Destination);
    }
}
```

`Location` implements `IPoint`, so it can be passed straight to `TraverseMap`. If it doesn't, pass `new Point(trip.Destination.X, trip.Destination.Y)`.

- [ ] **Step 4: The wisp**

Create `SERVER/Chaos/Scripting/MerchantScripts/Events/Halloween/MirrorMazeWispScript.cs`:

```csharp
#region
using Chaos.Extensions.Geometry;
using Chaos.Models.World;
using Chaos.Scripting.MerchantScripts.Abstractions;
using Chaos.Services.MirrorMaze;
#endregion

namespace Chaos.Scripting.MerchantScripts.Events.Halloween;

/// <summary>A wisp at the far end of a maze wing. Clicking it gives that wing's key piece once a day.</summary>
public class MirrorMazeWispScript : ConfigurableMerchantScriptBase
{
    public const int REACH = 4;

    #region ScriptVars
    public MirrorWing Wing { get; init; }
    #endregion

    public MirrorMazeWispScript(Merchant subject)
        : base(subject) { }

    public override void OnClicked(Aisling source)
    {
        if (source.ManhattanDistanceFrom(Subject) > REACH)
            return;

        var name = MirrorMazeProgress.NameOf(Wing);

        source.SendOrangeBarMessage(
            MirrorMazeProgress.TakeKeyPiece(source, Wing) == KeyPieceResult.Given
                ? $"The wisp presses a shard of the {name} mirror into your hand."
                : $"You already hold a shard of the {name} mirror.");
    }
}
```

Check `GildedSpindleScript`'s constructor for the exact `ConfigurableMerchantScriptBase` base call; if it needs more than `subject`, match it.

- [ ] **Step 5: The prize chest**

Create `SERVER/Chaos/Scripting/MerchantScripts/Events/Halloween/MirrorMazeChestScript.cs`:

```csharp
#region
using Chaos.Extensions.Geometry;
using Chaos.Models.World;
using Chaos.Scripting.MerchantScripts.Abstractions;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.MirrorMaze;
#endregion

namespace Chaos.Scripting.MerchantScripts.Events.Halloween;

/// <summary>The chest in the maze's heart: the daily candy, plus a legend mark the first time.</summary>
public class MirrorMazeChestScript : MerchantScriptBase
{
    public const int REACH = 2;

    private readonly IItemFactory ItemFactory;

    public MirrorMazeChestScript(Merchant subject, IItemFactory itemFactory)
        : base(subject)
        => ItemFactory = itemFactory;

    public override void OnClicked(Aisling source)
    {
        if (source.ManhattanDistanceFrom(Subject) > REACH)
            return;

        var result = MirrorMazeProgress.ClaimPrize(source);

        switch (result)
        {
            case PrizeResult.NoPieces:
                source.SendOrangeBarMessage("The chest will not open without all three shards.");

                return;
            case PrizeResult.AlreadyClaimed:
                source.SendOrangeBarMessage("The chest is empty. Come back tomorrow.");

                return;
        }

        var candy = ItemFactory.Create(MirrorMazeProgress.CANDY_TEMPLATE_KEY);
        candy.Count = MirrorMazeProgress.PRIZE_CANDY;
        source.GiveItemOrSendToBank(candy);
        source.SendOrangeBarMessage($"You find {MirrorMazeProgress.PRIZE_CANDY} pieces of candy in the chest!");

        if (result == PrizeResult.ClaimedFirstTime)
            source.SendActiveMessage("Your reflection bows to you.");
    }
}
```

- [ ] **Step 6: Thulin's trade**

Create `SERVER/Chaos/Scripting/DialogScripts/Temuair/Suomi/ThulinCandyTradeScript.cs`:

```csharp
#region
using Chaos.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.MirrorMaze;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.Suomi;

/// <summary>During the Halloween window Thulin trades 100 pieces of candy for a Macabre Box.</summary>
public class ThulinCandyTradeScript : DialogScriptBase
{
    public const int CANDY_PRICE = 100;
    public const string MACABRE_BOX_KEY = "macabrebox";

    private readonly IItemFactory ItemFactory;

    public ThulinCandyTradeScript(Dialog subject, IItemFactory itemFactory)
        : base(subject)
        => ItemFactory = itemFactory;

    private static bool IsHalloween()
        => EventPeriod.IsSpecificEventActive(DateTime.UtcNow, "suomi_mirror_maze", EventType.Halloween);

    public override void OnDisplaying(Aisling source)
    {
        if (!Subject.Template.TemplateKey.Equals("thulin_initial", StringComparison.OrdinalIgnoreCase) || !IsHalloween())
            return;

        var option = new DialogOption
        {
            DialogKey = "thulin_tradecandy",
            OptionText = "Trade candy"
        };

        if (!Subject.HasOption(option.OptionText))
            Subject.Options.Add(option);
    }

    public override void OnNext(Aisling source, byte? optionIndex = null)
    {
        if (!Subject.Template.TemplateKey.Equals("thulin_tradecandy", StringComparison.OrdinalIgnoreCase) || (optionIndex != 1))
            return;

        if (!IsHalloween())
        {
            Subject.Close(source);

            return;
        }

        if (!source.Inventory.HasCountByTemplateKey(MirrorMazeProgress.CANDY_TEMPLATE_KEY, CANDY_PRICE))
        {
            Subject.Reply(source, $"You need {CANDY_PRICE} pieces of candy.");

            return;
        }

        if (!source.Inventory.RemoveQuantityByTemplateKey(MirrorMazeProgress.CANDY_TEMPLATE_KEY, CANDY_PRICE))
        {
            Subject.Reply(source, $"You need {CANDY_PRICE} pieces of candy.");

            return;
        }

        source.GiveItemOrSendToBank(ItemFactory.Create(MACABRE_BOX_KEY));
        Subject.Reply(source, "Thulin hands you a Macabre Box.");
    }
}
```

If `DialogOption` or `Subject.HasOption` live elsewhere, copy the `using` lines from `SuomiTheatreDirectorScript`, which adds an option to `thulin_initial` the same way.

- [ ] **Step 7: Show the timers in Terminus**

In `SERVER/Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusCheckTimedEventsScript.cs`, the dictionary maps timed-event ids to `TimedEventDisplay(name, category)`. Add these four entries next to the other `"Other"` entries:

```csharp
        {
            "mirrormaze_key_funhouse", new TimedEventDisplay("Mirror Shard: Funhouse", "Other")
        },
        {
            "mirrormaze_key_endless", new TimedEventDisplay("Mirror Shard: Endless", "Other")
        },
        {
            "mirrormaze_key_haunted", new TimedEventDisplay("Mirror Shard: Haunted", "Other")
        },
        {
            "mirrormaze_daily_cd", new TimedEventDisplay("Mirror Maze Prize", "Other")
        },
```

- [ ] **Step 8: Build and run the full server tests**

Run the Verify commands. Expected: `0 Error(s)`, and the full run fails only GiveAbility and OnItemDroppedOn stackable.

```json:metadata
{"files": ["Chaos/Services/MirrorMaze/MirrorDoorTrip.cs", "Chaos/Scripting/ReactorTileScripts/Temauir/MirrorMaze/MirrorDoorScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Events/HalloweenEvents/MirrorDoorDialogScript.cs", "Chaos/Scripting/MerchantScripts/Events/Halloween/MirrorMazeWispScript.cs", "Chaos/Scripting/MerchantScripts/Events/Halloween/MirrorMazeChestScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Suomi/ThulinCandyTradeScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusCheckTimedEventsScript.cs"], "verifyCommand": "dotnet build Chaos.slnx", "acceptanceCriteria": ["mirrordoor shows the dialog only to players facing the mirror, with window and heart checks", "mirrordoordialog warps on option 1 from the door only, re-checking the heart", "mirrormazewisp gives the wing piece within 4 tiles", "mirrormazechest claims the prize and gives 20 candy with exact messages", "thulincandytrade adds the option during the window and swaps 100 candy for a macabre box", "Terminus lists the three shards and the daily prize", "server builds with 0 errors and no new test failures"], "modelTier": "standard"}
```

---

### Task 7: Maze generator

**Goal:** `Tools/MirrorMaze/generate_maze.py` builds the 58x58 maze (map file, map template, `mirrors.json`, `reactors.json`, `merchants.json`) from a fixed seed, checks it by the server's rules and the maze's own, and can save a top-down plan image.

**Files:**
- Create: `UNORA/Tools/MirrorMaze/generate_maze.py`
- Create: `UNORA/Tools/MirrorMaze/test_generate_maze.py`
- Create (generated): `UNORA/Data/Configuration/MapData/lod10231.map`
- Create (generated): `UNORA/Data/Configuration/Templates/Maps/10231.json`
- Create (generated): `UNORA/Data/Configuration/MapInstances/Temuair/Events/Halloween/Mirror_Maze/mirrors.json`, `reactors.json`, `merchants.json`

**Acceptance Criteria:**
- [ ] `python -m pytest Tools/MirrorMaze/test_generate_maze.py -q` → 12 passed
- [ ] `python Tools/MirrorMaze/generate_maze.py` prints no `PROBLEM:` lines, reports 3 window pairs and 2 dark stretches, and writes the five files
- [ ] `lod10231.map` is 20,184 bytes (58 × 58 × 6)
- [ ] `python Tools/MirrorMaze/generate_maze.py --check --plan <scratch>/plan.png` writes a plan image showing the lobby, three wings and the heart

**Verify:** from UNORA: `python -m pytest Tools/MirrorMaze/test_generate_maze.py -q` → `12 passed`

**Steps:**

This generator was written and run during planning (2026-09-30). With seed 31 it printed: `map 10231: 58x58, 2284 floor tiles`, `mirror runs: 287 (endless 58, funhouse 57, glass 63, haunted 103, window 6)`, `window pairs: 3`, `dark stretches: 2`, `wisps: {'funhouse': (1, 1), 'endless': (40, 28), 'haunted': (1, 49)}`. Expect the same output.

- [ ] **Step 1: Check the map number is still free**

Run: `ls UNORA/Data/Configuration/MapData | sort -V | tail -1` and the same in every `worktrees/*unora*` folder.
Expected: `lod10230.map` everywhere. If any tree has `lod10231.map`, stop and ask the coordinator for the next free number; change `MAP_ID` in the generator and the `templateKey` in Task 8's `instance.json` to match.

- [ ] **Step 2: Write the tests**

Create `UNORA/Tools/MirrorMaze/test_generate_maze.py`:

```python
"""Tests for the mirror maze generator. Run from the Unora repo root: python -m pytest Tools/MirrorMaze -q"""
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import generate_maze as g  # noqa: E402

MAZE = g.build()


def test_the_default_maze_has_no_problems():
    assert MAZE.problems == []


def test_the_map_file_is_58_by_58_tiles_of_six_bytes():
    data = g.map_bytes(MAZE.floor)
    assert g.SIZE == 58
    assert len(data) == 58 * 58 * 6


def test_floor_tiles_have_no_foreground_and_walls_block():
    data = g.map_bytes(MAZE.floor)
    for y in range(g.SIZE):
        for x in range(g.SIZE):
            bg, lf, rf = struct.unpack_from("<hhh", data, (y * g.SIZE + x) * 6)
            if MAZE.floor[y][x]:
                assert (lf, rf) == (0, 0) and bg in g.FLOOR
            else:
                assert lf or rf, f"wall ({x}, {y}) has no foreground, so it would not block"


def test_outer_back_walls_use_the_mansion_wallpaper():
    assert g.tile_values(MAZE.floor, 4, 0) == (0, g.NORTH_WALLPAPER[1], 0)
    assert g.tile_values(MAZE.floor, 0, 5) == (0, 0, g.WEST_WALLPAPER[2])


def test_every_inside_face_toward_floor_is_a_mirror():
    covered = set()
    for s in MAZE.segments:
        for i in range(s["length"]):
            covered.add((s["side"], s["x"] + i, s["y"]) if s["side"] == "north" else (s["side"], s["x"], s["y"] + i))
    for y in range(1, g.SIZE):
        for x in range(1, g.SIZE):
            if MAZE.floor[y][x]:
                continue
            if y + 1 < g.SIZE and MAZE.floor[y + 1][x]:
                assert ("north", x, y) in covered
            if x + 1 < g.SIZE and MAZE.floor[y][x + 1]:
                assert ("west", x, y) in covered


def test_each_window_zone_has_a_pair_and_every_style_appears():
    styles = {s["style"] for s in MAZE.segments}
    assert styles == {"glass", "funhouse", "haunted", "endless", "window"}
    window_zones = {s["_zone"] for s in MAZE.segments if s["style"] == "window"}
    assert window_zones == set(g.WINDOW_ZONES)


def test_funhouse_runs_name_their_kind():
    for s in MAZE.segments:
        assert (s["style"] == "funhouse") == ("funhouse" in s)
        if s["style"] == "funhouse":
            assert s["funhouse"] in g.FUNHOUSE_KINDS


def test_the_heart_is_only_reached_through_its_mirror():
    reach = g.reachable(MAZE.floor, g.ARRIVAL)
    heart = g.reachable(MAZE.floor, g.HEART_ARRIVAL)
    assert not reach & heart
    assert g.HEART_DOOR in reach and g.CHEST in heart and g.HEART_EXIT_DOOR in heart


def test_the_doors_face_up_into_mirrors():
    doors = {r["source"]: r["scriptVars"]["mirrorDoor"] for r in MAZE.reactors}
    for tile in (g.LOBBY_EXIT_DOOR, g.HEART_DOOR, g.HEART_EXIT_DOOR):
        door = doors[f"({tile[0]}, {tile[1]})"]
        assert door["facing"] == "Up"
        assert not MAZE.floor[tile[1] - 1][tile[0]], "the tile north of a door must be the mirror's wall"
    assert doors[f"({g.HEART_DOOR[0]}, {g.HEART_DOOR[1]})"].get("heartEntrance") is True


def test_one_wisp_per_wing_and_a_chest():
    keys = sorted(m["merchantTemplateKey"] for m in MAZE.merchants)
    assert keys == ["mirrormaze_chest", "mirrormaze_wisp_endless", "mirrormaze_wisp_funhouse", "mirrormaze_wisp_haunted"]
    for wing, (x, y) in MAZE.wisps.items():
        assert g.tile_zone(x, y) == wing


def test_the_build_is_repeatable():
    again = g.build()
    assert g.map_bytes(again.floor) == g.map_bytes(MAZE.floor)
    assert g.mirrors_json(again) == g.mirrors_json(MAZE)


def test_mirrors_json_drops_private_keys():
    for s in g.mirrors_json(MAZE)["segments"]:
        assert not any(k.startswith("_") for k in s)
```

- [ ] **Step 3: Write the generator**

Create `UNORA/Tools/MirrorMaze/generate_maze.py`:

```python
"""Generate the Halloween mirror maze: the map file, its template, and the instance's mirrors, reactors and merchants.
Run from the Unora repo root:

    python Tools/MirrorMaze/generate_maze.py           # write every file
    python Tools/MirrorMaze/generate_maze.py --check   # build in memory, check, print the summary, write nothing

The maze is a grid of 19x19 cells. Each cell is 2x2 floor tiles with a 1-tile wall line on its north and west, so the
map is 58x58. Zones follow plan B of the spec: a glass lobby at the top, the funhouse wing on the left, the endless
wing on the right, the haunted wing along the bottom, and the walled-in heart in the middle, reached only through the
mirror door on its south wall (from the haunted wing).

Every face of an inside wall that faces open floor becomes a mirror run in mirrors.json, styled by the zone in front of
it. The outer north and west walls keep the Macabre Mansion wallpaper; only the lobby's exit mirror sits on them.
"""
from __future__ import annotations

import argparse
import json
import random
import struct
import sys
from collections import deque
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = ROOT / "Data" / "Configuration"
INSTANCE_DIR = CONFIG / "MapInstances" / "Temuair" / "Events" / "Halloween" / "Mirror_Maze"

MAP_ID = 10231
INSTANCE_ID = "suomi_mirror_maze"
CELLS = 19
SIZE = CELLS * 3 + 1  # 58
SEED = 31

FLOOR = (11056, 11462)  # Macabre Mansion checker, by (x + y) % 2
NORTH_WALLPAPER = (10903, 10904, 10905)  # outer north wall, left foreground slot, by x % 3
WEST_WALLPAPER = (10888, 10887, 10886)  # outer west wall, right foreground slot, by y % 3
BLOCK = 1  # foreground 1: a wall in sotp.dat that is never drawn
WALL_FLOOR = 11507  # dark mansion floor under inside walls, so the layout shows in the map editor

GLASS, FUN, ENDLESS, HAUNTED, HEART = "glass", "funhouse", "endless", "haunted", "heart"
LOOPS = {GLASS: 0.35, FUN: 0.12, ENDLESS: 0.12, HAUNTED: 0.10}
ZONE_LINKS = [(GLASS, FUN), (GLASS, ENDLESS), (FUN, HAUNTED), (ENDLESS, HAUNTED)]
CODES = {GLASS: "lob", FUN: "fun", ENDLESS: "end", HAUNTED: "hau", HEART: "hrt"}
WINDOW_ZONES = (GLASS, FUN, ENDLESS)
FUNHOUSE_KINDS = ("tall", "wide", "wave")

# straight corridors forced open in the haunted wing; each becomes a dark stretch: (row, first cell, last cell)
DARK_CORRIDORS = ((16, 1, 6), (14, 12, 17))

# doors and arrivals (tiles)
ARRIVAL = (29, 2)                        # where the theatre's mirror lands you, in the lobby's top-centre cell
LOBBY_EXIT_DOOR = (28, 1)                # in front of the exit mirror on the outer north wall (28-29, 0)
HEART_DOOR = (28, 37)                    # in front of the heart's south wall (28-29, 36), in the haunted wing
HEART_ARRIVAL = (29, 34)
HEART_EXIT_DOOR = (28, 22)               # in front of the heart's north wall (28-29, 21), inside the heart
HEART_EXIT_ARRIVAL = (29, 19)            # lobby cell (9, 6)
CHEST = (28, 28)
THEATRE_RETURN = "suomi_theatre:(6, 4)"


def zone(cx: int, cy: int) -> str:
    if 7 <= cx <= 11 and 7 <= cy <= 11:
        return HEART
    if cy <= 6 and 4 <= cx <= 14:
        return GLASS
    if cy >= 12:
        return HAUNTED
    if cx <= 6:
        return FUN
    return ENDLESS


def tile_zone(x: int, y: int) -> str:
    """The zone of a floor tile. Opening tiles on a wall line belong to the cell west or north of them."""
    return zone(max((x - 1) // 3, 0), max((y - 1) // 3, 0))


def neighbours(cx: int, cy: int):
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nx, ny = cx + dx, cy + dy
        if 0 <= nx < CELLS and 0 <= ny < CELLS:
            yield nx, ny


def edge(a, b):
    return (a, b) if a < b else (b, a)


@dataclass
class Maze:
    floor: list[list[bool]]                      # floor[y][x]
    opened: set
    segments: list[dict]
    stretches: list[dict]
    reactors: list[dict]
    merchants: list[dict]
    wisps: dict[str, tuple[int, int]]
    problems: list[str] = field(default_factory=list)


def carve(rng: random.Random) -> set:
    cells_by_zone: dict[str, list] = {}
    for cy in range(CELLS):
        for cx in range(CELLS):
            cells_by_zone.setdefault(zone(cx, cy), []).append((cx, cy))

    opened: set = set()
    for z, cells in cells_by_zone.items():
        if z == HEART:  # one open room
            for c in cells:
                for n in neighbours(*c):
                    if zone(*n) == HEART:
                        opened.add(edge(c, n))
            continue
        seen = {cells[0]}
        stack = [cells[0]]
        while stack:
            c = stack[-1]
            options = [n for n in neighbours(*c) if zone(*n) == z and n not in seen]
            if not options:
                stack.pop()
                continue
            n = rng.choice(options)
            opened.add(edge(c, n))
            seen.add(n)
            stack.append(n)
        inner = sorted({edge(c, n) for c in cells for n in neighbours(*c) if zone(*n) == z} - opened)
        for e in rng.sample(inner, int(len(inner) * LOOPS[z])):
            opened.add(e)

    for row, first, last in DARK_CORRIDORS:
        for cx in range(first, last):
            opened.add(edge((cx, row), (cx + 1, row)))

    for a, b in ZONE_LINKS:
        pairs = sorted(edge(c, n) for c in cells_by_zone[a] for n in neighbours(*c) if zone(*n) == b)
        opened.add(pairs[len(pairs) // 2])
    return opened


def build_floor(opened: set) -> list[list[bool]]:
    floor = [[False] * SIZE for _ in range(SIZE)]
    for cy in range(CELLS):
        for cx in range(CELLS):
            for dy in (1, 2):
                for dx in (1, 2):
                    floor[3 * cy + dy][3 * cx + dx] = True
    for (ax, ay), (bx, by) in opened:
        if ay == by:  # east-west: a is the west cell
            for dy in (1, 2):
                floor[3 * ay + dy][3 * ax + 3] = True
        else:  # north-south: a is the north cell
            for dx in (1, 2):
                floor[3 * ay + 3][3 * ax + dx] = True
    return floor


def tile_values(floor, x: int, y: int) -> tuple[int, int, int]:
    if floor[y][x]:
        return FLOOR[(x + y) % 2], 0, 0
    if x == 0 and y == 0:
        return 0, BLOCK, 0
    if y == 0:
        return 0, NORTH_WALLPAPER[x % 3], 0
    if x == 0:
        return 0, 0, WEST_WALLPAPER[y % 3]
    lf = BLOCK if y + 1 < SIZE and floor[y + 1][x] else 0
    rf = BLOCK if x + 1 < SIZE and floor[y][x + 1] else 0
    if not lf and not rf:
        lf = BLOCK
    return WALL_FLOOR, lf, rf


def map_bytes(floor) -> bytes:
    out = bytearray()
    for y in range(SIZE):
        for x in range(SIZE):
            out += struct.pack("<hhh", *tile_values(floor, x, y))
    return bytes(out)


def face_runs(floor) -> list[dict]:
    """Every inside-wall face that faces open floor, grouped into runs of one side and one zone."""
    north: dict[tuple[int, str], list[int]] = {}
    west: dict[tuple[int, str], list[int]] = {}
    for y in range(1, SIZE):
        for x in range(1, SIZE):
            if floor[y][x]:
                continue
            if y + 1 < SIZE and floor[y + 1][x]:
                north.setdefault((y, tile_zone(x, y + 1)), []).append(x)
            if x + 1 < SIZE and floor[y][x + 1]:
                west.setdefault((x, tile_zone(x + 1, y)), []).append(y)

    runs = []
    for (y, z), xs in sorted(north.items()):
        for start, length in _consecutive(sorted(xs)):
            runs.append({"side": "north", "x": start, "y": y, "length": length, "zone": z})
    for (x, z), ys in sorted(west.items()):
        for start, length in _consecutive(sorted(ys)):
            runs.append({"side": "west", "x": x, "y": start, "length": length, "zone": z})
    return runs


def _consecutive(values):
    start = prev = values[0]
    for v in values[1:]:
        if v != prev + 1:
            yield start, prev - start + 1
            start = v
        prev = v
    yield start, prev - start + 1


def style_runs(runs: list[dict]) -> list[dict]:
    counters: dict[str, int] = {}
    segments = []
    for run in runs:
        z = run["zone"]
        n = counters.get(z, 0)
        counters[z] = n + 1
        seg = {"id": f"{CODES[z]}-{run['side'][0]}-{run['x']}-{run['y']}", "x": run["x"], "y": run["y"],
               "side": run["side"], "length": run["length"]}
        if z == GLASS:
            seg["style"] = "glass"
        elif z == FUN:
            seg["style"] = "funhouse"
            seg["funhouse"] = FUNHOUSE_KINDS[n % 3]
        elif z == ENDLESS:
            seg["style"] = "endless"
        elif z == HAUNTED:
            seg["style"] = "haunted"
        elif run["side"] == "north":  # the heart: endless on its north wall, a waving funhouse on its west
            seg["style"] = "endless"
        else:
            seg["style"] = "funhouse"
            seg["funhouse"] = "wave"
        seg["_zone"] = z
        segments.append(seg)
    return segments


def pair_windows(segments: list[dict]) -> int:
    """Turns one pair of parallel north runs per window zone into window mirrors. Returns how many pairs."""
    pairs = 0
    for z in WINDOW_ZONES:
        candidates = [s for s in segments if s["_zone"] == z and s["side"] == "north" and s["length"] >= 2
                      and s["style"] != "window"]
        done = False
        for a in candidates:
            for b in candidates:
                if b is a or done:
                    continue
                if a["x"] == b["x"] and a["length"] == b["length"] and b["y"] - a["y"] in (3, 6):
                    for s, partner in ((a, b), (b, a)):
                        s["style"] = "window"
                        s.pop("funhouse", None)
                        s["partner"] = partner["id"]
                    pairs += 1
                    done = True
            if done:
                break
    return pairs


def dark_stretches() -> list[dict]:
    return [{"x": 3 * first + 1, "y": 3 * row + 1, "width": 3 * (last - first) + 2, "height": 2}
            for row, first, last in DARK_CORRIDORS]


def farthest_cell(opened: set, z: str, starts: list) -> tuple[int, int]:
    dist = {s: 0 for s in starts}
    queue = deque(starts)
    while queue:
        c = queue.popleft()
        for n in neighbours(*c):
            if zone(*n) == z and n not in dist and edge(c, n) in opened:
                dist[n] = dist[c] + 1
                queue.append(n)
    return max(sorted(dist), key=lambda c: dist[c])


def zone_entrances(opened: set, z: str) -> list:
    """The cells of zone z that sit on an opening to another zone."""
    out = []
    for a, b in sorted(opened):
        za, zb = zone(*a), zone(*b)
        if za != zb:
            if za == z:
                out.append(a)
            if zb == z:
                out.append(b)
    return out


def reachable(floor, start) -> set:
    seen = {start}
    queue = deque([start])
    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < SIZE and 0 <= ny < SIZE and floor[ny][nx] and (nx, ny) not in seen:
                seen.add((nx, ny))
                queue.append((nx, ny))
    return seen


def reactor(source, facing: str, destination: str, **flags) -> dict:
    vars_ = {"facing": facing, "destination": destination}
    vars_.update(flags)
    return {"scriptKeys": ["mirrorDoor"], "scriptVars": {"mirrorDoor": vars_}, "shouldBlockPathfinding": True,
            "source": f"({source[0]}, {source[1]})"}


def merchant(template: str, at) -> dict:
    return {"blackList": [], "direction": "Down", "extraScriptKeys": [], "merchantTemplateKey": template,
            "spawnPoint": f"({at[0]}, {at[1]})"}


def check(maze: Maze) -> list[str]:
    """The server's mirrors.json rules plus the maze's own: every place a player needs is reachable, the heart isn't."""
    problems = []
    floor = maze.floor
    ids = set()
    for s in maze.segments:
        if s["id"].lower() in ids:
            problems.append(f"duplicate id {s['id']}")
        ids.add(s["id"].lower())
        for i in range(s["length"]):
            wx, wy = (s["x"] + i, s["y"]) if s["side"] == "north" else (s["x"], s["y"] + i)
            fx, fy = (wx, wy + 1) if s["side"] == "north" else (wx + 1, wy)
            if not (0 <= wx < SIZE and 0 <= wy < SIZE and 0 <= fx < SIZE and 0 <= fy < SIZE):
                problems.append(f"{s['id']}: off the map")
            elif floor[wy][wx] or not floor[fy][fx]:
                problems.append(f"{s['id']}: ({wx}, {wy}) must be a wall with floor in front")
    by_id = {s["id"]: s for s in maze.segments}
    for s in maze.segments:
        if s["style"] == "window":
            p = by_id.get(s.get("partner"))
            if (p is None or p.get("partner") != s["id"] or p["side"] != s["side"] or p["length"] != s["length"]
                    or max(abs(p["x"] - s["x"]), abs(p["y"] - s["y"])) > 10):
                problems.append(f"{s['id']}: broken window pair")
    for st in maze.stretches:
        for y in range(st["y"], st["y"] + st["height"]):
            for x in range(st["x"], st["x"] + st["width"]):
                if not floor[y][x] or tile_zone(x, y) != HAUNTED:
                    problems.append(f"stretch at ({st['x']}, {st['y']}) covers ({x}, {y}), not haunted floor")
    reach = reachable(floor, ARRIVAL)
    for name, at in [("lobby exit door", LOBBY_EXIT_DOOR), ("heart door", HEART_DOOR),
                     *((f"{w} wisp", p) for w, p in maze.wisps.items())]:
        if at not in reach:
            problems.append(f"{name} at {at} cannot be reached from the arrival")
    heart = reachable(floor, HEART_ARRIVAL)
    if heart & reach:
        problems.append("the heart can be walked into without its mirror door")
    for at in (CHEST, HEART_EXIT_DOOR):
        if at not in heart:
            problems.append(f"{at} is not inside the heart")
    if not any(s["style"] == "window" for s in maze.segments):
        problems.append("no window pairs")
    return problems


def build(seed: int = SEED) -> Maze:
    rng = random.Random(seed)
    opened = carve(rng)
    floor = build_floor(opened)
    segments = style_runs(face_runs(floor))
    pair_windows(segments)
    segments.append({"id": "lobby-exit", "x": LOBBY_EXIT_DOOR[0], "y": 0, "side": "north", "length": 2,
                     "style": "glass", "_zone": GLASS})

    wisps = {
        FUN: farthest_cell(opened, FUN, zone_entrances(opened, FUN)),
        ENDLESS: farthest_cell(opened, ENDLESS, zone_entrances(opened, ENDLESS)),
        HAUNTED: farthest_cell(opened, HAUNTED, zone_entrances(opened, HAUNTED) + [(9, 12)]),
    }
    wisp_tiles = {w: (3 * cx + 1, 3 * cy + 1) for w, (cx, cy) in wisps.items()}

    reactors = [
        reactor(LOBBY_EXIT_DOOR, "Up", THEATRE_RETURN),
        reactor(HEART_DOOR, "Up", f"{INSTANCE_ID}:({HEART_ARRIVAL[0]}, {HEART_ARRIVAL[1]})", heartEntrance=True),
        reactor(HEART_EXIT_DOOR, "Up", f"{INSTANCE_ID}:({HEART_EXIT_ARRIVAL[0]}, {HEART_EXIT_ARRIVAL[1]})"),
    ]
    merchants = [merchant(f"mirrormaze_wisp_{w}", at) for w, at in wisp_tiles.items()]
    merchants.append(merchant("mirrormaze_chest", CHEST))

    maze = Maze(floor, opened, segments, dark_stretches(), reactors, merchants, wisp_tiles)
    maze.problems = check(maze)
    return maze


def mirrors_json(maze: Maze) -> dict:
    return {"segments": [{k: v for k, v in s.items() if not k.startswith("_")} for s in maze.segments],
            "darkStretches": maze.stretches}


def write(maze: Maze) -> None:
    (CONFIG / "MapData" / f"lod{MAP_ID}.map").write_bytes(map_bytes(maze.floor))
    template = {"height": SIZE, "scriptKeys": [], "templateKey": str(MAP_ID), "width": SIZE}
    (CONFIG / "Templates" / "Maps" / f"{MAP_ID}.json").write_text(json.dumps(template, indent=2) + "\n", encoding="utf-8")
    INSTANCE_DIR.mkdir(parents=True, exist_ok=True)
    (INSTANCE_DIR / "mirrors.json").write_text(json.dumps(mirrors_json(maze), indent=2) + "\n", encoding="utf-8")
    (INSTANCE_DIR / "reactors.json").write_text(json.dumps(maze.reactors, indent=2) + "\n", encoding="utf-8")
    (INSTANCE_DIR / "merchants.json").write_text(json.dumps(maze.merchants, indent=2) + "\n", encoding="utf-8")


def summary(maze: Maze) -> str:
    styles: dict[str, int] = {}
    for s in maze.segments:
        styles[s["style"]] = styles.get(s["style"], 0) + 1
    lines = [f"map {MAP_ID}: {SIZE}x{SIZE}, {sum(map(sum, maze.floor))} floor tiles",
             f"mirror runs: {len(maze.segments)} ({', '.join(f'{k} {v}' for k, v in sorted(styles.items()))})",
             f"window pairs: {sum(1 for s in maze.segments if s['style'] == 'window') // 2}",
             f"dark stretches: {len(maze.stretches)}",
             f"wisps: {maze.wisps}"]
    lines += [f"PROBLEM: {p}" for p in maze.problems]
    return "\n".join(lines)


PLAN_COLOURS = {GLASS: (127, 166, 255), FUN: (255, 122, 217), ENDLESS: (143, 220, 255), HAUNTED: (125, 255, 168),
                HEART: (255, 204, 85), "window": (255, 255, 255)}


def plan_image(maze: Maze, scale: int = 10):
    """A top-down plan: zone-coloured floor, black walls, mirror faces as bright edges, doors (D), wisps (W),
    the chest (C) and the arrival (A); dark stretches are shaded."""
    from PIL import Image, ImageDraw
    im = Image.new("RGB", (SIZE * scale, SIZE * scale), (12, 10, 20))
    d = ImageDraw.Draw(im)
    for y in range(SIZE):
        for x in range(SIZE):
            if maze.floor[y][x]:
                r, g, b = PLAN_COLOURS[tile_zone(x, y)]
                d.rectangle((x * scale, y * scale, x * scale + scale - 1, y * scale + scale - 1), fill=(r // 3, g // 3, b // 3))
    for st in maze.stretches:
        d.rectangle((st["x"] * scale, st["y"] * scale, (st["x"] + st["width"]) * scale - 1, (st["y"] + st["height"]) * scale - 1),
                    fill=(5, 5, 8))
    for s in maze.segments:
        colour = PLAN_COLOURS["window"] if s["style"] == "window" else PLAN_COLOURS[s["_zone"]]
        for i in range(s["length"]):
            wx, wy = (s["x"] + i, s["y"]) if s["side"] == "north" else (s["x"], s["y"] + i)
            if s["side"] == "north":  # the face is the wall tile's south edge
                d.line((wx * scale, (wy + 1) * scale - 1, (wx + 1) * scale - 1, (wy + 1) * scale - 1), fill=colour, width=2)
            else:  # the face is the wall tile's east edge
                d.line(((wx + 1) * scale - 1, wy * scale, (wx + 1) * scale - 1, (wy + 1) * scale - 1), fill=colour, width=2)
    marks = [("A", ARRIVAL), ("D", LOBBY_EXIT_DOOR), ("D", HEART_DOOR), ("D", HEART_EXIT_DOOR), ("C", CHEST),
             *(("W", at) for at in maze.wisps.values())]
    for text, (x, y) in marks:
        d.text((x * scale + 2, y * scale - 1), text, fill=(255, 255, 255))
    return im


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="build and check in memory; write nothing")
    ap.add_argument("--seed", type=int, default=SEED)
    ap.add_argument("--plan", type=Path, help="also save a top-down plan PNG here")
    args = ap.parse_args()
    maze = build(args.seed)
    print(summary(maze))
    if args.plan:
        plan_image(maze).save(args.plan)
        print(f"plan: {args.plan}")
    if maze.problems:
        return 1
    if not args.check:
        write(maze)
        print(f"wrote lod{MAP_ID}.map, Templates/Maps/{MAP_ID}.json and {INSTANCE_DIR.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 4: Run the tests**

Run: the Verify command. Expected: `12 passed`.

- [ ] **Step 5: Generate the files**

Run from UNORA: `python Tools/MirrorMaze/generate_maze.py`
Expected: the summary above, then `wrote lod10231.map, Templates/Maps/10231.json and Data\Configuration\MapInstances\Temuair\Events\Halloween\Mirror_Maze`.

Then run `python Tools/MirrorMaze/generate_maze.py --check --plan "C:/Users/Michael/AppData/Local/Temp/mirror-maze-plan.png"` and look at the image: a blue lobby at the top, pink funhouse left, cyan endless right, green haunted bottom with two black dark stretches, a gold heart in the middle with a 4x4 grid of pillars, white window edges, and the letters A, D (3), W (3) and C.

```json:metadata
{"files": ["Tools/MirrorMaze/generate_maze.py", "Tools/MirrorMaze/test_generate_maze.py", "Data/Configuration/MapData/lod10231.map", "Data/Configuration/Templates/Maps/10231.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Mirror_Maze/mirrors.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Mirror_Maze/reactors.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Mirror_Maze/merchants.json"], "verifyCommand": "python -m pytest Tools/MirrorMaze/test_generate_maze.py -q", "acceptanceCriteria": ["12 generator tests pass", "generator prints no PROBLEM lines, 3 window pairs, 2 dark stretches, writes five files", "lod10231.map is 20184 bytes", "plan image shows lobby, wings and heart"], "modelTier": "mechanical"}
```

---

### Task 8: Maze and theatre content

**Goal:** The maze instance, its NPC and item templates, dialogs, the Mirror Walker emblem, Thulin's trade option, and the theatre's mirror door all exist as Unora data.

**Files:**
- Create: `UNORA/Data/Configuration/MapInstances/Temuair/Events/Halloween/Mirror_Maze/instance.json`, `monsters.json`
- Create: `UNORA/Data/Configuration/Templates/Merchants/Temauir/MirrorMaze/mirrormaze_wisp_funhouse.json`, `mirrormaze_wisp_endless.json`, `mirrormaze_wisp_haunted.json`, `mirrormaze_chest.json`
- Create: `UNORA/Data/Configuration/Templates/Items/Events/Halloween/halloweencandy.json`
- Create: `UNORA/Data/Configuration/Templates/Dialogs/Temauir/Events/Halloween/MirrorMaze/mirrordoor_step.json`
- Create: `UNORA/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/thulin_tradecandy.json`
- Create: `UNORA/Data/Configuration/Templates/Emblems/mirrorwalker.json`
- Create: `UNORA/Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/mirrors.json`
- Modify: `UNORA/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/thulin_initial.json` (script key)
- Modify: `UNORA/Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/instance.json` (script key)
- Modify: `UNORA/Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/reactors.json` (the theatre's mirror door)

**Acceptance Criteria:**
- [ ] Every new and changed JSON file parses
- [ ] `suomi_mirror_maze` uses template `10231` and the script keys `eventtiming` and `mirrormap`
- [ ] `suomi_theatre` has the `mirrormap` script key, a one-run glass `mirrors.json` on wall (5, 2), and a mirror door at (5, 3) facing Up to `suomi_mirror_maze:(29, 2)` with `halloweenOnly`
- [ ] `thulin_initial` has the `thulinCandyTrade` script key
- [ ] The emblem uses art 256 and legend mark `mirrormaze`

**Verify:** from UNORA: `python -c "import json,glob;[json.load(open(f,encoding='utf-8')) for f in glob.glob('Data/Configuration/**/*.json',recursive=True)]"` → no error

**Steps:**

- [ ] **Step 1: The maze instance**

Create `Mirror_Maze/instance.json`:

```json
{
  "flags": "None",
  "instanceId": "suomi_mirror_maze",
  "music": 1,
  "name": "Mirror Maze",
  "scriptKeys": [
    "eventtiming",
    "mirrormap"
  ],
  "templateKey": "10231"
}
```

Create `Mirror_Maze/monsters.json` with the content `[]`.

- [ ] **Step 2: The wisps and the chest**

Create `Templates/Merchants/Temauir/MirrorMaze/mirrormaze_wisp_funhouse.json`:

```json
{
  "itemsForSale": [],
  "itemsToBuy": [],
  "name": "Funhouse Wisp",
  "restockIntervalHrs": 24,
  "restockPct": 100,
  "scriptKeys": [
    "mirrorMazeWisp"
  ],
  "scriptVars": {
    "mirrorMazeWisp": {
      "wing": "Funhouse"
    }
  },
  "skillsToTeach": [],
  "spellsToTeach": [],
  "sprite": 179,
  "templateKey": "mirrormaze_wisp_funhouse"
}
```

Create `mirrormaze_wisp_endless.json` and `mirrormaze_wisp_haunted.json` the same way, with `"name": "Endless Wisp"` / `"Haunted Wisp"`, `"wing": "Endless"` / `"Haunted"`, and `"templateKey": "mirrormaze_wisp_endless"` / `"mirrormaze_wisp_haunted"`.

Create `mirrormaze_chest.json`:

```json
{
  "itemsForSale": [],
  "itemsToBuy": [],
  "name": " ",
  "restockIntervalHrs": 24,
  "restockPct": 100,
  "scriptKeys": [
    "mirrorMazeChest"
  ],
  "scriptVars": {},
  "skillsToTeach": [],
  "spellsToTeach": [],
  "sprite": 456,
  "templateKey": "mirrormaze_chest"
}
```

- [ ] **Step 3: The candy**

Create `Templates/Items/Events/Halloween/halloweencandy.json`:

```json
{
  "category": "Event",
  "color": "Default",
  "maxStacks": 1000,
  "modifiers": {},
  "description": "Sweets from the Mirror Maze. Thulin in Suomi trades 100 for a Macabre Box.",
  "level": 1,
  "name": "Halloween Candy",
  "panelSprite": 4358,
  "scriptKeys": [
    "pickupNotifier"
  ],
  "scriptVars": {},
  "templateKey": "halloweencandy"
}
```

- [ ] **Step 4: The dialogs**

Create `Templates/Dialogs/Temauir/Events/Halloween/MirrorMaze/mirrordoor_step.json`:

```json
{
  "options": [
    {
      "dialogKey": "Close",
      "optionText": "Step through"
    },
    {
      "dialogKey": "Close",
      "optionText": "Stay"
    }
  ],
  "scriptKeys": [
    "mirrorDoorDialog"
  ],
  "scriptVars": {},
  "templateKey": "mirrordoor_step",
  "text": "Your reflection beckons. Step through the glass?",
  "type": "DialogMenu"
}
```

Create `Templates/Dialogs/Temauir/suomi/thulin/thulin_tradecandy.json`:

```json
{
  "options": [
    {
      "dialogKey": "Close",
      "optionText": "Trade"
    },
    {
      "dialogKey": "Close",
      "optionText": "Not now"
    }
  ],
  "scriptKeys": [
    "thulinCandyTrade"
  ],
  "scriptVars": {},
  "templateKey": "thulin_tradecandy",
  "text": "100 pieces of candy for a Macabre Box?",
  "type": "DialogMenu"
}
```

In `thulin_initial.json`, add `"thulinCandyTrade"` as the last entry of `scriptKeys` (after `"QuestDialog"`).

These follow `suomitheatre_jumponstage.json`, whose options both use `"Close"` while its script acts on the option index. If, when testing in game, choosing "Step through" closes the dialog without warping, the `Close` key skips `OnNext`: then give option 1 a `dialogKey` of the dialog's own template key and keep the script's `Subject.Close` calls.

- [ ] **Step 5: The emblem**

Create `Templates/Emblems/mirrorwalker.json`:

```json
{
  "key": "mirrorwalker",
  "art": 256,
  "name": "Mirror Walker",
  "description": "Saw through the Halloween Mirror Maze.",
  "source": { "legendMark": "mirrormaze" }
}
```

- [ ] **Step 6: The theatre's mirror door**

In `suomi_theatre/instance.json`, change `"scriptKeys": ["suomitheatremap"]` to `"scriptKeys": ["suomitheatremap", "mirrormap"]`.

Create `suomi_theatre/mirrors.json`:

```json
{
  "segments": [
    { "id": "theatre-mirror", "x": 5, "y": 2, "side": "north", "length": 1, "style": "glass" }
  ],
  "darkStretches": []
}
```

Append this object to the array in `suomi_theatre/reactors.json`:

```json
  {
    "scriptKeys": [
      "mirrorDoor"
    ],
    "scriptVars": {
      "mirrorDoor": {
        "facing": "Up",
        "destination": "suomi_mirror_maze:(29, 2)",
        "halloweenOnly": true
      }
    },
    "shouldBlockPathfinding": true,
    "source": "(5, 3)"
  }
```

Wall (5, 2) has foreground `7932` on its left (north) face and (5, 3) is open floor (checked against `lod346.map` on 2026-09-30). Keep the file's existing formatting.

- [ ] **Step 7: Check every file parses**

Run: the Verify command. Expected: no output.

```json:metadata
{"files": ["Data/Configuration/MapInstances/Temuair/Events/Halloween/Mirror_Maze/instance.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Mirror_Maze/monsters.json", "Data/Configuration/Templates/Merchants/Temauir/MirrorMaze/mirrormaze_wisp_funhouse.json", "Data/Configuration/Templates/Merchants/Temauir/MirrorMaze/mirrormaze_wisp_endless.json", "Data/Configuration/Templates/Merchants/Temauir/MirrorMaze/mirrormaze_wisp_haunted.json", "Data/Configuration/Templates/Merchants/Temauir/MirrorMaze/mirrormaze_chest.json", "Data/Configuration/Templates/Items/Events/Halloween/halloweencandy.json", "Data/Configuration/Templates/Dialogs/Temauir/Events/Halloween/MirrorMaze/mirrordoor_step.json", "Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/thulin_tradecandy.json", "Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/thulin_initial.json", "Data/Configuration/Templates/Emblems/mirrorwalker.json", "Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/instance.json", "Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/mirrors.json", "Data/Configuration/MapInstances/Temuair/Towns/Suomi/suomi_theatre/reactors.json"], "verifyCommand": "python -c \"import json,glob;[json.load(open(f,encoding='utf-8')) for f in glob.glob('Data/Configuration/**/*.json',recursive=True)]\"", "acceptanceCriteria": ["all JSON parses", "maze instance uses 10231 with eventtiming and mirrormap", "theatre has mirrormap, a glass mirrors.json at (5,2) and a halloweenOnly mirror door at (5,3)", "thulin_initial has thulinCandyTrade", "emblem art 256 from legend mark mirrormaze"], "modelTier": "mechanical"}
```

---

### Task 9: Mirror art, packed and approved

**Goal:** The two panel frame sprites and the Mirror Walker emblem are drawn, previewed on a render of the real maze, approved by the user, and packed into a `setoa.dat` batch on the Desktop.

> **USER-ORDERED GATE — NON-SKIPPABLE.** This task was requested by the user in the current conversation. It MUST NOT be closed by walking around it, by declaring it "verified inline", or by substituting a cheaper check. Close only after every item in `acceptanceCriteria` has been re-validated independently, with output captured.

**Files:**
- Create: `UNORA/Tools/MirrorMaze/make_art.py`
- Create: `UNORA/Tools/MirrorMaze/preview_panels.py`
- Create: `UNORA/Tools/MirrorMaze/render_map.cs`
- Create: `UNORA/Tools/MirrorMaze/build_art.py`
- Create (generated): `UNORA/Tools/MirrorMaze/art/mirpnl01.png`, `art/mirpnl02.png`, `UNORA/Tools/Emblems/art/custom/embl256.png`

**Acceptance Criteria:**
- [ ] `make_art.py` writes the three PNGs: the frames are 28x89 with a transparent glass hole; the emblem is 11x11
- [ ] `preview_panels.py` writes a lobby preview showing framed panels on every inside face
- [ ] The user has seen the preview and the emblem and approved them; the approval is quoted in the task close
- [ ] `build_art.py --no-apply` prints `done: <Desktop folder>`, and that folder holds `setoa.dat` and `manifest.txt` naming `mirpnl01.spf`, `mirpnl02.spf` and `embl256.spf`

**Verify:** from UNORA: `python Tools/MirrorMaze/build_art.py --no-apply` → `done: C:\Users\Michael\Desktop\Mirror maze art batch <stamp>`

**Steps:**

These scripts were written and run during planning: `build_art.py` (pointed at the live client folder, with `--no-apply --out` to a scratch folder) produced a 29.7 MB `setoa.dat` that re-read cleanly.

- [ ] **Step 1: Write the art script**

Create `UNORA/Tools/MirrorMaze/make_art.py`:

```python
"""Draw the mirror maze's art. Run from the Unora repo root:

    python Tools/MirrorMaze/make_art.py

Writes Tools/MirrorMaze/art/mirpnl01.png (the frame of a north face), art/mirpnl02.png (a west face) and
Tools/Emblems/art/custom/embl256.png (the Mirror Walker emblem).

Face geometry matches the client's MirrorMath and the plan's Global Constraints: a face is 28 px wide; its base row
is 13 + u // 2 (north, column u) or 26 - v // 2 (west, column v) in tile-local pixels; a panel is 76 rows tall; the
glass is columns 3-24, rows base-69 to base-6. The canvas is 28x89 and sits at tile-local y -62, so canvas row =
local row + 62. The glass is left transparent: the client draws the glass and its reflections first, then this frame.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ART = HERE / "art"
EMBLEM = HERE.parent / "Emblems" / "art" / "custom" / "embl256.png"

WIDTH, HEIGHT, CANVAS_TOP = 28, 89, -62
PANEL_ROWS, GLASS_LEFT, GLASS_RIGHT, GLASS_TOP, GLASS_BOTTOM = 76, 3, 24, 69, 6

RIM = (24, 16, 12)        # dark top edge
WOOD = (62, 38, 24)
WOOD_LIGHT = (92, 60, 36)
GILT = (176, 138, 70)     # the thin line round the glass
GILT_DARK = (120, 90, 44)
SKIRT = (40, 26, 18)      # bottom band


def base_row(side: str, column: int) -> int:
    """The face's base row in tile-local pixels for a column 0-27 of the face."""
    return 13 + column // 2 if side == "north" else 26 - column // 2


def frame(side: str) -> Image.Image:
    im = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    px = im.load()
    for c in range(WIDTH):
        base = base_row(side, c) - CANVAS_TOP          # canvas row of the base
        top = base - (PANEL_ROWS - 1)
        for r in range(top, base + 1):
            above = base - r                            # 0 at the base, 75 at the top
            in_glass_cols = GLASS_LEFT <= c <= GLASS_RIGHT
            in_glass_rows = GLASS_BOTTOM <= above <= GLASS_TOP
            if in_glass_cols and in_glass_rows:
                continue                                 # transparent: the client draws the glass here
            if above >= PANEL_ROWS - 2:
                colour = RIM
            elif above <= 2:
                colour = SKIRT
            elif in_glass_cols and above in (GLASS_BOTTOM - 1, GLASS_TOP + 1):
                colour = GILT
            elif c in (GLASS_LEFT - 1, GLASS_RIGHT + 1) and GLASS_BOTTOM - 1 <= above <= GLASS_TOP + 1:
                colour = GILT if c == GLASS_LEFT - 1 else GILT_DARK
            elif c in (0, WIDTH - 1):
                colour = RIM
            else:
                colour = WOOD_LIGHT if (c + above // 4) % 7 == 0 else WOOD
            px[c, r] = (*colour, 255)
    return im


def emblem() -> Image.Image:
    rows = [
        "...GGGGG...",
        "..GgbbbgG..",
        ".GgbwbbbgG.",
        ".GbwbbbbbG.",
        ".GbbbbbbbG.",
        ".GbbbpbbbG.",
        ".GbbbbbbbG.",
        ".GgbbbbbgG.",
        "..GgbbbgG..",
        "...GGGGG...",
        "....GGG....",
    ]
    colours = {"G": (196, 152, 64), "g": (120, 90, 44), "b": (96, 112, 168), "w": (232, 240, 255), "p": (168, 255, 200)}
    im = Image.new("RGBA", (11, 11), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in colours:
                im.putpixel((x, y), (*colours[ch], 255))
    return im


def main() -> None:
    ART.mkdir(parents=True, exist_ok=True)
    for name, side in (("mirpnl01.png", "north"), ("mirpnl02.png", "west")):
        frame(side).save(ART / name)
        print(f"wrote {ART / name}")
    EMBLEM.parent.mkdir(parents=True, exist_ok=True)
    emblem().save(EMBLEM)
    print(f"wrote {EMBLEM}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: Write the map renderer and the preview**

Create `UNORA/Tools/MirrorMaze/render_map.cs` (a .NET 10 file-based app; DALib is the sibling repo):

```csharp
#:project C:/Users/Michael/Documents/GitHub/dalib/DALib/DALib.csproj
// Renders a map file with DALib for review.
// usage, from the Unora repo root: dotnet run Tools/MirrorMaze/render_map.cs -- <lod path> <width> <height> <out.png>
// Reads seo.dat and ia.dat from the local client folder below.
using DALib.Data;
using DALib.Drawing;
using SkiaSharp;

var data = @"C:\Users\Michael\Documents\Unora\Unora Files";
var seo = DataArchive.FromFile(Path.Combine(data, "seo.dat"));
var ia = DataArchive.FromFile(Path.Combine(data, "ia.dat"));
var map = MapFile.FromFile(args[0], int.Parse(args[1]), int.Parse(args[2]));
using var img = Graphics.RenderMap(map, seo, ia, 256);
using var png = img.Encode(SKEncodedImageFormat.Png, 100);
File.WriteAllBytes(args[3], png.ToArray());
Console.WriteLine($"{img.Width}x{img.Height} -> {args[3]}");
```

Create `UNORA/Tools/MirrorMaze/preview_panels.py`:

```python
"""Paste the mirror panels onto a DALib render of the maze, the way the client will, for reviewing the art.
Run from the Unora repo root, after make_art.py and after rendering the map with render_map.cs:

    python Tools/MirrorMaze/preview_panels.py <render.png> <out.png> [x0 y0 x1 y1]

<render.png> must be a DALib RenderMap of lod10231 with foregroundPadding 256. The optional tile box crops the
output to that part of the map (default: the lobby). Glass is a plain gradient here; reflections are the client's job.
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import generate_maze as g  # noqa: E402
from make_art import CANVAS_TOP, GLASS_BOTTOM, GLASS_LEFT, GLASS_RIGHT, GLASS_TOP, base_row  # noqa: E402

PAD = 256
STYLE_TINT = {"glass": (58, 70, 96), "funhouse": (74, 58, 96), "haunted": (47, 64, 64), "endless": (52, 66, 100),
              "window": (60, 60, 70)}


def tile_origin(x: int, y: int) -> tuple[int, int]:
    return (g.SIZE - 1 + x - y) * 28, PAD + (x + y) * 14


def main() -> int:
    render, out = Path(sys.argv[1]), Path(sys.argv[2])
    box = tuple(int(v) for v in sys.argv[3:7]) if len(sys.argv) >= 7 else (9, 0, 46, 22)
    im = Image.open(render).convert("RGBA")
    frames = {"north": Image.open(HERE / "art" / "mirpnl01.png"), "west": Image.open(HERE / "art" / "mirpnl02.png")}
    maze = g.build()

    faces = []
    for s in maze.segments:
        for i in range(s["length"]):
            wx, wy = (s["x"] + i, s["y"]) if s["side"] == "north" else (s["x"], s["y"] + i)
            faces.append((wx + wy, wx, wy, s["side"], s["style"]))
    for _, wx, wy, side, style in sorted(faces):
        ox, oy = tile_origin(wx, wy)
        fx = ox + (0 if side == "north" else 28)
        glass = STYLE_TINT[style]
        for c in range(GLASS_LEFT, GLASS_RIGHT + 1):
            base = base_row(side, c)
            for above in range(GLASS_BOTTOM, GLASS_TOP + 1):
                k = above / GLASS_TOP
                colour = tuple(int(v * (0.55 + 0.6 * k)) for v in glass)
                im.putpixel((fx + c, oy + base - above), (*colour, 255))
        frame = frames[side]
        im.alpha_composite(frame, (fx, oy + CANVAS_TOP))

    x0, y0, x1, y1 = box
    left = tile_origin(x0, y1)[0]
    right = tile_origin(x1, y0)[0] + 56
    top = tile_origin(x0, y0)[1] - 120
    bottom = tile_origin(x1, y1)[1] + 27
    im.crop((left, top, right, bottom)).save(out)
    print(f"wrote {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 3: Draw and preview**

Run from UNORA:

```bash
python Tools/MirrorMaze/make_art.py
dotnet run Tools/MirrorMaze/render_map.cs -- Data/Configuration/MapData/lod10231.map 58 58 "C:/Users/Michael/AppData/Local/Temp/mirror-maze-render.png"
python Tools/MirrorMaze/preview_panels.py "C:/Users/Michael/AppData/Local/Temp/mirror-maze-render.png" "C:/Users/Michael/Desktop/mirror-maze-preview.png" 12 0 30 12
```

Expected: three `wrote ...` lines, `3276x1894 -> ...`, and `wrote C:/Users/Michael/Desktop/mirror-maze-preview.png`. Look at the preview: rows of framed panels (dark wood, gilt edge, blue glass in the lobby, purple in the funhouse corner) standing on the inside walls, well below the purple wallpaper of the outer north wall.

- [ ] **Step 4: Get the user's approval**

Show the user `C:/Users/Michael/Desktop/mirror-maze-preview.png` and a 16x enlargement of `Tools/Emblems/art/custom/embl256.png` (`python -c "from PIL import Image; Image.open('Tools/Emblems/art/custom/embl256.png').resize((176,176), Image.NEAREST).save('C:/Users/Michael/Desktop/mirror-walker-emblem.png')"`). If they may be away, follow the companion-on-phone memory: send the images with `SendUserFile`. Ask: "Here are the mirror panel frames on the real maze, and the Mirror Walker emblem. Approve, or what should change?" Change the colours or shapes in `make_art.py` and repeat Step 3 until they approve. Quote their approval in the task close.

- [ ] **Step 5: Write the packer and build the batch**

Create `UNORA/Tools/MirrorMaze/build_art.py`:

```python
"""Pack the mirror maze's art into setoa.dat. Run from the Unora repo root:

    python Tools/MirrorMaze/build_art.py            # batch folder on the desktop + copy into the client folder
    python Tools/MirrorMaze/build_art.py --no-apply # batch folder only

Reads art/mirpnl01.png and art/mirpnl02.png (the panel frames) and ../Emblems/art/custom/embl256.png (the Mirror
Walker emblem), makes one SPF each, writes them into a copy of the client folder's setoa.dat, re-reads the copy to
check every file, and writes manifest.txt. Upload the batch's setoa.dat in the same launcher patch as the client that
draws mirrors.
"""
from __future__ import annotations

import argparse
import shutil
import struct
import sys
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent
sys.path.insert(0, str(TOOLS / "Accessories"))
sys.path.insert(0, str(TOOLS / "Emblems"))
sys.path.insert(0, str(TOOLS / "WorldList"))

from PIL import Image  # noqa: E402

from acclib.daformats import DatArchive, Spf, SpfFrame  # noqa: E402
from acclib.paths import DEFAULT_DATA_DIR  # noqa: E402
from emblem_art import custom_emblem_spf  # noqa: E402

ART = HERE / "art"
EMBLEM_PNG = TOOLS / "Emblems" / "art" / "custom" / "embl256.png"
PANELS = {"mirpnl01.spf": "mirpnl01.png", "mirpnl02.spf": "mirpnl02.png"}


def _rgb565(r: int, g: int, b: int) -> int:
    return ((r * 31 // 255) << 11) | ((g * 63 // 255) << 5) | (b * 31 // 255)


def _rgb555(r: int, g: int, b: int) -> int:
    return ((r * 31 // 255) << 10) | ((g * 31 // 255) << 5) | (b * 31 // 255)


def png_to_spf(image: Image.Image) -> Spf:
    """One palettized frame. Alpha under half is index 0 (transparent); visible pure black is refused."""
    im = image.convert("RGBA")
    colours: dict[tuple[int, int, int], int] = {}
    data = bytearray()
    for r, g, b, a in struct.iter_unpack("4B", im.tobytes()):
        if a < 128:
            data.append(0)
            continue
        if (r, g, b) == (0, 0, 0):
            raise ValueError("visible pure black reads as transparent in the client; use (8, 6, 4)")
        data.append(colours.setdefault((r, g, b), len(colours) + 1))
    if len(colours) > 255:
        raise ValueError(f"{len(colours)} colours; an SPF palette holds 255 plus transparent")
    pad = [0] * (255 - len(colours))
    primary = struct.pack("<256H", 0, *(_rgb565(*c) for c in colours), *pad)
    secondary = struct.pack("<256H", 0, *(_rgb555(*c) for c in colours), *pad)
    frame = SpfFrame(0, 0, im.width, im.height, 0, 0, 0, im.width, 0, bytes(data))
    return Spf(0, 1, primary, secondary, [frame])


def files() -> dict[str, bytes]:
    out = {}
    for name, png in PANELS.items():
        try:
            out[name] = png_to_spf(Image.open(ART / png)).to_bytes()
        except ValueError as e:
            raise SystemExit(f"{name}: {e}")
    try:
        out["embl256.spf"] = custom_emblem_spf(Image.open(EMBLEM_PNG)).to_bytes()
    except ValueError as e:
        raise SystemExit(f"embl256.spf: {e}")
    return out


def check(arc_bytes: bytes, expected: dict[str, bytes]) -> None:
    arc = DatArchive.from_bytes(arc_bytes)
    for name, data in expected.items():
        if arc[name] != data:
            raise SystemExit(f"{name} read back differently from its source")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR, help="client folder to build from")
    ap.add_argument("--out", type=Path, help="output folder (default: a new folder on the desktop)")
    ap.add_argument("--no-apply", action="store_true", help="don't copy the batch into the client folder")
    args = ap.parse_args()

    built = files()
    arc = DatArchive.load(args.data_dir / "setoa.dat")
    for name, data in built.items():
        arc.put(name, data)
    data = arc.to_bytes()
    check(data, built)

    stamp = datetime.now()
    out = args.out or Path.home() / "Desktop" / f"Mirror maze art batch {stamp:%Y%m%d-%H%M}"
    out.mkdir(parents=True, exist_ok=True)
    (out / "setoa.dat").write_bytes(data)
    lines = [f"Mirror maze art batch built {stamp:%Y-%m-%d %H:%M} from {args.data_dir}", "",
             "Changed client files (upload in the same launcher patch as the client that draws mirrors):",
             f"  setoa.dat ({', '.join(built)})", ""]
    (out / "manifest.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")

    if not args.no_apply:
        target = args.data_dir / "setoa.dat"
        shutil.copy2(target, target.with_name(f"setoa.dat.{stamp:%Y%m%d-%H%M%S}.bak"))
        shutil.copy2(out / "setoa.dat", target)
        print(f"copied the batch into {args.data_dir} (backup kept)")
    print(f"done: {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

Run: the Verify command. Expected: `done: C:\Users\Michael\Desktop\Mirror maze art batch <stamp>`. Don't run it without `--no-apply` unless the user asks: that replaces the local client's `setoa.dat` (a backup is kept).

```json:metadata
{"files": ["Tools/MirrorMaze/make_art.py", "Tools/MirrorMaze/preview_panels.py", "Tools/MirrorMaze/render_map.cs", "Tools/MirrorMaze/build_art.py", "Tools/MirrorMaze/art/mirpnl01.png", "Tools/MirrorMaze/art/mirpnl02.png", "Tools/Emblems/art/custom/embl256.png"], "verifyCommand": "python Tools/MirrorMaze/build_art.py --no-apply", "acceptanceCriteria": ["make_art writes 28x89 frames with transparent glass and an 11x11 emblem", "preview shows framed panels on every inside face", "user approved the preview and emblem, quoted in the close", "build_art --no-apply writes a Desktop batch with setoa.dat and a manifest naming the three files"], "userGate": true, "tags": ["user-gate"], "modelTier": "standard"}
```

---

### Task 10: Client mirror state and packets

**Goal:** The client receives `MirrorLayout` and `MirrorDouble`, keeps them in `WorldState.Mirrors` with a per-wall-tile face index, keeps them through a same-map refresh, and clears them on a real map change and on logout.

**Files:**
- Create: `CLIENT/Chaos.Client/ViewModel/MirrorState.cs`
- Create: `CLIENT/Chaos.Client/Screens/WorldScreen.Mirrors.cs` (wiring and reset only in this task; Tasks 13 and 14 add drawing)
- Modify: `CLIENT/Chaos.Client.Networking/Definitions/Delegates.cs` (two delegates)
- Modify: `CLIENT/Chaos.Client.Networking/ConnectionManager.cs` (two events, two handlers, two registrations)
- Modify: `CLIENT/Chaos.Client/Collections/WorldState.cs` (`Mirrors` property; `ResetAll` clears it)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.cs` (call `WireMirrors` / `UnwireMirrors` beside the stage lighting ones)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.Map.cs` (call `ResetMirrors()` beside `ResetStageLighting()`)
- Test: `CLIENT/Tests/Chaos.Client.Tests/MirrorStateTests.cs`

**Acceptance Criteria:**
- [ ] `Apply` indexes every face by its wall tile; a tile can hold a north and a west face
- [ ] `FacesAt` is empty for tiles without mirrors; `Clear` empties everything
- [ ] `AddDouble` ignores an out-of-range run index and replaces an earlier double of the same entity; `PruneDoubles` drops doubles 7.6 s old
- [ ] `IsInDarkStretch` is true exactly inside a stretch
- [ ] The client solution builds with `0 Error(s)`

**Verify:** from CLIENT, with `UnoraServerPath` set: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorStateTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `CLIENT/Tests/Chaos.Client.Tests/MirrorStateTests.cs`:

```csharp
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class MirrorStateTests
{
    private static MirrorLayoutArgs Layout()
        => new()
        {
            Segments =
            [
                new MirrorSegmentInfo { Id = "n", X = 2, Y = 5, Side = MirrorSide.North, Length = 3, Style = MirrorStyle.Glass },
                new MirrorSegmentInfo { Id = "w", X = 4, Y = 5, Side = MirrorSide.West, Length = 2, Style = MirrorStyle.Haunted }
            ],
            DarkStretches = [new MirrorStretchInfo { X = 10, Y = 20, Width = 4, Height = 2 }]
        };

    [Test]
    public async Task Apply_indexes_faces_by_wall_tile()
    {
        var state = new MirrorState();
        state.Apply(Layout());

        state.FacesAt(2, 5)
             .Should()
             .Equal(new MirrorFace(0, 0, MirrorSide.North));

        state.FacesAt(3, 5)
             .Should()
             .Equal(new MirrorFace(0, 1, MirrorSide.North));

        //(4, 5) is the last north face and the first west face
        state.FacesAt(4, 5)
             .Should()
             .BeEquivalentTo(new[] { new MirrorFace(0, 2, MirrorSide.North), new MirrorFace(1, 0, MirrorSide.West) });

        state.FacesAt(4, 6)
             .Should()
             .Equal(new MirrorFace(1, 1, MirrorSide.West));

        state.FacesAt(5, 5)
             .Should()
             .BeEmpty();

        state.HasMirrors
             .Should()
             .BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clear_empties_everything()
    {
        var state = new MirrorState();
        state.Apply(Layout());
        state.AddDouble(7, 1, 1000);

        state.Clear();

        state.HasMirrors
             .Should()
             .BeFalse();

        state.FacesAt(2, 5)
             .Should()
             .BeEmpty();

        state.Doubles
             .Should()
             .BeEmpty();

        state.DarkStretches
             .Should()
             .BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Doubles_are_checked_replaced_and_pruned()
    {
        var state = new MirrorState();
        state.Apply(Layout());

        state.AddDouble(7, 5, 1000);

        state.Doubles
             .Should()
             .BeEmpty();

        state.AddDouble(7, 1, 1000);
        state.AddDouble(7, 1, 2000);
        state.AddDouble(8, 1, 1500);

        state.Doubles
             .Should()
             .HaveCount(2)
             .And
             .Contain(new MirrorDouble(7, 1, 2000));

        state.PruneDoubles(1500 + MirrorState.DOUBLE_LIFETIME_MS);

        state.Doubles
             .Should()
             .Equal(new MirrorDouble(7, 1, 2000));

        await Task.CompletedTask;
    }

    [Test]
    public async Task IsInDarkStretch_matches_the_rectangle()
    {
        var state = new MirrorState();
        state.Apply(Layout());

        state.IsInDarkStretch(10, 20)
             .Should()
             .BeTrue();

        state.IsInDarkStretch(13, 21)
             .Should()
             .BeTrue();

        state.IsInDarkStretch(14, 21)
             .Should()
             .BeFalse();

        state.IsInDarkStretch(10, 22)
             .Should()
             .BeFalse();

        state.IsInDarkStretch(9, 20)
             .Should()
             .BeFalse();

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: the Verify command. Expected: build error, `MirrorState` doesn't exist.

- [ ] **Step 3: Write the state**

Create `CLIENT/Chaos.Client/ViewModel/MirrorState.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>One mirror face on a wall tile: its run, how far along the run it is, and which face of the tile.</summary>
public readonly record struct MirrorFace(int SegmentIndex, int IndexInRun, MirrorSide Side);

/// <summary>A reflection climbing out of a haunted mirror, from the server's MirrorDouble.</summary>
public readonly record struct MirrorDouble(uint EntityId, int SegmentIndex, long StartMs);

/// <summary>
///     The current map's mirrors, from the server's MirrorLayout, indexed by wall tile, plus the doubles now showing.
///     Not cleared by <c>WorldState.Clear</c>: a same-map refresh keeps the mirrors. WorldScreen clears it on a real
///     map change; <c>WorldState.ResetAll</c> clears it on logout.
/// </summary>
public sealed class MirrorState
{
    /// <summary>A double climbs for 0.6 s, follows for 6 s and fades for 1 s.</summary>
    public const long DOUBLE_LIFETIME_MS = 7600;

    private static readonly List<MirrorFace> NoFaces = [];

    private readonly List<MirrorDouble> DoubleList = [];
    private readonly Dictionary<(int X, int Y), List<MirrorFace>> FacesByTile = [];

    public IReadOnlyList<MirrorStretchInfo> DarkStretches { get; private set; } = [];
    public IReadOnlyList<MirrorDouble> Doubles => DoubleList;
    public bool HasMirrors => Segments.Count > 0;
    public IReadOnlyList<MirrorSegmentInfo> Segments { get; private set; } = [];

    public void AddDouble(uint entityId, int segmentIndex, long nowMs)
    {
        if ((segmentIndex < 0) || (segmentIndex >= Segments.Count))
            return;

        DoubleList.RemoveAll(d => d.EntityId == entityId);
        DoubleList.Add(new MirrorDouble(entityId, segmentIndex, nowMs));
    }

    public void Apply(MirrorLayoutArgs args)
    {
        Clear();
        Segments = args.Segments ?? [];
        DarkStretches = args.DarkStretches ?? [];

        for (var i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];

            for (var k = 0; k < segment.Length; k++)
            {
                var tile = segment.Side == MirrorSide.North ? (segment.X + k, segment.Y) : (segment.X, segment.Y + k);

                if (!FacesByTile.TryGetValue(tile, out var faces))
                    FacesByTile[tile] = faces = [];

                faces.Add(new MirrorFace(i, k, segment.Side));
            }
        }
    }

    public void Clear()
    {
        Segments = [];
        DarkStretches = [];
        FacesByTile.Clear();
        DoubleList.Clear();
    }

    /// <summary>The mirror faces on wall tile (x, y): none, one, or a north and a west face.</summary>
    public IReadOnlyList<MirrorFace> FacesAt(int x, int y) => FacesByTile.TryGetValue((x, y), out var faces) ? faces : NoFaces;

    public bool IsInDarkStretch(int x, int y)
    {
        foreach (var s in DarkStretches)
            if ((x >= s.X) && (x < s.X + s.Width) && (y >= s.Y) && (y < s.Y + s.Height))
                return true;

        return false;
    }

    public void PruneDoubles(long nowMs) => DoubleList.RemoveAll(d => nowMs - d.StartMs >= DOUBLE_LIFETIME_MS);

    public void RemoveDoublesOf(uint entityId) => DoubleList.RemoveAll(d => d.EntityId == entityId);
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: the Verify command. Expected: 4 passed.

- [ ] **Step 5: Add the delegates and the connection events**

In `CLIENT/Chaos.Client.Networking/Definitions/Delegates.cs`, after `public delegate void StageLightingStateHandler(StageLightingStateArgs args);`, insert:

```csharp

public delegate void MirrorLayoutHandler(MirrorLayoutArgs args);

public delegate void MirrorDoubleHandler(MirrorDoubleArgs args);
```

In `CLIENT/Chaos.Client.Networking/ConnectionManager.cs`:

1. After the `OnStageLightingState` event declaration, insert:

```csharp

    /// <summary>Fired when the current map's mirrors arrive (on entering a map with the mirror script).</summary>
    public event MirrorLayoutHandler? OnMirrorLayout;

    /// <summary>Fired when a player's reflection climbs out of a haunted mirror nearby.</summary>
    public event MirrorDoubleHandler? OnMirrorDouble;
```

2. After `PacketHandlers[(byte)ServerOpCode.StageLightingState] = HandleStageLightingState;`, insert:

```csharp
        PacketHandlers[(byte)ServerOpCode.MirrorLayout] = HandleMirrorLayout;
        PacketHandlers[(byte)ServerOpCode.MirrorDouble] = HandleMirrorDouble;
```

3. After the `HandleStageLightingState` method, insert:

```csharp

    private void HandleMirrorLayout(ServerPacket pkt)
    {
        var args = Client.Deserialize<MirrorLayoutArgs>(in pkt);
        OnMirrorLayout?.Invoke(args);
    }

    private void HandleMirrorDouble(ServerPacket pkt)
    {
        var args = Client.Deserialize<MirrorDoubleArgs>(in pkt);
        OnMirrorDouble?.Invoke(args);
    }
```

If `Client.Deserialize` needs the converter registered by type, find how `StageLightingStateConverter` is registered on the client (search `StageLightingStateConverter` in CLIENT) and register `MirrorLayoutConverter` and `MirrorDoubleConverter` the same way.

- [ ] **Step 6: Add the state to WorldState**

In `CLIENT/Chaos.Client/Collections/WorldState.cs`, after the `StageLightingPanel` property, insert:

```csharp

    /// <summary>
    ///     The current map's mirrors and the doubles now showing. Not cleared by <see cref="Clear" />: a same-map refresh
    ///     keeps them. WorldScreen clears it on a real map change; <see cref="ResetAll" /> clears it on logout.
    /// </summary>
    public static MirrorState Mirrors { get; } = new();
```

In `ResetAll()`, after `StageLightingPanel.Reset();`, insert `Mirrors.Clear();`.

- [ ] **Step 7: Wire the screen**

Create `CLIENT/Chaos.Client/Screens/WorldScreen.Mirrors.cs`:

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Mirrors: the layout and doubles from the server, and (Tasks 13-14) their drawing.</summary>
public sealed partial class WorldScreen
{
    private void WireMirrors()
    {
        Game.Connection.OnMirrorLayout += HandleMirrorLayout;
        Game.Connection.OnMirrorDouble += HandleMirrorDouble;
    }

    private void UnwireMirrors()
    {
        Game.Connection.OnMirrorLayout -= HandleMirrorLayout;
        Game.Connection.OnMirrorDouble -= HandleMirrorDouble;
    }

    private static void HandleMirrorLayout(MirrorLayoutArgs args) => WorldState.Mirrors.Apply(args);

    private static void HandleMirrorDouble(MirrorDoubleArgs args)
        => WorldState.Mirrors.AddDouble(args.EntityId, args.SegmentIndex, Environment.TickCount64);

    /// <summary>Forgets the mirrors. Called on a real map change only; maps without mirrors send no layout at all.</summary>
    private void ResetMirrors() => WorldState.Mirrors.Clear();
}
```

In `CLIENT/Chaos.Client/Screens/WorldScreen.cs`, add `WireMirrors();` directly after `WireStageLighting();` and `UnwireMirrors();` directly after `UnwireStageLighting();`.

In `CLIENT/Chaos.Client/Screens/WorldScreen.Map.cs`, directly after `ResetStageLighting();` (the real map change branch), insert `ResetMirrors();`. The server sends `MirrorLayout` from `OnEntered`, after map info, so the new map's layout arrives after this reset.

- [ ] **Step 8: Build**

Run: from CLIENT, `UnoraServerPath=/c/Users/Michael/Documents/GitHub/worktrees/mirror-maze-server dotnet build Chaos.Client.slnx`. Expected: `0 Error(s)`.

```json:metadata
{"files": ["Chaos.Client/ViewModel/MirrorState.cs", "Chaos.Client/Screens/WorldScreen.Mirrors.cs", "Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client.Networking/ConnectionManager.cs", "Chaos.Client/Collections/WorldState.cs", "Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/Screens/WorldScreen.Map.cs", "Tests/Chaos.Client.Tests/MirrorStateTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MirrorStateTests/*\"", "acceptanceCriteria": ["faces indexed by wall tile, north and west on one tile", "FacesAt empty elsewhere and Clear empties", "AddDouble checks index and replaces; PruneDoubles at 7.6 s", "IsInDarkStretch exact", "client builds with 0 errors"], "modelTier": "mechanical"}
```

---

### Task 11: Mirror maths

**Goal:** Every number the mirror renderer needs comes from two pure, tested classes: `MirrorGeometry` (face pixels, in the rendering project) and `MirrorMath` (reflections, styles, the haunted schedule, doubles, cache ids, in the client project).

**Files:**
- Create: `CLIENT/Chaos.Client.Rendering/MirrorGeometry.cs`
- Create: `CLIENT/Chaos.Client/Systems/MirrorMath.cs`
- Test: `CLIENT/Tests/Chaos.Client.Tests/MirrorMathTests.cs`

**Acceptance Criteria:**
- [ ] Glass pixels match the Global Constraints geometry at its corners for both sides
- [ ] A north run reflects (x, y) to (x, 2·Y + 1 − y) and swaps Up/Down; a west run reflects to (2·X + 1 − x, y) and swaps Left/Right
- [ ] `OffsetToTiles` turns a one-tile world step back into one tile; `TileCenterWorld` matches `Camera.TileToWorld` plus half a tile
- [ ] Endless copies, window points and funhouse scales have the spec's numbers
- [ ] Over 2,000 s the haunted schedule's slips last 3 s, are 8–20 s apart, show all three kinds, repeat exactly for the same id and differ between ids
- [ ] The double timeline climbs, follows, fades and ends at 0.6 s, 6.6 s and 7.6 s
- [ ] Mirror cache ids have the top bit set and differ by facing and by idle

**Verify:** from CLIENT, with `UnoraServerPath` set: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorMathTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `CLIENT/Tests/Chaos.Client.Tests/MirrorMathTests.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class MirrorMathTests
{
    private static readonly MirrorSegmentInfo North = new() { Id = "n", X = 10, Y = 5, Side = MirrorSide.North, Length = 3 };
    private static readonly MirrorSegmentInfo West = new() { Id = "w", X = 20, Y = 10, Side = MirrorSide.West, Length = 2 };

    [Test]
    public async Task Glass_pixels_match_the_shared_geometry()
    {
        //north column 3: base row 14
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 3, 14 - 6).Should().BeTrue();
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 3, 14 - 5).Should().BeFalse();
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 2, 14 - 10).Should().BeFalse();
        //north column 24: base row 25
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 24, 25 - 69).Should().BeTrue();
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 24, 25 - 70).Should().BeFalse();
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 25, 25 - 30).Should().BeFalse();
        //west column 3: base row 25; west column 24: base row 14
        MirrorGeometry.IsGlassPixel(MirrorSide.West, 3, 25 - 6).Should().BeTrue();
        MirrorGeometry.IsGlassPixel(MirrorSide.West, 24, 14 - 69).Should().BeTrue();
        MirrorGeometry.IsGlassPixel(MirrorSide.West, 24, 14 - 5).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Reflections_flip_across_the_glass()
    {
        MirrorMath.ReflectPoint(North, new Vector2(11, 6)).Should().Be(new Vector2(11, 5));
        MirrorMath.ReflectPoint(North, new Vector2(11.5f, 8.25f)).Should().Be(new Vector2(11.5f, 2.75f));
        MirrorMath.ReflectPoint(West, new Vector2(23, 11)).Should().Be(new Vector2(18, 11));

        MirrorMath.ReflectFacing(MirrorSide.North, Direction.Up).Should().Be(Direction.Down);
        MirrorMath.ReflectFacing(MirrorSide.North, Direction.Left).Should().Be(Direction.Left);
        MirrorMath.ReflectFacing(MirrorSide.West, Direction.Right).Should().Be(Direction.Left);
        MirrorMath.ReflectFacing(MirrorSide.West, Direction.Down).Should().Be(Direction.Down);

        MirrorMath.FacingOutOf(MirrorSide.North).Should().Be(Direction.Down);
        MirrorMath.FacingOutOf(MirrorSide.West).Should().Be(Direction.Right);
        await Task.CompletedTask;
    }

    [Test]
    public async Task World_offsets_and_tile_centres()
    {
        MirrorMath.OffsetToTiles(new Vector2(28, 14)).Should().Be(new Vector2(1, 0));
        MirrorMath.OffsetToTiles(new Vector2(-28, 14)).Should().Be(new Vector2(0, 1));
        MirrorMath.OffsetToTiles(Vector2.Zero).Should().Be(Vector2.Zero);

        //Camera.TileToWorld(3, 2, 10) = ((10 - 1 + 3 - 2) * 28, (3 + 2) * 14) = (280, 70); plus half a tile
        MirrorMath.TileCenterWorld(new Vector2(3, 2), 10).Should().Be(new Vector2(308, 84));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Endless_window_and_funhouse_numbers()
    {
        MirrorMath.EndlessCopy(MirrorSide.North, new Vector2(11, 4), 2).Should().Be(new Vector2(11, 4 - 1.8f));
        MirrorMath.EndlessCopy(MirrorSide.West, new Vector2(18, 11), 1).Should().Be(new Vector2(18 - 0.9f, 11));
        MirrorMath.EndlessScale(0).Should().Be(1f);
        MirrorMath.EndlessScale(2).Should().BeApproximately(0.7396f, 0.0001f);
        MirrorMath.EndlessAlpha(1).Should().BeApproximately(0.372f, 0.0001f);
        MirrorMath.EndlessFacing(1, Direction.Up, Direction.Down).Should().Be(Direction.Up);
        MirrorMath.EndlessFacing(2, Direction.Up, Direction.Down).Should().Be(Direction.Down);

        var self = new MirrorSegmentInfo { Id = "a", X = 10, Y = 5, Side = MirrorSide.North, Length = 2 };
        var partner = new MirrorSegmentInfo { Id = "b", X = 14, Y = 5, Side = MirrorSide.North, Length = 2 };
        MirrorMath.WindowPoint(self, partner, new Vector2(15, 7)).Should().Be(new Vector2(11, 4));

        MirrorMath.FunhouseScale(MirrorFunhouse.Tall).Should().Be((0.75f, 1.5f));
        MirrorMath.FunhouseScale(MirrorFunhouse.Wide).Should().Be((1.35f, 0.7f));
        MirrorMath.FunhouseScale(MirrorFunhouse.Wave).Should().Be((1f, 1f));
        MirrorMath.RippleOffset(0, 0).Should().Be(0f);
        Math.Abs(MirrorMath.RippleOffset(1.3, 40)).Should().BeLessThanOrEqualTo(3f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Haunted_slips_last_three_seconds_eight_to_twenty_apart()
    {
        const double START = 1_790_000_000;
        const double STEP = 0.05;
        var slips = new List<(double Start, double End, HauntedSlip Kind)>();
        double? openStart = null;
        var openKind = HauntedSlip.None;
        var seenCalm = false; //a slip already running at START would be measured short, so skip it

        for (var t = START; t < START + 2000; t += STEP)
        {
            (var kind, _) = MirrorMath.HauntedSlipAt("hau-n-12-30", t);

            if (kind == HauntedSlip.None)
                seenCalm = true;

            if ((kind != HauntedSlip.None) && openStart is null && seenCalm)
            {
                openStart = t;
                openKind = kind;
            } else if ((kind == HauntedSlip.None) && openStart is not null)
            {
                slips.Add((openStart.Value, t, openKind));
                openStart = null;
            }
        }

        slips.Count.Should().BeGreaterThan(80);

        foreach (var slip in slips)
            (slip.End - slip.Start).Should().BeApproximately(3, STEP * 2);

        for (var i = 1; i < slips.Count; i++)
            (slips[i].Start - slips[i - 1].End).Should().BeInRange(8 - STEP * 2, 20 + STEP * 2);

        slips.Select(s => s.Kind).Distinct().Should().BeEquivalentTo(new[] { HauntedSlip.Lag, HauntedSlip.Stare, HauntedSlip.Ghost });

        MirrorMath.HauntedSlipAt("hau-n-12-30", START + 123.4).Should().Be(MirrorMath.HauntedSlipAt("hau-n-12-30", START + 123.4));

        Enumerable.Range(0, 200)
                  .Count(i => MirrorMath.HauntedSlipAt("a", START + i).Kind != MirrorMath.HauntedSlipAt("b", START + i).Kind)
                  .Should()
                  .BeGreaterThan(0);

        MirrorMath.GhostAlpha(0).Should().BeApproximately(0.35f, 0.001f);
        MirrorMath.GhostAlpha(0.25).Should().BeApproximately(0.5f, 0.001f);
        MirrorMath.GhostAlpha(0.75).Should().BeApproximately(0.2f, 0.001f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Double_timeline()
    {
        MirrorMath.DoubleAt(0.3).Should().Be((DoublePhase.Climbing, 0.5f, 0.225f));
        MirrorMath.DoubleAt(3.6).Should().Be((DoublePhase.Following, 0.5f, 0.45f));
        MirrorMath.DoubleAt(7.1).Phase.Should().Be(DoublePhase.Fading);
        MirrorMath.DoubleAt(7.1).Alpha.Should().BeApproximately(0.225f, 0.001f);
        MirrorMath.DoubleAt(7.65).Phase.Should().Be(DoublePhase.Done);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Mirror_cache_ids()
    {
        var a = MirrorMath.MirrorCacheId(42, Direction.Up, false);
        var b = MirrorMath.MirrorCacheId(42, Direction.Down, false);
        var c = MirrorMath.MirrorCacheId(42, Direction.Up, true);

        (a & 0x8000_0000u).Should().NotBe(0u);
        new[] { a, b, c }.Distinct().Should().HaveCount(3);
        MirrorMath.MirrorCacheId(43, Direction.Up, false).Should().NotBe(a);
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: the Verify command. Expected: build error, `MirrorGeometry` and `MirrorMath` don't exist.

- [ ] **Step 3: Write the geometry**

Create `CLIENT/Chaos.Client.Rendering/MirrorGeometry.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Where a mirror face's panel and glass sit inside a wall tile, in tile-local pixels (origin at the tile's
///     <see cref="Camera.TileToWorld" /> corner). The Unora art script (Tools/MirrorMaze/make_art.py) uses the same
///     numbers; change both together.
/// </summary>
public static class MirrorGeometry
{
    public const int FACE_WIDTH = 28;
    public const int PANEL_ROWS = 76;
    public const int GLASS_LEFT = 3;
    public const int GLASS_RIGHT = 24;
    public const int GLASS_TOP = 69;
    public const int GLASS_BOTTOM = 6;

    /// <summary>The frame sprite's canvas: 28 wide, 89 tall, drawn at tile-local y -62.</summary>
    public const int CANVAS_HEIGHT = 89;

    public const int CANVAS_TOP = -62;

    /// <summary>The face's base row for column 0-27 of the face.</summary>
    public static int BaseRow(MirrorSide side, int column) => side == MirrorSide.North ? 13 + column / 2 : 26 - column / 2;

    /// <summary>The face's left edge within the tile: 0 for north (left half), 28 for west (right half).</summary>
    public static int FaceLocalX(MirrorSide side) => side == MirrorSide.North ? 0 : FACE_WIDTH;

    /// <summary>True when tile-local row <paramref name="localRow" /> of face column <paramref name="column" /> is glass.</summary>
    public static bool IsGlassPixel(MirrorSide side, int column, int localRow)
    {
        if ((column < GLASS_LEFT) || (column > GLASS_RIGHT))
            return false;

        var above = BaseRow(side, column) - localRow;

        return (above >= GLASS_BOTTOM) && (above <= GLASS_TOP);
    }

    /// <summary>How far up the glass a pixel is, 0 at its bottom edge and 1 at its top. Only meaningful for glass pixels.</summary>
    public static float GlassHeightFraction(MirrorSide side, int column, int localRow)
        => (BaseRow(side, column) - localRow - GLASS_BOTTOM) / (float)(GLASS_TOP - GLASS_BOTTOM);
}
```

- [ ] **Step 4: Write the maths**

Create `CLIENT/Chaos.Client/Systems/MirrorMath.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>A haunted mirror's current trick.</summary>
public enum HauntedSlip : byte
{
    None,
    Lag,
    Stare,
    Ghost
}

/// <summary>Where a double is in its life.</summary>
public enum DoublePhase : byte
{
    Climbing,
    Following,
    Fading,
    Done
}

/// <summary>
///     The mirror renderer's pure maths: reflected positions and facings, the styles' numbers, the haunted schedule, the
///     double's timeline and the render cache ids of mirrored copies. Tile positions are tile-space floats.
/// </summary>
public static class MirrorMath
{
    public const int REFLECT_DEPTH = 4;
    public const int REFLECT_MARGIN = 1;
    public const int SPRITE_CAP = 40;

    public const float GLASS_ALPHA = 0.6f;
    public const float WINDOW_ALPHA = 0.7f;
    public const double GLINT_PERIOD_SECONDS = 9;
    public const double GLINT_SWEEP_SECONDS = 3;

    public const double HAUNTED_SLOT_SECONDS = 17;
    public const double HAUNTED_SLIP_SECONDS = 3;
    public const double HAUNTED_LAG_SECONDS = 1.1;

    public const int ENDLESS_COPIES = 4;
    public const float ENDLESS_STEP_TILES = 0.9f;

    public const double DOUBLE_CLIMB_SECONDS = 0.6;
    public const double DOUBLE_FOLLOW_SECONDS = 6;
    public const double DOUBLE_FADE_SECONDS = 1;
    public const double DOUBLE_LAG_SECONDS = 0.45;
    public const float DOUBLE_MAX_ALPHA = 0.45f;

    /// <summary>The wall tile at <paramref name="index" /> along a run.</summary>
    public static Point WallTile(MirrorSegmentInfo segment, int index)
        => segment.Side == MirrorSide.North ? new Point(segment.X + index, segment.Y) : new Point(segment.X, segment.Y + index);

    /// <summary>True when tile (x, y) is 1 to <paramref name="depth" /> tiles in front of the run, within its span ± margin.</summary>
    public static bool IsInFront(MirrorSegmentInfo segment, int x, int y, int depth, int margin)
        => segment.Side == MirrorSide.North
            ? (y > segment.Y) && (y <= segment.Y + depth) && (x >= segment.X - margin) && (x <= segment.X + segment.Length - 1 + margin)
            : (x > segment.X) && (x <= segment.X + depth) && (y >= segment.Y - margin) && (y <= segment.Y + segment.Length - 1 + margin);

    public static Vector2 ReflectPoint(MirrorSegmentInfo segment, Vector2 tile)
        => segment.Side == MirrorSide.North
            ? new Vector2(tile.X, 2 * segment.Y + 1 - tile.Y)
            : new Vector2(2 * segment.X + 1 - tile.X, tile.Y);

    public static Direction ReflectFacing(MirrorSide side, Direction facing)
        => side == MirrorSide.North
            ? facing switch
            {
                Direction.Up   => Direction.Down,
                Direction.Down => Direction.Up,
                _              => facing
            }
            : facing switch
            {
                Direction.Left  => Direction.Right,
                Direction.Right => Direction.Left,
                _               => facing
            };

    /// <summary>The facing that looks out of the glass at the viewer.</summary>
    public static Direction FacingOutOf(MirrorSide side) => side == MirrorSide.North ? Direction.Down : Direction.Right;

    /// <summary>A world-pixel offset (like <c>WorldEntity.VisualOffset</c>) in tiles.</summary>
    public static Vector2 OffsetToTiles(Vector2 worldOffset)
    {
        var a = worldOffset.X / 28f;
        var b = worldOffset.Y / 14f;

        return new Vector2((a + b) / 2f, (b - a) / 2f);
    }

    /// <summary>The world pixel at the centre of a tile-space position, as <c>Camera.TileToWorld</c> + half a tile.</summary>
    public static Vector2 TileCenterWorld(Vector2 tile, int mapHeight)
        => new((mapHeight - 1 + tile.X - tile.Y) * 28f + 28f, (tile.X + tile.Y) * 14f + 14f);

    public static Vector2 EndlessCopy(MirrorSide side, Vector2 reflected, int copy)
        => side == MirrorSide.North
            ? new Vector2(reflected.X, reflected.Y - ENDLESS_STEP_TILES * copy)
            : new Vector2(reflected.X - ENDLESS_STEP_TILES * copy, reflected.Y);

    public static float EndlessScale(int copy) => MathF.Pow(0.86f, copy);

    public static float EndlessAlpha(int copy) => GLASS_ALPHA * MathF.Pow(0.62f, copy);

    public static Direction EndlessFacing(int copy, Direction original, Direction reflected) => copy % 2 == 1 ? original : reflected;

    /// <summary>Where a character in front of <paramref name="partner" /> shows in <paramref name="self" />.</summary>
    public static Vector2 WindowPoint(MirrorSegmentInfo self, MirrorSegmentInfo partner, Vector2 tile)
        => ReflectPoint(partner, tile) + new Vector2(self.X - partner.X, self.Y - partner.Y);

    public static (float X, float Y) FunhouseScale(MirrorFunhouse kind)
        => kind switch
        {
            MirrorFunhouse.Tall => (0.75f, 1.5f),
            MirrorFunhouse.Wide => (1.35f, 0.7f),
            _                   => (1f, 1f)
        };

    /// <summary>Sideways shift of a 2-pixel row of a funhouse reflection.</summary>
    public static float RippleOffset(double seconds, int row) => (float)(Math.Sin(seconds * 5 + row * 0.18) * 3);

    /// <summary>
    ///     How far along its run the glint is, from -0.3 to 1.3, during the first 3 s of every 9 s; null the rest of
    ///     the time.
    /// </summary>
    public static float? GlintFraction(double seconds)
    {
        var into = seconds % GLINT_PERIOD_SECONDS;

        return into >= GLINT_SWEEP_SECONDS ? null : (float)(into / GLINT_SWEEP_SECONDS * 1.6 - 0.3);
    }

    /// <summary>
    ///     The haunted schedule. Time is cut into 17 s slots (offset per run); in each slot one 3 s slip starts 2-8 s
    ///     in, so slips are 8-20 s apart. Everything comes from the run id and the clock, so every client agrees.
    /// </summary>
    public static (HauntedSlip Kind, double SecondsInto) HauntedSlipAt(string segmentId, double unixSeconds)
    {
        var hash = Fnv1a(segmentId);
        var shifted = unixSeconds + hash % 17;
        var slot = (long)Math.Floor(shifted / HAUNTED_SLOT_SECONDS);
        var into = shifted - slot * HAUNTED_SLOT_SECONDS;
        var mixed = Mix(hash, slot);
        var start = 2 + mixed % 7;

        if ((into < start) || (into >= start + HAUNTED_SLIP_SECONDS))
            return (HauntedSlip.None, 0);

        return ((HauntedSlip)(1 + mixed / 7 % 3), into - start);
    }

    /// <summary>The ghost slip's opacity: 0.2 to 0.5, once a second.</summary>
    public static float GhostAlpha(double seconds) => 0.35f + 0.15f * (float)Math.Sin(seconds * 2 * Math.PI);

    public static (DoublePhase Phase, float Progress, float Alpha) DoubleAt(double seconds)
    {
        if (seconds < DOUBLE_CLIMB_SECONDS)
        {
            var p = (float)(seconds / DOUBLE_CLIMB_SECONDS);

            return (DoublePhase.Climbing, p, DOUBLE_MAX_ALPHA * p);
        }

        seconds -= DOUBLE_CLIMB_SECONDS;

        if (seconds < DOUBLE_FOLLOW_SECONDS)
            return (DoublePhase.Following, (float)(seconds / DOUBLE_FOLLOW_SECONDS), DOUBLE_MAX_ALPHA);

        seconds -= DOUBLE_FOLLOW_SECONDS;

        if (seconds < DOUBLE_FADE_SECONDS)
        {
            var p = (float)(seconds / DOUBLE_FADE_SECONDS);

            return (DoublePhase.Fading, p, DOUBLE_MAX_ALPHA * (1 - p));
        }

        return (DoublePhase.Done, 1, 0);
    }

    /// <summary>
    ///     The render cache id of a mirrored copy: top bit set, so it never meets a real entity id, then the entity id,
    ///     the facing and whether it is posed idle.
    /// </summary>
    public static uint MirrorCacheId(uint entityId, Direction facing, bool idle)
        => 0x8000_0000u | ((entityId & 0x0FFF_FFFFu) << 3) | ((uint)facing << 1) | (idle ? 1u : 0u);

    public static uint Fnv1a(string text)
    {
        var hash = 2166136261u;

        foreach (var c in text.ToLowerInvariant())
            unchecked
            {
                hash ^= c;
                hash *= 16777619u;
            }

        return hash;
    }

    private static uint Mix(uint hash, long slot)
    {
        unchecked
        {
            var x = hash ^ (uint)(slot * 0x9E3779B1L);
            x ^= x >> 16;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;

            return x;
        }
    }
}
```

- [ ] **Step 5: Run the tests to see them pass**

Run: the Verify command. Expected: 7 passed. If `Double_timeline`'s exact tuple equality fails on float rounding, compare each field with `BeApproximately(…, 0.0001f)` instead; the numbers stay the same.

```json:metadata
{"files": ["Chaos.Client.Rendering/MirrorGeometry.cs", "Chaos.Client/Systems/MirrorMath.cs", "Tests/Chaos.Client.Tests/MirrorMathTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MirrorMathTests/*\"", "acceptanceCriteria": ["glass pixels match the shared geometry", "north and west reflections and facings", "offset and tile centre conversions", "endless, window and funhouse numbers", "haunted slips 3 s long, 8-20 s apart, all kinds, deterministic, id-dependent", "double timeline boundaries", "cache ids high bit and distinct"], "modelTier": "mechanical"}
```

---

### Task 12: Mirrored copies get their own render cache id

**Goal:** A character's mirrored copy is drawn under its own composite cache id, so drawing reflections never rebuilds or disposes the character's real cached image mid-frame.

**Files:**
- Modify: `CLIENT/Chaos.Client/Models/WorldEntity.cs` (`RenderCacheId`, `CopyForMirror`)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.Draw.cs` (`DrawAisling` passes `entity.RenderCacheId`)
- Test: `CLIENT/Tests/Chaos.Client.Tests/WorldEntityMirrorCopyTests.cs`

**Acceptance Criteria:**
- [ ] `RenderCacheId` is `Id` for normal entities
- [ ] `CopyForMirror(id)` returns a separate object with the same `Id`, tile, direction and appearance, and the given `RenderCacheId`; changing the copy leaves the original alone
- [ ] `DrawAisling` builds `AislingDrawParams` and reads the composite top padding with `entity.RenderCacheId`
- [ ] The client builds with `0 Error(s)`

**Verify:** from CLIENT, with `UnoraServerPath` set: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/WorldEntityMirrorCopyTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing test**

Create `CLIENT/Tests/Chaos.Client.Tests/WorldEntityMirrorCopyTests.cs`:

```csharp
using Chaos.Client.Models;
using Chaos.Geometry.Abstractions.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class WorldEntityMirrorCopyTests
{
    [Test]
    public async Task A_copy_has_its_own_cache_id_and_leaves_the_original_alone()
    {
        var original = new WorldEntity
        {
            Id = 42,
            TileX = 3,
            TileY = 4,
            Direction = Direction.Up,
            SpriteId = 179,
            VisualOffset = new Vector2(5, 6)
        };

        original.RenderCacheId.Should().Be(42u);

        var copy = original.CopyForMirror(0x8000_0151u);

        copy.Should().NotBeSameAs(original);
        copy.Id.Should().Be(42u);
        copy.RenderCacheId.Should().Be(0x8000_0151u);
        copy.TileX.Should().Be(3);
        copy.SpriteId.Should().Be((ushort)179);

        copy.Direction = Direction.Down;
        copy.VisualOffset = Vector2.Zero;

        original.Direction.Should().Be(Direction.Up);
        original.VisualOffset.Should().Be(new Vector2(5, 6));
        original.RenderCacheId.Should().Be(42u);
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the test to see it fail**

Run: the Verify command. Expected: build error, `RenderCacheId` doesn't exist.

- [ ] **Step 3: Add the cache id and the copy**

In `CLIENT/Chaos.Client/Models/WorldEntity.cs`, directly after `public uint Id { get; init; }`, insert:

```csharp

    /// <summary>
    ///     The id renderers cache this entity's composed image under. It is <see cref="Id" />, except on a copy made by
    ///     <see cref="CopyForMirror" />, so a reflection never evicts the real character's cached image mid-frame.
    /// </summary>
    public uint RenderCacheId => RenderCacheIdOverride ?? Id;

    private uint? RenderCacheIdOverride { get; set; }

    /// <summary>
    ///     A shallow copy for drawing in a mirror, cached under <paramref name="renderCacheId" />. The caller moves and
    ///     turns the copy; the original is not touched.
    /// </summary>
    public WorldEntity CopyForMirror(uint renderCacheId)
    {
        var copy = (WorldEntity)MemberwiseClone();
        copy.RenderCacheIdOverride = renderCacheId;

        return copy;
    }
```

- [ ] **Step 4: Use it when drawing aislings**

In `CLIENT/Chaos.Client/Screens/WorldScreen.Draw.cs`, in `DrawAisling`:
- In `new AislingDrawParams(entity.Id, ...`, change the first argument to `entity.RenderCacheId`.
- In `Game.AislingRenderer.TryGetCompositeTopPadding(entity.Id, out var topPadding);`, change `entity.Id` to `entity.RenderCacheId`.

Leave every other `entity.Id` in the file alone: hover and group highlights should still match the real character.

- [ ] **Step 5: Run the test and build**

Run: the Verify command. Expected: 1 passed. Then build the client solution. Expected: `0 Error(s)`.

```json:metadata
{"files": ["Chaos.Client/Models/WorldEntity.cs", "Chaos.Client/Screens/WorldScreen.Draw.cs", "Tests/Chaos.Client.Tests/WorldEntityMirrorCopyTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/WorldEntityMirrorCopyTests/*\"", "acceptanceCriteria": ["RenderCacheId equals Id normally", "CopyForMirror separate object with given cache id, original untouched", "DrawAisling uses RenderCacheId for draw params and top padding", "client builds with 0 errors"], "modelTier": "mechanical"}
```

---

### Task 13: Position history

**Goal:** `EntityTrail` remembers where each character stood and faced over the last 2 seconds, for the haunted mirror's lag and stare slips and for doubles.

**Files:**
- Create: `CLIENT/Chaos.Client/Systems/EntityTrail.cs`
- Test: `CLIENT/Tests/Chaos.Client.Tests/EntityTrailTests.cs`

**Acceptance Criteria:**
- [ ] `TryGet` returns the latest sample at or before the asked time, and false before the first sample or for unknown ids
- [ ] Samples older than 2 s are dropped, except the newest one older than that, so a lookup exactly 2 s back still works
- [ ] `Prune` forgets characters with no sample in the last 2 s; `Clear` forgets all

**Verify:** from CLIENT, with `UnoraServerPath` set: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/EntityTrailTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `CLIENT/Tests/Chaos.Client.Tests/EntityTrailTests.cs`:

```csharp
using Chaos.Client.Systems;
using Chaos.Geometry.Abstractions.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class EntityTrailTests
{
    [Test]
    public async Task Returns_the_latest_sample_at_or_before_the_time()
    {
        var trail = new EntityTrail();
        trail.Record(1, 1000, new Vector2(1, 1), Direction.Up);
        trail.Record(1, 1100, new Vector2(1, 2), Direction.Down);
        trail.Record(1, 1200, new Vector2(1, 3), Direction.Left);

        trail.TryGet(1, 1150, out var tile, out var facing).Should().BeTrue();
        tile.Should().Be(new Vector2(1, 2));
        facing.Should().Be(Direction.Down);

        trail.TryGet(1, 1200, out tile, out _).Should().BeTrue();
        tile.Should().Be(new Vector2(1, 3));

        trail.TryGet(1, 999, out _, out _).Should().BeFalse();
        trail.TryGet(2, 1200, out _, out _).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Keeps_two_seconds_and_one_sample_before()
    {
        var trail = new EntityTrail();

        for (var ms = 0L; ms <= 5000; ms += 100)
            trail.Record(1, ms, new Vector2(ms, 0), Direction.Up);

        trail.TryGet(1, 3000, out var tile, out _).Should().BeTrue();
        tile.X.Should().Be(3000);

        trail.TryGet(1, 2899, out _, out _).Should().BeFalse();
        trail.Count(1).Should().BeLessThanOrEqualTo(22);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Prune_and_clear()
    {
        var trail = new EntityTrail();
        trail.Record(1, 1000, Vector2.Zero, Direction.Up);
        trail.Record(2, 4000, Vector2.Zero, Direction.Up);

        trail.Prune(4500);

        trail.TryGet(1, 1000, out _, out _).Should().BeFalse();
        trail.TryGet(2, 4000, out _, out _).Should().BeTrue();

        trail.Clear();
        trail.TryGet(2, 4000, out _, out _).Should().BeFalse();
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: the Verify command. Expected: build error, `EntityTrail` doesn't exist.

- [ ] **Step 3: Write the trail**

Create `CLIENT/Chaos.Client/Systems/EntityTrail.cs`:

```csharp
#region
using Chaos.Geometry.Abstractions.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Where each character stood (tile-space, walk offset included) and which way it faced over the last
///     <see cref="KEEP_MS" />. The mirror renderer records every character once a frame on maps with mirrors.
/// </summary>
public sealed class EntityTrail
{
    public const long KEEP_MS = 2000;

    private readonly Dictionary<uint, List<Sample>> SamplesById = [];

    public void Clear() => SamplesById.Clear();

    /// <summary>How many samples are kept for <paramref name="id" />. For tests.</summary>
    public int Count(uint id) => SamplesById.TryGetValue(id, out var samples) ? samples.Count : 0;

    /// <summary>Forgets characters with no sample in the last <see cref="KEEP_MS" />.</summary>
    public void Prune(long nowMs)
    {
        foreach (var id in SamplesById.Where(kvp => kvp.Value[^1].Ms < nowMs - KEEP_MS)
                                      .Select(kvp => kvp.Key)
                                      .ToList())
            SamplesById.Remove(id);
    }

    public void Record(uint id, long ms, Vector2 tile, Direction facing)
    {
        if (!SamplesById.TryGetValue(id, out var samples))
            SamplesById[id] = samples = [];

        if ((samples.Count > 0) && (samples[^1].Ms == ms))
            samples[^1] = new Sample(ms, tile, facing);
        else
            samples.Add(new Sample(ms, tile, facing));

        //keep the newest sample older than the window, so a lookup exactly KEEP_MS back still finds one
        var cutoff = ms - KEEP_MS;
        var drop = 0;

        while ((drop + 1 < samples.Count) && (samples[drop + 1].Ms <= cutoff))
            drop++;

        if (drop > 0)
            samples.RemoveRange(0, drop);
    }

    /// <summary>The latest sample at or before <paramref name="ms" />.</summary>
    public bool TryGet(uint id, long ms, out Vector2 tile, out Direction facing)
    {
        tile = default;
        facing = default;

        if (!SamplesById.TryGetValue(id, out var samples))
            return false;

        for (var i = samples.Count - 1; i >= 0; i--)
            if (samples[i].Ms <= ms)
            {
                tile = samples[i].Tile;
                facing = samples[i].Facing;

                return true;
            }

        return false;
    }

    private readonly record struct Sample(long Ms, Vector2 Tile, Direction Facing);
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: the Verify command. Expected: 3 passed.

```json:metadata
{"files": ["Chaos.Client/Systems/EntityTrail.cs", "Tests/Chaos.Client.Tests/EntityTrailTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/EntityTrailTests/*\"", "acceptanceCriteria": ["latest sample at or before the time; false before first or unknown id", "2 s window plus one older sample", "Prune drops stale ids; Clear empties"], "modelTier": "mechanical"}
```

---

### Task 14: The mirror renderer: glass, reflections and frames

**Goal:** On a map with mirrors, every visible inside wall face shows a framed glass panel, and characters standing in front of it show in it in their run's style: glass, funhouse, haunted (with its three slips), endless, or window.

**Files:**
- Create: `CLIENT/Chaos.Client.Rendering/MirrorRenderer.cs`
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.Mirrors.cs` (placements, prerender, per-tile drawing)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.Draw.cs` (call `PreRenderMirrors` after the silhouette prerender; call `DrawMirrorTile` after each foreground tile)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.cs` (create and dispose `MirrorRenderer` beside `SilhouetteRenderer`)

**Acceptance Criteria:**
- [ ] The client builds with `0 Error(s)` and the full client test run passes
- [ ] On a map without mirrors, no mirror code runs past its first `HasMirrors` check
- [ ] Atlas and layer render targets are created once and reused while the back buffer size is unchanged
- [ ] With more than 40 reflections in view, the 40 nearest to the local player are drawn
- [ ] A frame sprite missing from `setoa.dat` is skipped (no checkerboard), and the glass still draws

**Verify:** from CLIENT, with `UnoraServerPath` set: `dotnet build Chaos.Client.slnx` → `0 Error(s)`; then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass. The look is checked in game in Task 18.

**Steps:**

- [ ] **Step 1: Write the renderer**

Create `CLIENT/Chaos.Client.Rendering/MirrorRenderer.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Owns the mirror render targets and textures. Each frame, before the world pass, WorldScreen paints every character
///     a mirror needs into the atlas (one cell per character and pose), then composes all visible glass and reflections
///     into the layer, which is the size of the back buffer, like the silhouette target. The world pass pastes each wall
///     tile's slice of the layer and then its frame sprite.
/// </summary>
public sealed class MirrorRenderer : IDisposable
{
    public const int CELL_SIZE = 160;
    public const int CELL_ANCHOR_X = 80;
    public const int CELL_ANCHOR_Y = 136;
    public const int ATLAS_COLUMNS = 12;
    public const int ATLAS_ROWS = 8;

    /// <summary>Draws only where the target already has alpha (the glass) and keeps the target's alpha.</summary>
    public static readonly BlendState SourceAtop = new()
    {
        Name = "MirrorSourceAtop",
        ColorSourceBlend = Blend.DestinationAlpha,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One
    };

    private static readonly RasterizerState CellScissor = new()
    {
        CullMode = CullMode.None,
        ScissorTestEnable = true
    };

    private readonly Dictionary<ulong, int> CellByKey = [];
    private readonly GraphicsDevice Device;
    private RenderTarget2D? Atlas;
    private SpriteBatch? Batch;
    private Texture2D? DiamondTexture;
    private Texture2D? GlowTexture;
    private RenderTarget2D? Layer;
    private int NextCell;
    private Texture2D? NorthGlass;
    private Texture2D? PixelTexture;
    private Texture2D? WestGlass;

    public MirrorRenderer(GraphicsDevice device) => Device = device;

    public Texture2D? AtlasTexture => Atlas;

    /// <summary>A white 56x27 tile diamond.</summary>
    public Texture2D Diamond => DiamondTexture ??= BuildDiamond();

    /// <summary>A soft white 64x32 ellipse, premultiplied, for additive light.</summary>
    public Texture2D Glow => GlowTexture ??= BuildGlow();

    public Texture2D? LayerTexture => Layer;

    /// <summary>True once this frame's layer has been composed.</summary>
    public bool LayerReady { get; private set; }

    public Texture2D Pixel => PixelTexture ??= BuildPixel();

    public void Dispose()
    {
        Atlas?.Dispose();
        Layer?.Dispose();
        Batch?.Dispose();
        DiamondTexture?.Dispose();
        GlowTexture?.Dispose();
        NorthGlass?.Dispose();
        WestGlass?.Dispose();
        PixelTexture?.Dispose();
    }

    /// <summary>A face's glass: white at the top fading to grey at the bottom, transparent outside the glass. Tint it.</summary>
    public Texture2D Glass(MirrorSide side)
        => side == MirrorSide.North ? NorthGlass ??= BuildGlass(MirrorSide.North) : WestGlass ??= BuildGlass(MirrorSide.West);

    /// <summary>Forgets last frame's cells and marks the layer stale. Call once a frame before reserving cells.</summary>
    public void BeginFrame()
    {
        CellByKey.Clear();
        NextCell = 0;
        LayerReady = false;
    }

    public static Rectangle CellRect(int index)
        => new(
            index % ATLAS_COLUMNS * CELL_SIZE,
            index / ATLAS_COLUMNS * CELL_SIZE,
            CELL_SIZE,
            CELL_SIZE);

    /// <summary>Finds or reserves the atlas cell for <paramref name="key" />. False when the atlas is full.</summary>
    public bool TryReserveCell(ulong key, out Rectangle cell, out bool isNew)
    {
        isNew = false;

        if (CellByKey.TryGetValue(key, out var index))
        {
            cell = CellRect(index);

            return true;
        }

        if (NextCell >= ATLAS_COLUMNS * ATLAS_ROWS)
        {
            cell = default;

            return false;
        }

        index = NextCell++;
        CellByKey[key] = index;
        cell = CellRect(index);
        isNew = true;

        return true;
    }

    public bool TryGetCell(ulong key, out Rectangle cell)
    {
        if (CellByKey.TryGetValue(key, out var index))
        {
            cell = CellRect(index);

            return true;
        }

        cell = default;

        return false;
    }

    /// <summary>Clears the atlas and runs <paramref name="paint" /> with it bound, then restores the previous target.</summary>
    public void RenderAtlas(Action paint)
    {
        Atlas = EnsureTarget(Atlas, ATLAS_COLUMNS * CELL_SIZE, ATLAS_ROWS * CELL_SIZE);
        var previous = CurrentTarget();
        Device.SetRenderTarget(Atlas);
        Device.ScissorRectangle = new Rectangle(0, 0, Atlas.Width, Atlas.Height);
        Device.Clear(Color.Transparent);

        try
        {
            paint();
        } finally
        {
            Device.SetRenderTarget(previous);
        }
    }

    /// <summary>
    ///     Inside <see cref="RenderAtlas" />: runs <paramref name="draw" /> with a batch whose transform puts the screen
    ///     point <paramref name="feetScreen" /> on the cell's anchor. Anything outside the cell is cut off.
    /// </summary>
    public void PaintCell(Rectangle cell, Vector2 feetScreen, Action<SpriteBatch> draw)
    {
        Batch ??= new SpriteBatch(Device);
        Device.ScissorRectangle = cell;

        var transform = Matrix.CreateTranslation(cell.X + CELL_ANCHOR_X - feetScreen.X, cell.Y + CELL_ANCHOR_Y - feetScreen.Y, 0);

        Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, CellScissor, null, transform);

        try
        {
            draw(Batch);
        } finally
        {
            Batch.End();
        }
    }

    /// <summary>
    ///     Clears the layer, runs <paramref name="drawGlass" /> with normal blending, then
    ///     <paramref name="drawReflections" /> with <see cref="SourceAtop" />, so reflections and glints only land on glass.
    /// </summary>
    public void RenderLayer(Action<SpriteBatch> drawGlass, Action<SpriteBatch> drawReflections)
    {
        Batch ??= new SpriteBatch(Device);

        Layer = EnsureTarget(Layer, Device.PresentationParameters.BackBufferWidth, Device.PresentationParameters.BackBufferHeight);
        var previous = CurrentTarget();
        Device.SetRenderTarget(Layer);
        Device.ScissorRectangle = new Rectangle(0, 0, Layer.Width, Layer.Height);
        Device.Clear(Color.Transparent);

        try
        {
            Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            try
            {
                drawGlass(Batch);
            } finally
            {
                Batch.End();
            }

            Batch.Begin(SpriteSortMode.Deferred, SourceAtop, SamplerState.PointClamp);

            try
            {
                drawReflections(Batch);
            } finally
            {
                Batch.End();
            }
        } finally
        {
            Device.SetRenderTarget(previous);
        }

        LayerReady = true;
    }

    private RenderTarget2D? CurrentTarget()
    {
        var bindings = Device.GetRenderTargets();

        return bindings.Length > 0 ? bindings[0].RenderTarget as RenderTarget2D : null;
    }

    private RenderTarget2D EnsureTarget(RenderTarget2D? target, int width, int height)
    {
        if (target is not null && (target.Width == width) && (target.Height == height))
            return target;

        target?.Dispose();

        return new RenderTarget2D(Device, width, height);
    }

    private Texture2D BuildDiamond()
    {
        const int W = 56;
        const int H = 27;
        var data = new Color[W * H];

        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
                if ((Math.Abs(x + 0.5f - W / 2f) / (W / 2f)) + (Math.Abs(y + 0.5f - H / 2f) / (H / 2f)) <= 1f)
                    data[y * W + x] = Color.White;

        var texture = new Texture2D(Device, W, H);
        texture.SetData(data);

        return texture;
    }

    private Texture2D BuildGlass(MirrorSide side)
    {
        const int W = MirrorGeometry.FACE_WIDTH;
        const int H = MirrorGeometry.CANVAS_HEIGHT;
        var data = new Color[W * H];

        for (var row = 0; row < H; row++)
        {
            var localRow = row + MirrorGeometry.CANVAS_TOP;

            for (var column = 0; column < W; column++)
                if (MirrorGeometry.IsGlassPixel(side, column, localRow))
                {
                    var v = Math.Min(1f, 0.55f + 0.6f * MirrorGeometry.GlassHeightFraction(side, column, localRow));
                    data[row * W + column] = new Color(v, v, v, 1f);
                }
        }

        var texture = new Texture2D(Device, W, H);
        texture.SetData(data);

        return texture;
    }

    private Texture2D BuildGlow()
    {
        const int W = 64;
        const int H = 32;
        var data = new Color[W * H];

        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                var dx = (x + 0.5f - W / 2f) / (W / 2f);
                var dy = (y + 0.5f - H / 2f) / (H / 2f);
                var a = Math.Clamp(1f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                a *= a;
                data[y * W + x] = new Color(a, a, a, a);
            }

        var texture = new Texture2D(Device, W, H);
        texture.SetData(data);

        return texture;
    }

    private Texture2D BuildPixel()
    {
        var texture = new Texture2D(Device, 1, 1);
        texture.SetData(new[] { Color.White });

        return texture;
    }
}
```

- [ ] **Step 2: Create and dispose it**

In `CLIENT/Chaos.Client/Screens/WorldScreen.cs`, directly after `SilhouetteRenderer = new SilhouetteRenderer(graphicsDevice);`, insert `MirrorRenderer = new MirrorRenderer(graphicsDevice);`. Directly after `SilhouetteRenderer.Dispose();`, insert `MirrorRenderer.Dispose();`.

- [ ] **Step 3: Add the drawing to the mirrors partial**

Replace the whole of `CLIENT/Chaos.Client/Screens/WorldScreen.Mirrors.cs` with:

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Mirrors: the layout and doubles from the server, and their drawing. Before the world pass,
///     <see cref="PreRenderMirrors" /> paints the characters the mirrors need into the atlas and composes all visible glass
///     into the layer; during the stripe pass, <see cref="DrawMirrorTile" /> pastes each wall tile's slice and frame.
/// </summary>
public sealed partial class WorldScreen
{
    private static readonly Color GlassTint = new(190, 205, 255);
    private static readonly Color FunhouseTallTint = new(255, 200, 235);
    private static readonly Color FunhouseWideTint = new(200, 255, 225);
    private static readonly Color FunhouseWaveTint = new(230, 210, 255);
    private static readonly Color HauntedTint = new(185, 225, 215);
    private static readonly Color GhostTint = new(170, 255, 190);
    private static readonly Color WindowTint = new(220, 220, 235);

    private readonly List<WorldEntity> MirrorCandidates = [];
    private readonly Dictionary<ulong, WorldEntity> MirrorCellEntities = [];
    private readonly List<MirrorPlacement> MirrorPlacements = [];
    private readonly EntityTrail MirrorTrail = new();
    private readonly List<int> VisibleMirrorSegments = [];
    private MirrorRenderer MirrorRenderer = null!;

    private void WireMirrors()
    {
        Game.Connection.OnMirrorLayout += HandleMirrorLayout;
        Game.Connection.OnMirrorDouble += HandleMirrorDouble;
    }

    private void UnwireMirrors()
    {
        Game.Connection.OnMirrorLayout -= HandleMirrorLayout;
        Game.Connection.OnMirrorDouble -= HandleMirrorDouble;
    }

    private static void HandleMirrorLayout(MirrorLayoutArgs args) => WorldState.Mirrors.Apply(args);

    private static void HandleMirrorDouble(MirrorDoubleArgs args)
        => WorldState.Mirrors.AddDouble(args.EntityId, args.SegmentIndex, Environment.TickCount64);

    /// <summary>Forgets the mirrors. Called on a real map change only; maps without mirrors send no layout at all.</summary>
    private void ResetMirrors()
    {
        WorldState.Mirrors.Clear();
        MirrorTrail.Clear();
    }

    private static Vector2 EntityTile(WorldEntity entity) => new Vector2(entity.TileX, entity.TileY) + MirrorMath.OffsetToTiles(entity.VisualOffset);

    private static ulong MirrorCellKey(uint entityId, Direction facing, bool idle)
        => ((ulong)entityId << 8) | ((ulong)facing << 1) | (idle ? 1UL : 0UL);

    /// <summary>Screen-local top-left of a face's 28x89 canvas, on whole pixels so the layer slice and frame line up.</summary>
    private Vector2 MirrorFaceOrigin(Point wall, MirrorSide side)
    {
        var tile = Camera.WorldToScreen(Camera.TileToWorld(wall.X, wall.Y, MapFile!.Height));

        return new Vector2(
            MathF.Floor(tile.X) + MirrorGeometry.FaceLocalX(side),
            MathF.Floor(tile.Y) + MirrorGeometry.CANVAS_TOP);
    }

    /// <summary>Once a frame, before the world pass: records positions, places reflections, paints the atlas and the layer.</summary>
    private void PreRenderMirrors(IReadOnlyList<WorldEntity> sortedEntities)
    {
        var mirrors = WorldState.Mirrors;

        if (!mirrors.HasMirrors || MapFile is null)
            return;

        var nowMs = Environment.TickCount64;
        var seconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        MirrorCandidates.Clear();

        foreach (var entity in sortedEntities)
            if (entity.Type is ClientEntityType.Aisling or ClientEntityType.Creature && !entity.IsHidden)
            {
                MirrorCandidates.Add(entity);
                MirrorTrail.Record(entity.Id, nowMs, EntityTile(entity), entity.Direction);
            }

        MirrorTrail.Prune(nowMs);
        CollectVisibleMirrorSegments();
        CollectMirrorPlacements(nowMs, seconds);

        MirrorRenderer.BeginFrame();
        MirrorCellEntities.Clear();

        foreach (var placement in MirrorPlacements)
            ReserveMirrorCell(placement.EntityId, placement.Facing, placement.Idle);

        CollectMirrorDoubles(nowMs);

        MirrorRenderer.RenderAtlas(PaintMirrorCells);
        MirrorRenderer.RenderLayer(DrawMirrorGlass, batch => DrawMirrorReflections(batch, seconds));
    }

    private void CollectVisibleMirrorSegments()
    {
        VisibleMirrorSegments.Clear();

        (var minX, var minY, var maxX, var maxY) = Camera.GetVisibleTileBounds(MapFile!.Width, MapFile.Height, MapRenderer.ForegroundExtraMargin);
        var segments = WorldState.Mirrors.Segments;

        for (var i = 0; i < segments.Count; i++)
            for (var k = 0; k < segments[i].Length; k++)
            {
                var wall = MirrorMath.WallTile(segments[i], k);

                if ((wall.X >= minX - 2) && (wall.X <= maxX + 2) && (wall.Y >= minY - 2) && (wall.Y <= maxY + 2))
                {
                    VisibleMirrorSegments.Add(i);

                    break;
                }
            }
    }

    private void CollectMirrorPlacements(long nowMs, double seconds)
    {
        MirrorPlacements.Clear();

        var player = WorldState.GetPlayerEntity();
        var centre = player is null ? Vector2.Zero : EntityTile(player);
        var segments = WorldState.Mirrors.Segments;

        void Add(WorldEntity entity, Direction facing, bool idle, Vector2 tile, float scaleX, float scaleY, bool ripple, Color tint, float alpha)
            => MirrorPlacements.Add(
                new MirrorPlacement(
                    entity.Id,
                    facing,
                    idle,
                    tile,
                    scaleX,
                    scaleY,
                    ripple,
                    tint,
                    alpha,
                    Vector2.Distance(EntityTile(entity), centre)));

        foreach (var index in VisibleMirrorSegments)
        {
            var segment = segments[index];

            if (segment.Style == MirrorStyle.Window)
            {
                if (segment.PartnerIndex >= segments.Count)
                    continue;

                var partner = segments[segment.PartnerIndex];

                foreach (var entity in MirrorCandidates)
                    if (MirrorMath.IsInFront(partner, entity.TileX, entity.TileY, MirrorMath.REFLECT_DEPTH, MirrorMath.REFLECT_MARGIN))
                        Add(
                            entity,
                            MirrorMath.ReflectFacing(partner.Side, entity.Direction),
                            false,
                            MirrorMath.WindowPoint(segment, partner, EntityTile(entity)),
                            1,
                            1,
                            false,
                            WindowTint,
                            MirrorMath.WINDOW_ALPHA);

                continue;
            }

            var slip = segment.Style == MirrorStyle.Haunted ? MirrorMath.HauntedSlipAt(segment.Id, seconds) : (HauntedSlip.None, 0d);

            foreach (var entity in MirrorCandidates)
            {
                if (!MirrorMath.IsInFront(segment, entity.TileX, entity.TileY, MirrorMath.REFLECT_DEPTH, MirrorMath.REFLECT_MARGIN))
                    continue;

                var tile = EntityTile(entity);
                var facing = MirrorMath.ReflectFacing(segment.Side, entity.Direction);

                switch (segment.Style)
                {
                    case MirrorStyle.Funhouse:
                    {
                        (var sx, var sy) = MirrorMath.FunhouseScale(segment.Funhouse);

                        var tint = segment.Funhouse switch
                        {
                            MirrorFunhouse.Tall => FunhouseTallTint,
                            MirrorFunhouse.Wide => FunhouseWideTint,
                            _                   => FunhouseWaveTint
                        };

                        Add(entity, facing, false, MirrorMath.ReflectPoint(segment, tile), sx, sy, true, tint, MirrorMath.GLASS_ALPHA);

                        break;
                    }
                    case MirrorStyle.Endless:
                    {
                        var reflected = MirrorMath.ReflectPoint(segment, tile);

                        for (var copy = 0; copy < MirrorMath.ENDLESS_COPIES; copy++)
                        {
                            var scale = MirrorMath.EndlessScale(copy);

                            Add(
                                entity,
                                MirrorMath.EndlessFacing(copy, entity.Direction, facing),
                                false,
                                MirrorMath.EndlessCopy(segment.Side, reflected, copy),
                                scale,
                                scale,
                                false,
                                GlassTint,
                                MirrorMath.EndlessAlpha(copy));
                        }

                        break;
                    }
                    case MirrorStyle.Haunted:
                    {
                        var at = tile;
                        var idle = false;
                        var tint = HauntedTint;
                        var alpha = MirrorMath.GLASS_ALPHA;

                        switch (slip.Item1)
                        {
                            case HauntedSlip.Lag when MirrorTrail.TryGet(
                                entity.Id,
                                nowMs - (long)(MirrorMath.HAUNTED_LAG_SECONDS * 1000),
                                out var past,
                                out var pastFacing):
                                at = past;
                                facing = MirrorMath.ReflectFacing(segment.Side, pastFacing);

                                break;
                            case HauntedSlip.Stare:
                                if (MirrorTrail.TryGet(entity.Id, nowMs - (long)(slip.Item2 * 1000), out var then, out _))
                                    at = then;

                                facing = MirrorMath.FacingOutOf(segment.Side);
                                idle = true;

                                break;
                            case HauntedSlip.Ghost:
                                tint = GhostTint;
                                alpha = MirrorMath.GhostAlpha(seconds);

                                break;
                        }

                        Add(entity, facing, idle, MirrorMath.ReflectPoint(segment, at), 1, 1, false, tint, alpha);

                        break;
                    }
                    default:
                        Add(entity, facing, false, MirrorMath.ReflectPoint(segment, tile), 1, 1, false, GlassTint, MirrorMath.GLASS_ALPHA);

                        break;
                }
            }
        }

        //the cap keeps the nearest; then paint far to near so nearer reflections overlap farther ones
        if (MirrorPlacements.Count > MirrorMath.SPRITE_CAP)
        {
            MirrorPlacements.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            MirrorPlacements.RemoveRange(MirrorMath.SPRITE_CAP, MirrorPlacements.Count - MirrorMath.SPRITE_CAP);
        }

        MirrorPlacements.Sort((a, b) => (a.Tile.X + a.Tile.Y).CompareTo(b.Tile.X + b.Tile.Y));
    }

    /// <summary>Doubles are added in Task 15. Until then this reserves nothing.</summary>
    private void CollectMirrorDoubles(long nowMs) { }

    private void ReserveMirrorCell(uint entityId, Direction facing, bool idle)
    {
        var key = MirrorCellKey(entityId, facing, idle);

        if (!MirrorRenderer.TryReserveCell(key, out _, out var isNew) || !isNew)
            return;

        var entity = WorldState.GetEntity(entityId);

        if (entity is null)
            return;

        var copy = entity.CopyForMirror(MirrorMath.MirrorCacheId(entityId, facing, idle));
        copy.Direction = facing;
        copy.VisualOffset = Vector2.Zero;

        if (idle)
        {
            copy.AnimState = EntityAnimState.Idle;
            copy.ActiveBodyAnimation = null;
        }

        MirrorCellEntities[key] = copy;
    }

    private void PaintMirrorCells()
    {
        foreach ((var key, var copy) in MirrorCellEntities)
        {
            if (!MirrorRenderer.TryGetCell(key, out var cell))
                continue;

            var feet = Camera.WorldToScreen(
                Camera.TileToWorld(copy.TileX, copy.TileY, MapFile!.Height)
                + new Vector2(DaLibConstants.HALF_TILE_WIDTH, DaLibConstants.HALF_TILE_HEIGHT));

            MirrorRenderer.PaintCell(cell, feet, batch => DrawEntity(batch, copy));
        }
    }

    private void DrawMirrorGlass(SpriteBatch batch)
    {
        var segments = WorldState.Mirrors.Segments;

        foreach (var index in VisibleMirrorSegments)
        {
            var segment = segments[index];

            var colour = segment.Style switch
            {
                MirrorStyle.Funhouse => new Color(74, 58, 96),
                MirrorStyle.Haunted  => new Color(47, 64, 64),
                MirrorStyle.Endless  => new Color(52, 66, 100),
                MirrorStyle.Window   => new Color(60, 60, 70),
                _                    => new Color(58, 70, 96)
            };

            var glass = MirrorRenderer.Glass(segment.Side);

            for (var k = 0; k < segment.Length; k++)
                batch.Draw(glass, MirrorFaceOrigin(MirrorMath.WallTile(segment, k), segment.Side), colour);
        }
    }

    private void DrawMirrorReflections(SpriteBatch batch, double seconds)
    {
        var atlas = MirrorRenderer.AtlasTexture;

        if (atlas is null)
            return;

        var anchor = new Vector2(MirrorRenderer.CELL_ANCHOR_X, MirrorRenderer.CELL_ANCHOR_Y);

        foreach (var p in MirrorPlacements)
        {
            if (!MirrorRenderer.TryGetCell(MirrorCellKey(p.EntityId, p.Facing, p.Idle), out var cell))
                continue;

            var feet = Camera.WorldToScreen(MirrorMath.TileCenterWorld(p.Tile, MapFile!.Height));
            var colour = p.Tint * p.Alpha;

            if (!p.Ripple)
            {
                batch.Draw(atlas, feet, cell, colour, 0f, anchor, new Vector2(p.ScaleX, p.ScaleY), SpriteEffects.None, 0f);

                continue;
            }

            for (var row = 0; row < MirrorRenderer.CELL_SIZE; row += 2)
            {
                var source = new Rectangle(cell.X, cell.Y + row, MirrorRenderer.CELL_SIZE, 2);

                var at = new Vector2(
                    feet.X - MirrorRenderer.CELL_ANCHOR_X * p.ScaleX + MirrorMath.RippleOffset(seconds, row),
                    feet.Y + (row - MirrorRenderer.CELL_ANCHOR_Y) * p.ScaleY);

                batch.Draw(atlas, at, source, colour, 0f, Vector2.Zero, new Vector2(p.ScaleX, p.ScaleY), SpriteEffects.None, 0f);
            }
        }

        //glints: a faint streak sweeping along each glass, endless or window run
        var segments = WorldState.Mirrors.Segments;

        foreach (var index in VisibleMirrorSegments)
        {
            var segment = segments[index];

            if (segment.Style is not (MirrorStyle.Glass or MirrorStyle.Endless or MirrorStyle.Window))
                continue;

            var fraction = MirrorMath.GlintFraction(seconds + MirrorMath.Fnv1a(segment.Id) % 9);

            if (fraction is null)
                continue;

            var step = segment.Side == MirrorSide.North ? new Vector2(28, 14) : new Vector2(-28, 14);
            var start = MirrorFaceOrigin(MirrorMath.WallTile(segment, 0), segment.Side) + new Vector2(14, 82);
            var at = start + step * (fraction.Value * segment.Length);

            batch.Draw(MirrorRenderer.Pixel, at, null, Color.White * 0.18f, 0.35f, new Vector2(0.5f, 1f), new Vector2(3, 70), SpriteEffects.None, 0f);
        }
    }

    /// <summary>During the stripe pass, right after wall tile (x, y)'s foreground: its faces' glass slices and frames.</summary>
    private void DrawMirrorTile(BatchBlendScope scope, int x, int y)
    {
        var faces = WorldState.Mirrors.FacesAt(x, y);

        if (faces.Count == 0)
            return;

        scope.Require(BlendState.AlphaBlend);

        foreach (var face in faces)
            DrawMirrorFace(scope.Batch, new Point(x, y), face.Side);
    }

    private void DrawMirrorFace(SpriteBatch batch, Point wall, MirrorSide side)
    {
        var layer = MirrorRenderer.LayerTexture;

        if (layer is null)
            return;

        var origin = MirrorFaceOrigin(wall, side);
        var source = new Rectangle((int)origin.X, (int)origin.Y, MirrorGeometry.FACE_WIDTH, MirrorGeometry.CANVAS_HEIGHT);
        var clipped = Rectangle.Intersect(source, layer.Bounds);

        if (clipped.Width > 0 && clipped.Height > 0)
            batch.Draw(layer, new Vector2(clipped.X, clipped.Y), clipped, Color.White);

        var ui = UiRenderer.Instance;
        var frame = ui?.GetSpfTexture(side == MirrorSide.North ? "mirpnl01.spf" : "mirpnl02.spf");

        if (frame is not null && !ReferenceEquals(frame, ui!.MissingTexture))
            batch.Draw(frame, origin, Color.White);
    }

    private readonly record struct MirrorPlacement(
        uint EntityId,
        Direction Facing,
        bool Idle,
        Vector2 Tile,
        float ScaleX,
        float ScaleY,
        bool Ripple,
        Color Tint,
        float Alpha,
        float Distance);
}
```

Notes for the implementer:
- `DrawEntity` is the existing private method in `WorldScreen.Draw.cs`. It also adds hit boxes; that is harmless here because `DrawForegroundAndEntities` clears `EntityHitBoxes` before the stripe pass.
- If `ClientEntityType`, `EntityAnimState` or `BodyAnimation` live in a namespace not covered by these `using` lines, copy the matching `using` from `WorldScreen.Draw.cs`.
- `UiRenderer.Instance.GetSpfTexture` caches by file name, so the per-face lookup is a dictionary hit.

- [ ] **Step 4: Hook it into the draw**

In `CLIENT/Chaos.Client/Screens/WorldScreen.Draw.cs`:

1. In `Draw`, inside the first `if (MapFile is not null && MapPreloaded)` block, directly after the `SilhouetteRenderer.PreRenderSilhouettes(...);` statement ends, insert:

```csharp

            //mirrors: paint the characters they need into the atlas and compose the glass layer. like the silhouettes,
            //this must happen before the main target's drawing starts, because switching targets discards it
            PreRenderMirrors(sortedEntities);
```

2. In `DrawForegroundAndEntities`, before the `for (var depth = minDepth; ...)` loop, insert:

```csharp
        var mirrorsOn = WorldState.Mirrors.HasMirrors && MirrorRenderer.LayerReady;
```

and inside the `for (var tileX = tileXStart; ...)` loop, directly after the `MapRenderer.DrawForegroundTile(...)` call, insert:

```csharp
                if (mirrorsOn)
                    DrawMirrorTile(scope, tileX, depth - tileX);
```

Because that loop body is now two statements, wrap it in braces.

- [ ] **Step 5: Build and run the client tests**

Run the Verify commands. Expected: `0 Error(s)`; all client tests pass.

```json:metadata
{"files": ["Chaos.Client.Rendering/MirrorRenderer.cs", "Chaos.Client/Screens/WorldScreen.Mirrors.cs", "Chaos.Client/Screens/WorldScreen.Draw.cs", "Chaos.Client/Screens/WorldScreen.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx", "acceptanceCriteria": ["client builds with 0 errors and client tests pass", "no mirror work past HasMirrors on maps without mirrors", "render targets reused while back buffer size unchanged", "40 nearest reflections drawn", "missing frame sprite skipped, glass still drawn"], "modelTier": "standard"}
```

---

### Task 15: Doubles and dark stretches

**Goal:** A player's double climbs out of a haunted mirror, follows them and fades, and dark stretches go nearly black with a faint glow at each player's feet while their mirrors keep shining.

**Files:**
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.Mirrors.cs` (`CollectMirrorDoubles`, `DrawMirrorDoubles`, `DrawDarkStretches`)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.Draw.cs` (call both after the silhouettes)

**Acceptance Criteria:**
- [ ] Each active double reserves its atlas cell during the prerender and is drawn after the silhouettes: climbing from its reflected spot 0–0.6 s, following 0.45 s behind 0.6–6.6 s, fading 6.6–7.6 s, tinted `rgb(200,230,255)` at up to 0.45 opacity
- [ ] A double whose owner is no longer known is dropped at once
- [ ] Each dark stretch, plus one tile north and west of it, is drawn 95% black; each player inside gets a soft warm glow; mirror faces whose front tile is inside a stretch are drawn again on top
- [ ] The client builds with `0 Error(s)` and all client tests pass

**Verify:** from CLIENT, with `UnoraServerPath` set: `dotnet build Chaos.Client.slnx` → `0 Error(s)`; then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Collect doubles during the prerender**

In `WorldScreen.Mirrors.cs`, add these fields next to the others:

```csharp
    private static readonly Color DoubleTint = new(200, 230, 255);
    private const float DARK_STRETCH_OPACITY = 0.95f;
    private readonly List<(ulong Key, Vector2 Tile, float Alpha)> MirrorDoubleDraws = [];
```

Replace the placeholder `CollectMirrorDoubles` with:

```csharp
    /// <summary>Works out where each double is now and reserves its atlas cell. Drawn later by <see cref="DrawMirrorDoubles" />.</summary>
    private void CollectMirrorDoubles(long nowMs)
    {
        MirrorDoubleDraws.Clear();

        var mirrors = WorldState.Mirrors;
        mirrors.PruneDoubles(nowMs);

        foreach (var d in mirrors.Doubles.ToList())
        {
            var owner = WorldState.GetEntity(d.EntityId);

            if (owner is null)
            {
                mirrors.RemoveDoublesOf(d.EntityId);

                continue;
            }

            (var phase, var progress, var alpha) = MirrorMath.DoubleAt((nowMs - d.StartMs) / 1000.0);

            if (phase == DoublePhase.Done)
                continue;

            var segment = mirrors.Segments[d.SegmentIndex];

            if (!MirrorTrail.TryGet(owner.Id, nowMs - (long)(MirrorMath.DOUBLE_LAG_SECONDS * 1000), out var follow, out var followFacing))
            {
                follow = EntityTile(owner);
                followFacing = owner.Direction;
            }

            var tile = follow;
            var facing = followFacing;

            if (phase == DoublePhase.Climbing)
            {
                if (!MirrorTrail.TryGet(owner.Id, d.StartMs, out var startTile, out _))
                    startTile = EntityTile(owner);

                tile = Vector2.Lerp(MirrorMath.ReflectPoint(segment, startTile), follow, progress);
                facing = MirrorMath.FacingOutOf(segment.Side);
            }

            ReserveMirrorCell(owner.Id, facing, false);
            MirrorDoubleDraws.Add((MirrorCellKey(owner.Id, facing, false), tile, alpha));
        }
    }
```

It runs after the placements reserve their cells and before `RenderAtlas`, as `PreRenderMirrors` already orders it. `MirrorTrail` keeps 2 s of samples, so for the first 2 s it has `d.StartMs`; later the climb is over.

- [ ] **Step 2: Draw doubles and dark stretches**

Add to `WorldScreen.Mirrors.cs`:

```csharp
    /// <summary>After the silhouettes: the doubles, faint and pale, over the world.</summary>
    private void DrawMirrorDoubles(BatchBlendScope scope)
    {
        var atlas = MirrorRenderer.AtlasTexture;

        if ((MirrorDoubleDraws.Count == 0) || atlas is null || !WorldState.Mirrors.HasMirrors)
            return;

        scope.Require(BlendState.AlphaBlend);
        var anchor = new Vector2(MirrorRenderer.CELL_ANCHOR_X, MirrorRenderer.CELL_ANCHOR_Y);

        foreach ((var key, var tile, var alpha) in MirrorDoubleDraws)
        {
            if (!MirrorRenderer.TryGetCell(key, out var cell))
                continue;

            var feet = Camera.WorldToScreen(MirrorMath.TileCenterWorld(tile, MapFile!.Height));
            scope.Batch.Draw(atlas, feet, cell, DoubleTint * alpha, 0f, anchor, 1f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    ///     After the doubles: each dark stretch (and one tile north and west of it, where heads and walls rise) nearly
    ///     black, a warm glow at each player's feet inside, then the stretch's mirrors again at full brightness.
    /// </summary>
    private void DrawDarkStretches(BatchBlendScope scope)
    {
        var mirrors = WorldState.Mirrors;

        if ((mirrors.DarkStretches.Count == 0) || MapFile is null)
            return;

        scope.Require(BlendState.AlphaBlend);
        var shade = Color.Black * DARK_STRETCH_OPACITY;

        foreach (var s in mirrors.DarkStretches)
            for (var y = s.Y - 1; y < s.Y + s.Height; y++)
                for (var x = s.X - 1; x < s.X + s.Width; x++)
                {
                    if ((x < 0) || (y < 0) || (x >= MapFile.Width) || (y >= MapFile.Height))
                        continue;

                    scope.Batch.Draw(MirrorRenderer.Diamond, Camera.WorldToScreen(Camera.TileToWorld(x, y, MapFile.Height)), shade);
                }

        scope.Require(BlendState.Additive);
        var glowOrigin = new Vector2(32, 16);

        foreach (var entity in MirrorCandidates)
            if ((entity.Type == ClientEntityType.Aisling) && mirrors.IsInDarkStretch(entity.TileX, entity.TileY))
                scope.Batch.Draw(
                    MirrorRenderer.Glow,
                    Camera.WorldToScreen(MirrorMath.TileCenterWorld(EntityTile(entity), MapFile.Height)),
                    null,
                    new Color(90, 70, 50) * 0.6f,
                    0f,
                    glowOrigin,
                    1f,
                    SpriteEffects.None,
                    0f);

        scope.Require(BlendState.AlphaBlend);

        if (!MirrorRenderer.LayerReady)
            return;

        foreach (var index in VisibleMirrorSegments)
        {
            var segment = mirrors.Segments[index];

            for (var k = 0; k < segment.Length; k++)
            {
                var wall = MirrorMath.WallTile(segment, k);
                var front = segment.Side == MirrorSide.North ? new Point(wall.X, wall.Y + 1) : new Point(wall.X + 1, wall.Y);

                if (mirrors.IsInDarkStretch(front.X, front.Y))
                    DrawMirrorFace(scope.Batch, wall, segment.Side);
            }
        }
    }
```

- [ ] **Step 3: Call them after the silhouettes**

In `WorldScreen.Draw.cs`, in `Draw`, directly after `SilhouetteRenderer.DrawSilhouettes(BlendScope.Batch);`, insert:

```csharp

                //mirror doubles, then dark stretches: after the silhouettes, so the dark also hides silhouettes inside it
                DrawMirrorDoubles(BlendScope);
                DrawDarkStretches(BlendScope);
```

- [ ] **Step 4: Build and test**

Run the Verify commands. Expected: `0 Error(s)`; all client tests pass.

```json:metadata
{"files": ["Chaos.Client/Screens/WorldScreen.Mirrors.cs", "Chaos.Client/Screens/WorldScreen.Draw.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx", "acceptanceCriteria": ["doubles reserve cells in prerender and draw after silhouettes with the climb/follow/fade timeline and tint", "doubles of unknown owners dropped", "dark stretches 95% black plus a ring of one tile, glow per player, mirrors redrawn on top", "client builds with 0 errors and tests pass"], "modelTier": "standard"}
```

---

### Task 16: Documentation

**Goal:** Unora has a `docs/mirror-maze.md` that explains the maze to a newcomer and to a developer, and the client's `CLAUDE.md` names the new classes and draw steps.

**Files:**
- Create: `UNORA/docs/mirror-maze.md`
- Modify: `CLIENT/CLAUDE.md`

**Acceptance Criteria:**
- [ ] `docs/mirror-maze.md` covers: what players see, the map and zones, `mirrors.json` fields and rules, the four styles and three tricks with their numbers, key pieces and prize with the timed-event names, the candy trade, how to regenerate the maze and rebuild the art, and the release order
- [ ] `CLAUDE.md` lists `MirrorRenderer`, `MirrorGeometry`, `MirrorMath`, `EntityTrail` and `MirrorState`, and the draw order shows the mirror slices (after each foreground tile) and the doubles and dark stretches (after the silhouettes)

**Verify:** `grep -c "mirrors.json\|generate_maze\|mirrormaze_daily_cd" UNORA/docs/mirror-maze.md` → 3 or more; `grep -n "MirrorRenderer\|MirrorState\|MirrorMath" CLIENT/CLAUDE.md` → at least three lines

**Steps:**

- [ ] **Step 1: Write `UNORA/docs/mirror-maze.md`**

Follow the house style of `docs/slot-machines.md`: a scope line, a table of which repo owns what, then "Part 1 — Laymen's terms" and "Part 2 — How it works". Write in plain English. Include, at least:

1. **Scope and repos:** Chaos-Server (rules, packets, scripts), Chaos.Client (drawing), Unora (map, content, tools).
2. **What players see:** the theatre's mirror (open Oct 4 – Nov 4), the glass lobby, three wings, wisps with key pieces, the heart behind its mirror door in the haunted wing, the chest, the candy, Thulin's trade.
3. **Map:** `suomi_mirror_maze`, template 10231, 58x58, 19x19 cells; zones; inside walls are invisible blocking tiles (foreground 1) with a dark floor (11507); outer north/west walls are mansion wallpaper; the generator is the only way to change the layout (edit `generate_maze.py`, run it, commit its outputs).
4. **`mirrors.json`:** every field (`id`, `x`, `y`, `side`, `length`, `style`, `funhouse`, `partner`, `darkStretches`), the meaning of north/west from the wall tile, the validation rules and that bad entries are logged and skipped, and that any map can have mirrors by adding the `mirrormap` script key and a `mirrors.json` (the theatre does).
5. **Styles and tricks:** the numbers from the plan's Global Constraints, the haunted schedule (17 s slots, 2–8 s start, 3 s slips, same on every client), doubles (server picks every 20–40 s, 2-minute cooldown, client-only picture), window pairs (≤ 10 tiles apart), dark stretches.
6. **Progress:** the timed events `mirrormaze_key_funhouse/endless/haunted` and `mirrormaze_daily_cd`, the legend mark `mirrormaze`, the emblem `mirrorwalker` (art 256), 20 candy a day, 100 candy per Macabre Box.
7. **Tools:** `generate_maze.py` (`--check`, `--plan`), `test_generate_maze.py`, `make_art.py`, `render_map.cs`, `preview_panels.py`, `build_art.py` (`--no-apply`), with one example command each.
8. **Release:** server first (CLIENT_VERSION went up), then client, with the `setoa.dat` art batch in the same launcher patch; Unora data with the server.

- [ ] **Step 2: Update `CLIENT/CLAUDE.md`**

- Under "Rendering Layer", after the `SilhouetteRenderer` line, add:
  `- **`MirrorRenderer`** -- Mirror maze drawing: an atlas of characters (one 160x160 cell per character and pose) and a back-buffer-sized layer of all visible glass and reflections, composed before the world pass (source-atop keeps reflections on the glass). **`MirrorGeometry`** -- where a mirror face's panel and glass sit in a wall tile; shared with Unora's `Tools/MirrorMaze/make_art.py`.`
- Under "Game Systems", add:
  `- **`MirrorMath`** -- Pure mirror maths: reflected tile and facing, endless/window/funhouse numbers, the haunted slip schedule (same on every client), the double timeline, mirrored-copy cache ids. **`EntityTrail`** -- 2 s of positions and facings per character, for haunted lag/stare and doubles.`
- Under "ViewModel", add: `- **`MirrorState`** -- The current map's mirror runs (indexed by wall tile), dark stretches and active doubles. Kept through a same-map refresh; cleared on a real map change and logout.`
- In the draw order list, after step 3 add: `3b. Mirror faces -- right after each wall tile's foreground, that tile's slice of the mirror layer and its frame sprite (`WorldScreen.Mirrors`)` and after step 4 add: `4b. Mirror doubles, then dark stretches (95% black, glow at players' feet, their mirrors redrawn)`.

- [ ] **Step 3: Check**

Run the Verify commands.

```json:metadata
{"files": ["docs/mirror-maze.md", "CLAUDE.md"], "verifyCommand": "grep -c \"mirrors.json\\|generate_maze\\|mirrormaze_daily_cd\" docs/mirror-maze.md", "acceptanceCriteria": ["mirror-maze.md covers players, map, mirrors.json, styles, tricks, progress, tools and release", "CLAUDE.md lists the new classes and draw steps"], "modelTier": "standard"}
```

---

### Task 17: Full verification

**Goal:** Every test suite and build passes with the whole feature in place, and the generated map and theatre data are consistent.

**Files:**
- None (checks only)

**Acceptance Criteria:**
- [ ] SERVER: `dotnet build Chaos.slnx` → `0 Error(s)`; the full `Chaos.Tests` run fails only GiveAbility and OnItemDroppedOn stackable
- [ ] CLIENT: build with `UnoraServerPath` → `0 Error(s)`; the full client test run passes
- [ ] UNORA: `python -m pytest Tools/MirrorMaze -q` → 12 passed; the JSON parse check prints nothing; `python Tools/MirrorMaze/generate_maze.py --check` prints the planning summary with no `PROBLEM:` lines and the generated files on disk match a fresh build (no git diff after re-running `generate_maze.py`)
- [ ] Each pass count is quoted in the task close

**Verify:** the commands in the steps, with their outputs quoted

**Steps:**

- [ ] **Step 1: Server**

Ask the user to stop any running server or client. From SERVER: `dotnet build Chaos.slnx`, then `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi`. Expected: `0 Error(s)`; failures only GiveAbility and OnItemDroppedOn stackable.

- [ ] **Step 2: Client**

From CLIENT: `UnoraServerPath=/c/Users/Michael/Documents/GitHub/worktrees/mirror-maze-server dotnet build Chaos.Client.slnx`, then the same prefix with `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`. Expected: `0 Error(s)`; all pass.

- [ ] **Step 3: Unora**

From UNORA:

```bash
python -m pytest Tools/MirrorMaze -q
python -c "import json,glob;[json.load(open(f,encoding='utf-8')) for f in glob.glob('Data/Configuration/**/*.json',recursive=True)]"
python Tools/MirrorMaze/generate_maze.py
git status --short Data/Configuration/MapData/lod10231.map Data/Configuration/Templates/Maps/10231.json "Data/Configuration/MapInstances/Temuair/Events/Halloween/Mirror_Maze"
```

Expected: `12 passed`; no output from the JSON check; the summary with no `PROBLEM:` lines. The `git status` lines show the files as new (`??`) but `git diff` on them is empty after the re-run (they were already the generator's output).

```json:metadata
{"files": [], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["server builds and only the two known test failures", "client builds and all client tests pass", "generator tests pass, JSON parses, generator check clean and outputs stable", "pass counts quoted"], "modelTier": "mechanical"}
```

---

### Task 18: In-game check with the user

**Goal:** The user sees every mirror style and trick, collects the pieces, claims the prize, and confirms the window and the trade, on a local server.

> **USER-ORDERED GATE — NON-SKIPPABLE.** This task was requested by the user in the current conversation. It MUST NOT be closed by walking around it, by declaring it "verified inline", or by substituting a cheaper check. Close only after every item in `acceptanceCriteria` has been re-validated independently, with output captured.

**Files:**
- None (temporary local settings only; nothing committed)

**Acceptance Criteria:**
- [ ] The user has walked past glass, funhouse (tall, wide, wave), haunted and endless mirrors and confirmed each looks right
- [ ] With a second character, both saw the same haunted slip at the same time, and both saw a double step out
- [ ] One character was seen through a window mirror by the other
- [ ] A dark stretch was crossed: dark floor, glow at the feet, bright mirrors
- [ ] Three pieces collected, the heart door opened, the chest gave 20 candy, the legend mark and the Mirror Walker emblem; a second claim was refused
- [ ] Thulin traded 100 candy for a Macabre Box (with candy given by an admin command if needed)
- [ ] A non-admin character found the theatre mirror cloudy and was sent to the temple from the maze outside the window (before 2026-10-04, this is the default)
- [ ] The user's confirmation for each item is quoted in the task close; anything that looked wrong became a fix before closing

**Verify:** the user's quoted confirmations

**Steps:**

- [ ] **Step 1: Run locally**

- Apply the art batch to the local client folder: from UNORA, `python Tools/MirrorMaze/build_art.py` (without `--no-apply`; it keeps a backup of `setoa.dat`). Ask the user first.
- Start the server from SERVER with its staging directory pointed at UNORA's `Data` folder, following the local-staging-directory memory. Do not commit that `appsettings.json` change.
- Start the client from CLIENT, following the client-worktree-version memory (its `Chaos-Server` submodule is empty; the build already used SERVER through `UnoraServerPath`).
- Before 2026-10-04 only admins get past `eventtiming` and the theatre mirror. Use an admin character for steps 2–6 and a normal one for step 7.

- [ ] **Step 2: Walk the user through the checklist**

Ask the user to do each acceptance item in order and say what they saw. Admin commands that save walking:
- `/traverse suomi_mirror_maze 29 2` (lobby arrival); `/traverse suomi_mirror_maze 28 38` (just south of the heart door; step up onto (28, 37) facing Up).
- Wisps: funhouse (1, 1), endless (40, 28), haunted (1, 49). Dark stretches: rows 49–50 (x 4–20) and rows 43–44 (x 37–53).
- `/create halloweencandy 100` for the Thulin trade.
- `/removeTimedEvent <name>, mirrormaze_daily_cd` to claim the prize again after the refusal check.
- Terminus → timed events should list the shards and "Mirror Maze Prize".

- [ ] **Step 3: Fix and re-check**

For each problem the user reports, fix it in the right worktree (re-running the relevant task's verify command), rebuild, and have them check that item again.

- [ ] **Step 4: Put the local client back**

If the user wants, restore `setoa.dat` from the backup `build_art.py` made, and revert the temporary staging directory.

```json:metadata
{"files": [], "verifyCommand": "", "acceptanceCriteria": ["user confirmed each mirror style", "same haunted slip and double seen on two characters", "window mirror shows the other character", "dark stretch looks right", "pieces, heart, chest prize, mark, emblem, refused second claim", "Thulin trade works", "non-admin sees cloudy mirror and is sent to the temple outside the window", "user confirmations quoted"], "userGate": true, "tags": ["user-gate"], "modelTier": "standard"}
```

---

### Task 19: Commit the full implementation

**Goal:** One commit per repo on `feat/mirror-maze`, with the client's submodule pointing at the server commit.

**Files:**
- All files created or changed by Tasks 1–16 (no build output, no local settings)

**Acceptance Criteria:**
- [ ] SERVER has one new commit on `feat/mirror-maze` with every server file from Tasks 1–6
- [ ] UNORA has one new commit with every Unora file from Tasks 7–9 and 16 (including `Tools/Emblems/art/custom/embl256.png` and the generated map files)
- [ ] CLIENT has one new commit with every client file from Tasks 10–16, the plan files, and `Chaos-Server` pointing at the SERVER commit
- [ ] `git status --short` in each worktree shows no tracked changes left (only untracked build output)
- [ ] Nothing is merged or pushed

**Verify:** `git -C <each worktree> log --oneline -2` and `git -C <each worktree> status --short`

**Steps:**

- [ ] **Step 1: Server**

From SERVER, check `CLIENT_VERSION` is still master's value + 1 (`git show master:Chaos.DarkAges/Definitions/CONSTANTS.cs | grep CLIENT_VERSION`). Then stage by explicit path:

```bash
git add Chaos.DarkAges/Definitions/Enums.cs Chaos.DarkAges/Definitions/CONSTANTS.cs \
  Chaos.Networking.Abstractions/Definitions/Enums.cs \
  Chaos.Networking/Entities/Server/MirrorLayoutArgs.cs Chaos.Networking/Entities/Server/MirrorDoubleArgs.cs \
  Chaos.Networking/Converters/Server/MirrorLayoutConverter.cs Chaos.Networking/Converters/Server/MirrorDoubleConverter.cs \
  Chaos/Services/MirrorMaze Chaos/Collections/MapInstance.cs Chaos/Services/Storage/ExpiringMapInstanceCache.cs \
  Chaos/Networking/Abstractions/IChaosWorldClient.cs Chaos/Networking/ChaosWorldClient.cs Chaos/Models/World/EventPeriod.cs \
  Chaos/Scripting/MapScripts/Temuair/Events/MirrorMapScript.cs \
  Chaos/Scripting/ReactorTileScripts/Temauir/MirrorMaze \
  Chaos/Scripting/DialogScripts/Temuair/Events/HalloweenEvents/MirrorDoorDialogScript.cs \
  Chaos/Scripting/MerchantScripts/Events/Halloween/MirrorMazeWispScript.cs Chaos/Scripting/MerchantScripts/Events/Halloween/MirrorMazeChestScript.cs \
  Chaos/Scripting/DialogScripts/Temuair/Suomi/ThulinCandyTradeScript.cs Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusCheckTimedEventsScript.cs \
  Tests/Chaos.Tests/Networking/MirrorPacketConverterTests.cs Tests/Chaos.Tests/MirrorMaze
git commit -F - <<'EOF'
Add the Halloween mirror maze

Mirror layout and double packets (opcodes 146/147, CLIENT_VERSION up by
one), mirrors.json loading and validation, the mirror map script with
haunted doubles, mirror doors, key-piece wisps, the prize chest, and
Thulin's candy-for-Macabre-Box trade. The maze joins the Halloween
event window.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 2: Unora**

From UNORA, stage `Tools/MirrorMaze` (excluding any `__pycache__`), `Tools/Emblems/art/custom/embl256.png`, `Data/Configuration/MapData/lod10231.map`, `Data/Configuration/Templates/Maps/10231.json`, `Data/Configuration/MapInstances/Temuair/Events/Halloween/Mirror_Maze`, the four merchant templates, `halloweencandy.json`, both new dialogs, `thulin_initial.json`, `mirrorwalker.json`, the three theatre files and `docs/mirror-maze.md`, each by explicit path. If the worktree gathered `Custom Client Mods/.../obj` changes, leave them out (see the shared-checkouts memory). Commit with the message "Add the Halloween mirror maze map, content and tools" and the Co-Authored-By line.

- [ ] **Step 3: Client**

From CLIENT:

```bash
git update-index --cacheinfo 160000,$(git -C /c/Users/Michael/Documents/GitHub/worktrees/mirror-maze-server rev-parse HEAD),Chaos-Server
git add Chaos.Client.Rendering/MirrorRenderer.cs Chaos.Client.Rendering/MirrorGeometry.cs \
  Chaos.Client/Systems/MirrorMath.cs Chaos.Client/Systems/EntityTrail.cs Chaos.Client/ViewModel/MirrorState.cs \
  Chaos.Client/Screens/WorldScreen.Mirrors.cs Chaos.Client/Screens/WorldScreen.Draw.cs Chaos.Client/Screens/WorldScreen.cs \
  Chaos.Client/Screens/WorldScreen.Map.cs Chaos.Client/Collections/WorldState.cs Chaos.Client/Models/WorldEntity.cs \
  Chaos.Client.Networking/Definitions/Delegates.cs Chaos.Client.Networking/ConnectionManager.cs \
  Tests/Chaos.Client.Tests/MirrorStateTests.cs Tests/Chaos.Client.Tests/MirrorMathTests.cs \
  Tests/Chaos.Client.Tests/WorldEntityMirrorCopyTests.cs Tests/Chaos.Client.Tests/EntityTrailTests.cs CLAUDE.md
git add -f docs/superpowers/plans/2026-09-30-mirror-maze.md docs/superpowers/plans/2026-09-30-mirror-maze.md.tasks.json
git commit -F - <<'EOF'
Draw the Halloween mirror maze

Mirror panels on every listed wall face, reflections in four styles
(glass, funhouse, haunted, endless) plus window mirrors, doubles that
climb out of haunted glass, and dark stretches. Mirrored copies get
their own render cache id. Bumps Chaos-Server to the mirror maze commit.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 4: Check**

Run the Verify commands. Expected: one new commit at the top of each; `status --short` shows no tracked changes. Then hand over to `superpowers-extended-cc:finishing-a-development-branch` for merging (server, then client, then Unora) and the release.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/mirror-maze-client log --oneline -2", "acceptanceCriteria": ["one server commit with all server files", "one Unora commit with all Unora files", "one client commit with all client files, plan files and the submodule at the server commit", "no tracked changes left", "nothing merged or pushed"], "modelTier": "mechanical"}
```
