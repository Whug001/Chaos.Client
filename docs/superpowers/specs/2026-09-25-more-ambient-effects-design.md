# More ambient effects, part 1: wider flags and 11 new effects

Designed 2026-09-25. It is the first of three parts that add 16 new atmosphere effects:

1. **This part.** Widen the map-effect flags from 4 bytes to 8, and add 11 effects built from the existing mist and particle renderers.
2. **Part 2.** Sunbeams, aurora, shooting stars, and bats and crows. Each needs new drawing code.
3. **Part 3.** Heat shimmer on the Heat flag and a water wobble on the Underwater flag. The client has no shader, so this redraws the map in thin horizontal strips shifted on a wave. It starts with a quick test of the look and the cost.

Parts 2 and 3 get their own specs. How the existing effects work is in `Unora/docs/ambient-map-effects.md`.

## Goal

- Eleven new map flags: Gloom, Radiance, Arcane, Frost, Blizzard, Dust, CloudShadows, SeaSpray, Heat, Wisps and Drips.
- Room for about 30 more effects after parts 1 and 2.
- The Suomi theatre can start and stop the new effects, on a second menu page.

## Decisions

| Question | Decision |
|---|---|
| Flag space | `MapFlags` becomes 8 bytes (`ulong`). The effect packet grows from 4 bytes to 8. |
| How each effect looks | Chosen from animated browser previews drawn over two real Unora scenes. The numbers are in "The presets" below. |
| Wisps | Glowing orbs with fairy wings: two pairs, small, fluttering fast (preview 10A). Wings are a new optional particle setting. |
| Dust, Cloud shadows | The second-round versions: stronger Dust, medium Cloud shadows (preview 7A). |
| Theatre menu | A second page. Page 1 is unchanged apart from a "More Effects" option at the end. |
| Maps | None in this part. The effects are reachable only through `/mapFlag` and the theatre. Suggested maps are listed at the end for later. |
| Old clients | The client version goes up by one, so an old client can't read the longer packet. |

## 1. Wider flags

### Server (`Chaos-Server`)

`MapFlags` in `Chaos.DarkAges/Definitions/Enums.cs` changes from `: uint` to `: ulong`. The new flags take bits 17 to 27:

```csharp
    NoTownMap = 1 << 16,
    Gloom = 1UL << 17,
    Radiance = 1UL << 18,
    Arcane = 1UL << 19,
    Frost = 1UL << 20,
    Blizzard = 1UL << 21,
    Dust = 1UL << 22,
    CloudShadows = 1UL << 23,
    SeaSpray = 1UL << 24,
    Heat = 1UL << 25,
    Wisps = 1UL << 26,
    Drips = 1UL << 27
```

Other changes:

- `SetMapEffectsArgs.ExtendedFlags` becomes `ulong`.
- `SetMapEffectsConverter` reads and writes it with `ReadUInt64` and `WriteUInt64`. `SpanReader` and `SpanWriter` in `Chaos.IO` already have both.
- `ChaosWorldClient.SendMapInfo` sends `(ulong)flags & ~0xFFUL`.
- `MapFlagVisibility.ATMOSPHERE` adds all 11 flags, so `/atmosphere` hides them.
- `CONSTANTS.CLIENT_VERSION` in `Chaos.DarkAges/Definitions/CONSTANTS.cs` goes up by one. The client reads the same constant, so this one change covers both sides.

Any other code that turns `MapFlags` into a number fails to compile, and gets fixed to use `ulong`. About 20 server files use `MapFlags`. Most only combine named flags, which behaves the same at 8 bytes.

`instance.json` stores flags by name, so no map file changes.

### Client (`Chaos.Client`)

- `AmbientEffects.EXTENDED_FLAGS` becomes `(MapFlags)0xFFFF_FFFF_FFFF_FF00UL`.
- `WorldScreen.HandleSetMapEffects` already casts `args.ExtendedFlags` to `MapFlags`, so it works unchanged at 8 bytes.
- `WeatherRenderer` reads only the low four bits through a `(byte)` cast. It doesn't change.

## 2. The presets

Every effect is a preset in `Chaos.Client.Rendering/MistStyle.cs` or `ParticleStyle.cs`. The values below are the approved previews. `MistLayer` is `(scale, alpha, drift in px/sec)`. Fields left out keep their defaults. The previews didn't show fading, so the few `FadeSeconds` values here were set to match similar existing presets. They change only how long an effect takes to fade in and out.

### Mists

```csharp
/// <summary>Dark violet dim with slow shadow wisps and heavy dark edges.</summary>
public static MistStyle Gloom { get; } = new()
{
    WashColor = new Color(28, 16, 44),
    WashAlpha = 0.32f,
    Pulse = MistPulse.Sine,
    PulseDepth = 0.15f,
    PulsePeriod = 7f,
    LayerTint = new Color(50, 30, 70),
    Layers =
    [
        new MistLayer(4.0f, 0.35f, new Vector2(-5f, 1f)),
        new MistLayer(2.6f, 0.40f, new Vector2(8f, -2f))
    ],
    Seed = 6661,
    NoiseKnee = 0.45f,
    NoiseRange = 0.4f,
    VignetteColor = new Color(8, 0, 18),
    VignetteAlpha = 0.6f,
    VignetteInner = 0.5f
};

/// <summary>Warm gold glow breathing slowly. Paired with rising golden motes.</summary>
public static MistStyle Radiance { get; } = new()
{
    WashColor = new Color(255, 210, 120),
    WashAlpha = 0.10f,
    Pulse = MistPulse.Sine,
    PulseDepth = 0.3f,
    PulsePeriod = 6f,
    LayerTint = new Color(255, 236, 180),
    Layers =
    [
        new MistLayer(3.5f, 0.16f, new Vector2(0f, -6f)),
        new MistLayer(2.2f, 0.18f, new Vector2(3f, -10f))
    ],
    Seed = 7777,
    NoiseKnee = 0.45f
};

/// <summary>Faint pulsing violet haze. Paired with violet and blue sparks.</summary>
public static MistStyle Arcane { get; } = new()
{
    WashColor = new Color(60, 30, 120),
    WashAlpha = 0.14f,
    Pulse = MistPulse.Sine,
    PulseDepth = 0.3f,
    PulsePeriod = 4f,
    LayerTint = new Color(150, 110, 230),
    Layers = [new MistLayer(3.0f, 0.14f, new Vector2(-4f, -3f))],
    Seed = 2718,
    NoiseKnee = 0.5f,
    NoiseRange = 0.4f,
    VignetteColor = new Color(20, 0, 40),
    VignetteAlpha = 0.3f,
    VignetteInner = 0.6f
};

/// <summary>Cold blue tint with frosted white edges. Paired with pale sparkles.</summary>
public static MistStyle Frost { get; } = new()
{
    WashColor = new Color(150, 190, 230),
    WashAlpha = 0.14f,
    LayerTint = new Color(230, 245, 255),
    Layers = [new MistLayer(3.0f, 0.12f, new Vector2(4f, 1f))],
    Seed = 1212,
    NoiseKnee = 0.5f,
    VignetteColor = new Color(220, 238, 255),
    VignetteAlpha = 0.45f,
    VignetteInner = 0.6f
};

/// <summary>White-out haze tearing sideways. Paired with snow flakes and streaks.</summary>
public static MistStyle Blizzard { get; } = new()
{
    FadeSeconds = 1.5f,
    WashColor = new Color(200, 210, 225),
    WashAlpha = 0.26f,
    LayerTint = new Color(240, 245, 255),
    Layers =
    [
        new MistLayer(3.0f, 0.28f, new Vector2(-120f, 20f)),
        new MistLayer(2.0f, 0.34f, new Vector2(-200f, 30f)),
        new MistLayer(1.3f, 0.24f, new Vector2(-300f, 40f))
    ],
    Seed = 5150,
    NoiseKnee = 0.32f,
    VignetteColor = new Color(230, 238, 250),
    VignetteAlpha = 0.35f,
    VignetteInner = 0.55f
};

/// <summary>Faint warm haze. Paired with specks hanging in the air.</summary>
public static MistStyle Dust { get; } = new()
{
    WashColor = new Color(170, 150, 110),
    WashAlpha = 0.10f,
    LayerTint = new Color(220, 200, 160),
    Layers = [new MistLayer(3.5f, 0.14f, new Vector2(3f, -1f))],
    Seed = 3030,
    NoiseKnee = 0.5f
};

/// <summary>Large soft shadows of passing clouds sliding over the ground. No tint.</summary>
public static MistStyle CloudShadows { get; } = new()
{
    FadeSeconds = 2f,
    LayerTint = new Color(8, 14, 24),
    Layers = [new MistLayer(5.0f, 0.45f, new Vector2(14f, 6f))],
    Seed = 4040,
    NoiseKnee = 0.45f,
    NoiseRange = 0.35f
};

/// <summary>Pale salt mist blowing sideways. Paired with white flecks of spray.</summary>
public static MistStyle SeaSpray { get; } = new()
{
    WashColor = new Color(180, 200, 210),
    WashAlpha = 0.08f,
    LayerTint = new Color(235, 245, 250),
    Layers =
    [
        new MistLayer(3.0f, 0.18f, new Vector2(-40f, -2f)),
        new MistLayer(1.8f, 0.20f, new Vector2(-70f, -4f))
    ],
    Seed = 8080,
    NoiseKnee = 0.45f
};

/// <summary>Orange tint throbbing gently, with faint haze rising.</summary>
public static MistStyle Heat { get; } = new()
{
    WashColor = new Color(200, 110, 40),
    WashAlpha = 0.14f,
    Pulse = MistPulse.Sine,
    PulseDepth = 0.25f,
    PulsePeriod = 3f,
    LayerTint = new Color(255, 190, 120),
    Layers =
    [
        new MistLayer(2.5f, 0.12f, new Vector2(0f, -18f)),
        new MistLayer(1.6f, 0.10f, new Vector2(3f, -28f))
    ],
    Seed = 9191,
    NoiseKnee = 0.5f,
    VignetteColor = new Color(120, 40, 0),
    VignetteAlpha = 0.3f,
    VignetteInner = 0.6f
};
```

`CloudShadows` has no wash: `WashAlpha` stays 0, so it draws only the dark cloud layer.

### Particles

```csharp
/// <summary>Golden motes rising and glimmering. Paired with the Radiance mist.</summary>
public static ParticleStyle RadianceMotes { get; } = new()
{
    Count = 30,
    Shape = ParticleShape.Dot,
    Additive = true,
    Colors = [new Color(255, 220, 120), new Color(255, 240, 180), new Color(255, 200, 90)],
    SizeMin = 1.5f,
    SizeMax = 3f,
    VelocityMin = new Vector2(-3f, -18f),
    VelocityMax = new Vector2(3f, -8f),
    Sway = new Vector2(6f, 0f),
    SwayFreqMin = 0.3f,
    SwayFreqMax = 0.7f,
    AlphaMin = 0.6f,
    AlphaMax = 1f,
    Twinkle = 0.5f,
    TwinkleFreqMin = 0.5f,
    TwinkleFreqMax = 1.2f,
    TwinkleSharpness = 1.5f,
    HaloScale = 4f,
    HaloAlpha = 0.25f
};

/// <summary>Violet and blue sparks drifting up and flickering. Paired with the Arcane mist.</summary>
public static ParticleStyle ArcaneSparks { get; } = new()
{
    Count = 35,
    Shape = ParticleShape.Dot,
    Additive = true,
    Colors = [new Color(170, 120, 255), new Color(110, 160, 255), new Color(220, 170, 255)],
    SizeMin = 1.5f,
    SizeMax = 3f,
    VelocityMin = new Vector2(-5f, -25f),
    VelocityMax = new Vector2(5f, -10f),
    Sway = new Vector2(10f, 4f),
    SwayFreqMin = 0.4f,
    SwayFreqMax = 1f,
    AlphaMin = 0.7f,
    AlphaMax = 1f,
    Twinkle = 0.8f,
    TwinkleFreqMin = 1.5f,
    TwinkleFreqMax = 3f,
    TwinkleSharpness = 2.5f,
    HaloScale = 3.5f,
    HaloAlpha = 0.3f
};

/// <summary>Pale sparkles drifting down and twinkling. Paired with the Frost mist.</summary>
public static ParticleStyle FrostSparkles { get; } = new()
{
    Count = 30,
    Shape = ParticleShape.Dot,
    Additive = true,
    Colors = [new Color(190, 225, 255), new Color(235, 245, 255)],
    SizeMin = 1.5f,
    SizeMax = 2.5f,
    VelocityMin = new Vector2(-3f, 4f),
    VelocityMax = new Vector2(3f, 10f),
    Sway = new Vector2(6f, 0f),
    Twinkle = 0.8f,
    TwinkleFreqMin = 0.5f,
    TwinkleFreqMax = 1.5f,
    TwinkleSharpness = 2f,
    HaloScale = 3f,
    HaloAlpha = 0.2f
};

/// <summary>Snow streaks driving sideways. Paired with the Blizzard mist and flakes.</summary>
public static ParticleStyle BlizzardStreaks { get; } = new()
{
    FadeSeconds = 1.5f,
    Count = 120,
    Shape = ParticleShape.Streak,
    Colors = [new Color(255, 255, 255), new Color(230, 238, 250), new Color(210, 222, 240)],
    SizeMin = 3f,
    SizeMax = 8f,
    VelocityMin = new Vector2(-420f, 60f),
    VelocityMax = new Vector2(-260f, 140f),
    Sway = new Vector2(0f, 15f),
    SwayFreqMin = 1f,
    SwayFreqMax = 2f,
    AlphaMin = 0.5f,
    AlphaMax = 0.9f
};

/// <summary>Snow flakes blowing sideways, slower than the streaks.</summary>
public static ParticleStyle BlizzardFlakes { get; } = new()
{
    FadeSeconds = 1.5f,
    Count = 70,
    Shape = ParticleShape.Square,
    Colors = [new Color(255, 255, 255), new Color(235, 242, 252)],
    SizeMin = 1.5f,
    SizeMax = 3f,
    VelocityMin = new Vector2(-240f, 40f),
    VelocityMax = new Vector2(-150f, 90f),
    Sway = new Vector2(0f, 20f),
    SwayFreqMin = 0.8f,
    SwayFreqMax = 1.6f,
    AlphaMin = 0.6f,
    AlphaMax = 1f
};

/// <summary>Warm specks hanging in the air and catching the light. Paired with the Dust mist.</summary>
public static ParticleStyle DustMotes { get; } = new()
{
    FadeSeconds = 2f,
    Count = 70,
    Shape = ParticleShape.Dot,
    Additive = true,
    Colors = [new Color(255, 235, 200), new Color(240, 220, 190)],
    SizeMin = 1.5f,
    SizeMax = 3f,
    VelocityMin = new Vector2(-2f, -2f),
    VelocityMax = new Vector2(2f, 3f),
    Sway = new Vector2(4f, 3f),
    SwayFreqMin = 0.05f,
    SwayFreqMax = 0.2f,
    AlphaMin = 0.5f,
    AlphaMax = 0.9f,
    Twinkle = 0.5f,
    TwinkleFreqMin = 0.2f,
    TwinkleFreqMax = 0.5f,
    HaloScale = 2.5f,
    HaloAlpha = 0.3f
};

/// <summary>White flecks of spray blowing sideways. Paired with the SeaSpray mist.</summary>
public static ParticleStyle SeaSprayFlecks { get; } = new()
{
    Count = 45,
    Shape = ParticleShape.Dot,
    Colors = [new Color(245, 250, 255), new Color(225, 238, 245)],
    SizeMin = 1f,
    SizeMax = 2.5f,
    VelocityMin = new Vector2(-160f, -20f),
    VelocityMax = new Vector2(-90f, 10f),
    Sway = new Vector2(0f, 20f),
    SwayFreqMin = 0.8f,
    SwayFreqMax = 1.5f,
    AlphaMin = 0.4f,
    AlphaMax = 0.8f
};

/// <summary>A few blue lights on small fluttering fairy wings, drifting slowly and dimming.</summary>
public static ParticleStyle Wisps { get; } = new()
{
    FadeSeconds = 2f,
    Count = 6,
    Shape = ParticleShape.Dot,
    Additive = true,
    Colors = [new Color(120, 190, 255), new Color(160, 230, 255), new Color(100, 255, 230)],
    SizeMin = 4f,
    SizeMax = 6f,
    VelocityMin = new Vector2(-6f, -4f),
    VelocityMax = new Vector2(6f, 4f),
    Sway = new Vector2(22f, 16f),
    SwayFreqMin = 0.05f,
    SwayFreqMax = 0.15f,
    AlphaMin = 0.7f,
    AlphaMax = 1f,
    Twinkle = 0.4f,
    TwinkleFreqMin = 0.2f,
    TwinkleFreqMax = 0.4f,
    TwinkleSharpness = 1.5f,
    HaloScale = 6f,
    HaloAlpha = 0.3f,
    WingScale = 1.5f,
    WingWidth = 0.6f,
    WingAlpha = 0.5f,
    WingPairs = 2,
    WingFlapMin = 7f,
    WingFlapMax = 9f
};

/// <summary>A few water drops falling from above.</summary>
public static ParticleStyle Drips { get; } = new()
{
    Count = 8,
    Shape = ParticleShape.Streak,
    Colors = [new Color(170, 200, 230), new Color(200, 225, 250)],
    SizeMin = 4f,
    SizeMax = 7f,
    VelocityMin = new Vector2(0f, 220f),
    VelocityMax = new Vector2(0f, 320f),
    AlphaMin = 0.35f,
    AlphaMax = 0.6f
};
```

## 3. Wings

Wings are new optional settings on `ParticleStyle`, next to the halo settings:

| Field | Default | Meaning |
|---|---|---|
| `WingScale` | 0 | Wing length as a multiple of the particle size. 0 means no wings. |
| `WingWidth` | 1 | Wing width as a multiple of the particle size. |
| `WingAlpha` | 0.5 | Wing opacity as a fraction of the particle's own. |
| `WingPairs` | 1 | 1 draws one pair. 2 adds a smaller lower pair. |
| `WingFlapMin` / `WingFlapMax` | 2 | Flaps per second. Each particle picks one. |

Every existing preset leaves `WingScale` at 0, so the existing effects draw exactly as before.

`ParticleRenderer` changes:

- `Particle` gets `FlapFreq`, rolled from `WingFlapMin`..`WingFlapMax`, and `FlapPhase`, rolled from 0..τ.
- A new 32x16 wing texture is made the first time it's needed and released in `Dispose`. Its alpha comes from `u` in 0..1 (0 at the body end) and `v` in -1..1:
  - `half = sin(π · u^0.7)`
  - `edge = half <= 0 ? 0 : 1 - |v| / half`
  - `alpha = clamp(edge · 2.5, 0, 1) · (0.55 + 0.45 · u)`
- In `Draw`, the wings go between the halo and the body:
  - `flap = 0.2 + 0.8 · |sin(τ · FlapFreq · Clock + FlapPhase)|`
  - length = `Size · WingScale · flap`, width = `Size · WingWidth`
  - color = the particle's color moved 55% of the way to white, at `alpha · WingAlpha`
  - Origin is the texture's narrow end, `(0, 8)`. Scale is `(length / 32, width / 16)`.
  - Upper wing angle: -0.45 rad on the right and `π + 0.45` on the left.
  - With `WingPairs = 2`, a lower wing is added at 0.55 rad on the right and `π - 0.55` on the left, at 0.7 of the length and 0.8 of the width.

The wing is symmetric along its length, so rotating it half a turn mirrors it correctly for the left side.

## 4. Draw order

The `Overlays` list in `AmbientEffects.cs` runs back to front:

```csharp
(MapFlags.CloudShadows, new MistRenderer(MistStyle.CloudShadows)),
(MapFlags.Underwater, new MistRenderer(MistStyle.Underwater)),
(MapFlags.Heat, new MistRenderer(MistStyle.Heat)),
(MapFlags.Gloom, new MistRenderer(MistStyle.Gloom)),
(MapFlags.BloodMoon, new MistRenderer(MistStyle.BloodMoon)),
(MapFlags.Miasma, new MistRenderer(MistStyle.Miasma)),
(MapFlags.Radiance, new MistRenderer(MistStyle.Radiance)),
(MapFlags.Arcane, new MistRenderer(MistStyle.Arcane)),
(MapFlags.Frost, new MistRenderer(MistStyle.Frost)),
(MapFlags.Dust, new MistRenderer(MistStyle.Dust)),
(MapFlags.SeaSpray, new MistRenderer(MistStyle.SeaSpray)),
(MapFlags.Sandstorm, new MistRenderer(MistStyle.Sandstorm)),
(MapFlags.Blizzard, new MistRenderer(MistStyle.Blizzard)),
(MapFlags.Lightning, new LightningRenderer()),
(MapFlags.Fog, new MistRenderer(MistStyle.Fog)),
(MapFlags.Sandstorm, new ParticleRenderer(ParticleStyle.SandGrains)),
(MapFlags.Underwater, new ParticleRenderer(ParticleStyle.Bubbles)),
(MapFlags.Ash, new ParticleRenderer(ParticleStyle.Ash)),
(MapFlags.Ash, new ParticleRenderer(ParticleStyle.Embers)),
(MapFlags.Leaves, new ParticleRenderer(ParticleStyle.Leaves)),
(MapFlags.Petals, new ParticleRenderer(ParticleStyle.Petals)),
(MapFlags.Fireflies, new ParticleRenderer(ParticleStyle.Fireflies)),
(MapFlags.Radiance, new ParticleRenderer(ParticleStyle.RadianceMotes)),
(MapFlags.Arcane, new ParticleRenderer(ParticleStyle.ArcaneSparks)),
(MapFlags.Frost, new ParticleRenderer(ParticleStyle.FrostSparkles)),
(MapFlags.Dust, new ParticleRenderer(ParticleStyle.DustMotes)),
(MapFlags.SeaSpray, new ParticleRenderer(ParticleStyle.SeaSprayFlecks)),
(MapFlags.Blizzard, new ParticleRenderer(ParticleStyle.BlizzardFlakes)),
(MapFlags.Blizzard, new ParticleRenderer(ParticleStyle.BlizzardStreaks)),
(MapFlags.Wisps, new ParticleRenderer(ParticleStyle.Wisps)),
(MapFlags.Drips, new ParticleRenderer(ParticleStyle.Drips))
```

Cloud shadows go first because they lie on the ground. The existing entries keep their order relative to each other.

## 5. Theatre second page

### Server

In `TheatreStageEffects`:

- `StageEffect` becomes `StageEffect(string Name, MapFlags Flag, int Page)`.
- Page 1 is today's 12 effects in today's order.
- Page 2 is: Gloom, Radiance, Arcane, Frost, Blizzard, Dust, Cloud Shadows, Sea Spray, Heat, Wisps, Drips.
- New constants: `MORE_TEXT = "More Effects"` and `BACK_TEXT = "Back"`.
- `Available(MapFlags current, int page)` returns that page's effects. While the lights are off it still leaves out Snow and Rain, which are both on page 1.
- `ClearAll` keeps covering `All`, so it stops effects on both pages.

In `suomiTheatreScript`:

- `OnDisplaying`, case `suomitheatre_stageeffects`: page 1 options, then "Clear All Effects", then "More Effects" going to `suomitheatre_stageeffects2`.
- `OnDisplaying`, new case `suomitheatre_stageeffects2`: the same theatre check. Then page 2 options, each going back to `suomitheatre_stageeffects2`. Then "Clear All Effects" going to `suomitheatre_stageeffects2`, and "Back" going to `suomitheatre_stageeffects`.
- `OnNext`, new case `suomitheatre_stageeffects2`: calls `HandleStageEffects`. "More Effects" and "Back" don't parse as effects, so `HandleStageEffects` ignores them and the dialog moves on.

Row counts: page 1 has 14 rows and page 2 has 13. The option panel fits 18 rows before it runs off the top of the screen. Part 2's four effects go on page 2, making 17.

### Unora

New file `Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stageeffects2.json`:

```json
{
  "options": [],
  "scriptKeys": [
    "suomitheatre"
  ],
  "scriptVars": {},
  "templateKey": "suomitheatre_stageeffects2",
  "text": "More atmosphere! Choose an effect to start it on the stage, or choose it again to stop it.",
  "type": "DialogMenu"
}
```

## 6. Testing

### Server tests

- `MapEffectsPacketConverterTests`:
  - Add the 11 flags to `AmbientFlags`, and change the `uint` casts to `ulong`.
  - New test: a value with a bit above 31 set, such as `(MapFlags)(1UL << 40)`, survives a round trip.
- `MapFlagVisibilityTests`: add the 11 flags to `EVERY_EFFECT_BUT_SNOW`.
- `TheatreStageEffectsTests`:
  - Page 1 lists exactly today's 12 effects.
  - Page 2 lists the 11 new ones.
  - The dark-stage test counts page 1 only.
  - Clear All stops a page-2 effect.
  - `TryParseOption` reads back a page-2 option.
  - New guard: no page has more than 16 effects.

Run the three classes with:

```
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/(MapEffectsPacketConverterTests)|(MapFlagVisibilityTests)|(TheatreStageEffectsTests)/*"
```

The two known failures on master (`GiveAbility`, `OnItemDroppedOn` stackable) are not in these classes.

### Client test (new)

`Tests/Chaos.Client.Tests/AmbientEffectsTests.cs` gets one guard test. Every single-bit `MapFlags` value from bit 4 up has at least one overlay in `AmbientEffects`. `NoTabMap`, `SnowTileset` and `NoTownMap` are the exceptions. The test project references `Chaos.Client` only, and has no access to internals. So `AmbientEffects` gets a public read-only `CoveredFlags` property that lists the flags in `Overlays`.

### In game

Close any running server or client first, because they lock the build. Then build both and start them.

- On a test map, use `/mapFlag add` for each flag. Screenshot each one with the window automation and compare it with its preview.
- Watch Blizzard, the heaviest effect, for a frame-rate drop.
- Theatre:
  - Page 1 ends with "More Effects".
  - Page 2 flips an effect between Start and Stop.
  - "Back" returns to page 1.
  - "Clear All Effects" stops effects on both pages.
- `/atmosphere` hides the new effects.

## 7. Docs

- `Unora/docs/ambient-map-effects.md`:
  - Add the 11 effects to the effects table.
  - Update the bit table: 17–27 are used and 28–63 are free.
  - Say the flags are 8 bytes and the packet sends 8 bytes.
  - Add the wing fields to the `ParticleStyle` table.
  - Mention the theatre's second page.
  - Update "Limits": 36 free slots, not "Fifteen more slots".
  - Replace the `Snowglow` walk-through with the next free bit, 28.
- `Chaos.Client/CLAUDE.md`: list the new flags on the AmbientEffects line, and say the effect packet carries bits 8–63.

## 8. Delivery

- Each repo gets a `feat/more-ambient-effects` branch, in its own worktree so the shared checkouts aren't touched. The bases:
  - server master `f90d470f3`
  - client main `d63c4db`
  - Unora main `ad2031769`
- One commit per repo at the end. Commit the server first, then the client with the `Chaos-Server` pointer moved to the server commit, then Unora (the dialog JSON and the doc).
- Nothing is merged or pushed without the user's go-ahead.

**Open item for merge time.** The shared server checkout has an uncommitted edit to `MapFlagVisibilityTests.cs`. It adds `NOT_EFFECTS` and `Atmosphere_CoversEveryEffectFlag`, and it predates this work. That test casts to `uint`, which won't compile once the flags are 8 bytes. This branch adds the same test with `ulong`. Merging into the shared checkout will then collide with that local edit, so ask the user before replacing it.

## Suggested maps for later

Not part of this work. The user chose to add no effects to maps for now.

| Effect | Suggested maps |
|---|---|
| Gloom | House Macabre, Deep Crypt 1–3, nightmare challenge maps, Halloween's Macabre Mansion, Macabre Yard, Billy's Graveyard |
| Radiance | Radiant Temple, Divine Trials, GodsRealm (both copies) |
| Arcane | Lunar Sanctum, Transcendence Chamber |
| Frost | Wilderness Frozen Cave; Mount Merry and North Pole alongside their Snow |
| Blizzard | Frosty's Challenge, replacing its Snow |
| Dust | Eingren Manor, Dubhaim Castle, Kasmanium Mines, Deep Crypt |
| Cloud shadows | Grassy Fields, Asilon Prairie, Noam Field, the four Lynith Beach maps |
| Sea spray | The four Lynith Beach maps, both Pirate Ship Deck maps, the Passenger Ship deck |
| Heat | Arena_Lava, Arena_LavaTeams, Fire Elemental Master, Flame Guardian Domain, Fire Canyon 1–3 |
| Wisps | Lost Woodlands, the After-life, Cthonic Remains, Cthonic Demise |
| Drips | The 10 Wilderness caves, the secluded cave, Kasmanium Mines, WaterDungeon |
