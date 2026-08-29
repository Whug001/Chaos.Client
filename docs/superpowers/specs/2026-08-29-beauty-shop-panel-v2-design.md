# Josephine's Mirror v2 — Preview-first Panel

Date: 2026-08-29. Supersedes the panel section of `2026-08-29-beauty-shop-panel-design.md`; the
protocol, catalog, checkout and data from that spec are unchanged.

## Summary

Rebuild the client panel around the sprite: a large framed preview with zoom and facings, visual
pickers (head-crop thumbnails for hairstyle / skin / face, a colour-swatch grid for hair dye),
hover-to-preview, a segmented gender selector, a purchase summary that owns all the money text, an
"unsaved changes" indicator, Discard Changes, and a Randomize button. **Client only** — no packet,
server or data change.

## Why

The v1 panel proved the plumbing but reads like a configuration screen: a small sprite floating in
empty space, `◀ Style 5 ▶` rows that force players to cycle blind through ~100 hairstyles, prices
competing with the controls, and selection state carried by text. The client renders any look
locally already; the thumbnails are the same composite the preview uses, cropped to the head.

## Goals

- The sprite is the biggest thing on the panel and updates instantly on hover or selection.
- Every visual choice is picked by looking, not by counting clicks.
- Selection state is obvious without reading (gold border, ✓).
- Money lives in one place — the purchase summary.
- Fits the client's 640×480 virtual resolution.

## Non-goals

- No walk-cycle animation in the preview (four facings suffice; a walk cycle multiplies render work
  per change).
- No hairstyle names (the catalog carries sprite ids only; names would be a server/data change).
- No changes to what is for sale, prices, validation or the Apply/Rejected/Close flow.

## Layout

Panel 600×440, centred, same `FramedDialogPanelBase` frame (47px ornate bottom border stays clear).

```
┌ JOSEPHINE'S MIRROR ──────────────────────────────────────────┐
│ Customize your appearance                 ● Unsaved changes   │
├── PREVIEW ───────────────┬── APPEARANCE ──────────────────────┤
│ ┌──────────────────────┐ │ Gender    ┃ MALE ┃ FEMALE ┃        │
│ │   recessed pedestal  │ │ Hairstyle                 12 / 101 │
│ │      sprite 2×       │ │  ◀ [👤][👤][👤][👤][👤][👤] ▶       │
│ └──────────────────────┘ │ Hair dye                            │
│  ◀  ▶      1× / 2×       │  ●●●●●●●●●●  10 × 8 swatches        │
│  ☐ Show gear             │ Skin       ◀ [👤][👤][👤][👤][👤] ▶  │
│  🎲 Randomize            │ Face       ◀ [👤][👤][👤][👤][👤][👤] ▶│
├── PURCHASE SUMMARY ──────┴──────────────────────────────────────┤
│ Hairstyle 1,000 · Hair dye 1,000 · Face 50,000  TOTAL 52,000    │
│ You have 7,653,744            [ Discard Changes ]  [  APPLY  ]  │
└────────────────────────────────────────────────────────────────┘
```

Columns: preview 200px wide (x 20–220), appearance 360px (x 236–596). Summary band 72px above the
frame border. Exact constants are the plan's; the proportions above are the contract.

## Components (all in `Chaos.Client`)

### View model additions (`ViewModel/BeautyShop.cs`, unit-tested)

- **Hover state**: `HoveredHairStyle`, `HoveredHairColor`, `HoveredBodyColor`, `HoveredFaceSprite`
  (nullable, at most one non-null at a time via `SetHover(category, value)` / `ClearHover()`).
  **Effective** values (`EffectiveHairStyle` etc.) = hovered ?? selected — the preview renders the
  effective look; `Total`/`*Changed`/`CanApply` keep using the *selected* values. A `HoverTotal`
  gives the total as if the hovered item were chosen (for the summary's dimmed "if chosen" line).
- **Paging**: `PageOf(list, sprite, pageSize)` helper and `HairstylePage`, `SkinPage`, `FacePage`
  ints with `StepHairstylePage(±1)` etc. (wrap); selecting an item off-page moves the page to it.
- **`HasUnsavedChanges`** = any `*Changed`.
- **`Randomize()`**: uniformly random valid hairstyle, hair colour, skin and face for the current
  gender (`AvailableFaces`), gender untouched; deterministic under an injected `Random` in tests.

### Preview panel (left)

- `PreviewView` draws the effective appearance onto a recessed pedestal texture
  (`DialogFrame.BuildRecessedTexture`) via `DrawTextureFitted` into a 1× or 2× destination rect
  (the UI pass uses point sampling, so 2× stays crisp); 2× is the default; a `1× / 2×`
  `CustomButton` toggles.
- ◀ ▶ facings and **Show gear** as in v1; a "PREVIEW" caption above; **🎲 Randomize** below.
- Texture cache keyed by (effective appearance, facing) exactly as v1; hover changes re-render like
  a selection would.

### Thumbnail strips (`ThumbnailStrip` — reusable for hairstyle, skin, face)

- N cells (6 for hairstyle and face, 5 for skin) of 36×36 showing a **head crop** of the current
  effective look with only that cell's item swapped in; ◀ ▶ page buttons; a `n / N` label.
- Crop = the same head-and-shoulders window poker's `PortraitView` uses (`FindContentTop` alpha scan,
  30×30 source, scaled into the cell).
- Each cell renders on demand and caches its texture keyed by (item, gender, hair colour, skin,
  face); the strip drops its cache when any dependency changes (so a dye change re-renders the
  six visible hairstyle heads) and on hide. Only visible cells ever render.
- Cell states: normal; hovered (silver 1px outline, sets hover in the VM); selected (gold 2px
  outline + ✓ badge top-right). Click selects.

### Dye swatch grid (`SwatchGrid`)

- 10 × 8 grid of 14px squares for every `HairColors` entry (80 today), colour sampled from the dye
  table as in v1 (`Default` → lavender). Same hover/selected treatment as thumbnails.

### Gender selector

- One 2-segment control: MALE | FEMALE; selected segment gold fill + dark text, other segment dim.
  Clicking the unselected segment calls `SetGender` (remaps hairstyle/face as v1).

### Purchase summary (bottom band)

- One line per **changed** category: `Hairstyle 1,000`, `Hair dye 1,000`, `Skin 1,000`,
  `Face 50,000`, `Gender 50,000` (face omitted when the forced-reset waiver applies), joined by
  ` · `; then `TOTAL n` in gold, red when unaffordable; `You have g`.
- While hovering: a dimmed second line `if chosen: TOTAL n` from `HoverTotal`.
- Buttons: **Discard Changes** (enabled only when `HasUnsavedChanges`; calls `Reset`) and **APPLY**
  (enabled == `CanApply`; gender confirm as v1). Status/rejection line stays red, under the total.

### Header

- Title, subtitle "Customize your appearance", and `● Unsaved changes` (gold) at the right when
  `HasUnsavedChanges`.

## Interaction rules

- Hover previews; click selects; leaving clears hover (preview and summary revert).
- Every selection change goes through the v1 `Select` funnel (hide confirm, clear status, refresh).
- Esc / Close discard silently as v1 (no gold at stake); `Hide` releases every cached texture.
- Randomize is a selection change (unsaved indicator lights, summary updates).

## Testing

- View model: hover/effective values, `HoverTotal`, paging (wrap, auto-page-to-selection),
  `HasUnsavedChanges`, `Randomize` (validity for gender, gender unchanged, seeded determinism).
- Control: build only; the v1 walkthrough gains rows for hover-preview, thumbnails, swatches,
  zoom, randomize, discard, and the unsaved indicator.

## Files

`Chaos.Client/ViewModel/BeautyShop.cs`, `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs`,
`Chaos.Client/Controls/World/Popups/Beauty/{BeautyShopControl,ThumbnailStrip,SwatchGrid,GenderSelector}.cs`,
`docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md` (new rows). No other file changes.
