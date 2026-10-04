# Josephine's Mirror — Map Backdrops

Date: 2026-10-04. Builds on `2026-10-04-beauty-shop-guide-design.md` (the five-page mirror, merged to client
main `14080c36`, Review heading fix `28ea4d3b`). Client only — no server, packet, Unora data or
`CLIENT_VERSION` change.

## Summary

Let players see their new look standing in real places. The mirror's preview gets a row of six small
pictures under it: **Plain** (today's dark box) and five backdrops cut from real maps — **Mileth**,
**West Woodlands**, **Lynith Beach**, **Frozen Cave** and **Crypt**. Clicking a picture puts that place
behind the figure; pointing at one tries it on. The Review page's NOW and NEW figures use the same
backdrop.

The backdrops are **pictures built into the client**, rendered once from the real maps by a small
generator tool. They do not depend on which map files a player has downloaded.

## Decisions taken in brainstorming

- Places: Plain + Mileth, West Woodlands, Lynith Beach, Frozen Cave, Crypt.
- Picker: a row of six small pictures under the preview (mockup option A), not a named `< >` switcher.
- Backdrops ship as embedded PNGs made by a generator from the real maps (approach 1), not drawn live
  from the player's map files. Reason: a player only has a map file after visiting that map.

## Decisions made for you — check these when you review

- The Review page has **no picker of its own**; NOW and NEW both use whatever backdrop was chosen on
  pages 1–4.
- The choice is **remembered while the game runs** (it survives closing and reopening the mirror) and
  starts as Plain after a restart. It is not saved to settings.
- **Hover tries a backdrop on** without choosing it, like the hair and face pickers; moving off returns
  to the chosen one.
- Backdrops are daylight stills: no animated tiles, palette cycling, weather or darkness.

## What it looks like

Mockups and real crops from this brainstorm are in
`.superpowers/brainstorm/787942-1791139302/content/` (gitignored): `backdrop-picker.html` and
`bd_<place>_{1x,2x,rv}.png`, `th_<place>.png`.

- At 2x (the default zoom) about 2 × 5 tiles show; the figure covers much of the box. At 1x about
  4 × 9 tiles show. The existing 2x/1x button scales the backdrop with the figure.
- The figure is always drawn on top of the whole backdrop. Standing spots are chosen with open ground at
  the player's feet so nothing in the picture looks like it should be in front of them.

## Places and standing spots

Map files come from `DA_PATH` (`C:\Users\Michael\Documents\Unora\Unora Files\maps\lod<id>.map`).
Sizes are from Unora `Data/Configuration/Templates/Maps/<id>.json`.

| Backdrop | Map id | Size (w × h) | Standing tile (provisional) | Mockup spot |
|---|---|---|---|---|
| Mileth | 397 | 60 × 60 | (23, 38) | south of the town fountain |
| West Woodlands | 97 | 50 × 50 | (15, 34) | beside a cabin and pine |
| Lynith Beach | 336 | 20 × 20 | (4, 4) | sand by the palms |
| Frozen Cave | 443 | 20 × 30 | (5, 15) | in front of a snowy tree |
| Crypt | 473 (Crypt 1) | 30 × 30 | (7, 13) | in front of a coffin |

The tiles were back-calculated from the mockup crops and may be off by one. The plan includes a step
that renders all five and shows them to you; tiles are adjusted until you are happy. Crypt 1 (473) has
the same look as Deep Crypt (488, 200 × 200) on a map small enough to render quickly.

## Components

### Generator tool (`Tools/MirrorBackdrops/`, new; not in the solution)

- A small .NET 10 console project, like `Unora/Tools/TownHall/MapRender`. It references the `DALib`
  NuGet package (central version 0.7.0). If that version lacks `Graphics.RenderMap(MapFile, DataArchive
  seoDat, DataArchive iaDat, int foregroundPadding)`, reference the local checkout
  `C:\Users\Michael\Documents\GitHub\dalib\DALib\DALib.csproj` the way MapRender does.
- Reads `seo.dat`, `ia.dat` and the five map files from `DA_PATH` (default above).
- For each place: render the whole map with padding 256, find the standing tile's centre with the
  isometric formula — tile top-left = ((h − 1 + x − y) × 28, 256 + (x + y) × 14), centre = +(28, 14) —
  and cut a **260 × 250** image with the tile centre at **(130, 153)**. Fill transparent areas with the
  pedestal colour (24, 22, 30).
- Also write a **24 × 24** thumbnail per place: a 64 × 64 crop with the tile centre at (32, 48),
  downscaled with a smooth filter.
- Output: `Chaos.Client.Rendering/Assets/MirrorBackdrops/<key>.png` and `<key>-thumb.png`, where `<key>`
  is `mileth`, `woodlands`, `beach`, `frozencave`, `crypt`. The places and tiles live in one table at
  the top of the tool's `Program.cs`.
- Run by hand when a backdrop should change; the PNGs are committed.

### Embedded assets (`Chaos.Client.Rendering`)

- `Assets/MirrorBackdrops/*.png` embedded with `LogicalName` `mirrorbackdrop.<file>`, next to the
  existing `MirrorScare` entry in `Chaos.Client.Rendering.csproj`.

### Backdrop list and loader

- `enum MirrorBackdrop { Plain, Mileth, Woodlands, Beach, FrozenCave, Crypt }` with a display name per
  value ("Plain", "Mileth", "Woodlands", "Beach", "Frozen Cave", "Crypt") and a resource key (none for
  Plain). Pure; unit-tested for order, names (ASCII only) and that every non-Plain value has a key.
- `MirrorBackdrops` (owned by the mirror window): loads a place's 260 × 250 source and thumbnail on first
  use with `Texture2D.FromStream`, and returns a **cached texture cut for one box**:
  `Get(MirrorBackdrop place, int width, int height, int scale)`. The cut is made on the CPU: source rect
  left = 130 − width / (2 × scale), top = 125 − height / (2 × scale), size width / scale × height /
  scale, then scaled up by `scale` with nearest-neighbour. This lines the standing tile's centre up with
  where `MirrorPreview` puts the figure's feet (box centre + 28 × scale). `Get` returns null for Plain.
  `Thumbnail(place)` returns the 24 × 24 picture (null for Plain). `Clear()` disposes everything; the
  window calls it on hide.

### Preview (`MirrorPreview`)

- Takes the window's `MirrorBackdrops` loader in its constructor and gains a settable
  `MirrorBackdrop Backdrop` (default Plain).
- `Draw` draws the pedestal, then `loader.Get(Backdrop, Width − 4, Height − 4, scale)` inset **2 px** on
  every side (so the recessed edge still shows), then the figure. `scale` is the one the figure is drawn
  at in that same call, so when a 2x figure falls back to 1x because it does not fit, the backdrop falls
  back with it. `Get` returns null for Plain, and nothing extra is drawn.

### Picker (`ThumbnailGrid` reuse)

- Generalise `ThumbnailGrid<T>` with an optional cell size (default 32) and an option to leave out the
  page arrows and their space. The picker is `ThumbnailGrid<MirrorBackdrop>` with 6 columns × 1 row,
  28 px cells and 4 px gaps (188 px wide), no arrows. The Plain cell shows an empty dark cell. Selected =
  gold double border, hover = silver border, as on the other pages.

### Window (`BeautyShopControl`) layout

- Preview column, below the Show gear / Randomize row (which ends at y 332):
  - caption at y 338: `Backdrop` (gray) and the hovered-or-chosen name (gold);
  - picker at y 350, x 20; it ends at y 378, above the footer total at y 381.
- Shell state: `Backdrop` (chosen) and `HoveredBackdrop` (nullable). Both are UI-only — they do not go
  through the view model or the `Select` funnel, never touch money, and never clear the review status.
  The chosen value is a static so it outlives the window; `Show()` does not reset it. Turning a page
  clears `HoveredBackdrop`.
- The preview shows `HoveredBackdrop ?? Backdrop`; Plain means no backdrop texture. The Review page's
  two figures show the chosen `Backdrop` (there is no hover on that page).
- `Hide()` calls `MirrorBackdrops.Clear()` along with the existing releases.

## Testing

- Unit: `MirrorBackdrop` order, names and keys; the crop maths (source rect for 230 × 250 at 1x and 2x,
  and 260 × 160 at 1x, all inside 260 × 250) as a pure function.
- Build-time: every embedded resource the enum names exists (a test that opens each
  `mirrorbackdrop.<key>.png` and `<key>-thumb.png` stream from the assembly).
- In game (user): walkthrough rows B1–B6 added to `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md`:
  picker shows six cells; clicking switches; hover tries on and reverts; 2x/1x scales the backdrop;
  Review shows the backdrop behind both figures; closing and reopening keeps the choice.

## Files

- Create: `Tools/MirrorBackdrops/{MirrorBackdrops.csproj,Program.cs}`,
  `Chaos.Client.Rendering/Assets/MirrorBackdrops/*.png` (10 files),
  `Chaos.Client/Controls/World/Popups/Beauty/{MirrorBackdrop.cs,MirrorBackdrops.cs}`, tests.
- Modify: `Chaos.Client.Rendering/Chaos.Client.Rendering.csproj`, `Beauty/MirrorPreview.cs`,
  `Beauty/ThumbnailGrid.cs`, `Beauty/BeautyShopControl.cs`, `Beauty/Pages/ReviewPage.cs`,
  the walkthrough, `CLAUDE.md` (one clause in the `Beauty/` entry).

## Non-goals

- No live map rendering, no animation, no night/weather.
- No backdrop picker on the Review page.
- No saving the choice to settings.
- No "where you are now" backdrop (the player is always in the beauty shop when using the mirror).
