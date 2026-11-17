# Cooking Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stoves open the crafting window in a cooking mode with flavour rows and clickable ingredient
rows, and the old cooking dialogs and their code are removed.

**Architecture:** `CraftingSession` stops talking to `CraftingEngine`/`CraftingBrowser` directly and
talks to an `ICraftingBook`. `EngineCraftingBook` carries today's seven crafts unchanged; `CookingBook`
carries cooking's own rules. The wire protocol gains a mode, a per-ingredient flag and one action.

**Tech Stack:** C# 14 / .NET 10, TUnit tests (run as executables), MonoGame client, JSON dialog
templates.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-11-17-cooking-window-design.md`. Read it first.

## Global Constraints

- Three repos. Server: `~/Documents/GitHub/Chaos.Client/Chaos-Server` (the submodule; never a
  standalone clone). Client: `~/Documents/GitHub/Chaos.Client`. Content: `~/Documents/GitHub/Unora`.
  Work on a branch named `cooking-window` in each (the client branch already exists and holds the spec).
- Cooking rules do not change: failure chances, Orchard Tart reduction, rank ladder, recipes, outputs.
- Catalysts do not work in cooking mode.
- The seven existing crafting modes behave exactly as before.
- Orange bar messages are 45 characters at most.
- Chaos-Server is a base other forks build on: do not rename or remove public types outside the cooking
  scripts and types this plan names.
- Build: `dotnet build <sln or csproj> -m:1 -v:q -clp:ErrorsOnly`.
  Server tests: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -m:1 -- --treenode-filter "/*/*/<Class>/*"`.
  Client tests: `dotnet build Chaos.Client.slnx -m:1 -v:q -clp:ErrorsOnly` then
  `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build -- --treenode-filter "/*/*/<Class>/*"`.
  Never `dotnet test`.
- Commit after each task. Do not push.

## Review Focus

1. The bag changes while the window is open (a chosen ingredient is dropped or sold between Select and
   Craft): the batch is refused or stops with "Out of materials."; nothing is taken without a roll and
   no exception is thrown. Test in Task 4.
2. A `SwapIngredient` with no recipe selected, a row past the end, or a non-choosable row: ignored, no
   packet sent, no exception. Test in Task 4.
3. A slot needing N different items when the player holds exactly N eligible (swap has nowhere to go)
   or fewer than N (unmakeable, the unfilled rows read "Any ..."). Test in Task 4.
4. The client asks for more than the bag covers (quantity 100, bag covers 3): the batch makes 3. Test in
   Task 4.
5. Orchard Tart expires partway through a batch: each meal rolls against the chance at that moment.
   Test in Task 4.

---

## Server tasks (repo: `Chaos.Client/Chaos-Server`, branch `cooking-window` from `master`)

### Task 1: `ICraftingBook` and `EngineCraftingBook` (no behaviour change)

**Files:**
- Create: `Chaos/Services/Crafting/ICraftingBook.cs`
- Create: `Chaos/Services/Crafting/EngineCraftingBook.cs`
- Modify: `Chaos/Services/Crafting/CraftingSession.cs`, `Chaos/Services/Crafting/CraftingWindow.cs`
- Modify (construction only): `Tests/Chaos.Tests/Professions/CraftingSessionTests.cs`,
  `CraftingRestrictionTests.cs`, `CraftingTestKit.cs`

**Interfaces — Produces:**

```csharp
namespace Chaos.Services.Crafting;

/// What one step of a batch did. StopReason is set when the batch must end after this step.
public sealed record CraftStep(CraftOutcome Outcome, string? StopReason = null);

public interface ICraftingBook
{
    /// Open or List packet for the player's current rows. Null rows are never sent.
    CraftingDisplayArgs ListDisplay(CraftingDisplayType type);
    bool HasRows { get; }
    /// Detail packet for a row, or null when the id is not in the player's list.
    CraftingDisplayArgs? DetailDisplay(ushort id);
    /// Starts a batch and returns how many it will make; 0 when refused. A null refusal with 0 means
    /// the book already told the player why.
    int Begin(ushort id, byte quantity, byte catalystSlot, out string? refusal);
    /// Makes the next item of the batch begun last. Rejected always carries a StopReason.
    CraftStep MakeNext();
    void EndBatch();
    /// Moves an ingredient choice of a row. False when nothing changed.
    bool Swap(ushort id, byte row, bool forward);
}
```

`CraftingSession` constructor becomes `(Aisling crafter, Merchant station, ICraftingBook book)`. Its
public members stay: `REACH`, `ItemInterval`, `Station`, `SelectedId`, `IsRunning`, `InReach`,
`SendList`, `Select`, `StartBatch`, `StopBatch`, `End`, `Update`. The `Craft` property is removed; fix
any caller.

`EngineCraftingBook(Aisling crafter, CraftingWindowCraft craft, CraftingEngine engine, CraftingBrowser
browser, Func<string, (ushort Sprite, DisplayColor Color)> iconOf)` holds everything craft-specific now
in the session: the `IsUpgrade` branches, the weapon reference check ("You can no longer craft that."),
catalyst validation ("That is not a catalyst."), the per-item catalyst check and consumption ("Out of
catalysts."), the `MeetsRequirements` call for a non-makeable recipe (returns 0 with null refusal),
"Out of materials." and "Mistakes happen. The weapon is lost." (as `StopReason` on a Destroyed step).
`Swap` returns false.

- [ ] **Step 1:** Run the existing crafting tests and record the pass count:
  filters `CraftingSessionTests`, `CraftingWindowTests`, `CraftingBrowserTests`,
  `CraftingRestrictionTests`, `CraftingEngineTests`. Expected: all pass.
- [ ] **Step 2:** Add the interface and `EngineCraftingBook`; move the logic out of `CraftingSession`;
  `CraftingWindow.Open` builds the book. Add `CraftingTestKit.Book(Aisling crafter,
  CraftingWindowCraft? craft = null)` and use it where tests construct a session.
- [ ] **Step 3:** Run the same filters. Expected: the same tests pass, with no assertion edited.
- [ ] **Step 4:** Commit: "Put the crafting window's craft rules behind ICraftingBook".

### Task 2: Protocol additions

**Files:**
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (`CraftingWindowMode`, `CraftingActionType`)
- Modify: `Chaos.Networking/Entities/Server/CraftingInfos.cs`,
  `Chaos.Networking/Entities/Client/CraftingActionArgs.cs`
- Modify: `Chaos.Networking/Converters/Server/CraftingDisplayConverter.cs`,
  `Chaos.Networking/Converters/Client/CraftingActionConverter.cs`
- Test: `Tests/Chaos.Tests/Networking/CraftingPacketConverterTests.cs`

**Interfaces — Produces:**
- `CraftingWindowMode.Cooking` appended (value 7). `CraftingActionType.SwapIngredient` appended (value 4).
- `CraftingIngredientInfo.Choosable` (`bool`), written and read as a boolean straight after `Need`.
- `CraftingActionArgs.IngredientRow` (`byte`) and `CraftingActionArgs.Forward` (`bool`), written and
  read after `CatalystSlot`, always on the wire.

- [ ] **Step 1:** Add failing tests: `Detail_round_trips_choosable_flags` (two ingredients, the second
  `Choosable = true`, mode `Cooking`; every field equal after a round trip) and
  `SwapIngredient_round_trips` (`Type = SwapIngredient, RecipeId = 12, IngredientRow = 3, Forward =
  false`, then again with `Forward = true`).
- [ ] **Step 2:** Run filter `CraftingPacketConverterTests`. Expected: the two new tests fail to compile
  or fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the filter. Expected: all pass.
- [ ] **Step 5:** Commit: "Crafting protocol: cooking mode, choosable ingredients, swap action".

### Task 3: Recipe snapshot, then the recipe table rewrite

**Files:**
- Create: `Tests/Chaos.Tests/Professions/CookingRecipeSnapshotTests.cs`
- Create: `Tests/Chaos.Tests/Professions/Fixtures/cooking-recipes.snapshot.txt`
- Modify: `Chaos/Definitions/CookingRequirements.cs` (rewrite), `Chaos/Definitions/CraftingRequirements.cs`
  (the three cooking types move out)

**Interfaces — Produces** (all in `Chaos/Definitions/CookingRequirements.cs`, namespace unchanged):
- `CookingRecipe`, `IngredientSlot`, `SlotIngredient` keep their names and members, except
  `CookingRecipe.BookRequirements` becomes a computed `string` property. `SuccessMessage` and
  `CookingRequirements.CookingCategoryDialogKeys` stay until Task 5.
- Private helpers used to declare the table: `Fixed(string key, int amount)`,
  `Any(FoodCategory category, int amount)`, `AnyDistinct(FoodCategory category, int picks, int amount)`,
  `OneOf(FoodCategory category, bool decidesOutput, params (string Key, int Amount)[] items)`, each
  returning an `IngredientSlot`. `Fixed` takes the category from `CookingCategoryItems`.
- `CookingRequirements.CategoryWord(FoodCategory)` returns "meat", "fruit", "vegetable", "ingredient".

- [ ] **Step 1:** Write `CookingRecipeSnapshotTests.Recipes_match_snapshot`. It renders
  `CookingRequirements.Recipes` in table order to text, one block per recipe: flag name, `Name`,
  `DisplayTemplateKey`, `DefaultOutputTemplateKey`, `FailureChance`, then each slot as
  `category|pickCount|amountPerPick|decides|key:amount,key:amount` (empty list when
  `AllowedIngredients` is null), then `OutputMappings` sorted by key. It compares to the fixture, found
  through `[CallerFilePath]`. When the environment variable `UPDATE_COOKING_SNAPSHOT=1` is set it writes
  the fixture instead and passes.
- [ ] **Step 2:** With the table still unchanged, run once with `UPDATE_COOKING_SNAPSHOT=1`, then
  without. Expected: fixture has 35 blocks; the test passes. Commit the test and fixture alone:
  "Snapshot the cooking recipe table".
- [ ] **Step 3:** Rewrite the table with the helpers. Move the three types into this file. Make
  `BookRequirements` computed: "requires " + the slots joined with ", " and a final " and " + ".".
  A fixed slot reads "{amount} {item name}" using the item key's display name from a small private
  key-to-name map only where the key is not already readable; an any slot reads "{amount} {category
  word} of any type"; a distinct slot reads "{amount} each of {picks} different {category word}s"; a
  `OneOf` slot lists its items with "or". A recipe with a deciding slot appends " The {category word}
  used decides what is made."
- [ ] **Step 4:** Run filters `CookingRecipeSnapshotTests`, `HalloweenTreatRecipeTests`,
  `RecipeItemScriptTests`. Expected: all pass, fixture untouched (`git status` shows no change to it).
- [ ] **Step 5:** Commit: "Rewrite the cooking recipe table with slot helpers".

### Task 4: `CookingBook` and the stove wiring

**Files:**
- Create: `Chaos/Services/Crafting/CookingRows.cs`, `Chaos/Services/Crafting/CookingBook.cs`
- Modify: `Chaos/Definitions/CookingHobby.cs`, `Chaos/Services/Crafting/CraftingWindow.cs`,
  `Chaos/Services/Crafting/CraftingSession.cs`,
  `Chaos/Scripting/DialogScripts/Temuair/Crafting/CraftingWindowScript.cs`,
  `Chaos/Scripting/ReactorTileScripts/Temauir/CraftingTileScript.cs`
- Test: create `Tests/Chaos.Tests/Professions/CookingBookTests.cs`; extend `CraftingSessionTests.cs`,
  `CraftingWindowTests.cs`, `CraftingWindowScriptTests.cs`, `CraftingTileScriptTests.cs`,
  `CookingRanksTests.cs`

**Interfaces — Consumes:** `ICraftingBook`, `CraftStep` (Task 1); `Choosable`, `SwapIngredient`,
`IngredientRow`, `Forward`, `CraftingWindowMode.Cooking` (Task 2); `CategoryWord` (Task 3).

**Interfaces — Produces:**

```csharp
// CookingRows.cs
public sealed record CookingRow(
    ushort Id, BigFlagsValue<BigCookingRecipes> Flag, CookingRecipe Recipe,
    string? FlavourKey, string OutputTemplateKey);

public static class CookingRows
{
    /// Every row, built once in recipe table order; a recipe with a deciding slot gives one row per
    /// OutputMappings entry in mapping order. Id is the index.
    public static IReadOnlyList<CookingRow> All { get; }
}

// CookingBook.cs
public sealed class CookingBook(
    Aisling cook, IItemFactory itemFactory,
    Func<string, (ushort Sprite, DisplayColor Color)> iconOf,
    Func<int, bool>? rollChance = null) : ICraftingBook   // default IntegerRandomizer.RollChance

// CookingHobby.cs
public static int MealsCooked(Aisling aisling);   // the counter, or the legend mark's count when the counter is missing, else 0
public static (string Title, string NextTitle, int NextThreshold) RankProgress(int mealsCooked);
// ("", "Beginner Cook", 1) at 0; ("Sous Chef", "Chef", 500) at 300; ("Master Chef", "", 0) at 1500+

// CraftingSession.cs
public bool Swap(byte row, bool forward);   // false while running or with nothing selected; resends Detail on true
```

Rules the code must follow (from the spec):
- A row is listed when the cook has its recipe flag. Order: makeable first, then name.
- Row name and icon: the output item for a flavour row, otherwise `Recipe.Name` and
  `DisplayTemplateKey`. `Rank` 0.
- Lines are in slot order, one per pick. A line is choosable when its slot offers more than one item
  and is not the flavour's deciding slot. The deciding slot's line is fixed to `FlavourKey` with that
  item's amount.
- Eligible: the cook holds at least the line's amount of the item, and no other line of the same slot
  has it. Default: the eligible item held in the greatest count; ties go to the slot's item order.
  A stored choice that is no longer eligible is dropped for the default.
- A line with nothing eligible: name "Any {category word}", sprite 0, have 0, need
  `slot.AmountPerPick`.
- `Have`/`Need` on each line are that item's own count and that line's amount.
- Makeable: every line has an item and, summing amounts by item across lines, the cook holds enough.
  `MaxQuantity`: the smallest `held / summed need` over items, at most `CraftingProtocol.MAX_QUANTITY`.
- Detail: `SuccessRate = clamp(100 - (Recipe.FailureChance - UtilityMealBuffs.CookingFailureReduction(cook)), 0, 100)`,
  `CatalystRates` empty, `TimesMade = -1`, `Level` the cook's level, `RequiredLevel = 0`,
  `AbilityLevel` the cook's, `RequiredAbility = 0`, `Description` from
  `CraftingDescription.Describe` on the faux output item.
- List header: `CraftName = "Cooking"`, `Mode = Cooking`, rank fields from `RankProgress(MealsCooked(cook))`.
- `Begin`: ignores `catalystSlot`; refusal "You cannot cook that." for an unknown id or a row that is
  not makeable; otherwise returns `clamp(quantity, 1, MaxQuantity)` and remembers the row and its
  chosen items for the batch.
- `MakeNext`: re-totals the batch's chosen items against the bag; short gives
  `Rejected, "Out of materials."`. Removes them; a failed removal refunds what was taken with
  `GiveItemOrSendToBank` and gives the same Rejected step. Failure chance is recomputed each meal; when
  it is above 0 and the roll hits, the step is `Failed`. Otherwise gives the output item with
  `GiveItemOrSendToBank`, calls `CookingHobby.RecordCookedMeal`, step `Succeeded`. No per-meal message.
- `Swap`: unknown id, row out of range or a non-choosable line returns false. Otherwise moves to the
  next or previous eligible item in slot item order, wrapping; false when there is no other.
- `CraftingWindow.Open` for `Cooking`: refuses with `CookingHobby.NOT_A_COOK_MESSAGE` unless
  `CookingHobby.IsCook` or god mode; skips the `Crafts`/legend checks and `AddRankExperience`; "You
  know no recipes for this craft." when `HasRows` is false. `CraftingWindowCatalog.Get` is not called
  for `Cooking`.
- `CraftingWindow.Handle`: `SwapIngredient` calls `session.Swap(args.IngredientRow, args.Forward)`.
- `CraftingWindowScript`: `["cooking_window"] = CraftingWindowMode.Cooking`.
- `CraftingTileScript`: both stoves use dialog key `cooking_window`.

- [ ] **Step 1:** Write `CookingBookTests` (a cook built with `MockAisling`, recipe flags added, items
  added by template key; a book with a scripted roll). Tests, each asserting on the packets the book
  returns or on the bag:
  - `Flavour_rows_listed_only_for_known_recipes`: Pie known gives 11 rows named after the pies; Pie
    unknown gives none.
  - `Default_choice_is_most_held`: Dinner Plate with 12 apple and 40 cherry: fruit line is cherry,
    `Choosable`, 40 / 10.
  - `Swap_moves_and_wraps`: forward from cherry reaches apple, forward again returns to cherry;
    backward mirrors.
  - `Distinct_rows_never_share_an_item`: Salad with two eligible vegetables shows both; a swap on
    either returns false.
  - `Too_few_distinct_items_is_unmakeable`: Salad with one eligible vegetable: second line named
    "Any vegetable", `MaxQuantity` 0, `Begin` returns 0 with "You cannot cook that."
  - `Quantity_cap_sums_by_item`: Lucky Stew (two meats at 2 each, three vegetables at 15 each) with
    exactly enough for 3: `MaxQuantity` 3; `Begin(id, 100, 0, ...)` returns 3.
  - `Failure_consumes_ingredients`: roll scripted to fail: step `Failed`, ingredients gone, no meal,
    `MealsCooked` unchanged.
  - `Success_gives_meal_and_records_rank`: step `Succeeded`, one meal in the bag, `MealsCooked` 1,
    legend mark "Beginner Cook".
  - `Stale_choice_stops_the_batch`: `Begin` for 5, remove the chosen fruit from the bag, `MakeNext`
    gives `Rejected` with "Out of materials." and nothing else is taken.
  - `Orchard_tart_is_read_each_meal`: with the effect the detail shows the raised rate; when the
    reduced chance is 0 or less the roll delegate is not called; removing the effect mid-batch makes
    the next meal roll the full chance (assert the chance passed to the delegate).
  - `Catalyst_slot_is_ignored`: `Begin` with a catalyst's slot returns the same total and the catalyst
    is still in the bag after the batch.
  - `Bad_swaps_are_ignored`: unknown id, row 200, and a fixed line each return false.
  - `Ids_do_not_depend_on_known_recipes`: the Dinner Plate row's id is the same for a cook who knows
    one recipe and one who knows all.
- [ ] **Step 2:** Add to the existing test classes:
  - `CookingRanksTests`: `RankProgress` at 0, 1, 24, 25, 300, 1499, 1500; `MealsCooked` from counter,
    from mark only, from neither.
  - `CraftingSessionTests`: a cooking batch of 3 sends progress and a finished summary "Made 3, failed
    0."; `Swap` during a running batch returns false and sends nothing; `Swap` with nothing selected
    returns false.
  - `CraftingWindowTests`: a non-cook gets `NOT_A_COOK_MESSAGE` and no session; a cook with no recipes
    gets "You know no recipes for this craft."; a cook with a recipe gets an Open packet with mode
    `Cooking` and craft name "Cooking".
  - `CraftingWindowScriptTests`: `ModeFor("cooking_window")` is `Cooking`.
  - `CraftingTileScriptTests`: `DialogKeyFor("mileth_kitchen")` and `("asilon_canteena")` are
    `cooking_window`.
- [ ] **Step 3:** Run the filters. Expected: new tests fail.
- [ ] **Step 4:** Implement.
- [ ] **Step 5:** Run filters `CookingBookTests`, `CookingRanksTests`, `CraftingSessionTests`,
  `CraftingWindowTests`, `CraftingWindowScriptTests`, `CraftingTileScriptTests`,
  `CraftingBrowserTests`. Expected: all pass.
- [ ] **Step 6:** Commit: "Cook at the stove through the crafting window".

### Task 5: Remove the old flow; recipe scroll lookup

**Files:**
- Delete: `Chaos/Scripting/DialogScripts/Temuair/Crafting/CookingScript.cs`, `CookItemScript.cs`,
  `CookingDirectoryScript.cs`
- Modify: `Chaos/Definitions/CookingRequirements.cs` (remove `SuccessMessage`,
  `CookingCategoryDialogKeys`), `Chaos/Scripting/ItemScripts/RecipeItemScript.Cooking.cs`
- Test: `Tests/Chaos.Tests/Items/RecipeItemScriptTests.cs`,
  `Tests/Chaos.Tests/Professions/CookingRecipeSnapshotTests.cs`

**Interfaces — Produces:** `RecipeItemScript.CookingScrolls`, an `internal static
IReadOnlyDictionary<string, BigFlagsValue<BigCookingRecipes>>` (case-insensitive) from scroll template
key to flag, 34 entries. `HandleCookingRecipes` looks the key up; a known recipe gives "You already
know this recipe."; otherwise it calls `CookingRecipeLearn` with
`CookingRequirements.Recipes[flag].Name`.

- [ ] **Step 1:** Add tests: `Every_cooking_scroll_maps_to_a_recipe` (34 entries; each flag is in
  `CookingRequirements.Recipes`; `recipe_liverandonions` maps to `LiverAndOnions`) and
  `Learn_message_uses_recipe_name` (reading `recipe_scholarsfeast` as a cook sends "You've learned
  Scholar's Feast."). Run filter `RecipeItemScriptTests`. Expected: new tests fail.
- [ ] **Step 2:** Replace the 34 case blocks with the dictionary and one path. Delete the three
  scripts. Remove `SuccessMessage` and `CookingCategoryDialogKeys`. Grep the solution for
  `"cooking_initial"`, `"cook_item"` and `CookingCategoryDialogKeys` and remove what remains.
- [ ] **Step 3:** Build `Chaos.slnx`. Run filters `RecipeItemScriptTests`,
  `CookingRecipeSnapshotTests`, `CookingHobbyTests`, `HalloweenTreatRecipeTests`. Expected: build clean,
  all pass, snapshot fixture untouched.
- [ ] **Step 4:** Commit: "Remove the cooking dialogs' scripts; table-drive the recipe scrolls".

---

## Client task (repo: `Chaos.Client`, branch `cooking-window`)

### Task 6: Choosable rows, swap action, cooking mode

**Files:**
- Modify: `Chaos.Client/ViewModel/CraftingWindowState.cs`,
  `Chaos.Client/Controls/World/Popups/Crafting/CraftingIngredientRow.cs`,
  `CraftingConfirmControl.cs`, `CraftingBrowserControl.cs`
- Test: `Tests/Chaos.Client.Tests/CraftingWindowStateTests.cs`

**Interfaces — Consumes:** Task 2's protocol members, from the submodule's working tree at the server
branch.

**Interfaces — Produces** (on `CraftingWindowState`):
- `bool IsCooking => Mode == CraftingWindowMode.Cooking`
- `CraftingActionArgs? SwapAction(byte row, bool forward)`: null while `Running` or with nothing
  selected; otherwise `Type = SwapIngredient, RecipeId = SelectedId, IngredientRow = row, Forward = forward`.
- `RankBarText` in cooking mode: `"{RankCount} / {RankThreshold} to {NextRankTitle}"` (full title);
  `RankShortTitle` in cooking mode returns the full `RankTitle`.
- Progress text in cooking mode: `"Cooking {done} / {total}"`, in both `Apply` and `CraftAction`.
- `SetCatalyst` returns false in cooking mode; `ApplyList` clears the catalyst when the mode is
  `Cooking`.
- `string TimesMadeText`: `"-"` when `Detail.TimesMade < 0`, otherwise the number; empty with no detail.

Controls:
- `CraftingIngredientRow`: `event Action<bool>? SwapRequested` (true for left click, false for right).
  When the bound ingredient is `Choosable` the row is hit-test visible, draws the arrow glyph `>` in
  the name colour directly after the name, and raises the event on click. Otherwise it stays
  click-through as now. The row needs its index: `Bind(CraftingIngredientInfo ingredient, int index)`.
- `CraftingConfirmControl`: subscribes each row and raises `ActionRequested` with
  `State.SwapAction((byte)index, forward)` when not null. The catalyst line is never choosable and is
  not counted in the index. In cooking mode the catalyst image has no texture and the drop target
  refuses (through `SetCatalyst`).
- `CraftingBrowserControl`: `MadeValue.Text = State.TimesMadeText`.

- [ ] **Step 1:** Add state tests: `Swap_action_carries_row_and_direction`,
  `Swap_action_is_null_while_running`, `Swap_action_is_null_without_selection`,
  `Cooking_rank_text_uses_full_title` (`"212 / 300 to Sous Chef"`, and `"0 / 1 to Beginner Cook"` with
  an empty `RankTitle`), `Cooking_progress_says_cooking`, `Catalyst_cannot_be_set_when_cooking`,
  `Times_made_dash_when_negative`, and `Other_crafts_keep_short_rank_text` (Alchemy still gives
  `"... to Initiate"`).
- [ ] **Step 2:** Build, run filter `CraftingWindowStateTests`. Expected: new tests fail.
- [ ] **Step 3:** Implement state, then the controls.
- [ ] **Step 4:** Build, run the filter. Expected: all pass.
- [ ] **Step 5:** `git add Chaos-Server` to pin the submodule at the server branch's head, with the
  code. Commit: "Crafting window: cooking mode with choosable ingredients". (The pin is moved to
  `master` once the server branch is merged.)

---

## Content task (repo: `Unora`, branch `cooking-window`)

### Task 7: Dialog templates

**Files** (under `Data/Configuration/Templates/Dialogs/Temauir/hobby/cooking/`):
- Create: `cooking_window.json`: a copy of `alchemy_window.json` (find it under `Dialogs/`) with
  `templateKey` `cooking_window` and `text` `You step up to the stove.`
- Delete: `cooking_initial.json`, `cooking_directory.json`, `meat_initial.json`, `fruit_initial.json`,
  `vegetable_initial.json`, `extraingredients_initial.json`, `cook_item.json`, `cook_itemrepeat.json`,
  `cookfailed_itemrepeat.json`

- [ ] **Step 1:** For each key to delete, `git grep -n '"<key>"' -- Data/Configuration`. Expected: hits
  only inside the files being deleted. Stop and report any other hit.
- [ ] **Step 2:** Add the new file, delete the nine. `python -c "import json,sys;
  json.load(open(sys.argv[1]))" <new file>` exits 0.
- [ ] **Step 3:** Commit: "Cooking: stove opens the crafting window".

`cooking_explanation.json` does not describe the old popups and stays as it is.

---

### Task 8: Playtest (run by Claude with the user, not by the coding agent)

Using the TamerTest setup with the three branches: at the Mileth stove as `whug`, cook a fixed recipe;
a Dinner Plate after swapping each choosable row both ways; a Salad; one pie flavour; a batch that runs
out; walk away mid-batch; confirm a non-cook is turned away; open the alchemy table and check it is
unchanged, catalyst included. Check the rank cell fits "Beginner Cook" and the arrow sits clear of the
count on the longest ingredient name.
