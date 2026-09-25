# Guild Tuition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Guild members can pay for skill training, spell training and repairs with guild bank gold. Each rank has a weekly training allowance and a weekly repair allowance, and the guild leader sets both at Quill.

**Architecture:** Each `GuildRank` stores its two allowances. Each `Guild` stores a weekly ledger (`GuildAllowanceLedger`) of what each member spent, and works out and takes payments under its existing lock (`QuoteAllowance`, `TryPayWithAllowance`). A static `GuildFundsHelper` gives trainers and smiths the quote, the split message and the payment step. That step also writes the guild bank log and the server log. Dialog steps carry the agreed split in `Dialog.Context` as a `GuildFundsQuoteContext`. The new `GuildAllowanceScript` runs Quill's Allowances menu. The guild bank's View Logs menu gets an Allowance Spending list.

**Tech Stack:** C# 14 / .NET 10, Chaos-Server dialog scripts, `IStorage<T>` JSON storage with `System.Text.Json` source generation, TUnit + FluentAssertions + Moq, Unora JSON dialog templates.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-09-25-guild-tuition-design.md` (committed on `main` as `219e95f`).

## Global Constraints

- **Work only in the two worktrees from Task 0.** Never edit, stage, stash, reset or switch branches in the shared checkouts (`C:/Users/Michael/Documents/GitHub/Chaos.Client`, its `Chaos-Server` submodule, `C:/Users/Michael/Documents/GitHub/Unora`). Other Claude sessions work in them. The guild cloak session also has its own worktrees (`worktrees/guild-cloak-*`). Never touch those.
  - `SRV` = `C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server` (Chaos-Server, branch `feat/guild-tuition` from `master`)
  - `UNO` = `C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-unora` (Unora, branch `feat/guild-tuition` from `main`)
- **Do not commit** in Tasks 0–8. Leave all changes in the worktrees. Task 9 makes one commit per repo (at-end strategy).
- **Test projects are TUnit executables.** Use `dotnet run --project ... -- --treenode-filter "..." --no-ansi`, never `dotnet test`. The filter pattern is `/Assembly/Namespace/Class/Method`.
- **Two server tests already fail on master:** `GiveAbility` and `OnItemDroppedOn` (stackable). Leave them alone. Every other test must pass.
- **If a build fails with MSB3027** (a file is locked), something is running from that output folder. Stop and report. Don't kill any process.
- **Serena for C#.** Read and edit C# files with Serena's tools, as the user's global CLAUDE.md requires. Serena paths are relative to `C:/Users/Michael/Documents/GitHub`, so a server file is `worktrees/guild-tuition-server/Chaos/...`. JSON files use the built-in Read/Edit/Write tools.
- **Keep edits to shared files small.** The guild cloak branch also changes `Chaos/Collections/Guild.cs` (in `AddMember`, `Disband`, `TryKickMember`, `TryLeave` and the `using` block) and `Chaos/Models/World/GuildHouseState.cs` (the property switches). Add new `Guild` members only after `SetTaxRate`, add no `using` lines to `Guild.cs`, and add the new transaction type only at the end of the `TransactionType` enum. Don't edit `Tests/Chaos.Tests/GuildHall/GuildHouseStateTests.cs` or `Chaos/SerializationContext.cs`.
- **Never stage** `Chaos/appsettings.json`, `launchSettings.json`, or anything under `UNO/Custom Client Mods/**/obj`.
- **The game font is 6×12 ASCII.** Use only ASCII in dialog text and messages. No em dashes, no curly quotes.
- **Exact values:**

  | Value | Setting |
  |---|---|
  | Allowance choices (gold per week) | 0 (Off), 100,000, 250,000, 500,000, 1,000,000, 2,500,000, 5,000,000 |
  | Default allowance | 0 (Off) for every rank |
  | Week start | the latest Sunday 20:00 UTC at or before now |
  | Guild share | the smallest of cost, allowance left and guild bank gold |
  | Guild-funds option text | `Use guild funds` |
  | New log type | `GuildHouseState.TransactionType.AllowanceSpend` (last enum value) |
  | Log descriptions | `Training: <name>`, `Repairs: all items`, `Repairs: <item name>` |
  | Log list length | newest 30, like the other bank logs |
  | Quill script key | `guildAllowance` (class `GuildAllowanceScript`) |

**User decisions (already made):**
- Guild tuition is idea 32 in `Unora/docs/guild-hall-ideas.md`, picked second after the guild cloak. It includes guild-paid repairs.
- The limit is a weekly gold allowance per member, set per rank.
- Training and repairs have two separate allowances per rank. An allowance of 0 turns that option off.
- The member chooses each time whether to use guild funds.
- Guild-paid repairs work at every smith, not only Fixx.
- When the allowance or the bank can't cover the whole cost, the cost is split. The prompt tells the member the split before they agree.
- Storage approach A: allowances are saved on the ranks, weekly spending is saved on the guild.
- The week starts Sunday 20:00 UTC, the same moment as the casino lottery draw.
- Only the leader changes allowances, from a fixed list. Members see their own rank's allowances and what's left.
- Saying "Repair All" out loud keeps using only the member's gold.
- Item requirements for training always come from the member.

**Changes from the spec (decided while planning):**
1. The stale-quote message is "The cost or your guild's funds changed. Please try again." A repair can get more expensive between the prompt and "Yes" if the member takes damage, so the check compares the cost too.
2. If the guild can't pay when the guild-funds prompt opens, the trainer or smith says "Your guild can't pay toward this right now." This can happen if another member spent the last of the bank in between.
3. A leader's allowance change is announced in guild chat and written to the server log, like tax changes.
4. The Allowance Spending list shows the guild's share, which is the gold that left the guild bank.
5. Known and unchanged: trainers take gold before checking that the skill or spell book has room. With guild funds, that gold includes the guild's share. Fixing it is out of scope.
6. `LearnSkillScript` gets no new unit tests. It changes the same way as `LearnSpellScript`, which does get tests, and the in-game check covers both.

---

## File map

| Repo | File | Responsibility |
|---|---|---|
| SRV | `Chaos/Collections/GuildAllowanceKind.cs` | Training or Repairs |
| SRV | `Chaos/Collections/GuildAllowanceSpending.cs` | one member's spending this week |
| SRV | `Chaos/Collections/GuildAllowanceLedger.cs`, `GuildAllowanceLedgerSnapshot.cs` | weekly ledger, week start, rollover |
| SRV | `Chaos/Collections/GuildAllowanceStatus.cs`, `GuildAllowanceQuote.cs`, `GuildAllowancePayResult.cs` | results of guild allowance calls |
| SRV | `Chaos/Collections/GuildRank.cs` | the two allowances per rank |
| SRV | `Chaos/Collections/Guild.cs` | ledger field; quote, pay, status, set, save and restore methods |
| SRV | `Chaos.Schemas/Guilds/GuildAllowanceSpendingSchema.cs`, `GuildSchema.cs`, `GuildRankSchema.cs` | saved fields |
| SRV | `Chaos/Services/MapperProfiles/GuildMapperProfile.cs` | map the saved fields |
| SRV | `Chaos/Models/World/GuildHouseState.cs` | `AllowanceSpend` log type |
| SRV | `Chaos/Utilities/GuildFundsHelper.cs`, `GuildFundsQuoteContext.cs` | quote, split text, pay and log for trainers and smiths |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSkillScript.cs`, `LearnSpellScript.cs` | guild-funds option and payment |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/Generic/RepairAllItemsScript.cs`, `RepairSingleItemScript.cs` | guild-funds option and payment |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildAllowanceScript.cs` | Quill's Allowances menu |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs` | Allowances option at Quill |
| SRV | `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBankManagementScript.cs` | Allowance Spending log view |
| SRV | `Tests/Chaos.Tests/GuildAllowances/*.cs`, `Tests/Chaos.Tests/Trainers/LearnSpellScriptTests.cs` | tests |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSkill/generic_learnskill_guild*.json` | skill guild-funds dialogs |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSpell/generic_learnspell_guild*.json` | spell guild-funds dialogs |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repair*Guild*.json` | repair guild-funds dialogs |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement/*.json` | Quill menus |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash/stash_guildbankallowancelog.json`, `stash_guildbanklogs.json` | log view |

---

### Task 0: Create the two worktrees

**Goal:** Isolated `feat/guild-tuition` branches for the server and Unora, so no shared checkout is touched.

**Files:**
- Create: worktrees `SRV` and `UNO` (see Global Constraints)

**Acceptance Criteria:**
- [ ] `git -C <each worktree> branch --show-current` prints `feat/guild-tuition`
- [ ] `SRV/Tests/Chaos.Tests` builds, and `GuildTests` passes before any change

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server branch --show-current` → `feat/guild-tuition`

**Steps:**

- [ ] **Step 1: Check the worktrees don't exist yet**

```bash
ls /c/Users/Michael/Documents/GitHub/worktrees/
```

Expected: no `guild-tuition-server` or `guild-tuition-unora`. If either exists, stop and report. A previous run may have started them.

- [ ] **Step 2: Create the worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server -b feat/guild-tuition master
git -C Unora -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/guild-tuition-unora -b feat/guild-tuition main
```

Pass `core.longpaths` with `-c` only. Never write it to the repo config.

- [ ] **Step 3: Baseline build and tests**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildTests/*" --no-ansi
```

Expected: `Build succeeded`, and every `GuildTests` test passes.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server branch --show-current", "acceptanceCriteria": ["both worktrees on feat/guild-tuition", "SRV Chaos.Tests builds and GuildTests pass before any change"], "modelTier": "mechanical"}
```

---

### Task 1: Allowance kinds and the weekly ledger

**Goal:** A `GuildAllowanceLedger` that records each member's guild-paid spending for the current week and clears itself when a new week starts (Sunday 20:00 UTC).

**Files:**
- Create: `SRV/Chaos/Collections/GuildAllowanceKind.cs`
- Create: `SRV/Chaos/Collections/GuildAllowanceSpending.cs`
- Create: `SRV/Chaos/Collections/GuildAllowanceLedgerSnapshot.cs`
- Create: `SRV/Chaos/Collections/GuildAllowanceLedger.cs`
- Test: `SRV/Tests/Chaos.Tests/GuildAllowances/GuildAllowanceLedgerTests.cs`

**Acceptance Criteria:**
- [ ] `GetWeekStart` returns the latest Sunday 20:00 UTC at or before the given time (four cases in the test)
- [ ] Spending adds up within a week, and member names ignore case
- [ ] Spending is cleared at the first read on or after the next week start
- [ ] `Restore` keeps spending from the current week and drops spending from an earlier week
- [ ] A negative amount throws `ArgumentOutOfRangeException`

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildAllowanceLedgerTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildAllowances/GuildAllowanceLedgerTests.cs`:

```csharp
#region
using System.Globalization;
using Chaos.Collections;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildAllowances;

public sealed class GuildAllowanceLedgerTests
{
    private static readonly DateTime Wednesday = Utc("2026-09-30T12:00:00Z");

    private static DateTime Utc(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    //formatter:off
    [Test]
    [Arguments("2026-09-27T19:59:59Z", "2026-09-20T20:00:00Z")]
    [Arguments("2026-09-27T20:00:00Z", "2026-09-27T20:00:00Z")]
    [Arguments("2026-09-30T12:00:00Z", "2026-09-27T20:00:00Z")]
    [Arguments("2026-10-03T23:59:59Z", "2026-09-27T20:00:00Z")]
    //formatter:on
    public void GetWeekStart_returns_the_latest_Sunday_2000_UTC_at_or_before_now(string now, string expected)
        => GuildAllowanceLedger.GetWeekStart(Utc(now))
                               .Should()
                               .Be(Utc(expected));

    [Test]
    public void Spending_adds_up_within_a_week()
    {
        var ledger = new GuildAllowanceLedger();

        ledger.AddSpending("Iglis", GuildAllowanceKind.Training, 100_000, Wednesday);
        ledger.AddSpending("Iglis", GuildAllowanceKind.Training, 50_000, Wednesday);
        ledger.AddSpending("Iglis", GuildAllowanceKind.Repairs, 7_000, Wednesday);

        ledger.GetSpending("Iglis", Wednesday)
              .Should()
              .Be(new GuildAllowanceSpending(150_000, 7_000));
    }

    [Test]
    public void Member_names_ignore_case()
    {
        var ledger = new GuildAllowanceLedger();

        ledger.AddSpending("Iglis", GuildAllowanceKind.Training, 100_000, Wednesday);

        ledger.GetSpending("IGLIS", Wednesday)
              .Training
              .Should()
              .Be(100_000);
    }

    [Test]
    public void Spending_is_cleared_when_a_new_week_starts()
    {
        var ledger = new GuildAllowanceLedger();

        ledger.AddSpending("Iglis", GuildAllowanceKind.Training, 100_000, Utc("2026-09-27T19:00:00Z"));

        ledger.GetSpending("Iglis", Utc("2026-09-27T19:59:59Z"))
              .Training
              .Should()
              .Be(100_000);

        ledger.GetSpending("Iglis", Utc("2026-09-27T20:00:00Z"))
              .Should()
              .Be(GuildAllowanceSpending.None);
    }

    [Test]
    public void Restore_keeps_spending_from_the_current_week()
    {
        var ledger = new GuildAllowanceLedger();

        ledger.Restore(
            Utc("2026-09-27T20:00:00Z"),
            [KeyValuePair.Create("Iglis", new GuildAllowanceSpending(100_000, 5_000))]);

        var snapshot = ledger.Snapshot(Wednesday);

        snapshot.WeekStart
                .Should()
                .Be(Utc("2026-09-27T20:00:00Z"));

        snapshot.Spending["iglis"]
                .Should()
                .Be(new GuildAllowanceSpending(100_000, 5_000));
    }

    [Test]
    public void Restore_of_an_earlier_week_is_dropped_on_the_first_read()
    {
        var ledger = new GuildAllowanceLedger();

        ledger.Restore(
            Utc("2026-09-20T20:00:00Z"),
            [KeyValuePair.Create("Iglis", new GuildAllowanceSpending(100_000, 5_000))]);

        var snapshot = ledger.Snapshot(Wednesday);

        snapshot.WeekStart
                .Should()
                .Be(Utc("2026-09-27T20:00:00Z"));

        snapshot.Spending
                .Should()
                .BeEmpty();
    }

    [Test]
    public void AddSpending_rejects_a_negative_amount()
    {
        var ledger = new GuildAllowanceLedger();

        var act = () => ledger.AddSpending("Iglis", GuildAllowanceKind.Training, -1, Wednesday);

        act.Should()
           .Throw<ArgumentOutOfRangeException>();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildAllowanceLedgerTests/*" --no-ansi`
Expected: build error, `GuildAllowanceLedger` not found.

- [ ] **Step 3: Write the types**

Create `SRV/Chaos/Collections/GuildAllowanceKind.cs`:

```csharp
namespace Chaos.Collections;

/// <summary>
///     What a guild's weekly allowance pays toward
/// </summary>
public enum GuildAllowanceKind
{
    /// <summary>
    ///     Learning skills and spells from trainers
    /// </summary>
    Training,

    /// <summary>
    ///     Repairs at smiths
    /// </summary>
    Repairs
}
```

Create `SRV/Chaos/Collections/GuildAllowanceSpending.cs`:

```csharp
namespace Chaos.Collections;

/// <summary>
///     Gold one member has spent from their guild's allowances in the current week
/// </summary>
/// <param name="Training">
///     Gold spent on training
/// </param>
/// <param name="Repairs">
///     Gold spent on repairs
/// </param>
public sealed record GuildAllowanceSpending(int Training, int Repairs)
{
    /// <summary>
    ///     Nothing spent
    /// </summary>
    public static GuildAllowanceSpending None { get; } = new(0, 0);

    /// <summary>
    ///     A copy with <paramref name="amount" /> more spent on <paramref name="kind" />
    /// </summary>
    public GuildAllowanceSpending Add(GuildAllowanceKind kind, int amount)
        => kind switch
        {
            GuildAllowanceKind.Training => this with
            {
                Training = Training + amount
            },
            GuildAllowanceKind.Repairs => this with
            {
                Repairs = Repairs + amount
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    /// <summary>
    ///     The gold spent on <paramref name="kind" />
    /// </summary>
    public int Get(GuildAllowanceKind kind)
        => kind switch
        {
            GuildAllowanceKind.Training => Training,
            GuildAllowanceKind.Repairs  => Repairs,
            _                           => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
```

Create `SRV/Chaos/Collections/GuildAllowanceLedgerSnapshot.cs`:

```csharp
namespace Chaos.Collections;

/// <summary>
///     A copy of a guild's allowance ledger, safe to read outside the guild's lock
/// </summary>
/// <param name="WeekStart">
///     The start of the week the spending belongs to
/// </param>
/// <param name="Spending">
///     Gold spent this week, by member name (names ignore case)
/// </param>
public sealed record GuildAllowanceLedgerSnapshot(DateTime WeekStart, IReadOnlyDictionary<string, GuildAllowanceSpending> Spending);
```

Create `SRV/Chaos/Collections/GuildAllowanceLedger.cs`:

```csharp
namespace Chaos.Collections;

/// <summary>
///     What each member of one guild has spent from the guild's allowances this week
/// </summary>
/// <remarks>
///     Not thread safe. The owning <see cref="Guild" /> only touches it under its lock. No timer clears it: every read and
///     write first checks whether a new week has started, and if so drops the old week's spending
/// </remarks>
public sealed class GuildAllowanceLedger
{
    private readonly Dictionary<string, GuildAllowanceSpending> Spending = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The start of the week the recorded spending belongs to
    /// </summary>
    public DateTime WeekStart { get; private set; }

    /// <summary>
    ///     Records <paramref name="amount" /> more gold spent by <paramref name="memberName" />
    /// </summary>
    public void AddSpending(string memberName, GuildAllowanceKind kind, int amount, DateTime utcNow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        RollOver(utcNow);

        var current = Spending.GetValueOrDefault(memberName, GuildAllowanceSpending.None);
        Spending[memberName] = current.Add(kind, amount);
    }

    /// <summary>
    ///     What <paramref name="memberName" /> has spent this week
    /// </summary>
    public GuildAllowanceSpending GetSpending(string memberName, DateTime utcNow)
    {
        RollOver(utcNow);

        return Spending.GetValueOrDefault(memberName, GuildAllowanceSpending.None);
    }

    /// <summary>
    ///     The start of the allowance week that contains <paramref name="utcNow" />: the latest Sunday 20:00 UTC at or before
    ///     it. The casino lottery draws at the same moment
    /// </summary>
    public static DateTime GetWeekStart(DateTime utcNow)
    {
        var sundayAt2000 = utcNow.Date
                                 .AddDays(-(int)utcNow.DayOfWeek)
                                 .AddHours(20);

        return sundayAt2000 <= utcNow ? sundayAt2000 : sundayAt2000.AddDays(-7);
    }

    /// <summary>
    ///     Replaces the ledger with saved spending
    /// </summary>
    public void Restore(DateTime weekStart, IEnumerable<KeyValuePair<string, GuildAllowanceSpending>> spending)
    {
        WeekStart = weekStart;
        Spending.Clear();

        foreach ((var memberName, var amount) in spending)
            Spending[memberName] = amount;
    }

    /// <summary>
    ///     A copy of this week's spending
    /// </summary>
    public GuildAllowanceLedgerSnapshot Snapshot(DateTime utcNow)
    {
        RollOver(utcNow);

        return new GuildAllowanceLedgerSnapshot(
            WeekStart,
            new Dictionary<string, GuildAllowanceSpending>(Spending, StringComparer.OrdinalIgnoreCase));
    }

    private void RollOver(DateTime utcNow)
    {
        var currentWeekStart = GetWeekStart(utcNow);

        if (WeekStart >= currentWeekStart)
            return;

        Spending.Clear();
        WeekStart = currentWeekStart;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildAllowanceLedgerTests/*" --no-ansi`
Expected: all 10 tests pass (the week-start test counts as 4).

```json:metadata
{"files": ["Chaos/Collections/GuildAllowanceKind.cs", "Chaos/Collections/GuildAllowanceSpending.cs", "Chaos/Collections/GuildAllowanceLedgerSnapshot.cs", "Chaos/Collections/GuildAllowanceLedger.cs", "Tests/Chaos.Tests/GuildAllowances/GuildAllowanceLedgerTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildAllowances/GuildAllowanceLedgerTests/*\" --no-ansi", "acceptanceCriteria": ["GetWeekStart returns the latest Sunday 20:00 UTC at or before the given time", "spending adds up within a week and names ignore case", "spending is cleared on or after the next week start", "Restore keeps current-week spending and drops earlier weeks", "negative amounts throw ArgumentOutOfRangeException"], "modelTier": "mechanical"}
```

---

### Task 2: Rank allowances and guild payments

**Goal:** Ranks hold a training allowance and a repair allowance. The guild can report a member's allowance, quote a split, and take a split payment all-or-nothing under its lock.

**Files:**
- Create: `SRV/Chaos/Collections/GuildAllowanceStatus.cs`
- Create: `SRV/Chaos/Collections/GuildAllowanceQuote.cs`
- Create: `SRV/Chaos/Collections/GuildAllowancePayResult.cs`
- Modify: `SRV/Chaos/Collections/GuildRank.cs` (two properties, `GetAllowance`, `SetAllowance`)
- Modify: `SRV/Chaos/Collections/Guild.cs` (one field; new members after `SetTaxRate`)
- Test: `SRV/Tests/Chaos.Tests/GuildAllowances/GuildAllowancePaymentTests.cs`

**Acceptance Criteria:**
- [ ] The quote's guild share is the smallest of cost, allowance left and bank gold; `LimitedByBank` is true only when the bank capped it
- [ ] The quote offers nothing when the allowance is off, the cost is 0, or the name holds no rank
- [ ] A successful payment takes both shares and records the guild share
- [ ] A payment where the member can't afford their share, or the guild share changed, takes nothing from either side
- [ ] Promotion keeps the week's spending and applies the new rank's allowance; leaving and rejoining keeps it too
- [ ] Spending resets at Sunday 20:00 UTC

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/*/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildAllowances/GuildAllowancePaymentTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Models.World;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildAllowances;

public sealed class GuildAllowancePaymentTests
{
    //AddMember puts new members in the lowest rank, tier 3
    private const int NEWEST_TIER = 3;
    private const int MEMBER_TIER = 2;
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private static (Guild Guild, Aisling Member, Aisling Leader) CreateGuild(int trainingAllowance, uint bankGold, int memberGold)
    {
        var guild = MockGuild.Create();
        var leader = MockAisling.Create(name: "Stahli");
        var member = MockAisling.Create(name: "Iglis");

        guild.AddMember(member, leader);
        guild.SetRankAllowance(NEWEST_TIER, GuildAllowanceKind.Training, trainingAllowance);
        guild.Bank.AddGold(bankGold);
        member.Gold = memberGold;

        return (guild, member, leader);
    }

    [Test]
    public void Quote_covers_the_whole_cost_when_the_allowance_and_bank_allow_it()
    {
        var (guild, member, _) = CreateGuild(500_000, 1_000_000, 0);

        guild.QuoteAllowance(member.Name, GuildAllowanceKind.Training, 150_000, Now)
             .Should()
             .Be(new GuildAllowanceQuote(150_000, 500_000, 150_000, 0, false));
    }

    [Test]
    public void Quote_is_capped_by_the_allowance_left()
    {
        var (guild, member, _) = CreateGuild(200_000, 1_000_000, 0);

        guild.QuoteAllowance(member.Name, GuildAllowanceKind.Training, 500_000, Now)
             .Should()
             .Be(new GuildAllowanceQuote(500_000, 200_000, 200_000, 300_000, false));
    }

    [Test]
    public void Quote_is_capped_by_the_guild_bank()
    {
        var (guild, member, _) = CreateGuild(500_000, 50_000, 0);

        guild.QuoteAllowance(member.Name, GuildAllowanceKind.Training, 500_000, Now)
             .Should()
             .Be(new GuildAllowanceQuote(500_000, 500_000, 50_000, 450_000, true));
    }

    [Test]
    public void Quote_offers_nothing_when_the_allowance_is_off()
    {
        var (guild, member, _) = CreateGuild(0, 1_000_000, 0);

        var quote = guild.QuoteAllowance(member.Name, GuildAllowanceKind.Training, 150_000, Now);

        quote.CanUseGuildFunds
             .Should()
             .BeFalse();

        quote.MemberShare
             .Should()
             .Be(150_000);
    }

    [Test]
    public void Quote_offers_nothing_for_a_free_purchase()
    {
        var (guild, member, _) = CreateGuild(500_000, 1_000_000, 0);

        guild.QuoteAllowance(member.Name, GuildAllowanceKind.Training, 0, Now)
             .CanUseGuildFunds
             .Should()
             .BeFalse();
    }

    [Test]
    public void Quote_offers_nothing_to_someone_outside_the_guild()
    {
        var (guild, _, _) = CreateGuild(500_000, 1_000_000, 0);

        guild.QuoteAllowance("Stranger", GuildAllowanceKind.Training, 150_000, Now)
             .CanUseGuildFunds
             .Should()
             .BeFalse();
    }

    [Test]
    public void Pay_takes_both_shares_and_records_the_guild_share()
    {
        var (guild, member, _) = CreateGuild(200_000, 1_000_000, 400_000);

        var result = guild.TryPayWithAllowance(
            member,
            GuildAllowanceKind.Training,
            500_000,
            200_000,
            out var quote,
            Now);

        result.Should()
              .Be(GuildAllowancePayResult.Paid);

        quote.MemberShare
             .Should()
             .Be(300_000);

        guild.Bank
             .Gold
             .Should()
             .Be(800_000u);

        member.Gold
              .Should()
              .Be(100_000);

        guild.GetAllowanceStatus(member.Name, GuildAllowanceKind.Training, Now)
             .Should()
             .Be(new GuildAllowanceStatus(200_000, 200_000));
    }

    [Test]
    public void Pay_takes_nothing_when_the_member_cannot_afford_their_share()
    {
        var (guild, member, _) = CreateGuild(200_000, 1_000_000, 100_000);

        guild.TryPayWithAllowance(
                 member,
                 GuildAllowanceKind.Training,
                 500_000,
                 200_000,
                 out _,
                 Now)
             .Should()
             .Be(GuildAllowancePayResult.MemberCannotAfford);

        guild.Bank
             .Gold
             .Should()
             .Be(1_000_000u);

        member.Gold
              .Should()
              .Be(100_000);

        guild.GetAllowanceStatus(member.Name, GuildAllowanceKind.Training, Now)
             .Spent
             .Should()
             .Be(0);
    }

    [Test]
    public void Pay_takes_nothing_when_the_guild_share_changed()
    {
        var (guild, member, _) = CreateGuild(200_000, 1_000_000, 400_000);

        guild.TryPayWithAllowance(
                 member,
                 GuildAllowanceKind.Training,
                 500_000,
                 150_000,
                 out _,
                 Now)
             .Should()
             .Be(GuildAllowancePayResult.QuoteChanged);

        guild.Bank
             .Gold
             .Should()
             .Be(1_000_000u);

        member.Gold
              .Should()
              .Be(400_000);
    }

    [Test]
    public void Pay_refuses_someone_outside_the_guild()
    {
        var (guild, _, _) = CreateGuild(500_000, 1_000_000, 0);
        var stranger = MockAisling.Create(name: "Stranger");
        stranger.Gold = 1_000_000;

        guild.TryPayWithAllowance(
                 stranger,
                 GuildAllowanceKind.Training,
                 100_000,
                 100_000,
                 out _,
                 Now)
             .Should()
             .Be(GuildAllowancePayResult.NotInGuild);

        guild.Bank
             .Gold
             .Should()
             .Be(1_000_000u);
    }

    [Test]
    public void Promotion_keeps_the_week_spending_and_applies_the_new_allowance()
    {
        var (guild, member, leader) = CreateGuild(500_000, 2_000_000, 0);
        guild.SetRankAllowance(MEMBER_TIER, GuildAllowanceKind.Training, 1_000_000);

        guild.TryPayWithAllowance(
                 member,
                 GuildAllowanceKind.Training,
                 500_000,
                 500_000,
                 out _,
                 Now)
             .Should()
             .Be(GuildAllowancePayResult.Paid);

        guild.ChangeRank(member.Name, MEMBER_TIER, leader);

        guild.GetAllowanceStatus(member.Name, GuildAllowanceKind.Training, Now)
             .Left
             .Should()
             .Be(500_000);
    }

    [Test]
    public void Leaving_and_rejoining_in_the_same_week_keeps_the_spending()
    {
        var (guild, member, leader) = CreateGuild(500_000, 1_000_000, 0);

        guild.TryPayWithAllowance(
                 member,
                 GuildAllowanceKind.Training,
                 500_000,
                 500_000,
                 out _,
                 Now)
             .Should()
             .Be(GuildAllowancePayResult.Paid);

        guild.TryLeave(member)
             .Should()
             .BeTrue();

        guild.AddMember(member, leader);

        guild.GetAllowanceStatus(member.Name, GuildAllowanceKind.Training, Now)
             .Left
             .Should()
             .Be(0);
    }

    [Test]
    public void Spending_resets_when_the_week_starts_on_Sunday_at_2000_UTC()
    {
        var (guild, member, _) = CreateGuild(500_000, 1_000_000, 0);
        var sundayEvening = new DateTime(2026, 9, 27, 19, 0, 0, DateTimeKind.Utc);

        guild.TryPayWithAllowance(
                 member,
                 GuildAllowanceKind.Training,
                 500_000,
                 500_000,
                 out _,
                 sundayEvening)
             .Should()
             .Be(GuildAllowancePayResult.Paid);

        guild.GetAllowanceStatus(member.Name, GuildAllowanceKind.Training, sundayEvening.AddMinutes(59))
             .Left
             .Should()
             .Be(0);

        guild.GetAllowanceStatus(member.Name, GuildAllowanceKind.Training, sundayEvening.AddHours(1))
             .Left
             .Should()
             .Be(500_000);
    }

    [Test]
    public void SetRankAllowance_rejects_a_negative_amount()
    {
        var guild = MockGuild.Create();

        var act = () => guild.SetRankAllowance(MEMBER_TIER, GuildAllowanceKind.Training, -1);

        act.Should()
           .Throw<ArgumentOutOfRangeException>();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildAllowancePaymentTests/*" --no-ansi`
Expected: build error, `SetRankAllowance` / `GuildAllowanceQuote` not found.

- [ ] **Step 3: Write the result types**

Create `SRV/Chaos/Collections/GuildAllowanceStatus.cs`:

```csharp
namespace Chaos.Collections;

/// <summary>
///     One member's allowance of one kind for the current week
/// </summary>
/// <param name="Allowance">
///     The weekly amount for the member's rank. 0 means the allowance is off
/// </param>
/// <param name="Spent">
///     Gold already spent from it this week
/// </param>
public readonly record struct GuildAllowanceStatus(int Allowance, int Spent)
{
    /// <summary>
    ///     Gold still available this week
    /// </summary>
    public int Left => Math.Max(0, Allowance - Spent);
}
```

Create `SRV/Chaos/Collections/GuildAllowanceQuote.cs`:

```csharp
namespace Chaos.Collections;

/// <summary>
///     How a price splits between the guild bank and the member paying it
/// </summary>
/// <param name="Cost">
///     The full price
/// </param>
/// <param name="AllowanceLeft">
///     The member's allowance left before this purchase
/// </param>
/// <param name="GuildShare">
///     The part the guild bank pays
/// </param>
/// <param name="MemberShare">
///     The part the member pays
/// </param>
/// <param name="LimitedByBank">
///     True when the guild bank's gold, not the allowance, capped the guild's share
/// </param>
public readonly record struct GuildAllowanceQuote(
    int Cost,
    int AllowanceLeft,
    int GuildShare,
    int MemberShare,
    bool LimitedByBank)
{
    /// <summary>
    ///     The member's allowance left after this purchase
    /// </summary>
    public int AllowanceLeftAfter => AllowanceLeft - GuildShare;

    /// <summary>
    ///     Whether the guild would pay any of the price
    /// </summary>
    public bool CanUseGuildFunds => GuildShare > 0;

    /// <summary>
    ///     A quote where the member pays everything
    /// </summary>
    public static GuildAllowanceQuote None(int cost) => new(cost, 0, 0, cost, false);
}
```

Create `SRV/Chaos/Collections/GuildAllowancePayResult.cs`:

```csharp
namespace Chaos.Collections;

/// <summary>
///     The outcome of <see cref="Guild.TryPayWithAllowance" />
/// </summary>
public enum GuildAllowancePayResult
{
    /// <summary>
    ///     Both shares were taken and the guild's share recorded
    /// </summary>
    Paid,

    /// <summary>
    ///     The payer holds no rank in the guild. Nothing was taken
    /// </summary>
    NotInGuild,

    /// <summary>
    ///     The guild's share is no longer the one the member agreed to. Nothing was taken
    /// </summary>
    QuoteChanged,

    /// <summary>
    ///     The member can't pay their share. Nothing was taken
    /// </summary>
    MemberCannotAfford
}
```

- [ ] **Step 4: Add the allowances to `GuildRank`**

In `SRV/Chaos/Collections/GuildRank.cs`, insert these properties after the `IsOfficerRank` property (Serena `insert_after_symbol`, name path `GuildRank/IsOfficerRank`):

```csharp

    /// <summary>
    ///     The most gold each member of this rank may spend from the guild bank on repairs each week. 0 means none
    /// </summary>
    public int RepairAllowance { get; private set; }

    /// <summary>
    ///     The most gold each member of this rank may spend from the guild bank on training each week. 0 means none
    /// </summary>
    public int TrainingAllowance { get; private set; }
```

Insert these methods after `RemoveMember` (name path `GuildRank/RemoveMember`):

```csharp

    /// <summary>
    ///     The weekly allowance of the given kind
    /// </summary>
    public int GetAllowance(GuildAllowanceKind kind)
        => kind switch
        {
            GuildAllowanceKind.Training => TrainingAllowance,
            GuildAllowanceKind.Repairs  => RepairAllowance,
            _                           => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    /// <summary>
    ///     Sets the weekly allowance of the given kind. 0 turns it off
    /// </summary>
    public void SetAllowance(GuildAllowanceKind kind, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        switch (kind)
        {
            case GuildAllowanceKind.Training:
                TrainingAllowance = amount;

                break;
            case GuildAllowanceKind.Repairs:
                RepairAllowance = amount;

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
```

`DeepClone` copies fields, so `GetRanks()` and `RankOf()` clones carry the allowances with no further change.

- [ ] **Step 5: Add the ledger and payment methods to `Guild`**

In `SRV/Chaos/Collections/Guild.cs`:

1. Add this field directly below `private readonly Lock Sync;` (use `replace_content`, literal mode, needle `    private readonly Lock Sync;`):

```csharp
    private readonly Lock Sync;
    private readonly GuildAllowanceLedger AllowanceLedger = new();
```

2. Insert these members after `SetTaxRate` (Serena `insert_after_symbol`, name path `Guild/SetTaxRate`). Add nothing anywhere else in this file, and no `using` lines. Every type here is in `Chaos.Collections` or already imported.

```csharp

    /// <summary>
    ///     A copy of this week's allowance spending, for saving and for the leader's report
    /// </summary>
    public GuildAllowanceLedgerSnapshot GetAllowanceLedger(DateTime? utcNow = null)
    {
        using var @lock = Sync.EnterScope();

        return AllowanceLedger.Snapshot(utcNow ?? DateTime.UtcNow);
    }

    /// <summary>
    ///     One member's allowance of one kind, and how much of it they've used this week
    /// </summary>
    /// <remarks>
    ///     A name that holds no rank has an allowance of 0
    /// </remarks>
    public GuildAllowanceStatus GetAllowanceStatus(string memberName, GuildAllowanceKind kind, DateTime? utcNow = null)
    {
        using var @lock = Sync.EnterScope();

        var rank = UnsafeRankof(memberName);

        var spent = AllowanceLedger.GetSpending(memberName, utcNow ?? DateTime.UtcNow)
                                   .Get(kind);

        return new GuildAllowanceStatus(rank?.GetAllowance(kind) ?? 0, spent);
    }

    /// <summary>
    ///     How <paramref name="cost" /> would split between the guild bank and <paramref name="memberName" />
    /// </summary>
    public GuildAllowanceQuote QuoteAllowance(
        string memberName,
        GuildAllowanceKind kind,
        int cost,
        DateTime? utcNow = null)
    {
        using var @lock = Sync.EnterScope();

        return UnsafeQuoteAllowance(memberName, kind, cost, utcNow ?? DateTime.UtcNow);
    }

    /// <summary>
    ///     Loads saved allowance spending. Spending from an earlier week is dropped on the next read
    /// </summary>
    public void RestoreAllowanceLedger(DateTime weekStart, IEnumerable<KeyValuePair<string, GuildAllowanceSpending>> spending)
    {
        using var @lock = Sync.EnterScope();

        AllowanceLedger.Restore(weekStart, spending);
    }

    /// <summary>
    ///     Sets a rank's weekly allowance of one kind. 0 turns it off
    /// </summary>
    public void SetRankAllowance(int tier, GuildAllowanceKind kind, int amount)
    {
        using var @lock = Sync.EnterScope();

        if (!UnsafeTryGetRank(tier, out var rank))
            throw new InvalidOperationException($"Attempted to set an allowance on rank tier {tier}, which does not exist.");

        rank.SetAllowance(kind, amount);
    }

    /// <summary>
    ///     Takes the guild's share of <paramref name="cost" /> from the guild bank and the rest from
    ///     <paramref name="payer" />
    /// </summary>
    /// <param name="payer">
    ///     The member paying
    /// </param>
    /// <param name="kind">
    ///     Which allowance pays the guild's share
    /// </param>
    /// <param name="cost">
    ///     The full price
    /// </param>
    /// <param name="expectedGuildShare">
    ///     The guild share the member agreed to. If the share is different now, nothing is taken
    /// </param>
    /// <param name="quote">
    ///     The split worked out now
    /// </param>
    /// <param name="utcNow">
    ///     The current time; tests pass a fixed one
    /// </param>
    /// <remarks>
    ///     Either both shares are taken and the guild's share recorded, or nothing changes. The guild's lock is held
    ///     throughout, so two members paying at once can't overdraw an allowance
    /// </remarks>
    public GuildAllowancePayResult TryPayWithAllowance(
        Aisling payer,
        GuildAllowanceKind kind,
        int cost,
        int expectedGuildShare,
        out GuildAllowanceQuote quote,
        DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(payer);

        var now = utcNow ?? DateTime.UtcNow;

        using var @lock = Sync.EnterScope();

        if (UnsafeRankof(payer.Name) is null)
        {
            quote = GuildAllowanceQuote.None(Math.Max(cost, 0));

            return GuildAllowancePayResult.NotInGuild;
        }

        quote = UnsafeQuoteAllowance(payer.Name, kind, cost, now);

        if (!quote.CanUseGuildFunds || (quote.GuildShare != expectedGuildShare))
            return GuildAllowancePayResult.QuoteChanged;

        if (payer.Gold < quote.MemberShare)
            return GuildAllowancePayResult.MemberCannotAfford;

        //other bank withdrawals don't take the guild's lock, so the bank can still come up short here
        if (!Bank.RemoveGold((uint)quote.GuildShare))
            return GuildAllowancePayResult.QuoteChanged;

        if (!payer.TryTakeGold(quote.MemberShare))
        {
            Bank.AddGold((uint)quote.GuildShare);

            return GuildAllowancePayResult.MemberCannotAfford;
        }

        AllowanceLedger.AddSpending(payer.Name, kind, quote.GuildShare, now);

        return GuildAllowancePayResult.Paid;
    }

    private GuildAllowanceQuote UnsafeQuoteAllowance(
        string memberName,
        GuildAllowanceKind kind,
        int cost,
        DateTime utcNow)
    {
        var rank = UnsafeRankof(memberName);

        if ((rank is null) || (cost <= 0))
            return GuildAllowanceQuote.None(Math.Max(cost, 0));

        var spent = AllowanceLedger.GetSpending(memberName, utcNow)
                                   .Get(kind);

        var allowanceLeft = Math.Max(0, rank.GetAllowance(kind) - spent);
        var bankGold = (int)Math.Min(Bank.Gold, (uint)int.MaxValue);
        var guildShare = Math.Min(cost, Math.Min(allowanceLeft, bankGold));

        return new GuildAllowanceQuote(
            cost,
            allowanceLeft,
            guildShare,
            cost - guildShare,
            (guildShare < cost) && (bankGold < allowanceLeft));
    }
```

`UnsafeRankof` and `UnsafeTryGetRank(int, out GuildRank)` already exist in `Guild`. `System.Threading.Lock` is re-entrant, so calling `UnsafeRankof` (which also enters the lock) under the lock is fine, as elsewhere in this class.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/*/*" --no-ansi`
Expected: all ledger and payment tests pass.

Also run `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests/GuildTests/*" --no-ansi`. Expected: all pass (no regressions).

```json:metadata
{"files": ["Chaos/Collections/GuildAllowanceStatus.cs", "Chaos/Collections/GuildAllowanceQuote.cs", "Chaos/Collections/GuildAllowancePayResult.cs", "Chaos/Collections/GuildRank.cs", "Chaos/Collections/Guild.cs", "Tests/Chaos.Tests/GuildAllowances/GuildAllowancePaymentTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildAllowances/*/*\" --no-ansi", "acceptanceCriteria": ["guild share is min(cost, allowance left, bank gold); LimitedByBank only when the bank capped it", "no guild share when allowance off, cost 0, or not a member", "successful payment takes both shares and records the guild share", "member-can't-afford and changed-quote payments take nothing", "promotion and leave/rejoin keep the week's spending", "spending resets at Sunday 20:00 UTC"], "modelTier": "standard"}
```

---

### Task 3: Save and load allowances

**Goal:** Rank allowances and the weekly ledger survive a server restart, and guild files saved before this change still load with every allowance Off.

**Files:**
- Create: `SRV/Chaos.Schemas/Guilds/GuildAllowanceSpendingSchema.cs`
- Modify: `SRV/Chaos.Schemas/Guilds/GuildSchema.cs`
- Modify: `SRV/Chaos.Schemas/Guilds/GuildRankSchema.cs`
- Modify: `SRV/Chaos/Services/MapperProfiles/GuildMapperProfile.cs`
- Test: `SRV/Tests/Chaos.Tests/GuildAllowances/GuildAllowanceMappingTests.cs`

**Acceptance Criteria:**
- [ ] A rank's two allowances survive rank → schema → rank
- [ ] A rank schema without allowance fields maps to allowances of 0
- [ ] The guild's week start and spending survive guild → schema → guild
- [ ] Guild JSON without the new fields deserializes, and the mapped guild has an empty ledger

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildAllowanceMappingTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildAllowances/GuildAllowanceMappingTests.cs`:

```csharp
#region
using System.Text.Json;
using Chaos.Collections;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Schemas.Guilds;
using Chaos.Services.MapperProfiles;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildAllowances;

public sealed class GuildAllowanceMappingTests
{
    private static GuildMapperProfile CreateProfile() => new(MockChannelService.Create(), new ClientRegistry<IChaosWorldClient>());

    [Test]
    public void Rank_allowances_survive_a_round_trip()
    {
        var profile = CreateProfile();
        var rank = new GuildRank("Member", 2, ["Iglis"]);
        rank.SetAllowance(GuildAllowanceKind.Training, 500_000);
        rank.SetAllowance(GuildAllowanceKind.Repairs, 100_000);

        var loaded = profile.Map(profile.Map(rank));

        loaded.TrainingAllowance
              .Should()
              .Be(500_000);

        loaded.RepairAllowance
              .Should()
              .Be(100_000);
    }

    [Test]
    public void A_rank_saved_before_allowances_existed_has_them_off()
    {
        var loaded = CreateProfile()
            .Map(
                new GuildRankSchema
                {
                    RankName = "Member",
                    Tier = 2
                });

        loaded.TrainingAllowance
              .Should()
              .Be(0);

        loaded.RepairAllowance
              .Should()
              .Be(0);
    }

    [Test]
    public void Guild_allowance_spending_survives_a_round_trip()
    {
        var profile = CreateProfile();
        var guild = MockGuild.Create("Sradagan");
        var weekStart = GuildAllowanceLedger.GetWeekStart(DateTime.UtcNow);

        guild.RestoreAllowanceLedger(weekStart, [KeyValuePair.Create("Iglis", new GuildAllowanceSpending(150_000, 7_000))]);

        var loaded = profile.Map(profile.Map(guild));
        var ledger = loaded.GetAllowanceLedger();

        ledger.WeekStart
              .Should()
              .Be(weekStart);

        ledger.Spending["Iglis"]
              .Should()
              .Be(new GuildAllowanceSpending(150_000, 7_000));
    }

    [Test]
    public void A_guild_file_saved_before_allowances_existed_still_loads()
    {
        const string JSON = """{"Guid":"5f2b","Name":"Sradagan","TaxRatePercent":5}""";

        var schema = JsonSerializer.Deserialize<GuildSchema>(JSON)!;

        schema.AllowanceSpending
              .Should()
              .BeEmpty();

        CreateProfile()
            .Map(schema)
            .GetAllowanceLedger()
            .Spending
            .Should()
            .BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildAllowanceMappingTests/*" --no-ansi`
Expected: build error, `AllowanceSpending` not found on `GuildSchema`.

- [ ] **Step 3: Add the schema fields**

Create `SRV/Chaos.Schemas/Guilds/GuildAllowanceSpendingSchema.cs`:

```csharp
namespace Chaos.Schemas.Guilds;

/// <summary>
///     Represents the serializable schema of the gold one guild member has spent from the guild's allowances in a week
/// </summary>
public sealed record GuildAllowanceSpendingSchema
{
    /// <summary>
    ///     Gold spent on repairs
    /// </summary>
    public int Repairs { get; set; }

    /// <summary>
    ///     Gold spent on training
    /// </summary>
    public int Training { get; set; }
}
```

In `SRV/Chaos.Schemas/Guilds/GuildSchema.cs`, add these two properties at the end of the record, after `TaxRatePercent`:

```csharp

    /// <summary>
    ///     Gold each member has spent from the guild's allowances in the week starting at <see cref="AllowanceWeekStart" />,
    ///     by member name
    /// </summary>
    public Dictionary<string, GuildAllowanceSpendingSchema> AllowanceSpending { get; set; } = [];

    /// <summary>
    ///     The start (UTC) of the week that <see cref="AllowanceSpending" /> covers
    /// </summary>
    public DateTime AllowanceWeekStart { get; set; }
```

In `SRV/Chaos.Schemas/Guilds/GuildRankSchema.cs`, add these two properties at the end of the record, after `Tier`:

```csharp

    /// <summary>
    ///     The most gold each member of this rank may spend from the guild bank on repairs each week. 0 means none
    /// </summary>
    public int RepairAllowance { get; set; }

    /// <summary>
    ///     The most gold each member of this rank may spend from the guild bank on training each week. 0 means none
    /// </summary>
    public int TrainingAllowance { get; set; }
```

`SerializationContext` already lists `GuildSchema` and `GuildRankSchema`, and the source generator picks up the new nested type on its own. Don't edit `SerializationContext.cs`. The guild cloak branch edits it.

- [ ] **Step 4: Map the new fields**

Replace the four `Map` methods in `SRV/Chaos/Services/MapperProfiles/GuildMapperProfile.cs` so the class body reads (the fields and constructor stay as they are):

```csharp
    /// <inheritdoc />
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

        return guild;
    }

    /// <inheritdoc />
    public GuildSchema Map(Guild obj)
    {
        var ledger = obj.GetAllowanceLedger();

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
                StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <inheritdoc />
    public GuildRank Map(GuildRankSchema obj)
    {
        var rank = new GuildRank(obj.RankName, obj.Tier, obj.Members);

        //a hand-edited file could hold a negative number; treat it as off rather than refusing to load the guild
        rank.SetAllowance(GuildAllowanceKind.Training, Math.Max(0, obj.TrainingAllowance));
        rank.SetAllowance(GuildAllowanceKind.Repairs, Math.Max(0, obj.RepairAllowance));

        return rank;
    }

    /// <inheritdoc />
    public GuildRankSchema Map(GuildRank obj)
        => new()
        {
            RankName = obj.Name,
            Tier = obj.Tier,
            Members = obj.GetMemberNames()
                         .ToList(),
            TrainingAllowance = obj.TrainingAllowance,
            RepairAllowance = obj.RepairAllowance
        };
```

The four methods are overloads, so Serena's name paths are `GuildMapperProfile/Map[0]` to `Map[3]`. `replace_content` in regex mode on the region from the first `/// <inheritdoc />` to the class's closing brace is simpler.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/*/*" --no-ansi`
Expected: all pass.

```json:metadata
{"files": ["Chaos.Schemas/Guilds/GuildAllowanceSpendingSchema.cs", "Chaos.Schemas/Guilds/GuildSchema.cs", "Chaos.Schemas/Guilds/GuildRankSchema.cs", "Chaos/Services/MapperProfiles/GuildMapperProfile.cs", "Tests/Chaos.Tests/GuildAllowances/GuildAllowanceMappingTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildAllowances/GuildAllowanceMappingTests/*\" --no-ansi", "acceptanceCriteria": ["rank allowances survive a round trip", "rank schema without allowances maps to 0", "guild week start and spending survive a round trip", "old guild JSON loads with an empty ledger"], "modelTier": "mechanical"}
```

---

### Task 4: Guild funds helper and the allowance log type

**Goal:** One helper that trainers and smiths call to quote a split, describe it, and pay it. A successful payment also writes an `AllowanceSpend` line to the guild bank log and the server log.

**Files:**
- Modify: `SRV/Chaos/Models/World/GuildHouseState.cs` (one enum value, at the end)
- Create: `SRV/Chaos/Utilities/GuildFundsQuoteContext.cs`
- Create: `SRV/Chaos/Utilities/GuildFundsHelper.cs`
- Test: `SRV/Tests/Chaos.Tests/GuildAllowances/GuildFundsHelperTests.cs`

**Acceptance Criteria:**
- [ ] `TryQuote` is false for a player with no guild
- [ ] `DescribeSplit` gives the three exact messages (full cover, allowance runs out, bank runs out)
- [ ] `TryPay` takes both shares and logs one `AllowanceSpend` line with the description, the member and the guild share
- [ ] `TryPay` takes nothing and returns "The cost or your guild's funds changed. Please try again." when there's no agreed quote or the cost changed
- [ ] `TryPay` returns "You need <share> gold for your share." when the member can't afford it

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildFundsHelperTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildAllowances/GuildFundsHelperTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using Chaos.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
#endregion

namespace Chaos.Tests.GuildAllowances;

public sealed class GuildFundsHelperTests
{
    private const string CHANGED = "The cost or your guild's funds changed. Please try again.";

    private static (Guild Guild, Aisling Member) CreateGuild(int repairAllowance, uint bankGold, int memberGold)
    {
        var guild = MockGuild.Create();
        var member = MockAisling.Create(name: "Iglis");

        guild.AddMember(member, MockAisling.Create(name: "Stahli"));
        guild.SetRankAllowance(3, GuildAllowanceKind.Repairs, repairAllowance);
        guild.Bank.AddGold(bankGold);
        member.Gold = memberGold;

        return (guild, member);
    }

    private static (IStorage<GuildHouseState> Storage, GuildHouseState State) CreateHouseStorage()
    {
        var state = new GuildHouseState();
        var storage = new Mock<IStorage<GuildHouseState>>();

        storage.SetupGet(s => s.Value)
               .Returns(state);

        return (storage.Object, state);
    }

    private static bool Pay(Aisling member, Dialog dialog, int cost, IStorage<GuildHouseState> storage, out string? failureMessage)
        => GuildFundsHelper.TryPay(
            member,
            dialog,
            GuildAllowanceKind.Repairs,
            cost,
            "Repairs: all items",
            storage,
            new Mock<ILogger>().Object,
            out failureMessage);

    [Test]
    public void TryQuote_is_false_for_a_player_without_a_guild()
    {
        var player = MockAisling.Create();

        GuildFundsHelper.TryQuote(player, GuildAllowanceKind.Repairs, 1_000, out var quote)
                        .Should()
                        .BeFalse();

        quote.MemberShare
             .Should()
             .Be(1_000);
    }

    [Test]
    public void DescribeSplit_when_the_guild_covers_everything()
        => GuildFundsHelper.DescribeSplit(new GuildAllowanceQuote(150_000, 500_000, 150_000, 0, false), GuildAllowanceKind.Training)
                           .Should()
                           .Be("Your guild will cover all 150,000 gold. You will have 350,000 of your training allowance left this week.");

    [Test]
    public void DescribeSplit_when_the_allowance_runs_out()
        => GuildFundsHelper.DescribeSplit(new GuildAllowanceQuote(500_000, 200_000, 200_000, 300_000, false), GuildAllowanceKind.Training)
                           .Should()
                           .Be(
                               "Your guild will cover 200,000 of the 500,000 gold. You will pay the other 300,000. That uses the rest of your training allowance this week.");

    [Test]
    public void DescribeSplit_when_the_bank_runs_out()
        => GuildFundsHelper.DescribeSplit(new GuildAllowanceQuote(500_000, 500_000, 50_000, 450_000, true), GuildAllowanceKind.Repairs)
                           .Should()
                           .Be(
                               "The guild bank only has 50,000 gold. Your guild will cover 50,000 of the 500,000 and you will pay the other 450,000.");

    [Test]
    public void TryPay_takes_both_shares_and_logs_the_guild_share()
    {
        var (guild, member) = CreateGuild(2_000, 1_000_000, 5_000);
        var (storage, state) = CreateHouseStorage();
        var dialog = MockDialog.Create("generic_repairallitemguildaccepted");
        dialog.Context = new GuildFundsQuoteContext(3_000, 2_000);

        Pay(member, dialog, 3_000, storage, out var failureMessage)
            .Should()
            .BeTrue();

        failureMessage.Should()
                      .BeNull();

        member.Gold
              .Should()
              .Be(4_000);

        guild.Bank
             .Gold
             .Should()
             .Be(998_000u);

        state.GetItemTransactions(guild.Name)
             .Should()
             .ContainSingle(log => (log.Action == GuildHouseState.TransactionType.AllowanceSpend)
                                   && (log.ItemName == "Repairs: all items")
                                   && (log.Member == "Iglis")
                                   && (log.Amount == 2_000));
    }

    [Test]
    public void TryPay_refuses_without_an_agreed_quote()
    {
        var (guild, member) = CreateGuild(2_000, 1_000_000, 5_000);
        var (storage, state) = CreateHouseStorage();
        var dialog = MockDialog.Create("generic_repairallitemguildaccepted");

        Pay(member, dialog, 3_000, storage, out var failureMessage)
            .Should()
            .BeFalse();

        failureMessage.Should()
                      .Be(CHANGED);

        member.Gold
              .Should()
              .Be(5_000);

        guild.Bank
             .Gold
             .Should()
             .Be(1_000_000u);

        state.GetItemTransactions(guild.Name)
             .Should()
             .BeEmpty();
    }

    [Test]
    public void TryPay_refuses_when_the_cost_changed_since_the_quote()
    {
        var (guild, member) = CreateGuild(2_000, 1_000_000, 5_000);
        var (storage, _) = CreateHouseStorage();
        var dialog = MockDialog.Create("generic_repairallitemguildaccepted");
        dialog.Context = new GuildFundsQuoteContext(2_500, 2_000);

        Pay(member, dialog, 3_000, storage, out var failureMessage)
            .Should()
            .BeFalse();

        failureMessage.Should()
                      .Be(CHANGED);

        guild.Bank
             .Gold
             .Should()
             .Be(1_000_000u);
    }

    [Test]
    public void TryPay_names_the_share_the_member_cannot_afford()
    {
        var (guild, member) = CreateGuild(2_000, 1_000_000, 500);
        var (storage, _) = CreateHouseStorage();
        var dialog = MockDialog.Create("generic_repairallitemguildaccepted");
        dialog.Context = new GuildFundsQuoteContext(3_000, 2_000);

        Pay(member, dialog, 3_000, storage, out var failureMessage)
            .Should()
            .BeFalse();

        failureMessage.Should()
                      .Be("You need 1,000 gold for your share.");

        guild.Bank
             .Gold
             .Should()
             .Be(1_000_000u);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildFundsHelperTests/*" --no-ansi`
Expected: build error, `GuildFundsHelper` not found.

- [ ] **Step 3: Add the log type**

In `SRV/Chaos/Models/World/GuildHouseState.cs`, change the `TransactionType` enum to (the new value goes last, so saved logs keep their meaning):

```csharp
    public enum TransactionType
    {
        Deposit,
        Withdrawal,
        GoldDeposit,
        GoldWithdrawal,
        AllowanceSpend
    }
```

Change nothing else in this file. The guild cloak branch edits its property switches.

- [ ] **Step 4: Write the context record and the helper**

Create `SRV/Chaos/Utilities/GuildFundsQuoteContext.cs`:

```csharp
namespace Chaos.Utilities;

/// <summary>
///     The split a guild-funds prompt showed the member. It rides in <see cref="Chaos.Models.Menu.Dialog.Context" /> from
///     the prompt to the accepted dialog, which pays only if the split is still the same
/// </summary>
/// <param name="Cost">
///     The full price shown
/// </param>
/// <param name="GuildShare">
///     The guild's share shown
/// </param>
public sealed record GuildFundsQuoteContext(int Cost, int GuildShare);
```

Create `SRV/Chaos/Utilities/GuildFundsHelper.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.NLog.Logging.Definitions;
using Chaos.NLog.Logging.Extensions;
using Chaos.Storage.Abstractions;
#endregion

namespace Chaos.Utilities;

/// <summary>
///     Lets trainers and smiths take part of a price from the guild bank, within the member's weekly allowance
/// </summary>
public static class GuildFundsHelper
{
    /// <summary>
    ///     The text of the option trainers and smiths add when guild funds can pay
    /// </summary>
    public const string OPTION_TEXT = "Use guild funds";

    private const string CHANGED_MESSAGE = "The cost or your guild's funds changed. Please try again.";
    private const string NOT_IN_GUILD_MESSAGE = "You are no longer in a guild.";

    /// <summary>
    ///     The split to show the member, before they agree to it
    /// </summary>
    public static string DescribeSplit(GuildAllowanceQuote quote, GuildAllowanceKind kind)
    {
        var allowanceName = kind == GuildAllowanceKind.Training ? "training" : "repair";

        if (quote.MemberShare == 0)
            return $"Your guild will cover all {quote.Cost:N0} gold. You will have {quote.AllowanceLeftAfter:N0} of your {
                allowanceName} allowance left this week.";

        if (quote.LimitedByBank)
            return $"The guild bank only has {quote.GuildShare:N0} gold. Your guild will cover {quote.GuildShare:N0} of the {
                quote.Cost:N0} and you will pay the other {quote.MemberShare:N0}.";

        return $"Your guild will cover {quote.GuildShare:N0} of the {quote.Cost:N0} gold. You will pay the other {
            quote.MemberShare:N0}. That uses the rest of your {allowanceName} allowance this week.";
    }

    /// <summary>
    ///     Pays <paramref name="cost" /> with the split the member agreed to in <paramref name="dialog" />'s context
    /// </summary>
    /// <param name="source">
    ///     The member paying
    /// </param>
    /// <param name="dialog">
    ///     The accepted dialog. Its <see cref="Dialog.Context" /> must hold the <see cref="GuildFundsQuoteContext" /> the
    ///     prompt showed
    /// </param>
    /// <param name="kind">
    ///     Which allowance pays the guild's share
    /// </param>
    /// <param name="cost">
    ///     The full price, worked out again now
    /// </param>
    /// <param name="description">
    ///     What the gold paid for, for the guild bank log, such as "Training: Ambush"
    /// </param>
    /// <param name="guildHouseStateStorage">
    ///     Where the guild bank log lives
    /// </param>
    /// <param name="logger">
    ///     The calling script's logger
    /// </param>
    /// <param name="failureMessage">
    ///     What to tell the member when nothing was paid
    /// </param>
    /// <returns>
    ///     <c>true</c> if both shares were taken; otherwise <c>false</c>, and nothing was taken
    /// </returns>
    public static bool TryPay(
        Aisling source,
        Dialog dialog,
        GuildAllowanceKind kind,
        int cost,
        string description,
        IStorage<GuildHouseState> guildHouseStateStorage,
        ILogger logger,
        [NotNullWhen(false)] out string? failureMessage)
    {
        var guild = source.Guild;

        if (guild is null)
        {
            failureMessage = NOT_IN_GUILD_MESSAGE;

            return false;
        }

        if (dialog.Context is not GuildFundsQuoteContext agreed || (agreed.Cost != cost))
        {
            failureMessage = CHANGED_MESSAGE;

            return false;
        }

        var bankGoldBefore = guild.Bank.Gold;
        var result = guild.TryPayWithAllowance(source, kind, cost, agreed.GuildShare, out var quote);

        switch (result)
        {
            case GuildAllowancePayResult.NotInGuild:
                failureMessage = NOT_IN_GUILD_MESSAGE;

                return false;
            case GuildAllowancePayResult.QuoteChanged:
                failureMessage = CHANGED_MESSAGE;

                return false;
            case GuildAllowancePayResult.MemberCannotAfford:
                failureMessage = $"You need {quote.MemberShare:N0} gold for your share.";

                return false;
        }

        var guildHouseState = guildHouseStateStorage.Value;
        guildHouseState.SetStorage(guildHouseStateStorage);

        guildHouseState.LogItemTransaction(
            guild.Name,
            source.Name,
            description,
            GuildHouseState.TransactionType.AllowanceSpend,
            quote.GuildShare);

        logger.WithTopics(Topics.Entities.Guild, Topics.Entities.Gold, Topics.Actions.Withdraw)
              .WithProperty(guild)
              .WithProperty(source)
              .LogInformation(
                  "Aisling {@AislingName} used guild {@GuildName}'s {AllowanceKind} allowance for {Description}: cost {Cost}, guild paid {GuildShare}, member paid {MemberShare}, guild bank {BankGoldBefore} -> {BankGoldAfter}",
                  source.Name,
                  guild.Name,
                  kind,
                  description,
                  quote.Cost,
                  quote.GuildShare,
                  quote.MemberShare,
                  bankGoldBefore,
                  guild.Bank.Gold);

        source.SendOrangeBarMessage(
            quote.MemberShare > 0
                ? $"Your guild paid {quote.GuildShare:N0} gold and you paid {quote.MemberShare:N0}."
                : $"Your guild paid all {quote.GuildShare:N0} gold.");

        failureMessage = null;

        return true;
    }

    /// <summary>
    ///     Works out how much of <paramref name="cost" /> the member's guild would pay
    /// </summary>
    /// <returns>
    ///     <c>true</c> if the guild would pay some of it, which is when the "Use guild funds" option should appear
    /// </returns>
    public static bool TryQuote(Aisling source, GuildAllowanceKind kind, int cost, out GuildAllowanceQuote quote)
    {
        quote = GuildAllowanceQuote.None(Math.Max(cost, 0));

        if ((source.Guild is null) || (cost <= 0))
            return false;

        quote = source.Guild.QuoteAllowance(source.Name, kind, cost);

        return quote.CanUseGuildFunds;
    }
}
```

`ILogger` comes from the Chaos project's implicit usings (it is a Web SDK project), as in `GuildBankManagementScript`. If the build says `ILogger` isn't found, add `using Microsoft.Extensions.Logging;` to the `using` block. The multi-line interpolation holes (`{` then a line break) are valid C# 11+. If the formatter or analyzer objects, join each message onto one line.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/*/*" --no-ansi`
Expected: all pass. Also run `--treenode-filter "/*/Chaos.Tests.GuildHall/*/*"`. Expected: all pass. The new enum value doesn't change the other log types.

```json:metadata
{"files": ["Chaos/Models/World/GuildHouseState.cs", "Chaos/Utilities/GuildFundsQuoteContext.cs", "Chaos/Utilities/GuildFundsHelper.cs", "Tests/Chaos.Tests/GuildAllowances/GuildFundsHelperTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildAllowances/GuildFundsHelperTests/*\" --no-ansi", "acceptanceCriteria": ["TryQuote false without a guild", "DescribeSplit gives the three exact messages", "TryPay takes both shares and logs one AllowanceSpend line", "TryPay takes nothing and returns the changed message without a matching quote", "TryPay names the unaffordable member share"], "modelTier": "mechanical"}
```

---

### Task 5: Guild funds at skill and spell trainers

**Goal:** When guild funds can pay, a trainer's requirements menu shows "Use guild funds". That option opens a prompt with the split, and "Yes" learns the skill or spell with the split payment.

**Files:**
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSpellScript.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSkillScript.cs`
- Modify: `SRV/Tests/Chaos.Tests/Trainers/LearnSpellScriptTests.cs`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSpell/generic_learnspell_guildfunds.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSpell/generic_learnspell_guildaccepted.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSkill/generic_learnskill_guildfunds.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSkill/generic_learnskill_guildaccepted.json`

**Acceptance Criteria:**
- [ ] The spell requirements menu reads Yes / Use guild funds / No when the rank has training allowance left, and Yes / No when the allowance is off
- [ ] The spell guild-funds prompt stores `GuildFundsQuoteContext(cost, guild share)` in its context
- [ ] Learning on the guild-funded path takes the member share from the member and the guild share from the bank
- [ ] On the guild-funded path, a member who can't pay their share loses nothing
- [ ] Paying yourself still takes the full price and nothing from the bank
- [ ] `LearnSkillScript` has the same option, prompt and payment (checked by build and in game)
- [ ] The four new dialog files are valid JSON

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Trainers/*/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing spell tests**

In `SRV/Tests/Chaos.Tests/Trainers/LearnSpellScriptTests.cs`:

1. Add these `using` lines to the `#region` block: `using Chaos.Collections;`, `using Chaos.Common.Collections;`, `using Chaos.Models.Data;`, `using Chaos.Models.Panel;`, `using Chaos.Models.World;`, `using Chaos.Utilities;`.
2. Replace `CreateScript` with:

```csharp
    private static LearnSpellScript CreateScript(Dialog dialog, IStorage<GuildHouseState>? guildHouseStateStorage = null)
        => new(
            dialog,
            new Mock<IItemFactory>().Object,
            new Mock<ISkillFactory>().Object,
            new Mock<ISpellFactory>().Object,
            new Mock<ISimpleCache>().Object,
            new Mock<ILogger<LearnSpellScript>>().Object,
            guildHouseStateStorage ?? new Mock<IStorage<GuildHouseState>>().Object);
```

3. Add these helpers and tests inside the class, after the existing Fury test:

```csharp
    private static Spell CreatePricedSpell(int requiredGold)
        => MockSpell.Create(
            "Test Blessing",
            templateSetup: t => t with
            {
                TemplateKey = "testblessing",
                Class = BaseClass.Priest,
                Level = 1,
                Description = "A test blessing.",
                LearningRequirements = new LearningRequirements
                {
                    ItemRequirements = [],
                    PrerequisiteSkills = [],
                    PrerequisiteSpells = [],
                    RequiredGold = requiredGold,
                    RequiredStats = null
                }
            });

    private static Dialog CreateSpellDialog(string templateKey, Spell spell)
    {
        var trainer = new Mock<ISpellTeacherSource>();

        trainer.SetupGet(t => t.Name)
               .Returns("Trainer");

        trainer.SetupGet(t => t.SpellsToTeach)
               .Returns([spell]);

        var dialog = CreateTrainerDialog(templateKey, trainer.Object);
        dialog.MenuArgs = new ArgumentCollection(new[] { spell.Template.Name });

        return dialog;
    }

    private static (Guild Guild, Aisling Priest) CreateGuildPriest(int trainingAllowance, uint bankGold, int priestGold)
    {
        var guild = MockGuild.Create();

        var priest = MockAisling.Create(
            name: "Iglis",
            setup: a =>
            {
                a.UserStatSheet.SetBaseClass(BaseClass.Priest);
                a.UserStatSheet.SetLevel(10);
            });

        //AddMember puts new members in the lowest rank, tier 3
        guild.AddMember(priest, MockAisling.Create(name: "Stahli"));
        guild.SetRankAllowance(3, GuildAllowanceKind.Training, trainingAllowance);
        guild.Bank.AddGold(bankGold);
        priest.Gold = priestGold;

        return (guild, priest);
    }

    private static IStorage<GuildHouseState> CreateHouseStorage()
    {
        var storage = new Mock<IStorage<GuildHouseState>>();

        storage.SetupGet(s => s.Value)
               .Returns(new GuildHouseState());

        return storage.Object;
    }

    [Test]
    public void Requirements_offer_guild_funds_when_the_rank_has_training_allowance_left()
    {
        var spell = CreatePricedSpell(300_000);
        var (_, priest) = CreateGuildPriest(100_000, 1_000_000, 0);
        var dialog = CreateSpellDialog("generic_learnspell_showrequirements", spell);
        dialog.AddOptions(("Yes", "generic_learnspell_accepted"), ("No", "generic_learnspell_initial"));

        CreateScript(dialog)
            .OnDisplaying(priest);

        dialog.Options
              .Select(option => option.OptionText)
              .Should()
              .Equal("Yes", GuildFundsHelper.OPTION_TEXT, "No");
    }

    [Test]
    public void Requirements_do_not_offer_guild_funds_when_the_allowance_is_off()
    {
        var spell = CreatePricedSpell(300_000);
        var (_, priest) = CreateGuildPriest(0, 1_000_000, 0);
        var dialog = CreateSpellDialog("generic_learnspell_showrequirements", spell);
        dialog.AddOptions(("Yes", "generic_learnspell_accepted"), ("No", "generic_learnspell_initial"));

        CreateScript(dialog)
            .OnDisplaying(priest);

        dialog.Options
              .Select(option => option.OptionText)
              .Should()
              .Equal("Yes", "No");
    }

    [Test]
    public void Guild_funds_prompt_keeps_the_split_it_showed()
    {
        var spell = CreatePricedSpell(300_000);
        var (_, priest) = CreateGuildPriest(100_000, 1_000_000, 0);
        var dialog = CreateSpellDialog("generic_learnspell_guildfunds", spell);

        CreateScript(dialog)
            .OnDisplaying(priest);

        dialog.Context
              .Should()
              .Be(new GuildFundsQuoteContext(300_000, 100_000));
    }

    [Test]
    public void Guild_funded_learning_takes_the_split_from_the_bank_and_the_priest()
    {
        var spell = CreatePricedSpell(300_000);
        var (guild, priest) = CreateGuildPriest(100_000, 1_000_000, 250_000);
        var dialog = CreateSpellDialog("generic_learnspell_guildaccepted", spell);
        dialog.Context = new GuildFundsQuoteContext(300_000, 100_000);

        CreateScript(dialog, CreateHouseStorage())
            .ValidateAndTakeRequirements(priest, dialog, spell)
            .Should()
            .BeTrue();

        priest.Gold
              .Should()
              .Be(50_000);

        guild.Bank
             .Gold
             .Should()
             .Be(900_000u);
    }

    [Test]
    public void Guild_funded_learning_takes_nothing_when_the_priest_cannot_pay_their_share()
    {
        var spell = CreatePricedSpell(300_000);
        var (guild, priest) = CreateGuildPriest(100_000, 1_000_000, 100_000);
        var dialog = CreateSpellDialog("generic_learnspell_guildaccepted", spell);
        dialog.Context = new GuildFundsQuoteContext(300_000, 100_000);

        CreateScript(dialog, CreateHouseStorage())
            .ValidateAndTakeRequirements(priest, dialog, spell)
            .Should()
            .BeFalse();

        priest.Gold
              .Should()
              .Be(100_000);

        guild.Bank
             .Gold
             .Should()
             .Be(1_000_000u);
    }

    [Test]
    public void Paying_yourself_still_takes_the_full_price()
    {
        var spell = CreatePricedSpell(300_000);
        var (guild, priest) = CreateGuildPriest(100_000, 1_000_000, 400_000);
        var dialog = CreateSpellDialog("generic_learnspell_accepted", spell);

        CreateScript(dialog)
            .ValidateAndTakeRequirements(priest, dialog, spell)
            .Should()
            .BeTrue();

        priest.Gold
              .Should()
              .Be(100_000);

        guild.Bank
             .Gold
             .Should()
             .Be(1_000_000u);
    }
```

If one of these tests fails on a check that has nothing to do with gold (level, class or grand master checks in `ValidateAndTakeRequirements`), change the test spell's template or the priest's setup. Don't change the production checks.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Trainers/*/*" --no-ansi`
Expected: build error, because the `LearnSpellScript` constructor has no seventh parameter yet.

- [ ] **Step 3: Change `LearnSpellScript`**

In `SRV/Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSpellScript.cs`:

1. Add `using Chaos.Collections;` to the `using` block. `Chaos.Storage.Abstractions`, `Chaos.Utilities`, `Chaos.Models.World` and `Chaos.Extensions.Common` are already imported.
2. Add the field `private readonly IStorage<GuildHouseState> GuildHouseStateStorage;` next to the other `private readonly` fields at the top of the class.
3. Replace the constructor:

```csharp
    public LearnSpellScript(
        Dialog subject,
        IItemFactory itemFactory,
        ISkillFactory skillFactory,
        ISpellFactory spellFactory,
        ISimpleCache simpleCache,
        ILogger<LearnSpellScript> logger,
        IStorage<GuildHouseState> guildHouseStateStorage)
        : base(subject)
    {
        ItemFactory = itemFactory;
        SkillFactory = skillFactory;
        SpellFactory = spellFactory;
        SimpleCache = simpleCache;
        Logger = logger;
        GuildHouseStateStorage = guildHouseStateStorage;
        SpellTeacherSource = (ISpellTeacherSource)Subject.DialogSource;
    }
```

4. Replace `OnDisplaying`:

```csharp
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "generic_learnspell_initial":
                OnDisplayingInitial(source);

                break;
            case "generic_learnspell_showrequirements":
                OnDisplayingRequirements(source);

                break;
            case "generic_learnspell_guildfunds":
                OnDisplayingGuildFunds(source);

                break;
            case "generic_learnspell_accepted":
            case "generic_learnspell_guildaccepted":
                OnDisplayingAccepted(source);

                break;
        }
    }
```

5. Replace `OnDisplayingRequirements`:

```csharp
    private void OnDisplayingRequirements(Aisling source)
    {
        if (!TryFetchArgs<string>(out var spellName)
            || !TryGetSpell(spellName, source, out var spell)
            || source.SpellBook.Contains(spellName))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        var learningRequirementsStr = spell.Template
                                           .LearningRequirements
                                           ?.BuildRequirementsString(ItemFactory, SkillFactory, SpellFactory)
                                           .ToString();

        Subject.InjectTextParameters(spell.Template.Description ?? string.Empty, learningRequirementsStr ?? string.Empty);

        var requiredGold = spell.Template.LearningRequirements?.RequiredGold ?? 0;

        //between "Yes" and "No"
        if (GuildFundsHelper.TryQuote(source, GuildAllowanceKind.Training, requiredGold, out _))
            Subject.InsertOption(1, GuildFundsHelper.OPTION_TEXT, "generic_learnspell_guildfunds");
    }
```

6. Insert after `OnDisplayingRequirements` (Serena `insert_after_symbol`, name path `LearnSpellScript/OnDisplayingRequirements`):

```csharp

    private void OnDisplayingGuildFunds(Aisling source)
    {
        if (!TryFetchArgs<string>(out var spellName)
            || !TryGetSpell(spellName, source, out var spell)
            || source.SpellBook.Contains(spellName))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        var requiredGold = spell.Template.LearningRequirements?.RequiredGold ?? 0;

        if (!GuildFundsHelper.TryQuote(source, GuildAllowanceKind.Training, requiredGold, out var quote))
        {
            Subject.Reply(source, "Your guild can't pay toward this right now.", "generic_learnspell_initial");

            return;
        }

        //the accepted dialog pays only if the split is still the one shown here
        Subject.Context = new GuildFundsQuoteContext(quote.Cost, quote.GuildShare);
        Subject.InjectTextParameters(spell.Template.Name, GuildFundsHelper.DescribeSplit(quote, GuildAllowanceKind.Training));
    }

    private static bool IsGuildFunded(Dialog dialog) => dialog.Template.TemplateKey.EqualsI("generic_learnspell_guildaccepted");
```

7. At the end of the public `ValidateAndTakeRequirements(Aisling source, Dialog dialog, Spell spellToLearn)`, replace the item-and-gold block. It starts at the first `foreach (var itemRequirement in requirements.ItemRequirements)` and runs to the method's final `return true;`. Use `replace_content` in regex mode with the needle `foreach \(var itemRequirement in requirements\.ItemRequirements\).*?return true;` (the only two `requirements.ItemRequirements` loops in the file are in this block). The replacement:

```csharp
foreach (var itemRequirement in requirements.ItemRequirements)
        {
            var requiredItem = ItemFactory.CreateFaux(itemRequirement.ItemTemplateKey);

            if (!source.Inventory.HasCount(requiredItem.DisplayName, itemRequirement.AmountRequired))
            {
                dialog.Reply(source, "Come back when you have what is required.", "generic_learnspell_initial");

                return false;
            }
        }

        var requiredGold = requirements.RequiredGold ?? 0;
        var guildFunded = IsGuildFunded(dialog);

        if (guildFunded)
        {
            //both shares are taken before any item, so a refused payment costs the member nothing
            if (!GuildFundsHelper.TryPay(
                    source,
                    dialog,
                    GuildAllowanceKind.Training,
                    requiredGold,
                    $"Training: {spellToLearn.Template.Name}",
                    GuildHouseStateStorage,
                    Logger,
                    out var failureMessage))
            {
                dialog.Reply(source, failureMessage, "generic_learnspell_initial");

                return false;
            }
        } else if (source.Gold < requiredGold)
        {
            dialog.Reply(source, "Come back when you are more wealthy.", "generic_learnspell_initial");

            return false;
        }

        foreach (var itemRequirement in requirements.ItemRequirements)
        {
            var requiredItem = ItemFactory.CreateFaux(itemRequirement.ItemTemplateKey);

            source.Inventory.RemoveQuantity(requiredItem.DisplayName, itemRequirement.AmountRequired);
        }

        if (!guildFunded && (requiredGold > 0))
            source.TryTakeGold(requiredGold);

        return true;
```

Read the method first with `find_symbol` (`LearnSpellScript/ValidateAndTakeRequirements`, `include_body=true`). Check that the local holding the requirements is called `requirements` and the spell parameter is `spellToLearn`, and rename in the replacement if not.

- [ ] **Step 4: Run the spell tests to verify they pass**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Trainers/*/*" --no-ansi`
Expected: all pass, including the existing Fury test.

- [ ] **Step 5: Change `LearnSkillScript` the same way**

In `SRV/Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSkillScript.cs`:

1. Add `using Chaos.Collections;`. Add the field `private readonly IStorage<GuildHouseState> GuildHouseStateStorage;` next to the other fields.
2. Replace the constructor:

```csharp
    public LearnSkillScript(
        Dialog subject,
        IItemFactory itemFactory,
        ISkillFactory skillFactory,
        ISpellFactory spellFactory,
        ISimpleCache simpleCache,
        ILogger<LearnSkillScript> logger,
        IStorage<GuildHouseState> guildHouseStateStorage)
        : base(subject)
    {
        ItemFactory = itemFactory;
        SkillFactory = skillFactory;
        SpellFactory = spellFactory;
        SimpleCache = simpleCache;
        Logger = logger;
        GuildHouseStateStorage = guildHouseStateStorage;
        SkillTeacherSource = (ISkillTeacherSource)Subject.DialogSource;
    }
```

3. Replace `OnDisplaying`:

```csharp
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "generic_learnskill_initial":
                OnDisplayingInitial(source);

                break;
            case "generic_learnskill_showrequirements":
                OnDisplayingRequirements(source);

                break;
            case "generic_learnskill_guildfunds":
                OnDisplayingGuildFunds(source);

                break;
            case "generic_learnskill_accepted":
            case "generic_learnskill_guildaccepted":
                OnDisplayingAccepted(source);

                break;
        }
    }
```

4. Replace `OnDisplayingRequirements`:

```csharp
    private void OnDisplayingRequirements(Aisling source)
    {
        if (!TryFetchArgs<string>(out var skillName)
            || !TryGetSkill(skillName, source, out var skill)
            || source.SkillBook.Contains(skillName))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        var learningRequirementsStr = skill.Template
                                           .LearningRequirements
                                           ?.BuildRequirementsString(ItemFactory, SkillFactory, SpellFactory)
                                           .ToString();

        Subject.InjectTextParameters(skill.Template.Description ?? string.Empty, learningRequirementsStr ?? string.Empty);

        var requiredGold = skill.Template.LearningRequirements?.RequiredGold ?? 0;

        //between "Yes" and "No"
        if (GuildFundsHelper.TryQuote(source, GuildAllowanceKind.Training, requiredGold, out _))
            Subject.InsertOption(1, GuildFundsHelper.OPTION_TEXT, "generic_learnskill_guildfunds");
    }
```

5. Insert after `OnDisplayingRequirements`:

```csharp

    private void OnDisplayingGuildFunds(Aisling source)
    {
        if (!TryFetchArgs<string>(out var skillName)
            || !TryGetSkill(skillName, source, out var skill)
            || source.SkillBook.Contains(skillName))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        var requiredGold = skill.Template.LearningRequirements?.RequiredGold ?? 0;

        if (!GuildFundsHelper.TryQuote(source, GuildAllowanceKind.Training, requiredGold, out var quote))
        {
            Subject.Reply(source, "Your guild can't pay toward this right now.", "generic_learnskill_initial");

            return;
        }

        //the accepted dialog pays only if the split is still the one shown here
        Subject.Context = new GuildFundsQuoteContext(quote.Cost, quote.GuildShare);
        Subject.InjectTextParameters(skill.Template.Name, GuildFundsHelper.DescribeSplit(quote, GuildAllowanceKind.Training));
    }

    private static bool IsGuildFunded(Dialog dialog) => dialog.Template.TemplateKey.EqualsI("generic_learnskill_guildaccepted");
```

6. Replace `ValidateAndTakeItemAndGoldRequirements` (it gains a `skillName` parameter):

```csharp
    private bool ValidateAndTakeItemAndGoldRequirements(
        Aisling source,
        Dialog dialog,
        LearningRequirements requirements,
        string skillName)
    {
        foreach (var itemRequirement in requirements.ItemRequirements)
        {
            var requiredItem = ItemFactory.CreateFaux(itemRequirement.ItemTemplateKey);

            if (!source.Inventory.HasCount(requiredItem.DisplayName, itemRequirement.AmountRequired))
            {
                dialog.Reply(source, "Come back when you have what is required.", "generic_learnskill_initial");

                return false;
            }
        }

        var requiredGold = requirements.RequiredGold ?? 0;
        var guildFunded = IsGuildFunded(dialog);

        if (guildFunded)
        {
            //both shares are taken before any item, so a refused payment costs the member nothing
            if (!GuildFundsHelper.TryPay(
                    source,
                    dialog,
                    GuildAllowanceKind.Training,
                    requiredGold,
                    $"Training: {skillName}",
                    GuildHouseStateStorage,
                    Logger,
                    out var failureMessage))
            {
                dialog.Reply(source, failureMessage, "generic_learnskill_initial");

                return false;
            }
        } else if (source.Gold < requiredGold)
        {
            dialog.Reply(source, "Come back when you are more wealthy.", "generic_learnskill_initial");

            return false;
        }

        foreach (var itemRequirement in requirements.ItemRequirements)
        {
            var requiredItem = ItemFactory.CreateFaux(itemRequirement.ItemTemplateKey);
            source.Inventory.RemoveQuantity(requiredItem.DisplayName, itemRequirement.AmountRequired);
        }

        if (!guildFunded && (requiredGold > 0))
            source.TryTakeGold(requiredGold);

        return true;
    }
```

7. In `ValidateAndTakeRequirements`, pass the name. Use `replace_content`, literal, needle `|| !ValidateAndTakeItemAndGoldRequirements(source, dialog, requirements))`, replacement `|| !ValidateAndTakeItemAndGoldRequirements(source, dialog, requirements, template.Name))`.

- [ ] **Step 6: Add the trainer dialogs**

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSpell/generic_learnspell_guildfunds.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_learnspell_guildaccepted",
      "optionText": "Yes"
    },
    {
      "dialogKey": "generic_learnspell_initial",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "learnspell"
  ],
  "scriptVars": {},
  "templateKey": "generic_learnspell_guildfunds",
  "text": "{Spell}\n\n{Split}\n\nDo you wish to learn this spell?",
  "type": "DialogMenu"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSpell/generic_learnspell_guildaccepted.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "learnspell"
  ],
  "scriptVars": {},
  "templateKey": "generic_learnspell_guildaccepted",
  "text": "Your guild has helped pay for your lesson. Use it well.",
  "type": "Normal"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSkill/generic_learnskill_guildfunds.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_learnskill_guildaccepted",
      "optionText": "Yes"
    },
    {
      "dialogKey": "generic_learnskill_initial",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "learnskill"
  ],
  "scriptVars": {},
  "templateKey": "generic_learnskill_guildfunds",
  "text": "{Skill}\n\n{Split}\n\nDo you wish to learn this skill?",
  "type": "DialogMenu"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSkill/generic_learnskill_guildaccepted.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "learnskill"
  ],
  "scriptVars": {},
  "templateKey": "generic_learnskill_guildaccepted",
  "text": "Your guild has helped pay for your lesson. Use it well.",
  "type": "Normal"
}
```

`contextual: true` is what carries the chosen skill or spell name (`MenuArgs`) and the agreed split (`Context`) from one dialog to the next (`Dialog.Next`).

- [ ] **Step 7: Check the build and the JSON**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Chaos/Chaos.csproj
cd "C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-unora/Data/Configuration/Templates/Dialogs/Temauir/generic"
python -c "import json,sys; [json.load(open(p, encoding='utf-8')) for p in sys.argv[1:]]; print('ok')" LearnSpell/generic_learnspell_guildfunds.json LearnSpell/generic_learnspell_guildaccepted.json LearnSkill/generic_learnskill_guildfunds.json LearnSkill/generic_learnskill_guildaccepted.json
```

Expected: `Build succeeded` with no new warnings in the two trainer scripts, then `ok`.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSpellScript.cs", "Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSkillScript.cs", "Tests/Chaos.Tests/Trainers/LearnSpellScriptTests.cs", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSpell/generic_learnspell_guildfunds.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSpell/generic_learnspell_guildaccepted.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSkill/generic_learnskill_guildfunds.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSkill/generic_learnskill_guildaccepted.json"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.Trainers/*/*\" --no-ansi", "acceptanceCriteria": ["requirements menu reads Yes / Use guild funds / No with allowance left, Yes / No when off", "guild-funds prompt stores GuildFundsQuoteContext(cost, guild share)", "guild-funded learning takes member share from member and guild share from bank", "guild-funded learning takes nothing when the member cannot pay their share", "paying yourself takes the full price and nothing from the bank", "LearnSkillScript mirrors the spell changes and builds", "four new dialog files are valid JSON"], "modelTier": "standard"}
```

---

### Task 6: Guild funds at smiths

**Goal:** Repair All and single-item repairs at every smith offer "Use guild funds" when the rank has repair allowance left, show the split, and charge it on "Yes".

**Files:**
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/Generic/RepairAllItemsScript.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/Generic/RepairSingleItemScript.cs`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairAllItemGuildFunds.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairAllItemGuildAccepted.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairSingleItemGuildFunds.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairSingleItemGuildAccepted.json`

**Acceptance Criteria:**
- [ ] `generic_repairAllItemInitial` and `generic_repairSingleItemConfirmation` get a "Use guild funds" option between Yes and No when `GuildFundsHelper.TryQuote` is true for the repair cost
- [ ] The guild-funds prompts store `GuildFundsQuoteContext` and show the split
- [ ] The guild-accepted dialogs pay through `GuildFundsHelper.TryPay` and repair only if it succeeds; the log descriptions are `Repairs: all items` and `Repairs: <item display name>`
- [ ] Paying yourself works exactly as before
- [ ] `VerbalRepairAllScript` is unchanged
- [ ] The server builds and the four dialog files are valid JSON

**Verify:** `dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Chaos/Chaos.csproj` → `Build succeeded`

**Steps:**

- [ ] **Step 1: Change `RepairAllItemsScript`**

In `SRV/Chaos/Scripting/DialogScripts/Temuair/Generic/RepairAllItemsScript.cs`:

1. Add `using Chaos.Collections;`, `using Chaos.Storage.Abstractions;` and `using Chaos.Utilities;` to the `using` block.
2. Change the primary constructor to:

```csharp
public class RepairAllItemsScript(
    Dialog subject,
    ILogger<RepairAllItemsScript> logger,
    IStorage<GuildHouseState> guildHouseStateStorage) : DialogScriptBase(subject)
```

3. Replace `OnDisplaying`:

```csharp
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "generic_repairalliteminitial":
                OnDisplayingInitial(source);

                break;
            case "generic_repairallitemaccepted":
                OnDisplayingAccepted(source, false);

                break;
            case "generic_repairallitemguildfunds":
                OnDisplayingGuildFunds(source);

                break;
            case "generic_repairallitemguildaccepted":
                OnDisplayingAccepted(source, true);

                break;
        }
    }
```

4. Replace `OnDisplayingInitial`:

```csharp
    private void OnDisplayingInitial(Aisling source)
    {
        CalculateRepairs(source);

        if (RepairCost == 0)
        {
            Subject.Reply(source, "All of your items are already fully repaired!");

            return;
        }

        Subject.InjectTextParameters((int)RepairCost);

        //between "Yes" and "No"
        if (GuildFundsHelper.TryQuote(source, GuildAllowanceKind.Repairs, (int)RepairCost, out _))
            Subject.InsertOption(1, GuildFundsHelper.OPTION_TEXT, "generic_repairAllItemGuildFunds");
    }
```

5. Insert after `OnDisplayingInitial`:

```csharp

    private void OnDisplayingGuildFunds(Aisling source)
    {
        CalculateRepairs(source);

        if (RepairCost == 0)
        {
            Subject.Reply(source, "All of your items are already fully repaired!");

            return;
        }

        if (!GuildFundsHelper.TryQuote(source, GuildAllowanceKind.Repairs, (int)RepairCost, out var quote))
        {
            Subject.Reply(source, "Your guild can't pay toward this right now.");

            return;
        }

        //the accepted dialog pays only if the cost and split are still the ones shown here
        Subject.Context = new GuildFundsQuoteContext(quote.Cost, quote.GuildShare);
        Subject.InjectTextParameters(GuildFundsHelper.DescribeSplit(quote, GuildAllowanceKind.Repairs));
    }
```

6. Replace `OnDisplayingAccepted` (it gains a `useGuildFunds` parameter; everything after the payment is unchanged):

```csharp
    private void OnDisplayingAccepted(Aisling source, bool useGuildFunds)
    {
        CalculateRepairs(source);

        if (useGuildFunds)
        {
            if (!GuildFundsHelper.TryPay(
                    source,
                    Subject,
                    GuildAllowanceKind.Repairs,
                    (int)RepairCost,
                    "Repairs: all items",
                    guildHouseStateStorage,
                    logger,
                    out var failureMessage))
            {
                Subject.Reply(source, failureMessage);

                return;
            }
        } else if ((RepairCost > 0) && !source.TryTakeGold((int)RepairCost))
        {
            // Paying yourself: only take gold if the repair cost is greater than 0
            Subject.Close(source);
            source.SendOrangeBarMessage($"You do not have enough. You need {(int)RepairCost} gold.");

            return;
        }

        logger.WithTopics(Topics.Entities.Aisling, Topics.Entities.Item, Topics.Entities.Gold)
              .WithProperty(source)
              .WithProperty(Subject)
              .LogInformation("{@AislingName} has repaired all items for {@AmountGold}", source.Name, RepairCost);

        foreach (var repair in source.Equipment)
            if ((repair.Template.MaxDurability > 0) && (repair.CurrentDurability != repair.Template.MaxDurability))
            {
                repair.CurrentDurability = repair.Template.MaxDurability;
                repair.LastWarningLevel = 100;
            }

        foreach (var repair in source.Inventory)
            if ((repair.Template.MaxDurability > 0) && (repair.CurrentDurability != repair.Template.MaxDurability))
                source.Inventory.Update(
                    repair.Slot,
                    _ =>
                    {
                        repair.CurrentDurability = repair.Template.MaxDurability;
                        repair.LastWarningLevel = 100;
                    });

        source.SendOrangeBarMessage(
            RepairCost > 0 ? "Your items have been repaired." : "Your slightly damaged items were repaired for free.");

        Subject.InjectTextParameters((int)RepairCost);
        source.Client.SendSound(172, false);
    }
```

- [ ] **Step 2: Change `RepairSingleItemScript`**

In `SRV/Chaos/Scripting/DialogScripts/Temuair/Generic/RepairSingleItemScript.cs`:

1. Add `using Chaos.Collections;`, `using Chaos.Storage.Abstractions;` and `using Chaos.Utilities;` to the `using` block.
2. Change the primary constructor to:

```csharp
public class RepairSingleItemScript(
    Dialog subject,
    ILogger<RepairSingleItemScript> logger,
    IStorage<GuildHouseState> guildHouseStateStorage) : DialogScriptBase(subject)
```

3. Replace `OnDisplaying`:

```csharp
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "generic_repairsingleiteminitial":
                OnDisplayingInitial(source);

                break;
            case "generic_repairsingleitemconfirmation":
                OnDisplayingConfirmation(source);

                break;
            case "generic_repairsingleitemaccepted":
                OnDisplayingAccepted(source, false);

                break;
            case "generic_repairsingleitemguildfunds":
                OnDisplayingGuildFunds(source);

                break;
            case "generic_repairsingleitemguildaccepted":
                OnDisplayingAccepted(source, true);

                break;
        }
    }
```

4. Replace `OnDisplayingConfirmation`:

```csharp
    private void OnDisplayingConfirmation(Aisling source)
    {
        if (!TryFetchArgs<byte>(out var slot) || !source.Inventory.TryGetObject(slot, out var item))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        if (item is { CurrentDurability: not null, Template.MaxDurability: not null })
        {
            RepairCost = CalculateNewRepairCostForItem(source, item);

            Subject.InjectTextParameters(item.DisplayName, RepairCost);

            //between "Yes" and "No"
            if (GuildFundsHelper.TryQuote(source, GuildAllowanceKind.Repairs, (int)RepairCost, out _))
                Subject.InsertOption(1, GuildFundsHelper.OPTION_TEXT, "generic_repairSingleItemGuildFunds");
        }
    }
```

5. Insert after `OnDisplayingConfirmation`:

```csharp

    private void OnDisplayingGuildFunds(Aisling source)
    {
        if (!TryFetchArgs<byte>(out var slot) || !source.Inventory.TryGetObject(slot, out var item))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        RepairCost = CalculateNewRepairCostForItem(source, item);

        if (!GuildFundsHelper.TryQuote(source, GuildAllowanceKind.Repairs, (int)RepairCost, out var quote))
        {
            Subject.Reply(source, "Your guild can't pay toward this right now.");

            return;
        }

        //the accepted dialog pays only if the cost and split are still the ones shown here
        Subject.Context = new GuildFundsQuoteContext(quote.Cost, quote.GuildShare);
        Subject.InjectTextParameters(item.DisplayName, GuildFundsHelper.DescribeSplit(quote, GuildAllowanceKind.Repairs));
    }
```

6. Replace `OnDisplayingAccepted`:

```csharp
    private void OnDisplayingAccepted(Aisling source, bool useGuildFunds)
    {
        if (!TryFetchArgs<byte>(out var slot) || !source.Inventory.TryGetObject(slot, out var item))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        if (item is { CurrentDurability: not null, Template.MaxDurability: not null }
            && (item.CurrentDurability != item.Template.MaxDurability))
        {
            RepairCost = CalculateNewRepairCostForItem(source, item);

            if (useGuildFunds)
            {
                if (!GuildFundsHelper.TryPay(
                        source,
                        Subject,
                        GuildAllowanceKind.Repairs,
                        (int)RepairCost,
                        $"Repairs: {item.DisplayName}",
                        guildHouseStateStorage,
                        logger,
                        out var failureMessage))
                {
                    Subject.Reply(source, failureMessage);

                    return;
                }
            } else if ((RepairCost > 0) && !source.TryTakeGold((int)RepairCost))
            {
                // Paying yourself: only deduct gold if cost is greater than 0
                Subject.Close(source);
                source.SendOrangeBarMessage($"You do not have enough gold. You need {(int)RepairCost} gold.");

                return;
            }

            logger.WithTopics(Topics.Entities.Aisling, Topics.Entities.Item, Topics.Entities.Gold)
                  .WithProperty(source)
                  .WithProperty(Subject)
                  .LogInformation(
                      "{@AislingName} has repaired {@ItemName} for {@AmountGold}",
                      source.Name,
                      item.DisplayName,
                      RepairCost);

            source.Inventory.Update(
                slot,
                i =>
                {
                    i.CurrentDurability = i.Template.MaxDurability;
                    i.LastWarningLevel = 100;
                });

            source.SendOrangeBarMessage(
                RepairCost > 0
                    ? $"Your {item.DisplayName} has been repaired."
                    : $"Your slightly damaged {item.DisplayName} was repaired for free.");

            Subject.InjectTextParameters(item.DisplayName, (int)RepairCost);
            source.Client.SendSound(172, false);
        }
    }
```

- [ ] **Step 3: Add the repair dialogs**

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairAllItemGuildFunds.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_repairAllItemGuildAccepted",
      "optionText": "Yes"
    },
    {
      "dialogKey": "Close",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "repairAllItems"
  ],
  "scriptVars": {},
  "templateKey": "generic_repairAllItemGuildFunds",
  "text": "{Split}\n\nShall I repair all of your items?",
  "type": "DialogMenu"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairAllItemGuildAccepted.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "repairAllItems"
  ],
  "scriptVars": {},
  "templateKey": "generic_repairAllItemGuildAccepted",
  "text": "Thanks! Your items are as good as new. The full price was {Price} gold.",
  "type": "Normal"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairSingleItemGuildFunds.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_repairSingleItemGuildAccepted",
      "optionText": "Yes"
    },
    {
      "dialogKey": "generic_repairSingleItemInitial",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "repairSingleItem"
  ],
  "scriptVars": {},
  "templateKey": "generic_repairSingleItemGuildFunds",
  "text": "{Item}\n\n{Split}\n\nShall I repair it?",
  "type": "DialogMenu"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairSingleItemGuildAccepted.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "repairSingleItem"
  ],
  "scriptVars": {},
  "templateKey": "generic_repairSingleItemGuildAccepted",
  "text": "Thanks! Your {Item} is as good as new. The full price was {Price} gold.",
  "type": "Normal"
}
```

- [ ] **Step 4: Check the build and the JSON**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Chaos/Chaos.csproj
cd "C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-unora/Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems"
python -c "import json,sys; [json.load(open(p, encoding='utf-8')) for p in sys.argv[1:]]; print('ok')" generic_repairAllItemGuildFunds.json generic_repairAllItemGuildAccepted.json generic_repairSingleItemGuildFunds.json generic_repairSingleItemGuildAccepted.json
git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server diff --stat -- Chaos/Scripting/MerchantScripts/VerbalRepairAllScript.cs
```

Expected: `Build succeeded`, `ok`, and no diff for `VerbalRepairAllScript.cs`.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/Generic/RepairAllItemsScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Generic/RepairSingleItemScript.cs", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairAllItemGuildFunds.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairAllItemGuildAccepted.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairSingleItemGuildFunds.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairSingleItemGuildAccepted.json"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Chaos/Chaos.csproj", "acceptanceCriteria": ["repair-all initial and single confirmation insert Use guild funds between Yes and No when TryQuote is true", "guild-funds prompts store GuildFundsQuoteContext and show the split", "guild-accepted dialogs pay via TryPay and repair only on success, with the exact log descriptions", "paying yourself unchanged", "VerbalRepairAllScript unchanged", "server builds and four dialog files are valid JSON"], "modelTier": "standard"}
```

---

### Task 7: Quill's Allowances menu

**Goal:** At Quill, every member can see their rank's allowances and what's left this week. The leader can set any rank's training or repair allowance from the fixed list and read this week's spending per member.

**Files:**
- Create: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildAllowanceScript.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs`
- Test: `SRV/Tests/Chaos.Tests/GuildAllowances/GuildAllowanceScriptTests.cs`
- Create, all in `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement/`:
  - `generic_guild_allowance_initial.json`
  - `generic_guild_allowance_rank.json`
  - `generic_guild_allowance_kind.json`
  - `generic_guild_allowance_amount.json`
  - `generic_guild_allowance_set.json`
  - `generic_guild_allowance_report.json`

**Acceptance Criteria:**
- [ ] Quill shows "Allowances" to leaders (after Taxes) and to members (after Buffs)
- [ ] The first menu offers "Set allowances" and "This week's spending" only to the leader
- [ ] Choosing a rank stores its tier in the dialog context
- [ ] A leader choosing an amount sets that rank's allowance; option 5 of the amount menu is 1,000,000
- [ ] A non-leader choosing an amount changes nothing
- [ ] The six dialog files are valid JSON

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildAllowanceScriptTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildAllowances/GuildAllowanceScriptTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.Common.Abstractions;
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

namespace Chaos.Tests.GuildAllowances;

public sealed class GuildAllowanceScriptTests
{
    private const int MEMBER_TIER = 2;

    private static GuildAllowanceScript CreateScript(Dialog dialog)
        => new(
            dialog,
            new Mock<IClientRegistry<IChaosWorldClient>>().Object,
            new Mock<IStore<Guild>>().Object,
            new Mock<IFactory<Guild>>().Object,
            new Mock<ILogger<GuildAllowanceScript>>().Object);

    private static (Guild Guild, Aisling Leader, Aisling Member) CreateGuild()
    {
        var guild = MockGuild.Create();
        var leader = MockAisling.Create(name: "Stahli");
        var member = MockAisling.Create(name: "Iglis");

        guild.AddMember(leader, member);
        guild.ChangeRank(leader.Name, 0, member);
        guild.AddMember(member, leader);

        return (guild, leader, member);
    }

    [Test]
    public void The_first_menu_offers_leader_options_only_to_the_leader()
    {
        var (_, leader, member) = CreateGuild();
        var leaderDialog = MockDialog.Create("generic_guild_allowance_initial");
        var memberDialog = MockDialog.Create("generic_guild_allowance_initial");

        CreateScript(leaderDialog)
            .OnDisplaying(leader);

        CreateScript(memberDialog)
            .OnDisplaying(member);

        leaderDialog.Options
                    .Select(option => option.OptionText)
                    .Should()
                    .Equal("Set allowances", "This week's spending");

        memberDialog.Options
                    .Should()
                    .BeEmpty();
    }

    [Test]
    public void Choosing_a_rank_remembers_its_tier()
    {
        var (_, leader, _) = CreateGuild();
        var dialog = MockDialog.Create("generic_guild_allowance_rank");
        var script = CreateScript(dialog);

        script.OnDisplaying(leader);
        script.OnNext(leader, 2);

        dialog.Context
              .Should()
              .Be(new GuildAllowanceScript.EditContext(1, GuildAllowanceKind.Training));
    }

    [Test]
    public void A_leader_choosing_an_amount_sets_that_rank_allowance()
    {
        var (guild, leader, _) = CreateGuild();
        var dialog = MockDialog.Create("generic_guild_allowance_amount");
        dialog.Context = new GuildAllowanceScript.EditContext(MEMBER_TIER, GuildAllowanceKind.Repairs);

        //option 5 of Off, 100k, 250k, 500k, 1M, 2.5M, 5M
        CreateScript(dialog)
            .OnNext(leader, 5);

        guild.GetRanks()
             .Single(rank => rank.Tier == MEMBER_TIER)
             .RepairAllowance
             .Should()
             .Be(1_000_000);

        dialog.Context
              .Should()
              .Be(new GuildAllowanceScript.EditContext(MEMBER_TIER, GuildAllowanceKind.Repairs, 1_000_000));
    }

    [Test]
    public void A_member_choosing_an_amount_changes_nothing()
    {
        var (guild, _, member) = CreateGuild();
        var dialog = MockDialog.Create("generic_guild_allowance_amount");
        dialog.Context = new GuildAllowanceScript.EditContext(MEMBER_TIER, GuildAllowanceKind.Repairs);

        CreateScript(dialog)
            .OnNext(member, 5);

        guild.GetRanks()
             .Single(rank => rank.Tier == MEMBER_TIER)
             .RepairAllowance
             .Should()
             .Be(0);

        dialog.Context
              .Should()
              .Be(new GuildAllowanceScript.EditContext(MEMBER_TIER, GuildAllowanceKind.Repairs));
    }
}
```

`IStore<T>` is in `Chaos.Storage.Abstractions` and `IFactory<T>` is in `Chaos.Common.Abstractions`, as in `GuildTaxManagementScript`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/GuildAllowanceScriptTests/*" --no-ansi`
Expected: build error, `GuildAllowanceScript` not found.

- [ ] **Step 3: Write `GuildAllowanceScript`**

Create `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildAllowanceScript.cs`:

```csharp
#region
using System.Text;
using Chaos.Collections;
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.DarkAges.Extensions;
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
///     Quill's Allowances menu. Members see what their rank may spend from the guild bank on training and repairs each
///     week; the leader sets those amounts and reads this week's spending
/// </summary>
public class GuildAllowanceScript : GuildScriptBase
{
    /// <summary>
    ///     The weekly amounts a leader can pick, in the order of the amount menu's options
    /// </summary>
    public static readonly IReadOnlyList<int> AllowanceChoices =
    [
        0,
        100_000,
        250_000,
        500_000,
        1_000_000,
        2_500_000,
        5_000_000
    ];

    /// <inheritdoc />
    public GuildAllowanceScript(
        Dialog subject,
        IClientRegistry<IChaosWorldClient> clientRegistry,
        IStore<Guild> guildStore,
        IFactory<Guild> guildFactory,
        ILogger<GuildAllowanceScript> logger)
        : base(
            subject,
            clientRegistry,
            guildStore,
            guildFactory,
            logger) { }

    private static string DescribeAllowance(int amount) => amount == 0 ? "Off" : $"{amount:N0} a week";

    private static string DescribeKind(GuildAllowanceKind kind) => kind == GuildAllowanceKind.Training ? "training" : "repair";

    private static string DescribeStatus(GuildAllowanceStatus status)
        => status.Allowance == 0 ? "Off" : $"{status.Allowance:N0} a week, {status.Left:N0} left";

    private static string GetRankName(Guild guild, int tier)
        => guild.GetRanks()
                .FirstOrDefault(rank => rank.Tier == tier)
                ?.Name
           ?? $"Tier {tier}";

    private static bool IsLeader(Aisling source, [MaybeNullWhen(false)] out Guild guild)
        => IsInGuild(source, out guild, out var sourceRank) && sourceRank.IsLeaderRank;

    /// <inheritdoc />
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "generic_guild_allowance_initial":
                OnDisplayingInitial(source);

                break;
            case "generic_guild_allowance_rank":
                OnDisplayingRank(source);

                break;
            case "generic_guild_allowance_kind":
                OnDisplayingKind(source);

                break;
            case "generic_guild_allowance_amount":
                OnDisplayingAmount(source);

                break;
            case "generic_guild_allowance_set":
                OnDisplayingSet(source);

                break;
            case "generic_guild_allowance_report":
                OnDisplayingReport(source);

                break;
        }
    }

    private void OnDisplayingAmount(Aisling source)
    {
        if (!IsLeader(source, out var guild) || Subject.Context is not EditContext context)
        {
            Subject.Reply(source, "Only the guild leader can change allowances.", "top");

            return;
        }

        var rank = guild.GetRanks()
                        .First(rank => rank.Tier == context.Tier);

        Subject.InjectTextParameters(rank.Name, DescribeKind(context.Kind), DescribeAllowance(rank.GetAllowance(context.Kind)));
    }

    private void OnDisplayingInitial(Aisling source)
    {
        if (!IsInGuild(source, out var guild, out var sourceRank))
        {
            Subject.Reply(source, "You are not in a guild.", "top");

            return;
        }

        var training = guild.GetAllowanceStatus(source.Name, GuildAllowanceKind.Training);
        var repairs = guild.GetAllowanceStatus(source.Name, GuildAllowanceKind.Repairs);

        Subject.InjectTextParameters(sourceRank.Name, DescribeStatus(training), DescribeStatus(repairs));

        if (sourceRank.IsLeaderRank)
        {
            Subject.InsertOption(0, "Set allowances", "generic_guild_allowance_rank");
            Subject.InsertOption(1, "This week's spending", "generic_guild_allowance_report");
        }
    }

    private void OnDisplayingKind(Aisling source)
    {
        if (!IsLeader(source, out var guild) || Subject.Context is not EditContext context)
        {
            Subject.Reply(source, "Only the guild leader can change allowances.", "top");

            return;
        }

        var rank = guild.GetRanks()
                        .First(rank => rank.Tier == context.Tier);

        Subject.InjectTextParameters(rank.Name, DescribeAllowance(rank.TrainingAllowance), DescribeAllowance(rank.RepairAllowance));
    }

    private void OnDisplayingRank(Aisling source)
    {
        if (!IsLeader(source, out var guild))
        {
            Subject.Reply(source, "Only the guild leader can change allowances.", "top");

            return;
        }

        foreach (var rank in guild.GetRanks()
                                  .OrderBy(rank => rank.Tier))
            Subject.AddOption(rank.Name, "generic_guild_allowance_kind");

        Subject.AddOption("Nevermind", "generic_guild_allowance_initial");
    }

    private void OnDisplayingReport(Aisling source)
    {
        if (!IsLeader(source, out var guild))
        {
            Subject.Reply(source, "Only the guild leader can see this.", "top");

            return;
        }

        var ledger = guild.GetAllowanceLedger();

        var builder = new StringBuilder();
        builder.AppendLineFColored(MessageColor.Silver, "Allowance spending this week");
        builder.AppendLineFColored(MessageColor.Silver, $"(since {ledger.WeekStart:yyyy-MM-dd HH:mm} UTC)");
        builder.AppendLine();

        if (ledger.Spending.Count == 0)
            builder.AppendLineFColored(MessageColor.Gainsboro, "No one has used guild funds this week.");
        else
        {
            builder.AppendLineFColored(MessageColor.Orange, $"{"Player",-15} {"Training",-12} {"Repairs",-12}");
            builder.AppendLineFColored(MessageColor.Gainsboro, new string('-', 40));

            var isSilver = true;

            foreach ((var memberName, var spending) in ledger.Spending.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                var color = isSilver ? MessageColor.Silver : MessageColor.Gainsboro;
                builder.AppendLineFColored(color, $"{memberName,-15} {spending.Training,-12:N0} {spending.Repairs,-12:N0}");
                isSilver = !isSilver;
            }
        }

        source.SendServerMessage(ServerMessageType.ScrollWindow, builder.ToString());
        Subject.Close(source);
    }

    private void OnDisplayingSet(Aisling source)
    {
        if (!IsInGuild(source, out var guild, out _) || Subject.Context is not EditContext { Amount: { } amount } context)
        {
            Subject.Reply(source, "Only the guild leader can change allowances.", "top");

            return;
        }

        Subject.InjectTextParameters(GetRankName(guild, context.Tier), DescribeKind(context.Kind), DescribeAllowance(amount));
    }

    /// <inheritdoc />
    public override void OnNext(Aisling source, byte? optionIndex = null)
    {
        if (optionIndex is null)
            return;

        switch (Subject.Template.TemplateKey.ToLower())
        {
            case "generic_guild_allowance_rank":
                OnNextRank(source, optionIndex.Value);

                break;
            case "generic_guild_allowance_kind":
                if ((Subject.Context is EditContext context) && optionIndex.Value is 1 or 2)
                    Subject.Context = context with
                    {
                        Kind = optionIndex.Value == 1 ? GuildAllowanceKind.Training : GuildAllowanceKind.Repairs
                    };

                break;
            case "generic_guild_allowance_amount":
                OnNextAmount(source, optionIndex.Value);

                break;
        }
    }

    private void OnNextAmount(Aisling source, byte optionIndex)
    {
        if ((optionIndex < 1) || (optionIndex > AllowanceChoices.Count) || Subject.Context is not EditContext context)
            return;

        if (!IsLeader(source, out var guild))
            return;

        var amount = AllowanceChoices[optionIndex - 1];

        guild.SetRankAllowance(context.Tier, context.Kind, amount);

        Subject.Context = context with
        {
            Amount = amount
        };

        var rankName = GetRankName(guild, context.Tier);

        foreach (var member in guild.GetOnlineMembers())
            member.SendServerMessage(
                ServerMessageType.GuildChat,
                $"Leader {source.Name} set the {rankName} {DescribeKind(context.Kind)} allowance to {DescribeAllowance(amount)}.");

        Logger.WithTopics(Topics.Entities.Guild, Topics.Entities.Gold)
              .WithProperty(guild)
              .WithProperty(source)
              .LogInformation(
                  "Aisling {@AislingName} set guild {@GuildName} rank tier {RankTier} {AllowanceKind} allowance to {Amount}",
                  source.Name,
                  guild.Name,
                  context.Tier,
                  context.Kind,
                  amount);
    }

    private void OnNextRank(Aisling source, byte optionIndex)
    {
        if (!IsLeader(source, out var guild))
            return;

        //the options are the ranks in tier order, then "Nevermind"
        var rank = guild.GetRanks()
                        .OrderBy(rank => rank.Tier)
                        .ElementAtOrDefault(optionIndex - 1);

        if (rank is not null)
            Subject.Context = new EditContext(rank.Tier, GuildAllowanceKind.Training);
    }

    /// <summary>
    ///     What the leader has picked so far, carried between the menus in <see cref="Dialog.Context" />
    /// </summary>
    /// <param name="Tier">
    ///     The rank's tier
    /// </param>
    /// <param name="Kind">
    ///     Training or repairs
    /// </param>
    /// <param name="Amount">
    ///     The amount set, once the leader has picked one
    /// </param>
    public sealed record EditContext(int Tier, GuildAllowanceKind Kind, int? Amount = null);
}
```

`AppendLineFColored` and `MessageColor` come from the same imports `GuildBankManagementScript` uses (`Chaos.DarkAges.Extensions`, `Chaos.DarkAges.Definitions`).

- [ ] **Step 4: Add the option at Quill**

In `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs`, inside the `Subject.DialogSource.Name is "Quill"` branch only:

Change the leader list to:

```csharp
                Subject.AddOptions(
                    ("Buffs", "generic_guildbuff_initial"),
                    ("Taxes", "generic_guild_tax_initial"),
                    ("Allowances", "generic_guild_allowance_initial"),
                    ("Ranks", "generic_guild_ranks_initial"),
                    ("Members", "generic_guild_members_initial"),
                    ("Disband", "generic_guild_disband_initial"),
                    ("Leave", "generic_guild_leave_initial"));
```

Change the member list to:

```csharp
                Subject.AddOptions(
                    ("Buffs", "generic_guildbuff_initial"),
                    ("Allowances", "generic_guild_allowance_initial"),
                    ("Members", "generic_guild_members_initial"),
                    ("Leave", "generic_guild_leave_initial"));
```

Leave the non-Quill branch alone.

- [ ] **Step 5: Add the Quill dialogs**

In `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement/`, create:

`generic_guild_allowance_initial.json`:

```json
{
  "options": [
    {
      "dialogKey": "Top",
      "optionText": "Back"
    }
  ],
  "scriptKeys": [
    "guildAllowance"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_allowance_initial",
  "text": "Your guild can pay toward training and repairs, up to your rank's weekly allowance. The week starts Sunday at 20:00 UTC.\n\nRank: {Rank}\nTraining: {Training}\nRepairs: {Repairs}",
  "type": "Menu"
}
```

`generic_guild_allowance_rank.json`:

```json
{
  "options": [],
  "scriptKeys": [
    "guildAllowance"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_allowance_rank",
  "text": "Which rank's allowances would you like to change?",
  "type": "Menu"
}
```

`generic_guild_allowance_kind.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_allowance_amount",
      "optionText": "Training"
    },
    {
      "dialogKey": "generic_guild_allowance_amount",
      "optionText": "Repairs"
    },
    {
      "dialogKey": "generic_guild_allowance_initial",
      "optionText": "Nevermind"
    }
  ],
  "scriptKeys": [
    "guildAllowance"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_allowance_kind",
  "text": "{Rank}\nTraining: {Training}\nRepairs: {Repairs}\n\nWhich allowance would you like to change?",
  "type": "Menu"
}
```

`generic_guild_allowance_amount.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "generic_guild_allowance_set",
      "optionText": "Off"
    },
    {
      "dialogKey": "generic_guild_allowance_set",
      "optionText": "100,000 a week"
    },
    {
      "dialogKey": "generic_guild_allowance_set",
      "optionText": "250,000 a week"
    },
    {
      "dialogKey": "generic_guild_allowance_set",
      "optionText": "500,000 a week"
    },
    {
      "dialogKey": "generic_guild_allowance_set",
      "optionText": "1,000,000 a week"
    },
    {
      "dialogKey": "generic_guild_allowance_set",
      "optionText": "2,500,000 a week"
    },
    {
      "dialogKey": "generic_guild_allowance_set",
      "optionText": "5,000,000 a week"
    },
    {
      "dialogKey": "generic_guild_allowance_initial",
      "optionText": "Nevermind"
    }
  ],
  "scriptKeys": [
    "guildAllowance"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_allowance_amount",
  "text": "The {Rank} {Kind} allowance is {Current}. What should each member of this rank get?",
  "type": "Menu"
}
```

`generic_guild_allowance_set.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "guildAllowance"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_allowance_set",
  "text": "Done. The {Rank} {Kind} allowance is now {Amount}.",
  "type": "Normal"
}
```

`generic_guild_allowance_report.json`:

```json
{
  "options": [],
  "scriptKeys": [
    "guildAllowance"
  ],
  "scriptVars": {},
  "templateKey": "generic_guild_allowance_report",
  "text": "Here is this week's spending.",
  "type": "Normal"
}
```

The amount options must stay in the order of `GuildAllowanceScript.AllowanceChoices`. `OnNextAmount` maps option N to `AllowanceChoices[N - 1]`.

- [ ] **Step 6: Run the tests, the build and the JSON check**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildAllowances/*/*" --no-ansi
cd "C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-unora/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement"
python -c "import json,sys; [json.load(open(p, encoding='utf-8')) for p in sys.argv[1:]]; print('ok')" *.json
```

Expected: all pass, then `ok`.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildAllowanceScript.cs", "Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs", "Tests/Chaos.Tests/GuildAllowances/GuildAllowanceScriptTests.cs", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement/generic_guild_allowance_initial.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement/generic_guild_allowance_rank.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement/generic_guild_allowance_kind.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement/generic_guild_allowance_amount.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement/generic_guild_allowance_set.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement/generic_guild_allowance_report.json"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildAllowances/GuildAllowanceScriptTests/*\" --no-ansi", "acceptanceCriteria": ["Quill shows Allowances to leaders after Taxes and members after Buffs", "first menu offers Set allowances and This week's spending only to the leader", "choosing a rank stores its tier in context", "leader choosing amount option 5 sets 1,000,000", "non-leader choosing an amount changes nothing", "six dialog files are valid JSON"], "modelTier": "standard"}
```

---

### Task 8: Allowance Spending in the guild bank logs

**Goal:** The guild bank's View Logs menu lists the newest 30 guild-funded purchases: who, what for, the guild's share and when.

**Files:**
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBankManagementScript.cs`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash/stash_guildbankallowancelog.json`
- Modify: `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash/stash_guildbanklogs.json`

**Acceptance Criteria:**
- [ ] `stash_guildbanklogs` has a fifth option, "View Allowance Spending", leading to `stash_guildbankallowancelog`
- [ ] That dialog shows a scroll window of the newest 30 `AllowanceSpend` logs, or "No one has used guild funds recently."
- [ ] A player with no guild gets the empty message instead of an exception
- [ ] The server builds and both JSON files are valid

**Verify:** `dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Chaos/Chaos.csproj` → `Build succeeded`

**Steps:**

- [ ] **Step 1: Add the log case**

In `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBankManagementScript.cs`, add this case to the `switch` in `OnDisplaying`, after the `stash_guildbankgolddepositlog` case:

```csharp
            case "stash_guildbankallowancelog":
            {
                var guildHouseState = guildHouseStateStorage.Value;
                guildHouseState.SetStorage(guildHouseStateStorage);

                var spends = guildHouseState.GetItemTransactions(source.Guild?.Name ?? string.Empty)
                                            .Where(log => log.Action == GuildHouseState.TransactionType.AllowanceSpend)
                                            .OrderByDescending(log => log.Timestamp)
                                            .Take(30)
                                            .ToList();

                if (spends.Count == 0)
                {
                    source.SendServerMessage(ServerMessageType.ScrollWindow, "No one has used guild funds recently.");

                    return;
                }

                ShowAllowanceLog(source, spends);
                Subject.Close(source);

                break;
            }
```

Then add this method after `ShowGuildGoldTransactionLog`:

```csharp

    private static void ShowAllowanceLog(Aisling source, IEnumerable<GuildHouseState.ItemTransactionLog> logs)
    {
        var builder = new StringBuilder();
        builder.AppendLineFColored(MessageColor.Silver, "Recent Allowance Spending:");
        builder.AppendLine();
        builder.AppendLineFColored(MessageColor.Orange, "Player  Paid for  Guild paid  Date");
        builder.AppendLineFColored(MessageColor.Gainsboro, new string('-', 50));

        var isSilver = true;

        foreach (var log in logs)
        {
            var color = isSilver ? MessageColor.Silver : MessageColor.Gainsboro;
            builder.AppendLineFColored(color, $"{log.Member}  {log.ItemName}  {log.Amount:N0}  {log.Timestamp:g}");
            isSilver = !isSilver;
        }

        source.SendServerMessage(ServerMessageType.ScrollWindow, builder.ToString());
    }
```

`GetItemTransactions` returns an empty list for a guild with no entry, so a guild without a hall shows the empty message.

- [ ] **Step 2: Add the dialogs**

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash/stash_guildbankallowancelog.json`:

```json
{
  "options": [],
  "scriptKeys": [
    "guildbankmanagement"
  ],
  "scriptVars": {},
  "templateKey": "stash_guildbankallowancelog",
  "text": "Here is the allowance spending.",
  "type": "Normal"
}
```

In `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash/stash_guildbanklogs.json`, add a fifth entry at the end of `options`:

```json
    {
      "dialogKey": "stash_guildbankallowancelog",
      "optionText": "View Allowance Spending"
    }
```

(Add a comma after the previous entry's closing brace.)

- [ ] **Step 3: Check the build and the JSON**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Chaos/Chaos.csproj
cd "C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-unora/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash"
python -c "import json,sys; d=[json.load(open(p, encoding='utf-8')) for p in sys.argv[1:]]; print(len(d[1]['options']))" stash_guildbankallowancelog.json stash_guildbanklogs.json
```

Expected: `Build succeeded`, then `5`.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBankManagementScript.cs", "UNO:Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash/stash_guildbankallowancelog.json", "UNO:Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash/stash_guildbanklogs.json"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Chaos/Chaos.csproj", "acceptanceCriteria": ["stash_guildbanklogs has a fifth option View Allowance Spending", "the log dialog shows the newest 30 AllowanceSpend logs or the empty message", "a player with no guild gets the empty message, not an exception", "server builds and both JSON files are valid"], "modelTier": "mechanical"}
```

---

### Task 9: Commit the full implementation

**Goal:** Every test passes except the two known failures. The server branch, the Unora branch and this plan are each committed once.

**Files:**
- Commit: every file listed in Tasks 1–8, in `SRV` and `UNO`
- Commit: `Chaos.Client/docs/superpowers/plans/2026-09-25-guild-tuition.md` and its `.tasks.json` (on `Chaos.Client` `main`, by path)

**Acceptance Criteria:**
- [ ] The full `Chaos.Tests` run fails only `GiveAbility` and `OnItemDroppedOn` (stackable)
- [ ] `SRV` has one new commit on `feat/guild-tuition` with only this plan's server files
- [ ] `UNO` has one new commit on `feat/guild-tuition` with only this plan's dialog files, and no `Custom Client Mods` build output
- [ ] The plan and tasks file are committed on `Chaos.Client` `main`, with nothing else in that commit

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server log --oneline -1` → the guild tuition commit

**Steps:**

- [ ] **Step 1: Run the full server test suite**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi
```

Expected: every test passes except `GiveAbility` and `OnItemDroppedOn` (stackable). Any other failure: stop and fix it in the task that caused it.

- [ ] **Step 2: Commit the server worktree**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server
git status --short
git add -- Chaos/Collections/GuildAllowanceKind.cs Chaos/Collections/GuildAllowanceSpending.cs Chaos/Collections/GuildAllowanceLedgerSnapshot.cs Chaos/Collections/GuildAllowanceLedger.cs Chaos/Collections/GuildAllowanceStatus.cs Chaos/Collections/GuildAllowanceQuote.cs Chaos/Collections/GuildAllowancePayResult.cs Chaos/Collections/GuildRank.cs Chaos/Collections/Guild.cs Chaos.Schemas/Guilds/GuildAllowanceSpendingSchema.cs Chaos.Schemas/Guilds/GuildSchema.cs Chaos.Schemas/Guilds/GuildRankSchema.cs Chaos/Services/MapperProfiles/GuildMapperProfile.cs Chaos/Models/World/GuildHouseState.cs Chaos/Utilities/GuildFundsQuoteContext.cs Chaos/Utilities/GuildFundsHelper.cs Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSkillScript.cs Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSpellScript.cs Chaos/Scripting/DialogScripts/Temuair/Generic/RepairAllItemsScript.cs Chaos/Scripting/DialogScripts/Temuair/Generic/RepairSingleItemScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildAllowanceScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBankManagementScript.cs Tests/Chaos.Tests/GuildAllowances Tests/Chaos.Tests/Trainers/LearnSpellScriptTests.cs
git status --short
git commit -F - <<'EOF'
Let guilds pay for members' training and repairs

Each guild rank gets a weekly training allowance and a weekly repair
allowance, set by the leader at Quill. Trainers and smiths offer "Use
guild funds" when the member has allowance left; the guild pays up to
the allowance and the bank's gold, and the member pays the rest. The
guild bank logs every guild-paid purchase.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

Before committing, the second `git status --short` must show nothing staged outside the list above. If `appsettings.json` or `launchSettings.json` show as modified, leave them unstaged.

- [ ] **Step 3: Commit the Unora worktree**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-tuition-unora
git status --short
git add -- "Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSkill/generic_learnskill_guildfunds.json" "Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSkill/generic_learnskill_guildaccepted.json" "Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSpell/generic_learnspell_guildfunds.json" "Data/Configuration/Templates/Dialogs/Temauir/generic/LearnSpell/generic_learnspell_guildaccepted.json" "Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairAllItemGuildFunds.json" "Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairAllItemGuildAccepted.json" "Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairSingleItemGuildFunds.json" "Data/Configuration/Templates/Dialogs/Temauir/generic/RepairItems/generic_repairSingleItemGuildAccepted.json" "Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildAllowanceManagement" "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash/stash_guildbankallowancelog.json" "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Stash/stash_guildbanklogs.json"
git status --short
git commit -F - <<'EOF'
Add guild allowance dialogs for trainers, smiths, Quill and the bank logs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

Leave any modified `Custom Client Mods/**/obj` files unstaged. They are build output.

- [ ] **Step 4: Commit the plan on Chaos.Client `main`**

`docs/superpowers` is gitignored in Chaos.Client, so use `-f`. Commit by path so nothing else in the shared checkout is included:

```bash
cd /c/Users/Michael/Documents/GitHub/Chaos.Client
git add -f -- docs/superpowers/plans/2026-09-25-guild-tuition.md docs/superpowers/plans/2026-09-25-guild-tuition.md.tasks.json
git commit -F - -- docs/superpowers/plans/2026-09-25-guild-tuition.md docs/superpowers/plans/2026-09-25-guild-tuition.md.tasks.json <<'EOF'
Add the guild tuition implementation plan

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 5: Report the in-game check**

Merging is the next step (superpowers-extended-cc:finishing-a-development-branch), not this task. Report the check for the user to run in game after the merge:

1. As a guild leader at Quill: Allowances, Set allowances, Applicant, Training, 100,000 a week. Guild chat announces the change.
2. As an Applicant: check Quill's Allowances shows "100,000 a week, 100,000 left".
3. At a trainer, pick something that costs more than 100,000. Pick "Use guild funds" and check the split message, then say Yes. The orange bar shows both shares.
4. Set the Applicant repair allowance, then repair at a town smith and at Fixx with "Use guild funds".
5. At the guild bank: View Logs, View Allowance Spending lists both purchases.
6. As the leader at Quill: This week's spending lists the Applicant.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-tuition-server log --oneline -1", "acceptanceCriteria": ["full Chaos.Tests run fails only GiveAbility and OnItemDroppedOn (stackable)", "one SRV commit with only this plan's server files", "one UNO commit with only this plan's dialog files and no Custom Client Mods obj output", "plan and tasks file committed alone on Chaos.Client main"], "modelTier": "mechanical"}
```
