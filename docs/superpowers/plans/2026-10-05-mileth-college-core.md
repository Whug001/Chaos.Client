# Mileth College Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build part 1 of Mileth College: the way there, the Lyceum hall and five subject rooms, roles, the timetable, classes with attendance and Educated Marks, the College board, the shop and the `/college` admin command.

**Architecture:** This follows the Mileth town government. Five pure rule units hold every rule, with no game-world access:

- `MarkLedger`
- `CollegeRoles`
- `Timetable`
- `ClassSession`
- `MachineKeys`

A singleton `CollegeService` owns `CollegeState`, which is saved through `IStorage<CollegeState>`. The service is driven by:

- dialog scripts;
- a room map script that samples presence;
- a 30-second `BackgroundService` tick.

Content comes from a Python map tool beside `Tools/TownHall/town_hall.py` and hand-written JSON.

**Tech Stack:** C# / .NET 10 (Chaos server), TUnit + FluentAssertions + Moq, Python 3 (map tool, unittest), the existing `Tools/TownHall/MapRender` C# renderer.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-05-mileth-college-core-design.md`. Its parent is `2026-10-04-mileth-college-design.md` in the same folder. Read both before starting a task.

## Global Constraints

- **Worktrees:** all server work is in `C:\Users\Michael\Documents\GitHub\worktrees\college-server`, branch `feat/college-core`, made from Chaos-Server `master`. All content work is in `C:\Users\Michael\Documents\GitHub\worktrees\college-unora`, branch `feat/college-core`, made from Unora `main`. Never edit the shared checkouts in `Chaos.Client\Chaos-Server` or `Unora`; other sessions use them.
- **No commits by implementers.** Leave every change in the worktree's working tree. The final task commits once per repo.
- **Never build the server and the client at the same time.** Only the server solution is built in this plan.
- **Tests:** server tests run with `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/Chaos.Tests.College/*/*"`, from the server worktree root. `dotnet test` does not work. The Python tests run from the Unora worktree root with `python -m unittest Tools.College.test_college_maps`.
- **Serena first:** for C# work, use the Serena MCP tools (`get_symbols_overview`, `find_symbol`, `replace_symbol_body`, `insert_after_symbol`, `replace_content`) instead of the built-in Read/Edit. This is the user's standing rule.
- **Namespace:** `Chaos.Services.College` for every type under `Chaos/Services/College/`. Tests live in `Tests/Chaos.Tests/College/`, namespace `Chaos.Tests.College`.
- **Names:** all name comparisons are case-insensitive. `CollegeState` dictionaries are keyed by `name.ToLowerInvariant()` and store the display name inside the value.
- **Time:** `DateTime` values are UTC. Services read time from the injected `TimeProvider`, through `Time.GetUtcNow().UtcDateTime`.
- **Dialog limits:** option text is 35 characters at most. Dialog text is 90 characters per line and 360 characters in total. Orange bar text is 45 characters at most. No em dashes in JSON; use hyphens.
- **Map numbers:** 774 College Grounds, 775 Chamber of Creation, 776 Chronicle Hall, 777 Codex Room, 778 Philosophers' Thought. 762 is the Lyceum hall and 763 the Chamber of Quills.
- **Room instance ids:**
  - `college_creation`: Art and Music
  - `college_chronicle`: History
  - `college_quills`: Literature
  - `college_codex`: Lore
  - `college_philosophers`: Philosophy
- **Other instance ids:** hall `mileth_college`, grounds `mileth_college_grounds`.
- **Comments:** comment only to explain why, when the code isn't self-evident. No explanatory comments in test code.

**User decisions (already made):**
- One class runs at a time across the whole College.
- Class formats: lecture, discussion or activity (the Teacher picks; activity is hidden until a creation tool exists).
- Each student who stays for the whole class gets one roll; the chance starts at 20%, adds 10 points per miss, and resets on success.
- Minimum class length is 20 minutes. At least 3 students on 3 different computers, each present 80% of the time.
- The Teacher can't end a class before 20 minutes.
- The Teacher gets 1 guaranteed mark per class that meets the rules.
- Timetable plus walk-ins.
- Teachers teach only their award's subject. Knights teach a subject until it has a Teacher. Admins always can. The Director is appointed by admins.
- One currency, Educated Marks, used for entry (3 marks) and the shop.
- Shop stock and prices are as in the spec table.
- The College is reached by a new path from Mileth Village Way to College Grounds, with the Tavaly colonnade temple as the building.
- The inside is the Lyceum hall plus five rooms; Art and Music share the Chamber of Creation.
- The user delegated the remaining decisions overnight. The spec lists them under "Recommendations made without the user".

---

## File map

**Server (`worktrees/college-server`)**

| File | Responsibility |
|---|---|
| `Chaos/Services/College/CollegeEnums.cs` | `CollegeSubject`, `AwardTier`, `ClassFormat`, `BookResult`, `ClassOutcome`, `StartResult`, `EndResult` |
| `Chaos/Services/College/CollegeOptions.cs` | tunable numbers and the subject → room map |
| `Chaos/Services/College/CollegeState.cs` | saved state and its records |
| `Chaos/Services/College/MarkLedger.cs` | rolls, give, take |
| `Chaos/Services/College/CollegeRoles.cs` | teach, judge, moderate rules |
| `Chaos/Services/College/Timetable.cs` | booking rules, walk-in limit, upcoming list |
| `Chaos/Services/College/MachineKeys.cs` | the per-computer key |
| `Chaos/Services/College/ClassSession.cs` | presence and the end-of-class check |
| `Chaos/Services/College/ICollegeNotifier.cs`, `CollegeNotifier.cs` | orange bars, legend marks |
| `Chaos/Services/College/CollegeService.cs` | the locked entry point |
| `Chaos/Services/College/CollegeTickService.cs` | 30-second tick |
| `Chaos/Services/College/CollegeText.cs` | timetable lines and messages |
| `Chaos/Scripting/MapScripts/Temuair/College/CollegeRoomScript.cs` | presence sampling |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs` | Registrar menus |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs` | lectern menus |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeShopScript.cs` | marks shop |
| `Chaos/Scripting/BulletinBoardScripts/CollegeBoardScript.cs` | board rights |
| `Chaos/Messaging/Admin/CollegeCommand.cs` | `/college` |
| `Chaos/Extensions/ServiceCollectionExtensions.cs` | `AddCollege()` |
| `Chaos/Program.cs` | call `AddCollege()` |
| `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` | refresh the legend mark at login |
| `Tests/Chaos.Tests/College/*.cs` | unit tests |

**Content (`worktrees/college-unora`)**

| File | Responsibility |
|---|---|
| `Tools/College/college_maps.py` | builds all College maps and instance files |
| `Tools/College/test_college_maps.py` | tests for the tool |
| `Tools/College/README.md` | how to run it |
| `Tools/College/*_base.map` | base copies of 402, 762, 763 |
| `Data/Configuration/MapData/lod402.map`, `lod762.map`, `lod763.map`, `lod774-778.map` | built maps |
| `Data/Configuration/Templates/Maps/774-778.json` | new templates |
| `Data/Configuration/MapInstances/Temuair/Towns/Mileth/College/**` | hall, grounds, rooms |
| `Data/Configuration/MapInstances/Temuair/Towns/Mileth/Mileth_Village_Way/reactors.json` | new edge warps |
| `Data/Configuration/Templates/Merchants/Temauir/tagor/{veyrin,regalia}.json`, `.../mileth/college_lectern.json` | NPCs |
| `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/*.json` | dialogs |
| `Data/Configuration/Templates/BulletinBoards/milethcollege.json` | board |

---

### Task 0: Create the worktrees

**Goal:** Two isolated worktrees on `feat/college-core`, one for the server and one for Unora, with the server building and its College test filter runnable.

**Files:**
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\college-server` (git worktree of Chaos-Server)
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\college-unora` (git worktree of Unora)

**Acceptance Criteria:**
- [ ] `git -C worktrees/college-server branch --show-current` prints `feat/college-core`
- [ ] `git -C worktrees/college-unora branch --show-current` prints `feat/college-core`
- [ ] `dotnet build Chaos/Chaos.csproj -c Debug` in the server worktree ends with `0 Error(s)`

**Verify:** `dotnet build Chaos/Chaos.csproj -c Debug` (server worktree) → `0 Error(s)`

**Steps:**

- [ ] **Step 1: Create both worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server
git worktree add -b feat/college-core /c/Users/Michael/Documents/GitHub/worktrees/college-server master
cd /c/Users/Michael/Documents/GitHub/Unora
git worktree add -b feat/college-core /c/Users/Michael/Documents/GitHub/worktrees/college-unora main
```

- [ ] **Step 2: Build the server worktree**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/college-server
dotnet build Chaos/Chaos.csproj -c Debug 2>&1 | tail -3
```
Expected: `0 Error(s)`.

---

### Task 1: Enums, options, state and MarkLedger

**Goal:** The College's data types and the Educated Mark roll rules, covered by tests.

**Files:**
- Create: `Chaos/Services/College/CollegeEnums.cs`
- Create: `Chaos/Services/College/CollegeOptions.cs`
- Create: `Chaos/Services/College/CollegeState.cs`
- Create: `Chaos/Services/College/MarkLedger.cs`
- Test: `Tests/Chaos.Tests/College/MarkLedgerTests.cs`

**Acceptance Criteria:**
- [ ] With a random source that always returns 0.99, rolls miss with chances 0.2 through 0.9 and succeed on the 9th roll at 1.0
- [ ] A success adds 1 to `Marks` and `LifetimeMarks` and resets the chance to 0.2
- [ ] `TryTake` refuses to go below 0 and leaves the balance unchanged
- [ ] `CollegeState` keys students and awards by lower-case name

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/Chaos.Tests.College/MarkLedgerTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the types**

`Chaos/Services/College/CollegeEnums.cs`:

```csharp
namespace Chaos.Services.College;

public enum CollegeSubject
{
    Art,
    Music,
    History,
    Literature,
    Lore,
    Philosophy
}

public enum AwardTier
{
    None,
    Clave,
    Village,
    Kingdom,
    Aisling
}

public enum ClassFormat
{
    Lecture,
    Discussion,
    Activity
}

public enum BookResult
{
    Booked,
    NotAllowed,
    InThePast,
    TooFarAhead,
    BadLength,
    BadStartTime,
    Overlaps,
    FormatNotAvailable
}

public enum ClassOutcome
{
    Completed,
    CalledOff,
    TeacherAbsent,
    TooFewStudents
}

public enum StartResult
{
    Started,
    NotAllowed,
    ClassRunning,
    WrongRoom,
    NoTimeBeforeNextBooking,
    NoSuchBooking,
    NotYourBooking,
    TooEarly,
    FormatNotAvailable
}

public enum EndResult
{
    Ended,
    NoClass,
    NotYourClass,
    TooSoon
}
```

`Chaos/Services/College/CollegeOptions.cs`:

```csharp
namespace Chaos.Services.College;

public sealed record CollegeOptions
{
    public int TickSeconds { get; set; } = 30;
    public int MinimumClassMinutes { get; set; } = 20;
    public int MaximumClassMinutes { get; set; } = 120;
    public int MinimumStudents { get; set; } = 3;
    public double PresenceShare { get; set; } = 0.8;
    public int StartEarlyMinutes { get; set; } = 10;
    public int StartGraceMinutes { get; set; } = 10;
    public int TeacherAbsenceMinutes { get; set; } = 10;
    public int BookingDaysAhead { get; set; } = 14;
    public int TimetableDaysShown { get; set; } = 7;
    public double FirstRollChance { get; set; } = 0.2;
    public double RollChanceStep { get; set; } = 0.1;
    public int EntryCost { get; set; } = 3;
    public int KnightJudgingTeacherLimit { get; set; } = 3;
    public int HistoryLength { get; set; } = 50;
    public string HallInstanceId { get; set; } = "mileth_college";

    public Dictionary<CollegeSubject, string> Rooms { get; set; } = new()
    {
        [CollegeSubject.Art] = "college_creation",
        [CollegeSubject.Music] = "college_creation",
        [CollegeSubject.History] = "college_chronicle",
        [CollegeSubject.Literature] = "college_quills",
        [CollegeSubject.Lore] = "college_codex",
        [CollegeSubject.Philosophy] = "college_philosophers"
    };

    public static IReadOnlyList<int> Lengths { get; } = [20, 30, 45, 60, 90, 120];

    public string RoomFor(CollegeSubject subject) => Rooms[subject];

    public IEnumerable<CollegeSubject> SubjectsInRoom(string roomInstanceId)
        => Rooms.Where(kv => string.Equals(kv.Value, roomInstanceId, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .OrderBy(s => s);

    public bool IsRoom(string instanceId) => Rooms.Values.Any(r => string.Equals(r, instanceId, StringComparison.OrdinalIgnoreCase));
}
```

`Chaos/Services/College/CollegeState.cs`:

```csharp
namespace Chaos.Services.College;

/// <summary>Everything the College saves. Dictionaries are keyed by lower-case character name.</summary>
public sealed class CollegeState
{
    public string? Director { get; set; }
    public int NextBookingId { get; set; } = 1;
    public List<CollegeBooking> Bookings { get; set; } = [];
    public CollegeClass? RunningClass { get; set; }
    public Dictionary<string, CollegeStudent> Students { get; set; } = [];

    /// <summary>Character (lower case) to subject name to the highest award held in that subject.</summary>
    public Dictionary<string, Dictionary<string, AwardTier>> Awards { get; set; } = [];

    public List<CollegeClassRecord> History { get; set; } = [];

    public static string Key(string name) => name.ToLowerInvariant();

    public CollegeStudent StudentFor(string name)
    {
        var key = Key(name);

        if (!Students.TryGetValue(key, out var student))
            Students[key] = student = new CollegeStudent { Name = name };

        return student;
    }

    public AwardTier AwardOf(string name, CollegeSubject subject)
        => Awards.TryGetValue(Key(name), out var awards) && awards.TryGetValue(subject.ToString(), out var tier) ? tier : AwardTier.None;

    public void SetAward(string name, CollegeSubject subject, AwardTier tier)
    {
        var key = Key(name);

        if (!Awards.TryGetValue(key, out var awards))
            Awards[key] = awards = [];

        if (tier == AwardTier.None)
            awards.Remove(subject.ToString());
        else
            awards[subject.ToString()] = tier;

        if (awards.Count == 0)
            Awards.Remove(key);
    }
}

public sealed class CollegeStudent
{
    public string Name { get; set; } = "";
    public int Marks { get; set; }
    public int LifetimeMarks { get; set; }

    /// <summary>0 means "never rolled"; the ledger treats it as the first-roll chance.</summary>
    public double RollChance { get; set; }

    public int FreeEntries { get; set; }
}

public sealed class CollegeBooking
{
    public int Id { get; set; }
    public string Teacher { get; set; } = "";
    public CollegeSubject Subject { get; set; }
    public ClassFormat Format { get; set; }
    public DateTime StartUtc { get; set; }
    public int Minutes { get; set; }
    public DateTime EndUtc => StartUtc.AddMinutes(Minutes);
}

public sealed class CollegeClass
{
    public int? BookingId { get; set; }
    public string Teacher { get; set; } = "";
    public string TeacherMachine { get; set; } = "";
    public CollegeSubject Subject { get; set; }
    public ClassFormat Format { get; set; }
    public string Room { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public int PlannedMinutes { get; set; }
    public int TeacherSeconds { get; set; }
    public DateTime TeacherLastSeenUtc { get; set; }
    public Dictionary<string, CollegePresence> Presence { get; set; } = [];
    public List<string> Removed { get; set; } = [];
    public DateTime PlannedEndUtc => StartedUtc.AddMinutes(PlannedMinutes);
}

public sealed class CollegePresence
{
    public string Name { get; set; } = "";
    public string MachineKey { get; set; } = "";
    public int Seconds { get; set; }
}

public sealed class CollegeClassRecord
{
    public string Teacher { get; set; } = "";
    public CollegeSubject Subject { get; set; }
    public DateTime StartedUtc { get; set; }
    public int Minutes { get; set; }
    public ClassOutcome Outcome { get; set; }
    public List<string> Qualified { get; set; } = [];
    public List<string> Won { get; set; } = [];
}
```

- [ ] **Step 2: Write the failing tests**

`Tests/Chaos.Tests/College/MarkLedgerTests.cs`:

```csharp
#region
using Chaos.Services.College;
using FluentAssertions;
#endregion

namespace Chaos.Tests.College;

public sealed class MarkLedgerTests
{
    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }

    private static readonly CollegeOptions Options = new();

    [Test]
    public void Chances_rise_by_ten_points_per_miss_until_the_ninth_roll_succeeds()
    {
        var state = new CollegeState();
        var ledger = new MarkLedger(Options);
        var random = new FixedRandom(0.99);
        var seen = new List<double>();

        for (var i = 0; i < 8; i++)
        {
            seen.Add(Math.Round(ledger.ChanceOf(state, "Aroha"), 4));
            ledger.Roll(state, "Aroha", random).Should().BeFalse();
        }

        ledger.ChanceOf(state, "Aroha").Should().BeApproximately(1.0, 1e-9);
        ledger.Roll(state, "Aroha", random).Should().BeTrue();
        seen.Should().Equal(0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9);
    }

    [Test]
    public void A_success_adds_a_mark_and_resets_the_chance()
    {
        var state = new CollegeState();
        var ledger = new MarkLedger(Options);
        ledger.Roll(state, "Aroha", new FixedRandom(0.99));

        ledger.Roll(state, "aroha", new FixedRandom(0.0)).Should().BeTrue();

        var student = state.StudentFor("AROHA");
        student.Marks.Should().Be(1);
        student.LifetimeMarks.Should().Be(1);
        ledger.ChanceOf(state, "Aroha").Should().BeApproximately(0.2, 1e-9);
        state.Students.Keys.Should().Equal("aroha");
    }

    [Test]
    public void Take_never_goes_below_zero()
    {
        var state = new CollegeState();
        var ledger = new MarkLedger(Options);
        ledger.Give(state, "Aroha", 2);

        ledger.TryTake(state, "Aroha", 3).Should().BeFalse();
        state.StudentFor("Aroha").Marks.Should().Be(2);
        ledger.TryTake(state, "Aroha", 2).Should().BeTrue();
        state.StudentFor("Aroha").Marks.Should().Be(0);
    }

    [Test]
    public void Awards_are_stored_by_lower_case_name_and_none_removes_them()
    {
        var state = new CollegeState();
        state.SetAward("Aroha", CollegeSubject.Art, AwardTier.Village);

        state.AwardOf("AROHA", CollegeSubject.Art).Should().Be(AwardTier.Village);
        state.AwardOf("Aroha", CollegeSubject.Music).Should().Be(AwardTier.None);

        state.SetAward("aroha", CollegeSubject.Art, AwardTier.None);
        state.Awards.Should().BeEmpty();
    }
}
```

- [ ] **Step 3: Run to see it fail**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/Chaos.Tests.College/MarkLedgerTests/*"`
Expected: build error, `MarkLedger` not found.

- [ ] **Step 4: Write `MarkLedger`**

`Chaos/Services/College/MarkLedger.cs`:

```csharp
namespace Chaos.Services.College;

/// <summary>Educated Mark rolls and balances. Not thread-safe; <see cref="CollegeService" /> holds the lock.</summary>
public sealed class MarkLedger(CollegeOptions options)
{
    public double ChanceOf(CollegeState state, string name)
    {
        var chance = state.StudentFor(name).RollChance;

        return chance <= 0 ? options.FirstRollChance : chance;
    }

    public bool Roll(CollegeState state, string name, Random random)
    {
        var student = state.StudentFor(name);
        var chance = ChanceOf(state, name);

        if (random.NextDouble() < chance)
        {
            student.Marks++;
            student.LifetimeMarks++;
            student.RollChance = options.FirstRollChance;

            return true;
        }

        //rounded so 0.2 + 0.1 steps land on 0.3, 0.4 ... instead of drifting past 1.0 a step late
        student.RollChance = Math.Min(1.0, Math.Round(chance + options.RollChanceStep, 6));

        return false;
    }

    public void Give(CollegeState state, string name, int amount)
    {
        var student = state.StudentFor(name);
        student.Marks += amount;
        student.LifetimeMarks += amount;
    }

    public bool TryTake(CollegeState state, string name, int amount)
    {
        var student = state.StudentFor(name);

        if ((amount < 0) || (student.Marks < amount))
            return false;

        student.Marks -= amount;

        return true;
    }
}
```

- [ ] **Step 5: Run to see it pass**

Run the Verify command. Expected: 4 tests pass.

---

### Task 2: CollegeRoles

**Goal:** Who may teach, judge and moderate, as pure functions, with tests.

**Files:**
- Create: `Chaos/Services/College/CollegeRoles.cs`
- Test: `Tests/Chaos.Tests/College/CollegeRolesTests.cs`

**Acceptance Criteria:**
- [ ] Village or higher in a subject can teach only that subject; Clave can't teach
- [ ] A Knight can teach a subject until any character holds Village or higher in it
- [ ] Art and Music are separate subjects for teaching
- [ ] Admins and the Director can teach every subject
- [ ] Knights can judge while fewer than 3 characters are Teachers, and not after
- [ ] `CanPost` is true for anyone who can teach any subject

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/Chaos.Tests.College/CollegeRolesTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

`Tests/Chaos.Tests/College/CollegeRolesTests.cs`:

```csharp
#region
using Chaos.Services.College;
using FluentAssertions;
#endregion

namespace Chaos.Tests.College;

public sealed class CollegeRolesTests
{
    [Test]
    [Arguments(AwardTier.Clave, false)]
    [Arguments(AwardTier.Village, true)]
    [Arguments(AwardTier.Kingdom, true)]
    [Arguments(AwardTier.Aisling, true)]
    public void Village_or_higher_teaches_its_own_subject(AwardTier tier, bool expected)
    {
        var state = new CollegeState();
        state.SetAward("Aroha", CollegeSubject.History, tier);

        CollegeRoles.CanTeach(state, "Aroha", CollegeSubject.History, false, false).Should().Be(expected);
        CollegeRoles.CanTeach(state, "Aroha", CollegeSubject.Lore, false, false).Should().BeFalse();
    }

    [Test]
    public void A_knight_teaches_a_subject_until_it_has_a_teacher()
    {
        var state = new CollegeState();
        CollegeRoles.CanTeach(state, "Sir", CollegeSubject.Music, true, false).Should().BeTrue();

        state.SetAward("Aroha", CollegeSubject.Art, AwardTier.Village);
        CollegeRoles.CanTeach(state, "Sir", CollegeSubject.Music, true, false).Should().BeTrue();
        CollegeRoles.CanTeach(state, "Sir", CollegeSubject.Art, true, false).Should().BeFalse();

        state.SetAward("Bea", CollegeSubject.Music, AwardTier.Clave);
        CollegeRoles.CanTeach(state, "Sir", CollegeSubject.Music, true, false).Should().BeTrue();
    }

    [Test]
    public void Admins_and_the_director_teach_everything()
    {
        var state = new CollegeState { Director = "Dee" };

        foreach (var subject in Enum.GetValues<CollegeSubject>())
        {
            CollegeRoles.CanTeach(state, "Admin", subject, false, true).Should().BeTrue();
            CollegeRoles.CanTeach(state, "DEE", subject, false, false).Should().BeTrue();
            CollegeRoles.CanTeach(state, "Nobody", subject, false, false).Should().BeFalse();
        }
    }

    [Test]
    public void Teachable_subjects_lists_only_what_the_player_may_teach()
    {
        var state = new CollegeState();
        state.SetAward("Aroha", CollegeSubject.Lore, AwardTier.Kingdom);

        CollegeRoles.TeachableSubjects(state, "Aroha", false, false).Should().Equal(CollegeSubject.Lore);
        CollegeRoles.CanPost(state, "Aroha", false, false).Should().BeTrue();
        CollegeRoles.CanPost(state, "Nobody", false, false).Should().BeFalse();
    }

    [Test]
    public void Knights_judge_until_there_are_three_teachers()
    {
        var state = new CollegeState();
        state.SetAward("A", CollegeSubject.Art, AwardTier.Village);
        state.SetAward("B", CollegeSubject.Lore, AwardTier.Aisling);
        CollegeRoles.CanJudge(state, "Sir", true, false, 3).Should().BeTrue();

        state.SetAward("C", CollegeSubject.History, AwardTier.Village);
        CollegeRoles.TeacherCount(state).Should().Be(3);
        CollegeRoles.CanJudge(state, "Sir", true, false, 3).Should().BeFalse();
        CollegeRoles.CanJudge(state, "A", false, false, 3).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run the Verify command. Expected: build error, `CollegeRoles` not found.

- [ ] **Step 3: Write `CollegeRoles`**

`Chaos/Services/College/CollegeRoles.cs`:

```csharp
namespace Chaos.Services.College;

public static class CollegeRoles
{
    public static bool IsTeachingTier(AwardTier tier) => tier >= AwardTier.Village;

    public static bool IsDirector(CollegeState state, string name)
        => state.Director is { } director && string.Equals(director, name, StringComparison.OrdinalIgnoreCase);

    public static bool CanModerate(CollegeState state, string name, bool isAdmin) => isAdmin || IsDirector(state, name);

    public static bool HasTeacher(CollegeState state, CollegeSubject subject)
        => state.Awards.Values.Any(awards => awards.TryGetValue(subject.ToString(), out var tier) && IsTeachingTier(tier));

    public static bool CanTeach(CollegeState state, string name, CollegeSubject subject, bool isKnight, bool isAdmin)
        => CanModerate(state, name, isAdmin)
           || IsTeachingTier(state.AwardOf(name, subject))
           || (isKnight && !HasTeacher(state, subject));

    public static IReadOnlyList<CollegeSubject> TeachableSubjects(CollegeState state, string name, bool isKnight, bool isAdmin)
        => Enum.GetValues<CollegeSubject>()
               .Where(subject => CanTeach(state, name, subject, isKnight, isAdmin))
               .ToList();

    public static bool CanPost(CollegeState state, string name, bool isKnight, bool isAdmin)
        => TeachableSubjects(state, name, isKnight, isAdmin).Count > 0;

    public static int TeacherCount(CollegeState state) => state.Awards.Values.Count(awards => awards.Values.Any(IsTeachingTier));

    public static bool CanJudge(CollegeState state, string name, bool isKnight, bool isAdmin, int knightTeacherLimit)
        => CanModerate(state, name, isAdmin)
           || (state.Awards.TryGetValue(CollegeState.Key(name), out var awards) && awards.Values.Any(IsTeachingTier))
           || (isKnight && (TeacherCount(state) < knightTeacherLimit));
}
```

- [ ] **Step 4: Run to see it pass**

Run the Verify command. Expected: all `CollegeRolesTests` pass.

---

### Task 3: Timetable

**Goal:** Booking, cancelling, the walk-in limit and the upcoming list, as pure rules with tests.

**Files:**
- Create: `Chaos/Services/College/Timetable.cs`
- Test: `Tests/Chaos.Tests/College/TimetableTests.cs`

**Acceptance Criteria:**
- [ ] It refuses: a start in the past; more than 14 days ahead; lengths outside 20/30/45/60/90/120; starts not on :00 or :30; the Activity format
- [ ] It refuses overlaps with a booking and with the running class; back-to-back bookings are allowed
- [ ] Booking ids come from `NextBookingId` and increase
- [ ] `WalkInLimit` is 0 while a class runs or while a booking's start has passed but it isn't over; otherwise it's the minutes until the next booking, capped at 120
- [ ] Owners cancel their own bookings; moderators cancel any; others can't

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/Chaos.Tests.College/TimetableTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

`Tests/Chaos.Tests/College/TimetableTests.cs`:

```csharp
#region
using Chaos.Services.College;
using FluentAssertions;
#endregion

namespace Chaos.Tests.College;

public sealed class TimetableTests
{
    private static readonly CollegeOptions Options = new();
    private static readonly DateTime Now = new(2026, 10, 5, 12, 10, 0, DateTimeKind.Utc);
    private static readonly DateTime OneOClock = new(2026, 10, 5, 13, 0, 0, DateTimeKind.Utc);

    private static BookResult Book(CollegeState state, DateTime start, int minutes, ClassFormat format = ClassFormat.Lecture)
        => Timetable.TryBook(state, Options, "Aroha", CollegeSubject.History, format, start, minutes, Now, out _);

    [Test]
    public void A_good_booking_is_stored_with_a_new_id()
    {
        var state = new CollegeState();

        Timetable.TryBook(state, Options, "Aroha", CollegeSubject.History, ClassFormat.Lecture, OneOClock, 45, Now, out var first)
                 .Should().Be(BookResult.Booked);
        Timetable.TryBook(state, Options, "Aroha", CollegeSubject.Lore, ClassFormat.Discussion, OneOClock.AddHours(2), 30, Now, out var second)
                 .Should().Be(BookResult.Booked);

        first!.Id.Should().Be(1);
        second!.Id.Should().Be(2);
        state.Bookings.Should().HaveCount(2);
    }

    [Test]
    public void Bad_bookings_are_refused()
    {
        var state = new CollegeState();

        Book(state, Now.AddMinutes(-10), 30).Should().Be(BookResult.InThePast);
        Book(state, OneOClock.AddDays(15), 30).Should().Be(BookResult.TooFarAhead);
        Book(state, OneOClock, 25).Should().Be(BookResult.BadLength);
        Book(state, OneOClock.AddMinutes(15), 30).Should().Be(BookResult.BadStartTime);
        Book(state, OneOClock, 30, ClassFormat.Activity).Should().Be(BookResult.FormatNotAvailable);
        state.Bookings.Should().BeEmpty();
    }

    [Test]
    public void Overlaps_are_refused_but_back_to_back_is_fine()
    {
        var state = new CollegeState();
        Book(state, OneOClock, 60).Should().Be(BookResult.Booked);

        Book(state, OneOClock.AddMinutes(30), 30).Should().Be(BookResult.Overlaps);
        Book(state, OneOClock.AddMinutes(-30), 60).Should().Be(BookResult.Overlaps);
        Book(state, OneOClock.AddMinutes(60), 30).Should().Be(BookResult.Booked);
        Book(state, OneOClock.AddMinutes(-30), 30).Should().Be(BookResult.Booked);
    }

    [Test]
    public void A_booking_may_not_overlap_the_running_class()
    {
        var state = new CollegeState
        {
            RunningClass = new CollegeClass { StartedUtc = Now.AddMinutes(-10), PlannedMinutes = 120 }
        };

        Book(state, OneOClock, 30).Should().Be(BookResult.Overlaps);
    }

    [Test]
    public void Walk_in_limit_follows_the_next_booking()
    {
        var state = new CollegeState();
        Timetable.WalkInLimit(state, Options, Now).Should().Be(120);

        Book(state, OneOClock, 30);
        Timetable.WalkInLimit(state, Options, Now).Should().Be(50);
        Timetable.WalkInLimit(state, Options, OneOClock.AddMinutes(5)).Should().Be(0);

        state.RunningClass = new CollegeClass { StartedUtc = Now, PlannedMinutes = 20 };
        Timetable.WalkInLimit(state, Options, Now).Should().Be(0);
    }

    [Test]
    public void Owners_and_moderators_cancel()
    {
        var state = new CollegeState();
        Book(state, OneOClock, 30);

        Timetable.TryCancel(state, 1, "Stranger", false).Should().BeFalse();
        Timetable.TryCancel(state, 1, "AROHA", false).Should().BeTrue();
        Book(state, OneOClock, 30);
        Timetable.TryCancel(state, 2, "Stranger", true).Should().BeTrue();
        Timetable.TryCancel(state, 99, "Stranger", true).Should().BeFalse();
        state.Bookings.Should().BeEmpty();
    }

    [Test]
    public void Upcoming_lists_bookings_in_order_within_the_window()
    {
        var state = new CollegeState();
        Book(state, OneOClock.AddDays(3), 30);
        Book(state, OneOClock, 30);
        Book(state, OneOClock.AddDays(10), 30);

        Timetable.Upcoming(state, Now, 7).Select(b => b.StartUtc).Should().Equal(OneOClock, OneOClock.AddDays(3));
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run the Verify command. Expected: build error, `Timetable` not found.

- [ ] **Step 3: Write `Timetable`**

`Chaos/Services/College/Timetable.cs`:

```csharp
namespace Chaos.Services.College;

public static class Timetable
{
    public static BookResult TryBook(
        CollegeState state,
        CollegeOptions options,
        string teacher,
        CollegeSubject subject,
        ClassFormat format,
        DateTime startUtc,
        int minutes,
        DateTime now,
        out CollegeBooking? booking)
    {
        booking = null;

        if (format == ClassFormat.Activity)
            return BookResult.FormatNotAvailable;

        if (startUtc <= now)
            return BookResult.InThePast;

        if (startUtc > now.AddDays(options.BookingDaysAhead))
            return BookResult.TooFarAhead;

        if (!CollegeOptions.Lengths.Contains(minutes))
            return BookResult.BadLength;

        if ((startUtc.Second != 0) || (startUtc.Millisecond != 0) || (startUtc.Minute % 30 != 0))
            return BookResult.BadStartTime;

        var end = startUtc.AddMinutes(minutes);

        if (Overlaps(state, startUtc, end))
            return BookResult.Overlaps;

        booking = new CollegeBooking
        {
            Id = state.NextBookingId++,
            Teacher = teacher,
            Subject = subject,
            Format = format,
            StartUtc = startUtc,
            Minutes = minutes
        };
        state.Bookings.Add(booking);

        return BookResult.Booked;
    }

    public static bool Overlaps(CollegeState state, DateTime start, DateTime end)
    {
        if (state.RunningClass is { } running && (start < running.PlannedEndUtc) && (running.StartedUtc < end))
            return true;

        return state.Bookings.Any(b => (start < b.EndUtc) && (b.StartUtc < end));
    }

    public static bool TryCancel(CollegeState state, int bookingId, string by, bool isModerator)
    {
        var booking = state.Bookings.FirstOrDefault(b => b.Id == bookingId);

        if (booking is null)
            return false;

        if (!isModerator && !string.Equals(booking.Teacher, by, StringComparison.OrdinalIgnoreCase))
            return false;

        state.Bookings.Remove(booking);

        return true;
    }

    /// <summary>Whole minutes a walk-in class may run from now: 0 when a class runs or a booked slot has begun.</summary>
    public static int WalkInLimit(CollegeState state, CollegeOptions options, DateTime now)
    {
        if (state.RunningClass is not null)
            return 0;

        if (state.Bookings.Any(b => (b.StartUtc <= now) && (now < b.EndUtc)))
            return 0;

        var next = state.Bookings.Where(b => b.StartUtc > now)
                        .Select(b => (DateTime?)b.StartUtc)
                        .Min();

        var limit = next is null ? options.MaximumClassMinutes : (int)Math.Floor((next.Value - now).TotalMinutes);

        return Math.Clamp(limit, 0, options.MaximumClassMinutes);
    }

    public static IReadOnlyList<CollegeBooking> Upcoming(CollegeState state, DateTime now, int days)
        => state.Bookings.Where(b => (b.EndUtc > now) && (b.StartUtc < now.AddDays(days)))
                .OrderBy(b => b.StartUtc)
                .ToList();
}
```

- [ ] **Step 4: Run to see it pass**

Run the Verify command. Expected: all `TimetableTests` pass.

---

### Task 4: MachineKeys and ClassSession

**Goal:** The per-computer key, presence recording and the end-of-class check, with tests.

**Files:**
- Create: `Chaos/Services/College/MachineKeys.cs`
- Create: `Chaos/Services/College/ClassSession.cs`
- Test: `Tests/Chaos.Tests/College/ClassSessionTests.cs`

**Acceptance Criteria:**
- [ ] `MachineKeys.For(0xFF00FF00, "1.2.3.4")` and `MachineKeys.For(null, ...)` return `ip:1.2.3.4`; any other id returns the id as text
- [ ] A class shorter than 20 minutes ends `CalledOff`
- [ ] A Teacher under 80% presence ends `TeacherAbsent`
- [ ] Students qualify at 80% presence or more, with one sampling interval of slack; only the character with the most seconds on each machine counts; the Teacher's machine and removed students never count
- [ ] Fewer than 3 qualifying students ends `TooFewStudents`; otherwise `Completed` with the qualified names
- [ ] `CanEndEarly` is false before 20 minutes and true at 20

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/Chaos.Tests.College/ClassSessionTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

`Tests/Chaos.Tests/College/ClassSessionTests.cs`:

```csharp
#region
using Chaos.Services.College;
using FluentAssertions;
#endregion

namespace Chaos.Tests.College;

public sealed class ClassSessionTests
{
    private static readonly CollegeOptions Options = new();
    private static readonly DateTime Start = new(2026, 10, 5, 13, 0, 0, DateTimeKind.Utc);

    private static CollegeClass NewClass()
        => ClassSession.Start("Teach", "m-teach", CollegeSubject.Lore, ClassFormat.Lecture, "college_codex", Start, 60, null);

    private static void Run(CollegeClass session, int minutes, params (string Name, string Machine)[] people)
    {
        for (var t = 30; t <= minutes * 60; t += 30)
            ClassSession.RecordPresence(
                session,
                people.Select(p => new PresenceSample(p.Name, p.Machine)).ToList(),
                30,
                Start.AddSeconds(t));
    }

    [Test]
    public void Machine_keys_fall_back_to_the_ip_for_the_default_id()
    {
        MachineKeys.For(0xFF00FF00, "1.2.3.4").Should().Be("ip:1.2.3.4");
        MachineKeys.For(null, "1.2.3.4").Should().Be("ip:1.2.3.4");
        MachineKeys.For(12345, "1.2.3.4").Should().Be("12345");
    }

    [Test]
    public void Three_students_on_three_machines_complete_the_class()
    {
        var session = NewClass();
        Run(session, 20, ("Teach", "m-teach"), ("A", "m1"), ("B", "m2"), ("C", "m3"));

        var result = ClassSession.Finish(session, Start.AddMinutes(20), Options);

        result.Outcome.Should().Be(ClassOutcome.Completed);
        result.Qualified.Should().BeEquivalentTo("A", "B", "C");
        result.Minutes.Should().Be(20);
    }

    [Test]
    public void Before_twenty_minutes_the_class_is_called_off()
    {
        var session = NewClass();
        Run(session, 19, ("Teach", "m-teach"), ("A", "m1"), ("B", "m2"), ("C", "m3"));

        ClassSession.CanEndEarly(session, Start.AddMinutes(19), Options).Should().BeFalse();
        ClassSession.CanEndEarly(session, Start.AddMinutes(20), Options).Should().BeTrue();
        ClassSession.Finish(session, Start.AddMinutes(19), Options).Outcome.Should().Be(ClassOutcome.CalledOff);
    }

    [Test]
    public void A_teacher_below_eighty_percent_fails_the_class()
    {
        var session = NewClass();
        Run(session, 10, ("Teach", "m-teach"), ("A", "m1"), ("B", "m2"), ("C", "m3"));
        for (var t = 630; t <= 1800; t += 30)
            ClassSession.RecordPresence(session, [new("A", "m1"), new("B", "m2"), new("C", "m3")], 30, Start.AddSeconds(t));

        ClassSession.Finish(session, Start.AddMinutes(30), Options).Outcome.Should().Be(ClassOutcome.TeacherAbsent);
    }

    [Test]
    public void One_character_per_machine_and_never_the_teachers_machine()
    {
        var session = NewClass();
        Run(session, 20, ("Teach", "m-teach"), ("A", "m1"), ("A2", "m1"), ("B", "m2"), ("Alt", "m-teach"));
        ClassSession.RecordPresence(session, [new("A2", "m1")], 30, Start.AddMinutes(20));

        var result = ClassSession.Finish(session, Start.AddMinutes(20), Options);

        result.Outcome.Should().Be(ClassOutcome.TooFewStudents);
        result.Qualified.Should().BeEquivalentTo("A2", "B");
    }

    [Test]
    public void Eighty_percent_with_one_interval_of_slack_qualifies_and_removed_students_do_not()
    {
        var session = NewClass();
        Run(session, 20, ("Teach", "m-teach"), ("A", "m1"), ("B", "m2"));
        for (var t = 30; t <= 15 * 60 + 30; t += 30)
            ClassSession.RecordPresence(session, [new("Late", "m3")], 30, Start.AddSeconds(t));
        for (var t = 30; t <= 14 * 60; t += 30)
            ClassSession.RecordPresence(session, [new("Short", "m4")], 30, Start.AddSeconds(t));
        Run(session, 20, ("Gone", "m5"));
        session.Removed.Add("gone");

        var result = ClassSession.Finish(session, Start.AddMinutes(20), Options);

        result.Outcome.Should().Be(ClassOutcome.Completed);
        result.Qualified.Should().BeEquivalentTo("A", "B", "Late");
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run the Verify command. Expected: build error, `ClassSession` not found.

- [ ] **Step 3: Write `MachineKeys` and `ClassSession`**

`Chaos/Services/College/MachineKeys.cs`:

```csharp
namespace Chaos.Services.College;

public static class MachineKeys
{
    /// <summary>What Chaos.Client's MachineIdentity sends when it can't read the machine; shared by strangers.</summary>
    public const uint DEFAULT_CLIENT_ID1 = 0xFF00FF00;

    public static string For(uint? loginId1, string ip)
        => loginId1 is null or DEFAULT_CLIENT_ID1 ? $"ip:{ip}" : loginId1.Value.ToString();
}
```

`Chaos/Services/College/ClassSession.cs`:

```csharp
namespace Chaos.Services.College;

public readonly record struct PresenceSample(string Name, string MachineKey);

public sealed record ClassResult(ClassOutcome Outcome, IReadOnlyList<string> Qualified, int Minutes);

public static class ClassSession
{
    public static CollegeClass Start(
        string teacher,
        string teacherMachine,
        CollegeSubject subject,
        ClassFormat format,
        string room,
        DateTime now,
        int plannedMinutes,
        int? bookingId)
        => new()
        {
            BookingId = bookingId,
            Teacher = teacher,
            TeacherMachine = teacherMachine,
            Subject = subject,
            Format = format,
            Room = room,
            StartedUtc = now,
            PlannedMinutes = plannedMinutes,
            TeacherLastSeenUtc = now
        };

    public static void RecordPresence(CollegeClass session, IReadOnlyList<PresenceSample> samples, int seconds, DateTime now)
    {
        foreach (var sample in samples)
        {
            var key = CollegeState.Key(sample.Name);

            if (string.Equals(sample.Name, session.Teacher, StringComparison.OrdinalIgnoreCase))
            {
                session.TeacherSeconds += seconds;
                session.TeacherLastSeenUtc = now;

                continue;
            }

            if (session.Removed.Contains(key))
                continue;

            if (!session.Presence.TryGetValue(key, out var presence))
                session.Presence[key] = presence = new CollegePresence { Name = sample.Name };

            presence.MachineKey = sample.MachineKey;
            presence.Seconds += seconds;
        }
    }

    public static bool CanEndEarly(CollegeClass session, DateTime now, CollegeOptions options)
        => now - session.StartedUtc >= TimeSpan.FromMinutes(options.MinimumClassMinutes);

    public static ClassResult Finish(CollegeClass session, DateTime now, CollegeOptions options)
    {
        var elapsed = now - session.StartedUtc;
        var minutes = (int)Math.Floor(elapsed.TotalMinutes);

        if (elapsed < TimeSpan.FromMinutes(options.MinimumClassMinutes))
            return new ClassResult(ClassOutcome.CalledOff, [], minutes);

        //one sampling interval of slack: the first sample lands up to a tick after the class starts
        var needed = (elapsed.TotalSeconds * options.PresenceShare) - options.TickSeconds;

        if (session.TeacherSeconds < needed)
            return new ClassResult(ClassOutcome.TeacherAbsent, [], minutes);

        var qualified = session.Presence.Values
                               .Where(p => p.Seconds >= needed)
                               .Where(p => !session.Removed.Contains(CollegeState.Key(p.Name)))
                               .Where(p => !string.Equals(p.MachineKey, session.TeacherMachine, StringComparison.Ordinal))
                               .GroupBy(p => p.MachineKey)
                               .Select(g => g.OrderByDescending(p => p.Seconds)
                                             .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                                             .First()
                                             .Name)
                               .ToList();

        return qualified.Count < options.MinimumStudents
            ? new ClassResult(ClassOutcome.TooFewStudents, qualified, minutes)
            : new ClassResult(ClassOutcome.Completed, qualified, minutes);
    }
}
```

- [ ] **Step 4: Run to see it pass**

Run the Verify command. Expected: all `ClassSessionTests` pass.

---

### Task 5: CollegeService, notifier interface, tick service, DI

**Goal:** The locked entry point, which turns the rule units into actions: booking, starting and ending classes, presence, the tick, the shop balance and admin edits. It's registered in DI with a tick service.

**Files:**
- Create: `Chaos/Services/College/ICollegeNotifier.cs`
- Create: `Chaos/Services/College/CollegeText.cs`
- Create: `Chaos/Services/College/CollegeService.cs`
- Create: `Chaos/Services/College/CollegeTickService.cs`
- Test: `Tests/Chaos.Tests/College/CollegeServiceTests.cs`

(DI registration is in Task 6, because it needs `CollegeNotifier`.)

**Acceptance Criteria:**
- [ ] A booked class starts only for its Teacher, in its subject's room, from 10 minutes before the start to 10 minutes after; starting it removes the booking
- [ ] A walk-in needs `CanTeach`, no running class and a `WalkInLimit` of at least 20
- [ ] `EndClass` refuses before 20 minutes with `TooSoon`; at 20 or later it finishes and pays out
- [ ] Payout on `Completed`: each qualified student gets one roll, the Teacher gets 1 mark, and `RefreshEducatedMark` is called for everyone whose balance changed; nothing is paid for other outcomes
- [ ] `Tick` ends a class at its planned end, ends it after 10 minutes without the Teacher, and drops bookings 10 minutes past their start, telling the Teacher
- [ ] `TryBuy` leaves the balance unchanged when it fails
- [ ] Every state change calls `IStorage<CollegeState>.Save()`
- [ ] The server project builds

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/Chaos.Tests.College/*/*"` → all College tests pass

**Steps:**

- [ ] **Step 1: Write the notifier interface and text helpers**

`Chaos/Services/College/ICollegeNotifier.cs`:

```csharp
namespace Chaos.Services.College;

/// <summary>The world side of the College: orange bars and the Educated legend mark. Missing players are skipped.</summary>
public interface ICollegeNotifier
{
    void Broadcast(string message);
    void Tell(string name, string message);
    void RefreshEducatedMark(string name, int marks);
}
```

`Chaos/Services/College/CollegeText.cs`:

```csharp
using System.Globalization;

namespace Chaos.Services.College;

public static class CollegeText
{
    public const string ROLL_WON = "You earned an Educated Mark!";
    public const string ROLL_MISSED = "No mark this time. Better luck next class.";
    public const string TEACHER_PAID = "You earned an Educated Mark for teaching.";
    public const string CALLED_OFF = "Class called off before 20 minutes.";
    public const string TOO_FEW = "Class ended: fewer than 3 students stayed.";
    public const string TEACHER_ABSENT = "Class ended: the Teacher was away too long.";
    public const string BOOKING_EXPIRED = "Your class booking expired.";

    public static string SubjectName(CollegeSubject subject) => subject.ToString();

    /// <summary>Under 45 characters for the orange bar; the Teacher's name is dropped when it would not fit.</summary>
    public static string ClassBegun(CollegeSubject subject, string teacher)
    {
        var full = $"A {SubjectName(subject)} class with {teacher} has begun!";

        return full.Length <= 45 ? full : $"A {SubjectName(subject)} class has begun at the College!";
    }

    public static string Outcome(ClassOutcome outcome)
        => outcome switch
        {
            ClassOutcome.CalledOff     => CALLED_OFF,
            ClassOutcome.TeacherAbsent => TEACHER_ABSENT,
            ClassOutcome.TooFewStudents => TOO_FEW,
            _                          => "Class complete. Well studied!"
        };

    /// <summary>"Tue 14:00 UTC (in 5h)".</summary>
    public static string When(DateTime startUtc, DateTime now)
    {
        var until = startUtc - now;
        var relative = until.TotalMinutes < 1
            ? "now"
            : until.TotalHours < 1
                ? $"in {(int)until.TotalMinutes}m"
                : until.TotalDays < 1
                    ? $"in {(int)until.TotalHours}h"
                    : $"in {(int)until.TotalDays}d";

        return $"{startUtc.ToString("ddd HH:mm", CultureInfo.InvariantCulture)} UTC ({relative})";
    }

    public static string TimetableLine(CollegeBooking booking, DateTime now)
        => $"{When(booking.StartUtc, now)} {SubjectName(booking.Subject)} - {booking.Teacher}, {booking.Minutes}m";
}
```

- [ ] **Step 2: Write the failing service tests**

`Tests/Chaos.Tests/College/CollegeServiceTests.cs`. Use the existing `FixedTime` test clock (it's in the test project and used by `TownTestSupport`; find it with Serena `find_symbol FixedTime`). Its API moves time forward; check its member names before using them. The examples below assume `time.Advance(TimeSpan)`; if the method is named differently, use the real name.

```csharp
#region
using Chaos.Services.College;
using Chaos.Storage.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
#endregion

namespace Chaos.Tests.College;

public sealed class CollegeServiceTests
{
    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }

    private static readonly DateTimeOffset Noon = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static CollegeService Create(
        out CollegeState state,
        out FixedTime time,
        out Mock<ICollegeNotifier> notifier,
        out Mock<IStorage<CollegeState>> storage,
        double roll = 0.0)
    {
        state = new CollegeState();
        time = new FixedTime(Noon);
        notifier = new Mock<ICollegeNotifier>();
        storage = new Mock<IStorage<CollegeState>>();
        storage.SetupGet(s => s.Value).Returns(state);

        return new CollegeService(
            storage.Object,
            Options.Create(new CollegeOptions()),
            notifier.Object,
            time,
            NullLogger<CollegeService>.Instance) { Random = new FixedRandom(roll) };
    }

    private static void Attend(CollegeService service, int minutes, FixedTime time, params (string Name, string Machine)[] people)
    {
        for (var t = 0; t < minutes * 2; t++)
        {
            time.Advance(TimeSpan.FromSeconds(30));
            service.RecordPresence("college_codex", people.Select(p => new PresenceSample(p.Name, p.Machine)).ToList());
        }
    }

    [Test]
    public void A_completed_walk_in_pays_students_and_the_teacher()
    {
        var service = Create(out var state, out var time, out var notifier, out var storage);

        service.StartWalkIn("Sir", "m0", CollegeSubject.Lore, ClassFormat.Lecture, "college_codex", true, false)
               .Result.Should().Be(StartResult.Started);
        Attend(service, 20, time, ("Sir", "m0"), ("A", "m1"), ("B", "m2"), ("C", "m3"));

        service.EndClass("Sir", false).Should().Be(EndResult.Ended);

        state.RunningClass.Should().BeNull();
        state.StudentFor("A").Marks.Should().Be(1);
        state.StudentFor("Sir").Marks.Should().Be(1);
        notifier.Verify(n => n.Tell("Sir", CollegeText.TEACHER_PAID), Times.Once);
        notifier.Verify(n => n.RefreshEducatedMark("A", 1), Times.Once);
        notifier.Verify(n => n.Broadcast(It.Is<string>(m => m.Contains("Lore"))), Times.Once);
        storage.Verify(s => s.Save(), Times.AtLeastOnce);
        state.History.Should().ContainSingle().Which.Outcome.Should().Be(ClassOutcome.Completed);
    }

    [Test]
    public void Ending_before_twenty_minutes_is_refused()
    {
        var service = Create(out var state, out var time, out _, out _);
        service.StartWalkIn("Sir", "m0", CollegeSubject.Lore, ClassFormat.Lecture, "college_codex", true, false);
        Attend(service, 10, time, ("Sir", "m0"));

        service.EndClass("Sir", false).Should().Be(EndResult.TooSoon);
        service.EndClass("Someone", false).Should().Be(EndResult.NotYourClass);
        state.RunningClass.Should().NotBeNull();
    }

    [Test]
    public void Walk_ins_need_a_teacher_the_right_room_and_a_free_slot()
    {
        var service = Create(out var state, out var time, out _, out _);

        service.StartWalkIn("Nobody", "m0", CollegeSubject.Lore, ClassFormat.Lecture, "college_codex", false, false)
               .Result.Should().Be(StartResult.NotAllowed);
        service.StartWalkIn("Sir", "m0", CollegeSubject.Lore, ClassFormat.Lecture, "college_quills", true, false)
               .Result.Should().Be(StartResult.WrongRoom);

        service.Book("Sir", CollegeSubject.History, ClassFormat.Lecture, Noon.UtcDateTime.AddMinutes(30), 30, true, false)
               .Should().Be(BookResult.Booked);
        time.Advance(TimeSpan.FromMinutes(15));
        service.StartWalkIn("Sir", "m0", CollegeSubject.Lore, ClassFormat.Lecture, "college_codex", true, false)
               .Result.Should().Be(StartResult.NoTimeBeforeNextBooking);
        state.RunningClass.Should().BeNull();
    }

    [Test]
    public void A_booked_class_starts_in_its_window_and_removes_the_booking()
    {
        var service = Create(out var state, out var time, out _, out _);
        service.Book("Sir", CollegeSubject.Lore, ClassFormat.Discussion, Noon.UtcDateTime.AddHours(1), 45, true, false);
        var id = state.Bookings.Single().Id;

        service.StartBooked("Sir", "m0", id, "college_codex", true, false).Result.Should().Be(StartResult.TooEarly);
        time.Advance(TimeSpan.FromMinutes(50));
        service.StartBooked("Other", "m9", id, "college_codex", true, false).Result.Should().Be(StartResult.NotYourBooking);
        service.StartBooked("Sir", "m0", id, "college_codex", true, false).Result.Should().Be(StartResult.Started);

        state.Bookings.Should().BeEmpty();
        state.RunningClass!.PlannedMinutes.Should().Be(55);
    }

    [Test]
    public void Tick_expires_no_shows_and_ends_classes()
    {
        var service = Create(out var state, out var time, out var notifier, out _);
        service.Book("Sir", CollegeSubject.Lore, ClassFormat.Lecture, Noon.UtcDateTime.AddMinutes(30), 30, true, false);
        time.Advance(TimeSpan.FromMinutes(41));
        service.Tick();
        state.Bookings.Should().BeEmpty();
        notifier.Verify(n => n.Tell("Sir", CollegeText.BOOKING_EXPIRED), Times.Once);

        service.StartWalkIn("Sir", "m0", CollegeSubject.Lore, ClassFormat.Lecture, "college_codex", true, false);
        Attend(service, 5, time, ("Sir", "m0"));
        time.Advance(TimeSpan.FromMinutes(11));
        service.Tick();
        state.RunningClass.Should().BeNull();
        state.History.Last().Outcome.Should().Be(ClassOutcome.CalledOff);
    }

    [Test]
    public void A_failed_purchase_leaves_the_balance_alone()
    {
        var service = Create(out var state, out _, out _, out _);
        service.GiveMarks("A", 2);

        service.TryBuy("A", 3).Should().BeFalse();
        state.StudentFor("A").Marks.Should().Be(2);
        service.TryBuy("A", 2).Should().BeTrue();
        service.MarksOf("A").Should().Be(0);
    }
}
```

- [ ] **Step 3: Run to see it fail**

Run the Verify command. Expected: build error, `CollegeService` not found.

- [ ] **Step 4: Write `CollegeService`**

`Chaos/Services/College/CollegeService.cs`. All public members take the `Sync` lock. The `*Locked` helpers assume it's held. `Now` is `Time.GetUtcNow().UtcDateTime`.

```csharp
using Chaos.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chaos.Services.College;

public sealed record StartInfo(StartResult Result, int Minutes);

public sealed class CollegeService
{
    private readonly ILogger<CollegeService> Logger;
    private readonly MarkLedger Ledger;
    private readonly ICollegeNotifier Notifier;
    private readonly CollegeState State;
    private readonly IStorage<CollegeState> Storage;
    private readonly Lock Sync = new();
    private readonly TimeProvider Time;

    public CollegeOptions Options { get; }
    public Random Random { get; init; } = Random.Shared;

    public CollegeService(
        IStorage<CollegeState> storage,
        IOptions<CollegeOptions> options,
        ICollegeNotifier notifier,
        TimeProvider time,
        ILogger<CollegeService> logger)
    {
        Storage = storage;
        State = storage.Value;
        Options = options.Value;
        Ledger = new MarkLedger(Options);
        Notifier = notifier;
        Time = time;
        Logger = logger;
    }

    private DateTime Now => Time.GetUtcNow().UtcDateTime;

    private void SaveLocked() => Storage.Save();

    // ---- roles ----

    public bool CanTeach(string name, CollegeSubject subject, bool isKnight, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        return CollegeRoles.CanTeach(State, name, subject, isKnight, isAdmin);
    }

    public IReadOnlyList<CollegeSubject> TeachableSubjects(string name, bool isKnight, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        return CollegeRoles.TeachableSubjects(State, name, isKnight, isAdmin);
    }

    public bool CanPost(string name, bool isKnight, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        return CollegeRoles.CanPost(State, name, isKnight, isAdmin);
    }

    public bool CanModerate(string name, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        return CollegeRoles.CanModerate(State, name, isAdmin);
    }

    public AwardTier AwardOf(string name, CollegeSubject subject)
    {
        using var scope = Sync.EnterScope();

        return State.AwardOf(name, subject);
    }

    // ---- timetable ----

    public BookResult Book(string teacher, CollegeSubject subject, ClassFormat format, DateTime startUtc, int minutes, bool isKnight, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        if (!CollegeRoles.CanTeach(State, teacher, subject, isKnight, isAdmin))
            return BookResult.NotAllowed;

        var result = Timetable.TryBook(State, Options, teacher, subject, format, startUtc, minutes, Now, out _);

        if (result == BookResult.Booked)
            SaveLocked();

        return result;
    }

    public bool Cancel(int bookingId, string by, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        var cancelled = Timetable.TryCancel(State, bookingId, by, CollegeRoles.CanModerate(State, by, isAdmin));

        if (cancelled)
            SaveLocked();

        return cancelled;
    }

    /// <summary>Copies, so dialogs can show them without holding the lock.</summary>
    public IReadOnlyList<CollegeBooking> Upcoming(int days)
    {
        using var scope = Sync.EnterScope();

        return Timetable.Upcoming(State, Now, days).Select(Copy).ToList();
    }

    public int WalkInLimit()
    {
        using var scope = Sync.EnterScope();

        return Timetable.WalkInLimit(State, Options, Now);
    }

    // ---- classes ----

    public StartInfo StartWalkIn(string teacher, string machineKey, CollegeSubject subject, ClassFormat format, string roomInstanceId, bool isKnight, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        if (format == ClassFormat.Activity)
            return new StartInfo(StartResult.FormatNotAvailable, 0);

        if (!CollegeRoles.CanTeach(State, teacher, subject, isKnight, isAdmin))
            return new StartInfo(StartResult.NotAllowed, 0);

        if (!string.Equals(Options.RoomFor(subject), roomInstanceId, StringComparison.OrdinalIgnoreCase))
            return new StartInfo(StartResult.WrongRoom, 0);

        if (State.RunningClass is not null)
            return new StartInfo(StartResult.ClassRunning, 0);

        var limit = Timetable.WalkInLimit(State, Options, Now);

        if (limit < Options.MinimumClassMinutes)
            return new StartInfo(StartResult.NoTimeBeforeNextBooking, limit);

        BeginLocked(teacher, machineKey, subject, format, roomInstanceId, limit, null);

        return new StartInfo(StartResult.Started, limit);
    }

    public StartInfo StartBooked(string teacher, string machineKey, int bookingId, string roomInstanceId, bool isKnight, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        var booking = State.Bookings.FirstOrDefault(b => b.Id == bookingId);

        if (booking is null)
            return new StartInfo(StartResult.NoSuchBooking, 0);

        if (!string.Equals(booking.Teacher, teacher, StringComparison.OrdinalIgnoreCase))
            return new StartInfo(StartResult.NotYourBooking, 0);

        if (!CollegeRoles.CanTeach(State, teacher, booking.Subject, isKnight, isAdmin))
            return new StartInfo(StartResult.NotAllowed, 0);

        if (!string.Equals(Options.RoomFor(booking.Subject), roomInstanceId, StringComparison.OrdinalIgnoreCase))
            return new StartInfo(StartResult.WrongRoom, 0);

        if (State.RunningClass is not null)
            return new StartInfo(StartResult.ClassRunning, 0);

        if (Now < booking.StartUtc.AddMinutes(-Options.StartEarlyMinutes))
            return new StartInfo(StartResult.TooEarly, 0);

        State.Bookings.Remove(booking);

        //a late start keeps the booked end, so the next booking is never overrun
        var minutes = Math.Max(Options.MinimumClassMinutes, (int)Math.Floor((booking.EndUtc - Now).TotalMinutes));
        BeginLocked(teacher, machineKey, booking.Subject, booking.Format, roomInstanceId, minutes, booking.Id);

        return new StartInfo(StartResult.Started, minutes);
    }

    private void BeginLocked(string teacher, string machineKey, CollegeSubject subject, ClassFormat format, string room, int minutes, int? bookingId)
    {
        State.RunningClass = ClassSession.Start(teacher, machineKey, subject, format, room, Now, minutes, bookingId);
        SaveLocked();
        Notifier.Broadcast(CollegeText.ClassBegun(subject, teacher));
    }

    public EndResult EndClass(string by, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        if (State.RunningClass is not { } running)
            return EndResult.NoClass;

        if (!string.Equals(running.Teacher, by, StringComparison.OrdinalIgnoreCase) && !CollegeRoles.CanModerate(State, by, isAdmin))
            return EndResult.NotYourClass;

        if (!ClassSession.CanEndEarly(running, Now, Options))
            return EndResult.TooSoon;

        FinishLocked();

        return EndResult.Ended;
    }

    /// <summary>The admin command's end: the normal end-of-class check, at any time.</summary>
    public ClassResult? ForceEnd()
    {
        using var scope = Sync.EnterScope();

        return State.RunningClass is null ? null : FinishLocked();
    }

    public bool RemoveStudent(string teacher, string name, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        if (State.RunningClass is not { } running)
            return false;

        if (!string.Equals(running.Teacher, teacher, StringComparison.OrdinalIgnoreCase) && !CollegeRoles.CanModerate(State, teacher, isAdmin))
            return false;

        var key = CollegeState.Key(name);

        if (!running.Removed.Contains(key))
            running.Removed.Add(key);

        running.Presence.Remove(key);
        SaveLocked();

        return true;
    }

    public void RecordPresence(string roomInstanceId, IReadOnlyList<PresenceSample> samples)
    {
        using var scope = Sync.EnterScope();

        if (State.RunningClass is not { } running || !string.Equals(running.Room, roomInstanceId, StringComparison.OrdinalIgnoreCase))
            return;

        ClassSession.RecordPresence(running, samples, Options.TickSeconds, Now);
    }

    /// <summary>A snapshot of the running class for dialogs, or null.</summary>
    public CollegeClass? Running()
    {
        using var scope = Sync.EnterScope();

        return State.RunningClass is { } running ? Copy(running) : null;
    }

    public void Tick()
    {
        using var scope = Sync.EnterScope();

        var now = Now;

        foreach (var expired in State.Bookings.Where(b => now > b.StartUtc.AddMinutes(Options.StartGraceMinutes)).ToList())
        {
            State.Bookings.Remove(expired);
            Notifier.Tell(expired.Teacher, CollegeText.BOOKING_EXPIRED);
        }

        if (State.RunningClass is { } running
            && ((now >= running.PlannedEndUtc) || (now - running.TeacherLastSeenUtc >= TimeSpan.FromMinutes(Options.TeacherAbsenceMinutes))))
            FinishLocked();

        SaveLocked();
    }

    private ClassResult FinishLocked()
    {
        var running = State.RunningClass!;
        var result = ClassSession.Finish(running, Now, Options);
        var won = new List<string>();

        if (result.Outcome == ClassOutcome.Completed)
        {
            foreach (var name in result.Qualified)
            {
                var success = Ledger.Roll(State, name, Random);

                if (success)
                {
                    won.Add(name);
                    Notifier.RefreshEducatedMark(name, State.StudentFor(name).Marks);
                }

                Notifier.Tell(name, success ? CollegeText.ROLL_WON : CollegeText.ROLL_MISSED);
            }

            Ledger.Give(State, running.Teacher, 1);
            Notifier.RefreshEducatedMark(running.Teacher, State.StudentFor(running.Teacher).Marks);
            Notifier.Tell(running.Teacher, CollegeText.TEACHER_PAID);
        } else
        {
            var message = CollegeText.Outcome(result.Outcome);
            Notifier.Tell(running.Teacher, message);

            foreach (var presence in running.Presence.Values)
                Notifier.Tell(presence.Name, message);
        }

        State.History.Add(
            new CollegeClassRecord
            {
                Teacher = running.Teacher,
                Subject = running.Subject,
                StartedUtc = running.StartedUtc,
                Minutes = result.Minutes,
                Outcome = result.Outcome,
                Qualified = result.Qualified.ToList(),
                Won = won
            });

        if (State.History.Count > Options.HistoryLength)
            State.History.RemoveRange(0, State.History.Count - Options.HistoryLength);

        State.RunningClass = null;
        SaveLocked();
        Logger.LogInformation("College class by {Teacher} ended {Outcome} after {Minutes}m", running.Teacher, result.Outcome, result.Minutes);

        return result;
    }

    // ---- marks ----

    public int MarksOf(string name)
    {
        using var scope = Sync.EnterScope();

        return State.Students.TryGetValue(CollegeState.Key(name), out var student) ? student.Marks : 0;
    }

    public CollegeStudent StudentSnapshot(string name)
    {
        using var scope = Sync.EnterScope();

        var student = State.Students.GetValueOrDefault(CollegeState.Key(name));

        return new CollegeStudent
        {
            Name = student?.Name ?? name,
            Marks = student?.Marks ?? 0,
            LifetimeMarks = student?.LifetimeMarks ?? 0,
            RollChance = student is { RollChance: > 0 } ? student.RollChance : Options.FirstRollChance,
            FreeEntries = student?.FreeEntries ?? 0
        };
    }

    public bool TryBuy(string name, int cost)
    {
        using var scope = Sync.EnterScope();

        if (!Ledger.TryTake(State, name, cost))
            return false;

        SaveLocked();
        Notifier.RefreshEducatedMark(name, State.StudentFor(name).Marks);

        return true;
    }

    /// <summary>Gives back a purchase that could not be delivered. It doesn't count toward lifetime marks.</summary>
    public void Refund(string name, int cost)
    {
        using var scope = Sync.EnterScope();

        State.StudentFor(name).Marks += cost;
        SaveLocked();
        Notifier.RefreshEducatedMark(name, State.StudentFor(name).Marks);
    }

    public void GiveMarks(string name, int amount)
    {
        using var scope = Sync.EnterScope();

        Ledger.Give(State, name, amount);
        SaveLocked();
        Notifier.RefreshEducatedMark(name, State.StudentFor(name).Marks);
    }

    public bool TakeMarks(string name, int amount)
    {
        using var scope = Sync.EnterScope();

        if (!Ledger.TryTake(State, name, amount))
            return false;

        SaveLocked();
        Notifier.RefreshEducatedMark(name, State.StudentFor(name).Marks);

        return true;
    }

    // ---- admin ----

    public void SetDirector(string? name)
    {
        using var scope = Sync.EnterScope();

        State.Director = name;
        SaveLocked();
    }

    public void SetAward(string name, CollegeSubject subject, AwardTier tier)
    {
        using var scope = Sync.EnterScope();

        State.SetAward(name, subject, tier);
        SaveLocked();
    }

    public string? Director()
    {
        using var scope = Sync.EnterScope();

        return State.Director;
    }

    public IReadOnlyDictionary<CollegeSubject, int> TeacherCounts()
    {
        using var scope = Sync.EnterScope();

        return Enum.GetValues<CollegeSubject>()
                   .ToDictionary(
                       s => s,
                       s => State.Awards.Values.Count(a => a.TryGetValue(s.ToString(), out var t) && CollegeRoles.IsTeachingTier(t)));
    }

    private static CollegeBooking Copy(CollegeBooking b)
        => new() { Id = b.Id, Teacher = b.Teacher, Subject = b.Subject, Format = b.Format, StartUtc = b.StartUtc, Minutes = b.Minutes };

    private static CollegeClass Copy(CollegeClass c)
        => new()
        {
            BookingId = c.BookingId,
            Teacher = c.Teacher,
            TeacherMachine = c.TeacherMachine,
            Subject = c.Subject,
            Format = c.Format,
            Room = c.Room,
            StartedUtc = c.StartedUtc,
            PlannedMinutes = c.PlannedMinutes,
            TeacherSeconds = c.TeacherSeconds,
            TeacherLastSeenUtc = c.TeacherLastSeenUtc,
            Presence = c.Presence.ToDictionary(
                kv => kv.Key,
                kv => new CollegePresence { Name = kv.Value.Name, MachineKey = kv.Value.MachineKey, Seconds = kv.Value.Seconds }),
            Removed = c.Removed.ToList()
        };
}
```

- [ ] **Step 5: Write the tick service**

`Chaos/Services/College/CollegeTickService.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chaos.Services.College;

public sealed class CollegeTickService(CollegeService service, IOptions<CollegeOptions> options, ILogger<CollegeTickService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, options.Value.TickSeconds)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
            try
            {
                service.Tick();
            } catch (Exception e)
            {
                logger.LogError(e, "College tick failed");
            }
    }
}
```

- [ ] **Step 6: (moved) DI registration is done in Task 6 Step 6, with this code**

In `Chaos/Extensions/ServiceCollectionExtensions.cs`, add this method after `AddTowns` (use Serena `insert_after_symbol` on `ServiceCollectionExtensions/AddTowns`):

```csharp
    public static void AddCollege(this IServiceCollection services)
    {
        services.AddOptionsFromConfig<CollegeOptions>(ConfigKeys.Options.Key);
        services.AddSingleton<ICollegeNotifier, CollegeNotifier>();
        services.AddSingleton<CollegeService>();
        services.AddHostedService<CollegeTickService>();
    }
```

Add `using Chaos.Services.College;` at the top of the file. In `Chaos/Program.cs`, find the `AddTowns()` call (around line 201) and add `services.AddCollege();` on the next line. Write it the same way that call is written; if it's `builder.Services.AddTowns()`, follow that shape.

Task 5 doesn't make this change; Task 6 does, once `CollegeNotifier` exists. Task 5's tests don't need DI.

- [ ] **Step 7: Run to see it pass**

Run the Verify command. Expected: every College test passes, including `CollegeServiceTests`.

---

### Task 6: Notifier, login refresh, room map script, DI wiring

**Goal:** The world side. Orange bars and the "Educated" legend mark reach online players. Login refreshes the mark. Each subject room reports who is present every 30 seconds. `AddCollege()` is registered and called.

**Files:**
- Create: `Chaos/Services/College/CollegeNotifier.cs`
- Create: `Chaos/Scripting/MapScripts/Temuair/College/CollegeRoomScript.cs`
- Modify: `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` (constructor injection plus `OnLogin`)
- Modify: `Chaos/Extensions/ServiceCollectionExtensions.cs` (add `AddCollege()`), `Chaos/Program.cs` (call it)
- Test: `Tests/Chaos.Tests/College/CollegeNotifierTests.cs`

**Acceptance Criteria:**
- [ ] `CollegeNotifier.RefreshEducatedMark` on an online player sets a legend mark with key `collegeEducated`, text `Educated` and `Count` equal to the balance; it updates an existing mark in place; it removes the mark when the balance is 0
- [ ] `Broadcast` sends OrangeBar2 to every online aisling; `Tell` sends an orange bar to the named player only
- [ ] `DefaultAislingScript.OnLogin` calls a College refresh after `ApplyPendingPositionRemovals()`
- [ ] `CollegeRoomScript` (script key `collegeRoom`) calls `CollegeService.RecordPresence(Subject.InstanceId, samples)` once per `TickSeconds` from its `Update`, with every aisling on the map and `MachineKeys.For(client.LoginId1, client.RemoteIp.ToString())`
- [ ] The server builds and the full College filter passes

**Verify:** `dotnet build Chaos/Chaos.csproj -c Debug 2>&1 | tail -3` → `0 Error(s)`; then the College test filter → all pass

**Steps:**

- [ ] **Step 1: Read the patterns.** Use Serena on:
  - `Chaos/Services/Towns/TownNotifier.cs`: `Broadcast`, `Tell`, `Find`, and how it gives legend marks.
  - `Chaos/Models/Legend/LegendMark.cs` and `Chaos/Collections/Legend.cs`: `TryGetValue`, `AddUnique`, `Remove`, settable `Count`/`Text`.
  - `Chaos/Scripting/AislingScripts/KnightScript.cs` `TryGiveKnightLegendMark`: building a `LegendMark`.
  - `Chaos/Scripting/MapScripts/Abstractions/MapScriptBase.cs`: the `Update(TimeSpan)` signature.
  - One existing map script that uses `Update` with an interval timer, such as an `IntervalTimer` in a spawner script under `Chaos/Scripting/MapScripts/Abstractions/`.

- [ ] **Step 2: Write `CollegeNotifier`.** Follow `TownNotifier`'s constructor (`IClientRegistry<IChaosWorldClient> clients`). Its members:
  - `Broadcast` copies `TownNotifier.Broadcast` exactly.
  - `Tell(name, message)`: finds the online aisling by name, case-insensitive, as `TownNotifier.Find` does, and calls `SendOrangeBarMessage(message)`.
  - `RefreshEducatedMark(name, marks)`: finds the online aisling (skips if offline), then calls the shared static `CollegeNotifier.ApplyEducatedMark(Aisling aisling, int marks)`.
  - `ApplyEducatedMark` is `public static` so the login hook can reuse it:
    - if `marks <= 0`, remove the mark with key `collegeEducated` if present;
    - else if `aisling.Legend.TryGetValue("collegeEducated", out var mark)`, set `mark.Count = marks` and `mark.Text = "Educated"`;
    - else `aisling.Legend.AddUnique(new LegendMark("Educated", "collegeEducated", MarkIcon.Wizard, MarkColor.White, marks, GameTime.Now))`.

  Check which `MarkIcon`/`MarkColor` names exist. If `Wizard` or `White` is missing, pick the closest scholarly icon and a light colour, and note it in your report. After changing the legend, send whatever profile refresh `KnightScript` sends after `AddUnique`; if it sends none, send none.

- [ ] **Step 3: Write `CollegeNotifierTests`.** Test only the static `ApplyEducatedMark` on a test aisling. Find how other tests build an `Aisling`: search `Tests/Chaos.Tests` for `MockAisling` or `new Aisling(` with Serena `search_for_pattern`.
  - `ApplyEducatedMark(aisling, 3)` adds the mark with `Count == 3`.
  - Calling it again with 5 updates the same mark, and `Legend` keeps one `collegeEducated` entry.
  - Calling it with 0 removes it.

- [ ] **Step 4: Login refresh.**
  - Inject `CollegeService` into `DefaultAislingScript`'s constructor the way `PollManager` is injected, and keep the existing parameter style.
  - Add a public method to `CollegeService` (lock, read only): `public int MarksOf(string name)` already exists.
  - In `OnLogin`, right after `ApplyPendingPositionRemovals();`, add:

```csharp
        //marks earned or spent while offline show on the legend from the next login
        CollegeNotifier.ApplyEducatedMark(Subject, College.MarksOf(Subject.Name));
```

  - If the constructor change breaks tests that build `DefaultAislingScript` by hand, update those call sites. Find them with Serena `find_referencing_symbols` on the constructor.

- [ ] **Step 5: Write `CollegeRoomScript`.**

```csharp
using Chaos.Collections;
using Chaos.Models.World;
using Chaos.Scripting.MapScripts.Abstractions;
using Chaos.Services.College;
using Chaos.Time;
using Microsoft.Extensions.Options;

namespace Chaos.Scripting.MapScripts.Temuair.College;

/// <summary>Reports who is in a College subject room every tick; samples run on the map's own update.</summary>
public sealed class CollegeRoomScript : MapScriptBase
{
    private readonly CollegeService College;
    private readonly IIntervalTimer SampleTimer;

    public CollegeRoomScript(MapInstance subject, CollegeService college, IOptions<CollegeOptions> options)
        : base(subject)
    {
        College = college;
        SampleTimer = new IntervalTimer(TimeSpan.FromSeconds(options.Value.TickSeconds), false);
    }

    public override void Update(TimeSpan delta)
    {
        SampleTimer.Update(delta);

        if (!SampleTimer.IntervalElapsed)
            return;

        var samples = Subject.GetEntities<Aisling>()
                             .Select(a => new PresenceSample(a.Name, MachineKeys.For(a.Client.LoginId1, a.Client.RemoteIp.ToString())))
                             .ToList();

        College.RecordPresence(Subject.InstanceId, samples);
    }
}
```

Fix the usings, the timer type and `GetEntities` to match the real APIs you found in Step 1. Scripts are resolved by class name: `CollegeRoomScript` → key `collegeRoom`. Confirm the naming rule with any existing map script's key in an `instance.json`.

- [ ] **Step 6: DI.** Add `AddCollege()` exactly as shown in Task 5 Step 6. Add `using Chaos.Services.College;`, and call it from `Program.cs` next to `AddTowns()`, in the same style as that call.

- [ ] **Step 7: Build and test.** Run the Verify commands.

---

### Task 7: Registrar and lectern dialogs (server scripts and their dialog JSON)

**Goal:** Players use the College through Veyrin, the Registrar, and the lectern in each room. Each menu shows only what the speaker may do, and every action goes through `CollegeService`.

**Files:**
- Create: `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs` (server worktree)
- Create: `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs` (server worktree)
- Create: `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/*.json` (Unora worktree)
- Modify: `Data/Configuration/Templates/Merchants/Temauir/tagor/veyrin.json` (Unora worktree)
- Create: `Data/Configuration/Templates/Merchants/Temauir/mileth/college_lectern.json` (Unora worktree)

**Acceptance Criteria:**
- [ ] Veyrin's merchant uses script key `collegeRegistrar` and opens `college_registrar_initial`; her name is "Veyrin, College Registrar"
- [ ] The Registrar main menu offers: Timetable, My marks, About the College; plus "Book a class" and "My bookings" only for someone who can teach any subject, and "All bookings" for the Director and admins
- [ ] Timetable pages show `CollegeText.TimetableLine` lines, each page 360 characters or fewer; "Nothing is booked this week." when empty
- [ ] Booking runs subject → format (Lecture/Discussion) → day (next 14) → half-hour time (free slots only) → length (20/30/45/60/90/120) → confirm, and calls `CollegeService.Book`; each `BookResult` has a plain reply
- [ ] My bookings lists the speaker's bookings and cancels the chosen one via `CollegeService.Cancel`
- [ ] The lectern opens `college_lectern_initial`. It offers "Begin booked class", "Begin a class now", "End class", "Remove a student" and "Class status" only when they apply (see the spec's lectern section). "End class" before 20 minutes replies with the minutes left
- [ ] "Remove a student" calls `RemoveStudent`, then moves that player to the Lyceum hall tile in front of the room's door, using the same warp call reactor scripts use (`TraverseMap` with a map from `ISimpleCache<MapInstance>`)
- [ ] Every option text is 35 characters or fewer and every dialog text 360 or fewer
- [ ] The server builds; the College filter passes

**Verify:** `dotnet build Chaos/Chaos.csproj -c Debug 2>&1 | tail -3` → `0 Error(s)`; `python Tools/College/check_dialogs.py` (written in Task 9; until then, check the limits by hand) → `ok`

**Steps:**

- [ ] **Step 1: Read the patterns.** Use Serena and Read on:
  - `Chaos/Scripting/DialogScripts/Temuair/Generic/TownGovernmentScript.cs`: menus, `OnDisplaying`, `OnNext`, `Subject.InsertOption`, `Subject.Reply`, how it stores choices between steps (dialog context or `Subject.MenuArgs`) and how it pages long text.
  - Its dialog JSON in Unora under `Templates/Dialogs/Temauir/mileth/aingeal/` (or wherever `aingeal_initial` lives; find it with Grep on `"templateKey": "aingeal_initial"`).
  - `Chaos/Scripting/ReactorTileScripts/Temauir/ShowDialogOnClickScript.cs`: how the lectern opens.
  - A reactor or dialog script that warps a player: Grep `TraverseMap(` under `Chaos/Scripting`.

- [ ] **Step 2: Write the dialog JSON.** Follow the TownGovernment JSON style. The registrar keys:
  - `college_registrar_initial`: a Menu with the static options Timetable / My marks / About the College; the script inserts the role-based options.
  - `college_registrar_timetable`: Normal, the script fills the text.
  - `college_registrar_marks`: Normal.
  - `college_registrar_about1`, `college_registrar_about2`: Normal, with fixed text explaining classes (20 minutes, 3 students on different computers, 80% presence), marks (20% rising by 10 per miss) and Teachers (Village awards, Knights until a subject has a Teacher).
  - `college_registrar_book_subject`, `_book_format`, `_book_day`, `_book_time`, `_book_length`, `_book_confirm`, `_book_done`.
  - `college_registrar_mybookings`, `_cancel_done`.

  The lectern keys: `college_lectern_initial`, `_begin_booked`, `_begin_now_format`, `_begin_done`, `_end_done`, `_end_toosoon`, `_remove_pick`, `_remove_done`, `_status`. Option text is 35 characters or fewer. Give each option an exact dialogKey, as other dialog JSON does.

- [ ] **Step 3: Write `CollegeRegistrarScript`.**
  - Inject `CollegeService`.
  - Role checks use `source.IsKnight` and `source.IsAdmin`.
  - Times shown with `CollegeText.When`.
  - Day options read like `Tue 6 Oct`.
  - Time options read like `14:00 UTC`.
  - Only offer half-hour starts after now whose whole chosen length would be free. The length is chosen after the time, so offer every start that's free for at least 20 minutes, then offer only the lengths that fit.

- [ ] **Step 4: Write `CollegeLecternScript`.**
  - Inject `CollegeService`, `ISimpleCache<MapInstance>` (or whatever reactor warps use) and `IOptions<CollegeOptions>`.
  - The room is `source.MapInstance.InstanceId`.
  - The subject for a walk-in is the room's subject. For the Chamber of Creation (Art and Music), add a step asking Art or Music.
  - The machine key comes from `MachineKeys.For(source.Client.LoginId1, source.Client.RemoteIp.ToString())`.
  - Map `StartResult` and `EndResult` values to plain replies, for example `NoTimeBeforeNextBooking` → "A booked class starts too soon for a walk-in."
  - The removal target tile is the hall tile in front of this room's door. Read it from a `college_lectern` merchant scriptVar or an options map; the simplest is a dictionary in `CollegeOptions`, `RoomExits` (room instance id → `"mileth_college:(x, y)"`), which Task 9 fills in with the real tiles. Add `RoomExits` to `CollegeOptions` with placeholder-free defaults once Task 9 has decided the tiles. If Task 9 isn't done yet, use the alcove door tiles from the spec (6/13/20/27/34, y = 2) and let Task 9 correct them.

- [ ] **Step 5: Edit the merchants.**
  - `veyrin.json`: `"name": "Veyrin, College Registrar"`, `"scriptKeys": ["collegeRegistrar"]`, remove the `showdialog` scriptVars.
  - Create `college_lectern.json` by copying the Town Hall station merchant (find a merchant used by a Town Hall `showDialogOnClick` reactor; its key is in a `mileth_town_hall` `reactors.json` `merchant` var). Set `templateKey: college_lectern`, `name: "Lectern"` and `scriptKeys: ["collegeLectern"]`.

- [ ] **Step 6: Build.** Run the Verify build.

---

### Task 8: Shop, board and `/college` command

**Goal:** The Curator sells for marks, the College board enforces Teacher posting, and admins can run the College from chat.

**Files:**
- Create: `Chaos/Scripting/DialogScripts/Temuair/College/CollegeShopScript.cs` (server)
- Create: `Chaos/Scripting/BulletinBoardScripts/CollegeBoardScript.cs` (server)
- Create: `Chaos/Messaging/Admin/CollegeCommand.cs` (server)
- Modify: `Data/Configuration/Templates/Merchants/Temauir/tagor/regalia.json` (Unora)
- Create: `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/collegeshop_*.json` (Unora)
- Create: `Data/Configuration/Templates/BulletinBoards/milethcollege.json` (Unora)
- Test: `Tests/Chaos.Tests/College/CollegeShopRulesTests.cs`

**Acceptance Criteria:**
- [ ] `regalia.json` lists exactly the spec's stock in `scriptVars.collegeShop.items` with the spec's prices, the honorary keys in `collegeShop.artAward` and `collegeShop.loreAward`, and no `itemsForSale`
- [ ] Buying calls `CollegeService.TryBuy`; when the item can't be given, `Refund` returns the marks; bought items have `NoNpcSale = true`
- [ ] An honorary item is refused without an Art (or Lore) award of Clave or higher, with the reply "You need an award in Art to wear this." (or Lore)
- [ ] The board lets anyone view, `CollegeService.CanPost` post, and the author or `CanModerate` delete; template `milethcollege` uses script key `CollegeBoard`
- [ ] `/college status|director|marks|award|endclass` work as the spec's table; bad input prints a one-line usage
- [ ] The server builds; the College filter passes

**Verify:** `dotnet build Chaos/Chaos.csproj -c Debug 2>&1 | tail -3` → `0 Error(s)`; the College filter → all pass

**Steps:**

- [ ] **Step 1: Read the patterns.**
  - `Chaos/Scripting/DialogScripts/Medenia/Tower/TowerMarksShopScript.cs` and its dialogs in Unora (`Templates/Dialogs/Medenia/Tower/towermarks_*.json`).
  - `Chaos/Scripting/BulletinBoardScripts/TheatreBoardScript.cs` and `Abstractions/ConfigurableBulletinBoardScriptBase.cs`.
  - `Chaos/Messaging/Admin/RemovePositionCommand.cs` for the command shape and argument parsing.

- [ ] **Step 2: Shop rule helper and test.** Put the award gate in a pure static method so it's testable:

```csharp
namespace Chaos.Services.College;

public static class CollegeShopRules
{
    /// <summary>The subject whose award an item needs, or null when anyone may buy it.</summary>
    public static CollegeSubject? RequiredAward(string itemKey, IReadOnlyCollection<string> artAward, IReadOnlyCollection<string> loreAward)
        => artAward.Contains(itemKey, StringComparer.OrdinalIgnoreCase)
            ? CollegeSubject.Art
            : loreAward.Contains(itemKey, StringComparer.OrdinalIgnoreCase)
                ? CollegeSubject.Lore
                : null;

    public static bool MayBuy(AwardTier held) => held >= AwardTier.Clave;
}
```

Save it as `Chaos/Services/College/CollegeShopRules.cs`. Test it in `CollegeShopRulesTests.cs`:
  - `artist_boots` in `artAward` → Art;
  - `male_artist` → null;
  - `MayBuy(None)` false;
  - `MayBuy(Clave)` true.

- [ ] **Step 3: Write `CollegeShopScript`** by copying `TowerMarksShopScript`. Its changes:
  - script vars `collegeShop.items`, `collegeShop.artAward`, `collegeShop.loreAward`;
  - the balance is `CollegeService.MarksOf`;
  - payment is `TryBuy`, and failed delivery calls `Refund`;
  - add the award gate before the amount request.

  Write its dialogs `collegeshop_initial`, `_amountrequest`, `_confirmation` and `_accepted` by copying the `towermarks_*` dialogs and changing the wording to "Educated Marks". Update `regalia.json`:

```json
{
  "itemsForSale": [],
  "itemsToBuy": [],
  "name": "Curator of Regalia",
  "restockIntervalHrs": 24,
  "restockPct": 100,
  "scriptKeys": ["collegeShop"],
  "scriptVars": {
    "collegeShop": {
      "items": {
        "male_artist": 2, "female_artist": 2,
        "male_lorekeeper": 2, "female_lorekeeper": 2,
        "male_PrepUniform": 1, "female_prepuniform": 1,
        "male_scholaroutfit": 1, "female_scholardress": 1,
        "female_milethcollegeskirt": 1,
        "honor_male_artist": 3, "honor_female_artist": 3,
        "honor_male_artisthat": 3, "honor_female_artisthat": 3, "artist_boots": 3,
        "honor_male_lorekeeper": 3, "honor_female_lorekeeper": 3,
        "honor_male_lorekeeperhat": 3, "honor_female_lorekeeperhat": 3, "lorekeeper_boots": 3
      },
      "artAward": ["honor_male_artist", "honor_female_artist", "honor_male_artisthat", "honor_female_artisthat", "artist_boots"],
      "loreAward": ["honor_male_lorekeeper", "honor_female_lorekeeper", "honor_male_lorekeeperhat", "honor_female_lorekeeperhat", "lorekeeper_boots"]
    }
  },
  "skillsToTeach": [],
  "spellsToTeach": [],
  "sprite": 943,
  "templateKey": "regalia"
}
```

  Confirm every item key exists: Grep each `"templateKey": "<key>"` under `Data/Configuration/Templates/Items`. If one doesn't exist, drop it and say so in your report. The `scriptVars` key shape must match how `TowerMarksShopScript` reads `towerMarksShop.items`; if Tower uses a list of `{key, price}` objects, use that shape instead.

- [ ] **Step 4: Write `CollegeBoardScript`** by copying `TheatreBoardScript`. Rights go through `CollegeService` (`CanPost`, `CanModerate`, author check). Create `milethcollege.json`:

```json
{
    "templateKey": "milethcollege",
    "name": "Mileth College",
    "scriptKeys": ["CollegeBoard"],
    "scriptVars": {
        "collegeBoard": {
            "postRetentionHours": -1
        }
    }
}
```

- [ ] **Step 5: Write `CollegeCommand`.** Use `[Command("college", helpText: "...")]`, admin-only by default. The subcommands:
  - `status` prints the Director, the running class (Teacher, subject, minutes run), the next 3 bookings and the Teacher counts per subject.
  - `director <name|none>`.
  - `marks <name> [give|take <n>]`.
  - `award <name> <subject> <none|clave|village|kingdom|aisling>`.
  - `endclass` calls `ForceEnd` and prints the outcome.

  Parse subjects and tiers case-insensitively with `Enum.TryParse(..., true, ...)`.

- [ ] **Step 6: Build and test.** Run the Verify commands.

---

### Task 9: College maps tool and world content

**Goal:** `Tools/College/college_maps.py` builds every College map and instance file from base copies. Running it puts the path, the grounds with the Tavaly temple, the moved Lyceum hall and the five rooms into the Unora worktree. Rendered previews confirm the result.

**Files:**
- Create: `Tools/College/college_maps.py`, `Tools/College/test_college_maps.py`, `Tools/College/README.md`, `Tools/College/__init__.py`, `Tools/College/check_dialogs.py`
- Create: `Tools/College/village_way_402_base.map`, `lyceum_762_base.map`, `chamber_763_base.map` (copies of today's files)
- Modify: `Data/Configuration/MapData/lod402.map`, `lod762.map`, `lod763.map`
- Create: `Data/Configuration/MapData/lod774.map` … `lod778.map`, `Data/Configuration/Templates/Maps/774.json` … `778.json`
- Move: `Data/Configuration/MapInstances/Temuair/Towns/Tagor/Lyceum_of_Tagor` → `.../Mileth/College/Mileth_College`; `.../Tagor/Chamber_of_Quills` → `.../Mileth/College/Chamber_of_Quills`
- Create: `.../Mileth/College/{College_Grounds,Chamber_of_Creation,Chronicle_Hall,Codex_Room,Philosophers_Thought}/{instance,merchants,reactors,monsters}.json`
- Modify: `Data/Configuration/MapInstances/Temuair/Towns/Mileth/Mileth_Village_Way/reactors.json` (append 3 warps)

**Acceptance Criteria:**
- [ ] `python Tools/College/college_maps.py --check` exits 0, and running the tool twice gives byte-identical files
- [ ] Village Way: only the new dirt path tiles change, and warps at (15, 24..26) lead to `mileth_college_grounds:(1, 14..16)`; every existing reactor is kept
- [ ] Grounds 774 (30 × 30): the temple contains only foreground ids 19701–19761 and 20075/20076 from Tavaly; the door warp leads to `mileth_college:(43, 10)`; the west edge (0, 14..16) leads back to Village Way; every warp source and arrival tile is walkable (not under a `0x0F` sotp foreground)
- [ ] Lyceum `mileth_college`:
  - its name is "Mileth College";
  - the exit (44, 10) leads to the grounds tile in front of the temple door;
  - five alcove door warps lead to the five rooms;
  - the Art sign reads "(Art and Music) Chamber of Creation";
  - Veyrin and the Curator are placed on walkable tiles, the guards are kept, and there's a `bulletinboard` reactor for `milethcollege`
- [ ] Rooms 763/775–778: each has a lectern piece at the front, a `showDialogOnClick` reactor on it (merchant `college_lectern`, dialog `college_lectern_initial`), an exit to the hall tile in front of its alcove, and `scriptKeys: ["collegeRoom"]`; music 16
- [ ] The old Tagor instance folders are gone, and no file in `Data/Configuration` still mentions `lyceum_of_tagor` or `chamber_of_quills` (Grep)
- [ ] `python -m unittest Tools.College.test_college_maps` passes
- [ ] Preview PNGs of 402, 774, 762 and one room are written to the scratch folder given by `--preview`, and the task report attaches them for the coordinator to view
- [ ] `check_dialogs.py` checks every `Templates/Dialogs/Temauir/mileth/college/*.json`: option text 35 characters or fewer, text 360 or fewer, no em dash, and every `dialogKey` it names exists. It prints `ok`
- [ ] The lectern room exits chosen here are written into `CollegeOptions.RoomExits` defaults (server worktree), if Task 7 added that dictionary

**Verify:** `python Tools/College/college_maps.py --check && python -m unittest Tools.College.test_college_maps && python Tools/College/check_dialogs.py` → exit 0 and `ok`

**Steps:**

- [ ] **Step 1: Read the model.** Read `Tools/TownHall/town_hall.py` and its README fully. Reuse its:
  - map read/write helpers;
  - sotp walkability check (`load_sotp`, `is_wall`, `walkable`);
  - rectangle copy;
  - JSON writers.

  You may import from `Tools.TownHall.town_hall` instead of copying, if the functions are importable without side effects.

- [ ] **Step 2: Copy the bases.**

```bash
cp Data/Configuration/MapData/lod402.map Tools/College/village_way_402_base.map
cp Data/Configuration/MapData/lod762.map Tools/College/lyceum_762_base.map
cp Data/Configuration/MapData/lod763.map Tools/College/chamber_763_base.map
```

- [ ] **Step 3: Find the facts the tool needs, and write them as constants at the top of the script.**
  - **Village Way dirt:** the background ids of the dirt patch around (12..14, 14..18) in `lod402.map`.
  - **Tavaly paving and grass:** the most common background ids in `UnusedMapData/lod11500.map` in the rectangle x 40–62, y 25–50.
  - **The temple's door:** after pasting, the walkable tiles directly in front of the temple's opening. Render a preview and look. Tavaly's own door tiles can't be read from a warp, because Tavaly has no instance.
  - **The Lyceum alcove door tiles:** the walkable tile at y = 0 or 1 in each alcove under the signs at x 6, 13, 20, 27, 34 (today's Literature door is (22, 0)). Render 762 with the existing `MapRender` and the old instance to see them.
  - **The lectern piece:** render stc 20161–20166 (see `Tools/TownHall/MapRender` or DALib), and pick a marble podium that reads as a lectern.

- [ ] **Step 4: Write `test_college_maps.py` first.** Use unittest, like `test_town_hall.py`. The tests:
  - building twice gives identical bytes;
  - the Village Way bytes outside the path rectangle are unchanged;
  - the grounds contain only the allowed temple ids;
  - every warp in the generated reactors points at an existing instance id and a walkable tile;
  - each room has exactly one `collegeRoom` script key and one lectern reactor.

- [ ] **Step 5: Write `college_maps.py`** with `--check`, `--root`, `--sotp` and `--preview <dir>`. Write the instance JSON with the same field order as existing instances (copy `Mileth_Town_Hall/instance.json` as the shape). Keep the Lyceum's existing `merchants.json` entries, but move Veyrin and add Regalia's spawn where you choose. Keep the guards. Edit `Mileth_Village_Way/reactors.json` by loading it, removing any earlier College warps (so reruns are stable) and appending the three new warps.

- [ ] **Step 6: Run, render and look.**

```bash
python Tools/College/college_maps.py --preview "$TEMP/college-preview"
for m in "402 16 32 Mileth_Village_Way" "774 30 30 College/College_Grounds" "762 45 25 College/Mileth_College" "775 20 20 College/Chamber_of_Creation"; do
  set -- $m
  dotnet run --project Tools/TownHall/MapRender -c Release -- Data/Configuration/MapData/lod$1.map $2 $3 "$TEMP/college-preview/$1.png" Data/Configuration/MapInstances/Temuair/Towns/Mileth/$4
done
```

  Open each PNG with the Read tool and check:
  - the temple sits whole on the grounds, with no cut-off pieces;
  - the path joins the edge;
  - the warp markers sit on doors;
  - the lectern stands at the front of the room.

  Fix and rerun until they do.

- [ ] **Step 7: `check_dialogs.py`.** A short script that loads every JSON under `Templates/Dialogs/Temauir/mileth/college/` and checks:
  - option `optionText` is 35 characters or fewer;
  - `text` is 360 characters or fewer;
  - no `—`;
  - each option's `dialogKey` exists among all dialog templates in `Templates/Dialogs`, or is `close`.

  It prints `ok` or each failure.

- [ ] **Step 8: Run the Verify command.**

---

### Task 10: Full checks

**Goal:** Everything builds and the whole server test suite is at its known baseline, so the College changed nothing else.

**Files:**
- none (fixes go back into the task that owns the failing file)

**Acceptance Criteria:**
- [ ] `dotnet build Chaos/Chaos.csproj -c Debug` → `0 Error(s)` with no new warnings in College files
- [ ] The full `Chaos.Tests` run fails only the 2 known baseline tests (GiveAbility, OnItemDroppedOn stackable)
- [ ] The Python tool and dialog checks pass
- [ ] Grep finds no `TODO`, `TBD` or `NotImplementedException` in College files

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi 2>&1 | tail -15` → only the 2 known failures

**Steps:**

- [ ] **Step 1:** Run the build, the full test run, `python Tools/College/college_maps.py --check`, `python -m unittest Tools.College.test_college_maps` and `python Tools/College/check_dialogs.py`.
- [ ] **Step 2:** For each failure outside the known two, find the owning task's file and fix it.

---

### Task 11: Commit the full implementation

**Goal:** One commit per repo on `feat/college-core`, not pushed and not merged.

**Files:**
- every file changed in both worktrees

**Acceptance Criteria:**
- [ ] `git -C worktrees/college-server log -1 --oneline` shows the College commit on `feat/college-core`
- [ ] `git -C worktrees/college-unora log -1 --oneline` shows the College commit on `feat/college-core`
- [ ] `git status --short` is empty in both worktrees, except for files that were untracked before (none expected)
- [ ] Nothing is pushed

**Verify:** `git -C <worktree> status --short` → empty for both

**Steps:**

- [ ] **Step 1: Review what will be committed**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/college-server && git status --short
cd /c/Users/Michael/Documents/GitHub/worktrees/college-unora && git status --short
```

- [ ] **Step 2: Commit each repo.** Stage by explicit path, not `git add -A`. Write the message file with UTF-8 without a BOM (see the PowerShell BOM memory; in Bash, `printf` is fine).

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/college-server
git add Chaos/Services/College Chaos/Scripting/DialogScripts/Temuair/College Chaos/Scripting/MapScripts/Temuair/College \
  Chaos/Scripting/BulletinBoardScripts/CollegeBoardScript.cs Chaos/Messaging/Admin/CollegeCommand.cs \
  Chaos/Extensions/ServiceCollectionExtensions.cs Chaos/Program.cs Chaos/Scripting/AislingScripts/DefaultAislingScript.cs \
  Tests/Chaos.Tests/College
git status --short   # anything else listed must be explained or added by path
printf '%s\n' "Mileth College part 1: classes, marks, timetable, shop, board" "" \
  "College service with roles, timetable, attendance sampling and Educated Mark rolls;" \
  "Registrar, lectern and shop dialogs; College board; /college admin command." "" \
  "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" \
  "Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz" > "$TEMP/college-server-msg.txt"
git commit -F "$TEMP/college-server-msg.txt"
```

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/college-unora
git add Tools/College Data/Configuration/MapData/lod402.map Data/Configuration/MapData/lod762.map Data/Configuration/MapData/lod763.map \
  Data/Configuration/MapData/lod774.map Data/Configuration/MapData/lod775.map Data/Configuration/MapData/lod776.map \
  Data/Configuration/MapData/lod777.map Data/Configuration/MapData/lod778.map \
  Data/Configuration/Templates/Maps/774.json Data/Configuration/Templates/Maps/775.json Data/Configuration/Templates/Maps/776.json \
  Data/Configuration/Templates/Maps/777.json Data/Configuration/Templates/Maps/778.json \
  Data/Configuration/MapInstances/Temuair/Towns/Mileth/College Data/Configuration/MapInstances/Temuair/Towns/Tagor \
  Data/Configuration/MapInstances/Temuair/Towns/Mileth/Mileth_Village_Way/reactors.json \
  Data/Configuration/Templates/Merchants Data/Configuration/Templates/Dialogs/Temauir/mileth/college \
  Data/Configuration/Templates/BulletinBoards/milethcollege.json
git status --short
printf '%s\n' "Mileth College part 1: College Grounds, Lyceum hall, subject rooms" "" \
  "New path from Mileth Village Way to the College Grounds with the Tavaly temple;" \
  "the Lyceum hall and Chamber of Quills move from Tagor; four more subject rooms;" \
  "Registrar, lectern, shop and board content. Maps built by Tools/College/college_maps.py." "" \
  "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" \
  "Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz" > "$TEMP/college-unora-msg.txt"
git commit -F "$TEMP/college-unora-msg.txt"
```

## Self-review notes

- **Spec coverage:**

  | Spec part | Task |
  |---|---|
  | World and maps | 9 |
  | Options, state, enums | 1 |
  | Rule units | 1–4 |
  | Service, tick | 5 |
  | Presence, notifier, legend mark, login | 6 |
  | Registrar, lectern | 7 |
  | Shop, board, command | 8 |
  | Tests | in each task, plus 10 |
  | Recommendations | already in the spec |

- **Cross-task names:** these are used consistently across tasks:
  - `CollegeService.MarksOf`, `TryBuy`, `Refund`, `CanPost`, `CanModerate`, `StartWalkIn`, `StartBooked`, `EndClass`, `ForceEnd`, `RemoveStudent`, `RecordPresence`, `Running`, `Upcoming`, `Book`, `Cancel`;
  - `CollegeText.TimetableLine` / `When`;
  - `MachineKeys.For`;
  - `CollegeNotifier.ApplyEducatedMark`.
- **`RoomExits`** is added to `CollegeOptions` by Task 7 and filled in by Task 9. Both tasks name it.
