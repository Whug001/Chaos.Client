# Guild roster last-seen and message of the day

Designed 2026-09-25. It builds ideas 44 (message of the day) and 45 (last seen on the roster) from `Unora/docs/guild-hall-ideas.md`. It is the first of three guild sub-projects picked that day. Guild applications (idea 43) and the librarian (ideas 10 and 47) follow with their own specs.

## Goal

- A guild can set a short message that each member sees when they log in.
- The guild roster shows when each member last played, so leaders can find inactive members.

Today the roster at Quill and Aricin lists names by rank and nothing else. No guild-wide login message exists. Three guilds have a hand-made guild bulletin board, but most guilds have none.

## Decisions

| Question | Decision |
|---|---|
| How the message shows at login | One guild chat line in the chat pane, tagged by the chat filter like normal guild chat. |
| Who can change the message | The leader, plus any rank the leader gives a new eighth permission switch. |
| Who sees last-seen | Every member, on the normal roster. |
| Where last-seen comes from | A list kept in `guild.json`, updated at logout. A member with no entry is read once from their save (approach 1). |
| Last-seen format | `online`, `under an hour ago`, `N hours ago` or `N days ago`. No exact dates. |
| Client changes | None. The guild chat message type and the scroll window already exist. |

## 1. Message of the day

### Storage

`GuildSchema` gets one nullable field:

```csharp
public GuildMessageOfTheDaySchema? MessageOfTheDay { get; set; }
```

`GuildMessageOfTheDaySchema` is a new record in `Chaos.Schemas/Guilds/` with `Text` (string), `SetBy` (string) and `SetAt` (UTC `DateTime`). The server saves with `WhenWritingDefault`, so a guild with no message writes no field. An older server skips the unknown field, so this doesn't block a rollback.

`GuildMapperProfile` maps it both ways. A missing field loads as "no message". Loading uses `RestoreMessageOfTheDay`, which doesn't check length, so a hand-edited file never stops a guild from loading.

### On the guild

`Guild` gets a read-only `GuildMessageOfTheDay? MessageOfTheDay` (a small record: `Text`, `SetBy`, `SetAt`) and three methods. All take the guild's `Sync` lock.

- `SetMessageOfTheDay(string text, string setBy, DateTime setAt)`: stores the message. It throws on empty text or text over `MESSAGE_OF_THE_DAY_MAX_LENGTH` (150). The dialog checks both first.
- `ClearMessageOfTheDay()`: removes it.
- `RestoreMessageOfTheDay(GuildMessageOfTheDay? message)`: used only by the mapper. It stores the value without checks, like `RestoreAllowanceLedger`.

Neither method saves; the dialog script calls `GuildStore.Save(guild)` after a change, as the other guild dialogs do.

### New permission switch

- `GuildPermission.SetMessageOfTheDay = 128` in `Chaos.DarkAges/Definitions/Enums.cs`.
- `GuildPermissionRules.ForTier` offers it to tiers 1, 2 and 3, as the last item in each list. The Permissions menu builds its options from `ForTier`, so the switch appears there with no menu code change.
- `GuildPermissionRules.Label` returns "Set the message of the day".
- It is **not** added to `COUNCIL_DEFAULT`. That constant stands for the rules from before permissions existed, and no old rule covered this. So the switch starts off for every rank, existing Council ranks included. Until the leader turns it on for a rank, only the leader can change the message.
- `Guild.HasPermission` already returns true for the leader.

**Rollback limit:** permissions are saved by name. Once a leader turns this switch on for a rank, that rank's `tierN.json` holds `SetMessageOfTheDay`, and an older server can't read the file. That guild then fails to load, so its members can't log in. Before rolling back, remove `SetMessageOfTheDay` from every `tier*.json`. An older server's first timed save also drops the message and the last-seen list from `guild.json`. Guild tuition has a similar limit.

### Menu

`GuildManagementScript` adds a "Message of the Day" option (`generic_guild_motd_initial`) for every guild member, at both Quill and Aricin. It sits just before "Members" in each list. People not in a guild don't see it.

A new `GuildMessageOfTheDayScript` (script key `GuildMessageOfTheDay`, deriving from `GuildScriptBase`) runs these dialogs, which live in Unora under `Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildMessageOfTheDay/`:

| Template key | Type | What it does |
|---|---|---|
| `generic_guild_motd_initial` | Menu | Shows the message, who set it and the UTC date, or "Your guild has no message of the day." Adds "Change" and, when a message exists, "Clear" for anyone with the switch. |
| `generic_guild_motd_change` | DialogTextEntry, `textBoxLength` 150 | "What should the message of the day say?" |
| `generic_guild_motd_change_confirmation` | Menu | Shows the new text with Yes/No. |
| `generic_guild_motd_change_accepted` | Normal | Saves, broadcasts, and says "The message of the day is set." |
| `generic_guild_motd_clear_confirmation` | Menu | "Clear the message of the day?" Yes/No. |
| `generic_guild_motd_clear_accepted` | Normal | Clears and says "The message of the day is cleared." |

Rules in the script:

- Every step re-checks guild membership and the switch, like `GuildMemberAdmitScript` re-checks `Admit`. A rank that loses the switch mid-dialog gets "Your rank can't change the message of the day."
- The entered text is trimmed. Blank text replies "Nothing was changed." and goes back to `generic_guild_motd_initial`, so nobody wipes the message by accident. Clearing is its own option.
- Text over 150 characters after trimming is refused with "That message is too long. Keep it to 150 characters." The client box already stops at 150, so this only guards against a modified client.
- The text travels to the confirmation and accepted steps through `MenuArgs`, the same way `GuildRankModifyScript` passes the new rank name.

### Broadcast on change

When a message is set, every online member gets the same line they would see at login (below). Clearing sends nothing to other members.

### At login

In `DefaultAislingScript.OnLogin`, after the existing "has appeared online" notices, the member who just logged in gets:

```
<Guild name> message of the day: <text>
```

It is sent with `SendServerMessage(ServerMessageType.GuildChat, …)` to that one player only, not through the guild channel. So it doesn't appear as a chat message from anyone. There's no line when the guild has no message. The line carries the chat filter's tags for the message text, so each player's Fantasy, Censored or Hide chat setting applies to it, as it does to normal guild chat.

## 2. Last seen on the roster

### Storage

`GuildSchema` gets:

```csharp
public Dictionary<string, DateTime> LastSeen { get; set; } = [];
```

It maps a member's name to the UTC time they were last seen. An older server skips the field.

`GuildStore.LoadFromFile` maps `guild.json` before it loads the rank files, so the mapper can't tell who is a member. The mapper passes every entry to `RestoreLastSeen`, and `Initialize` (which runs once the ranks are attached) drops names that aren't in any rank.

### On the guild

These methods take the `Sync` lock:

- `RecordLastSeen(string name, DateTime when)`: does nothing for a non-member. Otherwise it keeps the later of the stored time and `when`. Keeping the later time means a slow one-time read can never overwrite a newer logout.
- `TryGetLastSeen(string name, out DateTime when)`.
- `RestoreLastSeen(IEnumerable<KeyValuePair<string, DateTime>> entries)`: used only by the mapper. It stores entries without a member check, into a dictionary using `StringComparer.OrdinalIgnoreCase`.
- `Initialize` gains one step: after it sets the ranks, it removes last-seen names that aren't members.

The entry changes at these points:

| Event | Change |
|---|---|
| Login (`DefaultAislingScript.OnLogin`) | `RecordLastSeen(name, now)`, so a logout lost to a server restart costs only that session |
| Logout (`DefaultAislingScript.OnLogout`) | `RecordLastSeen(name, now)` |
| `AddMember` | `RecordLastSeen(name, now)` |
| `TryLeave` and `TryKickMember` succeed | The entry is removed |
| Disband | The whole guild folder goes, so nothing extra is needed |

No explicit save is needed at logout. `GuildStore` is a periodic store, and `SaveIntervalMins` is 5. A crash loses at most five minutes of logout times, which the player's own `LastLogout` would lose too.

### Roster text

`GuildMemberRosterScript` changes two things:

- The header becomes `Members: 12 (3 online)`.
- Each member line becomes `Name - <status>`. Rank grouping and order stay as they are.

A new static helper, `GuildLastSeen.Describe(bool online, DateTime? lastSeen, DateTime now)`, builds the status so it can be tested alone:

| Case | Text |
|---|---|
| The member is online | `online` |
| No time is known | `unknown` |
| Under 1 hour | `under an hour ago` |
| 1 to 24 hours | `N hours ago`, where N is whole hours ("1 hour ago") |
| 24 hours or more | `N days ago`, where N is whole days ("1 day ago") |

"Online" means the name is in `guild.GetOnlineMembers()`.

### Members with no entry yet

Members who haven't logged out since this ships have no entry. Those are the long-idle members leaders most want to see. So the first time the roster needs one of them:

1. The roster script calls `AislingFacadeCache.Get(name)`. The cache is injected through the constructor. For an offline player it reads their save's `aisling.json`, `trackers.json`, `legend.json` and `equipment.json`, and caches the result for an hour.
2. It takes the later of `Trackers.LastLogout` and `Trackers.LastLogin`.
3. It calls `guild.RecordLastSeen(name, thatTime)`. The periodic save then writes it, so that member's save is never read for this again.

If the save can't be read, or has neither time, the line shows `unknown`. The next roster view tries again. `AislingFacadeCache` already logs a warning for a failed load.

The one-time reads block the dialog while they run, like the existing profile view of an offline player. A guild of 50 members who all idled since the update pays that cost on one roster open, then never again.

## 3. Files

Chaos-Server:

- `Chaos.DarkAges/Definitions/Enums.cs`: `GuildPermission.SetMessageOfTheDay`.
- `Chaos/Collections/GuildPermissionRules.cs`: `ForTier` and `Label`.
- `Chaos.Schemas/Guilds/GuildSchema.cs`: `MessageOfTheDay` and `LastSeen`.
- `Chaos.Schemas/Guilds/GuildMessageOfTheDaySchema.cs`: new.
- `Chaos/Collections/Guild.cs`: the message record, the methods above, and the last-seen changes in `AddMember`, `TryLeave`, `TryKickMember` and `Initialize`.
- `Chaos/Collections/GuildMessageOfTheDay.cs`: new record.
- `Chaos/Collections/GuildLastSeen.cs`: new static `Describe` helper.
- `Chaos/Services/MapperProfiles/GuildMapperProfile.cs`: both new fields.
- `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs`: the login line and the logout record.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs`: the new menu option.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMessageOfTheDayScript.cs`: new.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberRosterScript.cs`: status lines, online count and the one-time read.
- `Tests/Chaos.Tests/`: the tests below.

Unora:

- The six dialog templates above.
- `docs/guild-hall-ideas.md`: mark ideas 44 and 45 built.

Chaos.Client:

- The `Chaos-Server` submodule pointer, after the server merge. No client code or `CLIENT_VERSION` change.

## 4. Testing

TUnit tests in `Tests/Chaos.Tests/`, using the `MockGuild` and `MockAisling` helpers and Moq, like `GuildPermissionsScriptTests`:

- **Permission rules:** `ForTier(1..3)` ends with `SetMessageOfTheDay`. `Label` returns the menu text. `COUNCIL_DEFAULT` doesn't include it. A rank file with no permissions loads without it.
- **Guild message:** set, read back and clear. Empty text and text over 150 characters throw.
- **Mapping:** a guild with a message and last-seen entries round-trips through `GuildMapperProfile`. A `GuildSchema` with neither field loads with no message and an empty list. A saved message over 150 characters still loads. After `Initialize`, last-seen names for non-members are gone.
- **Last seen on the guild:** `RecordLastSeen` keeps the later time and ignores non-members. `AddMember` records. `TryLeave` and `TryKickMember` remove.
- **`GuildLastSeen.Describe`:** online, unknown, 5 minutes ("under an hour ago"), exactly 1 hour ("1 hour ago"), 23 hours 59 minutes ("23 hours ago"), exactly 24 hours ("1 day ago"), 3 days ("3 days ago"), and a time in the future from clock drift ("under an hour ago").
- **Message script:** a member without the switch sees no Change or Clear option. The leader does. Blank input changes nothing. Over-long input is refused. Accepting saves through the mocked `IStore<Guild>`. A rank that loses the switch before accepting is refused.
- **Roster script:** the header online count and one status line per member. A real `AislingFacadeCache` built over a mocked `IFacadeStore<Aisling>` shows one save read for a member with no entry, then none on the next view.

In-game check after merging:

1. As leader, set a message at Quill. Online members see the guild chat line.
2. Log a member out and in. They see the line after the "appeared online" notices.
3. As a Council member without the switch, open Message of the Day. You see the text but no Change or Clear.
4. As leader, turn the switch on for Council at Aricin. The Council member can now change it.
5. Clear the message. The next login shows no line.
6. Open the roster. The header shows the online count, online members show `online`, and an idle member shows a day count.
7. Restart the server. The message, the switch and the roster times are still there.

## Out of scope

- Exact last-seen dates, or hiding last-seen from lower ranks.
- A message history or scheduled messages (see idea 46, the event scheduler).
- Showing the message anywhere other than login, the broadcast on change, and Quill and Aricin.
- Rank colors (idea 27).
