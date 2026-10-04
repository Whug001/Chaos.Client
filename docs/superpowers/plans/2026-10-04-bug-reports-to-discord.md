# In-game Bug Reports to Discord Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A report sent from F1 → Terminus → Report a bug gets a player-written title and is posted by the UnoraReader bot to the Discord bug forum straight away, with its screenshot and forum tag.

**Architecture:** The client window gains a Title box, and the submit packet gains a `Title` string. The game server validates it and stores it in `report.md`. Once a report is saved, `BugReportService` queues it on a hosted `BugReportDiscordQueue`. The queue calls Discord's REST API through `DiscordRestClient` (plain `HttpClient`, no gateway login), retries 429s and 5xx errors, writes `discord_thread`/`discord_url` (or `discord_error`) into `report.md`, and tells the player. The same REST client replaces the never-working `DiscordSocketClient` admin-activity messages. The file server's Bug reports page links to the post.

**Tech Stack:** C# / .NET 10, TUnit + FluentAssertions + Moq (server tests), TUnit (client tests), MonoGame UI (client), ASP.NET + vanilla JS (file server), Discord REST API v10.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-04-bug-reports-to-discord-design.md`

## Global Constraints

- Title: 5 to 80 characters after trimming (`BugReportProtocol.MIN_TITLE_CHARS = 5`, `MAX_TITLE_CHARS = 80`). Client and server both enforce it.
- The submit packet writes `Title` with `WriteString8` immediately after `Category`; `ReadString8` in the same place.
- `CONSTANTS.CLIENT_VERSION` goes up by exactly one from its value on server `master` when Task 1 starts (770 on 2026-10-04 → 771).
- Discord thread name: `<Character>: <title>`, at most 100 characters.
- Discord message content: at most 2,000 characters, sent with `allowed_mentions: { "parse": [] }`.
- Category → forum tag lookup uses `BugReportProtocol.FolderName(category)` as the key into `BugReportDiscordOptions.TagIds` (`skill-spell`, `item`, `map-warp`, `npc`, `quest`, `monster-combat`, `client-ui`, `other`). A missing or empty entry means no tag.
- Only the title, description, category, map name, coordinates, client build, character name and screenshot are posted. `server-log.jsonl`, `world.json`, `client.json` and `character/` are never posted.
- Posting is off when `BotToken` or `ForumChannelId` is empty. That is the default.
- Orange-bar text ≤ 45 characters. Success: `Your report is on Discord in #bug-reports.` Failure: `Report saved. Discord post failed.` Missing title: `Please give your report a title.`
- Chat-panel lines (`SendActiveMessage`): success `Your bug report "<title>" was posted on Discord in #bug-reports. Add more screenshots or details there.` Failure `Your bug report was saved and staff will see it, but it could not be posted on Discord right now.`
- Retry: 429 waits `retry_after`; 5xx or network error waits 5 s, then 30 s, then 2 min (and 2 min after that) up to `MaxAttempts` (default 5); any other 4xx is not retried.
- The bot token never goes in a tracked file. It lives in the untracked `Chaos/appsettings.discord.json`.
- Shared checkouts: other Claude sessions use `Chaos.Client`, `Chaos.Client/Chaos-Server` and `Chaos.FileServer`. Work only in the worktrees from Task 0. Never stash, reset, clean or `add -A`. Stage by explicit path.
- Commit strategy is at-end: implementers do NOT commit. Task 9 makes one commit per repo.
- Code comments only where the reason is not obvious. No explanatory comments in test code.

**User decisions (already made):**
- "Right away" — posts go up as soon as the report is saved, with no staff review.
- "Include it" — the screenshot is posted; the window warns it is public.
- "Character name" — the post title names the character.
- "Chat message only" — no open-in-Discord button; orange bar plus chat line.
- "Maybe we should add a title to the bug report panel" — the player writes the title.
- "yes" — spec approved 2026-10-04.

---

## File map

**Server (`worktrees/discord-reports-server`, branch `feat/bug-reports-discord`):**
- Modify `Chaos.DarkAges/Definitions/BugReportProtocol.cs` — title limits.
- Modify `Chaos.DarkAges/Definitions/CONSTANTS.cs` — `CLIENT_VERSION` + 1.
- Modify `Chaos.Networking/Entities/Client/BugReportInteractionArgs.cs` — `Title`.
- Modify `Chaos.Networking/Converters/Client/BugReportInteractionConverter.cs` — read/write `Title`.
- Modify `Chaos/Services/BugReports/BugReportOptions.cs`, `BugReportLedger.cs`, `BugReportMarkdown.cs`, `BugReportStore.cs`, `BugReportService.cs`.
- Create `Chaos/Services/Discord/BugReportDiscordOptions.cs`, `DiscordRestClient.cs`, `DiscordAdminLog.cs`.
- Create `Chaos/Services/BugReports/BugReportPostFormatter.cs`, `BugReportDiscordQueue.cs`.
- Modify `Chaos/Extensions/ServiceCollectionExtensions.cs`, `Chaos/Program.cs`, `Chaos/Chaos.csproj`, `Chaos/appsettings.json`, `.gitignore`.
- Create `Chaos/appsettings.discord.example.json`.
- Modify `Chaos/Scripting/DialogScripts/Temuair/Generic/AdminTrinketScript.cs`, `Chaos/Scripting/DialogScripts/Temuair/Arena/ArenaUndergroundScript.cs`.
- Tests in `Tests/Chaos.Tests/BugReports/` and `Tests/Chaos.Tests/Networking/`, plus new `Tests/Chaos.Tests/Discord/`.

**Client (`worktrees/discord-reports-client`, branch `feat/bug-reports-discord`):**
- Modify `Chaos.Client/Systems/BugReportUpload.cs`, `Chaos.Client/Controls/World/Popups/BugReport/BugReportControl.cs`, `Chaos.Client/Screens/WorldScreen.Wiring.cs`.
- Modify `Tests/Chaos.Client.Tests/BugReportUploadTests.cs`.

**File server (`worktrees/discord-reports-fs`, branch `feat/bug-reports-discord`):**
- Modify `src/Chaos.FileServer/BugReports/BugReportModels.cs`, `BugReportMarkdownReader.cs`, `src/Chaos.FileServer/Admin/Pages/bugreports.js`.
- Test in `tests/Chaos.FileServer.Tests/BugReports/`.

**Unora:**
- Create `Unora/Tools/FeedbackSync/forum_tags.py` — prints the bug forum's tag ids for the config file.

## How to run tests

- Server, bug report and Discord tests only (from the server worktree):
  `dotnet build Tests/Chaos.Tests/Chaos.Tests.csproj -v q -nologo && Tests/Chaos.Tests/bin/Debug/net10.0/Chaos.Tests.exe --no-progress --treenode-filter "/*/Chaos.Tests.BugReports/*/*"` (swap the namespace for `Chaos.Tests.Networking` or `Chaos.Tests.Discord`).
- Server, whole suite: the same exe with no filter. Known failure on master: `OnItemDroppedOn_ShouldAddStackableItem_WhenCountIsPositive`.
- `dotnet test` does not work with this SDK. Run the test exe or `dotnet run --project`.
- Client (from the client worktree): `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=../discord-reports-server -- --no-ansi`.
- File server (from its worktree): `dotnet run --project tests/Chaos.FileServer.Tests/Chaos.FileServer.Tests.csproj -- --no-ansi` (if that project uses another runner, check its csproj and use `dotnet test`).

---

### Task 0: Create the worktrees

**Goal:** Three isolated worktrees on branch `feat/bug-reports-discord`, so no shared checkout is touched.

**Files:**
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\discord-reports-server`, `...\discord-reports-client`, `...\discord-reports-fs`

**Acceptance Criteria:**
- [ ] `git -C worktrees/discord-reports-server branch --show-current` prints `feat/bug-reports-discord`, based on server `master`.
- [ ] Same for the client worktree (based on Chaos.Client `main`) and the file server worktree (based on its local `master`).
- [ ] The server worktree builds: `dotnet build Chaos/Chaos.csproj -v q -nologo` ends in `Build succeeded.`

**Verify:** `git -C <each worktree> log --oneline -1` and the build above.

**Steps:**

- [ ] **Step 1: Create the worktrees** (from `C:\Users\Michael\Documents\GitHub`)

```bash
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add -b feat/bug-reports-discord ../../worktrees/discord-reports-server master
git -C Chaos.Client -c core.longpaths=true worktree add -b feat/bug-reports-discord ../worktrees/discord-reports-client main
git -C Chaos.FileServer worktree add -b feat/bug-reports-discord ../worktrees/discord-reports-fs master
```

- [ ] **Step 2: Build the server worktree**

Run: `cd worktrees/discord-reports-server && dotnet build Chaos/Chaos.csproj -v q -nologo`
Expected: `Build succeeded.`

```json:metadata
{"files": [], "verifyCommand": "git -C worktrees/discord-reports-server branch --show-current", "acceptanceCriteria": ["three worktrees on feat/bug-reports-discord", "server worktree builds"], "modelTier": "mechanical"}
```

---

### Task 1: Title in the protocol, ledger and report.md (server)

**Goal:** The submit packet carries a title, the server refuses a title outside 5–80 characters, and `report.md` stores the player's title.

**Files:**
- Modify: `Chaos.DarkAges/Definitions/BugReportProtocol.cs`
- Modify: `Chaos.DarkAges/Definitions/CONSTANTS.cs:23`
- Modify: `Chaos.Networking/Entities/Client/BugReportInteractionArgs.cs`
- Modify: `Chaos.Networking/Converters/Client/BugReportInteractionConverter.cs`
- Modify: `Chaos/Services/BugReports/BugReportOptions.cs`, `BugReportLedger.cs`, `BugReportMarkdown.cs`, `BugReportService.cs`
- Modify: `Chaos/appsettings.json` (`BugReportOptions` section)
- Test: `Tests/Chaos.Tests/Networking/BugReportPacketConverterTests.cs`, `Tests/Chaos.Tests/BugReports/BugReportLedgerTests.cs`, `BugReportMarkdownTests.cs`, `BugReportSamples.cs`, `BugReportServiceTests.cs`

**Acceptance Criteria:**
- [ ] `Submit_round_trips` includes `Title = "Stuck in the inn wall"` and passes.
- [ ] A submit whose trimmed title is under 5 or over 80 characters returns `SubmitStatus.BadTitle`, the report stays open, and the player gets `Please give your report a title.`
- [ ] `report.md` front matter `title:` is the player's title on one line; with an empty title it falls back to the first 60 characters of the description.
- [ ] `CLIENT_VERSION` is one higher than before.
- [ ] All `Chaos.Tests.BugReports` and `Chaos.Tests.Networking` tests pass.

**Verify:** `Tests/Chaos.Tests/bin/Debug/net10.0/Chaos.Tests.exe --no-progress --treenode-filter "/*/Chaos.Tests.BugReports/*/*"` → 0 failed; same for `Chaos.Tests.Networking`.

**Steps:**

- [ ] **Step 1: Write the failing tests**

In `BugReportPacketConverterTests.Submit_round_trips`, add `Title = "Stuck in the inn wall",` after `Category = BugReportCategory.MapWarp,`.

In `BugReportLedgerTests.cs`, give the existing `SubmitArgs` helper a `string title = "Stuck in the inn wall"` parameter that sets `Title = title` on the args it builds, then add:

```csharp
    //formatter:off
    [Test]
    [Arguments("abcd")]
    [Arguments("   abc   ")]
    //formatter:on
    public async Task Short_titles_are_refused_and_the_report_stays_open(string title)
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        ledger.Submit(id, "Bob", SubmitArgs(id, title: title), T0).Status.Should().Be(SubmitStatus.BadTitle);
        ledger.Submit(id, "Bob", SubmitArgs(id), T0).Status.Should().Be(SubmitStatus.Complete);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Titles_over_eighty_characters_are_refused()
    {
        var ledger = NewLedger();
        var id = Open(ledger);

        ledger.Submit(id, "Bob", SubmitArgs(id, title: new string('a', 81)), T0).Status.Should().Be(SubmitStatus.BadTitle);

        await Task.CompletedTask;
    }
```

In `BugReportSamples.Input`, add a `string title = ""` parameter and set `Title = title` in the initializer. In `BugReportMarkdownTests.cs`, add:

```csharp
    [Test]
    public async Task Title_is_the_players_title_when_given()
    {
        var text = BugReportMarkdown.Render(BugReportSamples.Input(title: "  Stuck in\nthe inn wall  "));

        text.Should().Contain("title: \"Stuck in the inn wall\"\n");

        await Task.CompletedTask;
    }
```

In `BugReportServiceTests.Submit`, add `Title = "Stuck in the inn wall",` after `Category`.

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build Tests/Chaos.Tests/Chaos.Tests.csproj -v q -nologo`
Expected: build errors — `Title` and `SubmitStatus.BadTitle` do not exist.

- [ ] **Step 3: Protocol, args and converter**

`BugReportProtocol.cs`, after `MAX_DESCRIPTION_CHARS`:

```csharp
    /// <summary>The shortest title the server accepts, after trimming.</summary>
    public const int MIN_TITLE_CHARS = 5;

    /// <summary>The longest title the server accepts, after trimming. With a 12-character name it fits Discord's 100-character thread name.</summary>
    public const int MAX_TITLE_CHARS = 80;
```

`BugReportInteractionArgs.cs`, after `Category`:

```csharp
    /// <summary>The player's one-line summary, used as the Discord post title.</summary>
    public string Title { get; set; } = string.Empty;
```

`BugReportInteractionConverter.cs`: in `Deserialize` Submit, after `args.Category = ...` add `args.Title = reader.ReadString8();`. In `Serialize` Submit, after `writer.WriteByte((byte)args.Category);` add `writer.WriteString8(args.Title);`.

`CONSTANTS.cs`: raise `CLIENT_VERSION` by one (770 → 771 if it is still 770).

- [ ] **Step 4: Options, ledger and service**

`BugReportOptions.cs`, after `MaxDescriptionChars`:

```csharp
    public int MinTitleChars { get; set; } = BugReportProtocol.MIN_TITLE_CHARS;
    public int MaxTitleChars { get; set; } = BugReportProtocol.MAX_TITLE_CHARS;
```

`appsettings.json`, in `BugReportOptions` after `"MaxDescriptionChars": 1000,` add `"MinTitleChars": 5,` and `"MaxTitleChars": 80,`.

`BugReportLedger.cs`: add to `SubmitStatus` after `BadDescription`:

```csharp
    /// <summary>The trimmed title is outside the allowed length. The report stays open.</summary>
    BadTitle
```

and in `Submit`, between the category check and the description check:

```csharp
        var titleLength = args.Title.Trim().Length;

        if ((titleLength < options.MinTitleChars) || (titleLength > options.MaxTitleChars))
            return new SubmitOutcome<TContext>(SubmitStatus.BadTitle, null);
```

`BugReportService.Submit`, add a case before `AwaitingPicture`:

```csharp
            case SubmitStatus.BadTitle:
                aisling.SendOrangeBarMessage("Please give your report a title.");

                break;
```

and in `FinishAsync`'s `BugReportMarkdown.Input` initializer add `Title = submit.Title,`.

- [ ] **Step 5: Markdown title**

`BugReportMarkdown.Input`: add `public string Title { get; init; } = string.Empty;`. In `Render`, replace the title line with:

```csharp
        sb.Append($"title: {YamlQuote(HeaderTitle(input.Title, text))}\n");
```

and add:

```csharp
    /// <summary>The player's title on one line, or the start of the description when there is none.</summary>
    public static string HeaderTitle(string title, string description)
    {
        var line = OneLine(title);

        return line.Length > 0 ? line : Title(description);
    }
```

- [ ] **Step 6: Run the tests to see them pass**

Run the `Chaos.Tests.BugReports` and `Chaos.Tests.Networking` filters from "How to run tests".
Expected: 0 failed.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/BugReportProtocol.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking/Entities/Client/BugReportInteractionArgs.cs", "Chaos.Networking/Converters/Client/BugReportInteractionConverter.cs", "Chaos/Services/BugReports/BugReportOptions.cs", "Chaos/Services/BugReports/BugReportLedger.cs", "Chaos/Services/BugReports/BugReportMarkdown.cs", "Chaos/Services/BugReports/BugReportService.cs", "Chaos/appsettings.json"], "verifyCommand": "Tests/Chaos.Tests/bin/Debug/net10.0/Chaos.Tests.exe --no-progress --treenode-filter \"/*/Chaos.Tests.BugReports/*/*\"", "acceptanceCriteria": ["Submit round-trips with Title", "BadTitle under 5 or over 80 trimmed chars", "report.md title is the player's title with description fallback", "CLIENT_VERSION +1", "BugReports and Networking tests pass"], "modelTier": "standard"}
```

---

### Task 2: Title box in the client window

**Goal:** The report window has a required Title box, warns that the report is posted publicly, and sends the title.

**Files:**
- Modify: `Chaos.Client/Systems/BugReportUpload.cs`
- Modify: `Chaos.Client/Controls/World/Popups/BugReport/BugReportControl.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.Wiring.cs` (`SendBugReport`)
- Test: `Tests/Chaos.Client.Tests/BugReportUploadTests.cs`

**Acceptance Criteria:**
- [ ] `BugReportUpload.CanSend(category, title, text)` is true only with a category, a 5–80 character trimmed title and a 10–1,000 character trimmed description.
- [ ] The window shows a one-line Title box between the category grid and the description, cleared on `Open`, `MaxLength` 80.
- [ ] The note reads: `Your character, position and recent server events go to staff. The title, description and picture are posted publicly in the Unora Discord.` and fits without overlapping the Send button.
- [ ] `BugReportSubmission` carries `Title`, and `SendBugReport` sets `Title = submission.Title`.
- [ ] Client tests pass, and the client builds against the server worktree.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=../discord-reports-server -- --no-ansi` → 0 failed.

**Steps:**

- [ ] **Step 1: Write the failing tests** in `BugReportUploadTests.cs` (replace the existing `CanSend` tests' calls with the three-argument form, passing `"Stuck in the inn wall"` as the title, then add):

```csharp
    [Test]
    public async Task CanSend_needs_a_title_of_five_to_eighty_characters()
    {
        BugReportUpload.CanSend(BugReportCategory.Item, "abcd", "A long enough description").Should().BeFalse();
        BugReportUpload.CanSend(BugReportCategory.Item, "  abcde  ", "A long enough description").Should().BeTrue();
        BugReportUpload.CanSend(BugReportCategory.Item, new string('a', 81), "A long enough description").Should().BeFalse();

        await Task.CompletedTask;
    }
```

(Use the assertion style the file already uses; if it uses TUnit's `await Assert.That(...)`, write the same checks that way.)

- [ ] **Step 2: Run the client tests to see them fail**

Expected: compile error, `CanSend` has no three-argument overload.

- [ ] **Step 3: `BugReportUpload.CanSend`**

```csharp
    /// <summary>True when a category is picked, the trimmed title is 5 to 80 characters and the trimmed text is 10 to 1,000.</summary>
    public static bool CanSend(BugReportCategory? category, string title, string text)
    {
        if (category is null)
            return false;

        var titleLength = title.Trim().Length;

        if (titleLength is < BugReportProtocol.MIN_TITLE_CHARS or > BugReportProtocol.MAX_TITLE_CHARS)
            return false;

        var length = text.Trim().Length;

        return length is >= BugReportProtocol.MIN_DESCRIPTION_CHARS and <= BugReportProtocol.MAX_DESCRIPTION_CHARS;
    }
```

- [ ] **Step 4: The window**

In `BugReportControl.cs`:

1. `public sealed record BugReportSubmission(uint ReportId, BugReportCategory Category, string Title, string Description, byte[]? Picture);`
2. Layout constants — insert the title row and push the description down:

```csharp
    private const int TITLE_CAPTION_TOP = CATEGORY_GRID_TOP + (2 * CustomButton.HEIGHT) + GAP + 8;
    private const int TITLE_BOX_TOP = TITLE_CAPTION_TOP + 14;
    private const int TITLE_BOX_HEIGHT = 18;
    private const int DESCRIPTION_CAPTION_TOP = TITLE_BOX_TOP + TITLE_BOX_HEIGHT + 8;
```

   Raise `PANEL_HEIGHT` from 310 by the added height (`TITLE_BOX_HEIGHT + 22`, so 350), plus one more `TextRenderer.CHAR_HEIGHT` for the longer note.
3. A field `private readonly CustomTextBox TitleBox;` built after the category buttons:

```csharp
        Caption("TITLE", LEFT, TITLE_CAPTION_TOP, CONTENT_WIDTH, color: LegendColors.Gray);

        TitleBox = new CustomTextBox
        {
            X = LEFT,
            Y = TITLE_BOX_TOP,
            Width = CONTENT_WIDTH,
            Height = TITLE_BOX_HEIGHT,
            MaxLength = BugReportProtocol.MAX_TITLE_CHARS,
            HintText = "A short summary, like: Stuck in the Mileth inn wall"
        };

        AddChild(TitleBox);
```

   If `CustomTextBox` needs a different single-line height to look right, use the height another single-line `CustomTextBox` in the client uses.
4. The note text becomes `"Your character, position and recent server events go to staff. The title, description and picture are posted publicly in the Unora Discord."` with `note.Height = TextRenderer.CHAR_HEIGHT * 3`.
5. `Open`: add `TitleBox.Text = string.Empty;` next to `DescriptionBox.Text = string.Empty;`.
6. `RefreshSendState` and `Send`: call `BugReportUpload.CanSend(Category, TitleBox.Text, DescriptionBox.Text)`; `Send` builds `new BugReportSubmission(ReportId, Category!.Value, TitleBox.Text.Trim(), DescriptionBox.Text.Trim(), IncludePicture.Checked ? Frame?.Png : null)`.
7. Update the class summary: "Send Report stays dim until a category is picked, a 5-character title and 10 or more characters of description are typed."

- [ ] **Step 5: Send the title** — in `WorldScreen.Wiring.cs` `SendBugReport`, add `Title = submission.Title,` after `Category = submission.Category,`.

- [ ] **Step 6: Run the client tests**

Expected: 0 failed. Also `dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=../discord-reports-server -v q -nologo` → `Build succeeded.`

```json:metadata
{"files": ["Chaos.Client/Systems/BugReportUpload.cs", "Chaos.Client/Controls/World/Popups/BugReport/BugReportControl.cs", "Chaos.Client/Screens/WorldScreen.Wiring.cs", "Tests/Chaos.Client.Tests/BugReportUploadTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=../discord-reports-server -- --no-ansi", "acceptanceCriteria": ["CanSend requires 5-80 char title", "Title box between categories and description, cleared on Open, MaxLength 80", "public-posting note shown without overlap", "submission and packet carry Title", "client tests pass and client builds"], "modelTier": "standard"}
```

---

### Task 3: Discord options and REST client (server)

**Goal:** A small, tested `DiscordRestClient` that creates a forum thread with an optional PNG and sends a channel message, reporting status, ids and `retry_after`.

**Files:**
- Create: `Chaos/Services/Discord/BugReportDiscordOptions.cs`
- Create: `Chaos/Services/Discord/DiscordRestClient.cs`
- Test: `Tests/Chaos.Tests/Discord/DiscordRestClientTests.cs`

**Acceptance Criteria:**
- [ ] `CreateForumThreadAsync` sends `POST https://discord.com/api/v10/channels/{channel}/threads` with `Authorization: Bot <token>`, multipart parts `payload_json` (JSON: `name`, `applied_tags` only when a tag is given, `message.content`, `message.allowed_mentions.parse = []`, `message.attachments[0] = {id: 0, filename: "screenshot.png"}` only with a PNG) and `files[0]` (`image/png`, filename `screenshot.png`) only with a PNG.
- [ ] A 2xx response returns `Ok`, the thread `id` and `guild_id`.
- [ ] A 429 returns `Status 429`, `ShouldRetry`, and `RetryAfter` from the JSON `retry_after` seconds.
- [ ] A 500 returns `ShouldRetry`; a 403 does not; an `HttpRequestException` returns `Status 0` and `ShouldRetry`.
- [ ] `SendMessageAsync` posts JSON `{content, allowed_mentions:{parse:[]}}` to `/channels/{id}/messages`.

**Verify:** `Chaos.Tests.exe --no-progress --treenode-filter "/*/Chaos.Tests.Discord/*/*"` → 0 failed.

**Steps:**

- [ ] **Step 1: Write the failing tests** — `Tests/Chaos.Tests/Discord/DiscordRestClientTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Chaos.Services.Discord;
using FluentAssertions;

namespace Chaos.Tests.Discord;

public class DiscordRestClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, byte[] Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Seen.Add((request, body));

            return await respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static DiscordForumPost Post(byte[]? png = null, string? tag = "111")
        => new("999", "Bob: Stuck in the inn wall", "Walked into the wall.", tag, png);

    [Test]
    public async Task A_created_thread_returns_its_id_and_guild()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json(HttpStatusCode.Created, "{\"id\":\"555\",\"guild_id\":\"777\"}")));
        var client = new DiscordRestClient(new HttpClient(handler), "token");

        var result = await client.CreateForumThreadAsync(Post([0x89, 0x50, 0x4E, 0x47]));

        result.Ok.Should().BeTrue();
        result.Id.Should().Be("555");
        result.GuildId.Should().Be("777");

        var (request, body) = handler.Seen.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Be("https://discord.com/api/v10/channels/999/threads");
        request.Headers.Authorization!.ToString().Should().Be("Bot token");

        var text = Encoding.UTF8.GetString(body);
        text.Should().Contain("payload_json").And.Contain("files[0]").And.Contain("screenshot.png").And.Contain("image/png");
    }

    [Test]
    public async Task The_payload_names_the_thread_tags_it_and_mentions_nobody()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json(HttpStatusCode.Created, "{\"id\":\"1\",\"guild_id\":\"2\"}")));
        var client = new DiscordRestClient(new HttpClient(handler), "token");

        await client.CreateForumThreadAsync(Post());

        var payload = DiscordRestClient.BuildThreadPayload(Post());
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        root.GetProperty("name").GetString().Should().Be("Bob: Stuck in the inn wall");
        root.GetProperty("applied_tags")[0].GetString().Should().Be("111");
        root.GetProperty("message").GetProperty("content").GetString().Should().Be("Walked into the wall.");
        root.GetProperty("message").GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength().Should().Be(0);
        root.GetProperty("message").TryGetProperty("attachments", out _).Should().BeFalse();
    }

    [Test]
    public async Task No_tag_means_no_applied_tags_and_a_png_adds_the_attachment()
    {
        using var doc = JsonDocument.Parse(DiscordRestClient.BuildThreadPayload(Post([1, 2, 3], tag: null)));
        var root = doc.RootElement;

        root.TryGetProperty("applied_tags", out _).Should().BeFalse();
        var attachment = root.GetProperty("message").GetProperty("attachments")[0];
        attachment.GetProperty("id").GetInt32().Should().Be(0);
        attachment.GetProperty("filename").GetString().Should().Be("screenshot.png");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Rate_limited_requests_carry_retry_after()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json((HttpStatusCode)429, "{\"retry_after\":1.5,\"global\":false}")));
        var result = await new DiscordRestClient(new HttpClient(handler), "token").CreateForumThreadAsync(Post());

        result.Status.Should().Be(429);
        result.ShouldRetry.Should().BeTrue();
        result.RetryAfter.Should().Be(TimeSpan.FromSeconds(1.5));
    }

    //formatter:off
    [Test]
    [Arguments(500, true)]
    [Arguments(502, true)]
    [Arguments(403, false)]
    [Arguments(400, false)]
    //formatter:on
    public async Task Server_errors_are_retried_and_other_client_errors_are_not(int status, bool retry)
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json((HttpStatusCode)status, "{\"message\":\"nope\"}")));
        var result = await new DiscordRestClient(new HttpClient(handler), "token").CreateForumThreadAsync(Post());

        result.Ok.Should().BeFalse();
        result.ShouldRetry.Should().Be(retry);
    }

    [Test]
    public async Task A_network_failure_is_status_zero_and_retried()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("down"));
        var result = await new DiscordRestClient(new HttpClient(handler), "token").CreateForumThreadAsync(Post());

        result.Status.Should().Be(0);
        result.ShouldRetry.Should().BeTrue();
    }

    [Test]
    public async Task SendMessage_posts_json_to_the_channel()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, "{\"id\":\"9\"}")));
        var result = await new DiscordRestClient(new HttpClient(handler), "token").SendMessageAsync("123", "hello");

        result.Ok.Should().BeTrue();
        var (request, body) = handler.Seen.Single();
        request.RequestUri!.ToString().Should().Be("https://discord.com/api/v10/channels/123/messages");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("content").GetString().Should().Be("hello");
        doc.RootElement.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength().Should().Be(0);
    }
}
```

- [ ] **Step 2: Run to see it fail** — build errors: `Chaos.Services.Discord` does not exist.

- [ ] **Step 3: Options** — `Chaos/Services/Discord/BugReportDiscordOptions.cs`:

```csharp
namespace Chaos.Services.Discord;

/// <summary>
///     The UnoraReader bot's settings, bound from <c>Options:BugReportDiscordOptions</c> in the untracked
///     <c>appsettings.discord.json</c>. Empty values switch the matching feature off.
/// </summary>
public sealed record BugReportDiscordOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string ForumChannelId { get; set; } = string.Empty;
    public string AdminChannelId { get; set; } = string.Empty;

    /// <summary>Forum tag ids by report folder name, such as <c>map-warp</c>. A missing entry posts with no tag.</summary>
    public Dictionary<string, string> TagIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int MaxAttempts { get; set; } = 5;

    public bool PostsReports => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ForumChannelId);
    public bool PostsAdminActivity => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(AdminChannelId);
}
```

- [ ] **Step 4: REST client** — `Chaos/Services/Discord/DiscordRestClient.cs`:

```csharp
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chaos.Services.Discord;

/// <summary>A forum post: thread name, first message, optional forum tag and optional PNG.</summary>
public sealed record DiscordForumPost(string ChannelId, string Name, string Content, string? TagId, byte[]? Png);

/// <summary>What Discord answered. <see cref="Status" /> is 0 when the request never got an answer.</summary>
public sealed record DiscordResult(bool Ok, int Status, string? Id, string? GuildId, TimeSpan? RetryAfter, string? Error)
{
    public bool ShouldRetry => !Ok && (Status is 0 or 429 || Status >= 500);
}

public interface IDiscordRestClient
{
    Task<DiscordResult> CreateForumThreadAsync(DiscordForumPost post, CancellationToken cancellationToken = default);
    Task<DiscordResult> SendMessageAsync(string channelId, string content, CancellationToken cancellationToken = default);
}

/// <summary>
///     Discord's REST API over plain HTTP. A gateway login is not needed to post, and it would hold a socket open
///     for the life of the server.
/// </summary>
public sealed class DiscordRestClient(HttpClient http, string botToken) : IDiscordRestClient
{
    public const string API_BASE = "https://discord.com/api/v10";
    private const string USER_AGENT = "DiscordBot (https://github.com/Whug001/Unora, 1.0)";

    public static string BuildThreadPayload(DiscordForumPost post)
    {
        var message = new JsonObject
        {
            ["content"] = post.Content,
            ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() }
        };

        if (post.Png is not null)
            message["attachments"] = new JsonArray(new JsonObject { ["id"] = 0, ["filename"] = "screenshot.png" });

        var payload = new JsonObject { ["name"] = post.Name, ["message"] = message };

        if (!string.IsNullOrWhiteSpace(post.TagId))
            payload["applied_tags"] = new JsonArray(post.TagId);

        return payload.ToJsonString();
    }

    public Task<DiscordResult> CreateForumThreadAsync(DiscordForumPost post, CancellationToken cancellationToken = default)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(BuildThreadPayload(post), Encoding.UTF8, "application/json"), "payload_json" }
        };

        if (post.Png is not null)
        {
            var file = new ByteArrayContent(post.Png);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "files[0]", "screenshot.png");
        }

        return SendAsync($"{API_BASE}/channels/{post.ChannelId}/threads", form, cancellationToken);
    }

    public Task<DiscordResult> SendMessageAsync(string channelId, string content, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["content"] = content,
            ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() }
        };

        return SendAsync(
            $"{API_BASE}/channels/{channelId}/messages",
            new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            cancellationToken);
    }

    private async Task<DiscordResult> SendAsync(string url, HttpContent content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        request.Headers.UserAgent.ParseAdd(USER_AGENT);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            var status = (int)response.StatusCode;
            JsonElement? root = null;

            try
            {
                root = JsonDocument.Parse(text).RootElement.Clone();
            } catch (JsonException) { }

            string? Str(string name)
                => root is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.String)
                    ? v.GetString()
                    : null;

            if (response.IsSuccessStatusCode)
                return new DiscordResult(true, status, Str("id"), Str("guild_id"), null, null);

            TimeSpan? retryAfter = null;

            if (root is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("retry_after", out var seconds)
                                                                  && seconds.TryGetDouble(out var value))
                retryAfter = TimeSpan.FromSeconds(value);
            else if (response.Headers.TryGetValues("Retry-After", out var header)
                     && double.TryParse(header.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var headerSeconds))
                retryAfter = TimeSpan.FromSeconds(headerSeconds);

            return new DiscordResult(false, status, null, null, retryAfter, $"HTTP {status}: {Str("message") ?? text}");
        } catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new DiscordResult(false, 0, null, null, null, e.Message);
        }
    }
}
```

- [ ] **Step 5: Run the tests** — `Chaos.Tests.Discord` filter → 0 failed.

```json:metadata
{"files": ["Chaos/Services/Discord/BugReportDiscordOptions.cs", "Chaos/Services/Discord/DiscordRestClient.cs", "Tests/Chaos.Tests/Discord/DiscordRestClientTests.cs"], "verifyCommand": "Tests/Chaos.Tests/bin/Debug/net10.0/Chaos.Tests.exe --no-progress --treenode-filter \"/*/Chaos.Tests.Discord/*/*\"", "acceptanceCriteria": ["multipart thread POST with payload_json and files[0]", "2xx returns id and guild_id", "429 carries retry_after and retries", "5xx and network errors retry, other 4xx do not", "SendMessage posts JSON with empty allowed_mentions"], "modelTier": "standard"}
```

---

### Task 4: Post formatter (server)

**Goal:** Pure functions for the thread name, message content, tag lookup and post URL.

**Files:**
- Create: `Chaos/Services/BugReports/BugReportPostFormatter.cs`
- Test: `Tests/Chaos.Tests/BugReports/BugReportPostFormatterTests.cs`

**Acceptance Criteria:**
- [ ] `ThreadName("Bob", "  Stuck in\nthe wall ")` is `Bob: Stuck in the wall`; any name is cut to 100 characters.
- [ ] `Content` is the sanitized description, a blank line, `Category: Map/Warp · Map: Mileth Inn (12, 7) · Client 0.1.0`, then `Reported in game by Bob. Add more screenshots or details below.`, and never over 2,000 characters.
- [ ] `TagId` finds `map-warp` in the map, and returns null for a missing or blank entry.
- [ ] `PostUrl("777", "555")` is `https://discord.com/channels/777/555`.

**Verify:** `Chaos.Tests.exe --no-progress --treenode-filter "/*/Chaos.Tests.BugReports/BugReportPostFormatterTests/*"` → 0 failed.

**Steps:**

- [ ] **Step 1: Write the failing tests** — `Tests/Chaos.Tests/BugReports/BugReportPostFormatterTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.BugReports;
using FluentAssertions;

namespace Chaos.Tests.BugReports;

public class BugReportPostFormatterTests
{
    [Test]
    public async Task Thread_name_is_the_character_and_the_title_on_one_line()
    {
        BugReportPostFormatter.ThreadName("Bob", "  Stuck in\nthe wall ").Should().Be("Bob: Stuck in the wall");
        BugReportPostFormatter.ThreadName("Bob", new string('a', 200)).Length.Should().Be(100);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Content_has_the_description_details_and_a_call_for_more()
    {
        var content = BugReportPostFormatter.Content("Walked into the wall.\r\n", BugReportCategory.MapWarp, BugReportSamples.World(), "0.1.0");

        content.Should()
               .Be(
                   "Walked into the wall.\n\n"
                   + $"Category: {BugReportProtocol.DisplayName(BugReportCategory.MapWarp)} · Map: Mileth Inn (12, 7) · Client 0.1.0\n"
                   + "Reported in game by Bob. Add more screenshots or details below.");

        BugReportPostFormatter.Content(new string('a', 5000), BugReportCategory.Item, BugReportSamples.World(), "0.1.0")
                              .Length.Should().BeLessThanOrEqualTo(2000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Tag_comes_from_the_folder_name_and_blank_means_none()
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["map-warp"] = "111", ["other"] = "" };

        BugReportPostFormatter.TagId(BugReportCategory.MapWarp, tags).Should().Be("111");
        BugReportPostFormatter.TagId(BugReportCategory.Other, tags).Should().BeNull();
        BugReportPostFormatter.TagId(BugReportCategory.Item, tags).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Post_url_points_at_the_thread()
    {
        BugReportPostFormatter.PostUrl("777", "555").Should().Be("https://discord.com/channels/777/555");

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run to see it fail** — `BugReportPostFormatter` does not exist.

- [ ] **Step 3: Implement** — `Chaos/Services/BugReports/BugReportPostFormatter.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.BugReports;

/// <summary>The text of a report's Discord post. Only what is safe to show every player goes in it.</summary>
public static class BugReportPostFormatter
{
    public const int MAX_THREAD_NAME = 100;
    public const int MAX_CONTENT = 2000;

    public static string ThreadName(string characterName, string title)
    {
        var name = $"{characterName}: {BugReportMarkdown.OneLine(title)}";

        return name.Length <= MAX_THREAD_NAME ? name : name[..MAX_THREAD_NAME];
    }

    public static string Content(string description, BugReportCategory category, WorldSnapshot world, string clientBuild)
    {
        var footer = $"\n\nCategory: {BugReportProtocol.DisplayName(category)} · Map: {world.Map.Name} ({world.Position.X}, {world.Position.Y}) · Client {clientBuild}\n"
                     + $"Reported in game by {world.CharacterName}. Add more screenshots or details below.";

        var text = BugReportMarkdown.Sanitize(description);
        var room = MAX_CONTENT - footer.Length;

        if (text.Length > room)
            text = text[..Math.Max(0, room)];

        return text + footer;
    }

    public static string? TagId(BugReportCategory category, IReadOnlyDictionary<string, string> tagIds)
        => tagIds.TryGetValue(BugReportProtocol.FolderName(category), out var id) && !string.IsNullOrWhiteSpace(id) ? id : null;

    public static string PostUrl(string guildId, string threadId) => $"https://discord.com/channels/{guildId}/{threadId}";
}
```

- [ ] **Step 4: Run the tests** → 0 failed.

```json:metadata
{"files": ["Chaos/Services/BugReports/BugReportPostFormatter.cs", "Tests/Chaos.Tests/BugReports/BugReportPostFormatterTests.cs"], "verifyCommand": "Tests/Chaos.Tests/bin/Debug/net10.0/Chaos.Tests.exe --no-progress --treenode-filter \"/*/Chaos.Tests.BugReports/BugReportPostFormatterTests/*\"", "acceptanceCriteria": ["thread name Character: title on one line, max 100", "content layout and 2000 cap", "tag lookup by folder name, blank is none", "post url format"], "modelTier": "mechanical"}
```

---

### Task 5: Posting queue, report.md update and player messages (server)

**Goal:** A saved report is posted in the background with retries, its result is written into `report.md`, and the player is told; registration and config files are in place.

**Files:**
- Create: `Chaos/Services/BugReports/BugReportDiscordQueue.cs`
- Modify: `Chaos/Services/BugReports/BugReportMarkdown.cs` (`AddHeaderFields`), `BugReportStore.cs` (`AddReportFieldsAsync`), `BugReportService.cs`
- Modify: `Chaos/Extensions/ServiceCollectionExtensions.cs` (`AddBugReports`), `Chaos/Program.cs`, `Chaos/Chaos.csproj`, `.gitignore`
- Create: `Chaos/appsettings.discord.example.json`
- Test: `Tests/Chaos.Tests/BugReports/BugReportDiscordQueueTests.cs`, `BugReportMarkdownTests.cs`, `BugReportServiceTests.cs`

**Acceptance Criteria:**
- [ ] `BugReportMarkdown.AddHeaderFields` inserts `key: "value"` lines just before the closing `---` of the front matter and changes nothing else.
- [ ] On success the queue writes `discord_thread` and `discord_url` into `report.md`, sends the success orange bar and chat line to a connected reporter, and does nothing to a disconnected one.
- [ ] A 429 waits its `RetryAfter`, a 5xx waits 5 s then 30 s then 2 min; a 403 stops at once; after `MaxAttempts` the queue writes `discord_error` and sends the failure orange bar and chat line.
- [ ] `BugReportService` enqueues a job only when `PostsReports` is true, after `report.md` is written, with the PNG only when the screenshot was saved.
- [ ] `appsettings.discord.json` is loaded optionally, ignored by git, copied to bin, never published; the example file is tracked and never copied.
- [ ] The whole server suite passes apart from the known stackable-item failure.

**Verify:** `Chaos.Tests.exe --no-progress --treenode-filter "/*/Chaos.Tests.BugReports/*/*"` → 0 failed; then the full suite.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Add to `BugReportMarkdownTests.cs`:

```csharp
    [Test]
    public async Task AddHeaderFields_goes_before_the_closing_line()
    {
        var text = BugReportMarkdown.Render(BugReportSamples.Input());

        var updated = BugReportMarkdown.AddHeaderFields(text, [("discord_thread", "555"), ("discord_url", "https://discord.com/channels/777/555")]);

        updated.Should().Contain("status_note: \"\"\ndiscord_thread: \"555\"\ndiscord_url: \"https://discord.com/channels/777/555\"\n---\n\n## Report");
        updated.Replace("discord_thread: \"555\"\ndiscord_url: \"https://discord.com/channels/777/555\"\n", "").Should().Be(text);

        await Task.CompletedTask;
    }
```

Create `Tests/Chaos.Tests/BugReports/BugReportDiscordQueueTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Services.BugReports;
using Chaos.Services.Discord;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Moq;

namespace Chaos.Tests.BugReports;

public class BugReportDiscordQueueTests
{
    private sealed class FakeDiscord(params DiscordResult[] results) : IDiscordRestClient
    {
        private readonly Queue<DiscordResult> Results = new(results);
        public int Calls { get; private set; }

        public Task<DiscordResult> CreateForumThreadAsync(DiscordForumPost post, CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromResult(Results.Dequeue());
        }

        public Task<DiscordResult> SendMessageAsync(string channelId, string content, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static readonly DiscordResult Created = new(true, 201, "555", "777", null, null);

    private static (string Folder, Aisling Aisling) Report(bool connected = true)
    {
        var folder = Path.Combine(Path.GetTempPath(), "bugreport-discord-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "report.md"), BugReportMarkdown.Render(BugReportSamples.Input(title: "Stuck in the inn wall")));

        var aisling = MockAisling.Create(name: "Bob");
        Mock.Get(aisling.Client).SetupGet(c => c.Connected).Returns(connected);

        return (folder, aisling);
    }

    private static BugReportDiscordJob Job(string folder, Aisling aisling)
        => new(folder, aisling, "Stuck in the inn wall", new DiscordForumPost("999", "Bob: Stuck in the inn wall", "text", null, null));

    private static (BugReportDiscordQueue Queue, List<TimeSpan> Waits) NewQueue(IDiscordRestClient discord, int maxAttempts = 5)
    {
        var waits = new List<TimeSpan>();

        var queue = new BugReportDiscordQueue(
            discord,
            Microsoft.Extensions.Options.Options.Create(new BugReportDiscordOptions { BotToken = "t", ForumChannelId = "999", MaxAttempts = maxAttempts }),
            MockLogger.Create<BugReportDiscordQueue>().Object,
            (wait, _) =>
            {
                waits.Add(wait);

                return Task.CompletedTask;
            });

        return (queue, waits);
    }

    [Test]
    public async Task A_posted_report_records_the_thread_and_tells_the_player()
    {
        (var folder, var aisling) = Report();
        (var queue, _) = NewQueue(new FakeDiscord(Created));

        (await queue.PostAsync(Job(folder, aisling), CancellationToken.None)).Should().BeTrue();

        var text = await File.ReadAllTextAsync(Path.Combine(folder, "report.md"));
        text.Should().Contain("discord_thread: \"555\"\n").And.Contain("discord_url: \"https://discord.com/channels/777/555\"\n");

        var client = Mock.Get(aisling.Client);
        client.Verify(c => c.SendServerMessage(ServerMessageType.OrangeBar1, "Your report is on Discord in #bug-reports."), Times.Once);
        client.Verify(
            c => c.SendServerMessage(
                ServerMessageType.ActiveMessage,
                "Your bug report \"Stuck in the inn wall\" was posted on Discord in #bug-reports. Add more screenshots or details there."),
            Times.Once);
    }

    [Test]
    public async Task Rate_limits_and_server_errors_wait_then_retry()
    {
        (var folder, var aisling) = Report();
        var discord = new FakeDiscord(
            new DiscordResult(false, 429, null, null, TimeSpan.FromSeconds(1.5), "slow down"),
            new DiscordResult(false, 500, null, null, null, "boom"),
            new DiscordResult(false, 502, null, null, null, "boom"),
            Created);
        (var queue, var waits) = NewQueue(discord);

        (await queue.PostAsync(Job(folder, aisling), CancellationToken.None)).Should().BeTrue();

        discord.Calls.Should().Be(4);
        waits.Should().Equal(TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2));
    }

    [Test]
    public async Task A_forbidden_post_stops_at_once_and_records_the_error()
    {
        (var folder, var aisling) = Report();
        var discord = new FakeDiscord(new DiscordResult(false, 403, null, null, null, "HTTP 403: Missing Access"));
        (var queue, var waits) = NewQueue(discord);

        (await queue.PostAsync(Job(folder, aisling), CancellationToken.None)).Should().BeFalse();

        discord.Calls.Should().Be(1);
        waits.Should().BeEmpty();
        (await File.ReadAllTextAsync(Path.Combine(folder, "report.md"))).Should().Contain("discord_error: \"HTTP 403: Missing Access\"\n");
        Mock.Get(aisling.Client).Verify(c => c.SendServerMessage(ServerMessageType.OrangeBar1, "Report saved. Discord post failed."), Times.Once);
    }

    [Test]
    public async Task It_gives_up_after_the_last_attempt()
    {
        (var folder, var aisling) = Report();
        var fail = new DiscordResult(false, 503, null, null, null, "HTTP 503");
        var discord = new FakeDiscord(fail, fail);
        (var queue, var waits) = NewQueue(discord, maxAttempts: 2);

        (await queue.PostAsync(Job(folder, aisling), CancellationToken.None)).Should().BeFalse();

        discord.Calls.Should().Be(2);
        waits.Should().Equal(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task A_player_who_left_gets_no_message()
    {
        (var folder, var aisling) = Report(connected: false);
        (var queue, _) = NewQueue(new FakeDiscord(Created));

        await queue.PostAsync(Job(folder, aisling), CancellationToken.None);

        Mock.Get(aisling.Client).Verify(c => c.SendServerMessage(It.IsAny<ServerMessageType>(), It.IsAny<string>()), Times.Never);
    }
}
```

The `SendServerMessage` calls assume `Aisling.SendOrangeBarMessage` and `SendActiveMessage` go through `IChaosWorldClient.SendServerMessage(ServerMessageType, string)` with `OrangeBar1` and `ActiveMessage`. Check `Aisling.SendOrangeBarMessage`/`SendActiveMessage` first; if they call something else, verify that instead.

In `BugReportServiceTests.Setup`, the service gains two constructor arguments (Step 4). Pass a `Mock<IBugReportDiscordQueue>` and `Options.Create(new BugReportDiscordOptions())`, return the mock from `Setup`, and add:

```csharp
    [Test]
    public async Task A_saved_report_is_queued_for_discord_when_posting_is_on()
    {
        (var service, var aisling, var reportId, var root, var queue) = Setup(discord: new BugReportDiscordOptions { BotToken = "t", ForumChannelId = "999" });

        service.Open(aisling);
        service.Submit(aisling, Submit(reportId()));
        await WaitForReport(root, "map-warp");
        await WaitUntil(() => queue.Invocations.Count == 1);

        var job = (BugReportDiscordJob)queue.Invocations.Single().Arguments[0];
        job.Title.Should().Be("Stuck in the inn wall");
        job.Post.Name.Should().Be("Bob: Stuck in the inn wall");
        job.Post.ChannelId.Should().Be("999");
        job.Post.Png.Should().BeNull();
    }

    [Test]
    public async Task Nothing_is_queued_when_posting_is_off()
    {
        (var service, var aisling, var reportId, var root, var queue) = Setup();

        service.Open(aisling);
        service.Submit(aisling, Submit(reportId()));
        await WaitForReport(root, "map-warp");
        await Task.Delay(200);

        queue.Invocations.Should().BeEmpty();
    }
```

(`Setup` takes `BugReportDiscordOptions? discord = null` and returns the queue mock as a fifth tuple item; update existing callers' deconstructions with `_`.)

- [ ] **Step 2: Run to see them fail** — compile errors for the missing types and methods.

- [ ] **Step 3: Front matter fields**

`BugReportMarkdown.cs`:

```csharp
    /// <summary>Adds header fields just before the line that closes the header. Nothing else in the file changes.</summary>
    public static string AddHeaderFields(string markdown, IReadOnlyList<(string Key, string Value)> fields)
    {
        var close = markdown.IndexOf("\n---\n", 3, StringComparison.Ordinal);

        if (!markdown.StartsWith("---\n", StringComparison.Ordinal) || (close < 0))
            throw new FormatException("report.md has no header.");

        var lines = string.Concat(fields.Select(field => $"{field.Key}: {YamlQuote(field.Value)}\n"));

        return markdown.Insert(close + 1, lines);
    }
```

`BugReportStore.cs` (match the file's existing async style):

```csharp
    /// <summary>Adds header fields to a finished report's report.md.</summary>
    public static async Task AddReportFieldsAsync(string folder, IReadOnlyList<(string Key, string Value)> fields)
    {
        var path = Path.Combine(folder, "report.md");
        var markdown = await File.ReadAllTextAsync(path);

        await File.WriteAllTextAsync(path, BugReportMarkdown.AddHeaderFields(markdown, fields));
    }
```

(If `WriteReportAsync` writes through a temporary file or a specific encoding, do the same here.)

- [ ] **Step 4: The queue** — `Chaos/Services/BugReports/BugReportDiscordQueue.cs`:

```csharp
using System.Threading.Channels;
using Chaos.Models.World;
using Chaos.Services.Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chaos.Services.BugReports;

/// <summary>One saved report waiting to be posted.</summary>
public sealed record BugReportDiscordJob(string ReportFolder, Aisling Reporter, string Title, DiscordForumPost Post);

public interface IBugReportDiscordQueue
{
    void Enqueue(BugReportDiscordJob job);
}

/// <summary>
///     Posts saved reports to the Discord bug forum one at a time, off the game loop. Jobs still waiting at shutdown
///     are dropped; their report.md has no discord_thread line, so staff can find them.
/// </summary>
public sealed class BugReportDiscordQueue : BackgroundService, IBugReportDiscordQueue
{
    public const string POSTED_BAR = "Your report is on Discord in #bug-reports.";
    public const string FAILED_BAR = "Report saved. Discord post failed.";
    public const string FAILED_LINE = "Your bug report was saved and staff will see it, but it could not be posted on Discord right now.";

    private readonly Channel<BugReportDiscordJob> Jobs = Channel.CreateUnbounded<BugReportDiscordJob>();
    private readonly IDiscordRestClient Discord;
    private readonly ILogger<BugReportDiscordQueue> Logger;
    private readonly BugReportDiscordOptions Options;
    private readonly Func<TimeSpan, CancellationToken, Task> Delay;

    public BugReportDiscordQueue(
        IDiscordRestClient discord,
        IOptions<BugReportDiscordOptions> options,
        ILogger<BugReportDiscordQueue> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        Discord = discord;
        Options = options.Value;
        Logger = logger;
        Delay = delay ?? Task.Delay;
    }

    public void Enqueue(BugReportDiscordJob job) => Jobs.Writer.TryWrite(job);

    public static string PostedLine(string title) => $"Your bug report \"{title}\" was posted on Discord in #bug-reports. Add more screenshots or details there.";

    private static TimeSpan Backoff(int attempt)
        => attempt switch
        {
            1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromSeconds(30),
            _ => TimeSpan.FromMinutes(2)
        };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in Jobs.Reader.ReadAllAsync(stoppingToken))
                try
                {
                    await PostAsync(job, stoppingToken);
                } catch (Exception e) when (e is not OperationCanceledException)
                {
                    Logger.LogError(e, "Failed to post bug report {@ReportFolder} to Discord", job.ReportFolder);
                }
        } catch (OperationCanceledException) { }
    }

    public async Task<bool> PostAsync(BugReportDiscordJob job, CancellationToken cancellationToken)
    {
        DiscordResult result = null!;

        for (var attempt = 1; attempt <= Options.MaxAttempts; attempt++)
        {
            result = await Discord.CreateForumThreadAsync(job.Post, cancellationToken);

            if (result.Ok)
            {
                await BugReportStore.AddReportFieldsAsync(
                    job.ReportFolder,
                    [("discord_thread", result.Id!), ("discord_url", BugReportPostFormatter.PostUrl(result.GuildId!, result.Id!))]);

                Logger.LogInformation("Posted bug report {@ReportFolder} to Discord thread {@ThreadId}", job.ReportFolder, result.Id);
                Tell(job.Reporter, POSTED_BAR, PostedLine(job.Title));

                return true;
            }

            if (!result.ShouldRetry || (attempt == Options.MaxAttempts))
                break;

            var wait = result.Status == 429 ? result.RetryAfter ?? Backoff(attempt) : Backoff(attempt);
            await Delay(wait, cancellationToken);
        }

        Logger.LogWarning("Could not post bug report {@ReportFolder} to Discord: {@Error}", job.ReportFolder, result.Error);
        await BugReportStore.AddReportFieldsAsync(job.ReportFolder, [("discord_error", result.Error ?? $"HTTP {result.Status}")]);
        Tell(job.Reporter, FAILED_BAR, FAILED_LINE);

        return false;
    }

    private static void Tell(Aisling aisling, string bar, string line)
    {
        if (!aisling.Client.Connected)
            return;

        aisling.SendOrangeBarMessage(bar);
        aisling.SendActiveMessage(line);
    }
}
```

The wait after a failed attempt is picked by the attempt number, so 5xx failures from the first attempt wait 5 s, 30 s, then 2 min. With attempts 1 (429), 2 (500), 3 (502) the test expects 1.5 s, 30 s, 2 min; two 503s with `MaxAttempts` 2 give one 5 s wait.

- [ ] **Step 5: Hook it into `BugReportService`**

Constructor gains `IBugReportDiscordQueue discordQueue` and `IOptions<BugReportDiscordOptions> discordOptions` (store as `DiscordQueue` and `DiscordOptions = discordOptions.Value`). At the end of `FinishAsync`, after `Reply(aisling, "Thank you, your report was sent.");`:

```csharp
        if (DiscordOptions.PostsReports)
            DiscordQueue.Enqueue(
                new BugReportDiscordJob(
                    folder,
                    aisling,
                    BugReportMarkdown.OneLine(submit.Title),
                    new DiscordForumPost(
                        DiscordOptions.ForumChannelId,
                        BugReportPostFormatter.ThreadName(aisling.Name, submit.Title),
                        BugReportPostFormatter.Content(submit.Description, submit.Category, context.World, client.Build),
                        BugReportPostFormatter.TagId(submit.Category, DiscordOptions.TagIds),
                        screenshotSaved ? picture : null)));
```

(`client.Build` is the `ClientDetails` property holding the build string; use whatever `ClientDetails` calls it.)

- [ ] **Step 6: Registration and config files**

`ServiceCollectionExtensions.AddBugReports`, before `services.AddSingleton<BugReportService>();`:

```csharp
        //Discord settings come from appsettings.discord.json, which is not tracked; empty settings switch posting off
        services.AddOptionsFromConfig<BugReportDiscordOptions>(ConfigKeys.Options.Key);

        services.AddSingleton<IDiscordRestClient>(
            provider => new DiscordRestClient(
                new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                provider.GetRequiredService<IOptions<BugReportDiscordOptions>>().Value.BotToken));

        services.AddSingleton<BugReportDiscordQueue>();
        services.AddSingleton<IBugReportDiscordQueue>(provider => provider.GetRequiredService<BugReportDiscordQueue>());
        services.AddHostedService(provider => provider.GetRequiredService<BugReportDiscordQueue>());
```

(Add `using Chaos.Services.Discord;`.) `BugReportDiscordQueue`'s optional `delay` parameter must not confuse the container; if DI fails to construct it, register it with a factory: `new BugReportDiscordQueue(p.GetRequiredService<IDiscordRestClient>(), p.GetRequiredService<IOptions<BugReportDiscordOptions>>(), p.GetRequiredService<ILogger<BugReportDiscordQueue>>())`.

`Program.cs`, after the translation line:

```csharp
    //the UnoraReader bot's token and channel ids; optional and never tracked, so a server without it posts nothing
    builder.Configuration.AddJsonFile("appsettings.discord.json", true, false);
```

`Chaos.csproj`: next to the translation entries add

```xml
        <Content Update="appsettings.discord.json">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
            <CopyToPublishDirectory>Never</CopyToPublishDirectory>
        </Content>
```

and

```xml
        <!-- a template for appsettings.discord.json; never loaded, so never copied -->
        <Content Remove="appsettings.discord.example.json"/>
        <None Remove="appsettings.discord.example.json"/>
```

`.gitignore`: add `Chaos/appsettings.discord.json` under the translation line.

`Chaos/appsettings.discord.example.json`:

```json
{
  "Options": {
    "BugReportDiscordOptions": {
      "BotToken": "",
      "ForumChannelId": "",
      "AdminChannelId": "1089331247999885372",
      "TagIds": {
        "skill-spell": "",
        "item": "",
        "map-warp": "",
        "npc": "",
        "quest": "",
        "monster-combat": "",
        "client-ui": "",
        "other": ""
      },
      "MaxAttempts": 5
    }
  }
}
```

- [ ] **Step 7: Run the tests** — `Chaos.Tests.BugReports` filter → 0 failed; then the whole suite → only the known stackable-item failure. Also `dotnet build Chaos/Chaos.csproj -v q -nologo` → `Build succeeded.`

```json:metadata
{"files": ["Chaos/Services/BugReports/BugReportDiscordQueue.cs", "Chaos/Services/BugReports/BugReportMarkdown.cs", "Chaos/Services/BugReports/BugReportStore.cs", "Chaos/Services/BugReports/BugReportService.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Chaos/Program.cs", "Chaos/Chaos.csproj", ".gitignore", "Chaos/appsettings.discord.example.json", "Tests/Chaos.Tests/BugReports/BugReportDiscordQueueTests.cs"], "verifyCommand": "Tests/Chaos.Tests/bin/Debug/net10.0/Chaos.Tests.exe --no-progress --treenode-filter \"/*/Chaos.Tests.BugReports/*/*\"", "acceptanceCriteria": ["AddHeaderFields inserts before closing ---", "success writes discord_thread/discord_url and tells a connected player", "429/5xx retry with the set waits, 403 stops, give-up writes discord_error and failure messages", "service enqueues only when PostsReports, png only if screenshot saved", "appsettings.discord.json optional, gitignored, copied, not published; example tracked", "full suite passes except the known failure"], "modelTier": "standard"}
```

---

### Task 6: Admin activity messages through the REST client (server)

**Goal:** The admin trinket and arena admin messages post through `IDiscordAdminLog` instead of a `DiscordSocketClient` with an empty hardcoded token.

**Files:**
- Create: `Chaos/Services/Discord/DiscordAdminLog.cs`
- Modify: `Chaos/Scripting/DialogScripts/Temuair/Generic/AdminTrinketScript.cs`, `Chaos/Scripting/DialogScripts/Temuair/Arena/ArenaUndergroundScript.cs`
- Modify: `Chaos/Extensions/ServiceCollectionExtensions.cs`
- Test: `Tests/Chaos.Tests/Discord/DiscordAdminLogTests.cs`

**Acceptance Criteria:**
- [ ] Neither script has a `BOT_TOKEN` or `CHANNEL_ID` constant or a `DiscordSocketClient`.
- [ ] Both scripts call `AdminLog.Report($"```Admin {source.Name} has used the {command} command on Player {target.Name}```")` where they used to call the Discord client.
- [ ] `DiscordAdminLog.Report` sends to `AdminChannelId` when `PostsAdminActivity` is true and sends nothing otherwise.
- [ ] Server builds and the `Chaos.Tests.Discord` tests pass.

**Verify:** `Chaos.Tests.exe --no-progress --treenode-filter "/*/Chaos.Tests.Discord/*/*"` → 0 failed; `grep -n "BOT_TOKEN\|DiscordSocketClient" -r Chaos/Scripting` → no output.

**Steps:**

- [ ] **Step 1: Write the failing test** — `Tests/Chaos.Tests/Discord/DiscordAdminLogTests.cs`:

```csharp
using Chaos.Services.Discord;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Moq;

namespace Chaos.Tests.Discord;

public class DiscordAdminLogTests
{
    [Test]
    public async Task Reports_go_to_the_admin_channel_when_configured()
    {
        var discord = new Mock<IDiscordRestClient>();
        discord.Setup(d => d.SendMessageAsync("123", "hi", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new DiscordResult(true, 200, "1", null, null, null));

        var log = new DiscordAdminLog(
            discord.Object,
            Microsoft.Extensions.Options.Options.Create(new BugReportDiscordOptions { BotToken = "t", AdminChannelId = "123" }),
            MockLogger.Create<DiscordAdminLog>().Object);

        await log.ReportAsync("hi");

        discord.Verify(d => d.SendMessageAsync("123", "hi", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Nothing_is_sent_without_a_token_or_channel()
    {
        var discord = new Mock<IDiscordRestClient>();

        var log = new DiscordAdminLog(
            discord.Object,
            Microsoft.Extensions.Options.Options.Create(new BugReportDiscordOptions { AdminChannelId = "123" }),
            MockLogger.Create<DiscordAdminLog>().Object);

        await log.ReportAsync("hi");

        discord.VerifyNoOtherCalls();
    }
}
```

- [ ] **Step 2: Run to see it fail** — `DiscordAdminLog` does not exist.

- [ ] **Step 3: Implement** — `Chaos/Services/Discord/DiscordAdminLog.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chaos.Services.Discord;

public interface IDiscordAdminLog
{
    /// <summary>Posts to the admin channel in the background. Never throws and never blocks the caller.</summary>
    void Report(string message);
}

public sealed class DiscordAdminLog(IDiscordRestClient discord, IOptions<BugReportDiscordOptions> options, ILogger<DiscordAdminLog> logger)
    : IDiscordAdminLog
{
    public void Report(string message) => _ = ReportAsync(message);

    public async Task ReportAsync(string message)
    {
        var settings = options.Value;

        if (!settings.PostsAdminActivity)
            return;

        try
        {
            var result = await discord.SendMessageAsync(settings.AdminChannelId, message);

            if (!result.Ok)
                logger.LogWarning("Admin activity message was not posted to Discord: {@Error}", result.Error);
        } catch (Exception e)
        {
            logger.LogWarning(e, "Admin activity message was not posted to Discord");
        }
    }
}
```

Register in `AddBugReports` after the `IDiscordRestClient` line: `services.AddSingleton<IDiscordAdminLog, DiscordAdminLog>();`

- [ ] **Step 4: The scripts** — in each script:
  1. Delete `BOT_TOKEN` and `CHANNEL_ID`.
  2. Add a constructor parameter `IDiscordAdminLog adminLog` stored as `private readonly IDiscordAdminLog AdminLog;` (follow how the script's constructor already takes its other services).
  3. Replace the body of the method that logs in with `DiscordSocketClient` (`ReportActivity` in `AdminTrinketScript.cs:615`; the matching method around `ArenaUndergroundScript.cs:843`) with `AdminLog.Report($"```Admin {source.Name} has used the {command} command on Player {target.Name}```");`, keeping the arena script's message text exactly as it is now.
  4. Remove `using Discord;` / `using Discord.WebSocket;` if nothing else in the file uses them.

- [ ] **Step 5: Run** — build, `Chaos.Tests.Discord` filter → 0 failed, and the grep in **Verify** prints nothing.

```json:metadata
{"files": ["Chaos/Services/Discord/DiscordAdminLog.cs", "Chaos/Scripting/DialogScripts/Temuair/Generic/AdminTrinketScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Arena/ArenaUndergroundScript.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Tests/Chaos.Tests/Discord/DiscordAdminLogTests.cs"], "verifyCommand": "Tests/Chaos.Tests/bin/Debug/net10.0/Chaos.Tests.exe --no-progress --treenode-filter \"/*/Chaos.Tests.Discord/*/*\"", "acceptanceCriteria": ["no BOT_TOKEN, CHANNEL_ID or DiscordSocketClient in either script", "scripts call AdminLog.Report with the same message", "DiscordAdminLog sends only when PostsAdminActivity", "server builds and Discord tests pass"], "modelTier": "mechanical"}
```

---

### Task 7: Forum tag helper (Unora FeedbackSync)

**Goal:** A script that prints the bug forum's channel id and tag ids in the shape `appsettings.discord.json` needs.

**Files:**
- Create: `Unora/Tools/FeedbackSync/forum_tags.py`
- Modify: `Unora/Tools/FeedbackSync/README.md` (one short section)

**Acceptance Criteria:**
- [ ] `python Tools/FeedbackSync/forum_tags.py` reads the token and `bugs_channel_id` the same way `sync.py` does, calls `GET /channels/{bugs_channel_id}`, and prints a JSON block with `ForumChannelId` and `TagIds` keyed by the in-game folder names, mapping Spell→`skill-spell`, Item→`item`, Map→`map-warp`, NPC→`npc`, Quest→`quest`, Monster→`monster-combat`, Client→`client-ui`.
- [ ] It never prints the token.
- [ ] The README says to paste the output into `Chaos/appsettings.discord.json` next to `BotToken`.

**Verify:** `python -c "import ast,sys; ast.parse(open('Tools/FeedbackSync/forum_tags.py').read())"` → no error. The live run happens in Task 10.

**Steps:**

- [ ] **Step 1: Read `sync.py`** for its config loading, `discord_request` helper and `API_BASE`, and import them rather than copying.

- [ ] **Step 2: Write `forum_tags.py`**

```python
"""Prints the bug forum's channel id and tag ids for Chaos/appsettings.discord.json."""
import json

import sync

TAG_FOLDERS = {
    "Spell": "skill-spell",
    "Item": "item",
    "Map": "map-warp",
    "NPC": "npc",
    "Quest": "quest",
    "Monster": "monster-combat",
    "Client": "client-ui",
}


def main():
    config = sync.load_config()
    channel_id = config["bugs_channel_id"]
    forum = sync.discord_request(config["token"], f"/channels/{channel_id}")
    tags = {tag["name"]: tag["id"] for tag in forum.get("available_tags", [])}
    tag_ids = {folder: tags.get(name, "") for name, folder in TAG_FOLDERS.items()}
    tag_ids["other"] = ""
    print(json.dumps({"ForumChannelId": channel_id, "TagIds": tag_ids}, indent=2))


if __name__ == "__main__":
    main()
```

Adjust `sync.load_config` / `sync.discord_request` / the token key to the real names in `sync.py` (it may read `DISCORD_TOKEN` as a fallback; keep that behaviour by calling the same helper).

- [ ] **Step 3: README** — add a "Game server posting" section: run `python Tools/FeedbackSync/forum_tags.py` from the Unora root, then paste `ForumChannelId` and `TagIds` into `Chaos/appsettings.discord.json` (see `appsettings.discord.example.json`) along with the bot token. The bot needs Send Messages, Create Posts and Attach Files on the bug forum.

```json:metadata
{"files": ["Unora/Tools/FeedbackSync/forum_tags.py", "Unora/Tools/FeedbackSync/README.md"], "verifyCommand": "python -c \"import ast; ast.parse(open('Tools/FeedbackSync/forum_tags.py').read())\"", "acceptanceCriteria": ["prints ForumChannelId and TagIds keyed by folder names", "never prints the token", "README explains where to paste it"], "modelTier": "mechanical"}
```

---

### Task 8: Discord link on the admin Bug reports page (file server)

**Goal:** The file server reads `discord_url` from `report.md` and shows a "Discord post" link on the report.

**Files:**
- Modify: `src/Chaos.FileServer/BugReports/BugReportModels.cs` (`ParsedReport`, `BugReportSummary`)
- Modify: `src/Chaos.FileServer/BugReports/BugReportMarkdownReader.cs:40`
- Modify: `src/Chaos.FileServer/Admin/Pages/bugreports.js`
- Test: `tests/Chaos.FileServer.Tests/BugReports/` (the existing markdown reader tests, or a new `BugReportMarkdownReaderDiscordTests.cs`)

**Acceptance Criteria:**
- [ ] `ParsedReport` and `BugReportSummary` have `string DiscordUrl` (empty when absent), filled from the `discord_url` header field.
- [ ] The detail view shows `<a href="..." target="_blank" rel="noopener">Discord post</a>` only when the URL starts with `https://discord.com/`.
- [ ] File server tests pass.

**Verify:** file server test command from "How to run tests" → 0 failed.

**Steps:**

- [ ] **Step 1: Failing test** — parse a report.md string whose header has `discord_url: "https://discord.com/channels/777/555"` (copy an existing reader test's sample and add the line), and assert `parsed!.DiscordUrl == "https://discord.com/channels/777/555"`; a sample without it gives `""`.

- [ ] **Step 2: Run to see it fail** — `DiscordUrl` does not exist.

- [ ] **Step 3: Models and reader** — append `string DiscordUrl` as the last positional parameter of `ParsedReport` and of `BugReportSummary`. In `BugReportSummary.From`, pass `p.DiscordUrl` last. In `BugReportMarkdownReader.Parse`, pass `Get("discord_url")` as the last argument of `new ParsedReport(...)`. Fix any other constructor calls the compiler reports.

- [ ] **Step 4: Page** — in `bugreports.js`, where the detail view builds its header for `s` (near line 216, where `s.hasScreenshot` is used), add:

```js
          if (s.discordUrl && s.discordUrl.indexOf('https://discord.com/') === 0)
            h += '<p><a href="' + esc(s.discordUrl) + '" target="_blank" rel="noopener">Discord post</a></p>';
```

(Confirm the JSON property name is `discordUrl` — camelCase like `hasScreenshot`.)

- [ ] **Step 5: Run the file server tests** → 0 failed.

```json:metadata
{"files": ["src/Chaos.FileServer/BugReports/BugReportModels.cs", "src/Chaos.FileServer/BugReports/BugReportMarkdownReader.cs", "src/Chaos.FileServer/Admin/Pages/bugreports.js"], "verifyCommand": "dotnet run --project tests/Chaos.FileServer.Tests/Chaos.FileServer.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["DiscordUrl parsed from discord_url, empty when absent", "detail view links only https://discord.com/ urls", "file server tests pass"], "modelTier": "mechanical"}
```

---

### Task 9: Commit the full implementation

**Goal:** One commit per repo on `feat/bug-reports-discord`, with the client pointing at the server commit.

**Files:**
- All files changed in Tasks 1–8, in their worktrees, plus Unora's two FeedbackSync files.

**Acceptance Criteria:**
- [ ] Server worktree: one commit with every server file from Tasks 1, 3, 4, 5, 6; `git status --short` clean apart from build outputs.
- [ ] Client worktree: one commit with Task 2's files and the `Chaos-Server` submodule pointer set to the server commit.
- [ ] File server worktree: one commit with Task 8's files.
- [ ] Unora: one commit with `forum_tags.py` and the README, staged by explicit path on `main`.
- [ ] No `appsettings.discord.json` or token in any commit.

**Verify:** `git -C <each repo> log --oneline -1` and `git -C <each repo> show --stat HEAD`.

**Steps:**

- [ ] **Step 1: Server** — `git add` each changed and new path explicitly, then commit:

```bash
git commit -F - <<'EOF'
Post in-game bug reports to the Discord bug forum

- Bug report submit carries a 5-80 character title (CLIENT_VERSION +1)
- BugReportDiscordQueue posts saved reports through Discord's REST API with retries, records the thread in report.md and tells the player
- Admin activity messages use the same REST client; the empty hardcoded token is gone
- Settings live in the untracked appsettings.discord.json

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 2: Client** — `git update-index --cacheinfo 160000,<server sha>,Chaos-Server`, `git add` Task 2's files, commit "Bug report window: title box and public posting note" with the same trailer.
- [ ] **Step 3: File server** — `git add` Task 8's files, commit "Bug reports page: link to the Discord post" with the trailer.
- [ ] **Step 4: Unora** — `git commit -F msg -- Tools/FeedbackSync/forum_tags.py Tools/FeedbackSync/README.md` ("FeedbackSync: forum_tags.py prints the bug forum's tag ids").
- [ ] **Step 5: Check** — `git grep -n "BotToken\": \"[^\"]" ` in each repo prints nothing.

```json:metadata
{"files": [], "verifyCommand": "git -C worktrees/discord-reports-server show --stat HEAD", "acceptanceCriteria": ["one server commit", "one client commit with submodule pointer", "one file server commit", "one Unora commit", "no token committed"], "modelTier": "mechanical"}
```

---

### Task 10: Live check with the real bot

**Goal:** One real report from a local client reaches the Discord bug forum with its tag, screenshot, and player messages.

**Files:**
- Create (untracked, user-provided): `worktrees/discord-reports-server/Chaos/appsettings.discord.json`

**Acceptance Criteria:**
- [ ] `forum_tags.py` output plus the bot token are in `appsettings.discord.json` (the user supplies the token; never echo it).
- [ ] A report sent from a local client built against the server worktree shows up in the bug forum as `<Character>: <title>` with the right tag and the screenshot.
- [ ] The client shows `Your report is on Discord in #bug-reports.` and the chat line with the title.
- [ ] The report's `report.md` has `discord_thread` and `discord_url`, and the admin page (file server worktree build) shows the link.
- [ ] The test post is deleted from the forum afterwards, or the user chooses to keep it.

**Verify:** a screenshot of the client message and the Discord thread URL from `report.md`.

**Steps:**

- [ ] **Step 1:** Ask the user to stop any running Chaos.exe / client that locks the build, and to create `appsettings.discord.json` with the token (or approve writing it from `forum_tags.py` output plus a token they paste).
- [ ] **Step 2:** Run `python Tools/FeedbackSync/forum_tags.py` in Unora and merge its output into the file.
- [ ] **Step 3:** Start the server worktree (`dotnet run --project Chaos/Chaos.csproj`) and a client from the client worktree (see memory `chaos-client-worktree-version` for local client launch), log in, F1 → Terminus → Report a bug, pick a category, type a title and description, send.
- [ ] **Step 4:** Check the forum, the client messages, `report.md` and the admin page. Capture a client screenshot (memory `chaos-client-window-automation`).
- [ ] **Step 5:** Ask the user whether to delete the test post.

```json:metadata
{"files": ["Chaos/appsettings.discord.json"], "verifyCommand": "grep -n discord_url <report folder>/report.md", "acceptanceCriteria": ["config filled without echoing the token", "real post with title, tag and screenshot", "client shows success orange bar and chat line", "report.md has discord_thread/discord_url and admin page links it", "test post cleaned up or kept by user choice"], "modelTier": "standard"}
```
