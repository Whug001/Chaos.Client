# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Chaos.Client is a Dark Ages MMORPG client built in C# (.NET 10.0) using MonoGame for windowing/graphics and DALib for Dark Ages file format handling. Targets Chaos-Server. Licensed under AGPL-3.0-or-later. v0.1.0.

## Build & Run

```bash
# First time: fetch the Chaos-Server submodule (the shared protocol projects).
# Cloning the client with --recursive does this automatically.
git submodule update --init --recursive

dotnet build Chaos.Client.slnx
dotnet run --project Chaos.Client/Chaos.Client.csproj
```

The client requires a Dark Ages game-data directory (the folder containing `*.dat` archives). Point `GlobalSettings.DataPath` at that directory before running; `LobbyHost`/`LobbyPort` and `ClientVersion` also live in `GlobalSettings`.

Tests live in `Tests/Chaos.Client.Tests` (TUnit). Run with `dotnet build` then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`; `dotnet test` does not work with this SDK.

## Solution Structure

Chaos.Client.slnx (.NET 10.0, C# 14)

- Chaos.Client: MonoGame Game class, screens, UI controls, systems, entry point
- Chaos.Client.Data: asset repositories, DALib integration, archive loading, caching
- Chaos.Client.Rendering: texture conversion, sprite renderers, map rendering, text, camera
- Chaos.Client.Networking: TCP client, crypto, packet framing, connection state machine
- DALib: Dark Ages file format support

**Dependency flow:** Data <- Rendering <- Client, Networking <- Client

## Subsystem docs

| File | When |
|------|------|
| docs/ai/architecture.md | Layers, screens, controls, systems, state, input, key maps, targeting. |
| docs/ai/controls.md | Panel building from prefabs. |
| docs/ai/isometric.md | Tile math, draw order. |
| docs/ai/dalib.md | File-format types. |
| docs/ai/dependencies.md | Packages, build config. |

## Conventions

### Concurrency

- `Lock`+`EnterScope()` (e.g. `using var scope = SendLock.EnterScope();`), never `lock` (.NET 9+ primitive).

### Packet Dispatch

- Opcode-byte array-indexed dispatch, never switch-case (match Chaos-Server pattern).
- Tables sized `byte.MaxValue + 1`, indexed by opcode, wired in `IndexHandlers()`.
- New handler: `private void OnXxx(ref ServerPacket packet)` in `ConnectionManager`; `Handlers[(byte)ServerOpCode.Xxx] = OnXxx`; deserialize via `Chaos.Networking` converter; raise manager event for `WorldScreen`.

### UI Patterns

- `PrefabPanel` (prefab)/`UIPanel` (manual); build only needed controls via `CreateButton`/`CreateImage`/`CreateLabel`/`CreateTextBox`/`CreateProgressBar` (no auto-populate).
- Popups `Show()`/`Hide()` under WorldScreen Root.
- HUD `IWorldHud`: `WorldHudControl`/`LargeWorldHudControl`; tabs share center-bottom via `ShowTab(HudTab)`, one visible.
- `Controls/World/`: `Hud/`,`Hud/Panel/`,`Hud/Panel/Slots/`,`Popups/`(`Popups/Boards/`,`Popups/Dialog/`,`Popups/Exchange/`,`Popups/Options/`,`Popups/Profile/`,`Popups/WorldList/`,`Popups/Tcg/`,`Popups/Crafting/`,`Popups/Bank/`,`Popups/Market/`,`Popups/Fishing/`,`Popups/Poker/`,`Popups/Slots/`,`Popups/Wheel/`,`Popups/RingToss/`,`Popups/MountRace/`),`ViewPort/`; shared `Controls/Components/`; restyled `Controls/Custom/`; login `Controls/LobbyLogin/`; generic `Controls/Generic/`.
- Hotkeys are player-rebindable through F12, with chord dispatch and a separate armed-only Targeting scope; Escape/F11/F12/Alt+Enter stay fixed.
- HUD letters, emote chords, slot keys and hotkey help follow fixed maps and flows.
- Grids `PanelBase`->`PanelSlot` (slot-number overlays,cooldown rendering); exchange/dialog/equipment/profile panels server-driven.

### Quest & Dialog Conventions

- `Quest<TStage>` (QuestBuilder) only; no quest logic in dialog/reactor scripts.
- Quest dialogs in `Dialogs/[Region]/Quests/[QuestName]/`,never NPC folder.
- `InsertOption(optionText, dialogKey, 0)` in `OnDisplaying("npc_initial")`; never edit `*_initial`.
- Orange-bar message every stage advance; offer via `DialogMenu` with accept+decline (`dialogKey: "close"`).
- Limits: orange bar 45 characters max; options 35 characters max; dialog 90 characters/line,360 characters total (4 lines); no `—` in JSON,use `-`.

### Architecture Patterns

- WorldScreen owns all world UI (children of Root UIPanel); partials split by concern.
- Network->UI: ConnectionManager events,WorldScreen subscribes,builds/updates/shows controls.
- `WorldState` static exposes ViewModels updated by server packets; controls read `WorldState.Xxx` directly,no injection.
- Renderers cache lazily,`Clear()` on map change; `WorldEntity` data bag,`AnimationSystem` pure functions.
- Rendering never depends on Networking; Client coordinates; `ChaosGame` wires entity tracking at construction,before WorldScreen exists.
- Right-click A* to tile/entity,follow+auto-assail; arrows/spacebar cancel.
- `CastingSystem`: targeting->`UseSpellOnTarget`+chant progress.
- Tab targeting is opt-in with per-spell filters, F12 chords and kind-specific picks.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
