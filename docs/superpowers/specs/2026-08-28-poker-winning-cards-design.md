# Poker showdown: hold the reveal longer, and show which five cards won

**Date:** 2026-08-28
**Scope:** `Chaos-Server` (`master`) and `Chaos.Client` (`main`)
**Builds on:** `2026-08-28-poker-win-reveal-design.md`

## Problem

The showdown is on screen for four seconds and the panel names the winning category, but the
player still has to find the five cards that made it among seven (two hole cards plus five on
the board) — for the winner and for anyone who split the pot.

## Decisions

1. **The reveal lasts 8 seconds.** `PokerTable.InterHandPause` goes from 4000 ms to 8000 ms. It is
   a constant, not a catalog knob (nobody has asked for per-table timing). Tests already inject
   `TimeSpan.Zero` and are unaffected.

2. **The server names the winning five.** `HandEvaluator.BestFive(cards)` returns the five cards
   that make the best hand out of five to seven, found by evaluating every five-card subset with
   the existing `Evaluate` and keeping the highest `HandRank` (first found on a tie). Because it is
   defined *through* `Evaluate`, it can never disagree with the rank that decided the pot.

3. **Recorded per winner, sent per seat.** At showdown `PokerHand` records `BestFive` of each
   winner's hole cards + board as `WinningCardsOf(handIndex)` (empty for every other seat, and for
   every seat when the hand was won by folds). `PokerSeatEntry` gains `WinningCards` (`List<byte>`,
   card indices, 0–5 entries), filled on the completion snapshot for winner seats only, at showdown
   only. Wire: `count` + card bytes, after `HoleCards`.

4. **Client: outline the five, dim the rest.** During the reveal — i.e. while any seat's
   `WinningCards` is non-empty — every face-up card on the felt that is in the union of all
   winners' `WinningCards` gets a 2 px gold frame (the winner's gold), and every face-up card that
   is not gets a translucent dark overlay. Hole cards on the plaques and community cards on the
   board are treated identically. Face-down cards and empty slots are untouched. When no seat has
   `WinningCards` (hand running, fold win, next hand's first snapshot) every card is drawn plainly.
   A split pot unions both winners' fives — the shared board cards frame once; each winner's own
   hole cards frame on their plaque.

   Rejected: lines from the plaque to each card (cross the pot and other plaques; hard to read),
   outline without dim (the five do not pop against seven face-up cards).

## Server

- `HandEvaluator.BestFive(ReadOnlySpan<Card> cards) → Card[]` (exactly 5). 5 in → same 5 out.
  Throws for `< 5` or `> 7`, same as `Evaluate`.
- `PokerHand.WinningCardsOf(int handIndex) → IReadOnlyList<Card>`; populated in `Showdown()` for
  each winner from `[..HoleCards[seat], ..BoardCards]`; `[]` otherwise.
- `PokerSeatEntry.WinningCards` (`IReadOnlyList<byte>`, default `[]`), documented; converter
  writes/reads it after `HoleCards` with the same `CountToByte` guard.
- `PokerTableScript.BuildRosterFor`: `WinningCards = hand is { IsComplete: true } && handIndex >= 0
  ? hand.WinningCardsOf(handIndex).Select(c => (byte)c.ToIndex()).ToList() : []`. Nothing else
  changes — `WinningCardsOf` is already empty for non-winners and fold wins.
- `InterHandPause` → 8000 ms.

## Client

- `PokerSeatInfo.WinningCards` (`IReadOnlyList<byte>`), copied in `ApplySnapshot`.
- `CardView` gains `Emphasis` (`Plain | Winning | Dimmed`), set alongside `ShowFace`. `Draw`
  finishes by painting a 2 px gold frame (`WinnerSeatColor`) for `Winning`, or a
  `Color.Black * 0.55f` overlay for `Dimmed`. `ShowBack`/`ShowNothing` reset it to `Plain`.
- `OnSnapshot` builds `winning = union of seat.WinningCards over vm.Seats`. If empty, every card
  is `Plain`. Otherwise each face-up board card is `Winning` if its index is in the set, else
  `Dimmed`; `SeatPanel.Apply` receives the set and does the same for its face-up hole cards.
- No timer: the emphasis is snapshot state and clears when the next hand's first snapshot arrives
  with no winning cards.

## Testing

- `HandEvaluatorTests`: `BestFive` returns the five that make a full house out of seven; picks the
  five highest of a six-card flush; and — sampled over 500 seeded random seven-card hands —
  `Evaluate(BestFive(cards)) == Evaluate(cards)`.
- `PokerHandTests`: a showdown winner's `WinningCardsOf` has 5 cards drawn from their hole cards +
  board and evaluates to `WinningCategory`; a non-winner's is empty; a fold win leaves all empty.
- `PokerPacketConverterTests`: full-roster round-trip carries `WinningCards` for one seat and `[]`
  for the rest; the null-collections test round-trips a null list as empty.
- Client: build + live (walkthrough Run I): at showdown the five winning cards are framed, the
  others dimmed; on a split both plaques' hole cards frame; a fold win frames nothing; the next
  deal clears it; the reveal now lasts ~8 s.
