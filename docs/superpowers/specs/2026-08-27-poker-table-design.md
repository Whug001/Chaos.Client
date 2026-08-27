# Rucesion Casino — Fixed-Limit Hold'em Table

Date: 2026-08-27

## Summary

Add a six-seat fixed-limit Texas Hold'em cash table to the Rucesion casino floor. Players wager
against each other; the house takes a capped percentage of each contested pot and destroys it. The
table is driven entirely by a new client popup control over push packets — no dialogs — following
the rail already established by the slot machines and the Gilded Spindle.

The existing twenty-one tables are left completely untouched. This is additive.

## Why this shape

The casino's current games are all "player versus RNG" (slots, the Gilded Spindle), a scheduled
spectacle (monster racing), or a passive draw (the lottery). Twenty-one was meant to be the social
table and is not: every player rolls privately and scores are compared only at the end, so sitting
next to someone changes nothing.

Poker fills that gap, and it does so with an economic profile nothing else in the building has:
**the house takes no risk**. There is no RTP to tune, no house variance, and no progressive
exposure. The rake is pure margin and a pure gold sink.

## Goals

- A table where the other players are the game.
- Playable heads-up, so it is not dead content on a quiet night.
- Drop in and out between any two hands, with nothing to reconcile.
- A capped, predictable gold sink.
- No dialogs anywhere in the flow.

## Non-goals

- **Twenty-one is not touched.** Its two merchant scripts, its dialog script, its four
  `twentyonetable_*` dialog templates, `CasinoTwentyOneCleanupMapScript`, both merchant templates,
  and the five twenty-one fields on `Aisling` all remain exactly as they are. Retiring or repairing
  that game is a separate decision.
- No no-limit or pot-limit betting.
- No tournaments or sit-and-gos.
- No chip balances, buy-in stacks, or cash-out flow (see Money model).
- No bad-beat jackpot in v1.
- No second table in v1.

## Game rules

Six seats. Standard Texas Hold'em: two hole cards each, a three-card flop, a turn, a river, four
betting rounds, best five-card hand from seven.

**Stakes: 2,000 / 4,000.** Small blind 1,000, big blind 2,000.

| Street  | Bet / raise increment |
| ------- | --------------------- |
| Preflop | 2,000 (small bet)     |
| Flop    | 2,000 (small bet)     |
| Turn    | 4,000 (big bet)       |
| River   | 4,000 (big bet)       |

Four bets maximum per street — bet, raise, re-raise, cap. The button rotates one seat clockwise per
hand among seated, active players.

**Heads-up blind rules** (they differ from ring play and are a common source of bugs): with exactly
two players, the button posts the small blind and acts **first** preflop, and acts **last** on every
later street.

## Money model — no chips, no stacks, no side pots

Players bet **directly out of gold**. Wagers are escrowed into the pot as they are made; the pot is
awarded at showdown.

This is viable only because fixed limit bounds a hand's maximum exposure exactly:

```
    4 × 2,000   (preflop)
  + 4 × 2,000   (flop)
  + 4 × 4,000   (turn)
  + 4 × 4,000   (river)
  = 48,000 gold  =  24 small bets
```

**A player must hold 48,000 gold to be dealt in.** This is checked immediately before cards are
dealt each hand; a player who cannot cover it is sat out, not removed, and is told why.

Three consequences, and they are the entire reason for this design:

1. **All-ins cannot happen, so side pots cannot exist.** That removes the single largest and
   buggiest subsystem of a poker engine.
2. **It is crash-safe by construction.** Gold is only ever in a player's gold field or in the
   current hand's pot. There is no persisted chip balance that must survive a restart, and no
   cash-out step that can half-complete.
3. **Standing up is free.** Between hands there is nothing to settle.

The 48,000 requirement is a minimum buy-in, which every real cardroom enforces anyway.

## Rake

**5% of the pot, capped at 12,000 gold (3 big bets), and nothing is raked from a hand that ends
before the flop** — "no flop, no drop".

The rake is **destroyed**, not redistributed. It is the gold sink, and that is the only reason the
server should want this table to exist. It is deducted from the pot at award time and logged via
`logger.WithTopics(Topics.Entities.Gold)`, matching how the existing casino scripts log gold
movement.

Order of operations at award time is fixed: **rake first, then split.** When a raked pot does not
divide evenly among tied winners, the odd gold goes to the first tied winner clockwise from the
button — the standard cardroom rule, and stated here so it is not decided arbitrarily at
implementation time.

## Table lifecycle

- **Sitting down.** Walking onto a seat reactor tile claims that seat and opens the popup. The seat
  reactor follows `SlotMachineStoolScript`, which releases its claim from `Update` by checking each
  tick whether the claiming aisling is still on the tile. It does **not** follow
  `CasinoLockTwentyOneScript`, which never releases its own lock and relies on a dialog to clear a
  flag.
- **Starting a hand.** A hand deals when two or more players are **eligible**, meaning: holding a
  seat, not sitting out, and carrying at least 48,000 gold. When exactly one player is eligible and
  waiting, the table announces itself to the casino map so a second player has a reason to come
  over.
- **Shot clock.** 20 seconds per action. Timing out folds. Two consecutive timeouts sit the player
  out. Sitting out for three hands releases the seat.
- **Leaving.** Standing up between hands is immediate. Standing up mid-hand folds at your turn and
  forfeits gold already in the pot.
- **State ownership.** All seat and hand state lives in the table's own objects. **Nothing is stored
  on `Aisling`**, so there is no state to leak out of the casino and no map-exit cleanup script is
  required. The movement lock derives from "standing on a poker seat reactor **and** the table
  reports you hold that seat".

## Architecture

Server, in `Chaos-Server/Chaos/`:

| Component          | Path                                                            | Responsibility                                                                  |
| ------------------ | --------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| `HandEvaluator`    | `Services/Poker/HandEvaluator.cs`                                 | Pure function: 7 cards in, ranked hand out. No dependencies.                     |
| `Deck`             | `Services/Poker/Deck.cs`                                          | Server-side shuffle and deal.                                                    |
| `BettingRound`     | `Services/Poker/BettingRound.cs`                                  | One street's state machine: whose turn, legal actions, bet cap, round-complete.  |
| `PokerHand`        | `Services/Poker/PokerHand.cs`                                     | One hand start to finish: blinds, streets, pot, showdown, rake, award.           |
| `PokerTable`       | `Services/Poker/PokerTable.cs`                                    | Seats, button rotation, funding checks, sit-out, hand scheduling.                |
| `PokerTableScript` | `Scripting/MerchantScripts/Casino/PokerTableScript.cs`            | Drives `PokerTable` from `Update`; owns packet broadcast.                        |
| `PokerSeatScript`  | `Scripting/ReactorTileScripts/Temauir/Casino/PokerSeatScript.cs`  | Seat claim and release.                                                          |

Each of these is independently testable and none reaches past its neighbour: `HandEvaluator` knows
nothing about tables, `BettingRound` knows nothing about gold, and `PokerHand` is the only thing
that moves gold.

Packets, alongside the slot equivalents:

- `Chaos-Server/Chaos.Networking/Entities/Server/PokerTableDisplayArgs.cs` + `PokerDisplayType`
- `Chaos-Server/Chaos.Networking/Entities/Client/PokerTableInteractionArgs.cs` + `PokerInteractionType`

**The protocol is snapshot-based, not delta-based.** Every change broadcasts a complete
authoritative table snapshot, built per recipient, plus a small event payload describing what just
happened for animation and the action log. Deltas desync; snapshots cannot.

`PokerDisplayType`: `Open`, `Snapshot`, `Rejected`, `Close`.
`PokerInteractionType`: `Sit`, `Leave`, `SitOut`, `SitIn`, `Act` (with fold / check / call / bet /
raise).

Client, in `Chaos.Client/`:

| Change               | Path                                              |
| -------------------- | ------------------------------------------------- |
| Popup control        | `Controls/World/Popups/Poker/PokerTableControl.cs` |
| View model           | `ViewModel/PokerTable.cs`                          |
| State registration   | `Collections/WorldState.cs`                        |
| Packet dispatch      | `Screens/WorldScreen.ServerHandlers.cs`            |
| Event wiring         | `Screens/WorldScreen.Wiring.cs`                    |
| Control construction | `Screens/WorldScreen.cs`                           |
| Send methods         | `Chaos.Client.Networking/ConnectionManager.cs`     |

The control renders six seats, the community board, the pot, your own hole cards, the legal action
buttons for your turn, the shot clock, and each player's most recent action. Wiring mirrors
`Slots.SpinRequested += () => Game.Connection.SendSlotSpin();`.

## Data and configuration

In the `Unora` repository:

- `Data/LocalStorage/PokerTableCatalog.json` — stakes, seat count, rake percentage and cap,
  shot-clock duration, sit-out thresholds. Validated at startup, in the spirit of
  `SlotConfigValidator`. The validator must assert that the configured maximum exposure
  (`4 × (2 × smallBet + 2 × bigBet)`) matches the enforced minimum buy-in, because the
  no-side-pots guarantee depends on those two numbers agreeing.
- `Data/Configuration/Templates/Merchants/Temauir/pokerTable.json` — merchant template.
- `…/Rucesion_Casino/merchants.json` — one new spawn point, at a new floor location. The twenty-one
  spawns at `(9, 23)` and `(14, 23)` are left in place.
- `…/Rucesion_Casino/reactors.json` — six seat tiles with `scriptKeys: ["PokerSeat"]`, matching the
  existing `{ scriptKeys, scriptVars, source }` shape.

## Security

Two requirements, both of which are unrecoverable if shipped wrong even once:

1. **Hole cards are sent only to their owner.** The snapshot is built per recipient with every other
   player's hole cards omitted until showdown. The natural implementation — broadcasting one shared
   snapshot to all seats — hands a modified client the whole table. This is why the protocol is
   specified as per-recipient rather than broadcast.
2. **The shuffle is server-side and seeded from nothing the client can observe.** Card order is
   never derived from anything a player can see, influence, or time.

Every hand is logged in full — hole cards, board, actions, pot, rake, winner — so collusion between
players can be reviewed after the fact. Collusion cannot be prevented in a game with a chat box;
fixed-limit betting caps what it is worth, and logging makes it detectable.

## Failure handling

| Situation                                 | Behaviour                                                                                                                                       |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| Player disconnects mid-hand               | Folds at their turn; gold already in the pot is forfeit. Always a legal action, so not exploitable.                                              |
| Player times out                          | Fold. Two in a row sits them out; three hands sat out releases the seat.                                                                          |
| Player cannot cover 48,000 at deal time   | Sat out with a message, not removed.                                                                                                              |
| Server restarts mid-hand                  | The hand is abandoned. Gold in the pot at that moment is lost — an accepted, documented consequence of holding escrow in memory. Bounded at 48,000 per player. |
| Seat reactor and table disagree on a seat | The table is authoritative; the reactor's `Update` reconciles.                                                                                    |

## Testing

Test-first throughout. The natural order:

**1. `HandEvaluator`** — the most testable component in the codebase. Unit tests for every category
and for the ranking edges that break naive implementations: wheel straights (A-2-3-4-5), a flush
that also contains a straight, playing the board, kicker comparisons, and exact ties. Then an
exhaustive enumeration over all `C(52,7) = 133,784,560` seven-card combinations, asserting the
published category frequencies:

| Category        | Count      |
| --------------- | ---------- |
| Straight flush  | 41,584     |
| Four of a kind  | 224,848    |
| Full house      | 3,473,184  |
| Flush           | 4,047,644  |
| Straight        | 6,180,020  |
| Three of a kind | 6,461,620  |
| Two pair        | 31,433,400 |
| One pair        | 58,627,800 |
| High card       | 23,294,460 |

These must sum to 133,784,560 — the sum itself is part of the assertion. This test is slow and
belongs in a nightly or explicitly-invoked suite, not the default run.

**2. `BettingRound`** — turn order, the four-bet cap, check/call/raise legality per street, round
completion, and heads-up ordering (button first preflop, last thereafter).

**3. `PokerHand`** — pot accounting, rake percentage and cap, "no flop, no drop", split pots on
exact ties, and the invariant that **gold in equals gold out plus rake**, asserted on every hand.

**4. `PokerTable`** — button rotation across sit-downs and stand-ups, funding checks, sit-out and
seat-release thresholds, and heads-up play.

## Deferred

- Bad-beat progressive jackpot over the existing `ProgressivePot` / `IProgressivePotState`.
- A second table at different stakes, once the first proves it draws a crowd.
- Retiring or repairing twenty-one, including the three bugs found while surveying it: integer
  division in the winnings split (`MerchantScripts/Casino/TwentyOneScript.cs:83`), a guaranteed loss
  for a solo non-busting player, and both tables gathering players from the whole map instance
  rather than their own seats.
