# Poker: gold outline around the action row on your turn

**Date:** 2026-08-28
**Scope:** `Chaos.Client` only (`PokerTableControl`)
**Builds on:** `docs/superpowers/specs/2026-08-27-poker-table-design.md`, `2026-08-28-poker-win-reveal-design.md`

## Problem

The only cue that it is the local player's turn is a sound and the acting outline on their own
plaque. The thing they must actually use — the Fold / Check / Call / Bet / Raise row below the
felt — does not change at all.

## Decision

A `TurnOutline` panel: the same gold `BuildBorder` the winner's plaque wears, 2 px thick, drawn
around the whole action row with 4 px of breathing room (`ACTION_ROW_LEFT − 4`,
`ACTION_ROW_TOP − 4`, row width + 8, `CustomButton.HEIGHT` + 8). Not hit-testable.

Visibility is set in `OnSnapshot` from the **same** `yourTurn` predicate that fires the your-turn
sound (`actorIndex == YourSeatIndex`), so the outline and the sound can never disagree, and it is
cleared in `Hide()` with the rest of the panel's transient state.

Whole row, static (no pulse), so greyed-out illegal buttons still read as part of one prompt.
Rejected: per-button borders (five small boxes, busier) and a pulsing border (adds a per-frame
update for no information the static border does not carry).

## Testing

No client test project. Build, then on the live clients: the outline appears exactly when the
your-turn sound plays, and disappears on the next snapshot after acting or folding.
