# Guild Rank Permissions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The guild leader switches seven powers on or off for each lower rank: taking items from the guild bank, taking gold from it, paying for buffs with guild gold, buying hall upgrades, admitting, kicking, and promoting/demoting.

**Architecture:** A `[Flags] enum GuildPermission` in `Chaos.DarkAges` names the seven switches. Each `GuildRank` stores its set in a `Permissions` property, saved by name in its tier file. A missing value loads as the rules from before this change. `GuildRank.Allows` holds the "the leader may always" rule, and `Guild.HasPermission` asks it for a member. The static `GuildPermissionRules` says which switches each tier can have, in menu order, with their menu text. The bank, buff, hall and member scripts swap their fixed officer checks for `HasPermission`. A new `GuildPermissionsScript` runs the leader's Permissions menu at Quill and Aricin.

**Tech Stack:** C# 14 / .NET 10, Chaos-Server dialog scripts, `IStore<Guild>` JSON storage with `System.Text.Json` (`JsonStringEnumConverter`), TUnit + FluentAssertions + Moq, Unora JSON dialog templates.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-09-25-guild-rank-permissions-design.md` (committed on `main` as `5951303`).

## Global Constraints

- **Work only in the two worktrees from Task 0.** Never edit, stage, stash, reset or switch branches in the shared checkouts (`C:/Users/Michael/Documents/GitHub/Chaos.Client`, its `Chaos-Server` submodule, `C:/Users/Michael/Documents/GitHub/Unora`). Other Claude sessions work in them. Never touch the other folders under `C:/Users/Michael/Documents/GitHub/worktrees/`.
  - `SRV` = `C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server` (Chaos-Server, branch `feat/guild-rank-permissions` from `master`)
  - `UNO` = `C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-unora` (Unora, branch `feat/guild-rank-permissions` from `main`)
- **Do not commit** in Tasks 0–6. Leave all changes in the worktrees. Task 7 makes one commit per repo (at-end strategy).
- **Test projects are TUnit executables.** Use `dotnet run --project ... -- --treenode-filter "..." --no-ansi`, never `dotnet test`. The filter pattern is `/Assembly/Namespace/Class/Method`.
- **Two server tests already fail on master:** `GiveAbility` and `OnItemDroppedOn` (stackable). Leave them alone. Every other test must pass.
- **If a build fails with MSB3027** (a file is locked), something is running from that output folder. Stop and report. Don't kill any process.
- **Serena for C#.** Read and edit C# files with Serena's tools, as the user's global CLAUDE.md requires. Serena paths are relative to `C:/Users/Michael/Documents/GitHub`, so a server file is `worktrees/rank-permissions-server/Chaos/...`. Create brand-new C# files with the Write tool. JSON files use the built-in Read/Edit/Write tools.
- **Line endings.** The shared checkout's files use LF, but a fresh worktree may check out CRLF (`core.autocrlf` is `true`). If a multi-line literal `replace_content` needle doesn't match, retry the same needle in regex mode with each line break written as `\r?\n` and the other regex characters escaped. When a replacement adds lines, pass real line breaks in the replacement string, never the two characters `\n`.
- **Never stage** `Chaos/appsettings.json`, `launchSettings.json`, or anything under `UNO/Custom Client Mods/**/obj`.
- **The game font is 6×12 ASCII.** Use only ASCII in dialog text and messages. No em dashes, no curly quotes.
- **Exact values:**

  | Value | Setting |
  |---|---|
  | Enum | `GuildPermission` in `Chaos.DarkAges/Definitions/Enums.cs`: `None = 0`, `WithdrawItems = 1`, `WithdrawGold = 2`, `GuildGoldBuffs = 4`, `BuyHallRooms = 8`, `Admit = 16`, `Kick = 32`, `PromoteDemote = 64`. No `All` value. |
  | Council default | `GuildPermissionRules.COUNCIL_DEFAULT`, all seven |
  | Member and Applicant default | `GuildPermission.None` |
  | Switches per tier (menu order) | Tier 1: all seven. Tier 2: all but `PromoteDemote`. Tier 3: all but `Kick` and `PromoteDemote`. Tier 0: none. |
  | Menu labels | `Take items from the bank`, `Take gold from the bank`, `Pay for buffs with guild gold`, `Buy hall upgrades`, `Admit new members`, `Kick lower ranks`, `Promote and demote` |
  | Menu line | `<label>: On` or `<label>: Off`, then a last line `Done` |
  | Leader menu option | `Permissions`, right after `Ranks`, at Quill and at Aricin |
  | Script key | `guildPermissions` (class `GuildPermissionsScript`) |
  | Dialog keys | `generic_guild_permissions_initial`, `generic_guild_permissions_rank` |
  | Refusals | `Your rank can't take items from the guild bank.` / `Your rank can't take gold from the guild bank.` / `Your rank can't pay for buffs with guild gold.` / `Your rank can't buy hall upgrades.` / `Only the guild leader can buy the guild house deed.` / `Your rank can't admit new members.` / `Your rank can't kick members.` / `Your rank can't promote members.` / `Your rank can't demote members.` / `Only the leader can change permissions.` |

**User decisions (already made):**
- Rank permissions is idea 51 from the 2026-09-25 guild ideas list, answering the "Guild Rank Permissions" player thread.
- The seven switches are: bank items, bank gold, buffs from guild gold, buying hall upgrades, admit, kick, and promote/demote.
- Taking items and taking gold are separate switches.
- Admit, kick and promote/demote are three separate switches.
- The rank-gap rules don't change. The leader always keeps every power.
- The leader edits switches in a dialog menu at Quill and Aricin (approach A), not a client window.
- Storage is one flags enum per rank in its tier file (approach 1). Missing values load as today's rules.
- Each flip saves the guild at once and writes a server log line. No guild chat message.
- The house deed stays leader-only, with a leader check added at the purchase step.

**Changes from the spec (decided while planning):**
1. The rank menu reads "What the {RankName} rank may do. Choose a line to switch it on or off." The spec's "What {RankName} may do." reads badly with rank names like "Member".
2. `GuildRank.Allows(permission)` holds the "leader may always" rule in one place. `Guild.HasPermission` and `Guild.GetBankPermissions` both use it.
3. `GuildRank`'s constructor takes an optional `permissions` argument. When it's missing, the rank gets `GuildPermissionRules.DefaultFor(tier)`. New guilds get the defaults through the `Guild` constructor, and old files through the mapper.
4. A click on a switch line sets the opposite of what that line showed, read from the line's text. A guild can have more than one leader, so if two leaders click the same line at once, each click does what its leader saw.
5. `WorldServer`'s bank handler has no unit tests today, and this plan doesn't add any. `GetBankPermissions` is unit-tested, and the in-game check covers the two refusal messages.

---

## File map

| Repo | File | Responsibility |
|---|---|---|
| SRV | `Chaos.DarkAges/Definitions/Enums.cs` | `GuildPermission` enum |
| SRV | `Chaos/Collections/GuildPermissionRules.cs` (new) | switches per tier, defaults, menu labels |
| SRV | `Chaos/Collections/GuildRank.cs` | `Permissions`, `Allows`, `SetPermission` |
| SRV | `Chaos/Collections/Guild.cs` | `HasPermission`, `SetRankPermission`, `GetBankPermissions` |
| SRV | `Chaos.Schemas/Guilds/GuildRankSchema.cs` | saved `Permissions` |
| SRV | `Chaos/Services/MapperProfiles/GuildMapperProfile.cs` | map `Permissions` both ways |
| SRV | `Chaos/Collections/BankPermissions.cs` | `CanWithdrawItems`, `CanWithdrawGold` |
| SRV | `Chaos/Services/Servers/WorldServer.cs` | separate item and gold withdraw checks |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBuffScript.cs` | buff switch; payment choice read by option text |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs` | hall switch; house deed leader check |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs`, `GuildMemberAdmitScript.cs`, `GuildMemberKickScript.cs`, `GuildMemberPromoteScript.cs`, `GuildMemberDemoteScript.cs` | member switches |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs` | Permissions option |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildPermissionsScript.cs` (new) | the leader's Permissions menu |
| SRV | `Tests/Chaos.Tests/GuildPermissions/*.cs` (new), `Tests/Chaos.Tests/BankPermissionsTests.cs`, `Tests/Chaos.Tests/ComplexActionHelperTests.cs` | tests |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions/generic_guild_permissions_initial.json`, `generic_guild_permissions_rank.json` (new) | the two menus |

---

### Task 0: Create the two worktrees

**Goal:** Isolated `feat/guild-rank-permissions` branches for the server and Unora, so no shared checkout is touched.

**Files:**
- Create: worktrees `SRV` and `UNO` (see Global Constraints)

**Acceptance Criteria:**
- [ ] `git -C <each worktree> branch --show-current` prints `feat/guild-rank-permissions`
- [ ] `SRV/Tests/Chaos.Tests` builds, and `GuildTests` passes before any change

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server branch --show-current` → `feat/guild-rank-permissions`

**Steps:**

- [ ] **Step 1: Check the worktrees don't exist yet**

```bash
ls /c/Users/Michael/Documents/GitHub/worktrees/
```

Expected: no `rank-permissions-server` or `rank-permissions-unora`. If either exists, stop and report. A previous run may have started them.

- [ ] **Step 2: Create the worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server -b feat/guild-rank-permissions master
git -C Unora -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/rank-permissions-unora -b feat/guild-rank-permissions main
```

Pass `core.longpaths` with `-c` only. Never write it to the repo config.

- [ ] **Step 3: Baseline build and tests**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildTests/*" --no-ansi
```

Expected: `Build succeeded`, and every `GuildTests` test passes.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server branch --show-current", "acceptanceCriteria": ["both worktrees on feat/guild-rank-permissions", "SRV Chaos.Tests builds and GuildTests pass before any change"], "modelTier": "mechanical"}
```

---

### Task 1: Permission data on ranks and guilds

**Goal:** Each rank carries a saved set of permission switches, with today's rules as the default, and the guild can answer "may this member do X?" and change a rank's switch.

**Files:**
- Modify: `SRV/Chaos.DarkAges/Definitions/Enums.cs` (add `GuildPermission` after `GuildCloakReviewAction`)
- Create: `SRV/Chaos/Collections/GuildPermissionRules.cs`
- Modify: `SRV/Chaos/Collections/GuildRank.cs` (using, `Permissions`, constructor, `Allows`, `SetPermission`)
- Modify: `SRV/Chaos.Schemas/Guilds/GuildRankSchema.cs` (using, `Permissions`)
- Modify: `SRV/Chaos/Services/MapperProfiles/GuildMapperProfile.cs` (both rank maps)
- Modify: `SRV/Chaos/Collections/Guild.cs` (add `HasPermission` and `SetRankPermission` after `SetRankAllowance`)
- Test: `SRV/Tests/Chaos.Tests/GuildPermissions/GuildPermissionDataTests.cs`

**Acceptance Criteria:**
- [ ] A rank file with no `permissions` loads as all seven for tier 1 and `None` for tiers 0, 2 and 3
- [ ] A new guild's ranks start with the same defaults
- [ ] Permissions survive a map round trip and are written as names, such as `"permissions":"WithdrawItems, Admit"`
- [ ] `HasPermission` is true for the leader, false for a non-member, and the rank's switch otherwise; `RankOf` copies keep the value
- [ ] `SetRankPermission` throws `InvalidOperationException` for tier 0
- [ ] `GuildPermissionRules.ForTier` returns 7, 6 and 5 switches for tiers 1, 2 and 3 in menu order, and none for tier 0

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildPermissionDataTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildPermissions/GuildPermissionDataTests.cs`:

```csharp
#region
using System.Text.Json;
using System.Text.Json.Serialization;
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Schemas.Guilds;
using Chaos.Services.MapperProfiles;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildPermissions;

public sealed class GuildPermissionDataTests
{
    //Guild's default hierarchy: Leader 0, Council 1, Member 2, Applicant 3
    private const int APPLICANT_TIER = 3;
    private const int COUNCIL_TIER = 1;
    private const int LEADER_TIER = 0;
    private const int MEMBER_TIER = 2;

    private static GuildMapperProfile CreateProfile() => new(MockChannelService.Create(), new ClientRegistry<IChaosWorldClient>());

    private static (Guild Guild, Aisling Leader, Aisling Member) CreateGuild(int memberTier)
    {
        var guild = MockGuild.Create();
        var leader = MockAisling.Create(name: "Stahli");
        var member = MockAisling.Create(name: "Iglis");

        guild.AddMember(leader, member);
        guild.ChangeRank(leader.Name, LEADER_TIER, member);
        guild.AddMember(member, leader);
        guild.ChangeRank(member.Name, memberTier, leader);

        return (guild, leader, member);
    }

    //formatter:off
    [Test]
    [Arguments(LEADER_TIER, GuildPermission.None)]
    [Arguments(COUNCIL_TIER, GuildPermissionRules.COUNCIL_DEFAULT)]
    [Arguments(MEMBER_TIER, GuildPermission.None)]
    [Arguments(APPLICANT_TIER, GuildPermission.None)]
    //formatter:on
    public void A_rank_file_without_permissions_loads_with_the_old_rules(int tier, GuildPermission expected)
        => CreateProfile()
           .Map(
               new GuildRankSchema
               {
                   RankName = "Rank",
                   Tier = tier
               })
           .Permissions
           .Should()
           .Be(expected);

    [Test]
    public void A_new_guild_starts_with_the_old_rules()
    {
        var permissions = MockGuild.Create()
                                   .GetRanks()
                                   .ToDictionary(rank => rank.Tier, rank => rank.Permissions);

        permissions[COUNCIL_TIER]
            .Should()
            .Be(GuildPermissionRules.COUNCIL_DEFAULT);

        permissions[MEMBER_TIER]
            .Should()
            .Be(GuildPermission.None);

        permissions[APPLICANT_TIER]
            .Should()
            .Be(GuildPermission.None);
    }

    [Test]
    public void Permissions_survive_a_round_trip()
    {
        var profile = CreateProfile();
        var rank = new GuildRank("Member", MEMBER_TIER, ["Iglis"]);
        rank.SetPermission(GuildPermission.WithdrawItems, true);
        rank.SetPermission(GuildPermission.Admit, true);

        profile.Map(profile.Map(rank))
               .Permissions
               .Should()
               .Be(GuildPermission.WithdrawItems | GuildPermission.Admit);
    }

    [Test]
    public void Permissions_are_saved_as_names()
    {
        //the same naming policy and enum converter the server's storage uses
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        var schema = new GuildRankSchema
        {
            RankName = "Member",
            Tier = MEMBER_TIER,
            Permissions = GuildPermission.WithdrawItems | GuildPermission.Admit
        };

        JsonSerializer.Serialize(schema, options)
                      .Should()
                      .Contain("\"permissions\":\"WithdrawItems, Admit\"");

        JsonSerializer.Deserialize<GuildRankSchema>("""{"rankName":"Member","tier":2,"permissions":"WithdrawItems, Admit"}""", options)!
                      .Permissions
                      .Should()
                      .Be(GuildPermission.WithdrawItems | GuildPermission.Admit);
    }

    [Test]
    public void The_leader_may_do_everything_and_an_outsider_nothing()
    {
        var (guild, leader, _) = CreateGuild(MEMBER_TIER);

        guild.HasPermission(leader.Name, GuildPermission.WithdrawGold)
             .Should()
             .BeTrue();

        guild.HasPermission("Stranger", GuildPermission.WithdrawGold)
             .Should()
             .BeFalse();
    }

    [Test]
    public void A_member_may_do_what_their_rank_has_switched_on()
    {
        var (guild, _, member) = CreateGuild(MEMBER_TIER);

        guild.HasPermission(member.Name, GuildPermission.WithdrawItems)
             .Should()
             .BeFalse();

        guild.SetRankPermission(MEMBER_TIER, GuildPermission.WithdrawItems, true);

        guild.HasPermission(member.Name, GuildPermission.WithdrawItems)
             .Should()
             .BeTrue();

        guild.HasPermission(member.Name, GuildPermission.WithdrawGold)
             .Should()
             .BeFalse();

        //RankOf hands out a deep copy; the copy must carry the switches
        guild.RankOf(member.Name)
             .Permissions
             .Should()
             .Be(GuildPermission.WithdrawItems);

        guild.SetRankPermission(MEMBER_TIER, GuildPermission.WithdrawItems, false);

        guild.HasPermission(member.Name, GuildPermission.WithdrawItems)
             .Should()
             .BeFalse();
    }

    [Test]
    public void The_leader_rank_has_no_permissions_to_change()
    {
        var guild = MockGuild.Create();

        var act = () => guild.SetRankPermission(LEADER_TIER, GuildPermission.Kick, false);

        act.Should()
           .Throw<InvalidOperationException>();
    }

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
                                GuildPermission.PromoteDemote);

        GuildPermissionRules.ForTier(MEMBER_TIER)
                            .Should()
                            .Equal(
                                GuildPermission.WithdrawItems,
                                GuildPermission.WithdrawGold,
                                GuildPermission.GuildGoldBuffs,
                                GuildPermission.BuyHallRooms,
                                GuildPermission.Admit,
                                GuildPermission.Kick);

        GuildPermissionRules.ForTier(APPLICANT_TIER)
                            .Should()
                            .Equal(
                                GuildPermission.WithdrawItems,
                                GuildPermission.WithdrawGold,
                                GuildPermission.GuildGoldBuffs,
                                GuildPermission.BuyHallRooms,
                                GuildPermission.Admit);

        GuildPermissionRules.ForTier(LEADER_TIER)
                            .Should()
                            .BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to check they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildPermissionDataTests/*" --no-ansi`

Expected: the build fails with `error CS0246: The type or namespace name 'GuildPermission' could not be found`.

- [ ] **Step 3: Add the enum**

Serena `insert_after_symbol`, name path `GuildCloakReviewAction`, file `worktrees/rank-permissions-server/Chaos.DarkAges/Definitions/Enums.cs`. The new enum must land before the file's final `#endregion`. Body:

```csharp

/// <summary>
///     What a guild rank below the leader may do. The leader may always do everything. Saved by name in each rank's
///     file, so there is deliberately no "All" value: a saved "All" would quietly grant any permission added later
/// </summary>
[Flags]
public enum GuildPermission
{
    None = 0,

    /// <summary>Take items out of the guild bank.</summary>
    WithdrawItems = 1,

    /// <summary>Take gold out of the guild bank.</summary>
    WithdrawGold = 2,

    /// <summary>Pay for a guild buff with guild bank gold.</summary>
    GuildGoldBuffs = 4,

    /// <summary>Buy hall rooms and the guild cloak deed from Tibbs, with the buyer's own gold.</summary>
    BuyHallRooms = 8,

    /// <summary>Admit someone in the Abel tavern.</summary>
    Admit = 16,

    /// <summary>Kick someone of a lower rank.</summary>
    Kick = 32,

    /// <summary>Promote and demote, within the rank-gap rules.</summary>
    PromoteDemote = 64
}
```

- [ ] **Step 4: Add the rules table**

Create `SRV/Chaos/Collections/GuildPermissionRules.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Collections;

/// <summary>
///     Which guild permissions each rank tier can have, what each tier starts with, and each permission's menu text
/// </summary>
/// <remarks>
///     The tiers follow the rank-gap rules in the member scripts. A kick or demotion needs a target at least one tier
///     below, and a non-leader's promotion needs a target two tiers below. So a Member (tier 2) can't promote or demote
///     anyone, and an Applicant (tier 3) can't kick, promote or demote anyone
/// </remarks>
public static class GuildPermissionRules
{
    /// <summary>
    ///     Every permission. A Council rank starts with this, which matches the rules from before permissions could be
    ///     changed
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
        GuildPermission.PromoteDemote
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
            GuildPermission.WithdrawItems  => "Take items from the bank",
            GuildPermission.WithdrawGold   => "Take gold from the bank",
            GuildPermission.GuildGoldBuffs => "Pay for buffs with guild gold",
            GuildPermission.BuyHallRooms   => "Buy hall upgrades",
            GuildPermission.Admit          => "Admit new members",
            GuildPermission.Kick           => "Kick lower ranks",
            GuildPermission.PromoteDemote  => "Promote and demote",
            _                              => throw new ArgumentOutOfRangeException(nameof(permission), permission, null)
        };
}
```

- [ ] **Step 5: Give ranks their permissions**

In `worktrees/rank-permissions-server/Chaos/Collections/GuildRank.cs`, with Serena `replace_content` (literal):

1. Needle `using Chaos.Models.World;` → replacement:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
```

2. Needle `    public int TrainingAllowance { get; private set; }` → replacement:

```csharp
    public int TrainingAllowance { get; private set; }

    /// <summary>
    ///     What members of this rank may do. The leader rank ignores this: the leader may always do everything
    /// </summary>
    public GuildPermission Permissions { get; private set; }
```

3. Needle (the constructor with its last doc lines):

```csharp
    /// <param name="memberNames">
    ///     The members to populate the rank with
    /// </param>
    public GuildRank(string name, int tier, ICollection<string>? memberNames = null)
    {
        memberNames ??= [];

        Name = name;
        Tier = tier;
        MemberNames = new HashSet<string>(memberNames, StringComparer.OrdinalIgnoreCase);
    }
```

→ replacement:

```csharp
    /// <param name="memberNames">
    ///     The members to populate the rank with
    /// </param>
    /// <param name="permissions">
    ///     What members of this rank may do. Null gives the tier's default from <see cref="GuildPermissionRules.DefaultFor" />
    /// </param>
    public GuildRank(
        string name,
        int tier,
        ICollection<string>? memberNames = null,
        GuildPermission? permissions = null)
    {
        memberNames ??= [];

        Name = name;
        Tier = tier;
        MemberNames = new HashSet<string>(memberNames, StringComparer.OrdinalIgnoreCase);
        Permissions = permissions ?? GuildPermissionRules.DefaultFor(tier);
    }
```

Then Serena `insert_after_symbol`, name path `GuildRank/SetAllowance`, same file. Body:

```csharp

    /// <summary>
    ///     Whether members of this rank may do <paramref name="permission" />. Always true for the leader rank
    /// </summary>
    public bool Allows(GuildPermission permission) => IsLeaderRank || ((Permissions & permission) == permission);

    /// <summary>
    ///     Turns one permission on or off for this rank
    /// </summary>
    public void SetPermission(GuildPermission permission, bool on)
        => Permissions = on ? Permissions | permission : Permissions & ~permission;
```

- [ ] **Step 6: Save permissions with the rank**

In `worktrees/rank-permissions-server/Chaos.Schemas/Guilds/GuildRankSchema.cs`, Serena `replace_content` (literal):

1. Needle `using System.Text.Json.Serialization;` → replacement:

```csharp
using System.Text.Json.Serialization;
using Chaos.DarkAges.Definitions;
```

2. Needle:

```csharp
    public int TrainingAllowance { get; set; }
}
```

→ replacement:

```csharp
    public int TrainingAllowance { get; set; }

    /// <summary>
    ///     What members of this rank may do, saved by name. Missing in files saved before permissions existed; those load
    ///     with the rules of that time for the rank's tier
    /// </summary>
    public GuildPermission? Permissions { get; set; }
}
```

In `worktrees/rank-permissions-server/Chaos/Services/MapperProfiles/GuildMapperProfile.cs`, Serena `replace_content` (literal):

1. Needle `        var rank = new GuildRank(obj.RankName, obj.Tier, obj.Members);` → replacement:

```csharp
        //a file saved before permissions existed has none; the rank then gets its tier's default
        var rank = new GuildRank(
            obj.RankName,
            obj.Tier,
            obj.Members,
            obj.Permissions);
```

2. Needle:

```csharp
            RepairAllowance = obj.RepairAllowance
        };
```

→ replacement:

```csharp
            RepairAllowance = obj.RepairAllowance,
            Permissions = obj.Permissions
        };
```

- [ ] **Step 7: Let the guild answer and change permissions**

Serena `insert_after_symbol`, name path `Guild/SetRankAllowance`, file `worktrees/rank-permissions-server/Chaos/Collections/Guild.cs`. `Guild.cs` already has `using Chaos.DarkAges.Definitions;`. Body:

```csharp

    /// <summary>
    ///     Whether <paramref name="memberName" /> may do <paramref name="permission" />. False for someone not in the
    ///     guild, and always true for the leader
    /// </summary>
    public bool HasPermission(string memberName, GuildPermission permission)
    {
        using var @lock = Sync.EnterScope();

        return UnsafeRankof(memberName)
                   ?.Allows(permission)
               ?? false;
    }

    /// <summary>
    ///     Turns one permission on or off for a rank. The leader's rank has no permissions to change, because the leader
    ///     may always do everything
    /// </summary>
    /// <remarks>
    ///     This doesn't check <see cref="GuildPermissionRules.ForTier" />; only the leader's menu limits which permissions
    ///     appear. A permission set on a tier that can't use it does nothing, because the rank-gap rules still block the
    ///     action
    /// </remarks>
    public void SetRankPermission(int tier, GuildPermission permission, bool on)
    {
        using var @lock = Sync.EnterScope();

        if (tier == 0)
            throw new InvalidOperationException("The leader's rank has no permissions to change.");

        if (!UnsafeTryGetRank(tier, out var rank))
            throw new InvalidOperationException($"Attempted to set a permission on rank tier {tier}, which does not exist.");

        rank.SetPermission(permission, on);
    }
```

- [ ] **Step 8: Run the tests to check they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildPermissionDataTests/*" --no-ansi`

Expected: every test passes.

- [ ] **Step 9: Check the existing guild tests still pass**

Run:

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildRankTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/*/*" --no-ansi
```

Expected: every test passes in all three runs.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos/Collections/GuildPermissionRules.cs", "Chaos/Collections/GuildRank.cs", "Chaos.Schemas/Guilds/GuildRankSchema.cs", "Chaos/Services/MapperProfiles/GuildMapperProfile.cs", "Chaos/Collections/Guild.cs", "Tests/Chaos.Tests/GuildPermissions/GuildPermissionDataTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildPermissions/GuildPermissionDataTests/*\" --no-ansi", "acceptanceCriteria": ["missing permissions load as all seven for tier 1 and None for tiers 0, 2, 3", "new guild ranks start with the same defaults", "permissions round-trip and are written as names", "HasPermission: leader true, non-member false, else rank switch; RankOf copy keeps value", "SetRankPermission throws for tier 0", "ForTier returns 7/6/5/0 switches in menu order"], "modelTier": "standard"}
```

---

### Task 2: Separate item and gold withdrawals from the guild bank

**Goal:** The guild bank checks taking items and taking gold against their own switches, with a refusal message for each.

**Files:**
- Modify: `SRV/Chaos/Collections/BankPermissions.cs` (`CanWithdraw` → `CanWithdrawItems` + `CanWithdrawGold`)
- Modify: `SRV/Chaos/Collections/Guild.cs` (`GetBankPermissions` and its remarks)
- Modify: `SRV/Chaos/Services/Servers/WorldServer.cs` (the `WithdrawItem` and `WithdrawGold` cases, `CanWithdraw` helper)
- Test: `SRV/Tests/Chaos.Tests/BankPermissionsTests.cs`
- Test: `SRV/Tests/Chaos.Tests/ComplexActionHelperTests.cs` (constructor calls only)

**Acceptance Criteria:**
- [ ] `BankPermissions` has `CanWithdrawItems` and `CanWithdrawGold`; `Personal` allows both and `None` neither
- [ ] By default the leader and Council can take both, and Member and Applicant neither
- [ ] A rank with items on and gold off gets `CanWithdrawItems: true, CanWithdrawGold: false`, and the reverse
- [ ] In `WorldServer`, the withdraw-item case checks `CanWithdrawItems` and the withdraw-gold case checks `CanWithdrawGold`, with "Your rank can't take items from the guild bank." and "Your rank can't take gold from the guild bank."
- [ ] No `CanWithdraw` identifier is left in `Chaos/` or `Tests/`

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/BankPermissionsTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Update and add the bank permission tests**

In `worktrees/rank-permissions-server/Tests/Chaos.Tests/BankPermissionsTests.cs`, Serena `replace_content`:

1. Literal needle `using Chaos.Collections;` → replacement:

```csharp
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
```

2. Literal needle `.Be(new BankPermissions(CanView: true, CanDeposit: true, CanWithdraw: true, AllowsBoundItems: true));` → replacement:

```csharp
.Be(new BankPermissions(CanView: true, CanDeposit: true, CanWithdrawItems: true, CanWithdrawGold: true, AllowsBoundItems: true));
```

3. Literal needle `public void GetBankPermissions_WithdrawIsOfficerAndAbove_DepositIsAnyMember(int tier, bool canWithdraw)` → replacement:

```csharp
public void GetBankPermissions_ByDefaultWithdrawIsCouncilAndAbove_DepositIsAnyMember(int tier, bool canWithdraw)
```

4. Literal needle `.Be(new BankPermissions(CanView: true, CanDeposit: true, CanWithdraw: canWithdraw, AllowsBoundItems: false));` → replacement:

```csharp
.Be(new BankPermissions(CanView: true, CanDeposit: true, CanWithdrawItems: canWithdraw, CanWithdrawGold: canWithdraw, AllowsBoundItems: false));
```

5. Regex needle `\.CanWithdraw(?=\s)` with `allow_multiple_occurrences: true` (two matches, both in `Session_Permissions_ReDeriveOnDemotion`) → replacement `.CanWithdrawItems`.

Then Serena `insert_after_symbol`, name path `BankPermissionsTests/GetBankPermissions_NonMember_DeniesEverythingWithoutThrowing`, same file. Body:

```csharp

    [Test]
    public void GetBankPermissions_ItemsAndGoldFollowTheirOwnSwitches()
    {
        var guild = GuildWith("Member1", MEMBER_TIER, out var member);

        guild.SetRankPermission(MEMBER_TIER, GuildPermission.WithdrawItems, true);

        guild.GetBankPermissions(member.Name)
             .Should()
             .Be(
                 new BankPermissions(
                     CanView: true,
                     CanDeposit: true,
                     CanWithdrawItems: true,
                     CanWithdrawGold: false,
                     AllowsBoundItems: false));

        guild.SetRankPermission(MEMBER_TIER, GuildPermission.WithdrawItems, false);
        guild.SetRankPermission(MEMBER_TIER, GuildPermission.WithdrawGold, true);

        guild.GetBankPermissions(member.Name)
             .Should()
             .Be(
                 new BankPermissions(
                     CanView: true,
                     CanDeposit: true,
                     CanWithdrawItems: false,
                     CanWithdrawGold: true,
                     AllowsBoundItems: false));
    }

    [Test]
    public void GetBankPermissions_ACouncilWithGoldLockedCanStillTakeItems()
    {
        var guild = GuildWith("Officer1", COUNCIL_TIER, out var officer);

        guild.SetRankPermission(COUNCIL_TIER, GuildPermission.WithdrawGold, false);

        var permissions = guild.GetBankPermissions(officer.Name);

        permissions.CanWithdrawItems
                   .Should()
                   .BeTrue();

        permissions.CanWithdrawGold
                   .Should()
                   .BeFalse();
    }
```

In `worktrees/rank-permissions-server/Tests/Chaos.Tests/ComplexActionHelperTests.cs`, Serena `replace_content` in regex mode, `allow_multiple_occurrences: true` (two matches). Needle `( *)CanWithdraw: true,` → replacement (two lines, with a real line break between them):

```
$!1CanWithdrawItems: true,
$!1CanWithdrawGold: true,
```

- [ ] **Step 2: Run the tests to check they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/BankPermissionsTests/*" --no-ansi`

Expected: the build fails with `error CS1739: The best overload for 'BankPermissions' does not have a parameter named 'CanWithdrawItems'`.

- [ ] **Step 3: Split the permission**

In `worktrees/rank-permissions-server/Chaos/Collections/BankPermissions.cs`, Serena `replace_symbol_body`, name path `BankPermissions`. Body:

```csharp
public readonly record struct BankPermissions(
    bool CanView,
    bool CanDeposit,
    bool CanWithdrawItems,
    bool CanWithdrawGold,
    bool AllowsBoundItems)
{
    /// <summary>
    ///     No bank at all. A member kicked while the window is open holds a view onto a bank they have left.
    /// </summary>
    public static readonly BankPermissions None = new(
        CanView: false,
        CanDeposit: false,
        CanWithdrawItems: false,
        CanWithdrawGold: false,
        AllowsBoundItems: false);

    /// <summary>
    ///     Your own bank: no rank, no gate. Bound items are welcome — they are already yours and are going nowhere.
    /// </summary>
    public static readonly BankPermissions Personal = new(
        CanView: true,
        CanDeposit: true,
        CanWithdrawItems: true,
        CanWithdrawGold: true,
        AllowsBoundItems: true);
}
```

In `worktrees/rank-permissions-server/Chaos/Collections/Guild.cs`:

1. Serena `replace_content` (literal), needle `    ///     Withdrawing is officer-and-above; depositing is open to any member. Someone who is not in the guild may not` → replacement:

```csharp
    ///     Taking items and taking gold each follow the rank's permissions; depositing is open to any member. Someone who is not in the guild may not
```

2. Serena `replace_symbol_body`, name path `Guild/GetBankPermissions`. Body:

```csharp
    public BankPermissions GetBankPermissions(string memberName)
    {
        using var @lock = Sync.EnterScope();

        var rank = UnsafeRankof(memberName);

        if (rank is null)
            return BankPermissions.None;

        return new BankPermissions(
            CanView: true,
            CanDeposit: true,
            CanWithdrawItems: rank.Allows(GuildPermission.WithdrawItems),
            CanWithdrawGold: rank.Allows(GuildPermission.WithdrawGold),
            AllowsBoundItems: false);
    }
```

- [ ] **Step 4: Check each withdrawal against its own permission**

In `worktrees/rank-permissions-server/Chaos/Services/Servers/WorldServer.cs`, Serena `replace_content` (literal):

1. Needle (in the `WithdrawItem` case):

```csharp
                    if (string.IsNullOrEmpty(localArgs.ItemName))
                        break;

                    if (!CanWithdraw(aisling, permissions))
                        break;
```

→ replacement:

```csharp
                    if (string.IsNullOrEmpty(localArgs.ItemName))
                        break;

                    if (!CanWithdrawItems(aisling, permissions))
                        break;
```

2. Needle (in the `WithdrawGold` case):

```csharp
                    if (!CanWithdraw(aisling, permissions))
                        break;

                    var result = ComplexActionHelper.WithdrawGold(aisling, bank, localArgs.Amount);
```

→ replacement:

```csharp
                    if (!CanWithdrawGold(aisling, permissions))
                        break;

                    var result = ComplexActionHelper.WithdrawGold(aisling, bank, localArgs.Amount);
```

Then Serena `replace_symbol_body`, name path `WorldServer/CanWithdraw`. Body:

```csharp
    private static bool CanWithdrawItems(Aisling aisling, BankPermissions permissions)
    {
        if (permissions.CanWithdrawItems)
            return true;

        aisling.SendOrangeBarMessage("Your rank can't take items from the guild bank.");

        return false;
    }
```

Then Serena `insert_after_symbol`, name path `WorldServer/CanWithdrawItems`. Body:

```csharp

    private static bool CanWithdrawGold(Aisling aisling, BankPermissions permissions)
    {
        if (permissions.CanWithdrawGold)
            return true;

        aisling.SendOrangeBarMessage("Your rank can't take gold from the guild bank.");

        return false;
    }
```

- [ ] **Step 5: Check no old name is left**

Serena `search_for_pattern`, pattern `CanWithdraw\b`, relative path `worktrees/rank-permissions-server`, restricted to `*.cs`.

Expected: no matches.

- [ ] **Step 6: Run the tests to check they pass**

Run:

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/BankPermissionsTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/ComplexActionHelperTests/*" --no-ansi
```

Expected: every test passes in both runs.

```json:metadata
{"files": ["Chaos/Collections/BankPermissions.cs", "Chaos/Collections/Guild.cs", "Chaos/Services/Servers/WorldServer.cs", "Tests/Chaos.Tests/BankPermissionsTests.cs", "Tests/Chaos.Tests/ComplexActionHelperTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests/BankPermissionsTests/*\" --no-ansi", "acceptanceCriteria": ["BankPermissions has CanWithdrawItems and CanWithdrawGold; Personal both, None neither", "default: leader and Council take both, Member and Applicant neither", "items-on/gold-off and the reverse map to the two fields", "WorldServer withdraw-item and withdraw-gold cases check their own field with their own message", "no CanWithdraw identifier left"], "modelTier": "standard"}
```

---

### Task 3: Guild buffs paid from guild gold

**Goal:** Only a rank with `GuildGoldBuffs` can pay for a guild buff from the guild bank, and a click that lands after the leader turns the permission off is refused instead of charged to the member's own gold.

**Files:**
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBuffScript.cs` (two constants, `On25ExpPaymentChoiceDisplaying`, `On25ExpPaymentChoiceNext`, the guild-bank check in `On25ExpConfirmationNext`)
- Test: `SRV/Tests/Chaos.Tests/GuildPermissions/GuildBuffPermissionTests.cs`

**Acceptance Criteria:**
- [ ] The payment menu is `Pay from Guild Bank`, `Pay from Personal Gold`, `Nevermind` with the permission, and `Pay from Personal Gold`, `Nevermind` without it
- [ ] The payment choice is read from the clicked option's text, not its position
- [ ] After the permission goes off mid-menu, a click on `Pay from Guild Bank` then Yes charges neither the member nor the guild bank, and replies "Your rank can't pay for buffs with guild gold."
- [ ] Paying from personal gold still works without the permission

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildBuffPermissionTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildPermissions/GuildBuffPermissionTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts;
using Chaos.Scripting.WorldScripts.WorldBuffs.Guild;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
#endregion

namespace Chaos.Tests.GuildPermissions;

public sealed class GuildBuffPermissionTests
{
    private const int MEMBER_TIER = 2;
    private const int ONE_HOUR_BUFF_COST = 2_000_000;
    private const int STARTING_GOLD = 50_000_000;

    private static GuildBuffScript CreateScript(Dialog dialog)
    {
        var buffStorage = new Mock<IStorage<GuildBuffs>>();

        buffStorage.SetupGet(storage => storage.Value)
                   .Returns(new GuildBuffs());

        return new GuildBuffScript(
            dialog,
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildBuffScript>>().Object,
            buffStorage.Object);
    }

    private static (Guild Guild, Aisling Member) CreateGuild()
    {
        var guild = MockGuild.Create();
        var leader = MockAisling.Create(name: "Stahli");
        var member = MockAisling.Create(name: "Iglis");

        guild.AddMember(leader, member);
        guild.ChangeRank(leader.Name, 0, member);
        guild.AddMember(member, leader);
        guild.ChangeRank(member.Name, MEMBER_TIER, leader);

        guild.Bank.AddGold(STARTING_GOLD);
        member.Gold = STARTING_GOLD;

        return (guild, member);
    }

    //the payment menu after picking the 1 hour buff, which is option 1 of the duration menu
    private static Dialog PaymentChoice()
    {
        var dialog = MockDialog.Create("generic_guildbuff_25exp_paymentchoice");
        dialog.Context = (byte)1;

        return dialog;
    }

    private static Dialog ConfirmationAfter(Dialog paymentChoice)
    {
        var dialog = MockDialog.Create("generic_guildbuff_25exp_confirmation");
        dialog.Context = paymentChoice.Context;

        return dialog;
    }

    [Test]
    public void Pay_from_Guild_Bank_shows_only_with_the_permission()
    {
        var (guild, member) = CreateGuild();

        var without = PaymentChoice();

        CreateScript(without)
            .OnDisplaying(member);

        guild.SetRankPermission(MEMBER_TIER, GuildPermission.GuildGoldBuffs, true);

        var with = PaymentChoice();

        CreateScript(with)
            .OnDisplaying(member);

        without.Options
               .Select(option => option.OptionText)
               .Should()
               .Equal("Pay from Personal Gold", "Nevermind");

        with.Options
            .Select(option => option.OptionText)
            .Should()
            .Equal("Pay from Guild Bank", "Pay from Personal Gold", "Nevermind");
    }

    [Test]
    public void A_late_click_after_the_permission_goes_off_charges_nobody()
    {
        var (guild, member) = CreateGuild();
        guild.SetRankPermission(MEMBER_TIER, GuildPermission.GuildGoldBuffs, true);

        var choice = PaymentChoice();
        var choiceScript = CreateScript(choice);

        choiceScript.OnDisplaying(member);

        //the leader switches it off while the member is looking at the menu
        guild.SetRankPermission(MEMBER_TIER, GuildPermission.GuildGoldBuffs, false);

        //"Pay from Guild Bank", then "Yes"
        choiceScript.OnNext(member, 1);

        CreateScript(ConfirmationAfter(choice))
            .OnNext(member, 1);

        member.Gold
              .Should()
              .Be(STARTING_GOLD);

        guild.Bank
             .Gold
             .Should()
             .Be(STARTING_GOLD);

        member.ActiveDialog
              .Get()!
              .Text
              .Should()
              .Be("Your rank can't pay for buffs with guild gold.");
    }

    [Test]
    public void Paying_from_personal_gold_still_works_without_the_permission()
    {
        var (guild, member) = CreateGuild();

        var choice = PaymentChoice();
        var choiceScript = CreateScript(choice);

        choiceScript.OnDisplaying(member);

        //"Pay from Personal Gold" is option 1 without the permission, then "Yes"
        choiceScript.OnNext(member, 1);

        CreateScript(ConfirmationAfter(choice))
            .OnNext(member, 1);

        member.Gold
              .Should()
              .Be(STARTING_GOLD - ONE_HOUR_BUFF_COST);

        guild.Bank
             .Gold
             .Should()
             .Be(STARTING_GOLD);
    }
}
```

- [ ] **Step 2: Run the tests to check they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildBuffPermissionTests/*" --no-ansi`

Expected: `Pay_from_Guild_Bank_shows_only_with_the_permission` fails (a Member never sees "Pay from Guild Bank"), and `A_late_click_after_the_permission_goes_off_charges_nobody` fails because the member's gold dropped by 2,000,000.

- [ ] **Step 3: Name the two payment options**

In `worktrees/rank-permissions-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBuffScript.cs`, Serena `replace_content` (literal), needle `    public const string GUILD_25_EXP_BUFF_NAME = "GuildExp25";` → replacement:

```csharp
    public const string GUILD_25_EXP_BUFF_NAME = "GuildExp25";
    private const string PAY_FROM_GUILD_BANK = "Pay from Guild Bank";
    private const string PAY_FROM_PERSONAL_GOLD = "Pay from Personal Gold";
```

- [ ] **Step 4: Show the guild-bank option by permission**

Serena `replace_symbol_body`, name path `GuildBuffScript/On25ExpPaymentChoiceDisplaying`. Body:

```csharp
    private void On25ExpPaymentChoiceDisplaying(Aisling source)
    {
        if (!IsInGuild(source, out var guild, out _))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        if (guild.HasPermission(source.Name, GuildPermission.GuildGoldBuffs))
            Subject.AddOption(PAY_FROM_GUILD_BANK, "generic_guildbuff_25exp_confirmation");

        Subject.AddOption(PAY_FROM_PERSONAL_GOLD, "generic_guildbuff_25exp_confirmation");
        Subject.AddOption("Nevermind", "top");
    }
```

- [ ] **Step 5: Read the payment choice by its text**

Serena `replace_symbol_body`, name path `GuildBuffScript/On25ExpPaymentChoiceNext`. Body:

```csharp
    private void On25ExpPaymentChoiceNext(Aisling source, byte? optionIndex = null)
    {
        if (!optionIndex.HasValue || Subject.Context is not byte durationChoice)
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        if (!IsInGuild(source, out _, out _))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        //read the clicked option by its text, not its position. The list depends on the guild-gold permission, which the
        //leader can switch off while this menu is open; by position, a late click on "Pay from Guild Bank" would read as
        //"Pay from Personal Gold" and charge the member's own gold
        bool useGuildBank;

        switch (Subject.GetOptionText(optionIndex.Value))
        {
            case PAY_FROM_GUILD_BANK:
                useGuildBank = true;

                break;
            case PAY_FROM_PERSONAL_GOLD:
                useGuildBank = false;

                break;
            default:
                return;
        }

        Subject.Context = new BuffPurchaseContext(durationChoice, useGuildBank);
    }
```

- [ ] **Step 6: Check the permission again at payment**

Serena `replace_content` (literal), same file. Needle:

```csharp
            if (!IsInGuild(source, out _, out var sourceRank) || !sourceRank.IsOfficerRank)
            {
                Subject.Reply(source, "You do not have permission to pay from the guild bank.", "top");
```

→ replacement:

```csharp
            if (!guild.HasPermission(source.Name, GuildPermission.GuildGoldBuffs))
            {
                Subject.Reply(source, "Your rank can't pay for buffs with guild gold.", "top");
```

`guild` is the local `var guild = source.Guild;` declared earlier in `On25ExpConfirmationNext`. `GuildBuffScript.cs` already has `using Chaos.DarkAges.Definitions;`.

- [ ] **Step 7: Run the tests to check they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildBuffPermissionTests/*" --no-ansi`

Expected: every test passes.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBuffScript.cs", "Tests/Chaos.Tests/GuildPermissions/GuildBuffPermissionTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildPermissions/GuildBuffPermissionTests/*\" --no-ansi", "acceptanceCriteria": ["payment menu shows Pay from Guild Bank only with the permission", "payment choice read by option text", "late click after the switch goes off charges nobody and replies the refusal", "personal gold payment still works without the permission"], "modelTier": "mechanical"}
```

---

### Task 4: Hall upgrades and the house deed

**Goal:** Buying hall rooms and the guild cloak deed follows `BuyHallRooms`, and only the leader can buy the house deed, checked at the step that takes the gold.

**Files:**
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs` (the rank check in `HandleUpgrade`, the `tibbs_initial` case)
- Test: `SRV/Tests/Chaos.Tests/GuildPermissions/GuildHallPermissionTests.cs`

**Acceptance Criteria:**
- [ ] A Member with `BuyHallRooms` buys the guild cloak deed: 10,000,000 gold is taken and the `cloaks` property is on
- [ ] A Member without it is refused with "Your rank can't buy hall upgrades.", and keeps their gold
- [ ] A Council member (who has `BuyHallRooms` by default) can't buy the house deed: "Only the guild leader can buy the guild house deed.", gold kept, `deed` off
- [ ] Tibbs's opening menu turns away a rank without the permission with "Your rank can't buy hall upgrades."

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildHallPermissionTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildPermissions/GuildHallPermissionTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts;
using Chaos.Services.Factories.Abstractions;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Moq;
#endregion

namespace Chaos.Tests.GuildPermissions;

public sealed class GuildHallPermissionTests
{
    private const int COUNCIL_TIER = 1;
    private const int MEMBER_TIER = 2;
    private const int STARTING_GOLD = 20_000_000;

    private static GuildUpdateHallScript CreateScript(Dialog dialog, GuildHouseState state)
    {
        var storage = new Mock<IStorage<GuildHouseState>>();

        storage.SetupGet(s => s.Value)
               .Returns(state);

        return new GuildUpdateHallScript(
            dialog,
            storage.Object,
            new Mock<IMerchantFactory>().Object,
            new ClientRegistry<IChaosWorldClient>());
    }

    private static (Guild Guild, Aisling Buyer) CreateGuild(int buyerTier)
    {
        var guild = MockGuild.Create();
        var leader = MockAisling.Create(name: "Stahli");
        var buyer = MockAisling.Create(name: "Iglis");

        guild.AddMember(leader, buyer);
        guild.ChangeRank(leader.Name, 0, buyer);
        guild.AddMember(buyer, leader);
        guild.ChangeRank(buyer.Name, buyerTier, leader);

        buyer.Gold = STARTING_GOLD;

        return (guild, buyer);
    }

    [Test]
    public void A_member_with_the_permission_can_buy_the_guild_cloak_deed()
    {
        var (guild, buyer) = CreateGuild(MEMBER_TIER);
        guild.SetRankPermission(MEMBER_TIER, GuildPermission.BuyHallRooms, true);
        var state = new GuildHouseState();

        CreateScript(MockDialog.Create("tibbs_purchase_cloaks_confirm"), state)
            .OnDisplaying(buyer);

        buyer.Gold
             .Should()
             .Be(STARTING_GOLD - GuildCloakProtocol.DEED_PRICE);

        state.HasProperty(guild.Name, GuildCloakProtocol.DEED_PROPERTY)
             .Should()
             .BeTrue();
    }

    [Test]
    public void A_member_without_the_permission_is_refused()
    {
        var (guild, buyer) = CreateGuild(MEMBER_TIER);
        var state = new GuildHouseState();

        CreateScript(MockDialog.Create("tibbs_purchase_cloaks_confirm"), state)
            .OnDisplaying(buyer);

        buyer.Gold
             .Should()
             .Be(STARTING_GOLD);

        state.HasProperty(guild.Name, GuildCloakProtocol.DEED_PROPERTY)
             .Should()
             .BeFalse();

        buyer.ActiveDialog
             .Get()!
             .Text
             .Should()
             .Be("Your rank can't buy hall upgrades.");
    }

    [Test]
    public void Only_the_leader_can_buy_the_house_deed()
    {
        //Council has BuyHallRooms by default, which must not open up the house deed
        var (guild, buyer) = CreateGuild(COUNCIL_TIER);
        var state = new GuildHouseState();

        CreateScript(MockDialog.Create("tibbs_purchase_house"), state)
            .OnDisplaying(buyer);

        buyer.Gold
             .Should()
             .Be(STARTING_GOLD);

        state.HasProperty(guild.Name, "deed")
             .Should()
             .BeFalse();

        buyer.ActiveDialog
             .Get()!
             .Text
             .Should()
             .Be("Only the guild leader can buy the guild house deed.");
    }

    [Test]
    public void Tibbs_turns_away_a_rank_without_the_permission()
    {
        var (_, buyer) = CreateGuild(MEMBER_TIER);

        CreateScript(MockDialog.Create("tibbs_initial"), new GuildHouseState())
            .OnDisplaying(buyer);

        buyer.ActiveDialog
             .Get()!
             .Text
             .Should()
             .Be("Your rank can't buy hall upgrades.");
    }
}
```

- [ ] **Step 2: Run the tests to check they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildHallPermissionTests/*" --no-ansi`

Expected: all four fail. The Member is refused with the old council message, and the Council member buys the house deed.

- [ ] **Step 3: Check the permission and the leader at the purchase step**

In `worktrees/rank-permissions-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs`, Serena `replace_content` (literal). Needle (in `HandleUpgrade`):

```csharp
        var rank = source.Guild?.RankOf(source.Name);

        if (rank is null || !rank.IsOfficerRank)
        {
            Subject.Reply(source, "You must be a council member or leader to purchase guild hall upgrades.");

            return;
        }
```

→ replacement:

```csharp
        //the house deed stays the leader's. Tibbs only offers it to the leader, but this is the step that takes the gold,
        //so it checks again
        if (property == "deed")
        {
            var rank = source.Guild?.RankOf(source.Name);

            if (rank is null || !rank.IsLeaderRank)
            {
                Subject.Reply(source, "Only the guild leader can buy the guild house deed.");

                return;
            }
        } else if (source.Guild?.HasPermission(source.Name, GuildPermission.BuyHallRooms) != true)
        {
            Subject.Reply(source, "Your rank can't buy hall upgrades.");

            return;
        }
```

- [ ] **Step 4: Check the permission at Tibbs's first menu**

Same file, Serena `replace_content` (literal). Needle (the `tibbs_initial` case):

```csharp
                var rank = source.Guild?.RankOf(source.Name);

                if (rank is null || !rank.IsOfficerRank)
                {
                    source.SendOrangeBarMessage("You must be a council member or leader to buy guild hall upgrades.");
                    Subject.Reply(source, "You must be a council member or the leader to buy guild hall upgrades.");
                }
```

→ replacement:

```csharp
                if (source.Guild?.HasPermission(source.Name, GuildPermission.BuyHallRooms) != true)
                {
                    source.SendOrangeBarMessage("Your rank can't buy hall upgrades.");
                    Subject.Reply(source, "Your rank can't buy hall upgrades.");
                }
```

`GuildUpdateHallScript.cs` already has `using Chaos.DarkAges.Definitions;`.

- [ ] **Step 5: Run the tests to check they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildHallPermissionTests/*" --no-ansi`

Expected: every test passes.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs", "Tests/Chaos.Tests/GuildPermissions/GuildHallPermissionTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildPermissions/GuildHallPermissionTests/*\" --no-ansi", "acceptanceCriteria": ["Member with BuyHallRooms buys the cloak deed: 10,000,000 taken, cloaks on", "Member without it refused with the hall message and keeps gold", "Council can't buy the house deed: leader message, gold kept, deed off", "Tibbs's first menu turns away a rank without the permission"], "modelTier": "mechanical"}
```

---

### Task 5: Admit, kick, promote and demote

**Goal:** The Members menu shows Admit, Kick and Promote/Demote only with their permission, and the four action scripts check the same permission when the action happens. The rank-gap rules don't change.

**Files:**
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs` (using, `OnDisplayingInitial`)
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberAdmitScript.cs` (using, check in `OnDisplayingAccepted`)
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberKickScript.cs` (using, check in `OnDisplayingAccepted`)
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberPromoteScript.cs` (using, check in `OnDisplayingAccepted`)
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberDemoteScript.cs` (using, check in `OnDisplayingAccepted`)
- Test: `SRV/Tests/Chaos.Tests/GuildPermissions/GuildMemberPermissionTests.cs`

**Acceptance Criteria:**
- [ ] A Member in the tavern sees only `Roster`, and sees `Roster`, `Admit`, `Kick` once both are switched on
- [ ] A Council member sees `Roster`, `Promote`, `Demote`, `Kick` outside the tavern, and `Roster`, `Kick` with `PromoteDemote` off
- [ ] Kick is refused with "Your rank can't kick members." when the switch is off, and nobody is kicked
- [ ] A Member with `Kick` can kick an Applicant but not another Member
- [ ] Admit, promote and demote are refused with their own "Your rank can't ..." message when their switch is off, and nobody's rank changes
- [ ] "Promote to Leader" is still shown to the leader only

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildMemberPermissionTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildPermissions/GuildMemberPermissionTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Collections.Common;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts;
using Chaos.Services.Factories.Abstractions;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
#endregion

namespace Chaos.Tests.GuildPermissions;

public sealed class GuildMemberPermissionTests
{
    private const int APPLICANT_TIER = 3;
    private const int COUNCIL_TIER = 1;
    private const int MEMBER_TIER = 2;

    private static (Guild Guild, Aisling Leader) CreateGuild()
    {
        var guild = MockGuild.Create();
        var leader = MockAisling.Create(name: "Stahli");

        guild.AddMember(leader, leader);
        guild.ChangeRank(leader.Name, 0, leader);

        return (guild, leader);
    }

    private static Aisling Join(
        Guild guild,
        Aisling leader,
        string name,
        int tier,
        MapInstance? map = null)
    {
        var aisling = MockAisling.Create(map, name);

        guild.AddMember(aisling, leader);
        guild.ChangeRank(aisling.Name, tier, leader);

        return aisling;
    }

    private static Dialog Accepted(string templateKey, string targetName)
        => MockDialog.Create(templateKey, dialog => dialog.MenuArgs = new ArgumentCollection(new[] { targetName }));

    private static IEnumerable<string> MembersMenuFor(Aisling source)
    {
        var dialog = MockDialog.Create("generic_guild_members_initial");

        new GuildMemberManagementScript(
            dialog,
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildMemberManagementScript>>().Object).OnDisplaying(source);

        return dialog.Options.Select(option => option.OptionText);
    }

    private static void Kick(Aisling source, string targetName)
        => new GuildMemberKickScript(
            Accepted("generic_guild_members_kick_accepted", targetName),
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildMemberKickScript>>().Object).OnDisplaying(source);

    [Test]
    public void The_members_menu_follows_each_permission()
    {
        var (guild, leader) = CreateGuild();
        var tavern = MockMapInstance.Create(name: GuildMemberAdmitScript.ADMISSION_MAP_NAME);
        var member = Join(guild, leader, "Iglis", MEMBER_TIER, tavern);

        MembersMenuFor(member)
            .Should()
            .Equal("Roster");

        guild.SetRankPermission(MEMBER_TIER, GuildPermission.Admit, true);
        guild.SetRankPermission(MEMBER_TIER, GuildPermission.Kick, true);

        MembersMenuFor(member)
            .Should()
            .Equal("Roster", "Admit", "Kick");
    }

    [Test]
    public void A_council_without_promote_and_demote_does_not_see_them()
    {
        var (guild, leader) = CreateGuild();
        var officer = Join(guild, leader, "Iglis", COUNCIL_TIER);

        MembersMenuFor(officer)
            .Should()
            .Equal("Roster", "Promote", "Demote", "Kick");

        guild.SetRankPermission(COUNCIL_TIER, GuildPermission.PromoteDemote, false);

        MembersMenuFor(officer)
            .Should()
            .Equal("Roster", "Kick");

        MembersMenuFor(leader)
            .Should()
            .Contain("Promote to Leader");
    }

    [Test]
    public void Kicking_needs_the_permission()
    {
        var (guild, leader) = CreateGuild();
        var officer = Join(guild, leader, "Iglis", COUNCIL_TIER);
        Join(guild, leader, "Newbie", APPLICANT_TIER);
        guild.SetRankPermission(COUNCIL_TIER, GuildPermission.Kick, false);

        Kick(officer, "Newbie");

        guild.HasMember("Newbie")
             .Should()
             .BeTrue();

        officer.ActiveDialog
               .Get()!
               .Text
               .Should()
               .Be("Your rank can't kick members.");
    }

    [Test]
    public void A_member_with_kick_can_kick_an_applicant_but_not_another_member()
    {
        var (guild, leader) = CreateGuild();
        var member = Join(guild, leader, "Iglis", MEMBER_TIER);
        Join(guild, leader, "Newbie", APPLICANT_TIER);
        Join(guild, leader, "Peer", MEMBER_TIER);
        guild.SetRankPermission(MEMBER_TIER, GuildPermission.Kick, true);

        Kick(member, "Peer");

        guild.HasMember("Peer")
             .Should()
             .BeTrue();

        Kick(member, "Newbie");

        guild.HasMember("Newbie")
             .Should()
             .BeFalse();
    }

    [Test]
    public void Admitting_needs_the_permission()
    {
        var (guild, leader) = CreateGuild();
        var officer = Join(guild, leader, "Iglis", COUNCIL_TIER);
        guild.SetRankPermission(COUNCIL_TIER, GuildPermission.Admit, false);

        new GuildMemberAdmitScript(
            Accepted("generic_guild_members_admit_accepted", "Newbie"),
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildMemberAdmitScript>>().Object,
            new Mock<IDialogFactory>().Object).OnDisplaying(officer);

        officer.ActiveDialog
               .Get()!
               .Text
               .Should()
               .Be("Your rank can't admit new members.");
    }

    [Test]
    public void Promoting_and_demoting_need_the_permission()
    {
        var (guild, leader) = CreateGuild();
        var officer = Join(guild, leader, "Iglis", COUNCIL_TIER);
        Join(guild, leader, "Newbie", APPLICANT_TIER);
        Join(guild, leader, "Peer", MEMBER_TIER);
        guild.SetRankPermission(COUNCIL_TIER, GuildPermission.PromoteDemote, false);

        new GuildMemberPromoteScript(
            Accepted("generic_guild_members_promote_accepted", "Newbie"),
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildMemberPromoteScript>>().Object).OnDisplaying(officer);

        officer.ActiveDialog
               .Get()!
               .Text
               .Should()
               .Be("Your rank can't promote members.");

        new GuildMemberDemoteScript(
            Accepted("generic_guild_members_demote_accepted", "Peer"),
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildMemberDemoteScript>>().Object).OnDisplaying(officer);

        officer.ActiveDialog
               .Get()!
               .Text
               .Should()
               .Be("Your rank can't demote members.");

        guild.RankOf("Newbie")
             .Tier
             .Should()
             .Be(APPLICANT_TIER);

        guild.RankOf("Peer")
             .Tier
             .Should()
             .Be(MEMBER_TIER);
    }
}
```

- [ ] **Step 2: Run the tests to check they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildMemberPermissionTests/*" --no-ansi`

Expected: all six tests fail. The menus and actions still follow the officer rule, so the switches have no effect. (The admit test may fail with an exception instead of a wrong message. That's fine; it still fails.)

- [ ] **Step 3: Add the using line to the five scripts**

For each of `GuildMemberManagementScript.cs`, `GuildMemberAdmitScript.cs`, `GuildMemberKickScript.cs`, `GuildMemberPromoteScript.cs` and `GuildMemberDemoteScript.cs` in `worktrees/rank-permissions-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/`, Serena `replace_content` (literal), needle `using Chaos.Common.Abstractions;` → replacement:

```csharp
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
```

- [ ] **Step 4: Build the Members menu from the permissions**

Serena `replace_symbol_body`, name path `GuildMemberManagementScript/OnDisplayingInitial`. Body:

```csharp
    private void OnDisplayingInitial(Aisling source)
    {
        //ensure the player is still in a guild
        if (!IsInGuild(source, out var guild, out var sourceRank))
        {
            Subject.Reply(source, "You are not in a guild", "top");

            return;
        }

        //all members can see the roster
        Subject.AddOption("Roster", "generic_guild_members_roster_initial");

        //promote/demote, admit and kick are each a permission the leader switches on or off per rank. The scripts behind
        //them check the same permission again, and the rank-gap rules still decide who each can act on
        if (guild.HasPermission(source.Name, GuildPermission.PromoteDemote))
            Subject.AddOptions(
                ("Promote", "generic_guild_members_promote_initial"),
                ("Demote", "generic_guild_members_demote_initial"));

        //admitting is offered by the registrar in the tavern and by nobody else. This menu is shared by both
        //guild NPCs -- Aricin in the Abel tavern and Quill in the guild hall -- and an admission needs the
        //candidate standing next to the officer doing it. A guild hall is closed to non-members, so there is
        //never anybody in one to admit; the tavern is open to everybody, which is what makes it the place
        //recruiting happens.
        //
        //gated on the map rather than on which NPC was clicked, so the rule stays true if either of them moves,
        //and read from the same constant GuildMemberAdmitScript demands of the candidate, so the option appears
        //exactly where it would work
        if (guild.HasPermission(source.Name, GuildPermission.Admit)
            && (source.MapInstance.Name == GuildMemberAdmitScript.ADMISSION_MAP_NAME))
            Subject.AddOption("Admit", "generic_guild_members_admit_initial");

        if (guild.HasPermission(source.Name, GuildPermission.Kick))
            Subject.AddOption("Kick", "generic_guild_members_kick_initial");

        if (sourceRank.IsLeaderRank)
            Subject.AddOptions(("Promote to Leader", "generic_guild_members_transferownership_initial"));
    }
```

- [ ] **Step 5: Check the permission in each action script**

Serena `replace_content` (literal) in each file:

`GuildMemberAdmitScript.cs`, needle:

```csharp
        if (!sourceRank.IsOfficerRank)
        {
            Subject.Reply(source, "You do not have permission to admit members", "generic_guild_members_initial");
```

→ replacement:

```csharp
        if (!guild.HasPermission(source.Name, GuildPermission.Admit))
        {
            Subject.Reply(source, "Your rank can't admit new members.", "generic_guild_members_initial");
```

`GuildMemberKickScript.cs`, needle:

```csharp
        if (!sourceRank.IsOfficerRank)
        {
            Subject.Reply(source, "You do not have permission to kick members.", "generic_guild_members_initial");
```

→ replacement:

```csharp
        if (!guild.HasPermission(source.Name, GuildPermission.Kick))
        {
            Subject.Reply(source, "Your rank can't kick members.", "generic_guild_members_initial");
```

`GuildMemberPromoteScript.cs`, needle:

```csharp
        //ensure the player has permission to promote members
        if (!sourceRank.IsOfficerRank)
        {
            Subject.Reply(source, "You do not have permission to promote members.", "generic_guild_members_initial");
```

→ replacement:

```csharp
        //ensure the player's rank may promote members
        if (!guild.HasPermission(source.Name, GuildPermission.PromoteDemote))
        {
            Subject.Reply(source, "Your rank can't promote members.", "generic_guild_members_initial");
```

`GuildMemberDemoteScript.cs`, needle:

```csharp
        //ensure the player has permission to demote members (Tier 1+)
        if (!sourceRank.IsOfficerRank)
        {
            Subject.Reply(source, "You do not have permission to demote members.", "generic_guild_members_initial");
```

→ replacement:

```csharp
        //ensure the player's rank may demote members
        if (!guild.HasPermission(source.Name, GuildPermission.PromoteDemote))
        {
            Subject.Reply(source, "Your rank can't demote members.", "generic_guild_members_initial");
```

Leave the rank-gap checks (`IsSuperiorTo`, the promote `factor`) and the leader-only transfer check untouched. After this step, `sourceRank` is still used by those checks, so don't remove it.

- [ ] **Step 6: Check no officer check is left in the guild scripts**

Serena `search_for_pattern`, pattern `IsOfficerRank`, relative path `worktrees/rank-permissions-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts`.

Expected: no matches. (`GuildRank.IsOfficerRank` itself stays. Its own tests still use it.)

- [ ] **Step 7: Run the tests to check they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildMemberPermissionTests/*" --no-ansi`

Expected: every test passes.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberAdmitScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberKickScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberPromoteScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberDemoteScript.cs", "Tests/Chaos.Tests/GuildPermissions/GuildMemberPermissionTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildPermissions/GuildMemberPermissionTests/*\" --no-ansi", "acceptanceCriteria": ["Member in the tavern sees Roster, then Roster/Admit/Kick once switched on", "Council sees Roster/Promote/Demote/Kick, then Roster/Kick with PromoteDemote off", "kick refused with its message when off, nobody kicked", "Member with Kick kicks an Applicant but not a Member", "admit, promote, demote refused with their own message when off, no rank changes", "Promote to Leader still leader-only"], "modelTier": "standard"}
```

---

### Task 6: The leader's Permissions menu at Quill and Aricin

**Goal:** The leader picks a lower rank, sees one "<label>: On/Off" line per switch that rank can have, and clicks a line to flip it. Each flip saves the guild and writes a server log line.

**Files:**
- Create: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildPermissionsScript.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs` (Permissions option in both leader lists)
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions/generic_guild_permissions_initial.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions/generic_guild_permissions_rank.json`
- Test: `SRV/Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs`

**Acceptance Criteria:**
- [ ] The leader's guild menu has `Permissions` right after `Ranks` at Quill and at Aricin; a member's has none
- [ ] The rank list is `Council`, `Member`, `Applicant` (the guild's own names, in tier order); picking one stores `PermissionContext(tier)`
- [ ] Each rank menu lists exactly `GuildPermissionRules.ForTier(tier)` as `<label>: On/Off`, then `Done`
- [ ] A leader's click sets the opposite of what the line showed, saves the guild once, and logs it
- [ ] A non-leader is turned away with "Only the leader can change permissions.", and a non-leader's click changes nothing and saves nothing
- [ ] Both dialog files exist in `UNO` with the exact keys, text and script key below

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildPermissionsScriptTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.Scripting.DialogScripts.Temuair.GuildScripts;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
#endregion

namespace Chaos.Tests.GuildPermissions;

public sealed class GuildPermissionsScriptTests
{
    private const int APPLICANT_TIER = 3;
    private const int COUNCIL_TIER = 1;
    private const int MEMBER_TIER = 2;

    private static GuildPermissionsScript CreateScript(Dialog dialog, IStore<Guild>? store = null)
        => new(
            dialog,
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            store ?? new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildPermissionsScript>>().Object);

    private static (Guild Guild, Aisling Leader, Aisling Member) CreateGuild()
    {
        var guild = MockGuild.Create();
        var leader = MockAisling.Create(name: "Stahli");
        var member = MockAisling.Create(name: "Iglis");

        guild.AddMember(leader, member);
        guild.ChangeRank(leader.Name, 0, member);
        guild.AddMember(member, leader);
        guild.ChangeRank(member.Name, MEMBER_TIER, leader);

        return (guild, leader, member);
    }

    private static Dialog RankMenu(int tier)
    {
        var dialog = MockDialog.Create("generic_guild_permissions_rank");
        dialog.Context = new GuildPermissionsScript.PermissionContext(tier);

        return dialog;
    }

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
                "Members",
                "Disband",
                "Leave");

        GuildMenuAt("Aricin", leader)
            .Should()
            .Equal(
                "Ranks",
                "Permissions",
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

    [Test]
    public void The_rank_list_shows_the_three_lower_ranks()
    {
        var (_, leader, _) = CreateGuild();
        var dialog = MockDialog.Create("generic_guild_permissions_initial");

        CreateScript(dialog)
            .OnDisplaying(leader);

        dialog.Options
              .Select(option => option.OptionText)
              .Should()
              .Equal("Council", "Member", "Applicant");
    }

    [Test]
    public void A_member_is_turned_away()
    {
        var (_, _, member) = CreateGuild();
        var dialog = MockDialog.Create("generic_guild_permissions_initial");

        CreateScript(dialog)
            .OnDisplaying(member);

        dialog.Options
              .Should()
              .BeEmpty();

        member.ActiveDialog
              .Get()!
              .Text
              .Should()
              .Be("Only the leader can change permissions.");
    }

    [Test]
    public void Choosing_a_rank_remembers_its_tier()
    {
        var (_, leader, _) = CreateGuild();
        var dialog = MockDialog.Create("generic_guild_permissions_initial");
        var script = CreateScript(dialog);

        script.OnDisplaying(leader);
        script.OnNext(leader, 2);

        dialog.Context
              .Should()
              .Be(new GuildPermissionsScript.PermissionContext(MEMBER_TIER));
    }

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
                     "Done");
    }

    [Test]
    public void A_leader_click_switches_the_line_and_saves()
    {
        var (guild, leader, member) = CreateGuild();
        var store = new Mock<IStore<Guild>>();
        var dialog = RankMenu(MEMBER_TIER);
        var script = CreateScript(dialog, store.Object);

        script.OnDisplaying(leader);

        //"Take gold from the bank: Off"
        script.OnNext(leader, 2);

        guild.HasPermission(member.Name, GuildPermission.WithdrawGold)
             .Should()
             .BeTrue();

        guild.HasPermission(member.Name, GuildPermission.WithdrawItems)
             .Should()
             .BeFalse();

        store.Verify(s => s.Save(guild), Times.Once);
    }

    [Test]
    public void A_click_sets_the_opposite_of_what_the_line_showed()
    {
        var (guild, leader, member) = CreateGuild();
        var dialog = RankMenu(MEMBER_TIER);
        var script = CreateScript(dialog);

        //line 2 reads "Take gold from the bank: Off"
        script.OnDisplaying(leader);

        //a second leader turns it on before this click lands
        guild.SetRankPermission(MEMBER_TIER, GuildPermission.WithdrawGold, true);

        script.OnNext(leader, 2);

        guild.HasPermission(member.Name, GuildPermission.WithdrawGold)
             .Should()
             .BeTrue();
    }

    [Test]
    public void A_non_leader_click_changes_nothing()
    {
        var (guild, leader, member) = CreateGuild();
        var store = new Mock<IStore<Guild>>();
        var dialog = RankMenu(MEMBER_TIER);
        var script = CreateScript(dialog, store.Object);

        script.OnDisplaying(leader);
        script.OnNext(member, 2);

        guild.HasPermission(member.Name, GuildPermission.WithdrawGold)
             .Should()
             .BeFalse();

        store.Verify(s => s.Save(It.IsAny<Guild>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run the tests to check they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildPermissionsScriptTests/*" --no-ansi`

Expected: the build fails with `error CS0246: The type or namespace name 'GuildPermissionsScript' could not be found`.

- [ ] **Step 3: Write the menu script**

Create `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildPermissionsScript.cs`:

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
///     The leader's Permissions menu at Quill and Aricin: pick a lower rank, then switch what that rank may do on or off
/// </summary>
public class GuildPermissionsScript : GuildScriptBase
{
    private const string NOT_LEADER = "Only the leader can change permissions.";
    private const string OFF = ": Off";
    private const string ON = ": On";

    /// <inheritdoc />
    public GuildPermissionsScript(
        Dialog subject,
        IClientRegistry<IChaosWorldClient> clientRegistry,
        IStore<Guild> guildStore,
        IFactory<Guild> guildFactory,
        ILogger<GuildPermissionsScript> logger)
        : base(
            subject,
            clientRegistry,
            guildStore,
            guildFactory,
            logger) { }

    private static string Describe(GuildRank rank, GuildPermission permission)
        => GuildPermissionRules.Label(permission) + (rank.Allows(permission) ? ON : OFF);

    //the ranks below the leader, in tier order: the rank list's options, before the template's "Back"
    private static List<GuildRank> GetLowerRanks(Guild guild)
        => guild.GetRanks()
                .Where(rank => !rank.IsLeaderRank)
                .OrderBy(rank => rank.Tier)
                .ToList();

    private static bool IsLeader(Aisling source, [MaybeNullWhen(false)] out Guild guild)
        => IsInGuild(source, out guild, out var sourceRank) && sourceRank.IsLeaderRank;

    /// <inheritdoc />
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "generic_guild_permissions_initial":
                OnDisplayingInitial(source);

                break;
            case "generic_guild_permissions_rank":
                OnDisplayingRank(source);

                break;
        }
    }

    private void OnDisplayingInitial(Aisling source)
    {
        if (!IsLeader(source, out var guild))
        {
            Subject.Reply(source, NOT_LEADER, "top");

            return;
        }

        var index = 0;

        foreach (var rank in GetLowerRanks(guild))
            Subject.InsertOption(index++, rank.Name, "generic_guild_permissions_rank");
    }

    private void OnDisplayingRank(Aisling source)
    {
        if (!IsLeader(source, out var guild))
        {
            Subject.Reply(source, NOT_LEADER, "top");

            return;
        }

        if (Subject.Context is not PermissionContext context || !guild.TryGetRank(context.Tier, out var rank))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        Subject.InjectTextParameters(rank.Name);

        foreach (var permission in GuildPermissionRules.ForTier(rank.Tier))
            Subject.AddOption(Describe(rank, permission), "generic_guild_permissions_rank");

        Subject.AddOption("Done", "generic_guild_permissions_initial");
    }

    /// <inheritdoc />
    public override void OnNext(Aisling source, byte? optionIndex = null)
    {
        if (optionIndex is null)
            return;

        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "generic_guild_permissions_initial":
                OnNextInitial(source, optionIndex.Value);

                break;
            case "generic_guild_permissions_rank":
                OnNextRank(source, optionIndex.Value);

                break;
        }
    }

    private void OnNextInitial(Aisling source, byte optionIndex)
    {
        //a non-leader goes on to the rank menu, which turns them away
        if (!IsLeader(source, out var guild))
            return;

        var rank = GetLowerRanks(guild)
            .ElementAtOrDefault(optionIndex - 1);

        if (rank is not null)
            Subject.Context = new PermissionContext(rank.Tier);
    }

    private void OnNextRank(Aisling source, byte optionIndex)
    {
        if (Subject.Context is not PermissionContext context)
            return;

        //a non-leader goes on to the rank menu again, which turns them away
        if (!IsLeader(source, out var guild))
            return;

        //the options are the rank's permissions in ForTier order, then "Done"
        var permissions = GuildPermissionRules.ForTier(context.Tier);

        if ((optionIndex < 1) || (optionIndex > permissions.Count))
            return;

        var permission = permissions[optionIndex - 1];

        //set the opposite of what the clicked line showed, not of the current value. A guild can have more than one
        //leader; if two click the same line at once, each click does what its leader saw
        var lineText = Subject.GetOptionText(optionIndex);
        bool on;

        if (lineText?.EndsWith(OFF, StringComparison.Ordinal) == true)
            on = true;
        else if (lineText?.EndsWith(ON, StringComparison.Ordinal) == true)
            on = false;
        else
            return;

        guild.SetRankPermission(context.Tier, permission, on);

        //saved now rather than at the next timed save, so a crash can't undo a leader locking the bank
        GuildStore.Save(guild);

        Logger.WithTopics(Topics.Entities.Guild)
              .WithProperty(guild)
              .WithProperty(source)
              .LogInformation(
                  "Aisling {@AislingName} turned {Permission} {State} for guild {@GuildName} rank tier {RankTier}",
                  source.Name,
                  permission,
                  on ? "on" : "off",
                  guild.Name,
                  context.Tier);
    }

    /// <summary>
    ///     The rank the leader picked, carried to the rank menu in <see cref="Dialog.Context" />
    /// </summary>
    /// <param name="Tier">
    ///     The rank's tier
    /// </param>
    public sealed record PermissionContext(int Tier);
}
```

- [ ] **Step 4: Add Permissions to the leader's guild menu**

In `worktrees/rank-permissions-server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs`, Serena `replace_content` in regex mode, `allow_multiple_occurrences: true` (two matches, one per leader list). Needle:

```
( *)\("Ranks", "generic_guild_ranks_initial"\),
```

→ replacement:

```
$!1("Ranks", "generic_guild_ranks_initial"),
$!1("Permissions", "generic_guild_permissions_initial"),
```

- [ ] **Step 5: Add the two dialogs**

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions/generic_guild_permissions_initial.json`:

```json
{
  "options": [
    {
      "dialogKey": "Top",
      "optionText": "Back"
    }
  ],
  "scriptKeys": [
    "guildPermissions"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_permissions_initial",
  "text": "Which rank do you want to change? The leader can always do everything.",
  "type": "Menu"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions/generic_guild_permissions_rank.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "guildPermissions"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_permissions_rank",
  "text": "What the {RankName} rank may do. Choose a line to switch it on or off.",
  "type": "Menu"
}
```

- [ ] **Step 6: Run the tests to check they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildPermissions/GuildPermissionsScriptTests/*" --no-ansi`

Expected: every test passes.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildPermissionsScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs", "UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions/generic_guild_permissions_initial.json", "UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions/generic_guild_permissions_rank.json", "Tests/Chaos.Tests/GuildPermissions/GuildPermissionsScriptTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildPermissions/GuildPermissionsScriptTests/*\" --no-ansi", "acceptanceCriteria": ["leader menu has Permissions after Ranks at Quill and Aricin; member none", "rank list is the three lower ranks in tier order; picking stores PermissionContext(tier)", "rank menu lists ForTier(tier) as label: On/Off then Done", "leader click sets the opposite of the line, saves once, logs", "non-leader turned away; non-leader click changes and saves nothing", "both UNO dialog files exist with exact keys, text and script key"], "modelTier": "standard"}
```

---

### Task 7: Commit the full implementation

**Goal:** Every test passes except the two known failures. The server branch, the Unora branch and this plan are each committed once.

**Files:**
- Commit: every file listed in Tasks 1–6, in `SRV` and `UNO`
- Commit: `Chaos.Client/docs/superpowers/plans/2026-09-25-guild-rank-permissions.md` and its `.tasks.json` (on `Chaos.Client` `main`, by path)

**Acceptance Criteria:**
- [ ] The full `Chaos.Tests` run fails only `GiveAbility` and `OnItemDroppedOn` (stackable)
- [ ] `SRV` has one new commit on `feat/guild-rank-permissions` with only this plan's server files
- [ ] `UNO` has one new commit on `feat/guild-rank-permissions` with only the two dialog files, and no `Custom Client Mods` build output
- [ ] The plan and tasks file are committed on `Chaos.Client` `main`, with nothing else in that commit

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server log --oneline -1` → the rank permissions commit

**Steps:**

- [ ] **Step 1: Run the full server test suite**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi
```

Expected: every test passes except `GiveAbility` and `OnItemDroppedOn` (stackable). Any other failure: stop and fix it in the task that caused it.

- [ ] **Step 2: Commit the server worktree**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server
git status --short
git add -- Chaos.DarkAges/Definitions/Enums.cs Chaos/Collections/GuildPermissionRules.cs Chaos/Collections/GuildRank.cs Chaos/Collections/Guild.cs Chaos/Collections/BankPermissions.cs Chaos.Schemas/Guilds/GuildRankSchema.cs Chaos/Services/MapperProfiles/GuildMapperProfile.cs Chaos/Services/Servers/WorldServer.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBuffScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberAdmitScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberKickScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberPromoteScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberDemoteScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildPermissionsScript.cs Tests/Chaos.Tests/GuildPermissions Tests/Chaos.Tests/BankPermissionsTests.cs Tests/Chaos.Tests/ComplexActionHelperTests.cs
git status --short
git commit -F - <<'EOF'
Let guild leaders set what each rank may do

The leader switches seven permissions on or off for each lower rank at
Quill or Aricin: taking items or gold from the guild bank, paying for
buffs with guild gold, buying hall upgrades, admitting, kicking, and
promoting/demoting. Rank files saved before this load with the old
officer rules, so nothing changes until a leader flips a switch.

The buff payment menu now reads the clicked option by its text, so a
late click after the permission goes off is refused rather than charged
to the member's own gold. The house deed purchase step now checks for
the leader itself.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

Before committing, the second `git status --short` must show nothing staged outside the list above. If `appsettings.json` or `launchSettings.json` show as modified, leave them unstaged.

- [ ] **Step 3: Commit the Unora worktree**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/rank-permissions-unora
git status --short
git add -- "Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions"
git status --short
git commit -F - <<'EOF'
Add the guild Permissions menu dialogs for Quill and Aricin

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

Leave any modified `Custom Client Mods/**/obj` files unstaged. They are build output.

- [ ] **Step 4: Commit the plan on Chaos.Client `main`**

`docs/` is gitignored in Chaos.Client, so use `-f`. Commit by path so nothing else in the shared checkout is included:

```bash
cd /c/Users/Michael/Documents/GitHub/Chaos.Client
git add -f -- docs/superpowers/plans/2026-09-25-guild-rank-permissions.md docs/superpowers/plans/2026-09-25-guild-rank-permissions.md.tasks.json
git commit -F - -- docs/superpowers/plans/2026-09-25-guild-rank-permissions.md docs/superpowers/plans/2026-09-25-guild-rank-permissions.md.tasks.json <<'EOF'
Add the guild rank permissions implementation plan

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 5: Report the in-game check**

Merging, and pointing Chaos.Client's `Chaos-Server` submodule at the merged master, come next (superpowers-extended-cc:finishing-a-development-branch). They aren't part of this task. Report the check for the user to run in game after the merge:

1. As the guild leader at Quill: Permissions, Council, "Take gold from the bank: On". The line changes to Off.
2. As a Council member at the guild bank: try to take gold. The orange bar says "Your rank can't take gold from the guild bank." Taking an item still works.
3. As the leader: Permissions, Member, "Take items from the bank: Off". The line changes to On.
4. As a Member at the guild bank: take a potion. It works. Taking gold is refused.
5. As a Member at Quill: Buffs, a duration. "Pay from Guild Bank" isn't offered.
6. At Aricin in the Abel tavern: the leader's guild menu shows Permissions after Ranks. A Member's doesn't.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/rank-permissions-server log --oneline -1", "acceptanceCriteria": ["full Chaos.Tests run fails only GiveAbility and OnItemDroppedOn (stackable)", "one SRV commit with only this plan's server files", "one UNO commit with only the two dialog files and no Custom Client Mods obj output", "plan and tasks file committed alone on Chaos.Client main"], "modelTier": "mechanical"}
```
