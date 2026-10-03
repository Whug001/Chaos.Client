# Haunted Theatre Vendors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three candy vendors beside fair stalls in the haunted Suomi theatre. One sells three new Halloween treat recipes, one sells the Macabre Box accessories, and one sells the Macabre Box overcoats.

**Architecture:** The server gets three treat buffs (copies of existing utility meals), three cooking recipes and one shared `CandyShopScript`. The shop reads each vendor's price list from its merchant template. Unora gets the item, merchant and dialog data. It also gets four tools: one cuts the stall pictures into wall strips, one imports them into `ia.dat` and both `sotp.dat` copies, one writes them into the theatre map, and an extension to the accessory tool draws the treat icons into `Legend.dat`.

**Tech Stack:** C# / .NET 10 (Chaos-Server), TUnit + FluentAssertions + Moq, Python 3 + Pillow + numpy (pytest / unittest), DALib file-based C# scripts.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-03-theatre-vendors-design.md`

## Global Constraints

- **Worktrees** (Task 0 creates them; every path below is relative to one of these):
  - server `S` = `C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server` (branch `feat/theatre-vendors` from Chaos-Server `master`)
  - content `U` = `C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-unora` (branch `feat/theatre-vendors` from Unora `main`)
- **Never touch the shared checkouts** (`GitHub\Chaos.Client`, `GitHub\Chaos.Client\Chaos-Server`, `GitHub\Unora`). Other sessions have uncommitted work there.
- **Never write into the local client folder** `C:\Users\Michael\Documents\Unora\Unora Files`. Tools read it and write their outputs elsewhere.
- **Do not commit** in any task except the last. Leave all changes in the worktrees' working trees.
- **Test commands.** Server (from `S`): `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/<Class>/*"`. `dotnet test` does not work. Python (from `U`): `python -m pytest Tools/HauntedTheatre -q` and `python -m unittest Tools/Accessories/test_items.py`.
- **Known failures:** two server tests already fail on master (`GiveAbility`, `OnItemDroppedOn` stackable). Leave them alone.
- **Fixed numbers:**
  - Stall strips: `stc09132.hpf` to `stc09143.hpf`, all with walk flag 15 in both `sotp.dat` copies. The back cell of each stall gets left foreground 1, an invisible blocker.
  - Stall anchors (front-left cell): popcorn (13, 6), face paint (17, 10), fortune (17, 24).
  - Vendor tiles: (13, 7), (17, 11), (17, 25).
  - Candy key: `halloweencandy`. Recipe price: 25 candy each.
  - Accessory and overcoat prices: as listed in spec section 2.
- **Text limits:** orange bar ≤ 45 characters, dialog option ≤ 35, dialog text ≤ 360, no em dashes in JSON.
- **Comments:** only where the why is not obvious. No explanatory comments in test code.
- **Code tools:** use Serena's symbolic tools for C# reads and edits in `S` (see the user's CLAUDE.md). Built-in Read/Edit are fine for JSON, Markdown, Python and file-based `.cs` scripts in `U`. Serena's edit tools take paths relative to `C:\Users\Michael\Documents\GitHub`, e.g. `worktrees/theatre-vendors-server/Chaos/...`.

**User decisions (already made):**
- Three vendors: popcorn stall sells recipes, face paint stall sells accessories, fortune stall sells overcoats.
- Vendors: Crypt Reaper (sprite 68) at popcorn, Dark Spirit (1250) at face paint, Mummy (188) at fortune.
- Layout C ("spread out"); vendors stand beside their stalls, not behind the counter.
- Treats share the utility-meal slot ("same slot as meals").
- Popcorn +5% experience (needs acorns); Cotton Candy mana regen (sugar, cotton); Hot Dog health regen (raw meat, flour).
- Everything is paid in Halloween candy. Accessories and overcoats are the Macabre Box items, "priced high".
- Three new hand-drawn treat icons.
- The release `ia.dat` is built from the local client folder copy, which ships the Endless Tower strips too (spec section 5, approved).

---

## File structure

**Chaos-Server (`S`)**

| File | Responsibility |
|---|---|
| `Chaos/Definitions/UtilityMealBuffs.cs` | Treat names; the exp and regen readers count the treats |
| `Chaos/Scripting/EffectScripts/Items/CookingMeals/UtilityMealConflicts.cs` | Treats join the utility-meal slot |
| `Chaos/Scripting/EffectScripts/Items/CookingMeals/{Popcorn,CottonCandy,HotDog}Effect.cs` (new) | The three buffs |
| `Chaos/Definitions/BigFlags.cs` | Three `BigCookingRecipes` flags |
| `Chaos/Definitions/CookingRequirements.cs` | Three recipes; `cotton` as an extra ingredient |
| `Chaos/Scripting/ItemScripts/RecipeItemScript.Cooking.cs` | Learning the three recipes |
| `Chaos/Scripting/DialogScripts/Temuair/Generic/RecipeLibraryScript.Cooking.cs` | The cookbook lists them |
| `Chaos/Scripting/DialogScripts/Temuair/Generic/CandyShopScript.cs` (new) | The shared candy shop |
| `Chaos/Resources/sotp.dat` | Walk flags for the 12 stall strips (copied in by Task 6) |
| `Tests/Chaos.Tests/Effects/HalloweenTreatTests.cs`, `Tests/Chaos.Tests/Items/HalloweenTreatRecipeTests.cs`, `Tests/Chaos.Tests/Merchants/CandyShopScriptTests.cs` (new) | Tests |

**Unora (`U`)**

| File | Responsibility |
|---|---|
| `Data/Configuration/Templates/Items/crafting/Cooking/Meals/{popcorn,cottoncandy,hotdog}.json` (new) | The treats |
| `Data/Configuration/Templates/Items/Recipes/Cooking/recipe_{popcorn,cottoncandy,hotdog}.json` (new) | The recipe scrolls |
| `Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/stall_{reaper,spirit,mummy}.json` (new) | The vendors and their price lists |
| `Data/Configuration/Templates/Dialogs/Temauir/generic/CandyShop/candyshop_*.json` (5 new) | The shop dialogs |
| `Data/Configuration/Templates/Dialogs/Temauir/Events/Halloween/theatre_vendors/stall_*_initial.json` (3 new) | Each vendor's greeting |
| `Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/merchants.json` | Places the vendors |
| `Data/Configuration/MapData/lod10269.map` | The stalls on the map |
| `Tools/HauntedTheatre/vendors.py` (new) | The vendor constants every theatre-vendor tool and test reads |
| `Tools/HauntedTheatre/test_vendors.py` (new) | Content checks |
| `Tools/HauntedTheatre/stall_strips.py`, `test_stall_strips.py` (new) | Cuts a stall picture into four strips |
| `Tools/HauntedTheatre/stalls/` (generated, committed) | The 12 strips and `ids.json` |
| `Tools/HauntedTheatre/import_stalls.cs` (new) | Imports the strips into `ia.dat` and both `sotp.dat` copies |
| `Tools/HauntedTheatre/place_stalls.py`, `test_place_stalls.py` (new) | Writes the stalls into the map and checks reachability |
| `Tools/HauntedTheatre/README.md` (new) | How to run the stall tools |
| `Tools/MirrorMaze/render_map.cs` | Optional data-folder argument |
| `Tools/Accessories/acclib/registry.py`, `acclib/review.py`, `test_items.py` | Icon-only item kind `item` |
| `Tools/Accessories/items/{Popcorn,CottonCandy,HotDog}/` (new) | The three icon tools |
| `Tools/Accessories/registry.json` | Three new entries |

---

### Task 0: Create the worktrees and record the baseline

**Goal:** Two clean worktrees on `feat/theatre-vendors`, with a baseline server test run.

**Files:** none (git only).

**Acceptance Criteria:**
- [ ] `S` and `U` exist on branch `feat/theatre-vendors`.
- [ ] The server solution builds in `S`, and the server test run shows only the 2 known failures.
- [ ] `python -c "import PIL, numpy, pytest"` succeeds, and `python -m pytest Tools/HauntedTheatre -q` passes in `U`.

**Verify:** `git -C C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server branch --show-current` → `feat/theatre-vendors`

**Steps:**

- [ ] **Step 1: Create the worktrees** (PowerShell):

```powershell
git -C C:\Users\Michael\Documents\GitHub\Chaos.Client\Chaos-Server worktree add C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server -b feat/theatre-vendors master
git -C C:\Users\Michael\Documents\GitHub\Unora -c core.longpaths=true worktree add C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-unora -b feat/theatre-vendors main
```

The Unora worktree needs `-c core.longpaths=true`, because one crafting template path is too long for Windows. Do not set it with `git config`, because that writes the shared repo config.

- [ ] **Step 2: Baseline server build and tests** (from `S`):

```powershell
dotnet build Chaos.slnx
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi
```

Expected: the build succeeds, and only `GiveAbility` and `OnItemDroppedOn` (stackable) fail. If anything else fails, stop and report it.

- [ ] **Step 3: Python baseline** (from `U`):

```powershell
python -c "import PIL, numpy, pytest"
python -m pytest Tools/HauntedTheatre -q
python -m unittest Tools/Accessories/test_items.py
```

Expected: all pass (build tests that need the client folder may be skipped).

```json:metadata
{"files": [], "verifyCommand": "git -C C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\theatre-vendors-server branch --show-current", "acceptanceCriteria": ["S and U on feat/theatre-vendors", "server baseline only the 2 known failures", "python baseline passes"], "modelTier": "mechanical"}
```

---

### Task 1: Treat buffs (server)

**Goal:** `PopcornEffect`, `CottonCandyEffect` and `HotDogEffect`, which share the utility-meal slot and pay the same bonuses as Acorn Mash, Cucumber Cooler and Hearthside Porridge.

**Files:**
- Modify: `S/Chaos/Definitions/UtilityMealBuffs.cs`
- Modify: `S/Chaos/Scripting/EffectScripts/Items/CookingMeals/UtilityMealConflicts.cs`
- Create: `S/Chaos/Scripting/EffectScripts/Items/CookingMeals/PopcornEffect.cs`, `CottonCandyEffect.cs`, `HotDogEffect.cs`
- Test: `S/Tests/Chaos.Tests/Effects/HalloweenTreatTests.cs`

**Acceptance Criteria:**
- [ ] Eating Popcorn gives `UtilityMealBuffs.ExperienceBonus` 0.05, Cotton Candy gives `ManaRegenBonus` 3 and Hot Dog gives `HealthRegenBonus` 6.
- [ ] Each treat replaces Acorn Mash (and any other utility meal), and Acorn Mash replaces a treat.
- [ ] The existing `CookingMealStackingTests` still pass; they pick the new effects up automatically.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/HalloweenTreatTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** in `S/Tests/Chaos.Tests/Effects/HalloweenTreatTests.cs`:

```csharp
#region
using Chaos.Definitions;
using Chaos.Models.World;
using Chaos.Scripting.EffectScripts.Items.CookingMeals;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Effects;

public sealed class HalloweenTreatTests
{
    private static Aisling Eater() => MockAisling.Create();

    [Test]
    public void Popcorn_adds_five_percent_experience()
    {
        var aisling = Eater();

        aisling.Effects.Apply(aisling, new PopcornEffect());

        UtilityMealBuffs.ExperienceBonus(aisling)
                        .Should()
                        .Be(0.05m);
    }

    [Test]
    public void Cotton_Candy_adds_three_points_of_mana_regeneration()
    {
        var aisling = Eater();

        aisling.Effects.Apply(aisling, new CottonCandyEffect());

        UtilityMealBuffs.ManaRegenBonus(aisling)
                        .Should()
                        .Be(3m);
    }

    [Test]
    public void A_Hot_Dog_adds_six_points_of_health_regeneration()
    {
        var aisling = Eater();

        aisling.Effects.Apply(aisling, new HotDogEffect());

        UtilityMealBuffs.HealthRegenBonus(aisling)
                        .Should()
                        .Be(6m);
    }

    [Test]
    public void Popcorn_replaces_Acorn_Mash()
    {
        var aisling = Eater();

        aisling.Effects.Apply(aisling, new AcornMashEffect());
        aisling.Effects.Apply(aisling, new PopcornEffect());

        aisling.Effects.Contains(UtilityMealBuffs.ACORN_MASH).Should().BeFalse();
        aisling.Effects.Contains(UtilityMealBuffs.POPCORN).Should().BeTrue();
    }

    [Test]
    public void Acorn_Mash_replaces_a_Hot_Dog()
    {
        var aisling = Eater();

        aisling.Effects.Apply(aisling, new HotDogEffect());
        aisling.Effects.Apply(aisling, new AcornMashEffect());

        aisling.Effects.Contains(UtilityMealBuffs.HOT_DOG).Should().BeFalse();
        aisling.Effects.Contains(UtilityMealBuffs.ACORN_MASH).Should().BeTrue();
    }

    [Test]
    public void Cotton_Candy_replaces_Popcorn()
    {
        var aisling = Eater();

        aisling.Effects.Apply(aisling, new PopcornEffect());
        aisling.Effects.Apply(aisling, new CottonCandyEffect());

        aisling.Effects.Contains(UtilityMealBuffs.POPCORN).Should().BeFalse();
        aisling.Effects.Contains(UtilityMealBuffs.COTTON_CANDY).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run them to see them fail.** Run the Verify command. Expected: a build error, because `PopcornEffect` and the other new names do not exist.

- [ ] **Step 3: Add the names and readers to `UtilityMealBuffs`.** After `public const string ACORN_MASH = "Acorn Mash";` add:

```csharp
    public const string POPCORN = "Popcorn";
    public const string COTTON_CANDY = "Cotton Candy";
    public const string HOT_DOG = "Hot Dog";
```

Replace the three readers (use `replace_content`; keep the Cooler's doc comment above `ManaRegenBonus`):

```csharp
    public static decimal ExperienceBonus(Aisling? aisling)
        => Has(aisling, ACORN_MASH) || Has(aisling, POPCORN) ? EXPERIENCE_BONUS : 0m;
```

```csharp
    public static decimal HealthRegenBonus(Creature? creature)
        => Has(creature, HEARTHSIDE_PORRIDGE) || Has(creature, HOT_DOG) ? HEALTH_REGEN_POINTS : 0m;
```

```csharp
    public static decimal ManaRegenBonus(Creature? creature)
        => Has(creature, CUCUMBER_COOLER) || Has(creature, COTTON_CANDY) ? MANA_REGEN_POINTS : 0m;
```

The treats sit in the same conflict list as the meals they copy, so a player can never hold both, and an `||` never pays twice.

- [ ] **Step 4: Add the treats to `UtilityMealConflicts.ConflictingNameList`**, after `UtilityMealBuffs.ACORN_MASH`:

```csharp
        UtilityMealBuffs.ACORN_MASH,
        UtilityMealBuffs.POPCORN,
        UtilityMealBuffs.COTTON_CANDY,
        UtilityMealBuffs.HOT_DOG
```

- [ ] **Step 5: Create the three effects.** `PopcornEffect.cs` (the usings are the same as `AcornMashEffect.cs`):

```csharp
#region
using Chaos.Definitions;
using Chaos.Models.Data;
using Chaos.Models.World.Abstractions;
using Chaos.Scripting.Components.EffectComponents;
using Chaos.Scripting.Components.Execution;
using Chaos.Scripting.EffectScripts.Abstractions;
#endregion

namespace Chaos.Scripting.EffectScripts.Items.CookingMeals;

/// <summary>
///     A Halloween treat from the haunted theatre. Pays exactly what Acorn Mash pays and shares its slot.
/// </summary>
public class PopcornEffect : EffectBase, OverwritableEffectComponent.IOverwritableEffectComponentOptions
{
    public List<string> ConflictingEffectNames { get; init; } = UtilityMealConflicts.ConflictingNameList;

    protected override TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);

    protected Animation Animation { get; } = new()
    {
        TargetAnimation = 88,
        AnimationSpeed = 100
    };

    public override byte Icon => 72;
    public override string Name => UtilityMealBuffs.POPCORN;

    public override void OnApplied()
    {
        base.OnApplied();

        AislingSubject?.SendOrangeBarMessage("Salty, crunchy, gone too fast.");
        Subject.Animate(Animation);
    }

    public override void OnDispelled() => OnTerminated();

    public override void OnTerminated() => AislingSubject?.SendOrangeBarMessage("The popcorn bag is empty.");

    public override bool ShouldApply(Creature source, Creature target)
    {
        var execution = new ComponentExecutor(source, target).WithOptions(this)
                                                             .ExecuteAndCheck<OverwritableEffectComponent>();

        return execution is not null;
    }
}
```

`CottonCandyEffect.cs` is the same with these differences: class `CottonCandyEffect`, summary "A Halloween treat from the haunted theatre. Pays exactly what the Cucumber Cooler pays, for 30 minutes, and shares its slot.", `Name => UtilityMealBuffs.COTTON_CANDY`, applied message `"Spun sugar melts on the tongue."`, ended message `"The sugar rush fades."`.

`HotDogEffect.cs` is the same with: class `HotDogEffect`, summary "A Halloween treat from the haunted theatre. Pays exactly what Hearthside Porridge pays and shares its slot.", `Name => UtilityMealBuffs.HOT_DOG`, applied message `"Hot, greasy and just right."`, ended message `"The hot dog has settled."`.

Write all three files in full; do not leave any part as "same as".

- [ ] **Step 6: Run the tests.** Run the Verify command, then `--treenode-filter "/*/*/CookingMealStackingTests/*"`. Expected: all pass.

```json:metadata
{"files": ["Chaos/Definitions/UtilityMealBuffs.cs", "Chaos/Scripting/EffectScripts/Items/CookingMeals/UtilityMealConflicts.cs", "Chaos/Scripting/EffectScripts/Items/CookingMeals/PopcornEffect.cs", "Chaos/Scripting/EffectScripts/Items/CookingMeals/CottonCandyEffect.cs", "Chaos/Scripting/EffectScripts/Items/CookingMeals/HotDogEffect.cs", "Tests/Chaos.Tests/Effects/HalloweenTreatTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/HalloweenTreatTests/*\"", "acceptanceCriteria": ["treat bonuses 0.05 exp / 3 mana / 6 health", "treats and utility meals replace each other", "CookingMealStackingTests pass"], "modelTier": "mechanical"}
```

---

### Task 2: Treat recipes (server)

**Goal:** The three cooking recipes, learnable from their scrolls and listed in the cookbook.

**Files:**
- Modify: `S/Chaos/Definitions/BigFlags.cs` (`BigCookingRecipes`)
- Modify: `S/Chaos/Definitions/CookingRequirements.cs`
- Modify: `S/Chaos/Scripting/ItemScripts/RecipeItemScript.Cooking.cs`
- Modify: `S/Chaos/Scripting/DialogScripts/Temuair/Generic/RecipeLibraryScript.Cooking.cs`
- Test: `S/Tests/Chaos.Tests/Items/HalloweenTreatRecipeTests.cs`

**Acceptance Criteria:**
- [ ] Using `recipe_popcorn`, `recipe_cottoncandy` or `recipe_hotdog` sets its flag and uses the scroll up.
- [ ] `CookingRequirements.Recipes` has the three recipes: Popcorn 15 `acorn` + 1 `salt`; Cotton Candy 2 `sugar` + 1 `cotton`; Hot Dog 2 `rawmeat` + 1 `flour`; each with `FailureChance = 30`.
- [ ] `cotton` is in the `ExtraIngredient` list.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/HalloweenTreatRecipeTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** in `S/Tests/Chaos.Tests/Items/HalloweenTreatRecipeTests.cs`:

```csharp
#region
using Chaos.Definitions;
using Chaos.Scripting.ItemScripts;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Items;

public sealed class HalloweenTreatRecipeTests
{
    public static IEnumerable<Func<(string RecipeKey, BigFlagsValue<BigCookingRecipes> Flag)>> Recipes()
    {
        yield return () => ("recipe_popcorn", BigCookingRecipes.Popcorn);
        yield return () => ("recipe_cottoncandy", BigCookingRecipes.CottonCandy);
        yield return () => ("recipe_hotdog", BigCookingRecipes.HotDog);
    }

    [Test]
    [MethodDataSource(nameof(Recipes))]
    public void Reading_the_scroll_teaches_the_recipe_and_uses_the_scroll(
        (string RecipeKey, BigFlagsValue<BigCookingRecipes> Flag) recipe)
    {
        var cook = MockAisling.Create();
        var scroll = MockItem.Create(recipe.RecipeKey);
        cook.Inventory.TryAddToNextSlot(scroll);

        new RecipeItemScript(scroll).OnUse(cook);

        cook.Trackers.BigFlags.HasFlag(recipe.Flag).Should().BeTrue();
        cook.Inventory.CountOfByTemplateKey(recipe.RecipeKey).Should().Be(0);
    }

    [Test]
    public void Popcorn_takes_15_acorns_and_1_salt() => Ingredients(BigCookingRecipes.Popcorn)
                                                         .Should()
                                                         .BeEquivalentTo(new[] { ("acorn", 15), ("salt", 1) });

    [Test]
    public void Cotton_Candy_takes_2_sugar_and_1_cotton() => Ingredients(BigCookingRecipes.CottonCandy)
                                                              .Should()
                                                              .BeEquivalentTo(new[] { ("sugar", 2), ("cotton", 1) });

    [Test]
    public void A_Hot_Dog_takes_2_raw_meat_and_1_flour() => Ingredients(BigCookingRecipes.HotDog)
                                                             .Should()
                                                             .BeEquivalentTo(new[] { ("rawmeat", 2), ("flour", 1) });

    [Test]
    public void The_treats_fail_30_percent_of_the_time()
    {
        foreach (var flag in new[] { BigCookingRecipes.Popcorn, BigCookingRecipes.CottonCandy, BigCookingRecipes.HotDog })
            CookingRequirements.Recipes[flag].FailureChance.Should().Be(30);
    }

    [Test]
    public void Cotton_is_an_extra_ingredient()
        => CookingRequirements.CookingCategoryItems[FoodCategory.ExtraIngredient]
                              .Should()
                              .Contain("cotton");

    private static (string, int)[] Ingredients(BigFlagsValue<BigCookingRecipes> flag)
        => CookingRequirements.Recipes[flag]
                              .Slots
                              .SelectMany(slot => slot.AllowedIngredients!)
                              .Select(ingredient => (ingredient.TemplateKey, ingredient.Amount))
                              .ToArray();
}
```

If `MethodDataSource` with a tuple does not compile in this TUnit version, write three separate `[Test]` methods (one per recipe) with the same body instead.

- [ ] **Step 2: Run them to see them fail.** Expected: a build error, because the three flags do not exist.

- [ ] **Step 3: Append the flags** at the end of `BigCookingRecipes`, after `StonecapCasserole` and before the static constructor. Flags are stored by bit index, so append them and never insert them:

```csharp
    //the Halloween theatre treats -- appended for the same reason as the blocks above
    public static readonly BigFlagsValue<BigCookingRecipes> Popcorn;
    public static readonly BigFlagsValue<BigCookingRecipes> CottonCandy;
    public static readonly BigFlagsValue<BigCookingRecipes> HotDog;
```

- [ ] **Step 4: Add `cotton` to `CookingCategoryItems[FoodCategory.ExtraIngredient]`**, after `"emptycanteen"`:

```csharp
            "emptycanteen",

            //a fiber, not food; only Cotton Candy pins it, by name
            "cotton"
```

- [ ] **Step 5: Add the three recipes to `Recipes`**, after the `[BigCookingRecipes.StonecapCasserole]` entry:

```csharp
        [BigCookingRecipes.Popcorn] = new CookingRecipe
        {
            Name = "Popcorn",
            DisplayTemplateKey = "popcorn",
            DefaultOutputTemplateKey = "popcorn",
            SuccessMessage = "You have made Popcorn!",
            FailureChance = 30,
            Slots =
            [
                new IngredientSlot
                {
                    Category = FoodCategory.Fruit,
                    AmountPerPick = 15,
                    AllowedIngredients = [new SlotIngredient { TemplateKey = "acorn", Amount = 15 }]
                },
                new IngredientSlot
                {
                    Category = FoodCategory.ExtraIngredient,
                    AmountPerPick = 1,
                    AllowedIngredients = [new SlotIngredient { TemplateKey = "salt", Amount = 1 }]
                }
            ]
        },
        [BigCookingRecipes.CottonCandy] = new CookingRecipe
        {
            Name = "Cotton Candy",
            DisplayTemplateKey = "cottoncandy",
            DefaultOutputTemplateKey = "cottoncandy",
            SuccessMessage = "You have spun Cotton Candy!",
            FailureChance = 30,
            Slots =
            [
                new IngredientSlot
                {
                    Category = FoodCategory.ExtraIngredient,
                    AmountPerPick = 2,
                    AllowedIngredients = [new SlotIngredient { TemplateKey = "sugar", Amount = 2 }]
                },
                new IngredientSlot
                {
                    Category = FoodCategory.ExtraIngredient,
                    AmountPerPick = 1,
                    AllowedIngredients = [new SlotIngredient { TemplateKey = "cotton", Amount = 1 }]
                }
            ]
        },
        [BigCookingRecipes.HotDog] = new CookingRecipe
        {
            Name = "Hot Dog",
            DisplayTemplateKey = "hotdog",
            DefaultOutputTemplateKey = "hotdog",
            SuccessMessage = "You have cooked a Hot Dog!",
            FailureChance = 30,
            Slots =
            [
                new IngredientSlot
                {
                    Category = FoodCategory.Meat,
                    AmountPerPick = 2,
                    AllowedIngredients = [new SlotIngredient { TemplateKey = "rawmeat", Amount = 2 }]
                },
                new IngredientSlot
                {
                    Category = FoodCategory.ExtraIngredient,
                    AmountPerPick = 1,
                    AllowedIngredients = [new SlotIngredient { TemplateKey = "flour", Amount = 1 }]
                }
            ]
        },
```

- [ ] **Step 6: Teach them from the scrolls.** In `RecipeItemScript.Cooking.cs`, after the `case "recipe_stonecapcasserole":` block, add three cases in the same shape as `case "recipe_acornmash":`:

```csharp
            case "recipe_popcorn":
            {
                if (!source.Trackers.BigFlags.HasFlag(BigCookingRecipes.Popcorn))
                {
                    CookingRecipeLearn(source, ani, BigCookingRecipes.Popcorn, "Popcorn", $"{Subject.Template.TemplateKey}");

                    return true;
                }

                source.SendOrangeBarMessage("You already know this recipe.");

                return true;
            }
            case "recipe_cottoncandy":
            {
                if (!source.Trackers.BigFlags.HasFlag(BigCookingRecipes.CottonCandy))
                {
                    CookingRecipeLearn(source, ani, BigCookingRecipes.CottonCandy, "Cotton Candy", $"{Subject.Template.TemplateKey}");

                    return true;
                }

                source.SendOrangeBarMessage("You already know this recipe.");

                return true;
            }
            case "recipe_hotdog":
            {
                if (!source.Trackers.BigFlags.HasFlag(BigCookingRecipes.HotDog))
                {
                    CookingRecipeLearn(source, ani, BigCookingRecipes.HotDog, "Hot Dog", $"{Subject.Template.TemplateKey}");

                    return true;
                }

                source.SendOrangeBarMessage("You already know this recipe.");

                return true;
            }
```

Match the surrounding argument layout (one argument per line) if the file's style requires it.

- [ ] **Step 7: List them in the cookbook.** In `RecipeLibraryScript.Cooking.cs`, after the `StonecapCasserole` `HasFlag` block that adds a faux item, add:

```csharp
                if (source.Trackers.BigFlags.HasFlag(BigCookingRecipes.Popcorn))
                {
                    var item = ItemFactory.CreateFaux("popcorn");
                    Subject.Items.Add(ItemDetails.DisplayRecipe(item));
                }

                if (source.Trackers.BigFlags.HasFlag(BigCookingRecipes.CottonCandy))
                {
                    var item = ItemFactory.CreateFaux("cottoncandy");
                    Subject.Items.Add(ItemDetails.DisplayRecipe(item));
                }

                if (source.Trackers.BigFlags.HasFlag(BigCookingRecipes.HotDog))
                {
                    var item = ItemFactory.CreateFaux("hotdog");
                    Subject.Items.Add(ItemDetails.DisplayRecipe(item));
                }
```

After the `case "stonecapcasserole":` description block, add:

```csharp
                    case "popcorn":
                    {
                        Subject.Reply(source, $"{FauxItem.Template.Name} requires 15 Acorns and 1 Salt.", "cookbook");

                        return true;
                    }

                    case "cottoncandy":
                    {
                        Subject.Reply(source, $"{FauxItem.Template.Name} requires 2 Sugar and 1 Cotton.", "cookbook");

                        return true;
                    }

                    case "hotdog":
                    {
                        Subject.Reply(source, $"{FauxItem.Template.Name} requires 2 Raw Meat and 1 Flour.", "cookbook");

                        return true;
                    }
```

- [ ] **Step 8: Run the tests.** Run the Verify command, then `--treenode-filter "/*/*/RecipeItemScriptTests/*"`. Expected: all pass.

```json:metadata
{"files": ["Chaos/Definitions/BigFlags.cs", "Chaos/Definitions/CookingRequirements.cs", "Chaos/Scripting/ItemScripts/RecipeItemScript.Cooking.cs", "Chaos/Scripting/DialogScripts/Temuair/Generic/RecipeLibraryScript.Cooking.cs", "Tests/Chaos.Tests/Items/HalloweenTreatRecipeTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/HalloweenTreatRecipeTests/*\"", "acceptanceCriteria": ["scrolls teach the three recipes and are used up", "recipes have the spec ingredients and failure chance 30", "cotton is an extra ingredient"], "modelTier": "mechanical"}
```

---

### Task 3: The candy shop (server)

**Goal:** `CandyShopScript`, a buy-only dialog shop that charges `halloweencandy` and reads its items and prices from the merchant template's `candyShop` script vars.

**Files:**
- Create: `S/Chaos/Scripting/DialogScripts/Temuair/Generic/CandyShopScript.cs`
- Test: `S/Tests/Chaos.Tests/Merchants/CandyShopScriptTests.cs`

**Acceptance Criteria:**
- [ ] The item list shows every item in the vendor's `candyShop.items`, at its candy price.
- [ ] Buying charges price × amount in candy and gives the items.
- [ ] With too little candy, with items that do not fit, with an item not on the list, or with an amount of 0 or less, nothing changes.
- [ ] Gold is never touched.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter "/*/*/CandyShopScriptTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** in `S/Tests/Chaos.Tests/Merchants/CandyShopScriptTests.cs`:

```csharp
#region
using Chaos.Common.Abstractions;
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.Panel;
using Chaos.Models.Templates;
using Chaos.Models.World;
using Chaos.Scripting.Abstractions;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Scripting.DialogScripts.Temuair.Generic;
using Chaos.Services.Factories.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Moq;
#endregion

namespace Chaos.Tests.Merchants;

public sealed class CandyShopScriptTests
{
    private const int MASK_PRICE = 300;
    private const string CANDY = "halloweencandy";
    private const int GOLD_HELD = 5_000;

    private static IItemFactory Factory()
    {
        var factory = new Mock<IItemFactory>();

        factory.Setup(f => f.Create("skullmask", It.IsAny<ICollection<string>?>()))
               .Returns(() => Mask());

        factory.Setup(f => f.CreateFaux("skullmask", It.IsAny<ICollection<string>?>()))
               .Returns(() => Mask());

        return factory.Object;
    }

    private static Item Mask() => MockItem.Create("Skull Mask", templateSetup: t => t with { TemplateKey = "skullmask" });

    private static Merchant Vendor()
        => MockMerchant.Create(
            configureTemplate: template =>
            {
                var vars = new MockScriptVars();
                vars.Set(new Dictionary<string, int> { ["skullmask"] = MASK_PRICE }, "items");
                template.ScriptVars[CandyShopScript.SCRIPT_VARS_KEY] = vars;
            });

    private static Dialog Dialog(string templateKey, Merchant vendor, params string[] menuArgs)
    {
        var scriptProvider = new Mock<IScriptProvider>();

        scriptProvider.Setup(p => p.CreateScript<IDialogScript, Dialog>(It.IsAny<ICollection<string>>(), It.IsAny<Dialog>()))
                      .Returns(() => new Mock<IDialogScript>().Object);

        var template = new DialogTemplate
        {
            TemplateKey = templateKey,
            Text = "{0} {1} {2}",
            Type = ChaosDialogType.Normal,
            NextDialogKey = null,
            PrevDialogKey = null,
            Contextual = false,
            Options = [],
            ScriptKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ScriptVars = new Dictionary<string, IScriptVars>(StringComparer.OrdinalIgnoreCase),
            TextBoxLength = null,
            TextBoxPrompt = null,
            IllustrationIndex = 0
        };

        var dialog = new Dialog(template, vendor, scriptProvider.Object, new Mock<IDialogFactory>().Object);

        foreach (var arg in menuArgs)
            dialog.MenuArgs.Add(arg);

        return dialog;
    }

    private static Aisling Buyer(int candy, int maxWeight = 100)
    {
        var buyer = MockAisling.Create(setup: a => a.UserStatSheet.SetMaxWeight(maxWeight));
        buyer.Gold = GOLD_HELD;

        if (candy > 0)
            buyer.Inventory.TryAddToNextSlot(MockItem.Create(CANDY, candy, true, t => t with { MaxStacks = 1000 }));

        return buyer;
    }

    private static void Buy(Aisling buyer, string itemName, string amount)
    {
        var dialog = Dialog("candyshop_accepted", Vendor(), itemName, amount);

        new CandyShopScript(dialog, Factory(), MockScriptProvider.ItemCloner.Object, MockLogger.Create<CandyShopScript>().Object)
            .OnDisplaying(buyer);
    }

    [Test]
    public void The_list_shows_the_vendors_items_at_their_candy_prices()
    {
        var dialog = Dialog("candyshop_initial", Vendor());

        new CandyShopScript(dialog, Factory(), MockScriptProvider.ItemCloner.Object, MockLogger.Create<CandyShopScript>().Object)
            .OnDisplaying(Buyer(0));

        dialog.Items.Should().ContainSingle();
        dialog.Items[0].Item.Template.TemplateKey.Should().Be("skullmask");
        dialog.Items[0].Price.Should().Be(MASK_PRICE);
    }

    [Test]
    public void Buying_two_charges_twice_the_price_in_candy_and_no_gold()
    {
        var buyer = Buyer(MASK_PRICE * 2 + 7);

        Buy(buyer, "Skull Mask", "2");

        buyer.Inventory.CountOfByTemplateKey("skullmask").Should().Be(2);
        buyer.Inventory.CountOfByTemplateKey(CANDY).Should().Be(7);
        buyer.Gold.Should().Be(GOLD_HELD);
    }

    [Test]
    public void Too_little_candy_buys_nothing()
    {
        var buyer = Buyer(MASK_PRICE - 1);

        Buy(buyer, "Skull Mask", "1");

        buyer.Inventory.CountOfByTemplateKey("skullmask").Should().Be(0);
        buyer.Inventory.CountOfByTemplateKey(CANDY).Should().Be(MASK_PRICE - 1);
    }

    [Test]
    public void Items_that_do_not_fit_keep_the_candy()
    {
        var buyer = Buyer(MASK_PRICE, maxWeight: 0);

        Buy(buyer, "Skull Mask", "1");

        buyer.Inventory.CountOfByTemplateKey("skullmask").Should().Be(0);
        buyer.Inventory.CountOfByTemplateKey(CANDY).Should().Be(MASK_PRICE);
    }

    [Test]
    public void An_item_not_on_the_list_is_refused()
    {
        var buyer = Buyer(MASK_PRICE);

        Buy(buyer, "Witch's Hat", "1");

        buyer.Inventory.CountOfByTemplateKey(CANDY).Should().Be(MASK_PRICE);
    }

    [Test]
    public void An_amount_of_zero_is_refused()
    {
        var buyer = Buyer(MASK_PRICE);

        Buy(buyer, "Skull Mask", "0");

        buyer.Inventory.CountOfByTemplateKey("skullmask").Should().Be(0);
        buyer.Inventory.CountOfByTemplateKey(CANDY).Should().Be(MASK_PRICE);
    }
}
```

`MockItem.Create(name)` sets the template key to the lower-cased name ("skull mask"), so `Mask()` overrides it to `skullmask`. The mock candy stacks to 1000, as the real candy does; a stackable mock item otherwise stops at 100.

- [ ] **Step 2: Run them to see them fail.** Expected: a build error, because `CandyShopScript` does not exist.

- [ ] **Step 3: Write `CandyShopScript.cs`:**

```csharp
#region
using Chaos.Extensions.Common;
using Chaos.Models.Data;
using Chaos.Models.Menu;
using Chaos.Models.Panel;
using Chaos.Models.World;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.Theatre;
using Chaos.TypeMapper.Abstractions;
using Microsoft.Extensions.Logging;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.Generic;

/// <summary>
///     A buy-only shop paid in Halloween candy. One script serves every candy vendor: each vendor's merchant template
///     lists what it sells and the candy price of one, under <c>scriptVars.candyShop.items</c>.
/// </summary>
public class CandyShopScript : DialogScriptBase
{
    public const string SCRIPT_VARS_KEY = "candyShop";
    private const string INITIAL = "candyshop_initial";

    private readonly ICloningService<Item> ItemCloner;
    private readonly IItemFactory ItemFactory;
    private readonly ILogger<CandyShopScript> Logger;

    public CandyShopScript(
        Dialog subject,
        IItemFactory itemFactory,
        ICloningService<Item> itemCloner,
        ILogger<CandyShopScript> logger)
        : base(subject)
    {
        ItemFactory = itemFactory;
        ItemCloner = itemCloner;
        Logger = logger;
    }

    private Dictionary<string, int> Prices()
    {
        var prices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (Subject.DialogSource is Merchant merchant
            && merchant.Template.ScriptVars.TryGetValue(SCRIPT_VARS_KEY, out var vars)
            && vars.Get<Dictionary<string, int>>("items") is { } items)
            foreach (var (key, price) in items)
                prices[key] = price;

        return prices;
    }

    /// <summary>The menu sends back the item's display name, so the name is matched to a listed template key.</summary>
    private bool TryFind(string itemName, out string templateKey, out int price)
    {
        foreach (var (key, listed) in Prices())
            if (ItemFactory.CreateFaux(key).DisplayName.EqualsI(itemName))
            {
                templateKey = key;
                price = listed;

                return true;
            }

        templateKey = string.Empty;
        price = 0;

        return false;
    }

    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLower())
        {
            case INITIAL:
                foreach (var (key, price) in Prices())
                    Subject.Items.Add(ItemDetails.BuyWithTokensCustom(ItemFactory.CreateFaux(key), price));

                break;
            case "candyshop_amountrequest":
                OnDisplayingAmountRequest(source);

                break;
            case "candyshop_confirmation":
                OnDisplayingConfirmation(source);

                break;
            case "candyshop_accepted":
                OnDisplayingAccepted(source);

                break;
        }
    }

    private void OnDisplayingAmountRequest(Aisling source)
    {
        if (!TryFetchArgs<string>(out var itemName) || !TryFind(itemName, out _, out var price))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        Subject.InjectTextParameters(itemName, price);
    }

    private void OnDisplayingConfirmation(Aisling source)
    {
        if (!TryFetchArgs<string, int>(out var itemName, out var amount) || (amount <= 0) || !TryFind(itemName, out _, out var price))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        Subject.InjectTextParameters(amount, itemName, price * amount);
    }

    private void OnDisplayingAccepted(Aisling source)
    {
        if (!TryFetchArgs<string, int>(out var itemName, out var amount)
            || (amount <= 0)
            || !TryFind(itemName, out var templateKey, out var price))
        {
            Subject.ReplyToUnknownInput(source);

            return;
        }

        var item = ItemFactory.Create(templateKey);
        var cost = price * amount;

        if (!source.CanCarry((item, amount)))
        {
            Subject.Reply(source, "You can't carry that many.", INITIAL);

            return;
        }

        if (!source.Inventory.HasCountByTemplateKey(SpotlightChairs.CandyTemplateKey, cost)
            || !source.Inventory.RemoveQuantityByTemplateKey(SpotlightChairs.CandyTemplateKey, cost))
        {
            Subject.Reply(source, $"That's {cost} candy, and you haven't got it.", INITIAL);

            return;
        }

        item.Count = amount;

        foreach (var stack in item.FixStacks(ItemCloner))
            source.Inventory.TryAddToNextSlot(stack);

        Logger.LogInformation(
            "{@Player} bought {Amount} {ItemName} from {@Merchant} for {Candy} candy",
            source,
            amount,
            item.DisplayName,
            Subject.DialogSource,
            cost);

        Subject.Reply(source, $"{amount} {item.DisplayName} for {cost} candy. Pleasure doing business.", INITIAL);
    }
}
```

Fix the `using` lines to whatever the compiler needs (compare with `BuyNyxTokenShopScript.cs`); the names in the body are what matter. `RemoveQuantityByTemplateKey` must return `bool`; if it returns `void`, drop it from the `if` and call it on its own line after the `HasCountByTemplateKey` check.

- [ ] **Step 4: Run the tests.** Run the Verify command. Expected: all 6 pass. If the "do not fit" test passes trivially because `CanCarry` ignores weight for a 0 max weight, make the mock mask heavy instead (`templateSetup: t => t with { Weight = 50 }`, max weight 10) and say so in your report.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/Generic/CandyShopScript.cs", "Tests/Chaos.Tests/Merchants/CandyShopScriptTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/CandyShopScriptTests/*\"", "acceptanceCriteria": ["list shows the vendor's items at candy prices", "buying charges price x amount in candy, no gold", "too little candy / cannot carry / unlisted item / amount 0 change nothing"], "modelTier": "standard"}
```

---

### Task 4: Items, vendors and dialogs (Unora data)

**Goal:** The treat and recipe templates, the three vendors with their price lists, the shop and greeting dialogs, and the vendors placed in the haunted theatre, with content tests.

**Files:**
- Create: `U/Tools/HauntedTheatre/vendors.py`, `U/Tools/HauntedTheatre/test_vendors.py`
- Create: `U/Data/Configuration/Templates/Items/crafting/Cooking/Meals/popcorn.json`, `cottoncandy.json`, `hotdog.json`
- Create: `U/Data/Configuration/Templates/Items/Recipes/Cooking/recipe_popcorn.json`, `recipe_cottoncandy.json`, `recipe_hotdog.json`
- Create: `U/Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/stall_reaper.json`, `stall_spirit.json`, `stall_mummy.json`
- Create: `U/Data/Configuration/Templates/Dialogs/Temauir/generic/CandyShop/candyshop_initial.json`, `candyshop_amountrequest.json`, `candyshop_confirmation.json`, `candyshop_accepted.json`, `candyshop_declined.json`
- Create: `U/Data/Configuration/Templates/Dialogs/Temauir/Events/Halloween/theatre_vendors/stall_reaper_initial.json`, `stall_spirit_initial.json`, `stall_mummy_initial.json`
- Modify: `U/Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/merchants.json`

**Acceptance Criteria:**
- [ ] Every item key a vendor lists exists as an item template, every price matches spec section 2, and display names are unique within each vendor.
- [ ] The three vendors stand at (13, 7), (17, 11) and (17, 25) in `merchants.json`, and Thulin and the four ghosts are unchanged.
- [ ] Dialog texts meet the limits: options ≤ 35 characters, text ≤ 360, no em dashes.
- [ ] The treat templates use effect keys `Popcorn`, `CottonCandy`, `HotDog`.

**Verify:** `python -m pytest Tools/HauntedTheatre/test_vendors.py -q` (from `U`) → all pass

**Steps:**

- [ ] **Step 1: Write `Tools/HauntedTheatre/vendors.py`**, the one place the vendor numbers live:

```python
"""The theatre vendors: their stalls, tiles and candy prices. Spec: Chaos.Client
docs/superpowers/specs/2026-10-03-theatre-vendors-design.md. Every vendor tool and test reads these."""
from __future__ import annotations

RECIPE_PRICE = 25

RECIPES = {"recipe_popcorn": RECIPE_PRICE, "recipe_cottoncandy": RECIPE_PRICE, "recipe_hotdog": RECIPE_PRICE}

ACCESSORIES = {
    **dict.fromkeys(["fiendmask", "abominationmask", "spectremask", "dyeablecatears", "gasmask", "witchbuddy",
                     "reaperbuddy"], 500),
    **dict.fromkeys(["undeadhand", "ghosteffect", "catbowtail", "zombimask", "frankensteinmask", "skullmask",
                     "demonhorns", "demontail", "eingrenghost", "dungcap", "cyclopseye"], 300),
    "swampwitchpet": 150,
}

OVERCOATS = {
    **dict.fromkeys(["macabrehexeddress", "macabrehexedrobes", "fshadowcloak", "mshadowcloak", "macabredivinerobe",
                     "macabredivinegown", "macabrebattlearmor"], 300),
    **dict.fromkeys(["fpumpkincostume", "mpumpkincostume"], 150),
}

# key: (merchant template, stall picture, anchor = the stall's front-left cell, vendor tile, price list)
VENDORS = {
    "popcorn": ("stall_reaper", "stall_popcorn.png", (13, 6), (13, 7), RECIPES),
    "facepaint": ("stall_spirit", "stall_facepaint.png", (17, 10), (17, 11), ACCESSORIES),
    "fortune": ("stall_mummy", "stall_fortune.png", (17, 24), (17, 25), OVERCOATS),
}

FIRST_STRIP_ID = 9132  # the first stc id after the Endless Tower strips (9082-9131)
INVISIBLE_BLOCKER = 1  # left foreground 1 is never drawn and is a wall in sotp.dat, as in the mirror maze


def footprint(anchor: tuple[int, int]) -> list[tuple[int, int]]:
    """The four cells a stall covers: (x, y), (x+1, y), (x+1, y-1) and the back cell (x, y-1)."""
    x, y = anchor
    return [(x, y), (x + 1, y), (x + 1, y - 1), (x, y - 1)]
```

- [ ] **Step 2: Write the failing content test** `Tools/HauntedTheatre/test_vendors.py`:

```python
"""Content checks for the theatre vendors. Run from the Unora repo root: python -m pytest Tools/HauntedTheatre -q"""
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import vendors as v  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
TEMPLATES = ROOT / "Data" / "Configuration" / "Templates"
INSTANCE = ROOT / "Data" / "Configuration" / "MapInstances" / "Temuair" / "Events" / "Halloween" / "Suomi_Theatre_Halloween"


def _load(p: Path):
    return json.loads(p.read_text(encoding="utf-8-sig"))


def _items_by_key() -> dict[str, dict]:
    out = {}
    for p in (TEMPLATES / "Items").rglob("*.json"):
        try:
            t = _load(p)
        except ValueError:
            continue
        if isinstance(t, dict) and "templateKey" in t:
            out[t["templateKey"].lower()] = t
    return out


ITEMS = _items_by_key()


def _merchant(key: str) -> dict:
    return _load(TEMPLATES / "Merchants" / "Temauir" / "Events" / "Halloween" / f"{key}.json")


def _dialogs() -> dict[str, dict]:
    out = {}
    for p in (TEMPLATES / "Dialogs").rglob("*.json"):
        t = _load(p)
        out[t["templateKey"].lower()] = t
    return out


DIALOGS = _dialogs()


def test_every_vendor_lists_the_spec_prices():
    for merchant, _, _, _, prices in v.VENDORS.values():
        assert _merchant(merchant)["scriptVars"]["candyShop"]["items"] == prices


def test_every_listed_item_exists_with_a_unique_name_per_vendor():
    for merchant, _, _, _, prices in v.VENDORS.values():
        names = []
        for key in prices:
            assert key in ITEMS, f"{merchant} lists {key}, which has no item template"
            names.append(ITEMS[key]["name"].lower())
        assert len(names) == len(set(names)), f"{merchant} has two items with the same name"


def test_the_vendors_stand_beside_their_stalls():
    placed = {m["merchantTemplateKey"]: m["spawnPoint"] for m in _load(INSTANCE / "merchants.json")}
    for merchant, _, _, (x, y), _ in v.VENDORS.values():
        assert placed.get(merchant) == f"({x}, {y})"


def test_thulin_and_the_ghosts_are_still_there():
    keys = [m["merchantTemplateKey"] for m in _load(INSTANCE / "merchants.json")]
    assert keys.count("Thulin") == 1
    assert keys.count("theatre_ghost") == 4


def test_each_vendor_opens_its_greeting_and_the_greeting_leads_to_the_shop():
    for merchant, *_ in v.VENDORS.values():
        m = _merchant(merchant)
        greeting = m["scriptVars"]["showdialog"]["dialogKey"]
        assert greeting == f"{merchant}_initial"
        assert any(o["dialogKey"] == "candyshop_initial" for o in DIALOGS[greeting]["options"])


def test_the_shop_dialogs_run_the_candy_shop_script():
    for key in ("candyshop_initial", "candyshop_amountrequest", "candyshop_confirmation", "candyshop_accepted"):
        assert DIALOGS[key]["scriptKeys"] == ["candyShop"]


def test_dialog_text_limits():
    ours = [k for k in DIALOGS if k.startswith("candyshop_") or k.startswith("stall_")]
    assert len(ours) == 8
    for k in ours:
        d = DIALOGS[k]
        assert "\u2014" not in json.dumps(d, ensure_ascii=False)
        assert len(d["text"]) <= 360, k
        for o in d.get("options", []):
            assert len(o["optionText"]) <= 35, (k, o["optionText"])


def test_the_treats_use_their_effects_and_the_recipes_teach():
    for key, effect in (("popcorn", "Popcorn"), ("cottoncandy", "CottonCandy"), ("hotdog", "HotDog")):
        t = ITEMS[key]
        assert t["scriptVars"]["VitalityConsumable"]["effectKey"] == effect
        assert ITEMS[f"recipe_{key}"]["scriptKeys"] == ["pickupnotifier", "recipeItem"]
        assert re.match(r"^\[Recipe\] ", ITEMS[f"recipe_{key}"]["name"])
```

Run `python -m pytest Tools/HauntedTheatre/test_vendors.py -q`. Expected: failures (files missing).

- [ ] **Step 3: Write the treat templates.** `popcorn.json` (panel sprites are stand-ins until Task 9 gives the real icons):

```json
{
  "category": "Food",
  "name": "Popcorn",
  "templateKey": "popcorn",
  "panelSprite": 5251,
  "maxStacks": 100,
  "weight": 1,
  "sellValue": 200,
  "description": "+5% Experience on kills\n\nA Halloween treat from the Garamonde Theatre.\nConflicts with other utility meals.\nLasts 30 minutes.",
  "scriptKeys": [
    "VitalityConsumable",
    "pickupnotifier"
  ],
  "scriptVars": {
    "VitalityConsumable": {
      "effectKey": "Popcorn",
      "filter": "AliveOnly, SelfOnly"
    }
  }
}
```

`cottoncandy.json`: name `Cotton Candy`, templateKey `cottoncandy`, panelSprite `5253`, description `"+3% Mana regeneration\n\nA Halloween treat from the Garamonde Theatre.\nConflicts with other utility meals.\nLasts 30 minutes."`, effectKey `CottonCandy`. `hotdog.json`: name `Hot Dog`, templateKey `hotdog`, panelSprite `5259`, description `"+6% Health regeneration\n\nA Halloween treat from the Garamonde Theatre.\nConflicts with other utility meals.\nLasts 30 minutes."`, effectKey `HotDog`. Everything else as in `popcorn.json`. Write each file in full.

- [ ] **Step 4: Write the recipe scrolls.** `recipe_popcorn.json`:

```json
{
  "category": "Recipe",
  "color": "Default",
  "gender": "Unisex",
  "maxStacks": 10,
  "sellValue": 100,
  "description": "Salted acorns, popped over a ghost's candle. Teaches Popcorn.",
  "name": "[Recipe] Popcorn",
  "panelSprite": 1280,
  "scriptKeys": [
    "pickupnotifier",
    "recipeItem"
  ],
  "scriptVars": {},
  "templateKey": "recipe_popcorn"
}
```

`recipe_cottoncandy.json`: name `[Recipe] Cotton Candy`, description `"Sugar spun as fine as a shroud. Teaches Cotton Candy."`, templateKey `recipe_cottoncandy`. `recipe_hotdog.json`: name `[Recipe] Hot Dog`, description `"Meat in a bun, no questions asked. Teaches Hot Dog."`, templateKey `recipe_hotdog`. The sell value stays low (100 gold), so the scrolls are not a way to turn candy into gold.

- [ ] **Step 5: Write the vendors.** `stall_reaper.json`:

```json
{
  "itemsForSale": [],
  "itemsToBuy": [],
  "name": "Grim Kernel",
  "restockIntervalHrs": 24,
  "restockPct": 100,
  "scriptKeys": [
    "showdialog"
  ],
  "scriptVars": {
    "showdialog": {
      "dialogKey": "stall_reaper_initial"
    },
    "candyShop": {
      "items": {
        "recipe_popcorn": 25,
        "recipe_cottoncandy": 25,
        "recipe_hotdog": 25
      }
    }
  },
  "skillsToTeach": [],
  "spellsToTeach": [],
  "sprite": 68,
  "templateKey": "stall_reaper"
}
```

`stall_spirit.json`: name `Pale Wisp`, sprite `1250`, dialogKey `stall_spirit_initial`, templateKey `stall_spirit`, `candyShop.items` = every key and price in `vendors.ACCESSORIES`. `stall_mummy.json`: name `Madame Linen`, sprite `188`, dialogKey `stall_mummy_initial`, templateKey `stall_mummy`, `candyShop.items` = `vendors.OVERCOATS`. Write the price lists out in full, in the order `vendors.py` lists them.

- [ ] **Step 6: Write the shop dialogs** in `Templates/Dialogs/Temauir/generic/CandyShop/`:

`candyshop_initial.json`:
```json
{
  "nextDialogKey": "candyshop_amountrequest",
  "options": [],
  "scriptKeys": [
    "candyShop"
  ],
  "scriptVars": {},
  "templateKey": "candyshop_initial",
  "text": "Candy only. What catches your eye?",
  "type": "ShowItems"
}
```

`candyshop_amountrequest.json`:
```json
{
  "contextual": true,
  "nextDialogKey": "candyshop_confirmation",
  "options": [],
  "scriptKeys": [
    "candyShop"
  ],
  "scriptVars": {},
  "templateKey": "candyshop_amountrequest",
  "text": "How many {ItemName}? They are {Cost} candy each.",
  "type": "MenuTextEntryWithArgs"
}
```

`candyshop_confirmation.json`:
```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "candyshop_accepted",
      "optionText": "Yes"
    },
    {
      "dialogKey": "candyshop_declined",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "candyShop"
  ],
  "scriptVars": {},
  "templateKey": "candyshop_confirmation",
  "text": "{Amount} {ItemName}? That will be {Cost} candy.",
  "type": "MenuWithArgs"
}
```

`candyshop_accepted.json`:
```json
{
  "contextual": true,
  "nextDialogKey": "candyshop_initial",
  "options": [],
  "scriptKeys": [
    "candyShop"
  ],
  "scriptVars": {},
  "templateKey": "candyshop_accepted",
  "text": "Skip",
  "type": "Normal"
}
```

`candyshop_declined.json`:
```json
{
  "nextDialogKey": "candyshop_initial",
  "options": [],
  "prevDialogKey": "candyshop_confirmation",
  "scriptKeys": [],
  "scriptVars": {},
  "templateKey": "candyshop_declined",
  "text": "Suit yourself. The dead are patient.",
  "type": "Normal"
}
```

- [ ] **Step 7: Write the greetings** in `Templates/Dialogs/Temauir/Events/Halloween/theatre_vendors/`. `stall_reaper_initial.json`:

```json
{
  "options": [
    {
      "dialogKey": "candyshop_initial",
      "optionText": "Browse recipes"
    }
  ],
  "scriptKeys": [],
  "scriptVars": {},
  "templateKey": "stall_reaper_initial",
  "text": "Popcorn, cotton candy, hot dogs... I sell the recipes, not the food. The living cook for themselves. Candy only.",
  "type": "DialogMenu"
}
```

`stall_spirit_initial.json`: option `"Browse accessories"`, text `"Masks, horns, little friends to follow you home. Everything the boxes hide, for a price in candy."`. `stall_mummy_initial.json`: option `"Browse costumes"`, text `"Robes, cloaks and gowns, wrapped with care. Pay in candy and I will tell your fortune for free... it is grim."`. Both otherwise as above.

- [ ] **Step 8: Place the vendors.** Append to `merchants.json` (keep the existing five entries):

```json
  {
    "blackList": [],
    "direction": "Down",
    "extraScriptKeys": [],
    "merchantTemplateKey": "stall_reaper",
    "spawnPoint": "(13, 7)"
  },
  {
    "blackList": [],
    "direction": "Down",
    "extraScriptKeys": [],
    "merchantTemplateKey": "stall_spirit",
    "spawnPoint": "(17, 11)"
  },
  {
    "blackList": [],
    "direction": "Down",
    "extraScriptKeys": [],
    "merchantTemplateKey": "stall_mummy",
    "spawnPoint": "(17, 25)"
  }
```

- [ ] **Step 9: Run the test.** Run the Verify command. Expected: all pass.

```json:metadata
{"files": ["Tools/HauntedTheatre/vendors.py", "Tools/HauntedTheatre/test_vendors.py", "Data/Configuration/Templates/Items/crafting/Cooking/Meals/popcorn.json", "Data/Configuration/Templates/Items/crafting/Cooking/Meals/cottoncandy.json", "Data/Configuration/Templates/Items/crafting/Cooking/Meals/hotdog.json", "Data/Configuration/Templates/Items/Recipes/Cooking/recipe_popcorn.json", "Data/Configuration/Templates/Items/Recipes/Cooking/recipe_cottoncandy.json", "Data/Configuration/Templates/Items/Recipes/Cooking/recipe_hotdog.json", "Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/stall_reaper.json", "Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/stall_spirit.json", "Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/stall_mummy.json", "Data/Configuration/Templates/Dialogs/Temauir/generic/CandyShop/", "Data/Configuration/Templates/Dialogs/Temauir/Events/Halloween/theatre_vendors/", "Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/merchants.json"], "verifyCommand": "python -m pytest Tools/HauntedTheatre/test_vendors.py -q", "acceptanceCriteria": ["listed items exist, prices match the spec, names unique per vendor", "vendors at (13,7) (17,11) (17,25); Thulin and ghosts unchanged", "dialog limits met", "treat effect keys Popcorn/CottonCandy/HotDog"], "modelTier": "mechanical"}
```

---

### Task 5: Cut the stalls into strips (Unora tool)

**Goal:** `stall_strips.py` cuts a 112 × 150 stall picture into the four CamPrep "Prop2" strips and writes the three vendor stalls' strips to `Tools/HauntedTheatre/stalls/`.

**Files:**
- Create: `U/Tools/HauntedTheatre/stall_strips.py`, `U/Tools/HauntedTheatre/test_stall_strips.py`
- Create (generated): `U/Tools/HauntedTheatre/stalls/{popcorn,facepaint,fortune}_{1,2,3,4}.png` and `stalls/sources/stall_{popcorn,facepaint,fortune}.png` (copies of the Desktop pictures)

**Acceptance Criteria:**
- [ ] Cutting `stall_fortune.png`, `stall_cocoa.png` and `stall_games.png` reproduces their existing `cam/` strips pixel for pixel.
- [ ] Cutting `stall_ringtoss.png` and `stall_souvenirs.png` reproduces the cam strips' sizes and transparency exactly (their colors differ slightly because CamPrep reduced their palette).
- [ ] The 12 strips exist in `stalls/`; strips 1 and 4 have no bottom padding, and strips 2 and 3 have 14 transparent rows at the bottom.

**Verify:** `python -m pytest Tools/HauntedTheatre/test_stall_strips.py -q` → all pass

**Steps:**

- [ ] **Step 1: Copy the three sources into the repo** so the tool does not depend on the Desktop:

```powershell
New-Item -ItemType Directory -Force Tools\HauntedTheatre\stalls\sources
Copy-Item "C:\Users\Michael\Desktop\Halloween\Stalls\stall_popcorn.png","C:\Users\Michael\Desktop\Halloween\Stalls\stall_facepaint.png","C:\Users\Michael\Desktop\Halloween\Stalls\stall_fortune.png" Tools\HauntedTheatre\stalls\sources\
```

- [ ] **Step 2: Write the failing test** `Tools/HauntedTheatre/test_stall_strips.py`:

```python
"""Tests for the stall cutter. Run from the Unora repo root: python -m pytest Tools/HauntedTheatre -q

The cam/ cuts on the Desktop were made by CamPrep, whose engine is not in the repo; matching them is the proof that
this cutter places strips the way the map expects."""
import sys
from pathlib import Path

import numpy as np
import pytest
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))

import stall_strips as s  # noqa: E402

DESKTOP = Path(r"C:\Users\Michael\Desktop\Halloween\Stalls")
HERE = Path(__file__).resolve().parent


def _cam(name: str) -> list[np.ndarray]:
    files = sorted((DESKTOP / "cam").glob(f"stall_{name}_0*.png"))
    return [np.array(Image.open(f).convert("RGBA")) for f in files]


def _cut(name: str) -> list[np.ndarray]:
    return [np.array(im) for im in s.cut(Image.open(DESKTOP / f"stall_{name}.png"))]


needs_desktop = pytest.mark.skipif(not (DESKTOP / "cam").is_dir(), reason="the Desktop stall cuts are not here")


@needs_desktop
@pytest.mark.parametrize("name", ["fortune", "cocoa", "games"])
def test_matches_camprep_exactly(name):
    mine, theirs = _cut(name), _cam(name)
    assert len(theirs) == 4
    for m, t in zip(mine, theirs):
        assert m.shape == t.shape
        assert np.array_equal(m[:, :, 3] > 0, t[:, :, 3] > 0)
        opaque = m[:, :, 3] > 0
        assert np.array_equal(m[opaque], t[opaque])


@needs_desktop
@pytest.mark.parametrize("name", ["ringtoss", "souvenirs"])
def test_matches_camprep_shape_where_camprep_reduced_colors(name):
    for m, t in zip(_cut(name), _cam(name)):
        assert m.shape == t.shape
        assert np.array_equal(m[:, :, 3] > 0, t[:, :, 3] > 0)


def test_strip_layout():
    im = Image.new("RGBA", (112, 150), (0, 0, 0, 0))
    for x in range(112):
        for y in range(40, 150):
            im.putpixel((x, y), (200, 100, 50, 255))
    strips = s.cut(im)
    assert [st.size for st in strips] == [(28, 110), (28, 124), (28, 124), (28, 110)]
    assert all(strips[1].getpixel((0, 123 - k))[3] == 0 for k in range(14))


def test_a_picture_of_the_wrong_size_is_refused():
    with pytest.raises(ValueError):
        s.cut(Image.new("RGBA", (100, 150)))


def test_the_vendor_strips_are_written():
    for key in ("popcorn", "facepaint", "fortune"):
        for k in range(1, 5):
            assert (HERE / "stalls" / f"{key}_{k}.png").is_file()
```

Run it. Expected: an import error, because `stall_strips` does not exist.

- [ ] **Step 3: Write `Tools/HauntedTheatre/stall_strips.py`:**

```python
"""Cut a 112x150 stall picture into four 28 px wall strips, the "Prop2" layout CamPrep uses.
Run from the Unora repo root:

    python Tools/HauntedTheatre/stall_strips.py      # writes stalls/<key>_<1-4>.png for every vendor stall

A stall covers a 2x2 block whose front-left cell is (x, y). Strip 1 is the left foreground of (x, y), strips 2 and 3
the left and right foreground of (x+1, y), strip 4 the right foreground of (x+1, y-1). Each strip is one 28 px column
of the picture from its first visible row down. (x+1, y) sits 14 px lower on screen than the other two cells, so its
strips get 14 transparent rows at the bottom: the game bottom-aligns a foreground strip to its own cell.
"""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))

import vendors  # noqa: E402

WIDTH, HEIGHT = 112, 150
STRIP = 28
BOTTOM_PAD = (0, 14, 14, 0)
HERE = Path(__file__).resolve().parent
OUT = HERE / "stalls"


def cut(picture: Image.Image) -> list[Image.Image]:
    if picture.size != (WIDTH, HEIGHT):
        raise ValueError(f"a stall picture is {WIDTH}x{HEIGHT}, not {picture.size[0]}x{picture.size[1]}")
    px = np.array(picture.convert("RGBA"))
    strips = []
    for k, pad in enumerate(BOTTOM_PAD):
        column = px[:, k * STRIP:(k + 1) * STRIP]
        rows = np.where(column[:, :, 3].any(axis=1))[0]
        top = int(rows.min()) if rows.size else HEIGHT - 1
        strip = np.concatenate([column[top:], np.zeros((pad, STRIP, 4), np.uint8)])
        strips.append(Image.fromarray(strip, "RGBA"))
    return strips


def main() -> int:
    OUT.mkdir(exist_ok=True)
    for key, (_, picture, *_rest) in vendors.VENDORS.items():
        for k, strip in enumerate(cut(Image.open(OUT / "sources" / picture)), start=1):
            strip.save(OUT / f"{key}_{k}.png")
            print(f"{key}_{k}.png {strip.size[0]}x{strip.size[1]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 4: Generate the strips:** `python Tools/HauntedTheatre/stall_strips.py`. Expected: 12 lines, every width 28.

- [ ] **Step 5: Run the test.** Run the Verify command. Expected: all pass.

```json:metadata
{"files": ["Tools/HauntedTheatre/stall_strips.py", "Tools/HauntedTheatre/test_stall_strips.py", "Tools/HauntedTheatre/stalls/"], "verifyCommand": "python -m pytest Tools/HauntedTheatre/test_stall_strips.py -q", "acceptanceCriteria": ["fortune/cocoa/games cuts match CamPrep pixel for pixel", "ringtoss/souvenirs match in size and transparency", "12 vendor strips written with the right padding"], "modelTier": "mechanical"}
```

---

### Task 6: Import the strips into `ia.dat` and `sotp.dat` (Unora tool)

**Goal:** `import_stalls.cs` adds the 12 strips to a copy of the local client `ia.dat` as `stc09132.hpf` to `stc09143.hpf`. It marks them as walls in that copy's `sotp.dat` and in the server worktree's `sotp.dat`, and records the ids in `stalls/ids.json`.

**Files:**
- Create: `U/Tools/HauntedTheatre/import_stalls.cs`
- Create (generated): `U/Tools/HauntedTheatre/stalls/ids.json`
- Modify (generated): `S/Chaos/Resources/sotp.dat`
- Output (not in git): `C:\Users\Michael\Desktop\Theatre Vendors 2026-10-03\ia.dat`, `sotp.server.dat`

**Acceptance Criteria:**
- [ ] The output `ia.dat` holds `stc09132.hpf` … `stc09143.hpf`. Each is 28 px wide and as tall as its source strip, and one new `stc####.pal` palette covers them.
- [ ] The output `ia.dat`'s `sotp.dat` and the server worktree's `sotp.dat` have flag 15 at ids 9132–9143. No other byte changes, and byte 0 (id 1) is 15 in both.
- [ ] `stalls/ids.json` maps each strip (`popcorn_1` … `fortune_4`) to its id, in vendor order popcorn, facepaint, fortune.
- [ ] The tool refuses to run if the source `ia.dat`'s first free strip id is not 9132, and never writes into the source folder.

**Verify:** `dotnet run Tools/HauntedTheatre/import_stalls.cs -- --verify "C:\Users\Michael\Desktop\Theatre Vendors 2026-10-03"` → prints `ok: 12 strips 9132-9143, walls in both sotp copies`

**Steps:**

- [ ] **Step 1: Write `Tools/HauntedTheatre/import_stalls.cs`.** Its strip import follows `Tools/Tower/TowerArt/Importer.cs` (quantize, one `stc` palette, `stcpal.tbl`, flags in both `sotp.dat` copies):

```csharp
#:project C:/Users/Michael/Documents/GitHub/dalib/DALib/DALib.csproj
// Imports the theatre stall strips into a copy of the client's ia.dat. Run from the Unora repo root:
//   dotnet run Tools/HauntedTheatre/import_stalls.cs -- --server-sotp <server repo>/Chaos/Resources/sotp.dat [--source <client folder>] [--out <folder>]
//   dotnet run Tools/HauntedTheatre/import_stalls.cs -- --verify <out folder> [--server-sotp <path>]
// The source folder is read only. The output folder gets ia.dat and sotp.server.dat; copy sotp.server.dat over the
// server's Chaos/Resources/sotp.dat. Strip ids come after the Endless Tower strips; see stalls/ids.json.
using System.Text.Json;
using DALib.Abstractions;
using DALib.Data;
using DALib.Drawing;
using DALib.Extensions;
using DALib.Utility;
using SkiaSharp;

const byte WALL = 0x0F;
const int FIRST_ID = 9132;
string[] keys = ["popcorn", "facepaint", "fortune"];

var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
var source = Path.Combine(documents, "Unora", "Unora Files");
var outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Theatre Vendors 2026-10-03");
string? serverSotpPath = null;
string? verifyDir = null;

for (var i = 0; i < args.Length; i++)
    switch (args[i])
    {
        case "--source": source = args[++i]; break;
        case "--out": outDir = args[++i]; break;
        case "--server-sotp": serverSotpPath = args[++i]; break;
        case "--verify": verifyDir = args[++i]; break;
        default: Console.Error.WriteLine($"unknown argument {args[i]}"); return 2;
    }

var stalls = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "HauntedTheatre", "stalls");
var names = keys.SelectMany(k => Enumerable.Range(1, 4).Select(n => $"{k}_{n}")).ToArray();

if (verifyDir is not null)
    return Verify(verifyDir, serverSotpPath);

if (serverSotpPath is null) { Console.Error.WriteLine("--server-sotp is required"); return 2; }
if (Path.GetFullPath(outDir).TrimEnd('\\') == Path.GetFullPath(source).TrimEnd('\\'))
{ Console.Error.WriteLine("--out must not be the source folder"); return 2; }

using var ia = DataArchive.FromFile(Path.Combine(source, "ia.dat"), memoryMapped: false);
var existing = ia.Where(e => e.EntryName.StartsWith("stc", StringComparison.OrdinalIgnoreCase)
                             && e.EntryName.EndsWith(".hpf", StringComparison.OrdinalIgnoreCase))
                 .Select(e => e.TryGetNumericIdentifier(out var id) ? id : -1)
                 .ToHashSet();
var firstFree = 1;
while (existing.Contains(firstFree)) firstFree++;

if (firstFree != FIRST_ID)
{
    Console.Error.WriteLine($"{source}\\ia.dat has its first free strip at {firstFree}, not {FIRST_ID}. "
                            + "It must hold the Endless Tower strips and nothing after them.");
    return 1;
}

for (var id = FIRST_ID; id < FIRST_ID + names.Length; id++)
    if (existing.Contains(id)) { Console.Error.WriteLine($"stc{id:D5}.hpf already exists"); return 1; }

var images = names.Select(n => SKImage.FromEncodedData(Path.Combine(stalls, $"{n}.png"))
                               ?? throw new InvalidOperationException($"cannot read {n}.png")).ToList();
if (images.Any(im => im.Width != 28)) { Console.Error.WriteLine("every strip must be 28 px wide"); return 1; }

ImageProcessor.PreserveNonTransparentBlacks(images);
using var quantized = ImageProcessor.QuantizeMultiple(QuantizerOptions.Default, images.ToArray());
var stc = PaletteLookup.FromArchive("stc", ia);
var paletteId = stc.GetNextPaletteId();
var sotp = ia.TryGetValue("sotp.dat", out var sotpEntry) ? sotpEntry.ToSpan().ToArray() : [];
var serverSotp = File.ReadAllBytes(serverSotpPath);
var ids = new Dictionary<string, int>();

for (var i = 0; i < names.Length; i++)
{
    var id = FIRST_ID + i;
    ia.Patch($"stc{id:D5}.hpf", new HpfFile(new byte[8], quantized.Entity[i].GetPalettizedPixelData(quantized.Palette)));
    stc.Table.Add(id + 1, paletteId);
    SetFlag(ref sotp, id);
    SetFlag(ref serverSotp, id);
    ids[names[i]] = id;
}

stc.Palettes[paletteId] = quantized.Palette;
ia.Patch($"stc{paletteId:D4}.pal", quantized.Palette);
ia.Patch("stcpal.tbl", stc.Table);
ia.Patch("sotp.dat", new RawEntry(sotp));

Directory.CreateDirectory(outDir);
ia.Save(Path.Combine(outDir, "ia.dat"));
File.WriteAllBytes(Path.Combine(outDir, "sotp.server.dat"), serverSotp);
File.WriteAllText(Path.Combine(stalls, "ids.json"), JsonSerializer.Serialize(ids, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"wrote {outDir}: strips {FIRST_ID}-{FIRST_ID + names.Length - 1}, palette stc{paletteId:D4}.pal");
return 0;

void SetFlag(ref byte[] bytes, int id)
{
    if (bytes.Length < id) Array.Resize(ref bytes, id);
    bytes[id - 1] = WALL;
}

int Verify(string dir, string? serverPath)
{
    using var built = DataArchive.FromFile(Path.Combine(dir, "ia.dat"), memoryMapped: false);
    var problems = new List<string>();
    var clientSotp = built["sotp.dat"].ToSpan().ToArray();
    var server = File.ReadAllBytes(serverPath ?? Path.Combine(dir, "sotp.server.dat"));

    for (var i = 0; i < names.Length; i++)
    {
        var id = FIRST_ID + i;
        if (!built.TryGetValue($"stc{id:D5}.hpf", out var entry)) { problems.Add($"stc{id:D5}.hpf missing"); continue; }
        var hpf = HpfFile.FromEntry(entry);
        using var strip = SKImage.FromEncodedData(Path.Combine(stalls, $"{names[i]}.png"));
        if (hpf.PixelWidth != 28 || hpf.PixelHeight != strip.Height)
            problems.Add($"stc{id:D5}.hpf is {hpf.PixelWidth}x{hpf.PixelHeight}, expected 28x{strip.Height}");
        if (clientSotp[id - 1] != WALL) problems.Add($"client sotp {id} is {clientSotp[id - 1]}");
        if (server[id - 1] != WALL) problems.Add($"server sotp {id} is {server[id - 1]}");
    }

    if (clientSotp[0] != WALL || server[0] != WALL) problems.Add("id 1 is not a wall, so the invisible blocker would not block");
    foreach (var p in problems) Console.Error.WriteLine(p);
    if (problems.Count == 0) Console.WriteLine($"ok: {names.Length} strips {FIRST_ID}-{FIRST_ID + names.Length - 1}, walls in both sotp copies");
    return problems.Count == 0 ? 0 : 1;
}

sealed class RawEntry(byte[] data) : ISavable
{
    public void Save(string path)
    {
        using var file = File.Create(path);
        Save(file);
    }

    public void Save(Stream stream) => stream.Write(data, 0, data.Length);
}
```

If a DALib name differs (for example `HpfFile.FromEntry`, `PixelWidth`/`PixelHeight`), look it up in `C:\Users\Michael\Documents\GitHub\dalib\DALib\Drawing\HpfFile.cs` and use the real one. `TowerArt/Importer.cs` shows the import calls already compile.

- [ ] **Step 2: Run the import** (from `U`):

```powershell
dotnet run Tools/HauntedTheatre/import_stalls.cs -- --server-sotp C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server\Chaos\Resources\sotp.dat
```

Expected: `wrote C:\Users\Michael\Desktop\Theatre Vendors 2026-10-03: strips 9132-9143, palette stc0324.pal` (the palette number may differ).

- [ ] **Step 3: Copy the server flags in and check that only 12 bytes changed:**

```powershell
Copy-Item "C:\Users\Michael\Desktop\Theatre Vendors 2026-10-03\sotp.server.dat" C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server\Chaos\Resources\sotp.dat
python -c "import subprocess;old=subprocess.run(['git','-C',r'C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server','show','HEAD:Chaos/Resources/sotp.dat'],capture_output=True).stdout;new=open(r'C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server\Chaos\Resources\sotp.dat','rb').read();print(len(old),len(new),[i+1 for i in range(max(len(old),len(new))) if (old[i:i+1] or b'\0')!=(new[i:i+1] or b'\0')])"
```

Expected: both lengths `24403`, and the changed ids are exactly `[9132, …, 9143]`.

- [ ] **Step 4: Verify.** Run the Verify command. Expected: the `ok:` line.

```json:metadata
{"files": ["Tools/HauntedTheatre/import_stalls.cs", "Tools/HauntedTheatre/stalls/ids.json", "Chaos/Resources/sotp.dat"], "verifyCommand": "dotnet run Tools/HauntedTheatre/import_stalls.cs -- --verify \"C:\\Users\\Michael\\Desktop\\Theatre Vendors 2026-10-03\"", "acceptanceCriteria": ["ia.dat holds stc09132-09143 at the strips' sizes with one new stc palette", "both sotp copies have 15 at 9132-9143 and nothing else changed", "ids.json written", "refuses a source whose first free id is not 9132; never writes the source"], "modelTier": "standard"}
```

---

### Task 7: Put the stalls on the map (Unora tool)

**Goal:** `place_stalls.py` writes the stall strips and back-cell blockers into `lod10269.map` without moving anything else, and checks that every door, Thulin, the stage tiles, the ghost spawns and the vendor tiles can still reach each other. A render shows the result.

**Files:**
- Create: `U/Tools/HauntedTheatre/place_stalls.py`, `U/Tools/HauntedTheatre/test_place_stalls.py`, `U/Tools/HauntedTheatre/README.md`
- Modify: `U/Tools/MirrorMaze/render_map.cs` (optional 5th argument: the data folder)
- Modify (generated): `U/Data/Configuration/MapData/lod10269.map`

**Acceptance Criteria:**
- [ ] Only the 12 stall cells change in `lod10269.map`. On each stall, `(x, y)` left = strip 1, `(x+1, y)` left/right = strips 2/3, `(x+1, y-1)` right = strip 4, and `(x, y-1)` left = 1.
- [ ] The tool refuses if a stall cell is not open floor with no foreground, or if a protected spot loses reach to the town door (19, 16).
- [ ] Running the tool twice gives the same file. The second run finds the stalls already in place and changes nothing.
- [ ] A render of the map with the new `ia.dat` shows the three stalls in place.

**Verify:** `python -m pytest Tools/HauntedTheatre -q` → all pass, then `python Tools/HauntedTheatre/place_stalls.py --check` → `no problems`

**Steps:**

- [ ] **Step 1: Write the failing test** `Tools/HauntedTheatre/test_place_stalls.py`:

```python
"""Tests for the stall placer. Run from the Unora repo root: python -m pytest Tools/HauntedTheatre -q"""
import copy
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import place_stalls as p  # noqa: E402
import vendors as v  # noqa: E402

IDS = p.load_ids()


def _bare_map():
    """The haunted theatre with any stalls taken back out, so the tests do not depend on the map's current state."""
    tiles = p.read_map()
    for _, _, anchor, _, _ in v.VENDORS.values():
        for x, y in v.footprint(anchor):
            tiles[y][x][1] = tiles[y][x][2] = 0
    return tiles


def test_placing_sets_exactly_the_stall_cells():
    before = _bare_map()
    after = p.place(copy.deepcopy(before), IDS)
    changed = {(x, y) for y in range(p.HEIGHT) for x in range(p.WIDTH) if before[y][x] != after[y][x]}
    expected = {cell for _, _, anchor, _, _ in v.VENDORS.values() for cell in v.footprint(anchor)}
    assert changed == expected


def test_strip_order_on_the_popcorn_stall():
    after = p.place(_bare_map(), IDS)
    x, y = v.VENDORS["popcorn"][2]
    assert after[y][x][1] == IDS["popcorn_1"]
    assert after[y][x + 1][1:] == [IDS["popcorn_2"], IDS["popcorn_3"]]
    assert after[y - 1][x + 1][2] == IDS["popcorn_4"]
    assert after[y - 1][x][1] == v.INVISIBLE_BLOCKER


def test_backgrounds_never_change():
    before = _bare_map()
    after = p.place(copy.deepcopy(before), IDS)
    assert [[t[0] for t in row] for row in before] == [[t[0] for t in row] for row in after]


def test_the_layout_has_no_problems():
    assert p.problems(_bare_map(), p.place(_bare_map(), IDS)) == []


def test_a_stall_on_a_prop_is_a_problem():
    before = _bare_map()
    x, y = v.VENDORS["fortune"][2]
    before[y][x][1] = 6075
    assert any("already has" in m for m in p.problems(before, p.place(copy.deepcopy(before), IDS)))


def test_a_stall_cutting_off_the_vendor_is_a_problem():
    before = _bare_map()
    after = p.place(copy.deepcopy(before), IDS)
    vx, vy = v.VENDORS["popcorn"][3]
    for nx, ny in ((vx + 1, vy), (vx - 1, vy), (vx, vy + 1)):
        after[ny][nx][1] = v.INVISIBLE_BLOCKER
    assert any("reach" in m for m in p.problems(before, after))


def test_placing_twice_changes_nothing():
    once = p.place(_bare_map(), IDS)
    assert p.place(copy.deepcopy(once), IDS) == once
```

Run it. Expected: an import error.

- [ ] **Step 2: Write `Tools/HauntedTheatre/place_stalls.py`:**

```python
"""Put the vendor stalls into the haunted theatre map (lod10269.map). Run from the Unora repo root:

    python Tools/HauntedTheatre/place_stalls.py --check   # build in memory, print problems, write nothing
    python Tools/HauntedTheatre/place_stalls.py           # write lod10269.map

Touches only the stall cells (see vendors.footprint), so hand edits elsewhere in the map survive. Unlike
decorate_theatre.py, it reads the map as it is now. It needs the strip ids from stalls/ids.json (import_stalls.cs)
and a sotp.dat that already marks them as walls: pass --sotp, or set SOTP_DAT, to the server worktree's copy.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import vendors as v  # noqa: E402


def _sotp_arg() -> None:
    """decorate_theatre reads SOTP_DAT when it is imported, so --sotp has to be in the environment first."""
    for i, a in enumerate(sys.argv):
        if a == "--sotp" and i + 1 < len(sys.argv):
            os.environ["SOTP_DAT"] = sys.argv[i + 1]


_sotp_arg()
import decorate_theatre as d  # noqa: E402

WIDTH, HEIGHT = d.WIDTH, d.HEIGHT
MAP = d.CONFIG / "MapData" / f"lod{d.MAP_ID}.map"
INSTANCE = d.INSTANCE_DIR
TOWN_DOOR = d.TOWN_DOOR


def load_ids() -> dict[str, int]:
    return json.loads((HERE / "stalls" / "ids.json").read_text(encoding="utf-8"))


def read_map() -> list:
    data = MAP.read_bytes()
    assert len(data) == WIDTH * HEIGHT * 6, f"{MAP} is not {WIDTH}x{HEIGHT}"
    import struct
    return [[list(struct.unpack_from("<hhh", data, (y * WIDTH + x) * 6)) for x in range(WIDTH)] for y in range(HEIGHT)]


def place(tiles: list, ids: dict[str, int]) -> list:
    for key, (_, _, (x, y), _, _) in v.VENDORS.items():
        tiles[y][x][1] = ids[f"{key}_1"]
        tiles[y][x + 1][1] = ids[f"{key}_2"]
        tiles[y][x + 1][2] = ids[f"{key}_3"]
        tiles[y - 1][x + 1][2] = ids[f"{key}_4"]
        tiles[y - 1][x][1] = v.INVISIBLE_BLOCKER
    return tiles


def _spots(name: str, key: str) -> list[tuple[int, int]]:
    out = []
    for entry in json.loads((INSTANCE / name).read_text(encoding="utf-8")):
        x, y = map(int, re.findall(r"\d+", entry[key]))
        out.append((x, y))
    return out


def protected() -> set[tuple[int, int]]:
    """Every spot that must stay reachable: doors and the mirror door (reactors), Thulin, the ghosts and the vendors
    (merchants), the pumpkin claim tiles and the display pumpkin. The doors themselves may be walls; their open
    neighbours stand in for them."""
    spots = set(_spots("reactors.json", "source")) | set(_spots("merchants.json", "spawnPoint"))
    spots |= {(5, y) for y in range(13, 21)} | {(11, 15)}
    spots |= {tile for _, _, _, tile, _ in v.VENDORS.values()}
    return spots


def _open_neighbour(tiles, spot):
    x, y = spot
    for cx, cy in ((x, y), (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
        if 0 <= cx < WIDTH and 0 <= cy < HEIGHT and not d.is_wall(tiles[cy][cx]):
            return cx, cy
    return None


def problems(before: list, after: list) -> list[str]:
    out = []
    stall_cells = {cell for _, _, anchor, _, _ in v.VENDORS.values() for cell in v.footprint(anchor)}
    for x, y in sorted(stall_cells):
        tile = before[y][x]
        already = after[y][x]
        if tile[1] or tile[2]:
            if tile[1:] != already[1:]:
                out.append(f"({x}, {y}) already has foreground {tile[1]}/{tile[2]}")
        elif d.is_wall(tile):
            out.append(f"({x}, {y}) is a wall")
    for spot in sorted(protected()):
        if spot in stall_cells:
            out.append(f"{spot} is a protected spot under a stall")
        elif _reaches(before, spot) and not _reaches(after, spot):
            out.append(f"{spot} can no longer reach the town door")
    return out


def _reaches(tiles, spot) -> bool:
    """Whether the spot (or an open tile beside it, for a door in a wall) can walk to the town door. Compared before
    and after placing, so a spot that was already cut off on the hand-edited map is not blamed on the stalls."""
    door, stand_in = _open_neighbour(tiles, TOWN_DOOR), _open_neighbour(tiles, spot)
    if door is None or stand_in is None:
        return False
    return any(door in g and stand_in in g for g in d.components(tiles, {door, stand_in}))


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="print problems, write nothing")
    ap.add_argument("--sotp", help="a sotp.dat that marks the stall strips as walls (default: SOTP_DAT)")
    args = ap.parse_args()
    ids = load_ids()
    if not all(d.blocks(i) for i in ids.values()):
        print("the sotp.dat in use does not mark the stall strips as walls; pass --sotp <server worktree sotp.dat>")
        return 1
    before = read_map()
    after = place([[list(t) for t in row] for row in before], ids)
    found = problems(before, after)
    for m in found:
        print(f"PROBLEM: {m}")
    if found:
        return 1
    print("no problems")
    if not args.check:
        MAP.write_bytes(d.map_bytes(after))
        print(f"wrote {MAP}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

The tests must see a `sotp.dat` that marks the strips as walls. Add this at the top of `test_place_stalls.py`, before `import place_stalls`:

```python
import os
os.environ.setdefault("SOTP_DAT", r"C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server\Chaos\Resources\sotp.dat")
```

- [ ] **Step 3: Run the tests**: `python -m pytest Tools/HauntedTheatre/test_place_stalls.py -q`. Expected: all pass. If `test_the_layout_has_no_problems` reports a reach problem, do not move a stall on your own: report the message, which names the spot and the tile.

- [ ] **Step 4: Write the map:**

```powershell
python Tools/HauntedTheatre/place_stalls.py --sotp C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server\Chaos\Resources\sotp.dat
python Tools/HauntedTheatre/place_stalls.py --check --sotp C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server\Chaos\Resources\sotp.dat
```

Expected: `no problems` / `wrote …lod10269.map`, then `no problems` again.

- [ ] **Step 5: Let `render_map.cs` read another data folder.** In `Tools/MirrorMaze/render_map.cs`, replace the fixed `var data = …` line with:

```csharp
var data = args.Length > 4 ? args[4] : @"C:\Users\Michael\Documents\Unora\Unora Files";
```

Update its usage comment to `<lod path> <width> <height> <out.png> [data folder]`. Then render (from `U`) into the session scratchpad or `%TEMP%`:

```powershell
dotnet run Tools/MirrorMaze/render_map.cs -- Data/Configuration/MapData/lod10269.map 20 31 $env:TEMP\theatre-stalls.png "C:\Users\Michael\Desktop\Theatre Vendors 2026-10-03"
```

The render needs `seo.dat` beside `ia.dat`. Copy the local client folder's `seo.dat` into `%TEMP%\theatre-render\` together with the new `ia.dat`, and pass that folder instead. Do not put `seo.dat` in the release folder. Open the PNG and check that the popcorn stall sits by the back wall, face paint is mid-floor, and fortune is in the bottom-left. Each stall must be drawn whole: no strip may be cut off or shifted by 14 px against its neighbours. Report the PNG path.

- [ ] **Step 6: Write `Tools/HauntedTheatre/README.md`:**

```markdown
# Haunted theatre tools

- `decorate_theatre.py`: built the first haunted theatre map from lod346. Do not rerun it: lod10269.map has been
  edited by hand and by place_stalls.py since, and it would overwrite both.
- Theatre vendors (spec: Chaos.Client docs/superpowers/specs/2026-10-03-theatre-vendors-design.md). `vendors.py` holds
  the stalls, tiles and prices. To change a stall picture:
  1. Put the 112x150 picture in `stalls/sources/` and run `python Tools/HauntedTheatre/stall_strips.py`.
  2. `dotnet run Tools/HauntedTheatre/import_stalls.cs -- --server-sotp <server repo>/Chaos/Resources/sotp.dat`
     writes `ia.dat` and `sotp.server.dat` to a Desktop folder and `stalls/ids.json`. Copy `sotp.server.dat` over the
     server's `Chaos/Resources/sotp.dat`. It refuses a client folder whose first free strip is not 9132.
  3. `python Tools/HauntedTheatre/place_stalls.py --sotp <that sotp.dat>` writes the map.
  4. Preview: `dotnet run Tools/MirrorMaze/render_map.cs -- Data/Configuration/MapData/lod10269.map 20 31 out.png <folder with seo.dat and the new ia.dat>`.
- Tests: `python -m pytest Tools/HauntedTheatre -q`.
```

```json:metadata
{"files": ["Tools/HauntedTheatre/place_stalls.py", "Tools/HauntedTheatre/test_place_stalls.py", "Tools/HauntedTheatre/README.md", "Tools/MirrorMaze/render_map.cs", "Data/Configuration/MapData/lod10269.map"], "verifyCommand": "python -m pytest Tools/HauntedTheatre -q", "acceptanceCriteria": ["only the 12 stall cells change, in the spec strip order", "refuses occupied cells and lost reach", "idempotent", "render shows the three stalls whole"], "modelTier": "standard"}
```

---

### Task 8: An icon-only item kind in the accessory tool (Unora tool)

**Goal:** `Tools/Accessories` can register, number and deploy an item that has only an inventory icon (kind `item`), such as food.

**Files:**
- Modify: `U/Tools/Accessories/acclib/registry.py`, `U/Tools/Accessories/acclib/review.py`
- Test: `U/Tools/Accessories/test_items.py`

**Acceptance Criteria:**
- [ ] `KINDS["item"]` exists with no equipment type and no sprite sheets.
- [ ] `registry.check` accepts a non-draft `item` entry with an icon and no sprite. It still rejects one without an icon.
- [ ] `next_free(reg, "item")` returns `(None, <next free icon>)`.
- [ ] `installed()` is true for an `item` entry once its icon frame is drawn.
- [ ] `review_sheet` for an `item` writes just the enlarged icon.
- [ ] Every existing `test_items.py` test still passes.

**Verify:** `python -m unittest Tools/Accessories/test_items.py` → OK

**Steps:**

- [ ] **Step 1: Write the failing tests.** Add to `RegistryTests` in `test_items.py`:

```python
    def test_an_icon_only_item_needs_an_icon_but_no_sprite(self):
        reg = {"_about": "", "items": [
            {"key": "popcorn", "name": "Popcorn", "kind": "item", "tool": None,
             "template": "crafting/Cooking/Meals/popcorn.json", "sprite": None, "icon": 9001, "status": "no-tool"},
            {"key": "hotdog", "name": "Hot Dog", "kind": "item", "tool": None,
             "template": "crafting/Cooking/Meals/hotdog.json", "sprite": None, "icon": None, "status": "no-tool"}]}
        with mock.patch.object(registry, "template", return_value={"templateKey": "popcorn", "panelSprite": 9001}):
            errors = registry.check(reg).errors
        self.assertFalse(any(e.startswith("popcorn:") for e in errors), errors)
        self.assertTrue(any(e.startswith("hotdog:") and "icon" in e for e in errors), errors)

    def test_next_free_for_an_icon_only_item_has_no_sprite(self):
        sprite, icon = registry.next_free(registry.load(), "item")
        self.assertIsNone(sprite)
        self.assertGreater(icon, registry.FIRST_CUSTOM_ICON)

    def test_an_icon_only_item_is_installed_once_its_icon_is_drawn(self):
        legend = DatArchive()
        drawn = Frame.from_pixels({(0, 0): 5, (1, 0): 5, (0, 1): 5})
        legend.put("item020.epf", Epf.of([drawn] * 3).to_bytes())
        archives = {"Legend.dat": legend}
        # icon 5057 is item020.epf frame 2 (drawn); icon 5060 is frame 5 (past the end)
        self.assertTrue(registry.installed({"key": "a", "kind": "item", "sprite": None, "icon": 5057}, archives))
        self.assertFalse(registry.installed({"key": "b", "kind": "item", "sprite": None, "icon": 5060}, archives))
```

The `hotdog` entry fails with the template mock's `templateKey` too; the test only checks that one of its errors mentions the icon. Run `python -m unittest Tools/Accessories/test_items.py`. Expected: the three new tests fail.

- [ ] **Step 2: Add the kind.** In `acclib/registry.py`, change `Kind.equipment_type` to `str | None` and add to `KINDS`:

```python
    # an icon and nothing worn, such as food: no sprite number, no sheets
    "item": Kind(None, ()),
```

- [ ] **Step 3: Teach `check`, `installed` and `next_free` about it.**

In `check`, replace the "needs a sprite and an icon" line with:

```python
        needs_sprite = bool(KINDS[it["kind"]].sheets)
        if it["status"] != "draft" and ((needs_sprite and it.get("sprite") is None) or it.get("icon") is None):
            r.errors.append(f"{k}: status {it['status']} needs {'a sprite and ' if needs_sprite else ''}an icon "
                            f"(run ids.py assign)")
```

The rule that "status needs a sprite and an icon" must still apply to every other kind.

In `installed`, replace the first `if` with:

```python
    if it.get("icon") is None or (KINDS[it["kind"]].sheets and it.get("sprite") is None):
        return False
```

and after the icon-frame check add:

```python
    if not KINDS[it["kind"]].sheets:
        return True
```

In `check`'s "what the client files say" loop, replace `if it.get("sprite") is None or it["key"] in clashing:` with `if it.get("icon") is None or it["key"] in clashing:`.

In `next_free`, after `icons` is built from the registry and templates and the archive's last icon is added, return early for a kind with no sheets. Move the `return` so it happens after the archive icons are counted:

```python
    if not KINDS[kind].sheets:
        return None, max(icons) + 1
```

Keep the template scan's `KINDS[kind].equipment_type` comparison as it is; with `None` it only adds sprites, and the early return ignores them.

- [ ] **Step 4: Let `review_sheet` handle it.** At the top of `review_sheet` in `acclib/review.py`:

```python
    from .registry import KINDS
    if not KINDS[kind].sheets:
        r = Reviewer(archives, data_dir)
        out.parent.mkdir(parents=True, exist_ok=True)
        r.icon(icon_id).save(out)
        return out
```

If importing `registry` from `review` makes an import cycle, pass `has_sheets: bool = True` as a new last parameter instead, and have `deploy_batch.py` pass `bool(registry.KINDS[it["kind"]].sheets)`.

- [ ] **Step 5: Run the tests.** Run the Verify command. Expected: OK. Then run `python Tools/Accessories/ids.py --no-archives check`. Expected: no new errors compared with Task 0's baseline.

```json:metadata
{"files": ["Tools/Accessories/acclib/registry.py", "Tools/Accessories/acclib/review.py", "Tools/Accessories/test_items.py"], "verifyCommand": "python -m unittest Tools/Accessories/test_items.py", "acceptanceCriteria": ["KINDS['item'] has no equipment type and no sheets", "check accepts an icon-only entry and still needs an icon", "next_free('item') gives no sprite", "installed true once the icon is drawn", "review writes just the icon", "existing tests pass"], "modelTier": "standard"}
```

---

### Task 9: Draw the treat icons (Unora tool)

**Goal:** Three 32 × 32 inventory icons (a striped popcorn box, a pink cotton candy cone and a hot dog in a bun) as item tools. They are registered, numbered and built into a `Legend.dat` batch, and the three treat templates point at them.

**Files:**
- Create: `U/Tools/Accessories/items/Popcorn/{popcorn_art.py,build_popcorn.py,test_popcorn.py,README.md}`
- Create: `U/Tools/Accessories/items/CottonCandy/{cottoncandy_art.py,build_cottoncandy.py,test_cottoncandy.py,README.md}`
- Create: `U/Tools/Accessories/items/HotDog/{hotdog_art.py,build_hotdog.py,test_hotdog.py,README.md}`
- Modify: `U/Tools/Accessories/registry.json`, `U/Tools/Accessories/deployments.json` (by the tools)
- Modify (by `ids.py assign`): the three treat templates' `panelSprite`
- Output (not in git): `Desktop\Accessory batch … (Popcorn, Cotton Candy, Hot Dog)\Legend.dat`

**Acceptance Criteria:**
- [ ] Each icon is inside 32 × 32, uses only `palc000.pal` indices, has a one-pixel dark outline, and reads as its treat in the review picture.
- [ ] `registry.json` has `popcorn`, `cottoncandy` and `hotdog` as kind `item` with consecutive icons after the current last icon. `ids.py check` shows no errors for them.
- [ ] Each treat template's `panelSprite` equals its registry icon.
- [ ] `deploy_batch.py popcorn cottoncandy hotdog` writes a batch folder holding `Legend.dat` and a review picture per icon.

**Verify:** `python -m unittest discover -s Tools/Accessories/items/Popcorn -p "test_*.py"` (and the same for `CottonCandy` and `HotDog`) → OK, then `python Tools/Accessories/ids.py check` → no errors naming the three keys

**Steps:**

- [ ] **Step 1: Read the reference tool.** Read `items/Bubblegum/bubblegum_art.py` (its `icon_frame`), `build_bubblegum.py` and `test_bubblegum.py`. The new tools follow the same layout: `<name>_art.py` holds `ICON_ID = 0` and `icon_frame() -> Frame`; `build_<name>.py` holds `ARCHIVES = ["Legend.dat", "khanpal.dat"]` and `build(archives)`, which calls `put_icon(archives["Legend.dat"], ICON_ID, icon_frame(), archives["khanpal.dat"]["palc000.pal"])`; `main()` is the same as Bubblegum's without the style and preview options. Look up colors with:

```powershell
python -c "import sys;sys.path.insert(0,'Tools/Accessories');from acclib.daformats import DatArchive;from pathlib import Path;p=DatArchive.load(Path(r'C:\Users\Michael\Documents\Unora\Unora Files\khanpal.dat'))['palc000.pal'];[print(i,p[i*3:i*3+3].hex()) for i in range(256)]"
```

Pick indices for red, white, yellow, brown/tan, pink and a near-black outline, and name them as constants at the top of each art file.

- [ ] **Step 2: Write each tool's test first.** `items/Popcorn/test_popcorn.py`:

```python
"""Run from the repo root: python -m unittest discover -s Tools/Accessories/items/Popcorn -p "test_*.py" """
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from popcorn_art import OUTLINE, icon_frame  # noqa: E402


class IconTests(unittest.TestCase):
    def test_the_icon_fits_32_by_32(self):
        px = icon_frame().pixels()
        self.assertTrue(px)
        self.assertTrue(all(0 <= x < 32 and 0 <= y < 32 for x, y in px))

    def test_the_shape_is_outlined(self):
        px = icon_frame().pixels()
        for (x, y), c in px.items():
            if c == OUTLINE:
                continue
            for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                self.assertIn(q, px, f"{(x, y)} touches the background without an outline")

    def test_indices_are_palette_indices(self):
        self.assertTrue(all(0 < c < 256 for c in icon_frame().pixels().values()))


if __name__ == "__main__":
    unittest.main()
```

`Frame.pixels()` returns `{(x, y): index}` for the opaque pixels (check `acclib/daformats.py`; use the real method name). Write the same test for `CottonCandy` (`from cottoncandy_art import …`) and `HotDog` (`from hotdog_art import …`). Run them. Expected: import errors.

- [ ] **Step 3: Draw the icons** in each `<name>_art.py`, as pixel dictionaries built by code (like Bubblegum), then outlined with `OUTLINE`:
  - **Popcorn:** a red-and-white vertically striped paper box (about 16 wide, 14 tall, slightly wider at the top) at the bottom centre, with a heap of popped kernels above it: overlapping 3–4 px white and pale-yellow blobs with a few butter-yellow spots.
  - **Cotton Candy:** a tan paper cone, its point at the bottom centre, holding a large fluffy pink cloud. The cloud is round, about 22 px across, with lighter pink highlights at the top left and a darker pink rim at the bottom right.
  - **Hot Dog:** seen at a slight diagonal. A tan bun, split along its length, holds a red-brown sausage that sticks out at both ends, with a yellow mustard zigzag along the top.

Run the three tests. Expected: OK.

- [ ] **Step 4: Preview before numbering.** Render the three icons 6× enlarged on the review background `(120, 110, 95)` into one PNG in `%TEMP%`, read it, and check each one is recognizable. Report the path so the coordinator can show the user. Do not go on until the coordinator says the user approved the look.

- [ ] **Step 5: Register and number them** (from `U`):

```powershell
python Tools/Accessories/ids.py add popcorn --name "Popcorn" --kind item --tool Popcorn --template crafting/Cooking/Meals/popcorn.json
python Tools/Accessories/ids.py add cottoncandy --name "Cotton Candy" --kind item --tool CottonCandy --template crafting/Cooking/Meals/cottoncandy.json
python Tools/Accessories/ids.py add hotdog --name "Hot Dog" --kind item --tool HotDog --template crafting/Cooking/Meals/hotdog.json
python Tools/Accessories/ids.py assign popcorn
python Tools/Accessories/ids.py assign cottoncandy
python Tools/Accessories/ids.py assign hotdog
python Tools/Accessories/ids.py status popcorn ready
python Tools/Accessories/ids.py status cottoncandy ready
python Tools/Accessories/ids.py status hotdog ready
python Tools/Accessories/ids.py check
```

Expected: three consecutive icons; each template's `panelSprite` and each art file's `ICON_ID` now hold them; `check` shows no errors naming the three keys. If `assign` fails or writes `SPRITE_ID = None` into an art file, fix `write_numbers` for kinds with no sheets (skip the sprite substitution) and add a test for it to `test_items.py`.

- [ ] **Step 6: Build the batch:**

```powershell
python Tools/Accessories/deploy_batch.py popcorn cottoncandy hotdog
```

If it stops because the client folder lacks the last logged batch, do NOT pass `--force`. Report the message to the coordinator; the user decides. Expected otherwise: a Desktop folder `Accessory batch … (Popcorn, Cotton Candy, Hot Dog)` with `Legend.dat`, `templates/`, `review/` and `manifest.txt`. Read the three review pictures.

```json:metadata
{"files": ["Tools/Accessories/items/Popcorn/", "Tools/Accessories/items/CottonCandy/", "Tools/Accessories/items/HotDog/", "Tools/Accessories/registry.json", "Tools/Accessories/deployments.json", "Data/Configuration/Templates/Items/crafting/Cooking/Meals/popcorn.json", "Data/Configuration/Templates/Items/crafting/Cooking/Meals/cottoncandy.json", "Data/Configuration/Templates/Items/crafting/Cooking/Meals/hotdog.json"], "verifyCommand": "python Tools/Accessories/ids.py check", "acceptanceCriteria": ["icons fit 32x32, outlined, palc000 indices, recognizable", "registered as kind item with consecutive icons, no check errors", "template panelSprite equals registry icon", "deploy_batch writes Legend.dat and review pictures"], "modelTier": "frontier"}
```

---

### Task 10: Release folder and full check

**Goal:** One Desktop folder with everything that ships, a `DEPLOY.txt`, and a full test pass on both worktrees.

**Files:**
- Output (not in git): `C:\Users\Michael\Desktop\Theatre Vendors 2026-10-03\` (`ia.dat` from Task 6, `Legend.dat` copied from the Task 9 batch, `DEPLOY.txt`, `preview.png`)

**Acceptance Criteria:**
- [ ] The folder holds `ia.dat`, `Legend.dat`, `DEPLOY.txt` and `preview.png`, and no `sotp.server.dat` (that ships in the server build).
- [ ] The full server test run shows only the 2 known failures; `python -m pytest Tools/HauntedTheatre -q` and `python -m unittest Tools/Accessories/test_items.py` pass.
- [ ] `dotnet run Tools/HauntedTheatre/import_stalls.cs -- --verify <folder> --server-sotp <S sotp>` prints `ok`.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi` (from `S`) → only `GiveAbility` and `OnItemDroppedOn` fail

**Steps:**

- [ ] **Step 1: Assemble the folder.** Copy the batch's `Legend.dat` in, then copy the Task 7 render in as `preview.png`. Run the verify command with `--server-sotp C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server\Chaos\Resources\sotp.dat` first; after it passes, delete `sotp.server.dat` from the folder.

- [ ] **Step 2: Write `DEPLOY.txt`:**

```text
Theatre Vendors (2026-10-03)

Ship in ONE launcher patch together with the server build from feat/theatre-vendors:
  ia.dat      12 stall wall strips (stc09132-09143) + their walk flags. Built from the local client folder,
              so it also carries the Endless Tower wall strips 9082-9131 (no shipped map uses them yet).
  Legend.dat  Popcorn, Cotton Candy and Hot Dog icons (<icon numbers>).

The server build carries Chaos/Resources/sotp.dat with the same walk flags. Upload both together:
a server without the new sotp.dat lets players walk through the stalls.

After uploading:
  copy ia.dat and Legend.dat into C:\Users\Michael\Documents\Unora\Unora Files
  python Tools/Accessories/ids.py status popcorn deployed   (and cottoncandy, hotdog)

In-game check (haunted theatre, Oct 4 - Nov 4):
  - the three stalls draw whole and block walking; the vendors stand beside them and open their greetings
  - buy a recipe for 25 candy, learn it, cook it at a pot, eat it, see the buff and its orange bar line
  - buy one accessory and one overcoat; candy goes down by the listed price, gold does not change
  - Popcorn replaces Acorn Mash
```

Fill in `<icon numbers>` from `registry.json`.

- [ ] **Step 3: Run every test** (server full run from `S`; both Python suites from `U`). Expected: only the 2 known server failures.

```json:metadata
{"files": [], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["release folder has ia.dat, Legend.dat, DEPLOY.txt, preview.png and no sotp.server.dat", "full server run only the 2 known failures; python suites pass", "import_stalls --verify ok"], "modelTier": "mechanical"}
```

---

### Task 11: Commit the full implementation

**Goal:** One commit per worktree on `feat/theatre-vendors`, holding exactly this plan's files.

**Files:** every file the earlier tasks created or changed in `S` and `U`.

**Acceptance Criteria:**
- [ ] `S` has one new commit with the server changes, including `Chaos/Resources/sotp.dat`.
- [ ] `U` has one new commit with the data, tools, strips, `ids.json`, map and registry changes, and no `Custom Client Mods/**/obj` noise.
- [ ] `git status` in both worktrees is clean apart from known build noise.

**Verify:** `git -C <each worktree> log --oneline -1` → the new commit; `git -C <each worktree> status --short` → empty (or only `Custom Client Mods` obj files in `U`)

**Steps:**

- [ ] **Step 1: Review the change sets:** `git -C <worktree> status --short` and `git -C <worktree> diff --stat`. Every path must belong to this plan.

- [ ] **Step 2: Commit the server** (stage by explicit path, never `add -A`):

```powershell
git -C C:\Users\Michael\Documents\GitHub\worktrees\theatre-vendors-server add Chaos/Definitions/UtilityMealBuffs.cs Chaos/Definitions/BigFlags.cs Chaos/Definitions/CookingRequirements.cs Chaos/Scripting/EffectScripts/Items/CookingMeals Chaos/Scripting/ItemScripts/RecipeItemScript.Cooking.cs Chaos/Scripting/DialogScripts/Temuair/Generic/RecipeLibraryScript.Cooking.cs Chaos/Scripting/DialogScripts/Temuair/Generic/CandyShopScript.cs Chaos/Resources/sotp.dat Tests/Chaos.Tests/Effects/HalloweenTreatTests.cs Tests/Chaos.Tests/Items/HalloweenTreatRecipeTests.cs Tests/Chaos.Tests/Merchants/CandyShopScriptTests.cs
```

Commit message (end with the attribution lines from the session):

```text
Theatre vendors: Halloween treats, treat recipes and the candy shop

Popcorn, Cotton Candy and Hot Dog share the utility-meal slot and pay what
Acorn Mash, the Cucumber Cooler and Hearthside Porridge pay. Their recipes
are learned from scrolls. CandyShopScript sells for Halloween candy from a
price list in each vendor's merchant template. sotp.dat marks the 12 stall
strips (9132-9143) as walls.
```

- [ ] **Step 3: Commit Unora.** First run `git -C <U> checkout -- "Custom Client Mods"` if build noise appeared there. Then stage by explicit path: `Data/Configuration/Templates/Items/crafting/Cooking/Meals/{popcorn,cottoncandy,hotdog}.json`, `Data/Configuration/Templates/Items/Recipes/Cooking/recipe_{popcorn,cottoncandy,hotdog}.json`, `Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/stall_*.json`, `Data/Configuration/Templates/Dialogs/Temauir/generic/CandyShop`, `Data/Configuration/Templates/Dialogs/Temauir/Events/Halloween/theatre_vendors`, `Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/merchants.json`, `Data/Configuration/MapData/lod10269.map`, `Tools/HauntedTheatre`, `Tools/MirrorMaze/render_map.cs`, `Tools/Accessories/acclib`, `Tools/Accessories/test_items.py`, `Tools/Accessories/items/Popcorn`, `Tools/Accessories/items/CottonCandy`, `Tools/Accessories/items/HotDog`, `Tools/Accessories/registry.json`, `Tools/Accessories/deployments.json`. Exclude `__pycache__` folders. Commit message:

```text
Theatre vendors: stalls, vendors, treats and icons

Three candy vendors beside fair stalls in the haunted theatre: recipes at
the popcorn stall, Macabre Box accessories at face paint, overcoats at
fortune. Adds the treat and recipe items, the candy shop dialogs, the stall
tools (cut, import, place) with the map change, and an icon-only item kind
in the accessory tool for the three treat icons.
```

- [ ] **Step 4: Report** both commit hashes, the release folder path, and that nothing is merged or pushed.

```json:metadata
{"files": [], "verifyCommand": "git -C C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\theatre-vendors-server log --oneline -1", "acceptanceCriteria": ["one server commit with this plan's files incl. sotp.dat", "one Unora commit with this plan's files, no obj noise", "worktrees clean"], "modelTier": "mechanical"}
```
