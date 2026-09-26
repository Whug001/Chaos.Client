# Emblems (spec 2 of 2)

Date: 2026-09-26
Repos: Chaos-Server (emblem rules, storage, packets), Unora (emblem files, art build), Chaos.Client (Emblem tab,
world list cell)
Builds on: spec 1, `2026-09-26-worldlist-drawer-design.md`, branch `feat/worldlist-drawer`

## Summary

Players earn emblems for things they do in the game. Each player can show one emblem in the outer icon cell of
their world list row. Spec 1 left that cell empty.

Most emblems come from legend marks the game already gives. So players who did something before launch get the
emblem at their first login. Other emblems go to whoever is first on a leaderboard, and staff can grant the rest.
Some emblems last forever, some expire after a set number of days, and record emblems move when someone takes first
place.

Players browse their emblems in the profile's unused Album tab, which becomes an Emblem tab. The page uses the
Korean client's emblem book art (`_nui_ebl`), simplified and relabeled in English. Locked emblems show dimmed as
goals, except secret ones, which stay hidden until earned.

Each emblem is one JSON file. Staff add an emblem by writing a file and running `/reload emblems`, with no code
change and no restart.

## Goals

- Adding an emblem is data work: one file, one reload.
- Past achievements count. Veterans find their emblems waiting at launch.
- The book matches the old client UI. Labels are carved art, not client-drawn text.
- The server and client can update in either order without breaking the world list.

## Not in this spec

- Showing emblems anywhere except the world list and your own Emblem tab. Other players' profiles don't show them.
- The 30 rank icons (`rank001`–`rank030`) and the combined `emblem.spf`. They're not used.
- New emblem art. This spec ships the Korean set as it is.
- A full launch catalog. This spec ships five example emblems, one of each kind. More are added later as files.

## Decisions

| Question | Decision |
|---|---|
| Past achievements | Retroactive. Login grants every emblem the player already qualifies for |
| Launch set | Five examples, one per kind |
| How earning works | Each emblem file names its source. One server service checks the rules |
| Record emblems | First place only. Ownership is worked out live from the board |
| Expiry | A set number of days after the emblem is granted |
| Auto-show | A new emblem shows by itself only if the player's cell is empty |
| Where the book lives | The profile's Album tab becomes the Emblem tab |
| Opening from the world list | Double-click your own row |
| Emblem size in the book grid | 2× (22 px) |
| Right page top row | Removed. The panels below move up 24 px |

## Emblem files (Unora)

Each emblem is one file in `Data/Configuration/Templates/Emblems/`. The file name doesn't matter, but by
convention it's `<key>.json`.

```json
{
  "key": "carnun",
  "art": 2,
  "name": "Carnun Slayer",
  "description": "Challenge the Carnun and declare victory.",
  "secret": false,
  "durationDays": null,
  "source": { "legendMark": "carnunwon" }
}
```

| Field | Meaning |
|---|---|
| `key` | Stable id. Saves refer to it, so never change it after launch. Case-insensitive |
| `art` | Korean emblem number: 1–173 or 181–183 |
| `name` | Shown in the book. Up to 28 characters |
| `description` | How the emblem is earned. Shown in the book, including for locked emblems |
| `secret` | Optional, default false. If true, the book hides it until it's earned |
| `durationDays` | Optional. If set, the emblem expires this many days after it's granted |
| `source` | Exactly one of the three forms below |

Sources:

- `{ "legendMark": "<key>" }`: granted when the player has a legend mark with this key, ignoring case.
- `{ "leaderboard": "<board>" }`: held by whoever is first on that board. `durationDays` is not allowed.
- `{ "staff": true }`: only granted by `/emblem give`.

Book order is file order. Files are read in name order, so a numeric prefix orders them if needed.

### The five examples

| File | Kind | Source |
|---|---|---|
| `carnun.json` | Permanent | Legend mark `carnunwon` (Challenged the Carnun and declared victory) |
| `fisksecret.json` | Secret | Legend mark `FiskSecret` (Kept Fisk's Secret) |
| `newyear2026.json` | Expiring, 30 days | Legend mark `2026` (Celebrated the New Year 2026) |
| `pitfightchampion.json` | Record | Board `pitFight` |
| `staffaward.json` | Staff only | `staff` |

Art numbers for these are picked from the review sheet (see Art) when the files are written.

## Server (Chaos-Server)

### Loading emblem files

- A new `EmblemTemplate` and its schema. They load at startup from the folder above, the same way other templates
  load.
- Validation skips a bad file and logs one error naming the file and the problem. A bad file is one with:
  - a duplicate key
  - an `art` number outside the valid set
  - a missing name
  - anything other than exactly one source
  - an unknown board
  - `durationDays` on a record emblem
- `ReloadCommand` gains an `emblems` case. It reloads the files and then re-checks every online player.

### Per-player storage: `emblems.json`

`AislingStore` saves and loads a new `emblems.json` next to `legend.json`. It holds:

```
Shown:    string?                       // key of the emblem in the world list cell, or null
Owned:    key -> { Granted: DateTime, Expires: DateTime? }
Revoked:  set of keys                   // expired or taken by staff; blocks automatic re-grant
```

- Record emblems never appear in `Owned` or `Revoked`. Ownership of those is live (see below).
- An entry whose key has no loaded file is kept in the save but ignored everywhere. If `Shown` names it, the cell
  is empty.
- A missing file loads as empty. So existing characters need no migration.

### Record holders

A small adapter per board answers "who is first right now?":

| Board id | Storage object | First place means |
|---|---|---|
| `pitFight` | `PitFightLeaderboardObj` | Most `Victories` |
| `damageDummy` | `DamageGameObj` | Highest `Damage` |
| `hopocalypse` | `HopocalypseLeaderboardObj` | Highest `HighestLevelAchieved` |
| `halloween` | `HalloweenChallengeLeaderboardObj` | Highest `HighestWaveAchieved` |
| `frosty` | `FrostyChallengeLeaderboardObj` | Lowest `Seconds` |

- Each record emblem's holder name is saved in `LocalStorage/EmblemRecords.json`.
- **Ties.** The current holder keeps the emblem. A challenger needs a strictly better score. With no holder saved,
  the tie goes to the first name in ordinal order, so the result is stable.
- An empty or reset board has no holder.
- A player owns a record emblem while their name matches its saved holder, ignoring case. Nothing is written to
  the old holder's save when the record moves, so offline players need no handling.

### `EmblemService`

One singleton service owns the rules. It runs at these points:

- **Login.** It grants every legend-mark emblem the player qualifies for and hasn't had revoked. Then it moves
  expired entries from `Owned` to `Revoked`. Last, if `Shown` names anything the player no longer owns, it becomes
  null. That includes an expired emblem and a record lost while offline.
- **Legend mark added.** `Legend` gains a `MarkAdded` event. It's raised by `AddOrAccumulate` and `AddUnique` when a
  mark is new, not when a count goes up. The player's `Legend` wires it to the service.
- **Board saved.** Each leaderboard script calls `EmblemService.OnBoardChanged(boardId)` after saving. The service
  recomputes first place and updates `EmblemRecords.json`. If the holder changed:
  - The new holder, if online, is treated as a grant (message and auto-show, below).
  - The old holder, if online, gets "You lost the <name> emblem." If it was shown, `Shown` becomes null.
  - Both get a fresh `EmblemBook` if online.
- **Reload.** After `/reload emblems`, it runs the login check for every online player.
- **Staff command:**
  - `/emblem give <player> <key>` grants the emblem. It clears the key from `Revoked` and sets a fresh duration.
  - `/emblem take <player> <key>` removes it and adds the key to `Revoked`.
  - Both refuse record emblems and unknown keys, and both work on online players only.

On every grant:

- `Granted` is now. `Expires` is now plus `durationDays`, if set. A retroactive grant counts from the grant, not
  from the mark's date. Otherwise old expiring emblems would be gone on arrival.
- The player gets a server message: "You earned the <name> emblem."
- If `Shown` is null, it becomes this key.

When the shown emblem expires, is taken, or its record is lost, `Shown` becomes null. The next emblem earned then
shows by itself.

**Expiry between checks.** Anything that reads ownership treats an entry past `Expires` as not owned. That covers
the world list and the book, so an expired emblem never shows, even before the next login moves it to `Revoked`.

### Packets

**World list extras record.** It grows from 3 bytes to 5:

```
byte  length        // 5 now
byte  advClass
byte  continent
byte  flags
u16   emblemArt     // 0 = no emblem; the shown emblem's art number
```

- `WorldListMemberInfo` gains `EmblemArt`, filled from the service, which applies the expiry and record rules.
- Readers already skip unknown trailing bytes and default missing ones. So a 3-byte record reads as no emblem, and
  an old client ignores the two new bytes.

**Emblem book.** It uses three new opcodes, each the next free number on master when the work is done:

1. `ClientOpCode.EmblemBookRequest`: no body. The client sends it when the Emblem tab opens.
2. `ServerOpCode.EmblemBook`: sent in reply, and again whenever the player's emblems change while they're online.
   ```
   string8  shownKey                // empty = none
   u16      count
   repeat count:
     string8  key
     u16      art
     string8  name
     string16 description
     byte     flags                 // bit 0 owned, bit 1 record
     u32      grantedUnix           // grant time in Unix seconds; 0 for locked and record emblems
     u32      secondsLeft           // 0 = never expires
     string8  holder                // record emblems: current first place, or empty
   ```
   - Unowned secret emblems are left out, so the client never learns they exist.
   - Order: owned emblems in file order, then locked ones in file order.
3. `ClientOpCode.EmblemChoice`: `string8 key`, where empty means hide.
   - The server checks ownership, including expiry and records. It ignores keys the player doesn't own.
   - Then it saves `Shown` and sends a fresh `EmblemBook`. The cell changes the next time anyone's world list is
     sent.

## Art (Unora)

A new `Tools/Emblems/` folder follows `Tools/WorldList/`. It has sources, indexed-PNG art, a build script that
writes into `setoa.dat` and reads everything back, and review PNGs.

- **`_nui_ebl.spf` (599×306).** Source: `Desktop/Emblem Assets/Emblem UI/_nui_ebl.spf`. Changes:
  - **Top row removed.** Remove the right page's top row: the Name button, the wings, two small boxes and the long
    field. Move the panels below it (y 58–245) up 24 px. Fill the freed band at the bottom with the blank panel
    texture from below the description box. The approved mock is `ebl-right-up.png` in the brainstorm session.
  - **Labels.** Replace `이전 페이지` and `다음 페이지` with carved "Prev Page" and "Next Page", keeping the arrows.
    Replace the `남은기간` button face with "Time Left".
  - **Carving method.** Letters are cut from existing silver-button art, such as Name, Class, Guild, Title and
    Presentation. Missing letters (v, x and any others) are drawn by hand in the same style, as in spec 1's
    `letters.py`.
- **Show/Hide button (`_nui_eblb.spf`).** New stone button frames: Show, Show lit, Hide, Hide lit. They match the
  size and style of the old Name button.
- **Emblem tab.** A new frame in `_nui_tb2.spf`: "Emblem", carved from the existing tab labels. E comes from Event,
  m from Family, b and l from Album, and e from Legend. `TAB_ALBUM` in `_nui.txt` points to the new frame.
- **Emblems.** `embl001.spf`–`embl173.spf` and `embl181.spf`–`embl183.spf` (176 files) go in unchanged. The client
  loads `emblNNN.spf` by art number.
- **Review sheet.** The tool writes `review/emblems.png`, every emblem at 3× with its number under it, for picking
  art numbers.

## Client (Chaos.Client)

### Data

- `WorldListEntry` gains `EmblemArt`, read from the extras record.
- `WorldState` holds the last `EmblemBook`: the shown key and the entries. It raises an event when a new one
  arrives. It records the arrival time so the client can count `secondsLeft` down.

### Emblem art

- A small loader returns the frames for an art number from `emblNNN.spf` and caches them.
- Animated emblems advance one frame every 120 ms, from a shared clock, so every copy of an emblem stays in step.
  The timing is adjusted by eye during the in-game check.
- Emblems narrower or shorter than their box are centered in it.

### World list

- `WorldListEntryControl` draws the emblem at 1× in the 11×11 cell at x 293, row top, centered.
- **Double-click.** Double-clicking your own row opens your profile on the Emblem tab. Double-clicking any other
  row still whispers that player.

### Emblem tab: `SelfProfileEmblemTab`

It replaces `SelfProfileBlankTab` for `StatusBookTab.Album`, and its prefab is `_nui_ebl`.

- **Opening.** Opening the tab sends `EmblemBookRequest`. The page redraws each time an `EmblemBook` arrives. It
  keeps the current page and selection when the selected key is still present. Otherwise it selects the first
  entry.
- **Left page:**
  - A 5×5 grid, 25 per page, with slots at x 45 + 43·col, y 43 + 45·row, each 36×36.
  - Emblems are drawn at 2×. Locked emblems are drawn dimmed.
  - A gold outline marks the selected slot. A small gold dot in the top-right corner marks the shown emblem.
  - Prev Page and Next Page wrap around. The page box shows "1/3".
  - An empty book shows page "1/1" and an empty right page.
- **Right page** (positions after the 24 px move):
  - The selected emblem at 3× in the large box.
  - Its name in the field beside it.
  - The "Time Left" field shows one of:
    - "Never"
    - a countdown, such as "12d 4h", or "3h 20m" under a day
    - "While first place", for record emblems
    - "Locked", for locked emblems
  - The description box shows the description, a blank line, and then one of:
    - "Earned <yyyy-MM-dd>", from `grantedUnix`, for owned non-record emblems
    - "Held by <name>", for record emblems with a holder
    - "Locked"
- **Show/Hide button.**
  - It shows "Hide" when the selected emblem is the shown one, and "Show" otherwise.
  - It's disabled for locked emblems.
  - Clicking it sends `EmblemChoice` with the key, or with an empty key for Hide.
- `controlFileList.txt` gets an `_nui_ebl` entry for the new rects.

## Edge cases

- **A file is deleted or its key renamed.** Owned entries for it are ignored. A shown one leaves the cell empty.
  Renaming a key after launch orphans every player's entry, so keys never change.
- **A legend mark is removed later,** for example on divorce. The emblem stays. Emblems are earned once, and staff
  can take them.
- **An emblem expires or is taken.** It's added to `Revoked`, so the mark doesn't grant it again. `/emblem give`
  clears that.
- **Record ties, and empty boards.** See Record holders.
- **The record holder renames.** The holder name no longer matches, so nobody holds the record until the board
  next changes. Boards are keyed by name, so this matches how the boards themselves behave.
- **Mismatched versions.**
  - An old client ignores the new world list bytes and never sends the new opcodes.
  - A new client on an old server reads no emblems. Its Emblem tab stays empty, because no reply comes.
- **Hidden admins** stay hidden from the world list, as today.

## Testing

- **Server (Chaos.Tests):**
  - Login grants mark-based emblems, including ones earned before launch.
  - No re-grant after an emblem expires or is taken, and give clears that.
  - The duration counts from the grant.
  - An expired entry reads as not owned before the next login.
  - Auto-show happens only when `Shown` is null.
  - The record adapters pick first place, including lowest-is-best for `frosty`.
  - The tie rule keeps the current holder.
  - Template validation rejects each bad case with a named error.
  - Round trips for 3-byte and 5-byte world list records, `EmblemBook` (with a secret locked emblem left out), and
    `EmblemChoice`.
  - `EmblemChoice` ignores unowned keys.
- **Client:**
  - Reading 3-byte and 5-byte records.
  - Paging and wrap-around.
  - The "Time Left" text for each case.
  - Selection kept across a refresh.
  - The gold dot's slot.
- **Art:** the build script reads every file back from `setoa.dat`. Review PNGs cover the book, the button and the
  tab.
- **In-game check** with a test character:
  - Add `carnunwon` and see the emblem arrive and auto-show.
  - Use `/emblem give` and `/emblem take`.
  - Take first place on the pit fight board with a second character.
  - Use a test emblem with `durationDays: 1` and a shortened clock to watch expiry.
  - Run `/reload emblems` after adding a file.
  - Check animated emblems in the grid and the world list.

## Rollout and build notes

- Work on a new `feat/emblems` branch in each repo, based on `feat/worldlist-drawer`. The two ship together,
  because emblems extend spec 1's world list record.
- Merge the server first, then point the client's `Chaos-Server` submodule at it.
- Ship the client and `setoa.dat` in one launcher patch. The server can go before or after.
- The emblem JSON files deploy with the server data.
- Stop any running `Chaos.exe` or client before building, because they lock `bin`. Never build both solutions at
  once.
- Several sessions share these checkouts, so stage by explicit path. Never commit `launchSettings.json` or a local
  `StagingDirectory`.

## How to add an emblem (goes into Unora `docs/emblems.md`)

1. **Pick the art.** Choose a number from `Tools/Emblems/review/emblems.png`.
2. **Find the source.**
   - For a legend mark, use its key: the second text in the script's `new LegendMark(...)` call.
   - For a record, use a board id from the table above.
   - For a staff emblem, use `"staff": true`.
3. **Write the file.** Save `<key>.json` in `Data/Configuration/Templates/Emblems/`. Add `"secret": true` or
   `"durationDays": N` if needed.
4. **Load it.** Run `/reload emblems` in game. Online players who qualify get it straight away.

Limits:

- Art must be one of the 176 Korean emblems. New art needs a client patch.
- An achievement with no legend mark needs a line of script. The script either gives a mark or calls the service
  to grant the emblem.
