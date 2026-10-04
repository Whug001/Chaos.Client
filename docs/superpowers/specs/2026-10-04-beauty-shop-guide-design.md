# Josephine's Mirror v3 — Step-by-step Guide

Date: 2026-10-04. Supersedes the layout in `2026-08-29-beauty-shop-panel-v2-design.md`. The protocol,
catalog, checkout, prices and Apply/Rejected/Close flow from the earlier specs are unchanged.

## Summary

Turn the one-screen mirror into a five-page guide: **Gender → Hair → Skin → Face → Review & pay**.
Each page asks one thing. Arrow buttons at the bottom move between pages and name the page they lead
to. Five dots show where you are and jump to a page when clicked. The last page shows the old look
and the new look side by side with a receipt, and its right-hand button is **APPLY**. **Client only**
— no packet, server or data change.

## Why

The v2 panel puts gender, 6 hairstyle heads, 71 dye swatches, 5 skin heads, 6 faces, the preview
controls and the money on one screen. Players have to page six heads at a time through ~100
hairstyles and 35 faces, and nothing tells them where to start. A guide gives each choice a whole
page, so each page can show far more options at once, and it reads in order like character
creation.

## Decisions taken in brainstorming

- Five pages; hairstyle and hair color share the Hair page.
- Guide style: a page heading and arrows, no tab row. Clicking a dot jumps to that page.
- Mouse only. The keyboard's Left/Right keys do **not** turn pages. Escape still closes the window.
- The Review page shows **NOW** and **NEW** figures side by side, with the receipt under them.
- Implementation: one window (`BeautyShopControl`) as the shell, with one small class per page.

## Layout

Panel stays 600 × 470, centred, `FramedDialogPanelBase` frame, `Y = TOP_MARGIN`. The client font is
one size (6 × 12), so page headings are gold capitals in that font, not a larger font.

### Shell (pages 1–4)

```
┌──────────────────────────────────────────────────────────────┐
│        PREVIEW           │ HAIR   page 2 of 5                 │
│ ┌──────────────────────┐ │ Pick a style, then a color.        │
│ │  recessed pedestal   │ │                                    │
│ │     sprite 2x        │ │   (the page's own content)         │
│ └──────────────────────┘ │                                    │
│ [<][>]            [2x]   │                                    │
│ [ ] Show gear [Randomize]│                                    │
│                                                               │
│                       TOTAL 2,000                             │
│ [ < Gender ]           ● ● ○ ○ ○              [ Skin > ]      │
└──────────────────────────────────────────────────────────────┘
```

- Left column: the v2 preview column unchanged (caption, pedestal, rotate, 2x, Show gear, Randomize).
- Right column (x ≈ 262–580): heading `NAME   page n of 5` (name gold, page count gray), one gray
  instruction line, then the page's content.
- Footer row (kept clear of the 47 px ornate bottom border, as today's buttons are):
  - Left button `< PreviousPage`, hidden on page 1.
  - Five dots, centred. Current dot gold, others dim. Clicking a dot goes to that page.
  - Above the dots: `TOTAL n` (gold, red when unaffordable), or `No changes yet` (gray) when
    `!HasUnsavedChanges`.
  - Right button `NextPage >`. On page 5 it reads `APPLY`.
- Next is always enabled. Each page starts with the player's current choice already selected, so
  pressing Next without touching anything skips the page.

### Page 1 — Gender

- Instruction: "Choose male or female."
- The two character-creation gender buttons (`_ncreate` Male/Female art) drawn at **2x** (88 px),
  with "Male" / "Female" under them and "(current)" under the player's own gender.
- Note in gray: "Changing gender costs *GenderPrice* gold. It also reshapes your Master and
  Grandmaster gear, and may change your hairstyle and face to ones made for the new body. Keep your
  current gender to skip this page."

### Page 2 — Hair

- Instruction: "Pick a style, then a color. Hover to try one on."
- Caption `Hairstyle   37 / 101 - 1,000` with `PAGE 3/7` right-aligned.
- Hairstyle grid: **8 columns × 2 rows = 16 heads per page**, 32 px cells, page arrows on both sides.
  Arrow buttons narrow to 16 px so the grid fits the 318 px column.
- Caption `Hair color   Crimson - 1,000`.
- Swatch grid: every hair color, 10 columns, swatches **15 px** (up from 12) with a 2 px gap.

### Page 3 — Skin

- Instruction: "Choose a skin color."
- Caption `Skin   Tan - 1,000`.
- All 10 skin colors at once: 5 columns × 2 rows of 32 px heads on a 60 px column pitch, each with
  its name under it. No paging.

### Page 4 — Face

- Instruction: "Choose a face. Hover to try one on."
- Caption `Face   Vampire - 50,000`.
- All faces on one page: 8 columns × 5 rows (40 slots; 35 faces today, 34 for males). No page arrows
  while everything fits on one page.

### Page 5 — Review & pay

The shell hides its preview column on this page. The page draws:

- Heading `REVIEW   page 5 of 5`, centred across the panel.
- Two pedestals, 260 × 160, side by side: **NOW** (gray caption) and **NEW** (gold caption), with a
  gold `>` between them. Both draw at 1x (two 2x figures do not fit). Rotate `<` `>` under NOW turns
  both figures; Show gear under NEW applies to both. **Start over** at the right (enabled when
  `HasUnsavedChanges`; calls `Reset`).
- Receipt, one row per category, in the fixed order Gender, Hairstyle, Hair color, Skin, Face:
  - changed: `Hair color   Crimson >   Gold      1,000`
  - hairstyles have no names, so their row shows positions, `12 >   37`, each counted in its own
    gender's list
  - unchanged: `Skin   no change`
  - face reset waived by the gender rule: price reads `free`
  - then `TOTAL n` (gold, red when unaffordable) and `You have g` (gray).
- Status line in red under the receipt, for server refusals (same texts as today's `OnRejected`).
- Footer: `< Face`, dots, `APPLY` (enabled == `CanApply`). A gender change still shows the existing
  reshape confirm before sending.

### Caption prices

Each category caption names the hovered item if the mouse is over one in that category, otherwise
the selected one, followed by what choosing it would cost:

- `current` when it is the player's current look in that category;
- `free` when it costs nothing for another reason (the face waiver);
- otherwise the price, `N0` formatted.

Hairstyles have no names, so their caption shows the position, `37 / 101`.

## Components

### View model (`ViewModel/BeautyShop.cs`)

- Page sizes: `HAIRSTYLE_PAGE_SIZE` 6 → **16**, `FACE_PAGE_SIZE` 6 → **40**, `BODY_COLOR_PAGE_SIZE`
  5 → **10**. Paging, wrap and auto-page-to-selection logic is unchanged.
- Split the private `PriceOf` into per-category costs and expose four of them:
  `CostOfHairStyle(int sprite)`, `CostOfHairColor(DisplayColor)`, `CostOfBodyColor(BodyColor)`,
  `CostOfFace(int sprite)`. Each returns what that choice adds to the total, given the other current
  selections (the face one applies the forced-reset waiver against the selected gender). `Total`
  stays the sum of the gender price and these four for the selected values, so both can never
  disagree.
- Remove `HoverTotal`. The window no longer shows it, and the cost methods replace it.

### Page order (`Controls/World/Popups/Beauty/MirrorPage.cs`, new)

- `enum MirrorPage { Gender, Hair, Skin, Face, Review }` and a static helper with `Title`,
  `Instruction`, `Previous` / `Next` (null at the ends) and `IsLast`. Pure, unit-tested.

### Shell (`BeautyShopControl.cs`, reworked)

- Owns the frame, the preview column, the footer and the current page. Builds the five page panels
  once, shows one at a time, and hides the preview column on Review.
- Keeps today's `Select` / `Hover` funnels, `Show` / `Hide` / Escape behavior, the gender reshape
  confirm, `OnRejected`, and the `Closed` / `ApplyRequested` events. `WorldScreen` wiring does not
  change.
- `Show()` always starts on Gender.
- Removed: the `* Unsaved changes` label, the summary line, the hover-total line, the
  Discard Changes button (now **Start over** on Review) and the bottom-band APPLY (now the footer's
  right button on Review).

### Page panels (`Controls/World/Popups/Beauty/Pages/`, new)

`GenderPage`, `HairPage`, `SkinPage`, `FacePage`, `ReviewPage`. Each is a `UIPanel` sized to its
area. Pages raise selection and hover through callbacks the shell passes in, so every change still
goes through the shell's funnels. Each has a `Refresh()` that repaints from `WorldState.BeautyShop`.

### Shared pieces

- `ThumbnailStrip<T>` becomes `ThumbnailGrid<T>`: columns, rows, column gap, row gap. Page arrows
  hide when there is only one page. The hover re-announce logic on paging is kept.
- `GenderSelector` gets a scale (1 or 2); the mirror uses 2. Hit-testing scales with it.
- `PreviewView` moves out of `BeautyShopControl` into its own file (`MirrorPreview.cs`) and takes its
  pedestal size in the constructor, so the shell (230 × 250) and the Review page (two of 260 × 160)
  share it. The cache-and-dispose rules are unchanged.
- `PageDots` (new): five clickable dots, raises `PageChosen(int)`.
- `SwatchGrid.SWATCH` 12 → 15.

## Interaction rules

- Hover previews; click selects; leaving clears hover. Unchanged.
- Turning a page clears hover and never changes a selection.
- Randomize (pages 1–4) changes hair style, hair color, skin and face, never gender, and stays on the
  current page.
- Escape or the close button closes without charging, from any page. The next open starts on Gender.
- A server refusal (`Rejected`) arrives while the player is on Review; it shows on Review's red line.
- `Hide` releases every cached texture, including both Review figures.

## Testing

- View model (TUnit, `BeautyShopViewModelTests.cs`):
  - Update the paging test fixture for the new page sizes.
  - Replace the `HoverTotal` tests with cost-method tests: each cost is 0 for the current value, the
    list price for another value, `free` (0) for the waived face; and `Total` equals the gender price
    plus the four costs of the selected values across the same cases the old invariant test covered.
- `MirrorPage` helper: order, ends, titles.
- Window: build only, plus new rows in `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md`
  for page turns, dots, the hidden left arrow on page 1, APPLY on page 5, NOW/NEW, caption prices,
  Start over, and reopening on Gender. In-game check by the user.

## Files

- Modify: `Chaos.Client/ViewModel/BeautyShop.cs`, `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs`,
  `Chaos.Client/Controls/World/Popups/Beauty/{BeautyShopControl,GenderSelector,SwatchGrid}.cs`,
  `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md`.
- Rename: `ThumbnailStrip.cs` → `ThumbnailGrid.cs`.
- Create: `Beauty/MirrorPage.cs`, `Beauty/MirrorPreview.cs`, `Beauty/PageDots.cs`,
  `Beauty/Pages/{GenderPage,HairPage,SkinPage,FacePage,ReviewPage}.cs`, a `MirrorPage` test file.
- No server, Unora data or `CLIENT_VERSION` change.

## Non-goals

- No hairstyle names (still a data change).
- No keyboard page turns.
- No change to what is for sale, prices, validation, or what the server does on Apply.
