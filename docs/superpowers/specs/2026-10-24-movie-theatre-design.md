# Movie theatre — design

Date: 2026-10-24
Repos touched: Chaos.Client, Chaos-Server (submodule), Unora, Chaos.FileServer, plus a new film site on the M73.

## Goal

Players in the Loures 2 Floor Hall watch films, with sound, on a big screen built into the hall's back corner. Admins choose the films and run the showings from inside the game. Watching is free: no tickets.

Films must be public domain, Creative Commons, or licensed.

## Overview

1. Staff upload a film to a small web site on the M73 (192.168.4.125).
2. The M73 encodes it once into HLS chunks (short video files plus an index file) at 854×480.
3. An admin types `/film play <name>` in game.
4. The game server sends a small state message to everyone in the hall: which film, what position, paused or not.
5. Each client streams the chunks straight from the M73 with LibVLCSharp and draws the frames on the screen in the map.

The game server never handles video data.

## 1. Film site on the M73

**App.** A new ASP.NET Core app running on the M73 (Windows 11 Pro, .NET 10) as a background service. It shows an upload page and a film list, and serves the chunks.

**HTTPS and network.**
- Caddy sits in front of the app and gets its own certificates.
- Only port 443 is forwarded on the router. Caddy uses the TLS-ALPN challenge on 443, so port 80 stays closed.
- The domain is a free dynamic DNS name (for example DuckDNS) that follows the home IP.
- SSH and Remote Desktop stay closed to the internet. Cloudflare's free plan is not used for video.

**Sign-in.**
- Staff paste their existing admin-panel token.
- Chaos.FileServer gets a new "who am I" endpoint. It takes a token and returns its role, or a refusal. It sits behind the existing limit of 10 wrong tries per 5 minutes per address.
- The film site calls that endpoint and caches the answer for 5 minutes. Tokens stay stored only on the file server (`AdminOptions.Tokens`, SHA-256 hashes), so revoking one there locks it out of the film site within 5 minutes.
- A new **Cinema** role can upload, rename and delete films. **Publisher** can too, as the superuser role.

**Uploads.**
- Resumable uploads with tus (an open protocol for resumable uploads, via tusdotnet). A dropped connection continues where it stopped.
- Cap of 10 GB per file.
- The original goes into a `raw` folder and the film is queued for encoding.

**Film list page.**
- Each film shows title, length, and status: queued, encoding, ready or failed.
- Staff can rename, delete, or retry a failed film.
- The page shows the free disk space.
- A JSON endpoint returns the same list (ready films only, with id, title, length and chunk address) for the game server.

**Chunk serving.**
- Each ready film is served at `/films/<id>/index.m3u8` plus its chunk files.
- No sign-in is needed to fetch chunks. Anyone holding the link could watch, which is acceptable for public-domain or licensed films.
- About 1.2 Mbps per viewer, so 50 viewers is about 60 Mbps. The home connection is 2 Gbps both ways.

## 2. Encoding

**Worker.** A worker inside the film site encodes one film at a time with FFmpeg. FFmpeg and ffprobe are installed on the M73.

**Check first.** ffprobe reads each upload. A file with no video track is marked failed with a plain reason. Otherwise the length is recorded.

**Output (one version for every player).**
- Video: H.264, 854×480, capped at about 1.1 Mbps, at most 30 frames per second.
- Sound: AAC stereo, 128 kbps.
- HLS chunks of 6 seconds, keyframe every 2 seconds, so seeks and late joins land close to the right spot.
- Preset `veryfast`. Expected 30–60 minutes for a 90-minute film on the M73's 2 cores; measure the real figure during the build.

**Staying out of the way of test runs and Muse jobs.** The M73's full test runs take 14–19 minutes against a 20-minute limit.
- FFmpeg runs at idle priority.
- Every 10 seconds the worker looks for running test processes (`testhost`, `dotnet test`) or other .NET builds, excluding the film site's own process.
- While any are running, it suspends the FFmpeg process. It resumes it once they have all finished. No encoding work is lost.
- A film waiting its turn stays "queued".

**Disk.** About 0.9 GB per 90-minute film. The original is deleted after a successful encode and kept after a failure, so staff can retry.

**Out of scope.** Subtitles and multiple quality levels.

## 3. Client playback

**Packages.** LibVLCSharp plus `VideoLAN.LibVLC.Windows`, about 40 MB more in the client. The client is MonoGame DesktopGL 3.8, whose built-in video player doesn't work. The VLC player is only created while the player is in the hall and a film is running.

**Frames.**
- VLC decodes on its own thread into an 854×480 buffer (RV32 video callbacks).
- On each game update, if a new frame is ready, the client copies it into a Texture2D.
- The texture is drawn into the screen's picture area (about 360×190 game pixels). The film is 16:9, so it fits at about 338×190 with thin dark bars at the sides.
- The picture draws in the same pass as the screen's wall row, so players standing in front are drawn over it.

**Sound.** Film sound plays through VLC, not the game's sound system. Section 6 covers volume, mute and music fading.

**Sync.**
- From the server's state message, the client starts at the given position plus half its current ping.
- Every 5 seconds it compares its position with the expected one. More than 1.5 seconds off: it seeks.
- Pause, seek and stop messages apply at once.
- VLC network caching is 3 seconds.

**Leaving and returning.** Leaving the hall stops the film and disposes the player. Entering the hall gets the current state from the server and resumes at the right spot.

**Failures.** If the M73 can't be reached or a chunk fails, the screen shows a still "Film unavailable" image and the client retries every 15 seconds. The game keeps running normally.

## 4. Game server sync and admin control

**State.** A cinema service on the game server holds the current showing for the hall:
- film id, title and chunk address
- start time, in server time
- paused or not, and the paused position

The state is not saved. A server restart ends the showing.

**Message.** A new server-to-client message carries the film (id, title, chunk address), the current position, and whether it is paused. A stop is the same message with no film.
- Sent to everyone in the hall on start, pause, resume, seek and stop.
- Sent to each player entering the hall.
- A stop is sent to each player leaving the hall.
- New message, so CLIENT_VERSION is bumped. All players use Chaos.Client, so a custom message is fine.

**Admin commands** (existing admins only):
- `/film list` — ready films from the film site's JSON list.
- `/film play <name>` — start from the beginning.
- `/film pause`, `/film resume`.
- `/film seek mm:ss` (also `h:mm:ss`).
- `/film stop`.

When a film starts, everyone in the hall sees "Now showing: <title>". The showing ends on its own at the film's length.

**Config.** The film site's address goes into the server's appsettings. If the site can't be reached, `/film list` and `/film play` say so plainly.

## 5. Map changes and screen art

**Location.** The real Loures 2 Floor Hall (`loures_2_floor_hall`, map 15, 16×16), instance folder `Unora/Data/Configuration/MapInstances/Temuair/Towns/Loures/Loures_2_Floor_Hall`. It has no merchants or monsters. All exits stay:
- (5,15) to `loures_2_floor_corridor`
- (15,14) to `loures_cyril_corridor`
- (14,6) and (15,6) to `loures_3_floor_office`

**Wall.**
- A castle-wall section across the back corner, flat and facing the camera, per the approved `castle-wall.html` mockup.
- Floor footprint: a triangle, 8 tiles along each wall from the corner. The floor behind the front face becomes solid.
- Its top meets the existing wall tops. In the 924×718 hall render: apex (450,62), front face from (226,174) to (674,402).
- The corner plant and the first 2–3 chairs under it are removed. Nothing else in the hall changes.
- No new chairs. Players stand; there is no sitting pose.

**Art (drawn by Claude, not Muse).**
- Stone taken from the hall's real wall tops (the mockup's top was too bright).
- Light-grey end pillars and purple trim bands, as in the hall.
- A recessed screen: stone frame, purple inner border, sill.
- Dark-grey screen area when no film plays.
- Added as new wall tiles in the client's wall archive, marked solid.

The map file changes in both the server data and the client files.

**Picture rectangle.** The client keeps the picture rectangle for map 15, about 360×190 game pixels inside the recess.

**Checks before shipping.**
- Render the changed hall with `Unora/Tools/TownHall/MapRender` and get the user's approval of the image.
- An in-game screenshot with a test film running.

**Shipping.** Changed .dat files only for the art, plus the map file and the new client build.

## 6. Player settings

A new **Cinema** section in the F4 settings window, saved per PC like the other client settings.

| Setting | Default | Effect |
|---|---|---|
| Film volume (0–100) | 80 | Sets VLC's volume; applies at once. |
| Mute film sound | off | Silences VLC; the picture keeps playing. |
| Don't show video | off | Never starts VLC or downloads chunks; idle screen art; no film sound. Turning it off mid-showing joins at the current position. |
| Lower game music during films | on | `MusicDuck` fades game music to about 20% over 1 second while a film plays, and back over 2 seconds on stop, pause or leaving the hall. |

Sound effects are not changed.

## Out of scope

- Tickets or payment.
- A sitting pose or new chairs.
- Subtitles and multiple quality levels.
- Saving a showing across server restarts.
- Theatres on other maps.

## Testing

- Film site: unit tests for the role check and its cache, the film list, and the encoder's pause rule (test processes found → suspend).
- Server: unit tests for the cinema service position math (start, pause, resume, seek, end at length) and the command parsing (`mm:ss`, `h:mm:ss`, unknown film).
- Client: unit tests for the sync rule (seek only past 1.5 seconds) and the settings defaults.
- Manual: upload and encode a short public-domain film, play it in the hall with two clients, check late join, pause, seek, settings, and the "Film unavailable" fallback with the site stopped.

## Naming

The client and server already use "Theatre" for the stage lighting feature (`StageLightingControl`, `SpotlightRenderer`, `Popups/Theatre/`). To avoid a clash, this feature uses **Cinema** in code, settings and roles: the `Cinema` admin role, the cinema service, the F4 **Cinema** section, and a `Popups/Cinema/` or `Systems/Cinema/` folder where needed. "Movie theatre" is only the player-facing description.
