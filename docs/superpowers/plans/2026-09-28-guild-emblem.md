# Guild Emblem Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Guild leaders buy a guild emblem from Tibbs, paint an 11 × 11 emblem in a small editor, an admin approves it in the shared guild design review window, and members can show it in their world list cell.

**Architecture:** The emblem reuses the guild cloak's flow. Designs live in the cloak's storage (`GuildCloakState.json`) and share its id counter. `GuildCloakService` handles editing, review and download for both kinds. The emblem system (`EmblemService`) treats the reserved key `guild` as a live emblem owned while the player's guild has an approved emblem. The world list and the Emblem tab carry the emblem's id; the client downloads each id once and draws it where it would draw emblem art.

**Tech Stack:** C# / .NET 10, TUnit + FluentAssertions + Moq (tests), MonoGame (client), System.Text.Json storage, JSON dialog templates (Unora).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-09-28-guild-emblem-design.md` (commit 3b2d753).

## Global Constraints

- Emblem size 11 × 11 (`GuildEmblemProtocol.SIZE`), 121 pixel bytes, row-major; `0` = see-through, `1`–`6` = palette color. 1 to 6 colors.
- Price 5,000,000 gold (`GuildEmblemProtocol.PRICE`), hall property name `emblem`, reserved emblem key `guild`.
- Emblem submissions take ids from `GuildCloakState.NextDesignId`, the same counter as cloaks.
- Opcodes: `ClientOpCode.GuildEmblemEditorInteraction = 133`, `ClientOpCode.GuildEmblemDesignRequest = 134`, `ServerOpCode.GuildEmblemEditor = 138`, `ServerOpCode.GuildEmblemDesign = 139`. Check they are still free on server master before Task 2 (see Task 2, Step 1).
- `CONSTANTS.CLIENT_VERSION` goes from 759 to 760.
- The save/submit rate limit (2 s, `GuildCloakProtocol.SaveInterval`) is shared between cloak and emblem saves.
- Messages exactly as in the spec's refusal table; the Emblem tab texts are "The emblem of your guild.", "While you are in <guild>", "While in guild".
- Work only in the three worktrees under `C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-*` (Task 0). Several Claude sessions share the main checkouts; never stash, reset, clean or `git add -A` there.
- Stop any running `Chaos.exe` or client before building (they lock `bin`). Never build the server and client solutions at the same time.
- Commit strategy is at-end: implementer subagents do NOT commit. Leave all changes in the worktrees; Task 11 makes one commit per repo.
- Two server tests already fail on master: `OnItemDroppedOn` stackable (and possibly `GiveAbility`). Leave them alone.

**User decisions (already made):**
- Members choose whether to show the guild emblem; it is one more entry in their Emblem tab.
- The guild emblem costs 5,000,000 gold, bought once from Tibbs.
- An admin approves each emblem before other players see it.
- Approach A: share the cloak's storage and review window; one mixed review list ("Review guild designs").
- Editor layout B (compact) with all 6 color boxes plus a see-through box.
- Section 1 and 2 of the design, and the written spec, were approved as written.

**Implementation choices made while planning (within the spec's intent):**
- The world list reads a cached guild emblem id from `AislingEmblems` instead of asking `GuildCloakService` on every list. `EmblemService` refreshes that cache on every approval, clear, join, leave, login and `/reload emblems`, which gives the spec's "a new version shows at once" behavior.
- The admin trinket's clear flow asks for the guild name, then one menu: "Clear the cloak", "Clear the emblem", "No". The menu itself is the confirmation (section 1's wording), instead of a separate confirmation step.
- The editor window title uses a hyphen ("Guild Emblem - Richards"), because the game font may not have an em dash.

## Paths used below

| Name | Path |
|---|---|
| `$SRV` | `C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server` (Chaos-Server, branch `feat/guild-emblem`) |
| `$CLI` | `C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client` (Chaos.Client, branch `feat/guild-emblem`) |
| `$UNO` | `C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-unora` (Unora, branch `feat/guild-emblem`) |

Commands:

- Server tests (one class): `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<ClassName>/*"`
- Client tests (one class): `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/<ClassName>/*"`
- Client build: `cd $CLI && UnoraServerPath=$SRV dotnet build Chaos.Client/Chaos.Client.csproj` (the `.slnx` fails in a worktree).

Use `dotnet run`, never `dotnet test`. Filter with `--treenode-filter`, never `--filter`.

---

### Task 0: Create the three worktrees

**Goal:** Isolated `feat/guild-emblem` branches in all three repos, with a green baseline build.

**Files:**
- Create: the three worktree folders listed under "Paths used below"

**Acceptance Criteria:**
- [ ] `git -C $SRV branch --show-current` prints `feat/guild-emblem`; same for `$CLI` and `$UNO`
- [ ] The server test project builds in `$SRV`
- [ ] The client builds in `$CLI` against `$SRV`

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server branch --show-current` → `feat/guild-emblem`

**Steps:**

- [ ] **Step 1: Create the worktrees**

```bash
cd C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server
git -c core.longpaths=true worktree add -b feat/guild-emblem C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server master
cd C:/Users/Michael/Documents/GitHub/Chaos.Client
git worktree add -b feat/guild-emblem C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client main
cd C:/Users/Michael/Documents/GitHub/Unora
git -c core.longpaths=true worktree add -b feat/guild-emblem C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-unora main
```

Pass `core.longpaths` only with `-c`. Never run `git config core.longpaths` in a worktree: it writes the shared repo config.

- [ ] **Step 2: Baseline builds**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server
dotnet build Tests/Chaos.Tests/Chaos.Tests.csproj
cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client
UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server dotnet build Chaos.Client/Chaos.Client.csproj
```

Expected: both succeed. If a build fails with a locked file, ask the user to stop `Chaos.exe` or the client, then retry.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server branch --show-current", "acceptanceCriteria": ["three worktrees on feat/guild-emblem", "server test project builds", "client builds against the server worktree"], "modelTier": "mechanical"}
```

---

### Task 1: Shared emblem design type, constants and design kind

**Goal:** `GuildEmblemDesign`, `GuildEmblemProtocol` and `GuildDesignKind` exist in `Chaos.DarkAges`, with tests.

**Files:**
- Create: `$SRV/Chaos.DarkAges/Definitions/GuildEmblemProtocol.cs`
- Create: `$SRV/Chaos.DarkAges/Definitions/GuildEmblemDesign.cs`
- Modify: `$SRV/Chaos.DarkAges/Definitions/Enums.cs` (after `GuildCloakReviewAction`)
- Test: `$SRV/Tests/Chaos.Tests/GuildEmblem/GuildEmblemDesignTests.cs`

**Acceptance Criteria:**
- [ ] The default design is valid, all see-through, with one gold color (212, 175, 55)
- [ ] `IsValid` refuses a wrong grid size, 0 or more than 6 colors, and out-of-range pixel values
- [ ] `HasPaint` is true only when a pixel is not 0
- [ ] `DeepCopy` is independent; `ContentEquals` compares contents

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemDesignTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing test** — create `Tests/Chaos.Tests/GuildEmblem/GuildEmblemDesignTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildEmblem;

public sealed class GuildEmblemDesignTests
{
    [Test]
    public void Default_is_valid_all_see_through_with_one_gold_color()
    {
        var design = GuildEmblemDesign.CreateDefault();

        design.IsValid().Should().BeTrue();
        design.HasPaint().Should().BeFalse();
        design.Colors.Should().Equal(new GuildCloakColor(212, 175, 55));
        design.Pixels.Should().HaveCount(GuildEmblemProtocol.PIXEL_COUNT).And.OnlyContain(p => p == 0);
    }

    [Test]
    public void IsValid_refuses_a_wrong_grid_size_no_colors_too_many_colors_and_out_of_range_values()
    {
        var wrongSize = GuildEmblemDesign.CreateDefault();
        wrongSize.Pixels = new byte[GuildEmblemProtocol.PIXEL_COUNT - 1];

        var noColors = GuildEmblemDesign.CreateDefault();
        noColors.Colors.Clear();

        var tooMany = GuildEmblemDesign.CreateDefault();

        for (var i = 0; i < GuildEmblemProtocol.MAX_COLORS; i++)
            tooMany.Colors.Add(new GuildCloakColor(1, 2, 3));

        var outOfRange = GuildEmblemDesign.CreateDefault();
        outOfRange.Pixels[60] = 2;

        wrongSize.IsValid().Should().BeFalse();
        noColors.IsValid().Should().BeFalse();
        tooMany.IsValid().Should().BeFalse();
        outOfRange.IsValid().Should().BeFalse();
    }

    [Test]
    public void HasPaint_is_true_once_one_pixel_has_a_color()
    {
        var design = GuildEmblemDesign.CreateDefault();
        design.Pixels[GuildEmblemProtocol.PIXEL_COUNT - 1] = 1;

        design.HasPaint().Should().BeTrue();
    }

    [Test]
    public void DeepCopy_is_independent_and_ContentEquals_compares_contents()
    {
        var design = GuildEmblemDesign.CreateDefault();
        var copy = design.DeepCopy();

        copy.ContentEquals(design).Should().BeTrue();

        copy.Pixels[0] = 1;
        copy.Colors.Add(new GuildCloakColor(9, 9, 9));

        design.Pixels[0].Should().Be(0);
        design.Colors.Should().HaveCount(1);
        copy.ContentEquals(design).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemDesignTests/*"`
Expected: build error, `GuildEmblemDesign` does not exist.

- [ ] **Step 3: Create `Chaos.DarkAges/Definitions/GuildEmblemProtocol.cs`**

```csharp
namespace Chaos.DarkAges.Definitions;

/// <summary>Fixed numbers for guild emblems, shared by the server and the client.</summary>
public static class GuildEmblemProtocol
{
    /// <summary>The emblem's width and height, in pixels: the size of a world list emblem.</summary>
    public const int SIZE = 11;

    /// <summary>The emblem's total pixel count.</summary>
    public const int PIXEL_COUNT = SIZE * SIZE;

    /// <summary>The most colors one emblem may use, besides see-through.</summary>
    public const int MAX_COLORS = GuildCloakProtocol.MAX_COLORS;

    /// <summary>What Tibbs charges for the guild emblem.</summary>
    public const int PRICE = 5_000_000;

    /// <summary>The guild hall property that records the purchase.</summary>
    public const string DEED_PROPERTY = "emblem";

    /// <summary>The reserved emblem key a member shows to display their guild's emblem. No emblem file may use it.</summary>
    public const string EMBLEM_KEY = "guild";

    /// <summary>The first color of a new emblem: gold.</summary>
    public static readonly GuildCloakColor DefaultColor = new(212, 175, 55);
}
```

- [ ] **Step 4: Create `Chaos.DarkAges/Definitions/GuildEmblemDesign.cs`**

```csharp
namespace Chaos.DarkAges.Definitions;

/// <summary>
///     A painted guild emblem: 1 to <see cref="GuildEmblemProtocol.MAX_COLORS" /> colors and an 11 × 11 grid of color
///     numbers, row-major, one byte per pixel. <c>0</c> is see-through; <c>1</c> up to the color count picks a color.
///     Record-generated equality compares <see cref="Colors" /> and <see cref="Pixels" /> by reference, so compare designs
///     with <see cref="ContentEquals" />.
/// </summary>
public sealed record GuildEmblemDesign
{
    /// <summary>The emblem's palette: 1 to <see cref="GuildEmblemProtocol.MAX_COLORS" /> colors.</summary>
    public List<GuildCloakColor> Colors { get; set; } = [];

    /// <summary>The color-number grid, row-major, <see cref="GuildEmblemProtocol.PIXEL_COUNT" /> pixels.</summary>
    public byte[] Pixels { get; set; } = [];

    /// <summary>The editor's starting emblem: every pixel see-through, and one gold color ready to paint with.</summary>
    public static GuildEmblemDesign CreateDefault()
        => new()
        {
            Colors = [GuildEmblemProtocol.DefaultColor],
            Pixels = new byte[GuildEmblemProtocol.PIXEL_COUNT]
        };

    /// <summary>True when both emblems have the same colors in the same order and the same pixels.</summary>
    public bool ContentEquals(GuildEmblemDesign other) => Colors.SequenceEqual(other.Colors) && Pixels.AsSpan().SequenceEqual(other.Pixels);

    /// <summary>Returns an independent copy with its own colors list and pixel array.</summary>
    public GuildEmblemDesign DeepCopy()
        => new()
        {
            Colors = [..Colors],
            Pixels = (byte[])Pixels.Clone()
        };

    /// <summary>True when at least one pixel is painted.</summary>
    public bool HasPaint() => Pixels is not null && Pixels.AsSpan().IndexOfAnyExcept((byte)0) >= 0;

    /// <summary>True when the color count, the grid size and every color number are in range.</summary>
    public bool IsValid()
    {
        if (Colors is null || (Colors.Count < 1) || (Colors.Count > GuildEmblemProtocol.MAX_COLORS))
            return false;

        if (Pixels is null || (Pixels.Length != GuildEmblemProtocol.PIXEL_COUNT))
            return false;

        foreach (var value in Pixels)
            if (value > Colors.Count)
                return false;

        return true;
    }
}
```

- [ ] **Step 5: Add `GuildDesignKind` to `Chaos.DarkAges/Definitions/Enums.cs`**, directly after the `GuildCloakReviewAction` enum:

```csharp
/// <summary>Which kind of guild design a review list entry holds.</summary>
public enum GuildDesignKind : byte
{
    Cloak = 0,
    Emblem = 1
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemDesignTests/*"`
Expected: 4 passed.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/GuildEmblemProtocol.cs", "Chaos.DarkAges/Definitions/GuildEmblemDesign.cs", "Chaos.DarkAges/Definitions/Enums.cs", "Tests/Chaos.Tests/GuildEmblem/GuildEmblemDesignTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/GuildEmblemDesignTests/*\"", "acceptanceCriteria": ["default design valid, see-through, one gold color", "IsValid refuses bad size, 0 or >6 colors, out-of-range values", "HasPaint only when a pixel is non-zero", "DeepCopy independent, ContentEquals by content"], "modelTier": "mechanical"}
```

---

### Task 2: Messages — opcodes, emblem messages, review kind, book and world list fields

**Goal:** Every emblem packet exists and round-trips; the review list, emblem book and world list carry the new fields; the client version goes to 760.

**Files:**
- Modify: `$SRV/Chaos.Networking.Abstractions/Definitions/Enums.cs`
- Create: `$SRV/Chaos.Networking/Converters/GuildEmblemDesignCodec.cs`
- Create: `$SRV/Chaos.Networking/Entities/Server/GuildEmblemEditorArgs.cs`, `$SRV/Chaos.Networking/Converters/Server/GuildEmblemEditorConverter.cs`
- Create: `$SRV/Chaos.Networking/Entities/Server/GuildEmblemDesignArgs.cs`, `$SRV/Chaos.Networking/Converters/Server/GuildEmblemDesignConverter.cs`
- Create: `$SRV/Chaos.Networking/Entities/Client/GuildEmblemEditorInteractionArgs.cs`, `$SRV/Chaos.Networking/Converters/Client/GuildEmblemEditorInteractionConverter.cs`
- Create: `$SRV/Chaos.Networking/Entities/Client/GuildEmblemDesignRequestArgs.cs`, `$SRV/Chaos.Networking/Converters/Client/GuildEmblemDesignRequestConverter.cs`
- Modify: `$SRV/Chaos.Networking/Entities/Server/GuildCloakReviewEntry.cs`, `$SRV/Chaos.Networking/Converters/Server/GuildCloakReviewListConverter.cs`
- Modify: `$SRV/Chaos.Networking/Entities/Server/EmblemBookArgs.cs`, `$SRV/Chaos.Networking/Converters/Server/EmblemBookConverter.cs`
- Modify: `$SRV/Chaos.Networking/Entities/Server/WorldListMemberInfo.cs`, `$SRV/Chaos.Networking/Converters/Server/WorldListConverter.cs`
- Modify: `$SRV/Chaos.DarkAges/Definitions/CONSTANTS.cs` (`CLIENT_VERSION` 759 → 760)
- Modify: `$SRV/Chaos/Networking/Abstractions/IChaosWorldClient.cs`, `$SRV/Chaos/Networking/ChaosWorldClient.cs`
- Modify: `$SRV/Chaos/Services/Emblems/EmblemService.cs` (`EntryBytes` only)
- Test: `$SRV/Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs` (new), `WorldListConverterTests.cs`, `EmblemBookConverterTests.cs`

**Acceptance Criteria:**
- [ ] The four new messages round-trip; unknown editor types, actions and review kinds throw `ArgumentOutOfRangeException`
- [ ] Opcodes 133, 134 (client) and 138, 139 (server) are each defined once
- [ ] A review list with a cloak and an emblem round-trips in order
- [ ] An emblem book entry keeps `Guild` and `GuildEmblemId`
- [ ] The world list carries `GuildEmblemId` after the emblem name; a record without it reads 0; all existing `WorldListConverterTests` still pass
- [ ] `CONSTANTS.CLIENT_VERSION` is 760

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Networking/*/*"` → all pass

**Steps:**

- [ ] **Step 1: Check the opcodes are still free**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server
grep -n "= 13[3-4],\|= 13[8-9]," Chaos.Networking.Abstractions/Definitions/Enums.cs
```

Expected: no output. If a value is taken, use the next free ones and change every number in this plan that names them (this task's tests and the enum entries).

- [ ] **Step 2: Write the failing tests** — create `Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs`:

```csharp
#region
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Converters.Client;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Networking;

public sealed class GuildEmblemPacketConverterTests
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

    private static bool Throws<T>(PacketConverterBase<T> converter, byte[] bytes) where T: class, IPacketSerializable
    {
        try
        {
            var reader = new SpanReader(Enc, bytes);
            converter.Deserialize(ref reader);

            return false;
        } catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    private static GuildEmblemDesign Sample()
    {
        var design = GuildEmblemDesign.CreateDefault();
        design.Colors.Add(new GuildCloakColor(140, 20, 30));
        design.Pixels[0] = 1;
        design.Pixels[GuildEmblemProtocol.PIXEL_COUNT - 1] = 2;

        return design;
    }

    [Test]
    public void Editor_open_round_trips_the_guild_name_and_emblem()
    {
        var original = new GuildEmblemEditorArgs
        {
            Type = GuildCloakEditorType.Open,
            Status = GuildCloakStatus.Rejected,
            RejectionReason = "Too plain.",
            GuildName = "Richards",
            Design = Sample()
        };

        RoundTrip(new GuildEmblemEditorConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Editor_status_carries_no_emblem()
    {
        var original = new GuildEmblemEditorArgs
        {
            Type = GuildCloakEditorType.Status,
            Status = GuildCloakStatus.Waiting,
            GuildName = "ignored",
            Design = Sample()
        };

        var read = RoundTrip(new GuildEmblemEditorConverter(), original);

        read.Status.Should().Be(GuildCloakStatus.Waiting);
        read.GuildName.Should().BeEmpty();
        read.Design.Pixels.Should().BeEmpty();
    }

    [Test]
    public void Editor_interaction_round_trips()
    {
        var original = new GuildEmblemEditorInteractionArgs
        {
            Action = GuildCloakEditorAction.Submit,
            Design = Sample()
        };

        RoundTrip(new GuildEmblemEditorInteractionConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Design_request_and_design_round_trip()
    {
        var request = new GuildEmblemDesignRequestArgs { DesignId = 42 };
        var design = new GuildEmblemDesignArgs { DesignId = 42, Design = Sample() };

        RoundTrip(new GuildEmblemDesignRequestConverter(), request).Should().BeEquivalentTo(request);
        RoundTrip(new GuildEmblemDesignConverter(), design).Should().BeEquivalentTo(design);
    }

    [Test]
    public void Review_list_round_trips_a_cloak_and_an_emblem_in_order()
    {
        var original = new GuildCloakReviewListArgs
        {
            Type = GuildCloakReviewListType.Open,
            Entries =
            [
                new GuildCloakReviewEntry
                {
                    Kind = GuildDesignKind.Cloak,
                    SubmissionId = 5,
                    GuildName = "Moonveil",
                    LeaderName = "Iglis",
                    SubmittedAtUtc = new DateTime(2026, 9, 28, 18, 0, 0, DateTimeKind.Utc),
                    Design = GuildCloakDesign.CreateDefault()
                },
                new GuildCloakReviewEntry
                {
                    Kind = GuildDesignKind.Emblem,
                    SubmissionId = 6,
                    GuildName = "Richards",
                    LeaderName = "Dose",
                    SubmittedAtUtc = new DateTime(2026, 9, 28, 18, 40, 0, DateTimeKind.Utc),
                    EmblemDesign = Sample()
                }
            ]
        };

        RoundTrip(new GuildCloakReviewListConverter(), original)
            .Should()
            .BeEquivalentTo(original, options => options.WithStrictOrdering());
    }

    [Test]
    public void Unknown_types_actions_and_kinds_throw()
    {
        byte[] zeros = [9, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        Throws(new GuildEmblemEditorConverter(), zeros).Should().BeTrue();
        Throws(new GuildEmblemEditorInteractionConverter(), zeros).Should().BeTrue();

        //review list: type Open, one entry, kind 9
        Throws(new GuildCloakReviewListConverter(), [0, 1, 9, 0, 0, 0, 0, 0, 0, 0]).Should().BeTrue();
    }

    [Test]
    public void Opcodes_are_defined_once()
    {
        foreach (var value in new byte[] { 133, 134 })
            Enum.GetValues<ClientOpCode>().Count(code => (byte)code == value).Should().Be(1, $"client opcode {value}");

        foreach (var value in new byte[] { 138, 139 })
            Enum.GetValues<ServerOpCode>().Count(code => (byte)code == value).Should().Be(1, $"server opcode {value}");

        ((byte)ClientOpCode.GuildEmblemEditorInteraction).Should().Be(133);
        ((byte)ServerOpCode.GuildEmblemDesign).Should().Be(139);
    }

    [Test]
    public void Client_version_is_760()
        => CONSTANTS.CLIENT_VERSION.Should().Be(760);
}
```

Add to `Tests/Chaos.Tests/Networking/WorldListConverterTests.cs` (inside the class, after the last test):

```csharp
    [Test]
    public void Guild_emblem_id_round_trips_after_the_emblem_name()
    {
        var args = Sample();
        args.CountryList.First().EmblemName = "Richards";
        args.CountryList.First().GuildEmblemId = 42;

        var read = Read(Write(args)).CountryList.ToList();

        read[0].EmblemName.Should().Be("Richards");
        read[0].GuildEmblemId.Should().Be(42);
        read[1].GuildEmblemId.Should().Be(0);
        read[1].Name.Should().Be("Mirelle");
    }

    [Test]
    public void A_record_without_the_guild_emblem_id_reads_as_zero()
    {
        byte[] section =
        [
            7, (byte)AdvClass.Bard, (byte)Continent.Medenia, 1, 0x00, 0x0D, 1, (byte)'X',
            6, (byte)AdvClass.Druid, (byte)Continent.Temuair, 0, 0x00, 0x04, 0
        ];

        var read = Read([.. EntriesOnly(Write(Sample()), Sample()), .. section]).CountryList.ToList();

        read[0].EmblemName.Should().Be("X");
        read[0].GuildEmblemId.Should().Be(0);
        read[1].EmblemArt.Should().Be(4);
        read[1].GuildEmblemId.Should().Be(0);
    }
```

Add to `Tests/Chaos.Tests/Networking/EmblemBookConverterTests.cs` (inside the class):

```csharp
    [Test]
    public void EmblemBook_round_trip_keeps_the_guild_flag_and_emblem_id()
    {
        var args = new EmblemBookArgs
        {
            ShownKey = "guild",
            Entries =
            [
                new EmblemBookEntry
                {
                    Key = "guild", Name = "Richards", Description = "The emblem of your guild.", Owned = true, Guild = true,
                    GuildEmblemId = 42
                },
                new EmblemBookEntry { Key = "carnun", Art = 6, Name = "Carnun Slayer", Owned = true }
            ]
        };

        var read = RoundTrip(new EmblemBookConverter(), args);

        read.ShownKey.Should().Be("guild");
        read.Entries.Should().BeEquivalentTo(args.Entries, options => options.WithStrictOrdering());
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemPacketConverterTests/*"`
Expected: build errors (missing types and members).

- [ ] **Step 4: Add the opcodes** — in `Chaos.Networking.Abstractions/Definitions/Enums.cs`, add after `QuestLogRequest = 132,` in `ClientOpCode`:

```csharp

    /// <summary>
    ///     A save or submit from the guild emblem editor. The server checks the leader rank, the purchase, the rate and the
    ///     emblem.
    ///     <br />
    ///     Hex value: 0x85
    /// </summary>
    GuildEmblemEditorInteraction = 133,

    /// <summary>
    ///     Asks for one approved guild emblem by its id, after a world list or emblem book named it.
    ///     <br />
    ///     Hex value: 0x86
    /// </summary>
    GuildEmblemDesignRequest = 134,
```

and after `HotkeyHelpOpen = 137,` in `ServerOpCode`:

```csharp

    /// <summary>
    ///     Opens the guild emblem editor for the leader, or updates its status line after a save or submit.
    ///     <br />
    ///     Hex value: 0x8A
    /// </summary>
    GuildEmblemEditor = 138,

    /// <summary>
    ///     One approved guild emblem, in reply to <see cref="ClientOpCode.GuildEmblemDesignRequest" />.
    ///     <br />
    ///     Hex value: 0x8B
    /// </summary>
    GuildEmblemDesign = 139,
```

- [ ] **Step 5: Create `Chaos.Networking/Converters/GuildEmblemDesignCodec.cs`**

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;

namespace Chaos.Networking.Converters;

/// <summary>
///     Reads and writes one <see cref="GuildEmblemDesign" />: a color count, 3 bytes per color, then the pixel grid with a
///     16-bit length. Values are not checked here; the server calls <see cref="GuildEmblemDesign.IsValid" />.
/// </summary>
internal static class GuildEmblemDesignCodec
{
    public static GuildEmblemDesign Read(ref SpanReader reader)
    {
        var count = reader.ReadByte();
        var colors = new List<GuildCloakColor>(count);

        for (var i = 0; i < count; i++)
        {
            var r = reader.ReadByte();
            var g = reader.ReadByte();
            var b = reader.ReadByte();
            colors.Add(new GuildCloakColor(r, g, b));
        }

        return new GuildEmblemDesign
        {
            Colors = colors,
            Pixels = reader.ReadData16()
        };
    }

    public static void Write(ref SpanWriter writer, GuildEmblemDesign design)
    {
        var colors = design.Colors ?? [];

        if (colors.Count > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(design), colors.Count, "Too many guild emblem colors");

        writer.WriteByte((byte)colors.Count);

        foreach (var color in colors)
        {
            writer.WriteByte(color.R);
            writer.WriteByte(color.G);
            writer.WriteByte(color.B);
        }

        writer.WriteData16(design.Pixels ?? []);
    }
}
```

- [ ] **Step 6: Create the server → client messages**

`Chaos.Networking/Entities/Server/GuildEmblemEditorArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.GuildEmblemEditor" /> packet. Open carries the guild's
///     name and the emblem to edit; Status carries only the status line.
/// </summary>
public sealed record GuildEmblemEditorArgs : IPacketSerializable
{
    /// <summary>Whether this message opens the editor or only updates its status line.</summary>
    public required GuildCloakEditorType Type { get; set; }

    /// <summary>The emblem's current review status.</summary>
    public GuildCloakStatus Status { get; set; }

    /// <summary>Rejected only: the admin's reason.</summary>
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>Open only: the guild's name, for the window title.</summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>Open only: the draft, else the approved emblem, else the default emblem.</summary>
    public GuildEmblemDesign Design { get; set; } = new();
}
```

`Chaos.Networking/Converters/Server/GuildEmblemEditorConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildEmblemEditorArgs" /></summary>
public sealed class GuildEmblemEditorConverter : PacketConverterBase<GuildEmblemEditorArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.GuildEmblemEditor;

    /// <inheritdoc />
    public override GuildEmblemEditorArgs Deserialize(ref SpanReader reader)
    {
        var type = (GuildCloakEditorType)reader.ReadByte();

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown guild emblem editor type");

        var args = new GuildEmblemEditorArgs
        {
            Type = type,
            Status = (GuildCloakStatus)reader.ReadByte(),
            RejectionReason = reader.ReadString16()
        };

        if (type == GuildCloakEditorType.Open)
        {
            args.GuildName = reader.ReadString8();
            args.Design = GuildEmblemDesignCodec.Read(ref reader);
        }

        return args;
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildEmblemEditorArgs args)
    {
        writer.WriteByte((byte)args.Type);
        writer.WriteByte((byte)args.Status);
        writer.WriteString16(args.RejectionReason ?? string.Empty);

        if (args.Type != GuildCloakEditorType.Open)
            return;

        writer.WriteString8(args.GuildName ?? string.Empty);
        GuildEmblemDesignCodec.Write(ref writer, args.Design ?? new GuildEmblemDesign());
    }
}
```

`Chaos.Networking/Entities/Server/GuildEmblemDesignArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>Represents the serialization of the <see cref="ServerOpCode.GuildEmblemDesign" /> packet: one approved emblem.</summary>
public sealed record GuildEmblemDesignArgs : IPacketSerializable
{
    /// <summary>The id of the emblem.</summary>
    public int DesignId { get; set; }

    /// <summary>The emblem's contents.</summary>
    public GuildEmblemDesign Design { get; set; } = new();
}
```

`Chaos.Networking/Converters/Server/GuildEmblemDesignConverter.cs`:

```csharp
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildEmblemDesignArgs" /></summary>
public sealed class GuildEmblemDesignConverter : PacketConverterBase<GuildEmblemDesignArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.GuildEmblemDesign;

    /// <inheritdoc />
    public override GuildEmblemDesignArgs Deserialize(ref SpanReader reader)
    {
        var designId = reader.ReadInt32();

        return new GuildEmblemDesignArgs
        {
            DesignId = designId,
            Design = GuildEmblemDesignCodec.Read(ref reader)
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildEmblemDesignArgs args)
    {
        writer.WriteInt32(args.DesignId);
        GuildEmblemDesignCodec.Write(ref writer, args.Design ?? new());
    }
}
```

- [ ] **Step 7: Create the client → server messages**

`Chaos.Networking/Entities/Client/GuildEmblemEditorInteractionArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>
///     Represents the serialization of the <see cref="ClientOpCode.GuildEmblemEditorInteraction" /> packet: a save or a
///     submit with the whole emblem. The server checks the sender and the emblem.
/// </summary>
public sealed record GuildEmblemEditorInteractionArgs : IPacketSerializable
{
    /// <summary>Save draft or submit.</summary>
    public required GuildCloakEditorAction Action { get; set; }

    /// <summary>The emblem being saved or submitted.</summary>
    public GuildEmblemDesign Design { get; set; } = new();
}
```

`Chaos.Networking/Converters/Client/GuildEmblemEditorInteractionConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildEmblemEditorInteractionArgs" /></summary>
public sealed class GuildEmblemEditorInteractionConverter : PacketConverterBase<GuildEmblemEditorInteractionArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.GuildEmblemEditorInteraction;

    /// <inheritdoc />
    public override GuildEmblemEditorInteractionArgs Deserialize(ref SpanReader reader)
    {
        var action = (GuildCloakEditorAction)reader.ReadByte();

        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(nameof(reader), action, "Unknown guild emblem editor action");

        return new GuildEmblemEditorInteractionArgs
        {
            Action = action,
            Design = GuildEmblemDesignCodec.Read(ref reader)
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildEmblemEditorInteractionArgs args)
    {
        writer.WriteByte((byte)args.Action);
        GuildEmblemDesignCodec.Write(ref writer, args.Design ?? new());
    }
}
```

`Chaos.Networking/Entities/Client/GuildEmblemDesignRequestArgs.cs`:

```csharp
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>Represents the serialization of the <see cref="ClientOpCode.GuildEmblemDesignRequest" /> packet.</summary>
public sealed record GuildEmblemDesignRequestArgs : IPacketSerializable
{
    /// <summary>The id of the emblem being requested.</summary>
    public int DesignId { get; set; }
}
```

`Chaos.Networking/Converters/Client/GuildEmblemDesignRequestConverter.cs`:

```csharp
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildEmblemDesignRequestArgs" /></summary>
public sealed class GuildEmblemDesignRequestConverter : PacketConverterBase<GuildEmblemDesignRequestArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.GuildEmblemDesignRequest;

    /// <inheritdoc />
    public override GuildEmblemDesignRequestArgs Deserialize(ref SpanReader reader) => new() { DesignId = reader.ReadInt32() };

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildEmblemDesignRequestArgs args) => writer.WriteInt32(args.DesignId);
}
```

Before writing these files, open the matching cloak file (for example `GuildCloakDesignRequestConverter.cs`) and copy its `using` lines if they differ from the ones above; the namespaces must match the cloak files exactly.

- [ ] **Step 8: Review list kind** — in `Chaos.Networking/Entities/Server/GuildCloakReviewEntry.cs`, add these two properties and change the `Design` summary:

```csharp
    /// <summary>Whether this entry is a cloak design or an emblem.</summary>
    public GuildDesignKind Kind { get; set; }

    /// <summary>Cloak entries: the submitted design.</summary>
    public GuildCloakDesign Design { get; set; } = new();

    /// <summary>Emblem entries: the submitted emblem.</summary>
    public GuildEmblemDesign EmblemDesign { get; set; } = new();
```

In `GuildCloakReviewListConverter.Deserialize`, the loop body becomes:

```csharp
        for (var i = 0; i < count; i++)
        {
            var kind = (GuildDesignKind)reader.ReadByte();

            if (!Enum.IsDefined(kind))
                throw new ArgumentOutOfRangeException(nameof(reader), kind, "Unknown guild design kind");

            var entry = new GuildCloakReviewEntry
            {
                Kind = kind,
                SubmissionId = reader.ReadInt32(),
                GuildName = reader.ReadString8(),
                LeaderName = reader.ReadString8(),
                SubmittedAtUtc = new DateTime((long)reader.ReadUInt64(), DateTimeKind.Utc)
            };

            if (kind == GuildDesignKind.Emblem)
                entry.EmblemDesign = GuildEmblemDesignCodec.Read(ref reader);
            else
                entry.Design = GuildCloakDesignCodec.Read(ref reader);

            entries.Add(entry);
        }
```

In `Serialize`, the loop body becomes:

```csharp
        foreach (var entry in entries)
        {
            writer.WriteByte((byte)entry.Kind);
            writer.WriteInt32(entry.SubmissionId);
            writer.WriteString8(entry.GuildName ?? string.Empty);
            writer.WriteString8(entry.LeaderName ?? string.Empty);
            writer.WriteUInt64((ulong)entry.SubmittedAtUtc.Ticks);

            if (entry.Kind == GuildDesignKind.Emblem)
                GuildEmblemDesignCodec.Write(ref writer, entry.EmblemDesign ?? new());
            else
                GuildCloakDesignCodec.Write(ref writer, entry.Design ?? new());
        }
```

Object initializers evaluate in order, so the reads above happen in the written order.

- [ ] **Step 9: Emblem book fields** — in `Chaos.Networking/Entities/Server/EmblemBookArgs.cs`, add to `EmblemBookEntry`:

```csharp
    /// <summary>Whether this is the player's guild emblem (key <c>guild</c>), drawn from <see cref="GuildEmblemId" />.</summary>
    public bool Guild { get; set; }

    /// <summary>For the guild emblem, the approved emblem's id; 0 for every other emblem.</summary>
    public int GuildEmblemId { get; set; }
```

In `EmblemBookConverter`: add `private const byte GUILD_FLAG = 0b_0000_0100;`. In `Deserialize`, after `var holder = reader.ReadString8();` add `var guildEmblemId = reader.ReadInt32();`, and in the entry initializer add `Guild = (flags & GUILD_FLAG) != 0,` and `GuildEmblemId = guildEmblemId`. In `Serialize`, add `if (entry.Guild) flags |= GUILD_FLAG;` next to the other flags, and after `writer.WriteString8(entry.Holder);` add `writer.WriteInt32(entry.GuildEmblemId);`.

Each entry is now 4 bytes longer, so the server's book-size count must grow too. In `Chaos/Services/Emblems/EmblemService.cs`, `EntryBytes` gets `+ 4` at the end of its expression, and its comment ends with ", i32 guild emblem id". Otherwise the book trimming under-counts and `EmblemServiceTests`' size check can fail. Add `Chaos/Services/Emblems/EmblemService.cs` to this task's files.

- [ ] **Step 10: World list field** — in `Chaos.Networking/Entities/Server/WorldListMemberInfo.cs`, after `EmblemName`:

```csharp
    /// <summary>
    ///     The approved guild emblem shown in the player's world list cell, or 0. When set, the client draws it instead of
    ///     <see cref="EmblemArt" />, and <see cref="EmblemName" /> is the guild's name.
    /// </summary>
    public int GuildEmblemId { get; set; }
```

In `WorldListConverter`: change the comment above `EXTRAS_LENGTH` to end with "…ending with the art number and the name (a String8) of the emblem shown in that player's world list cell, then the shown guild emblem's id (an Int32, 0 for none)." Replace the name block in `ReadExtras` with:

```csharp
            //the name must fit inside the record, or it is left empty; the guild emblem id follows it when there is room
            if (length >= 6)
            {
                var nameLength = reader.ReadByte();

                if (reader.Position + nameLength <= end)
                {
                    member.EmblemName = reader.Encoding.GetString(reader.ReadBytes(nameLength));

                    if (reader.Position + 4 <= end)
                        member.GuildEmblemId = reader.ReadInt32();
                }
            }
```

In `Serialize`, the record becomes:

```csharp
            writer.WriteByte((byte)(EXTRAS_LENGTH + 1 + emblemName.Length + 4));
            writer.WriteByte((byte)user.AdvClass);
            writer.WriteByte((byte)user.Continent);
            writer.WriteByte(user.HasAbility ? HAS_ABILITY_FLAG : (byte)0);
            writer.WriteUInt16(user.EmblemArt);
            writer.WriteData8(emblemName);
            writer.WriteInt32(user.GuildEmblemId);
```

The record is now 4 bytes longer, so fix the test helper that strips the records. In `Tests/Chaos.Tests/Networking/WorldListConverterTests.cs`, `EntriesOnly` becomes:

```csharp
    /// <summary>What an old server sends: the entries without the trailing section (11 bytes per player, plus the emblem name).</summary>
    private static byte[] EntriesOnly(byte[] bytes, WorldListArgs args)
        => bytes[..^args.CountryList.Sum(m => 11 + Enc.GetByteCount(m.EmblemName))];
```

- [ ] **Step 11: Client version** — in `Chaos.DarkAges/Definitions/CONSTANTS.cs`, change `public const ushort CLIENT_VERSION = 759;` to `760`.

- [ ] **Step 12: World client sends** — in `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, next to `SendGuildCloakReviewList`:

```csharp
    /// <summary>Opens the guild emblem editor or updates its status line.</summary>
    void SendGuildEmblemEditor(GuildEmblemEditorArgs args);

    /// <summary>Sends one approved guild emblem.</summary>
    void SendGuildEmblemDesign(GuildEmblemDesignArgs args);
```

In `Chaos/Networking/ChaosWorldClient.cs`, next to `SendGuildCloakReviewList`:

```csharp
    /// <inheritdoc />
    public void SendGuildEmblemEditor(GuildEmblemEditorArgs args) => Send(args);

    /// <inheritdoc />
    public void SendGuildEmblemDesign(GuildEmblemDesignArgs args) => Send(args);
```

Then run `grep -rn "IChaosWorldClient$\|: IChaosWorldClient\b\|, IChaosWorldClient\b" --include=*.cs .` from `$SRV`. If any class other than `ChaosWorldClient` implements the interface, add the same two methods to it.

- [ ] **Step 13: Run the networking tests**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Networking/*/*"`
Expected: all pass, including the existing `GuildCloakPacketConverterTests`, `WorldListConverterTests` and `EmblemBookConverterTests`.
Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Emblems/*/*"`
Expected: all pass (the book-size check uses `EntryBytes`).

```json:metadata
{"files": ["Chaos/Services/Emblems/EmblemService.cs", "Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos.Networking/Converters/GuildEmblemDesignCodec.cs", "Chaos.Networking/Entities/Server/GuildEmblemEditorArgs.cs", "Chaos.Networking/Converters/Server/GuildEmblemEditorConverter.cs", "Chaos.Networking/Entities/Server/GuildEmblemDesignArgs.cs", "Chaos.Networking/Converters/Server/GuildEmblemDesignConverter.cs", "Chaos.Networking/Entities/Client/GuildEmblemEditorInteractionArgs.cs", "Chaos.Networking/Converters/Client/GuildEmblemEditorInteractionConverter.cs", "Chaos.Networking/Entities/Client/GuildEmblemDesignRequestArgs.cs", "Chaos.Networking/Converters/Client/GuildEmblemDesignRequestConverter.cs", "Chaos.Networking/Entities/Server/GuildCloakReviewEntry.cs", "Chaos.Networking/Converters/Server/GuildCloakReviewListConverter.cs", "Chaos.Networking/Entities/Server/EmblemBookArgs.cs", "Chaos.Networking/Converters/Server/EmblemBookConverter.cs", "Chaos.Networking/Entities/Server/WorldListMemberInfo.cs", "Chaos.Networking/Converters/Server/WorldListConverter.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos/Networking/ChaosWorldClient.cs", "Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs", "Tests/Chaos.Tests/Networking/WorldListConverterTests.cs", "Tests/Chaos.Tests/Networking/EmblemBookConverterTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.Networking/*/*\"", "acceptanceCriteria": ["four new messages round-trip; unknown type/action/kind throws", "opcodes 133,134,138,139 defined once", "mixed review list round-trips in order", "emblem book keeps Guild and GuildEmblemId", "world list carries GuildEmblemId after the name, old records read 0, existing tests pass", "CLIENT_VERSION is 760"], "modelTier": "standard"}
```

---

### Task 3: Emblem storage in `GuildCloakState`

**Goal:** Each guild's record holds an emblem draft, waiting, approved and last rejection; submissions share the cloak id counter; decisions find either kind by id; one mixed waiting list.

**Files:**
- Modify: `$SRV/Chaos/Models/World/GuildCloakState.cs`
- Test: `$SRV/Tests/Chaos.Tests/GuildEmblem/GuildEmblemStateTests.cs`

**Acceptance Criteria:**
- [ ] Emblem editor view starts from the default, then the draft; waiting/approved/rejected statuses as for cloaks
- [ ] Cloak and emblem submissions share one id counter
- [ ] An unchanged emblem resubmit keeps its id; a changed one gets a new id and the old id can no longer be decided
- [ ] `TryApprove`/`TryReject` report the kind; approving an emblem leaves a waiting cloak alone
- [ ] `WaitingDesigns` lists both kinds, oldest first
- [ ] Clearing the emblem leaves the cloak; `RemoveGuild` drops both
- [ ] A JSON file with no emblem part loads, and emblem state round-trips through JSON
- [ ] All existing `GuildCloakStateTests` still pass

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemStateTests/*"` and `--treenode-filter "/*/*/GuildCloakStateTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** — create `Tests/Chaos.Tests/GuildEmblem/GuildEmblemStateTests.cs`:

```csharp
#region
using System.Text.Json;
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildEmblem;

public sealed class GuildEmblemStateTests
{
    private const string GUILD = "Shinebox";
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static GuildEmblemDesign Painted(int pixel = 60)
    {
        var design = GuildEmblemDesign.CreateDefault();
        design.Pixels[pixel] = 1;

        return design;
    }

    [Test]
    public void Emblem_editor_view_starts_from_the_default_then_the_draft()
    {
        var state = new GuildCloakState();

        var first = state.EmblemEditorView(GUILD);
        first.Design.ContentEquals(GuildEmblemDesign.CreateDefault()).Should().BeTrue();
        first.Status.Should().Be(GuildCloakStatus.Draft);

        state.SaveEmblemDraft(GUILD, Painted());

        var second = state.EmblemEditorView(GUILD);
        second.Design.ContentEquals(Painted()).Should().BeTrue();
        second.Status.Should().Be(GuildCloakStatus.Draft);
    }

    [Test]
    public void Emblem_and_cloak_submissions_share_one_id_counter()
    {
        var state = new GuildCloakState();

        var cloakId = state.Submit(GUILD, "Stahli", GuildCloakDesign.CreateDefault(), Now);
        var emblemId = state.SubmitEmblem(GUILD, "Stahli", Painted(), Now);

        emblemId.Should().Be(cloakId + 1);
        state.IsWaiting(GUILD).Should().BeTrue();
        state.IsEmblemWaiting(GUILD).Should().BeTrue();
        state.EmblemEditorView(GUILD).Status.Should().Be(GuildCloakStatus.Waiting);
    }

    [Test]
    public void An_unchanged_resubmit_keeps_its_id_and_a_changed_one_replaces_it()
    {
        var state = new GuildCloakState();

        var first = state.SubmitEmblem(GUILD, "Stahli", Painted(), Now);
        state.SubmitEmblem(GUILD, "Stahli", Painted(), Now).Should().Be(first);

        var changed = state.SubmitEmblem(GUILD, "Stahli", Painted(61), Now);

        changed.Should().NotBe(first);
        state.TryApprove(first, "Sichi", Now, out _, out _).Should().BeFalse();
        state.TryApprove(changed, "Sichi", Now, out _, out _).Should().BeTrue();
    }

    [Test]
    public void Approving_an_emblem_moves_it_to_approved_and_leaves_the_cloak_waiting()
    {
        var state = new GuildCloakState();
        state.Submit(GUILD, "Stahli", GuildCloakDesign.CreateDefault(), Now);
        var emblemId = state.SubmitEmblem(GUILD, "Stahli", Painted(), Now);

        state.TryApprove(emblemId, "Sichi", Now, out var guildName, out var kind).Should().BeTrue();

        guildName.Should().Be(GUILD);
        kind.Should().Be(GuildDesignKind.Emblem);
        state.ApprovedEmblemIdOf(GUILD).Should().Be(emblemId);
        state.IsEmblemWaiting(GUILD).Should().BeFalse();
        state.IsWaiting(GUILD).Should().BeTrue();
        state.ApprovedIdOf(GUILD).Should().Be(0);
        state.FindApprovedEmblem(emblemId)!.ContentEquals(Painted()).Should().BeTrue();
        state.FindApproved(emblemId).Should().BeNull();
        state.EmblemEditorView(GUILD).Status.Should().Be(GuildCloakStatus.Approved);
    }

    [Test]
    public void Approving_a_cloak_reports_the_cloak_kind()
    {
        var state = new GuildCloakState();
        var cloakId = state.Submit(GUILD, "Stahli", GuildCloakDesign.CreateDefault(), Now);

        state.TryApprove(cloakId, "Sichi", Now, out _, out var kind).Should().BeTrue();

        kind.Should().Be(GuildDesignKind.Cloak);
        state.ApprovedIdOf(GUILD).Should().Be(cloakId);
        state.ApprovedEmblemIdOf(GUILD).Should().Be(0);
    }

    [Test]
    public void Rejecting_an_emblem_stores_the_reason_for_the_editor()
    {
        var state = new GuildCloakState();
        var id = state.SubmitEmblem(GUILD, "Stahli", Painted(), Now);

        state.TryReject(id, "Sichi", "Too plain.", Now.AddMinutes(1), out _, out var kind).Should().BeTrue();

        kind.Should().Be(GuildDesignKind.Emblem);
        state.IsEmblemWaiting(GUILD).Should().BeFalse();

        var view = state.EmblemEditorView(GUILD);
        view.Status.Should().Be(GuildCloakStatus.Rejected);
        view.Reason.Should().Be("Too plain.");
    }

    [Test]
    public void Waiting_designs_lists_both_kinds_oldest_first()
    {
        var state = new GuildCloakState();
        state.SubmitEmblem("Beta", "Lead", Painted(), Now);
        state.Submit("Alpha", "Lead", GuildCloakDesign.CreateDefault(), Now.AddMinutes(1));

        var list = state.WaitingDesigns(20);

        list.Select(w => (w.GuildName, w.Kind))
            .Should()
            .Equal(("Beta", GuildDesignKind.Emblem), ("Alpha", GuildDesignKind.Cloak));

        list[0].Emblem.Should().NotBeNull();
        list[0].Cloak.Should().BeNull();
        list[1].Cloak.Should().NotBeNull();
        list[1].SubmittedBy.Should().Be("Lead");
    }

    [Test]
    public void Clearing_the_emblem_leaves_the_cloak_and_removing_the_guild_drops_both()
    {
        var state = new GuildCloakState();
        var cloakId = state.Submit(GUILD, "Stahli", GuildCloakDesign.CreateDefault(), Now);
        var emblemId = state.SubmitEmblem(GUILD, "Stahli", Painted(), Now);
        state.TryApprove(cloakId, "Sichi", Now, out _, out _);
        state.TryApprove(emblemId, "Sichi", Now, out _, out _);

        state.ClearApprovedEmblem(GUILD).Should().BeTrue();
        state.ClearApprovedEmblem(GUILD).Should().BeFalse();

        state.ApprovedEmblemIdOf(GUILD).Should().Be(0);
        state.ApprovedIdOf(GUILD).Should().Be(cloakId);

        state.SubmitEmblem(GUILD, "Stahli", Painted(), Now);
        state.RemoveGuild(GUILD);

        state.ApprovedIdOf(GUILD).Should().Be(0);
        state.IsEmblemWaiting(GUILD).Should().BeFalse();
    }

    [Test]
    public void A_file_without_emblems_loads_and_emblem_state_round_trips_through_json()
    {
        const string OLD = """{"NextDesignId":3,"Guilds":{"Shinebox":{"Approved":null,"Draft":null,"LastRejection":null,"Waiting":null}}}""";

        var state = JsonSerializer.Deserialize<GuildCloakState>(OLD)!;
        state.EnsureCaseInsensitive();

        state.ApprovedEmblemIdOf("shinebox").Should().Be(0);
        state.SubmitEmblem(GUILD, "Stahli", Painted(), Now).Should().Be(3);

        var again = JsonSerializer.Deserialize<GuildCloakState>(JsonSerializer.Serialize(state))!;
        again.EnsureCaseInsensitive();

        again.IsEmblemWaiting(GUILD).Should().BeTrue();
        again.NextDesignId.Should().Be(4);
        again.WaitingDesigns(1)[0].Emblem!.ContentEquals(Painted()).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemStateTests/*"`
Expected: build errors (missing members).

- [ ] **Step 3: Add the emblem types** — in `GuildCloakState.cs`, add to `GuildCloakRecord`:

```csharp
        /// <summary>The guild's emblem designs. Null in files saved before guild emblems existed.</summary>
        public GuildEmblemRecord? Emblem { get; set; }
```

and add these nested types after `GuildCloakRejection`:

```csharp
    /// <summary>One guild's emblem designs, with the same four slots as its cloak.</summary>
    public sealed class GuildEmblemRecord
    {
        public GuildEmblemEntry? Approved { get; set; }
        public GuildEmblemDesign? Draft { get; set; }
        public GuildCloakRejection? LastRejection { get; set; }
        public GuildEmblemEntry? Waiting { get; set; }
    }

    /// <summary>A submitted emblem. <see cref="DecidedBy" /> and <see cref="DecidedAtUtc" /> are set when it is approved.</summary>
    public sealed class GuildEmblemEntry
    {
        public DateTime DecidedAtUtc { get; set; }
        public string DecidedBy { get; set; } = string.Empty;
        public GuildEmblemDesign Design { get; set; } = new();
        public int Id { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
    }

    /// <summary>One entry of the mixed review list: a waiting cloak (<see cref="Cloak" />) or emblem (<see cref="Emblem" />).</summary>
    public sealed record GuildWaitingDesign(
        string GuildName,
        GuildDesignKind Kind,
        int Id,
        string SubmittedBy,
        DateTime SubmittedAtUtc,
        GuildCloakDesign? Cloak,
        GuildEmblemDesign? Emblem);
```

Update the class summary to: "Every guild's cloak and emblem designs: the approved one, the one waiting for review, the leader's draft, and the last rejection, for each kind. Cloak and emblem submissions share one id counter. Saved as …" (keep the rest).

- [ ] **Step 4: Add the emblem methods** — add to `GuildCloakState` (keep members in alphabetical order as the file does):

```csharp
    /// <summary>The guild's approved emblem id, or 0.</summary>
    public int ApprovedEmblemIdOf(string? guildName)
        => (guildName is not null) && Guilds.TryGetValue(guildName, out var record) && record.Emblem?.Approved is { } approved
            ? approved.Id
            : 0;

    public bool ClearApprovedEmblem(string guildName)
    {
        if (!Guilds.TryGetValue(guildName, out var record) || record.Emblem?.Approved is null)
            return false;

        record.Emblem.Approved = null;

        return true;
    }

    /// <summary>What the emblem editor opens with and its status line, by the same rules as <see cref="EditorView" />.</summary>
    public (GuildEmblemDesign Design, GuildCloakStatus Status, string Reason) EmblemEditorView(string guildName)
    {
        Guilds.TryGetValue(guildName, out var record);
        var emblem = record?.Emblem;

        var design = (emblem?.Draft ?? emblem?.Approved?.Design ?? GuildEmblemDesign.CreateDefault()).DeepCopy();

        if (emblem is null)
            return (design, GuildCloakStatus.Draft, string.Empty);

        if (emblem.Waiting is not null)
            return (design, GuildCloakStatus.Waiting, string.Empty);

        if (emblem.LastRejection is not null
            && ((emblem.Approved is null) || (emblem.LastRejection.RejectedAtUtc > emblem.Approved.DecidedAtUtc)))
            return (design, GuildCloakStatus.Rejected, emblem.LastRejection.Reason);

        return (design, emblem.Approved is not null ? GuildCloakStatus.Approved : GuildCloakStatus.Draft, string.Empty);
    }

    /// <summary>An approved emblem by its id, or null. Only approved emblems are ever sent to other players.</summary>
    public GuildEmblemDesign? FindApprovedEmblem(int designId)
    {
        foreach (var record in Guilds.Values)
            if (record.Emblem?.Approved?.Id == designId)
                return record.Emblem.Approved.Design;

        return null;
    }

    /// <summary>True when the guild has an emblem waiting for review.</summary>
    public bool IsEmblemWaiting(string guildName)
        => Guilds.TryGetValue(guildName, out var record) && record.Emblem?.Waiting is not null;

    public void SaveEmblemDraft(string guildName, GuildEmblemDesign design) => GetOrAddEmblem(guildName).Draft = design.DeepCopy();

    /// <summary>
    ///     Makes <paramref name="design" /> the guild's waiting emblem, replacing any, and its draft. Returns the new
    ///     submission id (from the counter cloaks use too), or the waiting one's id when the emblem is unchanged.
    /// </summary>
    public int SubmitEmblem(string guildName, string leaderName, GuildEmblemDesign design, DateTime nowUtc)
    {
        var emblem = GetOrAddEmblem(guildName);
        emblem.Draft = design.DeepCopy();

        if (emblem.Waiting is not null && emblem.Waiting.Design.ContentEquals(design))
            return emblem.Waiting.Id;

        var id = NextDesignId++;

        emblem.Waiting = new GuildEmblemEntry
        {
            Id = id,
            Design = design.DeepCopy(),
            SubmittedBy = leaderName,
            SubmittedAtUtc = nowUtc
        };

        return id;
    }

    /// <summary>Every waiting cloak and emblem, oldest first, at most <paramref name="max" />. The designs are the stored ones: copy before sending.</summary>
    public IReadOnlyList<GuildWaitingDesign> WaitingDesigns(int max)
    {
        var waiting = new List<GuildWaitingDesign>();

        foreach ((var guildName, var record) in Guilds)
        {
            if (record.Waiting is { } cloak)
                waiting.Add(
                    new GuildWaitingDesign(
                        guildName,
                        GuildDesignKind.Cloak,
                        cloak.Id,
                        cloak.SubmittedBy,
                        cloak.SubmittedAtUtc,
                        cloak.Design,
                        null));

            if (record.Emblem?.Waiting is { } emblem)
                waiting.Add(
                    new GuildWaitingDesign(
                        guildName,
                        GuildDesignKind.Emblem,
                        emblem.Id,
                        emblem.SubmittedBy,
                        emblem.SubmittedAtUtc,
                        null,
                        emblem.Design));
        }

        return waiting.OrderBy(w => w.SubmittedAtUtc)
                      .ThenBy(w => w.Id)
                      .Take(max)
                      .ToList();
    }

    private GuildEmblemRecord GetOrAddEmblem(string guildName)
    {
        var record = GetOrAdd(guildName);

        return record.Emblem ??= new GuildEmblemRecord();
    }
```

- [ ] **Step 5: Decide either kind by id** — replace `TryApprove`, `TryReject` and `TryFindWaiting` with:

```csharp
    /// <summary>Approves the waiting cloak or emblem with this exact id. False when no guild has it waiting.</summary>
    public bool TryApprove(int submissionId, string adminName, DateTime nowUtc, [MaybeNullWhen(false)] out string guildName)
        => TryApprove(submissionId, adminName, nowUtc, out guildName, out _);

    /// <summary>Approves the waiting cloak or emblem with this exact id, and says which kind it was.</summary>
    public bool TryApprove(
        int submissionId,
        string adminName,
        DateTime nowUtc,
        [MaybeNullWhen(false)] out string guildName,
        out GuildDesignKind kind)
    {
        if (!TryFindWaiting(submissionId, out guildName, out var record, out kind))
            return false;

        if (kind == GuildDesignKind.Emblem)
        {
            var emblem = record.Emblem!;
            var entry = emblem.Waiting!;
            entry.DecidedBy = adminName;
            entry.DecidedAtUtc = nowUtc;
            emblem.Approved = entry;
            emblem.Waiting = null;
            emblem.LastRejection = null;

            return true;
        }

        var cloak = record.Waiting!;
        cloak.DecidedBy = adminName;
        cloak.DecidedAtUtc = nowUtc;
        record.Approved = cloak;
        record.Waiting = null;
        record.LastRejection = null;

        return true;
    }

    /// <summary>Rejects the waiting cloak or emblem with this exact id. False when no guild has it waiting.</summary>
    public bool TryReject(
        int submissionId,
        string adminName,
        string reason,
        DateTime nowUtc,
        [MaybeNullWhen(false)] out string guildName)
        => TryReject(submissionId, adminName, reason, nowUtc, out guildName, out _);

    /// <summary>Rejects the waiting cloak or emblem with this exact id, and says which kind it was.</summary>
    public bool TryReject(
        int submissionId,
        string adminName,
        string reason,
        DateTime nowUtc,
        [MaybeNullWhen(false)] out string guildName,
        out GuildDesignKind kind)
    {
        if (!TryFindWaiting(submissionId, out guildName, out var record, out kind))
            return false;

        var rejection = new GuildCloakRejection
        {
            Id = submissionId,
            Reason = reason,
            RejectedBy = adminName,
            RejectedAtUtc = nowUtc
        };

        if (kind == GuildDesignKind.Emblem)
        {
            record.Emblem!.LastRejection = rejection;
            record.Emblem.Waiting = null;
        } else
        {
            record.LastRejection = rejection;
            record.Waiting = null;
        }

        return true;
    }

    private bool TryFindWaiting(
        int submissionId,
        [MaybeNullWhen(false)] out string guildName,
        [MaybeNullWhen(false)] out GuildCloakRecord record,
        out GuildDesignKind kind)
    {
        foreach (var pair in Guilds)
        {
            if (pair.Value.Waiting?.Id == submissionId)
            {
                guildName = pair.Key;
                record = pair.Value;
                kind = GuildDesignKind.Cloak;

                return true;
            }

            if (pair.Value.Emblem?.Waiting?.Id == submissionId)
            {
                guildName = pair.Key;
                record = pair.Value;
                kind = GuildDesignKind.Emblem;

                return true;
            }
        }

        guildName = null;
        record = null;
        kind = default;

        return false;
    }
```

The summary of `WaitingList` stays "The waiting cloak designs, oldest first…" — it is still used by the cloak tests.

- [ ] **Step 6: Run the tests**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemStateTests/*"`
Expected: 9 passed.
Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildCloakStateTests/*"`
Expected: all pass.

```json:metadata
{"files": ["Chaos/Models/World/GuildCloakState.cs", "Tests/Chaos.Tests/GuildEmblem/GuildEmblemStateTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/GuildEmblemStateTests/*\"", "acceptanceCriteria": ["emblem editor view default then draft; statuses like cloaks", "shared id counter", "unchanged resubmit keeps id; changed replaces and old id refused", "TryApprove/TryReject report kind; emblem approval leaves waiting cloak", "WaitingDesigns lists both kinds oldest first", "ClearApprovedEmblem leaves cloak; RemoveGuild drops both", "old JSON loads; emblem state round-trips", "existing GuildCloakStateTests pass"], "modelTier": "standard"}
```

---

### Task 4: `GuildCloakService` handles emblems

**Goal:** The service opens the emblem editor, saves and submits emblems, answers emblem downloads, reviews both kinds from one list, clears emblems, and raises `EmblemChanged` after an emblem approval or clear.

**Files:**
- Create: `$SRV/Chaos/Services/GuildCloak/IGuildEmblemSource.cs`
- Modify: `$SRV/Chaos/Services/GuildCloak/GuildCloakService.cs`
- Modify: `$SRV/Chaos/Extensions/ServiceCollectionExtensions.cs` (after the `GuildCloakService` registration, line ~369)
- Modify: `$SRV/Chaos/Services/Servers/WorldServer.cs` (two handlers + registration next to the cloak ones)
- Test: `$SRV/Tests/Chaos.Tests/GuildEmblem/GuildEmblemServiceTests.cs`

**Acceptance Criteria:**
- [ ] Only the leader of a guild that owns the emblem can open, save or submit; refusals use the spec's messages
- [ ] An unpainted emblem cannot be submitted ("Paint at least one pixel before submitting.")
- [ ] Cloak and emblem saves share the 2-second limit per player
- [ ] Submitting puts the emblem in the mixed review list and tells online admins once
- [ ] Approving or clearing an emblem raises `EmblemChanged` with the guild's name and tells the leader
- [ ] `EmblemIdFor` returns the current guild's approved emblem id, or 0
- [ ] Design requests are answered only for approved emblem ids
- [ ] All existing `GuildCloakServiceTests` and `GuildCloakScriptTests` still pass

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildCloak/*/*"` and `--treenode-filter "/*/*/GuildEmblemServiceTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** — create `Tests/Chaos.Tests/GuildEmblem/GuildEmblemServiceTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Services.GuildCloak;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
#endregion

namespace Chaos.Tests.GuildEmblem;

public sealed class GuildEmblemServiceTests
{
    private const string GUILD = "Shinebox";

    private static GuildEmblemDesign Painted(int pixel = 60)
    {
        var design = GuildEmblemDesign.CreateDefault();
        design.Pixels[pixel] = 1;

        return design;
    }

    private static GuildEmblemEditorInteractionArgs SubmitArgs(GuildEmblemDesign? design = null)
        => new()
        {
            Action = GuildCloakEditorAction.Submit,
            Design = design ?? Painted()
        };

    private static void ShouldGetOrangeBar(Aisling aisling, string message)
        => Mock.Get(aisling.Client)
               .Verify(c => c.SendServerMessage(ServerMessageType.AdminMessage, message), Times.Once());

    [Test]
    public void OpenEmblemEditor_refuses_a_member_who_is_not_the_leader()
    {
        var world = new World();

        world.Service.OpenEmblemEditor(world.Member);

        ShouldGetOrangeBar(world.Member, GuildCloakService.EMBLEM_NOT_LEADER);
        Mock.Get(world.Member.Client).Verify(c => c.SendGuildEmblemEditor(It.IsAny<GuildEmblemEditorArgs>()), Times.Never());
    }

    [Test]
    public void OpenEmblemEditor_refuses_a_guild_that_has_not_bought_the_emblem()
    {
        var world = new World(ownsEmblem: false);

        world.Service.OpenEmblemEditor(world.Leader);

        ShouldGetOrangeBar(world.Leader, GuildCloakService.EMBLEM_NOT_OWNED);
    }

    [Test]
    public void OpenEmblemEditor_sends_the_default_emblem_and_the_guild_name()
    {
        var world = new World();

        world.Service.OpenEmblemEditor(world.Leader);

        Mock.Get(world.Leader.Client)
            .Verify(
                c => c.SendGuildEmblemEditor(
                    It.Is<GuildEmblemEditorArgs>(
                        a => (a.Type == GuildCloakEditorType.Open)
                             && (a.GuildName == GUILD)
                             && a.Design.ContentEquals(GuildEmblemDesign.CreateDefault()))),
                Times.Once());
    }

    [Test]
    public void Submitting_an_unpainted_emblem_is_refused()
    {
        var world = new World();

        world.Service.HandleEmblemEditor(world.Leader, SubmitArgs(GuildEmblemDesign.CreateDefault()));

        ShouldGetOrangeBar(world.Leader, GuildCloakService.EMBLEM_NO_PAINT);
        world.State.IsEmblemWaiting(GUILD).Should().BeFalse();
    }

    [Test]
    public void An_unpainted_draft_can_be_saved()
    {
        var world = new World();

        world.Service.HandleEmblemEditor(
            world.Leader,
            new GuildEmblemEditorInteractionArgs
            {
                Action = GuildCloakEditorAction.SaveDraft,
                Design = GuildEmblemDesign.CreateDefault()
            });

        Mock.Get(world.Leader.Client)
            .Verify(c => c.SendGuildEmblemEditor(It.Is<GuildEmblemEditorArgs>(a => a.Type == GuildCloakEditorType.Status)), Times.Once());
    }

    [Test]
    public void Submit_puts_the_emblem_in_the_mixed_review_list_and_tells_admins_once()
    {
        var world = new World();

        world.Service.HandleEmblemEditor(world.Leader, SubmitArgs());
        world.Later(3);
        world.Service.HandleEmblemEditor(world.Leader, SubmitArgs(Painted(61)));

        ShouldGetOrangeBar(world.Admin, $"{GUILD} submitted a guild emblem for review.");

        world.Service.OpenReview(world.Admin);

        Mock.Get(world.Admin.Client)
            .Verify(
                c => c.SendGuildCloakReviewList(
                    It.Is<GuildCloakReviewListArgs>(
                        a => (a.Entries.Count == 1)
                             && (a.Entries[0].Kind == GuildDesignKind.Emblem)
                             && a.Entries[0].EmblemDesign.ContentEquals(Painted(61)))),
                Times.Once());
    }

    [Test]
    public void Cloak_and_emblem_saves_share_the_rate_limit()
    {
        var world = new World(ownsCloak: true);

        world.Service.HandleEditor(
            world.Leader,
            new GuildCloakEditorInteractionArgs
            {
                Action = GuildCloakEditorAction.SaveDraft,
                Design = GuildCloakDesign.CreateDefault()
            });

        world.Service.HandleEmblemEditor(world.Leader, SubmitArgs());

        ShouldGetOrangeBar(world.Leader, GuildCloakService.TOO_FAST);
        world.State.IsEmblemWaiting(GUILD).Should().BeFalse();
    }

    [Test]
    public void Approving_an_emblem_raises_EmblemChanged_and_tells_the_leader()
    {
        var world = new World();
        var changed = new List<string>();
        world.Service.EmblemChanged += changed.Add;

        var id = world.SubmitAndApprove();

        changed.Should().Equal(GUILD);
        ShouldGetOrangeBar(world.Leader, GuildCloakService.EMBLEM_APPROVED);
        ShouldGetOrangeBar(world.Admin, $"Approved the guild emblem of {GUILD}.");
        world.Service.EmblemIdFor(world.Member).Should().Be(id);
        world.Service.EmblemIdFor(MockAisling.Create(name: "Outsider")).Should().Be(0);
    }

    [Test]
    public void Rejecting_an_emblem_tells_the_leader_the_reason()
    {
        var world = new World();
        var id = world.Submit();

        world.Service.HandleReview(
            world.Admin,
            new GuildCloakReviewInteractionArgs
            {
                Action = GuildCloakReviewAction.Reject,
                SubmissionId = id,
                Reason = "Too plain."
            });

        ShouldGetOrangeBar(world.Leader, "An admin rejected your guild emblem: Too plain.");
        world.State.EmblemEditorView(GUILD).Status.Should().Be(GuildCloakStatus.Rejected);
    }

    [Test]
    public void ClearEmblem_needs_an_admin_and_raises_EmblemChanged()
    {
        var world = new World();
        world.SubmitAndApprove();
        var changed = new List<string>();
        world.Service.EmblemChanged += changed.Add;

        world.Service.ClearEmblem(world.Member, GUILD).Should().BeFalse();
        world.Service.ClearEmblem(world.Admin, GUILD).Should().BeTrue();

        changed.Should().Equal(GUILD);
        ShouldGetOrangeBar(world.Leader, GuildCloakService.EMBLEM_CLEARED);
        world.Service.EmblemIdFor(world.Member).Should().Be(0);
    }

    [Test]
    public void Design_requests_are_answered_only_for_approved_emblems()
    {
        var world = new World();
        var waiting = world.Submit();

        world.Service.HandleEmblemDesignRequest(world.Member.Client, waiting);

        Mock.Get(world.Member.Client).Verify(c => c.SendGuildEmblemDesign(It.IsAny<GuildEmblemDesignArgs>()), Times.Never());

        world.Approve(waiting);
        world.Service.HandleEmblemDesignRequest(world.Member.Client, waiting);

        Mock.Get(world.Member.Client)
            .Verify(
                c => c.SendGuildEmblemDesign(It.Is<GuildEmblemDesignArgs>(a => (a.DesignId == waiting) && a.Design.ContentEquals(Painted()))),
                Times.Once());
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>The guild "Shinebox" with a leader and a member, plus an online admin. The guild owns the emblem unless told otherwise.</summary>
    private sealed class World
    {
        public readonly Aisling Admin;
        public readonly ClientRegistry<IChaosWorldClient> Clients = new();
        public readonly GuildHouseState House = new(new Mock<IStorage<GuildHouseState>>().Object);
        public readonly Aisling Leader;
        public readonly Aisling Member;
        public readonly GuildCloakService Service;
        public readonly GuildCloakState State = new();
        public readonly FixedTime Time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
        private uint NextClientId = 1;

        public World(bool ownsEmblem = true, bool ownsCloak = false)
        {
            var guild = MockGuild.Create(GUILD);
            Leader = Online(MockAisling.Create(name: "Stahli"));
            Member = Online(MockAisling.Create(name: "Iglis"));
            Admin = Online(MockAisling.Create(name: "Sichi"));
            Admin.IsAdmin = true;

            guild.AddMember(Leader, Leader);
            guild.ChangeRank(Leader.Name, 0, Leader);
            guild.AddMember(Member, Leader);

            if (ownsEmblem)
                House.EnableProperty(GUILD, GuildEmblemProtocol.DEED_PROPERTY);

            if (ownsCloak)
                House.EnableProperty(GUILD, GuildCloakProtocol.DEED_PROPERTY);

            var cloakStorage = new Mock<IStorage<GuildCloakState>>();
            cloakStorage.SetupGet(s => s.Value).Returns(State);

            var houseStorage = new Mock<IStorage<GuildHouseState>>();
            houseStorage.SetupGet(s => s.Value).Returns(House);

            Service = new GuildCloakService(
                cloakStorage.Object,
                houseStorage.Object,
                Clients,
                Time,
                NullLogger<GuildCloakService>.Instance);
        }

        public void Approve(int id)
            => Service.HandleReview(
                Admin,
                new GuildCloakReviewInteractionArgs
                {
                    Action = GuildCloakReviewAction.Approve,
                    SubmissionId = id
                });

        public void Later(double seconds) => Time.Now = Time.Now.AddSeconds(seconds);

        /// <summary>Submits an emblem as the leader and returns its submission id. Waits out the save limit.</summary>
        public int Submit(GuildEmblemDesign? design = null)
        {
            Service.HandleEmblemEditor(Leader, SubmitArgs(design));
            Later(3);

            return State.WaitingDesigns(20).Single(w => w.Kind == GuildDesignKind.Emblem).Id;
        }

        public int SubmitAndApprove()
        {
            var id = Submit();
            Approve(id);

            return id;
        }

        private Aisling Online(Aisling aisling)
        {
            Mock.Get(aisling.Client).SetupGet(c => c.Id).Returns(NextClientId++);
            Clients.TryAdd(aisling.Client);

            return aisling;
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemServiceTests/*"`
Expected: build errors (missing members).

- [ ] **Step 3: Create `Chaos/Services/GuildCloak/IGuildEmblemSource.cs`**

```csharp
#region
using Chaos.Models.World;
#endregion

namespace Chaos.Services.GuildCloak;

/// <summary>
///     The guild emblem facts the emblem rules need: which approved emblem a player's guild has, and when a guild's approved
///     emblem changes. <see cref="GuildCloakService" /> is the one implementation.
/// </summary>
public interface IGuildEmblemSource
{
    /// <summary>Raised with the guild's name after an admin approves a new emblem for it or clears its emblem.</summary>
    event Action<string>? EmblemChanged;

    /// <summary>The approved emblem id of <paramref name="aisling" />'s current guild, or 0.</summary>
    int EmblemIdFor(Aisling aisling);
}
```

- [ ] **Step 4: Change `GuildCloakService`**

1. Class declaration: `public sealed class GuildCloakService : IGuildEmblemSource`. Class summary: "Guild cloak and guild emblem rules: who may design and review, the save rate, which design every guild cloak shows, and which emblem a guild has. It holds the only lock around <see cref="GuildCloakState" /> …" (keep the rest).
2. Add constants (keep them alphabetical with the others):

```csharp
    public const string EMBLEM_APPROVED = "An admin approved your guild emblem.";
    public const string EMBLEM_CLEARED = "An admin cleared your guild emblem.";
    public const string EMBLEM_INVALID = "That emblem could not be saved.";
    public const string EMBLEM_NO_PAINT = "Paint at least one pixel before submitting.";
    public const string EMBLEM_NOT_LEADER = "Only the guild leader can design the guild emblem.";
    public const string EMBLEM_NOT_OWNED = "Your guild does not own a guild emblem.";
    public const string EMBLEM_SUBMITTED = "Your guild emblem was sent to the admins for review.";
```

3. Add the event after the constructor:

```csharp
    /// <inheritdoc />
    public event Action<string>? EmblemChanged;
```

4. Add a shared rate-limit helper and use it in `HandleEditor`:

```csharp
    //the caller holds Sync. Cloak and emblem saves share one limit per player.
    private bool TryStartSave(string playerName, DateTime now)
    {
        if (LastSaveUtc.TryGetValue(playerName, out var last) && ((now - last) < GuildCloakProtocol.SaveInterval))
            return false;

        LastSaveUtc[playerName] = now;

        return true;
    }
```

In `HandleEditor`, replace

```csharp
            if (!LastSaveUtc.TryGetValue(source.Name, out var last) || ((now - last) >= GuildCloakProtocol.SaveInterval))
            {
                LastSaveUtc[source.Name] = now;
```

with

```csharp
            if (TryStartSave(source.Name, now))
            {
```

5. Add the emblem members:

```csharp
    /// <summary>Removes an admin-chosen guild's approved emblem. Members stop showing it. False for non-admins or no emblem.</summary>
    public bool ClearEmblem(Aisling admin, string guildName)
    {
        if (!admin.IsAdmin)
            return false;

        bool cleared;

        using (Sync.EnterScope())
        {
            cleared = State.ClearApprovedEmblem(guildName);

            if (cleared)
                CloakStorage.Save();
        }

        if (!cleared)
            return false;

        Logger.LogInformation("{Admin} cleared the guild emblem of {Guild}", admin.Name, guildName);
        NotifyLeaders(guildName, EMBLEM_CLEARED);
        EmblemChanged?.Invoke(guildName);

        return true;
    }

    /// <summary>Why <paramref name="source" /> cannot use the emblem editor, or null when they can.</summary>
    public string? EmblemEditorRefusal(Aisling source)
    {
        if (source.Guild is null)
            return NOT_IN_GUILD;

        if (!IsLeader(source))
            return EMBLEM_NOT_LEADER;

        return OwnsEmblem(source.Guild.Name) ? null : EMBLEM_NOT_OWNED;
    }

    /// <inheritdoc />
    public int EmblemIdFor(Aisling aisling)
    {
        var guildName = aisling.Guild?.Name;

        using (Sync.EnterScope())
            return State.ApprovedEmblemIdOf(guildName);
    }

    /// <summary>Answers a client's request for one emblem. Only approved emblems are ever sent.</summary>
    public void HandleEmblemDesignRequest(IChaosWorldClient client, int designId)
    {
        GuildEmblemDesign? design;

        using (Sync.EnterScope())
            design = State.FindApprovedEmblem(designId)
                          ?.DeepCopy();

        if (design is not null)
            client.SendGuildEmblemDesign(
                new GuildEmblemDesignArgs
                {
                    DesignId = designId,
                    Design = design
                });
    }

    /// <summary>A leader's save or submit from the emblem editor.</summary>
    public void HandleEmblemEditor(Aisling source, GuildEmblemEditorInteractionArgs args)
    {
        var refusal = EmblemEditorRefusal(source);

        if (refusal is not null)
        {
            source.SendOrangeBarMessage(refusal);

            return;
        }

        if (args.Design is null || !args.Design.IsValid())
        {
            source.SendOrangeBarMessage(EMBLEM_INVALID);

            return;
        }

        if ((args.Action == GuildCloakEditorAction.Submit) && !args.Design.HasPaint())
        {
            source.SendOrangeBarMessage(EMBLEM_NO_PAINT);

            return;
        }

        var guildName = source.Guild!.Name;
        var now = NowUtc;
        GuildEmblemEditorArgs? reply = null;
        var enteredQueue = false;

        using (Sync.EnterScope())
            if (TryStartSave(source.Name, now))
            {
                if (args.Action == GuildCloakEditorAction.Submit)
                {
                    enteredQueue = !State.IsEmblemWaiting(guildName);
                    State.SubmitEmblem(guildName, source.Name, args.Design, now);
                } else
                    State.SaveEmblemDraft(guildName, args.Design);

                CloakStorage.Save();
                reply = EmblemEditorArgs(GuildCloakEditorType.Status, guildName);
            }

        if (reply is null)
        {
            source.SendOrangeBarMessage(TOO_FAST);

            return;
        }

        source.Client.SendGuildEmblemEditor(reply);

        if (args.Action != GuildCloakEditorAction.Submit)
            return;

        Logger.LogInformation("{Leader} submitted a guild emblem for {Guild}", source.Name, guildName);
        source.SendOrangeBarMessage(EMBLEM_SUBMITTED);

        //only a guild joining the queue is news; changes to a waiting emblem show up in the review list
        if (!enteredQueue)
            return;

        foreach (var admin in OnlineAislings()
                     .Where(aisling => aisling.IsAdmin))
            admin.SendOrangeBarMessage($"{guildName} submitted a guild emblem for review.");
    }

    public void OpenEmblemEditor(Aisling source)
    {
        var refusal = EmblemEditorRefusal(source);

        if (refusal is not null)
        {
            source.SendOrangeBarMessage(refusal);

            return;
        }

        GuildEmblemEditorArgs args;

        using (Sync.EnterScope())
            args = EmblemEditorArgs(GuildCloakEditorType.Open, source.Guild!.Name);

        source.Client.SendGuildEmblemEditor(args);
    }

    public bool OwnsEmblem(string guildName) => HouseStorage.Value.HasProperty(guildName, GuildEmblemProtocol.DEED_PROPERTY);

    //the caller holds Sync
    private GuildEmblemEditorArgs EmblemEditorArgs(GuildCloakEditorType type, string guildName)
    {
        (var design, var status, var reason) = State.EmblemEditorView(guildName);
        var open = type == GuildCloakEditorType.Open;

        return new GuildEmblemEditorArgs
        {
            Type = type,
            Status = status,
            RejectionReason = reason,
            GuildName = open ? guildName : string.Empty,
            Design = open ? design : new GuildEmblemDesign()
        };
    }
```

6. Replace the body of `HandleReview` from `var now = NowUtc;` to the end with:

```csharp
        var now = NowUtc;
        bool decided;
        string? guildName;
        GuildDesignKind kind;

        using (Sync.EnterScope())
        {
            decided = args.Action == GuildCloakReviewAction.Approve
                ? State.TryApprove(args.SubmissionId, admin.Name, now, out guildName, out kind)
                : State.TryReject(args.SubmissionId, admin.Name, reason, now, out guildName, out kind);

            if (decided)
                CloakStorage.Save();
        }

        var isEmblem = kind == GuildDesignKind.Emblem;
        var what = isEmblem ? "guild emblem" : "guild cloak";

        if (!decided)
            admin.SendOrangeBarMessage(STALE_DECISION);
        else if (args.Action == GuildCloakReviewAction.Approve)
        {
            Logger.LogInformation("{Admin} approved {Kind} {Id} for {Guild}", admin.Name, what, args.SubmissionId, guildName);
            admin.SendOrangeBarMessage($"Approved the {what} of {guildName}.");

            if (isEmblem)
            {
                NotifyLeaders(guildName!, EMBLEM_APPROVED);
                EmblemChanged?.Invoke(guildName!);
            } else
            {
                NotifyLeaders(guildName!, APPROVED);
                RedisplayMembers(guildName!);
            }
        } else
        {
            Logger.LogInformation(
                "{Admin} rejected {Kind} {Id} for {Guild}: {Reason}",
                admin.Name,
                what,
                args.SubmissionId,
                guildName,
                reason);

            admin.SendOrangeBarMessage($"Rejected the {what} of {guildName}.");

            NotifyLeaders(
                guildName!,
                isEmblem ? $"An admin rejected your guild emblem: {reason}" : $"An admin rejected your guild cloak design: {reason}");
        }

        admin.Client.SendGuildCloakReviewList(ReviewList(GuildCloakReviewListType.Update));
```

7. Replace `ReviewList` with:

```csharp
    private GuildCloakReviewListArgs ReviewList(GuildCloakReviewListType type)
    {
        using (Sync.EnterScope())
            return new GuildCloakReviewListArgs
            {
                Type = type,
                Entries = State.WaitingDesigns(GuildCloakProtocol.MAX_REVIEW_ENTRIES)
                               .Select(
                                   waiting => new GuildCloakReviewEntry
                                   {
                                       Kind = waiting.Kind,
                                       SubmissionId = waiting.Id,
                                       GuildName = waiting.GuildName,
                                       LeaderName = waiting.SubmittedBy,
                                       SubmittedAtUtc = waiting.SubmittedAtUtc,
                                       Design = waiting.Cloak?.DeepCopy() ?? new GuildCloakDesign(),
                                       EmblemDesign = waiting.Emblem?.DeepCopy() ?? new GuildEmblemDesign()
                                   })
                               .ToList()
            };
    }
```

- [ ] **Step 5: Register the interface** — in `Chaos/Extensions/ServiceCollectionExtensions.cs`, directly after `services.AddSingleton<GuildCloakService>();`:

```csharp
            services.AddSingleton<IGuildEmblemSource>(provider => provider.GetRequiredService<GuildCloakService>());
```

- [ ] **Step 6: World server handlers** — in `Chaos/Services/Servers/WorldServer.cs`, after `OnGuildCloakReviewInteraction`:

```csharp
    /// <summary>
    ///     Routes a guild emblem editor save or submit to <see cref="GuildCloakService" />, which checks the leader rank, the
    ///     purchase, the rate and the emblem.
    /// </summary>
    public ValueTask OnGuildEmblemEditorInteraction(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<GuildEmblemEditorInteractionArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnGuildEmblemEditorInteraction);

        ValueTask InnerOnGuildEmblemEditorInteraction(IChaosWorldClient localClient, GuildEmblemEditorInteractionArgs localArgs)
        {
            if (localClient.Connected && localClient.Aisling is { } aisling)
                GuildCloakService.HandleEmblemEditor(aisling, localArgs);

            return default;
        }
    }

    /// <summary>Answers a request for one approved guild emblem. Unknown and unapproved ids get no answer.</summary>
    public ValueTask OnGuildEmblemDesignRequest(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<GuildEmblemDesignRequestArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnGuildEmblemDesignRequest);

        ValueTask InnerOnGuildEmblemDesignRequest(IChaosWorldClient localClient, GuildEmblemDesignRequestArgs localArgs)
        {
            if (localClient.Connected)
                GuildCloakService.HandleEmblemDesignRequest(localClient, localArgs.DesignId);

            return default;
        }
    }
```

and after `ClientHandlers[(byte)ClientOpCode.GuildCloakReviewInteraction] = OnGuildCloakReviewInteraction;`:

```csharp
        ClientHandlers[(byte)ClientOpCode.GuildEmblemEditorInteraction] = OnGuildEmblemEditorInteraction;
        ClientHandlers[(byte)ClientOpCode.GuildEmblemDesignRequest] = OnGuildEmblemDesignRequest;
```

- [ ] **Step 7: Run the tests**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemServiceTests/*"`
Expected: 11 passed.
Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildCloak/*/*"`
Expected: all pass.

```json:metadata
{"files": ["Chaos/Services/GuildCloak/IGuildEmblemSource.cs", "Chaos/Services/GuildCloak/GuildCloakService.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Chaos/Services/Servers/WorldServer.cs", "Tests/Chaos.Tests/GuildEmblem/GuildEmblemServiceTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/GuildEmblemServiceTests/*\"", "acceptanceCriteria": ["leader + owned emblem required; spec refusal messages", "unpainted submit refused", "shared 2 s save limit", "submit enters mixed review list; admins told once", "approve/clear raise EmblemChanged and tell leader", "EmblemIdFor = current guild's approved id or 0", "design requests answered only for approved ids", "existing GuildCloak tests pass"], "modelTier": "standard"}
```

---

### Task 5: Emblem rules — reserved `guild` key, offer once per guild, membership hooks, world list id

**Goal:** Members own the reserved `guild` emblem while their guild has an approved emblem; the book lists it first; it is offered once per guild; joins, leaves, approvals and clears keep the shown emblem and the world list id right.

**Files:**
- Modify: `$SRV/Chaos/Services/Emblems/AislingEmblems.cs`, `$SRV/Chaos/Services/Emblems/AislingEmblemsSchema.cs`
- Modify: `$SRV/Chaos/Services/Emblems/EmblemService.cs`, `$SRV/Chaos/Services/Emblems/EmblemCatalog.cs`
- Create: `$SRV/Chaos/Services/GuildCloak/GuildEmblemRefresh.cs`
- Modify: `$SRV/Chaos/Collections/Guild.cs` (`UnsafeDetach`, `UnsafeJoin`)
- Modify: `$SRV/Chaos/Services/Servers/WorldServer.cs` (constructor: set the hook)
- Modify: `$SRV/Chaos/Services/MapperProfiles/AislingMapperProfile.cs` (world list map)
- Test: `$SRV/Tests/Chaos.Tests/Emblems/EmblemServiceTests.cs`, `AislingEmblemsTests.cs`, `EmblemCatalogTests.cs`, `$SRV/Tests/Chaos.Tests/AislingMapperProfileTests.cs`, new `$SRV/Tests/Chaos.Tests/GuildEmblem/GuildEmblemRefreshTests.cs`

**Acceptance Criteria:**
- [ ] `EmblemCatalog` skips a file whose key is `guild` (any case) with an error containing "reserved"
- [ ] The book lists the guild entry first (key `guild`, guild name, "The emblem of your guild.", owned, `Guild`, the id) only while the guild has an approved emblem
- [ ] `EmblemChoice` with `guild` shows it only when owned
- [ ] The offer happens once per guild at login, at join, or after an approval: it auto-shows into an empty cell with "Your guild's emblem is ready."; otherwise it sends "Your guild's emblem is ready. Show it from the Emblem tab." and changes nothing
- [ ] A new approved version updates the cached id for members showing it; a clear or leaving the guild hides it
- [ ] `emblems.json` saves the offered guild; `ShownAt` returns the guild name with art 0 while a guild emblem is shown
- [ ] The world list map carries `GuildEmblemId`
- [ ] `Guild` join and detach call `GuildEmblemRefresh.MembershipChanged`; the world server points the hook at `EmblemService.OnGuildMembershipChanged`
- [ ] All existing emblem and mapper tests still pass

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Emblems/*/*"` plus `/*/*/GuildEmblemRefreshTests/*` and `/*/*/AislingMapperProfileTests/*` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

In `Tests/Chaos.Tests/Emblems/EmblemCatalogTests.cs`, add:

```csharp
    [Test]
    public void The_guild_key_is_reserved_for_guild_emblems()
    {
        var load = Parse(("guild.json", """{ "key": "Guild", "art": 6, "name": "Guild", "description": "d", "source": { "staff": true } }"""));

        load.Templates.Should().BeEmpty();
        load.Errors.Should().ContainSingle(error => error.Contains("reserved"));
    }
```

In `Tests/Chaos.Tests/Emblems/AislingEmblemsTests.cs`, add:

```csharp
    [Test]
    public void A_shown_guild_emblem_keeps_its_name_with_art_zero_and_clearing_resets_the_id()
    {
        var emblems = new AislingEmblems();

        emblems.SetShown("guild", 0, null, "Shinebox", 42);

        emblems.ShownAt(DateTime.UtcNow).Should().Be(((ushort)0, "Shinebox"));
        emblems.ShownGuildEmblemId.Should().Be(42);

        emblems.SetShown(null, 0, null);

        emblems.ShownGuildEmblemId.Should().Be(0);
        emblems.ShownAt(DateTime.UtcNow).Should().Be(((ushort)0, string.Empty));
    }

    [Test]
    public void TryMarkOffered_is_true_once_per_guild_and_survives_a_save()
    {
        var emblems = new AislingEmblems();

        emblems.TryMarkOffered("Shinebox").Should().BeTrue();
        emblems.TryMarkOffered("shinebox").Should().BeFalse();

        var again = AislingEmblems.FromSchema(emblems.ToSchema());

        again.TryMarkOffered("Shinebox").Should().BeFalse();
        again.TryMarkOffered("Moonveil").Should().BeTrue();
    }
```

In `Tests/Chaos.Tests/AislingMapperProfileTests.cs`, add:

```csharp
    [Test]
    public void WorldList_map_carries_a_shown_guild_emblem_id_and_the_guild_name()
    {
        var aisling = MockAisling.Create(name: "Aldric");
        aisling.Emblems.SetShown("guild", 0, null, "Shinebox", 42);

        IMapperProfile<Aisling, WorldListMemberInfo> profile = CreateProfile(new Mock<ITypeMapper>().Object);
        var info = profile.Map(aisling);

        info.EmblemArt.Should().Be(0);
        info.EmblemName.Should().Be("Shinebox");
        info.GuildEmblemId.Should().Be(42);
    }
```

Create `Tests/Chaos.Tests/GuildEmblem/GuildEmblemRefreshTests.cs`:

```csharp
#region
using System.Collections.Concurrent;
using Chaos.Models.World;
using Chaos.Services.GuildCloak;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildEmblem;

public sealed class GuildEmblemRefreshTests
{
    [Test]
    [NotInParallel(nameof(GuildEmblemRefresh))]
    public void Joining_and_leaving_a_guild_reach_the_emblem_hook()
    {
        //other tests may join guilds while this runs, so only this test's player is counted
        var seen = new ConcurrentBag<Aisling>();
        var previous = GuildEmblemRefresh.Handler;
        GuildEmblemRefresh.Handler = seen.Add;

        try
        {
            var guild = MockGuild.Create("Shinebox");
            var aisling = MockAisling.Create(name: "Aldric");

            guild.AddMember(aisling, aisling);
            guild.TryLeave(aisling).Should().BeTrue();

            seen.Count(a => ReferenceEquals(a, aisling)).Should().Be(2);
        } finally
        {
            GuildEmblemRefresh.Handler = previous;
        }
    }
}
```

A player's first `AddMember` gives them the lowest rank, so `TryLeave` succeeds (only a sole leader is refused).

In `Tests/Chaos.Tests/Emblems/EmblemServiceTests.cs`:

1. Add usings: `using Chaos.Collections;` and `using Chaos.Services.GuildCloak;`.
2. In `World`, add the fake and pass it to the service:

```csharp
        public readonly FakeGuildEmblems GuildEmblems = new();
```

```csharp
            Service = new EmblemService(
                Catalog,
                Boards.Object,
                RecordStorage.Object,
                Clients,
                GuildEmblems,
                Time,
                NullLogger<EmblemService>.Instance);
```

and this helper in `World`:

```csharp
        public Guild JoinGuild(Aisling aisling, string guildName)
        {
            var guild = MockGuild.Create(guildName);
            guild.AddMember(aisling, aisling);

            return guild;
        }
```

3. Add the fake as a nested class of `EmblemServiceTests`:

```csharp
    private sealed class FakeGuildEmblems : IGuildEmblemSource
    {
        public readonly Dictionary<string, int> Approved = new(StringComparer.OrdinalIgnoreCase);

        public event Action<string>? EmblemChanged;

        public int EmblemIdFor(Aisling aisling)
            => aisling.Guild is { } guild && Approved.TryGetValue(guild.Name, out var id) ? id : 0;

        public void Approve(string guildName, int id)
        {
            Approved[guildName] = id;
            EmblemChanged?.Invoke(guildName);
        }

        public void Clear(string guildName)
        {
            Approved.Remove(guildName);
            EmblemChanged?.Invoke(guildName);
        }
    }
```

4. Add these tests:

```csharp
    [Test]
    public void Guild_emblem_is_listed_first_in_the_book_while_the_guild_has_one()
    {
        var world = new World(Mark("carnun", "carnunwon"));
        var aisling = world.Online("Aldric");
        world.JoinGuild(aisling, "Shinebox");

        world.Service.BuildBook(aisling).Entries.Should().NotContain(e => e.Key == "guild");

        world.GuildEmblems.Approved["Shinebox"] = 42;
        var book = world.Service.BuildBook(aisling);

        book.Entries[0]
            .Should()
            .BeEquivalentTo(
                new EmblemBookEntry
                {
                    Key = "guild",
                    Name = "Shinebox",
                    Description = EmblemService.GUILD_DESCRIPTION,
                    Owned = true,
                    Guild = true,
                    GuildEmblemId = 42
                });
    }

    [Test]
    public void Login_offers_the_guild_emblem_once_into_an_empty_cell()
    {
        var world = new World();
        var aisling = world.Online("Aldric");
        world.JoinGuild(aisling, "Shinebox");
        world.GuildEmblems.Approved["Shinebox"] = 42;

        world.Service.OnLogin(aisling);

        aisling.Emblems.Shown.Should().Be("guild");
        aisling.Emblems.ShownGuildEmblemId.Should().Be(42);
        aisling.Emblems.ShownAt(world.Now).Should().Be(((ushort)0, "Shinebox"));
        ShouldGetOrangeBar(aisling, EmblemService.GUILD_READY);

        world.Service.HandleChoice(aisling, string.Empty);
        world.Service.Refresh(aisling);

        aisling.Emblems.Shown.Should().BeNull("the offer happens once per guild");
        ShouldGetOrangeBar(aisling, EmblemService.GUILD_READY);
    }

    [Test]
    public void The_offer_leaves_a_chosen_emblem_alone()
    {
        var world = new World(Mark("carnun", "carnunwon"));
        var aisling = world.Online("Aldric");
        aisling.Legend.AddUnique(LegendMarkOf("carnunwon"));
        world.Service.OnLogin(aisling);
        world.JoinGuild(aisling, "Shinebox");

        world.GuildEmblems.Approve("Shinebox", 42);

        aisling.Emblems.Shown.Should().Be("carnun");
        ShouldGetOrangeBar(aisling, EmblemService.GUILD_READY_HIDDEN);
    }

    [Test]
    public void A_new_approved_version_updates_members_showing_it_without_a_second_offer()
    {
        var world = new World();
        var aisling = world.Online("Aldric");
        world.JoinGuild(aisling, "Shinebox");
        world.GuildEmblems.Approved["Shinebox"] = 42;
        world.Service.OnLogin(aisling);

        world.GuildEmblems.Approve("Shinebox", 57);

        aisling.Emblems.Shown.Should().Be("guild");
        aisling.Emblems.ShownGuildEmblemId.Should().Be(57);
        ShouldGetOrangeBar(aisling, EmblemService.GUILD_READY);
    }

    [Test]
    public void Clearing_the_guild_emblem_hides_it()
    {
        var world = new World();
        var aisling = world.Online("Aldric");
        world.JoinGuild(aisling, "Shinebox");
        world.GuildEmblems.Approved["Shinebox"] = 42;
        world.Service.OnLogin(aisling);

        world.GuildEmblems.Clear("Shinebox");

        aisling.Emblems.Shown.Should().BeNull();
        aisling.Emblems.ShownGuildEmblemId.Should().Be(0);
    }

    [Test]
    public void Leaving_the_guild_hides_it_and_joining_another_offers_again()
    {
        var world = new World();
        var aisling = world.Online("Aldric");
        world.JoinGuild(aisling, "Shinebox");
        world.GuildEmblems.Approved["Shinebox"] = 42;
        world.GuildEmblems.Approved["Moonveil"] = 57;
        world.Service.OnLogin(aisling);

        aisling.Guild = null;
        world.Service.OnGuildMembershipChanged(aisling);

        aisling.Emblems.Shown.Should().BeNull();

        world.JoinGuild(aisling, "Moonveil");
        world.Service.OnGuildMembershipChanged(aisling);

        aisling.Emblems.Shown.Should().Be("guild");
        aisling.Emblems.ShownGuildEmblemId.Should().Be(57);
        aisling.Emblems.ShownAt(world.Now).Name.Should().Be("Moonveil");
    }

    [Test]
    public void Choosing_the_guild_emblem_needs_an_approved_one()
    {
        var world = new World();
        var aisling = world.Online("Aldric");
        world.JoinGuild(aisling, "Shinebox");

        world.Service.HandleChoice(aisling, "guild");

        aisling.Emblems.Shown.Should().BeNull();

        world.GuildEmblems.Approved["Shinebox"] = 42;
        world.Later(TimeSpan.FromSeconds(1));
        world.Service.HandleChoice(aisling, "GUILD");

        aisling.Emblems.Shown.Should().Be("guild");
        aisling.Emblems.ShownGuildEmblemId.Should().Be(42);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Emblems/*/*"`
Expected: build errors (missing members and constructor parameter).

- [ ] **Step 3: `AislingEmblemsSchema`** — add:

```csharp
    /// <summary>The guild whose emblem this player was last offered, so each guild's emblem is offered once.</summary>
    public string? GuildOffered { get; set; }
```

- [ ] **Step 4: `AislingEmblems`**

1. Add fields next to the other shown fields:

```csharp
    //the shown guild emblem's id (key "guild"), or 0; the world list draws it instead of the art
    private int ShownGuildEmblem;

    //the guild whose emblem was last offered to this player (saved)
    private string? OfferedGuild;
```

2. Add the property after `Shown`:

```csharp
    /// <summary>The id of the guild emblem shown in the world list, or 0 when the shown emblem is not the guild emblem.</summary>
    public int ShownGuildEmblemId
    {
        get
        {
            lock (Sync)
                return ShownGuildEmblem;
        }
    }
```

3. In `FromSchema`, extend the initializer:

```csharp
        var emblems = new AislingEmblems
        {
            ShownKey = string.IsNullOrWhiteSpace(schema.Shown) ? null : schema.Shown,
            OfferedGuild = string.IsNullOrWhiteSpace(schema.GuildOffered) ? null : schema.GuildOffered
        };
```

4. Replace `SetShown`:

```csharp
    /// <summary>
    ///     Sets the shown emblem and the art, name, expiry and guild emblem id the world list reads. A null key clears it.
    /// </summary>
    public void SetShown(string? key, ushort art, DateTime? expiresUtc, string name = "", int guildEmblemId = 0)
    {
        lock (Sync)
        {
            if (key is null)
            {
                ClearShownLocked();

                return;
            }

            ShownKey = key;
            ShownArt = art;
            ShownName = name;
            ShownExpires = expiresUtc;
            ShownGuildEmblem = guildEmblemId;
        }
    }
```

5. In `ShownAt`, change the condition to `(ShownExpires is { } expires && (nowUtc >= expires)) || ((ShownArt == 0) && (ShownGuildEmblem == 0))` and its summary to "…the shown emblem's (art 0 with the guild's name for a guild emblem), or 0 and empty…".
6. In `ToSchema`, add `GuildOffered = OfferedGuild,`.
7. Add:

```csharp
    /// <summary>True the first time it is called for a guild (ignoring case), and remembers that guild; false after that.</summary>
    public bool TryMarkOffered(string guildName)
    {
        lock (Sync)
        {
            if (string.Equals(OfferedGuild, guildName, StringComparison.OrdinalIgnoreCase))
                return false;

            OfferedGuild = guildName;

            return true;
        }
    }
```

8. In `ClearShownLocked`, add `ShownGuildEmblem = 0;`.
9. Class summary: add "The reserved key <c>guild</c> is the player's guild emblem; it is never in the owned or revoked keys."

- [ ] **Step 5: `EmblemCatalog.Validate`** — after the `MAX_KEY_LENGTH` check, add:

```csharp
        if (schema.Key.Equals(GuildEmblemProtocol.EMBLEM_KEY, StringComparison.OrdinalIgnoreCase))
            return $"key {GuildEmblemProtocol.EMBLEM_KEY} is reserved for guild emblems";
```

Add `using Chaos.DarkAges.Definitions;` if the file lacks it.

- [ ] **Step 6: Create `Chaos/Services/GuildCloak/GuildEmblemRefresh.cs`**

```csharp
#region
using Chaos.Models.World;
#endregion

namespace Chaos.Services.GuildCloak;

/// <summary>
///     Tells the emblem rules that a player joined or left a guild. <see cref="Collections.Guild" /> is built by a mapper
///     without the emblem service, so the world server sets <see cref="Handler" /> once at startup; while it is null (in
///     tests), nothing happens.
/// </summary>
public static class GuildEmblemRefresh
{
    /// <summary>Set by the world server to <c>EmblemService.OnGuildMembershipChanged</c>.</summary>
    public static Action<Aisling>? Handler { get; set; }

    public static void MembershipChanged(Aisling aisling) => Handler?.Invoke(aisling);
}
```

- [ ] **Step 7: `Guild.cs`** — in both `UnsafeDetach` and `UnsafeJoin`, directly after `GuildCloakRefresh.Redisplay(aisling);`, add:

```csharp
        GuildEmblemRefresh.MembershipChanged(aisling);
```

Update the `UnsafeDetach` summary's "…and re-sends their look so a guild cloak stops showing this guild's design." to "…re-sends their look so a guild cloak stops showing this guild's design, and hides this guild's emblem if they showed it."

- [ ] **Step 8: `EmblemService`**

1. Add usings `using Chaos.DarkAges.Definitions;` and `using Chaos.Services.GuildCloak;`.
2. Constants:

```csharp
    public const string GUILD_DESCRIPTION = "The emblem of your guild.";
    public const string GUILD_READY = "Your guild's emblem is ready.";
    public const string GUILD_READY_HIDDEN = "Your guild's emblem is ready. Show it from the Emblem tab.";
```

3. Field `private readonly IGuildEmblemSource GuildEmblems;`. Constructor: add the parameter `IGuildEmblemSource guildEmblems` after `clientRegistry`, then in the body:

```csharp
        GuildEmblems = guildEmblems;
        GuildEmblems.EmblemChanged += OnGuildEmblemChanged;
```

4. Class summary: add "The guild emblem (key <c>guild</c>) is live too: owned while the player's current guild has an approved emblem."
5. In `BuildBook`, replace the part from `var shown = aisling.Emblems.Shown;` to `var shownKey = …;` with:

```csharp
        //the guild emblem is live, like records: owned while the player's current guild has an approved one
        if (GuildEmblemEntry(aisling) is { } guildEntry)
            owned.Insert(0, guildEntry);

        var shown = aisling.Emblems.Shown;
        string shownKey;

        if (shown is not null && IsGuildKey(shown))
            shownKey = owned is [{ Guild: true }, ..] ? GuildEmblemProtocol.EMBLEM_KEY : string.Empty;
        else
            shownKey = shown is not null && Catalog.TryGet(shown, out var shownTemplate) && Owns(aisling, shownTemplate, now)
                ? shownTemplate.Key
                : string.Empty;
```

6. `EntryBytes` already counts the guild emblem id (Task 2); leave it.
7. `HandleChoice`: between the empty-key branch and the catalog branch, add `else if (IsGuildKey(key)) ShowGuildEmblem(aisling);`.
8. `Refresh`: after `changed |= FixShown(aisling, now);` add `changed |= OfferGuildEmblem(aisling, now);`, and add "…, and offers the guild emblem once per guild" to its summary.
9. `IsShowing`: replace its body with:

```csharp
        var shown = aisling.Emblems.Shown;

        if (shown is null)
            return false;

        if (IsGuildKey(shown))
            return GuildEmblems.EmblemIdFor(aisling) != 0;

        return Catalog.TryGet(shown, out var template) && Owns(aisling, template, now);
```

10. `FixShown`: right after `if (shown is null) return false;`, add:

```csharp
        if (IsGuildKey(shown))
        {
            //refreshes the cached id and name, which follow the guild's newest approved emblem
            if (ShowGuildEmblem(aisling))
                return false;

            SetShown(aisling, null);

            return true;
        }
```

11. Add these members:

```csharp
    /// <summary>
    ///     A player joined or left a guild, or it disbanded. Hides a guild emblem they no longer own, offers their new
    ///     guild's emblem, and sends a fresh book. Runs from <see cref="GuildEmblemRefresh" /> inside the guild's lock, so it
    ///     only reads guild emblem state and sends packets. Never throws: emblems must not break a guild change.
    /// </summary>
    public void OnGuildMembershipChanged(Aisling aisling)
    {
        try
        {
            var now = NowUtc;
            FixShown(aisling, now);
            OfferGuildEmblem(aisling, now);
            SendBook(aisling);
        } catch (Exception e)
        {
            Logger.LogError(e, "Guild emblem check failed for {@AislingName}", aisling.Name);
        }
    }

    private EmblemBookEntry? GuildEmblemEntry(Aisling aisling)
    {
        var id = GuildEmblems.EmblemIdFor(aisling);

        if ((id == 0) || aisling.Guild is not { } guild)
            return null;

        return new EmblemBookEntry
        {
            Key = GuildEmblemProtocol.EMBLEM_KEY,
            Name = guild.Name,
            Description = GUILD_DESCRIPTION,
            Owned = true,
            Guild = true,
            GuildEmblemId = id
        };
    }

    private static bool IsGuildKey(string key) => key.EqualsI(GuildEmblemProtocol.EMBLEM_KEY);

    /// <summary>
    ///     Offers the player's guild emblem once per guild: shown when nothing owned is showing, otherwise only announced.
    ///     True when the offer happened now.
    /// </summary>
    private bool OfferGuildEmblem(Aisling aisling, DateTime now)
    {
        if (aisling.Guild is not { } guild || (GuildEmblems.EmblemIdFor(aisling) == 0) || !aisling.Emblems.TryMarkOffered(guild.Name))
            return false;

        if (IsShowing(aisling, now))
            aisling.SendOrangeBarMessage(GUILD_READY_HIDDEN);
        else
        {
            ShowGuildEmblem(aisling);
            aisling.SendOrangeBarMessage(GUILD_READY);
        }

        return true;
    }

    //an admin approved or cleared a guild's emblem: every online member gets the new id, a hidden cell or an offer
    private void OnGuildEmblemChanged(string guildName)
    {
        var members = ClientRegistry.Select(client => client.Aisling)
                                    .Where(aisling => aisling?.Guild is { } guild && guild.Name.EqualsI(guildName))
                                    .ToList();

        foreach (var member in members)
            OnGuildMembershipChanged(member);
    }

    /// <summary>Shows the player's guild emblem. False (and nothing changes) when their guild has no approved emblem.</summary>
    private bool ShowGuildEmblem(Aisling aisling)
    {
        var id = GuildEmblems.EmblemIdFor(aisling);

        if ((id == 0) || aisling.Guild is not { } guild)
            return false;

        aisling.Emblems.SetShown(GuildEmblemProtocol.EMBLEM_KEY, 0, null, guild.Name, id);

        return true;
    }
```

- [ ] **Step 9: World server hook** — in `WorldServer`'s constructor, directly after `EmblemService = emblemService;`, add:

```csharp
        //Guild is built by a mapper without the emblem service; its join and leave paths reach it through this hook
        GuildEmblemRefresh.Handler = emblemService.OnGuildMembershipChanged;
```

- [ ] **Step 10: World list map** — in `AislingMapperProfile`'s `WorldListMemberInfo` map, add `GuildEmblemId = obj.Emblems.ShownGuildEmblemId,` after `EmblemName = emblem.Name,`.

- [ ] **Step 11: Run the tests**

Run each and expect all to pass:

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Emblems/*/*"
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemRefreshTests/*"
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/AislingMapperProfileTests/*"
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildEmblem/*/*"
```

```json:metadata
{"files": ["Chaos/Services/Emblems/AislingEmblems.cs", "Chaos/Services/Emblems/AislingEmblemsSchema.cs", "Chaos/Services/Emblems/EmblemService.cs", "Chaos/Services/Emblems/EmblemCatalog.cs", "Chaos/Services/GuildCloak/GuildEmblemRefresh.cs", "Chaos/Collections/Guild.cs", "Chaos/Services/Servers/WorldServer.cs", "Chaos/Services/MapperProfiles/AislingMapperProfile.cs", "Tests/Chaos.Tests/Emblems/EmblemServiceTests.cs", "Tests/Chaos.Tests/Emblems/AislingEmblemsTests.cs", "Tests/Chaos.Tests/Emblems/EmblemCatalogTests.cs", "Tests/Chaos.Tests/AislingMapperProfileTests.cs", "Tests/Chaos.Tests/GuildEmblem/GuildEmblemRefreshTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.Emblems/*/*\"", "acceptanceCriteria": ["catalog rejects key guild as reserved", "book lists guild entry first only while approved", "EmblemChoice guild only when owned", "offer once per guild at login/join/approval; auto-show only into empty cell; spec messages", "new version updates cached id; clear or leave hides", "emblems.json saves offered guild; ShownAt gives guild name with art 0", "world list map carries GuildEmblemId", "Guild join/detach call the hook; world server sets it", "existing emblem and mapper tests pass"], "modelTier": "frontier"}
```

---

### Task 6: Tibbs purchase, Quill option and admin clear (server scripts)

**Goal:** Council or leader can buy the guild emblem from Tibbs for 5,000,000; Quill offers "Design the guild emblem" to the leader; the admin clear menu can clear the cloak or the emblem.

**Files:**
- Modify: `$SRV/Chaos/Models/World/GuildHouseState.cs`
- Modify: `$SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs`
- Modify: `$SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildCloakScript.cs`
- Modify: `$SRV/Chaos/Scripting/DialogScripts/Temuair/Generic/GuildCloakAdminScript.cs`
- Test: `$SRV/Tests/Chaos.Tests/GuildEmblem/GuildEmblemScriptTests.cs`

**Acceptance Criteria:**
- [ ] Tibbs lists "Purchase Guild Emblem" (→ `tibbs_purchase_emblem`) while the guild does not own it
- [ ] `tibbs_purchase_emblem_confirm` by a council member takes 5,000,000 gold, sets `emblem`, and does not morph the hall or spawn NPCs
- [ ] Quill's `quill_initial` offers "Design the guild emblem" (→ `quill_guildemblem_design`) only to the leader, only when the guild owns it
- [ ] Admin clear confirm: option 1 clears the cloak only, option 2 clears the emblem only
- [ ] Existing `GuildCloakScriptTests` and `GuildHallPermissionTests` still pass

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemScriptTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** — create `Tests/Chaos.Tests/GuildEmblem/GuildEmblemScriptTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Scripting.DialogScripts.Temuair.Generic;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.GuildCloak;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
#endregion

namespace Chaos.Tests.GuildEmblem;

public sealed class GuildEmblemScriptTests
{
    private const string GUILD = "Shinebox";
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static GuildHouseState BuildHouse(out Mock<IStorage<GuildHouseState>> storage)
    {
        storage = new Mock<IStorage<GuildHouseState>>();
        var house = new GuildHouseState(new Mock<IStorage<GuildHouseState>>().Object);
        storage.SetupGet(s => s.Value).Returns(house);

        return house;
    }

    private static GuildCloakService BuildService(GuildHouseState house, GuildCloakState? state = null)
    {
        var cloakStorage = new Mock<IStorage<GuildCloakState>>();
        cloakStorage.SetupGet(s => s.Value).Returns(state ?? new GuildCloakState());

        var houseStorage = new Mock<IStorage<GuildHouseState>>();
        houseStorage.SetupGet(s => s.Value).Returns(house);

        return new GuildCloakService(
            cloakStorage.Object,
            houseStorage.Object,
            new ClientRegistry<IChaosWorldClient>(),
            TimeProvider.System,
            NullLogger<GuildCloakService>.Instance);
    }

    private static GuildUpdateHallScript Tibbs(string dialogKey, Mock<IStorage<GuildHouseState>> houseStorage, out Dialog dialog)
    {
        dialog = MockDialog.Create(dialogKey);

        return new GuildUpdateHallScript(dialog, houseStorage.Object, new Mock<IMerchantFactory>().Object, new ClientRegistry<IChaosWorldClient>());
    }

    [Test]
    public void Tibbs_lists_purchase_guild_emblem_until_it_is_bought()
    {
        var guild = MockGuild.Create(GUILD);
        var source = MockAisling.Create(name: "Council");
        guild.AddMember(source, source);
        var house = BuildHouse(out var houseStorage);

        Tibbs("tibbs_purchase_additions", houseStorage, out var before).OnDisplaying(source);

        before.Options.Should().Contain(o => (o.DialogKey == "tibbs_purchase_emblem") && (o.OptionText == "Purchase Guild Emblem"));

        house.EnableProperty(GUILD, GuildEmblemProtocol.DEED_PROPERTY);
        Tibbs("tibbs_purchase_additions", houseStorage, out var after).OnDisplaying(source);

        after.Options.Should().NotContain(o => o.DialogKey == "tibbs_purchase_emblem");
    }

    [Test]
    public void Tibbs_emblem_confirm_by_a_council_member_takes_five_million_and_does_not_morph_or_spawn()
    {
        var guild = MockGuild.Create(GUILD);
        var source = MockAisling.Create(name: "Council");
        guild.AddMember(source, source);
        guild.ChangeRank(source.Name, 1, source);

        var house = BuildHouse(out var houseStorage);
        source.Gold = GuildEmblemProtocol.PRICE;
        var originalTemplate = source.MapInstance.Template;

        Tibbs("tibbs_purchase_emblem_confirm", houseStorage, out _).OnDisplaying(source);

        source.Gold.Should().Be(0);
        house.HasProperty(GUILD, GuildEmblemProtocol.DEED_PROPERTY).Should().BeTrue();
        source.MapInstance.Template.Should().BeSameAs(originalTemplate, "the emblem adds no room, so the hall map must not be morphed");
        source.MapInstance.GetEntities<Merchant>().Should().BeEmpty("no NPCs are spawned for the emblem");
    }

    [Test]
    public void Quill_offers_the_emblem_design_only_to_the_leader_of_a_guild_that_owns_it()
    {
        var guild = MockGuild.Create(GUILD);
        var leader = MockAisling.Create(name: "Leader");
        var member = MockAisling.Create(name: "Member");
        guild.AddMember(leader, leader);
        guild.ChangeRank(leader.Name, 0, leader);
        guild.AddMember(member, leader);

        var house = BuildHouse(out _);
        var service = BuildService(house);

        var notOwned = MockDialog.Create("quill_initial");
        new GuildCloakScript(notOwned, service, new Mock<IItemFactory>().Object).OnDisplaying(leader);
        notOwned.Options.Should().NotContain(o => o.DialogKey == "quill_guildemblem_design");

        house.EnableProperty(GUILD, GuildEmblemProtocol.DEED_PROPERTY);

        var leaderDialog = MockDialog.Create("quill_initial");
        new GuildCloakScript(leaderDialog, service, new Mock<IItemFactory>().Object).OnDisplaying(leader);

        leaderDialog.Options
                    .Should()
                    .Contain(o => (o.DialogKey == "quill_guildemblem_design") && (o.OptionText == "Design the guild emblem"));

        var memberDialog = MockDialog.Create("quill_initial");
        new GuildCloakScript(memberDialog, service, new Mock<IItemFactory>().Object).OnDisplaying(member);
        memberDialog.Options.Should().NotContain(o => o.DialogKey == "quill_guildemblem_design");
    }

    [Test]
    public void Admin_clear_option_two_clears_the_emblem_and_option_one_clears_the_cloak()
    {
        var admin = MockAisling.Create(name: "Sichi");
        admin.IsAdmin = true;

        var state = new GuildCloakState();
        var cloakId = state.Submit(GUILD, "Lead", GuildCloakDesign.CreateDefault(), Now);
        var emblem = GuildEmblemDesign.CreateDefault();
        emblem.Pixels[0] = 1;
        var emblemId = state.SubmitEmblem(GUILD, "Lead", emblem, Now);
        state.TryApprove(cloakId, "Sichi", Now, out _);
        state.TryApprove(emblemId, "Sichi", Now, out _);

        var service = BuildService(BuildHouse(out _), state);

        var clearEmblem = MockDialog.Create("admintrinket_guildcloak_clear_confirm");
        clearEmblem.Context = GUILD;
        new GuildCloakAdminScript(clearEmblem, service).OnNext(admin, 2);

        state.ApprovedEmblemIdOf(GUILD).Should().Be(0);
        state.ApprovedIdOf(GUILD).Should().Be(cloakId);

        var clearCloak = MockDialog.Create("admintrinket_guildcloak_clear_confirm");
        clearCloak.Context = GUILD;
        new GuildCloakAdminScript(clearCloak, service).OnNext(admin, 1);

        state.ApprovedIdOf(GUILD).Should().Be(0);
    }
}
```


- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemScriptTests/*"`
Expected: FAIL (`Unknown property: emblem`, missing options).

- [ ] **Step 3: `GuildHouseState`** — add `"emblem"     => properties.Emblem,` to `GetPropertyValue`'s switch (after `"cloaks"`), `case "emblem": properties.Emblem = value; break;` to the setter switch (after `"cloaks"`, same layout), and `public bool Emblem { get; set; }` to `HouseProperties` (after `Deed`, keeping alphabetical order).

- [ ] **Step 4: `GuildUpdateHallScript`**

In `tibbs_purchase_additions`, after the cloak option:

```csharp
                if (!guildHouseState.HasProperty(guildName, GuildEmblemProtocol.DEED_PROPERTY))
                    Subject.AddOption("Purchase Guild Emblem", "tibbs_purchase_emblem");
```

Add a case after `tibbs_purchase_cloaks_confirm`:

```csharp
            case "tibbs_purchase_emblem_confirm":
                HandleUpgrade(
                    source,
                    guildName,
                    GuildEmblemProtocol.DEED_PROPERTY,
                    "You do not have enough gold to purchase the guild emblem.",
                    GuildEmblemProtocol.PRICE);

                break;
```

In `HandleUpgrade`, after the cloak branch (`else if (property == GuildCloakProtocol.DEED_PROPERTY) { … }`), add:

```csharp
        } else if (property == GuildEmblemProtocol.DEED_PROPERTY)
        {
            //the emblem adds no room, so the hall map and its NPCs stay as they are
            AnnounceAndEnable(
                source,
                guildHouseState,
                guildName,
                property,
                $"{{=o{source.Name} has purchased a guild emblem! The leader can design it with Quill.");
```

(The closing `}` of the new branch is the `} else` that already starts the room-upgrade branch.) Update the `AnnounceAndEnable` summary: "Shared by the cloak and emblem branches and the room-upgrade branch…".

- [ ] **Step 5: `GuildCloakScript` (Quill)** — in `quill_initial`, after the cloak option:

```csharp
                if (OwnsEmblem(source) && GuildCloakService.IsLeader(source))
                    Subject.AddOption("Design the guild emblem", "quill_guildemblem_design");
```

Add a case:

```csharp
            case "quill_guildemblem_design":
                Subject.Close(source);
                guildCloaks.OpenEmblemEditor(source);

                break;
```

Add the helper next to `OwnsDeed`:

```csharp
    private bool OwnsEmblem(Aisling source) => source.Guild is not null && guildCloaks.OwnsEmblem(source.Guild.Name);
```

Class summary: add "The leader can also open the guild emblem editor once the guild owns the emblem."

- [ ] **Step 6: `GuildCloakAdminScript`** — replace the confirm case in `OnNext` with:

```csharp
            //option 1 clears the cloak, option 2 the emblem; option 3 just closes
            case "admintrinket_guildcloak_clear_confirm" when Subject.Context is string name:
                if (optionIndex == 1)
                    source.SendOrangeBarMessage(
                        guildCloaks.Clear(source, name) ? $"Cleared the cloak design of {name}." : $"{name} has no approved cloak design.");
                else if (optionIndex == 2)
                    source.SendOrangeBarMessage(
                        guildCloaks.ClearEmblem(source, name) ? $"Cleared the guild emblem of {name}." : $"{name} has no approved guild emblem.");

                break;
```

Class summary: "The admin trinket's guild design options: open the review window (cloaks and emblems), or clear a guild's approved cloak or emblem after typing the guild's name and choosing which."

- [ ] **Step 7: Run the tests**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildEmblemScriptTests/*"`
Expected: 4 passed.
Run: `--treenode-filter "/*/*/GuildCloakScriptTests/*"` and `--treenode-filter "/*/*/GuildHallPermissionTests/*"`
Expected: all pass.

```json:metadata
{"files": ["Chaos/Models/World/GuildHouseState.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildCloakScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Generic/GuildCloakAdminScript.cs", "Tests/Chaos.Tests/GuildEmblem/GuildEmblemScriptTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/GuildEmblemScriptTests/*\"", "acceptanceCriteria": ["Tibbs lists Purchase Guild Emblem until bought", "confirm takes 5,000,000, sets emblem, no morph or spawn", "Quill offers Design the guild emblem only to the leader of an owning guild", "admin clear option 1 = cloak, option 2 = emblem", "existing cloak script and hall permission tests pass"], "modelTier": "standard"}
```

---

### Task 7: Unora dialog files and emblem docs

**Goal:** The dialog templates behind Task 6's keys exist, the admin trinket texts say "design", and the emblem docs mention the reserved key.

**Files:**
- Create: `$UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_emblem.json`
- Create: `$UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_emblem_confirm.json`
- Create: `$UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildemblem_design.json`
- Modify: `$UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear.json`, `admintrinket_guildcloak_clear_confirm.json`, `admintrinket_guildcloak_review.json`, `admintrinket_initial.json`
- Modify: `$UNO/docs/emblems.md`

**Acceptance Criteria:**
- [ ] Every new or changed JSON file parses
- [ ] `tibbs_purchase_emblem` offers `tibbs_purchase_emblem_confirm`; the confirm runs script `guildupdatehall`
- [ ] `quill_guildemblem_design` runs script `GuildCloak` and closes
- [ ] The clear confirm has options in the order "Clear the cloak", "Clear the emblem", "No"
- [ ] The admin trinket shows "Review Guild Designs" and "Clear a Guild Design"

**Verify:** `cd $UNO && python -c "import json,glob; [json.load(open(f,encoding='utf-8')) for f in glob.glob('Data/Configuration/Templates/Dialogs/Temauir/**/*.json', recursive=True)]; print('ok')"` → `ok`

**Steps:**

- [ ] **Step 1: `Guild Hall/Tibbs/tibbs_purchase_emblem.json`**

```json
{
  "options": [
    {
      "dialogKey": "tibbs_purchase_emblem_confirm",
      "optionText": "Yes, let's get the guild emblem."
    },
    {
      "dialogKey": "tibbs_initial",
      "optionText": "No thanks."
    }
  ],
  "scriptKeys": [],
  "scriptVars": {},
  "templateKey": "tibbs_purchase_emblem",
  "text": "Would you like a guild emblem? Your leader paints it with Quill, and once an admin approves it, members can show it in the world list. It will be 5 million gold coins.",
  "type": "DialogMenu"
}
```

- [ ] **Step 2: `Guild Hall/Tibbs/tibbs_purchase_emblem_confirm.json`**

```json
{
  "nextDialogKey": "tibbs_initial",
  "options": [],
  "scriptKeys": [
    "guildupdatehall"
  ],
  "scriptVars": {},
  "templateKey": "tibbs_purchase_emblem_confirm",
  "text": "(He flicks his wand around in a fancy sequence)\n Done! Your leader can design the guild emblem with Quill.",
  "type": "Normal"
}
```

- [ ] **Step 3: `Guild Hall/Quill/quill_guildemblem_design.json`**

```json
{
  "nextDialogKey": "Close",
  "options": [],
  "scriptKeys": [
    "GuildCloak"
  ],
  "scriptVars": {},
  "templateKey": "quill_guildemblem_design",
  "text": "Let me fetch the ink and my smallest brush...",
  "type": "Normal"
}
```

- [ ] **Step 4: Admin trinket texts**
  - `admintrinket_guildcloak_clear.json`: `"text": "Which guild's design should be cleared?"`
  - `admintrinket_guildcloak_review.json`: `"text": "Opening the guild design review..."`
  - `admintrinket_initial.json`: option text `"Review Guild Cloaks"` → `"Review Guild Designs"`, `"Clear a Guild Cloak"` → `"Clear a Guild Design"`
  - `admintrinket_guildcloak_clear_confirm.json` becomes:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "Close",
      "optionText": "Clear the cloak"
    },
    {
      "dialogKey": "Close",
      "optionText": "Clear the emblem"
    },
    {
      "dialogKey": "Close",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "GuildCloakAdmin"
  ],
  "scriptVars": {},
  "templateKey": "admintrinket_guildcloak_clear_confirm",
  "text": "Clear which approved design of {0}? Members stop showing it.",
  "type": "DialogMenu"
}
```

- [ ] **Step 5: `docs/emblems.md`** — in the limits list at the end of the "How to add an emblem" section, add: "- The key `guild` is reserved for guild emblems. A file that uses it is skipped with an error."

- [ ] **Step 6: Validate** — run the Verify command. Expected: `ok`.

```json:metadata
{"files": ["Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_emblem.json", "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_emblem_confirm.json", "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildemblem_design.json", "Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear.json", "Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear_confirm.json", "Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_review.json", "Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_initial.json", "docs/emblems.md"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-unora && python -c \"import json,glob; [json.load(open(f,encoding='utf-8')) for f in glob.glob('Data/Configuration/Templates/Dialogs/Temauir/**/*.json', recursive=True)]; print('ok')\"", "acceptanceCriteria": ["all dialog JSON parses", "tibbs_purchase_emblem -> confirm with guildupdatehall", "quill_guildemblem_design runs GuildCloak and closes", "clear confirm options: cloak, emblem, No", "trinket shows Review Guild Designs / Clear a Guild Design"], "modelTier": "mechanical"}
```

---

### Task 8: Client — messages, emblem texture store, and drawing guild emblems

**Goal:** The client receives emblem messages, downloads each emblem id it sees once, and draws guild emblems in the world list and the Emblem tab.

**Files:**
- Modify: `$CLI/Chaos.Client.Networking/Definitions/Delegates.cs`, `$CLI/Chaos.Client.Networking/ConnectionManager.cs`
- Modify: `$CLI/Chaos.Client/Models/WorldListEntry.cs`, `$CLI/Chaos.Client/Collections/WorldState.cs`
- Create: `$CLI/Chaos.Client/Controls/World/Emblems/GuildEmblemTextures.cs`
- Modify: `$CLI/Chaos.Client/Controls/World/Emblems/EmblemIcon.cs`
- Modify: `$CLI/Chaos.Client/Controls/World/Popups/WorldList/WorldListEntryControl.cs`
- Modify: `$CLI/Chaos.Client/Controls/World/Popups/Profile/SelfProfileEmblemTab.cs`, `$CLI/Chaos.Client/Controls/World/Popups/Profile/EmblemBookLayout.cs`
- Create: `$CLI/Chaos.Client/Screens/WorldScreen.GuildEmblem.cs`
- Modify: `$CLI/Chaos.Client/Screens/WorldScreen.cs`
- Test: `$CLI/Tests/Chaos.Client.Tests/GuildEmblemTexturesTests.cs`, `$CLI/Tests/Chaos.Client.Tests/EmblemBookLayoutTests.cs`

**Acceptance Criteria:**
- [ ] `GuildEmblemTextures.ToPixels` makes see-through pixels transparent and painted ones their palette color
- [ ] A missing emblem id is requested at most once every 10 seconds; ids ≤ 0 are never requested
- [ ] `SetLocal` returns a new negative id and forgets the one it replaces
- [ ] Guild entries read "While you are in <guild>" and "While in guild" in the Emblem tab
- [ ] World list rows and Emblem tab slots draw the guild emblem when an id is set; the hover name shows for guild emblems
- [ ] The design store is cleared on logout
- [ ] The client builds against `$SRV`

**Verify:** `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** — create `Tests/Chaos.Client.Tests/GuildEmblemTexturesTests.cs`:

```csharp
using Chaos.Client.Controls.World.Emblems;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class GuildEmblemTexturesTests
{
    [Test]
    public void See_through_pixels_are_transparent_and_painted_ones_take_their_color()
    {
        var design = GuildEmblemDesign.CreateDefault();
        design.Colors.Add(new GuildCloakColor(140, 20, 30));
        design.Pixels[0] = 1;
        design.Pixels[12] = 2;

        var pixels = GuildEmblemTextures.ToPixels(design);

        pixels.Should().HaveCount(GuildEmblemProtocol.PIXEL_COUNT);
        pixels[0].Should().Be(new Color(212, 175, 55));
        pixels[12].Should().Be(new Color(140, 20, 30));
        pixels[1].A.Should().Be(0);
    }

    [Test]
    public void A_server_emblem_is_asked_for_at_most_once_every_ten_seconds()
    {
        //ids unique to this test: the store is static
        GuildEmblemTextures.ShouldRequest(9001, 1_000).Should().BeTrue();
        GuildEmblemTextures.ShouldRequest(9001, 5_000).Should().BeFalse();
        GuildEmblemTextures.ShouldRequest(9001, 11_000).Should().BeTrue();
        GuildEmblemTextures.ShouldRequest(0, 1_000).Should().BeFalse();
        GuildEmblemTextures.ShouldRequest(-3, 1_000).Should().BeFalse();

        GuildEmblemTextures.Set(9001, GuildEmblemDesign.CreateDefault());

        GuildEmblemTextures.ShouldRequest(9001, 50_000).Should().BeFalse();
        GuildEmblemTextures.TryGet(9001, out _).Should().BeTrue();
    }

    [Test]
    public void SetLocal_gives_a_new_negative_id_and_drops_the_one_it_replaces()
    {
        var first = GuildEmblemTextures.SetLocal(0, GuildEmblemDesign.CreateDefault());
        var second = GuildEmblemTextures.SetLocal(first, GuildEmblemDesign.CreateDefault());

        first.Should().BeNegative();
        second.Should().BeLessThan(first);
        GuildEmblemTextures.TryGet(first, out _).Should().BeFalse();
        GuildEmblemTextures.TryGet(second, out _).Should().BeTrue();
    }
}
```

Add to `Tests/Chaos.Client.Tests/EmblemBookLayoutTests.cs` (add `using Chaos.Networking.Entities.Server;` if missing):

```csharp
    [Test]
    public void Guild_emblem_texts_name_the_guild()
    {
        var entry = new EmblemBookEntry { Key = "guild", Name = "Richards", Owned = true, Guild = true, GuildEmblemId = 42 };

        EmblemBookLayout.StatusText(entry).Should().Be("While you are in Richards");
        EmblemBookLayout.TimeLeftText(entry, 0).Should().Be("While in guild");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/GuildEmblemTexturesTests/*"`
Expected: build error, `GuildEmblemTextures` does not exist.

- [ ] **Step 3: Create `Chaos.Client/Controls/World/Emblems/GuildEmblemTextures.cs`**

```csharp
#region
using System.Diagnostics.CodeAnalysis;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Emblems;

/// <summary>
///     Guild emblems by id, and their 11 × 11 textures (built on first draw). Positive ids come from the server; the client
///     asks for each missing one at most once every <see cref="REQUEST_RETRY_MS" />. Negative ids are emblems painted on
///     this client (the editor's draft, an emblem under review): every change gets a new id. Game thread only; cleared on
///     logout. The textures belong to this store: never dispose them elsewhere.
/// </summary>
public static class GuildEmblemTextures
{
    public const long REQUEST_RETRY_MS = 10_000;

    private static readonly Dictionary<int, GuildEmblemDesign> Designs = [];
    private static readonly Dictionary<int, long> RequestedAtMs = [];
    private static readonly Dictionary<int, Texture2D> Textures = [];
    private static int NextLocalId = -1;

    /// <summary>
    ///     Forgets every emblem, texture and pending request, for a new session. Local ids keep counting down, so an emblem
    ///     painted before the clear and one painted after never collide.
    /// </summary>
    public static void Clear()
    {
        Designs.Clear();
        RequestedAtMs.Clear();

        foreach (var texture in Textures.Values)
            texture.Dispose();

        Textures.Clear();
    }

    /// <summary>The emblem's texture, built on first use; null while the emblem is unknown or for id 0.</summary>
    public static Texture2D? Get(int designId)
    {
        if ((designId == 0) || !Designs.TryGetValue(designId, out var design))
            return null;

        if (!Textures.TryGetValue(designId, out var texture))
        {
            texture = new Texture2D(TextureConverter.Device, GuildEmblemProtocol.SIZE, GuildEmblemProtocol.SIZE);
            texture.SetData(ToPixels(design));
            Textures[designId] = texture;
        }

        return texture;
    }

    public static void Set(int designId, GuildEmblemDesign design)
    {
        DropTexture(designId);
        Designs[designId] = design;
        RequestedAtMs.Remove(designId);
    }

    /// <summary>Stores an emblem painted on this client under a new negative id, and drops the local emblem it replaces.</summary>
    public static int SetLocal(int replacedId, GuildEmblemDesign design)
    {
        if (replacedId < 0)
        {
            DropTexture(replacedId);
            Designs.Remove(replacedId);
        }

        var id = NextLocalId--;
        Designs[id] = design;

        return id;
    }

    /// <summary>True when a server emblem is missing and was not asked for in the last <see cref="REQUEST_RETRY_MS" />.</summary>
    public static bool ShouldRequest(int designId, long nowMs)
    {
        if ((designId <= 0) || Designs.ContainsKey(designId))
            return false;

        if (RequestedAtMs.TryGetValue(designId, out var askedAt) && ((nowMs - askedAt) < REQUEST_RETRY_MS))
            return false;

        RequestedAtMs[designId] = nowMs;

        return true;
    }

    /// <summary>The emblem's pixels, row-major. See-through (0) and out-of-range numbers are transparent.</summary>
    public static Color[] ToPixels(GuildEmblemDesign design)
    {
        var pixels = new Color[GuildEmblemProtocol.PIXEL_COUNT];

        for (var i = 0; (i < pixels.Length) && (i < design.Pixels.Length); i++)
        {
            var number = design.Pixels[i];

            if ((number == 0) || (number > design.Colors.Count))
                continue;

            var color = design.Colors[number - 1];
            pixels[i] = new Color(color.R, color.G, color.B);
        }

        return pixels;
    }

    public static bool TryGet(int designId, [MaybeNullWhen(false)] out GuildEmblemDesign design)
    {
        design = null;

        return (designId != 0) && Designs.TryGetValue(designId, out design);
    }

    private static void DropTexture(int designId)
    {
        if (Textures.Remove(designId, out var texture))
            texture.Dispose();
    }
}
```

If `TextureConverter` lives in another namespace than `Chaos.Client.Rendering`, copy the `using` from `Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakCanvas.cs`, which uses `TextureConverter.Device` the same way.

- [ ] **Step 4: Emblem tab texts** — in `EmblemBookLayout.cs`: at the top of `StatusText` add

```csharp
        if (entry.Guild)
            return $"While you are in {entry.Name}";
```

and at the top of `TimeLeftText` add

```csharp
        if (entry.Guild)
            return "While in guild";
```

Update `StatusText`'s summary: "The line under the description: the guild for the guild emblem, who holds a record, when the player earned it, or Locked."

- [ ] **Step 5: Run the two test classes**

Run: `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/GuildEmblemTexturesTests/*"` and the same with `EmblemBookLayoutTests`.
Expected: all pass.

- [ ] **Step 6: Networking** — in `Chaos.Client.Networking/Definitions/Delegates.cs`, after `GuildCloakReviewListHandler`:

```csharp
public delegate void GuildEmblemEditorHandler(GuildEmblemEditorArgs args);

public delegate void GuildEmblemDesignHandler(GuildEmblemDesignArgs args);
```

(copy the doc-comment style of the neighbouring delegates). In `ConnectionManager.cs`:
- events, after `OnGuildCloakReviewList`:

```csharp
    /// <summary>Fired when the server opens the guild emblem editor or updates its status line.</summary>
    public event GuildEmblemEditorHandler? OnGuildEmblemEditor;

    /// <summary>Fired when an approved guild emblem arrives.</summary>
    public event GuildEmblemDesignHandler? OnGuildEmblemDesign;
```

- sends, after `SendGuildCloakReviewInteraction`:

```csharp
    /// <summary>Sends a save or submit from the guild emblem editor. The server checks the sender and the emblem.</summary>
    public void SendGuildEmblemEditorInteraction(GuildEmblemEditorInteractionArgs args) => SendIfWorld(args);

    /// <summary>Asks for one approved guild emblem.</summary>
    public void SendGuildEmblemDesignRequest(int designId) => SendIfWorld(new GuildEmblemDesignRequestArgs { DesignId = designId });
```

- registration, after `PacketHandlers[(byte)ServerOpCode.GuildCloakReviewList] = HandleGuildCloakReviewList;`:

```csharp
        PacketHandlers[(byte)ServerOpCode.GuildEmblemEditor] = HandleGuildEmblemEditor;
        PacketHandlers[(byte)ServerOpCode.GuildEmblemDesign] = HandleGuildEmblemDesign;
```

- handlers, after `HandleGuildCloakReviewList`:

```csharp
    private void HandleGuildEmblemEditor(ServerPacket pkt)
    {
        var args = Client.Deserialize<GuildEmblemEditorArgs>(in pkt);
        OnGuildEmblemEditor?.Invoke(args);
    }

    private void HandleGuildEmblemDesign(ServerPacket pkt)
    {
        var args = Client.Deserialize<GuildEmblemDesignArgs>(in pkt);
        OnGuildEmblemDesign?.Invoke(args);
    }
```

- [ ] **Step 7: World list model** — `Models/WorldListEntry.cs`: add a last parameter `int GuildEmblemId = 0`. In `Collections/WorldState.cs`, the `OnWorldList` mapping adds `m.GuildEmblemId` after `m.EmblemName`.

- [ ] **Step 8: `EmblemIcon`** — add the property and draw from the store when it is set:

```csharp
    /// <summary>A guild emblem's id; when not 0 it is drawn instead of <see cref="Art" />.</summary>
    public int GuildEmblemId { get; set; }
```

In `Draw`, replace `if (EmblemTextures.Get(Art, Environment.TickCount64) is not { } texture)` with:

```csharp
        var texture = GuildEmblemId != 0 ? GuildEmblemTextures.Get(GuildEmblemId) : EmblemTextures.Get(Art, Environment.TickCount64);

        if (texture is null)
            return;
```

Update the class summary: "…It never disposes its texture: emblem textures belong to UiRenderer's cache, and guild emblem textures to <see cref=\"GuildEmblemTextures\" />."

- [ ] **Step 9: World list row** — in `WorldListEntryControl.Clear()`, add `Emblem.GuildEmblemId = 0;` after `Emblem.Art = 0;`. In `SetEntry`, replace the two emblem lines with:

```csharp
        Emblem.Art = entry.EmblemArt;
        Emblem.GuildEmblemId = entry.GuildEmblemId;
        EmblemName = (entry.EmblemArt == 0) && (entry.GuildEmblemId == 0) ? string.Empty : entry.EmblemName;
```

- [ ] **Step 10: Emblem tab** — in `SelfProfileEmblemTab.BindPage`, after `Slots[i].Art = entry?.Art ?? 0;` add `Slots[i].GuildEmblemId = entry?.GuildEmblemId ?? 0;`. In `BindSelection`, after `Preview.Art = entry?.Art ?? 0;` add `Preview.GuildEmblemId = entry?.GuildEmblemId ?? 0;`.

- [ ] **Step 11: Create `Chaos.Client/Screens/WorldScreen.GuildEmblem.cs`**

```csharp
#region
using Chaos.Client.Controls.World.Emblems;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Guild emblems: asking for the emblems the world list and the emblem book name, and storing them as they arrive.</summary>
public sealed partial class WorldScreen
{
    private void WireGuildEmblem()
    {
        Game.Connection.OnGuildEmblemDesign += HandleGuildEmblemDesign;
        Game.Connection.OnWorldList += RequestWorldListGuildEmblems;
        Game.Connection.OnEmblemBook += RequestBookGuildEmblems;
    }

    private void UnwireGuildEmblem()
    {
        Game.Connection.OnGuildEmblemDesign -= HandleGuildEmblemDesign;
        Game.Connection.OnWorldList -= RequestWorldListGuildEmblems;
        Game.Connection.OnEmblemBook -= RequestBookGuildEmblems;
    }

    private void HandleGuildEmblemDesign(GuildEmblemDesignArgs args)
    {
        if (args.Design.IsValid())
            GuildEmblemTextures.Set(args.DesignId, args.Design);
    }

    private void RequestBookGuildEmblems(EmblemBookArgs args)
    {
        foreach (var entry in args.Entries)
            RequestGuildEmblem(entry.GuildEmblemId);
    }

    private void RequestGuildEmblem(int designId)
    {
        if (GuildEmblemTextures.ShouldRequest(designId, Environment.TickCount64))
            Game.Connection.SendGuildEmblemDesignRequest(designId);
    }

    private void RequestWorldListGuildEmblems(WorldListArgs args)
    {
        foreach (var member in args.CountryList)
            RequestGuildEmblem(member.GuildEmblemId);
    }
}
```

- [ ] **Step 12: `WorldScreen.cs`** — after `WireGuildCloak();` add `WireGuildEmblem();`; after `UnwireGuildCloak();` add `UnwireGuildEmblem();`; after `Game.AislingRenderer.GuildCloaks.Clear();` add `GuildEmblemTextures.Clear();` (add `using Chaos.Client.Controls.World.Emblems;` if missing).

- [ ] **Step 13: Build and run all client tests**

Run: `cd $CLI && UnoraServerPath=$SRV dotnet build Chaos.Client/Chaos.Client.csproj`
Expected: build succeeds with no new warnings in the touched files.
Run: `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: all pass.

```json:metadata
{"files": ["Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client.Networking/ConnectionManager.cs", "Chaos.Client/Models/WorldListEntry.cs", "Chaos.Client/Collections/WorldState.cs", "Chaos.Client/Controls/World/Emblems/GuildEmblemTextures.cs", "Chaos.Client/Controls/World/Emblems/EmblemIcon.cs", "Chaos.Client/Controls/World/Popups/WorldList/WorldListEntryControl.cs", "Chaos.Client/Controls/World/Popups/Profile/SelfProfileEmblemTab.cs", "Chaos.Client/Controls/World/Popups/Profile/EmblemBookLayout.cs", "Chaos.Client/Screens/WorldScreen.GuildEmblem.cs", "Chaos.Client/Screens/WorldScreen.cs", "Tests/Chaos.Client.Tests/GuildEmblemTexturesTests.cs", "Tests/Chaos.Client.Tests/EmblemBookLayoutTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client && UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["ToPixels: see-through transparent, painted = palette color", "missing id requested at most every 10 s; ids <= 0 never", "SetLocal new negative id, drops replaced", "guild entry texts While you are in <guild> / While in guild", "world list and Emblem tab draw guild emblem by id; hover name shows", "store cleared on logout", "client builds against the server worktree"], "modelTier": "standard"}
```

---

### Task 9: Client — the compact emblem editor

**Goal:** The leader's emblem editor window (layout B): 11 × 11 canvas at 16×, see-through + 6 color boxes, pencil/fill/pick/mirror/undo/redo, previews at 1×/2×/3×, status line, Save draft and Submit.

**Files:**
- Create: `$CLI/Chaos.Client/ViewModel/GuildEmblemEditorModel.cs`
- Create: `$CLI/Chaos.Client/Controls/World/Popups/GuildEmblem/GuildEmblemCanvas.cs`
- Create: `$CLI/Chaos.Client/Controls/World/Popups/GuildEmblem/GuildEmblemSwatches.cs`
- Create: `$CLI/Chaos.Client/Controls/World/Popups/GuildEmblem/GuildEmblemPreviewStrip.cs`
- Create: `$CLI/Chaos.Client/Controls/World/Popups/GuildEmblem/GuildEmblemEditorControl.cs`
- Modify: `$CLI/Chaos.Client/Screens/WorldScreen.GuildEmblem.cs`
- Test: `$CLI/Tests/Chaos.Client.Tests/GuildEmblemEditorModelTests.cs`

**Acceptance Criteria:**
- [ ] Pencil paints the selected color; mirror paints column `10 − x` too
- [ ] Color 0 (see-through) erases; pick selects a cell's color, including 0
- [ ] Fill colors only the 4-connected area of the same color
- [ ] A stroke undoes as one step; redo restores it
- [ ] `LoadSaved` keeps unsaved painting while the window is open
- [ ] The window opens from `GuildEmblemEditor` Open, shows "Guild Emblem - <guild>", sends Save draft/Submit, and disables Submit while nothing is painted
- [ ] The client builds against `$SRV`

**Verify:** `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/GuildEmblemEditorModelTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** — create `Tests/Chaos.Client.Tests/GuildEmblemEditorModelTests.cs`:

```csharp
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildEmblemEditorModelTests
{
    [Test]
    public void Pencil_paints_the_selected_color_and_mirror_paints_the_matching_column()
    {
        var model = new GuildEmblemEditorModel { Mirror = true };

        model.Apply(2, 5);

        model.CellAt(2, 5).Should().Be(1);
        model.CellAt(8, 5).Should().Be(1);
        model.IsDirty.Should().BeTrue();
    }

    [Test]
    public void See_through_erases()
    {
        var model = new GuildEmblemEditorModel();
        model.Apply(0, 0);

        model.SelectColor(0);
        model.Apply(0, 0);

        model.CellAt(0, 0).Should().Be(0);
        model.Design.HasPaint().Should().BeFalse();
    }

    [Test]
    public void Fill_colors_the_connected_area_only()
    {
        var model = new GuildEmblemEditorModel();
        model.AddColor(new GuildCloakColor(140, 20, 30));
        model.SelectColor(1);

        for (var y = 0; y < GuildEmblemProtocol.SIZE; y++)
            model.Apply(5, y);

        model.SelectColor(2);
        model.Tool = GuildCloakTool.Fill;
        model.Apply(0, 0);

        model.CellAt(4, 10).Should().Be(2);
        model.CellAt(5, 3).Should().Be(1);
        model.CellAt(6, 0).Should().Be(0);
    }

    [Test]
    public void A_stroke_undoes_as_one_step_and_redo_brings_it_back()
    {
        var model = new GuildEmblemEditorModel();

        model.BeginStroke();
        model.Apply(1, 1);
        model.Apply(2, 1);
        model.EndStroke();

        model.Undo();
        model.Design.HasPaint().Should().BeFalse();

        model.Redo();
        model.CellAt(1, 1).Should().Be(1);
        model.CellAt(2, 1).Should().Be(1);
    }

    [Test]
    public void Pick_selects_the_cell_color_including_see_through()
    {
        var model = new GuildEmblemEditorModel();
        model.Apply(3, 3);
        model.Tool = GuildCloakTool.Pick;

        model.Apply(4, 4);
        model.SelectedColor.Should().Be(0);

        model.Apply(3, 3);
        model.SelectedColor.Should().Be(1);
    }

    [Test]
    public void LoadSaved_keeps_unsaved_painting_while_the_window_is_open()
    {
        var model = new GuildEmblemEditorModel();
        model.Apply(0, 0);

        model.LoadSaved(GuildEmblemDesign.CreateDefault(), true).Should().BeFalse();
        model.CellAt(0, 0).Should().Be(1);

        model.LoadSaved(GuildEmblemDesign.CreateDefault(), false).Should().BeTrue();
        model.CellAt(0, 0).Should().Be(0);
        model.IsDirty.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/GuildEmblemEditorModelTests/*"`
Expected: build error, `GuildEmblemEditorModel` does not exist.

- [ ] **Step 3: Create `Chaos.Client/ViewModel/GuildEmblemEditorModel.cs`**

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The guild emblem editor's state: the emblem being painted, the chosen color (0 = see-through, which erases), the
///     tool, mirror painting, and undo and redo. <see cref="Version" /> changes on every visible change, so the window knows
///     when to redraw. It uses the cloak editor's <see cref="GuildCloakTool" />s.
/// </summary>
public sealed class GuildEmblemEditorModel
{
    public const int MAX_UNDO = 50;
    private const int SIZE = GuildEmblemProtocol.SIZE;

    private readonly List<GuildEmblemDesign> RedoSteps = [];
    private readonly List<GuildEmblemDesign> UndoSteps = [];
    private GuildEmblemDesign? StrokeStart;
    private bool StrokeRecorded;

    public bool CanRedo => RedoSteps.Count > 0;
    public bool CanUndo => UndoSteps.Count > 0;
    public GuildEmblemDesign Design { get; private set; } = GuildEmblemDesign.CreateDefault();
    public bool IsDirty { get; private set; }
    public bool Mirror { get; set; }

    /// <summary>The color painted with: 1 up to the color count, or 0 for see-through.</summary>
    public int SelectedColor { get; private set; } = 1;

    public GuildCloakTool Tool { get; set; } = GuildCloakTool.Pencil;
    public int Version { get; private set; }

    /// <summary>Adds a color and selects it. False when the emblem already has <see cref="GuildEmblemProtocol.MAX_COLORS" />.</summary>
    public bool AddColor(GuildCloakColor color)
    {
        if (Design.Colors.Count >= GuildEmblemProtocol.MAX_COLORS)
            return false;

        Record();
        Design.Colors.Add(color);
        SelectedColor = Design.Colors.Count;
        Changed();

        return true;
    }

    /// <summary>Uses the current tool on a pixel. Pencil and fill change the emblem (and the mirrored pixel with mirror on); pick selects the pixel's color.</summary>
    public void Apply(int x, int y)
    {
        if (!InGrid(x, y))
            return;

        switch (Tool)
        {
            case GuildCloakTool.Pencil:
                SetCell(x, y);

                if (Mirror)
                    SetCell(MirrorX(x), y);

                break;

            case GuildCloakTool.Fill:
                Fill(x, y);

                if (Mirror)
                    Fill(MirrorX(x), y);

                break;

            case GuildCloakTool.Pick:
                SelectColor(CellAt(x, y));

                break;
        }
    }

    /// <summary>Starts a stroke: every change until <see cref="EndStroke" /> undoes as one step.</summary>
    public void BeginStroke()
    {
        StrokeStart = Design.DeepCopy();
        StrokeRecorded = false;
    }

    public byte CellAt(int x, int y) => Design.Pixels[(y * SIZE) + x];

    public void EndStroke() => StrokeStart = null;

    /// <summary>Starts over from an emblem sent by the server: no history, color 1 selected, nothing unsaved.</summary>
    public void Load(GuildEmblemDesign design)
    {
        Design = design.DeepCopy();
        SelectedColor = 1;
        UndoSteps.Clear();
        RedoSteps.Clear();
        StrokeStart = null;
        IsDirty = false;
        Version++;
    }

    /// <summary>Loads the guild's saved emblem, unless the window is already open with unsaved painting, which is kept.</summary>
    /// <returns><c>true</c> if the saved emblem was loaded.</returns>
    public bool LoadSaved(GuildEmblemDesign saved, bool windowOpen)
    {
        if (windowOpen && IsDirty)
            return false;

        Load(saved);

        return true;
    }

    public void MarkSaved()
    {
        IsDirty = false;
        Version++;
    }

    /// <summary>The column that mirrors <paramref name="x" /> across the emblem's middle column.</summary>
    public static int MirrorX(int x) => SIZE - 1 - x;

    public void Redo()
    {
        if (RedoSteps.Count == 0)
            return;

        UndoSteps.Add(Design);
        Design = Pop(RedoSteps);
        AfterHistoryStep();
    }

    /// <summary>Selects a color by number: 0 for see-through, or 1 up to the color count.</summary>
    public void SelectColor(int number)
    {
        if ((number < 0) || (number > Design.Colors.Count) || (number == SelectedColor))
            return;

        SelectedColor = number;
        Version++;
    }

    /// <summary>Changes one color. The color picker calls this while dragging, inside a stroke, so the whole drag undoes as one step.</summary>
    public void SetColor(int number, GuildCloakColor color)
    {
        if ((number < 1) || (number > Design.Colors.Count) || (Design.Colors[number - 1] == color))
            return;

        Record();
        Design.Colors[number - 1] = color;
        Changed();
    }

    public void Undo()
    {
        if (UndoSteps.Count == 0)
            return;

        RedoSteps.Add(Design);
        Design = Pop(UndoSteps);
        AfterHistoryStep();
    }

    private void AfterHistoryStep()
    {
        StrokeStart = null;
        SelectedColor = Math.Clamp(SelectedColor, 0, Design.Colors.Count);
        IsDirty = true;
        Version++;
    }

    private void Changed()
    {
        IsDirty = true;
        Version++;
    }

    private void Fill(int x, int y)
    {
        var from = CellAt(x, y);

        if (from == SelectedColor)
            return;

        Record();
        var pending = new Stack<(int X, int Y)>();
        pending.Push((x, y));

        while (pending.Count > 0)
        {
            (var cx, var cy) = pending.Pop();

            if (!InGrid(cx, cy) || (Design.Pixels[(cy * SIZE) + cx] != from))
                continue;

            Design.Pixels[(cy * SIZE) + cx] = (byte)SelectedColor;
            pending.Push((cx + 1, cy));
            pending.Push((cx - 1, cy));
            pending.Push((cx, cy + 1));
            pending.Push((cx, cy - 1));
        }

        Changed();
    }

    private static bool InGrid(int x, int y) => (x >= 0) && (y >= 0) && (x < SIZE) && (y < SIZE);

    private static GuildEmblemDesign Pop(List<GuildEmblemDesign> steps)
    {
        var last = steps[^1];
        steps.RemoveAt(steps.Count - 1);

        return last;
    }

    /// <summary>Saves the emblem as an undo step before it changes: once per stroke, or once per change outside a stroke.</summary>
    private void Record()
    {
        if (StrokeStart is not null)
        {
            if (StrokeRecorded)
                return;

            UndoSteps.Add(StrokeStart);
            StrokeRecorded = true;
        } else
            UndoSteps.Add(Design.DeepCopy());

        if (UndoSteps.Count > MAX_UNDO)
            UndoSteps.RemoveAt(0);

        RedoSteps.Clear();
    }

    private void SetCell(int x, int y)
    {
        var index = (y * SIZE) + x;

        if (Design.Pixels[index] == SelectedColor)
            return;

        Record();
        Design.Pixels[index] = (byte)SelectedColor;
        Changed();
    }
}
```

- [ ] **Step 4: Run the model tests**

Run: `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/GuildEmblemEditorModelTests/*"`
Expected: 6 passed.

- [ ] **Step 5: Create `Controls/World/Popups/GuildEmblem/GuildEmblemCanvas.cs`**

Copy the `using` block from `GuildCloakCanvas.cs` (it has the right namespaces for `UIElement`, `MouseDownEvent`, `InputBuffer`, `TextureConverter`), and add `using Chaos.Client.Controls.World.Emblems;` and `using Chaos.DarkAges.Definitions;`.

```csharp
namespace Chaos.Client.Controls.World.Popups.GuildEmblem;

/// <summary>
///     An 11 × 11 guild emblem drawn at <see cref="Zoom" /> on a checker board, so see-through pixels read as empty. Unless
///     <see cref="ReadOnly" />, left-button strokes raise pixel events in grid coordinates.
/// </summary>
public sealed class GuildEmblemCanvas : UIElement
{
    private const int SIZE = GuildEmblemProtocol.SIZE;
    private static readonly Color CheckDark = new(52, 52, 52);
    private static readonly Color CheckLight = new(72, 72, 72);
    private static readonly Color GridLine = new(0, 0, 0, 60);

    private GuildEmblemDesign? Design;
    private bool Dirty;
    private Point LastCell;
    private bool Painting;
    private Texture2D? Texture;

    public GuildEmblemCanvas(int zoom)
    {
        Zoom = zoom;
        Width = SIZE * zoom;
        Height = SIZE * zoom;
    }

    public bool ReadOnly { get; set; }
    public int Zoom { get; }

    public event Action<int, int>? CellPainted;
    public event Action? StrokeEnded;
    public event Action? StrokeStarted;

    public override void Dispose()
    {
        Texture?.Dispose();
        Texture = null;
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        for (var y = 0; y < SIZE; y++)
            for (var x = 0; x < SIZE; x++)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(ScreenX + (x * Zoom), ScreenY + (y * Zoom), Zoom, Zoom),
                    ((x + y) % 2) == 0 ? CheckDark : CheckLight);

        if (Dirty)
            RebuildTexture();

        if (Texture is not null)
            DrawTextureFitted(spriteBatch, Texture, new Rectangle(ScreenX, ScreenY, Width, Height), Color.White);

        for (var i = 1; i < SIZE; i++)
        {
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (i * Zoom), ScreenY, 1, Height), GridLine);
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + (i * Zoom), Width, 1), GridLine);
        }
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (ReadOnly || Painting || (e.Button != MouseButton.Left))
            return;

        Painting = true;
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
        if (!InputBuffer.IsLeftButtonHeld)
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

    public void SetDesign(GuildEmblemDesign design)
    {
        Design = design;
        Dirty = true;
    }

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
        if ((x >= 0) && (y >= 0) && (x < SIZE) && (y < SIZE))
            CellPainted?.Invoke(x, y);
    }

    /// <summary>Paints every pixel on the straight line after <paramref name="from" /> up to <paramref name="to" />, so a fast drag leaves no gaps.</summary>
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

    private void RebuildTexture()
    {
        Dirty = false;

        if (Design is null)
            return;

        //created once; later rebuilds only upload new pixels
        Texture ??= new Texture2D(TextureConverter.Device, SIZE, SIZE);
        Texture.SetData(GuildEmblemTextures.ToPixels(Design));
    }
}
```

- [ ] **Step 6: Create `Controls/World/Popups/GuildEmblem/GuildEmblemSwatches.cs`**

Copy the `using` block from `GuildCloakSwatches.cs`.

```csharp
namespace Chaos.Client.Controls.World.Popups.GuildEmblem;

/// <summary>
///     The emblem editor's color row: a see-through box, then six color boxes. Clicking a box selects it; clicking the
///     selected color, or an empty box (which adds a gray color), asks to open the color picker.
/// </summary>
public sealed class GuildEmblemSwatches : UIElement
{
    public const int BOX = 22;
    public const int COUNT = GuildEmblemProtocol.MAX_COLORS + 1;
    public const int GAP = 3;

    /// <summary>The whole row's width, for laying out the window around it.</summary>
    public const int TOTAL_WIDTH = (COUNT * BOX) + ((COUNT - 1) * GAP);

    private static readonly Color CheckDark = new(52, 52, 52);
    private static readonly Color CheckLight = new(96, 96, 96);

    private readonly GuildEmblemEditorModel Model;

    public GuildEmblemSwatches(GuildEmblemEditorModel model)
    {
        Model = model;
        Width = TOTAL_WIDTH;
        Height = BOX;
    }

    /// <summary>Raised with a color number (1-based) when the picker should open for it.</summary>
    public event Action<int>? EditRequested;

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        var colors = Model.Design.Colors;

        for (var i = 0; i < COUNT; i++)
        {
            var box = BoxBounds(i);

            if (i == 0)
            {
                //see-through: a two-by-two checker
                var half = BOX / 2;
                DrawRectClipped(spriteBatch, new Rectangle(box.X, box.Y, half, half), CheckLight);
                DrawRectClipped(spriteBatch, new Rectangle(box.X + half, box.Y, BOX - half, half), CheckDark);
                DrawRectClipped(spriteBatch, new Rectangle(box.X, box.Y + half, half, BOX - half), CheckDark);
                DrawRectClipped(spriteBatch, new Rectangle(box.X + half, box.Y + half, BOX - half, BOX - half), CheckLight);
                DrawBorder(spriteBatch, box, Model.SelectedColor == 0 ? Color.White : LegendColors.Gray);

                continue;
            }

            if (i <= colors.Count)
            {
                DrawRectClipped(spriteBatch, box, new Color(colors[i - 1].R, colors[i - 1].G, colors[i - 1].B));
                DrawBorder(spriteBatch, box, i == Model.SelectedColor ? Color.White : LegendColors.Gray);
            } else
            {
                DrawBorder(spriteBatch, box, LegendColors.Gray);
                DrawTextClipped(spriteBatch, new Vector2(box.X + 8, box.Y + 5), "+", LegendColors.Gray, false);
            }
        }
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        for (var i = 0; i < COUNT; i++)
        {
            if (!BoxBounds(i).Contains(e.ScreenX, e.ScreenY))
                continue;

            if (i == 0)
                Model.SelectColor(0);
            else if (i > Model.Design.Colors.Count)
            {
                if (Model.AddColor(new GuildCloakColor(128, 128, 128)))
                    EditRequested?.Invoke(Model.SelectedColor);
            } else if (i == Model.SelectedColor)
                EditRequested?.Invoke(i);
            else
                Model.SelectColor(i);

            return;
        }
    }

    private Rectangle BoxBounds(int index) => new(ScreenX + (index * (BOX + GAP)), ScreenY, BOX, BOX);
}
```

- [ ] **Step 7: Create `Controls/World/Popups/GuildEmblem/GuildEmblemPreviewStrip.cs`**

Copy the `using` block from `GuildCloakSwatches.cs` and add `using Chaos.Client.Controls.World.Emblems;`.

```csharp
namespace Chaos.Client.Controls.World.Popups.GuildEmblem;

/// <summary>
///     An emblem at every size the game shows it: in a world list row (1×), then at the Emblem tab's grid (2×) and large box
///     (3×) sizes. Draws <see cref="DesignId" /> from <see cref="GuildEmblemTextures" />.
/// </summary>
public sealed class GuildEmblemPreviewStrip : UIElement
{
    public const int ROW_HEIGHT = 15;
    public const int ROW_WIDTH = 90;
    public const int TOTAL_HEIGHT = 3 * SIZE;
    public const int TOTAL_WIDTH = ROW_WIDTH + GAP + (2 * SIZE) + GAP + (3 * SIZE);
    private const int GAP = 8;
    private const int SIZE = GuildEmblemProtocol.SIZE;

    public GuildEmblemPreviewStrip()
    {
        Width = TOTAL_WIDTH;
        Height = TOTAL_HEIGHT;
    }

    public int DesignId { get; set; }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        var rowY = ScreenY + ((TOTAL_HEIGHT - ROW_HEIGHT) / 2);
        DrawRectClipped(spriteBatch, new Rectangle(ScreenX, rowY, ROW_WIDTH, ROW_HEIGHT), Color.Black);
        DrawTextClipped(spriteBatch, new Vector2(ScreenX + 3, rowY + 2), "World list", LegendColors.Gray, false);

        if (GuildEmblemTextures.Get(DesignId) is not { } texture)
            return;

        DrawTextureFitted(spriteBatch, texture, new Rectangle(ScreenX + ROW_WIDTH - SIZE - 2, rowY + 2, SIZE, SIZE), Color.White);

        var doubleX = ScreenX + ROW_WIDTH + GAP;
        DrawTextureFitted(spriteBatch, texture, new Rectangle(doubleX, ScreenY + ((TOTAL_HEIGHT - (2 * SIZE)) / 2), 2 * SIZE, 2 * SIZE), Color.White);
        DrawTextureFitted(spriteBatch, texture, new Rectangle(doubleX + (2 * SIZE) + GAP, ScreenY, 3 * SIZE, 3 * SIZE), Color.White);
    }
}
```

- [ ] **Step 8: Create `Controls/World/Popups/GuildEmblem/GuildEmblemEditorControl.cs`**

Copy the `using` block from `GuildCloakEditorControl.cs` and add `using Chaos.Client.Controls.World.Emblems;` and `using Chaos.Client.Controls.World.Popups.GuildCloak;`.

```csharp
namespace Chaos.Client.Controls.World.Popups.GuildEmblem;

/// <summary>
///     The guild leader's emblem editor (spec: docs/superpowers/specs/2026-09-28-guild-emblem-design.md, layout B): an
///     11 × 11 canvas, a see-through box and six colors with the Theatre color picker, pencil, fill, pick, mirror, undo and
///     redo, previews at world list and Emblem tab sizes, and Save draft / Submit. Opened by the server's GuildEmblemEditor
///     Open from Quill.
/// </summary>
/// <remarks>
///     Layout, top to bottom: title, canvas, color row, two rows of three tools, preview strip, status line, then Save draft
///     and Submit on the right; under that is the frame's ornate bottom border with the Close button.
/// </remarks>
public sealed class GuildEmblemEditorControl : GuildCloakDialogBase
{
    private const int BORDER_GAP = 4;
    private const string CLOSE_WARNING = "Unsaved changes. Press Close again to discard them.";
    private const int CONTENT_TOP = 32;
    private const int CONTENT_WIDTH = GuildEmblemProtocol.SIZE * ZOOM;
    private const int GAP = 6;
    private const int LEFT = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int OK_RIGHT_MARGIN = 20;
    private const int SAVE_WIDTH = 76;
    private const long SEND_COOLDOWN_MS = 2_500;
    private const int STATUS_LINES = 3;
    private const int SUBMIT_WIDTH = 64;
    private const int TITLE_TOP = 10;
    private const int TOOL_SPACING = 3;
    private const int TOOL_WIDTH = (CONTENT_WIDTH - (2 * TOOL_SPACING)) / 3;
    private const int ZOOM = 16;

    private readonly GuildEmblemCanvas Canvas;
    private readonly CustomButton FillButton;
    private readonly CustomButton MirrorButton;
    private readonly GuildEmblemEditorModel Model = new();
    private readonly CustomButton PencilButton;
    private readonly CustomButton PickButton;
    private readonly StageColorPicker Picker;
    private readonly GuildEmblemPreviewStrip Preview;
    private readonly CustomButton RedoButton;
    private readonly CustomButton SaveButton;
    private readonly UILabel StatusLabel;
    private readonly CustomButton SubmitButton;
    private readonly UILabel TitleLabel;
    private readonly CustomButton UndoButton;

    private bool CloseArmed;

    //true from the picker's first color change until its drag ends: the whole drag undoes as one step
    private bool ColorDragging;
    private int EditingColor;
    private long LastSendMs = long.MinValue / 2;
    private int PreviewId;
    private GuildEmblemDesign? SentDesign;
    private int ShownVersion = -1;
    private GuildCloakStatus Status;
    private string StatusReason = string.Empty;

    public GuildEmblemEditorControl()
        : base("_nsett", false)
    {
        Name = "GuildEmblemEditor";
        Visible = false;
        UsesControlStack = true;

        Canvas = new GuildEmblemCanvas(ZOOM)
        {
            X = LEFT,
            Y = CONTENT_TOP
        };

        var swatchTop = Canvas.Y + Canvas.Height + GAP;
        var toolsTop = swatchTop + GuildEmblemSwatches.BOX + GAP;
        var secondToolRow = toolsTop + CustomButton.HEIGHT + TOOL_SPACING;
        var previewTop = secondToolRow + CustomButton.HEIGHT + GAP;
        var statusTop = previewTop + GuildEmblemPreviewStrip.TOTAL_HEIGHT + GAP;
        var buttonsTop = statusTop + (STATUS_LINES * TextRenderer.CHAR_HEIGHT) + GAP;

        Width = LEFT + CONTENT_WIDTH + LEFT;
        Height = buttonsTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(RequestClose, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        TitleLabel = Caption("Guild Emblem", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        Canvas.StrokeStarted += Model.BeginStroke;
        Canvas.CellPainted += Model.Apply;
        Canvas.StrokeEnded += Model.EndStroke;
        AddChild(Canvas);

        var swatches = new GuildEmblemSwatches(Model)
        {
            X = LEFT + ((CONTENT_WIDTH - GuildEmblemSwatches.TOTAL_WIDTH) / 2),
            Y = swatchTop
        };

        swatches.EditRequested += OpenPicker;
        AddChild(swatches);

        PencilButton = ToolButton("Pencil", 0, toolsTop, () => Model.Tool = GuildCloakTool.Pencil);
        FillButton = ToolButton("Fill", 1, toolsTop, () => Model.Tool = GuildCloakTool.Fill);
        PickButton = ToolButton("Pick", 2, toolsTop, () => Model.Tool = GuildCloakTool.Pick);
        MirrorButton = ToolButton("Mirror", 0, secondToolRow, () => Model.Mirror = !Model.Mirror);
        UndoButton = ToolButton("Undo", 1, secondToolRow, () => StepHistory(Model.Undo));
        RedoButton = ToolButton("Redo", 2, secondToolRow, () => StepHistory(Model.Redo));

        Preview = new GuildEmblemPreviewStrip
        {
            X = LEFT + ((CONTENT_WIDTH - GuildEmblemPreviewStrip.TOTAL_WIDTH) / 2),
            Y = previewTop
        };

        AddChild(Preview);

        StatusLabel = Caption(string.Empty, LEFT, statusTop, CONTENT_WIDTH);
        StatusLabel.WordWrap = true;
        StatusLabel.Height = TextRenderer.CHAR_HEIGHT * STATUS_LINES;
        StatusLabel.VerticalAlignment = VerticalAlignment.Top;
        StatusLabel.PaddingLeft = 0;
        StatusLabel.PaddingRight = 0;
        StatusLabel.PaddingTop = 0;
        StatusLabel.PaddingBottom = 0;

        SubmitButton = AddButton(
            "Submit",
            SUBMIT_WIDTH,
            Width - LEFT - SUBMIT_WIDTH,
            buttonsTop,
            () => Send(GuildCloakEditorAction.Submit));

        SaveButton = AddButton(
            "Save draft",
            SAVE_WIDTH,
            SubmitButton.X - GAP - SAVE_WIDTH,
            buttonsTop,
            () => Send(GuildCloakEditorAction.SaveDraft));

        //added last so it draws over the canvas while open, and is hit-tested before it
        Picker = new StageColorPicker
        {
            Y = CONTENT_TOP
        };

        Picker.X = LEFT + ((CONTENT_WIDTH - Picker.Width) / 2);

        Picker.ColorChanged += color =>
        {
            if (!ColorDragging)
            {
                ColorDragging = true;
                Model.BeginStroke();
            }

            Model.SetColor(EditingColor, new GuildCloakColor(color.R, color.G, color.B));
        };

        Picker.DragEnded += EndColorDrag;
        Picker.Closed += EndColorDrag;
        AddChild(Picker);
    }

    /// <summary>Raised with the action and a copy of the emblem when the leader presses Save draft or Submit.</summary>
    public event Action<GuildCloakEditorAction, GuildEmblemDesign>? SaveRequested;

    /// <summary>
    ///     A status from the server. Local edits count as saved only when this client sent an emblem and the emblem is still
    ///     exactly that copy. The sent copy is used up here, so one reply cannot confirm a later emblem.
    /// </summary>
    public void ApplyStatus(GuildEmblemEditorArgs args)
    {
        if (SentDesign is not null && SentDesign.ContentEquals(Model.Design))
            Model.MarkSaved();

        SentDesign = null;
        SetStatus(args.Status, args.RejectionReason);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        //hiding resets the children's interaction state, which ends any stroke or color drag in progress
        base.Hide();
        Picker.Visible = false;
    }

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

    /// <summary>Loads the server's emblem and status and shows the window. An open window with unsaved painting keeps it.</summary>
    public void Open(GuildEmblemEditorArgs args)
    {
        TitleLabel.Text = $"Guild Emblem - {args.GuildName}";

        //asking Quill again while painting keeps the unsaved work; only the status line follows the server
        if (!Model.LoadSaved(args.Design, Visible))
        {
            SetStatus(args.Status, args.RejectionReason);

            return;
        }

        CloseArmed = false;
        ColorDragging = false;
        SentDesign = null;
        Picker.Visible = false;
        SetStatus(args.Status, args.RejectionReason);
        Show();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        if (Model.Version != ShownVersion)
        {
            ShownVersion = Model.Version;
            CloseArmed = false;
            Canvas.SetDesign(Model.Design);
            PreviewId = GuildEmblemTextures.SetLocal(PreviewId, Model.Design.DeepCopy());
            Preview.DesignId = PreviewId;

            //the picker always edits the selected color: follow a new selection (a box click, the pick tool, an undo)
            if (Picker.Visible && !ColorDragging && (EditingColor != Model.SelectedColor))
                OpenPicker(Model.SelectedColor);

            RefreshControls();
        }

        var canSend = (Environment.TickCount64 - LastSendMs) >= SEND_COOLDOWN_MS;
        SaveButton.Enabled = canSend;
        SubmitButton.Enabled = canSend && Model.Design.HasPaint();
    }

    private void EndColorDrag()
    {
        if (!ColorDragging)
            return;

        ColorDragging = false;
        Model.EndStroke();
    }

    private void OpenPicker(int number)
    {
        //see-through has no shade to pick
        if ((number < 1) || (number > Model.Design.Colors.Count))
        {
            Picker.Visible = false;

            return;
        }

        EditingColor = number;
        var color = Model.Design.Colors[number - 1];
        Picker.Open(new Color(color.R, color.G, color.B));
    }

    private void RefreshControls()
    {
        PencilButton.Selected = Model.Tool == GuildCloakTool.Pencil;
        FillButton.Selected = Model.Tool == GuildCloakTool.Fill;
        PickButton.Selected = Model.Tool == GuildCloakTool.Pick;
        MirrorButton.Selected = Model.Mirror;
        UndoButton.Enabled = Model.CanUndo;
        RedoButton.Enabled = Model.CanRedo;
        StatusLabel.Text = CloseArmed ? CLOSE_WARNING : GuildCloakEditorModel.StatusText(Status, StatusReason, Model.IsDirty);
    }

    private void RequestClose()
    {
        if (Model.IsDirty && !CloseArmed)
        {
            CloseArmed = true;
            StatusLabel.Text = CLOSE_WARNING;

            return;
        }

        Hide();
    }

    private void Send(GuildCloakEditorAction action)
    {
        var now = Environment.TickCount64;

        if ((now - LastSendMs) < SEND_COOLDOWN_MS)
            return;

        LastSendMs = now;
        CloseArmed = false;
        SentDesign = Model.Design.DeepCopy();
        SaveRequested?.Invoke(action, Model.Design.DeepCopy());
        RefreshControls();
    }

    private void SetStatus(GuildCloakStatus status, string? reason)
    {
        Status = status;
        StatusReason = reason ?? string.Empty;
        RefreshControls();
    }

    /// <summary>Undo or redo, then point an open picker at the color it now shows.</summary>
    private void StepHistory(Action step)
    {
        step();

        if (Picker.Visible && !ColorDragging)
            OpenPicker(Model.SelectedColor);
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

- [ ] **Step 9: Wire the editor** — in `Screens/WorldScreen.GuildEmblem.cs`:
  - add usings `using Chaos.Client.Controls.World.Popups.GuildEmblem;`, `using Chaos.DarkAges.Definitions;`, `using Chaos.Networking.Entities.Client;`;
  - change the class summary to "Guild emblems: the leader's editor window, asking for the emblems the world list and the emblem book name, and storing them as they arrive.";
  - add the field and members:

```csharp
    //built on first use
    private GuildEmblemEditorControl? GuildEmblemEditor;

    private void HandleGuildEmblemEditor(GuildEmblemEditorArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (GuildEmblemEditor is null)
        {
            GuildEmblemEditor = new GuildEmblemEditorControl
            {
                ZIndex = 2
            };

            GuildEmblemEditor.SaveRequested += SendGuildEmblemEdit;
            Root.AddChild(GuildEmblemEditor);
        }

        if (args.Type == GuildCloakEditorType.Open)
            GuildEmblemEditor.Open(args);
        else
            GuildEmblemEditor.ApplyStatus(args);
    }

    private void SendGuildEmblemEdit(GuildCloakEditorAction action, GuildEmblemDesign design)
        => Game.Connection.SendGuildEmblemEditorInteraction(
            new GuildEmblemEditorInteractionArgs
            {
                Action = action,
                Design = design
            });
```

  - in `WireGuildEmblem` add `Game.Connection.OnGuildEmblemEditor += HandleGuildEmblemEditor;`; in `UnwireGuildEmblem` add `Game.Connection.OnGuildEmblemEditor -= HandleGuildEmblemEditor;` and

```csharp
        if (GuildEmblemEditor is not null)
            GuildEmblemEditor.SaveRequested -= SendGuildEmblemEdit;
```

- [ ] **Step 10: Build and run all client tests**

Run: `cd $CLI && UnoraServerPath=$SRV dotnet build Chaos.Client/Chaos.Client.csproj`
Expected: build succeeds. Fix any missing `using` by copying it from the matching cloak file.
Run: `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: all pass.

```json:metadata
{"files": ["Chaos.Client/ViewModel/GuildEmblemEditorModel.cs", "Chaos.Client/Controls/World/Popups/GuildEmblem/GuildEmblemCanvas.cs", "Chaos.Client/Controls/World/Popups/GuildEmblem/GuildEmblemSwatches.cs", "Chaos.Client/Controls/World/Popups/GuildEmblem/GuildEmblemPreviewStrip.cs", "Chaos.Client/Controls/World/Popups/GuildEmblem/GuildEmblemEditorControl.cs", "Chaos.Client/Screens/WorldScreen.GuildEmblem.cs", "Tests/Chaos.Client.Tests/GuildEmblemEditorModelTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client && UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/GuildEmblemEditorModelTests/*\"", "acceptanceCriteria": ["pencil paints; mirror paints 10-x", "color 0 erases; pick selects incl. 0", "fill 4-connected same color only", "stroke undoes as one step; redo restores", "LoadSaved keeps unsaved painting while open", "window opens from GuildEmblemEditor Open with title, sends save/submit, Submit disabled while unpainted", "client builds"], "modelTier": "standard"}
```

---

### Task 10: Client — emblems in the shared review window

**Goal:** The admin review window lists cloaks and emblems together, labeled by kind, and shows the enlarged emblem with its previews when an emblem is selected.

**Files:**
- Create: `$CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildDesignReviewText.cs`
- Modify: `$CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakDialogBase.cs` (`AddStepButtons` returns its buttons)
- Modify: `$CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakReviewControl.cs`
- Test: `$CLI/Tests/Chaos.Client.Tests/GuildDesignReviewTextTests.cs`

**Acceptance Criteria:**
- [ ] List entries read "Cloak <guild>" or "Emblem <guild>"
- [ ] An emblem entry hides every cloak view (canvases, walking preview, its turn/body/step buttons, the BACK and PREVIEW captions) and shows the emblem canvas at 12× and the preview strip; the FRONT caption reads "EMBLEM"
- [ ] A cloak entry shows the cloak views as before
- [ ] The title is "Guild Design Review"
- [ ] The client builds; all client tests pass

**Verify:** `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing test** — create `Tests/Chaos.Client.Tests/GuildDesignReviewTextTests.cs`:

```csharp
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildDesignReviewTextTests
{
    [Test]
    public void Entries_are_labeled_by_kind_and_only_emblems_show_the_emblem_view()
    {
        var cloak = new GuildCloakReviewEntry { Kind = GuildDesignKind.Cloak, GuildName = "Moonveil" };
        var emblem = new GuildCloakReviewEntry { Kind = GuildDesignKind.Emblem, GuildName = "Richards" };

        GuildDesignReviewText.EntryCaption(cloak).Should().Be("Cloak Moonveil");
        GuildDesignReviewText.EntryCaption(emblem).Should().Be("Emblem Richards");
        GuildDesignReviewText.ShowsEmblem(emblem).Should().BeTrue();
        GuildDesignReviewText.ShowsEmblem(cloak).Should().BeFalse();
        GuildDesignReviewText.ShowsEmblem(null).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/GuildDesignReviewTextTests/*"`
Expected: build error, `GuildDesignReviewText` does not exist.

- [ ] **Step 3: Create `Controls/World/Popups/GuildCloak/GuildDesignReviewText.cs`**

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>The review window's per-entry choices, kept apart from the window so they can be tested.</summary>
public static class GuildDesignReviewText
{
    /// <summary>The list button's caption: the kind, then the guild.</summary>
    public static string EntryCaption(GuildCloakReviewEntry entry)
        => $"{(entry.Kind == GuildDesignKind.Emblem ? "Emblem" : "Cloak")} {entry.GuildName}";

    /// <summary>True when the entry is an emblem, so the window shows the emblem view instead of the cloak views.</summary>
    public static bool ShowsEmblem(GuildCloakReviewEntry? entry) => entry is { Kind: GuildDesignKind.Emblem };
}
```

- [ ] **Step 4: `AddStepButtons` returns its buttons** — in `GuildCloakDialogBase.cs`, change the signature to `protected IReadOnlyList<CustomButton> AddStepButtons(GuildCloakPreview preview, int x, int y, int width)`, keep the `Prev`/`Next` buttons in locals `prev` and `next`, and end with `return [prev, play, next];`. Update its summary to add "Returns the three buttons." The editor ignores the result; no change there.

- [ ] **Step 5: `GuildCloakReviewControl`**
  1. Add usings `using Chaos.Client.Controls.World.Emblems;` and `using Chaos.Client.Controls.World.Popups.GuildEmblem;`.
  2. Change `LIST_WIDTH` from 120 to 140 (the captions now start with "Emblem ").
  3. Add fields:

```csharp
    private readonly UILabel BackCaption;
    private readonly UIElement[] CloakViews;
    private readonly GuildEmblemCanvas EmblemCanvas;
    private readonly GuildEmblemPreviewStrip EmblemPreview;
    private readonly UILabel FrontCaption;
    private readonly UILabel PreviewCaption;
    private int EmblemPreviewId;
```

  4. In the constructor, replace the four caption lines with:

```csharp
        Caption("Guild Design Review", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("WAITING", LEFT, CAPTION_TOP, LIST_WIDTH, color: LegendColors.Gray);
        FrontCaption = Caption("FRONT", FrontCanvas.X, CAPTION_TOP, FrontCanvas.Width, color: LegendColors.Gray);
        BackCaption = Caption("BACK", BackCanvas.X, CAPTION_TOP, BackCanvas.Width, color: LegendColors.Gray);
        PreviewCaption = Caption("PREVIEW", previewLeft, CAPTION_TOP, PREVIEW_WIDTH, color: LegendColors.Gray);
```

  5. Replace the four preview-button lines (`<`, `>`, `Body`, `AddStepButtons`) with:

```csharp
        var turnLeft = AddButton("<", SMALL_BUTTON, previewLeft, turnTop, () => Preview.Turn(-1));
        var turnRight = AddButton(">", SMALL_BUTTON, previewLeft + SMALL_BUTTON + 4, turnTop, () => Preview.Turn(1));
        var body = AddButton("Body", 50, previewLeft + PREVIEW_WIDTH - 50, turnTop, Preview.ToggleBody);

        CloakViews = [FrontCanvas, BackCanvas, Preview, turnLeft, turnRight, body, .. AddStepButtons(Preview, previewLeft, stepTop, PREVIEW_WIDTH)];

        EmblemCanvas = new GuildEmblemCanvas(12)
        {
            X = FrontCanvas.X,
            Y = CONTENT_TOP,
            ReadOnly = true,
            Visible = false
        };

        EmblemPreview = new GuildEmblemPreviewStrip
        {
            X = FrontCanvas.X,
            Y = CONTENT_TOP + EmblemCanvas.Height + 8,
            Visible = false
        };

        AddChild(EmblemCanvas);
        AddChild(EmblemPreview);
```

  6. Change `EmptyLabel`'s text to `"Nothing is waiting."`.
  7. In `RefreshList`, replace `button.Caption = Entries[index].GuildName;` with `button.Caption = GuildDesignReviewText.EntryCaption(Entries[index]);`.
  8. In `Select`, replace the block from `var entry = Selected;` to the end of the `if (entry is not null) { … }` block with:

```csharp
        var entry = Selected;
        var showsEmblem = GuildDesignReviewText.ShowsEmblem(entry);

        foreach (var view in CloakViews)
            view.Visible = entry is not null && !showsEmblem;

        EmblemCanvas.Visible = showsEmblem;
        EmblemPreview.Visible = showsEmblem;
        FrontCaption.Text = showsEmblem ? "EMBLEM" : "FRONT";
        BackCaption.Visible = !showsEmblem;
        PreviewCaption.Visible = !showsEmblem;
        EmptyLabel.Visible = entry is null;
        InfoLabel.Text = entry is null ? string.Empty : $"{entry.GuildName}, by {entry.LeaderName}, {entry.SubmittedAtUtc:yyyy-MM-dd HH:mm} UTC";

        if (entry is null) { }
        else if (showsEmblem)
        {
            EmblemCanvas.SetDesign(entry.EmblemDesign);
            EmblemPreviewId = GuildEmblemTextures.SetLocal(EmblemPreviewId, entry.EmblemDesign.DeepCopy());
            EmblemPreview.DesignId = EmblemPreviewId;
        } else
        {
            FrontCanvas.SetDesign(entry.Design);
            BackCanvas.SetDesign(entry.Design);
            PreviewDesignId = Renderer.GuildCloaks.SetLocal(PreviewDesignId, entry.Design.DeepCopy());
            Preview.SetDesignId(PreviewDesignId);
        }
```

Write the empty `if` as a positive check if the project's analyzers reject empty blocks: `if (entry is not null && showsEmblem) { … } else if (entry is not null) { … }`.

  9. Update the class summary: "The admins' review window for guild designs: waiting cloaks and emblems in one list, labeled by kind. A cloak shows its front, back and a walking preview; an emblem shows enlarged with its world list and Emblem tab sizes. Approve / Reject with a reason. …" (keep the opening sentence about where it is opened from).

- [ ] **Step 6: Build and run all client tests**

Run: `cd $CLI && UnoraServerPath=$SRV dotnet build Chaos.Client/Chaos.Client.csproj`
Expected: build succeeds.
Run: `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: all pass.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/GuildCloak/GuildDesignReviewText.cs", "Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakDialogBase.cs", "Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakReviewControl.cs", "Tests/Chaos.Client.Tests/GuildDesignReviewTextTests.cs"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client && UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["entries read Cloak <guild> / Emblem <guild>", "emblem entry hides all cloak views and shows 12x canvas + preview strip, caption EMBLEM", "cloak entry unchanged", "title Guild Design Review", "client builds; all client tests pass"], "modelTier": "standard"}
```

---

### Task 11: Full test runs, docs, and one commit per repo

**Goal:** Everything passes together; the client CLAUDE.md names the new files; each worktree has one commit on `feat/guild-emblem`. Nothing is merged or pushed.

**Files:**
- Modify: `$CLI/CLAUDE.md`
- Commit: all changes in `$SRV`, `$CLI` (plus its `Chaos-Server` pointer), `$UNO`

**Acceptance Criteria:**
- [ ] Full server `Chaos.Tests` run: only the known master failures fail (`OnItemDroppedOn` stackable, possibly `GiveAbility`)
- [ ] Full client test run passes
- [ ] Client CLAUDE.md lists the guild emblem editor, `GuildEmblemTextures`, and `WorldScreen.GuildEmblem.cs`
- [ ] One commit per worktree with the attribution trailers; the client commit's `Chaos-Server` pointer is the server worktree's commit
- [ ] `git status` is clean in all three worktrees except known generated files (Unora `Custom Client Mods/**/obj`)

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client log -1 --format=%s` → the commit subject below

**Steps:**

- [ ] **Step 1: Full server tests**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi
```

Expected: only the known failures. Any other failure is a bug in this work: fix it before committing.

- [ ] **Step 2: Full client tests**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client
UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi
```

Expected: all pass.

- [ ] **Step 3: Client CLAUDE.md** — add to the `Popups/` list: "`GuildEmblem/` (GuildEmblemEditorControl — the guild leader's compact emblem editor, opened from Quill; GuildEmblemCanvas, GuildEmblemSwatches, GuildEmblemPreviewStrip; the editor state is `ViewModel/GuildEmblemEditorModel`)"; add `WorldScreen.GuildEmblem.cs` — "Guild emblem editor and emblem download handlers" to the WorldScreen partial list; add under Rendering/emblems: "`Controls/World/Emblems/GuildEmblemTextures` — guild emblems by id (server ids positive, local drafts negative) and their 11 × 11 textures; `EmblemIcon.GuildEmblemId` draws from it. Cleared on logout." Mention in the `GuildCloak/` entry that `GuildCloakReviewControl` reviews emblems too.

- [ ] **Step 4: Commit the server**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-server
git status --short
git add Chaos.DarkAges Chaos.Networking Chaos.Networking.Abstractions Chaos Tests/Chaos.Tests
git status --short
git commit -F - <<'EOF'
Add guild emblems: painted 11x11 world list emblems for guilds

Guilds buy a guild emblem from Tibbs (5,000,000). The leader paints it
in a new editor; admins approve it in the shared guild design review.
Members own the reserved "guild" emblem while their guild has an
approved one, and it is offered once per guild. Emblem designs share
the cloak storage and id counter. CLIENT_VERSION 760.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_013X729KRHVUw62SaYunhT8e
EOF
git log -1 --format=%H
```

Before `git add`, read `git status --short`: stage only files this plan created or changed. Never commit `appsettings.json` with a local `StagingDirectory` or any `launchSettings.json`.

- [ ] **Step 5: Commit the client** (replace `<server-sha>` with the hash printed in Step 4)

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client
git update-index --cacheinfo 160000,<server-sha>,Chaos-Server
git add CLAUDE.md Chaos.Client Chaos.Client.Networking Tests/Chaos.Client.Tests
git status --short
git commit -F - <<'EOF'
Add guild emblems: editor, review view and world list drawing

The compact 11x11 guild emblem editor opened from Quill, the emblem
view in the guild design review window, and guild emblems drawn by id
in the world list and the Emblem tab. Server pointer: feat/guild-emblem.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_013X729KRHVUw62SaYunhT8e
EOF
```

Check `git status --short` again before committing: never stage `Chaos.Client/Properties/launchSettings.json` or `.run/` files.

- [ ] **Step 6: Commit Unora**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-unora
git add "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_emblem.json" "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_emblem_confirm.json" "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildemblem_design.json" Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear.json Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear_confirm.json Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_review.json Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_initial.json docs/emblems.md
git commit -F - <<'EOF'
Add guild emblem dialogs: Tibbs purchase, Quill design, admin clear

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_013X729KRHVUw62SaYunhT8e
EOF
```

- [ ] **Step 7: Report** — tell the user the three branch heads, that nothing is merged or pushed, and the spec's in-game hand check (10 steps in the spec's Testing section). Do not merge, push or deploy: those are the user's steps.

```json:metadata
{"files": ["CLAUDE.md"], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-emblem-client log -1 --format=%s", "acceptanceCriteria": ["full server tests: only known master failures", "full client tests pass", "client CLAUDE.md names the new files", "one commit per worktree with trailers; client Chaos-Server pointer = server commit", "worktrees clean except known generated files"], "modelTier": "mechanical"}
```
