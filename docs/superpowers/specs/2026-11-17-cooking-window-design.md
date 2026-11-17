# Cooking Window Design

Date: 2026-11-17

## Goal

Move cooking out of its chain of item-list dialogs and into the crafting window the other crafts use,
and rebuild the cooking code around it. Cooking's rules do not change: this is a look-and-code rework,
not a balance change.

## Decisions made with the user

- Stoves open the crafting window in a new cooking mode. The old cooking dialogs are removed.
- Pie and Popsicle flavours are separate rows in the recipe list (Apple Pie, Cherry Pie, ...).
- "Any meat / fruit / vegetable" slots are clickable rows in the confirm panel. The player chooses what
  gets spent.
- Rules stay as they are: flat failure chance per recipe (20, 30 or 35), the Orchard Tart reduction, the
  Cook rank ladder by meals cooked, no rank or level gating.
- Catalysts do not work at the stove.
- No per-recipe "times made" count for cooking (the cell shows a dash).
- The per-meal orange-bar lines ("You have cooked a Dinner Plate!", "The ingredients you used were
  rotten.") are dropped in favour of the batch summary the other crafts show.
- Server design: one window session, two rule sets behind an interface (approach A). Cooking is not
  forced through `CraftingEngine`, and it does not get a window of its own.

## What exists today

Server (`Chaos-Server`, the submodule at `Chaos.Client/Chaos-Server`):

- `CraftingTileScript` sends both stoves (`mileth_kitchen`, `asilon_canteena`) to the `cooking_initial`
  dialog, gated on `Hobbies.Cooking`.
- `CookingScript` lists known recipes as a `ShowItems` dialog, then one `ShowItems` dialog per
  ingredient slot (`meat_initial`, `fruit_initial`, `vegetable_initial`, `extraingredients_initial`).
  `CookingDirectoryScript` routes between them. `CookItemScript` validates, takes the ingredients, rolls
  failure and gives the meal. The picks travel between dialogs as a `(string, int, string[])` context
  with comma-joined keys. `CookingScript` and `CookItemScript` each hold a copy of the same two helpers,
  and `CookItemScript` checks the ingredients twice.
- `CookingRequirements.cs` (1,825 lines) holds 35 recipes as hand-written `CookingRecipe` objects. The
  recipe types (`CookingRecipe`, `IngredientSlot`, `SlotIngredient`) sit in `CraftingRequirements.cs`.
- Slot shapes: 22 recipes are fully fixed. 11 have at least one "any of a category" slot. Five slots use
  `PickCount` 2 or 3 (several different items). Pie (11 outputs) and Popsicle (3 outputs) have a slot
  that decides the output through `OutputMappings`.
- `CookingHobby` holds the hobby gate, owed recipes and the rank ladder (`RecordCookedMeal`, eight
  titles from Beginner Cook at 1 meal to Master Chef at 1,500, counter `mealscooked`, legend key `cook`).
- `UtilityMealBuffs.CookingFailureReduction` takes points off a recipe's failure chance while Orchard
  Tart is active.
- `RecipeItemScript.Cooking.cs` (632 lines) is one identical `case` block per recipe scroll.
- `RecipeLibraryScript.Cooking.cs` shows the F1 cook book from `CookingRecipe.BookRequirements`, a
  hand-written sentence per recipe.
- The crafting window: `CraftingWindow` opens a `CraftingSession` per player. The session owns reach,
  the half-second batch timer, the hurt check and progress packets, and calls `CraftingBrowser` (list,
  preview) and `CraftingEngine` (make one). `CraftingWindowCatalog` describes the seven modes.
  `CraftingDisplays` builds the packets.
- Protocol: `CraftingDisplayArgs` (server to client; types Open, List, Detail, Progress, Close) and
  `CraftingActionArgs` (client to server; Select, Craft, Cancel, Close). Every field is always on the
  wire.

Client (`Chaos.Client`):

- `CraftingBrowserControl`: recipe list, filter, description, rank bar, success, level, ability, times
  made.
- `CraftingConfirmControl`: quantity, catalyst square, ingredient list (`CraftingIngredientRow`).
- `CraftingWindowState`: the pure view model both bind to.

Content (`Unora`): `Data/Configuration/Templates/Dialogs/Temauir/hobby/cooking/` holds the dialog
templates. No template outside that folder refers to the cooking flow's dialog keys.

## Player-facing behaviour

### Opening

Stepping on a stove tile opens the crafting window in cooking mode. A player without the cooking hobby
gets `CookingHobby.NOT_A_COOK_MESSAGE`, as today. God mode skips the gate, as today. A cook who knows no
recipes gets "You know no recipes for this craft." and no window.

### Recipe list

- One row per known recipe, with the meal's icon and name.
- A recipe with an output-deciding slot becomes one row per output: Pie gives 11 rows, Popsicle 3. The
  row's name and icon are the output item's. All flavours of a known recipe are listed.
- Rows that cannot be made right now are greyed and sorted after makeable ones, then by name. Cooking
  rows carry rank 0, so the list's rank ordering has no effect.
- The filter box works as in other crafts.

### Browser details

- Description: the finished meal, built by `CraftingDescription.Describe` as for other crafts.
- Success: `100 - (FailureChance - CookingFailureReduction)`, clamped to 0..100. With Orchard Tart at or
  above the recipe's chance this reads 100 and no roll happens, matching today's guard.
- Level and ability cells show what they show for a craft with no requirement.
- Times made shows a dash. The server sends -1; the client draws a dash for a negative value.
- Rank bar: the Cook ladder. Title is the current Cook title, count is `mealscooked`, threshold is the
  next title's. Text reads "212 / 300 to Sous Chef" with the full next title (other crafts shorten to
  the first word, which would read "to Sous"). At Master Chef the bar is full and shows the title.
  Before the first meal there is no title: the bar reads "0 / 1 to Beginner Cook".

### Confirm panel

- A fixed ingredient shows as today: icon, name, "have / need".
- A choosable row shows the chosen ingredient followed by a small arrow, and its own have / need.
  Left-click moves to the next eligible ingredient, right-click to the previous, wrapping round.
- Eligible means the player holds at least one meal's amount, and no other row of the same slot has it
  chosen.
- A row starts on the eligible ingredient the player holds most of (ties: recipe table order).
- With nothing eligible the row reads "Any fruit" (or meat, vegetable, ingredient) with "0 / need" in
  red and no icon, and the recipe is not makeable.
- A slot that needs several different items (`PickCount` above 1) shows one row per pick.
- A flavour row's deciding slot is fixed by the flavour and is not choosable.
- The catalyst square is empty in cooking mode, ignores clicks and refuses drops.
- Quantity caps at what the chosen ingredients cover (summing by item across rows), up to 100.

### Cooking a batch

- One meal per `CraftingSession.ItemInterval`, with the usual progress line and final summary
  ("Made 4, failed 1."). The client's progress text says "Cooking n / m" in cooking mode.
- A failed meal still consumes its ingredients.
- The batch stops when the player is hurt, leaves reach, or no longer holds a chosen ingredient ("Out of
  materials."). It never switches to another ingredient by itself.
- Each cooked meal calls `CookingHobby.RecordCookedMeal`, so rank-ups, titles and the legend mark behave
  as today. After a batch the list is resent, so the rank bar updates.
- Choices are kept while the window stays open. A chosen ingredient that is no longer eligible when the
  detail is rebuilt falls back to the default rule.

## Server design

### `ICraftingBook`

A new interface in `Chaos/Services/Crafting`. A book is created per session, so it may hold state.

- `IReadOnlyList<CraftingEntry> List()`: the player's rows, already ordered.
- `CraftingPreview? Preview(ushort id)`: one row's detail, or null when the id is not in the list.
- `CraftResult MakeOne(ushort id, BatchContext batch)`: makes one item, or returns Rejected.
- `CraftingDisplayArgs Header(CraftingDisplayType type, entries)`: craft name and rank fields for the
  Open and List packets.
- `bool StartBatch(ushort id, byte quantity, byte catalystSlot, out BatchContext batch, out string?
  refusal)`: validates and sizes a batch.
- `bool Swap(byte row, bool forward)`: moves an ingredient choice. False when the book has no choices.

Exact member shapes are the implementer's call so long as `CraftingSession` no longer names
`CraftingEngine`, `CraftingBrowser`, `Recipe` or upgrade rules. `CraftingEntry` and `CraftingPreview`
lose what only the engine book needs (`Recipe`, `Weapon`, `RequirementCheck`) or carry it in an
engine-only subtype; the preview carries its ingredient lines directly, each with a choosable flag.

### `EngineCraftingBook`

Wraps `CraftingBrowser` and `CraftingEngine` and takes the upgrade and catalyst logic now inside
`CraftingSession` (`IsUpgrade` branches, the weapon reference check, catalyst validation and
consumption, the "Mistakes happen" ending). The seven existing modes must behave exactly as before. The
existing crafting tests pass with changes only where they construct a session.

### `CookingBook`

- Rows: for each `CookingRequirements.Recipes` entry the player knows, one row, or one per flavour. Ids
  are indexes into a list built once at start-up from the recipe table in table order, so they are
  stable for the life of the server and independent of what the player knows.
- Choices: a dictionary from row id to the chosen template key per choosable row. Read with the default
  rule when absent or stale.
- Preview: ingredient lines in slot order, the rate, the quantity cap, the description of the output
  item, times made -1, no catalyst rates.
- `MakeOne`: totals the chosen ingredients by item; returns Rejected if the player is short; removes
  them, refunding what was taken if a removal fails; rolls failure unless the reduced chance is zero or
  less; on success gives the output with `GiveItemOrSendToBank` and calls
  `CookingHobby.RecordCookedMeal`. No orange-bar line per meal.
- `Swap`: refused while a batch runs (the session checks this), otherwise moves the row's choice to the
  next or previous eligible ingredient. With one or no eligible ingredient it does nothing.
- Header: craft name "Cooking", the Cook ladder fields from `CookingHobby`.
- Catalysts: `StartBatch` ignores the catalyst slot.

### Wiring

- `CraftingWindow.Open` builds the book for the mode. The cooking branch gates on
  `CookingHobby.IsCook` (or god mode) and skips the legend and `Crafts` checks and
  `Engine.AddRankExperience`.
- `CraftingWindow.Handle` routes `SwapIngredient` to the session, which calls the book and resends the
  detail when it returns true.
- `CraftingWindowScript` maps `cooking_window` to `CraftingWindowMode.Cooking`.
- `CraftingTileScript` points both stoves at `cooking_window`.

## Protocol

In `Chaos.DarkAges` and `Chaos.Networking`, shared by both sides:

- `CraftingWindowMode.Cooking`, appended.
- `CraftingIngredientInfo.Choosable` (bool), written after `Need` for each ingredient.
- `CraftingActionType.SwapIngredient`, appended. `CraftingActionArgs` gains `IngredientRow` (byte) and
  `Forward` (bool), always on the wire like the other fields. The server ignores a row that is out of
  range or not choosable.

An older client would misread every crafting window, so server and client ship together.

## Client design

- `CraftingIngredientRow`: when the bound ingredient is choosable, draws the arrow after the name and
  raises a swap event on left and right click. Non-choosable rows stay click-through.
- `CraftingConfirmControl`: forwards swap events as `SwapIngredient` actions; in cooking mode the
  catalyst square draws empty, does not respond to clicks and `IInventoryDropTarget` refuses the drop.
- `CraftingWindowState`: `SwapAction(row, forward)` (null while running); `RankBarText` uses the full
  next title in cooking mode; progress text says "Cooking" in cooking mode; opening in cooking mode
  clears any catalyst.
- `CraftingBrowserControl`: draws a dash for a negative times-made value.

## Code cleanup

- New file `Chaos/Definitions/CookingRecipes.cs` (or the existing `CookingRequirements.cs` rewritten in
  place) holding the recipe types and the table. Recipes are declared with three helpers:
  `Fixed(key, amount)`, `Any(category, amount)`, `AnyDistinct(category, picks, amount)`, plus
  `OneOf(...)` for a slot limited to named items and a flavour table for Pie and Popsicle. The types
  leave `CraftingRequirements.cs`.
- Before the rewrite, a snapshot of the current table is generated (per recipe: name, display key,
  default output, failure chance, each slot's category, allowed items and amounts, pick count,
  deciding flag, output mappings) and checked in as a test fixture. A test compares the new table to it.
  Any difference fails the test.
- `SuccessMessage` is removed from the recipe type (no longer shown).
- `BookRequirements` becomes a computed property that writes the sentence from the slots ("requires 1
  meat of any type, 10 fruit of any type and 5 vegetables of any type."). Wording may differ slightly
  from the hand-written strings.
- `RecipeItemScript.Cooking.cs`: a dictionary from scroll template key to recipe flag, and one learn
  path. Behaviour for non-cooks and already-known recipes is unchanged; `RecipeItemScriptTests` keeps
  passing.
- Deleted: `CookingScript`, `CookItemScript`, `CookingDirectoryScript`,
  `CookingRequirements.CookingCategoryDialogKeys`.

## Content changes (Unora)

- Add `hobby/cooking/cooking_window.json`: type `Normal`, script key `craftingwindow`, text "You step up
  to the stove.", modelled on `alchemy_window.json`.
- Delete `cooking_initial.json`, `cooking_directory.json`, `meat_initial.json`, `fruit_initial.json`,
  `vegetable_initial.json`, `extraingredients_initial.json`, `cook_item.json`, `cook_itemrepeat.json`,
  `cookfailed_itemrepeat.json`. Grep each key across `Data/Configuration` first.
- Keep `cookbook.json`, `cooking_accepthobby.json`, `cooking_starthobby.json` and the explanation
  dialogs. Update `cooking_explanation.json` if its text describes the old popups.

## Tests

Server (`Tests/Chaos.Tests`):

- Recipe snapshot test (above).
- `CookingBook`: flavour rows appear only for known recipes; default choice is the most-held eligible
  ingredient; swap moves forward and back and wraps; two rows of one slot never hold the same item; a
  row with nothing eligible makes the recipe unmakeable; quantity cap sums by item across rows; failure
  consumes ingredients; a short removal refunds; a cooked meal records rank progress; Orchard Tart
  raises the shown rate and skips the roll at zero; the catalyst slot is ignored.
- Session: a cooking batch runs to its total, stops with "Out of materials." when a chosen ingredient
  runs out, and a swap during a batch is refused.
- Window: a non-cook is refused; a cook with no recipes gets the message.
- Packets: round-trip of `Choosable` and of `SwapIngredient` with its fields.
- All existing crafting tests keep passing.

Client (`Tests/Chaos.Client.Tests`): `SwapAction` contents and null while running; cooking rank text;
cooking progress text; catalyst cannot be set in cooking mode.

Manual: in the TamerTest setup, at the Mileth stove as `whug`: cook a fixed recipe, a Dinner Plate with
swapped ingredients, a Salad (two vegetables), a pie flavour, a batch that runs out, and check a
non-cook is turned away.

## Order of work

1. Chaos-Server, branch in the submodule: extract `ICraftingBook` and `EngineCraftingBook` with no
   behaviour change.
2. Protocol additions and their converter tests.
3. Recipe snapshot fixture, then the recipe table rewrite.
4. `CookingBook`, window wiring, tile script.
5. Recipe scroll and cook book cleanup; delete the old scripts.
6. Chaos.Client, branch: row, confirm panel, state; bump the submodule pin.
7. Unora: dialog templates.
8. Playtest.

Server and client release together. Nothing is pushed without the user's say.

## Out of scope

- Any change to failure chances, the rank ladder, recipes or meal effects.
- Per-recipe cooking counters.
- Catalysts for cooking.
- The Loures army supply, ship errand and other scripts that only read cooking flags or counters.
