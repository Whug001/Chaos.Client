# Illustria's Closet (fitting room) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A fitting room in Abel where players try on every overcoat, overhelmet and accessory in the game on a mirror preview, without buying.

**Architecture:** The server builds a catalog of cosmetic "looks" (dye variants grouped) from item templates at boot and sends it in one new server packet when a player steps on the mirror tile or asks the NPC. The client shows a new window modelled on Josephine's beauty shop, with the character standing in front of a mirror; all try-on is client-only. Unora data turns Abel Empty Room 3 into Illustria's Closet with a wall mirror, a trigger tile and the NPC Illustria.

**Tech Stack:** C# 14 / .NET 10, MonoGame, TUnit + FluentAssertions (+ Moq on the server), Unora JSON data.

**Spec:** `docs/superpowers/specs/2026-10-12-illustrias-closet-fitting-room-design.md` (client worktree)

## Worktrees and commands

| Repo | Worktree (branch `fitting-room`) |
|---|---|
| Chaos-Server | `C:\Users\Michael\Documents\GitHub\worktrees\fitting-room-server` |
| Chaos.Client | `C:\Users\Michael\Documents\GitHub\worktrees\fitting-room-client` |
| Unora | `C:\Users\Michael\Documents\GitHub\worktrees\fitting-room-unora` |

- Server tests: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<ClassName>/*"` (from the server worktree). `dotnet test` does not work.
- Client build: `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\fitting-room-server`
- Client tests: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\fitting-room-server -- --no-ansi --treenode-filter "/*/*/<ClassName>/*"`
- Never run two builds of these solutions at once. Never build the shared checkouts (`GitHub\Chaos.Client`, `GitHub\Chaos.Client\Chaos-Server`); build only the worktrees.
- Known unrelated server test failures may exist (OnItemDroppedOn stackable, LootBox, mayor fireworks). Only tests you touch must pass.
- Serena edit tools work for paths under `C:\Users\Michael\Documents\GitHub` (worktrees included).
- **Do not commit.** Leave changes in the working tree; the final task commits.

## Global Constraints

- New server opcode: `ServerOpCode.FittingRoomDisplay = 157`. No client opcode (the window sends nothing).
- `CLIENT_VERSION` goes 780 → 781; rename the pin test to `Client_version_is_781`.
- Cosmetic = `EquipmentType` is `OverArmor`, `OverHelmet` or `Accessory` (reuse `TownImportLooks.IsCosmetic`).
- Gender `null` counts as `Unisex`.
- Mount exclusion: template key ends with `_mount` or name ends with ` Mount` (both OrdinalIgnoreCase).
- Hide list: Unora `Data/Configuration/FittingRoom/hidden.json`, shape `{ "hiddenTemplateKeys": [] }`; a missing file means an empty list.
- Draw layer of a look comes from `TownImportLooks.From(template)` (server), extended with a new `TownImportLayer.OverHair = 4` for `DrawsOverHair` overhelmets.
- OverHair placement copies the server mapper: first empty of accessory slots 1, 2, 3, else slot 3; the head keeps the hair/helmet.
- Dialog option text ≤ 35 chars, dialog text ≤ 360 chars and 90 per line, orange bar ≤ 45 chars, no em dashes in JSON.
- Comments only where the why is not obvious. No explanatory comments in tests.

**User decisions (already made):**
- "2" — a fitting room only; nothing is sold.
- "1 but no GM items" — every cosmetic in the game, excluding admin items (none exist today; the hide list covers future ones) and mounts.
- Layout "C" — the character stands with their back to the viewer; the mirror shows the front.
- "3" — a real wall mirror in the shop map; stepping in front of it opens the window; the NPC also opens it.
- "Abel empty room 3" for the location; room name "Illustria's Closet"; NPC "Illustria".
- "Get this done, use your suggestions" — the design presented in chat (one tile per look with real colors, dyeable looks offer every color, start in the real outfit, "Take everything off", click to wear / click again to remove, 4th accessory replaces the oldest, Wearing tab with remove buttons, walk-in animation, turning both views together).
- Door: Abel has no free building door. Repoint Abel door (7,21) — the second door of the unused Abel Empty Room 1 — to Illustria's Closet (data only, no art).

---

### Task 1: Server protocol — FittingRoomDisplay packet + version 781

**Goal:** The packet that carries the catalog exists, round-trips, and the client version is 781.

**Files:**
- Modify: `Chaos.Networking.Abstractions/Definitions/Enums.cs` (add `ServerOpCode.FittingRoomDisplay = 157` after `GlassWayState`)
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (add `FittingRoomDisplayType`; add `OverHair = 4` to `TownImportLayer`)
- Modify: `Chaos.DarkAges/Definitions/CONSTANTS.cs:23` (781)
- Create: `Chaos.Networking/Entities/Server/FittingRoomDisplayArgs.cs`
- Create: `Chaos.Networking/Converters/Server/FittingRoomDisplayConverter.cs`
- Modify: `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, `Chaos/Networking/ChaosWorldClient.cs` (send methods)
- Test: `Tests/Chaos.Tests/Networking/FittingRoomPacketConverterTests.cs`; modify `Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs` (version pin)

**Acceptance Criteria:**
- [ ] Open (with looks, colors, gender, hair) and Close round-trip byte-exactly through `FittingRoomDisplayConverter`.
- [ ] Look count is written as u16; a color list over 255 throws `InvalidOperationException`; an unknown type byte throws `ArgumentOutOfRangeException`.
- [ ] `The_opcode_is_157` and `Client_version_is_781` pass; the opcode-uniqueness test still passes.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/FittingRoomPacketConverterTests/*"` → all pass; same for `GuildEmblemPacketConverterTests`.

**Steps:**

- [ ] **Step 1: Enums.** In `Chaos.DarkAges/Definitions/Enums.cs`, next to `BeautyShopDisplayType`, add (with `<summary>` docs in the file's style):

```csharp
public enum FittingRoomDisplayType : byte
{
    /// <summary>Opens the fitting room with the catalog and the player's own hair.</summary>
    Open = 0,
    /// <summary>Closes the fitting room.</summary>
    Close = 1
}
```

Add to `TownImportLayer` (at `:1826`): `OverHair = 4` with summary "An overhelmet that draws over hair: it goes into an accessory slot and the head keeps the hair or helmet." In `Chaos.Networking.Abstractions/Definitions/Enums.cs` add `FittingRoomDisplay = 157` to `ServerOpCode` after `GlassWayState = 156`, with a summary in the same style.

- [ ] **Step 2: Args.** `Chaos.Networking/Entities/Server/FittingRoomDisplayArgs.cs`:

```csharp
namespace Chaos.Networking.Entities.Server;

public sealed record FittingRoomDisplayArgs : IPacketSerializable
{
    public required FittingRoomDisplayType Type { get; set; }
    public Gender Gender { get; set; }
    public ushort HairStyle { get; set; }
    public DisplayColor HairColor { get; set; }
    public IReadOnlyList<FittingRoomLook> Looks { get; set; } = [];
}

public sealed record FittingRoomLook
{
    public required string Name { get; init; }
    public required ushort PanelSprite { get; init; }
    public required Gender Gender { get; init; }
    public required TownImportLookInfo Look { get; init; }
    public bool Dyeable { get; init; }
    public IReadOnlyList<DisplayColor> Colors { get; init; } = [];
}
```

(Match the usings/namespace style of `BeautyShopDisplayArgs.cs`. `TownImportLookInfo` lives in `Chaos.Networking/Entities/Server/TownImportInfo.cs`.) `Look.Color` is the default color shown first; `Colors` lists the colors that exist as items (sorted by enum value, distinct). For a dyeable look `Dyeable` is true and the client offers every `DisplayColor`.

- [ ] **Step 3: Converter.** Copy the shape of `BeautyShopDisplayConverter`. Wire format:
  - byte type.
  - Open: byte gender, u16 hairStyle, byte hairColor, u16 lookCount, then per look: String8 name, u16 panelSprite, byte gender, byte layer, u16 displaySprite, byte color, bool keepsHairColor, bool hidesHead, bool hidesBoots, bool dyeable, byte colorCount, colorCount bytes.
  - Close: nothing more.
  - Count guards: lookCount > ushort.MaxValue or colorCount > 255 → `InvalidOperationException` (same guard style as the beauty converter). Unknown type → `ArgumentOutOfRangeException`. Check how `TownImportInfo`'s converter serializes `TownImportLookInfo` and reuse the field order above regardless.

- [ ] **Step 4: Send methods.** `IChaosWorldClient`: `void SendFittingRoomOpen(FittingRoomDisplayArgs args);` and `void SendFittingRoomClose();` next to the beauty-shop ones. `ChaosWorldClient`: `Send(args with { Type = FittingRoomDisplayType.Open })` and `Send(new FittingRoomDisplayArgs { Type = FittingRoomDisplayType.Close })`.

- [ ] **Step 5: Version.** `CONSTANTS.CLIENT_VERSION = 781`. Rename `Client_version_is_780` to `Client_version_is_781` with `.Be(781)`. If an opcode-uniqueness test lists server opcodes explicitly, add 157.

- [ ] **Step 6: Tests** in `Tests/Chaos.Tests/Networking/FittingRoomPacketConverterTests.cs`, using the `RoundTrip` helper pattern from `BeautyShopPacketConverterTests.cs`:
  - `Open_round_trips` (two looks: one Overcoat with colors [Default, Red], one OverHair dyeable with no colors; gender Female, hair 17, hair color Blue).
  - `Close_round_trips`.
  - `More_than_255_colors_throws` (a look with 256 colors → `InvalidOperationException` on serialize).
  - `Unknown_type_throws` (deserialize `[200]`).
  - `The_opcode_is_157` (`((byte)ServerOpCode.FittingRoomDisplay).Should().Be(157)`).

- [ ] **Step 7: Run tests** — both classes pass.

```json:metadata
{"files": ["Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking/Entities/Server/FittingRoomDisplayArgs.cs", "Chaos.Networking/Converters/Server/FittingRoomDisplayConverter.cs", "Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos/Networking/ChaosWorldClient.cs", "Tests/Chaos.Tests/Networking/FittingRoomPacketConverterTests.cs", "Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/FittingRoomPacketConverterTests/*\"", "acceptanceCriteria": ["Open and Close round-trip", "u16 look count, >255 colors throws, unknown type throws", "opcode 157 and version 781 tests pass"], "modelTier": "standard", "repo": "fitting-room-server"}
```

---

### Task 2: Server catalog — grouping, exclusions, hide list, OverHair layer

**Goal:** A `FittingRoomCatalog` singleton holds every cosmetic look grouped by dye variant, built at boot, minus mounts and hidden keys; town imports learn the OverHair layer.

**Files:**
- Create: `Chaos/Services/FittingRoom/FittingRoomCatalog.cs` (the built list + `BuildOpenArgs(Aisling)`)
- Create: `Chaos/Services/FittingRoom/FittingRoomCatalogBuilder.cs` (pure: `Build(IEnumerable<ItemTemplate>, IReadOnlySet<string> hidden)`)
- Create: `Chaos/Services/FittingRoom/FittingRoomOptions.cs` (hide-list location, `IDirectoryBound` pattern like `TownGovernmentOptions`/`TowerOptions`)
- Create: `Chaos/Services/FittingRoom/FittingRoomCatalogValidationService.cs` (hosted service; forces the build at boot and logs the look count)
- Modify: `Chaos/Services/Towns/TownImportItems.cs` (`TownImportLooks.From`: `DrawsOverHair` overhelmet → `TownImportLayer.OverHair`)
- Modify: `Chaos/Extensions/ServiceCollectionExtensions.cs` (registrations next to the beauty shop at ~504) and `Chaos/appsettings.json` only if the options pattern needs a section (follow whatever `TowerOptions` does; do not touch the local `StagingDirectory` value)
- Test: `Tests/Chaos.Tests/FittingRoom/FittingRoomCatalogBuilderTests.cs`; extend `Tests/Chaos.Tests/Towns/TownImportItemsTests.cs`

**Acceptance Criteria:**
- [ ] Only OverArmor/OverHelmet/Accessory templates become looks; mounts and hidden keys are left out.
- [ ] Variants sharing (layer, display sprite, gender, keepsHairColor, hidesHead, hidesBoots) become one look whose `Colors` are the distinct colors, sorted; `Dyeable` is true if any variant is dyeable.
- [ ] Look name: the name of the variant with `DisplayColor.Default` if any, else the most common name in the group (ties: ordinal-first). Looks are sorted by layer then name (OrdinalIgnoreCase).
- [ ] Gender null → Unisex. `Look.Color` = `DisplayColor.Default` if present in Colors, else the first color.
- [ ] `TownImportLooks.From` returns `OverHair` for a `DrawsOverHair` overhelmet; existing town import tests still pass.
- [ ] `BuildOpenArgs(aisling)` fills Gender, HairStyle (`aisling.HairStyle`), HairColor (`aisling.HairColor`) and the looks.
- [ ] The server boots the catalog via the hosted service (resolving the singleton).

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/FittingRoomCatalogBuilderTests/*"` and `.../TownImportItemsTests/*` → pass; `dotnet build Chaos.slnx` (server worktree) → 0 errors.

**Steps:**

- [ ] **Step 1: Failing tests.** Reuse `TownImportItemsTests.Template(...)` (make it reachable or copy a local helper with every `required` member). Tests:
  - `Non_cosmetics_are_skipped` (a Weapon and an Armor → no looks).
  - `Dye_variants_group_into_one_look` (3 accessories, same sprite/gender, colors Red, Default, Blue → one look, Colors [Default, Blue, Red] in enum order, name of the Default one).
  - `Different_genders_stay_separate`.
  - `Mounts_are_left_out` (key `brown_horse_mount`; and name `Ant Mount` with another key).
  - `Hidden_keys_are_left_out` (case-insensitive).
  - `Null_gender_is_unisex`.
  - `Dyeable_if_any_variant_is_dyeable`.
  - `Draws_over_hair_overhelmet_is_over_hair_layer`.
  - In `TownImportItemsTests`: `A_draws_over_hair_overhelmet_is_the_over_hair_layer`.

- [ ] **Step 2: Implement** `FittingRoomCatalogBuilder.Build` as a pure static method returning `IReadOnlyList<FittingRoomLook>`; use `TownImportLooks.IsCosmetic` and `TownImportLooks.From`. `FittingRoomLook.PanelSprite` = the panel sprite of the variant whose name was chosen.

- [ ] **Step 3: Hide list.** `FittingRoomOptions` points at `Data/Configuration/FittingRoom/hidden.json` under the staging directory, following the existing `IDirectoryBound` options pattern. Load with `System.Text.Json` (case-insensitive property names) into `{ HiddenTemplateKeys: string[] }`; a missing file → empty set, a malformed file → throw (boot should fail loudly, like the beauty shop validation).

- [ ] **Step 4: Catalog + DI.** `FittingRoomCatalog` is built from `ISimpleCache<ItemTemplate>` — **call `templates.ForceLoad()` before enumerating** — and the options. Register as singleton next to the beauty shop, plus `services.AddHostedService<FittingRoomCatalogValidationService>()` whose `StartAsync` logs `"Fitting room catalog built with {Count} looks"` via `logger.WithTopics(Topics.Entities.Merchant)` (same as `BeautyShopCatalogValidationService`).

- [ ] **Step 5: Run tests + build.**

```json:metadata
{"files": ["Chaos/Services/FittingRoom/FittingRoomCatalog.cs", "Chaos/Services/FittingRoom/FittingRoomCatalogBuilder.cs", "Chaos/Services/FittingRoom/FittingRoomOptions.cs", "Chaos/Services/FittingRoom/FittingRoomCatalogValidationService.cs", "Chaos/Services/Towns/TownImportItems.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Tests/Chaos.Tests/FittingRoom/FittingRoomCatalogBuilderTests.cs", "Tests/Chaos.Tests/Towns/TownImportItemsTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/FittingRoomCatalogBuilderTests/*\"", "acceptanceCriteria": ["cosmetics only, mounts and hidden excluded", "variants grouped with sorted distinct colors", "name rule and sort order", "OverHair layer from TownImportLooks.From", "BuildOpenArgs fills hair and gender", "catalog built at boot"], "modelTier": "standard", "repo": "fitting-room-server"}
```

---

### Task 3: Server scripts — mirror tile and Illustria's dialog

**Goal:** Stepping on the mirror tile opens the fitting room and stepping off closes it; Illustria's dialog option opens it too.

**Files:**
- Create: `Chaos/Scripting/ReactorTileScripts/Temauir/Abel/FittingRoomMirrorScript.cs` (key `fittingRoomMirror`)
- Create: `Chaos/Scripting/DialogScripts/Temuair/Abel/FittingRoomOpenScript.cs` (key `fittingRoomOpen`)
- Test: `Tests/Chaos.Tests/FittingRoom/FittingRoomMirrorScriptTests.cs`

(Check the exact folder spelling used by existing reactor and dialog scripts — `Temauir` vs `Temuair` — and match the neighbours, e.g. `ReactorTileScripts/Temauir/Casino/`.)

**Acceptance Criteria:**
- [ ] `OnWalkedOn(aisling)` sends `SendFittingRoomOpen(catalog.BuildOpenArgs(aisling))` and remembers the aisling id; non-aislings are ignored.
- [ ] `Update` (checked at most every 250 ms) sends `SendFittingRoomClose()` to each remembered aisling no longer standing on the tile and forgets them; several players can be remembered at once; an aisling that left the map is forgotten (send Close only if still on this map instance and connected).
- [ ] `FittingRoomOpenScript.OnDisplaying` closes the dialog and opens the fitting room.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/FittingRoomMirrorScriptTests/*"` → pass.

**Steps:**

- [ ] **Step 1: Study** `SlotMachineStoolScript.cs` (open on step-on, close when the holder leaves, checked in `Update`) and `BeautyShopOpenScript.cs` (close dialog in `OnDisplaying`, then open). Reactor and dialog scripts can take services by constructor.
- [ ] **Step 2: Failing tests** for the mirror script, mocking `IChaosWorldClient` like the slot stool tests do (find `SlotMachineStoolScriptTests` or the nearest reactor-script test and copy its map/aisling setup): opens on walk-on; ignores a monster; closes after the aisling moves off and `Update` passes 250 ms; does not close while still on the tile; two aislings tracked independently.
- [ ] **Step 3: Implement** both scripts. Use a `HashSet<uint>` of aisling ids and an `IIntervalTimer`/elapsed accumulator for the 250 ms check, whichever the codebase's reactor scripts already use.
- [ ] **Step 4: Run tests.**

```json:metadata
{"files": ["Chaos/Scripting/ReactorTileScripts/Temauir/Abel/FittingRoomMirrorScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Abel/FittingRoomOpenScript.cs", "Tests/Chaos.Tests/FittingRoom/FittingRoomMirrorScriptTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/FittingRoomMirrorScriptTests/*\"", "acceptanceCriteria": ["walk-on opens and remembers", "step-off closes within 250 ms tick, multi-player", "dialog option opens"], "modelTier": "standard", "repo": "fitting-room-server"}
```

---

### Task 4: Unora data — Illustria's Closet

**Goal:** Abel Empty Room 3 becomes Illustria's Closet with a door, a north mirror, the trigger tile, Illustria and the hide list.

**Files (all under `Data/Configuration/` in the Unora worktree):**
- Modify: `MapInstances/Temuair/Towns/Abel/Abel_empty_room3/instance.json` (name `Illustria's Closet`, `scriptKeys: ["mirrormap"]`)
- Create: `MapInstances/Temuair/Towns/Abel/Abel_empty_room3/mirrors.json`
- Modify: `MapInstances/Temuair/Towns/Abel/Abel_empty_room3/reactors.json` (exit destination + trigger)
- Modify: `MapInstances/Temuair/Towns/Abel/Abel_empty_room3/merchants.json` (Illustria spawn)
- Modify: `MapInstances/Temuair/Towns/Abel/Abel/reactors.json` (door (7, 21) → room 3)
- Create: `Templates/Merchants/Temauir/abel/illustria.json`
- Create: `Templates/Dialogs/Temauir/abel/illustria/illustria_initial.json`, `illustria_fit.json`
- Modify: `MetaData/MundaneIllustrations.json` (Illustria → `west1.spf`, Josephine's portrait)
- Create: `FittingRoom/hidden.json`

**Acceptance Criteria:**
- [ ] Abel door (7, 21) warps to `abel_empty_room3:(6, 11)`; the room's exits (6,12),(7,12) warp back to the same street tile Abel Empty Room 1 uses when you leave through its (7,21)-side door (read Abel_empty_room1/reactors.json — the exit whose destination is near (7, 22) — and copy that destination).
- [ ] `mirrors.json` = `{"segments":[{"id":"closet-mirror","x":9,"y":0,"side":"north","length":3,"style":"glass"}],"darkStretches":[]}`.
- [ ] Trigger reactor `{"scriptKeys":["fittingRoomMirror"],"scriptVars":{},"source":"(10, 1)"}` (no `shouldBlockPathfinding`).
- [ ] Illustria spawns at `(12, 2)` facing `Down`, sprite `1299`, `scriptKeys ["showdialog"]`, `scriptVars.showdialog.dialogKey "illustria_initial"`, no items, `restockIntervalHrs 1`, `restockPct 100` (copy Josephine's file shape).
- [ ] `illustria_initial`: `DialogMenu`, text `Welcome to my closet, dear. Every coat, hat and trinket in the land hangs here. Step up to the mirror and try on anything you like.`, one option `Let me try things on` → `illustria_fit`.
- [ ] `illustria_fit`: `Normal`, `scriptKeys ["fittingRoomOpen"]`, text `Step up to the mirror.`
- [ ] `FittingRoom/hidden.json` = `{ "hiddenTemplateKeys": [] }`.
- [ ] All JSON parses (`python -I -c "import json,sys;[json.load(open(p,encoding='utf-8')) for p in sys.argv[1:]]" <files>`), no em dashes, 2-space indent matching neighbours.

**Verify:** the python JSON parse command over every touched file exits 0; `git -C <unora worktree> diff --stat` lists only the files above.

**Steps:**

- [ ] **Step 1:** Read the neighbour files (Josephine's merchant + dialogs, Suomi theatre instance/mirrors/reactors, Abel_empty_room1/reactors.json) for exact shapes.
- [ ] **Step 2:** Make the edits above. Keep property order and formatting of the existing files (these are serializer-written JSON; match key order alphabetical as in the neighbours).
- [ ] **Step 3:** Parse-check every file.

```json:metadata
{"files": ["Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_empty_room3/instance.json", "Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_empty_room3/mirrors.json", "Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_empty_room3/reactors.json", "Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_empty_room3/merchants.json", "Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel/reactors.json", "Data/Configuration/Templates/Merchants/Temauir/abel/illustria.json", "Data/Configuration/Templates/Dialogs/Temauir/abel/illustria/illustria_initial.json", "Data/Configuration/Templates/Dialogs/Temauir/abel/illustria/illustria_fit.json", "Data/Configuration/MetaData/MundaneIllustrations.json", "Data/Configuration/FittingRoom/hidden.json"], "verifyCommand": "python -I -c \"import json,sys;[json.load(open(p,encoding='utf-8')) for p in sys.argv[1:]]\" <files>", "acceptanceCriteria": ["door + exits linked", "mirror segment", "trigger reactor", "Illustria merchant, dialogs, spawn, portrait", "hide list file", "JSON parses"], "modelTier": "mechanical", "repo": "fitting-room-unora"}
```

---

### Task 5: Client plumbing, view model and outfit rules

**Goal:** The client receives FittingRoomDisplay, keeps the catalog and the tried-on outfit in a view model, and composes the preview appearance by clear rules; town import previews learn OverHair.

**Files (client worktree):**
- Modify: `Chaos.Client.Networking/Definitions/Delegates.cs` (`FittingRoomDisplayHandler`)
- Modify: `Chaos.Client.Networking/ConnectionManager.cs` (event `OnFittingRoomDisplay`, handler registered in `IndexHandlers()`)
- Create: `Chaos.Client/ViewModel/FittingRoom.cs`
- Create: `Chaos.Client/Systems/FittingRoomOutfit.cs` (pure composition)
- Modify: `Chaos.Client/Systems/TownImportPreviewLook.cs` (OverHair case)
- Modify: `Chaos.Client/Collections/WorldState.cs` (`public static FittingRoom FittingRoom { get; } = new();` — NOT cleared by `WorldState.Clear()`)
- Test: `Tests/Chaos.Client.Tests/FittingRoomOutfitTests.cs`, `Tests/Chaos.Client.Tests/FittingRoomViewModelTests.cs`, extend `Tests/Chaos.Client.Tests/TownImportPreviewLookTests.cs`

**Acceptance Criteria:**
- [ ] `FittingRoomOutfit.Compose(base, overcoat, head, accessories)` rules:
  - Overcoat layer → `OvercoatSprite/OvercoatColor`; OvercoatAsArmor → `ArmorSprite`, overcoat cleared.
  - Head layer → `HeadSprite`, `HeadColor` = base head color if `KeepsHairColor` else the chosen color.
  - OverHair → first empty accessory slot (1,2,3) after tried-on accessories are placed, else slot 3; head unchanged.
  - Tried-on accessories go into slots 1..n in order, replacing whatever the base had there; slot 1 taken → `GuildCloakDesignId = 0`.
  - `HidesHead` / `HidesBoots` of any worn piece clear head / boots (sprite 0, color Default).
- [ ] `FittingRoom` view model: `ApplyOpen(args, liveLook)`; `Tab` (Overcoats, Hats, Accessories, Wearing); `Search` (case-insensitive substring of name); paging (`PAGE_SIZE = 30`, `Page`, `PageCount`, `VisibleLooks`, `StepPage`); gender filter (look gender == player gender or Unisex); `Toggle(look)` wears/removes (overcoat and head: one each; accessories: up to 3, a 4th replaces the oldest; an OverHair look counts as the head piece); `SetColor(look, color)`; `Remove(look)`; `Stripped` toggle (base = live look with overcoat 0, accessories 0, guild cloak 0, head = hair style/color from the packet); `Hovered` look previewed on top; `Effective` appearance; `Worn` list; `Clear()`.
- [ ] Changing tab or search resets the page to 0.
- [ ] `TownImportPreviewLook.Apply` handles OverHair like the server (first empty accessory slot, else 3; head unchanged).
- [ ] Client test project builds and the new tests pass.

**Verify:** client test command with `--treenode-filter "/*/*/FittingRoomOutfitTests/*"`, `.../FittingRoomViewModelTests/*`, `.../TownImportPreviewLookTests/*` → pass.

**Steps:**

- [ ] **Step 1:** Read `ViewModel/BeautyShop.cs`, `Systems/TownImportPreviewLook.cs`, and their tests for style.
- [ ] **Step 2:** Write failing tests for each rule above (one test per rule; include: 4th accessory replaces the oldest; OverHair with two tried-on accessories lands in slot 3; OverHair with none lands in slot 1 when base slot 1 empty; Stripped uses packet hair; gender filter hides the other gender but keeps Unisex; search + tab reset page).
- [ ] **Step 3:** Implement. Keep `FittingRoomOutfit` pure (no `WorldState` access). The view model stores worn looks plus their chosen colors; the color of a worn look defaults to `look.Look.Color`.
- [ ] **Step 4:** Plumbing: `HandleFittingRoomDisplay(ref ServerPacket pkt)` → `Client.Deserialize<FittingRoomDisplayArgs>` → `OnFittingRoomDisplay?.Invoke(args)`, registered as `PacketHandlers[(byte)ServerOpCode.FittingRoomDisplay] = HandleFittingRoomDisplay;` next to the beauty shop.
- [ ] **Step 5:** Build and run tests.

```json:metadata
{"files": ["Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client.Networking/ConnectionManager.cs", "Chaos.Client/ViewModel/FittingRoom.cs", "Chaos.Client/Systems/FittingRoomOutfit.cs", "Chaos.Client/Systems/TownImportPreviewLook.cs", "Chaos.Client/Collections/WorldState.cs", "Tests/Chaos.Client.Tests/FittingRoomOutfitTests.cs", "Tests/Chaos.Client.Tests/FittingRoomViewModelTests.cs", "Tests/Chaos.Client.Tests/TownImportPreviewLookTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\fitting-room-server -- --no-ansi --treenode-filter \"/*/*/FittingRoomOutfitTests/*\"", "acceptanceCriteria": ["compose rules", "view model tabs/search/paging/gender/toggle/strip/hover", "town import OverHair", "tests pass"], "modelTier": "standard", "repo": "fitting-room-client"}
```

---

### Task 6: Client window — standing at the mirror

**Goal:** `FittingRoomControl` shows the catalog and the character standing in front of a mirror (back view below, front view in the mirror), wired to the packet and closed on map change.

**Files (client worktree):**
- Create: `Chaos.Client/Controls/World/Popups/FittingRoom/FittingRoomControl.cs`
- Create: `Chaos.Client/Controls/World/Popups/FittingRoom/MirrorStage.cs` (the left-column scene)
- Create: `Chaos.Client/Controls/World/Popups/FittingRoom/WornRow.cs` (one Wearing-tab row: icon, name, "x")
- Modify: `Chaos.Client/Screens/WorldScreen.cs` (field, creation `{ ZIndex = 2 }`, `Root.AddChild`, unsubscribe on dispose), `WorldScreen.Wiring.cs` (`WireFittingRoom`), `WorldScreen.ServerHandlers.cs` (`HandleFittingRoomDisplay`), `WorldScreen.Map.cs` (hide on new map next to `Poker.Hide()`)
- Modify: `CLAUDE.md` (Popups list: `FittingRoom/` entry)

**Acceptance Criteria:**
- [ ] Window 600×470, `FramedDialogPanelBase("_nsett", false)`, centered, `Y = 5`, `UsesControlStack = true`, close button and Escape hide it (no packet).
- [ ] Left column (x 20, width 230): `MirrorStage` draws an arched gold-framed glass panel (recessed texture from `DialogFrame.BuildRecessedTexture` plus a 2-px gold border; arch = rounded top corners drawn with filled rows) showing the **front** view at 2× (crop around the body center like `GuildCloakPreview` if it would not fit), and below/overlapping the mirror's bottom edge the **back** view at 2×.
- [ ] Facing pairs: back view Up ↔ mirror Down; back Down ↔ mirror Up; back Left ↔ mirror Left; back Right ↔ mirror Right. Turn ◀ ▶ buttons rotate the back view; the mirror follows by that table.
- [ ] Walk-in: on Show, the back view starts 90 px lower and walks up (Up-facing walk frames 1-4 of `WALK_ANIM`, 114 ms per frame) to its spot over ~1 s, while the mirror figure walks with Down-facing walk frames; then both idle (`IDLE_ANIM`). Clicking anywhere in the stage skips the walk.
- [ ] Right column: four tab buttons (Overcoats, Hats, Accessories, Wearing); a `CustomTextBox` search (`HintText = "Search"`) filtered live by comparing `Text` in `Update`; a `ThumbnailGrid<FittingRoomLook>` 6×5 cells of 32 px using `UiRenderer.Instance!.GetItemIcon(look.PanelSprite, colorForTile)` (shared textures, never disposed); a caption label with the hovered or last-picked look's name; a `SwatchGrid` of the selected worn look's colors (every `DisplayColor` when `Dyeable`), hidden when there is nothing to dye; a page label `Page n / m`.
- [ ] Wearing tab: up to 5 `WornRow`s (overcoat, head, 3 accessories) with icon, name and an "x" button calling `vm.Remove`; an empty state line "You are wearing nothing new.".
- [ ] Footer: "Take everything off" / "Put my clothes back" toggle and the close button.
- [ ] Hovering a tile previews it (vm hover), leaving clears it; clicking toggles it.
- [ ] Textures from `AislingRenderer.Render` are owned by the stage and disposed on refresh and on Hide; renders are cached per (appearance, facing, frame).
- [ ] `HandleFittingRoomDisplay`: Open → `WorldState.FittingRoom.ApplyOpen(args, WorldState.GetPlayerEntity()?.Appearance)`, `FittingRoom.Show()`; Close → `FittingRoom.Hide()`, `WorldState.FittingRoom.Clear()`.
- [ ] `HandleMapInfo` new-map branch hides it and clears the view model.
- [ ] Client solution builds with 0 errors and no new warnings in the new files.

**Verify:** `dotnet build Chaos.Client.slnx -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\fitting-room-server` → 0 errors; full client test run shows no new failures.

**Steps:**

- [ ] **Step 1:** Read `BeautyShopControl.cs`, `MirrorPreview.cs`, `Pages/HairPage.cs`, `ThumbnailGrid.cs`, `SwatchGrid.cs`, `GuildCloakPreview.cs`, `TownImportAdminControl.cs` (search box) and `WorldScreen.Wiring.cs:301`/`ServerHandlers.cs:1787`.
- [ ] **Step 2:** Build `MirrorStage` (UIElement with `SetLook(AislingAppearance)`, `Turn(int)`, `StartWalkIn()`, `Release()`, `Update`, `Draw`).
- [ ] **Step 3:** Build `FittingRoomControl` + `WornRow`, all state read from `WorldState.FittingRoom`.
- [ ] **Step 4:** Wire into WorldScreen (creation, handler, map hide, dispose).
- [ ] **Step 5:** Update `CLAUDE.md` Popups list with a `FittingRoom/` entry in the existing style.
- [ ] **Step 6:** Build the solution and run the full client tests.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/FittingRoom/FittingRoomControl.cs", "Chaos.Client/Controls/World/Popups/FittingRoom/MirrorStage.cs", "Chaos.Client/Controls/World/Popups/FittingRoom/WornRow.cs", "Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/Screens/WorldScreen.Wiring.cs", "Chaos.Client/Screens/WorldScreen.ServerHandlers.cs", "Chaos.Client/Screens/WorldScreen.Map.cs", "CLAUDE.md"], "verifyCommand": "dotnet build Chaos.Client.slnx -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\fitting-room-server", "acceptanceCriteria": ["window frame and close", "mirror stage front+back 2x with facing table", "walk-in", "tabs/search/grid/swatches/paging", "wearing tab rows", "strip toggle", "hover preview", "texture ownership", "packet wiring + map-change hide", "builds clean"], "modelTier": "frontier", "repo": "fitting-room-client"}
```

---

### Task 7: Commit the full implementation

**Goal:** One commit per repo on branch `fitting-room`, then merged `--no-ff` into the shared checkouts' main/master (not pushed).

**Files:** every file changed by Tasks 1-6, plus the spec and plan (`git add -f docs/superpowers/...` in the client).

**Acceptance Criteria:**
- [ ] Server commit on `fitting-room` in the server worktree; merged into `GitHub\Chaos.Client\Chaos-Server` master with `git merge --no-ff fitting-room`.
- [ ] Unora commit in the Unora worktree (stage explicit paths; no `Custom Client Mods/**/obj`); merged into `GitHub\Unora` main.
- [ ] Client commit records the submodule at the merged server master commit (`git update-index --cacheinfo 160000,<sha>,Chaos-Server`); merged into `GitHub\Chaos.Client` main.
- [ ] No other session's files staged; `appsettings.json` and `.run/` untouched; nothing pushed.

**Verify:** `git -C <each shared checkout> log --oneline -3` shows the merge commit; `git status` in each shared checkout shows only the pre-existing unrelated changes.

```json:metadata
{"files": [], "verifyCommand": "git log --oneline -3", "acceptanceCriteria": ["three commits on fitting-room", "merged --no-ff locally", "client submodule at merged server sha", "nothing pushed"], "modelTier": "standard"}
```
