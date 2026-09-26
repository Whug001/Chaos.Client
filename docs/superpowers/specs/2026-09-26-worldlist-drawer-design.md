# World list drawer redesign (spec 1 of 2)

Date: 2026-09-26
Repos: Unora (art and build tool), Chaos-Server (map continent, packet), Chaos.Client (drawer)

## Summary

The world list drawer switches to the Korean client's drawer art: a grid of 15 px rows, a title
column, a name column and two 11 px icon cells per row. We change that art in two ways. We move 30 px
from the count boxes into the title column, and we run the row lines across the title column so
every title sits in the same row as its name.

The nine filter buttons rotate through faces. For example, Warrior → Berserker → Warlord, and
Country → Temuair → Medenia. To filter this way, the client needs three new values per player. It
needs the player's Medenian class, the continent of the map they are on, and whether they have
ability levels.

This spec leaves the outer icon cell (the emblem cell) empty. Spec 2 (the emblem system) fills it.

## Goals

- The drawer matches the old client UI exactly. All new art is carved in the style of the existing
  art, and nothing is drawn as plain text where the original used art.
- Titles get more room: 28 letters instead of 22.
- Players can filter by continent, by ability levels, and by each of the ten Medenian classes.
- The server and client can be updated in either order without breaking the world list.

## Not in this spec

- Emblems: their ownership, how they're earned, the emblem window, and drawing them in the cell.
  All of this is spec 2. The answers gathered for it are at the end of this file.
- Showing anything new on player profiles.

## Decisions

| Question | Decision |
|---|---|
| Row style | The Korean grid, as shipped: 15 px rows, 17 visible |
| Title column | 30 px wider (168 px, 28 letters). The count boxes shrink to 32 px, which fits 5 digits |
| Grid in the title column | Row lines run through the title column |
| Icon cells | The inner cell is the status icon. The outer cell is the emblem, empty in this spec |
| Button labels | New carved art in the original style. No text drawn by the client |
| Long class names | "Berserker", "Elemental", "Plague Dr", with letters packed 1 px tighter |
| Face order | Temuair face first, then the two Medenian classes in alphabetical order |
| What Temuair / Medenia mean | Where the player is right now, taken from the map's folder |
| How the data travels | A length-prefixed section at the end of the world list packet |

## Art (Unora)

All sizes are at 1× in the 498×303 drawer. Pixel edits are made on palette indexes, so every pixel
keeps an exact color from the original palette.

### Drawer background: `_nusers.spf`

The source is the Korean client's `_nusers.spf` (498×303, from its `setoa.dat`).

1. **Move 30 px into the titles.** Duplicate columns 60–89 (plain panel texture, uniform top and
   bottom frame) so the title column widens by 30 px. Remove columns 440–469, which cut through the
   middle of the count boxes, the header dots and the bottom border. The width stays 498 px.
   Everything that sat between x = 60 and x = 440 moves right by 30 px.
2. **Row lines through the titles.** The drawer has 18 row lines. Each is 4 px tall: dark, bright,
   mid, dark, at y = 20 + 15k to 23 + 15k for k = 0–17. For each line, copy the name column's line
   pixels (x = 193–288, tiled every 96 px) into x = 16–187. The line then meets the left border
   (bright edge at x = 13) and the title divider (bright line at x = 188) the same way the existing
   lines meet the column dividers.

Resulting columns (absolute x):

| Part | x |
|---|---|
| Title cell | 16–186 (text right edge 183) |
| Title / name divider | 187–192 |
| Name cell | 193–288 |
| Divider | 289–292 |
| Status cell | 293–303 (11 px) |
| Divider | 304–307 |
| Emblem cell | 308–319 (12 px) |
| Frame | 320–324 |
| Scroll bar track | 325–340 (16 px, the client's scroll bar width) |
| Count boxes | right-aligned text in 447–479 |

Row k's cell interior is y = 24 + 15k to 34 + 15k.

### Layout file: `_nusers.txt`

| Control | Rect (left top right bottom) | Images |
|---|---|---|
| Noname (anchor) | 0 0 498 303 | `_nusers.spf` 0 |
| UsersList | 15 24 341 276 | — |
| TotalNum | 447 39 479 51 | — |
| CountryNum | 447 65 479 77 | — |
| CountryBtn | 365 62 431 78 | `_nusersb.spf` 0, 1 |
| MasterBtn | 365 84 431 100 | `_nusersb.spf` 2, 3 |
| Close | 392 277 453 299 | `_nbtn.spf` 0, 1, 2 |

The Close rect centers the button under the right panel. Check it against the art when building, and
nudge it if the bottom frame says otherwise. The Korean file's `Emoticon` control is dropped,
because the client never used it.

### Button frames: `_nusersb.spf`

Each face is 66×16. The file grows from 18 to 44 frames.

- Frames 0–17 stay as today's frames from **our** `_nusersb.spf`: nine buttons, each normal then lit.
  Our 8th button is Peasant. The Korean file's 8th button is Friends, and we don't use it.
- Frames 18–43 hold the 13 new faces, each normal then lit (face i uses frames 18 + 2i and 19 + 2i):
  Temuair, Medenia, Ability, Berserker, Warlord, Archer, Assassin, Arcanist, Elemental, Bard,
  Plague Dr, Adept, Druid.

**Carved labels.**
- They are built from the letters in today's labels. Those letters are C o u n t r y M a s e W i R
  g z d P k l G.
- The missing letters are T, A, B, D, E, b, c, h, m and p. They are drawn new in the same carved
  style, once for the normal state and once for the lit state.
- Every label must sit inside the button the way "Country" does. "Country" is the widest today at
  53 px (x = 7–59).
- Berserker, Elemental and Plague Dr pack their letters 1 px tighter to fit.

**Review gate.** Produce a 4× review sheet that shows every new face beside the originals, in both
states. The user approves the sheet, or touches up the PNGs in Aseprite, before any batch is built.

### Sources and build tool

- The sources are committed under `Tools/WorldList/`:
  - `art/`: indexed PNGs using the SPF palette. This means the drawer background and one PNG per
    button frame (or a single frame sheet).
  - `_nusers.txt`.
  - A `README.md`.
- A build script reads the sources and writes `_nusers.spf`, `_nusersb.spf` and `_nusers.txt`. It
  then produces a new `setoa.dat` from the local client folder.
- It writes these into a desktop batch folder with a `manifest.txt`, the same way
  `Tools/Accessories/deploy_batch.py` works.
- Palettized SPF reading and writing is added to the shared Python helpers
  (`Tools/Accessories/acclib/daformats.py`), next to `Epf`.
- The build checks itself by extracting the three files from the new `setoa.dat` and comparing
  renders against the sources.

## Server (Chaos-Server)

### Continent

- Add `Continent : byte { None = 0, Temuair = 1, Medenia = 2 }` to `Chaos.DarkAges/Definitions`,
  next to `BaseClass` and `AdvClass`.
- Add a `Continent` property to `MapInstance` (`Chaos/Collections/MapInstance.cs`).
- `ExpiringMapInstanceCache.InnerLoadFromFile(directory, …)` sets the property from the map's
  directory, using a small pure helper that can be tested. The helper takes the path segment right
  after a segment named `MapInstances`, ignoring case. `Temuair` gives Temuair, `Medenia` gives
  Medenia, and anything else gives None.
- Shards load from their base instance's directory, so they get the same continent.
- No map data changes.

### World list values

`WorldListMemberInfo` (`Chaos.Networking/Entities/Server`) gains:

- `AdvClass AdvClass`: the player's Medenian class, or None.
- `Continent Continent`: the continent of the player's current map.
- `bool HasAbility`: true when the player's ability level is above 0.

`AislingMapperProfile` fills them from `UserStatSheet.AdvClass`, `MapInstance.Continent` and the
ability level. `ChaosWorldClient.SendWorldList` is otherwise unchanged. The sort order, hidden
admins, name colors, and the Arena Host / Theatre Director / Knight of Unora overrides all stay.

### Packet: trailing section in `WorldListConverter`

The existing entries are written exactly as today. After the last entry comes one record per
player, in the same order:

```
byte  length        // number of bytes that follow in this record (3 in this spec)
byte  advClass      // AdvClass
byte  continent     // Continent
byte  flags         // bit 0: HasAbility; other bits 0
```

- **Reading.** After the entries, if the reader is at the end of the data, every player keeps None,
  None and false. This is what an old server sends.
- Otherwise, for each player, read `length`. Read the fields it knows, up to `length`, and skip any
  bytes beyond them. This is how spec 2 will add the emblem number.
- If the data runs out part-way, stop and leave the rest at their defaults.
- **Compatibility.** Old readers ignore the section, because `PacketSerializer.Deserialize` doesn't
  check for leftover bytes. So the server and client can update in either order. Existing converters
  already use the same optional-tail pattern: `DisplayPublicMessageConverter` tags and
  `SelfProfileConverter`.

## Client (Chaos.Client)

### Data

- `WorldListEntry` gains `AdvClass`, `Continent` and `HasAbility`.
- The `OnWorldList` mapping in `WorldState` copies them from each `WorldListMemberInfo`.

### Faces: new `WorldListFaces` (next to `WorldListControl`, no UI dependencies)

Each button has a fixed list of faces. Each face has a label frame pair and a filter.

| Button | Faces (filter) |
|---|---|
| 0 | Country (everyone) → Temuair (Continent = Temuair) → Medenia (Continent = Medenia) |
| 1 | Master (IsMaster) → Ability (HasAbility) |
| 2 | Warrior (BaseClass = Warrior) → Berserker → Warlord |
| 3 | Rogue (BaseClass = Rogue) → Archer → Assassin |
| 4 | Wizard (BaseClass = Wizard) → Arcanist → Elementalist |
| 5 | Priest (BaseClass = Priest) → Bard → Plague Doctor |
| 6 | Monk (BaseClass = Monk) → Adept → Druid |
| 7 | Peasant (BaseClass = Peasant) |
| 8 | Guild (IsGuilded) |

A Temuair class face matches on base class alone, so "Warrior" includes Berserkers and Warlords. A
Medenian face matches `AdvClass` exactly. Its label is the short carved label (Elemental, Plague
Dr).

### Rows: `WorldListEntryControl`

- Row height is 15. Positions are relative to the list's left edge, x = 15:
  - Title: x 0, width 168, right-aligned.
  - Name: x 178, width 94, right-aligned.
  - Status icon: x 278, 11×11.
  - Emblem cell: x 293, left empty.
- Text sits 1 px above the row top, so it centers in the 11 px cell as in the approved mock. Check it
  against the art.
- Icons sit at the row top.
- The row width comes from the existing formula: `UsersList.Width − ScrollBarControl.DEFAULT_WIDTH −
  5` = 305.
- The list scrolls a whole row at a time. `VirtualizedRowList` offsets are row indexes, so text
  never drifts off the painted lines.

### Drawer: `WorldListControl`

- `ROW_HEIGHT` becomes 15. 17 rows show at once: 16 full rows plus the peek row, which fits
  exactly.
- The tab and count rects come from `_nusers.txt`, as today. The stride is still MasterBtn.Y −
  CountryBtn.Y = 22.
- **Clicking an unlit button** lights it and filters by its current face.
- **Clicking the lit button** turns it to its next face, wrapping around. It swaps the button's
  normal and lit textures to that face's frames and filters again.
- **Each count box** shows the count for its own button's current face.
- **Opening (`Show`).** Button 0 goes back to Country and lights. Every other button is unlit and
  keeps its face until logout. The list still scrolls to your own row.
- **Bug fix.** Today `Show` lights button 0 without unlighting the button that was lit before, so two
  buttons can look lit after reopening. The new selection code clears the old one.
- Update the `_nusers` entry in `controlFileList.txt` to match: the rects, the 44-frame
  `_nusersb.spf`, and the new faces.

## Edge cases

- A title longer than 28 letters is clipped, as long titles are today.
- A player on a map outside both folders appears only under Country and their class faces.
- A client talking to an old server shows 0 for Temuair, Medenia, Ability and every Medenian face,
  until the server updates. Nothing breaks.
- The new client must ship in the same launcher patch as the new `setoa.dat`, because the new layout
  needs the new art.

## Testing

**Server**
- `Chaos.Networking.Tests`, world list converter:
  - a round trip with the section;
  - an old-format packet with no section, which gives None, None and false;
  - a record with a length above 3 and extra bytes, which should be skipped and leave the following
    records correct;
  - a truncated section, which should give defaults for the players it doesn't cover.
- `Chaos.Tests`, the continent helper: Temuair, Medenia, another folder, mixed case, a shard's
  directory.
- The mapper fills all three values.

**Client** (`Chaos.Client.Tests`, `WorldListFaces`)
- Every face's filter, including Warrior matching Berserker and Warlord.
- Per-face counts.
- Rotation order and wrap-around.
- The open behavior: button 0 resets to Country and is the only lit button, and other faces are
  kept.

**Art**
- The user approves the 4× review sheet.
- The round-trip extraction from the built `setoa.dat` matches the sources.

**In game** (local server, screenshots through the game window automation)
- The new art shows, rows sit in the grid, 17 rows are visible, and the list scrolls one row at a
  time.
- Long titles fit, and counts sit right-aligned in the small boxes.
- Every button turns through its faces with the right carved art, and counts follow.
- Moving from a Temuair map to a Medenia map and reopening the list moves you between those counts.
- Reopening lights only Country.

Two `Chaos.Tests` failures already exist on master: GiveAbility and OnItemDroppedOn stackable. They
aren't caused by this work.

## Rollout and build notes

- Ship the client and `setoa.dat` in one launcher patch. The server can go before or after.
- After the server change merges, point the client's `Chaos-Server` submodule at it.
- Stop any running `Chaos.exe` or client before building, because they lock `bin`. Never build both
  solutions at once.
- Several sessions share these checkouts, so stage by explicit path. Never commit
  `launchSettings.json` or a local `StagingDirectory` in `appsettings.json`.

## Hand-off to spec 2 (emblem system)

Answers already gathered:
- **Where emblems come from:** achievements the game records, events and contests, and staff
  grants. There is no shop.
- **How many show:** one per player, shown only in the world list for now, in the outer cell.
- **Choosing an emblem:** an emblem window, a simplified version of the Korean book (`_nui_ebl`). It
  opens from your own world list row or your profile, and shows each emblem's name and how it was
  earned.
- **Locked emblems:** they show dimmed as goals, except ones marked secret, which stay hidden until
  earned.
- **Losing an emblem:** some are permanent, some expire, and some move to whoever currently holds a
  record.
- **Art:** the Korean set is 183 emblems (`embl001`–`embl183`, mostly 11×11, 21 animated with 3–64
  frames), plus 30 rank icons. The emblem number travels as a new field in the trailing section
  above.
