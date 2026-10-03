# Haunted Theatre Vendors — design

Date: 2026-10-03. Status: approved in chat, awaiting spec review.

Three spooky vendors stand beside fair stalls in the haunted Suomi theatre (`suomi_theatre_halloween`). They
sell for Halloween candy (`halloweencandy`):

| Stall art | Vendor (sprite) | Sells |
|---|---|---|
| `stall_popcorn.png` | Crypt Reaper (68) | three Halloween treat recipes |
| `stall_facepaint.png` | Dark Spirit (1250) | the Macabre Box accessories |
| `stall_fortune.png` | Mummy (188) | the Macabre Box overcoats |

The vendors exist only in the haunted theatre, so they come and go with the Halloween event period (Oct 4 - Nov 4).
Learned recipes and cooked treats keep working after the event.

## 1. Treats and recipes

Three new cooking recipes in the normal cooking system. They are cooked at any cooking pot, like every other meal.

| Treat | Template key | Ingredients | Buff | Lasts |
|---|---|---|---|---|
| Popcorn | `popcorn` | 15 `acorn` + 1 `salt` | +5% experience on kills | 30 min |
| Cotton Candy | `cottoncandy` | 2 `sugar` + 1 `cotton` | +3% mana regeneration | 30 min |
| Hot Dog | `hotdog` | 2 `rawmeat` + 1 `flour` | +6% health regeneration | 30 min |

- The buffs copy the meals they mirror: Acorn Mash (exp), Cucumber Cooler (mana regen) and Hearthside Porridge
  (health regen). The treats join `UtilityMealConflicts`, so eating one replaces any other utility meal, and the
  other way round (user's choice: "same slot as meals").
- Failure chance 30%, like Garden Medley.
- Wherever the server reads a mirrored meal's buff by name (the experience bonus for Acorn Mash, `DefaultRegenFormula`
  for the regen meals), it also reads the matching treat. Each gets a constant in `UtilityMealBuffs`.
- Recipe scrolls `recipe_popcorn`, `recipe_cottoncandy` and `recipe_hotdog` (category Recipe, `recipeItem` script,
  panel sprite 1280) teach the recipe through a new `BigCookingRecipes` flag each. They are added in the same places
  as Garden Medley: `BigFlags`, `CookingRequirements`, `RecipeItemScript.Cooking`, `RecipeLibraryScript.Cooking`.
- Meal templates go in `Templates/Items/crafting/Cooking/Meals/` with `VitalityConsumable` (effect keys `Popcorn`,
  `CottonCandy`, `HotDog`) and descriptions in the style of the existing meals.
- Effect scripts `PopcornEffect`, `CottonCandyEffect` and `HotDogEffect` go in `EffectScripts/Items/CookingMeals/`,
  modeled on the mirrored meals. Each has its own orange bar line, under 45 characters.

## 2. Candy shop

One new dialog script, `CandyShopScript`, serves all three vendors. It is a buy-only shop that charges candy.

- **Price list:** each vendor's merchant template carries its items and prices in `scriptVars` under `candyShop`,
  as `{ "items": { "<templateKey>": <price>, ... } }`. The script reads the list from the merchant that opened the
  dialog. Changing a price is a data edit.
- **Flow:** the same four steps as the token shops: item list, amount, confirmation, done. The confirmation says the
  candy total, for example "That will be 300 candy."
- **Checks, in this order:** the item is in this vendor's list; the amount is 1 or more; the player can carry the
  items; the player has enough candy. Then candy is removed, and only after that succeeds are the items given.
  A failure at any step changes nothing.
- The existing token shop scripts stay as they are.

### Prices

Recipes, at the popcorn stall: 25 candy each.

Accessories, at the face paint stall (19 items):

| Price | Items |
|---|---|
| 500 | `fiendmask`, `abominationmask`, `spectremask`, `dyeablecatears`, `gasmask`, `witchbuddy`, `reaperbuddy` |
| 300 | `undeadhand`, `ghosteffect`, `catbowtail`, `zombimask`, `frankensteinmask`, `skullmask`, `demonhorns`, `demontail`, `eingrenghost`, `dungcap`, `cyclopseye` |
| 150 | `swampwitchpet` |

Overcoats, at the fortune stall (9 items):

| Price | Items |
|---|---|
| 300 | `macabrehexeddress`, `macabrehexedrobes`, `fshadowcloak`, `mshadowcloak`, `macabredivinerobe`, `macabredivinegown`, `macabrebattlearmor` |
| 150 | `fpumpkincostume`, `mpumpkincostume` |

Tiers follow the Macabre Box weights: 0.03-0.04 is 500, 0.05-0.10 is 300, 0.15 and up is 150. The box's hats and
boots stay box-only. For scale, a box costs 100 candy, and a specific rare mask from the box costs about 14,500 candy
on average.

## 3. Vendors

Three new merchant templates in `Templates/Merchants/Temauir/Events/Halloween/`: `stall_reaper` (Crypt Reaper,
sprite 68), `stall_spirit` (Dark Spirit, sprite 1250) and `stall_mummy` (Mummy, sprite 188). Each has `showdialog`
pointing at its own `<key>_initial` dialog with a short spooky greeting and one "Browse wares" option into the candy
shop. They do not wander. Dialog text follows the limits in CLAUDE.md (35-character options, 4 lines, no em dashes).

They are added to `Suomi_Theatre_Halloween/merchants.json` by hand. `decorate_theatre.py` is not rerun, because it
overwrites that file and the hand-edited map.

## 4. Stalls on the map

### Placement (layout C)

Each stall covers a 2x2 block of cells. Its anchor `(x, y)` is the front-left cell. The block is `(x, y)`,
`(x+1, y)`, `(x+1, y-1)` and `(x, y-1)`.

| Stall | Anchor | Vendor stands at |
|---|---|---|
| Popcorn | (13, 6) | (13, 7) |
| Face paint | (17, 10) | (17, 11) |
| Fortune | (17, 24) | (17, 25) |

Vendors stand beside the stall's front-left counter. A tile behind the counter does not work: the stall art hides
the sprite completely (checked in a mockup). The positions may move by a step after the in-game check.

### Tiles

- Each stall is cut into four 28 px wall strips, the "Prop2" layout that CamPrep used for the five stalls in
  `Desktop/Halloween/Stalls/cam/`: `x0_y0_L` on `(x, y)` left, `x1_y0_L` and `x1_y0_R` on `(x+1, y)`, and `x1_ym1_R`
  on `(x+1, y-1)` right. The back cell `(x, y-1)` gets an invisible blocker (foreground 1, as in the mirror maze).
- CamPrep's slicing engine (`Tools/Maps/HalloweenFair/Painter`) is not in the repo. A new script,
  `Tools/HauntedTheatre/stall_strips.py`, does the cut. Its test: cutting `stall_fortune.png` must reproduce the four
  existing `cam/stall_fortune_*` strips pixel for pixel. The popcorn and face paint stalls are then cut the same way.
- 12 new foreground tiles, `stc09132.hpf` to `stc09143.hpf`, follow the Endless Tower strips (9082-9131, already on
  `origin/master`). All 12 are walls in `sotp.dat`: flag 15, as the tower strips use.
- The strips go into `ia.dat` with the same import code TowerArt uses. That code is reused, not copied, if it can be
  called on its own.
- `Tools/HauntedTheatre/place_stalls.py` writes the strip ids and blockers into the existing `lod10269.map`. It touches
  only the 12 stall cells and refuses if any of them is not plain open floor. It reuses `decorate_theatre.py`'s
  reachability check, so Thulin, both town doors, the mirror door, the pumpkin claim tiles, the display pumpkin, the
  ghost spawns and the three vendor tiles must all still reach each other.

### Inventory icons

Three new hand-drawn icons (popcorn, cotton candy, hot dog). They get icon numbers and go into `Legend.dat` through
`Tools/Accessories` (`ids.py`, `deploy_batch.py`), so the registry knows them. The numbers are given out at build time.

## 5. Release

- Desktop folder `Theatre Vendors 2026-10-03` with `ia.dat`, `Legend.dat` and a `DEPLOY.txt`.
- `ia.dat` is built from the local client folder copy. That copy already holds the Endless Tower strips 9082-9131.
  The tower's `sotp.dat` flags are already on `origin/master`, so shipping those strips adds art that no shipped map
  uses yet. Building from the live copy instead would mean two `ia.dat` versions that drift apart.
- The new `sotp.dat` ships in the server build. The `ia.dat` and `Legend.dat` ship in one launcher patch with that
  server build.
- Server, Unora and client work go on `feat/theatre-vendors` worktrees, following the shared-checkout rules.

## 6. Testing

- **Server (TUnit):**
  - `CandyShopScript`: buys and charges the listed price × amount; refuses with too little candy; refuses when the
    items do not fit and keeps the candy; refuses an item not on the list; refuses an amount of 0 or less.
  - Each recipe scroll sets its flag once and is used up.
  - Each treat applies its effect and replaces another utility meal.
  - The exp and regen bonuses count the treats.
- **Data:** a test loads the three merchant templates and checks every listed item key exists and every price is
  above 0.
- **Tools:** the strip-cut test above. `place_stalls.py --check` passes the reachability check.
- **In game, with the new dats in the local client folder:**
  - The stalls draw and block.
  - The vendors are visible and clickable.
  - Each shop buys an item and charges candy.
  - A recipe is learned, the treat is cooked and eaten, and its buff shows.

## 7. Out of scope

- Hats, boots and the items outside the Macabre Box (ghost sheet, Halloween cape, Ancient Dubhaim) are not sold.
- The other 17 stall pictures are not used.
- No change to the Macabre Box, Thulin's candy trade or how candy is earned.
