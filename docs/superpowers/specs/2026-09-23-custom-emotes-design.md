# Custom Emotes: Shared System + Five New Emotes — Design

Date: 2026-09-23
Status: Draft for review
Scope: client only. No server change.

## 1. Goal

Add five client-side emotes: Heart Eyes, Halo, Lightbulb, Anger Steam and Party Hat. They are selectable from the emote wheel and catalog.

Today each custom emote (Sunglasses, Middle Finger) is wired by hand into about eight files. Five more wired that way would add about 40 more hand-wired spots. So the work first moves every custom emote onto one shared system. After that, adding an emote means one new class plus one registry line.

## 2. Locked decisions

- **Approach:** one shared system for all custom emotes. Sunglasses and Middle Finger move onto it and must look and time exactly as before.
- **Bytes:** Heart Eyes = 20, Halo = 2, Lightbulb = 3, Anger Steam = 4, Party Hat = 5. The server relays any emote byte up to 44 (`WorldServer.OnEmote`), and none of these is a `BodyAnimation` member. Bytes 7 and 8 stay free for later.
- **Facing:** drawn only on front-facing poses, like Sunglasses today. Facing away shows nothing.
- **Headgear:** drawn on top of whatever the aisling wears. No per-helmet adjustment.
- **Art:** text pixel maps in code (rows of characters + a colour key), like `SunglassesEmote.GlassesPixels`. No new asset files.
- **Movement:** a playing custom emote keeps the player "not at rest" (no walking), as today.
- **Hotkeys:** none. Wheel and catalog only, like the two existing custom emotes.
- **Old client builds:** bytes 2–5 fall through to the assail swing on an old build. Byte 18 already behaves this way on old builds, so this is accepted.

## 3. Shared structure

All new types live in `Chaos.Client.Rendering/CustomEmotes/`.

### 3.1 `ICustomEmote`

Each custom emote implements:

- `int BodyAnimation`: the emote byte it sends and listens for.
- `string Name`: the catalog display name.
- `int PreviewFrame`: the icon code (1000 + n). It sits past emot01's 50 frames, as today.
- `float DurationMs`: total length. Nothing is drawn after it.
- `Draw(in CustomEmoteDrawContext ctx, float elapsedMs)`: draws the emote at a point in time.
- `int IconSourceFrame`: the emot01 frame the icon starts from. It is 0 (the plain Smile face) for the hand-drawn emotes and 13 (Stop) for Middle Finger.
- `SKImage? BuildIcon(SKImage sourceFrame)`: paints the settled emote onto the rendered source frame for the wheel icon. The caller has already palettized that frame with the viewer's body colour. It returns null when it can't, which leaves the plain source frame as the icon.

Sunglasses and Middle Finger keep their constants and pure logic (`Resolve`, `TryFold`, `HeadBobOffset`, …). Their existing tests stay valid.

### 3.2 `CustomEmoteDrawContext`

This is what the draw pass hands an emote:
- the `SpriteBatch`
- the composite origin in screen space (today's `baseX`/`baseY` arithmetic, done once)
- `flip`
- the head-bob row offset for the current walk frame
- the aisling's `alpha`
- body colour
- the shared texture cache

### 3.3 `CustomEmotes` registry

A static, ordered list of all seven emotes. Its lookups:
- `TryGet(int bodyAnimation, out ICustomEmote)`
- `TryGetByPreviewFrame(int previewFrame, out ICustomEmote)`

The list order sets the catalog order: Sunglasses, Middle Finger, Heart Eyes, Halo, Lightbulb, Anger Steam, Party Hat.

### 3.4 `PixelSprite`

A shared helper for the hand-drawn emotes:
- pixel map + colour key → `Texture2D` (cached by the renderer)
- pixel map + colour key → stamped onto an `SKBitmap` at an offset, growing the canvas in any direction, including up and left for art above the head

Today's `SunglassesRenderer.BuildTexture` and `UiRenderer.StampSunglasses` are replaced by it.

### 3.5 `CustomEmoteRenderer`

It replaces `SunglassesRenderer` and `MiddleFingerRenderer` on `ChaosGame`. It owns the texture cache, which is cleared with the other renderers. It builds the draw context and calls the active emote's `Draw`.

### 3.6 Call sites that become generic

| Where | Today | After |
|---|---|---|
| `WorldEntity` | `SunglassesElapsedMs`, `MiddleFingerRemainingMs`, `IsWearingSunglasses`, `IsFlippingOff` | `ActiveCustomEmote` (`ICustomEmote?`), `CustomEmoteElapsedMs`, `IsPlayingCustomEmote` |
| `WorldEntity.IsAtRest` | checks both flags | checks `IsPlayingCustomEmote` |
| `WorldScreen.Update` tick | one block per emote | one block: add elapsed time, clear the emote at `DurationMs` |
| `WorldScreen.Update.TryPlayLocalEmote` | switch per byte | `CustomEmotes.TryGet` → start it |
| `WorldScreen.ServerHandlers` body-animation handler | switch per byte | `CustomEmotes.TryGet` → start it |
| `WorldScreen.Draw` | one call per emote | one `CustomEmoteRenderer.Draw` call |
| `UiRenderer.GetEmoteFaceTexture` | sentinel checks per emote | `TryGetByPreviewFrame` → render emot01 `IconSourceFrame` → `BuildIcon` |
| `EmoteCatalog` | two hand-added entries + sentinel constants | appends one entry per registry item |

Middle Finger's icon starts from emot01 frame 13 through its `IconSourceFrame`, and its `BuildIcon` applies `TryFold`. Its world bubble keeps building from the body palette inside its own `Draw`, using the body colour in the draw context.

### 3.7 Icon crop

`ImageUtil.FindHeadBounds` skips pixels with R, G and B all ≥ 220, so that speech bubbles don't count as head. Emote art must keep every colour below that on at least one channel, or it gets cropped out of the icon. A test enforces this for every pixel map (§5).

## 4. The five emotes

All five use composite coordinates, the 111×85 aisling canvas. On it the head spans rows 24–38, the face spans about x53–61 and the eyes sit on rows 32–33. All five add `HeadBobOffset` rows during a walk and mirror about `AislingRenderer.FLIP_PIVOT_X` when flipped.

| Emote | Byte | Length | Behaviour |
|---|---|---|---|
| Heart Eyes | 20 | 1500 ms | Two small red hearts cover the eyes. They alternate between a small and a large heart about every 125 ms, which is about four pulses a second. |
| Halo | 2 | 2000 ms | A gold ellipse ring sits a few rows above the head's top row. It fades in over the first 200 ms, bobs ±1 row on a slow cycle, and a pale-gold glint slides across once. |
| Lightbulb | 3 | 1600 ms | A bulb rises about 4 rows into place above the head over 150 ms, unlit (grey). It flickers lit/unlit twice, then stays lit (yellow) with short rays until the end. |
| Anger Steam | 4 | 1600 ms | Puffs start at ear height on both sides of the head: three per side, staggered. Each rises, drifts outward, grows through 2–3 sizes and fades. Puff colour is light grey, kept below the icon cut-off. |
| Party Hat | 5 | 1800 ms | A striped cone hat with a pom-pom falls onto the head (same easing as the sunglasses drop). On landing, about 8 confetti pixels in mixed colours burst outward and fall for about 400 ms. |

Exact pixel maps and row/column positions are set during art review (§6). The timings above are the agreed starting values.

## 5. Testing

TUnit, in `Tests/Chaos.Client.Tests`. Run with `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`.

- **Registry:** bytes are unique and in 1–44. No byte is a defined `BodyAnimation` member. Preview frames are unique and ≥ 1000.
- **Catalog:** 40 entries (was 35). The seven custom emotes come last, in registry order. `TryGet` finds each byte.
- **Per-emote timing:** each finishes at `DurationMs`. Key moments hold: hat landing, bulb turning on, halo fade-in done, hearts alternating.
- **Pixel maps:** every row has the same width. Every character is in the emote's colour key. No colour is near-white (§3.7).
- **Mirroring:** each piece's flipped X equals `AislingRenderer.MirrorX` of its unflipped right edge.
- **Regression:** the existing `SunglassesEmoteTests`, `MiddleFingerEmoteTests` and `EmoteCatalogTests` pass, changed only where an API moved.

## 6. Art review and in-game check

1. **Preview sheet (throwaway).** A script in the session scratchpad renders each new emote on a real emot01 face from the game data. It is enlarged 4× and shows frames over time, facing both ways. The user approves or asks for changes before the emotes are wired in. The script is not committed.
2. **In-game check.** After build + tests pass, the user plays each emote from the wheel in the client, to judge timing and feel.

## 7. Order of work

1. Build the shared structure. Move Sunglasses and Middle Finger onto it, and confirm there is no visible or test change.
2. Draw the five emotes. Produce the preview sheet for approval.
3. Register and wire the approved emotes, and add their tests.
4. In-game check by the user, then one commit for the whole change (this spec included).

## 8. Out of scope

- Server changes, and emote bytes above 44.
- Back-facing versions of any emote.
- Per-helmet placement.
- Hotkeys for custom emotes.
- Speech-bubble emotes ("?", "…", thumbs up). The shared system supports them later.
