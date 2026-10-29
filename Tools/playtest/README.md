# Playtest runner

`Tools/playtest/playtest.ps1` sets up a throwaway playtest environment: a
git worktree per feature, a Debug build of client and server, a copy of the
Unora data folder, three auto-login admin test accounts (Dose, Whug, Bob), and a
server plus three clients on ports 4300-4302. `pt.ps1` (same folder) polls one
client over its `DA_TEST_PIPE` named pipe.

## Config (`playtest.config.json`)

- `unoraPath`: Unora checkout to copy into the run `data\` folder.
- `clientRepo`: Chaos.Client checkout the worktree is created from.
- `worktreeRoot`: parent folder for `playtest-<feature>` worktrees.
- `gameDataPath`: Dark Ages game-data folder (`cious.dat`); passed to clients
  as `DA_PATH`. Empty stops `up` with an error.
- `password`: plain-text password for the configured clients (stored as upper-case
  SHA-512 hex in each `password.txt`, only inside the run data copy).
- `clients`: existing character names (default Dose, Whug, Bob); each name must
  already have a folder under `Unora\Data\Saved\Aislings` in the source data.
- `portBase`: lobby port; login/world follow at `+1`/`+2` (default 4300).
- `loginWaitSeconds`: per-client login wait (default 90).
- `runMinutes`: run `deadline` offset from start (default 45).

## Actions

- `powershell -File Tools/playtest/playtest.ps1 up smoke` creates (or reuses)
  `playtest-smoke`, builds, copies data, starts everything, prints
  `ready: Dose Whug Bob`.
- `powershell -File Tools/playtest/playtest.ps1 up smoke -ServerRef abc123`
  checks the server submodule out at `abc123` (default: HEAD of the shared
  `Chaos.Client/Chaos-Server` checkout) before building.
- `powershell -File Tools/playtest/playtest.ps1 logs` prints new server log
  lines since the last call, errors first.
- `powershell -File Tools/playtest/playtest.ps1 rebuild` stops everything,
  rebuilds, re-patches config, and starts again.
- `powershell -File Tools/playtest/playtest.ps1 down` stops every PID in
  `run.json` plus any `Chaos.exe`/`Chaos.Client.exe` under the worktree.

## Run folder

`%LOCALAPPDATA%\chaos-playtest\<yyyyMMdd-HHmm>-<feature>\` holds `data\`,
`logs\`, per-client `shots\` folders, and `run.json` (start time, deadline, PIDs,
worktree path). `current.txt` next to it points at the latest run.

Warning: Don't run `up` while your own server uses ports 4300-4302.

## Report

File the playtest report at `docs/playtests/reports/<yyyy-MM-dd>-<feature>.md`
in the Unora repo.

## Kart units (for `state kart.*` and `kartdrive.ps1`)

- `kart.x`/`kart.y` (and `kart.vx`/`kart.vy`, gates, centre line, respawns)
  are in **tiles**, as doubles with sub-tile precision
  (`KartBody`: "position and motion in tiles"; `TileX = floor(X)`).
  Velocities are **tiles per second** (`X += Vx * dt`); `kart.speed` is
  `sqrt(vx^2 + vy^2)`.
- `kart.heading` is in **radians**, normalized to `[0, 2*pi)`. 0 points along
  `+x` (east) and the kart moves along `(cos(heading), sin(heading))`; `+y`
  is south, so growing angles turn **clockwise (right)**.
- The shared physics confirms the steer direction: `Steer` is `+1` for Right
  only and `-1` for Left only, and each step does
  `heading += steer * TurnRate(...)` (times `-1` in reverse), so holding
  **Right increases the heading** and holding Left decreases it.
- `kart.next` is `Gates[GatesPassed % Gates.Count]`: gate 0 is the finish
  line, racers start behind it, and the count keeps rising across laps, so
  the modulo is also the lap wrap (`LapTracker.Move` in the server code).

## kartdrive.ps1

`powershell -File Tools/playtest/kartdrive.ps1 -Client Dose` drives one client
around the track with pure pursuit over `state kart.track`'s centre line.

- `-Client <name>` (required) matches `pt.ps1`: pipe `chaos-pt-<name>`.
- `-Seconds <n>` (default 300): stop after this long (exit 1 on timeout).
- `-Lateral <tiles>` (default 0): aim this far right (positive) or left
  (negative) of the centre line, e.g. to hit a vent or the sea on purpose.
- `-UseItems` (switch): tap Space whenever `kart.item` is not None.
- `-Laps <n>` (default: the race's lap count): stop after this many laps done.
- Exits 0 when the stage reaches Results/Done/Finished or the lap target is
  met, 2 on pipe error. All held keys are released before exit.
