# In-Game Bug Reports

**Date:** 2026-09-23
**Status:** Design for review
**Scope:** Players report a bug from the F1 Terminus menu in a wooden report window. The server saves each report as a folder on the live machine, sorted by category. Each report holds the player's text, a picture of their screen, a copy of their character, the live world state around them, their client details, and recent server log lines about them. A local command imports the copied folders next to the Discord feedback archive for review.

Repos touched:

- `Chaos.Client`: the report window, picture capture, and the client side of the messages.
- `Chaos-Server` (submodule, `Jinori/Chaos-Server`): the shared messages, the Terminus entry, and the report writer.
- `Unora`: the Terminus dialog files, the import command, and the player-feedback docs.

## Purpose

The old F1 report flow is dead. The dialogs in `Unora/.../terminus/Report System/` are no longer linked from the Terminus menu. `TerminusBugReportScript` posts to Discord with an empty bot token. When it worked, it sent 100 characters of text, the character name, level, map name and X/Y. That is rarely enough to find a cause.

The custom client can do more. It can send a picture of what the player saw. The server can save the full character and everything around it at that moment. Staff then start from evidence instead of a one-line description.

## Decisions

| Question | Decision |
|---|---|
| Entry point | F1 → Terminus → "Report a bug". No hotkey, options button or chat command. |
| Form | A new client window (layout A2) in the wooden `FramedDialogPanelBase` frame used by Josephine's Mirror, the Marketplace, the Bank and F4 Settings. |
| Categories | Skill/Spell, Item, Map or Warp, NPC, Quest, Monster/Combat, Client display/UI, Other. One shared enum. |
| Description | 10 to 1,000 characters, multi-line. |
| Picture | Taken when the window opens, with no dialog on screen. A small preview sits in the window. A checkbox, ticked by default, lets the player leave it out. It is never saved on the player's computer. |
| Extra data | Nine character save files, live world state, client details, and server log lines that name the character. Recent chat is not sent. |
| Delivery | Files on the live server. Staff copy the folder off by hand. No Discord post. |
| Storage | `Data/Saved/BugReports/<category>/<yyyy-MM-dd_HHmmss>_<name>/`, set in appsettings. |
| Report format | `report.md` uses the Discord archive's header fields, status values and Triage preserve marker. |
| Reading | `import_reports.py` copies new report folders into `Unora/docs/player-feedback/in-game/` and rebuilds an index. It never overwrites an imported report. |
| Abuse limits | One report per character every 2 minutes. Opening the window is limited to once every 10 seconds per character ("Please wait a few seconds before opening another report."). A single-use report number from the server. A 512 KB picture cap. |
| Client version | 751 → 752. |
| Rewards | None automatic. Staff use the existing `GiveBugReportPointsCommand`. |
| Out of scope | A Discord notice, a transfer tool for the live machine, a staff viewer inside the game, sending chat history, suggestions through this window, re-encoding the picture on the server. |

## Player flow

1. The player presses F1. The client already sends a click on entity `uint.MaxValue`, and the server opens `terminus_initial`.
2. `TerminusBugReportScript` adds a "Report a bug" option to `terminus_initial`, the same way the other Terminus scripts add theirs.
3. The option leads to `terminus_bugreport`. When it displays, the script checks the wait time.
   - If the player's last report was under 2 minutes ago, Terminus replies: "You sent a report recently. Please wait N more minute(s)." The window does not open.
   - Otherwise, if the player opened a report window less than 10 seconds ago, Terminus replies: "Please wait a few seconds before opening another report." The window does not open.
   - Otherwise the script closes the dialog and calls `BugReportService.Open`.
4. `Open` makes a new report number and captures the character and world state into a holding folder. Then it sends `BugReportOpen` to the client.
5. The client closes any NPC dialog, asks for a frame capture, and opens the report window on the next update with the preview.
6. The player picks a category, writes, and presses Send Report. The client sends a `Submit`, then the picture in parts, then closes the window.
7. The server writes the final folder and sends an orange bar reply: "Thank you, your report was sent." If it refuses, the reply says why.
8. Close or Esc sends `Cancel`, and the server deletes the holding folder.

## Report window (client)

`Chaos.Client/Controls/World/Popups/BugReport/BugReportControl.cs`, deriving from `FramedDialogPanelBase`. 430 × 310 pixels, centered, `UsesControlStack = true`.

| Element | Control | Behavior |
|---|---|---|
| Title "Report a Bug" | label, gold | — |
| "What kind of problem?" | caption, gray | — |
| Category grid, 4 × 2 | 8 × `CustomButton` | Works like radio buttons. The picked one is shown selected. None is picked at first. |
| "What happened? What did you expect?" | caption | — |
| Description | `CustomTextBox`, `IsMultiLine = true`, `MaxLength = 1000` | Hint text: "What were you doing? What went wrong?" The hint draws on one clipped line, so it must fit the 254-pixel box. |
| Counter | label | `n / 1000` |
| "Screenshot" preview | image, about 110 × 83 | The captured frame, scaled down. If the capture failed, it shows "No picture" and the checkbox is hidden. |
| "Include it" | `CustomCheckBox` | Ticked by default. |
| Note | label, gray | "Your character, position and recent server events are attached for staff." |
| Send Report | `CustomButton` | Dim until a category is picked and the trimmed text is at least 10 characters. |
| Close | frame OK area, `CreateCloseButton` | Sends `Cancel` and hides the window. Esc does the same. |

The window keeps its text only while it is open. A disconnect loses the draft.

## Picture capture (client)

`ChaosGame.Draw` copies the whole 640 × 480 render target after the screens draw (`ChaosGame.cs:199`). Anything on screen in that frame is in the picture.

- Split the palettized PNG encoding out of `SaveScreenshot` into a method that returns PNG bytes. `SaveScreenshot` keeps writing `lod###.png` through it.
- Add `RequestCapture(Action<byte[]?> onCaptured)`. `Draw` calls it at the same point as the F12 screenshot.
- On `BugReportOpen`, during `Update`, `WorldScreen` hides the NPC dialog, then requests the capture. That same frame's `Draw` renders without the dialog and captures it. The callback stores the bytes. The next `Update` shows the window.
- The picture lives only in memory. It is dropped when the window closes.
- F12 is read in `ChaosGame.Update` before any screen or popup, so the report window does not block it.

## Messages

Defined in `Chaos-Server` so the client and server build from the same code.

- `ServerOpCode.BugReportOpen = 127`, server to client.
- `ClientOpCode.BugReportInteraction = 124`, client to server, with an action byte, matching `MarketInteraction`'s pattern.

Check both values against `Enums.cs` at implementation time. Server values 112–126 are taken, so 127 is next. Client values 112–119 and 121–123 are taken. 120 is also free, but 124 sits past the highest value in use. That avoids a clash if upstream later fills the gap, which already happened once with 114 (see the `SlotMachineInteraction` remark).

`BugReportCategory` (byte enum in `Chaos.DarkAges/Definitions`): `SkillSpell = 0`, `Item = 1`, `MapWarp = 2`, `Npc = 3`, `Quest = 4`, `MonsterCombat = 5`, `ClientUi = 6`, `Other = 7`. Folder names: `skill-spell`, `item`, `map-warp`, `npc`, `quest`, `monster-combat`, `client-ui`, `other`.

| Message | Fields |
|---|---|
| `BugReportOpen` | `ReportId` (uint) |
| `BugReportInteraction` / `Submit` (0) | `ReportId` (uint), `Category` (byte), `Description` (string16), `PictureLength` (uint, 0 = none), `ClientBuild` (string8, the informational version such as `0.1.0+c0a7eb7`), `OsDescription` (string8), `FramesPerSecond` (ushort), `PingMs` (ushort), `HudStyle` (byte: 0 classic, 1 large), `WindowWidth` (ushort), `WindowHeight` (ushort) |
| `BugReportInteraction` / `PicturePart` (1) | `ReportId` (uint), `PartIndex` (byte), `Data` (ushort length + bytes, at most 32,768) |
| `BugReportInteraction` / `Cancel` (2) | `ReportId` (uint) |

Client detail sources:

- `ClientBuild`: the client assembly's informational version (Nerdbank.GitVersioning).
- `OsDescription`: `RuntimeInformation.OSDescription`.
- `FramesPerSecond`: `DebugOverlay`'s `DisplayFps`, exposed read-only. The plan must confirm it updates while the overlay is hidden. If it doesn't, count frames the same way outside the overlay.
- `PingMs`: `LatencyMonitor.LatencyMs`.
- `HudStyle`: whichever `IWorldHud` is active.
- `WindowWidth` / `WindowHeight`: the game window's client size.

The part count is `ceil(PictureLength / 32768)`. With the 512 KB cap, that is at most 16 parts. The client sends `Submit` first, then every part in order, in one go. The server accepts parts in any order.

`CONSTANTS.CLIENT_VERSION` goes from 751 to 752, so older clients are turned away at the lobby.

## Server

### Settings

`BugReportOptions` in appsettings, next to the other `Data\\Saved\\...` stores:

| Key | Default |
|---|---|
| `Directory` | `Data\\Saved\\BugReports` |
| `LogDirectory` | `logs` (relative to the server's base directory, matching the NLog file target) |
| `WaitMinutes` | 2 |
| `ReportLifetimeMinutes` | 30 |
| `PictureGapSeconds` | 30 |
| `MaxPictureBytes` | 524288 |
| `MinDescriptionChars` / `MaxDescriptionChars` | 10 / 1000 |
| `LogWindowMinutes` | 15 |
| `LogLineCap` | 2000 |

### Components

- **`TerminusBugReportScript`** (rewritten). Adds the option to `terminus_initial`. Handles `terminus_bugreport`: checks the wait time, replies or closes, and calls `BugReportService.Open`. The Discord code is removed.
- **`BugReportService`** (singleton). Keeps open reports in memory, keyed by report number: the character name, the time opened, the holding folder, the submit fields, and the parts received. Also keeps each character's last submit time. It handles `Open`, `Submit`, `PicturePart` and `Cancel`. A timer checks every 5 seconds for picture gaps and expired report numbers. The wait times and open reports are lost on restart, which is acceptable.
- **`BugReportStore`**. Does all file work: the holding folder, the move into a category, writing the files, and clearing `_pending` at startup.
- **World state builder**. Builds `world.json`.
- **Log reader**. Builds `server-log.jsonl`.
- **Report renderer**. Builds `report.md`.
- **`WorldServer`** handler for `ClientOpCode.BugReportInteraction`. It deserializes the message and passes it to the service.

### Open

1. Replace this character's existing open report, if any, and delete its holding folder.
2. Make a random `ReportId` (uint) that is not in use.
3. Capture into `_pending/<ReportId>/`:
   - `character/`: the nine save files, through a new public `AislingStore` method that runs `InnerSaveAsync` on a given folder.
   - `world.json`: see below.
4. Send `BugReportOpen`.

The capture runs inside the Terminus dialog script, which already runs on the world thread for the player's map, so nothing changes the character mid-copy. The plan must confirm that `AislingStore` maps the character to its save schemas before its first `await`. If it doesn't, map first, then write.

`world.json` holds:

- `serverTimeUtc`, `serverTimeLocal`, `serverVersion`
- `map`: name, template key, instance id, width, height
- `position`: x, y, direction
- `vitals`: HP, max HP, MP, max MP, alive or dead
- `group`: for each member, name, level, class, map instance id, x, y
- `nearby`: within the view range the server uses to send entities
  - `monsters`: name, template key, x, y, HP percent
  - `npcs`: name, template key, x, y
  - `groundItems`: name, template key, x, y, count
  - `players`: names

### Submit

1. Look up the report number. If it is unknown, used or expired, reply "This report has expired. Please open a new one from Terminus." and stop.
2. Check that it belongs to the sending character. If not, drop it silently.
3. Check the category is 0–7 and the trimmed description length is 10–1,000. If not, reply with the reason. The report number stays open, so a fixed client can retry.
4. Record the submit fields and the character's wait time. If `PictureLength` is 0, finish now. Otherwise wait for parts.

### Picture parts

- Parts are stored by index. A duplicate index replaces the earlier one.
- A `PictureLength` over `MaxPictureBytes` means no picture is kept. Parts are dropped and the report finishes without one.
- When the received bytes equal `PictureLength`, join the parts and finish.
- If no part arrives for `PictureGapSeconds`, finish without the picture. This also covers a disconnect after the submit.
- A submitted report is finished after `2 × PictureGapSeconds` (60 seconds by default) whatever parts keep arriving, even if a part keeps landing often enough to reset the gap above. This caps how long a modified client resending parts can keep a report open.

### Finish

1. Keep the picture only if it starts with the PNG signature and its length matches `PictureLength`. The server does not decode it.
2. Read the log lines.
3. Move `_pending/<ReportId>/` to `<category>/<yyyy-MM-dd_HHmmss>_<lowercase name>/`, using server local time. If that name exists, add `_2`, `_3`, and so on.
4. Write `screenshot.png`, `client.json`, `server-log.jsonl` and `report.md`.
5. Reply "Thank you, your report was sent."

If a step after the move fails, write what can be written. Note each failure in `report.md` and log an error. If the move itself fails, reply "Your report could not be saved. Please tell staff on Discord." Leave the holding folder for the startup cleanup.

### Log lines

The NLog file target writes one JSON object per line to `${basedir}\logs\${shortdate}.log`, local time. `Time` uses `${longdate}`. Production logs at Info level and above.

- Read today's file. Also read yesterday's if the window crosses midnight.
- Open with `FileShare.ReadWrite`, because NLog keeps the file open.
- Read backwards from the end in blocks. Stop at the first line older than `LogWindowMinutes`.
- Keep lines that contain the character's name as a complete JSON string, `"<name>"`, ignoring case. Character names are letters only, so this does not match inside longer words.
- Keep at most `LogLineCap` lines, newest first, and write them oldest first.
- Run this off the game loop.
- If a file is missing or unreadable, note it in `report.md` and continue.

### Pending cleanup

- `Cancel`: delete the holding folder and forget the report number.
- Expiry after `ReportLifetimeMinutes` with no submit: the same.
- Logout: the same, unless a submit already arrived. Then the report finishes normally.
- Startup: delete everything in `_pending/`.

## Report folder

```
Data/Saved/BugReports/
  map-warp/
    2026-09-23_194301_bob/
      report.md
      screenshot.png
      world.json
      client.json
      server-log.jsonl
      character/
        aisling.json  bank.json  effects.json  equipment.json  inventory.json
        legend.json   skills.json  spells.json  trackers.json
```

`report.md`:

````markdown
---
id: "2026-09-23_194301_bob"
kind: in-game
category: map-warp
title: "Walked through the east door of the Mileth inn and landed inside"
author: "Bob"
created: 2026-09-23
reported_at: 2026-09-23T19:43:01-04:00
map: "Mileth Inn"
map_key: "mileth_inn"
x: 12
y: 7
client_build: "0.1.0+c0a7eb7"
screenshot: true
status: open
status_note: ""
---

## Report

```text
Walked through the east door of the Mileth inn and landed inside the wall.
Can't move in any direction. Had to log out to get free.
```

## Details

- Warrior, level 41. HP 812/900, MP 120/300. Alive.
- Group: Aroha (Priest 38), Kinley (Wizard 40)
- Client: 0.1.0+c0a7eb7, Windows 11, 60 fps, 45 ms, large HUD, 1280×960
- Files: screenshot.png, world.json, client.json, server-log.jsonl (214 lines), character/

## Triage

<!-- sync:preserve -->
````

Rules:

- `title` is the first 60 characters of the description, on one line.
- `author` is the character name. `created` is the local date. The Discord index reads both.
- The description goes in a fenced `text` block. The fence is one backtick longer than the longest backtick run in the text, and at least three. Control characters other than newline and tab are removed. So the player's text cannot add header fields, headings or the preserve marker.
- Every quoted header value is escaped the same way `archive.yaml_quote` does.
- Failures go under Details as `- Note: <what failed>`, for example `- Note: picture not saved (length did not match)`.
- The status values are the Discord archive's: `open`, `fixed`, `wontfix`, `wanted`, `duplicate`.

## Import and index (Unora)

`Unora/Tools/FeedbackSync/import_reports.py`, Python standard library only:

```
python Tools/FeedbackSync/import_reports.py <path to the copied BugReports folder>
```

- For each `<category>/<report>/` in the source, except `_pending`, copy the folder to `docs/player-feedback/in-game/<category>/<report>/` only if it does not exist there.
- Never overwrite or delete a local report, so `status`, `status_note` and Triage survive a re-copy.
- Print how many were imported and how many were skipped.
- Rebuild `docs/player-feedback/in-game/index.md` from every local `report.md`.

`archive.render_index` looks the title up in `INDEX_TITLES` by kind and takes a `stamp_label` ("Imported" for in-game, versus Discord's default "Synced"). The Discord output stays byte-for-byte the same. The in-game index groups by status in the same order. Each line is:

```
- [<title>](<category>/<report>/report.md) — <category>, <author>, <created> — <status_note>
```

`docs/player-feedback/.gitignore` gains `in-game/`, because reports hold bank contents, trackers and other players' names. `docs/player-feedback/README.md` gains a line on the folder and the command.

## Removed

- The Discord posting code in `TerminusBugReportScript`. Its bot token was empty.
- The seven dialog files in `Unora/Data/Configuration/Templates/Dialogs/Temauir/terminus/Report System/`.
- The Discord.Net package stays. `AdminTrinketScript` and `ArenaUndergroundScript` still use it.

Added dialog data:

- `terminus_bugreport.json` (Normal, script key `terminusBugReport`).
- `terminusBugReport` added to the `scriptKeys` of `terminus_initial.json`.

## Tests

**Chaos-Server tests:**

- `BugReportOpen` and all three `BugReportInteraction` actions survive a write and read back unchanged.
- The wait time: a second report within 2 minutes is refused, and one after is allowed.
- Report numbers: a number works once. It fails after use, after expiry, after `Cancel`, and from another character.
- Parts join correctly in order, out of order and duplicated. An oversized `PictureLength` keeps no picture. A 30-second gap finishes without the picture.
- A picture that fails the PNG signature or length check is not saved, and a note says why.
- `report.md` stays safe with hostile descriptions: backtick runs, a fake `---` header, `## Triage`, and `<!-- sync:preserve -->`.
- Log reader: it keeps only the time window and the exact name, reads across midnight, stops at the cap, and works on a file held open for writing.
- Folder names: the category folder, the timestamp format, and `_2` on a clash.
- Holding folders: removed on cancel, expiry, logout without submit, and startup.

**Client tests (TUnit):**

- A picture split into parts of 32 KB or less joins back to the original bytes, including an exact multiple of 32 KB.
- Send Report enables only with a category picked and 10 or more trimmed characters.

**Import tests (Python):**

- It copies new folders and skips existing ones and `_pending`.
- The index groups by status and links to each `report.md`.
- The Discord index output for the existing fixtures is unchanged.

**Manual, local server and client:**

- F1 → Report a bug → Send writes a complete folder in the right category.
- The picture shows no Terminus dialog and no report window.
- Unticking the box sends no picture, and `report.md` says `screenshot: false`.
- A second report within 2 minutes gets the wait message, and no window opens.
- Close and Esc delete the holding folder.
- A version 751 client is turned away at the lobby.

## Rollout

1. Merge the `Chaos-Server` change, then move the submodule pointer in `Chaos.Client`.
2. Release client 752 and the server together, because the server refuses 751.
3. Deploy the `Unora` dialog data with the server.

## Success criteria

- A player reports a bug in under a minute without leaving the game.
- Each report arrives in its category folder with the text, a clean picture, the character, the world state around them, their client details, and the server's log lines about them.
- Staff import reports on their own machine and triage them next to the Discord reports, and re-importing never loses their notes.
