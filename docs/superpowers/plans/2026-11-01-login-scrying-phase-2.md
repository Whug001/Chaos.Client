# Login Scrying Phase 2 (Spells and Emotes) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The start-screen scrying view shows spell effects, projectiles, body animations and emotes that happen near the watched player, with no sound.

**Architecture:** Three public broadcast methods on the server report events to a static `ScryEventTap` when their map is the watched one. `ScryingService` drains the tap each 100 ms update, keeps only events whose entities are in the captured view, and sends them as a new `ScryFrameKind.Events` frame. The client moves the world screen's effect and body-animation start logic into shared helpers, and `ScryScene` uses them with its own effect lists.

**Tech Stack:** C# 14 / .NET 10, MonoGame, TUnit (client tests), the server's own test project (`Tests/Chaos.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-01-login-scrying-phase-2-design.md`

**Repos:** the client is `C:\Users\Michael\Documents\GitHub\Chaos.Client`. The server is its submodule `Chaos-Server` (a separate git repo). Server paths below start with `Chaos-Server/`.

## Global Constraints

- `RADIUS` stays 8 (`ScryingService.RADIUS`).
- The tap queue holds at most 256 events. An events frame holds at most 32 events, oldest first. Extra events are dropped, never carried over.
- Projectile target animations are 10000 to 11999 inclusive (`PROJECTILE_ANIMATION_BASE` 10000, `PROJECTILE_ANIMATION_MAX_EXCLUSIVE` 12000).
- The view plays no sound. Nothing in `Controls/LobbyLogin/Scrying` calls `SoundSystem`.
- The scrying view never reads or writes `WorldState`.
- The world screen's animation behaviour must not change. Its handlers call the new helpers with `WorldState` lists.
- `CLIENT_VERSION` stays 819.
- Never change a signature on a `Chaos.*` library interface. Adding a frame kind, a record and converter code is allowed.
- Do not touch `MapInstance.ShowAnimationToFriendly` or any script that calls `Client.SendAnimation` directly.

**User decisions (already made):**
- "1" — no sound in the view.
- "A" — hook the public broadcasts; never copy what the watched player's connection receives.
- "get this done, ill be afk" — run the plan without further approval gates; do not push or deploy.

## Review Focus

1. A body animation or spell from a bystander who opted out, or who is hidden, must never reach a frame. Covered by the filter test in Task 2.
2. An events frame that arrives for an old target, after the pick switched, must not start effects on the new view. Covered by the `ApplyEvents` target test in Task 4.
3. An events frame must never go through `ScryScene.Apply`. That would treat it as a full frame and wipe every entity. Covered by the routing test in Task 4.
4. A projectile whose source walked out of the view must be dropped, not drawn from (0,0). Covered by the filter test in Task 2.
5. The game screen still plays spells, projectiles and custom emotes exactly as before after the move into helpers. Covered by the regression run in Task 3.

---

### Task 1: Events frame in the packet

**Goal:** `ScryFrameKind.Events` and its event records round-trip through `ScryFrameConverter`, and the client converter skips unknown kinds safely.

**Files:**
- Modify: `Chaos-Server/Chaos.Networking/Entities/Server/ScryFrameArgs.cs`
- Modify: `Chaos-Server/Chaos.Networking/Converters/Server/ScryFrameConverter.cs`
- Test: `Chaos-Server/Tests/Chaos.Tests/Scrying/ScryPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `ScryFrameKind.Events = 3` exists.
- [ ] An events frame with one animation event (target id + source id), one point animation event and one body event round-trips with every field equal.
- [ ] A frame whose kind byte is 99 deserializes to `Kind == 99` with empty lists and does not throw.
- [ ] Existing None, Full and Positions round-trip tests still pass.

**Verify:** `dotnet test Chaos-Server/Tests/Chaos.Tests --filter "FullyQualifiedName~Scrying"` → all pass

**Steps:**

- [ ] **Step 1: Add the types** to `ScryFrameArgs.cs`:

```csharp
public enum ScryFrameKind : byte
{
    None = 0,
    Full = 1,
    Positions = 2,
    Events = 3
}

public enum ScryEventKind : byte
{
    Animation = 0,
    Body = 1
}

/// <summary>A spell effect or body animation seen in the scrying view. Ids of 0 mean none.</summary>
public sealed record ScryEventEntry
{
    public ScryEventKind Kind { get; set; }
    public ushort TargetAnimation { get; set; }
    public ushort SourceAnimation { get; set; }
    public ushort Speed { get; set; }
    public uint SourceId { get; set; }
    public uint TargetId { get; set; }
    public byte? PointX { get; set; }
    public byte? PointY { get; set; }
    public bool IsBackground { get; set; }
    public BodyAnimation BodyAnimation { get; set; }
}
```

Add `public List<ScryEventEntry> Events { get; set; } = [];` to `ScryFrameArgs`.

- [ ] **Step 2: Write the failing tests** in `ScryPacketConverterTests.cs`, following the file's existing round-trip helper. One test builds `Kind = Events, TargetId = 7` with three entries (animation with `TargetId 5, SourceId 7, TargetAnimation 120, SourceAnimation 3, Speed 100`; animation with `PointX 10, PointY 12, TargetAnimation 55, IsBackground true`; body with `SourceId 7, BodyAnimation = BodyAnimation.Assail, Speed 25`) and asserts each field after round trip. A second test serializes only the byte `99` and asserts `Kind == (ScryFrameKind)99`, `Entities` and `Events` empty.

- [ ] **Step 3: Run tests, expect FAIL** (kind 3 is read as a positions frame).

- [ ] **Step 4: Implement** in `ScryFrameConverter`:
  - `Deserialize`: after reading the kind, `if (args.Kind is not (ScryFrameKind.Full or ScryFrameKind.Positions or ScryFrameKind.Events)) return args;` (covers None and unknown kinds). For `Events`: read `TargetId` (uint32), then a `ushort` count, then per entry: kind byte; for `Animation` read target animation, source animation, speed (uint16 each), source id, target id (uint32 each), a boolean point flag then x and y bytes when set, and the background boolean; for `Body` read source id (uint32), body animation byte, speed (uint16). Return.
  - `Serialize`: write the same layout for `Events` and return before the map fields.

- [ ] **Step 5: Run tests, expect PASS.**

```json:metadata
{"files": ["Chaos-Server/Chaos.Networking/Entities/Server/ScryFrameArgs.cs", "Chaos-Server/Chaos.Networking/Converters/Server/ScryFrameConverter.cs", "Chaos-Server/Tests/Chaos.Tests/Scrying/ScryPacketConverterTests.cs"], "verifyCommand": "dotnet test Chaos-Server/Tests/Chaos.Tests --filter FullyQualifiedName~Scrying", "acceptanceCriteria": ["ScryFrameKind.Events = 3", "events frame round-trips all fields", "unknown kind 99 returns empty args without throwing", "existing scry converter tests pass"], "modelTier": "mechanical"}
```

---

### Task 2: Server tap, hooks, filter and sending

**Goal:** Animations and body animations near the watched player reach watchers as events frames, filtered to entities in the captured view.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryEvent.cs`
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryEventTap.cs`
- Create: `Chaos-Server/Chaos/Services/Scrying/ScryEventFilter.cs`
- Modify: `Chaos-Server/Chaos/Models/World/Abstractions/Creature.cs` (`Animate`, `AnimateBody`)
- Modify: `Chaos-Server/Chaos/Collections/MapInstance.cs` (`ShowAnimation`)
- Modify: `Chaos-Server/Chaos/Services/Scrying/ScryingService.cs`
- Test: `Chaos-Server/Tests/Chaos.Tests/Scrying/ScryEventFilterTests.cs`, `Chaos-Server/Tests/Chaos.Tests/Scrying/ScryEventTapTests.cs`

**Acceptance Criteria:**
- [ ] `ScryEventTap.IsWatching(map)` is false for every map when `Watched` is null; `Record` past 256 queued events drops the extra ones.
- [ ] The filter drops: events from another map; body events whose source is not in the view; targeted animations whose target is not in the view; projectiles (target animation 10000-11999) missing source or target; point events farther than 8 tiles from the watched player.
- [ ] The filter clears `SourceId` and `SourceAnimation` of a non-projectile targeted animation when only the source is missing.
- [ ] The filter returns at most 32 events, the oldest ones.
- [ ] `ScryingService` sets `ScryEventTap.Watched` each update (null when no watchers or no view), sends an events frame after the view frame only when at least one event survives, and drains-and-discards on a none update.
- [ ] Each hook costs one reference comparison when its map is not watched.

**Verify:** `dotnet test Chaos-Server/Tests/Chaos.Tests --filter "FullyQualifiedName~Scrying"` → all pass

**Steps:**

- [ ] **Step 1: Event record and tap.**

```csharp
// ScryEvent.cs
namespace Chaos.Services.Scrying;

public sealed record ScryEvent(MapInstance Map, ScryEventEntry Entry);
```

```csharp
// ScryEventTap.cs
namespace Chaos.Services.Scrying;

/// <summary>
///     Static because Creature and MapInstance are models with no dependency injection. The broadcast methods record
///     here only while their map is the one the login scrying view is watching.
/// </summary>
public static class ScryEventTap
{
    public const int MAX_QUEUED = 256;

    private static readonly ConcurrentQueue<ScryEvent> Queue = new();
    private static int Count;

    public static MapInstance? Watched { get; set; }

    public static bool IsWatching(MapInstance? map) => (map is not null) && ReferenceEquals(Watched, map);

    public static void Record(MapInstance map, ScryEventEntry entry)
    {
        if (Interlocked.Increment(ref Count) > MAX_QUEUED)
        {
            Interlocked.Decrement(ref Count);

            return;
        }

        Queue.Enqueue(new ScryEvent(map, entry));
    }

    public static List<ScryEvent> Drain()
    {
        var drained = new List<ScryEvent>();

        while (Queue.TryDequeue(out var e))
        {
            Interlocked.Decrement(ref Count);
            drained.Add(e);
        }

        return drained;
    }
}
```

- [ ] **Step 2: Hooks.** At the end of each method, guarded by `ScryEventTap.IsWatching(MapInstance)` (in `MapInstance.ShowAnimation` use `this`):
  - `Creature.Animate`: record `Kind = Animation` from `targetedAnimation` (`TargetAnimation`, `SourceAnimation`, `AnimationSpeed`, `SourceId ?? 0`, `TargetId ?? 0`, background flag if the server `Animation` has one, else false).
  - `Creature.AnimateBody`: after the `None` early return and before the loop, record `Kind = Body, SourceId = Id, BodyAnimation = bodyAnimation, Speed = speed`.
  - `MapInstance.ShowAnimation`: after the point-over-id rewrite, record the animation; when `TargetPoint` is set, fill `PointX`/`PointY` and leave `TargetId` 0.
  Read the server `Animation` class to map its field names; do not change it.

- [ ] **Step 3: Write failing filter tests.** `ScryEventFilter.Filter(IReadOnlyList<ScryEvent> events, MapInstance map, ScryView view, int targetX, int targetY)` returns `List<ScryEventEntry>`. Tests build a `ScryView` with entity ids `{1 (target at 10,10), 2 (at 12,10)}`, use one map instance for "this map" and a second for "other map" (use the same map-instance test helper the other Scrying tests use), and assert each rule from the Acceptance Criteria, one test per rule. Add `ScryEventTapTests`: `IsWatching` false when `Watched` null; 300 `Record` calls leave 256 in `Drain()`. Reset `Watched` and drain in each test's cleanup; mark tap tests not parallel (TUnit `[NotInParallel]`) because the tap is static.

- [ ] **Step 4: Implement `ScryEventFilter`** as a static class. Rules, in order per event: map reference must equal `map`; `Body` needs `SourceId` in the view; an animation with `PointX` set needs Chebyshev or the same `WithinRange` distance the server uses (≤ `ScryingService.RADIUS`) from the target position; projectile (`TargetAnimation` in [10000, 12000)) needs both ids in the view; other targeted animation needs `TargetId` in the view, and when `SourceId` is not in the view copy the entry with `SourceId = 0, SourceAnimation = 0`. Stop after 32 kept entries.

- [ ] **Step 5: Wire `ScryingService.TickAsync`:**
  - When `!watchers.Any`: set `ScryEventTap.Watched = null`, drain and discard, return.
  - After the pick: set `ScryEventTap.Watched = target?.MapInstance`.
  - In `CaptureAsync`, return the captured `MapInstance` alongside the view (or store it in a field) so the filter compares against the exact instance captured.
  - When the view is null: drain and discard before sending none.
  - Otherwise send the view frame, then `var kept = ScryEventFilter.Filter(ScryEventTap.Drain(), map, view, target.X, target.Y)`; if `kept.Count > 0`, `watchers.SendToAll(new ScryFrameArgs { Kind = ScryFrameKind.Events, TargetId = view.TargetId, Events = kept })`.
  Use whatever position fields `ScryView` already carries for the target; if it has none, pass the target's position read inside `CaptureAsync` under `Sync`.

- [ ] **Step 6: Run the Scrying tests, expect PASS.** Then run the full server suite once, because this changes the network protocol: `dotnet test Chaos-Server/Tests/Chaos.Tests` and report the summary line.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/Scrying/ScryEvent.cs", "Chaos-Server/Chaos/Services/Scrying/ScryEventTap.cs", "Chaos-Server/Chaos/Services/Scrying/ScryEventFilter.cs", "Chaos-Server/Chaos/Models/World/Abstractions/Creature.cs", "Chaos-Server/Chaos/Collections/MapInstance.cs", "Chaos-Server/Chaos/Services/Scrying/ScryingService.cs", "Chaos-Server/Tests/Chaos.Tests/Scrying/ScryEventFilterTests.cs", "Chaos-Server/Tests/Chaos.Tests/Scrying/ScryEventTapTests.cs"], "verifyCommand": "dotnet test Chaos-Server/Tests/Chaos.Tests --filter FullyQualifiedName~Scrying", "acceptanceCriteria": ["tap watch check and 256 cap", "filter drops other-map, out-of-view, missing-projectile-end, out-of-range point events", "filter clears source part when only source missing", "at most 32 oldest events", "service sets Watched, sends events frame only when non-empty, discards on none", "full server suite passes"], "modelTier": "standard"}
```

---

### Task 3: Shared client playback helpers

**Goal:** The world screen's effect, projectile and body-animation start logic lives in shared static helpers that take their lists and entities as parameters, with no change in game behaviour.

**Files:**
- Create: `Chaos.Client/Systems/EffectSpawner.cs`
- Create: `Chaos.Client/Systems/BodyAnimationPlayer.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.ServerHandlers.cs` (`HandleBodyAnimation`, `CreateProjectile`, `CreateEffect`)
- Modify: `Chaos.Client/Collections/WorldState.cs` (effect/projectile advance loop near line 1248, only if moving it out)
- Test: `Tests/Chaos.Client.Tests/EffectSpawnerTests.cs`

**Acceptance Criteria:**
- [ ] `EffectSpawner.CreateEffect(List<Animation> effects, int effectId, ushort speed, uint? targetEntityId, int? tileX, int? tileY, bool isBackground)` keeps today's restart-same-effect, per-entity cap (`ClientSettings.MaxEffectAnimationsPerEntity`) and oldest-first eviction rules.
- [ ] `EffectSpawner.CreateProjectile(List<Projectile> projectiles, int meffectId, WorldEntity source, WorldEntity target, int mapHeight)` builds the same `Projectile` as today.
- [ ] `EffectSpawner.Advance(List<Animation> effects, List<Projectile> projectiles, float elapsedMs, Func<uint, WorldEntity?> lookup)` holds the per-frame advance and removal logic now run for `WorldState.ActiveEffects`/`ActiveProjectiles`; `WorldState` calls it with its own lists.
- [ ] `BodyAnimationPlayer.Start(WorldEntity entity, BodyAnimationArgs args, bool hidden)` holds the four-way choice from `HandleBodyAnimation`, including the already-playing guard; `HandleBodyAnimation` computes `hidden` exactly as before and calls it.
- [ ] Unit tests cover the cap, the restart rule and eviction order.
- [ ] Every existing client test still passes.

**Verify:** `dotnet build Chaos.Client.slnx` then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build` → all pass

**Steps:**

- [ ] **Step 1: Write failing tests** in `EffectSpawnerTests.cs`. Use an effect id the test can resolve, or give `CreateEffect` an overload that takes the already-resolved `(frameCount, fileIntervalMs, isEfa, blendMode)` tuple so tests don't need `Game.EffectRenderer`; the public overload resolves via `Game.EffectRenderer.GetEffectInfo` and calls it. Tests: same effect on the same entity twice leaves one entry; with cap 2, a third effect on one entity evicts the oldest; cap 0 adds nothing; a tile effect is never capped.
- [ ] **Step 2: Run, expect FAIL** (type missing).
- [ ] **Step 3: Move the code.** Cut the bodies of `CreateEffect` and `CreateProjectile` into `EffectSpawner`, replacing `WorldState.ActiveEffects`/`ActiveProjectiles`/`WorldState.GetEntity` with parameters. The world screen's private methods become one-line calls. Move the body-animation branch logic (everything after the entity lookup in `HandleBodyAnimation`) into `BodyAnimationPlayer.Start`, using `Game.CreatureRenderer`/`Game.AislingRenderer` as it does today. Move the advance loop from `WorldState` into `EffectSpawner.Advance` only if it does not depend on other `WorldState` members; otherwise leave it and add a parallel `Advance` that the scene uses, with a comment-free shared private core.
- [ ] **Step 4: Build and run every client test, expect PASS.**

```json:metadata
{"files": ["Chaos.Client/Systems/EffectSpawner.cs", "Chaos.Client/Systems/BodyAnimationPlayer.cs", "Chaos.Client/Screens/WorldScreen.ServerHandlers.cs", "Chaos.Client/Collections/WorldState.cs", "Tests/Chaos.Client.Tests/EffectSpawnerTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["CreateEffect keeps restart, cap and eviction rules", "CreateProjectile builds same projectile", "Advance shared by WorldState", "BodyAnimationPlayer.Start holds the four-way choice", "unit tests for cap/restart/eviction", "all client tests pass"], "modelTier": "standard"}
```

---

### Task 4: Events in the scrying view

**Goal:** The start-screen oval plays received spell effects, projectiles, body animations and emotes, from its own lists, with no sound.

**Files:**
- Modify: `Chaos.Client/Controls/LobbyLogin/Scrying/ScryScene.cs`
- Modify: `Chaos.Client/Controls/LobbyLogin/Scrying/ScryingViewControl.cs` (`DrawScene`)
- Modify: `Chaos.Client/Screens/LobbyLoginScreen.cs` (`OnScryFrame`)
- Test: `Tests/Chaos.Client.Tests/ScrySceneTests.cs`

**Acceptance Criteria:**
- [ ] `OnScryFrame` sends `Events` frames to `Scene.ApplyEvents` and every other known kind to `Scene.Apply`; unknown kinds go nowhere.
- [ ] `ApplyEvents` does nothing when `HasView` is false or `frame.TargetId != TargetId`.
- [ ] A body event calls `BodyAnimationPlayer.Start(entity, args, hidden: false)` on the matching scene entity; a custom-emote byte starts the custom emote.
- [ ] Animation events split like `WorldScreen.HandleAnimation`: projectile, ground (point), target and source effects, into `ScryScene.Effects`/`Projectiles`; `WorldState.ActiveEffects` and `ActiveProjectiles` stay empty.
- [ ] `Update` advances and removes finished effects; a target change, a map change and `Clear` empty both lists.
- [ ] `DrawScene` draws background effects before entities, ground effects at their tile depth among entities, then entity effects and projectiles, using `Game.EffectRenderer`, and skips the arena tumble/glass rules.
- [ ] No `SoundSystem` call anywhere under `Controls/LobbyLogin/Scrying`.

**Verify:** `dotnet build Chaos.Client.slnx` then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build --treenode-filter "/*/*/ScrySceneTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write failing tests** in `ScrySceneTests.cs` (follow its existing frame builders): events for another target are ignored; events before any full frame are ignored; a target-effect event adds one entry to `scene.Effects` and none to `WorldState.ActiveEffects`; a point event adds a tile effect; a projectile event with both entities present adds to `scene.Projectiles`; a new target id in a full frame empties `scene.Effects`; a body event for a known emote byte sets the entity's emote state (use an emote that does not need renderer data, or the custom-emote registry). Effect-info lookups that need `Game` go through the Task 3 resolved-tuple overload or are skipped by giving the scene an injectable `Func<int, (int, int, bool, EffectBlendMode)?>` effect-info source, defaulting to `Game.EffectRenderer.GetEffectInfo`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement `ScryScene`:** add `public List<Animation> Effects { get; } = [];` and `public List<Projectile> Projectiles { get; } = [];`; add `ApplyEvents(ScryFrameArgs frame)`; clear both lists where `MapChanged`/`TargetChanged` fire and in `Clear`; call `EffectSpawner.Advance(Effects, Projectiles, (float)elapsedMs, id => EntitiesById.GetValueOrDefault(id))` in `Update`. Map each `ScryEventEntry` back to the world rules: zero ids mean none; `PointX`/`PointY` set means a ground effect; projectile when both ids are non-zero and `TargetAnimation` is in [10000, 12000), with `meffectId = TargetAnimation - 10000` and the source effect also played when `SourceAnimation > 0`.
- [ ] **Step 4: Route frames** in `LobbyLoginScreen.OnScryFrame`:

```csharp
private void OnScryFrame(ScryFrameArgs args)
{
    switch (args.Kind)
    {
        case ScryFrameKind.Events:
            ScryView?.Scene.ApplyEvents(args);

            break;
        case ScryFrameKind.None or ScryFrameKind.Full or ScryFrameKind.Positions:
            ScryView?.Scene.Apply(args);

            break;
    }
}
```

- [ ] **Step 5: Draw** in `ScryingViewControl.DrawScene`, copying the matching parts of `WorldScreen.Draw.cs` (`DrawBackgroundEffects`, `DrawGroundEffectsAtDepth`, the entity-effect pass and the projectile pass) but reading `Scene.Effects`/`Scene.Projectiles` and the scene's entities, without the tumble/glass checks.
- [ ] **Step 6: Build and run the client tests, expect PASS.**

```json:metadata
{"files": ["Chaos.Client/Controls/LobbyLogin/Scrying/ScryScene.cs", "Chaos.Client/Controls/LobbyLogin/Scrying/ScryingViewControl.cs", "Chaos.Client/Screens/LobbyLoginScreen.cs", "Tests/Chaos.Client.Tests/ScrySceneTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["Events frames routed to ApplyEvents only", "ApplyEvents ignores wrong target and no view", "body events start body animation or custom emote", "animation events land in scene lists, never WorldState", "lists advance and clear on target/map change and Clear", "DrawScene draws effects in the right passes", "no SoundSystem calls in Scrying folder"], "modelTier": "standard"}
```

---

### Task 5: Commit the full implementation

**Goal:** One server commit on `master` and one client commit on `main` that carries every change, the submodule pointer and these plan files.

**Files:** none new.

**Acceptance Criteria:**
- [ ] The server repo has one new commit on `master` with every server file from Tasks 1 and 2.
- [ ] The client repo has one new commit on `main` with every client file from Tasks 3 and 4, the new `Chaos-Server` pointer, this plan and its `.tasks.json` (`git add -f` for `docs/superpowers`).
- [ ] Only this feature's files are staged (shared checkouts: stage by explicit path).
- [ ] Nothing is pushed or deployed.

**Verify:** `git -C Chaos-Server log -1 --stat; git log -1 --stat` → the two commits with the expected files

**Steps:**

- [ ] **Step 1:** Squash-merge the server feature branch into `master` in the `Chaos-Server` checkout, commit with a message file in the session scratchpad.
- [ ] **Step 2:** Squash-merge the client feature branch into `main`, point `Chaos-Server` at the new server commit, stage by explicit path, commit.
- [ ] **Step 3:** Run `git log -1 --stat` in both repos and check the file lists.

```json:metadata
{"files": [], "verifyCommand": "git -C Chaos-Server log -1 --stat; git log -1 --stat", "acceptanceCriteria": ["one server commit with Task 1-2 files", "one client commit with Task 3-4 files, submodule pointer, plan docs", "nothing pushed"], "modelTier": "mechanical"}
```
