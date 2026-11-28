# Quest Badge and Pinned Tracker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** New players see when the quest log has something for them (a blinking quest button), and can keep one quest's current step and progress pinned on screen.

**Architecture:**
- The server sends every player their quest log on the first tick after login and again whenever it changes, not only while the window is open. It reuses the existing `QuestLogDisplay` packet.
- On the client, a new pure `QuestWatchState` compares each list against a per-character "seen" snapshot to drive the badge, and owns the single pin and the auto-pin rules.
- A new `QuestTrackerControl` draws the pinned quest.
- One per-character JSON file stores the snapshot, the pin and the dismissed keys.

**Tech Stack:**
- C# / .NET 10, TUnit + FluentAssertions.
- MonoGame client (`Chaos.Client`) and Chaos-Server.

**Spec:** `docs/superpowers/specs/2026-11-27-new-player-onboarding-design.md` (in this client worktree).

**Worktrees** (both on branch `feat/new-player-onboarding`):

| Code | Worktree |
|---|---|
| Server | `/home/michael/Documents/GitHub/worktrees/onboarding-server` |
| Client | `/home/michael/Documents/GitHub/worktrees/onboarding-client` |

The client's `Chaos-Server` submodule is NOT used for server edits.

## Global Constraints

- **Server build/test:** `dotnet build -m:1 Tests/Chaos.Tests/Chaos.Tests.csproj`, then `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter "/*/*/<Class>/*"`.
- **Client build/test:** `dotnet build -m:1 Chaos.Client.slnx`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/<Class>/*"`. Five Linux-only failures already exist on main, plus TestHookServerTests failing in a sandbox. Ignore them.
- **Locking:** use `Lock` + `EnterScope()`, never `lock`.
- **Level gate:** "level 41 or below" means `Level <= 41`.
- **Pins:** exactly one pinned quest.
- **Tracker text:** the stage text is capped at 3 wrapped lines and ends with an ellipsis when cut.
- **F4 row:** "Show quest tracker", in the Display section, ClientLocal, default **on**. When it is off, the tracker is hidden and auto-pin is suppressed; a manual pin is kept; the badge is unaffected.
- **Badge:** removed quests never light it. A quest with no progress lines is never Ready.
- **Commits:** every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Fresh install or first login with the new client:** the snapshot file is missing, so every active quest counts as New. The badge lights, and the first quest in display order auto-pins (at level 41 or below). Expected and fine; covered in Task 2.
2. **Corrupt or unreadable snapshot file:** load must fall back to empty and never crash login. Covered in Task 3.
3. **Character switch on the same client:** the watch state must be reset and reloaded for the new character, so one character's pin never shows on another. Covered in Task 4.
4. **Logging in at level 42 or above with a pin saved from earlier:** the pin stays (manual or earlier auto-pin) and only *new* auto-pins stop. Covered in Task 2.
5. **Quest log open while updates arrive:** the open window marks each update seen at once, so the badge must not flash while the player is looking at it. Covered in Task 2 (`MarkSeen` on every update while open) and wired in Task 4.

---

### Task 1: Server sends the quest log to every player on change

**Files:**
- Modify: `Chaos/Services/QuestLog/QuestLogService.cs` (the `OpenWindows` table, `Open`, `Close`, `IsOpen`, `Tick`, `GiveUp`, the private `OpenWindow` class)
- Modify: `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs:3494` (update the comment only: "keeps every player's quest list current on the client; sends only when it changed")
- Test: `Tests/Chaos.Tests/QuestLog/QuestLogServiceTests.cs`

**Interfaces:**
- Produces: unchanged public signatures `Open(Aisling)`, `Close(Aisling)`, `IsOpen(Aisling) -> bool`, `Tick(Aisling)`, `GiveUp(Aisling, string?)`.
- Behavior change:
  - `Tick` sends to any player whose list differs from the last one sent. The first tick for a player (no state yet) always sends.
  - `Close` only clears the open flag, and keeps `LastSent`.

- [ ] **Step 1: Replace the test `Tick_sends_nothing_before_Open_or_after_Close` with these three tests** (same fixture helpers as the neighbouring tests)

```csharp
[Test]
public async Task Tick_sends_on_the_first_tick_without_Open()
{
    // a player with one active quest, window never opened
    service.Tick(aisling);
    Mock.Get(aisling.Client).Verify(c => c.SendQuestLog(It.IsAny<IReadOnlyList<QuestLogEntryInfo>>()), Times.Once);
}

[Test]
public async Task Tick_with_the_window_closed_sends_when_the_list_changed_and_not_otherwise()
{
    service.Tick(aisling);          // first send
    service.Tick(aisling);          // unchanged -> nothing
    /* advance the quest stage or counter, as Tick_sends_only_when_the_list_changed does */
    service.Tick(aisling);          // changed -> send
    Mock.Get(aisling.Client).Verify(c => c.SendQuestLog(It.IsAny<IReadOnlyList<QuestLogEntryInfo>>()), Times.Exactly(2));
}

[Test]
public async Task Close_keeps_sending_changes_and_IsOpen_turns_false()
{
    service.Open(aisling);          // send 1
    service.Close(aisling);
    service.IsOpen(aisling).Should().BeFalse();
    /* change the list */
    service.Tick(aisling);          // send 2
    Mock.Get(aisling.Client).Verify(c => c.SendQuestLog(It.IsAny<IReadOnlyList<QuestLogEntryInfo>>()), Times.Exactly(2));
}
```

  Keep `Open_sends_the_list_once` and `Tick_sends_only_when_the_list_changed` as they are.

- [ ] **Step 2: Run them. Expected: the three new tests FAIL** (Tick currently returns early with no open window).

- [ ] **Step 3: Implement.**
  - Rename `OpenWindow` to `PlayerQuestLog`, with `IReadOnlyList<QuestLogEntryInfo>? LastSent` and `bool WindowOpen`, and rename the table to match.
  - `Open` and `GiveUp` set `LastSent` after sending. `Open` sets `WindowOpen = true`; `Close` sets it to false.
  - `IsOpen` returns `WindowOpen`.
  - `Tick` uses get-or-add on the table, then sends when `LastSent is null || !SameEntries(LastSent, entries)`.
  - Update the `Tick` XML doc to match.

- [ ] **Step 4: Run all `QuestLogServiceTests`. Expected: all PASS.**

- [ ] **Step 5: Commit** in the server worktree: `git commit -am "Send the quest log to every player when it changes"`, plus the Co-Authored-By trailer.

---

### Task 2: Client `QuestWatchState` (badge, pin and auto-pin logic)

**Files:**
- Create: `Chaos.Client/ViewModel/QuestWatchState.cs`
- Modify: `Chaos.Client/Collections/WorldState.cs` (add `public static QuestWatchState QuestWatch { get; } = new();` next to `QuestLog`, around line 188, and reset it wherever `QuestLog` is reset on logout; grep for `QuestLog.` in `WorldState.cs`)
- Test: `Tests/Chaos.Client.Tests/QuestWatchStateTests.cs`

**Interfaces:**
- Consumes: `Chaos.Networking.Entities.Server.QuestLogEntryInfo` (Key, Title, Text, Progress of `QuestLogProgressInfo(Label, Have, Need)`).
- Produces (all public, in `Chaos.Client.ViewModel`):
  - `public sealed record QuestSeen(string Text, bool Ready);`
  - `public sealed class QuestWatchData { public Dictionary<string, QuestSeen> Seen { get; set; } = []; public string? PinnedKey { get; set; } public HashSet<string> Dismissed { get; set; } = []; }`, the persisted shape that Task 3 serializes.
  - `public static bool IsReady(QuestLogEntryInfo entry)`: true when `Progress.Count > 0` and every line has `Have == Need`.
  - `public sealed class QuestWatchState` with:
    - `bool HasUnseen { get; }`, `string? PinnedKey { get; }`, `QuestLogEntryInfo? Pinned { get; }` (the pinned entry from the last list, or null), and `event Action? Changed`.
    - `void Load(QuestWatchData data)` / `QuestWatchData Export()` / `void Reset()`.
    - `void OnEntries(IReadOnlyList<QuestLogEntryInfo> entries, int level, bool trackerEnabled)`.
    - `void MarkSeen()`: copies the current list into `Seen` (Text, IsReady), drops keys not in the list, and clears `HasUnseen`.
    - `void Pin(string key)`: sets `PinnedKey` and removes the key from `Dismissed`.
    - `void Unpin()`: adds the current `PinnedKey` to `Dismissed` and clears it.
  - `bool Dirty { get; }` / `void ClearDirty()`: set whenever the persisted data changes, so Task 4 saves only then.

Rules for `OnEntries`, in this order:
1. Store the entries.
2. Work out three sets against `Seen`:
   - **New:** key not in `Seen`.
   - **Advanced:** key in `Seen` and `Text` differs.
   - **Ready:** `IsReady` is true and the snapshot says not ready, or the key is new.

   `HasUnseen` = any of the three sets is non-empty.
3. Drop `Dismissed` keys that are no longer in the list.
4. If `PinnedKey` is not in the list, clear it.
5. Auto-pin: if `PinnedKey` is null, `trackerEnabled` is true, and `level <= 41`, pin the first entry (in list order) that is in New and not in `Dismissed`.
6. Raise `Changed`.

- [ ] **Step 1: Write the failing tests** in `QuestWatchStateTests`, with a helper `Entry(key, text, params (ushort have, ushort need)[] progress)`:
  - `New_quest_lights_the_badge`: empty Seen, one entry, then `HasUnseen` is true.
  - `Changed_stage_text_lights_the_badge`: Seen `{a: ("old", false)}`, entry a with "new", then true.
  - `Newly_ready_quest_lights_the_badge`: Seen `{a: (t, false)}`, entry a with the same text and progress (5,5), then true.
  - `Unchanged_list_does_not_light_the_badge`: Seen equals the list, then false.
  - `Removed_quest_does_not_light_the_badge`: Seen `{a, b}`, list `[a]` unchanged, then false.
  - `Quest_without_progress_lines_is_never_ready`: `IsReady(Entry("a","t"))` is false.
  - `MarkSeen_clears_the_badge_and_drops_missing_keys`: after `MarkSeen`, `HasUnseen` is false and `Export().Seen.Keys` equals the list keys.
  - `Auto_pins_the_first_new_quest_at_level_41`: two new entries a and b, level 41, then `PinnedKey == "a"`.
  - `Does_not_auto_pin_at_level_42`: then `PinnedKey` is null.
  - `Does_not_auto_pin_when_tracker_is_off`: then null.
  - `Keeps_an_existing_pin_at_level_42`: Load with `PinnedKey "a"`, list `[a]`, level 60, then `PinnedKey == "a"`.
  - `Manual_unpin_is_not_repinned`: auto-pinned a, `Unpin()`, then `OnEntries` with the same list (a still new because Seen is empty), then `PinnedKey` is null.
  - `Finished_pin_unpins_and_auto_pin_picks_the_next_new_quest`: pinned a; next list `[b]` with b new, then `PinnedKey == "b"`.
  - `Open_window_marks_each_update_seen` (Review Focus 5): call `OnEntries` then `MarkSeen`, then `HasUnseen` is false.

- [ ] **Step 2: Run `QuestWatchStateTests`. Expected: FAIL to compile** (the type is missing).

- [ ] **Step 3: Implement `QuestWatchState.cs`** as above. Add an XML doc comment on the class explaining the snapshot, matching the style of `QuestLogState`.

- [ ] **Step 4: Run `QuestWatchStateTests`. Expected: PASS.**

- [ ] **Step 5: Commit** in the client worktree: `"Add QuestWatchState for the quest badge and pin"`.

---

### Task 3: Per-character persistence and the F4 setting

**Files:**
- Create: `Chaos.Client/ViewModel/QuestWatchStore.cs`
- Modify: `Chaos.Client/Systems/ClientSettings.cs` (property, load case, save line, next to `ShowAbilityLevels` at lines 88, 334 and 615)
- Modify: `Chaos.Client/ViewModel/SettingDefinition.cs` (add `SettingKey.ShowQuestTracker` near line 85, and a definition row after `ShowAbilityLevels` around line 229: label "Show quest tracker", `SettingSection.Display`, `SettingCategory.ClientLocal`)
- Test: `Tests/Chaos.Client.Tests/QuestWatchStoreTests.cs`, plus an extension to the existing settings round-trip test if one covers `ShowAbilityLevels` (grep the Tests folder)

**Interfaces:**
- Produces:
  - `public static class QuestWatchStore { public const string FILE_NAME = "questwatch.json"; public static QuestWatchData Load(string path); public static void Save(string path, QuestWatchData data); }`
    - Uses System.Text.Json.
    - `Load` returns `new QuestWatchData()` when the file is missing, empty or invalid. It never throws.
    - `Save` writes to a temp file next to the target, then moves it over.
  - `public static bool ClientSettings.ShowQuestTracker { get; set; } = true;`, saved as `ShowQuestTracker : 1|0`.
- Consumes: `QuestWatchData` and `QuestSeen` (Task 2).

- [ ] **Step 1: Failing tests** (use a temp directory):
  - `Round_trips_seen_pin_and_dismissed`
  - `Missing_file_loads_empty`
  - `Corrupt_file_loads_empty`: write `"{not json"`, then `Load` returns empty `Seen` and a null pin.
  - In the settings test (if present): `ShowQuestTracker` defaults to true and round-trips false.

- [ ] **Step 2: Run them. Expected: FAIL.**

- [ ] **Step 3: Implement the store and the setting.**

- [ ] **Step 4: Run them. Expected: PASS.**

- [ ] **Step 5: Commit:** `"Persist quest watch state per character; add Show quest tracker setting"`.

---

### Task 4: Wiring — badge on the quest button, load/save, mark seen

**Files:**
- Modify: `Chaos.Client/Controls/World/Hud/IWorldHud.cs` (add `void SetQuestBadge(bool on);`)
- Modify: `Chaos.Client/Controls/World/Hud/WorldHudControl.cs:232` and `LargeWorldHudControl.cs:223`:
  - Create `QuestButton` with `CreateButton<MailButton>("BTN_HELP")`. `MailButton` is the existing pulsing `UIButton`; keep the property typed `UIButton?` on the interface.
  - Implement `SetQuestBadge(on)` as `(QuestButton as MailButton)?.SetAnimating(on)`.
- Modify: `Chaos.Client/Screens/WorldScreen.ServerHandlers.cs:2006` (`HandleQuestLogDisplay`) and `Chaos.Client/Screens/WorldScreen.Wiring.cs:248-275` (`WireQuestLog`, `ToggleQuestLog`)
- Modify: the client's login/character-ready path, where `DataContext.LocalPlayerSettings` is initialized for the character (grep `LocalPlayerSettings.Initialize` or `IsInitialized`), and the logout/reset path.

**Interfaces:**
- Consumes:
  - `WorldState.QuestWatch` (Task 2)
  - `QuestWatchStore.Load/Save`, `ClientSettings.ShowQuestTracker` (Task 3)
  - `WorldState.Attributes.Current?.Level`, treated as 1 when null
  - `DataContext.LocalPlayerSettings.GetFilePath(QuestWatchStore.FILE_NAME)`
- Produces:
  - `private void SaveQuestWatchIfDirty()` in WorldScreen. It saves when `QuestWatch.Dirty` is set, then calls `ClearDirty()`.
  - Changes to the badge on the HUD.

Behavior:
- **Character ready:** `QuestWatch.Reset()`, then `QuestWatch.Load(QuestWatchStore.Load(path))`.
- **Logout and character switch:** `SaveQuestWatchIfDirty()`, then `Reset()` (Review Focus 3).
- **`HandleQuestLogDisplay`:** after `WorldState.QuestLog.Apply(...)`:
  1. Call `QuestWatch.OnEntries(args.Entries, level, ClientSettings.ShowQuestTracker)`.
  2. If `QuestLogWindow.Visible` or `QuestLogOpenPending`, call `QuestWatch.MarkSeen()`.
  3. Call `WorldHud.SetQuestBadge(QuestWatch.HasUnseen)`.
  4. Call `SaveQuestWatchIfDirty()`.
- **Opening the quest log window** (where `QuestLogWindow.Show()` is called): `MarkSeen()`, clear the badge, save.
- **HUD swap** (the `/` key that rebuilds `WorldHud`): re-apply `SetQuestBadge(QuestWatch.HasUnseen)`.

- [ ] **Step 1: Build.** `dotnet build -m:1 Chaos.Client.slnx`. Expected: 0 errors.

- [ ] **Step 2: Run the full client test suite.** Expected: no new failures beyond the known Linux/sandbox ones.

- [ ] **Step 3: Commit:** `"Blink the quest button for unseen quest changes"`.

---

### Task 5: `QuestTrackerControl`, the Pin button and the tracker wiring

**Files:**
- Create: `Chaos.Client/Controls/World/Hud/QuestTrackerLayout.cs` (pure geometry and text)
- Create: `Chaos.Client/Controls/World/Hud/QuestTrackerControl.cs`
- Modify: `Chaos.Client/Controls/World/Popups/QuestLog/QuestLogControl.cs`:
  - Add a `CustomButton` labelled "Pin" or "Unpin" at `X = 16`, `Y = BOTTOM_BUTTON_Y`, size `WIDE_BUTTON_WIDTH × WIDE_BUTTON_HEIGHT`, left of GiveUp (x 71).
  - Add `public event Action<string>? PinToggled;` (fires with the selected key).
  - Hide the button while the window is minimized, like GiveUp.
- Modify: `Chaos.Client/Screens/WorldScreen.cs` (construct and add the tracker under popups, re-anchor it on HUD swap) and `WorldScreen.Wiring.cs` (wire `PinToggled`, the tracker's X and the title click)
- Test: `Tests/Chaos.Client.Tests/QuestTrackerLayoutTests.cs`

**Interfaces:**
- Consumes:
  - `WorldState.QuestWatch.Pinned`, `PinnedKey`, `Pin(key)`, `Unpin()`, `Changed` (Task 2)
  - `ClientSettings.ShowQuestTracker` (Task 3)
  - `SaveQuestWatchIfDirty()` (Task 4)
  - `ToggleQuestLog()` (existing)
  - `HotbarDockLayout` (existing), to learn the width of bars docked on the right
- Produces:
  - `public static class QuestTrackerLayout`:
    - `public const int WIDTH = 180; public const int MARGIN = 8; public const int MAX_TEXT_LINES = 3;`
    - `public static Point Position(Rectangle viewport, int rightReserved)`: top-right inside the viewport, `x = viewport.Right - MARGIN - rightReserved - WIDTH`, `y = viewport.Y + MARGIN`.
    - `public static IReadOnlyList<string> StageLines(string text, Func<string, int> measure, int maxWidth)`: word-wraps and caps at `MAX_TEXT_LINES`. When cut, the last line ends in "..." and still fits `maxWidth`.
    - `public static string ProgressLine(QuestLogProgressInfo p) => $"{p.Label}: {p.Have} / {p.Need}"`.
  - `public sealed class QuestTrackerControl : UIPanel`:
    - Shows the title in the gold heading color used by `QuestLogControl`, the stage lines, the progress lines, and an X (`x_bn.spf`, 18×18) at the top right.
    - Events: `event Action? UnpinClicked; event Action? TitleClicked;`.
    - `Visible = ClientSettings.ShowQuestTracker && WorldState.QuestWatch.Pinned is not null`.
    - Rebuilds on `QuestWatch.Changed`, and resizes its height to the content.
    - Takes no input outside its bounds.

Wiring:
- **`PinToggled(key)`:** if `key == PinnedKey`, call `Unpin()`, else `Pin(key)`. Then save.
- **`UnpinClicked`:** `Unpin()`, then save.
- **`TitleClicked`:** `ToggleQuestLog()` if the window is not visible.
- **The F4 setting changes:** refresh the tracker's visibility. Find where settings apply live, for example where `ShowAbilityLevels` takes effect, and do the same.

- [ ] **Step 1: Failing tests** in `QuestTrackerLayoutTests`, using `measure = s => s.Length * 6`:
  - `Position_is_top_right_inside_the_viewport`: viewport (0,0,640,480), reserved 0, then (452, 8).
  - `Position_moves_left_of_right_docked_bars`: reserved 40, then x = 412.
  - `StageLines_wraps_and_caps_at_three_with_ellipsis`: a long text, maxWidth 120, then 3 lines; the last ends with "..."; each `measure(line) <= 120`.
  - `StageLines_keeps_short_text_on_one_line`.
  - `ProgressLine_formats_label_have_need`: ("Rats", 3, 5) gives "Rats: 3 / 5".

- [ ] **Step 2: Run them. Expected: FAIL.**

- [ ] **Step 3: Implement the layout, the control, the Pin button and the wiring.**

- [ ] **Step 4: Run the new tests and the full client suite. Expected: the new tests PASS, and there are no new failures.**

- [ ] **Step 5: Commit:** `"Add the pinned quest tracker and quest log Pin button"`.

---

### Task 6: Local playtest and finish

- [ ] **Step 1: Build the server and client from the worktrees.** Run them using the throwaway setup in `~/Documents/Unora/TamerTest` (see the local-playtest notes in memory). Refresh `run/` from the server worktree's build output, and `Data/Configuration` from `worktrees/onboarding-unora`.
- [ ] **Step 2: Check on a new character:**
  1. After Riona's rat quest is accepted, the quest button blinks and the tracker shows "Rats: 0 / N".
  2. Kill a rat: the counter updates without opening the log.
  3. Open the log: the blink stops.
  4. Unpin with X: the tracker hides and stays hidden on the next kill.
  5. Pin from the log: it shows again.
  6. Turn off F4 "Show quest tracker": it hides.
  7. Log out and back in: the pin and the seen state persist.
- [ ] **Step 3: Take screenshots** of the tracker in both HUD layouts (small and large; `/` swaps them) for the owner.
- [ ] **Step 4: Pin the client.** Do not bump the client's `Chaos-Server` submodule pin until the server branch is merged to master; note this in the handoff to the owner.
