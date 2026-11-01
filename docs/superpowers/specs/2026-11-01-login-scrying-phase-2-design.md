# Login scrying view, phase 2: spells and emotes

Date: 2026-11-01
Repos: Chaos.Client (client) and its Chaos-Server submodule (server)
Builds on: `2026-10-31-login-scrying-view-design.md` (phase 1, merged to local main)

## Goal

The start-screen oval already shows a live view of one player and the area around them.
Phase 2 adds what happens in that area: spell effects, projectiles, body animations (attacks, casting poses) and emotes, including the client-side custom emotes.

## Decisions made with the user

| Topic | Decision |
|---|---|
| Sound | None. The view plays no sound effects. |
| How events are caught | Hook the public broadcasts on the server (approach A). Never copy what the watched player's own connection receives. |
| What is shown | Only what a stranger standing next to the watched player would see. Private, ally-only and script-to-one-player effects are never shown. |

## Background facts (checked in code)

- Shared animations go through three methods:
  - `Creature.Animate(animation, sourceId)` sends a targeted animation to everyone in range who can observe the creature.
  - `Creature.AnimateBody(bodyAnimation, speed)` sends body animations and emotes to everyone in range who can see the creature.
  - `MapInstance.ShowAnimation(animation)` sends a point or targeted animation to everyone in range.
- `MapInstance.ShowAnimationToFriendly` sends only to allies. Scripts also call `Client.SendAnimation` for one player. Phase 2 does not hook either one.
- All three hooked methods run on the map's update thread, inside `MapInstance.Sync`.
- `ScryingService` runs every 100 ms on its own thread. It takes `Sync` on the watched map, copies the area within 8 tiles (`RADIUS`) into a `ScryView`, and sends a frame.
- The client's world screen keeps effects in `WorldState.ActiveEffects` and `WorldState.ActiveProjectiles`. The scrying view must not touch `WorldState`.
- `WorldScreen.HandleBodyAnimation` picks between four kinds of playback: a custom emote, a creature-sprite attack, an aisling body animation, or an emote overlay.
- `WorldScreen.HandleAnimation` picks between a projectile (target animation 10000 to 11999), a ground effect, a target effect and a source effect.

## Part 1: the server

### The tap

A new static class, `ScryEventTap`, in `Chaos/Services/Scrying`. It is static because `Creature` and `MapInstance` are models with no dependency injection.

- `Watched` holds the `MapInstance` being watched, or null. `ScryingService` sets it on every update. It sets it to null when there are no watchers or no pick.
- `IsWatching(MapInstance map)` is a reference comparison against `Watched`. This is the only cost to the game when the map is not watched.
- `Record(ScryEvent e)` adds to a `ConcurrentQueue`. The queue holds at most 256 events. Extra events are dropped.
- `Drain()` empties the queue and returns its contents. Only `ScryingService` calls it.

### Hooks

Each of the three methods adds one guarded call at the end:

```csharp
if (ScryEventTap.IsWatching(MapInstance))
    ScryEventTap.Record(...);
```

- `Creature.Animate`: records the targeted animation it built, with the creature's id and position.
- `Creature.AnimateBody`: records the source id, body animation and speed. It records before the per-viewer ignore-list check, because the view has no viewer.
  The early return for `BodyAnimation.None` stays first.
- `MapInstance.ShowAnimation`: records the animation after the point-over-id rule is applied.

The hooks record facts only. They never read `ScryingService` state and never send packets.

### Event record

`ScryEvent` is a plain record:

- kind: `Animation` or `Body`;
- the `MapInstance` it happened on;
- for `Animation`: target animation, source animation, speed, source id, target id, target point, background flag;
- for `Body`: source id, body animation, speed.

### Filtering

On each update, after it captures the view, the service drains the tap and keeps an event only when all of these hold:

1. Its map is the map captured in this view. Events from an earlier pick or an earlier map are dropped.
2. Any entity it names is in this view's entity list. This one rule covers range, hidden players, GM-hidden players, opted-out bystanders and entrance effects, because the capture already left those out.
   - `Body`: the source id must be in the view.
   - `Animation` with a target id: the target must be in the view. If the source is not in the view, the source animation is cleared, and the event is kept for its target part.
   - Projectile (target animation 10000 to 11999): both the source and the target must be in the view.
3. A point animation's point must be within `RADIUS` of the watched player.

The service keeps at most 32 events per frame, oldest first. Events left over in the same update are dropped, not carried to the next frame.

If nobody is eligible, or the update sends a none frame, the drained events are thrown away.

### Frames and packets

- A new frame kind, `ScryFrameKind.Events = 3`, on the existing `ScryFrame` opcode. No new opcode.
- An events frame holds the target id, then a count, then each event:
  - one kind byte (`0` animation, `1` body);
  - animation: target animation, source animation, speed, source id (0 for none), target id (0 for none), a point flag with x and y, and the background flag;
  - body: source id, body animation byte, speed.
- The service sends the events frame right after the position or full frame in the same update. It sends nothing when no event survives.
- Clients older than 819 never see an events frame, because they never start watching.
- The phase 1 converter does not skip unknown kinds. It would read kind 3 as a position frame and get garbage. Phase 1 is not deployed, so no 819 client exists in the wild. Phase 2 ships on the same 819 build, and the version stays 819.
- The client converter now treats any kind it does not know as `None`-like and skips the rest of the packet. This keeps later frame kinds safe.

## Part 2: the client

### Shared playback code

To avoid copying the world screen's logic, move the pure parts into shared static helpers. The world screen then calls them with its own lists.

- `EffectSpawner.CreateEffect(list, effectId, speed, targetEntityId, tileX, tileY, isBackground)` holds today's `CreateEffect` body, including the per-entity cap and the eviction rule.
- `EffectSpawner.CreateProjectile(list, meffectId, source, target, mapHeight)` holds today's `CreateProjectile` body. It takes the two entities, not their ids.
- `BodyAnimationPlayer.Start(entity, args, hideOwn)` holds the four-way choice from `HandleBodyAnimation`, including the "already playing" guard.

The world screen behaves exactly as before. Its handlers become thin calls into these helpers.

### The scrying view

- `ScryScene` gets its own `Effects` list and `Projectiles` list.
- `ScryScene.ApplyEvents(frame)` ignores the frame when it has no view or when the frame's target id is not the current target. Otherwise, for each event:
  - `Body`: look up the entity and call `BodyAnimationPlayer.Start` with `hideOwn` false.
  - `Animation`: split it the same way `HandleAnimation` does: projectile, ground effect, target effect, source effect. Then call `EffectSpawner`.
- `ScryScene.Update` advances effects and projectiles with the same timing code the world uses. It removes the finished ones.
- `ScryScene` clears both lists when the target changes, when the map changes and in `Clear`.
- `ScryingViewControl.DrawScene` draws the effects:
  - background effects before entities;
  - ground effects at their depth, among the entities;
  - entity effects and projectiles after the entities.
  It reuses `Game.EffectRenderer`. It skips the world screen's arena rules (tumble and glass hiding), because those depend on the logged-in player.
- No sound is played. The view never calls `SoundSystem`.
- The cross-fade on a new pick covers the effect list being cleared.

### Connection

`ConnectionManager` already raises the scrying frame event. The lobby screen routes frames with kind `Events` to `ScryScene.ApplyEvents`. All other kinds go to `ScryScene.Apply` as they do now.

## Part 3: testing and rollout

### Tests

- **Server unit tests:**
  - the tap records nothing when the map is not watched;
  - the tap drops events past 256;
  - filtering drops events from another map, events whose entity is not in the view, out-of-range point events, and projectiles with a missing source;
  - filtering clears the source part of a targeted animation when only the source is missing;
  - the 32-event cap keeps the oldest events;
  - a none update sends no events frame;
  - a body animation from an opted-out bystander never reaches a frame.
- **Packet round-trip test** for the events frame with both event kinds, a point animation and a target animation.
- **Client unit tests:**
  - `ApplyEvents` ignores frames for a different target;
  - a body event starts a body animation on the matching entity;
  - an emote byte from `CustomEmoteRegistry` starts the custom emote;
  - target, ground and projectile events land in the scene's own lists and never in `WorldState`;
  - a target change clears the effects.
- **Regression:** the world screen's existing animation tests still pass after the helpers are moved out.
- **Playtest** with a local server and two clients. One client is logged in. The other sits on the start screen. On the logged-in client:
  - cast a spell on a monster, and cast one on the ground;
  - throw a projectile;
  - assail;
  - use a normal emote and a custom drawn emote.
  Each one must show in the oval with no sound. Then go hidden and emote. Nothing must show.

### Rollout

1. This changes the network protocol, so the full test suite runs before merge, and the change gets a Codex cross-review.
2. Deploy order is the same as phase 1: the 819 client goes on the launcher before the server restarts. Phase 2 must merge before that deploy, because both phases ship together on 819. If 819 ships first without phase 2, raise the client version to 820 for phase 2.

## Out of scope

- Sound, music and chat.
- Ally-only animations and animations a script sends to one player.
- Ground-item and door animations.
- Effects on entities outside the 8-tile area.
