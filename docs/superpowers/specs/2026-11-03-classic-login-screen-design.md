# Classic login screen (retail look): design

Date: 2026-11-03. Status: approved by the user in brainstorming (Parts 1 to 3), spec approval waived ("get it done").
Repos: Chaos.Client (code), Unora (art and the art tool). No server or protocol change.

## Goal

Unora's login screens temporarily use the retail Dark Ages (Nexon, 2002) look. The current screens stay in the
code and in the data files, so one setting brings them back.

In scope: the start screen, login box, server list, password change, agreement box and character creation.
Out of scope: loading bars, the goodbye screen (`term.spf`), the in-game beauty window, and teal specks in other
retail interface art.

## The switch

- `GlobalSettings.ClassicLogin => true;` in `Chaos.Client/GlobalSettings.cs`, next to `RequireSwimmingSkill`. It is a
  fixed value on purpose: turning the classic look off means a client update. It is not an F4 option.
- New static class `LoginLayouts` in `Chaos.Client/Controls/LobbyLogin/LoginLayouts.cs`:
  - `public static string Resolve(string current, bool classic)` maps `_nstart`->`lmain`, `_nlogin`->`llogin`,
    `_nsvr`->`lsvr`, `_npw`->`lpasswd`, `_nagree`->`lagre`, `_ncreate`->`lcreate` when `classic` is true. It returns
    `current` unchanged when `classic` is false or the name is not in the table. Matching ignores case.
  - `public static string For(string current) => Resolve(current, GlobalSettings.ClassicLogin);`
  - Tests call only `Resolve`. Touching `GlobalSettings` in the test host ends the process, because its static
    constructor exits when `cious.dat` is missing.
- The six lobby controls pass `LoginLayouts.For("<current name>")` to their `PrefabPanel` base constructor:
  `LobbyLoginControl`, `LoginControl`, `ServerSelectControl`, `PasswordChangeControl`, `LoginNoticeControl` and
  `CharacterCreationControl`, all in `Chaos.Client/Controls/LobbyLogin/`.
- `GenderSelector` (the in-game beauty window) keeps its hard-coded `_ncreate` and does not change.
- The old layout files already load: `UiComponentRepository.LoadPrefabSet` tries Cious, then Setoa, and the six
  `l*.txt` files are in `setoa.dat`. `PrefabPanel.Create*` already returns null for control names a layout lacks.
- Palettes need no change. `UiComponentRepository.GetGuiPaletteNumber` already maps `lod0`/`dlgcre` to 4,
  `lodbk` to 7 and `dlgcre01` to 8.

## How each screen behaves on the classic look

### Start screen (`lmain`)

- Background `lod00.epf` (edited, see Art). Buttons Create, Continue, Password, Credit, Homepage and Exit use
  `lod01.epf`. Mouse-over shows the button's second frame, through the existing hover swap in `LobbyLoginControl`.
- Corner picture: `lmain` has an `ExtraBack` image slot at 0,149 (341x331) that shows `lodbk00.epf`. The control
  shows one of `lodbk00.epf` to `lodbk08.epf`, picked at random each time the start screen is built. That covers
  launch, logging out and a lost connection. If `LobbyLoginScreen` reuses the start panel across logouts, the pick
  also happens when the panel is shown again.
  - Pure helper `CornerPicture.Pick(Random random)` returns `"lodbk0" + random.Next(9) + ".epf"`.
  - The picked texture comes from the same EPF texture cache that `OkPopupMessageControl` uses for `butt001.epf`.
    That path picks palette 7 from the `lodbk` prefix.
  - On the current look there is no `ExtraBack`, so nothing changes.
- The `Version` label exists in `lmain` and keeps today's text.
- No crystal ball. `lmain` has no `LOGO` control, so `AnimatedLogo` is null and `LobbyLoginScreen` never creates
  the scrying view or sends ScryStart (`LobbyLoginScreen.cs` 167-172 and 252-268). No code change; the playtest
  confirms it.
- `LoginBound` is ignored. The login box stays centered on the screen, as today.

### Login box (`llogin`) and password change (`lpasswd`)

Their control names match what `LoginControl` (OK, Cancel, Name, Password) and `PasswordChangeControl` (OK, Cancel,
Name, Password, NewPassword, Confirm) create. OK and Cancel are painted into the box art. Only the layout name
changes.

### Server list (`lsvr`)

The `lsvr` base control is a full-screen 640x480 rectangle with no picture. The box is stacked from three pieces:
`ServerTopImage` (`svrtop.epf`, 413x146, a SERVER sign plus 5 row slots), `ServerMidImage` (`svrmid.epf`, 413x20,
one more row slot) and `ServerBotImage` (`svrbot.epf`, 413x17, the closing border).

- `ServerSelectControl` builds the stacked box only when the layout is `lsvr` and it contains `ServerTopImage`. The current `_nsvr` layout also has ServerTopImage/MidImage/BotImage, so otherwise it keeps today's
  fixed numbers (first row 37, name 28, description 108, row height 20).
- Stacked layout:
  - Middle strips: `max(1, serverCount - 5)`. The layout file itself stacks one, which gives 6 slots.
  - Box size: 413 wide, `146 + 20 * strips + 17` high, centered on the 640x480 screen.
  - Pieces are drawn top, strips, bottom, with no gaps.
  - Row `i` name text goes in the `ServerTop` rectangle (21,50 to 91,62) moved down by `i * pitch`. The description
    goes in the `ServerATop` rectangle (102,50 to 388,62) moved the same way. The row pitch is
    `ServerBottom.Y - ServerTop.Y`, which is 20.
  - A click on row `i` is any point from the name's left edge to the description's right edge, between the row top
    and the row top plus the pitch.
- Pure helper `ServerListLayout` holds this arithmetic (strip count, box height, row rectangles), so tests do not
  need the game data.

### Agreement box (`lagre`)

Background `legend.epf` (444x412), which has no painted buttons. Its `OK` and `CANCEL` controls are type 3 with no
images. When a button comes back with no normal texture, `LoginNoticeControl` gives it the `butt001.epf` frames
`OkPopupMessageControl` uses: OK 15 normal / 16 pressed, Cancel 21 normal / 22 pressed. The picture is centered in
the control's rectangle (82x31). `AGREEMENTTEXT` (15,62 to 429,362) holds the text as today.

### Character creation (`lcreate`)

- Background `dlgcre00.epf` (edited, see Art). OK, Cancel, gender, facing and hair-style arrows come from the layout
  with today's code.
- Hair color: `lcreate` has no `HairColor` swatch, and `CharacterCreationControl` already skips the swatch when it is
  missing. New `ColorLeft` and `ColorRight` buttons step `SelectedHairColor` through `SWATCH_COLOR_MAP` (14 colors),
  wrapping at both ends. They are wired the same way as `HairLeft`/`HairRight`, and they mark the preview dirty so
  the character preview shows the pick. On the current look these buttons do not exist and nothing changes.
  - Pure helper `HairColorCycle.Step(DisplayColor current, int direction)` returns the next color. It returns the
    first color for direction +1 and the last for direction -1 when `current` is not in the map.
- Not created: `NCID`, `NCPW`, `New`, `Help` and `Email`. Today's code never creates them, and the edited art has
  the NEXON Club block painted out.

## Art (Unora repo)

Claude made the art. The edits are reproducible from the retail originals.

- `Custom Client Mods/Classic Login Screen/Retail/`: the 12 untouched retail EPFs from `setoa.dat`.
- `Custom Client Mods/Classic Login Screen/Edited/`: the 12 edited EPFs.
- `Tools/ClassicLogin/classic_art.py` rebuilds `Edited/` from `Retail/` in about a minute. It reads the palettes and
  `_nstart.spf` from the live `setoa.dat`. `--preview DIR` writes before and after PNGs.
- `Tools/ClassicLogin/pack_setoa.py` writes `Desktop/Unora Dats/setoa.dat`: the live `setoa.dat` with the 12 entries
  replaced in place. It checks entry order and reads every entry back.

Edits:

- `lod00.epf`: Korean title and "Legend of Darkness" painted out. Unora medallion (cut from `_nstart.spf`, the
  current start screen) placed at center 504,90. "DARK AGES IV" emblem and the 2002 Nexon copyright line painted
  out.
- `lodbk00.epf` to `lodbk08.epf`: each has its own copyright line over the character art. Rows 200-311 are stretched
  down to cover rows 200-322, so the art stays sharp.
- `dlgcre00.epf`: NEXON Club block (plaque, logo, ID and Password labels and boxes, NEW ID, HELP) and the Korean
  title painted out. Medallion placed at center 84,410. The page fold fades out at row 236.
- All 12 files, including `dlgcre01.epf` (gender buttons): palette slot 255 removed. Retail drew white highlights
  with slot 255, which the display modes of the day showed as white. The gui palettes store teal there, so this
  client showed teal specks. In light surroundings the pixel becomes the image's whitest color; on dark frame lines
  it takes the surrounding color.

The current client never draws these 12 pictures. The new `setoa.dat` is safe to ship before or with the client
update, and it can stay if the classic look is turned off.

## Tests (TUnit, `Tests/Chaos.Client.Tests`)

- `LoginLayouts.Resolve`: all six names map when classic is on; nothing maps when it is off; unknown names and
  `_nchar`-style names pass through; matching ignores case.
- `HairColorCycle.Step`: +1 and -1 wrap at both ends; 14 steps of +1 return to the start; a color outside the map
  gives the first or last color.
- `CornerPicture.Pick`: 1,000 picks with a seeded `Random` stay within `lodbk00.epf` to `lodbk08.epf` and reach all
  nine.
- `ServerListLayout`: strip count for 0, 1, 5, 6, 7 and 10 servers; box height; row rectangles for rows 0 and 5.

## In-game check

Run the `/playtest` skill with the new `setoa.dat` in the local test client's data folder. Check the start screen
(corner picture, button hover, version text, no crystal ball), login, server list, password change, agreement and
character creation (color arrows, gender, hair style, facing, OK and Cancel). Create one character to prove the
picked hair color reaches the server. The user does a final look afterwards.

## Turning it off later

Set `GlobalSettings.ClassicLogin` to false and ship a client update. The current screens come back unchanged.
