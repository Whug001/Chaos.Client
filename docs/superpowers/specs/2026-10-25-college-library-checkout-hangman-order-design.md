# College: Director self-approval, Library checkout, Hangman turn order

Date: 2026-10-25. Source: player suggestions from the College (Skeith, Sneap), relayed by the user.

## 1. Directors approve their own Library books

**Problem.** `LibraryRules.MayReview` refuses any reviewer who is the author, or whose machine key matches the
author's. A Director's own book never appears in their review list, so Skeith could not find or approve the guide
they wrote.

**Rule change.**
- A College Director (`CollegeRoles.IsDirector`) may approve or reject a book they wrote. The machine-key check is
  also waived for their own book.
- Approving still sets `Counted` and adds to the author's Library Author mark, as for any other approval.
- Knights and admins are unchanged: they still cannot review their own books.
- Reports stay with others: a Director may not dismiss reports on, or open in report-review mode, a book they wrote.
  So the waiver covers the Waiting review (approve/reject of a waiting version, including an edit of a shelved book)
  only, not `ReviewReported`/`DismissLibrary`.

**Where.** `LibraryRules.MayReview` gains the Director's status as an input (or a sibling
`MayReviewWaiting(book, reviewer, machineKey, isDirector)`), and the call sites in `CollegeService.Library.cs` that
handle the waiting list, `OpenBook(ReviewWaiting / ReviewShelf)`, `ApproveLibrary` and `RejectLibrary` pass it.
`ReviewList` includes the Director's own waiting books. Report paths keep the old rule.

## 2. Checking out a Library copy

**Purpose.** A player can carry a copy of a shelved book, for example a quest guide, and read it anywhere. The book
stays on the shelf for everyone.

**Checkout.**
- The Library book window gets a **Check out** button, shown when the book is on the shelf (`OnShelf` or
  `EditWaiting`) and its shelf is Guides, Lore, Short Stories or Poems and Songs. Not Artwork.
- Free. A new Library panel action carries the book id; the server re-checks the shelf and status.
- A player holds at most one copy. Checking out while holding one replaces it, with the message
  "You return <old title> and check out <new title>."
- Full inventory: refused with "You have no room for a Library copy."
- The button is hidden when the window is opened from a copy, and in review/own/submission views.

**The item.**
- One new item template in Unora, "Library Copy", using an existing book icon from the item art (no new art).
- Display name "Library Copy: <title>" if the item model supports a per-instance name; otherwise the template name,
  with the title shown when read. Decided during planning after checking the item model.
- `NoTrade`, cannot be dropped, `PreventBanking`, `NoNpcSale`.
- Two new saved item fields: the Library book id and the expiry time (UTC). Persisted like `PortraitId`/
  `PortraitHash` (schema, mapper, serializers).
- Expires 24 hours of real time after checkout.

**Reading.**
- Using the item opens the Library book window for the book's **current** shelved version (`book.Shelved`), with the
  same display the Library uses (`SendLibraryBook` with `LibraryOpenMode.Shelf`), anywhere in the world. A copy made
  before an approved edit shows the edited text.
- Check out, review, take-down and report buttons are hidden in this view.
- If the book is no longer on the shelf (taken down, removed, never shelved), the copy is removed with
  "This book is no longer on the Library shelves."
- If the copy has expired, it is removed (see below) instead of opening.

**Expiry.**
- When expired, the copy is removed with "Your Library copy of <title> has been returned."
- Checked on login, on use, and once a minute for online players (an existing tick, e.g. `CollegeTickService`, or
  the item's update hook; decided in planning).

**Client.** The new button and the panel action in the Library book window; CLIENT_VERSION bump. A display flag
(e.g. `CanCheckOut`) on `CollegeDisplayArgs` tells the client when to show it, and a context value marks the
copy view.

## 3. Hangman random turn order

**Problem.** Turns follow join order, and each new word starts after the player who ended the last word. Players at
the top of the list get the early picks.

**Rule change.**
- At game start the turn order is shuffled.
- Each player gets one turn per pass. When the pass is used up, a new pass is shuffled from all players still in the
  game.
- A new pass never starts with the player who took the last turn of the previous pass (when there are 2 or more
  players).
- A new word continues the current pass; `NextStarter` no longer picks the first player of a word.
- A late joiner is inserted at a random place among the players who have not yet gone in this pass.
- A removed player is skipped. A player who rejoins is treated like a late joiner.
- Turn skips (timeouts) count as that player's turn in the pass.
- Attendance (`TurnsTaken`/`TurnsGiven`), buzzing and solving are unchanged.

**Where.** `HangmanSession` and `LiveHangman`. `Players` keeps its role as the stable player list. A new pass queue
(list of keys still to go this pass) drives `BeginTurnFrom`'s replacement. Randomness is injectable (a `Random` passed
in, or a seed) so tests are deterministic.

## Testing

Unit tests in the existing server test project:
- `LibraryRulesTests` / `LibraryServiceTests`: a Director approves their own waiting book and it counts; a Knight
  still cannot; a Director cannot dismiss reports on their own book; the Director's own book shows in their review
  list.
- Checkout: allowed shelves succeed; Artwork refused; a not-shelved book refused; a second checkout replaces the
  first; a full bag refuses; reading shows the newest shelved version; a taken-down book removes the copy; an expired
  copy is removed on use and on the tick.
- `HangmanSessionTests`: every player goes once before anyone repeats; the order differs across passes with a fixed
  seed; no back-to-back turn across passes; a late joiner gets a turn in the current pass; a removed player is
  skipped; a new word continues the pass.

## Out of scope

- Fees, more than one copy per player, copies of Artwork.
- Changing who may review or dismiss reports beyond the Director self-approval rule.
