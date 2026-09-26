# World List Drawer Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the world list drawer with the Korean client's grid drawer (wider titles, row lines through the titles, rotating class/continent filter buttons), fed by three new per-player values from the server.

**Architecture:** The server tags each map with a continent taken from its folder, and it adds a length-prefixed "extras" section after the last world list entry: Medenian class, continent and an ability flag. Old and new readers both stay safe. The client keeps the face definitions and the selection state in two UI-free classes, and the drawer control drives them. The new art is committed in Unora as indexed PNGs, and a Python build tool writes it into `setoa.dat`.

**Tech Stack:** C# / .NET 10 (Chaos-Server, Chaos.Client with MonoGame), TUnit + FluentAssertions, Python 3.14 + Pillow (Unora tools), DALib file formats.

**Spec:** `docs/superpowers/specs/2026-09-26-worldlist-drawer-design.md` (in Chaos.Client)

## Global Constraints

- **Repos and paths.**
  - Client: `C:\Users\Michael\Documents\GitHub\Chaos.Client`.
  - Server: the client's submodule `Chaos.Client\Chaos-Server`. It is the real server checkout.
  - Art and tools: `C:\Users\Michael\Documents\GitHub\Unora`.
  - The local game client folder is `C:\Users\Michael\Documents\Unora\Unora Files` (`acclib.paths.DEFAULT_DATA_DIR`).
- **Branches.** Work on `feat/worldlist-drawer` in all three repos. Other sessions share these checkouts, so stage by explicit path only. Never use `git add -A`, stash, or reset. Never commit `launchSettings.json` or a local `StagingDirectory` in `appsettings.json`.
- **Commits.** This plan uses the at-end commit strategy. Implementers do not commit. Task 9 makes the commits.
- **Building.** Stop any running `Chaos.exe` or client before building, because they lock `bin`. Never build the server and client solutions at the same time.
- **Tests.**
  - Test projects are TUnit executables. Run them with `dotnet run --project <proj> -- --no-ansi [--treenode-filter "/*/*/Class/*"]`, never `dotnet test`.
  - `Chaos.Tests` already has two failures on master: GiveAbility and OnItemDroppedOn stackable. They are not regressions.
- **Drawer geometry.** These values are binding and match the spec's tables.
  - The drawer is 498×303.
  - Row k's cell interior is y = 24 + 15k to 34 + 15k. The row pitch is 15.
  - Relative to the list's left edge (x = 15):
    - Title: x 0, width 168, right-aligned.
    - Name: x 178, width 94, right-aligned.
    - Status icon: x 278.
    - Emblem cell: x 293, left empty.
  - Text Y within a row is -1.
- **Frame numbers in `_nusersb.spf`.**
  - Button b's original face uses frames 2b (normal) and 2b + 1 (lit), for b = 0–8.
  - New face i (0–12) uses frames 18 + 2i and 19 + 2i, in this order: Temuair, Medenia, Ability, Berserker, Warlord, Archer, Assassin, Arcanist, Elemental, Bard, Plague Dr, Adept, Druid.
- **Extras record** (one per player, after the last entry): `length` (byte, 3), `advClass` (byte), `continent` (byte), `flags` (byte, bit 0 = HasAbility). Readers skip any bytes beyond the ones they know.
- **`Continent` enum:** None = 0, Temuair = 1, Medenia = 2.

**User decisions (already made):**
- Row style: Korean grid as shipped, 15 px rows, 17 visible.
- Move 30 px from the count boxes to the title column (titles 168 px, 28 letters).
- Row lines run through the title column.
- Inner icon cell is the status icon. The outer cell is for emblems, and stays empty until spec 2.
- Button labels are new carved art that matches the old UI exactly. No client-drawn text.
- Long names: "Berserker", "Elemental", "Plague Dr", packed 1 px tighter.
- Face order: Temuair face, then the two Medenian classes alphabetically.
- Temuair/Medenia means the continent of the map the player is on now.
- Data travels in a length-prefixed trailing section of the world list packet.
- Opening the drawer resets Country to "everyone" and lights only it. Other faces persist until logout.
- The user reviews and approves a 4× review sheet of the carved faces before a batch ships.

---

### Task 1: Map continent on the server

**Goal:** Every loaded map knows its continent (Temuair, Medenia or None) from its folder under `MapInstances`.

**Files:**
- Modify: `Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs` (add `Continent` after `AdvClass`)
- Create: `Chaos-Server/Chaos/Utilities/MapContinent.cs`
- Modify: `Chaos-Server/Chaos/Collections/MapInstance.cs` (property after `BaseInstanceId`)
- Modify: `Chaos-Server/Chaos/Services/Storage/ExpiringMapInstanceCache.cs` (`InnerLoadFromFile`)
- Test: `Chaos-Server/Tests/Chaos.Tests/MapContinentTests.cs`

**Acceptance Criteria:**
- [ ] `MapContinent.FromDirectory` returns Temuair or Medenia for the folder right after a `MapInstances` segment, ignoring case, with either slash. It returns None otherwise, including for null or empty input.
- [ ] `InnerLoadFromFile` sets `mapInstance.Continent`. Shards get their base map's continent, because they load from the base directory.
- [ ] `MapContinentTests` pass.

**Verify:** from `Chaos-Server`: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MapContinentTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing test** at `Tests/Chaos.Tests/MapContinentTests.cs`

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Utilities;
using FluentAssertions;
#endregion

namespace Chaos.Tests;

public sealed class MapContinentTests
{
    //formatter:off
    [Test]
    [Arguments(@"C:\Data\Configuration\MapInstances\Temuair\mileth", Continent.Temuair)]
    [Arguments(@"C:\Data\Configuration\MapInstances\Medenia\undine_fields\", Continent.Medenia)]
    [Arguments("/srv/data/Configuration/mapinstances/MEDENIA/arena", Continent.Medenia)]
    [Arguments(@"C:\Data\Configuration\MapInstances\Temuair\Mileth\mileth_inn", Continent.Temuair)]
    [Arguments(@"C:\Data\Configuration\MapInstances\Events\fair", Continent.None)]
    [Arguments(@"C:\Data\Configuration\MapInstances\mileth", Continent.None)]
    [Arguments(@"C:\Data\Temuair\mileth", Continent.None)]
    [Arguments(@"C:\Data\Configuration\MapInstances", Continent.None)]
    [Arguments("", Continent.None)]
    //formatter:on
    public void FromDirectory_reads_the_folder_right_after_MapInstances(string directory, Continent expected)
        => MapContinent.FromDirectory(directory)
                       .Should()
                       .Be(expected);

    [Test]
    public void FromDirectory_of_null_is_None()
        => MapContinent.FromDirectory(null)
                       .Should()
                       .Be(Continent.None);
}
```

- [ ] **Step 2: Run it and see it fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MapContinentTests/*"`
Expected: build error, because `Continent` and `MapContinent` don't exist.

- [ ] **Step 3: Add the enum** in `Chaos.DarkAges/Definitions/Enums.cs`, directly after the `AdvClass` enum:

```csharp
/// <summary>
///     The continent a map belongs to, taken from its folder under MapInstances. Used by ServerOpCode.WorldList
/// </summary>
public enum Continent : byte
{
    None = 0,
    Temuair = 1,
    Medenia = 2
}
```

- [ ] **Step 4: Add the helper** at `Chaos/Utilities/MapContinent.cs`

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Utilities;

/// <summary>
///     Works out which continent a map belongs to from the folder it is loaded from.
/// </summary>
public static class MapContinent
{
    private const string MAP_INSTANCES_FOLDER = "MapInstances";

    /// <summary>
    ///     Returns the continent named by the folder right after "MapInstances" in <paramref name="directory" />, or
    ///     <see cref="Continent.None" /> when there is no such folder or it names neither continent.
    /// </summary>
    public static Continent FromDirectory(string? directory)
    {
        if (string.IsNullOrEmpty(directory))
            return Continent.None;

        var segments = directory.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!segments[i].Equals(MAP_INSTANCES_FOLDER, StringComparison.OrdinalIgnoreCase))
                continue;

            var folder = segments[i + 1];

            if (folder.Equals(nameof(Continent.Temuair), StringComparison.OrdinalIgnoreCase))
                return Continent.Temuair;

            if (folder.Equals(nameof(Continent.Medenia), StringComparison.OrdinalIgnoreCase))
                return Continent.Medenia;

            return Continent.None;
        }

        return Continent.None;
    }
}
```

- [ ] **Step 5: Add the property** in `Chaos/Collections/MapInstance.cs`, directly after `BaseInstanceId`. The file already has `using Chaos.DarkAges.Definitions;`.

```csharp
    /// <summary>
    ///     The continent this map belongs to, taken from its folder under MapInstances when it is loaded
    /// </summary>
    public Continent Continent { get; set; }
```

- [ ] **Step 6: Set it on load.** In `ExpiringMapInstanceCache.InnerLoadFromFile`, directly after `mapInstance.BaseInstanceId = baseInstanceId;`, add the line below. Add `using Chaos.Utilities;` if it's missing.

```csharp
        mapInstance.Continent = MapContinent.FromDirectory(directory);
```

- [ ] **Step 7: Run the tests and see them pass**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MapContinentTests/*"`
Expected: 10 passed.

```json:metadata
{"files": ["Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs", "Chaos-Server/Chaos/Utilities/MapContinent.cs", "Chaos-Server/Chaos/Collections/MapInstance.cs", "Chaos-Server/Chaos/Services/Storage/ExpiringMapInstanceCache.cs", "Chaos-Server/Tests/Chaos.Tests/MapContinentTests.cs"], "verifyCommand": "cd Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MapContinentTests/*\"", "acceptanceCriteria": ["FromDirectory maps the folder after MapInstances to Temuair/Medenia/None", "InnerLoadFromFile sets Continent; shards inherit", "MapContinentTests pass"], "modelTier": "mechanical"}
```

---

### Task 2: World list extras section in the packet

**Goal:** `WorldListMemberInfo` carries `AdvClass`, `Continent` and `HasAbility`, and `WorldListConverter` writes and reads them as a length-prefixed trailing section that old and new readers both handle.

**Files:**
- Modify: `Chaos-Server/Chaos.Networking/Entities/Server/WorldListMemberInfo.cs`
- Modify: `Chaos-Server/Chaos.Networking/Converters/Server/WorldListConverter.cs`
- Test: `Chaos-Server/Tests/Chaos.Tests/Networking/WorldListConverterTests.cs`

**Acceptance Criteria:**
- [ ] A round trip keeps every field, including the three new ones.
- [ ] Bytes with no section (an old server) read with None, None and false, and every old field stays intact.
- [ ] A record longer than 3 bytes has its extra bytes skipped, and the next player's values are still correct.
- [ ] A section cut off part-way leaves the uncovered players at their defaults and doesn't throw.

**Verify:** from `Chaos-Server`: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/WorldListConverterTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** at `Tests/Chaos.Tests/Networking/WorldListConverterTests.cs`

```csharp
#region
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Networking;

public sealed class WorldListConverterTests
{
    private static readonly Encoding Enc = Encoding.GetEncoding(949);
    private static readonly WorldListConverter Converter = new();

    private static WorldListArgs Sample()
        => new()
        {
            WorldMemberCount = 2,
            CountryList =
            [
                new WorldListMemberInfo
                {
                    BaseClass = BaseClass.Warrior,
                    AdvClass = AdvClass.Berserker,
                    Continent = Continent.Medenia,
                    HasAbility = true,
                    Color = WorldListColor.White,
                    SocialStatus = SocialStatus.Awake,
                    Title = "Creant Slayer",
                    IsMaster = true,
                    Name = "Aldric"
                },
                new WorldListMemberInfo
                {
                    BaseClass = BaseClass.Monk,
                    AdvClass = AdvClass.None,
                    Continent = Continent.Temuair,
                    HasAbility = false,
                    Color = WorldListColor.Orange,
                    SocialStatus = SocialStatus.Grouped,
                    Title = string.Empty,
                    IsMaster = false,
                    Name = "Mirelle"
                }
            ]
        };

    private static byte[] Write(WorldListArgs args)
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        Converter.Serialize(ref writer, args);

        return writer.ToSpan()
                     .ToArray();
    }

    private static WorldListArgs Read(byte[] bytes)
    {
        var reader = new SpanReader(Enc, bytes);

        return Converter.Deserialize(ref reader);
    }

    /// <summary>What an old server sends: the entries without the trailing section (4 bytes per player).</summary>
    private static byte[] EntriesOnly(byte[] bytes, int players) => bytes[..^(4 * players)];

    [Test]
    public void Round_trip_keeps_every_value()
    {
        var read = Read(Write(Sample()));

        read.WorldMemberCount.Should().Be(2);
        read.CountryList.Should().BeEquivalentTo(Sample().CountryList, options => options.WithStrictOrdering());
    }

    [Test]
    public void Packet_without_the_section_reads_with_defaults()
    {
        var read = Read(EntriesOnly(Write(Sample()), 2)).CountryList.ToList();

        read.Select(m => m.Name).Should().Equal("Aldric", "Mirelle");
        read.Select(m => m.Title).Should().Equal("Creant Slayer", string.Empty);
        read.Should().AllSatisfy(m =>
        {
            m.AdvClass.Should().Be(AdvClass.None);
            m.Continent.Should().Be(Continent.None);
            m.HasAbility.Should().BeFalse();
        });
    }

    [Test]
    public void Longer_record_is_skipped_past_and_the_next_record_still_reads()
    {
        byte[] section =
        [
            5, (byte)AdvClass.Bard, (byte)Continent.Medenia, 1, 0xAA, 0xBB,   // a future reader's 5-byte record
            3, (byte)AdvClass.Druid, (byte)Continent.Temuair, 0
        ];

        var read = Read([.. EntriesOnly(Write(Sample()), 2), .. section]).CountryList.ToList();

        read[0].AdvClass.Should().Be(AdvClass.Bard);
        read[0].Continent.Should().Be(Continent.Medenia);
        read[0].HasAbility.Should().BeTrue();
        read[1].AdvClass.Should().Be(AdvClass.Druid);
        read[1].Continent.Should().Be(Continent.Temuair);
        read[1].HasAbility.Should().BeFalse();
    }

    [Test]
    public void Truncated_section_leaves_the_rest_at_defaults()
    {
        byte[] section =
        [
            3, (byte)AdvClass.Archer, (byte)Continent.Temuair, 1,
            3, (byte)AdvClass.Adept                                        // cut off after one of three bytes
        ];

        var read = Read([.. EntriesOnly(Write(Sample()), 2), .. section]).CountryList.ToList();

        read[0].AdvClass.Should().Be(AdvClass.Archer);
        read[0].HasAbility.Should().BeTrue();
        read[1].AdvClass.Should().Be(AdvClass.None);
        read[1].Continent.Should().Be(Continent.None);
        read[1].HasAbility.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/WorldListConverterTests/*"`
Expected: build error, because `WorldListMemberInfo` has no `AdvClass`, `Continent` or `HasAbility`.

- [ ] **Step 3: Add the properties** to `WorldListMemberInfo`, before `BaseClass` (the members are alphabetical):

```csharp
    /// <summary>
    ///     The character's medenia class, or None
    /// </summary>
    public AdvClass AdvClass { get; set; }
```

and before `IsGuilded`:

```csharp
    /// <summary>
    ///     The continent of the map the character is on
    /// </summary>
    public Continent Continent { get; set; }

    /// <summary>
    ///     Whether the character has any ability levels
    /// </summary>
    public bool HasAbility { get; set; }
```

- [ ] **Step 4: Write and read the section** in `WorldListConverter`. Add these members at the top of the class:

```csharp
    //after the last entry, each player gets one record in list order: a length byte, then the values below.
    //readers take the values they know and skip the rest, so later fields (the emblem number) append safely,
    //and a packet with no records at all (an older server) leaves every player at the defaults.
    private const byte EXTRAS_LENGTH = 3;
    private const byte HAS_ABILITY_FLAG = 0b_0000_0001;

    private static void ReadExtras(ref SpanReader reader, List<WorldListMemberInfo> members)
    {
        foreach (var member in members)
        {
            if (reader.EndOfSpan)
                return;

            var length = reader.ReadByte();

            if (reader.Remaining < length)
                return;

            var end = reader.Position + length;

            if (length >= 1)
                member.AdvClass = (AdvClass)reader.ReadByte();

            if (length >= 2)
                member.Continent = (Continent)reader.ReadByte();

            if (length >= 3)
                member.HasAbility = (reader.ReadByte() & HAS_ABILITY_FLAG) != 0;

            reader.Position = end;
        }
    }
```

In `Deserialize`, after the `for` loop that fills `countryList` and before the `return`, add:

```csharp
        ReadExtras(ref reader, countryList);
```

In `Serialize`, after the `foreach` that writes the entries, add:

```csharp
        foreach (var user in args.CountryList)
        {
            writer.WriteByte(EXTRAS_LENGTH);
            writer.WriteByte((byte)user.AdvClass);
            writer.WriteByte((byte)user.Continent);
            writer.WriteByte(user.HasAbility ? HAS_ABILITY_FLAG : (byte)0);
        }
```

- [ ] **Step 5: Run the tests and see them pass**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/WorldListConverterTests/*"`
Expected: 4 passed.

- [ ] **Step 6: Run the networking tests** to catch anything else that uses the world list.

Run: `dotnet run --project Tests/Chaos.Networking.Tests/Chaos.Networking.Tests.csproj -- --no-ansi`
Expected: all pass (`SendWorldList_ShouldSendArgs` included).

```json:metadata
{"files": ["Chaos-Server/Chaos.Networking/Entities/Server/WorldListMemberInfo.cs", "Chaos-Server/Chaos.Networking/Converters/Server/WorldListConverter.cs", "Chaos-Server/Tests/Chaos.Tests/Networking/WorldListConverterTests.cs"], "verifyCommand": "cd Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/WorldListConverterTests/*\"", "acceptanceCriteria": ["round trip keeps all fields", "no section reads as None/None/false", "longer records skipped", "truncated section leaves defaults without throwing"], "modelTier": "mechanical"}
```

---

### Task 3: Fill the new values from each player

**Goal:** The world list mapper fills `AdvClass`, `Continent` and `HasAbility` from the player.

**Files:**
- Modify: `Chaos-Server/Chaos/Services/MapperProfiles/AislingMapperProfile.cs` (the `IMapperProfile<Aisling, WorldListMemberInfo>.Map` method)
- Test: `Chaos-Server/Tests/Chaos.Tests/AislingMapperProfileTests.cs`

**Acceptance Criteria:**
- [ ] A Berserker with one ability level on a Medenia map maps to Berserker, Medenia and true.
- [ ] A default test player maps to None, None and false.
- [ ] Nothing else in `SendWorldList` changes.

**Verify:** from `Chaos-Server`: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/AislingMapperProfileTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests.** Add these to `AislingMapperProfileTests`. The file already has the usings it needs, including `Chaos.Networking.Entities.Server` and `Moq`.

```csharp
    [Test]
    public void WorldList_map_carries_medenia_class_continent_and_ability()
    {
        var map = MockMapInstance.Create(setup: m => m.Continent = Continent.Medenia);

        var aisling = MockAisling.Create(
            map,
            setup: a =>
            {
                a.UserStatSheet.SetAdvClass(AdvClass.Berserker);
                a.UserStatSheet.AddAbilityLevel();
            });

        IMapperProfile<Aisling, WorldListMemberInfo> profile = CreateProfile(new Mock<ITypeMapper>().Object);
        var info = profile.Map(aisling);

        info.AdvClass.Should().Be(AdvClass.Berserker);
        info.Continent.Should().Be(Continent.Medenia);
        info.HasAbility.Should().BeTrue();
    }

    [Test]
    public void WorldList_map_defaults_to_no_medenia_class_and_no_ability()
    {
        IMapperProfile<Aisling, WorldListMemberInfo> profile = CreateProfile(new Mock<ITypeMapper>().Object);
        var info = profile.Map(MockAisling.Create());

        info.AdvClass.Should().Be(AdvClass.None);
        info.Continent.Should().Be(Continent.None);
        info.HasAbility.Should().BeFalse();
    }
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/AislingMapperProfileTests/*"`
Expected: the first new test fails, with `AdvClass` expected Berserker but found None.

- [ ] **Step 3: Fill the values.** Replace the body of `WorldListMemberInfo IMapperProfile<Aisling, WorldListMemberInfo>.Map(Aisling obj)` with:

```csharp
    WorldListMemberInfo IMapperProfile<Aisling, WorldListMemberInfo>.Map(Aisling obj)
        => new()
        {
            AdvClass = obj.UserStatSheet.AdvClass,
            BaseClass = obj.UserStatSheet.BaseClass,
            Color = WorldListColor.White,
            Continent = obj.MapInstance.Continent,
            HasAbility = obj.UserStatSheet.AbilityLevel > 0,
            IsMaster = obj.UserStatSheet.Master,
            Name = obj.Name,
            SocialStatus = obj.Options.SocialStatus,
            Title = obj.ActiveTitle
        };
```

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/AislingMapperProfileTests/*"`
Expected: all pass.

- [ ] **Step 5: Run the whole `Chaos.Tests` project**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi`
Expected: everything passes except the two failures already on master (GiveAbility, OnItemDroppedOn stackable).

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/MapperProfiles/AislingMapperProfile.cs", "Chaos-Server/Tests/Chaos.Tests/AislingMapperProfileTests.cs"], "verifyCommand": "cd Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/AislingMapperProfileTests/*\"", "acceptanceCriteria": ["Berserker with ability on Medenia maps to Berserker/Medenia/true", "default player maps to None/None/false", "SendWorldList otherwise unchanged"], "modelTier": "mechanical"}
```

---

### Task 4: Client face definitions and filter state

**Goal:** UI-free client classes define every button's faces and filters, and track which button is lit and which face each button shows. World list entries carry the three new values.

**Files:**
- Modify: `Chaos.Client/Models/WorldListEntry.cs`
- Modify: `Chaos.Client/Collections/WorldState.cs` (the `OnWorldList` mapping)
- Create: `Chaos.Client/Controls/World/Popups/WorldList/WorldListFaces.cs`
- Create: `Chaos.Client/Controls/World/Popups/WorldList/WorldListFilterState.cs`
- Test: `Tests/Chaos.Client.Tests/WorldListFacesTests.cs`

**Acceptance Criteria:**
- [ ] There are 9 buttons whose faces and frames match the Global Constraints: 3, 2, 3, 3, 3, 3, 3, 1 and 1 faces.
- [ ] A Temuair class face matches on base class, so Warrior includes Berserker and Warlord. A Medenian face matches `AdvClass` exactly.
- [ ] Clicking an unlit button selects it without changing its face. Clicking the lit button advances its face and wraps around.
- [ ] `ResetForOpen` lights button 0 on its first face and keeps the other buttons' faces.
- [ ] `WorldState` copies `AdvClass`, `Continent` and `HasAbility`.

**Verify:** from `Chaos.Client`: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/WorldListFacesTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** at `Tests/Chaos.Client.Tests/WorldListFacesTests.cs`

```csharp
using Chaos.Client.Controls.World.Popups.WorldList;
using Chaos.Client.Models;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class WorldListFacesTests
{
    private static WorldListEntry Player(
        BaseClass baseClass = BaseClass.Peasant,
        AdvClass advClass = AdvClass.None,
        Continent continent = Continent.None,
        bool master = false,
        bool ability = false,
        bool guilded = false)
        => new("Name", null, baseClass, master, guilded, WorldListColor.White, SocialStatus.Awake, advClass, continent, ability);

    [Test]
    public async Task Buttons_have_the_agreed_faces_and_frames()
    {
        WorldListFaces.Buttons.Select(b => b.Count).Should().Equal(3, 2, 3, 3, 3, 3, 3, 1, 1);

        WorldListFaces.Buttons.SelectMany(b => b).Select(f => f.Label).Should().Equal(
            "Country", "Temuair", "Medenia", "Master", "Ability", "Warrior", "Berserker", "Warlord",
            "Rogue", "Archer", "Assassin", "Wizard", "Arcanist", "Elementalist", "Priest", "Bard", "Plague Doctor",
            "Monk", "Adept", "Druid", "Peasant", "Guild");

        //original faces keep frames 2b / 2b + 1
        for (var b = 0; b < WorldListFaces.BUTTON_COUNT; b++)
        {
            WorldListFaces.Buttons[b][0].NormalFrame.Should().Be(2 * b);
            WorldListFaces.Buttons[b][0].LitFrame.Should().Be(2 * b + 1);
        }

        //new faces are 18 + 2i / 19 + 2i in the agreed order
        var newFaces = WorldListFaces.Buttons.SelectMany(b => b.Skip(1)).ToList();
        newFaces.Select(f => f.Label).Should().Equal(
            "Temuair", "Medenia", "Ability", "Berserker", "Warlord", "Archer", "Assassin", "Arcanist", "Elementalist",
            "Bard", "Plague Doctor", "Adept", "Druid");

        for (var i = 0; i < newFaces.Count; i++)
        {
            newFaces[i].NormalFrame.Should().Be(18 + 2 * i);
            newFaces[i].LitFrame.Should().Be(19 + 2 * i);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Warrior_face_includes_its_medenian_classes_but_Berserker_face_does_not_include_Warlords()
    {
        var berserker = Player(BaseClass.Warrior, AdvClass.Berserker);
        var warlord = Player(BaseClass.Warrior, AdvClass.Warlord);
        var warrior = Player(BaseClass.Warrior);
        var monk = Player(BaseClass.Monk, AdvClass.Druid);

        var faces = WorldListFaces.Buttons[2];

        faces[0].Matches(berserker).Should().BeTrue();
        faces[0].Matches(warlord).Should().BeTrue();
        faces[0].Matches(warrior).Should().BeTrue();
        faces[0].Matches(monk).Should().BeFalse();
        faces[1].Matches(berserker).Should().BeTrue();
        faces[1].Matches(warlord).Should().BeFalse();
        faces[2].Matches(warlord).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Country_Master_Peasant_and_Guild_faces_filter_as_expected()
    {
        var onTemuair = Player(continent: Continent.Temuair);
        var onMedenia = Player(continent: Continent.Medenia, ability: true);
        var elsewhere = Player(master: true, guilded: true);

        WorldListFaces.Buttons[0][0].Matches(elsewhere).Should().BeTrue();
        WorldListFaces.Buttons[0][1].Matches(onTemuair).Should().BeTrue();
        WorldListFaces.Buttons[0][1].Matches(onMedenia).Should().BeFalse();
        WorldListFaces.Buttons[0][2].Matches(onMedenia).Should().BeTrue();
        WorldListFaces.Buttons[0][2].Matches(elsewhere).Should().BeFalse();
        WorldListFaces.Buttons[1][0].Matches(elsewhere).Should().BeTrue();
        WorldListFaces.Buttons[1][1].Matches(onMedenia).Should().BeTrue();
        WorldListFaces.Buttons[1][1].Matches(elsewhere).Should().BeFalse();
        WorldListFaces.Buttons[7][0].Matches(onTemuair).Should().BeTrue();
        WorldListFaces.Buttons[8][0].Matches(elsewhere).Should().BeTrue();
        WorldListFaces.Buttons[8][0].Matches(onTemuair).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Count_counts_players_matching_a_face()
    {
        List<WorldListEntry> players =
        [
            Player(BaseClass.Priest, AdvClass.Bard),
            Player(BaseClass.Priest, AdvClass.PlagueDoctor),
            Player(BaseClass.Priest)
        ];

        WorldListFaces.Count(players, WorldListFaces.Buttons[5][0]).Should().Be(3);
        WorldListFaces.Count(players, WorldListFaces.Buttons[5][1]).Should().Be(1);
        WorldListFaces.Count(players, WorldListFaces.Buttons[5][2]).Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clicking_the_lit_button_turns_it_and_wraps_while_clicking_another_only_selects_it()
    {
        var state = new WorldListFilterState();

        state.ActiveButton.Should().Be(0);
        state.Click(2);                               //select Warrior
        state.ActiveButton.Should().Be(2);
        state.FaceOf(2).Should().Be(0);
        state.Click(2);                               //Berserker
        state.Click(2);                               //Warlord
        state.ActiveFace.Label.Should().Be("Warlord");
        state.Click(2);                               //wraps to Warrior
        state.FaceOf(2).Should().Be(0);
        state.Click(2);                               //Berserker again
        state.Click(4);                               //select Wizard: Warrior button keeps Berserker
        state.ActiveButton.Should().Be(4);
        state.FaceOf(2).Should().Be(1);
        state.FaceOf(4).Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ResetForOpen_lights_Country_on_everyone_and_keeps_other_faces()
    {
        var state = new WorldListFilterState();
        state.Click(0);                               //Country is lit: turns to Temuair
        state.Click(3);                               //select Rogue
        state.Click(3);                               //Archer

        state.ResetForOpen();

        state.ActiveButton.Should().Be(0);
        state.FaceOf(0).Should().Be(0);
        state.ActiveFace.Label.Should().Be("Country");
        state.FaceOf(3).Should().Be(1);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/WorldListFacesTests/*"`
Expected: build error, because `WorldListFaces`, `WorldListFilterState` and the new `WorldListEntry` parameters don't exist.

- [ ] **Step 3: Extend `WorldListEntry`** (`Chaos.Client/Models/WorldListEntry.cs`). The defaults keep other callers compiling.

```csharp
public sealed record WorldListEntry(
    string Name,
    string? Title,
    BaseClass BaseClass,
    bool IsMaster,
    bool IsGuilded,
    WorldListColor Color,
    SocialStatus SocialStatus,
    AdvClass AdvClass = AdvClass.None,
    Continent Continent = Continent.None,
    bool HasAbility = false);
```

- [ ] **Step 4: Copy the values in `WorldState`.** In the `connection.OnWorldList` handler, replace the `new WorldListEntry(...)` call with:

```csharp
                              .Select(m => new WorldListEntry(
                                  m.Name,
                                  m.Title,
                                  m.BaseClass,
                                  m.IsMaster,
                                  m.IsGuilded,
                                  m.Color,
                                  m.SocialStatus,
                                  m.AdvClass,
                                  m.Continent,
                                  m.HasAbility))
```

- [ ] **Step 5: Create `WorldListFaces.cs`**

```csharp
#region
using Chaos.Client.Models;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.WorldList;

/// <summary>
///     One face of a world list filter button: its carved label frames in _nusersb.spf and the players it shows.
/// </summary>
public sealed record WorldListFace(string Label, int NormalFrame, int LitFrame, Func<WorldListEntry, bool> Matches);

/// <summary>
///     The nine world list filter buttons and the faces each one turns through. A button's first face is the art that
///     shipped with the drawer (frames 2b and 2b + 1). The carved faces added later sit at 18 + 2i and 19 + 2i, in the
///     order they appear below.
/// </summary>
public static class WorldListFaces
{
    public const int BUTTON_COUNT = 9;

    public static IReadOnlyList<IReadOnlyList<WorldListFace>> Buttons { get; } =
    [
        [Original(0, "Country", _ => true), Added(0, "Temuair", e => e.Continent == Continent.Temuair),
            Added(1, "Medenia", e => e.Continent == Continent.Medenia)],
        [Original(1, "Master", e => e.IsMaster), Added(2, "Ability", e => e.HasAbility)],
        [Class(2, "Warrior", BaseClass.Warrior), Medenian(3, "Berserker", AdvClass.Berserker), Medenian(4, "Warlord", AdvClass.Warlord)],
        [Class(3, "Rogue", BaseClass.Rogue), Medenian(5, "Archer", AdvClass.Archer), Medenian(6, "Assassin", AdvClass.Assassin)],
        [Class(4, "Wizard", BaseClass.Wizard), Medenian(7, "Arcanist", AdvClass.Arcanist),
            Medenian(8, "Elementalist", AdvClass.Elementalist)],
        [Class(5, "Priest", BaseClass.Priest), Medenian(9, "Bard", AdvClass.Bard),
            Medenian(10, "Plague Doctor", AdvClass.PlagueDoctor)],
        [Class(6, "Monk", BaseClass.Monk), Medenian(11, "Adept", AdvClass.Adept), Medenian(12, "Druid", AdvClass.Druid)],
        [Class(7, "Peasant", BaseClass.Peasant)],
        [Original(8, "Guild", e => e.IsGuilded)]
    ];

    private static WorldListFace Added(int index, string label, Func<WorldListEntry, bool> matches)
        => new(label, 18 + 2 * index, 19 + 2 * index, matches);

    //a temuair class face shows the whole class, medenian classes included
    private static WorldListFace Class(int button, string label, BaseClass baseClass)
        => Original(button, label, e => e.BaseClass == baseClass);

    public static int Count(IEnumerable<WorldListEntry> entries, WorldListFace face) => entries.Count(face.Matches);

    private static WorldListFace Medenian(int index, string label, AdvClass advClass)
        => Added(index, label, e => e.AdvClass == advClass);

    private static WorldListFace Original(int button, string label, Func<WorldListEntry, bool> matches)
        => new(label, 2 * button, 2 * button + 1, matches);
}
```

- [ ] **Step 6: Create `WorldListFilterState.cs`**

```csharp
namespace Chaos.Client.Controls.World.Popups.WorldList;

/// <summary>
///     Which world list button is lit and which face each button shows. Clicking the lit button turns it to its next
///     face. Clicking any other button lights it without turning it.
/// </summary>
public sealed class WorldListFilterState
{
    private readonly int[] FaceIndexes = new int[WorldListFaces.BUTTON_COUNT];

    public int ActiveButton { get; private set; }

    public WorldListFace ActiveFace => CurrentFace(ActiveButton);

    public void Click(int button)
    {
        if (button == ActiveButton)
            FaceIndexes[button] = (FaceIndexes[button] + 1) % WorldListFaces.Buttons[button].Count;
        else
            ActiveButton = button;
    }

    public WorldListFace CurrentFace(int button) => WorldListFaces.Buttons[button][FaceIndexes[button]];

    public int FaceOf(int button) => FaceIndexes[button];

    /// <summary>
    ///     Opening the drawer always shows everyone: Country lights on its first face. Other buttons keep their faces.
    /// </summary>
    public void ResetForOpen()
    {
        ActiveButton = 0;
        FaceIndexes[0] = 0;
    }
}
```

- [ ] **Step 7: Run the tests and see them pass**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/WorldListFacesTests/*"`
Expected: 6 passed. This first needs the Task 2 server change in the `Chaos-Server` submodule checkout, because `WorldListMemberInfo.AdvClass` must exist for `WorldState` to build.

```json:metadata
{"files": ["Chaos.Client/Models/WorldListEntry.cs", "Chaos.Client/Collections/WorldState.cs", "Chaos.Client/Controls/World/Popups/WorldList/WorldListFaces.cs", "Chaos.Client/Controls/World/Popups/WorldList/WorldListFilterState.cs", "Tests/Chaos.Client.Tests/WorldListFacesTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/WorldListFacesTests/*\"", "acceptanceCriteria": ["9 buttons with agreed faces and frames", "Temuair class faces include medenian classes; medenian faces exact", "click selects or turns with wrap", "ResetForOpen lights Country on everyone and keeps other faces", "WorldState copies the three values"], "modelTier": "mechanical"}
```

---

### Task 5: Drawer rows, rotating buttons and counts

**Goal:** `WorldListControl` and `WorldListEntryControl` use the new layout. The buttons turn through their faces with their carved frames, and each count box shows its button's current face.

**Files:**
- Modify: `Chaos.Client/Controls/World/Popups/WorldList/WorldListEntryControl.cs`
- Modify: `Chaos.Client/Controls/World/Popups/WorldList/WorldListControl.cs`
- Modify: `controlFileList.txt` (the `_nusers.txt` entry, around lines 371–379)
- Modify: `CLAUDE.md` (the `WorldList/` mention in the Popups list)

**Acceptance Criteria:**
- [ ] Rows are 15 px, with the columns from the Global Constraints. The emblem cell is left empty.
- [ ] `ROW_HEIGHT` is 15. Tab count and textures come from `WorldListFaces` and `WorldListFilterState`, with no `TAB_COUNT` or `ActiveTab` left.
- [ ] Clicking an unlit button filters by its face. Clicking the lit button turns it, swaps both textures and filters. The count labels show each button's current face.
- [ ] `Show` calls `ResetForOpen` and refreshes all nine buttons, so only button 0 is lit. This fixes the stale lit button.
- [ ] The client solution builds with no new warnings.

**Verify:** from `Chaos.Client`: `dotnet build Chaos.Client.slnx` → 0 errors. `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass.

**Steps:**

- [ ] **Step 1: Row layout.** In `WorldListEntryControl`, replace the constants and the constructor with:

```csharp
    //columns match the grid cells painted in _nusers.spf; x is relative to the list's left edge (x = 15 in the drawer)
    private const int ROW_HEIGHT = 15;
    private const int TEXT_HEIGHT = 12;
    private const int TEXT_Y = -1;
    private const int TITLE_WIDTH = 168;
    private const int NAME_X = 178;
    private const int NAME_WIDTH = 94;
    private const int STATUS_X = 278;
    private const int ICON_SIZE = 11;

    private readonly UIImage Icon;
    private readonly UILabel NameLabel;
    private readonly UILabel TitleLabel;

    public WorldListEntryControl(int rowWidth)
    {
        Width = rowWidth;
        Height = ROW_HEIGHT;

        TitleLabel = new UILabel
        {
            Name = "Title",
            X = 0,
            Y = TEXT_Y,
            Width = TITLE_WIDTH,
            Height = TEXT_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            PaddingLeft = 0
        };

        AddChild(TitleLabel);

        NameLabel = new UILabel
        {
            Name = "Name",
            X = NAME_X,
            Y = TEXT_Y,
            Width = NAME_WIDTH,
            Height = TEXT_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            PaddingLeft = 0
        };

        AddChild(NameLabel);

        //the emblem cell (x = 293) stays empty until the emblem system fills it
        Icon = new UIImage
        {
            Name = "StatusIcon",
            X = STATUS_X,
            Y = 0,
            Width = ICON_SIZE,
            Height = ICON_SIZE
        };

        AddChild(Icon);
    }
```

- [ ] **Step 2: Drawer fields.** In `WorldListControl`:
  - Change `ROW_HEIGHT` to `15`.
  - Delete `TAB_COUNT` and the `ActiveTab` field.
  - Size the arrays with `WorldListFaces.BUTTON_COUNT`.
  - Add the state field.

```csharp
    private const int ROW_HEIGHT = 15;
    private const int STATUS_ICON_COUNT = 8;
    private const string BUTTON_FRAMES = "_nusersb.spf";

    private readonly WorldListFilterState Filter = new();
    private readonly UIButton[] TabButtons = new UIButton[WorldListFaces.BUTTON_COUNT];
    private readonly UILabel[] TabCountLabels = new UILabel[WorldListFaces.BUTTON_COUNT];
```

- [ ] **Step 3: Build the buttons from their faces.** In the constructor's tab loop, replace the loop body and the line after it (`TabButtons[0].IsSelected = true;`) with:

```csharp
        for (var i = 0; i < WorldListFaces.BUTTON_COUNT; i++)
        {
            var button = i;

            TabButtons[i] = new UIButton
            {
                Name = $"Tab{i}",
                X = countryBtnRect.X,
                Y = countryBtnRect.Y + i * tabStride,
                Width = countryBtnRect.Width,
                Height = countryBtnRect.Height
            };

            TabButtons[i].Clicked += () => OnTabClicked(button);
            AddChild(TabButtons[i]);

            TabCountLabels[i] = new UILabel
            {
                Name = $"TabCount{i}",
                X = countryNumRect.X,
                Y = countryNumRect.Y + i * labelStride,
                Width = countryNumRect.Width,
                Height = countryNumRect.Height,
                HorizontalAlignment = HorizontalAlignment.Right,
                PaddingLeft = 0
            };

            TabCountLabels[i].ForegroundColor = Color.White;
            AddChild(TabCountLabels[i]);
        }

        RefreshTabs();
```

  Delete the now-unused `var cache = UiRenderer.Instance!;` line above the loop, unless something else in the constructor still uses it.

- [ ] **Step 4: Replace `ApplyFilter`, `SelectTab` and `UpdateCountLabels`.** Delete `SelectTab` and add `OnTabClicked` and `RefreshTabs`. The whole-list fast path stays for the Country face.

```csharp
    private void ApplyFilter()
    {
        var face = Filter.ActiveFace;

        if ((Filter.ActiveButton == 0) && (Filter.FaceOf(0) == 0))
            FilteredEntries = AllEntries;
        else
        {
            FilterBuffer.Clear();

            foreach (var entry in AllEntries)
                if (face.Matches(entry))
                    FilterBuffer.Add(entry);

            FilteredEntries = FilterBuffer;
        }

        RowList.SetItems(FilteredEntries);
    }

    private void OnTabClicked(int button)
    {
        Filter.Click(button);
        RefreshTabs();
        ApplyFilter();
        UpdateCountLabels();
    }

    //every button shows its current face's carved frames; only the active button is lit
    private void RefreshTabs()
    {
        var cache = UiRenderer.Instance!;

        for (var i = 0; i < WorldListFaces.BUTTON_COUNT; i++)
        {
            var face = Filter.CurrentFace(i);
            TabButtons[i].NormalTexture = cache.GetSpfTexture(BUTTON_FRAMES, face.NormalFrame);
            TabButtons[i].SelectedTexture = cache.GetSpfTexture(BUTTON_FRAMES, face.LitFrame);
            TabButtons[i].IsSelected = i == Filter.ActiveButton;
        }
    }

    private void UpdateCountLabels()
    {
        TotalNumLabel.Text = $"{TotalOnline}";

        for (var i = 0; i < WorldListFaces.BUTTON_COUNT; i++)
            TabCountLabels[i].Text = $"{WorldListFaces.Count(AllEntries, Filter.CurrentFace(i))}";
    }
```

- [ ] **Step 5: Reset on open.** In `Show`, replace `ActiveTab = 0;` and `TabButtons[0].IsSelected = true;` with:

```csharp
        Filter.ResetForOpen();
        RefreshTabs();
```

  The method keeps its existing order after that: `ApplyFilter(); UpdateCountLabels(); AutoScrollToSelf();` and the slide-in.

- [ ] **Step 6: Tidy.** Remove any `using` that is now unused. `BaseClass` may no longer be referenced in this file, but `Chaos.DarkAges.Definitions` is still needed for `WorldListColor`. Update the class summary: "Shows a scrollable user list and 9 filter buttons that turn through class and continent faces".

- [ ] **Step 7: Docs.**
  - In `controlFileList.txt`, rewrite the `_nusers.txt` entry:

```
_nusers.txt
  Class:    WorldListControl : PrefabPanel
  File:     Controls/World/Popups/WorldList/WorldListControl.cs
  Purpose:  World list drawer (Korean grid art, 498x303) — 15 px rows, 9 filter buttons that turn through faces
  Images referenced by controls:
    _nusers.spf          — Anchor background (row lines run through the title column)
    _nbtn.spf            — Close button frames
  Also manually loads:
    _nusersb.spf         — 44 button frames: 0-17 the original nine (normal, lit), 18-43 the added faces
                           (Temuair, Medenia, Ability, Berserker, Warlord, Archer, Assassin, Arcanist, Elemental,
                           Bard, Plague Dr, Adept, Druid) — see WorldListFaces
```

  - In `CLAUDE.md`, change `` `WorldList/` (WorldListControl/WorldListEntryControl) `` to `` `WorldList/` (WorldListControl/WorldListEntryControl; WorldListFaces/WorldListFilterState — the filter buttons' faces and which is lit) ``.

- [ ] **Step 8: Build and test**

Run: `dotnet build Chaos.Client.slnx` → Build succeeded, 0 errors.
Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/WorldList/WorldListEntryControl.cs", "Chaos.Client/Controls/World/Popups/WorldList/WorldListControl.cs", "controlFileList.txt", "CLAUDE.md"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["15 px rows with agreed columns, emblem cell empty", "buttons driven by WorldListFaces/WorldListFilterState", "click selects or turns, textures swap, per-face counts", "Show lights only Country", "client builds and tests pass"], "modelTier": "standard"}
```

---

### Task 6: Drawer art, layout file and setoa build tool

**Goal:** The new drawer background and layout are committed in Unora as sources. A build tool writes `_nusers.spf`, `_nusersb.spf` and `_nusers.txt` into a `setoa.dat` batch folder and checks the result.

**Files:**
- Modify: `Unora/Tools/Accessories/acclib/daformats.py` (add `SpfFrame` and `Spf`)
- Create: `Unora/Tools/WorldList/README.md`
- Create: `Unora/Tools/WorldList/spfpng.py` (SPF frame ↔ indexed PNG)
- Create: `Unora/Tools/WorldList/make_drawer_art.py` (derives `art/nusers.png` from the Korean source)
- Create: `Unora/Tools/WorldList/extract_buttons.py` (writes `art/nusersb/00.png`–`17.png` from our `_nusersb.spf`)
- Create: `Unora/Tools/WorldList/build.py`
- Create: `Unora/Tools/WorldList/_nusers.txt`
- Create: `Unora/Tools/WorldList/source/kr_nusers.spf` (copy of `Desktop\Emblem Assets\Emblem UI\_nusers.spf`), `source/nusersb.spf` (copy of our `_nusersb.spf` from the client folder's `setoa.dat`)
- Create: `Unora/Tools/WorldList/art/nusers.png`, `art/nusersb/00.png`–`17.png` (generated, committed)
- Test: `Unora/Tools/WorldList/test_worldlist.py`

**Acceptance Criteria:**
- [ ] `Spf.from_bytes(raw).to_bytes() == raw` for our real `_nusersb.spf` and the Korean `_nusers.spf`.
- [ ] `art/nusers.png` is indexed and 498×303, with columns 60–89 duplicated, 440–469 removed, and the 18 row lines copied into x 16–187.
- [ ] `_nusers.txt` has exactly the rects in the spec's layout table, with CRLF line endings and tabs, like the original.
- [ ] `build.py` writes `<Desktop>\WorldList batch <stamp>\setoa.dat` and `manifest.txt`. It re-reads what it wrote and fails if any frame differs from its PNG. It builds `_nusersb.spf` from however many consecutive PNGs exist (18 now, 44 after Task 7), and the manifest says how many.

**Verify:** from `Unora`: `python -m unittest Tools/WorldList/test_worldlist.py` → OK. `python Tools/WorldList/build.py --no-apply` → prints the batch folder and "frames: 18".

**Steps:**

- [ ] **Step 1: Write the failing tests** at `Tools/WorldList/test_worldlist.py`

```python
"""World list drawer tools. Run from the repo root:

    python -m unittest Tools/WorldList/test_worldlist.py

The real-file tests read the local client folder (read-only) and skip when it is not present.
"""
from __future__ import annotations

import struct
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "Accessories"))  # the shared acclib package
sys.path.insert(0, str(HERE))

from acclib.daformats import DatArchive, Spf, SpfFrame  # noqa: E402
from acclib.paths import DEFAULT_DATA_DIR  # noqa: E402
from make_drawer_art import extend_row_lines, widen_titles  # noqa: E402


def tiny_spf(frames: int = 2) -> Spf:
    return Spf(unknown1=0, unknown2=1, primary=bytes(range(256)) * 2, secondary=bytes(512),
               frames=[SpfFrame(0, 0, 3, 2, 0, 0, 0, 3, 6, bytes([i, 1, 2, 3, 4, 5])) for i in range(frames)])


class SpfTests(unittest.TestCase):
    def test_round_trip_of_a_built_spf(self):
        spf = tiny_spf()
        again = Spf.from_bytes(spf.to_bytes())
        self.assertEqual(again, spf)

    def test_start_addresses_and_total_are_written(self):
        raw = tiny_spf().to_bytes()
        count = struct.unpack_from("<I", raw, 1036)[0]
        starts = [struct.unpack_from("<I", raw, 1040 + 36 * i + 16)[0] for i in range(count)]
        total = struct.unpack_from("<I", raw, 1040 + 36 * count)[0]
        self.assertEqual(starts, [0, 6])
        self.assertEqual(total, 12)

    def test_rejects_colorized_spf(self):
        with self.assertRaises(ValueError):
            Spf.from_bytes(struct.pack("<III", 0, 0, 2) + bytes(2000))

    @unittest.skipUnless((DEFAULT_DATA_DIR / "setoa.dat").exists(), "no local client folder")
    def test_real_nusersb_round_trips_byte_for_byte(self):
        raw = DatArchive.load(DEFAULT_DATA_DIR / "setoa.dat")["_nusersb.spf"]
        self.assertEqual(Spf.from_bytes(raw).to_bytes(), raw)

    def test_korean_drawer_source_round_trips_byte_for_byte(self):
        raw = (HERE / "source" / "kr_nusers.spf").read_bytes()
        spf = Spf.from_bytes(raw)
        self.assertEqual((spf.frames[0].width, spf.frames[0].height), (498, 303))
        self.assertEqual(spf.to_bytes(), raw)


class DrawerArtTests(unittest.TestCase):
    def test_widen_titles_duplicates_60_89_and_drops_440_469(self):
        rows = [bytearray(x % 256 for x in range(498))]
        out = widen_titles(rows)
        self.assertEqual(len(out[0]), 498)
        self.assertEqual(list(out[0][:90]), list(range(90)))
        self.assertEqual(list(out[0][90:120]), list(range(60, 90)))
        self.assertEqual(out[0][469], 439)
        self.assertEqual(out[0][470], 470)

    def test_extend_row_lines_copies_name_column_lines_into_the_title_column(self):
        rows = [bytearray([7]) * 498 for _ in range(303)]
        for y in range(20, 24):                      # first row line, name column only
            for x in range(193, 289):
                rows[y][x] = 100 + (x - 193) % 50
        out = extend_row_lines(rows)
        self.assertEqual(out[21][16], 100)           # x 16 takes name column x 193
        self.assertEqual(out[21][16 + 96], 100)      # tiled every 96 px
        self.assertEqual(out[21][187], rows[21][193 + (187 - 16) % 96])
        self.assertEqual(out[21][15], 7)             # the left border is untouched
        self.assertEqual(out[24][100], 7)            # rows between lines are untouched


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Copy the two sources**

```bash
mkdir -p Tools/WorldList/source Tools/WorldList/art/nusersb
cp "/c/Users/Michael/Desktop/Emblem Assets/Emblem UI/_nusers.spf" Tools/WorldList/source/kr_nusers.spf
python -c "import sys; sys.path.insert(0,'Tools/Accessories'); from acclib.daformats import DatArchive; from acclib.paths import DEFAULT_DATA_DIR; open('Tools/WorldList/source/nusersb.spf','wb').write(DatArchive.load(DEFAULT_DATA_DIR/'setoa.dat')['_nusersb.spf'])"
md5sum Tools/WorldList/source/kr_nusers.spf   # expect 8ee3d3e2794580ad255d7b36003082b9
md5sum Tools/WorldList/source/nusersb.spf     # expect 6a7c5301ec96a32a7a7b2beb0b4313e9
```

- [ ] **Step 3: Run the tests and see them fail**

Run: `python -m unittest Tools/WorldList/test_worldlist.py`
Expected: ImportError, because `Spf` isn't in `acclib.daformats` yet.

- [ ] **Step 4: Add SPF support** at the end of `acclib/daformats.py` (before any trailing helpers is fine):

```python
# --- .spf palettized images ------------------------------------------------------------------
# Mirrors dalib/DALib/Drawing/SpfFile.cs (ReadPalettized / SavePalettized): a 12-byte header (unknown1, unknown2,
# format), 256 RGB565 colors, 256 RGB555 colors, a frame count, 36-byte frame headers, a total byte count, then
# one byte per pixel per frame. Only format 0 (palettized) is supported.

SPF_FRAME_HEADER = struct.Struct("<HHHHhhIIIII")  # left top right bottom centerX centerY flags start byteWidth byteCount imageByteCount


@dataclass
class SpfFrame:
    left: int
    top: int
    right: int
    bottom: int
    center_x: int
    center_y: int
    flags: int
    byte_width: int
    image_byte_count: int
    data: bytes

    @property
    def width(self) -> int:
        return self.right - self.left

    @property
    def height(self) -> int:
        return self.bottom - self.top


@dataclass
class Spf:
    unknown1: int
    unknown2: int
    primary: bytes    # 512 bytes, RGB565
    secondary: bytes  # 512 bytes, RGB555
    frames: list[SpfFrame]

    @classmethod
    def from_bytes(cls, d: bytes) -> "Spf":
        unknown1, unknown2, fmt = struct.unpack_from("<III", d, 0)
        if fmt != 0:
            raise ValueError(f"only palettized SPF files are supported (format {fmt})")
        primary, secondary = d[12:524], d[524:1036]
        count = struct.unpack_from("<I", d, 1036)[0]
        heads = [SPF_FRAME_HEADER.unpack_from(d, 1040 + i * SPF_FRAME_HEADER.size) for i in range(count)]
        data_start = 1040 + count * SPF_FRAME_HEADER.size + 4
        frames = [SpfFrame(l, t, r, b, cx, cy, flags, bw, ibc, d[data_start + start:data_start + start + bc])
                  for (l, t, r, b, cx, cy, flags, start, bw, bc, ibc) in heads]
        return cls(unknown1, unknown2, primary, secondary, frames)

    def to_bytes(self) -> bytes:
        out = bytearray(struct.pack("<III", self.unknown1, self.unknown2, 0)) + self.primary + self.secondary
        out += struct.pack("<I", len(self.frames))
        start = 0
        for f in self.frames:
            out += SPF_FRAME_HEADER.pack(f.left, f.top, f.right, f.bottom, f.center_x, f.center_y, f.flags,
                                         start, f.byte_width, len(f.data), f.image_byte_count)
            start += len(f.data)
        out += struct.pack("<I", start)
        for f in self.frames:
            out += f.data
        return bytes(out)

    def palette_rgb(self) -> list[int]:
        """The primary palette as a flat [r, g, b, ...] list of 768 ints, for PIL's putpalette."""
        rgb: list[int] = []
        for i in range(256):
            v = struct.unpack_from("<H", self.primary, i * 2)[0]
            rgb += [((v >> 11) & 31) * 255 // 31, ((v >> 5) & 63) * 255 // 63, (v & 31) * 255 // 31]
        return rgb
```

  If `daformats.py` doesn't already `import struct`, `from dataclasses import dataclass` and `from pathlib import Path`, add them. It does today.

- [ ] **Step 5: PNG helpers** at `Tools/WorldList/spfpng.py`

```python
"""Move SPF frames to and from indexed PNGs. Edit the PNGs in indexed mode (Aseprite: Sprite > Color Mode >
Indexed) with the file's own palette; only the palette indexes are read back, so the palette in the PNG is ignored."""
from __future__ import annotations

from pathlib import Path

from PIL import Image


def frame_to_png(spf, frame, path: Path) -> None:
    im = Image.frombytes("P", (frame.width, frame.height), frame.data)
    im.putpalette(spf.palette_rgb())
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path)


def png_to_indexes(path: Path, width: int, height: int) -> bytes:
    im = Image.open(path)
    if im.mode != "P":
        raise ValueError(f"{path} must be an indexed (palette) PNG, not {im.mode}")
    if im.size != (width, height):
        raise ValueError(f"{path} is {im.size[0]}x{im.size[1]}, expected {width}x{height}")
    return im.tobytes()
```

- [ ] **Step 6: Drawer art** at `Tools/WorldList/make_drawer_art.py`

```python
"""Derive art/nusers.png (the new drawer background) from the Korean client's _nusers.spf. Run from the repo root:

    python Tools/WorldList/make_drawer_art.py

Edits palette indexes only, so every pixel keeps an exact color from the source palette:
  1. widen the title column by 30 px: duplicate columns 60-89, drop columns 440-469 (the middle of the count boxes)
  2. run the 18 row lines (4 px each: dark, bright, mid, dark at y = 20 + 15k) across the title column, copying the
     name column's line pixels (x 193-288, tiled every 96 px) into x 16-187
Re-running overwrites art/nusers.png, so rerun only to start the drawer over from the source.
"""
from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "Accessories"))
sys.path.insert(0, str(HERE))

from acclib.daformats import Spf  # noqa: E402
from spfpng import frame_to_png  # noqa: E402

WIDTH, HEIGHT = 498, 303
LINES, FIRST_LINE_TOP, PITCH = 18, 20, 15
TITLE_LEFT, TITLE_RIGHT = 16, 187
NAME_LEFT, NAME_SPAN = 193, 96


def widen_titles(rows: list[bytearray]) -> list[bytearray]:
    return [row[:90] + row[60:440] + row[470:] for row in rows]


def extend_row_lines(rows: list[bytearray]) -> list[bytearray]:
    out = [bytearray(row) for row in rows]
    for k in range(LINES):
        for y in range(FIRST_LINE_TOP + PITCH * k, FIRST_LINE_TOP + PITCH * k + 4):
            for x in range(TITLE_LEFT, TITLE_RIGHT + 1):
                out[y][x] = rows[y][NAME_LEFT + (x - TITLE_LEFT) % NAME_SPAN]
    return out


def main() -> int:
    spf = Spf.from_bytes((HERE / "source" / "kr_nusers.spf").read_bytes())
    frame = spf.frames[0]
    rows = [bytearray(frame.data[y * WIDTH:(y + 1) * WIDTH]) for y in range(HEIGHT)]
    rows = extend_row_lines(widen_titles(rows))
    frame.data = b"".join(bytes(r) for r in rows)
    frame_to_png(spf, frame, HERE / "art" / "nusers.png")
    print("wrote", HERE / "art" / "nusers.png")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 7: Button frames 0–17** at `Tools/WorldList/extract_buttons.py`

```python
"""Write art/nusersb/00.png-17.png from source/nusersb.spf (our nine original buttons, normal then lit).
Run once from the repo root: python Tools/WorldList/extract_buttons.py. Frames 18-43 are drawn by hand."""
from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "Accessories"))
sys.path.insert(0, str(HERE))

from acclib.daformats import Spf  # noqa: E402
from spfpng import frame_to_png  # noqa: E402


def main() -> int:
    spf = Spf.from_bytes((HERE / "source" / "nusersb.spf").read_bytes())
    for i, frame in enumerate(spf.frames):
        frame_to_png(spf, frame, HERE / "art" / "nusersb" / f"{i:02d}.png")
    print(f"wrote {len(spf.frames)} frames")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 8: Layout file** at `Tools/WorldList/_nusers.txt`. Write it with tabs and CRLF, using the Python one-liner below so line endings are exact.

```python
python - <<'EOF'
controls = [
    ("Noname", 0, "0 0 498 303", [('"_nusers.spf"', 0)]),
    ("UsersList", 7, "15 24 341 276", []),
    ("TotalNum", 7, "447 39 479 51", []),
    ("CountryNum", 7, "447 65 479 77", []),
    ("CountryBtn", 7, "365 62 431 78", [('"_nusersb.spf"', 0), ('"_nusersb.spf"', 1)]),
    ("MasterBtn", 7, "365 84 431 100", [('"_nusersb.spf"', 2), ('"_nusersb.spf"', 3)]),
    ("Close", 7, "392 277 453 299", [('"_nbtn.spf"', 0), ('"_nbtn.spf"', 1), ('"_nbtn.spf"', 2)]),
]
lines = []
for name, kind, rect, images in controls:
    lines += ["<CONTROL>", f'\t<NAME> "{name}"', f"\t<TYPE> {kind}", f"\t<RECT> {rect}"]
    if images:
        lines.append("\t<IMAGE>")
        lines += [f"\t\t{file} {frame}" for file, frame in images]
    lines.append("<ENDCONTROL>")
open("Tools/WorldList/_nusers.txt", "wb").write(("\r\n".join(lines) + "\r\n").encode("ascii"))
EOF
```

- [ ] **Step 9: Build tool** at `Tools/WorldList/build.py`

```python
"""Build the world list drawer into setoa.dat. Run from the repo root:

    python Tools/WorldList/build.py            # batch folder on the desktop + copy into the client folder
    python Tools/WorldList/build.py --no-apply # batch folder only

Reads art/nusers.png, art/nusersb/NN.png (00 upward, consecutive) and _nusers.txt, writes _nusers.spf,
_nusersb.spf and _nusers.txt into a copy of the client folder's setoa.dat, re-reads the copy to check every frame,
and writes the batch folder with manifest.txt. Upload the batch's setoa.dat with the client that uses it.
"""
from __future__ import annotations

import argparse
import shutil
import sys
from dataclasses import replace
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "Accessories"))
sys.path.insert(0, str(HERE))

from acclib.daformats import DatArchive, Spf  # noqa: E402
from acclib.paths import DEFAULT_DATA_DIR  # noqa: E402
from spfpng import png_to_indexes  # noqa: E402

FULL_FACE_COUNT = 44


def build_drawer() -> Spf:
    spf = Spf.from_bytes((HERE / "source" / "kr_nusers.spf").read_bytes())
    frame = spf.frames[0]
    frame.data = png_to_indexes(HERE / "art" / "nusers.png", frame.width, frame.height)
    return spf


def build_buttons() -> Spf:
    spf = Spf.from_bytes((HERE / "source" / "nusersb.spf").read_bytes())
    template = spf.frames[0]
    frames = []
    while (png := HERE / "art" / "nusersb" / f"{len(frames):02d}.png").exists():
        frames.append(replace(template, data=png_to_indexes(png, template.width, template.height)))
    spf.frames = frames
    return spf


def check(arc_bytes: bytes, drawer: Spf, buttons: Spf, layout: bytes) -> None:
    arc = DatArchive.from_bytes(arc_bytes)
    for name, expected in (("_nusers.spf", drawer), ("_nusersb.spf", buttons)):
        got = Spf.from_bytes(arc[name])
        if [f.data for f in got.frames] != [f.data for f in expected.frames]:
            raise SystemExit(f"{name} read back differently from its sources")
    if arc["_nusers.txt"] != layout:
        raise SystemExit("_nusers.txt read back differently from its source")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR, help="client folder to build from")
    ap.add_argument("--out", type=Path, help="output folder (default: a new folder on the desktop)")
    ap.add_argument("--no-apply", action="store_true", help="don't copy the batch into the client folder")
    args = ap.parse_args()

    drawer, buttons = build_drawer(), build_buttons()
    layout = (HERE / "_nusers.txt").read_bytes()

    arc = DatArchive.load(args.data_dir / "setoa.dat")
    arc.put("_nusers.spf", drawer.to_bytes())
    arc.put("_nusersb.spf", buttons.to_bytes())
    arc.put("_nusers.txt", layout)
    data = arc.to_bytes()
    check(data, drawer, buttons, layout)

    stamp = datetime.now()
    out = args.out or Path.home() / "Desktop" / f"WorldList batch {stamp:%Y%m%d-%H%M}"
    out.mkdir(parents=True, exist_ok=True)
    (out / "setoa.dat").write_bytes(data)
    faces = len(buttons.frames)
    lines = [f"World list drawer batch built {stamp:%Y-%m-%d %H:%M} from {args.data_dir}", "",
             "Changed client files (upload these):", "  setoa.dat (_nusers.spf, _nusersb.spf, _nusers.txt)", "",
             f"Button frames: {faces} of {FULL_FACE_COUNT}"]
    if faces < FULL_FACE_COUNT:
        lines.append("  NOT READY TO SHIP: the carved faces 18-43 are missing (Tools/WorldList/art/nusersb/)")
    (out / "manifest.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")

    if not args.no_apply:
        target = args.data_dir / "setoa.dat"
        shutil.copy2(target, target.with_name(f"setoa.dat.{stamp:%Y%m%d-%H%M%S}.bak"))
        shutil.copy2(out / "setoa.dat", target)
        print(f"copied the batch into {args.data_dir} (backup kept)")
    print(f"done: {out}")
    print(f"frames: {faces}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 10: Generate the art and run the tests**

```bash
python Tools/WorldList/make_drawer_art.py
python Tools/WorldList/extract_buttons.py
python -m unittest Tools/WorldList/test_worldlist.py
python -m unittest Tools/Accessories/test_items.py
```

Expected: `wrote .../art/nusers.png`, `wrote 18 frames`, and both test runs end with `OK`. The accessory tests must still pass after the `daformats.py` change.

- [ ] **Step 11: Look at the drawer.** Open `Tools/WorldList/art/nusers.png` and check it against the approved mock: row lines through the titles, narrow count boxes, and no visible seam at x 60–120 or at the header dots. Then run a trial build that doesn't touch the client folder:

Run: `python Tools/WorldList/build.py --no-apply`
Expected: `done: ...WorldList batch ...` and `frames: 18`. The manifest says NOT READY TO SHIP.

- [ ] **Step 12: README** at `Tools/WorldList/README.md`, and a row in `Tools/README.md`'s table.

```markdown
# World list drawer

The world list drawer's art and layout: the Korean client's grid drawer, widened titles, and carved filter-button
faces. Spec: Chaos.Client `docs/superpowers/specs/2026-09-26-worldlist-drawer-design.md`.

| File | What it is |
|---|---|
| `source/kr_nusers.spf` | The Korean client's drawer, untouched. `make_drawer_art.py` starts from it. |
| `source/nusersb.spf` | Our original `_nusersb.spf` (nine buttons). Its palette and frame header are reused. |
| `art/nusers.png` | The drawer background. Indexed PNG; edit in indexed mode with its own palette. |
| `art/nusersb/NN.png` | Button frames: 00-17 the original nine (normal, lit), 18-43 the added faces. |
| `_nusers.txt` | The drawer's layout (control rects). |

Build: `python Tools/WorldList/build.py` writes a desktop batch folder with `setoa.dat` and `manifest.txt`, and copies
it into the client folder (backup kept). `--no-apply` leaves the client folder alone. Upload the batch's `setoa.dat`
in the same launcher patch as the client that uses the new drawer.

Tests: `python -m unittest Tools/WorldList/test_worldlist.py`.
```

  In `Tools/README.md`, add this row to the table after `Hair/`:

`| `WorldList/` | The world list drawer's art, layout and carved button faces, and the setoa.dat build. Start with `WorldList/README.md`. |`

```json:metadata
{"files": ["Unora/Tools/Accessories/acclib/daformats.py", "Unora/Tools/WorldList/spfpng.py", "Unora/Tools/WorldList/make_drawer_art.py", "Unora/Tools/WorldList/extract_buttons.py", "Unora/Tools/WorldList/build.py", "Unora/Tools/WorldList/_nusers.txt", "Unora/Tools/WorldList/README.md", "Unora/Tools/WorldList/test_worldlist.py", "Unora/Tools/WorldList/source/", "Unora/Tools/WorldList/art/", "Unora/Tools/README.md"], "verifyCommand": "cd Unora && python -m unittest Tools/WorldList/test_worldlist.py && python -m unittest Tools/Accessories/test_items.py && python Tools/WorldList/build.py --no-apply", "acceptanceCriteria": ["Spf round-trips real files byte for byte", "nusers.png 498x303 indexed with the agreed edits", "_nusers.txt rects match the spec, CRLF + tabs", "build writes batch + manifest and re-reads every frame"], "modelTier": "standard"}
```

---

### Task 7: Carved button faces 18–43

**Goal:** Draw the 13 new faces, each in normal and lit states, in the original carved style. The user approves them on a 4× review sheet.

**Files:**
- Create: `Unora/Tools/WorldList/art/nusersb/18.png`–`43.png`
- Create: `Unora/Tools/WorldList/letters.py` (cuts letters from 00–17 and composes words; kept so faces can be rebuilt)
- Create: `Unora/Tools/WorldList/art/letters_normal.png`, `art/letters_lit.png` (the carved alphabet, including hand-drawn letters)
- Create: `Unora/Tools/WorldList/review_faces.py` → writes `review/faces.png` (not committed; add `Tools/WorldList/review/` to `.gitignore`)

**Acceptance Criteria:**
- [ ] Frames 18–43 exist as indexed 66×16 PNGs using `source/nusersb.spf`'s palette, in the order: Temuair, Medenia, Ability, Berserker, Warlord, Archer, Assassin, Arcanist, Elemental, Bard, Plague Dr, Adept, Druid (normal then lit).
- [ ] The background of each new face comes from a blank version of an original button, in the same state. Letters come from the carved alphabet. T, A, B, D, E, b, c, h, m and p are drawn in the same style: face color, dark outline and bevel, matching the neighboring letters in each state.
- [ ] Every label's letters span no more than 53 px, like "Country". Berserker, Elemental and Plague Dr use 1 px less spacing than normal.
- [ ] `review/faces.png` shows each new face at 4× beside the originals, in both states. The user approves it before Task 8's in-game check and before any batch ships.
- [ ] `python Tools/WorldList/build.py --no-apply` reports `frames: 44` and no NOT READY line.

**Verify:** `python Tools/WorldList/build.py --no-apply` → `frames: 44`. User approval of `review/faces.png` is recorded in the task notes.

**Steps:**

- [ ] **Step 1: Cut the carved alphabet.** In `letters.py`, load 00–17 as palette indexes. For each original label, find its letters by column. A column belongs to a letter when it holds any outline pixel (luminance under 70) in rows 3–13. Where letters touch, split by hand, and record the letter boxes in a table in the script. Save the normal letters from even frames and the lit letters from odd frames into `art/letters_normal.png` and `art/letters_lit.png`. Label each glyph in a JSON sidecar (`letters.json`: character → x, width, for each state). The available letters are C o u n t r y M a s e W i R g z d P k l G.
- [ ] **Step 2: Make blank buttons.** For each state, blank the label area x 13–53 of the Monk frame (12 normal, 13 lit). Fill it from the clean margins: take columns 3–12 and 54–62, alternating in 10-px runs. Use these blanks as the background of every new face.
- [ ] **Step 3: Draw the missing letters** T, A, B, D, E, b, c, h, m and p, at the same cap height and x-height as their neighbors, in both states. Build them from existing strokes where possible:
  - E from F-like strokes of "t" and "l".
  - h from "n" plus the ascender of "l".
  - b from "o" plus the ascender of "l".
  - p from "o" plus the descender of "g".
  - m from two "n" arches.
  - c from "o" with the right side opened.
  - D and B from "P", closed and doubled.
  - A from mirrored "W" diagonals, with a crossbar.
  - T from the "t" bar and stem, raised to cap height.

  Add them to the letter sheets and `letters.json`.
- [ ] **Step 4: Compose the 13 words.** Center each word in the button the way the originals are centered. Use the normal spacing measured from the originals. Use 1 px less spacing for Berserker, Elemental and Plague Dr. Write `art/nusersb/18.png`–`43.png` as indexed PNGs with `spfpng.frame_to_png`, using `source/nusersb.spf`'s palette. Assert that each word's letters span at most 53 px, and fail loudly if not.
- [ ] **Step 5: Review sheet.** In `review_faces.py`, write `review/faces.png` at 4×. It has one row per button: the original face (normal, lit) followed by its new faces (normal, lit). Add `Tools/WorldList/review/` to Unora's `.gitignore`.
- [ ] **Step 6: User review.** Show the user `review/faces.png` and ask for approval, or for touch-ups. They can edit `art/nusersb/NN.png` in Aseprite directly, in indexed mode. Re-run `review_faces.py` after any change, and repeat until the user approves. Record "faces approved by user on <date>" in the task.
- [ ] **Step 7: Build check**

Run: `python Tools/WorldList/build.py --no-apply`
Expected: `frames: 44`, and the manifest has no NOT READY line.

```json:metadata
{"files": ["Unora/Tools/WorldList/art/nusersb/", "Unora/Tools/WorldList/letters.py", "Unora/Tools/WorldList/letters.json", "Unora/Tools/WorldList/art/letters_normal.png", "Unora/Tools/WorldList/art/letters_lit.png", "Unora/Tools/WorldList/review_faces.py", "Unora/.gitignore"], "verifyCommand": "cd Unora && python Tools/WorldList/build.py --no-apply", "acceptanceCriteria": ["frames 18-43 indexed 66x16 in agreed order", "missing letters drawn in carved style in both states", "labels within 53 px; three long names packed 1 px tighter", "user approves review/faces.png", "build reports frames: 44"], "modelTier": "frontier"}
```

---

### Task 8: In-game check on a local server

**Goal:** Confirm in the running game that the drawer looks and behaves as specified, with screenshots.

**Files:**
- None changed. Screenshots go to the scratchpad.

**Acceptance Criteria:**
- [ ] The drawer shows the new art. Rows sit in the grid cells, 17 show at once, and the mouse wheel scrolls one row at a time.
- [ ] Titles up to 28 letters fit, and counts sit right-aligned in the small boxes.
- [ ] Each of the nine buttons turns through its faces with the carved art, lit and unlit. Its count changes with the face.
- [ ] A character on a Temuair map counts under Temuair. After walking to a Medenia map and reopening the list, it counts under Medenia.
- [ ] Reopening the list lights only Country, and the other buttons keep their faces.

**Verify:** screenshots of each check, reviewed against the approved mock (`layout-final`/`full-grid` screens).

**Steps:**

- [ ] **Step 1:** Ask the user to stop any running `Chaos.exe` and client, then build the server (`dotnet build Chaos-Server/Chaos.slnx`). When that finishes, build the client (`dotnet build Chaos.Client.slnx`). Never build both at once.
- [ ] **Step 2:** Apply the art: `python Tools/WorldList/build.py`, run from Unora. It copies into the client folder and keeps a backup.
- [ ] **Step 3:** Start the local server and the client. Log in with a test character. Take screenshots with the game window automation: PowerShell user32 clicks and screenshots, where UI coordinates are 640×480 scaled.
- [ ] **Step 4:** Walk through each acceptance criterion, with a screenshot per check. To test continents, use a character with a Medenian class and ability levels, or set them with admin commands. Walk between a Temuair and a Medenia map, reopening the list each time.
- [ ] **Step 5:** Report any mismatch as a bug against Task 5, 6 or 7, with its screenshot. Only if the art needs it, adjust `TEXT_Y` (Task 5) or the Close rect in `_nusers.txt` (Task 6), then rebuild and recheck.

```json:metadata
{"files": [], "verifyCommand": "", "acceptanceCriteria": ["new art, 17 rows in grid, scroll one row", "titles to 28 letters, counts right-aligned", "every button turns with carved art and counts follow", "continent counts follow the player between Temuair and Medenia", "reopen lights only Country, other faces kept"], "modelTier": "standard"}
```

---

### Task 9: Commit the full implementation

**Goal:** Commit each repo's changes on `feat/worldlist-drawer`, staging explicit paths only.

**Files:**
- Commit only: the files listed in Tasks 1–7.

**Acceptance Criteria:**
- [ ] Chaos-Server: one commit with the Task 1–3 files and tests.
- [ ] Unora: one commit with `Tools/WorldList/`, `Tools/Accessories/acclib/daformats.py`, `Tools/README.md` and `.gitignore`.
- [ ] Chaos.Client: one commit with the Task 4–5 files, `controlFileList.txt`, `CLAUDE.md`, this plan and its `.tasks.json`. The plan and tasks file must be force-added, because `docs/` is gitignored.
- [ ] Nothing else is staged, and `launchSettings.json` is not committed.

**Verify:** `git status --short` in each repo shows only files unrelated to this plan. `git log -1 --stat` lists exactly the intended files.

**Steps:**

- [ ] **Step 1: Chaos-Server** (run inside `Chaos.Client/Chaos-Server`)

```bash
git add Chaos.DarkAges/Definitions/Enums.cs Chaos/Utilities/MapContinent.cs Chaos/Collections/MapInstance.cs \
  Chaos/Services/Storage/ExpiringMapInstanceCache.cs Chaos.Networking/Entities/Server/WorldListMemberInfo.cs \
  Chaos.Networking/Converters/Server/WorldListConverter.cs Chaos/Services/MapperProfiles/AislingMapperProfile.cs \
  Tests/Chaos.Tests/MapContinentTests.cs Tests/Chaos.Tests/Networking/WorldListConverterTests.cs \
  Tests/Chaos.Tests/AislingMapperProfileTests.cs
git commit -m "Send each player's Medenian class, continent and ability flag in the world list

Maps take their continent from their MapInstances folder. The values travel in a length-prefixed section after
the last world list entry, so older and newer readers both read the packet safely.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 2: Unora**

```bash
git add Tools/WorldList Tools/Accessories/acclib/daformats.py Tools/README.md .gitignore
git commit -m "Add the world list drawer art, carved button faces and setoa build

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 3: Chaos.Client**

```bash
git add Chaos.Client/Models/WorldListEntry.cs Chaos.Client/Collections/WorldState.cs \
  Chaos.Client/Controls/World/Popups/WorldList/WorldListFaces.cs \
  Chaos.Client/Controls/World/Popups/WorldList/WorldListFilterState.cs \
  Chaos.Client/Controls/World/Popups/WorldList/WorldListEntryControl.cs \
  Chaos.Client/Controls/World/Popups/WorldList/WorldListControl.cs \
  Tests/Chaos.Client.Tests/WorldListFacesTests.cs controlFileList.txt CLAUDE.md
git add -f docs/superpowers/plans/2026-09-26-worldlist-drawer.md docs/superpowers/plans/2026-09-26-worldlist-drawer.md.tasks.json
git commit -m "Redesign the world list drawer with the grid art and rotating filter buttons

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4:** Report the three commit hashes. Merging, pushing, and pointing the client's `Chaos-Server` submodule at the merged server commit are handled when the branches are finished, not here.

```json:metadata
{"files": [], "verifyCommand": "git log -1 --stat", "acceptanceCriteria": ["one server commit with Task 1-3 files", "one Unora commit with Tools/WorldList and daformats", "one client commit with Task 4-5 files, docs, plan", "nothing unrelated staged; launchSettings.json not committed"], "modelTier": "mechanical"}
```
