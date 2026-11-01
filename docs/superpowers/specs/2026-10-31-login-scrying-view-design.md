# Login scrying view: design

Date: 2026-10-31
Repos: Chaos.Client (client) and its Chaos-Server submodule (server)

## Goal

The start screen has an oval window in its frame art. Today the 20-frame `_nslogo.spf` animation (296x266) plays inside it.
This feature shows a live view of the game world in that oval instead, like Adventure Land's login screen.
The view follows one random, active, non-GM player who hasn't opted out, and switches every 20 seconds.
Anyone who opens the client sees it before logging in.

## Decisions made with the user

| Topic | Decision |
|---|---|
| What the oval shows | A live view of a real map around one player, with players and monsters moving |
| Look | The existing oval only. No crystal ball, no stand, no glow. |
| How live | Truly live: about 10 updates a second, smooth walking |
| Who is shown | A random active player. GMs are never shown. |
| Opt-out | A saved setting "Show me on the login screen", on by default |
| Chat | Never shown. No speech bubbles, no chat text. |
| Caption | None. No player name, no map name. |
| Switch time | 20 seconds per player, or at once when the player stops being eligible |
| Approach | The server streams snapshots of the area around the player over the login connection. It does not add a hidden character to the map, and it does not change the core visibility code. |

## Background facts (checked in code)

- On the start screen the client is connected to the login server (`ConnectionState.Login`). The connection stays open and idle until login. The login server kicks idle clients after 30 minutes (`LoginServer.cs`, `IdleClientTimeout`). That timeout counts only packets the client sends, so streaming to the client does not reset it.
- The login server and world server run in one process and one DI container. The login server can take `IClientRegistry<IChaosWorldClient>` to list online players.
- `MapInstance.Sync` guards map state. The map tick holds it for the whole update. A reader on another thread must wait on it and copy what it needs.
- Kart racing's `KartSnapshot` (15 Hz) is the model for a streamed snapshot packet. That covers the opcode in `Enums.cs`, an Args record, a `PacketConverterBase<T>` converter, `Send` on the client interface, and a handler in `ConnectionManager`. Converters are found by reflection.
- No custom packets exist on the login server yet. `IChaosLoginClient` gets new `Send` methods.
- `Mapper.Map<DisplayAislingArgs>(aisling)` builds a player's appearance without a viewer. `CreatureInfo` and `GroundItemInfo` do the same for monsters and items.
- Player toggles follow `DamageNumbersMyOutputOnly`: `UserOption` enum, `UserOptions` collection, `UserOptionsSchema`, `UserOptionsArgs` (trailing optional fields guarded by `EndOfSpan`), then the client `SettingDefinition` with `SettingCategory.ServerOption`.
- Nothing tracks AFK. `Trackers.LastWalk` holds the last walk time.
- `AislingRenderer`, `CreatureRenderer`, `ItemRenderer` and `EffectRenderer` are created by `ChaosGame` before any screen. `MapRenderer` is not shared, so the view creates its own.
- Map files load from `{DataPath}/maps/lod{id}.map`. The login server cannot send map data.

## Part 1: the server

### Eligibility

A player can be picked only when all of these hold:

1. Online in the world server.
2. Not an admin, not GM-hidden (`IsGmHidden`, `VisibilityType.GmHidden`), and not in god mode (`IsGodModeEnabled`).
3. Not hidden (`IsHidden` covers `hide`, `gm hide` and `true hide`) and not `HiddenForEntrance`.
4. Their `ShowOnLoginScreen` option is on. It defaults to on.
5. `Trackers.LastWalk` is within the last 5 minutes.
6. Their map is not a streamed map. A streamed map has no file on disk for the client to load.

### Rotation

- There is one shared pick for all watchers.
- Each 20 seconds the service picks a random eligible player who is not the current one. If only the current player is eligible, it keeps them.
- Each update, the service re-checks the current pick against the eligibility rules. If the pick fails, it chooses a new one at once.
- If nobody is eligible, watchers get a "none" frame. The client plays the logo animation.

### The scrying service

`ScryingService` is a singleton hosted service in the server. It runs a loop about every 100 ms.

1. If there are no watchers, it does nothing.
2. It reads the target's `MapInstance` once into a local, waits on `Sync`, and copies everything within 8 tiles of the target into plain records. That includes players, monsters, NPCs and ground items. Then it releases `Sync`.
3. It applies the same filters an ordinary, non-admin player viewing the area would get. Hidden and GM-hidden players and players in an entrance effect are left out.
4. It builds one frame and sends the same bytes to every watcher.

### Frames

- **Full frame**, about once a second and whenever the pick changes. It holds:
  - map id, width, height and checksum;
  - target entity id and position;
  - every entity's id, kind, position, direction and appearance.
- **Position frame** for the other updates. It holds the target position, then each entity's id, position and direction. An entity that is new since the last full frame comes with its appearance. An entity that left is listed by id.
- **None frame**: nobody is eligible.
- Frames never carry chat or names. The appearance record has its name field blanked, and the client draws no name tags in the view.
- A new watcher waits for the next full frame, which is at most one second away.

### Packets

| Direction | Opcode | Args |
|---|---|---|
| Client to server | `ScryStart` | none |
| Client to server | `ScryStop` | none |
| Server to client | `ScryFrame` | frame kind plus the fields above |

- These packets are accepted only on the login server. The server decides who to show, and the client sends no choice.
- A watcher is removed on `ScryStop`, on disconnect and on redirect to the world server.
- The cap is 200 watchers. Watchers past the cap get no frames and keep the logo.

### Phase 2: spells and emotes

After Part 1 and Part 2 work, the service also forwards events near the target: spell animations, body animations (including emotes) and sounds. It hooks the existing broadcasts on the target's map, filters them to the 8-tile area, and sends them as a new frame kind. Phase 2 gets its own tests and playtest.

## Part 2: the client

### Scrying view control

- `ScryingViewControl` sits at the `LOGO` control's position and size (296x266) in `LobbyLoginControl`. The existing `LogoImage` stays as the fallback.
- It draws the map and entities into a 296x266 render target. It cuts that to the oval, using an ellipse mask that matches the logo frames, and draws it in place of the logo. The frame art does not change.
- The logo animation plays:
  - before the first full frame;
  - on a "none" frame;
  - when the map file is missing, or its size or checksum doesn't match;
  - when no frame has arrived for 3 seconds.

### Drawing

- The view loads `lod{id}.map` from the install `maps` folder, using the same loading and checks as `WorldScreen.Map.cs`.
- It has its own `MapRenderer`, disposed when the start screen goes away. It uses the shared aisling, creature and item renderers from `ChaosGame`.
- Lighting is normal daylight.
- It does not touch `WorldState`. The view keeps its own small store of entities, keyed by id.

### Smooth motion

- When an entity moves one tile between frames, it plays its normal walk animation over the normal walk time.
- When it moves further, for example after a teleport or a map change, it jumps.
- The camera glides to keep the target in the middle.
- A new pick cross-fades over about 0.5 seconds.

### Start and stop

- `LobbyLoginScreen` sends `ScryStart` when the start screen is shown in the `Login` state.
- It sends `ScryStop` when the player goes to the login or create-character dialog, or leaves the screen.
- `ConnectionManager` gets a `ScryFrame` handler and an event. It also gets `SendScryStart` and `SendScryStop`.

### Opt-out setting

- The new `UserOption.ShowOnLoginScreen` defaults to on. It is saved in `UserOptionsSchema` and sent as a trailing optional field in `UserOptionsArgs`, so older data still reads.
- The client gets a new `SettingDefinition` entry, "Show me on the login screen", in the Settings window, under `SettingCategory.ServerOption`.

## Part 3: testing and rollout

### Tests

- **Server unit tests:**
  - eligibility: GMs, GM-hidden, god mode, hidden, entrance-hidden, opted-out, idle and streamed-map players are all skipped;
  - rotation: the pick changes after 20 seconds, the same player is never picked twice in a row when another is eligible, the pick switches at once when it stops being eligible, and a "none" frame is sent when nobody is eligible;
  - frames: a full frame comes about once a second, hidden players never appear, and names are blank;
  - the cap holds at 200 watchers.
- **Packet round-trip tests** for `ScryStart`, `ScryStop` and each `ScryFrame` kind. A `UserOptionsArgs` test covers data without the new field.
- **Playtest** with a local server and two clients. One client is logged in and walking. The other sits on the start screen and shows it live. Then check that each of these switches the view or falls back to the logo:
  - turning on the opt-out;
  - going GM-hidden;
  - standing still for 5 minutes;
  - logging out.

### Rollout

1. Deploy the server first. Old clients never send `ScryStart`, so nothing changes for them.
2. Ship the client after the server is live. An old server would not know `ScryStart`.
3. Bump the client version.
4. This changes the network protocol, so the full test suite runs before merge, and the change gets a cross-review.

## Out of scope

- Captions, names and chat in the view.
- Lighting and weather in the view.
- Sending map data to clients that lack a map file.
- Choosing which player to watch, or controlling the view.
