# Guild Roster Last-Seen and Message of the Day Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Guild members see a message of the day as one guild chat line at login, and the guild roster shows when each member was last seen.

**Architecture:** `Guild` gains a `MessageOfTheDay` record and a name-to-time `LastSeenTimes` list, both saved in `guild.json` through `GuildSchema` and `GuildMapperProfile`. A new eighth rank permission, `SetMessageOfTheDay`, decides who may change the message. A new `GuildMessageOfTheDayScript` runs six Unora dialogs at Quill and Aricin. `DefaultAislingScript` sends the message at login and records the time at logout. `GuildMemberRosterScript` prints each member's status through a small `GuildLastSeen.Describe` helper. For members with no stored time, it reads their save once through the existing `AislingFacadeCache`.

**Tech Stack:** C# 14 / .NET 10, Chaos-Server dialog scripts, `IStore<Guild>` JSON storage with `System.Text.Json`, TUnit + FluentAssertions + Moq, Unora JSON dialog templates.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-09-25-guild-roster-and-message-of-the-day-design.md` (committed on `main` as `4ff64ef`).

## Global Constraints

- **Work only in the two worktrees from Task 0.** Never edit, stage, stash, reset or switch branches in the shared checkouts (`C:/Users/Michael/Documents/GitHub/Chaos.Client`, its `Chaos-Server` submodule, `C:/Users/Michael/Documents/GitHub/Unora`). Other Claude sessions work in them. Never touch the other folders under `C:/Users/Michael/Documents/GitHub/worktrees/`.
  - `SRV` = `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server` (Chaos-Server, branch `feat/guild-roster-motd` from `master`)
  - `UNO` = `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-unora` (Unora, branch `feat/guild-roster-motd` from `main`)
- **Do not commit** in Tasks 0–5. Leave all changes in the worktrees. Task 6 makes one commit per repo (at-end strategy).
- **Test projects are TUnit executables.** Use `dotnet run --project ... -- --treenode-filter "..." --no-ansi`, never `dotnet test`. The filter pattern is `/Assembly/Namespace/Class/Method`.
- **Two server tests already fail on master:** `GiveAbility` and `OnItemDroppedOn` (stackable). Leave them alone. Every other test must pass.
- **If a build fails with MSB3027** (a file is locked), something is running from that output folder. Stop and report. Don't kill any process.
- **Serena for C#.** Read and edit C# files with Serena's tools, as the user's global CLAUDE.md requires. Serena paths are relative to `C:/Users/Michael/Documents/GitHub`, so a server file is `worktrees/guild-motd-server/Chaos/...`. Before a `replace_symbol_body`, read the symbol with `find_symbol` and `include_body=true`. For C# methods, Serena's body includes attributes such as `[Test]` but not the `///` doc comment, so the replacement code below starts at the attribute or signature. Create brand-new C# files with the Write tool. JSON and Markdown files use the built-in Read/Edit/Write tools.
- **Line endings.** A fresh worktree may check out CRLF (`core.autocrlf` is `true`). If a multi-line literal `replace_content` needle doesn't match, retry the same needle in regex mode with each line break written as `\r?\n` and the other regex characters escaped. When a replacement adds lines, pass real line breaks in the replacement string, never the two characters `\n`.
- **Never stage** `Chaos/appsettings.json`, `launchSettings.json`, or anything under `UNO/Custom Client Mods/**/obj`.
- **The game font is 6×12 ASCII.** Use only ASCII in dialog text and messages. No em dashes, no curly quotes.
- **Test namespaces.** New tests go in `Tests/Chaos.Tests/GuildMotd/` (namespace `Chaos.Tests.GuildMotd`) and `Tests/Chaos.Tests/GuildRoster/` (namespace `Chaos.Tests.GuildRoster`). Don't name a folder `GuildMessageOfTheDay` or `GuildLastSeen`: the namespace would hide the types of the same name.
- **Exact values:**

  | Value | Setting |
  |---|---|
  | Permission | `GuildPermission.SetMessageOfTheDay = 128`, label `Set the message of the day`. Last in every tier's `ForTier` list. **Not** in `COUNCIL_DEFAULT`. |
  | Longest message | `Guild.MESSAGE_OF_THE_DAY_MAX_LENGTH = 150`; the text box's `textBoxLength` is `150` |
  | Chat line | `<guild name> message of the day: <text>`, sent as `ServerMessageType.GuildChat` to one player at a time |
  | Menu option | `Message of the Day` → `generic_guild_motd_initial`, right before `Members`, for every guild member at Quill and at Aricin |
  | Script key | `GuildMessageOfTheDay` (class `GuildMessageOfTheDayScript`) |
  | Dialog keys | `generic_guild_motd_initial`, `generic_guild_motd_change`, `generic_guild_motd_change_confirmation`, `generic_guild_motd_change_accepted`, `generic_guild_motd_clear_confirmation`, `generic_guild_motd_clear_accepted` |
  | First screen | `Your guild has no message of the day.` or `Set by <name> on <yyyy-MM-dd> (UTC):` + blank line + the text |
  | Options on the first screen | `Change`, then `Clear` when a message exists, only for the leader and ranks with the switch; then the template's `Back` |
  | Replies | `You are not in a guild.` / `Your rank can't change the message of the day.` / `Nothing was changed.` / `That message is too long. Keep it to 150 characters.` |
  | Roster header | `Members: <count> (<online> online)` |
  | Roster line | `<name> - <status>`, where status is `online`, `unknown`, `under an hour ago`, `1 hour ago`, `N hours ago`, `1 day ago` or `N days ago` |

**User decisions (already made):**
- This is sub-project 1 of the 2026-09-25 guild batch: ideas 44 (message of the day) and 45 (last seen on the roster). Guild applications and the librarian come later, each with its own spec.
- The message of the day shows as one guild chat line at login.
- Who changes it: the leader, plus any rank the leader gives a new eighth permission switch. The switch starts off for every rank, Council included.
- Every member sees last-seen times on the normal roster.
- Last-seen times live in `guild.json`, updated at logout. A member with no entry is read once from their save (approach 1).
- The format is relative (`online`, hours, days). No exact dates. `under an hour ago` was added in the spec review.
- Messages are at most 150 characters and trimmed. A blank entry cancels. A confirm screen shows the new text. Clearing is its own option.
- The option sits just before `Members`, at both Quill and Aricin.
- No client change and no `CLIENT_VERSION` bump.

**Changes from the spec (decided while planning):**
1. `GuildMessageOfTheDay.ToChatLine(guildName)` builds the line, and the guild sends it with `SendMessageOfTheDay(aisling)` and `BroadcastMessageOfTheDay()`. The login hook and the dialog both call these, so the format lives in one place and is unit-tested without `DefaultAislingScript`.
2. `Guild.GetLastSeen()` returns a copy of all times, for the mapper to save.
3. The private list on `Guild` is named `LastSeenTimes`, so it doesn't read like the schema property.
4. When a save can't be read, `AislingFacadeCache` remembers the failure for an hour (sliding). So the roster tries again once the cache forgets it, not on literally the next view. The tests use a fresh cache per view to show the guild itself stores nothing.
5. A missing text argument counts as blank ("Nothing was changed."), not as unknown input.

## File map

| Repo | File | Responsibility |
|---|---|---|
| SRV | `Chaos.DarkAges/Definitions/Enums.cs` | `GuildPermission.SetMessageOfTheDay` |
| SRV | `Chaos/Collections/GuildPermissionRules.cs` | offer the switch to tiers 1–3, its label |
| SRV | `Chaos/Collections/GuildMessageOfTheDay.cs` (new) | the message record and its chat line |
| SRV | `Chaos/Collections/GuildLastSeen.cs` (new) | the roster status text |
| SRV | `Chaos/Collections/Guild.cs` | message and last-seen state and methods |
| SRV | `Chaos.Schemas/Guilds/GuildMessageOfTheDaySchema.cs` (new) | saved message |
| SRV | `Chaos.Schemas/Guilds/GuildSchema.cs` | `MessageOfTheDay`, `LastSeen` |
| SRV | `Chaos/Services/MapperProfiles/GuildMapperProfile.cs` | map both fields both ways |
| SRV | `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` | login line, logout time |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMessageOfTheDayScript.cs` (new) | the six dialogs |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs` | the menu option |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberRosterScript.cs` | status lines, online count, one-time save read |
| SRV | `Tests/Chaos.Tests/GuildPermissions/GuildPermissionDataTests.cs`, `GuildPermissionsScriptTests.cs` | updated lists, new switch tests |
| SRV | `Tests/Chaos.Tests/GuildMotd/*.cs` (new), `Tests/Chaos.Tests/GuildRoster/*.cs` (new) | new tests |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildMessageOfTheDay/*.json` (new, 6 files) | the dialogs |
| UNO | `docs/guild-hall-ideas.md` | mark ideas 44 and 45 built |

---

### Task 0: Create the two worktrees

**Goal:** Isolated `feat/guild-roster-motd` branches for the server and Unora, so no shared checkout is touched.

**Files:**
- Create: worktrees `SRV` and `UNO` (see Global Constraints)

**Acceptance Criteria:**
- [ ] `git -C <each worktree> branch --show-current` prints `feat/guild-roster-motd`
- [ ] `SRV/Tests/Chaos.Tests` builds, and `GuildTests` and the `GuildPermissions` tests pass before any change

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server branch --show-current` → `feat/guild-roster-motd`

**Steps:**

- [ ] **Step 1: Check the worktrees don't exist yet**

```bash
ls /c/Users/Michael/Documents/GitHub/worktrees/
```

Expected: no `guild-motd-server` or `guild-motd-unora`. If either exists, stop and report. A previous run may have started them.

- [ ] **Step 2: Create the worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/guild-motd-server -b feat/guild-roster-motd master
git -C Unora -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/guild-motd-unora -b feat/guild-roster-motd main
```

Pass `core.longpaths` with `-c` only. Never write it to the repo config.

- [ ] **Step 3: Baseline build and tests**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/*/*" --no-ansi
```

Expected: `Build succeeded`, and every test in both runs passes.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server branch --show-current", "acceptanceCriteria": ["both worktrees on feat/guild-roster-motd", "SRV Chaos.Tests builds; GuildTests and GuildPermissions tests pass before any change"], "modelTier": "mechanical"}
```

---

### Task 1: The message-of-the-day permission switch

**Goal:** `GuildPermission.SetMessageOfTheDay` exists, the leader's Permissions menu offers it to every lower rank, and it starts off everywhere.

**Files:**
- Modify: `SRV/Chaos.DarkAges/Definitions/Enums.cs` (end of `GuildPermission`)
- Modify: `SRV/Chaos/Collections/GuildPermissionRules.cs` (whole class)
- Test: `SRV/Tests/Chaos.Tests/GuildPermissions/GuildPermissionDataTests.cs` (update one test, add two)
- Test: `SRV/Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs` (update one test)

**Acceptance Criteria:**
- [ ] `ForTier(1)`, `ForTier(2)` and `ForTier(3)` each end with `SetMessageOfTheDay`; `ForTier(0)` is still empty
- [ ] `Label(SetMessageOfTheDay)` is `Set the message of the day`
- [ ] `COUNCIL_DEFAULT` doesn't include it, and no rank of a new guild has it
- [ ] The leader has it; a Council member has it only after `SetRankPermission(1, SetMessageOfTheDay, true)`
- [ ] Each rank menu ends with `Set the message of the day: Off`, then `Done`

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/*/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Update the tests first**

In `worktrees/guild-motd-server/Tests/Chaos.Tests/GuildPermissions/GuildPermissionDataTests.cs`, use `replace_symbol_body` on `GuildPermissionDataTests/Each_tier_lists_only_the_permissions_it_can_use`:

```csharp
[Test]
    public void Each_tier_lists_only_the_permissions_it_can_use()
    {
        GuildPermissionRules.ForTier(COUNCIL_TIER)
                            .Should()
                            .Equal(
                                GuildPermission.WithdrawItems,
                                GuildPermission.WithdrawGold,
                                GuildPermission.GuildGoldBuffs,
                                GuildPermission.BuyHallRooms,
                                GuildPermission.Admit,
                                GuildPermission.Kick,
                                GuildPermission.PromoteDemote,
                                GuildPermission.SetMessageOfTheDay);

        GuildPermissionRules.ForTier(MEMBER_TIER)
                            .Should()
                            .Equal(
                                GuildPermission.WithdrawItems,
                                GuildPermission.WithdrawGold,
                                GuildPermission.GuildGoldBuffs,
                                GuildPermission.BuyHallRooms,
                                GuildPermission.Admit,
                                GuildPermission.Kick,
                                GuildPermission.SetMessageOfTheDay);

        GuildPermissionRules.ForTier(APPLICANT_TIER)
                            .Should()
                            .Equal(
                                GuildPermission.WithdrawItems,
                                GuildPermission.WithdrawGold,
                                GuildPermission.GuildGoldBuffs,
                                GuildPermission.BuyHallRooms,
                                GuildPermission.Admit,
                                GuildPermission.SetMessageOfTheDay);

        GuildPermissionRules.ForTier(LEADER_TIER)
                            .Should()
                            .BeEmpty();
    }
```

Then `insert_after_symbol` on that same method:

```csharp

    [Test]
    public void The_message_of_the_day_switch_starts_off_for_every_rank()
    {
        GuildPermissionRules.Label(GuildPermission.SetMessageOfTheDay)
                            .Should()
                            .Be("Set the message of the day");

        GuildPermissionRules.COUNCIL_DEFAULT
                            .HasFlag(GuildPermission.SetMessageOfTheDay)
                            .Should()
                            .BeFalse();

        MockGuild.Create()
                 .GetRanks()
                 .Should()
                 .OnlyContain(rank => !rank.Permissions.HasFlag(GuildPermission.SetMessageOfTheDay));
    }

    [Test]
    public void Only_the_leader_may_set_the_message_until_a_rank_gets_the_switch()
    {
        var (guild, leader, council) = CreateGuild(COUNCIL_TIER);

        guild.HasPermission(leader.Name, GuildPermission.SetMessageOfTheDay)
             .Should()
             .BeTrue();

        guild.HasPermission(council.Name, GuildPermission.SetMessageOfTheDay)
             .Should()
             .BeFalse();

        guild.SetRankPermission(COUNCIL_TIER, GuildPermission.SetMessageOfTheDay, true);

        guild.HasPermission(council.Name, GuildPermission.SetMessageOfTheDay)
             .Should()
             .BeTrue();
    }
```

In `worktrees/guild-motd-server/Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs`, use `replace_symbol_body` on `GuildPermissionsScriptTests/Each_rank_menu_lists_only_the_switches_that_rank_can_use`:

```csharp
[Test]
    public void Each_rank_menu_lists_only_the_switches_that_rank_can_use()
    {
        var (_, leader, _) = CreateGuild();
        var council = RankMenu(COUNCIL_TIER);
        var member = RankMenu(MEMBER_TIER);
        var applicant = RankMenu(APPLICANT_TIER);

        CreateScript(council)
            .OnDisplaying(leader);

        CreateScript(member)
            .OnDisplaying(leader);

        CreateScript(applicant)
            .OnDisplaying(leader);

        council.Options
               .Select(option => option.OptionText)
               .Should()
               .Equal(
                   "Take items from the bank: On",
                   "Take gold from the bank: On",
                   "Pay for buffs with guild gold: On",
                   "Buy hall upgrades: On",
                   "Admit new members: On",
                   "Kick lower ranks: On",
                   "Promote and demote: On",
                   "Set the message of the day: Off",
                   "Done");

        member.Options
              .Select(option => option.OptionText)
              .Should()
              .Equal(
                  "Take items from the bank: Off",
                  "Take gold from the bank: Off",
                  "Pay for buffs with guild gold: Off",
                  "Buy hall upgrades: Off",
                  "Admit new members: Off",
                  "Kick lower ranks: Off",
                  "Set the message of the day: Off",
                  "Done");

        applicant.Options
                 .Select(option => option.OptionText)
                 .Should()
                 .Equal(
                     "Take items from the bank: Off",
                     "Take gold from the bank: Off",
                     "Pay for buffs with guild gold: Off",
                     "Buy hall upgrades: Off",
                     "Admit new members: Off",
                     "Set the message of the day: Off",
                     "Done");
    }
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/*/*" --no-ansi`

Expected: the build fails with `'GuildPermission' does not contain a definition for 'SetMessageOfTheDay'`.

- [ ] **Step 3: Add the enum value**

In `worktrees/guild-motd-server/Chaos.DarkAges/Definitions/Enums.cs`, use `replace_content` (literal) with this needle:

```csharp
    /// <summary>Promote and demote, within the rank-gap rules.</summary>
    PromoteDemote = 64
```

and this replacement:

```csharp
    /// <summary>Promote and demote, within the rank-gap rules.</summary>
    PromoteDemote = 64,

    /// <summary>Change or clear the guild's message of the day.</summary>
    SetMessageOfTheDay = 128
```

- [ ] **Step 4: Offer the switch and label it**

In `worktrees/guild-motd-server/Chaos/Collections/GuildPermissionRules.cs`, use `replace_symbol_body` on `GuildPermissionRules`:

```csharp
public static class GuildPermissionRules
{
    /// <summary>
    ///     What a Council rank starts with: every permission that existed before permissions could be changed, which
    ///     matches the rules of that time. <see cref="GuildPermission.SetMessageOfTheDay" /> came later and starts off
    /// </summary>
    public const GuildPermission COUNCIL_DEFAULT = GuildPermission.WithdrawItems
                                                   | GuildPermission.WithdrawGold
                                                   | GuildPermission.GuildGoldBuffs
                                                   | GuildPermission.BuyHallRooms
                                                   | GuildPermission.Admit
                                                   | GuildPermission.Kick
                                                   | GuildPermission.PromoteDemote;

    private static readonly IReadOnlyList<GuildPermission> CouncilPermissions =
    [
        GuildPermission.WithdrawItems,
        GuildPermission.WithdrawGold,
        GuildPermission.GuildGoldBuffs,
        GuildPermission.BuyHallRooms,
        GuildPermission.Admit,
        GuildPermission.Kick,
        GuildPermission.PromoteDemote,
        GuildPermission.SetMessageOfTheDay
    ];

    private static readonly IReadOnlyList<GuildPermission> MemberPermissions = CouncilPermissions
                                                                               .Where(permission => permission != GuildPermission.PromoteDemote)
                                                                               .ToList();

    private static readonly IReadOnlyList<GuildPermission> ApplicantPermissions = MemberPermissions
                                                                                  .Where(permission => permission != GuildPermission.Kick)
                                                                                  .ToList();

    /// <summary>
    ///     What a rank of this tier starts with. A rank file saved before permissions existed loads with this too
    /// </summary>
    public static GuildPermission DefaultFor(int tier) => tier == 1 ? COUNCIL_DEFAULT : GuildPermission.None;

    /// <summary>
    ///     The permissions a rank of this tier can be given, in the order the leader's menu lists them. The leader's tier
    ///     has none, because the leader may always do everything
    /// </summary>
    public static IReadOnlyList<GuildPermission> ForTier(int tier)
        => tier switch
        {
            1 => CouncilPermissions,
            2 => MemberPermissions,
            3 => ApplicantPermissions,
            _ => Array.Empty<GuildPermission>()
        };

    /// <summary>
    ///     The text of a permission's line in the leader's menu
    /// </summary>
    public static string Label(GuildPermission permission)
        => permission switch
        {
            GuildPermission.WithdrawItems      => "Take items from the bank",
            GuildPermission.WithdrawGold       => "Take gold from the bank",
            GuildPermission.GuildGoldBuffs     => "Pay for buffs with guild gold",
            GuildPermission.BuyHallRooms       => "Buy hall upgrades",
            GuildPermission.Admit              => "Admit new members",
            GuildPermission.Kick               => "Kick lower ranks",
            GuildPermission.PromoteDemote      => "Promote and demote",
            GuildPermission.SetMessageOfTheDay => "Set the message of the day",
            _                                  => throw new ArgumentOutOfRangeException(nameof(permission), permission, null)
        };
}
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/*/*" --no-ansi`

Expected: every test passes. The Permissions menu builds its lines from `ForTier`, so no script change is needed.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos/Collections/GuildPermissionRules.cs", "Tests/Chaos.Tests/GuildPermissions/GuildPermissionDataTests.cs", "Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildPermissions/*/*\" --no-ansi", "acceptanceCriteria": ["ForTier 1-3 end with SetMessageOfTheDay; tier 0 empty", "Label is 'Set the message of the day'", "not in COUNCIL_DEFAULT; no new-guild rank has it", "leader has it; Council only after SetRankPermission", "each rank menu ends with 'Set the message of the day: Off' then Done"], "modelTier": "mechanical"}
```

---

### Task 2: The message of the day on the guild

**Goal:** A guild stores, saves, loads and sends its message of the day, and a member gets it as a guild chat line at login.

**Files:**
- Create: `SRV/Chaos/Collections/GuildMessageOfTheDay.cs`
- Create: `SRV/Chaos.Schemas/Guilds/GuildMessageOfTheDaySchema.cs`
- Modify: `SRV/Chaos.Schemas/Guilds/GuildSchema.cs` (add `MessageOfTheDay`)
- Modify: `SRV/Chaos/Collections/Guild.cs` (constant and property after `Name`; five methods after `SetTaxRate`)
- Modify: `SRV/Chaos/Services/MapperProfiles/GuildMapperProfile.cs` (both guild maps)
- Modify: `SRV/Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` (`OnLogin`)
- Test: `SRV/Tests/Chaos.Tests/GuildMotd/GuildMessageOfTheDayDataTests.cs` (new)

**Acceptance Criteria:**
- [ ] A new guild has no message; set and clear work
- [ ] Blank text throws `ArgumentException`; 150 characters is allowed and 151 throws `ArgumentOutOfRangeException`
- [ ] The message survives a map round trip; a `guild.json` without the field loads with no message; a saved 300-character message still loads
- [ ] `SendMessageOfTheDay` sends `TestGuild message of the day: Hunt at 8` as `GuildChat` to that player only, and nothing when there is no message
- [ ] `BroadcastMessageOfTheDay` reaches online members only
- [ ] `DefaultAislingScript.OnLogin` calls `Subject.Guild?.SendMessageOfTheDay(Subject)` after the "has appeared online" loop, and the server builds

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildMotd/GuildMessageOfTheDayDataTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/GuildMotd/GuildMessageOfTheDayDataTests.cs`:

```csharp
#region
using System.Text.Json;
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Schemas.Guilds;
using Chaos.Services.MapperProfiles;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Moq;
#endregion

namespace Chaos.Tests.GuildMotd;

public sealed class GuildMessageOfTheDayDataTests
{
    private static readonly DateTime SET_AT = new(2026, 9, 25, 18, 0, 0, DateTimeKind.Utc);

    private static GuildMapperProfile CreateProfile() => new(MockChannelService.Create(), new ClientRegistry<IChaosWorldClient>());

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
    public void A_new_guild_has_no_message()
        => MockGuild.Create()
                    .MessageOfTheDay
                    .Should()
                    .BeNull();

    [Test]
    public void A_message_can_be_set_and_cleared()
    {
        var guild = MockGuild.Create();

        guild.SetMessageOfTheDay("Hunt at 8", "Stahli", SET_AT);

        guild.MessageOfTheDay
             .Should()
             .Be(new GuildMessageOfTheDay("Hunt at 8", "Stahli", SET_AT));

        guild.ClearMessageOfTheDay();

        guild.MessageOfTheDay
             .Should()
             .BeNull();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public void Blank_text_is_refused(string text)
    {
        var act = () => MockGuild.Create()
                                 .SetMessageOfTheDay(text, "Stahli", SET_AT);

        act.Should()
           .Throw<ArgumentException>();
    }

    [Test]
    public void Text_over_150_characters_is_refused()
    {
        var guild = MockGuild.Create();

        guild.SetMessageOfTheDay(new string('a', 150), "Stahli", SET_AT);

        var act = () => guild.SetMessageOfTheDay(new string('a', 151), "Stahli", SET_AT);

        act.Should()
           .Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void The_message_survives_a_save_and_load()
    {
        var profile = CreateProfile();
        var guild = MockGuild.Create();

        guild.SetMessageOfTheDay("Hunt at 8", "Stahli", SET_AT);

        var reloaded = profile.Map(profile.Map(guild));

        reloaded.MessageOfTheDay
                .Should()
                .Be(new GuildMessageOfTheDay("Hunt at 8", "Stahli", SET_AT));
    }

    [Test]
    public void A_guild_file_from_before_this_change_loads_with_no_message()
    {
        var schema = JsonSerializer.Deserialize<GuildSchema>(
            """{"guid":"abc","name":"Old"}""",
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;

        CreateProfile()
            .Map(schema)
            .MessageOfTheDay
            .Should()
            .BeNull();
    }

    [Test]
    public void A_hand_edited_long_message_still_loads()
    {
        var schema = new GuildSchema
        {
            Guid = "abc",
            Name = "Edited",
            MessageOfTheDay = new GuildMessageOfTheDaySchema
            {
                Text = new string('a', 300),
                SetBy = "Stahli",
                SetAt = SET_AT
            }
        };

        CreateProfile()
            .Map(schema)
            .MessageOfTheDay!
            .Text
            .Should()
            .HaveLength(300);
    }

    [Test]
    public void The_login_line_goes_to_that_member_only()
    {
        var registry = new ClientRegistry<IChaosWorldClient>();
        var leader = CreateOnline(registry, "Stahli");
        var member = CreateOnline(registry, "Iglis");
        var guild = MockGuild.Create(clientRegistry: registry);

        guild.AddMember(leader, member);
        guild.AddMember(member, leader);
        guild.SetMessageOfTheDay("Hunt at 8", "Stahli", SET_AT);

        guild.SendMessageOfTheDay(member);

        Mock.Get(member.Client)
            .Verify(c => c.SendServerMessage(ServerMessageType.GuildChat, "TestGuild message of the day: Hunt at 8"), Times.Once);

        Mock.Get(leader.Client)
            .Verify(c => c.SendServerMessage(ServerMessageType.GuildChat, It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void No_line_is_sent_when_there_is_no_message()
    {
        var member = MockAisling.Create(name: "Iglis");

        MockGuild.Create()
                 .SendMessageOfTheDay(member);

        Mock.Get(member.Client)
            .Verify(c => c.SendServerMessage(ServerMessageType.GuildChat, It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void A_broadcast_reaches_online_members_only()
    {
        var registry = new ClientRegistry<IChaosWorldClient>();
        var leader = CreateOnline(registry, "Stahli");
        var member = CreateOnline(registry, "Iglis");
        var away = MockAisling.Create(name: "Away");
        var guild = MockGuild.Create(clientRegistry: registry);

        guild.AddMember(leader, member);
        guild.AddMember(member, leader);
        guild.AddMember(away, leader);
        guild.SetMessageOfTheDay("Hunt at 8", "Stahli", SET_AT);

        guild.BroadcastMessageOfTheDay();

        foreach (var online in new[] { leader, member })
            Mock.Get(online.Client)
                .Verify(c => c.SendServerMessage(ServerMessageType.GuildChat, "TestGuild message of the day: Hunt at 8"), Times.Once);

        Mock.Get(away.Client)
            .Verify(c => c.SendServerMessage(ServerMessageType.GuildChat, It.IsAny<string>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildMotd/GuildMessageOfTheDayDataTests/*" --no-ansi`

Expected: the build fails because `GuildMessageOfTheDay`, `GuildMessageOfTheDaySchema` and the new `Guild` members don't exist.

- [ ] **Step 3: Create the message record**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Chaos/Collections/GuildMessageOfTheDay.cs`:

```csharp
namespace Chaos.Collections;

/// <summary>
///     A guild's message of the day
/// </summary>
/// <param name="Text">
///     What the message says
/// </param>
/// <param name="SetBy">
///     The name of the member who set it
/// </param>
/// <param name="SetAt">
///     When it was set (UTC)
/// </param>
public sealed record GuildMessageOfTheDay(string Text, string SetBy, DateTime SetAt)
{
    /// <summary>
    ///     The guild chat line a member sees at login, and every online member sees when the message changes
    /// </summary>
    public string ToChatLine(string guildName) => $"{guildName} message of the day: {Text}";
}
```

- [ ] **Step 4: Create the saved form**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Chaos.Schemas/Guilds/GuildMessageOfTheDaySchema.cs`:

```csharp
namespace Chaos.Schemas.Guilds;

/// <summary>
///     Represents the serializable schema of a guild's message of the day
/// </summary>
public sealed record GuildMessageOfTheDaySchema
{
    /// <summary>
    ///     When the message was set (UTC)
    /// </summary>
    public DateTime SetAt { get; set; }

    /// <summary>
    ///     The name of the member who set the message
    /// </summary>
    public string SetBy { get; set; } = string.Empty;

    /// <summary>
    ///     What the message says. An empty text loads as no message
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
```

In `worktrees/guild-motd-server/Chaos.Schemas/Guilds/GuildSchema.cs`, use `insert_after_symbol` on `GuildSchema/AllowanceWeekStart`:

```csharp

    /// <summary>
    ///     The guild's message of the day, or null when it has none. Missing in files saved before messages existed
    /// </summary>
    public GuildMessageOfTheDaySchema? MessageOfTheDay { get; set; }
```

- [ ] **Step 5: Add the message to the guild**

In `worktrees/guild-motd-server/Chaos/Collections/Guild.cs`, use `insert_after_symbol` on `Guild/Name`:

```csharp

    /// <summary>
    ///     The longest message of the day a member can set
    /// </summary>
    public const int MESSAGE_OF_THE_DAY_MAX_LENGTH = 150;

    /// <summary>
    ///     The guild's message of the day, or null when it has none
    /// </summary>
    public GuildMessageOfTheDay? MessageOfTheDay { get; private set; }
```

Then `insert_after_symbol` on `Guild/SetTaxRate`:

```csharp

    /// <summary>
    ///     Sets the message of the day. The dialog trims and checks the text first; this throws on text that is blank or
    ///     longer than <see cref="MESSAGE_OF_THE_DAY_MAX_LENGTH" />
    /// </summary>
    public void SetMessageOfTheDay(string text, string setBy, DateTime setAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrEmpty(setBy);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, MESSAGE_OF_THE_DAY_MAX_LENGTH, nameof(text));

        using var @lock = Sync.EnterScope();

        MessageOfTheDay = new GuildMessageOfTheDay(text, setBy, setAt);
    }

    /// <summary>
    ///     Removes the message of the day
    /// </summary>
    public void ClearMessageOfTheDay()
    {
        using var @lock = Sync.EnterScope();

        MessageOfTheDay = null;
    }

    /// <summary>
    ///     Puts back a saved message of the day. Only the mapper calls this. It skips the checks in
    ///     <see cref="SetMessageOfTheDay" />, so a hand-edited file never stops the guild from loading
    /// </summary>
    public void RestoreMessageOfTheDay(GuildMessageOfTheDay? message)
    {
        using var @lock = Sync.EnterScope();

        MessageOfTheDay = message;
    }

    /// <summary>
    ///     Sends the message of the day to one member as a guild chat line. Sends nothing when there is no message
    /// </summary>
    public void SendMessageOfTheDay(Aisling aisling)
    {
        var message = MessageOfTheDay;

        if (message is null)
            return;

        aisling.SendServerMessage(ServerMessageType.GuildChat, message.ToChatLine(Name));
    }

    /// <summary>
    ///     Sends the message of the day to every online member
    /// </summary>
    public void BroadcastMessageOfTheDay()
    {
        foreach (var member in GetOnlineMembers())
            SendMessageOfTheDay(member);
    }
```

- [ ] **Step 6: Save and load the message**

In `worktrees/guild-motd-server/Chaos/Services/MapperProfiles/GuildMapperProfile.cs`, run `find_symbol` on `GuildMapperProfile/Map` with `include_body=true`. Use `replace_symbol_body` on the overload whose signature is `public Guild Map(GuildSchema obj)`:

```csharp
public Guild Map(GuildSchema obj)
    {
        var guild = new Guild(
            obj.Name,
            obj.Guid,
            ChannelService,
            ClientRegistry);

        guild.SetTaxRate(obj.TaxRatePercent);

        guild.RestoreAllowanceLedger(
            obj.AllowanceWeekStart,
            obj.AllowanceSpending.Select(kvp => KeyValuePair.Create(
                kvp.Key,
                new GuildAllowanceSpending(kvp.Value.Training, kvp.Value.Repairs))));

        //an empty text counts as no message, so a half-finished hand edit loads cleanly
        guild.RestoreMessageOfTheDay(
            obj.MessageOfTheDay is { Text.Length: > 0 } message
                ? new GuildMessageOfTheDay(message.Text, message.SetBy, message.SetAt)
                : null);

        return guild;
    }
```

Then `replace_symbol_body` on the overload whose signature is `public GuildSchema Map(Guild obj)`:

```csharp
public GuildSchema Map(Guild obj)
    {
        var ledger = obj.GetAllowanceLedger();
        var message = obj.MessageOfTheDay;

        return new GuildSchema
        {
            Name = obj.Name,
            Guid = obj.Guid,
            TaxRatePercent = obj.TaxRatePercent,
            AllowanceWeekStart = ledger.WeekStart,
            AllowanceSpending = ledger.Spending.ToDictionary(
                kvp => kvp.Key,
                kvp => new GuildAllowanceSpendingSchema
                {
                    Training = kvp.Value.Training,
                    Repairs = kvp.Value.Repairs
                },
                StringComparer.OrdinalIgnoreCase),
            MessageOfTheDay = message is null
                ? null
                : new GuildMessageOfTheDaySchema
                {
                    Text = message.Text,
                    SetBy = message.SetBy,
                    SetAt = message.SetAt
                }
        };
    }
```

- [ ] **Step 7: Send the line at login**

In `worktrees/guild-motd-server/Chaos/Scripting/AislingScripts/DefaultAislingScript.cs`, read `DefaultAislingScript/OnLogin` with `find_symbol` (`include_body=true`). Check it matches the code below apart from the two new lines, then use `replace_symbol_body`:

```csharp
public override void OnLogin()
    {
        MigrateMountsAndCloaks(Subject);
        MigrateDefaultFaceSprite(Subject);

        if (Subject.Trackers.LastLogout is { } lastLogout
            && (lastLogout.AddHours(2) <= DateTime.UtcNow)
            && Subject.Trackers.Enums.HasValue(TutorialQuestStage.CompletedTutorial))
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
        Subject.Guild?.SendMessageOfTheDay(Subject);

        ApplyPendingPositionRemovals();

        //after the pending removals, so an Apostle removed while offline drops off the temple board now
        ClergyRoster.Sync(Subject);
        PollManager.SendSnapshotTo(Subject);
    }
```

If the current body differs from this in any line other than the two new ones, stop and report instead of replacing it. Another session may have changed it.

- [ ] **Step 8: Run the tests to see them pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildMotd/GuildMessageOfTheDayDataTests/*" --no-ansi`

Expected: all 11 tests pass, and the build (which includes `DefaultAislingScript`) succeeds.

Then run `GuildTests`, to check the guild still behaves as before:

`dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildTests/*" --no-ansi` → all pass.

```json:metadata
{"files": ["Chaos/Collections/GuildMessageOfTheDay.cs", "Chaos.Schemas/Guilds/GuildMessageOfTheDaySchema.cs", "Chaos.Schemas/Guilds/GuildSchema.cs", "Chaos/Collections/Guild.cs", "Chaos/Services/MapperProfiles/GuildMapperProfile.cs", "Chaos/Scripting/AislingScripts/DefaultAislingScript.cs", "Tests/Chaos.Tests/GuildMotd/GuildMessageOfTheDayDataTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildMotd/GuildMessageOfTheDayDataTests/*\" --no-ansi", "acceptanceCriteria": ["new guild has no message; set and clear work", "blank throws ArgumentException; 150 ok; 151 throws ArgumentOutOfRangeException", "round trip keeps it; old file loads with none; 300-char saved message loads", "SendMessageOfTheDay sends the GuildChat line to that player only, nothing when empty", "BroadcastMessageOfTheDay reaches online members only", "OnLogin calls Subject.Guild?.SendMessageOfTheDay(Subject) after the online loop; server builds"], "modelTier": "standard"}
```

---

### Task 3: Last-seen times on the guild

**Goal:** A guild keeps each member's last-seen time, updated at join and logout, removed on leave and kick, saved in `guild.json`, and cleaned of non-members on load.

**Files:**
- Modify: `SRV/Chaos.Schemas/Guilds/GuildSchema.cs` (add `LastSeen`)
- Modify: `SRV/Chaos/Collections/Guild.cs` (field; `AddMember`, `TryLeave`, `TryKickMember`, `Initialize`; four public methods after `BroadcastMessageOfTheDay`; `UnsafeRecordLastSeen` after `UnsafeRankof`)
- Modify: `SRV/Chaos/Services/MapperProfiles/GuildMapperProfile.cs` (both guild maps)
- Modify: `SRV/Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` (`OnLogout`)
- Test: `SRV/Tests/Chaos.Tests/GuildRoster/GuildLastSeenDataTests.cs` (new)

**Acceptance Criteria:**
- [ ] Joining records a time at or after the moment of joining
- [ ] `RecordLastSeen` keeps the later time, ignores non-members, and matches names without regard to case
- [ ] `TryLeave` and `TryKickMember` remove the member's time
- [ ] Times survive a map round trip; a `guild.json` without the field loads with none
- [ ] After `Initialize`, names that aren't in any rank are gone
- [ ] `DefaultAislingScript.OnLogout` calls `guild.RecordLastSeen(Subject.Name, DateTime.UtcNow)`, and the server builds

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildRoster/GuildLastSeenDataTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/GuildRoster/GuildLastSeenDataTests.cs`:

```csharp
#region
using System.Text.Json;
using Chaos.Collections;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Schemas.Guilds;
using Chaos.Services.MapperProfiles;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildRoster;

public sealed class GuildLastSeenDataTests
{
    private const int LEADER_TIER = 0;
    private static readonly DateTime SEEN = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

    private static GuildMapperProfile CreateProfile() => new(MockChannelService.Create(), new ClientRegistry<IChaosWorldClient>());

    private static (Guild Guild, Aisling Leader, Aisling Member) CreateGuild()
    {
        var guild = MockGuild.Create();
        var leader = MockAisling.Create(name: "Stahli");
        var member = MockAisling.Create(name: "Iglis");

        guild.AddMember(leader, member);
        guild.ChangeRank(leader.Name, LEADER_TIER, member);
        guild.AddMember(member, leader);

        return (guild, leader, member);
    }

    [Test]
    public void Joining_records_the_time()
    {
        var before = DateTime.UtcNow;
        var (guild, _, member) = CreateGuild();

        guild.TryGetLastSeen(member.Name, out var seen)
             .Should()
             .BeTrue();

        seen.Should()
            .BeOnOrAfter(before);
    }

    [Test]
    public void Recording_keeps_the_later_time()
    {
        var (guild, _, member) = CreateGuild();

        guild.RestoreLastSeen([KeyValuePair.Create(member.Name, SEEN)]);

        guild.RecordLastSeen(member.Name, SEEN.AddDays(-1));

        guild.TryGetLastSeen(member.Name, out var afterOlder);

        afterOlder.Should()
                  .Be(SEEN);

        guild.RecordLastSeen("IGLIS", SEEN.AddDays(1));

        guild.TryGetLastSeen("iglis", out var afterNewer)
             .Should()
             .BeTrue();

        afterNewer.Should()
                  .Be(SEEN.AddDays(1));
    }

    [Test]
    public void Recording_ignores_someone_outside_the_guild()
    {
        var (guild, _, _) = CreateGuild();

        guild.RecordLastSeen("Stranger", SEEN);

        guild.TryGetLastSeen("Stranger", out _)
             .Should()
             .BeFalse();
    }

    [Test]
    public void Leaving_removes_the_time()
    {
        var (guild, _, member) = CreateGuild();

        guild.TryLeave(member)
             .Should()
             .BeTrue();

        guild.TryGetLastSeen("Iglis", out _)
             .Should()
             .BeFalse();
    }

    [Test]
    public void Being_kicked_removes_the_time()
    {
        var (guild, leader, _) = CreateGuild();

        guild.TryKickMember("Iglis", leader)
             .Should()
             .BeTrue();

        guild.TryGetLastSeen("Iglis", out _)
             .Should()
             .BeFalse();
    }

    [Test]
    public void Times_survive_a_save_and_load()
    {
        var profile = CreateProfile();
        var (guild, _, _) = CreateGuild();

        guild.RestoreLastSeen([KeyValuePair.Create("Stahli", SEEN), KeyValuePair.Create("Iglis", SEEN.AddDays(1))]);

        var reloaded = profile.Map(profile.Map(guild));

        reloaded.GetLastSeen()
                .Should()
                .BeEquivalentTo(
                    new Dictionary<string, DateTime>
                    {
                        ["Stahli"] = SEEN,
                        ["Iglis"] = SEEN.AddDays(1)
                    });
    }

    [Test]
    public void A_guild_file_from_before_this_change_loads_with_no_times()
    {
        var schema = JsonSerializer.Deserialize<GuildSchema>(
            """{"guid":"abc","name":"Old"}""",
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;

        CreateProfile()
            .Map(schema)
            .GetLastSeen()
            .Should()
            .BeEmpty();
    }

    [Test]
    public void Loading_drops_names_that_are_not_members()
    {
        var guild = CreateProfile()
            .Map(
                new GuildSchema
                {
                    Guid = "abc",
                    Name = "Loaded",
                    LastSeen = new Dictionary<string, DateTime>
                    {
                        ["Stahli"] = SEEN,
                        ["Gone"] = SEEN
                    }
                });

        guild.Initialize(
            [
                new GuildRank("Leader", 0, ["Stahli"]),
                new GuildRank("Council", 1),
                new GuildRank("Member", 2),
                new GuildRank("Applicant", 3)
            ],
            new Bank());

        guild.GetLastSeen()
             .Keys
             .Should()
             .Equal("Stahli");
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildRoster/GuildLastSeenDataTests/*" --no-ansi`

Expected: the build fails because `LastSeen`, `RestoreLastSeen`, `RecordLastSeen`, `TryGetLastSeen` and `GetLastSeen` don't exist.

- [ ] **Step 3: Add the saved field**

In `worktrees/guild-motd-server/Chaos.Schemas/Guilds/GuildSchema.cs`, use `insert_after_symbol` on `GuildSchema/MessageOfTheDay`:

```csharp

    /// <summary>
    ///     When each member was last seen (UTC), by member name. Written at logout. Missing for members who haven't logged
    ///     out since last-seen times were added
    /// </summary>
    public Dictionary<string, DateTime> LastSeen { get; set; } = [];
```

- [ ] **Step 4: Add the field and methods to the guild**

In `worktrees/guild-motd-server/Chaos/Collections/Guild.cs`, use `replace_content` (literal) with the needle `    private readonly GuildAllowanceLedger AllowanceLedger = new();` and this replacement (two lines):

```csharp
    private readonly GuildAllowanceLedger AllowanceLedger = new();
    private readonly Dictionary<string, DateTime> LastSeenTimes = new(StringComparer.OrdinalIgnoreCase);
```

Then `insert_after_symbol` on `Guild/BroadcastMessageOfTheDay`:

```csharp

    /// <summary>
    ///     Records when a member was last seen. Keeps the later of the stored time and <paramref name="when" />, so a slow
    ///     read of an old save can't overwrite a newer logout. Does nothing for someone not in the guild
    /// </summary>
    public void RecordLastSeen(string memberName, DateTime when)
    {
        ArgumentException.ThrowIfNullOrEmpty(memberName);

        using var @lock = Sync.EnterScope();

        if (UnsafeRankof(memberName) is null)
            return;

        UnsafeRecordLastSeen(memberName, when);
    }

    /// <summary>
    ///     When a member was last seen (UTC), if the guild knows
    /// </summary>
    public bool TryGetLastSeen(string memberName, out DateTime when)
    {
        using var @lock = Sync.EnterScope();

        return LastSeenTimes.TryGetValue(memberName, out when);
    }

    /// <summary>
    ///     A copy of every known last-seen time, by member name
    /// </summary>
    public IReadOnlyDictionary<string, DateTime> GetLastSeen()
    {
        using var @lock = Sync.EnterScope();

        return new Dictionary<string, DateTime>(LastSeenTimes, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Replaces the last-seen times with saved ones. Only the mapper calls this. The mapper runs before the ranks are
    ///     loaded, so this can't check membership; <see cref="Initialize" /> drops names that aren't members
    /// </summary>
    public void RestoreLastSeen(IEnumerable<KeyValuePair<string, DateTime>> entries)
    {
        using var @lock = Sync.EnterScope();

        LastSeenTimes.Clear();

        foreach (var (memberName, when) in entries)
            UnsafeRecordLastSeen(memberName, when);
    }
```

Then `insert_after_symbol` on `Guild/UnsafeRankof`:

```csharp

    private void UnsafeRecordLastSeen(string memberName, DateTime when)
    {
        if (!LastSeenTimes.TryGetValue(memberName, out var stored) || (when > stored))
            LastSeenTimes[memberName] = when;
    }
```

- [ ] **Step 5: Keep the times in step with membership**

Use `replace_symbol_body` on `Guild/AddMember`:

```csharp
public void AddMember(Aisling aisling, Aisling by)
    {
        ArgumentNullException.ThrowIfNull(aisling);

        using var @lock = Sync.EnterScope();

        if (aisling.Guild is not null)
        {
            if (aisling.Guild == this)
                return;

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

        foreach (var member in GetOnlineMembers()
                     .Where(member => !member.Equals(by)))
            member.SendActiveMessage($"{aisling.Name} has been admitted to the guild by {by.Name}!");
    }
```

Use `replace_symbol_body` on `Guild/TryKickMember`:

```csharp
public bool TryKickMember(string memberName, Aisling by)
    {
        ArgumentException.ThrowIfNullOrEmpty(memberName);

        using var @lock = Sync.EnterScope();

        var rank = UnsafeRankof(memberName);

        //leaders can not be removed
        if (rank is null or { Tier: 0 })
            return false;

        rank.RemoveMember(memberName);
        LastSeenTimes.Remove(memberName);

        var aisling = ClientRegistry.FirstOrDefault(cli => cli.Aisling.Name.EqualsI(memberName))
                                    ?.Aisling;

        if (aisling is not null)
            UnsafeDetach(aisling);

        foreach (var member in GetOnlineMembers())
            member.SendActiveMessage($"{memberName} has been kicked from the guild by {by.Name}");

        return true;
    }
```

Use `replace_symbol_body` on `Guild/TryLeave`:

```csharp
public bool TryLeave(Aisling aisling)
    {
        ArgumentNullException.ThrowIfNull(aisling);

        using var @lock = Sync.EnterScope();

        var memberName = aisling.Name;
        var rank = UnsafeRankof(memberName);

        //leaders can only leave if there's another leader
        if (rank is null or { Tier: 0, Count: <= 1 })
            return false;

        rank.RemoveMember(memberName);
        LastSeenTimes.Remove(memberName);
        UnsafeDetach(aisling);

        aisling.SendActiveMessage($"You have left {Name}");

        foreach (var member in GetOnlineMembers())
            member.SendActiveMessage($"{memberName} has left the guild");

        return true;
    }
```

Use `replace_symbol_body` on `Guild/Initialize`:

```csharp
public void Initialize(IEnumerable<GuildRank> guildHierarchy, Bank bank)
    {
        using var @lock = Sync.EnterScope();

        GuildHierarchy.Clear();
        GuildHierarchy.AddRange(guildHierarchy);
        Bank = bank;

        //the mapper restores last-seen times before the ranks exist, so names that aren't members are dropped here
        foreach (var memberName in LastSeenTimes.Keys.ToArray())
            if (UnsafeRankof(memberName) is null)
                LastSeenTimes.Remove(memberName);
    }
```

For each of these four, first read the current body with `find_symbol` and check it matches the code above apart from the new lines. If anything else differs, stop and report.

- [ ] **Step 6: Save and load the times**

In `worktrees/guild-motd-server/Chaos/Services/MapperProfiles/GuildMapperProfile.cs`, use `replace_symbol_body` on the overload whose signature is `public Guild Map(GuildSchema obj)`:

```csharp
public Guild Map(GuildSchema obj)
    {
        var guild = new Guild(
            obj.Name,
            obj.Guid,
            ChannelService,
            ClientRegistry);

        guild.SetTaxRate(obj.TaxRatePercent);

        guild.RestoreAllowanceLedger(
            obj.AllowanceWeekStart,
            obj.AllowanceSpending.Select(kvp => KeyValuePair.Create(
                kvp.Key,
                new GuildAllowanceSpending(kvp.Value.Training, kvp.Value.Repairs))));

        //an empty text counts as no message, so a half-finished hand edit loads cleanly
        guild.RestoreMessageOfTheDay(
            obj.MessageOfTheDay is { Text.Length: > 0 } message
                ? new GuildMessageOfTheDay(message.Text, message.SetBy, message.SetAt)
                : null);

        //the ranks aren't loaded yet; Guild.Initialize drops names that aren't members
        guild.RestoreLastSeen(obj.LastSeen);

        return guild;
    }
```

Then `replace_symbol_body` on the overload whose signature is `public GuildSchema Map(Guild obj)`:

```csharp
public GuildSchema Map(Guild obj)
    {
        var ledger = obj.GetAllowanceLedger();
        var message = obj.MessageOfTheDay;

        return new GuildSchema
        {
            Name = obj.Name,
            Guid = obj.Guid,
            TaxRatePercent = obj.TaxRatePercent,
            AllowanceWeekStart = ledger.WeekStart,
            AllowanceSpending = ledger.Spending.ToDictionary(
                kvp => kvp.Key,
                kvp => new GuildAllowanceSpendingSchema
                {
                    Training = kvp.Value.Training,
                    Repairs = kvp.Value.Repairs
                },
                StringComparer.OrdinalIgnoreCase),
            MessageOfTheDay = message is null
                ? null
                : new GuildMessageOfTheDaySchema
                {
                    Text = message.Text,
                    SetBy = message.SetBy,
                    SetAt = message.SetAt
                },
            LastSeen = obj.GetLastSeen()
                          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
        };
    }
```

- [ ] **Step 7: Record the time at logout**

In `worktrees/guild-motd-server/Chaos/Scripting/AislingScripts/DefaultAislingScript.cs`, read `DefaultAislingScript/OnLogout` with `find_symbol` (`include_body=true`). Check it matches the code below apart from the new braces and the two new lines, then use `replace_symbol_body`:

```csharp
public override void OnLogout()
    {
        base.OnLogout();

        if (Subject.Effects.Contains("Mount"))
            Subject.Effects.Terminate("Mount");

        //the temple boards only re-check online players, so an Apostle change made this session is recorded before they leave
        ClergyRoster.Sync(Subject);

        var guild = Subject.Guild;

        if (guild != null)
        {
            //the roster's last-seen time; the guild's timed save writes it to disk
            guild.RecordLastSeen(Subject.Name, DateTime.UtcNow);

            foreach (var member in guild.GetOnlineMembers())
                member.SendServerMessage(ServerMessageType.ActiveMessage, $"({guild.Name}) - {Subject.Name} has gone offline.");
        }
    }
```

If the current body differs in any other line, stop and report.

- [ ] **Step 8: Run the tests to see them pass**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildRoster/GuildLastSeenDataTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildMotd/*/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildTests/*" --no-ansi
```

Expected: all pass in every run.

```json:metadata
{"files": ["Chaos.Schemas/Guilds/GuildSchema.cs", "Chaos/Collections/Guild.cs", "Chaos/Services/MapperProfiles/GuildMapperProfile.cs", "Chaos/Scripting/AislingScripts/DefaultAislingScript.cs", "Tests/Chaos.Tests/GuildRoster/GuildLastSeenDataTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildRoster/GuildLastSeenDataTests/*\" --no-ansi", "acceptanceCriteria": ["joining records a time at or after joining", "RecordLastSeen keeps the later time, ignores non-members, is case-insensitive", "TryLeave and TryKickMember remove the time", "times round-trip; old guild.json loads with none", "Initialize drops non-member names", "OnLogout records DateTime.UtcNow; server builds"], "modelTier": "standard"}
```

---

### Task 4: The Message of the Day dialogs at Quill and Aricin

**Goal:** Every guild member can read the message at Quill and Aricin, and the leader and permitted ranks can change or clear it through six Unora dialogs.

**Files:**
- Create: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMessageOfTheDayScript.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs` (`OnDisplaying`)
- Modify: `SRV/Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs` (`Only_the_leader_gets_Permissions_at_Quill_and_Aricin`)
- Test: `SRV/Tests/Chaos.Tests/GuildMotd/GuildMessageOfTheDayScriptTests.cs` (new)
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildMessageOfTheDay/` (6 JSON files)

**Acceptance Criteria:**
- [ ] Every guild member sees `Message of the Day` right before `Members` at Quill and Aricin; a player outside a guild sees only `Create`
- [ ] The first screen shows `Set by Stahli on 2026-09-25 (UTC):` + blank line + text, or `Your guild has no message of the day.`
- [ ] The leader gets `Change` and `Clear` (Clear only when a message exists); a Council member without the switch gets neither; with the switch, gets `Change`
- [ ] The confirmation shows the trimmed text
- [ ] Blank input replies `Nothing was changed.`; 151 characters replies `That message is too long. Keep it to 150 characters.`; neither saves
- [ ] Accepting sets the message with the setter's name, saves once, and sends the chat line to online members
- [ ] A rank whose switch goes off before accepting gets `Your rank can't change the message of the day.` and nothing changes
- [ ] Clearing removes the message and saves once
- [ ] The six Unora dialog files exist with the keys, types and texts below, and parse as JSON

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildMotd/GuildMessageOfTheDayScriptTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/GuildMotd/GuildMessageOfTheDayScriptTests.cs`:

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
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
#endregion

namespace Chaos.Tests.GuildMotd;

public sealed class GuildMessageOfTheDayScriptTests
{
    private const int COUNCIL_TIER = 1;
    private const int LEADER_TIER = 0;
    private static readonly DateTime SET_AT = new(2026, 9, 25, 18, 0, 0, DateTimeKind.Utc);

    private static GuildMessageOfTheDayScript CreateScript(Dialog dialog, IStore<Guild>? store = null)
        => new(
            dialog,
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            store ?? new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildMessageOfTheDayScript>>().Object);

    private static Aisling CreateOnline(ClientRegistry<IChaosWorldClient> registry, string name)
    {
        var aisling = MockAisling.Create(name: name);

        Mock.Get(aisling.Client)
            .SetupGet(c => c.Id)
            .Returns(aisling.Id);

        registry.TryAdd(aisling.Client);

        return aisling;
    }

    private static (Guild Guild, Aisling Leader, Aisling Council) CreateGuild()
    {
        var registry = new ClientRegistry<IChaosWorldClient>();
        var guild = MockGuild.Create(clientRegistry: registry);
        var leader = CreateOnline(registry, "Stahli");
        var council = CreateOnline(registry, "Iglis");

        guild.AddMember(leader, council);
        guild.ChangeRank(leader.Name, LEADER_TIER, council);
        guild.AddMember(council, leader);
        guild.ChangeRank(council.Name, COUNCIL_TIER, leader);

        return (guild, leader, council);
    }

    private static Dialog DialogWithText(string templateKey, string text, string? typed = null)
        => MockDialog.Create(
            templateKey,
            dialog =>
            {
                dialog.Text = text;

                if (typed is not null)
                    dialog.MenuArgs = new ArgumentCollection(new[] { typed });
            });

    private static IEnumerable<string> GuildMenuAt(string npcName, Aisling source)
    {
        var dialog = MockDialog.Create("guild_initial");

        Mock.Get(dialog.DialogSource)
            .SetupGet(entity => entity.Name)
            .Returns(npcName);

        new GuildManagementScript(
            dialog,
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildManagementScript>>().Object).OnDisplaying(source);

        return dialog.Options.Select(option => option.OptionText);
    }

    private static IEnumerable<string> OptionsOf(Dialog dialog) => dialog.Options.Select(option => option.OptionText);

    [Test]
    public void Every_member_sees_Message_of_the_Day_at_Quill_and_Aricin()
    {
        var (_, _, council) = CreateGuild();

        GuildMenuAt("Quill", council)
            .Should()
            .Equal(
                "Buffs",
                "Allowances",
                "Message of the Day",
                "Members",
                "Leave");

        GuildMenuAt("Aricin", council)
            .Should()
            .Equal("Message of the Day", "Members", "Leave");

        GuildMenuAt("Quill", MockAisling.Create(name: "Outsider"))
            .Should()
            .Equal("Create");
    }

    [Test]
    public void The_first_screen_shows_the_message_and_who_set_it()
    {
        var (guild, _, council) = CreateGuild();
        var dialog = DialogWithText("generic_guild_motd_initial", "{Message}");

        guild.SetMessageOfTheDay("Hunt at 8", "Stahli", SET_AT);

        CreateScript(dialog)
            .OnDisplaying(council);

        dialog.Text
              .Should()
              .Be("Set by Stahli on 2026-09-25 (UTC):\n\nHunt at 8");
    }

    [Test]
    public void With_no_message_the_leader_gets_Change_but_not_Clear()
    {
        var (_, leader, _) = CreateGuild();
        var dialog = DialogWithText("generic_guild_motd_initial", "{Message}");

        CreateScript(dialog)
            .OnDisplaying(leader);

        dialog.Text
              .Should()
              .Be("Your guild has no message of the day.");

        OptionsOf(dialog)
            .Should()
            .Equal("Change");
    }

    [Test]
    public void With_a_message_the_leader_gets_Change_and_Clear()
    {
        var (guild, leader, _) = CreateGuild();
        var dialog = DialogWithText("generic_guild_motd_initial", "{Message}");

        guild.SetMessageOfTheDay("Hunt at 8", "Stahli", SET_AT);

        CreateScript(dialog)
            .OnDisplaying(leader);

        OptionsOf(dialog)
            .Should()
            .Equal("Change", "Clear");
    }

    [Test]
    public void A_rank_without_the_switch_can_only_read()
    {
        var (guild, _, council) = CreateGuild();
        var dialog = DialogWithText("generic_guild_motd_initial", "{Message}");

        guild.SetMessageOfTheDay("Hunt at 8", "Stahli", SET_AT);

        CreateScript(dialog)
            .OnDisplaying(council);

        OptionsOf(dialog)
            .Should()
            .BeEmpty();
    }

    [Test]
    public void A_rank_given_the_switch_gets_Change()
    {
        var (guild, _, council) = CreateGuild();
        var dialog = DialogWithText("generic_guild_motd_initial", "{Message}");

        guild.SetRankPermission(COUNCIL_TIER, GuildPermission.SetMessageOfTheDay, true);

        CreateScript(dialog)
            .OnDisplaying(council);

        OptionsOf(dialog)
            .Should()
            .Equal("Change");
    }

    [Test]
    public void The_confirmation_shows_the_trimmed_text()
    {
        var (_, leader, _) = CreateGuild();
        var dialog = DialogWithText("generic_guild_motd_change_confirmation", "{Text}", "  Hunt at 8  ");

        CreateScript(dialog)
            .OnDisplaying(leader);

        dialog.Text
              .Should()
              .Be("Hunt at 8");
    }

    [Test]
    public void A_blank_entry_changes_nothing()
    {
        var (guild, leader, _) = CreateGuild();
        var store = new Mock<IStore<Guild>>();
        var dialog = DialogWithText("generic_guild_motd_change_accepted", "The message of the day is set.", "   ");

        CreateScript(dialog, store.Object)
            .OnDisplaying(leader);

        leader.ActiveDialog
              .Get()!
              .Text
              .Should()
              .Be("Nothing was changed.");

        guild.MessageOfTheDay
             .Should()
             .BeNull();

        store.Verify(s => s.Save(It.IsAny<Guild>()), Times.Never);
    }

    [Test]
    public void An_over_long_entry_is_refused()
    {
        var (guild, leader, _) = CreateGuild();
        var store = new Mock<IStore<Guild>>();
        var dialog = DialogWithText("generic_guild_motd_change_accepted", "The message of the day is set.", new string('a', 151));

        CreateScript(dialog, store.Object)
            .OnDisplaying(leader);

        leader.ActiveDialog
              .Get()!
              .Text
              .Should()
              .Be("That message is too long. Keep it to 150 characters.");

        guild.MessageOfTheDay
             .Should()
             .BeNull();

        store.Verify(s => s.Save(It.IsAny<Guild>()), Times.Never);
    }

    [Test]
    public void Accepting_sets_saves_and_tells_online_members()
    {
        var (guild, leader, council) = CreateGuild();
        var store = new Mock<IStore<Guild>>();
        var dialog = DialogWithText("generic_guild_motd_change_accepted", "The message of the day is set.", "Hunt at 8");

        CreateScript(dialog, store.Object)
            .OnDisplaying(leader);

        guild.MessageOfTheDay!
             .Text
             .Should()
             .Be("Hunt at 8");

        guild.MessageOfTheDay
             .SetBy
             .Should()
             .Be("Stahli");

        store.Verify(s => s.Save(guild), Times.Once);

        Mock.Get(council.Client)
            .Verify(c => c.SendServerMessage(ServerMessageType.GuildChat, "TestGuild message of the day: Hunt at 8"), Times.Once);
    }

    [Test]
    public void A_rank_that_loses_the_switch_is_refused_at_the_last_step()
    {
        var (guild, _, council) = CreateGuild();
        var store = new Mock<IStore<Guild>>();
        var dialog = DialogWithText("generic_guild_motd_change_accepted", "The message of the day is set.", "Hunt at 8");

        guild.SetRankPermission(COUNCIL_TIER, GuildPermission.SetMessageOfTheDay, true);
        guild.SetRankPermission(COUNCIL_TIER, GuildPermission.SetMessageOfTheDay, false);

        CreateScript(dialog, store.Object)
            .OnDisplaying(council);

        council.ActiveDialog
               .Get()!
               .Text
               .Should()
               .Be("Your rank can't change the message of the day.");

        guild.MessageOfTheDay
             .Should()
             .BeNull();

        store.Verify(s => s.Save(It.IsAny<Guild>()), Times.Never);
    }

    [Test]
    public void Clearing_removes_the_message_and_saves()
    {
        var (guild, leader, _) = CreateGuild();
        var store = new Mock<IStore<Guild>>();
        var dialog = DialogWithText("generic_guild_motd_clear_accepted", "The message of the day is cleared.");

        guild.SetMessageOfTheDay("Hunt at 8", "Stahli", SET_AT);

        CreateScript(dialog, store.Object)
            .OnDisplaying(leader);

        guild.MessageOfTheDay
             .Should()
             .BeNull();

        store.Verify(s => s.Save(guild), Times.Once);
    }
}
```

In `worktrees/guild-motd-server/Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs`, use `replace_symbol_body` on `GuildPermissionsScriptTests/Only_the_leader_gets_Permissions_at_Quill_and_Aricin`:

```csharp
[Test]
    public void Only_the_leader_gets_Permissions_at_Quill_and_Aricin()
    {
        var (_, leader, member) = CreateGuild();

        GuildMenuAt("Quill", leader)
            .Should()
            .Equal(
                "Buffs",
                "Taxes",
                "Allowances",
                "Ranks",
                "Permissions",
                "Message of the Day",
                "Members",
                "Disband",
                "Leave");

        GuildMenuAt("Aricin", leader)
            .Should()
            .Equal(
                "Ranks",
                "Permissions",
                "Message of the Day",
                "Members",
                "Disband",
                "Leave");

        GuildMenuAt("Quill", member)
            .Should()
            .NotContain("Permissions");

        GuildMenuAt("Aricin", member)
            .Should()
            .NotContain("Permissions");
    }
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildMotd/GuildMessageOfTheDayScriptTests/*" --no-ansi`

Expected: the build fails because `GuildMessageOfTheDayScript` doesn't exist.

- [ ] **Step 3: Write the dialog script**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMessageOfTheDayScript.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.NLog.Logging.Definitions;
using Chaos.NLog.Logging.Extensions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts.Abstractions;
using Chaos.Storage.Abstractions;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.GuildScripts;

/// <summary>
///     The guild's message of the day at Quill and Aricin. Every member can read it. The leader and ranks with the
///     <see cref="GuildPermission.SetMessageOfTheDay" /> switch can change or clear it
/// </summary>
public class GuildMessageOfTheDayScript : GuildScriptBase
{
    private const string INITIAL_KEY = "generic_guild_motd_initial";

    /// <inheritdoc />
    public GuildMessageOfTheDayScript(
        Dialog subject,
        IClientRegistry<IChaosWorldClient> clientRegistry,
        IStore<Guild> guildStore,
        IFactory<Guild> guildFactory,
        ILogger<GuildMessageOfTheDayScript> logger)
        : base(
            subject,
            clientRegistry,
            guildStore,
            guildFactory,
            logger) { }

    /// <summary>
    ///     The text of the first screen: who set the message and when, then the message itself
    /// </summary>
    public static string Describe(GuildMessageOfTheDay? message)
        => message is null
            ? "Your guild has no message of the day."
            : $"Set by {message.SetBy} on {message.SetAt:yyyy-MM-dd} (UTC):\n\n{message.Text}";

    /// <inheritdoc />
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "generic_guild_motd_initial":
                OnDisplayingInitial(source);

                break;

            //nothing to fill in on these two; they only need the permission check
            case "generic_guild_motd_change":
            case "generic_guild_motd_clear_confirmation":
                TryGetEditableGuild(source, out _);

                break;

            case "generic_guild_motd_change_confirmation":
                OnDisplayingChangeConfirmation(source);

                break;

            case "generic_guild_motd_change_accepted":
                OnDisplayingChangeAccepted(source);

                break;

            case "generic_guild_motd_clear_accepted":
                OnDisplayingClearAccepted(source);

                break;
        }
    }

    private void OnDisplayingChangeAccepted(Aisling source)
    {
        if (!TryGetEditableGuild(source, out var guild) || !TryReadNewText(source, out var text))
            return;

        guild.SetMessageOfTheDay(text, source.Name, DateTime.UtcNow);

        //saved now rather than at the next timed save, like the other leader settings
        GuildStore.Save(guild);
        guild.BroadcastMessageOfTheDay();

        Logger.WithTopics(Topics.Entities.Guild, Topics.Actions.Update)
              .WithProperty(guild)
              .WithProperty(source)
              .LogInformation(
                  "Aisling {@AislingName} set the message of the day for guild {@GuildName}: {Message}",
                  source.Name,
                  guild.Name,
                  text);
    }

    private void OnDisplayingChangeConfirmation(Aisling source)
    {
        if (!TryGetEditableGuild(source, out _) || !TryReadNewText(source, out var text))
            return;

        Subject.InjectTextParameters(text);
    }

    private void OnDisplayingClearAccepted(Aisling source)
    {
        if (!TryGetEditableGuild(source, out var guild))
            return;

        guild.ClearMessageOfTheDay();
        GuildStore.Save(guild);

        Logger.WithTopics(Topics.Entities.Guild, Topics.Actions.Update)
              .WithProperty(guild)
              .WithProperty(source)
              .LogInformation("Aisling {@AislingName} cleared the message of the day for guild {@GuildName}", source.Name, guild.Name);
    }

    private void OnDisplayingInitial(Aisling source)
    {
        if (!IsInGuild(source, out var guild, out _))
        {
            Subject.Reply(source, "You are not in a guild.", "top");

            return;
        }

        var message = guild.MessageOfTheDay;

        Subject.InjectTextParameters(Describe(message));

        if (!guild.HasPermission(source.Name, GuildPermission.SetMessageOfTheDay))
            return;

        Subject.InsertOption(0, "Change", "generic_guild_motd_change");

        if (message is not null)
            Subject.InsertOption(1, "Clear", "generic_guild_motd_clear_confirmation");
    }

    /// <summary>
    ///     Whether the member may change the message now. Checked on every screen, because the leader can turn the switch
    ///     off while a member is partway through
    /// </summary>
    private bool TryGetEditableGuild(Aisling source, [MaybeNullWhen(false)] out Guild guild)
    {
        if (!IsInGuild(source, out guild, out _))
        {
            Subject.Reply(source, "You are not in a guild.", "top");

            return false;
        }

        if (guild.HasPermission(source.Name, GuildPermission.SetMessageOfTheDay))
            return true;

        Subject.Reply(source, "Your rank can't change the message of the day.", INITIAL_KEY);

        return false;
    }

    /// <summary>
    ///     The typed message, trimmed. A blank or missing entry changes nothing. An over-long one is refused; that only
    ///     happens with a modified client, because the text box stops at 150
    /// </summary>
    private bool TryReadNewText(Aisling source, [MaybeNullWhen(false)] out string text)
    {
        text = (TryFetchArgs<string>(out var typed) ? typed : string.Empty).Trim();

        if (text.Length == 0)
        {
            Subject.Reply(source, "Nothing was changed.", INITIAL_KEY);

            return false;
        }

        if (text.Length > Guild.MESSAGE_OF_THE_DAY_MAX_LENGTH)
        {
            Subject.Reply(source, $"That message is too long. Keep it to {Guild.MESSAGE_OF_THE_DAY_MAX_LENGTH} characters.", INITIAL_KEY);

            return false;
        }

        return true;
    }
}
```

- [ ] **Step 4: Add the menu option**

In `worktrees/guild-motd-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs`, use `replace_symbol_body` on `GuildManagementScript/OnDisplaying`:

```csharp
public override void OnDisplaying(Aisling source)
    {
        if (Subject.DialogSource.Name is "Quill")
        {
            //ensure the player is still in a guild
            if (!IsInGuild(source, out _, out var sourceRank))
                Subject.AddOption("Create", "generic_guild_create_initial");
            else if (sourceRank.IsLeaderRank)
                Subject.AddOptions(
                    ("Buffs", "generic_guildbuff_initial"),
                    ("Taxes", "generic_guild_tax_initial"),
                    ("Allowances", "generic_guild_allowance_initial"),
                    ("Ranks", "generic_guild_ranks_initial"),
                    ("Permissions", "generic_guild_permissions_initial"),
                    ("Message of the Day", "generic_guild_motd_initial"),
                    ("Members", "generic_guild_members_initial"),
                    ("Disband", "generic_guild_disband_initial"),
                    ("Leave", "generic_guild_leave_initial"));
            else
                Subject.AddOptions(
                    ("Buffs", "generic_guildbuff_initial"),
                    ("Allowances", "generic_guild_allowance_initial"),
                    ("Message of the Day", "generic_guild_motd_initial"),
                    ("Members", "generic_guild_members_initial"),
                    ("Leave", "generic_guild_leave_initial"));
        } else
        {
            //ensure the player is still in a guild
            if (!IsInGuild(source, out _, out var sourceRank))
                Subject.AddOption("Create", "generic_guild_create_initial");
            else if (sourceRank.IsLeaderRank)
                Subject.AddOptions(
                    ("Ranks", "generic_guild_ranks_initial"),
                    ("Permissions", "generic_guild_permissions_initial"),
                    ("Message of the Day", "generic_guild_motd_initial"),
                    ("Members", "generic_guild_members_initial"),
                    ("Disband", "generic_guild_disband_initial"),
                    ("Leave", "generic_guild_leave_initial"));
            else
                Subject.AddOptions(
                    ("Message of the Day", "generic_guild_motd_initial"),
                    ("Members", "generic_guild_members_initial"),
                    ("Leave", "generic_guild_leave_initial"));
        }
    }
```

- [ ] **Step 5: Run the tests to see them pass**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildMotd/GuildMessageOfTheDayScriptTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/*/*" --no-ansi
```

Expected: all pass in both runs.

- [ ] **Step 6: Create the six Unora dialogs**

Create these files in `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-unora/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildMessageOfTheDay/`. Use two-space indentation and no trailing newline, like the other guild dialogs.

`generic_guild_motd_initial.json`:

```json
{
  "options": [
    {
      "dialogKey": "Top",
      "optionText": "Back"
    }
  ],
  "scriptKeys": [
    "GuildMessageOfTheDay"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_motd_initial",
  "text": "{Message}",
  "type": "Menu"
}
```

`generic_guild_motd_change.json`:

```json
{
  "nextDialogKey": "generic_guild_motd_change_confirmation",
  "options": [],
  "scriptKeys": [
    "GuildMessageOfTheDay"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_motd_change",
  "text": "What should the message of the day say? Members see it when they log in.",
  "textBoxLength": 150,
  "type": "DialogTextEntry"
}
```

`generic_guild_motd_change_confirmation.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_motd_change_accepted",
      "optionText": "Yes"
    },
    {
      "dialogKey": "generic_guild_motd_initial",
      "optionText": "Nevermind"
    }
  ],
  "scriptKeys": [
    "GuildMessageOfTheDay"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_motd_change_confirmation",
  "text": "Set the message of the day to this?\n\n{Text}",
  "type": "Menu"
}
```

`generic_guild_motd_change_accepted.json`:

```json
{
  "contextual": true,
  "nextDialogKey": "generic_guild_motd_initial",
  "options": [],
  "scriptKeys": [
    "GuildMessageOfTheDay"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_motd_change_accepted",
  "text": "The message of the day is set.",
  "type": "Normal"
}
```

`generic_guild_motd_clear_confirmation.json`:

```json
{
  "options": [
    {
      "dialogKey": "generic_guild_motd_clear_accepted",
      "optionText": "Yes"
    },
    {
      "dialogKey": "generic_guild_motd_initial",
      "optionText": "Nevermind"
    }
  ],
  "scriptKeys": [
    "GuildMessageOfTheDay"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_motd_clear_confirmation",
  "text": "Clear the message of the day? Members will see nothing at login.",
  "type": "Menu"
}
```

`generic_guild_motd_clear_accepted.json`:

```json
{
  "nextDialogKey": "generic_guild_motd_initial",
  "options": [],
  "scriptKeys": [
    "GuildMessageOfTheDay"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_motd_clear_accepted",
  "text": "The message of the day is cleared.",
  "type": "Normal"
}
```

The flow: `Change` opens the text box. The server adds the typed text to that dialog's `MenuArgs`, then the confirmation and accepted dialogs copy them because they are `contextual`. That is how `TryFetchArgs<string>` finds the text.

- [ ] **Step 7: Check the JSON parses**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-motd-unora/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildMessageOfTheDay
for f in *.json; do python -c "import json,sys; json.load(open(sys.argv[1]))" "$f" && echo "ok $f"; done
```

Expected: six `ok` lines.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMessageOfTheDayScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs", "Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs", "Tests/Chaos.Tests/GuildMotd/GuildMessageOfTheDayScriptTests.cs", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildMessageOfTheDay/*.json"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildMotd/GuildMessageOfTheDayScriptTests/*\" --no-ansi", "acceptanceCriteria": ["every member sees 'Message of the Day' before Members at Quill and Aricin; outsider sees only Create", "first screen shows setter, UTC date and text, or the no-message line", "leader gets Change (+Clear when set); Council without switch gets none; with switch gets Change", "confirmation shows trimmed text", "blank -> 'Nothing was changed.'; 151 chars -> too-long reply; no save", "accepting sets text and setter, saves once, sends chat line to online members", "switch turned off before accepting -> refusal, nothing changes", "clearing removes and saves once", "six Unora dialogs exist and parse"], "modelTier": "standard"}
```

---

### Task 5: Last-seen status on the roster

**Goal:** The roster header shows the online count, and each member line shows `online`, hours or days since last seen, or `unknown`, reading an idle member's save only once.

**Files:**
- Create: `SRV/Chaos/Collections/GuildLastSeen.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberRosterScript.cs` (using, whole class)
- Test: `SRV/Tests/Chaos.Tests/GuildRoster/GuildLastSeenDescribeTests.cs` (new)
- Test: `SRV/Tests/Chaos.Tests/GuildRoster/GuildRosterScriptTests.cs` (new)

**Acceptance Criteria:**
- [ ] `Describe` returns `online`, `unknown`, `under an hour ago` (also for a future time), `1 hour ago`, `23 hours ago` at 23h59m, `1 day ago` at exactly 24h, and `3 days ago`
- [ ] The roster header reads `Members: 3 (1 online)` for one online and two offline members
- [ ] Lines read `Stahli - online`, `Iglis - 3 days ago` (stored time) and `Idle - 5 days ago` (from the save)
- [ ] An idle member's save is read once; the next view, even with a fresh cache, reads nothing
- [ ] A save that can't be read, or has no times, shows `unknown` and stores nothing

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildRoster/*/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/GuildRoster/GuildLastSeenDescribeTests.cs`:

```csharp
#region
using Chaos.Collections;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildRoster;

public sealed class GuildLastSeenDescribeTests
{
    private static readonly DateTime NOW = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void An_online_member_reads_online()
        => GuildLastSeen.Describe(true, NOW.AddDays(-3), NOW)
                        .Should()
                        .Be("online");

    [Test]
    public void No_known_time_reads_unknown()
        => GuildLastSeen.Describe(false, null, NOW)
                        .Should()
                        .Be("unknown");

    //formatter:off
    [Test]
    [Arguments(-5.0, "under an hour ago")]
    [Arguments(5.0, "under an hour ago")]
    [Arguments(60.0, "1 hour ago")]
    [Arguments(119.0, "1 hour ago")]
    [Arguments(1439.0, "23 hours ago")]
    [Arguments(1440.0, "1 day ago")]
    [Arguments(4320.0, "3 days ago")]
    //formatter:on
    public void An_offline_member_reads_the_time_since(double minutesAgo, string expected)
        => GuildLastSeen.Describe(false, NOW.AddMinutes(-minutesAgo), NOW)
                        .Should()
                        .Be(expected);
}
```

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/GuildRoster/GuildRosterScriptTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts;
using Chaos.Services.Storage;
using Chaos.Services.Storage.Abstractions;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
#endregion

namespace Chaos.Tests.GuildRoster;

public sealed class GuildRosterScriptTests
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

    /// <summary>
    ///     Stahli online; Iglis offline with a stored time 3 days ago; Idle offline with no stored time
    /// </summary>
    private static (Guild Guild, Aisling Viewer, ClientRegistry<IChaosWorldClient> Registry) CreateGuild()
    {
        var registry = new ClientRegistry<IChaosWorldClient>();
        var guild = MockGuild.Create(clientRegistry: registry);
        var viewer = CreateOnline(registry, "Stahli");
        var iglis = MockAisling.Create(name: "Iglis");
        var idle = MockAisling.Create(name: "Idle");

        guild.AddMember(viewer, iglis);
        guild.AddMember(iglis, viewer);
        guild.AddMember(idle, viewer);

        //joining records "now" for everyone; put back the state of a guild from before this change
        guild.RestoreLastSeen([KeyValuePair.Create("Iglis", DateTime.UtcNow.AddDays(-3))]);

        return (guild, viewer, registry);
    }

    private static Mock<IFacadeStore<Aisling>> SaveOfIdle(DateTime? lastLogout, DateTime? lastLogin)
    {
        var save = MockAisling.Create(name: "Idle");

        save.Trackers.LastLogout = lastLogout;
        save.Trackers.LastLogin = lastLogin;

        var store = new Mock<IFacadeStore<Aisling>>();

        store.Setup(s => s.Load("Idle"))
             .Returns(save);

        return store;
    }

    private static string ShowRoster(Aisling viewer, IFacadeStore<Aisling> store, IClientRegistry<IChaosWorldClient> registry)
    {
        //a fresh cache each time, so a second view can only skip the save read if the guild stored the time
        var cache = new AislingFacadeCache(
            new MemoryCache(new MemoryCacheOptions()),
            new Mock<ILogger<AislingFacadeCache>>().Object,
            store,
            registry);

        string? shown = null;

        Mock.Get(viewer.Client)
            .Setup(c => c.SendServerMessage(ServerMessageType.ScrollWindow, It.IsAny<string>()))
            .Callback<ServerMessageType, string>((_, text) => shown = text);

        new GuildMemberRosterScript(
            MockDialog.Create("generic_guild_members_roster_initial"),
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildMemberRosterScript>>().Object,
            cache).OnDisplaying(viewer);

        return shown!;
    }

    [Test]
    public void The_roster_shows_the_online_count_and_each_status()
    {
        var (_, viewer, registry) = CreateGuild();
        var store = SaveOfIdle(DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(-6));

        var roster = ShowRoster(viewer, store.Object, registry);

        roster.Should()
              .Contain("Members: 3 (1 online)");

        roster.Should()
              .Contain("Stahli - online");

        roster.Should()
              .Contain("Iglis - 3 days ago");

        roster.Should()
              .Contain("Idle - 5 days ago");
    }

    [Test]
    public void An_idle_members_save_is_read_only_once()
    {
        var (guild, viewer, registry) = CreateGuild();
        var store = SaveOfIdle(DateTime.UtcNow.AddDays(-5), null);

        ShowRoster(viewer, store.Object, registry);
        var second = ShowRoster(viewer, store.Object, registry);

        store.Verify(s => s.Load("Idle"), Times.Once);

        guild.TryGetLastSeen("Idle", out _)
             .Should()
             .BeTrue();

        second.Should()
              .Contain("Idle - 5 days ago");
    }

    [Test]
    public void A_save_with_no_times_shows_unknown()
    {
        var (guild, viewer, registry) = CreateGuild();
        var store = SaveOfIdle(null, null);

        ShowRoster(viewer, store.Object, registry)
            .Should()
            .Contain("Idle - unknown");

        guild.TryGetLastSeen("Idle", out _)
             .Should()
             .BeFalse();
    }

    [Test]
    public void An_unreadable_save_shows_unknown_and_is_tried_again_later()
    {
        var (guild, viewer, registry) = CreateGuild();
        var store = new Mock<IFacadeStore<Aisling>>();

        store.Setup(s => s.Load("Idle"))
             .Throws(new InvalidOperationException("No aisling data exists"));

        ShowRoster(viewer, store.Object, registry)
            .Should()
            .Contain("Idle - unknown");

        ShowRoster(viewer, store.Object, registry);

        store.Verify(s => s.Load("Idle"), Times.Exactly(2));

        guild.TryGetLastSeen("Idle", out _)
             .Should()
             .BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildRoster/*/*" --no-ansi`

Expected: the build fails because `GuildLastSeen` doesn't exist and `GuildMemberRosterScript` has no constructor that takes an `AislingFacadeCache`.

- [ ] **Step 3: Write the status helper**

Create `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Chaos/Collections/GuildLastSeen.cs`:

```csharp
namespace Chaos.Collections;

/// <summary>
///     The last-seen text on the guild roster
/// </summary>
public static class GuildLastSeen
{
    /// <summary>
    ///     "online", "unknown", "under an hour ago", "N hours ago" or "N days ago", in whole hours or days. A time in the
    ///     future (clock drift) reads as under an hour ago
    /// </summary>
    public static string Describe(bool online, DateTime? lastSeen, DateTime now)
    {
        if (online)
            return "online";

        if (lastSeen is not { } seen)
            return "unknown";

        var elapsed = now - seen;

        if (elapsed < TimeSpan.FromHours(1))
            return "under an hour ago";

        if (elapsed < TimeSpan.FromDays(1))
        {
            var hours = (int)elapsed.TotalHours;

            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        var days = (int)elapsed.TotalDays;

        return days == 1 ? "1 day ago" : $"{days} days ago";
    }
}
```

- [ ] **Step 4: Rewrite the roster**

In `worktrees/guild-motd-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberRosterScript.cs`, use `replace_content` (literal) with the needle `using Chaos.Storage.Abstractions;` and this two-line replacement:

```csharp
using Chaos.Services.Storage;
using Chaos.Storage.Abstractions;
```

Then use `replace_symbol_body` on `GuildMemberRosterScript` (the class):

```csharp
public class GuildMemberRosterScript : GuildScriptBase
{
    private readonly AislingFacadeCache FacadeCache;

    /// <inheritdoc />
    public GuildMemberRosterScript(
        Dialog subject,
        IClientRegistry<IChaosWorldClient> clientRegistry,
        IStore<Guild> guildStore,
        IFactory<Guild> guildFactory,
        ILogger<GuildMemberRosterScript> logger,
        AislingFacadeCache facadeCache)
        : base(
            subject,
            clientRegistry,
            guildStore,
            guildFactory,
            logger)
        => FacadeCache = facadeCache;

    /// <inheritdoc />
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            //occurs when you click "Roster"
            case "generic_guild_members_roster_initial":
            {
                OnDisplayingInitial(source);

                break;
            }
        }
    }

    /// <summary>
    ///     The guild's stored time for an offline member. A member who hasn't logged out since last-seen times were added
    ///     has none, so their save is read once and the time is stored for next time
    /// </summary>
    private DateTime? FindLastSeen(Guild guild, string memberName)
    {
        if (guild.TryGetLastSeen(memberName, out var stored))
            return stored;

        //Get logs a warning and returns null when the save can't be read
        var trackers = FacadeCache.Get(memberName)
                                  ?.Trackers;

        var fromSave = Later(trackers?.LastLogout, trackers?.LastLogin);

        if (fromSave is { } when)
            guild.RecordLastSeen(memberName, when);

        return fromSave;
    }

    private static DateTime? Later(DateTime? first, DateTime? second)
        => (first, second) switch
        {
            ({ } a, { } b) => a > b ? a : b,
            ({ } a, null)  => a,
            (null, { } b)  => b,
            _              => null
        };

    private void OnDisplayingInitial(Aisling source)
    {
        //ensure the player is still in a guild
        if (!IsInGuild(source, out var guild, out _))
        {
            Subject.Reply(source, "You are not in a guild", "top");

            return;
        }

        //build a message containing the guild roster
        /*
        Guild: ChaosBros
        Members: 3 (1 online)

        Rank: Leader
        ---------------
        Sichi - online

        Rank: Officer
        ---------------
        Sichii - 3 days ago
        Sichiii - under an hour ago

        Rank: Member
        ---------------

        Rank: Applicant
        ---------------

         */
        var now = DateTime.UtcNow;

        var online = guild.GetOnlineMembers()
                          .Select(aisling => aisling.Name)
                          .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var builder = new StringBuilder();
        var separator = new string('-', 15);

        builder.AppendLine($"Guild: {guild.Name}");
        builder.AppendLine($"Members: {guild.GetMemberNames().Count()} ({online.Count} online)");
        builder.AppendLine();

        foreach (var rank in guild.GetRanks())
        {
            builder.AppendLine($"Rank: {rank.Name}");
            builder.AppendLine(separator);

            foreach (var member in rank.GetMemberNames())
            {
                var isOnline = online.Contains(member);
                var lastSeen = isOnline ? null : FindLastSeen(guild, member);

                builder.AppendLine($"{member} - {GuildLastSeen.Describe(isOnline, lastSeen, now)}");
            }

            builder.AppendLine();
        }

        //send the message to the player
        source.Client.SendServerMessage(ServerMessageType.ScrollWindow, builder.ToString());
    }
}
```

The script is created through dependency injection, and `AislingFacadeCache` is already registered as a singleton (`ServiceCollectionExtensions`), so no registration change is needed.

- [ ] **Step 5: Run the tests to see them pass**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildRoster/*/*" --no-ansi
```

Expected: every `GuildLastSeenDescribeTests`, `GuildRosterScriptTests` and `GuildLastSeenDataTests` test passes.

```json:metadata
{"files": ["Chaos/Collections/GuildLastSeen.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberRosterScript.cs", "Tests/Chaos.Tests/GuildRoster/GuildLastSeenDescribeTests.cs", "Tests/Chaos.Tests/GuildRoster/GuildRosterScriptTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildRoster/*/*\" --no-ansi", "acceptanceCriteria": ["Describe covers online, unknown, under an hour (incl. future), 1 hour, 23 hours, 1 day, 3 days", "header 'Members: 3 (1 online)'", "lines 'Stahli - online', 'Iglis - 3 days ago', 'Idle - 5 days ago'", "idle save read once; second view with fresh cache reads nothing", "unreadable save or no times -> 'unknown', nothing stored"], "modelTier": "standard"}
```

---

### Task 6: Commit the full implementation

**Goal:** Every test passes except the two known failures, the ideas doc marks 44 and 45 built, and the server branch, the Unora branch and this plan are each committed once.

**Files:**
- Modify: `UNO/docs/guild-hall-ideas.md`
- Commit: every file listed in Tasks 1–5, in `SRV` and `UNO`
- Commit: `Chaos.Client/docs/superpowers/plans/2026-09-25-guild-roster-and-message-of-the-day.md` and its `.tasks.json` (on `Chaos.Client` `main`, by path)

**Acceptance Criteria:**
- [ ] The full `Chaos.Tests` run fails only `GiveAbility` and `OnItemDroppedOn` (stackable)
- [ ] `docs/guild-hall-ideas.md` says five ideas were built on 2026-09-25, says the leader switches eight powers, lists the two new hall behaviours, and marks ideas 44 and 45 built
- [ ] `SRV` has one new commit on `feat/guild-roster-motd` with only this plan's server files
- [ ] `UNO` has one new commit on `feat/guild-roster-motd` with only the six dialog files and the ideas doc, and no `Custom Client Mods` build output
- [ ] The plan and tasks file are committed on `Chaos.Client` `main`, with nothing else in that commit

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server log --oneline -1` → the message-of-the-day commit

**Steps:**

- [ ] **Step 1: Run the full server test suite**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi
```

Expected: every test passes except `GiveAbility` and `OnItemDroppedOn` (stackable). Any other failure: stop and fix it in the task that caused it.

- [ ] **Step 2: Update the ideas doc**

In `C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-unora/docs/guild-hall-ideas.md`, make four edits with the Edit tool. Each is an exact old line and new text.

Edit 1. Old:

```text
Three ideas were built on 2026-09-25: the guild cloak (idea 15), guild tuition (idea 32) and guild rank permissions (idea 51).
```

New:

```text
Five ideas were built on 2026-09-25: the guild cloak (idea 15), guild tuition (idea 32), guild rank permissions (idea 51), the message of the day (idea 44) and last seen on the roster (idea 45).
```

Edit 2. Old:

```text
- At Quill or Aricin, the leader switches seven powers on or off for each lower rank, such as taking gold from the guild bank.
```

New (three lines):

```text
- At Quill or Aricin, the leader switches eight powers on or off for each lower rank, such as taking gold from the guild bank.
- At Quill or Aricin, every member can read the guild's message of the day. The leader, and ranks given the switch, can change it. Members see it as a guild chat line when they log in.
- The roster shows each member as online, or how many hours or days ago they were last seen.
```

Edit 3. Old:

```text
44. **Message of the day.** The leader sets a message that members see when they log in. *Light.*
```

New:

```text
44. **Message of the day.** The leader sets a message that members see when they log in. *Light.* **Built 2026-09-25** (branch `feat/guild-roster-motd`); spec and plan in `Chaos.Client/docs/superpowers/`.
```

Edit 4. Old:

```text
45. **Last seen on the roster.** The roster shows when each member last logged in, so leaders can find inactive members. *Light to medium.*
```

New:

```text
45. **Last seen on the roster.** The roster shows when each member last logged in, so leaders can find inactive members. *Light to medium.* **Built 2026-09-25** (branch `feat/guild-roster-motd`); spec and plan in `Chaos.Client/docs/superpowers/`.
```

Leave idea 51's own text ("seven powers") alone. It records what was built on that day.

- [ ] **Step 3: Commit the server worktree**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-motd-server
git status --short
git add -- Chaos.DarkAges/Definitions/Enums.cs Chaos/Collections/GuildPermissionRules.cs Chaos/Collections/GuildMessageOfTheDay.cs Chaos/Collections/GuildLastSeen.cs Chaos/Collections/Guild.cs Chaos.Schemas/Guilds/GuildMessageOfTheDaySchema.cs Chaos.Schemas/Guilds/GuildSchema.cs Chaos/Services/MapperProfiles/GuildMapperProfile.cs Chaos/Scripting/AislingScripts/DefaultAislingScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMessageOfTheDayScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberRosterScript.cs Tests/Chaos.Tests/GuildPermissions/GuildPermissionDataTests.cs Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs Tests/Chaos.Tests/GuildMotd Tests/Chaos.Tests/GuildRoster
git status --short
git commit -F - <<'EOF'
Add a guild message of the day and last-seen times on the roster

Members see the guild's message of the day as one guild chat line when
they log in, and can read it at Quill or Aricin. The leader, and any
rank given the new "Set the message of the day" switch, can change or
clear it. The switch starts off for every rank.

The roster now shows how many members are online, and each member as
online or how many hours or days ago they were last seen. The guild
records the time at logout. A member who hasn't logged out since this
change is read once from their save.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

Before committing, the second `git status --short` must show nothing staged outside the list above. If `appsettings.json` or `launchSettings.json` show as modified, leave them unstaged.

- [ ] **Step 4: Commit the Unora worktree**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-motd-unora
git status --short
git add -- "Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildMessageOfTheDay" docs/guild-hall-ideas.md
git status --short
git commit -F - <<'EOF'
Add the guild message of the day dialogs for Quill and Aricin

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

Leave any modified `Custom Client Mods/**/obj` files unstaged. They are build output.

- [ ] **Step 5: Commit the plan on Chaos.Client `main`**

`docs/` is gitignored in Chaos.Client, so use `-f`. Commit by path so nothing else in the shared checkout is included:

```bash
cd /c/Users/Michael/Documents/GitHub/Chaos.Client
git add -f -- docs/superpowers/plans/2026-09-25-guild-roster-and-message-of-the-day.md docs/superpowers/plans/2026-09-25-guild-roster-and-message-of-the-day.md.tasks.json
git commit -F - -- docs/superpowers/plans/2026-09-25-guild-roster-and-message-of-the-day.md docs/superpowers/plans/2026-09-25-guild-roster-and-message-of-the-day.md.tasks.json <<'EOF'
Add the guild roster last-seen and message of the day implementation plan

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 6: Report the in-game check**

Merging, and pointing Chaos.Client's `Chaos-Server` submodule at the merged master, come next (superpowers-extended-cc:finishing-a-development-branch). They aren't part of this task. Report the check for the user to run in game after the merge:

1. As the leader at Quill: Message of the Day, Change, type a message, Yes. Online members see `<guild> message of the day: <text>` in guild chat.
2. Log a member out and in. They see the line after the "has appeared online" notices.
3. As a Council member at Quill: Message of the Day shows the text, who set it and the date, but no Change or Clear.
4. As the leader at Aricin: Permissions, Council, `Set the message of the day: Off`. The line changes to On. The Council member now gets Change.
5. As the leader: Message of the Day, Clear, Yes. The next login shows no line.
6. Open the roster (Members, Roster). The header shows the online count, online members read `online`, and a member who has been away reads a day count.
7. Restart the server. The message, the switch and the roster times are still there.

```json:metadata
{"files": ["UNO:docs/guild-hall-ideas.md"], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-motd-server log --oneline -1", "acceptanceCriteria": ["full Chaos.Tests run fails only GiveAbility and OnItemDroppedOn (stackable)", "ideas doc: five built, two new hall bullets, ideas 44 and 45 marked built", "one SRV commit with only this plan's server files", "one UNO commit with only the six dialogs and the ideas doc, no Custom Client Mods obj output", "plan and tasks file committed alone on Chaos.Client main"], "modelTier": "mechanical"}
```
