# Pumpkin Carving Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A director-run Halloween carving game in the haunted Suomi theatre: eight blank pumpkins on stage, a carving window, a big reveal, click-to-vote, candy, marks and a display pumpkin.

**Architecture:** The server owns the round. A pure state machine (`PumpkinCarvingRound`) holds the rules. A second map script on the haunted theatre (`PumpkinCarvingMapScript`) spawns the pumpkins as merchants, opens windows, sends looks, sets the lights and pays out. The client draws each lit pumpkin by recolouring the carved pixels of a new creature sprite, using a face map made by the same tool that draws the sprite.

**Tech Stack:** C# / .NET 10 (Chaos-Server, Chaos.Client with MonoGame), TUnit + FluentAssertions + Moq, Python 3 + Pillow (art tool, pytest), a DALib file-based C# script (sprite packing).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md` (read section 10, "Planning amendments", first: it records where this plan refines the spec).

## Global Constraints

- **Worktrees** (Task 0 creates them; all paths below are relative to one of these):
  - server `S` = `C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server` (branch `feat/pumpkin-carving` from Chaos-Server `master`)
  - client `C` = `C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-client` (branch `feat/pumpkin-carving` from Chaos.Client `main`)
  - content `U` = `C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-unora` (branch `feat/pumpkin-carving` from Unora `main`)
- **Never touch the shared checkouts** (`GitHub\Chaos.Client`, `GitHub\Chaos.Client\Chaos-Server`, `GitHub\Unora`). Other sessions have uncommitted work there.
- **Do not commit** in any task except the last. Leave all changes in the worktrees' working trees. The final task makes one commit per repo.
- **The client builds against the server worktree.** Every client `dotnet build` / `dotnet run` passes `-p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server`.
- **Test commands.** Server: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/<Class>/*"` (run from `S`). Client: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server -- --no-ansi --treenode-filter "/*/*/<Class>/*"` (from `C`). Python: `python -m pytest Tools/PumpkinCarving -q` (from `U`). `dotnet test` does not work.
- **Known failures:** two server tests already fail on master (`GiveAbility`, `OnItemDroppedOn` stackable). Leave them alone.
- **Fixed numbers:** grid 22 × 14 cells, 1 bit each, row-major, low bit first, 39 bytes. Client opcode `PumpkinCarvingSave = 142`. Server opcodes `PumpkinCarvingDisplay = 149`, `PumpkinLook = 150`. `CLIENT_VERSION` 766 → 767. Creature sprite 1455, palette 355. 8 pumpkins, 180 s carving, 60 s voting, 6 cut cells minimum, 2 pumpkins minimum for a vote, winner 20 candy, finishers 5, candy once per 24 h (`pumpkincarvingCandy`).
- **Stage tiles** (haunted theatre map `lod10269`, 20 × 31): pumpkins at `(6, 13)` … `(6, 20)`, claim tiles at `(5, 13)` … `(5, 20)` (slot `i` is row `13 + i`), display tile `(11, 15)`. Pumpkins face `Direction.Right` (toward the house).
- **Text limits:** orange bar ≤ 45 characters, dialog option ≤ 35, no em dashes in JSON.
- **Comments:** only where the why is not obvious. No explanatory comments in test code.
- **Code tools:** use Serena's symbolic tools for C# reads and edits (see the user's CLAUDE.md). Built-in Read/Edit are fine for JSON, Markdown, Python and `.csproj`.

**User decisions (already made):**
- The theatre director starts each round from Thulin's menu.
- Every carving shows at the reveal; the director can remove any pumpkin.
- Carving is cut-or-not on a flat 22 × 14 grid; cut cells glow with the candle.
- The pumpkin is an isometric sprite with its face turned partway (22°) toward the viewer (mockup option B); the grid is mapped onto it like a guild cloak design.
- Up to 8 carvers; a player claims a pumpkin by stepping on the tile in front of it.
- Pumpkins stay blank until a big reveal: the house goes dark and every face lights at once.
- The audience votes by clicking a pumpkin; counts stay hidden.
- Winner 20 candy, other finishers 5, at most once per 24 hours per player.
- The winner's pumpkin stays on display until the next winner replaces it.
- Pumpkins are stage NPCs (merchants) with a per-entity look message, not client-drawn props.
- Numbers chosen in the design and approved: 3 minutes to carve, 60 seconds to vote, 6-cut minimum, legend marks "Carved a pumpkin at the Garamonde Theatre" and "Carved the best pumpkin".
- Art is drawn by code, not AI.

---

## File structure

**Chaos-Server (`S`)**

| File | Responsibility |
|---|---|
| `Chaos.DarkAges/Definitions/PumpkinGrid.cs` (new) | The grid size and bit helpers, shared with the client |
| `Chaos.DarkAges/Definitions/Enums.cs` | `PumpkinCarvingDisplayType`, `PumpkinLookState` |
| `Chaos.DarkAges/Definitions/CONSTANTS.cs` | `CLIENT_VERSION` 767 |
| `Chaos.Networking.Abstractions/Definitions/Enums.cs` | The three opcodes |
| `Chaos.Networking/Entities/{Client,Server}/Pumpkin*Args.cs`, `Converters/{Client,Server}/Pumpkin*Converter.cs` (new) | The three messages |
| `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, `Chaos/Networking/ChaosWorldClient.cs` | Send methods; the look hook in `SendVisibleEntities` |
| `Chaos/Services/Theatre/PumpkinCarving.cs` (new) | Constants, texts, tiles, enums, small records |
| `Chaos/Services/Theatre/PumpkinCarvingRound.cs` (new) | The round's rules, no game objects |
| `Chaos/Services/Theatre/PumpkinDisplay.cs` (new) | The stored display winner |
| `Chaos/Services/Theatre/PumpkinLooks.cs` (new) | "Is this merchant a pumpkin, and how does it look?" for the network hook |
| `Chaos/Scripting/MapScripts/Temuair/Suomi/PumpkinCarvingMapScript.cs` (new) | Runs the round on the map |
| `Chaos/Scripting/MerchantScripts/Suomi/PumpkinCarvingScript.cs` (new) | Pumpkin clicks |
| `Chaos/Scripting/DialogScripts/Temuair/Suomi/PumpkinCarvingDirectorScript.cs` (new) | The director's Vote/Remove menu on a pumpkin |
| `Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs` | `ApplySavedScene`, stage-game exclusivity |
| `Chaos/Services/Theatre/SpotlightChairs.cs` | `StageBusy` refusal |
| `Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs` | Thulin's carving options |
| `Chaos/Services/Servers/WorldServer.cs` | The save handler |
| Tests under `Tests/Chaos.Tests/Theatre/` and `Tests/Chaos.Tests/Networking/` | |

**Unora (`U`)**

| File | Responsibility |
|---|---|
| `Tools/PumpkinCarving/make_pumpkin.py` (new) | Draws the sprite frames, the face map and a preview |
| `Tools/PumpkinCarving/pack_pumpkin.cs` (new) | Packs the sprite and palette into a copy of `hades.dat` |
| `Tools/PumpkinCarving/art/` (generated, committed) | `pumpkin_frames.json`, `pumpkinfacemap.json`, `preview.png` |
| `Tools/PumpkinCarving/test_*.py` (new) | Art and content checks |
| `Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/carving_pumpkin.json` (new) | The pumpkin merchant |
| `Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/instance.json` | Adds `pumpkincarvingmap` |
| `Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/*.json` (4 new) | Start / end now / call off / director menu |

**Chaos.Client (`C`)**

| File | Responsibility |
|---|---|
| `Chaos.Client.Rendering/PumpkinFaceMap.cs` (new) | Loads the embedded face map |
| `Chaos.Client.Rendering/PumpkinPainter.cs` (new) | Recolours carved pixels; pure |
| `Chaos.Client.Rendering/PumpkinLook.cs` (new) | One pumpkin's look |
| `Chaos.Client.Rendering/Assets/Pumpkin/pumpkinfacemap.json` (copied from `U`) | The embedded face map |
| `Chaos.Client.Rendering/CreatureRenderer.cs` | Painted-frame cache and the `Draw` hook |
| `Chaos.Client/Collections/PumpkinLookStore.cs` (new), `Collections/WorldState.cs` | Looks by entity id; the pumpkin's light |
| `Chaos.Client/ViewModel/PumpkinCarvingModel.cs` (new) | The window's grid, tools and history |
| `Chaos.Client/Controls/World/Popups/PumpkinCarving/PumpkinCarvingCanvas.cs`, `PumpkinCarvingControl.cs` (new) | The window |
| `Chaos.Client/Screens/WorldScreen.PumpkinCarving.cs` (new) and `WorldScreen*.cs` | Wiring |
| `Chaos.Client.Networking/ConnectionManager.cs`, `Definitions/Delegates.cs` | Handlers, events, send |

---

### Task 0: Create the worktrees and record the baseline

**Goal:** Three clean worktrees on `feat/pumpkin-carving`, with the client building against the server worktree.

**Files:** none (git only).

**Acceptance Criteria:**
- [ ] `S`, `C` and `U` exist on branch `feat/pumpkin-carving`.
- [ ] The server solution builds in `S`; the server test run shows only the 2 known failures.
- [ ] The client builds in `C` with the `UnoraServerPath` property and its tests pass.

**Verify:** `git -C <each worktree> branch --show-current` → `feat/pumpkin-carving`

**Steps:**

- [ ] **Step 1: Create the worktrees** (PowerShell):

```powershell
git -C C:\Users\Michael\Documents\GitHub\Chaos.Client\Chaos-Server worktree add C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server -b feat/pumpkin-carving master
git -C C:\Users\Michael\Documents\GitHub\Chaos.Client worktree add C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-client -b feat/pumpkin-carving main
git -C C:\Users\Michael\Documents\GitHub\Unora worktree add C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-unora -b feat/pumpkin-carving main
```

- [ ] **Step 2: Baseline server build and tests** (from `S`):

```powershell
dotnet build Chaos.slnx
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi
```

Expected: build succeeds; only `GiveAbility` and `OnItemDroppedOn` (stackable) fail. If anything else fails, stop and report it.

- [ ] **Step 3: Baseline client build and tests** (from `C`):

```powershell
dotnet build Chaos.Client.slnx -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server
dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server -- --no-ansi
```

Expected: build succeeds and all tests pass. If the build complains the `Chaos-Server` submodule is missing despite the property, run `git submodule update --init` in `C` and report that you did.

- [ ] **Step 4: Check Python**: `python -c "import PIL, pytest"` from `U` succeeds.

```json:metadata
{"files": [], "verifyCommand": "git -C C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\pumpkin-carving-server branch --show-current", "acceptanceCriteria": ["three worktrees on feat/pumpkin-carving", "server baseline only the 2 known failures", "client builds against the server worktree and its tests pass"], "modelTier": "mechanical"}
```

---

### Task 1: Shared grid, messages and send methods (server)

**Goal:** The grid helpers, the two enums, the three opcodes and messages, `CLIENT_VERSION` 767, and the world client's send methods, with round-trip tests.

**Files:**
- Create: `S/Chaos.DarkAges/Definitions/PumpkinGrid.cs`
- Modify: `S/Chaos.DarkAges/Definitions/Enums.cs` (append), `S/Chaos.DarkAges/Definitions/CONSTANTS.cs`
- Modify: `S/Chaos.Networking.Abstractions/Definitions/Enums.cs`
- Create: `S/Chaos.Networking/Entities/Client/PumpkinCarvingSaveArgs.cs`, `S/Chaos.Networking/Converters/Client/PumpkinCarvingSaveConverter.cs`
- Create: `S/Chaos.Networking/Entities/Server/PumpkinCarvingDisplayArgs.cs`, `S/Chaos.Networking/Converters/Server/PumpkinCarvingDisplayConverter.cs`
- Create: `S/Chaos.Networking/Entities/Server/PumpkinLookArgs.cs`, `S/Chaos.Networking/Converters/Server/PumpkinLookConverter.cs`
- Modify: `S/Chaos/Networking/Abstractions/IChaosWorldClient.cs`, `S/Chaos/Networking/ChaosWorldClient.cs`
- Test: `S/Tests/Chaos.Tests/Theatre/PumpkinGridTests.cs`, `S/Tests/Chaos.Tests/Networking/PumpkinCarvingPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `PumpkinGrid` sets, reads and counts cut cells; `IsValid` rejects a wrong length and any of the 4 unused high bits of the last byte.
- [ ] All three messages round-trip; a blank `PumpkinLook` serializes to exactly 5 bytes (no grid).
- [ ] `CLIENT_VERSION` is 767.
- [ ] `IChaosWorldClient` has `SendPumpkinCarvingOpen`, `SendPumpkinCarvingClose`, `SendPumpkinLook`.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/Pumpkin*Tests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests.**

`S/Tests/Chaos.Tests/Theatre/PumpkinGridTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Tests.Theatre;

public sealed class PumpkinGridTests
{
    [Test]
    public void A_new_grid_is_39_empty_bytes()
    {
        var grid = PumpkinGrid.Empty();

        grid.Should().HaveCount(39);
        PumpkinGrid.CountCut(grid).Should().Be(0);
        PumpkinGrid.IsValid(grid).Should().BeTrue();
    }

    [Test]
    public void Cells_are_set_read_and_cleared_one_bit_each()
    {
        var grid = PumpkinGrid.Empty();

        PumpkinGrid.SetCut(grid, 0, 0, true);
        PumpkinGrid.SetCut(grid, 21, 13, true);
        PumpkinGrid.SetCut(grid, 5, 7, true);

        PumpkinGrid.IsCut(grid, 0, 0).Should().BeTrue();
        PumpkinGrid.IsCut(grid, 21, 13).Should().BeTrue();
        PumpkinGrid.IsCut(grid, 5, 7).Should().BeTrue();
        PumpkinGrid.IsCut(grid, 1, 0).Should().BeFalse();
        PumpkinGrid.CountCut(grid).Should().Be(3);
        grid[0].Should().Be(1);
        grid[38].Should().Be(0b1000);

        PumpkinGrid.SetCut(grid, 5, 7, false);

        PumpkinGrid.IsCut(grid, 5, 7).Should().BeFalse();
        PumpkinGrid.CountCut(grid).Should().Be(2);
    }

    [Test]
    public void A_wrong_length_or_a_stray_high_bit_is_invalid()
    {
        PumpkinGrid.IsValid(new byte[38]).Should().BeFalse();
        PumpkinGrid.IsValid(new byte[40]).Should().BeFalse();

        var grid = PumpkinGrid.Empty();
        grid[38] = 0b0001_0000;

        PumpkinGrid.IsValid(grid).Should().BeFalse();
    }
}
```

`S/Tests/Chaos.Tests/Networking/PumpkinCarvingPacketConverterTests.cs`:

```csharp
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Converters.Client;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;
using FluentAssertions;

namespace Chaos.Tests.Networking;

public sealed class PumpkinCarvingPacketConverterTests
{
    private static readonly Encoding Enc = Encoding.GetEncoding(949);

    private static byte[] Bytes<T>(PacketConverterBase<T> converter, T args) where T: class, IPacketSerializable
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        converter.Serialize(ref writer, args);

        return writer.ToSpan()
                     .ToArray();
    }

    private static T RoundTrip<T>(PacketConverterBase<T> converter, T original) where T: class, IPacketSerializable
    {
        var reader = new SpanReader(Enc, Bytes(converter, original));

        return converter.Deserialize(ref reader);
    }

    private static byte[] SampleGrid()
    {
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 3, 4, true);
        PumpkinGrid.SetCut(grid, 21, 13, true);

        return grid;
    }

    [Test]
    public void Save_round_trips()
    {
        var original = new PumpkinCarvingSaveArgs
        {
            Done = true,
            Grid = SampleGrid()
        };

        RoundTrip(new PumpkinCarvingSaveConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Display_round_trips_for_Open_and_Close()
    {
        var open = new PumpkinCarvingDisplayArgs
        {
            Type = PumpkinCarvingDisplayType.Open,
            SecondsLeft = 171,
            Grid = SampleGrid()
        };

        var close = new PumpkinCarvingDisplayArgs { Type = PumpkinCarvingDisplayType.Close };

        RoundTrip(new PumpkinCarvingDisplayConverter(), open).Should().BeEquivalentTo(open);
        RoundTrip(new PumpkinCarvingDisplayConverter(), close).Should().BeEquivalentTo(close);
    }

    [Test]
    public void A_lit_look_carries_the_grid_and_a_blank_one_does_not()
    {
        var lit = new PumpkinLookArgs
        {
            EntityId = 4021,
            State = PumpkinLookState.Lit,
            Grid = SampleGrid()
        };

        var blank = new PumpkinLookArgs
        {
            EntityId = 4022,
            State = PumpkinLookState.Blank
        };

        RoundTrip(new PumpkinLookConverter(), lit).Should().BeEquivalentTo(lit);
        RoundTrip(new PumpkinLookConverter(), blank).Should().BeEquivalentTo(blank);
        Bytes(new PumpkinLookConverter(), blank).Should().HaveCount(5);
    }
}
```

- [ ] **Step 2: Run them and see them fail** (compile errors: the types don't exist).

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/Pumpkin*Tests/*"`
Expected: build fails naming `PumpkinGrid`, `PumpkinCarvingSaveArgs`, etc.

- [ ] **Step 3: Add `PumpkinGrid`** — `S/Chaos.DarkAges/Definitions/PumpkinGrid.cs`:

```csharp
using System.Numerics;

namespace Chaos.DarkAges.Definitions;

/// <summary>
///     The Pumpkin Carving grid: 22 × 14 cells, one bit each (1 = cut through), row-major, packed low bit first into 39
///     bytes. The last byte holds 4 cells; its 4 high bits are always zero.
/// </summary>
public static class PumpkinGrid
{
    public const int WIDTH = 22;
    public const int HEIGHT = 14;
    public const int CELLS = WIDTH * HEIGHT;
    public const int BYTES = (CELLS + 7) / 8;

    private const int UNUSED_MASK = 0xFF << (CELLS - ((BYTES - 1) * 8));

    public static byte[] Empty() => new byte[BYTES];

    public static bool IsCut(ReadOnlySpan<byte> grid, int x, int y)
    {
        var index = (y * WIDTH) + x;

        return (grid[index >> 3] & (1 << (index & 7))) != 0;
    }

    public static void SetCut(Span<byte> grid, int x, int y, bool cut)
    {
        var index = (y * WIDTH) + x;
        var bit = (byte)(1 << (index & 7));

        if (cut)
            grid[index >> 3] |= bit;
        else
            grid[index >> 3] &= (byte)~bit;
    }

    public static int CountCut(ReadOnlySpan<byte> grid)
    {
        var count = 0;

        foreach (var value in grid)
            count += BitOperations.PopCount(value);

        return count;
    }

    public static bool IsValid(ReadOnlySpan<byte> grid) => (grid.Length == BYTES) && ((grid[BYTES - 1] & UNUSED_MASK) == 0);
}
```

- [ ] **Step 4: Add the enums.** Append to `S/Chaos.DarkAges/Definitions/Enums.cs` (after `FishingDisplayType` and its neighbours, at the end of the file):

```csharp
/// <summary>What a PumpkinCarvingDisplay packet does to the carving window.</summary>
public enum PumpkinCarvingDisplayType : byte
{
    Open = 0,
    Close = 1
}

/// <summary>How one Pumpkin Carving pumpkin looks: blank skin, or lit with its carving.</summary>
public enum PumpkinLookState : byte
{
    Blank = 0,
    Lit = 1
}
```

- [ ] **Step 5: Bump the client version.** In `S/Chaos.DarkAges/Definitions/CONSTANTS.cs` change `public const ushort CLIENT_VERSION = 766;` to `767`. If it is no longer 766, use the current value + 1 and note it in your report.

- [ ] **Step 6: Add the opcodes** in `S/Chaos.Networking.Abstractions/Definitions/Enums.cs`. In `ClientOpCode`, after `FishingInteraction = 141,`:

```csharp
    /// <summary>
    ///     A save from the Pumpkin Carving window: a "done" flag and the 39-byte grid. The server uses the pumpkin this
    ///     player owns in the round on their map.
    ///     <br />
    ///     Hex value: 0x8E
    /// </summary>
    PumpkinCarvingSave = 142,
```

In `ServerOpCode`, after `FishingDisplay = 148,`:

```csharp
    /// <summary>
    ///     Opens the Pumpkin Carving window (seconds left and the carver's saved grid) or closes it.
    ///     <br />
    ///     Hex value: 0x95
    /// </summary>
    PumpkinCarvingDisplay = 149,

    /// <summary>
    ///     How one Pumpkin Carving pumpkin looks. A blank look carries no grid, so no client sees a carving before the reveal.
    ///     <br />
    ///     Hex value: 0x96
    /// </summary>
    PumpkinLook = 150,
```

If 142, 149 or 150 is already used, take the next free values and note them in your report.

- [ ] **Step 7: Add the args.**

`S/Chaos.Networking/Entities/Client/PumpkinCarvingSaveArgs.cs`:

```csharp
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>Represents the serialization of the <see cref="ClientOpCode.PumpkinCarvingSave" /> packet.</summary>
/// <remarks>Carries no pumpkin id: the server uses the pumpkin this player owns, so a client cannot carve someone else's.</remarks>
public sealed record PumpkinCarvingSaveArgs : IPacketSerializable
{
    /// <summary>True when the window is closing (Done or Close); false for the periodic save.</summary>
    public required bool Done { get; set; }

    /// <summary>The 39-byte grid (see PumpkinGrid).</summary>
    public required byte[] Grid { get; set; }
}
```

`S/Chaos.Networking/Entities/Server/PumpkinCarvingDisplayArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>Represents the serialization of the <see cref="ServerOpCode.PumpkinCarvingDisplay" /> packet.</summary>
public sealed record PumpkinCarvingDisplayArgs : IPacketSerializable
{
    public required PumpkinCarvingDisplayType Type { get; set; }

    // --- Open ---

    /// <summary>Seconds of carving left in the round.</summary>
    public ushort SecondsLeft { get; set; }

    /// <summary>The carver's grid as the server last saved it.</summary>
    public byte[]? Grid { get; set; }
}
```

`S/Chaos.Networking/Entities/Server/PumpkinLookArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>Represents the serialization of the <see cref="ServerOpCode.PumpkinLook" /> packet.</summary>
public sealed record PumpkinLookArgs : IPacketSerializable
{
    /// <summary>The pumpkin merchant's entity id.</summary>
    public required uint EntityId { get; set; }

    public required PumpkinLookState State { get; set; }

    /// <summary>The carving; only sent when <see cref="State" /> is Lit.</summary>
    public byte[]? Grid { get; set; }
}
```

- [ ] **Step 8: Add the converters** (converters are found by reflection; no registration).

`S/Chaos.Networking/Converters/Client/PumpkinCarvingSaveConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="PumpkinCarvingSaveArgs" /></summary>
public sealed class PumpkinCarvingSaveConverter : PacketConverterBase<PumpkinCarvingSaveArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.PumpkinCarvingSave;

    /// <inheritdoc />
    public override PumpkinCarvingSaveArgs Deserialize(ref SpanReader reader)
    {
        var done = reader.ReadByte() != 0;
        var grid = reader.ReadBytes(PumpkinGrid.BYTES);

        return new PumpkinCarvingSaveArgs
        {
            Done = done,
            Grid = grid
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, PumpkinCarvingSaveArgs args)
    {
        writer.WriteByte(args.Done ? (byte)1 : (byte)0);
        writer.WriteBytes(args.Grid);
    }
}
```

`S/Chaos.Networking/Converters/Server/PumpkinCarvingDisplayConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="PumpkinCarvingDisplayArgs" /></summary>
public sealed class PumpkinCarvingDisplayConverter : PacketConverterBase<PumpkinCarvingDisplayArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.PumpkinCarvingDisplay;

    /// <inheritdoc />
    public override PumpkinCarvingDisplayArgs Deserialize(ref SpanReader reader)
    {
        var type = (PumpkinCarvingDisplayType)reader.ReadByte();

        return type switch
        {
            PumpkinCarvingDisplayType.Open => new PumpkinCarvingDisplayArgs
            {
                Type = type,
                SecondsLeft = reader.ReadUInt16(),
                Grid = reader.ReadBytes(PumpkinGrid.BYTES)
            },
            PumpkinCarvingDisplayType.Close => new PumpkinCarvingDisplayArgs { Type = type },
            _ => throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown pumpkin carving display type")
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, PumpkinCarvingDisplayArgs args)
    {
        writer.WriteByte((byte)args.Type);

        if (args.Type != PumpkinCarvingDisplayType.Open)
            return;

        writer.WriteUInt16(args.SecondsLeft);
        writer.WriteBytes(args.Grid ?? PumpkinGrid.Empty());
    }
}
```

`S/Chaos.Networking/Converters/Server/PumpkinLookConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="PumpkinLookArgs" /></summary>
public sealed class PumpkinLookConverter : PacketConverterBase<PumpkinLookArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.PumpkinLook;

    /// <inheritdoc />
    public override PumpkinLookArgs Deserialize(ref SpanReader reader)
    {
        var id = reader.ReadUInt32();
        var state = (PumpkinLookState)reader.ReadByte();

        if (!Enum.IsDefined(state))
            throw new ArgumentOutOfRangeException(nameof(reader), state, "Unknown pumpkin look state");

        return new PumpkinLookArgs
        {
            EntityId = id,
            State = state,
            Grid = state == PumpkinLookState.Lit ? reader.ReadBytes(PumpkinGrid.BYTES) : null
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, PumpkinLookArgs args)
    {
        writer.WriteUInt32(args.EntityId);
        writer.WriteByte((byte)args.State);

        if (args.State == PumpkinLookState.Lit)
            writer.WriteBytes(args.Grid ?? PumpkinGrid.Empty());
    }
}
```

- [ ] **Step 9: Add the send methods.** In `S/Chaos/Networking/Abstractions/IChaosWorldClient.cs`, after `SendFishingClose`:

```csharp
    /// <summary>Opens the Pumpkin Carving window on this player's pumpkin.</summary>
    void SendPumpkinCarvingOpen(ushort secondsLeft, byte[] grid);

    /// <summary>Closes the Pumpkin Carving window.</summary>
    void SendPumpkinCarvingClose();

    /// <summary>Tells this player how one Pumpkin Carving pumpkin looks.</summary>
    void SendPumpkinLook(PumpkinLookArgs args);
```

In `S/Chaos/Networking/ChaosWorldClient.cs`, after `SendFishingClose`:

```csharp
    /// <inheritdoc />
    public void SendPumpkinCarvingOpen(ushort secondsLeft, byte[] grid)
        => Send(
            new PumpkinCarvingDisplayArgs
            {
                Type = PumpkinCarvingDisplayType.Open,
                SecondsLeft = secondsLeft,
                Grid = grid
            });

    /// <inheritdoc />
    public void SendPumpkinCarvingClose() => Send(new PumpkinCarvingDisplayArgs { Type = PumpkinCarvingDisplayType.Close });

    /// <inheritdoc />
    public void SendPumpkinLook(PumpkinLookArgs args) => Send(args);
```

- [ ] **Step 10: Run the tests and see them pass.**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/Pumpkin*Tests/*"`
Expected: 6 tests pass. If `SpanReader.ReadUInt32` / `SpanWriter.WriteUInt32` are named differently, use the names `GuildCloakLookConverter` uses for its `u32`.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/PumpkinGrid.cs", "Chaos.DarkAges/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos.Networking/Entities/Client/PumpkinCarvingSaveArgs.cs", "Chaos.Networking/Converters/Client/PumpkinCarvingSaveConverter.cs", "Chaos.Networking/Entities/Server/PumpkinCarvingDisplayArgs.cs", "Chaos.Networking/Converters/Server/PumpkinCarvingDisplayConverter.cs", "Chaos.Networking/Entities/Server/PumpkinLookArgs.cs", "Chaos.Networking/Converters/Server/PumpkinLookConverter.cs", "Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos/Networking/ChaosWorldClient.cs", "Tests/Chaos.Tests/Theatre/PumpkinGridTests.cs", "Tests/Chaos.Tests/Networking/PumpkinCarvingPacketConverterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/Pumpkin*Tests/*\"", "acceptanceCriteria": ["PumpkinGrid set/read/count; IsValid rejects wrong length and high bits", "three messages round-trip; blank look is 5 bytes", "CLIENT_VERSION 767", "IChaosWorldClient has the three send methods"], "modelTier": "mechanical"}
```

---

### Task 2: Round rules (server)

**Goal:** `PumpkinCarving` (constants, texts, tiles, enums) and `PumpkinCarvingRound` (the pure state machine), fully tested.

**Files:**
- Create: `S/Chaos/Services/Theatre/PumpkinCarving.cs`, `S/Chaos/Services/Theatre/PumpkinCarvingRound.cs`
- Test: `S/Tests/Chaos.Tests/Theatre/PumpkinCarvingRoundTests.cs`

**Acceptance Criteria:**
- [ ] One pumpkin per player; stepping up again finds your own; a taken pumpkin is refused; a removed carver is barred.
- [ ] Saves need the owner, the Carving state, a valid grid, and at most one per second (periodic and closing saves limited separately).
- [ ] Time up drops unclaimed pumpkins and those under 6 cuts; under 2 left ends with no vote, otherwise voting starts for 60 s.
- [ ] Carvers can't vote; voters can change their vote; removed pumpkins lose their votes; ties name every top pumpkin; no votes means no winner.
- [ ] Leaving while carving frees the pumpkin; leaving later removes it; a leaving voter's vote is dropped.
- [ ] Every orange-bar text in `PumpkinCarving` is ≤ 45 characters (checked by a test).

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/PumpkinCarvingRoundTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** — `S/Tests/Chaos.Tests/Theatre/PumpkinCarvingRoundTests.cs`:

```csharp
using System.Reflection;
using Chaos.DarkAges.Definitions;
using Chaos.Services.Theatre;
using FluentAssertions;

namespace Chaos.Tests.Theatre;

public sealed class PumpkinCarvingRoundTests
{
    private const long START = 1_000_000;
    private const long CARVING_OVER = START + (PumpkinCarving.CARVING_SECONDS * 1000L);
    private const long VOTING_OVER = CARVING_OVER + (PumpkinCarving.VOTING_SECONDS * 1000L);

    private static byte[] Carved(int cuts)
    {
        var grid = PumpkinGrid.Empty();

        for (var i = 0; i < cuts; i++)
            PumpkinGrid.SetCut(grid, i % PumpkinGrid.WIDTH, i / PumpkinGrid.WIDTH, true);

        return grid;
    }

    private static PumpkinCarvingRound TwoCarved()
    {
        var round = new PumpkinCarvingRound(START);
        round.Claim(0, 1, "Mira");
        round.Claim(1, 2, "Otto");
        round.Save(1, Carved(10), false, START + 1000).Should().BeTrue();
        round.Save(2, Carved(6), false, START + 1000).Should().BeTrue();

        return round;
    }

    private static PumpkinCarvingRound Voting()
    {
        var round = TwoCarved();
        round.Tick(CARVING_OVER).Kind.Should().Be(PumpkinTick.Reveal);

        return round;
    }

    [Test]
    public void A_new_round_has_eight_blank_pumpkins_and_three_minutes()
    {
        var round = new PumpkinCarvingRound(START);

        round.State.Should().Be(PumpkinRoundState.Carving);
        round.Pumpkins.Should().HaveCount(8).And.OnlyContain(p => (p.OwnerId == null) && !p.Removed);
        round.SecondsLeft(START).Should().Be(180);
        round.SecondsLeft(START + 500).Should().Be(180);
        round.SecondsLeft(CARVING_OVER).Should().Be(0);
    }

    [Test]
    public void A_free_pumpkin_is_claimed_and_stepping_up_again_finds_your_own()
    {
        var round = new PumpkinCarvingRound(START);

        round.Claim(3, 1, "Mira").Should().Be((PumpkinClaim.Claimed, 3));
        round.Claim(5, 1, "Mira").Should().Be((PumpkinClaim.OwnPumpkin, 3));
        round.Pumpkins[3].OwnerName.Should().Be("Mira");
        round.Pumpkins[5].OwnerId.Should().BeNull();
        round.SlotOf(1).Should().Be(3);
        round.IsCarver(1).Should().BeTrue();
    }

    [Test]
    public void A_taken_pumpkin_is_refused()
    {
        var round = new PumpkinCarvingRound(START);
        round.Claim(3, 1, "Mira");

        round.Claim(3, 2, "Otto").Should().Be((PumpkinClaim.Taken, 3));
        round.SlotOf(2).Should().BeNull();
    }

    [Test]
    public void Saves_need_the_owner_a_valid_grid_and_a_second_between_them()
    {
        var round = new PumpkinCarvingRound(START);
        round.Claim(0, 1, "Mira");

        round.Save(2, Carved(8), false, START).Should().BeFalse();
        round.Save(1, new byte[10], false, START).Should().BeFalse();
        round.Save(1, Carved(8), false, START).Should().BeTrue();
        round.Save(1, Carved(9), false, START + 999).Should().BeFalse();
        round.Save(1, Carved(9), false, START + 1000).Should().BeTrue();

        PumpkinGrid.CountCut(round.Pumpkins[0].Grid).Should().Be(9);
    }

    [Test]
    public void A_closing_save_has_its_own_limit()
    {
        var round = new PumpkinCarvingRound(START);
        round.Claim(0, 1, "Mira");
        round.Save(1, Carved(8), false, START);

        round.Save(1, Carved(12), true, START + 10).Should().BeTrue();
        round.Save(1, Carved(13), true, START + 20).Should().BeFalse();

        PumpkinGrid.CountCut(round.Pumpkins[0].Grid).Should().Be(12);
    }

    [Test]
    public void The_saved_grid_is_a_copy()
    {
        var round = new PumpkinCarvingRound(START);
        round.Claim(0, 1, "Mira");
        var grid = Carved(8);
        round.Save(1, grid, false, START);

        grid[0] = 0;

        PumpkinGrid.CountCut(round.Pumpkins[0].Grid).Should().Be(8);
    }

    [Test]
    public void Time_up_drops_blank_and_thin_pumpkins_and_starts_the_vote()
    {
        var round = TwoCarved();
        round.Claim(2, 3, "Nia");
        round.Save(3, Carved(5), false, START + 1000);

        round.Tick(CARVING_OVER - 1).Kind.Should().Be(PumpkinTick.None);

        var result = round.Tick(CARVING_OVER);

        result.Kind.Should().Be(PumpkinTick.Reveal);
        result.Dropped.Should().BeEquivalentTo([2, 3, 4, 5, 6, 7]);
        round.State.Should().Be(PumpkinRoundState.Voting);
        round.SecondsLeft(CARVING_OVER).Should().Be(60);
        round.Pumpkins.Count(p => !p.Removed).Should().Be(2);
    }

    [Test]
    public void Fewer_than_two_carved_pumpkins_end_the_round_without_a_vote()
    {
        var round = new PumpkinCarvingRound(START);
        round.Claim(0, 1, "Mira");
        round.Save(1, Carved(6), false, START);

        var result = round.Tick(CARVING_OVER);

        result.Kind.Should().Be(PumpkinTick.TooFew);
        round.State.Should().Be(PumpkinRoundState.Done);
        round.Finishers().Should().BeEquivalentTo([0]);
    }

    [Test]
    public void Carvers_cannot_vote_and_others_can_change_their_vote()
    {
        var round = Voting();

        round.Vote(1, 1).Should().Be(PumpkinVote.CarverCantVote);
        round.Vote(9, 0).Should().Be(PumpkinVote.Voted);
        round.Vote(9, 0).Should().Be(PumpkinVote.Unchanged);
        round.Vote(9, 1).Should().Be(PumpkinVote.Voted);
        round.Vote(9, 5).Should().Be(PumpkinVote.NotOnStage);
        round.Winners().Should().BeEquivalentTo([1]);
    }

    [Test]
    public void Votes_are_refused_while_carving()
    {
        var round = TwoCarved();

        round.Vote(9, 0).Should().Be(PumpkinVote.NotVoting);
    }

    [Test]
    public void The_most_votes_win_and_a_tie_names_every_top_pumpkin()
    {
        var round = Voting();
        round.Vote(9, 0);
        round.Vote(10, 1);

        round.Winners().Should().Equal(0, 1);

        round.Vote(11, 1);

        round.Winners().Should().Equal(1);
        round.Tick(VOTING_OVER).Kind.Should().Be(PumpkinTick.Finished);
        round.State.Should().Be(PumpkinRoundState.Done);
        round.Finishers().Should().BeEquivalentTo([0, 1]);
    }

    [Test]
    public void No_votes_means_no_winner()
    {
        var round = Voting();

        round.Tick(VOTING_OVER);

        round.Winners().Should().BeEmpty();
    }

    [Test]
    public void Removing_a_pumpkin_drops_its_votes_and_bars_its_carver()
    {
        var round = new PumpkinCarvingRound(START);
        round.Claim(0, 1, "Mira");

        round.Remove(0).Should().BeTrue();
        round.Remove(0).Should().BeFalse();
        round.Claim(4, 1, "Mira").Should().Be((PumpkinClaim.Barred, 4));

        var voting = Voting();
        voting.Vote(9, 0);
        voting.Remove(0);

        voting.Winners().Should().BeEmpty();
        voting.Finishers().Should().BeEquivalentTo([1]);
    }

    [Test]
    public void Leaving_while_carving_frees_the_pumpkin_and_leaving_later_removes_it()
    {
        var round = TwoCarved();

        round.Leave(1).Should().Be(0);
        round.Pumpkins[0].OwnerId.Should().BeNull();
        round.Pumpkins[0].Removed.Should().BeFalse();
        PumpkinGrid.CountCut(round.Pumpkins[0].Grid).Should().Be(0);

        var voting = Voting();

        voting.Leave(2).Should().Be(1);
        voting.Pumpkins[1].Removed.Should().BeTrue();
    }

    [Test]
    public void A_leaving_voter_loses_their_vote()
    {
        var round = Voting();
        round.Vote(9, 0);

        round.Leave(9).Should().BeNull();

        round.Winners().Should().BeEmpty();
    }

    [Test]
    public void Ending_the_carving_early_reveals_at_once()
    {
        var round = TwoCarved();

        round.EndCarving(START + 5000).Kind.Should().Be(PumpkinTick.Reveal);
        round.SecondsLeft(START + 5000).Should().Be(60);
        round.EndCarving(START + 6000).Kind.Should().Be(PumpkinTick.None);
    }

    [Test]
    public void Calling_off_ends_the_round()
    {
        var round = TwoCarved();

        round.CallOff();

        round.State.Should().Be(PumpkinRoundState.Done);
        round.Tick(VOTING_OVER).Kind.Should().Be(PumpkinTick.None);
        round.Claim(4, 7, "Late").Should().Be((PumpkinClaim.NotCarving, 4));
    }

    [Test]
    public void Every_orange_bar_text_fits()
    {
        var texts = typeof(PumpkinCarving).GetFields(BindingFlags.Public | BindingFlags.Static)
                                          .Where(f => f.Name.EndsWith("Text", StringComparison.Ordinal) && (f.FieldType == typeof(string)))
                                          .Select(f => (string)f.GetValue(null)!)
                                          .Append(PumpkinCarving.VotedText("Abcdefghijkl"))
                                          .Append(PumpkinCarving.WinnerAnnouncement(["Abcdefghijkl", "Mnopqrstuvwx"]))
                                          .Concat(Enum.GetValues<PumpkinCarvingRefusal>().Where(r => r != PumpkinCarvingRefusal.None).Select(PumpkinCarving.RefusalText));

        texts.Should().OnlyContain(text => text.Length <= 45);
    }
}
```

- [ ] **Step 2: Run them and see them fail** (compile errors).

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/PumpkinCarvingRoundTests/*"`
Expected: build fails naming `PumpkinCarvingRound`.

- [ ] **Step 3: Write `PumpkinCarving`** — `S/Chaos/Services/Theatre/PumpkinCarving.cs`:

```csharp
#region
using Chaos.Geometry;
#endregion

namespace Chaos.Services.Theatre;

public enum PumpkinCarvingRefusal
{
    None,
    NotDirector,
    OutsideWindow,
    AlreadyRunning,
    StageBusy
}

public enum PumpkinRoundState
{
    Carving,
    Voting,
    Done
}

public enum PumpkinClaim
{
    Claimed,
    OwnPumpkin,
    Taken,
    Barred,
    NotCarving
}

public enum PumpkinVote
{
    Voted,
    Unchanged,
    CarverCantVote,
    NotVoting,
    NotOnStage
}

public enum PumpkinTick
{
    None,
    Reveal,
    TooFew,
    Finished
}

/// <summary>What a tick or an early end did: <see cref="Dropped" /> lists the pumpkins it took off the stage.</summary>
public readonly record struct PumpkinTickResult(PumpkinTick Kind, IReadOnlyList<int> Dropped);

/// <summary>One stage pumpkin in a round, by slot (0-7).</summary>
public sealed record PumpkinSlot(
    int Index,
    uint? OwnerId,
    string? OwnerName,
    byte[] Grid,
    bool Removed);

/// <summary>
///     Pumpkin Carving's numbers, texts and stage tiles (spec: Chaos.Client
///     docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md). The rules are in <see cref="PumpkinCarvingRound" />.
/// </summary>
public static class PumpkinCarving
{
    public const int PUMPKINS = 8;
    public const int CARVING_SECONDS = 180;
    public const int VOTING_SECONDS = 60;
    public const int MIN_CUT_CELLS = 6;
    public const int MIN_FOR_VOTE = 2;
    public const int WINNER_CANDY = 20;
    public const int FINISHER_CANDY = 5;
    public const long SAVE_INTERVAL_MS = 1000;

    public const string PumpkinTemplateKey = "carving_pumpkin";
    public const string DirectorDialogKey = "pumpkincarving_director";
    public const string CandyTemplateKey = SpotlightChairs.CandyTemplateKey;
    public const string CandyEvent = "pumpkincarvingCandy";
    public const string ParticipationKey = "pumpkincarving";
    public const string ParticipationText = "Carved a pumpkin at the Garamonde Theatre";
    public const string WinnerKey = "pumpkincarvingWin";
    public const string WinnerText = "Carved the best pumpkin";
    public const string BlankName = "Blank Pumpkin";

    public const string StartText = "Pumpkin Carving! Step up to a pumpkin.";
    public const string RevealText = "The pumpkins are lit! Click your favorite.";
    public const string TakenText = "That pumpkin is taken.";
    public const string CarverCantVoteText = "Carvers can't vote.";
    public const string RemovedText = "The director removed your pumpkin.";
    public const string NoVotesText = "No votes were cast.";
    public const string TooFewText = "Not enough pumpkins were carved.";
    public const string CalledOffText = "The director called off Pumpkin Carving.";
    public const string LightsBusyText = "Pumpkin Carving is using the lights.";
    public const string CandyWaitText = SpotlightChairs.CandyWaitText;

    public const string StartOption = "Start Pumpkin Carving";
    public const string CallOffOption = "Call Off Pumpkin Carving";
    public const string EndNowOption = "End the Carving Now";

    /// <summary>Slot i's pumpkin. The stage is x 0-8, y 12-20 of the haunted theatre; column 0 rows 12-19 are backstage warps.</summary>
    public static readonly Point[] PumpkinTiles =
    [
        new(6, 13), new(6, 14), new(6, 15), new(6, 16), new(6, 17), new(6, 18), new(6, 19), new(6, 20)
    ];

    /// <summary>Slot i's claim tile: behind its pumpkin, so the carver faces the house past it.</summary>
    public static readonly Point[] ClaimTiles =
    [
        new(5, 13), new(5, 14), new(5, 15), new(5, 16), new(5, 17), new(5, 18), new(5, 19), new(5, 20)
    ];

    /// <summary>The winner's display spot, in the house beside the stage wall.</summary>
    public static readonly Point DisplayTile = new(11, 15);

    public static string PumpkinName(string owner) => $"{owner}'s pumpkin";

    public static string VotedText(string owner) => $"You voted for {owner}'s pumpkin.";

    public static string WinnerAnnouncement(IReadOnlyList<string> names)
        => names.Count switch
        {
            1 => $"{names[0]}'s pumpkin wins!",
            2 => $"{names[0]} and {names[1]} tie!",
            _ => $"{names.Count} pumpkins tie!"
        };

    public static string RefusalText(PumpkinCarvingRefusal refusal)
        => refusal switch
        {
            PumpkinCarvingRefusal.NotDirector    => "Only the director can start Pumpkin Carving.",
            PumpkinCarvingRefusal.OutsideWindow  => "Pumpkin Carving is only played at Halloween.",
            PumpkinCarvingRefusal.AlreadyRunning => "Pumpkin Carving is already going.",
            PumpkinCarvingRefusal.StageBusy      => "Spotlight Chairs is using the stage.",
            _                                    => string.Empty
        };

    /// <summary>The director's start or call-off option, or null when this person can't run the game now.</summary>
    public static string? MenuOption(bool canDirect, bool halloween, bool running, bool admin = false)
    {
        if (!canDirect || (!halloween && !admin))
            return null;

        return running ? CallOffOption : StartOption;
    }

    /// <summary>"End the Carving Now", only for a director while pumpkins are being carved.</summary>
    public static string? EndNowMenuOption(bool canDirect, PumpkinRoundState? state)
        => canDirect && (state == PumpkinRoundState.Carving) ? EndNowOption : null;
}
```

If `Point` here does not accept `new(6, 13)` from `Chaos.Geometry`, use the `Point` type `SpotlightChairs.cs` uses for its tiles.

- [ ] **Step 4: Write `PumpkinCarvingRound`** — `S/Chaos/Services/Theatre/PumpkinCarvingRound.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.Theatre;

/// <summary>
///     One Pumpkin Carving round's rules, with no game objects: who owns which pumpkin, the saved carvings, the votes and
///     the clock. Time is passed in as milliseconds, so every rule is testable without a map. The map script
///     (PumpkinCarvingMapScript) acts on what these methods return.
/// </summary>
public sealed class PumpkinCarvingRound
{
    private readonly HashSet<uint> Barred = [];
    private readonly HashSet<uint> Carvers = [];
    private readonly Dictionary<uint, long> LastClosingSave = [];
    private readonly Dictionary<uint, long> LastSave = [];
    private readonly PumpkinSlot[] Slots = new PumpkinSlot[PumpkinCarving.PUMPKINS];
    private readonly Dictionary<uint, int> Votes = [];

    public PumpkinCarvingRound(long nowMs)
    {
        for (var i = 0; i < Slots.Length; i++)
            Slots[i] = Blank(i);

        State = PumpkinRoundState.Carving;
        DeadlineMs = nowMs + (PumpkinCarving.CARVING_SECONDS * 1000L);
    }

    public IReadOnlyCollection<uint> CarverIds => Carvers;

    public long DeadlineMs { get; private set; }

    public IReadOnlyList<PumpkinSlot> Pumpkins => Slots;

    public PumpkinRoundState State { get; private set; }

    public void CallOff() => State = PumpkinRoundState.Done;

    public (PumpkinClaim Result, int Slot) Claim(int slot, uint playerId, string name)
    {
        if (State != PumpkinRoundState.Carving)
            return (PumpkinClaim.NotCarving, slot);

        if (Barred.Contains(playerId))
            return (PumpkinClaim.Barred, slot);

        if (SlotOf(playerId) is { } own)
            return (PumpkinClaim.OwnPumpkin, own);

        var pumpkin = Slots[slot];

        if (pumpkin.Removed || pumpkin.OwnerId is not null)
            return (PumpkinClaim.Taken, slot);

        Slots[slot] = pumpkin with
        {
            OwnerId = playerId,
            OwnerName = name
        };

        Carvers.Add(playerId);

        return (PumpkinClaim.Claimed, slot);
    }

    /// <summary>
    ///     Time is up (or the director ended it): unclaimed pumpkins and those under the minimum cuts leave the stage. With
    ///     enough left the vote starts; otherwise the round is over.
    /// </summary>
    public PumpkinTickResult EndCarving(long nowMs)
    {
        if (State != PumpkinRoundState.Carving)
            return new PumpkinTickResult(PumpkinTick.None, []);

        var dropped = new List<int>();

        foreach (var pumpkin in Slots)
            if (!pumpkin.Removed && (pumpkin.OwnerId is null || (PumpkinGrid.CountCut(pumpkin.Grid) < PumpkinCarving.MIN_CUT_CELLS)))
            {
                Slots[pumpkin.Index] = pumpkin with { Removed = true };
                dropped.Add(pumpkin.Index);
            }

        if (Slots.Count(pumpkin => !pumpkin.Removed) < PumpkinCarving.MIN_FOR_VOTE)
        {
            State = PumpkinRoundState.Done;

            return new PumpkinTickResult(PumpkinTick.TooFew, dropped);
        }

        State = PumpkinRoundState.Voting;
        DeadlineMs = nowMs + (PumpkinCarving.VOTING_SECONDS * 1000L);

        return new PumpkinTickResult(PumpkinTick.Reveal, dropped);
    }

    /// <summary>Pumpkins still on stage with an owner and enough cuts: the carvers who earn a prize.</summary>
    public IReadOnlyList<int> Finishers()
        => Slots.Where(pumpkin => !pumpkin.Removed
                                  && pumpkin.OwnerId is not null
                                  && (PumpkinGrid.CountCut(pumpkin.Grid) >= PumpkinCarving.MIN_CUT_CELLS))
                .Select(pumpkin => pumpkin.Index)
                .ToArray();

    public bool IsCarver(uint playerId) => Carvers.Contains(playerId);

    /// <summary>
    ///     Someone left the map. A voter's vote goes. An owner's pumpkin is freed while carving, or leaves the stage later.
    ///     Returns the owner's slot, or null when they owned none.
    /// </summary>
    public int? Leave(uint playerId)
    {
        Votes.Remove(playerId);

        if ((State == PumpkinRoundState.Done) || SlotOf(playerId) is not { } slot)
            return null;

        if (State == PumpkinRoundState.Carving)
            Slots[slot] = Blank(slot);
        else
            TakeOff(slot);

        return slot;
    }

    /// <summary>The director removes a pumpkin. Its votes go and its carver can't claim another this round.</summary>
    public bool Remove(int slot)
    {
        if ((State == PumpkinRoundState.Done) || (slot < 0) || (slot >= Slots.Length) || Slots[slot].Removed)
            return false;

        if (Slots[slot].OwnerId is { } owner)
            Barred.Add(owner);

        TakeOff(slot);

        return true;
    }

    public bool Save(uint playerId, byte[] grid, bool closing, long nowMs)
    {
        if ((State != PumpkinRoundState.Carving) || SlotOf(playerId) is not { } slot || !PumpkinGrid.IsValid(grid))
            return false;

        //the periodic save and the closing save each get one a second, so a Done right after a periodic save still lands
        var last = closing ? LastClosingSave : LastSave;

        if (last.TryGetValue(playerId, out var at) && ((nowMs - at) < PumpkinCarving.SAVE_INTERVAL_MS))
            return false;

        last[playerId] = nowMs;
        Slots[slot] = Slots[slot] with { Grid = grid.ToArray() };

        return true;
    }

    public int SecondsLeft(long nowMs) => (int)Math.Max(0, (DeadlineMs - nowMs + 999) / 1000);

    public int? SlotOf(uint playerId)
    {
        foreach (var pumpkin in Slots)
            if (!pumpkin.Removed && (pumpkin.OwnerId == playerId))
                return pumpkin.Index;

        return null;
    }

    public PumpkinTickResult Tick(long nowMs)
    {
        if ((State == PumpkinRoundState.Carving) && (nowMs >= DeadlineMs))
            return EndCarving(nowMs);

        if ((State == PumpkinRoundState.Voting) && (nowMs >= DeadlineMs))
        {
            State = PumpkinRoundState.Done;

            return new PumpkinTickResult(PumpkinTick.Finished, []);
        }

        return new PumpkinTickResult(PumpkinTick.None, []);
    }

    public PumpkinVote Vote(uint voterId, int slot)
    {
        if (State != PumpkinRoundState.Voting)
            return PumpkinVote.NotVoting;

        if (Carvers.Contains(voterId))
            return PumpkinVote.CarverCantVote;

        if ((slot < 0) || (slot >= Slots.Length) || Slots[slot].Removed)
            return PumpkinVote.NotOnStage;

        if (Votes.TryGetValue(voterId, out var current) && (current == slot))
            return PumpkinVote.Unchanged;

        Votes[voterId] = slot;

        return PumpkinVote.Voted;
    }

    /// <summary>The pumpkins with the most votes, lowest slot first. Empty when nobody voted.</summary>
    public IReadOnlyList<int> Winners()
    {
        var tally = Votes.Values
                         .Where(slot => !Slots[slot].Removed)
                         .GroupBy(slot => slot)
                         .Select(group => (Slot: group.Key, Count: group.Count()))
                         .ToList();

        if (tally.Count == 0)
            return [];

        var best = tally.Max(entry => entry.Count);

        return tally.Where(entry => entry.Count == best)
                    .Select(entry => entry.Slot)
                    .Order()
                    .ToArray();
    }

    private static PumpkinSlot Blank(int index) => new(index, null, null, PumpkinGrid.Empty(), false);

    private void TakeOff(int slot)
    {
        Slots[slot] = Slots[slot] with { Removed = true };

        foreach (var voter in Votes.Where(vote => vote.Value == slot)
                                   .Select(vote => vote.Key)
                                   .ToArray())
            Votes.Remove(voter);
    }
}
```

- [ ] **Step 5: Run the tests and see them pass.**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/PumpkinCarvingRoundTests/*"`
Expected: 18 tests pass.

```json:metadata
{"files": ["Chaos/Services/Theatre/PumpkinCarving.cs", "Chaos/Services/Theatre/PumpkinCarvingRound.cs", "Tests/Chaos.Tests/Theatre/PumpkinCarvingRoundTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/PumpkinCarvingRoundTests/*\"", "acceptanceCriteria": ["one pumpkin per player, taken refused, removed carver barred", "saves need owner, Carving state, valid grid, 1/s per kind", "time up drops blank and thin; under 2 ends with no vote", "carvers can't vote, votes change, ties name all, no votes no winner", "leave frees while carving, removes later, drops a voter's vote", "orange-bar texts <= 45 chars"], "modelTier": "standard"}
```

---

### Task 3: The carving map script (server)

**Goal:** `PumpkinCarvingMapScript` runs a round on the map: spawn, claims, saves, reveal with house lights, votes, prizes, marks, the display pumpkin and its storage.

**Files:**
- Create: `S/Chaos/Services/Theatre/PumpkinDisplay.cs`
- Create: `S/Chaos/Scripting/MapScripts/Temuair/Suomi/PumpkinCarvingMapScript.cs`
- Modify: `S/Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs` (add `ApplySavedScene`)
- Test: `S/Tests/Chaos.Tests/Theatre/PumpkinCarvingMapTests.cs`

**Acceptance Criteria:**
- [ ] A director's start spawns 8 merchants named "Blank Pumpkin" on the pumpkin tiles, facing Right; a non-director is refused.
- [ ] Arriving on a claim tile renames that pumpkin "<Name>'s pumpkin" and opens the window once (standing still does not reopen it).
- [ ] At time up: windows close, dropped pumpkins despawn, every player gets a Lit look for each remaining pumpkin, and the house goes dark (when a theatre script is present).
- [ ] After the vote: winner 20 candy + both marks, other finishers 5 candy + the participation mark, candy refused within 24 h, lights restored, pumpkins gone, the display pumpkin spawned at (11, 15) and saved.
- [ ] Too few carved: no vote, the lone finisher gets 5 candy.
- [ ] Director removal despawns the pumpkin and tells its carver; a carver leaving while carving gets a fresh blank pumpkin in its place.
- [ ] Before the reveal, `LookFor` returns Blank with no grid.
- [ ] The display pumpkin is not shown outside the Halloween window.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/PumpkinCarvingMapTests/*"` → all pass; then the whole `Theatre` folder: `--treenode-filter "/*/Chaos.Tests.Theatre/*/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** — `S/Tests/Chaos.Tests/Theatre/PumpkinCarvingMapTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Definitions;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions;
using Chaos.Models.World;
using Chaos.Networking.Entities.Server;
using Chaos.Scripting.MapScripts.Temuair.Suomi;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.Theatre;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
#endregion

namespace Chaos.Tests.Theatre;

public sealed class PumpkinCarvingMapTests
{
    private static readonly DateTime Start = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    private sealed class Clock
    {
        public DateTime Now = Start;

        public void Advance(int seconds) => Now = Now.AddSeconds(seconds);
    }

    private sealed record Game(
        PumpkinCarvingMapScript Script,
        MapInstance Map,
        Aisling Director,
        PumpkinDisplay Display,
        Mock<IStorage<PumpkinDisplay>> Storage,
        Clock Clock,
        SuomiTheatreMapScript? Theatre);

    private static Game Create(bool halloween = true, bool withTheatre = false)
    {
        var map = MockMapInstance.Create(width: 32, height: 32);
        var director = MockAisling.Create(map, name: "Dana", position: new Point(12, 16));
        director.Trackers.Enums.Set(TheatreRoles.Director);
        map.AddEntity(director, new Point(12, 16));

        var display = new PumpkinDisplay();
        var storage = new Mock<IStorage<PumpkinDisplay>>();
        storage.Setup(s => s.Value).Returns(display);

        var clock = new Clock();

        var theatre = withTheatre
            ? new SuomiTheatreMapScript(
                map,
                new Mock<IEffectFactory>().Object,
                new Mock<IStorage<TheatreLightingScenes>>().Object,
                new Mock<ISimpleCache>().Object,
                new Mock<ILogger<SuomiTheatreMapScript>>().Object,
                ItemFactory(),
                () => clock.Now)
            : null;

        var script = new PumpkinCarvingMapScript(
            map,
            MerchantFactory(),
            ItemFactory(),
            new Mock<IDialogFactory>().Object,
            storage.Object,
            () => clock.Now,
            () => theatre,
            () => halloween);

        return new Game(script, map, director, display, storage, clock, theatre);
    }

    private static IMerchantFactory MerchantFactory()
    {
        var factory = new Mock<IMerchantFactory>();

        factory.Setup(f => f.Create(PumpkinCarving.PumpkinTemplateKey, It.IsAny<MapInstance>(), It.IsAny<IPoint>(), It.IsAny<ICollection<string>?>()))
               .Returns((string _, MapInstance map, IPoint _, ICollection<string>? _) => MockMerchant.Create(map, name: PumpkinCarving.PumpkinTemplateKey));

        return factory.Object;
    }

    private static IItemFactory ItemFactory()
    {
        var factory = new Mock<IItemFactory>();

        factory.Setup(f => f.Create(PumpkinCarving.CandyTemplateKey, It.IsAny<ICollection<string>?>()))
               .Returns(() => MockItem.Create("Halloween Candy", stackable: true));

        return factory.Object;
    }

    private static Aisling Person(Game game, string name, Point tile)
    {
        var aisling = MockAisling.Create(game.Map, name: name, position: tile);
        aisling.UserStatSheet.SetMaxWeight(50);
        game.Map.AddEntity(aisling, tile);

        return aisling;
    }

    private static Aisling Carver(Game game, string name, int slot, int cuts)
    {
        var aisling = Person(game, name, PumpkinCarving.ClaimTiles[slot]);
        game.Script.Tick();
        game.Script.HandleSave(aisling, new PumpkinCarvingSaveArgs { Done = false, Grid = Carved(cuts) });

        return aisling;
    }

    private static byte[] Carved(int cuts)
    {
        var grid = PumpkinGrid.Empty();

        for (var i = 0; i < cuts; i++)
            PumpkinGrid.SetCut(grid, i % PumpkinGrid.WIDTH, i / PumpkinGrid.WIDTH, true);

        return grid;
    }

    private static Merchant? PumpkinAt(Game game, Point tile)
        => game.Map.GetEntitiesAtPoints<Merchant>(tile).SingleOrDefault();

    private static void EndCarving(Game game)
    {
        game.Clock.Advance(PumpkinCarving.CARVING_SECONDS);
        game.Script.Tick();
    }

    private static void EndVoting(Game game)
    {
        game.Clock.Advance(PumpkinCarving.VOTING_SECONDS);
        game.Script.Tick();
    }

    [Test]
    public void A_director_starts_with_eight_blank_pumpkins_facing_the_house()
    {
        var game = Create();

        game.Script.TryStart(game.Director).Should().Be(PumpkinCarvingRefusal.None);

        foreach (var tile in PumpkinCarving.PumpkinTiles)
        {
            var pumpkin = PumpkinAt(game, tile);
            pumpkin.Should().NotBeNull();
            pumpkin!.Name.Should().Be(PumpkinCarving.BlankName);
            pumpkin.Direction.Should().Be(Direction.Right);
        }

        game.Script.Running.Should().BeTrue();
    }

    [Test]
    public void Only_a_director_can_start()
    {
        var game = Create();
        var guest = Person(game, "Gus", new Point(14, 16));

        game.Script.TryStart(guest).Should().Be(PumpkinCarvingRefusal.NotDirector);
        game.Script.Running.Should().BeFalse();
    }

    [Test]
    public void Stepping_on_a_claim_tile_claims_the_pumpkin_and_opens_the_window_once()
    {
        var game = Create();
        game.Script.TryStart(game.Director);

        var mira = Person(game, "Mira", PumpkinCarving.ClaimTiles[2]);
        game.Script.Tick();
        game.Script.Tick();

        PumpkinAt(game, PumpkinCarving.PumpkinTiles[2])!.Name.Should().Be("Mira's pumpkin");
        Mock.Get(mira.Client).Verify(c => c.SendPumpkinCarvingOpen(It.IsAny<ushort>(), It.IsAny<byte[]>()), Times.Once());
    }

    [Test]
    public void Before_the_reveal_a_pumpkin_looks_blank()
    {
        var game = Create();
        game.Script.TryStart(game.Director);
        Carver(game, "Mira", 0, 10);

        var look = game.Script.LookFor(PumpkinAt(game, PumpkinCarving.PumpkinTiles[0])!);

        look!.State.Should().Be(PumpkinLookState.Blank);
        look.Grid.Should().BeNull();
    }

    [Test]
    public void Time_up_lights_the_carved_pumpkins_and_drops_the_rest()
    {
        var game = Create(withTheatre: true);
        game.Script.TryStart(game.Director);
        var mira = Carver(game, "Mira", 0, 10);
        Carver(game, "Otto", 1, 8);
        Carver(game, "Nia", 2, 3);
        var guest = Person(game, "Gus", new Point(14, 16));

        EndCarving(game);

        PumpkinAt(game, PumpkinCarving.PumpkinTiles[0]).Should().NotBeNull();
        PumpkinAt(game, PumpkinCarving.PumpkinTiles[1]).Should().NotBeNull();
        PumpkinAt(game, PumpkinCarving.PumpkinTiles[2]).Should().BeNull();
        PumpkinAt(game, PumpkinCarving.PumpkinTiles[7]).Should().BeNull();
        Mock.Get(mira.Client).Verify(c => c.SendPumpkinCarvingClose(), Times.Once());
        Mock.Get(guest.Client).Verify(c => c.SendPumpkinLook(It.Is<PumpkinLookArgs>(a => a.State == PumpkinLookState.Lit)), Times.Exactly(2));
        game.Theatre!.Lighting.IsDark.Should().BeTrue();
    }

    [Test]
    public void The_vote_pays_the_winner_and_the_finishers_and_puts_the_winner_on_display()
    {
        var game = Create(withTheatre: true);
        game.Script.TryStart(game.Director);
        var mira = Carver(game, "Mira", 0, 10);
        var otto = Carver(game, "Otto", 1, 8);
        var guest = Person(game, "Gus", new Point(14, 16));
        EndCarving(game);

        game.Script.HandleClick(guest, PumpkinAt(game, PumpkinCarving.PumpkinTiles[0])!);
        EndVoting(game);

        mira.Legend.GetCount(PumpkinCarving.WinnerKey).Should().Be(1);
        mira.Legend.ContainsKey(PumpkinCarving.ParticipationKey).Should().BeTrue();
        mira.Inventory.CountOf("Halloween Candy").Should().Be(20);
        otto.Legend.ContainsKey(PumpkinCarving.WinnerKey).Should().BeFalse();
        otto.Legend.ContainsKey(PumpkinCarving.ParticipationKey).Should().BeTrue();
        otto.Inventory.CountOf("Halloween Candy").Should().Be(5);
        PumpkinAt(game, PumpkinCarving.PumpkinTiles[0]).Should().BeNull();
        PumpkinAt(game, PumpkinCarving.DisplayTile)!.Name.Should().Be("Mira's pumpkin");
        game.Display.OwnerName.Should().Be("Mira");
        game.Storage.Verify(s => s.Save(), Times.AtLeastOnce());
        game.Theatre!.Lighting.IsDark.Should().BeFalse();
        game.Script.Running.Should().BeFalse();
    }

    [Test]
    public void Candy_comes_once_a_day()
    {
        var game = Create();
        game.Script.TryStart(game.Director);
        var mira = Carver(game, "Mira", 0, 10);
        Carver(game, "Otto", 1, 8);
        mira.Trackers.TimedEvents.AddEvent(PumpkinCarving.CandyEvent, TimeSpan.FromHours(24), true);
        EndCarving(game);
        EndVoting(game);

        mira.Inventory.CountOf("Halloween Candy").Should().Be(0);
        mira.Legend.ContainsKey(PumpkinCarving.ParticipationKey).Should().BeTrue();
    }

    [Test]
    public void Too_few_carvings_end_without_a_vote_and_pay_the_finisher()
    {
        var game = Create();
        game.Script.TryStart(game.Director);
        var mira = Carver(game, "Mira", 0, 10);

        EndCarving(game);

        game.Script.Running.Should().BeFalse();
        mira.Inventory.CountOf("Halloween Candy").Should().Be(5);
        PumpkinAt(game, PumpkinCarving.PumpkinTiles[0]).Should().BeNull();
        PumpkinAt(game, PumpkinCarving.DisplayTile).Should().BeNull();
    }

    [Test]
    public void The_director_removes_a_pumpkin_and_its_carver_is_told()
    {
        var game = Create();
        game.Script.TryStart(game.Director);
        var mira = Carver(game, "Mira", 0, 10);

        game.Script.Remove(game.Director, PumpkinAt(game, PumpkinCarving.PumpkinTiles[0])!);

        PumpkinAt(game, PumpkinCarving.PumpkinTiles[0]).Should().BeNull();
        Mock.Get(mira.Client).Verify(c => c.SendPumpkinCarvingClose(), Times.Once());
        Mock.Get(mira.Client).Verify(c => c.SendServerMessage(It.IsAny<ServerMessageType>(), PumpkinCarving.RemovedText), Times.Once());
    }

    [Test]
    public void A_carver_leaving_while_carving_leaves_a_blank_pumpkin()
    {
        var game = Create();
        game.Script.TryStart(game.Director);
        var mira = Carver(game, "Mira", 0, 10);

        game.Map.RemoveEntity(mira);
        game.Script.OnExited(mira);

        PumpkinAt(game, PumpkinCarving.PumpkinTiles[0])!.Name.Should().Be(PumpkinCarving.BlankName);
    }

    [Test]
    public void The_display_pumpkin_only_shows_during_halloween()
    {
        var outside = Create(halloween: false);
        outside.Display.Set("Mira", Carved(10));

        outside.Script.Tick();

        PumpkinAt(outside, PumpkinCarving.DisplayTile).Should().BeNull();

        var inside = Create();
        inside.Display.Set("Mira", Carved(10));

        inside.Script.Tick();

        var shown = PumpkinAt(inside, PumpkinCarving.DisplayTile);
        shown!.Name.Should().Be("Mira's pumpkin");
        inside.Script.LookFor(shown)!.State.Should().Be(PumpkinLookState.Lit);
    }

    [Test]
    public void Removing_the_display_pumpkin_clears_the_saved_winner()
    {
        var game = Create();
        game.Display.Set("Mira", Carved(10));
        game.Script.Tick();

        game.Script.Remove(game.Director, PumpkinAt(game, PumpkinCarving.DisplayTile)!);

        PumpkinAt(game, PumpkinCarving.DisplayTile).Should().BeNull();
        game.Display.HasWinner.Should().BeFalse();
        game.Storage.Verify(s => s.Save(), Times.Once());
    }
}
```

`ServerMessageType` needs `using Chaos.DarkAges.Definitions;` (already listed). If a `using` is missing or a namespace differs (`Direction`, `ISimpleCache`, `IEffectFactory`), add the one the compiler names; `SpotlightChairsMapTests.cs` uses all of these types.

- [ ] **Step 2: Run them and see them fail** (compile errors: `PumpkinCarvingMapScript`, `PumpkinDisplay`).

- [ ] **Step 3: Add `PumpkinDisplay`** — `S/Chaos/Services/Theatre/PumpkinDisplay.cs`:

```csharp
#region
using System.Text.Json.Serialization;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.Theatre;

/// <summary>
///     The last Pumpkin Carving winner, shown in the haunted theatre until the next winner. Stored through
///     <c>IStorage&lt;T&gt;</c> as <c>PumpkinDisplay.json</c>; the grid is written as base64.
/// </summary>
public sealed class PumpkinDisplay
{
    public byte[]? Grid { get; set; }

    [JsonIgnore]
    public bool HasWinner => OwnerName is not null && Grid is not null && PumpkinGrid.IsValid(Grid);

    public string? OwnerName { get; set; }

    public void Clear()
    {
        OwnerName = null;
        Grid = null;
    }

    public void Set(string ownerName, byte[] grid)
    {
        OwnerName = ownerName;
        Grid = grid.ToArray();
    }
}
```

`IStorage<T>` is registered as an open generic, so no service registration is needed.

- [ ] **Step 4: Add `ApplySavedScene` to the theatre script.** In `S/Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs`, insert after `SetHouseLevel`:

```csharp
    /// <summary>Puts back lighting saved with <see cref="StageLighting.Snapshot" /> and tells everyone, as Spotlight Chairs does.</summary>
    public void ApplySavedScene(StageLightingScene scene)
    {
        Lighting.ApplyScene(scene);
        SyncDarknessAndBroadcast(FADE_SCENE);
    }
```

- [ ] **Step 5: Write the map script** — `S/Chaos/Scripting/MapScripts/Temuair/Suomi/PumpkinCarvingMapScript.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Definitions;
using Chaos.Geometry;
using Chaos.Models.Legend;
using Chaos.Models.World;
using Chaos.Models.World.Abstractions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Scripting.MapScripts.Abstractions;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.Theatre;
using Chaos.Storage.Abstractions;
using Chaos.Time;
using Chaos.Time.Abstractions;
#endregion

namespace Chaos.Scripting.MapScripts.Temuair.Suomi;

/// <summary>
///     Runs Pumpkin Carving on the haunted theatre (spec: Chaos.Client docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md).
///     <see cref="PumpkinCarvingRound" /> holds the rules; this script spawns the pumpkins as merchants, opens the carving
///     windows, sends looks, sets the house lights through the theatre's own script, pays out and keeps the display
///     pumpkin.
/// </summary>
public sealed class PumpkinCarvingMapScript : MapScriptBase
{
    private readonly IDialogFactory DialogFactory;
    private readonly IStorage<PumpkinDisplay> DisplayStorage;
    private readonly Func<SuomiTheatreMapScript?> FindTheatre;
    private readonly Func<bool> IsHalloween;
    private readonly IItemFactory ItemFactory;
    private readonly IMerchantFactory MerchantFactory;

    /// <summary>Who stood on which claim tile at the last tick, so standing still doesn't reopen the window every second.</summary>
    private readonly Dictionary<uint, int> OnClaimTile = [];

    private readonly Merchant?[] Pumpkins = new Merchant?[PumpkinCarving.PUMPKINS];
    private readonly IIntervalTimer UpdateTimer = new IntervalTimer(TimeSpan.FromSeconds(1));
    private readonly Func<DateTime> UtcNow;

    private Merchant? Display;
    private bool KeepOutsideWindow;
    private PumpkinCarvingRound? Round;
    private StageLightingScene? SavedLighting;

    public PumpkinCarvingMapScript(
        MapInstance subject,
        IMerchantFactory merchantFactory,
        IItemFactory itemFactory,
        IDialogFactory dialogFactory,
        IStorage<PumpkinDisplay> displayStorage,
        Func<DateTime>? utcNow = null,
        Func<SuomiTheatreMapScript?>? theatre = null,
        Func<bool>? isHalloween = null)
        : base(subject)
    {
        MerchantFactory = merchantFactory;
        ItemFactory = itemFactory;
        DialogFactory = dialogFactory;
        DisplayStorage = displayStorage;
        UtcNow = utcNow ?? (() => DateTime.UtcNow);
        FindTheatre = theatre ?? (() => Subject.Script.Is<SuomiTheatreMapScript>(out var found) ? found : null);
        IsHalloween = isHalloween ?? (() => EventPeriod.IsSpecificEventActive(UtcNow(), Subject.LoadedFromInstanceId, EventType.Halloween));
    }

    public bool Available => IsHalloween();

    public bool Running => Round is { State: not PumpkinRoundState.Done };

    public PumpkinRoundState? State => Round?.State;

    private long NowMs => UtcNow().Ticks / TimeSpan.TicksPerMillisecond;

    /// <summary>A Vote option is offered only to a non-carver while that pumpkin is up for the vote.</summary>
    public bool CanVote(Aisling voter, Merchant pumpkin)
        => Round is { State: PumpkinRoundState.Voting } round && !round.IsCarver(voter.Id) && (Array.IndexOf(Pumpkins, pumpkin) >= 0);

    public void CallOff(Aisling director)
    {
        if (SuomiTheatreMapScript.CanDirect(director))
            CallOff(announce: true);
    }

    public void CallOff(bool announce)
    {
        if (Round is not { } round)
            return;

        round.CallOff();
        CloseWindows(round);

        if (announce)
            Announce(PumpkinCarving.CalledOffText);

        End();
    }

    public void EndCarvingNow(Aisling director)
    {
        if (SuomiTheatreMapScript.CanDirect(director) && Round is { State: PumpkinRoundState.Carving } round)
            Apply(round, round.EndCarving(NowMs));
    }

    /// <summary>A click on a pumpkin, routed from its merchant script.</summary>
    public void HandleClick(Aisling source, Merchant pumpkin)
    {
        if (ReferenceEquals(pumpkin, Display))
        {
            if (SuomiTheatreMapScript.CanDirect(source))
                DialogFactory.Create(PumpkinCarving.DirectorDialogKey, pumpkin)
                             .Display(source);

            return;
        }

        var slot = Array.IndexOf(Pumpkins, pumpkin);

        if ((slot < 0) || Round is not { } round)
            return;

        if (SuomiTheatreMapScript.CanDirect(source))
        {
            DialogFactory.Create(PumpkinCarving.DirectorDialogKey, pumpkin)
                         .Display(source);

            return;
        }

        if (round.State == PumpkinRoundState.Carving)
        {
            if (round.SlotOf(source.Id) == slot)
                OpenWindow(source);

            return;
        }

        Vote(source, pumpkin);
    }

    public void HandleSave(Aisling source, PumpkinCarvingSaveArgs args) => Round?.Save(source.Id, args.Grid, args.Done, NowMs);

    /// <summary>How a pumpkin looks to a player, or null when the merchant isn't one of this game's pumpkins.</summary>
    public PumpkinLookArgs? LookFor(Merchant pumpkin)
    {
        if (ReferenceEquals(pumpkin, Display) && DisplayStorage.Value is { HasWinner: true } stored)
            return Lit(pumpkin, stored.Grid!);

        var slot = Array.IndexOf(Pumpkins, pumpkin);

        if (slot < 0)
            return null;

        //the grid never leaves the server before the reveal
        return Round is { State: PumpkinRoundState.Voting } round
            ? Lit(pumpkin, round.Pumpkins[slot].Grid)
            : new PumpkinLookArgs
            {
                EntityId = pumpkin.Id,
                State = PumpkinLookState.Blank
            };
    }

    public override void OnExited(Creature creature)
    {
        if (creature is not Aisling aisling)
            return;

        OnClaimTile.Remove(aisling.Id);

        if (Round is not { } round || round.Leave(aisling.Id) is not { } slot)
            return;

        if (round.State == PumpkinRoundState.Carving)
            Replace(slot, PumpkinCarving.BlankName);
        else
            TakeOff(slot);
    }

    public void Remove(Aisling director, Merchant pumpkin)
    {
        if (!SuomiTheatreMapScript.CanDirect(director))
            return;

        if (ReferenceEquals(pumpkin, Display))
        {
            Despawn(Display);
            Display = null;
            DisplayStorage.Value.Clear();
            DisplayStorage.Save();

            return;
        }

        var slot = Array.IndexOf(Pumpkins, pumpkin);

        if ((slot < 0) || Round is not { } round)
            return;

        var owner = round.Pumpkins[slot].OwnerId;

        if (!round.Remove(slot))
            return;

        TakeOff(slot);

        if (owner is not { } ownerId || !Subject.TryGetEntity<Aisling>(ownerId, out var carver))
            return;

        carver.Client.SendPumpkinCarvingClose();
        carver.SendOrangeBarMessage(PumpkinCarving.RemovedText);
    }

    /// <summary>One second of the game. <see cref="Update" /> calls it; tests call it directly.</summary>
    public void Tick()
    {
        var halloween = IsHalloween();

        if (Running && !halloween && !KeepOutsideWindow)
            CallOff(announce: false);

        SyncDisplay(halloween);

        if (Round is not { } round || (round.State == PumpkinRoundState.Done))
            return;

        if (round.State == PumpkinRoundState.Carving)
            CheckClaims(round);

        Apply(round, round.Tick(NowMs));
    }

    public PumpkinCarvingRefusal TryStart(Aisling starter)
    {
        var refusal = Refusal(starter);

        if (refusal != PumpkinCarvingRefusal.None)
        {
            starter.SendOrangeBarMessage(PumpkinCarving.RefusalText(refusal));

            return refusal;
        }

        KeepOutsideWindow = starter.IsAdmin && !IsHalloween();
        Round = new PumpkinCarvingRound(NowMs);

        for (var slot = 0; slot < Pumpkins.Length; slot++)
            Replace(slot, PumpkinCarving.BlankName);

        Announce(PumpkinCarving.StartText);

        return PumpkinCarvingRefusal.None;
    }

    public override void Update(TimeSpan delta)
    {
        UpdateTimer.Update(delta);

        if (UpdateTimer.IntervalElapsed)
            Tick();
    }

    public void Vote(Aisling voter, Merchant pumpkin)
    {
        var slot = Array.IndexOf(Pumpkins, pumpkin);

        if ((slot < 0) || Round is not { } round)
            return;

        switch (round.Vote(voter.Id, slot))
        {
            case PumpkinVote.Voted:
                voter.SendOrangeBarMessage(PumpkinCarving.VotedText(round.Pumpkins[slot].OwnerName ?? string.Empty));

                break;

            case PumpkinVote.CarverCantVote:
                voter.SendOrangeBarMessage(PumpkinCarving.CarverCantVoteText);

                break;
        }
    }

    private void Announce(string text)
    {
        foreach (var aisling in Subject.GetEntities<Aisling>())
            aisling.SendActiveMessage(text);
    }

    private void Apply(PumpkinCarvingRound round, PumpkinTickResult result)
    {
        foreach (var slot in result.Dropped)
            TakeOff(slot);

        switch (result.Kind)
        {
            case PumpkinTick.Reveal:
                Reveal(round);

                break;

            case PumpkinTick.TooFew:
                CloseWindows(round);
                Announce(PumpkinCarving.TooFewText);
                Pay(round, []);
                End();

                break;

            case PumpkinTick.Finished:
                Finish(round);

                break;
        }
    }

    private void CheckClaims(PumpkinCarvingRound round)
    {
        var standing = new Dictionary<uint, int>();

        foreach (var aisling in Subject.GetEntities<Aisling>()
                                       .ToList())
        {
            var slot = Array.FindIndex(PumpkinCarving.ClaimTiles, tile => (tile.X == aisling.X) && (tile.Y == aisling.Y));

            if (slot < 0)
                continue;

            standing[aisling.Id] = slot;

            if (OnClaimTile.TryGetValue(aisling.Id, out var before) && (before == slot))
                continue;

            Claim(round, aisling, slot);
        }

        OnClaimTile.Clear();

        foreach ((var id, var slot) in standing)
            OnClaimTile[id] = slot;
    }

    private void Claim(PumpkinCarvingRound round, Aisling aisling, int slot)
    {
        (var result, var owned) = round.Claim(slot, aisling.Id, aisling.Name);

        switch (result)
        {
            case PumpkinClaim.Claimed:
                Replace(owned, PumpkinCarving.PumpkinName(aisling.Name));
                OpenWindow(aisling);

                break;

            case PumpkinClaim.OwnPumpkin:
                OpenWindow(aisling);

                break;

            case PumpkinClaim.Taken:
                aisling.SendOrangeBarMessage(PumpkinCarving.TakenText);

                break;

            case PumpkinClaim.Barred:
                aisling.SendOrangeBarMessage(PumpkinCarving.RemovedText);

                break;
        }
    }

    private void CloseWindows(PumpkinCarvingRound round)
    {
        foreach (var id in round.CarverIds)
            if (Subject.TryGetEntity<Aisling>(id, out var carver))
                carver.Client.SendPumpkinCarvingClose();
    }

    private Merchant Create(Point tile, string name)
    {
        var pumpkin = MerchantFactory.Create(PumpkinCarving.PumpkinTemplateKey, Subject, tile);
        pumpkin.Name = name;
        pumpkin.Direction = Direction.Right;

        return pumpkin;
    }

    private void Despawn(Merchant? pumpkin)
    {
        if (pumpkin is not null)
            Subject.RemoveEntity(pumpkin);
    }

    private void End()
    {
        for (var slot = 0; slot < Pumpkins.Length; slot++)
            TakeOff(slot);

        if (SavedLighting is { } saved && FindTheatre() is { } theatre)
            theatre.ApplySavedScene(saved);

        SavedLighting = null;
        Round = null;
        KeepOutsideWindow = false;
        OnClaimTile.Clear();
    }

    private void Finish(PumpkinCarvingRound round)
    {
        var winners = round.Winners();

        Announce(
            winners.Count == 0
                ? PumpkinCarving.NoVotesText
                : PumpkinCarving.WinnerAnnouncement(winners.Select(slot => round.Pumpkins[slot].OwnerName ?? string.Empty).ToList()));

        Pay(round, winners);

        if (winners.Count > 0)
        {
            var top = round.Pumpkins[winners[0]];
            DisplayStorage.Value.Set(top.OwnerName ?? string.Empty, top.Grid);
            DisplayStorage.Save();
            Despawn(Display);
            Display = null;
        }

        End();
        SyncDisplay(IsHalloween());
    }

    private void GiveCandy(Aisling aisling, int count)
    {
        if (aisling.Trackers.TimedEvents.HasActiveEvent(PumpkinCarving.CandyEvent, out _))
        {
            aisling.SendOrangeBarMessage(PumpkinCarving.CandyWaitText);

            return;
        }

        var candy = ItemFactory.Create(PumpkinCarving.CandyTemplateKey);
        candy.Count = count;
        aisling.GiveItemOrSendToBank(candy);
        aisling.Trackers.TimedEvents.AddEvent(PumpkinCarving.CandyEvent, TimeSpan.FromHours(24), true);
    }

    private static PumpkinLookArgs Lit(Merchant pumpkin, byte[] grid)
        => new()
        {
            EntityId = pumpkin.Id,
            State = PumpkinLookState.Lit,
            Grid = grid
        };

    private void OpenWindow(Aisling aisling)
    {
        if (Round is not { State: PumpkinRoundState.Carving } round || round.SlotOf(aisling.Id) is not { } slot)
            return;

        aisling.Client.SendPumpkinCarvingOpen((ushort)round.SecondsLeft(NowMs), round.Pumpkins[slot].Grid);
    }

    private void Pay(PumpkinCarvingRound round, IReadOnlyList<int> winners)
    {
        foreach (var slot in round.Finishers())
        {
            if (round.Pumpkins[slot].OwnerId is not { } id || !Subject.TryGetEntity<Aisling>(id, out var carver))
                continue;

            var won = winners.Contains(slot);

            carver.Legend.AddOrAccumulate(
                new LegendMark(
                    PumpkinCarving.ParticipationText,
                    PumpkinCarving.ParticipationKey,
                    MarkIcon.Yay,
                    MarkColor.White,
                    1,
                    GameTime.Now));

            if (won)
                carver.Legend.AddOrAccumulate(
                    new LegendMark(
                        PumpkinCarving.WinnerText,
                        PumpkinCarving.WinnerKey,
                        MarkIcon.Yay,
                        MarkColor.White,
                        1,
                        GameTime.Now));

            GiveCandy(carver, won ? PumpkinCarving.WINNER_CANDY : PumpkinCarving.FINISHER_CANDY);
        }
    }

    private PumpkinCarvingRefusal Refusal(Aisling starter)
    {
        if (!SuomiTheatreMapScript.CanDirect(starter))
            return PumpkinCarvingRefusal.NotDirector;

        if (!IsHalloween() && !starter.IsAdmin)
            return PumpkinCarvingRefusal.OutsideWindow;

        if (Running)
            return PumpkinCarvingRefusal.AlreadyRunning;

        if (FindTheatre() is { SpotlightRunning: true })
            return PumpkinCarvingRefusal.StageBusy;

        return PumpkinCarvingRefusal.None;
    }

    /// <summary>
    ///     Puts a new pumpkin in a slot, replacing any there. The merchant is registered before it is added to the map,
    ///     because adding it sends it to nearby players and the look hook must already know it.
    /// </summary>
    private void Replace(int slot, string name)
    {
        Despawn(Pumpkins[slot]);

        var tile = PumpkinCarving.PumpkinTiles[slot];
        var pumpkin = Create(tile, name);
        Pumpkins[slot] = pumpkin;
        Subject.AddEntity(pumpkin, tile);
    }

    private void Reveal(PumpkinCarvingRound round)
    {
        CloseWindows(round);

        if (FindTheatre() is { } theatre)
        {
            SavedLighting = theatre.Lighting.Snapshot("pumpkin-carving");
            theatre.SetHouseLevel(0, SuomiTheatreMapScript.FADE_SCENE);
        }

        var looks = Pumpkins.OfType<Merchant>()
                            .Select(LookFor)
                            .OfType<PumpkinLookArgs>()
                            .ToList();

        foreach (var aisling in Subject.GetEntities<Aisling>())
            foreach (var look in looks)
                aisling.Client.SendPumpkinLook(look);

        Announce(PumpkinCarving.RevealText);
    }

    private void SyncDisplay(bool halloween)
    {
        var stored = DisplayStorage.Value;

        if (!halloween || !stored.HasWinner)
        {
            Despawn(Display);
            Display = null;

            return;
        }

        if (Display is not null)
            return;

        var pumpkin = Create(PumpkinCarving.DisplayTile, PumpkinCarving.PumpkinName(stored.OwnerName!));
        Display = pumpkin;
        Subject.AddEntity(pumpkin, PumpkinCarving.DisplayTile);
    }

    private void TakeOff(int slot)
    {
        Despawn(Pumpkins[slot]);
        Pumpkins[slot] = null;
    }
}
```

Notes for the implementer:
- `Direction` lives in `Chaos.Geometry.Abstractions.Definitions` (or wherever `SuomiTheatreMapScript`'s neighbours import it from); add that `using`.
- If `Merchant.Name` or `Merchant.Direction` has no public setter, set them through the factory's `extraScriptKeys`-free path the way other code renames merchants, and report what you used.
- `SendActiveMessage` / `SendOrangeBarMessage` are the same extension methods `SuomiTheatreMapScript` uses.

- [ ] **Step 6: Run the tests and see them pass.**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/PumpkinCarvingMapTests/*"`
Expected: 12 tests pass. Then run the whole theatre folder: `--treenode-filter "/*/Chaos.Tests.Theatre/*/*"`; expected all pass.

```json:metadata
{"files": ["Chaos/Services/Theatre/PumpkinDisplay.cs", "Chaos/Scripting/MapScripts/Temuair/Suomi/PumpkinCarvingMapScript.cs", "Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs", "Tests/Chaos.Tests/Theatre/PumpkinCarvingMapTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/PumpkinCarvingMapTests/*\"", "acceptanceCriteria": ["start spawns 8 blank pumpkins facing Right; non-director refused", "claim tile renames and opens window once", "time up closes windows, drops, sends Lit looks, darkens house", "vote pays 20/5, marks, 24h limit, restores lights, spawns + saves display", "too few: no vote, lone finisher 5 candy", "director removal despawns and tells carver; leaving while carving leaves a blank", "LookFor before reveal is Blank with no grid", "display hidden outside Halloween"], "modelTier": "standard"}
```

---

### Task 4: Wire the game into the theatre and the network (server)

**Goal:** Pumpkin clicks, the look hook, the save handler, Thulin's options, the director's pumpkin menu, and mutual exclusion with Spotlight Chairs and the lighting tools.

**Files:**
- Create: `S/Chaos/Services/Theatre/PumpkinLooks.cs`
- Create: `S/Chaos/Scripting/MerchantScripts/Suomi/PumpkinCarvingScript.cs`
- Create: `S/Chaos/Scripting/DialogScripts/Temuair/Suomi/PumpkinCarvingDirectorScript.cs`
- Modify: `S/Chaos/Networking/ChaosWorldClient.cs` (`SendVisibleEntities`)
- Modify: `S/Chaos/Services/Servers/WorldServer.cs`
- Modify: `S/Chaos/Services/Theatre/SpotlightChairs.cs`
- Modify: `S/Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs`
- Modify: `S/Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs`
- Test: `S/Tests/Chaos.Tests/Theatre/PumpkinCarvingTheatreTests.cs`

**Acceptance Criteria:**
- [ ] Spotlight Chairs refuses to start while carving runs ("Pumpkin Carving is using the stage."), and carving refuses while Spotlight Chairs runs.
- [ ] The lighting window and Thulin's lights on/off refuse while either game runs, with that game's text.
- [ ] `PumpkinLooks.For` returns null for a merchant that isn't a carving pumpkin and the game's look otherwise; `SendVisibleEntities` sends it after each chunk.
- [ ] A `PumpkinCarvingSave` packet reaches `HandleSave` on the sender's map.
- [ ] Thulin's director menu shows Start / Call Off / End the Carving Now at the right times, and their Yes answers call the map script.
- [ ] The director's pumpkin menu offers Vote only when `CanVote` is true, and its options vote or remove.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/Chaos.Tests.Theatre/*/*"` → all pass; then `dotnet build Chaos.slnx` → 0 errors

**Steps:**

- [ ] **Step 1: Write the failing tests** — `S/Tests/Chaos.Tests/Theatre/PumpkinCarvingTheatreTests.cs`:

```csharp
#region
using System.Reflection;
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Definitions;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions;
using Chaos.Models.World;
using Chaos.Scripting.MapScripts.Abstractions;
using Chaos.Scripting.MapScripts.Temuair.Suomi;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.Theatre;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
#endregion

namespace Chaos.Tests.Theatre;

public sealed class PumpkinCarvingTheatreTests
{
    private sealed record Stage(MapInstance Map, SuomiTheatreMapScript Theatre, PumpkinCarvingMapScript Carving, Aisling Director, Aisling Guest);

    private static Stage Create()
    {
        var map = MockMapInstance.Create(width: 32, height: 32);
        var director = MockAisling.Create(map, name: "Dana", position: new Point(4, 12));
        director.Trackers.Enums.Set(TheatreRoles.Director);
        map.AddEntity(director, new Point(4, 12));
        var guest = MockAisling.Create(map, name: "Gus", position: new Point(2, 13));
        map.AddEntity(guest, new Point(2, 13));

        var theatre = new SuomiTheatreMapScript(
            map,
            new Mock<IEffectFactory>().Object,
            new Mock<IStorage<TheatreLightingScenes>>().Object,
            new Mock<ISimpleCache>().Object,
            new Mock<ILogger<SuomiTheatreMapScript>>().Object,
            new Mock<IItemFactory>().Object);

        var storage = new Mock<IStorage<PumpkinDisplay>>();
        storage.Setup(s => s.Value).Returns(new PumpkinDisplay());

        var merchants = new Mock<IMerchantFactory>();

        merchants.Setup(f => f.Create(PumpkinCarving.PumpkinTemplateKey, It.IsAny<MapInstance>(), It.IsAny<IPoint>(), It.IsAny<ICollection<string>?>()))
                 .Returns((string _, MapInstance m, IPoint _, ICollection<string>? _) => MockMerchant.Create(m, name: PumpkinCarving.PumpkinTemplateKey));

        var carving = new PumpkinCarvingMapScript(
            map,
            merchants.Object,
            new Mock<IItemFactory>().Object,
            new Mock<IDialogFactory>().Object,
            storage.Object,
            theatre: () => theatre,
            isHalloween: () => true);

        UseAsMapScript(map, carving);

        return new Stage(map, theatre, carving, director, guest);
    }

    private static void UseAsMapScript(MapInstance map, IMapScript script)
    {
        var field = typeof(MapInstance).GetField("<Script>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(map, script);
    }

    [Test]
    public void Spotlight_chairs_will_not_start_while_carving_runs()
    {
        var stage = Create();
        stage.Carving.TryStart(stage.Director);

        stage.Theatre.TryStartSpotlight(stage.Director, halloweenActive: true, () => 6)
             .Should()
             .Be(SpotlightStartRefusal.StageBusy);

        stage.Theatre.StageGameRunning.Should().BeTrue();
        stage.Theatre.LightsBusyText.Should().Be(PumpkinCarving.LightsBusyText);
    }

    [Test]
    public void Carving_will_not_start_while_spotlight_chairs_runs()
    {
        var stage = Create();
        stage.Theatre.TryStartSpotlight(stage.Director, halloweenActive: true, () => 6);

        stage.Carving.TryStart(stage.Director).Should().Be(PumpkinCarvingRefusal.StageBusy);
        stage.Theatre.LightsBusyText.Should().Be(SpotlightChairs.LightsBusyText);
    }

    [Test]
    public void The_look_hook_answers_only_for_this_games_pumpkins()
    {
        var stage = Create();
        stage.Carving.TryStart(stage.Director);
        var other = MockMerchant.Create(stage.Map, name: "Thulin");
        var pumpkin = stage.Map.GetEntitiesAtPoints<Merchant>(PumpkinCarving.PumpkinTiles[0]).Single();

        PumpkinLooks.For(other).Should().BeNull();
        PumpkinLooks.For(pumpkin)!.State.Should().Be(PumpkinLookState.Blank);
    }

    [Test]
    public void Menu_options_follow_the_round()
    {
        PumpkinCarving.MenuOption(canDirect: false, halloween: true, running: false).Should().BeNull();
        PumpkinCarving.MenuOption(canDirect: true, halloween: false, running: false).Should().BeNull();
        PumpkinCarving.MenuOption(canDirect: true, halloween: false, running: false, admin: true).Should().Be(PumpkinCarving.StartOption);
        PumpkinCarving.MenuOption(canDirect: true, halloween: true, running: true).Should().Be(PumpkinCarving.CallOffOption);
        PumpkinCarving.EndNowMenuOption(true, PumpkinRoundState.Carving).Should().Be(PumpkinCarving.EndNowOption);
        PumpkinCarving.EndNowMenuOption(true, PumpkinRoundState.Voting).Should().BeNull();
        PumpkinCarving.EndNowMenuOption(false, PumpkinRoundState.Carving).Should().BeNull();
    }
}
```

If `IMapScript` lives in another namespace, use the type of `MapInstance.Script` (the compiler names it).

- [ ] **Step 2: Run them and see them fail** (compile errors: `StageBusy`, `StageGameRunning`, `LightsBusyText`, `PumpkinLooks`).

- [ ] **Step 3: Spotlight Chairs learns `StageBusy`.** In `S/Chaos/Services/Theatre/SpotlightChairs.cs`:
  - add `StageBusy` as the last member of `SpotlightStartRefusal`;
  - add to `RefusalText`: `SpotlightStartRefusal.StageBusy => "Pumpkin Carving is using the stage.",`
  - in `SpotlightChairsGame.TryStart`, add a last optional parameter `bool stageBusy = false`, and right after the `AlreadyRunning` check add:

```csharp
        if (stageBusy)
            return SpotlightStartRefusal.StageBusy;
```

- [ ] **Step 4: The theatre script knows about the other game.** In `S/Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs`:
  - add these properties after `SpotlightRunning`:

```csharp
    /// <summary>True while the haunted theatre's carving game runs on this map.</summary>
    public bool CarvingRunning => Subject.Script.Is<PumpkinCarvingMapScript>(out var carving) && carving.Running;

    /// <summary>A stage game owns the lights; the lighting window and the lights on/off options wait.</summary>
    public bool StageGameRunning => SpotlightRunning || CarvingRunning;

    public string LightsBusyText => CarvingRunning ? PumpkinCarving.LightsBusyText : SpotlightChairs.LightsBusyText;
```

  - in `HandleLightingEdit`, replace

```csharp
        if (Spotlight is { Running: true })
        {
            source.SendOrangeBarMessage(SpotlightChairs.LightsBusyText);
```

    with

```csharp
        if (StageGameRunning)
        {
            source.SendOrangeBarMessage(LightsBusyText);
```

  - in `TryStartSpotlight(Aisling starter, bool halloweenActive, Func<int> rollSeconds)`, pass `stageBusy: CarvingRunning` as the last argument of `game.TryStart(...)`.

- [ ] **Step 5: Thulin's lights options wait too.** In `S/Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs`, in `HandleLightsOff` and `HandleLightsOn`, replace

```csharp
            if (theatreScript.SpotlightRunning)
            {
                source.SendOrangeBarMessage(SpotlightChairs.LightsBusyText);
```

with

```csharp
            if (theatreScript.StageGameRunning)
            {
                source.SendOrangeBarMessage(theatreScript.LightsBusyText);
```

(Both occurrences.)

- [ ] **Step 6: Thulin's carving options.** In the same file, in the `OnDisplaying` block that adds the Spotlight Chairs option (it ends with `Subject.AddOption(spotlight, key);` and a closing brace), insert right after that `if (spotlight is not null) { ... }` block and before its `break;`:

```csharp
                if (source.MapInstance.Script.Is<PumpkinCarvingMapScript>(out var carving))
                {
                    var canDirect = SuomiTheatreMapScript.CanDirect(source);
                    var carvingOption = PumpkinCarving.MenuOption(canDirect, carving.Available, carving.Running, source.IsAdmin);

                    if (carvingOption is not null && !Subject.HasOption(carvingOption))
                        Subject.AddOption(carvingOption, carving.Running ? "suomitheatre_pumpkinstop" : "suomitheatre_pumpkinstart");

                    if (PumpkinCarving.EndNowMenuOption(canDirect, carving.State) is { } endNow && !Subject.HasOption(endNow))
                        Subject.AddOption(endNow, "suomitheatre_pumpkinendnow");
                }
```

In `OnNext`'s switch, after the `"suomitheatre_spotlightstop"` case, add:

```csharp
            case "suomitheatre_pumpkinstart":
                if ((optionIndex == 1) && source.MapInstance.Script.Is<PumpkinCarvingMapScript>(out var startCarving))
                    startCarving.TryStart(source);

                break;

            case "suomitheatre_pumpkinstop":
                if ((optionIndex == 1) && source.MapInstance.Script.Is<PumpkinCarvingMapScript>(out var stopCarving))
                    stopCarving.CallOff(source);

                break;

            case "suomitheatre_pumpkinendnow":
                if ((optionIndex == 1) && source.MapInstance.Script.Is<PumpkinCarvingMapScript>(out var endCarving))
                    endCarving.EndCarvingNow(source);

                break;
```

- [ ] **Step 7: The look hook.** Create `S/Chaos/Services/Theatre/PumpkinLooks.cs`:

```csharp
#region
using Chaos.Models.World;
using Chaos.Networking.Entities.Server;
using Chaos.Scripting.MapScripts.Temuair.Suomi;
#endregion

namespace Chaos.Services.Theatre;

/// <summary>Answers "how does this merchant look as a carving pumpkin?" for the world client's entity display.</summary>
public static class PumpkinLooks
{
    public static PumpkinLookArgs? For(Merchant merchant)
        => merchant.Template.TemplateKey.Equals(PumpkinCarving.PumpkinTemplateKey, StringComparison.OrdinalIgnoreCase)
           && merchant.MapInstance.Script.Is<PumpkinCarvingMapScript>(out var carving)
            ? carving.LookFor(merchant)
            : null;
}
```

In `S/Chaos/Networking/ChaosWorldClient.cs`, in `SendVisibleEntities`, replace the chunk loop's final `Send(args);` with:

```csharp
            Send(args);

            //a carving pumpkin's look follows its display, on approach, on refresh and when it spawns, so no client draws a
            //stale carving
            foreach (var merchant in chunk.OfType<Merchant>())
                if (PumpkinLooks.For(merchant) is { } look)
                    Send(look);
```

- [ ] **Step 8: The click script** — `S/Chaos/Scripting/MerchantScripts/Suomi/PumpkinCarvingScript.cs`:

```csharp
#region
using Chaos.Models.World;
using Chaos.Scripting.MapScripts.Temuair.Suomi;
using Chaos.Scripting.MerchantScripts.Abstractions;
#endregion

namespace Chaos.Scripting.MerchantScripts.Suomi;

/// <summary>A Pumpkin Carving pumpkin. Clicks go to the theatre's carving game, which reopens the window, votes or shows the director's menu.</summary>
public sealed class PumpkinCarvingScript : MerchantScriptBase
{
    public PumpkinCarvingScript(Merchant subject)
        : base(subject) { }

    public override void OnClicked(Aisling source)
    {
        if (Subject.MapInstance.Script.Is<PumpkinCarvingMapScript>(out var carving))
            carving.HandleClick(source, Subject);
    }
}
```

Script keys are class names without "Script": this is `pumpkincarving`. If a merchant script with the key `pumpkincarving` already exists, stop and report it.

- [ ] **Step 9: The director's pumpkin menu** — `S/Chaos/Scripting/DialogScripts/Temuair/Suomi/PumpkinCarvingDirectorScript.cs`:

```csharp
#region
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Scripting.MapScripts.Temuair.Suomi;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.Suomi;

/// <summary>The director's menu on a carving pumpkin: vote for it (only while it's up for the vote) or remove it.</summary>
public class PumpkinCarvingDirectorScript : DialogScriptBase
{
    public const string REMOVE = "Remove this Pumpkin";
    public const string VOTE = "Vote for this Pumpkin";

    public PumpkinCarvingDirectorScript(Dialog subject)
        : base(subject) { }

    public override void OnDisplaying(Aisling source)
    {
        if (!TryGetGame(source, out var carving, out var pumpkin) || !carving.CanVote(source, pumpkin))
            RemoveOption(VOTE);
    }

    public override void OnNext(Aisling source, byte? optionIndex = null)
    {
        if (optionIndex is not { } index || !TryGetGame(source, out var carving, out var pumpkin))
            return;

        var chosen = Subject.Options.ElementAtOrDefault(index - 1)?.OptionText;

        if (chosen == VOTE)
            carving.Vote(source, pumpkin);
        else if (chosen == REMOVE)
            carving.Remove(source, pumpkin);
    }

    private void RemoveOption(string text)
    {
        if (Subject.GetOptionIndex(text) is { } index)
            Subject.Options.RemoveAt(index);
    }

    private bool TryGetGame(Aisling source, out PumpkinCarvingMapScript carving, out Merchant pumpkin)
    {
        pumpkin = null!;
        carving = null!;

        if (Subject.DialogSource is not Merchant merchant || !source.MapInstance.Script.Is(out carving))
            return false;

        pumpkin = merchant;

        return true;
    }
}
```

`GetOptionIndex` and `Options` are what `SuomiTheatreScript.RemoveOption` uses. If `Is(out carving)` needs the type argument spelled out, write `Is<PumpkinCarvingMapScript>(out carving)`. If the dialog's source property is not called `DialogSource`, use the name `Dialog` exposes for the entity that opened it.

- [ ] **Step 10: The save handler.** In `S/Chaos/Services/Servers/WorldServer.cs`, after `OnFishingInteraction`:

```csharp
    /// <summary>A save from the carving window. The pumpkin is the one this player owns in the round on their own map.</summary>
    public ValueTask OnPumpkinCarvingSave(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<PumpkinCarvingSaveArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnPumpkinCarvingSave);

        static ValueTask InnerOnPumpkinCarvingSave(IChaosWorldClient localClient, PumpkinCarvingSaveArgs localArgs)
        {
            if (!localClient.Connected || localClient.Aisling is not { } aisling)
                return default;

            if (aisling.MapInstance.Script.Is<PumpkinCarvingMapScript>(out var carving))
                carving.HandleSave(aisling, localArgs);

            return default;
        }
    }
```

and register it beside the fishing one: `ClientHandlers[(byte)ClientOpCode.PumpkinCarvingSave] = OnPumpkinCarvingSave;`

- [ ] **Step 11: Run the tests and the build.**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/Chaos.Tests.Theatre/*/*"` → all pass (including the existing Spotlight Chairs tests).
Run: `dotnet build Chaos.slnx` → 0 errors.

```json:metadata
{"files": ["Chaos/Services/Theatre/PumpkinLooks.cs", "Chaos/Scripting/MerchantScripts/Suomi/PumpkinCarvingScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Suomi/PumpkinCarvingDirectorScript.cs", "Chaos/Networking/ChaosWorldClient.cs", "Chaos/Services/Servers/WorldServer.cs", "Chaos/Services/Theatre/SpotlightChairs.cs", "Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs", "Tests/Chaos.Tests/Theatre/PumpkinCarvingTheatreTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/Chaos.Tests.Theatre/*/*\"", "acceptanceCriteria": ["Spotlight and carving refuse each other", "lighting window and lights on/off refuse while either game runs", "PumpkinLooks.For null for other merchants; SendVisibleEntities sends looks", "save packet reaches HandleSave", "Thulin menu options and handlers", "director pumpkin menu Vote only when CanVote"], "modelTier": "standard"}
```

---

### Task 5: Content (Unora)

**Goal:** The pumpkin merchant template, the map script key on the haunted theatre, and the four dialogs, checked by a content test.

**Files:**
- Create: `U/Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/carving_pumpkin.json`
- Modify: `U/Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/instance.json`
- Create: `U/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_pumpkinstart.json`, `suomitheatre_pumpkinstop.json`, `suomitheatre_pumpkinendnow.json`, `pumpkincarving_director.json`
- Test: `U/Tools/PumpkinCarving/test_pumpkin_content.py`

**Acceptance Criteria:**
- [ ] Every pumpkin, claim and display tile is open floor with no reactor on `lod10269`; pumpkins and claims are on the stage, the display tile is not.
- [ ] The template uses sprite 1455 and script key `pumpkincarving`; the instance runs `pumpkincarvingmap`.
- [ ] The four dialogs exist with the right `templateKey`s and script keys.

**Verify:** `python -m pytest Tools/PumpkinCarving/test_pumpkin_content.py -q` (from `U`) → all pass

**Steps:**

- [ ] **Step 1: Write the failing test** — `U/Tools/PumpkinCarving/test_pumpkin_content.py`:

```python
"""Content checks for Pumpkin Carving. The tile lists must match PumpkinCarving.PumpkinTiles, ClaimTiles and
DisplayTile in Chaos-Server."""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "HauntedTheatre"))

import decorate_theatre as theatre  # noqa: E402

SPRITE_ID = 1455
PUMPKIN_TILES = [(6, y) for y in range(13, 21)]
CLAIM_TILES = [(5, y) for y in range(13, 21)]
DISPLAY_TILE = (11, 15)
TEMPLATE = theatre.CONFIG / "Templates" / "Merchants" / "Temauir" / "Events" / "Halloween" / "carving_pumpkin.json"
DIALOGS = theatre.CONFIG / "Templates" / "Dialogs" / "Temauir" / "suomi" / "thulin"


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def reactor_tiles():
    entries = read_json(theatre.INSTANCE_DIR / "reactors.json")
    return {tuple(map(int, re.findall(r"\d+", entry["source"]))) for entry in entries}


def open_floor(tiles, x, y):
    bg, lf, rf = tiles[y][x]
    return not theatre.is_wall(tiles[y][x]) and not theatre.blocks(lf) and not theatre.blocks(rf)


def test_every_pumpkin_claim_and_display_tile_is_open_floor_without_a_reactor():
    tiles = theatre.read_tiles(theatre.CONFIG / "MapData" / "lod10269.map")
    reactors = reactor_tiles()
    for x, y in PUMPKIN_TILES + CLAIM_TILES + [DISPLAY_TILE]:
        assert open_floor(tiles, x, y), (x, y)
        assert (x, y) not in reactors, (x, y)


def test_pumpkins_and_claims_are_on_the_stage_and_the_display_is_in_the_house():
    assert all(theatre.on_stage(x, y) for x, y in PUMPKIN_TILES + CLAIM_TILES)
    assert not theatre.on_stage(*DISPLAY_TILE)


def test_the_template_uses_the_pumpkin_sprite_and_the_click_script():
    template = read_json(TEMPLATE)
    assert template["templateKey"] == "carving_pumpkin"
    assert template["sprite"] == SPRITE_ID
    assert template["scriptKeys"] == ["pumpkincarving"]


def test_the_haunted_theatre_runs_the_carving_map_script():
    instance = read_json(theatre.INSTANCE_DIR / "instance.json")
    assert instance["scriptKeys"] == ["suomitheatremap", "mirrormap", "pumpkincarvingmap"]


def test_the_dialogs_exist_with_their_scripts():
    for key, script in [("suomitheatre_pumpkinstart", "suomitheatre"), ("suomitheatre_pumpkinstop", "suomitheatre"),
                        ("suomitheatre_pumpkinendnow", "suomitheatre"), ("pumpkincarving_director", "pumpkincarvingdirector")]:
        dialog = read_json(DIALOGS / f"{key}.json")
        assert dialog["templateKey"] == key
        assert dialog["scriptKeys"] == [script]
        assert all(len(option["optionText"]) <= 35 for option in dialog["options"])
        assert "—" not in json.dumps(dialog, ensure_ascii=False)
```

- [ ] **Step 2: Run it and see it fail** (missing template file).

Run: `python -m pytest Tools/PumpkinCarving/test_pumpkin_content.py -q`

- [ ] **Step 3: Add the template** — `carving_pumpkin.json`:

```json
{
  "itemsForSale": [],
  "itemsToBuy": [],
  "name": "Blank Pumpkin",
  "restockIntervalHrs": 24,
  "restockPct": 100,
  "scriptKeys": [
    "pumpkincarving"
  ],
  "scriptVars": {},
  "skillsToTeach": [],
  "spellsToTeach": [],
  "sprite": 1455,
  "templateKey": "carving_pumpkin",
  "wanderIntervalMs": 1500
}
```

- [ ] **Step 4: Add the map script key.** In `Suomi_Theatre_Halloween/instance.json`, set `"scriptKeys": ["suomitheatremap", "mirrormap", "pumpkincarvingmap"]` (keep the file's existing formatting: one key per line).

- [ ] **Step 5: Add the dialogs** in `U/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/`.

`suomitheatre_pumpkinstart.json`:

```json
{
  "options": [
    {
      "dialogKey": "Close",
      "optionText": "Yes"
    },
    {
      "dialogKey": "Close",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "suomitheatre"
  ],
  "scriptVars": {},
  "templateKey": "suomitheatre_pumpkinstart",
  "text": "Start Pumpkin Carving? Eight blank pumpkins will appear on the stage.",
  "type": "DialogMenu"
}
```

`suomitheatre_pumpkinstop.json`: the same shape, `"templateKey": "suomitheatre_pumpkinstop"`, `"text": "Call off Pumpkin Carving? Nobody gets a prize."`.

`suomitheatre_pumpkinendnow.json`: the same shape, `"templateKey": "suomitheatre_pumpkinendnow"`, `"text": "End the carving now and light the pumpkins?"`.

`pumpkincarving_director.json`:

```json
{
  "options": [
    {
      "dialogKey": "Close",
      "optionText": "Vote for this Pumpkin"
    },
    {
      "dialogKey": "Close",
      "optionText": "Remove this Pumpkin"
    },
    {
      "dialogKey": "Close",
      "optionText": "Never mind"
    }
  ],
  "scriptKeys": [
    "pumpkincarvingdirector"
  ],
  "scriptVars": {},
  "templateKey": "pumpkincarving_director",
  "text": "What should happen to this pumpkin?",
  "type": "DialogMenu"
}
```

- [ ] **Step 6: Run the test and see it pass.** Expected: 5 tests pass.

```json:metadata
{"files": ["Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/carving_pumpkin.json", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/instance.json", "Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_pumpkinstart.json", "Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_pumpkinstop.json", "Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_pumpkinendnow.json", "Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/pumpkincarving_director.json", "Tools/PumpkinCarving/test_pumpkin_content.py"], "verifyCommand": "python -m pytest Tools/PumpkinCarving/test_pumpkin_content.py -q", "acceptanceCriteria": ["tiles open floor, no reactors, stage/house placement", "template sprite 1455 + pumpkincarving; instance runs pumpkincarvingmap", "four dialogs with keys and scripts"], "modelTier": "mechanical"}
```

---

### Task 6: Pumpkin art and face map (Unora tool)

**Goal:** `make_pumpkin.py` draws the two sprite frames, the face map and a preview from one geometry, with tests.

**Files:**
- Create: `U/Tools/PumpkinCarving/make_pumpkin.py`
- Create (generated): `U/Tools/PumpkinCarving/art/pumpkin_frames.json`, `art/pumpkinfacemap.json`, `art/preview.png`
- Test: `U/Tools/PumpkinCarving/test_make_pumpkin.py`

**Acceptance Criteria:**
- [ ] Frame 0 (back) has no face pixels; frame 1 (front) shows every one of the 308 cells.
- [ ] The front face sits right of centre (it faces down-right).
- [ ] Frames are 44 × 44 palette indices; the palette has 256 entries; no visible index is pure black.
- [ ] Glow is brightest in the middle of the face.
- [ ] `--check` exits 0; a run without it writes the three files.

**Verify:** `python -m pytest Tools/PumpkinCarving/test_make_pumpkin.py -q` → all pass; `python Tools/PumpkinCarving/make_pumpkin.py --check` → exit 0

**Steps:**

- [ ] **Step 1: Write the failing test** — `U/Tools/PumpkinCarving/test_make_pumpkin.py`:

```python
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import make_pumpkin as mp  # noqa: E402


def frames():
    return mp.build()


def test_the_built_frames_pass_the_check():
    assert mp.check(frames()) == []


def test_the_back_frame_shows_no_face():
    assert frames()[0].face == []


def test_the_front_frame_shows_every_cell():
    cells = {cell for pixel in frames()[1].face for cell in pixel[3:]}
    assert cells == set(range(mp.GRID_W * mp.GRID_H))


def test_the_face_turns_toward_the_right():
    face = frames()[1].face
    assert sum(pixel[0] for pixel in face) / len(face) > mp.WIDTH / 2


def test_frames_are_full_size_palette_indices():
    for frame in frames():
        assert len(frame.pixels) == mp.WIDTH * mp.HEIGHT
        assert max(frame.pixels) < len(mp.PALETTE) <= 256
    assert len(mp.palette_256()) == 256


def test_glow_is_brightest_in_the_middle():
    middle = mp.glow_amount([7 * mp.GRID_W + 10, 7 * mp.GRID_W + 11])
    corner = mp.glow_amount([0])
    assert middle < corner
    assert mp.glow_rgb(0) == (255, 244, 150)


def test_the_json_outputs_describe_the_sprite():
    built = frames()
    sprite = mp.frames_json(built)
    face = mp.facemap_json(built)
    assert (sprite["spriteId"], sprite["paletteId"]) == (1455, 355)
    assert (sprite["centerX"], sprite["centerY"]) == mp.ANCHOR
    assert len(sprite["palette"]) == 256
    assert face["spriteId"] == 1455
    assert len(face["frames"]) == 2
```

- [ ] **Step 2: Run it and see it fail** (no module).

- [ ] **Step 3: Write the tool** — `U/Tools/PumpkinCarving/make_pumpkin.py`:

```python
"""Draw the Pumpkin Carving pumpkin and its face map. Run from the Unora repo root:

    python Tools/PumpkinCarving/make_pumpkin.py           # write art/ (frames, face map, preview)
    python Tools/PumpkinCarving/make_pumpkin.py --check   # build in memory and check; writes nothing

The pumpkin is a ray-cast ellipsoid seen 16 degrees from above: the stand-in approved in the design session (mockup
option B), with the face turned 22 degrees toward the viewer from the facing diagonal. Frame 0 faces up-right (its back),
frame 1 faces down-right (its face). The client mirrors them for the other two directions.

The face map lists, for each pixel of each frame, the carving cells it shows. Each pixel takes 4 x 4 samples and keeps
every cell they land in, so a one-cell cut always lights at least one pixel even though a cell is about one pixel at game
size. The client lights a pixel when any of its cells is cut. One geometry makes the sprite and the map, so they match.

Outputs in art/: pumpkin_frames.json (palette and indexed frames, read by pack_pumpkin.cs), pumpkinfacemap.json (copied
into the client as an embedded resource) and preview.png (for review).
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ART = HERE / "art"

SPRITE_ID = 1455
PALETTE_ID = 355
WIDTH, HEIGHT = 44, 44
BODY_CX, BODY_CY = 22, 24
ANCHOR = (22, 38)  # the frame's CenterX/CenterY: this pixel sits on the tile centre
RADIUS_X, RADIUS_Y = 18.0, 16.0
TILT = math.radians(16)
GRID_W, GRID_H = 22, 14  # PumpkinGrid in Chaos.DarkAges
FACE_TURN = 22
FACE_HALF = math.radians(52)
FACE_TOP, FACE_BOTTOM = math.radians(40), math.radians(-24)
SUPERSAMPLE = 4
FRAME_FACINGS = [180 - FACE_TURN, FACE_TURN]  # frame 0 back (up-right), frame 1 front (down-right)
LIGHT = (-0.5, 0.7, -0.5)

OUTLINE = (70, 30, 8)
STEM = (96, 116, 42)
STEM_DARK = (60, 72, 26)
SKIN_LEVELS = [0.5, 0.62, 0.74, 0.86, 0.98, 1.1]


def _skin(k: float) -> tuple[int, int, int]:
    return min(255, round(224 * k)), min(255, round(118 * k)), max(8, min(255, round(30 * k)))


# index 0 is transparent; 4.. are the skin shades, then the same shades darkened for the ribs
PALETTE = [(0, 0, 0), OUTLINE, STEM, STEM_DARK] + [_skin(k) for k in SKIN_LEVELS] + [_skin(k * 0.75) for k in SKIN_LEVELS]
SKIN_INDEX = 4
RIB_INDEX = SKIN_INDEX + len(SKIN_LEVELS)


@dataclass
class Frame:
    pixels: list[int]  # palette indices, row-major WIDTH x HEIGHT
    face: list[list[int]]  # [x, y, glow, cell, cell, ...]


def _norm(v):
    length = math.sqrt(sum(c * c for c in v))
    return tuple(c / length for c in v)


def _ray(sx: float, sy: float):
    """Where a view ray through screen offset (sx, sy) from the body's middle first meets the body, or None."""
    fy, fz = -math.sin(TILT), math.cos(TILT)
    uy, uz = math.cos(TILT), math.sin(TILT)
    ox, oy, oz = sx, sy * uy, sy * uz
    a2, b2 = RADIUS_X ** 2, RADIUS_Y ** 2
    qa = fy * fy / b2 + fz * fz / a2
    qb = 2 * (oy * fy / b2 + oz * fz / a2)
    qc = ox * ox / a2 + oy * oy / b2 + oz * oz / a2 - 1
    disc = qb * qb - 4 * qa * qc
    if disc < 0:
        return None
    t = (-qb - math.sqrt(disc)) / (2 * qa)
    return ox, oy + t * fy, oz + t * fz


def _surface(px: float, py: float):
    return _ray(px - BODY_CX, -(py - BODY_CY))


def _angles(point, facing: float):
    x, y, z = point
    theta = math.atan2(x, -z) - math.radians(facing)
    theta = (theta + math.pi) % (2 * math.pi) - math.pi
    phi = math.asin(max(-1.0, min(1.0, y / RADIUS_Y)))
    return theta, phi


def _cell(theta: float, phi: float):
    if abs(theta) > FACE_HALF or not FACE_BOTTOM <= phi <= FACE_TOP:
        return None
    u = (theta + FACE_HALF) / (2 * FACE_HALF)
    v = (FACE_TOP - phi) / (FACE_TOP - FACE_BOTTOM)
    return min(GRID_H - 1, int(v * GRID_H)) * GRID_W + min(GRID_W - 1, int(u * GRID_W))


def glow_amount(cells) -> int:
    """0 in the middle of the face, 100 at its corners: the candle is brightest in the middle."""
    u = sum(((c % GRID_W) + 0.5) / GRID_W for c in cells) / len(cells)
    v = sum(((c // GRID_W) + 0.5) / GRID_H for c in cells) / len(cells)
    return round(min(1.0, math.hypot(u - 0.5, v - 0.5) / math.sqrt(0.5)) * 100)


def glow_rgb(amount: int) -> tuple[int, int, int]:
    """Must match PumpkinPainter.GlowColor in the client (flicker phase 0)."""
    return 255, round(244 - 0.63 * amount), round(150 - amount)


def build_frame(facing: float) -> Frame:
    light = _norm(LIGHT)
    hits = [[_surface(x + 0.5, y + 0.5) for x in range(WIDTH)] for y in range(HEIGHT)]
    pixels = [0] * (WIDTH * HEIGHT)
    face = []
    for y in range(HEIGHT):
        for x in range(WIDTH):
            point = hits[y][x]
            if point is None:
                continue
            edge = any(not (0 <= x + dx < WIDTH and 0 <= y + dy < HEIGHT) or hits[y + dy][x + dx] is None
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if edge:
                pixels[y * WIDTH + x] = 1
                continue
            normal = _norm((point[0] / RADIUS_X ** 2, point[1] / RADIUS_Y ** 2, point[2] / RADIUS_X ** 2))
            lambert = max(0.0, sum(n * l for n, l in zip(normal, light)))
            brightness = 0.45 + 0.6 * lambert
            level = min(range(len(SKIN_LEVELS)), key=lambda i: abs(SKIN_LEVELS[i] - brightness))
            theta, _ = _angles(point, facing)
            rib = abs(math.cos(5 * theta)) ** 18 > 0.5
            pixels[y * WIDTH + x] = (RIB_INDEX if rib else SKIN_INDEX) + level
            cells = set()
            for j in range(SUPERSAMPLE):
                for i in range(SUPERSAMPLE):
                    sample = _surface(x + (i + 0.5) / SUPERSAMPLE, y + (j + 0.5) / SUPERSAMPLE)
                    if sample is None:
                        continue
                    cell = _cell(*_angles(sample, facing))
                    if cell is not None:
                        cells.add(cell)
            if cells:
                face.append([x, y, glow_amount(cells), *sorted(cells)])
    top = next(y for y in range(HEIGHT) if pixels[y * WIDTH + BODY_CX])
    stem = set()
    for y in range(max(0, top - 5), top + 2):
        for x in range(BODY_CX - 1, BODY_CX + 2):
            pixels[y * WIDTH + x] = 3 if x == BODY_CX - 1 else 2
            stem.add((x, y))
    face = [pixel for pixel in face if (pixel[0], pixel[1]) not in stem]
    return Frame(pixels, face)


def build() -> list[Frame]:
    return [build_frame(facing) for facing in FRAME_FACINGS]


def palette_256() -> list[tuple[int, int, int]]:
    return PALETTE + [(0, 0, 0)] * (256 - len(PALETTE))


def check(frames: list[Frame]) -> list[str]:
    problems = []
    if len(PALETTE) > 256:
        problems.append(f"{len(PALETTE)} colours; a palette holds 256")
    if any(colour == (0, 0, 0) for colour in PALETTE[1:]):
        problems.append("visible pure black reads as transparent in the client")
    front = {cell for pixel in frames[1].face for cell in pixel[3:]}
    if len(front) != GRID_W * GRID_H:
        problems.append(f"the front frame shows {len(front)} of {GRID_W * GRID_H} cells")
    for number, frame in enumerate(frames):
        for x, y, *_ in frame.face:
            if frame.pixels[y * WIDTH + x] < SKIN_INDEX:
                problems.append(f"frame {number}: face pixel ({x}, {y}) is not skin")
    ax, ay = ANCHOR
    if not (0 <= ax < WIDTH and 0 <= ay < HEIGHT):
        problems.append("the anchor is outside the frame")
    return problems


def frames_json(frames: list[Frame]) -> dict:
    return {"spriteId": SPRITE_ID, "paletteId": PALETTE_ID, "width": WIDTH, "height": HEIGHT,
            "centerX": ANCHOR[0], "centerY": ANCHOR[1], "palette": [list(c) for c in palette_256()],
            "frames": [frame.pixels for frame in frames]}


def facemap_json(frames: list[Frame]) -> dict:
    return {"spriteId": SPRITE_ID, "gridWidth": GRID_W, "gridHeight": GRID_H, "width": WIDTH, "height": HEIGHT,
            "frames": [frame.face for frame in frames]}


def sample_face() -> set[int]:
    """The classic jack-o'-lantern from the mockups: triangle eyes, a nose and a toothy grin, mirrored."""
    cells = set()

    def cut(x, y):
        cells.add(y * GRID_W + x)
        cells.add(y * GRID_W + GRID_W - 1 - x)

    for r in range(4):
        for c in range(5 - r, 6 + r):
            cut(c, 1 + r)
    for x, y in [(10, 6), (9, 7), (10, 7), (1, 8), (2, 8)]:
        cut(x, y)
    for y, x0 in [(9, 2), (10, 3), (11, 5), (12, 7)]:
        for x in range(x0, 11):
            cut(x, y)
    for x, y in [(6, 9), (9, 9), (8, 12), (8, 11)]:
        cells.discard(y * GRID_W + x)
        cells.discard(y * GRID_W + GRID_W - 1 - x)
    return cells


def render(frame: Frame, cut: set[int]) -> Image.Image:
    image = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    colours = palette_256()
    for i, index in enumerate(frame.pixels):
        if index:
            image.putpixel((i % WIDTH, i // WIDTH), (*colours[index], 255))
    for x, y, glow, *cells in frame.face:
        if any(cell in cut for cell in cells):
            image.putpixel((x, y), (*glow_rgb(glow), 255))
    return image


def preview(frames: list[Frame]) -> Image.Image:
    scale, gap = 4, 16
    face = sample_face()
    shots = [render(frames[1], set()), render(frames[1], face), render(frames[1], face).transpose(Image.FLIP_LEFT_RIGHT),
             render(frames[0], face)]
    sheet = Image.new("RGBA", (gap + len(shots) * (WIDTH * scale + gap), HEIGHT * scale + 2 * gap), (20, 16, 24, 255))
    for n, shot in enumerate(shots):
        sheet.alpha_composite(shot.resize((WIDTH * scale, HEIGHT * scale), Image.NEAREST), (gap + n * (WIDTH * scale + gap), gap))
    return sheet


def write(frames: list[Frame]) -> None:
    ART.mkdir(exist_ok=True)
    (ART / "pumpkin_frames.json").write_text(json.dumps(frames_json(frames)), encoding="utf-8")
    (ART / "pumpkinfacemap.json").write_text(json.dumps(facemap_json(frames)), encoding="utf-8")
    preview(frames).save(ART / "preview.png")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--check", action="store_true", help="check only; write nothing")
    args = parser.parse_args()
    frames = build()
    problems = check(frames)
    for problem in problems:
        print(problem, file=sys.stderr)
    if problems:
        return 1
    if not args.check:
        write(frames)
        print(f"wrote {ART / 'pumpkin_frames.json'}, {ART / 'pumpkinfacemap.json'}, {ART / 'preview.png'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 4: Run the tests and see them pass.** Expected: 7 tests pass. If `test_the_front_frame_shows_every_cell` fails, report the missing cells; do not lower the bar (planning measured full coverage with these exact numbers).

- [ ] **Step 5: Write the art** (from `U`): `python Tools/PumpkinCarving/make_pumpkin.py`. Expected: three files in `Tools/PumpkinCarving/art/`. Look at `art/preview.png` with the Read tool: four pumpkins (blank, carved, carved mirrored, back) on a dark background. Report anything that looks wrong (face cut off, stem missing, outline gaps).

```json:metadata
{"files": ["Tools/PumpkinCarving/make_pumpkin.py", "Tools/PumpkinCarving/test_make_pumpkin.py", "Tools/PumpkinCarving/art/pumpkin_frames.json", "Tools/PumpkinCarving/art/pumpkinfacemap.json", "Tools/PumpkinCarving/art/preview.png"], "verifyCommand": "python -m pytest Tools/PumpkinCarving/test_make_pumpkin.py -q", "acceptanceCriteria": ["back frame no face; front frame all 308 cells", "front face right of centre", "44x44 indices, 256-entry palette, no visible black", "glow brightest in the middle", "--check exits 0; a run writes the three files"], "modelTier": "standard"}
```

---

### Task 7: Pack the sprite into hades.dat (Unora tool)

**Goal:** `pack_pumpkin.cs` writes `mns1455.mpf` and `mns355.pal` into a copy of the local `hades.dat`, re-reads it the way the client does, and leaves a release folder on the Desktop.

**Files:**
- Create: `U/Tools/PumpkinCarving/pack_pumpkin.cs`

**Acceptance Criteria:**
- [ ] The tool refuses if either name is already in `hades.dat`.
- [ ] The copy re-reads with 2 frames, palette 355, and renders `check-front.png`.
- [ ] The Desktop folder holds `hades.dat`, `check-front.png` and `manifest.txt`; the live client folder is not touched.

**Verify:** `dotnet run Tools/PumpkinCarving/pack_pumpkin.cs` (from `U`) → exit 0 and "wrote …\hades.dat"; then view `check-front.png`

**Steps:**

- [ ] **Step 1: Write the tool** — `U/Tools/PumpkinCarving/pack_pumpkin.cs`:

```csharp
#:project C:/Users/Michael/Documents/GitHub/dalib/DALib/DALib.csproj
// Packs the Pumpkin Carving pumpkin into a copy of hades.dat. Run from the Unora repo root after make_pumpkin.py:
//   dotnet run Tools/PumpkinCarving/pack_pumpkin.cs -- [out folder]
// Reads Tools/PumpkinCarving/art/pumpkin_frames.json and the local client folder's hades.dat. Refuses if the sprite or
// palette number is taken. Writes the sprite and palette into a copy, re-reads the copy the way the client does, and
// leaves the copy, check-front.png and manifest.txt in the out folder (default: Desktop\Pumpkin Carving <date>).
using System.Text.Json;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using SkiaSharp;

var framesPath = Path.Combine("Tools", "PumpkinCarving", "art", "pumpkin_frames.json");
var clientFolder = @"C:\Users\Michael\Documents\Unora\Unora Files";
var outDir = args.Length > 0
    ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Pumpkin Carving {DateTime.Now:yyyy-MM-dd}");

using var doc = JsonDocument.Parse(File.ReadAllText(framesPath));
var root = doc.RootElement;
var spriteId = root.GetProperty("spriteId").GetInt32();
var paletteId = root.GetProperty("paletteId").GetInt32();
var width = (short)root.GetProperty("width").GetInt32();
var height = (short)root.GetProperty("height").GetInt32();
var centerX = (short)root.GetProperty("centerX").GetInt32();
var centerY = (short)root.GetProperty("centerY").GetInt32();
var mpfName = $"mns{spriteId:D3}.mpf";
var palName = $"mns{paletteId:D3}.pal";

Directory.CreateDirectory(outDir);
var copyPath = Path.Combine(outDir, "hades.dat");
var newPath = copyPath + ".new";
File.Copy(Path.Combine(clientFolder, "hades.dat"), copyPath, overwrite: true);

using (var archive = DataArchive.FromFile(copyPath, memoryMapped: false))
{
    if (archive.TryGetValue(mpfName, out _) || archive.TryGetValue(palName, out _))
    {
        Console.Error.WriteLine($"{mpfName} or {palName} is already in hades.dat. Pick free numbers in make_pumpkin.py, the template and the client's PumpkinFaceMap.");

        return 1;
    }

    var palette = new Palette(
        root.GetProperty("palette")
            .EnumerateArray()
            .Select(c => new SKColor((byte)c[0].GetInt32(), (byte)c[1].GetInt32(), (byte)c[2].GetInt32())));

    //StaticNoIdle: frame 0 is the back (Up, and Left mirrored), frame 1 the front (Right, and Down mirrored)
    var mpf = new MpfFile(MpfHeaderType.None, MpfFormatType.SingleAttack, width, height)
    {
        PaletteNumber = paletteId,
        WalkFrameIndex = 0,
        WalkFrameCount = 1,
        AttackFrameIndex = 0,
        AttackFrameCount = 1,
        StandingFrameIndex = 0,
        StandingFrameCount = 0,
        OptionalAnimationFrameCount = 0
    };

    foreach (var frame in root.GetProperty("frames").EnumerateArray())
        mpf.Add(
            new MpfFrame
            {
                Left = 0,
                Top = 0,
                Right = width,
                Bottom = height,
                CenterX = centerX,
                CenterY = centerY,
                Data = frame.EnumerateArray().Select(v => (byte)v.GetInt32()).ToArray()
            });

    archive.Patch(mpfName, mpf);
    archive.Patch(palName, palette);
    archive.Save(newPath);
}

File.Move(newPath, copyPath, overwrite: true);

using (var check = DataArchive.FromFile(copyPath))
{
    var mpf = MpfFile.FromArchive(mpfName, check);
    var palettes = Palette.FromArchive("mns", check);

    if ((mpf.Count != 2) || (mpf.PaletteNumber != paletteId) || !palettes.ContainsKey(paletteId))
    {
        Console.Error.WriteLine($"the copy did not read back: {mpf.Count} frames, palette {mpf.PaletteNumber}");

        return 1;
    }

    using var image = Graphics.RenderImage(mpf[1], palettes[paletteId]);
    using var png = image.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(Path.Combine(outDir, "check-front.png"), png.ToArray());
}

File.WriteAllText(
    Path.Combine(outDir, "manifest.txt"),
    $"""
     hades.dat with {mpfName} (the Pumpkin Carving pumpkin) and {palName}.
     Ship it in the same launcher patch as the client with CLIENT_VERSION 767.
     For a local test, back up the client folder's hades.dat first, then copy this one over it.
     """);

Console.WriteLine($"wrote {copyPath}");

return 0;
```

If an API name differs in this DALib (`TryGetValue`, `Patch`, `Save`, `FromArchive`, `Graphics.RenderImage`), use the name `Tools/MirrorMaze/render_map.cs` or the client's `CreatureSpriteRepository` uses, and report it.

- [ ] **Step 2: Run it** (from `U`): `dotnet run Tools/PumpkinCarving/pack_pumpkin.cs`. Expected: exit 0, "wrote …\hades.dat".

- [ ] **Step 3: Look at `check-front.png`** with the Read tool: an orange pumpkin with a stem, no face (faces are drawn by the client). Report its size and anything odd.

```json:metadata
{"files": ["Tools/PumpkinCarving/pack_pumpkin.cs"], "verifyCommand": "dotnet run Tools/PumpkinCarving/pack_pumpkin.cs", "acceptanceCriteria": ["refuses taken numbers", "copy re-reads with 2 frames and palette 355; check-front.png written", "Desktop folder holds hades.dat, check-front.png, manifest.txt; live client folder untouched"], "modelTier": "standard"}
```

---

### Task 8: Client messages

**Goal:** `ConnectionManager` raises events for the two server messages and sends the save.

**Files:**
- Modify: `C/Chaos.Client.Networking/ConnectionManager.cs`, `C/Chaos.Client.Networking/Definitions/Delegates.cs`

**Acceptance Criteria:**
- [ ] `OnPumpkinCarvingDisplay` and `OnPumpkinLook` fire from opcodes 149 and 150.
- [ ] `SendPumpkinCarvingSave(byte[] grid, bool done)` sends opcode 142.
- [ ] The client solution builds against the server worktree.

**Verify:** `dotnet build Chaos.Client.slnx -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server` → 0 errors

**Steps:**

- [ ] **Step 1: Add the delegates** at the end of `Delegates.cs`, beside `FishingDisplayHandler`:

```csharp
/// <summary>
///     Fired when a Pumpkin Carving window display packet is received.
/// </summary>
public delegate void PumpkinCarvingDisplayHandler(PumpkinCarvingDisplayArgs args);

/// <summary>
///     Fired when a Pumpkin Carving pumpkin look is received.
/// </summary>
public delegate void PumpkinLookHandler(PumpkinLookArgs args);
```

- [ ] **Step 2: Add the events** after `OnFishingDisplay` in `ConnectionManager.cs`:

```csharp
    /// <summary>
    ///     Fired when the server opens or closes the Pumpkin Carving window.
    /// </summary>
    public event PumpkinCarvingDisplayHandler? OnPumpkinCarvingDisplay;

    /// <summary>
    ///     Fired when the server says how a Pumpkin Carving pumpkin looks.
    /// </summary>
    public event PumpkinLookHandler? OnPumpkinLook;
```

- [ ] **Step 3: Add the send** after `SendFishingClose`:

```csharp
    /// <summary>Sends the carving grid. <paramref name="done" /> is true when the window is closing.</summary>
    public void SendPumpkinCarvingSave(byte[] grid, bool done)
        => SendIfWorld(
            new PumpkinCarvingSaveArgs
            {
                Grid = grid,
                Done = done
            });
```

- [ ] **Step 4: Register and handle.** In `IndexHandlers`, after the `FishingDisplay` line:

```csharp
        PacketHandlers[(byte)ServerOpCode.PumpkinCarvingDisplay] = HandlePumpkinCarvingDisplay;
        PacketHandlers[(byte)ServerOpCode.PumpkinLook] = HandlePumpkinLook;
```

After `HandleFishingDisplay`:

```csharp
    private void HandlePumpkinCarvingDisplay(ServerPacket pkt)
    {
        var args = Client.Deserialize<PumpkinCarvingDisplayArgs>(in pkt);
        OnPumpkinCarvingDisplay?.Invoke(args);
    }

    private void HandlePumpkinLook(ServerPacket pkt)
    {
        var args = Client.Deserialize<PumpkinLookArgs>(in pkt);
        OnPumpkinLook?.Invoke(args);
    }
```

- [ ] **Step 5: Check the client version source.** Open `C/Chaos.Client/GlobalSettings.cs`. If `ClientVersion` is a literal number rather than `CONSTANTS.CLIENT_VERSION`, set it to 767 (or the value Task 1 used).

- [ ] **Step 6: Build.** Expected: 0 errors.

```json:metadata
{"files": ["Chaos.Client.Networking/ConnectionManager.cs", "Chaos.Client.Networking/Definitions/Delegates.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\pumpkin-carving-server", "acceptanceCriteria": ["events fire for 149 and 150", "SendPumpkinCarvingSave sends 142", "client builds against the server worktree"], "modelTier": "mechanical"}
```

---

### Task 9: Face map and painter (client rendering)

**Goal:** The embedded face map, its loader, and the pure painter that recolours carved pixels.

**Files:**
- Create: `C/Chaos.Client.Rendering/Assets/Pumpkin/pumpkinfacemap.json` (copy of `U/Tools/PumpkinCarving/art/pumpkinfacemap.json`)
- Modify: `C/Chaos.Client.Rendering/Chaos.Client.Rendering.csproj`
- Create: `C/Chaos.Client.Rendering/PumpkinFaceMap.cs`, `C/Chaos.Client.Rendering/PumpkinPainter.cs`, `C/Chaos.Client.Rendering/PumpkinLook.cs`
- Test: `C/Tests/Chaos.Client.Tests/PumpkinPainterTests.cs`

**Acceptance Criteria:**
- [ ] The embedded map loads: 2 frames, frame 0 empty, frame 1 covering all 308 cells.
- [ ] A pixel glows only when one of its cells is cut; out-of-frame pixels are skipped; frame offsets (Left/Top) are applied.
- [ ] `GlowColor(0, 0)` is (255, 244, 150, 255), matching the Python preview.

**Verify:** client test command with `--treenode-filter "/*/*/PumpkinPainterTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Copy the face map** (PowerShell):

```powershell
New-Item -ItemType Directory -Force C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-client\Chaos.Client.Rendering\Assets\Pumpkin
Copy-Item C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-unora\Tools\PumpkinCarving\art\pumpkinfacemap.json C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-client\Chaos.Client.Rendering\Assets\Pumpkin\
```

- [ ] **Step 2: Embed it.** In `Chaos.Client.Rendering.csproj`, inside the `ItemGroup` that embeds the mirror scare PNGs, add:

```xml
        <!-- Pumpkin Carving face map, written by Unora Tools/PumpkinCarving/make_pumpkin.py with the sprite it matches.
             Read back by PumpkinFaceMap as "pumpkin.facemap.json". -->
        <EmbeddedResource Include="Assets\Pumpkin\pumpkinfacemap.json">
            <LogicalName>pumpkin.facemap.json</LogicalName>
        </EmbeddedResource>
```

- [ ] **Step 3: Write the failing tests** — `C/Tests/Chaos.Client.Tests/PumpkinPainterTests.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class PumpkinPainterTests
{
    private static readonly Color Skin = new(224, 118, 30, 255);

    [Test]
    public void The_embedded_map_has_a_blank_back_and_a_full_front()
    {
        var map = PumpkinFaceMap.Shared;

        map.FrameCount.Should().Be(2);
        map.For(0).Should().BeEmpty();
        map.For(1).SelectMany(pixel => pixel.Cells).Distinct().Should().HaveCount(PumpkinGrid.CELLS);
        map.For(7).Should().BeEmpty();
    }

    [Test]
    public void A_pixel_glows_only_when_one_of_its_cells_is_cut()
    {
        var map = new[] { new PumpkinFacePixel(1, 0, 0, [0, 1]), new PumpkinFacePixel(2, 0, 0, [5]) };
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 1, 0, true);
        var pixels = Enumerable.Repeat(Skin, 4).ToArray();

        PumpkinPainter.Paint(pixels, 4, 1, 0, 0, map, grid, 0);

        pixels[1].Should().Be(PumpkinPainter.GlowColor(0, 0));
        pixels[2].Should().Be(Skin);
    }

    [Test]
    public void The_frame_offset_is_applied_and_outside_pixels_are_skipped()
    {
        var map = new[] { new PumpkinFacePixel(3, 2, 0, [0]), new PumpkinFacePixel(9, 9, 0, [0]) };
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 0, 0, true);
        var pixels = Enumerable.Repeat(Skin, 4).ToArray();

        PumpkinPainter.Paint(pixels, 2, 2, 2, 1, map, grid, 0);

        pixels[3].Should().Be(PumpkinPainter.GlowColor(0, 0));
        pixels.Count(p => p == Skin).Should().Be(3);
    }

    [Test]
    public void The_glow_matches_the_art_tool_and_dims_toward_the_edge()
    {
        PumpkinPainter.GlowColor(0, 0).Should().Be(new Color(255, 244, 150, 255));
        PumpkinPainter.GlowColor(100, 0).G.Should().BeLessThan(PumpkinPainter.GlowColor(0, 0).G);
        PumpkinPainter.PhaseAt(0).Should().Be(0);
        PumpkinPainter.PhaseAt(PumpkinPainter.FLICKER_MS).Should().Be(1);
        PumpkinPainter.PhaseAt(PumpkinPainter.FLICKER_MS * PumpkinPainter.FLICKER_PHASES).Should().Be(0);
    }
}
```

- [ ] **Step 4: Run them and see them fail** (compile errors).

- [ ] **Step 5: Write the types.**

`C/Chaos.Client.Rendering/PumpkinLook.cs`:

```csharp
namespace Chaos.Client.Rendering;

/// <summary>
///     How one Pumpkin Carving pumpkin looks. <see cref="Version" /> changes with every new look, so painted frames are
///     cached per look.
/// </summary>
public sealed record PumpkinLook(bool Lit, byte[] Grid, int Version);
```

`C/Chaos.Client.Rendering/PumpkinFaceMap.cs`:

```csharp
#region
using System.Text.Json;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>One sprite pixel on a pumpkin's face: where it is in the frame, how far from the face's middle (0-100), and the cells it shows.</summary>
public sealed record PumpkinFacePixel(int X, int Y, int Glow, int[] Cells);

/// <summary>
///     The Pumpkin Carving face map (spec: docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md, 5.2), embedded as
///     "pumpkin.facemap.json". Written by Unora's Tools/PumpkinCarving/make_pumpkin.py from the geometry that drew sprite
///     <see cref="SPRITE_ID" />, so the two always match. Coordinates are in the full frame (0,0 at its top left).
/// </summary>
public sealed class PumpkinFaceMap
{
    public const int FRONT_FRAME = 1;
    public const int SPRITE_ID = 1455;

    private const string RESOURCE = "pumpkin.facemap.json";
    private static PumpkinFaceMap? SharedMap;

    private readonly PumpkinFacePixel[][] Frames;

    private PumpkinFaceMap(PumpkinFacePixel[][] frames) => Frames = frames;

    public int FrameCount => Frames.Length;

    public static PumpkinFaceMap Shared => SharedMap ??= Load();

    public IReadOnlyList<PumpkinFacePixel> For(int frameIndex)
        => (frameIndex >= 0) && (frameIndex < Frames.Length) ? Frames[frameIndex] : [];

    public static PumpkinFaceMap Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var frames = doc.RootElement
                        .GetProperty("frames")
                        .EnumerateArray()
                        .Select(frame => frame.EnumerateArray()
                                              .Select(ToPixel)
                                              .ToArray())
                        .ToArray();

        return new PumpkinFaceMap(frames);
    }

    private static PumpkinFaceMap Load()
    {
        using var stream = typeof(PumpkinFaceMap).Assembly.GetManifestResourceStream(RESOURCE)
                           ?? throw new InvalidOperationException($"missing embedded {RESOURCE}");
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    private static PumpkinFacePixel ToPixel(JsonElement entry)
    {
        var values = entry.EnumerateArray()
                          .Select(value => value.GetInt32())
                          .ToArray();

        return new PumpkinFacePixel(values[0], values[1], values[2], values[3..]);
    }
}
```

`C/Chaos.Client.Rendering/PumpkinPainter.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Lights a pumpkin's carving: each face-map pixel with any cut cell becomes candle glow, brightest in the middle of the
///     face, with a slight flicker. Works on a plain pixel buffer, so it is tested without a graphics device.
/// </summary>
public static class PumpkinPainter
{
    public const int FLICKER_MS = 150;
    public const int FLICKER_PHASES = 4;

    private static readonly float[] Flicker = [1f, 0.94f, 1f, 0.97f];

    /// <summary>Must match glow_rgb in make_pumpkin.py at phase 0.</summary>
    public static Color GlowColor(int glow, int phase)
    {
        var flicker = Flicker[phase % FLICKER_PHASES];

        return new Color(
            255,
            (int)Math.Clamp(MathF.Round((244 - (0.63f * glow)) * flicker), 0, 255),
            (int)Math.Clamp(MathF.Round((150 - glow) * flicker), 0, 255),
            255);
    }

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
    {
        foreach (var pixel in map)
        {
            var x = pixel.X - left;
            var y = pixel.Y - top;

            if (((uint)x >= (uint)width) || ((uint)y >= (uint)height) || !AnyCut(pixel.Cells, grid))
                continue;

            pixels[(y * width) + x] = GlowColor(pixel.Glow, phase);
        }
    }

    public static int PhaseAt(long tickMs) => (int)(tickMs / FLICKER_MS % FLICKER_PHASES);

    private static bool AnyCut(int[] cells, ReadOnlySpan<byte> grid)
    {
        foreach (var cell in cells)
            if (PumpkinGrid.IsCut(grid, cell % PumpkinGrid.WIDTH, cell / PumpkinGrid.WIDTH))
                return true;

        return false;
    }
}
```

- [ ] **Step 6: Run the tests and see them pass.** Expected: 4 tests pass.

```json:metadata
{"files": ["Chaos.Client.Rendering/Assets/Pumpkin/pumpkinfacemap.json", "Chaos.Client.Rendering/Chaos.Client.Rendering.csproj", "Chaos.Client.Rendering/PumpkinFaceMap.cs", "Chaos.Client.Rendering/PumpkinPainter.cs", "Chaos.Client.Rendering/PumpkinLook.cs", "Tests/Chaos.Client.Tests/PumpkinPainterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\pumpkin-carving-server -- --no-ansi --treenode-filter \"/*/*/PumpkinPainterTests/*\"", "acceptanceCriteria": ["embedded map: 2 frames, back empty, front all 308 cells", "glow only when a cell is cut; offsets applied; outside skipped", "GlowColor(0,0) = (255,244,150,255)"], "modelTier": "mechanical"}
```

---

### Task 10: Looks, the pumpkin's light and the drawing hook (client)

**Goal:** Looks are stored by entity id, a lit pumpkin carries a small light, and `DrawCreature` draws lit pumpkins with their carving.

**Files:**
- Create: `C/Chaos.Client/Collections/PumpkinLookStore.cs`
- Modify: `C/Chaos.Client/Collections/WorldState.cs`
- Modify: `C/Chaos.Client.Rendering/CreatureRenderer.cs`
- Modify: `C/Chaos.Client/Screens/WorldScreen.Draw.cs`
- Test: `C/Tests/Chaos.Client.Tests/PumpkinLookStoreTests.cs`

**Acceptance Criteria:**
- [ ] A look can arrive before or after its entity; each new look has a higher version; a blank look stores an empty grid; removal and clear drop looks.
- [ ] `LanternFor` is Small for a lit look and None otherwise; `WorldState` applies it on look arrival and when a creature is (re)displayed.
- [ ] `CreatureRenderer.Draw` takes an optional look and flicker phase; lit pumpkins of sprite 1455 draw painted frames from a cache cleared in `Clear()`.

**Verify:** client test command with `--treenode-filter "/*/*/PumpkinLookStoreTests/*"` → all pass; client build → 0 errors

**Steps:**

- [ ] **Step 1: Write the failing tests** — `C/Tests/Chaos.Client.Tests/PumpkinLookStoreTests.cs`:

```csharp
using Chaos.Client.Collections;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class PumpkinLookStoreTests
{
    [Test]
    public void Each_look_is_kept_by_id_with_a_new_version()
    {
        var store = new PumpkinLookStore();
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 2, 3, true);

        var blank = store.Apply(7, lit: false, grid: null);
        var lit = store.Apply(7, lit: true, grid: grid);

        store.Get(7).Should().Be(lit);
        lit.Version.Should().BeGreaterThan(blank.Version);
        PumpkinGrid.CountCut(blank.Grid).Should().Be(0);
        PumpkinGrid.IsCut(lit.Grid, 2, 3).Should().BeTrue();
        store.Get(8).Should().BeNull();
    }

    [Test]
    public void The_stored_grid_is_a_copy()
    {
        var store = new PumpkinLookStore();
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 0, 0, true);

        store.Apply(7, lit: true, grid: grid);
        grid[0] = 0;

        PumpkinGrid.IsCut(store.Get(7)!.Grid, 0, 0).Should().BeTrue();
    }

    [Test]
    public void Removal_and_clear_drop_looks()
    {
        var store = new PumpkinLookStore();
        store.Apply(7, lit: true, grid: PumpkinGrid.Empty());
        store.Apply(8, lit: true, grid: PumpkinGrid.Empty());

        store.Remove(7);

        store.Get(7).Should().BeNull();

        store.Clear();

        store.Get(8).Should().BeNull();
    }

    [Test]
    public void Only_a_lit_pumpkin_carries_a_light()
    {
        var store = new PumpkinLookStore();

        PumpkinLookStore.LanternFor(store.Apply(7, lit: true, grid: PumpkinGrid.Empty())).Should().Be(LanternSize.Small);
        PumpkinLookStore.LanternFor(store.Apply(8, lit: false, grid: null)).Should().Be(LanternSize.None);
        PumpkinLookStore.LanternFor(null).Should().Be(LanternSize.None);
    }
}
```

If `LanternSize` is not in `Chaos.DarkAges.Definitions`, add the namespace `WorldEntity.LanternSize` uses.

- [ ] **Step 2: Run them and see them fail.**

- [ ] **Step 3: Write the store** — `C/Chaos.Client/Collections/PumpkinLookStore.cs`:

```csharp
#region
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Collections;

/// <summary>
///     Pumpkin Carving looks by entity id (spec 2026-10-03-pumpkin-carving-design.md, 5.2). A look may arrive before or
///     after its pumpkin; the server resends it whenever it displays the pumpkin.
/// </summary>
public sealed class PumpkinLookStore
{
    private readonly Dictionary<uint, PumpkinLook> Looks = [];
    private int Version;

    public PumpkinLook Apply(uint entityId, bool lit, byte[]? grid)
    {
        var look = new PumpkinLook(lit, lit && grid is not null ? grid.ToArray() : PumpkinGrid.Empty(), ++Version);
        Looks[entityId] = look;

        return look;
    }

    public void Clear() => Looks.Clear();

    public PumpkinLook? Get(uint entityId) => Looks.GetValueOrDefault(entityId);

    /// <summary>A lit pumpkin carries a small light, so it shows when the house lights are off.</summary>
    public static LanternSize LanternFor(PumpkinLook? look) => look is { Lit: true } ? LanternSize.Small : LanternSize.None;

    public void Remove(uint entityId) => Looks.Remove(entityId);
}
```

- [ ] **Step 4: Wire it into `WorldState`** (`C/Chaos.Client/Collections/WorldState.cs`):
  - after the `GuildCloakLooks` property add:

```csharp
    /// <summary>Pumpkin Carving looks by entity id; see <see cref="PumpkinLookStore" />.</summary>
    public static PumpkinLookStore PumpkinLooks { get; } = new();
```

  - in `Clear()`, after `GuildCloakLooks.Clear();` add `PumpkinLooks.Clear();`
  - in `RemoveEntity`, after `GuildCloakLooks.Remove(id);` add `PumpkinLooks.Remove(id);`
  - in `AddOrUpdateVisibleEntities`, in the `case CreatureInfo creature:` block, after `entity.Name = creature.Name ?? string.Empty;` add:

```csharp
                    entity.LanternSize = PumpkinLookStore.LanternFor(PumpkinLooks.Get(obj.Id));
```

  - after `ApplyGuildCloakLook` add:

```csharp
    /// <summary>Records a pumpkin's look and gives a lit pumpkin its light.</summary>
    public static void ApplyPumpkinLook(uint entityId, bool lit, byte[]? grid)
    {
        var look = PumpkinLooks.Apply(entityId, lit, grid);

        if (Entities.TryGetValue(entityId, out var entity))
            entity.LanternSize = PumpkinLookStore.LanternFor(look);
    }
```

- [ ] **Step 5: Painted frames in `CreatureRenderer`** (`C/Chaos.Client.Rendering/CreatureRenderer.cs`):
  - add a field beside `FrameCache`:

```csharp
    //painted Pumpkin Carving frames, per (look version, frame, flicker phase); never shares textures with FrameCache
    private readonly Dictionary<(int Version, int FrameIndex, int Phase), SpriteFrame> PumpkinCache = [];
```

  - at the end of `Clear()` add:

```csharp
        foreach (var painted in PumpkinCache.Values)
            painted.Texture.Dispose();

        PumpkinCache.Clear();
```

  - add a method after `GetFrame`:

```csharp
    /// <summary>A lit carving pumpkin's frame with its carving painted in. Falls back to the plain frame where the face map has nothing.</summary>
    public SpriteFrame? GetPumpkinFrame(int spriteId, int frameIndex, PumpkinLook look, int phase)
    {
        var key = (look.Version, frameIndex, phase);

        if (PumpkinCache.TryGetValue(key, out var cached))
            return cached;

        if (GetFrame(spriteId, frameIndex) is not { } plain)
            return null;

        var map = PumpkinFaceMap.Shared.For(frameIndex);

        if (map.Count == 0)
            return plain;

        using var scope = new PixelBufferScope(plain.Texture);
        PumpkinPainter.Paint(scope.AsSpan(), scope.Width, scope.Height, plain.Left, plain.Top, map, look.Grid, phase);

        var texture = new Texture2D(TextureConverter.Device, scope.Width, scope.Height);
        scope.CommitTo(texture);

        var painted = new SpriteFrame(texture, plain.CenterX, plain.CenterY, plain.Left, plain.Top);
        PumpkinCache[key] = painted;

        return painted;
    }
```

  - in `Draw`, add two optional parameters after `float alpha = 1f`: `PumpkinLook? pumpkin = null, int pumpkinPhase = 0`, and replace `var spriteFrame = GetFrame(spriteId, frameIndex);` with:

```csharp
        var spriteFrame = pumpkin is { Lit: true } && (spriteId == PumpkinFaceMap.SPRITE_ID)
            ? GetPumpkinFrame(spriteId, frameIndex, pumpkin, pumpkinPhase)
            : GetFrame(spriteId, frameIndex);
```

  If `PixelBufferScope.AsSpan()` returns a different span type, pass what `Paint` needs (a `Span<Color>` over `scope.Pixels` limited to `scope.Count`).

- [ ] **Step 6: Draw with the look.** In `C/Chaos.Client/Screens/WorldScreen.Draw.cs`, in `DrawCreature`, change the final `creatureRenderer.Draw(...)` call by adding two arguments after `alpha`:

```csharp
            alpha,
            WorldState.PumpkinLooks.Get(entity.Id),
            PumpkinPainter.PhaseAt(Environment.TickCount64));
```

- [ ] **Step 7: Run the tests and build.** Expected: 4 store tests pass; the solution builds.

```json:metadata
{"files": ["Chaos.Client/Collections/PumpkinLookStore.cs", "Chaos.Client/Collections/WorldState.cs", "Chaos.Client.Rendering/CreatureRenderer.cs", "Chaos.Client/Screens/WorldScreen.Draw.cs", "Tests/Chaos.Client.Tests/PumpkinLookStoreTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\pumpkin-carving-server -- --no-ansi --treenode-filter \"/*/*/PumpkinLookStoreTests/*\"", "acceptanceCriteria": ["looks by id in either order, versions rise, blank empty grid, removal/clear", "LanternFor Small only when lit; WorldState applies it", "Draw takes look + phase; lit 1455 pumpkins draw painted cached frames cleared in Clear"], "modelTier": "standard"}
```

---

### Task 11: Carving window model (client)

**Goal:** `PumpkinCarvingModel`: grid, knife/eraser, mirror, undo, redo, clear, and the unsent flag, fully tested.

**Files:**
- Create: `C/Chaos.Client/ViewModel/PumpkinCarvingModel.cs`
- Test: `C/Tests/Chaos.Client.Tests/PumpkinCarvingModelTests.cs`

**Acceptance Criteria:**
- [ ] The knife cuts, the eraser and the erase flag (right-click) fill; mirror applies to column `21 − x`.
- [ ] A stroke undoes as one step; redo restores; a new stroke clears redo; clear is undoable.
- [ ] `Load` resets history and the unsent flag; `TakeForSend` returns a copy and clears the flag; out-of-range cells are ignored.

**Verify:** client test command with `--treenode-filter "/*/*/PumpkinCarvingModelTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** — `C/Tests/Chaos.Client.Tests/PumpkinCarvingModelTests.cs`:

```csharp
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class PumpkinCarvingModelTests
{
    private static void Stroke(PumpkinCarvingModel model, params (int X, int Y)[] cells)
    {
        model.BeginStroke();

        foreach ((var x, var y) in cells)
            model.Apply(x, y, erase: false);

        model.EndStroke();
    }

    [Test]
    public void The_knife_cuts_and_mirror_cuts_the_matching_column()
    {
        var model = new PumpkinCarvingModel { Mirror = true };

        Stroke(model, (2, 5));

        PumpkinGrid.IsCut(model.Grid, 2, 5).Should().BeTrue();
        PumpkinGrid.IsCut(model.Grid, 19, 5).Should().BeTrue();
        model.CutCount.Should().Be(2);
        model.HasUnsent.Should().BeTrue();
    }

    [Test]
    public void The_eraser_and_a_right_click_fill_cells_back_in()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (1, 1), (2, 1));

        model.Apply(1, 1, erase: true);
        model.Tool = PumpkinTool.Eraser;
        model.Apply(2, 1, erase: false);

        model.CutCount.Should().Be(0);
    }

    [Test]
    public void A_stroke_undoes_as_one_step_and_redo_restores_it()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (0, 0), (1, 0), (2, 0));

        model.Undo();

        model.CutCount.Should().Be(0);
        model.CanRedo.Should().BeTrue();

        model.Redo();

        model.CutCount.Should().Be(3);

        model.Undo();
        Stroke(model, (5, 5));

        model.CanRedo.Should().BeFalse();
    }

    [Test]
    public void Clear_is_undoable()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (3, 3));

        model.Clear();

        model.CutCount.Should().Be(0);

        model.Undo();

        model.CutCount.Should().Be(1);
    }

    [Test]
    public void Load_resets_history_and_the_unsent_flag()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (3, 3));
        var saved = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(saved, 9, 9, true);

        model.Load(saved);

        PumpkinGrid.IsCut(model.Grid, 9, 9).Should().BeTrue();
        model.CanUndo.Should().BeFalse();
        model.HasUnsent.Should().BeFalse();
    }

    [Test]
    public void Taking_the_grid_for_sending_copies_it_and_clears_the_flag()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (4, 4));

        var sent = model.TakeForSend();
        sent[0] = 0xFF;

        model.HasUnsent.Should().BeFalse();
        model.CutCount.Should().Be(1);
    }

    [Test]
    public void Cells_outside_the_grid_are_ignored()
    {
        var model = new PumpkinCarvingModel();

        Stroke(model, (-1, 0), (22, 0), (0, 14));

        model.CutCount.Should().Be(0);
        model.CanUndo.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run them and see them fail.**

- [ ] **Step 3: Write the model** — `C/Chaos.Client/ViewModel/PumpkinCarvingModel.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

public enum PumpkinTool
{
    Knife,
    Eraser
}

/// <summary>
///     The Pumpkin Carving window's state: the grid, the tool, mirror, undo and redo, and whether the grid changed since it
///     was last sent. No graphics, so it is tested directly.
/// </summary>
public sealed class PumpkinCarvingModel
{
    private const int MAX_UNDO = 50;

    private readonly List<byte[]> RedoSteps = [];
    private readonly List<byte[]> UndoSteps = [];
    private byte[]? StrokeStart;

    public bool CanRedo => RedoSteps.Count > 0;
    public bool CanUndo => UndoSteps.Count > 0;
    public int CutCount => PumpkinGrid.CountCut(Grid);
    public byte[] Grid { get; private set; } = PumpkinGrid.Empty();
    public bool HasUnsent { get; private set; }
    public bool Mirror { get; set; }
    public PumpkinTool Tool { get; set; } = PumpkinTool.Knife;
    public int Version { get; private set; }

    /// <summary>Cuts with the knife, fills with the eraser; <paramref name="erase" /> (a right-click) always fills.</summary>
    public void Apply(int x, int y, bool erase)
    {
        if (((uint)x >= PumpkinGrid.WIDTH) || ((uint)y >= PumpkinGrid.HEIGHT))
            return;

        var cut = !erase && (Tool == PumpkinTool.Knife);
        var changed = Set(x, y, cut);

        if (Mirror)
            changed |= Set(MirrorX(x), y, cut);

        if (changed)
            Changed();
    }

    public void BeginStroke() => StrokeStart = Grid.ToArray();

    public void Clear()
    {
        if (CutCount == 0)
            return;

        Record(UndoSteps, Grid);
        RedoSteps.Clear();
        Grid = PumpkinGrid.Empty();
        Changed();
    }

    public void EndStroke()
    {
        if (StrokeStart is null)
            return;

        if (!StrokeStart.AsSpan().SequenceEqual(Grid))
        {
            Record(UndoSteps, StrokeStart);
            RedoSteps.Clear();
        }

        StrokeStart = null;
    }

    public void Load(byte[] grid)
    {
        Grid = PumpkinGrid.IsValid(grid) ? grid.ToArray() : PumpkinGrid.Empty();
        UndoSteps.Clear();
        RedoSteps.Clear();
        StrokeStart = null;
        HasUnsent = false;
        Version++;
    }

    public static int MirrorX(int x) => PumpkinGrid.WIDTH - 1 - x;

    public void Redo()
    {
        if (!CanRedo)
            return;

        Record(UndoSteps, Grid);
        Grid = Pop(RedoSteps);
        Changed();
    }

    public byte[] TakeForSend()
    {
        HasUnsent = false;

        return Grid.ToArray();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;

        Record(RedoSteps, Grid);
        Grid = Pop(UndoSteps);
        Changed();
    }

    private void Changed()
    {
        HasUnsent = true;
        Version++;
    }

    private static byte[] Pop(List<byte[]> steps)
    {
        var last = steps[^1];
        steps.RemoveAt(steps.Count - 1);

        return last;
    }

    private static void Record(List<byte[]> steps, byte[] grid)
    {
        steps.Add(grid.ToArray());

        if (steps.Count > MAX_UNDO)
            steps.RemoveAt(0);
    }

    private bool Set(int x, int y, bool cut)
    {
        if (PumpkinGrid.IsCut(Grid, x, y) == cut)
            return false;

        PumpkinGrid.SetCut(Grid, x, y, cut);

        return true;
    }
}
```

- [ ] **Step 4: Run the tests and see them pass.** Expected: 7 tests pass.

```json:metadata
{"files": ["Chaos.Client/ViewModel/PumpkinCarvingModel.cs", "Tests/Chaos.Client.Tests/PumpkinCarvingModelTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\pumpkin-carving-server -- --no-ansi --treenode-filter \"/*/*/PumpkinCarvingModelTests/*\"", "acceptanceCriteria": ["knife cuts, eraser/right-click fill, mirror 21-x", "stroke undo as one step, redo, new stroke clears redo, clear undoable", "Load resets; TakeForSend copies and clears; out of range ignored"], "modelTier": "mechanical"}
```

---

### Task 12: Carving window and wiring (client)

**Goal:** The carving canvas and window, and `WorldScreen` wiring for the window and the looks.

**Files:**
- Create: `C/Chaos.Client/Controls/World/Popups/PumpkinCarving/PumpkinCarvingCanvas.cs`
- Create: `C/Chaos.Client/Controls/World/Popups/PumpkinCarving/PumpkinCarvingControl.cs`
- Create: `C/Chaos.Client/Screens/WorldScreen.PumpkinCarving.cs`
- Modify: `C/Chaos.Client/Screens/WorldScreen.cs`, `C/Chaos.Client/Screens/WorldScreen.Map.cs`

**Acceptance Criteria:**
- [ ] The server's Open shows the window with the saved grid and the time left; reopening an open window keeps unsent strokes.
- [ ] Left-drag uses the tool, right-drag always erases; Knife/Eraser/Mirror/Undo/Redo/Clear work.
- [ ] Saves go out every 3 s while changed (about every 1.1 s in the last 10 s); Done and Close send a closing save; a server Close or a map change hides the window without sending.
- [ ] The preview shows the lit pumpkin at 2× when sprite 1455 is installed and nothing otherwise.
- [ ] Looks from the server reach `WorldState.ApplyPumpkinLook`.

**Verify:** `dotnet build Chaos.Client.slnx -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server` → 0 errors; full client tests pass

**Steps:**

- [ ] **Step 1: The canvas** — `PumpkinCarvingCanvas.cs`. Copy the `using` block from `GuildEmblemCanvas.cs`, then:

```csharp
namespace Chaos.Client.Controls.World.Popups.PumpkinCarving;

/// <summary>
///     The carving grid at a fixed zoom: skin cells orange, cut cells candle yellow. A left drag uses the window's tool; a
///     right drag always erases. Raises one event per cell, with every cell on the line between mouse moves.
/// </summary>
public sealed class PumpkinCarvingCanvas : UIElement
{
    private static readonly Color Cut = new(255, 226, 122);
    private static readonly Color GridLine = new(0, 0, 0, 50);
    private static readonly Color Skin = new(217, 116, 28);

    private bool Erasing;
    private byte[] Grid = PumpkinGrid.Empty();
    private Point LastCell;
    private bool Painting;

    public PumpkinCarvingCanvas(int zoom)
    {
        Zoom = zoom;
        Width = PumpkinGrid.WIDTH * zoom;
        Height = PumpkinGrid.HEIGHT * zoom;
    }

    public int Zoom { get; }

    /// <summary>x, y, and true when the stroke erases (a right drag).</summary>
    public event Action<int, int, bool>? CellPainted;

    public event Action? StrokeEnded;
    public event Action? StrokeStarted;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        for (var y = 0; y < PumpkinGrid.HEIGHT; y++)
            for (var x = 0; x < PumpkinGrid.WIDTH; x++)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(ScreenX + (x * Zoom), ScreenY + (y * Zoom), Zoom, Zoom),
                    PumpkinGrid.IsCut(Grid, x, y) ? Cut : Skin);

        for (var x = 1; x < PumpkinGrid.WIDTH; x++)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (x * Zoom), ScreenY, 1, Height), GridLine);

        for (var y = 1; y < PumpkinGrid.HEIGHT; y++)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + (y * Zoom), Width, 1), GridLine);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (Painting || ((e.Button != MouseButton.Left) && (e.Button != MouseButton.Right)))
            return;

        Painting = true;
        Erasing = e.Button == MouseButton.Right;
        LastCell = CellAt(e.ScreenX, e.ScreenY);
        StrokeStarted?.Invoke();
        PaintCell(LastCell.X, LastCell.Y);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Painting)
            return;

        e.Handled = true;

        //the button came up where no mouse-up reached this canvas (e.g. the window lost focus mid-stroke)
        if (!(Erasing ? InputBuffer.IsRightButtonHeld : InputBuffer.IsLeftButtonHeld))
        {
            EndStroke();

            return;
        }

        var cell = CellAt(e.ScreenX, e.ScreenY);

        if (cell == LastCell)
            return;

        PaintLine(LastCell, cell);
        LastCell = cell;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (!Painting)
            return;

        EndStroke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();
        EndStroke();
    }

    public void SetGrid(byte[] grid) => Grid = grid;

    private Point CellAt(int screenX, int screenY)
        => new((int)Math.Floor((screenX - ScreenX) / (double)Zoom), (int)Math.Floor((screenY - ScreenY) / (double)Zoom));

    private void EndStroke()
    {
        if (!Painting)
            return;

        Painting = false;
        StrokeEnded?.Invoke();
    }

    private void PaintCell(int x, int y)
    {
        if ((x >= 0) && (y >= 0) && (x < PumpkinGrid.WIDTH) && (y < PumpkinGrid.HEIGHT))
            CellPainted?.Invoke(x, y, Erasing);
    }

    /// <summary>Paints every cell on the straight line after <paramref name="from" /> up to <paramref name="to" />, so a fast drag leaves no gaps.</summary>
    private void PaintLine(Point from, Point to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = -Math.Abs(to.Y - from.Y);
        var sx = from.X < to.X ? 1 : -1;
        var sy = from.Y < to.Y ? 1 : -1;
        var error = dx + dy;
        var x = from.X;
        var y = from.Y;

        while ((x != to.X) || (y != to.Y))
        {
            var doubled = 2 * error;

            if (doubled >= dy)
            {
                error += dy;
                x += sx;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += sy;
            }

            PaintCell(x, y);
        }
    }
}
```

Add `using Chaos.DarkAges.Definitions;`. If `MouseButton.Right` is named differently, use the name `InputDispatcher` uses for the right button.

- [ ] **Step 2: The window** — `PumpkinCarvingControl.cs`. Copy the `using` block from `GuildEmblemEditorControl.cs` and add `using Chaos.Client.Controls.World.Popups.GuildCloak;`, `using Microsoft.Xna.Framework.Graphics;`. Then:

```csharp
namespace Chaos.Client.Controls.World.Popups.PumpkinCarving;

/// <summary>
///     The Pumpkin Carving window (spec: docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md, 5.1): the 22 × 14
///     grid, knife, eraser, mirror, undo, redo and clear, a lit preview of the pumpkin and the time left. Saves to the
///     server every few seconds while changed; Done and Close send a closing save. Opened by the server's
///     PumpkinCarvingDisplay Open.
/// </summary>
/// <remarks>Layout: title; the canvas with the preview and timer on its right; two rows of three tools; a status line; Done.</remarks>
public sealed class PumpkinCarvingControl : GuildCloakDialogBase
{
    private const int BORDER_GAP = 4;
    private const int CANVAS_HEIGHT = PumpkinGrid.HEIGHT * ZOOM;
    private const int CANVAS_WIDTH = PumpkinGrid.WIDTH * ZOOM;
    private const int CONTENT_TOP = 32;
    private const int DONE_WIDTH = 64;
    private const long FAST_SAVE_MS = 1_100;
    private const int FAST_SAVE_SECONDS = 10;
    private const int GAP = 8;
    private const int LEFT = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int OK_RIGHT_MARGIN = 20;
    private const int PREVIEW_SIZE = 88;
    private const long SAVE_MS = 3_000;
    private const int TITLE_TOP = 10;
    private const int TOOL_SPACING = 3;
    private const int TOOL_WIDTH = (CANVAS_WIDTH - (2 * TOOL_SPACING)) / 3;
    private const int WARN_SECONDS = 30;
    private const int ZOOM = 10;

    private static readonly Color WarnColor = new(230, 70, 50);

    private readonly PumpkinCarvingCanvas Canvas;
    private readonly CreatureRenderer Creatures;
    private readonly CustomButton EraserButton;
    private readonly CustomButton KnifeButton;
    private readonly CustomButton MirrorButton;
    private readonly PumpkinCarvingModel Model = new();
    private readonly int PreviewX;
    private readonly CustomButton RedoButton;
    private readonly UILabel StatusLabel;
    private readonly UILabel TimerLabel;
    private readonly CustomButton UndoButton;

    private long DeadlineMs;
    private long LastSendMs = long.MinValue / 2;
    private Texture2D? Preview;
    private int PreviewVersion = -1;
    private int ShownVersion = -1;

    public PumpkinCarvingControl(CreatureRenderer creatures)
        : base("_nsett", false)
    {
        Creatures = creatures;
        Name = "PumpkinCarving";
        Visible = false;
        UsesControlStack = true;

        Canvas = new PumpkinCarvingCanvas(ZOOM)
        {
            X = LEFT,
            Y = CONTENT_TOP
        };

        var toolsTop = CONTENT_TOP + CANVAS_HEIGHT + GAP;
        var secondToolRow = toolsTop + CustomButton.HEIGHT + TOOL_SPACING;
        var statusTop = secondToolRow + CustomButton.HEIGHT + GAP;
        var buttonsTop = statusTop + TextRenderer.CHAR_HEIGHT + GAP;
        PreviewX = LEFT + CANVAS_WIDTH + GAP;

        Width = PreviewX + PREVIEW_SIZE + LEFT;
        Height = buttonsTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(RequestClose, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        Caption("Carve your pumpkin", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        Canvas.StrokeStarted += Model.BeginStroke;
        Canvas.CellPainted += Model.Apply;
        Canvas.StrokeEnded += Model.EndStroke;
        AddChild(Canvas);

        KnifeButton = ToolButton("Knife", 0, toolsTop, () => Model.Tool = PumpkinTool.Knife);
        EraserButton = ToolButton("Eraser", 1, toolsTop, () => Model.Tool = PumpkinTool.Eraser);
        MirrorButton = ToolButton("Mirror", 2, toolsTop, () => Model.Mirror = !Model.Mirror);
        UndoButton = ToolButton("Undo", 0, secondToolRow, Model.Undo);
        RedoButton = ToolButton("Redo", 1, secondToolRow, Model.Redo);
        ToolButton("Clear", 2, secondToolRow, Model.Clear);

        TimerLabel = Caption(string.Empty, PreviewX, CONTENT_TOP + PREVIEW_SIZE + GAP, PREVIEW_SIZE, HorizontalAlignment.Center);
        StatusLabel = Caption(string.Empty, LEFT, statusTop, CANVAS_WIDTH);
        AddButton("Done", DONE_WIDTH, Width - LEFT - DONE_WIDTH, buttonsTop, Finish);
    }

    /// <summary>Raised with a copy of the grid and whether the window is closing.</summary>
    public event Action<byte[], bool>? SaveRequested;

    public override void Dispose()
    {
        Preview?.Dispose();
        Preview = null;
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        if (PreviewVersion != Model.Version)
            RebuildPreview();

        if (Preview is not null)
            DrawTextureFitted(
                spriteBatch,
                Preview,
                new Rectangle(ScreenX + PreviewX, ScreenY + CONTENT_TOP, Preview.Width * 2, Preview.Height * 2),
                Color.White);
    }

    /// <summary>The server closed the window (time up, removed, a map change). Nothing more is sent.</summary>
    public void OnServerClose() => Hide();

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            RequestClose();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Shows the window with the server's grid. Reopening an open window keeps strokes not yet sent.</summary>
    public void Open(ushort secondsLeft, byte[] grid)
    {
        DeadlineMs = Environment.TickCount64 + (secondsLeft * 1000L);

        if (!Visible)
            Model.Load(grid);

        Show();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        var now = Environment.TickCount64;
        var secondsLeft = (int)Math.Max(0, (DeadlineMs - now + 999) / 1000);
        TimerLabel.Text = $"{secondsLeft / 60}:{secondsLeft % 60:00} left";
        TimerLabel.ForegroundColor = secondsLeft < WARN_SECONDS ? WarnColor : LegendColors.White;

        if (Model.Version != ShownVersion)
        {
            ShownVersion = Model.Version;
            Canvas.SetGrid(Model.Grid);
            RefreshControls();
        }

        //near the end, save faster so little is lost when time runs out
        var interval = secondsLeft <= FAST_SAVE_SECONDS ? FAST_SAVE_MS : SAVE_MS;

        if (Model.HasUnsent && ((now - LastSendMs) >= interval))
            Send(closing: false);
    }

    private void Finish()
    {
        Send(closing: true);
        Hide();
    }

    private void RebuildPreview()
    {
        PreviewVersion = Model.Version;

        //no preview until the launcher patch with the pumpkin sprite is installed
        if (Creatures.GetFrame(PumpkinFaceMap.SPRITE_ID, PumpkinFaceMap.FRONT_FRAME) is not { } plain)
            return;

        using var scope = new PixelBufferScope(plain.Texture);

        PumpkinPainter.Paint(
            scope.AsSpan(),
            scope.Width,
            scope.Height,
            plain.Left,
            plain.Top,
            PumpkinFaceMap.Shared.For(PumpkinFaceMap.FRONT_FRAME),
            Model.Grid,
            0);

        Preview ??= new Texture2D(TextureConverter.Device, scope.Width, scope.Height);
        scope.CommitTo(Preview);
    }

    private void RefreshControls()
    {
        KnifeButton.Selected = Model.Tool == PumpkinTool.Knife;
        EraserButton.Selected = Model.Tool == PumpkinTool.Eraser;
        MirrorButton.Selected = Model.Mirror;
        UndoButton.Enabled = Model.CanUndo;
        RedoButton.Enabled = Model.CanRedo;

        StatusLabel.Text = Model.CutCount < 6
            ? $"Cuts: {Model.CutCount}. Cut at least 6 to count."
            : $"Cuts: {Model.CutCount}. Right-click erases.";
    }

    private void RequestClose()
    {
        if (Model.HasUnsent)
            Send(closing: true);

        Hide();
    }

    private void Send(bool closing)
    {
        LastSendMs = Environment.TickCount64;
        SaveRequested?.Invoke(Model.TakeForSend(), closing);
    }

    private CustomButton ToolButton(string caption, int column, int top, Action onClick)
        => AddButton(
            caption,
            TOOL_WIDTH,
            LEFT + (column * (TOOL_WIDTH + TOOL_SPACING)),
            top,
            () =>
            {
                onClick();
                RefreshControls();
            });
}
```

The 6 in `RefreshControls` is the server's `PumpkinCarving.MIN_CUT_CELLS`; it is not shared, so name it `MIN_CUTS = 6` as a constant with a comment pointing at the server constant.

- [ ] **Step 3: Wiring** — `C/Chaos.Client/Screens/WorldScreen.PumpkinCarving.cs`:

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.PumpkinCarving;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Pumpkin Carving: the carving window and pumpkin looks (spec 2026-10-03-pumpkin-carving-design.md).</summary>
public sealed partial class WorldScreen
{
    //carving window — opened by the server's PumpkinCarvingDisplay Open when a player claims a pumpkin
    private PumpkinCarvingControl PumpkinWindow = null!;

    private void CreatePumpkinCarving()
    {
        PumpkinWindow = new PumpkinCarvingControl(Game.CreatureRenderer)
        {
            ZIndex = 2
        };

        Game.Connection.OnPumpkinCarvingDisplay += HandlePumpkinCarvingDisplay;
        Game.Connection.OnPumpkinLook += HandlePumpkinLook;
        PumpkinWindow.SaveRequested += (grid, closing) => Game.Connection.SendPumpkinCarvingSave(grid, closing);
    }

    private void HandlePumpkinCarvingDisplay(PumpkinCarvingDisplayArgs args)
    {
        if (args.Type == PumpkinCarvingDisplayType.Open)
            PumpkinWindow.Open(args.SecondsLeft, args.Grid ?? PumpkinGrid.Empty());
        else
            PumpkinWindow.OnServerClose();
    }

    private static void HandlePumpkinLook(PumpkinLookArgs args)
        => WorldState.ApplyPumpkinLook(args.EntityId, args.State == PumpkinLookState.Lit, args.Grid);

    private void UnwirePumpkinCarving()
    {
        Game.Connection.OnPumpkinCarvingDisplay -= HandlePumpkinCarvingDisplay;
        Game.Connection.OnPumpkinLook -= HandlePumpkinLook;
    }
}
```

In `WorldScreen.cs`:
- right after the fishing window block (`FishingWindow = new FishingControl { ZIndex = 2 }; WireFishing();`) add `CreatePumpkinCarving();`
- after `Root.AddChild(FishingWindow);` add `Root.AddChild(PumpkinWindow);`
- after `Game.Connection.OnFishingDisplay -= HandleFishingDisplay;` add `UnwirePumpkinCarving();`

In `WorldScreen.Map.cs`, after `FishingWindow.Hide();` add:

```csharp
        //the round on the old map already let this carver go; nothing to send
        PumpkinWindow.OnServerClose();
```

- [ ] **Step 4: Build and run all client tests.** Expected: 0 errors; all tests pass.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/PumpkinCarving/PumpkinCarvingCanvas.cs", "Chaos.Client/Controls/World/Popups/PumpkinCarving/PumpkinCarvingControl.cs", "Chaos.Client/Screens/WorldScreen.PumpkinCarving.cs", "Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/Screens/WorldScreen.Map.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\pumpkin-carving-server", "acceptanceCriteria": ["Open shows the window with grid and time; reopen keeps unsent strokes", "left uses tool, right erases; six tool buttons work", "saves every 3 s / 1.1 s near the end; Done/Close send closing save; server close and map change send nothing", "preview at 2x when sprite installed", "looks reach WorldState.ApplyPumpkinLook"], "modelTier": "standard"}
```

---

### Task 13: Docs, full test runs and the commits

**Goal:** Record what was built, run every suite, and make one commit per repo with the client pointing at the server commit.

**Files:**
- Modify: `C/docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md` (append "As built" notes if anything differed from section 10)
- Modify: `C/CLAUDE.md` (the Popups list gains `PumpkinCarving/`; the Rendering list gains `PumpkinFaceMap`/`PumpkinPainter`)
- Modify: `U/Tools/PumpkinCarving/README.md` (new: how to run the two tools and ship the dat)

**Acceptance Criteria:**
- [ ] Server: all tests pass except the 2 known failures. Client: all tests pass. Python: `python -m pytest Tools/PumpkinCarving -q` passes.
- [ ] One commit in each worktree on `feat/pumpkin-carving`, staged by explicit path; `launchSettings.json`, `appsettings.json` local paths and `.run` files are not committed.
- [ ] The client commit points its `Chaos-Server` submodule at the server commit.

**Verify:** `git -C <worktree> log --oneline -1` in each worktree shows the new commit; `git -C C diff HEAD~1 --stat` lists `Chaos-Server`

**Steps:**

- [ ] **Step 1: Run every suite** (commands in Global Constraints, without filters). Stop and report any new failure.

- [ ] **Step 2: Write `U/Tools/PumpkinCarving/README.md`:**

```markdown
# Pumpkin Carving art

1. `python Tools/PumpkinCarving/make_pumpkin.py` draws `art/pumpkin_frames.json`, `art/pumpkinfacemap.json` and
   `art/preview.png` from one geometry. `--check` only checks.
2. Copy `art/pumpkinfacemap.json` to Chaos.Client `Chaos.Client.Rendering/Assets/Pumpkin/` whenever it changes. The
   client's face map and the sprite must come from the same run.
3. `dotnet run Tools/PumpkinCarving/pack_pumpkin.cs` writes `hades.dat` with `mns1455.mpf` and `mns355.pal` to a
   Desktop folder, re-reads it and saves `check-front.png`. Ship that `hades.dat` in the launcher patch with the client
   that has the carving window (CLIENT_VERSION 767).

`test_pumpkin_content.py` keeps the stage tiles in step with `PumpkinCarving` in Chaos-Server.
```

- [ ] **Step 3: Update `C/CLAUDE.md`.** In the Popups paragraph, add: "`PumpkinCarving/` (PumpkinCarvingControl — the Pumpkin Carving window, opened by the server's PumpkinCarvingDisplay Open when a player claims a stage pumpkin; PumpkinCarvingCanvas — the 22 × 14 grid; the state is `ViewModel/PumpkinCarvingModel`)". In the Rendering list, add a bullet: "**`PumpkinFaceMap`/`PumpkinPainter`** -- Pumpkin Carving: the embedded face map (which carving cells each pixel of sprite 1455 shows) and the painter that lights carved pixels; `CreatureRenderer.GetPumpkinFrame` caches painted frames per look."

- [ ] **Step 4: Commit the server** (from `S`), staging by explicit path (list every file from Tasks 1-4), message:

```
Add Pumpkin Carving: a director-run carving game in the haunted theatre

Messages 142 (save), 149 (window) and 150 (look); CLIENT_VERSION 767.
PumpkinCarvingRound holds the rules; PumpkinCarvingMapScript runs a round on
suomi_theatre_halloween; Spotlight Chairs and carving refuse each other.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 5: Commit Unora** (from `U`), explicit paths (Tasks 5-7 files, `art/`, README), message:

```
Add Pumpkin Carving content and art tools

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 6: Point the client at the server commit and commit** (from `C`):

```powershell
git submodule update --init Chaos-Server
git -C Chaos-Server fetch C:\Users\Michael\Documents\GitHub\worktrees\pumpkin-carving-server feat/pumpkin-carving
git -C Chaos-Server checkout FETCH_HEAD
```

Stage `Chaos-Server` and every file from Tasks 8-12 plus `CLAUDE.md` and the spec (`git add -f` for files under `docs/`), message:

```
Add the Pumpkin Carving window and lit pumpkins; point Chaos-Server at the carving commit

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 7: Report** the three commit hashes, the Desktop folder path from Task 7, and the by-hand checklist from spec section 7.

```json:metadata
{"files": ["docs/superpowers/specs/2026-10-03-pumpkin-carving-design.md", "CLAUDE.md", "Tools/PumpkinCarving/README.md"], "verifyCommand": "git -C C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\pumpkin-carving-client log --oneline -1", "acceptanceCriteria": ["all suites pass (server: only the 2 known failures)", "one commit per worktree by explicit path; no local config committed", "client submodule points at the server commit"], "modelTier": "mechanical"}
```

---

## Dependencies

| Task | Blocked by |
|---|---|
| 1 | 0 |
| 2 | 1 |
| 3 | 2 |
| 4 | 3 |
| 5 | 4 |
| 6 | 0 |
| 7 | 6 |
| 8 | 1 |
| 9 | 1, 6 |
| 10 | 8, 9 |
| 11 | 1 |
| 12 | 10, 11 |
| 13 | 4, 5, 7, 12 |
