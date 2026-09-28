# Guild Emblem — Painted Guild Emblems for the World List

Date: 2026-09-28. Spans three repos: Chaos-Server (storage, messages, emblem rules, guild and admin scripts),
Chaos.Client (editor, review window, world list and Emblem tab drawing) and Unora (dialog data).

Builds on: `2026-09-25-guild-cloak-design.md` (the paint → review → download flow) and
`2026-09-26-emblems-design.md` (the world list emblem cell and the Emblem tab).

Mockups from the design session: `.superpowers/brainstorm/133801-1790634407/content/` in Chaos.Client. The approved
ones are layout B in `editor-layout.html` (compact editor) and layout A in `review-window.html` (one mixed review
list). The bottom of `review-window.html` shows the editor with all 6 color boxes.

## Summary

A guild buys a guild emblem from Tibbs. The guild leader paints an 11 × 11 emblem in a small in-game editor and
submits it. An admin approves or rejects it in the same review window that handles guild cloaks. Once approved, the
emblem appears in every member's Emblem tab. A member can show it in their world list cell like any other emblem.

## Why

Emblems today are earned for deeds, one player at a time. A guild has no emblem of its own. Guild cloaks show the
guild in the world; a guild emblem shows it in the world list, where players look for each other.

## Goals

- The leader can paint a clear 11 × 11 icon without art tools, in the game.
- A new or changed emblem shows for everyone as soon as an admin approves it. No client patch or art patch is needed.
- Nothing a player paints is shown to other players until an admin approves it.
- Members choose whether to show it. It never replaces an emblem they chose.

## Non-goals

- No animation. A guild emblem is one frame.
- No emblem on the guild cloak, the hall or the profile.
- No second world list cell. The guild emblem uses the existing emblem cell.
- No fallback for the plain Dark Ages client. All players use Chaos.Client.

## Behavior

### Who can do what

| Action | Who |
|---|---|
| Buy the guild emblem from Tibbs | Council or leader (`IsOfficerRank`), like the other hall additions |
| Open the editor, save a draft, submit | The current leader only (`IsLeaderRank`) |
| Show or hide it in the world list | Any member, once the guild has an approved emblem |
| Review, approve, reject | Admins (`IsAdmin`) |
| Clear a guild's approved emblem | Admins (`IsAdmin`) |

The server checks the role on every request, not only when a window opens.

### Buying

- Tibbs's "Purchase Additions" list gets "Purchase Guild Emblem" while the guild does not own it.
- It costs 5,000,000 gold and goes through the same confirm dialog and guild funds prompt as the other additions.
- The guild gets the same "has purchased" message as for the other additions.
- It is a guild hall property named `emblem`. It does not change the hall map, so `GetMorphCode` ignores it and no
  NPCs are spawned for it.

### Designing

- Quill gets "Design the guild emblem" for the leader, once the guild owns it.
- It opens the editor with the guild's draft. With no draft, it opens the approved emblem. With neither, it opens the
  default: an empty (all see-through) grid with one gold color (RGB 212, 175, 55) in the first box.
- "Save draft" stores the draft on the server. The draft belongs to the guild, so a new leader continues from it.
- "Submit" stores the emblem as the guild's waiting emblem. It replaces any emblem already waiting. Submitting is free.
- An emblem with no painted pixel cannot be submitted: "Paint at least one pixel before submitting."
- The editor's status line shows `Draft`, `Waiting for review`, `Approved` or `Rejected: <reason>`.

### Reviewing

- The admin trinket's "Review guild cloaks" becomes "Review guild designs". It opens the review window with every
  waiting cloak and emblem in one list, oldest first. Each entry is labeled `Cloak` or `Emblem`.
- Approve and reject work exactly as for cloaks: a reject needs a reason of 1 to 200 characters, a decision on a stale
  submission is refused and the list refreshes, and the leader gets an orange-bar message if online.
- The admin trinket's "Clear a guild's cloak" becomes "Clear a guild design". It asks for a guild name, then asks
  "Cloak" or "Emblem", then asks for confirmation.
- Clearing an emblem deletes the guild's approved emblem. Members showing it get an empty cell.

### The member's Emblem tab

- While a player's guild has an approved emblem, their Emblem tab lists it first, before the other owned emblems.
- Its name is the guild's name. The world list hover tooltip shows the same name.
- Its description is "The emblem of your guild." The line under the description is "While you are in <guild>".
- "Time Left" shows "While in guild".
- The Show/Hide button works as for any owned emblem.
- It is never listed as a locked goal. A player whose guild has no approved emblem does not see an entry.

### Auto-show, once per guild

A member is offered their guild's emblem once per guild. The offer happens at the first of these moments:

- an admin approves the guild's emblem while the member is online;
- the member joins a guild that has an approved emblem;
- the member logs in while in a guild that has an approved emblem.

At the offer:

- If the member's world list cell is empty, the guild emblem is shown, and they get "Your guild's emblem is ready."
- Otherwise they get "Your guild's emblem is ready. Show it from the Emblem tab." Their choice is left alone.
- The guild's name is saved in the member's emblem save as the guild already offered. Later approvals of the same
  guild do not offer it again, so a member who hid it stays hidden.

### Changes over time

- A newly approved version replaces the old one for every member showing it. Nobody has to choose it again.
- While a new version waits for review, members keep showing the approved one.
- When a player leaves or is removed from the guild, or the guild disbands, the guild emblem leaves their cell and
  their Emblem tab. Joining another guild with an emblem starts a new offer for that guild.
- A guild rename keeps the emblem, because the admin panel's guild rename already moves the whole guild record.

### Numbers

| Setting | Value |
|---|---|
| Guild emblem purchase | 5,000,000 gold |
| Submitting an emblem | free |
| Size | 11 × 11 pixels |
| Colors per emblem | 1 to 6, any RGB, plus see-through |
| Rejection reason | 1 to 200 characters |
| Save or submit | at most once every 2 seconds per player (the cloak's limit, shared) |

## Architecture

The server stores each guild's emblems next to its cloak designs and decides which emblem id a member shows. The world
list and the Emblem tab carry that id. The client asks for an unknown id once per session, turns the design into an
11 × 11 image, and draws it where it would draw emblem art.

Emblem submissions take their ids from the cloak's existing counter. So a changed emblem always has a new id, clients
never draw an old cached version, and every entry in the mixed review list has a unique id.

## The design

| Part | Size | Meaning |
|---|---|---|
| Palette | 1 to 6 RGB colors | — |
| Grid | 11 × 11 = 121 bytes, row-major | `0` = see-through; `1` to `6` pick a palette color |

- A new shared type `GuildEmblemDesign` in `Chaos.DarkAges/Definitions`, next to `GuildCloakDesign`. It has
  `Colors`, `Pixels`, `CreateDefault()`, `DeepCopy()`, `ContentEquals()`, `IsValid()` and `HasPaint()`.
- `IsValid`: 1 to 6 colors, exactly 121 bytes, every byte 0 to the palette size.
- The size lives as a constant in the shared library, so the client and server agree.
- A whole design is about 140 bytes.

## Server (Chaos-Server)

### Storage (`GuildCloakState`)

- `GuildCloakRecord` gains `Emblem`, a record with the same four slots as the cloak: `Approved`, `Waiting`, `Draft`
  and `LastRejection`, holding `GuildEmblemDesign`s.
- New methods mirror the cloak ones: save emblem draft, submit emblem, approved emblem id of a guild, find an approved
  emblem by id, emblem editor view, clear approved emblem.
- `TryApprove` and `TryReject` find the submission id in either kind. They return which kind it was.
- `WaitingList` returns both kinds, oldest first, each tagged with its kind.
- `RemoveGuild` already removes the whole record, so disbands need no change.
- The saved file stays `GuildCloakState.json`. Old files load with no `Emblem` part, which reads as empty.

### Guild hall property (`GuildHouseState`, `GuildUpdateHallScript`)

- `GuildHouseState` gets an `Emblem` property and the `emblem` name in its property switches.
- `GuildUpdateHallScript` lists "Purchase Guild Emblem" and handles its confirm like the others, at 5,000,000 gold.
  It skips the map morph and NPC spawn for `emblem`.

### `GuildEmblemService` (new)

A singleton next to `GuildCloakService`, sharing its `GuildCloakState` storage and lock. It owns:

- the editor: open, save draft, submit, with the same refusals as the cloak (leader, deed, rate limit, validation);
- design requests by id: answer with the approved design, ignore unknown ids;
- `ApprovedIdFor(Aisling)`: the current guild's approved emblem id, or 0;
- the offer check (see Auto-show), called at login, at join and after an approval;
- after an approval or clear: a fresh `EmblemBook` for each online member, and the offer check after an approval.

`GuildCloakService.HandleReview` and `OpenReview` handle both kinds. After a decision, they call the cloak or emblem
follow-up by kind.

### Emblem rules (`EmblemService`, `EmblemCatalog`, `AislingEmblems`)

- The key `guild` is reserved. `EmblemCatalog` rejects a file with that key and logs the reason.
- A player owns `guild` while `GuildEmblemService.ApprovedIdFor(player)` is not 0. Ownership is live, like record
  emblems. `guild` is never stored in `Owned` or `Revoked`.
- `EmblemChoice` with key `guild` is accepted when the player owns it.
- `AislingEmblems` gains `GuildOffered` (string, the guild name last offered; saved in `emblems.json`).
- The Emblem book lists the guild entry first when owned (see Messages).
- At login, after the existing checks: if `Shown` is `guild` and the player no longer owns it, `Shown` becomes null.
  Then the offer check runs.

### Guild membership hooks (`Guild`)

- `Guild.UnsafeDetach` (every removal path, including disband) and `Guild.UnsafeJoin` already call
  `GuildCloakRefresh.Redisplay`. Next to that call, they call a new static `GuildEmblemRefresh.MembershipChanged`.
- On detach: if `Shown` is `guild`, it becomes null. A fresh `EmblemBook` is sent.
- On join: the offer check runs, and a fresh `EmblemBook` is sent.

### World list (`AislingMapperProfile`)

- If the player's `Shown` is `guild` and they own it: `EmblemArt` 0, `EmblemName` the guild's name, and
  `GuildEmblemId` the approved id. The id is read fresh on every world list, so new versions show at once.
- Otherwise, as today, and `GuildEmblemId` is 0.

### Refusal messages (orange bar)

| Case | Message |
|---|---|
| Not the leader | "Only the guild leader can design the guild emblem." |
| Not owned | "Your guild does not own a guild emblem." |
| Not in a guild | "You are not part of a guild." |
| Too fast | "Please wait a moment before saving again." |
| Invalid design | "That emblem could not be saved." |
| Nothing painted | "Paint at least one pixel before submitting." |
| Stale decision | "This design changed. The list has been refreshed." |

## Messages (Chaos-Server `Chaos.Networking`, shared with the client)

Opcode numbers are chosen in the plan, from the next free values in `ClientOpCode` and `ServerOpCode`.

**New messages**

| Message | Direction | Contents |
|---|---|---|
| `GuildEmblemEditorOpen` | server → leader | The emblem to edit, its status, the last rejection reason if any |
| `GuildEmblemEditorInteraction` | leader → server | Action (`SaveDraft = 0`, `Submit = 1`, `Close = 2`), the emblem for save and submit |
| `GuildEmblemDesignRequest` | client → server | Design id |
| `GuildEmblemDesign` | server → client | Design id, the emblem |

**Changed messages**

- **World list extras record.** After the emblem name, a `u32 guildEmblemId` (0 = none). Readers already skip unknown
  trailing bytes and default missing ones.
- **`EmblemBook` entry.** After `holder`, a `u32 guildEmblemId`. Flags gain bit 2 = guild. The guild entry has key
  `guild`, art 0, the guild's name, the description above, flags owned + guild, `grantedUnix` 0, `secondsLeft` 0 and
  an empty holder.
- **`GuildCloakReviewList` entry.** It starts with a `kind` byte (`Cloak = 0`, `Emblem = 1`), then the cloak design or
  the emblem design by kind.
- `GuildCloakReviewInteraction` does not change: submission ids are unique across both kinds.
- `CONSTANTS.CLIENT_VERSION` goes up by one, so older clients cannot log in.

## Client (Chaos.Client)

### Design store (`GuildEmblemDesigns`, new)

- A per-session store of emblem designs by id, each turned once into an 11 × 11 texture. See-through pixels are
  transparent.
- On a miss it sends one `GuildEmblemDesignRequest`. It asks again for a still-missing id at most once every 10
  seconds.
- Cleared on logout, like `GuildCloakDesignStore`.

### Drawing

- `WorldListEntry` gains `GuildEmblemId`, read from the extras record.
- `EmblemIcon` gains a design id. When it is not 0, it draws the store's texture instead of `emblNNN.spf` art, at the
  same scales and centering. Until the design arrives, the cell is empty.
- The Emblem tab's entries gain the same id. The grid (2×), the large box (3×) and the gold "shown" dot work as for
  any emblem. "Time Left" shows "While in guild".

### Editor window (`GuildEmblemEditorControl`, new; layout B)

- The wooden frame (`FramedDialogPanelBase`), like the cloak editor, but compact. Title "Guild Emblem — <guild>".
- **Canvas:** the 11 × 11 grid at 16× (176 px), on a checker background so see-through pixels read as empty.
- **Colors:** a see-through box, then 6 color boxes in one row. Clicking a box selects it. Clicking the selected color
  box opens `StageColorPicker`. An unused box is dashed; clicking it adds a color.
- **Tools** in rows under the colors: pencil, fill, pick color, mirror (paints the matching pixel on the other half,
  left to right), undo, redo.
- **Previews** in one strip: a world list row at 1×, then the emblem at 2× and 3×. They draw the live draft directly.
- **Buttons:** Close, Save draft, Submit. Close warns about unsaved changes.
- **Status line** as in Behavior.
- It reuses the cloak editor's pieces where they fit: color boxes, color picker, undo and redo, and the send cooldown.

### Review window (`GuildCloakReviewControl`)

- Title "Guild Design Review".
- Each list entry shows its kind before the guild name.
- Selecting a cloak shows the cloak views, as today. Selecting an emblem hides them and shows the emblem view: the
  grid enlarged (12×, 132 px) on a checker background, and the world list row at 1× with the emblem at 2× and 3×.
- Approve and Reject work the same for both kinds.

## Error handling

- The server never trusts the client's rank, ownership state or design. It checks all of them on every request.
- Invalid emblems are refused with a message and never stored.
- A design request for an unknown id gets no answer. The client leaves the cell empty and may ask again later.
- A saved `Shown` of `guild` never stores a design id. The id is looked up live, so a cleared or replaced emblem
  can never leave a stale id behind.
- The storage saves after every change, like the cloak.

## Testing

**Server tests** (TUnit; run with `dotnet run`):

- Save draft, submit, approve and reject change the emblem slots as described. Cloak slots are untouched.
- Emblem and cloak submissions share the id counter. A decision finds the right kind by id.
- A resubmit replaces the waiting emblem. A decision on an older id is refused.
- Only the leader can save or submit. Only admins can review or clear.
- Validation refuses a wrong grid size, more than 6 colors, out-of-range values, and an unpainted submit.
- The Tibbs purchase takes 5,000,000 gold, sets `emblem`, and does not morph the map.
- Quill offers "Design the guild emblem" only to the leader, and only when the guild owns it.
- `EmblemCatalog` rejects a file with key `guild`.
- The Emblem book lists the guild entry first while owned, and not at all otherwise.
- `EmblemChoice` accepts `guild` only when owned.
- The offer: auto-shows into an empty cell; leaves a chosen emblem alone; happens once per guild; happens at login for
  a member who was offline at approval.
- Leaving the guild clears a shown `guild`. Joining another guild with an emblem offers again.
- The world list sends the current approved id for a member showing `guild`, and 0 otherwise.
- Round trips: world list records with and without the new id, `EmblemBook` with a guild entry, the review list with
  both kinds, and the four new messages.

Two server tests already fail on master (`GiveAbility`, `OnItemDroppedOn` stackable). Leave them alone.

**Client tests:**

- The design store's texture: see-through pixels are transparent, others match the palette.
- The store asks once per id, then again only after 10 seconds.
- Reading world list records and Emblem book entries with and without the new id.
- The editor's mirror paints the matching column (x ↔ 10 − x).
- The review window picks the emblem view for an emblem entry and the cloak views for a cloak entry.

**By hand in the game:**

1. Buy the guild emblem from Tibbs as a council member.
2. As the leader, paint an emblem with an off-center detail, save a draft, reopen it, then submit.
3. From an admin character, reject it with a reason. Check the leader's message and status line.
4. Resubmit, then approve. Check a member with an empty cell sees it auto-shown, and a member with a chosen emblem
   keeps theirs and gets the "Show it from the Emblem tab" message.
5. Check the world list shows it at 1× with the guild name on hover, and the Emblem tab at 2× and 3×.
6. Hide it, then approve a new version. Check it stays hidden for that member and updates for members showing it.
7. Log in a member who was offline at the first approval. Check the offer happens.
8. Leave the guild. Check the cell empties at the next world list.
9. Clear the emblem from the admin trinket. Check members' cells empty.
10. Check a cloak still reviews, approves and clears as before.

## Rollout

- The client update must reach players before the Tibbs option goes live. `CLIENT_VERSION` goes up by one, so server
  and client ship together.
- No `setoa.dat` or other art ships with this change.
- Unora dialog JSON ships with the server data.
- Work on a `feat/guild-emblem` branch in each repo. Merge the server first, then point the client's `Chaos-Server`
  submodule at it.
- Several sessions share these checkouts, so stage by explicit path. Never commit `launchSettings.json` or a local
  `StagingDirectory`.

## Files (expected)

**Chaos-Server**

- `Chaos.Networking.Abstractions/Definitions/Enums.cs`: new opcodes.
- `Chaos.Networking`: the four new message types and converters; changes to `WorldListConverter`,
  `WorldListMemberInfo`, the `EmblemBook` converter and entry, and the review list entry and converter.
- `Chaos.DarkAges/Definitions/GuildEmblemDesign.cs` (new), `CONSTANTS.cs` (client version, grid size).
- `Chaos/Models/World/GuildCloakState.cs`, `GuildHouseState.cs`.
- `Chaos/Services/GuildCloak/GuildEmblemService.cs`, `GuildEmblemRefresh.cs` (new); `GuildCloakService.cs` (review by
  kind).
- `Chaos/Services/Emblems/EmblemService.cs`, `EmblemCatalog.cs`, `AislingEmblems.cs`, `AislingEmblemsSchema.cs`.
- `Chaos/Collections/Guild.cs` (two hook calls).
- `Chaos/Services/MapperProfiles/AislingMapperProfile.cs`.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs`, `GuildCloakScript.cs` (or a new
  `GuildEmblemScript.cs`), `Generic/GuildCloakAdminScript.cs`.
- `Chaos/Services/Servers/WorldServer.cs`, `Chaos/Networking/ChaosWorldClient.cs`.
- Tests under `Tests/Chaos.Tests/GuildEmblem/`, plus the existing emblem and converter tests.

**Chaos.Client**

- `Chaos.Client/Controls/World/Emblems/GuildEmblemDesigns.cs` (new), `EmblemIcon.cs`.
- `Chaos.Client/Controls/World/Popups/GuildEmblem/GuildEmblemEditorControl.cs` and `GuildEmblemCanvas.cs` (new).
- `Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakReviewControl.cs`.
- The Emblem tab (`SelfProfileEmblemTab`) and `WorldListEntryControl`.
- `Chaos.Client/Models/WorldListEntry.cs`, `Collections/WorldState.cs`.
- `Chaos.Client.Networking/ConnectionManager` handlers; `Screens/WorldScreen.ServerHandlers.cs`, `WorldScreen.Wiring.cs`
  (or a new `WorldScreen.GuildEmblem.cs`).
- Tests under `Tests/Chaos.Client.Tests/`.

**Unora**

- Dialog JSON: Tibbs's emblem purchase and confirm; Quill's "Design the guild emblem"; the admin trinket's renamed
  review option and the clear flow's Cloak/Emblem choice.

## Risks

- **Tiny canvas.** 11 × 11 allows little detail. Accepted: the Korean emblems are the same size, and the editor
  previews every size the game uses.
- **Bad emblems.** Review stops them before anyone sees them. An approved emblem reported later can be cleared.
- **Shared review window.** The cloak review control grows a second view. If it gets hard to follow, the emblem view
  can move into its own child control inside the window.
- **Shared storage file.** A bug in the emblem code could damage cloak data in `GuildCloakState.json`. The storage
  tests cover both kinds side by side.
