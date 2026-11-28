# New-player onboarding: quest badge, quest tracker, ability hints

Date: 2026-11-27
Source: player feedback PDF "This game sucks... for a brand-new player" (2026-11-24).
Related, already built on `feat/new-player-onboarding`: tutorial skip hidden from new players (1), Riona points to Kamel (3), exact learn-requirement messages (4).

This spec covers the two remaining picks:

- **Item 6:** a quest log badge and one pinned on-screen quest tracker.
- **Item 8:** short effect text on ability hover, on automatically up to level 41.

## Why

The player and two friends returned on day two and asked "now what?". The quest log existed, but they found it only days later. Hotbar abilities show bare Gaelic names ("Beag Ioc", "Cradh") with no hint of what they do.

Success means three things:

- A new player sees that the quest log has something for them.
- They always have their current step on screen.
- They can tell what an ability does by hovering it.

Veterans can turn all of it off in F4.

## Repos

| Repo | Path for this work |
|---|---|
| Server | `Chaos.Client/Chaos-Server` submodule; worktree `worktrees/onboarding-server` |
| Client | `Chaos.Client` (new worktree) |
| Content | Unora; worktree `worktrees/onboarding-unora` |

Ship order: the client before the server.

---

## Item 6: quest log badge and pinned tracker

### Approach (chosen: A)

The client keeps an up-to-date copy of the quest log at all times. It derives the badge and the tracker from that copy.

- There is no new packet: the existing `QuestLogDisplay` (`QuestLogDisplayArgs`) is reused.
- Pins are client-side, per character.

Two alternatives were rejected:

- A new summary packet with pins stored on the server. It needs more protocol and saves little.
- Client polling. It is wasteful, and the badge would lag.

### Server (Chaos-Server)

`Chaos/Services/QuestLog/QuestLogService.cs`:

- `Tick(aisling)` currently returns early unless the quest log window is open. Change it so every online player keeps a `LastSent` list:
  - On each tick (once a second, as now), build the entries.
  - If they differ from `LastSent` (`SameEntries`), send them and store them.
  - Keep the open/closed state only where something still needs it, such as `IsOpen` callers. Sending no longer depends on it.
- On login (world entry), send the list once and seed `LastSent`. A client that just logged in can then show the badge without opening the log.
- `Open` stays as it is: it sends right away. `GiveUp` already sends.
- Per-player state stays in the `ConditionalWeakTable`, so it is released with the `Aisling`.

Cost: one `Build` per online player per second. It loops over about 34 quest definitions and counts items for the progress lines. A packet goes out only on change.

Tests (TUnit, `Tests/Chaos.Tests`):

- A tick with the window closed sends when the list changed.
- A tick with an unchanged list sends nothing.
- Login sends exactly once.
- Open still sends at once.

### Client (Chaos.Client)

#### State: `ViewModel/QuestLogState.cs`

- After `Apply(entries)` (already called by `HandleQuestLogDisplay`), work out three sets:
  - **New:** keys not in the seen snapshot.
  - **Advanced:** the key is in the snapshot, but its stage `Text` differs.
  - **Ready:** every `Progress` line has `Have == Need`, there is at least one line, and the quest was not ready in the snapshot.
- `HasUnseen` is true when any of the three sets is non-empty.
- **Seen snapshot:** `{ key -> (stageText, ready) }`, saved per character in the client's per-character settings store. Use the same mechanism as other per-character client data; the plan names the exact file.
  - Opening the quest log window replaces the snapshot with the current list and clears `HasUnseen`.
  - Because the snapshot persists, a change made while logged out (or between sessions) shows as unseen at the next login.
- A quest that left the list is dropped from the snapshot on the next open. Its removal never lights the badge.
- `HandleQuestLogDisplay` keeps its current rule: the window opens only when `QuestLogOpenPending` is set. Unsolicited updates only refresh state.

#### Badge

- The HUD quest log button blinks while `HasUnseen` is true, the same way `MailButton` does.
- It stops when the log is opened.
- In both HUDs (`WorldHudControl`, `LargeWorldHudControl`), wherever the quest log button lives.

#### Pinned tracker: new `Controls/World/Hud/QuestTrackerControl.cs`

- It sits at the top right of the viewport, inside the viewport bounds, and avoids existing top-right HUD elements (the plan checks which).
- Contents:
  - The quest title, in the gold heading color.
  - The current stage `Text`, wrapped and capped at 3 lines with an ellipsis.
  - The progress lines, for example "Rats: 3 / 5".
  - A small X button that unpins.
- It reads `WorldState.QuestLog` and updates whenever state changes.
- Clicking the title opens the quest log, which sends the normal open request.
- It is drawn under popups and over the map. It never takes input outside its own bounds.

#### Pinning rules (one pinned quest)

- The quest log window gets a **Pin** button for the selected quest. It reads **Unpin** when that quest is already pinned.
- The pinned key is stored per character with the snapshot.
- **Auto-pin:** after a state update, three conditions must all be true:
  - nothing is pinned
  - the character's level is 41 or below
  - the tracker setting is on

  When they are, pin the quest the player most recently gained: the newest key in the New set. If several are new at once (login), take the first in display order.
- A pinned key that is no longer in the list (finished or given up) unpins itself. The auto-pin rule then runs again, which can pick the next newest remaining quest.
- A manual unpin (X or Unpin) is respected: auto-pin does not re-pin that same key. Auto-pin may still pin a later new quest.

#### F4 setting

- "Show quest tracker", in the `Display` section, client-local, on by default. Add it to `SettingDefinition.cs` and `ClientSettings.cs` the way `ShowAbilityLevels` is done.
- When it is off, the tracker is hidden and auto-pin does not run.
- A manual pin is kept, so turning the setting back on shows it again.
- The badge is not affected by this setting.

#### Client tests (TUnit, `Tests/Chaos.Client.Tests`)

- **Snapshot diff:**
  - A new quest goes in New.
  - A changed stage text goes in Advanced.
  - A quest whose lines all became full goes in Ready.
  - An unchanged list leaves all three sets empty.
  - A removed quest is not unseen.
  - A quest with no progress lines is never Ready.
- **Opening** clears `HasUnseen` and stores the snapshot.
- **Auto-pin:**
  - It pins the newest quest at level 41.
  - It does not pin at level 42.
  - It does not pin when the setting is off.
  - It does not re-pin a key the player unpinned by hand.
- **A disappearing pin** unpins itself, and auto-pin picks the next quest.

---

## Item 8: ability hints on hover

### Data (Unora content plus the Chaos-Server schema)

- Skill and spell templates gain an optional string field, `"summary"`.
  - The schema change is additive (`SkillTemplateSchema`, `SpellTemplateSchema` and the template models), so forks are unaffected.
  - Example: `"summary": "Small heal"`.
- Limit: 28 characters, in plain words.
  - No color codes, and no trailing period.
  - Numbers only where they matter, for example "Lowers enemy AC by 20".
  - At template load, the server logs a warning for a summary over 28 characters and sends it unchanged.
- Every learnable skill and spell (any template with `learningRequirements`, 927 today) gets a summary.
  - Ranks or tiers with the same effect share wording, for example `culmination1` to `culmination5`.
  - Abilities that are not learnable, such as monster and item-granted ones, may leave the field out.

### Getting it to the client

- `Chaos.MetaData/ClassMetaData/AbilityMetaNode.cs` gains a `Summary` property.
- Its serializer writes the summary as a 7th entry property ([6]) after the description, always, as an empty string when there is no summary.
- `MetaDataStore` fills it from the template.
- Client `Chaos.Client.Data/Models/AbilityMetadata.cs` `ParseEntry`: read `props[6]` when present, into a new `AbilityMetadataEntry.Summary` that defaults to empty.
- Old clients read only [0] to [5] and ignore the rest. New clients facing an old server get an empty summary.

### Display (client)

- `AbilitySlotControl.HoverText` (`Controls/World/Hud/Panel/Slots/AbilitySlotBase.cs`):
  - When hints are active and the ability's metadata entry has a summary, the text becomes `"{current hover text} - {summary}"`.
  - The level-suffix rule (`FormatHoverText`) applies first, as today.
- The summary is looked up by ability name in the player's class ability metadata (`WorldState`'s ability metadata).
- No metadata entry, or an empty summary, shows the name alone.
- Both the small HUD description strip (`WorldHudControl.SetDescription`) and the large HUD tooltip (`LargeWorldHudControl`, line ~471) use `HoverText`, so no change is needed there.
- Check: the longest real result ("name with level" plus " - " plus 28 characters) must fit the `SZ_DESCRIPTION` label in both HUD layouts. If it doesn't, lower the 28-character cap rather than wrapping.

### F4 setting

- **"Ability hints": Auto / On / Off**, in the `Display` section, client-local, default Auto.
  - Auto shows hints while the character's level is 41 or below.
  - The level comes from the player attributes the client already receives.
- If the F4 rows support only checkboxes, use two rows instead:
  - "Ability hints", on by default.
  - "Only up to level 41", on by default.

  The plan checks which controls exist.

### Writing the summaries

- Drafts are written from each template's `description`. Ranks are grouped by name stem so one line covers a family.
- **Review order:**
  1. A 20-ability sample from the level 41-and-below set goes to the owner first.
  2. Then the rest of the level 41-and-below set (98 templates).
  3. Then the remainder.
- Summaries are batch-edited into the JSON with a script. The script adds only the `"summary"` key next to `"description"` and changes no other formatting.

### Tests

- **Server:**
  - The metadata node serializes a summary as property [6], and an empty one when there is none.
  - The warning fires for summaries over 28 characters.
- **Client:**
  - `ParseEntry` handles 6- and 7-property entries.
  - The hover text formats with a summary, without one, and with hints off.
  - The Auto, On and Off settings behave correctly at levels 41 and 42.
- **Content check** (a Unora data test or script run in CI or by hand): every template with `learningRequirements` has a non-empty `summary` of 28 characters or fewer.

---

## Out of scope

- The tip bar.
- Monster and drop search.
- Cooldowns in the quest log.
- More than one pinned quest.
- Server-side pin storage.
- Tooltips for item-granted abilities.

## Risks

- **Retail DA client:** it may reject the 7th metadata property. Unora players use Chaos.Client, so this is accepted. Check that the launcher ships only Chaos.Client.
- **Per-second quest builds for every online player:** watch Seq or the profiler after deploy (see the server performance notes). If needed, build every 2 seconds for closed windows.
- **Summary accuracy:** wrong summaries mislead new players more than none at all. That is why the owner reviews the early set by hand.
