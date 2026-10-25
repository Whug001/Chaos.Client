# Celebration Map Effects: Confetti and Balloons

Date: 2026-10-24. Repos: Chaos-Server (server and shared protocol code) and Chaos.Client. Unora data gets one config line.

## Goal

The Mileth fair should look like a celebration while it is open. Two new map effects do this: **Confetti** and **Balloons**. They are separate effects, so a GM can switch on either one alone on any map.

What the user decided:

- The looks are previews A (confetti) and B (balloons) from the brainstorm companion page `.superpowers/brainstorm/146327-1792884471/content/celebration-looks.html`. Sparkle bursts (preview C) were rejected.
- The fair grounds are the fenced yard at the bottom of the Mileth town map (map 397), not their own map. A map effect covers the whole map. So the fair effects come on only while a Mileth festival runs, which is also when the fair vendors appear.
- GMs switch them on anywhere with the existing `/mapFlag` command.
- Both are added to the Suomi theatre's stage-effects menu.

## Server (Chaos-Server)

### New flags

In `Chaos.DarkAges/Definitions/Enums.cs`, `MapFlags` gets two new bits, each with a one-line doc comment like its neighbours:

```csharp
Confetti = 1UL << 31,
Balloons = 1UL << 32,
```

`MapFlags` is a `ulong`, and every bit above the low byte goes out in the `SetMapEffects` packet. The packet format does not change.

### GM switch

`Messaging/Admin/MapFlagCommand.cs` parses any `MapFlags` name, so `/mapFlag add Confetti`, `/mapFlag add Balloons` and the matching `remove` commands work with no code change. `ShowCommand` lists the map's flags by name, so it shows them too.

### Festival fair effects

- `TownCatalog` reads a new optional town setting, `fairEffects`: a list of `MapFlags` names, parsed the same way as `festivalEffects`. A name that is not a `MapFlags` value is a load error, reported the way `festivalEffects` reports one. A missing list means no fair effects.
- The town record carries the parsed list (combined into one `MapFlags` value), the way it carries `FairVendors`.
- `TownFestivalScript.Update` currently works out `wanted` from `festival?.Effect`. While a festival runs, `wanted` also includes the town's fair effects. When no festival runs, it includes neither. The existing logic stays the same:
  - the script removes only what it applied (`Applied`);
  - it does not take ownership of a flag the map already has on its own.
  
  That second rule must hold for each flag separately. If the map already has Confetti, the script still applies and later removes Balloons and the festival effect, but never Confetti. Today the check is all-or-nothing on `wanted` (`(Subject.Flags & wanted) == wanted`). It changes to `wanted &= ~Subject.Flags` after the old `Applied` flags are removed.
- Unora `Data/Configuration/Towns/mileth.json` gets `"fairEffects": ["Confetti", "Balloons"]` next to `festivalEffects`. No other town sets it.

### Theatre menu

`Scripting/DialogScripts/Temuair/Suomi/TheatreStageEffects.cs` gets two new rows after Harvest Moon:

```csharp
new("Confetti", MapFlags.Confetti, 2),
new("Balloons", MapFlags.Balloons, 2)
```

Page 2 goes from 14 to 16 effects, which is exactly `MAX_EFFECTS_PER_PAGE`. Any existing test that checks the page limit must still pass.

## Client (Chaos.Client)

### Looks

Both effects are `ParticleRenderer` effects with a `ParticleStyle` preset each, and each one needs a new `ParticleShape`:

- **`ParticleShape.Confetti`** is a small filled rectangle, about 2-4 px wide and 4-7 px tall. It spins, and its height squashes with `|cos|` of its spin angle, so it looks like paper flipping over. Preset `ParticleStyle.Confetti`:
  - about 70 particles, falling at about 18-34 px/s;
  - a light sideways sway;
  - these colours: `#ff4d6d`, `#ffd23f`, `#3ec1ff`, `#6be37a`, `#c77dff`, `#ff9f1c` and `#ffffff`;
  - not additive.
- **`ParticleShape.Balloon`** is a filled oval about 1.2 times taller than it is wide, 6-9 px across. It has a small white highlight at the top left and a thin, light wavy string hanging about 2.5 radii below it. Preset `ParticleStyle.Balloons`:
  - about 9 particles, rising at about 14-24 px/s;
  - a slow sway;
  - the first six confetti colours (no white);
  - not additive.

The preview page above is the reference for amount, speed and feel. Tune the numbers in game. Particle counts scale with screen area the way the other presets do (`REFERENCE_AREA`).

### Wiring

- In `AmbientEffects`:
  - `MapFlags.Confetti` maps to a `ParticleRenderer` with `ParticleStyle.Confetti`;
  - `MapFlags.Balloons` maps to a `ParticleRenderer` with `ParticleStyle.Balloons`;
  - both flags are added to `CoveredFlags`.
  
  `AmbientEffectsTests` already fails for any effect flag with no overlay, so it covers the new ones.
- Update the `AmbientEffects` line in `CLAUDE.md` to list Confetti and Balloons and the new bit range (8-32).

### F4 hide boxes

Add "Disable confetti" and "Disable balloons" to F4's "Triggering or Unsettling Map Effects" section. Each new effect goes in the four places listed for that section:

1. the `ClientSettings` bool, with its load and save lines;
2. the `SettingKey`, plus a row in `SettingDefinitions`;
3. `MapEffectFilter.Keys` and `HiddenByPlayer`;
4. the filter test's `Expected` map.

Both default to unticked, so the effects show.

### Version

No `CLIENT_VERSION` bump. The packet does not change, and older clients ignore bits they have no overlay for.

## Testing

Server (run with `dotnet run --project Tests/Chaos.Tests`):

- `TownCatalog`:
  - `fairEffects` loads into the town record;
  - a missing list gives none;
  - a bad name gives a load error;
  - the shipped `mileth.json` parses with no errors.
- `TownFestivalScript`:
  - a festival starting adds the festival effect plus Confetti and Balloons;
  - the festival ending removes all three;
  - a map that already has Confetti keeps it after the festival ends, while Balloons is still added and removed;
  - a town without `fairEffects` behaves as before.
- `TheatreStageEffects`: page 2 lists Confetti and Balloons and holds no more than 16 effects.

Client (run with `dotnet build`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`):

- `AmbientEffectsTests` passes with both new flags covered.
- The `MapEffectFilter` tests: ticking each new box strips its flag, and leaving it unticked keeps the flag.

In game:

1. Start a Mileth festival and walk to the fair yard. Check that confetti and balloons show with the festival effect, and that they go away when the festival ends.
2. Run `/mapFlag add Confetti` and `/mapFlag add Balloons` on another map. Check that each shows alone and that `remove` clears it.
3. Pick both from the theatre's stage-effects page 2.
4. Tick each F4 box. Check that the effect hides.

## Ship order

Deploy the server with or before the client release. The new flags live in the shared `Chaos.DarkAges` code, so the client must build against the server commit that adds them. The Unora `mileth.json` line ships with the server.

## Out of scope

- Sparkle bursts.
- Limiting an effect to part of a map, such as only the fair yard.
- Any other town's fair.
