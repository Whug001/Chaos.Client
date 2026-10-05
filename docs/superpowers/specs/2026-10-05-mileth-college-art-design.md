# Mileth College part 3: The Art canvas

Date: 2026-10-05
Parent design: `2026-10-04-mileth-college-design.md`. Part 1 spec: `2026-10-05-mileth-college-core-design.md`.
Part 2 spec: `2026-10-05-mileth-college-entries-design.md` (all in this folder). Read them first; this spec only
adds what part 3 needs.
Status: approved section by section in brainstorming on 2026-10-05.

## Scope

Part 3 delivers:

- a pixel canvas window in the client, for Art entries, Art hand-ins and pictures inside writing pieces;
- Art pieces on the server: a drawing block, its rules and storage;
- Art entries at the Registrar, and Art activity classes at the lectern;
- drawings in the reader, and a picture grid on the gallery's Art tab;
- a **Draw** button in the writing window.

Music stays closed ("Opens soon") until part 4.

## Decisions made in brainstorming

| Topic | Decision |
|---|---|
| Canvas size | 96 x 72 pixels, shown at zoom 4 (384 x 288 on screen) |
| Colours | Each piece has its own 32 colours. It starts from a default palette; any swatch can be changed with a colour picker. |
| Tools | Pen, Eraser, Undo/Redo, grid on/off, plus Fill, Pick (eyedropper), Line, brush sizes 1-3, Mirror |
| Art entry | A title, one drawing and an artist's note of up to 300 characters |
| Drawing in writing pieces | Yes, in part 3: a **Draw** button in the writing window |
| Importing pictures into the canvas | No. Every Art piece is drawn in the game. |
| Art activity class | The same as writing: prompt, hand-ins, Show to class |
| Gallery Art tab | A picture grid, 8 per page, framed in the award colour |
| Window layout | Everything on one page: title above the canvas, note below it |
| Storage | Canvas data (palette plus a swatch number per pixel) in the piece. Drawings inside writing pieces become normal pictures. |

Mockups from brainstorming: `.superpowers/brainstorm/1191197-1791226779/content/` in this repo
(`canvas-size.html`, `colours.html`, `tools.html`, `gallery-art.html`, `canvas-window.html`).

## Repositories and branches

| Repo | Worktree | Branch | Starts from |
|---|---|---|---|
| `Chaos-Server` | `worktrees/college-art-server` | `feat/college-art` | server master dbb1adaf2 (parts 1 and 2, not pushed) |
| `Unora` | `worktrees/college-art-unora`, only if a change turns out to be needed | `feat/college-art` | Unora main 019ab74d7 |
| `Chaos.Client` | `worktrees/college-art-client` | `feat/college-art` | client main 9ae89705 |

The client worktree builds `Chaos.Client/Chaos.Client.csproj` with `UnoraServerPath` pointing at the server
worktree, as in part 2. The `.slnx` fails in a worktree.

Registrar and lectern dialogs are server scripts, so Unora is expected to need no changes. The plan confirms this.

## Art pieces on the server

### The drawing block

`PieceBlockType` gains `Drawing`, and the message enum `CollegeBlockKind` gains the same. A drawing block holds:

- **Palette:** exactly 32 colours, RGB.
- **Pixels:** exactly 6,912 bytes (96 x 72, row by row from the top left). Each byte is a swatch number from 0 to 31.

On disk, inside the existing piece file:

```json
{ "type": "Drawing", "palette": ["#eec39a", "#000000", "..."], "pixels": "<base64 of 6912 bytes>" }
```

In messages, the palette is 96 raw bytes (R, G, B per swatch) and the pixels are 6,912 raw bytes. A whole Art piece
is about 7.5 KB, so it fits in one message.

Shared constants go in `CollegeProtocol`:

| Constant | Value |
|---|---|
| `DRAWING_WIDTH` | 96 |
| `DRAWING_HEIGHT` | 72 |
| `DRAWING_COLOURS` | 32 |
| `DRAWING_ZOOM` | 4 |
| `MAX_ART_NOTE_CHARS` | 300 |
| `MIN_ENTRY_DRAWN_PIXELS` | 346 (5% of the canvas) |
| `MIN_HAND_IN_DRAWN_PIXELS` | 70 (1% of the canvas) |

### Art piece shape

A piece with subject Art has kind `Drawing`. The kind follows from the subject, as today. It holds:

- a title (rules as in part 2: 1-40 characters for Submit, may be empty in a draft);
- exactly one `Drawing` block;
- at most one `Text` block: the artist's note, up to 300 characters. Line breaks are allowed.

Heading and picture blocks are refused in Art pieces. Drawing blocks are refused in every other subject.

### Background colour

Swatch 0 is the background. A new canvas is filled with swatch 0, and the eraser paints swatch 0.

The default palette is the 32-colour DawnBringer set, with its parchment colour moved to swatch 0:

```
#eec39a #222034 #45283c #663931 #8f563b #df7126 #d9a066 #000000
#fbf236 #99e550 #6abe30 #37946e #4b692f #524b24 #323c39 #3f3f74
#306082 #5b6ee1 #639bff #5fcde4 #cbdbfc #ffffff #9badb7 #847e87
#696a6a #595652 #76428a #ac3232 #d95763 #d77bba #8f974a #8a6f30
```

The default palette lives on the client only. The server accepts any 32 colours.

### Rules (`PieceRules`, pure, unit tested)

| Rule | Check result when broken |
|---|---|
| Palette has exactly 32 colours | new `BadDrawing` |
| Pixels are exactly 6,912 bytes, every value below 32 | `BadDrawing` |
| Art piece has exactly one drawing block | `BadDrawing` |
| Art note at most 300 characters | `TooMuchText` |
| Heading or picture block in an Art piece | new `WrongBlock` |
| Drawing block outside Art | `WrongBlock` |
| Submit: title present, and at least 346 pixels differ from the most-used swatch | `NoTitle` / `TooShort` |
| Hand-in: at least 70 pixels differ from the most-used swatch | `TooShort` |

"Drawn pixels" means pixels whose swatch is not the most-used swatch in the drawing. This stops a one-colour canvas,
or one colour plus a dot, being entered for marks, whichever swatch it is filled with. If two swatches tie for most
used, either one counts as most used; the count is the same.

The server never decodes images. A modified client could still send a drawing made elsewhere. Removal by the
Director or an admin is the safeguard, as for pictures in part 2.

### Messages

No new opcodes. `CLIENT_VERSION` 772 -> 773. Update the test that pins it (`GuildEmblemPacketConverterTests`).

- **`OpenWriter`** is reused. When its subject is Art, the client opens the canvas window instead of the writing
  window, in `Draft` or `HandIn` mode.
- **`SaveDraft`, `Submit`, `HandIn`** carry Art pieces through the same codec, with the new drawing block.
- **New `CollegeActionType.DrawingFetch = 12`**: entry id.
- **New `CollegeDisplayType.Drawing = 8`**: entry id, palette (96 bytes), pixels (6,912 bytes).

The server answers `DrawingFetch` only for an entry that is `Claimed`, has subject Art and is not hidden: the same
right as opening it from the gallery. Anything else gets no reply. The fetch reads the piece file; it changes no state.

### Opening Art

- `CollegeOptions.OpenSubjects` gains Art.
- `CollegeOptions.IsWritingSubject` is used today to gate activity classes and hand-ins. Those gates move to a new
  `CollegeOptions.HasActivity(subject)`, true for the four writing subjects and Art. `IsWritingSubject` stays for the
  places that really mean writing.
- `CollegeService.HandIn` accepts a hand-in when the running class is an Activity class in a subject that has
  activities, the sender is in its room, and the piece's subject matches the class. The refusal text becomes "You can
  hand in only during an activity class."
- `CollegeText`: an Art activity class ends its world message with " (drawing class)", the same way a writing class
  ends with " (writing class)".

## The client

### Drawing model (`ViewModel/College/PixelDrawing`, pure, unit tested)

Holds the palette (32 colours) and the pixels (6,912 swatch numbers), and performs every edit:

- **Pen** and **Eraser** with a square brush of size 1, 2 or 3. The eraser paints swatch 0. A size-2 brush covers the
  pixel and the pixels to its right, below, and below right. A size-3 brush is centred on the pixel. Brush pixels
  outside the canvas are skipped.
- **Fill:** fills the area connected to the clicked pixel that has the same swatch. Only up, down, left and right
  count as connected; pixels that touch only at corners are not.
- **Line:** a straight line (Bresenham) from the press to the release point, drawn with the current brush. While
  dragging, the window shows a preview without changing the drawing.
- **Pick:** makes the clicked pixel's swatch the current one, then switches back to the Pen.
- **Mirror:** when on, every Pen, Eraser, Line and Fill edit is also made at the mirrored position across the
  vertical centre line: column x mirrors to column 95 - x. A Fill also fills from the mirrored pixel.
- **Undo / Redo:** one step per stroke (press to release), Fill, Line or swatch colour change. Up to 50 steps; the
  oldest step is dropped after that. Any new edit clears Redo.
- **Swatch colour change:** pixels hold swatch numbers, so changing a swatch's colour recolours every pixel that uses it.
- **Counts:** `DrawnPixels` (pixels not on the most-used swatch), so the window can warn before Submit or Hand in.

### Canvas window (`Controls/World/Popups/College/ArtCanvasControl`)

560 x 464 (the frame's 47-px bottom border leaves no room for a 3-line note under the canvas at 440). It follows the `canvas-window.html` mockup, option A:

- **Top:** caption `Art: "{title}"`, then a **Title** box.
- **Left:** a tool column: Pen, Erase, Fill, Line, Pick; then **Size 1** (each click cycles 1, 2, 3); then
  **Mirror** and **Grid** (toggles, lit when on); then **Undo** and **Redo**.
- **Middle:** the canvas, `ArtCanvas`, at zoom 4. Grid lines are 1-pixel dark lines between canvas pixels, shown
  while Grid is on, which it is by default. While Mirror is on, a light line marks the centre.
- **Right:** the current colour with its hex code, then the 32 swatches in two columns of 16. Click a swatch to make
  it current. Double-click it to open the colour picker from the Stage Lighting window (`StageColorPicker`) beside the
  swatches. **OK** applies the colour as one undo step; **Cancel** leaves it unchanged.
- **Below the canvas:** a **Note** box (3 lines, 300 characters) and a counter "84/300".
- **Footer:** **Save**, **Submit (3 marks)** or **Submit (free entry)**, a status line, and **OK**.

Mouse and keys:

- The left button uses the current tool. The right button always erases with the current brush size.
- Between two mouse positions in a stroke, every pixel on the straight line between them is painted, as in Pumpkin
  Carving, so a fast drag leaves no gaps.
- A stroke ends on mouse-up, or when the button is found released (the window lost focus mid-stroke).
- Ctrl+Z undoes and Ctrl+Y redoes, while no text box has focus.

Modes:

| Mode | Opened by | Differences |
|---|---|---|
| `Draft` | `OpenWriter` (Draft) for Art | As above. Closing with changes saves the draft, as the writing window does. |
| `HandIn` | `OpenWriter` (HandIn) for Art | The caption shows the class prompt. Buttons are **Hand in** and **Close**. Closing before handing in asks "Close without handing in?". |
| `Picture` | **Draw** or **Edit** in the writing window | No title, no note. Buttons are **Insert** and **Cancel**. Nothing is sent to the server by the canvas itself. |

Before sending Submit or Hand in, the window checks `DrawnPixels` against the minimum and shows the same message the
server would ("Draw more before entering it."), so the player isn't charged a round trip for an obvious refusal. The
server still checks.

### Showing drawings

- **`DrawingTextures`:** builds a `Texture2D` from a palette and pixels at a given zoom (1 for thumbnails, 4 for the
  reader). Textures are owned by the control that asked for them and disposed with it.
- **`PieceView`** gains a drawing block. It shows the drawing at 384 x 288, then the note below it as a text block.
  This covers every reader context: Judge, Verdict, Gallery, HandIn and Shown.
- **Gallery Art tab (`CollegeGalleryControl`):** a picture grid of 4 columns and 2 rows, 8 per page. Each cell shows
  the drawing at 96 x 72 inside a 2-pixel frame in its award colour (Clave bronze, Village silver, Kingdom gold,
  Aisling pale blue, as in part 2), with the title and author below. Click selects a cell; double-click or **Read**
  opens the reader. The other five tabs keep their text rows.
- **Thumbnails:** for each visible cell without a cached drawing, the client sends `DrawingFetch`. A cell shows
  "Loading..." until the `Drawing` reply arrives. Drawings are kept in memory by entry id until logout. Fetches for a
  page are sent once; turning back to a page doesn't fetch again.

### Drawing inside writing pieces

- The writing window's footer gets **Draw**. To make room, **Insert picture** is shortened to **Picture**. The exact
  widths are settled in the plan; the counter keeps its place at the right.
- **Draw** opens the canvas in `Picture` mode, on a blank canvas with the default palette. It is refused, with the
  existing message, when the piece already has 5 pictures.
- **Insert** turns the drawing into a 384 x 288 PNG, each canvas pixel a 4 x 4 block of its colour. It then follows
  the part 2 picture path: hash, `PictureCheck`, upload, cache. It counts as one of the 5 pictures and against the
  upload caps.
- **Edit:** a picture block whose picture is a canvas drawing gets an **Edit** button next to its remove button. A
  picture counts as a canvas drawing when it is exactly 384 x 288, every 4 x 4 block is one colour, and it has at most
  32 colours. **Edit** reopens the canvas in `Picture` mode with that drawing. **Insert** then replaces the picture
  block's hash with the new picture; the old one is left to the server's 24-hour cleanup.
- When a picture is reopened, its palette is rebuilt: the colours found, in order of first appearance (reading row by
  row), then default-palette colours not already present until there are 32.
- This works in both the writing window's Draft and HandIn modes.

## Content

### Registrar (`CollegeRegistrarScript`)

- The main-menu option "Write an entry" becomes **"Make an entry"**. Picking Art sends `OpenWriter` for Art, which
  opens the canvas. Music still shows "Opens soon".
- **My entries**, **Judge entries** and **Class hand-ins** need no change for Art; the reader shows drawings.
- The Activity format is offered for Art bookings. The rule that hides Activity stays for Music.

### Lectern (`CollegeLecternScript`)

- In the Chamber of Creation, Art Activity classes can be booked and started. Music Activity stays hidden.
- Starting an Art Activity class asks for a prompt of up to 120 characters, as writing classes do.
- During an Art Activity class, students get **Draw a hand-in** (opens the canvas in HandIn mode), and the Teacher gets
  **Hand-ins**. Writing classes keep **Write a hand-in**.
- One hand-in per student per class; handing in again replaces it, as in part 2.

### No map or art changes

The Art gallery stand, the Art emblems (259-262) and the Honorary Artist shop items already exist. No .dat files ship
with this part.

Unora changes four dialog texts so they no longer say 'write' (Registrar subject choice, booking format, About page 2, the lectern prompt question).

### Messages (orange bar, 45 characters at most)

| When | Text |
|---|---|
| Too little drawn to enter | `Draw more before entering it.` |
| Too little drawn to hand in | `Draw more before handing it in.` |
| Draft saved, submitted, handed in | Same texts as part 2 |

Dialog limits apply: option text 35 characters, dialog text 360, no em dashes.

## Testing

**Server unit tests** in `Tests/Chaos.Tests/College/`:

- `PieceRulesTests` additions:
  - the palette must have exactly 32 colours (31 and 33 refused);
  - there must be exactly 6,912 pixels (6,911 and 6,913 refused), and a value of 32 is refused;
  - the note is limited to 300 characters;
  - heading and picture blocks are refused in Art; drawing blocks are refused outside Art;
  - Submit at 345 and 346 drawn pixels; hand-in at 69 and 70;
  - drawn pixels counted against the most-used swatch, including a canvas filled with a swatch other than 0.
- `CollegeServiceTests` additions:
  - an Art piece survives a save and reload, palette and pixels unchanged;
  - an Art draft can be submitted end to end;
  - Art hand-ins are accepted only in a running Art Activity class, in its room;
  - a writing piece handed in to an Art class is refused, and the reverse.
- `DrawingFetch`: answered for a visible `Claimed` Art entry; no reply for hidden, `Voting`, non-Art or unknown
  entries.
- Packet round-trip tests for the drawing block, `DrawingFetch` and `Drawing`, and the `CLIENT_VERSION` 773 pin.

**Client unit tests** in `Tests/Chaos.Client.Tests`:

- `PixelDrawing`:
  - each brush size at all four canvas edges;
  - Fill does not cross corner-only touches;
  - Mirror with Pen, Line and Fill, including on the centre columns;
  - Pick switches back to the Pen;
  - the 50-step undo limit, and Redo cleared by a new edit;
  - undoing a swatch colour change;
  - `DrawnPixels`.
- Drawing pictures:
  - a drawing exported to PNG and recognised again gives back the same pixels and colours;
  - a picture that isn't 384 x 288, has a mixed 4 x 4 block, or has 33 colours isn't recognised;
  - the rebuilt palette keeps the found colours in first-appearance order, then fills from the default palette.

The full server suite stays at its known baseline (`OnItemDroppedOn` stackable, and `GiveAbility` if it still fails
on master).

**In-game checklist** for the user:

1. Draw an Art draft using every tool, with Mirror on and off. Change a swatch, undo it, redo it. Save, close and
   reopen.
2. Submit it. Check that the marks drop.
3. As a judge, open it: the drawing and note show. Vote.
4. `/college closevoting <id>`, post the verdict, claim it. Open the Art stand: the grid shows it framed in its award
   colour. View it.
5. Run an Art Activity class: set a prompt, hand in from two characters, show one to the class.
6. In a History draft, press **Draw**, draw and insert. Press **Edit** on it, change it, insert again. Save, close and
   reopen.

## Shipping

Server + client 773 + Unora (dialog text). The client goes out in a launcher patch before the
server restart. Like parts 1 and 2, nothing is pushed until the user says so.

## Open follow-ups carried from parts 1 and 2

- The file-server `AislingRenamer` must update `CollegeState.json` before a rename. Part 3 adds nothing new to rename:
  drawings live in piece files, which hold no names.
