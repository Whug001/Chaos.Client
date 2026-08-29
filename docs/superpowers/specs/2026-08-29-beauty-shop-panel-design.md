# Mileth Beauty Shop — Josephine's Mirror

Date: 2026-08-29

## Summary

Replace Josephine's five one-at-a-time `ShowItems` dialogs with a single client popup — a
"mirror" — where the player sees their whole sprite and changes gender, hairstyle, hair dye, skin
color and face shape together, watching a running gold total, and pays once. The panel rides the
same rail as the slot machines, the Gilded Spindle and the poker table: one push packet each way,
args + converters in the shared `Chaos.Networking` project, a `FramedDialogPanelBase` control on
the client.

The server stays authoritative for everything that costs gold: it sends the catalog and prices on
open, and re-validates and charges exactly once on apply. The client never sends a price.

## Why this shape

Today every category is its own `ShowItems` menu that charges the moment an item is clicked, with
no preview. A player who wants a new look buys blind, five times, and discovers halfway through
that the hairstyle doesn't suit the new face. The client already owns everything needed to draw
any combination locally (`AislingRenderer.Render` composites a full Aisling for the profile
paperdoll and the character-creation preview), so browsing costs no network traffic at all. The
only thing the client cannot know is what is for sale and at what price — so that comes from the
server on open.

## Goals

- One panel: full sprite preview + all five appearance controls + running total + single Apply.
- Browsing is free and local; nothing is charged before Apply.
- Apply is all-or-nothing: one gold deduction for exactly the categories that changed.
- Pricing stays data-driven exactly as today (item-template `buyCost`, Master gender rule).
- The Riona tutorial hook on Josephine's greeting keeps working unchanged.

## Non-goals

- No equipment/armor dye, no accessory changes — Josephine never sold those.
- No walk-cycle animation in the preview (front idle + four facings is enough).
- No new hairstyles, faces or colors; the catalog is whatever the templates and enums say.
- No changes to other NPCs. Only Josephine's dialogs use the five appearance scripts (verified by
  grep over `Data/Configuration/Templates/Dialogs`), so retiring them affects nobody else.

## Player flow

1. Click Josephine → `josephine_initial` (DialogMenu). Text unchanged. `JosephineRewardScript`
   stays on it, so the Riona tutorial reward and the "Riona sent you?" reply are untouched.
2. Its **only** option is *"Show me the mirror."* → dialog `josephine_mirror` carrying script key
   `beautyShopOpen`. That script's `OnDisplaying` closes the dialog and calls
   `source.Client.SendBeautyShopOpen(...)`.
3. The panel opens. The player browses; the preview and total update locally.
4. **Apply** → one `Apply` packet. Server validates, charges, applies, refreshes, sends **Close**
   and an orange-bar message. On rejection the panel stays open and shows the reason.
5. **Close** / Esc discards silently (nothing was charged).

Deleted: `josephine_buyShop`, `josephine_changeHairDye`, `josephine_changeBodyDye`,
`josephine_changeFaceShape`, `josephine_changeGender`, `josephine_swapGenderConfirm` dialog
templates; `HairstyleScript`, `HairDyeScript`, `BodyDyeScript`, `FaceShapeScript`,
`SwapGenderScript` dialog scripts (the gear-swap logic is extracted first — see Server). The
`male_/female_hairstyle_N`, `*faceshape` and `hairDyeContainer` item templates stay: they are the
catalog's data source.

## Pricing

| Category   | Price source                                                    | Today |
| ---------- | --------------------------------------------------------------- | ----- |
| Hairstyle  | that style's item template `buyCost`                            | 1,000 |
| Hair dye   | `hairDyeContainer` template `buyCost`                           | 1,000 |
| Skin color | `hairDyeContainer` template `buyCost` (as `BodyDyeScript` does) | 1,000 |
| Face shape | that face's item template `buyCost`                             | 50,000 |
| Gender     | 50,000; 1,000,000 if `UserStatSheet.Master`                     | same |

A category is charged only if its applied value differs from the player's current value. Total =
sum of changed categories. Total 0 → rejected as `NothingChanged`.

## Protocol

Opcodes, next free in each direction (`Chaos.Networking.Abstractions/Definitions/Enums.cs`):
`ServerOpCode.BeautyShopDisplay = 121`, `ClientOpCode.BeautyShopInteraction = 119`. Same
renumber-when-upstream-claims-it note as the poker/wheel opcodes.

Shape enums in `Chaos.DarkAges/Definitions/Enums.cs` beside the poker ones:

```
BeautyShopDisplayType     : byte { Open, Rejected, Close }
BeautyShopInteractionType : byte { Apply, Close }
BeautyShopRejectReason    : byte { NothingChanged, InsufficientGold, InvalidSelection,
                                   GenderSwapUnavailable, NotNearShop }
```

### Server → client `BeautyShopDisplayArgs`

`Open`:

| Field                | Type                                   | Notes |
| -------------------- | -------------------------------------- | ----- |
| Gender               | byte (`Gender`)                        | current |
| HairStyle            | ushort                                 | current |
| HairColor            | byte (`DisplayColor`)                  | current |
| BodyColor            | byte (`BodyColor`)                     | current |
| FaceSprite           | byte                                   | current |
| Gold                 | int                                    | player's gold at open (display only) |
| GenderPrice          | int                                    | already resolved for Master |
| HairDyePrice         | int                                    | flat |
| BodyDyePrice         | int                                    | flat |
| MaleHairstyles       | count byte + (ushort id, int price)[]  | from templates; 96 is absent |
| FemaleHairstyles     | count byte + (ushort id, int price)[]  | |
| Faces                | count byte + (byte sprite, string8 name, int price, bool femaleOnly)[] | |
| HairColors           | count byte + byte[]                    | every `DisplayColor`, Default first, rest sorted by name (as today) |
| BodyColors           | count byte + byte[]                    | every `BodyColor`, sorted by name |

`Rejected`: `Reason` byte. `Close`: no payload. Counts go through the poker converter's
`CountToByte` guard (throw, never truncate).

### Client → server `BeautyShopInteractionArgs`

`{ Type; Gender; HairStyle (ushort); HairColor; BodyColor; FaceSprite }`. For `Close` the
appearance fields are ignored. **No prices, no ids of any other kind.**

## Server

**`BeautyShopCatalog`** (`Chaos/Services/BeautyShop/`) — singleton built at startup from the item
template cache and the enums: the two hairstyle lists (ids + prices, in template order), the face
list (sprite, display name, price, `femaleOnly` = template key `restingbitchfaceshape`, matching
`FaceShapeScript` and the face-18 rule in `SwapGenderScript`), the two flat dye prices, and color
lists. Exposes `IsValid(gender, hairStyle)`, `IsValid(gender, faceSprite)`, `PriceOf(...)`, and
`BuildOpenArgs(Aisling)`. A `BeautyShopCatalogValidationService` (hosted, like
`PokerCatalogValidationService`) fails startup if a referenced template is missing.

**`GenderSwapService`** — the gear/inventory/bank swap and `ApplyGenderChange` lifted verbatim out
of `SwapGenderScript`, including `GenderMasterEquipmentMap`. `TrySwap(Aisling, Gender to)` returns
false when the class has no map (today's "couldn't find your class-specific equipment" case).

**`BeautyShopScript`** (merchant script on Josephine's template; `showdialog` stays for the
greeting). `HandleApply(Aisling, BeautyShopInteractionArgs)`:

1. Validate: hairstyle and face against the catalog **for the requested gender**; colors are
   enum-defined. Any failure → `Rejected(InvalidSelection)`.
2. Diff against the Aisling's current values → set of changed categories. Empty →
   `Rejected(NothingChanged)`.
3. If gender changed and `GenderSwapService` has no map for the class → 
   `Rejected(GenderSwapUnavailable)`.
4. Total = sum of changed categories. `TryTakeGold(total)` **once**; failure →
   `Rejected(InsufficientGold)` with nothing applied.
5. Apply in order: gender (via `GenderSwapService`), hairstyle, hair color, body color, face.
   `Refresh(true)`; `Display()`.
6. One log line per changed category with `Topics.Entities.Aisling` + `Topics.Entities.Gold`
   (same wording as the old scripts), then `SendBeautyShopClose()` and an orange-bar
   "Josephine works her magic. Enjoy your new look!".

**`WorldServer.OnBeautyShopInteraction`** mirrors `OnPokerTableInteraction`: deserialize →
`ExecuteHandler` → `Connected` guard → resolve the nearest merchant carrying `BeautyShopScript`
within dialog range (never from a packet field) → `Rejected(NotNearShop)` if none, otherwise
dispatch `Apply`/`Close` to the script. Registered in `IndexHandlers` beside the poker entry.

**`beautyShopOpen` dialog script** — `OnDisplaying`: `Subject.Close(source)` then
`source.Client.SendBeautyShopOpen(catalog.BuildOpenArgs(source))`.

## Client

No packet code: `Chaos.Client.Networking` references `Chaos.Networking` from the submodule.

- `Delegates.cs`: `BeautyShopDisplayHandler(BeautyShopDisplayArgs)`. `ConnectionManager`: event,
  `PacketHandlers[(byte)ServerOpCode.BeautyShopDisplay]`, four-line handler, `SendBeautyShopApply`
  / `SendBeautyShopClose` via `SendIfWorld`.
- `ViewModel/BeautyShop.cs` (+ `WorldState.BeautyShop`, cleared on logout): holds the open
  payload, the *current* values, the *selected* values, and derives `ChangedCategories`, `Total`,
  `CanAfford`. `SetGender` re-resolves the hairstyle list (keep the same id if the other list has
  it, else the first entry) and drops a female-only face to the Default face when switching to
  male. This class is pure and unit-tested.
- `Controls/World/Popups/Beauty/BeautyShopControl.cs : FramedDialogPanelBase("_nsett", false)`,
  `UsesControlStack = true`, ctor takes `AislingRenderer`.

Layout (one frame, poker-style constants):

```
┌──────────────────────────────────────────────────────────┐
│  Josephine's Mirror                                      │
│ ┌────────────┐  Gender      [ Male ] [Female]   50,000  │
│ │            │  Hairstyle   ◀  17  ▶            +1,000 ● │
│ │   sprite   │  Hair dye    ◀ Scarlet ▶ ■       +1,000 ● │
│ │            │  Skin        ◀ Tan ▶ ■                   │
│ │            │  Face        ◀ Beauty ▶          50,000   │
│ └────────────┘                                           │
│  ◀  ▶  [x] Show gear                                     │
│  Total: 52,000 gold        You have: 61,230              │
│                 [Reset]  [Apply]  [Close]                │
└──────────────────────────────────────────────────────────┘
```

- Preview: `AislingRenderer.Render(in appearance, frame, IDLE_ANIM, flip)` on a cached
  `Texture2D`, re-rendered only when the selection, facing or gear toggle changes (the
  `PortraitView` caveat: the renderer's cache is per-entity, so the panel owns and disposes its
  texture). ◀ ▶ cycle four facings; default front. **Show gear** off → `AislingAppearance` with
  only body/hair/face/colors set; on → the player's own `WorldState` appearance with the five
  fields overridden.
- Each row: ◀ ▶ arrows (wrap), the value, a color swatch for the two dye rows, the price, and a
  "changed" marker. Gender is a two-button toggle.
- Footer: total vs. gold (total in red when unaffordable). **Apply** enabled only when
  `Total > 0 && CanAfford`. **Reset** returns every row to current. **Close**/Esc → `Close`
  packet, no confirm.
- Apply with a gender change first shows the stock `OkPopupMessageControl` (parented to the panel
  like poker's leave confirm): "This will reshape your Master and Grandmaster gear — equipped,
  banked and in inventory — for N gold. Continue?"
- `Rejected` → red status line under the total with the reason text; panel stays open. The
  "You have" figure is whatever the `Open` packet said; it is not refreshed on rejection.
- `WorldScreen`: construct + `Root.AddChild` + teardown; `WorldScreen.Wiring.cs`
  `WireBeautyShop()`; `WorldScreen.ServerHandlers.cs` apply-to-viewmodel-then-repaint switch
  (`Open` → `ApplyOpen` + `Show()`; `Rejected` → `OnRejected`; `Close` → `Hide()` + `Clear()`).

## Error handling

- Player logs out or walks out of range with the panel open: `Close` is never required for
  correctness — nothing is charged until Apply, and Apply re-resolves the merchant by proximity.
- Stale open payload (gold changed since open): the server charges from live gold; the client's
  "You have" is advisory.
- Malformed packet (undefined enum byte): converter throws, as the poker converters do.
- Catalog inconsistency (template deleted): startup validation fails loudly.

## Testing

Server (`Tests/Chaos.Tests`, TUnit, run with `dotnet run --project ... --`):
- Converter round-trips for both args records, including empty lists and the count guard.
- `BeautyShopCatalog`: male list omits 96; face 18 is female-only; prices come from templates;
  Master flag doubles into the 1M gender price.
- `BeautyShopScript.HandleApply`: nothing changed → `NothingChanged`, no gold moved; insufficient
  gold → `InsufficientGold` and **no field changed**; valid multi-category → gold taken exactly
  once for the sum, all fields applied; hairstyle valid only for the other gender →
  `InvalidSelection`; class without gear map + gender change → `GenderSwapUnavailable`.
- `GenderSwapService`: behaviour parity with the deleted script (equipped/inventory/bank swaps,
  mismatch unequip, face 18 → 1 on male).

Client (there is no client test project today — the poker work was verified by walkthrough only;
this adds a minimal TUnit `Tests/Chaos.Client.Tests` project referencing `Chaos.Client` so the
pure view model can be tested; the control itself is not unit-tested):
- `BeautyShop` view model: `Total`/`ChangedCategories` arithmetic; gender switch remaps the
  hairstyle and face as specified; `Reset` restores current.
- Manual walkthrough (`docs/superpowers/plans/…-walkthrough.md`): preview matches the world sprite
  after Apply; gear toggle; four facings; Esc discards; rejection text; Riona tutorial still pays
  out.

## Files

Server: `Chaos.Networking.Abstractions/Definitions/Enums.cs`, `Chaos.DarkAges/Definitions/Enums.cs`,
`Chaos.Networking/Entities/{Server,Client}/BeautyShop*Args.cs`,
`Chaos.Networking/Converters/{Server,Client}/BeautyShop*Converter.cs`,
`Chaos/Networking/{Abstractions/IChaosWorldClient,ChaosWorldClient}.cs`,
`Chaos/Services/Servers/WorldServer.cs`, `Chaos/Services/BeautyShop/*`,
`Chaos/Scripting/MerchantScripts/BeautyShopScript.cs`,
`Chaos/Scripting/DialogScripts/Temuair/Mileth/BeautyShopOpenScript.cs`,
`Chaos/Extensions/ServiceCollectionExtensions.cs`, tests.

Data (Unora): `Templates/Dialogs/Temauir/mileth/josephine/{josephine_initial,josephine_mirror}.json`,
`Templates/Merchants/Temauir/mileth/josephine.json`; six dialog templates deleted.

Client: `Chaos.Client.Networking/{ConnectionManager,Definitions/Delegates}.cs`,
`Chaos.Client/ViewModel/BeautyShop.cs`, `Chaos.Client/Collections/WorldState.cs`,
`Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs`,
`Chaos.Client/Screens/WorldScreen{,.Wiring,.ServerHandlers}.cs`, tests.
