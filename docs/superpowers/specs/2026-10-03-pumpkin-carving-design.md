# Pumpkin Carving — Design

**Date:** 2026-10-03
**Repos:** Chaos-Server (round rules, messages, pumpkins), Chaos.Client (carving window, pumpkin painter), Unora
(pumpkin template, tiles, dialogs, marks)
**Status:** approved in brainstorming. One of the haunted theatre games (sub-project 3 of
`2026-09-30-mirror-maze-design.md`), built after Spotlight Chairs.

Mockups from the design session: `.superpowers/brainstorm/31186-1791009704/content/` in Chaos.Client (not committed).
`carving-iso-v2.html` option B is the approved look: an isometric pumpkin with its face turned partway toward the
viewer, and the flat carving wrapped onto it.

## 1. Summary

During Halloween, the theatre director starts a carving round from Thulin's menu. Eight blank pumpkins appear on the
stage. Players claim one by stepping on the tile in front of it, and carve a face into a flat 22 × 14 grid in a carving
window. When time runs out, the house lights go dark and every carved face lights up at once. The audience votes by
clicking the pumpkin they like. The winner gets candy, a legend mark, and their pumpkin on display in the theatre until
the next winner.

## 2. Decisions

| Question | Decision |
|---|---|
| Who runs it | The theatre director starts each round. |
| Rude carvings | Every carving shows at the reveal. The director can remove any pumpkin. |
| Carving style | Cut or not: each cell is skin or cut through. Cut cells glow with the candle. |
| Pumpkin look | Isometric pumpkin sprite, face turned partway toward the viewer. The flat grid is mapped onto the sprite like a guild cloak design. |
| Carvers per round | Up to 8. |
| Joining | Step on the claim tile in front of a blank pumpkin. |
| During carving | Pumpkins stay blank. Big reveal at the end. |
| Voting | Click a pumpkin. Counts stay hidden until the end. |
| Prize | Winner 20 candy, other finishers 5, at most once per 24 hours per player. |
| Afterwards | The winner's pumpkin stays on display until the next round's winner replaces it. |
| Approach | Pumpkins are stage NPCs (merchants) with a per-entity "pumpkin look", like the guild cloak look. Not client-drawn props. |

## 3. Behaviour

### 3.1 Starting

- The director picks "Pumpkin Carving" from Thulin's director menu.
- Refused when: not the director; outside the Halloween window; a carving round is already running; Spotlight Chairs
  is running. Spotlight Chairs is likewise refused while a carving round runs.
- Eight blank pumpkins spawn on the stage, each with a claim tile in front of it. The plan picks the eight pumpkin tiles
  and claim tiles inside the stage rectangle `(0, 12)`–`(8, 20)`, keeping clear of column 0 rows 12–19 (the backstage
  warp).
- The room gets: "Pumpkin Carving! Step up to a pumpkin to carve."

### 3.2 Carving (3 minutes)

- Stepping on a free pumpkin's claim tile claims it and opens the carving window. A player can own one pumpkin per
  round. A player who already owns one gets the window for their own pumpkin instead.
- A claimed pumpkin is named "<Name>'s pumpkin". A free one is named "Blank Pumpkin".
- Late joiners get whatever time is left.
- The window saves to the server every 3 seconds while changed, and on Done. Closing the window keeps the work.
  Stepping on the claim tile again, or clicking one's own pumpkin, reopens it.
- Leaving the theatre map gives up the pumpkin. It becomes blank and free again (during Carving) or is removed
  (later).
- The director's menu gets "End the carving now", which jumps straight to the reveal.

### 3.3 Reveal

- When the carving time ends, every open carving window closes.
- Pumpkins with fewer than 6 cut cells, or unclaimed, count as blank. They are removed and earn nothing.
- The stage lighting is saved, the house lights go dark, and every remaining pumpkin's look switches to lit. If the
  client can give a merchant a small light in the dark, each lit pumpkin casts one (the plan checks this).
- With fewer than 2 carved pumpkins left, there is no vote. A single finisher gets the finisher prize, and the round
  goes straight to results with no winner.

### 3.4 Voting (60 seconds)

- Anyone on the theatre map who did not carve in this round can vote, by clicking a pumpkin.
- A vote shows "You voted for <Name>'s pumpkin." Clicking another pumpkin moves the vote. Clicking the same one again
  does nothing new.
- A carver who clicks a pumpkin gets "Carvers can't vote."
- Counts are never shown during the vote.
- The director clicking a pumpkin gets a menu: "Vote for this pumpkin", "Remove this pumpkin".

### 3.5 Results

- The stage lighting saved at the reveal is restored.
- The room hears the winner: "<Name>'s pumpkin wins!" (or "<A> and <B> tie!" for a tie, or "No votes were cast.").
- Every tied top carver wins. With no votes, there is no winner.
- Prizes, given to players still on the theatre map:
  - winner: 20 candy (`halloweencandy`);
  - every other finisher (6 or more cut cells, not removed): 5 candy;
  - candy at most once per 24 hours per player (timed event `pumpkincarvingCandy`). A player inside the 24 hours gets
    "You already found candy today." and no candy, but still the marks.
- Legend marks: every finisher gets "Carved a pumpkin at the Garamonde Theatre" (key `pumpkincarving`); each winner also
  gets "Carved the best pumpkin" (key `pumpkincarvingWin`). Same style as Spotlight Chairs' marks.
- The other pumpkins are removed. The winner's pumpkin moves to the display tile in the house. With a tie, the first
  tied pumpkin by stage order is displayed.

### 3.6 The display pumpkin

- The display pumpkin is lit and named "<Name>'s pumpkin".
- It is saved (carver name and grid) so it survives restarts. It is shown only while the Halloween window is open.
- The next round's winner replaces it. A round with no winner leaves it in place.
- The director (click it, "Remove this pumpkin") and admins (the same) can remove it. Removing deletes the saved file.
- Clicking it as anyone else does nothing.

### 3.7 Director controls

- Start, "End the carving now" (Carving state only), and "Call off Pumpkin Carving" (any state; no prizes or marks,
  pumpkins removed, lighting restored).
- "Remove this pumpkin" on a pumpkin's click menu, in any state. The carver gets "The director removed your pumpkin."
  and nothing else. A removed carver's vote rights do not change: they carved, so they still can't vote.
- Votes cast for a pumpkin that is removed (by the director, or because its carver left) are dropped. Those voters
  can vote again.

### 3.8 Numbers and messages

| Setting | Value |
|---|---|
| Pumpkins per round | 8 |
| Carving time | 3 minutes |
| Voting time | 60 seconds |
| Minimum cut cells to count | 6 |
| Minimum carved pumpkins for a vote | 2 |
| Winner candy | 20 |
| Finisher candy | 5 |
| Candy limit | once per 24 hours |
| Grid | 22 × 14 cells, 1 bit each, 39 bytes |
| Client save interval | 3 seconds while changed |
| Server save limit | 1 per second per carver |

Orange-bar texts (45 characters at most) are listed in the static class `PumpkinCarving`. Dialog option texts stay
under 35 characters.

## 4. Server (Chaos-Server)

### 4.1 Rules (`Chaos/Services/Theatre/PumpkinCarving.cs`, `PumpkinCarvingRound.cs`, new)

- `PumpkinCarving`: constants (section 3.8), template keys, mark keys and texts, refusal enum and texts, the grid size,
  and `CountCut(byte[] grid)` / `IsValidGrid(byte[] grid)` (exactly 39 bytes, the 4 unused bits of the last byte zero).
- `PumpkinCarvingRound`: a state machine with no game objects, following `SpotlightChairs`. States `Carving`,
  `Voting`, `Done`. Pumpkins are identified by stage slot (0–7). Players by id.
  - Inputs: `Claim(slot, playerId, name, now)`, `Save(playerId, grid, done, now)`, `Vote(voterId, slot)`,
    `Remove(slot)`, `Leave(playerId)`, `EndCarving(now)`, `CallOff()`, `Tick(now)`.
  - Each input returns a result the map script acts on: windows to open or close, looks to send, pumpkins to remove or
    rename, messages, and at the end the winners, finishers and the display pumpkin.
  - `Tick` moves Carving → Voting at the end of the carving time (or → Done when fewer than 2 carved pumpkins remain),
    and Voting → Done at the end of the voting time.
- Time is passed in, so every rule is testable without a map.

### 4.2 Map script (`SuomiTheatreMapScript`)

- Holds the current round, and ticks it once a second, like Spotlight Chairs.
- Finds claims the way Spotlight Chairs finds players on the stage: by checking which players stand on claim tiles.
- Spawns, renames and removes the pumpkin merchants. Sends windows, looks and messages. Saves and restores the stage
  lighting with `StageLighting.Snapshot` as Spotlight Chairs does, and sets the house to dark for the reveal.
- Gives prizes and marks, and writes the display pumpkin.
- Refuses to start either game while the other runs.
- On a player leaving the map during a round, calls `Leave`.

### 4.3 Pumpkins

- Merchant template `carving_pumpkin` (Unora): the new pumpkin sprite, no wander, no items, script key
  `pumpkincarving`. Spawned facing the house.
- `PumpkinCarvingMerchantScript` (new): on click, sends the click to the map script's round, which decides: owner →
  reopen the window (Carving); voter → vote (Voting); carver → "Carvers can't vote."; director → a dialog with "Vote
  for this pumpkin" and "Remove this pumpkin".
- When a pumpkin comes into a player's view, the server sends its `PumpkinLook`. Before the reveal, the look is
  blank and carries no grid, so no client can see a carving early.

### 4.4 Messages (`Chaos.Networking`, shared with the client)

Opcode numbers are the next free values in `ClientOpCode` and `ServerOpCode`, chosen in the plan.

| Message | Direction | Contents |
|---|---|---|
| `PumpkinCarvingOpen` | server → carver | seconds left (u16), the carver's current grid (39 bytes) |
| `PumpkinCarvingSave` | carver → server | done flag (u8), grid (39 bytes) |
| `PumpkinCarvingClose` | server → carver | none |
| `PumpkinLook` | server → players on the map | entity id (u32), state (u8: 0 blank, 1 lit), grid (39 bytes, only when lit) |

`CONSTANTS.CLIENT_VERSION` goes up by one.

### 4.5 Checks

- A save is accepted only from that pumpkin's owner, during Carving, with a valid grid, and at most once a second per
  carver. Anything else is ignored with no reply.
- A vote is accepted only during Voting, from a non-carver on the theatre map, for a pumpkin still on stage.
- Only a director (the existing theatre director check) can start, end, call off or remove. Admins can also remove
  the display pumpkin.
- The round runs only on the haunted theatre (`suomi_theatre_halloween`). If the theatre swaps back when the window
  closes, the round is called off.

### 4.6 Display pumpkin storage

- `Data/LocalStorage/PumpkinDisplay.json`: carver name and grid (base64). Written when a winner is chosen, deleted on
  removal.
- When the haunted theatre map loads (and on each tick, if it is missing and should exist), the map script spawns the
  display pumpkin at the display tile, lit, inside the Halloween window only.

## 5. Client (Chaos.Client)

### 5.1 Carving window (`Controls/World/Popups/PumpkinCarving/PumpkinCarvingControl.cs`, new)

- The wooden frame (`FramedDialogPanelBase`), compact. Title "Carve your pumpkin".
- Canvas: the 22 × 14 grid at 10× (220 × 140 px). Skin cells orange, cut cells candle yellow.
- Tools: knife, eraser, mirror toggle (also cuts or fills cell `x ↔ 21 − x`), undo, redo, clear. Right-click erases
  with any tool.
- Preview: the pumpkin, lit, at 2×, painted by `PumpkinPainter`.
- Timer: "m:ss left", counting down locally from the server's seconds; red under 30 seconds.
- Buttons: Done (sends the grid with done = 1, closes), Close (closes; work already saved).
- Sends `PumpkinCarvingSave` every 3 seconds while the grid has changed since the last send.
- The editor state lives in `ViewModel/PumpkinCarvingModel.cs` (grid, undo, redo, mirror, dirty flag), so it is
  testable without graphics. The plan decides how much to share with `GuildEmblemEditorModel` and
  `GuildEmblemCanvas`.

### 5.2 Pumpkin looks and the painter

- `WorldState.PumpkinLooks`: entity id → look (lit flag + grid). Works whether the look or the creature arrives
  first, like `GuildCloakLooks`. Removed with the entity and cleared on map change.
- `Chaos.Client.Rendering/PumpkinPainter.cs` (new): given a plain creature frame, its face map and a lit look, returns
  the frame with each carved pixel recoloured in candle glow (bright in the middle of the face, warmer at the edges,
  with a slight per-frame flicker). A blank look draws the plain frame.
- `PumpkinFaceMap` (new): per sprite frame, the grid cell each pixel shows, or none. Loaded from an embedded resource in
  `Chaos.Client.Rendering` that the art tool writes.
- Down-right uses the mirrored down-left frame, as for every creature. The face map is looked up in the unmirrored
  frame.
- Painted frames are cached by (entity look, frame index) in their own cache, never the plain sprite's cache, and
  cleared on map change.

### 5.3 Messages

`ConnectionManager` handlers for `PumpkinCarvingOpen`, `PumpkinCarvingClose` and `PumpkinLook`, and a send method for
`PumpkinCarvingSave`. A new partial `WorldScreen.PumpkinCarving.cs` owns the window and applies looks.

## 6. Art (Unora tools + launcher patch)

- `Tools/PumpkinCarving/make_pumpkin.py` draws the pumpkin with code: an isometric round pumpkin with ribs, a stem, a
  dark outline and a limited palette, standing frames only. The face is centred 22° toward the viewer from the
  down-left diagonal, as in mockup option B.
- From the same geometry it writes the face map (the embedded client resource) and a preview sheet: blank, lit with
  sample faces, both facings, on a floor tile. The user approves the sheet before the art ships.
- The plan picks the creature sprite number and the `.dat` file, and how the sprite is packed. That `.dat` ships in a
  launcher patch.
- No AI art, as with the mirror scare creatures.

## 7. Tests

**Server** (TUnit, run with `dotnet run`)
- Round: claim, one pumpkin each, claiming a taken pumpkin, late claim; save from a non-owner, in the wrong state, too
  fast, wrong size, unused bits set; the 6-cell rule; fewer than 2 carved → no vote; carvers can't vote; vote change;
  tie and no-vote results; remove in each state; call off; leave during Carving frees the pumpkin, leave later removes
  it; end carving early.
- Map script: Chairs and carving refuse each other; the look sent before the reveal has no grid; lighting restored
  after results and after call off; candy limit; marks; the round is called off on a theatre swap.
- Display pumpkin: saved, loaded, not shown outside the window, removed.
- Round trips for the four messages.

Two server tests already fail on master (`GiveAbility`, `OnItemDroppedOn` stackable). Leave them alone.

**Client** (TUnit)
- Grid packing to 39 bytes and back.
- Painter: a carved cell glows, an uncarved cell stays skin, a blank look returns the plain frame.
- Face map: every grid cell is shown by at least one pixel of the main frame.
- Looks store: look before creature and creature before look both apply; removal and map change clear it.
- Model: mirror, undo, redo, clear, dirty flag.

**Art tool**
- `make_pumpkin.py --check`: the face map covers every cell, and every mapped pixel is inside the sprite.

**By hand in the game**
1. Start a round as director with three characters in the theatre.
2. Claim, carve, close the window, reopen it from the tile and from the pumpkin.
3. Leave one pumpkin with fewer than 6 cuts.
4. Watch the reveal: lights dark, faces lit at once, the blank one gone.
5. Vote, change the vote, try to vote as a carver.
6. Remove a pumpkin as the director.
7. Check the candy, the 24-hour limit, the marks, and the display pumpkin, then restart the server and check it again.
8. Try to start Spotlight Chairs during a round, and the reverse.

## 8. Release

- Branches `feat/pumpkin-carving` in each repo (worktrees; the shared checkouts have other sessions' work).
- Merge order: server, then the client with its `Chaos-Server` pointer on the new server master, then Unora. Unora's
  data must ship before or with the server.
- `CLIENT_VERSION` goes up by one: the server, the client and the launcher patch with the pumpkin sprite go out
  together.
- Until it ships, the theatre plays as it does today.

## 9. Open items for the plan

- The eight pumpkin tiles, the claim tiles and the display tile.
- The opcode numbers.
- The sprite number, its `.dat` file and how it is packed.
- Whether a merchant can cast a light in the darkness (section 3.3).
- How much of the emblem editor's canvas and model to share.

## 10. Planning amendments (2026-10-03)

Plan: `docs/superpowers/plans/2026-10-03-pumpkin-carving.md`. Where this section and the sections above differ, this
section wins.

1. **Tiles.** The stage is walled off from the house; players reach it with Thulin's "jump on stage". Pumpkins stand at
   `(6, 13)` to `(6, 20)`, facing Right (toward the house). Each claim tile is behind its pumpkin, at `(5, y)`. The
   display tile is `(11, 15)` in the house.
2. **Numbers.** Client opcode `PumpkinCarvingSave = 142`; server opcodes `PumpkinCarvingDisplay = 149` (Open and Close
   in one message, like fishing) and `PumpkinLook = 150`. `CLIENT_VERSION` 766 → 767. Creature sprite 1455 and
   palette 355 in `hades.dat`.
3. **A separate map script.** The game runs in its own map script, `pumpkincarvingmap`, added to the haunted theatre's
   script keys. It reaches the lights through `SuomiTheatreMapScript`, which gains `ApplySavedScene`,
   `StageGameRunning` and `LightsBusyText`. Spotlight Chairs gains a `StageBusy` refusal.
4. **Looks follow the display.** `ChaosWorldClient.SendVisibleEntities` sends a pumpkin's look right after the
   pumpkin, so spawns, approaches and refreshes all get it. The reveal also sends every look to everyone on the map.
5. **Pumpkin names.** Claiming, or freeing a pumpkin when its carver leaves, replaces the merchant with a new one under
   the new name, so every client sees the name.
6. **A removed carver is barred** from claiming another pumpkin in the same round.
7. **Saves.** The "done" flag means "the window is closing": Done and Close both send it. The server allows one
   periodic save and one closing save per second per carver. The client saves every 3 seconds while changed, and about
   every 1.1 seconds in the last 10 seconds.
8. **Light.** The client gives a lit pumpkin a small lantern (`LanternSize.Small`), so it shows in the dark house.
9. **Face map.** Each sprite pixel maps to every grid cell its 4 × 4 samples land in, and lights when any of them is
   cut. At game size a cell is about one pixel, so one sample per pixel dropped cells; with this rule the front frame
   shows all 308 cells. Each entry also carries a glow amount (0 in the middle of the face, 100 at its corners).
10. **Sprite layout.** Frame 0 is the back (Up, and Left mirrored); frame 1 is the front (Right, and Down mirrored). No
    idle animation; the client adds a 4-phase candle flicker.
11. **Texts.** The start message is "Pumpkin Carving! Step up to a pumpkin." (the longer one did not fit the 45-character
    orange bar). The reveal message is "The pumpkins are lit! Click your favorite."

## 11. As built (2026-10-03)

Where this section and the sections above differ, this section wins.

1. **Face map includes the outline.** Pixels on the pumpkin's dark outline carry face cells too, so a cut at the rim
   breaks through the outline. Without them the front frame showed only 303 of 308 cells.
2. **Saves.** The server accepts one periodic save every 500 ms (not 1 s) and one closing save per second per carver.
   The client's 1.1 s end-of-round saves no longer get dropped by network jitter. Close always sends a closing save,
   like Done.
3. **Painted frames are cached by carving content** (the grid bytes, the frame and the flicker phase), not by a look
   counter. A same-map refresh (the theatre's darkness flips refresh everyone) resends looks without growing the cache.
4. **A removed pumpkin's claim tile says nothing**, instead of "That pumpkin is taken."
5. **The carving preview** reads the plain sprite's pixels once and repaints a copy.
6. **pack_pumpkin.cs** patches a temporary copy and only moves it to `hades.dat` after re-reading it; any failure
   cleans the release folder. The manifest says to confirm the base `hades.dat` matches the live client's.
   Do not pass the live client folder as the output folder: the cleanup would delete its `hades.dat`.
7. **The client solution file** names the `Chaos-Server` submodule's project paths directly, so in a worktree build
   `Chaos.Client/Chaos.Client.csproj` and the test project with `-p:UnoraServerPath=…` instead.
8. **`Client_version_is_766` became `Client_version_is_767`** (Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs).
