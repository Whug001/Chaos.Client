# Guild Cloak — Painted Guild Cloaks

Date: 2026-09-25. Spans three repos: Chaos.Client (drawing, editor and review windows), Chaos-Server
(design storage, messages, guild and admin scripts) and Unora (item, dialog data, sprite copy).

Mockups from the design session: `Unora/.superpowers/brainstorm/345065-1790308407/content/`, in
particular `cloak-customize.html` (the three color regions of sprite 127) and `cloak-editor.html` (the
approved editor and the mapping test). The throwaway mapping test is in `.../spike/fullpaint.py`.

The wider list of guild hall ideas this came from is `Unora/docs/guild-hall-ideas.md` (ideas 5, 15, 18).

## Summary

A guild buys a cloak deed from Tibbs. The guild leader then paints the guild's cloak in an in-game
editor: one front view and one back view, with up to 6 colors of any shade. The leader submits the design,
and an admin approves or rejects it in an in-game review window. After approval, any member can buy a
guild cloak from Quill. Every guild cloak shows its wearer's current guild design, to everyone who sees it.

## Why

Players want a way to show which guild they belong to. Today a guild has no look of its own. The guild
hall rooms are useful but invisible outside the hall. A painted cloak shows the guild in every hunting
ground and town, which also answers the complaint that guild halls pull players out of the world.

## Goals

- The leader can design a recognizable cloak without art tools, in the game.
- The design shows on every animation frame and both body types, with the cloak's own folds and shading.
- A new or changed design shows for everyone as soon as an admin approves it. No client patch is needed.
- Nothing a player paints is shown to other players until an admin approves it.

## Non-goals

- No hall banners, field banners, trim tiers, guild chat color or painted inventory icon. They stay in the
  ideas doc.
- No per-frame painting. The leader paints two views and the game maps them onto every frame.
- No readable text. The cloak is about 25 by 40 pixels, so fine detail stretches in turning frames.
- No stats on the cloak. It is cosmetic.
- No fallback for the plain Dark Ages client. All players use Chaos.Client.

## Behavior

### Who can do what

| Action | Who |
|---|---|
| Buy the cloak deed from Tibbs | Council or leader (`IsOfficerRank`), like the other hall additions |
| Open the editor, save a draft, submit | The current leader only (`IsLeaderRank`) |
| Buy a guild cloak from Quill | Any member, once the guild owns the deed |
| Review, approve, reject | Admins (`IsAdmin`) |
| Clear a guild's approved design | Admins (`IsAdmin`) |

The server checks the role on every request, not only when a window opens.

### The deed

- Tibbs's "Purchase Additions" list gets "Purchase Guild Cloaks" while the guild does not own it.
- It costs 5,000,000 gold, like the other additions, and goes through the same confirm dialog.
- The guild gets the same "has purchased" message as for the other additions.
- The deed is a guild hall property named `cloaks`. It does **not** change the hall map, so
  `GetMorphCode` ignores it and no NPCs are spawned for it.

### Designing

- Quill gets "Design the guild cloak" for the leader, once the guild owns the deed.
- It opens the editor with the guild's draft. With no draft, it opens the approved design. With neither, it
  opens a default design: one dark color (RGB 40, 40, 48) on every pixel.
- "Save draft" stores the draft on the server. The draft belongs to the guild, so a new leader continues
  from it.
- "Submit" stores the design as the guild's waiting design. It replaces any design already waiting.
- The editor shows a status line: `Draft`, `Waiting for review`, `Approved` or `Rejected: <reason>`.

### Reviewing

- The admin trinket gets "Review guild cloaks". It opens the review window with every waiting design.
- An admin approves a design, or rejects it with a typed reason of 1 to 200 characters.
- Each decision names the exact submission. If the leader resubmitted after the list was sent, the server
  refuses the decision, sends the fresh list, and shows "This design changed. The list has been refreshed."
- On approval, the waiting design becomes the approved design, and the waiting slot empties.
- On rejection, the waiting slot empties and the rejection (reason, admin name, time) is stored.
- The leader gets an orange-bar message with the outcome if they are online. They also see it on the
  editor's status line the next time they open it. A rejected design can be fixed and resubmitted at once.

### Clearing

- The admin trinket gets "Clear a guild's cloak". It asks for a guild name, then asks for confirmation.
- Clearing deletes the guild's approved design. Members' cloaks go plain until a new design is approved.
- It covers a design approved by mistake, or one reported later.

### Buying and wearing

- Quill gets "Buy a guild cloak" for every member, once the guild owns the deed.
- It costs 50,000 gold and gives one Guild Cloak item. There is no limit per member.
- The Guild Cloak is an accessory with no stats. It cannot be traded or dropped for others (`noTrade`,
  `accountBound`), like the Black Cape of Romance.

### What everyone sees

- A guild cloak always shows its wearer's **current** guild's approved design.
- It shows the plain cloak (the same art as the Black Cape of Romance) when the wearer has no guild, or
  the guild has no approved design.
- While a new design waits for review, members keep showing the last approved design.
- If the guild disbands, its cloak data is deleted and its members' cloaks go plain.
- When a player joins or leaves a guild, everyone near them sees the change at once.
- Emblems read the right way round in all four facing directions. See "Mirrored directions" below.

### Numbers

| Setting | Value |
|---|---|
| Cloak deed | 5,000,000 gold |
| Guild Cloak | 50,000 gold |
| Submitting a design | free |
| Colors per design | 1 to 6, any RGB |
| Rejection reason | 1 to 200 characters |
| Save or submit | at most once every 2 seconds per player |

## Architecture

The server stores each guild's designs and decides which design every guild cloak shows. When a player
wearing a guild cloak comes into view, the server tells the client "this player shows design N". The
client asks for design N once per session if it does not have it. The client then paints the cloak itself:
for every frame it draws, it maps the design onto the frame's pixels and keeps the frame's shading. Frames
are cached per design, so each one is worked out once.

A design has a number (its design id). Every submission gets a new id from one counter, and an approved
design keeps its submission's id. So a changed design always has a new id, and clients never draw an old
cached version.

## Art (Unora `Tools/Accessories`)

- The Guild Cloak gets its own accessory sprite number: an exact copy of sprite 127 (every `c` and `g` sheet,
  both body types, and its `palc.tbl` palette line).
- The number is assigned and shipped through the accessory tools (`ids.py add`, `ids.py assign`,
  `deploy_batch.py`), like every other custom accessory.
- A separate number means any accessory drawn with it is a guild cloak. The Black Cape of Romance (127)
  stays unchanged.
- The item's panel icon reuses the Black Cape's icon (4266).

## The design

A design is a palette plus three grids of color numbers.

| Part | Size | Painted where |
|---|---|---|
| Palette | 1 to 6 RGB colors | — |
| Back grid | 27 × 41 | The outside of the cloak, seen from behind |
| Lining grid | 27 × 38 | The inside of the cloak, seen behind the body from the front |
| Collar grid | 18 × 8 | The collar over the shoulders, seen from the front |

- Each grid is row-major, one byte per pixel. `0` means unpainted; `1` to `6` pick a palette color.
- The grid sizes are the frame sizes of the male walk sheet (`01`) of sprite 127: back = frame 0, `c`
  layer; lining = frame 5, `g` layer; collar = frame 5, `c` layer. They live as constants in the shared
  messages library, so the client and server agree.
- A whole design is about 2.3 KB.
- The editor's "Front" view shows the lining and the collar together, with the collar on top.

## Server (Chaos-Server)

### `GuildCloakState` — stored designs

A new storage object, saved as JSON through `IStorage<T>` like `GuildHouseState`, and registered in
`SerializationContext`.

- `NextDesignId` (int, starts at 1).
- `Guilds`: a dictionary keyed by guild name (case-insensitive), each holding:
  - `Approved`: design + id + approving admin + time, or none.
  - `Waiting`: design + id + submitting leader + time, or none.
  - `Draft`: design, or none.
  - `LastRejection`: id + reason + admin + time, or none.
- Methods: save draft, submit (assigns the next id), approve (by id), reject (by id, with reason), clear,
  remove guild, find the approved design id for a guild, find a design by id.
- Validation lives here and is used by every entry point:
  - the palette has 1 to 6 colors;
  - each grid has exactly its size in bytes;
  - every grid value is 0 to the palette size.

### Guild hall deed (`GuildHouseState`, `GuildUpdateHallScript`)

- `GuildHouseState` gets a `Cloaks` property and the `cloaks` name in its property switches.
- `GuildUpdateHallScript` lists "Purchase Guild Cloaks" and handles its confirm like the others. It skips
  the map morph and NPC spawn for `cloaks`.

### Quill (`GuildManagementScript`)

- Adds "Design the guild cloak" for the leader and "Buy a guild cloak" for members, when the guild owns
  the deed.
- "Design" sends the open-editor message and closes the dialog.
- "Buy" shows a confirm dialog, takes 50,000 gold and gives the item. It refuses with a message if the
  player lacks the gold or has a full inventory.

### Admin trinket (`AdminTrinketScript`)

- "Review guild cloaks" sends the review list and closes the dialog.
- "Clear a guild's cloak" asks for a guild name, confirms, clears the approved design, and refreshes the
  guild's online members.

### Guild disband (`GuildDisbandScript`)

- Also removes the guild from `GuildCloakState`, next to the existing `GuildHouseState.RemoveGuild`.

### Showing the cloak

- After the server sends a player's display to a viewer (`ChaosWorldClient.SendDisplayAisling`), it also
  sends a cloak-look message when that player wears a guild cloak. The design id is the wearer's current
  guild's approved id, or 0. This includes the wearer's own client, so players see their own cloak.
- The server re-sends a player's display to everyone who can see them after:
  - an approval or clear that changes their guild's design (for online members wearing the cloak);
  - the player joining or leaving a guild, or being removed from one.

### World server handlers

- Editor interaction: checks leader rank, deed, rate limit and validation; then saves or submits. Replies
  with the updated editor state.
- Design request: answers with the design for a known id. Unknown ids are ignored.
- Review interaction: checks `IsAdmin`; then approves or rejects; then sends the refreshed review list.
  Non-admin requests are ignored and logged.

### Refusal messages (orange bar)

| Case | Message |
|---|---|
| Not the leader | "Only the guild leader can design the guild cloak." |
| No deed | "Your guild does not own the cloak deed." |
| Not in a guild | "You are not part of a guild." |
| Too fast | "Please wait a moment before saving again." |
| Invalid design | "That design could not be saved." |
| Stale decision | "This design changed. The list has been refreshed." |

## Messages (Chaos-Server `Chaos.Networking`, shared with the client)

Opcode numbers are chosen in the plan, from the next free values in `ClientOpCode` and `ServerOpCode`.

| Message | Direction | Contents |
|---|---|---|
| `GuildCloakEditorOpen` | server → leader | The design to edit, its status, the last rejection reason if any |
| `GuildCloakEditorInteraction` | leader → server | Action (`SaveDraft = 0`, `Submit = 1`, `Close = 2`), the design for save and submit |
| `GuildCloakLook` | server → viewer | Entity id, design id (0 = plain) |
| `GuildCloakDesignRequest` | client → server | Design id |
| `GuildCloakDesign` | server → client | Design id, the design |
| `GuildCloakReviewList` | server → admin | Each waiting design: submission id, guild name, leader name, time, the design |
| `GuildCloakReviewInteraction` | admin → server | Action (`Approve = 0`, `Reject = 1`, `Close = 2`), submission id, reason for reject |

## Client (Chaos.Client)

### Design cache

- A per-session store of designs by id, plus a map from entity id to design id.
- `GuildCloakLook` updates the map and the entity's appearance. If the design is missing, the client sends
  one `GuildCloakDesignRequest` for it.
- When a design arrives, the client clears the saved drawings of every entity that shows it.
- Id `-1` is reserved for the editor's live draft, and `-2` for the design selected in the review window.

### Drawing (`AislingRenderer` + a new `GuildCloakPainter`)

- `AislingAppearance` gets a guild cloak design id, so a change of design redraws the player.
- `RenderEquipLayer` sends a guild cloak layer (the new sprite number, with a design id that has a loaded
  design) to `GuildCloakPainter` instead of the dye path.
- The layer cache key gets the design id and the flip flag. Both are 0 and false for every other layer.

**Which view a frame uses.** A frame counts as front-facing when its `g` layer has pixels. This matched
every frame of the art in the test.
- Front-facing: the `g` layer uses the lining grid and the `c` layer uses the collar grid.
- Back-facing: both layers use the back grid.

**Mapping one pixel.** For a pixel at (x, y) in a frame layer:

1. Find the frame's top and bottom rows, and the leftmost and rightmost pixel of row y.
2. `v = (y − top) / (bottom − top)`, and `u = (x − rowLeft) / (rowRight − rowLeft)`.
3. For a flipped draw, use `u = 1 − u`. See "Mirrored directions".
4. In the reference frame of the grid, take the row at `v`. If that row is empty, step toward the middle
   until one has pixels. Take the column at `u` within that row's leftmost and rightmost pixels.
5. Read the grid there. If it is 0, look outward along the same row for the nearest painted pixel. If the
   row has none, use color 1.

**Shading.** The paint is flat. The frame's own pixel gives the light and shadow:

- The brightness `b` is the largest RGB channel of the pixel's color in palette `palc000` (0 to 99 for the
  cloth).
- Dye-slot pixels (98 to 103) draw the old rune and hem. They take the middle brightness of their non-dye
  neighbors instead, so the rune disappears.
- An edge pixel (one with an empty neighbor above, below, left or right) uses `k = 0.42`.
- Any other pixel uses `k = 0.78 + 0.75 × b / 99`.
- The final color is the palette color times `k`, clamped to 0 to 255.
- These values come from the test. They may be tuned by eye during implementation.

Male and female frames both map from the same (male) reference grids. The test showed this works on both.

**Mirrored directions.** Two of the four facing directions are drawn by flipping the whole character. For
a flipped draw, the painter samples the design flipped (step 3), and the later flip turns it back. So an
emblem reads the right way round in every direction.

**No design yet.** If a design id has no loaded design, the layer draws as the plain cloak until it arrives.

### Editor window (`GuildCloakEditorControl`)

- Wooden frame (`FramedDialogPanelBase`), like the beauty shop and bug report windows.
- **Front** and **Back** canvases, zoomed, drawn from the reference frames. Only pixels inside the
  cloak's outline can be painted. The Front canvas shows a faint body for placement. It draws the collar
  on top of the lining, and a brush paints the collar where a collar pixel exists.
- **Colors:** 6 color boxes. Clicking one selects it. Clicking the selected one opens the color picker from
  the Theatre lighting board (`StageColorPicker`). An empty box adds a color.
- **Tools:** pencil, fill (within one grid), pick color, mirror painting (paints the matching pixel on the
  other half of the same grid too), undo and redo.
- **Live preview:** the beauty shop's character preview (`PreviewView`), showing design id `-1`. It turns
  through the four directions, walks, and switches between body types.
- **Buttons:** "Save draft", "Submit" and "Close". Close warns about unsaved changes.
- **Status line** as in Behavior.

### Review window (`GuildCloakReviewControl`)

- Same wooden frame.
- A list of waiting designs: guild name, leader name and time submitted.
- The selected design shows its Front and Back views (read only), and the same live preview with id `-2`.
- "Approve" and "Reject". Reject opens a reason box (1 to 200 characters).

## Error handling

- The server never trusts the client's rank, deed state or design. It checks all of them on every request.
- Invalid designs are refused with a message and never stored.
- A design request for an unknown id gets no answer. The client keeps drawing the plain cloak.
- A lost or late design reply only delays the look. The client asks again the next time the player comes
  into view.
- The storage saves after every change, like `GuildHouseState`.

## Testing

**Server tests** (TUnit; run with `dotnet run`):

- Save draft, submit, approve and reject change the stored state as described.
- A resubmit replaces the waiting design. A decision on an older submission id is refused.
- Only the leader can save or submit. Only admins can review or clear.
- Validation refuses a wrong grid size, more than 6 colors, and out-of-range color numbers.
- The Tibbs deed takes 5,000,000 gold, sets `cloaks`, and does not morph the map.
- Quill offers the cloak options only when the guild owns the deed, and the design option only to the
  leader.
- Buying a cloak takes 50,000 gold and gives the item.
- Disbanding a guild deletes its cloak data.
- The cloak-look id is the current guild's approved id, or 0.

Two server tests already fail on master (`GiveAbility`, `OnItemDroppedOn` stackable). Leave them alone.

**Client tests:**

- `GuildCloakPainter` gives the expected colors for a small hand-made frame and design.
- Dye-slot pixels take their neighbors' brightness, so the old rune disappears.
- A flipped draw samples the design flipped.
- The layer cache key differs by design id and flip, so a new design never reuses old drawings.
- Every new message reads back exactly as it was written.

**By hand in the game:**

1. Buy the deed from Tibbs as a council member.
2. As the leader, paint a design with an off-center emblem, save a draft, reopen it, then submit.
3. From an admin character, reject it with a reason. Check the leader's message and status line.
4. Resubmit, then approve.
5. Buy cloaks for a male and a female member. Walk, attack and cast in all four directions. Check the
   emblem reads the right way round.
6. Submit a new design while one is approved. Check members keep the old one until approval.
7. Leave the guild while wearing the cloak. Check it goes plain for a nearby viewer at once.
8. Clear the design from the admin trinket. Check the cloaks go plain.

## Rollout

- The client update (drawing code, both windows, the new sprite files) must reach players before the Tibbs
  option goes live. `CONSTANTS.CLIENT_VERSION` goes up by one so older clients cannot log in.
- The accessory batch with the sprite copy ships through `deploy_batch.py`, and the user copies the batch's
  `.dat` files into the local client folder as usual.

## Files (expected)

**Chaos-Server**

- `Chaos.Networking.Abstractions/Definitions/Enums.cs`: new opcodes.
- `Chaos.Networking`: the seven message argument types and their converters.
- `Chaos.DarkAges/Definitions/CONSTANTS.cs`: client version, guild cloak sprite number, grid sizes.
- `Chaos/Models/World/GuildCloakState.cs` (new), `Chaos/SerializationContext.cs`, storage registration.
- `Chaos/Models/World/GuildHouseState.cs`: the `cloaks` property.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs`,
  `GuildManagementScript.cs`, `GuildDisbandScript.cs`, and the join, leave and kick scripts for the refresh.
- `Chaos/Scripting/DialogScripts/Temuair/Generic/AdminTrinketScript.cs`.
- `Chaos/Networking/ChaosWorldClient.cs`, `Chaos/Services/Servers/WorldServer.cs`.
- Tests under `Tests/Chaos.Tests/GuildCloak/`.

**Chaos.Client**

- `Chaos.Client.Rendering/AislingRenderer.cs`, `Chaos.Client.Rendering/GuildCloakPainter.cs` (new).
- A guild cloak design cache next to the other world state.
- `Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakEditorControl.cs` and
  `GuildCloakReviewControl.cs` (new). `StageColorPicker` may move to shared components.
- `Chaos.Client/Screens/WorldScreen.ServerHandlers.cs` and `WorldScreen.Wiring.cs`, plus the networking
  layer for the new messages.
- Tests under `Tests/Chaos.Client.Tests/`.

**Unora**

- The Guild Cloak item template under `Data/Configuration/Templates/Items/Equipment/Temuair/Accessories/`.
- Dialog JSON: Tibbs's cloak deed purchase and confirm; Quill's design, buy and buy-confirm dialogs; the
  admin trinket's review and clear dialogs.
- `Tools/Accessories`: the registry entry and a small build that copies sprite 127's sheets to the new
  number.

## Risks

- **Stretched detail.** Turning and attacking frames squash the emblem. Accepted: large shapes stay
  readable, and the editor's live preview shows it before submitting. What still stretches badly, and the
  approaches to try, are in `2026-09-25-guild-cloak-stretch-notes.md` (deferred work).
- **Bad designs.** Review stops them before anyone sees them. An approved design reported later can be
  cleared.
- **Guild renames.** Designs are keyed by guild name, like the guild hall data. A renamed guild loses its
  design, the same way it loses its hall today.
- **Art changes.** The grid sizes come from sprite 127's art. If that art ever changes, the copy does not,
  because the guild cloak has its own sprite number.
- **Memory.** Each cached cloak layer is about 30 by 45 pixels. A crowded map with a few designs adds a
  few hundred small images at most.
