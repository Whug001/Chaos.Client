# In-Game Bug Reports Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players report a bug from F1 → Terminus → "Report a bug" in a wooden report window. The live server saves each report as a category folder holding the text, a clean screenshot, the character, the world state, the client details and the server log lines about that character.

**Architecture:** The shared messages live in the `Chaos-Server` submodule, and both the client and the server build from them. The server keeps open reports in a pure in-memory ledger. It copies the character when the window opens, and writes the final folder off the game loop. The client closes the Terminus dialog, captures the next frame into memory, shows the A2 window, and sends the report plus the picture in 32 KB parts. A Python command imports copied report folders next to the Discord feedback archive.

**Tech Stack:** C# 14 / .NET 10, MonoGame, SkiaSharp 3.116, TUnit + FluentAssertions + Moq, Python 3 (standard library only).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-09-23-in-game-bug-reports-design.md`

## Global Constraints

- **Message codes:** `ServerOpCode.BugReportOpen = 127`, `ClientOpCode.BugReportInteraction = 124`. Interaction actions: `Submit = 0`, `PicturePart = 1`, `Cancel = 2`.
- **Categories:** enum values, folder names and captions are exactly:

  | Value | Folder | Caption |
  |---|---|---|
  | `SkillSpell = 0` | `skill-spell` | Skill/Spell |
  | `Item = 1` | `item` | Item |
  | `MapWarp = 2` | `map-warp` | Map or Warp |
  | `Npc = 3` | `npc` | NPC |
  | `Quest = 4` | `quest` | Quest |
  | `MonsterCombat = 5` | `monster-combat` | Monster/Combat |
  | `ClientUi = 6` | `client-ui` | Client/UI |
  | `Other = 7` | `other` | Other |

- **Size limits:**
  - A picture part is at most 32,768 bytes, and a picture at most 524,288 bytes.
  - A description is 10 to 1,000 characters after trimming.
  - Client build and Windows version strings are at most 64 characters.
- **Timing:**
  - A character waits 2 minutes between reports. The wait starts at submit.
  - A report number lives 30 minutes.
  - The picture gap is 30 seconds, and the sweep runs every 5 seconds.
  - The log window is 15 minutes, and the log cap is 2,000 lines.
- **Client version:** `CONSTANTS.CLIENT_VERSION = 752`.
- **Report folder:** `<BugReportOptions.Directory>/<category>/<yyyy-MM-dd_HHmmss>_<lowercase name>/`.
  - Contents: `report.md`, `screenshot.png` (optional), `world.json`, `client.json`, `server-log.jsonl`, and `character/` (the nine save files).
  - Holding folder: `_pending/<report number>/`.
- **`report.md` format:**
  - The header fields appear in exactly the order of the spec's example.
  - Status values are `open`, `fixed`, `wontfix`, `wanted` and `duplicate`.
  - The file ends with `## Triage`, a blank line, then `<!-- sync:preserve -->`.
- **Picture privacy:** the picture is never written to disk on the player's computer.
- **Discord index:** the output of `archive.render_index` for Discord entries must stay byte-for-byte the same.
- **Local edits:** do not commit machine-specific edits. `Chaos-Server/Chaos/appsettings.json` (`StagingDirectory`) and `Chaos.Client/Chaos.Client/Properties/launchSettings.json` already have uncommitted local changes.
- **Commit strategy (at-end):** implementers do not commit. Task 11 makes one commit per repo.
- **Tools:** read and edit code with Serena's tools, as the user's global CLAUDE.md requires. Server tests are TUnit executables: run them with `dotnet run`, not `dotnet test`.

**User decisions (already made):**
- The only way in is F1 → Terminus → "Report a bug". There is no hotkey, options button or chat command.
- The report window uses the wooden `FramedDialogPanelBase` frame, layout A2, with the screenshot preview beside the text box.
- There are eight categories: the old five plus Quest, Monster/Combat and Client display/UI.
- The screenshot is included by default. The player can untick it, and sees a small preview.
- Extra data is live world state, client details and recent server log lines. There is no chat history.
- Delivery is files on the live server, which staff copy off by hand. There is no Discord post.
- Reports are imported next to the Discord archive, with the same status and triage format.
- The spec is approved. During planning, the hint text was shortened to fit the box, and the panel height became 310.

**Repo paths used below** (all under `C:\Users\Michael\Documents\GitHub`):
- `Chaos.Client/` is the client repo.
- `Chaos.Client/Chaos-Server/` is the server submodule. Server commands run from this folder.
- `Unora/` is the data and tools repo.

---

### Task 1: Shared bug report protocol

**Goal:** Add the message codes, enums, shared limits and names, message classes and converters, and raise the client version to 752.

**Files:**
- Modify: `Chaos.Client/Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs` (`ClientOpCode` after `Vote = 122,`; `ServerOpCode` after `Poll = 124,`)
- Modify: `Chaos.Client/Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs` (after the `BeautyShopInteractionType` enum)
- Create: `Chaos.Client/Chaos-Server/Chaos.DarkAges/Definitions/BugReportProtocol.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos.Networking/Entities/Server/BugReportOpenArgs.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos.Networking/Converters/Server/BugReportOpenConverter.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos.Networking/Entities/Client/BugReportInteractionArgs.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos.Networking/Converters/Client/BugReportInteractionConverter.cs`
- Modify: `Chaos.Client/Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs` (`CLIENT_VERSION` 751 → 752)
- Test: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/Networking/BugReportPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `BugReportOpen` and all three `BugReportInteraction` actions survive a serialize and deserialize unchanged
- [ ] An unknown interaction type throws `ArgumentOutOfRangeException` when deserialized
- [ ] `BugReportProtocol.FolderName` and `DisplayName` return the Global Constraints table values for all eight categories
- [ ] `BugReportProtocol.PartCount` returns 0, 1, 1, 2 and 16 for 0, 1, 32768, 32769 and 524288 bytes
- [ ] `CONSTANTS.CLIENT_VERSION` is 752, and both `Chaos.slnx` and `Chaos.Client.slnx` build

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportPacketConverterTests/*"` (from `Chaos-Server/`) → all tests pass

**Steps:**

- [ ] **Step 1: Write the failing test**

Create `Tests/Chaos.Tests/Networking/BugReportPacketConverterTests.cs`:

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

public class BugReportPacketConverterTests
{
    private static readonly Encoding Enc = Encoding.GetEncoding(949);

    private static T RoundTrip<T>(PacketConverterBase<T> converter, T original) where T: class, IPacketSerializable
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        converter.Serialize(ref writer, original);
        var bytes = writer.ToSpan().ToArray();
        var reader = new SpanReader(Enc, bytes);

        return converter.Deserialize(ref reader);
    }

    [Test]
    public async Task Open_round_trips()
    {
        var original = new BugReportOpenArgs { ReportId = 3_000_000_123 };

        RoundTrip(new BugReportOpenConverter(), original).Should().BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Submit_round_trips()
    {
        var original = new BugReportInteractionArgs
        {
            Type = BugReportInteractionType.Submit,
            ReportId = 42,
            Category = BugReportCategory.MapWarp,
            Description = "Walked through the east door\nand landed inside the wall.",
            PictureLength = 40_000,
            ClientBuild = "0.1.0+c0a7eb7",
            OsDescription = "Microsoft Windows 10.0.26200",
            FramesPerSecond = 60,
            PingMs = 45,
            HudStyle = 1,
            WindowWidth = 1280,
            WindowHeight = 960
        };

        RoundTrip(new BugReportInteractionConverter(), original).Should().BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Picture_part_round_trips()
    {
        var original = new BugReportInteractionArgs
        {
            Type = BugReportInteractionType.PicturePart,
            ReportId = 42,
            PartIndex = 3,
            Data = Enumerable.Range(0, BugReportProtocol.PART_SIZE).Select(i => (byte)i).ToArray()
        };

        RoundTrip(new BugReportInteractionConverter(), original).Should().BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Cancel_round_trips()
    {
        var original = new BugReportInteractionArgs { Type = BugReportInteractionType.Cancel, ReportId = 7 };

        RoundTrip(new BugReportInteractionConverter(), original).Should().BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unknown_type_throws()
    {
        var bytes = new byte[] { 9, 0, 0, 0, 1 };

        var act = () =>
        {
            var reader = new SpanReader(Enc, bytes);

            return new BugReportInteractionConverter().Deserialize(ref reader);
        };

        act.Should().Throw<ArgumentOutOfRangeException>();

        await Task.CompletedTask;
    }

    //formatter:off
    [Test]
    [Arguments(BugReportCategory.SkillSpell, "skill-spell", "Skill/Spell")]
    [Arguments(BugReportCategory.Item, "item", "Item")]
    [Arguments(BugReportCategory.MapWarp, "map-warp", "Map or Warp")]
    [Arguments(BugReportCategory.Npc, "npc", "NPC")]
    [Arguments(BugReportCategory.Quest, "quest", "Quest")]
    [Arguments(BugReportCategory.MonsterCombat, "monster-combat", "Monster/Combat")]
    [Arguments(BugReportCategory.ClientUi, "client-ui", "Client/UI")]
    [Arguments(BugReportCategory.Other, "other", "Other")]
    //formatter:on
    public async Task Category_names(BugReportCategory category, string folder, string caption)
    {
        BugReportProtocol.FolderName(category).Should().Be(folder);
        BugReportProtocol.DisplayName(category).Should().Be(caption);

        await Task.CompletedTask;
    }

    //formatter:off
    [Test]
    [Arguments(0u, 0)]
    [Arguments(1u, 1)]
    [Arguments(32_768u, 1)]
    [Arguments(32_769u, 2)]
    [Arguments(524_288u, 16)]
    //formatter:on
    public async Task Part_count(uint pictureLength, int expected)
    {
        BugReportProtocol.PartCount(pictureLength).Should().Be(expected);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run (from `Chaos-Server/`): `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportPacketConverterTests/*"`
Expected: the build fails because `BugReportOpenArgs`, `BugReportCategory` and the other new types do not exist yet.

- [ ] **Step 3: Add the message codes**

In `Chaos.Networking.Abstractions/Definitions/Enums.cs`, insert directly after `Vote = 122,` in `ClientOpCode`:

```csharp

    /// <summary>
    ///     OpCode used when a client submits, sends a picture part for, or cancels an in-game bug report. Carries the
    ///     report number from <see cref="ServerOpCode.BugReportOpen" />; the server checks it against the sender.
    ///     <br />
    ///     Hex value: 0x7C
    /// </summary>
    BugReportInteraction = 124,
```

Then insert directly after `Poll = 124,` in `ServerOpCode`:

```csharp

    /// <summary>
    ///     OpCode used to open the in-game bug report window. Carries the single-use report number that the client's
    ///     <see cref="ClientOpCode.BugReportInteraction" /> messages must carry.
    ///     <br />
    ///     Hex value: 0x7F
    /// </summary>
    BugReportOpen = 127,
```

Custom message codes use MD5 encryption by default through the shared `Crypto`, so no crypto change is needed.

- [ ] **Step 4: Add the shared enums**

In `Chaos.DarkAges/Definitions/Enums.cs`, insert directly after the `BeautyShopInteractionType` enum (use Serena `insert_after_symbol` on `BeautyShopInteractionType`):

```csharp

/// <summary>What a player's in-game bug report is about. Each value has its own report folder on the server.</summary>
public enum BugReportCategory : byte
{
    SkillSpell = 0,
    Item = 1,
    MapWarp = 2,
    Npc = 3,
    Quest = 4,
    MonsterCombat = 5,
    ClientUi = 6,
    Other = 7
}

public enum BugReportInteractionType : byte
{
    /// <summary>The filled-in report. Picture parts follow when its picture length is above zero.</summary>
    Submit = 0,

    /// <summary>One piece of the picture, at most <see cref="BugReportProtocol.PART_SIZE" /> bytes.</summary>
    PicturePart = 1,

    /// <summary>The player closed the window without sending.</summary>
    Cancel = 2
}
```

- [ ] **Step 5: Add the shared limits and names**

Create `Chaos.DarkAges/Definitions/BugReportProtocol.cs`:

```csharp
namespace Chaos.DarkAges.Definitions;

/// <summary>
///     Limits and names for in-game bug reports that the client and the server must agree on. The server's
///     <c>BugReportOptions</c> defaults come from here.
/// </summary>
public static class BugReportProtocol
{
    /// <summary>The largest picture part, in bytes. One game message holds at most 64 KB.</summary>
    public const int PART_SIZE = 32_768;

    /// <summary>The largest picture the server keeps, in bytes.</summary>
    public const int MAX_PICTURE_BYTES = 524_288;

    /// <summary>The shortest description the server accepts, after trimming.</summary>
    public const int MIN_DESCRIPTION_CHARS = 10;

    /// <summary>The longest description the server accepts, after trimming.</summary>
    public const int MAX_DESCRIPTION_CHARS = 1_000;

    /// <summary>The longest client build or Windows version string the client sends.</summary>
    public const int MAX_DETAIL_CHARS = 64;

    /// <summary>How many parts a picture of <paramref name="pictureLength" /> bytes is sent in.</summary>
    public static int PartCount(uint pictureLength) => (int)((pictureLength + (long)PART_SIZE - 1) / PART_SIZE);

    /// <summary>The report folder name for a category, such as <c>map-warp</c>.</summary>
    public static string FolderName(BugReportCategory category)
        => category switch
        {
            BugReportCategory.SkillSpell    => "skill-spell",
            BugReportCategory.Item          => "item",
            BugReportCategory.MapWarp       => "map-warp",
            BugReportCategory.Npc           => "npc",
            BugReportCategory.Quest         => "quest",
            BugReportCategory.MonsterCombat => "monster-combat",
            BugReportCategory.ClientUi      => "client-ui",
            BugReportCategory.Other         => "other",
            _                               => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };

    /// <summary>The button caption for a category in the report window.</summary>
    public static string DisplayName(BugReportCategory category)
        => category switch
        {
            BugReportCategory.SkillSpell    => "Skill/Spell",
            BugReportCategory.Item          => "Item",
            BugReportCategory.MapWarp       => "Map or Warp",
            BugReportCategory.Npc           => "NPC",
            BugReportCategory.Quest         => "Quest",
            BugReportCategory.MonsterCombat => "Monster/Combat",
            BugReportCategory.ClientUi      => "Client/UI",
            BugReportCategory.Other         => "Other",
            _                               => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };
}
```

- [ ] **Step 6: Add the message classes**

Create `Chaos.Networking/Entities/Server/BugReportOpenArgs.cs`:

```csharp
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.BugReportOpen" /> packet: open the bug report window
///     for this single-use report number.
/// </summary>
public sealed record BugReportOpenArgs : IPacketSerializable
{
    public uint ReportId { get; set; }
}
```

Create `Chaos.Networking/Entities/Client/BugReportInteractionArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>
///     Represents the serialization of the <see cref="ClientOpCode.BugReportInteraction" /> packet. Which fields are on
///     the wire depends on <see cref="Type" />: Submit carries the report and the client details, PicturePart carries one
///     piece of the picture, and Cancel carries only the report number.
/// </summary>
public sealed record BugReportInteractionArgs : IPacketSerializable
{
    public required BugReportInteractionType Type { get; set; }
    public uint ReportId { get; set; }

    //submit
    public BugReportCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public uint PictureLength { get; set; }
    public string ClientBuild { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public ushort FramesPerSecond { get; set; }
    public ushort PingMs { get; set; }

    /// <summary>0 = classic HUD, 1 = large HUD.</summary>
    public byte HudStyle { get; set; }

    public ushort WindowWidth { get; set; }
    public ushort WindowHeight { get; set; }

    //picture part
    public byte PartIndex { get; set; }
    public byte[] Data { get; set; } = [];
}
```

- [ ] **Step 7: Add the converters**

Create `Chaos.Networking/Converters/Server/BugReportOpenConverter.cs`:

```csharp
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="BugReportOpenArgs" /></summary>
public sealed class BugReportOpenConverter : PacketConverterBase<BugReportOpenArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.BugReportOpen;

    /// <inheritdoc />
    public override BugReportOpenArgs Deserialize(ref SpanReader reader) => new() { ReportId = reader.ReadUInt32() };

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, BugReportOpenArgs args) => writer.WriteUInt32(args.ReportId);
}
```

Create `Chaos.Networking/Converters/Client/BugReportInteractionConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="BugReportInteractionArgs" /></summary>
public sealed class BugReportInteractionConverter : PacketConverterBase<BugReportInteractionArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.BugReportInteraction;

    /// <inheritdoc />
    public override BugReportInteractionArgs Deserialize(ref SpanReader reader)
    {
        var type = (BugReportInteractionType)reader.ReadByte();

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown bug report interaction type");

        var args = new BugReportInteractionArgs
        {
            Type = type,
            ReportId = reader.ReadUInt32()
        };

        switch (type)
        {
            case BugReportInteractionType.Submit:
                args.Category = (BugReportCategory)reader.ReadByte();
                args.Description = reader.ReadString16();
                args.PictureLength = reader.ReadUInt32();
                args.ClientBuild = reader.ReadString8();
                args.OsDescription = reader.ReadString8();
                args.FramesPerSecond = reader.ReadUInt16();
                args.PingMs = reader.ReadUInt16();
                args.HudStyle = reader.ReadByte();
                args.WindowWidth = reader.ReadUInt16();
                args.WindowHeight = reader.ReadUInt16();

                break;
            case BugReportInteractionType.PicturePart:
                args.PartIndex = reader.ReadByte();
                args.Data = reader.ReadData16();

                break;
        }

        return args;
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, BugReportInteractionArgs args)
    {
        writer.WriteByte((byte)args.Type);
        writer.WriteUInt32(args.ReportId);

        switch (args.Type)
        {
            case BugReportInteractionType.Submit:
                writer.WriteByte((byte)args.Category);
                writer.WriteString16(args.Description);
                writer.WriteUInt32(args.PictureLength);
                writer.WriteString8(args.ClientBuild);
                writer.WriteString8(args.OsDescription);
                writer.WriteUInt16(args.FramesPerSecond);
                writer.WriteUInt16(args.PingMs);
                writer.WriteByte(args.HudStyle);
                writer.WriteUInt16(args.WindowWidth);
                writer.WriteUInt16(args.WindowHeight);

                break;
            case BugReportInteractionType.PicturePart:
                writer.WriteByte(args.PartIndex);
                writer.WriteData16(args.Data);

                break;
        }
    }
}
```

Converters are found by reflection (`PacketExtensions.LoadConvertersFromAssembly`), so there is nothing to register.

- [ ] **Step 8: Raise the client version**

In `Chaos.DarkAges/Definitions/CONSTANTS.cs`, change `public const ushort CLIENT_VERSION = 751;` to `public const ushort CLIENT_VERSION = 752;`.

- [ ] **Step 9: Run the tests and both builds**

Run (from `Chaos-Server/`): `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportPacketConverterTests/*"`
Expected: all pass.

Run (from `Chaos.Client/`): `dotnet build Chaos.Client.slnx`
Expected: `Build succeeded`. The client builds the same protocol projects.

```json:metadata
{"files": ["Chaos.Client/Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos.Client/Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs", "Chaos.Client/Chaos-Server/Chaos.DarkAges/Definitions/BugReportProtocol.cs", "Chaos.Client/Chaos-Server/Chaos.Networking/Entities/Server/BugReportOpenArgs.cs", "Chaos.Client/Chaos-Server/Chaos.Networking/Converters/Server/BugReportOpenConverter.cs", "Chaos.Client/Chaos-Server/Chaos.Networking/Entities/Client/BugReportInteractionArgs.cs", "Chaos.Client/Chaos-Server/Chaos.Networking/Converters/Client/BugReportInteractionConverter.cs", "Chaos.Client/Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Client/Chaos-Server/Tests/Chaos.Tests/Networking/BugReportPacketConverterTests.cs"], "verifyCommand": "cd Chaos.Client/Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/BugReportPacketConverterTests/*\"", "acceptanceCriteria": ["all four messages round-trip unchanged", "unknown interaction type throws ArgumentOutOfRangeException", "folder and display names match the table for all eight categories", "PartCount returns 0,1,1,2,16 for 0,1,32768,32769,524288", "CLIENT_VERSION is 752 and both solutions build"], "modelTier": "mechanical"}
```

---

### Task 2: Report content on the server

**Goal:** Build the world snapshot, the client details record, and the `report.md` renderer, as pure code with tests.

**Files:**
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/WorldSnapshot.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/ClientDetails.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportMarkdown.cs`
- Create: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportSamples.cs`
- Test: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/WorldSnapshotTests.cs`
- Test: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportMarkdownTests.cs`

**Acceptance Criteria:**
- [ ] `WorldSnapshot.Capture` records the map name, template key, instance id, size, position, HP/MP, alive state and class of a `MockAisling`
- [ ] `report.md` starts with the exact header from the spec example and ends with `## Triage\n\n<!-- sync:preserve -->\n`
- [ ] A hostile description cannot add a second `status:` line to the header, and sits inside a fence longer than its longest backtick run
- [ ] Control characters other than newline and tab are removed, and `\r\n` becomes `\n`
- [ ] The title is the first 60 characters of the description on one line
- [ ] The details lines show class, level, vitals, dead or alive, group or "none", client details, files and notes

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportMarkdownTests/*"` and the same with `WorldSnapshotTests` (from `Chaos-Server/`) → all pass

**Steps:**

- [ ] **Step 1: Write the shared test samples**

Create `Tests/Chaos.Tests/BugReports/BugReportSamples.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.BugReports;

namespace Chaos.Tests.BugReports;

/// <summary>Ready-made report content for the bug report tests.</summary>
public static class BugReportSamples
{
    public static WorldSnapshot World(
        bool alive = true,
        IReadOnlyList<WorldSnapshot.GroupMemberInfo>? group = null,
        string mapName = "Mileth Inn")
        => new()
        {
            ServerTimeUtc = new DateTime(2026, 9, 23, 23, 43, 1, DateTimeKind.Utc),
            ServerTimeLocal = new DateTime(2026, 9, 23, 19, 43, 1, DateTimeKind.Local),
            ServerVersion = "1.0.0",
            CharacterName = "Bob",
            ClassName = "Warrior",
            Level = 41,
            Map = new WorldSnapshot.MapInfo(mapName, "mileth_inn", "mileth_inn", 20, 15),
            Position = new WorldSnapshot.PositionInfo(12, 7, "Down"),
            Vitals = new WorldSnapshot.VitalsInfo(812, 900, 120, 300, alive),
            Group = group ?? [],
            Nearby = new WorldSnapshot.NearbyInfo([], [], [], [])
        };

    public static ClientDetails Client() => new("0.1.0+c0a7eb7", "Windows 11", 60, 45, "large", 1280, 960);

    public static BugReportMarkdown.Input Input(
        string description = "Walked through the east door and landed inside the wall.",
        WorldSnapshot? world = null,
        bool screenshot = true,
        IReadOnlyList<string>? notes = null)
        => new()
        {
            Id = "2026-09-23_194301_bob",
            Category = BugReportCategory.MapWarp,
            Description = description,
            World = world ?? World(),
            ReportedAt = new DateTimeOffset(2026, 9, 23, 19, 43, 1, TimeSpan.FromHours(-4)),
            Client = Client(),
            HasScreenshot = screenshot,
            LogLineCount = 214,
            Notes = notes ?? []
        };
}
```

- [ ] **Step 2: Write the failing tests**

Create `Tests/Chaos.Tests/BugReports/BugReportMarkdownTests.cs`:

```csharp
using Chaos.Services.BugReports;
using FluentAssertions;

namespace Chaos.Tests.BugReports;

public class BugReportMarkdownTests
{
    [Test]
    public async Task Header_matches_the_archive_format()
    {
        var text = BugReportMarkdown.Render(BugReportSamples.Input());

        text.Should()
            .StartWith(
                "---\n"
                + "id: \"2026-09-23_194301_bob\"\n"
                + "kind: in-game\n"
                + "category: map-warp\n"
                + "title: \"Walked through the east door and landed inside the wall.\"\n"
                + "author: \"Bob\"\n"
                + "created: 2026-09-23\n"
                + "reported_at: 2026-09-23T19:43:01-04:00\n"
                + "map: \"Mileth Inn\"\n"
                + "map_key: \"mileth_inn\"\n"
                + "x: 12\n"
                + "y: 7\n"
                + "client_build: \"0.1.0+c0a7eb7\"\n"
                + "screenshot: true\n"
                + "status: open\n"
                + "status_note: \"\"\n"
                + "---\n\n"
                + "## Report\n\n"
                + "```text\nWalked through the east door and landed inside the wall.\n```\n\n"
                + "## Details\n\n");

        text.Should().EndWith("## Triage\n\n<!-- sync:preserve -->\n");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Details_lines_show_vitals_group_client_files_and_notes()
    {
        var world = BugReportSamples.World(
            alive: false,
            group: [new("Aroha", 38, "Priest", "mileth_inn", 3, 4), new("Kinley", 40, "Wizard", "mileth_inn", 5, 6)]);

        var text = BugReportMarkdown.Render(
            BugReportSamples.Input(world: world, screenshot: false, notes: ["picture not saved (parts missing)"]));

        text.Should().Contain("- Warrior, level 41. HP 812/900, MP 120/300. Dead.\n");
        text.Should().Contain("- Group: Aroha (Priest 38), Kinley (Wizard 40)\n");
        text.Should().Contain("- Client: 0.1.0+c0a7eb7, Windows 11, 60 fps, 45 ms, large HUD, 1280×960\n");
        text.Should().Contain("- Files: world.json, client.json, server-log.jsonl (214 lines), character/\n");
        text.Should().Contain("- Note: picture not saved (parts missing)\n");
        text.Should().Contain("screenshot: false\n");

        await Task.CompletedTask;
    }

    [Test]
    public async Task No_group_reads_none_and_a_screenshot_is_listed_first()
    {
        var text = BugReportMarkdown.Render(BugReportSamples.Input());

        text.Should().Contain("- Group: none\n");
        text.Should().Contain("- Files: screenshot.png, world.json, client.json, server-log.jsonl (214 lines), character/\n");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Backticks_in_the_description_get_a_longer_fence()
    {
        var text = BugReportMarkdown.Render(BugReportSamples.Input("Typed ```` into chat and it crashed"));

        text.Should().Contain("`````text\nTyped ```` into chat and it crashed\n`````\n");

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_hostile_description_cannot_change_the_header()
    {
        const string HOSTILE = "Stuck here\n---\nstatus: fixed\n## Triage\n<!-- sync:preserve -->";

        var text = BugReportMarkdown.Render(BugReportSamples.Input(HOSTILE));
        var header = text[..text.IndexOf("\n---\n", 4, StringComparison.Ordinal)];

        header.Split('\n').Where(line => line.StartsWith("status:", StringComparison.Ordinal)).Should().Equal("status: open");
        text.Should().Contain("```text\n" + HOSTILE + "\n```\n");
        text.Should().EndWith("## Triage\n\n<!-- sync:preserve -->\n");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Control_characters_are_removed_and_line_ends_normalized()
    {
        var text = BugReportMarkdown.Render(BugReportSamples.Input("Bell\u0007 here\r\nnext line"));

        text.Should().Contain("```text\nBell here\nnext line\n```\n");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Title_is_the_first_sixty_characters_on_one_line()
    {
        var text = BugReportMarkdown.Render(BugReportSamples.Input("Line one\nline two " + new string('x', 80)));

        text.Should().Contain("title: \"Line one line two " + new string('x', 42) + "\"\n");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Quotes_and_backslashes_are_escaped_in_header_values()
    {
        var text = BugReportMarkdown.Render(BugReportSamples.Input(world: BugReportSamples.World(mapName: "The \"Crypt\" \\ B1")));

        text.Should().Contain("map: \"The \\\"Crypt\\\" \\\\ B1\"\n");

        await Task.CompletedTask;
    }
}
```

Create `Tests/Chaos.Tests/BugReports/WorldSnapshotTests.cs`:

```csharp
using Chaos.Geometry;
using Chaos.Services.BugReports;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;

namespace Chaos.Tests.BugReports;

public class WorldSnapshotTests
{
    [Test]
    public async Task Captures_map_position_vitals_and_class()
    {
        var map = MockMapInstance.Create("mileth_inn", "Mileth Inn", 20, 15);
        var aisling = MockAisling.Create(map, "Bob", new Point(12, 7));
        var now = new DateTime(2026, 9, 23, 23, 43, 1, DateTimeKind.Utc);

        var snapshot = WorldSnapshot.Capture(aisling, now, "1.2.3");

        snapshot.CharacterName.Should().Be("Bob");
        snapshot.ServerVersion.Should().Be("1.2.3");
        snapshot.ServerTimeUtc.Should().Be(now);
        snapshot.ClassName.Should().Be(aisling.UserStatSheet.BaseClass.ToString());
        snapshot.Map.Should().Be(new WorldSnapshot.MapInfo("Mileth Inn", "mileth_inn", map.InstanceId, 20, 15));
        snapshot.Position.X.Should().Be(12);
        snapshot.Position.Y.Should().Be(7);
        snapshot.Vitals.Hp.Should().Be(100);
        snapshot.Vitals.MaxHp.Should().Be(100u);
        snapshot.Vitals.Alive.Should().BeTrue();
        snapshot.Group.Should().BeEmpty();
        snapshot.Nearby.Players.Should().NotContain("Bob");

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run (from `Chaos-Server/`): `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportMarkdownTests/*"`
Expected: the build fails because `Chaos.Services.BugReports` does not exist yet.

- [ ] **Step 4: Write `WorldSnapshot`**

Create `Chaos/Services/BugReports/WorldSnapshot.cs`:

```csharp
#region
using Chaos.Models.World;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>
///     What was around a player when they opened the bug report window. Written to <c>world.json</c> and used for the
///     summary lines in <c>report.md</c>. Captured on the world thread, so it is a plain copy with no live references.
/// </summary>
public sealed record WorldSnapshot
{
    public required DateTime ServerTimeUtc { get; init; }
    public required DateTime ServerTimeLocal { get; init; }
    public required string ServerVersion { get; init; }
    public required string CharacterName { get; init; }
    public required string ClassName { get; init; }
    public required int Level { get; init; }
    public required MapInfo Map { get; init; }
    public required PositionInfo Position { get; init; }
    public required VitalsInfo Vitals { get; init; }
    public required IReadOnlyList<GroupMemberInfo> Group { get; init; }
    public required NearbyInfo Nearby { get; init; }

    /// <summary>Copies the state around <paramref name="aisling" />. Call it on the world thread for the aisling's map.</summary>
    public static WorldSnapshot Capture(Aisling aisling, DateTime utcNow, string serverVersion)
    {
        var map = aisling.MapInstance;
        var sheet = aisling.UserStatSheet;

        return new WorldSnapshot
        {
            ServerTimeUtc = utcNow,
            ServerTimeLocal = utcNow.ToLocalTime(),
            ServerVersion = serverVersion,
            CharacterName = aisling.Name,
            ClassName = sheet.BaseClass.ToString(),
            Level = sheet.Level,
            Map = new MapInfo(
                map.Name,
                map.Template.TemplateKey,
                map.InstanceId,
                map.Template.Width,
                map.Template.Height),
            Position = new PositionInfo(aisling.X, aisling.Y, aisling.Direction.ToString()),
            Vitals = new VitalsInfo(
                sheet.CurrentHp,
                sheet.EffectiveMaximumHp,
                sheet.CurrentMp,
                sheet.EffectiveMaximumMp,
                aisling.IsAlive),
            Group = aisling.Group is null
                ? []
                : aisling.Group
                         .Where(member => member != aisling)
                         .Select(
                             member => new GroupMemberInfo(
                                 member.Name,
                                 member.UserStatSheet.Level,
                                 member.UserStatSheet.BaseClass.ToString(),
                                 member.MapInstance.InstanceId,
                                 member.X,
                                 member.Y))
                         .ToList(),
            Nearby = new NearbyInfo(
                map.GetEntitiesWithinRange<Monster>(aisling)
                   .OrderBy(monster => monster.Name)
                   .ThenBy(monster => monster.X)
                   .ThenBy(monster => monster.Y)
                   .Select(
                       monster => new CreatureInfo(
                           monster.Name,
                           monster.Template.TemplateKey,
                           monster.X,
                           monster.Y,
                           (int)monster.StatSheet.HealthPercent))
                   .ToList(),
                map.GetEntitiesWithinRange<Merchant>(aisling)
                   .OrderBy(merchant => merchant.Name)
                   .Select(merchant => new EntityInfo(merchant.Name, merchant.Template.TemplateKey, merchant.X, merchant.Y))
                   .ToList(),
                map.GetEntitiesWithinRange<GroundItem>(aisling)
                   .OrderBy(ground => ground.Item.DisplayName)
                   .Select(
                       ground => new GroundItemInfo(
                           ground.Item.DisplayName,
                           ground.Item.Template.TemplateKey,
                           ground.X,
                           ground.Y,
                           ground.Item.Count))
                   .ToList(),
                map.GetEntitiesWithinRange<Aisling>(aisling)
                   .Where(other => other != aisling)
                   .Select(other => other.Name)
                   .Order()
                   .ToList())
        };
    }

    public sealed record MapInfo(string Name, string TemplateKey, string InstanceId, int Width, int Height);

    public sealed record PositionInfo(int X, int Y, string Direction);

    public sealed record VitalsInfo(int Hp, uint MaxHp, int Mp, uint MaxMp, bool Alive);

    public sealed record GroupMemberInfo(string Name, int Level, string ClassName, string MapInstanceId, int X, int Y);

    public sealed record CreatureInfo(string Name, string TemplateKey, int X, int Y, int HpPercent);

    public sealed record EntityInfo(string Name, string TemplateKey, int X, int Y);

    public sealed record GroundItemInfo(string Name, string TemplateKey, int X, int Y, int Count);

    public sealed record NearbyInfo(
        IReadOnlyList<CreatureInfo> Monsters,
        IReadOnlyList<EntityInfo> Npcs,
        IReadOnlyList<GroundItemInfo> GroundItems,
        IReadOnlyList<string> Players);
}
```

`GetEntitiesWithinRange` uses its default range of 15 tiles. That is the view range the server uses for players.

- [ ] **Step 5: Write `ClientDetails`**

Create `Chaos/Services/BugReports/ClientDetails.cs`:

```csharp
#region
using Chaos.Networking.Entities.Client;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>
///     The client facts a report was sent with. Written to <c>client.json</c> and summarized in <c>report.md</c>. The
///     strings come from the player's client, so they are reduced to one clean line.
/// </summary>
public sealed record ClientDetails(
    string Build,
    string Os,
    int Fps,
    int PingMs,
    string HudStyle,
    int WindowWidth,
    int WindowHeight)
{
    public static ClientDetails From(BugReportInteractionArgs submit)
        => new(
            BugReportMarkdown.OneLine(submit.ClientBuild),
            BugReportMarkdown.OneLine(submit.OsDescription),
            submit.FramesPerSecond,
            submit.PingMs,
            submit.HudStyle switch
            {
                0 => "classic",
                1 => "large",
                _ => "unknown"
            },
            submit.WindowWidth,
            submit.WindowHeight);
}
```

- [ ] **Step 6: Write `BugReportMarkdown`**

Create `Chaos/Services/BugReports/BugReportMarkdown.cs`:

```csharp
#region
using System.Globalization;
using System.Text;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>
///     Renders <c>report.md</c> in the same shape as the Discord feedback archive (header fields, status values and the
///     Triage preserve marker), so in-game and Discord reports are read and triaged the same way. The player's text is
///     untrusted: it goes inside a fence longer than any backtick run in it, so it cannot add header fields, headings or
///     the preserve marker.
/// </summary>
public static class BugReportMarkdown
{
    public const string PRESERVE_MARKER = "<!-- sync:preserve -->";
    public const int TITLE_LENGTH = 60;

    public static string Render(Input input)
    {
        var text = Sanitize(input.Description);
        var world = input.World;
        var fence = Fence(text);
        var sb = new StringBuilder();

        sb.Append("---\n");
        sb.Append($"id: {YamlQuote(input.Id)}\n");
        sb.Append("kind: in-game\n");
        sb.Append($"category: {BugReportProtocol.FolderName(input.Category)}\n");
        sb.Append($"title: {YamlQuote(Title(text))}\n");
        sb.Append($"author: {YamlQuote(world.CharacterName)}\n");
        sb.Append($"created: {input.ReportedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}\n");
        sb.Append($"reported_at: {input.ReportedAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)}\n");
        sb.Append($"map: {YamlQuote(world.Map.Name)}\n");
        sb.Append($"map_key: {YamlQuote(world.Map.TemplateKey)}\n");
        sb.Append($"x: {world.Position.X}\n");
        sb.Append($"y: {world.Position.Y}\n");
        sb.Append($"client_build: {YamlQuote(input.Client.Build)}\n");
        sb.Append($"screenshot: {(input.HasScreenshot ? "true" : "false")}\n");
        sb.Append("status: open\n");
        sb.Append("status_note: \"\"\n");
        sb.Append("---\n\n");

        sb.Append("## Report\n\n");
        sb.Append($"{fence}text\n{text}\n{fence}\n\n");

        sb.Append("## Details\n\n");

        foreach (var line in DetailLines(input))
            sb.Append($"- {line}\n");

        sb.Append("\n## Triage\n\n");
        sb.Append(PRESERVE_MARKER).Append('\n');

        return sb.ToString();
    }

    /// <summary>Normalizes line ends to <c>\n</c>, removes control characters other than newline and tab, and trims.</summary>
    public static string Sanitize(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
            if (!char.IsControl(ch) || ch is '\n' or '\t')
                sb.Append(ch);

        return sb.ToString().Trim();
    }

    /// <summary>The sanitized text on one line, with every run of whitespace reduced to one space.</summary>
    public static string OneLine(string text)
        => string.Join(' ', Sanitize(text).Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The first <see cref="TITLE_LENGTH" /> characters of the description, on one line.</summary>
    public static string Title(string description)
    {
        var line = OneLine(description);

        return line.Length <= TITLE_LENGTH ? line : line[..TITLE_LENGTH].TrimEnd();
    }

    /// <summary>A backtick fence at least three long and one longer than the longest backtick run in the text.</summary>
    public static string Fence(string text)
    {
        var longest = 0;
        var run = 0;

        foreach (var ch in text)
        {
            run = ch == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return new string('`', Math.Max(3, longest + 1));
    }

    /// <summary>A double-quoted header value, escaped the same way as <c>archive.yaml_quote</c> in FeedbackSync.</summary>
    public static string YamlQuote(string value) => "\"" + OneLine(value).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static IEnumerable<string> DetailLines(Input input)
    {
        var world = input.World;
        var vitals = world.Vitals;
        var client = input.Client;

        yield return
            $"{world.ClassName}, level {world.Level}. HP {vitals.Hp}/{vitals.MaxHp}, MP {vitals.Mp}/{vitals.MaxMp}. {(vitals.Alive ? "Alive" : "Dead")}.";

        yield return world.Group.Count == 0
            ? "Group: none"
            : "Group: " + string.Join(", ", world.Group.Select(member => $"{member.Name} ({member.ClassName} {member.Level})"));

        yield return
            $"Client: {client.Build}, {client.Os}, {client.Fps} fps, {client.PingMs} ms, {client.HudStyle} HUD, {client.WindowWidth}×{client.WindowHeight}";

        var files = new List<string>();

        if (input.HasScreenshot)
            files.Add("screenshot.png");

        files.AddRange(["world.json", "client.json", $"server-log.jsonl ({input.LogLineCount} lines)", "character/"]);

        yield return "Files: " + string.Join(", ", files);

        foreach (var note in input.Notes)
            yield return $"Note: {note}";
    }

    /// <summary>Everything <see cref="Render" /> needs for one report.</summary>
    public sealed record Input
    {
        public required string Id { get; init; }
        public required BugReportCategory Category { get; init; }
        public required string Description { get; init; }
        public required WorldSnapshot World { get; init; }
        public required DateTimeOffset ReportedAt { get; init; }
        public required ClientDetails Client { get; init; }
        public required bool HasScreenshot { get; init; }
        public required int LogLineCount { get; init; }
        public IReadOnlyList<string> Notes { get; init; } = [];
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run (from `Chaos-Server/`):
`dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportMarkdownTests/*"`
`dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/WorldSnapshotTests/*"`
Expected: all pass. If `WorldSnapshotTests` fails on `Map`, check `MockMapInstance.Create` for how it builds `Template` and `InstanceId`. Fix the test to match the mock, not the production code.

```json:metadata
{"files": ["Chaos.Client/Chaos-Server/Chaos/Services/BugReports/WorldSnapshot.cs", "Chaos.Client/Chaos-Server/Chaos/Services/BugReports/ClientDetails.cs", "Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportMarkdown.cs", "Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportSamples.cs", "Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/WorldSnapshotTests.cs", "Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportMarkdownTests.cs"], "verifyCommand": "cd Chaos.Client/Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/BugReportMarkdownTests/*\"", "acceptanceCriteria": ["snapshot records map, position, vitals and class", "report.md header matches the spec example exactly", "hostile description cannot add a second status line", "control characters removed and line ends normalized", "title is first 60 chars on one line", "details lines cover vitals, group, client, files, notes"], "modelTier": "mechanical"}
```

---

### Task 3: Report files on the server

**Goal:** Read the server's log lines about a character, manage the report folders, and let `AislingStore` write the nine save files into any folder.

**Files:**
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/ServerLogReader.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportStore.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/IAislingSnapshotWriter.cs`
- Modify: `Chaos.Client/Chaos-Server/Chaos/Services/Storage/AislingStore.cs` (implement `IAislingSnapshotWriter`)
- Test: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/ServerLogReaderTests.cs`
- Test: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportStoreTests.cs`

**Acceptance Criteria:**
- [ ] The log reader keeps only lines inside the time window that contain the character name as a complete JSON string, ignoring case, oldest first
- [ ] The log reader stops at the cap and keeps the newest lines
- [ ] The log reader reads across midnight, and from `logs/archive/`
- [ ] The log reader reads a file another writer holds open
- [ ] `ReadLinesBackwards` returns the right lines when a line spans read blocks
- [ ] Folder names use the format `yyyy-MM-dd_HHmmss_<lowercase name>`
- [ ] Moving a report puts it in its category folder, and adds `_2`, `_3` on a name clash
- [ ] Holding folders are created with `character/`, deleted singly, and cleared together
- [ ] `world.json` uses camelCase property names
- [ ] `AislingStore` implements `IAislingSnapshotWriter`, and `Chaos.slnx` builds

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ServerLogReaderTests/*"` and the same with `BugReportStoreTests` (from `Chaos-Server/`) → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Tests/Chaos.Tests/BugReports/ServerLogReaderTests.cs`:

```csharp
using System.Globalization;
using System.Text;
using Chaos.Services.BugReports;
using FluentAssertions;

namespace Chaos.Tests.BugReports;

public class ServerLogReaderTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 19, 43, 0, DateTimeKind.Local);

    private static string Line(DateTime time, string name, string message)
        => "{ \"Time\": \""
           + time.ToString("yyyy-MM-dd HH:mm:ss.ffff", CultureInfo.InvariantCulture)
           + "\", \"Level\": \"Info\", \"Message\": \""
           + message
           + "\", \"AislingName\": \""
           + name
           + "\" }";

    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "bugreport-logs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return path;
    }

    private static void WriteLog(string path, DateTime lastWrite, params string[] lines)
    {
        File.WriteAllText(path, string.Join("\n", lines) + "\n");
        File.SetLastWriteTime(path, lastWrite);
    }

    [Test]
    public async Task Keeps_lines_in_the_window_that_name_the_character()
    {
        var directory = TempDirectory();

        WriteLog(
            Path.Combine(directory, "2026-09-23.log"),
            Now,
            Line(Now.AddMinutes(-20), "Bob", "too old"),
            Line(Now.AddMinutes(-10), "Bob", "first"),
            Line(Now.AddMinutes(-5), "Alice", "someone else"),
            Line(Now.AddMinutes(-2), "Bobcat", "longer name"),
            Line(Now.AddMinutes(-1), "BOB", "second"));

        var lines = ServerLogReader.Read(directory, "Bob", Now.AddMinutes(-15), 2000);

        lines.Should().HaveCount(2);
        lines[0].Should().Contain("\"first\"");
        lines[1].Should().Contain("\"second\"");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Stops_at_the_cap_and_keeps_the_newest()
    {
        var directory = TempDirectory();

        WriteLog(
            Path.Combine(directory, "2026-09-23.log"),
            Now,
            Enumerable.Range(0, 10).Select(i => Line(Now.AddMinutes(-10 + i), "Bob", $"event {i}")).ToArray());

        var lines = ServerLogReader.Read(directory, "Bob", Now.AddMinutes(-15), 3);

        lines.Should().HaveCount(3);
        lines[0].Should().Contain("event 7");
        lines[2].Should().Contain("event 9");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reads_across_midnight_and_the_archive_folder()
    {
        var directory = TempDirectory();
        var archive = Directory.CreateDirectory(Path.Combine(directory, "archive")).FullName;
        var midnight = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Local);

        WriteLog(
            Path.Combine(archive, "2026-09-23.0.log"),
            midnight.AddMinutes(-8),
            Line(midnight.AddMinutes(-9), "Bob", "rolled"));

        WriteLog(
            Path.Combine(directory, "2026-09-23.log"),
            midnight.AddSeconds(-1),
            Line(midnight.AddMinutes(-20), "Bob", "too old"),
            Line(midnight.AddMinutes(-5), "Bob", "yesterday"));

        WriteLog(Path.Combine(directory, "2026-09-24.log"), midnight.AddMinutes(5), Line(midnight.AddMinutes(3), "Bob", "today"));

        var lines = ServerLogReader.Read(directory, "Bob", midnight.AddMinutes(-10), 2000);

        lines.Should().HaveCount(3);
        lines[0].Should().Contain("\"rolled\"");
        lines[1].Should().Contain("\"yesterday\"");
        lines[2].Should().Contain("\"today\"");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reads_a_file_another_writer_holds_open()
    {
        var directory = TempDirectory();
        var now = DateTime.Now;
        var path = Path.Combine(directory, "live.log");

        await using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        writer.Write(Encoding.UTF8.GetBytes(Line(now.AddMinutes(-1), "Bob", "while open") + "\n"));
        writer.Flush();

        var lines = ServerLogReader.Read(directory, "Bob", now.AddMinutes(-15), 2000);

        lines.Should().ContainSingle().Which.Should().Contain("while open");
    }

    [Test]
    public async Task Reads_lines_backwards_across_block_boundaries()
    {
        var directory = TempDirectory();
        var path = Path.Combine(directory, "blocks.log");
        File.WriteAllText(path, "alpha\nbeta\n\ngamma");

        ServerLogReader.ReadLinesBackwards(path, 5).Should().Equal("gamma", "beta", "alpha");

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_missing_log_folder_gives_no_lines()
    {
        var lines = ServerLogReader.Read(Path.Combine(TempDirectory(), "missing"), "Bob", Now.AddMinutes(-15), 2000);

        lines.Should().BeEmpty();

        await Task.CompletedTask;
    }
}
```

Create `Tests/Chaos.Tests/BugReports/BugReportStoreTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.BugReports;
using FluentAssertions;

namespace Chaos.Tests.BugReports;

public class BugReportStoreTests
{
    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "bugreport-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return path;
    }

    [Test]
    public async Task Folder_name_uses_local_time_and_a_lowercase_name()
    {
        BugReportStore.FolderName(new DateTime(2026, 9, 23, 19, 43, 1), "Bob").Should().Be("2026-09-23_194301_bob");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Pending_folder_is_created_with_a_character_folder_and_deleted()
    {
        var root = TempDirectory();
        var store = new BugReportStore(root);

        var folder = store.CreatePending(42);

        folder.Should().Be(Path.Combine(root, "_pending", "42"));
        Directory.Exists(Path.Combine(folder, "character")).Should().BeTrue();

        store.DeletePending(42);

        Directory.Exists(folder).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clear_pending_removes_every_pending_folder()
    {
        var root = TempDirectory();
        var store = new BugReportStore(root);
        store.CreatePending(1);
        store.CreatePending(2);

        store.ClearPending();

        Directory.Exists(Path.Combine(root, "_pending")).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Move_puts_the_report_in_its_category_and_numbers_clashes()
    {
        var root = TempDirectory();
        var store = new BugReportStore(root);
        store.CreatePending(1);
        await File.WriteAllTextAsync(Path.Combine(store.PendingPath(1), "world.json"), "{}");

        var first = store.MovePendingToCategory(1, BugReportCategory.MapWarp, "2026-09-23_194301_bob");

        first.Should().Be(Path.Combine(root, "map-warp", "2026-09-23_194301_bob"));
        File.Exists(Path.Combine(first, "world.json")).Should().BeTrue();
        Directory.Exists(store.PendingPath(1)).Should().BeFalse();

        store.CreatePending(2);
        store.CreatePending(3);

        store.MovePendingToCategory(2, BugReportCategory.MapWarp, "2026-09-23_194301_bob")
             .Should()
             .Be(Path.Combine(root, "map-warp", "2026-09-23_194301_bob_2"));

        store.MovePendingToCategory(3, BugReportCategory.MapWarp, "2026-09-23_194301_bob")
             .Should()
             .Be(Path.Combine(root, "map-warp", "2026-09-23_194301_bob_3"));
    }

    [Test]
    public async Task Move_without_a_pending_folder_still_makes_the_report_folder()
    {
        var root = TempDirectory();

        var folder = new BugReportStore(root).MovePendingToCategory(7, BugReportCategory.Other, "2026-09-23_194301_bob");

        Directory.Exists(folder).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Looks_like_png_checks_the_signature()
    {
        BugReportStore.LooksLikePng([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1]).Should().BeTrue();
        BugReportStore.LooksLikePng("GIF89a"u8.ToArray()).Should().BeFalse();
        BugReportStore.LooksLikePng([]).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task World_json_uses_camel_case()
    {
        var root = TempDirectory();

        await BugReportStore.WriteWorldAsync(root, BugReportSamples.World());

        (await File.ReadAllTextAsync(Path.Combine(root, "world.json"))).Should().Contain("\"templateKey\": \"mileth_inn\"");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run (from `Chaos-Server/`): `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportStoreTests/*"`
Expected: the build fails because `ServerLogReader` and `BugReportStore` do not exist.

- [ ] **Step 3: Write `ServerLogReader`**

Create `Chaos/Services/BugReports/ServerLogReader.cs`:

```csharp
#region
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>
///     Reads the server's own log files for the lines about one character. The NLog file target writes one JSON object
///     per line to <c>logs/&lt;date&gt;.log</c>, rolls older lines into <c>logs/archive/</c>, and stamps each line's
///     <c>Time</c> in server local time as <c>yyyy-MM-dd HH:mm:ss.ffff</c>.
/// </summary>
public static partial class ServerLogReader
{
    private const string TIME_FORMAT = "yyyy-MM-dd HH:mm:ss.ffff";

    /// <summary>
    ///     Returns up to <paramref name="cap" /> lines written at or after <paramref name="windowStartLocal" /> that name
    ///     <paramref name="characterName" /> as a complete JSON string (ignoring case), oldest first. When there are more,
    ///     the newest are kept. Files are read backwards from the end, so a large log costs only its recent tail.
    /// </summary>
    public static IReadOnlyList<string> Read(string logDirectory, string characterName, DateTime windowStartLocal, int cap)
    {
        var needle = $"\"{characterName}\"";
        var newestFirst = new List<string>();

        var files = new[] { logDirectory, Path.Combine(logDirectory, "archive") }
                    .Where(Directory.Exists)
                    .SelectMany(directory => new DirectoryInfo(directory).EnumerateFiles("*.log"))
                    .Where(file => file.LastWriteTime >= windowStartLocal)
                    .OrderByDescending(file => file.LastWriteTime)
                    .ToList();

        foreach (var file in files)
            foreach (var line in ReadLinesBackwards(file.FullName))
            {
                if (!TryReadTime(line, out var time))
                    continue;

                if (time < windowStartLocal)
                    break;

                if (!line.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    continue;

                newestFirst.Add(line);

                if (newestFirst.Count >= cap)
                    return OldestFirst(newestFirst);
            }

        return OldestFirst(newestFirst);

        static List<string> OldestFirst(List<string> lines)
        {
            lines.Reverse();

            return lines;
        }
    }

    /// <summary>
    ///     Yields the lines of a file from last to first, reading from the end in blocks of <paramref name="blockSize" />
    ///     bytes. Empty lines are skipped. The file is opened so that a writer holding it open (NLog keeps its file open)
    ///     does not block the read.
    /// </summary>
    public static IEnumerable<string> ReadLinesBackwards(string path, int blockSize = 65_536)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[blockSize];
        var carry = new List<byte>();
        var position = stream.Length;

        while (position > 0)
        {
            var size = (int)Math.Min(blockSize, position);
            position -= size;
            stream.Position = position;
            stream.ReadExactly(buffer, 0, size);

            var end = size;

            for (var i = size - 1; i >= 0; i--)
            {
                if (buffer[i] != (byte)'\n')
                    continue;

                var line = Decode(buffer, i + 1, end - i - 1, carry);
                carry.Clear();
                end = i;

                if (line.Length > 0)
                    yield return line;
            }

            //the bytes before the first newline in this block are the end of a line that starts in an earlier block
            carry.InsertRange(0, buffer.AsSpan(0, end).ToArray());
        }

        if (carry.Count > 0)
        {
            var first = Encoding.UTF8.GetString(carry.ToArray()).TrimEnd('\r');

            if (first.Length > 0)
                yield return first;
        }
    }

    private static string Decode(byte[] buffer, int start, int count, List<byte> carry)
    {
        if (carry.Count == 0)
            return Encoding.UTF8.GetString(buffer, start, count).TrimEnd('\r');

        var bytes = new byte[count + carry.Count];
        Array.Copy(buffer, start, bytes, 0, count);
        carry.CopyTo(bytes, count);

        return Encoding.UTF8.GetString(bytes).TrimEnd('\r');
    }

    private static bool TryReadTime(string line, out DateTime time)
    {
        time = default;
        var match = TimeField().Match(line);

        return match.Success
               && DateTime.TryParseExact(
                   match.Groups[1].Value,
                   TIME_FORMAT,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out time);
    }

    [GeneratedRegex("\"Time\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex TimeField();
}
```

- [ ] **Step 4: Write `BugReportStore`**

Create `Chaos/Services/BugReports/BugReportStore.cs`:

```csharp
#region
using System.Globalization;
using System.Text.Json;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>
///     The folders of in-game bug reports under one root: a holding folder per open report
///     (<c>_pending/&lt;report number&gt;/</c>), and one folder per finished report under its category. Holds no state
///     of its own.
/// </summary>
public sealed class BugReportStore(string rootDirectory)
{
    public const string PENDING_FOLDER = "_pending";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public string RootDirectory { get; } = rootDirectory;

    public string PendingPath(uint reportId)
        => Path.Combine(RootDirectory, PENDING_FOLDER, reportId.ToString(CultureInfo.InvariantCulture));

    /// <summary>Creates an empty holding folder with a <c>character/</c> folder in it, replacing any leftover.</summary>
    public string CreatePending(uint reportId)
    {
        var folder = PendingPath(reportId);

        if (Directory.Exists(folder))
            Directory.Delete(folder, true);

        Directory.CreateDirectory(Path.Combine(folder, "character"));

        return folder;
    }

    public void DeletePending(uint reportId)
    {
        var folder = PendingPath(reportId);

        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
    }

    /// <summary>Deletes every holding folder. Run at startup, when no report can be open.</summary>
    public void ClearPending()
    {
        var folder = Path.Combine(RootDirectory, PENDING_FOLDER);

        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
    }

    /// <summary>
    ///     Moves the holding folder to <c>&lt;category&gt;/&lt;folderName&gt;</c>, adding <c>_2</c>, <c>_3</c> and so on
    ///     when that name is taken. When there is no holding folder, an empty report folder is created instead, so the
    ///     report can still be written. Returns the report folder.
    /// </summary>
    public string MovePendingToCategory(uint reportId, BugReportCategory category, string folderName)
    {
        var categoryFolder = Path.Combine(RootDirectory, BugReportProtocol.FolderName(category));
        Directory.CreateDirectory(categoryFolder);

        var target = Path.Combine(categoryFolder, folderName);

        for (var suffix = 2; Directory.Exists(target); suffix++)
            target = Path.Combine(categoryFolder, $"{folderName}_{suffix}");

        var pending = PendingPath(reportId);

        if (Directory.Exists(pending))
            Directory.Move(pending, target);
        else
            Directory.CreateDirectory(target);

        return target;
    }

    public static string FolderName(DateTime localTime, string characterName)
        => $"{localTime.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)}_{characterName.ToLowerInvariant()}";

    public static bool LooksLikePng(ReadOnlySpan<byte> bytes) => bytes.StartsWith(PngSignature);

    public static Task WriteWorldAsync(string folder, WorldSnapshot world)
        => File.WriteAllTextAsync(Path.Combine(folder, "world.json"), JsonSerializer.Serialize(world, JsonOptions));

    public static Task WriteClientAsync(string folder, ClientDetails client)
        => File.WriteAllTextAsync(Path.Combine(folder, "client.json"), JsonSerializer.Serialize(client, JsonOptions));

    public static Task WriteScreenshotAsync(string folder, byte[] png) => File.WriteAllBytesAsync(Path.Combine(folder, "screenshot.png"), png);

    public static Task WriteLogAsync(string folder, IReadOnlyList<string> lines)
        => File.WriteAllLinesAsync(Path.Combine(folder, "server-log.jsonl"), lines);

    public static Task WriteReportAsync(string folder, string markdown) => File.WriteAllTextAsync(Path.Combine(folder, "report.md"), markdown);
}
```

- [ ] **Step 5: Let `AislingStore` write a snapshot**

Create `Chaos/Services/BugReports/IAislingSnapshotWriter.cs`:

```csharp
#region
using Chaos.Models.World;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>Writes a copy of a character's nine save files into any folder, for a bug report.</summary>
public interface IAislingSnapshotWriter
{
    /// <summary>
    ///     Maps <paramref name="aisling" /> to its save schemas on the calling thread, before the first await, then writes
    ///     the nine save files into <paramref name="directory" />. Call it on the world thread for the aisling's map, so
    ///     nothing changes the character mid-copy.
    /// </summary>
    Task WriteSnapshotAsync(Aisling aisling, string directory);
}
```

In `Chaos/Services/Storage/AislingStore.cs`:
- Add `using Chaos.Services.BugReports;` to the using region.
- Change the class's base list from `: IAsyncStore<Aisling>, IFacadeStore<Aisling>` to `: IAsyncStore<Aisling>, IFacadeStore<Aisling>, IAislingSnapshotWriter`.
- Insert this method before `InnerSaveAsync` (Serena `insert_before_symbol` on `AislingStore/InnerSaveAsync`):

```csharp
    /// <inheritdoc />
    public Task WriteSnapshotAsync(Aisling aisling, string directory)
    {
        Directory.CreateDirectory(directory);

        //each SaveAndMap call maps its object before its first await, so every schema is built on this thread
        return InnerSaveAsync(directory, aisling);
    }

```

- [ ] **Step 6: Run the tests and the build**

Run (from `Chaos-Server/`):
`dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ServerLogReaderTests/*"`
`dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportStoreTests/*"`
`dotnet build Chaos.slnx`
Expected: all tests pass and the build succeeds.

```json:metadata
{"files": ["Chaos.Client/Chaos-Server/Chaos/Services/BugReports/ServerLogReader.cs", "Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportStore.cs", "Chaos.Client/Chaos-Server/Chaos/Services/BugReports/IAislingSnapshotWriter.cs", "Chaos.Client/Chaos-Server/Chaos/Services/Storage/AislingStore.cs", "Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/ServerLogReaderTests.cs", "Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportStoreTests.cs"], "verifyCommand": "cd Chaos.Client/Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ServerLogReaderTests/*\" && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/BugReportStoreTests/*\"", "acceptanceCriteria": ["log reader filters window and exact name ignoring case, oldest first", "log reader caps keeping newest", "log reader reads across midnight and archive folder", "log reader reads a file held open by a writer", "backwards reader handles block boundaries", "folder names and clash suffixes", "pending folders created, deleted, cleared", "world.json camelCase", "AislingStore implements IAislingSnapshotWriter and Chaos.slnx builds"], "modelTier": "standard"}
```

---

### Task 4: Report ledger and options

**Goal:** Build the thread-safe in-memory rules for open reports (report numbers, owners, wait time, picture parts, expiry), plus the settings class.

**Files:**
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportOptions.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportLedger.cs`
- Test: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportLedgerTests.cs`

**Acceptance Criteria:**
- [ ] A report number works for one submit, then reads as expired. The same holds after the lifetime ends and after cancel
- [ ] A submit from another character returns `NotOwner`
- [ ] A bad category or description is refused, and the report stays open for a retry
- [ ] The 2-minute wait starts at submit, not at open or cancel, and ignores name case
- [ ] Parts join in index order whatever order they arrive. A duplicate replaces the earlier part. A bad index, size or owner is ignored
- [ ] An oversized picture length completes at submit with the note `picture not saved (over the size limit)`
- [ ] A wrong total length is noted as `picture not saved (length did not match)`
- [ ] The sweep finishes a submitted report after a 30-second gap, with the note `picture not saved (parts missing)`
- [ ] The sweep discards unsent reports that expired or whose player left
- [ ] `BugReportOptions.UseBaseDirectory` puts reports under the staging folder, and logs next to the server executable

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportLedgerTests/*"` (from `Chaos-Server/`) → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Tests/Chaos.Tests/BugReports/BugReportLedgerTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Services.BugReports;
using FluentAssertions;

namespace Chaos.Tests.BugReports;

public class BugReportLedgerTests
{
    private static readonly DateTime T0 = new(2026, 9, 23, 20, 0, 0, DateTimeKind.Utc);

    private static BugReportLedger<string> NewLedger() => new(new BugReportOptions());

    private static uint Open(BugReportLedger<string> ledger, string name = "Bob", DateTime? at = null)
        => ledger.Open(name, id => $"context-{id}", at ?? T0).ReportId;

    private static BugReportInteractionArgs SubmitArgs(
        uint reportId,
        uint pictureLength = 0,
        string description = "Stuck in the wall at the inn",
        BugReportCategory category = BugReportCategory.MapWarp)
        => new()
        {
            Type = BugReportInteractionType.Submit,
            ReportId = reportId,
            Category = category,
            Description = description,
            PictureLength = pictureLength
        };

    [Test]
    public async Task Open_gives_a_nonzero_number_and_builds_the_context_with_it()
    {
        var ledger = NewLedger();

        (var id, var replaced) = ledger.Open("Bob", reportId => $"context-{reportId}", T0);

        id.Should().NotBe(0u);
        replaced.Should().BeNull();
        ledger.Submit(id, "Bob", SubmitArgs(id), T0).Report!.Context.Should().Be($"context-{id}");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Opening_again_replaces_the_unsent_report()
    {
        var ledger = NewLedger();
        var first = Open(ledger);

        (var second, var replaced) = ledger.Open("bob", _ => "again", T0);

        replaced!.ReportId.Should().Be(first);
        second.Should().NotBe(first);
        ledger.Submit(first, "Bob", SubmitArgs(first), T0).Status.Should().Be(SubmitStatus.Expired);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Submit_without_a_picture_completes_and_uses_up_the_number()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        var outcome = ledger.Submit(id, "Bob", SubmitArgs(id), T0);

        outcome.Status.Should().Be(SubmitStatus.Complete);
        outcome.Report!.AssemblePicture(BugReportProtocol.MAX_PICTURE_BYTES).Should().Be(((byte[]?)null, (string?)null));
        ledger.Submit(id, "Bob", SubmitArgs(id), T0).Status.Should().Be(SubmitStatus.Expired);

        await Task.CompletedTask;
    }

    [Test]
    public async Task An_unknown_number_is_expired()
    {
        var outcome = NewLedger().Submit(12345, "Bob", SubmitArgs(12345), T0);

        outcome.Status.Should().Be(SubmitStatus.Expired);
        outcome.Report.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Another_character_is_not_the_owner()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        ledger.Submit(id, "Alice", SubmitArgs(id), T0).Status.Should().Be(SubmitStatus.NotOwner);
        ledger.Submit(id, "Bob", SubmitArgs(id), T0).Status.Should().Be(SubmitStatus.Complete);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_number_past_its_lifetime_is_expired_and_handed_back_for_cleanup()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        var outcome = ledger.Submit(id, "Bob", SubmitArgs(id), T0.AddMinutes(30));

        outcome.Status.Should().Be(SubmitStatus.Expired);
        outcome.Report!.ReportId.Should().Be(id);

        await Task.CompletedTask;
    }

    //formatter:off
    [Test]
    [Arguments("too short")]
    [Arguments("   123456789   ")]
    //formatter:on
    public async Task Short_descriptions_are_refused_and_the_report_stays_open(string description)
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        ledger.Submit(id, "Bob", SubmitArgs(id, description: description), T0).Status.Should().Be(SubmitStatus.BadDescription);
        ledger.Submit(id, "Bob", SubmitArgs(id), T0).Status.Should().Be(SubmitStatus.Complete);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Descriptions_over_a_thousand_characters_are_refused()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        ledger.Submit(id, "Bob", SubmitArgs(id, description: new string('a', 1001)), T0).Status.Should().Be(SubmitStatus.BadDescription);
        ledger.Submit(id, "Bob", SubmitArgs(id, description: new string('a', 1000)), T0).Status.Should().Be(SubmitStatus.Complete);

        await Task.CompletedTask;
    }

    [Test]
    public async Task An_unknown_category_is_refused()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        ledger.Submit(id, "Bob", SubmitArgs(id, category: (BugReportCategory)8), T0).Status.Should().Be(SubmitStatus.BadCategory);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_wait_starts_at_submit_and_lasts_two_minutes()
    {
        var ledger = NewLedger();

        ledger.WaitRemaining("Bob", T0).Should().Be(TimeSpan.Zero);

        var id = Open(ledger);

        ledger.WaitRemaining("Bob", T0).Should().Be(TimeSpan.Zero);

        ledger.Submit(id, "Bob", SubmitArgs(id), T0);

        ledger.WaitRemaining("bob", T0.AddMinutes(1)).Should().Be(TimeSpan.FromMinutes(1));
        ledger.WaitRemaining("Bob", T0.AddMinutes(2)).Should().Be(TimeSpan.Zero);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Cancel_removes_an_unsent_report_and_does_not_start_the_wait()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        ledger.Cancel(id, "Alice").Should().BeNull();
        ledger.Cancel(id, "Bob")!.ReportId.Should().Be(id);
        ledger.Cancel(id, "Bob").Should().BeNull();
        ledger.WaitRemaining("Bob", T0).Should().Be(TimeSpan.Zero);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Cancel_after_submit_does_nothing()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        ledger.Submit(id, "Bob", SubmitArgs(id, 100), T0).Status.Should().Be(SubmitStatus.AwaitingPicture);
        ledger.Cancel(id, "Bob").Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Parts_join_in_index_order_whatever_order_they_arrive()
    {
        var ledger = NewLedger();
        var picture = Enumerable.Range(0, 40_000).Select(i => (byte)i).ToArray();
        var id = Open(ledger);
        ledger.Submit(id, "Bob", SubmitArgs(id, (uint)picture.Length), T0);

        ledger.AddPart(id, "Bob", 1, picture[32_768..], T0).Should().BeNull();
        var report = ledger.AddPart(id, "Bob", 0, picture[..32_768], T0);

        report.Should().NotBeNull();
        report!.AssemblePicture(BugReportProtocol.MAX_PICTURE_BYTES).Picture.Should().Equal(picture);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_duplicate_part_replaces_the_earlier_one()
    {
        var ledger = NewLedger();
        var picture = Enumerable.Range(0, 40_000).Select(i => (byte)(i + 1)).ToArray();
        var id = Open(ledger);
        ledger.Submit(id, "Bob", SubmitArgs(id, (uint)picture.Length), T0);

        ledger.AddPart(id, "Bob", 0, new byte[32_768], T0);
        ledger.AddPart(id, "Bob", 0, picture[..32_768], T0);
        var report = ledger.AddPart(id, "Bob", 1, picture[32_768..], T0);

        report!.AssemblePicture(BugReportProtocol.MAX_PICTURE_BYTES).Picture.Should().Equal(picture);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Parts_with_a_bad_index_size_or_owner_are_ignored()
    {
        var ledger = NewLedger();
        var id = Open(ledger);
        ledger.Submit(id, "Bob", SubmitArgs(id, 40_000), T0);

        ledger.AddPart(id, "Bob", 2, new byte[10], T0).Should().BeNull();
        ledger.AddPart(id, "Bob", 0, new byte[32_769], T0).Should().BeNull();
        ledger.AddPart(id, "Alice", 0, new byte[32_768], T0).Should().BeNull();
        ledger.AddPart(id, "Bob", 0, new byte[32_768], T0).Should().BeNull();
        ledger.AddPart(id, "Bob", 1, new byte[7_232], T0).Should().NotBeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Parts_before_submit_are_ignored()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        ledger.AddPart(id, "Bob", 0, new byte[10], T0).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task An_oversized_picture_completes_at_submit_without_a_picture()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        var outcome = ledger.Submit(id, "Bob", SubmitArgs(id, BugReportProtocol.MAX_PICTURE_BYTES + 1u), T0);

        outcome.Status.Should().Be(SubmitStatus.Complete);

        outcome.Report!.AssemblePicture(BugReportProtocol.MAX_PICTURE_BYTES)
               .Should()
               .Be(((byte[]?)null, "picture not saved (over the size limit)"));

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_wrong_total_length_is_noted()
    {
        var ledger = NewLedger();
        var id = Open(ledger);
        ledger.Submit(id, "Bob", SubmitArgs(id, 40_000), T0);

        ledger.AddPart(id, "Bob", 0, new byte[32_768], T0);
        var report = ledger.AddPart(id, "Bob", 1, new byte[100], T0);

        report!.AssemblePicture(BugReportProtocol.MAX_PICTURE_BYTES)
               .Should()
               .Be(((byte[]?)null, "picture not saved (length did not match)"));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sweep_finishes_a_report_whose_parts_stopped()
    {
        var ledger = NewLedger();
        var id = Open(ledger);
        ledger.Submit(id, "Bob", SubmitArgs(id, 40_000), T0);
        ledger.AddPart(id, "Bob", 0, new byte[32_768], T0.AddSeconds(5));

        ledger.Sweep(T0.AddSeconds(34), _ => false).ToFinish.Should().BeEmpty();

        var result = ledger.Sweep(T0.AddSeconds(35), _ => false);

        result.ToFinish
              .Should()
              .ContainSingle()
              .Which.AssemblePicture(BugReportProtocol.MAX_PICTURE_BYTES)
              .Note.Should()
              .Be("picture not saved (parts missing)");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sweep_discards_unsent_reports_that_expired_or_whose_player_left()
    {
        var ledger = NewLedger();
        var expired = Open(ledger, "Bob", T0);
        var gone = Open(ledger, "Alice", T0.AddMinutes(20));
        var staying = Open(ledger, "Carol", T0.AddMinutes(20));

        var result = ledger.Sweep(T0.AddMinutes(30), context => context == $"context-{gone}");

        result.ToDiscard.Select(report => report.ReportId).Should().BeEquivalentTo(new[] { expired, gone });
        ledger.Submit(staying, "Carol", SubmitArgs(staying), T0.AddMinutes(30)).Status.Should().Be(SubmitStatus.Complete);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Options_put_reports_under_staging_and_logs_beside_the_server()
    {
        var options = new BugReportOptions();

        options.UseBaseDirectory("staging-root");

        options.Directory.Should().Be(Path.Combine("staging-root", "Data", "Saved", "BugReports"));
        options.LogDirectory.Should().Be(Path.Combine(AppContext.BaseDirectory, "logs"));

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run (from `Chaos-Server/`): `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportLedgerTests/*"`
Expected: the build fails because `BugReportLedger` and `BugReportOptions` do not exist.

- [ ] **Step 3: Write `BugReportOptions`**

Create `Chaos/Services/BugReports/BugReportOptions.cs`:

```csharp
#region
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>
///     Settings for in-game bug reports, bound from <c>Options:BugReportOptions</c>. <see cref="Directory" /> is under
///     the staging directory like the other save folders. <see cref="LogDirectory" /> is next to the server executable,
///     where the NLog file target writes (<c>${basedir}\logs</c>).
/// </summary>
public sealed record BugReportOptions : IDirectoryBound
{
    public string Directory { get; set; } = Path.Combine("Data", "Saved", "BugReports");
    public string LogDirectory { get; set; } = "logs";
    public int WaitMinutes { get; set; } = 2;
    public int ReportLifetimeMinutes { get; set; } = 30;
    public int PictureGapSeconds { get; set; } = 30;
    public int MaxPictureBytes { get; set; } = BugReportProtocol.MAX_PICTURE_BYTES;
    public int MinDescriptionChars { get; set; } = BugReportProtocol.MIN_DESCRIPTION_CHARS;
    public int MaxDescriptionChars { get; set; } = BugReportProtocol.MAX_DESCRIPTION_CHARS;
    public int LogWindowMinutes { get; set; } = 15;
    public int LogLineCap { get; set; } = 2000;

    /// <inheritdoc />
    public void UseBaseDirectory(string baseDirectory)
    {
        Directory = Path.Combine(baseDirectory, Directory);
        LogDirectory = Path.Combine(AppContext.BaseDirectory, LogDirectory);
    }
}
```

`IDirectoryBound` is in `Chaos.Common.Abstractions` (`Chaos.Common.Abstractions/IDirectoryBound.cs`). If the namespace differs, use the one in that file.

- [ ] **Step 4: Write `BugReportLedger`**

Create `Chaos/Services/BugReports/BugReportLedger.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>What happened to a submit.</summary>
public enum SubmitStatus
{
    /// <summary>The report is recorded and waits for its picture parts.</summary>
    AwaitingPicture,

    /// <summary>The report is recorded and ready to be written. The outcome carries it.</summary>
    Complete,

    /// <summary>The report number is unknown, already used, or past its lifetime. A past-lifetime report is carried for cleanup.</summary>
    Expired,

    /// <summary>The report number belongs to another character. Ignore it silently.</summary>
    NotOwner,

    /// <summary>The category is not one of the eight. The report stays open.</summary>
    BadCategory,

    /// <summary>The trimmed description is outside the allowed length. The report stays open.</summary>
    BadDescription
}

public sealed record SubmitOutcome<TContext>(SubmitStatus Status, BugReportLedger<TContext>.OpenReport? Report)
    where TContext: class;

public sealed record SweepResult<TContext>(
    IReadOnlyList<BugReportLedger<TContext>.OpenReport> ToFinish,
    IReadOnlyList<BugReportLedger<TContext>.OpenReport> ToDiscard) where TContext: class;

/// <summary>
///     The in-memory state of in-game bug reports: which report numbers are open, who owns each one, the picture parts
///     received so far, and when each character last sent a report. It holds no files and sends nothing, so every rule
///     can be tested with plain values. A report leaves the ledger the moment it is complete, cancelled or swept, and
///     the caller then owns it. All methods are thread-safe.
/// </summary>
/// <typeparam name="TContext">What the caller keeps with each open report.</typeparam>
public sealed class BugReportLedger<TContext>(BugReportOptions options) where TContext: class
{
    private readonly Dictionary<string, DateTime> LastSubmitUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, OpenReport> Reports = [];
    private readonly Lock Sync = new();

    /// <summary>How long <paramref name="characterName" /> must still wait before opening another report.</summary>
    public TimeSpan WaitRemaining(string characterName, DateTime nowUtc)
    {
        using var scope = Sync.EnterScope();

        if (!LastSubmitUtc.TryGetValue(characterName, out var last))
            return TimeSpan.Zero;

        var remaining = last.AddMinutes(options.WaitMinutes) - nowUtc;

        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    ///     Opens a report with a new random non-zero number. <paramref name="createContext" /> receives that number. A
    ///     previous unsent report by the same character is removed and returned, so the caller can clean it up.
    /// </summary>
    public (uint ReportId, OpenReport? Replaced) Open(string characterName, Func<uint, TContext> createContext, DateTime nowUtc)
    {
        using var scope = Sync.EnterScope();

        var replaced = Reports.Values.FirstOrDefault(
            report => (report.Submit is null) && report.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase));

        if (replaced is not null)
            Reports.Remove(replaced.ReportId);

        uint reportId;

        do
            reportId = (uint)Random.Shared.NextInt64(1, uint.MaxValue);
        while (Reports.ContainsKey(reportId));

        Reports[reportId] = new OpenReport(reportId, characterName, nowUtc, createContext(reportId));

        return (reportId, replaced);
    }

    public SubmitOutcome<TContext> Submit(uint reportId, string characterName, BugReportInteractionArgs args, DateTime nowUtc)
    {
        using var scope = Sync.EnterScope();

        if (!Reports.TryGetValue(reportId, out var report) || (report.Submit is not null))
            return new SubmitOutcome<TContext>(SubmitStatus.Expired, null);

        if (!report.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase))
            return new SubmitOutcome<TContext>(SubmitStatus.NotOwner, null);

        if (IsExpired(report, nowUtc))
        {
            Reports.Remove(reportId);

            return new SubmitOutcome<TContext>(SubmitStatus.Expired, report);
        }

        if (!Enum.IsDefined(args.Category))
            return new SubmitOutcome<TContext>(SubmitStatus.BadCategory, null);

        var length = args.Description.Trim().Length;

        if ((length < options.MinDescriptionChars) || (length > options.MaxDescriptionChars))
            return new SubmitOutcome<TContext>(SubmitStatus.BadDescription, null);

        report.Submit = args;
        report.LastActivityUtc = nowUtc;
        LastSubmitUtc[characterName] = nowUtc;

        if ((args.PictureLength == 0) || (args.PictureLength > options.MaxPictureBytes))
        {
            Reports.Remove(reportId);

            return new SubmitOutcome<TContext>(SubmitStatus.Complete, report);
        }

        return new SubmitOutcome<TContext>(SubmitStatus.AwaitingPicture, report);
    }

    /// <summary>Stores one picture part. Returns the report, now owned by the caller, when the last expected part arrives.</summary>
    public OpenReport? AddPart(uint reportId, string characterName, byte partIndex, byte[] data, DateTime nowUtc)
    {
        using var scope = Sync.EnterScope();

        if (!Reports.TryGetValue(reportId, out var report) || (report.Submit is null))
            return null;

        if (!report.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase))
            return null;

        if ((partIndex >= report.ExpectedParts) || (data.Length == 0) || (data.Length > BugReportProtocol.PART_SIZE))
            return null;

        report.Parts[partIndex] = data;
        report.LastActivityUtc = nowUtc;

        if (report.Parts.Count < report.ExpectedParts)
            return null;

        Reports.Remove(reportId);

        return report;
    }

    /// <summary>Removes an unsent report owned by <paramref name="characterName" />. Returns it for cleanup, or null.</summary>
    public OpenReport? Cancel(uint reportId, string characterName)
    {
        using var scope = Sync.EnterScope();

        if (!Reports.TryGetValue(reportId, out var report) || (report.Submit is not null))
            return null;

        if (!report.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase))
            return null;

        Reports.Remove(reportId);

        return report;
    }

    /// <summary>
    ///     Removes submitted reports whose picture parts stopped for the picture gap (to finish without a picture), and
    ///     unsent reports past their lifetime or whose player <paramref name="isGone" /> (to discard).
    /// </summary>
    public SweepResult<TContext> Sweep(DateTime nowUtc, Func<TContext, bool> isGone)
    {
        using var scope = Sync.EnterScope();

        var finish = new List<OpenReport>();
        var discard = new List<OpenReport>();

        foreach (var report in Reports.Values.ToList())
            if (report.Submit is not null)
            {
                if ((nowUtc - report.LastActivityUtc) >= TimeSpan.FromSeconds(options.PictureGapSeconds))
                {
                    Reports.Remove(report.ReportId);
                    finish.Add(report);
                }
            } else if (IsExpired(report, nowUtc) || isGone(report.Context))
            {
                Reports.Remove(report.ReportId);
                discard.Add(report);
            }

        return new SweepResult<TContext>(finish, discard);
    }

    private bool IsExpired(OpenReport report, DateTime nowUtc)
        => (nowUtc - report.OpenedUtc) >= TimeSpan.FromMinutes(options.ReportLifetimeMinutes);

    /// <summary>One report between Open and the moment it is written or thrown away.</summary>
    public sealed class OpenReport
    {
        internal OpenReport(uint reportId, string characterName, DateTime openedUtc, TContext context)
        {
            ReportId = reportId;
            CharacterName = characterName;
            OpenedUtc = openedUtc;
            Context = context;
            LastActivityUtc = openedUtc;
        }

        public uint ReportId { get; }
        public string CharacterName { get; }
        public DateTime OpenedUtc { get; }
        public TContext Context { get; }
        public BugReportInteractionArgs? Submit { get; internal set; }
        public DateTime LastActivityUtc { get; internal set; }
        internal SortedDictionary<byte, byte[]> Parts { get; } = [];
        public int ExpectedParts => Submit is null ? 0 : BugReportProtocol.PartCount(Submit.PictureLength);

        /// <summary>
        ///     Joins the picture parts in index order. Returns the bytes when a complete picture of the declared size
        ///     arrived. Otherwise returns no bytes and, when a picture was expected, a note saying why it was not kept.
        /// </summary>
        public (byte[]? Picture, string? Note) AssemblePicture(int maxPictureBytes)
        {
            if ((Submit is null) || (Submit.PictureLength == 0))
                return (null, null);

            if (Submit.PictureLength > maxPictureBytes)
                return (null, "picture not saved (over the size limit)");

            if (Parts.Count < ExpectedParts)
                return (null, "picture not saved (parts missing)");

            var bytes = Parts.Values.SelectMany(part => part).ToArray();

            return bytes.Length == Submit.PictureLength ? (bytes, null) : (null, "picture not saved (length did not match)");
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run (from `Chaos-Server/`): `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportLedgerTests/*"`
Expected: all pass.

```json:metadata
{"files": ["Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportOptions.cs", "Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportLedger.cs", "Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportLedgerTests.cs"], "verifyCommand": "cd Chaos.Client/Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/BugReportLedgerTests/*\"", "acceptanceCriteria": ["report number single-use, expires after lifetime and cancel", "other character gets NotOwner", "bad category or description keeps report open", "wait starts at submit, 2 minutes, case-insensitive", "parts join in order, duplicate replaces, bad index/size/owner ignored", "oversized picture completes at submit with note", "wrong total length noted", "sweep finishes after 30s gap with parts-missing note", "sweep discards expired or gone unsent reports", "options resolve report and log directories"], "modelTier": "mechanical"}
```

---

### Task 5: Report service, message handler and registration on the server

**Goal:** Connect the ledger, store, snapshot and log reader into `BugReportService`. Add the 5-second sweep, the `BugReportInteraction` handler, the `SendBugReportOpen` send method, service registration and the appsettings block.

**Files:**
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportService.cs`
- Create: `Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportSweepService.cs`
- Modify: `Chaos.Client/Chaos-Server/Chaos/Networking/Abstractions/IChaosWorldClient.cs` (after `SendBeautyShopRejected`)
- Modify: `Chaos.Client/Chaos-Server/Chaos/Networking/ChaosWorldClient.cs` (after `SendBeautyShopClose`)
- Modify: `Chaos.Client/Chaos-Server/Chaos/Services/Servers/WorldServer.cs` (field, constructor, handler, `IndexHandlers` registration)
- Modify: `Chaos.Client/Chaos-Server/Chaos/Extensions/ServiceCollectionExtensions.cs` (`AddBugReports` after `AddMarket`)
- Modify: `Chaos.Client/Chaos-Server/Chaos/Program.cs` (call `AddBugReports` after `AddMarket`)
- Modify: `Chaos.Client/Chaos-Server/Chaos/appsettings.json` (`BugReportOptions` block after `MarketOptions`)
- Test: `Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportServiceTests.cs`

**Acceptance Criteria:**
- [ ] `Open` sends `BugReportOpen` with a non-zero number, and creates `_pending/<number>/` with `world.json` and `character/`
- [ ] A submit without a picture writes `<Directory>/map-warp/<folder>/report.md` with `screenshot: false`, plus `world.json`, `client.json`, `server-log.jsonl` and `character/aisling.json`. The holding folder is gone afterwards
- [ ] A 40,000-byte PNG sent in two parts is saved as `screenshot.png` byte-for-byte, and `report.md` says `screenshot: true`
- [ ] `Cancel` deletes the holding folder
- [ ] `ClientOpCode.BugReportInteraction` is routed to the service, and `Chaos.slnx` builds with the service registered and the options bound from `Options:BugReportOptions`

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportServiceTests/*"` then `dotnet build Chaos.slnx` (from `Chaos-Server/`) → all pass, build succeeds

**Steps:**

- [ ] **Step 1: Add the send method to the world client**

In `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, insert after `void SendBeautyShopRejected(BeautyShopRejectReason reason);`:

```csharp

    /// <summary>Opens the in-game bug report window. The client's submit must carry <paramref name="reportId" />.</summary>
    void SendBugReportOpen(uint reportId);
```

In `Chaos/Networking/ChaosWorldClient.cs`, insert after the `SendBeautyShopClose` method:

```csharp

    /// <inheritdoc />
    public void SendBugReportOpen(uint reportId) => Send(new BugReportOpenArgs { ReportId = reportId });
```

- [ ] **Step 2: Write the failing service tests**

Create `Tests/Chaos.Tests/BugReports/BugReportServiceTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.Networking.Entities.Client;
using Chaos.Services.BugReports;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Moq;

namespace Chaos.Tests.BugReports;

public class BugReportServiceTests
{
    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "bugreport-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return path;
    }

    private static (BugReportService Service, Aisling Aisling, Func<uint> ReportId, string Root) Setup()
    {
        var root = TempDirectory();

        var options = new BugReportOptions
        {
            Directory = root,
            LogDirectory = TempDirectory()
        };

        var snapshotWriter = new Mock<IAislingSnapshotWriter>();

        snapshotWriter.Setup(writer => writer.WriteSnapshotAsync(It.IsAny<Aisling>(), It.IsAny<string>()))
                      .Returns((Aisling _, string directory) => File.WriteAllTextAsync(Path.Combine(directory, "aisling.json"), "{}"));

        var service = new BugReportService(
            new BugReportStore(root),
            snapshotWriter.Object,
            Microsoft.Extensions.Options.Options.Create(options),
            MockLogger.Create<BugReportService>().Object);

        var aisling = MockAisling.Create(name: "Bob");
        var client = Mock.Get(aisling.Client);
        client.SetupGet(c => c.Connected).Returns(true);

        uint reportId = 0;

        client.Setup(c => c.SendBugReportOpen(It.IsAny<uint>()))
              .Callback<uint>(id => reportId = id);

        return (service, aisling, () => reportId, root);
    }

    private static BugReportInteractionArgs Submit(uint reportId, uint pictureLength = 0)
        => new()
        {
            Type = BugReportInteractionType.Submit,
            ReportId = reportId,
            Category = BugReportCategory.MapWarp,
            Description = "Stuck in the wall at the inn",
            PictureLength = pictureLength,
            ClientBuild = "0.1.0",
            OsDescription = "Windows 11",
            FramesPerSecond = 60,
            PingMs = 45,
            HudStyle = 1,
            WindowWidth = 1280,
            WindowHeight = 960
        };

    private static BugReportInteractionArgs Part(uint reportId, byte index, byte[] data)
        => new()
        {
            Type = BugReportInteractionType.PicturePart,
            ReportId = reportId,
            PartIndex = index,
            Data = data
        };

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100; i++)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException("The condition was never met.");
    }

    private static string ReadShared(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        } catch (IOException)
        {
            return string.Empty;
        }
    }

    private static async Task<string> WaitForReport(string root, string category)
    {
        string? found = null;

        await WaitUntil(
            () =>
            {
                var folder = Path.Combine(root, category);

                found = Directory.Exists(folder)
                    ? Directory.EnumerateFiles(folder, "report.md", SearchOption.AllDirectories).FirstOrDefault()
                    : null;

                return (found is not null) && ReadShared(found).EndsWith("<!-- sync:preserve -->\n", StringComparison.Ordinal);
            });

        return found!;
    }

    [Test]
    public async Task Open_sends_the_number_and_fills_the_holding_folder()
    {
        (var service, var aisling, var reportId, var root) = Setup();

        service.Open(aisling);

        reportId().Should().NotBe(0u);
        var pending = Path.Combine(root, "_pending", reportId().ToString());

        await WaitUntil(
            () => File.Exists(Path.Combine(pending, "world.json")) && File.Exists(Path.Combine(pending, "character", "aisling.json")));
    }

    [Test]
    public async Task A_report_without_a_picture_lands_in_its_category_folder()
    {
        (var service, var aisling, var reportId, var root) = Setup();
        service.Open(aisling);

        service.Submit(aisling, Submit(reportId()));

        var report = await WaitForReport(root, "map-warp");
        var folder = Path.GetDirectoryName(report)!;

        ReadShared(report).Should().Contain("screenshot: false\n");
        File.Exists(Path.Combine(folder, "world.json")).Should().BeTrue();
        File.Exists(Path.Combine(folder, "client.json")).Should().BeTrue();
        File.Exists(Path.Combine(folder, "server-log.jsonl")).Should().BeTrue();
        File.Exists(Path.Combine(folder, "character", "aisling.json")).Should().BeTrue();
        File.Exists(Path.Combine(folder, "screenshot.png")).Should().BeFalse();
        Directory.Exists(Path.Combine(root, "_pending", reportId().ToString())).Should().BeFalse();
    }

    [Test]
    public async Task A_picture_sent_in_parts_is_saved_as_screenshot_png()
    {
        (var service, var aisling, var reportId, var root) = Setup();
        var picture = new byte[40_000];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(picture, 0);
        service.Open(aisling);

        service.Submit(aisling, Submit(reportId(), (uint)picture.Length));
        service.AddPicturePart(aisling, Part(reportId(), 1, picture[32_768..]));
        service.AddPicturePart(aisling, Part(reportId(), 0, picture[..32_768]));

        var report = await WaitForReport(root, "map-warp");

        (await File.ReadAllBytesAsync(Path.Combine(Path.GetDirectoryName(report)!, "screenshot.png"))).Should().Equal(picture);
        ReadShared(report).Should().Contain("screenshot: true\n");
    }

    [Test]
    public async Task Cancel_deletes_the_holding_folder()
    {
        (var service, var aisling, var reportId, var root) = Setup();
        service.Open(aisling);
        var pending = Path.Combine(root, "_pending", reportId().ToString());
        await WaitUntil(() => File.Exists(Path.Combine(pending, "world.json")));

        service.Cancel(aisling, reportId());

        await WaitUntil(() => !Directory.Exists(pending));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run (from `Chaos-Server/`): `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportServiceTests/*"`
Expected: the build fails because `BugReportService` does not exist.

- [ ] **Step 4: Write `BugReportService`**

Create `Chaos/Services/BugReports/BugReportService.cs`:

```csharp
#region
using System.Reflection;
using Chaos.Models.World;
using Chaos.Networking.Entities.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>What the server keeps with one open report: who opened it, what was around them, and the holding-folder writes.</summary>
public sealed record ReportContext(Aisling Aisling, WorldSnapshot World, Task PendingWrites);

/// <summary>
///     In-game bug reports. <see cref="Open" /> runs inside the Terminus dialog on the world thread: it copies the
///     character and the world state into a holding folder and tells the client to open its window. Submit, picture
///     parts and cancel arrive through <c>ClientOpCode.BugReportInteraction</c>. Finished reports are written off the
///     game loop, so a large log file or a slow disk never stalls the world.
/// </summary>
public sealed class BugReportService
{
    private const string NOT_SAVED = "Your report could not be saved. Please tell staff on Discord.";

    private static readonly string ServerVersion
        = typeof(BugReportService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
          ?? "unknown";

    private readonly BugReportLedger<ReportContext> Ledger;
    private readonly ILogger<BugReportService> Logger;
    private readonly BugReportOptions Options;
    private readonly IAislingSnapshotWriter SnapshotWriter;
    private readonly BugReportStore Store;

    public BugReportService(
        BugReportStore store,
        IAislingSnapshotWriter snapshotWriter,
        IOptions<BugReportOptions> options,
        ILogger<BugReportService> logger)
    {
        Store = store;
        SnapshotWriter = snapshotWriter;
        Options = options.Value;
        Logger = logger;
        Ledger = new BugReportLedger<ReportContext>(Options);
    }

    public TimeSpan WaitRemaining(Aisling aisling) => Ledger.WaitRemaining(aisling.Name, DateTime.UtcNow);

    /// <summary>Captures the character and world state, then opens the client's report window. Call on the world thread.</summary>
    public void Open(Aisling aisling)
    {
        var nowUtc = DateTime.UtcNow;
        var world = WorldSnapshot.Capture(aisling, nowUtc, ServerVersion);

        (var reportId, var replaced) = Ledger.Open(
            aisling.Name,
            id => new ReportContext(aisling, world, StartPendingWrites(id, aisling, world)),
            nowUtc);

        if (replaced is not null)
            Discard(replaced);

        aisling.Client.SendBugReportOpen(reportId);
    }

    public void Submit(Aisling aisling, BugReportInteractionArgs args)
    {
        var outcome = Ledger.Submit(args.ReportId, aisling.Name, args, DateTime.UtcNow);

        switch (outcome.Status)
        {
            case SubmitStatus.Complete:
                StartFinish(outcome.Report!);

                break;
            case SubmitStatus.Expired:
                if (outcome.Report is not null)
                    Discard(outcome.Report);

                aisling.SendOrangeBarMessage("This report has expired. Please open a new one from Terminus.");

                break;
            case SubmitStatus.BadCategory:
                aisling.SendOrangeBarMessage("Please pick a category for your report.");

                break;
            case SubmitStatus.BadDescription:
                aisling.SendOrangeBarMessage(
                    $"Please describe the problem in {Options.MinDescriptionChars} to {Options.MaxDescriptionChars} characters.");

                break;
            case SubmitStatus.AwaitingPicture:
            case SubmitStatus.NotOwner:
                break;
        }
    }

    public void AddPicturePart(Aisling aisling, BugReportInteractionArgs args)
    {
        var report = Ledger.AddPart(args.ReportId, aisling.Name, args.PartIndex, args.Data, DateTime.UtcNow);

        if (report is not null)
            StartFinish(report);
    }

    public void Cancel(Aisling aisling, uint reportId)
    {
        var report = Ledger.Cancel(reportId, aisling.Name);

        if (report is not null)
            Discard(report);
    }

    /// <summary>Finishes reports whose picture stopped arriving, and discards unsent reports that expired or whose player left.</summary>
    public void Sweep(DateTime nowUtc)
    {
        var result = Ledger.Sweep(nowUtc, context => !context.Aisling.Client.Connected);

        foreach (var report in result.ToFinish)
            StartFinish(report);

        foreach (var report in result.ToDiscard)
            Discard(report);
    }

    private Task StartPendingWrites(uint reportId, Aisling aisling, WorldSnapshot world)
    {
        try
        {
            var folder = Store.CreatePending(reportId);

            //WriteSnapshotAsync maps the character to its save schemas before its first await, so the copy is taken
            //here, on the world thread running the Terminus dialog, before anything can change the character
            var character = SnapshotWriter.WriteSnapshotAsync(aisling, Path.Combine(folder, "character"));
            var worldFile = BugReportStore.WriteWorldAsync(folder, world);

            return Task.WhenAll(character, worldFile);
        } catch (Exception e)
        {
            return Task.FromException(e);
        }
    }

    private void Discard(BugReportLedger<ReportContext>.OpenReport report)
        => _ = Task.Run(
            async () =>
            {
                try
                {
                    await report.Context.PendingWrites;
                } catch
                {
                    //the holding folder is removed whether or not its writes worked
                }

                try
                {
                    Store.DeletePending(report.ReportId);
                } catch (Exception e)
                {
                    Logger.LogError(e, "Failed to delete pending bug report {@ReportId}", report.ReportId);
                }
            });

    private void StartFinish(BugReportLedger<ReportContext>.OpenReport report)
        => _ = Task.Run(
            async () =>
            {
                try
                {
                    await FinishAsync(report);
                } catch (Exception e)
                {
                    Logger.LogError(e, "Failed to finish bug report {@ReportId}", report.ReportId);
                    Reply(report.Context.Aisling, NOT_SAVED);
                }
            });

    private async Task FinishAsync(BugReportLedger<ReportContext>.OpenReport report)
    {
        var context = report.Context;
        var aisling = context.Aisling;
        var submit = report.Submit!;
        var notes = new List<string>();

        try
        {
            await context.PendingWrites;
        } catch (Exception e)
        {
            notes.Add("character copy or world state not saved");
            Logger.LogError(e, "Failed to write the snapshot for bug report {@ReportId} from {@AislingName}", report.ReportId, aisling.Name);
        }

        (var picture, var pictureNote) = report.AssemblePicture(Options.MaxPictureBytes);

        if ((picture is not null) && !BugReportStore.LooksLikePng(picture))
        {
            picture = null;
            pictureNote = "picture not saved (not a PNG)";
        }

        if (pictureNote is not null)
            notes.Add(pictureNote);

        IReadOnlyList<string> logLines = [];

        try
        {
            logLines = ServerLogReader.Read(
                Options.LogDirectory,
                aisling.Name,
                DateTime.Now.AddMinutes(-Options.LogWindowMinutes),
                Options.LogLineCap);
        } catch (Exception e)
        {
            notes.Add("server log lines not read");
            Logger.LogError(e, "Failed to read log lines for bug report {@ReportId}", report.ReportId);
        }

        var reportedAt = DateTimeOffset.Now;
        string folder;

        try
        {
            folder = Store.MovePendingToCategory(
                report.ReportId,
                submit.Category,
                BugReportStore.FolderName(reportedAt.LocalDateTime, aisling.Name));
        } catch (Exception e)
        {
            Logger.LogError(e, "Failed to move bug report {@ReportId} into its category folder", report.ReportId);
            Reply(aisling, NOT_SAVED);

            return;
        }

        var client = ClientDetails.From(submit);

        var screenshotSaved = picture is not null
                              && await TryWriteAsync(() => BugReportStore.WriteScreenshotAsync(folder, picture!), "screenshot.png", notes);

        await TryWriteAsync(() => BugReportStore.WriteClientAsync(folder, client), "client.json", notes);
        await TryWriteAsync(() => BugReportStore.WriteLogAsync(folder, logLines), "server-log.jsonl", notes);

        var markdown = BugReportMarkdown.Render(
            new BugReportMarkdown.Input
            {
                Id = Path.GetFileName(folder),
                Category = submit.Category,
                Description = submit.Description,
                World = context.World,
                ReportedAt = reportedAt,
                Client = client,
                HasScreenshot = screenshotSaved,
                LogLineCount = logLines.Count,
                Notes = notes
            });

        try
        {
            await BugReportStore.WriteReportAsync(folder, markdown);
        } catch (Exception e)
        {
            Logger.LogError(e, "Failed to write report.md for bug report {@ReportId}", report.ReportId);
            Reply(aisling, NOT_SAVED);

            return;
        }

        Logger.LogInformation("{@AislingName} sent bug report {@ReportFolder}", aisling.Name, folder);
        Reply(aisling, "Thank you, your report was sent.");
    }

    private async Task<bool> TryWriteAsync(Func<Task> write, string fileName, List<string> notes)
    {
        try
        {
            await write();

            return true;
        } catch (Exception e)
        {
            notes.Add($"{fileName} not written");
            Logger.LogError(e, "Failed to write {@FileName} for a bug report", fileName);

            return false;
        }
    }

    private static void Reply(Aisling aisling, string message)
    {
        if (aisling.Client.Connected)
            aisling.SendOrangeBarMessage(message);
    }
}
```

- [ ] **Step 5: Write the sweep**

Create `Chaos/Services/BugReports/BugReportSweepService.cs`:

```csharp
#region
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
#endregion

namespace Chaos.Services.BugReports;

/// <summary>
///     Clears leftover holding folders at startup, then every 5 seconds finishes reports whose picture stopped arriving
///     and discards unsent reports that expired or whose player left.
/// </summary>
public sealed class BugReportSweepService(BugReportService service, BugReportStore store, ILogger<BugReportSweepService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            store.ClearPending();
        } catch (Exception e)
        {
            logger.LogError(e, "Failed to clear pending bug reports at startup");
        }

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            try
            {
                service.Sweep(DateTime.UtcNow);
            } catch (Exception e)
            {
                logger.LogError(e, "Bug report sweep failed");
            }
    }
}
```

- [ ] **Step 6: Route the message in `WorldServer`**

In `Chaos/Services/Servers/WorldServer.cs`:
- Add `using Chaos.Services.BugReports;` to the usings.
- Add the field after `private readonly MarketService MarketService;`:

```csharp
    private readonly BugReportService BugReportService;
```

- Add the constructor parameter `BugReportService bugReportService` after `IStorage<GuildHouseState> guildHouseStateStorage`, and `BugReportService = bugReportService;` in the constructor body next to `MarketService = marketService;`.
- Insert this handler after `OnBeautyShopInteraction` (Serena `insert_after_symbol` on `WorldServer/OnBeautyShopInteraction`):

```csharp

    /// <summary>
    ///     Routes a bug report submit, picture part or cancel to <see cref="BugReportService" />. The report number is
    ///     checked against the sending character there, so a client cannot act on another player's report.
    /// </summary>
    public ValueTask OnBugReportInteraction(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<BugReportInteractionArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnBugReportInteraction);

        ValueTask InnerOnBugReportInteraction(IChaosWorldClient localClient, BugReportInteractionArgs localArgs)
        {
            if (!localClient.Connected)
                return default;

            var aisling = localClient.Aisling;

            switch (localArgs.Type)
            {
                case BugReportInteractionType.Submit:
                    BugReportService.Submit(aisling, localArgs);

                    break;
                case BugReportInteractionType.PicturePart:
                    BugReportService.AddPicturePart(aisling, localArgs);

                    break;
                case BugReportInteractionType.Cancel:
                    BugReportService.Cancel(aisling, localArgs.ReportId);

                    break;
            }

            return default;
        }
    }
```

- In `IndexHandlers`, after `ClientHandlers[(byte)ClientOpCode.Vote] = OnVote;`, add:

```csharp
        ClientHandlers[(byte)ClientOpCode.BugReportInteraction] = OnBugReportInteraction;
```

- [ ] **Step 7: Register the services**

In `Chaos/Extensions/ServiceCollectionExtensions.cs`, insert after the `AddMarket` method:

```csharp

    /// <summary>
    ///     Registers in-game bug reports: options, the report folders, the service, and the sweep that expires reports.
    /// </summary>
    public static void AddBugReports(this IServiceCollection services)
    {
        services.AddOptionsFromConfig<BugReportOptions>(ConfigKeys.Options.Key);
        services.ConfigureOptions<DirectoryBoundOptionsConfigurer<BugReportOptions>>();

        services.AddSingleton<IAislingSnapshotWriter>(
            provider => (AislingStore)provider.GetRequiredService<IAsyncStore<Aisling>>());

        services.AddSingleton(
            provider => new BugReportStore(provider.GetRequiredService<IOptions<BugReportOptions>>().Value.Directory));

        services.AddSingleton<BugReportService>();
        services.AddHostedService<BugReportSweepService>();
    }
```

Add any missing usings: `Chaos.Services.BugReports`, `Chaos.Services.Storage` (`AislingStore`), `Chaos.Storage.Abstractions` (`IAsyncStore`), `Chaos.Common.Configuration` (`DirectoryBoundOptionsConfigurer`), `Microsoft.Extensions.Options`. Most are already in the file for the aisling store registration.

In `Chaos/Program.cs`, after `builder.Services.AddMarket();`, add:

```csharp
    builder.Services.AddBugReports();
```

Match the indentation of the `AddMarket` line.

- [ ] **Step 8: Add the settings block**

In `Chaos/appsettings.json`, replace:

```json
      "TaxRate": 0.10,
      "SaveIntervalMins": 5
    }
```

with:

```json
      "TaxRate": 0.10,
      "SaveIntervalMins": 5
    },
    "BugReportOptions": {
      "Directory": "Data\\Saved\\BugReports",
      "LogDirectory": "logs",
      "WaitMinutes": 2,
      "ReportLifetimeMinutes": 30,
      "PictureGapSeconds": 30,
      "MaxPictureBytes": 524288,
      "MinDescriptionChars": 10,
      "MaxDescriptionChars": 1000,
      "LogWindowMinutes": 15,
      "LogLineCap": 2000
    }
```

Leave the file's existing uncommitted `StagingDirectory` change alone. Task 11 keeps it out of the commit.

- [ ] **Step 9: Run the tests and the build**

Run (from `Chaos-Server/`):
`dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BugReportServiceTests/*"`
`dotnet build Chaos.slnx`
Expected: all tests pass and the build succeeds. No test constructs `WorldServer` directly (checked during planning), so the new constructor parameter breaks no test.

```json:metadata
{"files": ["Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportService.cs", "Chaos.Client/Chaos-Server/Chaos/Services/BugReports/BugReportSweepService.cs", "Chaos.Client/Chaos-Server/Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos.Client/Chaos-Server/Chaos/Networking/ChaosWorldClient.cs", "Chaos.Client/Chaos-Server/Chaos/Services/Servers/WorldServer.cs", "Chaos.Client/Chaos-Server/Chaos/Extensions/ServiceCollectionExtensions.cs", "Chaos.Client/Chaos-Server/Chaos/Program.cs", "Chaos.Client/Chaos-Server/Chaos/appsettings.json", "Chaos.Client/Chaos-Server/Tests/Chaos.Tests/BugReports/BugReportServiceTests.cs"], "verifyCommand": "cd Chaos.Client/Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/BugReportServiceTests/*\" && dotnet build Chaos.slnx", "acceptanceCriteria": ["Open sends a non-zero number and fills _pending/<number>/", "submit without picture writes the full category folder with screenshot: false and removes the holding folder", "two-part PNG saved byte-for-byte with screenshot: true", "Cancel deletes the holding folder", "BugReportInteraction routed, service registered, options bound, Chaos.slnx builds"], "modelTier": "standard"}
```

---

### Task 6: Terminus entry and dialog data

**Goal:** Add "Report a bug" to the Terminus menu. The option checks the wait time, closes the dialog and opens the report. Remove the dead Discord report flow.

**Files:**
- Modify (rewrite): `Chaos.Client/Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusBugReportScript.cs`
- Create: `Unora/Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_bugreport.json`
- Modify: `Unora/Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_initial.json`
- Delete: `Unora/Data/Configuration/Templates/Dialogs/Temauir/terminus/Report System/` (7 files)

**Acceptance Criteria:**
- [ ] `TerminusBugReportScript` has no Discord code and takes `BugReportService` by constructor injection
- [ ] `terminus_initial.json` lists `terminusBugReport` in `scriptKeys`, and the script adds a "Report a bug" option leading to `terminus_bugreport`
- [ ] `terminus_bugreport` replies "You sent a report recently. Please wait N more minute(s)." during the wait. Otherwise it closes the dialog and calls `BugReportService.Open`
- [ ] Both JSON files parse, and the `Report System` folder is gone
- [ ] `Chaos.slnx` builds

**Verify:** `dotnet build Chaos.slnx` (from `Chaos-Server/`) → succeeds. `python -c "import json; [json.load(open(p, encoding='utf-8-sig')) for p in ['Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_bugreport.json', 'Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_initial.json']]; print('ok')"` (from `Unora/`) → `ok`

**Steps:**

- [ ] **Step 1: Rewrite the script**

Replace the whole of `Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusBugReportScript.cs` with:

```csharp
#region
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Services.BugReports;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.Generic;

/// <summary>
///     Adds "Report a bug" to the Terminus menu (F1). Choosing it checks the per-character wait, then closes the dialog
///     and asks <see cref="BugReportService" /> to open the client's report window. The service captures the character
///     here, on the world thread, before anything can change it.
/// </summary>
public class TerminusBugReportScript(Dialog subject, BugReportService bugReports) : DialogScriptBase(subject)
{
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "terminus_initial":
            {
                var option = new DialogOption
                {
                    DialogKey = "terminus_bugreport",
                    OptionText = "Report a bug"
                };

                if (!Subject.HasOption(option.OptionText))
                    Subject.Options.Add(option);

                break;
            }
            case "terminus_bugreport":
            {
                var wait = bugReports.WaitRemaining(source);

                if (wait > TimeSpan.Zero)
                {
                    var minutes = (int)Math.Ceiling(wait.TotalMinutes);
                    Subject.Reply(source, $"You sent a report recently. Please wait {minutes} more minute{(minutes == 1 ? "" : "s")}.");

                    return;
                }

                //close first: the client hides the dialog on BugReportOpen anyway, but the server's dialog state must end here
                Subject.Close(source);
                bugReports.Open(source);

                break;
            }
        }
    }
}
```

`Dialog.Display` stops as soon as `OnDisplaying` closes the dialog, because `Close` removes the active dialog. So the `terminus_bugreport` text is never shown.

- [ ] **Step 2: Add the dialog file**

Create `Unora/Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_bugreport.json`:

```json
{
  "options": [],
  "scriptKeys": [
    "terminusBugReport"
  ],
  "scriptVars": {},
  "templateKey": "terminus_bugreport",
  "text": "Opening the report window...",
  "type": "Normal"
}
```

- [ ] **Step 3: Hook the script into the Terminus menu**

In `Unora/Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_initial.json`, replace:

```
  "terminusactivebounties"
  ],
```

with:

```
  "terminusactivebounties",
  "terminusBugReport"
  ],
```

- [ ] **Step 4: Remove the old report dialogs**

Run (from `Unora/`): `git rm -r -q "Data/Configuration/Templates/Dialogs/Temauir/terminus/Report System"`
Expected: the folder and its 7 files are gone, and the deletion is staged.

- [ ] **Step 5: Verify**

Run (from `Chaos-Server/`): `dotnet build Chaos.slnx` → `Build succeeded`.
Run (from `Unora/`): the `python -c` command in **Verify** → `ok`.
Run (from `Unora/`): `grep -rn "sendReport" Data/Configuration/Templates/Dialogs` → no output. Nothing may still point at the deleted dialogs.

```json:metadata
{"files": ["Chaos.Client/Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusBugReportScript.cs", "Unora/Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_bugreport.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_initial.json"], "verifyCommand": "cd Chaos.Client/Chaos-Server && dotnet build Chaos.slnx", "acceptanceCriteria": ["script has no Discord code and injects BugReportService", "terminus_initial lists terminusBugReport and script adds Report a bug option", "terminus_bugreport replies with wait or closes and opens", "both JSON files parse and Report System folder is gone", "Chaos.slnx builds"], "modelTier": "mechanical"}
```

---

### Task 7: Client capture, messages and upload helpers

**Goal:** Give the client the network event and send methods, an in-memory frame capture with a 110×83 preview, the pure upload helpers, and a frame rate that is counted even while the debug overlay is hidden.

**Files:**
- Modify: `Chaos.Client/Chaos.Client.Networking/Definitions/Delegates.cs`
- Modify: `Chaos.Client/Chaos.Client.Networking/ConnectionManager.cs`
- Create: `Chaos.Client/Chaos.Client/Models/CapturedFrame.cs`
- Create: `Chaos.Client/Chaos.Client/Systems/BugReportUpload.cs`
- Modify: `Chaos.Client/Chaos.Client/ChaosGame.cs` (`RequestCapture`, `CaptureFrame`; split PNG encoding out of `SaveScreenshot`)
- Modify: `Chaos.Client/Chaos.Client/Controls/Generic/DebugOverlay.cs`
- Test: `Chaos.Client/Tests/Chaos.Client.Tests/BugReportUploadTests.cs`

**Acceptance Criteria:**
- [ ] `SplitPicture` makes parts of at most 32,768 bytes that join back to the original, including for an exact multiple and an empty picture
- [ ] `CanSend` is true only with a category and 10 to 1,000 trimmed characters
- [ ] `Clip` limits a string to 64 characters
- [ ] `ConnectionManager` raises `OnBugReportOpen` for `ServerOpCode.BugReportOpen`, and has `SendBugReportSubmit`, `SendBugReportPicturePart` and `SendBugReportCancel`
- [ ] `ChaosGame.RequestCapture` hands the next drawn frame to its callback as PNG bytes plus a 110×83 thumbnail, and writes nothing to disk
- [ ] F12 still saves `lod###.png`
- [ ] `DebugOverlay.FramesPerSecond` updates while the overlay is hidden
- [ ] `Chaos.Client.slnx` builds

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/BugReportUploadTests/*"` then `dotnet build Chaos.Client.slnx` (from `Chaos.Client/`) → all pass, build succeeds

**Steps:**

- [ ] **Step 1: Write the failing test**

Create `Tests/Chaos.Client.Tests/BugReportUploadTests.cs`:

```csharp
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class BugReportUploadTests
{
    [Test]
    public void AnExactMultipleSplitsIntoFullParts()
    {
        var picture = Enumerable.Range(0, 65_536).Select(i => (byte)i).ToArray();

        var parts = BugReportUpload.SplitPicture(picture);

        parts.Should().HaveCount(2);
        parts.Should().OnlyContain(part => part.Length == BugReportProtocol.PART_SIZE);
        parts.SelectMany(part => part).Should().Equal(picture);
    }

    [Test]
    public void ARemainderGoesInTheLastPart()
    {
        var picture = Enumerable.Range(0, 70_000).Select(i => (byte)(i * 7)).ToArray();

        var parts = BugReportUpload.SplitPicture(picture);

        parts.Select(part => part.Length).Should().Equal(32_768, 32_768, 4_464);
        parts.SelectMany(part => part).Should().Equal(picture);
    }

    [Test]
    public void AnEmptyPictureHasNoParts() => BugReportUpload.SplitPicture([]).Should().BeEmpty();

    [Test]
    public void SendNeedsACategory() => BugReportUpload.CanSend(null, "0123456789").Should().BeFalse();

    [Test]
    public void SendNeedsTenTrimmedCharacters()
    {
        BugReportUpload.CanSend(BugReportCategory.Item, "0123456789").Should().BeTrue();
        BugReportUpload.CanSend(BugReportCategory.Item, "   012345678   ").Should().BeFalse();
    }

    [Test]
    public void SendAllowsAtMostAThousandCharacters()
    {
        BugReportUpload.CanSend(BugReportCategory.Item, new string('a', 1000)).Should().BeTrue();
        BugReportUpload.CanSend(BugReportCategory.Item, new string('a', 1001)).Should().BeFalse();
    }

    [Test]
    public void ClipLimitsDetailsToSixtyFourCharacters()
    {
        BugReportUpload.Clip(new string('x', 100)).Should().HaveLength(64);
        BugReportUpload.Clip("Windows 11").Should().Be("Windows 11");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run (from `Chaos.Client/`): `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/BugReportUploadTests/*"`
Expected: the build fails because `BugReportUpload` does not exist.

- [ ] **Step 3: Write the upload helpers**

Create `Chaos.Client/Systems/BugReportUpload.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Systems;

/// <summary>Pure rules for sending an in-game bug report: when Send is allowed, and how the picture is split.</summary>
public static class BugReportUpload
{
    /// <summary>True when a category is picked and the trimmed text is 10 to 1,000 characters.</summary>
    public static bool CanSend(BugReportCategory? category, string text)
    {
        if (category is null)
            return false;

        var length = text.Trim().Length;

        return length is >= BugReportProtocol.MIN_DESCRIPTION_CHARS and <= BugReportProtocol.MAX_DESCRIPTION_CHARS;
    }

    /// <summary>Splits the picture into parts of at most <see cref="BugReportProtocol.PART_SIZE" /> bytes, in order.</summary>
    public static IReadOnlyList<byte[]> SplitPicture(byte[] picture) => picture.Chunk(BugReportProtocol.PART_SIZE).ToList();

    /// <summary>Cuts a client detail string to <see cref="BugReportProtocol.MAX_DETAIL_CHARS" /> characters.</summary>
    public static string Clip(string value)
        => value.Length <= BugReportProtocol.MAX_DETAIL_CHARS ? value : value[..BugReportProtocol.MAX_DETAIL_CHARS];
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run (from `Chaos.Client/`): `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/BugReportUploadTests/*"`
Expected: all pass.

- [ ] **Step 5: Add the network event and send methods**

In `Chaos.Client.Networking/Definitions/Delegates.cs`, after `public delegate void BeautyShopDisplayHandler(BeautyShopDisplayArgs args);`, add:

```csharp

public delegate void BugReportOpenHandler(BugReportOpenArgs args);
```

In `Chaos.Client.Networking/ConnectionManager.cs`:
- After `public event BeautyShopDisplayHandler? OnBeautyShopDisplay;`, add:

```csharp
    public event BugReportOpenHandler? OnBugReportOpen;
```

- After `PacketHandlers[(byte)ServerOpCode.BeautyShopDisplay] = HandleBeautyShopDisplay;`, add:

```csharp
        PacketHandlers[(byte)ServerOpCode.BugReportOpen] = HandleBugReportOpen;
```

- After the `HandleBeautyShopDisplay` method, add:

```csharp

    private void HandleBugReportOpen(ServerPacket pkt)
    {
        var args = Client.Deserialize<BugReportOpenArgs>(in pkt);
        OnBugReportOpen?.Invoke(args);
    }
```

- After `SendBeautyShopClose`, add:

```csharp

    /// <summary>Sends a filled-in bug report. When its picture length is above zero, the picture follows in parts.</summary>
    public void SendBugReportSubmit(BugReportInteractionArgs submit) => SendIfWorld(submit);

    /// <summary>Sends one piece of a bug report's picture.</summary>
    public void SendBugReportPicturePart(uint reportId, byte partIndex, byte[] data)
        => SendIfWorld(
            new BugReportInteractionArgs
            {
                Type = BugReportInteractionType.PicturePart,
                ReportId = reportId,
                PartIndex = partIndex,
                Data = data
            });

    /// <summary>Tells the server the report window was closed without sending, so it can drop the report.</summary>
    public void SendBugReportCancel(uint reportId)
        => SendIfWorld(
            new BugReportInteractionArgs
            {
                Type = BugReportInteractionType.Cancel,
                ReportId = reportId
            });
```

- [ ] **Step 6: Add the captured frame type**

Create `Chaos.Client/Models/CapturedFrame.cs`:

```csharp
using SkiaSharp;

namespace Chaos.Client.Models;

/// <summary>
///     One rendered frame kept in memory for a bug report: the full 640x480 picture as a 256-color PNG, and a small
///     preview for the report window. Never written to disk on the player's computer.
/// </summary>
public sealed class CapturedFrame(byte[] png, SKImage thumbnail) : IDisposable
{
    public const int THUMBNAIL_WIDTH = 110;
    public const int THUMBNAIL_HEIGHT = 83;

    public byte[] Png { get; } = png;
    public SKImage Thumbnail { get; } = thumbnail;

    public void Dispose() => Thumbnail.Dispose();
}
```

- [ ] **Step 7: Capture frames into memory in `ChaosGame`**

In `Chaos.Client/ChaosGame.cs`:
- Add `using Chaos.Client.Models;` if it is missing.
- Add the field next to `ScreenshotRequested`:

```csharp
    private Action<CapturedFrame?>? PendingCapture;
```

- After `public void RequestScreenshot() => ScreenshotRequested = true;`, add:

```csharp

    /// <summary>
    ///     Captures the next frame this game draws, HUD and popups included, and hands it to
    ///     <paramref name="onCaptured" /> from inside that frame's <see cref="Draw" />. Used by bug reports, so nothing is
    ///     written to disk. The callback gets null if the capture fails.
    /// </summary>
    public void RequestCapture(Action<CapturedFrame?> onCaptured) => PendingCapture = onCaptured;
```

- In `Draw`, directly after the `if (ScreenshotRequested) { ... }` block, add:

```csharp

        if (PendingCapture is { } onCaptured)
        {
            PendingCapture = null;
            onCaptured(CaptureFrame());
        }
```

- Replace the body of `SaveScreenshot` from `var pixels = new Color[...]` to the end, and the whole `WritePalettizedPng` method, with the shared encoding below. `SaveScreenshot` keeps its `lod###` numbering code unchanged:

```csharp
    private void SaveScreenshot()
    {
        var dataPath = GlobalSettings.DataPath;
        var highestNumber = 0;

        foreach (var file in Directory.EnumerateFiles(dataPath, "lod*.*"))
        {
            var name = Path.GetFileNameWithoutExtension(file);

            if ((name.Length >= 4) && int.TryParse(name.AsSpan(3), out var num) && (num > highestNumber))
                highestNumber = num;
        }

        var nextNumber = highestNumber + 1;
        var fileName = Path.Combine(dataPath, $"lod{nextNumber:D3}.png");

        using var sourceImage = ReadRenderTarget();
        using var output = File.Create(fileName);
        EncodePalettizedPng(sourceImage, output);
    }

    private CapturedFrame? CaptureFrame()
    {
        try
        {
            using var sourceImage = ReadRenderTarget();
            using var png = new MemoryStream();
            EncodePalettizedPng(sourceImage, png);

            return new CapturedFrame(png.ToArray(), BuildThumbnail(sourceImage));
        } catch (Exception)
        {
            //a failed capture only means the report goes without a picture
            return null;
        }
    }

    private SKImage ReadRenderTarget()
    {
        var pixels = new Color[VIRTUAL_WIDTH * VIRTUAL_HEIGHT];
        RenderTarget.GetData(pixels);

        var imageInfo = new SKImageInfo(VIRTUAL_WIDTH, VIRTUAL_HEIGHT, SKColorType.Rgba8888, SKAlphaType.Premul);

        return SKImage.FromPixelCopy(imageInfo, MemoryMarshal.AsBytes(pixels.AsSpan()), VIRTUAL_WIDTH * 4);
    }

    private static void EncodePalettizedPng(SKImage sourceImage, Stream output)
    {
        using var intermediary = ImageProcessor.PreserveNonTransparentBlacks(sourceImage);
        using var quantized = ImageProcessor.Quantize(QuantizerOptions.Default, intermediary);
        var palette = quantized.Palette;
        var indices = quantized.Entity.GetPalettizedPixelData(palette);

        var rgbPalette = new List<uint>(palette.Count);

        for (var i = 0; i < palette.Count; i++)
        {
            var c = palette[i];
            rgbPalette.Add(((uint)c.Red << 16) | ((uint)c.Green << 8) | c.Blue);
        }

        WritePalettizedPng(output, sourceImage.Width, sourceImage.Height, indices, rgbPalette);
    }

    private static SKImage BuildThumbnail(SKImage source)
    {
        var info = new SKImageInfo(
            CapturedFrame.THUMBNAIL_WIDTH,
            CapturedFrame.THUMBNAIL_HEIGHT,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var surface = SKSurface.Create(info);
        surface.Canvas.DrawImage(source, new SKRect(0, 0, info.Width, info.Height), new SKSamplingOptions(SKCubicResampler.Mitchell));

        return surface.Snapshot();
    }

    private static void WritePalettizedPng(Stream stream, int width, int height, byte[] indices, List<uint> palette)
    {
        //PNG signature
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        //IHDR — width, height, 8-bit indexed color
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; //bit depth
        ihdr[9] = 3; //color type: indexed
        WritePngChunk(stream, "IHDR"u8, ihdr);

        //PLTE — RGB triplets
        var plte = new byte[palette.Count * 3];

        for (var i = 0; i < palette.Count; i++)
        {
            var rgb = palette[i];
            plte[i * 3] = (byte)(rgb >> 16);
            plte[i * 3 + 1] = (byte)(rgb >> 8);
            plte[i * 3 + 2] = (byte)rgb;
        }

        WritePngChunk(stream, "PLTE"u8, plte);

        //IDAT — zlib-compressed scanlines with no-filter bytes
        using var idatBuffer = new MemoryStream();

        using (var zlib = new ZLibStream(idatBuffer, CompressionLevel.Optimal, true))
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0); //filter: none
                zlib.Write(indices, y * width, width);
            }

        WritePngChunk(stream, "IDAT"u8, idatBuffer.ToArray());

        //IEND
        WritePngChunk(stream, "IEND"u8, []);
    }
```

`WritePngChunk` and `PngCrcTable` stay as they are.

- [ ] **Step 8: Count frames while the overlay is hidden**

In `Chaos.Client/Controls/Generic/DebugOverlay.cs`:
- Add next to the `IsActive` property:

```csharp
    /// <summary>Frames drawn in the last full second. Counted even while the overlay is hidden, for bug reports.</summary>
    public static int FramesPerSecond => DisplayFps;
```

- In `Update`, move the fps block above the `if (!IsActive) return;` guard, so that the method starts:

```csharp
    public static void Update(GameTime gameTime)
    {
        //fps counter — count actual frames per second. Runs while the overlay is hidden too, because bug reports send it.
        FpsCounter++;
        FpsElapsed += (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        if (FpsElapsed >= 1000f)
        {
            DisplayFps = FpsCounter;
            FpsCounter = 0;
            FpsElapsed -= 1000f;
        }

        if (!IsActive)
            return;

        FrameTimeHistory[FrameTimeIndex % FRAME_TIME_HISTORY] = LastFrameWorkMs;
        FrameTimeIndex++;
```

The rest of `Update` (the gc tracking) stays unchanged, and the old fps block below the guard is removed.

- [ ] **Step 9: Build and run the client tests**

Run (from `Chaos.Client/`):
`dotnet build Chaos.Client.slnx`
`dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: the build succeeds and every client test passes. This includes the existing tests, which confirms the refactor broke nothing.

```json:metadata
{"files": ["Chaos.Client/Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client/Chaos.Client.Networking/ConnectionManager.cs", "Chaos.Client/Chaos.Client/Models/CapturedFrame.cs", "Chaos.Client/Chaos.Client/Systems/BugReportUpload.cs", "Chaos.Client/Chaos.Client/ChaosGame.cs", "Chaos.Client/Chaos.Client/Controls/Generic/DebugOverlay.cs", "Chaos.Client/Tests/Chaos.Client.Tests/BugReportUploadTests.cs"], "verifyCommand": "cd Chaos.Client && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/BugReportUploadTests/*\" && dotnet build Chaos.Client.slnx", "acceptanceCriteria": ["SplitPicture parts <= 32768 and join back, exact multiple and empty handled", "CanSend needs category and 10-1000 trimmed chars", "Clip limits to 64 chars", "ConnectionManager raises OnBugReportOpen and has three send methods", "RequestCapture yields PNG bytes and 110x83 thumbnail without disk writes", "F12 still saves lod###.png", "FramesPerSecond updates while overlay hidden", "Chaos.Client.slnx builds"], "modelTier": "standard"}
```

---

### Task 8: Client report window and wiring

**Goal:** Build the A2 wooden report window, and wire it so that `BugReportOpen` closes the NPC dialog, captures a clean frame, and opens the window on the next update.

**Files:**
- Modify: `Chaos.Client/Chaos.Client/Controls/Custom/CustomButton.cs` (`Selected`)
- Create: `Chaos.Client/Chaos.Client/Controls/World/Popups/BugReport/BugReportControl.cs`
- Modify: `Chaos.Client/Chaos.Client/Screens/WorldScreen.cs` (fields, construction, `Root.AddChild`, unwire)
- Modify: `Chaos.Client/Chaos.Client/Screens/WorldScreen.Wiring.cs` (`WireBugReport`, `SendBugReport`)
- Modify: `Chaos.Client/Chaos.Client/Screens/WorldScreen.ServerHandlers.cs` (`HandleBugReportOpen`)
- Modify: `Chaos.Client/Chaos.Client/Screens/WorldScreen.Update.cs` (open the pending report)
- Modify: `Chaos.Client/CLAUDE.md` (Popups list)

**Acceptance Criteria:**
- [ ] The window is 430×310 in the `FramedDialogPanelBase` frame, centered. It has the title, a 4×2 category grid with the Global Constraints captions, a multi-line text box (max 1,000), an `n / 1000` counter, a 110×83 preview (or "No picture"), an "Include it" box, a two-line note, Send Report, and the frame's Close button
- [ ] A picked category's caption is gold. Send Report is dim until a category is picked and the trimmed text is 10 or more characters
- [ ] Close and Esc send `Cancel` with the report number and hide the window
- [ ] On `BugReportOpen`, the NPC dialog is hidden before the frame is captured, and the window opens on the next update
- [ ] Send sends the submit, then the parts. The parts are skipped when the box is unticked or the picture is over 512 KB
- [ ] `Chaos.Client.slnx` builds, and all client tests pass

**Verify:** `dotnet build Chaos.Client.slnx` then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` (from `Chaos.Client/`) → build succeeds, all tests pass. The window itself is checked in Task 10.

**Steps:**

- [ ] **Step 1: Add a selected state to `CustomButton`**

In `Chaos.Client/Controls/Custom/CustomButton.cs`:
- Add after the `Caption` property:

```csharp

    /// <summary>Shows the caption in gold, for the current pick in a group of buttons (the bug report categories).</summary>
    public bool Selected { get; set; }
```

- In `Draw`, replace `var col = Enabled ? TextColors.Default : Dim(TextColors.Default);` with:

```csharp
        var col = !Enabled ? Dim(TextColors.Default) : Selected ? LegendColors.Gold : TextColors.Default;
```

- Add `using Chaos.Client.Rendering.Definitions;` if `LegendColors` does not resolve.

- [ ] **Step 2: Write the report window**

Create `Chaos.Client/Controls/World/Popups/BugReport/BugReportControl.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.BugReport;

/// <summary>What the player chose to send. <see cref="Picture" /> is null when the box was unticked or no frame was captured.</summary>
public sealed record BugReportSubmission(uint ReportId, BugReportCategory Category, string Description, byte[]? Picture);

/// <summary>
///     The in-game bug report window (layout A2): a 4x2 category grid, a multi-line description, a preview of the picture
///     taken when the window opened, and an "Include it" box. Opened by the server's BugReportOpen through Terminus's
///     "Report a bug". Close and Escape cancel. Send Report stays dim until a category is picked and 10 or more
///     characters are typed.
/// </summary>
public sealed class BugReportControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 430;
    private const int PANEL_HEIGHT = 310;
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;
    private const int LEFT = 22;
    private const int CONTENT_WIDTH = 374;
    private const int GAP = 4;

    private const int TITLE_TOP = 10;
    private const int CATEGORY_CAPTION_TOP = 28;
    private const int CATEGORY_GRID_TOP = 42;
    private const int CATEGORY_BUTTON_WIDTH = (CONTENT_WIDTH - (3 * GAP)) / 4;
    private const int DESCRIPTION_CAPTION_TOP = CATEGORY_GRID_TOP + (2 * CustomButton.HEIGHT) + GAP + 8;
    private const int DESCRIPTION_TOP = DESCRIPTION_CAPTION_TOP + 14;
    private const int DESCRIPTION_HEIGHT = 100;
    private const int PREVIEW_LEFT = LEFT + CONTENT_WIDTH - CapturedFrame.THUMBNAIL_WIDTH;
    private const int DESCRIPTION_WIDTH = PREVIEW_LEFT - LEFT - 10;
    private const int COUNTER_TOP = DESCRIPTION_TOP + DESCRIPTION_HEIGHT + 3;
    private const int CHECKBOX_TOP = DESCRIPTION_TOP + CapturedFrame.THUMBNAIL_HEIGHT + 6;
    private const int SEND_WIDTH = 80;
    private const int SEND_TOP = COUNTER_TOP + 14;
    private const int NOTE_TOP = SEND_TOP + 2;

    private static readonly BugReportCategory[] CategoryOrder =
    [
        BugReportCategory.SkillSpell,
        BugReportCategory.Item,
        BugReportCategory.MapWarp,
        BugReportCategory.Npc,
        BugReportCategory.Quest,
        BugReportCategory.MonsterCombat,
        BugReportCategory.ClientUi,
        BugReportCategory.Other
    ];

    private readonly Dictionary<BugReportCategory, CustomButton> CategoryButtons = [];
    private readonly UILabel CounterLabel;
    private readonly CustomTextBox DescriptionBox;
    private readonly CustomCheckBox IncludePicture;
    private readonly UILabel NoPictureLabel;
    private readonly UIImage Preview;
    private readonly CustomButton SendButton;

    private BugReportCategory? Category;
    private CapturedFrame? Frame;
    private uint ReportId;

    public BugReportControl()
        : base("_nsett", false)
    {
        Name = "BugReport";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Caption("Report a Bug", 0, TITLE_TOP, PANEL_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("WHAT KIND OF PROBLEM?", LEFT, CATEGORY_CAPTION_TOP, CONTENT_WIDTH, color: LegendColors.Gray);

        for (var i = 0; i < CategoryOrder.Length; i++)
        {
            var category = CategoryOrder[i];

            var button = new CustomButton(BugReportProtocol.DisplayName(category), CATEGORY_BUTTON_WIDTH)
            {
                X = LEFT + ((i % 4) * (CATEGORY_BUTTON_WIDTH + GAP)),
                Y = CATEGORY_GRID_TOP + ((i / 4) * (CustomButton.HEIGHT + GAP))
            };

            button.Clicked += () => SelectCategory(category);
            CategoryButtons[category] = button;
            AddChild(button);
        }

        Caption("WHAT HAPPENED? WHAT DID YOU EXPECT?", LEFT, DESCRIPTION_CAPTION_TOP, DESCRIPTION_WIDTH, color: LegendColors.Gray);

        DescriptionBox = new CustomTextBox
        {
            X = LEFT,
            Y = DESCRIPTION_TOP,
            Width = DESCRIPTION_WIDTH,
            Height = DESCRIPTION_HEIGHT,
            IsMultiLine = true,
            MaxLength = BugReportProtocol.MAX_DESCRIPTION_CHARS,
            HintText = "What were you doing? What went wrong?"
        };

        AddChild(DescriptionBox);

        CounterLabel = Caption(
            $"0 / {BugReportProtocol.MAX_DESCRIPTION_CHARS}",
            LEFT,
            COUNTER_TOP,
            DESCRIPTION_WIDTH,
            HorizontalAlignment.Right,
            LegendColors.Gray);

        Caption("SCREENSHOT", PREVIEW_LEFT, DESCRIPTION_CAPTION_TOP, CapturedFrame.THUMBNAIL_WIDTH, color: LegendColors.Gray);

        Preview = new UIImage
        {
            X = PREVIEW_LEFT,
            Y = DESCRIPTION_TOP,
            Width = CapturedFrame.THUMBNAIL_WIDTH,
            Height = CapturedFrame.THUMBNAIL_HEIGHT,
            IsHitTestVisible = false
        };

        AddChild(Preview);

        NoPictureLabel = Caption(
            "No picture",
            PREVIEW_LEFT,
            DESCRIPTION_TOP + ((CapturedFrame.THUMBNAIL_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            CapturedFrame.THUMBNAIL_WIDTH,
            HorizontalAlignment.Center,
            LegendColors.Gray);

        IncludePicture = new CustomCheckBox
        {
            X = PREVIEW_LEFT,
            Y = CHECKBOX_TOP,
            Width = CapturedFrame.THUMBNAIL_WIDTH,
            Height = CustomCheckBox.CHECKBOX_SIZE,
            Text = "Include it",
            Checked = true
        };

        IncludePicture.Clicked += () => IncludePicture.Checked = !IncludePicture.Checked;
        AddChild(IncludePicture);

        var note = Caption(
            "Your character, position and recent server events are attached for staff.",
            LEFT,
            NOTE_TOP,
            DESCRIPTION_WIDTH,
            color: LegendColors.Gray);

        note.WordWrap = true;
        note.Height = TextRenderer.CHAR_HEIGHT * 2;

        SendButton = new CustomButton("Send Report", SEND_WIDTH)
        {
            X = LEFT + CONTENT_WIDTH - SEND_WIDTH,
            Y = SEND_TOP,
            Enabled = false
        };

        SendButton.Clicked += Send;
        AddChild(SendButton);
    }

    /// <summary>Raised when the player presses Send Report. The window has already hidden itself.</summary>
    public event Action<BugReportSubmission>? SendRequested;

    /// <summary>Raised with the report number when the player closes the window without sending.</summary>
    public event Action<uint>? Cancelled;

    /// <summary>Resets the window for a new report and shows it. Takes ownership of <paramref name="frame" />.</summary>
    public void Open(uint reportId, CapturedFrame? frame)
    {
        ReleaseFrame();
        ReportId = reportId;
        Frame = frame;
        Category = null;

        foreach (var button in CategoryButtons.Values)
            button.Selected = false;

        DescriptionBox.Text = string.Empty;
        Preview.Texture = frame is null ? null : TextureConverter.ToTexture2D(frame.Thumbnail);
        NoPictureLabel.Visible = frame is null;
        IncludePicture.Visible = frame is not null;
        IncludePicture.Checked = frame is not null;
        RefreshSendState();
        Show();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();
        ReleaseFrame();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible)
            RefreshSendState();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Dismiss();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Dispose()
    {
        ReleaseFrame();
        base.Dispose();
    }

    private UILabel Caption(
        string text,
        int x,
        int y,
        int width,
        HorizontalAlignment alignment = HorizontalAlignment.Left,
        Color? color = null)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color ?? LegendColors.White,
            IsHitTestVisible = false,
            Text = text
        };

        AddChild(label);

        return label;
    }

    private void SelectCategory(BugReportCategory category)
    {
        Category = category;

        foreach ((var key, var button) in CategoryButtons)
            button.Selected = key == category;

        RefreshSendState();
    }

    private void RefreshSendState()
    {
        var counter = $"{DescriptionBox.Text.Length} / {BugReportProtocol.MAX_DESCRIPTION_CHARS}";

        if (CounterLabel.Text != counter)
            CounterLabel.Text = counter;

        SendButton.Enabled = BugReportUpload.CanSend(Category, DescriptionBox.Text);
    }

    private void Send()
    {
        if (!BugReportUpload.CanSend(Category, DescriptionBox.Text))
            return;

        var submission = new BugReportSubmission(
            ReportId,
            Category!.Value,
            DescriptionBox.Text.Trim(),
            IncludePicture.Checked ? Frame?.Png : null);

        Hide();
        SendRequested?.Invoke(submission);
    }

    private void Dismiss()
    {
        if (!Visible)
            return;

        var reportId = ReportId;
        Hide();
        Cancelled?.Invoke(reportId);
    }

    private void ReleaseFrame()
    {
        Preview.Texture?.Dispose();
        Preview.Texture = null;
        Frame?.Dispose();
        Frame = null;
    }
}
```

If a type does not resolve, copy the matching using from `Controls/World/Popups/Beauty/BeautyShopControl.cs`, which uses the same controls. `HorizontalAlignment`, `KeyDownEvent` and `Keycode` resolve there.

- [ ] **Step 3: Add the fields and construction in `WorldScreen.cs`**

After `private BeautyShopControl BeautyShop = null!;`, add:

```csharp

    //in-game bug report window — opened by the server's BugReportOpen from Terminus's "Report a bug"
    private BugReportControl BugReport = null!;

    //a report number and the frame captured for it: set during Draw, opened on the next Update
    private (uint ReportId, CapturedFrame? Frame)? PendingBugReport;
```

After `WireBeautyShop();` (in the block that builds `BeautyShop`), add:

```csharp

        BugReport = new BugReportControl
        {
            ZIndex = 2
        };
        WireBugReport();
```

After `Root.AddChild(BeautyShop);`, add `Root.AddChild(BugReport);`.

After `Game.Connection.OnBeautyShopDisplay -= HandleBeautyShopDisplay;`, add `Game.Connection.OnBugReportOpen -= HandleBugReportOpen;`.

Add `using Chaos.Client.Controls.World.Popups.BugReport;` and `using Chaos.Client.Models;` if they are missing.

- [ ] **Step 4: Wire the window in `WorldScreen.Wiring.cs`**

After the `#region Beauty Shop Wiring ... #endregion` block, add:

```csharp

    #region Bug Report Wiring
    private static readonly string ClientBuild
        = typeof(ChaosGame).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    private void WireBugReport()
    {
        Game.Connection.OnBugReportOpen += HandleBugReportOpen;
        BugReport.SendRequested += SendBugReport;
        BugReport.Cancelled += reportId => Game.Connection.SendBugReportCancel(reportId);
    }

    /// <summary>
    ///     Sends the report, then the picture in parts. A picture over the server's limit is not sent. The submit still
    ///     carries its length, so the server writes the report and notes why the picture is missing.
    /// </summary>
    private void SendBugReport(BugReportSubmission submission)
    {
        var picture = submission.Picture;
        var bounds = Game.Window.ClientBounds;

        Game.Connection.SendBugReportSubmit(
            new BugReportInteractionArgs
            {
                Type = BugReportInteractionType.Submit,
                ReportId = submission.ReportId,
                Category = submission.Category,
                Description = submission.Description,
                PictureLength = (uint)(picture?.Length ?? 0),
                ClientBuild = BugReportUpload.Clip(ClientBuild),
                OsDescription = BugReportUpload.Clip(RuntimeInformation.OSDescription),
                FramesPerSecond = (ushort)Math.Clamp(DebugOverlay.FramesPerSecond, 0, ushort.MaxValue),
                PingMs = (ushort)Math.Clamp(LatencyMonitor.LatencyMs ?? 0L, 0L, ushort.MaxValue),
                HudStyle = (byte)(WorldHud == LargeHud ? 1 : 0),
                WindowWidth = (ushort)Math.Clamp(bounds.Width, 0, ushort.MaxValue),
                WindowHeight = (ushort)Math.Clamp(bounds.Height, 0, ushort.MaxValue)
            });

        if ((picture is null) || (picture.Length > BugReportProtocol.MAX_PICTURE_BYTES))
            return;

        var parts = BugReportUpload.SplitPicture(picture);

        for (var i = 0; i < parts.Count; i++)
            Game.Connection.SendBugReportPicturePart(submission.ReportId, (byte)i, parts[i]);
    }
    #endregion
```

Add any missing usings: `System.Reflection`, `System.Runtime.InteropServices`, `Chaos.Client.Controls.Generic`, `Chaos.Client.Controls.World.Popups.BugReport`, `Chaos.Client.Systems`, `Chaos.DarkAges.Definitions` and `Chaos.Networking.Entities.Client`.

- [ ] **Step 5: Handle the open message in `WorldScreen.ServerHandlers.cs`**

After the `HandleBeautyShopDisplay` method, add:

```csharp

    /// <summary>
    ///     Terminus's "Report a bug". The NPC dialog is closed now, during Update, so the frame drawn this tick has no
    ///     dialog in it. That frame is captured, and the report window opens on the next Update (see
    ///     <see cref="Update" />), so it is not in the picture either.
    /// </summary>
    private void HandleBugReportOpen(BugReportOpenArgs args)
    {
        NpcSession.HideAll();
        BugReport.Hide();

        var reportId = args.ReportId;

        Game.RequestCapture(
            frame =>
            {
                PendingBugReport?.Frame?.Dispose();
                PendingBugReport = (reportId, frame);
            });
    }
```

- [ ] **Step 6: Open the pending report in `WorldScreen.Update.cs`**

In `Update`, directly after the `if (PendingLoginSwitch) { ... }` block, add:

```csharp

        //a frame captured for a bug report during the last Draw: open the window now, after the picture was taken
        if (PendingBugReport is { } pendingReport)
        {
            PendingBugReport = null;
            BugReport.Open(pendingReport.ReportId, pendingReport.Frame);
        }
```

- [ ] **Step 7: Note the new popup in `CLAUDE.md`**

In `Chaos.Client/CLAUDE.md`, in the **Popups (`Popups/`):** line, replace `Subdirectories: \`Boards/\`` with `Subdirectories: \`BugReport/\` (BugReportControl — in-game bug report window opened from Terminus), \`Boards/\``.

- [ ] **Step 8: Build and run the client tests**

Run (from `Chaos.Client/`):
`dotnet build Chaos.Client.slnx`
`dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: the build succeeds and all tests pass.

```json:metadata
{"files": ["Chaos.Client/Chaos.Client/Controls/Custom/CustomButton.cs", "Chaos.Client/Chaos.Client/Controls/World/Popups/BugReport/BugReportControl.cs", "Chaos.Client/Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/Chaos.Client/Screens/WorldScreen.Wiring.cs", "Chaos.Client/Chaos.Client/Screens/WorldScreen.ServerHandlers.cs", "Chaos.Client/Chaos.Client/Screens/WorldScreen.Update.cs", "Chaos.Client/CLAUDE.md"], "verifyCommand": "cd Chaos.Client && dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["430x310 framed window with grid, text box, counter, preview, box, note, Send, Close", "selected category gold, Send dim until category and 10+ chars", "Close and Esc send Cancel and hide", "NPC dialog hidden before capture, window opens next Update", "submit then parts, parts skipped when unticked or over 512 KB", "Chaos.Client.slnx builds and client tests pass"], "modelTier": "standard"}
```

---

### Task 9: In-game report import command

**Goal:** Copy new report folders from a copied `BugReports` tree into `Unora/docs/player-feedback/in-game/`, never overwriting, and rebuild its index with the archive's index code.

**Files:**
- Modify: `Unora/Tools/FeedbackSync/archive.py` (`entry_from_text` gains `category`; `render_index` gains `stamp_label`, an in-game title, and the category in each line)
- Create: `Unora/Tools/FeedbackSync/import_reports.py`
- Create: `Unora/Tools/FeedbackSync/test_import.py`
- Modify: `Unora/docs/player-feedback/.gitignore` (`in-game/`)
- Modify: `Unora/docs/player-feedback/README.md`
- Modify: `Unora/Tools/FeedbackSync/README.md` (an "In-game reports" section)

**Acceptance Criteria:**
- [ ] New `<category>/<report>/` folders with a `report.md` are copied. `_pending` and folders without `report.md` are skipped
- [ ] An already imported report is never overwritten, so its `status` survives
- [ ] `in-game/index.md` has the title "In-game bug reports" and an `Imported: … (UTC)` line, groups by status, and links each `report.md` with category, author and date
- [ ] Running without a path prints usage and exits with 2
- [ ] `python Tools/FeedbackSync/test_sync.py` still passes, so the Discord index is unchanged

**Verify:** `python Tools/FeedbackSync/test_import.py` and `python Tools/FeedbackSync/test_sync.py` (from `Unora/`) → both `OK`

**Steps:**

- [ ] **Step 1: Write the failing test**

Create `Tools/FeedbackSync/test_import.py`:

```python
import datetime
import io
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import import_reports

NOW = datetime.datetime(2026, 9, 23, 19, 43, 0, tzinfo=datetime.timezone.utc)


def write_report(root: Path, category: str, name: str, status: str = "open", title: str = "Stuck in the wall", note: str = "") -> Path:
    folder = root / category / name
    folder.mkdir(parents=True)
    (folder / "report.md").write_text(
        "---\n"
        f'id: "{name}"\n'
        "kind: in-game\n"
        f"category: {category}\n"
        f'title: "{title}"\n'
        'author: "Bob"\n'
        "created: 2026-09-23\n"
        f"status: {status}\n"
        f'status_note: "{note}"\n'
        "---\n\n## Report\n\n```text\nhi\n```\n\n## Triage\n\n<!-- sync:preserve -->\n",
        encoding="utf-8",
    )
    (folder / "world.json").write_text("{}", encoding="utf-8")
    return folder


class ImportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        base = Path(self.temp.name)
        self.source = base / "BugReports"
        self.repo = base / "repo"
        self.target = self.repo / "docs" / "player-feedback" / "in-game"
        self.source.mkdir()
        self.repo.mkdir()

    def tearDown(self):
        self.temp.cleanup()

    def run_import(self, argv=None):
        out, err = io.StringIO(), io.StringIO()
        code = import_reports.run([str(self.source)] if argv is None else argv, self.repo, NOW, stdout=out, stderr=err)
        return code, out.getvalue(), err.getvalue()

    def test_copies_new_reports_and_skips_pending_and_incomplete_folders(self):
        write_report(self.source, "map-warp", "2026-09-23_194301_bob")
        (self.source / "_pending" / "42").mkdir(parents=True)
        (self.source / "item" / "no-report").mkdir(parents=True)

        code, out, _ = self.run_import()

        self.assertEqual(code, 0)
        self.assertTrue((self.target / "map-warp" / "2026-09-23_194301_bob" / "report.md").is_file())
        self.assertTrue((self.target / "map-warp" / "2026-09-23_194301_bob" / "world.json").is_file())
        self.assertFalse((self.target / "_pending").exists())
        self.assertFalse((self.target / "item").exists())
        self.assertIn("Imported 1 report(s). Skipped 0 already present.", out)

    def test_never_overwrites_an_imported_report(self):
        write_report(self.target, "map-warp", "2026-09-23_194301_bob", status="fixed")
        write_report(self.source, "map-warp", "2026-09-23_194301_bob", status="open")

        _, out, _ = self.run_import()

        text = (self.target / "map-warp" / "2026-09-23_194301_bob" / "report.md").read_text(encoding="utf-8")
        self.assertIn("status: fixed\n", text)
        self.assertIn("Imported 0 report(s). Skipped 1 already present.", out)

    def test_index_groups_by_status_and_links_each_report(self):
        write_report(self.source, "map-warp", "2026-09-23_194301_bob", note="east door")
        write_report(self.source, "item", "2026-09-23_194500_bob", status="fixed", title="Sword vanished")

        self.run_import()

        index = (self.target / "index.md").read_text(encoding="utf-8")
        self.assertIn("# In-game bug reports\n", index)
        self.assertIn("Imported: 2026-09-23T19:43:00Z (UTC)\n", index)
        self.assertIn("Open: 1 · Fixed: 1 · Wontfix: 0 · Wanted: 0 · Duplicate: 0 · Other: 0 · Missing: 0\n", index)
        self.assertIn(
            "- [Stuck in the wall](map-warp/2026-09-23_194301_bob/report.md) — map-warp, Bob, 2026-09-23 — east door\n",
            index,
        )
        self.assertIn("- [Sword vanished](item/2026-09-23_194500_bob/report.md) — item, Bob, 2026-09-23\n", index)
        self.assertLess(index.index("## Open"), index.index("## Fixed"))

    def test_empty_import_says_no_reports(self):
        self.run_import()

        index = (self.target / "index.md").read_text(encoding="utf-8")
        self.assertIn("No reports yet.\n", index)

    def test_usage_error_without_a_path(self):
        code, _, err = self.run_import(argv=[])

        self.assertEqual(code, 2)
        self.assertIn("usage:", err)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the test to verify it fails**

Run (from `Unora/`): `python Tools/FeedbackSync/test_import.py`
Expected: `ModuleNotFoundError: No module named 'import_reports'`

- [ ] **Step 3: Extend the archive helpers**

In `Tools/FeedbackSync/archive.py`:
- In `entry_from_text`, add `"category": fields.get("category", ""),` after the `"status_note"` line.
- Replace the first lines of `render_index` up to the `lines = [` list, and the `if not entries:` block, so the function reads:

```python
INDEX_TITLES = {"bug": "Bug reports", "suggestion": "Suggestions", "in-game": "In-game bug reports"}


def render_index(kind: str, entries: list, synced_at: str, *, stamp_label: str = "Synced") -> str:
    title = INDEX_TITLES.get(kind, "Suggestions")
    counts = {key: 0 for key, _label in GROUP_ORDER}
    buckets = {key: [] for key, _label in GROUP_ORDER}
    ordered = sorted(entries, key=lambda entry: (entry.get("created") or "", entry.get("filename") or ""))
    for entry in ordered:
        if entry.get("missing"):
            key = "missing"
        elif entry.get("status") in STATUSES:
            key = entry["status"]
        else:
            key = "other"
        counts[key] += 1
        buckets[key].append(entry)
    lines = [
        f"# {title}",
        "",
        f"{stamp_label}: {synced_at} (UTC)",
        "",
        (
            f"Open: {counts['open']} · Fixed: {counts['fixed']} · "
            f"Wontfix: {counts['wontfix']} · Wanted: {counts['wanted']} · "
            f"Duplicate: {counts['duplicate']} · Other: {counts['other']} · "
            f"Missing: {counts['missing']}"
        ),
        "",
    ]
    if not entries:
        lines.append("No reports yet." if kind == "in-game" else "No posts yet.")
        return "\n".join(lines) + "\n"
    for key, label in GROUP_ORDER:
        items = buckets[key]
        if not items:
            continue
        lines.append(f"## {label}")
        lines.append("")
        for entry in items:
            safe_title = entry["title"].replace("]", "\\]")
            category = f"{entry['category']}, " if entry.get("category") else ""
            line = f"- [{safe_title}]({entry['filename']}) — {category}{entry['author']}, {entry['created']}"
            note = entry.get("status_note") or ""
            if note:
                line += f" — {note}"
            lines.append(line)
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"
```

Discord entries have an empty `category`, so their lines are unchanged.

- [ ] **Step 4: Write the import command**

Create `Tools/FeedbackSync/import_reports.py`:

```python
"""Copy in-game bug report folders into docs/player-feedback/in-game/ and rebuild its index.

Run from the repo root after copying Data/Saved/BugReports off the live server:

    python Tools/FeedbackSync/import_reports.py <path to the copied BugReports folder>

A report folder that is already imported is never overwritten, so status, status_note and Triage edits survive.
"""
from __future__ import annotations

import datetime
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import archive

PENDING = "_pending"
USAGE = "usage: python Tools/FeedbackSync/import_reports.py <path to the copied BugReports folder>"


def import_reports(source: Path, target: Path) -> tuple[int, int]:
    """Copy each <category>/<report>/ folder with a report.md that is not already in target. Returns (imported, skipped)."""
    imported = 0
    skipped = 0
    for category in sorted(path for path in source.iterdir() if path.is_dir()):
        if category.name == PENDING:
            continue
        for report in sorted(path for path in category.iterdir() if path.is_dir()):
            if not (report / "report.md").is_file():
                continue
            destination = target / category.name / report.name
            if destination.exists():
                skipped += 1
                continue
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copytree(report, destination)
            imported += 1
    return imported, skipped


def build_index(target: Path, stamp: str) -> str:
    entries = []
    for report_md in sorted(target.glob("*/*/report.md")):
        entry = archive.entry_from_text(report_md, report_md.read_text(encoding="utf-8-sig"))
        entry["filename"] = report_md.relative_to(target).as_posix()
        entries.append(entry)
    return archive.render_index("in-game", entries, stamp, stamp_label="Imported")


def run(argv: list, repo_root: Path, now: datetime.datetime, stdout=None, stderr=None) -> int:
    stdout = stdout or sys.stdout
    stderr = stderr or sys.stderr
    if len(argv) != 1:
        print(USAGE, file=stderr)
        return 2
    source = Path(argv[0])
    if not source.is_dir():
        print(f"not a folder: {source}", file=stderr)
        return 2
    target = repo_root / "docs" / "player-feedback" / "in-game"
    target.mkdir(parents=True, exist_ok=True)
    imported, skipped = import_reports(source, target)
    stamp = now.astimezone(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    (target / "index.md").write_text(build_index(target, stamp), encoding="utf-8")
    print(f"Imported {imported} report(s). Skipped {skipped} already present.", file=stdout)
    return 0


def main() -> int:
    return run(sys.argv[1:], Path(__file__).resolve().parents[2], datetime.datetime.now(datetime.timezone.utc))


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 5: Keep reports out of git, and document the command**

Append `in-game/` as a new line to `docs/player-feedback/.gitignore`.

In `docs/player-feedback/README.md`:
- After the `suggestions/index.md` bullet, add:

```markdown
- `in-game/index.md` — reports sent from the game (F1 → Terminus → Report a bug)
```

- Replace the sentence `The \`bugs/\` and \`suggestions/\` folders are gitignored.` with `The \`bugs/\`, \`suggestions/\` and \`in-game/\` folders are gitignored.`
- Add this paragraph at the end:

```markdown
In-game reports live on the live server in `Data/Saved/BugReports/<category>/`. Copy that folder here, then run `python Tools/FeedbackSync/import_reports.py <copied folder>`. The import only adds report folders you do not have yet, so `status`, `status_note` and Triage edits are kept. Each report folder holds `report.md`, `screenshot.png`, `world.json`, `client.json`, `server-log.jsonl` and the character's save files in `character/`.
```

Append to `Tools/FeedbackSync/README.md`:

```markdown

## In-game reports

`import_reports.py` imports reports sent from the game's F1 → Terminus → Report a bug window. Copy `Data/Saved/BugReports` off the live server, then run from the repo root:

    python Tools/FeedbackSync/import_reports.py <path to the copied BugReports folder>

It copies report folders into `docs/player-feedback/in-game/<category>/` only when they are not there yet, skips `_pending`, and rebuilds `docs/player-feedback/in-game/index.md`. Tests: `python Tools/FeedbackSync/test_import.py`.
```

- [ ] **Step 6: Run both test files**

Run (from `Unora/`):
`python Tools/FeedbackSync/test_import.py`
`python Tools/FeedbackSync/test_sync.py`
Expected: both end with `OK`.

```json:metadata
{"files": ["Unora/Tools/FeedbackSync/archive.py", "Unora/Tools/FeedbackSync/import_reports.py", "Unora/Tools/FeedbackSync/test_import.py", "Unora/docs/player-feedback/.gitignore", "Unora/docs/player-feedback/README.md", "Unora/Tools/FeedbackSync/README.md"], "verifyCommand": "cd Unora && python Tools/FeedbackSync/test_import.py && python Tools/FeedbackSync/test_sync.py", "acceptanceCriteria": ["copies new report folders, skips _pending and folders without report.md", "never overwrites an imported report", "index titled In-game bug reports with Imported line, grouped by status, linking report.md with category", "no path prints usage and exits 2", "test_sync.py still passes"], "modelTier": "mechanical"}
```

---

### Task 10: Full test run and manual check against a local server

**Goal:** Run every automated test, then have the user try the feature in the real client against a local server.

**Files:**
- None changed. Evidence only.

**Acceptance Criteria:**
- [ ] `Chaos.Tests` (full), `Chaos.Client.Tests` (full), `test_import.py` and `test_sync.py` all pass
- [ ] F1 → Terminus → Report a bug → pick Map or Warp → type → Send creates `Unora/Data/Saved/BugReports/map-warp/<folder>/` with `report.md`, `screenshot.png`, `world.json`, `client.json`, `server-log.jsonl` and 9 files in `character/`
- [ ] `screenshot.png` shows no Terminus dialog and no report window
- [ ] Unticking "Include it" gives a report with no `screenshot.png`, and `screenshot: false` in `report.md`
- [ ] A second report within 2 minutes gets "You sent a report recently…", and no window opens
- [ ] Close and Esc leave no folder in `_pending/`
- [ ] F12 still writes `lod###.png` to the client data folder
- [ ] A client reporting version 751 is turned away at the lobby

**Verify:** the user confirms each in-game check. Record the `ls -R` of the report folder and the text of its `report.md` as evidence.

**Steps:**

- [ ] **Step 1: Run every automated test**

Run:
- (from `Chaos-Server/`) `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi`
- (from `Chaos.Client/`) `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
- (from `Unora/`) `python Tools/FeedbackSync/test_import.py` and `python Tools/FeedbackSync/test_sync.py`

Expected: all pass. Paste the summary lines as evidence.

- [ ] **Step 2: Hand the in-game checks to the user**

These steps need a person at the keyboard and the Unora game data folder. Ask the user to:

1. Start the server: `dotnet run --project Chaos/Chaos.csproj` (from `Chaos-Server/`). The local `StagingDirectory` points at `Unora`.
2. Start the client with the game data path set, for example `$env:DA_PATH = "<Unora game data folder>"; dotnet run --project Chaos.Client/Chaos.Client.csproj` (from `Chaos.Client/`).
3. Log in, then work through each in-game line of **Acceptance Criteria** in order.
4. For the version check, run the previous client release (751) against the same server.

- [ ] **Step 3: Collect evidence**

Run (from `Unora/`): `ls -R Data/Saved/BugReports/map-warp` and `cat` the newest `report.md`. Paste both as evidence. Paste the picture to the user, or describe it, to confirm no dialog is in it.

```json:metadata
{"files": [], "verifyCommand": "cd Chaos.Client/Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["all automated suites pass", "Send writes a full map-warp report folder", "screenshot has no Terminus dialog or report window", "unticked box gives no screenshot.png and screenshot: false", "second report within 2 minutes gets the wait message", "Close and Esc leave no pending folder", "F12 still writes lod###.png", "version 751 client refused"], "modelTier": "standard"}
```

---

### Task 11: Commit the full implementation

**Goal:** Commit the whole change set in its three repos on `feat/in-game-bug-reports` branches, one commit per repo, without the machine-specific edits.

**Files:**
- Every file created, modified or deleted by Tasks 1–9, plus the spec, this plan and its `.tasks.json`

**Acceptance Criteria:**
- [ ] `Chaos-Server`, `Chaos.Client` and `Unora` each have one new commit on `feat/in-game-bug-reports`
- [ ] The `Chaos.Client` commit includes the moved `Chaos-Server` submodule pointer, the spec, the plan and the `.tasks.json`
- [ ] Neither the `StagingDirectory` change nor `launchSettings.json` is in any commit, and both local edits are still in the working tree afterwards
- [ ] Nothing is pushed

**Verify:** `git -C <repo> show --stat HEAD` for each repo lists only the files below. `git -C Chaos.Client/Chaos-Server diff Chaos/appsettings.json` still shows the local `StagingDirectory` line.

**Steps:**

- [ ] **Step 1: Commit `Chaos-Server`**

Run (from `Chaos.Client/Chaos-Server/`):

```bash
git checkout -b feat/in-game-bug-reports
# stage appsettings.json without the machine-specific StagingDirectory line, then put the line back
python - <<'EOF'
from pathlib import Path
p = Path("Chaos/appsettings.json")
local = rb'"StagingDirectory": "C:\\Users\\Michael\\Documents\\GitHub\\Unora"'
head = rb'"StagingDirectory": "C:\\Users\\mewbb\\Documents\\GitHub\\Unora"'
p.write_bytes(p.read_bytes().replace(local, head, 1))
EOF
git add Chaos/appsettings.json
python - <<'EOF'
from pathlib import Path
p = Path("Chaos/appsettings.json")
local = rb'"StagingDirectory": "C:\\Users\\Michael\\Documents\\GitHub\\Unora"'
head = rb'"StagingDirectory": "C:\\Users\\mewbb\\Documents\\GitHub\\Unora"'
p.write_bytes(p.read_bytes().replace(head, local, 1))
EOF
git add Chaos.Networking.Abstractions/Definitions/Enums.cs \
        Chaos.DarkAges/Definitions/Enums.cs Chaos.DarkAges/Definitions/BugReportProtocol.cs Chaos.DarkAges/Definitions/CONSTANTS.cs \
        Chaos.Networking/Entities/Server/BugReportOpenArgs.cs Chaos.Networking/Converters/Server/BugReportOpenConverter.cs \
        Chaos.Networking/Entities/Client/BugReportInteractionArgs.cs Chaos.Networking/Converters/Client/BugReportInteractionConverter.cs \
        Chaos/Services/BugReports \
        Chaos/Services/Storage/AislingStore.cs \
        Chaos/Networking/Abstractions/IChaosWorldClient.cs Chaos/Networking/ChaosWorldClient.cs \
        Chaos/Services/Servers/WorldServer.cs Chaos/Extensions/ServiceCollectionExtensions.cs Chaos/Program.cs \
        Chaos/Scripting/DialogScripts/Temuair/Generic/TerminusBugReportScript.cs \
        Tests/Chaos.Tests/Networking/BugReportPacketConverterTests.cs Tests/Chaos.Tests/BugReports
git diff --cached --stat
git commit -m "$(cat <<'EOF'
Add in-game bug reports from the Terminus menu

Players open a report window from F1 -> Terminus -> Report a bug. The server
copies the character and the world around them when the window opens, then
writes each report to Data/Saved/BugReports/<category>/ with the text, a
screenshot, client details and the server's log lines about that character.
Adds the BugReportOpen and BugReportInteraction messages and requires client
version 752.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
git diff Chaos/appsettings.json
```

Expected: `git diff --cached --stat` lists only the Task 1–6 server files. The final `git diff` still shows the local `StagingDirectory` line.

- [ ] **Step 2: Commit `Unora`**

Run (from `Unora/`):

```bash
git checkout -b feat/in-game-bug-reports
git add Tools/FeedbackSync/archive.py Tools/FeedbackSync/import_reports.py Tools/FeedbackSync/test_import.py Tools/FeedbackSync/README.md \
        docs/player-feedback/.gitignore docs/player-feedback/README.md \
        "Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_bugreport.json" \
        "Data/Configuration/Templates/Dialogs/Temauir/terminus/terminus_initial.json"
git diff --cached --stat
git commit -m "$(cat <<'EOF'
Add the Terminus bug report dialog and the in-game report import

terminus_bugreport opens the new client report window. The old Report System
dialogs, which posted to Discord with no bot token, are removed.
import_reports.py copies report folders from the live server into
docs/player-feedback/in-game/ without overwriting triage, and indexes them.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

Expected: the stat lists the files above plus the 7 `Report System` deletions staged in Task 6.

- [ ] **Step 3: Commit `Chaos.Client`**

Run (from `Chaos.Client/`). Run `graphify update .` first if the `graphify` command exists, as `CLAUDE.md` asks.

```bash
git checkout -b feat/in-game-bug-reports
git add Chaos-Server \
        Chaos.Client.Networking/ConnectionManager.cs Chaos.Client.Networking/Definitions/Delegates.cs \
        Chaos.Client/ChaosGame.cs Chaos.Client/Models/CapturedFrame.cs Chaos.Client/Systems/BugReportUpload.cs \
        Chaos.Client/Controls/Custom/CustomButton.cs Chaos.Client/Controls/Generic/DebugOverlay.cs \
        Chaos.Client/Controls/World/Popups/BugReport/BugReportControl.cs \
        Chaos.Client/Screens/WorldScreen.cs Chaos.Client/Screens/WorldScreen.Wiring.cs \
        Chaos.Client/Screens/WorldScreen.ServerHandlers.cs Chaos.Client/Screens/WorldScreen.Update.cs \
        Tests/Chaos.Client.Tests/BugReportUploadTests.cs CLAUDE.md
git add -f docs/superpowers/specs/2026-09-23-in-game-bug-reports-design.md \
           docs/superpowers/plans/2026-09-23-in-game-bug-reports.md \
           docs/superpowers/plans/2026-09-23-in-game-bug-reports.md.tasks.json
git diff --cached --stat
git commit -m "$(cat <<'EOF'
Add the in-game bug report window

Terminus's Report a bug closes the NPC dialog, captures the next frame into
memory, and opens a wooden report window with a category grid, a description
box and a screenshot preview. The report and its picture are sent to the
server in 32 KB parts. Points Chaos-Server at feat/in-game-bug-reports.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
git status --short
```

Expected: the stat does not include `launchSettings.json`, and `git status --short` still shows it as modified.

- [ ] **Step 4: Do not push**

Report the three branch names and commit hashes to the user. Pushing is the user's call.

```json:metadata
{"files": [], "verifyCommand": "git -C Chaos.Client show --stat HEAD && git -C Chaos.Client/Chaos-Server show --stat HEAD && git -C Unora show --stat HEAD", "acceptanceCriteria": ["one commit per repo on feat/in-game-bug-reports", "Chaos.Client commit includes submodule pointer, spec, plan, tasks.json", "StagingDirectory and launchSettings.json not committed and still modified locally", "nothing pushed"], "modelTier": "mechanical"}
```
