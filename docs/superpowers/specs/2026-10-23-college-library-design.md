# Mileth College Library — design

Date: 2026-10-23
Asked for by: the College Director (request pasted by the user on 2026-10-23)
Repos: Chaos.Client (client), Chaos-Server (submodule, server), Unora (data and maps), file server (admin page)

## Goal

Players write guides, lore, stories, poems and songs, or draw artwork. A College Director or a staff member reviews each piece. Approved pieces go on a bookshelf in a new Library room off the College hall, where anyone can read them at any time.

## Decisions (from the brainstorm)

| Topic | Decision |
|---|---|
| Entrance | 6 warps on the hall's east wall, between the two pillar pairs |
| Room | 20x20, same base as the subject rooms, one bookcase per shelf |
| Shelves | Guides, Lore, Short stories, Poems and songs, Artwork (no sub-shelves) |
| Browsing | Book carousel: spines on a curved shelf; the front book turns to show its cover |
| Writing and submitting | At a Librarian NPC in the Library |
| Reviewers | College Directors and staff (GMs and admins); nobody reviews their own piece |
| Where review happens | In the game (at the Librarian) and on a new admin panel page |
| Removing | Readers report; a Director or staff member takes the book down. Authors can take down their own books |
| Limits | The College limits: 10,000 characters, 5 pictures, 40-character title |
| Cover | Optional, upload or draw; does not count toward the 5 pictures |
| College pieces | Can be copied into a Library draft; the College piece stays; always reviewed again |
| Edits after approval | Allowed; reviewed; the shelf version stays until the edit is approved |
| Who can submit | Any player; at most 3 pieces waiting for review at once |
| Reward | A "Library Author" legend mark with a count; no College marks |

## 1. The room and the entrance

### Entrance (hall, map 762, instance `mileth_college`)

- The hall's east edge (x = 44) has a pillar and potted plant at (44, 8–9) and another at (44, 16–17). The carpet between them runs from (44, 10) to (44, 15).
- Those 6 tiles become `warpWithChecksReactorTile` warps into the Library. They are built by `Tools/College/college_maps.py` like the other College warps.
- The Library's exit warps land players at (43, 12) in the hall.
- A hall sign reads "Library".

### The room

- New map 787 (`lod787.map`), instance `college_library`. If another piece of work takes 787 first, use the next free number when building.
- Built by `college_maps.py` from `chamber_763_base.map`: the same floor, walls and south doorway as the subject rooms. The benches and lectern are left out.
- Exits: the 4 carpet warps at (8–11, 19) back to the hall, with arrival at (9, 18). These are the subject rooms' `ROOM_EXITS` and `ROOM_ARRIVAL`.
- 5 bookcases, one per shelf, use the bookcase art from the hall's study corner:
  - north wall: Guides, Lore, Short stories
  - west wall: Poems and songs
  - east wall: Artwork
- Each bookcase has a floor label, like the gallery stands.
- The Librarian NPC stands in the middle of the room, facing the door.
- Music: the College's (`MUSIC_COLLEGE`).
- `college_maps.py --check` covers the new warps, arrival tiles and walkability, like the existing rooms.

Clicking a bookcase opens the carousel at that shelf.

## 2. Browsing and reading

### Carousel window (`LibraryCarouselControl`)

- Header: the shelf name and the number of books on it.
- Tabs along the top switch between the 5 shelves.
- Books sit spine-out in a curved row. The spine shows the spine color and the title.
- The arrow buttons, the mouse wheel and Left/Right spin the row. Clicking a spine brings that book to the front.
- The front book turns to show its cover. Under it: title, author, approval date and an **Open** button. Double-clicking the front book also opens it.
- Below the row:
  - a search box that matches title or author
  - a sort switch: Newest or A–Z
  - page buttons, 20 books per page
- The spin and placement maths live in `CarouselLayout`, a class with no graphics, so they can be unit tested.

### Loading

- The server sends one page at a time. Each entry has: book id, title, author, spine color, cover picture hash and approval date.
- Covers download only when a book comes within 2 places of the front. They use the College's picture download (`CollegePictureTransfers`) under its existing rate limit of 60 per minute, and are cached for the session.

### Reading

- **Open** loads the shelf version into `CollegeReaderControl`.
- A Library footer adds **Report** for everyone and **Take down** for the author, Directors and staff.
- Artwork shows as one full-size picture with its title, artist and note.

## 3. The Librarian and submitting

### Librarian menu

- **Write a piece**: pick a shelf. Guides, Lore, Short stories, and Poems and songs open `CollegeWriterControl` in Library mode. Artwork opens `ArtCanvasControl` in Library mode. Drafts save the way College drafts do.
- **My submissions** (`LibrarySubmissionsControl`): the player's Library books, each with a status:

  | Status | Meaning |
  |---|---|
  | Draft | Not sent yet |
  | Waiting for review | Sent, never approved |
  | On the shelf | Approved, nothing waiting |
  | Edit waiting | Approved; a new version waits for review; the old version stays on the shelf |
  | Rejected | Sent and refused; shows the reason |
  | Taken down | Removed from the shelf; shows the reason and who removed it |

  Actions: edit, send for review, withdraw a waiting piece or edit, and take down your own book.
- **Bring a College piece**: lists the player's own College writing and drawings from drafts, hand-ins and the gallery. Picking one copies it into a new Library draft. Music pieces are not offered.
- **Review** (Directors and staff only; see section 4).

### Library mode in the writer and the art canvas

- A **Shelf** picker. Artwork is fixed to the Artwork shelf.
- A **Spine color** row with 8 colors.
- A **Cover** button: upload an image or draw one on the art canvas.
- **Send for review** in place of the hand-in button.

### Covers

- Optional. They do not count toward the 5 pictures.
- Portrait, scaled to fit 240x340. File size limit 200 KB (`MAX_PICTURE_BYTES`).
- Uploaded through the existing picture upload (`PictureUploads`).
- An Artwork book with no cover uses its drawing as the cover.
- A book with no cover shows a plain cover in its spine color, with the title and author.
- A cover is reviewed with its piece. A changed cover on an approved book is an edit and goes through review.

### Limits

- At most 3 waiting items per player. An edit waiting for review counts as one. A 4th send is refused with an orange bar message of 45 characters or fewer.
- Text and pictures use the College limits (`CollegeProtocol.MAX_TEXT_CHARS`, `MAX_PICTURES`, `MAX_TITLE_CHARS`, `MAX_BLOCKS`) and are checked by `PieceRules`.
- A rejected piece can be fixed and sent again.

## 4. Review and reports

### Reviewers

- College Directors (`CollegeService.MAX_DIRECTORS` = 2) and staff (GMs and admins).
- Nobody can review their own book. The server checks this on every review action.

### In the game (`LibraryReviewControl`, from the Librarian)

- **Waiting** tab: new pieces and edits, oldest first. Edits are marked "Edit". The reviewer can switch between the shelf version and the new one.
- **Reports** tab: books on the shelf that have reports, with every reason given.
- Opening an item shows it in the reader with a review footer:
  - **Approve**: the piece or edit becomes the shelf version.
  - **Reject**: needs a reason of up to 300 characters (`MAX_COMMENT_CHARS`). The author sees it.
  - For reports: **Take down** (needs a reason) or **Dismiss**. Dismiss clears that book's reports.
- When a Director or staff member logs in with items waiting, an orange bar shows a message such as "Library: 3 to review".

### Admin panel (file server)

- A new Library page, built the same way as the Moderation page and calling new server admin endpoints.
- It has the same Waiting and Reports lists, showing text and pictures in the browser.
- Actions: Approve, Reject, Take down and Dismiss, each needing a reason where the game asks for one.
- The page shows each book's review history.

### Reports

- Any reader can press **Report** with a reason of up to 300 characters. Each player can report a book once.
- Reports don't hide the book. They only add it to the Reports list.

### Telling the author

The author is told about every decision (approve, reject, take down) by orange bar if they are online, and by College mail (`ICollegeMail`) with the reason.

### History

Every action is stored on the book with who did it, when, and the reason: send, approve, reject, withdraw, report, dismiss, take down.

## 5. The author mark and storage

### Legend mark

- A "Library Author" mark is given on the author's first approved book.
- The count goes up by 1 for each newly approved book. Approved edits don't change it.
- When a Director or staff member takes a book down, the count goes down by 1. At 0 the mark is removed.
- When the author takes down their own book, the count stays.
- No College marks are given for Library books.

### Storage

- A `LibraryBook` record holds:
  - id, author, shelf, spine color
  - the shelf version (piece id and cover hash), if it has one
  - the waiting version (piece id and cover hash), if it has one
  - status, approval date, reports (reporter, reason, time), history
- Pieces live in `CollegePieceStore`. Pictures and covers live in `CollegePictureStore`. They are saved with the rest of the College state and backed up with it.
- The unused-picture cleanup (`UnusedPictureHours`) counts Library pieces and covers as users. A book's pictures are never deleted while the book exists.
- Copying a College piece makes new piece ids but shares the picture hashes, so nothing is uploaded twice.

### Checks and rate limits

- The College's existing checks apply to writing and reporting: dead, blocking dialog, muted.
- The College's existing download, save and upload rate limits apply (`CollegeFetchLimit`, `DraftSaveBurst`, `PictureUploadsPerHour`, `PictureBytesPerDay`).

## 6. Network messages, windows, testing and release

### Network messages

These are new message kinds in the existing College packet family, with no new opcodes.

- Client to server: open shelf page (shelf, page, search, sort), open book, save Library draft, send for review, withdraw, take down, report, my submissions, bring College piece, review list, approve, reject, dismiss.
- Server to client: shelf page, book contents, my-submissions list, review list, result message.
- The server checks every message for: the player, the player being in the Library room (for shelf, write and review actions), the player's role, and the book's state. The client is never trusted.

### Server

- `CollegeService.Library.cs`: a new part of `CollegeService`, next to Entries, Wall and Quizzes.
- `LibraryRules`: the pure rules (waiting limit, own-piece review, state changes, mark count). Unit tested.
- A Librarian dialog script and its dialog JSON in `Unora/Data/Configuration/Templates/Dialogs/Temauir/mileth/college/`. Options are 35 characters or fewer, and there are no em dashes.
- Bookcase click handling: a reactor or object script on each bookcase tile that opens the carousel at its shelf.
- Admin endpoints for the file server page.

### Client (`Controls/World/Popups/College/Library/`)

- `LibraryCarouselControl` and `CarouselLayout`
- `LibrarySubmissionsControl` and `LibraryReviewControl`, built on `CollegeListWindow`
- Library footers on `CollegeReaderControl`
- Library mode in `CollegeWriterControl` and `ArtCanvasControl`
- `Screens/WorldScreen.Library.cs` for the wiring

### File server

- The Library admin page.

### Testing

- Server unit tests:
  - the 3-waiting limit, counting edits
  - refusing review of your own book
  - an edit leaving the shelf version until it's approved; rejecting an edit keeps the shelf version
  - the mark count going up and down
  - reporting once per player, and dismissing reports
  - the picture cleanup keeping Library pictures and covers
  - copying a College piece into a draft, including sharing pictures
  - permission and room checks on every message
- Client unit tests: `CarouselLayout`, the list states, and cover fallback choice.
- `college_maps.py --check` passes with the new room and warps.
- An in-game check list for the user at the end.

### Release

- The server deploys first, then the client with a new `CLIENT_VERSION`.
- Map 787 and the hall changes ship the same way map 786 (the card shop) did.
- Nothing goes live until the user deploys it.

## Out of scope

- The class announcement (green "starting soon" message) and games in Activity classes. These are a separate small batch.
- Music on the shelves.
- Ratings, favorites or comments on books.
