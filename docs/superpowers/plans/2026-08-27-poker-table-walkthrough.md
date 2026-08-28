# Poker table: two-client manual walkthrough

The properties on this branch that no agent could verify. Everything here needs a running
server, two clients, and two characters — which is why it is a script for you and not a test.

Branch: `feature/poker-table` in all three repos. Run `git submodule update --init` in
`Chaos.Client` first — the branch pins `Chaos-Server` to the commit this was tested against.

---

## 0. What is already proven, so you do not re-test it

84 poker tests pass on the server. Hand evaluation, betting-round legality, blind posting,
rake, side-potless settlement, departure settlement and gold conservation are all covered
there and do not need a human. **Do not** spend your session re-checking that a flush beats
a straight.

What is unproven is everything that only exists when two real clients are attached:
tile walkability, seat claim/release, the wire format, and hole-card privacy.

---

## 1. Setup

### Server — local, Debug, repointed

`appsettings.local.json` only loads under `#if DEBUG`; a Release boot silently runs against
an empty scaffold and proves nothing.

1. In `Chaos-Server/Chaos/appsettings.local.json`, set `StagingDirectory` to
   `C:\Users\mikeb\Documents\GitHub\Unora`. **Do not commit this** — the file is tracked.
2. `dotnet run -c Debug` from `Chaos-Server/Chaos`.
3. Wait for `WorldServer: Listening` (~7 s).

**Watch the boot log.** `PokerCatalogValidationService` runs at startup and logs (never throws).
A line matching `Poker table catalog problem:` means the catalog is misconfigured — fix that
before going further, because the table will behave in ways this script does not predict.
Silence from that service is the pass condition.

### Clients — two, both built from this branch, both pointed at localhost

The new opcodes (client `118`, server `120`) exist only on this branch. A stock client will
not render the table, and the live server has no poker. Both halves must be branch builds.

```powershell
$env:DA_LOBBY_HOST = '127.0.0.1'
$env:DA_LOBBY_PORT = '4200'          # local lobby, not 6900
$env:DA_PATH       = 'C:\Users\mikeb\Documents\Chaos\Unora'   # the .dat archives
Start-Process 'C:\Users\mikeb\Documents\GitHub\Chaos.Client\Chaos.Client\bin\Debug\net10.0\Chaos.Client.exe'
```

Run it twice. Log in as two different characters.

### Characters — gold matters

`MinimumBuyIn` is **48,000** and the eligibility gate is `Gold >= MinimumBuyIn`, re-checked
before every hand. Give both characters comfortably more — say 200,000 — or they will be
silently skipped when the button comes round and you will think the table is broken.

48,000 is `MaxExposure`: four bets a street at 2000/2000/4000/4000. Requiring it to be dealt
in is what makes all-ins structurally impossible, which is what lets the design work with no
chips and no side pots.

---

## 2. The floor

Rucesion Casino. Table merchant at **(9, 27)**, facing down. Six seat reactors:

```
        (8,26)   (9,26)   (10,26)        <- seats 0, 1, 2
                 (9,27)                  <- the table
        (8,28)   (9,28)   (10,28)        <- seats 3, 4, 5
```

**Step 1 is a walkability check and it fails fast.** Walk one character onto each of the six
tiles. Tile passability lives in the compiled `.map` binary and cannot be read from JSON, so
this is the first thing that can be wrong. If a tile blocks you, that seat is unreachable and
the content needs moving — stop and say so.

Every seat is within 2 tiles (Manhattan) of the table, inside the script's search range.

---

## 3. The runs

### Run A — seat, claim, release

1. Character 1 walks onto **(8, 26)**. The poker panel should open, showing seat 0 occupied
   by their name and the table idle (one player is not enough to deal).
2. Walk them off the tile. The seat should release — the reactor polls on Update, so allow a
   tick. Panel closes.
3. Step back on. Seat re-claimed.

Then, with character 1 seated, walk character 2 onto **the same tile**. Known gap, already
triaged: the second claimant is **refused silently**. No message. If that bothers you it is a
one-line fix, but it is deliberate for now and not a defect to report.

### Run B — the cheapest complete hand

Both characters seated (say seats 0 and 1). **Write down both gold totals before you start.**

Note the panel shows `Gold:` only for **your own** seat; every other seat reads `-`. That is
deliberate — a player's purse is their whole net worth here, not a table stack — so take the
totals from each client's own HUD, not from the poker panel's view of the other player.

The table deals automatically once two eligible players are seated. Blinds are **1000 / 2000**.

Play the simplest possible hand: the small blind folds preflop.

Expected, exactly:
- No flop was dealt, so **no rake** ("no flop no drop").
- Pot 3000 goes to the big blind.
- SB net **−1000**. BB net **+1000**. Total gold across both characters **unchanged**.

If total gold moved on a hand that never saw a flop, stop — that is a rake bug and it is
the class of defect this branch has already produced twice.

### Run C — showdown, and the check that matters

Play a hand to showdown. Both call to the river, both check it down.

The settled hand stands for **4 seconds** before the next is dealt. That pause exists only
because this review found it missing — before it, the reveal lived about 33 ms and this run
was literally impossible to perform.

**The hole-card privacy check.** Before the river is turned, look at client 2's panel and
confirm it shows nothing for client 1's hole cards, and vice versa. At showdown both hands
should appear.

Be honest about what that proves. **Seeing nothing rendered is not proof nothing was sent.**
The guard is server-side — `PokerTableScript.cs:872`, where `maySee` requires the seat to be
yours or the hand to have reached `Showdown` — and snapshots are built per recipient. The
visual check confirms the rendering agrees with the guard; it does not confirm the wire is
clean.

If you want the wire-level answer, say so and I will add a temporary `#if DEBUG` assertion in
the client view model that screams when a snapshot arrives carrying hole cards for a seat that
is not yours and the street is not Showdown. That turns "looks right" into "cannot happen
without a log line." It is about ten minutes of work and I would rather do it than have you
accept a visual as a proof.

Also worth recording during this run: **rake**. Pot × 5%, capped at 12,000. Total gold across
both characters should drop by exactly the rake and no more — that gold is destroyed, by design.

### Run D — timeouts and the shot clock

Seat both, let a hand start, and simply do not act. Expect:
- **20 seconds** per decision, then the actor is timed out (folded / checked).
- **2 timeouts** and that player is set to sit out.
- **3 sit-out hands** and they are released from the seat entirely.

### Run E — closing mid-hand

With gold committed to a live pot, hit the panel's Close button or Escape.

You should get a confirmation prompt first. Confirm it, and the seat is released and the
committed gold is forfeit to the pot.

**Close and Leave are the same thing.** Both route to `ReleaseSeat`. This is deliberate: a
seat that keeps being dealt in behind a closed panel is a gold trap. I got this backwards
earlier in the build and said closing kept you seated — it does not, and the confirmation
prompt exists precisely because it does not.

### Run F — the second audit's fixes

Each of these was a confirmed defect in the UI overhaul and is now fixed; re-check the fix, not the
feature.

1. **Refresh mid-hand.** With gold committed, press F5 (same-map refresh) with the panel open.
   The panel must stay open and keep showing the hand, and Escape must still show the
   "bet stays in the pot" confirmation. Before the fix the refresh wiped the view model under the
   panel and Escape closed it silently — forfeiting the pot with no prompt.
2. **Warp out mid-hand.** Have one player warp/log to another map while seated. Their panel must
   close on the map change (not sit painted over the new map), and the other client must see
   "*Name* leaves the table." within a tick or two.
3. **Disconnect between hands.** Kill one client (not Leave — close the window) between hands.
   The other client must see "*Name* leaves the table." and the seat go empty. Before, the
   table stood them up silently.
4. **Sit down between hands.** After a hand completes, have a third character sit. Their panel
   must open without a burst of action flashes replaying the previous hand's last street.
5. **Chat length.** Open the table's Chat prompt and hold a key: the box must stop accepting
   input at the same length the HUD's own say box does (57 for an 8-character name), not 90.
   What you send must arrive whole on the other client.
6. **Emote then Chat.** Click Chat, then Emote: the prompt must close and the picker open, not
   both stack. Then the reverse.
7. **Portraits survive a refresh.** With two seated, press F5 on one client. The other seat's
   portrait must still be there afterwards and its emotes must still animate on the portrait.
   Before, the portrait held a dead entity after the refresh and emotes stopped.

### Run G — the gold audit's fixes

Three findings from the gold-duplication audit. None minted in-process; re-check the fix, not the
feature.

1. **Leave on the river, having already acted.** Three-handed, everyone checks to the river. The
   seat that opens the river checks and then stands up (walk off the tile). The other two check.
   The leaver must NOT be announced as the winner and must be exactly their committed gold down —
   even if they held the best hand. Before, they were paid at showdown; on a disconnect that payout
   went into a dead object and vanished.
2. **Hand result is durable at once.** Play one hand to completion, then kill the server process
   (not a clean shutdown) within a minute. Restart. Both characters must log in at their post-hand
   balances. Before, either side could roll back to its last 5-minute save — minting or destroying
   the pot depending on which side happened to be saved. Watch the log for
   `Failed to force-save`; silence is the pass.
3. **Winner at the gold cap.** Admin-set one character to a few thousand under `MaxGoldHeld`
   (500,000,000) so the deal-time gate still passes, have a bystander drop gold on them mid-hand
   (exchange), then let them win. The table must NOT break down; the payout must land in their
   **bank** with "gold was sent to your bank as overflow". Before, the payout threw and the whole
   pot was destroyed.

---

## 4. Known, already triaged — not worth reporting

- **A player holding near the 500,000,000 gold cap is sat out**, with the reason "You are
  holding too much gold to be dealt in". That is the fix for a defect where such a winner
  could not be paid and the whole pot was destroyed; the seat is refused rather than the pot
  lost. You will not hit this unless you deliberately set a character near the cap.
- **Sprite `1310`** collides with `twentyOneTable.json`. The Hold'em table will look identical
  to a twenty-one table on the same floor. Cosmetic, and your call.
- Second claimant on an occupied tile is refused silently (Run A).
- `SeatRegistry` is not self-healing across unloaded map instances.
- The seat reactor re-resolves its table every tick.
- 4+-handed play is untested. If you can get four characters seated, that is genuinely new
  coverage — everything to date is heads-up and three-handed.

## 5. What to bring back

Per run: pass, or what you saw. For anything involving gold, **the before and after totals** —
not an impression. Gold conservation is the invariant this whole design is built around, and
every real defect found so far showed up as a number that did not add up, never as something
that looked wrong on screen.
