# Haunted Theatre — Design

**Date:** 2026-10-01
**Repos:** Chaos-Server, Unora (no client change)
**Status:** approved in brainstorming. This is sub-project 2 of `2026-09-30-mirror-maze-design.md` (section 5), written out in full.

## 1. Summary

During the Halloween window (Oct 4 06:00 UTC to Nov 4 06:00 UTC) the Suomi theatre is replaced by a haunted copy of
itself: the same 20x31 room, redecorated, with a spooky atmosphere and a few harmless wandering ghosts. All director
tools keep working. The mirror maze entrance moves to the haunted copy. Outside the window nothing changes: the
normal theatre is the only one open.

Sub-project 3 (theatre games) is not part of this spec.

## 2. Decisions

| Question | Decision |
|---|---|
| Scope | Full spec 5: new map, theatre swap, ghosts, mirror entrance moved, effect flags on. |
| How the swap works | **Two map instances and one helper** (approach 1 below). |
| Decorations | A script places a first pass of existing Halloween tiles and renders a preview. The user then tweaks the map in ChaosAssetManager, as with the maze. |
| Baseline effects | `Ghosts` and `HarvestMoon`. `Bats` stays on the director's menu only. |
| Director tools | All kept. The haunted map runs the same `suomitheatremap` script. |
| Backstage | Unchanged. It is shared by both theatres. |
| Ghosts | Harmless. They wander, cannot be fought, never attack. |

## 3. Approaches considered for the swap

1. **Chosen: two instances, one helper.** `suomi_theatre` and `suomi_theatre_halloween` both exist. A static
   `SuomiTheatre` class answers "is this map a theatre?" and "which theatre is open now?". Every hard-coded
   `"suomi_theatre"` goes through it.
2. **Rejected: swap the tiles under the one `suomi_theatre` id when the window opens.** Needs a live map reload,
   and the reactors, merchants and mirror list differ too. Risky on a running server.
3. **Rejected: load the Halloween data under the `suomi_theatre` id at server start.** Simple, but it needs a server
   restart on Oct 4 and again on Nov 4.

## 4. Server (Chaos-Server)

### 4.1 `SuomiTheatre` helper

New file `Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatre.cs` (beside `TheatreStageEffects`):

- `NORMAL_ID = "suomi_theatre"`, `HALLOWEEN_ID = "suomi_theatre_halloween"`.
- `IsTheatre(string instanceId)`: true for either id (case-insensitive).
- `OpenInstanceId(DateTime nowUtc)`: `HALLOWEEN_ID` when `EventPeriod.IsSpecificEventActive(nowUtc, HALLOWEEN_ID,
  EventType.Halloween)`, otherwise `NORMAL_ID`.

`EventPeriod`'s Halloween list gains `suomi_theatre_halloween`, so the dates stay in one place.

### 4.2 Call sites

Two kinds of use, and they change differently:

- **"Is the player in the theatre?" checks** use `SuomiTheatre.IsTheatre(...)`. These are the `LoadedFromInstanceId` /
  `InstanceId` comparisons in `SuomiTheatreScript` (invite checks, director-only options, the stage lighting and
  effects options), `LanternDialogScript`, `TheatreSilenceEffect` and `TerminusTheatrePortScript`.
- **"Send the player to the theatre" uses** call `SimpleCache.Get<MapInstance>(SuomiTheatre.OpenInstanceId(now))`.
  These are the Suomi door (`suomi` reactors), the Terminus theatre port, the stage and backstage invites in
  `SuomiTheatreScript` that fetch the theatre map, and the backstage exit warp.

Some warps are reactors with a fixed `suomi_theatre:(x, y)` destination: the ten backstage exit tiles
(`suomi_theatre_backstage/reactors.json`), the Suomi town door (`suomi/reactors.json`) and the maze's return warp. During
the window they would send players to the closed map. The plan decides whether these become a script that asks the
helper, or whether the stock `warp` script learns to resolve a theatre destination. Either way it gets a test.

The admin map list in `DisplayMapListCommand` gets the new id added.

### 4.3 Moving players when the window changes

Each theatre's `SuomiTheatreMapScript.Update` already runs once a second. It gains one check: if
`SuomiTheatre.OpenInstanceId(now)` is not this map's own id, move every Aisling on the map to the same tile on the
open theatre. The two layouts match, so nobody lands in a wall. Admins and directors are moved too.

Lighting state belongs to each map's script, so it starts fresh on the other map, as it does after a server restart.
Saved scenes are shared by both maps already.

### 4.4 Effects and darkness

`TrySyncDarkness` only sets and clears the Darkness bits (bits 0 and 1), so the Halloween effect flags (higher bits)
are not touched by the house lights. "Clear All Effects" on the director's menu clears the baseline `Ghosts` and
`HarvestMoon` as well. That is intended: the director owns the stage mood. Restarting the server restores the baseline.

## 5. Content (Unora)

### 5.1 New instance

`Data/Configuration/MapInstances/Temuair/Events/Halloween/Theatre_Halloween/`:

- `instance.json`: `instanceId` `suomi_theatre_halloween`, name "Garamonde Theatre" (same as the normal one, so the
  swap is invisible in the name), `scriptKeys` `suomitheatremap` and `mirrormap`, `flags` `"Ghosts, HarvestMoon"`,
  `templateKey` of the new map.
- `reactors.json`: a copy of the normal theatre's reactors (exits to Suomi, backstage warps, the bulletin board)
  plus the mirror door from the normal theatre.
- `merchants.json`: Thulin at (1, 9), as in the normal theatre, plus the ghosts.
- `mirrors.json`: the `theatre-mirror` segment from the normal theatre.
- `monsters.json`: empty.

The normal theatre's `suomi_theatre` loses its mirror door reactor and its `mirrors.json` segment. Outside the window
it had nothing to open onto.

### 5.2 New map

- `Tools/HauntedTheatre/decorate_theatre.py` reads `lod346.map` and writes the new `lod<id>.map` and
  `Templates/Maps/<id>.json` (20x31).
- The script keeps clear: the stage rectangle `(0, 12)` to `(8, 20)`, every reactor tile, Thulin's tile, the
  director's spots and the mirror door tile. It checks this and fails if a decoration lands on one of them.
- It places existing Halloween tiles from the Macabre art (cobwebs, pumpkins, graves, candles, a darker floor).
  The exact tile numbers are chosen in the plan by rendering candidates.
- `Tools/MirrorMaze/render_map.cs` renders a preview picture for review. The user then edits the map in
  ChaosAssetManager. Because the script can write the map again, it overwrites hand edits. The README says to run it
  once and then edit by hand.
- The map number is the first number that is free in both `Data/Configuration/MapData` and `UnusedMapData`
  (`lod10232` is taken in `UnusedMapData`).

### 5.3 Ghosts

- A few merchant templates (for example `theatre_ghost_1`..`3`), each with an existing ghost sprite and the
  `wander` script key and a `wanderIntervalMs` (about 1500, as the Paradise fish use), a name like "Restless
  Spirit", and no items. The plan picks the sprites by looking at what
  exists (for example the House Macabre ghost sprites).
- They spawn in the house (away from the stage) in `merchants.json`.
- They are merchants, so they cannot be fought. Clicking one gives a short line of flavour text.
- The mirror renderer already reflects creatures, so ghosts show in the theatre mirror.

### 5.4 Maze return warp

`Tools/MirrorMaze/generate_maze.py` writes the maze's exit mirror with `THEATRE_RETURN = "suomi_theatre:(6, 4)"`. Inside
the window that sends players to the closed theatre. The maze's return reactor is changed to ask `SuomiTheatre` at
the moment of use (the same fix as 4.2), and the generator is updated to match.

## 6. Tests

**Server**
- `SuomiTheatre.IsTheatre`: both ids, either case, and other ids (including `suomi_theatre_backstage`).
- `SuomiTheatre.OpenInstanceId`: just before the window, at its start, inside, at its end, and after.
- Map script: a player on the closed theatre is moved to the same tile on the open one; a player on the open
  theatre stays.
- Existing theatre tests (`SuomiTheatreMapScriptTests`, `TheatreStageEffectsTests`, lighting and lantern tests) stay
  green.

**Unora (script checks)**
- `decorate_theatre.py --check` fails on a decoration over a protected tile, and passes on the real layout.
- Every reactor and merchant in the new instance sits on a walkable tile.

## 7. In-game check (by the user)

1. Before the window (or with the dates moved in a test build): only the normal theatre opens from every door.
2. Inside the window: the Suomi door, Terminus and invites all go to the haunted theatre.
3. Stand in the normal theatre when the window opens: you are moved to the same tile in the haunted one.
4. Run the director tools in the haunted theatre: lights, scenes, effects, silence, lanterns.
5. See the ghosts wandering. Check you cannot attack them.
6. Use the mirror door to the maze and the maze exit back: you land in the haunted theatre.
7. When the window closes, players inside are moved back to the normal theatre.

## 8. Deployment

Merge order: server, then Unora. There is no client change and no `CLIENT_VERSION` change. If the map art needs new
tiles later, that is a separate launcher patch.

## 9. Open items for the plan

- The new map number.
- Which Halloween tile numbers to use for each decoration.
- Which ghost sprites to use.
- How the fixed-destination warps (Suomi door, backstage exit, maze return) ask the helper: a new reactor script, or
  a change to the existing warp script.
