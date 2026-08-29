# Josephine's Mirror (Beauty Shop Panel) — Manual Walkthrough

Task 13 of `.superpowers/sdd/2026-08-29-beauty-shop-panel/task-13-brief.md`. This run was performed with **the user away**, so only the headless checks (H1, H2) were executed. All in-game checks (#1–#9) are recorded as `PENDING (user)` and must be run through the client by hand before this feature is considered verified end-to-end.

## Setup

Repos, all on `feature/beauty-shop`:
- Client: `C:\Users\mikeb\Documents\GitHub\Chaos.Client` (HEAD `717b622`)
- Server (submodule): `C:\Users\mikeb\Documents\GitHub\Chaos.Client\Chaos-Server` (HEAD `ec0971b86`)
- Data: `C:\Users\mikeb\Documents\GitHub\Unora` (HEAD `0e7ac9284`)

### Boot the server (Debug — Release reads the wrong `StagingDirectory`)

1. Confirm `Chaos-Server/Chaos/appsettings.local.json` → `StagingDirectory` points at `C:\Users\mikeb\Documents\GitHub\Unora` (a local, uncommitted edit — never commit it).
2. Confirm no `Chaos` process is already running (`tasklist | findstr /i chaos`).
3. From `C:\Users\mikeb\Documents\GitHub\Chaos.Client\Chaos-Server\Chaos`:
   ```bash
   dotnet run -c Debug
   ```
4. Wait for `WorldServer: Listening` and the `Beauty shop catalog loaded: <N> male styles, <N> female styles, <N> faces` line.
5. Local ports: lobby 4200, login 4201, world 4202, http 5000.

### Launch the client

Per the `unora-client-run-config` memory, set env vars before launching (repo defaults are stale):
- `DA_LOBBY_HOST` / `DA_LOBBY_PORT` — point at the locally booted server (`127.0.0.1`, lobby port `4200`) rather than the live `unora.freeddns.org:6900` when testing this branch locally.
- `DA_PATH` — the folder containing the 20 `.dat` archives (e.g. `C:\Users\mikeb\Documents\Chaos\Unora`).

Log in, and walk to the Mileth Beauty Shop (Josephine at `(5,6)`).

### During the run

- Use `Unora/Data/Saved/<name>` (after logout) or the orange-bar gold readout to confirm gold deduction.
- Grep the server log for `Beauty Shop` to confirm per-category + total log lines.
- `Unora/Data/Saved` and `Unora/Data/Backups` pick up test-play noise from any boot — do not commit them.

## Results

| # | Check | Expected | Result | Notes |
|---|-------|----------|--------|-------|
| 1 | Click Josephine → greeting → mirror | Greeting shows with one option; choosing it opens the mirror with the player's current look and correct gold | PENDING (user) | Requires interactive client session |
| 2 | Step hairstyle/dye/skin/face; rotate; gear toggle | Preview updates immediately; total = sum of changed rows; four facings on rotate; "Show gear" layers equipped armor/weapon | PENDING (user) | Requires interactive client session |
| 3 | Apply with hairstyle + dye change | Panel closes, orange-bar message shown, world sprite matches preview, gold drops by exactly the total | PENDING (user) | Requires interactive client session |
| 4 | Apply with insufficient gold | Panel stays open, shows "You can't afford that.", nothing changes | PENDING (user) | Requires interactive client session |
| 5 | Gender change confirm | Confirm dialog shown; OK reshapes gear + sprite; Cancel leaves panel open unchanged | PENDING (user) | Requires interactive client session |
| 6 | Escape / Close | Discards changes; reopening shows the current (unchanged) look | PENDING (user) | Requires interactive client session |
| 7 | Fresh character, Riona tutorial | Josephine's greeting still grants 1000g / 1000exp / 5 GP | PENDING (user) | Requires interactive client session with a fresh character |
| 8 | Server log per-category + total lines | One `Beauty Shop` line per changed category plus a total line | PENDING (user) | Depends on an actual Apply from check #3; nothing to grep without a live apply |
| 9 | Branch integration (submodule pointer bump) | Out of scope for this task — controller performs Step 5 after final review | N/A | Not attempted per task scope |
| H1 | Server boots against the data branch and loads the beauty shop catalog | Log contains `Beauty shop catalog loaded: 101 male styles, 102 female styles, 18 faces` and `WorldServer: Listening`; no error/warning mentioning `josephine`, `beautyShop`, `BeautyShop`, or dialog-template failures | PASS | See "H1 — Headless live check" below for exact log lines and counts |
| H2 | Automated suites | Server `BeautyShop*` filter: 29 passed. Client suite: 7 passed | PASS | See "H2 — Automated suites" below for exact summaries |

## v2 panel (2026-08-29)

Task 5 of `.superpowers/sdd/2026-08-29-beauty-shop-panel-v2/task-5-brief.md`. Client repo `C:\Users\mikeb\Documents\GitHub\Chaos.Client`, branch `feature/beauty-shop-v2` (HEAD `668c22c`). Server (`Chaos.exe`) was already running locally against 127.0.0.1:4201 and was left running throughout. All rows below are recorded as `PENDING (user)` and must be run through the client by hand before the v2 panel is considered verified end-to-end.

| # | Check | Expected | Result | Notes |
|---|-------|----------|--------|-------|
| 1 | Open the mirror panel | Panel opens at 600×470 with the title "JOSEPHINE'S MIRROR" and its subtitle; no "* Unsaved changes" indicator yet | PENDING (user) | Requires interactive client session |
| 2 | Preview pedestal, 2x toggle, facing, gear | Preview shows the sprite at 2x scale standing on the pedestal; the "2x" button toggles the preview to 1x and back; ◀ ▶ cycle through all four facings; "Show gear" layers equipped armor/weapon onto the preview | PENDING (user) | Requires interactive client session |
| 3 | Hover a hairstyle thumbnail | Hovering changes the preview sprite to that hairstyle and shows "if chosen: TOTAL n"; moving off the thumbnail reverts both the sprite and the total | PENDING (user) | Requires interactive client session |
| 4 | Click a hairstyle thumbnail | Selects it (gold border + badge); "* Unsaved changes" appears; the purchase summary lists a "Hairstyle 1,000" line | PENDING (user) | Requires interactive client session |
| 5 | Hairstyle strip paging | ◀ ▶ page arrows move the hairstyle strip; the caption's "page p/P" updates; thumbnails do not flicker while paging | PENDING (user) | Requires interactive client session |
| 6 | Dye swatch grid | Hovering a swatch previews the dye; clicking selects it (gold border); the caption shows the colour name | PENDING (user) | Requires interactive client session |
| 7 | Skin and face strips | Behave like the hairstyle strip (hover preview, click select, paging); the face caption shows the face name | PENDING (user) | Requires interactive client session |
| 8 | MALE \| FEMALE segmented selector | Switching gender remaps the hairstyle and face strips to the new gender's options; a gender line appears in the purchase summary | PENDING (user) | Requires interactive client session |
| 9 | Randomize | Changes hairstyle, dye, skin, and face; never changes the selected gender | PENDING (user) | Requires interactive client session |
| 10 | Discard Changes | Disabled with no pending changes; becomes enabled after any change; clicking it restores hairstyle/dye/skin/face/gender to the values on open | PENDING (user) | Requires interactive client session |
| 11 | Unaffordable total | When the running TOTAL exceeds the player's gold, the TOTAL text turns red and the APPLY button disables | PENDING (user) | Requires interactive client session |
| 12 | Apply | Charges exactly the TOTAL shown, closes the panel, and produces a server orange-bar confirmation message | PENDING (user) | Requires interactive client session |
| 13 | Esc / Close, then reopen | Esc or Close discards pending changes without charging gold; reopening the mirror shows the player's current (saved) look at 2x with gear off | PENDING (user) | Requires interactive client session |

## H1 — Headless live check

- `Chaos-Server/Chaos/appsettings.local.json` `StagingDirectory`: `C:\Users\mikeb\Documents\GitHub\Unora` (already correct; not edited).
- No `Chaos` process was found running before boot.
- Server booted via `dotnet run -c Debug` in the background, output redirected to `.superpowers/sdd/2026-08-29-beauty-shop-panel/server-boot.log`.
- Catalog line observed: `Beauty shop catalog loaded: 101 male styles, 102 female styles, 18 faces`
- Listening line observed: `WorldServer: Listening`
- Error/warning grep (`ERROR`, `Exception`, `josephine`, `BeautyShop`, `beautyShop`): no matches.
- Server process was then stopped (the PID launched by this check only); confirmed no longer running.

## H2 — Automated suites

- Server: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShop*/*"` → **29 passed**, 0 failed.
- Client: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → **7 passed**, 0 failed.

(Exact summary lines are in `task-13-report.md` alongside this walkthrough.)
