# Pumpkin Live Carving Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Everyone on the haunted theatre map sees each pumpkin's carving appear live, as dark holes, while it is carved.

**Architecture:** A third pumpkin look state, `Carving`, carries the grid. The server sends it to everyone on the map on each accepted save that changes a grid, and `LookFor` returns it for claimed pumpkins during carving. The client paints carved pixels as dark holes, caches only the latest carving image per pumpkin, and saves every 600 ms while the grid changes.

**Tech Stack:** C# 14 / .NET 10, TUnit + FluentAssertions + Moq, MonoGame (client).

**Spec:** Chaos.Client `docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md`, section 12 (sections 10 and 11 still apply where 12 is silent).

## Global Constraints

- Work only in the worktrees, branch `feat/pumpkin-live` in each:
  - server: `C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server`
  - client: `C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-client`
  Never touch the shared checkouts (`GitHub\Chaos.Client`, `GitHub\Chaos.Client\Chaos-Server`, `GitHub\Unora`): other sessions have uncommitted work there.
- Never build the server and client at the same time. Build/test one, wait, then the other.
- The client worktree has an empty `Chaos-Server` submodule. Always build client projects with `-p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server`, and build `Chaos.Client/Chaos.Client.csproj` or the test project, never the `.slnx`.
- `PumpkinLookState.Carving = 2`. `Blank = 0` and `Lit = 1` keep their values.
- Hole colour: `new Color(58, 24, 8, 255)`. No glow, no flicker and no lantern for a carving look.
- Client save interval while changed: 600 ms (`PumpkinCarvingModel.SAVE_INTERVAL_MS`). Server `PumpkinCarving.SAVE_INTERVAL_MS` (500) is unchanged.
- `CLIENT_VERSION` stays 767.
- Two server tests already fail on master: `GiveAbility` and `OnItemDroppedOn` stackable. Leave them alone.
- Code comments only where the why is not obvious. No explanatory comments in test code.
- Commit strategy is at-end: implementer tasks do NOT commit. The final task commits.

**User decisions (already made):**
- "A" — the audience watches on the stage pumpkins (dark holes), not in a watch window.
- "Everyone sees" — carvers see the other pumpkins' carving too.
- Spec section 12 approved ("looks good").

---

### Task 1: Carving look state in the shared protocol

**Goal:** `PumpkinLookState.Carving` exists and the look message carries the grid for it, both ways.

**Files:**
- Modify: `worktrees/pumpkin-carving-server/Chaos.DarkAges/Definitions/Enums.cs` (enum `PumpkinLookState`, near line 1856)
- Modify: `worktrees/pumpkin-carving-server/Chaos.Networking/Converters/Server/PumpkinLookConverter.cs`
- Test: `worktrees/pumpkin-carving-server/Tests/Chaos.Tests/Networking/PumpkinCarvingPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `PumpkinLookState` has `Carving = 2`; `Blank = 0`, `Lit = 1` unchanged.
- [ ] A Carving look round-trips with its grid; serialized size is 4 + 1 + 39 = 44 bytes.
- [ ] Blank and Lit round-trips still pass.

**Verify:** `cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PumpkinCarvingPacketConverterTests/*"` → all pass.

**Steps:**

- [ ] **Step 1: Write the failing test** — add to `PumpkinCarvingPacketConverterTests`:

```csharp
    [Test]
    public void A_carving_look_carries_the_grid()
    {
        var carving = new PumpkinLookArgs
        {
            EntityId = 4023,
            State = PumpkinLookState.Carving,
            Grid = SampleGrid()
        };

        RoundTrip(new PumpkinLookConverter(), carving).Should().BeEquivalentTo(carving);
        Bytes(new PumpkinLookConverter(), carving).Should().HaveCount(44);
    }
```

- [ ] **Step 2: Run it and watch it fail** (compile error: `Carving` not defined). Then add the enum value so it compiles, and run again: it fails because the converter drops the grid.

```csharp
/// <summary>How one Pumpkin Carving pumpkin looks: blank skin, carved but unlit, or lit with its carving.</summary>
public enum PumpkinLookState : byte
{
    Blank = 0,
    Lit = 1,
    Carving = 2
}
```

- [ ] **Step 3: Make the converter carry the grid for every non-blank state.** In `PumpkinLookConverter.Deserialize`:

```csharp
            Grid = state == PumpkinLookState.Blank ? null : reader.ReadBytes(PumpkinGrid.BYTES)
```

and in `Serialize`:

```csharp
        if (args.State != PumpkinLookState.Blank)
            writer.WriteBytes(args.Grid ?? PumpkinGrid.Empty());
```

Also update the `PumpkinLookArgs.Grid` doc comment in `Chaos.Networking/Entities/Server/PumpkinLookArgs.cs` to "The carving; sent when <see cref="State" /> is Lit or Carving." and the `ServerOpCode.PumpkinLook` summary in `Chaos.Networking.Abstractions/Definitions/Enums.cs` to "How one Pumpkin Carving pumpkin looks: blank, carving (unlit) or lit. Blank carries no grid."

- [ ] **Step 4: Run the Verify command** → all `PumpkinCarvingPacketConverterTests` pass.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos.Networking/Converters/Server/PumpkinLookConverter.cs", "Chaos.Networking/Entities/Server/PumpkinLookArgs.cs", "Chaos.Networking.Abstractions/Definitions/Enums.cs", "Tests/Chaos.Tests/Networking/PumpkinCarvingPacketConverterTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/PumpkinCarvingPacketConverterTests/*\"", "acceptanceCriteria": ["PumpkinLookState has Carving = 2; Blank = 0, Lit = 1 unchanged", "A Carving look round-trips with its grid; 44 bytes", "Blank and Lit round-trips still pass"], "modelTier": "mechanical"}
```

---

### Task 2: Server sends live carving looks

**Goal:** During carving a claimed pumpkin's look is `Carving` with its grid, and each accepted save that changes a grid sends that look to everyone on the theatre map.

**Files:**
- Modify: `worktrees/pumpkin-carving-server/Chaos/Scripting/MapScripts/Temuair/Suomi/PumpkinCarvingMapScript.cs` (`HandleSave`, `LookFor`, `Lit`)
- Test: `worktrees/pumpkin-carving-server/Tests/Chaos.Tests/Theatre/PumpkinCarvingMapTests.cs`

**Acceptance Criteria:**
- [ ] `LookFor` on a claimed pumpkin during carving returns State `Carving` with the saved grid; on an unclaimed one returns `Blank` with a null grid; during voting returns `Lit` (unchanged).
- [ ] An accepted save that changes the grid sends one `Carving` look to every aisling on the map, the carver included.
- [ ] A save with an unchanged grid, a rate-limited save and a save from a non-owner send no look.
- [ ] All `PumpkinCarving*` tests pass.

**Verify:** `cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PumpkinCarving*/*"` → all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests.** In `PumpkinCarvingMapTests`, replace the test `Before_the_reveal_a_pumpkin_looks_blank` with these three:

```csharp
    [Test]
    public void During_carving_a_claimed_pumpkin_shows_its_carving_and_a_free_one_looks_blank()
    {
        var game = Create();
        game.Script.TryStart(game.Director);
        Carver(game, "Mira", 0, 10);

        var claimed = game.Script.LookFor(PumpkinAt(game, PumpkinCarving.PumpkinTiles[0])!)!;
        var free = game.Script.LookFor(PumpkinAt(game, PumpkinCarving.PumpkinTiles[1])!)!;

        claimed.State.Should().Be(PumpkinLookState.Carving);
        PumpkinGrid.CountCut(claimed.Grid).Should().Be(10);
        free.State.Should().Be(PumpkinLookState.Blank);
        free.Grid.Should().BeNull();
    }

    [Test]
    public void A_save_that_changes_the_carving_is_sent_to_everyone_on_the_map()
    {
        var game = Create();
        game.Script.TryStart(game.Director);
        var guest = Person(game, "Gus", new Point(14, 16));

        var mira = Carver(game, "Mira", 0, 10);

        Mock.Get(guest.Client).Verify(
            c => c.SendPumpkinLook(It.Is<PumpkinLookArgs>(a => (a.State == PumpkinLookState.Carving) && (PumpkinGrid.CountCut(a.Grid) == 10))),
            Times.Once());
        Mock.Get(mira.Client).Verify(c => c.SendPumpkinLook(It.Is<PumpkinLookArgs>(a => a.State == PumpkinLookState.Carving)), Times.Once());
        Mock.Get(game.Director.Client).Verify(c => c.SendPumpkinLook(It.Is<PumpkinLookArgs>(a => a.State == PumpkinLookState.Carving)), Times.Once());
    }

    [Test]
    public void An_unchanged_rejected_or_strangers_save_sends_nothing()
    {
        var game = Create();
        game.Script.TryStart(game.Director);
        var guest = Person(game, "Gus", new Point(14, 16));
        var mira = Carver(game, "Mira", 0, 10);

        game.Script.HandleSave(mira, new PumpkinCarvingSaveArgs { Done = false, Grid = Carved(12) });
        game.Clock.Advance(1);
        game.Script.HandleSave(mira, new PumpkinCarvingSaveArgs { Done = false, Grid = Carved(10) });
        game.Script.HandleSave(guest, new PumpkinCarvingSaveArgs { Done = false, Grid = Carved(20) });

        Mock.Get(guest.Client).Verify(c => c.SendPumpkinLook(It.Is<PumpkinLookArgs>(a => a.State == PumpkinLookState.Carving)), Times.Once());
    }
```

(The second save in the last test is at the same clock time as `Carver`'s save, so the 500 ms limit rejects it; the third is accepted but unchanged; the fourth comes from a non-owner.)

- [ ] **Step 2: Run the Verify command** → the three new tests fail (the claimed look is Blank; no look is sent).

- [ ] **Step 3: Implement.** In `PumpkinCarvingMapScript`, replace `HandleSave`:

```csharp
    /// <summary>A save from a carving window. A save that changes the carving is shown to everyone on the map at once.</summary>
    public void HandleSave(Aisling source, PumpkinCarvingSaveArgs args)
    {
        if (Round is not { } round || round.SlotOf(source.Id) is not { } slot)
            return;

        var before = round.Pumpkins[slot].Grid;

        if (!round.Save(source.Id, args.Grid, args.Done, NowMs) || before.AsSpan().SequenceEqual(round.Pumpkins[slot].Grid))
            return;

        if (Pumpkins[slot] is not { } pumpkin || LookFor(pumpkin) is not { } look)
            return;

        foreach (var aisling in Subject.GetEntities<Aisling>())
            aisling.Client.SendPumpkinLook(look);
    }
```

Replace `LookFor`:

```csharp
    /// <summary>How a pumpkin looks to a player, or null when the merchant isn't one of this game's pumpkins.</summary>
    public PumpkinLookArgs? LookFor(Merchant pumpkin)
    {
        if (ReferenceEquals(pumpkin, Display) && DisplayStorage.Value is { HasWinner: true } stored)
            return Look(pumpkin, PumpkinLookState.Lit, stored.Grid!);

        var slot = Array.IndexOf(Pumpkins, pumpkin);

        if (slot < 0)
            return null;

        return Round switch
        {
            { State: PumpkinRoundState.Voting } round => Look(pumpkin, PumpkinLookState.Lit, round.Pumpkins[slot].Grid),
            { State: PumpkinRoundState.Carving } round when round.Pumpkins[slot].OwnerId is not null
                => Look(pumpkin, PumpkinLookState.Carving, round.Pumpkins[slot].Grid),
            _ => new PumpkinLookArgs
            {
                EntityId = pumpkin.Id,
                State = PumpkinLookState.Blank
            }
        };
    }
```

Replace the private `Lit` helper with:

```csharp
    private static PumpkinLookArgs Look(Merchant pumpkin, PumpkinLookState state, byte[] grid)
        => new()
        {
            EntityId = pumpkin.Id,
            State = state,
            Grid = grid
        };
```

Update the class summary's "sends looks" phrase if needed so it does not say the grid stays hidden. Remove the comment `//the grid never leaves the server before the reveal`.

- [ ] **Step 4: Run the Verify command** → all `PumpkinCarving*` tests pass.

```json:metadata
{"files": ["Chaos/Scripting/MapScripts/Temuair/Suomi/PumpkinCarvingMapScript.cs", "Tests/Chaos.Tests/Theatre/PumpkinCarvingMapTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/PumpkinCarving*/*\"", "acceptanceCriteria": ["LookFor: claimed during carving -> Carving with grid; unclaimed -> Blank null grid; voting -> Lit", "Accepted changing save sends one Carving look to every aisling on the map", "Unchanged, rate-limited and non-owner saves send no look", "All PumpkinCarving* tests pass"], "modelTier": "standard"}
```

---

### Task 3: Client look store keeps the state and entity id

**Goal:** The client stores each pumpkin's look state (Blank, Carving, Lit) and entity id, and only a Lit look gets a lantern.

**Files:**
- Modify: `worktrees/pumpkin-carving-client/Chaos.Client.Rendering/PumpkinLook.cs`
- Modify: `worktrees/pumpkin-carving-client/Chaos.Client/Collections/PumpkinLookStore.cs`
- Modify: `worktrees/pumpkin-carving-client/Chaos.Client/Collections/WorldState.cs` (`ApplyPumpkinLook`, near line 811)
- Modify: `worktrees/pumpkin-carving-client/Chaos.Client/Screens/WorldScreen.PumpkinCarving.cs` (`HandlePumpkinLook`)
- Test: `worktrees/pumpkin-carving-client/Tests/Chaos.Client.Tests/PumpkinLookStoreTests.cs`

**Acceptance Criteria:**
- [ ] `PumpkinLook` is `(uint EntityId, PumpkinLookState State, byte[] Grid)` with `Lit => State == PumpkinLookState.Lit`; the unused `Version` is gone.
- [ ] `PumpkinLookStore.Apply(uint entityId, PumpkinLookState state, byte[]? grid)` keeps a copy of the grid for Carving and Lit, and an empty grid for Blank.
- [ ] A Carving look carries no lantern; a Lit look carries `LanternSize.Small`.
- [ ] The client builds and all `PumpkinLookStoreTests` pass.

**Verify:** `cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-client && dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/PumpkinLookStoreTests/*"` → build succeeds, all pass.

**Steps:**

- [ ] **Step 1: Rewrite the tests.** Replace the whole body of `PumpkinLookStoreTests` with:

```csharp
    [Test]
    public void Each_look_is_kept_by_id_with_its_state()
    {
        var store = new PumpkinLookStore();
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 2, 3, true);

        var blank = store.Apply(7, PumpkinLookState.Blank, null);
        var carving = store.Apply(7, PumpkinLookState.Carving, grid);

        store.Get(7).Should().Be(carving);
        carving.EntityId.Should().Be(7);
        carving.State.Should().Be(PumpkinLookState.Carving);
        carving.Lit.Should().BeFalse();
        PumpkinGrid.CountCut(blank.Grid).Should().Be(0);
        PumpkinGrid.IsCut(carving.Grid, 2, 3).Should().BeTrue();
        store.Get(8).Should().BeNull();
    }

    [Test]
    public void The_stored_grid_is_a_copy()
    {
        var store = new PumpkinLookStore();
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 0, 0, true);

        store.Apply(7, PumpkinLookState.Lit, grid);
        grid[0] = 0;

        PumpkinGrid.IsCut(store.Get(7)!.Grid, 0, 0).Should().BeTrue();
    }

    [Test]
    public void Removal_and_clear_drop_looks()
    {
        var store = new PumpkinLookStore();
        store.Apply(7, PumpkinLookState.Lit, PumpkinGrid.Empty());
        store.Apply(8, PumpkinLookState.Carving, PumpkinGrid.Empty());

        store.Remove(7);

        store.Get(7).Should().BeNull();

        store.Clear();

        store.Get(8).Should().BeNull();
    }

    [Test]
    public void Only_a_lit_pumpkin_carries_a_light()
    {
        var store = new PumpkinLookStore();

        PumpkinLookStore.LanternFor(store.Apply(7, PumpkinLookState.Lit, PumpkinGrid.Empty())).Should().Be(LanternSize.Small);
        PumpkinLookStore.LanternFor(store.Apply(8, PumpkinLookState.Carving, PumpkinGrid.Empty())).Should().Be(LanternSize.None);
        PumpkinLookStore.LanternFor(store.Apply(9, PumpkinLookState.Blank, null)).Should().Be(LanternSize.None);
        PumpkinLookStore.LanternFor(null).Should().Be(LanternSize.None);
    }
```

- [ ] **Step 2: Build the test project** (Verify command's build part) → compile errors (`Apply` takes `bool`; `EntityId`/`State` missing). That is the expected red.

- [ ] **Step 3: Implement.** `Chaos.Client.Rendering/PumpkinLook.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Rendering;

/// <summary>How one Pumpkin Carving pumpkin looks: blank, carving (dark holes) or lit (candle glow).</summary>
public sealed record PumpkinLook(uint EntityId, PumpkinLookState State, byte[] Grid)
{
    public bool Lit => State == PumpkinLookState.Lit;
}
```

`PumpkinLookStore`: remove the `Version` field and replace `Apply`:

```csharp
    public PumpkinLook Apply(uint entityId, PumpkinLookState state, byte[]? grid)
    {
        var look = new PumpkinLook(
            entityId,
            state,
            (state != PumpkinLookState.Blank) && grid is not null ? grid.ToArray() : PumpkinGrid.Empty());

        Looks[entityId] = look;

        return look;
    }
```

`WorldState.ApplyPumpkinLook`:

```csharp
    /// <summary>Records a pumpkin's look and gives a lit pumpkin its light.</summary>
    public static void ApplyPumpkinLook(uint entityId, PumpkinLookState state, byte[]? grid)
    {
        var look = PumpkinLooks.Apply(entityId, state, grid);

        if (Entities.TryGetValue(entityId, out var entity))
            entity.LanternSize = PumpkinLookStore.LanternFor(look);
    }
```

(add `using Chaos.DarkAges.Definitions;` to `WorldState.cs` if it is not already there).

`WorldScreen.PumpkinCarving.cs`:

```csharp
    private static void HandlePumpkinLook(PumpkinLookArgs args) => WorldState.ApplyPumpkinLook(args.EntityId, args.State, args.Grid);
```

`CreatureRenderer.Draw` still compiles because it reads `pumpkin is { Lit: true }`; Task 4 changes it.

- [ ] **Step 4: Run the Verify command** → build succeeds; all `PumpkinLookStoreTests` pass.

```json:metadata
{"files": ["Chaos.Client.Rendering/PumpkinLook.cs", "Chaos.Client/Collections/PumpkinLookStore.cs", "Chaos.Client/Collections/WorldState.cs", "Chaos.Client/Screens/WorldScreen.PumpkinCarving.cs", "Tests/Chaos.Client.Tests/PumpkinLookStoreTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-client && dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/PumpkinLookStoreTests/*\"", "acceptanceCriteria": ["PumpkinLook is (EntityId, State, Grid) with Lit => State == Lit; Version removed", "Apply(entityId, state, grid) copies the grid for Carving and Lit, empty for Blank", "Carving look has no lantern; Lit has LanternSize.Small", "Client builds and PumpkinLookStoreTests pass"], "modelTier": "standard"}
```

---

### Task 4: Paint carving pumpkins as dark holes with a replacing cache

**Goal:** A pumpkin with a Carving look draws its carved pixels as dark holes, and each pumpkin keeps only its latest carving image per frame.

**Files:**
- Modify: `worktrees/pumpkin-carving-client/Chaos.Client.Rendering/PumpkinPainter.cs`
- Create: `worktrees/pumpkin-carving-client/Chaos.Client.Rendering/PumpkinCarvingCache.cs`
- Modify: `worktrees/pumpkin-carving-client/Chaos.Client.Rendering/CreatureRenderer.cs` (fields, `Clear`, `Draw`, new `GetCarvingFrame`)
- Modify: `worktrees/pumpkin-carving-client/CLAUDE.md` (the `PumpkinFaceMap`/`PumpkinPainter` bullet)
- Test: `worktrees/pumpkin-carving-client/Tests/Chaos.Client.Tests/PumpkinPainterTests.cs`
- Test (create): `worktrees/pumpkin-carving-client/Tests/Chaos.Client.Tests/PumpkinCarvingCacheTests.cs`

**Acceptance Criteria:**
- [ ] `PumpkinPainter.PaintHoles` paints each face-map pixel with a cut cell as `PumpkinPainter.HoleColor` (58, 24, 8, 255) and leaves the others; `Paint` (glow) behaves as before.
- [ ] `PumpkinCarvingCache<T>.GetOrPaint` returns the cached value without painting when the grid is unchanged; on a changed grid it paints, releases the old value and keeps one entry per (entity, frame); `Clear` releases every value.
- [ ] `CreatureRenderer.Draw` uses the lit frame for Lit, the carving frame for Carving, and the plain frame for Blank or no look.
- [ ] The client builds and all `Pumpkin*` client tests pass.

**Verify:** `cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-client && dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/Pumpkin*/*"` → build succeeds, all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests.** Add to `PumpkinPainterTests`:

```csharp
    [Test]
    public void A_carving_shows_cut_pixels_as_dark_holes()
    {
        var map = new[] { new PumpkinFacePixel(1, 0, 0, [0, 1]), new PumpkinFacePixel(2, 0, 100, [5]) };
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 1, 0, true);
        var pixels = Enumerable.Repeat(Skin, 4).ToArray();

        PumpkinPainter.PaintHoles(pixels, 4, 1, 0, 0, map, grid);

        pixels[1].Should().Be(new Color(58, 24, 8, 255));
        pixels[1].Should().Be(PumpkinPainter.HoleColor);
        pixels[2].Should().Be(Skin);
    }
```

Create `Tests/Chaos.Client.Tests/PumpkinCarvingCacheTests.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class PumpkinCarvingCacheTests
{
    private sealed class Painted(int number)
    {
        public int Number { get; } = number;
    }

    private static byte[] Grid(int cuts)
    {
        var grid = PumpkinGrid.Empty();

        for (var i = 0; i < cuts; i++)
            PumpkinGrid.SetCut(grid, i % PumpkinGrid.WIDTH, i / PumpkinGrid.WIDTH, true);

        return grid;
    }

    [Test]
    public void An_unchanged_carving_is_not_repainted()
    {
        var released = new List<Painted>();
        var cache = new PumpkinCarvingCache<Painted>(released.Add);
        var paints = 0;

        var first = cache.GetOrPaint(7, 1, Grid(3), () => new Painted(++paints));
        var second = cache.GetOrPaint(7, 1, Grid(3), () => new Painted(++paints));

        second.Should().BeSameAs(first);
        paints.Should().Be(1);
        released.Should().BeEmpty();
    }

    [Test]
    public void A_new_carving_replaces_the_old_image_instead_of_adding_one()
    {
        var released = new List<Painted>();
        var cache = new PumpkinCarvingCache<Painted>(released.Add);
        var paints = 0;

        var old = cache.GetOrPaint(7, 1, Grid(3), () => new Painted(++paints))!;

        for (var cuts = 4; cuts < 40; cuts++)
            cache.GetOrPaint(7, 1, Grid(cuts), () => new Painted(++paints));

        cache.Count.Should().Be(1);
        released.Should().HaveCount(36).And.Contain(old);
    }

    [Test]
    public void Each_pumpkin_and_frame_has_its_own_entry_and_clear_releases_them()
    {
        var released = new List<Painted>();
        var cache = new PumpkinCarvingCache<Painted>(released.Add);

        cache.GetOrPaint(7, 0, Grid(3), () => new Painted(1));
        cache.GetOrPaint(7, 1, Grid(3), () => new Painted(2));
        cache.GetOrPaint(8, 1, Grid(3), () => new Painted(3));

        cache.Count.Should().Be(3);

        cache.Clear();

        cache.Count.Should().Be(0);
        released.Select(p => p.Number).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Test]
    public void The_cached_grid_is_a_copy()
    {
        var cache = new PumpkinCarvingCache<Painted>(_ => { });
        var grid = Grid(3);
        var paints = 0;

        cache.GetOrPaint(7, 1, grid, () => new Painted(++paints));
        grid[0] = 0;
        cache.GetOrPaint(7, 1, Grid(3), () => new Painted(++paints));

        paints.Should().Be(1);
    }
}
```

- [ ] **Step 2: Build the test project** → compile errors (`PaintHoles`, `HoleColor`, `PumpkinCarvingCache` missing). Expected red.

- [ ] **Step 3: Implement the painter.** In `PumpkinPainter`, add the hole colour and route both paints through one loop:

```csharp
    /// <summary>A carved pixel before the reveal: a dark hole, no candle yet.</summary>
    public static readonly Color HoleColor = new(58, 24, 8, 255);
```

Replace `Paint` with:

```csharp
    /// <summary>
    ///     Paints <paramref name="map" /> into a frame's pixels. The buffer's (0,0) is the frame's (<paramref name="left" />,
    ///     <paramref name="top" />).
    /// </summary>
    public static void Paint(
        Span<Color> pixels,
        int width,
        int height,
        int left,
        int top,
        IReadOnlyList<PumpkinFacePixel> map,
        ReadOnlySpan<byte> grid,
        int phase)
        => PaintCut(pixels, width, height, left, top, map, grid, glow => GlowColor(glow, phase));

    /// <summary>Paints a carving that is not lit yet: every pixel with a cut cell becomes <see cref="HoleColor" />.</summary>
    public static void PaintHoles(
        Span<Color> pixels,
        int width,
        int height,
        int left,
        int top,
        IReadOnlyList<PumpkinFacePixel> map,
        ReadOnlySpan<byte> grid)
        => PaintCut(pixels, width, height, left, top, map, grid, _ => HoleColor);

    private static void PaintCut(
        Span<Color> pixels,
        int width,
        int height,
        int left,
        int top,
        IReadOnlyList<PumpkinFacePixel> map,
        ReadOnlySpan<byte> grid,
        Func<int, Color> colorForGlow)
    {
        foreach (var pixel in map)
        {
            var x = pixel.X - left;
            var y = pixel.Y - top;

            if (((uint)x >= (uint)width) || ((uint)y >= (uint)height) || !AnyCut(pixel.Cells, grid))
                continue;

            pixels[(y * width) + x] = colorForGlow(pixel.Glow);
        }
    }
```

Update the class summary to mention both: "Paints a pumpkin's carving: lit (candle glow, brightest in the middle, with a slight flicker) or not yet lit (dark holes)."

- [ ] **Step 4: Create the cache** `Chaos.Client.Rendering/PumpkinCarvingCache.cs`:

```csharp
namespace Chaos.Client.Rendering;

/// <summary>
///     Painted frames for pumpkins being carved (spec 2026-10-03-pumpkin-carving-design.md, 12.6). A carving changes about
///     twice a second, so each (entity, frame) keeps only its latest image; the old one is released when the grid changes.
/// </summary>
public sealed class PumpkinCarvingCache<T> where T: class
{
    private readonly Dictionary<(uint EntityId, int FrameIndex), (byte[] Grid, T Value)> Entries = [];
    private readonly Action<T> Release;

    public PumpkinCarvingCache(Action<T> release) => Release = release;

    public int Count => Entries.Count;

    public void Clear()
    {
        foreach (var entry in Entries.Values)
            Release(entry.Value);

        Entries.Clear();
    }

    /// <summary>The cached image when the grid is unchanged; otherwise paints a new one, replacing and releasing the old.</summary>
    public T? GetOrPaint(uint entityId, int frameIndex, byte[] grid, Func<T?> paint)
    {
        var key = (entityId, frameIndex);

        if (Entries.TryGetValue(key, out var entry) && entry.Grid.AsSpan().SequenceEqual(grid))
            return entry.Value;

        if (paint() is not { } painted)
            return null;

        if (Entries.TryGetValue(key, out var old))
            Release(old.Value);

        Entries[key] = (grid.ToArray(), painted);

        return painted;
    }
}
```

- [ ] **Step 5: Use it in `CreatureRenderer`.** Add the field next to `PumpkinCache`, and fix `PumpkinCache`'s comment:

```csharp
    //painted lit Pumpkin Carving frames, per (carving, frame, flicker phase); never shares textures with FrameCache
    private readonly Dictionary<(string GridKey, int FrameIndex, int Phase), SpriteFrame> PumpkinCache = [];
    private readonly PumpkinCarvingCache<SpriteFrame> CarvingCache = new(frame => frame.Texture.Dispose());
```

In `Clear()`, after `PumpkinCache.Clear();` add `CarvingCache.Clear();`.

In `Draw`, replace the `spriteFrame` assignment:

```csharp
        var spriteFrame = pumpkin is not null && (spriteId == PumpkinFaceMap.SPRITE_ID)
            ? pumpkin.State switch
            {
                PumpkinLookState.Lit     => GetPumpkinFrame(spriteId, frameIndex, pumpkin, pumpkinPhase),
                PumpkinLookState.Carving => GetCarvingFrame(spriteId, frameIndex, pumpkin),
                _                        => GetFrame(spriteId, frameIndex)
            }
            : GetFrame(spriteId, frameIndex);
```

(add `using Chaos.DarkAges.Definitions;` at the top if missing). Add after `GetPumpkinFrame`:

```csharp
    /// <summary>A pumpkin being carved: its cuts as dark holes. Falls back to the plain frame where the face map has nothing.</summary>
    public SpriteFrame? GetCarvingFrame(int spriteId, int frameIndex, PumpkinLook look)
    {
        if (GetFrame(spriteId, frameIndex) is not { } plain)
            return null;

        var map = PumpkinFaceMap.Shared.For(frameIndex);

        //the plain frame belongs to FrameCache, so it must never enter CarvingCache (which disposes what it replaces)
        if (map.Count == 0)
            return plain;

        return CarvingCache.GetOrPaint(
            look.EntityId,
            frameIndex,
            look.Grid,
            () =>
            {
                using var scope = new PixelBufferScope(plain.Texture);
                PumpkinPainter.PaintHoles(scope.AsSpan(), scope.Width, scope.Height, plain.Left, plain.Top, map, look.Grid);

                var texture = new Texture2D(TextureConverter.Device, scope.Width, scope.Height);
                scope.CommitTo(texture);

                return new SpriteFrame(texture, plain.CenterX, plain.CenterY, plain.Left, plain.Top);
            });
    }
```

- [ ] **Step 6: Update `CLAUDE.md`.** Replace the bullet starting `- **`PumpkinFaceMap`/`PumpkinPainter`**` with:

```markdown
- **`PumpkinFaceMap`/`PumpkinPainter`/`PumpkinCarvingCache`** -- Pumpkin Carving: the embedded face map (which carving cells each pixel of sprite 1455 shows) and the painter, which lights carved pixels (Lit look) or shows them as dark holes (Carving look, while players carve). `CreatureRenderer.GetPumpkinFrame` caches lit frames by carving; `GetCarvingFrame` keeps only each pumpkin's latest carving image in `PumpkinCarvingCache`.
```

- [ ] **Step 7: Run the Verify command** → build succeeds; all `Pumpkin*` client tests pass.

```json:metadata
{"files": ["Chaos.Client.Rendering/PumpkinPainter.cs", "Chaos.Client.Rendering/PumpkinCarvingCache.cs", "Chaos.Client.Rendering/CreatureRenderer.cs", "CLAUDE.md", "Tests/Chaos.Client.Tests/PumpkinPainterTests.cs", "Tests/Chaos.Client.Tests/PumpkinCarvingCacheTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-client && dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/Pumpkin*/*\"", "acceptanceCriteria": ["PaintHoles paints cut face-map pixels HoleColor (58,24,8,255), others untouched; Paint unchanged", "PumpkinCarvingCache: unchanged grid -> cached, no paint; changed -> paint, release old, one entry per (entity, frame); Clear releases all", "Draw: Lit -> lit frame, Carving -> carving frame, Blank/none -> plain", "Client builds and all Pumpkin* client tests pass"], "modelTier": "standard"}
```

---

### Task 5: Save every 600 ms while carving

**Goal:** The carving window saves every 600 ms while the grid has changed, so the audience sees the carving almost live.

**Files:**
- Modify: `worktrees/pumpkin-carving-client/Chaos.Client/ViewModel/PumpkinCarvingModel.cs`
- Modify: `worktrees/pumpkin-carving-client/Chaos.Client/Controls/World/Popups/PumpkinCarving/PumpkinCarvingControl.cs` (constants, class summary, `Update`)
- Test: `worktrees/pumpkin-carving-client/Tests/Chaos.Client.Tests/PumpkinCarvingModelTests.cs`

**Acceptance Criteria:**
- [ ] `PumpkinCarvingModel.SAVE_INTERVAL_MS == 600` and `SaveDue(nowMs, lastSendMs)` is true only when the grid changed since the last send and at least 600 ms passed.
- [ ] `PumpkinCarvingControl.Update` sends a periodic save when `Model.SaveDue(now, LastSendMs)`; the constants `SAVE_MS`, `FAST_SAVE_MS` and `FAST_SAVE_SECONDS` are removed.
- [ ] The client builds and all `PumpkinCarvingModelTests` pass.

**Verify:** `cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-client && dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/PumpkinCarvingModelTests/*"` → build succeeds, all pass.

**Steps:**

- [ ] **Step 1: Write the failing test** — add to `PumpkinCarvingModelTests`:

```csharp
    [Test]
    public void A_save_is_due_600_ms_after_the_last_one_while_the_grid_has_changed()
    {
        var model = new PumpkinCarvingModel();

        model.SaveDue(10_000, 0).Should().BeFalse();

        Stroke(model, (4, 4));

        PumpkinCarvingModel.SAVE_INTERVAL_MS.Should().Be(600);
        model.SaveDue(1_599, 1_000).Should().BeFalse();
        model.SaveDue(1_600, 1_000).Should().BeTrue();

        model.TakeForSend();

        model.SaveDue(5_000, 1_600).Should().BeFalse();
    }
```

- [ ] **Step 2: Build** → compile error (`SaveDue`, `SAVE_INTERVAL_MS` missing). Expected red.

- [ ] **Step 3: Implement.** In `PumpkinCarvingModel` add (near `MAX_UNDO`):

```csharp
    /// <summary>How often the window saves while the grid has changed, so the audience sees the carving almost live.</summary>
    public const long SAVE_INTERVAL_MS = 600;
```

and next to `TakeForSend`:

```csharp
    public bool SaveDue(long nowMs, long lastSendMs) => HasUnsent && ((nowMs - lastSendMs) >= SAVE_INTERVAL_MS);
```

In `PumpkinCarvingControl`, delete the constants `FAST_SAVE_MS`, `FAST_SAVE_SECONDS` and `SAVE_MS`. In `Update`, replace

```csharp
        //near the end, save faster so little is lost when time runs out
        var interval = secondsLeft <= FAST_SAVE_SECONDS ? FAST_SAVE_MS : SAVE_MS;

        if (Model.HasUnsent && ((now - LastSendMs) >= interval))
            Send(closing: false);
```

with

```csharp
        if (Model.SaveDue(now, LastSendMs))
            Send(closing: false);
```

In the class summary, change "Saves to the server every few seconds while changed" to "Saves to the server every 600 ms while changed, so everyone watches the carving live".

- [ ] **Step 4: Run the Verify command** → build succeeds; all `PumpkinCarvingModelTests` pass.

```json:metadata
{"files": ["Chaos.Client/ViewModel/PumpkinCarvingModel.cs", "Chaos.Client/Controls/World/Popups/PumpkinCarving/PumpkinCarvingControl.cs", "Tests/Chaos.Client.Tests/PumpkinCarvingModelTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-client && dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/PumpkinCarvingModelTests/*\"", "acceptanceCriteria": ["SAVE_INTERVAL_MS == 600; SaveDue true only when changed and >= 600 ms since last send", "Control Update uses Model.SaveDue; SAVE_MS, FAST_SAVE_MS, FAST_SAVE_SECONDS removed", "Client builds and PumpkinCarvingModelTests pass"], "modelTier": "mechanical"}
```

---

### Task 6: Commit the full implementation

**Goal:** One commit on `feat/pumpkin-live` in the server worktree and one in the client worktree, with the client's `Chaos-Server` pointer on the new server commit.

**Files:**
- Commit: every file changed by Tasks 1-5 in both worktrees, plus the plan and its `.tasks.json` (client, force-added).

**Acceptance Criteria:**
- [ ] Full server suite: only the known `GiveAbility`/`OnItemDroppedOn` failures, if any.
- [ ] Full client suite passes.
- [ ] Server worktree has one new commit; client worktree has one new commit whose `Chaos-Server` gitlink equals that server commit.
- [ ] `git status --short` in both worktrees shows nothing of ours left (untracked `__pycache__`/`obj` noise is fine).

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-client ls-tree HEAD Chaos-Server` → prints the server worktree's `git rev-parse HEAD`.

**Steps:**

- [ ] **Step 1: Run the full server suite** (`cd .../pumpkin-carving-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj`). Expected: only the known failures.
- [ ] **Step 2: Then run the full client suite** (build with `-p:UnoraServerPath=...`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`). Expected: 0 failed.
- [ ] **Step 3: Commit the server**, staging by explicit path:

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-server
git add Chaos.DarkAges/Definitions/Enums.cs Chaos.Networking/Converters/Server/PumpkinLookConverter.cs Chaos.Networking/Entities/Server/PumpkinLookArgs.cs Chaos.Networking.Abstractions/Definitions/Enums.cs Chaos/Scripting/MapScripts/Temuair/Suomi/PumpkinCarvingMapScript.cs Tests/Chaos.Tests/Networking/PumpkinCarvingPacketConverterTests.cs Tests/Chaos.Tests/Theatre/PumpkinCarvingMapTests.cs
git commit -F <message file>
```

Message: `Pumpkin Carving: live carving looks (PumpkinLookState.Carving), sent on each changing save` + blank line + one-line body + the attribution lines.

- [ ] **Step 4: Point the client at it and commit the client:**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-client
git update-index --cacheinfo 160000,$(git -C ../pumpkin-carving-server rev-parse HEAD),Chaos-Server
git add Chaos.Client.Rendering/PumpkinLook.cs Chaos.Client.Rendering/PumpkinPainter.cs Chaos.Client.Rendering/PumpkinCarvingCache.cs Chaos.Client.Rendering/CreatureRenderer.cs Chaos.Client/Collections/PumpkinLookStore.cs Chaos.Client/Collections/WorldState.cs Chaos.Client/Screens/WorldScreen.PumpkinCarving.cs Chaos.Client/ViewModel/PumpkinCarvingModel.cs Chaos.Client/Controls/World/Popups/PumpkinCarving/PumpkinCarvingControl.cs CLAUDE.md Tests/Chaos.Client.Tests/PumpkinLookStoreTests.cs Tests/Chaos.Client.Tests/PumpkinPainterTests.cs Tests/Chaos.Client.Tests/PumpkinCarvingCacheTests.cs Tests/Chaos.Client.Tests/PumpkinCarvingModelTests.cs
git add -f docs/superpowers/plans/2026-10-03-pumpkin-live-carving.md docs/superpowers/plans/2026-10-03-pumpkin-live-carving.md.tasks.json
git commit -F <message file>
```

Message: `Pumpkin Carving: watch carvings live as dark holes; save every 600 ms; Chaos-Server at the live-look commit` + attribution lines.

- [ ] **Step 5: Run the Verify command** and `git status --short` in both worktrees.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/pumpkin-carving-client ls-tree HEAD Chaos-Server", "acceptanceCriteria": ["Full server suite: only known GiveAbility/OnItemDroppedOn failures", "Full client suite passes", "One new commit per worktree; client gitlink equals server commit", "No uncommitted files of ours remain"], "modelTier": "mechanical"}
```
