# Mileth College: wall art and the live board

Date: 2026-10-13. Status: approved in brainstorming, awaiting spec review.

## 1. Goal

Two features for the five College subject rooms:

1. **Wall art.** Teachers hang Art drawings in picture frames on the room walls. Everyone in the room sees them, painted on the wall surface.
2. **Live board.** A large glowing magic pane on the north wall. The Teacher draws on it during a class (or hands the pen to a student), and everyone in the room sees the drawing appear on the wall.

## 2. Decisions taken in brainstorming

| Topic | Decision |
|---|---|
| What can be hung | College gallery Art entries, and Art hand-ins from the Teacher's own classes. No PC uploads, no writing pictures. |
| How long art stays | Until someone takes it down. Each room keeps its own wall across classes and restarts. |
| Who hangs / removes | A Teacher of the room's subject (a Knight while the subject has no Teacher), the Director, admins. The artist can take down their own piece. |
| Frame count | 6 per room: 2 on the north wall, 4 on the west wall. |
| Picture size on the wall | Full size: the 96x72 drawing 1:1 plus an 8 px border = 112 px = 4 tiles. |
| Board look | A glowing magic pane with a slow shimmer (not a chalkboard or whiteboard). |
| Board size | 6 tiles wide (168 px); drawing area 160x80. |
| Who draws on the board | The class Teacher, who can hand the pen to one student at a time. Director and admins can also draw. |
| Board tools | The Art canvas tools plus a text tool that stamps typed words in a pixel font. |
| Board at class end | Wipes clean. The Teacher can save boards beforehand. |
| Saved boards | Loaded back onto the board later, like prepared slides. Not hung in frames. |
| Seeing the board up close | The drawing shows on the wall; clicking the board opens a live read-only view window. |

## 3. Room layout

Applies to all five subject rooms (templates 763, 775, 776, 777, 778) and the debate layout (779). All are 20x20 and built by `Unora/Tools/College/college_maps.py`. The visible walls are the north wall (row y = 0) and the west wall (column x = 0).

| Spot | Wall | Tiles | Size |
|---|---|---|---|
| Frame 1 | North | x 1-4 | 4 tiles |
| Board | North | x 7-12 | 6 tiles |
| Frame 2 | North | x 15-18 | 4 tiles |
| Frame 3 | West | y 1-4 | 4 tiles |
| Frame 4 | West | y 6-9 | 4 tiles |
| Frame 5 | West | y 11-14 | 4 tiles |
| Frame 6 | West | y 16-19 | 4 tiles |

- The decorated wall pieces under these spots become the plain repeating wall art (north 20091-20094, west 20115/20113), so pictures never sit on top of windows or carvings. The remaining decorated pieces stay.
- 779 gets the same wall changes, so the art stays visible during a debate.
- Each spot gets a clickable reactor tile on the floor in front of it (y = 1 for north spots, x = 1 for west spots). Clicks on the wall itself resolve to floor tiles, so a floor reactor is the reliable target. The board uses the floor tile in front of its middle; each frame uses the tile in front of its middle-left tile. The plan must check these tiles are free of benches and pots in every layout, and move a bench if one clashes.
- An empty frame spot shows only the plain wall. There is no empty frame.

## 4. Drawing on the wall (client)

**Hook.** The mirror maze already draws panels on north and west walls inside the depth-sorted wall pass (`WorldScreen.Draw.cs` calls `DrawMirrorTile` after each wall tile's foreground). Wall art uses the same slot, so things in front of the wall cover the art correctly. The shared part of that hook is pulled into a small wall-decal step, called from the same place, that both mirrors and College art use.

**Slant.** Walls slope: a north wall drops half a pixel per pixel to the right, a west wall rises half a pixel per pixel to the right (`MirrorGeometry.BaseRow`). Each picture is drawn once into a slanted texture (each column shifted by col/2), then cut into 28 px strips, one per wall tile, and each strip draws in its tile's slot. North spots use the tile's left half, west spots the right half, as mirrors do.

**Frames.** The wooden frame is drawn into the slanted texture with the picture, so no new sprite file is needed. It is an 8 px border in two wood tones with a darker inner line.

**Board.** The pane is a translucent cyan-blue fill with a brighter 4 px edge. Empty board pixels (palette index 0) show the pane. A soft light band drifts across the pane every few seconds. The slanted board texture is rebuilt only when the drawing's version changes; the shimmer is a separate overlay pass.

**Vertical fit.** The mirror face canvas is 89 px tall starting 62 px above the tile. The frame (88 px) and board (88 px) are centred in it. The plan must confirm on a real room screenshot that the art sits fully on the wall face, and adjust the top offset if not.

**When the client has the data.** The server sends a wall message when a player enters a College room, after the debate layout swap (the client treats a morph as a map change and drops wall state), and whenever a frame changes. Leaving the room clears it.

## 5. Hanging pictures

**Teacher flow:**

1. Click a frame's floor tile. A dialog offers **View**, **Hang a picture**, and **Take down** (the last only when the frame is filled). Visitors only see **View**.
2. **Hang a picture** opens a picker window with two tabs:
   - **Gallery:** all claimed, not hidden Art entries (the existing gallery list and `DrawingFetch` thumbnails).
   - **Hand-ins:** Art hand-ins from classes this Teacher ran. The server sends the list and the drawings for this tab.
3. Choosing a drawing hangs it at once for everyone in the room. Hanging over a filled frame replaces it.

**View** opens a window with the drawing at zoom 4, its title, and the artist's name.

**Permissions** (checked on the server for every action):

- Hang / replace / take down: `CollegeRoles.CanTeach` for any subject of that room (the Art and Music room counts both), or the Director, or an admin.
- The artist may take down their own piece from any frame.
- Everyone may view.

**Stored data.** `CollegeState` gains `WallArt`: room key -> six slots. A filled slot holds:

- the drawing snapshot (palette + pixels), title, artist name;
- source kind (Gallery or HandIn) and source id;
- who hung it and when (UTC).

The snapshot means the picture survives the hand-in being cleared later.

**Automatic removal.** When the Director hides a gallery entry, or an entry is deleted, every slot whose source is that entry is emptied and affected rooms are re-sent.

**Renames.** The file server's `AislingRenamer.College.cs` rewrites the artist and "hung by" names in `WallArt`.

**Wire size.** Six drawings at about 7 KB each come to about 42 KB, under the 64 KB message limit, so one `WallArt` message carries the whole room.

## 6. The live board

**Pen rules:**

- While a class runs in the room, its Teacher holds the pen. The Director and admins may always draw.
- From the board view, the Teacher picks a student in the room and chooses **Give pen**, and later **Take pen back**. Only one holder at a time.
- The pen returns to the Teacher when the holder leaves the room or logs out, and at class end.
- With no class running, the wall shows an empty glowing board.

**Editor.** `ArtCanvasControl` gains a Board mode: a 160x80 drawing at zoom 3 (480x240), in the same window frame.

- Tools: pen, eraser, fill, line, pick, sizes 1-3, mirror, undo, 32 swatches.
- **Text tool:** click a spot, type up to 40 characters, choose Small or Large letters. The text is stamped as pixels in the current colour and can be undone like any stroke. The pixel font is a fixed bitmap embedded in the client: Small = 5 px tall glyphs, Large = 10 px (Small scaled 2x).
- **Clear board:** empties the board for everyone (undoable by the drawer).
- **Save board / Load board** (Teacher only; see below).
- A student holding the pen gets the drawing tools and Clear, but not Save or Load.

**Board view (everyone else).** Clicking the board's floor tile opens a read-only window with the live drawing at zoom 3. For the Teacher, the same window lists students in the room with Give pen / Take pen back, and an **Edit** button that opens the editor.

**Sync:**

- The holder's client sends the whole board, Deflate-compressed, at most every 600 ms and only while it changed, plus once when the editor closes.
- The server keeps the latest board per room and passes it to everyone in the room at most every 500 ms. A board that arrives early is held and applied on the next room tick (the pumpkin carving pattern). An unchanged board is not re-sent.
- The server rejects a board from anyone not allowed to draw, and any board that does not decompress to exactly 96 + 12,800 bytes.
- A player who enters mid-class receives the current board with the wall message.

**Saved boards:**

- **Save board** asks for a name (up to 30 characters) and stores the current board under the Teacher's name. Up to 20 per Teacher; saving a 21st asks the Teacher to delete one first. Saving with an existing name replaces it.
- **Load board** lists the Teacher's saved boards and puts the chosen one up for everyone.
- Saved boards live in the College works directory, `Boards/{teacher}.json`. The renamer updates the file name and owner.
- A Teacher of the subject can open the editor in the room outside a class to prepare boards. Nothing is broadcast; only Save works.

**End of class.** The board wipes, the pen returns, and any open board view or editor in the room closes.

## 7. Packets

- New `CollegeActionType` values from 39: WallHang, WallTakeDown, WallPickerOpen (hand-in tab list), BoardUpdate, BoardClear, BoardGivePen, BoardTakePen, BoardSave, BoardLoad, BoardDelete, BoardSavedList, BoardOpenEditor.
- New `CollegeDisplayType` values from 26: WallArt (all six slots), WallPicker (hand-in list with drawings), Board (compressed board + holder id), BoardSavedList, BoardPen (who holds it, for the Teacher view).
- The exact list may be trimmed in the plan if one message can serve two needs.
- Rate limits use the existing per-player `ClassActions` counter (120 per minute). BoardUpdate is limited by the 500 ms rule instead, so it does not count.
- `CLIENT_VERSION` 782 -> 783, with the pinned test in `GuildEmblemPacketConverterTests`.

## 8. Error handling

- Unknown room, slot out of range, missing source entry, or a hidden gallery entry: the server refuses with a short orange bar message and changes nothing.
- A board message from a non-holder, or with a bad size: dropped silently and logged at debug level.
- Wall data that fails to parse on the client: that slot draws as empty wall.
- A player who opens the board view in a room that then morphs: the view reopens after the wall message, as other class tools do.

## 9. Testing

**Server unit tests:**

- Permission rules for hang, replace, take down (Teacher, Knight fallback, Director, admin, artist, visitor).
- Hiding a gallery entry empties the slots that show it.
- Wall data survives a save and load of `CollegeState`.
- Pen give/take, return on leave, return at class end.
- The board throttle holds and later applies an early update; unchanged boards are not re-sent; bad sizes are rejected.
- Board wipes at class end; saved board cap of 20 and name replace.
- Renamer rewrites wall names and the saved-boards file.

**Client unit tests:**

- Slant and strip maths for north and west spots (pixel positions per column).
- Text tool stamping (glyph placement, clipping at the board edge, undo).
- Board compress/decompress round trip; packet converter round trips for each new message.

**Unora:** `college_maps.py` re-run gives the new walls and reactor tiles; existing Unora Python tests pass.

**In-game check (two clients):**

1. Hang a gallery piece and a hand-in; both appear on the wall for the second client; View shows title and artist.
2. Start a debate; the art is still on the walls after the layout swap.
3. As the artist, take down a hand-in.
4. Start a class; draw and type on the board; the second client sees it on the wall and in the board view within about a second.
5. Give the pen to the second client; they draw; the Teacher takes it back.
6. Save a board, end the class (board wipes), start a new class, load it.
7. Restart the server; the wall art is still there.

## 10. Release

- Server + client release (`CLIENT_VERSION` 783) + Unora map and dialog changes + file server renamer update.
- No new .dat art: the frames, pane and shimmer are drawn by the client.
