# Quest log window — design

Date: 2026-09-27
Repos: Chaos-Server, Chaos.Client, Unora (quest entries, dialogs, art tools and setoa.dat)

## Goal

Add a quest log window to the client, built from the Legend of Darkness (LoD) "Quest List" art. It lists
every quest the player has in progress. The details pane shows the selected quest's description and live progress
counts. A quest that is safe to abandon can be given up from the window. A "Q" button on the HUD opens it.

## Source art

The art is in `C:\Users\Michael\Desktop\Quest`. Every file is a palettized SPF with no frame offsets.

| File | Frames | Size | Use |
|---|---|---|---|
| `q_list.spf` | 1 | 247×307 | Full window: title bar, list pane, details pane, scroll strip, two button slots |
| `q_min.spf` | 1 | 247×32 | Minimized window: title bar only, with two button slots |
| `q_close.spf` | 2 | 50×20 | "Close" button, normal / pressed |
| `q_gvp.spf` | 2 | 50×20 | "GiveUp" button, normal / pressed-or-disabled |
| `under_bn.spf` | 2 | 18×18 | Minimize button ("_") |
| `full_bn.spf` | 2 | 18×18 | Restore button |
| `x_bn.spf` | 2 | 18×18 | Close ("X") button |
| `q_btm.spf` | 2 | 18×18 | "Q" HUD button, normal / pressed |

Measured positions in `q_list` (window pixels, left/top inclusive):

- List pane interior: x 16–209, y 35–124. About 7 rows of 12 px.
- Details pane interior: x 16–209, y 128–272.
- Scroll strip: x 213–230, y 35–272.
- Button slots: GiveUp at (71, 282), Close at (135, 282), each 50×20.
- Title-bar buttons: minimize at about (196, 8) and X at about (216, 8) on `q_list`. On `q_min`, restore sits in the
  slot at x 185–204 and X in the slot at x 212–231, both at y 7–27. Final pixel positions are set by eye in game.

## Decisions made

| Question | Decision |
|---|---|
| Which quests | All of them at launch (about 100). Entries are drafted from each quest's script and dialogs, then reviewed by the user |
| Where entries live | JSON data files in Unora, one per quest. No quest script is changed |
| What the list shows | Active quests only. A finished quest drops off. Cooldowns are not shown |
| Progress | Live item and counter counts, such as "Beetle Horns: 3 / 5" |
| GiveUp | Resets the quest to not started, only for quests marked safe. The button is greyed out on the rest |
| List style | One list: title on the left, area on the right |
| Order | Main Story first, then areas A–Z, then titles A–Z within an area |
| Opening it | The Q button replaces the Hotkeys button on both HUD layouts. No keyboard shortcut |
| Hotkey help | Moves to a new "Hotkeys" option in the Terminus (F1) menu |

## 1. Quest entries

### Files

One JSON file per quest in `Unora/Data/Configuration/Templates/QuestLog/`. Subfolders by area are allowed. The
server reads the folder from a new `QuestLogOptions.Directory` setting in `appsettings.json`, relative to the
staging directory like every other data path.

### Format

```json
{
  "key": "bitters",
  "title": "Bitters",
  "area": "Mileth",
  "givenBy": "Noahn, Mileth Tavern",
  "stageEnum": "BittersStage",
  "stages": {
    "Active": {
      "text": "Noahn's bar is running low. Bring him 5 Beetle Horns.",
      "progress": [ { "label": "Beetle Horns", "item": "beetleHorn", "need": 5 } ]
    }
  },
  "giveUp": { "removeEnums": ["BittersStage"], "removeCounters": [], "removeFlags": [] }
}
```

| Field | Meaning |
|---|---|
| `key` | Unique id, case-insensitive. The GiveUp request uses it |
| `title` | The name shown in the list and at the top of the details pane |
| `area` | Shown on the right of the list row and used for ordering. `"Main Story"` sorts first |
| `givenBy` | Optional. A line under the title, for example "Noahn, Mileth Tavern" |
| `stageEnum` | The name of the enum in `Chaos.Definitions` (or `Chaos.DarkAges.Definitions`) that holds the quest's stage in `Trackers.Enums` |
| `stageFlag` | Used instead of `stageEnum` by a quest tracked only by a flag. The value is `"EnumName.Member"`. The quest is active while that flag is set, and `stages` then has one entry named `"Set"` |
| `stages` | Map from stage member name to what to show. A quest is active only when the player's current stage is one of these names. `None`, finished and "between parts" stages are left out, so the quest stays hidden at those stages |
| `stages.*.text` | The description. Plain text. The client wraps it |
| `stages.*.progress` | Optional list. Each line has a `label`, a `need`, and one of `item` (item template key, counted across the inventory) or `counter` (a `Trackers.Counters` key). A missing counter counts as 0. The shown count is capped at `need` |
| `giveUp` | Optional. Missing means the quest cannot be given up. Lists the enum types, counter keys and flag values (`"EnumName.Member"`) to clear. It never touches `Trackers.TimedEvents`, so giving up can't skip a cooldown |

### Loading and checks

A new `QuestLogCatalog` service loads every file at startup. For each entry it checks:

- `key` is unique and `title` and `area` are not empty.
- `stageEnum` or `stageFlag` names a real enum type and member, and exactly one of them is set.
- Every `stages` name is a member of that enum.
- Every `item` is a real item template key, and every `need` is at least 1.
- Every `giveUp` enum type and flag value is real.

A bad entry is logged as a warning with the file name and reason, and it is skipped. One bad file never stops
the server. A unit test loads the real Unora folder and fails if any entry is skipped, so typos are caught
before a deploy.

## 2. Server behavior

### Building a player's list

`QuestLogService.Build(Aisling)` returns the player's active entries:

1. For each catalog entry, read the stage (`Trackers.Enums` by the enum type) or check the flag.
2. Keep the entry if the current stage is a key in `stages`.
3. Fill each progress line: `have = min(need, count)`. The count is the inventory count of that item template
   key, or the counter's value.
4. Sort: area `"Main Story"` first, then area A–Z, then title A–Z.

`Build` is a pure function of the player's trackers and inventory, so tests can set up a character and check the
result exactly.

### Live updates

- The service keeps, per player, whether the window is open and the last list it sent (as a value the server
  can compare).
- On Open, the server builds the list, sends it, and marks the window open.
- `DefaultAislingScript.Update` already runs a one-second timer. On each tick, for a player whose window is open,
  the service rebuilds the list. It sends the list only if it differs from the last one sent.
- On Close, or when the player logs out or changes servers, the open mark is cleared.

### Give up

On a GiveUp request with a quest key, the server:

1. Finds the entry. If it's missing, not active for this player, or has no `giveUp`, it ignores the request and
   logs it at debug level.
2. Removes each listed enum type, counter and flag.
3. Sends the orange-bar message "You gave up on {title}." and the updated list.

## 3. Packets

| Direction | Name | OpCode | Body |
|---|---|---|---|
| Client → server | `QuestLogRequest` | `ClientOpCode` 132 | `type` byte: Open = 0, Close = 1, GiveUp = 2. For GiveUp, the quest `key` string follows |
| Server → client | `QuestLogDisplay` | `ServerOpCode` 136 | `ushort` entry count, then per entry: `key`, `title`, `area`, `givenBy`, `text` (strings), `canGiveUp` (bool), progress line count (byte), then per line `label` (string), `have` and `need` (`ushort`) |
| Server → client | `HotkeyHelpOpen` | `ServerOpCode` 137 | Empty |

Strings use the same length-prefixed format as the other custom packets. Each packet gets a converter in
`Chaos.Networking`, an args record, and a round-trip test, following `LockpickDisplay` and `BugReportOpen`. The
client handles them in `ConnectionManager`. `CLIENT_VERSION` goes from 758 to 759.

## 4. Client window

A new `QuestLogControl` in `Chaos.Client/Controls/World/Popups/QuestLog/`.

- **Full state.** It draws `q_list` with minimize and X on the title bar, and GiveUp and Close in the button slots.
- **List pane.** Each row shows the title on the left and the area, right-aligned, in a dimmer color. The selected
  row is highlighted. The scroll strip scrolls the list, and the mouse wheel works over the list pane.
- **Details pane.** It shows the title, the `givenBy` line in a dimmer color, the wrapped `text`, and one line per
  progress entry: "Label: have / need". A line where `have == need` uses a "done" color. The mouse wheel scrolls
  long text.
- **Empty log.** The details pane shows "You have no active quests." GiveUp is greyed out.
- **Selection** is kept by quest key across updates. If the selected quest disappears, the first row is selected.
- **GiveUp.** It is greyed out (frame 1) when the selected quest can't be given up. The first click replaces the
  details text with "Click GiveUp again to abandon {title}." A second click within 5 seconds sends the request.
  Selecting another row, the timeout, or any other button cancels the confirm.
- **Minimize / restore.** Minimize switches to `q_min` with restore and X. Restore switches back. The window can be
  dragged by its title bar in both states. It stays open while the player plays and keeps receiving updates.
- **Close.** X and Close hide the window and send Close. Clicking the Q button again, or leaving the world
  screen, does the same.
- **Q button.** On both `WorldHudControl` and `LargeWorldHudControl`, the button made from `BTN_HELP` becomes the
  quest button. It keeps its position, uses `q_btm` frames 0 and 1 for normal and pressed, and gets the tooltip
  "Quests". Clicking it toggles the window: Open sends a request, and the window appears when the first
  `QuestLogDisplay` arrives.

The window pieces come from `setoa.dat` through the same repository the other custom windows use. There is no
fallback art. The new client needs the new `setoa.dat`.

## 5. Hotkey help moves to Terminus

- A new `TerminusHotkeysScript`, modeled on `TerminusBugReportScript`, adds the option "Hotkeys" to
  `terminus_initial`. On `terminus_hotkeys` it closes the dialog and calls `SendHotkeyHelpOpen()`.
- A new dialog template `terminus_hotkeys.json` next to `terminus_bugreport.json`, with the script key.
  `terminusHotkeys` is added to `terminus_initial`'s script keys.
- The client handles `HotkeyHelpOpen` by calling `HotkeyHelp.Show()`. The old `HelpButton.Clicked` wiring goes away.

## 6. Art build

`Unora/Tools/QuestLog/build.py`, modeled on `Tools/Lockpick/build.py`:

- It copies the 8 SPFs from `Tools/QuestLog/source/` (checked in, copied from the Desktop folder) into a copy of
  the client folder's `setoa.dat`.
- It reads the copy back and checks every frame against its source.
- It writes a batch folder on the Desktop with `manifest.txt` and copies the result into the client folder.
  `--no-apply` writes the batch folder only.
- A `test_build.py` covers the read-back check.

The art has to ship in a launcher patch together with the new client.

## 7. Writing the entries

About 100 quests, from `Scripting/DialogScripts/Temuair/Quests`, `Scripting/DialogScripts/Medenia`, and
`Scripting/Quests` (the newer quest framework). The work goes one area at a time:

1. First, list every quest. Name its script, stage enum or flag, and area. Leave out scripts that aren't quests,
   such as teleports and lock chests.
2. For each quest, read its script and dialog JSON. Write the entry: the active stages, a description per stage
   in the game's voice, progress lines taken from the script's own checks, and `giveUp`.
3. `giveUp` is added only when a reset can't pay anything out twice. A quest is not safe if accepting it
   hands the player an item, gold or experience. It is also not safe if any listed stage comes after a reward
   was paid. When in doubt, leave `giveUp` out.
4. The user reviews every entry on one page before anything merges.

## 8. Testing

**Server (Chaos.Tests)**

- The catalog loader: a good entry loads, and each kind of bad entry is skipped with a warning.
- The real Unora `QuestLog` folder loads with zero skipped entries.
- `Build`: active and hidden stages, item and counter counts, the `need` cap, a missing counter, and the order.
- Give up: a safe quest resets and sends the list. A quest that isn't safe, isn't active, or doesn't exist is
  ignored. Timed events are left alone.
- Live updates: send on change, no send when nothing changed, no sends after Close.
- A round-trip test for each of the three packets.

**Client (Chaos.Client tests)**

- Selection is kept across updates and falls back to the first row when the selected quest disappears.
- The GiveUp two-click confirm: the second click sends, and a timeout or another action cancels.

**In game**

A test character on the local test setup. Check a kill quest, an item quest and a Main Story quest. Check that
counts update live, minimize and restore work, dragging works, GiveUp works on a safe quest and is greyed out on
another, and Terminus → Hotkeys opens the hotkey help.

## Known limits

- Updates arrive up to one second late, because the server rebuilds the list on its one-second tick.
- A quest whose progress isn't stored as a stage, flag, counter or inventory item can only show text, with no
  count. Examples are map visits and conversation-only steps.
- The quest text is only as correct as the entries. A later change to a quest script needs a matching entry edit.

## Amendments (added while planning, 2026-09-27)

These override the sections above where the two differ.

1. **Item names.** Most older quests count items by display name (`Inventory.HasCount("Beetle Horn", 5)`, 93
   places) rather than by template key (23 places). A progress line may use `itemName` instead of `item`. It is
   counted with `Inventory.CountOf(name)` and checked at load against the item templates' names. Exactly one of
   `item`, `itemName` and `counter` is set.
2. **Reviewer notes.** An entry may carry `notes`: plain text for the reviewer, such as why give up is or isn't
   safe. The server never reads or sends it.
3. **Limits checked at load.** `key` is at most 40 characters. `title`, `area`, `givenBy`, progress labels and
   counter keys are at most 60. `text` is at most 2000. A stage has at most 8 progress lines. `need` is 1 to 65535.
   A `giveUp` that clears nothing is an error. `stageFlag` and `removeFlags` values must name a `[Flags]` enum.
4. **Enum lookup.** `stageEnum` and flag names are simple type names, looked up among the enums in the
   `Chaos.Definitions` and `Chaos.DarkAges.Definitions` namespaces. A name found in both is an error.
5. **Open windows.** The server keeps open windows in a `ConditionalWeakTable<Aisling, …>`, so a player who logs
   out is forgotten without a logout hook.
6. **Packet strings.** `text` is written as a 2-byte-length string (`WriteString16`). Every other string uses a
   1-byte length.
7. **Exclusions.** The quest inventory (plan Task 10) lists every script it leaves out, with the reason. Examples
   are teleports, chests, class choice and bounty boards (Terminus already lists active bounties). The review page
   shows that list, so the user can pull any of them back in.
8. **Real-data check.** The test that loads the real `QuestLog` folder reads the Unora path from the `UNORA_DIR`
   environment variable. It is skipped when `UNORA_DIR` is not set.
