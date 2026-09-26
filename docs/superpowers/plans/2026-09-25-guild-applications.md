# Guild Applications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A player without a guild applies to guilds from a new board outside the guild hall on Abel Port Way. Officers accept or decline at Quill and Aricin, even while the applicant is offline. A player accepted by several guilds picks one at login or at the board.

**Architecture:** One server-wide file, `GuildApplicationState.json`, holds every application, keyed by applicant name. A new singleton, `GuildApplicationService`, owns every rule: the limits, the 14-day expiry, accept, decline, join and disband cleanup. Three thin pieces call it: `GuildApplyScript` (the applicant's menus, opened by the board's `GuildApplicationBoardScript` and by the login pop-up), `GuildApplicationReviewScript` (the officers' menus under Members), and `DefaultAislingScript.OnLogin`. `Guild.AddMember` gets an overload that takes the accepting officer's name, because that officer may be offline. The board itself is two foreground tiles written into `lod180.map`; the client downloads the changed map from the server.

**Tech Stack:** C# 14 / .NET 10, Chaos-Server dialog and reactor scripts, `IStorage<T>` local JSON storage with `System.Text.Json` source generation, TUnit + FluentAssertions + Moq, Unora JSON dialog templates, a binary Dark Ages `.map` file.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-09-25-guild-applications-design.md` (committed on `main` as `d15c7f5`; amended while planning, and the amendment is committed with this plan in Task 7).

## Global Constraints

- **Work only in the two worktrees from Task 0.** Never edit, stage, stash, reset or switch branches in the shared checkouts (`C:/Users/Michael/Documents/GitHub/Chaos.Client`, its `Chaos-Server` submodule, `C:/Users/Michael/Documents/GitHub/Unora`). Other Claude sessions work in them. Never touch the other folders under `C:/Users/Michael/Documents/GitHub/worktrees/`.
  - `SRV` = `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server` (Chaos-Server, branch `feat/guild-applications` from `master`)
  - `UNO` = `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora` (Unora, branch `feat/guild-applications` from `main`)
  - The only file this plan touches in the shared `Chaos.Client` checkout is in Task 7: the plan, its tasks file and the spec, committed by path.
- **Do not commit** in Tasks 0–6. Leave all changes in the worktrees. Task 7 makes one commit per repo (at-end strategy).
- **Test projects are TUnit executables.** Use `dotnet run --project ... -- --treenode-filter "..." --no-ansi`, never `dotnet test`. The filter pattern is `/Assembly/Namespace/Class/Method`.
- **Two server tests already fail on master:** `GiveAbility` and `OnItemDroppedOn` (stackable). Leave them alone. Every other test must pass.
- **If a build fails with MSB3027** (a file is locked), something is running from that output folder. Stop and report. Don't kill any process.
- **Serena for C#.** Read and edit C# files with Serena's tools, as the user's global CLAUDE.md requires. Serena paths are relative to `C:/Users/Michael/Documents/GitHub`, so a server file is `worktrees/guild-applications-server/Chaos/...`. Before a `replace_symbol_body`, read the symbol with `find_symbol` and `include_body=true`. For C# methods, Serena's body includes attributes such as `[Test]` but not the `///` doc comment, so the replacement code below starts at the attribute or signature. Create brand-new C# files with the Write tool. JSON, Markdown and the `.map` file use the built-in tools or the Python given.
- **Line endings.** A fresh worktree may check out CRLF (`core.autocrlf` is `true`). If a multi-line literal `replace_content` needle doesn't match, retry the same needle in regex mode with each line break written as `\r?\n` and the other regex characters escaped. When a replacement adds lines, pass real line breaks in the replacement string, never the two characters `\n`.
- **Never stage** `Chaos/appsettings.json`, `launchSettings.json`, or anything under `UNO/Custom Client Mods/**/obj`.
- **The game font is 6×12 ASCII.** Use only ASCII in dialog text, letters and messages. No em dashes, no curly quotes.
- **New tests** go in `SRV/Tests/Chaos.Tests/GuildApplications/`, namespace `Chaos.Tests.GuildApplications`.
- **Exact values:**

  | Value | Setting |
  |---|---|
  | Limits | `GuildApplicationService.MAX_OPEN_APPLICATIONS = 5` (waiting + accepted, one per guild), `NOTE_MAX_LENGTH = 60`, `Lifetime = 14 days` from `AppliedAt` for waiting, from `AcceptedAt` for offers. Expired when `ExpiresAt < now`. |
  | State file | `Data/LocalStorage/GuildApplicationState.json` (from the type name `GuildApplicationState`) |
  | Board | `lod180.map` tile (5,19): background 12406 kept, left foreground 737, right foreground 738 |
  | Clickable spots | reactor script key `GuildApplicationBoard` at `(5, 19)`, `(4, 18)`, `(4, 19)` on `abel_port_way` |
  | Script keys | `GuildApply` (class `GuildApplyScript`), `GuildApplicationReview` (class `GuildApplicationReviewScript`), `GuildApplicationBoard` (class `GuildApplicationBoardScript`) |
  | Applicant dialog keys | `generic_guild_apply_initial`, `_list`, `_note`, `_confirmation`, `_sent`, `_mine`, `_waiting`, `_withdrawn`, `_offers`, `_offer`, `_joined`, `_turned_down` (each prefixed `generic_guild_apply`) |
  | Officer dialog keys | `generic_guild_applications_initial`, `_view`, `_accepted`, `_declined` (each prefixed `generic_guild_applications`) |
  | Paging | 10 per page; options `Next page` then `Previous page`, after the entries and before the template's options |
  | Members menu | `Applications (N)` (or `Applications` when nothing waits) right after `Roster`, for ranks with `Admit`, at Quill and Aricin |
  | Officer notice on apply | `<name> has applied to join the guild. Review it at Quill or Aricin.` (active message, online members with `Admit`) |
  | Login notice | `1 guild application is waiting. Review it at Quill or Aricin.` / `<N> guild applications are waiting. Review them at Quill or Aricin.` |
  | Join message | `<name> has joined the guild, accepted by <officer>.` to online members except the newcomer |
  | Letters | author `Aricin`, subject `Guild application`; accepted: `<guild> accepted your guild application. To join, click the guild board outside the guild hall on Abel Port Way, or log in again to choose. The offer lasts 14 days.`; declined: `<guild> declined your guild application.` |
  | Online applicant on accept | `<guild> accepted your application. Open the guild board on Abel Port Way to join.` |
  | Decide later | dialog key `close`, or `terminus_homeoptions` when the dialog source's name is `Terminus` |

**User decisions (already made):**
- Build guild applications (idea 43) next, from the 2026-09-25 guild batch.
- Players apply at a new board outside the guild house on Abel Port Way: spot C, (5,19), left of the door. The old board at (8,12) stays as decoration.
- The board needs art added to the map (the user's answer "No, add one").
- Players may apply to several guilds and be accepted by several; they choose which to join.
- The choice happens in a pop-up at login and at the board ("Pop-up and board").
- An optional note of up to 60 characters.
- Storage approach 1: one server-wide file keyed by applicant; guild files don't change.
- Sections 1–4 of the design, as presented: 5 open applications, 14-day expiry, officers with `Admit` review at Quill and Aricin, letters on accept and decline, a chat line to an online applicant but no mid-game pop-up, officer notices on apply and at login, in-person tavern admit unchanged, disband clears applications, the Terminus menu chained after "Decide later".

**Changes from the spec (decided while planning):**
1. The board has three clickable spots, not one: (5,19), (4,18) and (4,19). The client sends a click only for tiles with rendered wall art, and most of the board's image lies over the two hall-wall tiles. The spec is amended to match.
2. `GuildApplicationService` also has `OffersFor(applicant)`, `NowUtc`, and two static helpers: `ExpiresAt(application)` and `WaitingNotice(count)` (the login line, tested without `DefaultAislingScript`).
3. The officers' list shows `today` for an application under a day old.
4. After turning an offer down, the Next button goes to the remaining offers; with none left and the Terminus as source, it goes to the Terminus menu; otherwise the dialog closes.
5. `GuildApplicationReviewScript` reuses `GuildApplyScript.PAGE_SIZE`, `NEXT_PAGE` and `PREVIOUS_PAGE`.
6. Letters are wrapped in a `try`/`catch` that logs a warning, like the casino lottery's, so a missing mailbox never breaks an officer's answer.

## File map

| Repo | File | Responsibility |
|---|---|---|
| SRV | `Chaos/Models/World/GuildApplicationState.cs` (new) | saved state: applicant → applications |
| SRV | `Chaos/Services/GuildApplications/GuildApplicationService.cs` (new) | every rule, the lock, saving, listing guild folders |
| SRV | `Chaos/SerializationContext.cs` | JSON source generation for the state |
| SRV | `Chaos/Extensions/ServiceCollectionExtensions.cs` | register the service |
| SRV | `Chaos/Collections/Guild.cs` | `AddMember(aisling, acceptedByName)` and the shared `UnsafeJoin` |
| SRV | `Chaos/Scripting/ReactorTileScripts/Temauir/GuildApplicationBoardScript.cs` (new) | the board click opens the menu |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplyScript.cs` (new) | the applicant's 12 dialogs |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplicationReviewScript.cs` (new) | the officers' 4 dialogs |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs` | the Applications option |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs` | disband cleanup |
| SRV | `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` | login: clear, offers pop-up, waiting notice |
| SRV | `Tests/Chaos.Tests/GuildApplications/*.cs` (new) | the new tests and their support class |
| SRV | `Tests/Chaos.Tests/GuildPermissions/GuildMemberPermissionTests.cs`, `Tests/Chaos.Tests/GuildCloak/GuildCloakScriptTests.cs` | constructor and menu updates |
| UNO | `Data/Configuration/MapData/lod180.map` | the board art |
| UNO | `Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_port_way/reactors.json` | three clickable spots |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications/*.json` (new, 16 files) | the dialogs |
| UNO | `docs/guild-hall-ideas.md` | mark idea 43 built |

---

### Task 0: Create the two worktrees

**Goal:** Isolated `feat/guild-applications` branches for the server and Unora, so no shared checkout is touched.

**Files:**
- Create: worktrees `SRV` and `UNO` (see Global Constraints)

**Acceptance Criteria:**
- [ ] `git -C <each worktree> branch --show-current` prints `feat/guild-applications`
- [ ] `SRV/Tests/Chaos.Tests` builds, and `GuildTests`, the `GuildPermissions` tests and the `GuildCloak` tests pass before any change

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server branch --show-current` → `feat/guild-applications`

**Steps:**

- [ ] **Step 1: Check the worktrees don't exist yet**

```bash
ls /c/Users/Michael/Documents/GitHub/worktrees/
git -C /c/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server branch --list "feat/guild-applications"
git -C /c/Users/Michael/Documents/GitHub/Unora branch --list "feat/guild-applications"
```

Expected: no `guild-applications-server` or `guild-applications-unora` folder, and no branch printed. If either exists, stop and report. A previous run may have started them.

- [ ] **Step 2: Create the worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/guild-applications-server -b feat/guild-applications master
git -C Unora -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora -b feat/guild-applications main
```

Pass `core.longpaths` with `-c` only. Never write it to the repo config.

- [ ] **Step 3: Baseline build and tests**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/*/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildCloak/*/*" --no-ansi
```

Expected: `Build succeeded`, and every test in the three runs passes.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server branch --show-current", "acceptanceCriteria": ["both worktrees on feat/guild-applications", "SRV Chaos.Tests builds; GuildTests, GuildPermissions and GuildCloak tests pass before any change"], "modelTier": "mechanical"}
```

---

### Task 1: The application state and service

**Goal:** `GuildApplicationService` stores applications in `GuildApplicationState.json` and enforces the limits, expiry, accept, decline, take-offer and cleanup rules, with tests.

**Files:**
- Create: `SRV/Chaos/Models/World/GuildApplicationState.cs`
- Create: `SRV/Chaos/Services/GuildApplications/GuildApplicationService.cs`
- Modify: `SRV/Chaos/SerializationContext.cs` (two attributes after the `GuildCloakState` pair)
- Modify: `SRV/Chaos/Extensions/ServiceCollectionExtensions.cs` (one using, one registration after `GuildCloakService`)
- Test: `SRV/Tests/Chaos.Tests/GuildApplications/GuildApplicationTestSupport.cs` (new)
- Test: `SRV/Tests/Chaos.Tests/GuildApplications/GuildApplicationServiceTests.cs` (new)
- Test: `SRV/Tests/Chaos.Tests/GuildApplications/GuildApplicationStateTests.cs` (new)

**Acceptance Criteria:**
- [ ] `Apply` trims the note and refuses: the same guild again (ignoring case), a sixth open application (offers count), a note over 60 characters
- [ ] A waiting application is gone after 14 days and 1 minute from sending; an offer after 14 days and 1 minute from acceptance; expired entries are removed from the state
- [ ] `Accept` makes an offer once; a second accept reports `AlreadyAccepted` and keeps the first officer; a missing one reports `NotFound`
- [ ] `Decline` removes waiting applications only; `WaitingFor` lists waiting applicants oldest first and leaves offers out
- [ ] `TryTakeOffer` removes all of the applicant's applications and returns the officer; it fails for a waiting or expired one
- [ ] `RemoveGuild` and `ClearApplicant` remove only what they should
- [ ] `WaitingNotice(0)` is null; 1 and 3 give the exact lines
- [ ] `ListGuilds` lists folders with a `guild.json`, sorted ignoring case, and is empty for a missing folder
- [ ] The state round-trips through `SerializationContext` and matches names ignoring case after `EnsureCaseInsensitive`
- [ ] The server builds with the service registered

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildApplications/*/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the test support class**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/GuildApplications/GuildApplicationTestSupport.cs`:

```csharp
#region
using Chaos.Models.World;
using Chaos.Services.GuildApplications;
using Chaos.Services.Storage.Options;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
#endregion

namespace Chaos.Tests.GuildApplications;

/// <summary>
///     Builds a <see cref="GuildApplicationService" /> over an in-memory state, a clock the test moves, and an optional guild
///     folder. Also used by the guild permission and guild cloak tests, whose scripts now take the service.
/// </summary>
internal static class GuildApplicationTestSupport
{
    public static readonly DateTimeOffset Start = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    public static GuildApplicationService CreateService() => CreateService(out _, out _, out _);

    public static GuildApplicationService CreateService(
        out GuildApplicationState state,
        out FixedTime time,
        out Mock<IStorage<GuildApplicationState>> storage,
        string? guildDirectory = null)
    {
        state = new GuildApplicationState();
        time = new FixedTime(Start);
        storage = new Mock<IStorage<GuildApplicationState>>();

        storage.SetupGet(s => s.Value)
               .Returns(state);

        var options = new GuildStoreOptions
        {
            Directory = guildDirectory ?? Path.Combine(Path.GetTempPath(), "guild-applications-no-guilds")
        };

        return new GuildApplicationService(
            storage.Object,
            MockOptions.Create(options).Object,
            time,
            NullLogger<GuildApplicationService>.Instance);
    }

    internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;

        public void Later(TimeSpan by) => Now += by;
    }

    /// <summary>A temporary guild folder with one subfolder holding a guild.json per name. Deleted on dispose.</summary>
    internal sealed class TempGuildFolder : IDisposable
    {
        public TempGuildFolder(params string[] guildNames)
        {
            Directory.CreateDirectory(Root);

            foreach (var name in guildNames)
            {
                var directory = Path.Combine(Root, name);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "guild.json"), "{}");
            }
        }

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "guild-applications-" + Guid.NewGuid().ToString("N"));

        public void Dispose() => Directory.Delete(Root, true);
    }
}
```

- [ ] **Step 2: Write the failing service tests**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/GuildApplications/GuildApplicationServiceTests.cs`:

```csharp
#region
using Chaos.Services.GuildApplications;
using FluentAssertions;
using Moq;
using static Chaos.Tests.GuildApplications.GuildApplicationTestSupport;
#endregion

namespace Chaos.Tests.GuildApplications;

public sealed class GuildApplicationServiceTests
{
    private const string APPLICANT = "Wanderer";

    [Test]
    public void Apply_adds_a_waiting_application_with_the_trimmed_note_and_saves()
    {
        var service = CreateService(out _, out _, out var storage);

        service.Apply(APPLICANT, "Shinebox", "  Lv 60 priest  ")
               .Should()
               .Be(GuildApplyResult.Sent);

        var application = service.ForApplicant(APPLICANT)
                                 .Should()
                                 .ContainSingle()
                                 .Subject;

        application.GuildName.Should().Be("Shinebox");
        application.Note.Should().Be("Lv 60 priest");
        application.AppliedAt.Should().Be(Start.UtcDateTime);
        application.IsAccepted.Should().BeFalse();
        storage.Verify(s => s.Save(), Times.Once());
    }

    [Test]
    public void A_second_application_to_the_same_guild_is_refused_ignoring_case()
    {
        var service = CreateService();

        service.Apply(APPLICANT, "Shinebox", string.Empty);

        service.Apply(APPLICANT, "SHINEBOX", string.Empty)
               .Should()
               .Be(GuildApplyResult.AlreadyApplied);

        service.ForApplicant(APPLICANT).Should().ContainSingle();
    }

    [Test]
    public void A_sixth_open_application_is_refused_and_offers_count_toward_the_five()
    {
        var service = CreateService();

        for (var i = 1; i <= 5; i++)
            service.Apply(APPLICANT, $"Guild{i}", string.Empty)
                   .Should()
                   .Be(GuildApplyResult.Sent);

        service.Accept("Guild1", APPLICANT, "Stahli");

        service.Apply(APPLICANT, "Guild6", string.Empty)
               .Should()
               .Be(GuildApplyResult.TooMany);

        service.ForApplicant(APPLICANT).Should().HaveCount(5);
    }

    [Test]
    public void A_note_over_60_characters_is_refused()
    {
        var service = CreateService();

        service.Apply(APPLICANT, "Shinebox", new string('a', 61))
               .Should()
               .Be(GuildApplyResult.NoteTooLong);

        service.ForApplicant(APPLICANT).Should().BeEmpty();

        service.Apply(APPLICANT, "Shinebox", new string('a', 60))
               .Should()
               .Be(GuildApplyResult.Sent);
    }

    [Test]
    public void Withdraw_removes_the_application()
    {
        var service = CreateService(out var state, out _, out _);

        service.Apply(APPLICANT, "Shinebox", string.Empty);

        service.Withdraw(APPLICANT, "shinebox").Should().BeTrue();
        service.ForApplicant(APPLICANT).Should().BeEmpty();
        state.Applicants.Should().BeEmpty();
        service.Withdraw(APPLICANT, "Shinebox").Should().BeFalse();
    }

    [Test]
    public void A_waiting_application_expires_14_days_after_it_was_sent()
    {
        var service = CreateService(out var state, out var time, out _);

        service.Apply(APPLICANT, "Shinebox", string.Empty);

        time.Later(TimeSpan.FromDays(13));
        service.ForApplicant(APPLICANT).Should().ContainSingle();

        time.Later(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));
        service.ForApplicant(APPLICANT).Should().BeEmpty();
        state.Applicants.Should().BeEmpty();
    }

    [Test]
    public void An_offer_expires_14_days_after_it_was_accepted()
    {
        var service = CreateService(out _, out var time, out _);

        service.Apply(APPLICANT, "Shinebox", string.Empty);
        time.Later(TimeSpan.FromDays(10));
        service.Accept("Shinebox", APPLICANT, "Stahli");

        time.Later(TimeSpan.FromDays(10));
        service.OffersFor(APPLICANT).Should().ContainSingle();

        time.Later(TimeSpan.FromDays(4) + TimeSpan.FromMinutes(1));
        service.OffersFor(APPLICANT).Should().BeEmpty();
    }

    [Test]
    public void Accept_turns_a_waiting_application_into_an_offer()
    {
        var service = CreateService();

        service.Apply(APPLICANT, "Shinebox", string.Empty);

        service.Accept("Shinebox", APPLICANT, "Stahli")
               .Should()
               .Be(GuildAcceptResult.Accepted);

        var offer = service.OffersFor(APPLICANT)
                           .Should()
                           .ContainSingle()
                           .Subject;

        offer.AcceptedBy.Should().Be("Stahli");
        offer.AcceptedAt.Should().Be(Start.UtcDateTime);
        service.WaitingFor("Shinebox").Should().BeEmpty();
        service.CountWaiting("Shinebox").Should().Be(0);
    }

    [Test]
    public void A_second_accept_changes_nothing()
    {
        var service = CreateService(out _, out var time, out _);

        service.Apply(APPLICANT, "Shinebox", string.Empty);
        service.Accept("Shinebox", APPLICANT, "Stahli");
        time.Later(TimeSpan.FromHours(1));

        service.Accept("Shinebox", APPLICANT, "Iglis")
               .Should()
               .Be(GuildAcceptResult.AlreadyAccepted);

        var offer = service.OffersFor(APPLICANT).Single();
        offer.AcceptedBy.Should().Be("Stahli");
        offer.AcceptedAt.Should().Be(Start.UtcDateTime);
    }

    [Test]
    public void Accept_reports_a_missing_application()
        => CreateService()
           .Accept("Shinebox", APPLICANT, "Stahli")
           .Should()
           .Be(GuildAcceptResult.NotFound);

    [Test]
    public void Decline_removes_only_a_waiting_application()
    {
        var service = CreateService();

        service.Apply(APPLICANT, "Offered", string.Empty);
        service.Apply(APPLICANT, "Waiting", string.Empty);
        service.Accept("Offered", APPLICANT, "Stahli");

        service.Decline("Offered", APPLICANT).Should().BeFalse();
        service.Decline("Waiting", APPLICANT).Should().BeTrue();

        service.ForApplicant(APPLICANT)
               .Select(application => application.GuildName)
               .Should()
               .Equal("Offered");
    }

    [Test]
    public void WaitingFor_lists_the_guilds_waiting_applicants_oldest_first()
    {
        var service = CreateService(out _, out var time, out _);

        service.Apply("Early", "Shinebox", string.Empty);
        service.Apply("Elsewhere", "OtherGuild", string.Empty);
        service.Apply("Offered", "Shinebox", string.Empty);
        service.Accept("Shinebox", "Offered", "Stahli");
        time.Later(TimeSpan.FromHours(1));
        service.Apply("Late", "Shinebox", "hello");

        var waiting = service.WaitingFor("shinebox");

        waiting.Select(entry => entry.Applicant).Should().Equal("Early", "Late");
        waiting[1].Application.Note.Should().Be("hello");
        service.CountWaiting("Shinebox").Should().Be(2);
    }

    [Test]
    public void TryTakeOffer_removes_every_application_and_returns_the_officer()
    {
        var service = CreateService(out var state, out _, out _);

        service.Apply(APPLICANT, "Shinebox", string.Empty);
        service.Apply(APPLICANT, "OtherGuild", string.Empty);
        service.Accept("Shinebox", APPLICANT, "Stahli");

        service.TryTakeOffer(APPLICANT, "Shinebox", out var acceptedBy).Should().BeTrue();

        acceptedBy.Should().Be("Stahli");
        service.ForApplicant(APPLICANT).Should().BeEmpty();
        state.Applicants.Should().BeEmpty();
    }

    [Test]
    public void TryTakeOffer_fails_without_a_live_offer()
    {
        var service = CreateService(out _, out var time, out _);

        service.Apply(APPLICANT, "Shinebox", string.Empty);
        service.TryTakeOffer(APPLICANT, "Shinebox", out _).Should().BeFalse();
        service.ForApplicant(APPLICANT).Should().ContainSingle();

        service.Accept("Shinebox", APPLICANT, "Stahli");
        time.Later(TimeSpan.FromDays(15));
        service.TryTakeOffer(APPLICANT, "Shinebox", out _).Should().BeFalse();
    }

    [Test]
    public void RemoveGuild_removes_that_guilds_applications_and_offers_only()
    {
        var service = CreateService(out var state, out _, out _);

        service.Apply("One", "Doomed", string.Empty);
        service.Apply("One", "Kept", string.Empty);
        service.Apply("Two", "Doomed", string.Empty);
        service.Accept("Doomed", "Two", "Stahli");

        service.RemoveGuild("doomed");

        service.ForApplicant("One")
               .Select(application => application.GuildName)
               .Should()
               .Equal("Kept");

        service.ForApplicant("Two").Should().BeEmpty();
        state.Applicants.Should().NotContainKey("Two");
    }

    [Test]
    public void ClearApplicant_removes_all_of_one_applicants_applications()
    {
        var service = CreateService();

        service.Apply(APPLICANT, "Shinebox", string.Empty);
        service.Apply(APPLICANT, "OtherGuild", string.Empty);
        service.Apply("Someone", "Shinebox", string.Empty);

        service.ClearApplicant(APPLICANT);

        service.ForApplicant(APPLICANT).Should().BeEmpty();
        service.ForApplicant("Someone").Should().ContainSingle();
    }

    [Test]
    public void WaitingNotice_is_null_for_none_and_counts_in_words()
    {
        GuildApplicationService.WaitingNotice(0).Should().BeNull();

        GuildApplicationService.WaitingNotice(1)
                               .Should()
                               .Be("1 guild application is waiting. Review it at Quill or Aricin.");

        GuildApplicationService.WaitingNotice(3)
                               .Should()
                               .Be("3 guild applications are waiting. Review them at Quill or Aricin.");
    }

    [Test]
    public void ListGuilds_lists_folders_holding_a_guild_file_sorted_ignoring_case()
    {
        using var folder = new TempGuildFolder("beta", "Alpha");
        Directory.CreateDirectory(Path.Combine(folder.Root, "empty"));

        CreateService(out _, out _, out _, folder.Root)
            .ListGuilds()
            .Should()
            .Equal("Alpha", "beta");
    }

    [Test]
    public void ListGuilds_is_empty_when_the_folder_is_missing()
        => CreateService(out _, out _, out _, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
           .ListGuilds()
           .Should()
           .BeEmpty();
}
```

- [ ] **Step 3: Write the failing state tests**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/GuildApplications/GuildApplicationStateTests.cs`:

```csharp
#region
using System.Text.Json;
using Chaos.Models.World;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildApplications;

public sealed class GuildApplicationStateTests
{
    private static readonly DateTime ACCEPTED = new(2026, 9, 21, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime APPLIED = new(2026, 9, 20, 18, 0, 0, DateTimeKind.Utc);

    [Test]
    public void The_state_round_trips_and_names_match_ignoring_case_after_load()
    {
        var state = new GuildApplicationState();

        state.Applicants["Wanderer"] =
        [
            new GuildApplication
            {
                GuildName = "Shinebox",
                Note = "Lv 60 priest",
                AppliedAt = APPLIED,
                AcceptedBy = "Stahli",
                AcceptedAt = ACCEPTED
            },
            new GuildApplication
            {
                GuildName = "OtherGuild",
                AppliedAt = APPLIED
            }
        ];

        var json = JsonSerializer.Serialize(state, SerializationContext.Default.GuildApplicationState);
        var restored = JsonSerializer.Deserialize(json, SerializationContext.Default.GuildApplicationState)!;
        restored.EnsureCaseInsensitive();

        var applications = restored.Applicants["WANDERER"];

        applications.Should().HaveCount(2);
        applications[0].GuildName.Should().Be("Shinebox");
        applications[0].Note.Should().Be("Lv 60 priest");
        applications[0].AppliedAt.Should().Be(APPLIED);
        applications[0].AcceptedBy.Should().Be("Stahli");
        applications[0].AcceptedAt.Should().Be(ACCEPTED);
        applications[0].IsAccepted.Should().BeTrue();
        applications[1].IsAccepted.Should().BeFalse();
        applications[1].Note.Should().BeEmpty();
    }

    [Test]
    public void A_copy_is_a_separate_object_with_the_same_values()
    {
        var original = new GuildApplication
        {
            GuildName = "Shinebox",
            Note = "hi",
            AppliedAt = APPLIED
        };

        var copy = original.Copy();

        copy.Should().NotBeSameAs(original);
        copy.Should().BeEquivalentTo(original);
    }
}
```

- [ ] **Step 4: Run the tests to see them fail**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj
```

Expected: build errors naming `GuildApplicationService`, `GuildApplicationState`, `GuildApplyResult` and `GuildAcceptResult`.

- [ ] **Step 5: Write the state class**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Chaos/Models/World/GuildApplicationState.cs`:

```csharp
#region
using System.Text.Json.Serialization;
#endregion

namespace Chaos.Models.World;

/// <summary>
///     Every open guild application, keyed by the applicant's name. Saved as <c>GuildApplicationState.json</c> through
///     <c>IStorage&lt;GuildApplicationState&gt;</c>. Not thread safe: <c>GuildApplicationService</c> locks around every call
///     and saves afterwards.
/// </summary>
public sealed class GuildApplicationState
{
    public Dictionary<string, List<GuildApplication>> Applicants { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     System.Text.Json replaces <see cref="Applicants" /> on load with a case-sensitive dictionary. Call once after loading.
    /// </summary>
    public void EnsureCaseInsensitive()
        => Applicants = new Dictionary<string, List<GuildApplication>>(
            Applicants ?? new Dictionary<string, List<GuildApplication>>(),
            StringComparer.OrdinalIgnoreCase);
}

/// <summary>
///     One player's application to one guild. It becomes an offer once <see cref="AcceptedBy" /> is set.
/// </summary>
public sealed class GuildApplication
{
    public DateTime? AcceptedAt { get; set; }
    public string? AcceptedBy { get; set; }
    public DateTime AppliedAt { get; set; }
    public string GuildName { get; set; } = string.Empty;

    /// <summary>The applicant's note as typed (trimmed). It is filtered when shown, so a chat filter change applies to it.</summary>
    public string Note { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsAccepted => AcceptedBy is not null;

    public GuildApplication Copy()
        => new()
        {
            AcceptedAt = AcceptedAt,
            AcceptedBy = AcceptedBy,
            AppliedAt = AppliedAt,
            GuildName = GuildName,
            Note = Note
        };
}
```

- [ ] **Step 6: Write the service**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Chaos/Services/GuildApplications/GuildApplicationService.cs`:

```csharp
#region
using Chaos.Extensions.Common;
using Chaos.Models.World;
using Chaos.Services.Storage.Options;
using Chaos.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#endregion

namespace Chaos.Services.GuildApplications;

public enum GuildApplyResult
{
    Sent,
    AlreadyApplied,
    TooMany,
    NoteTooLong
}

public enum GuildAcceptResult
{
    Accepted,
    AlreadyAccepted,
    NotFound
}

/// <summary>A waiting application and who sent it. The application is a copy; changing it changes nothing.</summary>
public sealed record GuildApplicationEntry(string Applicant, GuildApplication Application);

/// <summary>
///     Guild application rules: applying from the board on Abel Port Way, officers' answers, offers and joining. It holds the
///     only lock around <see cref="GuildApplicationState" /> and saves it after every change. Expired entries are dropped at
///     the start of every call, so there is no timer. Returned applications are copies.
/// </summary>
public sealed class GuildApplicationService
{
    public const int MAX_OPEN_APPLICATIONS = 5;
    public const int NOTE_MAX_LENGTH = 60;

    /// <summary>How long a waiting application lasts from sending, and an offer from acceptance.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    private readonly string GuildDirectory;
    private readonly ILogger<GuildApplicationService> Logger;
    private readonly IStorage<GuildApplicationState> Storage;
    private readonly Lock Sync = new();
    private readonly TimeProvider Time;

    public GuildApplicationService(
        IStorage<GuildApplicationState> storage,
        IOptions<GuildStoreOptions> guildStoreOptions,
        TimeProvider time,
        ILogger<GuildApplicationService> logger)
    {
        Storage = storage;
        GuildDirectory = guildStoreOptions.Value.Directory;
        Time = time;
        Logger = logger;

        using (Sync.EnterScope())
            State.EnsureCaseInsensitive();
    }

    public DateTime NowUtc => Time.GetUtcNow().UtcDateTime;
    private GuildApplicationState State => Storage.Value;

    /// <summary>Turns a waiting application into an offer. The officer's name goes into the join message later.</summary>
    public GuildAcceptResult Accept(string guildName, string applicant, string acceptedBy)
    {
        GuildAcceptResult result;

        using (Sync.EnterScope())
        {
            var dirty = PruneExpired();
            var application = Find(applicant, guildName);

            if (application is null)
                result = GuildAcceptResult.NotFound;
            else if (application.IsAccepted)
                result = GuildAcceptResult.AlreadyAccepted;
            else
            {
                application.AcceptedBy = acceptedBy;
                application.AcceptedAt = NowUtc;
                dirty = true;
                result = GuildAcceptResult.Accepted;
            }

            if (dirty)
                Storage.Save();
        }

        if (result == GuildAcceptResult.Accepted)
            Logger.LogInformation("{Officer} accepted {Applicant}'s application to guild {Guild}", acceptedBy, applicant, guildName);

        return result;
    }

    /// <summary>Adds a waiting application. The caller checks that the guild exists.</summary>
    public GuildApplyResult Apply(string applicant, string guildName, string note)
    {
        note = note.Trim();

        if (note.Length > NOTE_MAX_LENGTH)
            return GuildApplyResult.NoteTooLong;

        GuildApplyResult result;

        using (Sync.EnterScope())
        {
            var dirty = PruneExpired();
            State.Applicants.TryGetValue(applicant, out var applications);
            applications ??= [];

            if (applications.Any(application => application.GuildName.EqualsI(guildName)))
                result = GuildApplyResult.AlreadyApplied;
            else if (applications.Count >= MAX_OPEN_APPLICATIONS)
                result = GuildApplyResult.TooMany;
            else
            {
                applications.Add(
                    new GuildApplication
                    {
                        GuildName = guildName,
                        Note = note,
                        AppliedAt = NowUtc
                    });

                State.Applicants[applicant] = applications;
                dirty = true;
                result = GuildApplyResult.Sent;
            }

            if (dirty)
                Storage.Save();
        }

        if (result == GuildApplyResult.Sent)
            Logger.LogInformation("{Applicant} applied to guild {Guild}", applicant, guildName);

        return result;
    }

    /// <summary>Removes every application of a player who turned out to be in a guild.</summary>
    public void ClearApplicant(string applicant)
    {
        using (Sync.EnterScope())
        {
            var dirty = PruneExpired();

            if (State.Applicants.Remove(applicant))
                dirty = true;

            if (dirty)
                Storage.Save();
        }
    }

    public int CountWaiting(string guildName) => WaitingFor(guildName).Count;

    /// <summary>Removes a waiting application. An offer can't be declined; the applicant turns it down instead.</summary>
    public bool Decline(string guildName, string applicant)
    {
        var declined = false;

        using (Sync.EnterScope())
        {
            var dirty = PruneExpired();
            var application = Find(applicant, guildName);

            if (application is { IsAccepted: false })
            {
                Remove(applicant, application);
                dirty = declined = true;
            }

            if (dirty)
                Storage.Save();
        }

        if (declined)
            Logger.LogInformation("{Applicant}'s application to guild {Guild} was declined", applicant, guildName);

        return declined;
    }

    /// <summary>When an application stops counting: 14 days after it was sent, or 14 days after it was accepted.</summary>
    public static DateTime ExpiresAt(GuildApplication application) => (application.AcceptedAt ?? application.AppliedAt) + Lifetime;

    /// <summary>Copies of the applicant's live applications, sorted by guild name.</summary>
    public IReadOnlyList<GuildApplication> ForApplicant(string applicant)
    {
        using (Sync.EnterScope())
        {
            if (PruneExpired())
                Storage.Save();

            return State.Applicants.TryGetValue(applicant, out var applications)
                ? applications.OrderBy(application => application.GuildName, StringComparer.OrdinalIgnoreCase)
                              .Select(application => application.Copy())
                              .ToList()
                : [];
        }
    }

    /// <summary>The names of the folders under the guild directory that hold a guild.json, sorted ignoring case.</summary>
    public IReadOnlyList<string> ListGuilds()
    {
        if (!Directory.Exists(GuildDirectory))
            return [];

        return Directory.EnumerateDirectories(GuildDirectory)
                        .Where(directory => File.Exists(Path.Combine(directory, "guild.json")))
                        .Select(Path.GetFileName)
                        .OfType<string>()
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToList();
    }

    public IReadOnlyList<GuildApplication> OffersFor(string applicant)
        => ForApplicant(applicant)
           .Where(application => application.IsAccepted)
           .ToList();

    /// <summary>Removes every application to a disbanded guild, waiting or accepted.</summary>
    public void RemoveGuild(string guildName)
    {
        using (Sync.EnterScope())
        {
            var dirty = PruneExpired();

            foreach ((var applicant, var applications) in State.Applicants.ToList())
                if (applications.RemoveAll(application => application.GuildName.EqualsI(guildName)) > 0)
                {
                    dirty = true;

                    if (applications.Count == 0)
                        State.Applicants.Remove(applicant);
                }

            if (dirty)
                Storage.Save();
        }

        Logger.LogInformation("Removed every application to disbanded guild {Guild}", guildName);
    }

    /// <summary>
    ///     If a live offer from that guild exists, removes all of the applicant's applications and returns who accepted. The
    ///     caller then adds the member.
    /// </summary>
    public bool TryTakeOffer(string applicant, string guildName, [NotNullWhen(true)] out string? acceptedBy)
    {
        acceptedBy = null;

        using (Sync.EnterScope())
        {
            var dirty = PruneExpired();
            var application = Find(applicant, guildName);

            if (application is { AcceptedBy: { } officer })
            {
                acceptedBy = officer;
                State.Applicants.Remove(applicant);
                dirty = true;
            }

            if (dirty)
                Storage.Save();
        }

        if (acceptedBy is null)
            return false;

        Logger.LogInformation("{Applicant} took guild {Guild}'s offer, accepted by {Officer}", applicant, guildName, acceptedBy);

        return true;
    }

    /// <summary>A guild's waiting applications, oldest first. Offers are left out.</summary>
    public IReadOnlyList<GuildApplicationEntry> WaitingFor(string guildName)
    {
        using (Sync.EnterScope())
        {
            if (PruneExpired())
                Storage.Save();

            return State.Applicants
                        .SelectMany(pair => pair.Value
                                                .Where(application => !application.IsAccepted && application.GuildName.EqualsI(guildName))
                                                .Select(application => new GuildApplicationEntry(pair.Key, application.Copy())))
                        .OrderBy(entry => entry.Application.AppliedAt)
                        .ThenBy(entry => entry.Applicant, StringComparer.OrdinalIgnoreCase)
                        .ToList();
        }
    }

    /// <summary>The login line for a member who can admit, or null when nothing is waiting.</summary>
    public static string? WaitingNotice(int count)
        => count switch
        {
            <= 0 => null,
            1    => "1 guild application is waiting. Review it at Quill or Aricin.",
            _    => $"{count} guild applications are waiting. Review them at Quill or Aricin."
        };

    /// <summary>Removes an application, waiting or accepted. Also used for "Turn down".</summary>
    public bool Withdraw(string applicant, string guildName)
    {
        var withdrawn = false;

        using (Sync.EnterScope())
        {
            var dirty = PruneExpired();
            var application = Find(applicant, guildName);

            if (application is not null)
            {
                Remove(applicant, application);
                dirty = withdrawn = true;
            }

            if (dirty)
                Storage.Save();
        }

        if (withdrawn)
            Logger.LogInformation("{Applicant} withdrew or turned down their application to guild {Guild}", applicant, guildName);

        return withdrawn;
    }

    private GuildApplication? Find(string applicant, string guildName)
        => State.Applicants.TryGetValue(applicant, out var applications)
            ? applications.FirstOrDefault(application => application.GuildName.EqualsI(guildName))
            : null;

    /// <summary>Drops every expired application. True when something was dropped.</summary>
    private bool PruneExpired()
    {
        var now = NowUtc;
        var dirty = false;

        foreach ((var applicant, var applications) in State.Applicants.ToList())
            if (applications.RemoveAll(application => ExpiresAt(application) < now) > 0)
            {
                dirty = true;

                if (applications.Count == 0)
                    State.Applicants.Remove(applicant);
            }

        return dirty;
    }

    private void Remove(string applicant, GuildApplication application)
    {
        if (!State.Applicants.TryGetValue(applicant, out var applications))
            return;

        applications.Remove(application);

        if (applications.Count == 0)
            State.Applicants.Remove(applicant);
    }
}
```

- [ ] **Step 7: Register the JSON types**

In `worktrees/guild-applications-server/Chaos/SerializationContext.cs`, use `replace_content` (literal) with needle:

```text
[JsonSerializable(typeof(Dictionary<string, GuildCloakState>))]
```

and replacement (three lines):

```text
[JsonSerializable(typeof(Dictionary<string, GuildCloakState>))]
[JsonSerializable(typeof(GuildApplicationState))]
[JsonSerializable(typeof(Dictionary<string, GuildApplicationState>))]
```

- [ ] **Step 8: Register the service**

In `worktrees/guild-applications-server/Chaos/Extensions/ServiceCollectionExtensions.cs`, two `replace_content` (literal) edits.

Needle:

```text
using Chaos.Services.GuildCloak;
```

Replacement (two lines):

```text
using Chaos.Services.GuildApplications;
using Chaos.Services.GuildCloak;
```

Needle:

```text
            services.AddSingleton<GuildCloakService>();
```

Replacement (four lines, keep the blank line):

```text
            services.AddSingleton<GuildCloakService>();

            //guild applications: the board on Abel Port Way, officers' answers and offers (IStorage<T> is an open generic)
            services.AddSingleton<GuildApplicationService>();
```

- [ ] **Step 9: Run the tests**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildApplications/*/*" --no-ansi
```

Expected: all 21 tests pass (19 service, 2 state).

```json:metadata
{"files": ["Chaos/Models/World/GuildApplicationState.cs", "Chaos/Services/GuildApplications/GuildApplicationService.cs", "Chaos/SerializationContext.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Tests/Chaos.Tests/GuildApplications/GuildApplicationTestSupport.cs", "Tests/Chaos.Tests/GuildApplications/GuildApplicationServiceTests.cs", "Tests/Chaos.Tests/GuildApplications/GuildApplicationStateTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildApplications/*/*\" --no-ansi", "acceptanceCriteria": ["Apply trims and refuses same guild (ignoring case), a sixth open (offers count), notes over 60", "waiting expires 14d+1m from sending, offers 14d+1m from acceptance; expired entries removed", "Accept once; second reports AlreadyAccepted keeping the first officer; missing reports NotFound", "Decline removes waiting only; WaitingFor oldest first without offers", "TryTakeOffer removes all and returns the officer; fails for waiting or expired", "RemoveGuild and ClearApplicant remove only what they should", "WaitingNotice null for 0, exact lines for 1 and 3", "ListGuilds lists folders with guild.json sorted ignoring case; empty for a missing folder", "state round-trips via SerializationContext and matches names ignoring case", "server builds with the service registered"], "modelTier": "standard"}
```

---

### Task 2: Joining by the accepting officer's name

**Goal:** `Guild.AddMember(Aisling, string acceptedByName)` adds a member at the lowest rank and tells online members who accepted them, for officers who may be offline.

**Files:**
- Modify: `SRV/Chaos/Collections/Guild.cs` (`AddMember(Aisling, Aisling)` body; new overload after it; new private `UnsafeJoin` after `UnsafeDetach`)
- Test: `SRV/Tests/Chaos.Tests/GuildApplications/GuildJoinByNameTests.cs` (new)

**Acceptance Criteria:**
- [ ] `AddMember(newcomer, "Stahli")` puts the newcomer in the lowest rank (`Applicant`) and sets `newcomer.Guild` and `GuildRank`
- [ ] Online members other than the newcomer get exactly `Wanderer has joined the guild, accepted by Stahli.`; the newcomer doesn't
- [ ] Joining the same guild twice changes nothing
- [ ] Every existing `GuildTests` test still passes

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildApplications/GuildJoinByNameTests/*" --no-ansi` → all pass, then the `GuildTests` run → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/GuildApplications/GuildJoinByNameTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Moq;
#endregion

namespace Chaos.Tests.GuildApplications;

public sealed class GuildJoinByNameTests
{
    private static Aisling CreateOnline(ClientRegistry<IChaosWorldClient> registry, string name)
    {
        var aisling = MockAisling.Create(name: name);

        Mock.Get(aisling.Client)
            .SetupGet(c => c.Id)
            .Returns(aisling.Id);

        registry.TryAdd(aisling.Client);

        return aisling;
    }

    [Test]
    public void Joining_by_name_adds_the_member_at_the_lowest_rank()
    {
        var guild = MockGuild.Create();
        var newcomer = MockAisling.Create(name: "Wanderer");

        guild.AddMember(newcomer, "Stahli");

        guild.HasMember("Wanderer").Should().BeTrue();
        newcomer.Guild.Should().BeSameAs(guild);
        newcomer.GuildRank.Should().Be("Applicant");
    }

    [Test]
    public void Online_members_hear_who_accepted_the_newcomer()
    {
        var registry = new ClientRegistry<IChaosWorldClient>();
        var guild = MockGuild.Create(clientRegistry: registry);
        var member = CreateOnline(registry, "Iglis");
        guild.AddMember(member, member);
        var newcomer = CreateOnline(registry, "Wanderer");

        guild.AddMember(newcomer, "Stahli");

        Mock.Get(member.Client)
            .Verify(
                c => c.SendServerMessage(ServerMessageType.ActiveMessage, "Wanderer has joined the guild, accepted by Stahli."),
                Times.Once());

        Mock.Get(newcomer.Client)
            .Verify(
                c => c.SendServerMessage(ServerMessageType.ActiveMessage, It.Is<string>(text => text.Contains("accepted by"))),
                Times.Never());
    }

    [Test]
    public void Joining_the_same_guild_twice_changes_nothing()
    {
        var guild = MockGuild.Create();
        var newcomer = MockAisling.Create(name: "Wanderer");

        guild.AddMember(newcomer, "Stahli");
        guild.AddMember(newcomer, "Stahli");

        guild.GetMemberNames().Should().ContainSingle();
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj
```

Expected: a build error — no `AddMember` overload takes a `string`.

- [ ] **Step 3: Split the join out of `AddMember`**

Read `Guild/AddMember` with `find_symbol` (`include_body=true`) in `worktrees/guild-applications-server/Chaos/Collections/Guild.cs`, then `replace_symbol_body` on `Guild/AddMember`:

```csharp
    public void AddMember(Aisling aisling, Aisling by)
    {
        using var @lock = Sync.EnterScope();

        if (!UnsafeJoin(aisling))
            return;

        foreach (var member in GetOnlineMembers()
                     .Where(member => !member.Equals(by)))
            member.SendActiveMessage($"{aisling.Name} has been admitted to the guild by {by.Name}!");
    }

    /// <summary>
    ///     Adds a member accepted through a guild application. The officer who accepted may be offline, so only their name
    ///     is needed. Online members, except the newcomer, are told who accepted them.
    /// </summary>
    public void AddMember(Aisling aisling, string acceptedByName)
    {
        ArgumentException.ThrowIfNullOrEmpty(acceptedByName);

        using var @lock = Sync.EnterScope();

        if (!UnsafeJoin(aisling))
            return;

        foreach (var member in GetOnlineMembers()
                     .Where(member => !member.Equals(aisling)))
            member.SendActiveMessage($"{aisling.Name} has joined the guild, accepted by {acceptedByName}.");
    }
```

The `///` doc comment above the first overload stays as it is.

- [ ] **Step 4: Add `UnsafeJoin`**

`insert_after_symbol` on `Guild/UnsafeDetach`:

```csharp

    /// <summary>
    ///     The join shared by both <see cref="AddMember(Aisling, Aisling)" /> overloads: lowest rank, last-seen time, guild
    ///     channel, profile and cloak. False when the player is already in this guild. Call inside the lock.
    /// </summary>
    private bool UnsafeJoin(Aisling aisling)
    {
        ArgumentNullException.ThrowIfNull(aisling);

        if (aisling.Guild is not null)
        {
            if (aisling.Guild == this)
                return false;

            throw new InvalidOperationException(
                $"Attempted to add \"{aisling.Name}\" to guild \"{Name}\", but that player is already in guild \"{aisling.Guild.Name}\"");
        }

        var lowestRank = GuildHierarchy.MaxBy(x => x.Tier)!;

        lowestRank.AddMember(aisling.Name);
        UnsafeRecordLastSeen(aisling.Name, DateTime.UtcNow);
        aisling.Guild = this;
        aisling.GuildRank = lowestRank.Name;
        JoinChannel(aisling);
        aisling.Client.SendSelfProfile();
        GuildCloakRefresh.Redisplay(aisling);

        return true;
    }
```

- [ ] **Step 5: Run the tests**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildApplications/GuildJoinByNameTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildTests/*" --no-ansi
```

Expected: the 3 new tests pass, and every `GuildTests` test passes.

```json:metadata
{"files": ["Chaos/Collections/Guild.cs", "Tests/Chaos.Tests/GuildApplications/GuildJoinByNameTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildApplications/GuildJoinByNameTests/*\" --no-ansi", "acceptanceCriteria": ["AddMember(newcomer, name) puts them in the lowest rank and sets Guild and GuildRank", "online members except the newcomer get 'Wanderer has joined the guild, accepted by Stahli.'", "joining the same guild twice changes nothing", "every existing GuildTests test passes"], "modelTier": "mechanical"}
```

---

### Task 3: The board on Abel Port Way

**Goal:** A wooden notice board stands at (5,19) on Abel Port Way, and clicking it (or the hall wall just behind it) opens `generic_guild_apply_initial`.

**Files:**
- Modify: `UNO/Data/Configuration/MapData/lod180.map` (one tile, by the Python below)
- Modify: `UNO/Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_port_way/reactors.json` (three entries after the `guildhallentrance` entry)
- Create: `SRV/Chaos/Scripting/ReactorTileScripts/Temauir/GuildApplicationBoardScript.cs`

**Acceptance Criteria:**
- [ ] Tile (5,19) of `lod180.map` reads background 12406, foregrounds 737 and 738; the file is still 2304 bytes and every other byte is unchanged
- [ ] `git ls-files --eol` still reports the map as `-text` (binary), and `git diff --stat` shows it as `Bin`
- [ ] `reactors.json` parses as JSON and has exactly three `GuildApplicationBoard` entries, at `(5, 19)`, `(4, 18)` and `(4, 19)`
- [ ] The server builds with `GuildApplicationBoardScript`

**Verify:** `python -c "import json;r=json.load(open(r'C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_port_way/reactors.json'));print(sorted(e['source'] for e in r if 'GuildApplicationBoard' in e['scriptKeys']))"` → `['(4, 18)', '(4, 19)', '(5, 19)']`

**Steps:**

- [ ] **Step 1: Write the board into the map**

```bash
python - <<'EOF'
import struct
path = r'C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/Data/Configuration/MapData/lod180.map'
data = bytearray(open(path, 'rb').read())
before = bytes(data)
assert len(data) == 16 * 24 * 6, len(data)
offset = (19 * 16 + 5) * 6
bg, left, right = struct.unpack_from('<HHH', data, offset)
assert (bg, left, right) == (12406, 0, 0), (bg, left, right)
struct.pack_into('<HHH', data, offset, bg, 737, 738)
open(path, 'wb').write(data)
changed = [i for i in range(len(data)) if data[i] != before[i]]
print(struct.unpack_from('<HHH', data, offset), len(data), changed)
EOF
```

Expected: `(12406, 737, 738) 2304 [1856, 1857, 1858, 1859]`. If the assert fails, stop and report: the map changed since planning.

- [ ] **Step 2: Check git still treats the map as binary**

```bash
git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora ls-files --eol -- Data/Configuration/MapData/lod180.map
git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora diff --stat -- Data/Configuration/MapData/lod180.map
```

Expected: `i/-text w/-text` in the first line, and `Bin 2304 -> 2304 bytes` in the second.

- [ ] **Step 3: Add the three clickable spots**

```bash
python - <<'EOF'
path = r'C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_port_way/reactors.json'
text = open(path, encoding='utf-8', newline='').read()
nl = '\r\n' if '\r\n' in text else '\n'
entry = '''  {
    "scriptKeys": [
      "GuildApplicationBoard"
    ],
    "scriptVars": {},
    "shouldBlockPathfinding": true,
    "source": "(%d, %d)"
  },'''
block = ''.join((entry % point).replace('\n', nl) + nl for point in [(5, 19), (4, 18), (4, 19)])
marker = '"source": "(5, 17)"' + nl + '  },' + nl
assert text.count(marker) == 1, text.count(marker)
text = text.replace(marker, marker + block)
open(path, 'w', encoding='utf-8', newline='').write(text)
EOF
python -c "import json;r=json.load(open(r'C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_port_way/reactors.json'));print(sorted(e['source'] for e in r if 'GuildApplicationBoard' in e['scriptKeys']))"
```

Expected: `['(4, 18)', '(4, 19)', '(5, 19)']`.

- [ ] **Step 4: Write the reactor script**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Chaos/Scripting/ReactorTileScripts/Temauir/GuildApplicationBoardScript.cs`:

```csharp
#region
using Chaos.Models.World;
using Chaos.Scripting.ReactorTileScripts.Abstractions;
using Chaos.Services.Factories.Abstractions;
#endregion

namespace Chaos.Scripting.ReactorTileScripts.Temauir;

/// <summary>
///     The guild applications board outside the guild hall on Abel Port Way. The client turns a click into the ground tile
///     under the cursor, and the board's art lies mostly over two hall-wall tiles behind it, so the board's own tile and those
///     two all carry this script. The server has already checked the player is in reach.
/// </summary>
public sealed class GuildApplicationBoardScript(
    ReactorTile subject,
    IDialogFactory dialogFactory,
    IMerchantFactory merchantFactory) : ReactorTileScriptBase(subject)
{
    public override void OnClicked(Aisling source)
    {
        var merchant = merchantFactory.Create("blank_merchant", Subject.MapInstance, Point);
        var dialog = dialogFactory.Create("generic_guild_apply_initial", merchant);
        dialog.Display(source);
    }
}
```

- [ ] **Step 5: Build the server**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Chaos/Chaos.csproj
```

Expected: `Build succeeded`.

```json:metadata
{"files": ["UNO:Data/Configuration/MapData/lod180.map", "UNO:Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_port_way/reactors.json", "Chaos/Scripting/ReactorTileScripts/Temauir/GuildApplicationBoardScript.cs"], "verifyCommand": "python -c \"import json;r=json.load(open(r'C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_port_way/reactors.json'));print(sorted(e['source'] for e in r if 'GuildApplicationBoard' in e['scriptKeys']))\"", "acceptanceCriteria": ["tile (5,19) reads 12406/737/738; file 2304 bytes; only bytes 1856-1859 changed", "git reports the map -text and diff shows Bin", "reactors.json parses and has GuildApplicationBoard at (5, 19), (4, 18), (4, 19)", "server builds with GuildApplicationBoardScript"], "modelTier": "mechanical"}
```

---

### Task 4: The applicant's menus

**Goal:** `GuildApplyScript` runs the 12 applicant dialogs: apply with a note, list and withdraw applications, and join or turn down offers, with the Terminus chain for "Decide later".

**Files:**
- Create: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplyScript.cs`
- Create: 12 files in `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications/` (listed in Step 5)
- Test: `SRV/Tests/Chaos.Tests/GuildApplications/GuildApplyScriptTests.cs` (new)

**Acceptance Criteria:**
- [ ] A player in a guild gets `You're already in a guild. Leave it before applying to another.` and their applications are cleared
- [ ] The first screen shows `Join a guild (1 offer)` (or `(N offers)`) only when offers exist, then `Apply to a guild`, `My applications`
- [ ] The guild list leaves out guilds already applied to, pages 10 at a time with `Next page` / `Previous page`, and picking carries the guild in `ApplyContext.Guild`
- [ ] At 5 open applications the list replies `You already have 5 open applications. Withdraw one before applying to another guild.`
- [ ] The confirmation reads `Apply to Alpha?` plus `\n\nYour note: <trimmed note>` when there is one
- [ ] Sending applies, shows `Your application to Alpha has been sent.`, and sends the officer notice to online members with `Admit` only; a missing guild replies `That guild no longer exists.`
- [ ] My applications lists `Alpha - waiting, 9 days left` / `Beta - accepted!` pointing at `_waiting` / `_offer`
- [ ] Withdrawing removes the application
- [ ] Joining takes the offer, adds the member, saves the guild; a missing guild withdraws the offer; no offer replies `That offer has expired.`
- [ ] `Decide later` points at `close`, or `terminus_homeoptions` from the Terminus; turning down points Next at the remaining offers, or the Terminus menu
- [ ] The 12 dialog files parse as JSON and use script key `GuildApply`

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildApplications/GuildApplyScriptTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/GuildApplications/GuildApplyScriptTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Collections.Common;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts;
using Chaos.Services.GuildApplications;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static Chaos.Tests.GuildApplications.GuildApplicationTestSupport;
#endregion

namespace Chaos.Tests.GuildApplications;

public sealed class GuildApplyScriptTests
{
    private const string APPLICANT = "Wanderer";

    private static GuildApplyScript CreateScript(Dialog dialog, GuildApplicationService service, IStore<Guild>? store = null)
        => new(
            dialog,
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            store ?? new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildApplyScript>>().Object,
            service);

    private static Dialog Screen(
        string templateKey,
        string text = "{Text}",
        string? guild = null,
        string? typed = null)
        => MockDialog.Create(
            templateKey,
            dialog =>
            {
                dialog.Text = text;

                if (guild is not null)
                    dialog.Context = new GuildApplyScript.ApplyContext(0, [], guild);

                if (typed is not null)
                    dialog.MenuArgs = new ArgumentCollection(new[] { typed });
            });

    private static Dialog Show(
        string templateKey,
        Aisling source,
        GuildApplicationService service,
        IStore<Guild>? store = null,
        string text = "{Text}",
        string? guild = null,
        string? typed = null)
    {
        var dialog = Screen(templateKey, text, guild, typed);

        CreateScript(dialog, service, store)
            .OnDisplaying(source);

        return dialog;
    }

    private static IEnumerable<string> OptionsOf(Dialog dialog) => dialog.Options.Select(option => option.OptionText);

    private static string RepliedText(Aisling aisling) => aisling.ActiveDialog.Get()!.Text;

    private static Aisling CreateOnline(ClientRegistry<IChaosWorldClient> registry, string name)
    {
        var aisling = MockAisling.Create(name: name);

        Mock.Get(aisling.Client)
            .SetupGet(c => c.Id)
            .Returns(aisling.Id);

        registry.TryAdd(aisling.Client);

        return aisling;
    }

    /// <summary>The guild "Alpha" with an online leader (who can admit) and an online plain member (who can't), in a store that knows it.</summary>
    private static (Guild Guild, Aisling Leader, Aisling Member, Mock<IStore<Guild>> Store) CreateGuild()
    {
        var registry = new ClientRegistry<IChaosWorldClient>();
        var guild = MockGuild.Create("Alpha", clientRegistry: registry);
        var leader = CreateOnline(registry, "Stahli");
        var member = CreateOnline(registry, "Iglis");

        guild.AddMember(leader, leader);
        guild.ChangeRank(leader.Name, 0, leader);
        guild.AddMember(member, leader);

        var store = new Mock<IStore<Guild>>();

        store.Setup(s => s.Exists("Alpha"))
             .Returns(true);

        store.Setup(s => s.Load("Alpha"))
             .Returns(guild);

        return (guild, leader, member, store);
    }

    [Test]
    public void A_player_in_a_guild_is_told_to_leave_first_and_loses_their_applications()
    {
        var service = CreateService();
        var (_, leader, _, _) = CreateGuild();
        service.Apply(leader.Name, "Elsewhere", string.Empty);

        Show(GuildApplyScript.INITIAL_KEY, leader, service);

        RepliedText(leader).Should().Be("You're already in a guild. Leave it before applying to another.");
        service.ForApplicant(leader.Name).Should().BeEmpty();
    }

    [Test]
    public void The_first_screen_offers_joining_once_a_guild_has_accepted()
    {
        var service = CreateService();
        var applicant = MockAisling.Create(name: APPLICANT);

        OptionsOf(Show(GuildApplyScript.INITIAL_KEY, applicant, service))
            .Should()
            .Equal("Apply to a guild", "My applications");

        service.Apply(APPLICANT, "Alpha", string.Empty);
        service.Apply(APPLICANT, "Beta", string.Empty);
        service.Accept("Alpha", APPLICANT, "Stahli");

        OptionsOf(Show(GuildApplyScript.INITIAL_KEY, applicant, service))
            .Should()
            .Equal("Join a guild (1 offer)", "Apply to a guild", "My applications");
    }

    [Test]
    public void The_guild_list_leaves_out_guilds_already_applied_to()
    {
        using var folder = new TempGuildFolder("Alpha", "Beta", "Gamma");
        var service = CreateService(out _, out _, out _, folder.Root);
        var applicant = MockAisling.Create(name: APPLICANT);
        service.Apply(APPLICANT, "beta", string.Empty);

        OptionsOf(Show(GuildApplyScript.LIST_KEY, applicant, service))
            .Should()
            .Equal("Alpha", "Gamma");
    }

    [Test]
    public void The_guild_list_pages_ten_at_a_time()
    {
        using var folder = new TempGuildFolder(Enumerable.Range(1, 12).Select(i => $"G{i:00}").ToArray());
        var service = CreateService(out _, out _, out _, folder.Root);
        var applicant = MockAisling.Create(name: APPLICANT);

        OptionsOf(Show(GuildApplyScript.LIST_KEY, applicant, service))
            .Should()
            .Equal(Enumerable.Range(1, 10).Select(i => $"G{i:00}").Append("Next page"));

        var second = Screen(GuildApplyScript.LIST_KEY);
        second.Context = new GuildApplyScript.ApplyContext(1, [], null);

        CreateScript(second, service)
            .OnDisplaying(applicant);

        OptionsOf(second)
            .Should()
            .Equal("G11", "G12", "Previous page");
    }

    [Test]
    public void Picking_from_the_list_carries_the_guild_forward_and_Next_page_turns_the_page()
    {
        using var folder = new TempGuildFolder(Enumerable.Range(1, 12).Select(i => $"G{i:00}").ToArray());
        var service = CreateService(out _, out _, out _, folder.Root);
        var applicant = MockAisling.Create(name: APPLICANT);

        var picked = Screen(GuildApplyScript.LIST_KEY);
        var pickScript = CreateScript(picked, service);
        pickScript.OnDisplaying(applicant);
        pickScript.OnNext(applicant, 2);

        ((GuildApplyScript.ApplyContext)picked.Context!).Guild.Should().Be("G02");

        var paged = Screen(GuildApplyScript.LIST_KEY);
        var pageScript = CreateScript(paged, service);
        pageScript.OnDisplaying(applicant);
        pageScript.OnNext(applicant, 11);

        var context = (GuildApplyScript.ApplyContext)paged.Context!;
        context.Page.Should().Be(1);
        context.Guild.Should().BeNull();
    }

    [Test]
    public void At_five_open_applications_the_list_refuses()
    {
        var service = CreateService();
        var applicant = MockAisling.Create(name: APPLICANT);

        for (var i = 1; i <= 5; i++)
            service.Apply(APPLICANT, $"Guild{i}", string.Empty);

        Show(GuildApplyScript.LIST_KEY, applicant, service);

        RepliedText(applicant).Should().Be("You already have 5 open applications. Withdraw one before applying to another guild.");
    }

    [Test]
    public void The_confirmation_shows_the_guild_and_the_trimmed_note()
    {
        var service = CreateService();
        var applicant = MockAisling.Create(name: APPLICANT);

        Show(GuildApplyScript.CONFIRMATION_KEY, applicant, service, guild: "Alpha", typed: "  Lv 60 priest  ")
            .Text
            .Should()
            .Be("Apply to Alpha?\n\nYour note: Lv 60 priest");

        Show(GuildApplyScript.CONFIRMATION_KEY, applicant, service, guild: "Alpha")
            .Text
            .Should()
            .Be("Apply to Alpha?");
    }

    [Test]
    public void Sending_applies_and_tells_online_officers_only()
    {
        const string NOTICE = "Wanderer has applied to join the guild. Review it at Quill or Aricin.";
        var service = CreateService();
        var (_, leader, member, store) = CreateGuild();
        var applicant = MockAisling.Create(name: APPLICANT);

        var dialog = Show(
            GuildApplyScript.SENT_KEY,
            applicant,
            service,
            store.Object,
            "Your application to {Guild} has been sent.",
            "Alpha",
            "hello");

        dialog.Text.Should().Be("Your application to Alpha has been sent.");

        service.ForApplicant(APPLICANT)
               .Single()
               .Note
               .Should()
               .Be("hello");

        Mock.Get(leader.Client).Verify(c => c.SendServerMessage(ServerMessageType.ActiveMessage, NOTICE), Times.Once());
        Mock.Get(member.Client).Verify(c => c.SendServerMessage(ServerMessageType.ActiveMessage, NOTICE), Times.Never());
    }

    [Test]
    public void Sending_to_a_guild_that_no_longer_exists_is_refused()
    {
        var service = CreateService();
        var applicant = MockAisling.Create(name: APPLICANT);

        Show(GuildApplyScript.SENT_KEY, applicant, service, new Mock<IStore<Guild>>().Object, guild: "Gone");

        RepliedText(applicant).Should().Be("That guild no longer exists.");
        service.ForApplicant(APPLICANT).Should().BeEmpty();
    }

    [Test]
    public void My_applications_lists_each_with_its_state()
    {
        var service = CreateService(out _, out var time, out _);
        var applicant = MockAisling.Create(name: APPLICANT);

        service.Apply(APPLICANT, "Beta", string.Empty);
        service.Apply(APPLICANT, "Alpha", string.Empty);
        service.Accept("Beta", APPLICANT, "Stahli");
        time.Later(TimeSpan.FromDays(5));

        var dialog = Show(GuildApplyScript.MINE_KEY, applicant, service);

        OptionsOf(dialog)
            .Should()
            .Equal("Alpha - waiting, 9 days left", "Beta - accepted!");

        dialog.Options
              .Select(option => option.DialogKey)
              .Should()
              .Equal(GuildApplyScript.WAITING_KEY, GuildApplyScript.OFFER_KEY);

        ((GuildApplyScript.ApplyContext)dialog.Context!).Shown.Should().Equal("Alpha", "Beta");
    }

    [Test]
    public void Describe_rounds_the_days_left_up()
    {
        var application = new GuildApplication
        {
            GuildName = "Alpha",
            AppliedAt = Start.UtcDateTime
        };

        GuildApplyScript.Describe(application, Start.UtcDateTime)
                        .Should()
                        .Be("Alpha - waiting, 14 days left");

        GuildApplyScript.Describe(application, Start.UtcDateTime.AddDays(13).AddHours(1))
                        .Should()
                        .Be("Alpha - waiting, 1 day left");
    }

    [Test]
    public void Withdrawing_removes_the_application()
    {
        var service = CreateService();
        var applicant = MockAisling.Create(name: APPLICANT);
        service.Apply(APPLICANT, "Alpha", string.Empty);

        Show(GuildApplyScript.WITHDRAWN_KEY, applicant, service, text: "You withdrew your application to {Guild}.", guild: "Alpha")
            .Text
            .Should()
            .Be("You withdrew your application to Alpha.");

        service.ForApplicant(APPLICANT).Should().BeEmpty();
    }

    [Test]
    public void Joining_takes_the_offer_adds_the_member_and_saves_the_guild()
    {
        var service = CreateService();
        var (guild, _, _, store) = CreateGuild();
        var applicant = MockAisling.Create(name: APPLICANT);

        service.Apply(APPLICANT, "Alpha", string.Empty);
        service.Apply(APPLICANT, "Beta", string.Empty);
        service.Accept("Alpha", APPLICANT, "Stahli");

        Show(GuildApplyScript.JOINED_KEY, applicant, service, store.Object, "You joined {Guild}!", "Alpha")
            .Text
            .Should()
            .Be("You joined Alpha!");

        applicant.Guild.Should().BeSameAs(guild);
        service.ForApplicant(APPLICANT).Should().BeEmpty();
        store.Verify(s => s.Save(guild), Times.Once());
    }

    [Test]
    public void Joining_a_guild_that_no_longer_exists_withdraws_the_offer()
    {
        var service = CreateService();
        var applicant = MockAisling.Create(name: APPLICANT);
        service.Apply(APPLICANT, "Gone", string.Empty);
        service.Accept("Gone", APPLICANT, "Stahli");

        Show(GuildApplyScript.JOINED_KEY, applicant, service, new Mock<IStore<Guild>>().Object, guild: "Gone");

        RepliedText(applicant).Should().Be("That guild no longer exists.");
        service.ForApplicant(APPLICANT).Should().BeEmpty();
    }

    [Test]
    public void Joining_without_an_offer_is_refused()
    {
        var service = CreateService();
        var (_, _, _, store) = CreateGuild();
        var applicant = MockAisling.Create(name: APPLICANT);
        service.Apply(APPLICANT, "Alpha", string.Empty);

        Show(GuildApplyScript.JOINED_KEY, applicant, service, store.Object, guild: "Alpha");

        RepliedText(applicant).Should().Be("That offer has expired.");
        applicant.Guild.Should().BeNull();
    }

    [Test]
    public void Decide_later_closes_or_goes_on_to_the_Terminus()
    {
        var service = CreateService();
        var applicant = MockAisling.Create(name: APPLICANT);
        service.Apply(APPLICANT, "Alpha", string.Empty);
        service.Accept("Alpha", APPLICANT, "Stahli");

        var plain = Show(GuildApplyScript.OFFERS_KEY, applicant, service);

        OptionsOf(plain)
            .Should()
            .Equal("Alpha", "Decide later");

        plain.Options[^1].DialogKey.Should().Be("close");

        var fromTerminus = Screen(GuildApplyScript.OFFERS_KEY);

        Mock.Get(fromTerminus.DialogSource)
            .SetupGet(source => source.Name)
            .Returns("Terminus");

        CreateScript(fromTerminus, service)
            .OnDisplaying(applicant);

        fromTerminus.Options[^1].DialogKey.Should().Be("terminus_homeoptions");
    }

    [Test]
    public void Turning_down_leads_to_the_other_offers_or_on_to_the_Terminus()
    {
        var service = CreateService();
        var applicant = MockAisling.Create(name: APPLICANT);
        service.Apply(APPLICANT, "Alpha", string.Empty);
        service.Apply(APPLICANT, "Beta", string.Empty);
        service.Accept("Alpha", APPLICANT, "Stahli");
        service.Accept("Beta", APPLICANT, "Stahli");

        var first = Show(GuildApplyScript.TURNED_DOWN_KEY, applicant, service, text: "You turned down {Guild}.", guild: "Alpha");

        first.Text.Should().Be("You turned down Alpha.");
        first.NextDialogKey.Should().Be(GuildApplyScript.OFFERS_KEY);

        var last = Screen(GuildApplyScript.TURNED_DOWN_KEY, "You turned down {Guild}.", "Beta");

        Mock.Get(last.DialogSource)
            .SetupGet(source => source.Name)
            .Returns("Terminus");

        CreateScript(last, service)
            .OnDisplaying(applicant);

        last.NextDialogKey.Should().Be("terminus_homeoptions");
        service.ForApplicant(APPLICANT).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj
```

Expected: build errors naming `GuildApplyScript`.

- [ ] **Step 3: Write the script**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplyScript.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Extensions.Common;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.NLog.Logging.Definitions;
using Chaos.NLog.Logging.Extensions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts.Abstractions;
using Chaos.Services.GuildApplications;
using Chaos.Storage.Abstractions;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.GuildScripts;

/// <summary>
///     The applicant's side of guild applications: the board on Abel Port Way and the offers menu shown at login. A player
///     without a guild applies, reads their applications, withdraws, and joins or turns down guilds that accepted them. Every
///     rule lives in <see cref="GuildApplicationService" />; this script only runs the menus.
/// </summary>
public class GuildApplyScript : GuildScriptBase
{
    public const string CONFIRMATION_KEY = "generic_guild_apply_confirmation";
    public const string INITIAL_KEY = "generic_guild_apply_initial";
    public const string JOINED_KEY = "generic_guild_apply_joined";
    public const string LIST_KEY = "generic_guild_apply_list";
    public const string MINE_KEY = "generic_guild_apply_mine";
    public const string NEXT_PAGE = "Next page";
    public const string NOTE_KEY = "generic_guild_apply_note";
    public const string OFFER_KEY = "generic_guild_apply_offer";
    public const string OFFERS_KEY = "generic_guild_apply_offers";
    public const int PAGE_SIZE = 10;
    public const string PREVIOUS_PAGE = "Previous page";
    public const string SENT_KEY = "generic_guild_apply_sent";
    public const string TURNED_DOWN_KEY = "generic_guild_apply_turned_down";
    public const string WAITING_KEY = "generic_guild_apply_waiting";
    public const string WITHDRAWN_KEY = "generic_guild_apply_withdrawn";

    private const string CLOSE_KEY = "close";
    private const string DECIDE_LATER = "Decide later";
    private const string TERMINUS_HOME_KEY = "terminus_homeoptions";
    private const string TERMINUS_NAME = "Terminus";

    private static readonly string NoteTooLong = $"Keep the note to {GuildApplicationService.NOTE_MAX_LENGTH} characters.";

    private static readonly string TooMany
        = $"You already have {GuildApplicationService.MAX_OPEN_APPLICATIONS} open applications. Withdraw one before applying to another guild.";

    private readonly GuildApplicationService Applications;

    /// <inheritdoc />
    public GuildApplyScript(
        Dialog subject,
        IClientRegistry<IChaosWorldClient> clientRegistry,
        IStore<Guild> guildStore,
        IFactory<Guild> guildFactory,
        ILogger<GuildApplyScript> logger,
        GuildApplicationService applications)
        : base(
            subject,
            clientRegistry,
            guildStore,
            guildFactory,
            logger)
        => Applications = applications;

    /// <summary>
    ///     One line of "My applications": "Shinebox - accepted!" or "Shinebox - waiting, 9 days left"
    /// </summary>
    public static string Describe(GuildApplication application, DateTime now)
    {
        if (application.IsAccepted)
            return $"{application.GuildName} - accepted!";

        var daysLeft = Math.Max(1, (int)Math.Ceiling((GuildApplicationService.ExpiresAt(application) - now).TotalDays));

        return daysLeft == 1
            ? $"{application.GuildName} - waiting, 1 day left"
            : $"{application.GuildName} - waiting, {daysLeft} days left";
    }

    /// <inheritdoc />
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case INITIAL_KEY:
                OnDisplayingInitial(source);

                break;
            case LIST_KEY:
                OnDisplayingList(source);

                break;
            case NOTE_KEY:
                if (IsGuildless(source))
                    TryGetChosenGuild(source, out _);

                break;
            case CONFIRMATION_KEY:
                OnDisplayingConfirmation(source);

                break;
            case SENT_KEY:
                OnDisplayingSent(source);

                break;
            case MINE_KEY:
                OnDisplayingMine(source);

                break;
            case WAITING_KEY:
            case OFFER_KEY:
                if (IsGuildless(source) && TryGetChosenGuild(source, out var guildName))
                    Subject.InjectTextParameters(guildName);

                break;
            case WITHDRAWN_KEY:
                OnDisplayingWithdrawn(source);

                break;
            case OFFERS_KEY:
                OnDisplayingOffers(source);

                break;
            case JOINED_KEY:
                OnDisplayingJoined(source);

                break;
            case TURNED_DOWN_KEY:
                OnDisplayingTurnedDown(source);

                break;
        }
    }

    /// <inheritdoc />
    public override void OnNext(Aisling source, byte? optionIndex = null)
    {
        if (optionIndex is null)
            return;

        switch (Subject.Template.TemplateKey.ToLower())
        {
            case LIST_KEY:
            case MINE_KEY:
            case OFFERS_KEY:
                OnNextPick(optionIndex.Value);

                break;
        }
    }

    /// <summary>
    ///     A player in a guild can't apply, and whatever applications they had no longer matter
    /// </summary>
    private bool IsGuildless(Aisling source)
    {
        if (source.Guild is null)
            return true;

        Applications.ClearApplicant(source.Name);
        Subject.Reply(source, "You're already in a guild. Leave it before applying to another.");

        return false;
    }

    private void OnDisplayingConfirmation(Aisling source)
    {
        if (!IsGuildless(source) || !TryGetChosenGuild(source, out var guildName) || !TryReadNote(source, out var note))
            return;

        Subject.InjectTextParameters(note.Length == 0 ? $"Apply to {guildName}?" : $"Apply to {guildName}?\n\nYour note: {note}");
    }

    private void OnDisplayingInitial(Aisling source)
    {
        if (!IsGuildless(source))
            return;

        var offers = Applications.OffersFor(source.Name).Count;

        if (offers > 0)
            Subject.AddOption(offers == 1 ? "Join a guild (1 offer)" : $"Join a guild ({offers} offers)", OFFERS_KEY);

        Subject.AddOptions(("Apply to a guild", LIST_KEY), ("My applications", MINE_KEY));
    }

    private void OnDisplayingJoined(Aisling source)
    {
        if (!IsGuildless(source) || !TryGetChosenGuild(source, out var guildName))
            return;

        if (!GuildExists(guildName))
        {
            Applications.Withdraw(source.Name, guildName);
            Subject.Reply(source, "That guild no longer exists.");

            return;
        }

        if (!Applications.TryTakeOffer(source.Name, guildName, out var acceptedBy))
        {
            Subject.Reply(source, "That offer has expired.");

            return;
        }

        var guild = GuildStore.Load(guildName);
        guild.AddMember(source, acceptedBy);
        GuildStore.Save(guild);

        Logger.WithTopics(Topics.Entities.Guild, Topics.Actions.Join)
              .WithProperty(guild)
              .WithProperty(source)
              .LogInformation(
                  "Aisling {@AislingName} joined guild {@GuildName} through an application accepted by {Officer}",
                  source.Name,
                  guild.Name,
                  acceptedBy);

        Subject.InjectTextParameters(guild.Name);
    }

    private void OnDisplayingList(Aisling source)
    {
        if (!IsGuildless(source))
            return;

        //"No" at the confirmation comes back here carrying the note typed last time. The next note screen appends the new
        //one, and the confirmation reads the first argument, so start the arguments afresh
        Subject.MenuArgs = [];

        var mine = Applications.ForApplicant(source.Name);

        if (mine.Count >= GuildApplicationService.MAX_OPEN_APPLICATIONS)
        {
            Subject.Reply(source, TooMany, MINE_KEY);

            return;
        }

        var guilds = Applications.ListGuilds()
                                 .Where(name => !mine.Any(application => application.GuildName.EqualsI(name)))
                                 .ToList();

        if (guilds.Count == 0)
        {
            Subject.Reply(source, "There are no other guilds to apply to.", INITIAL_KEY);

            return;
        }

        var context = Subject.Context as ApplyContext;
        var pageCount = (guilds.Count + PAGE_SIZE - 1) / PAGE_SIZE;
        var page = Math.Clamp(context?.Page ?? 0, 0, pageCount - 1);

        var shown = guilds.Skip(page * PAGE_SIZE)
                          .Take(PAGE_SIZE)
                          .ToArray();

        //the guilds first, then paging, all before the template's own options (Back)
        var index = 0;

        foreach (var name in shown)
            Subject.InsertOption(index++, name, NOTE_KEY);

        if (page + 1 < pageCount)
            Subject.InsertOption(index++, NEXT_PAGE, LIST_KEY);

        if (page > 0)
            Subject.InsertOption(index, PREVIOUS_PAGE, LIST_KEY);

        Subject.Context = new ApplyContext(page, shown, null);
    }

    private void OnDisplayingMine(Aisling source)
    {
        if (!IsGuildless(source))
            return;

        var applications = Applications.ForApplicant(source.Name);

        if (applications.Count == 0)
        {
            Subject.Reply(source, "You have no applications.", INITIAL_KEY);

            return;
        }

        var now = Applications.NowUtc;

        for (var i = 0; i < applications.Count; i++)
            Subject.InsertOption(i, Describe(applications[i], now), applications[i].IsAccepted ? OFFER_KEY : WAITING_KEY);

        Subject.Context = new ApplyContext(
            0,
            applications.Select(application => application.GuildName)
                        .ToArray(),
            null);
    }

    private void OnDisplayingOffers(Aisling source)
    {
        if (!IsGuildless(source))
            return;

        var offers = Applications.OffersFor(source.Name);

        if (offers.Count == 0)
        {
            Subject.Reply(source, "No guild has accepted you yet.");

            return;
        }

        for (var i = 0; i < offers.Count; i++)
            Subject.InsertOption(i, offers[i].GuildName, OFFER_KEY);

        //shown from the Terminus at login, "Decide later" goes on to the welcome-back menu this one stood in for
        Subject.AddOption(DECIDE_LATER, FromTerminus() ? TERMINUS_HOME_KEY : CLOSE_KEY);

        Subject.Context = new ApplyContext(
            0,
            offers.Select(offer => offer.GuildName)
                  .ToArray(),
            null);
    }

    private void OnDisplayingSent(Aisling source)
    {
        if (!IsGuildless(source) || !TryGetChosenGuild(source, out var guildName) || !TryReadNote(source, out var note))
            return;

        if (!GuildExists(guildName))
        {
            Subject.Reply(source, "That guild no longer exists.", LIST_KEY);

            return;
        }

        switch (Applications.Apply(source.Name, guildName, note))
        {
            case GuildApplyResult.AlreadyApplied:
                Subject.Reply(source, $"You have already applied to {guildName}.", MINE_KEY);

                return;
            case GuildApplyResult.TooMany:
                Subject.Reply(source, TooMany, MINE_KEY);

                return;
            case GuildApplyResult.NoteTooLong:
                Subject.Reply(source, NoteTooLong, LIST_KEY);

                return;
        }

        Subject.InjectTextParameters(guildName);
        TellOfficers(guildName, source.Name);
    }

    private void OnDisplayingTurnedDown(Aisling source)
    {
        if (!IsGuildless(source) || !TryGetChosenGuild(source, out var guildName))
            return;

        Applications.Withdraw(source.Name, guildName);
        Subject.InjectTextParameters(guildName);

        //back to the other offers; with none left, a login pop-up that stood in for the Terminus goes on to it
        if (Applications.OffersFor(source.Name).Count > 0)
            Subject.NextDialogKey = OFFERS_KEY;
        else if (FromTerminus())
            Subject.NextDialogKey = TERMINUS_HOME_KEY;
    }

    private void OnDisplayingWithdrawn(Aisling source)
    {
        if (!IsGuildless(source) || !TryGetChosenGuild(source, out var guildName))
            return;

        Applications.Withdraw(source.Name, guildName);
        Subject.InjectTextParameters(guildName);
    }

    //the options are the shown guilds in order, then (list only) paging, then the template's own options
    private void OnNextPick(byte optionIndex)
    {
        var context = Subject.Context as ApplyContext ?? new ApplyContext(0, [], null);

        if ((optionIndex >= 1) && (optionIndex <= context.Shown.Length))
        {
            Subject.Context = context with
            {
                Guild = context.Shown[optionIndex - 1]
            };

            return;
        }

        var text = Subject.GetOptionText(optionIndex);

        if (text == NEXT_PAGE)
            Subject.Context = context with
            {
                Page = context.Page + 1,
                Guild = null
            };
        else if (text == PREVIOUS_PAGE)
            Subject.Context = context with
            {
                Page = Math.Max(0, context.Page - 1),
                Guild = null
            };
    }

    private bool FromTerminus() => Subject.DialogSource.Name.EqualsI(TERMINUS_NAME);

    //online members who can admit hear about a new application straight away
    private void TellOfficers(string guildName, string applicantName)
    {
        var guild = GuildStore.Load(guildName);

        foreach (var member in guild.GetOnlineMembers())
            if (guild.HasPermission(member.Name, GuildPermission.Admit))
                member.SendActiveMessage($"{applicantName} has applied to join the guild. Review it at Quill or Aricin.");
    }

    private bool TryGetChosenGuild(Aisling source, [MaybeNullWhen(false)] out string guildName)
    {
        guildName = (Subject.Context as ApplyContext)?.Guild;

        if (!string.IsNullOrEmpty(guildName))
            return true;

        Subject.ReplyToUnknownInput(source);

        return false;
    }

    /// <summary>
    ///     The typed note, trimmed; blank or missing is an empty note. An over-long one is refused; that only happens with a
    ///     modified client, because the text box stops at 60
    /// </summary>
    private bool TryReadNote(Aisling source, out string note)
    {
        note = (TryFetchArgs<string>(out var typed) ? typed : string.Empty).Trim();

        if (note.Length <= GuildApplicationService.NOTE_MAX_LENGTH)
            return true;

        Subject.Reply(source, NoteTooLong, LIST_KEY);

        return false;
    }

    /// <summary>
    ///     What a list screen showed and what the player picked from it, carried from screen to screen in
    ///     <see cref="Dialog.Context" />
    /// </summary>
    /// <param name="Page">
    ///     The page of the guild list, from 0
    /// </param>
    /// <param name="Shown">
    ///     The guild names the list's first options stand for, in option order
    /// </param>
    /// <param name="Guild">
    ///     The guild the player picked, once they have
    /// </param>
    public sealed record ApplyContext(int Page, string[] Shown, string? Guild);
}
```

- [ ] **Step 4: Run the tests**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildApplications/GuildApplyScriptTests/*" --no-ansi
```

Expected: all 17 tests pass.

- [ ] **Step 5: Write the 12 dialog templates**

Create each file under `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications/`, named `<templateKey>.json`, with exactly this content.

`generic_guild_apply_initial.json`:

```json
{
  "options": [],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_initial",
  "text": "The guild board. Guilds read the applications pinned here and answer by letter.",
  "type": "Menu"
}
```

`generic_guild_apply_list.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_apply_initial",
      "optionText": "Back"
    }
  ],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_list",
  "text": "Which guild would you like to join?",
  "type": "Menu"
}
```

`generic_guild_apply_note.json`:

```json
{
  "contextual": true,
  "nextDialogKey": "generic_guild_apply_confirmation",
  "options": [],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_note",
  "text": "Add a short note for the officers, or leave it blank.",
  "textBoxLength": 60,
  "type": "DialogTextEntry"
}
```

`generic_guild_apply_confirmation.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_apply_sent",
      "optionText": "Yes"
    },
    {
      "dialogKey": "generic_guild_apply_list",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_confirmation",
  "text": "{Text}",
  "type": "Menu"
}
```

`generic_guild_apply_sent.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_sent",
  "text": "Your application to {Guild} has been sent. Watch your mail.",
  "type": "Normal"
}
```

`generic_guild_apply_mine.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_apply_initial",
      "optionText": "Back"
    }
  ],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_mine",
  "text": "Your applications:",
  "type": "Menu"
}
```

`generic_guild_apply_waiting.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_apply_withdrawn",
      "optionText": "Withdraw"
    },
    {
      "dialogKey": "generic_guild_apply_mine",
      "optionText": "Back"
    }
  ],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_waiting",
  "text": "Your application to {Guild} is waiting for an answer.",
  "type": "Menu"
}
```

`generic_guild_apply_withdrawn.json`:

```json
{
  "contextual": true,
  "nextDialogKey": "generic_guild_apply_mine",
  "options": [],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_withdrawn",
  "text": "You withdrew your application to {Guild}.",
  "type": "Normal"
}
```

`generic_guild_apply_offers.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_offers",
  "text": "These guilds have accepted you. Pick one to join.",
  "type": "Menu"
}
```

`generic_guild_apply_offer.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_apply_joined",
      "optionText": "Join"
    },
    {
      "dialogKey": "generic_guild_apply_turned_down",
      "optionText": "Turn down"
    },
    {
      "dialogKey": "generic_guild_apply_offers",
      "optionText": "Back"
    }
  ],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_offer",
  "text": "{Guild} accepted you. Join now? Your other applications will be withdrawn.",
  "type": "Menu"
}
```

`generic_guild_apply_joined.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_joined",
  "text": "You joined {Guild}!",
  "type": "Normal"
}
```

`generic_guild_apply_turned_down.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "GuildApply"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_apply_turned_down",
  "text": "You turned down {Guild}.",
  "type": "Normal"
}
```

- [ ] **Step 6: Check the templates parse**

```bash
python - <<'EOF'
import json, glob, os
folder = r'C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications'
files = sorted(glob.glob(os.path.join(folder, 'generic_guild_apply_*.json')))
for f in files:
    d = json.load(open(f, encoding='utf-8'))
    assert d['templateKey'] + '.json' == os.path.basename(f), f
    assert d['scriptKeys'] == ['GuildApply'], f
    assert all(ord(c) < 128 for c in json.dumps(d, ensure_ascii=False)), f
print(len(files))
EOF
```

Expected: `12`.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplyScript.cs", "Tests/Chaos.Tests/GuildApplications/GuildApplyScriptTests.cs", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications/generic_guild_apply_*.json"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildApplications/GuildApplyScriptTests/*\" --no-ansi", "acceptanceCriteria": ["player in a guild gets the leave-first reply and loses their applications", "first screen shows Join a guild (N offers) only with offers, then Apply to a guild, My applications", "list leaves out applied guilds, pages 10 with Next page / Previous page, picking sets ApplyContext.Guild", "at 5 open the list replies the TooMany text", "confirmation reads Apply to Alpha? plus the trimmed note", "sending applies, injects the guild, notifies online Admit members only; missing guild refused", "My applications lines and dialog keys", "withdrawing removes", "joining takes the offer, adds and saves; missing guild withdraws; no offer replies expired", "Decide later close or terminus_homeoptions; turned down Next goes to offers or Terminus", "12 templates parse with script key GuildApply"], "modelTier": "standard"}
```

---

### Task 5: The officers' menus, the Members option and disband cleanup

**Goal:** Ranks with `Admit` see `Applications (N)` under Members at Quill and Aricin, review waiting applications, accept or decline with letters, and disbanding clears a guild's applications.

**Files:**
- Create: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplicationReviewScript.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs` (using, field, constructor, one option)
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs` (using, field, constructor, one call)
- Modify: `SRV/Tests/Chaos.Tests/GuildPermissions/GuildMemberPermissionTests.cs` (`MembersMenuFor` and two tests' expected lists)
- Modify: `SRV/Tests/Chaos.Tests/GuildCloak/GuildCloakScriptTests.cs` (one constructor call)
- Create: 4 files in `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications/` (listed in Step 8)
- Test: `SRV/Tests/Chaos.Tests/GuildApplications/GuildApplicationReviewScriptTests.cs` (new)

**Acceptance Criteria:**
- [ ] `Applications (2)` follows `Roster` for a Council member when two wait; plain `Applications` when none; no option for a rank without `Admit`
- [ ] The list reads `Wanderer - level 45 Priest - 2 days ago` then `Unreadable - today`, oldest first, and picking sets `ReviewContext.Applicant`; empty replies `No one has applied.`
- [ ] The view reads `Wanderer, level 45 Priest, applied today.\n\nNote: **** words` (note filtered)
- [ ] Accepting makes an offer by the officer, posts one letter (author `Aricin`, subject `Guild application`) and saves the mailbox once; an online applicant also gets the active message
- [ ] Accepting someone whose guild still lists them replies `Wanderer has already joined another guild.`, clears them and sends no letter; a guild that no longer lists them doesn't count
- [ ] Declining removes the application and posts `Shinebox declined your guild application.`
- [ ] A rank without `Admit` is refused with `Your rank can't admit new members.`; a vanished application replies `That application is no longer waiting.`
- [ ] Disbanding removes the guild's applications
- [ ] The updated `GuildPermissions` and `GuildCloak` tests pass, and the 4 templates parse with script key `GuildApplicationReview`

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildApplications/*/*" --no-ansi` → all pass, and the `GuildPermissions` and `GuildCloak` runs → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/GuildApplications/GuildApplicationReviewScriptTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts;
using Chaos.Services.GuildApplications;
using Chaos.Services.GuildCloak;
using Chaos.Services.Other.Abstractions;
using Chaos.Services.Storage;
using Chaos.Services.Storage.Abstractions;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static Chaos.Tests.GuildApplications.GuildApplicationTestSupport;
#endregion

namespace Chaos.Tests.GuildApplications;

public sealed class GuildApplicationReviewScriptTests
{
    private const string APPLICANT = "Wanderer";
    private const string GUILD = "Shinebox";

    private static string RepliedText(Aisling aisling) => aisling.ActiveDialog.Get()!.Text;

    [Test]
    public void Only_ranks_that_can_admit_see_Applications_with_the_waiting_count()
    {
        var world = new World();

        world.MembersMenuFor(world.Leader).Should().ContainInOrder("Roster", "Applications");

        world.Service.Apply(APPLICANT, GUILD, string.Empty);
        world.Service.Apply("Second", GUILD, string.Empty);

        world.MembersMenuFor(world.Council).Should().ContainInOrder("Roster", "Applications (2)");
        world.MembersMenuFor(world.Plain).Should().NotContain(text => text.StartsWith("Applications"));
    }

    [Test]
    public void The_list_shows_level_class_and_age_oldest_first()
    {
        var world = new World();
        world.Service.Apply(APPLICANT, GUILD, string.Empty);
        world.Time.Later(TimeSpan.FromDays(2));
        world.Service.Apply("Unreadable", GUILD, string.Empty);

        var dialog = world.Show(GuildApplicationReviewScript.INITIAL_KEY, world.Council);

        dialog.Options
              .Select(option => option.OptionText)
              .Should()
              .Equal("Wanderer - level 45 Priest - 2 days ago", "Unreadable - today");

        ((GuildApplicationReviewScript.ReviewContext)dialog.Context!).Shown.Should().Equal(APPLICANT, "Unreadable");
    }

    [Test]
    public void An_empty_list_says_no_one_has_applied()
    {
        var world = new World();

        world.Show(GuildApplicationReviewScript.INITIAL_KEY, world.Council);

        RepliedText(world.Council).Should().Be("No one has applied.");
    }

    [Test]
    public void Picking_an_applicant_carries_them_forward()
    {
        var world = new World();
        world.Service.Apply(APPLICANT, GUILD, string.Empty);
        var dialog = MockDialog.Create(GuildApplicationReviewScript.INITIAL_KEY);
        var script = world.CreateScript(dialog);

        script.OnDisplaying(world.Council);
        script.OnNext(world.Council, 1);

        ((GuildApplicationReviewScript.ReviewContext)dialog.Context!).Applicant.Should().Be(APPLICANT);
    }

    [Test]
    public void The_view_shows_the_applicant_and_the_filtered_note()
    {
        var world = new World();
        world.Service.Apply(APPLICANT, GUILD, "rude words");

        world.Show(GuildApplicationReviewScript.VIEW_KEY, world.Council, APPLICANT)
             .Text
             .Should()
             .Be("Wanderer, level 45 Priest, applied today.\n\nNote: **** words");
    }

    [Test]
    public void Accepting_makes_an_offer_and_sends_a_letter()
    {
        var world = new World();
        world.Service.Apply(APPLICANT, GUILD, string.Empty);

        world.Show(GuildApplicationReviewScript.ACCEPTED_KEY, world.Council, APPLICANT, "You accepted {Name}.")
             .Text
             .Should()
             .Be("You accepted Wanderer.");

        world.Service
             .OffersFor(APPLICANT)
             .Single()
             .AcceptedBy
             .Should()
             .Be("Iglis");

        var letter = world.Mailbox(APPLICANT)
                          .Posts
                          .Values
                          .Should()
                          .ContainSingle()
                          .Subject;

        letter.Author.Should().Be("Aricin");
        letter.Subject.Should().Be("Guild application");
        letter.Message.Should().StartWith("Shinebox accepted your guild application.");
        world.Mail.Verify(s => s.Save(It.IsAny<MailBox>()), Times.Once());
    }

    [Test]
    public void An_online_applicant_also_hears_at_once()
    {
        var world = new World(true);
        world.Service.Apply(APPLICANT, GUILD, string.Empty);

        world.Show(GuildApplicationReviewScript.ACCEPTED_KEY, world.Council, APPLICANT);

        Mock.Get(world.Applicant.Client)
            .Verify(
                c => c.SendServerMessage(
                    ServerMessageType.ActiveMessage,
                    "Shinebox accepted your application. Open the guild board on Abel Port Way to join."),
                Times.Once());
    }

    [Test]
    public void Accepting_someone_already_in_a_guild_is_refused_and_clears_them()
    {
        var world = new World();
        var other = MockGuild.Create("Other");
        other.AddMember(world.Applicant, world.Applicant);
        world.Service.Apply(APPLICANT, GUILD, string.Empty);

        world.Show(GuildApplicationReviewScript.ACCEPTED_KEY, world.Council, APPLICANT);

        RepliedText(world.Council).Should().Be("Wanderer has already joined another guild.");
        world.Service.ForApplicant(APPLICANT).Should().BeEmpty();
        world.Mail.Verify(s => s.Save(It.IsAny<MailBox>()), Times.Never());
    }

    [Test]
    public void A_player_kicked_while_offline_counts_as_guildless()
    {
        var world = new World();

        //their save still names the guild, but the guild no longer lists them
        world.Applicant.Guild = MockGuild.Create("Other");
        world.Service.Apply(APPLICANT, GUILD, string.Empty);

        world.Show(GuildApplicationReviewScript.ACCEPTED_KEY, world.Council, APPLICANT);

        world.Service.OffersFor(APPLICANT).Should().ContainSingle();
    }

    [Test]
    public void Declining_removes_the_application_and_sends_a_letter()
    {
        var world = new World();
        world.Service.Apply(APPLICANT, GUILD, string.Empty);

        world.Show(GuildApplicationReviewScript.DECLINED_KEY, world.Council, APPLICANT, "You declined {Name}.")
             .Text
             .Should()
             .Be("You declined Wanderer.");

        world.Service.ForApplicant(APPLICANT).Should().BeEmpty();

        world.Mailbox(APPLICANT)
             .Posts
             .Values
             .Single()
             .Message
             .Should()
             .Be("Shinebox declined your guild application.");
    }

    [Test]
    public void A_rank_that_cannot_admit_is_refused()
    {
        var world = new World();
        world.Service.Apply(APPLICANT, GUILD, string.Empty);

        world.Show(GuildApplicationReviewScript.ACCEPTED_KEY, world.Plain, APPLICANT);

        RepliedText(world.Plain).Should().Be("Your rank can't admit new members.");
        world.Service.OffersFor(APPLICANT).Should().BeEmpty();
    }

    [Test]
    public void An_application_that_is_no_longer_waiting_is_refused()
    {
        var world = new World();

        world.Show(GuildApplicationReviewScript.VIEW_KEY, world.Council, APPLICANT);

        RepliedText(world.Council).Should().Be("That application is no longer waiting.");
    }

    [Test]
    public void Disbanding_removes_the_guilds_applications()
    {
        var world = new World();
        world.Service.Apply(APPLICANT, GUILD, string.Empty);

        var houseStorage = new Mock<IStorage<GuildHouseState>>();

        houseStorage.SetupGet(s => s.Value)
                    .Returns(new GuildHouseState(houseStorage.Object));

        var cloakStorage = new Mock<IStorage<GuildCloakState>>();

        cloakStorage.SetupGet(s => s.Value)
                    .Returns(new GuildCloakState());

        var cloaks = new GuildCloakService(
            cloakStorage.Object,
            houseStorage.Object,
            world.Clients,
            TimeProvider.System,
            NullLogger<GuildCloakService>.Instance);

        new GuildDisbandScript(
            MockDialog.Create("generic_guild_disband_accepted"),
            world.Clients,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            NullLogger<GuildDisbandScript>.Instance,
            houseStorage.Object,
            cloaks,
            world.Service).OnDisplaying(world.Leader);

        world.Service.ForApplicant(APPLICANT).Should().BeEmpty();
    }

    /// <summary>
    ///     The guild "Shinebox" with an online leader, a Council member (who can admit by default) and a plain member (who
    ///     can't). The applicant "Wanderer" is a level 45 Priest whose save the facade store returns; online only when asked.
    /// </summary>
    private sealed class World
    {
        public readonly Aisling Applicant;
        public readonly ClientRegistry<IChaosWorldClient> Clients = new();
        public readonly Aisling Council;
        public readonly Aisling Leader;
        public readonly Mock<IStore<MailBox>> Mail = new();
        public readonly Aisling Plain;
        public readonly GuildApplicationService Service;
        public readonly FixedTime Time;
        private readonly IChatFilterService ChatFilter;
        private readonly AislingFacadeCache Facades;
        private readonly Dictionary<string, MailBox> Mailboxes = new(StringComparer.OrdinalIgnoreCase);

        public World(bool applicantOnline = false)
        {
            Service = CreateService(out _, out Time, out _);

            var guild = MockGuild.Create(GUILD, clientRegistry: Clients);
            Leader = Online("Stahli");
            Council = Online("Iglis");
            Plain = Online("Plain");

            guild.AddMember(Leader, Leader);
            guild.ChangeRank(Leader.Name, 0, Leader);
            guild.AddMember(Council, Leader);
            guild.ChangeRank(Council.Name, 1, Leader);
            guild.AddMember(Plain, Leader);

            Applicant = applicantOnline ? Online(APPLICANT) : MockAisling.Create(name: APPLICANT);
            Applicant.UserStatSheet.SetLevel(45);
            Applicant.UserStatSheet.SetBaseClass(BaseClass.Priest);

            var saves = new Mock<IFacadeStore<Aisling>>();

            saves.Setup(s => s.Load(APPLICANT))
                 .Returns(Applicant);

            Facades = new AislingFacadeCache(
                new MemoryCache(new MemoryCacheOptions()),
                new Mock<ILogger<AislingFacadeCache>>().Object,
                saves.Object,
                Clients);

            Mail.Setup(s => s.Load(It.IsAny<string>()))
                .Returns((string name) => Mailbox(name));

            var chatFilter = new Mock<IChatFilterService>();

            chatFilter.Setup(f => f.FilterMessage(It.IsAny<string>()))
                      .Returns((string text) => text.Replace("rude", "****"));

            ChatFilter = chatFilter.Object;
        }

        public GuildApplicationReviewScript CreateScript(Dialog dialog)
            => new(
                dialog,
                Clients,
                new Mock<IStore<Guild>>().Object,
                new Mock<IFactory<Guild>>().Object,
                new Mock<ILogger<GuildApplicationReviewScript>>().Object,
                Service,
                Facades,
                Mail.Object,
                ChatFilter);

        public MailBox Mailbox(string name)
            => Mailboxes.TryGetValue(name, out var mailbox) ? mailbox : Mailboxes[name] = new MailBox(name, NullLogger<MailBox>.Instance);

        public IEnumerable<string> MembersMenuFor(Aisling source)
        {
            var dialog = MockDialog.Create("generic_guild_members_initial");

            new GuildMemberManagementScript(
                dialog,
                Clients,
                new Mock<IStore<Guild>>().Object,
                new Mock<IFactory<Guild>>().Object,
                new Mock<ILogger<GuildMemberManagementScript>>().Object,
                Service).OnDisplaying(source);

            return dialog.Options.Select(option => option.OptionText);
        }

        public Dialog Show(
            string templateKey,
            Aisling source,
            string? applicant = null,
            string text = "{Text}")
        {
            var dialog = MockDialog.Create(
                templateKey,
                d =>
                {
                    d.Text = text;

                    if (applicant is not null)
                        d.Context = new GuildApplicationReviewScript.ReviewContext(0, [], applicant);
                });

            CreateScript(dialog)
                .OnDisplaying(source);

            return dialog;
        }

        private Aisling Online(string name)
        {
            var aisling = MockAisling.Create(name: name);

            Mock.Get(aisling.Client)
                .SetupGet(c => c.Id)
                .Returns(aisling.Id);

            Clients.TryAdd(aisling.Client);

            return aisling;
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj
```

Expected: build errors naming `GuildApplicationReviewScript` and the constructors of `GuildMemberManagementScript` and `GuildDisbandScript`.

- [ ] **Step 3: Write the review script**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplicationReviewScript.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Extensions.Common;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.NLog.Logging.Definitions;
using Chaos.NLog.Logging.Extensions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts.Abstractions;
using Chaos.Services.GuildApplications;
using Chaos.Services.Other.Abstractions;
using Chaos.Services.Storage;
using Chaos.Storage.Abstractions;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.GuildScripts;

/// <summary>
///     The officers' side of guild applications, under Members at Quill and Aricin. Ranks with
///     <see cref="GuildPermission.Admit" /> read waiting applications and accept or decline them, even while the applicant is
///     offline. The applicant hears by letter either way, and at once when online and accepted.
/// </summary>
public class GuildApplicationReviewScript : GuildScriptBase
{
    public const string ACCEPTED_KEY = "generic_guild_applications_accepted";
    public const string DECLINED_KEY = "generic_guild_applications_declined";
    public const string INITIAL_KEY = "generic_guild_applications_initial";
    public const string VIEW_KEY = "generic_guild_applications_view";

    private const string LETTER_AUTHOR = "Aricin";
    private const string LETTER_SUBJECT = "Guild application";
    private const string MEMBERS_KEY = "generic_guild_members_initial";
    private const string NO_LONGER_WAITING = "That application is no longer waiting.";

    private readonly GuildApplicationService Applications;
    private readonly IChatFilterService ChatFilter;
    private readonly AislingFacadeCache FacadeCache;
    private readonly IStore<MailBox> MailStore;

    /// <inheritdoc />
    public GuildApplicationReviewScript(
        Dialog subject,
        IClientRegistry<IChaosWorldClient> clientRegistry,
        IStore<Guild> guildStore,
        IFactory<Guild> guildFactory,
        ILogger<GuildApplicationReviewScript> logger,
        GuildApplicationService applications,
        AislingFacadeCache facadeCache,
        IStore<MailBox> mailStore,
        IChatFilterService chatFilter)
        : base(
            subject,
            clientRegistry,
            guildStore,
            guildFactory,
            logger)
    {
        Applications = applications;
        FacadeCache = facadeCache;
        MailStore = mailStore;
        ChatFilter = chatFilter;
    }

    /// <summary>
    ///     "today", "1 day ago" or "N days ago"
    /// </summary>
    public static string DescribeAge(DateTime appliedAt, DateTime now)
    {
        var days = (int)Math.Floor((now - appliedAt).TotalDays);

        return days switch
        {
            <= 0 => "today",
            1    => "1 day ago",
            _    => $"{days} days ago"
        };
    }

    /// <inheritdoc />
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case INITIAL_KEY:
                OnDisplayingInitial(source);

                break;
            case VIEW_KEY:
                OnDisplayingView(source);

                break;
            case ACCEPTED_KEY:
                OnDisplayingAccepted(source);

                break;
            case DECLINED_KEY:
                OnDisplayingDeclined(source);

                break;
        }
    }

    /// <inheritdoc />
    public override void OnNext(Aisling source, byte? optionIndex = null)
    {
        if (optionIndex is null || !Subject.Template.TemplateKey.EqualsI(INITIAL_KEY))
            return;

        var context = Subject.Context as ReviewContext ?? new ReviewContext(0, [], null);
        var index = optionIndex.Value;

        if ((index >= 1) && (index <= context.Shown.Length))
        {
            Subject.Context = context with
            {
                Applicant = context.Shown[index - 1]
            };

            return;
        }

        var text = Subject.GetOptionText(index);

        if (text == GuildApplyScript.NEXT_PAGE)
            Subject.Context = context with
            {
                Page = context.Page + 1,
                Applicant = null
            };
        else if (text == GuildApplyScript.PREVIOUS_PAGE)
            Subject.Context = context with
            {
                Page = Math.Max(0, context.Page - 1),
                Applicant = null
            };
    }

    /// <summary>
    ///     "level 45 Priest", or null when the save can't be read. The facade returns the live aisling for an online player
    /// </summary>
    private string? DescribeApplicant(string applicant)
        => FacadeCache.Get(applicant) is { } aisling
            ? $"level {aisling.UserStatSheet.Level} {aisling.UserStatSheet.BaseClass}"
            : null;

    /// <summary>
    ///     Whether the applicant is in a guild now. A saved guild that no longer lists them (a kick while they were away)
    ///     doesn't count. An offline save can be up to an hour old; a stale "no" is harmless, because the next login clears
    ///     the applications of anyone in a guild
    /// </summary>
    private bool IsInAGuild(string applicant)
        => FacadeCache.Get(applicant) is { Guild: { } theirGuild } && theirGuild.HasMember(applicant);

    private void OnDisplayingAccepted(Aisling source)
    {
        if (!TryGetReviewingGuild(source, out var guild) || !TryGetWaiting(source, guild, out var applicant, out _))
            return;

        if (IsInAGuild(applicant))
        {
            Applications.ClearApplicant(applicant);
            Subject.Reply(source, $"{applicant} has already joined another guild.", INITIAL_KEY);

            return;
        }

        switch (Applications.Accept(guild.Name, applicant, source.Name))
        {
            case GuildAcceptResult.AlreadyAccepted:
                Subject.Reply(source, $"{applicant} was already accepted.", INITIAL_KEY);

                return;
            case GuildAcceptResult.NotFound:
                Subject.Reply(source, NO_LONGER_WAITING, INITIAL_KEY);

                return;
        }

        SendLetter(
            applicant,
            $"{guild.Name} accepted your guild application. To join, click the guild board outside the guild hall on Abel Port Way, or log in again to choose. The offer lasts {GuildApplicationService.Lifetime.Days} days.");

        ClientRegistry.FirstOrDefault(client => client.Aisling.Name.EqualsI(applicant))
                      ?.Aisling
                      .SendActiveMessage($"{guild.Name} accepted your application. Open the guild board on Abel Port Way to join.");

        Logger.WithTopics(Topics.Entities.Guild, Topics.Actions.Update)
              .WithProperty(guild)
              .WithProperty(source)
              .LogInformation(
                  "Aisling {@AislingName} accepted {Applicant}'s application to guild {@GuildName}",
                  source.Name,
                  applicant,
                  guild.Name);

        Subject.InjectTextParameters(applicant);
    }

    private void OnDisplayingDeclined(Aisling source)
    {
        if (!TryGetReviewingGuild(source, out var guild) || !TryGetWaiting(source, guild, out var applicant, out _))
            return;

        if (!Applications.Decline(guild.Name, applicant))
        {
            Subject.Reply(source, NO_LONGER_WAITING, INITIAL_KEY);

            return;
        }

        SendLetter(applicant, $"{guild.Name} declined your guild application.");

        Logger.WithTopics(Topics.Entities.Guild, Topics.Actions.Update)
              .WithProperty(guild)
              .WithProperty(source)
              .LogInformation(
                  "Aisling {@AislingName} declined {Applicant}'s application to guild {@GuildName}",
                  source.Name,
                  applicant,
                  guild.Name);

        Subject.InjectTextParameters(applicant);
    }

    private void OnDisplayingInitial(Aisling source)
    {
        if (!TryGetReviewingGuild(source, out var guild))
            return;

        var waiting = Applications.WaitingFor(guild.Name);

        if (waiting.Count == 0)
        {
            Subject.Reply(source, "No one has applied.", MEMBERS_KEY);

            return;
        }

        var context = Subject.Context as ReviewContext;
        var pageCount = (waiting.Count + GuildApplyScript.PAGE_SIZE - 1) / GuildApplyScript.PAGE_SIZE;
        var page = Math.Clamp(context?.Page ?? 0, 0, pageCount - 1);

        var shown = waiting.Skip(page * GuildApplyScript.PAGE_SIZE)
                           .Take(GuildApplyScript.PAGE_SIZE)
                           .ToList();

        var now = Applications.NowUtc;
        var index = 0;

        //the applicants first, then paging, all before the template's own options (Back)
        foreach (var entry in shown)
        {
            var levelAndClass = DescribeApplicant(entry.Applicant);
            var age = DescribeAge(entry.Application.AppliedAt, now);

            Subject.InsertOption(
                index++,
                levelAndClass is null ? $"{entry.Applicant} - {age}" : $"{entry.Applicant} - {levelAndClass} - {age}",
                VIEW_KEY);
        }

        if (page + 1 < pageCount)
            Subject.InsertOption(index++, GuildApplyScript.NEXT_PAGE, INITIAL_KEY);

        if (page > 0)
            Subject.InsertOption(index, GuildApplyScript.PREVIOUS_PAGE, INITIAL_KEY);

        Subject.Context = new ReviewContext(
            page,
            shown.Select(entry => entry.Applicant)
                 .ToArray(),
            null);
    }

    private void OnDisplayingView(Aisling source)
    {
        if (!TryGetReviewingGuild(source, out var guild) || !TryGetWaiting(source, guild, out var applicant, out var application))
            return;

        var levelAndClass = DescribeApplicant(applicant);
        var age = DescribeAge(application.AppliedAt, Applications.NowUtc);

        var text = levelAndClass is null ? $"{applicant}, applied {age}." : $"{applicant}, {levelAndClass}, applied {age}.";

        if (application.Note.Length > 0)
            text += $"\n\nNote: {ChatFilter.FilterMessage(application.Note)}";

        Subject.InjectTextParameters(text);
    }

    private void SendLetter(string applicant, string message)
    {
        try
        {
            var mailbox = MailStore.Load(applicant);
            mailbox.Post(LETTER_AUTHOR, LETTER_SUBJECT, message);
            MailStore.Save(mailbox);
        } catch (Exception e)
        {
            Logger.WithTopics(Topics.Entities.Guild, Topics.Entities.Mail)
                  .LogWarning(e, "Failed to send a guild application letter to {Applicant}", applicant);
        }
    }

    /// <summary>
    ///     Whether the member may review now. Checked on every screen, because the leader can switch Admit off while an officer
    ///     is partway through
    /// </summary>
    private bool TryGetReviewingGuild(Aisling source, [MaybeNullWhen(false)] out Guild guild)
    {
        if (!IsInGuild(source, out guild, out _))
        {
            Subject.Reply(source, "You are not in a guild.", "top");

            return false;
        }

        if (guild.HasPermission(source.Name, GuildPermission.Admit))
            return true;

        Subject.Reply(source, "Your rank can't admit new members.", MEMBERS_KEY);

        return false;
    }

    /// <summary>
    ///     The picked applicant's waiting application. False (with a reply) when it was answered, withdrawn or expired meanwhile
    /// </summary>
    private bool TryGetWaiting(
        Aisling source,
        Guild guild,
        [MaybeNullWhen(false)] out string applicant,
        [MaybeNullWhen(false)] out GuildApplication application)
    {
        applicant = (Subject.Context as ReviewContext)?.Applicant;
        application = null;

        if (applicant is not null)
        {
            var name = applicant;

            application = Applications.WaitingFor(guild.Name)
                                      .FirstOrDefault(entry => entry.Applicant.EqualsI(name))
                                      ?.Application;

            if (application is not null)
                return true;
        }

        Subject.Reply(source, NO_LONGER_WAITING, INITIAL_KEY);

        return false;
    }

    /// <summary>
    ///     What the list showed and which applicant the officer picked, carried from screen to screen in
    ///     <see cref="Dialog.Context" />
    /// </summary>
    /// <param name="Page">
    ///     The page of the list, from 0
    /// </param>
    /// <param name="Shown">
    ///     The applicant names the list's first options stand for, in option order
    /// </param>
    /// <param name="Applicant">
    ///     The applicant the officer picked, once they have
    /// </param>
    public sealed record ReviewContext(int Page, string[] Shown, string? Applicant);
}
```

- [ ] **Step 4: Add the Applications option to the Members menu**

In `worktrees/guild-applications-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs`:

1. `replace_content` (literal). Needle `using Chaos.Scripting.DialogScripts.Temuair.GuildScripts.Abstractions;`, replacement (two lines):

```text
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts.Abstractions;
using Chaos.Services.GuildApplications;
```

2. `replace_content` (regex). Needle `public class GuildMemberManagementScript : GuildScriptBase\r?\n\{`, replacement (three lines):

```text
public class GuildMemberManagementScript : GuildScriptBase
{
    private readonly GuildApplicationService Applications;
```

3. Read `GuildMemberManagementScript/GuildMemberManagementScript` with `find_symbol` (`include_body=true`), then `replace_symbol_body`:

```csharp
    public GuildMemberManagementScript(
        Dialog subject,
        IClientRegistry<IChaosWorldClient> clientRegistry,
        IStore<Guild> guildStore,
        IFactory<Guild> guildFactory,
        ILogger<GuildMemberManagementScript> logger,
        GuildApplicationService applications)
        : base(
            subject,
            clientRegistry,
            guildStore,
            guildFactory,
            logger)
        => Applications = applications;
```

4. `replace_content` (literal). Needle:

```text
        Subject.AddOption("Roster", "generic_guild_members_roster_initial");
```

Replacement (keep the blank line before the new comment):

```text
        Subject.AddOption("Roster", "generic_guild_members_roster_initial");

        //applications from the board on Abel Port Way. Answering one doesn't need the applicant nearby, so unlike Admit it is
        //offered at both Quill and Aricin
        if (guild.HasPermission(source.Name, GuildPermission.Admit))
        {
            var waiting = Applications.CountWaiting(guild.Name);

            Subject.AddOption(waiting > 0 ? $"Applications ({waiting})" : "Applications", GuildApplicationReviewScript.INITIAL_KEY);
        }
```

- [ ] **Step 5: Clear applications on disband**

In `worktrees/guild-applications-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs`:

1. `replace_content` (literal). Needle `using Chaos.Services.GuildCloak;`, replacement (two lines):

```text
using Chaos.Services.GuildApplications;
using Chaos.Services.GuildCloak;
```

2. `replace_content` (literal). Needle `    private readonly GuildCloakService GuildCloaks;`, replacement (two lines):

```text
    private readonly GuildApplicationService GuildApplications;
    private readonly GuildCloakService GuildCloaks;
```

3. Read `GuildDisbandScript/GuildDisbandScript` with `find_symbol` (`include_body=true`), then `replace_symbol_body`:

```csharp
    public GuildDisbandScript(
        Dialog subject,
        IClientRegistry<IChaosWorldClient> clientRegistry,
        IStore<Guild> guildStore,
        IFactory<Guild> guildFactory,
        ILogger<GuildDisbandScript> logger,
        IStorage<GuildHouseState> guildHouseStateStorage,
        GuildCloakService guildCloaks,
        GuildApplicationService guildApplications)
        : base(
            subject,
            clientRegistry,
            guildStore,
            guildFactory,
            logger)
    {
        GuildHouseStateStorage = guildHouseStateStorage;
        GuildCloaks = guildCloaks;
        GuildApplications = guildApplications;
    }
```

4. `replace_content` (literal). Needle `        GuildCloaks.RemoveGuild(guildName);`, replacement (two lines):

```text
        GuildCloaks.RemoveGuild(guildName);
        GuildApplications.RemoveGuild(guildName);
```

- [ ] **Step 6: Update the two existing test files**

In `worktrees/guild-applications-server/Tests/Chaos.Tests/GuildPermissions/GuildMemberPermissionTests.cs`:

1. `replace_content` (literal). Needle `using Chaos.Testing.Infrastructure.Mocks;`, replacement (two lines):

```text
using Chaos.Testing.Infrastructure.Mocks;
using Chaos.Tests.GuildApplications;
```

2. `replace_content` (literal). Needle:

```text
            new Mock<ILogger<GuildMemberManagementScript>>().Object).OnDisplaying(source);
```

Replacement:

```text
            new Mock<ILogger<GuildMemberManagementScript>>().Object,
            GuildApplicationTestSupport.CreateService()).OnDisplaying(source);
```

3. `replace_content` (literal). Needle `            .Equal("Roster", "Admit", "Kick");`, replacement:

```text
            .Equal("Roster", "Applications", "Admit", "Kick");
```

4. `replace_content` (literal). Needle `            .Equal("Roster", "Promote", "Demote", "Kick");`, replacement:

```text
            .Equal("Roster", "Applications", "Promote", "Demote", "Kick");
```

5. `replace_content` (literal). Needle `            .Equal("Roster", "Kick");`, replacement:

```text
            .Equal("Roster", "Applications", "Kick");
```

In `worktrees/guild-applications-server/Tests/Chaos.Tests/GuildCloak/GuildCloakScriptTests.cs`:

1. `replace_content` (literal). Needle `using Chaos.Testing.Infrastructure.Mocks;`, replacement (two lines):

```text
using Chaos.Testing.Infrastructure.Mocks;
using Chaos.Tests.GuildApplications;
```

2. `replace_content` (regex). Needle `            houseStorage\.Object,\r?\n            guildCloaks\);`, replacement (three lines):

```text
            houseStorage.Object,
            guildCloaks,
            GuildApplicationTestSupport.CreateService());
```

If that regex matches more than once, stop and report: it is expected only in the disband test.

- [ ] **Step 7: Run the tests**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildApplications/*/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/*/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildCloak/*/*" --no-ansi
```

Expected: every test in all three runs passes (the first includes the 13 new review tests).

- [ ] **Step 8: Write the 4 dialog templates**

Create each file under `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications/`, named `<templateKey>.json`.

`generic_guild_applications_initial.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_members_initial",
      "optionText": "Back"
    }
  ],
  "scriptKeys": [
    "GuildApplicationReview"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_applications_initial",
  "text": "Waiting applications, oldest first:",
  "type": "Menu"
}
```

`generic_guild_applications_view.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_applications_accepted",
      "optionText": "Accept"
    },
    {
      "dialogKey": "generic_guild_applications_declined",
      "optionText": "Decline"
    },
    {
      "dialogKey": "generic_guild_applications_initial",
      "optionText": "Back"
    }
  ],
  "scriptKeys": [
    "GuildApplicationReview"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_applications_view",
  "text": "{Text}",
  "type": "Menu"
}
```

`generic_guild_applications_accepted.json`:

```json
{
  "contextual": true,
  "nextDialogKey": "generic_guild_applications_initial",
  "options": [],
  "scriptKeys": [
    "GuildApplicationReview"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_applications_accepted",
  "text": "You accepted {Name}. They'll get a letter and can join within 14 days.",
  "type": "Normal"
}
```

`generic_guild_applications_declined.json`:

```json
{
  "contextual": true,
  "nextDialogKey": "generic_guild_applications_initial",
  "options": [],
  "scriptKeys": [
    "GuildApplicationReview"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_applications_declined",
  "text": "You declined {Name}. They'll get a letter.",
  "type": "Normal"
}
```

- [ ] **Step 9: Check the templates parse**

```bash
python - <<'EOF'
import json, glob, os
folder = r'C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications'
files = sorted(glob.glob(os.path.join(folder, 'generic_guild_applications_*.json')))
for f in files:
    d = json.load(open(f, encoding='utf-8'))
    assert d['templateKey'] + '.json' == os.path.basename(f), f
    assert d['scriptKeys'] == ['GuildApplicationReview'], f
    assert all(ord(c) < 128 for c in json.dumps(d, ensure_ascii=False)), f
print(len(files))
EOF
```

Expected: `4`.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplicationReviewScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs", "Tests/Chaos.Tests/GuildApplications/GuildApplicationReviewScriptTests.cs", "Tests/Chaos.Tests/GuildPermissions/GuildMemberPermissionTests.cs", "Tests/Chaos.Tests/GuildCloak/GuildCloakScriptTests.cs", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications/generic_guild_applications_*.json"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildApplications/*/*\" --no-ansi", "acceptanceCriteria": ["Applications (N) after Roster for Admit ranks; plain Applications when none; none without Admit", "list lines with level, class and age oldest first; picking sets ReviewContext.Applicant; empty replies No one has applied.", "view shows applicant line and filtered note", "accept makes offer, one letter from Aricin 'Guild application', mailbox saved once; online applicant gets the active message", "applicant still listed by another guild is refused and cleared, no letter; unlisted saved guild doesn't count", "decline removes and sends the declined letter", "rank without Admit refused; vanished application replies no longer waiting", "disband removes the guild's applications", "GuildPermissions and GuildCloak tests pass; 4 templates parse with GuildApplicationReview"], "modelTier": "standard"}
```

---

### Task 6: The login steps

**Goal:** At login, a guild member's applications are cleared, a guildless player with offers sees the offers menu (standing in for the Terminus menu when that is due), and members who can admit see the waiting count.

**Files:**
- Modify: `SRV/Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` (two usings, one field, the constructor, `OnLogin`, two new private methods after `OnLogin`)

**Acceptance Criteria:**
- [ ] `DefaultAislingScript` takes `GuildApplicationService` as its last constructor parameter
- [ ] `OnLogin` shows `generic_guild_apply_offers` from a `terminus` merchant when the Terminus menu is due and offers exist, from a `blank_merchant` when only offers exist, and the Terminus menu exactly as before when there are no offers
- [ ] A member in a guild has their applications cleared at login
- [ ] After the message of the day, a member whose rank has `Admit` gets `GuildApplicationService.WaitingNotice(count)` as an active message when it isn't null
- [ ] The server builds and the full `Chaos.Tests.GuildApplications` run still passes

**Verify:** `dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Chaos/Chaos.csproj` → `Build succeeded`

**Steps:**

- [ ] **Step 1: Add the usings and the field**

In `worktrees/guild-applications-server/Chaos/Scripting/AislingScripts/DefaultAislingScript.cs`, three `replace_content` (literal) edits.

Needle `using Chaos.Scripting.DialogScripts.Temuair.Class_Related;`, replacement (two lines):

```text
using Chaos.Scripting.DialogScripts.Temuair.Class_Related;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts;
```

Needle `using Chaos.Services.Factories.Abstractions;`, replacement (two lines):

```text
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.GuildApplications;
```

Needle `    private readonly IEffectFactory EffectFactory;`, replacement (two lines):

```text
    private readonly IEffectFactory EffectFactory;
    private readonly GuildApplicationService GuildApplications;
```

- [ ] **Step 2: Take the service in the constructor**

Read `DefaultAislingScript/DefaultAislingScript` with `find_symbol` (`include_body=true`). Then make two `replace_content` (literal) edits inside it.

Needle `        IChatFilterService chatFilter)`, replacement (two lines):

```text
        IChatFilterService chatFilter,
        GuildApplicationService guildApplications)
```

Needle `        ChatFilter = chatFilter;
    }`, replacement (three lines):

```text
        ChatFilter = chatFilter;
        GuildApplications = guildApplications;
    }
```

If the second needle matches more than once in the file, use regex mode with the needle `        ChatFilter = chatFilter;\r?\n    \}` and check the match is inside the constructor. If `IChatFilterService chatFilter)` matches more than once, stop and report.

- [ ] **Step 3: Replace `OnLogin`**

Read `DefaultAislingScript/OnLogin` with `find_symbol` (`include_body=true`). It must still match the body below except for the lines this step changes; if it has other differences, stop and report. Then `replace_symbol_body` on `DefaultAislingScript/OnLogin`:

```csharp
    public override void OnLogin()
    {
        MigrateMountsAndCloaks(Subject);
        MigrateDefaultFaceSprite(Subject);

        var terminusDue = Subject.Trackers.LastLogout is { } lastLogout
                          && (lastLogout.AddHours(2) <= DateTime.UtcNow)
                          && Subject.Trackers.Enums.HasValue(TutorialQuestStage.CompletedTutorial);

        //a guild's accepted offers come first. Only one dialog can be open, so when the welcome-back menu is also due the
        //offers menu is shown from the Terminus, and its "Decide later" goes on to the welcome-back menu
        if (!ShowGuildOffers(terminusDue) && terminusDue)
        {
            var merch = MerchantFactory.Create("terminus", Subject.MapInstance, Subject);
            var dialog = DialogFactory.Create("terminus_homeoptions", merch);
            dialog.Display(Subject);
        }

        if (Subject.Guild != null)
            foreach (var player in ClientRegistry)
                if (player.Aisling.Guild == Subject.Guild)
                    player.Aisling.SendServerMessage(
                        ServerMessageType.ActiveMessage,
                        $"{Subject.Guild.Name} member {Subject.Name} has appeared online.");

        //after the online notices, so the message is the last guild line the member sees
        Subject.Guild?.SendMessageOfTheDay(Subject, ChatFilter);

        SendWaitingApplicationsNotice();

        //so a logout lost to a server restart then costs only that session
        Subject.Guild?.RecordLastSeen(Subject.Name, DateTime.UtcNow);

        ApplyPendingPositionRemovals();

        //after the pending removals, so an Apostle removed while offline drops off the temple board now
        ClergyRoster.Sync(Subject);
        PollManager.SendSnapshotTo(Subject);
    }
```

- [ ] **Step 4: Add the two helpers**

`insert_after_symbol` on `DefaultAislingScript/OnLogin`:

```csharp

    /// <summary>
    ///     Shows the offers menu to a guildless player whom a guild has accepted, from the Terminus when its welcome-back menu
    ///     is due. A player in a guild has no use for their applications, so they are cleared. True when the menu was shown
    /// </summary>
    private bool ShowGuildOffers(bool terminusDue)
    {
        if (Subject.Guild is not null)
        {
            GuildApplications.ClearApplicant(Subject.Name);

            return false;
        }

        if (GuildApplications.OffersFor(Subject.Name).Count == 0)
            return false;

        var merchant = MerchantFactory.Create(terminusDue ? "terminus" : "blank_merchant", Subject.MapInstance, Subject);
        var dialog = DialogFactory.Create(GuildApplyScript.OFFERS_KEY, merchant);
        dialog.Display(Subject);

        return true;
    }

    /// <summary>
    ///     "3 guild applications are waiting..." for a member whose rank can admit, when anything is waiting
    /// </summary>
    private void SendWaitingApplicationsNotice()
    {
        if (Subject.Guild is not { } guild || !guild.HasPermission(Subject.Name, GuildPermission.Admit))
            return;

        if (GuildApplicationService.WaitingNotice(GuildApplications.CountWaiting(guild.Name)) is { } notice)
            Subject.SendActiveMessage(notice);
    }
```

- [ ] **Step 5: Build and re-run the application tests**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Chaos/Chaos.csproj
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildApplications/*/*" --no-ansi
```

Expected: `Build succeeded`, and every test passes.

```json:metadata
{"files": ["Chaos/Scripting/AislingScripts/DefaultAislingScript.cs"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Chaos/Chaos.csproj", "acceptanceCriteria": ["constructor takes GuildApplicationService last", "OnLogin shows the offers menu from terminus or blank_merchant, and the Terminus menu unchanged when no offers", "guild member's applications cleared at login", "Admit members get WaitingNotice after the message of the day", "server builds; GuildApplications tests pass"], "modelTier": "mechanical"}
```

---

### Task 7: Commit the full implementation

**Goal:** Every test passes except the two known failures, the ideas doc marks idea 43 built, and the server branch, the Unora branch and this plan (with the amended spec) are each committed once.

**Files:**
- Modify: `UNO/docs/guild-hall-ideas.md`
- Commit: every file listed in Tasks 1–6, in `SRV` and `UNO`
- Commit: `Chaos.Client/docs/superpowers/plans/2026-09-25-guild-applications.md`, its `.tasks.json`, and `Chaos.Client/docs/superpowers/specs/2026-09-25-guild-applications-design.md` (on `Chaos.Client` `main`, by path)

**Acceptance Criteria:**
- [ ] The full `Chaos.Tests` run fails only `GiveAbility` and `OnItemDroppedOn` (stackable)
- [ ] `docs/guild-hall-ideas.md` says six ideas were built on 2026-09-25, describes applications under "What guild halls do today", marks idea 43 built, and lists the "Admitting from Guild House" thread as built
- [ ] `SRV` has one new commit on `feat/guild-applications` with only this plan's server files
- [ ] `UNO` has one new commit on `feat/guild-applications` with only the map, the reactors file, the 16 dialogs and the ideas doc, and no `Custom Client Mods` build output
- [ ] The plan, its tasks file and the spec are committed on `Chaos.Client` `main`, with nothing else in that commit

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server log --oneline -1` → the guild applications commit

**Steps:**

- [ ] **Step 1: Run the full server test suite**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi
```

Expected: every test passes except `GiveAbility` and `OnItemDroppedOn` (stackable). Any other failure: stop and fix it in the task that caused it.

- [ ] **Step 2: Update the ideas doc**

In `C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora/docs/guild-hall-ideas.md`, make four edits with the Edit tool. Each is an exact old line and new text.

Edit 1. Old:

```text
Five ideas were built on 2026-09-25: the guild cloak (idea 15), guild tuition (idea 32), guild rank permissions (idea 51), the message of the day (idea 44) and last seen on the roster (idea 45).
```

New:

```text
Six ideas were built on 2026-09-25: the guild cloak (idea 15), guild tuition (idea 32), guild rank permissions (idea 51), the message of the day (idea 44), last seen on the roster (idea 45) and guild applications (idea 43).
```

Edit 2. Old:

```text
- The roster shows each member as online, or how many hours or days ago they were last seen.
```

New (two lines):

```text
- The roster shows each member as online, or how many hours or days ago they were last seen.
- Players without a guild apply from the guild board outside the guild hall on Abel Port Way. Ranks that can admit accept or decline at Quill or Aricin, even while the applicant is offline. A player accepted by several guilds picks one at login or at the board.
```

Edit 3. Old:

```text
43. **Guild applications.** Players apply from a board in town, and leaders accept them later, even while the player is offline. Today both people must stand in the tavern to admit someone. *Medium.*
```

New:

```text
43. **Guild applications.** Players apply from a board in town, and leaders accept them later, even while the player is offline. Today both people must stand in the tavern to admit someone. *Medium.* **Built 2026-09-25** (branch `feat/guild-applications`); spec and plan in `Chaos.Client/docs/superpowers/`.
```

Edit 4. Old:

```text
- **Guild tax rates opt in/out:** a dev promised guild-funded training there, and idea 32 delivers it. The thread also asks about the tax rules, which did not change.
```

New (two lines):

```text
- **Guild tax rates opt in/out:** a dev promised guild-funded training there, and idea 32 delivers it. The thread also asks about the tax rules, which did not change.
- **Admitting from Guild House (Dream):** idea 43. Officers now accept applications at Quill in the hall, with nobody else present. The thread can be closed after the in-game check.
```

- [ ] **Step 3: Commit the server worktree**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-applications-server
git status --short
git add -- Chaos/Models/World/GuildApplicationState.cs Chaos/Services/GuildApplications/GuildApplicationService.cs Chaos/SerializationContext.cs Chaos/Extensions/ServiceCollectionExtensions.cs Chaos/Collections/Guild.cs Chaos/Scripting/ReactorTileScripts/Temauir/GuildApplicationBoardScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplyScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplicationReviewScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs Chaos/Scripting/AislingScripts/DefaultAislingScript.cs Tests/Chaos.Tests/GuildApplications Tests/Chaos.Tests/GuildPermissions/GuildMemberPermissionTests.cs Tests/Chaos.Tests/GuildCloak/GuildCloakScriptTests.cs
git status --short
git commit -F - <<'EOF'
Add guild applications: apply at a board, answer while offline

Players without a guild apply to guilds from a board outside the guild
hall on Abel Port Way, with an optional note. Ranks with the admit
permission see "Applications (N)" under Members at Quill and Aricin and
accept or decline, even while the applicant is offline. The applicant
gets a letter either way. A player accepted by several guilds picks one
from a menu at login or at the board; joining withdraws the rest.

Up to 5 open applications per player, one per guild; both waiting
applications and offers expire after 14 days. Everything is kept in one
new file, GuildApplicationState.json; guild files don't change.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

Before committing, the second `git status --short` must show nothing staged outside the list above. If `appsettings.json` or `launchSettings.json` show as modified, leave them unstaged.

- [ ] **Step 4: Commit the Unora worktree**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-applications-unora
git status --short
git add -- Data/Configuration/MapData/lod180.map Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_port_way/reactors.json "Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications" docs/guild-hall-ideas.md
git status --short
git commit -F - <<'EOF'
Add the guild applications board and dialogs

A wooden notice board stands left of the guild hall door on Abel Port
Way (lod180.map tile 5,19). The board's tile and the two hall-wall
tiles behind it open the applications menu.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

Leave any modified `Custom Client Mods/**/obj` files unstaged. They are build output.

- [ ] **Step 5: Commit the plan and spec on Chaos.Client `main`**

`docs/` is gitignored in Chaos.Client, so use `-f`. Commit by path so nothing else in the shared checkout is included:

```bash
cd /c/Users/Michael/Documents/GitHub/Chaos.Client
git add -f -- docs/superpowers/plans/2026-09-25-guild-applications.md docs/superpowers/plans/2026-09-25-guild-applications.md.tasks.json docs/superpowers/specs/2026-09-25-guild-applications-design.md
git commit -F - -- docs/superpowers/plans/2026-09-25-guild-applications.md docs/superpowers/plans/2026-09-25-guild-applications.md.tasks.json docs/superpowers/specs/2026-09-25-guild-applications-design.md <<'EOF'
Add the guild applications implementation plan, and amend the spec

The board gets three clickable spots: the client sends a click only
for tiles with rendered art, and most of the board lies over the hall
wall behind it.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 6: Report the in-game check**

Merging, and pointing Chaos.Client's `Chaos-Server` submodule at the merged master, come next (superpowers-extended-cc:finishing-a-development-branch). They aren't part of this task. Report the check for the user to run in game after the merge and deploy (server and Unora together), with two accounts: an officer and a guildless alt:

1. Walk to Abel Port Way. The board stands left of the guild hall door, and you can't walk onto it. Clicking its lower half opens the menu, and so does the hall wall just behind it.
2. As the alt, apply to guild A with a note, and to guild B without one.
3. An online officer of A sees the "has applied" line.
4. As A's officer, open Quill, Members, Applications (1). The alt shows with level, class and the note. Accept. The alt (online) gets the line and a letter.
5. Log the alt out. As B's officer, accept at Aricin. Log the alt in: the offers menu lists A and B. Pick Decide later.
6. At the board, "Join a guild (2 offers)" shows. Join B. A's offer is gone, and B's online members see "has joined the guild, accepted by ...".
7. With a second alt, apply and have an officer decline. The letter arrives.
8. Give an alt an offer and keep it offline for over two hours (or set its `LastLogout` back). On login the offers menu shows first, and Decide later opens the Terminus menu.
9. A rank without admit sees no Applications option. A member whose rank can admit sees the waiting count at login.
10. Restart the server. Waiting applications and offers are still there.
11. Disband a test guild that has a waiting application. It vanishes from the alt's list.

```json:metadata
{"files": ["UNO:docs/guild-hall-ideas.md"], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-applications-server log --oneline -1", "acceptanceCriteria": ["full Chaos.Tests run fails only GiveAbility and OnItemDroppedOn (stackable)", "ideas doc: six built, applications bullet, idea 43 built, Admitting from Guild House thread listed", "one SRV commit with only this plan's server files", "one UNO commit with only the map, reactors, 16 dialogs and ideas doc, no Custom Client Mods obj output", "plan, tasks file and spec committed alone on Chaos.Client main"], "modelTier": "mechanical"}
```
