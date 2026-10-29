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
