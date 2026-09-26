# Guild applications

Designed 2026-09-25. It builds idea 43 (guild applications) from `Unora/docs/guild-hall-ideas.md` and answers the player thread "Admitting from Guild House". It is the second of three guild sub-projects picked that day. The roster last-seen and message of the day came first; the librarian (ideas 10 and 47) follows with its own spec.

## Goal

- A player without a guild can apply to guilds from a board outside the guild hall, without anyone else being there.
- Officers review applications later, from the hall or the tavern, even while the applicant is offline.
- A player accepted by more than one guild chooses which one to join.

Today the only way in is at Aricin in the Abel tavern. An officer with the admit permission and the new player must both stand there at once, and the player clicks yes on a prompt. That stays as it is.

## Decisions

| Question | Decision |
|---|---|
| Where players apply | A new wooden notice board at (5,19) on Abel Port Way, left of the guild hall door. |
| Board art | The same free-standing board art (foreground pieces 737/738) as the board at (8,12). The map file changes; the client downloads it from the server, so there is no client patch. |
| How many guilds at once | Up to 5 open applications (waiting or accepted), one per guild. |
| Note | Optional, up to 60 characters, shown to officers through the chat filter. |
| Who reviews | Ranks with the existing `Admit` permission, at Quill and Aricin, under Members. |
| Accepting while the applicant is offline | The application becomes an offer. The applicant chooses at the next login or at the board. |
| Choosing between offers | A menu at login lists every offer, with "Decide later". The board shows the same offers. |
| Expiry | 14 days: from sending for a waiting application, from acceptance for an offer. |
| Storage | One server-wide file, `Data/LocalStorage/GuildApplicationState.json` (approach 1). Guild files don't change. |
| Client changes | None. `CLIENT_VERSION` stays the same. |

## 1. Storage and rules

### State file

`Chaos/Models/World/GuildApplicationState.cs`, saved through `IStorage<GuildApplicationState>` like `GuildCloakState`. `IStorage<T>` is already registered as an open generic.

```csharp
public sealed class GuildApplicationState
{
    //applicant name -> that applicant's applications
    public Dictionary<string, List<GuildApplication>> Applicants { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GuildApplication
{
    public string GuildName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    public string? AcceptedBy { get; set; }
    public DateTime? AcceptedAt { get; set; }
}
```

- An application with `AcceptedBy` set is an offer.
- Both types go in `SerializationContext`, like `GuildCloakState`.
- System.Text.Json replaces the dictionary with a case-sensitive one on load. The service calls an `EnsureCaseInsensitive` step once, as `GuildCloakService` does.
- The note is stored as typed (trimmed). It is filtered when shown, so a later chat filter change applies to old notes too.

### Service

`Chaos/Services/GuildApplications/GuildApplicationService.cs`, a singleton registered next to `GuildCloakService`. It holds the only lock around the state, saves after every change, and takes a `TimeProvider` so tests can move the clock. The dialogs, the board and the login code only call it.

Constants: `MAX_OPEN_APPLICATIONS = 5`, `NOTE_MAX_LENGTH = 60`, `LIFETIME = 14 days`.

| Method | What it does |
|---|---|
| `Apply(applicant, guildName, note)` | Adds a waiting application. Refuses a second one to the same guild, a sixth open one, and a note over 60 characters. Returns a result enum. |
| `Withdraw(applicant, guildName)` | Removes that application, waiting or accepted. Also used for "Turn down". |
| `ForApplicant(applicant)` | Copies of the applicant's live applications, sorted by guild name. |
| `WaitingFor(guildName)` | Copies of the guild's waiting applications, oldest first, with applicant names. Offers are left out. |
| `CountWaiting(guildName)` | For the menu label and the login line. |
| `Accept(guildName, applicant, acceptedBy)` | Turns a waiting application into an offer. A second accept of the same application changes nothing and reports "already accepted". |
| `Decline(guildName, applicant)` | Removes a waiting application. |
| `TryTakeOffer(applicant, guildName, out acceptedBy)` | If a live offer from that guild exists, removes **all** of the applicant's applications and returns who accepted. |
| `ClearApplicant(applicant)` | Removes all of an applicant's applications. Used when they turn out to be in a guild. |
| `RemoveGuild(guildName)` | Removes every application to that guild, waiting or accepted. Called on disband. |
| `ListGuilds()` | Names of the folders under `GuildStoreOptions.Directory` that hold a `guild.json`, sorted ignoring case. Folders without one (such as a half-deleted guild) are skipped. |

Every call first drops expired entries: a waiting application older than 14 days from `AppliedAt`, or an offer older than 14 days from `AcceptedAt`. It saves if anything was dropped. There is no timer.

Every change is logged with `Topics.Entities.Guild`: apply, withdraw, accept, decline, join, and disband cleanup.

### Joining with an offline officer

`Guild.AddMember(Aisling aisling, Aisling by)` names `by` in the guild's message, so it needs `by` online. A new overload, `AddMember(Aisling aisling, string acceptedByName)`, does the same join. Its message to online members is "*Name* has joined the guild, accepted by *Officer*." The existing overload keeps its message and still leaves out `by`. Both share one private method for the join itself.

### Rollback

The state file is new, so an older server ignores it and loads every guild normally. The Unora data (the board's script key and the new dialogs' script keys) needs this server, as with every earlier guild feature, so ship the two together.

## 2. The board

### Map

`Unora/Data/Configuration/MapData/lod180.map` (Abel Port Way, 16×24): tile (5,19) keeps its background (12406) and gets left foreground 737 and right foreground 738. Those pieces are walls in `sotp.dat`, so nobody can stand on the board, and the tile is off the path to the door at (5,17). The client compares the map checksum on entry and downloads the new map from the server.

### Clickable spot

`Abel_port_way/reactors.json` gets three entries like this one, at (5,19), (4,18) and (4,19):

```json
{
  "scriptKeys": ["GuildApplicationBoard"],
  "scriptVars": {},
  "shouldBlockPathfinding": true,
  "source": "(5, 19)"
}
```

The client turns a click into the ground tile under the cursor, and sends it only when that tile has rendered wall art. The board's image is taller than its tile, so most of it lies over hall-wall tiles behind it. Measured on the render: 18% of the board's pixels map to (5,19), 34% to (4,18) and 12% to (4,19), and those three have rendered art. The rest map to tiles whose only foreground is the invisible placeholder `1`, which the client never sends. So the two hall-wall tiles carry the same script, and clicking the hall wall just there opens the board too. (Added while planning.)

`Chaos/Scripting/ReactorTileScripts/Temauir/GuildApplicationBoardScript.cs` handles `OnClicked`. Like `CaravanSiteScript`, it creates a `blank_merchant` at the board's point and displays `generic_guild_apply_initial` from it.

## 3. The applicant's menus

A new `GuildApplyScript` (script key `GuildApply`, deriving from `GuildScriptBase`) runs these dialogs. They live in Unora under `Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications/`, and every one is `contextual: true` so the picked guild and the page number carry forward in `Dialog.Context`.

| Template key | Type | What it does |
|---|---|---|
| `generic_guild_apply_initial` | Menu | For a guildless player: "Join a guild (N offers)" when they have offers, then "Apply to a guild" and "My applications". For a player in a guild: "You're already in a guild. Leave it before applying to another." with no options, and their applications are cleared. |
| `generic_guild_apply_list` | Menu | Up to 10 guild names per page from `ListGuilds()`, leaving out guilds already applied to, plus "Next page" and "Previous page" when needed. At 5 open applications it says so and offers nothing. With no guilds to show: "There are no other guilds to apply to." |
| `generic_guild_apply_note` | DialogTextEntry, `textBoxLength` 60 | "Add a short note for the officers, or leave it blank." |
| `generic_guild_apply_confirmation` | Menu | "Apply to {Guild}?" plus the note if there is one. Yes / No. |
| `generic_guild_apply_sent` | Normal | Calls `Apply` and says "Your application to {Guild} has been sent. Watch your mail." |
| `generic_guild_apply_mine` | Menu | One option per application: "{Guild} - waiting, 9 days left" or "{Guild} - accepted!". "You have no applications." when empty. |
| `generic_guild_apply_waiting` | Menu | "Your application to {Guild} is waiting for an answer." Withdraw / Back. |
| `generic_guild_apply_withdrawn` | Normal | "You withdrew your application to {Guild}." |
| `generic_guild_apply_offers` | Menu | "These guilds have accepted you. Pick one to join." with one option per offer, then "Decide later". Used by "Join a guild" and by the login pop-up. |
| `generic_guild_apply_offer` | Menu | "{Guild} accepted you. Join now? Your other applications will be withdrawn." Join / Turn down / Back. |
| `generic_guild_apply_joined` | Normal | Does the join and says "You joined {Guild}!" |
| `generic_guild_apply_turned_down` | Normal | Calls `Withdraw` and says "You turned down {Guild}." |

Rules:

- When a list screen is drawn, the guild names its options stand for are saved in that dialog's `Context`. The pick is looked up in that saved list, not in a freshly built one, so a list that changed between two clicks can't pick the wrong guild. Reading the option text instead would not work for "My applications", whose lines carry the state ("Alpha - waiting, 9 days left"), or for a guild named like a paging option. (Changed while implementing; see the plan's ledger ruling.)
- The note is trimmed. A note over 60 characters is refused with "Keep the note to 60 characters." The client box already stops at 60, so this only guards against a modified client.
- "Apply" re-checks everything the service checks, and that the guild still exists (`GuildStore.Exists`).
- On a successful apply, every online member of that guild with `Admit` gets an active message: "*Name* has applied to join the guild. Review it at Quill or Aricin."
- **Join** checks, in order: the player is still guildless; the guild still exists ("That guild no longer exists." and the offer is withdrawn); `TryTakeOffer` succeeds ("That offer has expired." otherwise). Then it loads the guild, calls `AddMember(aisling, acceptedBy)` and saves the guild with `GuildStore.Save`.
- Guild names and notes are put into the dialog text with `InjectTextParameters`, as the message of the day confirmation does.

### Login pop-up

In `DefaultAislingScript.OnLogin`:

- A player **in** a guild has their applications cleared with `ClearApplicant`.
- A guildless player with live offers gets `generic_guild_apply_offers`.
- The Terminus "welcome back" menu (`terminus_homeoptions`) is due after two hours away. Only one dialog can be open, and a second `Display` replaces the first. So when both are due, the offers menu is shown from the `terminus` merchant and the Terminus menu is not shown separately. `GuildApplyScript` sees that the dialog's source is the `terminus` merchant and points "Decide later" at `terminus_homeoptions`. Otherwise "Decide later" closes the dialog.
- A member whose rank has `Admit` gets, after the message of the day, an active message when the guild has waiting applications: "3 guild applications are waiting. Review them at Quill or Aricin." It says "1 guild application is" for one.

## 4. The officers' menus

`GuildMemberManagementScript` adds "Applications (N)" (`generic_guild_applications_initial`) for anyone whose rank has `Admit`, at both Quill and Aricin, just after "Roster". With nothing waiting it shows "Applications".

A new `GuildApplicationReviewScript` (script key `GuildApplicationReview`) runs these dialogs, in the same Unora folder:

| Template key | Type | What it does |
|---|---|---|
| `generic_guild_applications_initial` | Menu | Up to 10 waiting applicants per page, oldest first: "{Name} - level 45 Priest - 2 days ago". "Next page" / "Previous page" when needed. "No one has applied." when empty. |
| `generic_guild_applications_view` | Menu | "{Name}, level 45 Priest, applied 2 days ago." and "Note: …" when there is one. Accept / Decline / Back. |
| `generic_guild_applications_accepted` | Normal | Accepts and says "You accepted {Name}. They'll get a letter and can join within 14 days." |
| `generic_guild_applications_declined` | Normal | Declines and says "You declined {Name}. They'll get a letter." |

Rules:

- Every step re-checks guild membership and `Admit`, like `GuildMemberAdmitScript`. A rank that loses it mid-dialog gets "Your rank can't admit new members."
- Level and class come from `AislingFacadeCache.Get(name)`. It returns the live `Aisling` for an online player and reads the save for an offline one. If the save can't be read, the line shows the name and age only.
- The note is shown through `IChatFilterService.FilterMessage`.
- **Before accepting,** the script checks the applicant isn't in a guild. It uses the `Aisling` from `AislingFacadeCache.Get(name)`: the applicant counts as in a guild when `Aisling.Guild` is set and that guild still lists them (`Guild.HasMember`). So a player kicked while offline counts as guildless. If they are in a guild: "{Name} has already joined another guild.", and `ClearApplicant` runs.
- An offline applicant's cached save can be up to an hour old, so a player who joined a guild and logged out within that hour can still be accepted. That offer is harmless: their next login finds them in a guild and clears it.
- **Letters** go through `IStore<MailBox>` like `CasinoLotteryDrawingScript`. The author is "Aricin" and the subject is "Guild application".
  - Accepted: "{Guild} accepted your guild application. To join, click the guild board outside the guild hall on Abel Port Way, or log in again to choose. The offer lasts 14 days."
  - Declined: "{Guild} declined your guild application."
- An online applicant who is accepted also gets an active message: "{Guild} accepted your application. Open the guild board on Abel Port Way to join." The offers menu does not pop up mid-game.
- The accepted offer stands even if the officer's rank later loses `Admit`.

### Disband

`GuildDisbandScript` calls `GuildApplications.RemoveGuild(guildName)` next to the existing `GuildCloaks.RemoveGuild(guildName)`. Applicants are not told.

## 5. Edge cases

| Case | What happens |
|---|---|
| An applicant joins a guild some other way (the tavern) | Their applications are cleared at their next login or board visit, or when an officer tries to accept them. |
| Two officers accept the same application at once | The service lock makes the second a no-op; the second officer sees "{Name} was already accepted." |
| The applicant picks a guild that disbanded after accepting | `RemoveGuild` already removed the offer, so it isn't listed. If the menu was open at the time, Join says "That guild no longer exists." |
| A character is renamed or deleted | Applications are keyed by name, so the old ones expire after 14 days. |
| An empty folder under the guild directory | `ListGuilds` skips it. |
| More than 17 guilds or applicants | Plain menus don't scroll in the client, so both lists page 10 at a time. |

## 6. Files

Chaos-Server:

- `Chaos/Models/World/GuildApplicationState.cs`: new (`GuildApplicationState`, `GuildApplication`).
- `Chaos/Services/GuildApplications/GuildApplicationService.cs`: new, plus its result enums.
- `Chaos/SerializationContext.cs`: the two new types.
- `Chaos/Extensions/ServiceCollectionExtensions.cs`: register the service.
- `Chaos/Collections/Guild.cs`: the `AddMember` overload.
- `Chaos/Scripting/ReactorTileScripts/Temauir/GuildApplicationBoardScript.cs`: new.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplyScript.cs`: new.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildApplicationReviewScript.cs`: new.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs`: the Applications option.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs`: disband cleanup.
- `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs`: the login steps.
- `Tests/Chaos.Tests/GuildApplications/`: the tests below.

Unora:

- `Data/Configuration/MapData/lod180.map`: the board at (5,19).
- `Data/Configuration/MapInstances/Temuair/Towns/Abel/Abel_port_way/reactors.json`: the three clickable spots.
- The 16 dialog templates above, in `Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildApplications/`.
- `docs/guild-hall-ideas.md`: mark idea 43 built, and describe applications under "What guild halls do today".

Chaos.Client:

- The `Chaos-Server` submodule pointer, after the server merge. No client code change.

## 7. Testing

TUnit tests in `Tests/Chaos.Tests/GuildApplications/`, using `MockGuild`, `MockAisling`, Moq and a fake `TimeProvider`, like the guild cloak and message of the day tests:

- **Service rules:** apply, then the same guild again is refused; a sixth open application is refused; a 61-character note is refused; withdraw removes it.
- **Expiry:** a waiting application is gone after 14 days and one minute and still there at 13 days; an offer counts 14 days from acceptance, not from applying.
- **Accept and decline:** accept makes an offer; a second accept reports "already accepted"; decline removes; `WaitingFor` leaves offers out; `CountWaiting` matches.
- **Taking an offer:** `TryTakeOffer` removes every application of that applicant and returns who accepted; it fails for an expired offer and for a guild with no offer.
- **Cleanup:** `RemoveGuild` removes waiting and accepted entries for that guild only; `ClearApplicant` removes all of one applicant's.
- **Storage:** the state round-trips through the serializer, and names match ignoring case after load.
- **`ListGuilds`:** over a temporary folder, it lists folders with a `guild.json`, skips an empty one, and sorts ignoring case.
- **`Guild.AddMember` by name:** the member is added at the lowest rank, and online members get the "accepted by" line.
- **Apply script:** a guild member sees the "already in a guild" text; the list leaves out guilds already applied to and pages at 10; join refuses when the guild no longer exists.
- **Review script:** a rank without `Admit` sees no Applications option; accepting an applicant who is in a guild is refused and clears them; accept and decline each send one letter through the mocked `IStore<MailBox>`.

The full suite must pass, apart from the two failures already known on master (`GiveAbility`, `OnItemDroppedOn` stackable).

In-game check after merging (two accounts: an officer and a guildless alt):

1. Walk to Abel Port Way. The board stands left of the guild hall door, and you can't walk onto it. Clicking its lower half opens the menu, and so does the hall wall just behind it.
2. As the alt, apply to guild A with a note, and to guild B without one.
3. An online officer of A sees the "has applied" line.
4. As A's officer, open Quill → Members → Applications (1). The alt shows with level, class and the note. Accept. The alt (online) gets the line and a letter.
5. Log the alt out. As B's officer, accept at Aricin. Log the alt in: the offers menu lists A and B. Pick Decide later.
6. At the board, "Join a guild (2 offers)" shows. Join B. A's offer is gone, and B's guild chat shows "has joined the guild, accepted by …".
7. With a second alt, apply and have an officer decline. The letter arrives.
8. Give an alt an offer and keep it offline for over two hours (or set its `LastLogout` back). On login the offers menu shows first, and Decide later opens the Terminus menu.
9. A rank without admit sees no Applications option. A member whose rank can admit sees the waiting count at login.
10. Restart the server. Waiting applications and offers are still there.
11. Disband a test guild that has a waiting application. It vanishes from the alt's list.

## Out of scope

- A recruiting on/off switch or a recruiting blurb per guild. Any guild can be applied to, and officers can decline.
- Applying from anywhere other than the board, or guilds inviting offline players.
- Showing officers the offers they've made that are still waiting on the applicant.
- A guild directory with banners and renown (idea 20).
