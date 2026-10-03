# Fishing body poses

Approved in chat on 2026-10-03 after browser mockups.

## What players see

1. **Cast** (880 ms): pole held out, pulled back by the shoulder, flicked forward; the line flies out and the bobber lands about 1.5 tiles ahead. Plays when fishing starts and after every catch or escape.
2. **Waiting:** standing pose, pole low, line to a gently bobbing bobber. The old splash effect stays.
3. **Reeling** (reel window open) follows the reel button, near arm:
   - Holding: arm raised, pole hauled up, bent and jerking, splash at the line.
   - Released: pole drops low.
   - A 140 ms in-between frame plays on each change.
4. Other players see every stage. Late viewers get the current stage (a running cast shows as waiting).
5. Walking or a body animation (emote, skill) wins over the pose. Ending the effect drops the pose.

## Why no new body art

Class armors only carry their own class's pose sheets, so only 01 (walk/stand), 02 (assail) and 03 (hands up / blow kiss / wave) exist for every armor, hair and helmet. Each player layer has about 3,000 sheets per pose set, so new hand-drawn sets were out. The chosen poses reuse frames from 01 and 03 in a new order at runtime. The pole, line, bobber and splash are drawn by the client, tinted per pole sprite (169 brown, 207 green, 206 blue, 210 silver, 211 red). The equipped pole's own art is hidden while a pose shows.

## How it works

- Server: `FishingPose` enum (Chaos.DarkAges), `Aisling.SetFishingPose` broadcasts `FishingDisplayArgs { Type = Pose, EntityId, Pose }` to everyone who can see the fisher; `SendDisplayAisling` follows with the current pose. Driven by both fishing effects and `FishingPlay` (hold/release). CLIENT_VERSION 768.
- Client: the Pose packet sets `WorldEntity.FishingPose`, `PreviousFishingPose` and the start time. `AnimationSystem.GetFishingShot` resolves a `FishingPoseShot` through `FishingPoseAnimator`; `WorldScreen.Draw` swaps the body frame, zeroes the weapon sprite and calls `FishingPoleRenderer` after the composite.

Frames: front stand 01:5, raised 03:9, mid 03:8; back stand 01:0, raised 03:7, mid 03:6.
