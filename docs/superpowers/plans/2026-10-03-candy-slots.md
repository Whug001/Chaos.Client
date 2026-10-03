# Jack's Candy Reels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two candy-only, no-jackpot slot machines ("Jack's Candy Reels") in the haunted theatre, reusing the casino slot code.

**Architecture:** The casino `SlotMachineScript` learns an optional item currency and an optional jackpot from its catalog entry. Gold machines keep their exact behavior. The Open packet carries the currency name, so the client window shows candy instead of gold. New art (cabinet + 7 symbols) is packed into a copy of `hades.dat`, and Unora data places the machines.

**Tech Stack:** C# / .NET 10, TUnit + FluentAssertions + Moq (server), TUnit (client), DALib file-based `dotnet run` scripts, Python 3 + Pillow + pytest (Unora tools).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-03-candy-slots-design.md`

## Global Constraints

- Gold machines (`copperWheel`, `wheelOfFates`, `koboldsFortune`) behave exactly as before. Every existing slot test passes unchanged, except where a constructor or `SendSlotMachineOpen` signature gains new arguments.
- Currency item: `halloweencandy` ("Halloween Candy"). Currency word in text: `candy`. Bet: 1.
- Machine key `jacksCandyReels`, name "Jack's Candy Reels", merchant template key `slotmachinecandy`.
- Placement on `suomi_theatre_halloween`: machines at (16,3) and (18,3), facing Up (the same as the casino machines, which this art matches). Stools (`SlotMachineStool` reactors) at (16,4) and (18,4).
- Symbols, in index order: 0 Candy Corn (Common), 1 Wrapped Candy (Common), 2 Lollipop (Common), 3 Bat (Mid), 4 Ghost (Mid), 5 Skull (Rare), 6 Jack-o'-lantern (Rare). There is no Jackpot-tier symbol.
- Each reel: 32 stops with counts 12/7/3/4/3/2/1 for symbols 0-6. Base payback is exactly 0.9000. Paytable: Jack ×50, Skull ×20, Ghost ×10, Bat ×10, each candy ×4, any three candies ×2, two matching ×1.
- `CLIENT_VERSION` goes up by exactly 1 from whatever the server master holds at branch time (768 on 2026-10-03, so 769). Server and client must agree.
- Several Claude sessions share these checkouts. Work only in the `feat/candy-slots` worktrees. Stage by explicit path. Never use `git add -A`, stash or reset on the shared trees. Never commit `Chaos/appsettings.json` or `launchSettings.json`.
- Never build the server and client solutions at the same time. A running `Chaos.exe` or client locks `bin`; if a build fails on a locked file, ask the user to stop it.
- The art packer never writes into `C:\Users\Michael\Documents\Unora\Unora Files`.
- Two server tests already fail on master (`GiveAbility`, `OnItemDroppedOn` stackable). They are not ours.
- Commit strategy is at-end: implementers do not commit. The last task makes one commit per repo.
- Nothing is pushed.

**User decisions (already made):**
- "No jackpot" for the candy machine.
- Bet "1 candy".
- Payback "About 90%".
- Cabinet: PixelLab candidate 1, the jack-o'-lantern machine (image job `bbe6bb56-5b40-468e-a508-13b995ca1360`).
- Symbols: PixelLab set B, frames 0-6 of object `3bf8ce9b-9f04-4ec6-8ee0-43c46e08fe71`.
- Window: "A. Casino window, candy text".
- "2 machines", spot "B. East corner" at (16,3)/(18,3).
- Name "Jack's Candy Reels".
- Build approach "Option 1: teach the existing slot code to pay in an item".

**Changes from the spec, decided while planning:**
- The catalog entry also carries `currencyName` ("candy"), so the word shown in the window is data rather than code.
- No stack-limit check in the validator: `GiveItemOrSendToBank` already splits a payout larger than one stack. Instead, the script checks at spawn that the currency item can be created, and fails closed if not.
- Machines face Up, not Down, to match the casino cabinet art.
- The jackpot fairness check in `SlotCatalogValidator` skips no-jackpot machines. Otherwise it reports "can never win the pot" for the candy machine.

---

## File map

**Server worktree** `C:\Users\Michael\Documents\GitHub\worktrees\candy-slots-server`
- `Chaos/Models/Data/Slots/SlotTierConfig.cs`: add `CurrencyItemKey`, `CurrencyName`, `HasJackpot`.
- `Chaos/Services/Slots/SlotConfigValidator.cs`: no-jackpot and currency rules.
- `Chaos/Services/Slots/SlotCatalogValidator.cs`: fairness check only over jackpot machines.
- `Chaos/Scripting/MerchantScripts/Casino/SlotMachineScript.cs`: item currency path.
- `Chaos/Scripting/ReactorTileScripts/Temauir/Casino/SlotMachineStoolScript.cs`: send the currency fields.
- `Chaos/Services/Servers/WorldServer.cs`: no jackpot shout for no-jackpot machines.
- `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, `Chaos/Networking/ChaosWorldClient.cs`: `SendSlotMachineOpen` gains 3 parameters.
- `Chaos.Networking/Entities/Server/SlotMachineDisplayArgs.cs`, `Chaos.Networking/Converters/Server/SlotMachineDisplayConverter.cs`: 3 new Open fields.
- `Chaos.DarkAges/Definitions/Enums.cs`: `SlotRejectReason.InsufficientCurrency = 5`.
- `Chaos.DarkAges/Definitions/CONSTANTS.cs`: `CLIENT_VERSION` +1.
- Tests:
  - new `Tests/Chaos.Tests/Slots/NoJackpotSlotConfigTests.cs`;
  - new `Tests/Chaos.Tests/Slots/SlotMachineScriptCandyTests.cs`;
  - edit `Tests/Chaos.Tests/Slots/SlotMachineScriptTests.cs` and `SlotMachineStoolScriptTests.cs` (new constructor and send arguments);
  - edit `Tests/Chaos.Tests/Networking/SlotPacketConverterTests.cs`.

**Client worktree** `C:\Users\Michael\Documents\GitHub\worktrees\candy-slots-client`
- `Chaos.Client/ViewModel/SlotMachine.cs`: currency fields.
- `Chaos.Client/ViewModel/Inventory.cs`: `CountOf(itemName)`.
- `Chaos.Client/Controls/World/Popups/Slots/SlotMachineControl.cs`: candy text, no jackpot UI.
- New test `Tests/Chaos.Client.Tests/SlotCurrencyTests.cs`.
- Gitlink `Chaos-Server`: points at the server commit (done in the final task).

**Unora worktree** `C:\Users\Michael\Documents\GitHub\worktrees\candy-slots-unora`
- New folder `Tools/CandySlots/`:
  - `art/cabinet.png`, `art/symbols/0.png` .. `6.png` (PixelLab sources);
  - `make_candy_slots.py` (idle frames, shared palette, frames JSON, preview);
  - `pack_candy_slots.cs` (writes the hades.dat copy);
  - `rtp.py` (Python mirror of the server's payback calculator);
  - `README.md`;
  - `test_make_candy_slots.py`, `test_candy_content.py`.
- `Data/LocalStorage/SlotMachineCatalog.json`: `jacksCandyReels` entry.
- New `Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/slotMachineCandy.json`.
- `Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/merchants.json` and `reactors.json`.

---

### Task 1: Create the three worktrees

**Goal:** `feat/candy-slots` worktrees for the server, client and Unora, each starting from its local main line.

**Files:**
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\candy-slots-server` (from Chaos-Server `master`)
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\candy-slots-client` (from Chaos.Client `main`)
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\candy-slots-unora` (from Unora `main`)

**Acceptance Criteria:**
- [ ] `git -C <each worktree> branch --show-current` prints `feat/candy-slots`
- [ ] The client worktree's `Chaos-Server` submodule is initialized (`Chaos-Server/Chaos.slnx` exists)
- [ ] The shared checkouts' branches and status are unchanged

**Verify:** `for w in server client unora; do git -C /c/Users/Michael/Documents/GitHub/worktrees/candy-slots-$w branch --show-current; done` → three lines of `feat/candy-slots`

**Steps:**

- [ ] **Step 1: Create the worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server worktree add -b feat/candy-slots ../../worktrees/candy-slots-server master
git -C Chaos.Client worktree add -b feat/candy-slots ../worktrees/candy-slots-client main
git -C Unora worktree add -b feat/candy-slots ../worktrees/candy-slots-unora main
git -C worktrees/candy-slots-client submodule update --init
```

If a branch named `feat/candy-slots` already exists in any repo, stop and report it. Don't reuse or delete it.

- [ ] **Step 2: Confirm**

Run the Verify command. Also run `ls worktrees/candy-slots-client/Chaos-Server/Chaos.slnx`.

---

### Task 2: Server: no-jackpot and item-currency machine settings

**Goal:** A catalog entry can declare an item currency and no jackpot. The validator enforces the rules, and the catalog fairness check ignores no-jackpot machines.

**Files:**
- Modify: `Chaos/Models/Data/Slots/SlotTierConfig.cs`
- Modify: `Chaos/Services/Slots/SlotConfigValidator.cs` (the `jackpotSymbols` block)
- Modify: `Chaos/Services/Slots/SlotCatalogValidator.cs` (`CheckJackpotFairness`)
- Test: `Tests/Chaos.Tests/Slots/NoJackpotSlotConfigTests.cs` (new)

**Acceptance Criteria:**
- [ ] A machine with `HasJackpot = false`, no Jackpot-tier symbol, rates 0 and no jackpot row validates
- [ ] With `HasJackpot = false`: a Jackpot-tier symbol, a nonzero rate, or an `IsJackpot` row each throw
- [ ] A blank `CurrencyItemKey`, or an item currency with a blank `CurrencyName`, throws
- [ ] A catalog of one gold jackpot machine plus one no-jackpot machine reports no problems
- [ ] All existing `SlotCatalogValidatorTests` and `SlotRtpCalculatorTests` still pass

**Verify:** `cd /c/Users/Michael/Documents/GitHub/worktrees/candy-slots-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/Chaos.Tests.Slots/*/*"` → `failed: 0`

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Tests/Chaos.Tests/Slots/NoJackpotSlotConfigTests.cs`:

```csharp
#region
using Chaos.Models.Data.Slots;
using Chaos.Services.Slots;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Slots;

public class NoJackpotSlotConfigTests
{
    private static SlotTierConfig CandyConfig(
        string? currencyItemKey = "halloweencandy",
        string? currencyName = "candy",
        decimal jackpotRate = 0m,
        decimal seedRate = 0m,
        SlotSymbolTier secondTier = SlotSymbolTier.Rare,
        bool jackpotRow = false)
        => new()
        {
            MachineName = "Jack's Candy Reels",
            Bet = 1,
            CurrencyItemKey = currencyItemKey,
            CurrencyName = currencyName,
            HasJackpot = false,
            JackpotContributionRate = jackpotRate,
            SeedReserveRate = seedRate,
            TargetBaseRtp = 0m,
            RtpTolerance = 1000m,
            Symbols =
            [
                new SlotSymbol { SpriteId = 1, Name = "Candy Corn", Tier = SlotSymbolTier.Common },
                new SlotSymbol { SpriteId = 2, Name = "Jack-o'-lantern", Tier = secondTier }
            ],
            Reels =
            [
                new ReelStrip { Stops = [0, 1] },
                new ReelStrip { Stops = [0, 1] },
                new ReelStrip { Stops = [0, 1] }
            ],
            Paytable =
            [
                new SlotPayRule { Kind = SlotPayRuleKind.ThreeOfSymbol, SymbolIndex = 1, Multiplier = 2, IsJackpot = jackpotRow, Label = "Three jacks" },
                new SlotPayRule { Kind = SlotPayRuleKind.TwoMatchingLeft, Multiplier = 1, Label = "Two matching" }
            ]
        };

    private static SlotTierConfig GoldJackpotConfig()
        => new()
        {
            MachineName = "Copper Wheel",
            Bet = 1000,
            JackpotContributionRate = 0.04m,
            SeedReserveRate = 0.01m,
            TargetBaseRtp = 0m,
            RtpTolerance = 1000m,
            Symbols =
            [
                new SlotSymbol { SpriteId = 1260, Name = "Blue Bee", Tier = SlotSymbolTier.Common },
                new SlotSymbol { SpriteId = 1266, Name = "Kobold", Tier = SlotSymbolTier.Jackpot }
            ],
            Reels =
            [
                new ReelStrip { Stops = [0, 1] },
                new ReelStrip { Stops = [0, 1] },
                new ReelStrip { Stops = [0, 1] }
            ],
            Paytable =
            [
                new SlotPayRule { Kind = SlotPayRuleKind.ThreeOfSymbol, SymbolIndex = 1, Multiplier = 0, IsJackpot = true, Label = "Three kobolds" },
                new SlotPayRule { Kind = SlotPayRuleKind.ThreeOfSymbol, SymbolIndex = 0, Multiplier = 1, Label = "Three bees" }
            ]
        };

    [Test]
    public async Task Validate_accepts_a_candy_machine_without_a_jackpot()
    {
        var act = () => SlotConfigValidator.Validate("jacksCandyReels", CandyConfig(), 0);

        act.Should().NotThrow();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Validate_rejects_a_no_jackpot_machine_with_a_jackpot_symbol()
    {
        var act = () => SlotConfigValidator.Validate("jacksCandyReels", CandyConfig(secondTier: SlotSymbolTier.Jackpot), 0);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no jackpot*Jackpot-tier*");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Validate_rejects_a_no_jackpot_machine_that_skims_for_a_pot()
    {
        var act = () => SlotConfigValidator.Validate("jacksCandyReels", CandyConfig(jackpotRate: 0.01m), 0);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no jackpot*");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Validate_rejects_a_no_jackpot_machine_with_a_jackpot_row()
    {
        var act = () => SlotConfigValidator.Validate("jacksCandyReels", CandyConfig(jackpotRow: true), 0);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no jackpot*jackpot row*");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Validate_rejects_a_blank_currency_item_key()
    {
        var act = () => SlotConfigValidator.Validate("jacksCandyReels", CandyConfig(currencyItemKey: " "), 0);

        act.Should().Throw<InvalidOperationException>().WithMessage("*currency item*");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Validate_rejects_an_item_currency_without_a_name()
    {
        var act = () => SlotConfigValidator.Validate("jacksCandyReels", CandyConfig(currencyName: null), 0);

        act.Should().Throw<InvalidOperationException>().WithMessage("*currency name*");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Catalog_fairness_ignores_machines_without_a_jackpot()
    {
        var catalog = new SlotMachineCatalog();
        catalog.Machines["copperWheel"] = GoldJackpotConfig();
        catalog.Machines["jacksCandyReels"] = CandyConfig();

        var report = SlotCatalogValidator.Validate(catalog, 0);

        report.Problems.Should().BeEmpty();

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run the Verify command. Expected: a build error, because `CurrencyItemKey`, `CurrencyName` and `HasJackpot` don't exist yet.

- [ ] **Step 3: Add the settings**

In `Chaos/Models/Data/Slots/SlotTierConfig.cs`, add after `RtpTolerance`:

```csharp
    /// <summary>
    ///     The item this machine takes as its bet and pays out in, by template key. <c>null</c> means gold.
    /// </summary>
    public string? CurrencyItemKey { get; init; }

    /// <summary>
    ///     The word the window uses for <see cref="CurrencyItemKey" /> ("candy"). Required when the currency is an item.
    /// </summary>
    public string? CurrencyName { get; init; }

    /// <summary>
    ///     Whether this machine feeds and can win the shared progressive. A machine without one has no Jackpot-tier
    ///     symbol, skims nothing, and never touches <c>SlotJackpotService</c>.
    /// </summary>
    public bool HasJackpot { get; init; } = true;
```

- [ ] **Step 4: Validator rules**

In `SlotConfigValidator.Validate`, replace:

```csharp
        var jackpotSymbols = config.Symbols.Count(s => s.Tier == SlotSymbolTier.Jackpot);

        if (jackpotSymbols != 1)
            throw new InvalidOperationException($"Slot machine '{machineKey}' must declare exactly one Jackpot-tier symbol, found {jackpotSymbols}");
```

with:

```csharp
        var jackpotSymbols = config.Symbols.Count(s => s.Tier == SlotSymbolTier.Jackpot);

        if (config.HasJackpot)
        {
            if (jackpotSymbols != 1)
                throw new InvalidOperationException($"Slot machine '{machineKey}' must declare exactly one Jackpot-tier symbol, found {jackpotSymbols}");
        } else
        {
            if (jackpotSymbols != 0)
                throw new InvalidOperationException(
                    $"Slot machine '{machineKey}' has no jackpot but declares {jackpotSymbols} Jackpot-tier symbol(s)");

            if ((config.JackpotContributionRate != 0m) || (config.SeedReserveRate != 0m))
                throw new InvalidOperationException(
                    $"Slot machine '{machineKey}' has no jackpot but skims {config.JackpotContributionRate:P2} for a pot and "
                    + $"{config.SeedReserveRate:P2} for its reserve");

            if (config.Paytable.Any(rule => rule.IsJackpot))
                throw new InvalidOperationException($"Slot machine '{machineKey}' has no jackpot but its paytable has a jackpot row");
        }

        if (config.CurrencyItemKey is not null)
        {
            if (string.IsNullOrWhiteSpace(config.CurrencyItemKey))
                throw new InvalidOperationException($"Slot machine '{machineKey}' names a blank currency item");

            if (string.IsNullOrWhiteSpace(config.CurrencyName))
                throw new InvalidOperationException(
                    $"Slot machine '{machineKey}' pays in '{config.CurrencyItemKey}' but has no currency name for the window");
        }
```

- [ ] **Step 5: Fairness check over jackpot machines only**

In `SlotCatalogValidator.CheckJackpotFairness`, replace the start of the method body, up to and including the `foreach` header:

```csharp
        //one machine cannot be a better place to sit than itself, and a machine that failed its own validation
        //has no trustworthy numbers to compare.
        if (reports.Count < 2)
            yield break;

        var values = new List<(string MachineKey, decimal ValuePerGold)>(reports.Count);

        foreach ((var machineKey, var report) in reports)
```

with:

```csharp
        //a machine without a jackpot neither funds nor wins the pot, so it has no place in a comparison of who
        //wins it most cheaply. One machine cannot be a better place to sit than itself, and a machine that failed
        //its own validation has no trustworthy numbers to compare.
        var jackpotReports = reports.Where(pair => catalog.Machines[pair.Key].HasJackpot)
                                    .ToList();

        if (jackpotReports.Count < 2)
            yield break;

        var values = new List<(string MachineKey, decimal ValuePerGold)>(jackpotReports.Count);

        foreach ((var machineKey, var report) in jackpotReports)
```

- [ ] **Step 6: Run the slot tests**

Run the Verify command. Expected: `failed: 0`.

---

### Task 3: Server: the spin pays and takes the item currency

**Goal:** `SlotMachineScript` takes and pays `CurrencyItemKey` items, and never touches the jackpot, the gold ledger or the gold held-winnings for a no-jackpot item machine.

**Files:**
- Modify: `Chaos/Scripting/MerchantScripts/Casino/SlotMachineScript.cs` (constructor, `TryClaim`, `Spin`, new helpers)
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (`SlotRejectReason`)
- Modify: `Tests/Chaos.Tests/Slots/SlotMachineScriptTests.cs:206` and `Tests/Chaos.Tests/Slots/SlotMachineStoolScriptTests.cs:136` (pass an `IItemFactory`)
- Test: `Tests/Chaos.Tests/Slots/SlotMachineScriptCandyTests.cs` (new)

**Acceptance Criteria:**
- [ ] A spin on a candy machine removes 1 Halloween Candy and leaves gold untouched
- [ ] A winning candy spin adds candy to the inventory, or to the bank when the player can't carry it
- [ ] A candy spin with no candy is refused with `InsufficientCurrency`
- [ ] A candy spin leaves `SlotJackpotService.CurrentPot` unchanged and reports `jackpotAmount == 0`
- [ ] A candy machine whose currency item can't be created spawns with `Config == null`
- [ ] All existing `SlotMachineScriptTests` and `SlotMachineStoolScriptTests` pass

**Verify:** `cd /c/Users/Michael/Documents/GitHub/worktrees/candy-slots-server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/Chaos.Tests.Slots/*/*"` → `failed: 0`

**Steps:**

- [ ] **Step 1: Add the reject reason**

In `Chaos.DarkAges/Definitions/Enums.cs`, in `SlotRejectReason`, after `Misconfigured = 4`, add:

```csharp
    Misconfigured = 4,
    InsufficientCurrency = 5
```

(Put a comma after `Misconfigured = 4`.)

- [ ] **Step 2: Write the failing tests**

Create `Tests/Chaos.Tests/Slots/SlotMachineScriptCandyTests.cs`:

```csharp
#region
using Chaos.Collections;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry;
using Chaos.Models.Data.Slots;
using Chaos.Models.Panel;
using Chaos.Models.World;
using Chaos.Scripting.MerchantScripts.Casino;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.Slots;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using Chaos.Tests.Casino;
using FluentAssertions;
using Moq;
#endregion

namespace Chaos.Tests.Slots;

public class SlotMachineScriptCandyTests
{
    private const string MachineKey = "jacksCandyReels";
    private const string ScriptVarsKey = "SlotMachine";
    private const string CandyKey = "halloweencandy";

    // single-stop reels make every spin the same: symbol 0 on every reel
    private static SlotTierConfig CandyConfig(int winMultiplier)
        => new()
        {
            MachineName = "Jack's Candy Reels",
            Bet = 1,
            CurrencyItemKey = CandyKey,
            CurrencyName = "candy",
            HasJackpot = false,
            JackpotContributionRate = 0m,
            SeedReserveRate = 0m,
            TargetBaseRtp = 0m,
            RtpTolerance = 1000m,
            Symbols =
            [
                new SlotSymbol { SpriteId = 1, Name = "Candy Corn", Tier = SlotSymbolTier.Common },
                new SlotSymbol { SpriteId = 2, Name = "Jack-o'-lantern", Tier = SlotSymbolTier.Rare }
            ],
            Reels =
            [
                new ReelStrip { Stops = [0] },
                new ReelStrip { Stops = [0] },
                new ReelStrip { Stops = [0] }
            ],
            Paytable =
            [
                new SlotPayRule { Kind = SlotPayRuleKind.ThreeOfSymbol, SymbolIndex = 0, Multiplier = winMultiplier, Label = "Three candy corn" }
            ]
        };

    private static Item Candy(int count = 1)
        => MockItem.Create(
            "Halloween Candy",
            count,
            stackable: true,
            templateSetup: t => t with { TemplateKey = CandyKey });

    private static Mock<IItemFactory> CandyFactory()
    {
        var factory = new Mock<IItemFactory>();

        factory.Setup(f => f.Create(CandyKey, It.IsAny<ICollection<string>?>()))
               .Returns(() => Candy());

        return factory;
    }

    private static SlotJackpotService JackpotService(int pot)
    {
        var state = new SlotJackpotState { CurrentPot = pot, SeedAmount = 10 };
        var storage = new Mock<IStorage<SlotJackpotState>>();
        storage.SetupGet(s => s.Value).Returns(state);

        return new SlotJackpotService(storage.Object, MockLogger.Create<SlotJackpotService>().Object);
    }

    private static (SlotMachineScript Script, MapInstance Map, SlotJackpotService Jackpot) Build(
        SlotTierConfig config,
        Mock<IItemFactory>? itemFactory = null)
    {
        var map = MockMapInstance.Create();
        var scriptVars = new MockScriptVars();
        scriptVars.Set(MachineKey, "MachineKey");
        var merchant = MockMerchant.Create(map, setup: m => m.Template.ScriptVars[ScriptVarsKey] = scriptVars);

        var catalog = new SlotMachineCatalog();
        catalog.Machines[MachineKey] = config;
        var catalogStorage = new Mock<IStorage<SlotMachineCatalog>>();
        catalogStorage.SetupGet(s => s.Value).Returns(catalog);

        var jackpot = JackpotService(5_000);

        var script = new SlotMachineScript(
            merchant,
            catalogStorage.Object,
            jackpot,
            new Mock<IStore<MailBox>>().Object,
            new Mock<IStore<Aisling>>().Object,
            (itemFactory ?? CandyFactory()).Object,
            CasinoLedgerTestSupport.BuildQuiet(),
            MockLogger.Create<SlotMachineScript>().Object);

        return (script, map, jackpot);
    }

    private static Aisling Player(MapInstance map, int candy, int maxWeight = 50)
    {
        var position = new Point(5, 5);
        var aisling = MockAisling.Create(map, "Candyfan", position);
        map.AddEntity(aisling, position);
        aisling.Gold = 10_000;
        aisling.UserStatSheet.SetMaxWeight(maxWeight);

        if (candy > 0)
            aisling.Inventory.TryAddToNextSlot(Candy(candy));

        return aisling;
    }

    [Test]
    public async Task Spin_takes_one_candy_and_no_gold()
    {
        (var script, var map, _) = Build(CandyConfig(winMultiplier: 0));
        var aisling = Player(map, candy: 5);
        script.TryClaim(aisling);

        var outcome = script.Spin(aisling, out _, out var payout, out _);

        outcome.Should().NotBeNull();
        payout.Should().Be(0);
        aisling.Inventory.CountOfByTemplateKey(CandyKey).Should().Be(4);
        aisling.Gold.Should().Be(10_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Spin_pays_candy_into_the_inventory()
    {
        (var script, var map, _) = Build(CandyConfig(winMultiplier: 1));
        var aisling = Player(map, candy: 5);
        script.TryClaim(aisling);

        script.Spin(aisling, out _, out var payout, out _);

        payout.Should().Be(1);
        aisling.Inventory.CountOfByTemplateKey(CandyKey).Should().Be(5);
        aisling.Gold.Should().Be(10_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Spin_pays_candy_to_the_bank_when_it_cannot_be_carried()
    {
        (var script, var map, _) = Build(CandyConfig(winMultiplier: 1));
        var aisling = Player(map, candy: 1);
        aisling.UserStatSheet.SetMaxWeight(0);
        script.TryClaim(aisling);

        script.Spin(aisling, out _, out var payout, out _);

        payout.Should().Be(1);
        aisling.Inventory.CountOfByTemplateKey(CandyKey).Should().Be(0);
        aisling.Bank.Contains("Halloween Candy").Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Spin_without_candy_is_refused_and_takes_no_gold()
    {
        (var script, var map, _) = Build(CandyConfig(winMultiplier: 0));
        var aisling = Player(map, candy: 0);
        script.TryClaim(aisling);

        var outcome = script.Spin(aisling, out var reason, out _, out _);

        outcome.Should().BeNull();
        reason.Should().Be(SlotRejectReason.InsufficientCurrency);
        aisling.Gold.Should().Be(10_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Spin_on_a_machine_without_a_jackpot_leaves_the_pot_alone()
    {
        (var script, var map, var jackpot) = Build(CandyConfig(winMultiplier: 1));
        var aisling = Player(map, candy: 5);
        script.TryClaim(aisling);

        script.Spin(aisling, out _, out _, out var jackpotAmount);

        jackpot.CurrentPot.Should().Be(5_000);
        jackpotAmount.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_currency_item_that_cannot_be_created_fails_closed()
    {
        var factory = new Mock<IItemFactory>();

        factory.Setup(f => f.Create(CandyKey, It.IsAny<ICollection<string>?>()))
               .Throws(new KeyNotFoundException(CandyKey));

        (var script, _, _) = Build(CandyConfig(winMultiplier: 0), factory);

        script.Config.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_currency_item_name_comes_from_the_item()
    {
        (var script, _, _) = Build(CandyConfig(winMultiplier: 0));

        script.CurrencyItemName.Should().Be("Halloween Candy");

        await Task.CompletedTask;
    }
}
```

If a `using` above names a namespace that doesn't exist, find the right one with Serena `find_symbol` (for example `IItemFactory`, `MailBox`, `Item`). Fix only the `using` lines.

- [ ] **Step 3: Pass an item factory in the existing tests**

In `SlotMachineScriptTests.BuildScript` (line ~206) and in `SlotMachineStoolScriptTests` (line ~136), add `new Mock<IItemFactory>().Object,` between the aisling store argument and `CasinoLedgerTestSupport.BuildQuiet(),`. Add `using Chaos.Services.Factories.Abstractions;` if it isn't already there.

- [ ] **Step 4: Run the tests to see them fail**

Run the Verify command. Expected: a build error (no constructor takes 8 arguments, no `CurrencyItemName`).

- [ ] **Step 5: Constructor and new members**

In `SlotMachineScript`:

1. Add a field after `private readonly IStore<MailBox> MailStore;`:

```csharp
    private readonly IItemFactory ItemFactory;
```

2. Add a property after `Config`:

```csharp
    /// <summary>
    ///     The display name of the item this machine bets and pays (e.g. "Halloween Candy"), or empty for gold. Sent
    ///     to the client so the window can count the player's stack.
    /// </summary>
    public string CurrencyItemName { get; private set; } = string.Empty;
```

3. Change the constructor signature to take `IItemFactory itemFactory` right after `IStore<Aisling> aislingStore`, and add `/// <param name="itemFactory">Creates the currency item for bets refunded and winnings paid in an item.</param>` to its doc comment. Add `ArgumentNullException.ThrowIfNull(itemFactory);` and `ItemFactory = itemFactory;` with the others.

4. In the constructor's `try` block, replace:

```csharp
            SlotConfigValidator.Validate(MachineKey, config, JackpotService.SeedAmount);
            Config = config;
```

with:

```csharp
            SlotConfigValidator.Validate(MachineKey, config, JackpotService.SeedAmount);

            //a currency nobody can create would take the bet and then throw on the payout
            if (config.CurrencyItemKey is not null)
                CurrencyItemName = ItemFactory.Create(config.CurrencyItemKey).Template.Name;

            Config = config;
```

5. Add these helpers after `LogRejected`:

```csharp
    private static bool PaysGold(SlotTierConfig? config) => config?.CurrencyItemKey is null;

    private static bool CanCoverBet(Aisling aisling, SlotTierConfig config)
        => PaysGold(config)
            ? aisling.Gold >= config.Bet
            : aisling.Inventory.HasCountByTemplateKey(config.CurrencyItemKey!, config.Bet);

    private static bool TryTakeBet(Aisling aisling, SlotTierConfig config)
        => PaysGold(config)
            ? aisling.TryTakeGold(config.Bet)
            : aisling.Inventory.RemoveQuantityByTemplateKey(config.CurrencyItemKey!, config.Bet);

    /// <summary>
    ///     Hands over <paramref name="amount" /> of an item currency. Never fails: what the player cannot carry goes to
    ///     their bank, so an item machine needs none of the gold path's held-winnings machinery.
    /// </summary>
    private void GiveCurrencyItem(Aisling aisling, string templateKey, int amount)
    {
        var item = ItemFactory.Create(templateKey);
        item.Count = amount;
        aisling.GiveItemOrSendToBank(item);
    }
```

6. In `TryClaim`, replace `DrainPendingPayout(aisling);` with:

```csharp
        //held winnings are gold from the casino; an item machine lives on another map, outside the casino map's
        //sync that DrainPendingPayout relies on, and must not deliver them
        if (PaysGold(Config))
            DrainPendingPayout(aisling);
```

- [ ] **Step 6: The spin's money points**

In `Spin`, make these replacements.

a. The first `DrainPendingPayout(aisling);` (before the `Config is null` check) becomes:

```csharp
        if (PaysGold(Config))
            DrainPendingPayout(aisling);
```

b. Replace both bet blocks (`if (aisling.Gold < Config.Bet) {...}` and `if (!aisling.TryTakeGold(Config.Bet)) {...}`) with:

```csharp
        var shortReason = PaysGold(Config) ? SlotRejectReason.InsufficientGold : SlotRejectReason.InsufficientCurrency;

        if (!CanCoverBet(aisling, Config))
        {
            reason = shortReason;
            LogRejected(aisling, reason);

            return null;
        }

        //check the actual result of taking the bet rather than trusting the balance check above: if this ever
        //failed and went unchecked, the pot would be contributed to and a payout could be rolled for a bet that
        //was never actually taken
        if (!TryTakeBet(aisling, Config))
        {
            reason = shortReason;
            LogRejected(aisling, reason);

            return null;
        }
```

c. Inside the `try`, change `JackpotService.Contribute(...)` to only run with a jackpot:

```csharp
            if (Config.HasJackpot)
                JackpotService.Contribute(MachineKey, Config.Bet, Config.JackpotContributionRate, Config.SeedReserveRate);
```

d. At the top of the `catch (Exception ex)` block, before `if (aisling.TryGiveGold(Config.Bet))`, add an item branch. The existing `if/else` becomes the `else`:

```csharp
            if (!PaysGold(Config))
            {
                GiveCurrencyItem(aisling, Config.CurrencyItemKey!, Config.Bet);

                Logger.WithTopics(Topics.Entities.Casino, Topics.Entities.Merchant, Topics.Entities.Aisling)
                      .WithProperty(aisling)
                      .LogError(
                          ex,
                          "Slot machine {@MachineKey} failed mid-spin for {@AislingName}; refunded their {@Bet} {@Currency} bet",
                          MachineKey,
                          aisling.Name,
                          Config.Bet,
                          Config.CurrencyItemKey);
            } else if (aisling.TryGiveGold(Config.Bet))
```

The rest of the existing gold refund chain stays the same, followed by `throw;`.

e. Replace `if (outcome.IsJackpot && JackpotService.TryClaim(aisling.Name, out var won))` with:

```csharp
        if (Config.HasJackpot && outcome.IsJackpot && JackpotService.TryClaim(aisling.Name, out var won))
```

f. Replace `jackpotAmount = JackpotService.CurrentPot;` (the one after the payout is computed) with:

```csharp
        jackpotAmount = Config.HasJackpot ? JackpotService.CurrentPot : 0;
```

g. Replace the `if (payout > 0) { ... }` block with:

```csharp
        if (payout > 0)
        {
            if (PaysGold(Config))
            {
                //outcome.IsJackpot is a safe proxy for "this payout is jackpot money": SlotEvaluator forces
                //Multiplier to 0 whenever IsJackpot is true, so the only way payout is nonzero on a jackpot outcome
                //is the TryClaim branch above having actually succeeded.
                JackpotService.RecordReturn(MachineKey, payout, outcome.IsJackpot);

                if (!aisling.TryGiveGold(payout))
                {
                    JackpotService.CreditPendingPayout(aisling.Name, payout);
                    NotifyPendingWinnings(aisling, payout);
                }
            } else
                GiveCurrencyItem(aisling, Config.CurrencyItemKey!, payout);
        }
```

h. Wrap the existing audit `Logger...LogInformation("{@Game}: {@AislingName} spun ...")` call and the `Ledger.Record(aisling, "Slots", Config.Bet, payout);` line so they only run for gold. For items, log one line instead:

```csharp
        if (PaysGold(Config))
        {
            // (the existing LogInformation call, unchanged)

            Ledger.Record(aisling, "Slots", Config.Bet, payout);
        } else
            Logger.WithTopics(Topics.Entities.Casino, Topics.Entities.Aisling, Topics.Entities.Merchant)
                  .WithProperty(aisling)
                  .WithProperty(Subject)
                  .LogInformation(
                      "{@Game}: {@AislingName} spun {@MachineKey} for {@Wager} {@Currency}; stops [{@Stops}], multiplier {@Multiplier}, payout {@Payout} {@Currency}",
                      "Slots",
                      aisling.Name,
                      MachineKey,
                      Config.Bet,
                      Config.CurrencyItemKey,
                      string.Join(' ', outcome.Stops),
                      outcome.Multiplier,
                      payout,
                      Config.CurrencyItemKey);
```

`PersistAisling(aisling, "spin settled");` stays for both.

Add `using Chaos.Services.Factories.Abstractions;` if `IItemFactory` isn't already imported.

- [ ] **Step 7: Run the slot tests**

Run the Verify command. Expected: `failed: 0`.

---

### Task 4: Server: window packet, stool, jackpot shout and version

**Goal:** The Open packet carries the currency name, the item name and whether there's a jackpot. The stool sends them, the server skips the jackpot shout for no-jackpot machines, and `CLIENT_VERSION` goes up by one.

**Files:**
- Modify: `Chaos.Networking/Entities/Server/SlotMachineDisplayArgs.cs`
- Modify: `Chaos.Networking/Converters/Server/SlotMachineDisplayConverter.cs` (Open in `Serialize` and `Deserialize`)
- Modify: `Chaos/Networking/Abstractions/IChaosWorldClient.cs:222`, `Chaos/Networking/ChaosWorldClient.cs` (`SendSlotMachineOpen`)
- Modify: `Chaos/Scripting/ReactorTileScripts/Temauir/Casino/SlotMachineStoolScript.cs` (the `SendSlotMachineOpen` call)
- Modify: `Chaos/Services/Servers/WorldServer.cs` (`OnSlotMachineInteraction`, before `if (outcome.IsJackpot && (payout > 0))`)
- Modify: `Chaos.DarkAges/Definitions/CONSTANTS.cs` (`CLIENT_VERSION`)
- Modify: `Tests/Chaos.Tests/Slots/SlotMachineStoolScriptTests.cs` (4 `SendSlotMachineOpen` verifies)
- Test: `Tests/Chaos.Tests/Networking/SlotPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] Open round-trips `CurrencyName = "candy"`, `CurrencyItemName = "Halloween Candy"`, `HasJackpot = false`
- [ ] The existing gold Open round-trip test still passes (defaults: empty, empty, true)
- [ ] `CLIENT_VERSION` is the branch-time value + 1
- [ ] The full server test run fails only the 2 known master failures

**Verify:** `cd /c/Users/Michael/Documents/GitHub/worktrees/candy-slots-server && dotnet build Chaos.slnx -c Release 2>&1 | tail -3 && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi 2>&1 | tail -15` → `0 Error(s)`, and the failures are only `GiveAbility` and the `OnItemDroppedOn` stackable test

**Steps:**

- [ ] **Step 1: Write the failing test**

Add to `SlotPacketConverterTests`:

```csharp
    [Test]
    public async Task Open_round_trips_an_item_currency_without_a_jackpot()
    {
        var original = new SlotMachineDisplayArgs
        {
            Type = SlotDisplayType.Open,
            MachineName = "Jack's Candy Reels",
            Bet = 1,
            JackpotAmount = 0,
            Symbols = [new SlotSymbolEntry { SpriteId = 1474 }],
            Reels = [[0], [0], [0]],
            Paytable = [new SlotPayRowEntry { Label = "Three candy corn", Multiplier = 4, IsJackpot = false, SymbolIndices = [0, 0, 0] }],
            CurrencyName = "candy",
            CurrencyItemName = "Halloween Candy",
            HasJackpot = false
        };

        var result = RoundTrip(new SlotMachineDisplayConverter(), original);

        result.CurrencyName.Should().Be("candy");
        result.CurrencyItemName.Should().Be("Halloween Candy");
        result.HasJackpot.Should().BeFalse();
        result.Paytable.Should().HaveCount(1);

        await Task.CompletedTask;
    }
```

- [ ] **Step 2: Run it to see it fail**

`dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/Chaos.Tests.Networking/SlotPacketConverterTests/*"` → build error (no `CurrencyName`).

- [ ] **Step 3: Packet fields**

In `SlotMachineDisplayArgs`, after `public List<SlotPayRowEntry>? Paytable { get; set; }`, add:

```csharp
    public string? CurrencyName { get; set; }

    public string? CurrencyItemName { get; set; }

    public bool HasJackpot { get; set; } = true;
```

In `SlotMachineDisplayConverter.Serialize`, case `Open`, after the paytable `foreach` and before `break;`:

```csharp
                writer.WriteString8(args.CurrencyName ?? string.Empty);
                writer.WriteString8(args.CurrencyItemName ?? string.Empty);
                writer.WriteBoolean(args.HasJackpot);
```

In `Deserialize`, case `Open`, after the paytable loop:

```csharp
                var currencyName = reader.ReadString8();
                var currencyItemName = reader.ReadString8();
                var hasJackpot = reader.ReadBoolean();
```

Then add to the returned initializer:

```csharp
                    CurrencyName = currencyName,
                    CurrencyItemName = currencyItemName,
                    HasJackpot = hasJackpot
```

- [ ] **Step 4: Send method**

In `IChaosWorldClient.SendSlotMachineOpen` and `ChaosWorldClient.SendSlotMachineOpen`, add three parameters after `paytable`:

```csharp
        List<SlotPayRowEntry> paytable,
        string currencyName,
        string currencyItemName,
        bool hasJackpot)
```

In `ChaosWorldClient`, add them to the args initializer: `CurrencyName = currencyName, CurrencyItemName = currencyItemName, HasJackpot = hasJackpot`. In the interface's doc comment, add one `<param>` line for each.

- [ ] **Step 5: Stool sends them**

In `SlotMachineStoolScript.OnWalkedOn`, replace the `SendSlotMachineOpen` call with:

```csharp
        aisling.Client.SendSlotMachineOpen(
            machine.Config.MachineName,
            machine.Config.Bet,
            machine.Config.HasJackpot ? jackpotService.CurrentPot : 0,
            symbols,
            reels,
            paytable,
            machine.Config.CurrencyName ?? string.Empty,
            machine.CurrencyItemName,
            machine.Config.HasJackpot);
```

In `SlotMachineStoolScriptTests`, each of the 4 `SendSlotMachineOpen(` verifies gets three more matchers after the paytable matcher: `It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()`.

- [ ] **Step 6: No jackpot shout without a jackpot**

In `WorldServer.OnSlotMachineInteraction`, right after the `localClient.SendSlotSpinResult(...);` call, add:

```csharp
            //a machine without a jackpot has no pot to announce or tick
            if (!machine.Config!.HasJackpot)
                return default;
```

- [ ] **Step 7: Client version**

Read `Chaos.DarkAges/Definitions/CONSTANTS.cs`. Add 1 to `CLIENT_VERSION` (768 → 769 if it still reads 768). Write the new value in your report; Task 5 uses it.

- [ ] **Step 8: Build and run every server test**

Run the Verify command. Expected: 0 build errors. The only failing tests are the 2 known ones.

---

### Task 5: Client: candy in the slot window

**Goal:** The slot window shows the candy count, the bet and messages in candy, and hides the jackpot UI for a no-jackpot machine. Gold machines look the same as before.

**Files:**
- Modify: `Chaos.Client/ViewModel/SlotMachine.cs`
- Modify: `Chaos.Client/ViewModel/Inventory.cs`
- Modify: `Chaos.Client/Controls/World/Popups/Slots/SlotMachineControl.cs` (constructor, `RefreshFromViewModel`, `RefreshGold`, `WriteJackpotLabel`, `RefreshJackpot`, `OnRejected`, `ToggleAutoSpin`, `TickAutoSpin`, `ShowResultMessage`, `Dispose`)
- Test: `Tests/Chaos.Client.Tests/SlotCurrencyTests.cs` (new)

**Acceptance Criteria:**
- [ ] `SlotMachine.ApplyOpen` stores `CurrencyName`, `CurrencyItemName` and `HasJackpot`, and `Clear` resets them (`HasJackpot` back to true)
- [ ] `Inventory.CountOf("Halloween Candy")` sums stack counts across slots and ignores other items
- [ ] Candy machine: top bar reads `Halloween Candy: N`, footer `Bet: 1 candy`, the gold label is hidden, and texts say candy
- [ ] The client Release build has 0 errors against the server worktree

**Verify:** `cd /c/Users/Michael/Documents/GitHub/worktrees/candy-slots-client && dotnet build Chaos.Client.slnx -c Release -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/candy-slots-server 2>&1 | tail -3 && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -c Release -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/candy-slots-server -- --no-ansi --treenode-filter "/*/*/SlotCurrencyTests/*"` → `0 Error(s)` and `failed: 0`

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Tests/Chaos.Client.Tests/SlotCurrencyTests.cs`:

```csharp
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class SlotCurrencyTests
{
    private static SlotMachineDisplayArgs CandyOpen()
        => new()
        {
            Type = SlotDisplayType.Open,
            MachineName = "Jack's Candy Reels",
            Bet = 1,
            CurrencyName = "candy",
            CurrencyItemName = "Halloween Candy",
            HasJackpot = false
        };

    [Test]
    public async Task ApplyOpen_stores_the_currency_fields()
    {
        var slots = new SlotMachine();

        slots.ApplyOpen(CandyOpen());

        slots.CurrencyName.Should().Be("candy");
        slots.CurrencyItemName.Should().Be("Halloween Candy");
        slots.HasJackpot.Should().BeFalse();
        slots.UsesItemCurrency.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_gold_machine_has_no_item_currency()
    {
        var slots = new SlotMachine();

        slots.ApplyOpen(new SlotMachineDisplayArgs { Type = SlotDisplayType.Open, MachineName = "Copper Wheel", Bet = 1000 });

        slots.UsesItemCurrency.Should().BeFalse();
        slots.HasJackpot.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clear_resets_the_currency_fields()
    {
        var slots = new SlotMachine();
        slots.ApplyOpen(CandyOpen());

        slots.Clear();

        slots.CurrencyName.Should().BeEmpty();
        slots.CurrencyItemName.Should().BeEmpty();
        slots.HasJackpot.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task CountOf_sums_matching_stacks_only()
    {
        var inventory = new Inventory();
        inventory.SetSlot(1, 10, DisplayColor.Default, "Halloween Candy", true, 30, 0, 0);
        inventory.SetSlot(2, 11, DisplayColor.Default, "Apple", true, 5, 0, 0);
        inventory.SetSlot(3, 10, DisplayColor.Default, "Halloween Candy", true, 12, 0, 0);
        inventory.SetSlot(4, 12, DisplayColor.Default, "Halloween Candy Bag", false, 1, 0, 0);

        inventory.CountOf("Halloween Candy").Should().Be(42);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run the Verify command. Expected: build errors (`CurrencyName`, `CountOf` missing).

- [ ] **Step 3: View model**

In `SlotMachine`, add after `Paytable`:

```csharp
    /// <summary>The word for the bet item ("candy"), or empty when the machine plays for gold.</summary>
    public string CurrencyName { get; private set; } = string.Empty;

    /// <summary>The bet item's display name ("Halloween Candy"), used to count the player's stack.</summary>
    public string CurrencyItemName { get; private set; } = string.Empty;

    public bool HasJackpot { get; private set; } = true;

    public bool UsesItemCurrency => CurrencyName.Length > 0;
```

In `ApplyOpen`, after `JackpotAmount = args.JackpotAmount;`:

```csharp
        CurrencyName = args.CurrencyName ?? string.Empty;
        CurrencyItemName = args.CurrencyItemName ?? string.Empty;
        HasJackpot = args.HasJackpot;
```

In `Clear`, after `JackpotAmount = 0;`:

```csharp
        CurrencyName = string.Empty;
        CurrencyItemName = string.Empty;
        HasJackpot = true;
```

- [ ] **Step 4: Inventory count**

In `Inventory`, add after `GetSlot`:

```csharp
    /// <summary>
    ///     How many of <paramref name="itemName" /> the player carries, summed across slots. A stackable slot's name
    ///     carries a "[ count ]" suffix (see <see cref="SetSlot" />), so a slot matches on the bare name or the name
    ///     followed by that suffix.
    /// </summary>
    public uint CountOf(string itemName)
    {
        var stackedPrefix = itemName + "[ ";
        uint total = 0;

        foreach (var slot in Slots)
        {
            if (!slot.IsOccupied || slot.Name is null)
                continue;

            if ((slot.Name == itemName) || slot.Name.StartsWith(stackedPrefix, StringComparison.Ordinal))
                total += slot.Stackable ? slot.Count : 1;
        }

        return total;
    }
```

(`Slots` is the class's backing `InventorySlotData[]`; use the field's actual name if it differs.)

- [ ] **Step 5: Window changes**

In `SlotMachineControl`:

a. Constructor, after `WorldState.Inventory.GoldChanged += RefreshGold;`:

```csharp
        //an item-currency machine's count changes through slot updates, not gold updates
        WorldState.Inventory.SlotChanged += OnInventorySlotChanged;
```

Add the handler next to `RefreshGold`:

```csharp
    private void OnInventorySlotChanged(byte slot)
    {
        if (WorldState.SlotMachine.UsesItemCurrency)
            RefreshGold();
    }
```

In `Dispose`, add `WorldState.Inventory.SlotChanged -= OnInventorySlotChanged;` next to the gold unsubscribe.

b. Add two helpers next to `RefreshGold`:

```csharp
    private static string CurrencyWord
        => WorldState.SlotMachine.UsesItemCurrency ? WorldState.SlotMachine.CurrencyName : "gold";

    private static bool CanAffordSpin()
    {
        var vm = WorldState.SlotMachine;
        var bet = (uint)Math.Max(0, vm.Bet);

        return vm.UsesItemCurrency ? WorldState.Inventory.CountOf(vm.CurrencyItemName) >= bet : WorldState.Inventory.Gold >= bet;
    }
```

c. In `RefreshFromViewModel`, replace `BetLabel.Text = $"Bet: {vm.Bet:N0}";` with:

```csharp
        BetLabel.Text = vm.UsesItemCurrency ? $"Bet: {vm.Bet:N0} {vm.CurrencyName}" : $"Bet: {vm.Bet:N0}";
```

d. Replace the body of `RefreshGold` after the `if (AwaitingResult) return;` gate with:

```csharp
        var vm = WorldState.SlotMachine;

        //an item machine has no pot, so its count takes the jackpot readout's place and the gold purse is hidden
        if (vm.UsesItemCurrency)
        {
            var count = WorldState.Inventory.CountOf(vm.CurrencyItemName);
            JackpotLabel.Text = $"{vm.CurrencyItemName}: {count:N0}";
            JackpotLabel.ForegroundColor = CanAffordSpin() ? LegendColors.Gold : LegendColors.Red;
            RenderedJackpot = -1; //the label no longer shows a pot, so the next gold machine must rewrite it
            GoldLabel.Visible = false;

            return;
        }

        JackpotLabel.ForegroundColor = LegendColors.Gold;
        GoldLabel.Visible = true;

        var gold = WorldState.Inventory.Gold;
        GoldLabel.Text = $"Gold: {gold:N0}";

        //red the moment another pull is unaffordable -- the same condition the server answers with an
        //InsufficientGold rejection, said before the player spends a spin finding out.
        GoldLabel.ForegroundColor = CanAffordSpin() ? LegendColors.White : LegendColors.Red;
```

e. At the top of `WriteJackpotLabel`, add:

```csharp
        //the top bar shows the candy count instead (see RefreshGold)
        if (!WorldState.SlotMachine.HasJackpot)
            return;
```

f. In `RefreshJackpot`, add `vm.HasJackpot &&` at the front of the message condition:

```csharp
        if (vm.HasJackpot && !AwaitingResult && (ResultMessageHoldRemaining <= 0f) && !string.IsNullOrEmpty(vm.LastJackpotWinner))
```

g. In `OnRejected`'s switch, add after the `InsufficientGold` arm:

```csharp
            SlotRejectReason.InsufficientCurrency => $"You need {WorldState.SlotMachine.Bet:N0} {CurrencyWord} to play here.",
```

h. In `ToggleAutoSpin`, replace the gold check and message with:

```csharp
        if (!CanAffordSpin())
        {
            SetMessage($"Not enough {CurrencyWord} to spin.", LegendColors.Red, true);

            return;
        }
```

i. In `TickAutoSpin`, replace the gold check block with:

```csharp
        if (!CanAffordSpin())
        {
            StopAutoSpin();
            SetMessage($"Out of {CurrencyWord}. Auto spin stopped.", LegendColors.Red, true);

            return;
        }
```

j. In `ShowResultMessage`, replace the word `gold` in the three result strings with `{CurrencyWord}`. For example `$"JACKPOT! {vm.LastPayout:N0} {CurrencyWord}!"` and `$"{vm.LastLabel} — {vm.LastMultiplier}x — {vm.LastPayout:N0} {CurrencyWord}"`.

- [ ] **Step 6: Version**

Find the client's `CLIENT_VERSION` use. It comes from the server submodule's `CONSTANTS`, and the build points at the server worktree, so no client edit is needed. Confirm with `grep -rn "CLIENT_VERSION" Chaos.Client/GlobalSettings.cs`.

- [ ] **Step 7: Build and test**

Run the Verify command. Expected: `0 Error(s)` and `failed: 0`. Then run the whole client test project the same way (without `--treenode-filter`). Expected: `failed: 0`.

---

### Task 6: Unora: candy slot art and the packed hades.dat

**Goal:** The PixelLab cabinet and symbols become eight monster sprites, sharing one palette, in a copy of `hades.dat`. The copy is built on the "Unora Release 2026-10-03" one, with the sprite numbers recorded in `art/ids.json`.

**Files:**
- Create: `Tools/CandySlots/art/cabinet.png`, `Tools/CandySlots/art/symbols/0.png` .. `6.png`
- Create: `Tools/CandySlots/make_candy_slots.py`
- Create: `Tools/CandySlots/pack_candy_slots.cs`
- Create: `Tools/CandySlots/README.md`
- Test: `Tools/CandySlots/test_make_candy_slots.py`

**Acceptance Criteria:**
- [ ] `art/candy_slots_frames.json` holds 8 sprites: the cabinet with 4 idle frames at 80×100, and 7 symbols with 2 frames at 40×40, sharing one 256-color palette with index 0 transparent and no other pure black
- [ ] The packer refuses to start if the out folder is the live client folder
- [ ] The packer picks the first 8 consecutive free `mnsNNNN.mpf` numbers and one free `mnsNNN.pal` number above the highest used ones, and refuses if any is already taken
- [ ] Output folder `Desktop\Candy Slots <date>` has `hades.dat`, `check.png` (all 8 sprites read back from the new archive), `manifest.txt`, and `ids.json` is copied to `Tools/CandySlots/art/ids.json`
- [ ] `python -m pytest Tools/CandySlots -q` passes

**Verify:** `cd /c/Users/Michael/Documents/GitHub/worktrees/candy-slots-unora && python Tools/CandySlots/make_candy_slots.py && python -m pytest Tools/CandySlots -q && dotnet run Tools/CandySlots/pack_candy_slots.cs` → tests pass, and the packer prints the folder and the 8 sprite ids

**Steps:**

- [ ] **Step 1: Fetch the art**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/candy-slots-unora
mkdir -p Tools/CandySlots/art/symbols
curl -sf -o Tools/CandySlots/art/cabinet.png "https://api.pixellab.ai/mcp/images/bbe6bb56-5b40-468e-a508-13b995ca1360/download"
for i in 0 1 2 3 4 5 6; do curl -sf -o Tools/CandySlots/art/symbols/$i.png "https://backblaze.pixellab.ai/file/pixellab-characters/objects/17e46b86-928c-481f-aa0e-9ef94d1a1257/3bf8ce9b-9f04-4ec6-8ee0-43c46e08fe71/rotations/frame_$i.png"; done
python -c "from PIL import Image;import glob;[print(f,Image.open(f).size) for f in ['Tools/CandySlots/art/cabinet.png']+sorted(glob.glob('Tools/CandySlots/art/symbols/*.png'))]"
```

Expected: the cabinet is (80, 100), and each symbol is (40, 40). If a download fails, copies are in the brainstorm session folder `Unora/.superpowers/brainstorm/284769-1791054867/content/` (`pl1.png` is the cabinet at 3×; scale it down 3× with NEAREST) and `sym_f0.png` .. `sym_f6.png`.

- [ ] **Step 2: Write the tests**

Create `Tools/CandySlots/test_make_candy_slots.py`:

```python
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import make_candy_slots as mcs  # noqa: E402


def sprites():
    return mcs.build()


def test_there_is_a_cabinet_and_seven_symbols():
    built = sprites()
    assert [s.name for s in built] == ["cabinet"] + mcs.SYMBOL_NAMES
    assert len(mcs.SYMBOL_NAMES) == 7


def test_the_cabinet_has_four_idle_frames_at_full_size():
    cabinet = sprites()[0]
    assert (cabinet.width, cabinet.height) == (80, 100)
    assert len(cabinet.frames) == 4
    assert cabinet.frames[0] != cabinet.frames[2]


def test_each_symbol_has_two_identical_frames():
    for symbol in sprites()[1:]:
        assert (symbol.width, symbol.height) == (40, 40)
        assert len(symbol.frames) == 2
        assert symbol.frames[0] == symbol.frames[1]


def test_one_shared_palette_passes_the_check():
    built = sprites()
    palette = mcs.palette_for(built)
    assert mcs.check(built, palette) == []


def test_every_frame_has_visible_pixels_and_a_transparent_border():
    for sprite in sprites():
        for frame in sprite.frames:
            assert any(frame)
            assert frame[0] == 0
```

- [ ] **Step 3: Write make_candy_slots.py**

Create `Tools/CandySlots/make_candy_slots.py`:

```python
"""Builds the Jack's Candy Reels sprites from the PixelLab art.

The cabinet gets four idle frames: the carved face and the reel window glow brighter and dimmer, the way the casino
cabinet's marquee pulses (Tools/generate_slot_idle.py). The seven reel symbols get two identical frames each, the
layout the carving pumpkin uses (back and front), so they face the player whichever way the reel asks.

All eight share one palette. Index 0 is transparent, and no other entry is pure black, which the client would also
draw as transparent.

Run:  python Tools/CandySlots/make_candy_slots.py        (writes art/candy_slots_frames.json and art/preview.png)
      python Tools/CandySlots/make_candy_slots.py --check (checks only)
"""
from __future__ import annotations

import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ART = HERE / "art"

SYMBOL_NAMES = ["candycorn", "wrappedcandy", "lollipop", "bat", "ghost", "skull", "jackolantern"]
CABINET_SIZE = (80, 100)
SYMBOL_SIZE = (40, 40)
ALPHA_CUT = 128
GLOW_LUM = 150          # a pixel brighter than this is part of the lit face or reel glass
GLOW_STEPS = [1.0, 1.12, 1.22, 1.12]
MAX_COLOURS = 255       # index 0 is reserved for transparency


@dataclass
class Sprite:
    name: str
    width: int
    height: int
    center: tuple[int, int]
    rgba_frames: list[Image.Image]
    frames: list[list[int]] = field(default_factory=list)


def _lum(r: int, g: int, b: int) -> float:
    return 0.299 * r + 0.587 * g + 0.114 * b


def _solid(image: Image.Image) -> Image.Image:
    """Hard alpha: the client has no partial transparency."""
    image = image.convert("RGBA")
    px = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 255) if a >= ALPHA_CUT else (0, 0, 0, 0)
    return image


def _glow(base: Image.Image, factor: float) -> Image.Image:
    frame = base.copy()
    px = frame.load()
    for y in range(frame.height):
        for x in range(frame.width):
            r, g, b, a = px[x, y]
            if a and _lum(r, g, b) > GLOW_LUM:
                px[x, y] = (min(255, int(r * factor)), min(255, int(g * factor)), min(255, int(b * factor)), a)
    return frame


def _anchor(image: Image.Image) -> tuple[int, int]:
    """Bottom middle of the visible pixels, a little up from the base, as the casino cabinet's anchor sits."""
    left, top, right, bottom = image.getbbox()
    return (left + right) // 2, bottom - 13


def _load() -> list[Sprite]:
    cabinet = _solid(Image.open(ART / "cabinet.png"))
    if cabinet.size != CABINET_SIZE:
        raise SystemExit(f"cabinet.png is {cabinet.size}, expected {CABINET_SIZE}")
    sprites = [Sprite("cabinet", *CABINET_SIZE, _anchor(cabinet), [_glow(cabinet, f) for f in GLOW_STEPS])]

    for index, name in enumerate(SYMBOL_NAMES):
        symbol = _solid(Image.open(ART / "symbols" / f"{index}.png"))
        if symbol.size != SYMBOL_SIZE:
            raise SystemExit(f"symbols/{index}.png is {symbol.size}, expected {SYMBOL_SIZE}")
        left, top, right, bottom = symbol.getbbox()
        sprites.append(Sprite(name, *SYMBOL_SIZE, ((left + right) // 2, bottom), [symbol, symbol]))

    return sprites


def palette_for(sprites: list[Sprite]) -> list[tuple[int, int, int]]:
    """The shared 256-entry palette, already applied to every sprite's frames by build()."""
    return _PALETTE_CACHE[id(sprites)]


_PALETTE_CACHE: dict[int, list[tuple[int, int, int]]] = {}


def build() -> list[Sprite]:
    sprites = _load()

    # quantize every visible pixel of every frame together, so all eight sprites share one palette
    sheet_width = sum(s.width * len(s.rgba_frames) for s in sprites)
    sheet = Image.new("RGB", (sheet_width, max(s.height for s in sprites)), (0, 0, 0))
    x = 0
    for sprite in sprites:
        for frame in sprite.rgba_frames:
            sheet.paste(frame.convert("RGB"), (x, 0), frame)
            x += sprite.width
    quantized = sheet.quantize(colors=MAX_COLOURS, method=Image.Quantize.MEDIANCUT)
    raw = quantized.getpalette()[: MAX_COLOURS * 3]
    colours = [tuple(raw[i:i + 3]) for i in range(0, len(raw), 3)]
    # pure black would read as transparent; nudge it to the darkest visible grey
    colours = [(1, 1, 1) if c == (0, 0, 0) else c for c in colours]
    palette = [(0, 0, 0)] + colours + [(1, 1, 1)] * (MAX_COLOURS - len(colours))

    for sprite in sprites:
        sprite.frames = []
        for frame in sprite.rgba_frames:
            q = frame.convert("RGB").quantize(palette=quantized, dither=Image.Dither.NONE)
            alpha = frame.getchannel("A").load()
            qpx = q.load()
            pixels = []
            for y in range(sprite.height):
                for xx in range(sprite.width):
                    pixels.append(qpx[xx, y] + 1 if alpha[xx, y] else 0)
            sprite.frames.append(pixels)
    _PALETTE_CACHE[id(sprites)] = palette
    return sprites


def check(sprites: list[Sprite], palette: list[tuple[int, int, int]]) -> list[str]:
    problems = []
    if len(palette) != 256:
        problems.append(f"palette has {len(palette)} entries; it must have 256")
    if any(c == (0, 0, 0) for c in palette[1:]):
        problems.append("visible pure black reads as transparent in the client")
    for sprite in sprites:
        for i, frame in enumerate(sprite.frames):
            if len(frame) != sprite.width * sprite.height:
                problems.append(f"{sprite.name} frame {i} has {len(frame)} pixels")
            if max(frame) > 255:
                problems.append(f"{sprite.name} frame {i} uses an index past 255")
    return problems


def frames_json(sprites: list[Sprite]) -> dict:
    return {
        "palette": [list(c) for c in palette_for(sprites)],
        "sprites": [
            {"name": s.name, "width": s.width, "height": s.height, "centerX": s.center[0], "centerY": s.center[1],
             "idle": s.name == "cabinet", "frames": s.frames}
            for s in sprites
        ],
    }


def preview(sprites: list[Sprite]) -> Image.Image:
    palette = palette_for(sprites)
    width = sum(s.width for s in sprites) * 3 + 10 * len(sprites)
    out = Image.new("RGBA", (width, 100 * 3), (40, 40, 58, 255))
    x = 0
    for sprite in sprites:
        image = Image.new("RGBA", (sprite.width, sprite.height))
        image.putdata([(0, 0, 0, 0) if i == 0 else (*palette[i], 255) for i in sprite.frames[0]])
        big = image.resize((sprite.width * 3, sprite.height * 3), Image.NEAREST)
        out.alpha_composite(big, (x, out.height - big.height))
        x += big.width + 10
    return out


def main() -> int:
    sprites = build()
    problems = check(sprites, palette_for(sprites))
    for problem in problems:
        print(problem)
    if problems:
        return 1
    if "--check" in sys.argv:
        return 0
    (ART / "candy_slots_frames.json").write_text(json.dumps(frames_json(sprites)), encoding="utf-8")
    preview(sprites).save(ART / "preview.png")
    print(f"wrote {ART / 'candy_slots_frames.json'} and preview.png")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

Note: `palette_for` reads a cache keyed by the list built in the same call to `build()`. The tests call `palette_for(built)` on the same list, so it works. Don't call `palette_for` on a list `build()` didn't return.

- [ ] **Step 4: Run make and the tests**

```bash
python Tools/CandySlots/make_candy_slots.py && python -m pytest Tools/CandySlots/test_make_candy_slots.py -q
```

Expected: `wrote ...` and `5 passed`. Open `art/preview.png`. You should see the cabinet and seven symbols with correct colors.

- [ ] **Step 5: Write pack_candy_slots.cs**

Create `Tools/CandySlots/pack_candy_slots.cs`:

```csharp
#:project C:/Users/Michael/Documents/GitHub/dalib/DALib/DALib.csproj
// Packs the Jack's Candy Reels sprites into a copy of hades.dat. Run from the Unora repo root after make_candy_slots.py:
//   dotnet run Tools/CandySlots/pack_candy_slots.cs -- [source hades.dat] [out folder]
// The source defaults to the Desktop "Unora Release 2026-10-03" copy, which already holds the carving pumpkin. Picks the
// first 8 free consecutive sprite numbers and one free palette number above the highest used, patches a temp copy,
// re-reads it the way the client does, and only then puts hades.dat, check.png, manifest.txt and ids.json in the out
// folder (default: Desktop\Candy Slots <date>). It also copies ids.json to Tools/CandySlots/art/ids.json. On any
// failure it removes its outputs, so the folder never holds a half-patched hades.dat.
using System.Text.Json;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using SkiaSharp;

var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
var liveClient = Path.GetFullPath(@"C:\Users\Michael\Documents\Unora\Unora Files");
var sourcePath = args.Length > 0 ? args[0] : Path.Combine(desktop, "Unora Release 2026-10-03", "hades.dat");
var outDir = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(desktop, $"Candy Slots {DateTime.Now:yyyy-MM-dd}"));
var framesPath = Path.Combine("Tools", "CandySlots", "art", "candy_slots_frames.json");
var idsCopyPath = Path.Combine("Tools", "CandySlots", "art", "ids.json");

if (string.Equals(outDir.TrimEnd('\\'), liveClient, StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Refusing to write into the live client folder.");

    return 1;
}

var copyPath = Path.Combine(outDir, "hades.dat");
var workPath = Path.Combine(outDir, "hades-work.dat");
var newPath = Path.Combine(outDir, "hades-new.dat");
var checkPath = Path.Combine(outDir, "check.png");
var manifestPath = Path.Combine(outDir, "manifest.txt");
var idsPath = Path.Combine(outDir, "ids.json");

void Clean()
{
    foreach (var path in new[] { workPath, newPath, copyPath, checkPath, manifestPath, idsPath })
        File.Delete(path);
}

int Fail(string reason)
{
    Clean();
    Console.Error.WriteLine(reason);

    return 1;
}

using var doc = JsonDocument.Parse(File.ReadAllText(framesPath));
var root = doc.RootElement;
var sprites = root.GetProperty("sprites").EnumerateArray().ToList();
var ids = new Dictionary<string, int>();

try
{
    Directory.CreateDirectory(outDir);
    Clean();
    File.Copy(sourcePath, workPath, overwrite: true);

    int paletteId;

    using (var archive = DataArchive.FromFile(workPath, memoryMapped: false))
    {
        static IEnumerable<int> Numbers(DataArchive a, string ext)
            => a.Select(e => e.EntryName)
                .Where(n => n.StartsWith("mns", StringComparison.OrdinalIgnoreCase) && n.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                .Select(n => int.TryParse(n[3..^ext.Length], out var v) ? v : -1)
                .Where(v => v >= 0);

        var firstSprite = Numbers(archive, ".mpf").Max() + 1;
        paletteId = Numbers(archive, ".pal").Max() + 1;
        var palName = $"mns{paletteId:D3}.pal";

        if (archive.TryGetValue(palName, out _))
            return Fail($"{palName} is already in the archive");

        var palette = new Palette(
            root.GetProperty("palette").EnumerateArray()
                .Select(c => new SKColor((byte)c[0].GetInt32(), (byte)c[1].GetInt32(), (byte)c[2].GetInt32())));

        archive.Patch(palName, palette);

        for (var i = 0; i < sprites.Count; i++)
        {
            var sprite = sprites[i];
            var id = firstSprite + i;
            var mpfName = $"mns{id:D3}.mpf";

            if (archive.TryGetValue(mpfName, out _))
                return Fail($"{mpfName} is already in the archive");

            var width = (short)sprite.GetProperty("width").GetInt32();
            var height = (short)sprite.GetProperty("height").GetInt32();
            var frames = sprite.GetProperty("frames").EnumerateArray().ToList();
            var idle = sprite.GetProperty("idle").GetBoolean();

            //the cabinet idles like the casino cabinet (sprite 1453: standing frames looping every 200ms); a symbol is
            //the carving pumpkin's static layout, frame 0 the back and frame 1 the front
            var mpf = new MpfFile(MpfHeaderType.None, MpfFormatType.SingleAttack, width, height)
            {
                PaletteNumber = paletteId,
                WalkFrameIndex = 0,
                WalkFrameCount = (byte)(idle ? 0 : 1),
                AttackFrameIndex = 0,
                AttackFrameCount = (byte)(idle ? 0 : 1),
                StandingFrameIndex = 0,
                StandingFrameCount = (byte)(idle ? frames.Count : 0),
                OptionalAnimationFrameCount = (byte)(idle ? frames.Count : 0),
                OptionalAnimationProbability = 0,
                AnimationIntervalMs = idle ? 200 : 10000
            };

            foreach (var frame in frames)
                mpf.Add(
                    new MpfFrame
                    {
                        Left = 0,
                        Top = 0,
                        Right = width,
                        Bottom = height,
                        CenterX = (short)sprite.GetProperty("centerX").GetInt32(),
                        CenterY = (short)sprite.GetProperty("centerY").GetInt32(),
                        Data = frame.EnumerateArray().Select(v => (byte)v.GetInt32()).ToArray()
                    });

            archive.Patch(mpfName, mpf);
            ids[sprite.GetProperty("name").GetString()!] = id;
        }

        archive.Save(newPath);
    }

    using (var check = DataArchive.FromFile(newPath))
    {
        var palettes = Palette.FromArchive("mns", check);
        var images = new List<SKImage>();

        foreach ((var name, var id) in ids)
        {
            var mpf = MpfFile.FromArchive($"mns{id:D3}.mpf", check);

            if ((mpf.PaletteNumber != paletteId) || !palettes.ContainsKey(paletteId))
                return Fail($"{name} (mns{id:D3}.mpf) did not read back with palette {paletteId}");

            images.Add(Graphics.RenderImage(mpf[0], palettes[paletteId]));
        }

        var sheetWidth = images.Sum(i => i.Width * 3 + 10);
        var sheetHeight = images.Max(i => i.Height) * 3;
        using var surface = SKSurface.Create(new SKImageInfo(sheetWidth, sheetHeight));
        surface.Canvas.Clear(new SKColor(40, 40, 58));
        var x = 0f;

        using var paint = new SKPaint();

        foreach (var image in images)
        {
            surface.Canvas.DrawImage(image, new SKRect(x, sheetHeight - (image.Height * 3), x + (image.Width * 3), sheetHeight), new SKSamplingOptions(SKFilterMode.Nearest), paint);
            x += (image.Width * 3) + 10;
            image.Dispose();
        }

        using var snapshot = surface.Snapshot();
        using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(checkPath, png.ToArray());
    }

    File.Move(newPath, copyPath, overwrite: true);
    File.Delete(workPath);

    var idsJson = JsonSerializer.Serialize(new { paletteId, sprites = ids }, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(idsPath, idsJson);
    File.WriteAllText(idsCopyPath, idsJson);

    File.WriteAllText(
        manifestPath,
        $"""
         hades.dat with the Jack's Candy Reels sprites ({string.Join(", ", ids.Select(p => $"{p.Key} = mns{p.Value:D3}.mpf"))}) and mns{paletteId:D3}.pal.
         Built from {sourcePath}. That copy already holds the carving pumpkin (mns1455.mpf).
         Ship it in the same launcher patch as the client with the candy slot window (the new CLIENT_VERSION), then restart the server.
         For a local test, back up the client folder's hades.dat first, then copy this one over it.
         """);
}
catch (Exception ex)
{
    return Fail($"pack failed: {ex.Message}");
}

Console.WriteLine($"wrote {copyPath}");
Console.WriteLine(JsonSerializer.Serialize(ids));

return 0;
```

- [ ] **Step 6: Pack**

```bash
dotnet run Tools/CandySlots/pack_candy_slots.cs
```

Expected: `wrote ...\Candy Slots 2026-10-03\hades.dat` and an id map such as `{"cabinet":1474,...,"jackolantern":1481}`. Open `check.png` to confirm all 8 sprites look right. If the build fails on a DALib API name (for example `SKSamplingOptions` or `Palette.FromArchive`), compare with `Tools/PumpkinCarving/pack_pumpkin.cs` and `Tools/MirrorMaze/render_map.cs`, which compile against the same DALib.

- [ ] **Step 7: README**

Create `Tools/CandySlots/README.md`:

```markdown
# Jack's Candy Reels art

1. `python Tools/CandySlots/make_candy_slots.py` builds `art/candy_slots_frames.json` and `art/preview.png` from
   `art/cabinet.png` (PixelLab image bbe6bb56-5b40-468e-a508-13b995ca1360) and `art/symbols/0-6.png` (PixelLab object
   3bf8ce9b-9f04-4ec6-8ee0-43c46e08fe71, frames 0-6). `--check` only checks.
2. `dotnet run Tools/CandySlots/pack_candy_slots.cs -- [source hades.dat] [out folder]` writes `hades.dat`, `check.png`,
   `manifest.txt` and `ids.json` to a Desktop folder and copies `ids.json` to `art/`. The source must be the newest
   unshipped hades.dat (on 2026-10-03, the one in Desktop\Unora Release 2026-10-03, which holds the carving pumpkin).
3. Put the ids from `art/ids.json` into `Data/LocalStorage/SlotMachineCatalog.json` (`jacksCandyReels` symbol
   spriteIds, in SYMBOL_NAMES order) and `Templates/Merchants/Temauir/Events/Halloween/slotMachineCandy.json`
   (`sprite` = the cabinet).

Never pass the live client folder as the out folder; the packer refuses it.
`python Tools/CandySlots/rtp.py` prints the catalog entry's payback. Tests: `python -m pytest Tools/CandySlots -q`.
```

---

### Task 7: Unora: catalog entry, merchant template and theatre placement

**Goal:** `jacksCandyReels` is in the slot catalog with the real sprite ids and 0.9000 payback. Two `slotmachinecandy` merchants and their stools stand in the haunted theatre.

**Files:**
- Modify: `Data/LocalStorage/SlotMachineCatalog.json` (add `default.machines.jacksCandyReels`)
- Create: `Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/slotMachineCandy.json`
- Modify: `Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/merchants.json`
- Modify: `Data/Configuration/MapInstances/Temuair/Events/Halloween/Suomi_Theatre_Halloween/reactors.json`
- Create: `Tools/CandySlots/rtp.py`
- Test: `Tools/CandySlots/test_candy_content.py`

**Acceptance Criteria:**
- [ ] `rtp.py` reports a base payback between 0.89 and 0.91 for `jacksCandyReels` (expected 0.9000)
- [ ] The catalog entry's symbol sprite ids and the template's sprite equal `art/ids.json`
- [ ] The theatre has `slotmachinecandy` at (16,3) and (18,3), and `SlotMachineStool` at (16,4) and (18,4)
- [ ] The three gold machines' entries are unchanged
- [ ] `python -m pytest Tools/CandySlots -q` passes

**Verify:** `cd /c/Users/Michael/Documents/GitHub/worktrees/candy-slots-unora && python Tools/CandySlots/rtp.py && python -m pytest Tools/CandySlots -q` → `jacksCandyReels base payback 0.9000`, all tests pass

**Steps:**

- [ ] **Step 1: Write the content tests**

Create `Tools/CandySlots/test_candy_content.py`:

```python
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import rtp  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
CATALOG = REPO / "Data" / "LocalStorage" / "SlotMachineCatalog.json"
THEATRE = REPO / "Data" / "Configuration" / "MapInstances" / "Temuair" / "Events" / "Halloween" / "Suomi_Theatre_Halloween"
TEMPLATE = REPO / "Data" / "Configuration" / "Templates" / "Merchants" / "Temauir" / "Events" / "Halloween" / "slotMachineCandy.json"
IDS = Path(__file__).resolve().parent / "art" / "ids.json"
NAMES = ["candycorn", "wrappedcandy", "lollipop", "bat", "ghost", "skull", "jackolantern"]


def machine():
    return json.loads(CATALOG.read_text(encoding="utf-8"))["default"]["machines"]["jacksCandyReels"]


def test_the_candy_machine_pays_back_about_ninety_percent():
    assert 0.89 <= rtp.base_rtp(machine()) <= 0.91


def test_the_candy_machine_has_no_jackpot_and_bets_one_candy():
    m = machine()
    assert m["hasJackpot"] is False
    assert m["currencyItemKey"] == "halloweencandy"
    assert m["currencyName"] == "candy"
    assert m["bet"] == 1
    assert m["jackpotContributionRate"] == 0 and m["seedReserveRate"] == 0
    assert all(s["tier"] != "Jackpot" for s in m["symbols"])
    assert all(not row["isJackpot"] for row in m["paytable"])


def test_sprites_match_the_packed_ids():
    ids = json.loads(IDS.read_text(encoding="utf-8"))["sprites"]
    assert [s["spriteId"] for s in machine()["symbols"]] == [ids[n] for n in NAMES]
    assert json.loads(TEMPLATE.read_text(encoding="utf-8"))["sprite"] == ids["cabinet"]


def test_the_machines_and_stools_stand_in_the_east_corner():
    merchants = json.loads((THEATRE / "merchants.json").read_text(encoding="utf-8"))
    reactors = json.loads((THEATRE / "reactors.json").read_text(encoding="utf-8"))
    machines = sorted(m["spawnPoint"] for m in merchants if m["merchantTemplateKey"] == "slotmachinecandy")
    stools = sorted(r["source"] for r in reactors if "SlotMachineStool" in r["scriptKeys"])
    assert machines == ["(16, 3)", "(18, 3)"]
    assert stools == ["(16, 4)", "(18, 4)"]


def test_the_gold_machines_are_untouched():
    machines = json.loads(CATALOG.read_text(encoding="utf-8"))["default"]["machines"]
    for key in ("copperWheel", "wheelOfFates", "koboldsFortune"):
        assert "currencyItemKey" not in machines[key]
        assert "hasJackpot" not in machines[key]
```

- [ ] **Step 2: Write rtp.py**

Create `Tools/CandySlots/rtp.py`:

```python
"""Python mirror of Chaos-Server's SlotRtpCalculator + SlotEvaluator: every stop combination, first matching rule wins,
a jackpot row pays 0. Run: python Tools/CandySlots/rtp.py"""
import json
from itertools import product
from pathlib import Path

CATALOG = Path(__file__).resolve().parents[2] / "Data" / "LocalStorage" / "SlotMachineCatalog.json"


def _multiplier(machine: dict, center: list[int]) -> int:
    tiers = [s["tier"] for s in machine["symbols"]]
    for rule in machine["paytable"]:
        kind = rule["kind"]
        if kind == "ThreeOfSymbol" and all(c == rule["symbolIndex"] for c in center):
            hit = True
        elif kind == "ThreeOfTier" and all(tiers[c] == rule["tier"] for c in center):
            hit = True
        elif kind == "TwoMatchingLeft" and center[0] == center[1]:
            hit = True
        else:
            hit = False
        if hit:
            return 0 if rule.get("isJackpot") else rule["multiplier"]
    return 0


def base_rtp(machine: dict) -> float:
    strips = [reel["stops"] for reel in machine["reels"]]
    total = 0
    count = 0
    for center in product(*strips):
        total += _multiplier(machine, list(center))
        count += 1
    return total / count


def main() -> int:
    machines = json.loads(CATALOG.read_text(encoding="utf-8"))["default"]["machines"]
    for key, machine in machines.items():
        print(f"{key} base payback {base_rtp(machine):.4f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 3: Run the tests to see them fail**

`python -m pytest Tools/CandySlots/test_candy_content.py -q` → fails with `KeyError: 'jacksCandyReels'`.

- [ ] **Step 4: Add the catalog entry**

Insert the entry with Python, so the file's existing formatting and the gold entries stay intact:

```bash
python - <<'EOF'
import json
from pathlib import Path

path = Path("Data/LocalStorage/SlotMachineCatalog.json")
ids = json.loads(Path("Tools/CandySlots/art/ids.json").read_text(encoding="utf-8"))["sprites"]
data = json.loads(path.read_text(encoding="utf-8"))

def sym(name, key, tier):
    return {"spriteId": ids[key], "name": name, "tier": tier}

def three(index, multiplier, label):
    return {"kind": "ThreeOfSymbol", "symbolIndex": index, "tier": "Common", "multiplier": multiplier, "isJackpot": False, "label": label}

data["default"]["machines"]["jacksCandyReels"] = {
    "machineName": "Jack's Candy Reels",
    "bet": 1,
    "currencyItemKey": "halloweencandy",
    "currencyName": "candy",
    "hasJackpot": False,
    "jackpotContributionRate": 0,
    "seedReserveRate": 0,
    "symbols": [
        sym("Candy Corn", "candycorn", "Common"),
        sym("Wrapped Candy", "wrappedcandy", "Common"),
        sym("Lollipop", "lollipop", "Common"),
        sym("Bat", "bat", "Mid"),
        sym("Ghost", "ghost", "Mid"),
        sym("Skull", "skull", "Rare"),
        sym("Jack-o'-lantern", "jackolantern", "Rare"),
    ],
    "reels": [
        {"stops": [0, 3, 0, 1, 3, 1, 5, 0, 4, 6, 1, 3, 0, 0, 1, 0, 1, 5, 2, 1, 4, 0, 3, 2, 0, 0, 0, 2, 0, 1, 4, 0]},
        {"stops": [3, 4, 3, 0, 0, 0, 1, 0, 6, 4, 2, 4, 1, 5, 1, 0, 2, 3, 1, 3, 2, 5, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0]},
        {"stops": [2, 4, 1, 0, 1, 0, 6, 1, 3, 5, 0, 0, 1, 4, 0, 0, 4, 0, 0, 0, 3, 2, 1, 0, 3, 0, 3, 1, 2, 1, 5, 0]},
    ],
    "paytable": [
        three(6, 50, "Three jack-o'-lanterns"),
        three(5, 20, "Three skulls"),
        three(4, 10, "Three ghosts"),
        three(3, 10, "Three bats"),
        three(2, 4, "Three lollipops"),
        three(1, 4, "Three wrapped candies"),
        three(0, 4, "Three candy corn"),
        {"kind": "ThreeOfTier", "symbolIndex": 0, "tier": "Common", "multiplier": 2, "isJackpot": False, "label": "Any three candies"},
        {"kind": "TwoMatchingLeft", "symbolIndex": 0, "tier": "Common", "multiplier": 1, "isJackpot": False, "label": "Two matching"},
    ],
    "targetBaseRtp": 0.90,
    "rtpTolerance": 0.01,
}
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
EOF
git diff --stat Data/LocalStorage/SlotMachineCatalog.json
```

If `git diff` shows the gold entries reformatted (for example a different indent or line ending), revert the file with `git checkout -- Data/LocalStorage/SlotMachineCatalog.json`. Then match the original indent in `json.dumps` and keep the file's line endings (`newline=""`), so only the new entry is added. This file is in your own worktree, so the checkout is safe.

- [ ] **Step 5: Merchant template**

Read `art/ids.json` and use its `cabinet` id for `sprite`. Create `Data/Configuration/Templates/Merchants/Temauir/Events/Halloween/slotMachineCandy.json`:

```json
{
  "itemsForSale": [],
  "itemsToBuy": [],
  "name": "Jack's Candy Reels",
  "restockIntervalHrs": 1,
  "restockPct": 100,
  "scriptKeys": [
    "slotMachine"
  ],
  "scriptVars": {
    "slotMachine": {
      "machineKey": "jacksCandyReels"
    }
  },
  "skillsToTeach": [],
  "spellsToTeach": [],
  "sprite": CABINET_ID_FROM_ids.json,
  "templateKey": "slotmachinecandy"
}
```

Replace `CABINET_ID_FROM_ids.json` with the number before saving. The test in Step 1 checks it.

- [ ] **Step 6: Theatre placement**

Append to `Suomi_Theatre_Halloween/merchants.json` (keep the existing 8 entries):

```json
  {
    "blackList": [],
    "direction": "Up",
    "extraScriptKeys": [],
    "merchantTemplateKey": "slotmachinecandy",
    "spawnPoint": "(16, 3)"
  },
  {
    "blackList": [],
    "direction": "Up",
    "extraScriptKeys": [],
    "merchantTemplateKey": "slotmachinecandy",
    "spawnPoint": "(18, 3)"
  }
```

Append to `Suomi_Theatre_Halloween/reactors.json`:

```json
  {
    "scriptKeys": [
      "SlotMachineStool"
    ],
    "scriptVars": {},
    "source": "(16, 4)"
  },
  {
    "scriptKeys": [
      "SlotMachineStool"
    ],
    "scriptVars": {},
    "source": "(18, 4)"
  }
```

- [ ] **Step 7: Run everything**

Run the Verify command. Expected: `jacksCandyReels base payback 0.9000` (plus the gold machines' lines), and all CandySlots tests pass.

---

### Task 8: Boot check, commit and merge locally

**Goal:** The server boots against the Unora worktree data with no slot catalog problems. Each repo gets one commit on `feat/candy-slots`, merged into its local main line. Nothing is pushed.

**Files:**
- Modify (temporarily, restored after): the server worktree's `Chaos/appsettings.local.json` `StagingDirectory`, only if a local override file is how this machine points the server at data (see memory "Local StagingDirectory"); otherwise skip the boot and say so
- Commit: all files from Tasks 2-7, by explicit path
- Modify: client gitlink `Chaos-Server` → the server merge commit

**Acceptance Criteria:**
- [ ] The server boot log has the line "Slot machine catalog validated" and no "Slot machine catalog problem" line; or the reason the boot could not run is reported word for word
- [ ] Server, client and Unora each have one `feat/candy-slots` commit, merged with `--no-ff` into `master`/`main`
- [ ] The client `main`'s `Chaos-Server` gitlink equals the server merge commit
- [ ] `git status` in each shared checkout shows no new changes from this work; nothing pushed

**Verify:** `for r in Chaos.Client/Chaos-Server Chaos.Client Unora; do git -C /c/Users/Michael/Documents/GitHub/$r log --oneline -2; done` → each shows the candy slots merge on top

**Steps:**

- [ ] **Step 1: Boot check**

Ask the user before starting a server, because a running server locks builds and may clash with one already running. If they agree, start the server worktree build pointed at the Unora worktree's `Data`, wait for the world server to listen, then grep the log:

```bash
grep -E "Slot machine catalog (validated|problem)" <server log>
```

Stop the server afterwards and restore any settings file you changed.

- [ ] **Step 2: Commit each worktree by explicit path**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/candy-slots-server
git add -- Chaos/Models/Data/Slots/SlotTierConfig.cs Chaos/Services/Slots/SlotConfigValidator.cs Chaos/Services/Slots/SlotCatalogValidator.cs \
  Chaos/Scripting/MerchantScripts/Casino/SlotMachineScript.cs Chaos/Scripting/ReactorTileScripts/Temauir/Casino/SlotMachineStoolScript.cs \
  Chaos/Services/Servers/WorldServer.cs Chaos/Networking/Abstractions/IChaosWorldClient.cs Chaos/Networking/ChaosWorldClient.cs \
  Chaos.Networking/Entities/Server/SlotMachineDisplayArgs.cs Chaos.Networking/Converters/Server/SlotMachineDisplayConverter.cs \
  Chaos.DarkAges/Definitions/Enums.cs Chaos.DarkAges/Definitions/CONSTANTS.cs \
  Tests/Chaos.Tests/Slots/NoJackpotSlotConfigTests.cs Tests/Chaos.Tests/Slots/SlotMachineScriptCandyTests.cs \
  Tests/Chaos.Tests/Slots/SlotMachineScriptTests.cs Tests/Chaos.Tests/Slots/SlotMachineStoolScriptTests.cs \
  Tests/Chaos.Tests/Networking/SlotPacketConverterTests.cs
git status --short   # must list nothing else as staged
git commit -m "Candy-currency, no-jackpot slot machines (Jack's Candy Reels)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_019VQ5hHsM82iKgMWPzwzW9R"
```

Do the same in the Unora worktree with `Tools/CandySlots` (the folder: art sources, frames JSON, preview, ids.json, scripts, tests, README; not `__pycache__`), the catalog, the template, and the two theatre files. Then do it in the client worktree with `Chaos.Client/ViewModel/SlotMachine.cs`, `Chaos.Client/ViewModel/Inventory.cs`, `Chaos.Client/Controls/World/Popups/Slots/SlotMachineControl.cs` and `Tests/Chaos.Client.Tests/SlotCurrencyTests.cs`.

- [ ] **Step 3: Merge locally**

```bash
cd /c/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server merge --no-ff feat/candy-slots -m "Merge feat/candy-slots: Jack's Candy Reels"
git -C Unora merge --no-ff feat/candy-slots -m "Merge feat/candy-slots: Jack's Candy Reels"
git -C Chaos.Client merge --no-ff feat/candy-slots -m "Merge feat/candy-slots: Jack's Candy Reels"
```

If a shared checkout has uncommitted changes in a file the merge touches, git refuses. Stop and report it; don't stash.

- [ ] **Step 4: Point the client at the server merge**

```bash
cd /c/Users/Michael/Documents/GitHub/Chaos.Client
git -C Chaos-Server log --oneline -1          # the server merge commit
git add -- Chaos-Server
git commit -m "Bump Chaos-Server to the candy slots merge

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_019VQ5hHsM82iKgMWPzwzW9R" -- Chaos-Server
```

The shared `Chaos.Client/Chaos-Server` checkout is the server repo itself, so after Step 3 its HEAD is the merge commit.

- [ ] **Step 5: Report**

List each repo's merge commit hash, the sprite ids, the hades.dat folder, the new `CLIENT_VERSION`, and the shipping order: launcher patch (hades.dat + client build), then the server restart. Remind the user that nothing is pushed.
