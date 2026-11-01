# Login Scrying View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a live view of one random, active, non-GM, opted-in player inside the start screen's logo oval, switching every 20 seconds.

**Architecture:** A new background service in the server copies what is within 8 tiles of the chosen player about 10 times a second. It sends that as `ScryFrame` packets to clients that sent `ScryStart` on the login connection. The client keeps its own small entity store, draws the map and entities into an off-screen picture, cuts it to the oval, and draws it over the logo animation. The logo animation stays as the fallback.

**Tech Stack:** C# / .NET 10, MonoGame, Chaos.Networking packet converters, TUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-10-31-login-scrying-view-design.md`

**Scope note:** this plan covers spec Parts 1-3. Spec "Phase 2: spells and emotes" gets its own plan after this one ships and the playtest passes.

**Repos:** the client is `C:\Users\Michael\Documents\GitHub\Chaos.Client`. The server is its submodule `Chaos.Client\Chaos-Server` (a separate git repo). Server paths below are relative to `Chaos-Server/`. Client paths are relative to the client repo root.

## Global Constraints

- Opcodes: `ClientOpCode.ScryStart = 160`, `ClientOpCode.ScryStop = 161`, `ServerOpCode.ScryFrame = 176`.
- `UserOption.ShowOnLoginScreen = 32`, default **on** (true), including for saves that don't have the field.
- Tick 100 ms. Full frame every 10 ticks, and at once on a new pick or map change. Radius 8 tiles. Rotation 20 s. Idle limit 5 min (`Trackers.LastWalk`). Watcher cap 200.
- Client oval: the `LOGO` control rect (296x266). Fall back to the logo after 3 s with no frame. Fade in over 0.5 s.
- Players on map id `CONSTANTS.STREAMED_MAP_ID` (32000) are never picked.
- Frames never carry names or chat. Aisling and creature names are blanked to `string.Empty` on the server.
- Do not change the signature of any existing public interface member in the `Chaos.*` library projects. Adding new members, types and files is fine. (Forks implement these interfaces.)
- Packet dispatch uses opcode-indexed handler arrays, never switch statements (repo convention).
- Locks: use `Lock` + `EnterScope()`, never `lock` (client convention).
- Commit strategy is at-end: implementer tasks do **not** commit. Task 10 commits everything.

**User decisions (already made):**
- The oval shows a live view of a real map around one player. There is no crystal ball look.
- Truly live: about 10 updates a second, using streamed snapshots (not a ghost character, not a new visibility system).
- The view follows a random active player. GMs are never shown.
- Players can opt out with the "Show me on the login screen" setting, on by default.
- No chat and no caption: no player or map name.
- 20 seconds per player. Switch at once when the player stops being eligible.
- When nobody is eligible, or the map can't be drawn, the logo animation plays.
- Spells and emotes come in Phase 2.

## Review Focus

1. **The watched player walks through a door to another map.** Expect a full frame with the new map and the view reloading that map, not a stale map with entities in the wrong places. (Task 4 test `Map_change_forces_full_frame`, Task 7 test `Map_change_clears_entities`.)
2. **The client lacks the map file, or it is out of date.** Expect the logo, no crash, and recovery on the next pick. (Task 8 step: `MapUsable` false keeps fade at 0; Task 7 test covers the scene side.)
3. **A watcher disconnects or logs in while frames are being sent.** Expect the watcher to be removed, with no exception escaping the service loop. (Task 5 tests `Remove_on_disconnect` and `Send_failure_removes_watcher`.)
4. **Nobody is eligible: an empty server, or only GMs online.** Expect a single "none" frame (repeated at most once a second) and the logo. (Task 3 test `No_eligible_player_gives_no_pick`, Task 4 test `None_frame_sent_once_then_once_per_second`.)
5. **The feed goes silent, for example during a server restart.** Expect the logo after 3 seconds. (Task 7 test `Stale_feed_clears_view`.)

---

### Task 1: Scrying packets

**Goal:** Add `ScryStart`, `ScryStop` and `ScryFrame` packets to the shared networking projects, with round-trip tests.

**Files:**
- Modify: `Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs` (ClientOpCode after `KartInput = 159`, ServerOpCode after `KartRaceEvent = 175`)
- Create: `Chaos-Server/Chaos.Networking/Entities/Client/ScryStartArgs.cs`
- Create: `Chaos-Server/Chaos.Networking/Entities/Client/ScryStopArgs.cs`
- Create: `Chaos-Server/Chaos.Networking/Converters/Client/ScryStartConverter.cs`
- Create: `Chaos-Server/Chaos.Networking/Converters/Client/ScryStopConverter.cs`
- Create: `Chaos-Server/Chaos.Networking/Entities/Server/ScryFrameArgs.cs`
- Create: `Chaos-Server/Chaos.Networking/Converters/Server/ScryFrameConverter.cs`
- Modify: `Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs` (bump `CLIENT_VERSION` by one)
- Test: `Chaos-Server/Tests/Chaos.Tests/Scrying/ScryPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] The three opcodes exist with values 160, 161 and 176.
- [ ] `ScryFrameArgs` round-trips for the None, Full and Positions kinds, including an aisling look, a creature and a ground item.
- [ ] An aisling look is length-prefixed (`WriteData16`), so the `DisplayAislingConverter` trailing fields can't spill into the next entity.
- [ ] `CLIENT_VERSION` is bumped by one.

**Verify:** `cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ScryPacketConverterTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Add opcodes.** In `Enums.cs`, after `KartInput = 159,` in `ClientOpCode`, add the members below. Match the neighbours' doc-comment style, including `Hex value: 0xA0` and `0xA1`. Then, after `KartRaceEvent = 175,` in `ServerOpCode`, add `ScryFrame = 176` (hex `0xB0`).

```csharp
    /// <summary>
    ///     OpCode used when a client on the start screen asks to watch the login scrying view
    ///     <br />
    ///     Hex value: 0xA0
    /// </summary>
    ScryStart = 160,

    /// <summary>
    ///     OpCode used when a client stops watching the login scrying view
    ///     <br />
    ///     Hex value: 0xA1
    /// </summary>
    ScryStop = 161,
```

```csharp
    /// <summary>
    ///     OpCode used to stream the login scrying view to start-screen clients
    ///     <br />
    ///     Hex value: 0xB0
    /// </summary>
    ScryFrame = 176,
```

- [ ] **Step 2: Client-to-server args and converters.** Use the same `#region` usings as `KartInputArgs.cs` and `KartInputConverter.cs`.

```csharp
// Entities/Client/ScryStartArgs.cs
namespace Chaos.Networking.Entities.Client;

/// <summary>Represents the serialization of the <see cref="ClientOpCode.ScryStart" /> packet.</summary>
public sealed record ScryStartArgs : IPacketSerializable;
```

```csharp
// Entities/Client/ScryStopArgs.cs
namespace Chaos.Networking.Entities.Client;

/// <summary>Represents the serialization of the <see cref="ClientOpCode.ScryStop" /> packet.</summary>
public sealed record ScryStopArgs : IPacketSerializable;
```

```csharp
// Converters/Client/ScryStartConverter.cs
namespace Chaos.Networking.Converters.Client;

public sealed class ScryStartConverter : PacketConverterBase<ScryStartArgs>
{
    public override byte OpCode => (byte)ClientOpCode.ScryStart;

    public override ScryStartArgs Deserialize(ref SpanReader reader) => new();

    public override void Serialize(ref SpanWriter writer, ScryStartArgs args) { }
}
```

```csharp
// Converters/Client/ScryStopConverter.cs
namespace Chaos.Networking.Converters.Client;

public sealed class ScryStopConverter : PacketConverterBase<ScryStopArgs>
{
    public override byte OpCode => (byte)ClientOpCode.ScryStop;

    public override ScryStopArgs Deserialize(ref SpanReader reader) => new();

    public override void Serialize(ref SpanWriter writer, ScryStopArgs args) { }
}
```

- [ ] **Step 3: Write the failing round-trip tests.** Copy the `Bytes`, `Read` and `RoundTrip` helpers and the usings from `Tests/Chaos.Tests/Karting/KartPacketConverterTests.cs`. Use the same `Direction`, `CreatureType` and `DisplayColor` usings as `DisplayAislingArgs.cs`, `CreatureInfo.cs` and `GroundItemInfo.cs`.

```csharp
namespace Chaos.Tests.Scrying;

public sealed class ScryPacketConverterTests
{
    // Bytes / Read / RoundTrip helpers copied from KartPacketConverterTests

    [Test]
    public void Start_and_stop_round_trip()
    {
        RoundTrip(new ScryStartConverter(), new ScryStartArgs()).Should().BeOfType<ScryStartArgs>();
        RoundTrip(new ScryStopConverter(), new ScryStopArgs()).Should().BeOfType<ScryStopArgs>();
    }

    [Test]
    public void None_frame_round_trips()
    {
        var back = RoundTrip(new ScryFrameConverter(), new ScryFrameArgs { Kind = ScryFrameKind.None });

        back.Kind.Should().Be(ScryFrameKind.None);
        back.Entities.Should().BeEmpty();
    }

    [Test]
    public void Full_frame_round_trips_looks()
    {
        var args = new ScryFrameArgs
        {
            Kind = ScryFrameKind.Full,
            MapId = 500, Width = 60, Height = 40, CheckSum = 0xBEEF, TargetId = 7,
            Entities =
            [
                new ScryEntity
                {
                    Id = 7, Kind = ScryEntityKind.Aisling, X = 10, Y = 12, Direction = Direction.Down,
                    Aisling = new DisplayAislingArgs { Id = 7, X = 10, Y = 12, Direction = Direction.Down, Name = string.Empty, HeadSprite = 33 }
                },
                new ScryEntity
                {
                    Id = 900, Kind = ScryEntityKind.Creature, X = 11, Y = 12, Direction = Direction.Left,
                    Creature = new CreatureInfo { Id = 900, X = 11, Y = 12, Direction = Direction.Left, Sprite = 412, CreatureType = CreatureType.Normal, Name = string.Empty }
                },
                new ScryEntity
                {
                    Id = 901, Kind = ScryEntityKind.GroundItem, X = 9, Y = 9,
                    GroundItem = new GroundItemInfo { Id = 901, X = 9, Y = 9, Sprite = 140, Color = DisplayColor.Default }
                }
            ]
        };

        var back = RoundTrip(new ScryFrameConverter(), args);

        back.Kind.Should().Be(ScryFrameKind.Full);
        back.MapId.Should().Be(500);
        back.Width.Should().Be(60);
        back.Height.Should().Be(40);
        back.CheckSum.Should().Be(0xBEEF);
        back.TargetId.Should().Be(7u);
        back.Entities.Should().HaveCount(3);
        back.Entities[0].Aisling!.HeadSprite.Should().Be(33);
        back.Entities[0].Aisling!.Id.Should().Be(7u);
        back.Entities[1].Creature!.Sprite.Should().Be(412);
        back.Entities[1].Creature!.Direction.Should().Be(Direction.Left);
        back.Entities[2].GroundItem!.Sprite.Should().Be(140);
        back.Entities[2].X.Should().Be(9);
    }

    [Test]
    public void Positions_frame_round_trips_without_looks_and_with_removals()
    {
        var args = new ScryFrameArgs
        {
            Kind = ScryFrameKind.Positions,
            MapId = 500, Width = 60, Height = 40, CheckSum = 1, TargetId = 7,
            Entities = [new ScryEntity { Id = 7, Kind = ScryEntityKind.Aisling, X = 11, Y = 12, Direction = Direction.Right }],
            Removed = [900, 901]
        };

        var back = RoundTrip(new ScryFrameConverter(), args);

        back.Entities.Single().HasLook.Should().BeFalse();
        back.Entities.Single().X.Should().Be(11);
        back.Removed.Should().Equal(900u, 901u);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail.** Run the Verify command. Expected: a compile failure because `ScryFrameArgs` and the other new types don't exist yet.

- [ ] **Step 5: Add `ScryFrameArgs`.** Use the same usings as `DisplayAislingArgs.cs`, plus `Chaos.Packets.Abstractions`.

```csharp
// Entities/Server/ScryFrameArgs.cs
namespace Chaos.Networking.Entities.Server;

public enum ScryFrameKind : byte
{
    None = 0,
    Full = 1,
    Positions = 2
}

public enum ScryEntityKind : byte
{
    Aisling = 0,
    Creature = 1,
    GroundItem = 2
}

/// <summary>One thing in the scrying view. Exactly one look is set when the look is sent.</summary>
public sealed record ScryEntity
{
    public uint Id { get; set; }
    public ScryEntityKind Kind { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
    public Direction Direction { get; set; }
    public DisplayAislingArgs? Aisling { get; set; }
    public CreatureInfo? Creature { get; set; }
    public GroundItemInfo? GroundItem { get; set; }

    public bool HasLook => Aisling is not null || Creature is not null || GroundItem is not null;
}

/// <summary>Represents the serialization of the <see cref="ServerOpCode.ScryFrame" /> packet.</summary>
public sealed record ScryFrameArgs : IPacketSerializable
{
    public ScryFrameKind Kind { get; set; }
    public short MapId { get; set; }
    public byte Width { get; set; }
    public byte Height { get; set; }
    public ushort CheckSum { get; set; }
    public uint TargetId { get; set; }
    public List<ScryEntity> Entities { get; set; } = [];
    public List<uint> Removed { get; set; } = [];
}
```

- [ ] **Step 6: Add `ScryFrameConverter`.** Use the same usings as `KartSnapshotConverter.cs`, plus the `Direction`, `CreatureType` and `DisplayColor` namespaces.

```csharp
// Converters/Server/ScryFrameConverter.cs
namespace Chaos.Networking.Converters.Server;

public sealed class ScryFrameConverter : PacketConverterBase<ScryFrameArgs>
{
    private static readonly DisplayAislingConverter AislingLook = new();

    public override byte OpCode => (byte)ServerOpCode.ScryFrame;

    public override ScryFrameArgs Deserialize(ref SpanReader reader)
    {
        var args = new ScryFrameArgs { Kind = (ScryFrameKind)reader.ReadByte() };

        if (args.Kind == ScryFrameKind.None)
            return args;

        args.MapId = reader.ReadInt16();
        args.Width = reader.ReadByte();
        args.Height = reader.ReadByte();
        args.CheckSum = reader.ReadUInt16();
        args.TargetId = reader.ReadUInt32();

        var count = reader.ReadUInt16();

        for (var i = 0; i < count; i++)
        {
            var entity = new ScryEntity
            {
                Id = reader.ReadUInt32(),
                Kind = (ScryEntityKind)reader.ReadByte(),
                X = reader.ReadByte(),
                Y = reader.ReadByte(),
                Direction = (Direction)reader.ReadByte()
            };

            if (reader.ReadBoolean())
                switch (entity.Kind)
                {
                    case ScryEntityKind.Aisling:
                    {
                        var data = reader.ReadData16();
                        var span = new Span<byte>(data);
                        var inner = new SpanReader(reader.Encoding, in span);
                        entity.Aisling = AislingLook.Deserialize(ref inner);

                        break;
                    }
                    case ScryEntityKind.Creature:
                        entity.Creature = new CreatureInfo
                        {
                            Id = entity.Id,
                            X = entity.X,
                            Y = entity.Y,
                            Direction = entity.Direction,
                            Sprite = reader.ReadUInt16(),
                            CreatureType = (CreatureType)reader.ReadByte(),
                            Name = string.Empty
                        };

                        break;
                    case ScryEntityKind.GroundItem:
                        entity.GroundItem = new GroundItemInfo
                        {
                            Id = entity.Id,
                            X = entity.X,
                            Y = entity.Y,
                            Sprite = reader.ReadUInt16(),
                            Color = (DisplayColor)reader.ReadByte()
                        };

                        break;
                }

            args.Entities.Add(entity);
        }

        var removed = reader.ReadUInt16();

        for (var i = 0; i < removed; i++)
            args.Removed.Add(reader.ReadUInt32());

        return args;
    }

    public override void Serialize(ref SpanWriter writer, ScryFrameArgs args)
    {
        writer.WriteByte((byte)args.Kind);

        if (args.Kind == ScryFrameKind.None)
            return;

        writer.WriteInt16(args.MapId);
        writer.WriteByte(args.Width);
        writer.WriteByte(args.Height);
        writer.WriteUInt16(args.CheckSum);
        writer.WriteUInt32(args.TargetId);
        writer.WriteUInt16((ushort)args.Entities.Count);

        foreach (var entity in args.Entities)
        {
            writer.WriteUInt32(entity.Id);
            writer.WriteByte((byte)entity.Kind);
            writer.WriteByte(entity.X);
            writer.WriteByte(entity.Y);
            writer.WriteByte((byte)entity.Direction);
            writer.WriteBoolean(entity.HasLook);

            if (entity.Aisling is { } look)
            {
                var inner = new SpanWriter(writer.Encoding, usePooling: false);
                AislingLook.Serialize(ref inner, look);
                writer.WriteData16(inner.ToSpan().ToArray());
            } else if (entity.Creature is { } creature)
            {
                writer.WriteUInt16(creature.Sprite);
                writer.WriteByte((byte)creature.CreatureType);
            } else if (entity.GroundItem is { } item)
            {
                writer.WriteUInt16(item.Sprite);
                writer.WriteByte((byte)item.Color);
            }
        }

        writer.WriteUInt16((ushort)args.Removed.Count);

        foreach (var id in args.Removed)
            writer.WriteUInt32(id);
    }
}
```

If `SpanReader` has no `Encoding` property, pass the encoding the same way `SpanWriter.Encoding` is used: check `Chaos.IO/Memory/SpanReader.cs` for the property name and use it. Don't change `SpanReader` or `SpanWriter`.

- [ ] **Step 7: Bump the client version.** In `Chaos.DarkAges/Definitions/CONSTANTS.cs`, increase `CLIENT_VERSION` by one.

- [ ] **Step 8: Run the tests to verify they pass.** Run the Verify command. Expected: 4 tests passed.

```json:metadata
{"files": ["Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos-Server/Chaos.Networking/Entities/Client/ScryStartArgs.cs", "Chaos-Server/Chaos.Networking/Entities/Client/ScryStopArgs.cs", "Chaos-Server/Chaos.Networking/Converters/Client/ScryStartConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Client/ScryStopConverter.cs", "Chaos-Server/Chaos.Networking/Entities/Server/ScryFrameArgs.cs", "Chaos-Server/Chaos.Networking/Converters/Server/ScryFrameConverter.cs", "Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos-Server/Tests/Chaos.Tests/Scrying/ScryPacketConverterTests.cs"], "verifyCommand": "cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ScryPacketConverterTests/*\" --no-ansi", "acceptanceCriteria": ["opcodes 160/161/176", "ScryFrameArgs round-trips None/Full/Positions with looks", "aisling look length-prefixed via WriteData16", "CLIENT_VERSION bumped"], "modelTier": "standard"}
```

---

### Task 2: "Show me on the login screen" setting

**Goal:** Add a saved per-player option `ShowOnLoginScreen` that defaults to on, travels in `UserOptionsArgs`, and appears in the client Settings window.

**Files:**
- Modify: `Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs` (`UserOption`, after `ChatLanguage = 31`)
- Modify: `Chaos-Server/Chaos/Collections/UserOptions.cs` (property, `Toggle` case, `Set` case)
- Modify: `Chaos-Server/Chaos.Schemas/Aisling/UserOptionsSchema.cs`
- Modify: `Chaos-Server/Chaos/Services/MapperProfiles/UserOptionsMapperProfile.cs` (both directions, next to `DamageNumbersMyOutputOnly` at :24 and :63)
- Modify: `Chaos-Server/Chaos.Networking/Entities/Server/UserOptionsArgs.cs`
- Modify: `Chaos-Server/Chaos.Networking/Converters/Server/UserOptionsConverter.cs` (trailing optional section)
- Modify: `Chaos-Server/Chaos/Networking/ChaosWorldClient.cs:~914` (args builder)
- Modify: `Chaos.Client/ViewModel/SettingDefinition.cs` (`SettingKey` member and definition entry)
- Modify: `Chaos.Client/Screens/WorldScreen.ServerHandlers.cs:~519` (apply line)
- Test: `Chaos-Server/Tests/Chaos.Tests/Scrying/ShowOnLoginScreenOptionTests.cs`

**Acceptance Criteria:**
- [ ] `UserOption.ShowOnLoginScreen = 32`.
- [ ] A save JSON without the field loads as `true`.
- [ ] `UserOptionsArgs` bytes from before this change (no trailer) read back as `ShowOnLoginScreen == true`.
- [ ] When `ShowOnLoginScreen` is `true`, the converter writes exactly the same bytes as before this change.
- [ ] `ShowOnLoginScreen = false` round-trips with `ChatLanguage = 0` and with `ChatLanguage = 3`.
- [ ] The client shows "Show me on the login screen" in the Interaction section as a server option. Toggling it sends `SendSetUserOption(UserOption.ShowOnLoginScreen, value)` through the existing `HandleUserOptionToggled`.

**Verify:** `cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ShowOnLoginScreenOptionTests/*" --no-ansi` → all pass; then `dotnet build Chaos.Client.slnx` from the client root → 0 errors

**Steps:**

- [ ] **Step 1: Write the failing tests.** Reuse the `Bytes`, `Read` and `RoundTrip` helpers from `KartPacketConverterTests`. To build a full `UserOptionsArgs`, copy the object initializer of an existing `UserOptionsArgs` test if one exists (search `new UserOptionsArgs` under `Tests/`). Otherwise set every `required` member to `false`.

```csharp
namespace Chaos.Tests.Scrying;

public sealed class ShowOnLoginScreenOptionTests
{
    // Bytes / Read / RoundTrip helpers copied from KartPacketConverterTests

    private static UserOptionsArgs Args(bool show, byte language)
        => new()
        {
            // every required bool member = false (copy the list from UserOptionsArgs.cs)
            ChatLanguage = language,
            ShowOnLoginScreen = show
        };

    [Test]
    public void Default_on_writes_the_same_bytes_as_before()
    {
        var converter = new UserOptionsConverter();

        // on + no language: no trailer at all; off adds filter mode, filter flag, language and the new bool
        Bytes(converter, Args(true, 0)).Length.Should().Be(Bytes(converter, Args(false, 0)).Length - 4);
        // on + language: the old 3-byte trailer only; off adds just the new bool
        Bytes(converter, Args(true, 3)).Length.Should().Be(Bytes(converter, Args(false, 3)).Length - 1);
        Read(converter, Bytes(converter, Args(true, 0))).ShowOnLoginScreen.Should().BeTrue();
    }

    [Test]
    public void Off_round_trips_without_a_language()
        => RoundTrip(new UserOptionsConverter(), Args(false, 0)).ShowOnLoginScreen.Should().BeFalse();

    [Test]
    public void Off_round_trips_with_a_language()
    {
        var back = RoundTrip(new UserOptionsConverter(), Args(false, 3));

        back.ShowOnLoginScreen.Should().BeFalse();
        back.ChatLanguage.Should().Be(3);
    }

    [Test]
    public void Language_without_the_new_field_reads_as_on()
        => RoundTrip(new UserOptionsConverter(), Args(true, 3)).ShowOnLoginScreen.Should().BeTrue();

    [Test]
    public void Old_save_without_the_field_loads_as_on()
    {
        var schema = JsonSerializer.Deserialize<UserOptionsSchema>("{}")!;

        schema.ShowOnLoginScreen.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail.** Expected: a compile failure on `ShowOnLoginScreen`.

- [ ] **Step 3: Add the server pieces.**

`Chaos.DarkAges/Definitions/Enums.cs`, in `UserOption` after `ChatLanguage = 31,`:
```csharp
    ShowOnLoginScreen = 32,
```

`Chaos/Collections/UserOptions.cs`:
```csharp
// Toggle switch, next to the DamageNumbersMyOutputOnly case:
            case UserOption.ShowOnLoginScreen:
                ShowOnLoginScreen = !ShowOnLoginScreen;

                break;
// Set switch, next to the DamageNumbersMyOutputOnly case:
            case UserOption.ShowOnLoginScreen:
                ShowOnLoginScreen = value;

                break;
// property, next to DamageNumbersMyOutputOnly:
    public bool ShowOnLoginScreen { get; set; } = true;
```
If the `Set` switch's `value` isn't a `bool`, convert it the same way the `DamageNumbersMyOutputOnly` case does.

`Chaos.Schemas/Aisling/UserOptionsSchema.cs`:
```csharp
    /// <summary>Option 32. Whether this player can be shown in the login screen scrying view.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool ShowOnLoginScreen { get; set; } = true;
```

`UserOptionsMapperProfile.cs`, in both mapping directions next to `DamageNumbersMyOutputOnly = obj.DamageNumbersMyOutputOnly,`:
```csharp
            ShowOnLoginScreen = obj.ShowOnLoginScreen,
```

`UserOptionsArgs.cs`, after `ChatLanguage`:
```csharp
    /// <summary>Whether this player can be shown in the login screen scrying view. Optional trailing field; absent means true.</summary>
    public bool ShowOnLoginScreen { get; set; } = true;
```

`UserOptionsConverter.cs`, in deserialize after the `ChatLanguage = ...` line:
```csharp
            ChatLanguage = reader.EndOfSpan ? (byte)0 : reader.ReadByte(),
            ShowOnLoginScreen = reader.EndOfSpan || reader.ReadBoolean()
```
In serialize, replace the trailing block with the version below. It writes the same bytes as before whenever `ShowOnLoginScreen` is true.
```csharp
        var writeLanguage = (args.ChatLanguage != 0) || !args.ShowOnLoginScreen;

        if ((args.ChatFilterMode != 0) || args.HasConfiguredChatFilter || writeLanguage)
        {
            writer.WriteByte(args.ChatFilterMode);
            writer.WriteBoolean(args.HasConfiguredChatFilter);

            if (writeLanguage)
            {
                writer.WriteByte(args.ChatLanguage);

                if (!args.ShowOnLoginScreen)
                    writer.WriteBoolean(false);
            }
        }
```

`ChaosWorldClient.cs`, in the `UserOptionsArgs` builder next to `DamageNumbersMyOutputOnly = options.DamageNumbersMyOutputOnly,`:
```csharp
            ShowOnLoginScreen = options.ShowOnLoginScreen,
```

- [ ] **Step 4: Run the server tests to verify they pass.** Expected: 5 passed.

- [ ] **Step 5: Add the client setting.**

`Chaos.Client/ViewModel/SettingDefinition.cs`, in the `SettingKey` enum after `DamageNumbersMyOutputOnly,`:
```csharp
    ShowOnLoginScreen,
```
Next, in the definitions list, put this entry after the last entry whose section is `SettingSection.Interaction`:
```csharp
        new(
            SettingKey.ShowOnLoginScreen,
            "Show me on the login screen",
            SettingSection.Interaction,
            SettingCategory.ServerOption,
            UserOption.ShowOnLoginScreen,
            Span: SettingSpan.Full),
```

`Chaos.Client/Screens/WorldScreen.ServerHandlers.cs`, in `HandleUserOptions` after the `DamageNumbersMyOutputOnly` apply line:
```csharp
        userOptions.Apply(SettingKey.ShowOnLoginScreen, args.ShowOnLoginScreen);
```

- [ ] **Step 6: Build the client.** From the client root, run `dotnet build Chaos.Client.slnx`. Expected: 0 errors.

```json:metadata
{"files": ["Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs", "Chaos-Server/Chaos/Collections/UserOptions.cs", "Chaos-Server/Chaos.Schemas/Aisling/UserOptionsSchema.cs", "Chaos-Server/Chaos/Services/MapperProfiles/UserOptionsMapperProfile.cs", "Chaos-Server/Chaos.Networking/Entities/Server/UserOptionsArgs.cs", "Chaos-Server/Chaos.Networking/Converters/Server/UserOptionsConverter.cs", "Chaos-Server/Chaos/Networking/ChaosWorldClient.cs", "Chaos.Client/ViewModel/SettingDefinition.cs", "Chaos.Client/Screens/WorldScreen.ServerHandlers.cs", "Chaos-Server/Tests/Chaos.Tests/Scrying/ShowOnLoginScreenOptionTests.cs"], "verifyCommand": "cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ShowOnLoginScreenOptionTests/*\" --no-ansi", "acceptanceCriteria": ["UserOption.ShowOnLoginScreen = 32", "old save loads true", "old bytes read true", "default-on writes identical bytes", "off round-trips with and without language", "client setting in Interaction section"], "modelTier": "standard"}
```

---

### Task 3: Server pick and rotation logic

**Goal:** Pure classes that decide which player is eligible and which one to watch, with unit tests.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryCandidate.cs`
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryEligibility.cs`
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryRotation.cs`
- Test: `Chaos-Server/Tests/Chaos.Tests/Scrying/ScryRotationTests.cs`

**Acceptance Criteria:**
- [ ] Each of these makes a candidate ineligible: admin, GM-hidden, god mode, hidden, entrance-hidden, opted out, `LastWalk` null or older than 5 minutes, and map id 32000.
- [ ] A pick holds for 20 seconds, then moves to a different eligible player.
- [ ] A pick that stops being eligible is replaced on the next update.
- [ ] With only the current player eligible, the pick stays.
- [ ] With nobody eligible, `Current` is null.
- [ ] `Update` returns true only when `Current` changes.

**Verify:** `cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ScryRotationTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests.**

```csharp
using Chaos.Services.Scrying;
using FluentAssertions;

namespace Chaos.Tests.Scrying;

public sealed class ScryRotationTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private static ScryCandidate Player(uint id, DateTime? lastWalk = null, short mapId = 500)
        => new(id, false, false, false, false, false, true, lastWalk ?? Now.AddSeconds(-10), mapId);

    [Test]
    public void Active_opted_in_player_is_eligible()
        => ScryEligibility.IsEligible(Player(1), Now).Should().BeTrue();

    [Test]
    public void Ineligible_cases()
    {
        var p = Player(1);

        ScryEligibility.IsEligible(p with { IsAdmin = true }, Now).Should().BeFalse();
        ScryEligibility.IsEligible(p with { IsGmHidden = true }, Now).Should().BeFalse();
        ScryEligibility.IsEligible(p with { IsGodMode = true }, Now).Should().BeFalse();
        ScryEligibility.IsEligible(p with { IsHidden = true }, Now).Should().BeFalse();
        ScryEligibility.IsEligible(p with { IsEntranceHidden = true }, Now).Should().BeFalse();
        ScryEligibility.IsEligible(p with { OptedIn = false }, Now).Should().BeFalse();
        ScryEligibility.IsEligible(p with { LastWalk = null }, Now).Should().BeFalse();
        ScryEligibility.IsEligible(p with { LastWalk = Now.AddMinutes(-5).AddSeconds(-1) }, Now).Should().BeFalse();
        ScryEligibility.IsEligible(p with { MapId = 32000 }, Now).Should().BeFalse();
    }

    [Test]
    public void No_eligible_player_gives_no_pick()
    {
        var rotation = new ScryRotation(new Random(1));

        rotation.Update([Player(1) with { IsAdmin = true }], Now).Should().BeFalse();
        rotation.Current.Should().BeNull();
    }

    [Test]
    public void Pick_holds_for_twenty_seconds_then_changes()
    {
        var rotation = new ScryRotation(new Random(1));
        ScryCandidate[] players = [Player(1), Player(2), Player(3)];

        rotation.Update(players, Now).Should().BeTrue();
        var first = rotation.Current;

        rotation.Update(players, Now.AddSeconds(19)).Should().BeFalse();
        rotation.Current.Should().Be(first);

        rotation.Update(players, Now.AddSeconds(20)).Should().BeTrue();
        rotation.Current.Should().NotBe(first);
    }

    [Test]
    public void Ineligible_pick_is_replaced_at_once()
    {
        var rotation = new ScryRotation(new Random(1));
        rotation.Update([Player(1), Player(2)], Now);
        var first = rotation.Current!.Value;
        var other = first == 1 ? 2u : 1u;

        rotation.Update([Player(first) with { OptedIn = false }, Player(other)], Now.AddSeconds(1)).Should().BeTrue();
        rotation.Current.Should().Be(other);
    }

    [Test]
    public void Only_eligible_player_stays()
    {
        var rotation = new ScryRotation(new Random(1));
        rotation.Update([Player(5)], Now);

        rotation.Update([Player(5)], Now.AddSeconds(30)).Should().BeFalse();
        rotation.Current.Should().Be(5u);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail.** Expected: a compile failure because `ScryCandidate` does not exist.

- [ ] **Step 3: Implement the three classes.** Use the namespaces that `StatusExtensions`, `Aisling`, `VisibilityType` and `CONSTANTS` live in.

```csharp
// ScryCandidate.cs
namespace Chaos.Services.Scrying;

public readonly record struct ScryCandidate(
    uint Id,
    bool IsAdmin,
    bool IsGmHidden,
    bool IsGodMode,
    bool IsHidden,
    bool IsEntranceHidden,
    bool OptedIn,
    DateTime? LastWalk,
    short MapId)
{
    public static ScryCandidate From(Aisling aisling)
        => new(
            aisling.Id,
            aisling.IsAdmin,
            aisling.IsGmHidden(),
            aisling.IsGodModeEnabled(),
            aisling.IsHidden() || (aisling.Visibility != VisibilityType.Normal),
            aisling.HiddenForEntrance,
            aisling.Options.ShowOnLoginScreen,
            aisling.Trackers.LastWalk,
            aisling.MapInstance.Template.MapId);
}
```

```csharp
// ScryEligibility.cs
namespace Chaos.Services.Scrying;

public static class ScryEligibility
{
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(5);

    public static bool IsEligible(in ScryCandidate candidate, DateTime utcNow)
        => !candidate.IsAdmin
           && !candidate.IsGmHidden
           && !candidate.IsGodMode
           && !candidate.IsHidden
           && !candidate.IsEntranceHidden
           && candidate.OptedIn
           && candidate.LastWalk is { } walked
           && (utcNow - walked <= IdleLimit)
           && (candidate.MapId != CONSTANTS.STREAMED_MAP_ID);
}
```

```csharp
// ScryRotation.cs
namespace Chaos.Services.Scrying;

public sealed class ScryRotation(Random random)
{
    public static readonly TimeSpan Hold = TimeSpan.FromSeconds(20);

    private DateTime PickedAt;

    public uint? Current { get; private set; }

    /// <returns>true when <see cref="Current" /> changed</returns>
    public bool Update(IReadOnlyList<ScryCandidate> candidates, DateTime utcNow)
    {
        var eligible = new List<uint>();

        foreach (var candidate in candidates)
            if (ScryEligibility.IsEligible(candidate, utcNow))
                eligible.Add(candidate.Id);

        var currentStillEligible = Current is { } current && eligible.Contains(current);

        if (currentStillEligible && (utcNow - PickedAt < Hold))
            return false;

        var others = eligible.Where(id => id != Current).ToList();
        uint? next = others.Count > 0 ? others[random.Next(others.Count)] : currentStillEligible ? Current : null;

        PickedAt = utcNow;

        if (next == Current)
            return false;

        Current = next;

        return true;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass.** Expected: 6 passed.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/Scrying/ScryCandidate.cs", "Chaos-Server/Chaos/Services/Scrying/ScryEligibility.cs", "Chaos-Server/Chaos/Services/Scrying/ScryRotation.cs", "Chaos-Server/Tests/Chaos.Tests/Scrying/ScryRotationTests.cs"], "verifyCommand": "cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ScryRotationTests/*\" --no-ansi", "acceptanceCriteria": ["all ineligible cases rejected", "20 s hold then different player", "ineligible pick replaced at once", "sole eligible player stays", "no eligible -> null", "Update true only on change"], "modelTier": "mechanical"}
```

---

### Task 4: Server frame composer

**Goal:** A pure class that turns a captured view into Full or Positions frames, strips looks the watchers already have, lists removals, and limits "none" frames to one a second.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryView.cs`
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryFrameComposer.cs`
- Test: `Chaos-Server/Tests/Chaos.Tests/Scrying/ScryFrameComposerTests.cs`

**Acceptance Criteria:**
- [ ] The first frame is Full. Every 10th frame after it is Full. A forced frame is Full.
- [ ] Positions frames carry looks only for entities not sent before, and list ids that left.
- [ ] A map change forces Full (the caller passes `force: true`; covered by the composer's `force` test).
- [ ] `None()` returns a frame the first time, then null, then a frame again every 10th call. A later `Compose` after `None()` is Full.

**Verify:** `cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ScryFrameComposerTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests.**

```csharp
using Chaos.Networking.Entities.Server;
using Chaos.Services.Scrying;
using FluentAssertions;

namespace Chaos.Tests.Scrying;

public sealed class ScryFrameComposerTests
{
    private static ScryEntity Monster(uint id, byte x)
        => new()
        {
            Id = id, Kind = ScryEntityKind.Creature, X = x, Y = 5,
            Creature = new CreatureInfo { Id = id, X = x, Y = 5, Sprite = 1, Name = string.Empty }
        };

    private static ScryView View(params ScryEntity[] entities) => new(500, 60, 40, 1, 7, entities);

    [Test]
    public void First_frame_is_full_with_looks()
    {
        var frame = new ScryFrameComposer().Compose(View(Monster(1, 3)), force: false);

        frame.Kind.Should().Be(ScryFrameKind.Full);
        frame.Entities.Single().HasLook.Should().BeTrue();
    }

    [Test]
    public void Positions_frames_strip_known_looks_and_list_removals()
    {
        var composer = new ScryFrameComposer();
        composer.Compose(View(Monster(1, 3), Monster(2, 4)), force: false);

        var frame = composer.Compose(View(Monster(1, 4), Monster(3, 6)), force: false);

        frame.Kind.Should().Be(ScryFrameKind.Positions);
        frame.Entities.Single(e => e.Id == 1).HasLook.Should().BeFalse();
        frame.Entities.Single(e => e.Id == 1).X.Should().Be(4);
        frame.Entities.Single(e => e.Id == 3).HasLook.Should().BeTrue();
        frame.Removed.Should().Equal(2u);
    }

    [Test]
    public void Every_tenth_frame_is_full()
    {
        var composer = new ScryFrameComposer();
        var kinds = Enumerable.Range(0, 21).Select(_ => composer.Compose(View(Monster(1, 3)), force: false).Kind).ToList();

        kinds[0].Should().Be(ScryFrameKind.Full);
        kinds[10].Should().Be(ScryFrameKind.Full);
        kinds[20].Should().Be(ScryFrameKind.Full);
        kinds.Count(k => k == ScryFrameKind.Full).Should().Be(3);
    }

    [Test]
    public void Map_change_forces_full_frame()
    {
        var composer = new ScryFrameComposer();
        composer.Compose(View(Monster(1, 3)), force: false);

        composer.Compose(View(Monster(1, 3)), force: true).Kind.Should().Be(ScryFrameKind.Full);
    }

    [Test]
    public void None_frame_sent_once_then_once_per_second()
    {
        var composer = new ScryFrameComposer();
        var sent = Enumerable.Range(0, 21).Select(_ => composer.None() is not null).ToList();

        sent[0].Should().BeTrue();
        sent.Count(s => s).Should().Be(3);

        composer.Compose(View(Monster(1, 3)), force: false).Kind.Should().Be(ScryFrameKind.Full);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail.** Expected: a compile failure because `ScryView` does not exist.

- [ ] **Step 3: Implement.**

```csharp
// ScryView.cs
using Chaos.Networking.Entities.Server;

namespace Chaos.Services.Scrying;

/// <summary>What is around the watched player at one moment, with every look filled in.</summary>
public sealed record ScryView(short MapId, byte Width, byte Height, ushort CheckSum, uint TargetId, IReadOnlyList<ScryEntity> Entities);
```

```csharp
// ScryFrameComposer.cs
using Chaos.Networking.Entities.Server;

namespace Chaos.Services.Scrying;

public sealed class ScryFrameComposer
{
    public const int KEYFRAME_EVERY = 10;

    private HashSet<uint> Sent = [];
    private int TicksSinceFull = KEYFRAME_EVERY;
    private bool ShowingNone;

    public ScryFrameArgs Compose(ScryView view, bool force)
    {
        var full = force || ShowingNone || (++TicksSinceFull >= KEYFRAME_EVERY);

        if (full)
            TicksSinceFull = 0;

        ShowingNone = false;

        var frame = new ScryFrameArgs
        {
            Kind = full ? ScryFrameKind.Full : ScryFrameKind.Positions,
            MapId = view.MapId,
            Width = view.Width,
            Height = view.Height,
            CheckSum = view.CheckSum,
            TargetId = view.TargetId
        };

        var present = new HashSet<uint>();

        foreach (var entity in view.Entities)
        {
            present.Add(entity.Id);

            frame.Entities.Add(
                full || !Sent.Contains(entity.Id)
                    ? entity
                    : entity with
                    {
                        Aisling = null,
                        Creature = null,
                        GroundItem = null
                    });
        }

        if (!full)
            frame.Removed.AddRange(Sent.Where(id => !present.Contains(id)));

        Sent = present;

        return frame;
    }

    /// <returns>a None frame the first time and once a second after; otherwise null (send nothing)</returns>
    public ScryFrameArgs? None()
    {
        Sent.Clear();

        if (ShowingNone && (++TicksSinceFull < KEYFRAME_EVERY))
            return null;

        ShowingNone = true;
        TicksSinceFull = 0;

        return new ScryFrameArgs { Kind = ScryFrameKind.None };
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass.** Expected: 5 passed.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/Scrying/ScryView.cs", "Chaos-Server/Chaos/Services/Scrying/ScryFrameComposer.cs", "Chaos-Server/Tests/Chaos.Tests/Scrying/ScryFrameComposerTests.cs"], "verifyCommand": "cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ScryFrameComposerTests/*\" --no-ansi", "acceptanceCriteria": ["first/every 10th/forced frame Full", "Positions strip known looks and list removals", "None once then once per second; Compose after None is Full"], "modelTier": "mechanical"}
```

---

### Task 5: Server scrying service and login server wiring

**Goal:** A background service that runs every 100 ms, captures the view around the pick under the map lock, and sends frames to login-server watchers. Watchers join with `ScryStart`, leave with `ScryStop`, and are removed on disconnect and on login.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryWatchers.cs`
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryingService.cs`
- Modify: `Chaos-Server/Chaos/Networking/Abstractions/IChaosLoginClient.cs` (add `SendScryFrame`. This is the game server's own interface, not a `Chaos.*` library.)
- Modify: `Chaos-Server/Chaos/Networking/ChaosLoginClient.cs`
- Modify: `Chaos-Server/Chaos/Services/Servers/LoginServer.cs` (constructor, `IndexHandlers`, two handlers, `OnDisconnect`, the login redirect point at ~:309-331)
- Modify: `Chaos-Server/Chaos/Extensions/ServiceCollectionExtensions.cs` (registration next to `:532`)
- Modify: any test or helper that constructs `LoginServer` directly (search `new LoginServer(`) to pass the new argument
- Test: `Chaos-Server/Tests/Chaos.Tests/Scrying/ScryWatchersTests.cs`

**Acceptance Criteria:**
- [ ] `ScryWatchers` holds at most 200 clients. A 201st `TryAdd` returns false.
- [ ] `ScryStart` adds the client, and `ScryStop` removes it. Disconnect removes it, and so does the login redirect.
- [ ] A watcher whose `SendScryFrame` throws is removed, and the loop keeps running.
- [ ] With no watchers, the tick does no work.
- [ ] Capture happens while holding `map.Sync`, and covers aislings, creatures (excluding aislings), ground items and money within 8 tiles.
- [ ] An aisling is left out when it is GM-hidden, hidden, entrance-hidden or has `Visibility != Normal`. A creature is left out when `Visibility != Normal`.
- [ ] Every aisling look and creature look has `Name = string.Empty`.
- [ ] A new pick or map change forces a Full frame.

**Verify:** `cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ScryWatchersTests/*" --no-ansi` → all pass; `dotnet build` of the server solution → 0 errors

**Steps:**

- [ ] **Step 1: Write the failing tests.** Use the mocking library the test project already uses (search `Mock<` in `Tests/Chaos.Tests`) to create `IChaosLoginClient` doubles with distinct `Id` values.

```csharp
namespace Chaos.Tests.Scrying;

public sealed class ScryWatchersTests
{
    private static IChaosLoginClient Client(uint id, bool throws = false)
    {
        var mock = new Mock<IChaosLoginClient>();
        mock.SetupGet(c => c.Id).Returns(id);

        if (throws)
            mock.Setup(c => c.SendScryFrame(It.IsAny<ScryFrameArgs>())).Throws<ObjectDisposedException>();

        return mock.Object;
    }

    [Test]
    public void Cap_is_two_hundred()
    {
        var watchers = new ScryWatchers();

        for (uint i = 1; i <= 200; i++)
            watchers.TryAdd(Client(i)).Should().BeTrue();

        watchers.TryAdd(Client(201)).Should().BeFalse();
    }

    [Test]
    public void Remove_on_disconnect()
    {
        var watchers = new ScryWatchers();
        watchers.TryAdd(Client(1));

        watchers.Remove(1);

        watchers.Any.Should().BeFalse();
    }

    [Test]
    public void Send_failure_removes_watcher()
    {
        var watchers = new ScryWatchers();
        watchers.TryAdd(Client(1, throws: true));
        watchers.TryAdd(Client(2));

        watchers.SendToAll(new ScryFrameArgs { Kind = ScryFrameKind.None });

        watchers.Count.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail.** Expected: a compile failure because `ScryWatchers` does not exist.

- [ ] **Step 3: Implement `ScryWatchers`.**

```csharp
// ScryWatchers.cs
using System.Collections.Concurrent;
using Chaos.Networking.Entities.Server;

namespace Chaos.Services.Scrying;

/// <summary>Login-server clients that are watching the start-screen scrying view.</summary>
public sealed class ScryWatchers
{
    public const int CAP = 200;

    private readonly ConcurrentDictionary<uint, IChaosLoginClient> Clients = new();

    public bool Any => !Clients.IsEmpty;
    public int Count => Clients.Count;

    public bool TryAdd(IChaosLoginClient client) => (Clients.Count < CAP) && Clients.TryAdd(client.Id, client);

    public void Remove(uint clientId) => Clients.TryRemove(clientId, out _);

    public void SendToAll(ScryFrameArgs frame)
    {
        foreach (var client in Clients.Values)
            try
            {
                client.SendScryFrame(frame);
            } catch
            {
                Remove(client.Id);
            }
    }
}
```

- [ ] **Step 4: Add `SendScryFrame`.**

`IChaosLoginClient.cs`:
```csharp
    void SendScryFrame(ScryFrameArgs args);
```
`ChaosLoginClient.cs`:
```csharp
    public void SendScryFrame(ScryFrameArgs args) => Send(args);
```
Add the same no-op member to any other `IChaosLoginClient` implementations, such as test fakes (search `: IChaosLoginClient`).

- [ ] **Step 5: Wire up `LoginServer`.**
- Add `ScryWatchers scryWatchers` as the last constructor parameter. Store it in `private readonly ScryWatchers ScryWatchers;`.
- In `IndexHandlers`, after the existing lines:
```csharp
        ClientHandlers[(byte)ClientOpCode.ScryStart] = OnScryStart;
        ClientHandlers[(byte)ClientOpCode.ScryStop] = OnScryStop;
```
- Add the handlers:
```csharp
    public ValueTask OnScryStart(IChaosLoginClient client, in Packet packet)
    {
        ScryWatchers.TryAdd(client);

        return default;
    }

    public ValueTask OnScryStop(IChaosLoginClient client, in Packet packet)
    {
        ScryWatchers.Remove(client.Id);

        return default;
    }
```
- In `OnDisconnect`, after `ClientRegistry.TryRemove(client.Id, out _);`:
```csharp
        ScryWatchers.Remove(client.Id);
```
- In the `OnLogin` inner handler, directly before `localClient.SendRedirect(redirect);`:
```csharp
            ScryWatchers.Remove(localClient.Id);
```

- [ ] **Step 6: Implement `ScryingService`.** Use the namespaces of `Aisling`, `Creature`, `GroundItem`, `Money`, `VisibilityType`, `StatusExtensions`, `IClientRegistry`, `IChaosWorldClient` and `ITypeMapper` (`Chaos.TypeMapper.Abstractions`).

```csharp
// ScryingService.cs
using Chaos.Networking.Entities.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chaos.Services.Scrying;

/// <summary>Streams the area around one eligible player to start-screen watchers about 10 times a second.</summary>
public sealed class ScryingService(
    ScryWatchers watchers,
    IClientRegistry<IChaosWorldClient> worldClients,
    ITypeMapper mapper,
    ILogger<ScryingService> logger) : BackgroundService
{
    public const int RADIUS = 8;

    private readonly ScryFrameComposer Composer = new();
    private readonly ScryRotation Rotation = new(Random.Shared);
    private short LastMapId = -1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

        while (await timer.WaitForNextTickAsync(stoppingToken))
            try
            {
                await TickAsync();
            } catch (Exception e)
            {
                logger.LogError(e, "Scrying tick failed");
            }
    }

    private async Task TickAsync()
    {
        if (!watchers.Any)
            return;

        var aislings = worldClients.Select(client => client.Aisling)
                                   .Where(aisling => aisling?.MapInstance is not null)
                                   .Select(aisling => aisling!)
                                   .ToList();

        var changed = Rotation.Update(aislings.Select(ScryCandidate.From).ToList(), DateTime.UtcNow);
        var target = aislings.FirstOrDefault(aisling => aisling.Id == Rotation.Current);
        var view = target is null ? null : await CaptureAsync(target);

        if (view is null)
        {
            LastMapId = -1;

            if (Composer.None() is { } none)
                watchers.SendToAll(none);

            return;
        }

        var force = changed || (view.MapId != LastMapId);
        LastMapId = view.MapId;
        watchers.SendToAll(Composer.Compose(view, force));
    }

    private async Task<ScryView?> CaptureAsync(Aisling target)
    {
        var map = target.MapInstance;

        await using var sync = await map.Sync.WaitAsync();

        if (target.MapInstance != map)
            return null;

        var entities = new List<ScryEntity>();

        foreach (var aisling in map.GetEntitiesWithinRange<Aisling>(target, RADIUS))
        {
            if (aisling.IsGmHidden() || aisling.IsHidden() || aisling.HiddenForEntrance || (aisling.Visibility != VisibilityType.Normal))
                continue;

            var look = mapper.Map<DisplayAislingArgs>(aisling);
            look.Name = string.Empty;

            entities.Add(
                new ScryEntity
                {
                    Id = aisling.Id, Kind = ScryEntityKind.Aisling, X = (byte)aisling.X, Y = (byte)aisling.Y,
                    Direction = aisling.Direction, Aisling = look
                });
        }

        foreach (var creature in map.GetEntitiesWithinRange<Creature>(target, RADIUS))
        {
            if (creature is Aisling || (creature.Visibility != VisibilityType.Normal))
                continue;

            var info = mapper.Map<CreatureInfo>(creature) with { Name = string.Empty };

            entities.Add(
                new ScryEntity
                {
                    Id = creature.Id, Kind = ScryEntityKind.Creature, X = (byte)creature.X, Y = (byte)creature.Y,
                    Direction = creature.Direction, Creature = info
                });
        }

        foreach (var item in map.GetEntitiesWithinRange<GroundItem>(target, RADIUS))
            entities.Add(GroundEntity(item.Id, item.X, item.Y, mapper.Map<GroundItemInfo>(item)));

        foreach (var money in map.GetEntitiesWithinRange<Money>(target, RADIUS))
            entities.Add(GroundEntity(money.Id, money.X, money.Y, mapper.Map<GroundItemInfo>(money)));

        var template = map.Template;

        return new ScryView(template.MapId, template.Width, template.Height, template.CheckSum, target.Id, entities);
    }

    private static ScryEntity GroundEntity(uint id, int x, int y, GroundItemInfo info)
        => new() { Id = id, Kind = ScryEntityKind.GroundItem, X = (byte)x, Y = (byte)y, GroundItem = info };
}
```
If `GroundItem` or `Money` have different class names, use the types that `ChaosWorldClient.cs:1706` and `:1719` map to `GroundItemInfo`.

- [ ] **Step 7: Register.** In `ServiceCollectionExtensions.cs`, next to the `LoginServer` registration at ~:532:
```csharp
        services.AddSingleton<ScryWatchers>();
        services.AddHostedService<ScryingService>();
```

- [ ] **Step 8: Run the tests and build.** Expected: 3 passed and 0 build errors.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/Scrying/ScryWatchers.cs", "Chaos-Server/Chaos/Services/Scrying/ScryingService.cs", "Chaos-Server/Chaos/Networking/Abstractions/IChaosLoginClient.cs", "Chaos-Server/Chaos/Networking/ChaosLoginClient.cs", "Chaos-Server/Chaos/Services/Servers/LoginServer.cs", "Chaos-Server/Chaos/Extensions/ServiceCollectionExtensions.cs", "Chaos-Server/Tests/Chaos.Tests/Scrying/ScryWatchersTests.cs"], "verifyCommand": "cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ScryWatchersTests/*\" --no-ansi", "acceptanceCriteria": ["cap 200", "start/stop/disconnect/login remove", "send failure removes watcher", "no watchers no work", "capture under map.Sync within 8 tiles", "hidden filtering", "names blanked", "new pick or map change forces Full"], "modelTier": "standard"}
```

---

### Task 6: Client networking

**Goal:** The client receives `ScryFrame` packets as an event and can send `ScryStart` and `ScryStop` while in the Login state.

**Files:**
- Modify: `Chaos.Client.Networking/Definitions/Delegates.cs` (after `KartSnapshotReceivedHandler` at ~:575)
- Modify: `Chaos.Client.Networking/ConnectionManager.cs` (event near :652, send helper near :100, sends near :131, handler registration near :2035, handler near :2600)

**Acceptance Criteria:**
- [ ] `ConnectionManager.ScryFrameReceived` fires with the deserialized `ScryFrameArgs`.
- [ ] `SendScryStart()` and `SendScryStop()` send only when `State == ConnectionState.Login`.
- [ ] The handler is registered in the opcode-indexed table, not a switch.

**Verify:** `dotnet build Chaos.Client.slnx` → 0 errors

**Steps:**

- [ ] **Step 1: Add the delegate.**
```csharp
/// <summary>Fired when a login-screen scrying frame arrives.</summary>
public delegate void ScryFrameReceivedHandler(ScryFrameArgs args);
```

- [ ] **Step 2: Add the event, sends and handler to `ConnectionManager`.**
```csharp
    public event ScryFrameReceivedHandler? ScryFrameReceived;
```
```csharp
    private void SendIfLogin<T>(T args) where T : IPacketSerializable
    {
        if (State != ConnectionState.Login)
            return;

        Client.Send(args);
    }

    public void SendScryStart() => SendIfLogin(new ScryStartArgs());

    public void SendScryStop() => SendIfLogin(new ScryStopArgs());
```
In the handler table, next to the `KartSnapshot` line:
```csharp
        PacketHandlers[(byte)ServerOpCode.ScryFrame] = HandleScryFrame;
```
```csharp
    private void HandleScryFrame(ServerPacket pkt)
    {
        var args = Client.Deserialize<ScryFrameArgs>(in pkt);
        ScryFrameReceived?.Invoke(args);
    }
```

- [ ] **Step 3: Build.** Run `dotnet build Chaos.Client.slnx`. Expected: 0 errors.

```json:metadata
{"files": ["Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client.Networking/ConnectionManager.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx", "acceptanceCriteria": ["ScryFrameReceived event fires", "sends only in Login state", "handler in opcode table"], "modelTier": "mechanical"}
```

---

### Task 7: Client scrying scene

**Goal:** A client-side store that applies frames to `WorldEntity` objects. A one-tile step plays the walk animation, and anything further jumps. It clears on "none" and after 3 seconds with no frame.

**Files:**
- Modify: `Chaos.Client/Collections/WorldState.cs`. Extract the look-setting part of `AddOrUpdateAisling` into `public static void ApplyAislingLook(WorldEntity entity, DisplayAislingArgs args)`. Do the same for the per-entity part of `AddOrUpdateVisibleEntities`, as `public static void ApplyVisibleEntityLook(WorldEntity entity, VisibleEntityInfo info)`. The existing methods call the new ones, and their behavior is unchanged.
- Create: `Chaos.Client/Controls/LobbyLogin/Scrying/ScryScene.cs`
- Test: `Tests/Chaos.Client.Tests/ScrySceneTests.cs`

**Acceptance Criteria:**
- [ ] `WorldState.AddOrUpdateAisling` and `AddOrUpdateVisibleEntities` behave exactly as before. The existing client tests pass.
- [ ] Positions frames before the first Full frame are ignored.
- [ ] A Full frame replaces the set: entities missing from it are removed. Positions frames remove the ids listed in `Removed`.
- [ ] A one-tile move starts a walk (`AnimState == Walking`). A longer move jumps and leaves the entity not walking.
- [ ] A new map id clears all entities and raises `MapChanged`. A new target raises `TargetChanged`.
- [ ] A None frame clears the scene (`HasView == false`).
- [ ] With no frame for more than 3000 ms, `Update` clears the scene.
- [ ] `StepDirection` inverts `Direction.ToTileOffset()` for all four directions.

**Verify:** `dotnet build Chaos.Client.slnx; dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/ScrySceneTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Extract the look helpers.**
  - In `WorldState.AddOrUpdateAisling`, move everything from `entity.Type = ClientEntityType.Aisling;` through `AnimationSystem.CancelAllAnimations(entity);` into `public static void ApplyAislingLook(WorldEntity entity, DisplayAislingArgs args)`, and call it where that code was. `SortVersion++` and the dictionary lookup stay in `AddOrUpdateAisling`.
  - In `AddOrUpdateVisibleEntities`, move the per-object block into `public static void ApplyVisibleEntityLook(WorldEntity entity, VisibleEntityInfo info)`. That block sets `TileX`, `TileY`, `SpriteId` and `Appearance = null`, and contains the `CreatureInfo` and `GroundItemInfo` cases. Call it where that block was.

- [ ] **Step 2: Write the failing tests.**

```csharp
using Chaos.Client.Controls.LobbyLogin.Scrying;
using Chaos.Client.Models;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class ScrySceneTests
{
    private static ScryEntity Monster(uint id, byte x, byte y, bool look = true)
        => new()
        {
            Id = id, Kind = ScryEntityKind.Creature, X = x, Y = y, Direction = Direction.Down,
            Creature = look ? new CreatureInfo { Id = id, X = x, Y = y, Sprite = 1, Name = string.Empty } : null
        };

    private static ScryFrameArgs Frame(ScryFrameKind kind, short mapId = 500, uint target = 1, params ScryEntity[] entities)
        => new() { Kind = kind, MapId = mapId, Width = 60, Height = 40, CheckSum = 1, TargetId = target, Entities = [.. entities] };

    [Test]
    public void Positions_before_full_are_ignored()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Positions, entities: Monster(1, 3, 3)));

        scene.HasView.Should().BeFalse();
        scene.Entities.Should().BeEmpty();
    }

    [Test]
    public void One_tile_step_walks_and_longer_move_jumps()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Full, entities: [Monster(1, 3, 3), Monster(2, 10, 10)]));

        scene.Apply(Frame(ScryFrameKind.Positions, entities: [Monster(1, 4, 3, look: false), Monster(2, 14, 10, look: false)]));

        var walker = scene.Entities.Single(e => e.Id == 1);
        walker.TileX.Should().Be(4);
        walker.AnimState.Should().Be(EntityAnimState.Walking);

        var jumper = scene.Entities.Single(e => e.Id == 2);
        jumper.TileX.Should().Be(14);
        jumper.AnimState.Should().NotBe(EntityAnimState.Walking);
    }

    [Test]
    public void Full_frame_replaces_and_positions_remove()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Full, entities: [Monster(1, 3, 3), Monster(2, 5, 5)]));
        scene.Apply(Frame(ScryFrameKind.Full, entities: Monster(1, 3, 3)));
        scene.Entities.Select(e => e.Id).Should().Equal(1u);

        scene.Apply(Frame(ScryFrameKind.Positions, entities: Monster(3, 6, 6)) with { Removed = [1] });
        scene.Entities.Select(e => e.Id).Should().Equal(3u);
    }

    [Test]
    public void Map_change_clears_entities()
    {
        var scene = new ScryScene();
        var mapChanged = 0;
        scene.MapChanged += () => mapChanged++;
        scene.Apply(Frame(ScryFrameKind.Full, mapId: 500, entities: Monster(1, 3, 3)));

        scene.Apply(Frame(ScryFrameKind.Full, mapId: 501, entities: Monster(9, 3, 3)));

        mapChanged.Should().Be(2);
        scene.Entities.Select(e => e.Id).Should().Equal(9u);
    }

    [Test]
    public void None_frame_clears_view()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Full, entities: Monster(1, 3, 3)));

        scene.Apply(new ScryFrameArgs { Kind = ScryFrameKind.None });

        scene.HasView.Should().BeFalse();
        scene.Entities.Should().BeEmpty();
    }

    [Test]
    public void Stale_feed_clears_view()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Full, entities: Monster(1, 3, 3)));

        scene.Update(2999);
        scene.HasView.Should().BeTrue();

        scene.Update(2);
        scene.HasView.Should().BeFalse();
    }

    [Test]
    public void Step_direction_inverts_tile_offset()
    {
        foreach (var direction in new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left })
        {
            (var dx, var dy) = direction.ToTileOffset();
            ScryScene.StepDirection(dx, dy).Should().Be(direction);
        }
    }
}
```
Use the namespaces `WorldEntity`, `EntityAnimState`, `Direction` and `ToTileOffset` come from in the client.

- [ ] **Step 3: Run the tests to verify they fail.** Expected: a compile failure because `ScryScene` does not exist.

- [ ] **Step 4: Implement `ScryScene`.**

```csharp
// Chaos.Client/Controls/LobbyLogin/Scrying/ScryScene.cs
namespace Chaos.Client.Controls.LobbyLogin.Scrying;

/// <summary>The entities in the login scrying view, kept apart from WorldState.</summary>
public sealed class ScryScene
{
    public const double STALE_AFTER_MS = 3000;

    private readonly Dictionary<uint, WorldEntity> EntitiesById = [];
    private double MsSinceFrame;

    public event Action? MapChanged;
    public event Action? TargetChanged;

    public IReadOnlyCollection<WorldEntity> Entities => EntitiesById.Values;
    public bool HasView { get; private set; }
    public short MapId { get; private set; } = -1;
    public byte Width { get; private set; }
    public byte Height { get; private set; }
    public ushort CheckSum { get; private set; }
    public uint TargetId { get; private set; }

    public WorldEntity? Target => EntitiesById.GetValueOrDefault(TargetId);

    public void Apply(ScryFrameArgs frame)
    {
        MsSinceFrame = 0;

        if (frame.Kind == ScryFrameKind.None)
        {
            Clear();

            return;
        }

        if ((frame.Kind == ScryFrameKind.Positions) && !HasView)
            return;

        if ((frame.MapId != MapId) || (frame.Width != Width) || (frame.Height != Height) || (frame.CheckSum != CheckSum))
        {
            EntitiesById.Clear();
            MapId = frame.MapId;
            Width = frame.Width;
            Height = frame.Height;
            CheckSum = frame.CheckSum;
            MapChanged?.Invoke();
        }

        if (frame.TargetId != TargetId)
        {
            TargetId = frame.TargetId;
            TargetChanged?.Invoke();
        }

        HasView = true;

        var present = new HashSet<uint>();

        foreach (var entity in frame.Entities)
        {
            present.Add(entity.Id);
            ApplyEntity(entity);
        }

        if (frame.Kind == ScryFrameKind.Full)
        {
            foreach (var id in EntitiesById.Keys.Where(id => !present.Contains(id)).ToList())
                EntitiesById.Remove(id);
        } else
            foreach (var id in frame.Removed)
                EntitiesById.Remove(id);
    }

    public void Update(double elapsedMs)
    {
        MsSinceFrame += elapsedMs;

        if (HasView && (MsSinceFrame > STALE_AFTER_MS))
        {
            Clear();

            return;
        }

        foreach (var entity in EntitiesById.Values)
            AnimationSystem.Advance(entity, (float)elapsedMs);
    }

    public void Clear()
    {
        EntitiesById.Clear();
        HasView = false;
        MapId = -1;
        Width = 0;
        Height = 0;
        CheckSum = 0;
        TargetId = 0;
    }

    public static Direction StepDirection(int dx, int dy)
        => (dx, dy) switch
        {
            (0, -1) => Direction.Up,
            (1, 0) => Direction.Right,
            (0, 1) => Direction.Down,
            _ => Direction.Left
        };

    private void ApplyEntity(ScryEntity scry)
    {
        if (!EntitiesById.TryGetValue(scry.Id, out var entity))
        {
            if (!scry.HasLook)
                return;

            entity = new WorldEntity { Id = scry.Id };
            ApplyLook(entity, scry);
            EntitiesById[scry.Id] = entity;

            return;
        }

        var moved = (entity.TileX != scry.X) || (entity.TileY != scry.Y);

        if (scry.HasLook && !moved && (entity.AnimState != EntityAnimState.Walking))
        {
            ApplyLook(entity, scry);

            return;
        }

        MoveTo(entity, scry.X, scry.Y, scry.Direction);
    }

    private static void ApplyLook(WorldEntity entity, ScryEntity scry)
    {
        if (scry.Aisling is { } aisling)
            WorldState.ApplyAislingLook(entity, aisling);
        else if (scry.Creature is { } creature)
            WorldState.ApplyVisibleEntityLook(entity, creature);
        else if (scry.GroundItem is { } item)
            WorldState.ApplyVisibleEntityLook(entity, item);

        entity.TileX = scry.X;
        entity.TileY = scry.Y;
        entity.Direction = scry.Direction;
    }

    private static void MoveTo(WorldEntity entity, int x, int y, Direction facing)
    {
        var dx = x - entity.TileX;
        var dy = y - entity.TileY;

        if ((dx == 0) && (dy == 0))
        {
            entity.Direction = facing;

            return;
        }

        entity.TileX = x;
        entity.TileY = y;

        if (Math.Abs(dx) + Math.Abs(dy) == 1)
        {
            var step = StepDirection(dx, dy);
            entity.Direction = step;
            AnimationSystem.StartWalk(entity, step, entity.UsesCreatureWalkTiming);

            return;
        }

        entity.Direction = facing;
        AnimationSystem.CancelAllAnimations(entity);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass.** Expected: 7 passed. Then run the whole client test project to confirm the `WorldState` extraction changed nothing: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`. Expected: the same failures as before this task (record the baseline count first), no new ones.

```json:metadata
{"files": ["Chaos.Client/Collections/WorldState.cs", "Chaos.Client/Controls/LobbyLogin/Scrying/ScryScene.cs", "Tests/Chaos.Client.Tests/ScrySceneTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx; dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/ScrySceneTests/*\"", "acceptanceCriteria": ["WorldState behaviour unchanged", "Positions before Full ignored", "Full replaces, Positions remove", "1-tile walk, longer jump", "map change clears + events", "None clears", "stale after 3000 ms clears", "StepDirection inverts ToTileOffset"], "modelTier": "standard"}
```

---

### Task 8: Client oval view on the start screen

**Goal:** Draw the scene into a 296x266 off-screen picture, cut it to the oval, and fade it in over the logo animation. Watching starts and stops automatically.

**Files:**
- Create: `Chaos.Client/Controls/LobbyLogin/Scrying/ScryingViewControl.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.Map.cs` (`LoadMapFile` from `private static` to `internal static`)
- Modify: `Chaos.Client/Controls/LobbyLogin/LobbyLoginControl.cs` (an `AddScryView` method)
- Modify: `Chaos.Client/Screens/LobbyLoginScreen.cs` (create, subscribe, watch/unwatch in `Update`, render in `Draw`, clean up in `UnloadContent`)

**Acceptance Criteria:**
- [ ] The view sits exactly on the `AnimatedLogo` rect and draws after it. The frame art is unchanged.
- [ ] With no view, a missing or mismatched map file, or a stale feed, only the logo is visible (fade = 0).
- [ ] A new map loads with `WorldScreen.LoadMapFile`. A null result means `MapUsable = false` until the next `MapChanged`.
- [ ] The camera centers on the watched player's tile plus `VisualOffset`, so it follows walking smoothly.
- [ ] On `TargetChanged`, the fade restarts from 0 and reaches 1 over 0.5 s.
- [ ] The map draws background, then foreground and entities interleaved in depth order (`tileX + tileY`), as `WorldScreen.Draw.cs` does. No name tags and no chat are drawn.
- [ ] The oval cut uses a soft-edged ellipse mask with the same size as the logo.
- [ ] `ScryStart` is sent when the state is Login and the login, create-character and password dialogs are all hidden. `ScryStop` is sent when any of those stops being true. `UnloadContent` sends `ScryStop` if watching, and disposes the render targets, mask texture and `MapRenderer`.
- [ ] Rendering into the off-screen targets happens before `spriteBatch.Begin` in `LobbyLoginScreen.Draw`, and the previous render target is restored afterwards.

**Verify:** `dotnet build Chaos.Client.slnx` → 0 errors; the visual check happens in Task 9's playtest

**Steps:**

- [ ] **Step 1: Make the map loader reachable.** In `WorldScreen.Map.cs`, change `private static MapFile? LoadMapFile(` to `internal static MapFile? LoadMapFile(`.

- [ ] **Step 2: Add the host method to `LobbyLoginControl`.**
```csharp
    public void AddScryView(UIElement view) => AddChild(view);
```

- [ ] **Step 3: Create `ScryingViewControl`.** Mirror `WorldScreen`'s map-load sequence for `MapRenderer`: `PreloadMapTiles`, palette cycling and the animation tick. Draw ground items the way `WorldScreen.Draw.cs` does; find its ground-item draw call and copy it. The skeleton below fixes the structure, the mask and the compositing. The entity draw calls copy `DrawCreature` (`WorldScreen.Draw.cs:900-935`) and the aisling block (`:975-1072`), passing `EntityTintType` none, default colors, and the frame/emotion values that `AnimationSystem` returns.

```csharp
// Chaos.Client/Controls/LobbyLogin/Scrying/ScryingViewControl.cs
namespace Chaos.Client.Controls.LobbyLogin.Scrying;

/// <summary>Live view of one player's surroundings, cut to the start screen's logo oval.</summary>
public sealed class ScryingViewControl : UIElement
{
    private const float FADE_IN_MS = 500f;

    private static readonly BlendState MaskBlend = new()
    {
        ColorSourceBlend = Blend.DestinationAlpha,
        ColorDestinationBlend = Blend.Zero,
        AlphaSourceBlend = Blend.DestinationAlpha,
        AlphaDestinationBlend = Blend.Zero
    };

    private readonly ChaosGame Game;
    private Camera Camera = null!;
    private float Fade;
    private MapFile? MapFile;
    private MapRenderer? MapRenderer;
    private Texture2D? Mask;
    private RenderTarget2D? OvalTarget;
    private RenderTarget2D? SceneTarget;
    private int AnimationTick;

    public ScryScene Scene { get; } = new();

    private bool MapUsable => MapFile is not null;
    private bool Showing => Scene.HasView && MapUsable && Scene.Target is not null;

    public ScryingViewControl(ChaosGame game, UIElement logoSlot)
    {
        Game = game;
        Name = "ScryingView";
        X = logoSlot.X;
        Y = logoSlot.Y;
        Width = logoSlot.Width;
        Height = logoSlot.Height;

        Camera = new Camera(Width, Height) { Offset = new Vector2(-28, 24) };
        Scene.MapChanged += OnMapChanged;
        Scene.TargetChanged += () => Fade = 0f;
    }

    private void OnMapChanged()
    {
        MapFile = WorldScreen.LoadMapFile(Scene.MapId, Scene.Width, Scene.Height, Scene.CheckSum);
        Fade = 0f;

        if (MapFile is null)
            return;

        MapRenderer ??= new MapRenderer();
        // same preload / palette setup WorldScreen does after it gets a MapFile
    }

    public override void Update(GameTime gameTime)
    {
        var elapsed = gameTime.ElapsedGameTime.TotalMilliseconds;
        Scene.Update(elapsed);

        if (!Scene.HasView)
            MapFile = null;

        Fade = Showing ? Math.Min(1f, Fade + (float)(elapsed / FADE_IN_MS)) : 0f;
        // advance AnimationTick and MapRenderer palette cycling the same way WorldScreen.Update does
    }

    /// <summary>Call before the screen's SpriteBatch.Begin.</summary>
    public void RenderOffscreen(SpriteBatch spriteBatch)
    {
        if (!Showing || (Fade <= 0f))
            return;

        var device = spriteBatch.GraphicsDevice;
        EnsureTargets(device);

        var bindings = device.GetRenderTargets();
        var previous = bindings.Length > 0 ? bindings[0].RenderTarget as RenderTarget2D : null;

        device.SetRenderTarget(SceneTarget);
        device.Clear(Color.Transparent);
        DrawScene(spriteBatch);

        device.SetRenderTarget(OvalTarget);
        device.Clear(Color.Transparent);
        spriteBatch.Begin(blendState: BlendState.Opaque);
        spriteBatch.Draw(Mask, Vector2.Zero, Color.White);
        spriteBatch.End();
        spriteBatch.Begin(blendState: MaskBlend, samplerState: GlobalSettings.Sampler);
        spriteBatch.Draw(SceneTarget, Vector2.Zero, Color.White);
        spriteBatch.End();

        device.SetRenderTarget(previous);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || !Showing || (Fade <= 0f) || OvalTarget is null)
            return;

        spriteBatch.Draw(OvalTarget, new Rectangle(ScreenX, ScreenY, Width, Height), Color.White * Fade);
    }

    private void DrawScene(SpriteBatch spriteBatch)
    {
        var target = Scene.Target!;
        Camera.Position = Camera.TileToWorld(target.TileX, target.TileY, MapFile!.Height) + target.VisualOffset;

        spriteBatch.Begin(samplerState: GlobalSettings.Sampler);
        MapRenderer!.DrawBackground(spriteBatch, MapFile, Camera, AnimationTick);
        spriteBatch.End();

        // Foreground + entities by depth, as WorldScreen.Draw.cs:538-544 and :824-1072:
        // var bounds = Camera.GetVisibleTileBounds(MapFile.Width, MapFile.Height, MapRenderer.ForegroundExtraMargin);
        // var byTile = Scene.Entities.ToLookup(e => (e.TileX, e.TileY));
        // using var scope = BlendScope.Begin(spriteBatch, BlendState.AlphaBlend, GlobalSettings.Sampler, RasterizerState.CullNone, Matrix.Identity);
        // for depth = bounds.MinX + bounds.MinY .. bounds.MaxX + bounds.MaxY:
        //     for tileX in range, tileY = depth - tileX, inside bounds:
        //         MapRenderer.DrawForegroundTile(scope, MapFile, Camera, tileX, tileY, AnimationTick);
        //         foreach entity in byTile[(tileX, tileY)] (ground items first, then creatures and aislings):
        //             compute tileCenterX/Y like WorldScreen.Draw.cs:824-826 and draw with
        //             Game.ItemRenderer / Game.CreatureRenderer.Draw / Game.AislingRenderer.Draw(new AislingDrawParams(...))
        // Never draw names, name tags, chat bubbles, health bars or group boxes.
    }

    private void EnsureTargets(GraphicsDevice device)
    {
        SceneTarget ??= new RenderTarget2D(device, Width, Height);
        OvalTarget ??= new RenderTarget2D(device, Width, Height);
        Mask ??= BuildOvalMask(device, Width, Height);
    }

    internal static Color[] OvalMaskPixels(int width, int height)
    {
        var pixels = new Color[width * height];
        var rx = width / 2f;
        var ry = height / 2f;

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var nx = (x + 0.5f - rx) / rx;
                var ny = (y + 0.5f - ry) / ry;
                var distance = MathF.Sqrt(nx * nx + ny * ny);
                var edge = Math.Clamp((1f - distance) * rx / 2f, 0f, 1f);
                pixels[y * width + x] = Color.White * edge;
            }

        return pixels;
    }

    private static Texture2D BuildOvalMask(GraphicsDevice device, int width, int height)
    {
        var texture = new Texture2D(device, width, height);
        texture.SetData(OvalMaskPixels(width, height));

        return texture;
    }

    public override void Dispose()
    {
        SceneTarget?.Dispose();
        OvalTarget?.Dispose();
        Mask?.Dispose();
        MapRenderer?.Dispose();
        base.Dispose();
    }
}
```
The commented block in `DrawScene` is the required algorithm, not optional. Write it out in full by copying the matching `WorldScreen.Draw.cs` code. Check the logo frames: if the oval in `_nslogo.spf` frame 0 is inset from the 296x266 box, shrink `rx`/`ry` to match. To check, run `python` with `Unora/Tools/Accessories/acclib/daformats.py` (`Spf.from_bytes`) on `setoa.dat`'s `_nslogo.spf` and find where the black corners end on the middle row and middle column.

- [ ] **Step 4: Wire up `LobbyLoginScreen`.**
- Fields: `private ScryingViewControl? ScryView;` and `private bool Watching;`.
- In `LoadContent`, after `StartPanel` is created and before `Root.AddChild(StartPanel)`:
```csharp
        if (StartPanel.AnimatedLogo is { } logo)
        {
            ScryView = new ScryingViewControl(Game, logo);
            StartPanel.AddScryView(ScryView);
            Game.Connection.ScryFrameReceived += OnScryFrame;
        }
```
```csharp
    private void OnScryFrame(ScryFrameArgs args) => ScryView?.Scene.Apply(args);
```
- In `Update`, before `Root!.Update(gameTime);`:
```csharp
        var shouldWatch = ScryView is not null
                          && (Game.Connection.State == ConnectionState.Login)
                          && !LoginControl.Visible
                          && !CharCreateControl.Visible
                          && !PasswordChangeControl.Visible;

        if (shouldWatch != Watching)
        {
            Watching = shouldWatch;

            if (shouldWatch)
                Game.Connection.SendScryStart();
            else
            {
                Game.Connection.SendScryStop();
                ScryView!.Scene.Clear();
            }
        }
```
- In `Draw`, as the first line, before `spriteBatch.Begin(...)`:
```csharp
        ScryView?.RenderOffscreen(spriteBatch);
```
- In `UnloadContent`, next to the other unsubscribes:
```csharp
        Game.Connection.ScryFrameReceived -= OnScryFrame;

        if (Watching)
            Game.Connection.SendScryStop();

        Watching = false;
```
(`ScryView` is a child of `Root`, so `Root.Dispose()` disposes it.)

- [ ] **Step 5: Add the mask test.** Add a pure test in `Tests/Chaos.Client.Tests/ScrySceneTests.cs`:
```csharp
    [Test]
    public void Oval_mask_is_clear_in_corners_and_solid_in_center()
    {
        var pixels = ScryingViewControl.OvalMaskPixels(296, 266);

        pixels[0].A.Should().Be(0);
        pixels[133 * 296 + 148].A.Should().Be(255);
    }
```
If `internal` isn't visible to the test project, check for `InternalsVisibleTo` in `Chaos.Client.csproj`. If it's missing, make `OvalMaskPixels` `public`.

- [ ] **Step 6: Build and run the client tests.** Run `dotnet build Chaos.Client.slnx`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/ScrySceneTests/*"`. Expected: 0 errors and 8 passed.

```json:metadata
{"files": ["Chaos.Client/Controls/LobbyLogin/Scrying/ScryingViewControl.cs", "Chaos.Client/Screens/WorldScreen.Map.cs", "Chaos.Client/Controls/LobbyLogin/LobbyLoginControl.cs", "Chaos.Client/Screens/LobbyLoginScreen.cs", "Tests/Chaos.Client.Tests/ScrySceneTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx; dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/ScrySceneTests/*\"", "acceptanceCriteria": ["view on AnimatedLogo rect, drawn after it", "logo only when no view/map/stale", "LoadMapFile null -> not usable", "camera follows target incl VisualOffset", "0.5 s fade on new target", "depth-ordered draw, no names/chat", "soft oval mask", "watch/unwatch on Login + dialogs hidden; cleanup on unload", "offscreen render before Begin, previous RT restored"], "modelTier": "frontier"}
```

---

### Task 9: Full test suites and playtest

**Goal:** Prove the feature works end to end and nothing else broke.

**Files:**
- Test: no new files. Fix any regressions in the files touched by Tasks 1-8.

**Acceptance Criteria:**
- [ ] The full server test suite has no failures that weren't in the baseline (`Tests/Chaos.Tests`). Record the baseline from `main` before comparing.
- [ ] The full client test suite has no failures that weren't in the baseline.
- [ ] Playtest, with a local server, client A logged in and walking, and client B on the start screen:
  - B's oval shows A's surroundings live, and A walks smoothly.
  - Each of these on A makes B show the logo within 1 s: turning off "Show me on the login screen", going GM-hidden, logging out.
  - B shows the logo after A stands still for 5 minutes.
  - Clicking Continue on B stops the feed. That shows in the server log or debugger as watcher removal; or confirm with a breakpoint in `OnScryStop`.

**Verify:** both suites' summary lines plus playtest notes with screenshots of B's start screen

**Steps:**

- [ ] **Step 1: Run the full server suite.** `cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi`. Report only the failures and the summary line.
- [ ] **Step 2: Run the full client suite.** `dotnet build Chaos.Client.slnx; dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`. Report only the failures and the summary line.
- [ ] **Step 3: Playtest.** Use the `playtest` skill (`/playtest login scrying view`) with the scenario in the acceptance criteria. If the skill can't hold one client on the start screen, run the server and two Debug clients by hand using the "Game window automation" notes in memory, and take screenshots of client B.

```json:metadata
{"files": [], "verifyCommand": "cd Chaos-Server; dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["server suite no new failures", "client suite no new failures", "live view shows walking player", "opt-out / GM hide / logout -> logo within 1 s", "idle 5 min -> logo", "Continue stops feed"], "modelTier": "standard"}
```

---

### Task 10: Commit the full implementation

**Goal:** One commit in the server submodule, then one client commit that includes the submodule pointer.

**Files:**
- Commit only the files changed by Tasks 1-9. Several sessions share these checkouts, so stage by explicit path.

**Acceptance Criteria:**
- [ ] The server submodule has one commit with every server file from Tasks 1, 2, 3, 4 and 5.
- [ ] The client has one commit with every client file from Tasks 2, 6, 7 and 8, the new `Chaos-Server` submodule pointer, and this plan's `.md` and `.tasks.json`.
- [ ] Nothing is pushed.

**Verify:** `git -C Chaos-Server log -1 --stat` and `git log -1 --stat` list exactly the intended files

**Steps:**

- [ ] **Step 1: Commit the server.** `cd Chaos-Server`, run `git status --short`, then `git add` each changed or new file by path. Commit with the message `Add login scrying view feed` followed by a blank line and `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Write the message to a UTF-8 file without a BOM and use `git commit -F`.
- [ ] **Step 2: Commit the client.** From the client root, `git add` each changed or new client file by path, plus `Chaos-Server`, `docs/superpowers/plans/2026-10-31-login-scrying-view.md` and `docs/superpowers/plans/2026-10-31-login-scrying-view.md.tasks.json`. Use `-f` for the docs, because `superpowers` is gitignored. Commit with the message `Add login scrying view` and the same trailer.

```json:metadata
{"files": [], "verifyCommand": "git -C Chaos-Server log -1 --stat; git log -1 --stat", "acceptanceCriteria": ["one server commit with Task 1-5 server files", "one client commit with client files, submodule pointer, plan docs", "nothing pushed"], "modelTier": "mechanical"}
```
