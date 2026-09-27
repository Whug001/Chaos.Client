# Lockpicking window (Skyrim-style) — design

Date: 2026-09-27
Repos: Chaos-Server, Chaos.Client, Unora (art tools and setoa.dat)

## Goal

Replace both lockpicking minigames with one lockpicking window in the client, played like Skyrim's. The
rogue moves a pick around the top of a lock and turns the lock. The closer the pick is to a hidden sweet
spot, the further the lock turns. Each jam wears the pick. When the pick breaks, the chest disappears.

## What exists today

- **Crypt chests** (`Chaos/Scripting/MerchantScripts/LockpickingChestScript.cs`). These are merchants
  spawned by `LockpickChestSpawnerScript` from the `lockPickChest` merchant template. A rogue with
  lockpicks who stands one tile east of the chest starts a session on its own. The chest then chants a
  text bar (`[---===--^---]`). An attack while the `^` is in the `=` zone opens it. Success and failure
  both have a 36% chance to use up a lockpick. After a failure the chest stays, so retries are unlimited.
  Each chest rolls Easy, Medium or Hard (50% / 35% / 15%). The prize is gold scaled by crypt level and
  difficulty, with a 1% jackpot.
- **Deep Crypt chests** (`DCEasyLockedChestScript`, `DCMediumLockedChestScript`, `DCHardLockedChestScript`)
  and the **Asilon Prairie chest** (`AsilonChestScript`). These are dialogs. "Attempt to pick the lock"
  asks for a number from 1 to 4, and a right guess (1 in 4) pays a weighted item prize. A wrong guess
  removes the chest and one lockpick. "Open with Key" opens the chest with no minigame.
- The unrelated `lockPickChest` **monster** ("Chest", `MonsterScripts/LockPickChestScript.cs`) is out of
  scope.

**Base branch.** The server work starts from local `master` at 2821f83f7. That is the unpushed
dialog-block merge, and it changes the three Deep Crypt chest scripts. The client work starts from local
`main`, which includes 5ee193a (the menu-close fix).

## Decisions made

| Question | Decision |
|---|---|
| Which chests | All of them: crypt, Deep Crypt (3) and Asilon Prairie |
| When a pick breaks | The pick is used up and the chest disappears |
| Window style | Layout A: a framed panel with only the lock, built from the game's frame art |
| Lock art | New pixel-art sprites in setoa.dat, shipped in a launcher patch |
| Who decides | The server keeps the sweet spot and judges each turn. The client only animates |
| Item-chest odds | Close to today's 1 in 4: the pick breaks on the second jam |

## 1. Server rules

### The lock engine

A new `LockpickLock` class in `Chaos/Scripting/MerchantScripts/Lockpicking/` holds one chest's lock:

- `Difficulty`: Easy, Medium or Hard.
- `Tuning`: the numbers for this chest (see section 5). Crypt chests and item chests use different sets.
- `SweetSpotDegrees`: the center of the sweet spot, a random angle, rolled once when the chest is created.
  It is rolled so the whole sweet spot fits inside 0–180°.
- `JamsTaken`: how many jams the current pick has taken.
- `Picker`: the Aisling who has the window open, or null.

It has one pure method that decides a turn:

```
TurnOutcome Turn(int pickDegrees)
  → Opened                       if |pick − sweet| ≤ width / 2
  → Jammed(turnPercent)          otherwise, if JamsTaken + 1 < JamsToBreak
  → Broke(turnPercent)           otherwise
```

`turnPercent` (0–99) is how far the lock turns before it stops. It is highest at the edge of the sweet
spot and falls in a straight line to a minimum of 5% at the far edge of the warm range. Past the warm
range it stays at 5%. An Opened result is sent as 100%. `Turn` uses no random numbers, so tests can check
it exactly.

The constructor takes the sweet-spot angle as a parameter. A static `LockpickLock.Roll(difficulty,
tuning, Random)` picks the angle and builds the lock. Tests call the constructor with a fixed angle.

### Who owns a lock

Every lockable chest is a merchant, and a merchant script lives as long as its chest. So the lock and the
session live in a merchant script. A new abstract `LockpickChestScriptBase : MerchantScriptBase`, in the
same folder, holds:

- the `LockpickLock`
- the session: `Picker`, the time of the last turn, the time since start, and the prize callback
- `BeginSession(Aisling, Action<Aisling> onOpened)`, `HandleTurn(Aisling, int)`, `EndSession(reason)`
- an `Update` that checks distance and the 60-second timeout

Two scripts derive from it:

- **`LockpickingChestScript` (crypt)** keeps its difficulty roll, sprite, `CalculatePrize` and
  `AwardPrize`, and uses the crypt tuning. Its `OnClicked` calls `BeginSession(aisling, AwardPrize)`.
- **`LockedItemChestScript`** (new, script key `lockedItemChest`) reads its difficulty from `scriptVars`
  and uses the item tuning. It is added next to `showdialog` in the seven item-chest merchant templates.
  These are `dceasyChest`, `dcmediumChest` and `dcHardChest` (Easy, Medium, Hard) and the four
  `asilon*Chest` templates (Medium). Its prize comes from the dialog script: the "Attempt to pick the
  lock" handler calls `BeginSession(source, AwardPrize)`, and `AwardPrize` is the dialog script's
  existing method. The callback keeps that dialog script instance, with its `ItemFactory` and `Logger`,
  alive until the session ends.

To find a player's session, `WorldServer`'s `LockpickInteraction` handler uses the same lookup as the
Spindle's. It checks merchants within 1 tile with `Script.As<LockpickChestScriptBase>()` and takes the one
whose `Picker` is this player.

### Starting a session

To start, the player must be a rogue, have at least one `lockpicks`, and be within 1 tile of the chest.

- **Crypt chests:** clicking the chest (`OnClicked`) starts the session. Remove the old trigger from
  `Update` (a rogue standing east of the chest), `DisplayLockBar`, the `Chant` bar, and the attack-based
  `OnAttacked` check. The chest still sets its sprite from its difficulty.
- **Deep Crypt and Asilon chests:** the "Attempt to pick the lock" option closes the dialog and starts the
  session. The `*picklock_initial` number-prompt path and its dialog templates
  (`*picklock_initial.json`, `*picklock_PickChestNext.json`) are removed. "Open with Key" does not change.
- If another player holds the lock: send the orange-bar message "Someone is already working on this lock."
  and do not open the window.
- If the chest was already removed: the existing "The chest has vanished already!" handling applies.

Starting a session sets `Picker` and sends the `Open` display.

### Handling a turn

When the server gets `LockpickInteraction.Turn(angle)`:

1. Find the player's active session. If there is none, ignore the message.
2. Check that the chest is still on the map, the player is within 1 tile, and they still have a lockpick.
   If any check fails, end the session with the matching reason.
3. Ignore the turn if it arrives less than 400 ms after the last accepted turn.
4. Clamp the angle to 0–180 and call `Turn`.
5. **Opened:** send a `TurnResult` (Opened, 100%). Play sound 183 for players within 8 tiles. Remove the
   chest, then give the chest's normal prize with its existing code (`AwardPrize`). The pick is **not**
   used up.
6. **Jammed:** increment `JamsTaken` and send a `TurnResult` (Jammed, percent).
7. **Broke:** remove one `lockpicks`. Send the orange-bar message "Your lockpick broke!". Play sound 184
   for players within 8 tiles. Send a `TurnResult` (Broke, percent) and remove the chest. For item chests,
   keep the existing failure log line ("…has failed opening…").

`JamsTaken` belongs to the lock, not the session. Closing and reopening the window does not reset it.

### Ending a session

The session ends and the window closes (`Close` display with a reason) when any of these happen:

| Trigger | Reason text |
|---|---|
| Client sends `Close` | (none, no message sent back) |
| Player moves more than 1 tile from the chest | "You moved away from the chest." |
| Chest removed by someone else, or it despawns | "The chest is gone." |
| Player has no lockpicks left | "You have no lockpicks." |
| 60 s since the session started or the last turn | "You lose your focus." |
| Player logs off or changes map | (nothing sent) |

`LockpickChestScriptBase.Update` checks distance, time and whether the picker is still on the map. An
Opened or Broke result ends the session with no `Close` message, because the client closes itself.

## 2. Messages

Numbers that were free on 2026-09-27: `ClientOpCode` 131 and `ServerOpCode` 135. Check every open server
branch again before using them.

### `ClientOpCode.LockpickInteraction` = 131 → `LockpickInteractionArgs`

| Field | Type | Notes |
|---|---|---|
| `Action` | byte | 0 = Turn, 1 = Close |
| `PickDegrees` | byte | 0–180. Only read when the action is Turn |

### `ServerOpCode.LockpickDisplay` = 135 → `LockpickDisplayArgs`

| Field | Type | When sent | Notes |
|---|---|---|---|
| `Kind` | byte | always | 0 = Open, 1 = TurnResult, 2 = Close |
| `Difficulty` | byte | Open | 0 Easy, 1 Medium, 2 Hard |
| `Title` | string8 | Open | e.g. "Hard Lock" |
| `LockpickCount` | ushort | Open, TurnResult | How many picks the rogue has now |
| `Outcome` | byte | TurnResult | 0 Jammed, 1 Opened, 2 Broke |
| `TurnPercent` | byte | TurnResult | 0–100 |
| `Reason` | string8 | Close | May be empty |

The sweet spot and the pick's wear are never sent.

These follow the existing pattern: an args record in `Chaos.Networking/Entities/{Client,Server}/`, a
converter in `Chaos.Networking/Converters/{Client,Server}/`, registration wherever the Spindle converters
are registered, a handler in `WorldServer`, and a `SendLockpickDisplay` method on `IChaosWorldClient`.
Converter round-trip tests go next to `SlotPacketConverterTests`.

## 3. Client window

`Controls/World/Popups/Lockpick/LockpickControl.cs`, based on `FramedDialogPanelBase` (`_nsett` prefab,
`UsesControlStack = true`). It is mounted and driven the same way as `GildedSpindleControl`: a
`WorldScreen.ServerHandlers` handler for `LockpickDisplay`, a `ViewModel/Lockpick.cs` state class next to
`GildedSpindle.cs` (exposed as `WorldState.Lockpick`), and an `Interaction` send on the connection.

### Layout (panel-local pixels)

The approved mockups are `lock-a-idle.png`, `lock-a-jam.png` and `lock-a-open.png` in
`.superpowers/brainstorm/1050-1790499417/content/`. That folder is git-ignored, and the script that made
them is not in the repo.

- Width 236. Title at y 8, centered. The title color follows difficulty: Easy green, Medium yellow,
  Hard red, using the nearest `LegendColors`.
- Recessed window, 196 × 196 at y 26, centered, built with `DialogFrame.BuildRecessedTexture` and fill
  (10, 8, 5). The lock is drawn inside it with 6 px padding.
- Info row 8 px below the window. "Lockpicks: N" sits on the left in white, and "Space: turn" on the
  right in grey.
- Message line 16 px below that, centered.
- The Close button from `CreateCloseButton`, in the bottom border at the right.

### Input

- **Pick angle.** The mouse position relative to the lock center is turned into an angle with `atan2`,
  mapped to 0° (pointing left) through 90° (up) to 180° (right). Angles below the center are clamped to
  the nearest end. The Left and Right arrows move the pick 2° per press. The pick is frozen while a turn
  is animating.
- **Turn.** Space or right-click starts one attempt, on key-down only. Holding the key does not repeat.
  The next attempt needs a release first and an idle state. While the window is open, it takes Space and
  the arrow keys so they do not reach the world (Space is normally attack).
- **Close.** The Close button or Esc sends `Close` and hides the window.

### Animation states

```
Idle ──turn pressed──▶ Turning ──result: Jammed──▶ Straining (0.3 s, pick shakes ±2 px) ──▶ Returning (0.2 s) ──▶ Idle
                          │   └─result: Opened──▶ Opening (to 90°) ──▶ Done (1 s, "The lock clicks open!") ──▶ Hidden
                          └─────result: Broke──▶ Straining ──▶ Snapped (halves fall, 1 s, "Your lockpick broke!") ──▶ Hidden
```

- Turning moves the cylinder at 360°/s from 0°, and keeps going until it reaches
  `TurnPercent × 90°`. If the reply has not arrived when the cylinder reaches 20°, it waits there.
- The wrench rotates with the cylinder. The pick keeps its angle.
- If no reply comes within 2 seconds, the window returns to Idle and shows "No answer from the server.".
- Sounds are played on the client. A jam plays a short scrape using an existing sound id picked during
  the build. Open (183) and break (184) come from the server, as they do today.

### Tests (Tests/Chaos.Client.Tests, TUnit)

- Mouse-to-angle mapping and its clamps.
- The state machine: every path in the diagram, the 2-second timeout, and a held key giving one attempt.
- A `LockpickDisplay` Open, then a TurnResult, updates the pick count.

## 4. Art

New files in setoa.dat. Each SPF carries its own palette.

| File | Contents | Size (approx.) | Use |
|---|---|---|---|
| `lockpk01.spf` | Lock plate: iron ring, rivets, inner face | 184 × 184 | Drawn once, centered |
| `lockpk02.spf` | Keyhole cylinder, 19 frames, 0° to 90° in 5° steps | ~64 × 64 | Frame = round(angle / 5) |
| `lockpk03.spf` | Lockpick, pointing up, pivot at the tip | ~8 × 110 | Drawn rotated, point sampling |
| `lockpk04.spf` | Tension wrench | ~12 × 80 | Drawn rotated with the cylinder |
| `lockpk05.spf` | Broken pick: frame 0 is the tip half, frame 1 the handle half | — | The Snapped animation |

- Colors: iron greys, with brass and brown taken from the ornate frame so the lock matches the panel.
- Sources are indexed PNGs in `Unora/Tools/Lockpick/art/`. `Unora/Tools/Lockpick/build.py` packs them
  with `acclib.daformats`, the same way `Tools/Emblems/build.py` does.
- The script reads the newest setoa.dat, which is the world list + emblems + custom art batch. It writes
  a new Desktop folder with the new setoa.dat, review sheets and a manifest.
- Review gate: the art is rendered into the panel mockup and approved in the browser before packing.
- The client build and this setoa.dat must ship in the same launcher patch. There is no code-drawn
  fallback.

## 5. Tuning

One `LockpickTuning` record holds the numbers. It has two sets, one for crypt chests and one for item
chests.

| | Easy | Medium | Hard |
|---|---|---|---|
| Sweet-spot width | 20° | 12° | 6° |
| Warm range past each edge | 60° | 45° | 30° |
| Crypt chests: jam that breaks the pick | 6th | 5th | 4th |
| Item chests: jam that breaks the pick | 2nd | 2nd | 2nd |

- Crypt chests keep their difficulty mix (50% / 35% / 15%), `CalculatePrize`, the difficulty
  multipliers and the 1% jackpot.
- Item chests keep their prize tables.
- **Odds check (test).** A simulated "sensible player" strategy runs 10,000 chests per difficulty and
  set: probe at 90°, then move toward the side that turned further. The test asserts that item-chest
  success is 20–30% for each difficulty. If a width misses that band, change the item set's width (not
  the jam count) until it passes. The crypt set's result is printed in the test output as a record,
  with no pass/fail band.

## Out of scope

- The `lockPickChest` monster and its `generic_lockPickChest` dialog.
- Changes to prizes, lockpick price or where lockpicks drop.
- A lockpicking skill or level that changes the tuning.

## Risks

- **Other sessions and message numbers.** Several sessions add messages at once. Re-check 131 and 135
  just before merging.
- **The dialog-block fix is not pushed.** This work stacks on 2821f83f7. If that merge changes before it
  is pushed, rebase.
- **Economy.** Crypt gold per chest does not change. How often chests open does change, and that depends
  on player skill. After release, run the gold audit tool to compare lockpick-chest gold before and
  after.

## Amendments (2026-09-27, while planning)

1. **Composition, not a base class.** The crypt chest script has no `scriptVars`, so it derives from
   `MerchantScriptBase`. The item chest script needs `scriptVars`, so it derives from
   `ConfigurableMerchantScriptBase`. One abstract base can't serve both. The session logic therefore lives in a
   plain class, `LockpickChest` (lock + session + turn handling + update checks). Each chest script owns one
   and exposes it through `ILockpickChestScript : IMerchantScript { LockpickChest Lockpick { get; } }`. The
   world server looks it up with `Script.As<ILockpickChestScript>()`.
2. **Item-chest tuning.** A simulation showed that, with one probe, the sweet-spot width barely changes a
   sensible player's odds. The warm range drives them. The item set is Easy 12° / 20°, Medium 12° / 15°,
   Hard 6° / 30° (width / warm range), with the pick still breaking on the second jam. That simulates to
   27% / 24% / 25%. The odds test may tune width **and** warm range. The crypt set simulates to
   88% / 73% / 52% for a careful player.
3. **Names.** The client folder is `Controls/World/Popups/Lockpicking/`, and its state class is
   `ViewModel/LockpickState.cs`, exposed as `WorldState.Lockpick`. A class named `Lockpick` would collide
   with a namespace named `Lockpick`. The server classes live in `Chaos/Scripting/MerchantScripts/Lockpicking/`.
4. **Item chests and the removed dialog.** `*picklock_initial.json` stays as a normal dialog, because the
   "Attempt to pick the lock" option points at it. Its script starts the session and closes the dialog. Only
   `*picklock_PickChestNext.json` is deleted.
5. **Pick size.** The pick sprite is about 8 × 86 so that, turning about the lock's center, it stays inside
   the 184 × 184 lock face.
6. **Opening order.** On Opened, the prize callback runs **before** the chest is removed. The Asilon
   explosion and monster spawn need the chest's position on the map.
