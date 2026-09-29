# Korean–English chat translation — design

Date: 2026-09-29. Brainstormed with the user the same night. The user approved approach 1 and asked for it to be
built overnight, fast, with all three free providers chained.

## What it does

A player picks **Chat translation: Off / English / Korean** in the F4 Chat section. Another player's chat line in
the other language reaches them in two steps:

1. Everyone gets the original line at once, exactly as today.
2. About 0.2–1 s later the server sends a follow-up packet. The client replaces that line **in place**, in the chat
   log and in the speaker's bubble. The chat log line starts with a tag naming the original language. An English
   reader sees `[Kor]` and a Korean reader sees `[영어]`. Hovering the chat log line shows the original.

The speaker always sees what they typed. A reader set to Off sees the original. Scripts (merchants, monsters,
verbal triggers) always get the typed text. If translation fails, is late, or is refused, the reader keeps the
original line. Nothing else changes.

## Decisions (from the brainstorm)

| Topic | Decision |
|---|---|
| Channels | say, shout, whisper, group, guild, `!global`, `!trade`. Not chants, NPC or monster lines, orange-bar system messages, boards or mail. |
| Delivery | Show the original, then swap it (chat log + bubble). The late translation replaces a bubble only while that bubble still shows the same line. |
| Whispers | The reader decides; the sender is not asked. Whisper translations are never written to disk. |
| Length | A translated line may be up to **double** the normal limit: 134 characters for say/shout, 180 for whisper/group/guild/channels (prefix included). |
| Tag | Added by the client at the start of the chat log line only (after a leading `{=x` color code if present). Not in bubbles. `[Kor]` for English readers, `[영어]` for Korean readers. |
| Game names | Map, item, spell, skill, monster, merchant and online player names stay in English. The list is built from game data. A small hand-made JSON maps Korean spellings (`밀레스`) to the English name. |
| Per-player limit | 5,000 characters per speaker per UTC day, counting only characters sent to a provider. Cache and shortcut hits are free. One orange-bar notice to the speaker when they hit it. |
| Cost per line | At most one provider request per line, shared by every reader. No request when no reader wants it. Identical in-flight requests are merged. |
| Providers | Azure Translator F0 (2,000,000/month) → DeepL API Free (500,000/month) → Google Cloud Translation v2 (500,000/month). Skip a provider at 90% of its limit, and at once on a quota error. |
| Cache | In-memory dictionary. Non-whisper results are also appended to a `.jsonl` file and reloaded at startup. |
| Placement | Everything runs inside the game server. No separate program. |

## Server

### Line numbers on chat packets

Every player chat line gets a **line id**: a server-wide `uint` from `Interlocked.Increment`, never 0. It is only
assigned while translation is enabled, so with the feature off every packet stays byte-identical to today.

- `DisplayPublicMessageArgs` and `ServerMessageArgs` get `uint LineId` (0 = none).
- Wire layout, after the existing fields: when `LineId != 0` the tag section is always written (count may be 0),
  then the `uint32` line id. When `LineId == 0` the layout is unchanged (tags only when present). Readers take tags
  if bytes remain, then the line id if bytes remain.

### New packet: ChatTranslation (server opcode 140)

`ChatTranslationArgs { uint LineId; ChatLanguage SourceLanguage; string Text; IReadOnlyList<ChatTag> Tags }`

- `Text` is the translated **body only**, with no name, channel prefix or color code. The client keeps the prefix
  of the line it already has (`ChatDisplayBuilder.SplitPrefix`), so the server never re-formats a line.
- `Tags` are chat-filter tags for the translated body from `IChatFilterService.Detect(translatedBody)`. They are
  body-relative, like today's tags.
- Layout: `uint32` line id, `byte` source language, `String16` text, tag section (always written).
- `CLIENT_VERSION` 760 → 761.

### Player option

- `ChatLanguage : byte { Off = 0, English = 1, Korean = 2 }` in `Chaos.DarkAges.Definitions`.
- `UserOption.ChatLanguage = 31`. It is set through the existing OptionToggle `Set` action with a byte value,
  the same way `ChatFilterMode` works. The server echoes `UserOptions` after the change.
- `UserOptions.ChatLanguage` is saved as a string in `UserOptionsSchema` (`"Off"` default, so legacy saves load
  as Off). Unknown strings load as Off.
- `UserOptionsArgs.ChatLanguage` (byte). Converter trailer: when any trailer value is non-default, write
  chat-filter mode and configured flag first, then the language byte only when it is non-zero. Read it if bytes
  remain. An old reader never sees a language byte where it expects the filter mode.
- When a player sets it to English or Korean, the server sends one orange-bar line: "Chat lines you read in the
  other language are sent to a translation service." When the feature is disabled on the server, it sends
  "Chat translation is not available right now." instead. The choice is still saved.

### Where lines are captured

The rule everywhere: **decide the readers while holding the map lock, send the original, run scripts, then hand the
job to the translation service.** The service does its HTTP work on the thread pool and sends the follow-up packet
without taking any map lock. Sending a packet already works without the reader's map lock: whispers and channels do
it today.

A reader is an `Aisling` that received the original, is not the speaker, and has
`Options.ChatLanguage == target language of the line`.

- **Say / shout:** `Creature.ShowPublicMessage` builds the reader list inside its existing recipient loop, but only
  when `this is Aisling` and the type is Normal or Shout. Chants are never translated. The `OnPublicMessage` script
  calls keep getting the typed text.
- **Whisper:** in `WorldServer.OnWhisper`, the target is the only possible reader. The sender's echo
  (`[Name]> …`) is never translated.
- **Group, guild, `!global`, `!trade` and custom channels:** `ChannelService.SendMessage` gains a `uint lineId`
  parameter, passes it to the channel's send action, and **returns the subscribers it delivered to**. The send
  action delegate gains the line id. The four registrations (default channels, group, guild, custom channels)
  forward it to `SendServerMessage` / `SendDisplayPublicMessage`. `Group.SendMessage` and `Guild.SendMessage`
  return the recipient list. `WorldServer.OnWhisper`'s channel branch builds readers from it. Orange-bar copies of
  channel lines carry no line id.

### ChatTranslationService

One singleton, `IChatTranslationService`, in `Chaos/Services/ChatTranslation/`. The public surface:

```csharp
bool IsEnabled { get; }
uint NextLineId();                      // 0 when disabled
ChatLanguage? DetectLanguage(string body);
void Submit(ChatTranslationJob job);    // fire-and-forget; never throws; never blocks
```

`ChatTranslationJob(uint LineId, string SpeakerName, string Body, ChatLanguage Source, ChatLanguage Target,
IReadOnlyList<Aisling> Readers, int MaxBodyLength, bool Persist)`. `Persist` is false for whispers.

Callers first check `IsEnabled`, then `DetectLanguage(body)`. When the result is null, or no reader wants the
target language, they skip `Submit` entirely.

**Language detection** (pure, unit-tested):

- One or more Hangul syllables (U+AC00–U+D7A3) → Korean.
- Otherwise, two or more ASCII letters → English.
- Otherwise (numbers, punctuation, jamo-only lines like `ㅋㅋㅋ`, emoticons) → null. The shortcut table still
  gets a chance first (below).

**Pipeline for one job** (runs on the thread pool):

1. **Shortcut table.** The normalized body is looked up in a fixed per-direction table (`ㅋㅋ`/`ㅋㅋㅋ`→`lol`,
   `ㅎㅇ`→`hi`, `ㄱㄱ`→`let's go`, `ㅇㅋ`→`ok`, `ㄳ`/`ㄱㅅ`→`thanks`, `ㅠㅠ`→`:(`, and `lol`, `ty`, `thx`,
   `gg`, `brb`, `np`, `afk`, `omw` for English→Korean). A hit costs nothing. The table lives in the tracked config
   file and can be extended.
2. **Cache.** The key is the direction plus the normalized body: trimmed, runs of spaces collapsed, and lowercased
   when English. A hit costs nothing.
3. **In-flight merge.** If the same key is already being translated, the job awaits that task.
4. **Per-player limit.** If the speaker has used 5,000 provider characters today (UTC), stop. The first refusal of
   the day sends the speaker one orange-bar line: "You've reached today's translation limit. Your lines are shown
   untranslated until tomorrow."
5. **Protect names.** Split the body into segments: plain text and protected names. Protected names are matched
   case-insensitively as whole words, longest match first, from the game-name list plus the names of online
   players. Korean spellings from the hand-made map become their English name, marked protected.
6. **Provider chain.** Try each usable provider in order with a per-attempt timeout (default 2,000 ms) and an
   overall budget (default 4,000 ms). Each provider renders the protected segments in its own markup and strips it
   from the response (see below). The first success wins.
7. **Check the result.** If the provider detected a source language other than the expected one (for example a
   Spanish line sent as English), the result is dropped. That drop is cached in memory only, so the same line is not
   paid for again.
8. **Clean the text.** HTML-decode, strip leftover tags, turn curly quotes and dashes into ASCII, and drop any
   character code page 949 cannot encode. Cut the body to `MaxBodyLength`, at a word boundary when one exists in
   the last 12 characters.
9. **Store.** Save to the memory cache. When `Persist` is true, also append to the cache file.
10. **Deliver.** Run the chat filter on the translated body, then send `ChatTranslation` to each reader whose client
    is still connected.

`MaxBodyLength` = double the line limit minus the original prefix length, where the prefix length is the sent line
length minus the typed body length. Double the limit is 134 for public lines and 180 for the rest.

A failure anywhere logs one warning and ends the job. Readers keep the original. Exceptions never escape `Submit`.

**Charging.** The characters sent to the provider are added to the speaker's daily total and to the provider's
monthly total. The provider total uses Azure's `X-metered-usage` header and DeepL's `billed_characters` when
present, and the sent length otherwise.

### Providers

`IChatTranslationProvider { string Name; Task<ProviderResult> TranslateAsync(ProtectedText text, ChatLanguage
target, CancellationToken ct); }`. `ProviderResult` is Success(text, detectedLanguage, billedChars), QuotaExhausted,
or TransientFailure. All three use one shared `HttpClient` (pooled connections, kept alive).

| Provider | Request | Protected markup | Quota error |
|---|---|---|---|
| Azure | `POST https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&to={ko\|en}&textType=html`, headers `Ocp-Apim-Subscription-Key` and `Ocp-Apim-Subscription-Region` (when configured), body `[{"Text":…}]`. No `from`, so it auto-detects. | `<span class="notranslate">Mileth</span>` | 403 → exhausted until the monthly reset. 429 → transient. |
| DeepL | `POST https://api-free.deepl.com/v2/translate`, header `Authorization: DeepL-Auth-Key {key}`, JSON `{"text":[…],"target_lang":"KO"\|"EN-US","tag_handling":"xml","ignore_tags":["x"],"show_billed_characters":true}` | `<x>Mileth</x>` | 456 → exhausted until the monthly reset. 429 → transient. |
| Google | `POST https://translation.googleapis.com/language/translate/v2?key={key}`, JSON `{"q":…,"target":"ko"\|"en","format":"html"}` | `<span translate="no">Mileth</span>` | 403, or 429 whose body mentions a daily limit → exhausted until the next UTC day. Other 429s → transient. |

The plain-text parts are XML-escaped before markup is added. The response is decoded after the markup is removed.

A provider is **usable** when it has a key, is not marked exhausted for its current period, and its local count is
under `SwitchAtPercent` (90) of its monthly limit. Each provider has a `ResetDayOfMonth` (default 1). DeepL's local
count is re-synced from `GET /v2/usage` at startup and then hourly.

### Storage

Files live under the staging directory, next to `ChatFilterEdits.json`:

- `Data/LocalStorage/ChatTranslation/cache.jsonl`: one line per saved translation,
  `{"d":"ko-en","k":"<normalized body>","v":"<translation>","t":"2026-09-29"}`. Appended per new entry. At startup
  it is read line by line, entries older than 90 days are dropped, and the file is rewritten once if anything was
  dropped. A bad line is skipped.
- `Data/LocalStorage/ChatTranslation/usage.json`: per provider `{periodStart, characters, exhaustedUntil}`, and per
  player `{day, characters}`. It is written at most every 30 seconds when changed, and on shutdown.
- `Data/Configuration/ChatTranslation.json` (tracked in Unora): the shortcut tables and the Korean-name map.

### Configuration

The `ChatTranslationOptions` section in `appsettings.json` holds the defaults with **empty keys**:

```json
"ChatTranslationOptions": {
  "Directory": "Data\\LocalStorage\\ChatTranslation",
  "ConfigFile": "Data\\Configuration\\ChatTranslation.json",
  "AttemptTimeoutMs": 2000,
  "TotalTimeoutMs": 4000,
  "SwitchAtPercent": 90,
  "PerPlayerDailyCharacters": 5000,
  "CacheDays": 90,
  "Providers": [
    { "Name": "Azure",  "Key": "", "Region": "", "MonthlyCharacters": 2000000, "ResetDayOfMonth": 1 },
    { "Name": "DeepL",  "Key": "", "MonthlyCharacters": 500000,  "ResetDayOfMonth": 1 },
    { "Name": "Google", "Key": "", "MonthlyCharacters": 500000,  "ResetDayOfMonth": 1 }
  ]
}
```

The keys go in a new optional file, **`appsettings.translation.json`**, loaded after the others and gitignored.
The feature is enabled when at least one provider has a key. A deploy must never overwrite that file.

### Admin command

`/translation` (admin only) prints to the orange bar: whether translation is enabled, each provider's
characters used, its limit and state (usable / over 90% / exhausted until …), cache entry count, the cache hit
rate since startup, and the jobs in flight.

### Logging

Each delivered translation is logged at Information with the speaker, direction, provider (or `cache` /
`shortcut`), original and translation. Chat is already logged to Seq today, so this adds no new exposure.
Provider failures are logged at Warning.

## Client

- **Packets:** read `LineId` from `DisplayPublicMessage` and `ServerMessage`. Handle `ChatTranslation` (opcode 140)
  with the usual `ConnectionManager` event plus a `WorldScreen` handler.
- **Chat log model:** `Chat.ChatMessage` gains `LineId` (0 = none). `Chat.AddMessage` takes it.
  `Chat.TryReplaceMessage(lineId, text, originalText, tags)` replaces the stored message and raises
  `MessageReplaced(lineId, message)`. A line that already scrolled out of the 1,000-message buffer is ignored.
- **Chat panel:** each wrapped `ChatLine` row carries its `LineId` and the original text. On `MessageReplaced`, the
  rows of that line are removed and the re-wrapped rows are inserted at the same index. A reader who scrolled up
  keeps their place. Hovering a translated row shows the original line in a tooltip, reusing the existing tooltip
  look (`KeywordTooltipControl` / `TooltipPanelBase`).
- **Building the translated line:** take the stored original, split its prefix with the same rule as
  `ChatDisplayBuilder`, run `ChatDisplayBuilder.Build(prefix, translatedBody, tags, filterMode, fantasy)`, then
  insert `[Kor] ` or `[영어] ` at the start, after a leading `{=x` color code when there is one. The reader's own
  chat filter mode applies to the translated body.
- **Bubbles:** `ChatBubble` stores its `LineId`. `EntityOverlayManager.TryReplaceChatBubble(lineId, text)` swaps
  the bubble only if a bubble with that line id still exists. The swap restarts its 3-second timer. The poker table
  bubble does the same. Bubbles get the translated display text without the language tag.
- **Orange bar and message pane:** whisper, group, guild and world-channel lines put a copy on the orange bar (and
  whispers, group and guild also on the message pane). When their translation arrives, the translated line is
  added there as a new entry in the same color.
- **F4 setting:** a Chat-section dropdown **"Chat translation"** with choices **Off / English / Korean (한국어)**
  on `UserOption.ChatLanguage`. It sits next to the chat filter and round-trips like the chat filter mode.

## Error handling summary

| Situation | Result |
|---|---|
| No keys configured | Feature off; no line ids; packets unchanged; the setting is saved and an orange-bar line says it's unavailable. |
| Provider timeout / 5xx / 429 | Try the next provider; if none succeeds within the budget, readers keep the original. Provider not marked exhausted. |
| Provider quota error | Mark exhausted for its period; try the next provider. |
| All providers exhausted | Readers keep originals until a reset. |
| Speaker over daily limit | Readers keep originals; the speaker gets one notice per day. |
| Wrong detected language | Dropped; cached in memory as "no translation". |
| Reader disconnected or line gone from client buffer | Packet skipped or ignored. |
| Cache file corrupt line | Line skipped, logged once. |

## Testing

- **Server unit tests:** language detection; normalization and cache keys; shortcut table; name protection
  (longest match, whole word, case, Korean map); text cleanup (entities, quotes, CP949 fallback, word-boundary
  cut); per-provider request building and response parsing for success, quota and transient cases (with a fake
  `HttpMessageHandler`); chain order, the 90% switch, exhaustion periods and reset days; per-player limit and the
  once-a-day notice; in-flight merging; the cache file round trip and pruning; the no-reader short cut;
  converters (line id trailers present and absent, `ChatTranslation`, the `UserOptions` language trailer with old
  and new layouts); option set and echo; `ChannelService` returning recipients and passing the line id.
- **Client unit tests:** converters; `Chat.TryReplaceMessage`; chat panel row replacement (same index, scroll
  kept); translated-line building with tags and the color code; the bubble swap only for the same line id.
- **By hand (needs keys):**
  1. Put an Azure key in `appsettings.translation.json` and start the local server.
  2. Log in two clients. Set A to Korean and B to English.
  3. A says a Korean line; B sees it swap to `[Kor] …` with the bubble swapping too. Hovering shows the original.
  4. B shouts English; A sees `[영어] …`.
  5. Check a whisper, group, guild, `!global` and `!trade` line.
  6. Say to a merchant in Korean; the merchant reacts to the typed text.
  7. Use `/translation` to check the counts. Set the Azure limit low to force the switch to DeepL.

## Out of scope

- An admin-panel quota page in the file server (the in-game `/translation` command covers it for now).
- Languages other than Korean and English.
- A local translation model.
- Translating chants, NPC and monster lines, boards or mail.

## Setup notes for the user

- **Azure:** create a Translator resource on the **F0** tier. Copy key 1 and the region.
- **DeepL:** create a DeepL API **Free** account and copy the key (it ends in `:fx`).
- **Google:** enable Cloud Translation API and create an API key restricted to that API. A budget does **not** cap
  spending, it only alerts. Cap usage by setting the API's quota "v2 characters per day" to about 16,000
  (500,000 ÷ 31) in the Cloud console's Quotas page.
- Check each provider's data terms, especially DeepL Free, which may keep submitted text.
