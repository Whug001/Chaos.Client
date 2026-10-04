# In-game bug reports posted to Discord

Date: 2026-10-04. Builds on `2026-09-23-in-game-bug-reports-design.md`.

## Goal

When a player sends a report from F1 → Terminus → Report a bug, the UnoraReader bot posts it in the Discord bug forum straight away. The player and other players can then add screenshots and comments in the thread.

## Decisions

- Post immediately, with no staff review first.
- The screenshot is posted. The report window warns that it is public.
- The post title names the character: `<Character>: <title>`.
- The game tells the player where the post is with an orange-bar message only. There is no link button.
- The player writes a title in the report window.
- The game server does the posting. The file server and the FeedbackSync scripts are not involved.

## Client

### Report window (`BugReportControl.cs`)

- Add a one-line Title box above the description box.
- The title must be 5 to 80 characters after trimming.
- Send stays disabled until the category, title and description are all valid. `BugReportUpload.CanSend` takes the title too.
- The note under the picture adds: "The title, description and picture are posted publicly in the Unora Discord."
- `BugReportSubmission` gains `Title`.

### Packet

- `BugReportInteractionArgs` Submit gains `Title`, written right after the category, in both the client and the server converters.
- `BugReportProtocol` gains `MIN_TITLE_CHARS = 5` and `MAX_TITLE_CHARS = 80`.
- `CLIENT_VERSION` goes up by one. Older clients get the existing "client is out of date" refusal, so no fallback is needed.

## Server

### Validation and storage

- `BugReportService` rejects a submit whose trimmed title is outside 5 to 80 characters, the same way it rejects a bad description.
- The title is stored in `report.md` front matter as `title:`. It is also the report's first heading.
- The folder name and the existing files are unchanged.

### Posting (`Chaos/Services/BugReports/Discord/`)

- **`BugReportDiscordOptions`** is bound from the `BugReportDiscord` config section:
  - `BotToken`
  - `ForumChannelId`
  - `TagIds`: a map from in-game category to forum tag id
  - `MaxAttempts` (default 5)

  Posting is off when `BotToken` or `ForumChannelId` is empty. That is the default, so local and test servers never post.
- **`DiscordForumPoster`** calls Discord's REST API with `HttpClient`. It does not log in a gateway client.
  - It sends `POST https://discord.com/api/v10/channels/{ForumChannelId}/threads` as `multipart/form-data`.
  - The `payload_json` part has `name`, `applied_tags`, and `message: { content, attachments: [{ id: 0, filename: "screenshot.png" }] }`. The `files[0]` part is the PNG.
  - On success it returns the thread id.
  - Headers are `Authorization: Bot <token>` and a `DiscordBot (...)` User-Agent.
- **`BugReportPostQueue`** is a hosted background service fed by a channel. A finished report is queued once `MovePendingToCategory` succeeds. The submit itself never waits on Discord.
  - A 429 waits for `retry_after`. A 5xx or network error backs off 5 s, then 30 s, then 2 min, up to `MaxAttempts`.
  - A 4xx other than 429 is logged and not retried.
- **On success:** `discord_thread:` and `discord_url:` are added to `report.md` front matter. The player gets the messages below if they are still online.
- **On final failure:** the error is logged, and `discord_error:` is written to front matter. The player gets the failure messages below. Reports that are queued but not yet posted at shutdown are not resumed. Their front matter has no `discord_thread:`, so staff can find them.

### Post content (`BugReportPostFormatter`, pure functions)

- **Name:** `<Character>: <title>`. Trimmed to Discord's 100-character limit, though a 12-character name plus an 80-character title never reaches it.
- **Content:** under Discord's 2,000-character limit. It contains:
  - the description
  - a blank line
  - `Category: <category> · Map: <map name> (<x>, <y>) · Client <build>`
  - `Reported in game by <Character>. Add more screenshots or details below.`

  The description is at most 1,000 characters, so the content always fits.
- **Mentions:** the content is sent with `allowed_mentions: { parse: [] }`, so `@everyone` or a role name typed into a report pings nobody.
- **Tag:** looked up from `TagIds`. A missing entry (for example Other) posts with no tag.

  | In-game category | Forum tag |
  | --- | --- |
  | Skill/Spell | Spell |
  | Item | Item |
  | Map/Warp | Map |
  | NPC | NPC |
  | Quest | Quest |
  | Monster/Combat | Monster |
  | Client/UI | Client |
  | Other | none |

- **Never posted:** the server log, world snapshot, character copy and client details file stay on the server.

### Player message

Orange-bar text is limited to 45 characters, so each outcome has a short orange-bar line and a full line in the chat panel:

| Outcome | Orange bar | Chat panel |
| --- | --- | --- |
| Posted | `Your report is on Discord in #bug-reports.` | `Your bug report "<title>" was posted on Discord in #bug-reports. Add more screenshots or details there.` |
| Failed | `Report saved. Discord post failed.` | `Your bug report was saved and staff will see it, but it could not be posted on Discord right now.` |

### Existing hardcoded token

- `AdminTrinketScript.cs` and `ArenaUndergroundScript.cs` hardcode a bot token and a channel id.
- Both read the token from `BugReportDiscordOptions.BotToken` instead, and the channel id moves into config next to it as `AdminChannelId`.
- With no token configured, those two messages are skipped.
- After this ships, the user rotates the token in the Discord developer portal and puts the new one in the live server's config. The old token stays in git history, so rotating it is required.

## Admin page and FeedbackSync

- The file server's Bug reports page shows the title. When `discord_url` is present, it shows the link as "Discord post".
- `import_reports.py` copies the new front-matter fields unchanged.
- The next `sync.py` run pulls the posted thread into `docs/player-feedback/bugs/` like any other post. Nothing de-duplicates the two copies; the in-game copy keeps the private files and the forum copy keeps the thread.

## Abuse

- The existing per-character limits apply: 2 minutes between reports and 10 seconds between opens.
- Staff delete unwanted posts in Discord.
- No word filter is added in this version.

## Testing

### Server (`Tests/Chaos.Tests`)

- **Formatter:** the name format, the category-to-tag lookup with a missing tag, the content layout, and `allowed_mentions` being empty.
- **Validation:** a title under 5 or over 80 characters after trimming is rejected; a valid one lands in `report.md`.
- **Poster:** a fake `HttpMessageHandler` checks:
  - the multipart shape (`payload_json` plus `files[0]`, and the attachment id)
  - that a 429 is retried after `retry_after`
  - that a 5xx is retried and then gives up
  - that a 403 is not retried
- **Queue:** with an empty token nothing is sent. On success the front matter gains `discord_thread` and `discord_url`.

### Client (`Tests/Chaos.Client.Tests`)

- `CanSend` covers the title rules.
- The submit packet round-trips with the title.

### Live check before release

- Send one real report from a local client against a server pointed at the bug forum, or at a private test forum.
- Check the post, its tag, the screenshot and the orange-bar message.

## Out of scope

- Linking a character to a Discord account, or tagging the player.
- A clickable "open post" button in the game.
- Staff review before posting.
- Posting suggestions or arena reports from the game.
