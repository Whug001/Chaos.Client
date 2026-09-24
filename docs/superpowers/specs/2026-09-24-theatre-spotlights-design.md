# Theatre Spotlights — Stage Lighting Window

Date: 2026-09-24. Spans three repos: Chaos.Client (window and drawing), Chaos-Server (lighting setup,
messages, Theatre scripts) and Unora (dialog data).

Mockups from the design session: `.superpowers/brainstorm/1086360-1790245966/content/`, in particular
`spotlight-look.html`, `house-lights.html` and `board-compact.html` (the approved window).

## Summary

The Garamonde Theatre director gets a Stage Lighting window. From it they can:

- dim the house lights anywhere from 0% to 100%, instead of only switching them on or off;
- place up to 8 colored spotlights on the stage and drag them around;
- give each light a color, size, brightness and an optional beam;
- give each light an effect (pulse, flicker, color cycle) and a motion (sweep, circle, follow a person);
- save the whole setup as a named scene and load it later with a 1-second fade.

Everyone in the Theatre sees the same lights.

## Why

Today the director has one lighting control: "Turn Lights Off" / "Turn Lights On" in Thulin's Theatre
Options. The client can only draw white light (lanterns thin out a black darkness layer). The ambient
effects work (fog, blood moon, fireflies) showed the client can draw rich screen effects. Spotlights
give directors a real lighting board for shows.

## Goals

- The director can light a show without leaving the Theatre floor or reopening a dialog.
- Everyone in the Theatre sees the same setup, including players who walk in mid-show.
- Moving lights cost no network traffic while they move.
- The window leaves most of the stage visible, and it can shrink to a thin bar.

## Non-goals

- No spotlights outside the 9×9 stage (the user chose stage only).
- No lights on other maps. The client code is map-agnostic, but only the Theatre sends lighting data.
- No per-player "hide spotlights" option. The admin `/atmosphere` switch does not hide spotlights.
- No GPU rewrite of the darkness layer. Spotlights reuse the current CPU-built darkness texture.
- No fallback for the plain Dark Ages client. All players use Chaos.Client.

## Behavior

### Who can use it

The director (`TheatreRoles.Director`) and admins (`IsAdmin`). These are the same people who see
Theatre Options today. The server checks the role on every edit, not only when the window opens.

### Opening and closing

- Theatre Options gets a new choice, "Stage Lighting". It opens the window and closes the dialog.
- The window stays open while the director walks around the Theatre.
- The client closes the window when the player changes maps.
- If the server refuses an edit because the player lost the role, it sends a close message and an
  orange-bar explanation.

### House lights

- A dimmer from 0% to 100%. 100% is fully lit; 0% is today's "lights off".
- Blackout sets the house level to 0% in one click.
- "Turn Lights Off" and "Turn Lights On" stay in Theatre Options as shortcuts for 0% and 100%. They go
  through the same code path as the window, so the two always agree.
- Below 100%, audience members lose their lanterns. This matches today's lights-off behavior.
- Below 100%, people on the stage get a small lantern while **Stage glow** is on, and no lantern while
  it is off. Stage glow is on by default. With it off, only spotlights light the stage.
- Admins and the director keep their own lanterns, as today.

### Spotlights

Up to 8 lights. Positions are anywhere inside the stage rectangle, in 1/16-tile steps (no snapping).

| Setting | Values | Default for a new light |
|---|---|---|
| Color | 13 swatches + custom picker (any RGB) | white |
| Size | Small, Medium, Large | Medium |
| Brightness | 0–100 | 80 |
| Beam | on / off | on |
| Effect | None, Pulse, Flicker, Color cycle | None |
| Effect speed | 1–5 | 3 |
| Motion | Still, Sweep, Circle, Follow | Still |
| Motion speed | 1–5 | 3 |

A new light appears in the middle of the stage.

- **Sweep:** the light swings back and forth between its position and a second point. It eases in and
  out at each end, the way a person working a spotlight would.
- **Circle:** the light circles its position. The second point sets the radius.
- **Follow:** the light stays on a chosen person. The director picks them by clicking their square on
  the stage view, so they must be on the stage at that moment. If the person walks off the stage, the
  light stops at the stage edge nearest them. If they leave the Theatre, the light goes back to its own
  position and stays there.
- **Pulse:** brightness rises and falls smoothly between 30% and 100% of the light's brightness.
- **Flicker:** brightness jumps like a torch, between 55% and 100% of the light's brightness.
- **Color cycle:** the hue rotates around the color wheel at full saturation. The light's own color
  sets the starting hue.

Changing Sweep or Circle sets the second point to 2 tiles right of the light (held inside the stage).

### Speeds

Tunable constants. The plan puts them in one place.

| Speed | Sweep (there and back) | Circle (one lap) | Pulse (one cycle) | Flicker (jumps/s) | Color cycle (full wheel) |
|---|---|---|---|---|---|
| 1 | 16 s | 16 s | 4 s | 4 | 24 s |
| 2 | 11 s | 11 s | 3 s | 7 | 16 s |
| 3 | 8 s | 8 s | 2 s | 10 | 10 s |
| 4 | 5.5 s | 5.5 s | 1.4 s | 14 | 6 s |
| 5 | 4 s | 4 s | 1 s | 20 | 3 s |

### Light sizes

The pool radius, measured across the floor in tiles: Small 1, Medium 1.5, Large 2.5. The darkness mask
and the colored pool use the same size.

### Scenes

- A scene holds the house level, stage glow and every light.
- The Theatre keeps up to 20 scenes. Names are 1–24 printable characters. Names are unique, ignoring
  case, and "Save as" with an existing name replaces that scene.
- Any director or admin can save, load or delete any scene.
- Loading a scene fades from the current setup to the scene over 1 second.
- A light that follows someone is saved as a Still light at its current position. A person's ID means
  nothing after they log out.
- Scenes are saved to disk and survive restarts.

### What everyone sees

- Everyone on the Theatre map sees the same setup.
- A player who enters the Theatre gets the current setup at once, with no fade.
- Players in admin god mode see no darkness (today's rule), but they still see spotlights.
- The live setup resets to 100% house, stage glow on and no lights when the server restarts.

### Fades

Every setup message carries a fade time. Clients blend from what they show now to the new setup over
that time.

| Cause | Fade |
|---|---|
| Entering the Theatre | 0 ms |
| A light setting changed in the window, including drags | 150 ms |
| House level or stage glow changed | 400 ms |
| Blackout, Turn Lights Off / On | 300 ms |
| Scene loaded | 1000 ms |

## Architecture

The server owns the live setup. Clients draw it and run motions and effects themselves.

1. The director's client sends one edit.
2. The server checks the sender and the edit, and applies it to the Theatre's setup.
3. The server sends the whole setup to everyone on the Theatre map.
4. Each client blends to the new setup and animates it locally.

The whole setup is at most 257 bytes, so resending it after each edit is simpler than sending changes.
Motion does not need network traffic. Each light carries how far into its motion and effect it is, and
each client continues from that point.

## Server (Chaos-Server)

### `StageLighting` — the live setup

New plain class, for example `Chaos/Services/Theatre/StageLighting.cs`. It does no network or file work,
so it is unit-tested directly.

- State: `HouseLevel` (0–100), `StageGlow` (bool), the lights (0–8 `StageLight` records), the stage
  rectangle, and the time each light's motion and effect started.
- Methods, one per edit: `SetHouseLevel`, `SetStageGlow`, `TryAddLight`, `TryUpdateLight`,
  `TryRemoveLight`, `ClearLights`, `ApplyScene`, `ToScene`.
- Every method checks and corrects its input before changing state:
  - positions (both points) are held inside the stage rectangle;
  - brightness, speeds and house level are held to their ranges;
  - unknown enum values are corrected to the default (Medium, None, Still);
  - a 9th light is refused;
  - edits to an unknown light ID are ignored.
- The server picks light IDs (the lowest free number, 1–8). The client's ID on "add" is ignored.
- A light's motion start time resets when its motion, motion speed or points change. Its effect start
  time resets when its effect or effect speed changes. A color, size or beam change resets neither, so
  a moving light does not jump.
- A follow target must be an Aisling on the Theatre map and inside the stage when it is set.
  Otherwise the edit is refused.

### `TheatreLightingScenes` — saved scenes

New storage type, saved through the existing open-generic `IStorage<T>`. It works like `ChatFilters`
and `SlotJackpotState` and is written to `TheatreLightingScenes.json` in local storage. It holds a
name → scene map with the limits above. It calls `Save()` after each save or delete.

### `SuomiTheatreMapScript`

- Owns one `StageLighting` and gets `IStorage<TheatreLightingScenes>` through its constructor. The
  constructor already takes an injected `IEffectFactory`.
- Replaces `IsDarknessEnabled` with `StageLighting.HouseLevel < 100`. The only writers today are
  `HandleLightsOff` and `HandleLightsOn` in `SuomiTheatreScript`.
- Keeps `MapFlags.Darkness` in step with the house level:
  - when the level crosses 100% in either direction, set or clear the flag and call `Refresh(true)`
    on everyone, the same way Stage Effects does today. Then send the setup, so it arrives after the
    refreshed map info;
  - any other change only sends the setup message.
- `OnEntered(aisling)`: sends the current setup to that player, with fade 0.
- After any accepted edit: sends the setup to every Aisling on the map.
- `Update`: while the house level is below 100%:
  - audience members (off stage, not admin, not director) have their lanterns removed, as today;
  - people on the stage get `LanternSize.Small` while stage glow is on, and `LanternSize.None` while
    it is off. Today, only people on stage at the moment the lights go off get a lantern. After this
    change, people who step onto the stage later get one too.
- Exposes the edit entry point that the world server calls. It applies the edit, saves scenes if
  needed, and broadcasts.

The Darkness flag is Snow and Rain together. The existing Stage Effects rules for snow and rain while
dark stay as they are.

### World server

- New handler for `StageLightingInteraction`, like `OnBeautyShopInteraction`.
- It checks that the player's map script is `SuomiTheatreMapScript`, and sends `StageLightingBoard` Close
  when it isn't. Then it hands the edit to the map script.
- The map script's edit entry point checks that the player is the director or an admin. On a role
  failure it sends `StageLightingBoard` Close, plus an orange-bar message.
- Rate limit, also in the map script: edits from one player faster than 20 per second are dropped
  silently.

### Dialog (`SuomiTheatreScript` + Unora data)

- New option "Stage Lighting" in `suomitheatre_options.json`, pointing at a new dialog key
  `suomitheatre_stagelighting`. The script sends `StageLightingBoard` Open (stage rectangle + scene
  names) and closes the dialog.
- `HandleLightsOff` / `HandleLightsOn` call the map script's house-level entry point with 0 or 100
  (300 ms fade). They keep their current orange-bar messages. Their manual lantern loops are removed,
  because the map script's `Update` now does that job.

### Refusal messages (orange bar)

- "Only the director can change the stage lights."
- "The stage already has 8 lights."
- "The Theatre can only keep 20 scenes."
- "That person isn't on the stage."
- "There's no scene called <name>."

## Messages (Chaos-Server `Chaos.Networking`, shared with the client)

Each message gets an args record, a converter and a round-trip test, like `SetMapEffects`.

| Direction | Name | Opcode |
|---|---|---|
| Server → client | `StageLightingState` | 128 |
| Server → client | `StageLightingBoard` | 129 |
| Client → server | `StageLightingInteraction` | 125 |

These are the next free numbers. The plan checks them against the `Crypto` encryption tables and the
client's handler array size.

### `StageLightingState` (server → everyone in the Theatre)

| Field | Type | Notes |
|---|---|---|
| StageX, StageY, StageWidth, StageHeight | 4 × byte | the stage rectangle, so every client can hold followed lights on the stage |
| HouseLevel | byte | 0–100 |
| StageGlow | bool | |
| FadeMs | ushort | see Fades |
| Lights | byte count + entries | 0–8 |

Each light entry:

| Field | Type | Notes |
|---|---|---|
| Id | byte | 1–8 |
| X, Y | ushort, ushort | map position in 1/16-tile steps |
| X2, Y2 | ushort, ushort | second point (sweep end or circle edge) |
| R, G, B | 3 × byte | |
| Size | byte | Small / Medium / Large |
| Brightness | byte | 0–100 |
| Beam | bool | |
| Effect | byte | None / Pulse / Flicker / ColorCycle |
| EffectSpeed | byte | 1–5 |
| Motion | byte | Still / Sweep / Circle / Follow |
| MotionSpeed | byte | 1–5 |
| FollowId | uint | Aisling ID, 0 = none |
| MotionElapsedMs | uint | time since the motion started |
| EffectElapsedMs | uint | time since the effect started |

That is 31 bytes per light plus a 9-byte header, so at most 257 bytes.

### `StageLightingBoard` (server → the director only)

| Field | Type | Notes |
|---|---|---|
| Type | byte | Open / Scenes / Close |
| SceneNames | byte count + strings | Open and Scenes |

After a save or delete, the server sends Scenes to every director and admin on the Theatre map. A
client with no window open ignores it. When the window opens, Open brings the current list.

### `StageLightingInteraction` (client → server)

One record with every field. Each action ignores the fields it doesn't use.

| Action | Uses |
|---|---|
| SetHouseLevel | Level |
| Blackout | — |
| SetStageGlow | Flag |
| AddLight | Light (ID ignored) |
| UpdateLight | Light |
| RemoveLight | LightId |
| ClearLights | — |
| SaveScene / LoadScene / DeleteScene | SceneName |

"Light" uses the same fields as a state entry, without the two elapsed values.

## Client (Chaos.Client)

### State and animation

- **`StageLightingState` handler** (in `WorldScreen.ServerHandlers.cs`): hands the setup to the
  animator.
- **Clearing.** The client clears the setup and closes the window on a full map change, meaning a
  move to a different map. That's the same point where map effects reset. A same-map refresh
  (`Refresh(true)`, used when the darkness flag flips) must **not** clear it, or the lights would blink
  out.
- **`StageLightAnimator`**: plain math with no drawing, unit-tested. For each light at time *t*, it
  returns screen-independent values: floor position (tile units, fractional), RGB color, and strength
  (0–1).
  - Motion and effect times are anchored on receipt: *start = now − elapsed*.
  - Follow looks up the entity by ID in `WorldState`. It uses the entity's tile plus its walking
    offset, so the light moves smoothly mid-step, and it holds the result inside the stage. If the
    entity is missing, the light uses its own position.
  - Flicker comes from a hash of (light ID, time step), so every client computes the same jumps.
  - Fades: when a new setup arrives, the animator records what each light looks like now. It then
    blends position, color and strength to the new values over `FadeMs`. Lights are matched by ID. A
    new light fades in from strength 0, and a removed light fades out to 0. House level blends the
    same way.
  - The light the director is dragging ignores incoming updates until the drag ends. After the drag
    ends, the local copy is kept until the server's copy matches it, or for at most 1 second, so the
    light doesn't jump back for a moment.

### Drawing

Each spotlight is drawn in two layers.

**Layer 1: lifting the darkness.**
- `LightSource` gains a strength value (0–32, the darkness renderer's scale). Lanterns use 32.
  `DarknessRenderer.StampLightSources` scales each mask value by it.
- `LightingSystem.Gather` adds one source per spotlight after the lanterns. The mask is a generated
  soft oval with 2:1 proportions, one per size, made once and cached. It is centered a little above
  the floor point so it covers a standing person. Tile offsets for the Tab map come from the same
  radius.
- `LightingSystem.Gather` still gathers only when the map is dark, so nothing changes on lit maps.

**Layer 2: color and beam** — new `SpotlightRenderer` in `Chaos.Client.Rendering`.
- It draws after the darkness layer and before weather and ambient effects (new step 5b in the draw
  order), using additive blending.
- For each light, it draws:
  - a soft colored pool on the floor;
  - a taller, fainter tint over a person standing in it;
  - the beam, if on: a narrowing column from the pool to the top of the viewport.
- The three textures are generated once at startup, like `CloudNoiseTexture`.
- Color strength = light strength × (0.35 + 0.65 × (100 − house) / 100). The light reads as a wash at
  100% and as vivid at 0%.
- It draws whenever a setup with lights exists, dark or not.

**House level.** While a lighting setup exists, `DarknessRenderer` uses darkness strength
(100 − house) / 100 in black, instead of the full black it uses for dark maps. At 0% this is the same
as today. The Tab map's "fully dark" mode stays tied to full strength, so it only turns on at 0%.

**Cost.** Any moving light makes the darkness texture rebuild every frame. The client already does
this while a player with a lantern walks on a dark map. Check it with the debug frame counter.

### The Stage Lighting window

Approved layout: `board-compact.html`. It is about 380×248 on the 640×480 screen (the real font is 6×12,
so the plan fixes exact rows), and it opens centered. Buttons use ASCII captions (`<` `>` `-` `+` `x`),
because the game font has no arrow or box glyphs.

```
┌ STAGE LIGHTING ──────────────────────────────── – ✕ ┐
│ ┌ stage view 200×150 ─────┐  COLOR                   │
│ │   diamond 9×9 stage,    │  ■■■■■■🌈 (7 × 2)        │
│ │   lights, handles,      │  [S][M][L]     Beam [On] │
│ │   people                │  Bright ───●──           │
│ └─────────────────────────┘  EFFECT ◀ Pulse ▶ ──●──  │
│ (1)(2)(3) [+] [Remove] 3/8   MOTION ◀ Sweep ▶ ─●───  │
│                              hint line               │
├──────────────────────────────────────────────────────┤
│ House ──●──── 30%  [Blackout]          ☑ Stage glow  │
│ [Act 1 – Opening ▾] [Load] [Save as…] [Delete]       │
└──────────────────────────────────────────────────────┘
```

- **Stage view.** The diamond shape and colors follow the Tab map.
  - Lights are soft colored circles with their number. The selected light has a dashed ring.
  - Dragging a light moves it. Sweep lights show a dashed line to a ◆ handle, and Circle lights show
    their circle and a ◆ handle on it. Dragging ◆ sets the second point.
  - People on the stage are blue squares, and hovering one shows their name. The followed person gets
    a ring in the light's color.
  - With Motion set to Follow, clicking a person picks them. The hint line says "Click a person on the
    stage."
- **Light chips.** One per light for selecting it, plus Add, Remove and a count. Add is disabled at 8
  lights.
- **Color.** Thirteen swatches plus a rainbow swatch that opens the custom picker: a
  saturation/brightness square, a hue strip, a preview and OK. The light updates live while the picker
  is dragged. Each change is sent within the drag rate limit.
- **Effect and Motion.** ◀ ▶ pickers, each with its speed slider.
- **Footer.** The house dimmer (live while dragging, within the rate limit), Blackout and Stage glow.
  Below them, the scene dropdown, Load, "Save as…" (a name prompt) and Delete (asks to confirm).
- **Moving the window.** Dragging the title bar moves it, and it stays on screen.
- **Minimize (–).** Shrinks the window to a 230×20 bar where the title bar was. The bar holds the
  house dimmer, Blackout, restore (▢) and close. Position and minimized state last until logout.
- **Sending edits.** While dragging a light, a handle, a slider or the color picker, the client sends
  at most 8 updates a second, plus the final value on release.
- **View model** (`ViewModel/StageLighting.cs`) holds the selection, drag state, rate limiting and
  minimized state, and it is unit-tested. The control only draws and forwards input.

## Error handling

- **Bad or out-of-range input** is corrected or refused by `StageLighting`, never trusted. Refusals
  send an orange-bar message.
- **Lost role mid-show:** the next edit is refused, the window closes, and the player gets a message.
- **Scene file missing or corrupt:** it starts empty, the same behavior other `IStorage<T>` users
  get. Loading a scene that was deleted meanwhile is refused with a message.
- **Setup timing on entry:** the server sends map info before it calls the map script's
  `OnEntered` (`MapInstance.InnerAddEntity`). So the setup always arrives after the client has
  cleared the old map's setup. People the setup follows may show up in the viewport a moment later.
  Until they do, those lights use their own position.
- **Follow target logs out:** the light falls back to its own position on every client. The server
  keeps the ID. The next edit to that light, or a scene save, stores it as Still.

## Testing

**Server unit tests** (`Tests/Chaos.Tests/Theatre/`):
- `StageLighting`:
  - holding positions inside the stage, and ranges;
  - the 8-light limit and ID reuse;
  - which changes reset motion and effect start times;
  - follow checks;
  - `ToScene` turning Follow into Still;
  - `ApplyScene`.
- `TheatreLightingScenes`: the 20-scene limit, name rules, case-insensitive replace.
- Lantern rules: a pure helper that decides each player's lantern from house level, stage glow,
  on-stage, admin and director.

**Packet tests** (`Tests/Chaos.Tests/Networking/`): round-trip all three messages, with 0 and 8 lights
and every enum value.

**Client unit tests** (`Tests/Chaos.Client.Tests/`):
- `StageLightAnimator`:
  - sweep endpoints and easing;
  - circle radius;
  - follow holding at the stage edge and falling back;
  - pulse and flicker ranges;
  - flicker being the same for the same inputs;
  - color cycle;
  - phase anchoring;
  - fade blending, fade-in and fade-out.
- Spotlight mask generation: size and symmetry.
- `StageLighting` view model: selection, rate limiting, ignoring updates while dragging, and
  minimize state.

**Manual walkthrough** with a server and two clients (director and audience):
- open the window from Thulin;
- add three lights and drag them;
- try each effect and motion;
- follow an actor on and off the stage;
- dim to 30% and black out;
- toggle stage glow;
- save, load and delete scenes;
- have the audience client walk in mid-show;
- restart the server and check that scenes are kept and the live setup is reset;
- check the frame counter with 8 moving lights.

## Files (expected)

**Chaos-Server**
- New: `Chaos/Services/Theatre/StageLighting.cs`, `StageLight.cs`, `TheatreLightingScenes.cs`
- New: `Chaos.Networking/Entities/Server/StageLightingStateArgs.cs`, `StageLightingBoardArgs.cs`,
  `Entities/Client/StageLightingInteractionArgs.cs`, and a converter for each
- Changed: `Chaos.Networking.Abstractions/Definitions/Enums.cs` (opcodes);
  `Chaos.DarkAges/Definitions/Enums.cs` (light enums, next to `BeautyShopInteractionType`)
- Changed: `SuomiTheatreMapScript.cs`, `SuomiTheatreScript.cs`, `WorldServer.cs`,
  `ChaosWorldClient.cs`, `IChaosWorldClient.cs`
- New tests under `Tests/Chaos.Tests/Theatre/` and `Tests/Chaos.Tests/Networking/`

**Chaos.Client**
- New: `Chaos.Client.Rendering/SpotlightRenderer.cs`, spotlight mask generator,
  `Chaos.Client/Systems/StageLightAnimator.cs`, `Chaos.Client/ViewModel/StageLighting.cs`,
  `Chaos.Client/Controls/World/Popups/Theatre/StageLightingControl.cs` (+ stage view, color picker)
- Changed: `LightSource.cs`, `DarknessRenderer.cs`, `LightingSystem.cs`,
  `Chaos.Client.Networking/ConnectionManager.cs`, `Definitions/Delegates.cs`,
  `WorldScreen.cs` / `.Draw.cs` / `.ServerHandlers.cs` / `.Wiring.cs`
- `CLAUDE.md`: draw order step 5b and a `SpotlightRenderer` entry
- New tests under `Tests/Chaos.Client.Tests/`

**Unora**
- Changed: `Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_options.json`
- New: `.../thulin/suomitheatre_stagelighting.json`

## Risks

- **Additive color on a bright room** can wash out to white. The 0.35 factor at 100% house is a
  starting point, to be tuned in game.
- **The Theatre may have light metadata or a per-pixel light file.** If so, the house-level override
  has to go through that path as well. The plan checks map 346's light data first.
- **Opcode 128 is the first server opcode above 127.** The plan confirms that nothing treats the
  opcode as a signed byte.

## Changes made during implementation

Decisions taken while building (each reviewed; the plan's ledger recorded them):

- **Scene names** are printable ASCII only (space to `~`), 1–24 characters after trimming, on both server and
  client, because the game font and the wire encoding are ASCII. The refusal reads "Scene names are 1 to 24
  plain letters, digits or symbols."
- **Darkness flips are throttled.** The map's Darkness flag changes at most once a second, and turning it off
  waits until the house lights have been at 100% for a second. Each flip force-refreshes every player, and the
  dimmer can cross 100% many times a second. Waiting also lets clients fade the darkness up smoothly. The map's
  once-a-second update catches up any flip that was held back. The logic is in `TheatreDarknessSync`.
- **Following someone who left:** the next edit to that light makes it Still at its own spot.
- **Fades** keep a clock per fading-out light, and a new setup blends from what is on screen at that moment,
  not from the last drawn frame.
- **Window size** is 380×264; the minimized bar is 230×20. Clicking a person re-targets a light that is
  already following someone. A held drag edit is sent once its 125 ms slot passes, even if the mouse stops.
- **`StageViewGeometry`** lives in `Chaos.Client.Systems`, so the view model never depends on Controls.
