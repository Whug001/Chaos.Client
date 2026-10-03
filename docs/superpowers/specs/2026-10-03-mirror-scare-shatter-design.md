# Haunted-mirror scare: the screen shatters

Date: 2026-10-03. Replaces the 4-frame oval face (`MirrorScareFrames`). The "ghost climbs over the bottom edge"
idea from 2026-10-01 was dropped.

## What the player sees

A haunted mirror's scare still starts the same way: the same 1-in-6 slip windows, the 60 s cooldown, a random
Legend.dat stinger, and only when the mirror shows the local player. What plays is new:

1. **Crack, 0-0.12 s.** The whole screen (HUD included) freezes. Crack lines run out from near the centre, with a
   short white flash.
2. **Shatter, 0.12-1.05 s.** About 33 pieces of the frozen screen fly outward, spin and fall. Behind them is black,
   and a creature grows from 0.9x to 1.35x of the screen by 0.37 s. From 0.4 s it shakes.
3. **Black, 1.05-1.2 s.** A cut to black, then the game returns.

Each scare picks one of three hand-drawn creatures at random. Each has a few frames, timed from the scare's start:

| Creature | Frames |
|---|---|
| Bloody Mary | slit mouth, eye off to the side (0 s) -> half open (0.33 s) -> full scream (0.37 s) |
| The Grinner | blank black eyes, closed smile (0 s) -> pinprick pupils (0.30 s) -> half smile (0.39 s) -> full toothy grin (0.42 s) |
| The Eye | looking away, wide pupil (0 s) -> snaps to you (0.32 s) -> pupil shrinks (0.38 s) |

Leaving the mirror or changing maps still ends the scare at once and stops the stinger.

## Art

The ten 160x120 frames are drawn by `Tools/MirrorScare/draw_creatures.py` (Python, PIL + numpy; no AI). The
output PNGs live in `Chaos.Client.Rendering/Assets/MirrorScare/` and are embedded resources in the rendering
assembly. Nothing goes into a `.dat` archive, so no launcher data patch is needed.

## Code

- `MirrorScareTimeline` (Rendering, pure): phases, creature scale, shake, frame lookup, the creature cue table.
- `ShatterPattern` (Rendering, pure): builds the shards from a seed. Each pixel of the 640x480 frame belongs to
  exactly one shard: its sector comes from the 11 jittered rays and its ring from the straight chords between ray
  points. It cuts a frame into per-shard pixel boxes with pale crack edges, and gives each shard's offset and spin
  over time.
- `MirrorScareRenderer` (Rendering): loads the creature frames, builds shard textures from the frozen frame, and
  draws one scare frame.
- `WorldScreen.Mirrors`: starting a scare picks a creature and sets "capture pending". The next draw copies the
  finished frame from the bound render target with `GetData` and hands it to the renderer. That is one 640x480
  read per scare. Drawing starts on the frame after that.
- Removed: `MirrorScareFrames`, `MirrorMath.SCARE_FRAMES`, `SCARE_FRAME_SECONDS` and `ScareFrame`.

## Testing

Unit tests cover the timeline (phase edges, scale, frame lookup) and the shatter pattern (every pixel owned by
one shard, about 33 shards, the same seed gives the same pattern, and shards move outward and down). Check it in
game in the mirror maze's haunted wing.

## Not in scope

A setting to turn the scare off, and a glass-break sound.
