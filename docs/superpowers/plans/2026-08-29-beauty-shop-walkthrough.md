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
