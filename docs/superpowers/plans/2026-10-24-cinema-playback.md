# Cinema Playback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players in the Loures 2 Floor Hall (map 15) watch a film, with sound, on a wall screen. Admins run the showing with `/film` commands, and every player's playback stays in step with the server's clock.

**Architecture:** The server keeps one `CinemaShowing` (film, anchor time, anchor position, paused). `/film` changes it and broadcasts a `CinemaState` packet (opcode 171) to everyone in the hall. A `cinemamap` map script sends the current state on entry and ends the film at its length. The client turns each packet into a `CinemaController` command. The controller drives a LibVLC player that streams the HLS film from the film site (Plan 1). It checks drift every 5 s and draws the frame onto the hall wall in the world draw loop. Game music is lowered while a film plays.

**Tech Stack:** C# / .NET 10, MonoGame (client), LibVLCSharp + VideoLAN.LibVLC.Windows, TUnit + FluentAssertions (server tests), the existing client test project.

**Spec:** `C:\Users\Michael\Documents\GitHub\Chaos.Client\docs\superpowers\specs\2026-10-24-movie-theatre-design.md`

## Workspace (the controller sets this up before Task 1)

- Client worktree: `C:\Users\Michael\Documents\GitHub\worktrees\cinema-client`, branch `cinema-playback` from Chaos.Client `main` (`6a42c1152`). Then `git submodule update --init`.
- Server submodule inside it (`worktrees\cinema-client\Chaos-Server`): `git fetch C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server master`, then `git checkout -b cinema-playback FETCH_HEAD`. The fetch is needed because local master `05f2b486b` is not on origin.
- Unora worktree: `C:\Users\Michael\Documents\GitHub\worktrees\cinema-unora`, branch `cinema-playback` from Unora `main` (`0655577bf`).
- All paths below that start with `Chaos-Server/` are inside the client worktree's submodule. Paths that start with `Chaos.Client/`, `Tests/Chaos.Client.Tests/` or `Directory.Packages.props` are in the client worktree root. Paths that start with `Data/` are in the Unora worktree.

## Global Constraints

- Name everything "Cinema" in code. "Theatre" and "StageLighting" are the existing stage lighting feature; do not touch or reuse them.
- Opcode: `ServerOpCode.CinemaState = 171` (hex 0xAB). Re-check that `HallBattleDisplay = 170` is still the last entry first; if not, use last + 1 and update this plan's references.
- Packet field order, fixed: `WriteString8(FilmId)`, `WriteString8(Title)`, `WriteString16(Url)`, `WriteUInt32(PositionMs)`, `WriteBoolean(Paused)`. An empty `Url` means "nothing is showing".
- `CLIENT_VERSION` goes from 805 to 806 in `Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs`.
- Hall: map instance id `loures_2_floor_hall`, client map id 15.
- Titles are clipped to 60 characters on the server before they go in a packet.
- Client sync numbers: check drift every 5000 ms, retry a failed film after 15000 ms, seek when drift is over 1500 ms.
- Never seek while VLC is buffering (`Playing` is false).
- Video format from VLC: RV32, 854×480, pitch 854×4.
- Music duck during films: down to 0.2 over 1 s, back up over 2 s.
- The film site's ready list is `GET {FilmSiteUrl}/api/films/ready`, returning `[{ "id", "title", "lengthSeconds" (number or null), "url" }]`. `url` is already absolute.
- `/film` must answer within `TimeoutSeconds` (10) when the film site is down or slow.
- Commit strategy is at-end: implementers do NOT commit. Leave all changes in the working tree. Task 10 makes the commits.
- Shared checkouts: stage by explicit path only. Never stash, reset, or `git add -A`.
- Never write the word that follows "Bearer" in an auth header into any file (a read filter redacts it).
- Comments only where the reason is not obvious from the code. No explanatory comments in tests.

**User decisions (already made):**
- Films play in the Loures 2 Floor Hall (map 15) on a castle-wall screen; the real screen spot comes in Plan 3.
- Admins control showings with `/film list | play <name> | pause | resume | seek <time> | stop`.
- Players get Cinema options: film volume, mute, hide video, lower game music (on by default).
- Films must be public domain, Creative Commons, or licensed.
- Muse implements; Claude plans and reviews; no push or deploy without asking the user.

## Review Focus

1. VLC reports `TimeMs` as 0 while it fills its cache. The client must not seek again and again during buffering. (Task 7 test: no seek while `Playing` is false.)
2. When the film site is down or slow, `/film list` and `/film play` reply within the timeout with a plain message. (Task 3 test: `GetReadyFilmsAsync` returns null on a failing handler.)
3. A film with an unknown length never ends on its own. (Task 2 test.)
4. Pause followed by seek stays paused at the new spot, on the server and on the client. (Task 2 and Task 7 tests.)
5. Long or Korean titles fit in `String8` (255 bytes in code page 949). (Task 1 round-trip with a Korean title; Task 3 test that the service clips to 60 characters.)

## Test commands

- Server: `dotnet build Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj`, then `dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj --no-build`. Add `-- --treenode-filter "/*/*/<ClassName>/*"` to run one class.
- Client: `dotnet build Chaos.Client.slnx`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`. Plain `dotnet test` does not work on this SDK.
- A running `Chaos.exe` or client locks the build output. Stop it first.

---

### Task 1: CinemaState packet

**Goal:** A `CinemaStateArgs` packet with opcode 171 that round-trips, plus the server's `SendCinemaState` method and the client version bump.

**Files:**
- Create: `Chaos-Server/Chaos.Networking/Entities/Server/CinemaStateArgs.cs`
- Create: `Chaos-Server/Chaos.Networking/Converters/Server/CinemaStateConverter.cs`
- Modify: `Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs` (after `HallBattleDisplay = 170,` near line 1594)
- Modify: `Chaos-Server/Chaos/Networking/Abstractions/IChaosWorldClient.cs` (after `SendStageLightingState`, line 355)
- Modify: `Chaos-Server/Chaos/Networking/ChaosWorldClient.cs` (after `SendStageLightingState`, line 1144)
- Modify: `Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs:23`
- Test: `Chaos-Server/Tests/Chaos.Tests/Networking/CinemaPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `ServerOpCode.CinemaState` equals 171.
- [ ] A state with a Korean title, a long URL, position 5,400,000 ms and `Paused = true` round-trips equal.
- [ ] A stop message (all defaults) round-trips to empty strings, position 0, not paused.
- [ ] `CONSTANTS.CLIENT_VERSION` is 806.
- [ ] Server solution builds.

**Verify:** `dotnet build Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj && dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter "/*/*/CinemaPacketConverterTests/*"` → 3 tests pass.

**Steps:**

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Tests.Networking;

public sealed class CinemaPacketConverterTests
{
    private static readonly Encoding Enc = Encoding.GetEncoding(949);

    private static CinemaStateArgs RoundTrip(CinemaStateArgs original)
    {
        var converter = new CinemaStateConverter();
        var writer = new SpanWriter(Enc, usePooling: false);
        converter.Serialize(ref writer, original);
        var bytes = writer.ToSpan()
                          .ToArray();

        var reader = new SpanReader(Enc, bytes);

        return converter.Deserialize(ref reader);
    }

    [Test]
    public void Opcode_is_171()
        => ((byte)ServerOpCode.CinemaState).Should().Be(171);

    [Test]
    public void Playing_state_with_korean_title_round_trips()
    {
        var original = new CinemaStateArgs
        {
            FilmId = "night-of-the-living-dead",
            Title = "살아있는 시체들의 밤 (1968)",
            Url = "https://films.example.duckdns.org/films/night-of-the-living-dead/index.m3u8?" + new string('a', 400),
            PositionMs = 5_400_000,
            Paused = true
        };

        RoundTrip(original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Stop_message_round_trips_empty()
    {
        var result = RoundTrip(new CinemaStateArgs());

        result.FilmId.Should().BeEmpty();
        result.Title.Should().BeEmpty();
        result.Url.Should().BeEmpty();
        result.PositionMs.Should().Be(0);
        result.Paused.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the test to see it fail**

Run the Verify command. Expected: build error, `CinemaStateArgs` and `CinemaStateConverter` not found.

- [ ] **Step 3: Add the opcode**

In `Enums.cs`, after `HallBattleDisplay = 170,`:

```csharp

    /// <summary>
    ///     OpCode used to send the Cinema's current film, position and pause state. An empty url means nothing is showing.
    ///     <br />
    ///     Hex value: 0xAB
    /// </summary>
    CinemaState = 171,
```

- [ ] **Step 4: Add the args record**

`Chaos-Server/Chaos.Networking/Entities/Server/CinemaStateArgs.cs`:

```csharp
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.CinemaState" /> packet: what the Cinema is showing.
///     An empty <see cref="Url" /> means nothing is showing.
/// </summary>
public sealed record CinemaStateArgs : IPacketSerializable
{
    /// <summary>The film site's id for the film.</summary>
    public string FilmId { get; set; } = "";

    /// <summary>The film's title, at most 60 characters.</summary>
    public string Title { get; set; } = "";

    /// <summary>The film's absolute HLS playlist url. Empty when nothing is showing.</summary>
    public string Url { get; set; } = "";

    /// <summary>Where the film is right now, in milliseconds from its start.</summary>
    public uint PositionMs { get; set; }

    /// <summary>Whether the film is paused at <see cref="PositionMs" />.</summary>
    public bool Paused { get; set; }
}
```

- [ ] **Step 5: Add the converter**

`Chaos-Server/Chaos.Networking/Converters/Server/CinemaStateConverter.cs`:

```csharp
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="CinemaStateArgs" /></summary>
public sealed class CinemaStateConverter : PacketConverterBase<CinemaStateArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.CinemaState;

    /// <inheritdoc />
    public override CinemaStateArgs Deserialize(ref SpanReader reader)
        => new()
        {
            FilmId = reader.ReadString8(),
            Title = reader.ReadString8(),
            Url = reader.ReadString16(),
            PositionMs = reader.ReadUInt32(),
            Paused = reader.ReadBoolean()
        };

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, CinemaStateArgs args)
    {
        writer.WriteString8(args.FilmId);
        writer.WriteString8(args.Title);
        writer.WriteString16(args.Url);
        writer.WriteUInt32(args.PositionMs);
        writer.WriteBoolean(args.Paused);
    }
}
```

If `SpanReader`/`SpanWriter` name the 8-bit and 16-bit length-prefixed string methods differently, use the names other converters in `Chaos.Networking/Converters/Server/` use (search for `ReadString8`).

- [ ] **Step 6: Add the send method**

`IChaosWorldClient.cs`, after `void SendStageLightingState(StageLightingStateArgs args);`:

```csharp

    /// <summary>Sends what the Cinema is showing. Empty args mean nothing is showing.</summary>
    void SendCinemaState(CinemaStateArgs args);
```

`ChaosWorldClient.cs`, after `public void SendStageLightingState(StageLightingStateArgs args) => Send(args);`:

```csharp

    /// <inheritdoc />
    public void SendCinemaState(CinemaStateArgs args) => Send(args);
```

Use `/// <inheritdoc />` only if the neighbouring methods use it; otherwise match them.

- [ ] **Step 7: Bump the client version**

`CONSTANTS.cs:23`: `public const ushort CLIENT_VERSION = 806;`

- [ ] **Step 8: Run the tests to see them pass**

Run the Verify command. Expected: 3 pass. Then run the whole server test project once and confirm no new failures.

```json:metadata
{"files": ["Chaos-Server/Chaos.Networking/Entities/Server/CinemaStateArgs.cs", "Chaos-Server/Chaos.Networking/Converters/Server/CinemaStateConverter.cs", "Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos-Server/Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos-Server/Chaos/Networking/ChaosWorldClient.cs", "Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos-Server/Tests/Chaos.Tests/Networking/CinemaPacketConverterTests.cs"], "verifyCommand": "dotnet build Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj && dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter \"/*/*/CinemaPacketConverterTests/*\"", "acceptanceCriteria": ["ServerOpCode.CinemaState equals 171", "Korean-title playing state round-trips equal", "Stop message round-trips empty", "CLIENT_VERSION is 806", "Server solution builds"], "modelTier": "mechanical"}
```

---

### Task 2: Pure cinema types on the server

**Goal:** Server-side types with no engine calls: a film record, a showing clock, a time parser and a film-name matcher, all unit tested.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Cinema/CinemaFilm.cs`
- Create: `Chaos-Server/Chaos/Services/Cinema/CinemaShowing.cs`
- Create: `Chaos-Server/Chaos/Services/Cinema/CinemaTime.cs`
- Create: `Chaos-Server/Chaos/Services/Cinema/CinemaFilmMatcher.cs`
- Test: `Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaShowingTests.cs`
- Test: `Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaTimeTests.cs`
- Test: `Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaFilmMatcherTests.cs`

**Acceptance Criteria:**
- [ ] `Position` grows with time while playing and holds still while paused.
- [ ] `Seek` while paused stays paused at the new spot.
- [ ] Position is clamped to 0..length when the length is known.
- [ ] `IsOver` is true at or past the length when playing; never true when paused; never true when the length is unknown.
- [ ] `CinemaTime.TryParse` accepts `mm:ss` (minutes may be over 59) and `h:mm:ss` (mm and ss under 60) and rejects everything else.
- [ ] `CinemaTime.Format` writes `m:ss` under an hour and `h:mm:ss` from an hour.
- [ ] `CinemaFilmMatcher.Match` prefers an exact id, then an exact title (case-insensitive), then a unique title that contains the name; several containing titles return `Ambiguous = true`.

**Verify:** `dotnet build Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj && dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter "/*/Chaos.Tests.Cinema/*/*"` → all Cinema tests pass.

**Steps:**

- [ ] **Step 1: Write the failing tests**

`Tests/Chaos.Tests/Cinema/CinemaShowingTests.cs`:

```csharp
using Chaos.Services.Cinema;
using FluentAssertions;

namespace Chaos.Tests.Cinema;

public sealed class CinemaShowingTests
{
    private static readonly DateTime T0 = new(2026, 10, 24, 20, 0, 0, DateTimeKind.Utc);
    private static readonly CinemaFilm Film = new("nosferatu", "Nosferatu", TimeSpan.FromMinutes(94), "https://x/films/nosferatu/index.m3u8");
    private static readonly CinemaFilm NoLength = new("live", "Live", null, "https://x/films/live/index.m3u8");

    [Test]
    public void Start_plays_from_zero()
    {
        var s = new CinemaShowing();
        s.Start(Film, T0);

        s.Film.Should().Be(Film);
        s.Paused.Should().BeFalse();
        s.Position(T0.AddSeconds(30)).Should().Be(TimeSpan.FromSeconds(30));
    }

    [Test]
    public void Pause_holds_position_and_resume_continues()
    {
        var s = new CinemaShowing();
        s.Start(Film, T0);
        s.Pause(T0.AddSeconds(10));

        s.Paused.Should().BeTrue();
        s.Position(T0.AddMinutes(5)).Should().Be(TimeSpan.FromSeconds(10));

        s.Resume(T0.AddMinutes(5));
        s.Position(T0.AddMinutes(5).AddSeconds(2)).Should().Be(TimeSpan.FromSeconds(12));
    }

    [Test]
    public void Seek_while_paused_stays_paused_at_new_spot()
    {
        var s = new CinemaShowing();
        s.Start(Film, T0);
        s.Pause(T0.AddSeconds(10));
        s.Seek(TimeSpan.FromMinutes(30), T0.AddSeconds(20));

        s.Paused.Should().BeTrue();
        s.Position(T0.AddMinutes(10)).Should().Be(TimeSpan.FromMinutes(30));
    }

    [Test]
    public void Seek_while_playing_continues_from_new_spot()
    {
        var s = new CinemaShowing();
        s.Start(Film, T0);
        s.Seek(TimeSpan.FromMinutes(30), T0.AddSeconds(20));

        s.Position(T0.AddSeconds(25)).Should().Be(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(5));
    }

    [Test]
    public void Seek_is_clamped_to_length()
    {
        var s = new CinemaShowing();
        s.Start(Film, T0);
        s.Seek(TimeSpan.FromHours(5), T0);

        s.Position(T0).Should().Be(TimeSpan.FromMinutes(94));
    }

    [Test]
    public void Ends_at_length_when_playing()
    {
        var s = new CinemaShowing();
        s.Start(Film, T0);

        s.IsOver(T0.AddMinutes(93)).Should().BeFalse();
        s.IsOver(T0.AddMinutes(94)).Should().BeTrue();
        s.Position(T0.AddMinutes(200)).Should().Be(TimeSpan.FromMinutes(94));
    }

    [Test]
    public void Paused_never_ends()
    {
        var s = new CinemaShowing();
        s.Start(Film, T0);
        s.Seek(TimeSpan.FromMinutes(94), T0);
        s.Pause(T0);

        s.IsOver(T0.AddHours(3)).Should().BeFalse();
    }

    [Test]
    public void Unknown_length_never_ends()
    {
        var s = new CinemaShowing();
        s.Start(NoLength, T0);

        s.IsOver(T0.AddDays(2)).Should().BeFalse();
        s.Position(T0.AddHours(10)).Should().Be(TimeSpan.FromHours(10));
    }

    [Test]
    public void Nothing_showing_by_default()
    {
        var s = new CinemaShowing();

        s.Film.Should().BeNull();
        s.Position(T0).Should().Be(TimeSpan.Zero);
        s.IsOver(T0).Should().BeFalse();
    }
}
```

`Tests/Chaos.Tests/Cinema/CinemaTimeTests.cs`:

```csharp
using Chaos.Services.Cinema;
using FluentAssertions;

namespace Chaos.Tests.Cinema;

public sealed class CinemaTimeTests
{
    [Test]
    [Arguments("0:00", 0)]
    [Arguments("1:30", 90)]
    [Arguments("90:05", 5405)]
    [Arguments("1:02:03", 3723)]
    [Arguments("0:59:59", 3599)]
    public void Parses_valid_times(string text, int seconds)
    {
        CinemaTime.TryParse(text, out var time).Should().BeTrue();
        time.Should().Be(TimeSpan.FromSeconds(seconds));
    }

    [Test]
    [Arguments("")]
    [Arguments("abc")]
    [Arguments("90")]
    [Arguments("1:60")]
    [Arguments("1:60:00")]
    [Arguments("1:00:60")]
    [Arguments("-1:00")]
    [Arguments("1:2:3:4")]
    [Arguments(" : ")]
    public void Rejects_invalid_times(string text)
        => CinemaTime.TryParse(text, out _).Should().BeFalse();

    [Test]
    [Arguments(0, "0:00")]
    [Arguments(65, "1:05")]
    [Arguments(3599, "59:59")]
    [Arguments(3600, "1:00:00")]
    [Arguments(5405, "1:30:05")]
    public void Formats_times(int seconds, string expected)
        => CinemaTime.Format(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
}
```

`Tests/Chaos.Tests/Cinema/CinemaFilmMatcherTests.cs`:

```csharp
using Chaos.Services.Cinema;
using FluentAssertions;

namespace Chaos.Tests.Cinema;

public sealed class CinemaFilmMatcherTests
{
    private static readonly CinemaFilm[] Films =
    [
        new("nosferatu", "Nosferatu", null, "u1"),
        new("night-1968", "Night of the Living Dead", null, "u2"),
        new("night-2", "Night Tide", null, "u3"),
        new("metropolis", "Metropolis", null, "u4")
    ];

    [Test]
    public void Exact_id_wins()
        => CinemaFilmMatcher.Match(Films, "night-2").Film!.Id.Should().Be("night-2");

    [Test]
    public void Exact_title_ignores_case()
        => CinemaFilmMatcher.Match(Films, "metropolis").Film!.Id.Should().Be("metropolis");

    [Test]
    public void Exact_title_beats_contains()
    {
        var films = Films.Append(new CinemaFilm("nos2", "Nosferatu the Vampyre", null, "u5")).ToArray();

        CinemaFilmMatcher.Match(films, "NOSFERATU").Film!.Id.Should().Be("nosferatu");
    }

    [Test]
    public void Unique_contains_matches()
        => CinemaFilmMatcher.Match(Films, "living dead").Film!.Id.Should().Be("night-1968");

    [Test]
    public void Several_contains_is_ambiguous()
    {
        var match = CinemaFilmMatcher.Match(Films, "night");

        match.Film.Should().BeNull();
        match.Ambiguous.Should().BeTrue();
    }

    [Test]
    public void No_match_is_not_ambiguous()
    {
        var match = CinemaFilmMatcher.Match(Films, "casablanca");

        match.Film.Should().BeNull();
        match.Ambiguous.Should().BeFalse();
    }

    [Test]
    public void Blank_name_matches_nothing()
        => CinemaFilmMatcher.Match(Films, "  ").Film.Should().BeNull();
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run the Verify command. Expected: build errors, `Chaos.Services.Cinema` not found.

- [ ] **Step 3: Write the types**

`CinemaFilm.cs`:

```csharp
namespace Chaos.Services.Cinema;

/// <summary>A film the film site has ready to stream. <see cref="Length" /> is null when the site does not know it.</summary>
public sealed record CinemaFilm(string Id, string Title, TimeSpan? Length, string Url);
```

`CinemaShowing.cs`:

```csharp
namespace Chaos.Services.Cinema;

/// <summary>
///     The film the Cinema is showing and where it is. Position is worked out from an anchor (a time and the film
///     position at that time) so nothing has to tick.
/// </summary>
public sealed class CinemaShowing
{
    private DateTime AnchorTime;
    private TimeSpan AnchorPosition;

    public CinemaFilm? Film { get; private set; }
    public bool Paused { get; private set; }

    public void Start(CinemaFilm film, DateTime now)
    {
        Film = film;
        Paused = false;
        AnchorTime = now;
        AnchorPosition = TimeSpan.Zero;
    }

    public void Stop()
    {
        Film = null;
        Paused = false;
        AnchorPosition = TimeSpan.Zero;
    }

    public void Pause(DateTime now)
    {
        if (Film is null || Paused)
            return;

        AnchorPosition = Position(now);
        AnchorTime = now;
        Paused = true;
    }

    public void Resume(DateTime now)
    {
        if (Film is null || !Paused)
            return;

        AnchorTime = now;
        Paused = false;
    }

    public void Seek(TimeSpan position, DateTime now)
    {
        if (Film is null)
            return;

        AnchorPosition = Clamp(position);
        AnchorTime = now;
    }

    public TimeSpan Position(DateTime now)
    {
        if (Film is null)
            return TimeSpan.Zero;

        return Paused ? AnchorPosition : Clamp(AnchorPosition + (now - AnchorTime));
    }

    public bool IsOver(DateTime now)
        => Film?.Length is { } length && !Paused && Position(now) >= length;

    private TimeSpan Clamp(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
            return TimeSpan.Zero;

        return Film?.Length is { } length && position > length ? length : position;
    }
}
```

Match the repo's field naming for private fields (check another class in `Chaos/Services/`; if it uses `_camelCase` or PascalCase fields, follow that).

`CinemaTime.cs`:

```csharp
using System.Globalization;

namespace Chaos.Services.Cinema;

public static class CinemaTime
{
    /// <summary>Reads "mm:ss" (minutes may be over 59) or "h:mm:ss" (minutes and seconds under 60).</summary>
    public static bool TryParse(string text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        var parts = text.Trim().Split(':');

        if (parts.Length is < 2 or > 3)
            return false;

        var numbers = new int[parts.Length];

        for (var i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return false;

        if (parts.Length == 2)
        {
            if (numbers[1] >= 60)
                return false;

            time = new TimeSpan(0, numbers[0], numbers[1]);

            return true;
        }

        if (numbers[1] >= 60 || numbers[2] >= 60)
            return false;

        time = new TimeSpan(numbers[0], numbers[1], numbers[2]);

        return true;
    }

    public static string Format(TimeSpan time)
        => time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
}
```

`CinemaFilmMatcher.cs`:

```csharp
namespace Chaos.Services.Cinema;

public readonly record struct CinemaFilmMatch(CinemaFilm? Film, bool Ambiguous);

public static class CinemaFilmMatcher
{
    /// <summary>Exact id, then exact title (any case), then the one title that contains the name.</summary>
    public static CinemaFilmMatch Match(IReadOnlyList<CinemaFilm> films, string name)
    {
        var wanted = name.Trim();

        if (wanted.Length == 0)
            return new CinemaFilmMatch(null, false);

        var byId = films.FirstOrDefault(f => string.Equals(f.Id, wanted, StringComparison.OrdinalIgnoreCase));

        if (byId is not null)
            return new CinemaFilmMatch(byId, false);

        var byTitle = films.FirstOrDefault(f => string.Equals(f.Title, wanted, StringComparison.OrdinalIgnoreCase));

        if (byTitle is not null)
            return new CinemaFilmMatch(byTitle, false);

        var containing = films.Where(f => f.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase))
                              .Take(2)
                              .ToList();

        return containing.Count switch
        {
            1 => new CinemaFilmMatch(containing[0], false),
            0 => new CinemaFilmMatch(null, false),
            _ => new CinemaFilmMatch(null, true)
        };
    }
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run the Verify command. Expected: every test in `Chaos.Tests.Cinema` passes. If the tree-node filter path does not match, run the three classes by name with `/*/*/CinemaShowingTests/*` and so on.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/Cinema/CinemaFilm.cs", "Chaos-Server/Chaos/Services/Cinema/CinemaShowing.cs", "Chaos-Server/Chaos/Services/Cinema/CinemaTime.cs", "Chaos-Server/Chaos/Services/Cinema/CinemaFilmMatcher.cs", "Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaShowingTests.cs", "Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaTimeTests.cs", "Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaFilmMatcherTests.cs"], "verifyCommand": "dotnet build Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj && dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter \"/*/Chaos.Tests.Cinema/*/*\"", "acceptanceCriteria": ["Position grows while playing and holds while paused", "Seek while paused stays paused at new spot", "Position clamped to 0..length", "IsOver only when playing with known length", "TryParse accepts mm:ss and h:mm:ss, rejects the rest", "Format writes m:ss or h:mm:ss", "Match prefers id, then exact title, then unique contains; several contains is Ambiguous"], "modelTier": "mechanical"}
```

---

### Task 3: Film site client, options and CinemaService

**Goal:** The server can fetch the film site's ready list without hanging, and one `CinemaService` singleton holds the showing and builds packet args.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Cinema/CinemaOptions.cs`
- Create: `Chaos-Server/Chaos/Services/Cinema/CinemaFilmSite.cs` (interface `ICinemaFilmSite` and class `CinemaFilmSite`)
- Create: `Chaos-Server/Chaos/Services/Cinema/CinemaService.cs`
- Modify: `Chaos-Server/Chaos/Extensions/ServiceCollectionExtensions.cs` (new `AddCinema()` after `AddCollege()`, which ends near line 350)
- Modify: `Chaos-Server/Chaos/Program.cs:251` (call `AddCinema()` after `builder.Services.AddCollege();`)
- Modify: `Chaos-Server/Chaos/appsettings.json`, `appsettings.prod.json`, `appsettings.local.json` (add `CinemaOptions` inside `"Options": {`)
- Test: `Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaFilmSiteTests.cs`
- Test: `Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaServiceTests.cs`

**Acceptance Criteria:**
- [ ] `CinemaFilmSite.Parse` reads the ready list, skips entries without an id or url, and maps a null or missing `lengthSeconds` to a null `Length`.
- [ ] `GetReadyFilmsAsync` returns null (never throws) when the handler throws `HttpRequestException`, when the site answers 500, when the body is not JSON, and when the request times out.
- [ ] `CinemaService.Play` returns args with the film's id, url, position 0 and `Paused = false`; titles over 60 characters are cut to 60.
- [ ] `Pause`, `Resume` and `Seek` return null when nothing is showing; `Stop` returns false then.
- [ ] `CurrentState()` returns empty args when nothing is showing and the live position otherwise.
- [ ] `EndIfOver()` stops the showing and returns true once a film with a known length has run past it.
- [ ] The server starts with the new options in all three appsettings files.

**Verify:** `dotnet build Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj && dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter "/*/Chaos.Tests.Cinema/*/*"` → all Cinema tests pass.

**Steps:**

- [ ] **Step 1: Write the failing tests**

`Tests/Chaos.Tests/Cinema/CinemaFilmSiteTests.cs`:

```csharp
using System.Net;
using Chaos.Services.Cinema;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chaos.Tests.Cinema;

public sealed class CinemaFilmSiteTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }

    private static CinemaFilmSite Site(StubHandler handler, int timeoutMs = 2000)
        => new(
            new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(timeoutMs) },
            Options.Create(new CinemaOptions { FilmSiteUrl = "https://films.example.org/" }));

    private const string JSON = """
        [
          { "id": "nosferatu", "title": "Nosferatu", "lengthSeconds": 5640, "url": "https://films.example.org/films/nosferatu/index.m3u8" },
          { "id": "live", "title": "Live", "lengthSeconds": null, "url": "https://films.example.org/films/live/index.m3u8" },
          { "id": "nolen", "title": "No Length Key", "url": "https://films.example.org/films/nolen/index.m3u8" },
          { "id": "", "title": "No Id", "lengthSeconds": 10, "url": "https://x/a.m3u8" },
          { "id": "nourl", "title": "No Url", "lengthSeconds": 10 }
        ]
        """;

    [Test]
    public void Parse_reads_ready_films_and_skips_broken_entries()
    {
        var films = CinemaFilmSite.Parse(JSON);

        films.Select(f => f.Id).Should().Equal("nosferatu", "live", "nolen");
        films[0].Length.Should().Be(TimeSpan.FromSeconds(5640));
        films[0].Url.Should().Be("https://films.example.org/films/nosferatu/index.m3u8");
        films[1].Length.Should().BeNull();
        films[2].Length.Should().BeNull();
    }

    [Test]
    public async Task Fetch_asks_for_the_ready_list()
    {
        Uri? asked = null;
        var site = Site(new StubHandler((req, _) =>
        {
            asked = req.RequestUri;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JSON) });
        }));

        var films = await site.GetReadyFilmsAsync();

        asked!.ToString().Should().Be("https://films.example.org/api/films/ready");
        films.Should().HaveCount(3);
    }

    [Test]
    public async Task Unreachable_site_returns_null()
    {
        var site = Site(new StubHandler((_, _) => throw new HttpRequestException("refused")));

        (await site.GetReadyFilmsAsync()).Should().BeNull();
    }

    [Test]
    public async Task Server_error_returns_null()
    {
        var site = Site(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))));

        (await site.GetReadyFilmsAsync()).Should().BeNull();
    }

    [Test]
    public async Task Bad_json_returns_null()
    {
        var site = Site(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") })));

        (await site.GetReadyFilmsAsync()).Should().BeNull();
    }

    [Test]
    public async Task Json_that_is_not_a_list_returns_null()
    {
        var site = Site(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"error\":1}") })));

        (await site.GetReadyFilmsAsync()).Should().BeNull();
    }

    [Test]
    public async Task Slow_site_times_out_to_null()
    {
        var site = Site(
            new StubHandler(async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            timeoutMs: 100);

        (await site.GetReadyFilmsAsync()).Should().BeNull();
    }
}
```

`Tests/Chaos.Tests/Cinema/CinemaServiceTests.cs`:

```csharp
using Chaos.Services.Cinema;
using FluentAssertions;

namespace Chaos.Tests.Cinema;

public sealed class CinemaServiceTests
{
    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly CinemaFilm Film = new("nosferatu", "Nosferatu", TimeSpan.FromMinutes(94), "https://x/films/nosferatu/index.m3u8");

    private static (CinemaService Service, ManualTime Time) Make()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 24, 20, 0, 0, TimeSpan.Zero));

        return (new CinemaService(time), time);
    }

    [Test]
    public void Play_builds_args_from_zero()
    {
        var (service, _) = Make();

        var args = service.Play(Film);

        args.FilmId.Should().Be("nosferatu");
        args.Title.Should().Be("Nosferatu");
        args.Url.Should().Be(Film.Url);
        args.PositionMs.Should().Be(0);
        args.Paused.Should().BeFalse();
    }

    [Test]
    public void Long_title_is_clipped_to_60()
    {
        var (service, _) = Make();

        var args = service.Play(Film with { Title = new string('x', 61) + "살" });

        args.Title.Should().HaveLength(60);
    }

    [Test]
    public void Controls_do_nothing_when_nothing_is_showing()
    {
        var (service, _) = Make();

        service.Pause().Should().BeNull();
        service.Resume().Should().BeNull();
        service.Seek(TimeSpan.FromMinutes(1)).Should().BeNull();
        service.Stop().Should().BeFalse();
        service.CurrentState().Url.Should().BeEmpty();
        service.Describe().Should().BeNull();
    }

    [Test]
    public void Current_state_tracks_time_pause_and_seek()
    {
        var (service, time) = Make();
        service.Play(Film);
        time.Now += TimeSpan.FromSeconds(90);

        service.CurrentState().PositionMs.Should().Be(90_000);

        service.Pause()!.Paused.Should().BeTrue();
        time.Now += TimeSpan.FromMinutes(10);
        service.Seek(TimeSpan.FromMinutes(30))!.PositionMs.Should().Be(1_800_000);

        var state = service.CurrentState();
        state.Paused.Should().BeTrue();
        state.PositionMs.Should().Be(1_800_000);
        service.Describe().Should().Be("Nosferatu at 30:00 (paused)");

        service.Resume()!.Paused.Should().BeFalse();
    }

    [Test]
    public void Stop_clears_the_showing()
    {
        var (service, _) = Make();
        service.Play(Film);

        service.Stop().Should().BeTrue();
        service.CurrentState().Url.Should().BeEmpty();
    }

    [Test]
    public void Ends_after_its_length()
    {
        var (service, time) = Make();
        service.Play(Film);
        time.Now += TimeSpan.FromMinutes(93);

        service.EndIfOver().Should().BeFalse();

        time.Now += TimeSpan.FromMinutes(1);

        service.EndIfOver().Should().BeTrue();
        service.CurrentState().Url.Should().BeEmpty();
        service.EndIfOver().Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run the Verify command. Expected: build errors for `CinemaOptions`, `CinemaFilmSite`, `CinemaService`.

- [ ] **Step 3: Write the options**

`CinemaOptions.cs`. Look at `CollegeOptions` first and use the same shape (class vs record, `init` vs `set`) so `AddOptionsFromConfig` binds it the same way:

```csharp
namespace Chaos.Services.Cinema;

public sealed record CinemaOptions
{
    /// <summary>The film site's public base url, such as https://films.example.duckdns.org. Empty means not set up.</summary>
    public string FilmSiteUrl { get; init; } = "";

    /// <summary>The map instance that shows films.</summary>
    public string HallMapInstanceId { get; init; } = "loures_2_floor_hall";

    /// <summary>How long a request to the film site may take before /film gives up.</summary>
    public int TimeoutSeconds { get; init; } = 10;
}
```

- [ ] **Step 4: Write the film site client**

`CinemaFilmSite.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Chaos.Services.Cinema;

public interface ICinemaFilmSite
{
    /// <summary>The site's ready films, or null when the site can't be reached or answers with something unreadable.</summary>
    Task<IReadOnlyList<CinemaFilm>?> GetReadyFilmsAsync(CancellationToken cancellationToken = default);
}

public sealed class CinemaFilmSite(HttpClient http, IOptions<CinemaOptions> options) : ICinemaFilmSite
{
    public async Task<IReadOnlyList<CinemaFilm>?> GetReadyFilmsAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = options.Value.FilmSiteUrl.TrimEnd('/');

        if (baseUrl.Length == 0)
            return null;

        try
        {
            using var response = await http.GetAsync($"{baseUrl}/api/films/ready", cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            return Parse(json);
        } catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Reads the ready list. Throws JsonException on bad JSON and InvalidOperationException when the root is not a list.</summary>
    public static IReadOnlyList<CinemaFilm> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var films = new List<CinemaFilm>();

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var id = Text(entry, "id");
            var url = Text(entry, "url");

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url))
                continue;

            TimeSpan? length = entry.TryGetProperty("lengthSeconds", out var seconds) && (seconds.ValueKind == JsonValueKind.Number)
                ? TimeSpan.FromSeconds(seconds.GetDouble())
                : null;

            films.Add(new CinemaFilm(id, Text(entry, "title") ?? id, length, url));
        }

        return films;
    }

    private static string? Text(JsonElement entry, string name)
        => entry.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.String) ? value.GetString() : null;
}
```

- [ ] **Step 5: Write the service**

`CinemaService.cs`:

```csharp
using Chaos.Networking.Entities.Server;

namespace Chaos.Services.Cinema;

/// <summary>The one showing in the Cinema. Every method is safe to call from any map's update or a command.</summary>
public sealed class CinemaService(TimeProvider time)
{
    public const int MAX_TITLE_LENGTH = 60;

    private readonly CinemaShowing Showing = new();
    private readonly Lock Sync = new();

    public CinemaStateArgs Play(CinemaFilm film)
    {
        using var scope = Sync.EnterScope();
        Showing.Start(film, Now);

        return Build();
    }

    public CinemaStateArgs? Pause()
    {
        using var scope = Sync.EnterScope();

        if (Showing.Film is null)
            return null;

        Showing.Pause(Now);

        return Build();
    }

    public CinemaStateArgs? Resume()
    {
        using var scope = Sync.EnterScope();

        if (Showing.Film is null)
            return null;

        Showing.Resume(Now);

        return Build();
    }

    public CinemaStateArgs? Seek(TimeSpan position)
    {
        using var scope = Sync.EnterScope();

        if (Showing.Film is null)
            return null;

        Showing.Seek(position, Now);

        return Build();
    }

    public bool Stop()
    {
        using var scope = Sync.EnterScope();

        if (Showing.Film is null)
            return false;

        Showing.Stop();

        return true;
    }

    public CinemaStateArgs CurrentState()
    {
        using var scope = Sync.EnterScope();

        return Build();
    }

    /// <summary>Stops a film that has run past its length. True only on the call that stopped it.</summary>
    public bool EndIfOver()
    {
        using var scope = Sync.EnterScope();

        if (!Showing.IsOver(Now))
            return false;

        Showing.Stop();

        return true;
    }

    /// <summary>A one-line description for /film list, such as "Nosferatu at 12:04 (paused)", or null when nothing is showing.</summary>
    public string? Describe()
    {
        using var scope = Sync.EnterScope();

        if (Showing.Film is not { } film)
            return null;

        return $"{film.Title} at {CinemaTime.Format(Showing.Position(Now))}{(Showing.Paused ? " (paused)" : "")}";
    }

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    private CinemaStateArgs Build()
    {
        if (Showing.Film is not { } film)
            return new CinemaStateArgs();

        var ms = Showing.Position(Now).TotalMilliseconds;

        return new CinemaStateArgs
        {
            FilmId = film.Id,
            Title = film.Title.Length > MAX_TITLE_LENGTH ? film.Title[..MAX_TITLE_LENGTH] : film.Title,
            Url = film.Url,
            PositionMs = (uint)Math.Clamp(ms, 0, uint.MaxValue),
            Paused = Showing.Paused
        };
    }
}
```

60 characters of Korean is at most 120 bytes in code page 949, inside `String8`'s 255. The film site limits ids to short slugs.

- [ ] **Step 6: Register the services**

`ServiceCollectionExtensions.cs`, a new method after `AddCollege`:

```csharp
    public static void AddCinema(this IServiceCollection services)
    {
        services.AddOptionsFromConfig<CinemaOptions>(ConfigKeys.Options.Key);
        services.AddSingleton<ICinemaFilmSite>(
            provider =>
            {
                var options = provider.GetRequiredService<IOptions<CinemaOptions>>();

                return new CinemaFilmSite(
                    new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.TimeoutSeconds)) },
                    options);
            });
        services.AddSingleton<CinemaService>();
    }
```

Add `using Chaos.Services.Cinema;` at the top. `CollegeCommand` already takes a `TimeProvider` from DI, so it is registered.

`Program.cs`, after line 251 `builder.Services.AddCollege();`:

```csharp
    builder.Services.AddCinema();
```

- [ ] **Step 7: Add the settings**

In each of `appsettings.json`, `appsettings.prod.json` and `appsettings.local.json`, as the first entry inside `"Options": {`:

```json
    "CinemaOptions": {
      "FilmSiteUrl": "",
      "HallMapInstanceId": "loures_2_floor_hall",
      "TimeoutSeconds": 10
    },
```

`FilmSiteUrl` stays empty until the film site goes live (Plan 1 go-live). Keep each file valid JSON.

- [ ] **Step 8: Run the tests to see them pass**

Run the Verify command. Expected: all Cinema tests pass, including Task 2's.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/Cinema/CinemaOptions.cs", "Chaos-Server/Chaos/Services/Cinema/CinemaFilmSite.cs", "Chaos-Server/Chaos/Services/Cinema/CinemaService.cs", "Chaos-Server/Chaos/Extensions/ServiceCollectionExtensions.cs", "Chaos-Server/Chaos/Program.cs", "Chaos-Server/Chaos/appsettings.json", "Chaos-Server/Chaos/appsettings.prod.json", "Chaos-Server/Chaos/appsettings.local.json", "Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaFilmSiteTests.cs", "Chaos-Server/Tests/Chaos.Tests/Cinema/CinemaServiceTests.cs"], "verifyCommand": "dotnet build Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj && dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter \"/*/Chaos.Tests.Cinema/*/*\"", "acceptanceCriteria": ["Parse reads the list, skips entries without id or url, null/missing lengthSeconds gives null Length", "GetReadyFilmsAsync returns null on HttpRequestException, 500, bad JSON, non-list JSON and timeout", "Play returns id, url, position 0, not paused; titles cut to 60", "Pause/Resume/Seek null and Stop false when nothing showing", "CurrentState empty when nothing showing, live position otherwise", "EndIfOver stops and returns true once past a known length", "CinemaOptions added to all three appsettings files"], "modelTier": "standard"}
```

---

### Task 4: Cinema map script and /film command

**Goal:** Players who enter the hall get the current film; admins run showings with `/film`; a film ends on its own at its length.

**Files:**
- Create: `Chaos-Server/Chaos/Scripting/MapScripts/Temuair/Loures/CinemaMapScript.cs`
- Create: `Chaos-Server/Chaos/Messaging/Admin/FilmCommand.cs`
- Modify (Unora worktree): `Data/Configuration/MapInstances/Temuair/Towns/Loures/Loures_2_Floor_Hall/instance.json` (line 6, `"scriptKeys": []`)

**Acceptance Criteria:**
- [ ] The script's key is `cinemamap` (the class name without `Script`, lower case, as other map scripts get theirs).
- [ ] Entering the hall sends `CurrentState()`; leaving it sends empty args.
- [ ] Once a second the script calls `EndIfOver()`; when it returns true, everyone in the hall gets empty args and the active message "The film has ended."
- [ ] `/film list` shows each ready film as `id - title (length)` with `?` for an unknown length, plus what is showing now.
- [ ] `/film play <name>` takes a multi-word name, matches it with `CinemaFilmMatcher`, broadcasts the new state, and everyone in the hall sees "Now showing: <title>".
- [ ] `/film pause`, `resume`, `seek <mm:ss|h:mm:ss>` and `stop` broadcast the new state, or say "Nothing is showing." when nothing is.
- [ ] An empty `FilmSiteUrl` answers "The film site is not set up."; an unreachable site answers "The film site can't be reached right now." within the timeout.
- [ ] Broadcasts to the hall from another map take the hall's lock with a 5 s timeout, as `TcgCommand.UnderTargetMapAsync` does; from inside the hall they do not take it again.
- [ ] The hall's `instance.json` has `"scriptKeys": ["cinemamap"]`.
- [ ] Server builds and all server tests still pass.

**Verify:** `dotnet build Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj && dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj --no-build` → no new failures. Then the manual check in Step 5.

**Steps:**

- [ ] **Step 1: Write the map script**

`CinemaMapScript.cs`. Copy the `using` block and the `IntervalTimer` pattern from `Chaos-Server/Chaos/Scripting/MapScripts/Temuair/Suomi/SuomiTheatreMapScript.cs`, and add `using Chaos.Services.Cinema;` and `using Chaos.Networking.Entities.Server;`:

```csharp
namespace Chaos.Scripting.MapScripts.Temuair.Loures;

/// <summary>The Cinema in the Loures 2 Floor Hall: sends the film to people who come in and ends it at its length.</summary>
public sealed class CinemaMapScript : MapScriptBase
{
    public const string ENDED_TEXT = "The film has ended.";

    private readonly CinemaService Cinema;
    private readonly IIntervalTimer EndTimer = new IntervalTimer(TimeSpan.FromSeconds(1));

    public CinemaMapScript(MapInstance subject, CinemaService cinema)
        : base(subject)
        => Cinema = cinema;

    public override void OnEntered(Creature creature)
    {
        //map info went out before this call, so the client has already reset the previous map's film
        if (creature is Aisling aisling)
            aisling.Client.SendCinemaState(Cinema.CurrentState());
    }

    public override void OnExited(Creature creature)
    {
        if (creature is Aisling aisling)
            aisling.Client.SendCinemaState(new CinemaStateArgs());
    }

    public override void Update(TimeSpan delta)
    {
        EndTimer.Update(delta);

        if (!EndTimer.IntervalElapsed || !Cinema.EndIfOver())
            return;

        Broadcast(Subject, new CinemaStateArgs());
        Announce(Subject, ENDED_TEXT);
    }

    /// <summary>Sends the state to everyone in <paramref name="map" />. The caller holds the map's lock.</summary>
    public static void Broadcast(MapInstance map, CinemaStateArgs args)
    {
        foreach (var aisling in map.GetEntities<Aisling>())
            aisling.Client.SendCinemaState(args);
    }

    /// <summary>Shows <paramref name="text" /> to everyone in <paramref name="map" />. The caller holds the map's lock.</summary>
    public static void Announce(MapInstance map, string text)
    {
        foreach (var aisling in map.GetEntities<Aisling>())
            aisling.SendActiveMessage(text);
    }
}
```

Check how `SuomiTheatreMapScript` maps to its key in its instance.json. If the rule differs from "class name minus `Script`, lower case", use the rule and change `cinemamap` everywhere in this plan's files.

- [ ] **Step 2: Write the command**

`FilmCommand.cs`. Copy the `using` block, the constructor style and the reply helper from `Chaos-Server/Chaos/Messaging/Admin/CollegeCommand.cs`. Add `using Chaos.Services.Cinema;`, `using Chaos.Scripting.MapScripts.Temuair.Loures;`, `using Chaos.Networking.Entities.Server;`, `using Microsoft.Extensions.Options;`, and the namespace `SuomiTheatreMapScript` imports `ISimpleCache` from.

```csharp
namespace Chaos.Messaging.Admin;

[Command("film", helpText: "list | play <name> | pause | resume | seek <mm:ss|h:mm:ss> | stop")]
public sealed class FilmCommand : ICommand<Aisling>
{
    private const string USAGE = "Usage: /film list | play <name> | pause | resume | seek <mm:ss|h:mm:ss> | stop";
    private const string NOTHING_SHOWING = "Nothing is showing.";

    private readonly CinemaService Cinema;
    private readonly ICinemaFilmSite FilmSite;
    private readonly ILogger<FilmCommand> Logger;
    private readonly CinemaOptions Options;
    private readonly ISimpleCache SimpleCache;

    public FilmCommand(CinemaService cinema,
        ICinemaFilmSite filmSite,
        IOptions<CinemaOptions> options,
        ISimpleCache simpleCache,
        ILogger<FilmCommand> logger)
    {
        Cinema = cinema;
        FilmSite = filmSite;
        Options = options.Value;
        SimpleCache = simpleCache;
        Logger = logger;
    }

    public async ValueTask ExecuteAsync(Aisling source, ArgumentCollection args)
    {
        if (!args.TryGetNext<string>(out var sub))
        {
            Reply(source, USAGE);

            return;
        }

        switch (sub.ToLowerInvariant())
        {
            case "list":
                await ListAsync(source);

                break;
            case "play":
                await PlayAsync(source, RestOf(args));

                break;
            case "pause":
                await SendOrSayNothingAsync(source, Cinema.Pause());

                break;
            case "resume":
                await SendOrSayNothingAsync(source, Cinema.Resume());

                break;
            case "seek":
                if (!args.TryGetNext<string>(out var text) || !CinemaTime.TryParse(text, out var position))
                {
                    Reply(source, "Usage: /film seek mm:ss or h:mm:ss");

                    break;
                }

                await SendOrSayNothingAsync(source, Cinema.Seek(position));

                break;
            case "stop":
                if (!Cinema.Stop())
                {
                    Reply(source, NOTHING_SHOWING);

                    break;
                }

                await BroadcastAsync(source, new CinemaStateArgs(), "The film has been stopped.");

                break;
            default:
                Reply(source, USAGE);

                break;
        }
    }

    private async Task ListAsync(Aisling source)
    {
        if (await FetchFilmsAsync(source) is not { } films)
            return;

        var lines = new List<string> { $"Now showing: {Cinema.Describe() ?? "nothing"}", "", "Ready films:" };

        if (films.Count == 0)
            lines.Add("(none)");
        else
            lines.AddRange(films.Select(f => $"{f.Id} - {f.Title} ({(f.Length is { } length ? CinemaTime.Format(length) : "?")})"));

        Reply(source, string.Join('\n', lines));
    }

    private async Task PlayAsync(Aisling source, string name)
    {
        if (name.Length == 0)
        {
            Reply(source, "Usage: /film play <name>");

            return;
        }

        if (await FetchFilmsAsync(source) is not { } films)
            return;

        var match = CinemaFilmMatcher.Match(films, name);

        if (match.Ambiguous)
        {
            Reply(source, $"More than one film matches \"{name}\". Use its id from /film list.");

            return;
        }

        if (match.Film is not { } film)
        {
            Reply(source, $"No ready film matches \"{name}\".");

            return;
        }

        var state = Cinema.Play(film);
        await BroadcastAsync(source, state, $"Now showing: {state.Title}");
    }

    private async Task<IReadOnlyList<CinemaFilm>?> FetchFilmsAsync(Aisling source)
    {
        if (string.IsNullOrWhiteSpace(Options.FilmSiteUrl))
        {
            Reply(source, "The film site is not set up.");

            return null;
        }

        var films = await FilmSite.GetReadyFilmsAsync();

        if (films is null)
            Reply(source, "The film site can't be reached right now.");

        return films;
    }

    private async Task SendOrSayNothingAsync(Aisling source, CinemaStateArgs? state)
    {
        if (state is null)
        {
            Reply(source, NOTHING_SHOWING);

            return;
        }

        await BroadcastAsync(source, state, null);
    }

    private async Task BroadcastAsync(Aisling source, CinemaStateArgs state, string? announce)
    {
        MapInstance hall;

        try
        {
            hall = SimpleCache.Get<MapInstance>(Options.HallMapInstanceId);
        } catch (Exception e)
        {
            Logger.LogWarning(e, "Film command could not load the hall map {@MapInstanceId}", Options.HallMapInstanceId);
            Reply(source, "The hall map could not be loaded.");

            return;
        }

        if (ReferenceEquals(hall, source.MapInstance))
        {
            Send(hall, state, announce);
            Reply(source, "Done.");

            return;
        }

        //the command already holds the source map's lock, so an unbounded wait here could deadlock
        await using var sync = await hall.Sync.WaitAsync(TimeSpan.FromSeconds(5));

        if (sync is null)
        {
            Reply(source, "The hall is busy. The change is saved and reaches players as they enter; run the command again to update everyone now.");

            return;
        }

        Send(hall, state, announce);
        Reply(source, "Done.");
    }

    private static void Send(MapInstance hall, CinemaStateArgs state, string? announce)
    {
        CinemaMapScript.Broadcast(hall, state);

        if (announce is not null)
            CinemaMapScript.Announce(hall, announce);
    }

    private static string RestOf(ArgumentCollection args)
    {
        var words = new List<string>();

        while (args.TryGetNext<string>(out var word))
            words.Add(word);

        return string.Join(' ', words);
    }

    private static void Reply(Aisling source, string text) => source.Client.SendServerMessage(ServerMessageType.ScrollWindow, text);
}
```

Check how `CollegeCommand` / `TcgCommand` limit themselves to admins (the `Admin` namespace, or a flag on `[Command]`). Do the same so only admins can run `/film`. If commands in this repo return a non-async `ValueTask`, keep `async ValueTask` anyway; `TcgCommand` awaits inside its command.

- [ ] **Step 3: Turn on the script in Unora**

In the Unora worktree, `Data/Configuration/MapInstances/Temuair/Towns/Loures/Loures_2_Floor_Hall/instance.json` line 6:

```json
  "scriptKeys": ["cinemamap"],
```

- [ ] **Step 4: Build and run all server tests**

Run the Verify command. Expected: build passes, no new test failures.

- [ ] **Step 5: Manual check (the controller does this with the user, not the implementer)**

With the server running locally against the Unora worktree's data: `/film list` with `FilmSiteUrl` empty answers "The film site is not set up." Set `FilmSiteUrl` to an address nothing listens on (`http://127.0.0.1:9`) and `/film list` answers "can't be reached" within about 10 s.

```json:metadata
{"files": ["Chaos-Server/Chaos/Scripting/MapScripts/Temuair/Loures/CinemaMapScript.cs", "Chaos-Server/Chaos/Messaging/Admin/FilmCommand.cs", "Data/Configuration/MapInstances/Temuair/Towns/Loures/Loures_2_Floor_Hall/instance.json"], "verifyCommand": "dotnet build Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj && dotnet run --project Chaos-Server/Tests/Chaos.Tests/Chaos.Tests.csproj --no-build", "acceptanceCriteria": ["Script key is cinemamap", "Entering sends CurrentState, leaving sends empty args", "EndIfOver each second broadcasts stop and 'The film has ended.'", "/film list shows id - title (length) and what is showing", "/film play matches a multi-word name and announces 'Now showing: <title>'", "pause/resume/seek/stop broadcast or say 'Nothing is showing.'", "Empty FilmSiteUrl and unreachable site give plain messages within the timeout", "Cross-map broadcasts take the hall lock with a 5 s timeout; same-map ones do not", "Hall instance.json has scriptKeys [\"cinemamap\"]", "Server builds and tests pass"], "modelTier": "standard"}
```

---

### Task 5: Cinema music duck

**Goal:** `MusicDuck` can stop at a level above 0 and fade down and up at different speeds, and `SoundSystem` has a second duck for films.

**Files:**
- Modify: `Chaos.Client/Systems/MusicDuck.cs`
- Modify: `Chaos.Client/Systems/SoundSystem.cs` (field at line 74; volume at lines 291 and 452; the `Step` call at line 451)
- Test: `Tests/Chaos.Client.Tests/MusicDuckTests.cs`

**Acceptance Criteria:**
- [ ] The existing `MusicDuckTests` pass unchanged (default duck: down to 0 and back in 0.3 s).
- [ ] A duck with `DuckedLevel = 0.2, FadeDownSeconds = 1, FadeUpSeconds = 2` goes 1 → 0.6 after `Step(0.5)`, stops at 0.2, then climbs back to 1 over 2 s (0.6 after 1 s).
- [ ] Music volume is `Math.Round(MusicVolumeValue * Duck.Level * CinemaDuck.Level)` everywhere it is set.
- [ ] Both ducks step every update, and the volume is set when either one changed.
- [ ] `SoundSystem.SetCinemaDuck(bool on)` exists.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/MusicDuckTests/*"` → all MusicDuck tests pass.

**Steps:**

- [ ] **Step 1: Write the failing test**

Add to `Tests/Chaos.Client.Tests/MusicDuckTests.cs`:

```csharp
    [Test]
    public void Cinema_duck_stops_at_its_level_and_rises_slower()
    {
        var duck = new MusicDuck { DuckedLevel = 0.2, FadeDownSeconds = 1, FadeUpSeconds = 2, Ducked = true };

        duck.Step(0.5).Should().BeTrue();
        duck.Level.Should().BeApproximately(0.6, 1e-9);

        duck.Step(5).Should().BeTrue();
        duck.Level.Should().BeApproximately(0.2, 1e-9);
        duck.Step(1).Should().BeFalse();

        duck.Ducked = false;
        duck.Step(1).Should().BeTrue();
        duck.Level.Should().BeApproximately(0.6, 1e-9);

        duck.Step(5).Should().BeTrue();
        duck.Level.Should().Be(1);
    }
```

- [ ] **Step 2: Run the test to see it fail**

Run the Verify command. Expected: build error, `DuckedLevel` not found.

- [ ] **Step 3: Change MusicDuck**

Replace the class body in `MusicDuck.cs`:

```csharp
namespace Chaos.Client.Systems;

/// <summary>How far the map music is turned down while a College tune or a film plays: 1 is full volume, 0 is silent.</summary>
public sealed class MusicDuck
{
    public const double FADE_SECONDS = 0.3;

    public double DuckedLevel { get; init; }
    public double FadeDownSeconds { get; init; } = FADE_SECONDS;
    public double FadeUpSeconds { get; init; } = FADE_SECONDS;

    public double Level { get; private set; } = 1;
    public bool Ducked { get; set; }

    /// <summary>Moves the level toward its target. True when it changed, so the caller applies it.</summary>
    public bool Step(double elapsedSeconds)
    {
        var target = Ducked ? DuckedLevel : 1;

        if (Level == target)
            return false;

        var range = 1 - DuckedLevel;
        var delta = range * elapsedSeconds / (Ducked ? FadeDownSeconds : FadeUpSeconds);
        Level = Ducked ? Math.Max(DuckedLevel, Level - delta) : Math.Min(1, Level + delta);

        return true;
    }

    public int Apply(int musicVolume) => (int)Math.Round(musicVolume * Level);
}
```

- [ ] **Step 4: Add the cinema duck to SoundSystem**

After `private readonly MusicDuck Duck = new();` (line 74):

```csharp
    private readonly MusicDuck CinemaDuck = new() { DuckedLevel = 0.2, FadeDownSeconds = 1, FadeUpSeconds = 2 };
```

Add a helper near `SetMusicVolume`:

```csharp
    private int DuckedMusicVolume() => (int)Math.Round(MusicVolumeValue * Duck.Level * CinemaDuck.Level);
```

Line 291 (`SetMusicVolume`): `SdlMixer.Mix_VolumeMusic(DuckedMusicVolume());`

Lines 451-452 (in `Update`): step both ducks with a non-short-circuit `|` so both always move:

```csharp
        if (Duck.Step(elapsed) | CinemaDuck.Step(elapsed))
            SdlMixer.Mix_VolumeMusic(DuckedMusicVolume());
```

Search the file for any other `Duck.Apply(` and change it to `DuckedMusicVolume()` too.

Add the public method next to the other public music methods:

```csharp
    /// <summary>Lowers the map music while a film plays, and brings it back after.</summary>
    public void SetCinemaDuck(bool on) => CinemaDuck.Ducked = on;
```

- [ ] **Step 5: Run the tests to see them pass**

Run the Verify command. Expected: all MusicDuck tests pass, old and new.

```json:metadata
{"files": ["Chaos.Client/Systems/MusicDuck.cs", "Chaos.Client/Systems/SoundSystem.cs", "Tests/Chaos.Client.Tests/MusicDuckTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/MusicDuckTests/*\"", "acceptanceCriteria": ["Existing MusicDuckTests pass unchanged", "Cinema duck: 0.6 after Step(0.5), stops at 0.2, back to 1 over 2 s", "Music volume = round(MusicVolumeValue * Duck.Level * CinemaDuck.Level) everywhere", "Both ducks step every update with non-short-circuit |", "SoundSystem.SetCinemaDuck(bool) exists"], "modelTier": "mechanical"}
```

---

### Task 6: Cinema settings

**Goal:** Players get a "Cinema" section in F4 settings with film volume, mute, hide video and lower-music options, saved in `Settings.cfg`.

**Files:**
- Modify: `Chaos.Client/Systems/ClientSettings.cs` (property near line 119, load case near line 374, save line near line 562)
- Modify: `Chaos.Client/ViewModel/SettingDefinition.cs` (`SettingSection` enum line 10, `SettingKey` enum, the definitions list, `PanelSections` line 458)
- Modify: `Chaos.Client/Controls/World/Popups/Options/SettingsControl.cs:115` (`SectionTitle`)
- Test: `Tests/Chaos.Client.Tests/CinemaSettingsTests.cs`

**Acceptance Criteria:**
- [ ] Defaults: `CinemaVolume = 80`, `CinemaMute = false`, `CinemaHideVideo = false`, `CinemaLowerMusic = true`.
- [ ] The four values load from and save to `Settings.cfg` under the keys `CinemaVolume`, `CinemaMute`, `CinemaHideVideo`, `CinemaLowerMusic`. Volume is clamped to 0-100 on load.
- [ ] F4 shows a "Cinema" section after "Sound" with: "Film volume" (0-10 slider, value = volume / 10, setting it stores value × 10 and saves), "Mute film sound", "Don't show video", "Lower game music during films".
- [ ] The existing `SettingDefinitionsTests` pass (every key has one definition, every toggle reads and writes).

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/CinemaSettingsTests/*"` → pass; then the whole client test project → no new failures.

**Steps:**

- [ ] **Step 1: Write the failing test**

`Tests/Chaos.Client.Tests/CinemaSettingsTests.cs`. Look at `DisplaySettingsTests.cs` first: if it resets `ClientSettings` between tests or marks tests `[NotInParallel]`, do the same here.

```csharp
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class CinemaSettingsTests
{
    [Test]
    public void Cinema_section_has_four_rows_in_order()
        => SettingDefinitions.All
                             .Where(d => d.Section == SettingSection.Cinema)
                             .Select(d => d.Key)
                             .Should()
                             .Equal(SettingKey.CinemaVolume, SettingKey.CinemaMute, SettingKey.CinemaHideVideo, SettingKey.CinemaLowerMusic);

    [Test]
    public void Cinema_section_comes_after_sound()
    {
        var sections = SettingDefinitions.PanelSections.ToList();

        sections.IndexOf(SettingSection.Cinema).Should().Be(sections.IndexOf(SettingSection.Sound) + 1);
    }

    [Test]
    public void Film_volume_slider_is_volume_over_ten()
    {
        var row = SettingDefinitions.All.Single(d => d.Key == SettingKey.CinemaVolume);
        var saved = ClientSettings.CinemaVolume;

        try
        {
            ClientSettings.CinemaVolume = 70;
            row.GetSliderValue!().Should().Be(7);
        } finally
        {
            ClientSettings.CinemaVolume = saved;
        }
    }
}
```

Do not call the slider's `SetSliderValue` in the test; it saves `Settings.cfg`.

Also add a defaults check if `ClientSettings` has a way to reset to defaults (look for a `Reset`/`LoadDefaults` method used by `DisplaySettingsTests`). If it has none, skip it.

- [ ] **Step 2: Run the test to see it fail**

Run the Verify command. Expected: build errors for the missing enum values and properties.

- [ ] **Step 3: Add the settings**

`ClientSettings.cs`, next to `public static bool TabTargeting { get; set; }`:

```csharp
    public static int CinemaVolume { get; set; } = 80;
    public static bool CinemaMute { get; set; }
    public static bool CinemaHideVideo { get; set; }
    public static bool CinemaLowerMusic { get; set; } = true;
```

In the load switch, next to `case "TabTargeting":`. Use the same int parsing other int settings in the file use (look at `MaxEffectAnimationsPerEntity`'s case):

```csharp
                    case "CinemaVolume":
                        if (int.TryParse(value, out var cinemaVolume))
                            CinemaVolume = Math.Clamp(cinemaVolume, 0, 100);

                        break;

                    case "CinemaMute":
                        CinemaMute = value == "1";

                        break;

                    case "CinemaHideVideo":
                        CinemaHideVideo = value == "1";

                        break;

                    case "CinemaLowerMusic":
                        CinemaLowerMusic = value == "1";

                        break;
```

In the save method, after the `TabTargeting` line:

```csharp
            writer.WriteLine($"CinemaVolume : {CinemaVolume}");
            writer.WriteLine($"CinemaMute : {(CinemaMute ? 1 : 0)}");
            writer.WriteLine($"CinemaHideVideo : {(CinemaHideVideo ? 1 : 0)}");
            writer.WriteLine($"CinemaLowerMusic : {(CinemaLowerMusic ? 1 : 0)}");
```

- [ ] **Step 4: Add the section and rows**

`SettingDefinition.cs`:
- In `enum SettingSection`, add `Cinema,` right after `Sound,`.
- In `enum SettingKey`, add `CinemaVolume, CinemaMute, CinemaHideVideo, CinemaLowerMusic,` at the end of the sound-related keys (or at the end of the enum if keys are not grouped).
- In `PanelSections`, add `SettingSection.Cinema,` right after `SettingSection.Sound,`.
- In the definitions list, after the last Sound row, add:

```csharp

        //── Cinema ──
        new(
            SettingKey.CinemaVolume,
            "Film volume",
            SettingSection.Cinema,
            SettingCategory.ClientLocal,
            Span: SettingSpan.Full,
            GetSliderValue: () => ClientSettings.CinemaVolume / 10,
            SetSliderValue: v =>
            {
                ClientSettings.CinemaVolume = v * 10;
                ClientSettings.Save();
            }),
        new(
            SettingKey.CinemaMute,
            "Mute film sound",
            SettingSection.Cinema,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.CinemaMute,
            Set: v => ClientSettings.CinemaMute = v),
        new(
            SettingKey.CinemaHideVideo,
            "Don't show video",
            SettingSection.Cinema,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.CinemaHideVideo,
            Set: v => ClientSettings.CinemaHideVideo = v),
        new(
            SettingKey.CinemaLowerMusic,
            "Lower game music during films",
            SettingSection.Cinema,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.CinemaLowerMusic,
            Set: v => ClientSettings.CinemaLowerMusic = v,
            Span: SettingSpan.Full),
```

`SettingsControl.cs`, in `SectionTitle` after the `Sound` line:

```csharp
            SettingSection.Cinema        => "Cinema",
```

- [ ] **Step 5: Run the tests to see them pass**

Run the Verify command, then the whole client test project. Expected: the new tests pass and `SettingDefinitionsTests` still pass.

```json:metadata
{"files": ["Chaos.Client/Systems/ClientSettings.cs", "Chaos.Client/ViewModel/SettingDefinition.cs", "Chaos.Client/Controls/World/Popups/Options/SettingsControl.cs", "Tests/Chaos.Client.Tests/CinemaSettingsTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["Defaults CinemaVolume 80, CinemaMute false, CinemaHideVideo false, CinemaLowerMusic true", "Four values load and save under their keys; volume clamped 0-100 on load", "F4 Cinema section after Sound with the four rows; slider = volume / 10", "Existing SettingDefinitionsTests pass"], "modelTier": "mechanical"}
```

---

### Task 7: Client sync maths and CinemaController

**Goal:** A graphics-free controller turns `CinemaState` packets into commands for a video player, keeps the player within 1.5 s of the server, and handles failures, all tested with a fake player.

**Files:**
- Create: `Chaos.Client/Systems/Cinema/CinemaSync.cs`
- Create: `Chaos.Client/Systems/Cinema/ICinemaVideo.cs`
- Create: `Chaos.Client/Systems/Cinema/CinemaController.cs` (also holds `enum CinemaStatus`)
- Test: `Tests/Chaos.Client.Tests/Cinema/CinemaSyncTests.cs`
- Test: `Tests/Chaos.Client.Tests/Cinema/CinemaControllerTests.cs`

**Acceptance Criteria:**
- [ ] `CinemaSync.StartPosition(serverMs, pingMs)` is `serverMs + max(0, ping ?? 0) / 2`.
- [ ] `CinemaSync.NeedsSeek(actual, expected)` is true only when they differ by more than 1500 ms.
- [ ] A new film opens a video at the start position, sets volume first, and sets paused when the state is paused (no ping added when paused).
- [ ] An empty url stops: the video is disposed, `Status` is `Idle`, nothing is showing.
- [ ] The same film with a live video seeks to the new position and sets paused; it does not open a second video.
- [ ] Pause then seek stays paused at the new spot.
- [ ] Every 5 s, while playing and not paused and `Video.Playing` is true, a drift over 1500 ms seeks to the expected position.
- [ ] While `Video.Playing` is false (buffering, `TimeMs` 0), no drift seek happens, however long it lasts.
- [ ] A failed video is disposed, `Status` becomes `Unavailable`, and a new video opens 15 s later at the expected position, not before.
- [ ] An exception from `Open` makes the film `Unavailable` instead of crashing.
- [ ] The same film arriving while `Unavailable` waits for the retry.
- [ ] Hiding video disposes it; showing it again reopens at the expected position.
- [ ] `ShouldDuckMusic(lowerMusic)` is true only when lower-music is on, video is shown, a film is showing, it is not paused, and `Status` is `Playing`.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/Chaos.Client.Tests.Cinema/*/*"` → all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests**

`Tests/Chaos.Client.Tests/Cinema/CinemaSyncTests.cs`:

```csharp
using Chaos.Client.Systems.Cinema;
using FluentAssertions;

namespace Chaos.Client.Tests.Cinema;

public class CinemaSyncTests
{
    [Test]
    [Arguments(1000L, null, 1000L)]
    [Arguments(1000L, 300L, 1150L)]
    [Arguments(1000L, -5L, 1000L)]
    public void Start_position_adds_half_the_ping(long serverMs, long? pingMs, long expected)
        => CinemaSync.StartPosition(serverMs, pingMs).Should().Be(expected);

    [Test]
    [Arguments(0L, 1500L, false)]
    [Arguments(0L, 1501L, true)]
    [Arguments(5000L, 3000L, true)]
    [Arguments(5000L, 5000L, false)]
    public void Needs_seek_past_the_threshold(long actual, long expected, bool needs)
        => CinemaSync.NeedsSeek(actual, expected).Should().Be(needs);
}
```

`Tests/Chaos.Client.Tests/Cinema/CinemaControllerTests.cs`:

```csharp
using Chaos.Client.Systems.Cinema;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Tests.Cinema;

public class CinemaControllerTests
{
    private sealed class FakeVideo : ICinemaVideo
    {
        public bool ThrowOnOpen { get; init; }
        public bool Failed { get; set; }
        public bool Playing { get; set; }
        public long TimeMs { get; set; }
        public Texture2D? Frame => null;
        public string? OpenedUrl { get; private set; }
        public long OpenedAtMs { get; private set; }
        public List<long> Seeks { get; } = [];
        public bool? LastPaused { get; private set; }
        public (int Volume, bool Mute)? LastVolume { get; private set; }
        public int Uploads { get; private set; }
        public bool Disposed { get; private set; }
        public bool VolumeSetBeforeOpen { get; private set; }

        public void Open(string url, long startMs)
        {
            if (ThrowOnOpen)
                throw new InvalidOperationException("no vlc");

            VolumeSetBeforeOpen = LastVolume is not null;
            OpenedUrl = url;
            OpenedAtMs = startMs;
        }

        public void Seek(long positionMs) => Seeks.Add(positionMs);
        public void SetPaused(bool paused) => LastPaused = paused;
        public void SetVolume(int volume, bool mute) => LastVolume = (volume, mute);
        public void UploadFrame() => Uploads++;
        public void Dispose() => Disposed = true;
    }

    private readonly List<FakeVideo> Videos = [];
    private bool NextThrows;

    private CinemaController Make()
        => new(() =>
        {
            var video = new FakeVideo { ThrowOnOpen = NextThrows };
            Videos.Add(video);

            return video;
        });

    private static CinemaStateArgs Film(uint positionMs = 60_000, bool paused = false, string id = "nosferatu")
        => new()
        {
            FilmId = id,
            Title = "Nosferatu",
            Url = $"https://x/films/{id}/index.m3u8",
            PositionMs = positionMs,
            Paused = paused
        };

    [Test]
    public void New_film_opens_at_server_position_plus_half_ping()
    {
        var c = Make();
        c.SetVolume(60, false);

        c.Apply(Film(), 1000, 200);

        Videos.Should().ContainSingle();
        Videos[0].OpenedUrl.Should().Be("https://x/films/nosferatu/index.m3u8");
        Videos[0].OpenedAtMs.Should().Be(60_100);
        Videos[0].VolumeSetBeforeOpen.Should().BeTrue();
        Videos[0].LastVolume.Should().Be((60, false));
        c.Status.Should().Be(CinemaStatus.Playing);
        c.IsShowing.Should().BeTrue();
    }

    [Test]
    public void Paused_film_opens_paused_without_ping()
    {
        var c = Make();

        c.Apply(Film(paused: true), 1000, 200);

        Videos[0].OpenedAtMs.Should().Be(60_000);
        Videos[0].LastPaused.Should().BeTrue();
    }

    [Test]
    public void Empty_url_stops()
    {
        var c = Make();
        c.Apply(Film(), 0, null);

        c.Apply(new CinemaStateArgs(), 10, null);

        Videos[0].Disposed.Should().BeTrue();
        c.Status.Should().Be(CinemaStatus.Idle);
        c.IsShowing.Should().BeFalse();
        c.Video.Should().BeNull();
    }

    [Test]
    public void Same_film_seeks_instead_of_reopening()
    {
        var c = Make();
        c.Apply(Film(), 0, null);

        c.Apply(Film(positionMs: 70_000, paused: true), 10_000, null);

        Videos.Should().ContainSingle();
        Videos[0].Seeks.Should().Equal(70_000);
        Videos[0].LastPaused.Should().BeTrue();
    }

    [Test]
    public void Pause_then_seek_stays_paused_at_new_spot()
    {
        var c = Make();
        c.Apply(Film(), 0, null);
        c.Apply(Film(positionMs: 10_000, paused: true), 5_000, null);

        c.Apply(Film(positionMs: 1_800_000, paused: true), 6_000, null);

        Videos[0].Seeks.Last().Should().Be(1_800_000);
        Videos[0].LastPaused.Should().BeTrue();
        c.ExpectedPositionMs(60_000).Should().Be(1_800_000);
    }

    [Test]
    public void Drift_past_threshold_seeks_at_the_check()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];
        video.Playing = true;
        video.TimeMs = 1_000;

        c.Update(4_999);
        video.Seeks.Should().BeEmpty();

        c.Update(5_000);
        video.Seeks.Should().Equal(5_000);
    }

    [Test]
    public void Small_drift_does_not_seek()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];
        video.Playing = true;
        video.TimeMs = 4_000;

        c.Update(5_000);

        video.Seeks.Should().BeEmpty();
    }

    [Test]
    public void Buffering_never_seeks()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        var video = Videos[0];
        video.Playing = false;
        video.TimeMs = 0;

        for (long now = 0; now <= 60_000; now += 100)
            c.Update(now);

        video.Seeks.Should().BeEmpty();
        video.Uploads.Should().BeGreaterThan(0);
    }

    [Test]
    public void Paused_never_drift_seeks()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0, paused: true), 0, null);
        var video = Videos[0];
        video.Playing = true;
        video.TimeMs = 90_000;

        c.Update(20_000);

        video.Seeks.Should().BeEmpty();
    }

    [Test]
    public void Failed_video_retries_after_fifteen_seconds()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);
        Videos[0].Failed = true;

        c.Update(1_000);

        Videos[0].Disposed.Should().BeTrue();
        c.Status.Should().Be(CinemaStatus.Unavailable);
        c.Video.Should().BeNull();

        c.Update(15_999);
        Videos.Should().HaveCount(1);

        c.Update(16_000);
        Videos.Should().HaveCount(2);
        Videos[1].OpenedAtMs.Should().Be(16_000);
        c.Status.Should().Be(CinemaStatus.Playing);
    }

    [Test]
    public void Open_exception_marks_unavailable()
    {
        var c = Make();
        NextThrows = true;

        c.Apply(Film(), 0, null);

        c.Status.Should().Be(CinemaStatus.Unavailable);
        c.Video.Should().BeNull();
        Videos[0].Disposed.Should().BeTrue();
    }

    [Test]
    public void Same_film_while_unavailable_waits_for_retry()
    {
        var c = Make();
        NextThrows = true;
        c.Apply(Film(), 0, null);
        NextThrows = false;

        c.Apply(Film(positionMs: 70_000), 1_000, null);

        Videos.Should().HaveCount(1);
        c.Status.Should().Be(CinemaStatus.Unavailable);

        c.Update(15_000);

        Videos.Should().HaveCount(2);
        Videos[1].OpenedAtMs.Should().Be(84_000);
    }

    [Test]
    public void Different_film_while_unavailable_opens_at_once()
    {
        var c = Make();
        NextThrows = true;
        c.Apply(Film(), 0, null);
        NextThrows = false;

        c.Apply(Film(id: "metropolis", positionMs: 0), 1_000, null);

        Videos.Should().HaveCount(2);
        c.Status.Should().Be(CinemaStatus.Playing);
    }

    [Test]
    public void Hiding_disposes_and_showing_reopens_at_expected_position()
    {
        var c = Make();
        c.Apply(Film(positionMs: 0), 0, null);

        c.SetShowVideo(false, 1_000);

        Videos[0].Disposed.Should().BeTrue();
        c.Video.Should().BeNull();
        c.ShouldDuckMusic(true).Should().BeFalse();

        c.SetShowVideo(true, 10_000);

        Videos.Should().HaveCount(2);
        Videos[1].OpenedAtMs.Should().Be(10_000);
    }

    [Test]
    public void Hidden_video_does_not_open_new_films()
    {
        var c = Make();
        c.SetShowVideo(false, 0);

        c.Apply(Film(), 0, null);

        Videos.Should().BeEmpty();
        c.IsShowing.Should().BeTrue();
    }

    [Test]
    public void Duck_music_only_while_a_shown_film_plays()
    {
        var c = Make();
        c.ShouldDuckMusic(true).Should().BeFalse();

        c.Apply(Film(), 0, null);
        c.ShouldDuckMusic(true).Should().BeTrue();
        c.ShouldDuckMusic(false).Should().BeFalse();

        c.Apply(Film(paused: true), 1_000, null);
        c.ShouldDuckMusic(true).Should().BeFalse();

        c.Apply(Film(), 2_000, null);
        Videos[0].Failed = true;
        c.Update(3_000);
        c.ShouldDuckMusic(true).Should().BeFalse();
    }

    [Test]
    public void Volume_reaches_the_live_video()
    {
        var c = Make();
        c.Apply(Film(), 0, null);

        c.SetVolume(30, true);

        Videos[0].LastVolume.Should().Be((30, true));
    }
}
```

If the client test project cannot reference `Microsoft.Xna.Framework.Graphics.Texture2D` (no MonoGame reference), change `ICinemaVideo.Frame` to live only on `CinemaPlayer` and drop it from the interface and the fake. Check other client tests that use `Texture2D` first.

- [ ] **Step 2: Run the tests to see them fail**

Run the Verify command. Expected: build errors, `Chaos.Client.Systems.Cinema` not found.

- [ ] **Step 3: Write CinemaSync**

`Chaos.Client/Systems/Cinema/CinemaSync.cs`:

```csharp
namespace Chaos.Client.Systems.Cinema;

public static class CinemaSync
{
    public const int CHECK_INTERVAL_MS = 5000;
    public const int RETRY_INTERVAL_MS = 15000;
    public const int SEEK_THRESHOLD_MS = 1500;

    /// <summary>Where to start so the film lines up with the server, given the round-trip ping (half of it is the trip here).</summary>
    public static long StartPosition(long serverMs, long? pingMs) => serverMs + Math.Max(0, pingMs ?? 0) / 2;

    public static bool NeedsSeek(long actualMs, long expectedMs) => Math.Abs(actualMs - expectedMs) > SEEK_THRESHOLD_MS;
}
```

- [ ] **Step 4: Write the video interface**

`Chaos.Client/Systems/Cinema/ICinemaVideo.cs`:

```csharp
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.Cinema;

/// <summary>One open film stream. Members are called from the game loop only.</summary>
public interface ICinemaVideo : IDisposable
{
    /// <summary>True once the stream has hit an error it will not recover from.</summary>
    bool Failed { get; }

    /// <summary>True while frames are actually playing; false while opening or buffering.</summary>
    bool Playing { get; }

    /// <summary>The player's position in milliseconds. Reads 0 while buffering.</summary>
    long TimeMs { get; }

    /// <summary>The latest frame, or null before the first one.</summary>
    Texture2D? Frame { get; }

    void Open(string url, long startMs);
    void Seek(long positionMs);
    void SetPaused(bool paused);
    void SetVolume(int volume, bool mute);

    /// <summary>Copies the newest decoded frame into <see cref="Frame" />, if a new one arrived.</summary>
    void UploadFrame();
}
```

- [ ] **Step 5: Write the controller**

`Chaos.Client/Systems/Cinema/CinemaController.cs`:

```csharp
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Systems.Cinema;

public enum CinemaStatus
{
    /// <summary>Nothing is showing.</summary>
    Idle,

    /// <summary>A film is showing (its video may be hidden by the player's settings).</summary>
    Playing,

    /// <summary>The film's video failed; a retry is waiting.</summary>
    Unavailable
}

/// <summary>
///     Keeps a video player in step with the server's <see cref="CinemaStateArgs" />. Times are the caller's
///     millisecond clock (Environment.TickCount64 in the game).
/// </summary>
public sealed class CinemaController(Func<ICinemaVideo> createVideo)
{
    private long AnchorNowMs;
    private long AnchorPositionMs;
    private long NextCheckMs;
    private long RetryAtMs;
    private bool ShowVideo = true;
    private int Volume = 80;
    private bool Mute;

    public CinemaStatus Status { get; private set; }
    public CinemaStateArgs? State { get; private set; }
    public ICinemaVideo? Video { get; private set; }

    public bool IsShowing => State is not null;

    public void Apply(CinemaStateArgs args, long nowMs, long? pingMs)
    {
        if (string.IsNullOrEmpty(args.Url))
        {
            Stop();

            return;
        }

        var sameFilm = State is not null && (State.FilmId == args.FilmId) && (State.Url == args.Url);
        var start = args.Paused ? args.PositionMs : CinemaSync.StartPosition(args.PositionMs, pingMs);

        State = args;
        AnchorNowMs = nowMs;
        AnchorPositionMs = start;

        if (!ShowVideo)
        {
            Status = CinemaStatus.Playing;

            return;
        }

        if (sameFilm && Video is not null)
        {
            Video.SetPaused(args.Paused);
            Video.Seek(start);
            NextCheckMs = nowMs + CinemaSync.CHECK_INTERVAL_MS;

            return;
        }

        if (sameFilm && Status == CinemaStatus.Unavailable)
            return;

        Open(start, nowMs);
    }

    public void Update(long nowMs)
    {
        if (State is null || !ShowVideo)
            return;

        if (Status == CinemaStatus.Unavailable)
        {
            if (nowMs >= RetryAtMs)
                Open(ExpectedPositionMs(nowMs), nowMs);

            return;
        }

        if (Video is null)
            return;

        if (Video.Failed)
        {
            MarkUnavailable(nowMs);

            return;
        }

        Video.UploadFrame();

        if (nowMs < NextCheckMs)
            return;

        NextCheckMs = nowMs + CinemaSync.CHECK_INTERVAL_MS;

        //VLC reads 0 while it fills its cache, so a check then would seek again and again
        if (State.Paused || !Video.Playing)
            return;

        var expected = ExpectedPositionMs(nowMs);

        if (CinemaSync.NeedsSeek(Video.TimeMs, expected))
            Video.Seek(expected);
    }

    public long ExpectedPositionMs(long nowMs)
    {
        if (State is null)
            return 0;

        return State.Paused ? AnchorPositionMs : AnchorPositionMs + (nowMs - AnchorNowMs);
    }

    public void SetShowVideo(bool show, long nowMs)
    {
        if (show == ShowVideo)
            return;

        ShowVideo = show;

        if (!show)
        {
            DisposeVideo();

            if (State is not null)
                Status = CinemaStatus.Playing;

            return;
        }

        if (State is not null)
            Open(ExpectedPositionMs(nowMs), nowMs);
    }

    public void SetVolume(int volume, bool mute)
    {
        if ((volume == Volume) && (mute == Mute))
            return;

        Volume = volume;
        Mute = mute;
        Video?.SetVolume(volume, mute);
    }

    public void Stop()
    {
        DisposeVideo();
        State = null;
        Status = CinemaStatus.Idle;
    }

    public bool ShouldDuckMusic(bool lowerMusic)
        => lowerMusic && ShowVideo && State is { Paused: false } && (Status == CinemaStatus.Playing);

    private void Open(long startMs, long nowMs)
    {
        DisposeVideo();
        var video = createVideo();
        Video = video;

        try
        {
            video.SetVolume(Volume, Mute);
            video.Open(State!.Url, startMs);

            if (State.Paused)
                video.SetPaused(true);

            Status = CinemaStatus.Playing;
            NextCheckMs = nowMs + CinemaSync.CHECK_INTERVAL_MS;
        } catch (Exception)
        {
            MarkUnavailable(nowMs);
        }
    }

    private void MarkUnavailable(long nowMs)
    {
        DisposeVideo();
        Status = CinemaStatus.Unavailable;
        RetryAtMs = nowMs + CinemaSync.RETRY_INTERVAL_MS;
    }

    private void DisposeVideo()
    {
        Video?.Dispose();
        Video = null;
    }
}
```

Match the client's private field naming (look at another class in `Chaos.Client/Systems/`). If `createVideo()` itself can throw (LibVLC missing), move it inside the `try` and keep `Video` null on failure; the `Open_exception_marks_unavailable` test must still see the fake disposed.

- [ ] **Step 6: Run the tests to see them pass**

Run the Verify command. Expected: every Cinema test passes.

```json:metadata
{"files": ["Chaos.Client/Systems/Cinema/CinemaSync.cs", "Chaos.Client/Systems/Cinema/ICinemaVideo.cs", "Chaos.Client/Systems/Cinema/CinemaController.cs", "Tests/Chaos.Client.Tests/Cinema/CinemaSyncTests.cs", "Tests/Chaos.Client.Tests/Cinema/CinemaControllerTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/Chaos.Client.Tests.Cinema/*/*\"", "acceptanceCriteria": ["StartPosition = serverMs + max(0, ping)/2", "NeedsSeek only past 1500 ms", "New film opens at start position, volume set first, paused when paused, no ping when paused", "Empty url stops: disposed, Idle, nothing showing", "Same film seeks and sets paused without reopening", "Pause then seek stays paused at the new spot", "Drift over 1500 ms seeks at the 5 s check while playing", "No drift seek while Video.Playing is false", "Failed video: disposed, Unavailable, retry after 15 s", "Open exception gives Unavailable, no crash", "Same film while Unavailable waits for retry", "Hide disposes, show reopens at expected position", "ShouldDuckMusic only when lower-music on, shown, showing, not paused, Playing"], "modelTier": "standard"}
```

---

### Task 8: LibVLC player and screen layout

**Goal:** A real `ICinemaVideo` built on LibVLC that streams HLS into a `Texture2D`, plus the screen-spot layout maths for the hall.

**Files:**
- Modify: `Directory.Packages.props` (add two `PackageVersion` lines next to the MonoGame one, line 12)
- Modify: `Chaos.Client/Chaos.Client.csproj` (add two `PackageReference` lines next to `TextCopy`, line 41)
- Create: `Chaos.Client/Systems/Cinema/CinemaPlayer.cs`
- Create: `Chaos.Client/Systems/Cinema/CinemaScreenLayout.cs`
- Test: `Tests/Chaos.Client.Tests/Cinema/CinemaScreenLayoutTests.cs`

**Acceptance Criteria:**
- [ ] `LibVLCSharp` and `VideoLAN.LibVLC.Windows` are added at their latest stable versions (exact versions, no wildcards).
- [ ] After a build, `Chaos.Client/bin/Debug/net10.0/libvlc/win-x64/libvlc.dll` exists (or the output folder's equivalent; find it with `find Chaos.Client/bin -name libvlc.dll`).
- [ ] `CinemaPlayer` implements `ICinemaVideo` with one shared `LibVLC` (lazy, after `Core.Initialize()`), RV32 854×480 frames through `SetVideoFormat` and `SetVideoCallbacks`, and delegates kept in fields.
- [ ] `UploadFrame` swaps B and R, sets alpha to 255, and calls `SetData` only when a new frame arrived.
- [ ] Media options are `:network-caching=3000` and `:start-time=<seconds>` written with the invariant culture.
- [ ] `EncounteredError` sets `Failed`; `Playing` sets `Playing`; `EndReached` and `Stopped` clear `Playing`.
- [ ] `Dispose` stops and disposes the player and media on `Task.Run`, frees the frame buffer after the player is stopped, and disposes the texture on the calling thread.
- [ ] `CinemaScreenLayout.FitPicture(new Rectangle(0, 0, 360, 190), 854, 480)` returns `(11, 0, 338, 190)`.
- [ ] Client builds; all client tests pass.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/CinemaScreenLayoutTests/*"` → pass; `find Chaos.Client/bin -name libvlc.dll` → at least one path under `libvlc/win-x64`.

**Steps:**

- [ ] **Step 1: Write the failing test**

`Tests/Chaos.Client.Tests/Cinema/CinemaScreenLayoutTests.cs`:

```csharp
using Chaos.Client.Systems.Cinema;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.Cinema;

public class CinemaScreenLayoutTests
{
    [Test]
    public void Wide_area_is_pillarboxed()
        => CinemaScreenLayout.FitPicture(new Rectangle(0, 0, 360, 190), 854, 480).Should().Be(new Rectangle(11, 0, 338, 190));

    [Test]
    public void Tall_area_is_letterboxed()
        => CinemaScreenLayout.FitPicture(new Rectangle(0, 0, 400, 400), 854, 480).Should().Be(new Rectangle(0, 88, 400, 224));

    [Test]
    public void Area_offset_is_kept()
        => CinemaScreenLayout.FitPicture(new Rectangle(100, 50, 360, 190), 854, 480).Should().Be(new Rectangle(111, 50, 338, 190));

    [Test]
    public void Hall_spot_depth_is_anchor_sum()
        => CinemaScreenLayout.Hall.Depth.Should().Be(CinemaScreenLayout.Hall.AnchorX + CinemaScreenLayout.Hall.AnchorY);
}
```

- [ ] **Step 2: Run the test to see it fail**

Run the Verify command. Expected: build error, `CinemaScreenLayout` not found.

- [ ] **Step 3: Write the layout**

`Chaos.Client/Systems/Cinema/CinemaScreenLayout.cs`:

```csharp
using Microsoft.Xna.Framework;

namespace Chaos.Client.Systems.Cinema;

/// <summary>
///     Where the film screen sits on a map. The picture is drawn in world pixels at the anchor tile's top-left corner
///     plus the offset, in the draw stripe of <see cref="Depth" /> so walls and people in front cover it.
/// </summary>
public readonly record struct CinemaScreenSpot(int AnchorX, int AnchorY, int OffsetX, int OffsetY, int Width, int Height)
{
    public int Depth => AnchorX + AnchorY;
}

public static class CinemaScreenLayout
{
    public const int HALL_MAP_ID = 15;
    public const int VIDEO_WIDTH = 854;
    public const int VIDEO_HEIGHT = 480;

    //placeholder until Plan 3 paints the castle-wall screen and sets the real spot
    public static CinemaScreenSpot Hall { get; } = new(AnchorX: 8, AnchorY: 2, OffsetX: -180, OffsetY: -200, Width: 360, Height: 190);

    /// <summary>The largest rectangle with the picture's shape that fits centred in <paramref name="area" />.</summary>
    public static Rectangle FitPicture(Rectangle area, int pictureWidth, int pictureHeight)
    {
        if ((long)area.Width * pictureHeight > (long)area.Height * pictureWidth)
        {
            var width = area.Height * pictureWidth / pictureHeight;

            return new Rectangle(area.X + (area.Width - width) / 2, area.Y, width, area.Height);
        }

        var height = area.Width * pictureHeight / pictureWidth;

        return new Rectangle(area.X, area.Y + (area.Height - height) / 2, area.Width, height);
    }
}
```

- [ ] **Step 4: Add the packages**

Look up the latest stable versions on nuget.org (`dotnet package search LibVLCSharp --exact-match` and `dotnet package search VideoLAN.LibVLC.Windows --exact-match`, or the nuget.org pages). Do not take a prerelease. At the time of writing these were LibVLCSharp 3.9.x and VideoLAN.LibVLC.Windows 3.0.21; use whatever is newest and stable.

`Directory.Packages.props`, next to the MonoGame line:

```xml
    <PackageVersion Include="LibVLCSharp" Version="<latest stable>" />
    <PackageVersion Include="VideoLAN.LibVLC.Windows" Version="<latest stable>" />
```

`Chaos.Client/Chaos.Client.csproj`, next to `<PackageReference Include="TextCopy"/>`:

```xml
        <PackageReference Include="LibVLCSharp"/>
        <PackageReference Include="VideoLAN.LibVLC.Windows"/>
```

Build once, then run `find Chaos.Client/bin -name libvlc.dll`. If it is missing, look at how the package's build targets copy `libvlc/win-x64` (it needs a Windows runtime identifier or `<RuntimeIdentifier>`-free copy); fix the csproj so the folder lands in the output. Do not commit the native files.

- [ ] **Step 5: Write the player**

`Chaos.Client/Systems/Cinema/CinemaPlayer.cs`:

```csharp
using System.Globalization;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.Cinema;

/// <summary>Streams one film with LibVLC and hands its frames to the game as a texture.</summary>
public sealed class CinemaPlayer : ICinemaVideo
{
    private const int WIDTH = CinemaScreenLayout.VIDEO_WIDTH;
    private const int HEIGHT = CinemaScreenLayout.VIDEO_HEIGHT;
    private const int PITCH = WIDTH * 4;
    private const int FRAME_BYTES = PITCH * HEIGHT;

    private static readonly Lazy<LibVLC> Vlc = new(() =>
    {
        Core.Initialize();

        return new LibVLC("--no-video-title-show");
    });

    private readonly GraphicsDevice Device;
    private readonly MediaPlayer Player;
    private readonly nint Buffer;
    private readonly byte[] Staging = new byte[FRAME_BYTES];
    private readonly byte[] Upload = new byte[FRAME_BYTES];
    private readonly Lock FrameLock = new();

    //kept in fields so the garbage collector can't free them while VLC still calls them
    private readonly MediaPlayer.LibVLCVideoLockCb LockCallback;
    private readonly MediaPlayer.LibVLCVideoDisplayCb DisplayCallback;

    private Media? CurrentMedia;
    private Texture2D? Texture;
    private volatile bool FrameReady;
    private volatile bool IsFailed;
    private volatile bool IsPlaying;
    private bool Disposed;

    public CinemaPlayer(GraphicsDevice device)
    {
        Device = device;
        Player = new MediaPlayer(Vlc.Value);
        Buffer = Marshal.AllocHGlobal(FRAME_BYTES);
        LockCallback = OnLock;
        DisplayCallback = OnDisplay;

        Player.SetVideoFormat("RV32", WIDTH, HEIGHT, PITCH);
        Player.SetVideoCallbacks(LockCallback, null, DisplayCallback);

        Player.EncounteredError += (_, _) => IsFailed = true;
        Player.Playing += (_, _) => IsPlaying = true;
        Player.EndReached += (_, _) => IsPlaying = false;
        Player.Stopped += (_, _) => IsPlaying = false;
    }

    public bool Failed => IsFailed;
    public bool Playing => IsPlaying;
    public long TimeMs => Math.Max(0, Player.Time);
    public Texture2D? Frame => Texture;

    public void Open(string url, long startMs)
    {
        var start = (startMs / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);
        CurrentMedia = new Media(Vlc.Value, new Uri(url), ":network-caching=3000", $":start-time={start}");
        Player.Play(CurrentMedia);
    }

    public void Seek(long positionMs) => Player.Time = Math.Max(0, positionMs);

    public void SetPaused(bool paused) => Player.SetPause(paused);

    public void SetVolume(int volume, bool mute)
    {
        Player.Volume = Math.Clamp(volume, 0, 100);
        Player.Mute = mute;
    }

    public void UploadFrame()
    {
        if (!FrameReady)
            return;

        using (FrameLock.EnterScope())
        {
            Array.Copy(Staging, Upload, FRAME_BYTES);
            FrameReady = false;
        }

        //VLC's RV32 is B, G, R, X in memory; the texture wants R, G, B, A
        for (var i = 0; i < FRAME_BYTES; i += 4)
        {
            (Upload[i], Upload[i + 2]) = (Upload[i + 2], Upload[i]);
            Upload[i + 3] = 255;
        }

        Texture ??= new Texture2D(Device, WIDTH, HEIGHT, false, SurfaceFormat.Color);
        Texture.SetData(Upload);
    }

    public void Dispose()
    {
        if (Disposed)
            return;

        Disposed = true;
        Texture?.Dispose();
        Texture = null;

        var player = Player;
        var media = CurrentMedia;
        var buffer = Buffer;

        //Stop waits for VLC's threads, which can take a moment; the game loop must not wait for it
        _ = Task.Run(() =>
        {
            player.Stop();
            player.Dispose();
            media?.Dispose();
            Marshal.FreeHGlobal(buffer);
        });
    }

    private nint OnLock(nint opaque, nint planes)
    {
        Marshal.WriteIntPtr(planes, Buffer);

        return nint.Zero;
    }

    private void OnDisplay(nint opaque, nint picture)
    {
        using (FrameLock.EnterScope())
        {
            Marshal.Copy(Buffer, Staging, 0, FRAME_BYTES);
            FrameReady = true;
        }
    }
}
```

The delegate type names (`LibVLCVideoLockCb`, `LibVLCVideoDisplayCb`) and the `SetVideoCallbacks` parameter order must match the LibVLCSharp version you installed; check its API and adjust. If `SetData` with a `byte[]` is not accepted for `SurfaceFormat.Color`, reinterpret with `MemoryMarshal.Cast<byte, Color>` and call `SetData<Color>`.

- [ ] **Step 6: Build and run the tests**

Run the Verify command. Expected: the layout tests pass, the whole client test project has no new failures, and `libvlc.dll` is in the output under `libvlc/win-x64`.

```json:metadata
{"files": ["Directory.Packages.props", "Chaos.Client/Chaos.Client.csproj", "Chaos.Client/Systems/Cinema/CinemaPlayer.cs", "Chaos.Client/Systems/Cinema/CinemaScreenLayout.cs", "Tests/Chaos.Client.Tests/Cinema/CinemaScreenLayoutTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build && find Chaos.Client/bin -name libvlc.dll", "acceptanceCriteria": ["LibVLCSharp and VideoLAN.LibVLC.Windows at exact latest stable versions", "libvlc.dll under libvlc/win-x64 in the build output", "CinemaPlayer: shared lazy LibVLC after Core.Initialize, RV32 854x480 callbacks, delegates in fields", "UploadFrame swaps B and R, alpha 255, SetData only on a new frame", "Media options :network-caching=3000 and invariant :start-time", "EncounteredError sets Failed, Playing sets Playing, EndReached/Stopped clear it", "Dispose stops/disposes on Task.Run and frees the buffer after stop", "FitPicture(0,0,360,190; 854x480) = (11,0,338,190)", "Client builds and tests pass"], "modelTier": "standard"}
```

---

### Task 9: Client wiring and drawing

**Goal:** The client receives `CinemaState`, drives `CinemaController` every frame, lowers the music, and draws the film (or "Film unavailable") on the hall wall.

**Files:**
- Modify: `Chaos.Client.Networking/Definitions/Delegates.cs` (next to `StageLightingStateHandler`, line 492)
- Modify: `Chaos.Client.Networking/ConnectionManager.cs` (event next to line 639, registration next to line 2022, handler next to line 2542)
- Create: `Chaos.Client/Screens/WorldScreen.Cinema.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.cs` (`WireCinema()` after `WireStageLighting()` at line 904; `UnwireCinema()` after `UnwireStageLighting()` at line 1156)
- Modify: `Chaos.Client/Screens/WorldScreen.Map.cs` (`ResetCinema()` next to `ResetStageLighting()`, line 131)
- Modify: `Chaos.Client/Screens/WorldScreen.Update.cs` (`UpdateCinema()` right before `UpdateSpotlights()`, line 299)
- Modify: `Chaos.Client/Screens/WorldScreen.Draw.cs` (main stripe loop, line 429)
- Modify: `Chaos.Client/Systems/Cinema/CinemaScreenLayout.cs` (add `DrawStripe`)
- Modify: `CLAUDE.md` (client repo root: one line for `WorldScreen.Cinema.cs` in the WorldScreen partial list, one for `Systems/Cinema/` in Game Systems)
- Test: `Tests/Chaos.Client.Tests/Cinema/CinemaScreenLayoutTests.cs` (add three tests)

**Acceptance Criteria:**
- [ ] `ConnectionManager` has `public event CinemaStateHandler? OnCinemaState;`, registers `PacketHandlers[(byte)ServerOpCode.CinemaState] = HandleCinemaState;`, and its handler deserializes `CinemaStateArgs` and raises the event.
- [ ] `WireCinema` creates the controller with `() => new CinemaPlayer(Device)`, subscribes to `OnCinemaState`, and applies `SetShowVideo(!ClientSettings.CinemaHideVideo, now)` at once. `UnwireCinema` unsubscribes, stops the controller and lifts the duck.
- [ ] Each frame `UpdateCinema` applies `SetShowVideo` and `SetVolume(CinemaVolume, CinemaMute)`, then `Update(now)`, then `SoundSystem.SetCinemaDuck(ShouldDuckMusic(CinemaLowerMusic))`.
- [ ] `ResetCinema` (map change) stops the film and lifts the duck.
- [ ] The screen is drawn only when `CurrentMapId == CinemaScreenLayout.HALL_MAP_ID`, a film is showing and video is not hidden, in the stripe `CinemaScreenLayout.DrawStripe(Hall.Depth, minDepth, maxDepth)`, before that stripe's tiles and entities.
- [ ] A shown frame is drawn as a black fill of the spot plus the frame fitted with `FitPicture`. While `Status == Unavailable`, a grey fill plus a cached `TextRenderer.BuildSmallText(Device, "Film unavailable")` texture, centred.
- [ ] `DrawStripe` clamps the spot depth into `[minDepth, maxDepth]`; three new tests pin it.
- [ ] Client builds; all client tests pass.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build` → build succeeds, 0 failed.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Add to `Tests/Chaos.Client.Tests/Cinema/CinemaScreenLayoutTests.cs`, inside the class:

```csharp
    [Test]
    public void Draw_stripe_inside_range_is_spot_depth()
        => CinemaScreenLayout.DrawStripe(10, 4, 30).Should().Be(10);

    [Test]
    public void Draw_stripe_above_view_is_first_stripe()
        => CinemaScreenLayout.DrawStripe(2, 4, 30).Should().Be(4);

    [Test]
    public void Draw_stripe_below_view_is_last_stripe()
        => CinemaScreenLayout.DrawStripe(40, 4, 30).Should().Be(30);
```

- [ ] **Step 2: Run the tests to see them fail**

Run the Verify command. Expected: build error, `DrawStripe` not found.

- [ ] **Step 3: Add `DrawStripe`**

In `Chaos.Client/Systems/Cinema/CinemaScreenLayout.cs`, inside `CinemaScreenLayout`, after `FitPicture`:

```csharp
    /// <summary>
    ///     The stripe to draw the screen in. The picture hangs above its anchor, so it can be on screen while the anchor
    ///     tile's stripe is not; then it is drawn in the nearest visible stripe.
    /// </summary>
    public static int DrawStripe(int spotDepth, int minDepth, int maxDepth) => Math.Clamp(spotDepth, minDepth, maxDepth);
```

- [ ] **Step 4: Networking**

`Chaos.Client.Networking/Definitions/Delegates.cs`, right after `public delegate void StageLightingStateHandler(StageLightingStateArgs args);`:

```csharp
public delegate void CinemaStateHandler(CinemaStateArgs args);
```

`Chaos.Client.Networking/ConnectionManager.cs`:

Next to `public event StageLightingStateHandler? OnStageLightingState;`:

```csharp
    public event CinemaStateHandler? OnCinemaState;
```

Next to `PacketHandlers[(byte)ServerOpCode.StageLightingState] = HandleStageLightingState;`:

```csharp
        PacketHandlers[(byte)ServerOpCode.CinemaState] = HandleCinemaState;
```

Next to the `HandleStageLightingState` method, copying its signature exactly (same parameter list, same `in pkt` deserialize):

```csharp
    private void HandleCinemaState(ref ServerPacket pkt)
    {
        var args = Client.Deserialize<CinemaStateArgs>(in pkt);
        OnCinemaState?.Invoke(args);
    }
```

If `HandleStageLightingState`'s parameter is declared differently, match it.

- [ ] **Step 5: Write `WorldScreen.Cinema.cs`**

`Chaos.Client/Screens/WorldScreen.Cinema.cs`:

```csharp
#region
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.Systems.Cinema;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private CinemaController? Cinema;
    private Texture2D? CinemaPixel;
    private Texture2D? CinemaUnavailableText;

    private void WireCinema()
    {
        Cinema = new CinemaController(() => new CinemaPlayer(Device));
        Cinema.SetShowVideo(!ClientSettings.CinemaHideVideo, Environment.TickCount64);
        Game.Connection.OnCinemaState += HandleCinemaState;
    }

    private void UnwireCinema()
    {
        Game.Connection.OnCinemaState -= HandleCinemaState;
        ResetCinema();
        CinemaPixel?.Dispose();
        CinemaPixel = null;
        CinemaUnavailableText?.Dispose();
        CinemaUnavailableText = null;
    }

    private void HandleCinemaState(CinemaStateArgs args)
        => Cinema?.Apply(args, Environment.TickCount64, LatencyMonitor.LatencyMs);

    private void UpdateCinema()
    {
        if (Cinema is null)
            return;

        var now = Environment.TickCount64;
        Cinema.SetShowVideo(!ClientSettings.CinemaHideVideo, now);
        Cinema.SetVolume(ClientSettings.CinemaVolume, ClientSettings.CinemaMute);
        Cinema.Update(now);
        Game.SoundSystem.SetCinemaDuck(Cinema.ShouldDuckMusic(ClientSettings.CinemaLowerMusic));
    }

    private void ResetCinema()
    {
        Cinema?.Stop();
        Game.SoundSystem.SetCinemaDuck(false);
    }

    private bool CinemaScreenOn
        => Cinema is { IsShowing: true }
           && (CurrentMapId == CinemaScreenLayout.HALL_MAP_ID)
           && !ClientSettings.CinemaHideVideo
           && ((Cinema.Status == CinemaStatus.Unavailable) || Cinema.Video?.Frame is not null);

    private void DrawCinemaScreen(BatchBlendScope scope)
    {
        var spot = CinemaScreenLayout.Hall;
        var tile = Camera.WorldToScreen(Camera.TileToWorld(spot.AnchorX, spot.AnchorY, MapFile!.Height));
        var area = new Rectangle((int)MathF.Floor(tile.X) + spot.OffsetX, (int)MathF.Floor(tile.Y) + spot.OffsetY, spot.Width, spot.Height);

        if (CinemaPixel is null)
        {
            CinemaPixel = new Texture2D(Device, 1, 1);
            CinemaPixel.SetData([Color.White]);
        }

        scope.Require(BlendState.AlphaBlend);

        if (Cinema!.Status == CinemaStatus.Unavailable)
        {
            scope.Batch.Draw(CinemaPixel, area, new Color(40, 40, 40));
            CinemaUnavailableText ??= TextRenderer.BuildSmallText(Device, "Film unavailable");

            if (CinemaUnavailableText is { } text)
                scope.Batch.Draw(
                    text,
                    new Vector2(area.X + (area.Width - text.Width) / 2, area.Y + (area.Height - text.Height) / 2),
                    Color.White);

            return;
        }

        scope.Batch.Draw(CinemaPixel, area, Color.Black);
        scope.Batch.Draw(Cinema.Video!.Frame!, CinemaScreenLayout.FitPicture(area, CinemaScreenLayout.VIDEO_WIDTH, CinemaScreenLayout.VIDEO_HEIGHT), Color.White);
    }
}
```

Check these names against the code before building, and adjust to what exists:
- The `SoundSystem` instance: find how other `WorldScreen` partials reach it (search `SoundSystem.` in `Chaos.Client/Screens`). Use the same access path.
- The namespace of `BatchBlendScope` and `TextRenderer` (see the usings of `WorldScreen.CollegeWall.cs` and any file that calls `TextRenderer.BuildSmallText`).
- The namespace of `CinemaStateArgs` from Task 1.
- If Task 7 moved `Frame` off `ICinemaVideo` (its fallback for a test project without MonoGame), read it as `(Cinema.Video as CinemaPlayer)?.Frame` in both `CinemaScreenOn` and `DrawCinemaScreen`.

- [ ] **Step 6: Hook it up**

`WorldScreen.cs`: add `WireCinema();` on the line after `WireStageLighting();` and `UnwireCinema();` on the line after `UnwireStageLighting();`.

`WorldScreen.Map.cs`: add `ResetCinema();` on the line after `ResetStageLighting();`.

`WorldScreen.Update.cs`: add `UpdateCinema();` on the line before the `//theatre spotlights first` comment above `UpdateSpotlights();`.

`WorldScreen.Draw.cs`: before the stripe loop `for (var depth = minDepth; depth <= maxDepth; depth++)` (after `var collegeWallOn = ...`), add:

```csharp
        var cinemaStripe = CinemaScreenOn ? CinemaScreenLayout.DrawStripe(CinemaScreenLayout.Hall.Depth, minDepth, maxDepth) : int.MinValue;
```

As the first statement inside that loop body, before the entity collection:

```csharp
            if (depth == cinemaStripe)
                DrawCinemaScreen(scope);
```

Only the main stripe loop gets this. Leave the second loop (around line 512) alone. If `scope` is declared after the loop starts, move the call to the first point in the loop body where `scope` is in scope, still before any tile or entity of that stripe is drawn. Add `using Chaos.Client.Systems.Cinema;` to `WorldScreen.Draw.cs` if it is not already there.

- [ ] **Step 7: Update CLAUDE.md**

In the client `CLAUDE.md`, under the `WorldScreen` partial file list, after the `WorldScreen.StageLighting.cs` line:

```markdown
  - `WorldScreen.Cinema.cs` -- Cinema: CinemaState packets to `CinemaController`, the music duck, and the film screen on the Loures 2 Floor Hall wall (map 15)
```

Under "Game Systems", after the `StageLightAnimator` entry:

```markdown
- **`Cinema/`** -- Films in the hall. `CinemaController` keeps an `ICinemaVideo` in step with the server's `CinemaStateArgs` (drift check every 5 s, seek past 1.5 s, retry a failed film after 15 s; no seek while VLC buffers); `CinemaSync` holds the pure timing maths; `CinemaPlayer` is the LibVLC player (HLS into a 854 x 480 `Texture2D`); `CinemaScreenLayout` holds the screen spot and picture fitting.
```

- [ ] **Step 8: Build and run the tests**

Run the Verify command. Expected: build succeeds, the three new tests pass, 0 failed overall.

```json:metadata
{"files": ["Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client.Networking/ConnectionManager.cs", "Chaos.Client/Screens/WorldScreen.Cinema.cs", "Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/Screens/WorldScreen.Map.cs", "Chaos.Client/Screens/WorldScreen.Update.cs", "Chaos.Client/Screens/WorldScreen.Draw.cs", "Chaos.Client/Systems/Cinema/CinemaScreenLayout.cs", "CLAUDE.md", "Tests/Chaos.Client.Tests/Cinema/CinemaScreenLayoutTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["OnCinemaState event, CinemaState handler registered, handler deserializes CinemaStateArgs", "WireCinema creates controller with CinemaPlayer(Device), subscribes, applies SetShowVideo at once; UnwireCinema unsubscribes, stops, lifts duck", "UpdateCinema: SetShowVideo, SetVolume, Update(now), SetCinemaDuck(ShouldDuckMusic(CinemaLowerMusic))", "ResetCinema on map change stops the film and lifts the duck", "Screen drawn only on map 15 while showing and video not hidden, in DrawStripe(Hall.Depth, min, max) before that stripe's tiles", "Frame: black fill + FitPicture frame; Unavailable: grey fill + cached Film unavailable text", "DrawStripe clamps into [min, max]; three tests", "Client builds and tests pass"], "modelTier": "standard"}
```

---

### Task 10: Commit the full implementation

**Goal:** One commit in each of the three repos (server submodule, Unora worktree, client with the submodule pointer), staged by explicit path, not pushed.

**Files:**
- No new files. Commits every file listed in Tasks 1–9.

**Acceptance Criteria:**
- [ ] The server submodule (`worktrees/cinema-client/Chaos-Server`) has one new commit on `cinema-playback` with every server file from Tasks 1–4.
- [ ] The Unora worktree (`worktrees/cinema-unora`) has one new commit on `cinema-playback` with the `Data/` files from Task 4.
- [ ] The client worktree has one new commit on `cinema-playback` with every client file from Tasks 5–9 plus the `Chaos-Server` submodule pointer.
- [ ] `git status --short` in each of the three shows no file from this plan left unstaged.
- [ ] Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Nothing is pushed.

**Verify:** `git -C <repo> log -1 --stat` in each of the three → the commit lists the plan's files; `git -C <repo> status --short` → no plan file listed.

**Steps:**

- [ ] **Step 1: List the changed files in each repo**

Run `git status --short` in `worktrees/cinema-client/Chaos-Server`, `worktrees/cinema-unora` and `worktrees/cinema-client`. Compare each list with the **Files** sections of Tasks 1–9. Any file in a list that no task names must be explained before you commit it; leave unknown files out.

- [ ] **Step 2: Commit the server submodule**

Write the message with the Write tool to `<scratchpad>/cinema-server-commit.txt` (UTF-8, no BOM):

```text
Add cinema showings: CinemaState packet, /film command and hall map script

Admins run films in the Loures 2 Floor Hall with /film list, play, pause,
resume, seek and stop. The server keeps one showing, sends CinemaState
(opcode 171) to everyone in the hall and ends a film at its length.
CLIENT_VERSION 806.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

Then, inside `worktrees/cinema-client/Chaos-Server`: `git add <each server path from Tasks 1-4>` and `git commit -F <scratchpad>/cinema-server-commit.txt`.

- [ ] **Step 3: Commit the Unora worktree**

Message file `<scratchpad>/cinema-unora-commit.txt`:

```text
Add the cinemamap script to the Loures 2 Floor Hall

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

Inside `worktrees/cinema-unora`: `git add <each Data/ path from Task 4>`, then `git commit -F <scratchpad>/cinema-unora-commit.txt`.

- [ ] **Step 4: Commit the client**

Message file `<scratchpad>/cinema-client-commit.txt`:

```text
Play hall films on the wall screen with LibVLC

The client follows the server's CinemaState: it streams the film, keeps it
within 1.5 s of the server, retries a failed film after 15 s, lowers game
music while a film plays, and draws the picture on the Loures 2 Floor Hall
wall. New F4 Cinema settings: film volume, mute, hide video, lower music.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

Inside `worktrees/cinema-client`: `git add <each client path from Tasks 5-9> Chaos-Server`, then `git commit -F <scratchpad>/cinema-client-commit.txt`.

- [ ] **Step 5: Check**

Run the Verify commands. Do not push.

```json:metadata
{"files": [], "verifyCommand": "git -C <repo> log -1 --stat && git -C <repo> status --short (for each of the three repos)", "acceptanceCriteria": ["Server submodule: one commit on cinema-playback with all Task 1-4 server files", "Unora worktree: one commit on cinema-playback with the Task 4 Data files", "Client: one commit on cinema-playback with Task 5-9 files and the submodule pointer", "No plan file left unstaged in any of the three", "Messages end with the Co-Authored-By line; nothing pushed"], "modelTier": "mechanical"}
```
