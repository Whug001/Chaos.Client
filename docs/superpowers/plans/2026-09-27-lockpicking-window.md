# Lockpicking Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the crypt chest's timing bar and the Deep Crypt/Asilon number guess with one Skyrim-style lockpicking window in the client, judged by the server.

**Architecture:** The server keeps each chest's lock (a hidden sweet-spot angle, a width, a warm range and a jam count) in a `LockpickLock`. It keeps the rogue's session in a `LockpickChest`, which is owned by the chest's merchant script. Two new packets carry the window: `LockpickInteraction` (turn at an angle, or close) and `LockpickDisplay` (open, turn result, close). The client draws a framed panel like the Gilded Spindle, with a lock made from five new sprite files in setoa.dat, and animates only what the server tells it.

**Tech Stack:** C# / .NET 10 (Chaos-Server, and Chaos.Client with MonoGame), TUnit + FluentAssertions + Moq, Python 3 + Pillow (Unora tools), DALib file formats.

**Spec:** `docs/superpowers/specs/2026-09-27-lockpicking-window-design.md` in Chaos.Client (`main`, commit cc08645, plus the "Amendments" section added while planning). Read the amendments: they override the spec body where the two differ.

## Global Constraints

- **Worktrees.** Task 0 creates these, each on a new branch `feat/lockpick-window`:
  - Server: `C:\Users\Michael\Documents\GitHub\worktrees\lockpick-server` (called SERVER below), from server commit **2821f83f7**. That is the local, unpushed dialog-block merge. Do not base it on `origin/master`.
  - Client: `C:\Users\Michael\Documents\GitHub\worktrees\lockpick-client` (called CLIENT below), from client `main`.
  - Unora: `C:\Users\Michael\Documents\GitHub\worktrees\lockpick-unora` (called UNORA below), from Unora `main`.
  - The server repo is the client's submodule: `C:\Users\Michael\Documents\GitHub\Chaos.Client\Chaos-Server`.
  - The local game client folder is `C:\Users\Michael\Documents\Unora\Unora Files` (`acclib.paths.DEFAULT_DATA_DIR`).
- **Client builds against SERVER.** Set `UnoraServerPath` to SERVER for every client build and test run. In PowerShell, run `$env:UnoraServerPath = 'C:\Users\Michael\Documents\GitHub\worktrees\lockpick-server'`. In bash, prefix the command with `UnoraServerPath=/c/Users/Michael/Documents/GitHub/worktrees/lockpick-server`.
- **Shared checkouts.** Other Claude sessions use these repos at the same time.
  - Stage by explicit path only. Never use `git add -A`, stash, reset or clean.
  - Never commit `launchSettings.json` or a local `StagingDirectory` in `appsettings.json`.
  - `docs/` is git-ignored in Chaos.Client, so plans and specs need `git add -f`.
- **Commits.** This plan uses the at-end commit strategy. Implementers do not commit. Task 14 makes one commit per repo.
- **Building.**
  - Stop any running `Chaos.exe` or `Chaos.Client.exe` before building, because they lock `bin`. Ask the user to stop them; don't kill them.
  - Never build the server and client solutions at the same time.
- **Tests.**
  - Test projects are TUnit executables. Run them with `dotnet run --project <proj> -- --no-ansi [--treenode-filter "/*/*/Class/*"]`, never `dotnet test`.
  - `Chaos.Tests` already has two failures on master: GiveAbility and OnItemDroppedOn stackable. They are not regressions.
- **Code style.**
  - Read code with Serena (`get_symbols_overview`, `find_symbol`) and edit with Serena's symbol tools where they fit. Worktrees under `C:\Users\Michael\Documents\GitHub\worktrees\` work with Serena's edit tools, using paths relative to `C:\Users\Michael\Documents\GitHub`.
  - Match the surrounding code: `#region` using blocks, PascalCase private fields, `///` summaries on public types, `//lowercase` inline comments.
- **Opcodes.** These were the next free numbers on 2026-09-27. Before Task 1, confirm that no branch in the server repo has taken them: `git -C <server repo> grep -n "= 131,\|= 135," $(git -C <server repo> for-each-ref --format='%(refname)' refs/heads) -- Chaos.Networking.Abstractions/Definitions/Enums.cs`.
  - `ClientOpCode.LockpickInteraction = 131`
  - `ServerOpCode.LockpickDisplay = 135`
- **Wire formats:**
  - `LockpickInteraction`: `byte type` (0 Turn, 1 Close), then `byte pickDegrees` (0–180).
  - `LockpickDisplay`: `byte type`, then by type:
    - Open (0): `byte difficulty`, `string8 title`, `u16 lockpickCount`
    - TurnResult (1): `byte outcome`, `byte turnPercent`, `u16 lockpickCount`
    - Close (2): `string8 reason`
- **Angles.** Pick 0° = pointing left, 90° = straight up, 180° = pointing right. The cylinder turns clockwise from 0° to 90°. `turnPercent` × 90° is how far it turns.
- **Tuning** (`LockpickTuning`: width / warm range / jam that breaks the pick):
  - Crypt: Easy 20 / 60 / 6, Medium 12 / 45 / 5, Hard 6 / 30 / 4.
  - Item chests: Easy 12 / 20 / 2, Medium 12 / 15 / 2, Hard 6 / 30 / 2.
- **Turn percent.** Opened = 100. Otherwise `pastEdge = |pick − sweet| − width/2`. Then `percent = pastEdge >= warm ? 5 : round(99 − pastEdge / warm × 94)`, rounding half away from zero.
- **Session rules:**
  - Reach is 1 tile (`WithinRange(chest, 1)`).
  - Turns less than 400 ms apart are ignored.
  - The session ends after 60 s without a turn.
  - Sound 183 (open) and 184 (break) play for players within 8 tiles.
- **Server messages (exact text):**
  - "The chest has vanished already!"
  - "Only a rogue could pick this lock."
  - "You should get some lockpicks!"
  - "You are too far away from the chest!"
  - "Someone is already working on this lock."
  - "Your lockpick broke!"
  - Close reasons: "The chest is gone.", "You moved away from the chest.", "You have no lockpicks.", "You lose your focus."
- **Sprite files** in setoa.dat, each with its own palette. Index 0 is transparent. No visible pixel may be pure black (0,0,0), because the client and the review sheets treat it as transparent.
  - `lockpk01.spf`: plate, 184 × 184
  - `lockpk02.spf`: cylinder, 19 frames of 64 × 64, 0°–90° in 5° steps
  - `lockpk03.spf`: pick, 8 × 86, pointing up, tip at the bottom-centre
  - `lockpk04.spf`: wrench, 12 × 60, hanging down from the top-centre
  - `lockpk05.spf`: broken pick, 2 frames of 8 × 86: frame 0 is the tip half, frame 1 is the handle half, each on the full pick canvas

**User decisions (already made):**
- All lockable chests use the window: crypt, Deep Crypt Easy/Medium/Hard, and the four Asilon Prairie chests.
- When a pick breaks, one lockpick is used up and the chest disappears.
- Layout A: a framed panel with only the lock, built from the game's frame art, like the Gilded Spindle.
- The lock art is new pixel art in setoa.dat, shipped in the same launcher patch as the client. There is no code-drawn fallback.
- The server keeps the sweet spot and judges each turn (approach 1). The client never learns the sweet spot or the wear.
- Item chests stay close to today's 1-in-4: the pick breaks on the second jam.
- A success keeps the pick (today's 36% break-on-success chance is removed).
- One turn per key press. Holding the key does not repeat.

---

### Task 0: Create the worktrees

**Goal:** Three `feat/lockpick-window` worktrees exist, and the client builds against the server worktree.

**Files:**
- Create: the three worktree folders (no source changes)

**Acceptance Criteria:**
- [ ] SERVER is at 2821f83f7 on `feat/lockpick-window`
- [ ] CLIENT is at client `main`'s head on `feat/lockpick-window`
- [ ] UNORA is at Unora `main`'s head on `feat/lockpick-window`
- [ ] `dotnet build Chaos.Client.slnx` in CLIENT prints `0 Error(s)` with `UnoraServerPath` set to SERVER

**Verify:** `git -C <each worktree> log --oneline -1` shows those heads, and the client build prints `0 Error(s)`.

**Steps:**

- [ ] **Step 1: Create the worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/lockpick-server -b feat/lockpick-window 2821f83f7
git -C Chaos.Client worktree add /c/Users/Michael/Documents/GitHub/worktrees/lockpick-client -b feat/lockpick-window main
git -C Unora -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/lockpick-unora -b feat/lockpick-window main
```

- [ ] **Step 2: Check the heads**

Run: `for w in lockpick-server lockpick-client lockpick-unora; do git -C /c/Users/Michael/Documents/GitHub/worktrees/$w log --oneline -1; done`
Expected: `2821f83f7 Merge branch 'fix/dialog-blocks-actions'`, then the client and Unora `main` heads.

- [ ] **Step 3: Build the client against SERVER**

Stop any running client or `Chaos.exe` first (ask the user). The CLIENT worktree's own `Chaos-Server` folder stays empty. `UnoraServerPath` points the build at SERVER instead.

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/lockpick-client
UnoraServerPath=/c/Users/Michael/Documents/GitHub/worktrees/lockpick-server dotnet build Chaos.Client.slnx
```

Expected: `0 Error(s)`.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server log --oneline -1", "acceptanceCriteria": ["SERVER at 2821f83f7 on feat/lockpick-window", "CLIENT and UNORA at their main heads on feat/lockpick-window", "client builds against SERVER with 0 errors"], "modelTier": "mechanical"}
```

---

### Task 1: Lockpick messages

**Goal:** The two lockpick packets exist, with their enums and opcodes, and both round-trip through their converters.

**Files:**
- Modify: `SERVER/Chaos.DarkAges/Definitions/Enums.cs` (four enums after `WheelRejectReason`)
- Modify: `SERVER/Chaos.Networking.Abstractions/Definitions/Enums.cs` (`ClientOpCode.LockpickInteraction = 131`, `ServerOpCode.LockpickDisplay = 135`)
- Create: `SERVER/Chaos.Networking/Entities/Client/LockpickInteractionArgs.cs`
- Create: `SERVER/Chaos.Networking/Converters/Client/LockpickInteractionConverter.cs`
- Create: `SERVER/Chaos.Networking/Entities/Server/LockpickDisplayArgs.cs`
- Create: `SERVER/Chaos.Networking/Converters/Server/LockpickDisplayConverter.cs`
- Test: `SERVER/Tests/Chaos.Tests/Networking/LockpickPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] Turn and Close interactions round-trip
- [ ] Open, TurnResult and Close displays round-trip, with a null title or reason read back as empty
- [ ] An unknown interaction type or display type throws `ArgumentOutOfRangeException` on read, and an unknown display type throws on write

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickPacketConverterTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Check the opcodes are still free**

Run the opcode check from Global Constraints. Expected: no output. If 131 or 135 is taken, stop and ask the coordinator.

- [ ] **Step 2: Add the enums**

In `SERVER/Chaos.DarkAges/Definitions/Enums.cs`, directly after the closing brace of `public enum WheelRejectReason`, insert:

```csharp

/// <summary>How hard a lock is to pick. Sets the lockpick window's title and colour.</summary>
public enum LockpickDifficulty : byte
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}

/// <summary>What the player did in the lockpick window.</summary>
public enum LockpickInteractionType : byte
{
    /// <summary>One attempt to turn the lock, with the pick at the sent angle.</summary>
    Turn = 0,

    /// <summary>The player closed the window.</summary>
    Close = 1
}

/// <summary>Which lockpick window update the server is sending.</summary>
public enum LockpickDisplayType : byte
{
    Open = 0,
    TurnResult = 1,
    Close = 2
}

/// <summary>What happened when the lock was turned.</summary>
public enum LockpickTurnOutcome : byte
{
    /// <summary>The lock stopped short, and the pick took one jam of wear.</summary>
    Jammed = 0,

    /// <summary>The pick was in the sweet spot, and the lock opened.</summary>
    Opened = 1,

    /// <summary>The lock stopped short, and the pick broke.</summary>
    Broke = 2
}
```

- [ ] **Step 3: Add the opcodes**

In `SERVER/Chaos.Networking.Abstractions/Definitions/Enums.cs`, after `EmblemChoice = 130,` in `ClientOpCode`, insert:

```csharp

    /// <summary>
    ///     A turn or a close in the lockpick window. Carries no chest id: the server uses the chest this player is
    ///     picking.
    ///     <br />
    ///     Hex value: 0x83
    /// </summary>
    LockpickInteraction = 131,
```

After `EmblemBook = 134,` in `ServerOpCode`, insert:

```csharp

    /// <summary>
    ///     Opens, updates or closes the lockpick window. Never carries the lock's sweet spot or the pick's wear.
    ///     <br />
    ///     Hex value: 0x87
    /// </summary>
    LockpickDisplay = 135,
```

- [ ] **Step 4: Write the failing tests**

Create `SERVER/Tests/Chaos.Tests/Networking/LockpickPacketConverterTests.cs`:

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

public class LockpickPacketConverterTests
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

    [Test]
    public async Task Interaction_round_trips_for_Turn()
    {
        var original = new LockpickInteractionArgs { Type = LockpickInteractionType.Turn, PickDegrees = 137 };

        RoundTrip(new LockpickInteractionConverter(), original)
            .Should()
            .BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Interaction_round_trips_for_Close()
    {
        var original = new LockpickInteractionArgs { Type = LockpickInteractionType.Close };

        RoundTrip(new LockpickInteractionConverter(), original)
            .Should()
            .BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Display_round_trips_for_Open()
    {
        var original = new LockpickDisplayArgs
        {
            Type = LockpickDisplayType.Open,
            Difficulty = LockpickDifficulty.Hard,
            Title = "Hard Lock",
            LockpickCount = 1234
        };

        RoundTrip(new LockpickDisplayConverter(), original)
            .Should()
            .BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Display_round_trips_for_TurnResult()
    {
        var original = new LockpickDisplayArgs
        {
            Type = LockpickDisplayType.TurnResult,
            Outcome = LockpickTurnOutcome.Jammed,
            TurnPercent = 73,
            LockpickCount = 4
        };

        RoundTrip(new LockpickDisplayConverter(), original)
            .Should()
            .BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Display_round_trips_for_Close()
    {
        var original = new LockpickDisplayArgs { Type = LockpickDisplayType.Close, Reason = "You moved away from the chest." };

        RoundTrip(new LockpickDisplayConverter(), original)
            .Should()
            .BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Display_reads_a_null_title_and_reason_back_as_empty()
    {
        var open = RoundTrip(new LockpickDisplayConverter(), new LockpickDisplayArgs { Type = LockpickDisplayType.Open });
        var close = RoundTrip(new LockpickDisplayConverter(), new LockpickDisplayArgs { Type = LockpickDisplayType.Close });

        open.Title
            .Should()
            .BeEmpty();

        close.Reason
             .Should()
             .BeEmpty();

        await Task.CompletedTask;
    }

    // SpanReader/SpanWriter are ref structs, so they cannot be captured by a lambda for FluentAssertions'
    // Should().Throw(). The tests below call the converter inside a try/catch instead.

    [Test]
    public async Task Deserialize_throws_for_unknown_interaction_type()
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        writer.WriteByte(99);
        writer.WriteByte(0);
        var bytes = writer.ToSpan()
                          .ToArray();

        Exception? caught = null;

        try
        {
            var reader = new SpanReader(Enc, bytes);
            new LockpickInteractionConverter().Deserialize(ref reader);
        } catch (Exception ex)
        {
            caught = ex;
        }

        caught.Should()
              .BeOfType<ArgumentOutOfRangeException>();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Deserialize_throws_for_unknown_display_type()
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        writer.WriteByte(99);
        var bytes = writer.ToSpan()
                          .ToArray();

        Exception? caught = null;

        try
        {
            var reader = new SpanReader(Enc, bytes);
            new LockpickDisplayConverter().Deserialize(ref reader);
        } catch (Exception ex)
        {
            caught = ex;
        }

        caught.Should()
              .BeOfType<ArgumentOutOfRangeException>();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Serialize_throws_for_unknown_display_type()
    {
        Exception? caught = null;

        try
        {
            var writer = new SpanWriter(Enc, usePooling: false);
            new LockpickDisplayConverter().Serialize(ref writer, new LockpickDisplayArgs { Type = (LockpickDisplayType)99 });
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

- [ ] **Step 5: Run the tests to see them fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickPacketConverterTests/*"`
Expected: a build error, because `LockpickInteractionArgs` and the other new types don't exist yet.

- [ ] **Step 6: Write the args and converters**

`SERVER/Chaos.Networking/Entities/Client/LockpickInteractionArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>
///     Represents the serialization of the <see cref="ClientOpCode.LockpickInteraction" /> packet.
/// </summary>
/// <remarks>
///     Carries no chest id. The server uses the chest whose lock session this player holds, so a modified client
///     cannot aim a turn at another chest.
/// </remarks>
public sealed record LockpickInteractionArgs : IPacketSerializable
{
    /// <summary>Turn or Close.</summary>
    public required LockpickInteractionType Type { get; set; }

    /// <summary>The pick's angle, 0 (left) to 180 (right). Only read for a Turn.</summary>
    public byte PickDegrees { get; set; }
}
```

`SERVER/Chaos.Networking/Converters/Client/LockpickInteractionConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>
///     Provides packet serialization and deserialization logic for <see cref="LockpickInteractionArgs" />
/// </summary>
public sealed class LockpickInteractionConverter : PacketConverterBase<LockpickInteractionArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.LockpickInteraction;

    /// <inheritdoc />
    /// <remarks>
    ///     Rejects an undefined type rather than casting it through, like <see cref="WheelInteractionConverter" />:
    ///     the handler treats everything that is not Close as a turn.
    /// </remarks>
    public override LockpickInteractionArgs Deserialize(ref SpanReader reader)
    {
        var type = (LockpickInteractionType)reader.ReadByte();

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown lockpick interaction type");

        return new LockpickInteractionArgs
        {
            Type = type,
            PickDegrees = reader.ReadByte()
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, LockpickInteractionArgs args)
    {
        writer.WriteByte((byte)args.Type);
        writer.WriteByte(args.PickDegrees);
    }
}
```

`SERVER/Chaos.Networking/Entities/Server/LockpickDisplayArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.LockpickDisplay" /> packet.
///     <see cref="Type" /> selects the payload. The other fields apply to specific display types.
/// </summary>
/// <remarks>The lock's sweet spot and the pick's wear are never sent.</remarks>
public sealed record LockpickDisplayArgs : IPacketSerializable
{
    /// <summary>Open, TurnResult or Close.</summary>
    public required LockpickDisplayType Type { get; set; }

    // --- Open ---

    /// <summary>The lock's difficulty, which sets the title colour.</summary>
    public LockpickDifficulty Difficulty { get; set; }

    /// <summary>The window title, e.g. "Hard Lock".</summary>
    public string? Title { get; set; }

    // --- Open and TurnResult ---

    /// <summary>How many lockpicks the rogue has now.</summary>
    public ushort LockpickCount { get; set; }

    // --- TurnResult ---

    /// <summary>What the turn did.</summary>
    public LockpickTurnOutcome Outcome { get; set; }

    /// <summary>How far the lock turned before it stopped, 0-100. 100 when it opened.</summary>
    public byte TurnPercent { get; set; }

    // --- Close ---

    /// <summary>Why the window closed. May be empty.</summary>
    public string? Reason { get; set; }
}
```

`SERVER/Chaos.Networking/Converters/Server/LockpickDisplayConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>
///     Provides packet serialization and deserialization logic for <see cref="LockpickDisplayArgs" />
/// </summary>
public sealed class LockpickDisplayConverter : PacketConverterBase<LockpickDisplayArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.LockpickDisplay;

    /// <inheritdoc />
    public override LockpickDisplayArgs Deserialize(ref SpanReader reader)
    {
        var type = (LockpickDisplayType)reader.ReadByte();

        switch (type)
        {
            case LockpickDisplayType.Open:
                return new LockpickDisplayArgs
                {
                    Type = type,
                    Difficulty = (LockpickDifficulty)reader.ReadByte(),
                    Title = reader.ReadString8(),
                    LockpickCount = reader.ReadUInt16()
                };

            case LockpickDisplayType.TurnResult:
                return new LockpickDisplayArgs
                {
                    Type = type,
                    Outcome = (LockpickTurnOutcome)reader.ReadByte(),
                    TurnPercent = reader.ReadByte(),
                    LockpickCount = reader.ReadUInt16()
                };

            case LockpickDisplayType.Close:
                return new LockpickDisplayArgs
                {
                    Type = type,
                    Reason = reader.ReadString8()
                };

            default:
                throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown lockpick display type");
        }
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, LockpickDisplayArgs args)
    {
        writer.WriteByte((byte)args.Type);

        switch (args.Type)
        {
            case LockpickDisplayType.Open:
                writer.WriteByte((byte)args.Difficulty);
                writer.WriteString8(args.Title ?? string.Empty);
                writer.WriteUInt16(args.LockpickCount);

                break;

            case LockpickDisplayType.TurnResult:
                writer.WriteByte((byte)args.Outcome);
                writer.WriteByte(args.TurnPercent);
                writer.WriteUInt16(args.LockpickCount);

                break;

            case LockpickDisplayType.Close:
                writer.WriteString8(args.Reason ?? string.Empty);

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(args), args.Type, "Unknown lockpick display type");
        }
    }
}
```

Converters are found by assembly scan (`PacketExtensions.LoadConvertersFromAssembly`), so nothing else needs registering.

- [ ] **Step 7: Run the tests to see them pass**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickPacketConverterTests/*"`
Expected: 9 passed, 0 failed.

```json:metadata
{"files": ["Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs", "Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos-Server/Chaos.Networking/Entities/Client/LockpickInteractionArgs.cs", "Chaos-Server/Chaos.Networking/Converters/Client/LockpickInteractionConverter.cs", "Chaos-Server/Chaos.Networking/Entities/Server/LockpickDisplayArgs.cs", "Chaos-Server/Chaos.Networking/Converters/Server/LockpickDisplayConverter.cs", "Chaos-Server/Tests/Chaos.Tests/Networking/LockpickPacketConverterTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/LockpickPacketConverterTests/*\"", "acceptanceCriteria": ["Turn and Close interactions round-trip", "Open, TurnResult and Close displays round-trip; null title/reason read back empty", "unknown interaction or display type throws ArgumentOutOfRangeException"], "modelTier": "standard"}
```

---

### Task 2: Lock engine and tuning

**Goal:** `LockpickLock` decides a turn from the pick angle alone, `LockpickTuning` holds both tuning sets, and a simulation test holds item chests near 1 in 4.

**Files:**
- Create: `SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/LockpickTuning.cs`
- Create: `SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/LockpickLock.cs`
- Test: `SERVER/Tests/Chaos.Tests/Lockpicking/LockpickLockTests.cs`
- Test: `SERVER/Tests/Chaos.Tests/Lockpicking/LockpickOddsTests.cs`

**Acceptance Criteria:**
- [ ] A pick within width/2 of the sweet spot opens the lock at 100% and costs no wear
- [ ] Turn percent is 97 one degree past the edge (width 12, warm 45), falls as the pick moves away, and is 5 at and past the warm range
- [ ] The pick breaks on jam number `JamsToBreak` and not before
- [ ] Angles outside 0–180 are clamped
- [ ] `Roll` keeps the whole sweet spot inside 0–180
- [ ] The simulated sensible player opens 20–30% of item chests at each difficulty

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickLockTests/*"` and the same with `LockpickOddsTests` → all pass

**Steps:**

- [ ] **Step 1: Write the failing lock tests**

Create `SERVER/Tests/Chaos.Tests/Lockpicking/LockpickLockTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Scripting.MerchantScripts.Lockpicking;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Lockpicking;

public sealed class LockpickLockTests
{
    private static LockpickLock Create(int sweet = 90, int width = 12, int warm = 45, int jams = 5)
        => new(LockpickDifficulty.Medium, new LockpickTuning(width, warm, jams), sweet);

    //formatter:off
    [Test]
    [Arguments(90)]
    [Arguments(84)]
    [Arguments(96)]
    //formatter:on
    public void Turn_ShouldOpen_InsideTheSweetSpot(int pick)
    {
        var @lock = Create();

        var turn = @lock.Turn(pick);

        turn.Outcome
            .Should()
            .Be(LockpickTurnOutcome.Opened);

        turn.TurnPercent
            .Should()
            .Be(100);

        @lock.JamsTaken
             .Should()
             .Be(0);
    }

    [Test]
    public void Turn_ShouldBe97Percent_OneDegreePastTheEdge()
    {
        //width 12 -> edge at 96; 97 is 1 degree past it: 99 - 1/45*94 = 96.9 -> 97
        Create()
            .Turn(97)
            .Should()
            .Be(new LockpickTurn(LockpickTurnOutcome.Jammed, 97));
    }

    [Test]
    public void Turn_ShouldFall_AsThePickMovesAway()
    {
        var near = Create().Turn(100).TurnPercent;
        var middle = Create().Turn(115).TurnPercent;
        var far = Create().Turn(130).TurnPercent;

        near.Should()
            .BeGreaterThan(middle);

        middle.Should()
              .BeGreaterThan(far);
    }

    //formatter:off
    [Test]
    [Arguments(141)]
    [Arguments(180)]
    [Arguments(0)]
    //formatter:on
    public void Turn_ShouldBeTheMinimum_AtAndPastTheWarmRange(int pick)
    {
        //the edge is at 96, and the warm range ends 45 later at 141
        Create()
            .Turn(pick)
            .TurnPercent
            .Should()
            .Be(LockpickLock.MIN_TURN_PERCENT);
    }

    [Test]
    public void Turn_ShouldBreakThePick_OnTheLastJam()
    {
        var @lock = Create(jams: 3);

        @lock.Turn(0).Outcome.Should().Be(LockpickTurnOutcome.Jammed);
        @lock.Turn(0).Outcome.Should().Be(LockpickTurnOutcome.Jammed);
        @lock.Turn(0).Outcome.Should().Be(LockpickTurnOutcome.Broke);

        @lock.JamsTaken
             .Should()
             .Be(3);
    }

    [Test]
    public void Turn_ShouldBreakOnTheSecondJam_ForItemChests()
    {
        var @lock = new LockpickLock(LockpickDifficulty.Hard, LockpickTuning.ItemChest(LockpickDifficulty.Hard), 90);

        @lock.Turn(0).Outcome.Should().Be(LockpickTurnOutcome.Jammed);
        @lock.Turn(0).Outcome.Should().Be(LockpickTurnOutcome.Broke);
    }

    //formatter:off
    [Test]
    [Arguments(-40, 0)]
    [Arguments(400, 180)]
    //formatter:on
    public void Turn_ShouldClampTheAngle(int pick, int clamped)
    {
        var @lock = Create(sweet: clamped == 0 ? 6 : 174);

        @lock.Turn(pick)
             .Outcome
             .Should()
             .Be(LockpickTurnOutcome.Opened);
    }

    [Test]
    public void Roll_ShouldKeepTheWholeSweetSpotInsideTheArc()
    {
        var random = new Random(7);
        var tuning = new LockpickTuning(20, 60, 6);

        for (var i = 0; i < 2000; i++)
            LockpickLock.Roll(LockpickDifficulty.Easy, tuning, random)
                        .SweetSpotDegrees
                        .Should()
                        .BeInRange(10, 170);
    }

    [Test]
    public void Tuning_ShouldMatchTheAgreedNumbers()
    {
        LockpickTuning.Crypt(LockpickDifficulty.Easy).Should().Be(new LockpickTuning(20, 60, 6));
        LockpickTuning.Crypt(LockpickDifficulty.Medium).Should().Be(new LockpickTuning(12, 45, 5));
        LockpickTuning.Crypt(LockpickDifficulty.Hard).Should().Be(new LockpickTuning(6, 30, 4));
        LockpickTuning.ItemChest(LockpickDifficulty.Easy).JamsToBreak.Should().Be(2);
        LockpickTuning.ItemChest(LockpickDifficulty.Medium).JamsToBreak.Should().Be(2);
        LockpickTuning.ItemChest(LockpickDifficulty.Hard).JamsToBreak.Should().Be(2);
    }
}
```

- [ ] **Step 2: Write the failing odds test**

Create `SERVER/Tests/Chaos.Tests/Lockpicking/LockpickOddsTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Scripting.MerchantScripts.Lockpicking;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Lockpicking;

/// <summary>
///     Simulates a sensible player to hold item chests near the old 1-in-4 odds. The player probes straight up,
///     reads the distance to the sweet spot from how far the lock turned, and tries one side of the probe at that
///     distance. Past the warm range the lock turns only the minimum, so all they learn is "far away", and they
///     jump well clear of the probe. If an item-chest difficulty leaves the 20-30% band, change that difficulty's
///     width and warm range in LockpickTuning.ItemChest -- never its jam count.
/// </summary>
public sealed class LockpickOddsTests
{
    private const int TRIALS = 10_000;

    //formatter:off
    [Test]
    [Arguments(LockpickDifficulty.Easy)]
    [Arguments(LockpickDifficulty.Medium)]
    [Arguments(LockpickDifficulty.Hard)]
    //formatter:on
    public void ItemChests_ShouldOpenNearOneInFour_ForASensiblePlayer(LockpickDifficulty difficulty)
    {
        var rate = SuccessRate(LockpickTuning.ItemChest(difficulty), 1);

        Console.WriteLine($"item chest {difficulty}: {rate:P1}");

        rate.Should()
            .BeInRange(0.20, 0.30);
    }

    //formatter:off
    [Test]
    [Arguments(LockpickDifficulty.Easy)]
    [Arguments(LockpickDifficulty.Medium)]
    [Arguments(LockpickDifficulty.Hard)]
    //formatter:on
    public void CryptChests_ArePrintedForTheRecord(LockpickDifficulty difficulty)
    {
        //no band: crypt chests reward skill. Planning simulated about 88% / 73% / 52%.
        var rate = SuccessRate(LockpickTuning.Crypt(difficulty), 1);

        Console.WriteLine($"crypt chest {difficulty}: {rate:P1}");

        rate.Should()
            .BeInRange(0.0, 1.0);
    }

    private static double SuccessRate(LockpickTuning tuning, int seed)
    {
        var random = new Random(seed);
        var half = tuning.SweetSpotWidth / 2;
        var wins = 0;

        for (var i = 0; i < TRIALS; i++)
        {
            var @lock = LockpickLock.Roll(LockpickDifficulty.Medium, tuning, random);
            var pick = 90;

            while (true)
            {
                var turn = @lock.Turn(pick);

                if (turn.Outcome == LockpickTurnOutcome.Opened)
                {
                    wins++;

                    break;
                }

                if (turn.Outcome == LockpickTurnOutcome.Broke)
                    break;

                pick = NextPick(pick, turn.TurnPercent, tuning, half, random);
            }
        }

        return (double)wins / TRIALS;
    }

    private static int NextPick(int pick, byte turnPercent, LockpickTuning tuning, int half, Random random)
    {
        const int MAX = LockpickLock.MAX_DEGREES;

        if (turnPercent > LockpickLock.MIN_TURN_PERCENT)
        {
            var distance = half + ((99.0 - turnPercent) / (99 - LockpickLock.MIN_TURN_PERCENT) * tuning.WarmRange);

            var candidates = new[] { (int)Math.Round(pick - distance), (int)Math.Round(pick + distance) }
                             .Where(c => (c >= half) && (c <= (MAX - half)))
                             .ToArray();

            return candidates.Length == 0 ? pick : candidates[random.Next(candidates.Length)];
        }

        var jump = tuning.WarmRange + half + 20;
        int[] far = [Math.Max(half, pick - jump), Math.Min(MAX - half, pick + jump)];

        return far[random.Next(far.Length)];
    }
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickLockTests/*"`
Expected: a build error, because `LockpickLock` doesn't exist yet.

- [ ] **Step 4: Write the tuning and the lock**

`SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/LockpickTuning.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Scripting.MerchantScripts.Lockpicking;

/// <summary>
///     The numbers behind one lock. <see cref="SweetSpotWidth" /> is the full width of the sweet spot in degrees.
///     <see cref="WarmRange" /> is how far past each edge of it the lock still turns partway. The pick breaks on
///     jam number <see cref="JamsToBreak" />.
/// </summary>
public sealed record LockpickTuning(int SweetSpotWidth, int WarmRange, int JamsToBreak)
{
    /// <summary>Crypt chests, which pay gold. Skill decides: a careful rogue opens most of them.</summary>
    public static LockpickTuning Crypt(LockpickDifficulty difficulty)
        => difficulty switch
        {
            LockpickDifficulty.Easy   => new LockpickTuning(20, 60, 6),
            LockpickDifficulty.Medium => new LockpickTuning(12, 45, 5),
            LockpickDifficulty.Hard   => new LockpickTuning(6, 30, 4),
            _                         => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null)
        };

    /// <summary>
    ///     Deep Crypt and Asilon Prairie chests, which pay items. The pick breaks on the second jam, so a rogue gets
    ///     one probe and one informed guess. The widths and warm ranges hold a sensible player near the old 1-in-4
    ///     odds; LockpickOddsTests checks that.
    /// </summary>
    public static LockpickTuning ItemChest(LockpickDifficulty difficulty)
        => difficulty switch
        {
            LockpickDifficulty.Easy   => new LockpickTuning(12, 20, 2),
            LockpickDifficulty.Medium => new LockpickTuning(12, 15, 2),
            LockpickDifficulty.Hard   => new LockpickTuning(6, 30, 2),
            _                         => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null)
        };
}
```

`SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/LockpickLock.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Scripting.MerchantScripts.Lockpicking;

/// <summary>The result of one turn: what happened, and how far the lock turned (0-100).</summary>
public readonly record struct LockpickTurn(LockpickTurnOutcome Outcome, byte TurnPercent);

/// <summary>
///     One chest's lock. The sweet spot is a hidden angle along the top of the lock, 0 (left) to 180 (right). A turn
///     uses no random numbers, so the same lock always answers the same pick the same way.
/// </summary>
public sealed class LockpickLock
{
    public const int MAX_DEGREES = 180;
    public const byte MIN_TURN_PERCENT = 5;
    private const byte MAX_JAMMED_PERCENT = 99;

    public LockpickDifficulty Difficulty { get; }
    public LockpickTuning Tuning { get; }
    public int SweetSpotDegrees { get; }

    /// <summary>How many jams the current pick has taken. Kept by the lock, so closing the window does not reset it.</summary>
    public int JamsTaken { get; private set; }

    public LockpickLock(LockpickDifficulty difficulty, LockpickTuning tuning, int sweetSpotDegrees)
    {
        Difficulty = difficulty;
        Tuning = tuning;
        SweetSpotDegrees = sweetSpotDegrees;
    }

    /// <summary>Builds a lock with a random sweet spot that fits wholly inside 0-180.</summary>
    public static LockpickLock Roll(LockpickDifficulty difficulty, LockpickTuning tuning, Random random)
    {
        var half = tuning.SweetSpotWidth / 2;

        return new LockpickLock(difficulty, tuning, random.Next(half, MAX_DEGREES - half + 1));
    }

    /// <summary>
    ///     Tries to turn the lock with the pick at <paramref name="pickDegrees" />. Inside the sweet spot it opens and
    ///     costs no wear. Otherwise the pick takes one jam, and it breaks on jam number
    ///     <see cref="LockpickTuning.JamsToBreak" />.
    /// </summary>
    public LockpickTurn Turn(int pickDegrees)
    {
        var pick = Math.Clamp(pickDegrees, 0, MAX_DEGREES);
        var pastEdge = Math.Abs(pick - SweetSpotDegrees) - (Tuning.SweetSpotWidth / 2.0);

        if (pastEdge <= 0)
            return new LockpickTurn(LockpickTurnOutcome.Opened, 100);

        JamsTaken++;

        var outcome = JamsTaken >= Tuning.JamsToBreak ? LockpickTurnOutcome.Broke : LockpickTurnOutcome.Jammed;

        return new LockpickTurn(outcome, TurnPercentFor(pastEdge));
    }

    //99% just past the edge, falling in a straight line to the minimum at the far edge of the warm range
    private byte TurnPercentFor(double pastEdge)
    {
        if (pastEdge >= Tuning.WarmRange)
            return MIN_TURN_PERCENT;

        var percent = MAX_JAMMED_PERCENT - (pastEdge / Tuning.WarmRange * (MAX_JAMMED_PERCENT - MIN_TURN_PERCENT));

        return (byte)Math.Round(percent, MidpointRounding.AwayFromZero);
    }
}
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickLockTests/*"`, then the same with `LockpickOddsTests`.
Expected: all pass. The odds test output shows item chests near 27% / 24% / 25%. If an item difficulty falls outside 20–30%, change only that difficulty's width or warm range in `LockpickTuning.ItemChest`. Then update the item tuning line in this plan's Global Constraints and the spec amendment to match.

```json:metadata
{"files": ["Chaos-Server/Chaos/Scripting/MerchantScripts/Lockpicking/LockpickTuning.cs", "Chaos-Server/Chaos/Scripting/MerchantScripts/Lockpicking/LockpickLock.cs", "Chaos-Server/Tests/Chaos.Tests/Lockpicking/LockpickLockTests.cs", "Chaos-Server/Tests/Chaos.Tests/Lockpicking/LockpickOddsTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/LockpickLockTests/*\"", "acceptanceCriteria": ["inside width/2 opens at 100% with no wear", "97% one degree past the edge, falling, 5% at/past warm range", "breaks on jam JamsToBreak", "angles clamped", "Roll keeps sweet spot inside 0-180", "item chests 20-30% for the simulated player"], "modelTier": "standard"}
```

---

### Task 3: Lock session on the server

**Goal:** `LockpickChest` runs one rogue's session on a chest: it starts it, judges turns, ends it for the listed reasons, and sends the three lockpick displays.

**Files:**
- Create: `SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/LockpickChest.cs`
- Create: `SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/ILockpickChestScript.cs`
- Modify: `SERVER/Chaos/Networking/Abstractions/IChaosWorldClient.cs` (three `SendLockpick*` methods after `SendWheelClose`)
- Modify: `SERVER/Chaos/Networking/ChaosWorldClient.cs` (their implementations after `SendWheelClose`)
- Modify: `SERVER/Tests/Chaos.Testing.Infrastructure/Mocks/MockMerchant.cs` (optional template setup and script factory)
- Test: `SERVER/Tests/Chaos.Tests/Lockpicking/LockpickChestTests.cs`

**Acceptance Criteria:**
- [ ] `TryBegin` opens the window only for a rogue with lockpicks within 1 tile of a chest that is on the map and not held by someone else, and sends the exact refusal message otherwise
- [ ] An Opened turn sends TurnResult(Opened, 100), runs the prize callback while the chest is still on the map, then removes the chest, and the pick is kept
- [ ] A Broke turn removes one lockpick, sends "Your lockpick broke!" and TurnResult(Broke), runs the break callback, and removes the chest
- [ ] Turns from another player, and turns less than 400 ms after the last one, are ignored
- [ ] `Update` ends the session with the right Close reason when the picker walks away or the chest is gone, silently when the picker leaves the map, and after 60 s idle
- [ ] Wear survives closing and reopening the window

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickChestTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Add the send methods**

In `SERVER/Chaos/Networking/Abstractions/IChaosWorldClient.cs`, after `void SendWheelClose();`, insert:

```csharp

    /// <summary>Opens the lockpick window.</summary>
    void SendLockpickOpen(LockpickDifficulty difficulty, string title, ushort lockpickCount);

    /// <summary>Tells the lockpick window how far the lock turned and what happened.</summary>
    void SendLockpickTurnResult(LockpickTurnOutcome outcome, byte turnPercent, ushort lockpickCount);

    /// <summary>Closes the lockpick window. A non-empty <paramref name="reason" /> is shown briefly first.</summary>
    void SendLockpickClose(string reason);
```

In `SERVER/Chaos/Networking/ChaosWorldClient.cs`, after the `SendWheelClose` method, insert:

```csharp

    /// <inheritdoc />
    public void SendLockpickOpen(LockpickDifficulty difficulty, string title, ushort lockpickCount)
        => Send(
            new LockpickDisplayArgs
            {
                Type = LockpickDisplayType.Open,
                Difficulty = difficulty,
                Title = title,
                LockpickCount = lockpickCount
            });

    /// <inheritdoc />
    public void SendLockpickTurnResult(LockpickTurnOutcome outcome, byte turnPercent, ushort lockpickCount)
        => Send(
            new LockpickDisplayArgs
            {
                Type = LockpickDisplayType.TurnResult,
                Outcome = outcome,
                TurnPercent = turnPercent,
                LockpickCount = lockpickCount
            });

    /// <inheritdoc />
    public void SendLockpickClose(string reason)
        => Send(
            new LockpickDisplayArgs
            {
                Type = LockpickDisplayType.Close,
                Reason = reason
            });
```

Both files already import `Chaos.DarkAges.Definitions` and `Chaos.Networking.Entities.Server` for the wheel methods. Check the using block and add either one if it is missing.

- [ ] **Step 2: Let tests give a merchant a real script**

`Merchant.Script` is get-only and is created inside the `Merchant` constructor. So a test can only give a chest a real script through the script provider. In `SERVER/Tests/Chaos.Testing.Infrastructure/Mocks/MockMerchant.cs`:

1. Add `using Chaos.Scripting.Abstractions;` and `using Chaos.Scripting.MerchantScripts.Abstractions;` to the using block.
2. Replace the `Create` signature and body with:

```csharp
    public static Merchant Create(
        MapInstance? mapInstance = null,
        string? name = null,
        Action<Merchant>? setup = null,
        Action<MerchantTemplate>? configureTemplate = null,
        Func<Merchant, IMerchantScript>? scriptFactory = null)
    {
        mapInstance ??= MockMapInstance.Create();
        name ??= $"TestMerchant{Interlocked.Increment(ref Counter)}";

        var template = new MerchantTemplate
        {
            Name = name,
            TemplateKey = name.ToLowerInvariant(),
            Sprite = 1,
            WanderIntervalMs = 1000,
            DefaultStock = new Dictionary<string, int>(),
            ItemsForSale = new CounterCollection(),
            ItemsToBuy = [],
            SkillsToTeach = [],
            SpellsToTeach = [],
            RestockIntervalHrs = 1,
            RestockPct = 100,
            ScriptKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ScriptVars = new Dictionary<string, IScriptVars>(StringComparer.OrdinalIgnoreCase)
        };

        //runs before the merchant is built, because a configurable script reads its scriptVars in its constructor
        configureTemplate?.Invoke(template);

        var scriptProvider = MockScriptProvider.Instance.Object;

        if (scriptFactory is not null)
        {
            var providerMock = new Mock<IScriptProvider>();

            providerMock.Setup(p => p.CreateScript<IMerchantScript, Merchant>(It.IsAny<ICollection<string>>(), It.IsAny<Merchant>()))
                        .Returns((ICollection<string> _, Merchant merchant) => scriptFactory(merchant));

            scriptProvider = providerMock.Object;
        }

        var loggerMock = new Mock<ILogger<Merchant>>();
        var skillFactoryMock = new Mock<ISkillFactory>();
        var spellFactoryMock = new Mock<ISpellFactory>();
        var itemFactoryMock = new Mock<IItemFactory>();
        var stockServiceMock = new Mock<IStockService>();

        var merchant = new Merchant(
            template,
            mapInstance,
            new Point(5, 5),
            loggerMock.Object,
            skillFactoryMock.Object,
            spellFactoryMock.Object,
            itemFactoryMock.Object,
            stockServiceMock.Object,
            scriptProvider);

        setup?.Invoke(merchant);

        return merchant;
    }
```

The new parameters are optional and come last, so existing callers don't change.

- [ ] **Step 3: Write the failing tests**

Create `SERVER/Tests/Chaos.Tests/Lockpicking/LockpickChestTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.Scripting.MerchantScripts.Lockpicking;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Moq;
#endregion

namespace Chaos.Tests.Lockpicking;

public sealed class LockpickChestTests
{
    private static readonly TimeSpan PastCooldown = TimeSpan.FromMilliseconds(450);
    private readonly MapInstance Map = MockMapInstance.Create();

    private Aisling AddRogue(Point position, int lockpicks = 3, string? name = null)
    {
        var rogue = MockAisling.Create(Map, name);
        Map.AddEntity(rogue, position);
        rogue.UserStatSheet.SetBaseClass(BaseClass.Rogue);

        if (lockpicks > 0)
            rogue.Inventory.TryAddToNextSlot(MockItem.Create("lockpicks", lockpicks, true));

        return rogue;
    }

    //sweet spot at 90, width 12 (84-96 opens), warm 45, and the pick breaks on the second jam
    private (LockpickChest Subject, Merchant Chest) CreateChest()
    {
        var chest = MockMerchant.Create(Map, "Chest");
        Map.AddEntity(chest, new Point(5, 6));

        var @lock = new LockpickLock(LockpickDifficulty.Medium, new LockpickTuning(12, 45, 2), 90);

        return (new LockpickChest(chest, @lock), chest);
    }

    private static Mock<IChaosWorldClient> ClientOf(Aisling aisling) => Mock.Get(aisling.Client);

    private static void Ignore(Aisling _) { }

    [Test]
    public void TryBegin_ShouldOpenTheWindow_ForARogueWithLockpicks()
    {
        var rogue = AddRogue(new Point(5, 5));
        var (subject, _) = CreateChest();

        subject.TryBegin(rogue, Ignore)
               .Should()
               .BeTrue();

        subject.IsPickedBy(rogue)
               .Should()
               .BeTrue();

        ClientOf(rogue).Verify(c => c.SendLockpickOpen(LockpickDifficulty.Medium, "Medium Lock", 3), Times.Once);
    }

    [Test]
    public void TryBegin_ShouldRefuse_ANonRogue()
    {
        var warrior = AddRogue(new Point(5, 5));
        warrior.UserStatSheet.SetBaseClass(BaseClass.Warrior);
        var (subject, _) = CreateChest();

        subject.TryBegin(warrior, Ignore)
               .Should()
               .BeFalse();

        ClientOf(warrior).Verify(c => c.SendServerMessage(It.IsAny<ServerMessageType>(), "Only a rogue could pick this lock."), Times.Once);
        ClientOf(warrior).Verify(c => c.SendLockpickOpen(It.IsAny<LockpickDifficulty>(), It.IsAny<string>(), It.IsAny<ushort>()), Times.Never);
    }

    [Test]
    public void TryBegin_ShouldRefuse_ARogueWithoutLockpicks()
    {
        var rogue = AddRogue(new Point(5, 5), 0);
        var (subject, _) = CreateChest();

        subject.TryBegin(rogue, Ignore)
               .Should()
               .BeFalse();

        ClientOf(rogue).Verify(c => c.SendServerMessage(It.IsAny<ServerMessageType>(), "You should get some lockpicks!"), Times.Once);
    }

    [Test]
    public void TryBegin_ShouldRefuse_ARogueTooFarAway()
    {
        var rogue = AddRogue(new Point(5, 2));
        var (subject, _) = CreateChest();

        subject.TryBegin(rogue, Ignore)
               .Should()
               .BeFalse();

        ClientOf(rogue).Verify(c => c.SendServerMessage(It.IsAny<ServerMessageType>(), "You are too far away from the chest!"), Times.Once);
    }

    [Test]
    public void TryBegin_ShouldRefuse_ASecondRogue()
    {
        var first = AddRogue(new Point(5, 5));
        var second = AddRogue(new Point(5, 7));
        var (subject, _) = CreateChest();

        subject.TryBegin(first, Ignore);

        subject.TryBegin(second, Ignore)
               .Should()
               .BeFalse();

        ClientOf(second).Verify(c => c.SendServerMessage(It.IsAny<ServerMessageType>(), "Someone is already working on this lock."), Times.Once);
    }

    [Test]
    public void HandleTurn_ShouldGiveThePrizeThenRemoveTheChest_WhenItOpens()
    {
        var rogue = AddRogue(new Point(5, 5));
        var (subject, chest) = CreateChest();
        var chestWasOnMapForThePrize = false;

        subject.TryBegin(rogue, _ => chestWasOnMapForThePrize = Map.GetEntities<Merchant>().Contains(chest));
        subject.HandleTurn(rogue, 90);

        chestWasOnMapForThePrize.Should()
                                .BeTrue();

        Map.GetEntities<Merchant>()
           .Should()
           .NotContain(chest);

        rogue.Inventory
             .CountOfByTemplateKey("lockpicks")
             .Should()
             .Be(3);

        ClientOf(rogue).Verify(c => c.SendLockpickTurnResult(LockpickTurnOutcome.Opened, 100, 3), Times.Once);

        subject.Picker
               .Should()
               .BeNull();
    }

    [Test]
    public void HandleTurn_ShouldJamThenBreak_UsingUpAPickAndTheChest()
    {
        var rogue = AddRogue(new Point(5, 5));
        var (subject, chest) = CreateChest();
        var broke = false;

        subject.TryBegin(rogue, Ignore, _ => broke = true);

        subject.HandleTurn(rogue, 0);
        ClientOf(rogue).Verify(c => c.SendLockpickTurnResult(LockpickTurnOutcome.Jammed, LockpickLock.MIN_TURN_PERCENT, 3), Times.Once);

        subject.Update(PastCooldown);
        subject.HandleTurn(rogue, 0);

        ClientOf(rogue).Verify(c => c.SendLockpickTurnResult(LockpickTurnOutcome.Broke, LockpickLock.MIN_TURN_PERCENT, 2), Times.Once);
        ClientOf(rogue).Verify(c => c.SendServerMessage(It.IsAny<ServerMessageType>(), "Your lockpick broke!"), Times.Once);

        broke.Should()
             .BeTrue();

        Map.GetEntities<Merchant>()
           .Should()
           .NotContain(chest);
    }

    [Test]
    public void HandleTurn_ShouldIgnoreATurn_InsideTheCooldown()
    {
        var rogue = AddRogue(new Point(5, 5));
        var (subject, _) = CreateChest();

        subject.TryBegin(rogue, Ignore);
        subject.HandleTurn(rogue, 0);
        subject.HandleTurn(rogue, 0);

        subject.Lock
               .JamsTaken
               .Should()
               .Be(1);
    }

    [Test]
    public void HandleTurn_ShouldIgnore_AnotherPlayer()
    {
        var rogue = AddRogue(new Point(5, 5));
        var other = AddRogue(new Point(5, 7));
        var (subject, _) = CreateChest();

        subject.TryBegin(rogue, Ignore);
        subject.HandleTurn(other, 90);

        subject.Lock
               .JamsTaken
               .Should()
               .Be(0);

        subject.IsPickedBy(rogue)
               .Should()
               .BeTrue();
    }

    [Test]
    public void Update_ShouldClose_WhenThePickerWalksAway()
    {
        var rogue = AddRogue(new Point(5, 5));
        var (subject, _) = CreateChest();

        subject.TryBegin(rogue, Ignore);
        rogue.WarpTo(new Point(5, 2));
        subject.Update(TimeSpan.FromMilliseconds(50));

        ClientOf(rogue).Verify(c => c.SendLockpickClose("You moved away from the chest."), Times.Once);

        subject.Picker
               .Should()
               .BeNull();
    }

    [Test]
    public void Update_ShouldClose_WhenTheChestIsGone()
    {
        var rogue = AddRogue(new Point(5, 5));
        var (subject, chest) = CreateChest();

        subject.TryBegin(rogue, Ignore);
        Map.RemoveEntity(chest);
        subject.Update(TimeSpan.FromMilliseconds(50));

        ClientOf(rogue).Verify(c => c.SendLockpickClose("The chest is gone."), Times.Once);
    }

    [Test]
    public void Update_ShouldEndSilently_WhenThePickerLeavesTheMap()
    {
        var rogue = AddRogue(new Point(5, 5));
        var (subject, _) = CreateChest();

        subject.TryBegin(rogue, Ignore);
        Map.RemoveEntity(rogue);
        subject.Update(TimeSpan.FromMilliseconds(50));

        ClientOf(rogue).Verify(c => c.SendLockpickClose(It.IsAny<string>()), Times.Never);

        subject.Picker
               .Should()
               .BeNull();
    }

    [Test]
    public void Update_ShouldClose_AfterSixtySecondsIdle()
    {
        var rogue = AddRogue(new Point(5, 5));
        var (subject, _) = CreateChest();

        subject.TryBegin(rogue, Ignore);
        subject.Update(TimeSpan.FromSeconds(59));
        ClientOf(rogue).Verify(c => c.SendLockpickClose(It.IsAny<string>()), Times.Never);

        subject.Update(TimeSpan.FromSeconds(1));
        ClientOf(rogue).Verify(c => c.SendLockpickClose("You lose your focus."), Times.Once);
    }

    [Test]
    public void Wear_ShouldSurvive_ClosingAndReopening()
    {
        var rogue = AddRogue(new Point(5, 5));
        var (subject, chest) = CreateChest();

        subject.TryBegin(rogue, Ignore);
        subject.HandleTurn(rogue, 0);
        subject.HandleClose(rogue);

        subject.TryBegin(rogue, Ignore);
        subject.Update(PastCooldown);
        subject.HandleTurn(rogue, 0);

        //two jams on a pick that breaks on the second
        Map.GetEntities<Merchant>()
           .Should()
           .NotContain(chest);
    }
}
```

`ServerMessageType` and `BaseClass` both live in `Chaos.DarkAges.Definitions`. `IChaosWorldClient` is in `Chaos.Networking.Abstractions`. `Dialog.Close` removes the dialog from `ActiveDialog`.

- [ ] **Step 4: Run the tests to see them fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickChestTests/*"`
Expected: a build error, because `LockpickChest` doesn't exist yet.

- [ ] **Step 5: Write the interface and the session class**

`SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/ILockpickChestScript.cs`:

```csharp
#region
using Chaos.Scripting.MerchantScripts.Abstractions;
#endregion

namespace Chaos.Scripting.MerchantScripts.Lockpicking;

/// <summary>
///     A chest's merchant script that owns a lock. The world server finds a player's lock session through this
///     with <c>Script.As&lt;ILockpickChestScript&gt;()</c>, the same way it finds a Gilded Spindle.
/// </summary>
public interface ILockpickChestScript : IMerchantScript
{
    LockpickChest Lockpick { get; }
}
```

`SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/LockpickChest.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Extensions;
using Chaos.Models.World;
#endregion

namespace Chaos.Scripting.MerchantScripts.Lockpicking;

/// <summary>
///     One chest's lock plus the rogue working on it. The chest's merchant script owns it, so it lives as long as
///     the chest. The server decides every turn. The client only animates what it is told.
/// </summary>
public sealed class LockpickChest
{
    public const string LOCKPICK_TEMPLATE_KEY = "lockpicks";
    public const int REACH = 1;
    private const int SOUND_RANGE = 8;
    private const byte OPEN_SOUND = 183;
    private const byte BREAK_SOUND = 184;

    public static readonly TimeSpan TurnCooldown = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    private readonly Merchant Chest;
    private Action<Aisling>? OnBroke;
    private Action<Aisling>? OnOpened;
    private TimeSpan SinceActivity;
    private TimeSpan SinceLastTurn;

    public LockpickLock Lock { get; }

    /// <summary>The rogue with the window open, or null.</summary>
    public Aisling? Picker { get; private set; }

    public string Title => $"{Lock.Difficulty} Lock";

    public LockpickChest(Merchant chest, LockpickLock @lock)
    {
        Chest = chest;
        Lock = @lock;
    }

    public bool IsPickedBy(Aisling aisling) => Picker is not null && Picker.Equals(aisling);

    /// <summary>
    ///     Opens the window for <paramref name="aisling" /> if they may pick this lock now. <paramref name="onOpened" />
    ///     gives the prize, and runs while the chest is still on the map. <paramref name="onBroke" /> runs after a pick
    ///     breaks, before the chest is removed.
    /// </summary>
    public bool TryBegin(Aisling aisling, Action<Aisling> onOpened, Action<Aisling>? onBroke = null)
    {
        if (!IsChestOnMap())
            return Refuse(aisling, "The chest has vanished already!");

        if (aisling.UserStatSheet.BaseClass != BaseClass.Rogue)
            return Refuse(aisling, "Only a rogue could pick this lock.");

        if (!aisling.Inventory.HasCountByTemplateKey(LOCKPICK_TEMPLATE_KEY, 1))
            return Refuse(aisling, "You should get some lockpicks!");

        if (!aisling.WithinRange(Chest, REACH))
            return Refuse(aisling, "You are too far away from the chest!");

        if (Picker is not null && !Picker.Equals(aisling))
            return Refuse(aisling, "Someone is already working on this lock.");

        Picker = aisling;
        OnOpened = onOpened;
        OnBroke = onBroke;
        SinceLastTurn = TurnCooldown;
        SinceActivity = TimeSpan.Zero;

        aisling.Client.SendLockpickOpen(Lock.Difficulty, Title, LockpickCount(aisling));

        return true;
    }

    /// <summary>Judges one turn from the picker. Anyone else's turn, or one inside the cooldown, is ignored.</summary>
    public void HandleTurn(Aisling aisling, int pickDegrees)
    {
        if (!IsPickedBy(aisling) || !CheckSession() || (SinceLastTurn < TurnCooldown))
            return;

        SinceLastTurn = TimeSpan.Zero;
        SinceActivity = TimeSpan.Zero;

        var turn = Lock.Turn(pickDegrees);

        switch (turn.Outcome)
        {
            case LockpickTurnOutcome.Opened:
            {
                aisling.Client.SendLockpickTurnResult(turn.Outcome, turn.TurnPercent, LockpickCount(aisling));
                PlaySound(OPEN_SOUND);

                var onOpened = OnOpened;
                ClearSession();

                //the prize first: the Asilon explosion and monster spawn happen at the chest's position
                onOpened?.Invoke(aisling);
                RemoveChest();

                break;
            }

            case LockpickTurnOutcome.Broke:
            {
                aisling.Inventory.RemoveQuantityByTemplateKey(LOCKPICK_TEMPLATE_KEY, 1);
                aisling.SendOrangeBarMessage("Your lockpick broke!");
                PlaySound(BREAK_SOUND);
                aisling.Client.SendLockpickTurnResult(turn.Outcome, turn.TurnPercent, LockpickCount(aisling));

                var onBroke = OnBroke;
                ClearSession();
                onBroke?.Invoke(aisling);
                RemoveChest();

                break;
            }

            default:
                aisling.Client.SendLockpickTurnResult(turn.Outcome, turn.TurnPercent, LockpickCount(aisling));

                break;
        }
    }

    /// <summary>The picker closed the window. The lock keeps its wear.</summary>
    public void HandleClose(Aisling aisling)
    {
        if (IsPickedBy(aisling))
            ClearSession();
    }

    /// <summary>Ends the session and tells the picker why.</summary>
    public void End(string reason)
    {
        if (Picker is null)
            return;

        Picker.Client.SendLockpickClose(reason);
        ClearSession();
    }

    /// <summary>Called from the owning script's Update. Ends a session that can't go on.</summary>
    public void Update(TimeSpan delta)
    {
        if (Picker is null)
            return;

        SinceLastTurn += delta;
        SinceActivity += delta;

        if (!CheckSession())
            return;

        if (SinceActivity >= IdleTimeout)
            End("You lose your focus.");
    }

    //ends the session and returns false when it can't go on
    private bool CheckSession()
    {
        var picker = Picker!;

        if (!IsChestOnMap())
        {
            End("The chest is gone.");

            return false;
        }

        //logged off or changed map: there is no window left to tell
        if (!Chest.MapInstance
                  .GetEntities<Aisling>()
                  .Contains(picker))
        {
            ClearSession();

            return false;
        }

        if (!picker.WithinRange(Chest, REACH))
        {
            End("You moved away from the chest.");

            return false;
        }

        if (!picker.Inventory.HasCountByTemplateKey(LOCKPICK_TEMPLATE_KEY, 1))
        {
            End("You have no lockpicks.");

            return false;
        }

        return true;
    }

    private void ClearSession()
    {
        Picker = null;
        OnOpened = null;
        OnBroke = null;
    }

    private bool IsChestOnMap()
        => Chest.MapInstance
                .GetEntities<Merchant>()
                .Contains(Chest);

    private static ushort LockpickCount(Aisling aisling)
        => (ushort)Math.Clamp(aisling.Inventory.CountOfByTemplateKey(LOCKPICK_TEMPLATE_KEY), 0, ushort.MaxValue);

    private void PlaySound(byte sound)
    {
        foreach (var player in Chest.MapInstance.GetEntitiesWithinRange<Aisling>(Chest, SOUND_RANGE))
            player.Client.SendSound(sound, false);
    }

    private static bool Refuse(Aisling aisling, string message)
    {
        aisling.SendOrangeBarMessage(message);

        return false;
    }

    private void RemoveChest()
    {
        if (IsChestOnMap())
            Chest.MapInstance.RemoveEntity(Chest);
    }
}
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickChestTests/*"`
Expected: 14 passed. Also run `dotnet build Chaos.slnx` in SERVER. Expected: `0 Error(s)`. The new `IChaosWorldClient` methods have exactly one implementation, in `ChaosWorldClient`.

```json:metadata
{"files": ["Chaos-Server/Chaos/Scripting/MerchantScripts/Lockpicking/LockpickChest.cs", "Chaos-Server/Chaos/Scripting/MerchantScripts/Lockpicking/ILockpickChestScript.cs", "Chaos-Server/Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos-Server/Chaos/Networking/ChaosWorldClient.cs", "Chaos-Server/Tests/Chaos.Testing.Infrastructure/Mocks/MockMerchant.cs", "Chaos-Server/Tests/Chaos.Tests/Lockpicking/LockpickChestTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/LockpickChestTests/*\"", "acceptanceCriteria": ["TryBegin rules and exact refusal messages", "Opened: TurnResult(Opened,100), prize while chest on map, chest removed, pick kept", "Broke: pick removed, message, TurnResult(Broke), callback, chest removed", "other players and turns inside 400 ms ignored", "Update closes with the right reason, silently on leaving the map, after 60 s idle", "wear survives close and reopen"], "modelTier": "standard"}
```

---

### Task 4: Crypt chest uses the window

**Goal:** Clicking a crypt chest opens the lockpicking window. The timing bar, the chant and the attack trigger are gone, and gold prizes are unchanged.

**Files:**
- Modify: `SERVER/Chaos/Scripting/MerchantScripts/LockpickingChestScript.cs`
- Test: `SERVER/Tests/Chaos.Tests/Lockpicking/LockpickingChestScriptTests.cs`

**Acceptance Criteria:**
- [ ] The script implements `ILockpickChestScript`, and its lock uses `LockpickTuning.Crypt` for its rolled difficulty
- [ ] `OnClicked` starts a session. Opening pays gold with the existing `CalculatePrize` and jackpot rules, and removes the chest
- [ ] `Update` still sets the difficulty sprite once, and ticks the session
- [ ] `OnAttacked`, `DisplayLockBar`, the chant bar and the stand-east trigger are removed

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickingChestScriptTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SERVER/Tests/Chaos.Tests/Lockpicking/LockpickingChestScriptTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Models.World;
using Chaos.Scripting.MerchantScripts;
using Chaos.Scripting.MerchantScripts.Lockpicking;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Moq;
#endregion

namespace Chaos.Tests.Lockpicking;

public sealed class LockpickingChestScriptTests
{
    private readonly MapInstance Map = MockMapInstance.Create();

    private (LockpickingChestScript Script, Merchant Chest, Aisling Rogue) SetUp()
    {
        var chest = MockMerchant.Create(Map, "Crypt Chest");
        Map.AddEntity(chest, new Point(5, 6));

        var rogue = MockAisling.Create(Map);
        Map.AddEntity(rogue, new Point(5, 5));
        rogue.UserStatSheet.SetBaseClass(BaseClass.Rogue);
        rogue.Inventory.TryAddToNextSlot(MockItem.Create("lockpicks", 2, true));

        return (new LockpickingChestScript(chest), chest, rogue);
    }

    [Test]
    public void Lock_ShouldUseTheCryptTuning_ForItsDifficulty()
    {
        var (script, _, _) = SetUp();
        var @lock = script.Lockpick.Lock;

        @lock.Tuning
             .Should()
             .Be(LockpickTuning.Crypt(@lock.Difficulty));
    }

    [Test]
    public void OnClicked_ShouldOpenTheWindow()
    {
        var (script, _, rogue) = SetUp();

        script.OnClicked(rogue);

        script.Lockpick
              .IsPickedBy(rogue)
              .Should()
              .BeTrue();

        Mock.Get(rogue.Client)
            .Verify(c => c.SendLockpickOpen(script.Lockpick.Lock.Difficulty, $"{script.Lockpick.Lock.Difficulty} Lock", 2), Times.Once);
    }

    [Test]
    public void Opening_ShouldPayGoldAndRemoveTheChest()
    {
        var (script, chest, rogue) = SetUp();

        script.OnClicked(rogue);
        script.Lockpick.HandleTurn(rogue, script.Lockpick.Lock.SweetSpotDegrees);

        rogue.Gold
             .Should()
             .BeGreaterThan(0);

        Map.GetEntities<Merchant>()
           .Should()
           .NotContain(chest);
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickingChestScriptTests/*"`
Expected: a build error, because `LockpickingChestScript.Lockpick` doesn't exist.

- [ ] **Step 3: Rewrite the script**

Read the current class with `find_symbol` (`LockpickingChestScript`, `include_body=true`). Replace the whole class body with the version below. It keeps `CalculatePrize`, `ExtractLevelFromMapName`, `AwardPrize`, the sprite map and `MyRegex` unchanged. It replaces the private `LockDifficulty` enum with `LockpickDifficulty`. It removes the bar, the session fields, `OnAttacked`, `TryBreakLockpick` and the eligibility helpers. Keep the file's `#region` using block, and add `using Chaos.Scripting.MerchantScripts.Lockpicking;`.

```csharp
/// <summary>
///     A crypt chest that a rogue opens in the lockpicking window. Clicking it starts a session. The lock and the
///     session live in <see cref="Lockpick" />. This script keeps the chest's difficulty, sprite and gold prize.
/// </summary>
public partial class LockpickingChestScript : MerchantScriptBase, ILockpickChestScript
{
    private const int LOCKPICK_COST = 1250;
    private const int GOLD_JACKPOT_CHANCE_PERCENT = 1;

    private static readonly Dictionary<LockpickDifficulty, ushort> DifficultyToSprite = new()
    {
        {
            LockpickDifficulty.Easy, 1427
        }, //green
        {
            LockpickDifficulty.Medium, 456
        }, //brown
        {
            LockpickDifficulty.Hard, 1428
        } //red
    };

    private readonly LockpickDifficulty Difficulty;
    private bool SpriteSet;

    /// <inheritdoc />
    public LockpickChest Lockpick { get; }

    public LockpickingChestScript(Merchant subject)
        : base(subject)
    {
        Difficulty = GenerateRandomDifficulty();
        Lockpick = new LockpickChest(subject, LockpickLock.Roll(Difficulty, LockpickTuning.Crypt(Difficulty), Random.Shared));
    }

    public override void OnClicked(Aisling source) => Lockpick.TryBegin(source, AwardPrize);

    public override void Update(TimeSpan delta)
    {
        if (!SpriteSet)
        {
            Subject.Sprite = DifficultyToSprite.GetValueOrDefault(Difficulty, (ushort)456);
            Subject.Display();
            SpriteSet = true;
        }

        Lockpick.Update(delta);
    }

    private static LockpickDifficulty GenerateRandomDifficulty()
        => Random.Shared.Next(100) switch
        {
            < 50 => LockpickDifficulty.Easy,
            < 85 => LockpickDifficulty.Medium,
            _    => LockpickDifficulty.Hard
        };

    #region Prize Logic
    //CalculatePrize, ExtractLevelFromMapName, AwardPrize and MyRegex stay exactly as they are today, except that
    //the difficulty switches in CalculatePrize and AwardPrize use LockpickDifficulty.Easy/Medium/Hard in place of
    //LockDifficulty.Easy/Medium/Hard.
    #endregion
}
```

Inside the `Prize Logic` region, paste the four existing members (`CalculatePrize`, `ExtractLevelFromMapName`, `AwardPrize` and the `[GeneratedRegex(@"\d+")] private static partial Regex MyRegex();` declaration). Then replace `LockDifficulty.` with `LockpickDifficulty.` in them. Delete the region's placeholder comment once they are in. `AwardPrize` must still take `(Aisling source)`, so it can be passed as the `onOpened` callback.

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickingChestScriptTests/*"`
Expected: 3 passed. Then run `grep -n "Chant\|OnAttacked\|DisplayLockBar\|LockDifficulty\b" Chaos/Scripting/MerchantScripts/LockpickingChestScript.cs`. Expected: no output.

```json:metadata
{"files": ["Chaos-Server/Chaos/Scripting/MerchantScripts/LockpickingChestScript.cs", "Chaos-Server/Tests/Chaos.Tests/Lockpicking/LockpickingChestScriptTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/LockpickingChestScriptTests/*\"", "acceptanceCriteria": ["implements ILockpickChestScript with Crypt tuning", "OnClicked starts a session; opening pays gold and removes the chest", "Update sets the sprite once and ticks the session", "OnAttacked, DisplayLockBar, chant and stand-east trigger removed"], "modelTier": "standard"}
```

---

### Task 5: Item chests use the window

**Goal:** Deep Crypt and Asilon chests open the lockpicking window from "Attempt to pick the lock", keep their own prizes, and end an open session when opened with a key.

**Files:**
- Create: `SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/LockedItemChestScript.cs`
- Modify: `SERVER/Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt/DCEasyLockedChestScript.cs`
- Modify: `SERVER/Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt/DCMediumLockedChestScript.cs`
- Modify: `SERVER/Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt/DCHardLockedChestScript.cs`
- Modify: `SERVER/Chaos/Scripting/DialogScripts/Medenia/AsilonPrairie/AsilonChestScript.cs`
- Modify: `SERVER/Tests/Chaos.Tests/DeepCrypt/LockedChestScriptTests.cs` (replace the two `PickLock_*` tests)
- Test: `SERVER/Tests/Chaos.Tests/Lockpicking/LockedItemChestScriptTests.cs`

**Acceptance Criteria:**
- [ ] `LockedItemChestScript` reads `Difficulty` ("Easy", "Medium" or "Hard") from its scriptVars and uses `LockpickTuning.ItemChest`
- [ ] Choosing `*picklock_initial` starts a session on the chest's `LockedItemChestScript` and closes the dialog. Opening gives the chest's existing prize (the DC item table, or the Asilon outcome) before the chest is removed
- [ ] A broken pick logs the existing failure line. The number-guess code (`AttemptToPickLock`, the `OnNext` picklock case, `IntegerRandomizer.RollSingle(4)`) is gone
- [ ] Opening with a key ends any open lock session with "The chest is gone."
- [ ] A chest without a `LockedItemChestScript` replies "This lock can't be picked." and does not crash

**Verify:** from SERVER: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockedItemChestScriptTests/*"` and the same with `LockedChestScriptTests` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SERVER/Tests/Chaos.Tests/Lockpicking/LockedItemChestScriptTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Scripting.MerchantScripts.Lockpicking;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Lockpicking;

public sealed class LockedItemChestScriptTests
{
    //formatter:off
    [Test]
    [Arguments("Easy", LockpickDifficulty.Easy)]
    [Arguments("Medium", LockpickDifficulty.Medium)]
    [Arguments("Hard", LockpickDifficulty.Hard)]
    //formatter:on
    public void Lock_ShouldUseTheItemTuning_ForTheConfiguredDifficulty(string configured, LockpickDifficulty expected)
    {
        var map = MockMapInstance.Create();
        LockedItemChestScript? script = null;

        var chest = MockMerchant.Create(
            map,
            "Locked Chest",
            configureTemplate: t =>
            {
                var vars = new MockScriptVars();
                vars.Set(configured, "Difficulty");
                t.ScriptVars["LockedItemChest"] = vars;
            },
            scriptFactory: m => script = new LockedItemChestScript(m));

        map.AddEntity(chest, new Point(5, 6));

        script!.Lockpick
               .Lock
               .Difficulty
               .Should()
               .Be(expected);

        script.Lockpick
              .Lock
              .Tuning
              .Should()
              .Be(LockpickTuning.ItemChest(expected));
    }
}
```

In `SERVER/Tests/Chaos.Tests/DeepCrypt/LockedChestScriptTests.cs`:

1. Add `using Chaos.Scripting.MerchantScripts.Lockpicking;` to the using block.
2. Change `SetUp` so the chest can carry a real lock. Give it a `bool withLock = true` parameter and build the chest like this:

```csharp
        var chest = withLock
            ? MockMerchant.Create(
                Map,
                "Locked Chest",
                configureTemplate: t =>
                {
                    var vars = new MockScriptVars();
                    vars.Set("Easy", "Difficulty");
                    t.ScriptVars["LockedItemChest"] = vars;
                },
                scriptFactory: m => new LockedItemChestScript(m))
            : MockMerchant.Create(Map, "Locked Chest");
```

3. Replace `PickLock_ShouldNotOpen_WhenTheLockpicksAreGone` and `PickLock_ShouldUseUpTheChest_WhenLockpicksAreHeld` with:

```csharp
    //formatter:off
    [Test]
    [Arguments("easy")]
    [Arguments("medium")]
    [Arguments("hard")]
    //formatter:on
    public void PickLock_ShouldOpenTheLockpickWindow_AndCloseTheDialog(string tier)
    {
        var (player, chest, dialog) = SetUp($"dc{tier}picklock_initial");
        player.UserStatSheet.SetBaseClass(BaseClass.Rogue);
        player.Inventory.TryAddToNextSlot(MockItem.Create("lockpicks", 5, true));

        CreateScript(tier, dialog)
            .OnDisplaying(player);

        chest.Script
             .As<ILockpickChestScript>()!
             .Lockpick
             .IsPickedBy(player)
             .Should()
             .BeTrue();

        Mock.Get(player.Client)
            .Verify(c => c.SendLockpickOpen(It.IsAny<LockpickDifficulty>(), It.IsAny<string>(), 5), Times.Once);

        player.ActiveDialog
              .Get()
              .Should()
              .BeNull();
    }

    //formatter:off
    [Test]
    [Arguments("easy")]
    [Arguments("medium")]
    [Arguments("hard")]
    //formatter:on
    public void PickLock_ShouldSayItCannotBePicked_WhenTheChestHasNoLock(string tier)
    {
        var (player, chest, dialog) = SetUp($"dc{tier}picklock_initial", withLock: false);
        player.UserStatSheet.SetBaseClass(BaseClass.Rogue);
        player.Inventory.TryAddToNextSlot(MockItem.Create("lockpicks", 5, true));

        CreateScript(tier, dialog)
            .OnDisplaying(player);

        player.ActiveDialog
              .Get()!
              .Text
              .Should()
              .Be("This lock can't be picked.");

        Map.GetEntities<Merchant>()
           .Should()
           .Contain(chest);
    }
```

If `chest.Script.As<...>()` does not resolve, add `using Chaos.Extensions;` (it holds `ScriptExtensions`).

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockedItemChestScriptTests/*"`
Expected: a build error, because `LockedItemChestScript` doesn't exist.

- [ ] **Step 3: Write the item chest script**

`SERVER/Chaos/Scripting/MerchantScripts/Lockpicking/LockedItemChestScript.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Scripting.MerchantScripts.Abstractions;
#endregion

namespace Chaos.Scripting.MerchantScripts.Lockpicking;

/// <summary>
///     Gives a dialog-driven item chest (Deep Crypt, Asilon Prairie) a lock that lives as long as the chest. The chest
///     dialog's "Attempt to pick the lock" starts the session and supplies the prize. This script only holds the lock
///     and ticks the session. Template scriptVars: <c>"lockedItemChest": { "difficulty": "Easy" | "Medium" | "Hard" }</c>.
/// </summary>
public sealed class LockedItemChestScript : ConfigurableMerchantScriptBase, ILockpickChestScript
{
    /// <inheritdoc />
    public LockpickChest Lockpick { get; }

    public LockedItemChestScript(Merchant subject)
        : base(subject)
    {
        var difficulty = Enum.Parse<LockpickDifficulty>(Difficulty ?? nameof(LockpickDifficulty.Medium), true);

        Lockpick = new LockpickChest(subject, LockpickLock.Roll(difficulty, LockpickTuning.ItemChest(difficulty), Random.Shared));
    }

    public override void Update(TimeSpan delta) => Lockpick.Update(delta);

    #region ScriptVars
    public string? Difficulty { get; init; }
    #endregion
}
```

The script key is `LockedItemChest` (`ScriptBase.GetScriptKey` drops "Script"). Keys match without regard to case.

- [ ] **Step 4: Change the Deep Crypt dialog scripts**

Make these changes in each of `DCEasyLockedChestScript`, `DCMediumLockedChestScript` and `DCHardLockedChestScript`. Each uses its own tier in the template keys (`dceasy…`, `dcmedium…`, `dchard…`). Add `using Chaos.Scripting.MerchantScripts.Lockpicking;` to each.

1. Delete `AttemptToPickLock`.
2. Replace `HandleFailedAttempt` with a log-only method (keep each file's own logger type and message):

```csharp
    private void LogFailedAttempt(Aisling source)
        => Logger.WithTopics(Topics.Entities.Aisling, Topics.Entities.Merchant)
                 .WithProperty(source)
                 .WithProperty(Subject.DialogSource)
                 .LogInformation("{@AislingName} has failed opening {@Subject} using lockpicks.", source.Name, Subject.DialogSource.Name);
```

3. Replace the whole `case "dceasypicklock_initial":` block in `OnDisplaying` (with the tier's own key) with:

```csharp
            case "dceasypicklock_initial":
            {
                if (Subject.DialogSource is not Merchant chest
                    || chest.Script.As<ILockpickChestScript>() is not { } lockScript)
                {
                    Subject.Reply(source, "This lock can't be picked.");

                    return;
                }

                Subject.Close(source);
                lockScript.Lockpick.TryBegin(source, AwardPrize, LogFailedAttempt);

                break;
            }
```

4. Delete the whole `OnNext` override. Its only case was the number guess.
5. Replace `RemoveChest` with a version that also ends an open lock session:

```csharp
    private void RemoveChest()
    {
        if (Subject.DialogSource is not MapEntity mapEntity)
            return;

        //someone may have the lockpick window open on this chest
        (mapEntity as Merchant)?.Script
                               .As<ILockpickChestScript>()
                               ?.Lockpick
                               .End("The chest is gone.");

        mapEntity.MapInstance.RemoveEntity(mapEntity);
    }
```

6. If `IntegerRandomizer` or `Chaos.Common.Utilities` is no longer used, remove that using.

- [ ] **Step 5: Change the Asilon dialog script**

In `AsilonChestScript` (add `using Chaos.Scripting.MerchantScripts.Lockpicking;`):

1. Delete `AttemptToPickLock`.
2. Replace `HandleFailedPicklock` with:

```csharp
    private void LogFailedPicklock(Aisling source)
        => Logger.WithTopics(Topics.Entities.Aisling, Topics.Entities.Merchant)
                 .WithProperty(source)
                 .WithProperty(Subject.DialogSource)
                 .LogInformation("{@AislingName} failed to pick {@Subject}.", source.Name, Subject.DialogSource?.Name);
```

3. Replace the `case "asilonpicklock_initial":` block in `OnDisplaying` with:

```csharp
            case "asilonpicklock_initial":
            {
                if (Subject.DialogSource is not Merchant chest
                    || chest.Script.As<ILockpickChestScript>() is not { } lockScript)
                {
                    Subject.Reply(source, "This lock can't be picked.");

                    return;
                }

                Subject.Close(source);

                lockScript.Lockpick.TryBegin(
                    source,
                    opener =>
                    {
                        opener.SendOrangeBarMessage("You cracked the lock!");
                        AwardOutcome(opener, chest);
                    },
                    LogFailedPicklock);

                break;
            }
```

4. Delete the whole `OnNext` override.
5. Replace `RemoveChest` with the same session-ending version as in Step 4.5.

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockedItemChestScriptTests/*"`, then the same with `LockedChestScriptTests`.
Expected: all pass (3 + 12). Then run `grep -rn "RollSingle(4)\|MenuArgs" Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt Chaos/Scripting/DialogScripts/Medenia/AsilonPrairie`. Expected: no output.

```json:metadata
{"files": ["Chaos-Server/Chaos/Scripting/MerchantScripts/Lockpicking/LockedItemChestScript.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt/DCEasyLockedChestScript.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt/DCMediumLockedChestScript.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt/DCHardLockedChestScript.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Medenia/AsilonPrairie/AsilonChestScript.cs", "Chaos-Server/Tests/Chaos.Tests/DeepCrypt/LockedChestScriptTests.cs", "Chaos-Server/Tests/Chaos.Tests/Lockpicking/LockedItemChestScriptTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/LockedItemChestScriptTests/*\"", "acceptanceCriteria": ["LockedItemChestScript reads Difficulty and uses ItemChest tuning", "picklock_initial starts a session and closes the dialog; prize before removal", "broken pick logs; number-guess code gone", "key opening ends a session with 'The chest is gone.'", "chest without the script replies 'This lock can't be picked.'"], "modelTier": "standard"}
```

---

### Task 6: World server routes lockpick messages

**Goal:** `LockpickInteraction` packets reach the session of the chest the player is picking.

**Files:**
- Modify: `SERVER/Chaos/Services/Servers/WorldServer.cs` (new `OnLockpickInteraction` after `OnWheelInteraction`, plus one `ClientHandlers` line)

**Acceptance Criteria:**
- [ ] A Turn goes to `HandleTurn` and a Close goes to `HandleClose` on the merchant within 2 tiles whose lock session this player holds
- [ ] A Turn with no such session gets `SendLockpickClose("The chest is gone.")`. A Close with no session is ignored
- [ ] `ClientHandlers[(byte)ClientOpCode.LockpickInteraction]` is set, and the server solution builds

**Verify:** from SERVER: `dotnet build Chaos.slnx` → `0 Error(s)`, and `grep -n "LockpickInteraction" Chaos/Services/Servers/WorldServer.cs` shows the handler and the registration

**Steps:**

- [ ] **Step 1: Add the handler**

After the `OnWheelInteraction` method in `WorldServer.cs`, insert (add `using Chaos.Scripting.MerchantScripts.Lockpicking;` to the file's using block):

```csharp
    /// <summary>
    ///     Routes a lockpick turn or close to the chest this aisling is picking. The chest never comes from client
    ///     input: it is the merchant within reach whose lock session this aisling holds, the same kind of lookup the
    ///     Gilded Spindle handler uses. The search reaches one tile further than picking does, so a turn sent just
    ///     after a step away still finds the chest and is closed with the right reason.
    /// </summary>
    public ValueTask OnLockpickInteraction(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<LockpickInteractionArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnLockpickInteraction);

        ValueTask InnerOnLockpickInteraction(IChaosWorldClient localClient, LockpickInteractionArgs localArgs)
        {
            //a packet queued behind this client's own disconnect, or sent before the world redirect loaded the player
            if (!localClient.Connected || localClient.Aisling is not { } aisling)
                return default;

            var session = aisling.MapInstance
                                 .GetEntitiesWithinRange<Merchant>(aisling, LockpickChest.REACH + 1)
                                 .Select(m => m.Script.As<ILockpickChestScript>()?.Lockpick)
                                 .FirstOrDefault(l => (l is not null) && l.IsPickedBy(aisling));

            if (session is null)
            {
                if (localArgs.Type is LockpickInteractionType.Turn)
                    localClient.SendLockpickClose("The chest is gone.");

                return default;
            }

            if (localArgs.Type is LockpickInteractionType.Close)
                session.HandleClose(aisling);
            else
                session.HandleTurn(aisling, localArgs.PickDegrees);

            return default;
        }
    }
```

- [ ] **Step 2: Register it**

After `ClientHandlers[(byte)ClientOpCode.WheelInteraction] = OnWheelInteraction;`, add:

```csharp
        ClientHandlers[(byte)ClientOpCode.LockpickInteraction] = OnLockpickInteraction;
```

- [ ] **Step 3: Build**

Run: `dotnet build Chaos.slnx` in SERVER. Expected: `0 Error(s)`.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/Servers/WorldServer.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server && dotnet build Chaos.slnx", "acceptanceCriteria": ["Turn -> HandleTurn, Close -> HandleClose on the held session within 2 tiles", "Turn with no session gets SendLockpickClose('The chest is gone.'); Close ignored", "handler registered; solution builds"], "modelTier": "mechanical"}
```

---

### Task 7: Chest templates and dialogs in Unora

**Goal:** The seven item chests carry the `lockedItemChest` script with their difficulty, and the number-guess dialogs are gone.

**Files:**
- Modify: `UNORA/Data/Configuration/Templates/Merchants/Temauir/dceasyChest.json`, `dcmediumChest.json`, `dcHardChest.json`
- Modify: `UNORA/Data/Configuration/Templates/Merchants/Medenia/AsilonPrairie/asilonLeafChest.json`, `asilonLoveChest.json`, `asilonMarbleChest.json`, `asilonRoseChest.json`
- Modify: `UNORA/Data/Configuration/Templates/Dialogs/Temauir/crypt/LockedChest/dceasychest/dceasypicklock_initial.json`, and the `dcmediumchest`, `dchardchest` and `Medenia/AsilonPrairie/AsilonChest` equivalents
- Delete: the four `*picklock_PickChestNext.json` files next to them

**Acceptance Criteria:**
- [ ] Each of the seven merchant templates lists `lockedItemChest` in `scriptKeys` and has `"lockedItemChest": { "difficulty": … }` in `scriptVars`: Easy, Medium and Hard for the DC chests, Medium for all four Asilon chests
- [ ] Each `*picklock_initial.json` is a `Normal` dialog with no `nextDialogKey`
- [ ] No `PickChestNext` file or reference is left
- [ ] Every changed file is valid JSON

**Verify:** from UNORA: `python -c "import json,glob;[json.load(open(f,encoding='utf-8')) for f in glob.glob('Data/Configuration/Templates/**/*.json',recursive=True)]"` → no error, and `grep -rln "PickChestNext" Data/Configuration/Templates` → no output

**Steps:**

- [ ] **Step 1: Add the script to the merchant templates**

In each of the seven templates, change `scriptKeys` to `["showdialog", "lockedItemChest"]`. Then add a `lockedItemChest` entry next to the existing `showdialog` entry in `scriptVars`. Keep the file's indentation style. For `dceasyChest.json`:

```json
  "scriptKeys": [
    "showdialog",
    "lockedItemChest"
  ],
  "scriptVars": {
    "showdialog": {
      …keep the existing contents…
    },
    "lockedItemChest": {
      "difficulty": "Easy"
    }
  },
```

Use `"Medium"` for `dcmediumChest.json`, `"Hard"` for `dcHardChest.json`, and `"Medium"` for the four `asilon*Chest.json` files.

- [ ] **Step 2: Turn the number prompts into plain dialogs**

Replace the whole contents of `dceasypicklock_initial.json` with the following. Keep its `scriptKeys` value as it is in the file (`dceasylockedchest` here):

```json
{
  "options": [],
  "scriptKeys": [
    "dceasylockedchest"
  ],
  "scriptVars": {},
  "templateKey": "dceasypicklock_initial",
  "text": "You kneel and set your pick to the lock.",
  "type": "Normal"
}
```

Do the same for `dcmediumpicklock_initial.json`, `dchardpicklock_initial.json` and `asilonpicklock_initial.json`. Use each file's own `templateKey` and `scriptKeys` values, and remove its `nextDialogKey`. The script closes this dialog as soon as it shows, so the text is only a fallback.

- [ ] **Step 3: Delete the follow-up dialogs**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/lockpick-unora/Data/Configuration/Templates/Dialogs
git rm -q Temauir/crypt/LockedChest/dceasychest/dceasypicklock_PickChestNext.json \
          Temauir/crypt/LockedChest/dcmediumchest/dcmediumpicklock_PickChestNext.json \
          Temauir/crypt/LockedChest/dchardchest/dchardpicklock_PickChestNext.json \
          Medenia/AsilonPrairie/AsilonChest/asilonpicklock_PickChestNext.json
```

(`git rm` stages the deletions in the UNORA worktree only. That is allowed, because it names explicit paths.)

- [ ] **Step 4: Check**

Run the Verify commands. Expected: no error, and no output from the grep.

```json:metadata
{"files": ["Unora/Data/Configuration/Templates/Merchants/Temauir/dceasyChest.json", "Unora/Data/Configuration/Templates/Merchants/Temauir/dcmediumChest.json", "Unora/Data/Configuration/Templates/Merchants/Temauir/dcHardChest.json", "Unora/Data/Configuration/Templates/Merchants/Medenia/AsilonPrairie/asilonLeafChest.json", "Unora/Data/Configuration/Templates/Merchants/Medenia/AsilonPrairie/asilonLoveChest.json", "Unora/Data/Configuration/Templates/Merchants/Medenia/AsilonPrairie/asilonMarbleChest.json", "Unora/Data/Configuration/Templates/Merchants/Medenia/AsilonPrairie/asilonRoseChest.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/crypt/LockedChest/dceasychest/dceasypicklock_initial.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/crypt/LockedChest/dcmediumchest/dcmediumpicklock_initial.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/crypt/LockedChest/dchardchest/dchardpicklock_initial.json", "Unora/Data/Configuration/Templates/Dialogs/Medenia/AsilonPrairie/AsilonChest/asilonpicklock_initial.json"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-unora && grep -rln PickChestNext Data/Configuration/Templates", "acceptanceCriteria": ["seven merchant templates carry lockedItemChest with the right difficulty", "picklock_initial dialogs are Normal with no nextDialogKey", "no PickChestNext left", "all JSON parses"], "modelTier": "mechanical"}
```

---

### Task 8: Client messages and state

**Goal:** The client receives `LockpickDisplay` as an event, can send Turn and Close, and keeps the window's title, difficulty and pick count in `WorldState.Lockpick`.

**Files:**
- Modify: `CLIENT/Chaos.Client.Networking/Definitions/Delegates.cs`
- Modify: `CLIENT/Chaos.Client.Networking/ConnectionManager.cs`
- Create: `CLIENT/Chaos.Client/ViewModel/LockpickState.cs`
- Modify: `CLIENT/Chaos.Client/Collections/WorldState.cs`
- Test: `CLIENT/Tests/Chaos.Client.Tests/LockpickStateTests.cs`

**Acceptance Criteria:**
- [ ] `ConnectionManager.OnLockpickDisplay` fires for `ServerOpCode.LockpickDisplay`
- [ ] `SendLockpickTurn(byte)` and `SendLockpickClose()` send `LockpickInteractionArgs`
- [ ] `LockpickState.ApplyOpen` stores the difficulty, title and count, `ApplyTurnResult` updates the count, and `Clear` resets them
- [ ] `WorldState.Lockpick` is cleared wherever `WorldState.GildedSpindle` is cleared

**Verify:** from CLIENT, with `UnoraServerPath` set: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickStateTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing test**

Create `CLIENT/Tests/Chaos.Client.Tests/LockpickStateTests.cs`:

```csharp
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class LockpickStateTests
{
    [Test]
    public async Task ApplyOpen_stores_the_window_fields()
    {
        var state = new LockpickState();

        state.ApplyOpen(
            new LockpickDisplayArgs
            {
                Type = LockpickDisplayType.Open,
                Difficulty = LockpickDifficulty.Hard,
                Title = "Hard Lock",
                LockpickCount = 4
            });

        state.Difficulty.Should().Be(LockpickDifficulty.Hard);
        state.Title.Should().Be("Hard Lock");
        state.LockpickCount.Should().Be(4);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ApplyTurnResult_updates_the_count_only()
    {
        var state = new LockpickState();
        state.ApplyOpen(new LockpickDisplayArgs { Type = LockpickDisplayType.Open, Title = "Easy Lock", LockpickCount = 3 });

        state.ApplyTurnResult(
            new LockpickDisplayArgs { Type = LockpickDisplayType.TurnResult, Outcome = LockpickTurnOutcome.Broke, LockpickCount = 2 });

        state.LockpickCount.Should().Be(2);
        state.Title.Should().Be("Easy Lock");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clear_resets_everything()
    {
        var state = new LockpickState();
        state.ApplyOpen(new LockpickDisplayArgs { Type = LockpickDisplayType.Open, Difficulty = LockpickDifficulty.Hard, Title = "Hard Lock", LockpickCount = 3 });

        state.Clear();

        state.Difficulty.Should().Be(LockpickDifficulty.Easy);
        state.Title.Should().BeEmpty();
        state.LockpickCount.Should().Be(0);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickStateTests/*"`
Expected: a build error, because `LockpickState` doesn't exist.

- [ ] **Step 3: Write the state class**

`CLIENT/Chaos.Client/ViewModel/LockpickState.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>
///     What the server told the lockpick window: the lock's difficulty, the title and the rogue's lockpick count.
///     The sweet spot and the pick's wear are never sent, so they are not here.
/// </summary>
public sealed class LockpickState
{
    public LockpickDifficulty Difficulty { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int LockpickCount { get; private set; }

    public void ApplyOpen(LockpickDisplayArgs args)
    {
        Difficulty = args.Difficulty;
        Title = args.Title ?? string.Empty;
        LockpickCount = args.LockpickCount;
    }

    public void ApplyTurnResult(LockpickDisplayArgs args) => LockpickCount = args.LockpickCount;

    public void Clear()
    {
        Difficulty = LockpickDifficulty.Easy;
        Title = string.Empty;
        LockpickCount = 0;
    }
}
```

- [ ] **Step 4: Expose and clear it in WorldState**

In `CLIENT/Chaos.Client/Collections/WorldState.cs`:
- After `public static GildedSpindle GildedSpindle { get; } = new();`, add `public static LockpickState Lockpick { get; } = new();`.
- `GildedSpindle.Clear();` appears twice, around lines 407 and 448. After each one, add `Lockpick.Clear();` on its own line at the same indentation.

- [ ] **Step 5: Add the networking**

In `CLIENT/Chaos.Client.Networking/Definitions/Delegates.cs`, after the `WheelDisplayHandler` delegate:

```csharp

/// <summary>
///     Fired when a lockpick window display packet is received.
/// </summary>
public delegate void LockpickDisplayHandler(LockpickDisplayArgs args);
```

In `CLIENT/Chaos.Client.Networking/ConnectionManager.cs`:

1. After `public event WheelDisplayHandler? OnWheelDisplay;`:

```csharp

    /// <summary>
    ///     Fired when a lockpick window display packet is received from the server.
    /// </summary>
    public event LockpickDisplayHandler? OnLockpickDisplay;
```

2. After `PacketHandlers[(byte)ServerOpCode.WheelDisplay] = HandleWheelDisplay;`:

```csharp
        PacketHandlers[(byte)ServerOpCode.LockpickDisplay] = HandleLockpickDisplay;
```

3. After the `HandleWheelDisplay(ServerPacket pkt)` method:

```csharp

    private void HandleLockpickDisplay(ServerPacket pkt)
    {
        var args = Client.Deserialize<LockpickDisplayArgs>(in pkt);
        OnLockpickDisplay?.Invoke(args);
    }
```

4. After `SendWheelClose()`:

```csharp

    /// <summary>
    ///     Tries to turn the lock of the chest this character is picking, with the pick at the given angle
    ///     (0 = left, 90 = up, 180 = right).
    /// </summary>
    public void SendLockpickTurn(byte pickDegrees)
        => SendIfWorld(new LockpickInteractionArgs { Type = LockpickInteractionType.Turn, PickDegrees = pickDegrees });

    /// <summary>
    ///     Tells the server this character closed the lockpick window.
    /// </summary>
    public void SendLockpickClose() => SendIfWorld(new LockpickInteractionArgs { Type = LockpickInteractionType.Close });
```

Both files already import the namespaces that the wheel types use (`Chaos.Networking.Entities.Client`/`Server` and `Chaos.DarkAges.Definitions`). Add any that are missing.

- [ ] **Step 6: Run the test to see it pass**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickStateTests/*"`
Expected: 3 passed.

```json:metadata
{"files": ["Chaos.Client/Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client/Chaos.Client.Networking/ConnectionManager.cs", "Chaos.Client/Chaos.Client/ViewModel/LockpickState.cs", "Chaos.Client/Chaos.Client/Collections/WorldState.cs", "Chaos.Client/Tests/Chaos.Client.Tests/LockpickStateTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-client && UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/LockpickStateTests/*\"", "acceptanceCriteria": ["OnLockpickDisplay fires for LockpickDisplay", "SendLockpickTurn and SendLockpickClose send LockpickInteractionArgs", "LockpickState ApplyOpen/ApplyTurnResult/Clear", "WorldState.Lockpick cleared next to GildedSpindle"], "modelTier": "mechanical"}
```

---

### Task 9: Lock animation and pick geometry

**Goal:** A pure `LockpickAnimator` runs the window's states and angles, and `LockpickGeometry` turns a mouse position into a pick angle. Neither needs MonoGame.

**Files:**
- Create: `CLIENT/Chaos.Client/Controls/World/Popups/Lockpicking/LockpickGeometry.cs`
- Create: `CLIENT/Chaos.Client/Controls/World/Popups/Lockpicking/LockpickAnimator.cs`
- Test: `CLIENT/Tests/Chaos.Client.Tests/LockpickGeometryTests.cs`
- Test: `CLIENT/Tests/Chaos.Client.Tests/LockpickAnimatorTests.cs`

**Acceptance Criteria:**
- [ ] Mouse straight up is 90°, left is 0°, right is 180°, and up-left is 45°. A point below the centre snaps to the nearer end, and the centre itself is 90°
- [ ] `CylinderFrame` maps 0°–90° to frames 0–18
- [ ] Every path works: Idle → Turning → (Jammed) Straining → Returning → Idle; (Opened) → Done → Finished; (Broke) → Straining → Snapped → Finished
- [ ] With no reply for 2 s, the animator returns to Idle and sets `TimedOut`
- [ ] A held key (a repeat, or a second press without a release) starts only one turn, and the pick can't move outside Idle

**Verify:** from CLIENT: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickAnimatorTests/*"` and the same with `LockpickGeometryTests` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

`CLIENT/Tests/Chaos.Client.Tests/LockpickGeometryTests.cs`:

```csharp
using Chaos.Client.Controls.World.Popups.Lockpicking;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class LockpickGeometryTests
{
    [Test]
    public async Task Straight_up_is_90()
    {
        LockpickGeometry.PickDegreesFrom(0, -50).Should().BeApproximately(90f, 0.01f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Left_is_0_and_right_is_180()
    {
        LockpickGeometry.PickDegreesFrom(-50, 0).Should().BeApproximately(0f, 0.01f);
        LockpickGeometry.PickDegreesFrom(50, 0).Should().BeApproximately(180f, 0.01f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Up_left_is_45()
    {
        LockpickGeometry.PickDegreesFrom(-50, -50).Should().BeApproximately(45f, 0.01f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Below_the_centre_snaps_to_the_nearer_end()
    {
        LockpickGeometry.PickDegreesFrom(-50, 10).Should().Be(0f);
        LockpickGeometry.PickDegreesFrom(50, 10).Should().Be(180f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_centre_is_90()
    {
        LockpickGeometry.PickDegreesFrom(0, 0).Should().Be(90f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Cylinder_frames_run_0_to_18()
    {
        LockpickGeometry.CylinderFrame(0f).Should().Be(0);
        LockpickGeometry.CylinderFrame(47f).Should().Be(9);
        LockpickGeometry.CylinderFrame(90f).Should().Be(18);
        LockpickGeometry.CylinderFrame(120f).Should().Be(18);
        await Task.CompletedTask;
    }
}
```

`CLIENT/Tests/Chaos.Client.Tests/LockpickAnimatorTests.cs`:

```csharp
using Chaos.Client.Controls.World.Popups.Lockpicking;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class LockpickAnimatorTests
{
    private static void Run(LockpickAnimator animator, float seconds)
    {
        for (var t = 0f; t < seconds; t += 0.01f)
            animator.Update(0.01f);
    }

    [Test]
    public async Task A_jam_strains_returns_and_goes_idle()
    {
        var a = new LockpickAnimator();

        a.PressTurn(false).Should().BeTrue();
        a.State.Should().Be(LockpickAnimState.Turning);

        a.ApplyResult(LockpickTurnOutcome.Jammed, 40);
        Run(a, 0.2f);
        a.State.Should().Be(LockpickAnimState.Straining);
        a.CylinderDegrees.Should().BeApproximately(36f, 0.5f);

        Run(a, LockpickAnimator.STRAIN_SECONDS);
        a.State.Should().Be(LockpickAnimState.Returning);

        Run(a, LockpickAnimator.RETURN_SECONDS + 0.05f);
        a.State.Should().Be(LockpickAnimState.Idle);
        a.CylinderDegrees.Should().Be(0f);

        await Task.CompletedTask;
    }

    [Test]
    public async Task An_open_turns_fully_then_finishes()
    {
        var a = new LockpickAnimator();
        a.PressTurn(false);
        a.ApplyResult(LockpickTurnOutcome.Opened, 100);

        Run(a, 0.3f);
        a.State.Should().Be(LockpickAnimState.Done);
        a.CylinderDegrees.Should().Be(LockpickAnimator.FULL_TURN_DEGREES);

        Run(a, LockpickAnimator.CLOSE_DELAY_SECONDS + 0.05f);
        a.State.Should().Be(LockpickAnimState.Finished);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_break_strains_then_snaps_then_finishes()
    {
        var a = new LockpickAnimator();
        a.PressTurn(false);
        a.ApplyResult(LockpickTurnOutcome.Broke, 10);

        Run(a, 0.1f);
        a.State.Should().Be(LockpickAnimState.Straining);

        Run(a, LockpickAnimator.STRAIN_SECONDS);
        a.State.Should().Be(LockpickAnimState.Snapped);

        Run(a, LockpickAnimator.CLOSE_DELAY_SECONDS + 0.05f);
        a.State.Should().Be(LockpickAnimState.Finished);

        await Task.CompletedTask;
    }

    [Test]
    public async Task It_waits_at_20_degrees_for_the_reply_then_times_out()
    {
        var a = new LockpickAnimator();
        a.PressTurn(false);

        Run(a, 1f);
        a.State.Should().Be(LockpickAnimState.Turning);
        a.CylinderDegrees.Should().Be(LockpickAnimator.WAIT_AT_DEGREES);

        Run(a, LockpickAnimator.REPLY_TIMEOUT_SECONDS);
        a.TimedOut.Should().BeTrue();

        Run(a, LockpickAnimator.RETURN_SECONDS + 0.05f);
        a.State.Should().Be(LockpickAnimState.Idle);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_held_key_starts_only_one_turn()
    {
        var a = new LockpickAnimator();
        a.PressTurn(false).Should().BeTrue();
        a.ApplyResult(LockpickTurnOutcome.Jammed, 40);
        Run(a, 1f);
        a.State.Should().Be(LockpickAnimState.Idle);

        a.PressTurn(true).Should().BeFalse();
        a.PressTurn(false).Should().BeFalse();

        a.ReleaseTurn();
        a.PressTurn(false).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_pick_only_moves_while_idle_and_stays_in_range()
    {
        var a = new LockpickAnimator();
        a.SetPick(250f);
        a.PickDegrees.Should().Be(180f);

        a.NudgePick(-LockpickAnimator.PICK_STEP_DEGREES);
        a.PickDegrees.Should().Be(178f);

        a.PressTurn(false);
        a.SetPick(10f);
        a.PickDegrees.Should().Be(178f);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_result_outside_a_turn_is_ignored()
    {
        var a = new LockpickAnimator();
        a.ApplyResult(LockpickTurnOutcome.Opened, 100);
        a.State.Should().Be(LockpickAnimState.Idle);
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/LockpickAnimatorTests/*"`
Expected: a build error, because the types don't exist.

- [ ] **Step 3: Write the geometry**

`CLIENT/Chaos.Client/Controls/World/Popups/Lockpicking/LockpickGeometry.cs`:

```csharp
namespace Chaos.Client.Controls.World.Popups.Lockpicking;

/// <summary>Angle maths for the lockpick window. Pick angles: 0 = left, 90 = straight up, 180 = right.</summary>
public static class LockpickGeometry
{
    public const float MAX_PICK_DEGREES = 180f;
    public const int CYLINDER_FRAME_COUNT = 19;
    public const float CYLINDER_FRAME_STEP_DEGREES = 5f;

    /// <summary>
    ///     The pick angle for a point given relative to the lock's centre (screen y grows downward). A point below the
    ///     centre snaps to the nearer end, and the centre itself is straight up.
    /// </summary>
    public static float PickDegreesFrom(float dx, float dy)
    {
        if ((dx == 0f) && (dy == 0f))
            return 90f;

        //maths angle: right 0, up 90, left 180, and negative below the centre
        var theta = MathF.Atan2(-dy, dx) * (180f / MathF.PI);

        if (theta < 0f)
            theta = theta < -90f ? 180f : 0f;

        return MAX_PICK_DEGREES - theta;
    }

    /// <summary>The cylinder sprite frame for a turn angle: one frame per 5 degrees, 0 to 18.</summary>
    public static int CylinderFrame(float cylinderDegrees)
        => Math.Clamp((int)MathF.Round(cylinderDegrees / CYLINDER_FRAME_STEP_DEGREES), 0, CYLINDER_FRAME_COUNT - 1);
}
```

- [ ] **Step 4: Write the animator**

`CLIENT/Chaos.Client/Controls/World/Popups/Lockpicking/LockpickAnimator.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Controls.World.Popups.Lockpicking;

public enum LockpickAnimState
{
    Idle,
    Turning,
    Straining,
    Returning,
    Done,
    Snapped,
    Finished
}

/// <summary>
///     The lockpick window's animation, with no drawing and no MonoGame. A turn starts on a key press and turns the
///     cylinder at once. The server's answer arrives within a few frames and sets how far the cylinder may go. If
///     the answer is late, the cylinder waits at <see cref="WAIT_AT_DEGREES" />.
/// </summary>
public sealed class LockpickAnimator
{
    public const float TURN_DEGREES_PER_SECOND = 360f;
    public const float WAIT_AT_DEGREES = 20f;
    public const float FULL_TURN_DEGREES = 90f;
    public const float STRAIN_SECONDS = 0.3f;
    public const float RETURN_SECONDS = 0.2f;
    public const float CLOSE_DELAY_SECONDS = 1f;
    public const float REPLY_TIMEOUT_SECONDS = 2f;
    public const float PICK_STEP_DEGREES = 2f;
    private const float SHAKE_PIXELS = 2f;
    private const float SHAKE_FLIPS_PER_SECOND = 40f;

    private LockpickTurnOutcome? Outcome;
    private float ReturnFrom;
    private float TargetDegrees;
    private bool TurnKeyHeld;
    private float WaitSeconds;

    public LockpickAnimState State { get; private set; } = LockpickAnimState.Idle;
    public float PickDegrees { get; private set; } = 90f;
    public float CylinderDegrees { get; private set; }

    /// <summary>Seconds spent in the current state.</summary>
    public float StateSeconds { get; private set; }

    /// <summary>Set when the last turn got no answer in time. Cleared when the next turn starts.</summary>
    public bool TimedOut { get; private set; }

    /// <summary>Sideways pick offset in pixels while straining, flipping between +2 and -2.</summary>
    public float PickShakeOffset
        => State == LockpickAnimState.Straining
            ? (((int)(StateSeconds * SHAKE_FLIPS_PER_SECOND) % 2) == 0 ? SHAKE_PIXELS : -SHAKE_PIXELS)
            : 0f;

    public void SetPick(float degrees)
    {
        if (State == LockpickAnimState.Idle)
            PickDegrees = Math.Clamp(degrees, 0f, LockpickGeometry.MAX_PICK_DEGREES);
    }

    public void NudgePick(float deltaDegrees) => SetPick(PickDegrees + deltaDegrees);

    /// <summary>
    ///     The turn key or button went down. Returns true when a turn starts, and the caller then sends it to the
    ///     server. Repeats, and presses without a release in between, never start a turn.
    /// </summary>
    public bool PressTurn(bool isRepeat)
    {
        if (isRepeat || TurnKeyHeld)
            return false;

        TurnKeyHeld = true;

        if (State != LockpickAnimState.Idle)
            return false;

        Enter(LockpickAnimState.Turning);
        CylinderDegrees = 0f;
        TargetDegrees = WAIT_AT_DEGREES;
        Outcome = null;
        WaitSeconds = 0f;
        TimedOut = false;

        return true;
    }

    public void ReleaseTurn() => TurnKeyHeld = false;

    /// <summary>The server's answer to the turn in progress. Ignored outside a turn.</summary>
    public void ApplyResult(LockpickTurnOutcome outcome, byte turnPercent)
    {
        if ((State != LockpickAnimState.Turning) || Outcome.HasValue)
            return;

        Outcome = outcome;

        TargetDegrees = outcome == LockpickTurnOutcome.Opened
            ? FULL_TURN_DEGREES
            : Math.Clamp(turnPercent, (byte)0, (byte)100) / 100f * FULL_TURN_DEGREES;

        if (CylinderDegrees > TargetDegrees)
            CylinderDegrees = TargetDegrees;
    }

    public void Update(float seconds)
    {
        StateSeconds += seconds;

        switch (State)
        {
            case LockpickAnimState.Turning:
                CylinderDegrees = Math.Min(TargetDegrees, CylinderDegrees + (TURN_DEGREES_PER_SECOND * seconds));

                if (!Outcome.HasValue)
                {
                    WaitSeconds += seconds;

                    if (WaitSeconds >= REPLY_TIMEOUT_SECONDS)
                    {
                        TimedOut = true;
                        StartReturn();
                    }

                    break;
                }

                if (CylinderDegrees >= TargetDegrees)
                    Enter(Outcome == LockpickTurnOutcome.Opened ? LockpickAnimState.Done : LockpickAnimState.Straining);

                break;

            case LockpickAnimState.Straining:
                if (StateSeconds >= STRAIN_SECONDS)
                {
                    if (Outcome == LockpickTurnOutcome.Broke)
                        Enter(LockpickAnimState.Snapped);
                    else
                        StartReturn();
                }

                break;

            case LockpickAnimState.Returning:
                if (StateSeconds >= RETURN_SECONDS)
                {
                    CylinderDegrees = 0f;
                    Enter(LockpickAnimState.Idle);
                } else
                    CylinderDegrees = ReturnFrom * (1f - (StateSeconds / RETURN_SECONDS));

                break;

            case LockpickAnimState.Done:
            case LockpickAnimState.Snapped:
                if (StateSeconds >= CLOSE_DELAY_SECONDS)
                    Enter(LockpickAnimState.Finished);

                break;
        }
    }

    /// <summary>Back to a fresh lock with the pick straight up, for a new window.</summary>
    public void Reset()
    {
        Enter(LockpickAnimState.Idle);
        PickDegrees = 90f;
        CylinderDegrees = 0f;
        Outcome = null;
        TimedOut = false;
        TurnKeyHeld = false;
    }

    private void Enter(LockpickAnimState state)
    {
        State = state;
        StateSeconds = 0f;
    }

    private void StartReturn()
    {
        ReturnFrom = CylinderDegrees;
        Enter(LockpickAnimState.Returning);
    }
}
```

- [ ] **Step 5: Run the tests to see them pass**

Run the two Verify filters. Expected: 6 + 7 passed.

```json:metadata
{"files": ["Chaos.Client/Chaos.Client/Controls/World/Popups/Lockpicking/LockpickGeometry.cs", "Chaos.Client/Chaos.Client/Controls/World/Popups/Lockpicking/LockpickAnimator.cs", "Chaos.Client/Tests/Chaos.Client.Tests/LockpickGeometryTests.cs", "Chaos.Client/Tests/Chaos.Client.Tests/LockpickAnimatorTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-client && UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/LockpickAnimatorTests/*\"", "acceptanceCriteria": ["mouse angle mapping and clamps", "CylinderFrame 0-18", "all animation paths", "2 s timeout returns to Idle with TimedOut", "held key starts one turn; pick frozen outside Idle"], "modelTier": "standard"}
```

---

### Task 10: The lockpick window

**Goal:** `LockpickControl` shows the approved layout-A panel, draws the lock from the `lockpk0N.spf` sprites, takes mouse and keyboard input, and is wired into the world screen.

**Files:**
- Create: `CLIENT/Chaos.Client/Controls/World/Popups/Lockpicking/LockFaceControl.cs`
- Create: `CLIENT/Chaos.Client/Controls/World/Popups/Lockpicking/LockpickControl.cs`
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.cs` (field, construction, `Root.AddChild`)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.Wiring.cs` (`WireLockpick`)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.ServerHandlers.cs` (`HandleLockpickDisplay`)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.Update.cs` (tick in seconds)
- Modify: `CLIENT/Chaos.Client/Screens/WorldScreen.Map.cs` (hide on map change)

**Acceptance Criteria:**
- [ ] The panel is 236 px wide. The title is at y 8, coloured by difficulty (Lime, CanaryYellow, Red). The recessed 196 × 196 window is at y 26. The info row ("Lockpicks: N" on the left, "Space: turn" on the right) sits 8 px below the window, the message line 16 px below that, and Close is in the bottom border
- [ ] Mouse movement over the panel sets the pick angle. The Left and Right arrows nudge it 2°. Space or right-click starts one turn and sends `SendLockpickTurn`. Esc and Close hide the window and send `SendLockpickClose`
- [ ] TurnResult drives the animator and updates the count. A jam plays sound 9. The window hides itself when the animator reaches Finished
- [ ] A server Close with a reason shows the reason for 1 s, then hides. With no reason it hides at once
- [ ] The client solution builds, and all client tests pass

**Verify:** from CLIENT, with `UnoraServerPath` set: `dotnet build Chaos.Client.slnx` → `0 Error(s)`, and `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the lock face**

`CLIENT/Chaos.Client/Controls/World/Popups/Lockpicking/LockFaceControl.cs`:

```csharp
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.Lockpicking;

/// <summary>
///     Draws the lock from the <c>lockpk0N.spf</c> sprites: the plate, the cylinder frame for the turn angle, the
///     wrench turning with the cylinder, and the pick at its angle, or its two halves falling after it snaps. Like
///     <see cref="Wheel.WheelControl" />, it replaces UIPanel's drawing with its own, and it is not hit-testable,
///     so mouse events reach the panel around it.
/// </summary>
public sealed class LockFaceControl : UIPanel
{
    public const int SIZE = 184;
    private const float WRENCH_REST_DEGREES = -35f;
    private const float WRENCH_DROP_PIXELS = 14f;
    private const float HANDLE_FALL_PIXELS_PER_SECOND_SQUARED = 240f;
    private const float TIP_FALL_PIXELS_PER_SECOND_SQUARED = 120f;

    private readonly LockpickAnimator Animator;

    public LockFaceControl(LockpickAnimator animator)
    {
        Animator = animator;
        Width = SIZE;
        Height = SIZE;
        IsHitTestVisible = false;
    }

    public Vector2 CenterScreen => new(ScreenX + (SIZE / 2f), ScreenY + (SIZE / 2f));

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var ui = UiRenderer.Instance!;
        var center = CenterScreen;

        DrawCentered(spriteBatch, ui.GetSpfTexture("lockpk01.spf"), center);
        DrawCentered(spriteBatch, ui.GetSpfTexture("lockpk02.spf", LockpickGeometry.CylinderFrame(Animator.CylinderDegrees)), center);

        var wrench = ui.GetSpfTexture("lockpk04.spf");
        var cylinderRadians = MathHelper.ToRadians(Animator.CylinderDegrees);
        var wrenchPivot = center + Vector2.Transform(new Vector2(0f, WRENCH_DROP_PIXELS), Matrix.CreateRotationZ(cylinderRadians));

        AtlasHelper.Draw(
            spriteBatch,
            wrench,
            wrenchPivot,
            null,
            Color.White,
            MathHelper.ToRadians(WRENCH_REST_DEGREES + Animator.CylinderDegrees),
            new Vector2(wrench.Width / 2f, 0f),
            1f,
            SpriteEffects.None,
            0f);

        var pickRotation = MathHelper.ToRadians(Animator.PickDegrees - 90f);
        var pickPivot = center + new Vector2(Animator.PickShakeOffset, 0f);

        if (Animator.State is LockpickAnimState.Snapped or LockpickAnimState.Finished)
        {
            var t = Animator.StateSeconds;
            DrawPick(spriteBatch, ui.GetSpfTexture("lockpk05.spf"), pickPivot + new Vector2(0f, TIP_FALL_PIXELS_PER_SECOND_SQUARED * t * t), pickRotation);
            DrawPick(spriteBatch, ui.GetSpfTexture("lockpk05.spf", 1), pickPivot + new Vector2(0f, HANDLE_FALL_PIXELS_PER_SECOND_SQUARED * t * t), pickRotation);
        } else
            DrawPick(spriteBatch, ui.GetSpfTexture("lockpk03.spf"), pickPivot, pickRotation);
    }

    private static void DrawCentered(SpriteBatch spriteBatch, Texture2D texture, Vector2 center)
        => AtlasHelper.Draw(spriteBatch, texture, new Vector2(center.X - (texture.Width / 2f), center.Y - (texture.Height / 2f)), Color.White);

    //the pick sprite points up with its tip at the bottom centre, which is the pivot in the keyhole
    private static void DrawPick(SpriteBatch spriteBatch, Texture2D texture, Vector2 pivot, float rotation)
        => AtlasHelper.Draw(
            spriteBatch,
            texture,
            pivot,
            null,
            Color.White,
            rotation,
            new Vector2(texture.Width / 2f, texture.Height - 1f),
            1f,
            SpriteEffects.None,
            0f);
}
```

If the compiler can't find `UIPanel` or `UiRenderer`, copy the using lines from `Controls/World/Popups/Wheel/WheelControl.cs`. It draws the same way.

- [ ] **Step 2: Write the window**

`CLIENT/Chaos.Client/Controls/World/Popups/Lockpicking/LockpickControl.cs`:

```csharp
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Controls.World.Popups.Lockpicking;

/// <summary>
///     The lockpicking window: a title coloured by difficulty, a recessed window holding the lock, a row with the
///     pick count and the turn key, a message line and Close. It reuses the ornate dialog frame, like
///     <see cref="Wheel.GildedSpindleControl" />. It is a pure renderer: the server judges every turn, and this
///     control only animates the answer. The world screen opens it on the server's Open display.
/// </summary>
public sealed class LockpickControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 236;
    private const int FRAME_BOTTOM_BORDER = 47;
    private const int CONTENT_LEFT = 20;
    private const int CONTENT_RIGHT = 20;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int TITLE_TOP = 8;
    private const int WINDOW_TOP = 26;
    private const int WINDOW_SIZE = 196;
    private const int WINDOW_PADDING = (WINDOW_SIZE - LockFaceControl.SIZE) / 2;
    private const int WINDOW_X = (PANEL_WIDTH - WINDOW_SIZE) / 2;
    private const int INFO_TOP = WINDOW_TOP + WINDOW_SIZE + 8;
    private const int MESSAGE_TOP = INFO_TOP + 16;
    private const int PANEL_HEIGHT = MESSAGE_TOP + TextRenderer.CHAR_HEIGHT + 1 + FRAME_BOTTOM_BORDER;
    private const float CLOSE_MESSAGE_SECONDS = 1f;
    private const int SOUND_JAM = 9;

    private static readonly SKColor RecessedFillColor = new(10, 8, 5, 255);

    private readonly LockpickAnimator Animator = new();
    private readonly LockFaceControl Face;
    private readonly UILabel CountLabel;
    private readonly UILabel MessageLabel;
    private readonly SoundSystem SoundSystem;
    private readonly UILabel TitleLabel;
    private float CloseAfterSeconds = -1f;
    private bool ShowedTimeout;

    /// <summary>A turn started, at this pick angle.</summary>
    public event Action<byte>? TurnRequested;

    /// <summary>The window closed, for any reason. The world screen tells the server.</summary>
    public event Action? Closed;

    public LockpickControl(SoundSystem soundSystem)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(soundSystem);

        SoundSystem = soundSystem;
        Name = "Lockpick";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = new UILabel
        {
            X = 0,
            Y = TITLE_TOP,
            Width = PANEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(TitleLabel);

        //recessed window, added before the face so it draws underneath it
        AddChild(
            new UIPanel
            {
                X = WINDOW_X,
                Y = WINDOW_TOP,
                Width = WINDOW_SIZE,
                Height = WINDOW_SIZE,
                Background = DialogFrame.BuildRecessedTexture(RecessedFillColor, WINDOW_SIZE, WINDOW_SIZE),
                IsHitTestVisible = false
            });

        Face = new LockFaceControl(Animator)
        {
            X = WINDOW_X + WINDOW_PADDING,
            Y = WINDOW_TOP + WINDOW_PADDING
        };
        AddChild(Face);

        CountLabel = new UILabel
        {
            X = CONTENT_LEFT,
            Y = INFO_TOP,
            Width = (PANEL_WIDTH / 2) - CONTENT_LEFT,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(CountLabel);

        AddChild(
            new UILabel
            {
                X = PANEL_WIDTH / 2,
                Y = INFO_TOP,
                Width = (PANEL_WIDTH / 2) - CONTENT_RIGHT,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Right,
                ForegroundColor = LegendColors.Gray,
                Text = "Space: turn",
                IsHitTestVisible = false
            });

        MessageLabel = new UILabel
        {
            X = CONTENT_LEFT,
            Y = MESSAGE_TOP,
            Width = PANEL_WIDTH - CONTENT_LEFT - CONTENT_RIGHT,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(MessageLabel);
    }

    /// <summary>Repaints from <see cref="WorldState.Lockpick" /> (already filled by ApplyOpen) and shows the window.</summary>
    public override void Show()
    {
        Animator.Reset();
        CloseAfterSeconds = -1f;
        ShowedTimeout = false;

        var state = WorldState.Lockpick;
        TitleLabel.Text = state.Title;

        TitleLabel.ForegroundColor = state.Difficulty switch
        {
            LockpickDifficulty.Easy   => LegendColors.Lime,
            LockpickDifficulty.Medium => LegendColors.CanaryYellow,
            _                         => LegendColors.Red
        };

        RefreshCount();
        SetMessage("Move the mouse to set the pick.");

        base.Show();
    }

    public override void Hide()
    {
        var wasVisible = Visible;

        Animator.Reset();
        CloseAfterSeconds = -1f;

        base.Hide();
        WorldState.Lockpick.Clear();

        if (wasVisible)
            Closed?.Invoke();
    }

    /// <summary>The server's answer to the turn in progress (<see cref="WorldState.Lockpick" /> is already updated).</summary>
    public void OnTurnResult(LockpickTurnOutcome outcome, byte turnPercent)
    {
        Animator.ApplyResult(outcome, turnPercent);
        RefreshCount();

        switch (outcome)
        {
            case LockpickTurnOutcome.Jammed:
                SoundSystem.PlaySound(SOUND_JAM);
                SetMessage("The pick strains...");

                break;

            case LockpickTurnOutcome.Opened:
                SetMessage("The lock clicks open!");

                break;

            case LockpickTurnOutcome.Broke:
                SetMessage("Your lockpick broke!");

                break;
        }
    }

    /// <summary>The server ended the session. A reason is shown for a moment first.</summary>
    public void OnServerClose(string? reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            Hide();

            return;
        }

        SetMessage(reason);
        CloseAfterSeconds = CLOSE_MESSAGE_SECONDS;
    }

    /// <summary>Ticked by the world screen, in seconds.</summary>
    public void Update(float seconds)
    {
        if (!Visible)
            return;

        Animator.Update(seconds);

        if (Animator.TimedOut && !ShowedTimeout)
        {
            ShowedTimeout = true;
            SetMessage("No answer from the server.");
        }

        if (Animator.State == LockpickAnimState.Finished)
        {
            Hide();

            return;
        }

        if (CloseAfterSeconds >= 0f)
        {
            CloseAfterSeconds -= seconds;

            if (CloseAfterSeconds < 0f)
                Hide();
        }
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        var center = Face.CenterScreen;
        Animator.SetPick(LockpickGeometry.PickDegreesFrom(e.ScreenX - center.X, e.ScreenY - center.Y));

        base.OnMouseMove(e);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (e.Button == MouseButton.Right)
        {
            TryTurn(false);
            e.Handled = true;

            return;
        }

        base.OnMouseDown(e);
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button == MouseButton.Right)
        {
            Animator.ReleaseTurn();
            e.Handled = true;

            return;
        }

        base.OnMouseUp(e);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        switch (e.Keycode)
        {
            case Keycode.Escape:
                Hide();
                e.Handled = true;

                return;

            case Keycode.Space:
                TryTurn(e.IsRepeat);
                e.Handled = true;

                return;

            case Keycode.Left:
                Animator.NudgePick(-LockpickAnimator.PICK_STEP_DEGREES);
                e.Handled = true;

                return;

            case Keycode.Right:
                Animator.NudgePick(LockpickAnimator.PICK_STEP_DEGREES);
                e.Handled = true;

                return;
        }

        base.OnKeyDown(e);
    }

    public override void OnKeyUp(KeyUpEvent e)
    {
        if (e.Keycode == Keycode.Space)
        {
            Animator.ReleaseTurn();
            e.Handled = true;

            return;
        }

        base.OnKeyUp(e);
    }

    private void RefreshCount() => CountLabel.Text = $"Lockpicks: {WorldState.Lockpick.LockpickCount}";

    private void SetMessage(string text) => MessageLabel.Text = text;

    private void TryTurn(bool isRepeat)
    {
        if (!Animator.PressTurn(isRepeat))
            return;

        ShowedTimeout = false;
        SetMessage(string.Empty);
        TurnRequested?.Invoke((byte)MathF.Round(Animator.PickDegrees));
    }
}
```

The usings list follows `GildedSpindleControl.cs`. If one of them is unused, or a type such as `MouseButton`, `Keycode` or `HorizontalAlignment` doesn't resolve, match that file's using block. It uses the same types.

- [ ] **Step 3: Wire it into the world screen**

`WorldScreen.cs`:
- After the `Spindle` field (line ~180), add:

```csharp

    //lockpicking window — opened by the server's lockpick Open display when a rogue starts picking a chest
    private LockpickControl LockpickWindow = null!;
```

- After the `Spindle = new GildedSpindleControl(...) { ZIndex = 2 }; WireSpindle();` block, add:

```csharp

        LockpickWindow = new LockpickControl(Game.SoundSystem)
        {
            ZIndex = 2
        };
        WireLockpick();
```

- After `Root.AddChild(Spindle);`, add `Root.AddChild(LockpickWindow);`.
- Add `using Chaos.Client.Controls.World.Popups.Lockpicking;` to the file's using block.

`WorldScreen.Wiring.cs`: after the `#endregion` that closes `Spindle Wiring`, add:

```csharp

    #region Lockpick Wiring
    private void WireLockpick()
    {
        Game.Connection.OnLockpickDisplay += HandleLockpickDisplay;

        LockpickWindow.TurnRequested += pickDegrees => Game.Connection.SendLockpickTurn(pickDegrees);

        //like the Spindle: the session is the server's, so it is told whenever the window goes away
        LockpickWindow.Closed += () => Game.Connection.SendLockpickClose();
    }
    #endregion
```

Find where the world screen unhooks `Game.Connection.OnWheelDisplay -= HandleWheelDisplay;` (WorldScreen.cs, about line 1062). Add `Game.Connection.OnLockpickDisplay -= HandleLockpickDisplay;` after it.

`WorldScreen.ServerHandlers.cs`: after `HandleWheelDisplay`, add:

```csharp

    /// <summary>
    ///     Dispatches a lockpick display packet. Open and TurnResult update <see cref="WorldState.Lockpick" /> first,
    ///     then tell the window, the same way the Spindle is driven.
    /// </summary>
    private void HandleLockpickDisplay(LockpickDisplayArgs args)
    {
        switch (args.Type)
        {
            case LockpickDisplayType.Open:
                WorldState.Lockpick.ApplyOpen(args);
                LockpickWindow.Show();

                break;

            case LockpickDisplayType.TurnResult:
                WorldState.Lockpick.ApplyTurnResult(args);
                LockpickWindow.OnTurnResult(args.Outcome, args.TurnPercent);

                break;

            case LockpickDisplayType.Close:
                LockpickWindow.OnServerClose(args.Reason);

                break;
        }
    }
```

`WorldScreen.Update.cs`: after `Spindle.Update(elapsedMs / 1000f);`, add:

```csharp

        //the lockpick window's animation is in seconds, like the Spindle's
        LockpickWindow.Update(elapsedMs / 1000f);
```

`WorldScreen.Map.cs`: after `Spindle.Hide();` in the map-change path, add:

```csharp

        //a map change ends any lock session; Hide tells the server, which has already dropped it
        LockpickWindow.Hide();
```

- [ ] **Step 4: Build and run all client tests**

Run: `dotnet build Chaos.Client.slnx`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`.
Expected: `0 Error(s)`, and all tests pass.

```json:metadata
{"files": ["Chaos.Client/Chaos.Client/Controls/World/Popups/Lockpicking/LockFaceControl.cs", "Chaos.Client/Chaos.Client/Controls/World/Popups/Lockpicking/LockpickControl.cs", "Chaos.Client/Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/Chaos.Client/Screens/WorldScreen.Wiring.cs", "Chaos.Client/Chaos.Client/Screens/WorldScreen.ServerHandlers.cs", "Chaos.Client/Chaos.Client/Screens/WorldScreen.Update.cs", "Chaos.Client/Chaos.Client/Screens/WorldScreen.Map.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-client && UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server dotnet build Chaos.Client.slnx", "acceptanceCriteria": ["layout-A geometry and difficulty colours", "mouse/arrows set the pick; Space/right-click one turn; Esc/Close hide and send Close", "TurnResult drives animator, count, jam sound 9, auto-hide at Finished", "server Close shows reason 1 s then hides", "client builds and all client tests pass"], "modelTier": "standard"}
```

---

### Task 11: Draw the lock art and get it approved

**Goal:** Pixel-art PNGs for the five lock sprites exist in `UNORA/Tools/Lockpick/art/`. They are rendered into the real panel frame for review, and the user approves them in the browser.

> **USER-ORDERED GATE — NON-SKIPPABLE.** This task was requested by the user in the current conversation. It MUST NOT be closed by walking around it, by declaring it "verified inline", or by substituting a cheaper check. Close only after every item in `acceptanceCriteria` has been re-validated independently, with output captured.

**Files:**
- Create: `UNORA/Tools/Lockpick/make_art.py`
- Create: `UNORA/Tools/Lockpick/art/lockpk01.png`, `lockpk02_00.png` … `lockpk02_18.png`, `lockpk03.png`, `lockpk04.png`, `lockpk05_00.png`, `lockpk05_01.png`
- Create: `UNORA/Tools/Lockpick/review/panel-idle.png`, `panel-jam.png`, `panel-open.png`, `panel-broke.png`

**Acceptance Criteria:**
- [ ] Every PNG has the size given in Global Constraints and at most 255 visible colours per sprite file, with no visible pure-black pixel
- [ ] The four review renders use the real `nd_f0N`/`DlgBack2`/`dlgframe`/`_nbtn` art from the client folder's setoa.dat, with the layout from Task 10
- [ ] The user has seen the renders in the brainstorm companion (or as files) and said the art is approved

**Verify:** `python Tools/Lockpick/make_art.py` in UNORA prints each file with its size and colour count, and the user's approval is quoted in the task close

**Steps:**

- [ ] **Step 1: Write the generator**

`UNORA/Tools/Lockpick/make_art.py` draws every sprite at 1× with no anti-aliasing (PIL shapes), in iron greys with brass and brown taken from the ornate frame. It rotates the keyhole by rotating its polygon's points, not by rotating a bitmap, so each cylinder frame stays crisp. Start from this complete version, then adjust shapes and colours until the review looks right:

```python
"""Draw the lockpicking window's lock sprites as indexed-ready RGBA PNGs, and render review panels.

    python Tools/Lockpick/make_art.py

Writes art/lockpk01.png (plate), art/lockpk02_00-18.png (cylinder, 0-90 degrees in 5 degree steps),
art/lockpk03.png (pick), art/lockpk04.png (wrench), art/lockpk05_00/01.png (broken pick halves), and
review/panel-*.png: the lock inside the real dialog frame, as the client lays it out (LockpickControl)."""
from __future__ import annotations

import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "Accessories"))

from PIL import Image, ImageDraw, ImageFont  # noqa: E402

from acclib.daformats import DatArchive, Epf, Spf, read_palette  # noqa: E402
from acclib.paths import DEFAULT_DATA_DIR  # noqa: E402

ART = HERE / "art"
REVIEW = HERE / "review"

PLATE = 184
CYL = 64
PICK_W, PICK_H = 8, 86
WRENCH_W, WRENCH_H = 12, 60

#palette: never (0, 0, 0) -- the client treats pure black as transparent
IRON_DARK = (24, 22, 19)
IRON = (58, 54, 48)
IRON_MID = (92, 86, 76)
IRON_LIGHT = (128, 120, 106)
BRASS_DARK = (96, 70, 30)
BRASS = (160, 124, 58)
BRASS_LIGHT = (214, 180, 104)
HOLE = (8, 6, 4)
STEEL = (196, 192, 178)
STEEL_DARK = (72, 70, 64)


def plate() -> Image.Image:
    im = Image.new("RGBA", (PLATE, PLATE), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c, r = PLATE // 2, PLATE // 2 - 2
    d.ellipse((c - r, c - r, c + r, c + r), fill=IRON, outline=IRON_DARK, width=3)
    d.ellipse((c - r + 6, c - r + 6, c + r - 6, c + r - 6), outline=IRON_LIGHT, width=1)
    for a in range(0, 360, 30):
        x = c + (r - 11) * math.cos(math.radians(a))
        y = c + (r - 11) * math.sin(math.radians(a))
        d.ellipse((x - 3, y - 3, x + 3, y + 3), fill=BRASS, outline=BRASS_DARK)
        d.point((x - 1, y - 1), fill=BRASS_LIGHT)
    r2 = int(r * 0.62)
    d.ellipse((c - r2, c - r2, c + r2, c + r2), fill=IRON_MID, outline=IRON_DARK, width=2)
    return im


def cylinder(degrees: float) -> Image.Image:
    im = Image.new("RGBA", (CYL, CYL), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = CYL / 2
    d.ellipse((2, 2, CYL - 3, CYL - 3), fill=BRASS, outline=BRASS_DARK, width=2)
    d.arc((5, 5, CYL - 6, CYL - 6), 200, 290, fill=BRASS_LIGHT, width=1)

    def rot(x: float, y: float) -> tuple[float, float]:
        a = math.radians(degrees)
        return (c + x * math.cos(a) - y * math.sin(a), c + x * math.sin(a) + y * math.cos(a))

    #keyhole: a round top and a flared slot, rotated point by point
    top = [rot(7 * math.cos(t), -6 + 7 * math.sin(t)) for t in [i * math.pi / 8 for i in range(16)]]
    d.polygon(top, fill=HOLE)
    d.polygon([rot(-3, -2), rot(3, -2), rot(6, 16), rot(-6, 16)], fill=HOLE)
    return im


def pick() -> Image.Image:
    im = Image.new("RGBA", (PICK_W, PICK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = PICK_W // 2
    d.rectangle((cx - 1, 6, cx, PICK_H - 1), fill=STEEL)          #shaft, tip at the bottom centre
    d.line((cx + 1, 6, cx + 1, PICK_H - 4), fill=STEEL_DARK)
    d.rectangle((0, 0, PICK_W - 1, 16), fill=BRASS, outline=BRASS_DARK)  #handle wrap at the outer end
    d.line((1, 4, PICK_W - 2, 4), fill=BRASS_LIGHT)
    d.point((cx - 2, PICK_H - 2), fill=STEEL)                       #hooked tip
    return im


def wrench() -> Image.Image:
    im = Image.new("RGBA", (WRENCH_W, WRENCH_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = WRENCH_W // 2
    d.rectangle((cx - 2, 0, cx + 1, 8), fill=STEEL_DARK)                     #bent end in the keyhole
    d.rectangle((cx - 1, 6, cx + 1, WRENCH_H - 1), fill=IRON_LIGHT, outline=STEEL_DARK)
    d.line((cx, 8, cx, WRENCH_H - 3), fill=STEEL)
    return im


def broken_halves(whole: Image.Image) -> tuple[Image.Image, Image.Image]:
    split = PICK_H * 2 // 3
    tip = Image.new("RGBA", whole.size, (0, 0, 0, 0))
    handle = Image.new("RGBA", whole.size, (0, 0, 0, 0))
    tip.paste(whole.crop((0, split, PICK_W, PICK_H)), (0, split))
    handle.paste(whole.crop((0, 0, PICK_W, split)), (0, 0))
    return tip, handle


def save(im: Image.Image, name: str) -> None:
    ART.mkdir(parents=True, exist_ok=True)
    colours = {p[:3] for p in im.getdata() if p[3] >= 128}
    if (0, 0, 0) in colours:
        raise SystemExit(f"{name}: has a visible pure-black pixel")
    im.save(ART / name)
    print(f"{name} {im.width}x{im.height} {len(colours)} colours")


# --- review renders: the real frame from the client folder's setoa.dat, laid out as LockpickControl does ---

W, WINDOW_TOP, WINDOW, BBH, CW, CTH = 236, 26, 196, 47, 31, 24
INFO_TOP = WINDOW_TOP + WINDOW + 8
MSG_TOP = INFO_TOP + 16
H = MSG_TOP + 12 + 1 + BBH


def review(frames: dict[str, Image.Image | list[Image.Image]]) -> None:
    arc = DatArchive.load(DEFAULT_DATA_DIR / "setoa.dat")

    def spf(name: str, i: int = 0) -> Image.Image:
        s = Spf.from_bytes(arc.blobs[name])
        f = s.frames[i]
        im = Image.frombytes("P", (f.width, f.height), f.data)
        im.putpalette(s.palette_rgb())
        out = im.convert("RGBA")
        out.putdata([(r, g, b, 0 if v == 0 else 255) for (r, g, b, _a), v in zip(out.getdata(), f.data)])
        return out

    pal = read_palette(arc.blobs["gui00.pal"])
    epf = Epf.from_bytes(arc.blobs["dlgframe.epf"])

    def dlg(i: int) -> Image.Image:
        f = epf.frames[i]
        im = Image.new("RGBA", (f.width, f.height), (0, 0, 0, 0))
        for y in range(f.height):
            for x in range(f.width):
                v = f.data[y * f.width + x]
                if v:
                    im.putpixel((x, y), pal[v] + (255,))
        return im

    def tile(dst: Image.Image, t: Image.Image, x: int, y: int, w: int, h: int) -> None:
        for ty in range(0, h, t.height):
            for tx in range(0, w, t.width):
                dst.alpha_composite(t.crop((0, 0, min(t.width, w - tx), min(t.height, h - ty))), (x + tx, y + ty))

    btn = spf("_nbtn.spf")
    ok_x = W - 20 - btn.width

    def panel(title: str, colour, cyl_deg: float, pick_deg: float, shake: int, broken: bool, msg: str) -> Image.Image:
        im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        tile(im, spf("dlgback2.spf"), 0, 0, W, H)
        tile(im, spf("nd_f05.spf"), CW, 0, W - 2 * CW, spf("nd_f05.spf").height)
        tile(im, spf("nd_f06.spf"), 0, CTH, spf("nd_f06.spf").width, H - CTH - BBH)
        tile(im, spf("nd_f07.spf"), W - spf("nd_f07.spf").width, CTH, spf("nd_f07.spf").width, H - CTH - BBH)
        tile(im, spf("nd_f08_1.spf"), CW, H - BBH, ok_x - 8 - CW, BBH)
        tile(im, spf("nd_f08.spf"), ok_x - 8, H - BBH, W - CW - (ok_x - 8), BBH)
        for name, pos in (("nd_f01.spf", (0, 0)), ("nd_f02.spf", (W - CW, 0)), ("nd_f03.spf", (0, H - BBH)),
                          ("nd_f04.spf", (W - CW, H - BBH))):
            im.alpha_composite(spf(name), pos)

        wx = (W - WINDOW) // 2
        win = Image.new("RGBA", (WINDOW, WINDOW), (10, 8, 5, 255))
        tile(win, dlg(1), 16, 0, WINDOW - 32, 16)
        tile(win, dlg(6), 16, WINDOW - 16, WINDOW - 32, 16)
        tile(win, dlg(3), 0, 16, 16, WINDOW - 32)
        tile(win, dlg(4), WINDOW - 16, 16, 16, WINDOW - 32)
        for i, pos in ((0, (0, 0)), (2, (WINDOW - 16, 0)), (5, (0, WINDOW - 16)), (7, (WINDOW - 16, WINDOW - 16))):
            win.alpha_composite(dlg(i), pos)
        im.alpha_composite(win, (wx, WINDOW_TOP))

        face = Image.new("RGBA", (PLATE, PLATE), (0, 0, 0, 0))
        c = PLATE / 2
        face.alpha_composite(frames["plate"], (0, 0))
        cylf = frames["cylinder"][max(0, min(18, round(cyl_deg / 5)))]
        face.alpha_composite(cylf, (int(c - CYL / 2), int(c - CYL / 2)))
        #wrench: pivot 14 px below centre, turned with the cylinder, resting at -35 degrees
        wr = frames["wrench"].rotate(-(-35 + cyl_deg), resample=Image.NEAREST, expand=True, center=(WRENCH_W / 2, 0))
        piv = (c - 14 * math.sin(math.radians(cyl_deg)), c + 14 * math.cos(math.radians(cyl_deg)))
        face.alpha_composite(wr, (int(piv[0] - wr.width / 2), int(piv[1] - wr.height / 2)))
        for part in (frames["broken"] if broken else [frames["pick"]]):
            big = Image.new("RGBA", (PICK_H * 2 + 8, PICK_H * 2 + 8), (0, 0, 0, 0))
            big.alpha_composite(part, (PICK_H + 4 - PICK_W // 2, 4))
            big = big.rotate(-(pick_deg - 90), resample=Image.NEAREST, center=(PICK_H + 4, PICK_H + 3))
            face.alpha_composite(big, (int(c - (PICK_H + 4) + shake), int(c - (PICK_H + 3) + (30 if broken else 0))))
        im.alpha_composite(face, (wx + 6, WINDOW_TOP + 6))

        d = ImageDraw.Draw(im)
        d.fontmode = "1"
        font = ImageFont.truetype("tahoma.ttf", 11)
        d.text((W // 2, 8), title, font=font, fill=colour, anchor="ma")
        d.text((20, INFO_TOP), "Lockpicks: 4", font=font, fill=(255, 255, 255))
        d.text((W - 20, INFO_TOP), "Space: turn", font=font, fill=(170, 160, 140), anchor="ra")
        d.text((W // 2, MSG_TOP), msg, font=font, fill=(255, 255, 255), anchor="ma")
        im.alpha_composite(btn, (ok_x, H - 3 - btn.height))
        return im

    REVIEW.mkdir(exist_ok=True)
    red = (230, 110, 90)
    shots = {
        "panel-idle.png": panel("Hard Lock", red, 0, 50, 0, False, "Move the mouse to set the pick."),
        "panel-jam.png": panel("Hard Lock", red, 38, 120, 2, False, "The pick strains..."),
        "panel-open.png": panel("Hard Lock", red, 90, 75, 0, False, "The lock clicks open!"),
        "panel-broke.png": panel("Hard Lock", red, 30, 140, 0, True, "Your lockpick broke!"),
    }
    for name, im in shots.items():
        im.resize((im.width * 2, im.height * 2), Image.NEAREST).save(REVIEW / name)
        print(f"review/{name}")


def main() -> int:
    whole = pick()
    frames = {"plate": plate(), "cylinder": [cylinder(i * 5) for i in range(19)], "pick": whole,
              "wrench": wrench(), "broken": list(broken_halves(whole))}
    save(frames["plate"], "lockpk01.png")
    for i, im in enumerate(frames["cylinder"]):
        save(im, f"lockpk02_{i:02d}.png")
    save(frames["pick"], "lockpk03.png")
    save(frames["wrench"], "lockpk04.png")
    for i, im in enumerate(frames["broken"]):
        save(im, f"lockpk05_{i:02d}.png")
    review(frames)
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Generate and look**

Run: `cd /c/Users/Michael/Documents/GitHub/worktrees/lockpick-unora && python Tools/Lockpick/make_art.py`
Expected: one line per sprite with its size and colour count, then four `review/panel-*.png` lines. Open the four review PNGs with the Read tool and judge them the way a player would. Check that the lock reads clearly, that the pick and wrench look like tools, and that everything stays inside the recessed window. Improve `make_art.py` until they look good. If a hand-drawn shape can't be made to look good, generate that piece with PixelLab (`mcp__pixellab__create_ui_asset` or `create_image_pixflux`). Then reduce it to the listed size and palette, and save it over the generated PNG from a small loader step in `make_art.py`.

- [ ] **Step 3: Show the user and get approval**

The brainstorm companion for this feature serves from `C:\Users\Michael\Documents\GitHub\Chaos.Client\.superpowers\brainstorm\1050-1790499417\content\`. If `state/server-stopped` exists there, restart it with the brainstorming skill's `start-server.sh --project-dir C:/Users/Michael/Documents/GitHub/Chaos.Client`. Copy the four review PNGs into the content folder's top level (no subfolders). Write `lock-art-review.html` there, showing the four images at 2×. Then ask the user in the terminal whether the art is approved. Iterate on `make_art.py` until they say yes. Record their words in the task close.

```json:metadata
{"files": ["Unora/Tools/Lockpick/make_art.py", "Unora/Tools/Lockpick/art/lockpk01.png", "Unora/Tools/Lockpick/art/lockpk03.png", "Unora/Tools/Lockpick/art/lockpk04.png"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-unora && python Tools/Lockpick/make_art.py", "acceptanceCriteria": ["every PNG at the listed size, <=255 visible colours per file, no visible pure black", "four review renders use the real frame art and the Task 10 layout", "user approved the art in words, quoted in the close"], "modelTier": "frontier", "userGate": true, "tags": ["user-gate"]}
```

---

### Task 12: Pack the lock art into setoa.dat

**Goal:** `Tools/Lockpick/build.py` packs the approved PNGs into `lockpk01.spf`–`lockpk05.spf` in a copy of the client folder's setoa.dat, checks them, and writes a Desktop batch folder for the launcher patch.

**Files:**
- Create: `UNORA/Tools/Lockpick/build.py`
- Test: `UNORA/Tools/Lockpick/test_build.py`

**Acceptance Criteria:**
- [ ] Each SPF has one shared palette with index 0 transparent, the right frame count (1, 19, 1, 1, 2) and the right frame sizes, and it reads back pixel-identical to its PNGs
- [ ] The batch folder holds `setoa.dat` and a `manifest.txt` that names the five files. `--no-apply` leaves the client folder untouched, and the default copies the batch in with a backup
- [ ] `python -m pytest Tools/Lockpick/test_build.py` passes

**Verify:** from UNORA: `python -m pytest Tools/Lockpick/test_build.py -q` → all pass, then `python Tools/Lockpick/build.py --no-apply` → `done: <Desktop folder>`

**Steps:**

- [ ] **Step 1: Write the failing test**

`UNORA/Tools/Lockpick/test_build.py`:

```python
from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "Accessories"))
sys.path.insert(0, str(HERE))

import pytest  # noqa: E402
from PIL import Image  # noqa: E402

from acclib.daformats import Spf  # noqa: E402
from build import SPRITES, frames_to_spf, files  # noqa: E402


def test_frames_to_spf_shares_one_palette_and_keeps_transparency():
    a = Image.new("RGBA", (2, 1), (0, 0, 0, 0))
    a.putpixel((1, 0), (200, 10, 10, 255))
    b = Image.new("RGBA", (2, 1), (10, 200, 10, 255))
    spf = Spf.from_bytes(frames_to_spf([a, b]).to_bytes())
    assert len(spf.frames) == 2
    assert spf.frames[0].data[0] == 0
    assert spf.frames[0].data[1] != 0
    assert spf.frames[1].data[0] != spf.frames[0].data[1]


def test_frames_to_spf_rejects_visible_pure_black():
    im = Image.new("RGBA", (1, 1), (0, 0, 0, 255))
    with pytest.raises(ValueError):
        frames_to_spf([im])


def test_every_sprite_has_its_frame_count_and_size():
    built = files()
    expected = {"lockpk01.spf": (1, (184, 184)), "lockpk02.spf": (19, (64, 64)), "lockpk03.spf": (1, (8, 86)),
                "lockpk04.spf": (1, (12, 60)), "lockpk05.spf": (2, (8, 86))}
    assert set(built) == set(expected) == set(SPRITES)
    for name, (count, size) in expected.items():
        spf = Spf.from_bytes(built[name])
        assert len(spf.frames) == count, name
        assert all((f.width, f.height) == size for f in spf.frames), name
```

- [ ] **Step 2: Run it to see it fail**

Run: `python -m pytest Tools/Lockpick/test_build.py -q`
Expected: `ModuleNotFoundError: No module named 'build'`.

- [ ] **Step 3: Write the builder**

`UNORA/Tools/Lockpick/build.py`:

```python
"""Pack the lockpicking window's lock sprites into setoa.dat. Run from the repo root:

    python Tools/Lockpick/build.py            # batch folder on the desktop + copy into the client folder
    python Tools/Lockpick/build.py --no-apply # batch folder only

Reads art/*.png (RGBA; alpha under half is transparent), makes one SPF per sprite with its own palette, writes them
into a copy of the client folder's setoa.dat, re-reads the copy to check every file, and writes manifest.txt.
Upload the batch's setoa.dat in the same launcher patch as the client that draws the lockpick window."""
from __future__ import annotations

import argparse
import shutil
import struct
import sys
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "Accessories"))

from PIL import Image  # noqa: E402

from acclib.daformats import DatArchive, Spf, SpfFrame  # noqa: E402
from acclib.paths import DEFAULT_DATA_DIR  # noqa: E402

ART = HERE / "art"

#sprite file -> its PNG frames, in frame order
SPRITES: dict[str, list[str]] = {
    "lockpk01.spf": ["lockpk01.png"],
    "lockpk02.spf": [f"lockpk02_{i:02d}.png" for i in range(19)],
    "lockpk03.spf": ["lockpk03.png"],
    "lockpk04.spf": ["lockpk04.png"],
    "lockpk05.spf": ["lockpk05_00.png", "lockpk05_01.png"],
}


def _rgb565(r: int, g: int, b: int) -> int:
    return ((r * 31 // 255) << 11) | ((g * 63 // 255) << 5) | (b * 31 // 255)


def _rgb555(r: int, g: int, b: int) -> int:
    return ((r * 31 // 255) << 10) | ((g * 31 // 255) << 5) | (b * 31 // 255)


def frames_to_spf(images: list[Image.Image]) -> Spf:
    """One palettized SPF from RGBA frames that share a palette. Alpha under half is index 0 (transparent)."""
    colours: dict[tuple[int, int, int], int] = {}
    frames = []
    for image in images:
        im = image.convert("RGBA")
        data = bytearray()
        for r, g, b, a in struct.iter_unpack("4B", im.tobytes()):
            if a < 128:
                data.append(0)
                continue
            if (r, g, b) == (0, 0, 0):
                raise ValueError("visible pure black reads as transparent in the client; use (8, 6, 4)")
            data.append(colours.setdefault((r, g, b), len(colours) + 1))
        frames.append(SpfFrame(0, 0, im.width, im.height, 0, 0, 0, im.width, 0, bytes(data)))
    if len(colours) > 255:
        raise ValueError(f"{len(colours)} colours; an SPF palette holds 255 plus transparent")
    pad = [0] * (255 - len(colours))
    primary = struct.pack("<256H", 0, *(_rgb565(*c) for c in colours), *pad)
    secondary = struct.pack("<256H", 0, *(_rgb555(*c) for c in colours), *pad)
    return Spf(0, 1, primary, secondary, frames)


def files() -> dict[str, bytes]:
    out = {}
    for name, pngs in SPRITES.items():
        try:
            out[name] = frames_to_spf([Image.open(ART / png) for png in pngs]).to_bytes()
        except ValueError as e:
            raise SystemExit(f"{name}: {e}")
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
    out = args.out or Path.home() / "Desktop" / f"Lockpick art batch {stamp:%Y%m%d-%H%M}"
    out.mkdir(parents=True, exist_ok=True)
    (out / "setoa.dat").write_bytes(data)
    lines = [f"Lockpick art batch built {stamp:%Y-%m-%d %H:%M} from {args.data_dir}", "",
             "Changed client files (upload these, in the same launcher patch as the client with the lockpick window):",
             f"  setoa.dat ({', '.join(SPRITES)})", ""]
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

`DatArchive.put` and `DatArchive.__getitem__` are the same calls `Tools/Emblems/build.py` uses. Check both exist in `acclib/daformats.py` before running.

- [ ] **Step 4: Run the tests, then build a batch**

Run: `python -m pytest Tools/Lockpick/test_build.py -q`. Expected: 3 passed.
Run: `python Tools/Lockpick/build.py --no-apply`. Expected: `done: C:\Users\Michael\Desktop\Lockpick art batch …`. Don't apply it to the client folder unless the user asks. Task 13 applies it for the local test.

```json:metadata
{"files": ["Unora/Tools/Lockpick/build.py", "Unora/Tools/Lockpick/test_build.py"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-unora && python -m pytest Tools/Lockpick/test_build.py -q", "acceptanceCriteria": ["shared palette, index 0 transparent, frame counts 1/19/1/1/2 and sizes, read-back identical", "batch folder with setoa.dat and manifest; --no-apply leaves the client folder alone", "pytest passes"], "modelTier": "standard"}
```

---

### Task 13: Full verification

**Goal:** Both solutions build, every test suite passes (apart from the two known server failures), and the lock art loads in the real client.

**Files:**
- Modify: none

**Acceptance Criteria:**
- [ ] `dotnet build Chaos.slnx` in SERVER prints `0 Error(s)`
- [ ] The full `Chaos.Tests` run fails only GiveAbility and OnItemDroppedOn stackable, and `Chaos.Networking.Tests` passes
- [ ] The full client build and `Chaos.Client.Tests` run pass against SERVER
- [ ] `python -m pytest Tools/Lockpick -q` passes in UNORA
- [ ] A local client started with the batch's setoa.dat shows the lock sprites (not the missing-texture square) when the window opens. If the user isn't available to run the server, record that this check is still pending

**Verify:** the four commands in the steps, with their pass counts quoted in the task close

**Steps:**

- [ ] **Step 1: Server**

Stop any running `Chaos.exe` or client (ask the user). In SERVER:
`dotnet build Chaos.slnx` → `0 Error(s)`.
`dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi` → only the two known failures.
`dotnet run --project Tests/Chaos.Networking.Tests/Chaos.Networking.Tests.csproj -- --no-ansi` → all pass.

- [ ] **Step 2: Client**

In CLIENT, with `UnoraServerPath` set to SERVER:
`dotnet build Chaos.Client.slnx` → `0 Error(s)`.
`dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass.

- [ ] **Step 3: Unora tools**

In UNORA: `python -m pytest Tools/Lockpick -q` → all pass.

- [ ] **Step 4: In-game look (with the user)**

Ask the user whether they want to run the local server from SERVER now. If yes:
1. Apply the art: `python Tools/Lockpick/build.py` (keeps a backup of the client folder's setoa.dat).
2. The user starts the server from SERVER.
3. Launch the CLIENT build from `Chaos.Client/bin/Debug/net10.0` with `DA_PATH=C:\Users\Michael\Documents\Unora\Unora Files`.
4. On a rogue with lockpicks, click a crypt chest and check the window.

Take a screenshot, following the memory note `chaos-client-window-automation`. If the user declines, write "in-game check pending" in the task close.

```json:metadata
{"files": [], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/lockpick-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["server solution builds", "Chaos.Tests only the two known failures; Chaos.Networking.Tests pass", "client builds and Chaos.Client.Tests pass against SERVER", "Unora pytest passes", "lock sprites load in a local client, or recorded as pending"], "modelTier": "standard"}
```

---

### Task 14: Commit the full implementation

**Goal:** One commit per repo on `feat/lockpick-window`, with the client pointing its `Chaos-Server` submodule at the server commit.

**Files:**
- Modify: none (stages the files from Tasks 1–12)

**Acceptance Criteria:**
- [ ] SERVER has one new commit with every Task 1–6 file and nothing else
- [ ] UNORA has one new commit with the Task 7 and Task 11–12 files (the art PNGs, `make_art.py`, `build.py`, `test_build.py`; not `review/` or `__pycache__`)
- [ ] CLIENT has one new commit with the Task 8–10 files, this plan, its `.tasks.json`, the amended spec, and `Chaos-Server` pointed at the SERVER commit
- [ ] Nothing is pushed or merged

**Verify:** `git -C <each worktree> log --oneline -2` and `git -C <each worktree> status --short` (only untracked build output and `review/` left)

**Steps:**

- [ ] **Step 1: Commit SERVER**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/lockpick-server
git status --short
git add Chaos.DarkAges/Definitions/Enums.cs Chaos.Networking.Abstractions/Definitions/Enums.cs \
  Chaos.Networking/Entities/Client/LockpickInteractionArgs.cs Chaos.Networking/Converters/Client/LockpickInteractionConverter.cs \
  Chaos.Networking/Entities/Server/LockpickDisplayArgs.cs Chaos.Networking/Converters/Server/LockpickDisplayConverter.cs \
  Chaos/Scripting/MerchantScripts/Lockpicking Chaos/Scripting/MerchantScripts/LockpickingChestScript.cs \
  Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt/DCEasyLockedChestScript.cs \
  Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt/DCMediumLockedChestScript.cs \
  Chaos/Scripting/DialogScripts/Temuair/Quests/DeepCrypt/DCHardLockedChestScript.cs \
  Chaos/Scripting/DialogScripts/Medenia/AsilonPrairie/AsilonChestScript.cs \
  Chaos/Networking/Abstractions/IChaosWorldClient.cs Chaos/Networking/ChaosWorldClient.cs Chaos/Services/Servers/WorldServer.cs \
  Tests/Chaos.Testing.Infrastructure/Mocks/MockMerchant.cs Tests/Chaos.Tests/Networking/LockpickPacketConverterTests.cs \
  Tests/Chaos.Tests/Lockpicking Tests/Chaos.Tests/DeepCrypt/LockedChestScriptTests.cs
git commit -q -F - <<'EOF'
Pick chest locks in a Skyrim-style window

The crypt chest's timing bar and the Deep Crypt and Asilon number guess are replaced by one lockpicking
window. The server keeps each chest's hidden sweet spot and judges every turn; the client only animates.
Item chests break the pick on the second jam to stay near their old 1-in-4 odds.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
git log --oneline -1
```

Before committing, compare `git status --short` with the file list. Anything unexpected (a file you didn't change, `appsettings.json`, `launchSettings.json`) stays out.

- [ ] **Step 2: Commit UNORA**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/lockpick-unora
git status --short
git add Data/Configuration/Templates/Merchants/Temauir/dceasyChest.json Data/Configuration/Templates/Merchants/Temauir/dcmediumChest.json \
  Data/Configuration/Templates/Merchants/Temauir/dcHardChest.json Data/Configuration/Templates/Merchants/Medenia/AsilonPrairie/asilonLeafChest.json \
  Data/Configuration/Templates/Merchants/Medenia/AsilonPrairie/asilonLoveChest.json Data/Configuration/Templates/Merchants/Medenia/AsilonPrairie/asilonMarbleChest.json \
  Data/Configuration/Templates/Merchants/Medenia/AsilonPrairie/asilonRoseChest.json \
  Data/Configuration/Templates/Dialogs/Temauir/crypt/LockedChest/dceasychest/dceasypicklock_initial.json \
  Data/Configuration/Templates/Dialogs/Temauir/crypt/LockedChest/dcmediumchest/dcmediumpicklock_initial.json \
  Data/Configuration/Templates/Dialogs/Temauir/crypt/LockedChest/dchardchest/dchardpicklock_initial.json \
  Data/Configuration/Templates/Dialogs/Medenia/AsilonPrairie/AsilonChest/asilonpicklock_initial.json \
  Tools/Lockpick/make_art.py Tools/Lockpick/build.py Tools/Lockpick/test_build.py Tools/Lockpick/art
git commit -q -F - <<'EOF'
Give item chests a lock for the lockpicking window, and add the lock art

Deep Crypt and Asilon chests get the lockedItemChest script with their difficulty, and the number-guess
follow-up dialogs are removed. Tools/Lockpick draws the lock sprites and packs them into setoa.dat.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
git log --oneline -1
```

The four `PickChestNext` deletions were staged by `git rm` in Task 7, so they are part of this commit.

- [ ] **Step 3: Commit CLIENT**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/lockpick-client
SERVER_SHA=$(git -C /c/Users/Michael/Documents/GitHub/worktrees/lockpick-server rev-parse HEAD)
git update-index --cacheinfo 160000,$SERVER_SHA,Chaos-Server
git status --short
git add Chaos.Client.Networking/Definitions/Delegates.cs Chaos.Client.Networking/ConnectionManager.cs \
  Chaos.Client/ViewModel/LockpickState.cs Chaos.Client/Collections/WorldState.cs \
  Chaos.Client/Controls/World/Popups/Lockpicking Chaos.Client/Screens/WorldScreen.cs Chaos.Client/Screens/WorldScreen.Wiring.cs \
  Chaos.Client/Screens/WorldScreen.ServerHandlers.cs Chaos.Client/Screens/WorldScreen.Update.cs Chaos.Client/Screens/WorldScreen.Map.cs \
  Tests/Chaos.Client.Tests/LockpickStateTests.cs Tests/Chaos.Client.Tests/LockpickGeometryTests.cs Tests/Chaos.Client.Tests/LockpickAnimatorTests.cs
cp /c/Users/Michael/Documents/GitHub/Chaos.Client/docs/superpowers/specs/2026-09-27-lockpicking-window-design.md docs/superpowers/specs/
cp /c/Users/Michael/Documents/GitHub/Chaos.Client/docs/superpowers/plans/2026-09-27-lockpicking-window.md* docs/superpowers/plans/
git add -f docs/superpowers/specs/2026-09-27-lockpicking-window-design.md docs/superpowers/plans/2026-09-27-lockpicking-window.md \
  docs/superpowers/plans/2026-09-27-lockpicking-window.md.tasks.json
git commit -q -F - <<'EOF'
Draw the lockpicking window, and point Chaos-Server at the lockpick branch

A framed panel like the Gilded Spindle holds a sprite-drawn lock. The mouse or arrows set the pick,
Space or right-click makes one turn, and the server's answer drives the animation.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
git log --oneline -1
```

The plan and spec copies in the shared `Chaos.Client` tree are the working copies. After this commit, the shared tree's untracked plan and its spec edit match the worktree's commit, so a later merge brings them in.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/lockpick-client log --oneline -2", "acceptanceCriteria": ["one SERVER commit with Tasks 1-6 files only", "one UNORA commit with Task 7 and 11-12 files (no review/ or __pycache__)", "one CLIENT commit with Tasks 8-10 files, plan, tasks.json, spec, submodule at the SERVER commit", "nothing pushed or merged"], "modelTier": "mechanical"}
```
