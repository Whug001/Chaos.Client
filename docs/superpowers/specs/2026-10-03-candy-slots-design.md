# Jack's Candy Reels: a candy slot machine for the haunted theatre

Date: 2026-10-03. Repos: Chaos-Server, Chaos.Client, Unora.

## Goal

Two pumpkin-themed slot machines in the Halloween theatre (`suomi_theatre_halloween`). They work like the casino
slots, but the bet and the winnings are Halloween Candy (`halloweencandy`), never gold, and there is no jackpot.

## How it plays

- Name: **Jack's Candy Reels**. Machines at (16,3) and (18,3); stools at (16,4) and (18,4). Stepping on a stool sits
  the player down and opens the window, as in the casino.
- Bet: 1 Halloween Candy per spin, taken from the inventory. With none, the spin is refused: "You need 1 candy to
  play here."
- Winnings: Halloween Candy, given with `GiveItemOrSendToBank` (to the bank when the inventory is full).
- No jackpot. Three jack-o'-lanterns is the top prize at a fixed multiplier.
- Payback: the reel strips are tuned until `SlotRtpCalculator` reports a base payback of 0.89 to 0.91.
- Symbols (7):

  | Index | Symbol | Tier |
  |---|---|---|
  | 0 | Candy Corn | Common |
  | 1 | Wrapped Candy | Common |
  | 2 | Lollipop | Common |
  | 3 | Bat | Mid |
  | 4 | Ghost | Mid |
  | 5 | Skull | Rare |
  | 6 | Jack-o'-lantern | Rare (top symbol; there is no Jackpot tier on this machine) |

- Paytable shape. The multipliers are starting points; tuning may move them, but the order stays:

  | Rule | Kind | Multiplier |
  |---|---|---|
  | Three jack-o'-lanterns | ThreeOfSymbol 6 | about 50 |
  | Three skulls | ThreeOfSymbol 5 | about 20 |
  | Three ghosts | ThreeOfSymbol 4 | about 10 |
  | Three bats | ThreeOfSymbol 3 | about 10 |
  | Three lollipops / wrapped candies / candy corn | ThreeOfSymbol 2, 1, 0 | about 4 |
  | Any three candies | ThreeOfTier Common | 2 |
  | Two matching | TwoMatchingLeft | 1 |

- Unchanged from the casino: 2.5 s spin cooldown, claim and sit-down rules, Recent Spins log, auto spin.
- Out of scope: legend marks, daily limits, server-wide win announcements, a bet selector.

## Server (Chaos-Server)

1. `SlotTierConfig` gets two optional properties:
   - `CurrencyItemKey` (string, null = gold).
   - `HasJackpot` (bool, default true).

   The three gold machines in the catalog do not change.
2. `SlotConfigValidator`:
   - `HasJackpot == false` requires zero Jackpot-tier symbols, `JackpotContributionRate == 0`,
     `SeedReserveRate == 0` and no paytable row with `IsJackpot`. The seed-sustainability check is skipped (there
     is no pot).
   - `HasJackpot == true` keeps today's rules (exactly one Jackpot-tier symbol, and so on).
   - `CurrencyItemKey` set: the largest possible payout (`Bet` × largest multiplier) must not exceed the item's
     `maxStacks`. The validator gets the item's stack limit from the caller, so it stays a pure function.
3. `SlotMachineScript.Spin` branches only where money moves. The gold path is byte-for-byte the same behavior.
   - Bet: `Inventory.HasCountByTemplateKey` then `RemoveQuantityByTemplateKey`; the result of the remove is checked,
     as `TryTakeGold`'s is today. Refusal reason `InsufficientCurrency`.
   - Jackpot: when `HasJackpot` is false, `JackpotService.Contribute`, `TryClaim`, `RecordReturn` and the pot value are
     not used. `jackpotAmount` is 0.
   - Payout: `ItemFactory.Create(CurrencyItemKey)` with `Count = payout`, then `GiveItemOrSendToBank`. No pending
     payout ledger and no hold-notice mail; those exist only for the gold cap.
   - Mid-spin failure refund: one candy given back the same way.
   - `DrainPendingPayout` is only called for gold machines.
   - `Ledger.Record` (the casino gold net) is skipped for item machines. The audit log line names the currency.
   - `PersistAisling` after every settled spin stays.
   - `ItemFactory` is a new constructor dependency.
4. `SlotRejectReason.InsufficientCurrency` added (append-only enum value).
5. `SlotMachineDisplayArgs` (Open) gains `CurrencyName` (string, empty for gold), `CurrencyItemName` (string, empty
   for gold) and `HasJackpot` (bool). `SlotMachineDisplayConverter` writes them at the end of the Open payload.
   `SendSlotMachineOpen` and `SlotMachineStoolScript` pass them. For candy: "candy", "Halloween Candy", false.
6. `WorldServer.OnSlotMachineInteraction` only shouts and broadcasts a jackpot when the machine has one.
7. `CLIENT_VERSION` +1.

Tests (`Tests/Chaos.Tests/Slots`, `Networking`):
- candy bet taken;
- payout into the inventory, and into the bank when the inventory is full;
- refusal with no candy;
- no jackpot service calls for a no-jackpot machine;
- validator accepts a valid no-jackpot machine and rejects a no-jackpot machine with a Jackpot symbol or a nonzero
  rate;
- Open packet round-trip with the new fields;
- the real catalog's `jacksCandyReels` validates with a base payback of 0.89 to 0.91.

All existing gold tests pass unchanged. The two known failures on master (GiveAbility, OnItemDroppedOn stackable)
are not ours.

## Client (Chaos.Client)

Window look: unchanged casino window. Only the text and the symbols differ.

- `SlotMachine` view model stores `CurrencyName`, `CurrencyItemName`, `HasJackpot`.
- When `CurrencyName` is set:
  - the top bar shows `"{CurrencyItemName}: {count}"`, where the count is the sum of that item's stack counts in the
    inventory. It refreshes on inventory changes;
  - the footer reads `"Bet: {bet} {CurrencyName}"`;
  - the gold label is hidden;
  - refusal and auto-spin texts use the currency name ("Not enough candy to spin.", "Out of candy. Auto spin
    stopped.", "You need 1 candy to play here.").
- When `HasJackpot` is false: no jackpot count-up, no "just won the jackpot" message, no "JACKPOT" row label.
- Win sounds unchanged.
- Server submodule pointer and `CLIENT_VERSION` bumped to match.

## Art and data (Unora)

- Sources in `Tools/CandySlots/art/`:
  - cabinet: PixelLab image job `bbe6bb56-5b40-468e-a508-13b995ca1360`, 80×100;
  - symbols: PixelLab object `3bf8ce9b-9f04-4ec6-8ee0-43c46e08fe71` frames 0–6, 40×40.
- `Tools/CandySlots/make_candy_slots.py`:
  - cleans the symbols;
  - builds the cabinet's idle frames (face glow pulse and reel glint, derived from the one image, as
    `generate_slot_idle.py` did);
  - writes frame JSON in the PumpkinCarving format.
- `Tools/CandySlots/pack_candy_slots.cs`:
  - patches the eight monster sprites (cabinet + 7 symbols) and their palettes into a copy of `hades.dat` in a
    Desktop folder, then re-reads it and renders check PNGs;
  - refuses a sprite or palette number that is already taken;
  - never writes the live client folder.

  Its input is the `hades.dat` in `Desktop/Unora Release 2026-10-03`, so the carving pumpkin is kept. Sprite numbers
  are picked at pack time (1474+ free on 2026-10-03).
- `Templates/Merchants/Temauir/Events/Halloween/slotMachineCandy.json`:
  - name "Jack's Candy Reels";
  - script `slotMachine` with `machineKey: jacksCandyReels`;
  - sprite = the new cabinet.
- `Suomi_Theatre_Halloween/merchants.json`: two `slotmachinecandy` at (16,3) and (18,3), facing Down.
- `Suomi_Theatre_Halloween/reactors.json`: `SlotMachineStool` at (16,4) and (18,4).
- `Data/LocalStorage/SlotMachineCatalog.json`: `jacksCandyReels` entry (bet 1, `currencyItemKey: halloweencandy`,
  `hasJackpot: false`, rates 0).

## Shipping

1. One launcher patch with the new `hades.dat` and the new client build.
2. Then the server restart.

Work happens on `feat/candy-slots` worktrees in all three repos. Merges are local only, and nothing is pushed
without the user's go-ahead.
