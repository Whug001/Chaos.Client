# Guild Cloak — Stretching Notes (deferred work)

Date: 2026-09-25. Status: **partly done.** Problems 1 and 2 below, and the inside-of-cape frames, are fixed by
`2026-09-25-guild-cloak-stretch-design.md`. Problem 4 was fixed by the guild cloak audit. Problem 3 is still open.

These notes record how a guild cloak design gets stretched onto each animation frame, where that goes wrong, and
the approaches worth trying. The original design is `2026-09-25-guild-cloak-design.md` ("Mapping one pixel").

## What was already fixed (2026-09-25)

These looked like stretching problems but were bugs. They are fixed in the client, so don't chase them again:

- **Back frames painted from the collar canvas.** A frame counts as facing the viewer when its lining (`g`) layer
  has pixels. DALib 0.7.0 reads most retail frames past their own box: their frame table's end address is junk, so
  `EpfView[i]` reads from the frame's start to the table. An empty 1×1 lining frame then carries the next frames'
  pixels. Walk frames 0, 2, 3 and 4 were treated as facing the viewer. Their back was painted from the 18×8 collar
  canvas, which showed as red and navy blocks, and the walk "blinked" because frame 1 read cleanly. About 60 of
  the 144 cloak frames per body were affected. `GuildCloakGrid.HasPixels` now reads only width × height bytes.
  **Anything else that scans an `EpfFrame.Data` must stop at width × height too.**
- **Lining cells under the collar.** The collar covers 71 lining cells on the Front canvas, so they could never be
  painted. They show at the top of the lining on walk frames 7 and 9. The editor now fills each one from the
  nearest visible lining cell below it (`GuildCloakReferences.FillHiddenLining`), after every change and on load.
- The editor and review previews can now pause and step through the walk (Prev, Pause/Play, Next).

## How the stretch works today

For each pixel of a frame layer, `GuildCloakGrid.CellFor` picks a canvas cell. The full rule, with formulas, is in
`2026-09-25-guild-cloak-stretch-design.md`.

1. Across: the pixel's place in its row. On back frames each row is split at the old rune's centre (dye pixels at
   least 3 rows above the bottom of their column), so the rune's centre reads the canvas's rune centre.
2. Down: at the top of the layer, the pixel's height as a fraction of the layer (the old rule). Lower down, more and
   more by its distance above the bottom of its own column, so a hem follows the real lower edge.

A main-layer frame with no lining layer, more than 150 pixels and no dye pixel shows the inside of the cape. It is
painted from the lining canvas: sheet c frames 3, 15 and 20 on both bodies.

## What still goes wrong

Frame numbers are the male sheets of sprite 328 (a copy of 127). The female sheets behave the same way.

1. **Fixed** by the stretch design (the heart now stays within about 1 pixel of the rune sideways). **Paint slides
   sideways when the cape swings out.** On walk frames 2–4 (walking away) the cape billows to one side. Those rows
   get wider, so the middle of the row moves toward the billow. An emblem jumps up and to one side on frame 3.
2. **Fixed** by the stretch design. **The lining's hem shrinks to a point on the front walk.** On walk frames 7–9
   (walking toward the viewer) the lining is a flap swinging out behind the body. Its lower edge runs diagonally
   and ends in a thin point: on frame 7, rows 24–32 hold only 25 down to 2 pixels. The canvas's bottom rows (the
   hem) land on that point, so a hem painted along the bottom shows as a few pixels at the tip. The rest of the
   flap's lower edge gets the middle of the canvas. A gold hem shows on frame 6 and almost vanishes on 7–9, so it
   flashes once per cycle.
3. **Side-on poses squeeze the whole back into a strip.** Some attack and spell frames (for example sheet `c`
   frame 9, arms raised) show the cape nearly edge-on. The full back canvas, emblem included, is squeezed into a
   few columns.
4. **Fixed** by the guild cloak audit (client `06af73d`): the mirror now flips a cell within its row's outline.
   **Mirror flips around the canvas box, not the cloak.** `GuildCloakEditorModel.MirrorX` used
   `width − 1 − x`. The back canvas is 27 wide, but the cape flares to the left at the bottom. On the shoulders
   the cloak's middle is about column 15, not 13. A dab at column 19 should copy to column 11. It copies to
   column 7 instead, which is off the cloak there, so the copy is lost.
5. Both bodies map from the male reference frames. Female frames are 1–3 pixels narrower. This looked fine in
   testing, but recheck it with any new approach. Rechecked with the new rule on the 2026-09-25 stepper page: fine.

## Approaches to try

Listed from cheapest to most control. They can be combined.

- **Measure `u` from the body's centre line.** Take each frame's spine column from the body sprite frame, and
  measure `u` outward from it, not from the row's midpoint. An emblem would stay on the spine when the cape
  swings. This addresses problem 1, and partly problem 3.
- **Use the old rune and hem as anchor points.** Sprite 127's art draws a rune on the back and a hem along the
  bottom in the dye slots (palette 98–103). Their position in each frame is the artist's own idea of where the
  middle of the back and the bottom edge are. The mapping for each frame could line up the canvas's centre and hem
  with those pixels. This addresses problems 1 and 2.
- **Measure `v` per column.** Take `v` between each column's own top and bottom pixel, not the layer's top and
  bottom rows. Every column's lowest pixel then gets the canvas's hem, so a hem follows a diagonal lower edge.
  This addresses problem 2. Check that it doesn't bend a horizontal stripe on frames whose edges are uneven.
- **Measure `u` per connected run within a row,** not across gaps between separate pieces of cloth.
- **Mirror around the cloak's own middle.** Give each canvas a fixed mirror column, such as the middle of the
  upper back, instead of the box's middle. This addresses problem 4, and it's small enough to do on its own.
- **Per-frame fitting data made offline.** A tool writes, for every cloak pixel of every frame, which canvas cell
  it reads. It starts from the automatic rule, and bad frames get fixed by hand. The client ships it as a data
  file. This gives the most control, but it's the most work: 144 frames × 2 bodies × 2 layers. It also has to ship
  with a client patch.

## How to judge an approach

Throwaway Python scripts from the 2026-09-25 session are in
`Chaos.Client/.superpowers/brainstorm/102799-1790327029/spike/`. That folder is not committed. They need Unora's
`Tools/Accessories/acclib` and the local client folder (`Documents/Unora/Unora Files`).

| Script | What it shows |
|---|---|
| `cloakdiag.py` | Each canvas painted with a 3×3 grid of test colors, mapped onto the walk frames |
| `cloakwalk.py` | The editor preview's walk in all four facings, with a sample design |
| `cloakbeforeafter.py` | A saved design (from `Unora/Data/LocalStorage/GuildCloakState.json`) on every frame of every sheet |
| `cloakhidden.py` | Where the lining cells under the collar show on the front walk |
| `epfcheck/` | A tiny .NET program that shows DALib 0.7.0 reading past a frame's box |

A new approach should be tried in these scripts first. Change `mapped()` and `Canvas.sample()`, then compare the
3×3 test pattern and a real design across sheets `01`, `c` and `e`. The mockups from that session, including a
page that steps through every frame, are in the same folder's `content/`.

The stretch design's spike is in `Chaos.Client/.superpowers/brainstorm/142375-1790333274/` (not committed):
`spike/mapping.py` holds the candidate rules (the chosen one is `map_anchored(centred=True)`), `spike/parity.py`
checks the client against it, and `content/compare-rules-v2.html` steps through every frame.
