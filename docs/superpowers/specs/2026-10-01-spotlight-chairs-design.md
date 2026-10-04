# Spotlight Chairs — Design

**Date:** 2026-10-01
**Repos:** Chaos-Server, Unora (no client code change)
**Status:** approved in brainstorming. The theatre game deferred by `2026-10-01-haunted-theatre-design.md`.

## 1. Summary

Spotlight Chairs is a musical-chairs game on the Suomi theatre stage. The director puts people on the stage, then starts it from Thulin. The stage lights are the chairs. Each round turns on one fewer sweeping spotlight than the number of people still in, lets them move, freezes them, and gives everyone 2 seconds to step into a light. Anyone standing in a stopped beam's pool (the 3x3 tiles around its center) stays on the stage. A song plays while the lights move. Everyone else is walked off. The last person left wins an announcement, 10 Halloween candy once a day, and a winner's legend mark. Everyone who was on the stage when the game started gets a participation legend mark, and that mark unlocks an emblem.

The game runs on the theatre map script both theatres already use. It can only be started on the haunted theatre while the Halloween window is open (Oct 4 06:00 UTC through Nov 4 06:00 UTC).

## 2. Decisions

| Question | Decision |
|---|---|
| What a chair is | The 3x3 tiles around the center of a spotlight beam when that light stops. That is the whole medium pool as drawn. (Changed 2026-10-04: it was only the center tile, and players standing in the light lost.) |
| How many chairs | One fewer than the people still in the round. The stage holds at most 8 lights, so the game starts with 2 to 9 people. |
| Who stays | Each light saves one person in its pool. Pools on neighbouring rows overlap, so the game seats as many people as it can. When two people want the last seat, the one nearer a center wins, then the one who got there first. An empty pool saves nobody. |
| Time to react | Judging waits 2 seconds after the lights stop, so people can step into a light. (Added 2026-10-04.) |
| Music | Track 69 plays while the lights move and fades to silence when they stop. The theatre's own music comes back when the game ends or is called off. (Added 2026-10-04.) |
| Who leaves | Everyone else who was still in is walked off the stage. |
| Who starts it | A director or an admin, from Thulin's Theatre Options, after the players are already on the stage. |
| Who is in | Everyone standing on the stage when it starts, including the director if she is up there. |
| Winner | House announcement, 10 Halloween candy once a day, and a winner's legend mark that counts up. |
| Everyone who started | A participation legend mark at the start. The first one unlocks the emblem. |
| Where it runs | The shared theatre script. The Halloween window check only passes on `suomi_theatre_halloween`. |
| Lights already on the stage | Saved when the game starts and put back when it ends or is called off. |

Rejected: a separate script only on the haunted map, and a mode where the director aims the lights by hand.

## 3. A round

`StageRectangle` is `(0, 12, 9, 9)`, tiles x 0–8 and y 12–20. The map script already ticks once a second, and every wait below is a whole number of seconds.

### 3.1 Starting

The starter must pass `SuomiTheatreMapScript.CanDirect` (admin, or the Director role). The start is allowed only when `EventPeriod.IsSpecificEventActive(DateTime.UtcNow, LoadedFromInstanceId, EventType.Halloween)` is true. That is true only for `suomi_theatre_halloween` inside the Halloween window. The normal theatre is not in that event's map list, so the options never appear there.

One game per map. The stage must hold 2 to 9 aislings. The people on the stage at that moment are the participant set for the whole game. Someone who walks on later is not in it.

Refusals are an orange bar to the starter, and the lights are left alone:

- "Only the director can start Spotlight Chairs."
- "Spotlight Chairs is only played during Halloween."
- "Spotlight Chairs is already going."
- "Spotlight Chairs needs at least two people on the stage."
- "Spotlight Chairs can only seat nine people. Move someone off the stage."

Starting asks "Start Spotlight Chairs?" with Yes and No, the same shape as the existing light options. Yes begins the game.

On Yes, before the first sweep:

- Remember the current lighting with `StageLighting.ToScene`, including house level and stage glow.
- Give every participant the participation legend mark (section 5).
- Start round 1.

### 3.2 Sweep

At the start of a round, drop anyone in the set who is no longer on the stage. Then:

- 0 people: the game ends with no winner.
- 1 person: that person wins, with no further sweep.
- 2 or more: turn on `count - 1` lights.

Light `i` (counting from 0) sweeps row `y = 12 + i`, from x 1 to x 7 when `i` is even and from x 7 to x 1 when `i` is odd. Column 0 of rows 12 through 19 is the backstage warp, so a chair there would send its sitter off the map (changed 2026-10-02; it was 0 to 8). Eight lights use rows 12 through 19. Row 20 stays unused, and two lights never share a tile. Each light is white, medium, brightness 80, beam on, no effect, motion Sweep, speed 3. Speed 3 has an 8000 ms there-and-back period, the same table the client uses (`16000, 11000, 8000, 5500, 4000` for speeds 1 through 5).

The moving phase lasts a random whole number of seconds from 6 through 10, rolled at the start of that round. The house hears "The lights are moving."

The server copies the client's sweep formula and no client code changes. For elapsed milliseconds and speed 3, with period 8000:

- `phase = (elapsed % 8000) / 8000`
- `there = phase < 0.5 ? phase * 2 : 2 - phase * 2`
- `eased = there * there * (3 - 2 * there)`
- position = lerp from the near end to the far end by `eased`

Whole numbers in that tile space are tile centers. The chair is the nearest stage tile to the position. A point exactly halfway between two tiles belongs to the tile closer to the sweep's far end. The client's own test is the fixture: a sweep from `(0, 12)` to `(8, 12)` at speed 3 is x = 0 at 0 ms, about 4 at 2000 ms, 8 at 4000 ms, and 0 at 8000 ms, and at 500 ms its x is still under 1.

The server clock from the moment the light was added is the authority. Clients animate the sweep from when they receive it, so they can be a fraction of a second apart while it is moving. The freeze sends a Still light, and that is the position everyone plays by.

### 3.3 Freeze

On the first tick at or after the rolled duration, each light becomes Still on the center of its chair tile (`X = tileX * 16`, `Y = tileY * 16`, the unit `StageLightInfo` already uses). The house hears "The lights have stopped." The chairs are the lights sent when the sweep began, even if someone left during it, so every beam a player could see is still a chair.

The music fades to silence. Nobody is judged yet. People have 2 seconds (two ticks) to step into a light.

Then each chair saves at most one person standing within one tile of its center, diagonals included. The game seats as many people as it can across overlapping pools. When two people want the last seat, the one nearer a center wins, then the one who entered their tile earliest, then the lower aisling id. A visit starts when they enter the tile, or at the beginning of the sweep if they were already standing there. A chair with nobody in its pool saves nobody.

Anyone in the set who did not claim a chair is out. They are walked off with the same search as jumping off the stage: a spiral of 6, outside the stage rectangle, walkable for that person, with no reactor tile. They get an orange bar, "You missed the light." If that search finds no tile, they stay where they are, get the same orange bar, and are still removed from the set. The next round counts the set, so a person left standing on the stage who is out of the set is ignored.

After judging, the lights stay on those tiles for 4 more seconds. People still in may walk. The next round then begins at 3.2.

### 3.4 Leaving early

A participant who walks off the stage, warps away, or logs out is removed from the set. If that leaves one person, that person wins on the next tick without another sweep. If it leaves nobody, the game ends with no winner.

If the Halloween window closes while a game is running, the game ends as a call-off: lights go back, no winner. Participation marks already given stay. This matches the theatre swap, which moves everyone except god mode off the haunted map when the window ends.

### 3.5 Calling it off

While a game is running, Theatre Options shows "Stop Spotlight Chairs" instead of the start option. It asks "Call off Spotlight Chairs?" Yes restores the saved lights, tells the house "The director called off Spotlight Chairs.", and names no winner. Candy and the winner's mark are not given. Participation marks stay.

While a game is running, director lighting changes are refused with "The spotlight game is using the lights." That covers adding, moving, removing, and clearing lights, loading a scene, and changing the house level or stage glow. The restore at the end of the game is the one lighting change allowed.

## 4. End of the game

Saved lights are restored when someone wins, when the stage empties, when the game is called off, or when the window closes.

A winner who is still on the map gets:

- An active message to everyone on the map: "{Name} won Spotlight Chairs."
- The winner's legend mark, accumulated.
- 10 `halloweencandy`, the item Thulin already trades at 100 for a Macabre Box, given with `GiveItemOrSendToBank`. A timed event `spotlightchairsCandy` of 24 hours blocks a second stack. Inside that time they still get the announcement and the legend mark, plus an orange bar, "You already found candy today."

A game with no winner sends none of those three.

## 5. Legend marks and the emblem

Both marks use `MarkIcon.Yay`, `MarkColor.White`, and `AddOrAccumulate`.

| | Text | Key |
|---|---|---|
| Participation, given to the starting set | Took the stage for Spotlight Chairs | `spotlightchairs` |
| Winner, once per win | Won Spotlight Chairs | `spotlightchairsWin` |

The emblem template is `Data/Configuration/Templates/Emblems/spotlightchairs.json`:

- key `spotlightchairs` (at most 32 characters, and it must not change once shipped)
- name `Spotlight` (at most 28 characters)
- description `Took the stage for Spotlight Chairs.`
- art `257`, the next free custom number after Mirror Walker at 256
- source `{ "legendMark": "spotlightchairs" }`, so count 1 is enough and a higher count does not grant a second emblem

Art 257 is an RGBA PNG, at most 11×11, transparent background, at `Unora/Tools/Emblems/art/custom/embl257.png`. New art ships in setoa.dat. Until that launcher patch is out, a player sees an empty emblem cell. The mark is still granted. `/reload emblems` loads the template. No server restart.

## 6. Code

No client change. The client already draws Sweep and Still lights.

Chaos-Server:

- A pure type for the rules and the sweep math: the start checks, the light layout, the chair tile, who claims a tile, and who is still in. It does no warping and no item giving. Tests cover it without a map.
- `SuomiTheatreMapScript` runs the clock, saves and restores `StageLighting`, walks people off, gives the marks and the candy, and refuses lighting edits during a game.
- `SuomiTheatreScript` adds the two Theatre Options entries, shown only when the Halloween check passes and `CanDirect` is true. Start is hidden while a game is running. Stop is shown only then.
- Two dialog templates beside the existing Thulin light dialogs, `suomitheatre_spotlightstart` and `suomitheatre_spotlightstop`, each a Yes/No menu on the `suomitheatre` script.

Unora: the emblem template and `embl257.png` from section 5.

## 7. Tests

Server tests, in the existing theatre test project:

- The sweep fixture from section 3.2, including the 500 ms ease.
- A halfway point between two tiles belongs to the tile closer to the far end.
- Start refused for a non-director, outside the window, while a game is running, and for 1 or 10 people on the stage.
- Two people on one center: the earlier entry stays, the later one is out. An empty center saves nobody.
- After a freeze, the set is the people who claimed a chair. Someone who could not be walked off is still out of the set.
- One person left in the set wins without another sweep. An empty set ends with no winner.
- Participation marks are given at the start. The winner mark accumulates. Candy is given once, and a second win inside 24 hours skips the candy. A called-off game gives no winner mark and no candy.
- Lighting edits are refused while the game runs, and the scene saved at the start is the scene restored at the end.
