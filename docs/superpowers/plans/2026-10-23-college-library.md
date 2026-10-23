# Mileth College Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players write guides, lore, stories, poems and songs, or draw artwork; a College Director or staff member reviews each piece; approved pieces go on bookshelves in a new Library room (map 787) off the College hall.

**Architecture:** The Library is a new part of the existing College system. Server: new message kinds in the College packet family (opcodes 144/152), a `LibraryBook` list in `CollegeState`, pure rules in `LibraryRules`, the service in `CollegeService.Library.cs`, and dispatch in `CollegePanel`. Pieces and pictures reuse `CollegePieceStore` and `CollegePictureStore`. Client: a carousel window, two list windows, Library footers in the reader, and a Library mode in the writer and art canvas. Admin: pipe commands on the game server and a Library page on the file server.

**Tech Stack:** C# / .NET 10, TUnit + FluentAssertions + Moq (server and client tests), MonoGame UI (client), Python 3 (map builder, unittest), ASP.NET Core + plain JS (file server, xunit tests).

**Spec:** `C:\Users\Michael\Documents\GitHub\Chaos.Client\docs\superpowers\specs\2026-10-23-college-library-design.md`

## Global Constraints

- **Workspaces.** Other sessions work in the main checkouts. All work happens in worktrees under `C:\Users\Michael\Documents\GitHub\worktrees\`:
  - Client: `worktrees\library-client` on branch `college-library`, made from Chaos.Client `main`.
  - Server: the client worktree's own submodule checkout, `worktrees\library-client\Chaos-Server`. Run `git submodule update --init` in the worktree. Then create branch `college-library` there from the submodule's current commit. All server code and server tests live there, so the client builds against the changed protocol.
  - Data and maps: `worktrees\library-unora` on branch `college-library`, made from Unora `main`.
  - File server: `worktrees\library-fs` on branch `college-library`, made from Chaos.FileServer `master`.
- **Git rules.** Stage files by explicit path. Never use `git stash`, `git reset` or `git add -A`. Write commit message files into the scratchpad with unique names. Do not push or deploy.
- **Commits.** commitStrategy is at-end. Implementers do NOT commit. Task 18 makes one commit per repo.
- **Paths below** are relative to the worktree named in each task. `Server:` means `worktrees\library-client\Chaos-Server`. `Client:` means `worktrees\library-client`. `Unora:` means `worktrees\library-unora`. `FS:` means `worktrees\library-fs`.
- **Builds and tests.**
  - Server: `dotnet build Chaos.sln` is not needed. Run `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "<filter>"` from `Server:`. `dotnet test` does not work with this SDK.
  - Client: `dotnet build Chaos.Client.slnx`, then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`.
  - Unora: `python -m unittest Tools.College.test_college_maps` and `python Tools/College/college_maps.py --check` and `python Tools/College/check_dialogs.py`.
  - FS: `dotnet test tests/Chaos.FileServer.Tests`.
  - A running `Chaos.exe` or client locks the build output. If a build fails with a locked file, say so; do not kill processes.
- **Text limits.** Orange bar text at most 45 characters. Dialog options at most 35 characters. No em dashes (`—`) in any JSON or player text; use hyphens.
- **Locking.** Every public `CollegeService` method starts with `using var scope = Sync.EnterScope();`. Helpers that assume the lock end in `Locked`. `ICollegeNotifier` must never call back into the service. Mail and orange bars are sent after state is saved.
- **Reviewers ("staff").** A Library reviewer is `isAdmin || isKnight || CollegeRoles.IsDirector(state, name)`. The game has no GM flag. Knights are the GM-level role: they are exempt from mutes like admins (`Aisling.Muted`).
- **Comments.** Only add comments that explain a non-obvious why. No explanatory comments in tests.
- **Code tools.** Use Serena tools for reading and editing code files, per the user's CLAUDE.md.

**User decisions (already made):**
- Entrance: 6 warps at (44,10)–(44,15) on the hall's east wall (map 762); Library exits arrive at (43,12) in the hall; a hall sign reads "Library".
- Room: map 787 (or the next free number), instance `college_library`, 20x20 from `chamber_763_base.map` without benches or lectern; exits (8–11,19), arrival (9,18); College music.
- Bookcases: Guides, Lore, Short stories on the north wall; Poems and songs on the west wall; Artwork on the east wall; each has a floor label; the Librarian stands mid-room facing the door.
- Browsing: carousel with tabs per shelf, search (title or author), sort Newest / A–Z, 20 books per page; covers load only within 2 places of the front.
- Reviewers: Directors and staff; nobody reviews their own book; review in game and on the admin page.
- Reject and staff Take down need a reason of up to 300 characters (`MAX_COMMENT_CHARS`).
- At most 3 items waiting per player; a waiting edit counts as one.
- "Library Author" mark: count of approved books; drops by 1 on a staff take-down; removed at 0; stays when the author takes down their own book; approved edits don't change it.
- New message kinds go in the existing College packet family; no new opcodes.
- Covers are optional, upload or draw, portrait, fit 240x340, and don't count toward the 5 pictures. An Artwork book without a cover uses its drawing. A book with neither shows a plain cover in its spine colour with title and author.
- College pieces can be copied into a Library draft (not music); the College piece stays; picture hashes are shared.
- Edits after approval are reviewed; the shelf version stays until the edit is approved.

## Review Focus

1. **Two reviewers decide the same item at once** (one in the game, one on the admin page). The second decision must be refused with "That item was already decided." and must not change the mark count twice. Test in Task 4.
2. **The author withdraws or re-sends while a reviewer has the item open.** Approve or Reject on a withdrawn item is refused; approving never shelves a draft. Test in Task 4.
3. **A book's piece file is missing or unreadable** (`Pieces.Load` returns null). Opening it says "That book is no longer available." Approving it is refused. Nothing throws. Test in Task 4.
4. **A player is renamed by the file server.** `AislingRenamer.College.cs` rewrites `CollegeState.json` directly. It must also rewrite Library authors, reporters and history names, or the author loses their books and mark. Test in Task 16.
5. **A search string with control characters or over 40 characters.** The server cleans it with `PieceRules.OneLine` and cuts it to 40 characters before matching. An empty search shows the whole shelf. Test in Task 3.

---

## File structure

**Server (`Server:`):**

| File | Change | Responsibility |
|---|---|---|
| `Chaos.DarkAges/Definitions/Enums.cs` | modify | new `CollegeActionType` 48–63, `CollegeDisplayType` 31–36, `CollegeWriterMode.Library`, `CollegePieceContext` 6–9, new Library enums |
| `Chaos.DarkAges/Definitions/CollegeProtocol.cs` | modify | Library constants |
| `Chaos.Networking/Entities/Client/CollegeActionArgs.cs` | modify | Library fields |
| `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs` | modify | Library fields |
| `Chaos.Networking/Entities/Server/LibraryInfos.cs` | create | Library row records |
| `Chaos.Networking/Converters/Client/CollegeActionConverter.cs` | modify | read/write new actions |
| `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs` | modify | read/write new displays |
| `Chaos.Networking/Converters/LibraryCodec.cs` | create | row list codecs |
| `Chaos/Services/College/LibraryModels.cs` | create | `LibraryBook`, `LibraryVersion`, `LibraryReport`, `LibraryEvent`, enums |
| `Chaos/Services/College/LibraryRules.cs` | create | pure rules |
| `Chaos/Services/College/LibraryText.cs` | create | player strings |
| `Chaos/Services/College/CollegeState.cs` | modify | `LibraryBooks`, `NextLibraryBookId` |
| `Chaos/Services/College/CollegeOptions.cs` | modify | `LibraryInstanceId` |
| `Chaos/Services/College/CollegePictureStore.cs` | modify | allow cover size |
| `Chaos/Services/College/CollegeService.Entries.cs` | modify | cleanup counts covers |
| `Chaos/Services/College/CollegeService.Library.cs` | create | writing side and review side |
| `Chaos/Services/College/CollegeNotifier.cs` | modify | `RefreshLibraryMark`, `ApplyLibraryMark` |
| `Chaos/Services/College/CollegePanel.Library.cs` | create | Library dispatch and displays (`CollegePanel` becomes `partial`) |
| `Chaos/Services/College/CollegePanel.cs` | modify | `partial`, route Library actions |
| `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` | modify | login: mark sync and review count |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLibrarianScript.cs` | create | Librarian menu |
| `Chaos/Scripting/ReactorTileScripts/Temauir/College/CollegeBookcaseScript.cs` | create | bookcase click |
| `Chaos/Services/Control/ControlCommandHandler.cs` | modify | `library-*` pipe commands |
| `Tests/Chaos.Tests/College/Library*Tests.cs`, `Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs`, `Tests/Chaos.Tests/Control/LibraryControlCommandTests.cs` | create/modify | tests |

**Client (`Client:`):**

| File | Change | Responsibility |
|---|---|---|
| `Chaos.Client/ViewModel/College/Library/CarouselLayout.cs` | create | spin and placement maths |
| `Chaos.Client/ViewModel/College/Library/CoverChoice.cs` | create | which cover to show |
| `Chaos.Client/ViewModel/College/Library/LibraryLabels.cs` | create | shelf names, status names, spine colours |
| `Chaos.Client/Controls/World/Popups/College/Library/LibraryCarouselControl.cs` | create | carousel window |
| `Chaos.Client/Controls/World/Popups/College/Library/BookSpine.cs`, `BookCover.cs` | create | drawing helpers |
| `Chaos.Client/Controls/World/Popups/College/Library/LibrarySubmissionsControl.cs` | create | My submissions |
| `Chaos.Client/Controls/World/Popups/College/Library/LibraryReviewControl.cs` | create | Review lists |
| `Chaos.Client/Controls/World/Popups/College/Library/LibraryCollegePiecesControl.cs` | create | Bring a College piece |
| `Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs` | modify | Library footers |
| `Chaos.Client/Controls/World/Popups/College/CollegeWriterControl.cs` | modify | Library mode |
| `Chaos.Client/Controls/World/Popups/College/ArtCanvasControl.cs` | modify | Library mode |
| `Chaos.Client/Controls/World/Popups/College/Library/LibraryBookOptions.cs` | create | shelf picker, spine row, cover button (shared by writer and canvas) |
| `Chaos.Client/Systems/College/PicturePrep.cs` | modify | `PrepareCover` |
| `Chaos.Client/Screens/WorldScreen.Library.cs` | create | wiring |
| `Chaos.Client/Screens/WorldScreen.College.cs` | modify | route Library displays |
| `Tests/Chaos.Client.Tests/College/Library/*Tests.cs` | create | tests |

**Unora (`Unora:`):** `Tools/College/college_maps.py`, `Tools/College/test_college_maps.py`, generated map files for 787 and the hall, `Data/Configuration/Templates/Merchants/Temauir/mileth/college/college_librarian.json`, dialogs `college_librarian_*.json`.

**FS (`FS:`):** `src/Chaos.FileServer/Admin/AdminLibraryController.cs`, `Admin/Pages/library.{nav.html,view.html,js}`, `Admin/AdminPageModules.cs`, `Admin/Pages/core.js`, `ServerOps/ControlPipeClient.cs`, `ServerOps/AislingRenamer.College.cs`, tests.

---

### Task 1: Library protocol: enums, args and converters

**Goal:** Add every Library message kind to the College packet family, with round-trip tests.

**Files:**
- Modify: `Server: Chaos.DarkAges/Definitions/Enums.cs` (Mileth College region, lines ~1981–2320)
- Modify: `Server: Chaos.DarkAges/Definitions/CollegeProtocol.cs`
- Modify: `Server: Chaos.Networking/Entities/Client/CollegeActionArgs.cs`
- Modify: `Server: Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs`
- Create: `Server: Chaos.Networking/Entities/Server/LibraryInfos.cs`
- Create: `Server: Chaos.Networking/Converters/LibraryCodec.cs`
- Modify: `Server: Chaos.Networking/Converters/Client/CollegeActionConverter.cs`
- Modify: `Server: Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs`
- Test: `Server: Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `CollegeActionType` has values 48–63 and `CollegeDisplayType` has values 31–36 exactly as listed below.
- [ ] `Every_action_round_trips` and `Every_display_round_trips` cover every new value with a filled sample.
- [ ] `OpenWriter` and `WriterResult` with `Mode = Library` carry `Id`, `Shelf`, `Spine` and `Hash`; other modes are byte-identical to before.
- [ ] An unknown `LibraryShelfCode` byte in `LibraryShelfPage` throws `ArgumentOutOfRangeException` on read.
- [ ] All existing converter tests still pass.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Networking/*/*"` → all pass, 0 failed.

**Steps:**

- [ ] **Step 1: Add the enums.** In `Enums.cs`, append to the existing enums and add new ones in the Mileth College region:

```csharp
// append to CollegeActionType after BoardSavedList = 47
LibraryShelfPage = 48,
LibraryOpenBook = 49,
LibrarySaveDraft = 50,
LibrarySend = 51,
LibraryWithdraw = 52,
LibraryTakeDown = 53,
LibraryReport = 54,
LibrarySubmissions = 55,
LibraryCollegePieces = 56,
LibraryCopyCollege = 57,
LibraryReviewList = 58,
LibraryApprove = 59,
LibraryReject = 60,
LibraryDismiss = 61,
LibraryEdit = 62,
LibraryDrawingFetch = 63

// append to CollegeDisplayType after BoardSaved = 30
LibraryShelf = 31,
LibraryBook = 32,
LibrarySubmissions = 33,
LibraryReview = 34,
LibraryCollegePieces = 35,
LibraryDrawing = 36

// append to CollegeWriterMode
Library = 2

// append to CollegePieceContext
LibraryShelf = 6,
LibraryWaiting = 7,
LibraryReported = 8,
LibraryOwn = 9

public enum LibraryShelfCode : byte { Guides = 0, Lore = 1, ShortStories = 2, PoemsAndSongs = 3, Artwork = 4 }
public enum LibrarySortCode : byte { Newest = 0, AToZ = 1 }
public enum LibraryStatusCode : byte { Draft = 0, Waiting = 1, OnShelf = 2, EditWaiting = 3, Rejected = 4, TakenDown = 5 }
/// <summary>Which version of a book an open asks for, and from which window.</summary>
public enum LibraryOpenMode : byte { Shelf = 0, ReviewWaiting = 1, ReviewShelf = 2, ReviewReported = 3, Own = 4 }
public enum LibraryCopySource : byte { Draft = 0, HandIn = 1, Entry = 2 }
public enum LibraryReviewKind : byte { Waiting = 0, Reported = 1 }
```

- [ ] **Step 2: Add constants** to `CollegeProtocol`:

```csharp
public const int LIBRARY_PAGE_SIZE = 20;
public const int LIBRARY_SPINES = 8;
public const int LIBRARY_MAX_WAITING = 3;
public const int LIBRARY_MAX_SEARCH_CHARS = 40;
public const int MAX_COVER_WIDTH = 240;
public const int MAX_COVER_HEIGHT = 340;
public const int MAX_REASON_CHARS = MAX_COMMENT_CHARS;
```

- [ ] **Step 3: Create `LibraryInfos.cs`** (namespace as `CollegeInfos.cs`):

```csharp
public sealed record LibraryShelfRowInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public byte Spine { get; set; }
    public string CoverHash { get; set; } = string.Empty;
    public bool DrawingCover { get; set; }
    public uint ApprovedUnix { get; set; }
}

public sealed record LibrarySubmissionRowInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public LibraryShelfCode Shelf { get; set; }
    public LibraryStatusCode Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string By { get; set; } = string.Empty;
}

public sealed record LibraryReviewRowInfo
{
    public LibraryReviewKind Kind { get; set; }
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public LibraryShelfCode Shelf { get; set; }
    public bool IsEdit { get; set; }
    public byte ReportCount { get; set; }
    public uint Unix { get; set; }
}

public sealed record LibraryCollegeRowInfo
{
    public LibraryCopySource Source { get; set; }
    public int Id { get; set; }
    public CollegeSubjectCode Subject { get; set; }
    public string Title { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Add fields.** `CollegeActionArgs`: `LibraryShelfCode Shelf`, `LibrarySortCode Sort`, `byte Spine`, `LibraryOpenMode OpenMode`, `LibraryCopySource CopySource`. Reuse the existing `Id`, `Text` (search, reason), `Hash` (cover), `Page`, `Subject`, `Piece`.
  `CollegeDisplayArgs`: `LibraryShelfCode Shelf`, `LibrarySortCode Sort`, `string Search = ""`, `ushort Total`, `byte Spine`, `bool CanTakeDown`, `bool CanReview`, `bool CanReport`, `bool HasShelfVersion`, and lists `ShelfRows`, `SubmissionRows`, `ReviewRows`, `CollegeRows` (each `= []`). Reuse `Id`, `Header`, `Author`, `Hash` (cover), `Context`, `Votes` (reports: `Judge` = reporter, `Comment` = reason, `Tier` = None), `Piece`, `Page`, `PageCount`, `Palette`, `Pixels`, `Message`, `Result`, `Mode`, `Subject`.

- [ ] **Step 5: Create `LibraryCodec`** with `Write/Read` pairs for each row list. A list is a `ushort` count, then each row's fields in declaration order. Strings: titles and names `String8`, reasons `String16`. Enum bytes are validated with `Enum.IsDefined` on read, throwing `ArgumentOutOfRangeException` like `CollegeActionConverter` does for the type byte. Add `ReadShelf(ref SpanReader)` that validates `LibraryShelfCode`.

- [ ] **Step 6: Converters.** In `CollegeActionConverter.Deserialize` and `Serialize`, mirror each other:

| Action | Fields after the type byte |
|---|---|
| LibraryShelfPage | shelf, sort, page byte, `String8` Text |
| LibraryOpenBook | `Int32` Id, OpenMode byte |
| LibrarySaveDraft, LibrarySend | `Int32` Id, shelf, spine byte, `String8` Hash, `CollegeCodec` subject + piece (as SaveDraft) |
| LibraryWithdraw, LibraryApprove, LibraryDismiss, LibraryEdit, LibraryDrawingFetch | `Int32` Id |
| LibraryEdit additionally | shelf (used when Id is 0: a new book on that shelf) |
| LibraryTakeDown, LibraryReport, LibraryReject | `Int32` Id, `String16` Text |
| LibrarySubmissions, LibraryCollegePieces, LibraryReviewList | nothing |
| LibraryCopyCollege | CopySource byte, `Int32` Id |

  In `CollegeDisplayConverter`:

| Display | Fields |
|---|---|
| LibraryShelf | shelf, sort, `String8` Search, Page, PageCount, `UInt16` Total, shelf rows |
| LibraryBook | Context, `Int32` Id, `String8` Header, `String8` Author, `String8` Hash, Spine, CanTakeDown, CanReview, CanReport, HasShelfVersion, votes (`CollegeCodec.WriteVotes`), piece |
| LibrarySubmissions | submission rows |
| LibraryReview | review rows |
| LibraryCollegePieces | college rows |
| LibraryDrawing | same fields and order as the existing `Drawing` case |
| OpenWriter, WriterResult | when `Mode == Library`, append `Int32` Id, shelf, spine, `String8` Hash after the existing fields |

- [ ] **Step 7: Tests.** Add a filled sample for every new action and display to `Actions()` and `Displays()` in `CollegePacketConverterTests.cs`, plus Library-mode samples of `OpenWriter` and `WriterResult`. Add:

```csharp
[Test]
public void An_unknown_library_shelf_is_refused()
{
    byte[] body = [(byte)CollegeActionType.LibraryShelfPage, 9, 0, 0, 0];
    var act = () => ReadAction(body);
    act.Should().Throw<ArgumentOutOfRangeException>();
}
```

  Use the file's existing reader helper. If it has no raw-bytes helper, add `ReadAction(byte[])` next to `RoundTrip` using `new SpanReader(Enc, body)` and `new CollegeActionConverter().Deserialize(ref reader)`.

- [ ] **Step 8: Run the Verify command.** Expected: all Networking tests pass.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CollegeProtocol.cs", "Chaos.Networking/Entities/Client/CollegeActionArgs.cs", "Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs", "Chaos.Networking/Entities/Server/LibraryInfos.cs", "Chaos.Networking/Converters/LibraryCodec.cs", "Chaos.Networking/Converters/Client/CollegeActionConverter.cs", "Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs", "Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.Networking/*/*\"", "acceptanceCriteria": ["Action types 48-63 and display types 31-36 exist as listed", "Every new value round-trips", "Library-mode OpenWriter and WriterResult carry Id, Shelf, Spine, Hash; other modes unchanged", "Unknown shelf byte throws ArgumentOutOfRangeException", "Existing converter tests pass"], "modelTier": "standard"}
```

---

### Task 2: Library models and LibraryRules

**Goal:** Add the stored book model and every pure Library rule, fully unit tested.

**Files:**
- Create: `Server: Chaos/Services/College/LibraryModels.cs`
- Create: `Server: Chaos/Services/College/LibraryRules.cs`
- Test: `Server: Tests/Chaos.Tests/College/LibraryRulesTests.cs`

**Acceptance Criteria:**
- [ ] `LibraryRules.StatusOf` returns each of the 6 statuses for the book states in the tests.
- [ ] `MayReview` is false for the author (ignoring case) and true for anyone else.
- [ ] `WaitingCount` counts books with a waiting version, including edits; `MaySend` is false at 3.
- [ ] `MarkCount` counts only `Counted` books by that author.
- [ ] `MayReport` refuses the author, a second report by the same player (ignoring case), and books not on the shelf.
- [ ] `ShelfFits` refuses Artwork for a writing piece and a writing shelf for a drawing.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/LibraryRulesTests/*"` → all pass.

**Steps:**

- [ ] **Step 1: Create `LibraryModels.cs`:**

```csharp
namespace Chaos.Services.College;

public enum LibraryShelf { Guides, Lore, ShortStories, PoemsAndSongs, Artwork }

public enum LibraryStatus { Draft, Waiting, OnShelf, EditWaiting, Rejected, TakenDown }

public enum LibraryAction { Sent, Approved, Rejected, Withdrawn, Reported, Dismissed, TakenDown }

/// <summary>One saved version of a book: its piece file, cover and shelf details.</summary>
public sealed class LibraryVersion
{
    public string PieceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public LibraryShelf Shelf { get; set; }
    public byte Spine { get; set; }
    public string CoverHash { get; set; } = string.Empty;
    public DateTime Utc { get; set; }
}

public sealed class LibraryReport
{
    public string By { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime Utc { get; set; }
}

public sealed class LibraryEvent
{
    public LibraryAction Action { get; set; }
    public string By { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime Utc { get; set; }
}

/// <summary>
///     A Library book. Draft and Waiting are never both set: sending moves the draft to Waiting, and
///     editing is refused while a version waits.
/// </summary>
public sealed class LibraryBook
{
    public int Id { get; set; }
    public string Author { get; set; } = string.Empty;
    public LibraryVersion? Shelved { get; set; }
    public LibraryVersion? Waiting { get; set; }
    public LibraryVersion? Draft { get; set; }
    public string RejectReason { get; set; } = string.Empty;
    public bool TakenDown { get; set; }
    public string TakenDownBy { get; set; } = string.Empty;
    public string TakenDownReason { get; set; } = string.Empty;

    /// <summary>True while this book adds 1 to the author's Library Author mark.</summary>
    public bool Counted { get; set; }

    public List<LibraryReport> Reports { get; set; } = [];
    public List<LibraryEvent> History { get; set; } = [];

    public bool IsAuthor(string name) => Author.Equals(name, StringComparison.OrdinalIgnoreCase);

    public IEnumerable<LibraryVersion> Versions()
    {
        if (Shelved is not null) yield return Shelved;
        if (Waiting is not null) yield return Waiting;
        if (Draft is not null) yield return Draft;
    }
}
```

- [ ] **Step 2: Write the failing tests** in `LibraryRulesTests.cs` (namespace `Chaos.Tests.College`, TUnit `[Test]`, snake_case sentence names):

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public class LibraryRulesTests
{
    private static LibraryVersion Version(string title = "A book", LibraryShelf shelf = LibraryShelf.Lore)
        => new() { PieceId = Guid.NewGuid().ToString("N"), Title = title, Shelf = shelf, Utc = new DateTime(2026, 10, 23) };

    private static LibraryBook Book(string author = "Aroha") => new() { Id = 1, Author = author };

    [Test]
    public void A_book_with_only_a_draft_is_a_draft()
    {
        var book = Book();
        book.Draft = Version();
        LibraryRules.StatusOf(book).Should().Be(LibraryStatus.Draft);
    }
}
```

  Write each of the following tests in the same style. The required tests:

  - `A_book_with_only_a_draft_is_a_draft`
  - `A_sent_book_never_approved_is_waiting` (Waiting set, Shelved null)
  - `An_approved_book_is_on_the_shelf`
  - `An_approved_book_with_a_waiting_version_has_an_edit_waiting`
  - `A_rejected_draft_shows_rejected` (Draft set, RejectReason "Too short")
  - `A_rejected_edit_still_shows_rejected_while_the_shelf_version_stays` (Shelved and Draft set, RejectReason set)
  - `A_taken_down_book_shows_taken_down` (TakenDown true, Shelved null, Draft null)
  - `A_taken_down_book_sent_again_shows_waiting` (TakenDown true, Waiting set)
  - `Nobody_reviews_their_own_book` (`MayReview(book, "AROHA")` false; `"Mira"` true)
  - `Edits_count_toward_the_waiting_limit` (3 books by Aroha: one Waiting, one Shelved+Waiting, one Waiting → `WaitingCount` 3, `MaySend` false; another author's waiting book not counted)
  - `Two_waiting_items_leave_room_for_one_more` (`MaySend` true at 2)
  - `The_mark_counts_only_counted_books`
  - `A_player_reports_a_book_once` (`MayReport` false when `Reports` has `By = "mira"` and the reporter is `"Mira"`)
  - `Authors_cannot_report_their_own_book`
  - `Only_shelved_books_can_be_reported`
  - `Artwork_takes_only_drawings` (`ShelfFits(LibraryShelf.Artwork, PieceKind.Drawing)` true, `(Artwork, Writing)` false, `(Lore, Drawing)` false, `(Lore, Writing)` true, `(Lore, Tune)` false)

- [ ] **Step 3: Run the tests.** Expected: FAIL, `LibraryRules` does not exist.

- [ ] **Step 4: Create `LibraryRules.cs`:**

```csharp
namespace Chaos.Services.College;

public static class LibraryRules
{
    public static LibraryStatus StatusOf(LibraryBook book)
    {
        if (book.Waiting is not null)
            return book.Shelved is not null ? LibraryStatus.EditWaiting : LibraryStatus.Waiting;

        if (book.TakenDown)
            return LibraryStatus.TakenDown;

        if (book.RejectReason.Length > 0)
            return LibraryStatus.Rejected;

        return book.Shelved is not null ? LibraryStatus.OnShelf : LibraryStatus.Draft;
    }

    public static bool MayReview(LibraryBook book, string reviewer) => !book.IsAuthor(reviewer);

    public static int WaitingCount(IEnumerable<LibraryBook> books, string author)
        => books.Count(b => b.IsAuthor(author) && (b.Waiting is not null));

    public static bool MaySend(IEnumerable<LibraryBook> books, string author)
        => WaitingCount(books, author) < CollegeProtocol.LIBRARY_MAX_WAITING;

    public static int MarkCount(IEnumerable<LibraryBook> books, string author)
        => books.Count(b => b.IsAuthor(author) && b.Counted);

    public static bool MayReport(LibraryBook book, string reporter)
        => (book.Shelved is not null)
           && !book.IsAuthor(reporter)
           && !book.Reports.Any(r => r.By.Equals(reporter, StringComparison.OrdinalIgnoreCase));

    public static bool ShelfFits(LibraryShelf shelf, PieceKind kind)
        => shelf == LibraryShelf.Artwork ? kind == PieceKind.Drawing : kind == PieceKind.Writing;
}
```

  Add `using Chaos.DarkAges.Definitions;` if `CollegeProtocol` needs it.

- [ ] **Step 5: Run the Verify command.** Expected: all pass.

```json:metadata
{"files": ["Chaos/Services/College/LibraryModels.cs", "Chaos/Services/College/LibraryRules.cs", "Tests/Chaos.Tests/College/LibraryRulesTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/LibraryRulesTests/*\"", "acceptanceCriteria": ["StatusOf returns all 6 statuses for the listed states", "MayReview false for the author ignoring case", "WaitingCount counts edits; MaySend false at 3", "MarkCount counts only Counted books", "MayReport refuses author, repeat reporter, unshelved books", "ShelfFits pairs Artwork with drawings only"], "modelTier": "mechanical"}
```

---

### Task 3: Library storage, cover size and picture cleanup

**Goal:** Store books in `CollegeState`, allow cover-sized pictures, keep covers through the unused-picture cleanup, and add the shelf page query.

**Files:**
- Modify: `Server: Chaos/Services/College/CollegeState.cs`
- Modify: `Server: Chaos/Services/College/CollegeOptions.cs`
- Modify: `Server: Chaos/Services/College/CollegePictureStore.cs`
- Modify: `Server: Chaos/Services/College/CollegeService.Entries.cs` (`CleanupLocked`, lines ~627–642)
- Modify: `Server: Chaos/Services/College/LibraryRules.cs` (add `ShelfPage`, `CleanSearch`)
- Test: `Server: Tests/Chaos.Tests/College/LibraryStorageTests.cs`, `Server: Tests/Chaos.Tests/College/CollegePictureStoreTests.cs`, `Server: Tests/Chaos.Tests/College/LibraryRulesTests.cs`

**Acceptance Criteria:**
- [ ] `CollegeState` has `List<LibraryBook> LibraryBooks = []` and `int NextLibraryBookId = 1`; a state JSON without them loads with the defaults.
- [ ] `CollegePictureStore.Add` accepts a 240x340 PNG and still refuses 401x300 and 241x341.
- [ ] The hourly sweep never deletes a hash used as a cover by any version of any book, even after `UnusedPictureHours`.
- [ ] `ShelfPage` filters by shelf and search (title or author, ignoring case), sorts Newest or A–Z, and pages by 20.
- [ ] `CleanSearch` turns `"  Ghost\u0001 Story  "` into `"Ghost Story"` and cuts input to 40 characters.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.College/*/*"` → all pass.

**Steps:**

- [ ] **Step 1: State and options.** Add to `CollegeState`:

```csharp
public List<LibraryBook> LibraryBooks { get; set; } = [];
public int NextLibraryBookId { get; set; } = 1;

public LibraryBook? LibraryBook(int id) => LibraryBooks.FirstOrDefault(b => b.Id == id);
```

  Add `public string LibraryInstanceId { get; init; } = "college_library";` to `CollegeOptions`, next to `HallInstanceId`, and `public bool IsLibrary(string instanceId) => LibraryInstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase);`.

- [ ] **Step 2: Cover size.** In `CollegePictureStore.Add`, replace the 400x300 size check with:

```csharp
var fitsContent = (width <= CollegeProtocol.MAX_PICTURE_WIDTH) && (height <= CollegeProtocol.MAX_PICTURE_HEIGHT);
var fitsCover = (width <= CollegeProtocol.MAX_COVER_WIDTH) && (height <= CollegeProtocol.MAX_COVER_HEIGHT);
if (!fitsContent && !fitsCover)
    return PictureProblem.TooBig;
```

  Keep the variable names the method already uses. Add tests to `CollegePictureStoreTests` named `A_portrait_cover_picture_is_accepted` and `A_picture_bigger_than_both_shapes_is_refused`, building PNGs the same way the file's existing tests do.

- [ ] **Step 3: Cleanup.** In `CleanupLocked`, pass covers too:

```csharp
var referenced = new HashSet<string>(Pieces.AllPictureHashes(), StringComparer.Ordinal);
referenced.UnionWith(State.LibraryBooks.SelectMany(b => b.Versions()).Select(v => v.CoverHash).Where(h => h.Length > 0));
Pictures.Sweep(referenced, now);
```

  Book pieces are saved in `CollegePieceStore`, so their Picture blocks are already counted by `AllPictureHashes()`.

- [ ] **Step 4: Shelf page.** Add to `LibraryRules`:

```csharp
public sealed record ShelfPageResult(IReadOnlyList<LibraryBook> Books, int Page, int PageCount, int Total);

public static string CleanSearch(string text)
{
    var clean = PieceRules.OneLine(text);
    return clean.Length > CollegeProtocol.LIBRARY_MAX_SEARCH_CHARS ? clean[..CollegeProtocol.LIBRARY_MAX_SEARCH_CHARS] : clean;
}

public static ShelfPageResult ShelfPage(IEnumerable<LibraryBook> books, LibraryShelf shelf, string search, LibrarySortCode sort, int page)
{
    var term = CleanSearch(search);
    var onShelf = books.Where(b => b.Shelved?.Shelf == shelf)
                       .Where(b => (term.Length == 0)
                                   || b.Shelved!.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                                   || b.Author.Contains(term, StringComparison.OrdinalIgnoreCase));
    var sorted = sort == LibrarySortCode.AToZ
        ? onShelf.OrderBy(b => b.Shelved!.Title, StringComparer.OrdinalIgnoreCase).ThenBy(b => b.Id)
        : onShelf.OrderByDescending(b => b.Shelved!.Utc).ThenByDescending(b => b.Id);
    var all = sorted.ToList();
    var size = CollegeProtocol.LIBRARY_PAGE_SIZE;
    var pageCount = Math.Max(1, (all.Count + size - 1) / size);
    var at = Math.Clamp(page, 0, pageCount - 1);
    return new ShelfPageResult(all.Skip(at * size).Take(size).ToList(), at, pageCount, all.Count);
}
```

  `PieceRules.OneLine` drops control characters, so `"Ghost\u0001 Story"` becomes `"Ghost Story"`.

- [ ] **Step 5: Tests.** Add to `LibraryRulesTests`:
  - `The_shelf_page_shows_only_that_shelf_newest_first`
  - `A_to_z_sorts_by_title_ignoring_case`
  - `Search_matches_title_or_author_ignoring_case`
  - `A_search_with_control_characters_and_extra_length_is_cleaned` (asserts `CleanSearch(new string('a', 60)).Length == 40` and the "Ghost Story" case above)
  - `Twenty_five_books_make_two_pages_and_a_too_high_page_shows_the_last` (page 5 → `Page == 1`, 5 books)

  Create `LibraryStorageTests.cs`. Build the rig the way `CollegeEntryServiceTests.Create()` does (temp dir, `Mock<IStorage<CollegeState>>`, real stores, `FixedTime`). Tests:
  - `Old_state_json_without_library_fields_loads_with_defaults`: deserialize `{"Directors":[]}` with `System.Text.Json` into `CollegeState`; assert `LibraryBooks` is empty and `NextLibraryBookId == 1`.
  - `A_cover_is_kept_by_the_sweep_while_its_book_exists`: add a picture to the store, add a book whose `Shelved.CoverHash` is that hash, advance `FixedTime` by `UnusedPictureHours + 1` hours twice, call `Tick()` each time, and assert `Pictures.Has(hash)`. Then remove the book, advance twice more, tick, and assert the picture is gone.

  `CleanupLocked` runs at most once an hour from `Tick()`. Advance the clock enough between ticks.

- [ ] **Step 6: Run the Verify command.** Expected: all College tests pass.

```json:metadata
{"files": ["Chaos/Services/College/CollegeState.cs", "Chaos/Services/College/CollegeOptions.cs", "Chaos/Services/College/CollegePictureStore.cs", "Chaos/Services/College/CollegeService.Entries.cs", "Chaos/Services/College/LibraryRules.cs", "Tests/Chaos.Tests/College/LibraryStorageTests.cs", "Tests/Chaos.Tests/College/CollegePictureStoreTests.cs", "Tests/Chaos.Tests/College/LibraryRulesTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.College/*/*\"", "acceptanceCriteria": ["CollegeState has LibraryBooks and NextLibraryBookId with defaults for old JSON", "Store accepts 240x340 and refuses 401x300 and 241x341", "Sweep never deletes a cover of an existing book", "ShelfPage filters, sorts and pages by 20", "CleanSearch strips control chars and cuts to 40"], "modelTier": "standard"}
```

---

### Task 4: CollegeService.Library: writing, review and marks

**Goal:** Add every Library service operation: drafts, sending, withdrawing, editing, copying a College piece, submissions, shelf pages, opening books, reporting, review decisions, take-downs, marks and notifications.

**Files:**
- Create: `Server: Chaos/Services/College/CollegeService.Library.cs`
- Create: `Server: Chaos/Services/College/LibraryText.cs`
- Modify: `Server: Chaos/Services/College/CollegeNotifier.cs` (interface + class)
- Test: `Server: Tests/Chaos.Tests/College/LibraryServiceTests.cs`

**Acceptance Criteria:**
- [ ] All the tests listed in Step 3 pass, including the Review Focus cases 1–3.
- [ ] A 4th send while 3 items wait returns `LibraryText.TOO_MANY_WAITING` and changes nothing.
- [ ] Approving a new book sets `Counted`, adds 1 to the mark and calls `Notifier.RefreshLibraryMark(author, count)`; approving an edit leaves the count alone and deletes the old shelf piece file.
- [ ] A staff take-down lowers the count by 1; an author take-down keeps it.
- [ ] Every decision mails the author and tells them by orange bar, after `SaveLocked()`.
- [ ] Every action appends a `LibraryEvent` with who, when and reason.
- [ ] Copying a College piece makes a new piece id with the same picture hashes, and the College piece file is unchanged.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/LibraryServiceTests/*"` → all pass.

**Steps:**

- [ ] **Step 1: Strings.** Create `LibraryText.cs`. Bar strings must be 45 characters or fewer; the test in Step 3 checks this.

```csharp
namespace Chaos.Services.College;

public static class LibraryText
{
    public const string DRAFT_SAVED = "Library draft saved.";
    public const string SENT = "Sent to the Library for review.";
    public const string TOO_MANY_WAITING = "You have 3 pieces waiting for review.";
    public const string WITHDRAWN = "Taken back from review.";
    public const string NOT_YOURS = "That is not your book.";
    public const string NO_BOOK = "That book is no longer available.";
    public const string ALREADY_DECIDED = "That item was already decided.";
    public const string OWN_BOOK = "You can't review your own book.";
    public const string NOT_ALLOWED = "Only Directors and staff can review.";
    public const string NEED_REASON = "Please give a reason.";
    public const string WRONG_SHELF = "That piece doesn't fit that shelf.";
    public const string STILL_WAITING = "Take it back from review to edit it.";
    public const string NOTHING_TO_SEND = "There is nothing new to send.";
    public const string BAD_COVER = "The cover picture is missing.";
    public const string REPORTED = "Thank you. Staff will look at it.";
    public const string CANT_REPORT = "You can't report that book.";
    public const string APPROVED_BAR = "Your book is on the Library shelf.";
    public const string REJECTED_BAR = "Your Library piece was not approved.";
    public const string TAKEN_DOWN_BAR = "Your book was taken off the shelf.";
    public const string TAKEN_DOWN = "The book is off the shelf.";
    public const string DISMISSED = "Reports cleared.";
    public const string APPROVED = "Approved.";
    public const string REJECTED = "Rejected.";
    public const string COPIED = "Copied into a new Library draft.";
    public const string MAIL_SUBJECT = "Mileth Library";

    public static string ToReview(int count) => $"Library: {count} to review";

    public static string ApprovedMail(string title) => $"\"{title}\" is now on the Library shelf.";
    public static string RejectedMail(string title, string reason) => $"\"{title}\" was not approved.\n\nReason: {reason}\n\nYou can fix it and send it again.";
    public static string TakenDownMail(string title, string by, string reason) => $"\"{title}\" was taken off the Library shelf by {by}.\n\nReason: {reason}";
}
```

- [ ] **Step 2: Notifier.** Add to `ICollegeNotifier`: `void RefreshLibraryMark(string name, int count);`. In `CollegeNotifier`, add the constants `LIBRARY_KEY = "libraryAuthor"` and `LIBRARY_TEXT = "Library Author"`, and add `public static void ApplyLibraryMark(Aisling aisling, int count)` with the same shape as `ApplyEducatedMark`. Use `MarkIcon.Wizard` and `MarkColor.LightYellow`, and remove the mark at 0. `RefreshLibraryMark` finds the online player the way `Tell` does and calls `ApplyLibraryMark`. Update every existing test fake of `ICollegeNotifier`; Moq mocks need no change.

- [ ] **Step 3: Write the failing tests.** Create `LibraryServiceTests.cs` with a `Rig` and `Create()` copied from `CollegeEntryServiceTests` (it exposes `Service`, `State`, `Time`, `Mail`, `Notifier`, `Pieces`, `Pictures`). Helpers:
  - `Writing(string title, int chars)` builds a `CollegePiece` with one Text block of `chars` letters. Copy it from `CollegeEntryServiceTests` if it exists there.
  - `Drawing(string title)` builds an Art piece with one valid Drawing block. Copy it from the existing art tests.
  - `Shelve(rig, author, shelf)` saves, sends and approves (approved by `"Mira"`, staff), and returns the book id.

  Required tests:
  - `Saving_a_new_draft_makes_a_book_with_the_next_id`
  - `Saving_again_keeps_the_same_book_and_piece_id`
  - `Saving_a_writing_piece_to_the_artwork_shelf_is_refused` (`WRONG_SHELF`)
  - `A_draft_with_a_missing_cover_picture_is_refused` (`BAD_COVER`)
  - `Sending_moves_the_draft_to_waiting`
  - `A_fourth_send_is_refused_while_three_wait`
  - `Withdrawing_moves_waiting_back_to_draft`
  - `Editing_a_shelved_book_copies_the_shelf_piece_into_a_new_draft` (new piece id, same Picture hashes, `Shelved` unchanged)
  - `Editing_while_a_version_waits_is_refused` (`STILL_WAITING`)
  - `Approving_a_new_book_shelves_it_and_counts_the_mark` (Notifier `RefreshLibraryMark("Aroha", 1)`, Mail `Send("Aroha", MAIL_SUBJECT, ...)`, Notifier `Tell("Aroha", APPROVED_BAR)`)
  - `Approving_an_edit_replaces_the_shelf_version_and_keeps_the_count` (old shelf piece file deleted: `Pieces.Load(oldId)` is null)
  - `Rejecting_an_edit_keeps_the_shelf_version` (Waiting moves to Draft, `RejectReason` set, `Shelved` unchanged)
  - `Rejecting_needs_a_reason` (empty or whitespace → `NEED_REASON`; 301 characters → cut to 300)
  - `Nobody_can_review_their_own_book`
  - `Players_who_are_not_staff_or_directors_cannot_review` (Director via `AddDirector("Mira")` allowed; knight allowed; plain player → `NOT_ALLOWED`)
  - `A_second_decision_on_the_same_item_is_refused` (approve twice → second `ALREADY_DECIDED`, count still 1)
  - `Approving_after_the_author_withdrew_is_refused` (`ALREADY_DECIDED`, the draft is not shelved)
  - `Opening_a_book_whose_piece_file_is_missing_says_it_is_gone` (delete the piece file, `OpenBook` returns null; `Approve` → `NO_BOOK`)
  - `A_staff_take_down_lowers_the_mark` (count 1 → 0, `RefreshLibraryMark("Aroha", 0)`)
  - `An_author_take_down_keeps_the_mark`
  - `A_book_taken_down_by_staff_and_approved_again_counts_once`
  - `Reporting_twice_is_refused_and_dismiss_clears_reports`
  - `Copying_a_college_draft_shares_pictures_and_keeps_the_original` (save a College Lore draft with a Picture block, copy with `LibraryCopySource.Draft` and id `(int)CollegeSubject.Lore`, assert the new piece id differs, its picture hashes are equal, the College draft still loads, and the shelf is `Lore`)
  - `Music_pieces_are_not_offered_for_copying` (`CollegePiecesOf` has no Music rows; copying a Music source returns a refusal)
  - `Every_action_is_recorded_in_history` (send, approve, report, dismiss, take down → 5 events in order with `By`)
  - `Bar_texts_fit_the_orange_bar`: reflect over `LibraryText` const strings and assert each is at most 45 characters, except `MAIL_SUBJECT`.

- [ ] **Step 4: Run the tests.** Expected: FAIL, the methods do not exist.

- [ ] **Step 5: Implement `CollegeService.Library.cs`** (`public sealed partial class CollegeService`). Records at the top:

```csharp
public sealed record LibraryOutcome(bool Ok, string Message, int BookId = 0);

public sealed record LibraryBookView(LibraryBook Book, LibraryVersion Version, CollegePiece Piece, CollegePieceContext Context,
    bool CanTakeDown, bool CanReview, bool CanReport, bool HasShelfVersion);

public sealed record LibraryCollegeRow(LibraryCopySource Source, int Id, CollegeSubject Subject, string Title);

public sealed record LibraryWriterSession(int BookId, LibraryShelf Shelf, byte Spine, string CoverHash, CollegePiece? Piece);
```

  Public methods. Each takes the lock, saves with `SaveLocked()` after a change, and runs mail and bar notices after saving, each in its own try/catch (use `TryMail` and a try/catch around `Notifier.Tell`).

| Method | Behaviour |
|---|---|
| `bool CanReviewLibrary(string name, bool isKnight, bool isAdmin)` | `isAdmin \|\| isKnight \|\| CollegeRoles.IsDirector(State, name)` |
| `LibraryOutcome SaveLibraryDraft(string name, int bookId, LibraryShelf shelf, byte spine, string coverHash, CollegePiece piece)` | `bookId == 0` makes a new book (`Id = State.NextLibraryBookId++`, `Author = name`). Otherwise the book must exist and `IsAuthor(name)`, or return `NOT_YOURS`. Refuse while `Waiting` is set (`STILL_WAITING`). Clamp `spine` to `0..LIBRARY_SPINES-1`. Normalize with `PieceRules.Normalize`, set `Kind = CollegePiece.KindOf(piece.Subject)`, refuse `!LibraryRules.ShelfFits` (`WRONG_SHELF`), run `PieceRules.Check(clean, Pictures.Has)` and return `CollegeText.PieceProblem(check, subject)` on failure. A non-empty `coverHash` must pass `PieceRules.IsHash` and `Pictures.Has`, else `BAD_COVER`. Reuse the draft's piece id if a draft exists, else `CollegePiece.NewId()`. `Pieces.Save` in a try/catch like `SaveDraft`. Set `book.Draft = new LibraryVersion{...}` with `Title = clean.Title`, `Utc = Now`. Do not clear `RejectReason` here. Return `DRAFT_SAVED` and the book id. |
| `LibraryOutcome SendLibrary(string name, int bookId)` | Book must be the author's. Needs a `Draft` (`NOTHING_TO_SEND`). Load the piece and run `PieceRules.CheckEntry` with the Library minimum: `CheckHandIn` (20 characters / 70 drawn pixels) plus a non-empty title (`NoTitle`). Refuse if `!LibraryRules.MaySend` (`TOO_MANY_WAITING`). Move Draft to Waiting, clear `RejectReason`, set `TakenDown = false`, add a `Sent` event, and return `SENT`. |
| `LibraryOutcome WithdrawLibrary(string name, int bookId)` | Author only, needs `Waiting`. Move Waiting to Draft, add a `Withdrawn` event. |
| `LibraryWriterSession? EditLibrary(string name, int bookId, LibraryShelf newShelf)` | `bookId == 0` returns an empty session on `newShelf`, spine 0. Otherwise author only. Returns null while `Waiting` is set; the panel shows `STILL_WAITING`. If `Draft` exists, load it. Else copy the `Shelved` piece into a new piece id (`CopyPieceLocked`) and set it as `Draft`. |
| `LibraryOutcome TakeDownLibrary(string by, bool isKnight, bool isAdmin, int bookId, string reason)` | Needs `Shelved`. The author may always do it, with an optional reason. Staff and Directors need a reason (`NEED_REASON`), cut to `MAX_REASON_CHARS` after `PieceRules.OneLine`. Set `Shelved = null`, `TakenDown = true`, `TakenDownBy`, `TakenDownReason`, clear `Reports`. When the taker is not the author and `Counted` is true: set `Counted = false` and refresh the mark. Delete the old shelf piece file if no other version uses that piece id. Add a `TakenDown` event. When not the author: mail `TakenDownMail` and tell `TAKEN_DOWN_BAR`. Staff may take down their own book; that is an author take-down. |
| `LibraryOutcome ReportLibrary(string name, int bookId, string reason)` | Reason is required, cleaned and cut like above. `LibraryRules.MayReport`, else `CANT_REPORT`. Add the report and a `Reported` event. |
| `IReadOnlyList<LibraryBook> SubmissionsOf(string name)` | The author's books, newest id first. Return copies or the live objects only for reading inside the panel, which maps them under the lock. Simplest: return `LibrarySubmissionRow` records built here (`Id`, `Title` of Draft ?? Waiting ?? Shelved ?? "Untitled", `Shelf` of the same version, `Status`, `Reason` = RejectReason or TakenDownReason, `By` = TakenDownBy). |
| `IReadOnlyList<LibraryCollegeRow> CollegePiecesOf(string name)` | The player's College drafts (`State.Drafts`, writing subjects and Art; the id is `(int)subject`), hand-ins where `Student` is the player, and gallery entries the player authored with `Status == Claimed`. Exclude Music everywhere. Use the piece title. |
| `LibraryWriterSession? CopyCollegePiece(string name, LibraryCopySource source, int id)` | Load the source piece (author or student check as above; null if not theirs or Music). Copy into a new piece id with the same blocks. Make a new book with this as its `Draft`. Default shelf: Art → Artwork, Lore → Lore, Literature → ShortStories, History and Philosophy → Guides. Save and return the session. |
| `LibraryShelfPageView ShelfPage(LibraryShelf shelf, string search, LibrarySortCode sort, int page)` | Wraps `LibraryRules.ShelfPage`. Returns rows with `DrawingCover = Shelved.Shelf == Artwork && CoverHash.Length == 0`. |
| `LibraryBookView? OpenBook(string name, bool isKnight, bool isAdmin, int bookId, LibraryOpenMode mode)` | `Shelf`: needs `Shelved`; context `LibraryShelf`. `Own`: author only; shows Draft ?? Waiting ?? Shelved; context `LibraryOwn`. `ReviewWaiting`: reviewer only, needs `Waiting`; context `LibraryWaiting`. `ReviewShelf`: reviewer only, needs `Shelved`; context `LibraryWaiting`; this is the switch between the shelf version and the new one. `ReviewReported`: reviewer only, needs `Shelved` and reports; context `LibraryReported`. Load the piece; null on a missing file. `CanTakeDown = (Shelved is not null) && (author or reviewer)`. `CanReview = reviewer && !author`. `CanReport = MayReport(book, name)`. `HasShelfVersion = Shelved is not null && Waiting is not null`. |
| `IReadOnlyList<LibraryReviewRow> ReviewList(string name, bool isKnight, bool isAdmin)` | Empty when not a reviewer. Waiting rows, oldest `Waiting.Utc` first, with `IsEdit = Shelved is not null`. Reported rows, books with reports, oldest first report first, with `ReportCount`. Leave out the reviewer's own books. |
| `int LibraryToReview(string name, bool isKnight, bool isAdmin)` | Count of review rows. |
| `LibraryOutcome ApproveLibrary(string by, bool isKnight, bool isAdmin, int bookId)` | Order of checks: reviewer (`NOT_ALLOWED`), book exists (`NO_BOOK`), `MayReview` (`OWN_BOOK`), `Waiting` set (`ALREADY_DECIDED`), waiting piece loads (`NO_BOOK`). Delete the old shelf piece file if its id differs. `Shelved = Waiting` with `Utc = Now`, `Waiting = null`, `TakenDown = false`, `RejectReason = ""`. If `!Counted`: set `Counted = true` and refresh the mark. Add an `Approved` event, mail `ApprovedMail`, tell `APPROVED_BAR`. |
| `LibraryOutcome RejectLibrary(string by, bool isKnight, bool isAdmin, int bookId, string reason)` | Same checks plus reason. `Draft = Waiting`, `Waiting = null`, `RejectReason = reason`. Add a `Rejected` event, mail `RejectedMail`, tell `REJECTED_BAR`. |
| `LibraryOutcome DismissLibrary(string by, bool isKnight, bool isAdmin, int bookId)` | Reviewer, book exists, not own, has reports (`ALREADY_DECIDED` if none). Clear reports, add a `Dismissed` event. |
| `IReadOnlyList<LibraryBook> LibraryBooksForAdmin()` and `LibraryBookView? LibraryBookForAdmin(int id, bool waiting)` | For the pipe (Task 8). Return deep copies made with `JsonSerializer` round-trip so callers can't change state outside the lock. |
| `int LibraryMarkOf(string name)` | `LibraryRules.MarkCount(State.LibraryBooks, name)`. |

  The admin panel acts with `by = "Admin panel"`, `isAdmin = true`. A player name never equals that string, so own-book checks pass.

  Private helpers: `CopyPieceLocked(CollegePiece source) → CollegePiece` (new id, same blocks, `UpdatedUtc = Now`, `Pieces.Save`), `DeleteLibraryPieceLocked(LibraryBook book, string pieceId)` (delete only when no other version of the book uses it; uses the existing `DeletePiece`), `NoteLocked(LibraryBook, LibraryAction, string by, string reason)`, `RefreshMarkLocked(string author)` (queues `Notifier.RefreshLibraryMark(author, LibraryMarkOf(author))` to run after save).

- [ ] **Step 6: Run the Verify command,** then the whole College folder. Expected: all pass.

```json:metadata
{"files": ["Chaos/Services/College/CollegeService.Library.cs", "Chaos/Services/College/LibraryText.cs", "Chaos/Services/College/CollegeNotifier.cs", "Tests/Chaos.Tests/College/LibraryServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/LibraryServiceTests/*\"", "acceptanceCriteria": ["All listed LibraryServiceTests pass incl. double decision, withdraw race, missing piece", "4th send refused with TOO_MANY_WAITING", "New approval counts mark; edit approval keeps count and deletes old piece", "Staff take-down lowers count; author take-down keeps it", "Every decision mails and tells the author after save", "Every action adds a history event", "Copy makes new piece id sharing picture hashes; College piece unchanged"], "modelTier": "frontier"}
```

---

### Task 5: CollegePanel Library dispatch, checks and login notices

**Goal:** Route every Library action through `CollegePanel` with room, role, alive, mute, dialog and rate checks, build every Library display, and add the login notices.

**Files:**
- Modify: `Server: Chaos/Services/College/CollegePanel.cs` (make it `partial`; route `Library*` action types)
- Create: `Server: Chaos/Services/College/CollegePanel.Library.cs`
- Modify: `Server: Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` (lines ~2256–2260)
- Test: `Server: Tests/Chaos.Tests/College/LibraryPanelTests.cs`

**Acceptance Criteria:**
- [ ] Shelf page, open (Shelf mode), save, send, edit, copy, review list, approve, reject and dismiss are refused outside the Library room with the orange bar "Go to the Library to do that." (29 characters).
- [ ] Report, Own open, withdraw, take down and submissions work from anywhere; the reader and submissions windows can stay open after leaving the room.
- [ ] Save, send, report and copy are refused for a dead player (`!IsAlive`), a muted player (`Muted`) and a player with an open dialog (`ActiveDialog.Get() is not null`).
- [ ] Reads go through a 120 per minute `CollegeFetchLimit`; writes go through the existing 250 ms `Throttle` and `DraftBursts` (as `SaveDraft` for the piece's subject).
- [ ] On login, the Library Author mark is synced, and a reviewer with items waiting sees `LibraryText.ToReview(n)`.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.College/*/*"` → all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests** in `LibraryPanelTests.cs`. Build the panel the way `CollegeRoomScriptTests.cs:76` does: `new CollegePanel(college, new PictureUploads(pictures, options), pictures, time, NullLogger<CollegePanel>.Instance)`. Make the Aisling and its client mock the way `CollegeRoomScriptTests` does, and capture `SendCollegeDisplay` and `SendOrangeBarMessage` calls. Put the Aisling on a `MapInstance` with `InstanceId` `"college_library"` or `"mileth_college"`. Tests:
  - `A_shelf_page_outside_the_library_is_refused`
  - `A_shelf_page_in_the_library_sends_the_page`
  - `A_dead_player_cannot_send_a_piece`
  - `A_muted_player_cannot_report`
  - `A_player_in_a_dialog_cannot_save`
  - `Saving_in_library_mode_replies_with_a_library_writer_result_carrying_the_book_id`
  - `A_player_who_is_not_staff_gets_no_review_list`
  - `Opening_a_missing_book_says_it_is_gone`
  - `Reads_past_the_limit_are_dropped` (121 shelf pages in one minute → 120 displays)

- [ ] **Step 2: Run the tests.** Expected: FAIL.

- [ ] **Step 3: Implement.** In `CollegePanel.Handle`, before the existing first switch, add:

```csharp
if (args.Type is >= CollegeActionType.LibraryShelfPage and <= CollegeActionType.LibraryDrawingFetch)
{
    HandleLibrary(viewer, args);
    return;
}
```

  In `CollegePanel.Library.cs`:
  - Add the field `private readonly CollegeFetchLimit LibraryReads = new(120);`. If field initialisers can't use the primary constructor, put it next to the other limits in `CollegePanel.cs`.
  - `private static readonly string NOT_IN_LIBRARY = "Go to the Library to do that.";`
  - `private bool InLibrary(Aisling viewer) => college.Options.IsLibrary(viewer.MapInstance.InstanceId);`
  - `private static bool MayWrite(Aisling viewer) => viewer.IsAlive && !viewer.Muted && viewer.ActiveDialog.Get() is null;`
  - `HandleLibrary(Aisling viewer, CollegeActionArgs args)` switches on the type:
    - Reads (`LibraryShelfPage`, `LibraryOpenBook`, `LibrarySubmissions`, `LibraryCollegePieces`, `LibraryReviewList`, `LibraryDrawingFetch`): `LibraryReads.TryTake(viewer.Name, Now)` or return silently.
    - Writes (everything else): `Throttle.TryStart` + `DraftBursts.Allows(viewer.Name, CollegeActionType.SaveDraft, subject, throttled, Now)` for save and send. Other writes use `Throttle.TryStart` alone. When refused, send `CollegeText.TOO_FAST` (a `WriterResult` for save and send, else an orange bar).
    - Room rule: as in the Acceptance Criteria.
    - `MayWrite` rule: for save, send, report and copy. Refusal bar: `"You can't do that right now."`.
  - Map each service result to a display:
    - Shelf: `CollegeDisplayType.LibraryShelf` with rows mapped to `LibraryShelfRowInfo`. `ApprovedUnix` = `(uint)new DateTimeOffset(utc).ToUnixTimeSeconds()`.
    - Book: `LibraryBook` with `Header = $"{ShelfName(shelf)}{(context == LibraryWaiting && view.HasShelfVersion ? " - Edit" : "")}"`, `Author`, `Hash = version.CoverHash`, `Spine`, flags, `Votes = reports.Select(r => new CollegeVoteInfo { Judge = r.By, Tier = CollegeTierCode.None, Comment = r.Reason })`, `Piece = ToInfo(piece)`. When `OpenBook` returns null, send the bar `LibraryText.NO_BOOK`.
    - Save and send: `WriterResult` with `Mode = CollegeWriterMode.Library`, `Result = Ok ? (type == LibrarySend ? Submitted : Saved) : Refused`, `Message`, `Id = BookId`, plus the subject. On send also send the bar.
    - Edit and copy: `OpenWriter` with `Mode = Library`, `Subject` (Art when the shelf is Artwork, else the piece's subject, Literature for new books), `Piece`, `Id`, `Shelf`, `Spine`, `Hash`. A null session sends `LibraryText.STILL_WAITING` or `NOT_YOURS` as a bar.
    - Approve, reject, dismiss, take down, withdraw and report: send the bar `outcome.Message`, then resend the window that asked. Review actions → `SendLibraryReview`. Withdraw and author take-down → `SendLibrarySubmissions`.
    - Drawing fetch: load the shelf piece of an Artwork book and send `LibraryDrawing` with the drawing's palette and pixels. Map them the way the gallery `Drawing` display does; find that code in `CollegePanel` and reuse its mapping.
  - Public openers for scripts: `OpenLibraryShelf(Aisling, LibraryShelf)` (page 0, Newest, empty search), `OpenLibraryWriter(Aisling, LibraryShelf)`, `OpenLibrarySubmissions(Aisling)`, `OpenLibraryCollegePieces(Aisling)`, `OpenLibraryReview(Aisling)`.
  - Add `public static string ShelfName(LibraryShelf shelf)`: Guides → "Guides", Lore → "Lore", ShortStories → "Short stories", PoemsAndSongs → "Poems and songs", Artwork → "Artwork".

- [ ] **Step 4: Login.** In `DefaultAislingScript`, next to `CollegeNotifier.ApplyEducatedMark(...)`:

```csharp
CollegeNotifier.ApplyLibraryMark(Subject, College.LibraryMarkOf(Subject.Name));
if (College.LibraryToReview(Subject.Name, Subject.IsKnight, Subject.IsAdmin) is > 0 and var toReview)
    Subject.SendOrangeBarMessage(LibraryText.ToReview(toReview));
```

- [ ] **Step 5: Run the Verify command.** Expected: all pass.

```json:metadata
{"files": ["Chaos/Services/College/CollegePanel.cs", "Chaos/Services/College/CollegePanel.Library.cs", "Chaos/Scripting/AislingScripts/DefaultAislingScript.cs", "Tests/Chaos.Tests/College/LibraryPanelTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.College/*/*\"", "acceptanceCriteria": ["Room-bound actions refused outside college_library", "Report, own open, withdraw, take down, submissions work anywhere", "Dead, muted or in-dialog players cannot save, send, report or copy", "Reads limited to 120/min; writes throttled", "Login syncs the mark and tells reviewers the count"], "modelTier": "standard"}
```

---

### Task 6: Librarian dialog, bookcase reactor and NPC data

**Goal:** Add the Librarian's menu, the bookcase click script and their JSON data.

**Files:**
- Create: `Server: Chaos/Scripting/DialogScripts/Temuair/College/CollegeLibrarianScript.cs`
- Create: `Server: Chaos/Scripting/ReactorTileScripts/Temauir/College/CollegeBookcaseScript.cs`
- Create: `Unora: Data/Configuration/Templates/Merchants/Temauir/mileth/college/college_librarian.json`
- Create: `Unora: Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_librarian_initial.json`, `college_librarian_write.json`, `college_librarian_write_open.json`, `college_librarian_mine.json`, `college_librarian_bring.json`, `college_librarian_review.json`, `college_librarian_about.json`
- Test: `Server: Tests/Chaos.Tests/College/LibraryScriptTests.cs`

**Acceptance Criteria:**
- [ ] The Librarian's main menu shows "Write a piece", "My submissions", "Bring a College piece", "About the Library", and "Review" only for reviewers.
- [ ] "Write a piece" lists the 5 shelves; picking one closes the dialog and opens the writer on that shelf.
- [ ] Clicking a bookcase with `scriptVars.collegeBookcase.shelf = "Lore"` opens the Lore shelf; a bad shelf name does nothing.
- [ ] `python Tools/College/check_dialogs.py` passes; every option is 35 characters or fewer, and there are no em dashes.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/LibraryScriptTests/*"` → pass; and from `Unora:` `python Tools/College/check_dialogs.py` → no errors.

**Steps:**

- [ ] **Step 1: Bookcase script,** copied from `CollegeGalleryStandScript`:

```csharp
public sealed class CollegeBookcaseScript : ConfigurableReactorTileScriptBase
{
    private readonly CollegePanel Panel;
    public string Shelf { get; init; } = null!;

    public CollegeBookcaseScript(ReactorTile subject, CollegePanel panel)
        : base(subject)
        => Panel = panel;

    public override void OnClicked(Aisling source)
    {
        if (Enum.TryParse<LibraryShelf>(Shelf, true, out var shelf))
            Panel.OpenLibraryShelf(source, shelf);
    }
}
```

- [ ] **Step 2: Librarian script.** Model it on `CollegeRegistrarScript`. Use the constructor `(Dialog subject, CollegeService college, CollegePanel panel)` and switch on `Subject.Template.TemplateKey.ToLowerInvariant()`:
  - `college_librarian_initial`: if `College.CanReviewLibrary(source.Name, source.IsKnight, source.IsAdmin)`, insert the option `"Review"` → `college_librarian_review` before the last option.
  - `college_librarian_write`: clear the options; add one option per shelf using `CollegePanel.ShelfName`, each going to `college_librarian_write_open`. In `OnNext`, parse the chosen option text back to a `LibraryShelf` into `Subject.Context`.
  - `college_librarian_write_open`: `Panel.OpenLibraryWriter(source, shelf)`, then `Subject.Close(source)`.
  - `college_librarian_mine`: `Panel.OpenLibrarySubmissions(source)`, then close.
  - `college_librarian_bring`: `Panel.OpenLibraryCollegePieces(source)`, then close.
  - `college_librarian_review`: when a reviewer, `Panel.OpenLibraryReview(source)`, then close.

- [ ] **Step 3: Data.** Copy the shape of `college_dean.json` and `college_registrar_*.json` (`"type": "DialogMenu"`, `"scriptKeys": ["collegeLibrarian"]`):
  - Merchant `college_librarian.json`: name "Librarian", a sprite of a scholar NPC (reuse the College registrar's sprite id), `"scriptKeys": ["showdialog"]`, `scriptVars.showdialog.dialogKey = "college_librarian_initial"`.
  - `college_librarian_initial`: text "Welcome to the Library. Every book here was written by a player." Options: "Write a piece" → `college_librarian_write`, "My submissions" → `college_librarian_mine`, "Bring a College piece" → `college_librarian_bring`, "About the Library" → `college_librarian_about`.
  - `college_librarian_write`: `"contextual": true`, text "Which shelf is your piece for?", options `[]`.
  - `college_librarian_about`: `"type": "Normal"`, text "Write at my desk. A Director or staff member reads each piece. Approved books go on the shelves. You may have 3 waiting at once."
  - The other keys have short text ("Opening your submissions.") because the script closes them at once.

- [ ] **Step 4: Tests** (`LibraryScriptTests.cs`). Use the reactor test style from existing `CollegeGalleryStandScript` tests if there are any; otherwise call `OnClicked` on a script built with a mocked `ReactorTile` and a real panel, and assert the captured `LibraryShelf` display. Tests:
  - `Clicking_a_bookcase_opens_its_shelf`
  - `A_bookcase_with_an_unknown_shelf_does_nothing`

  Test the Librarian's review option with the dialog test pattern used by `CollegeRegistrarScript` tests, if one exists. If none exists, skip the dialog test and note that in the report.

- [ ] **Step 5: Run both Verify commands.**

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/College/CollegeLibrarianScript.cs", "Chaos/Scripting/ReactorTileScripts/Temauir/College/CollegeBookcaseScript.cs", "Unora/Data/Configuration/Templates/Merchants/Temauir/mileth/college/college_librarian.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_librarian_*.json", "Tests/Chaos.Tests/College/LibraryScriptTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/LibraryScriptTests/*\" && python Tools/College/check_dialogs.py", "acceptanceCriteria": ["Main menu has the 4 options plus Review for reviewers", "Write a piece lists 5 shelves and opens the writer", "Bookcase opens its shelf; bad shelf does nothing", "check_dialogs passes; options <= 35 chars; no em dashes"], "modelTier": "standard"}
```

---

### Task 7: Library room, entrance and hall sign (map builder)

**Goal:** Build map 787 `college_library` and the hall entrance with `college_maps.py`, and generate the map files.

**Files:**
- Modify: `Unora: Tools/College/college_maps.py`
- Modify: `Unora: Tools/College/test_college_maps.py`
- Generated: `Unora: Data/Configuration/MapData/lod787.map`, `Data/Configuration/Templates/Maps/787.json`, `Data/Configuration/MapInstances/Temuair/Towns/Mileth/College/college_library/{instance,merchants,reactors,monsters}.json`, and the hall's `mileth_college/reactors.json`

**Acceptance Criteria:**
- [ ] Map 787 is still free. Before building, run `ls Data/Configuration/MapData/lod787.map` and `git -C ..\..\Unora log --oneline -5 -- Data/Configuration/MapData`. If 787 is taken, use the next free number everywhere in this task and say so in the report.
- [ ] Hall tiles (44,10)–(44,15) are `warpWithChecksReactorTile` warps to the Library arrival (9,18); the 4 Library exits (8–11,19) warp to the hall at (43,12).
- [ ] A `clickablesign` reading "Library" sits on a wall tile next to the entrance, at (44,7) if it is a wall tile; otherwise the nearest wall tile.
- [ ] The Library has 5 bookcases with the hall's shelf art and `collegeBookcase` reactors: Guides, Lore, Short stories on the north wall; Poems and songs on the west wall; Artwork on the east wall.
- [ ] Each bookcase has a `clickablesign` floor label on the walkable tile in front of it, reading the shelf name. The label blocks no pathing and is the "floor label" pattern for this room.
- [ ] The Librarian merchant stands at (9,10) facing south (the door).
- [ ] `python Tools/College/college_maps.py --check` passes, and `python -m unittest Tools.College.test_college_maps` passes, including new tests.

**Verify:** from `Unora:` `python Tools/College/college_maps.py --check && python -m unittest Tools.College.test_college_maps` → "OK".

**Steps:**

- [ ] **Step 1: Read the builder.** Use `get_symbols_overview` on `college_maps.py`. Read `ROOMS`, `build_room`, `room_instance`, `room_entities`, `hall_entities`, `sign`, `warp`, `generated_instance_ids`, `grids_by_instance`, `build_all`, the write step and `self_check`. Constants: `ROOM_EXITS=[(8,19),(9,19),(10,19),(11,19)]`, `ROOM_ARRIVAL=(9,18)`, `MUSIC_COLLEGE=16`. Bookcase art comes from `SHELF_ROW` in `Tools/TownHall/town_hall.py:171`: north wall (1,1) 10774/10000, (2,1) 10775/10000, (3,1) 10776/10777. The west-wall bookcase is probably (1,2) 10786/10788 and (1,3) 10786/10787. Confirm that by rendering or reading the base map before using it. For the east wall, mirror the west-wall art. If no mirrored art exists, use the north-wall art on an east-wall tile and report it.

- [ ] **Step 2: Write the failing tests** in `test_college_maps.py`:
  - `test_hall_east_wall_warps_lead_to_the_library`: each of (44,10)–(44,15) in the hall's reactors is a warp to map 787 at (9,18).
  - `test_library_exits_return_to_the_hall`: each `ROOM_EXITS` tile in `college_library` warps to 762 at (43,12).
  - `test_library_has_five_bookcases_one_per_shelf`: the `collegeBookcase` reactors' shelf names are exactly `{"Guides","Lore","ShortStories","PoemsAndSongs","Artwork"}`.
  - `test_every_bookcase_has_a_floor_label`: for each bookcase, a `clickablesign` sits on a walkable tile next to it, with the shelf's display text.
  - `test_the_librarian_faces_the_door`: the merchant `college_librarian` is at (9,10), facing south.

  Update `test_hall_changes_only_on_the_board_and_stands` so it also allows the 6 entrance tiles and the sign tile. The hall art itself does not change: the warps are reactors.

- [ ] **Step 3: Run the tests.** Expected: FAIL.

- [ ] **Step 4: Implement.** Add constants:

```python
LIBRARY_MAP = 787
LIBRARY_INSTANCE = "college_library"
LIBRARY_ENTRANCE = [(44, y) for y in range(10, 16)]
LIBRARY_HALL_ARRIVAL = (43, 12)
LIBRARY_LIBRARIAN = (9, 10)
```

  Add `build_library(root)`: start from `chamber_763_base.map` like `build_room`, but leave out the benches and lectern. Find how `build_room` adds them and skip that step. Then place the 5 bookcases.
  Add `library_instance()` with music `MUSIC_COLLEGE` and no scriptKeys; the Library is not a class room.
  Add `library_entities()`: exit warps to the hall, the bookcases (`"shouldBlockPathfinding": true`, `scriptVars.collegeBookcase.shelf`), the floor labels via `sign(at, text)`, and `merchant("college_librarian", LIBRARY_LIBRARIAN, "south")`. Use the facing value the existing merchants use.
  In `hall_entities()`, add the 6 entrance warps and the "Library" sign.
  Add the instance to `generated_instance_ids()`, `grids_by_instance()` and `build_all()`, and make sure the write step writes it.

- [ ] **Step 5: Generate.** Run `python Tools/College/college_maps.py`, the write mode used for map 786. Check that `lod787.map`, `Templates/Maps/787.json` and the instance folder exist. Compare the template JSON with `786.json` for the fields the server needs (`templateKey`, width, height).

- [ ] **Step 6: Run the Verify command.**

```json:metadata
{"files": ["Unora/Tools/College/college_maps.py", "Unora/Tools/College/test_college_maps.py", "Unora/Data/Configuration/MapData/lod787.map", "Unora/Data/Configuration/Templates/Maps/787.json", "Unora/Data/Configuration/MapInstances/Temuair/Towns/Mileth/College/college_library/", "Unora/Data/Configuration/MapInstances/Temuair/Towns/Mileth/College/mileth_college/reactors.json"], "verifyCommand": "python Tools/College/college_maps.py --check && python -m unittest Tools.College.test_college_maps", "acceptanceCriteria": ["787 confirmed free or next free used and reported", "Hall (44,10)-(44,15) warp to Library (9,18); exits return to (43,12)", "Library sign beside the entrance", "5 bookcases with collegeBookcase reactors on the right walls", "Each bookcase has a floor label sign", "Librarian at (9,10) facing the door", "--check and unittest pass"], "modelTier": "standard"}
```

---

### Task 8: Library pipe commands for the admin page

**Goal:** Let the file server list, read and decide Library items over the control pipe.

**Files:**
- Modify: `Server: Chaos/Services/Control/ControlCommandHandler.cs`
- Test: `Server: Tests/Chaos.Tests/Control/LibraryControlCommandTests.cs`

**Acceptance Criteria:**
- [ ] `library-list` returns `{ok:true, waiting:[...], reported:[...]}` with `id, title, author, shelf, isEdit, utc` (waiting) and `id, title, author, shelf, reports:[{by,reason,utc}]` (reported).
- [ ] `library-book` with `{id, version:"waiting"|"shelf"}` returns the title, author, shelf, spine, cover hash, blocks and history. Blocks are `{kind, text, hash, palette, pixels}`, with pixels base64.
- [ ] `library-approve`, `library-reject`, `library-takedown` and `library-dismiss` act as `"Admin panel"`; reject and takedown without a reason return `{ok:false,error:"Please give a reason."}`.
- [ ] A refused decision returns `{ok:false, error:<LibraryText message>}`.
- [ ] Without a College service, every `library-*` command returns `{ok:false,error:"the library is not available"}`.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/LibraryControlCommandTests/*"` → all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests,** following `PortraitControlCommandTests.cs` and `ModerationControlCommandTests.cs`. Build a real `CollegeService` with the rig from Task 4's tests, then call `handler.HandleAsync(json, CancellationToken.None)` and parse the reply with `JsonNode.Parse`. Tests:
  - `Listing_shows_waiting_and_reported_books`
  - `Reading_a_waiting_book_returns_its_blocks_and_history`
  - `Approving_from_the_panel_shelves_the_book`
  - `Rejecting_without_a_reason_is_refused`
  - `A_second_approve_is_refused_with_the_message`
  - `Without_the_college_the_library_is_not_available`

- [ ] **Step 2: Implement.** Add `CollegeService? college = null` as the last primary-constructor parameter. In the dispatch, add the prefix guard `if (command.StartsWith("library-") && college is null) return Error("the library is not available");` and the cases. Use `PanelAdmin` as the actor name and `isAdmin: true`. Use `Text(body, "reason")` and `Id(body)`. Serialize with the existing `Serialize`. Shelf names in replies come from `CollegePanel.ShelfName`. Timestamps are ISO 8601 UTC strings.
  Check how the handler is registered in DI (`AddServerControl`). Optional constructor parameters resolve when the service is registered, so make sure `CollegeService` is resolvable there; it is registered by `AddCollege`.

- [ ] **Step 3: Run the Verify command.**

```json:metadata
{"files": ["Chaos/Services/Control/ControlCommandHandler.cs", "Tests/Chaos.Tests/Control/LibraryControlCommandTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/LibraryControlCommandTests/*\"", "acceptanceCriteria": ["library-list returns waiting and reported lists", "library-book returns blocks and history", "Decisions act as Admin panel; reject/takedown need a reason", "Refusals return the LibraryText message", "Without College every library-* returns not available"], "modelTier": "standard"}
```

---

### Task 9: CarouselLayout, CoverChoice and Library labels (client, pure)

**Goal:** Add the graphics-free carousel maths, the cover fallback choice and the Library label tables, with tests.

**Files:**
- Create: `Client: Chaos.Client/ViewModel/College/Library/CarouselLayout.cs`
- Create: `Client: Chaos.Client/ViewModel/College/Library/CoverChoice.cs`
- Create: `Client: Chaos.Client/ViewModel/College/Library/LibraryLabels.cs`
- Test: `Client: Tests/Chaos.Client.Tests/College/Library/CarouselLayoutTests.cs`, `CoverChoiceTests.cs`

**Acceptance Criteria:**
- [ ] `CarouselLayout.Place` puts the front book at the centre with scale 1, places books symmetrically left and right, and hides books more than 4 places from the front.
- [ ] `Spin` clamps the target to `0..Count-1`; `Step` moves the current position toward the target and never overshoots.
- [ ] `ShouldLoadCover(i)` is true only within 2 places of the rounded front.
- [ ] `DrawOrder` lists the visible books farthest first, so the front book draws last.
- [ ] `CoverChoice.Of` returns Picture for a cover hash, Drawing for an Artwork book with `DrawingCover`, else Plain.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/CarouselLayoutTests/*"` and the same for `CoverChoiceTests` → all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests.**

```csharp
using Chaos.Client.ViewModel.College.Library;
using FluentAssertions;

namespace Chaos.Client.Tests.College.Library;

public class CarouselLayoutTests
{
    [Test]
    public void The_front_book_sits_in_the_middle_at_full_size()
    {
        var layout = new CarouselLayout(count: 10, centerX: 250, radius: 200);
        layout.JumpTo(3);
        var place = layout.Place(3);
        place.Visible.Should().BeTrue();
        place.X.Should().BeApproximately(250, 0.01f);
        place.Scale.Should().BeApproximately(1f, 0.001f);
    }

    [Test]
    public void Books_either_side_are_placed_symmetrically()
    {
        var layout = new CarouselLayout(10, 250, 200);
        layout.JumpTo(5);
        (250 - layout.Place(4).X).Should().BeApproximately(layout.Place(6).X - 250, 0.01f);
        layout.Place(4).Scale.Should().BeLessThan(1f);
    }

    [Test]
    public void Books_more_than_four_places_away_are_hidden()
    {
        var layout = new CarouselLayout(12, 250, 200);
        layout.JumpTo(6);
        layout.Place(1).Visible.Should().BeFalse();
        layout.Place(2).Visible.Should().BeTrue();
    }

    [Test]
    public void Spinning_past_either_end_stops_at_the_end()
    {
        var layout = new CarouselLayout(5, 250, 200);
        layout.Spin(-3);
        layout.Target.Should().Be(0);
        layout.Spin(99);
        layout.Target.Should().Be(4);
    }

    [Test]
    public void Stepping_moves_toward_the_target_without_overshooting()
    {
        var layout = new CarouselLayout(10, 250, 200);
        layout.Spin(3);
        layout.Step(0.1f);
        layout.Current.Should().BeGreaterThan(0).And.BeLessThan(3);
        layout.Step(10f);
        layout.Current.Should().Be(3);
    }

    [Test]
    public void Covers_load_only_within_two_places_of_the_front()
    {
        var layout = new CarouselLayout(10, 250, 200);
        layout.JumpTo(5);
        layout.ShouldLoadCover(3).Should().BeTrue();
        layout.ShouldLoadCover(7).Should().BeTrue();
        layout.ShouldLoadCover(2).Should().BeFalse();
        layout.ShouldLoadCover(8).Should().BeFalse();
    }

    [Test]
    public void The_front_book_draws_last()
    {
        var layout = new CarouselLayout(10, 250, 200);
        layout.JumpTo(5);
        layout.DrawOrder().Last().Should().Be(5);
        layout.DrawOrder().First().Should().BeOneOf(1, 9);
    }

    [Test]
    public void An_empty_shelf_has_nothing_to_draw()
    {
        var layout = new CarouselLayout(0, 250, 200);
        layout.DrawOrder().Should().BeEmpty();
        layout.Front.Should().Be(-1);
    }
}

public class CoverChoiceTests
{
    [Test]
    public void A_cover_hash_wins()
        => CoverChoice.Of(new string('a', 64), drawingCover: true).Should().Be(CoverKind.Picture);

    [Test]
    public void Artwork_without_a_cover_uses_its_drawing()
        => CoverChoice.Of(string.Empty, drawingCover: true).Should().Be(CoverKind.Drawing);

    [Test]
    public void A_book_with_neither_gets_a_plain_cover()
        => CoverChoice.Of(string.Empty, drawingCover: false).Should().Be(CoverKind.Plain);
}
```

- [ ] **Step 2: Run the tests.** Expected: FAIL, the types do not exist.

- [ ] **Step 3: Implement.**

```csharp
namespace Chaos.Client.ViewModel.College.Library;

public readonly record struct CarouselPlace(bool Visible, float X, float Scale, float Lift);

public sealed class CarouselLayout(int count, float centerX, float radius)
{
    public const int VISIBLE_EACH_SIDE = 4;
    public const int COVER_RANGE = 2;
    private const float STEP_ANGLE = 0.33f;
    private const float SPIN_SPEED = 8f;

    public int Count { get; } = count;
    public float Current { get; private set; }
    public int Target { get; private set; }
    public int Front => Count == 0 ? -1 : Math.Clamp((int)MathF.Round(Current), 0, Count - 1);

    public void JumpTo(int index)
    {
        Target = Math.Clamp(index, 0, Math.Max(0, Count - 1));
        Current = Target;
    }

    public void Spin(int by) => Target = Math.Clamp(Target + by, 0, Math.Max(0, Count - 1));

    public void BringToFront(int index) => Target = Math.Clamp(index, 0, Math.Max(0, Count - 1));

    public void Step(float seconds)
    {
        var gap = Target - Current;
        var move = SPIN_SPEED * seconds * MathF.Max(1f, MathF.Abs(gap));
        Current = MathF.Abs(gap) <= move ? Target : Current + (MathF.Sign(gap) * move);
    }

    public CarouselPlace Place(int index)
    {
        var offset = index - Current;
        if (MathF.Abs(offset) > VISIBLE_EACH_SIDE + 0.5f)
            return new CarouselPlace(false, 0, 0, 0);
        var angle = offset * STEP_ANGLE;
        var x = centerX + (MathF.Sin(angle) * radius);
        var scale = MathF.Max(0.4f, 1f - (0.12f * MathF.Abs(offset)));
        var lift = (1f - MathF.Cos(angle)) * radius * 0.15f;
        return new CarouselPlace(true, x, scale, lift);
    }

    public bool ShouldLoadCover(int index) => (Front >= 0) && (Math.Abs(index - Front) <= COVER_RANGE);

    public IReadOnlyList<int> DrawOrder()
        => Enumerable.Range(0, Count)
                     .Where(i => Place(i).Visible)
                     .OrderByDescending(i => MathF.Abs(i - Current))
                     .ThenBy(i => i)
                     .ToList();
}
```

```csharp
namespace Chaos.Client.ViewModel.College.Library;

public enum CoverKind { Picture, Drawing, Plain }

public static class CoverChoice
{
    public static CoverKind Of(string coverHash, bool drawingCover)
        => coverHash.Length > 0 ? CoverKind.Picture : drawingCover ? CoverKind.Drawing : CoverKind.Plain;
}
```

  `LibraryLabels` (static): `ShelfName(LibraryShelfCode)` with the same names as the server's `ShelfName`, `StatusName(LibraryStatusCode)` ("Draft", "Waiting for review", "On the shelf", "Edit waiting", "Rejected", "Taken down"), and `Spines`, an 8-entry `Color[]`: oxblood (110,30,30), forest (30,80,40), navy (30,40,90), ochre (170,120,40), plum (90,40,90), teal (30,100,100), slate (80,85,95), umber (100,70,40).

- [ ] **Step 4: Run the Verify command.**

```json:metadata
{"files": ["Chaos.Client/ViewModel/College/Library/CarouselLayout.cs", "Chaos.Client/ViewModel/College/Library/CoverChoice.cs", "Chaos.Client/ViewModel/College/Library/LibraryLabels.cs", "Tests/Chaos.Client.Tests/College/Library/CarouselLayoutTests.cs", "Tests/Chaos.Client.Tests/College/Library/CoverChoiceTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/CarouselLayoutTests/*\"", "acceptanceCriteria": ["Front book centred at scale 1; symmetric sides; hidden beyond 4", "Spin clamps; Step never overshoots", "ShouldLoadCover only within 2", "DrawOrder ends with the front", "CoverChoice picks Picture, Drawing, Plain"], "modelTier": "mechanical"}
```

---

### Task 10: LibraryCarouselControl

**Goal:** Build the browsing window: shelf tabs, the curved row of spines, the turning front cover, search, sort and pages.

**Files:**
- Create: `Client: Chaos.Client/Controls/World/Popups/College/Library/LibraryCarouselControl.cs`
- Create: `Client: Chaos.Client/Controls/World/Popups/College/Library/BookSpine.cs` (static draw helper)
- Create: `Client: Chaos.Client/Controls/World/Popups/College/Library/BookCover.cs` (static draw helper)

**Acceptance Criteria:**
- [ ] The window is a `GuildCloakDialogBase` with `"_nsett"`, 560x440, centred, Escape closes it, and it has a close button like the other College windows.
- [ ] The header reads `"<Shelf name> - <Total> books"`. Five tabs switch shelves; each tab click raises `LibraryShelfPage` for page 0 with the current sort and search.
- [ ] Spines draw farthest first using `CarouselLayout`, filled with the spine colour, with the title drawn in small text. A click on a spine brings it to the front. The Left and Right keys, the wheel, and the `<` and `>` buttons spin the row. Arrow keys are ignored while the search box has focus.
- [ ] The front book's cover draws above the row, with a short turn animation (horizontal scale from 0 to 1 over 0.2 s) when the front changes. Under it: title, "by <author>", the approval date (`yyyy-MM-dd`), and an Open button. Double-clicking the front cover opens it.
- [ ] Covers come from `CollegePictureTransfers.TryGetTexture` (Picture), a Library drawing cache (Drawing), or a plain spine-coloured cover with title and author (Plain). Only books where `ShouldLoadCover` is true ask for a cover. Held textures are released when the page changes or the window hides.
- [ ] The search box (`CustomTextBox`, `MaxLength = LIBRARY_MAX_SEARCH_CHARS`, hint "Search title or author") sends on Enter. The sort button toggles "Newest" and "A-Z". Page buttons and "Page x of y" appear under the row.
- [ ] The empty shelf shows "No books on this shelf yet."
- [ ] The client builds with no new warnings.

**Verify:** `dotnet build Chaos.Client.slnx` → 0 errors and no new warnings in the new files; then all client tests pass with `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build`.

**Steps:**

- [ ] **Step 1: Read the patterns.** Use `CollegeGalleryControl` (tabs, `Update` fetching thumbnails, `GalleryArtCell.Draw`), `PieceView.AddPicture` (Hold, Release, `PictureReady`), `HangmanTurnOrderControl.OnMouseScroll`, `LockpickControl.OnKeyDown` (Left and Right) and `ArtCanvasControl.OnKeyDown` (the guard while a text box is focused).

- [ ] **Step 2: Build the control.** Public API:

```csharp
public sealed class LibraryCarouselControl : GuildCloakDialogBase
{
    public LibraryCarouselControl(CollegePictureTransfers transfers, LibraryDrawings drawings);
    public event Action<CollegeActionArgs>? ActionRequested;
    public void Open(CollegeDisplayArgs args);   // LibraryShelf display
    public override void Hide();                 // releases held covers
}
```

  `LibraryDrawings` is a new small class in `Chaos.Client/Systems/College/LibraryDrawings.cs`. It is a copy of `CollegeDrawings` (keyed by book id) with `KEPT_DRAWINGS = 24`; reuse `CollegeDrawings` itself if its API fits unchanged, with a second instance. It answers `LibraryDrawing` displays.
  - Spine area: a 500x150 band under the tabs. Each spine is 28 px wide x 120 px tall at scale 1, scaled by `Place.Scale`. Its bottom sits on a curved "shelf" line `y = bandBottom - Lift`, with a darker 2 px edge. Draw the title down the spine: `TextRenderer.BuildSmallText` cached per book, or the first 12 characters horizontally at `scale = 1` when the spine is too thin.
  - Front cover: 120x170 at the top centre (half the 240x340 cover size), drawn with `DrawTextureFitted` and `Color.White`. While turning, draw at `width * turnProgress`, centred.
  - Hit-testing spines: in `OnClick`, test the visible spines in reverse draw order and call `Layout.BringToFront(i)`. Double-click on the cover raises `LibraryOpenBook` with `OpenMode = Shelf`.
  - `Update(GameTime)`: `Layout.Step(dt)`, advance the turn, and for each index where `ShouldLoadCover` is true and the cover kind is Picture or Drawing, request the texture. For Drawing, raise `LibraryDrawingFetch` once per id, gated with `FetchRetry` the way `CollegeDrawings.NeedsFetch` does.

- [ ] **Step 3: Build.** Run `dotnet build Chaos.Client.slnx` and fix all errors. Run the client tests.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/Library/LibraryCarouselControl.cs", "Chaos.Client/Controls/World/Popups/College/Library/BookSpine.cs", "Chaos.Client/Controls/World/Popups/College/Library/BookCover.cs", "Chaos.Client/Systems/College/LibraryDrawings.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["560x440 _nsett window, Escape closes", "Header and 5 shelf tabs request page 0", "Spines draw back to front; click, keys, wheel and buttons spin", "Front cover turns and shows title, author, date, Open; double-click opens", "Covers only requested within 2 of front; held textures released", "Search on Enter, sort toggle, pager", "Empty shelf text", "Builds with no new warnings"], "modelTier": "frontier"}
```

---

### Task 11: Submissions, review and College-piece list windows

**Goal:** Build `LibrarySubmissionsControl`, `LibraryReviewControl` and `LibraryCollegePiecesControl` on `CollegeListWindow`.

**Files:**
- Create: `Client: Chaos.Client/Controls/World/Popups/College/Library/LibrarySubmissionsControl.cs`
- Create: `Client: Chaos.Client/Controls/World/Popups/College/Library/LibraryReviewControl.cs`
- Create: `Client: Chaos.Client/Controls/World/Popups/College/Library/LibraryCollegePiecesControl.cs`
- Create: `Client: Chaos.Client/ViewModel/College/Library/SubmissionActions.cs`
- Test: `Client: Tests/Chaos.Client.Tests/College/Library/SubmissionActionsTests.cs`

**Acceptance Criteria:**
- [ ] Submissions has the columns Title (0), Shelf (200) and Status (300). The status of a Rejected or Taken down row also shows its reason in a caption under the list when selected (with "by <name>" for Taken down).
- [ ] Submissions buttons: "Open", "Edit", "Send", "Take back" and "Take down", enabled per row by `SubmissionActions.For(status)`. "Take down" needs a second click ("Confirm"). A "New piece" button is not needed; the Librarian does that.
- [ ] `SubmissionActions.For` returns: Draft → Edit, Send; Waiting → Take back; OnShelf → Edit, Take down; EditWaiting → Take back, Take down; Rejected → Edit, Send; TakenDown → Edit. Open is always allowed.
- [ ] Review has "Waiting (n)" and "Reports (n)" tabs (client-side, like `CollegeJudgingControl`). Columns: Title, Author, Shelf, and "Edit" or "n reports". The Open button raises `LibraryOpenBook` with `ReviewWaiting` or `ReviewReported`.
- [ ] College pieces lists Title and Subject, with a "Copy to Library" button raising `LibraryCopyCollege`.
- [ ] Sending from the list (no piece) sends the stored draft; the server accepts `Piece == null` for `LibrarySend` (add it in `CollegePanel.Library.cs` with a test if Task 5 didn't).
- [ ] Unit tests for `SubmissionActions` pass.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/SubmissionActionsTests/*"` → pass.

**Steps:**

- [ ] **Step 1: Test first.**

```csharp
using Chaos.Client.ViewModel.College.Library;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College.Library;

public class SubmissionActionsTests
{
    [Test]
    [Arguments(LibraryStatusCode.Draft, true, true, false, false)]
    [Arguments(LibraryStatusCode.Waiting, false, false, true, false)]
    [Arguments(LibraryStatusCode.OnShelf, true, false, false, true)]
    [Arguments(LibraryStatusCode.EditWaiting, false, false, true, true)]
    [Arguments(LibraryStatusCode.Rejected, true, true, false, false)]
    [Arguments(LibraryStatusCode.TakenDown, true, false, false, false)]
    public void Each_status_offers_its_actions(LibraryStatusCode status, bool edit, bool send, bool takeBack, bool takeDown)
        => SubmissionActions.For(status).Should().Be(new SubmissionActions(edit, send, takeBack, takeDown));
}
```

  Implement `public readonly record struct SubmissionActions(bool Edit, bool Send, bool TakeBack, bool TakeDown)` with a static `For` switch.

- [ ] **Step 2: Build the windows,** following `CollegeHandInsControl` (constructor, `Open(args)`, `ColumnsOf`, `IdOf`, `OpenAction`) and `CollegeJudgingControl` (tabs). Actions raised:
  - Submissions: Open → `LibraryOpenBook{Id, OpenMode=Own}`; Edit → `LibraryEdit{Id}`; Send → `LibrarySend{Id}` with no piece (see the note below); Take back → `LibraryWithdraw{Id}`; Take down → `LibraryTakeDown{Id, Text=""}`.
  - Review: Open as above.
  - College pieces: `LibraryCopyCollege{CopySource, Id}`. Use `Id` as the row id; source plus id can collide, so give each row a local index id and keep a map to `(Source, Id)`.

  **Note on Send from the list.** `LibrarySend` normally carries the piece. From the submissions list the client has no piece. The server's `LibrarySend` handler must accept `Piece == null` and then send the stored draft without saving. Check that Task 5 did this; if not, add it to `CollegePanel.Library.cs`, and add a `LibraryPanelTests` case named `Sending_from_the_list_sends_the_stored_draft`.

- [ ] **Step 3: Run the Verify command.**

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/Library/LibrarySubmissionsControl.cs", "Chaos.Client/Controls/World/Popups/College/Library/LibraryReviewControl.cs", "Chaos.Client/Controls/World/Popups/College/Library/LibraryCollegePiecesControl.cs", "Chaos.Client/ViewModel/College/Library/SubmissionActions.cs", "Tests/Chaos.Client.Tests/College/Library/SubmissionActionsTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/SubmissionActionsTests/*\"", "acceptanceCriteria": ["Submissions columns and reason caption", "Buttons enabled per SubmissionActions; Take down confirms", "SubmissionActions table as listed", "Review tabs with counts; Open uses ReviewWaiting/ReviewReported", "College pieces list copies", "Tests pass"], "modelTier": "standard"}
```

---

### Task 12: Library footers in the reader

**Goal:** Show Library books in `CollegeReaderControl` with Report, Take down, Approve, Reject, Dismiss and version-switch footers.

**Files:**
- Modify: `Client: Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs`
- Create: `Client: Chaos.Client/ViewModel/College/Library/ReasonRule.cs`
- Test: `Client: Tests/Chaos.Client.Tests/College/Library/ReasonRuleTests.cs`

**Acceptance Criteria:**
- [ ] `Reader.Open` accepts a `LibraryBook` display. The title is the piece title, the header is `args.Header`, and the footer line reads "by <author>".
- [ ] LibraryShelf context: a reason box (hint "Why report this book?") with "Report", enabled when `CanReport` and the reason is 1–300 characters after trimming. "Take down" appears when `CanTakeDown`, needs a second click, and needs a reason unless the viewer is the author.
- [ ] LibraryWaiting context: "Approve", "Reject" (needs a reason), and "Shelf version" / "New version" when `HasShelfVersion` (raises `LibraryOpenBook` with `ReviewShelf` or `ReviewWaiting`). Approve and Reject hide the reader.
- [ ] LibraryReported context: "Take down" (needs a reason, two clicks), "Dismiss", and "Reports (n)", which shows every reason in the votes popup as `"<by>: <reason>"` lines.
- [ ] LibraryOwn context: no footer buttons, except "Take down" when `CanTakeDown`.
- [ ] `ReasonRule.Allows(text, required)` returns true for 1–300 trimmed characters, and also for empty text when not required.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/ReasonRuleTests/*"` → pass.

**Steps:**

- [ ] **Step 1: Test first.**

```csharp
public class ReasonRuleTests
{
    [Test]
    [Arguments("", true, false)]
    [Arguments("   ", true, false)]
    [Arguments("", false, true)]
    [Arguments("Copied from another game", true, true)]
    public void Reasons_are_checked(string text, bool required, bool allowed)
        => ReasonRule.Allows(text, required).Should().Be(allowed);

    [Test]
    public void A_reason_over_three_hundred_characters_is_refused()
        => ReasonRule.Allows(new string('a', 301), true).Should().BeFalse();
}
```

  ```csharp
  public static class ReasonRule
  {
      public static bool Allows(string text, bool required)
      {
          var length = text.Trim().Length;
          return (length <= CollegeProtocol.MAX_REASON_CHARS) && (!required || length > 0);
      }
  }
  ```

- [ ] **Step 2: Footers.** In `BuildFooter`, add the four `case CollegePieceContext.Library*` branches using `FooterAction`. Reuse `CommentBox` as the reason box: set its `HintText`, set `MaxLength = MAX_REASON_CHARS`, and show it. Enable buttons in `Update` with `ReasonRule`, the same way `GuildCloakReviewControl.Update` does. Keep a `bool LibraryAuthor` from the display (author = `!CanReview && CanTakeDown`) so the author's take-down needs no reason. `Send` for Library types raises `CollegeActionArgs { Type, Id = Current.Id, Text = CommentBox.Text.Trim() }`. Two-click confirm uses the existing `RemoveArmed` pattern, with captions "Take down" and "Confirm take down".
  `Step` (Prev/Next) is not used for Library books. The reader shows the piece only; the cover is seen in the carousel.

- [ ] **Step 3: Run the Verify command,** then all client tests.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs", "Chaos.Client/ViewModel/College/Library/ReasonRule.cs", "Tests/Chaos.Client.Tests/College/Library/ReasonRuleTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/ReasonRuleTests/*\"", "acceptanceCriteria": ["Reader opens LibraryBook displays with by-author line", "Shelf footer: Report with reason; Take down with confirm", "Waiting footer: Approve, Reject with reason, version switch", "Reported footer: Take down, Dismiss, Reports popup", "Own footer: only Take down", "ReasonRule tests pass"], "modelTier": "standard"}
```

---

### Task 13: Library mode in the writer and the art canvas

**Goal:** Let the writer and the art canvas edit a Library book: shelf picker, spine colour, cover, Save and "Send for review".

**Files:**
- Create: `Client: Chaos.Client/Controls/World/Popups/College/Library/LibraryBookOptions.cs`
- Modify: `Client: Chaos.Client/Controls/World/Popups/College/CollegeWriterControl.cs`
- Modify: `Client: Chaos.Client/Controls/World/Popups/College/ArtCanvasControl.cs`
- Modify: `Client: Chaos.Client/Systems/College/PicturePrep.cs`
- Test: `Client: Tests/Chaos.Client.Tests/College/Library/CoverPrepTests.cs`

**Acceptance Criteria:**
- [ ] With `Mode == Library`, the writer's title caption reads "Library: <shelf name>". The options row shows 4 writing-shelf buttons, 8 spine swatches and a "Cover" button, and the Submit button reads "Send for review". Draft and HandIn modes look and behave exactly as before.
- [ ] The art canvas in Library mode shows the same row with the shelf fixed to Artwork (no shelf buttons), plus Save and "Send for review".
- [ ] "Cover" opens a two-button choice, "Upload" and "Draw". Upload uses `PictureFilePicker` and `PicturePrep.PrepareCover`. Draw opens the picture canvas, whose Insert result becomes the cover. While a cover uploads, Send is disabled with the status "Uploading cover...". A cover can be cleared with "No cover".
- [ ] `PicturePrep.PrepareCover` scales an image to fit within 240x340, keeping its shape, and refuses files over 20 MB like `Prepare`.
- [ ] Save sends `LibrarySaveDraft` and Send sends `LibrarySend`, both with `Id` (0 for a new book), `Shelf`, `Spine`, `Hash` (cover or empty), `Subject` and `Piece`. The writer stores the book id from a Library `WriterResult`, so later saves update the same book.
- [ ] Closing a dirty Library draft saves it, like a College draft.
- [ ] `CoverPrepTests` pass.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/CoverPrepTests/*"` → pass; then all client tests pass.

**Steps:**

- [ ] **Step 1: Test first.** In `CoverPrepTests`, build PNGs with SkiaSharp the way `DrawingPicturesTests` does:
  - `A_large_portrait_image_is_scaled_to_fit_the_cover` (480x680 → 240x340)
  - `A_wide_image_is_scaled_by_its_width` (800x200 → 240x60)
  - `A_small_image_is_kept_at_its_size` (100x150 → 100x150)
  - `A_file_that_is_not_a_picture_is_refused` (returns null, error not empty)

- [ ] **Step 2: Implement `PrepareCover(byte[] file, out string error)`.** Factor the existing resize in `Prepare` into a private helper that takes the maximum width and height, then call it with `(MAX_COVER_WIDTH, MAX_COVER_HEIGHT)`. `Prepare` keeps its own limits.

- [ ] **Step 3: `LibraryBookOptions`** (a `UIPanel`, about 528x24):
  - API: `LibraryShelfCode Shelf`, `byte Spine`, `string CoverHash`, `bool FixedShelf`, events `CoverRequested` and `Changed`, `void Load(LibraryShelfCode shelf, byte spine, string coverHash, bool fixedShelf)`.
  - Shelf buttons are `CustomButton`s, 70 px wide ("Guides", "Lore", "Stories", "Poems"), selected with `Selected`.
  - Spine swatches are 14x14 squares drawn in `Draw` from `LibraryLabels.Spines`, with a gold border on the chosen one.
  - "Cover" is a 60 px button whose caption becomes "Cover ✓" once a hash is set. Use "Cover *" if that glyph isn't in the font; check `TextRenderer`.

- [ ] **Step 4: Writer.** Add the Library session fields: `LibraryBookId`, plus the `LibraryBookOptions` child, placed at `PROMPT` height. The writer's prompt label is hidden in Library mode, so reuse its row. In `Open`, when `args.Mode == Library`, load the options from `args.Shelf`, `args.Spine` and `args.Hash`, and set `LibraryBookId = args.Id`. In `Send`, pick `LibrarySaveDraft` or `LibrarySend` in Library mode and fill the fields. In `ShowResult`, in Library mode, store `args.Id` when it isn't 0. Cover upload: reuse `Transfers.Upload(picture)` and `UploadFinished`, keyed by the cover hash. Keep the cover out of the document blocks, so it doesn't count toward the 5 pictures.

- [ ] **Step 5: Canvas.** Add `ArtCanvasMode.Library`. In `Open`, map `args.Mode == Library` to it. `Begin()` shows Save, the Library options (fixed to Artwork) and "Send for review". `Send` uses the Library action types and `Subject = Art`.

- [ ] **Step 6: Run the Verify command,** then all client tests.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/Library/LibraryBookOptions.cs", "Chaos.Client/Controls/World/Popups/College/CollegeWriterControl.cs", "Chaos.Client/Controls/World/Popups/College/ArtCanvasControl.cs", "Chaos.Client/Systems/College/PicturePrep.cs", "Tests/Chaos.Client.Tests/College/Library/CoverPrepTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter \"/*/*/CoverPrepTests/*\"", "acceptanceCriteria": ["Writer Library mode: caption, shelf buttons, 8 spines, Cover, Send for review; other modes unchanged", "Canvas Library mode with fixed Artwork shelf", "Cover upload or draw; Send disabled while uploading; No cover clears", "PrepareCover fits 240x340 keeping shape", "Save/Send carry Id, Shelf, Spine, Hash, Subject, Piece; book id kept", "Dirty Library draft saved on close", "CoverPrepTests pass"], "modelTier": "frontier"}
```

---

### Task 14: WorldScreen.Library wiring

**Goal:** Build the Library windows on first use and route every Library display to them.

**Files:**
- Create: `Client: Chaos.Client/Screens/WorldScreen.Library.cs`
- Modify: `Client: Chaos.Client/Screens/WorldScreen.College.cs` (`CollegeWindows`, `BuildCollege`, `HandleCollegeDisplay`)

**Acceptance Criteria:**
- [ ] `CollegeWindows` gains `LibraryCarousel`, `LibrarySubmissions`, `LibraryReview`, `LibraryCollegePieces` and `LibraryDrawings`. `BuildCollege` makes them with `ZIndex = 2`, subscribes their `ActionRequested` to `SendCollegeAction`, and adds them to `Root`.
- [ ] `HandleCollegeDisplay` routes: `LibraryShelf` → carousel `Open`; `LibraryBook` → `Reader.SetSiblings([])`, then `Reader.Open`; `LibrarySubmissions` / `LibraryReview` / `LibraryCollegePieces` → `ShowList(...)`; `LibraryDrawing` → `LibraryDrawings.OnDrawing`; `OpenWriter` with `Mode == Library` → the canvas when `Subject == Art`, else the writer; `WriterResult` with `Mode == Library` → the canvas or the writer, whichever has a Library session open.
- [ ] `CollegeDrawings`/`LibraryDrawings` and transfers are cleared on logout with the rest of the College state.
- [ ] The client builds and all client tests pass.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build` → 0 errors, all tests pass.

**Steps:**

- [ ] **Step 1: Read** `WorldScreen.College.cs`: `BuildCollege`, `HandleCollegeDisplay`, `CollegeWindows`, and the logout `Clear()` call at `WorldScreen.cs:1217`.
- [ ] **Step 2: Put the Library window construction** in a `private void BuildLibrary(CollegeWindows windows)` method in `WorldScreen.Library.cs`, called from `BuildCollege`. Put the Library routing in a `private bool HandleLibraryDisplay(CollegeDisplayArgs args)` that returns true when it handled the display. Call it first in `HandleCollegeDisplay`.
- [ ] **Step 3: Check for existing writer routing.** The `OpenWriter` cases with `when args.Subject == CollegeSubjectCode.Art` already exist. Make sure the Library check runs before them, so a Library Art writer reaches the canvas in Library mode.
- [ ] **Step 4: Run the Verify command.**

```json:metadata
{"files": ["Chaos.Client/Screens/WorldScreen.Library.cs", "Chaos.Client/Screens/WorldScreen.College.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["CollegeWindows holds the Library windows, built lazily and wired to SendCollegeAction", "Every Library display routed as listed", "Library caches cleared on logout", "Build and all client tests pass"], "modelTier": "standard"}
```

---

### Task 15: CLIENT_VERSION bump and docs

**Goal:** Bump the client version for the new protocol, and document the Library in both CLAUDE.md files.

**Files:**
- Modify: `Server: Chaos.DarkAges/Definitions/CONSTANTS.cs:23`
- Modify: `Client: CLAUDE.md` (the `College/` popups paragraph and the `WorldScreen` partial list)

**Acceptance Criteria:**
- [ ] `CLIENT_VERSION` is one more than the highest value found in any of these: the worktree, Chaos.Client `main`'s submodule, and Chaos-Server's `main` branch. Today that is 803, so 804 unless another session bumped it.
- [ ] The client CLAUDE.md names `College/Library/` (the carousel, the submissions, review and College-piece windows, and `LibraryBookOptions`), `ViewModel/College/Library` (`CarouselLayout`, `CoverChoice`), and `WorldScreen.Library.cs`.

**Verify:** `grep -n "CLIENT_VERSION" Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs` → the new value; `dotnet build Chaos.Client.slnx` → 0 errors.

**Steps:**

- [ ] **Step 1: Find the highest version.** Run `git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server show main:Chaos.DarkAges/Definitions/CONSTANTS.cs | grep CLIENT_VERSION` and read the worktree value. Set the worktree value to max + 1.
- [ ] **Step 2: Edit CLAUDE.md** with one short paragraph in the existing style.
- [ ] **Step 3: Run the Verify command.**

```json:metadata
{"files": ["Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs", "CLAUDE.md"], "verifyCommand": "dotnet build Chaos.Client.slnx", "acceptanceCriteria": ["CLIENT_VERSION = highest known + 1", "CLAUDE.md documents the Library windows, view models and WorldScreen.Library.cs"], "modelTier": "mechanical"}
```

---

### Task 16: File server Library page

**Goal:** Add an admin panel Library page with the Waiting and Reports lists, the book view with pictures and history, and the four actions; and keep Library names right when a player is renamed.

**Files:**
- Create: `FS: src/Chaos.FileServer/Admin/AdminLibraryController.cs`
- Create: `FS: src/Chaos.FileServer/Admin/Pages/library.nav.html`, `library.view.html`, `library.js`
- Modify: `FS: src/Chaos.FileServer/Admin/AdminPageModules.cs:20` (`Names`)
- Modify: `FS: src/Chaos.FileServer/Admin/Pages/core.js` (`panelDesc` at lines 12–15, `NAV_ORDER` at line 61)
- Modify: `FS: src/Chaos.FileServer/ServerOps/ControlPipeClient.cs` (interface lines 67–115 and `NamedPipeControlClient`)
- Modify: `FS: src/Chaos.FileServer/ServerOps/AislingRenamer.College.cs`
- Modify: `FS: tests/Chaos.FileServer.Tests/ServerOps/Fakes.cs` (`FakePipe`, line 63)
- Test: `FS: tests/Chaos.FileServer.Tests/Library/AdminLibraryEndpointsTests.cs`, `LibraryPageTests.cs`, and the existing renamer tests

**Acceptance Criteria:**
- [ ] `GET api/admin/library` returns the pipe's lists; `GET api/admin/library/{id}?version=waiting|shelf` returns one book.
- [ ] `GET api/admin/library/picture/{hash}` serves `DataDir/LocalStorage/College/Pictures/{hash}.png` or `.jpg`. It returns 404 for a hash that isn't 64 lower-case hex characters or doesn't exist, and never reads outside that folder.
- [ ] `POST api/admin/library/{id}/approve|reject|takedown|dismiss` with `{reason}` calls the pipe. Reject and takedown with an empty reason return 400 without calling the pipe. Success writes `audit.Write(Role, "library-<action>", new { id, reason })`.
- [ ] Pipe errors map like the Moderation controller: `PipeUncertain` → 503, others via `PipeFailed`.
- [ ] The page is registered in all three places, sits under the "community" workspace, shows the two lists, opens a book with its text, headings, pictures (via the picture endpoint), drawings (rendered to a `<canvas>` from palette and pixels) and history, and asks for a reason in a per-row textarea for Reject and Take down.
- [ ] Renaming a player in `AislingRenamer.College.cs` also renames `LibraryBooks[].Author`, `Reports[].By`, `History[].By` and `TakenDownBy`, ignoring case.
- [ ] `dotnet test tests/Chaos.FileServer.Tests` passes.

**Verify:** from `FS:` `dotnet test tests/Chaos.FileServer.Tests` → all pass.

**Steps:**

- [ ] **Step 1: Read the patterns.** Read `AdminModerationController.cs`, `moderation.js` (the `mdApi` wrapper at lines 8–25, `esc()`, delegated clicks), `guilddesigns.js` (per-row textarea at lines 77 and 156–158), `GuildDesigns/GuildDesignCatalog.cs` (how it finds `DataDir/LocalStorage`), `AislingRenamer.College.cs`, and `Moderation/AdminModerationEndpointsTests.cs` + `ModerationPageTests.cs`.
- [ ] **Step 2: Pipe client.** Add `LibraryListAsync`, `LibraryBookAsync(int id, string version)` and `LibraryDecideAsync(string action, int id, string reason)` to `IControlPipeClient` and `NamedPipeControlClient`. Reads use `SendAsync`; decisions use `SendTrackedAsync`. Add the same members to `FakePipe`, recording calls and returning set-up replies.
- [ ] **Step 3: Write the failing tests:**
  - `Listing_returns_the_pipe_lists`
  - `Reading_a_book_passes_the_version`
  - `Rejecting_without_a_reason_is_a_bad_request_and_does_not_call_the_pipe`
  - `Approving_calls_the_pipe_and_writes_the_audit`
  - `A_bad_picture_hash_is_not_found` (`../x`, uppercase hex, 63 characters)
  - `A_stored_picture_is_served_with_its_type`
  - `An_editor_token_cannot_decide` (403, like the Moderation tests)
  - `The_library_page_is_registered`, copying `ModerationPageTests`
  - In the renamer tests, `Renaming_updates_library_authors_reporters_and_history`.
- [ ] **Step 4: Implement** the controller (`[ApiController][Route("api/admin/library")][AdminAuthorize("Publisher")]`), the page files, the registration, and the renamer change. In the renamer, walk `LibraryBooks` in the `CollegeState.json` "default" object with the same JSON-node style the file already uses.
- [ ] **Step 5: Run the Verify command.**

```json:metadata
{"files": ["src/Chaos.FileServer/Admin/AdminLibraryController.cs", "src/Chaos.FileServer/Admin/Pages/library.nav.html", "src/Chaos.FileServer/Admin/Pages/library.view.html", "src/Chaos.FileServer/Admin/Pages/library.js", "src/Chaos.FileServer/Admin/AdminPageModules.cs", "src/Chaos.FileServer/Admin/Pages/core.js", "src/Chaos.FileServer/ServerOps/ControlPipeClient.cs", "src/Chaos.FileServer/ServerOps/AislingRenamer.College.cs", "tests/Chaos.FileServer.Tests/ServerOps/Fakes.cs", "tests/Chaos.FileServer.Tests/Library/AdminLibraryEndpointsTests.cs", "tests/Chaos.FileServer.Tests/Library/LibraryPageTests.cs"], "verifyCommand": "dotnet test tests/Chaos.FileServer.Tests", "acceptanceCriteria": ["List and book endpoints call the pipe", "Picture endpoint serves only valid existing hashes inside the Pictures folder", "Decisions need reasons where required, call the pipe and audit", "Pipe errors map like Moderation", "Page registered, shows lists, book with pictures, drawings and history, reasons per row", "Renamer renames Library authors, reporters, history and TakenDownBy", "All FS tests pass"], "modelTier": "standard"}
```

---

### Task 17: Whole-feature check and in-game check list

**Goal:** Run every test suite and the map checks across all four worktrees, and write the in-game check list for the user.

**Files:**
- Create: `Client: docs/superpowers/plans/2026-10-23-college-library-ingame-checks.md`

**Acceptance Criteria:**
- [ ] The full server test suite passes, or every failure is listed with its output and shown to be unrelated to this work.
- [ ] All client tests, all FS tests, `college_maps.py --check`, the map unittests and `check_dialogs.py` pass.
- [ ] The check list covers: entering and leaving the Library; each bookcase and floor label; a new writing piece and an Artwork piece with and without a cover; the 3-waiting limit; an edit waiting while the old version stays; approve and reject in the game and on the admin page; report, dismiss and take down; the mark going up and down; the reviewer login message; and copying a College piece.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj` (from `Server:`) → 0 failed, or only pre-existing failures listed in the report.

**Steps:**

- [ ] **Step 1: Run every suite** named in Global Constraints, each from its worktree. Capture the totals line of each.
- [ ] **Step 2: Write the check list,** numbered, one action and one expected result per line.

```json:metadata
{"files": ["docs/superpowers/plans/2026-10-23-college-library-ingame-checks.md"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj", "acceptanceCriteria": ["Full server suite passes or failures shown unrelated", "Client, FS, map and dialog checks pass", "In-game check list covers the listed flows"], "modelTier": "standard"}
```

---

### Task 18: Commit the full implementation

**Goal:** Make one commit per repo with the whole Library change set.

**Files:**
- All files changed by Tasks 1–17, in their worktrees.

**Acceptance Criteria:**
- [ ] Chaos-Server (submodule, branch `college-library`): one commit with every server file from Tasks 1–6, 8 and 15.
- [ ] Unora (`college-library`): one commit with the map builder, tests, generated map files, merchant and dialogs.
- [ ] Chaos.FileServer (`college-library`): one commit with Task 16's files.
- [ ] Chaos.Client (`college-library`): one commit with the client files, CLAUDE.md, the check list, and the submodule pointer at the new server commit.
- [ ] Every file is staged by explicit path; `git status --short` in each worktree shows nothing left unstaged from this work.
- [ ] Each message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Nothing is pushed.

**Verify:** `git -C <each worktree> log --oneline -1` → the new commit; `git -C <each worktree> status --short` → clean of Library files.

**Steps:**

- [ ] **Step 1:** In each worktree, run `git status --short` and list the Library files.
- [ ] **Step 2:** Stage them by explicit path. Write the message to a unique scratchpad file, then run `git commit -F <file>`. Order: server, Unora, file server, then the client (after `git add Chaos-Server` for the submodule pointer).
- [ ] **Step 3:** Report the four commit hashes.

```json:metadata
{"files": [], "verifyCommand": "git log --oneline -1", "acceptanceCriteria": ["One commit each in server, Unora, file server and client", "Explicit-path staging; nothing left unstaged", "Co-Authored-By trailer; nothing pushed"], "modelTier": "mechanical"}
```
