# Poker win reveal: show why, and slide the pot to the winner

**Date:** 2026-08-28
**Scope:** `Chaos-Server` (`feature/poker-table`) and `Chaos.Client`
**Builds on:** `docs/superpowers/specs/2026-08-27-poker-table-design.md`

## Problem

When a hand ends, the panel says "Mike wins 24,000 gold." and nothing else. Players cannot see
*why* Mike won — the revealed hole cards are on screen, but reading five-card poker hands at a
glance is exactly what the table should be doing for them. And the pot just vanishes: the number
under the board goes to zero on the next hand with no motion toward the seat that took it.

The wire does not say who won (only the event text names them) and the hand keeps no record of
the winning rank. Both features need the server to say so.

## Decision

The server puts two facts on the completion snapshot — which seats won, and the winning hand's
category (or "won by fold") — and the client renders both: a two-line result banner under the
pot, and the existing coin animation run in reverse from the pot to each winner's plaque.

Detail level is **category only**: "Full House", "Two Pair", "Flush"… or "everyone else folded".
Not "Two Pair, Aces and Kings" (needs the tiebreak decoded per category) and not the winning five
cards highlighted (needs the evaluator to report which cards it used). Either can be added later
behind the same fields.

Rejected: deriving it on the client (parse names out of the event text, re-evaluate revealed
hole cards). Breaks on split pots and needs `HandEvaluator` moved into a shared project.

## Server

### `PokerHand`

- New `HandCategory? WinningCategory { get; private set; }`.
- `Showdown()` already tracks the best `HandRank`; it records `best.Category` into
  `WinningCategory` before returning the winners.
- A hand settled by `Forfeit` (everyone else folded) leaves it `null`.
- Reset is unnecessary: a `PokerHand` is single-use.

### Wire: `PokerTableDisplayArgs` (Snapshot)

Two new fields, after `EventText`:

| Field | Type | Meaning |
|---|---|---|
| `WinnerSeats` | `List<byte>` | Seat indices (seat space, not hand space) paid by the hand that just completed. Empty while a hand is running or no hand exists. |
| `WinningHand` | `byte` | `HandCategory` value (1–9) when the hand reached showdown; `0` when it was won by everyone else folding, or while no hand has completed. |

`PokerTableDisplayConverter` writes `count` + seat bytes, then the category byte; reads them
back the same way. `CountToByte` guards the list as it does the others. XML docs on the record
follow the existing parity rule (every field documented).

### `PokerTableScript.BroadcastSnapshot`

Fills both only when `hand is { IsComplete: true }`:

- `WinnerSeats = hand.Winners.Select(SeatOfHandIndex).Where(seat => seat >= 0).Select(ToWireSeat)`
- `WinningHand = (byte)(hand.WinningCategory ?? 0)`

Otherwise `[]` and `0`. `BuildResultText` is unchanged — the event text still says who won and
how much; the category is a separate fact for the client to place.

## Client

### View model `PokerTable`

`ApplySnapshot` copies `WinnerSeats` (as `IReadOnlyList<int>`) and `WinningHand` (as `byte`).
`Clear()` resets both. A completed hand is recognised by **`WinnerSeats.Count > 0`** — never by
parsing `EventText`.

### Result banner (the "why")

The existing centered `EventLabel` under the pot box grows a second line for the reveal window:

- Line 1 (existing): `EventText` — "Mike wins 24,000 gold." / "Mike and Ann split the pot, …".
- Line 2 (new, gold foreground): the category name from a client-side table keyed by
  `WinningHand` — `1 → "High Card"`, `2 → "One Pair"`, `3 → "Two Pair"`, `4 → "Three of a Kind"`,
  `5 → "Straight"`, `6 → "Flush"`, `7 → "Full House"`, `8 → "Four of a Kind"`,
  `9 → "Straight Flush"`; `0 → "everyone else folded"`.

It stays under the pot rather than over the board so the revealed cards remain readable. It is
painted from the snapshot like everything else and is cleared by the next hand's first snapshot
(`WinnerSeats` empty again), so it needs no timer of its own — the server's 4 s settle pause is
what keeps it on screen.

### Pot slide (the animation)

On a snapshot where `WinnerSeats` is non-empty and the previous snapshot's was empty (the same
"changed since last snapshot" discipline `DetectActionAnimations` already uses), for each winner:

- Spawn 8 `ChipSlide`s from the pot box centre to the winner's plaque centre, staggered ~60 ms
  apart, using the existing `CoinTexture`, duration and easing. `SpawnChipSlide` already takes
  `from` and `to`; it is called with the arguments reversed — no new animation type.
- Flash the winner's plaque border gold for the reveal window, using the seat panel's existing
  border-highlight mechanism (the one that marks the acting seat), in the gold colour instead.

Split pots animate to every winner. `ClearAnimations()` (already run on `Hide()` and on a new
hand) retires anything still in flight. Nothing here feeds back into state; it is presentation
diffed from snapshots, as the rest of the panel's animation is.

Not included: sound, a counting-down pot label, a counting-up gold label on the plaque (other
seats' gold is deliberately withheld from the viewer, so there is nothing to count up).

## Testing

- `PokerHandTests`: a showdown win records the winning category; a hand won by forfeit records
  `null`. Both watched to fail first.
- `PokerPacketConverterTests`: the full-roster snapshot round-trip sets `WinnerSeats` and
  `WinningHand`; watched to fail before the converter carries them. The null-collections test
  covers `WinnerSeats == null` round-tripping as empty.
- Client: no test project. Verified by build and by the two live clients — the walkthrough
  gains **Run H**: play one hand to showdown and one to a fold, check the second line reads the
  category / "everyone else folded", and that coins travel from the pot to the winner's plaque
  (both plaques on a split pot).
