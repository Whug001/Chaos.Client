# Korean–English Chat Translation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players who pick English or Korean in F4 see other players' chat in the other language swapped, in place, to a machine translation from Azure → DeepL → Google free tiers.

**Architecture:** The game server tags each player chat line with a line id, sends the original at once, and hands a translation job to a singleton `ChatTranslationService` outside the map lock. The service checks a shortcut table, a memory + `.jsonl` cache and a per-player limit, protects game names, calls the provider chain, then sends a `ChatTranslation` packet (opcode 140) to the readers. The client replaces the chat-log line and the bubble that carry that line id.

**Tech Stack:** C# / .NET 10, TUnit + FluentAssertions + Moq, System.Text.Json, HttpClient, MonoGame client.

**Spec:** `docs/superpowers/specs/2026-09-29-chat-translation-design.md` (client worktree). Read it before any task.

## Global Constraints

- `ServerOpCode.ChatTranslation = 140`. `UserOption.ChatLanguage = 31`. `CLIENT_VERSION` 760 → 761.
- `ChatLanguage : byte { Off = 0, English = 1, Korean = 2 }` lives in `Chaos.DarkAges.Definitions` (both sides compile it).
- Line id `0` means "no line id". When `LineId == 0` every changed packet stays byte-identical to today.
- The `UserOptions` trailer order is: chat-filter mode byte, configured flag, then the language byte. The chat-filter pair is written whenever ANY trailer value is non-default. The language byte is written only when non-zero.
- Translated body limits: public `134 - name.Length - 2`, whisper `180 - name.Length - 4`, channels `180 - name.Length - 20`.
- Scripts' `OnPublicMessage` always gets the typed text. The speaker never gets a translation of their own line. Whisper translations are never written to disk.
- No HTTP call may run while a map lock is held. `Submit` never throws and never blocks.
- The per-player limit is 5,000 provider characters per UTC day (config `PerPlayerDailyCharacters`).
- The client tag is `[Kor] ` when the source is Korean and `[영어] ` when the source is English. It goes at the start of the chat-log line, after a leading `{=x` color code (3 characters) when present. Bubbles never get it.
- API keys never go in a tracked file. They go in `Chaos/appsettings.translation.json`, which is gitignored and optional.
- Follow the existing code style: `//lowercase comments`, file-scoped namespaces, `#region` using blocks, XML docs on public members.
- **Commit strategy is at-end.** Implementers do NOT commit. They leave all changes in the working tree. Task 15 makes the commits.
- Shared checkouts: never touch `C:/Users/Michael/Documents/GitHub/Chaos.Client`, its `Chaos-Server` submodule or `Unora` main trees. Work only in the three worktrees below.

**User decisions (already made):**
- Delivery: show the original at once, then swap chat-log line and bubble in place; hover shows the original.
- Channels: say, shout, whisper, group, guild, `!global`, `!trade`.
- Whispers: the reader decides; whisper translations stay in memory only.
- Cache: memory dictionary + append-only `.jsonl`; no Redis, no database.
- Translated lines may be up to double the normal limit.
- Tag: `[Kor]` / `[영어]` at the start of the chat-log line only.
- Game names stay English; the list is built from game data; a small JSON maps Korean spellings to English names.
- Per-player limit: 5,000 characters per day.
- One provider request per line, shared by all readers; none when no reader wants it.
- Everything runs inside the game server; the server adds a line id to chat packets.
- Providers chained Azure → DeepL → Google; switch at 90% and on a quota error. It must be fast.

**Implementation choices made while planning (within the spec's intent):**
- A single-word game name is protected only when the player typed it with a capital first letter ("Wolf", not "wolf"). Multi-word names match case-insensitively. Korean spellings from the JSON match anywhere in the line, because Korean attaches particles to the end of a word (`밀레스에`). This stops every "apple" or "wolf" in ordinary English from staying English in the Korean output.
- Runs of the same Hangul jamo are shortened to two before the shortcut lookup, so `ㅋㅋㅋㅋㅋ` hits the `ㅋㅋ` entry.
- DeepL uses `api-free.deepl.com` when the key ends in `:fx`, and `api.deepl.com` otherwise.
- Say/shout capture happens in `WorldServer.OnPublicMessage`, through a new `Aisling.ShowChatMessage` that returns the players who got the line. The translation service stays out of the world models.
- At startup the service makes one free request per provider to open its connection (Azure `GET /languages`, DeepL `GET /v2/usage`), so the first real translation doesn't pay for the TLS handshake.

## Paths used below

| Name | Path |
|---|---|
| `$SRV` | `C:/Users/Michael/Documents/GitHub/worktrees/chat-translation-server` (Chaos-Server, branch `feat/chat-translation`) |
| `$CLI` | `C:/Users/Michael/Documents/GitHub/worktrees/chat-translation-client` (Chaos.Client, branch `feat/chat-translation`) |
| `$UNO` | `C:/Users/Michael/Documents/GitHub/worktrees/chat-translation-unora` (Unora, branch `feat/chat-translation`) |

The worktrees already exist.

Commands:

- Server tests (one class): `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<ClassName>/*"`
- Networking tests (one class): `cd $SRV && dotnet run --project Tests/Chaos.Networking.Tests/Chaos.Networking.Tests.csproj -- --treenode-filter "/*/*/<ClassName>/*"`
- Messaging tests: `cd $SRV && dotnet run --project Tests/Chaos.Messaging.Tests/Chaos.Messaging.Tests.csproj`
- Client tests (one class): `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/<ClassName>/*"`
- Client build: `cd $CLI && UnoraServerPath=$SRV dotnet build Chaos.Client/Chaos.Client.csproj` (the `.slnx` fails in a worktree).

Use `dotnet run`, never `dotnet test`. Filter with `--treenode-filter`, never `--filter`. Never build two of these at the same time: they share protocol project outputs. Serena's edit tools work on paths under `C:\Users\Michael\Documents\GitHub\worktrees\...`.

Known failing server test on master (ignore it): `AislingTests.OnItemDroppedOn_ShouldAddStackableItem_WhenCountIsPositive`.

---

### Task 1: Protocol — language enum, line ids, ChatTranslation packet, option byte

**Goal:** All wire changes on the server side, with converter tests.

**Files:**
- Modify: `$SRV/Chaos.DarkAges/Definitions/Enums.cs` (add `ChatLanguage`; add `UserOption.ChatLanguage = 31`)
- Modify: `$SRV/Chaos.DarkAges/Definitions/CONSTANTS.cs` (`CLIENT_VERSION = 761`)
- Modify: `$SRV/Chaos.Networking.Abstractions/Definitions/Enums.cs` (`ServerOpCode.ChatTranslation = 140`)
- Modify: `$SRV/Chaos.Networking/Entities/Server/DisplayPublicMessageArgs.cs`, `ServerMessageArgs.cs`, `UserOptionsArgs.cs`
- Modify: `$SRV/Chaos.Networking/Converters/Server/DisplayPublicMessageConverter.cs`, `ServerMessageConverter.cs`, `UserOptionsConverter.cs`
- Create: `$SRV/Chaos.Networking/Entities/Server/ChatTranslationArgs.cs`, `$SRV/Chaos.Networking/Converters/Server/ChatTranslationConverter.cs`
- Test: `$SRV/Tests/Chaos.Networking.Tests/Converters/Server/DisplayPublicMessageConverterTests.cs`, `ServerMessageConverterTests.cs`, `UserOptionsConverterTests.cs`, new `ChatTranslationConverterTests.cs`

**Acceptance Criteria:**
- [ ] `LineId == 0` packets serialize to exactly today's bytes (existing tests still pass).
- [ ] `LineId != 0` round-trips with and without tags.
- [ ] `ChatTranslationArgs` round-trips (line id, source language, Korean text, tags).
- [ ] `UserOptionsArgs` with only `ChatLanguage = Korean` writes `0, false, 2` after the 9 booleans. A 9-boolean legacy packet reads `ChatLanguage = 0`. A packet with the filter pair but no language byte reads `ChatLanguage = 0`.
- [ ] Converters are discovered the same way as existing ones (check how `DisplayPublicMessageConverter` is registered: if by assembly scan, nothing to do; if by list, add the new one).

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Networking.Tests/Chaos.Networking.Tests.csproj` → all pass

**Steps:**

- [ ] **Step 1: Add the enums and version**

In `Chaos.DarkAges/Definitions/Enums.cs`, after the `UserOption` enum:

```csharp
/// <summary>
///     The language a player reads chat in, for chat translation. Off shows every line as typed.
/// </summary>
public enum ChatLanguage : byte
{
    Off = 0,
    English = 1,
    Korean = 2
}
```

Add `ChatLanguage = 31` as the last member of `UserOption` (after `DamageNumbersMyOutputOnly = 30`). In `ServerOpCode` add `ChatTranslation = 140` after `GuildEmblemDesign = 139`, with a `/// <summary>` like its neighbors. Set `CLIENT_VERSION = 761`.

- [ ] **Step 2: Write failing converter tests**

Add to `DisplayPublicMessageConverterTests` (reuse its `RoundTrip`/`Serialize` helpers):

```csharp
[Test]
public void RoundTrips_LineId_WithoutTags()
{
    var args = new DisplayPublicMessageArgs
    {
        Message = "Haneul: 안녕",
        PublicMessageType = PublicMessageType.Normal,
        SourceId = 9,
        LineId = 1234
    };

    RoundTrip(args).Should().BeEquivalentTo(args);
}

[Test]
public void RoundTrips_LineId_WithTags()
{
    var args = new DisplayPublicMessageArgs
    {
        Message = "that is zorp",
        PublicMessageType = PublicMessageType.Shout,
        SourceId = 7,
        LineId = 77,
        Tags = [new ChatTag(Start: 8, Length: 4, RuleId: "profanity.zorp_001", Category: 0)]
    };

    RoundTrip(args).Should().BeEquivalentTo(args);
}

[Test]
public void Serializes_LineId_AfterEmptyTagSection()
{
    var bytes = Serialize(new DisplayPublicMessageArgs { Message = "hi", SourceId = 1, LineId = 5 });

    //type (1) + id (4) + string8 (1 + 2) + tag count (2, zero) + line id (4)
    bytes.Length.Should().Be(1 + 4 + 3 + 2 + 4);
}
```

Add the same three tests to `ServerMessageConverterTests`, adapted to `ServerMessageArgs` (`ServerMessageType.Whisper`, `String16`: the length prefix is 2 bytes, so the last test expects `1 + (2 + 2) + 2 + 4`).

Create `ChatTranslationConverterTests.cs` (namespace `Chaos.Networking.Tests`, same helper shape as the other converter tests):

```csharp
public sealed class ChatTranslationConverterTests
{
    [Test]
    public void RoundTrips_KoreanText_WithTags()
    {
        var args = new ChatTranslationArgs
        {
            LineId = 42,
            SourceLanguage = ChatLanguage.English,
            Text = "밀레스 가는 길 아는 사람?",
            Tags = [new ChatTag(Start: 0, Length: 3, RuleId: "r1", Category: 1)]
        };

        RoundTrip(args).Should().BeEquivalentTo(args);
    }

    [Test]
    public void RoundTrips_WithoutTags()
    {
        var args = new ChatTranslationArgs { LineId = 1, SourceLanguage = ChatLanguage.Korean, Text = "Does anyone know the way?" };

        RoundTrip(args).Should().BeEquivalentTo(args);
    }

    private static ChatTranslationArgs RoundTrip(ChatTranslationArgs args)
    {
        var converter = new ChatTranslationConverter();
        var writer = new SpanWriter(Encoding.GetEncoding(949));
        converter.Serialize(ref writer, args);
        var reader = new SpanReader(Encoding.GetEncoding(949), writer.ToSpan());

        return converter.Deserialize(ref reader);
    }
}
```

(Match the exact `SpanWriter`/`SpanReader` construction the existing converter tests use. If they build the encoding differently, copy theirs.)

Add to `UserOptionsConverterTests`:

```csharp
[Test]
public void Serializes_Language_AfterFilterPair()
{
    var bytes = Serialize(new UserOptionsArgs { ChatLanguage = (byte)ChatLanguage.Korean });

    bytes.Length.Should().Be(9 + 1 + 1 + 1);
    bytes[9].Should().Be(0);   //filter mode written as default
    bytes[10].Should().Be(0);  //configured flag written as default
    bytes[11].Should().Be(2);  //language
}

[Test]
public void Deserializes_FilterPairWithoutLanguage_AsOff()
{
    var bytes = Serialize(new UserOptionsArgs { ChatFilterMode = 2 });

    Deserialize(bytes).ChatLanguage.Should().Be(0);
}

[Test]
public void RoundTrips_AllTrailerValues()
{
    var args = new UserOptionsArgs { ChatFilterMode = 1, HasConfiguredChatFilter = true, ChatLanguage = 1 };

    Deserialize(Serialize(args)).Should().BeEquivalentTo(args);
}
```

(Adapt helper names to the file's existing helpers.)

- [ ] **Step 3: Run the tests to see them fail to compile**

Run: `cd $SRV && dotnet build Tests/Chaos.Networking.Tests/Chaos.Networking.Tests.csproj`
Expected: errors for `LineId`, `ChatTranslationArgs`, `ChatLanguage` on `UserOptionsArgs`.

- [ ] **Step 4: Implement**

`DisplayPublicMessageArgs` and `ServerMessageArgs` get:

```csharp
    /// <summary>
    ///     The chat line's id for chat translation, or 0 when the line has none. A later ChatTranslation packet names
    ///     the line by this id. Written after the tag section, which is always written when the id is set.
    /// </summary>
    public uint LineId { get; set; }
```

Converter changes (both). Deserialize:

```csharp
            Tags = reader.EndOfSpan ? [] : ChatTagCodec.ReadTags(ref reader),
            LineId = reader.EndOfSpan ? 0u : reader.ReadUInt32()
```

(Because object initializers run in order, the tag read happens before the line id read. Keep `Tags` before `LineId`.)

Serialize tail:

```csharp
        //a line id forces the tag section out (count may be 0) so the id always sits at the same place; with no id
        //and no tags the packet stays byte-identical to the legacy layout
        if ((args.Tags.Count != 0) || (args.LineId != 0))
            ChatTagCodec.WriteTags(ref writer, args.Tags);

        if (args.LineId != 0)
            writer.WriteUInt32(args.LineId);
```

`ChatTranslationArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.ChatTranslation" /> packet: the translated body of an
///     earlier chat line, which the client swaps in for that line.
/// </summary>
public sealed record ChatTranslationArgs : IPacketSerializable
{
    /// <summary>The line id the original chat packet carried.</summary>
    public uint LineId { get; set; }

    /// <summary>The language the line was typed in.</summary>
    public ChatLanguage SourceLanguage { get; set; }

    /// <summary>The translated body, without the name, channel prefix or color code.</summary>
    public string Text { get; set; } = null!;

    /// <summary>Chat filter tags for <see cref="Text" />, body-relative.</summary>
    public IReadOnlyList<ChatTag> Tags { get; set; } = [];
}
```

`ChatTranslationConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>
///     Serializes a <see cref="ChatTranslationArgs" />. Layout: line id (uint), source language (byte), text (String16),
///     tag section (always written).
/// </summary>
public sealed class ChatTranslationConverter : PacketConverterBase<ChatTranslationArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.ChatTranslation;

    /// <inheritdoc />
    public override ChatTranslationArgs Deserialize(ref SpanReader reader)
        => new()
        {
            LineId = reader.ReadUInt32(),
            SourceLanguage = (ChatLanguage)reader.ReadByte(),
            Text = reader.ReadString16(),
            Tags = ChatTagCodec.ReadTags(ref reader)
        };

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, ChatTranslationArgs args)
    {
        writer.WriteUInt32(args.LineId);
        writer.WriteByte((byte)args.SourceLanguage);
        writer.WriteString16(args.Text);
        ChatTagCodec.WriteTags(ref writer, args.Tags);
    }
}
```

`UserOptionsArgs`: add `public byte ChatLanguage { get; set; }` with a summary ("0 Off, 1 English, 2 Korean; see ChatLanguage"). `UserOptionsConverter`:

```csharp
            ChatFilterMode = reader.EndOfSpan ? (byte)0 : reader.ReadByte(),
            HasConfiguredChatFilter = reader.EndOfSpan ? false : reader.ReadBoolean(),
            ChatLanguage = reader.EndOfSpan ? (byte)0 : reader.ReadByte()
```

```csharp
        //the chat filter pair goes out whenever any trailer value is non-default, so a language byte can never land
        //where an older reader expects the filter mode
        if ((args.ChatFilterMode != 0) || args.HasConfiguredChatFilter || (args.ChatLanguage != 0))
        {
            writer.WriteByte(args.ChatFilterMode);
            writer.WriteBoolean(args.HasConfiguredChatFilter);

            if (args.ChatLanguage != 0)
                writer.WriteByte(args.ChatLanguage);
        }
```

- [ ] **Step 5: Run the tests**

Run: `cd $SRV && dotnet run --project Tests/Chaos.Networking.Tests/Chaos.Networking.Tests.csproj`
Expected: all pass.

```json:metadata
{"files": ["$SRV/Chaos.DarkAges/Definitions/Enums.cs", "$SRV/Chaos.DarkAges/Definitions/CONSTANTS.cs", "$SRV/Chaos.Networking.Abstractions/Definitions/Enums.cs", "$SRV/Chaos.Networking/Entities/Server/DisplayPublicMessageArgs.cs", "$SRV/Chaos.Networking/Entities/Server/ServerMessageArgs.cs", "$SRV/Chaos.Networking/Entities/Server/UserOptionsArgs.cs", "$SRV/Chaos.Networking/Entities/Server/ChatTranslationArgs.cs", "$SRV/Chaos.Networking/Converters/Server/DisplayPublicMessageConverter.cs", "$SRV/Chaos.Networking/Converters/Server/ServerMessageConverter.cs", "$SRV/Chaos.Networking/Converters/Server/UserOptionsConverter.cs", "$SRV/Chaos.Networking/Converters/Server/ChatTranslationConverter.cs"], "verifyCommand": "cd $SRV && dotnet run --project Tests/Chaos.Networking.Tests/Chaos.Networking.Tests.csproj", "acceptanceCriteria": ["LineId 0 packets unchanged", "LineId round-trips with and without tags", "ChatTranslationArgs round-trips", "UserOptions language trailer order and legacy reads", "new converter registered"], "modelTier": "standard"}
```

---

### Task 2: Server player option — ChatLanguage on UserOptions

**Goal:** The option is stored, saved, loaded, set through OptionToggle and echoed in `UserOptions`.

**Files:**
- Create: `$SRV/Chaos/Collections/ChatLanguages.cs`
- Modify: `$SRV/Chaos/Collections/UserOptions.cs` (property + `TrySetChatFilterOption` also handles `ChatLanguage`)
- Modify: `$SRV/Chaos.Schemas/Aisling/UserOptionsSchema.cs` (string property)
- Modify: `$SRV/Chaos/Services/MapperProfiles/UserOptionsMapperProfile.cs`
- Modify: `$SRV/Chaos/Networking/ChaosWorldClient.cs` (the `SendUserOptions` args build near line 808)
- Test: `$SRV/Tests/Chaos.Tests/ChatLanguagesTests.cs`, and extend the existing UserOptions mapper test if one exists (search `UserOptionsMapperProfile` in `Tests/Chaos.Tests`)

**Acceptance Criteria:**
- [ ] `ChatLanguages.ToWireValue("Korean") == 2`; unknown string → 0; `FromWireValue(3) == null`.
- [ ] `UserOptions.TrySetChatFilterOption(UserOption.ChatLanguage, 1)` returns true and stores `"English"`; value 9 returns true and leaves it unchanged.
- [ ] A schema with no `ChatLanguage` maps to `"Off"`; mapping round-trips `"Korean"`.
- [ ] `SendUserOptions` fills `UserOptionsArgs.ChatLanguage`.

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ChatLanguagesTests/*"` → pass

**Steps:**

- [ ] **Step 1: Write the failing test**

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Tests;

public sealed class ChatLanguagesTests
{
    [Test]
    public void ToWireValue_MapsNames()
    {
        ChatLanguages.ToWireValue("Off").Should().Be(0);
        ChatLanguages.ToWireValue("English").Should().Be(1);
        ChatLanguages.ToWireValue("Korean").Should().Be(2);
        ChatLanguages.ToWireValue("Klingon").Should().Be(0);
    }

    [Test]
    public void FromWireValue_RejectsOutOfRange() => ChatLanguages.FromWireValue(3).Should().BeNull();

    [Test]
    public void Parse_ReturnsEnum() => ChatLanguages.Parse("Korean").Should().Be(ChatLanguage.Korean);

    [Test]
    public void TrySetChatFilterOption_SetsLanguage()
    {
        var options = new UserOptions();

        options.TrySetChatFilterOption(UserOption.ChatLanguage, 1).Should().BeTrue();
        options.ChatLanguage.Should().Be("English");

        options.TrySetChatFilterOption(UserOption.ChatLanguage, 9).Should().BeTrue();
        options.ChatLanguage.Should().Be("English");
    }
}
```

- [ ] **Step 2: Run it to see it fail** (compile errors for `ChatLanguages`, `UserOptions.ChatLanguage`).

- [ ] **Step 3: Implement**

`ChatLanguages.cs` (mirror `ChatFilterModes.cs`):

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Collections;

/// <summary>
///     Maps the stored chat translation language name to its wire value (<see cref="ChatLanguage" />).
/// </summary>
public static class ChatLanguages
{
    /// <summary>Names in wire order. Index IS the wire value.</summary>
    public static readonly string[] Names = ["Off", "English", "Korean"];

    /// <summary>Maps a stored name to its wire index. Unknown names fail closed to Off (0).</summary>
    public static byte ToWireValue(string name)
    {
        var index = Array.IndexOf(Names, name);

        return (byte)(index < 0 ? 0 : index);
    }

    /// <summary>Maps a wire index to its name. Out-of-range indices map to null (caller ignores the edit).</summary>
    public static string? FromWireValue(byte value) => value < Names.Length ? Names[value] : null;

    /// <summary>The stored name as the enum. Unknown names are Off.</summary>
    public static ChatLanguage Parse(string name) => (ChatLanguage)ToWireValue(name);
}
```

`UserOptions`: add, next to `ChatFilterMode`:

```csharp
    /// <summary>
    ///     Chat translation language name (Off, English, Korean). Stored as text like <see cref="ChatFilterMode" />;
    ///     synced as <see cref="UserOption.ChatLanguage" />.
    /// </summary>
    public string ChatLanguage { get; set; } = "Off";
```

and a case in `TrySetChatFilterOption` (update its summary to say it covers chat translation too):

```csharp
            case UserOption.ChatLanguage:
                if (ChatLanguages.FromWireValue(value) is { } language)
                    ChatLanguage = language;

                return true;
```

`UserOptionsSchema`: add after `HasConfiguredChatFilter`:

```csharp
    /// <summary>
    ///     Chat translation language name (Off, English, Korean). Defaults to Off for legacy saves.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string ChatLanguage { get; set; } = "Off";
```

Map it both ways in `UserOptionsMapperProfile` like `ChatFilterMode`. In `ChaosWorldClient` where `ChatFilterMode = ChatFilterModes.ToWireValue(options.ChatFilterMode)` is set, add `ChatLanguage = ChatLanguages.ToWireValue(options.ChatLanguage)`.

- [ ] **Step 4: Run the tests** (the class above, plus any `UserOptionsMapperProfile` test class you touched). Expected: pass.

```json:metadata
{"files": ["$SRV/Chaos/Collections/ChatLanguages.cs", "$SRV/Chaos/Collections/UserOptions.cs", "$SRV/Chaos.Schemas/Aisling/UserOptionsSchema.cs", "$SRV/Chaos/Services/MapperProfiles/UserOptionsMapperProfile.cs", "$SRV/Chaos/Networking/ChaosWorldClient.cs", "$SRV/Tests/Chaos.Tests/ChatLanguagesTests.cs"], "verifyCommand": "cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ChatLanguagesTests/*\"", "acceptanceCriteria": ["wire mapping", "TrySetChatFilterOption handles ChatLanguage", "schema default Off and round-trip", "SendUserOptions fills ChatLanguage"], "modelTier": "mechanical"}
```

---

### Task 3: Translation text helpers — detection, keys, cleanup, cutting

**Goal:** Pure, tested helpers the service and callers use.

**Files:**
- Create: `$SRV/Chaos/Services/ChatTranslation/ChatTranslationText.cs`
- Test: `$SRV/Tests/Chaos.Tests/ChatTranslation/ChatTranslationTextTests.cs`

**Acceptance Criteria:**
- [ ] `DetectLanguage`: `"혹시 밀레스?"` → Korean; `"hello"` → English; `"ㅋㅋㅋ"`, `"123!!"`, `"a"`, `""` → null; `"gg 밀레스"` → Korean.
- [ ] `NormalizeKey`: English trimmed, spaces collapsed, lowercased; Korean keeps case; `ㅋㅋㅋㅋㅋ` → `ㅋㅋ`.
- [ ] `CleanOutput`: `&quot;`/`&amp;`/`&#39;` decoded; curly quotes → ASCII; `…` → `...`; an emoji is removed; Hangul kept.
- [ ] `CutToLength`: under the limit unchanged; cuts at a space within the last 12 characters; hard cut otherwise; never returns trailing spaces.
- [ ] Body limit helpers return the numbers in Global Constraints.

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ChatTranslationTextTests/*"` → pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Services.ChatTranslation;
using FluentAssertions;
#endregion

namespace Chaos.Tests.ChatTranslation;

public sealed class ChatTranslationTextTests
{
    [Test]
    [Arguments("혹시 밀레스 가는 길 아는 사람?", ChatLanguage.Korean)]
    [Arguments("gg 밀레스", ChatLanguage.Korean)]
    [Arguments("hello there", ChatLanguage.English)]
    [Arguments("ok", ChatLanguage.English)]
    public void DetectLanguage_Detects(string body, ChatLanguage expected)
        => ChatTranslationText.DetectLanguage(body).Should().Be(expected);

    [Test]
    [Arguments("ㅋㅋㅋ")]
    [Arguments("123!!")]
    [Arguments("a")]
    [Arguments("")]
    [Arguments(":)")]
    public void DetectLanguage_ReturnsNull_ForNothingToTranslate(string body)
        => ChatTranslationText.DetectLanguage(body).Should().BeNull();

    [Test]
    public void NormalizeKey_English_LowercasesAndCollapses()
        => ChatTranslationText.NormalizeKey("  Hello   THERE ", ChatLanguage.English).Should().Be("hello there");

    [Test]
    public void NormalizeKey_Korean_ShortensJamoRuns()
        => ChatTranslationText.NormalizeKey("ㅋㅋㅋㅋㅋ", ChatLanguage.Korean).Should().Be("ㅋㅋ");

    [Test]
    public void NormalizeKey_Korean_KeepsLatinCase()
        => ChatTranslationText.NormalizeKey("GG  밀레스", ChatLanguage.Korean).Should().Be("GG 밀레스");

    [Test]
    public void CleanOutput_DecodesAndSimplifies()
        => ChatTranslationText.CleanOutput("“Don&#39;t” &amp; wait…").Should().Be("\"Don't\" & wait...");

    [Test]
    public void CleanOutput_DropsCharactersCp949CannotEncode()
        => ChatTranslationText.CleanOutput("안녕 😀 친구").Should().Be("안녕 친구");

    [Test]
    public void CutToLength_LeavesShortText() => ChatTranslationText.CutToLength("short", 10).Should().Be("short");

    [Test]
    public void CutToLength_CutsAtRecentSpace()
        => ChatTranslationText.CutToLength("does anyone know the way", 20).Should().Be("does anyone know the");

    [Test]
    public void CutToLength_HardCutsLongWord()
        => ChatTranslationText.CutToLength("abcdefghijklmnopqrstuvwxyz", 10).Should().Be("abcdefghij");

    [Test]
    public void BodyLimits_MatchSpec()
    {
        ChatTranslationText.PublicBodyLimit("Haneul").Should().Be(134 - 6 - 2);
        ChatTranslationText.WhisperBodyLimit("Haneul").Should().Be(180 - 6 - 4);
        ChatTranslationText.ChannelBodyLimit("Haneul").Should().Be(180 - 6 - 20);
    }
}
```

- [ ] **Step 2: Run to see it fail** (type missing).

- [ ] **Step 3: Implement**

```csharp
#region
using System.Net;
using System.Text;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>
///     Pure text rules for chat translation: which language a line is in, the cache key, cleaning a provider's reply so
///     the game can send it, and the length limits of a translated line.
/// </summary>
public static class ChatTranslationText
{
    private static readonly Encoding Cp949 = Encoding.GetEncoding(
        949,
        new EncoderReplacementFallback(string.Empty),
        new DecoderReplacementFallback(string.Empty));

    /// <summary>
    ///     Korean when the body holds a Hangul syllable; English when it holds two or more ASCII letters; otherwise null
    ///     (numbers, punctuation, jamo-only lines and emoticons have nothing to translate).
    /// </summary>
    public static ChatLanguage? DetectLanguage(string body)
    {
        var letters = 0;

        foreach (var c in body)
        {
            if (c is >= '\uAC00' and <= '\uD7A3')
                return ChatLanguage.Korean;

            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'))
                letters++;
        }

        return letters >= 2 ? ChatLanguage.English : null;
    }

    /// <summary>The language a line is translated into.</summary>
    public static ChatLanguage TargetOf(ChatLanguage source) => source == ChatLanguage.Korean ? ChatLanguage.English : ChatLanguage.Korean;

    /// <summary>The cache direction label ("ko-en" or "en-ko").</summary>
    public static string Direction(ChatLanguage source) => source == ChatLanguage.Korean ? "ko-en" : "en-ko";

    /// <summary>
    ///     The cache and shortcut key: trimmed, runs of whitespace collapsed to one space, lowercased when English, and runs
    ///     of one Hangul jamo longer than two shortened to two ("ㅋㅋㅋㅋ" → "ㅋㅋ").
    /// </summary>
    public static string NormalizeKey(string body, ChatLanguage source)
    {
        var builder = new StringBuilder(body.Length);
        var pendingSpace = false;
        var run = 0;
        var last = '\0';

        foreach (var c in body.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                last = '\0';
                run = 0;

                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            run = c == last ? run + 1 : 1;
            last = c;

            //a jamo repeated for emphasis means the same thing at any length
            if (c is >= '\u3131' and <= '\u318E' && (run > 2))
                continue;

            builder.Append(c);
        }

        var key = builder.ToString();

        return source == ChatLanguage.English ? key.ToLowerInvariant() : key;
    }

    /// <summary>
    ///     Makes a provider's reply sendable: decodes HTML entities, swaps typographic quotes, dashes and ellipses for
    ///     ASCII, drops any character code page 949 cannot encode, and collapses whitespace.
    /// </summary>
    public static string CleanOutput(string text)
    {
        var decoded = WebUtility.HtmlDecode(text)
                                .Replace('\u2018', '\'')
                                .Replace('\u2019', '\'')
                                .Replace('\u201C', '"')
                                .Replace('\u201D', '"')
                                .Replace('\u2013', '-')
                                .Replace('\u2014', '-')
                                .Replace('\u00A0', ' ')
                                .Replace("\u2026", "...");

        var encodable = Cp949.GetString(Cp949.GetBytes(decoded));

        return string.Join(' ', encodable.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    ///     Cuts <paramref name="text" /> to <paramref name="max" /> characters, at a space when one falls in the last 12
    ///     characters of the cut.
    /// </summary>
    public static string CutToLength(string text, int max)
    {
        if (text.Length <= max)
            return text;

        var cut = text[..max];
        var space = cut.LastIndexOf(' ');

        if ((space > 0) && (space >= max - 12))
            cut = cut[..space];

        return cut.TrimEnd();
    }

    /// <summary>Longest translated body for a say/shout line: double the 67-character line, minus "Name: ".</summary>
    public static int PublicBodyLimit(string speakerName) => 134 - speakerName.Length - 2;

    /// <summary>Longest translated body for a whisper: double the 90-character line, minus "[Name]: ".</summary>
    public static int WhisperBodyLimit(string speakerName) => 180 - speakerName.Length - 4;

    /// <summary>Longest translated body for a channel line: double the 90-character line, minus about "{=x[!channel] Name: ".</summary>
    public static int ChannelBodyLimit(string speakerName) => 180 - speakerName.Length - 20;
}
```

Note: the `CleanOutput` emoji test relies on the code-page provider being registered in the test run. `Tests/Chaos.Tests/ServerSetup.cs` has a module initializer: check it calls `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`. If it doesn't, add that call there.

- [ ] **Step 4: Run the tests** → pass.

```json:metadata
{"files": ["$SRV/Chaos/Services/ChatTranslation/ChatTranslationText.cs", "$SRV/Tests/Chaos.Tests/ChatTranslation/ChatTranslationTextTests.cs"], "verifyCommand": "cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ChatTranslationTextTests/*\"", "acceptanceCriteria": ["DetectLanguage rules", "NormalizeKey rules", "CleanOutput rules", "CutToLength rules", "body limits"], "modelTier": "mechanical"}
```

---

### Task 4: Name protection and the translation config file

**Goal:** Split a body into plain and protected segments, and load the shortcut and Korean-name tables.

**Files:**
- Create: `$SRV/Chaos/Services/ChatTranslation/ProtectedText.cs`
- Create: `$SRV/Chaos/Services/ChatTranslation/NameProtector.cs`
- Create: `$SRV/Chaos/Services/ChatTranslation/ChatTranslationConfig.cs`
- Create: `$SRV/Chaos/Services/ChatTranslation/GameNameSource.cs` (interface `IGameNameSource` + implementation)
- Test: `$SRV/Tests/Chaos.Tests/ChatTranslation/NameProtectorTests.cs`, `ChatTranslationConfigTests.cs`

**Acceptance Criteria:**
- [ ] `"I'm in Mileth now"` with names `{"Mileth"}` → segments `["I'm in ", "Mileth"*, " now"]` (* = protected).
- [ ] Single-word names need a capital first letter in the body: `"a wolf"` with `{"Wolf"}` → one plain segment; `"a Wolf"` → protected `"Wolf"`.
- [ ] Multi-word names match case-insensitively and use the canonical spelling: `"got a holy scale"` with `{"Holy Scale"}` → protected `"Holy Scale"`.
- [ ] Whole words only: `"Milethian"` does not match `"Mileth"`.
- [ ] Longest match wins: names `{"Holy", "Holy Scale"}` on `"Holy Scale"` → one protected `"Holy Scale"`.
- [ ] Korean map matches inside words: `"밀레스에 가자"` with `{"밀레스": "Mileth"}` → protected `"Mileth"`, then plain `"에 가자"`.
- [ ] `ChatTranslationConfig.Load` on a missing file returns empty tables; on a valid file normalizes shortcut keys with `NormalizeKey`.
- [ ] `GameNameSource` returns template and map names (3+ characters, distinct) plus online player names, and rebuilds the template list at most every 10 minutes.

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/NameProtectorTests/*"` → pass, and the same for `ChatTranslationConfigTests`

**Steps:**

- [ ] **Step 1: Write the failing tests**

```csharp
#region
using Chaos.Services.ChatTranslation;
using FluentAssertions;
#endregion

namespace Chaos.Tests.ChatTranslation;

public sealed class NameProtectorTests
{
    private static readonly Dictionary<string, string> NoKorean = [];

    [Test]
    public void Protects_SingleWordName_WhenCapitalized()
    {
        var text = NameProtector.Protect("I'm in Mileth now", ["Mileth"], NoKorean);

        text.Segments.Should().Equal(new TextSegment("I'm in ", false), new TextSegment("Mileth", true), new TextSegment(" now", false));
    }

    [Test]
    public void Skips_SingleWordName_WhenLowercase()
        => NameProtector.Protect("a wolf", ["Wolf"], NoKorean).Segments.Should().Equal(new TextSegment("a wolf", false));

    [Test]
    public void Protects_MultiWordName_AnyCase_WithCanonicalSpelling()
        => NameProtector.Protect("got a holy scale", ["Holy Scale"], NoKorean)
                        .Segments.Should().Equal(new TextSegment("got a ", false), new TextSegment("Holy Scale", true));

    [Test]
    public void Matches_WholeWordsOnly()
        => NameProtector.Protect("Milethian", ["Mileth"], NoKorean).Segments.Should().Equal(new TextSegment("Milethian", false));

    [Test]
    public void Prefers_LongestMatch()
        => NameProtector.Protect("Holy Scale", ["Holy", "Holy Scale"], NoKorean)
                        .Segments.Should().Equal(new TextSegment("Holy Scale", true));

    [Test]
    public void Maps_KoreanSpelling_InsideWord()
        => NameProtector.Protect("밀레스에 가자", [], new Dictionary<string, string> { ["밀레스"] = "Mileth" })
                        .Segments.Should().Equal(new TextSegment("Mileth", true), new TextSegment("에 가자", false));

    [Test]
    public void PlainText_HasOneSegment()
        => NameProtector.Protect("hello", [], NoKorean).Segments.Should().Equal(new TextSegment("hello", false));
}
```

```csharp
#region
using Chaos.Services.ChatTranslation;
using FluentAssertions;
#endregion

namespace Chaos.Tests.ChatTranslation;

public sealed class ChatTranslationConfigTests
{
    [Test]
    public void Load_MissingFile_IsEmpty()
    {
        var config = ChatTranslationConfig.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));

        config.KoreanToEnglish.Should().BeEmpty();
        config.EnglishToKorean.Should().BeEmpty();
        config.KoreanNames.Should().BeEmpty();
    }

    [Test]
    public void Load_NormalizesShortcutKeys()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        File.WriteAllText(
            path,
            """
            { "Shortcuts": { "KoreanToEnglish": { "ㅋㅋㅋ": "lol" }, "EnglishToKorean": { "Thank  You": "감사합니다" } },
              "KoreanNames": { "밀레스": "Mileth" } }
            """);

        try
        {
            var config = ChatTranslationConfig.Load(path);

            config.KoreanToEnglish.Should().ContainKey("ㅋㅋ");
            config.EnglishToKorean.Should().ContainKey("thank you");
            config.KoreanNames["밀레스"].Should().Be("Mileth");
        } finally
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: Run to see them fail.**

- [ ] **Step 3: Implement**

`ProtectedText.cs`:

```csharp
namespace Chaos.Services.ChatTranslation;

/// <summary>One piece of a chat body: plain text to translate, or a protected name to keep as is.</summary>
public readonly record struct TextSegment(string Text, bool Protected);

/// <summary>A chat body split into plain and protected segments, ready for a provider to mark up.</summary>
public sealed record ProtectedText(IReadOnlyList<TextSegment> Segments)
{
    /// <summary>The body with every segment joined, no markup.</summary>
    public string ToPlainString() => string.Concat(Segments.Select(segment => segment.Text));
}
```

`NameProtector.cs`:

```csharp
namespace Chaos.Services.ChatTranslation;

/// <summary>
///     Finds game names in a chat body so providers leave them untranslated. English names match whole words; a
///     single-word name only when typed with a capital first letter, so ordinary words ("wolf", "apple") still translate.
///     Korean spellings from the config match anywhere, because Korean attaches particles to the end of a word, and are
///     replaced by their English name.
/// </summary>
public static class NameProtector
{
    public static ProtectedText Protect(
        string body,
        IReadOnlyCollection<string> englishNames,
        IReadOnlyDictionary<string, string> koreanNames)
    {
        var matches = new List<(int Start, int Length, string Replacement)>();

        foreach ((var korean, var english) in koreanNames)
            for (var at = body.IndexOf(korean, StringComparison.Ordinal); at >= 0; at = body.IndexOf(korean, at + korean.Length, StringComparison.Ordinal))
                matches.Add((at, korean.Length, english));

        foreach (var name in englishNames)
        {
            if (name.Length == 0)
                continue;

            var singleWord = !name.Contains(' ');

            for (var at = body.IndexOf(name, StringComparison.OrdinalIgnoreCase);
                 at >= 0;
                 at = body.IndexOf(name, at + 1, StringComparison.OrdinalIgnoreCase))
            {
                var end = at + name.Length;

                if (((at > 0) && char.IsLetterOrDigit(body[at - 1])) || ((end < body.Length) && char.IsLetterOrDigit(body[end])))
                    continue;

                if (singleWord && !char.IsUpper(body[at]))
                    continue;

                matches.Add((at, name.Length, name));
            }
        }

        if (matches.Count == 0)
            return new ProtectedText([new TextSegment(body, false)]);

        //earliest first, longest first at the same start; a match that overlaps a kept one is dropped
        matches.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Length.CompareTo(a.Length));

        var segments = new List<TextSegment>();
        var cursor = 0;

        foreach ((var start, var length, var replacement) in matches)
        {
            if (start < cursor)
                continue;

            if (start > cursor)
                segments.Add(new TextSegment(body[cursor..start], false));

            segments.Add(new TextSegment(replacement, true));
            cursor = start + length;
        }

        if (cursor < body.Length)
            segments.Add(new TextSegment(body[cursor..], false));

        return new ProtectedText(segments);
    }
}
```

`ChatTranslationConfig.cs`:

```csharp
#region
using System.Text.Json;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>
///     The hand-edited tables in Data/Configuration/ChatTranslation.json: fixed translations for common short chat
///     ("ㅋㅋ", "ty"), and Korean spellings of game names mapped to the English name.
/// </summary>
public sealed class ChatTranslationConfig
{
    public static readonly ChatTranslationConfig Empty = new([], [], []);

    public ChatTranslationConfig(
        IReadOnlyDictionary<string, string> koreanToEnglish,
        IReadOnlyDictionary<string, string> englishToKorean,
        IReadOnlyDictionary<string, string> koreanNames)
    {
        KoreanToEnglish = koreanToEnglish;
        EnglishToKorean = englishToKorean;
        KoreanNames = koreanNames;
    }

    public IReadOnlyDictionary<string, string> KoreanToEnglish { get; }
    public IReadOnlyDictionary<string, string> EnglishToKorean { get; }
    public IReadOnlyDictionary<string, string> KoreanNames { get; }

    /// <summary>Loads the file. A missing file is empty; a malformed one throws, so the caller can log it.</summary>
    public static ChatTranslationConfig Load(string path)
    {
        if (!File.Exists(path))
            return Empty;

        var file = JsonSerializer.Deserialize<ConfigFile>(File.ReadAllText(path)) ?? new ConfigFile();

        return new ChatTranslationConfig(
            Normalize(file.Shortcuts?.KoreanToEnglish, ChatLanguage.Korean),
            Normalize(file.Shortcuts?.EnglishToKorean, ChatLanguage.English),
            new Dictionary<string, string>(file.KoreanNames ?? []));
    }

    private static Dictionary<string, string> Normalize(Dictionary<string, string>? table, ChatLanguage source)
    {
        var result = new Dictionary<string, string>();

        foreach ((var key, var value) in table ?? [])
            result[ChatTranslationText.NormalizeKey(key, source)] = value;

        return result;
    }

    private sealed class ConfigFile
    {
        public ShortcutTables? Shortcuts { get; set; }
        public Dictionary<string, string>? KoreanNames { get; set; }
    }

    private sealed class ShortcutTables
    {
        public Dictionary<string, string>? KoreanToEnglish { get; set; }
        public Dictionary<string, string>? EnglishToKorean { get; set; }
    }
}
```

`GameNameSource.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Models.Templates;
using Chaos.Networking.Abstractions;
using Chaos.Networking.Abstractions;  // adjust: IClientRegistry<T> namespace
using Chaos.Storage.Abstractions;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>The names a translation must leave in English.</summary>
public interface IGameNameSource
{
    IReadOnlyCollection<string> GetNames();
}

/// <summary>
///     Item, spell, skill, monster and merchant template names plus loaded map names, rebuilt at most every 10 minutes,
///     plus the names of players online right now. Names shorter than 3 characters are left out.
/// </summary>
public sealed class GameNameSource(
    ISimpleCache<ItemTemplate> items,
    ISimpleCache<SpellTemplate> spells,
    ISimpleCache<SkillTemplate> skills,
    ISimpleCache<MonsterTemplate> monsters,
    ISimpleCache<MerchantTemplate> merchants,
    ISimpleCache<MapInstance> maps,
    IClientRegistry<IChaosWorldClient> clients,
    TimeProvider time) : IGameNameSource
{
    private static readonly TimeSpan RebuildAfter = TimeSpan.FromMinutes(10);
    private readonly Lock Sync = new();
    private DateTimeOffset BuiltAt = DateTimeOffset.MinValue;
    private string[] TemplateNames = [];

    public IReadOnlyCollection<string> GetNames()
    {
        string[] templateNames;

        lock (Sync)
        {
            if (time.GetUtcNow() - BuiltAt > RebuildAfter)
            {
                TemplateNames = items.Select(t => t.Name)
                                     .Concat(spells.Select(t => t.Name))
                                     .Concat(skills.Select(t => t.Name))
                                     .Concat(monsters.Select(t => t.Name))
                                     .Concat(merchants.Select(t => t.Name))
                                     .Concat(maps.Select(m => m.Name))
                                     .Where(name => !string.IsNullOrWhiteSpace(name) && (name.Length >= 3))
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .ToArray();
                BuiltAt = time.GetUtcNow();
            }

            templateNames = TemplateNames;
        }

        var online = clients.Select(client => client.Aisling?.Name)
                            .Where(name => name is { Length: >= 3 })
                            .Cast<string>();

        return templateNames.Concat(online).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
```

Fix the `using` lines to the real namespaces (look at `EmblemCommand.cs` for `IClientRegistry<IChaosWorldClient>` and at any file using `ISimpleCache<ItemTemplate>`). If a template type's display name property is not `Name`, use the property the game shows players. If `ISimpleCache<MapInstance>` enumeration forces loading every map, drop maps from the list and note it in your report.

- [ ] **Step 4: Run both test classes** → pass.

```json:metadata
{"files": ["$SRV/Chaos/Services/ChatTranslation/ProtectedText.cs", "$SRV/Chaos/Services/ChatTranslation/NameProtector.cs", "$SRV/Chaos/Services/ChatTranslation/ChatTranslationConfig.cs", "$SRV/Chaos/Services/ChatTranslation/GameNameSource.cs", "$SRV/Tests/Chaos.Tests/ChatTranslation/NameProtectorTests.cs", "$SRV/Tests/Chaos.Tests/ChatTranslation/ChatTranslationConfigTests.cs"], "verifyCommand": "cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/NameProtectorTests/*\"", "acceptanceCriteria": ["segments for capitalized single-word names", "lowercase single-word skipped", "multi-word any case canonical", "whole words only", "longest match", "Korean map inside words", "config load missing/valid", "GameNameSource names"], "modelTier": "standard"}
```

---

### Task 5: Providers — Azure, DeepL, Google

**Goal:** Three HTTP providers behind one interface, each with its own markup and error mapping.

**Files:**
- Create: `$SRV/Chaos/Services/ChatTranslation/Providers/IChatTranslationProvider.cs` (interface + `ProviderResult`)
- Create: `$SRV/Chaos/Services/ChatTranslation/Providers/ProviderMarkup.cs`
- Create: `$SRV/Chaos/Services/ChatTranslation/Providers/AzureTranslatorProvider.cs`
- Create: `$SRV/Chaos/Services/ChatTranslation/Providers/DeepLProvider.cs`
- Create: `$SRV/Chaos/Services/ChatTranslation/Providers/GoogleTranslateProvider.cs`
- Test: `$SRV/Tests/Chaos.Tests/ChatTranslation/ProviderTests.cs` (with a fake `HttpMessageHandler`)

**Acceptance Criteria:**
- [ ] Azure request: POST to `https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to=ko&textType=html`, header `Ocp-Apim-Subscription-Key`, `Ocp-Apim-Subscription-Region` only when a region is set, body `[{"Text":"…"}]`, protected text as `<span class="notranslate">…</span>`. Parses text, `detectedLanguage.language`, and `X-metered-usage`. 403 → exhausted (monthly); 429/5xx/other → transient.
- [ ] DeepL request: POST `https://api-free.deepl.com/v2/translate` for keys ending `:fx`, `https://api.deepl.com/v2/translate` otherwise; header `Authorization: DeepL-Auth-Key {key}`; JSON with `text`, `target_lang` (`KO` / `EN-US`), `tag_handling:"xml"`, `ignore_tags:["x"]`, `show_billed_characters:true`; protected as `<x>…</x>`. Parses text, `detected_source_language`, `billed_characters`. 456 → exhausted (monthly); others → transient. `GetUsageAsync` reads `character_count` from `GET /v2/usage`.
- [ ] Google request: POST `https://translation.googleapis.com/language/translate/v2?key={key}`, JSON `q`, `target` (`ko`/`en`), `format:"html"`; protected as `<span translate="no">…</span>`. Parses `data.translations[0].translatedText` and `detectedSourceLanguage`. 403 → exhausted (daily); 429 whose body contains "daily" or "per day" (case-insensitive) → exhausted (daily); other 429/5xx → transient.
- [ ] Plain text is XML-escaped in every request; the returned text has the markup stripped and entities decoded (via `ProviderMarkup.Strip`).
- [ ] Detected language is returned lowercased, first two letters (`"EN"` → `"en"`).

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ProviderTests/*"` → pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

```csharp
#region
using System.Net;
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.Services.ChatTranslation;
using Chaos.Services.ChatTranslation.Providers;
using FluentAssertions;
#endregion

namespace Chaos.Tests.ChatTranslation;

public sealed class ProviderTests
{
    private static readonly ProtectedText Text = new([new TextSegment("going to ", false), new TextSegment("Mileth", true), new TextSegment(" & back", false)]);

    [Test]
    public async Task Azure_BuildsRequest_AndParsesSuccess()
    {
        var handler = new FakeHandler(
            HttpStatusCode.OK,
            """[{"detectedLanguage":{"language":"en","score":1.0},"translations":[{"text":"<span class=\"notranslate\">Mileth</span>에 갔다가 &amp; 돌아와","to":"ko"}]}]""",
            ("X-metered-usage", "55"));
        var provider = new AzureTranslatorProvider(new HttpClient(handler), "key1", "koreacentral");

        var result = await provider.TranslateAsync(Text, ChatLanguage.Korean, CancellationToken.None);

        result.Kind.Should().Be(ProviderResultKind.Success);
        result.Text.Should().Be("Mileth에 갔다가 & 돌아와");
        result.DetectedLanguage.Should().Be("en");
        result.BilledCharacters.Should().Be(55);
        handler.Request!.RequestUri!.ToString().Should().Be("https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to=ko&textType=html");
        handler.Request.Headers.GetValues("Ocp-Apim-Subscription-Key").Should().Equal("key1");
        handler.Request.Headers.GetValues("Ocp-Apim-Subscription-Region").Should().Equal("koreacentral");
        handler.Body.Should().Contain("going to <span class=\\\"notranslate\\\">Mileth</span> &amp; back");
    }

    [Test]
    [Arguments(HttpStatusCode.Forbidden, ProviderResultKind.Exhausted)]
    [Arguments(HttpStatusCode.TooManyRequests, ProviderResultKind.Transient)]
    [Arguments(HttpStatusCode.InternalServerError, ProviderResultKind.Transient)]
    public async Task Azure_MapsErrors(HttpStatusCode status, ProviderResultKind expected)
    {
        var provider = new AzureTranslatorProvider(new HttpClient(new FakeHandler(status, "{}")), "key1", "");

        (await provider.TranslateAsync(Text, ChatLanguage.Korean, CancellationToken.None)).Kind.Should().Be(expected);
    }

    [Test]
    public async Task DeepL_UsesFreeHost_AndParses()
    {
        var handler = new FakeHandler(
            HttpStatusCode.OK,
            """{"translations":[{"detected_source_language":"KO","text":"Going to <x>Mileth</x>","billed_characters":12}]}""");
        var provider = new DeepLProvider(new HttpClient(handler), "abc:fx");

        var result = await provider.TranslateAsync(Text, ChatLanguage.English, CancellationToken.None);

        result.Text.Should().Be("Going to Mileth");
        result.DetectedLanguage.Should().Be("ko");
        result.BilledCharacters.Should().Be(12);
        handler.Request!.RequestUri!.ToString().Should().Be("https://api-free.deepl.com/v2/translate");
        handler.Request.Headers.Authorization!.ToString().Should().Be("DeepL-Auth-Key abc:fx");
        handler.Body.Should().Contain("\"target_lang\":\"EN-US\"").And.Contain("<x>Mileth</x>").And.Contain("\"ignore_tags\":[\"x\"]");
    }

    [Test]
    public async Task DeepL_456_IsExhausted()
    {
        var provider = new DeepLProvider(new HttpClient(new FakeHandler((HttpStatusCode)456, "{}")), "abc:fx");

        (await provider.TranslateAsync(Text, ChatLanguage.Korean, CancellationToken.None)).Kind.Should().Be(ProviderResultKind.Exhausted);
    }

    [Test]
    public async Task DeepL_GetUsage_ReadsCount()
    {
        var provider = new DeepLProvider(new HttpClient(new FakeHandler(HttpStatusCode.OK, """{"character_count":1234,"character_limit":500000}""")), "abc:fx");

        (await provider.GetUsageAsync(CancellationToken.None)).Should().Be(1234);
    }

    [Test]
    public async Task Google_Parses()
    {
        var handler = new FakeHandler(
            HttpStatusCode.OK,
            """{"data":{"translations":[{"translatedText":"<span translate=\"no\">Mileth</span>에 가는 중 &amp;","detectedSourceLanguage":"en"}]}}""");
        var provider = new GoogleTranslateProvider(new HttpClient(handler), "gkey");

        var result = await provider.TranslateAsync(Text, ChatLanguage.Korean, CancellationToken.None);

        result.Text.Should().Be("Mileth에 가는 중 &");
        handler.Request!.RequestUri!.ToString().Should().Be("https://translation.googleapis.com/language/translate/v2?key=gkey");
        handler.Body.Should().Contain("\"format\":\"html\"").And.Contain("\"target\":\"ko\"");
    }

    [Test]
    [Arguments(HttpStatusCode.Forbidden, "{}", ProviderResultKind.Exhausted)]
    [Arguments(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"Quota exceeded for quota metric 'v2 characters per day'\"}}", ProviderResultKind.Exhausted)]
    [Arguments(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"Rate limit\"}}", ProviderResultKind.Transient)]
    public async Task Google_MapsErrors(HttpStatusCode status, string body, ProviderResultKind expected)
    {
        var provider = new GoogleTranslateProvider(new HttpClient(new FakeHandler(status, body)), "gkey");
        var result = await provider.TranslateAsync(Text, ChatLanguage.Korean, CancellationToken.None);

        result.Kind.Should().Be(expected);

        if (expected == ProviderResultKind.Exhausted)
            result.ExhaustedForDay.Should().BeTrue();
    }

    private sealed class FakeHandler(HttpStatusCode status, string body, params (string Name, string Value)[] headers) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

            foreach ((var name, var value) in headers)
                response.Headers.TryAddWithoutValidation(name, value);

            return response;
        }
    }
}
```

- [ ] **Step 2: Run to see it fail.**

- [ ] **Step 3: Implement**

`IChatTranslationProvider.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.ChatTranslation.Providers;

/// <summary>One translation web service.</summary>
public interface IChatTranslationProvider
{
    /// <summary>The provider's name as in config ("Azure", "DeepL", "Google").</summary>
    string Name { get; }

    /// <summary>Translates <paramref name="text" /> into <paramref name="target" />. Never throws for HTTP errors.</summary>
    Task<ProviderResult> TranslateAsync(ProtectedText text, ChatLanguage target, CancellationToken cancellationToken);
}

public enum ProviderResultKind
{
    Success,
    Exhausted,
    Transient
}

/// <summary>
///     A provider's answer. Success carries the translated text (markup stripped, entities decoded, not yet cleaned for
///     code page 949), the detected source language ("en"/"ko") and the billed characters when the provider reports them.
///     Exhausted means the quota is used up: for the day when <see cref="ExhaustedForDay" />, otherwise until the monthly
///     reset. Transient is anything else that failed.
/// </summary>
public sealed record ProviderResult(
    ProviderResultKind Kind,
    string? Text = null,
    string? DetectedLanguage = null,
    int? BilledCharacters = null,
    bool ExhaustedForDay = false,
    int SentCharacters = 0)
{
    public static ProviderResult Transient(int sent) => new(ProviderResultKind.Transient, SentCharacters: sent);
    public static ProviderResult Exhausted(bool forDay, int sent) => new(ProviderResultKind.Exhausted, ExhaustedForDay: forDay, SentCharacters: sent);
}
```

`ProviderMarkup.cs`:

```csharp
#region
using System.Net;
using System.Security;
using System.Text.RegularExpressions;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.ChatTranslation.Providers;

/// <summary>Shared markup rules: escape plain text, wrap protected names, strip markup from a reply.</summary>
public static partial class ProviderMarkup
{
    /// <summary>Plain text XML-escaped; protected names escaped and wrapped by <paramref name="wrap" />.</summary>
    public static string Render(ProtectedText text, Func<string, string> wrap)
        => string.Concat(
            text.Segments.Select(segment => segment.Protected
                ? wrap(SecurityElement.Escape(segment.Text))
                : SecurityElement.Escape(segment.Text)));

    /// <summary>Removes every tag and decodes entities.</summary>
    public static string Strip(string reply) => WebUtility.HtmlDecode(TagRegex().Replace(reply, string.Empty));

    /// <summary>"EN-US" → "en", "ko" → "ko".</summary>
    public static string? LanguageCode(string? detected)
        => string.IsNullOrEmpty(detected) ? null : detected[..Math.Min(2, detected.Length)].ToLowerInvariant();

    /// <summary>The two-letter code for a target language.</summary>
    public static string Code(ChatLanguage language) => language == ChatLanguage.Korean ? "ko" : "en";

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();
}
```

`AzureTranslatorProvider.cs`:

```csharp
#region
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.ChatTranslation.Providers;

/// <summary>Azure AI Translator v3 (free F0 tier: 2,000,000 characters a month).</summary>
public sealed class AzureTranslatorProvider(HttpClient http, string key, string region) : IChatTranslationProvider
{
    private const string ENDPOINT = "https://api.cognitive.microsofttranslator.com";

    public string Name => "Azure";

    public async Task<ProviderResult> TranslateAsync(ProtectedText text, ChatLanguage target, CancellationToken cancellationToken)
    {
        var rendered = ProviderMarkup.Render(text, name => $"<span class=\"notranslate\">{name}</span>");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{ENDPOINT}/translate?api-version=3.0&to={ProviderMarkup.Code(target)}&textType=html");

        request.Headers.Add("Ocp-Apim-Subscription-Key", key);

        if (!string.IsNullOrWhiteSpace(region))
            request.Headers.Add("Ocp-Apim-Subscription-Region", region);

        request.Content = JsonContent.Create(new[] { new { Text = rendered } });

        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
            return ProviderResult.Exhausted(false, rendered.Length);

        if (!response.IsSuccessStatusCode)
            return ProviderResult.Transient(rendered.Length);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var first = json.RootElement[0];
        var translated = first.GetProperty("translations")[0].GetProperty("text").GetString() ?? string.Empty;

        var detected = first.TryGetProperty("detectedLanguage", out var detectedLanguage)
            ? detectedLanguage.GetProperty("language").GetString()
            : null;

        int? billed = response.Headers.TryGetValues("X-metered-usage", out var values)
                      && int.TryParse(values.FirstOrDefault(), out var metered)
            ? metered
            : null;

        return new ProviderResult(ProviderResultKind.Success, ProviderMarkup.Strip(translated), ProviderMarkup.LanguageCode(detected), billed, SentCharacters: rendered.Length);
    }

    /// <summary>Opens the connection with a free request, so the first translation skips the TLS handshake.</summary>
    public async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"{ENDPOINT}/languages?api-version=3.0&scope=translation", cancellationToken);
    }
}
```

`DeepLProvider.cs`:

```csharp
#region
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.ChatTranslation.Providers;

/// <summary>DeepL API (free plan: 500,000 characters a month). Free keys end in ":fx" and use the free host.</summary>
public sealed class DeepLProvider(HttpClient http, string key) : IChatTranslationProvider
{
    private readonly string Host = key.EndsWith(":fx", StringComparison.Ordinal) ? "https://api-free.deepl.com" : "https://api.deepl.com";

    public string Name => "DeepL";

    public async Task<ProviderResult> TranslateAsync(ProtectedText text, ChatLanguage target, CancellationToken cancellationToken)
    {
        var rendered = ProviderMarkup.Render(text, name => $"<x>{name}</x>");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Host}/v2/translate");
        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", key);

        request.Content = JsonContent.Create(
            new Dictionary<string, object>
            {
                ["text"] = new[] { rendered },
                ["target_lang"] = target == ChatLanguage.Korean ? "KO" : "EN-US",
                ["tag_handling"] = "xml",
                ["ignore_tags"] = new[] { "x" },
                ["show_billed_characters"] = true
            });

        using var response = await http.SendAsync(request, cancellationToken);

        if ((int)response.StatusCode == 456)
            return ProviderResult.Exhausted(false, rendered.Length);

        if (!response.IsSuccessStatusCode)
            return ProviderResult.Transient(rendered.Length);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var first = json.RootElement.GetProperty("translations")[0];

        int? billed = first.TryGetProperty("billed_characters", out var billedElement) ? billedElement.GetInt32() : null;

        return new ProviderResult(
            ProviderResultKind.Success,
            ProviderMarkup.Strip(first.GetProperty("text").GetString() ?? string.Empty),
            ProviderMarkup.LanguageCode(first.GetProperty("detected_source_language").GetString()),
            billed,
            SentCharacters: rendered.Length);
    }

    /// <summary>The characters DeepL has counted this billing period, or null when the call fails.</summary>
    public async Task<long?> GetUsageAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Host}/v2/usage");
        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", key);

        using var response = await http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return json.RootElement.GetProperty("character_count").GetInt64();
    }
}
```

`GoogleTranslateProvider.cs`:

```csharp
#region
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.ChatTranslation.Providers;

/// <summary>
///     Google Cloud Translation v2 (500,000 characters a month of credit). Its cap is a per-day quota set in the Cloud
///     console, so a quota error lasts until the next day.
/// </summary>
public sealed class GoogleTranslateProvider(HttpClient http, string key) : IChatTranslationProvider
{
    public string Name => "Google";

    public async Task<ProviderResult> TranslateAsync(ProtectedText text, ChatLanguage target, CancellationToken cancellationToken)
    {
        var rendered = ProviderMarkup.Render(text, name => $"<span translate=\"no\">{name}</span>");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://translation.googleapis.com/language/translate/v2?key={Uri.EscapeDataString(key)}");

        request.Content = JsonContent.Create(
            new Dictionary<string, string>
            {
                ["q"] = rendered,
                ["target"] = ProviderMarkup.Code(target),
                ["format"] = "html"
            });

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
            return ProviderResult.Exhausted(true, rendered.Length);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return body.Contains("daily", StringComparison.OrdinalIgnoreCase) || body.Contains("per day", StringComparison.OrdinalIgnoreCase)
                ? ProviderResult.Exhausted(true, rendered.Length)
                : ProviderResult.Transient(rendered.Length);

        if (!response.IsSuccessStatusCode)
            return ProviderResult.Transient(rendered.Length);

        using var json = JsonDocument.Parse(body);
        var first = json.RootElement.GetProperty("data").GetProperty("translations")[0];

        var detected = first.TryGetProperty("detectedSourceLanguage", out var detectedElement) ? detectedElement.GetString() : null;

        return new ProviderResult(
            ProviderResultKind.Success,
            ProviderMarkup.Strip(first.GetProperty("translatedText").GetString() ?? string.Empty),
            ProviderMarkup.LanguageCode(detected),
            SentCharacters: rendered.Length);
    }
}
```

Note: the Azure test's `Body` assertion uses the JSON-escaped form of the quotes; if `JsonContent` escapes `<`, `>` or `&` as `\u003C` etc., change the provider to serialize with `JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }` so the body is readable (the API accepts either), and keep the test as written. Apply the same options in all three providers.

- [ ] **Step 4: Run the tests** → pass.

```json:metadata
{"files": ["$SRV/Chaos/Services/ChatTranslation/Providers/IChatTranslationProvider.cs", "$SRV/Chaos/Services/ChatTranslation/Providers/ProviderMarkup.cs", "$SRV/Chaos/Services/ChatTranslation/Providers/AzureTranslatorProvider.cs", "$SRV/Chaos/Services/ChatTranslation/Providers/DeepLProvider.cs", "$SRV/Chaos/Services/ChatTranslation/Providers/GoogleTranslateProvider.cs", "$SRV/Tests/Chaos.Tests/ChatTranslation/ProviderTests.cs"], "verifyCommand": "cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ProviderTests/*\"", "acceptanceCriteria": ["Azure request/parse/errors", "DeepL host/request/parse/456/usage", "Google request/parse/errors daily", "escape and strip markup", "detected language normalized"], "modelTier": "standard"}
```

---

### Task 6: Cache and usage ledger

**Goal:** The memory + `.jsonl` cache and the quota/limit ledger, both thread-safe and clock-driven.

**Files:**
- Create: `$SRV/Chaos/Services/ChatTranslation/TranslationCache.cs`
- Create: `$SRV/Chaos/Services/ChatTranslation/TranslationUsage.cs`
- Create: `$SRV/Chaos/Services/ChatTranslation/ChatTranslationOptions.cs`
- Test: `$SRV/Tests/Chaos.Tests/ChatTranslation/TranslationCacheTests.cs`, `TranslationUsageTests.cs`

**Acceptance Criteria:**
- [ ] Cache: `Add(key, value, persist: true)` appends one JSON line; a new cache over the same file finds it after `Load()`. `persist: false` never touches the file. `Add(key, null, …)` stores a "no translation" entry in memory only; `TryGet` returns true with a null value.
- [ ] Cache load drops entries older than `CacheDays`, skips malformed lines, and rewrites the file once when it dropped anything.
- [ ] Usage: provider usable below `SwitchAtPercent`, not usable at or above it; `MarkExhausted(monthly)` blocks until the next reset day; `MarkExhausted(daily)` blocks until the next UTC midnight; a new period resets the count.
- [ ] Reset day handling: ResetDayOfMonth 31 in February uses the last day of the month.
- [ ] `TryChargeSpeaker("Bob", 3000, 5000)` true, then `("Bob", 3000, 5000)` false with `firstRefusal == true`; again false with `firstRefusal == false`; the next UTC day allows it again. Names compare case-insensitively.
- [ ] `Serialize()` → `Load(json)` restores provider counts, exhaustion and speaker totals.

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/TranslationUsageTests/*"` and `.../TranslationCacheTests/*` → pass

**Steps:**

- [ ] **Step 1: Options class**

```csharp
#region
using Chaos.Common.Abstractions;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>
///     Settings for chat translation, bound from <c>Options:ChatTranslationOptions</c>. The keys belong in
///     appsettings.translation.json, which is not tracked. Translation is on when at least one provider has a key.
/// </summary>
public sealed record ChatTranslationOptions : IDirectoryBound
{
    public string Directory { get; set; } = Path.Combine("Data", "LocalStorage", "ChatTranslation");
    public string ConfigFile { get; set; } = Path.Combine("Data", "Configuration", "ChatTranslation.json");
    public int AttemptTimeoutMs { get; set; } = 2000;
    public int TotalTimeoutMs { get; set; } = 4000;
    public int SwitchAtPercent { get; set; } = 90;
    public int PerPlayerDailyCharacters { get; set; } = 5000;
    public int CacheDays { get; set; } = 90;
    public List<TranslationProviderOptions> Providers { get; set; } = [];

    /// <inheritdoc />
    public void UseBaseDirectory(string baseDirectory)
    {
        Directory = Path.Combine(baseDirectory, Directory);
        ConfigFile = Path.Combine(baseDirectory, ConfigFile);
    }
}

/// <summary>One provider's settings. <see cref="Name" /> is "Azure", "DeepL" or "Google".</summary>
public sealed record TranslationProviderOptions
{
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public long MonthlyCharacters { get; set; }
    public int ResetDayOfMonth { get; set; } = 1;
}
```

(Check the real namespace of `IDirectoryBound`: `Chaos.Common.Abstractions/IDirectoryBound.cs`. Match `BugReportOptions`' usings. Note: config binding into a `List<>` that already has items appends; the defaults list is empty, so appsettings.json + appsettings.translation.json merge by index — that is what we want: the translation file only sets `Providers:0:Key` etc.)

- [ ] **Step 2: Write the failing tests**

Use `Microsoft.Extensions.Time.Testing.FakeTimeProvider` if the test project references it (search `FakeTimeProvider` in `Tests/`). If not, write a tiny `ManualTimeProvider : TimeProvider` in the test file with a settable `UtcNow` overriding `GetUtcNow()`.

```csharp
#region
using Chaos.Services.ChatTranslation;
using FluentAssertions;
#endregion

namespace Chaos.Tests.ChatTranslation;

public sealed class TranslationUsageTests
{
    private static readonly TranslationProviderOptions Azure = new() { Name = "Azure", Key = "k", MonthlyCharacters = 1000, ResetDayOfMonth = 1 };

    [Test]
    public void Usable_UntilSwitchPercent()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        var usage = new TranslationUsage(clock);

        usage.IsUsable(Azure, 90).Should().BeTrue();
        usage.AddProviderCharacters(Azure, 899);
        usage.IsUsable(Azure, 90).Should().BeTrue();
        usage.AddProviderCharacters(Azure, 1);
        usage.IsUsable(Azure, 90).Should().BeFalse();
    }

    [Test]
    public void NewPeriod_ResetsCount()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        var usage = new TranslationUsage(clock);
        usage.AddProviderCharacters(Azure, 950);

        clock.UtcNow = new DateTimeOffset(2026, 10, 1, 0, 0, 1, TimeSpan.Zero);

        usage.IsUsable(Azure, 90).Should().BeTrue();
        usage.GetProviderCharacters(Azure).Should().Be(0);
    }

    [Test]
    public void MonthlyExhaustion_LastsUntilReset()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        var usage = new TranslationUsage(clock);
        usage.MarkExhausted(Azure, forDay: false);

        clock.UtcNow = new DateTimeOffset(2026, 9, 30, 23, 0, 0, TimeSpan.Zero);
        usage.IsUsable(Azure, 90).Should().BeFalse();

        clock.UtcNow = new DateTimeOffset(2026, 10, 1, 0, 0, 1, TimeSpan.Zero);
        usage.IsUsable(Azure, 90).Should().BeTrue();
    }

    [Test]
    public void DailyExhaustion_LastsUntilMidnight()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        var usage = new TranslationUsage(clock);
        usage.MarkExhausted(Azure, forDay: true);

        usage.IsUsable(Azure, 90).Should().BeFalse();
        clock.UtcNow = new DateTimeOffset(2026, 9, 30, 0, 0, 1, TimeSpan.Zero);
        usage.IsUsable(Azure, 90).Should().BeTrue();
    }

    [Test]
    public void ResetDay31_InFebruary_UsesLastDay()
        => TranslationUsage.PeriodStart(new DateOnly(2027, 2, 28), 31).Should().Be(new DateOnly(2027, 2, 28));

    [Test]
    public void ResetDay15_BeforeThe15th_StartsLastMonth()
        => TranslationUsage.PeriodStart(new DateOnly(2026, 9, 10), 15).Should().Be(new DateOnly(2026, 8, 15));

    [Test]
    public void SpeakerLimit_RefusesAndNotifiesOnce()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        var usage = new TranslationUsage(clock);

        usage.TryChargeSpeaker("Bob", 3000, 5000, out _).Should().BeTrue();
        usage.TryChargeSpeaker("bob", 3000, 5000, out var first).Should().BeFalse();
        first.Should().BeTrue();
        usage.TryChargeSpeaker("Bob", 10, 5000, out var second).Should().BeTrue();   //3010 still fits
        usage.TryChargeSpeaker("Bob", 3000, 5000, out second).Should().BeFalse();
        second.Should().BeFalse();

        clock.UtcNow = new DateTimeOffset(2026, 9, 30, 0, 0, 1, TimeSpan.Zero);
        usage.TryChargeSpeaker("Bob", 3000, 5000, out _).Should().BeTrue();
    }

    [Test]
    public void SerializeLoad_RoundTrips()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        var usage = new TranslationUsage(clock);
        usage.AddProviderCharacters(Azure, 400);
        usage.MarkExhausted(new TranslationProviderOptions { Name = "DeepL", MonthlyCharacters = 500 }, forDay: false);
        usage.TryChargeSpeaker("Bob", 100, 5000, out _);

        var loaded = TranslationUsage.Load(usage.Serialize(), clock);

        loaded.GetProviderCharacters(Azure).Should().Be(400);
        loaded.IsUsable(new TranslationProviderOptions { Name = "DeepL", MonthlyCharacters = 500 }, 90).Should().BeFalse();
        loaded.GetSpeakerCharacters("bob").Should().Be(100);
    }
}
```

```csharp
public sealed class TranslationCacheTests
{
    [Test]
    public void PersistedEntry_SurvivesReload()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

        try
        {
            var cache = new TranslationCache(path, 90, clock);
            cache.Load();
            cache.Add("ko-en|안녕", "hi", persist: true);
            cache.Add("ko-en|비밀", "secret", persist: false);

            var reloaded = new TranslationCache(path, 90, clock);
            reloaded.Load();

            reloaded.TryGet("ko-en|안녕", out var hit).Should().BeTrue();
            hit.Should().Be("hi");
            reloaded.TryGet("ko-en|비밀", out _).Should().BeFalse();
        } finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void NoTranslation_IsCachedInMemoryOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");
        var cache = new TranslationCache(path, 90, TimeProvider.System);
        cache.Load();

        cache.Add("en-ko|hola amigo", null, persist: true);

        cache.TryGet("en-ko|hola amigo", out var value).Should().BeTrue();
        value.Should().BeNull();
        File.Exists(path).Should().BeFalse();
    }

    [Test]
    public void Load_DropsOldAndMalformed_AndRewrites()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl");

        File.WriteAllLines(
            path,
            [
                """{"d":"ko-en","k":"old","v":"old","t":"2026-01-01"}""",
                "not json",
                """{"d":"ko-en","k":"new","v":"new","t":"2026-09-20"}"""
            ]);

        try
        {
            var cache = new TranslationCache(path, 90, new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero)));
            cache.Load();

            cache.TryGet("ko-en|old", out _).Should().BeFalse();
            cache.TryGet("ko-en|new", out _).Should().BeTrue();
            File.ReadAllLines(path).Should().HaveCount(1);
        } finally
        {
            File.Delete(path);
        }
    }
}
```

Put `ManualTimeProvider` in its own file `Tests/Chaos.Tests/ChatTranslation/ManualTimeProvider.cs` if `FakeTimeProvider` isn't available:

```csharp
namespace Chaos.Tests.ChatTranslation;

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
```

- [ ] **Step 3: Run to see them fail.**

- [ ] **Step 4: Implement**

`TranslationCache.cs` — keys are `"{direction}|{normalized body}"`; the file stores direction and key separately:

```csharp
#region
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>
///     Translations already paid for. Everything lives in memory; entries added with persist are also appended to a
///     .jsonl file (one JSON object per line) so a restart doesn't pay again. A null value means "no translation" (the
///     provider saw another language) and is never written to the file.
/// </summary>
public sealed class TranslationCache(string filePath, int cacheDays, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, string?> Entries = new(StringComparer.Ordinal);
    private readonly Lock FileSync = new();
    private long HitCount;
    private long MissCount;

    public int Count => Entries.Count;
    public long Hits => Interlocked.Read(ref HitCount);
    public long Misses => Interlocked.Read(ref MissCount);

    /// <summary>Reads the file, drops entries older than the cache lifetime and bad lines, and rewrites it if it dropped any.</summary>
    public void Load()
    {
        if (!File.Exists(filePath))
            return;

        var cutoff = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(-cacheDays);
        var kept = new List<string>();
        var dropped = false;

        foreach (var line in File.ReadLines(filePath))
        {
            CacheLine? entry = null;

            try
            {
                entry = JsonSerializer.Deserialize<CacheLine>(line);
            } catch (JsonException) { }

            if (entry is null || string.IsNullOrEmpty(entry.K) || string.IsNullOrEmpty(entry.V) || !DateOnly.TryParse(entry.T, out var day) || (day < cutoff))
            {
                dropped = true;

                continue;
            }

            Entries[$"{entry.D}|{entry.K}"] = entry.V;
            kept.Add(line);
        }

        if (dropped)
            lock (FileSync)
                File.WriteAllLines(filePath, kept);
    }

    public bool TryGet(string key, out string? value)
    {
        var found = Entries.TryGetValue(key, out value);

        if (found)
            Interlocked.Increment(ref HitCount);
        else
            Interlocked.Increment(ref MissCount);

        return found;
    }

    public void Add(string key, string? value, bool persist)
    {
        Entries[key] = value;

        if (!persist || value is null)
            return;

        var split = key.IndexOf('|');

        var line = JsonSerializer.Serialize(
            new CacheLine
            {
                D = key[..split],
                K = key[(split + 1)..],
                V = value,
                T = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).ToString("yyyy-MM-dd")
            });

        lock (FileSync)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.AppendAllText(filePath, line + Environment.NewLine);
        }
    }

    private sealed class CacheLine
    {
        [JsonPropertyName("d")] public string D { get; set; } = string.Empty;
        [JsonPropertyName("k")] public string K { get; set; } = string.Empty;
        [JsonPropertyName("v")] public string V { get; set; } = string.Empty;
        [JsonPropertyName("t")] public string T { get; set; } = string.Empty;
    }
}
```

(`JsonSerializer.Serialize` escapes Hangul as `\uXXXX` by default. That is valid and reloads fine. Use `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` if you want the file readable.)

`TranslationUsage.cs`:

```csharp
#region
using System.Text.Json;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>
///     Characters used per provider this period, provider quota errors, and each speaker's characters today (UTC).
///     Thread-safe. Saved as JSON by the service every 30 seconds when changed.
/// </summary>
public sealed class TranslationUsage(TimeProvider time)
{
    private readonly Lock Sync = new();
    private readonly Dictionary<string, ProviderState> Providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpeakerState> Speakers = new(StringComparer.OrdinalIgnoreCase);

    public bool IsDirty { get; private set; }

    private DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    /// <summary>The first day of the provider period containing <paramref name="today" />.</summary>
    public static DateOnly PeriodStart(DateOnly today, int resetDayOfMonth)
    {
        var day = Math.Clamp(resetDayOfMonth, 1, 31);
        var thisMonth = new DateOnly(today.Year, today.Month, Math.Min(day, DateTime.DaysInMonth(today.Year, today.Month)));

        if (thisMonth <= today)
            return thisMonth;

        var last = today.AddMonths(-1);

        return new DateOnly(last.Year, last.Month, Math.Min(day, DateTime.DaysInMonth(last.Year, last.Month)));
    }

    public bool IsUsable(TranslationProviderOptions provider, int switchAtPercent)
    {
        lock (Sync)
        {
            var state = Current(provider);

            if (state.ExhaustedUntil is { } until && (time.GetUtcNow() < until))
                return false;

            return (provider.MonthlyCharacters <= 0) || (state.Characters * 100 < provider.MonthlyCharacters * switchAtPercent);
        }
    }

    public void AddProviderCharacters(TranslationProviderOptions provider, long characters)
    {
        lock (Sync)
        {
            Current(provider).Characters += characters;
            IsDirty = true;
        }
    }

    /// <summary>Overwrites the local count with the provider's own number (DeepL /v2/usage).</summary>
    public void SetProviderCharacters(TranslationProviderOptions provider, long characters)
    {
        lock (Sync)
        {
            Current(provider).Characters = characters;
            IsDirty = true;
        }
    }

    public long GetProviderCharacters(TranslationProviderOptions provider)
    {
        lock (Sync)
            return Current(provider).Characters;
    }

    public DateTimeOffset? GetExhaustedUntil(TranslationProviderOptions provider)
    {
        lock (Sync)
            return Current(provider).ExhaustedUntil is { } until && (time.GetUtcNow() < until) ? until : null;
    }

    /// <summary>Blocks the provider until the next UTC midnight (<paramref name="forDay" />) or its next monthly reset.</summary>
    public void MarkExhausted(TranslationProviderOptions provider, bool forDay)
    {
        lock (Sync)
        {
            var state = Current(provider);
            var today = Today;

            var nextReset = forDay
                ? today.AddDays(1)
                : PeriodStart(today, provider.ResetDayOfMonth).AddMonths(1) is var next
                    ? new DateOnly(next.Year, next.Month, Math.Min(Math.Clamp(provider.ResetDayOfMonth, 1, 31), DateTime.DaysInMonth(next.Year, next.Month)))
                    : today;

            state.ExhaustedUntil = new DateTimeOffset(nextReset.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            IsDirty = true;
        }
    }

    /// <summary>
    ///     Adds <paramref name="characters" /> to the speaker's total for today if it stays within
    ///     <paramref name="dailyLimit" />. <paramref name="firstRefusal" /> is true only on the first refusal of the day.
    /// </summary>
    public bool TryChargeSpeaker(string speaker, int characters, int dailyLimit, out bool firstRefusal)
    {
        lock (Sync)
        {
            var today = Today;

            if (!Speakers.TryGetValue(speaker, out var state) || (state.Day != today))
                Speakers[speaker] = state = new SpeakerState { Day = today };

            firstRefusal = false;

            if (state.Characters + characters > dailyLimit)
            {
                firstRefusal = !state.Refused;
                state.Refused = true;
                IsDirty = true;

                return false;
            }

            state.Characters += characters;
            IsDirty = true;

            return true;
        }
    }

    public long GetSpeakerCharacters(string speaker)
    {
        lock (Sync)
            return Speakers.TryGetValue(speaker, out var state) && (state.Day == Today) ? state.Characters : 0;
    }

    public string Serialize()
    {
        lock (Sync)
        {
            IsDirty = false;

            return JsonSerializer.Serialize(new UsageFile { Providers = Providers, Speakers = Speakers.Where(pair => pair.Value.Day == Today).ToDictionary() });
        }
    }

    public static TranslationUsage Load(string json, TimeProvider time)
    {
        var usage = new TranslationUsage(time);
        var file = JsonSerializer.Deserialize<UsageFile>(json);

        foreach ((var name, var state) in file?.Providers ?? [])
            usage.Providers[name] = state;

        foreach ((var name, var state) in file?.Speakers ?? [])
            usage.Speakers[name] = state;

        return usage;
    }

    //the state for the provider's current period; a new period starts at zero and clears a monthly block
    private ProviderState Current(TranslationProviderOptions provider)
    {
        var start = PeriodStart(Today, provider.ResetDayOfMonth);

        if (!Providers.TryGetValue(provider.Name, out var state))
            Providers[provider.Name] = state = new ProviderState { PeriodStart = start };
        else if (state.PeriodStart != start)
        {
            state.PeriodStart = start;
            state.Characters = 0;
            IsDirty = true;
        }

        return state;
    }

    public sealed class ProviderState
    {
        public DateOnly PeriodStart { get; set; }
        public long Characters { get; set; }
        public DateTimeOffset? ExhaustedUntil { get; set; }
    }

    public sealed class SpeakerState
    {
        public DateOnly Day { get; set; }
        public long Characters { get; set; }
        public bool Refused { get; set; }
    }

    private sealed class UsageFile
    {
        public Dictionary<string, ProviderState>? Providers { get; set; }
        public Dictionary<string, SpeakerState>? Speakers { get; set; }
    }
}
```

Rewrite `MarkExhausted`'s monthly branch more plainly if you like (compute `next = PeriodStart(today, day).AddMonths(1)` then clamp the day). The tests are the contract.

- [ ] **Step 5: Run both test classes** → pass.

```json:metadata
{"files": ["$SRV/Chaos/Services/ChatTranslation/ChatTranslationOptions.cs", "$SRV/Chaos/Services/ChatTranslation/TranslationCache.cs", "$SRV/Chaos/Services/ChatTranslation/TranslationUsage.cs", "$SRV/Tests/Chaos.Tests/ChatTranslation/TranslationCacheTests.cs", "$SRV/Tests/Chaos.Tests/ChatTranslation/TranslationUsageTests.cs"], "verifyCommand": "cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/TranslationUsageTests/*\"", "acceptanceCriteria": ["cache persist/memory-only/null", "cache load prune + rewrite", "provider usable/switch/exhaustion/period reset", "reset day clamping", "speaker limit + first refusal + next day", "usage serialize/load"], "modelTier": "standard"}
```

---

### Task 7: ChatTranslationService — pipeline, chain, delivery, hosting

**Goal:** The singleton that runs jobs end to end, plus DI registration and config files.

**Files:**
- Create: `$SRV/Chaos/Services/ChatTranslation/IChatTranslationService.cs` (interface, `ChatTranslationJob`, `ChatTranslationStatus`)
- Create: `$SRV/Chaos/Services/ChatTranslation/ChatTranslationSender.cs` (`IChatTranslationSender` + default implementation)
- Create: `$SRV/Chaos/Services/ChatTranslation/ChatTranslationProviderSet.cs`
- Create: `$SRV/Chaos/Services/ChatTranslation/ChatTranslationService.cs`
- Modify: `$SRV/Chaos/Networking/Abstractions/IChaosWorldClient.cs` and `$SRV/Chaos/Networking/ChaosWorldClient.cs` (add `SendChatTranslation(ChatTranslationArgs args)`)
- Modify: `$SRV/Chaos/Extensions/ServiceCollectionExtensions.cs` (add `AddChatTranslation()` and call it where `AddBugReports()` is called — search the call site)
- Modify: `$SRV/Chaos/Program.cs` (`AddConfiguration`: add `.AddJsonFile("appsettings.translation.json", true, false)` after the local/prod file, outside the `#if`)
- Modify: `$SRV/Chaos/appsettings.json` (add the `ChatTranslationOptions` section from the spec, empty keys, inside `Options`)
- Modify: `$SRV/.gitignore` (add `Chaos/appsettings.translation.json`)
- Test: `$SRV/Tests/Chaos.Tests/ChatTranslation/ChatTranslationServiceTests.cs`

**Acceptance Criteria:**
- [ ] `IsEnabled` is false and `NextLineId()` returns 0 when no provider has a key; otherwise ids start at 1, increase, and skip 0 on wrap.
- [ ] `DetectLanguage` returns Korean for jamo-only lines that are Korean shortcut keys (`"ㅋㅋㅋ"`), and defers to `ChatTranslationText.DetectLanguage` otherwise.
- [ ] Shortcut hit: no provider call, reader gets the shortcut text, speaker not charged.
- [ ] Cache hit: no provider call.
- [ ] Provider order: the first usable provider is tried; a Transient result moves to the next one; an Exhausted result marks the provider and moves on; the first Success wins and is charged (billed characters when given, sent characters otherwise).
- [ ] An attempt slower than `AttemptTimeoutMs` is cancelled and the next provider is tried; nothing is sent when the total budget runs out.
- [ ] Detected language mismatch → nothing sent, null cached (no second provider call for the same text).
- [ ] Speaker over the daily limit → nothing sent, speaker notified once per day with the spec's text.
- [ ] Two jobs with the same key at the same time → one provider call, both readers served.
- [ ] Delivery: each reader gets `ChatTranslationArgs` with the job's line id, source language, cleaned + cut text, and tags from `IChatFilterService.Detect(text)`.
- [ ] `Persist == false` jobs never reach the cache file.
- [ ] `Submit` returns immediately and swallows exceptions (a provider that throws does not fault anything).
- [ ] The server starts with no `appsettings.translation.json` present.

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ChatTranslationServiceTests/*"` → pass; `cd $SRV && dotnet build Chaos/Chaos.csproj` → 0 errors

**Steps:**

- [ ] **Step 1: Interfaces**

`IChatTranslationService.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>
///     Translates player chat lines between Korean and English for the players who asked for it. Callers decide the
///     readers while holding the map lock, send the original, then call <see cref="Submit" />, which returns at once.
/// </summary>
public interface IChatTranslationService
{
    /// <summary>True when at least one provider has a key.</summary>
    bool IsEnabled { get; }

    /// <summary>A new line id for a chat line, or 0 when translation is off.</summary>
    uint NextLineId();

    /// <summary>The language a body would be translated from, or null when there is nothing to translate.</summary>
    ChatLanguage? DetectLanguage(string body);

    /// <summary>Queues a job. Never blocks, never throws.</summary>
    void Submit(ChatTranslationJob job);

    /// <summary>A snapshot for the /translation admin command.</summary>
    ChatTranslationStatus GetStatus();
}

/// <summary>
///     One chat line to translate. <see cref="Readers" /> already excludes the speaker and anyone not reading
///     <see cref="Source" />'s target language. <see cref="Persist" /> is false for whispers, which never go to disk.
/// </summary>
public sealed record ChatTranslationJob(
    uint LineId,
    Aisling? Speaker,
    string SpeakerName,
    string Body,
    ChatLanguage Source,
    IReadOnlyList<Aisling> Readers,
    int MaxBodyLength,
    bool Persist);

public sealed record ChatTranslationStatus(
    bool Enabled,
    IReadOnlyList<ProviderStatus> Providers,
    int CacheEntries,
    long CacheHits,
    long CacheMisses,
    int InFlight);

public sealed record ProviderStatus(string Name, long Characters, long MonthlyCharacters, bool Usable, DateTimeOffset? ExhaustedUntil);
```

`ChatTranslationSender.cs`:

```csharp
#region
using Chaos.Models.World;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>Sends translation results to players. Split out so tests can record deliveries.</summary>
public interface IChatTranslationSender
{
    void Send(Aisling reader, ChatTranslationArgs args);
    void Notify(Aisling speaker, string message);
}

/// <summary>
///     Sends straight to each player's client. Packet sends need no map lock, the same as whispers and channel lines.
/// </summary>
public sealed class ChatTranslationSender : IChatTranslationSender
{
    public void Send(Aisling reader, ChatTranslationArgs args)
    {
        if (reader.Client.Connected)
            reader.Client.SendChatTranslation(args);
    }

    public void Notify(Aisling speaker, string message)
    {
        if (speaker.Client.Connected)
            speaker.SendOrangeBarMessage(message);
    }
}
```

`ChatTranslationProviderSet.cs`:

```csharp
#region
using System.Net;
using Chaos.Services.ChatTranslation.Providers;
using Microsoft.Extensions.Options;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>The providers that have keys, in config order, sharing one pooled HttpClient.</summary>
public sealed class ChatTranslationProviderSet
{
    public ChatTranslationProviderSet(IReadOnlyList<(TranslationProviderOptions Options, IChatTranslationProvider Provider)> providers)
        => Providers = providers;

    public IReadOnlyList<(TranslationProviderOptions Options, IChatTranslationProvider Provider)> Providers { get; }

    public static ChatTranslationProviderSet FromOptions(IOptions<ChatTranslationOptions> options)
    {
        var http = new HttpClient(
            new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                AutomaticDecompression = DecompressionMethods.All
            })
        {
            Timeout = Timeout.InfiniteTimeSpan //the service's own per-attempt timeout applies
        };

        var list = new List<(TranslationProviderOptions, IChatTranslationProvider)>();

        foreach (var provider in options.Value.Providers.Where(p => !string.IsNullOrWhiteSpace(p.Key)))
        {
            IChatTranslationProvider? instance = provider.Name.ToLowerInvariant() switch
            {
                "azure"  => new AzureTranslatorProvider(http, provider.Key, provider.Region),
                "deepl"  => new DeepLProvider(http, provider.Key),
                "google" => new GoogleTranslateProvider(http, provider.Key),
                _        => null
            };

            if (instance is not null)
                list.Add((provider, instance));
        }

        return new ChatTranslationProviderSet(list);
    }
}
```

- [ ] **Step 2: Write the failing service tests**

The service constructor (keep this exact signature, the tests use it):

```csharp
public ChatTranslationService(
    IOptions<ChatTranslationOptions> options,
    ChatTranslationProviderSet providers,
    IGameNameSource names,
    IChatFilterService chatFilter,
    IChatTranslationSender sender,
    TimeProvider time,
    ILogger<ChatTranslationService> logger)
```

Tests build it with: options pointing `Directory` and `ConfigFile` at a temp folder (write a `ChatTranslation.json` with one shortcut `ㅋㅋ → lol` and names `밀레스 → Mileth`), a list of `FakeProvider`s, a `Mock<IGameNameSource>` returning `["Mileth"]`, a `Mock<IChatFilterService>` returning `ChatFilterResult.Clean`, a `RecordingSender`, `TimeProvider.System`, `NullLogger<ChatTranslationService>.Instance`. Call `await service.InitializeAsync(CancellationToken.None)` (loads config/cache/usage; public for tests) before use. Because `Submit` is fire-and-forget, the tests call the internal method `RunJobAsync(job)` and await it; add `[assembly: InternalsVisibleTo("Chaos.Tests")]` if the project doesn't already have it (search `InternalsVisibleTo` in `$SRV/Chaos`), or make `RunJobAsync` public with an XML doc saying it is for tests.

Readers: `ChatTranslationJob.Readers` needs `Aisling` objects. Use the test infrastructure's aisling factory (search `Tests/Chaos.Testing.Infrastructure` for `MockAisling` or `Create(` helpers that build an `Aisling`). The `RecordingSender` records `(reader, args)` pairs, so the aisling's client is never touched.

```csharp
public sealed class ChatTranslationServiceTests
{
    [Test]
    public async Task ShortcutHit_SkipsProviders()
    {
        var provider = new FakeProvider("Azure", ProviderResultKind.Success, "unused");
        (var service, var sender) = await CreateAsync(provider);

        await service.RunJobAsync(Job("ㅋㅋㅋ", ChatLanguage.Korean));

        provider.Calls.Should().Be(0);
        sender.Sent.Single().Args.Text.Should().Be("lol");
    }

    [Test]
    public async Task CacheHit_SkipsProviders()
    {
        var provider = new FakeProvider("Azure", ProviderResultKind.Success, "Does anyone know?", "ko");
        (var service, var sender) = await CreateAsync(provider);

        await service.RunJobAsync(Job("아는 사람?", ChatLanguage.Korean));
        await service.RunJobAsync(Job("아는  사람?", ChatLanguage.Korean));

        provider.Calls.Should().Be(1);
        sender.Sent.Should().HaveCount(2);
    }

    [Test]
    public async Task Transient_FallsThroughToNextProvider()
    {
        var azure = new FakeProvider("Azure", ProviderResultKind.Transient);
        var deepl = new FakeProvider("DeepL", ProviderResultKind.Success, "hello", "ko");
        (var service, var sender) = await CreateAsync(azure, deepl);

        await service.RunJobAsync(Job("안녕", ChatLanguage.Korean));

        azure.Calls.Should().Be(1);
        deepl.Calls.Should().Be(1);
        sender.Sent.Single().Args.Text.Should().Be("hello");
    }

    [Test]
    public async Task Exhausted_MarksProvider_AndSkipsItNextTime()
    {
        var azure = new FakeProvider("Azure", ProviderResultKind.Exhausted);
        var deepl = new FakeProvider("DeepL", ProviderResultKind.Success, "hello", "ko");
        (var service, _) = await CreateAsync(azure, deepl);

        await service.RunJobAsync(Job("안녕", ChatLanguage.Korean));
        await service.RunJobAsync(Job("잘 가", ChatLanguage.Korean));

        azure.Calls.Should().Be(1);
        deepl.Calls.Should().Be(2);
    }

    [Test]
    public async Task SlowAttempt_TimesOut_AndNextProviderAnswers()
    {
        var azure = new FakeProvider("Azure", ProviderResultKind.Success, "late", "ko") { Delay = TimeSpan.FromSeconds(5) };
        var deepl = new FakeProvider("DeepL", ProviderResultKind.Success, "hello", "ko");
        (var service, var sender) = await CreateAsync([azure, deepl], attemptTimeoutMs: 100, totalTimeoutMs: 1000);

        await service.RunJobAsync(Job("안녕", ChatLanguage.Korean));

        sender.Sent.Single().Args.Text.Should().Be("hello");
    }

    [Test]
    public async Task WrongDetectedLanguage_SendsNothing_AndIsNotRetried()
    {
        var azure = new FakeProvider("Azure", ProviderResultKind.Success, "안녕 친구", "es");
        (var service, var sender) = await CreateAsync(azure);

        await service.RunJobAsync(Job("hola amigo", ChatLanguage.English));
        await service.RunJobAsync(Job("hola amigo", ChatLanguage.English));

        azure.Calls.Should().Be(1);
        sender.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task SpeakerOverLimit_NotifiedOnce()
    {
        var azure = new FakeProvider("Azure", ProviderResultKind.Success, "x", "ko");
        (var service, var sender) = await CreateAsync([azure], dailyLimit: 10);

        await service.RunJobAsync(Job("가나다라마바사아자차카타파하", ChatLanguage.Korean));
        await service.RunJobAsync(Job("하파타카차자아사바마라다나가", ChatLanguage.Korean));

        sender.Sent.Should().BeEmpty();
        sender.Notices.Should().Equal("You've reached today's translation limit. Your lines are shown untranslated until tomorrow.");
    }

    [Test]
    public async Task SameText_AtOnce_OneProviderCall()
    {
        var azure = new FakeProvider("Azure", ProviderResultKind.Success, "hello", "ko") { Delay = TimeSpan.FromMilliseconds(200) };
        (var service, var sender) = await CreateAsync(azure);

        await Task.WhenAll(service.RunJobAsync(Job("안녕", ChatLanguage.Korean)), service.RunJobAsync(Job("안녕", ChatLanguage.Korean)));

        azure.Calls.Should().Be(1);
        sender.Sent.Should().HaveCount(2);
    }

    [Test]
    public async Task Delivery_CarriesLineId_Source_AndCutText()
    {
        var azure = new FakeProvider("Azure", ProviderResultKind.Success, "does anyone know the way to Mileth", "ko");
        (var service, var sender) = await CreateAsync(azure);

        await service.RunJobAsync(Job("밀레스 가는 길 아는 사람?", ChatLanguage.Korean, lineId: 77, maxBody: 20));

        var args = sender.Sent.Single().Args;
        args.LineId.Should().Be(77);
        args.SourceLanguage.Should().Be(ChatLanguage.Korean);
        args.Text.Should().Be("does anyone know the");
        azure.LastText!.Segments.Should().Contain(new TextSegment("Mileth", true));
    }

    [Test]
    public async Task Disabled_WithoutProviders()
    {
        (var service, _) = await CreateAsync();

        service.IsEnabled.Should().BeFalse();
        service.NextLineId().Should().Be(0);
    }

    [Test]
    public async Task DetectLanguage_TreatsKoreanShortcutAsKorean()
    {
        (var service, _) = await CreateAsync(new FakeProvider("Azure", ProviderResultKind.Success));

        service.DetectLanguage("ㅋㅋㅋ").Should().Be(ChatLanguage.Korean);
        service.DetectLanguage("123").Should().BeNull();
    }

    //helpers: Job(...) builds a ChatTranslationJob with one reader aisling from the test infrastructure, speaker null,
    //SpeakerName "Haneul", Persist true, MaxBodyLength maxBody ?? 128, LineId lineId ?? 1.
    //CreateAsync(...) builds the service as described above and awaits InitializeAsync.
    //FakeProvider(name, kind, text = null, detected = null): counts Calls, stores LastText, waits Delay (honoring the
    //cancellation token) and returns new ProviderResult(kind, text, detected, SentCharacters: text.ToPlainString().Length).
    //RecordingSender: List<(Aisling Reader, ChatTranslationArgs Args)> Sent; List<string> Notices.
}
```

Write the helpers in full in the test file. The speaker-limit test needs a speaker to notify: pass a speaker aisling (the same factory) in that test's job, and have `RecordingSender.Notify` record the message.

- [ ] **Step 3: Run to see it fail.**

- [ ] **Step 4: Implement `ChatTranslationService`**

Shape (fill every member; the comments describe required behavior, not optional ideas):

```csharp
public sealed class ChatTranslationService : BackgroundService, IChatTranslationService
{
    private const string LIMIT_NOTICE = "You've reached today's translation limit. Your lines are shown untranslated until tomorrow.";

    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> InFlight = new(StringComparer.Ordinal);
    private readonly ChatTranslationOptions Options;
    private readonly ChatTranslationProviderSet ProviderSet;
    //...other injected fields
    private ChatTranslationConfig Config = ChatTranslationConfig.Empty;
    private TranslationCache Cache = null!;
    private TranslationUsage Usage = null!;
    private int LineCounter;

    public bool IsEnabled => ProviderSet.Providers.Count > 0;

    public uint NextLineId()
    {
        if (!IsEnabled)
            return 0;

        var id = (uint)Interlocked.Increment(ref LineCounter);

        //0 means "no line id" on the wire
        return id == 0 ? (uint)Interlocked.Increment(ref LineCounter) : id;
    }

    public ChatLanguage? DetectLanguage(string body)
        => ChatTranslationText.DetectLanguage(body)
           ?? (Config.KoreanToEnglish.ContainsKey(ChatTranslationText.NormalizeKey(body, ChatLanguage.Korean)) ? ChatLanguage.Korean : null);

    public void Submit(ChatTranslationJob job)
        => _ = Task.Run(async () =>
        {
            try
            {
                await RunJobAsync(job);
            } catch (Exception e)
            {
                Logger.LogWarning(e, "Chat translation job {LineId} failed", job.LineId);
            }
        });

    /// <summary>Loads the config file, the cache file and the usage file. Called once at startup (and by tests).</summary>
    public Task InitializeAsync(CancellationToken cancellationToken) { /* Config = ChatTranslationConfig.Load(Options.ConfigFile) in try/catch (log + Empty);
        Cache = new TranslationCache(Path.Combine(Options.Directory, "cache.jsonl"), Options.CacheDays, Time); Cache.Load();
        Usage = File.Exists(usagePath) ? TranslationUsage.Load(File.ReadAllText(usagePath), Time) : new TranslationUsage(Time) (catch: log, new) */ }

    internal async Task RunJobAsync(ChatTranslationJob job)
    {
        //1. key = $"{ChatTranslationText.Direction(job.Source)}|{ChatTranslationText.NormalizeKey(job.Body, job.Source)}"
        //2. shortcut: table = job.Source == Korean ? Config.KoreanToEnglish : Config.EnglishToKorean;
        //   if table has the normalized key -> Deliver(job, table[key], "shortcut") and return
        //3. if Cache.TryGet(key, out var cached): if cached is null return; Deliver(job, cached, "cache") and return
        //4. text = await InFlight.GetOrAdd(key, _ => new Lazy<Task<string?>>(() => TranslateAsync(job, key))).Value;
        //   finally InFlight.TryRemove(key, out _) only when the stored Lazy is the one this call added
        //   (use the KeyValuePair overload of TryRemove)
        //5. if text is not null -> Deliver(job, text, provider name from TranslateAsync — carry it via a small record
        //   instead of a bare string if you want the name in the log)
    }

    private async Task<string?> TranslateAsync(ChatTranslationJob job, string key)
    {
        //a. protected = NameProtector.Protect(job.Body, Names.GetNames(), Config.KoreanNames)
        //b. size = protected.ToPlainString().Length
        //c. if !Usage.TryChargeSpeaker(job.SpeakerName, size, Options.PerPlayerDailyCharacters, out var first):
        //     if first && job.Speaker is not null -> Sender.Notify(job.Speaker, LIMIT_NOTICE); return null
        //   (do NOT cache the refusal)
        //d. using total = new CancellationTokenSource(Options.TotalTimeoutMs)
        //   foreach (options, provider) in ProviderSet.Providers:
        //     if !Usage.IsUsable(options, Options.SwitchAtPercent) continue
        //     using attempt = CancellationTokenSource.CreateLinkedTokenSource(total.Token); attempt.CancelAfter(Options.AttemptTimeoutMs)
        //     try result = await provider.TranslateAsync(protected, target, attempt.Token)
        //     catch OperationCanceledException: log timeout (Debug); if total cancelled break; continue
        //     catch Exception: log Warning; continue
        //     Exhausted -> Usage.MarkExhausted(options, result.ExhaustedForDay); log Warning "{Provider} quota used up"; continue
        //     Transient -> Usage.AddProviderCharacters(options, result.SentCharacters); continue
        //     Success -> Usage.AddProviderCharacters(options, result.BilledCharacters ?? result.SentCharacters)
        //                if result.DetectedLanguage is not null and != expected ("ko" for Korean source, "en" for English):
        //                    Cache.Add(key, null, false); return null
        //                cleaned = ChatTranslationText.CleanOutput(result.Text ?? "")
        //                if cleaned.Length == 0 return null
        //                Cache.Add(key, cleaned, job.Persist); return cleaned
        //e. return null (all failed; nothing cached)
    }

    private void Deliver(ChatTranslationJob job, string translation, string origin)
    {
        var text = ChatTranslationText.CutToLength(translation, Math.Max(1, job.MaxBodyLength));
        var tags = ChatFilterTags.ToWireTags(ChatFilter.Detect(text).Tags);
        var args = new ChatTranslationArgs { LineId = job.LineId, SourceLanguage = job.Source, Text = text, Tags = tags };

        foreach (var reader in job.Readers)
            Sender.Send(reader, args);

        //Information log with speaker, direction, origin (provider name / "cache" / "shortcut"), original, translation
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeAsync(stoppingToken);

        if (!IsEnabled)
        {
            Logger.LogInformation("Chat translation is off: no provider has a key");

            return;
        }

        //warm-up: for each provider that is AzureTranslatorProvider call WarmUpAsync; for DeepLProvider call
        //GetUsageAsync and, when not null, Usage.SetProviderCharacters(options, count). Each in try/catch with a 5 s
        //timeout; failures are logged at Warning and ignored.

        //loop until stoppingToken: every 30 s save usage when Usage.IsDirty (write to a temp file then File.Move
        //overwrite, so a crash never leaves half a file); every 60 minutes repeat the DeepL usage sync.
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        //save usage if dirty, then base.StopAsync
    }

    public ChatTranslationStatus GetStatus() { /* build from ProviderSet, Usage, Cache, InFlight.Count */ }
}
```

`ChaosWorldClient.SendChatTranslation(ChatTranslationArgs args) => Send(args);` and declare it on `IChaosWorldClient`.

DI in `ServiceCollectionExtensions` (a new extension next to `AddBugReports`):

```csharp
    /// <summary>
    ///     Registers chat translation: options (keys come from appsettings.translation.json), the providers that have keys,
    ///     the game-name list, the sender and the service, which also runs as a hosted service for loading and saving.
    /// </summary>
    public static void AddChatTranslation(this IServiceCollection services)
    {
        services.AddOptionsFromConfig<ChatTranslationOptions>(ConfigKeys.Options.Key);
        services.ConfigureOptions<DirectoryBoundOptionsConfigurer<ChatTranslationOptions>>();
        services.AddSingleton(provider => ChatTranslationProviderSet.FromOptions(provider.GetRequiredService<IOptions<ChatTranslationOptions>>()));
        services.AddSingleton<IGameNameSource, GameNameSource>();
        services.AddSingleton<IChatTranslationSender, ChatTranslationSender>();
        services.AddSingleton<ChatTranslationService>();
        services.AddSingleton<IChatTranslationService>(provider => provider.GetRequiredService<ChatTranslationService>());
        services.AddHostedService(provider => provider.GetRequiredService<ChatTranslationService>());
    }
```

`appsettings.json`: add the `ChatTranslationOptions` block from the spec's Configuration section inside `"Options"`, next to `"BugReportOptions"`. Also add `appsettings.translation.json` to `Chaos.csproj` as `<None Update="appsettings.translation.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>` (matching the other entries) so a local file is copied to bin when present.

- [ ] **Step 5: Run the service tests** → pass. Then `cd $SRV && dotnet build Chaos/Chaos.csproj` → 0 errors.

```json:metadata
{"files": ["$SRV/Chaos/Services/ChatTranslation/IChatTranslationService.cs", "$SRV/Chaos/Services/ChatTranslation/ChatTranslationSender.cs", "$SRV/Chaos/Services/ChatTranslation/ChatTranslationProviderSet.cs", "$SRV/Chaos/Services/ChatTranslation/ChatTranslationService.cs", "$SRV/Chaos/Networking/Abstractions/IChaosWorldClient.cs", "$SRV/Chaos/Networking/ChaosWorldClient.cs", "$SRV/Chaos/Extensions/ServiceCollectionExtensions.cs", "$SRV/Chaos/Program.cs", "$SRV/Chaos/appsettings.json", "$SRV/Chaos/Chaos.csproj", "$SRV/.gitignore", "$SRV/Tests/Chaos.Tests/ChatTranslation/ChatTranslationServiceTests.cs"], "verifyCommand": "cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ChatTranslationServiceTests/*\"", "acceptanceCriteria": ["IsEnabled/NextLineId", "DetectLanguage with shortcuts", "shortcut and cache skip providers", "chain order/transient/exhausted/timeout", "wrong language cached null", "speaker limit notice once", "in-flight merge", "delivery args", "persist false never on disk", "Submit never throws", "starts without translation file"], "modelTier": "frontier"}
```

---

### Task 8: Capture lines — say/shout, whisper, group, guild, channels

**Goal:** Every player chat path assigns a line id, sends it on the original packets, collects readers and submits a job, without changing what anyone sees today.

**Files:**
- Modify: `$SRV/Chaos/Models/World/Abstractions/Creature.cs` (`ShowPublicMessage` core returns recipients and takes a line id)
- Modify: `$SRV/Chaos/Models/World/Aisling.cs` (new `ShowChatMessage`)
- Modify: `$SRV/Chaos/Networking/Abstractions/IChaosWorldClient.cs`, `$SRV/Chaos/Networking/ChaosWorldClient.cs` (line-id overloads of `SendDisplayPublicMessage` and `SendServerMessage`)
- Modify: `$SRV/Chaos.Messaging/ChannelService.cs`, `$SRV/Chaos.Messaging.Abstractions/IChannelService.cs` (or wherever `IChannelService` lives), and the channel details / delegate type
- Modify: `$SRV/Chaos/Collections/Group.cs`, `$SRV/Chaos/Collections/Guild.cs`, `$SRV/Chaos/Services/Configuration/OptionsConfigurer.cs`, `$SRV/Chaos/Services/Servers/WorldServer.cs` (custom-channel registration near line 319, `OnPublicMessage`, `OnWhisper`, option toggle), `$SRV/Chaos/Messaging/CreateChannelCommand.cs`, `$SRV/Chaos/Utilities/Helpers.cs` (`DefaultChannelMessageHandler`)
- Test: `$SRV/Tests/Chaos.Messaging.Tests/ChannelServiceTests.cs` (extend); `$SRV/Tests/Chaos.Tests/ChatTranslation/ChatLineCaptureTests.cs` (new, for `ChatLineCapture` below)

**Acceptance Criteria:**
- [ ] With translation off (`NextLineId() == 0`), every packet and every script call is exactly as before; existing tests pass unchanged.
- [ ] Say/shout: `WorldServer.OnPublicMessage` calls `Aisling.ShowChatMessage(type, message, tags, lineId)`; the original packets carry the line id; `OnPublicMessage` scripts get the typed text; readers = recipients that are not the speaker and whose `Options.ChatLanguage` names the target language. Chants never get a line id.
- [ ] Whisper: the target's packet carries the line id; the sender's echo carries none; reader = the target when their language matches. `Persist = false`.
- [ ] Group, guild, `!global`, `!trade`, custom channels: `ChannelService.SendMessage(subscriber, channelName, message, tags, lineId)` passes the id to the send action and returns the subscribers it delivered to (not the ignoring ones). Orange-bar copies carry no line id.
- [ ] No job is submitted when `DetectLanguage` is null or no reader wants the target language.
- [ ] Setting `UserOption.ChatLanguage` to English/Korean sends the disclosure line, or the "not available" line when translation is off. Setting Off sends nothing extra. Both still echo `UserOptions`.
- [ ] Existing `ChannelServiceTests` still pass; new tests cover the returned recipients and the line id reaching the send action.

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Messaging.Tests/Chaos.Messaging.Tests.csproj` → pass; `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ChatLineCaptureTests/*"` → pass

**Steps:**

- [ ] **Step 1: A small pure helper for reader selection, test-first**

Create `$SRV/Chaos/Services/ChatTranslation/ChatLineCapture.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
#endregion

namespace Chaos.Services.ChatTranslation;

/// <summary>The rule for who reads a line's translation: not the speaker, and reading the line's target language.</summary>
public static class ChatLineCapture
{
    public static IReadOnlyList<Aisling> SelectReaders(IEnumerable<Aisling> recipients, Aisling speaker, ChatLanguage source)
    {
        var target = ChatTranslationText.TargetOf(source);

        return recipients.Where(reader => !ReferenceEquals(reader, speaker) && (ChatLanguages.Parse(reader.Options.ChatLanguage) == target))
                         .ToList();
    }
}
```

Test (`ChatLineCaptureTests`): three aislings from the test infrastructure (speaker set to Korean, one reader English, one reader Off) → `SelectReaders(all, speaker, Korean)` returns only the English reader; `SelectReaders(all, speaker, English)` returns nobody except a Korean-set reader (set one to Korean and check).

- [ ] **Step 2: ChannelService — test first**

In `ChannelServiceTests`, add tests that register a channel whose send action records `(subscriber, message, tags, lineId)`, subscribe two subscribers (one ignoring the sender), send with `lineId: 9`, and assert: the action got `9` for the non-ignoring subscriber only; the returned list holds exactly that subscriber. Keep the old overloads compiling: `SendMessage(subscriber, channelName, message)` and `SendMessage(subscriber, channelName, message, tags)` call the new one with `lineId: 0`.

Implement:
- The tags-carrying send-action delegate gains a `uint` line id. Define `public delegate void ChannelMessageSender(IChannelSubscriber subscriber, string message, IReadOnlyList<ChatTag> tags, uint lineId);` next to `IChannelService` and use it in `ChannelDetails` and in the tags overload of `RegisterChannel`. The plain `Action<IChannelSubscriber, string>` overload wraps as `(sub, msg, _, _) => action(sub, msg)`.
- `SendMessage(IChannelSubscriber subscriber, string channelName, string message, IReadOnlyList<ChatTag> tags, uint lineId)` returns `IReadOnlyList<IChannelSubscriber>` (empty on every early return), collecting each subscriber it sent to. Keep every existing check and the log line.
- Update all registrations to the new delegate: `OptionsConfigurer` (default channels), `WorldServer` (custom channel re-create), `CreateChannelCommand`, `Group`, `Guild`. `Helpers.DefaultChannelMessageHandler(subscriber, message, tags, lineId)` passes the line id into `SendDisplayPublicMessage` (the `uint.MaxValue` source id stays). Group and guild pass it to `SendServerMessage(GroupChat/GuildChat, msg, tags, lineId)`. Their orange-bar copies stay without one.

- [ ] **Step 3: Line-id send overloads**

`IChaosWorldClient` / `ChaosWorldClient`:

```csharp
    void SendDisplayPublicMessage(uint id, PublicMessageType publicMessageType, string message, IReadOnlyList<ChatTag> tags, uint lineId);
    void SendServerMessage(ServerMessageType serverMessageType, string message, IReadOnlyList<ChatTag> tags, uint lineId);
```

The existing 4-argument versions call these with `lineId: 0`. In `SendServerMessage`, set `LineId` on the args only when `GetMessageLines` returns one line (whisper/group/guild always do); chunked lines get 0.

- [ ] **Step 4: Say / shout**

`Creature`: move the body of `ShowPublicMessage(type, message, tags)` into

```csharp
    /// <summary>
    ///     Sends a public message and returns the players who got it. <paramref name="lineId" /> goes on each packet for
    ///     chat translation (0 for none). Scripts in range still get the typed text.
    /// </summary>
    protected IReadOnlyList<Aisling> ShowPublicMessageCore(
        PublicMessageType publicMessageType,
        string message,
        IReadOnlyList<ChatMessageTag> tags,
        uint lineId)
```

It returns `[]` when `CanTalk()` fails, collects each aisling it sends to only when `lineId != 0` (no allocation otherwise), and passes `lineId` to `SendDisplayPublicMessage` only for Normal and Shout (0 for Chant). The virtual `ShowPublicMessage(type, message, tags)` becomes `=> ShowPublicMessageCore(type, message, tags, 0);` (keep it a block body returning void).

`Aisling`: add

```csharp
    /// <summary>
    ///     A player's own say or shout from the chat box: logged like <see cref="ShowPublicMessage(PublicMessageType, string, IReadOnlyList{ChatMessageTag})" />,
    ///     sent with <paramref name="lineId" />, and returns the players who got it so their translation can follow.
    /// </summary>
    public IReadOnlyList<Aisling> ShowChatMessage(
        PublicMessageType publicMessageType,
        string message,
        IReadOnlyList<ChatMessageTag> tags,
        uint lineId)
    {
        if (!Script.CanTalk())
            return [];

        //same log line as ShowPublicMessage
        ...

        return ShowPublicMessageCore(publicMessageType, message, tags, lineId);
    }
```

(Copy the existing log statement; don't duplicate the chant branch: `WorldServer.OnPublicMessage` only sees Normal/Shout because chants arrive through `OnChant`. If `PublicMessageArgs.PublicMessageType` can be Chant there, pass lineId 0 for it.)

`WorldServer.OnPublicMessage`: inject `IChatTranslationService ChatTranslation` into `WorldServer` (constructor + field, like `ChatFilterService`). Replace the last line with:

```csharp
            var filterResult = ChatFilterService.Detect(localArgs.Message);
            var aisling = localClient.Aisling;
            var source = ChatTranslation.IsEnabled ? ChatTranslation.DetectLanguage(localArgs.Message) : null;
            var lineId = source is null ? 0u : ChatTranslation.NextLineId();

            var recipients = aisling.ShowChatMessage(localArgs.PublicMessageType, localArgs.Message, filterResult.Tags, lineId);

            if (source is { } language && (lineId != 0))
            {
                var readers = ChatLineCapture.SelectReaders(recipients, aisling, language);

                if (readers.Count != 0)
                    ChatTranslation.Submit(
                        new ChatTranslationJob(
                            lineId,
                            aisling,
                            aisling.Name,
                            localArgs.Message,
                            language,
                            readers,
                            ChatTranslationText.PublicBodyLimit(aisling.Name),
                            Persist: true));
            }
```

(Only lines with a detected language get a line id, so lines with nothing to translate stay byte-identical.)

- [ ] **Step 5: Whisper and channels in `WorldServer.OnWhisper`**

Compute once near the top, after the length check:

```csharp
            var source = ChatTranslation.IsEnabled ? ChatTranslation.DetectLanguage(localArgs.Message) : null;
            var lineId = source is null ? 0u : ChatTranslation.NextLineId();
```

Channel branch: `Group.SendMessage`, `Guild.SendMessage` get a `uint lineId` parameter and return `IReadOnlyList<IChannelSubscriber>` from `ChannelService.SendMessage`. Keep their `ChatMessageTag` → wire-tag conversion. The `ChannelService.SendMessage(fromAisling, localArgs.TargetName, localArgs.Message, wireTags, lineId)` call returns recipients too. After whichever branch ran:

```csharp
                if (source is { } language && (lineId != 0))
                {
                    var readers = ChatLineCapture.SelectReaders(recipients.OfType<Aisling>(), fromAisling, language);

                    if (readers.Count != 0)
                        ChatTranslation.Submit(
                            new ChatTranslationJob(lineId, fromAisling, fromAisling.Name, localArgs.Message, language, readers,
                                ChatTranslationText.ChannelBodyLimit(fromAisling.Name), Persist: true));
                }
```

(`recipients` is an empty list on the early-return paths; declare it before the branches.)

Direct whisper: the echo to the sender keeps `wireTags` and no line id. The target's send becomes `targetAisling.Client.SendServerMessage(ServerMessageType.Whisper, $"[{fromAisling.Name}]: {message}", wireTags, lineId)`, but only when not ignored (the ignored path returns before it, unchanged). Then:

```csharp
            if (source is { } whisperLanguage && (lineId != 0))
            {
                var readers = ChatLineCapture.SelectReaders([targetAisling], fromAisling, whisperLanguage);

                if (readers.Count != 0)
                    ChatTranslation.Submit(
                        new ChatTranslationJob(lineId, fromAisling, fromAisling.Name, message, whisperLanguage, readers,
                            ChatTranslationText.WhisperBodyLimit(fromAisling.Name), Persist: false));
            }
```

(`message` is the possibly-cut body the target actually received.)

- [ ] **Step 6: Option toggle disclosure**

In the OptionToggle `Set` handler, the `TrySetChatFilterOption` branch: after the echo, when `localArgs.Option == UserOption.ChatLanguage`:

```csharp
                        if (localArgs.Option == UserOption.ChatLanguage && (localArgs.Value is 1 or 2))
                            localClient.Aisling.SendOrangeBarMessage(
                                ChatTranslation.IsEnabled
                                    ? "Chat lines you read in the other language are sent to a translation service."
                                    : "Chat translation is not available right now.");
```

- [ ] **Step 7: Build and run tests**

Run: `cd $SRV && dotnet build Chaos/Chaos.csproj` → 0 errors. Fix every compile error from the changed delegate and return types (search the solution for `RegisterChannel(` and `.SendMessage(` on groups/guilds; test projects too: `Tests/Chaos.Tests` may construct `Group`/`Guild` or mock `IChannelService`; update those mocks to the new signatures).
Run: Messaging tests, the `ChatLineCaptureTests` class, and then the full `Chaos.Tests` project: expect only the known failure.

```json:metadata
{"files": ["$SRV/Chaos/Services/ChatTranslation/ChatLineCapture.cs", "$SRV/Chaos/Models/World/Abstractions/Creature.cs", "$SRV/Chaos/Models/World/Aisling.cs", "$SRV/Chaos/Networking/Abstractions/IChaosWorldClient.cs", "$SRV/Chaos/Networking/ChaosWorldClient.cs", "$SRV/Chaos.Messaging/ChannelService.cs", "$SRV/Chaos/Collections/Group.cs", "$SRV/Chaos/Collections/Guild.cs", "$SRV/Chaos/Services/Configuration/OptionsConfigurer.cs", "$SRV/Chaos/Services/Servers/WorldServer.cs", "$SRV/Chaos/Messaging/CreateChannelCommand.cs", "$SRV/Chaos/Utilities/Helpers.cs", "$SRV/Tests/Chaos.Messaging.Tests/ChannelServiceTests.cs", "$SRV/Tests/Chaos.Tests/ChatTranslation/ChatLineCaptureTests.cs"], "verifyCommand": "cd $SRV && dotnet run --project Tests/Chaos.Messaging.Tests/Chaos.Messaging.Tests.csproj", "acceptanceCriteria": ["off = unchanged packets", "say/shout line id + readers + scripts typed text", "whisper target only, persist false", "channels return recipients + pass line id", "no job without language or readers", "option disclosure lines", "existing tests pass"], "modelTier": "frontier"}
```

---

### Task 9: `/translation` admin command

**Goal:** Admins can see translation health in game.

**Files:**
- Create: `$SRV/Chaos/Messaging/Admin/TranslationCommand.cs`
- Test: `$SRV/Tests/Chaos.Tests/ChatTranslation/TranslationCommandTests.cs` (format helper only)

**Acceptance Criteria:**
- [ ] `/translation` prints one orange-bar line per fact: enabled or off; per provider `Azure: 120,000 / 2,000,000 (usable)` or `(over 90%)` or `(used up until 2026-10-01)`; `Cache: N entries, H hits, M misses`; `In flight: K`.
- [ ] Registered like other admin commands (the `[Command]` attribute is enough if commands are discovered by reflection; check `EmblemCommand`'s registration).

**Verify:** `cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/TranslationCommandTests/*"` → pass

**Steps:**

- [ ] **Step 1: Test the formatter**

```csharp
public sealed class TranslationCommandTests
{
    [Test]
    public void Format_ListsProvidersAndCache()
    {
        var status = new ChatTranslationStatus(
            true,
            [
                new ProviderStatus("Azure", 120_000, 2_000_000, true, null),
                new ProviderStatus("DeepL", 460_000, 500_000, false, null),
                new ProviderStatus("Google", 0, 500_000, false, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero))
            ],
            10,
            7,
            3,
            1);

        TranslationCommand.Format(status).Should().Equal(
            "Chat translation: on",
            "Azure: 120,000 / 2,000,000 (usable)",
            "DeepL: 460,000 / 500,000 (over limit)",
            "Google: 0 / 500,000 (used up until 2026-10-01)",
            "Cache: 10 entries, 7 hits, 3 misses",
            "In flight: 1");
    }
}
```

- [ ] **Step 2: Implement**

```csharp
[Command("translation", helpText: "shows chat translation quota and cache")]
public sealed class TranslationCommand(IChatTranslationService chatTranslation) : ICommand<Aisling>
{
    public ValueTask ExecuteAsync(Aisling source, ArgumentCollection args)
    {
        foreach (var line in Format(chatTranslation.GetStatus()))
            source.SendOrangeBarMessage(line);

        return default;
    }

    public static IReadOnlyList<string> Format(ChatTranslationStatus status)
    {
        var lines = new List<string> { $"Chat translation: {(status.Enabled ? "on" : "off")}" };

        foreach (var provider in status.Providers)
        {
            var state = provider.ExhaustedUntil is { } until
                ? $"used up until {until:yyyy-MM-dd}"
                : provider.Usable ? "usable" : "over limit";

            lines.Add($"{provider.Name}: {provider.Characters:N0} / {provider.MonthlyCharacters:N0} ({state})");
        }

        lines.Add($"Cache: {status.CacheEntries} entries, {status.CacheHits} hits, {status.CacheMisses} misses");
        lines.Add($"In flight: {status.InFlight}");

        return lines;
    }
}
```

Use the invariant culture for `N0` (`string.Create(CultureInfo.InvariantCulture, $"...")`) so the test is stable on any machine.

- [ ] **Step 3: Run the test** → pass.

```json:metadata
{"files": ["$SRV/Chaos/Messaging/Admin/TranslationCommand.cs", "$SRV/Tests/Chaos.Tests/ChatTranslation/TranslationCommandTests.cs"], "verifyCommand": "cd $SRV && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/TranslationCommandTests/*\"", "acceptanceCriteria": ["formatted status lines", "command registered"], "modelTier": "mechanical"}
```

---

### Task 10: Unora data — ChatTranslation.json and gitignore

**Goal:** The tracked config file and an ignored storage folder in the data repo.

**Files:**
- Create: `$UNO/Data/Configuration/ChatTranslation.json`
- Modify: `$UNO/.gitignore` (add `Data/LocalStorage/ChatTranslation/`)

**Acceptance Criteria:**
- [ ] The file parses with `ChatTranslationConfig.Load` (valid JSON, the shape from Task 4).
- [ ] `git -C $UNO check-ignore Data/LocalStorage/ChatTranslation/cache.jsonl` prints the path.

**Verify:** `python -c "import json;json.load(open(r'C:/Users/Michael/Documents/GitHub/worktrees/chat-translation-unora/Data/Configuration/ChatTranslation.json',encoding='utf-8'))"` → no output

**Steps:**

- [ ] **Step 1: Write the file** (UTF-8, no BOM):

```json
{
  "Shortcuts": {
    "KoreanToEnglish": {
      "ㅋㅋ": "lol",
      "ㅎㅎ": "haha",
      "ㅎㅇ": "hi",
      "ㅂㅂ": "bye",
      "ㄱㄱ": "let's go",
      "ㅇㅋ": "ok",
      "ㅇㅇ": "yeah",
      "ㄴㄴ": "no",
      "ㄳ": "thanks",
      "ㄱㅅ": "thanks",
      "ㅈㅅ": "sorry",
      "ㅠㅠ": ":(",
      "ㅜㅜ": ":(",
      "ㄷㄷ": "wow",
      "안녕": "hi",
      "안녕하세요": "hello",
      "감사합니다": "thank you",
      "고마워": "thanks",
      "고마워요": "thanks",
      "네": "yes",
      "아니요": "no"
    },
    "EnglishToKorean": {
      "lol": "ㅋㅋㅋ",
      "lmao": "ㅋㅋㅋㅋ",
      "haha": "ㅎㅎ",
      "hi": "안녕하세요",
      "hello": "안녕하세요",
      "hey": "안녕",
      "ty": "고마워요",
      "thx": "고마워요",
      "thanks": "고마워요",
      "thank you": "감사합니다",
      "gg": "gg",
      "brb": "잠깐만요, 곧 올게요",
      "np": "천만에요",
      "afk": "잠시 자리 비움",
      "omw": "가는 중이에요",
      "ok": "알겠어요",
      "okay": "알겠어요",
      "yes": "네",
      "no": "아니요",
      "bye": "안녕히 계세요",
      "gl": "행운을 빌어요",
      "wb": "어서 와요"
    }
  },
  "KoreanNames": {
    "밀레스": "Mileth",
    "아벨": "Abel",
    "루어스": "Loures",
    "수오미": "Suomi",
    "피엣": "Piet",
    "운디네": "Undine"
  }
}
```

- [ ] **Step 2: Add the gitignore line** under the existing `Data/LocalStorage/ChatFilterEdits.json` line.

```json:metadata
{"files": ["$UNO/Data/Configuration/ChatTranslation.json", "$UNO/.gitignore"], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/chat-translation-unora check-ignore Data/LocalStorage/ChatTranslation/cache.jsonl", "acceptanceCriteria": ["valid config JSON", "storage folder ignored"], "modelTier": "mechanical"}
```

---

### Task 11: Client networking — ChatTranslation packet event

**Goal:** The client receives opcode 140 and raises an event.

**Files:**
- Modify: `$CLI/Chaos.Client.Networking/ConnectionManager.cs` (event, handler registration, handler)
- Modify: `$CLI/Chaos.Client.Networking/Definitions/Delegates.cs` (`ChatTranslationHandler`)
- Test: if `$CLI/Tests/Chaos.Client.Tests` has a test that checks every `ServerOpCode` has a handler, make it pass; otherwise none.

**Acceptance Criteria:**
- [ ] `PacketHandlers[(byte)ServerOpCode.ChatTranslation]` is set and raises `OnChatTranslation(ChatTranslationArgs)`.
- [ ] The client builds against `$SRV`.

**Verify:** `cd $CLI && UnoraServerPath=$SRV dotnet build Chaos.Client/Chaos.Client.csproj` → 0 errors

**Steps:**

- [ ] **Step 1:** Mirror the `GuildEmblemDesign` wiring exactly (search `GuildEmblemDesign` in `ConnectionManager.cs` and `Delegates.cs`): a `public event ChatTranslationHandler? OnChatTranslation;`, `PacketHandlers[(byte)ServerOpCode.ChatTranslation] = HandleChatTranslation;`, and

```csharp
    private void HandleChatTranslation(ServerPacket pkt)
    {
        var args = Client.Deserialize<ChatTranslationArgs>(in pkt);
        OnChatTranslation?.Invoke(args);
    }
```

`Delegates.cs`: `public delegate void ChatTranslationHandler(ChatTranslationArgs args);`

- [ ] **Step 2:** Check whether the client registers packet converters by list (search `DisplayPublicMessageConverter` in `$CLI`). If it does, add `ChatTranslationConverter`.

- [ ] **Step 3:** Build.

```json:metadata
{"files": ["$CLI/Chaos.Client.Networking/ConnectionManager.cs", "$CLI/Chaos.Client.Networking/Definitions/Delegates.cs"], "verifyCommand": "cd $CLI && UnoraServerPath=$SRV dotnet build Chaos.Client/Chaos.Client.csproj", "acceptanceCriteria": ["handler registered and event raised", "client builds"], "modelTier": "mechanical"}
```

---

### Task 12: Client chat model and panel — replace a line in place, hover shows the original

**Goal:** A chat-log line can be swapped by line id without moving the reader's view; hovering a translated row shows the original.

**Files:**
- Modify: `$CLI/Chaos.Client/ViewModel/Chat.cs`
- Modify: `$CLI/Chaos.Client/Collections/CircularBuffer.cs` (add an index setter)
- Modify: `$CLI/Chaos.Client/Controls/World/Hud/Panel/ChatPanel.cs`
- Modify: the delegate file that declares `ChatMessageAddedHandler` (add `ChatMessageReplacedHandler`)
- Test: `$CLI/Tests/Chaos.Client.Tests/ChatReplaceTests.cs` (and `CircularBufferTests` if that class exists)

**Acceptance Criteria:**
- [ ] `Chat.ChatMessage` gains `uint LineId = 0` and `string? HoverText = null` (last parameters). `AddMessage(text, color, originalText, tags, lineId = 0)`.
- [ ] `Chat.TryReplaceMessage(uint lineId, string text, IReadOnlyList<ChatTag> tags, string hoverText)` returns false for 0 or an unknown id, else replaces the newest message with that id (keeping its color), stores `hoverText`, raises `MessageReplaced(oldMessage, newMessage)`, and returns true.
- [ ] `Chat.FindMessage(uint lineId)` returns the stored message or null.
- [ ] `ChatPanel`: rows carry `LineId` and `HoverText`; on `MessageReplaced` the old rows are removed and the re-wrapped rows are inserted at the same index; when the view is pinned to the bottom it stays pinned; when scrolled up, the first visible line stays the same line (adjust the offset by the row-count difference when the change is above the view top). Check `VirtualizedRowList` for an offset API; add a small one if needed.
- [ ] Hovering a row with `HoverText` shows it in a tooltip using the existing tooltip look (mirror how the world list shows the emblem name tooltip: search `Tooltip` in `Controls/World/Popups/WorldList/WorldListControl.cs`). The tooltip hides when the mouse leaves the row.

**Verify:** `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/ChatReplaceTests/*"` → pass

**Steps:**

- [ ] **Step 1: Tests for the model**

```csharp
public sealed class ChatReplaceTests
{
    [Test]
    public void TryReplaceMessage_SwapsByLineId_AndRaisesEvent()
    {
        var chat = new Chat();
        chat.AddMessage("Haneul: 안녕", Color.White, "Haneul: 안녕", [], lineId: 5);
        chat.AddMessage("Bob: hi", Color.White, "Bob: hi", []);

        Chat.ChatMessage? replaced = null;
        chat.MessageReplaced += (_, now) => replaced = now;

        chat.TryReplaceMessage(5, "[Kor] Haneul: hi", [], "Haneul: 안녕").Should().BeTrue();

        replaced!.Value.Text.Should().Be("[Kor] Haneul: hi");
        replaced.Value.HoverText.Should().Be("Haneul: 안녕");
        chat.FindMessage(5)!.Value.Text.Should().Be("[Kor] Haneul: hi");
    }

    [Test]
    public void TryReplaceMessage_UnknownOrZero_ReturnsFalse()
    {
        var chat = new Chat();
        chat.AddMessage("x", Color.White);

        chat.TryReplaceMessage(0, "y", [], "x").Should().BeFalse();
        chat.TryReplaceMessage(99, "y", [], "x").Should().BeFalse();
    }
}
```

Adapt `Chat` construction to however existing client tests create it (search `new Chat()` in `Tests/`). Add a `ChatPanel` row-replacement test only if existing tests already construct `ChatPanel` (search `new ChatPanel(`); if the panel needs the graphics device, test the row logic by extracting it into a small static helper `ChatRows.Replace(List<ChatLine> rows, uint lineId, IReadOnlyList<ChatLine> newRows) → (int index, int removed)` and test that instead.

- [ ] **Step 2: Implement** the model changes, `CircularBuffer` setter (`set { ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count); Buffer[(Head - Count + index + Buffer.Length) % Buffer.Length] = value; }`), the event (`public delegate void ChatMessageReplacedHandler(Chat.ChatMessage previous, Chat.ChatMessage current);` next to `ChatMessageAddedHandler`), and the panel changes. Search newest-first in `TryReplaceMessage` (the translation targets a recent line).

- [ ] **Step 3: Run the tests; build the client.**

```json:metadata
{"files": ["$CLI/Chaos.Client/ViewModel/Chat.cs", "$CLI/Chaos.Client/Collections/CircularBuffer.cs", "$CLI/Chaos.Client/Controls/World/Hud/Panel/ChatPanel.cs", "$CLI/Tests/Chaos.Client.Tests/ChatReplaceTests.cs"], "verifyCommand": "cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/ChatReplaceTests/*\"", "acceptanceCriteria": ["ChatMessage LineId/HoverText", "TryReplaceMessage + event", "FindMessage", "panel replaces rows in place and keeps scroll", "hover tooltip shows original"], "modelTier": "standard"}
```

---

### Task 13: Client handlers — line ids in, translation swap, bubbles, orange bar

**Goal:** Incoming lines remember their line id; a `ChatTranslation` packet swaps the chat line and the bubble and re-shows transient copies.

**Files:**
- Create: `$CLI/Chaos.Client/Chat/TranslatedLineBuilder.cs`
- Create: `$CLI/Chaos.Client/Screens/WorldScreen.ChatTranslation.cs` (partial: wire/unwire, pending-line map, handler)
- Modify: `$CLI/Chaos.Client/Chat/ChatDisplayBuilder.cs` (make `SplitPrefix` `public`)
- Modify: `$CLI/Chaos.Client/Screens/WorldScreen.ServerHandlers.cs` (pass `args.LineId` into `AddMessage`, bubbles and the pending-line map)
- Modify: `$CLI/Chaos.Client/Screens/WorldScreen.Wiring.cs` or wherever `WireGuildEmblem()` is called (call `WireChatTranslation()` / unwire)
- Modify: `$CLI/Chaos.Client/Controls/World/ViewPort/ChatBubble.cs` (`LineId` property), `$CLI/Chaos.Client/Rendering/EntityOverlayManager.cs` (`AddChatBubble(..., uint lineId = 0)`, `TryReplaceChatBubble(uint lineId, string message)`), `$CLI/Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs` (same pair)
- Test: `$CLI/Tests/Chaos.Client.Tests/TranslatedLineBuilderTests.cs`

**Acceptance Criteria:**
- [ ] `TranslatedLineBuilder.Build(originalLine, translatedBody, tags, source, mode, fantasy)` returns `(ChatLog, Bubble)`: prefix from `SplitPrefix(originalLine)` + filtered body; the chat-log version has `[Kor] ` (source Korean) or `[영어] ` (source English) inserted after a leading `{=` + 1 color character when present, else at index 0. Examples: `("Haneul: 안녕", "hi", Korean)` → `("[Kor] Haneul: hi", "Haneul: hi")`; `("{=c[!global] Bob: hello", "안녕", English)` → `("{=c[영어] [!global] Bob: 안녕", "{=c[!global] Bob: 안녕")`; `("[Bob]: hey", "안녕", English)` → `("[영어] [Bob]: 안녕", ...)`.
- [ ] Every chat path that adds a chat-log message passes `args.LineId` (public, whisper, group, guild). When `LineId != 0` the screen records `lineId → (Kind, SourceId, IsShout, Color, OriginalLine)` in a map capped at 256 entries (drop the oldest).
- [ ] Public lines with an entity create the bubble with the line id; poker bubbles too.
- [ ] On `ChatTranslation`: look up the pending line (ignore unknown ids); build the two strings from the ORIGINAL line; `Chat.TryReplaceMessage(lineId, chatLog, tags, hoverText: stored message's display text)`; `Overlays.TryReplaceChatBubble(lineId, bubble)` and the poker equivalent; for whisper/group/guild kinds also `AddOrangeBarMessage(chatLog, color)` and `SystemMessagePane.AddMessage(chatLog, color)`; for world-channel lines (public with `SourceId == uint.MaxValue`) `AddOrangeBarMessage(chatLog)`.
- [ ] `TryReplaceChatBubble` swaps only a bubble whose `LineId` matches; the new bubble keeps entity, shout style and line id and restarts its timer.

**Verify:** `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/TranslatedLineBuilderTests/*"` → pass; client build → 0 errors

**Steps:**

- [ ] **Step 1: Test the builder**

```csharp
public sealed class TranslatedLineBuilderTests
{
    [Test]
    public void Public_KoreanSource()
    {
        var (chatLog, bubble) = TranslatedLineBuilder.Build("Haneul: 안녕", "hi", [], ChatLanguage.Korean, ChatFilterMode.Unfiltered, FantasyDictionary.Empty);

        chatLog.Should().Be("[Kor] Haneul: hi");
        bubble.Should().Be("Haneul: hi");
    }

    [Test]
    public void Channel_TagGoesAfterColorCode()
    {
        var (chatLog, bubble) = TranslatedLineBuilder.Build("{=c[!global] Bob: hello", "안녕", [], ChatLanguage.English, ChatFilterMode.Unfiltered, FantasyDictionary.Empty);

        chatLog.Should().Be("{=c[영어] [!global] Bob: 안녕");
        bubble.Should().Be("{=c[!global] Bob: 안녕");
    }

    [Test]
    public void Whisper_KeepsBracketPrefix()
        => TranslatedLineBuilder.Build("[Bob]: hey", "안녕", [], ChatLanguage.English, ChatFilterMode.Unfiltered, FantasyDictionary.Empty)
                                .ChatLog.Should().Be("[영어] [Bob]: 안녕");
}
```

(Check the names: `ChatFilterMode` and `FantasyDictionary.Empty` as used by `ChatDisplayBuilder`.)

- [ ] **Step 2: Implement the builder**

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Chat;

/// <summary>
///     Builds the text shown when a chat line's translation arrives: the original line's prefix, the translated body
///     through the reader's chat filter, and, for the chat log only, a tag naming the original language.
/// </summary>
public static class TranslatedLineBuilder
{
    public static (string ChatLog, string Bubble) Build(
        string originalLine,
        string translatedBody,
        IReadOnlyList<ChatTag> tags,
        ChatLanguage source,
        ChatFilterMode mode,
        FantasyDictionary fantasy)
    {
        var (prefix, _) = ChatDisplayBuilder.SplitPrefix(originalLine);
        var display = ChatDisplayBuilder.Build(prefix, translatedBody, tags, mode, fantasy).DisplayText;
        var tag = source == ChatLanguage.Korean ? "[Kor] " : "[영어] ";

        //a channel line may open with a color code ("{=" plus one color byte); the tag goes after it so the color holds
        var insertAt = display.StartsWith("{=", StringComparison.Ordinal) && (display.Length >= 3) ? 3 : 0;

        return (display.Insert(insertAt, tag), display);
    }
}
```

- [ ] **Step 3: Wire the handlers** as described in the acceptance criteria. Pending-line record:

```csharp
    private enum TranslatedLineKind { Public, WorldChannel, Whisper, Group, Guild }

    private readonly record struct PendingTranslationLine(TranslatedLineKind Kind, uint SourceId, bool IsShout, Color Color, string OriginalLine);
```

Store the ORIGINAL wire message (`args.Message`) as `OriginalLine`; the hover text is the chat log's current display text (`WorldState.Chat.FindMessage(lineId)?.Text`) read before replacing.

- [ ] **Step 4: Run the builder tests; build the client.**

```json:metadata
{"files": ["$CLI/Chaos.Client/Chat/TranslatedLineBuilder.cs", "$CLI/Chaos.Client/Chat/ChatDisplayBuilder.cs", "$CLI/Chaos.Client/Screens/WorldScreen.ChatTranslation.cs", "$CLI/Chaos.Client/Screens/WorldScreen.ServerHandlers.cs", "$CLI/Chaos.Client/Screens/WorldScreen.Wiring.cs", "$CLI/Chaos.Client/Controls/World/ViewPort/ChatBubble.cs", "$CLI/Chaos.Client/Rendering/EntityOverlayManager.cs", "$CLI/Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs", "$CLI/Tests/Chaos.Client.Tests/TranslatedLineBuilderTests.cs"], "verifyCommand": "cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/TranslatedLineBuilderTests/*\"", "acceptanceCriteria": ["builder tag placement", "line ids stored on every chat path", "bubbles carry line id", "translation swaps chat line + bubble + poker", "orange bar / pane copies for whisper group guild world", "bubble swap only same line id"], "modelTier": "standard"}
```

---

### Task 14: Client F4 setting and options sync

**Goal:** Players pick Off / English / Korean in F4 Chat; the value round-trips with the server.

**Files:**
- Modify: `$CLI/Chaos.Client/ViewModel/SettingDefinition.cs` (`SettingKey.ChatLanguage` + definition)
- Modify: `$CLI/Chaos.Client/Screens/WorldScreen.ServerHandlers.cs` (`HandleUserOptions`: `userOptions.ApplyChoice(SettingKey.ChatLanguage, args.ChatLanguage);`)
- Test: extend the client test that covers setting definitions if one exists (search `SettingDefinition` in `Tests/`)

**Acceptance Criteria:**
- [ ] F4 Chat section shows "Chat translation" with choices `Off`, `English`, `Korean (한국어)` right after "Chat filter".
- [ ] Choosing one sends `UserOption.ChatLanguage` with the index, the same way the chat filter dropdown sends its index (check how `ServerOption` choices are sent; the filter dropdown path must already handle byte values).
- [ ] The login options sync applies `args.ChatLanguage`.
- [ ] Client builds; client test suite passes.

**Verify:** `cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass

**Steps:**

- [ ] **Step 1:** Add `ChatLanguage` to `SettingKey` (after `HasConfiguredChatFilter`). Add, after the chat filter definition:

```csharp
        //── Chat translation ──
        //Same round-trip as the chat filter mode: the index travels in OptionToggleArgs.Value on UserOption.ChatLanguage
        //(31) and comes back in UserOptionsArgs.ChatLanguage. Choices order MUST match ChatLanguage (Off, English, Korean).
        new(
            SettingKey.ChatLanguage,
            "Chat translation",
            SettingSection.Chat,
            SettingCategory.ServerOption,
            UserOption.ChatLanguage,
            Span: SettingSpan.Full,
            Choices: ["Off", "English", "Korean (한국어)"],
            GetChoice: () => WorldState.UserOptions.ChoiceValue(SettingKey.ChatLanguage),
            SetChoice: i => WorldState.UserOptions.SelectChoice(SettingKey.ChatLanguage, i)),
```

- [ ] **Step 2:** In `HandleUserOptions`, after the chat filter line: `userOptions.ApplyChoice(SettingKey.ChatLanguage, args.ChatLanguage);`. If the options model needs a registered choice slot per key (check `ChoiceValue`/`ApplyChoice`), register `ChatLanguage` the same way as `ChatFilterMode`.

- [ ] **Step 3:** Run the full client tests and build.

```json:metadata
{"files": ["$CLI/Chaos.Client/ViewModel/SettingDefinition.cs", "$CLI/Chaos.Client/Screens/WorldScreen.ServerHandlers.cs"], "verifyCommand": "cd $CLI && UnoraServerPath=$SRV dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["dropdown in F4 Chat after filter", "sends index on UserOption.ChatLanguage", "login sync applies value", "client tests pass"], "modelTier": "mechanical"}
```

---

### Task 15: Commit the full implementation

**Goal:** Everything builds and passes; one commit per repo on `feat/chat-translation`.

**Files:**
- Modify: `$CLI` gitlink `Chaos-Server` → the new `$SRV` commit
- Commit: all changes in `$SRV`, `$CLI`, `$UNO`; the plan and its `.tasks.json` in `$CLI` (`git add -f`, `docs/superpowers/` is ignored)

**Acceptance Criteria:**
- [ ] `$SRV`: `Chaos.Networking.Tests`, `Chaos.Messaging.Tests` pass; `Chaos.Tests` passes except the one known failure.
- [ ] `$CLI`: client tests pass; client builds against `$SRV`.
- [ ] `git status` in each worktree is clean after committing, except ignored files.
- [ ] No file with an API key is staged (`appsettings.translation.json` absent from every commit).
- [ ] `$CLI`'s `Chaos-Server` gitlink points at the `$SRV` commit.

**Verify:** `git -C $SRV log --oneline -1 && git -C $CLI log --oneline -1 && git -C $UNO log --oneline -1` → three `feat/chat-translation` commits

**Steps:**

- [ ] **Step 1: Run every suite** (commands above). Record the counts.
- [ ] **Step 2: Commit `$SRV`**: `git -C $SRV add -A` is allowed here (it is a private worktree), but first run `git -C $SRV status --short` and make sure only this feature's files are listed. Message: `Add Korean-English chat translation (server)` + body listing the parts + the attribution lines from the session.
- [ ] **Step 3: Commit `$UNO`**: stage `Data/Configuration/ChatTranslation.json` and `.gitignore` by path only (never the `Custom Client Mods/**/obj` noise).
- [ ] **Step 4: Commit `$CLI`**: `git -C $CLI update-index --cacheinfo 160000,<SRV sha>,Chaos-Server`, stage the client files by path plus `git add -f docs/superpowers/plans/2026-09-29-chat-translation.md docs/superpowers/plans/2026-09-29-chat-translation.md.tasks.json`.
- [ ] **Step 5:** Do NOT merge, push or deploy.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/chat-translation-server log --oneline -1", "acceptanceCriteria": ["server suites pass (1 known failure)", "client tests pass and builds", "clean worktrees", "no key file committed", "client gitlink = server commit"], "modelTier": "standard"}
```
