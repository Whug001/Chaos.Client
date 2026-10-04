# Josephine's Mirror v3 (Step-by-step Guide) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the one-screen beauty shop window into a five-page guide (Gender → Hair → Skin → Face → Review & pay) with arrow buttons that name the next page, clickable page dots, and a NOW/NEW review page — client only.

**Architecture:** `BeautyShopControl` becomes a shell: frame, preview column, page heading, footer (back arrow, dots, total, next arrow / APPLY) and the current page. Each page is a small `MirrorPageView` subclass in `Beauty/Pages/` that reads `WorldState.BeautyShop` and sends every change through the shell's `Select` / `Hover` funnels (`MirrorActions`). The view model gains per-category cost methods (replacing `HoverTotal`) and bigger page sizes; reusable pieces (`ThumbnailGrid`, `MirrorPreview`, `MirrorLooks`, `PageDots`, a scaled `GenderSelector`) are split out so pages stay small.

**Tech Stack:** .NET 10 / C# 14, MonoGame DesktopGL, TUnit + FluentAssertions (`Tests/Chaos.Client.Tests`).

**Spec:** `docs/superpowers/specs/2026-10-04-beauty-shop-guide-design.md` (client main `956b857a`). Read it with this plan.

## Global Constraints

- Client only. No change to Chaos-Server, Unora data, packets, prices, checkout or `CLIENT_VERSION` (stays 770).
- Window stays 600 × 470, `FramedDialogPanelBase("_nsett", false)`, `Y = 5`, same close button.
- Page order is exactly Gender, Hair, Skin, Face, Review (`MirrorPage` enum order). The window opens on Gender on every `Show()`.
- Mouse only: the Left/Right keys must **not** turn pages. Escape still closes from any page.
- Hair page: 8 × 2 = 16 hairstyle heads per page; hair color swatches 15 px. Skin page: all 10 at once (5 × 2) with names. Face page: 8 × 5 = 40 slots, one page today. Gender buttons drawn at 2x. Review figures 260 × 160 each, NOW left, NEW right.
- Next is always enabled on pages 1–4. On Review the right button reads `APPLY` and is enabled exactly when `CanApply`. A gender change still shows the existing reshape confirm.
- Caption price text: `current` (item is the player's current look), `free` (costs 0 otherwise), else `N0` price.
- The client bitmap font draws ASCII 32–126 only. No `●`, `·`, `✓`, `—`, `◀` in any UI string; use `<`, `>`, `-`.
- Every selection and page step goes through the shell's `Select`; every hover through `Hover`.
- Tests: TUnit via `dotnet run`, never `dotnet test`. New test code has **no explanatory comments** (user rule).
- Commit strategy is at-end: implementer tasks do **not** commit; Task 9 makes the single commit.

**User decisions (already made):**
- Five pages; hairstyle and hair color share the Hair page.
- Guide style: page heading plus arrows that name the page they lead to; no tab row; clicking a dot jumps to that page.
- Mouse only — the keyboard's Left/Right keys do nothing in the mirror.
- Review page shows NOW and NEW figures side by side with the receipt under them.
- Build as one window (shell) with one class per page.
- Drop the dim "if chosen: TOTAL" line and the "* Unsaved changes" label; captions show the pointed-at item's cost instead.

---

## Conventions

- **Worktree.** Do all work in a client worktree on branch `feat/mirror-guide`, plus a detached server worktree used only as the build's server path (no server edits). Several Claude sessions share `C:\Users\Michael\Documents\GitHub\Chaos.Client`; never stash, reset, switch branches or `add -A` there.
  ```bash
  git -C C:/Users/Michael/Documents/GitHub/Chaos.Client worktree add C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-client -b feat/mirror-guide main
  git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server -c core.longpaths=true worktree add --detach C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-server ac3aa4556
  ```
  `ac3aa4556` is the server commit client main records (CLIENT_VERSION 770).
- **Paths below are relative to the client worktree** `C:\Users\Michael\Documents\GitHub\worktrees\mirror-guide-client`. Serena tools take paths relative to `C:\Users\Michael\Documents\GitHub`, so prefix `worktrees/mirror-guide-client/`.
- **Build** (from the client worktree):
  ```bash
  dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-server
  ```
- **Tests** (from the client worktree):
  ```bash
  dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-server
  dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi
  ```
  Add `--treenode-filter "/*/*/BeautyShopViewModelTests/*"` after `--no-ansi` to run one class.
- A running `Chaos.Client.exe` built from the worktree locks its `bin`; ask the user to close it before building.
- `docs/` is gitignored in Chaos.Client; stage docs with `git add -f`.
- Existing APIs relied on: `UIElement.DrawRect/DrawBorder(spriteBatch, Rectangle, Color)`, `UIElement.DrawTexture(spriteBatch, Texture2D, Vector2, Color)`, `UIElement.DrawTextureFitted(spriteBatch, Texture2D?, Rectangle, Color)`, `UIElement.OnMouseEnter/OnMouseLeave/OnMouseMove/OnClick/ResetInteractionState`, `UIPanel.AddChild`, `CustomButton(string caption, int width)` with `HEIGHT`, `Caption`, `Enabled`, `Clicked`, `CustomCheckBox` (`CHECKBOX_SIZE`, `CAPTION_GAP`, `Text`, `Checked`, `Clicked` — it does not toggle itself), `UILabel` (`Text`, `ForegroundColor`, `HorizontalAlignment`), `TextRenderer.CHAR_WIDTH = 6` / `CHAR_HEIGHT = 12`, `LegendColors.Gold/Gray/LightGray/DarkGray/Silver/White/Red/SpringGreen/AlmostBlack/DeepLavender`, `OrnateFrame.BORDER_BOTTOM_HEIGHT = 47` (as `BORDER_BOTTOM_HEIGHT` in `FramedDialogPanelBase`), `UiRenderer.Instance!.GetPrefabTexture(prefab, control, index)`, `DialogFrame.BuildRecessedTexture(SKColor, w, h)`.

## File structure

- `Chaos.Client/ViewModel/BeautyShop.cs` — page sizes 16/40/10, `CostOf*` methods, `HairstylesFor`, `HoverTotal` removed.
- `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs` — paging fixture updated, cost tests replace hover-total tests.
- `Chaos.Client/Controls/World/Popups/Beauty/MirrorPage.cs` (new) — page enum and `MirrorPages` helper (order, titles, arrow labels).
- `Tests/Chaos.Client.Tests/MirrorPagesTests.cs` (new).
- `Chaos.Client/Controls/World/Popups/Beauty/ThumbnailGrid.cs` (new; replaces `ThumbnailStrip.cs`, deleted in Task 7) — multi-row picker grid.
- `Chaos.Client/Controls/World/Popups/Beauty/SwatchGrid.cs` — 15 px swatches.
- `Chaos.Client/Controls/World/Popups/Beauty/GenderSelector.cs` — scale + gap.
- `Chaos.Client/Controls/World/Popups/Beauty/PageDots.cs` (new) — five clickable dots.
- `Chaos.Client/Controls/World/Popups/Beauty/MirrorPreview.cs` (new) — the pedestal figure, sized per use.
- `Chaos.Client/Controls/World/Popups/Beauty/MirrorLooks.cs` (new) — bare / NEW / NOW appearances.
- `Chaos.Client/Controls/World/Popups/Beauty/Pages/MirrorPageView.cs` (new) — `MirrorActions` record and page base class.
- `Chaos.Client/Controls/World/Popups/Beauty/Pages/{GenderPage,HairPage,SkinPage,FacePage,ReviewPage}.cs` (new).
- `Chaos.Client/Controls/World/Popups/Beauty/HeadThumbnailRenderer.cs` — `Count`.
- `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs` — rewritten as the shell.
- `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md` — v3 rows. `CLAUDE.md` — one line for `Popups/Beauty/`.

---

### Task 1: View model — page sizes, per-item costs, hairstyles by gender

**Goal:** The view model shows 16 hairstyles / 40 faces / 10 skins per page and can say what any single choice would cost, using the same rule as `Total`.

**Files:**
- Modify: `Chaos.Client/ViewModel/BeautyShop.cs`
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs` (delete one `HoverTotal` line only)
- Test: `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs`

**Acceptance Criteria:**
- [ ] `HAIRSTYLE_PAGE_SIZE == 16`, `FACE_PAGE_SIZE == 40`, `BODY_COLOR_PAGE_SIZE == 10`.
- [ ] `CostOfHairStyle(int)`, `CostOfHairColor(DisplayColor)`, `CostOfBodyColor(BodyColor)`, `CostOfFace(int)` return 0 for the current value, the list price for another value, and 0 for the waived forced face reset.
- [ ] `Total` equals the gender price (when changed) plus the four costs of the selected values; `HoverTotal` and the private `PriceOf` no longer exist.
- [ ] `HairstylesFor(Gender)` returns that gender's list; `Hairstyles` uses it.
- [ ] All `BeautyShopViewModelTests` pass (19 tests).

**Verify:** `dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/BeautyShopViewModelTests/*"` (after building the test project) → 19 passed, 0 failed

**Steps:**

- [ ] **Step 1: Update and add the tests.** In `BeautyShopViewModelTests.cs`:

Delete the whole `HoverTotal_prices_the_hovered_item_as_if_chosen` test and the whole `HoverTotal_matches_Total_of_the_same_selection` test (including its `<summary>` comment).

In `Hovering_the_selected_item_changes_nothing`, replace the line `vm.HoverTotal.Should().Be(50_000);` with:

```csharp
        vm.Total.Should().Be(50_000);
        vm.CostOfFace(10).Should().Be(50_000);
```

Replace `OpenedWithManyHairstyles`, `Pages_slice_the_list_and_wrap` and `Selecting_moves_the_page_to_the_selection` with:

```csharp
    private static BeautyShop OpenedWithManyHairstyles()
    {
        var args = Open();
        args.MaleHairstyles = Enumerable.Range(0, 40).Select(i => new BeautyShopHairstyleEntry { Sprite = (ushort)i, Price = 1_000 }).ToList();
        var vm = new BeautyShop();
        vm.ApplyOpen(args);

        return vm;
    }

    [Test]
    public async Task Pages_slice_the_list_and_wrap()
    {
        var vm = OpenedWithManyHairstyles();

        vm.HairstylePageCount.Should().Be(3);
        vm.HairstylePage.Should().Be(0);
        vm.VisibleHairstyles.Select(h => (int)h.Sprite).Should().Equal(Enumerable.Range(0, 16));

        vm.StepHairstylePage(+1);
        vm.VisibleHairstyles.Select(h => (int)h.Sprite).Should().Equal(Enumerable.Range(16, 16));
        vm.StepHairstylePage(+1);
        vm.VisibleHairstyles.Select(h => (int)h.Sprite).Should().Equal(Enumerable.Range(32, 8));
        vm.StepHairstylePage(+1);
        vm.HairstylePage.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Selecting_moves_the_page_to_the_selection()
    {
        var vm = OpenedWithManyHairstyles();

        vm.SelectHairStyle(39);
        vm.HairStyle.Should().Be(39);
        vm.HairstylePage.Should().Be(2);

        vm.StepHairstyle(+1);
        vm.HairstylePage.Should().Be(0);

        vm.SelectHairStyle(999);
        vm.HairStyle.Should().Be(0);

        await Task.CompletedTask;
    }
```

Append these three tests at the end of the class:

```csharp
    [Test]
    public async Task CostOf_prices_each_choice_against_the_current_look()
    {
        var vm = Opened();

        vm.CostOfHairStyle(1).Should().Be(0);
        vm.CostOfHairStyle(97).Should().Be(2_500);
        vm.CostOfHairColor(DisplayColor.Default).Should().Be(0);
        vm.CostOfHairColor(DisplayColor.Apple).Should().Be(1_000);
        vm.CostOfBodyColor(BodyColor.White).Should().Be(0);
        vm.CostOfBodyColor(BodyColor.Tan).Should().Be(1_000);
        vm.CostOfFace(1).Should().Be(0);
        vm.CostOfFace(10).Should().Be(50_000);

        vm.SelectHairStyle(97);
        vm.CostOfHairStyle(0).Should().Be(1_000);
        vm.CostOfHairStyle(1).Should().Be(0);

        vm.HairstylesFor(Gender.Female).Select(h => (int)h.Sprite).Should().Equal(0, 1, 96);
        vm.HairstylesFor(Gender.Male).Select(h => (int)h.Sprite).Should().Equal(0, 1, 97);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Total_is_the_gender_price_plus_the_four_costs()
    {
        var vm = Opened();
        vm.SelectHairStyle(97);
        vm.SelectHairColor(DisplayColor.Scarlet);
        vm.SelectBodyColor(BodyColor.Tan);
        vm.SelectFace(10);

        vm.Total.Should().Be(vm.CostOfHairStyle(97) + vm.CostOfHairColor(DisplayColor.Scarlet) + vm.CostOfBodyColor(BodyColor.Tan) + vm.CostOfFace(10));
        vm.Total.Should().Be(54_500);

        var waived = new BeautyShop();
        waived.ApplyOpen(OpenAsFemaleWithRestingFace());
        waived.SetGender(Gender.Male);

        waived.FaceSprite.Should().Be(1);
        waived.CostOfFace(1).Should().Be(0);
        waived.CostOfFace(10).Should().Be(50_000);
        waived.Total.Should().Be(50_000);

        waived.SelectFace(10);
        waived.Total.Should().Be(waived.GenderPrice + waived.CostOfFace(10));
        waived.Total.Should().Be(100_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_face_and_skin_fits_on_one_page()
    {
        var args = Open();
        args.Faces = Enumerable.Range(1, 35)
                               .Select(i => new BeautyShopFaceEntry { Sprite = (byte)i, Name = $"Face {i}", Price = 50_000, FemaleOnly = false })
                               .ToList();
        args.BodyColors = Enum.GetValues<BodyColor>().ToList();
        var vm = new BeautyShop();
        vm.ApplyOpen(args);

        vm.FacePageCount.Should().Be(1);
        vm.VisibleFaces.Should().HaveCount(35);
        vm.BodyColorPageCount.Should().Be(1);
        vm.VisibleBodyColors.Should().HaveCount(10);

        await Task.CompletedTask;
    }
```

- [ ] **Step 2: Build the test project and confirm it fails to compile** (`CostOfHairStyle`, `HairstylesFor` do not exist yet).

Run: `dotnet build Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-server`
Expected: FAIL with `CS1061 ... 'BeautyShop' does not contain a definition for 'CostOfHairStyle'`.

- [ ] **Step 3: Change the view model.** In `Chaos.Client/ViewModel/BeautyShop.cs`:

Replace the `Hairstyles` property with:

```csharp
    public IReadOnlyList<BeautyShopHairstyleEntry> Hairstyles => HairstylesFor(Gender);

    public IReadOnlyList<BeautyShopHairstyleEntry> HairstylesFor(Gender gender) => gender == Gender.Male ? MaleHairstyles : FemaleHairstyles;
```

Replace the `FaceCharged` property (keep its `<summary>`) with:

```csharp
    public bool FaceCharged => FaceChanged && !IsFaceResetWaived(Gender, FaceSprite);
```

Replace the `Total` property with:

```csharp
    public int Total
        => (GenderChanged ? GenderPrice : 0)
           + CostOfHairStyle(HairStyle)
           + CostOfHairColor(HairColor)
           + CostOfBodyColor(BodyColor)
           + CostOfFace(FaceSprite);
```

Delete the `HoverTotal` property (with its `<summary>`) and the whole private `PriceOf` method (with its `<summary>`). In their place (after `ClearHover()`), add:

```csharp
    /// <summary>What choosing <paramref name="sprite" /> as the hairstyle adds to <see cref="Total" />, given the selected gender. 0 for the current hairstyle.</summary>
    public int CostOfHairStyle(int sprite)
        => sprite == CurrentHairStyle ? 0 : Hairstyles.FirstOrDefault(h => h.Sprite == sprite)?.Price ?? 0;

    /// <summary>What choosing <paramref name="color" /> as the hair dye adds to <see cref="Total" />. 0 for the current dye.</summary>
    public int CostOfHairColor(DisplayColor color) => color != CurrentHairColor ? HairDyePrice : 0;

    /// <summary>What choosing <paramref name="color" /> as the skin adds to <see cref="Total" />. 0 for the current skin.</summary>
    public int CostOfBodyColor(BodyColor color) => color != CurrentBodyColor ? BodyDyePrice : 0;

    /// <summary>What choosing <paramref name="sprite" /> as the face adds to <see cref="Total" />, given the selected gender. 0 for the current face and for the waived forced reset.</summary>
    public int CostOfFace(int sprite)
        => (sprite == CurrentFaceSprite) || IsFaceResetWaived(Gender, sprite) ? 0 : Faces.FirstOrDefault(f => f.Sprite == sprite)?.Price ?? 0;

    /// <summary>
    ///     The server's free forced-face-reset rule (BeautyShopCheckout.TryApply): switching gender away from a face the
    ///     new gender can't wear forces a reset to the default face (the catalog's first entry) for free -- picking any
    ///     other face is still a purchase.
    /// </summary>
    private bool IsFaceResetWaived(Gender gender, int faceSprite)
        => (gender != CurrentGender) && !IsFaceAvailable(gender, CurrentFaceSprite) && (Faces.Count > 0) && (faceSprite == Faces[0].Sprite);
```

Change the three page-size constants:

```csharp
    public const int HAIRSTYLE_PAGE_SIZE = 16;
    public const int FACE_PAGE_SIZE = 40;
    public const int BODY_COLOR_PAGE_SIZE = 10;
```

- [ ] **Step 4: Keep the old window compiling.** In `BeautyShopControl.RefreshSummary`, delete only the line that starts `HoverTotalLabel.Text = (vm.IsHovering && (vm.HoverTotal`. (The whole window is rewritten in Task 7.)

- [ ] **Step 5: Build and run the view-model tests.**

Run: the two **Tests** commands from Conventions, with `--treenode-filter "/*/*/BeautyShopViewModelTests/*"`.
Expected: 19 passed, 0 failed. Then build the client (Conventions **Build**): `Build succeeded`, 0 errors.

```json:metadata
{"files": ["Chaos.Client/ViewModel/BeautyShop.cs", "Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs", "Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs"], "verifyCommand": "dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/BeautyShopViewModelTests/*\"", "acceptanceCriteria": ["page sizes 16/40/10", "CostOf* return 0 for current, list price otherwise, 0 for waived face", "Total = gender price + four costs; HoverTotal and PriceOf removed", "HairstylesFor(Gender) exists and Hairstyles uses it", "19 BeautyShopViewModelTests pass"], "modelTier": "mechanical"}
```

---

### Task 2: Page order helper

**Goal:** A pure `MirrorPage` enum and `MirrorPages` helper give the page order, titles, instructions and arrow labels.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/Beauty/MirrorPage.cs`
- Test: `Tests/Chaos.Client.Tests/MirrorPagesTests.cs`

**Acceptance Criteria:**
- [ ] `MirrorPage` values in order: Gender, Hair, Skin, Face, Review; `MirrorPages.COUNT == 5`.
- [ ] `Previous(Gender)` and `Next(Review)` are null; the others step by one.
- [ ] `NextLabel` is `"<Name> >"` or `"APPLY"` on Review; `BackLabel` is `"< <Name>"` or empty on Gender.
- [ ] Every title, instruction and label is ASCII 32–126 and each instruction fits 318 px (53 characters).
- [ ] All `MirrorPagesTests` pass (4 tests).

**Verify:** `dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorPagesTests/*"` → 4 passed

**Steps:**

- [ ] **Step 1: Write the failing tests** — create `Tests/Chaos.Client.Tests/MirrorPagesTests.cs`:

```csharp
using Chaos.Client.Controls.World.Popups.Beauty;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class MirrorPagesTests
{
    [Test]
    public async Task Pages_run_gender_hair_skin_face_review()
    {
        Enum.GetValues<MirrorPage>().Should().Equal(MirrorPage.Gender, MirrorPage.Hair, MirrorPage.Skin, MirrorPage.Face, MirrorPage.Review);
        MirrorPages.COUNT.Should().Be(5);
        MirrorPages.First.Should().Be(MirrorPage.Gender);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Previous_and_Next_stop_at_the_ends()
    {
        MirrorPages.Previous(MirrorPage.Gender).Should().BeNull();
        MirrorPages.Next(MirrorPage.Gender).Should().Be(MirrorPage.Hair);
        MirrorPages.Previous(MirrorPage.Skin).Should().Be(MirrorPage.Hair);
        MirrorPages.Next(MirrorPage.Face).Should().Be(MirrorPage.Review);
        MirrorPages.Previous(MirrorPage.Review).Should().Be(MirrorPage.Face);
        MirrorPages.Next(MirrorPage.Review).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Arrow_labels_name_the_page_they_lead_to()
    {
        MirrorPages.BackLabel(MirrorPage.Gender).Should().BeEmpty();
        MirrorPages.NextLabel(MirrorPage.Gender).Should().Be("Hair >");
        MirrorPages.BackLabel(MirrorPage.Skin).Should().Be("< Hair");
        MirrorPages.NextLabel(MirrorPage.Skin).Should().Be("Face >");
        MirrorPages.NextLabel(MirrorPage.Face).Should().Be("Review >");
        MirrorPages.BackLabel(MirrorPage.Review).Should().Be("< Face");
        MirrorPages.NextLabel(MirrorPage.Review).Should().Be("APPLY");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Page_text_is_plain_ascii_and_fits_the_column()
    {
        foreach (var page in Enum.GetValues<MirrorPage>())
        {
            MirrorPages.Title(page).Should().NotBeNullOrEmpty();

            var text = MirrorPages.Title(page) + MirrorPages.Name(page) + MirrorPages.Instruction(page) + MirrorPages.BackLabel(page) + MirrorPages.NextLabel(page);
            text.Should().MatchRegex("^[\\x20-\\x7E]*$");

            MirrorPages.Instruction(page).Length.Should().BeLessThanOrEqualTo(53);
        }

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Build the test project; expect a compile failure** (`MirrorPage` does not exist).

Run: the test-project build command from Conventions.
Expected: FAIL with `CS0246 ... 'MirrorPage' could not be found`.

- [ ] **Step 3: Create `Chaos.Client/Controls/World/Popups/Beauty/MirrorPage.cs`:**

```csharp
namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>The mirror's pages, in the order the guide walks through them.</summary>
public enum MirrorPage
{
    Gender,
    Hair,
    Skin,
    Face,
    Review
}

/// <summary>Page order and the words each page shows: its heading, its one-line instruction and the arrow labels.</summary>
public static class MirrorPages
{
    public const int COUNT = 5;

    public static MirrorPage First => MirrorPage.Gender;

    public static MirrorPage? Previous(MirrorPage page) => page == MirrorPage.Gender ? null : page - 1;

    public static MirrorPage? Next(MirrorPage page) => page == MirrorPage.Review ? null : page + 1;

    /// <summary>The page heading, in capitals.</summary>
    public static string Title(MirrorPage page) => Name(page).ToUpperInvariant();

    /// <summary>The page's name as the arrows show it.</summary>
    public static string Name(MirrorPage page)
        => page switch
        {
            MirrorPage.Gender => "Gender",
            MirrorPage.Hair   => "Hair",
            MirrorPage.Skin   => "Skin",
            MirrorPage.Face   => "Face",
            MirrorPage.Review => "Review",
            _                 => throw new ArgumentOutOfRangeException(nameof(page), page, null)
        };

    /// <summary>The gray line under the heading. The review page draws its own heading and has none.</summary>
    public static string Instruction(MirrorPage page)
        => page switch
        {
            MirrorPage.Gender => "Choose male or female.",
            MirrorPage.Hair   => "Pick a style, then a color. Hover to try one on.",
            MirrorPage.Skin   => "Choose a skin color. Hover to try one on.",
            MirrorPage.Face   => "Choose a face. Hover to try one on.",
            _                 => string.Empty
        };

    public static string BackLabel(MirrorPage page) => Previous(page) is { } previous ? $"< {Name(previous)}" : string.Empty;

    public static string NextLabel(MirrorPage page) => Next(page) is { } next ? $"{Name(next)} >" : "APPLY";
}
```

- [ ] **Step 4: Build the test project and run the class.**

Run: Conventions **Tests** commands with `--treenode-filter "/*/*/MirrorPagesTests/*"`.
Expected: 4 passed, 0 failed.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/MirrorPage.cs", "Tests/Chaos.Client.Tests/MirrorPagesTests.cs"], "verifyCommand": "dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MirrorPagesTests/*\"", "acceptanceCriteria": ["enum order Gender, Hair, Skin, Face, Review; COUNT 5", "Previous(Gender) and Next(Review) null", "NextLabel '<Name> >' or APPLY; BackLabel '< <Name>' or empty", "ASCII-only text, instructions <= 53 chars", "4 MirrorPagesTests pass"], "modelTier": "mechanical"}
```

---

### Task 3: Picker widgets — ThumbnailGrid, bigger swatches, scaled gender buttons, page dots

**Goal:** The reusable controls the pages need exist and build: a multi-row thumbnail grid, 15 px swatches, a gender selector that draws at 2x, and clickable page dots.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/Beauty/ThumbnailGrid.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/PageDots.cs`
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/SwatchGrid.cs`
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/GenderSelector.cs`

**Acceptance Criteria:**
- [ ] `ThumbnailGrid<T>(columns, rows, columnGap = 3, rowGap = 3)` lays cells out row by row, exposes `CellLeft`, `CellOrigin(int)`, `WidthFor`, `HeightFor`, and `SetItems(items, selected, pageCount, thumbFor)` hides both arrows when `pageCount <= 1`. `WidthFor(8) == 315`, `HeightFor(2) == 67`.
- [ ] `SwatchGrid.SWATCH == 15`.
- [ ] `new GenderSelector(2, 52)` is 228 × 88 and hit-tests and draws both buttons at 2x; `new GenderSelector()` behaves as before.
- [ ] `PageDots(5)` is 80 × 16, draws the current dot gold, and raises `PageChosen(index)` when another dot is clicked.
- [ ] `ThumbnailStrip.cs` is left in place (the old window still uses it until Task 7); the client builds with 0 errors.

**Verify:** Conventions **Build** → `Build succeeded`, 0 errors

**Steps:**

- [ ] **Step 1: Create `ThumbnailGrid.cs`.** It generalises `ThumbnailStrip<T>`; the `Cell` class and the hover re-announce block are copied from it unchanged.

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     One page of picker cells laid out row by row, with page arrows on both sides that hide when everything fits on
///     one page. Holds no state of its own beyond the items it was last given: the page calls <see cref="SetItems" />
///     on every refresh and reacts to the events.
/// </summary>
public sealed class ThumbnailGrid<T> : UIPanel where T : notnull
{
    public const int CELL = 32;
    public const int GAP = 3;
    public const int ARROW_WIDTH = 16;

    private readonly Cell[] Cells;
    private readonly CustomButton Left;
    private readonly CustomButton Right;
    private readonly int Columns;
    private readonly int ColumnPitch;
    private readonly int RowPitch;

    public event Action<T>? Hovered;
    public event Action? HoverCleared;
    public event Action<T>? Selected;
    public event Action<int>? PageStepped;

    /// <summary>X of the first cell column inside the grid, after the left arrow.</summary>
    public int CellLeft => ARROW_WIDTH + GAP;

    public static int WidthFor(int columns, int columnGap = GAP) => ARROW_WIDTH + GAP + ((columns * (CELL + columnGap)) - columnGap) + GAP + ARROW_WIDTH;

    public static int HeightFor(int rows, int rowGap = GAP) => (rows * (CELL + rowGap)) - rowGap;

    public ThumbnailGrid(int columns, int rows, int columnGap = GAP, int rowGap = GAP)
    {
        Background = null;
        Columns = columns;
        ColumnPitch = CELL + columnGap;
        RowPitch = CELL + rowGap;
        Width = WidthFor(columns, columnGap);
        Height = HeightFor(rows, rowGap);

        var arrowY = (Height - CustomButton.HEIGHT) / 2;

        Left = new CustomButton("<", ARROW_WIDTH) { X = 0, Y = arrowY };
        Left.Clicked += () => PageStepped?.Invoke(-1);
        AddChild(Left);

        Cells = new Cell[columns * rows];

        for (var i = 0; i < Cells.Length; i++)
        {
            var origin = CellOrigin(i);
            var cell = new Cell { X = origin.X, Y = origin.Y, Width = CELL, Height = CELL, Visible = false };
            cell.Hovered += item => Hovered?.Invoke((T)item);
            cell.HoverCleared += () => HoverCleared?.Invoke();
            cell.Clicked += item => Selected?.Invoke((T)item);
            Cells[i] = cell;
            AddChild(cell);
        }

        Right = new CustomButton(">", ARROW_WIDTH) { X = Width - ARROW_WIDTH, Y = arrowY };
        Right.Clicked += () => PageStepped?.Invoke(+1);
        AddChild(Right);
    }

    /// <summary>Top-left of cell <paramref name="index" />, relative to the grid.</summary>
    public Point CellOrigin(int index) => new(CellLeft + ((index % Columns) * ColumnPitch), (index / Columns) * RowPitch);

    /// <summary>
    ///     Shows one page. <paramref name="selected" /> is compared with <see cref="object.Equals(object)" />;
    ///     <paramref name="thumbFor" /> may return null (cell draws its border only). The arrows show only when
    ///     <paramref name="pageCount" /> is more than one.
    /// </summary>
    /// <remarks>
    ///     There is no "no selection" sentinel for value-type <typeparamref name="T" />: pass a real, currently-selected
    ///     value -- <c>default</c> would just compare equal to whatever item equals <c>default(T)</c>.
    /// </remarks>
    public void SetItems(IReadOnlyList<T> items, T? selected, int pageCount, Func<T, Texture2D?> thumbFor)
    {
        Left.Visible = pageCount > 1;
        Right.Visible = pageCount > 1;

        for (var i = 0; i < Cells.Length; i++)
        {
            var cell = Cells[i];
            var previousItem = cell.Item;

            if (i < items.Count)
            {
                cell.Visible = true;
                cell.Item = items[i];
                cell.Thumbnail = thumbFor(items[i]);
                cell.IsSelected = (selected is not null) && items[i].Equals(selected);
            } else
            {
                cell.Visible = false;
                cell.Item = null;
                cell.Thumbnail = null;
                cell.IsSelected = false;
            }

            //InputDispatcher only re-runs hit-testing on cursor movement and diffs the hovered element by
            //identity. Paging reuses these same Cell instances for different items (or hides them outright),
            //so a cell the dispatcher still considers "hovered" gets no Enter/Leave call when its item changes
            //out from under it -- the hover has to be re-announced here by hand, or a stale item (or a hidden
            //cell) stays "hovered" until the mouse physically moves again.
            if (cell.IsHovered && !Equals(previousItem, cell.Item))
            {
                if (cell.Item is not null)
                    Hovered?.Invoke((T)cell.Item);
                else
                {
                    HoverCleared?.Invoke();
                    cell.IsHovered = false;
                }
            }
        }
    }
}
```

Then copy the whole nested `private sealed class Cell : UIElement { ... }` (with its `<summary>`) from `ThumbnailStrip.cs` into `ThumbnailGrid<T>` unchanged, just before the class's closing brace.

- [ ] **Step 2: Bigger swatches.** In `SwatchGrid.cs` change `public const int SWATCH = 12;` to `public const int SWATCH = 15;` and in its class `<summary>` change `<see cref="ThumbnailStrip{T}" />` to `<see cref="ThumbnailGrid{T}" />`.

- [ ] **Step 3: Scaled gender buttons.** In `GenderSelector.cs`:

Replace the `<summary>` on `BUTTON_SIZE` with `/// <summary>Size of one gender button at scale 1, from the character creation art (<c>_ncbg.spf</c>).</summary>`. Keep `GAP` and `HEIGHT` (the old window uses `HEIGHT` until Task 7).

Add fields after `FemaleLit`:

```csharp
    private readonly int Scale;
    private readonly int ButtonSize;
    private readonly int ButtonGap;
```

Replace the constructor with:

```csharp
    /// <param name="scale">Whole-number zoom for the art. The UI pass point-samples, so 2 stays crisp.</param>
    /// <param name="gap">Space between the two buttons, in screen pixels.</param>
    public GenderSelector(int scale = 1, int gap = GAP)
    {
        Background = null;
        Scale = scale;
        ButtonSize = BUTTON_SIZE * scale;
        ButtonGap = gap;
        Width = (ButtonSize * 2) + ButtonGap;
        Height = ButtonSize;

        var cache = UiRenderer.Instance!;
        MaleNormal = cache.GetPrefabTexture(PREFAB_NAME, "Male", 0);
        MaleLit = cache.GetPrefabTexture(PREFAB_NAME, "Male", 1);
        FemaleNormal = cache.GetPrefabTexture(PREFAB_NAME, "Female", 0);
        FemaleLit = cache.GetPrefabTexture(PREFAB_NAME, "Female", 1);
    }
```

Replace `ButtonAt` (it is no longer static):

```csharp
    //null in the gap between the two buttons
    private Gender? ButtonAt(int localX)
        => localX < ButtonSize ? Gender.Male
            : localX >= ButtonSize + ButtonGap ? Gender.Female
            : null;
```

In `Draw`, change the female call's offset from `BUTTON_SIZE + GAP` to `ButtonSize + ButtonGap`. Replace `DrawButton` with:

```csharp
    //selected: lit. Hovered but not selected: the lit art at part strength, so hover never reads as a selection.
    private void DrawButton(SpriteBatch spriteBatch, Gender gender, Texture2D normal, Texture2D lit, int offsetX)
    {
        var bounds = new Rectangle(ScreenX + offsetX, ScreenY, normal.Width * Scale, normal.Height * Scale);

        if (gender == SelectedGender)
        {
            DrawTextureFitted(spriteBatch, lit, bounds, Color.White);

            return;
        }

        DrawTextureFitted(spriteBatch, normal, bounds, Color.White);

        if (HoveredGender == gender)
            DrawTextureFitted(spriteBatch, lit, bounds, Color.White * 0.5f);
    }
```

- [ ] **Step 4: Create `PageDots.cs`:**

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     One dot per page, the current one gold. Each dot's click target is its whole <see cref="SPACING" />-wide slot.
///     Clicking a dot other than the current one raises <see cref="PageChosen" />.
/// </summary>
public sealed class PageDots : UIElement
{
    public const int DOT = 8;
    public const int SPACING = 16;

    private readonly int Count;
    private int Current;
    private int? Hovered;

    public event Action<int>? PageChosen;

    public PageDots(int count)
    {
        Count = count;
        Width = count * SPACING;
        Height = SPACING;
    }

    public void SetCurrent(int index) => Current = index;

    private int? DotAt(int localX) => (localX >= 0) && (localX < Width) ? localX / SPACING : null;

    public override void OnMouseMove(MouseMoveEvent e) => Hovered = DotAt(e.ScreenX - ScreenX);

    public override void OnMouseLeave() => Hovered = null;

    public override void ResetInteractionState() => Hovered = null;

    public override void OnClick(ClickEvent e)
    {
        e.Handled = true;

        if (DotAt(e.ScreenX - ScreenX) is { } index && (index != Current))
            PageChosen?.Invoke(index);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        //primes ClipRect, which hit-testing (ContainsPoint) depends on
        base.Draw(spriteBatch);

        for (var i = 0; i < Count; i++)
        {
            var x = ScreenX + (i * SPACING) + ((SPACING - DOT) / 2);
            var y = ScreenY + ((SPACING - DOT) / 2);

            var fill = i == Current ? LegendColors.Gold
                : i == Hovered ? LegendColors.Silver
                : LegendColors.DarkGray;

            //two overlapping rects with the corners left out read as a round dot at this size
            DrawRect(spriteBatch, new Rectangle(x + 1, y, DOT - 2, DOT), fill);
            DrawRect(spriteBatch, new Rectangle(x, y + 1, DOT, DOT - 2), fill);
        }
    }
}
```

If the build reports `MouseMoveEvent`/`ClickEvent` missing, copy `GenderSelector.cs`'s `using` block — it uses both.

- [ ] **Step 5: Build.** Conventions **Build** → `Build succeeded`, 0 errors.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/ThumbnailGrid.cs", "Chaos.Client/Controls/World/Popups/Beauty/PageDots.cs", "Chaos.Client/Controls/World/Popups/Beauty/SwatchGrid.cs", "Chaos.Client/Controls/World/Popups/Beauty/GenderSelector.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-server", "acceptanceCriteria": ["ThumbnailGrid<T>(columns, rows, columnGap, rowGap) with CellLeft, CellOrigin, WidthFor(8)==315, HeightFor(2)==67, arrows hidden when pageCount<=1", "SwatchGrid.SWATCH == 15", "GenderSelector(2, 52) is 228x88 and draws/hit-tests at 2x; default ctor unchanged", "PageDots(5) is 80x16, current dot gold, PageChosen on another dot", "ThumbnailStrip.cs untouched; client builds with 0 errors"], "modelTier": "standard"}
```

---

### Task 4: Shared figure and looks — MirrorPreview, MirrorLooks

**Goal:** The pedestal figure and the look-building code live in their own files so the shell (one 230 × 250 figure) and the review page (two 260 × 160 figures) can share them.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/Beauty/MirrorPreview.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/MirrorLooks.cs`

**Acceptance Criteria:**
- [ ] `MirrorPreview(AislingRenderer, int width, int height)` has `FACING_COUNT`, `Zoomed`, `Refresh(appearance, facingIndex)`, `Release()`, `Draw`, `Dispose`, and `static CreateGearToggle(x, y)`; its render cache, 2x-fit fallback and body-centre anchoring are the same as today's nested `PreviewView`.
- [ ] `MirrorLooks.Bare(...)`, `MirrorLooks.New(vm, showGear)` (effective, hover-aware values) and `MirrorLooks.Now(vm, showGear)` (current values) exist; with `showGear` they take the live player appearance and swap the five fields in.
- [ ] The old window is untouched; the client builds with 0 errors.

**Verify:** Conventions **Build** → `Build succeeded`, 0 errors

**Steps:**

- [ ] **Step 1: Create `MirrorPreview.cs`** — the body is today's `BeautyShopControl.PreviewView`, sized by its constructor:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     A figure on a recessed pedestal. Owns exactly one composited texture at a time --
///     <see cref="AislingRenderer.Render" /> allocates a fresh texture per call and caches only per world entity, so this
///     view caches by (appearance, facing) and disposes the previous texture on every re-render (see
///     PokerTableControl's PortraitView for the same reasoning). Zoom is a pure draw-time scale -- it never invalidates
///     the cache.
/// </summary>
public sealed class MirrorPreview : UIElement
{
    public const int FACING_COUNT = 4;

    //(frame, flip, isFront): Down, Right, Up, Left. Epfs hold up (0-4) and right (5-9); down = right flipped, left = up flipped.
    private static readonly (int Frame, bool Flip, bool IsFront)[] FACINGS =
    [
        (5, true, true),
        (5, false, true),
        (0, false, false),
        (0, true, false)
    ];

    private readonly AislingRenderer Renderer;
    private readonly Texture2D Pedestal;

    private Texture2D? Figure;
    private AislingAppearance? RenderedAppearance;
    private int RenderedFacing = -1;

    public bool Zoomed { get; set; } = true;

    public MirrorPreview(AislingRenderer renderer, int width, int height)
    {
        Renderer = renderer;
        Width = width;
        Height = height;
        Pedestal = DialogFrame.BuildRecessedTexture(new SKColor(24, 22, 30), width, height);
    }

    /// <summary>The "Show gear" checkbox both the preview column and the review page put under their figures. It does not toggle itself.</summary>
    public static CustomCheckBox CreateGearToggle(int x, int y)
        => new()
        {
            X = x,
            Y = y,
            Width = CustomCheckBox.CHECKBOX_SIZE + CustomCheckBox.CAPTION_GAP + (9 * TextRenderer.CHAR_WIDTH),
            Height = CustomCheckBox.CHECKBOX_SIZE,
            Text = "Show gear",
            Checked = false
        };

    public void Refresh(AislingAppearance appearance, int facingIndex)
    {
        if (Nullable.Equals(appearance, RenderedAppearance) && (facingIndex == RenderedFacing))
            return;

        RenderedAppearance = appearance;
        RenderedFacing = facingIndex;

        Figure?.Dispose();
        var (frame, flip, isFront) = FACINGS[facingIndex];
        Figure = Renderer.Render(in appearance, frame, AislingRenderer.IDLE_ANIM, flip, isFront);

        if (Figure is null)
            RenderedFacing = -1;
    }

    /// <summary>Drops the texture on hide so a closed panel holds no GPU memory; the next Refresh re-renders.</summary>
    public void Release()
    {
        Figure?.Dispose();
        Figure = null;
        RenderedAppearance = null;
        RenderedFacing = -1;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        DrawTexture(spriteBatch, Pedestal, new Vector2(ScreenX, ScreenY), Color.White);

        if (Figure is null)
            return;

        var scale = Zoomed ? 2 : 1;

        //DrawTextureFitted culls when the destination rect doesn't intersect ClipRect at all -- it does not
        //clip to it -- so an oversized composite (e.g. Show gear on with a tall equip layer) can paint outside
        //the pedestal. Fall back to 1x for this draw alone (the toggle state itself is untouched) when 2x
        //wouldn't fit.
        if (((Figure.Height * scale) > (Height - 12)) || ((Figure.Width * scale) > Width))
            scale = 1;

        var w = Figure.Width * scale;
        var h = Figure.Height * scale;

        //anchor on the body centre so the figure stays centred in the pedestal at 1x and 2x and doesn't drift
        //sideways between poses whose padded canvases differ in width
        var x = ScreenX + (Width / 2) - (AislingRenderer.CANVAS_CENTER_X * scale);
        var y = ScreenY + (Height / 2) - (AislingRenderer.BODY_CENTER_Y * scale);

        DrawTextureFitted(spriteBatch, Figure, new Rectangle(x, y, w, h), Color.White);
    }

    public override void Dispose()
    {
        Release();
        Pedestal.Dispose();
        base.Dispose();
    }
}
```

- [ ] **Step 2: Create `MirrorLooks.cs`:**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     The looks the mirror draws. Bare body + hair + face by default so every change is visible; with Show gear on,
///     the player's live world appearance (armor, helmet, weapon...) with the five editable fields swapped in.
/// </summary>
public static class MirrorLooks
{
    public static AislingAppearance Bare(Gender gender, int hairStyle, DisplayColor hairColor, BodyColor bodyColor, int faceSprite)
        => new()
        {
            Gender = gender,
            BodySpriteId = AislingRenderer.BODY_ID,
            BodyColor = (int)bodyColor,
            HeadSprite = hairStyle,
            HeadColor = hairColor,
            FaceSprite = faceSprite
        };

    /// <summary>The look being tried on. Uses the effective (hover-aware) values so the figure shows what the player is pointing at.</summary>
    public static AislingAppearance New(ViewModel.BeautyShop vm, bool showGear)
        => Dressed(showGear, vm.Gender, vm.EffectiveHairStyle, vm.EffectiveHairColor, vm.EffectiveBodyColor, vm.EffectiveFaceSprite);

    /// <summary>The look the player had when the mirror opened.</summary>
    public static AislingAppearance Now(ViewModel.BeautyShop vm, bool showGear)
        => Dressed(showGear, vm.CurrentGender, vm.CurrentHairStyle, vm.CurrentHairColor, vm.CurrentBodyColor, vm.CurrentFaceSprite);

    private static AislingAppearance Dressed(bool showGear, Gender gender, int hairStyle, DisplayColor hairColor, BodyColor bodyColor, int faceSprite)
    {
        var bare = Bare(gender, hairStyle, hairColor, bodyColor, faceSprite);

        if (!showGear)
            return bare;

        var live = WorldState.GetPlayerEntity()?.Appearance;

        if (live is null)
            return bare;

        return live.Value with
        {
            Gender = gender,
            BodyColor = (int)bodyColor,
            HeadSprite = hairStyle,
            HeadColor = hairColor,
            FaceSprite = faceSprite
        };
    }
}
```

If `AislingAppearance` or `BODY_ID` is not found, check the `using` block of today's `BeautyShopControl.cs` (it builds the same appearance in `BareAppearance`).

- [ ] **Step 3: Build.** Conventions **Build** → `Build succeeded`, 0 errors.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/MirrorPreview.cs", "Chaos.Client/Controls/World/Popups/Beauty/MirrorLooks.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-server", "acceptanceCriteria": ["MirrorPreview(renderer, width, height) with FACING_COUNT, Zoomed, Refresh, Release, Draw, Dispose, CreateGearToggle; same cache/fit/anchor logic as PreviewView", "MirrorLooks.Bare/New(effective)/Now(current) with live-gear swap", "old window untouched; client builds with 0 errors"], "modelTier": "standard"}
```

---

### Task 5: Picker pages — Gender, Hair, Skin, Face

**Goal:** Four page panels, built on a shared base, show the gender choice, the 16-head hairstyle grid with dye swatches, all skins with names, and all faces, each with a caption that names the pointed-at item and its cost.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/Beauty/Pages/MirrorPageView.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/Pages/GenderPage.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/Pages/HairPage.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/Pages/SkinPage.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/Pages/FacePage.cs`

**Acceptance Criteria:**
- [ ] `MirrorActions(Select, Hover, Thumbnails)` record and abstract `MirrorPageView(actions, width, height)` with `Refresh()`, `AddLabel`, `BaseLook`, `CostText`, `Position`, `FaceName`; pages start hidden.
- [ ] Gender page: 2x selector centred, "Male"/"Female" names, "(current)" under the player's own gender, price line from `GenderPrice`, and the four fixed note lines.
- [ ] Hair page: caption `Hairstyle   n / N - cost` with `PAGE p/P` right-aligned, an 8 × 2 grid, caption `Hair color   Name - cost`, swatch grid aligned with the first cell column.
- [ ] Skin page: caption `Skin   Name - cost`, a 5 × 2 grid on a 60 px column pitch, each cell's name centred under it.
- [ ] Face page: caption `Face   Name - cost`, an 8 × 5 grid, no arrows while one page.
- [ ] Every hover goes through `Actions.Hover`, every select and page step through `Actions.Select`; captions use the hovered value when there is one.
- [ ] The client builds with 0 errors (the pages are not shown yet).

**Verify:** Conventions **Build** → `Build succeeded`, 0 errors

**Steps:**

- [ ] **Step 1: Create `Pages/MirrorPageView.cs`:**

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>
///     The shell's change funnels, handed to every page so every change still goes through
///     <see cref="BeautyShopControl" />. <see cref="Select" /> is for selections and page steps (it tears down a stale
///     confirm and repaints); <see cref="Hover" /> only repaints.
/// </summary>
public sealed record MirrorActions(
    Action<Action<ViewModel.BeautyShop>> Select,
    Action<Action<ViewModel.BeautyShop>> Hover,
    HeadThumbnailRenderer Thumbnails);

/// <summary>
///     One page of the mirror. The shell builds every page once, shows one at a time and calls <see cref="Refresh" />
///     on the visible one after every change.
/// </summary>
public abstract class MirrorPageView : UIPanel
{
    protected MirrorActions Actions { get; }

    protected MirrorPageView(MirrorActions actions, int width, int height)
    {
        Background = null;
        Actions = actions;
        Width = width;
        Height = height;
        Visible = false;
    }

    /// <summary>Repaints the page from <see cref="Collections.WorldState.BeautyShop" />.</summary>
    public abstract void Refresh();

    protected UILabel AddLabel(int x, int y, int width, Color? color = null, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color ?? LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(label);

        return label;
    }

    /// <summary>The look thumbnails are built on: the selected (not hovered) values, so cells never flicker while the player hovers.</summary>
    protected static AislingAppearance BaseLook(ViewModel.BeautyShop vm)
        => MirrorLooks.Bare(vm.Gender, vm.HairStyle, vm.HairColor, vm.BodyColor, vm.FaceSprite);

    /// <summary>What a caption says an item costs: "current" for the player's own, "free" when it costs nothing, else the price.</summary>
    protected static string CostText(bool isCurrent, int cost)
        => isCurrent ? "current"
            : cost == 0 ? "free"
            : cost.ToString("N0");

    /// <summary>1-based position of the matching entry in <paramref name="items" />, or "-" when none matches.</summary>
    protected static string Position<T>(IReadOnlyList<T> items, Func<T, bool> isCurrent)
    {
        for (var i = 0; i < items.Count; i++)
            if (isCurrent(items[i]))
                return (i + 1).ToString();

        return "-";
    }

    protected static string FaceName(ViewModel.BeautyShop vm, int sprite)
        => vm.Faces.FirstOrDefault(f => f.Sprite == sprite)?.Name ?? $"Face {sprite}";
}
```

`HorizontalAlignment` is in `Chaos.Client.Definitions`; `AislingAppearance` is in `Chaos.Client.Rendering`.

- [ ] **Step 2: Create `Pages/GenderPage.cs`:**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>Page 1: male or female, with what a change costs and what it does to gear.</summary>
public sealed class GenderPage : MirrorPageView
{
    private const int SCALE = 2;
    private const int BUTTON_GAP = 52;
    private const int SELECTOR_TOP = 10;
    private const int NOTE_TOP = 146;
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;

    private static readonly string[] NOTE =
    [
        "It also reshapes your Master and Grandmaster",
        "gear, and may change your hairstyle and face",
        "to ones made for the new body.",
        "",
        "Keep your current gender to skip this page."
    ];

    private readonly GenderSelector Selector;
    private readonly UILabel MaleCurrent;
    private readonly UILabel FemaleCurrent;
    private readonly UILabel PriceLine;

    public GenderPage(MirrorActions actions, int width, int height)
        : base(actions, width, height)
    {
        Selector = new GenderSelector(SCALE, BUTTON_GAP);
        Selector.X = (width - Selector.Width) / 2;
        Selector.Y = SELECTOR_TOP;
        Selector.GenderChosen += g => Actions.Select(v => v.SetGender(g));
        AddChild(Selector);

        var buttonSize = GenderSelector.BUTTON_SIZE * SCALE;
        var maleLeft = Selector.X;
        var femaleLeft = Selector.X + buttonSize + BUTTON_GAP;
        var nameTop = SELECTOR_TOP + buttonSize + 6;

        AddLabel(maleLeft, nameTop, buttonSize, alignment: HorizontalAlignment.Center).Text = "Male";
        AddLabel(femaleLeft, nameTop, buttonSize, alignment: HorizontalAlignment.Center).Text = "Female";

        MaleCurrent = AddLabel(maleLeft, nameTop + LINE_HEIGHT, buttonSize, LegendColors.Gray, HorizontalAlignment.Center);
        MaleCurrent.Text = "(current)";
        FemaleCurrent = AddLabel(femaleLeft, nameTop + LINE_HEIGHT, buttonSize, LegendColors.Gray, HorizontalAlignment.Center);
        FemaleCurrent.Text = "(current)";

        PriceLine = AddLabel(0, NOTE_TOP, width, LegendColors.Gray);

        for (var i = 0; i < NOTE.Length; i++)
            AddLabel(0, NOTE_TOP + ((i + 1) * LINE_HEIGHT), width, LegendColors.Gray).Text = NOTE[i];
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;

        Selector.SetSelected(vm.Gender);
        MaleCurrent.Visible = vm.CurrentGender == Gender.Male;
        FemaleCurrent.Visible = vm.CurrentGender == Gender.Female;
        PriceLine.Text = $"Changing gender costs {vm.GenderPrice:N0} gold.";
    }
}
```

- [ ] **Step 3: Create `Pages/HairPage.cs`.** `SwatchColorFor` moves here unchanged from `BeautyShopControl`:

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Data;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>Page 2: a hairstyle grid (16 heads a page) above every hair color.</summary>
public sealed class HairPage : MirrorPageView
{
    private const int STYLE_COLUMNS = 8;
    private const int STYLE_ROWS = ViewModel.BeautyShop.HAIRSTYLE_PAGE_SIZE / STYLE_COLUMNS;
    private const int GRID_TOP = TextRenderer.CHAR_HEIGHT + 2;
    private const int SECTION_GAP = 8;

    private readonly UILabel StyleCaption;
    private readonly UILabel StylePageLabel;
    private readonly ThumbnailGrid<BeautyShopHairstyleEntry> Styles;
    private readonly UILabel ColorCaption;
    private readonly SwatchGrid Colors;

    public HairPage(MirrorActions actions, int width, int height)
        : base(actions, width, height)
    {
        StyleCaption = AddLabel(0, 0, width);
        StylePageLabel = AddLabel(0, 0, width, LegendColors.Gray, HorizontalAlignment.Right);

        Styles = new ThumbnailGrid<BeautyShopHairstyleEntry>(STYLE_COLUMNS, STYLE_ROWS) { X = 0, Y = GRID_TOP };
        Styles.Hovered += h => Actions.Hover(v => v.SetHoverHairStyle(h.Sprite));
        Styles.HoverCleared += () => Actions.Hover(v => v.ClearHover());
        Styles.Selected += h => Actions.Select(v => v.SelectHairStyle(h.Sprite));
        Styles.PageStepped += d => Actions.Select(v => v.StepHairstylePage(d));
        AddChild(Styles);

        var colorTop = Styles.Y + Styles.Height + SECTION_GAP;
        ColorCaption = AddLabel(0, colorTop, width);

        Colors = new SwatchGrid { X = Styles.CellLeft, Y = colorTop + TextRenderer.CHAR_HEIGHT + 2 };
        Colors.Hovered += c => Actions.Hover(v => v.SetHoverHairColor(c));
        Colors.HoverCleared += () => Actions.Hover(v => v.ClearHover());
        Colors.Selected += c => Actions.Select(v => v.SelectHairColor(c));
        AddChild(Colors);
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;
        var baseLook = BaseLook(vm);

        var style = vm.HoveredHairStyle ?? vm.HairStyle;
        var stylePosition = Position(vm.Hairstyles, h => h.Sprite == style);
        StyleCaption.Text = $"Hairstyle   {stylePosition} / {vm.Hairstyles.Count} - {CostText(style == vm.CurrentHairStyle, vm.CostOfHairStyle(style))}";
        StylePageLabel.Text = $"PAGE {vm.HairstylePage + 1}/{vm.HairstylePageCount}";
        Styles.SetItems(
            vm.VisibleHairstyles,
            vm.Hairstyles.FirstOrDefault(h => h.Sprite == vm.HairStyle),
            vm.HairstylePageCount,
            h => Actions.Thumbnails.Get(baseLook with { HeadSprite = h.Sprite }));

        var color = vm.HoveredHairColor ?? vm.HairColor;
        ColorCaption.Text = $"Hair color   {color} - {CostText(color == vm.CurrentHairColor, vm.CostOfHairColor(color))}";
        Colors.SetColors(vm.HairColors, vm.HairColor, SwatchColorFor);
    }

    private static Color SwatchColorFor(DisplayColor color)
    {
        if (color == DisplayColor.Default)
            return LegendColors.DeepLavender;

        var table = DataContext.AislingDrawData.DyeColorTable;

        if (!table.Contains((int)color))
            return LegendColors.Gray;

        var colors = table[(int)color].Colors;
        var mid = colors[Math.Min(2, colors.Length - 1)];

        return new Color(mid.Red, mid.Green, mid.Blue);
    }
}
```

- [ ] **Step 4: Create `Pages/SkinPage.cs`:**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>Page 3: every skin color at once, each head with its name under it.</summary>
public sealed class SkinPage : MirrorPageView
{
    private const int COLUMNS = 5;
    private const int ROWS = ViewModel.BeautyShop.BODY_COLOR_PAGE_SIZE / COLUMNS;

    //a 60 px column pitch leaves room for the longest name ("LightBlue") under a 32 px head
    private const int COLUMN_GAP = 28;
    private const int ROW_GAP = TextRenderer.CHAR_HEIGHT + 4;
    private const int GRID_TOP = TextRenderer.CHAR_HEIGHT + 4;
    private const int NAME_WIDTH = ThumbnailGrid<BodyColor>.CELL + COLUMN_GAP;

    private readonly UILabel Caption;
    private readonly ThumbnailGrid<BodyColor> Skins;
    private readonly UILabel[] Names = new UILabel[COLUMNS * ROWS];

    public SkinPage(MirrorActions actions, int width, int height)
        : base(actions, width, height)
    {
        Caption = AddLabel(0, 0, width);

        Skins = new ThumbnailGrid<BodyColor>(COLUMNS, ROWS, COLUMN_GAP, ROW_GAP)
        {
            X = (width - ThumbnailGrid<BodyColor>.WidthFor(COLUMNS, COLUMN_GAP)) / 2,
            Y = GRID_TOP
        };
        Skins.Hovered += c => Actions.Hover(v => v.SetHoverBodyColor(c));
        Skins.HoverCleared += () => Actions.Hover(v => v.ClearHover());
        Skins.Selected += c => Actions.Select(v => v.SelectBodyColor(c));
        Skins.PageStepped += d => Actions.Select(v => v.StepBodyColorPage(d));
        AddChild(Skins);

        for (var i = 0; i < Names.Length; i++)
        {
            var origin = Skins.CellOrigin(i);

            Names[i] = AddLabel(
                Skins.X + origin.X - (COLUMN_GAP / 2),
                Skins.Y + origin.Y + ThumbnailGrid<BodyColor>.CELL + 2,
                NAME_WIDTH,
                LegendColors.LightGray,
                HorizontalAlignment.Center);
        }
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;
        var baseLook = BaseLook(vm);

        var skin = vm.HoveredBodyColor ?? vm.BodyColor;
        Caption.Text = $"Skin   {skin} - {CostText(skin == vm.CurrentBodyColor, vm.CostOfBodyColor(skin))}";

        var visible = vm.VisibleBodyColors;
        Skins.SetItems(visible, vm.BodyColor, vm.BodyColorPageCount, c => Actions.Thumbnails.Get(baseLook with { BodyColor = (int)c }));

        for (var i = 0; i < Names.Length; i++)
        {
            Names[i].Visible = i < visible.Count;
            Names[i].Text = i < visible.Count ? visible[i].ToString() : string.Empty;
        }
    }
}
```

- [ ] **Step 5: Create `Pages/FacePage.cs`:**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>Page 4: every face the selected gender may wear, on one page while they fit.</summary>
public sealed class FacePage : MirrorPageView
{
    private const int COLUMNS = 8;
    private const int ROWS = ViewModel.BeautyShop.FACE_PAGE_SIZE / COLUMNS;
    private const int GRID_TOP = TextRenderer.CHAR_HEIGHT + 4;

    private readonly UILabel Caption;
    private readonly ThumbnailGrid<BeautyShopFaceEntry> Faces;

    public FacePage(MirrorActions actions, int width, int height)
        : base(actions, width, height)
    {
        Caption = AddLabel(0, 0, width);

        Faces = new ThumbnailGrid<BeautyShopFaceEntry>(COLUMNS, ROWS) { X = 0, Y = GRID_TOP };
        Faces.Hovered += f => Actions.Hover(v => v.SetHoverFace(f.Sprite));
        Faces.HoverCleared += () => Actions.Hover(v => v.ClearHover());
        Faces.Selected += f => Actions.Select(v => v.SelectFace(f.Sprite));
        Faces.PageStepped += d => Actions.Select(v => v.StepFacePage(d));
        AddChild(Faces);
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;
        var baseLook = BaseLook(vm);

        var face = vm.HoveredFaceSprite ?? vm.FaceSprite;
        Caption.Text = $"Face   {FaceName(vm, face)} - {CostText(face == vm.CurrentFaceSprite, vm.CostOfFace(face))}";

        Faces.SetItems(
            vm.VisibleFaces,
            vm.Faces.FirstOrDefault(f => f.Sprite == vm.FaceSprite),
            vm.FacePageCount,
            f => Actions.Thumbnails.Get(baseLook with { FaceSprite = f.Sprite }));
    }
}
```

- [ ] **Step 6: Build.** Conventions **Build** → `Build succeeded`, 0 errors. Fix any missing `using` by copying it from `BeautyShopControl.cs`.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/Pages/MirrorPageView.cs", "Chaos.Client/Controls/World/Popups/Beauty/Pages/GenderPage.cs", "Chaos.Client/Controls/World/Popups/Beauty/Pages/HairPage.cs", "Chaos.Client/Controls/World/Popups/Beauty/Pages/SkinPage.cs", "Chaos.Client/Controls/World/Popups/Beauty/Pages/FacePage.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-server", "acceptanceCriteria": ["MirrorActions record and MirrorPageView base with Refresh, AddLabel, BaseLook, CostText, Position, FaceName; pages start hidden", "Gender page: 2x selector, names, (current), price line, four note lines", "Hair page: hairstyle caption + PAGE label, 8x2 grid, color caption, swatches aligned to first cell column", "Skin page: 5x2 grid at 60px pitch with names under cells", "Face page: 8x5 grid, arrows hidden on one page", "hovers via Actions.Hover, selects/page steps via Actions.Select; captions use hovered value", "client builds with 0 errors"], "modelTier": "standard"}
```

---

### Task 6: Review page

**Goal:** Page 5 shows NOW and NEW figures side by side with shared rotate and Show gear controls, a Start over button, a receipt of every category, the total, the player's gold and the server's refusal line.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/Beauty/Pages/ReviewPage.cs`

**Acceptance Criteria:**
- [ ] `ReviewPage(actions, renderer, rotate, toggleGear, width, height)` with `SetView(facingIndex, showGear)`, `Refresh()`, `Release()` and a settable `Status`.
- [ ] Heading `REVIEW` (gold) + `page 5 of 5` (gray) centred; NOW (gray) and NEW (gold) captions over two 260 × 160 `MirrorPreview`s with a gold `>` between.
- [ ] Rotate `<` `>` call `rotate(-1)` / `rotate(+1)`; the gear checkbox calls `toggleGear` and shows `showGear`; Start over calls `Actions.Select(v => v.Reset())` and is enabled only with unsaved changes.
- [ ] Receipt rows in order Gender, Hairstyle, Hair color, Skin, Face: changed → `old >` (gray), new (green), price (`free` when 0); unchanged → `no change` (gray). Hairstyle rows show 1-based positions in each gender's own list. Then `TOTAL` (gold; red when unaffordable) and `You have` (gray).
- [ ] `Status` shows in red, centred, under the receipt.
- [ ] The client builds with 0 errors.

**Verify:** Conventions **Build** → `Build succeeded`, 0 errors

**Steps:**

- [ ] **Step 1: Create `Pages/ReviewPage.cs`:**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>
///     Page 5: how the player looks now and how they will look, side by side, over a receipt of every change. The
///     shell hides its own preview column on this page; Apply is the shell's footer button.
/// </summary>
public sealed class ReviewPage : MirrorPageView
{
    private const int FIGURE_WIDTH = 260;
    private const int FIGURE_HEIGHT = 160;
    private const int CAPTION_TOP = TextRenderer.CHAR_HEIGHT + 4;
    private const int FIGURE_TOP = CAPTION_TOP + TextRenderer.CHAR_HEIGHT + 2;
    private const int CONTROLS_TOP = FIGURE_TOP + FIGURE_HEIGHT + 6;
    private const int RECEIPT_TOP = CONTROLS_TOP + CustomButton.HEIGHT + 8;
    private const int ROW_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int ROTATE_BUTTON_WIDTH = 28;
    private const int START_OVER_WIDTH = 110;

    //receipt columns, centred on the 560 px page
    private const int NAME_LEFT = 100;
    private const int NAME_WIDTH = 66;
    private const int OLD_LEFT = 170;
    private const int OLD_WIDTH = 110;
    private const int NEW_LEFT = 286;
    private const int NEW_WIDTH = 108;
    private const int PRICE_RIGHT = 460;
    private const int PRICE_WIDTH = 124;

    private static readonly string[] CATEGORIES = ["Gender", "Hairstyle", "Hair color", "Skin", "Face"];

    private readonly MirrorPreview NowFigure;
    private readonly MirrorPreview NewFigure;
    private readonly CustomCheckBox GearToggle;
    private readonly CustomButton StartOver;
    private readonly UILabel[] OldLabels = new UILabel[CATEGORIES.Length];
    private readonly UILabel[] NewLabels = new UILabel[CATEGORIES.Length];
    private readonly UILabel[] PriceLabels = new UILabel[CATEGORIES.Length];
    private readonly UILabel TotalValue;
    private readonly UILabel GoldValue;
    private readonly UILabel StatusLabel;

    private int FacingIndex;
    private bool ShowGear;

    /// <summary>The server's refusal of the last Apply, in red under the receipt. Empty shows nothing.</summary>
    public string Status { get; set; } = string.Empty;

    public ReviewPage(MirrorActions actions, AislingRenderer renderer, Action<int> rotate, Action toggleGear, int width, int height)
        : base(actions, width, height)
    {
        var title = MirrorPages.Title(MirrorPage.Review);
        var pageText = $"page {MirrorPages.COUNT} of {MirrorPages.COUNT}";
        var headingLeft = (width - ((title.Length + 3 + pageText.Length) * TextRenderer.CHAR_WIDTH)) / 2;
        AddLabel(headingLeft, 0, title.Length * TextRenderer.CHAR_WIDTH, LegendColors.Gold).Text = title;
        AddLabel(headingLeft + ((title.Length + 3) * TextRenderer.CHAR_WIDTH), 0, pageText.Length * TextRenderer.CHAR_WIDTH, LegendColors.Gray).Text = pageText;

        var newLeft = width - FIGURE_WIDTH;
        AddLabel(0, CAPTION_TOP, FIGURE_WIDTH, LegendColors.Gray, HorizontalAlignment.Center).Text = "NOW";
        AddLabel(newLeft, CAPTION_TOP, FIGURE_WIDTH, LegendColors.Gold, HorizontalAlignment.Center).Text = "NEW";
        AddLabel(FIGURE_WIDTH, FIGURE_TOP + ((FIGURE_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2), newLeft - FIGURE_WIDTH, LegendColors.Gold, HorizontalAlignment.Center)
            .Text = ">";

        NowFigure = new MirrorPreview(renderer, FIGURE_WIDTH, FIGURE_HEIGHT) { X = 0, Y = FIGURE_TOP };
        AddChild(NowFigure);
        NewFigure = new MirrorPreview(renderer, FIGURE_WIDTH, FIGURE_HEIGHT) { X = newLeft, Y = FIGURE_TOP };
        AddChild(NewFigure);

        var rotateLeft = new CustomButton("<", ROTATE_BUTTON_WIDTH) { X = 0, Y = CONTROLS_TOP };
        rotateLeft.Clicked += () => rotate(-1);
        AddChild(rotateLeft);

        var rotateRight = new CustomButton(">", ROTATE_BUTTON_WIDTH) { X = ROTATE_BUTTON_WIDTH + 4, Y = CONTROLS_TOP };
        rotateRight.Clicked += () => rotate(+1);
        AddChild(rotateRight);

        GearToggle = MirrorPreview.CreateGearToggle(newLeft, CONTROLS_TOP + ((CustomButton.HEIGHT - CustomCheckBox.CHECKBOX_SIZE) / 2));
        GearToggle.Clicked += () => toggleGear();
        AddChild(GearToggle);

        StartOver = new CustomButton("Start over", START_OVER_WIDTH) { X = width - START_OVER_WIDTH, Y = CONTROLS_TOP };
        StartOver.Clicked += () => Actions.Select(v => v.Reset());
        AddChild(StartOver);

        for (var i = 0; i < CATEGORIES.Length; i++)
        {
            var y = RECEIPT_TOP + (i * ROW_HEIGHT);
            AddLabel(NAME_LEFT, y, NAME_WIDTH).Text = CATEGORIES[i];
            OldLabels[i] = AddLabel(OLD_LEFT, y, OLD_WIDTH, LegendColors.Gray, HorizontalAlignment.Right);
            NewLabels[i] = AddLabel(NEW_LEFT, y, NEW_WIDTH, LegendColors.SpringGreen);
            PriceLabels[i] = AddLabel(PRICE_RIGHT - PRICE_WIDTH, y, PRICE_WIDTH, alignment: HorizontalAlignment.Right);
        }

        var totalTop = RECEIPT_TOP + (CATEGORIES.Length * ROW_HEIGHT) + 6;
        AddLabel(NAME_LEFT, totalTop, NAME_WIDTH, LegendColors.Gold).Text = "TOTAL";
        TotalValue = AddLabel(PRICE_RIGHT - PRICE_WIDTH, totalTop, PRICE_WIDTH, LegendColors.Gold, HorizontalAlignment.Right);
        AddLabel(NAME_LEFT, totalTop + ROW_HEIGHT, NAME_WIDTH, LegendColors.Gray).Text = "You have";
        GoldValue = AddLabel(PRICE_RIGHT - PRICE_WIDTH, totalTop + ROW_HEIGHT, PRICE_WIDTH, LegendColors.Gray, HorizontalAlignment.Right);
        StatusLabel = AddLabel(0, totalTop + (3 * ROW_HEIGHT), width, LegendColors.Red, HorizontalAlignment.Center);
    }

    /// <summary>The shell's facing and Show gear state, which both figures follow. Call before <see cref="Refresh" />.</summary>
    public void SetView(int facingIndex, bool showGear)
    {
        FacingIndex = facingIndex;
        ShowGear = showGear;
        GearToggle.Checked = showGear;
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;

        NowFigure.Refresh(MirrorLooks.Now(vm, ShowGear), FacingIndex);
        NewFigure.Refresh(MirrorLooks.New(vm, ShowGear), FacingIndex);

        SetRow(0, vm.GenderChanged, vm.CurrentGender.ToString(), vm.Gender.ToString(), vm.GenderPrice);
        SetRow(
            1,
            vm.HairstyleChanged,
            Position(vm.HairstylesFor(vm.CurrentGender), h => h.Sprite == vm.CurrentHairStyle),
            Position(vm.Hairstyles, h => h.Sprite == vm.HairStyle),
            vm.HairstylePrice);
        SetRow(2, vm.HairColorChanged, vm.CurrentHairColor.ToString(), vm.HairColor.ToString(), vm.HairDyePrice);
        SetRow(3, vm.BodyColorChanged, vm.CurrentBodyColor.ToString(), vm.BodyColor.ToString(), vm.BodyDyePrice);
        SetRow(4, vm.FaceChanged, FaceName(vm, vm.CurrentFaceSprite), FaceName(vm, vm.FaceSprite), vm.FaceCharged ? vm.FacePrice : 0);

        TotalValue.Text = vm.Total.ToString("N0");
        TotalValue.ForegroundColor = vm.CanAfford ? LegendColors.Gold : LegendColors.Red;
        GoldValue.Text = vm.Gold.ToString("N0");
        StartOver.Enabled = vm.HasUnsavedChanges;
        StatusLabel.Text = Status;
    }

    /// <summary>Drops both figures' textures; the shell calls this when the window hides.</summary>
    public void Release()
    {
        NowFigure.Release();
        NewFigure.Release();
    }

    private void SetRow(int row, bool changed, string from, string to, int price)
    {
        OldLabels[row].Text = changed ? $"{from} >" : string.Empty;
        NewLabels[row].Text = changed ? to : "no change";
        NewLabels[row].ForegroundColor = changed ? LegendColors.SpringGreen : LegendColors.Gray;
        PriceLabels[row].Text = !changed ? string.Empty
            : price == 0 ? "free"
            : price.ToString("N0");
    }
}
```

- [ ] **Step 2: Build.** Conventions **Build** → `Build succeeded`, 0 errors.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/Pages/ReviewPage.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-server", "acceptanceCriteria": ["ReviewPage(actions, renderer, rotate, toggleGear, width, height) with SetView, Refresh, Release, Status", "centred REVIEW heading; NOW/NEW captions over two 260x160 MirrorPreviews with gold >", "rotate/gear callbacks; Start over -> Select(Reset), enabled only with unsaved changes", "receipt rows Gender..Face: old > / new / price or free; no change when unchanged; hairstyle positions per gender list; TOTAL gold/red and You have", "Status in red under the receipt", "client builds with 0 errors"], "modelTier": "standard"}
```

---

### Task 7: The shell — rewrite BeautyShopControl as the five-page guide

**Goal:** `BeautyShopControl` shows one page at a time with the preview column, heading, arrows, dots and total, opens on Gender, applies from Review, and keeps today's public surface so `WorldScreen` is untouched.

**Files:**
- Modify (rewrite): `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs`
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/HeadThumbnailRenderer.cs` (add `Count`)
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/GenderSelector.cs` (delete `HEIGHT`)
- Delete: `Chaos.Client/Controls/World/Popups/Beauty/ThumbnailStrip.cs`

**Acceptance Criteria:**
- [ ] Public surface unchanged: constructor `(AislingRenderer)`, `Show()`, `Hide()`, `Refresh()`, `OnRejected(BeautyShopRejectReason)`, events `Closed` and `ApplyRequested`, Escape closes.
- [ ] `Show()` resets facing, gear, zoom and status and opens on Gender.
- [ ] Pages 1–4: preview column visible; heading `TITLE   page n of 5` and the instruction line; footer back button hidden on Gender, next button labelled by `MirrorPages.NextLabel`, dots at the current page, total (`TOTAL n` gold/red or `No changes yet` gray).
- [ ] Review: preview column, heading and footer total hidden; right button `APPLY`, enabled == `CanApply`; gender change still shows the reshape confirm.
- [ ] Turning a page (arrow or dot) clears hover and hides a standing confirm; it never changes a selection. No Left/Right key handling.
- [ ] `Select` no longer clears thumbnails on every change; it clears them only when `Thumbnails.Count > 512`. `Hide()` releases the preview, both review figures and every thumbnail.
- [ ] `ThumbnailStrip.cs` and `GenderSelector.HEIGHT` are gone; nothing else references `PreviewView`, `BareAppearance`, `HoverTotal`, `UnsavedLabel` or `DiscardButton`.
- [ ] Client builds with 0 errors; all client tests pass.

**Verify:** Conventions **Build** → `Build succeeded`, 0 errors; Conventions **Tests** (no filter) → 0 failed

**Steps:**

- [ ] **Step 1: Add a count to the thumbnail cache.** In `HeadThumbnailRenderer.cs`, after the `Cache` field, add:

```csharp
    /// <summary>How many head crops are cached. The mirror drops them all when this grows large.</summary>
    public int Count => Cache.Count;
```

- [ ] **Step 2: Replace the whole content of `BeautyShopControl.cs` with:**

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.Beauty.Pages;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     Josephine's mirror: a five-page guide (Gender, Hair, Skin, Face, Review). Pages 1-4 show the preview column on
///     the left and the page on the right; Review spans both. A footer of arrows that name the next page, page dots
///     and the running total sits at the bottom. Reads everything from <see cref="WorldState.BeautyShop" />; nothing
///     here costs gold until the server answers an Apply.
/// </summary>
public sealed class BeautyShopControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 600;
    private const int PANEL_HEIGHT = 470;
    private const int TOP_MARGIN = 5;
    private const int HEADER_TOP = 10;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    //preview column -- the pedestal must fit a 111px-wide composite at 2x (222px)
    private const int PREVIEW_LEFT = 20;
    private const int PREVIEW_WIDTH = 230;
    private const int PEDESTAL_HEIGHT = 250;
    private const int PEDESTAL_TOP = HEADER_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int PREVIEW_CONTROLS_TOP = PEDESTAL_TOP + PEDESTAL_HEIGHT + 6;
    private const int ROTATE_BUTTON_WIDTH = 28;
    private const int ZOOM_BUTTON_WIDTH = 60;
    private const int RANDOMIZE_BUTTON_WIDTH = 100;

    //page column
    private const int PAGE_LEFT = PREVIEW_LEFT + PREVIEW_WIDTH + 12;
    private const int PAGE_WIDTH = PANEL_WIDTH - PAGE_LEFT - 20;
    private const int INSTRUCTION_TOP = HEADER_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int PAGE_TOP = INSTRUCTION_TOP + TextRenderer.CHAR_HEIGHT + 8;

    //footer -- kept clear of the frame's ornate bottom border
    private const int FOOTER_TOP = PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int TOTAL_TOP = FOOTER_TOP - TextRenderer.CHAR_HEIGHT - 4;
    private const int PAGE_HEIGHT = TOTAL_TOP - 4 - PAGE_TOP;
    private const int NAV_BUTTON_WIDTH = 130;

    //the review page spans both columns
    private const int REVIEW_LEFT = PREVIEW_LEFT;
    private const int REVIEW_TOP = HEADER_TOP;
    private const int REVIEW_WIDTH = PANEL_WIDTH - (PREVIEW_LEFT * 2);
    private const int REVIEW_HEIGHT = TOTAL_TOP - REVIEW_TOP;

    //head crops are cached by the full look, so a selection never makes one wrong; they are only dropped when the
    //cache grows this large (each is a 30x30 texture)
    private const int THUMBNAIL_CACHE_LIMIT = 512;

    private readonly HeadThumbnailRenderer Thumbnails;
    private readonly UIPanel PreviewColumn;
    private readonly MirrorPreview Preview;
    private readonly CustomCheckBox GearToggle;
    private readonly CustomButton ZoomButton;
    private readonly UILabel TitleLabel;
    private readonly UILabel PageNumberLabel;
    private readonly UILabel InstructionLabel;
    private readonly MirrorPageView[] Pages;
    private readonly ReviewPage Review;
    private readonly CustomButton BackButton;
    private readonly CustomButton NextButton;
    private readonly PageDots Dots;
    private readonly UILabel TotalLabel;
    private readonly OkPopupMessageControl ConfirmDialog;

    private MirrorPage CurrentPage = MirrorPages.First;
    private int FacingIndex;
    private bool ShowGear;
    private bool Zoomed = true;

    /// <summary>The player closed the panel (button or Escape). Fires once per hide.</summary>
    public event Action? Closed;

    /// <summary>The player pressed Apply (and confirmed, when a gender change was involved).</summary>
    public event Action? ApplyRequested;

    public BeautyShopControl(AislingRenderer renderer)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        Name = "BeautyShop";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        Thumbnails = new HeadThumbnailRenderer(renderer);

        OkButton = CreateCloseButton(RequestDismissal, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        //── preview column (hidden on Review, which draws its own two figures) ──
        PreviewColumn = new UIPanel
        {
            Background = null,
            X = 0,
            Y = 0,
            Width = PREVIEW_LEFT + PREVIEW_WIDTH,
            Height = TOTAL_TOP
        };
        AddChild(PreviewColumn);

        var previewCaption = NewLabel(PREVIEW_LEFT, HEADER_TOP, PREVIEW_WIDTH, LegendColors.Gray, HorizontalAlignment.Center);
        previewCaption.Text = "PREVIEW";
        PreviewColumn.AddChild(previewCaption);

        Preview = new MirrorPreview(renderer, PREVIEW_WIDTH, PEDESTAL_HEIGHT) { X = PREVIEW_LEFT, Y = PEDESTAL_TOP };
        PreviewColumn.AddChild(Preview);

        var rotateLeft = new CustomButton("<", ROTATE_BUTTON_WIDTH) { X = PREVIEW_LEFT, Y = PREVIEW_CONTROLS_TOP };
        rotateLeft.Clicked += () => Rotate(-1);
        PreviewColumn.AddChild(rotateLeft);

        var rotateRight = new CustomButton(">", ROTATE_BUTTON_WIDTH)
        {
            X = PREVIEW_LEFT + ROTATE_BUTTON_WIDTH + 4,
            Y = PREVIEW_CONTROLS_TOP
        };
        rotateRight.Clicked += () => Rotate(+1);
        PreviewColumn.AddChild(rotateRight);

        ZoomButton = new CustomButton("2x", ZOOM_BUTTON_WIDTH)
        {
            X = PREVIEW_LEFT + PREVIEW_WIDTH - ZOOM_BUTTON_WIDTH,
            Y = PREVIEW_CONTROLS_TOP
        };
        ZoomButton.Clicked += () =>
        {
            Zoomed = !Zoomed;
            Preview.Zoomed = Zoomed;
            ZoomButton.Caption = Zoomed ? "2x" : "1x";
        };
        PreviewColumn.AddChild(ZoomButton);

        GearToggle = MirrorPreview.CreateGearToggle(PREVIEW_LEFT, PREVIEW_CONTROLS_TOP + CustomButton.HEIGHT + 6);
        GearToggle.Clicked += ToggleGear;
        PreviewColumn.AddChild(GearToggle);

        var randomizeButton = new CustomButton("Randomize", RANDOMIZE_BUTTON_WIDTH)
        {
            X = PREVIEW_LEFT + PREVIEW_WIDTH - RANDOMIZE_BUTTON_WIDTH,
            Y = GearToggle.Y
        };
        randomizeButton.Clicked += () => Select(v => v.Randomize());
        PreviewColumn.AddChild(randomizeButton);

        //── page heading (pages 1-4; Review draws its own) ──
        TitleLabel = NewLabel(PAGE_LEFT, HEADER_TOP, PAGE_WIDTH, LegendColors.Gold);
        AddChild(TitleLabel);
        PageNumberLabel = NewLabel(PAGE_LEFT, HEADER_TOP, PAGE_WIDTH, LegendColors.Gray);
        AddChild(PageNumberLabel);
        InstructionLabel = NewLabel(PAGE_LEFT, INSTRUCTION_TOP, PAGE_WIDTH, LegendColors.Gray);
        AddChild(InstructionLabel);

        //── pages, in MirrorPage order ──
        var actions = new MirrorActions(Select, Hover, Thumbnails);
        Review = new ReviewPage(actions, renderer, Rotate, ToggleGear, REVIEW_WIDTH, REVIEW_HEIGHT) { X = REVIEW_LEFT, Y = REVIEW_TOP };

        Pages =
        [
            new GenderPage(actions, PAGE_WIDTH, PAGE_HEIGHT) { X = PAGE_LEFT, Y = PAGE_TOP },
            new HairPage(actions, PAGE_WIDTH, PAGE_HEIGHT) { X = PAGE_LEFT, Y = PAGE_TOP },
            new SkinPage(actions, PAGE_WIDTH, PAGE_HEIGHT) { X = PAGE_LEFT, Y = PAGE_TOP },
            new FacePage(actions, PAGE_WIDTH, PAGE_HEIGHT) { X = PAGE_LEFT, Y = PAGE_TOP },
            Review
        ];

        foreach (var page in Pages)
            AddChild(page);

        //── footer ──
        BackButton = new CustomButton(string.Empty, NAV_BUTTON_WIDTH) { X = PREVIEW_LEFT, Y = FOOTER_TOP };
        BackButton.Clicked += GoBack;
        AddChild(BackButton);

        Dots = new PageDots(MirrorPages.COUNT);
        Dots.X = (PANEL_WIDTH - Dots.Width) / 2;
        Dots.Y = FOOTER_TOP + ((CustomButton.HEIGHT - Dots.Height) / 2);
        Dots.PageChosen += index => GoTo((MirrorPage)index);
        AddChild(Dots);

        TotalLabel = NewLabel(
            PREVIEW_LEFT + NAV_BUTTON_WIDTH,
            TOTAL_TOP,
            PANEL_WIDTH - (2 * (PREVIEW_LEFT + NAV_BUTTON_WIDTH)),
            LegendColors.Gray,
            HorizontalAlignment.Center);
        AddChild(TotalLabel);

        NextButton = new CustomButton(string.Empty, NAV_BUTTON_WIDTH)
        {
            X = PANEL_WIDTH - PREVIEW_LEFT - NAV_BUTTON_WIDTH,
            Y = FOOTER_TOP
        };
        NextButton.Clicked += GoForward;
        AddChild(NextButton);

        //parented to the panel like poker's leave confirm, drawn above everything else in it
        ConfirmDialog = new OkPopupMessageControl(true) { Name = "BeautyShopGenderConfirm", ZIndex = 100 };
        ConfirmDialog.X = (PANEL_WIDTH - ConfirmDialog.Width) / 2;
        ConfirmDialog.Y = (PANEL_HEIGHT - ConfirmDialog.Height) / 2;

        ConfirmDialog.OnOk += () =>
        {
            ConfirmDialog.Hide();

            //re-validate: a picker change or Start over while the prompt was up must not sneak an apply through
            if (WorldState.BeautyShop.CanApply && WorldState.BeautyShop.GenderChanged)
                ApplyRequested?.Invoke();
        };

        ConfirmDialog.OnCancel += () => ConfirmDialog.Hide();
        AddChild(ConfirmDialog);
    }

    private static UILabel NewLabel(int x, int y, int width, Color color, HorizontalAlignment alignment = HorizontalAlignment.Left)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color,
            IsHitTestVisible = false
        };

    /// <summary>Repaints the preview, the heading, the visible page and the footer from the view model.</summary>
    public void Refresh()
    {
        RefreshPreview();
        RefreshHeading();
        Review.SetView(FacingIndex, ShowGear);
        Pages[(int)CurrentPage].Refresh();
        RefreshFooter();
    }

    public override void Show()
    {
        FacingIndex = 0;
        ShowGear = false;
        GearToggle.Checked = false;
        Zoomed = true;
        Preview.Zoomed = true;
        ZoomButton.Caption = "2x";
        Review.Status = string.Empty;
        base.Show();
        GoTo(MirrorPages.First);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        //a gender-reshape prompt still standing when the panel goes away -- the player's own X/Escape while it is
        //up, or the server closing the session out from under it -- must not be left visible or on the input
        //stack; see PokerTableControl.Hide for the same reasoning.
        ConfirmDialog.Hide();
        base.Hide();
        Preview.Release();
        Review.Release();
        Thumbnails.Clear();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            RequestDismissal();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>The player's own close. Nothing is at stake before Apply, so no confirmation.</summary>
    private void RequestDismissal()
    {
        if (!Visible)
            return;

        Hide();
        Closed?.Invoke();
    }

    /// <summary>Shows <paramref name="page" />. Turning a page clears hover and never changes a selection.</summary>
    private void GoTo(MirrorPage page)
    {
        if (!Visible)
            return;

        if (ConfirmDialog.Visible)
            ConfirmDialog.Hide();

        WorldState.BeautyShop.ClearHover();
        CurrentPage = page;

        for (var i = 0; i < Pages.Length; i++)
            Pages[i].Visible = i == (int)page;

        PreviewColumn.Visible = page != MirrorPage.Review;
        Refresh();
    }

    private void GoBack()
    {
        if (MirrorPages.Previous(CurrentPage) is { } previous)
            GoTo(previous);
    }

    private void GoForward()
    {
        if (MirrorPages.Next(CurrentPage) is { } next)
            GoTo(next);
        else
            OnApplyClicked();
    }

    private void Rotate(int delta)
    {
        FacingIndex = (FacingIndex + delta + MirrorPreview.FACING_COUNT) % MirrorPreview.FACING_COUNT;
        Refresh();
    }

    private void ToggleGear()
    {
        ShowGear = !ShowGear;
        GearToggle.Checked = ShowGear;
        Refresh();
    }

    private void RefreshPreview()
    {
        //Review draws its own two figures; don't render one nobody can see
        if (CurrentPage != MirrorPage.Review)
            Preview.Refresh(MirrorLooks.New(WorldState.BeautyShop, ShowGear), FacingIndex);
    }

    private void RefreshHeading()
    {
        var visible = CurrentPage != MirrorPage.Review;
        TitleLabel.Visible = visible;
        PageNumberLabel.Visible = visible;
        InstructionLabel.Visible = visible;

        var title = MirrorPages.Title(CurrentPage);
        TitleLabel.Text = title;
        PageNumberLabel.X = PAGE_LEFT + ((title.Length + 3) * TextRenderer.CHAR_WIDTH);
        PageNumberLabel.Text = $"page {(int)CurrentPage + 1} of {MirrorPages.COUNT}";
        InstructionLabel.Text = MirrorPages.Instruction(CurrentPage);
    }

    private void RefreshFooter()
    {
        var vm = WorldState.BeautyShop;
        var onReview = CurrentPage == MirrorPage.Review;

        BackButton.Visible = MirrorPages.Previous(CurrentPage) is not null;
        BackButton.Caption = MirrorPages.BackLabel(CurrentPage);
        NextButton.Caption = MirrorPages.NextLabel(CurrentPage);
        NextButton.Enabled = !onReview || vm.CanApply;
        Dots.SetCurrent((int)CurrentPage);

        TotalLabel.Visible = !onReview;
        TotalLabel.Text = vm.HasUnsavedChanges ? $"TOTAL {vm.Total:N0}" : "No changes yet";
        TotalLabel.ForegroundColor = !vm.HasUnsavedChanges ? LegendColors.Gray
            : vm.CanAfford ? LegendColors.Gold
            : LegendColors.Red;
    }

    /// <summary>Every selection and page step goes through here: tear down a stale confirm, mutate, repaint.</summary>
    private void Select(Action<ViewModel.BeautyShop> mutate)
    {
        //InputDispatcher doesn't forget a hovered element across Hide(); ignore any late callback from it
        if (!Visible)
            return;

        //any change while the gender-reshape prompt is up invalidates what it was about to confirm
        if (ConfirmDialog.Visible)
            ConfirmDialog.Hide();

        mutate(WorldState.BeautyShop);
        Review.Status = string.Empty;

        if (Thumbnails.Count > THUMBNAIL_CACHE_LIMIT)
            Thumbnails.Clear();

        Refresh();
    }

    /// <summary>Hover never touches the confirm dialog, the status line, the thumbnail cache or the money -- only the preview and the page's captions.</summary>
    private void Hover(Action<ViewModel.BeautyShop> mutate)
    {
        //InputDispatcher doesn't forget a hovered element across Hide(); ignore any late callback from it
        if (!Visible)
            return;

        mutate(WorldState.BeautyShop);
        RefreshPreview();
        Pages[(int)CurrentPage].Refresh();
    }

    private void OnApplyClicked()
    {
        var vm = WorldState.BeautyShop;

        if (!vm.CanApply)
            return;

        if (vm.GenderChanged)
        {
            ConfirmDialog.Show(
                $"This will reshape your Master and Grandmaster gear - equipped, banked and in inventory - for {vm.GenderPrice:N0} gold. Continue?");

            return;
        }

        ApplyRequested?.Invoke();
    }

    /// <summary>Server refused the Apply; keep the panel open and say why on the review page.</summary>
    public void OnRejected(BeautyShopRejectReason reason)
    {
        if (!Visible)
            return;

        Review.Status = reason switch
        {
            BeautyShopRejectReason.NothingChanged        => "Nothing has changed.",
            BeautyShopRejectReason.InsufficientGold      => "You can't afford that.",
            BeautyShopRejectReason.InvalidSelection      => "Josephine can't do that one.",
            BeautyShopRejectReason.GenderSwapUnavailable => "Josephine can't reshape your class's gear.",
            BeautyShopRejectReason.NotNearShop           => "Step closer to Josephine.",
            _                                            => "Josephine shakes her head."
        };

        Refresh();
    }

    public override void Dispose()
    {
        Thumbnails.Dispose();
        base.Dispose();
    }
}
```

Notes for this step:
- `Randomize` sits at the right end of the Show gear row, as in the approved mockup (v2 put it on its own row below).
- `KeyDownEvent`, `Keycode` and `HorizontalAlignment` come from `Chaos.Client.Definitions`; `BeautyShopRejectReason` from `Chaos.DarkAges.Definitions`.

- [ ] **Step 3: Remove what is now unused.**
  - Delete `Chaos.Client/Controls/World/Popups/Beauty/ThumbnailStrip.cs`: `git rm Chaos.Client/Controls/World/Popups/Beauty/ThumbnailStrip.cs`.
  - In `GenderSelector.cs`, delete the line `public const int HEIGHT = BUTTON_SIZE;` (Serena `safe_delete_symbol` on `GenderSelector/HEIGHT` confirms nothing references it).

- [ ] **Step 4: Check nothing stale is left.**

Run (from the client worktree): `grep -rn "ThumbnailStrip\|PreviewView\|BareAppearance\|HoverTotal\|UnsavedLabel\|DiscardButton\|GenderSelector.HEIGHT" Chaos.Client Tests --include=*.cs`
Expected: no output.

- [ ] **Step 5: Build and run every client test.**

Run: Conventions **Build**, then Conventions **Tests** with no filter.
Expected: `Build succeeded`, 0 errors; test summary with 0 failed (BeautyShopViewModelTests 19 and MirrorPagesTests 4 among them).

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs", "Chaos.Client/Controls/World/Popups/Beauty/HeadThumbnailRenderer.cs", "Chaos.Client/Controls/World/Popups/Beauty/GenderSelector.cs", "Chaos.Client/Controls/World/Popups/Beauty/ThumbnailStrip.cs"], "verifyCommand": "dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["public surface unchanged (ctor, Show, Hide, Refresh, OnRejected, Closed, ApplyRequested, Escape closes)", "Show resets facing/gear/zoom/status and opens on Gender", "pages 1-4: preview column, heading + instruction, back hidden on Gender, NextLabel, dots, total", "Review: column/heading/total hidden, APPLY enabled == CanApply, gender confirm kept", "page turns clear hover and hide confirm; no Left/Right keys", "Select clears thumbnails only past 512; Hide releases preview, review figures, thumbnails", "ThumbnailStrip.cs and GenderSelector.HEIGHT removed; no stale references", "client builds 0 errors; all client tests pass"], "modelTier": "standard"}
```

---

### Task 8: Walkthrough rows, CLAUDE.md line, final checks

**Goal:** The manual walkthrough lists the v3 checks for the user's in-game pass, `CLAUDE.md` mentions the mirror's folder, and the full build and test run are green.

**Files:**
- Modify: `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md`
- Modify: `CLAUDE.md`

**Acceptance Criteria:**
- [ ] The walkthrough has a `## v3 guide (2026-10-04)` section with rows G1–G12 below, each `PENDING (user)`.
- [ ] `CLAUDE.md`'s Popups subdirectory list has a `Beauty/` entry describing the five-page mirror.
- [ ] Client build: 0 errors; client tests: 0 failed.

**Verify:** Conventions **Build** and **Tests** → `Build succeeded`; 0 failed

**Steps:**

- [ ] **Step 1: Append to `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md`:**

```markdown

## v3 guide (2026-10-04)

Spec: `docs/superpowers/specs/2026-10-04-beauty-shop-guide-design.md`. In-game checks for the user; build the client from `feat/mirror-guide`.

| # | Check | Expected | Result | Notes |
|---|-------|----------|--------|-------|
| G1 | Talk to Josephine, open the mirror | Opens on GENDER, page 1 of 5; no left arrow; right arrow reads "Hair >"; first dot gold; "No changes yet" | PENDING (user) | |
| G2 | Hover and click Female on page 1 | Hover shows the lit art at half strength; click selects it; preview reshapes; total shows TOTAL 50,000 (gender price) | PENDING (user) | |
| G3 | Press "Hair >" | HAIR page: 16 heads, PAGE n/N, color swatches; left arrow "< Gender" | PENDING (user) | |
| G4 | Hover a hairstyle, then a color | Preview changes; caption shows position and cost ("current", "free" or price); total unchanged until click | PENDING (user) | |
| G5 | Page the hairstyle grid with its own < > | Heads change page without losing the selection; no hitch | PENDING (user) | |
| G6 | Skin page | All 10 skins with names; no grid arrows | PENDING (user) | |
| G7 | Face page | All faces on one page (34 for male, 35 for female); hovering shows name and price | PENDING (user) | |
| G8 | Click dot 2 from page 4, then dot 5 | Jumps straight to Hair, then Review; selections kept | PENDING (user) | |
| G9 | Review page | NOW and NEW side by side; rotate turns both; Show gear applies to both; receipt rows match the changes; TOTAL and You have | PENDING (user) | |
| G10 | Start over on Review | Everything back to the current look; "no change" on every row; APPLY disabled | PENDING (user) | |
| G11 | Apply with a gender change | Reshape confirm appears; OK applies, window closes, orange bar message | PENDING (user) | |
| G12 | Press Left/Right keys, then Escape, then reopen | Keys do not turn pages; Escape closes with no charge; reopening starts on GENDER | PENDING (user) | |
```

- [ ] **Step 2: Add to `CLAUDE.md`.** In the **Popups (`Popups/`)** paragraph, after the `` `Lockpicking/` (...) `` entry, insert:

```markdown
`Beauty/` (BeautyShopControl — Josephine's Mirror, a five-page guide: Gender, Hair, Skin, Face, Review; each page is a `Pages/` MirrorPageView, the order and arrow labels are `MirrorPages`; ThumbnailGrid/SwatchGrid/GenderSelector/PageDots are the pickers, MirrorPreview the pedestal figure; the state is `ViewModel/BeautyShop`), 
```

- [ ] **Step 3: Final build and test run.** Conventions **Build** then **Tests** (no filter). Expected: `Build succeeded`, 0 errors; 0 failed.

```json:metadata
{"files": ["docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md", "CLAUDE.md"], "verifyCommand": "dotnet run --no-build --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["walkthrough has v3 section with G1-G12 PENDING (user)", "CLAUDE.md Popups list has a Beauty/ entry", "client build 0 errors; client tests 0 failed"], "modelTier": "mechanical"}
```

---

### Task 9: Commit the full implementation

**Goal:** One commit on `feat/mirror-guide` holds the whole change set.

**Files:**
- All files changed in Tasks 1–8 (client worktree only).

**Acceptance Criteria:**
- [ ] `git status` in the client worktree shows nothing staged or unstaged from this plan after the commit; the `Chaos-Server` submodule pointer is not part of the commit.
- [ ] The commit message ends with the `Co-Authored-By` trailer.

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-client show --stat HEAD` → lists only the files below

**Steps:**

- [ ] **Step 1: Stage by explicit path** (from the client worktree):

```bash
git add -- Chaos.Client/ViewModel/BeautyShop.cs \
  Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs \
  Tests/Chaos.Client.Tests/MirrorPagesTests.cs \
  Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs \
  Chaos.Client/Controls/World/Popups/Beauty/GenderSelector.cs \
  Chaos.Client/Controls/World/Popups/Beauty/HeadThumbnailRenderer.cs \
  Chaos.Client/Controls/World/Popups/Beauty/MirrorLooks.cs \
  Chaos.Client/Controls/World/Popups/Beauty/MirrorPage.cs \
  Chaos.Client/Controls/World/Popups/Beauty/MirrorPreview.cs \
  Chaos.Client/Controls/World/Popups/Beauty/PageDots.cs \
  Chaos.Client/Controls/World/Popups/Beauty/SwatchGrid.cs \
  Chaos.Client/Controls/World/Popups/Beauty/ThumbnailGrid.cs \
  Chaos.Client/Controls/World/Popups/Beauty/Pages \
  CLAUDE.md
git add -f -- docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md
git status --short
```

`ThumbnailStrip.cs` was already staged as deleted by `git rm` in Task 7. Expected `git status --short`: only the paths above (plus `D` for `ThumbnailStrip.cs`), all staged; nothing for `Chaos-Server`.

- [ ] **Step 2: Commit:**

```bash
git commit -m "Josephine's Mirror v3: five-page guide (Gender, Hair, Skin, Face, Review)

The mirror becomes a guide: arrows that name the next page, clickable page
dots, 16 hairstyles a page, every skin and face on one page, captions that
show what the pointed-at item costs, and a NOW/NEW review page with the
receipt and APPLY. Client only; no server, data or CLIENT_VERSION change.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git show --stat HEAD
```

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/mirror-guide-client show --stat HEAD", "acceptanceCriteria": ["single commit on feat/mirror-guide with all plan files", "Chaos-Server submodule pointer not in the commit", "message ends with Co-Authored-By trailer"], "modelTier": "mechanical"}
```
