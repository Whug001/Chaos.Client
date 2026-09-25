# Guild Cloak — Stretching a Design onto Each Frame

Date: 2026-09-25. Client only (Chaos.Client). Follows `2026-09-25-guild-cloak-design.md` ("Mapping one pixel")
and the deferred-work notes in `2026-09-25-guild-cloak-stretch-notes.md`.

Mockups and the throwaway spike from the design session are in
`Chaos.Client/.superpowers/brainstorm/142375-1790333274/` (not committed). `content/compare-rules-v2.html` steps
through all 288 cloak frames (144 per body) with today's rule and the two candidates. The chosen rule is
`map_anchored(centred=True)` in `spike/mapping.py`.

## Summary

A guild cloak design is painted onto every animation frame by fitting the frame to one of three canvases (back,
lining, collar). Today's fit stretches each frame by its bounding rows and each row's span. Three things look wrong
with a real design (the Richards draft: a red back with a white-outlined gold heart; a lining with a gold middle,
red sides and a wide white hem):

1. **The heart jumps sideways** on walk frame 3 (and its copies in sheets c, d and e), where the cape billows out.
2. **The hem shrinks to a few pixels** on front walk frames 7–9, where the lining swings out as a pointed flap. The
   standing frame 6 shows the full hem, so it flashes once per step.
3. **The inside of the cape shows the outside design.** On sheet c frames 3, 15 and 20 the cape flies out behind a
   side-on body and we see its inside. The game paints them from the back canvas, so the heart shows.

This change fixes all three with a new fitting rule and one new canvas choice. Nothing changes outside the client.

## Goals

- The heart (or any emblem) stays over the middle of the back when the cape billows.
- Whatever is painted along the bottom of a canvas runs along the frame's real lower edge, even when that edge is
  diagonal.
- Inside-of-cape frames show the lining design.
- On the three reference frames the editor shows, the painted result is exactly the canvas, cell for cell.

## Non-goals

These stay deferred (see the stretch notes):

- Side-on poses such as sheet c frame 9 still squeeze the whole back into a strip.
- The editor's mirror still flips around the canvas box's middle, not the cloak's middle.
- Per-frame fitting data made offline. It stays the fallback if frames still look wrong after this change.

## The old art as a guide

Sprite 328 (a copy of Black Cape 127) draws a two-glyph rune on the upper back and a hem line along the bottom
edge, both in the dye colors (palette slots 98–103). Their position on each frame is the original artist's idea of
where the middle of the back and the bottom edge are. On walk frame 3 the rune sits in the upper middle of the
cape. Today's rule puts the heart out on the billow, 3.2 pixels left of the rune's centre (frame 0: 0.2).

The cape art also reuses frames heavily. Walk frame 3 appears again as sheet c frames 1 and 10, sheet d frame 2
and sheet e frames 1 and 4. The male body has about 75 different large cloak shapes, not 144 frames per layer.

## The rule

### Terms

For a grid G (a frame layer, or a canvas's reference frame), all in the grid's own coordinates:

- `Top`, `Bottom`: the first and last filled row. `Row(y) = (First, Last)`: the first and last filled pixel of
  row y.
- `ColumnBottom(x)`: the last filled row of column x.
- **Rune pixel:** a pixel whose palette index is 98–103 and that sits at least 3 rows above its column's bottom
  (`ColumnBottom(x) − y ≥ 3`). Dye pixels closer to the bottom are the hem.
- `RuneCentre`: the mean x of the rune pixels, or none when there are fewer than 10.

Only the first width × height bytes of a frame are read. DALib returns most retail frames with the later frames'
bytes after their own (see `GuildCloakGrid.HasPixels`).

### Mapping one pixel

For pixel (x, y) of target layer T, with canvas C. `centre` is true only when painting the Back part and T has a
`RuneCentre`.

1. **Across.** With `(f, l) = T.Row(y)` and `c = T.RuneCentre`:
   - if `centre` and `f < c < l`: `u = 0.5·(x − f)/(c − f)` when `x ≤ c`, else `u = 0.5 + 0.5·(x − c)/(l − c)`;
   - otherwise `u = (x − f)/(l − f)`, or 0.5 when `f = l`.
   - For a draw the renderer will flip, `u = 1 − u`.
2. **Column at a canvas row**, `XAt(Y, u)`: with `(f, l) = C.Row(Y)` and `c = C.RuneCentre`, use the same split when
   `centre` holds and `f < c < l`, otherwise `f + u·(l − f)`.
3. **Down.** `fh = T.Bottom − T.Top`, `ch = C.Bottom − C.Top`.
   - `v = (y − T.Top)/fh`, or 0.5 when `fh = 0`.
   - `d = T.ColumnBottom(x) − y` (how far the pixel sits above the bottom of its own column).
   - Top reading: `yTop = NearestRow(round(C.Top + v·ch))`.
   - `xTop = NearestColumn(round(XAt(yTop, u)))`.
   - Bottom reading: `yBottom = C.ColumnBottom(xTop) − d·ch/max(1, fh)`.
   - `Y = NearestRow(round((1 − v)·yTop + v·yBottom))`.
4. **Cell.** `X = round(XAt(Y, u))`, clamped to `C.Row(Y)`. The color number then comes from
   `GuildCloakPainter.ColorNumberAt`, as today (an unpainted cell takes the nearest painted cell in its row).

`NearestRow` clamps to `[Top, Bottom]` and searches outward for a filled row, trying the row below before the row
above, as `CellAt` does today. `NearestColumn` picks the filled column closest to x, the left one on a tie.
`round` is `MathF.Round` (midpoint to even), which matches the spike.

At the top of a layer only the top reading counts, so the collar line stays where it is today. At the bottom only
the bottom reading counts, so every column's lowest pixel reads the canvas's bottom edge. On a reference frame both
readings give the pixel's own cell.

### Inside-of-cape frames

`AislingRenderer.RenderGuildCloakLayer` paints a main (`c`) layer from the lining canvas when all of these hold:

- the frame doesn't face the viewer (its lining `g` layer is empty — today's test);
- it has more than 150 filled pixels, so it isn't a collar-only frame;
- none of its pixels is a dye pixel (98–103), so it carries no rune and no hem.

On sprite 328 this picks exactly sheet c frames 3, 15 and 20 on both bodies (checked on all 288 frames). A fixed
list of frame numbers would tie the client to this sprite's frame order; the rule doesn't. Lining never uses rune
centring.

## Measured on the spike

Heart centre minus rune centre, in pixels (x, y). Frame 0 is the target.

| Frame (male) | Today | New |
|---|---|---|
| 01:0 | +0.2, +3.1 | +0.2, +3.1 |
| 01:2 | −0.3, +3.6 | +0.6, +1.7 |
| 01:3 | −3.2, +4.2 | −0.1, +1.0 |
| c:26 | −3.0, +3.8 | −0.2, +1.4 |

Female frames behave the same way (01:3: −3.6 today, −0.3 new).

Sideways, the new rule keeps the heart within about 1 pixel of where frame 0 has it on every back frame measured,
both bodies (today: up to 4.2 pixels off). Vertically, on 01:3 it sits 2.1 pixels higher relative to the rune than
on frame 0 (today: 1.1 lower). That comes from lining up the lower half with the column bottoms, which rise on the
billowing frames. Walk frame 4 and sheet c frame 18 are 3–4 pixels off vertically under both rules, so the rune is
not a precise vertical target. The result looked right on the stepper page.

Cells that differ from the canvas when a reference frame is painted onto its own grid: 0 for the back (753 cells),
lining (714) and collar (71). A per-column rule tried in the spike changed 9, 39 and 5, and left speckles in the
hem, so it was dropped.

## Code changes

All in Chaos.Client:

| File | Change |
|---|---|
| `Chaos.Client.Rendering/GuildCloakGrid.cs` | Records `ColumnBottom` per column and `RuneCentre` while reading a frame. New cell lookup for a target pixel replaces `CellAt(u, v)`, which only the painter and one test use. Static check for an inside-of-cape frame that reads only width × height bytes. Owns the dye-slot range 98–103. |
| `Chaos.Client.Rendering/GuildCloakPainter.cs` | `Paint` uses the new lookup and takes a flag that turns rune centring on (Back part only). Uses the grid's dye-slot range. Shading is unchanged. |
| `Chaos.Client.Rendering/AislingRenderer.cs` | `RenderGuildCloakLayer` picks `GuildCloakPart.Lining` for inside-of-cape frames and passes the centring flag. |
| `Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs` | Tests below. |

The editor preview and the review window draw through `AislingRenderer.Render`, so they get the change too.

Unchanged: the server, the design format and packets, the editor canvases, the hidden-lining fill, the layer image
cache (in memory only, no new key), and both bodies mapping from the male reference frames. The change reaches
players with the next client patch. It needs no new `.dat` files and no `CLIENT_VERSION` bump of its own.

## Testing

Unit tests use small made-up frames, as the current painter tests do (the test project has no game files). Each
new test is written first and seen failing on today's rule where the old rule differs.

1. A bell-shaped back with a rune and a hem, painted onto its own grid, returns every cell exactly. So does a
   lining-shaped frame with no rune.
2. On a flap whose bottom edge runs diagonally, every column's bottom pixel gets the canvas's hem color.
3. On a frame whose rune sits off to one side, the rune's centre column reads the canvas's centre stripe. A flipped
   draw mirrors around the rune, not around the box's middle.
4. Dye pixels within 3 rows of their column's bottom don't count as rune. Fewer than 10 rune pixels gives no centre.
5. The inside-of-cape check: true for more than 150 pixels with no dye pixel; false with one dye pixel; false at 150
   pixels or fewer; bytes past width × height are ignored.
6. The existing painter tests keep passing. `CellAt_skips_empty_rows_to_the_nearest_filled_row` becomes the same
   check against the new lookup.

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`. The whole suite must
pass.

## Hand check (after merge)

1. Open the cloak editor with a design that has an emblem and a hem. In the preview walking away, the emblem stays
   over the middle of the back on every step.
2. Walking toward the viewer, the hem runs along the flap's whole lower edge on every step, as on the standing
   frame.
3. Wear the cloak and use a skill whose animation uses sheet c. The cape's inside shows the lining, not the back.
4. Repeat on a female character.

## Docs

`2026-09-25-guild-cloak-stretch-notes.md` gets a status line saying the heart, hem and inside frames are fixed by
this design, an updated "How the stretch works today", and keeps the side-on squeeze and the mirror as deferred.
