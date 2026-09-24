# Theatre Spotlights Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Garamonde Theatre director a Stage Lighting window with a house-light dimmer, up to 8 colored, animated spotlights and saved scenes, seen by everyone in the Theatre.

**Architecture:** The server owns the live setup (`StageLighting` on `SuomiTheatreMapScript`). It checks every edit and sends the whole setup (at most 257 bytes) to everyone on the Theatre map after each change. Clients animate motion and effects locally (`StageLightAnimator`). Spotlights lift the existing CPU darkness layer through `LightingSystem`, and a new additive `SpotlightRenderer` adds the color. The director edits through a new UI window (`StageLightingControl`).

**Tech Stack:** C# 14 / .NET 10, MonoGame SpriteBatch, Chaos-Server packet converters (`SpanReader`/`SpanWriter`), TUnit + FluentAssertions, Chaos `IStorage<T>` JSON storage.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-09-24-theatre-spotlights-design.md`. Approved mockups: `Chaos.Client/.superpowers/brainstorm/1086360-1790245966/content/` (`spotlight-look.html`, `house-lights.html`, `board-compact.html`).

## Global Constraints

- **Work only in the three worktrees from Task 0.** Never edit, stage, stash, reset or switch branches in the shared checkouts (`C:/Users/Michael/Documents/GitHub/Chaos.Client`, its `Chaos-Server` submodule, `C:/Users/Michael/Documents/GitHub/Unora`). Other Claude sessions are working in them.
  - `SRV` = `C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server` (Chaos-Server, branch `feat/theatre-spotlights` from `master`)
  - `CLI` = `C:/Users/Michael/Documents/GitHub/worktrees/spotlights-client` (Chaos.Client, branch `feat/theatre-spotlights` from `main`)
  - `UNO` = `C:/Users/Michael/Documents/GitHub/worktrees/spotlights-unora` (Unora, branch `feat/theatre-spotlights` from `main`)
- **Do not commit** in Tasks 0–10. Leave all changes in the worktree. Task 11 makes the commits (at-end strategy).
- **Build the client against the server worktree:** `dotnet build CLI/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server`. Never run two builds at once; they share the protocol projects' output folders. If a build fails with MSB3027 (a file is locked), a local `Chaos.exe` or game client is running. Stop and report; don't kill it.
- **Test projects are TUnit executables.** Use `dotnet run --project ... -- --treenode-filter "/*/<Namespace>/<Class>/*" --no-ansi`, never `dotnet test`.
- **Serena works in the worktrees** (they sit under `C:/Users/Michael/Documents/GitHub`). Use Serena for code files, per the user's CLAUDE.md.
- **Never stage** `Chaos/appsettings.json` or `Chaos.Client/Properties/launchSettings.json`.
- **Positions on the wire** are map tiles in sixteenths: the value `16 × t` is the centre of tile `t`. Client-side "tile" vectors are fractional tiles with centres on whole numbers.
- **The game font is 6×12 ASCII.** Use only ASCII in UI captions (`<` `>` `-` `+` `x`).
- Exact values (from the spec): max 8 lights, max 20 scenes, scene names 1–24 printable characters, speeds 1–5, brightness 0–100, house level 0–100, drag sends ≤ 8 per second (125 ms), server drops > 20 edits per second per player, fades 0 / 150 / 300 / 400 / 1000 ms (enter / light edit / blackout + lights off/on / house or glow / scene).

**User decisions (already made):**
- Both spotlight looks: a pool of light, and a pool plus a beam. The beam is a per-light on/off switch.
- House lights are a 0–100% dimmer, not on/off.
- Spotlights go on the 9×9 stage only.
- The window's stage view is the diamond view (layout A), at the compact size, with a minimize-to-bar button.
- All four extras are in: follow an actor, moving lights, light effects, saved scenes.
- Saved scenes belong to the Theatre (shared, survive restarts).
- All players use Chaos.Client; no plain-client fallback.
- The server holds the setup; clients animate it (approach 1).
- While the user is away: take every recommended option, run with subagents, audit after completion.

---

## File map

| Repo | File | Responsibility |
|---|---|---|
| SRV | `Chaos.DarkAges/Definitions/Enums.cs` | 5 new enums |
| SRV | `Chaos.Networking.Abstractions/Definitions/Enums.cs` | 3 new opcodes |
| SRV | `Chaos.Networking/Entities/Server/StageLightInfo.cs` | one light's settings (wire + saved form) |
| SRV | `Chaos.Networking/Entities/Server/StageLightingStateArgs.cs` | the whole setup message |
| SRV | `Chaos.Networking/Entities/Server/StageLightingBoardArgs.cs` | window open / scenes / close |
| SRV | `Chaos.Networking/Entities/Client/StageLightingInteractionArgs.cs` | one edit from the director |
| SRV | `Chaos.Networking/Converters/StageLightInfoCodec.cs` | shared read/write of a light |
| SRV | `Chaos.Networking/Converters/Server/StageLightingStateConverter.cs`, `StageLightingBoardConverter.cs` | server message converters |
| SRV | `Chaos.Networking/Converters/Client/StageLightingInteractionConverter.cs` | client message converter |
| SRV | `Chaos/Services/Theatre/StageLighting.cs` | the live setup; clamps every edit |
| SRV | `Chaos/Services/Theatre/TheatreLightingScenes.cs` | saved scenes (storage type) |
| SRV | `Chaos/Services/Theatre/StageLightingRateLimiter.cs` | 20 edits/s per player |
| SRV | `Chaos/Services/Theatre/TheatreLanterns.cs` | which lantern each player gets |
| SRV | `Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs` | owns the setup, applies edits, broadcasts |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageLightingOpenScript.cs` | dialog → window |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs` | lights off/on go through the setup |
| SRV | `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, `Chaos/Networking/ChaosWorldClient.cs` | 2 send methods |
| SRV | `Chaos/Services/Servers/WorldServer.cs` | edit handler |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_options.json`, `suomitheatre_stagelighting.json` | dialog data |
| CLI | `Chaos.Client.Networking/ConnectionManager.cs`, `Definitions/Delegates.cs` | events + send |
| CLI | `Chaos.Client/Utilities/HsvColor.cs` | HSV conversions |
| CLI | `Chaos.Client/Systems/StageLightAnimator.cs` | setup + per-frame light math |
| CLI | `Chaos.Client.Rendering/LightSource.cs`, `DarknessRenderer.cs` | strength + house darkness |
| CLI | `Chaos.Client.Rendering/SpotlightMasks.cs`, `SpotlightRenderer.cs` | darkness masks; additive color |
| CLI | `Chaos.Client/Systems/LightingSystem.cs` | spotlights join the light sources |
| CLI | `Chaos.Client/Collections/WorldState.cs` | `StageLights`, `StageLightingPanel` |
| CLI | `Chaos.Client/Screens/WorldScreen.StageLighting.cs` (new partial) + small edits in `WorldScreen.cs`, `.Update.cs`, `.Draw.cs`, `.Map.cs` | wiring |
| CLI | `Chaos.Client/ViewModel/StageLightingPanelState.cs` | window state, throttle, helpers |
| CLI | `Chaos.Client/Controls/World/Popups/Theatre/*.cs` | geometry, button, slider, picker, stage view, window |
| CLI | `CLAUDE.md` | draw order 5b, new classes |

---

### Task 0: Create the three worktrees

**Goal:** Isolated branches for the server, client and Unora so no shared checkout is touched.

**Files:**
- Create: worktrees `SRV`, `CLI`, `UNO` (see Global Constraints)

**Acceptance Criteria:**
- [ ] `git -C SRV branch --show-current` prints `feat/theatre-spotlights`, and likewise for `CLI` and `UNO`
- [ ] `dotnet build CLI/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=<SRV>` succeeds before any change

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server status --short` → empty

**Steps:**

- [ ] **Step 1: Check for running game processes (they lock builds)**

Run (PowerShell): `Get-Process Chaos, Chaos.Client -ErrorAction SilentlyContinue | Select-Object Name, Id, Path`
Expected: nothing. If anything is listed, stop and report. Do not kill it.

- [ ] **Step 2: Create the worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub
mkdir -p worktrees
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/spotlights-server -b feat/theatre-spotlights master
git -C Chaos.Client worktree add /c/Users/Michael/Documents/GitHub/worktrees/spotlights-client -b feat/theatre-spotlights main
git -C Unora -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/spotlights-unora -b feat/theatre-spotlights main
```

Pass `core.longpaths` with `-c` only. Never write it to the repo config.

- [ ] **Step 3: Copy the approved spec and this plan into the client worktree**

The spec and plan were written in the shared client checkout and are not committed. Copy them so Task 11 commits them from the worktree:

```bash
mkdir -p /c/Users/Michael/Documents/GitHub/worktrees/spotlights-client/docs/superpowers/specs /c/Users/Michael/Documents/GitHub/worktrees/spotlights-client/docs/superpowers/plans
cp /c/Users/Michael/Documents/GitHub/Chaos.Client/docs/superpowers/specs/2026-09-24-theatre-spotlights-design.md /c/Users/Michael/Documents/GitHub/worktrees/spotlights-client/docs/superpowers/specs/
cp /c/Users/Michael/Documents/GitHub/Chaos.Client/docs/superpowers/plans/2026-09-24-theatre-spotlights.md /c/Users/Michael/Documents/GitHub/Chaos.Client/docs/superpowers/plans/2026-09-24-theatre-spotlights.md.tasks.json /c/Users/Michael/Documents/GitHub/worktrees/spotlights-client/docs/superpowers/plans/
```

- [ ] **Step 4: Baseline build**

```bash
dotnet build /c/Users/Michael/Documents/GitHub/worktrees/spotlights-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server
```

Expected: `Build succeeded`.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server branch --show-current", "acceptanceCriteria": ["three worktrees on feat/theatre-spotlights", "baseline client build succeeds against SRV"], "modelTier": "mechanical"}
```

---

### Task 1: Shared protocol — enums, messages, converters

**Goal:** Three new messages that round-trip every field, shared by server and client.

**Files:**
- Modify: `SRV/Chaos.DarkAges/Definitions/Enums.cs` (insert after `BeautyShopInteractionType`)
- Modify: `SRV/Chaos.Networking.Abstractions/Definitions/Enums.cs` (end of `ServerOpCode` and `ClientOpCode`)
- Create: `SRV/Chaos.Networking/Entities/Server/StageLightInfo.cs`
- Create: `SRV/Chaos.Networking/Entities/Server/StageLightingStateArgs.cs`
- Create: `SRV/Chaos.Networking/Entities/Server/StageLightingBoardArgs.cs`
- Create: `SRV/Chaos.Networking/Entities/Client/StageLightingInteractionArgs.cs`
- Create: `SRV/Chaos.Networking/Converters/StageLightInfoCodec.cs`
- Create: `SRV/Chaos.Networking/Converters/Server/StageLightingStateConverter.cs`
- Create: `SRV/Chaos.Networking/Converters/Server/StageLightingBoardConverter.cs`
- Create: `SRV/Chaos.Networking/Converters/Client/StageLightingInteractionConverter.cs`
- Test: `SRV/Tests/Chaos.Tests/Networking/StageLightingPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `StageLightingState` round-trips with 0 and with 8 lights, every field kept
- [ ] `StageLightingBoard` round-trips Open, Scenes and Close
- [ ] `StageLightingInteraction` round-trips every `StageLightingAction`; an unknown action throws
- [ ] Opcodes 128 and 129 (server) and 125 (client) are each used exactly once

**Verify:** `dotnet run --project SRV/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/StageLightingPacketConverterTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Add the enums** to `SRV/Chaos.DarkAges/Definitions/Enums.cs`, directly after `public enum BeautyShopInteractionType { ... }`:

```csharp
/// <summary>How wide a Theatre spotlight's pool of light is.</summary>
public enum StageLightSize : byte
{
    Small = 0,
    Medium = 1,
    Large = 2
}

/// <summary>What a Theatre spotlight's brightness or color does over time.</summary>
public enum StageLightEffect : byte
{
    None = 0,
    Pulse = 1,
    Flicker = 2,
    ColorCycle = 3
}

/// <summary>How a Theatre spotlight moves.</summary>
public enum StageLightMotion : byte
{
    Still = 0,
    Sweep = 1,
    Circle = 2,
    Follow = 3
}

public enum StageLightingBoardType : byte
{
    /// <summary>Open the Stage Lighting window. Carries the saved scene names.</summary>
    Open = 0,

    /// <summary>The saved scene names changed.</summary>
    Scenes = 1,

    /// <summary>Close the window.</summary>
    Close = 2
}

/// <summary>One edit from the Stage Lighting window.</summary>
public enum StageLightingAction : byte
{
    SetHouseLevel = 0,
    Blackout = 1,
    SetStageGlow = 2,
    AddLight = 3,
    UpdateLight = 4,
    RemoveLight = 5,
    ClearLights = 6,
    SaveScene = 7,
    LoadScene = 8,
    DeleteScene = 9
}
```

- [ ] **Step 2: Add the opcodes** in `SRV/Chaos.Networking.Abstractions/Definitions/Enums.cs`. In `ServerOpCode`, after `BugReportOpen = 127,`:

```csharp

    /// <summary>
    ///     The Theatre's whole lighting setup: stage rectangle, house level, stage glow, fade time and up to 8 spotlights.
    ///     Sent to everyone on the Theatre map after each accepted edit and to each player who enters.
    ///     <br />
    ///     Hex value: 0x80
    /// </summary>
    StageLightingState = 128,

    /// <summary>
    ///     Opens, updates the scene list of, or closes the director's Stage Lighting window.
    ///     <br />
    ///     Hex value: 0x81
    /// </summary>
    StageLightingBoard = 129,
```

In `ClientOpCode`, after `BugReportInteraction = 124,`:

```csharp

    /// <summary>
    ///     One edit from the Stage Lighting window. The server checks the sender's role and every value.
    ///     <br />
    ///     Hex value: 0x7D
    /// </summary>
    StageLightingInteraction = 125,
```

- [ ] **Step 3: Create `StageLightInfo.cs`**

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     One Theatre spotlight's settings, as sent to clients and as saved inside a lighting scene. Positions are map
///     tiles in sixteenths: <c>16 × t</c> is the centre of tile <c>t</c>. (X2, Y2) is the sweep's far end or a point on
///     the circle's edge.
/// </summary>
public sealed record StageLightInfo
{
    /// <summary>Position units per tile.</summary>
    public const int UNITS_PER_TILE = 16;

    public byte Id { get; set; }
    public ushort X { get; set; }
    public ushort Y { get; set; }
    public ushort X2 { get; set; }
    public ushort Y2 { get; set; }
    public byte R { get; set; } = 255;
    public byte G { get; set; } = 255;
    public byte B { get; set; } = 255;
    public StageLightSize Size { get; set; } = StageLightSize.Medium;
    public byte Brightness { get; set; } = 80;
    public bool Beam { get; set; } = true;
    public StageLightEffect Effect { get; set; } = StageLightEffect.None;
    public byte EffectSpeed { get; set; } = 3;
    public StageLightMotion Motion { get; set; } = StageLightMotion.Still;
    public byte MotionSpeed { get; set; } = 3;

    /// <summary>The Aisling a Follow light stays on. 0 when the light doesn't follow anyone.</summary>
    public uint FollowId { get; set; }
}
```

- [ ] **Step 4: Create `StageLightingStateArgs.cs`**

```csharp
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>A light in a lighting setup, with how far into its motion and effect it is right now.</summary>
public sealed record StageLightState
{
    public required StageLightInfo Light { get; set; }

    /// <summary>Milliseconds since this light's motion started. Clients continue the sweep or circle from here.</summary>
    public uint MotionElapsedMs { get; set; }

    /// <summary>Milliseconds since this light's effect started.</summary>
    public uint EffectElapsedMs { get; set; }
}

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.StageLightingState" /> packet: the Theatre's whole
///     lighting setup. Clients blend from what they show now to this setup over <see cref="FadeMs" />.
/// </summary>
public sealed record StageLightingStateArgs : IPacketSerializable
{
    public byte StageX { get; set; }
    public byte StageY { get; set; }
    public byte StageWidth { get; set; }
    public byte StageHeight { get; set; }

    /// <summary>0 (house lights off) to 100 (fully lit).</summary>
    public byte HouseLevel { get; set; } = 100;

    public bool StageGlow { get; set; } = true;
    public ushort FadeMs { get; set; }
    public IReadOnlyList<StageLightState> Lights { get; set; } = [];
}
```

- [ ] **Step 5: Create `StageLightingBoardArgs.cs`**

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.StageLightingBoard" /> packet. Open and Scenes carry
///     the Theatre's saved scene names; Close carries nothing.
/// </summary>
public sealed record StageLightingBoardArgs : IPacketSerializable
{
    public required StageLightingBoardType Type { get; set; }
    public IReadOnlyList<string> SceneNames { get; set; } = [];
}
```

- [ ] **Step 6: Create `StageLightingInteractionArgs.cs`** (in `Entities/Client`)

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>
///     Represents the serialization of the <see cref="ClientOpCode.StageLightingInteraction" /> packet: one edit from the
///     Stage Lighting window. Every field is always sent; each action ignores the ones it doesn't use. The server checks
///     the sender's role and corrects every value.
/// </summary>
public sealed record StageLightingInteractionArgs : IPacketSerializable
{
    public required StageLightingAction Action { get; set; }

    /// <summary>SetHouseLevel: 0-100.</summary>
    public byte Level { get; set; }

    /// <summary>SetStageGlow: on or off.</summary>
    public bool Flag { get; set; }

    /// <summary>RemoveLight: the light to remove.</summary>
    public byte LightId { get; set; }

    /// <summary>AddLight / UpdateLight: the light's settings. AddLight ignores the id.</summary>
    public StageLightInfo Light { get; set; } = new();

    /// <summary>SaveScene / LoadScene / DeleteScene.</summary>
    public string SceneName { get; set; } = string.Empty;
}
```

- [ ] **Step 7: Create `StageLightInfoCodec.cs`** (in `SRV/Chaos.Networking/Converters/`)

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Entities.Server;

namespace Chaos.Networking.Converters;

/// <summary>Reads and writes one <see cref="StageLightInfo" />: 23 bytes, the same order both ways.</summary>
internal static class StageLightInfoCodec
{
    public static StageLightInfo Read(ref SpanReader reader)
        => new()
        {
            Id = reader.ReadByte(),
            X = reader.ReadUInt16(),
            Y = reader.ReadUInt16(),
            X2 = reader.ReadUInt16(),
            Y2 = reader.ReadUInt16(),
            R = reader.ReadByte(),
            G = reader.ReadByte(),
            B = reader.ReadByte(),
            Size = (StageLightSize)reader.ReadByte(),
            Brightness = reader.ReadByte(),
            Beam = reader.ReadBoolean(),
            Effect = (StageLightEffect)reader.ReadByte(),
            EffectSpeed = reader.ReadByte(),
            Motion = (StageLightMotion)reader.ReadByte(),
            MotionSpeed = reader.ReadByte(),
            FollowId = reader.ReadUInt32()
        };

    public static void Write(ref SpanWriter writer, StageLightInfo light)
    {
        writer.WriteByte(light.Id);
        writer.WriteUInt16(light.X);
        writer.WriteUInt16(light.Y);
        writer.WriteUInt16(light.X2);
        writer.WriteUInt16(light.Y2);
        writer.WriteByte(light.R);
        writer.WriteByte(light.G);
        writer.WriteByte(light.B);
        writer.WriteByte((byte)light.Size);
        writer.WriteByte(light.Brightness);
        writer.WriteBoolean(light.Beam);
        writer.WriteByte((byte)light.Effect);
        writer.WriteByte(light.EffectSpeed);
        writer.WriteByte((byte)light.Motion);
        writer.WriteByte(light.MotionSpeed);
        writer.WriteUInt32(light.FollowId);
    }

    /// <summary>Narrows a count to a byte, throwing rather than silently truncating.</summary>
    public static byte CountToByte(int count, string what)
    {
        if ((uint)count > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(count), count, $"Cannot encode {count} {what}; the wire format allows at most {byte.MaxValue}.");

        return (byte)count;
    }
}
```

- [ ] **Step 8: Create `StageLightingStateConverter.cs`** (in `Converters/Server`)

```csharp
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="StageLightingStateArgs" /></summary>
public sealed class StageLightingStateConverter : PacketConverterBase<StageLightingStateArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.StageLightingState;

    /// <inheritdoc />
    public override StageLightingStateArgs Deserialize(ref SpanReader reader)
    {
        var args = new StageLightingStateArgs
        {
            StageX = reader.ReadByte(),
            StageY = reader.ReadByte(),
            StageWidth = reader.ReadByte(),
            StageHeight = reader.ReadByte(),
            HouseLevel = reader.ReadByte(),
            StageGlow = reader.ReadBoolean(),
            FadeMs = reader.ReadUInt16()
        };

        var count = reader.ReadByte();
        var lights = new List<StageLightState>(count);

        for (var i = 0; i < count; i++)
            lights.Add(
                new StageLightState
                {
                    Light = StageLightInfoCodec.Read(ref reader),
                    MotionElapsedMs = reader.ReadUInt32(),
                    EffectElapsedMs = reader.ReadUInt32()
                });

        args.Lights = lights;

        return args;
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, StageLightingStateArgs args)
    {
        writer.WriteByte(args.StageX);
        writer.WriteByte(args.StageY);
        writer.WriteByte(args.StageWidth);
        writer.WriteByte(args.StageHeight);
        writer.WriteByte(args.HouseLevel);
        writer.WriteBoolean(args.StageGlow);
        writer.WriteUInt16(args.FadeMs);

        var lights = args.Lights ?? [];
        writer.WriteByte(StageLightInfoCodec.CountToByte(lights.Count, "stage lights"));

        foreach (var entry in lights)
        {
            StageLightInfoCodec.Write(ref writer, entry.Light);
            writer.WriteUInt32(entry.MotionElapsedMs);
            writer.WriteUInt32(entry.EffectElapsedMs);
        }
    }
}
```

- [ ] **Step 9: Create `StageLightingBoardConverter.cs`** (in `Converters/Server`)

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="StageLightingBoardArgs" /></summary>
public sealed class StageLightingBoardConverter : PacketConverterBase<StageLightingBoardArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.StageLightingBoard;

    /// <inheritdoc />
    public override StageLightingBoardArgs Deserialize(ref SpanReader reader)
    {
        var type = (StageLightingBoardType)reader.ReadByte();

        switch (type)
        {
            case StageLightingBoardType.Open:
            case StageLightingBoardType.Scenes:
            {
                var count = reader.ReadByte();
                var names = new List<string>(count);

                for (var i = 0; i < count; i++)
                    names.Add(reader.ReadString8());

                return new StageLightingBoardArgs
                {
                    Type = type,
                    SceneNames = names
                };
            }

            case StageLightingBoardType.Close:
                return new StageLightingBoardArgs { Type = type };

            default:
                throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown stage lighting board type");
        }
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, StageLightingBoardArgs args)
    {
        writer.WriteByte((byte)args.Type);

        switch (args.Type)
        {
            case StageLightingBoardType.Open:
            case StageLightingBoardType.Scenes:
            {
                var names = args.SceneNames ?? [];
                writer.WriteByte(StageLightInfoCodec.CountToByte(names.Count, "scene names"));

                foreach (var name in names)
                    writer.WriteString8(name ?? string.Empty);

                break;
            }

            case StageLightingBoardType.Close:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(args), args.Type, "Unknown stage lighting board type");
        }
    }
}
```

- [ ] **Step 10: Create `StageLightingInteractionConverter.cs`** (in `Converters/Client`)

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="StageLightingInteractionArgs" /></summary>
public sealed class StageLightingInteractionConverter : PacketConverterBase<StageLightingInteractionArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.StageLightingInteraction;

    /// <inheritdoc />
    public override StageLightingInteractionArgs Deserialize(ref SpanReader reader)
    {
        var action = (StageLightingAction)reader.ReadByte();

        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(nameof(reader), action, "Unknown stage lighting action");

        return new StageLightingInteractionArgs
        {
            Action = action,
            Level = reader.ReadByte(),
            Flag = reader.ReadBoolean(),
            LightId = reader.ReadByte(),
            Light = StageLightInfoCodec.Read(ref reader),
            SceneName = reader.ReadString8()
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, StageLightingInteractionArgs args)
    {
        writer.WriteByte((byte)args.Action);
        writer.WriteByte(args.Level);
        writer.WriteBoolean(args.Flag);
        writer.WriteByte(args.LightId);
        StageLightInfoCodec.Write(ref writer, args.Light ?? new());
        writer.WriteString8(args.SceneName ?? string.Empty);
    }
}
```

- [ ] **Step 11: Write the tests** — `SRV/Tests/Chaos.Tests/Networking/StageLightingPacketConverterTests.cs`

```csharp
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Converters.Client;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;
using FluentAssertions;

namespace Chaos.Tests.Networking;

public sealed class StageLightingPacketConverterTests
{
    private static readonly Encoding Enc = Encoding.GetEncoding(949);

    private static T RoundTrip<T>(PacketConverterBase<T> converter, T original) where T: class, IPacketSerializable
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        converter.Serialize(ref writer, original);
        var bytes = writer.ToSpan()
                          .ToArray();

        var reader = new SpanReader(Enc, bytes);

        return converter.Deserialize(ref reader);
    }

    private static StageLightInfo Light(byte id)
        => new()
        {
            Id = id,
            X = (ushort)(16 * id),
            Y = (ushort)(200 + id),
            X2 = 128,
            Y2 = 320,
            R = (byte)(id * 30),
            G = 60,
            B = 200,
            Size = (StageLightSize)(id % 3),
            Brightness = (byte)(id * 10),
            Beam = id % 2 == 0,
            Effect = (StageLightEffect)(id % 4),
            EffectSpeed = (byte)(1 + (id % 5)),
            Motion = (StageLightMotion)(id % 4),
            MotionSpeed = (byte)(5 - (id % 5)),
            FollowId = id * 1000u
        };

    [Test]
    public void State_round_trips_eight_lights()
    {
        var original = new StageLightingStateArgs
        {
            StageX = 0,
            StageY = 12,
            StageWidth = 9,
            StageHeight = 9,
            HouseLevel = 30,
            StageGlow = false,
            FadeMs = 1000,
            Lights = Enumerable.Range(1, 8)
                               .Select(i => new StageLightState
                               {
                                   Light = Light((byte)i),
                                   MotionElapsedMs = (uint)(i * 12345),
                                   EffectElapsedMs = uint.MaxValue - (uint)i
                               })
                               .ToList()
        };

        RoundTrip(new StageLightingStateConverter(), original)
            .Should()
            .BeEquivalentTo(original);
    }

    [Test]
    public void State_round_trips_no_lights()
    {
        var original = new StageLightingStateArgs { StageX = 0, StageY = 12, StageWidth = 9, StageHeight = 9 };

        RoundTrip(new StageLightingStateConverter(), original)
            .Should()
            .BeEquivalentTo(original);
    }

    [Test]
    [Arguments(StageLightingBoardType.Open)]
    [Arguments(StageLightingBoardType.Scenes)]
    public void Board_round_trips_scene_names(StageLightingBoardType type)
    {
        var original = new StageLightingBoardArgs { Type = type, SceneNames = ["Act 1 - Opening", "Finale", "Blackout cue"] };

        RoundTrip(new StageLightingBoardConverter(), original)
            .Should()
            .BeEquivalentTo(original);
    }

    [Test]
    public void Board_close_round_trips()
    {
        var original = new StageLightingBoardArgs { Type = StageLightingBoardType.Close };

        RoundTrip(new StageLightingBoardConverter(), original)
            .Type
            .Should()
            .Be(StageLightingBoardType.Close);
    }

    [Test]
    public void Interaction_round_trips_every_action()
    {
        foreach (var action in Enum.GetValues<StageLightingAction>())
        {
            var original = new StageLightingInteractionArgs
            {
                Action = action,
                Level = 42,
                Flag = true,
                LightId = 3,
                Light = Light(3),
                SceneName = "Act 2"
            };

            RoundTrip(new StageLightingInteractionConverter(), original)
                .Should()
                .BeEquivalentTo(original, $"{action} must round-trip");
        }
    }

    [Test]
    public void Interaction_with_unknown_action_throws()
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        writer.WriteByte(200);
        var bytes = writer.ToSpan()
                          .ToArray();

        var act = () =>
        {
            var reader = new SpanReader(Enc, bytes);

            return new StageLightingInteractionConverter().Deserialize(ref reader);
        };

        act.Should()
           .Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void New_opcodes_are_unique()
    {
        Enum.GetValues<ServerOpCode>()
            .Count(v => (byte)v == 128)
            .Should()
            .Be(1);

        Enum.GetValues<ServerOpCode>()
            .Count(v => (byte)v == 129)
            .Should()
            .Be(1);

        Enum.GetValues<ClientOpCode>()
            .Count(v => (byte)v == 125)
            .Should()
            .Be(1);
    }
}
```

If the lambda capturing a `ref struct` does not compile in `Interaction_with_unknown_action_throws`, move the reader creation into a local static function `static StageLightingInteractionArgs Parse(byte[] bytes) { var reader = new SpanReader(Enc, bytes); return new StageLightingInteractionConverter().Deserialize(ref reader); }` and assert on `() => Parse(bytes)`.

- [ ] **Step 12: Run the tests**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/StageLightingPacketConverterTests/*" --no-ansi`
Expected: 7 tests (plus 2 argument cases) pass. If `New_opcodes_are_unique` fails, another branch took the number; pick the next free one on both sides and update the spec's table.

```json:metadata
{"files": ["Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs", "Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos-Server/Chaos.Networking/Entities/Server/StageLightInfo.cs", "Chaos-Server/Chaos.Networking/Entities/Server/StageLightingStateArgs.cs", "Chaos-Server/Chaos.Networking/Entities/Server/StageLightingBoardArgs.cs", "Chaos-Server/Chaos.Networking/Entities/Client/StageLightingInteractionArgs.cs", "Chaos-Server/Chaos.Networking/Converters/StageLightInfoCodec.cs", "Chaos-Server/Chaos.Networking/Converters/Server/StageLightingStateConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Server/StageLightingBoardConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Client/StageLightingInteractionConverter.cs", "Chaos-Server/Tests/Chaos.Tests/Networking/StageLightingPacketConverterTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/StageLightingPacketConverterTests/*\" --no-ansi", "acceptanceCriteria": ["state round-trips 0 and 8 lights", "board round-trips all types", "interaction round-trips every action; unknown throws", "opcodes 128/129/125 unique"], "modelTier": "mechanical"}
```

---

### Task 2: Server lighting core — setup, scenes, rate limit, lanterns

**Goal:** Plain, unit-tested server classes that hold and correct the live setup and the saved scenes.

**Files:**
- Create: `SRV/Chaos/Services/Theatre/StageLighting.cs`
- Create: `SRV/Chaos/Services/Theatre/TheatreLightingScenes.cs`
- Create: `SRV/Chaos/Services/Theatre/StageLightingRateLimiter.cs`
- Create: `SRV/Chaos/Services/Theatre/TheatreLanterns.cs`
- Test: `SRV/Tests/Chaos.Tests/Theatre/StageLightingTests.cs`, `TheatreLightingScenesTests.cs`, `StageLightingRateLimiterTests.cs`, `TheatreLanternsTests.cs`

**Acceptance Criteria:**
- [ ] New setup: house 100, glow on, no lights; house level held to 0–100
- [ ] Add picks the lowest free id 1–8 and refuses a 9th; unknown ids are ignored on update/remove
- [ ] Positions held to tile centres inside the stage; brightness 0–100; speeds 1–5; unknown enums corrected; Follow with no target becomes Still
- [ ] Motion start resets only on motion/speed/point/follow change; effect start only on effect/speed change
- [ ] `ToScene` saves a Follow light as Still at the followed person's position (or its own if unknown)
- [ ] Scenes: max 20, names 1–24 printable characters, case-insensitive replace/find/delete
- [ ] Rate limiter accepts 20 per second per player and refuses the 21st
- [ ] Lantern rules match the spec's table

**Verify:** `dotnet run --project SRV/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Theatre/*/*" --no-ansi` → all pass (new and existing `TheatreStageEffectsTests`)

**Steps:**

- [ ] **Step 1: Write the failing tests.** `SRV/Tests/Chaos.Tests/Theatre/StageLightingTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Networking.Entities.Server;
using Chaos.Services.Theatre;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Theatre;

public sealed class StageLightingTests
{
    private static readonly Rectangle Stage = new(0, 12, 9, 9);
    private DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private StageLighting Create() => new(Stage, () => Now);

    private static StageLightInfo Centre() => new() { X = 64, Y = 256, X2 = 96, Y2 = 256 };

    [Test]
    public void New_setup_is_fully_lit_with_glow_and_no_lights()
    {
        var lighting = Create();

        lighting.HouseLevel.Should().Be(100);
        lighting.StageGlow.Should().BeTrue();
        lighting.IsDark.Should().BeFalse();
        lighting.Lights.Should().BeEmpty();
    }

    [Test]
    [Arguments(150, 100)]
    [Arguments(-5, 0)]
    [Arguments(30, 30)]
    public void House_level_is_held_to_0_100(int requested, int expected)
    {
        var lighting = Create();
        lighting.SetHouseLevel(requested);

        lighting.HouseLevel.Should().Be((byte)expected);
        lighting.IsDark.Should().Be(expected < 100);
    }

    [Test]
    public void Add_uses_lowest_free_id_and_refuses_a_ninth()
    {
        var lighting = Create();

        for (var i = 1; i <= 8; i++)
            lighting.TryAddLight(Centre())!.Id.Should().Be((byte)i);

        lighting.TryAddLight(Centre()).Should().BeNull();

        lighting.TryRemoveLight(3).Should().BeTrue();
        lighting.TryAddLight(Centre())!.Id.Should().Be(3);
    }

    [Test]
    public void Add_ignores_the_requested_id()
        => Create().TryAddLight(Centre() with { Id = 7 })!.Id.Should().Be(1);

    [Test]
    public void Update_and_remove_of_unknown_id_are_ignored()
    {
        var lighting = Create();
        lighting.TryAddLight(Centre());

        lighting.TryUpdateLight(Centre() with { Id = 5, R = 1 }).Should().BeFalse();
        lighting.TryRemoveLight(5).Should().BeFalse();
        lighting.Lights.Should().ContainSingle().Which.R.Should().Be(255);
    }

    [Test]
    public void Positions_are_held_to_tile_centres_inside_the_stage()
    {
        var light = Create().TryAddLight(new StageLightInfo { X = 500, Y = 0, X2 = 0, Y2 = 9000 })!;

        light.X.Should().Be(128);   //tile 8
        light.Y.Should().Be(192);   //tile 12
        light.X2.Should().Be(0);    //tile 0
        light.Y2.Should().Be(320);  //tile 20
    }

    [Test]
    public void Brightness_speeds_and_enums_are_corrected()
    {
        var light = Create()
                    .TryAddLight(
                        Centre() with
                        {
                            Brightness = 250,
                            EffectSpeed = 0,
                            MotionSpeed = 9,
                            Size = (StageLightSize)77,
                            Effect = (StageLightEffect)77,
                            Motion = (StageLightMotion)77
                        })!;

        light.Brightness.Should().Be(100);
        light.EffectSpeed.Should().Be(1);
        light.MotionSpeed.Should().Be(5);
        light.Size.Should().Be(StageLightSize.Medium);
        light.Effect.Should().Be(StageLightEffect.None);
        light.Motion.Should().Be(StageLightMotion.Still);
    }

    [Test]
    public void Follow_without_a_target_becomes_still_and_other_motions_drop_the_target()
    {
        var lighting = Create();
        lighting.TryAddLight(Centre());

        lighting.TryUpdateLight(Centre() with { Id = 1, Motion = StageLightMotion.Follow, FollowId = 0 });
        lighting.Lights[0].Motion.Should().Be(StageLightMotion.Still);

        lighting.TryUpdateLight(Centre() with { Id = 1, Motion = StageLightMotion.Sweep, FollowId = 44 });
        lighting.Lights[0].FollowId.Should().Be(0u);
    }

    [Test]
    public void Motion_start_resets_on_motion_change_but_not_on_color_change()
    {
        var lighting = Create();
        lighting.TryAddLight(Centre() with { Motion = StageLightMotion.Sweep });

        Now = Now.AddSeconds(5);
        lighting.TryUpdateLight(Centre() with { Id = 1, Motion = StageLightMotion.Sweep, R = 10 });
        lighting.BuildState(0).Lights[0].MotionElapsedMs.Should().Be(5000u);

        Now = Now.AddSeconds(1);
        lighting.TryUpdateLight(Centre() with { Id = 1, Motion = StageLightMotion.Circle, R = 10 });
        Now = Now.AddSeconds(1);
        lighting.BuildState(0).Lights[0].MotionElapsedMs.Should().Be(1000u);
    }

    [Test]
    public void Effect_start_resets_on_effect_change_only()
    {
        var lighting = Create();
        lighting.TryAddLight(Centre() with { Effect = StageLightEffect.Pulse });

        Now = Now.AddSeconds(3);
        lighting.TryUpdateLight(Centre() with { Id = 1, Effect = StageLightEffect.Pulse, X = 80 });
        lighting.BuildState(0).Lights[0].EffectElapsedMs.Should().Be(3000u);

        lighting.TryUpdateLight(Centre() with { Id = 1, Effect = StageLightEffect.Flicker, X = 80 });
        Now = Now.AddSeconds(2);
        lighting.BuildState(0).Lights[0].EffectElapsedMs.Should().Be(2000u);
    }

    [Test]
    public void ToScene_saves_a_follow_light_as_still_where_the_person_stands()
    {
        var lighting = Create();
        lighting.TryAddLight(Centre());
        lighting.TryUpdateLight(Centre() with { Id = 1, Motion = StageLightMotion.Follow, FollowId = 44 });

        var scene = lighting.ToScene("Act 1", id => id == 44 ? ((ushort)32, (ushort)288) : null);

        scene.Lights.Should().ContainSingle();
        scene.Lights[0].Motion.Should().Be(StageLightMotion.Still);
        scene.Lights[0].FollowId.Should().Be(0u);
        scene.Lights[0].X.Should().Be(32);
        scene.Lights[0].Y.Should().Be(288);
    }

    [Test]
    public void ToScene_keeps_the_lights_own_spot_when_the_person_is_gone()
    {
        var lighting = Create();
        lighting.TryAddLight(Centre());
        lighting.TryUpdateLight(Centre() with { Id = 1, Motion = StageLightMotion.Follow, FollowId = 44 });

        var scene = lighting.ToScene("Act 1", _ => null);

        scene.Lights[0].X.Should().Be(64);
        scene.Lights[0].Y.Should().Be(256);
    }

    [Test]
    public void ApplyScene_replaces_house_glow_and_lights()
    {
        var lighting = Create();
        lighting.TryAddLight(Centre());
        lighting.TryAddLight(Centre());

        lighting.ApplyScene(
            new StageLightingScene
            {
                Name = "Dim",
                HouseLevel = 20,
                StageGlow = false,
                Lights = [Centre() with { R = 1 }]
            });

        lighting.HouseLevel.Should().Be(20);
        lighting.StageGlow.Should().BeFalse();
        lighting.Lights.Should().ContainSingle().Which.R.Should().Be(1);
    }

    [Test]
    public void BuildState_carries_stage_house_glow_and_fade()
    {
        var lighting = Create();
        lighting.SetHouseLevel(40);
        lighting.SetStageGlow(false);

        var state = lighting.BuildState(400);

        state.StageX.Should().Be(0);
        state.StageY.Should().Be(12);
        state.StageWidth.Should().Be(9);
        state.StageHeight.Should().Be(9);
        state.HouseLevel.Should().Be(40);
        state.StageGlow.Should().BeFalse();
        state.FadeMs.Should().Be(400);
    }
}
```

`SRV/Tests/Chaos.Tests/Theatre/TheatreLightingScenesTests.cs`:

```csharp
#region
using Chaos.Services.Theatre;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Theatre;

public sealed class TheatreLightingScenesTests
{
    private static StageLightingScene Scene(string name, byte house = 100) => new() { Name = name, HouseLevel = house };

    [Test]
    public void Save_adds_then_replaces_ignoring_case()
    {
        var scenes = new TheatreLightingScenes();

        scenes.Save(Scene("Finale", 10)).Should().Be(SceneSaveResult.Saved);
        scenes.Save(Scene("  FINALE ", 70)).Should().Be(SceneSaveResult.Saved);

        scenes.Scenes.Should().ContainSingle();
        scenes.Find("finale")!.HouseLevel.Should().Be(70);
        scenes.Names.Should().Equal("FINALE");
    }

    [Test]
    public void Save_refuses_a_21st_scene_but_still_replaces()
    {
        var scenes = new TheatreLightingScenes();

        for (var i = 0; i < 20; i++)
            scenes.Save(Scene($"Scene {i}")).Should().Be(SceneSaveResult.Saved);

        scenes.Save(Scene("One more")).Should().Be(SceneSaveResult.Full);
        scenes.Save(Scene("scene 3", 5)).Should().Be(SceneSaveResult.Saved);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("1234567890123456789012345")]
    [Arguments("bad\tname")]
    public void Save_refuses_bad_names(string name)
        => new TheatreLightingScenes().Save(Scene(name)).Should().Be(SceneSaveResult.InvalidName);

    [Test]
    public void Delete_removes_ignoring_case()
    {
        var scenes = new TheatreLightingScenes();
        scenes.Save(Scene("Act 1"));

        scenes.Delete("ACT 1").Should().BeTrue();
        scenes.Delete("Act 1").Should().BeFalse();
        scenes.Scenes.Should().BeEmpty();
    }
}
```

`SRV/Tests/Chaos.Tests/Theatre/StageLightingRateLimiterTests.cs`:

```csharp
#region
using Chaos.Services.Theatre;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Theatre;

public sealed class StageLightingRateLimiterTests
{
    private static readonly DateTime T0 = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Accepts_twenty_a_second_and_refuses_the_next()
    {
        var limiter = new StageLightingRateLimiter();

        for (var i = 0; i < 20; i++)
            limiter.TryAccept(1, T0.AddMilliseconds(i * 10)).Should().BeTrue();

        limiter.TryAccept(1, T0.AddMilliseconds(500)).Should().BeFalse();
        limiter.TryAccept(1, T0.AddMilliseconds(1000)).Should().BeTrue();
    }

    [Test]
    public void Players_are_counted_separately()
    {
        var limiter = new StageLightingRateLimiter();

        for (var i = 0; i < 20; i++)
            limiter.TryAccept(1, T0);

        limiter.TryAccept(2, T0).Should().BeTrue();
    }
}
```

`SRV/Tests/Chaos.Tests/Theatre/TheatreLanternsTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Services.Theatre;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Theatre;

public sealed class TheatreLanternsTests
{
    [Test]
    public void Nothing_changes_while_the_house_is_full()
        => TheatreLanterns.Desired(false, true, true, false, LanternSize.None).Should().BeNull();

    [Test]
    public void Director_and_admins_keep_their_lantern()
        => TheatreLanterns.Desired(true, false, false, true, LanternSize.Large).Should().BeNull();

    [Test]
    public void Audience_loses_its_lantern_when_dark()
        => TheatreLanterns.Desired(true, true, false, false, LanternSize.Large).Should().Be(LanternSize.None);

    [Test]
    public void Audience_without_a_lantern_is_left_alone()
        => TheatreLanterns.Desired(true, true, false, false, LanternSize.None).Should().BeNull();

    [Test]
    public void Stage_gets_a_small_lantern_while_glow_is_on()
        => TheatreLanterns.Desired(true, true, true, false, LanternSize.None).Should().Be(LanternSize.Small);

    [Test]
    public void Stage_gets_no_lantern_while_glow_is_off()
        => TheatreLanterns.Desired(true, false, true, false, LanternSize.Small).Should().Be(LanternSize.None);

    [Test]
    public void Matching_lantern_is_left_alone()
        => TheatreLanterns.Desired(true, true, true, false, LanternSize.Small).Should().BeNull();
}
```

- [ ] **Step 2: Run to confirm they fail to compile** (types missing).

Run: `dotnet build C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server/Tests/Chaos.Tests/Chaos.Tests.csproj`
Expected: errors naming `StageLighting`, `TheatreLightingScenes`, `StageLightingRateLimiter`, `TheatreLanterns`.

- [ ] **Step 3: Create `SRV/Chaos/Services/Theatre/StageLighting.cs`**

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Services.Theatre;

/// <summary>
///     The Theatre's live lighting setup: house level, stage glow and up to <see cref="MAX_LIGHTS" /> spotlights. Every
///     edit checks and corrects its input first. No network or file work happens here.
/// </summary>
public sealed class StageLighting
{
    public const int MAX_LIGHTS = 8;
    public const byte FULL_HOUSE = 100;
    public const byte MIN_SPEED = 1;
    public const byte MAX_SPEED = 5;

    private readonly List<StageLightInfo> LightList = [];
    private readonly Dictionary<byte, DateTime> MotionStarted = [];
    private readonly Dictionary<byte, DateTime> EffectStarted = [];
    private readonly Func<DateTime> UtcNow;

    public StageLighting(Rectangle stage, Func<DateTime>? utcNow = null)
    {
        Stage = stage;
        UtcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public Rectangle Stage { get; }
    public byte HouseLevel { get; private set; } = FULL_HOUSE;
    public bool StageGlow { get; private set; } = true;

    /// <summary>True below 100%. The Theatre map carries the Darkness flag exactly while this is true.</summary>
    public bool IsDark => HouseLevel < FULL_HOUSE;

    public IReadOnlyList<StageLightInfo> Lights => LightList;

    public void SetHouseLevel(int level) => HouseLevel = (byte)Math.Clamp(level, 0, FULL_HOUSE);

    public void SetStageGlow(bool on) => StageGlow = on;

    /// <summary>Adds a light under the lowest free id (1-8). Returns the stored light, or null when the stage is full.</summary>
    public StageLightInfo? TryAddLight(StageLightInfo requested)
    {
        if (LightList.Count >= MAX_LIGHTS)
            return null;

        byte id = 1;

        while (LightList.Any(l => l.Id == id))
            id++;

        var light = Sanitize(requested with { Id = id }, Stage);
        LightList.Add(light);

        var now = UtcNow();
        MotionStarted[id] = now;
        EffectStarted[id] = now;

        return light;
    }

    /// <summary>Replaces a light's settings. False when no light has that id.</summary>
    public bool TryUpdateLight(StageLightInfo requested)
    {
        var index = LightList.FindIndex(l => l.Id == requested.Id);

        if (index < 0)
            return false;

        var old = LightList[index];
        var light = Sanitize(requested, Stage);
        var now = UtcNow();

        //a colour, size or beam change leaves both clocks alone, so a moving light doesn't jump
        if (MotionChanged(old, light))
            MotionStarted[light.Id] = now;

        if ((old.Effect != light.Effect) || (old.EffectSpeed != light.EffectSpeed))
            EffectStarted[light.Id] = now;

        LightList[index] = light;

        return true;
    }

    public bool TryRemoveLight(byte id)
    {
        MotionStarted.Remove(id);
        EffectStarted.Remove(id);

        return LightList.RemoveAll(l => l.Id == id) > 0;
    }

    public void ClearLights()
    {
        LightList.Clear();
        MotionStarted.Clear();
        EffectStarted.Clear();
    }

    /// <summary>
    ///     The current setup as a scene. A person's id means nothing after they log out, so a Follow light is saved as a
    ///     Still light at <paramref name="followPosition" />'s answer for that person, or at its own spot when that is null.
    /// </summary>
    public StageLightingScene ToScene(string name, Func<uint, (ushort X, ushort Y)?> followPosition)
        => new()
        {
            Name = name,
            HouseLevel = HouseLevel,
            StageGlow = StageGlow,
            Lights = LightList.Select(
                                  light =>
                                  {
                                      if (light.Motion != StageLightMotion.Follow)
                                          return light with { };

                                      var (x, y) = followPosition(light.FollowId) ?? (light.X, light.Y);

                                      return light with
                                      {
                                          Motion = StageLightMotion.Still,
                                          FollowId = 0,
                                          X = x,
                                          Y = y
                                      };
                                  })
                              .ToList()
        };

    /// <summary>Replaces house level, glow and every light with the scene's. Lights get fresh ids 1..n.</summary>
    public void ApplyScene(StageLightingScene scene)
    {
        SetHouseLevel(scene.HouseLevel);
        StageGlow = scene.StageGlow;
        ClearLights();

        foreach (var light in scene.Lights.Take(MAX_LIGHTS))
            TryAddLight(light);
    }

    /// <summary>The whole setup as a message, with each light's elapsed motion and effect time.</summary>
    public StageLightingStateArgs BuildState(ushort fadeMs)
    {
        var now = UtcNow();

        return new StageLightingStateArgs
        {
            StageX = (byte)Stage.Left,
            StageY = (byte)Stage.Top,
            StageWidth = (byte)Stage.Width,
            StageHeight = (byte)Stage.Height,
            HouseLevel = HouseLevel,
            StageGlow = StageGlow,
            FadeMs = fadeMs,
            Lights = LightList.Select(
                                  light => new StageLightState
                                  {
                                      Light = light,
                                      MotionElapsedMs = ElapsedMs(MotionStarted, light.Id, now),
                                      EffectElapsedMs = ElapsedMs(EffectStarted, light.Id, now)
                                  })
                              .ToList()
        };
    }

    /// <summary>Holds a position (sixteenths of a tile) to the tile centres inside the stage.</summary>
    public static (ushort X, ushort Y) ClampToStage(int x, int y, Rectangle stage)
        => ((ushort)Math.Clamp(x, stage.Left * StageLightInfo.UNITS_PER_TILE, stage.Right * StageLightInfo.UNITS_PER_TILE),
            (ushort)Math.Clamp(y, stage.Top * StageLightInfo.UNITS_PER_TILE, stage.Bottom * StageLightInfo.UNITS_PER_TILE));

    /// <summary>Corrects every field: positions onto the stage, values into range, unknown enums to their defaults.</summary>
    public static StageLightInfo Sanitize(StageLightInfo light, Rectangle stage)
    {
        var (x, y) = ClampToStage(light.X, light.Y, stage);
        var (x2, y2) = ClampToStage(light.X2, light.Y2, stage);
        var motion = Enum.IsDefined(light.Motion) ? light.Motion : StageLightMotion.Still;

        if ((motion == StageLightMotion.Follow) && (light.FollowId == 0))
            motion = StageLightMotion.Still;

        return light with
        {
            X = x,
            Y = y,
            X2 = x2,
            Y2 = y2,
            Size = Enum.IsDefined(light.Size) ? light.Size : StageLightSize.Medium,
            Brightness = Math.Min(light.Brightness, FULL_HOUSE),
            Effect = Enum.IsDefined(light.Effect) ? light.Effect : StageLightEffect.None,
            EffectSpeed = Math.Clamp(light.EffectSpeed, MIN_SPEED, MAX_SPEED),
            Motion = motion,
            MotionSpeed = Math.Clamp(light.MotionSpeed, MIN_SPEED, MAX_SPEED),
            FollowId = motion == StageLightMotion.Follow ? light.FollowId : 0
        };
    }

    private static uint ElapsedMs(Dictionary<byte, DateTime> started, byte id, DateTime now)
        => started.TryGetValue(id, out var at) ? (uint)Math.Clamp((now - at).TotalMilliseconds, 0, uint.MaxValue) : 0;

    private static bool MotionChanged(StageLightInfo a, StageLightInfo b)
        => (a.Motion != b.Motion)
           || (a.MotionSpeed != b.MotionSpeed)
           || (a.X != b.X)
           || (a.Y != b.Y)
           || (a.X2 != b.X2)
           || (a.Y2 != b.Y2)
           || (a.FollowId != b.FollowId);
}
```

- [ ] **Step 4: Create `SRV/Chaos/Services/Theatre/TheatreLightingScenes.cs`**

```csharp
#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Services.Theatre;

/// <summary>A saved lighting setup: house level, stage glow and the lights.</summary>
public sealed class StageLightingScene
{
    public string Name { get; set; } = string.Empty;
    public byte HouseLevel { get; set; } = StageLighting.FULL_HOUSE;
    public bool StageGlow { get; set; } = true;
    public List<StageLightInfo> Lights { get; set; } = [];
}

public enum SceneSaveResult
{
    Saved,
    InvalidName,
    Full
}

/// <summary>
///     The Theatre's saved lighting scenes, shared by every director and admin. Stored through <c>IStorage&lt;T&gt;</c> as
///     <c>TheatreLightingScenes.json</c>. Names are unique ignoring case; saving an existing name replaces it.
/// </summary>
public sealed class TheatreLightingScenes
{
    public const int MAX_SCENES = 20;
    public const int MAX_NAME_LENGTH = 24;

    public List<StageLightingScene> Scenes { get; set; } = [];

    public IReadOnlyList<string> Names => Scenes.Select(scene => scene.Name).ToList();

    public static bool IsValidName(string? name)
        => !string.IsNullOrWhiteSpace(name) && (name.Trim().Length <= MAX_NAME_LENGTH) && !name.Any(char.IsControl);

    public StageLightingScene? Find(string name)
        => Scenes.FirstOrDefault(scene => scene.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    public SceneSaveResult Save(StageLightingScene scene)
    {
        if (!IsValidName(scene.Name))
            return SceneSaveResult.InvalidName;

        scene.Name = scene.Name.Trim();
        var index = Scenes.FindIndex(existing => existing.Name.Equals(scene.Name, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            Scenes[index] = scene;

            return SceneSaveResult.Saved;
        }

        if (Scenes.Count >= MAX_SCENES)
            return SceneSaveResult.Full;

        Scenes.Add(scene);

        return SceneSaveResult.Saved;
    }

    public bool Delete(string name)
        => Scenes.RemoveAll(scene => scene.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) > 0;
}
```

- [ ] **Step 5: Create `SRV/Chaos/Services/Theatre/StageLightingRateLimiter.cs`**

```csharp
namespace Chaos.Services.Theatre;

/// <summary>Drops lighting edits from one player beyond <see cref="MAX_PER_SECOND" /> in any one-second window.</summary>
public sealed class StageLightingRateLimiter
{
    public const int MAX_PER_SECOND = 20;

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);
    private readonly Dictionary<uint, Queue<DateTime>> Recent = [];

    public bool TryAccept(uint playerId, DateTime now)
    {
        if (!Recent.TryGetValue(playerId, out var times))
            Recent[playerId] = times = new Queue<DateTime>();

        while ((times.Count > 0) && ((now - times.Peek()) >= Window))
            times.Dequeue();

        if (times.Count >= MAX_PER_SECOND)
            return false;

        times.Enqueue(now);

        return true;
    }

    public void Forget(uint playerId) => Recent.Remove(playerId);
}
```

- [ ] **Step 6: Create `SRV/Chaos/Services/Theatre/TheatreLanterns.cs`**

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Services.Theatre;

/// <summary>Which lantern a player in the Theatre should have.</summary>
public static class TheatreLanterns
{
    /// <summary>
    ///     The lantern to set, or null to leave the player's lantern alone. While the house lights are below 100%, the
    ///     audience has none and people on the stage get a small one only while stage glow is on. The director and
    ///     admins (<paramref name="exempt" />) keep whatever they have.
    /// </summary>
    public static LanternSize? Desired(bool isDark, bool stageGlow, bool onStage, bool exempt, LanternSize current)
    {
        if (!isDark || exempt)
            return null;

        var wanted = onStage && stageGlow ? LanternSize.Small : LanternSize.None;

        return wanted == current ? null : wanted;
    }
}
```

- [ ] **Step 7: Run the Theatre tests**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Theatre/*/*" --no-ansi`
Expected: every test passes, including the existing `TheatreStageEffectsTests`.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/Theatre/StageLighting.cs", "Chaos-Server/Chaos/Services/Theatre/TheatreLightingScenes.cs", "Chaos-Server/Chaos/Services/Theatre/StageLightingRateLimiter.cs", "Chaos-Server/Chaos/Services/Theatre/TheatreLanterns.cs", "Chaos-Server/Tests/Chaos.Tests/Theatre/StageLightingTests.cs", "Chaos-Server/Tests/Chaos.Tests/Theatre/TheatreLightingScenesTests.cs", "Chaos-Server/Tests/Chaos.Tests/Theatre/StageLightingRateLimiterTests.cs", "Chaos-Server/Tests/Chaos.Tests/Theatre/TheatreLanternsTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.Theatre/*/*\" --no-ansi", "acceptanceCriteria": ["defaults and house clamp", "ids and 8-light limit", "sanitising", "clock resets", "ToScene follow handling", "scene limits", "rate limit", "lantern rules"], "modelTier": "standard"}
```

---

### Task 3: Server wiring — map script, sends, handler, dialog, Unora data

**Goal:** The Theatre map owns the setup, applies edits, keeps darkness and lanterns in step, and broadcasts; the dialog opens the window.

**Files:**
- Modify: `SRV/Chaos/Networking/Abstractions/IChaosWorldClient.cs` (after `SendBeautyShopClose`)
- Modify: `SRV/Chaos/Networking/ChaosWorldClient.cs` (after `SendBeautyShopClose`)
- Modify: `SRV/Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs` (whole class)
- Create: `SRV/Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageLightingOpenScript.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs` (`HandleLightsOff`, `HandleLightsOn`)
- Modify: `SRV/Chaos/Services/Servers/WorldServer.cs` (new handler after `OnBugReportInteraction`; registration in the `ClientHandlers` block)
- Modify: `UNO/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_options.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stagelighting.json`

**Acceptance Criteria:**
- [ ] Only the director or an admin can edit; a refused player gets Close + "Only the director can change the stage lights."
- [ ] Edits beyond 20/s per player are dropped silently
- [ ] The setup goes to each player who enters, and to everyone after each accepted edit
- [ ] The Darkness flag is set exactly while the house level is below 100%; crossing 100% refreshes everyone, then broadcasts
- [ ] Lanterns follow `TheatreLanterns` every update and immediately when darkness flips
- [ ] A follow target must be an Aisling on the stage when first set
- [ ] Scenes save/load/delete through `IStorage<TheatreLightingScenes>`; the scene list goes to every director/admin on the map
- [ ] "Turn Lights Off/On" set 0% / 100% through the map script with a 300 ms fade and keep their messages
- [ ] Thulin's Theatre Options lists "Stage Lighting", which opens the window

**Verify:** `dotnet build C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server/Chaos.slnx` → `Build succeeded`; Theatre tests still pass.

**Steps:**

- [ ] **Step 1: Add the two send methods.** In `IChaosWorldClient.cs`, after `void SendBeautyShopClose();`:

```csharp

    /// <summary>Sends the Theatre's whole lighting setup (stage, house level, glow, fade and spotlights).</summary>
    void SendStageLightingState(StageLightingStateArgs args);

    /// <summary>Opens, updates the scene list of, or closes the director's Stage Lighting window.</summary>
    void SendStageLightingBoard(StageLightingBoardArgs args);
```

In `ChaosWorldClient.cs`, after `public void SendBeautyShopClose() => ...;`:

```csharp

    /// <inheritdoc />
    public void SendStageLightingState(StageLightingStateArgs args) => Send(args);

    /// <inheritdoc />
    public void SendStageLightingBoard(StageLightingBoardArgs args) => Send(args);
```

Both files already import `Chaos.Networking.Entities.Server` (they use `BeautyShopDisplayArgs`).

- [ ] **Step 2: Replace `SuomiTheatreMapScript`** (whole file) with:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Definitions;
using Chaos.Extensions.Geometry;
using Chaos.Geometry;
using Chaos.Models.World;
using Chaos.Models.World.Abstractions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Scripting.MapScripts.Abstractions;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.Theatre;
using Chaos.Storage.Abstractions;
using Chaos.Time;
using Chaos.Time.Abstractions;
#endregion

namespace Chaos.Scripting.MapScripts.Temuair.Suomi;

/// <summary>
///     The Garamonde Theatre. Owns the live stage lighting (<see cref="Lighting" />): applies the director's edits, keeps
///     the map's Darkness flag and everyone's lanterns in step with the house level, and sends the whole setup to
///     everyone on the map after each change. Also enforces room silence.
/// </summary>
public class SuomiTheatreMapScript : MapScriptBase
{
    public const int UPDATE_INTERVAL_MS = 1;

    //fade times the clients blend over (see the spec's Fades table)
    public const ushort FADE_NONE = 0;
    public const ushort FADE_EDIT = 150;
    public const ushort FADE_SWITCH = 300;
    public const ushort FADE_HOUSE = 400;
    public const ushort FADE_SCENE = 1000;

    private readonly IEffectFactory EffectFactory;
    private readonly StageLightingRateLimiter RateLimiter = new();
    private readonly IStorage<TheatreLightingScenes> SceneStorage;

    public readonly Rectangle StageRectangle = new(
        0,
        12,
        9,
        9);

    private readonly IIntervalTimer UpdateTimer;

    public bool IsSilenceEnabled { get; set; }

    /// <summary>The live lighting setup. Resets to fully lit with no lights when the server restarts.</summary>
    public StageLighting Lighting { get; }

    public SuomiTheatreMapScript(MapInstance subject, IEffectFactory effectFactory, IStorage<TheatreLightingScenes> sceneStorage)
        : base(subject)
    {
        EffectFactory = effectFactory;
        SceneStorage = sceneStorage;
        UpdateTimer = new IntervalTimer(TimeSpan.FromSeconds(UPDATE_INTERVAL_MS));
        Lighting = new StageLighting(StageRectangle);
    }

    /// <summary>The director and admins may run the lights.</summary>
    public static bool CanDirect(Aisling aisling) => aisling.IsAdmin || aisling.Trackers.Enums.HasValue(TheatreRoles.Director);

    public override void OnEntered(Creature creature)
    {
        //map info went out before this call, so the client has already cleared the previous map's lights
        if (creature is Aisling aisling)
            aisling.Client.SendStageLightingState(Lighting.BuildState(FADE_NONE));
    }

    public override void OnExited(Creature creature)
    {
        if (creature is Aisling aisling)
            RateLimiter.Forget(aisling.Id);
    }

    /// <summary>Opens the Stage Lighting window for <paramref name="source" /> with the saved scene names.</summary>
    public void OpenBoard(Aisling source)
        => source.Client.SendStageLightingBoard(
            new StageLightingBoardArgs
            {
                Type = StageLightingBoardType.Open,
                SceneNames = SceneStorage.Value.Names
            });

    /// <summary>Sets the house level (window, Blackout, or the dialog's lights off/on) and tells everyone.</summary>
    public void SetHouseLevel(int level, ushort fadeMs)
    {
        Lighting.SetHouseLevel(level);
        SyncDarknessAndBroadcast(fadeMs);
    }

    /// <summary>
    ///     Applies one edit from the Stage Lighting window. Checks the sender's role and rate first; every value is
    ///     corrected by <see cref="StageLighting" />.
    /// </summary>
    public void HandleLightingEdit(Aisling source, StageLightingInteractionArgs args)
    {
        if (!CanDirect(source))
        {
            source.Client.SendStageLightingBoard(new StageLightingBoardArgs { Type = StageLightingBoardType.Close });
            source.SendOrangeBarMessage("Only the director can change the stage lights.");

            return;
        }

        if (!RateLimiter.TryAccept(source.Id, DateTime.UtcNow))
            return;

        switch (args.Action)
        {
            case StageLightingAction.SetHouseLevel:
                SetHouseLevel(args.Level, FADE_HOUSE);

                break;

            case StageLightingAction.Blackout:
                SetHouseLevel(0, FADE_SWITCH);

                break;

            case StageLightingAction.SetStageGlow:
                Lighting.SetStageGlow(args.Flag);
                ApplyLanterns();
                Broadcast(FADE_HOUSE);

                break;

            case StageLightingAction.AddLight:
            {
                //a new light never follows anyone; the director picks a person afterwards
                var requested = args.Light with
                {
                    Motion = args.Light.Motion == StageLightMotion.Follow ? StageLightMotion.Still : args.Light.Motion,
                    FollowId = 0
                };

                if (Lighting.TryAddLight(requested) is null)
                    source.SendOrangeBarMessage($"The stage already has {StageLighting.MAX_LIGHTS} lights.");
                else
                    Broadcast(FADE_EDIT);

                break;
            }

            case StageLightingAction.UpdateLight:
                if (!IsValidFollow(args.Light))
                {
                    source.SendOrangeBarMessage("That person isn't on the stage.");

                    break;
                }

                if (Lighting.TryUpdateLight(args.Light))
                    Broadcast(FADE_EDIT);

                break;

            case StageLightingAction.RemoveLight:
                if (Lighting.TryRemoveLight(args.LightId))
                    Broadcast(FADE_EDIT);

                break;

            case StageLightingAction.ClearLights:
                Lighting.ClearLights();
                Broadcast(FADE_EDIT);

                break;

            case StageLightingAction.SaveScene:
                SaveScene(source, args.SceneName);

                break;

            case StageLightingAction.LoadScene:
                LoadScene(source, args.SceneName);

                break;

            case StageLightingAction.DeleteScene:
                DeleteScene(source, args.SceneName);

                break;
        }
    }

    public override void Update(TimeSpan delta)
    {
        UpdateTimer.Update(delta);

        if (!UpdateTimer.IntervalElapsed)
            return;

        ApplyLanterns();

        if (IsSilenceEnabled)
        {
            var aislingsOnMap = Subject.GetEntities<Aisling>()
                                       .Where(x => !x.IsAdmin && !x.Trackers.Enums.HasValue(TheatreRoles.Director))
                                       .ToList();

            foreach (var player in aislingsOnMap)
                if (!player.Effects.Contains("Theatre Silence") && !StageRectangle.ContainsPoint(player))
                {
                    var silence = EffectFactory.Create("theatresilence");
                    player.Effects.Apply(player, silence);
                }
        }
    }

    /// <summary>Sets each player's lantern from <see cref="TheatreLanterns" />, only where it differs.</summary>
    private void ApplyLanterns()
    {
        foreach (var player in Subject.GetEntities<Aisling>()
                                      .ToList())
        {
            var wanted = TheatreLanterns.Desired(
                Lighting.IsDark,
                Lighting.StageGlow,
                StageRectangle.ContainsPoint(player),
                CanDirect(player),
                player.LanternSize);

            if (wanted is { } size)
                player.SetLanternSize(size);
        }
    }

    private void Broadcast(ushort fadeMs)
    {
        var state = Lighting.BuildState(fadeMs);

        foreach (var aisling in Subject.GetEntities<Aisling>())
            aisling.Client.SendStageLightingState(state);
    }

    /// <summary>A newly chosen follow target must be an Aisling standing on the stage. Keeping the same target is always fine.</summary>
    private bool IsValidFollow(StageLightInfo requested)
    {
        if ((requested.Motion != StageLightMotion.Follow) || (requested.FollowId == 0))
            return true;

        var existing = Lighting.Lights.FirstOrDefault(l => l.Id == requested.Id);

        if (existing is not null && (existing.FollowId == requested.FollowId))
            return true;

        return Subject.TryGetEntity<Aisling>(requested.FollowId, out var target) && StageRectangle.ContainsPoint(target);
    }

    private (ushort X, ushort Y)? FollowPosition(uint id)
        => Subject.TryGetEntity<Aisling>(id, out var target)
            ? StageLighting.ClampToStage(target.X * StageLightInfo.UNITS_PER_TILE, target.Y * StageLightInfo.UNITS_PER_TILE, StageRectangle)
            : null;

    private void SaveScene(Aisling source, string name)
    {
        var scene = Lighting.ToScene(name, FollowPosition);

        switch (SceneStorage.Value.Save(scene))
        {
            case SceneSaveResult.InvalidName:
                source.SendOrangeBarMessage($"Scene names are 1 to {TheatreLightingScenes.MAX_NAME_LENGTH} characters.");

                return;

            case SceneSaveResult.Full:
                source.SendOrangeBarMessage($"The Theatre can only keep {TheatreLightingScenes.MAX_SCENES} scenes.");

                return;
        }

        SceneStorage.Save();
        source.SendOrangeBarMessage($"Saved the scene \"{scene.Name}\".");
        SendSceneList();
    }

    private void LoadScene(Aisling source, string name)
    {
        var scene = SceneStorage.Value.Find(name);

        if (scene is null)
        {
            source.SendOrangeBarMessage($"There's no scene called {name}.");

            return;
        }

        Lighting.ApplyScene(scene);
        SyncDarknessAndBroadcast(FADE_SCENE);
    }

    private void DeleteScene(Aisling source, string name)
    {
        if (!SceneStorage.Value.Delete(name))
        {
            source.SendOrangeBarMessage($"There's no scene called {name}.");

            return;
        }

        SceneStorage.Save();
        SendSceneList();
    }

    private void SendSceneList()
    {
        var args = new StageLightingBoardArgs
        {
            Type = StageLightingBoardType.Scenes,
            SceneNames = SceneStorage.Value.Names
        };

        foreach (var aisling in Subject.GetEntities<Aisling>()
                                       .Where(CanDirect))
            aisling.Client.SendStageLightingBoard(args);
    }

    /// <summary>
    ///     Keeps the map's Darkness flag equal to "house below 100%". When it flips, everyone gets a forced refresh (map
    ///     info carries the flag) and their lanterns are fixed at once. The setup always goes out after, so it arrives
    ///     behind the refreshed map info.
    /// </summary>
    private void SyncDarknessAndBroadcast(ushort fadeMs)
    {
        var mapIsDark = (Subject.Flags & MapFlags.Darkness) == MapFlags.Darkness;

        if (Lighting.IsDark != mapIsDark)
        {
            if (Lighting.IsDark)
                Subject.Flags |= MapFlags.Darkness;
            else
                Subject.Flags &= ~MapFlags.Darkness;

            foreach (var aisling in Subject.GetEntities<Aisling>())
                aisling.Refresh(true);

            ApplyLanterns();
        }

        Broadcast(fadeMs);
    }
}
```

Check before building: if `Creature` is not in `Chaos.Models.World.Abstractions` or `Chaos.Models.World`, fix the using to wherever `MapScriptBase.OnEntered(Creature creature)` gets it (look at `MapScriptBase`'s usings). Remove any using the compiler reports as unused.

- [ ] **Step 3: Create `TheatreStageLightingOpenScript.cs`**

```csharp
#region
using Chaos.Extensions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Scripting.MapScripts.Temuair.Suomi;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.Suomi;

/// <summary>
///     On the <c>suomitheatre_stagelighting</c> dialog: closes the dialog before it is shown and opens the Stage Lighting
///     window instead. Only the director and admins get the window.
/// </summary>
public class TheatreStageLightingOpenScript(Dialog subject) : DialogScriptBase(subject)
{
    public override void OnDisplaying(Aisling source)
    {
        Subject.Close(source);

        if (!source.MapInstance.Script.Is<SuomiTheatreMapScript>(out var theatre))
        {
            source.SendOrangeBarMessage("You cannot do this here.");

            return;
        }

        if (!SuomiTheatreMapScript.CanDirect(source))
        {
            source.SendOrangeBarMessage("Only the director can change the stage lights.");

            return;
        }

        theatre.OpenBoard(source);
    }
}
```

Its script key is `theatreStageLightingOpen` (`ScriptBase.GetScriptKey` strips "Script").

- [ ] **Step 4: Route "Turn Lights Off/On" through the map script.** In `SuomiTheatreScript.cs`, replace the body of `HandleLightsOff` with:

```csharp
    private void HandleLightsOff(Aisling source, byte? optionIndex)
    {
        if (optionIndex == 1)
        {
            if (!source.MapInstance.LoadedFromInstanceId.EqualsI("suomi_theatre"))
            {
                Subject.Reply(source, "You cannot do this here.");

                return;
            }

            if (!source.MapInstance.Script.Is<SuomiTheatreMapScript>(out var theatreScript))
            {
                Subject.Reply(source, "The theatre script wasn’t found.");

                return;
            }

            //the map script flips darkness, fixes lanterns and tells every client
            theatreScript.SetHouseLevel(0, SuomiTheatreMapScript.FADE_SWITCH);

            foreach (var aisling in source.MapInstance.GetEntities<Aisling>())
                aisling.SendOrangeBarMessage($"{source.Name} turned off the lights.");

            Subject.Reply(source, "You turn off the lights.");
        }

        Subject.Close(source);
    }
```

and `HandleLightsOn` with:

```csharp
    private void HandleLightsOn(Aisling source, byte? optionIndex)
    {
        if (optionIndex == 1)
        {
            if (!source.MapInstance.LoadedFromInstanceId.EqualsI("suomi_theatre"))
            {
                Subject.Reply(source, "You cannot do this here.");

                return;
            }

            if (!source.MapInstance.Script.Is<SuomiTheatreMapScript>(out var theatreScript))
            {
                Subject.Reply(source, "The theatre script wasn’t found.");

                return;
            }

            theatreScript.SetHouseLevel(100, SuomiTheatreMapScript.FADE_SWITCH);

            foreach (var aisling in source.MapInstance.GetEntities<Aisling>())
                aisling.SendOrangeBarMessage($"{source.Name} turned on the lights.");

            Subject.Reply(source, "The lights turn on.");
        }

        Subject.Close(source);
    }
```

Use Serena `replace_symbol_body` for both. Keep the curly apostrophe in "wasn’t" as it is today.

- [ ] **Step 5: Add the world-server handler.** In `WorldServer.cs`, directly after the `OnBugReportInteraction` method:

```csharp

    /// <summary>
    ///     Routes a Stage Lighting edit to the Theatre's map script, which checks the sender's role and rate and corrects
    ///     every value. A player who isn't in the Theatre gets the window closed.
    /// </summary>
    public ValueTask OnStageLightingInteraction(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<StageLightingInteractionArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnStageLightingInteraction);

        static ValueTask InnerOnStageLightingInteraction(IChaosWorldClient localClient, StageLightingInteractionArgs localArgs)
        {
            if (!localClient.Connected)
                return default;

            var aisling = localClient.Aisling;

            if (!aisling.MapInstance.Script.Is<SuomiTheatreMapScript>(out var theatre))
            {
                localClient.SendStageLightingBoard(new StageLightingBoardArgs { Type = StageLightingBoardType.Close });

                return default;
            }

            theatre.HandleLightingEdit(aisling, localArgs);

            return default;
        }
    }
```

Register it after `ClientHandlers[(byte)ClientOpCode.BugReportInteraction] = OnBugReportInteraction;`:

```csharp
        ClientHandlers[(byte)ClientOpCode.StageLightingInteraction] = OnStageLightingInteraction;
```

Add `using Chaos.Scripting.MapScripts.Temuair.Suomi;` to `WorldServer.cs` if it is not already there.

- [ ] **Step 6: Unora dialog data.** In `UNO/.../thulin/suomitheatre_options.json`, add this option after the "Stage Effects" option (keep 2-space JSON formatting):

```json
    {
      "dialogKey": "suomitheatre_stagelighting",
      "optionText": "Stage Lighting"
    }
```

Create `UNO/.../thulin/suomitheatre_stagelighting.json`:

```json
{
  "options": [],
  "scriptKeys": [
    "theatreStageLightingOpen"
  ],
  "scriptVars": {},
  "templateKey": "suomitheatre_stagelighting",
  "text": "The lighting board is yours.",
  "type": "Normal"
}
```

Check the new file's line endings and trailing newline match its neighbours (`git -C UNO diff --stat` shows only these two files).

- [ ] **Step 7: Build and re-run the Theatre tests**

Run: `dotnet build C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server/Chaos.slnx`
Expected: `Build succeeded`, no new warnings in the touched files.
Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Theatre/*/*" --no-ansi`
Expected: all pass.

```json:metadata
{"files": ["Chaos-Server/Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos-Server/Chaos/Networking/ChaosWorldClient.cs", "Chaos-Server/Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageLightingOpenScript.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs", "Chaos-Server/Chaos/Services/Servers/WorldServer.cs", "Unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_options.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stagelighting.json"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server/Chaos.slnx", "acceptanceCriteria": ["role check + close", "rate limit", "send on enter + broadcast", "darkness flag sync with refresh", "lantern rules applied", "follow target validation", "scenes via IStorage", "lights off/on via map script", "Stage Lighting dialog option"], "modelTier": "standard"}
```

---

### Task 4: Client networking — events and send

**Goal:** The client raises events for the two new server messages and can send an edit.

**Files:**
- Modify: `CLI/Chaos.Client.Networking/ConnectionManager.cs` (events after `OnBugReportOpen`; handler registration after `BugReportOpen`; handlers after `HandleBugReportOpen`; send after `SendBugReportCancel`)
- Modify: `CLI/Chaos.Client.Networking/Definitions/Delegates.cs` (after `BugReportOpenHandler`)

**Acceptance Criteria:**
- [ ] `OnStageLightingState` and `OnStageLightingBoard` fire with the deserialized args
- [ ] `SendStageLightingInteraction(args)` sends while in the world

**Verify:** `dotnet build CLI/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server` → `Build succeeded`

**Steps:**

- [ ] **Step 1: Delegates** — after `public delegate void BugReportOpenHandler(BugReportOpenArgs args);`:

```csharp

/// <summary>
///     Fired when the Theatre's lighting setup arrives.
/// </summary>
public delegate void StageLightingStateHandler(StageLightingStateArgs args);

/// <summary>
///     Fired when the server opens, updates or closes the Stage Lighting window.
/// </summary>
public delegate void StageLightingBoardHandler(StageLightingBoardArgs args);
```

- [ ] **Step 2: Events** — after `public event BugReportOpenHandler? OnBugReportOpen;`:

```csharp

    /// <summary>
    ///     Fired when the Theatre's lighting setup arrives (on entering the Theatre and after each change).
    /// </summary>
    public event StageLightingStateHandler? OnStageLightingState;

    /// <summary>
    ///     Fired when the server opens, updates the scene list of, or closes the Stage Lighting window.
    /// </summary>
    public event StageLightingBoardHandler? OnStageLightingBoard;
```

- [ ] **Step 3: Registration** — after `PacketHandlers[(byte)ServerOpCode.BugReportOpen] = HandleBugReportOpen;`:

```csharp
        PacketHandlers[(byte)ServerOpCode.StageLightingState] = HandleStageLightingState;
        PacketHandlers[(byte)ServerOpCode.StageLightingBoard] = HandleStageLightingBoard;
```

- [ ] **Step 4: Handlers** — after the `HandleBugReportOpen` method:

```csharp

    private void HandleStageLightingState(ServerPacket pkt)
    {
        var args = Client.Deserialize<StageLightingStateArgs>(in pkt);
        OnStageLightingState?.Invoke(args);
    }

    private void HandleStageLightingBoard(ServerPacket pkt)
    {
        var args = Client.Deserialize<StageLightingBoardArgs>(in pkt);
        OnStageLightingBoard?.Invoke(args);
    }
```

- [ ] **Step 5: Send** — after the `SendBugReportCancel` method:

```csharp

    /// <summary>Sends one Stage Lighting edit. The server checks the sender's role and every value.</summary>
    public void SendStageLightingInteraction(StageLightingInteractionArgs args) => SendIfWorld(args);
```

- [ ] **Step 6: Build** (command in **Verify**). Expected: `Build succeeded`.

```json:metadata
{"files": ["Chaos.Client.Networking/ConnectionManager.cs", "Chaos.Client.Networking/Definitions/Delegates.cs"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/spotlights-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server", "acceptanceCriteria": ["two events raised", "send method"], "modelTier": "mechanical"}
```

---

### Task 5: StageLightAnimator and HsvColor (client math)

**Goal:** Given the last setup and a time, compute every light's tile, color and strength, including motion, effects, fades and held (dragged) lights.

**Files:**
- Create: `CLI/Chaos.Client/Utilities/HsvColor.cs`
- Create: `CLI/Chaos.Client/Systems/StageLightAnimator.cs`
- Test: `CLI/Tests/Chaos.Client.Tests/StageLightAnimatorTests.cs`

**Acceptance Criteria:**
- [ ] Sweep: A at 0, B at half period, eased midpoint at a quarter, A again at a full period
- [ ] Circle keeps its radius; Follow uses the person's tile held to the stage, or the light's own spot when unseen
- [ ] Pulse runs 0.30–1.0; Flicker runs 0.55–1.0 and is identical for identical inputs; Color cycle rotates hue from the base color
- [ ] Elapsed motion time from the server anchors the phase
- [ ] Fades: position/color/strength blend by id; new lights fade in; removed lights fade out; the first setup has no fade; house darkness blends
- [ ] Held lights ignore server updates; a released hold ends when the server matches it or after 1 s
- [ ] `HsvColor` round-trips

**Verify:** build (Task 4 command), then `dotnet run --no-build --project CLI/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter "/*/*/StageLightAnimatorTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** — `CLI/Tests/Chaos.Client.Tests/StageLightAnimatorTests.cs`:

```csharp
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class StageLightAnimatorTests
{
    private static StageLightInfo Light(byte id, int tileX = 4, int tileY = 16)
        => new() { Id = id, X = (ushort)(tileX * 16), Y = (ushort)(tileY * 16), X2 = (ushort)(tileX * 16), Y2 = (ushort)(tileY * 16) };

    private static StageLightingStateArgs State(ushort fadeMs, byte house, params StageLightState[] lights)
        => new() { StageX = 0, StageY = 12, StageWidth = 9, StageHeight = 9, HouseLevel = house, FadeMs = fadeMs, Lights = lights };

    private static StageLightState Entry(StageLightInfo light, uint motionMs = 0, uint effectMs = 0)
        => new() { Light = light, MotionElapsedMs = motionMs, EffectElapsedMs = effectMs };

    private static List<StageLightFrame> Frames(StageLightAnimator animator, long now, Func<uint, Vector2?>? lookup = null)
    {
        var frames = new List<StageLightFrame>();
        animator.Evaluate(now, lookup ?? (_ => null), frames);

        return frames;
    }

    [Test]
    public void ToTile_turns_sixteenths_into_tiles()
    {
        var tile = StageLightAnimator.ToTile(64, 264);
        tile.X.Should().Be(4f);
        tile.Y.Should().Be(16.5f);
    }

    [Test]
    public void Sweep_goes_there_and_back_with_easing()
    {
        var a = new Vector2(0, 12);
        var b = new Vector2(8, 12);

        //speed 3 → 8 s there and back
        StageLightAnimator.SweepPosition(a, b, 0, 3).X.Should().BeApproximately(0f, 0.001f);
        StageLightAnimator.SweepPosition(a, b, 2000, 3).X.Should().BeApproximately(4f, 0.001f);
        StageLightAnimator.SweepPosition(a, b, 4000, 3).X.Should().BeApproximately(8f, 0.001f);
        StageLightAnimator.SweepPosition(a, b, 8000, 3).X.Should().BeApproximately(0f, 0.001f);
        StageLightAnimator.SweepPosition(a, b, 500, 3).X.Should().BeLessThan(1f, "it eases out of the start");
    }

    [Test]
    public void Circle_keeps_its_radius()
    {
        var centre = new Vector2(4, 16);
        var edge = new Vector2(6, 16);

        foreach (var ms in new long[] { 0, 1000, 2500, 7999 })
            Vector2.Distance(StageLightAnimator.CirclePosition(centre, edge, ms, 3), centre).Should().BeApproximately(2f, 0.001f);
    }

    [Test]
    public void Pulse_runs_from_30_to_100_percent()
    {
        StageLightAnimator.PulseFactor(0, 3).Should().BeApproximately(0.30f, 0.001f);
        StageLightAnimator.PulseFactor(1000, 3).Should().BeApproximately(1.0f, 0.001f);   //speed 3 → 2 s cycle
    }

    [Test]
    public void Flicker_is_repeatable_and_in_range()
    {
        for (var ms = 0; ms < 5000; ms += 37)
        {
            var value = StageLightAnimator.FlickerFactor(3, ms, 3);
            value.Should().BeInRange(0.55f, 1.0f);
            StageLightAnimator.FlickerFactor(3, ms, 3).Should().Be(value);
        }
    }

    [Test]
    public void Color_cycle_starts_at_the_base_hue_and_turns()
    {
        var red = new Color(255, 0, 0);

        StageLightAnimator.CycleColor(red, 0, 3).Should().Be(new Color(255, 0, 0));

        //speed 3 → 10 s per wheel; a third of the way is green
        var third = StageLightAnimator.CycleColor(red, 3333, 3);
        third.G.Should().BeGreaterThan(200);
        third.R.Should().BeLessThan(40);
    }

    [Test]
    public void Hsv_round_trips()
    {
        var colour = HsvColor.FromHsv(200f, 0.6f, 0.8f);
        var (h, s, v) = HsvColor.ToHsv(colour);

        h.Should().BeApproximately(200f, 1.5f);
        s.Should().BeApproximately(0.6f, 0.01f);
        v.Should().BeApproximately(0.8f, 0.01f);
    }

    [Test]
    public void Entity_tile_turns_the_walking_offset_back_into_tiles()
    {
        //half a step toward +X: (Δx − Δy)·28 = 14, (Δx + Δy)·14 = 7
        var tile = StageLightAnimator.EntityTile(3, 15, new Vector2(14, 7));
        tile.X.Should().BeApproximately(3.5f, 0.001f);
        tile.Y.Should().BeApproximately(15f, 0.001f);
    }

    [Test]
    public void Follow_holds_to_the_stage_and_falls_back_to_its_own_spot()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1) with { Motion = StageLightMotion.Follow, FollowId = 44 })), 0);

        var seen = Frames(animator, 10, id => id == 44 ? new Vector2(20, 30) : null)[0];
        seen.Tile.Should().Be(new Vector2(8, 20));

        var gone = Frames(animator, 20)[0];
        gone.Tile.Should().Be(new Vector2(4, 16));
    }

    [Test]
    public void Server_elapsed_time_anchors_the_sweep()
    {
        var animator = new StageLightAnimator();
        var light = Light(1, 0, 12) with { Motion = StageLightMotion.Sweep, X2 = 8 * 16, Y2 = 12 * 16 };
        animator.Apply(State(0, 100, Entry(light, motionMs: 4000)), 1000);

        Frames(animator, 1000)[0].Tile.X.Should().BeApproximately(8f, 0.001f);
    }

    [Test]
    public void First_setup_does_not_fade_but_later_ones_do()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(1000, 100, Entry(Light(1, 2, 14))), 0);
        Frames(animator, 0)[0].Tile.Should().Be(new Vector2(2, 14));

        animator.Apply(State(1000, 100, Entry(Light(1, 6, 14))), 100);
        Frames(animator, 600)[0].Tile.X.Should().BeApproximately(4f, 0.001f);
        Frames(animator, 1100)[0].Tile.X.Should().BeApproximately(6f, 0.001f);
    }

    [Test]
    public void New_lights_fade_in_and_removed_lights_fade_out()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1) with { Brightness = 100 })), 0);
        Frames(animator, 0);

        animator.Apply(State(1000, 100, Entry(Light(2) with { Brightness = 100 })), 0);

        var half = Frames(animator, 500);
        half.Single(f => f.Id == 2).Strength.Should().BeApproximately(0.5f, 0.001f);
        half.Single(f => f.Id == 1).Strength.Should().BeApproximately(0.5f, 0.001f);

        Frames(animator, 1000).Should().ContainSingle(f => f.Id == 2);
    }

    [Test]
    public void House_darkness_blends()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100), 0);
        animator.Apply(State(400, 0), 0);

        animator.CurrentHouseDarkness(200).Should().BeApproximately(0.5f, 0.001f);
        animator.CurrentHouseDarkness(400).Should().BeApproximately(1f, 0.001f);
    }

    [Test]
    public void Held_light_ignores_the_server_until_it_catches_up()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1, 2, 14))), 0);

        var held = Light(1, 7, 19);
        animator.Hold(held);
        animator.Apply(State(0, 100, Entry(Light(1, 3, 15))), 10);
        Frames(animator, 10)[0].Tile.Should().Be(new Vector2(7, 19));

        animator.Release(1, 20);
        animator.Apply(State(0, 100, Entry(Light(1, 3, 15))), 30);
        Frames(animator, 30)[0].Tile.Should().Be(new Vector2(7, 19), "still waiting for the server to match");

        animator.Apply(State(0, 100, Entry(held)), 40);
        Frames(animator, 40)[0].Tile.Should().Be(new Vector2(7, 19));
        animator.Apply(State(0, 100, Entry(Light(1, 1, 13))), 50);
        Frames(animator, 50)[0].Tile.Should().Be(new Vector2(1, 13), "the hold ended once the server matched");
    }

    [Test]
    public void Released_hold_times_out()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 100, Entry(Light(1, 2, 14))), 0);
        animator.Hold(Light(1, 7, 19));
        animator.Release(1, 0);

        Frames(animator, 1500)[0].Tile.Should().Be(new Vector2(2, 14));
    }

    [Test]
    public void Clear_forgets_everything()
    {
        var animator = new StageLightAnimator();
        animator.Apply(State(0, 20, Entry(Light(1))), 0);
        animator.Clear();

        animator.HasSetup.Should().BeFalse();
        animator.Lights.Should().BeEmpty();
        Frames(animator, 0).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Confirm they fail to build** (build command from Task 4). Expected: errors naming `StageLightAnimator`, `HsvColor`.

- [ ] **Step 3: Create `CLI/Chaos.Client/Utilities/HsvColor.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Utilities;

/// <summary>Hue/saturation/value conversions. Hue is 0-360 degrees; saturation and value are 0-1.</summary>
public static class HsvColor
{
    public static (float Hue, float Saturation, float Value) ToHsv(Color color)
    {
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;
        var saturation = max <= 0f ? 0f : delta / max;

        if (delta <= 0f)
            return (0f, saturation, max);

        float hue;

        if (max == r)
            hue = (g - b) / delta % 6f;
        else if (max == g)
            hue = ((b - r) / delta) + 2f;
        else
            hue = ((r - g) / delta) + 4f;

        hue *= 60f;

        return (hue < 0f ? hue + 360f : hue, saturation, max);
    }

    public static Color FromHsv(float hue, float saturation, float value)
    {
        hue = ((hue % 360f) + 360f) % 360f;
        saturation = Math.Clamp(saturation, 0f, 1f);
        value = Math.Clamp(value, 0f, 1f);

        var chroma = value * saturation;
        var x = chroma * (1f - MathF.Abs((hue / 60f % 2f) - 1f));
        var m = value - chroma;

        var (r, g, b) = (int)(hue / 60f) switch
        {
            0 => (chroma, x, 0f),
            1 => (x, chroma, 0f),
            2 => (0f, chroma, x),
            3 => (0f, x, chroma),
            4 => (x, 0f, chroma),
            _ => (chroma, 0f, x)
        };

        return new Color(r + m, g + m, b + m);
    }
}
```

- [ ] **Step 4: Create `CLI/Chaos.Client/Systems/StageLightAnimator.cs`**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>One spotlight as it looks this frame. <see cref="Tile" /> is a fractional map tile; whole numbers are tile centres.</summary>
public readonly record struct StageLightFrame(
    byte Id,
    Vector2 Tile,
    Color Color,
    float Strength,
    StageLightSize Size,
    bool Beam);

/// <summary>
///     Holds the Theatre lighting setup the server last sent and works out, for any moment, where each spotlight is, what
///     colour it is and how strong it is. Blends from the old look to a new setup over the setup's fade time. Plain math
///     with no drawing, so it is unit-tested directly. Times are milliseconds from any steady clock
///     (<c>Environment.TickCount64</c> in the game).
/// </summary>
public sealed class StageLightAnimator
{
    /// <summary>How long a released drag keeps its local copy while waiting for the server to match it.</summary>
    public const int RELEASE_TIMEOUT_MS = 1000;

    //speed 1..5 → cycle lengths; the spec's Speeds table
    private static readonly float[] SweepMs = [16000, 11000, 8000, 5500, 4000];
    private static readonly float[] PulseMs = [4000, 3000, 2000, 1400, 1000];
    private static readonly float[] FlickerPerSecond = [4, 7, 10, 14, 20];
    private static readonly float[] CycleMs = [24000, 16000, 10000, 6000, 3000];

    private readonly Dictionary<byte, Target> Targets = [];
    private readonly Dictionary<byte, StageLightInfo> Held = [];
    private readonly Dictionary<byte, long> Releasing = [];
    private readonly Dictionary<byte, StageLightFrame> LastFrames = [];
    private readonly Dictionary<byte, StageLightFrame> FadeFrom = [];
    private readonly List<StageLightFrame> FadingOut = [];
    private readonly List<StageLightInfo> LightList = [];

    private float FromHouseDarkness;
    private float ToHouseDarkness;
    private long FadeStartMs;
    private int FadeMs;

    public bool HasSetup { get; private set; }
    public Rectangle Stage { get; private set; }
    public byte HouseLevel { get; private set; } = 100;
    public bool StageGlow { get; private set; } = true;

    /// <summary>The lights in id order. A light the director is dragging shows its local copy.</summary>
    public IReadOnlyList<StageLightInfo> Lights => LightList;

    public void Apply(StageLightingStateArgs args, long nowMs)
    {
        //blend in from whatever showed last frame
        FadeFrom.Clear();

        foreach (var frame in LastFrames.Values)
            FadeFrom[frame.Id] = frame;

        FromHouseDarkness = HasSetup ? CurrentHouseDarkness(nowMs) : Darkness(args.HouseLevel);

        var incoming = new HashSet<byte>();

        foreach (var state in args.Lights)
        {
            var id = state.Light.Id;
            incoming.Add(id);
            Targets[id] = new Target(state.Light, nowMs - state.MotionElapsedMs, nowMs - state.EffectElapsedMs);

            //a released drag ends once the server's copy matches what the director let go of
            if (Releasing.ContainsKey(id) && Held.TryGetValue(id, out var held) && (held == state.Light))
                EndHold(id);
        }

        FadingOut.RemoveAll(frame => incoming.Contains(frame.Id));

        foreach (var id in Targets.Keys.Where(id => !incoming.Contains(id)).ToList())
        {
            if (LastFrames.TryGetValue(id, out var last))
                FadingOut.Add(last);

            Targets.Remove(id);
            EndHold(id);
        }

        Stage = new Rectangle(args.StageX, args.StageY, args.StageWidth, args.StageHeight);
        HouseLevel = args.HouseLevel;
        StageGlow = args.StageGlow;
        ToHouseDarkness = Darkness(args.HouseLevel);
        FadeStartMs = nowMs;
        FadeMs = HasSetup ? args.FadeMs : 0;
        HasSetup = true;
        RebuildLightList();
    }

    public void Clear()
    {
        Targets.Clear();
        Held.Clear();
        Releasing.Clear();
        LastFrames.Clear();
        FadeFrom.Clear();
        FadingOut.Clear();
        LightList.Clear();
        HasSetup = false;
        Stage = Rectangle.Empty;
        HouseLevel = 100;
        StageGlow = true;
        FromHouseDarkness = 0f;
        ToHouseDarkness = 0f;
        FadeMs = 0;
    }

    /// <summary>Darkness strength for the darkness layer: 0 with the house lights full, 1 with them off.</summary>
    public float CurrentHouseDarkness(long nowMs) => MathHelper.Lerp(FromHouseDarkness, ToHouseDarkness, FadeProgress(nowMs));

    /// <summary>Shows <paramref name="light" /> for its id instead of the server's copy. Used while the director drags.</summary>
    public void Hold(StageLightInfo light)
    {
        Held[light.Id] = light;
        Releasing.Remove(light.Id);
        RebuildLightList();
    }

    /// <summary>Ends a hold once the server's copy matches it, or after <see cref="RELEASE_TIMEOUT_MS" />.</summary>
    public void Release(byte id, long nowMs)
    {
        if (Held.ContainsKey(id))
            Releasing[id] = nowMs + RELEASE_TIMEOUT_MS;
    }

    /// <summary>
    ///     Fills <paramref name="frames" /> with every light as it looks at <paramref name="nowMs" />, including lights
    ///     fading out. <paramref name="followLookup" /> turns an entity id into its fractional tile, or null when it isn't
    ///     in view.
    /// </summary>
    public void Evaluate(long nowMs, Func<uint, Vector2?> followLookup, List<StageLightFrame> frames)
    {
        frames.Clear();

        if (!HasSetup)
            return;

        foreach (var (id, deadline) in Releasing.ToList())
            if (nowMs >= deadline)
                EndHold(id);

        var k = FadeProgress(nowMs);
        LastFrames.Clear();

        foreach (var target in Targets.Values)
        {
            var light = Held.GetValueOrDefault(target.Light.Id) ?? target.Light;
            var to = Compute(light, target, nowMs, followLookup);
            var frame = k < 1f ? Blend(FadeFrom.TryGetValue(light.Id, out var from) ? from : to with { Strength = 0f }, to, k) : to;

            frames.Add(frame);
            LastFrames[frame.Id] = frame;
        }

        if (k < 1f)
            foreach (var gone in FadingOut)
                frames.Add(gone with { Strength = gone.Strength * (1f - k) });
        else
            FadingOut.Clear();
    }

    /// <summary>Holds a fractional tile to the stage's tile centres. Unchanged before any setup arrives.</summary>
    public Vector2 ClampToStage(Vector2 tile)
        => Stage.IsEmpty
            ? tile
            : new Vector2(Math.Clamp(tile.X, Stage.Left, Stage.Right - 1), Math.Clamp(tile.Y, Stage.Top, Stage.Bottom - 1));

    public static Vector2 ToTile(ushort x, ushort y)
        => new(x / (float)StageLightInfo.UNITS_PER_TILE, y / (float)StageLightInfo.UNITS_PER_TILE);

    /// <summary>
    ///     An entity's fractional tile, including its walking offset (world pixels). Inverts the isometric formula:
    ///     offset.X = (Δx − Δy)·28 and offset.Y = (Δx + Δy)·14.
    /// </summary>
    public static Vector2 EntityTile(int tileX, int tileY, Vector2 visualOffset)
    {
        var a = visualOffset.X / 28f;
        var b = visualOffset.Y / 14f;

        return new Vector2(tileX + ((a + b) / 2f), tileY + ((b - a) / 2f));
    }

    /// <summary>The follow lookup the game uses: an entity in view, as a fractional tile.</summary>
    public static Vector2? EntityTileOf(uint entityId)
        => WorldState.GetEntity(entityId) is { } entity ? EntityTile(entity.TileX, entity.TileY, entity.VisualOffset) : null;

    public static Vector2 SweepPosition(Vector2 a, Vector2 b, long elapsedMs, byte speed)
    {
        var period = SweepMs[SpeedIndex(speed)];
        var phase = Phase(elapsedMs, period);
        var there = phase < 0.5f ? phase * 2f : 2f - (phase * 2f);
        var eased = there * there * (3f - (2f * there));

        return Vector2.Lerp(a, b, eased);
    }

    public static Vector2 CirclePosition(Vector2 centre, Vector2 edge, long elapsedMs, byte speed)
    {
        var radius = Vector2.Distance(centre, edge);
        var angle = MathHelper.TwoPi * Phase(elapsedMs, SweepMs[SpeedIndex(speed)]);

        return centre + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
    }

    /// <summary>0.30 at the start of the cycle, 1.0 at its middle.</summary>
    public static float PulseFactor(long elapsedMs, byte speed)
    {
        var wave = 0.5f - (0.5f * MathF.Cos(MathHelper.TwoPi * Phase(elapsedMs, PulseMs[SpeedIndex(speed)])));

        return 0.30f + (0.70f * wave);
    }

    /// <summary>A new random level 0.55-1.0 on every step; the same inputs give the same level on every client.</summary>
    public static float FlickerFactor(byte id, long elapsedMs, byte speed)
    {
        var step = (long)(elapsedMs * FlickerPerSecond[SpeedIndex(speed)] / 1000f);

        return 0.55f + (0.45f * Hash01(id, step));
    }

    public static Color CycleColor(Color baseColor, long elapsedMs, byte speed)
    {
        var (hue, _, _) = HsvColor.ToHsv(baseColor);

        return HsvColor.FromHsv(hue + (360f * Phase(elapsedMs, CycleMs[SpeedIndex(speed)])), 1f, 1f);
    }

    //integer hash → [0, 1)
    private static float Hash01(byte id, long step)
    {
        unchecked
        {
            var h = (uint)(step * 374761393L) ^ (id * 668265263u);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;

            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    private StageLightFrame Compute(StageLightInfo light, Target target, long nowMs, Func<uint, Vector2?> followLookup)
    {
        var a = ToTile(light.X, light.Y);
        var b = ToTile(light.X2, light.Y2);
        var motionMs = Math.Max(0, nowMs - target.MotionStartMs);
        var effectMs = Math.Max(0, nowMs - target.EffectStartMs);

        var tile = light.Motion switch
        {
            StageLightMotion.Sweep  => SweepPosition(a, b, motionMs, light.MotionSpeed),
            StageLightMotion.Circle => ClampToStage(CirclePosition(a, b, motionMs, light.MotionSpeed)),
            StageLightMotion.Follow => followLookup(light.FollowId) is { } seen ? ClampToStage(seen) : a,
            _                       => a
        };

        var color = new Color(light.R, light.G, light.B);
        var strength = light.Brightness / 100f;

        switch (light.Effect)
        {
            case StageLightEffect.Pulse:
                strength *= PulseFactor(effectMs, light.EffectSpeed);

                break;
            case StageLightEffect.Flicker:
                strength *= FlickerFactor(light.Id, effectMs, light.EffectSpeed);

                break;
            case StageLightEffect.ColorCycle:
                color = CycleColor(color, effectMs, light.EffectSpeed);

                break;
        }

        return new StageLightFrame(light.Id, tile, color, strength, light.Size, light.Beam);
    }

    private static StageLightFrame Blend(StageLightFrame from, StageLightFrame to, float k)
        => to with
        {
            Tile = Vector2.Lerp(from.Tile, to.Tile, k),
            Color = Color.Lerp(from.Color, to.Color, k),
            Strength = MathHelper.Lerp(from.Strength, to.Strength, k)
        };

    private void EndHold(byte id)
    {
        Releasing.Remove(id);

        if (Held.Remove(id))
            RebuildLightList();
    }

    private void RebuildLightList()
    {
        LightList.Clear();

        foreach (var target in Targets.Values.OrderBy(t => t.Light.Id))
            LightList.Add(Held.GetValueOrDefault(target.Light.Id) ?? target.Light);
    }

    private float FadeProgress(long nowMs) => FadeMs <= 0 ? 1f : Math.Clamp((nowMs - FadeStartMs) / (float)FadeMs, 0f, 1f);

    private static float Darkness(byte houseLevel) => (100 - Math.Min(houseLevel, (byte)100)) / 100f;

    private static float Phase(long elapsedMs, float periodMs) => elapsedMs % (long)periodMs / periodMs;

    private static int SpeedIndex(byte speed) => Math.Clamp((int)speed, 1, 5) - 1;

    private sealed record Target(StageLightInfo Light, long MotionStartMs, long EffectStartMs);
}
```

- [ ] **Step 5: Build and run the tests** (commands in **Verify**). Expected: all `StageLightAnimatorTests` pass. If `Color_cycle_starts_at_the_base_hue_and_turns` is off by one on a channel, compare with `BeApproximately`-style bounds rather than changing the math.

```json:metadata
{"files": ["Chaos.Client/Utilities/HsvColor.cs", "Chaos.Client/Systems/StageLightAnimator.cs", "Tests/Chaos.Client.Tests/StageLightAnimatorTests.cs"], "verifyCommand": "dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/spotlights-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter \"/*/*/StageLightAnimatorTests/*\" --no-ansi", "acceptanceCriteria": ["sweep/circle/follow", "pulse/flicker/cycle", "phase anchoring", "fades", "held lights", "hsv round trip"], "modelTier": "standard"}
```

---

### Task 6: Rendering — light strength, spotlight masks, house darkness, SpotlightRenderer

**Goal:** The rendering layer can lift darkness with scaled spotlight masks, dim the darkness to the house level, and draw colored pools, tints and beams.

**Files:**
- Modify: `CLI/Chaos.Client.Rendering/LightSource.cs`
- Modify: `CLI/Chaos.Client.Rendering/DarknessRenderer.cs` (`StampLightSources`, `ComputeSourceHash`, `OnMapChanged`, `OnLightLevel`, new `SetHouseDarkness`)
- Create: `CLI/Chaos.Client.Rendering/SpotlightMasks.cs`
- Create: `CLI/Chaos.Client.Rendering/SpotlightRenderer.cs`
- Test: `CLI/Tests/Chaos.Client.Tests/SpotlightMaskTests.cs`

**Acceptance Criteria:**
- [ ] `LightSource` has `Strength` (default 32); lanterns unchanged; the stamp scales mask values by `Strength / 32`; the source hash includes it
- [ ] `SetHouseDarkness(d)` sets a dark map's alpha to `d` (black) and forces a rebuild; null returns to full black; it survives `OnMapChanged`
- [ ] Masks are symmetric, 32 at the centre, 0 at the corners, larger for larger sizes
- [ ] `SpotlightRenderer.ColorScale(s, d) = s · (0.35 + 0.65·d)`

**Verify:** build (Task 4 command) then `dotnet run --no-build --project CLI/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter "/*/*/SpotlightMaskTests/*" --no-ansi` → pass

**Steps:**

- [ ] **Step 1: Failing tests** — `CLI/Tests/Chaos.Client.Tests/SpotlightMaskTests.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class SpotlightMaskTests
{
    [Test]
    public void Mask_is_full_in_the_middle_and_empty_in_the_corners()
    {
        var mask = SpotlightMasks.Build(1.5f);
        var centre = (mask.Height / 2 * mask.Width) + (mask.Width / 2);

        mask.Pixels[centre].Should().Be(32);
        mask.Pixels[0].Should().Be(0);
        mask.Pixels[^1].Should().Be(0);
        mask.Pixels.Max().Should().Be(32);
    }

    [Test]
    public void Mask_is_mirror_symmetric()
    {
        var mask = SpotlightMasks.Build(1f);

        for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width / 2; x++)
                mask.Pixels[(y * mask.Width) + x].Should().Be(mask.Pixels[(y * mask.Width) + (mask.Width - 1 - x)]);
    }

    [Test]
    public void Larger_sizes_make_wider_masks()
    {
        SpotlightMasks.Get(StageLightSize.Small).Width.Should().BeLessThan(SpotlightMasks.Get(StageLightSize.Medium).Width);
        SpotlightMasks.Get(StageLightSize.Medium).Width.Should().BeLessThan(SpotlightMasks.Get(StageLightSize.Large).Width);
    }

    [Test]
    public void Colour_is_a_wash_with_the_house_lights_up_and_full_when_dark()
    {
        SpotlightRenderer.ColorScale(1f, 0f).Should().BeApproximately(0.35f, 0.001f);
        SpotlightRenderer.ColorScale(1f, 1f).Should().BeApproximately(1f, 0.001f);
        SpotlightRenderer.ColorScale(0.5f, 1f).Should().BeApproximately(0.5f, 0.001f);
    }
}
```

- [ ] **Step 2: `LightSource.cs`** — replace the record with:

```csharp
/// <summary>
///     One light for the darkness layer and the Tab map. <see cref="Strength" /> scales the mask, 0-32: lanterns use 32,
///     Theatre spotlights use their current brightness.
/// </summary>
public readonly record struct LightSource(
    Vector2 ScreenPosition,
    int TileX,
    int TileY,
    Direction Direction,
    LightMask PixelMask,
    (int Dx, int Dy)[] TileOffsets,
    byte Strength = 32);
```

(Keep the file's existing usings; the existing `new LightSource(...)` call in `LightingSystem` compiles unchanged.)

- [ ] **Step 3: `DarknessRenderer.StampLightSources`** — inside the per-source loop, read the strength once and scale each mask value. Replace:

```csharp
                    var maskValue = mask.Pixels[maskRowOffset + mx];

                    if (maskValue == 0)
                        continue;
```

with:

```csharp
                    var maskValue = mask.Pixels[maskRowOffset + mx];

                    //spotlights pass their brightness; lanterns pass 32 and skip the scale
                    if (strength < 32)
                        maskValue = (byte)(maskValue * strength / 32);

                    if (maskValue == 0)
                        continue;
```

and add `var strength = source.Strength;` right after `var mask = source.PixelMask;`.

In `ComputeSourceHash`, change the `HashCode.Combine` call to include the strength:

```csharp
            hash = HashCode.Combine(
                hash,
                (int)src.ScreenPosition.X,
                (int)src.ScreenPosition.Y,
                src.PixelMask.Width,
                src.Strength);
```

- [ ] **Step 4: House darkness on `DarknessRenderer`.** Add the field next to the other private fields:

```csharp
    private float? HouseDarkness;
```

Add this public method after `ReapplyLightLevel`:

```csharp
    /// <summary>
    ///     How dark a dark map is (0-1) while a Theatre lighting setup is active; null goes back to full black. Kept
    ///     across same-map refreshes (<see cref="OnMapChanged" /> reads it); WorldScreen clears it on a real map change.
    /// </summary>
    public void SetHouseDarkness(float? darkness)
    {
        var clamped = darkness is { } d ? Math.Clamp(d, 0f, 1f) : (float?)null;

        if (clamped == HouseDarkness)
            return;

        HouseDarkness = clamped;

        if (!IsDarkMap)
            return;

        Alpha = HouseDarkness ?? 1f;
        DarknessColor = Color.Black;

        //force the next Update to rebuild the texture with the new alpha
        LastOffsetX = int.MinValue;
        LastOffsetY = int.MinValue;

        if (Alpha <= 0f)
        {
            DisposeTexture();
            CacheValid = false;
        }
    }
```

In `OnMapChanged`, change the dark branch from `Alpha = 1f;` to `Alpha = HouseDarkness ?? 1f;`. In `OnLightLevel`, change the `else if (IsDarkMap)` branch's `Alpha = 1f;` to `Alpha = HouseDarkness ?? 1f;`.

- [ ] **Step 5: Create `CLI/Chaos.Client.Rendering/SpotlightMasks.cs`**

```csharp
#region
using Chaos.Client.Data.Models;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Darkness masks for Theatre spotlights: a soft floor oval with the game's 2:1 proportions, plus a softer column
///     above it so a person standing in the light is lit too. Values run 0-32 like lantern masks. The mask is centred on
///     the floor point. One per size, built once.
/// </summary>
public static class SpotlightMasks
{
    /// <summary>A floor radius of one tile spans about this many pixels across (and half as many down).</summary>
    public const float TILE_TO_PIXELS = 39.6f;

    /// <summary>How far above the floor point the lit column reaches, in pixels.</summary>
    public const int BODY_HEIGHT = 64;

    private static readonly Dictionary<StageLightSize, LightMask> Cache = [];

    /// <summary>Pool radius across the floor, in tiles: Small 1, Medium 1.5, Large 2.5.</summary>
    public static float RadiusTiles(StageLightSize size)
        => size switch
        {
            StageLightSize.Small => 1f,
            StageLightSize.Large => 2.5f,
            _                    => 1.5f
        };

    public static LightMask Get(StageLightSize size)
    {
        if (!Cache.TryGetValue(size, out var mask))
            Cache[size] = mask = Build(RadiusTiles(size));

        return mask;
    }

    public static LightMask Build(float radiusTiles)
    {
        var rx = radiusTiles * TILE_TO_PIXELS;
        var ry = rx / 2f;
        var columnHalfWidth = rx * 0.45f;
        var halfWidth = (int)MathF.Ceiling(rx);
        var halfHeight = (int)MathF.Ceiling(MathF.Max(ry, BODY_HEIGHT));
        var width = (halfWidth * 2) + 1;
        var height = (halfHeight * 2) + 1;
        var pixels = new byte[width * height];

        for (var py = 0; py < height; py++)
            for (var px = 0; px < width; px++)
            {
                var dx = px - halfWidth;
                var dy = py - halfHeight;
                var floor = Falloff(MathF.Sqrt((dx / rx * (dx / rx)) + (dy / ry * (dy / ry))));
                var body = 0f;

                if (dy < 0)
                {
                    var up = -dy / (float)BODY_HEIGHT;
                    var across = MathF.Abs(dx) / columnHalfWidth;
                    body = Falloff(MathF.Max(up, across)) * 0.9f;
                }

                pixels[(py * width) + px] = (byte)MathF.Round(MathF.Max(floor, body) * 32f);
            }

        return new LightMask
        {
            Width = width,
            Height = height,
            Pixels = pixels
        };
    }

    //1 inside 65% of the radius, easing to 0 at the edge
    private static float Falloff(float distance)
    {
        if (distance >= 1f)
            return 0f;

        if (distance <= 0.65f)
            return 1f;

        var t = (1f - distance) / 0.35f;

        return t * t * (3f - (2f * t));
    }
}
```

- [ ] **Step 6: Create `CLI/Chaos.Client.Rendering/SpotlightRenderer.cs`**

```csharp
#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>One spotlight to draw this frame. <see cref="ScreenPosition" /> is the floor point in viewport pixels.</summary>
public readonly record struct SpotlightDraw(
    Vector2 ScreenPosition,
    Color Color,
    float Strength,
    float RadiusTiles,
    bool Beam);

/// <summary>
///     Draws Theatre spotlights' colour: a soft pool on the floor, a fainter tint over whoever stands in it, and an
///     optional beam from the top of the view. Additive, drawn right after the darkness layer. The three textures are
///     built once, white with an alpha falloff, and tinted per light.
/// </summary>
public sealed class SpotlightRenderer : IDisposable
{
    private const int POOL_WIDTH = 128;
    private const int POOL_HEIGHT = 64;
    private const int TINT_WIDTH = 64;
    private const int TINT_HEIGHT = 128;
    private const int BEAM_WIDTH = 64;
    private const int BEAM_HEIGHT = 256;
    private const float POOL_ALPHA = 0.85f;
    private const float TINT_ALPHA = 0.45f;
    private const float BEAM_ALPHA = 0.38f;

    /// <summary>Colour strength with the house lights full, so a spotlight reads as a wash in a lit room.</summary>
    private const float WASH_FLOOR = 0.35f;

    private readonly Texture2D Beam;
    private readonly Texture2D Pool;
    private readonly Texture2D Tint;

    public SpotlightRenderer(GraphicsDevice device)
    {
        Pool = BuildOval(device, POOL_WIDTH, POOL_HEIGHT, 1.4f);
        Tint = BuildOval(device, TINT_WIDTH, TINT_HEIGHT, 1.2f);
        Beam = BuildBeam(device);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Pool.Dispose();
        Tint.Dispose();
        Beam.Dispose();
    }

    public static float ColorScale(float strength, float houseDarkness)
        => strength * (WASH_FLOOR + ((1f - WASH_FLOOR) * Math.Clamp(houseDarkness, 0f, 1f)));

    /// <summary>Draws every light. Call inside a batch begun with <see cref="BlendState.Additive" />.</summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle viewport, ReadOnlySpan<SpotlightDraw> lights, float houseDarkness)
    {
        foreach (var light in lights)
        {
            var scale = ColorScale(light.Strength, houseDarkness);

            if (scale <= 0.005f)
                continue;

            var x = viewport.X + light.ScreenPosition.X;
            var y = viewport.Y + light.ScreenPosition.Y;
            var poolWidth = light.RadiusTiles * SpotlightMasks.TILE_TO_PIXELS * 2f * 1.15f;
            var poolHeight = poolWidth / 2f;

            if (light.Beam && (y > viewport.Y))
            {
                var beamWidth = poolWidth * 0.9f;

                spriteBatch.Draw(
                    Beam,
                    new Rectangle((int)(x - (beamWidth / 2f)), viewport.Y, (int)beamWidth, (int)(y - viewport.Y)),
                    Tinted(light.Color, scale * BEAM_ALPHA));
            }

            spriteBatch.Draw(
                Pool,
                new Rectangle((int)(x - (poolWidth / 2f)), (int)(y - (poolHeight / 2f)), (int)poolWidth, (int)poolHeight),
                Tinted(light.Color, scale * POOL_ALPHA));

            var tintWidth = poolWidth * 0.55f;
            var tintHeight = SpotlightMasks.BODY_HEIGHT * 1.4f;

            spriteBatch.Draw(
                Tint,
                new Rectangle((int)(x - (tintWidth / 2f)), (int)(y - tintHeight + (poolHeight * 0.25f)), (int)tintWidth, (int)tintHeight),
                Tinted(light.Color, scale * TINT_ALPHA));
        }
    }

    //additive blend adds rgb × alpha, so the tint's alpha carries the strength and its rgb stays the pure colour
    private static Color Tinted(Color color, float alpha)
        => new(color.R, color.G, color.B, (byte)(Math.Clamp(alpha, 0f, 1f) * 255f));

    private static Texture2D BuildOval(GraphicsDevice device, int width, int height, float power)
    {
        var pixels = new Color[width * height];

        for (var py = 0; py < height; py++)
            for (var px = 0; px < width; px++)
            {
                var dx = (px + 0.5f - (width / 2f)) / (width / 2f);
                var dy = (py + 0.5f - (height / 2f)) / (height / 2f);
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                var alpha = distance >= 1f ? 0f : MathF.Pow(1f - distance, power);

                pixels[(py * width) + px] = new Color((byte)255, (byte)255, (byte)255, (byte)(alpha * 255f));
            }

        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);

        return texture;
    }

    //narrow at the top (35% of the width), full width at the bottom, brighter toward the floor, soft sides
    private static Texture2D BuildBeam(GraphicsDevice device)
    {
        var pixels = new Color[BEAM_WIDTH * BEAM_HEIGHT];

        for (var py = 0; py < BEAM_HEIGHT; py++)
        {
            var down = py / (BEAM_HEIGHT - 1f);
            var halfWidth = (0.175f + (0.325f * down)) * BEAM_WIDTH;

            for (var px = 0; px < BEAM_WIDTH; px++)
            {
                var across = MathF.Abs(px + 0.5f - (BEAM_WIDTH / 2f)) / halfWidth;
                var side = across >= 1f ? 0f : 1f - (across * across);
                var alpha = side * (0.15f + (0.85f * down));

                pixels[(py * BEAM_WIDTH) + px] = new Color((byte)255, (byte)255, (byte)255, (byte)(alpha * 255f));
            }
        }

        var texture = new Texture2D(device, BEAM_WIDTH, BEAM_HEIGHT);
        texture.SetData(pixels);

        return texture;
    }
}
```

- [ ] **Step 7: Build and run** (commands in **Verify**). Expected: `SpotlightMaskTests` pass; the whole client test project still passes: `dotnet run --no-build --project CLI/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`.

```json:metadata
{"files": ["Chaos.Client.Rendering/LightSource.cs", "Chaos.Client.Rendering/DarknessRenderer.cs", "Chaos.Client.Rendering/SpotlightMasks.cs", "Chaos.Client.Rendering/SpotlightRenderer.cs", "Tests/Chaos.Client.Tests/SpotlightMaskTests.cs"], "verifyCommand": "dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/spotlights-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter \"/*/*/SpotlightMaskTests/*\" --no-ansi", "acceptanceCriteria": ["LightSource strength + stamp scaling + hash", "SetHouseDarkness", "mask shape", "ColorScale"], "modelTier": "standard"}
```

---

### Task 7: World integration — spotlights in the game view

**Goal:** Every client in the Theatre sees the spotlights and the dimmed darkness, driven by `StageLightingState`.

**Files:**
- Modify: `CLI/Chaos.Client/Systems/LightingSystem.cs` (`Gather` signature, spotlight sources, `FloorToScreen`, `SpotlightTileOffsets`)
- Modify: `CLI/Chaos.Client/Collections/WorldState.cs` (`StageLights`; clear in `ResetAll`)
- Create: `CLI/Chaos.Client/Screens/WorldScreen.StageLighting.cs`
- Modify: `CLI/Chaos.Client/Screens/WorldScreen.cs` (fields, construction, dispose, wiring call, unwiring)
- Modify: `CLI/Chaos.Client/Screens/WorldScreen.Update.cs` (before `Lighting.Gather`)
- Modify: `CLI/Chaos.Client/Screens/WorldScreen.Draw.cs` (after the darkness block)
- Modify: `CLI/Chaos.Client/Screens/WorldScreen.Map.cs` (new-map branch)
- Modify: `CLI/CLAUDE.md`

**Acceptance Criteria:**
- [ ] On a dark map, spotlight sources follow the lanterns in `Lighting.Sources` with the size's mask, the frame's strength (×32) and radius-1/2/3 tile offsets
- [ ] `DarknessRenderer.SetHouseDarkness` is fed every frame (null when no setup)
- [ ] Spotlight colour draws in an additive screen-space batch right after the darkness layer, lit or dark
- [ ] A real map change clears the setup and house darkness; a same-map refresh keeps them
- [ ] `WorldState.ResetAll` clears the setup
- [ ] CLAUDE.md documents step 5b and the new classes

**Verify:** build (Task 4 command) → `Build succeeded`; full client test run passes.

**Steps:**

- [ ] **Step 1: `LightingSystem`.** Add offset arrays next to `Euclidean3`/`Euclidean5`:

```csharp
    private static readonly (int Dx, int Dy)[] Euclidean1 = ComputeEuclidean(1);
    private static readonly (int Dx, int Dy)[] Euclidean2 = ComputeEuclidean(2);
```

Replace `Gather` with (the lantern loop body is unchanged):

```csharp
    /// <summary>
    ///     Walks the world entity list and builds the light source array for the current frame, then adds the Theatre
    ///     spotlights in <paramref name="spotlights" />. Short-circuits to an empty span when the map isn't dark, so stale
    ///     sources from a prior map can't leak across a transition.
    /// </summary>
    public void Gather(MapFile? mapFile, MapFlags flags, Camera camera, IReadOnlyList<StageLightFrame> spotlights)
    {
        Count = 0;

        if (mapFile is null || !flags.HasFlag(MapFlags.Darkness))
            return;

        foreach (var entity in WorldState.GetEntities())
        {
            if (entity.LanternSize == LanternSize.None)
                continue;

            var pixelMask = DataContext.LightMasks.Get(entity.LanternSize);

            if (pixelMask is null)
                continue;

            var tileOffsets = GetTileOffsets(entity.LanternSize, entity.Direction);

            var tileWorld = Camera.TileToWorld(entity.TileX, entity.TileY, mapFile.Height);
            var tileCenterX = tileWorld.X + DaLibConstants.HALF_TILE_WIDTH;
            var tileCenterY = tileWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;
            var screenPos = camera.WorldToScreen(new Vector2(tileCenterX + entity.VisualOffset.X, tileCenterY + entity.VisualOffset.Y));

            if (Count >= Buffer.Length)
                Array.Resize(ref Buffer, Buffer.Length * 2);

            Buffer[Count++] = new LightSource(
                screenPos,
                entity.TileX,
                entity.TileY,
                entity.Direction,
                pixelMask,
                tileOffsets);
        }

        foreach (var spot in spotlights)
        {
            var strength = (byte)Math.Clamp((int)MathF.Round(spot.Strength * 32f), 0, 32);

            if (strength == 0)
                continue;

            if (Count >= Buffer.Length)
                Array.Resize(ref Buffer, Buffer.Length * 2);

            Buffer[Count++] = new LightSource(
                FloorToScreen(spot.Tile, mapFile.Height, camera),
                (int)MathF.Round(spot.Tile.X),
                (int)MathF.Round(spot.Tile.Y),
                default,
                SpotlightMasks.Get(spot.Size),
                SpotlightTileOffsets(spot.Size),
                strength);
        }
    }

    /// <summary>A fractional tile's floor centre in viewport pixels (same formula as <c>Camera.TileToWorld</c> plus half a tile).</summary>
    public static Vector2 FloorToScreen(Vector2 tile, int mapHeight, Camera camera)
    {
        var worldX = ((mapHeight - 1 + tile.X - tile.Y) * DaLibConstants.HALF_TILE_WIDTH) + DaLibConstants.HALF_TILE_WIDTH;
        var worldY = ((tile.X + tile.Y) * DaLibConstants.HALF_TILE_HEIGHT) + DaLibConstants.HALF_TILE_HEIGHT;

        return camera.WorldToScreen(new Vector2(worldX, worldY));
    }

    /// <summary>Tiles a spotlight reveals on the Tab map: radius 1, 2 or 3 for Small, Medium, Large.</summary>
    public static (int Dx, int Dy)[] SpotlightTileOffsets(StageLightSize size)
        => size switch
        {
            StageLightSize.Small => Euclidean1,
            StageLightSize.Large => Euclidean3,
            _                    => Euclidean2
        };
```

Keep the lantern loop exactly as it is; only the signature, doc comment and the new tail change. `DaLibConstants` is what the lantern loop already uses; if it resolves under another name there, use the same one. `Euclidean1`, `Euclidean2` must be declared **after** `ComputeEuclidean` is reachable (static field initializers in a class run in textual order; `ComputeEuclidean` is a method, so order doesn't matter).

- [ ] **Step 2: `WorldState`.** Next to `PokerTable`:

```csharp
    /// <summary>
    ///     The Theatre lighting setup and its animation. Not cleared by <see cref="Clear" />: a same-map refresh (which
    ///     happens whenever the director flips darkness) must keep the lights. WorldScreen clears it on a real map change;
    ///     <see cref="ResetAll" /> clears it on logout.
    /// </summary>
    public static StageLightAnimator StageLights { get; } = new();
```

In `ResetAll`, after `Song.Reset();` add `StageLights.Clear();`. Add `using Chaos.Client.Systems;` if missing.

- [ ] **Step 3: Create `CLI/Chaos.Client/Screens/WorldScreen.StageLighting.cs`**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Theatre stage lighting: the setup from the server, per-frame spotlights, and their drawing.</summary>
public sealed partial class WorldScreen
{
    private readonly List<StageLightFrame> SpotlightFrames = [];
    private SpotlightDraw[] SpotlightDraws = new SpotlightDraw[16];
    private int SpotlightDrawCount;
    private float HouseDarknessNow;
    private SpotlightRenderer SpotlightRenderer = null!;

    private void WireStageLighting() => Game.Connection.OnStageLightingState += HandleStageLightingState;

    private void UnwireStageLighting() => Game.Connection.OnStageLightingState -= HandleStageLightingState;

    private static void HandleStageLightingState(StageLightingStateArgs args)
        => WorldState.StageLights.Apply(args, Environment.TickCount64);

    /// <summary>Forgets the Theatre lighting. Called on a real map change only, before the darkness layer resets.</summary>
    private void ResetStageLighting()
    {
        WorldState.StageLights.Clear();
        DarknessRenderer.SetHouseDarkness(null);
    }

    /// <summary>Works out this frame's spotlights, the house darkness, and where to draw each light's colour.</summary>
    private void UpdateSpotlights()
    {
        var now = Environment.TickCount64;
        var lights = WorldState.StageLights;

        lights.Evaluate(now, StageLightAnimator.EntityTileOf, SpotlightFrames);
        HouseDarknessNow = lights.CurrentHouseDarkness(now);
        DarknessRenderer.SetHouseDarkness(lights.HasSetup ? HouseDarknessNow : null);

        SpotlightDrawCount = 0;

        if (MapFile is null || (SpotlightFrames.Count == 0))
            return;

        if (SpotlightDraws.Length < SpotlightFrames.Count)
            SpotlightDraws = new SpotlightDraw[SpotlightFrames.Count * 2];

        foreach (var frame in SpotlightFrames)
            SpotlightDraws[SpotlightDrawCount++] = new SpotlightDraw(
                LightingSystem.FloorToScreen(frame.Tile, MapFile.Height, Camera),
                frame.Color,
                frame.Strength,
                SpotlightMasks.RadiusTiles(frame.Size),
                frame.Beam);
    }

    /// <summary>Spotlight colour, additive, in screen space right after the darkness layer.</summary>
    private void DrawSpotlights(SpriteBatch spriteBatch)
    {
        if (SpotlightDrawCount == 0)
            return;

        spriteBatch.Begin(blendState: BlendState.Additive, samplerState: GlobalSettings.Sampler, rasterizerState: ScissorRasterizerState);
        SpotlightRenderer.Draw(spriteBatch, WorldHud.ViewportBounds, SpotlightDraws.AsSpan(0, SpotlightDrawCount), HouseDarknessNow);
        spriteBatch.End();
    }
}
```

- [ ] **Step 4: Hook it into `WorldScreen.cs`.**
  - After `AmbientEffects = new AmbientEffects();` add `SpotlightRenderer = new SpotlightRenderer(graphicsDevice);`.
  - After `AmbientEffects.Dispose();` (in the dispose/unload block) add `SpotlightRenderer.Dispose();`.
  - Where the other `Wire*()` calls run for connection events (e.g. near `WireBugReport();`), add `WireStageLighting();`.
  - Next to `Game.Connection.OnBugReportOpen -= HandleBugReportOpen;` add `UnwireStageLighting();`.

- [ ] **Step 5: `WorldScreen.Update.cs`.** Replace

```csharp
        //gather light sources for this frame and feed them to consumers
        Lighting.Gather(MapFile, CurrentMapFlags, Camera);
```

with

```csharp
        //theatre spotlights first: they feed both the light sources and the house darkness
        UpdateSpotlights();

        //gather light sources for this frame and feed them to consumers
        Lighting.Gather(MapFile, CurrentMapFlags, Camera, SpotlightFrames);
```

- [ ] **Step 6: `WorldScreen.Draw.cs`.** Directly after the darkness block (the `if (DarknessRenderer.IsActive) { ... spriteBatch.End(); }`) and before the weather block, add:

```csharp

            //theatre spotlight colour — over the darkness, under weather and ambient effects
            DrawSpotlights(spriteBatch);
```

- [ ] **Step 7: `WorldScreen.Map.cs`.** In `HandleMapInfo`'s new-map branch, directly after `Poker.Hide();`, add:

```csharp

        //a new map starts with no stage lighting; a same-map refresh (above) deliberately keeps it
        ResetStageLighting();
```

It must run before `DarknessRenderer.OnMapChanged(...)` in that branch. Do not touch the same-map branch.

- [ ] **Step 8: `CLAUDE.md`.** In the Rendering Layer list, after the `AmbientEffects` bullet, add:

```markdown
- **`SpotlightRenderer`** -- Theatre spotlight colour: additive pool, body tint and optional beam per light, drawn right after the darkness layer. Its darkness masks come from **`SpotlightMasks`** (generated 2:1 ovals, one per size).
```

In the Game Systems list, after `LightingSystem`, add:

```markdown
- **`StageLightAnimator`** -- Holds the Theatre lighting setup (`StageLightingState`) and computes each spotlight's tile, colour and strength per frame: sweep/circle/follow, pulse/flicker/colour cycle, fades, and lights held while the director drags. Lives on `WorldState.StageLights`; not cleared by `WorldState.Clear()`.
```

In the draw order, after `5. DarknessRenderer ...` add:

```
  5b. SpotlightRenderer -- Theatre spotlight colour (additive), whenever a stage lighting setup has lights
```

and change item 5's text to `5. DarknessRenderer -- light/darkness overlay (if MapFlags has Darkness; strength follows the Theatre house level)`.

- [ ] **Step 9: Build and run the whole client test project** (Task 4 build command, then `dotnet run --no-build --project CLI/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`). Expected: build succeeds, all tests pass.

```json:metadata
{"files": ["Chaos.Client/Systems/LightingSystem.cs", "Chaos.Client/Collections/WorldState.cs", "Chaos.Client/Screens/WorldScreen.StageLighting.cs", "Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/Screens/WorldScreen.Update.cs", "Chaos.Client/Screens/WorldScreen.Draw.cs", "Chaos.Client/Screens/WorldScreen.Map.cs", "CLAUDE.md"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/spotlights-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server", "acceptanceCriteria": ["spotlight light sources", "house darkness per frame", "additive draw after darkness", "clear on real map change only", "ResetAll clears", "CLAUDE.md"], "modelTier": "standard"}
```

---

### Task 8: Window state and stage geometry (client, tested)

**Goal:** The plain logic behind the window: selection, follow picking, edit throttling, new-light defaults, motion changes, and the diamond view's tile/pixel mapping.

**Files:**
- Create: `CLI/Chaos.Client/ViewModel/StageLightingPanelState.cs`
- Create: `CLI/Chaos.Client/Controls/World/Popups/Theatre/StageViewGeometry.cs`
- Modify: `CLI/Chaos.Client/Collections/WorldState.cs` (`StageLightingPanel`; reset in `ResetAll`)
- Test: `CLI/Tests/Chaos.Client.Tests/StageLightingPanelStateTests.cs`

**Acceptance Criteria:**
- [ ] `Throttle` passes the first edit, holds edits inside 125 ms, passes again after; `Flush` returns the held edit once
- [ ] `Reconcile` keeps a valid selection, picks the first light otherwise, and selects a newly added light after `ExpectNewLight`
- [ ] `NewLight` sits on the stage centre; `WithMotion` to Sweep/Circle sets the second point 2 tiles along +X (or −X at the edge) and clears FollowId unless Follow
- [ ] `Step` wraps enum values both ways
- [ ] Geometry: stage centre maps to the view centre; view↔tile round-trips; `ToUnits` clamps to the stage

**Verify:** build, then `dotnet run --no-build --project CLI/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter "/*/*/StageLightingPanelStateTests/*" --no-ansi` → pass

**Steps:**

- [ ] **Step 1: Failing tests** — `CLI/Tests/Chaos.Client.Tests/StageLightingPanelStateTests.cs`:

```csharp
using Chaos.Client.Controls.World.Popups.Theatre;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class StageLightingPanelStateTests
{
    private static readonly Rectangle Stage = new(0, 12, 9, 9);

    private static StageLightingInteractionArgs Edit(byte level) => new() { Action = StageLightingAction.SetHouseLevel, Level = level };

    [Test]
    public void Throttle_sends_at_most_every_125_ms_and_flush_sends_the_last()
    {
        var state = new StageLightingPanelState();

        state.Throttle(Edit(1), 1000).Should().NotBeNull();
        state.Throttle(Edit(2), 1050).Should().BeNull();
        state.Throttle(Edit(3), 1100).Should().BeNull();
        state.Flush(1110)!.Level.Should().Be(3);
        state.Flush(1120).Should().BeNull();
        state.Throttle(Edit(4), 1300).Should().NotBeNull();
    }

    [Test]
    public void Reconcile_keeps_a_valid_selection_or_picks_the_first()
    {
        var state = new StageLightingPanelState();
        StageLightInfo[] lights = [new() { Id = 2 }, new() { Id = 5 }];

        state.Reconcile(lights);
        state.SelectedLightId.Should().Be((byte)2);

        state.Select(5);
        state.Reconcile(lights);
        state.SelectedLightId.Should().Be((byte)5);

        state.Reconcile([new StageLightInfo { Id = 2 }]);
        state.SelectedLightId.Should().Be((byte)2);

        state.Reconcile([]);
        state.SelectedLightId.Should().BeNull();
    }

    [Test]
    public void Reconcile_selects_a_new_light_after_add()
    {
        var state = new StageLightingPanelState();
        state.Reconcile([new StageLightInfo { Id = 1 }]);

        state.ExpectNewLight();
        state.Reconcile([new StageLightInfo { Id = 1 }, new StageLightInfo { Id = 2 }]);

        state.SelectedLightId.Should().Be((byte)2);
    }

    [Test]
    public void Selecting_cancels_a_follow_pick()
    {
        var state = new StageLightingPanelState();
        state.Select(1);
        state.BeginFollowPick();
        state.AwaitingFollowPick.Should().BeTrue();

        state.Select(2);
        state.AwaitingFollowPick.Should().BeFalse();
    }

    [Test]
    public void New_light_sits_on_the_stage_centre()
    {
        var light = StageLightingPanelState.NewLight(Stage);

        light.X.Should().Be(64);
        light.Y.Should().Be(256);
        light.Brightness.Should().Be(80);
    }

    [Test]
    public void Sweep_puts_the_second_point_two_tiles_along_x_or_back_at_the_edge()
    {
        var middle = StageLightingPanelState.WithMotion(new StageLightInfo { X = 64, Y = 256 }, StageLightMotion.Sweep, Stage);
        middle.X2.Should().Be(96);
        middle.Y2.Should().Be(256);

        var edge = StageLightingPanelState.WithMotion(new StageLightInfo { X = 128, Y = 256 }, StageLightMotion.Circle, Stage);
        edge.X2.Should().Be(96);
    }

    [Test]
    public void Leaving_follow_clears_the_target()
        => StageLightingPanelState.WithMotion(new StageLightInfo { Motion = StageLightMotion.Follow, FollowId = 9 }, StageLightMotion.Still, Stage)
                                  .FollowId
                                  .Should()
                                  .Be(0u);

    [Test]
    public void Step_wraps_both_ways()
    {
        StageLightingPanelState.Step(StageLightMotion.Follow, 1).Should().Be(StageLightMotion.Still);
        StageLightingPanelState.Step(StageLightMotion.Still, -1).Should().Be(StageLightMotion.Follow);
        StageLightingPanelState.Step(StageLightEffect.None, 1).Should().Be(StageLightEffect.Pulse);
    }

    [Test]
    public void Geometry_centres_the_stage_and_round_trips()
    {
        var centre = StageViewGeometry.TileToView(new Vector2(4, 16), Stage, 200, 150);
        centre.Should().Be(new Vector2(100, 75));

        var tile = new Vector2(2.25f, 18.5f);
        var back = StageViewGeometry.ViewToTile(StageViewGeometry.TileToView(tile, Stage, 200, 150), Stage, 200, 150);
        back.X.Should().BeApproximately(tile.X, 0.001f);
        back.Y.Should().BeApproximately(tile.Y, 0.001f);
    }

    [Test]
    public void ToUnits_clamps_to_the_stage()
    {
        StageViewGeometry.ToUnits(new Vector2(-3, 40), Stage).Should().Be(((ushort)0, (ushort)320));
        StageViewGeometry.ToUnits(new Vector2(4.5f, 16.25f), Stage).Should().Be(((ushort)72, (ushort)260));
    }
}
```

- [ ] **Step 2: Create `CLI/Chaos.Client/Controls/World/Popups/Theatre/StageViewGeometry.cs`**

```csharp
#region
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     Maps between map tiles and pixels in the Stage Lighting window's diamond view, which draws the stage at the game's
///     angle (+X goes down-right, +Y goes down-left). Tile centres are whole numbers; the stage's centre tile sits in the
///     middle of the view.
/// </summary>
public static class StageViewGeometry
{
    public const float HALF_TILE_WIDTH = 11f;
    public const float HALF_TILE_HEIGHT = 5.5f;

    public static Vector2 TileToView(Vector2 tile, Rectangle stage, int viewWidth, int viewHeight)
    {
        var centre = Centre(stage);
        var u = tile.X - centre.X;
        var v = tile.Y - centre.Y;

        return new Vector2((viewWidth / 2f) + ((u - v) * HALF_TILE_WIDTH), (viewHeight / 2f) + ((u + v) * HALF_TILE_HEIGHT));
    }

    public static Vector2 ViewToTile(Vector2 view, Rectangle stage, int viewWidth, int viewHeight)
    {
        var centre = Centre(stage);
        var a = (view.X - (viewWidth / 2f)) / HALF_TILE_WIDTH;
        var b = (view.Y - (viewHeight / 2f)) / HALF_TILE_HEIGHT;

        return new Vector2(centre.X + ((a + b) / 2f), centre.Y + ((b - a) / 2f));
    }

    /// <summary>A fractional tile as wire units (sixteenths), held to the stage's tile centres.</summary>
    public static (ushort X, ushort Y) ToUnits(Vector2 tile, Rectangle stage)
    {
        var x = Math.Clamp(tile.X, stage.Left, stage.Right - 1);
        var y = Math.Clamp(tile.Y, stage.Top, stage.Bottom - 1);

        return ((ushort)MathF.Round(x * StageLightInfo.UNITS_PER_TILE), (ushort)MathF.Round(y * StageLightInfo.UNITS_PER_TILE));
    }

    public static Vector2 Centre(Rectangle stage) => new(stage.Left + ((stage.Width - 1) / 2f), stage.Top + ((stage.Height - 1) / 2f));
}
```

- [ ] **Step 3: Create `CLI/Chaos.Client/ViewModel/StageLightingPanelState.cs`**

```csharp
#region
using Chaos.Client.Controls.World.Popups.Theatre;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The Stage Lighting window's own state: the scene names, the selected light, a pending follow pick, edit
///     throttling while dragging, and where the window sits. The lighting setup itself lives in
///     <c>WorldState.StageLights</c>.
/// </summary>
public sealed class StageLightingPanelState
{
    public const int MAX_LIGHTS = 8;

    /// <summary>At most 8 edits a second while dragging.</summary>
    public const int SEND_INTERVAL_MS = 125;

    /// <summary>The window's 13 colour swatches (the 14th swatch opens the custom picker).</summary>
    public static readonly Color[] Swatches =
    [
        new(255, 255, 255),
        new(255, 210, 122),
        new(255, 170, 40),
        new(255, 75, 60),
        new(255, 60, 200),
        new(160, 80, 255),
        new(60, 100, 255),
        new(60, 220, 255),
        new(60, 255, 140),
        new(180, 255, 60),
        new(255, 255, 60),
        new(140, 60, 40),
        new(30, 30, 60)
    ];

    private HashSet<byte> KnownIds = [];
    private long? LastSendMs;
    private StageLightingInteractionArgs? Pending;
    private bool SelectNewest;

    public IReadOnlyList<string> Scenes { get; private set; } = [];
    public byte? SelectedLightId { get; private set; }

    /// <summary>True after the director picks Follow and before they click a person on the stage view.</summary>
    public bool AwaitingFollowPick { get; private set; }

    /// <summary>Whether the window is shrunk to its bar. Kept until logout.</summary>
    public bool IsMinimized { get; set; }

    /// <summary>Where the director dragged the window. Kept until logout.</summary>
    public Point? Position { get; set; }

    public void SetScenes(IReadOnlyList<string> names) => Scenes = names;

    public void Select(byte? id)
    {
        SelectedLightId = id;
        AwaitingFollowPick = false;
    }

    /// <summary>The next <see cref="Reconcile" /> selects whichever light is new.</summary>
    public void ExpectNewLight() => SelectNewest = true;

    /// <summary>Keeps the selection valid after a new setup: a new light when one was expected, else the current one, else the first.</summary>
    public void Reconcile(IReadOnlyList<StageLightInfo> lights)
    {
        if (SelectNewest && lights.FirstOrDefault(l => !KnownIds.Contains(l.Id)) is { } added)
        {
            SelectNewest = false;
            Select(added.Id);
        }

        KnownIds = lights.Select(l => l.Id).ToHashSet();

        if (SelectedLightId is { } id && KnownIds.Contains(id))
            return;

        Select(lights.Count > 0 ? lights[0].Id : null);
    }

    public void BeginFollowPick() => AwaitingFollowPick = SelectedLightId is not null;

    public void EndFollowPick() => AwaitingFollowPick = false;

    /// <summary>
    ///     Rate-limits a stream of edits (drags, sliders, the colour picker). Returns the edit to send now, or null to
    ///     hold it; the held edit goes out on the next allowed call or on <see cref="Flush" />.
    /// </summary>
    public StageLightingInteractionArgs? Throttle(StageLightingInteractionArgs edit, long nowMs)
    {
        if (LastSendMs is { } last && ((nowMs - last) < SEND_INTERVAL_MS))
        {
            Pending = edit;

            return null;
        }

        LastSendMs = nowMs;
        Pending = null;

        return edit;
    }

    /// <summary>The held edit when a drag ends, or null when nothing is waiting.</summary>
    public StageLightingInteractionArgs? Flush(long nowMs)
    {
        var pending = Pending;
        Pending = null;

        if (pending is not null)
            LastSendMs = nowMs;

        return pending;
    }

    /// <summary>Called on a real map change: the scene list and selection belong to the Theatre visit.</summary>
    public void ResetForNewMap()
    {
        Scenes = [];
        Select(null);
        KnownIds = [];
        SelectNewest = false;
        Pending = null;
        LastSendMs = null;
    }

    /// <summary>Called on logout: also forgets where the window sat.</summary>
    public void Reset()
    {
        ResetForNewMap();
        IsMinimized = false;
        Position = null;
    }

    public static StageLightInfo NewLight(Rectangle stage)
    {
        var (x, y) = StageViewGeometry.ToUnits(StageViewGeometry.Centre(stage), stage);

        return new StageLightInfo
        {
            X = x,
            Y = y,
            X2 = x,
            Y2 = y
        };
    }

    /// <summary>
    ///     The light with a new motion. Switching into Sweep or Circle puts the second point 2 tiles along +X (or −X when
    ///     that is off the stage). Any motion but Follow drops the follow target.
    /// </summary>
    public static StageLightInfo WithMotion(StageLightInfo light, StageLightMotion motion, Rectangle stage)
    {
        var wasMoving = light.Motion is StageLightMotion.Sweep or StageLightMotion.Circle;
        var isMoving = motion is StageLightMotion.Sweep or StageLightMotion.Circle;

        if (isMoving && !wasMoving)
        {
            var here = StageLightAnimator.ToTile(light.X, light.Y);
            var (x2, y2) = StageViewGeometry.ToUnits(here + new Vector2(2, 0), stage);

            if ((x2 == light.X) && (y2 == light.Y))
                (x2, y2) = StageViewGeometry.ToUnits(here - new Vector2(2, 0), stage);

            light = light with { X2 = x2, Y2 = y2 };
        }

        return light with
        {
            Motion = motion,
            FollowId = motion == StageLightMotion.Follow ? light.FollowId : 0
        };
    }

    /// <summary>The enum value <paramref name="direction" /> steps away, wrapping at both ends.</summary>
    public static T Step<T>(T value, int direction) where T: struct, Enum
    {
        var values = Enum.GetValues<T>();
        var index = Array.IndexOf(values, value);

        return values[(((index + direction) % values.Length) + values.Length) % values.Length];
    }
}
```

- [ ] **Step 4: `WorldState`.** Next to `StageLights`:

```csharp
    /// <summary>The Stage Lighting window's own state (selection, throttle, position). Reset on logout.</summary>
    public static StageLightingPanelState StageLightingPanel { get; } = new();
```

In `ResetAll`, after `StageLights.Clear();` add `StageLightingPanel.Reset();`. (`Chaos.Client.ViewModel` is already imported by `WorldState`.)

- [ ] **Step 5: Build and run** (commands in **Verify**). Expected: pass.

```json:metadata
{"files": ["Chaos.Client/ViewModel/StageLightingPanelState.cs", "Chaos.Client/Controls/World/Popups/Theatre/StageViewGeometry.cs", "Chaos.Client/Collections/WorldState.cs", "Tests/Chaos.Client.Tests/StageLightingPanelStateTests.cs"], "verifyCommand": "dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/spotlights-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter \"/*/*/StageLightingPanelStateTests/*\" --no-ansi", "acceptanceCriteria": ["throttle + flush", "reconcile + new light select", "NewLight/WithMotion", "Step wraps", "geometry round-trip + clamp"], "modelTier": "standard"}
```

---

### Task 9: Window building blocks — button, slider, colour picker, stage view

**Goal:** Four self-drawn controls the window is assembled from, each doing one job.

**Files:**
- Create: `CLI/Chaos.Client/Controls/World/Popups/Theatre/StageButton.cs`
- Create: `CLI/Chaos.Client/Controls/World/Popups/Theatre/StageSlider.cs`
- Create: `CLI/Chaos.Client/Controls/World/Popups/Theatre/StageColorPicker.cs`
- Create: `CLI/Chaos.Client/Controls/World/Popups/Theatre/StageView.cs`

**Acceptance Criteria:**
- [ ] `StageButton`: 16 px, caption, optional colour fill/dot/rainbow, Selected/Enabled/hover/pressed looks, `Clicked`
- [ ] `StageSlider`: any int range, drag to set, `ValueChanged` while dragging, `DragEnded` on release, `IsDragging`
- [ ] `StageColorPicker`: saturation/value square for the hue, hue strip, preview, OK; live `ColorChanged`, `DragEnded`, `Closed`
- [ ] `StageView`: diamond floor, lights with numbers, live position dots, ring on the selected light, dotted sweep line / circle dots and a square handle, blue people squares with a ring for followed people, hover name; press/drag light or handle; click person while picking follow
- [ ] All four compile and draw only with ASCII text

**Verify:** build (Task 4 command) → `Build succeeded`

**Steps:**

- [ ] **Step 1: Create `StageButton.cs`**

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     A compact button for the Stage Lighting window (16 px high by default). Can show a colour <see cref="Dot" />
///     before its caption, be filled with a <see cref="Fill" /> colour (a swatch), or show rainbow stripes (the custom
///     colour swatch).
/// </summary>
public sealed class StageButton : UIElement
{
    public const int HEIGHT = 16;

    private static readonly Color Face = new(42, 34, 26);
    private static readonly Color FacePressed = new(58, 47, 34);
    private static readonly Color FaceSelected = new(138, 109, 59);
    private static readonly Color FaceDisabled = new(24, 20, 16);
    private static readonly Color Edge = new(138, 109, 59);

    private bool Hovered;
    private bool Pressed;

    public StageButton(string caption, int width)
    {
        Caption = caption;
        Width = width;
        Height = HEIGHT;
    }

    public string Caption { get; set; }
    public bool Selected { get; set; }
    public Color? Dot { get; set; }
    public Color? Fill { get; set; }
    public bool Rainbow { get; set; }

    public event Action? Clicked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        var bounds = ScreenBounds;

        if (Rainbow)
        {
            var stripe = Math.Max(1, Width / 6);

            for (var i = 0; i < 6; i++)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(bounds.X + (i * stripe), bounds.Y, i == 5 ? Width - (5 * stripe) : stripe, Height),
                    HsvColor.FromHsv(i * 60f, 1f, 1f));
        } else
        {
            var face = Fill ?? (!Enabled ? FaceDisabled : Selected ? FaceSelected : Pressed ? FacePressed : Face);
            DrawRectClipped(spriteBatch, bounds, face);
        }

        var edge = (Fill.HasValue || Rainbow) && Selected ? LegendColors.Gold : Hovered && Enabled ? LegendColors.Silver : Edge;
        DrawBorder(spriteBatch, bounds, edge);

        if ((Fill.HasValue || Rainbow) && Selected)
            DrawBorder(spriteBatch, new Rectangle(bounds.X + 1, bounds.Y + 1, Width - 2, Height - 2), LegendColors.Gold);

        if (string.IsNullOrEmpty(Caption))
            return;

        var textColor = !Enabled ? LegendColors.Gray : Selected && !Fill.HasValue ? new Color(28, 24, 20) : Hovered ? Color.White : LegendColors.Silver;
        var dotSpace = Dot.HasValue ? 10 : 0;
        var textWidth = TextRenderer.MeasureWidth(Caption);
        var x = bounds.X + ((Width - textWidth - dotSpace) / 2);

        if (Dot is { } dot)
        {
            DrawRectClipped(spriteBatch, new Rectangle(x, bounds.Y + ((Height - 8) / 2), 8, 8), dot);
            x += dotSpace;
        }

        DrawTextClipped(spriteBatch, new Vector2(x, bounds.Y + ((Height - TextRenderer.CHAR_HEIGHT) / 2)), Caption, textColor, false);
    }

    public override void OnMouseEnter() => Hovered = true;

    public override void OnMouseLeave()
    {
        Hovered = false;
        Pressed = false;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if ((e.Button != MouseButton.Left) || !Enabled)
            return;

        Pressed = true;
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e) => Pressed = false;

    public override void OnClick(ClickEvent e)
    {
        if (!Enabled)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        Hovered = false;
        Pressed = false;
    }
}
```

If `DrawBorder` is ambiguous (it is a `public static` on `UIElement`), call it as `UIElement.DrawBorder(...)`.

- [ ] **Step 2: Create `StageSlider.cs`**

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     A thin slider for the Stage Lighting window, over any integer range. Raises <see cref="ValueChanged" /> while
///     dragging and <see cref="DragEnded" /> on release.
/// </summary>
public sealed class StageSlider : UIElement
{
    private const int HANDLE_WIDTH = 6;
    private const int TRACK_HEIGHT = 3;

    private static readonly Color Track = new(68, 68, 68);

    public StageSlider(int min, int max)
    {
        Min = min;
        Max = max;
        Value = min;
        Height = 12;
    }

    public int Min { get; }
    public int Max { get; }
    public int Value { get; private set; }

    /// <summary>True while the handle is held. The window doesn't overwrite a held slider from the server.</summary>
    public bool IsDragging { get; private set; }

    public event Action<int>? ValueChanged;
    public event Action? DragEnded;

    public void SetValue(int value) => Value = Math.Clamp(value, Min, Max);

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + ((Height - TRACK_HEIGHT) / 2), Width, TRACK_HEIGHT), Track);

        var usable = Width - HANDLE_WIDTH;
        var offset = Max == Min ? 0 : (int)MathF.Round(usable * (Value - Min) / (float)(Max - Min));

        DrawRectClipped(spriteBatch, new Rectangle(ScreenX + offset, ScreenY, HANDLE_WIDTH, Height), Enabled ? LegendColors.Silver : LegendColors.Gray);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if ((e.Button != MouseButton.Left) || !Enabled)
            return;

        IsDragging = true;
        SetFromMouse(e.ScreenX);
        e.Handled = true;
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!IsDragging)
            return;

        SetFromMouse(e.ScreenX);
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if ((e.Button != MouseButton.Left) || !IsDragging)
            return;

        IsDragging = false;
        DragEnded?.Invoke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        if (IsDragging)
        {
            IsDragging = false;
            DragEnded?.Invoke();
        }
    }

    private void SetFromMouse(int screenX)
    {
        var usable = Math.Max(1, Width - HANDLE_WIDTH);
        var ratio = Math.Clamp((screenX - ScreenX - (HANDLE_WIDTH / 2f)) / usable, 0f, 1f);
        var value = Min + (int)MathF.Round(ratio * (Max - Min));

        if (value == Value)
            return;

        Value = value;
        ValueChanged?.Invoke(value);
    }
}
```

- [ ] **Step 3: Create `StageColorPicker.cs`**

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     The custom colour picker: a saturation/value square for the current hue, a hue strip, a preview and OK. Raises
///     <see cref="ColorChanged" /> live while dragging, <see cref="DragEnded" /> on release, and <see cref="Closed" /> on
///     OK.
/// </summary>
public sealed class StageColorPicker : UIPanel
{
    private const int PAD = 6;
    private const int SQUARE = 96;
    private const int STRIP_WIDTH = 12;
    private const int STRIP_X = PAD + SQUARE + PAD;

    private Dragging Part;
    private float Hue;
    private float Saturation = 1f;
    private float Value = 1f;
    private Texture2D? SquareTexture;
    private float SquareHue = -1f;
    private Texture2D? StripTexture;

    public StageColorPicker()
    {
        Width = STRIP_X + STRIP_WIDTH + PAD;
        Height = PAD + SQUARE + 4 + StageButton.HEIGHT + PAD;
        BackgroundColor = new Color(28, 24, 20);
        BorderColor = new Color(138, 109, 59);
        Visible = false;

        var ok = new StageButton("OK", 32)
        {
            X = Width - PAD - 32,
            Y = PAD + SQUARE + 4
        };

        ok.Clicked += () =>
        {
            Visible = false;
            Closed?.Invoke();
        };

        AddChild(ok);
    }

    public Color Current => HsvColor.FromHsv(Hue, Saturation, Value);

    public event Action<Color>? ColorChanged;
    public event Action? DragEnded;
    public event Action? Closed;

    public void Open(Color color)
    {
        (Hue, Saturation, Value) = HsvColor.ToHsv(color);
        Visible = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
        EnsureTextures();

        var left = ScreenX;
        var top = ScreenY;
        DrawTexture(spriteBatch, SquareTexture, new Vector2(left + PAD, top + PAD), Color.White);
        DrawTexture(spriteBatch, StripTexture, new Vector2(left + STRIP_X, top + PAD), Color.White);

        //markers: a small box on the square, a bar on the strip
        var mx = left + PAD + (int)(Saturation * (SQUARE - 1));
        var my = top + PAD + (int)((1f - Value) * (SQUARE - 1));
        DrawBorder(spriteBatch, new Rectangle(mx - 3, my - 3, 7, 7), Color.White);
        var hy = top + PAD + (int)(Hue / 360f * (SQUARE - 1));
        DrawRectClipped(spriteBatch, new Rectangle(left + STRIP_X - 2, hy - 1, STRIP_WIDTH + 4, 3), Color.White);

        //preview and hex
        var previewY = top + PAD + SQUARE + 4;
        DrawRectClipped(spriteBatch, new Rectangle(left + PAD, previewY + 2, 24, 12), Current);
        var c = Current;
        DrawTextClipped(spriteBatch, new Vector2(left + PAD + 30, previewY + 2), $"#{c.R:X2}{c.G:X2}{c.B:X2}", LegendColors.Silver, false);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        var x = e.ScreenX - ScreenX;
        var y = e.ScreenY - ScreenY;

        if ((y < PAD) || (y >= PAD + SQUARE))
            return;

        if ((x >= PAD) && (x < PAD + SQUARE))
            Part = Dragging.Square;
        else if ((x >= STRIP_X) && (x < STRIP_X + STRIP_WIDTH))
            Part = Dragging.Strip;
        else
            return;

        SetFromMouse(e.ScreenX, e.ScreenY);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (Part == Dragging.None)
            return;

        SetFromMouse(e.ScreenX, e.ScreenY);
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (Part == Dragging.None)
            return;

        Part = Dragging.None;
        DragEnded?.Invoke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();

        if (Part != Dragging.None)
        {
            Part = Dragging.None;
            DragEnded?.Invoke();
        }
    }

    public override void Dispose()
    {
        SquareTexture?.Dispose();
        StripTexture?.Dispose();
        base.Dispose();
    }

    private void SetFromMouse(int screenX, int screenY)
    {
        var y = Math.Clamp((screenY - ScreenY - PAD) / (float)(SQUARE - 1), 0f, 1f);

        if (Part == Dragging.Square)
        {
            Saturation = Math.Clamp((screenX - ScreenX - PAD) / (float)(SQUARE - 1), 0f, 1f);
            Value = 1f - y;
        } else
            Hue = y * 359.9f;

        ColorChanged?.Invoke(Current);
    }

    //UI textures are premultiplied; these are opaque, so plain colours are already correct
    private void EnsureTextures()
    {
        if (StripTexture is null)
        {
            var strip = new Color[STRIP_WIDTH * SQUARE];

            for (var py = 0; py < SQUARE; py++)
                for (var px = 0; px < STRIP_WIDTH; px++)
                    strip[(py * STRIP_WIDTH) + px] = HsvColor.FromHsv(py / (float)(SQUARE - 1) * 359.9f, 1f, 1f);

            StripTexture = new Texture2D(TextureConverter.Device, STRIP_WIDTH, SQUARE);
            StripTexture.SetData(strip);
        }

        if (SquareTexture is not null && (Math.Abs(SquareHue - Hue) < 0.01f))
            return;

        var pixels = new Color[SQUARE * SQUARE];

        for (var py = 0; py < SQUARE; py++)
            for (var px = 0; px < SQUARE; px++)
                pixels[(py * SQUARE) + px] = HsvColor.FromHsv(Hue, px / (float)(SQUARE - 1), 1f - (py / (float)(SQUARE - 1)));

        SquareTexture ??= new Texture2D(TextureConverter.Device, SQUARE, SQUARE);
        SquareTexture.SetData(pixels);
        SquareHue = Hue;
    }

    private enum Dragging
    {
        None,
        Square,
        Strip
    }
}
```

- [ ] **Step 4: Create `StageView.cs`**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     The Stage Lighting window's picture of the stage: a diamond floor at the game's angle; each light as a soft
///     coloured circle with its number and a small dot where it is right now; a dotted line or circle to a square handle
///     for Sweep and Circle lights; people on the stage as blue squares (ringed in the light's colour when followed).
///     Press and drag a light or a handle to move it. While picking a follow target, click a person. Hover a person to
///     see their name.
/// </summary>
public sealed class StageView : UIElement
{
    public const int VIEW_WIDTH = 200;
    public const int VIEW_HEIGHT = 150;

    private const float HIT_RADIUS = 9f;
    private const int BLOB = 32;

    private static readonly Color Backdrop = new(13, 11, 9);
    private static readonly Color Wood = new(109, 74, 44);
    private static readonly Color Seam = new(74, 53, 36);
    private static readonly Color Person = new(127, 179, 255);

    private readonly List<StageLightFrame> Live = [];
    private Texture2D? Blob;
    private Texture2D? Floor;
    private Rectangle FloorStage;
    private DragKind Dragging;
    private byte DragId;
    private Vector2 Mouse;
    private bool MouseInside;

    public StageView()
    {
        Width = VIEW_WIDTH;
        Height = VIEW_HEIGHT;
    }

    public byte? SelectedLightId { get; set; }
    public bool PickingFollow { get; set; }

    public event Action<byte>? LightPressed;
    public event Action<byte, Vector2>? LightDragged;
    public event Action<byte, Vector2>? HandleDragged;
    public event Action<byte>? DragEnded;
    public event Action<uint>? PersonPicked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
        DrawRectClipped(spriteBatch, ScreenBounds, Backdrop);

        var setup = WorldState.StageLights;
        var stage = setup.Stage;

        if (stage.IsEmpty)
            return;

        EnsureTextures(stage);
        var origin = new Vector2(ScreenX, ScreenY);
        DrawTexture(spriteBatch, Floor, origin, Color.White);

        //paths and handles under the lights
        foreach (var light in setup.Lights)
        {
            var anchor = View(StageLightAnimator.ToTile(light.X, light.Y), stage);
            var handle = View(StageLightAnimator.ToTile(light.X2, light.Y2), stage);

            if (light.Motion == StageLightMotion.Sweep)
                DrawDots(spriteBatch, origin, [anchor, handle]);
            else if (light.Motion == StageLightMotion.Circle)
                DrawCircle(spriteBatch, origin, light, stage);

            if (light.Motion is StageLightMotion.Sweep or StageLightMotion.Circle)
            {
                var h = origin + handle;
                DrawRectClipped(spriteBatch, new Rectangle((int)h.X - 3, (int)h.Y - 3, 7, 7), new Color(light.R, light.G, light.B));
                UIElement.DrawBorder(spriteBatch, new Rectangle((int)h.X - 3, (int)h.Y - 3, 7, 7), Color.White);
            }
        }

        foreach (var light in setup.Lights)
        {
            var centre = origin + View(StageLightAnimator.ToTile(light.X, light.Y), stage);
            var radius = 7f + (3f * (int)light.Size);
            var rect = new Rectangle((int)(centre.X - (radius * 1.6f)), (int)(centre.Y - (radius * 0.8f)), (int)(radius * 3.2f), (int)(radius * 1.6f));

            DrawTextureFitted(spriteBatch, Blob, rect, new Color(light.R, light.G, light.B) * 0.9f);

            if (light.Id == SelectedLightId)
                UIElement.DrawBorder(spriteBatch, rect, Color.White);

            DrawTextClipped(spriteBatch, centre - new Vector2(3, 16), light.Id.ToString(), Color.White, false);
        }

        //where each light is right now (moving lights)
        setup.Evaluate(Environment.TickCount64, StageLightAnimator.EntityTileOf, Live);

        foreach (var frame in Live)
        {
            var p = origin + View(frame.Tile, stage);
            DrawRectClipped(spriteBatch, new Rectangle((int)p.X - 1, (int)p.Y - 1, 3, 3), Color.White * MathHelper.Clamp(frame.Strength, 0.3f, 1f));
        }

        //people on the stage
        string? hoverName = null;

        foreach (var entity in WorldState.GetEntities())
        {
            if (entity.Type != ClientEntityType.Aisling)
                continue;

            var tile = StageLightAnimator.EntityTile(entity.TileX, entity.TileY, entity.VisualOffset);

            if (!OnStage(tile, stage))
                continue;

            var p = View(tile, stage);
            var screen = origin + p;

            foreach (var light in setup.Lights)
                if ((light.Motion == StageLightMotion.Follow) && (light.FollowId == entity.Id))
                    UIElement.DrawBorder(spriteBatch, new Rectangle((int)screen.X - 5, (int)screen.Y - 5, 11, 11), new Color(light.R, light.G, light.B));

            DrawRectClipped(spriteBatch, new Rectangle((int)screen.X - 3, (int)screen.Y - 3, 7, 7), Person);
            UIElement.DrawBorder(spriteBatch, new Rectangle((int)screen.X - 3, (int)screen.Y - 3, 7, 7), Color.White);

            if (MouseInside && (Vector2.Distance(p, Mouse) <= 6f))
                hoverName = entity.Name;
        }

        if (hoverName is not null)
        {
            var width = TextRenderer.MeasureWidth(hoverName) + 6;
            var box = new Rectangle((int)(origin.X + Mouse.X + 8), (int)(origin.Y + Mouse.Y - 6), width, TextRenderer.CHAR_HEIGHT + 2);
            DrawRectClipped(spriteBatch, box, Color.Black * 0.8f);
            DrawTextClipped(spriteBatch, new Vector2(box.X + 3, box.Y + 1), hoverName, Color.White, false);
        }

        if (PickingFollow)
            DrawTextClipped(spriteBatch, origin + new Vector2(4, VIEW_HEIGHT - TextRenderer.CHAR_HEIGHT - 2), "Click a person", LegendColors.Gold, false);
    }

    public override void OnMouseEnter() => MouseInside = true;

    public override void OnMouseLeave() => MouseInside = false;

    public override void OnMouseMove(MouseMoveEvent e)
    {
        Mouse = new Vector2(e.ScreenX - ScreenX, e.ScreenY - ScreenY);

        if (Dragging == DragKind.None)
            return;

        var tile = StageViewGeometry.ViewToTile(Mouse, WorldState.StageLights.Stage, VIEW_WIDTH, VIEW_HEIGHT);

        if (Dragging == DragKind.Light)
            LightDragged?.Invoke(DragId, tile);
        else
            HandleDragged?.Invoke(DragId, tile);

        e.Handled = true;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        var point = new Vector2(e.ScreenX - ScreenX, e.ScreenY - ScreenY);

        if (PickingFollow)
        {
            if (PersonAt(point) is { } personId)
                PersonPicked?.Invoke(personId);

            return;
        }

        //handles sit on top, so they win over a light underneath
        if (HandleAt(point) is { } handleId)
        {
            Dragging = DragKind.Handle;
            DragId = handleId;
            LightPressed?.Invoke(handleId);

            return;
        }

        if (LightAt(point) is { } lightId)
        {
            Dragging = DragKind.Light;
            DragId = lightId;
            LightPressed?.Invoke(lightId);
        }
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if ((e.Button != MouseButton.Left) || (Dragging == DragKind.None))
            return;

        Dragging = DragKind.None;
        DragEnded?.Invoke(DragId);
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        MouseInside = false;

        if (Dragging == DragKind.None)
            return;

        Dragging = DragKind.None;
        DragEnded?.Invoke(DragId);
    }

    public override void Dispose()
    {
        Floor?.Dispose();
        Blob?.Dispose();
        base.Dispose();
    }

    private static Vector2 View(Vector2 tile, Rectangle stage) => StageViewGeometry.TileToView(tile, stage, VIEW_WIDTH, VIEW_HEIGHT);

    private static bool OnStage(Vector2 tile, Rectangle stage)
        => (tile.X >= stage.Left - 0.5f) && (tile.X < stage.Right - 0.5f) && (tile.Y >= stage.Top - 0.5f) && (tile.Y < stage.Bottom - 0.5f);

    private byte? LightAt(Vector2 point) => Nearest(WorldState.StageLights.Lights, l => StageLightAnimator.ToTile(l.X, l.Y), point);

    private byte? HandleAt(Vector2 point)
        => Nearest(
            WorldState.StageLights.Lights.Where(l => l.Motion is StageLightMotion.Sweep or StageLightMotion.Circle),
            l => StageLightAnimator.ToTile(l.X2, l.Y2),
            point);

    private static byte? Nearest(IEnumerable<StageLightInfo> lights, Func<StageLightInfo, Vector2> where, Vector2 point)
    {
        byte? best = null;
        var bestDistance = HIT_RADIUS;
        var stage = WorldState.StageLights.Stage;

        foreach (var light in lights)
        {
            var distance = Vector2.Distance(point, View(where(light), stage));

            if (distance <= bestDistance)
            {
                best = light.Id;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static uint? PersonAt(Vector2 point)
    {
        var stage = WorldState.StageLights.Stage;

        foreach (var entity in WorldState.GetEntities())
        {
            if (entity.Type != ClientEntityType.Aisling)
                continue;

            var tile = StageLightAnimator.EntityTile(entity.TileX, entity.TileY, entity.VisualOffset);

            if (OnStage(tile, stage) && (Vector2.Distance(point, View(tile, stage)) <= 6f))
                return entity.Id;
        }

        return null;
    }

    private void DrawDots(SpriteBatch spriteBatch, Vector2 origin, Vector2[] line)
    {
        var from = line[0];
        var to = line[1];
        var steps = Math.Max(1, (int)(Vector2.Distance(from, to) / 4f));

        for (var i = 0; i <= steps; i++)
        {
            var p = origin + Vector2.Lerp(from, to, i / (float)steps);
            DrawRectClipped(spriteBatch, new Rectangle((int)p.X, (int)p.Y, 2, 2), Color.White);
        }
    }

    private void DrawCircle(SpriteBatch spriteBatch, Vector2 origin, StageLightInfo light, Rectangle stage)
    {
        var centre = StageLightAnimator.ToTile(light.X, light.Y);
        var radius = Vector2.Distance(centre, StageLightAnimator.ToTile(light.X2, light.Y2));

        for (var i = 0; i < 32; i++)
        {
            var angle = MathHelper.TwoPi * i / 32f;
            var p = origin + View(centre + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius), stage);
            DrawRectClipped(spriteBatch, new Rectangle((int)p.X, (int)p.Y, 2, 2), Color.White * 0.8f);
        }
    }

    //the floor is rebuilt only when the stage rectangle changes; the blob once. UI textures are premultiplied.
    private void EnsureTextures(Rectangle stage)
    {
        if (Blob is null)
        {
            var blob = new Color[BLOB * BLOB];

            for (var py = 0; py < BLOB; py++)
                for (var px = 0; px < BLOB; px++)
                {
                    var dx = (px + 0.5f - (BLOB / 2f)) / (BLOB / 2f);
                    var dy = (py + 0.5f - (BLOB / 2f)) / (BLOB / 2f);
                    var d = MathF.Sqrt((dx * dx) + (dy * dy));
                    var a = d >= 1f ? 0f : d < 0.35f ? 1f : 1f - ((d - 0.35f) / 0.65f);
                    var v = (byte)(a * 255f);
                    blob[(py * BLOB) + px] = new Color(v, v, v, v);
                }

            Blob = new Texture2D(TextureConverter.Device, BLOB, BLOB);
            Blob.SetData(blob);
        }

        if (Floor is not null && (FloorStage == stage))
            return;

        var pixels = new Color[VIEW_WIDTH * VIEW_HEIGHT];

        for (var py = 0; py < VIEW_HEIGHT; py++)
            for (var px = 0; px < VIEW_WIDTH; px++)
            {
                var tile = StageViewGeometry.ViewToTile(new Vector2(px + 0.5f, py + 0.5f), stage, VIEW_WIDTH, VIEW_HEIGHT);

                if (!OnStage(tile, stage))
                {
                    pixels[(py * VIEW_WIDTH) + px] = Backdrop;

                    continue;
                }

                //a seam where either coordinate crosses a tile edge (x.5)
                var fx = MathF.Abs(((tile.X + 0.5f) % 1f) - 0.5f);
                var fy = MathF.Abs(((tile.Y + 0.5f) % 1f) - 0.5f);
                pixels[(py * VIEW_WIDTH) + px] = (fx > 0.45f) || (fy > 0.45f) ? Seam : Wood;
            }

        Floor ??= new Texture2D(TextureConverter.Device, VIEW_WIDTH, VIEW_HEIGHT);
        Floor.SetData(pixels);
        FloorStage = stage;
    }

    private enum DragKind
    {
        None,
        Light,
        Handle
    }
}
```

`TextureConverter` lives in `Chaos.Client.Rendering`; `ClientEntityType` in `Chaos.Client.Rendering.Definitions`. Remove any using the compiler reports unused.

- [ ] **Step 5: Build** (Task 4 command). Expected: `Build succeeded` with no warnings in the four new files.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Theatre/StageButton.cs", "Chaos.Client/Controls/World/Popups/Theatre/StageSlider.cs", "Chaos.Client/Controls/World/Popups/Theatre/StageColorPicker.cs", "Chaos.Client/Controls/World/Popups/Theatre/StageView.cs"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/spotlights-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server", "acceptanceCriteria": ["StageButton", "StageSlider", "StageColorPicker", "StageView"], "modelTier": "standard"}
```

---

### Task 10: The Stage Lighting window and its wiring

**Goal:** The director's window (layout `board-compact.html`), opened by the server, sending edits, minimizing to a bar and dragging by its title.

**Files:**
- Create: `CLI/Chaos.Client/Controls/World/Popups/Theatre/StageLightingControl.cs`
- Modify: `CLI/Chaos.Client/Screens/WorldScreen.StageLighting.cs` (window field, board handler, refresh, reset)
- Modify: `CLI/Chaos.Client/Screens/WorldScreen.cs` (create + add to Root)
- Modify: `CLI/CLAUDE.md` (Popups list)

**Acceptance Criteria:**
- [ ] Opens centred (or where it was last dragged), 380×264; minimizes to 230×18 in place; restores; stays on screen
- [ ] Chips select lights; Add (disabled at 8) selects the new light; Remove removes the selected one
- [ ] Swatches, custom picker (live, throttled), size, beam, brightness (live), effect and motion pickers with speed sliders all send UpdateLight
- [ ] Follow: picking Follow waits for a person click and shows "Click a person"; the click sends Follow with that id
- [ ] Dragging a light or handle moves it locally at once and sends ≤ 8/s, with the final value on release
- [ ] House dimmer (live), Blackout, Stage glow; scenes Load, Save as (name box, 24 chars), Delete (second click within 3 s)
- [ ] A held slider is not overwritten by server updates
- [ ] Server Close hides it; a real map change hides it and resets the panel state; it never takes keyboard focus except the scene-name box

**Verify:** build (Task 4 command) → `Build succeeded`; full client test run passes.

**Steps:**

- [ ] **Step 1: Create `StageLightingControl.cs`**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     The director's Stage Lighting window (layout board-compact.html). Left: the diamond stage view and a chip per
///     light. Right: the selected light's colour, size, beam, brightness, effect and motion. Bottom: house lights,
///     Blackout, stage glow and scenes. Drag the title bar to move it; "-" shrinks it to a bar that keeps the dimmer and
///     Blackout. It never takes the keyboard (except the scene-name box), so the director can keep walking.
/// </summary>
public sealed class StageLightingControl : UIPanel
{
    public const int FULL_WIDTH = 380;
    public const int FULL_HEIGHT = 264;
    public const int MINI_WIDTH = 230;
    public const int MINI_HEIGHT = 18;

    private const int TITLE_HEIGHT = 16;
    private const int PAD = 6;
    private const int RIGHT_X = 212;
    private const int FOOTER_Y = 198;
    private const int DELETE_CONFIRM_MS = 3000;

    private static readonly string[] EffectNames = ["None", "Pulse", "Flicker", "Cycle"];
    private static readonly string[] MotionNames = ["Still", "Sweep", "Circle", "Follow"];
    private static readonly string[] SizeNames = ["S", "M", "L"];

    private readonly StageButton AddButton;
    private readonly StageButton BeamButton;
    private readonly StageButton BlackoutButton;
    private readonly StageSlider Brightness;
    private readonly StageButton CancelButton;
    private readonly StageButton[] Chips = new StageButton[StageLightingPanelState.MAX_LIGHTS];
    private readonly StageButton CloseButton;
    private readonly UILabel CountLabel;
    private readonly StageButton CustomSwatch;
    private readonly StageButton DeleteButton;
    private readonly UILabel EffectLabel;
    private readonly StageSlider EffectSpeed;
    private readonly UIPanel Full;
    private readonly StageButton GlowButton;
    private readonly UILabel HintLabel;
    private readonly StageSlider House;
    private readonly UILabel HouseLabel;
    private readonly StageButton LoadButton;
    private readonly UIPanel Mini;
    private readonly StageSlider MiniHouse;
    private readonly UILabel MiniHouseLabel;
    private readonly StageButton MinimizeButton;
    private readonly UILabel MotionLabel;
    private readonly StageSlider MotionSpeed;
    private readonly StageColorPicker Picker;
    private readonly StageButton RemoveButton;
    private readonly StageButton SaveAsButton;
    private readonly StageButton SaveButton;
    private readonly CustomComboBox SceneList;
    private readonly CustomTextBox SceneName;
    private readonly UIPanel Settings;
    private readonly StageButton[] SizeButtons = new StageButton[3];
    private readonly StageButton[] Swatches;
    private readonly UIPanel TitleBand;
    private readonly UILabel TitleLabel;
    private readonly StageView View;

    private long DeleteArmedUntil;
    private bool DraggingWindow;
    private Point Grab;
    private bool SaveMode;

    public StageLightingControl()
    {
        Name = "StageLighting";
        Visible = false;
        Width = FULL_WIDTH;
        Height = FULL_HEIGHT;
        BackgroundColor = new Color(28, 24, 20);
        BorderColor = new Color(138, 109, 59);

        //── title bar ──
        TitleBand = new UIPanel
        {
            X = 1,
            Y = 1,
            Width = FULL_WIDTH - 2,
            Height = TITLE_HEIGHT - 1,
            BackgroundColor = new Color(42, 34, 26),
            IsHitTestVisible = false,
            ZIndex = -1
        };

        AddChild(TitleBand);
        TitleLabel = Label(this, "STAGE LIGHTING", PAD, 2, 120, LegendColors.Gold);
        MinimizeButton = Button(this, "-", FULL_WIDTH - 40, 1, 16, () => SetMinimized(true));
        MinimizeButton.Height = 14;
        CloseButton = Button(this, "x", FULL_WIDTH - 21, 1, 16, Hide);
        CloseButton.Height = 14;

        //── full body (under the title) ──
        Full = new UIPanel
        {
            X = 0,
            Y = TITLE_HEIGHT,
            Width = FULL_WIDTH,
            Height = FULL_HEIGHT - TITLE_HEIGHT
        };

        AddChild(Full);

        View = new StageView { X = PAD, Y = 4 };
        View.LightPressed += id => SelectLight(id);
        View.LightDragged += (id, tile) => MoveLight(id, tile, handle: false);
        View.HandleDragged += (id, tile) => MoveLight(id, tile, handle: true);
        View.DragEnded += EndContinuous;
        View.PersonPicked += PickFollowTarget;
        Full.AddChild(View);

        for (var i = 0; i < Chips.Length; i++)
        {
            var index = i;
            Chips[i] = Button(Full, string.Empty, PAD + (i * 20), 158, 18, () => SelectChip(index));
        }

        AddButton = Button(Full, "+ Add", PAD, 178, 44, AddLight);
        RemoveButton = Button(Full, "Remove", PAD + 48, 178, 48, RemoveLight);
        CountLabel = Label(Full, "0/8", PAD + 100, 180, 40, LegendColors.Gray);

        //── the selected light's settings ──
        Settings = new UIPanel
        {
            X = RIGHT_X,
            Y = 0,
            Width = FULL_WIDTH - RIGHT_X - PAD,
            Height = 190
        };

        Full.AddChild(Settings);

        Label(Settings, "COLOR", 0, 4, 60, LegendColors.Gray);
        Swatches = new StageButton[StageLightingPanelState.Swatches.Length];

        for (var i = 0; i < Swatches.Length; i++)
        {
            var colour = StageLightingPanelState.Swatches[i];
            Swatches[i] = Button(Settings, string.Empty, (i % 7) * 16, 18 + ((i / 7) * 16), 14, () => SetColour(colour, continuous: false));
            Swatches[i].Height = 14;
            Swatches[i].Fill = colour;
        }

        CustomSwatch = Button(Settings, string.Empty, 6 * 16, 34, 14, OpenPicker);
        CustomSwatch.Height = 14;
        CustomSwatch.Rainbow = true;

        for (var i = 0; i < SizeButtons.Length; i++)
        {
            var size = (StageLightSize)i;
            SizeButtons[i] = Button(Settings, SizeNames[i], i * 20, 54, 18, () => EditSelected(light => light with { Size = size }));
        }

        Label(Settings, "Beam", 70, 56, 30, LegendColors.Silver);
        BeamButton = Button(Settings, "On", 104, 54, 32, () => EditSelected(light => light with { Beam = !light.Beam }));

        Label(Settings, "Bright", 0, 76, 38, LegendColors.Silver);
        Brightness = Slider(Settings, 0, 100, 40, 76, 120);
        Brightness.ValueChanged += value => EditSelected(light => light with { Brightness = (byte)value }, continuous: true);
        Brightness.DragEnded += () => EndContinuous(WorldState.StageLightingPanel.SelectedLightId ?? 0);

        Label(Settings, "EFFECT", 0, 92, 60, LegendColors.Gray);
        Button(Settings, "<", 0, 106, 16, () => StepEffect(-1));
        EffectLabel = Label(Settings, "None", 18, 108, 50, LegendColors.White, HorizontalAlignment.Center);
        Button(Settings, ">", 70, 106, 16, () => StepEffect(1));
        EffectSpeed = Slider(Settings, 1, 5, 92, 108, 70);
        EffectSpeed.ValueChanged += value => EditSelected(light => light with { EffectSpeed = (byte)value });

        Label(Settings, "MOTION", 0, 126, 60, LegendColors.Gray);
        Button(Settings, "<", 0, 140, 16, () => StepMotion(-1));
        MotionLabel = Label(Settings, "Still", 18, 142, 50, LegendColors.White, HorizontalAlignment.Center);
        Button(Settings, ">", 70, 140, 16, () => StepMotion(1));
        MotionSpeed = Slider(Settings, 1, 5, 92, 142, 70);
        MotionSpeed.ValueChanged += value => EditSelected(light => light with { MotionSpeed = (byte)value });

        HintLabel = Label(Settings, string.Empty, 0, 162, Settings.Width, LegendColors.Gray);

        //── footer: house lights and scenes ──
        var separator = new UIPanel
        {
            X = PAD,
            Y = FOOTER_Y - 4,
            Width = FULL_WIDTH - (2 * PAD),
            Height = 1,
            BackgroundColor = new Color(61, 51, 38),
            IsHitTestVisible = false
        };

        Full.AddChild(separator);

        Label(Full, "House", PAD, FOOTER_Y + 2, 34, LegendColors.Silver);
        House = Slider(Full, 0, 100, PAD + 36, FOOTER_Y + 2, 110);
        House.ValueChanged += value => SetHouse(value);
        House.DragEnded += FlushContinuous;
        HouseLabel = Label(Full, "100%", PAD + 150, FOOTER_Y + 2, 30, LegendColors.White);
        BlackoutButton = Button(Full, "Blackout", PAD + 184, FOOTER_Y, 56, Blackout);
        GlowButton = Button(Full, "[x] Stage glow", PAD + 246, FOOTER_Y, 116, ToggleGlow);

        SceneList = new CustomComboBox(150) { X = PAD, Y = FOOTER_Y + 22 };
        Full.AddChild(SceneList);

        SceneName = new CustomTextBox
        {
            X = PAD,
            Y = FOOTER_Y + 22,
            Width = 150,
            Height = CustomButton.HEIGHT,
            MaxLength = 24,
            HintText = "Scene name",
            Visible = false
        };

        Full.AddChild(SceneName);

        LoadButton = Button(Full, "Load", PAD + 156, FOOTER_Y + 25, 40, LoadScene);
        SaveAsButton = Button(Full, "Save as", PAD + 200, FOOTER_Y + 25, 52, EnterSaveMode);
        DeleteButton = Button(Full, "Delete", PAD + 256, FOOTER_Y + 25, 52, DeleteScene);
        SaveButton = Button(Full, "Save", PAD + 156, FOOTER_Y + 25, 40, SaveScene);
        CancelButton = Button(Full, "Cancel", PAD + 200, FOOTER_Y + 25, 52, ExitSaveMode);
        SaveButton.Visible = false;
        CancelButton.Visible = false;

        Picker = new StageColorPicker
        {
            X = RIGHT_X - 30,
            Y = 4,
            ZIndex = 10
        };

        Picker.ColorChanged += colour => SetColour(colour, continuous: true);
        Picker.DragEnded += () => EndContinuous(WorldState.StageLightingPanel.SelectedLightId ?? 0);
        Full.AddChild(Picker);

        //── minimized bar ──
        Mini = new UIPanel
        {
            X = 0,
            Y = 0,
            Width = MINI_WIDTH,
            Height = MINI_HEIGHT,
            Visible = false
        };

        AddChild(Mini);
        Label(Mini, "Lights", 4, 3, 36, LegendColors.Gold);
        MiniHouse = Slider(Mini, 0, 100, 42, 3, 60);
        MiniHouse.ValueChanged += value => SetHouse(value);
        MiniHouse.DragEnded += FlushContinuous;
        MiniHouseLabel = Label(Mini, "100%", 106, 3, 26, LegendColors.White);
        Button(Mini, "Blackout", 134, 1, 52, Blackout);
        Button(Mini, "+", 190, 1, 16, () => SetMinimized(false));
        Button(Mini, "x", 210, 1, 16, Hide);
    }

    /// <summary>Raised with each edit to send to the server.</summary>
    public event Action<StageLightingInteractionArgs>? EditRequested;

    private static StageLightingPanelState Panel => WorldState.StageLightingPanel;
    private static StageLightAnimator Setup => WorldState.StageLights;

    public void Show()
    {
        var position = Panel.Position
                       ?? new Point((ChaosGame.VIRTUAL_WIDTH - FULL_WIDTH) / 2, Math.Max(0, ((ChaosGame.VIRTUAL_HEIGHT - FULL_HEIGHT) / 2) - 40));

        X = position.X;
        Y = position.Y;
        ApplyMode();
        Visible = true;
        RefreshFromState();
    }

    public void Hide()
    {
        if (!Visible)
            return;

        Picker.Visible = false;
        SceneList.Close();
        ExitSaveMode();
        DraggingWindow = false;
        Visible = false;
    }

    /// <summary>Repaints every control from the setup and the panel state. Called on each setup and scene-list message.</summary>
    public void RefreshFromState()
    {
        var lights = Setup.Lights;
        Panel.Reconcile(lights);
        var selected = Selected();

        for (var i = 0; i < Chips.Length; i++)
        {
            var chip = Chips[i];
            chip.Visible = i < lights.Count;

            if (!chip.Visible)
                continue;

            var light = lights[i];
            chip.Caption = light.Id.ToString();
            chip.Dot = new Color(light.R, light.G, light.B);
            chip.Selected = light.Id == Panel.SelectedLightId;
        }

        AddButton.Enabled = lights.Count < StageLightingPanelState.MAX_LIGHTS;
        RemoveButton.Enabled = selected is not null;
        CountLabel.Text = $"{lights.Count}/{StageLightingPanelState.MAX_LIGHTS}";

        View.SelectedLightId = Panel.SelectedLightId;
        View.PickingFollow = Panel.AwaitingFollowPick;
        Settings.Enabled = selected is not null;

        if (selected is not null)
        {
            var colour = new Color(selected.R, selected.G, selected.B);

            for (var i = 0; i < Swatches.Length; i++)
                Swatches[i].Selected = StageLightingPanelState.Swatches[i] == colour;

            CustomSwatch.Selected = !StageLightingPanelState.Swatches.Contains(colour);

            for (var i = 0; i < SizeButtons.Length; i++)
                SizeButtons[i].Selected = (int)selected.Size == i;

            BeamButton.Caption = selected.Beam ? "On" : "Off";
            BeamButton.Selected = selected.Beam;

            if (!Brightness.IsDragging)
                Brightness.SetValue(selected.Brightness);

            EffectLabel.Text = EffectNames[(int)selected.Effect];

            if (!EffectSpeed.IsDragging)
                EffectSpeed.SetValue(selected.EffectSpeed);

            var motion = Panel.AwaitingFollowPick ? StageLightMotion.Follow : selected.Motion;
            MotionLabel.Text = MotionNames[(int)motion];

            if (!MotionSpeed.IsDragging)
                MotionSpeed.SetValue(selected.MotionSpeed);

            HintLabel.Text = motion switch
            {
                StageLightMotion.Sweep                               => "Drag the square: swing end.",
                StageLightMotion.Circle                              => "Drag the square: circle size.",
                StageLightMotion.Follow when Panel.AwaitingFollowPick => "Click a person on the stage.",
                StageLightMotion.Follow                              => "Following. Pick again to change.",
                _                                                    => "Drag the light to move it."
            };
        } else
        {
            HintLabel.Text = Setup.HasSetup ? "Add a light to start." : string.Empty;
            Picker.Visible = false;
        }

        if (!House.IsDragging)
            House.SetValue(Setup.HouseLevel);

        if (!MiniHouse.IsDragging)
            MiniHouse.SetValue(Setup.HouseLevel);

        HouseLabel.Text = $"{Setup.HouseLevel}%";
        MiniHouseLabel.Text = HouseLabel.Text;
        GlowButton.Caption = Setup.StageGlow ? "[x] Stage glow" : "[ ] Stage glow";

        var current = SceneList.SelectedItem;
        var scenes = Panel.Scenes;
        SceneList.SetItems(scenes, Math.Max(0, scenes.ToList().FindIndex(name => name == current)));
        LoadButton.Enabled = scenes.Count > 0;
        DeleteButton.Enabled = scenes.Count > 0;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if ((DeleteArmedUntil != 0) && (Environment.TickCount64 > DeleteArmedUntil))
        {
            DeleteArmedUntil = 0;
            DeleteButton.Caption = "Delete";
        }
    }

    //── window dragging (title bar, or anywhere on the minimized bar) ──

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        if (!Panel.IsMinimized && ((e.ScreenY - ScreenY) >= TITLE_HEIGHT))
            return;

        DraggingWindow = true;
        Grab = new Point(e.ScreenX - X, e.ScreenY - Y);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!DraggingWindow)
            return;

        X = e.ScreenX - Grab.X;
        Y = e.ScreenY - Grab.Y;
        KeepOnScreen();
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (!DraggingWindow)
            return;

        DraggingWindow = false;
        Panel.Position = new Point(X, Y);
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();
        DraggingWindow = false;
    }

    //── edits ──

    private StageLightInfo? Selected()
        => Panel.SelectedLightId is { } id ? Setup.Lights.FirstOrDefault(light => light.Id == id) : null;

    private void Send(StageLightingInteractionArgs edit) => EditRequested?.Invoke(edit);

    private void SendContinuous(StageLightingInteractionArgs edit)
    {
        if (Panel.Throttle(edit, Environment.TickCount64) is { } now)
            Send(now);
    }

    private void FlushContinuous()
    {
        if (Panel.Flush(Environment.TickCount64) is { } last)
            Send(last);
    }

    /// <summary>Ends a drag on a light's setting: sends the held edit and lets the server's copy take over once it matches.</summary>
    private void EndContinuous(byte lightId)
    {
        FlushContinuous();
        Setup.Release(lightId, Environment.TickCount64);
        RefreshFromState();
    }

    /// <summary>Applies <paramref name="change" /> to the selected light and sends it. Continuous edits show at once and are throttled.</summary>
    private void EditSelected(Func<StageLightInfo, StageLightInfo> change, bool continuous = false)
    {
        if (Selected() is not { } light)
            return;

        var edited = change(light);
        var edit = new StageLightingInteractionArgs
        {
            Action = StageLightingAction.UpdateLight,
            LightId = edited.Id,
            Light = edited
        };

        if (continuous)
        {
            Setup.Hold(edited);
            SendContinuous(edit);
        } else
            Send(edit);

        RefreshFromState();
    }

    private void SelectLight(byte id)
    {
        Panel.Select(id);
        Picker.Visible = false;
        RefreshFromState();
    }

    private void SelectChip(int index)
    {
        if (index < Setup.Lights.Count)
            SelectLight(Setup.Lights[index].Id);
    }

    private void AddLight()
    {
        if (Setup.Stage.IsEmpty)
            return;

        Panel.ExpectNewLight();
        Send(new StageLightingInteractionArgs { Action = StageLightingAction.AddLight, Light = StageLightingPanelState.NewLight(Setup.Stage) });
    }

    private void RemoveLight()
    {
        if (Panel.SelectedLightId is not { } id)
            return;

        Send(new StageLightingInteractionArgs { Action = StageLightingAction.RemoveLight, LightId = id });
    }

    private void MoveLight(byte id, Vector2 tile, bool handle)
    {
        var light = Setup.Lights.FirstOrDefault(l => l.Id == id);

        if (light is null)
            return;

        var (x, y) = StageViewGeometry.ToUnits(tile, Setup.Stage);
        var moved = handle ? light with { X2 = x, Y2 = y } : light with { X = x, Y = y };

        //a followed light dragged by hand stops following
        if (!handle && (moved.Motion == StageLightMotion.Follow))
            moved = moved with { Motion = StageLightMotion.Still, FollowId = 0 };

        Setup.Hold(moved);
        SendContinuous(new StageLightingInteractionArgs { Action = StageLightingAction.UpdateLight, LightId = id, Light = moved });
        RefreshFromState();
    }

    private void SetColour(Color colour, bool continuous)
        => EditSelected(light => light with { R = colour.R, G = colour.G, B = colour.B }, continuous);

    private void OpenPicker()
    {
        if (Selected() is not { } light)
            return;

        Picker.Open(new Color(light.R, light.G, light.B));
    }

    private void StepEffect(int direction)
        => EditSelected(light => light with { Effect = StageLightingPanelState.Step(light.Effect, direction) });

    private void StepMotion(int direction)
    {
        if (Selected() is not { } light)
            return;

        var current = Panel.AwaitingFollowPick ? StageLightMotion.Follow : light.Motion;
        var next = StageLightingPanelState.Step(current, direction);

        if (next == StageLightMotion.Follow)
        {
            //nothing is sent until the director clicks a person
            Panel.BeginFollowPick();
            RefreshFromState();

            return;
        }

        Panel.EndFollowPick();
        EditSelected(l => StageLightingPanelState.WithMotion(l, next, Setup.Stage));
    }

    private void PickFollowTarget(uint entityId)
    {
        Panel.EndFollowPick();
        EditSelected(light => light with { Motion = StageLightMotion.Follow, FollowId = entityId });
    }

    private void SetHouse(int level)
    {
        HouseLabel.Text = $"{level}%";
        MiniHouseLabel.Text = HouseLabel.Text;
        SendContinuous(new StageLightingInteractionArgs { Action = StageLightingAction.SetHouseLevel, Level = (byte)level });
    }

    private void Blackout() => Send(new StageLightingInteractionArgs { Action = StageLightingAction.Blackout });

    private void ToggleGlow() => Send(new StageLightingInteractionArgs { Action = StageLightingAction.SetStageGlow, Flag = !Setup.StageGlow });

    //── scenes ──

    private void LoadScene()
    {
        if (SceneList.SelectedItem is { } name)
            Send(new StageLightingInteractionArgs { Action = StageLightingAction.LoadScene, SceneName = name });
    }

    private void EnterSaveMode()
    {
        SaveMode = true;
        SceneName.Text = SceneList.SelectedItem ?? string.Empty;
        ApplySaveMode();
    }

    private void ExitSaveMode()
    {
        SaveMode = false;
        ApplySaveMode();
    }

    private void SaveScene()
    {
        var name = SceneName.Text.Trim();

        if ((name.Length is 0 or > 24) || name.Any(char.IsControl))
            return;

        Send(new StageLightingInteractionArgs { Action = StageLightingAction.SaveScene, SceneName = name });
        ExitSaveMode();
    }

    private void DeleteScene()
    {
        if (SceneList.SelectedItem is not { } name)
            return;

        if (DeleteArmedUntil == 0)
        {
            DeleteArmedUntil = Environment.TickCount64 + DELETE_CONFIRM_MS;
            DeleteButton.Caption = "Sure?";

            return;
        }

        DeleteArmedUntil = 0;
        DeleteButton.Caption = "Delete";
        Send(new StageLightingInteractionArgs { Action = StageLightingAction.DeleteScene, SceneName = name });
    }

    private void ApplySaveMode()
    {
        SceneList.Visible = !SaveMode;
        LoadButton.Visible = !SaveMode;
        SaveAsButton.Visible = !SaveMode;
        DeleteButton.Visible = !SaveMode;
        SceneName.Visible = SaveMode;
        SaveButton.Visible = SaveMode;
        CancelButton.Visible = SaveMode;
    }

    //── layout ──

    private void SetMinimized(bool minimized)
    {
        Panel.IsMinimized = minimized;
        Picker.Visible = false;
        ApplyMode();
    }

    private void ApplyMode()
    {
        var minimized = Panel.IsMinimized;
        Full.Visible = !minimized;
        Mini.Visible = minimized;
        TitleBand.Visible = !minimized;
        TitleLabel.Visible = !minimized;
        MinimizeButton.Visible = !minimized;
        CloseButton.Visible = !minimized;
        Width = minimized ? MINI_WIDTH : FULL_WIDTH;
        Height = minimized ? MINI_HEIGHT : FULL_HEIGHT;
        KeepOnScreen();
    }

    private void KeepOnScreen()
    {
        X = Math.Clamp(X, 0, ChaosGame.VIRTUAL_WIDTH - Width);
        Y = Math.Clamp(Y, 0, ChaosGame.VIRTUAL_HEIGHT - Height);
    }

    private static StageButton Button(UIPanel parent, string caption, int x, int y, int width, Action onClick)
    {
        var button = new StageButton(caption, width) { X = x, Y = y };
        button.Clicked += onClick;
        parent.AddChild(button);

        return button;
    }

    private static StageSlider Slider(UIPanel parent, int min, int max, int x, int y, int width)
    {
        var slider = new StageSlider(min, max) { X = x, Y = y, Width = width };
        parent.AddChild(slider);

        return slider;
    }

    private static UILabel Label(
        UIPanel parent,
        string text,
        int x,
        int y,
        int width,
        Color color,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color,
            IsHitTestVisible = false,
            Text = text
        };

        parent.AddChild(label);

        return label;
    }
}
```

Notes for the implementer:
- If `UIPanel.Enabled` does not disable children's input, keep `Settings.Enabled` anyway (it greys the look where supported) and rely on `EditSelected`'s `Selected() is not { } light` guard, which already makes every settings control a no-op with no selection.
- If `UILabel` pads text so that captions shift, set `PaddingLeft = 0` and `PaddingTop = 0` in `Label`, as `BugReportControl` does for its note label.
- If `CustomComboBox.SetItems` with an empty list leaves a stale header, that is its existing behaviour; do not change `CustomComboBox`.

- [ ] **Step 2: Wire the window in `WorldScreen.StageLighting.cs`.** Add the field and extend the existing members:

```csharp
    private StageLightingControl StageLightingWindow = null!;
```

Replace `WireStageLighting`, `UnwireStageLighting`, `HandleStageLightingState` and `ResetStageLighting` with:

```csharp
    private void WireStageLighting()
    {
        Game.Connection.OnStageLightingState += HandleStageLightingState;
        Game.Connection.OnStageLightingBoard += HandleStageLightingBoard;
        StageLightingWindow.EditRequested += SendStageLightingEdit;
    }

    private void UnwireStageLighting()
    {
        Game.Connection.OnStageLightingState -= HandleStageLightingState;
        Game.Connection.OnStageLightingBoard -= HandleStageLightingBoard;
        StageLightingWindow.EditRequested -= SendStageLightingEdit;
    }

    private void SendStageLightingEdit(StageLightingInteractionArgs args) => Game.Connection.SendStageLightingInteraction(args);

    private void HandleStageLightingState(StageLightingStateArgs args)
    {
        WorldState.StageLights.Apply(args, Environment.TickCount64);

        if (StageLightingWindow.Visible)
            StageLightingWindow.RefreshFromState();
    }

    private void HandleStageLightingBoard(StageLightingBoardArgs args)
    {
        switch (args.Type)
        {
            case StageLightingBoardType.Open:
                WorldState.StageLightingPanel.SetScenes(args.SceneNames);
                StageLightingWindow.Show();

                break;

            case StageLightingBoardType.Scenes:
                WorldState.StageLightingPanel.SetScenes(args.SceneNames);

                if (StageLightingWindow.Visible)
                    StageLightingWindow.RefreshFromState();

                break;

            case StageLightingBoardType.Close:
                StageLightingWindow.Hide();

                break;
        }
    }

    /// <summary>Forgets the Theatre lighting and closes the window. Called on a real map change only, before the darkness layer resets.</summary>
    private void ResetStageLighting()
    {
        StageLightingWindow.Hide();
        WorldState.StageLights.Clear();
        WorldState.StageLightingPanel.ResetForNewMap();
        DarknessRenderer.SetHouseDarkness(null);
    }
```

Add usings `Chaos.Client.Controls.World.Popups.Theatre`, `Chaos.DarkAges.Definitions`, `Chaos.Networking.Entities.Client` to that file.

- [ ] **Step 3: Create the window in `WorldScreen.cs`.** Before the `WireStageLighting();` call added in Task 7 (it now subscribes to the window), add:

```csharp
        StageLightingWindow = new StageLightingControl
        {
            ZIndex = 2
        };
```

and, with the other `Root.AddChild(...)` popups (after `Root.AddChild(BugReport);`):

```csharp
        Root.AddChild(StageLightingWindow);
```

Make sure the `WireStageLighting();` call runs after the window is constructed; move it next to `WireBugReport();` if needed.

- [ ] **Step 4: `CLAUDE.md`.** In the Popups line, after the `BugReport/` entry add: ``, `Theatre/` (StageLightingControl — the director's Stage Lighting window; StageView, StageButton, StageSlider, StageColorPicker, StageViewGeometry)``.

- [ ] **Step 5: Build and run the whole client test project.** Expected: build succeeds with no warnings in new files; all tests pass.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Theatre/StageLightingControl.cs", "Chaos.Client/Screens/WorldScreen.StageLighting.cs", "Chaos.Client/Screens/WorldScreen.cs", "CLAUDE.md"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/spotlights-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/spotlights-server", "acceptanceCriteria": ["open/minimize/restore/drag", "chips/add/remove", "all light settings send UpdateLight", "follow pick", "throttled drags + hold", "house/blackout/glow", "scenes save/load/delete", "no overwrite of held slider", "close + map change reset"], "modelTier": "frontier"}
```

---

### Task 11: Commit the full implementation

**Goal:** One commit per repo on `feat/theatre-spotlights`, with the client pointing its submodule at the server branch commit. Nothing is merged or pushed.

**Files:**
- Commit: every file created or changed in Tasks 1–10, the spec, the plan and its `.tasks.json`

**Acceptance Criteria:**
- [ ] `SRV`, `UNO`, `CLI` each have exactly one new commit on `feat/theatre-spotlights`
- [ ] No `appsettings.json` or `launchSettings.json` in any commit
- [ ] The `CLI` commit records `Chaos-Server` at the `SRV` commit
- [ ] `git status --short` in each worktree is empty afterwards (except untracked build output that is already ignored)

**Verify:** `git -C <each worktree> log --oneline -1` shows the new commit; `git -C CLI ls-tree HEAD Chaos-Server` shows the `SRV` commit hash

**Steps:**

- [ ] **Step 1: Final full test runs** (server Theatre + Networking tests, whole client test project). All must pass before committing.

- [ ] **Step 2: Review what will be committed.** `git -C <worktree> status --short` in each. Expect only this plan's files. Anything else: stop and report.

- [ ] **Step 3: Commit the server.**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/spotlights-server
git add Chaos.DarkAges/Definitions/Enums.cs Chaos.Networking.Abstractions/Definitions/Enums.cs \
  Chaos.Networking/Entities/Server/StageLightInfo.cs Chaos.Networking/Entities/Server/StageLightingStateArgs.cs \
  Chaos.Networking/Entities/Server/StageLightingBoardArgs.cs Chaos.Networking/Entities/Client/StageLightingInteractionArgs.cs \
  Chaos.Networking/Converters/StageLightInfoCodec.cs Chaos.Networking/Converters/Server/StageLightingStateConverter.cs \
  Chaos.Networking/Converters/Server/StageLightingBoardConverter.cs Chaos.Networking/Converters/Client/StageLightingInteractionConverter.cs \
  Chaos/Services/Theatre Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs \
  Chaos/Scripting/DialogScripts/Temuair/Suomi/TheatreStageLightingOpenScript.cs Chaos/Scripting/DialogScripts/Temuair/Suomi/SuomiTheatreScript.cs \
  Chaos/Networking/Abstractions/IChaosWorldClient.cs Chaos/Networking/ChaosWorldClient.cs Chaos/Services/Servers/WorldServer.cs \
  Tests/Chaos.Tests/Networking/StageLightingPacketConverterTests.cs Tests/Chaos.Tests/Theatre/StageLightingTests.cs \
  Tests/Chaos.Tests/Theatre/TheatreLightingScenesTests.cs Tests/Chaos.Tests/Theatre/StageLightingRateLimiterTests.cs \
  Tests/Chaos.Tests/Theatre/TheatreLanternsTests.cs
git commit -F- <<'EOF'
Give the Theatre director stage lights: a dimmer and colored spotlights.

The Theatre map now owns a lighting setup: a 0-100% house dimmer, stage glow
and up to 8 spotlights with color, size, brightness, beam, an effect (pulse,
flicker, color cycle) and a motion (sweep, circle, follow). Every edit is
checked and corrected, then the whole setup goes to everyone on the map.
Scenes are saved per theatre and survive restarts. Turn Lights Off/On now
set the dimmer to 0% and 100%.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 4: Commit Unora.**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/spotlights-unora
git add Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_options.json Data/Configuration/Templates/Dialogs/Temauir/suomi/thulin/suomitheatre_stagelighting.json
git commit -F- <<'EOF'
Add Stage Lighting to Thulin's Theatre Options.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 5: Commit the client, pointing the submodule at the server commit.**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/spotlights-client
SRV_SHA=$(git -C /c/Users/Michael/Documents/GitHub/worktrees/spotlights-server rev-parse HEAD)
git update-index --cacheinfo 160000,$SRV_SHA,Chaos-Server
git add Chaos.Client.Networking/ConnectionManager.cs Chaos.Client.Networking/Definitions/Delegates.cs \
  Chaos.Client/Utilities/HsvColor.cs Chaos.Client/Systems/StageLightAnimator.cs Chaos.Client/Systems/LightingSystem.cs \
  Chaos.Client.Rendering/LightSource.cs Chaos.Client.Rendering/DarknessRenderer.cs Chaos.Client.Rendering/SpotlightMasks.cs \
  Chaos.Client.Rendering/SpotlightRenderer.cs Chaos.Client/Collections/WorldState.cs Chaos.Client/ViewModel/StageLightingPanelState.cs \
  Chaos.Client/Controls/World/Popups/Theatre Chaos.Client/Screens/WorldScreen.StageLighting.cs Chaos.Client/Screens/WorldScreen.cs \
  Chaos.Client/Screens/WorldScreen.Update.cs Chaos.Client/Screens/WorldScreen.Draw.cs Chaos.Client/Screens/WorldScreen.Map.cs \
  Tests/Chaos.Client.Tests/StageLightAnimatorTests.cs Tests/Chaos.Client.Tests/SpotlightMaskTests.cs \
  Tests/Chaos.Client.Tests/StageLightingPanelStateTests.cs CLAUDE.md \
  docs/superpowers/specs/2026-09-24-theatre-spotlights-design.md docs/superpowers/plans/2026-09-24-theatre-spotlights.md \
  docs/superpowers/plans/2026-09-24-theatre-spotlights.md.tasks.json
git commit -F- <<'EOF'
Add the Theatre Stage Lighting window and draw colored spotlights.

Spotlights lift the darkness like lanterns and add their color in a new
additive layer after it, with an optional beam. The director's window shows
the stage at the game's angle: drag lights and handles, pick colors (swatches
or a picker), size, brightness, effect and motion, follow an actor, dim the
house lights, and save or load scenes. It can shrink to a bar that keeps the
dimmer and Blackout. Points Chaos-Server at the matching server branch.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 6: Report.** Give each branch's commit hash. Do **not** merge into `main`/`master` or push: the in-game walkthrough (Task 12) is still the user's, and other sessions are active in the shared trees.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/spotlights-client ls-tree HEAD Chaos-Server", "acceptanceCriteria": ["one commit per repo on feat/theatre-spotlights", "no local config files committed", "client submodule at server commit", "clean worktrees"], "modelTier": "mechanical"}
```

---

### Task 12: In-game walkthrough (needs the user)

**Goal:** Confirm the feature in the real game with a server and two clients (director and audience).

**Files:** none

**Acceptance Criteria:**
- [ ] Thulin → Theatre Options → Stage Lighting opens the window for the director; a non-director is refused
- [ ] Three lights added and dragged; each effect and motion looks right to both clients
- [ ] Follow an actor on and off the stage (stops at the edge; returns to its spot when they leave the Theatre)
- [ ] Dim to 30%, Blackout, Turn Lights On from the dialog; stage glow toggles stage lanterns
- [ ] Save, load (1-second fade) and delete scenes; restart the server: scenes kept, live setup reset
- [ ] The audience client walks in mid-show and sees the current setup
- [ ] 8 moving lights keep the frame counter steady

**Verify:** the user's observation in game

**Steps:**

- [ ] **Step 1:** Build the server from `SRV`, patch only its `bin/Debug/net10.0/appsettings.json` staging path (`mewbb` → `Michael`) as in the `chaos-server-local-staging-dir` memory, and run `Chaos.exe` from there.
- [ ] **Step 2:** Run two clients built from `CLI` (with `-p:UnoraServerPath=<SRV>`), one as the Theatre director or an admin.
- [ ] **Step 3:** Walk the acceptance criteria above. Note anything that looks wrong for a follow-up fix.

```json:metadata
{"files": [], "verifyCommand": "", "acceptanceCriteria": ["window opens for director only", "lights, effects and motions", "follow", "dimmer/blackout/glow", "scenes + restart", "late joiner", "performance"], "modelTier": "standard", "requiresUser": true}
```
