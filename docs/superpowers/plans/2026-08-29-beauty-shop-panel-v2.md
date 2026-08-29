# Josephine's Mirror v2 (Preview-first Panel) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the beauty-shop client panel around a large zoomable sprite preview with visual pickers (head-crop thumbnails, dye swatch grid), hover-to-preview, a segmented gender selector, a purchase summary, an unsaved-changes indicator, Discard Changes and Randomize — client only.

**Architecture:** The pure `BeautyShop` view model grows hover/effective state, paging and `Randomize()` (all unit-tested). The panel is rebuilt from three new reusable controls (`ThumbnailStrip`, `SwatchGrid`, `GenderSelector`) plus a rewritten `BeautyShopControl`; thumbnails are head crops of `AislingRenderer.Render` composites (poker's `PortraitView` crop), cached per visible cell. Protocol, server, catalog, checkout and data are untouched. Spec: `docs/superpowers/specs/2026-08-29-beauty-shop-panel-v2-design.md`.

**Tech Stack:** .NET 10 / C# 14, MonoGame DesktopGL, TUnit + FluentAssertions (`Tests/Chaos.Client.Tests`).

**User decisions (already made):**
- Redesign approved 2026-08-29 exactly as the spec's layout: 600×440, 2× default zoom with 1×/2× toggle, thumbnail strips of 6/6/5, 10×8 dye swatch grid, hover-to-preview, segmented gender, purchase summary band, "● Unsaved changes", Discard Changes, 🎲 Randomize (never gender).
- No walk-cycle animation; no hairstyle names.
- Rebuilding requires closing the running client (the server may keep running).

---

## Conventions

- Repo: `C:\Users\mikeb\Documents\GitHub\Chaos.Client`, work on branch `feature/beauty-shop-v2` off `main` (`4b53080`+). `docs/` is gitignored — `git add -f`. Never stage the `Chaos-Server` submodule pointer, `Chaos.Client/GlobalSettings.cs`, or the old poker plan files that show as modified.
- Client tests: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` (never `dotnet test`). Build: `dotnet build Chaos.Client/Chaos.Client.csproj` (does not rebuild the server exe, so the running server is fine; the **client exe must be closed** first — `tasklist | findstr /i Chaos.Client`).
- Commit trailers: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` / `Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef`; prefix `git -c core.commitGraph=false` if git complains.
- Existing APIs the plan relies on: `UIElement.OnMouseEnter/OnMouseLeave/OnClick` virtuals, `DrawTextureFitted(spriteBatch, texture, Rectangle dest, Color)`, `UIElement.DrawRect/DrawBorder(spriteBatch, Rectangle, Color)`, `PixelBufferScope` (`Chaos.Client.Rendering`), `DialogFrame.BuildRecessedTexture(SKColor, w, h)`, `TextRenderer.CHAR_WIDTH/CHAR_HEIGHT`, `LegendColors.*`, poker's `FindContentTop` alpha scan (`PokerTableControl.cs:2736`).

## File structure

- `Chaos.Client/ViewModel/BeautyShop.cs` — hover/effective values, `HoverTotal`, `HasUnsavedChanges`, paging, `Randomize`.
- `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs` — new tests.
- `Chaos.Client/Controls/World/Popups/Beauty/HeadThumbnailRenderer.cs` — renders + caches head-crop textures for (appearance) keys.
- `Chaos.Client/Controls/World/Popups/Beauty/ThumbnailStrip.cs` — paged strip of selectable/hoverable cells drawing thumbnails.
- `Chaos.Client/Controls/World/Popups/Beauty/SwatchGrid.cs` — 10×8 colour grid with the same cell states.
- `Chaos.Client/Controls/World/Popups/Beauty/GenderSelector.cs` — two-segment selector.
- `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs` — rewritten layout; keeps the v1 public surface (`Show/Hide/OnRejected/ApplyRequested/Closed`) so `WorldScreen` is untouched.
- `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md` — v2 rows.

---

### Task 1: View model — hover state, effective look, hover total, unsaved flag

**Goal:** The view model can hold at most one hovered item, expose the effective (hovered-or-selected) look for the preview, price the hovered look, and report unsaved changes.

**Files:**
- Modify: `Chaos.Client/ViewModel/BeautyShop.cs`
- Test: `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs`

**Acceptance Criteria:**
- [ ] `SetHoverHairStyle(int)`, `SetHoverHairColor(DisplayColor)`, `SetHoverBodyColor(BodyColor)`, `SetHoverFace(int)` each clear the other three; `ClearHover()` clears all; `IsHovering` reflects it.
- [ ] `EffectiveHairStyle/EffectiveHairColor/EffectiveBodyColor/EffectiveFaceSprite` = hovered ?? selected; `Total`, `*Changed`, `CanApply` are unaffected by hover.
- [ ] `HoverTotal` = the total the selection would have if the hovered item were chosen (hovering the already-selected item gives `Total`; no hover gives `Total`).
- [ ] `HasUnsavedChanges` = any of the five `*Changed`.
- [ ] `Reset()`, `Clear()`, `SetGender()` and every `Step*` clear hover.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all passed (8 existing + 5 new)

**Steps:**

- [ ] **Step 1: Write the failing tests** — append to `BeautyShopViewModelTests`:

```csharp
    [Test]
    public async Task Hover_overrides_the_effective_look_but_not_the_selection()
    {
        var vm = Opened();

        vm.SetHoverHairStyle(97);

        vm.IsHovering.Should().BeTrue();
        vm.EffectiveHairStyle.Should().Be(97);
        vm.HairStyle.Should().Be(1);
        vm.Total.Should().Be(0);
        vm.HasUnsavedChanges.Should().BeFalse();

        vm.SetHoverHairColor(DisplayColor.Apple);

        vm.EffectiveHairStyle.Should().Be(1);          // only one hover at a time
        vm.EffectiveHairColor.Should().Be(DisplayColor.Apple);

        vm.ClearHover();

        vm.IsHovering.Should().BeFalse();
        vm.EffectiveHairColor.Should().Be(DisplayColor.Default);

        await Task.CompletedTask;
    }

    [Test]
    public async Task HoverTotal_prices_the_hovered_item_as_if_chosen()
    {
        var vm = Opened();
        vm.StepHairColor(+1);                          // Apple: 1,000 selected

        vm.HoverTotal.Should().Be(1_000);              // nothing hovered → Total

        vm.SetHoverHairStyle(97);                      // +2,500 if chosen
        vm.HoverTotal.Should().Be(3_500);
        vm.Total.Should().Be(1_000);

        vm.SetHoverHairColor(DisplayColor.Default);    // hovering "back to current" would drop the dye charge
        vm.HoverTotal.Should().Be(0);

        vm.SetHoverFace(10);                           // Beauty: +50,000
        vm.HoverTotal.Should().Be(51_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Selection_changes_clear_hover()
    {
        var vm = Opened();
        vm.SetHoverFace(10);

        vm.StepHairstyle(+1);
        vm.IsHovering.Should().BeFalse();

        vm.SetHoverFace(10);
        vm.SetGender(Gender.Female);
        vm.IsHovering.Should().BeFalse();

        vm.SetHoverFace(10);
        vm.Reset();
        vm.IsHovering.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task HasUnsavedChanges_tracks_any_difference()
    {
        var vm = Opened();

        vm.HasUnsavedChanges.Should().BeFalse();
        vm.StepBodyColor(+1);
        vm.HasUnsavedChanges.Should().BeTrue();
        vm.Reset();
        vm.HasUnsavedChanges.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hovering_the_selected_item_changes_nothing()
    {
        var vm = Opened();
        vm.StepFace(+1);                               // Beauty selected, 50,000

        vm.SetHoverFace(10);

        vm.HoverTotal.Should().Be(50_000);
        vm.EffectiveFaceSprite.Should().Be(10);

        await Task.CompletedTask;
    }
```

- [ ] **Step 2: Run tests → the five new ones fail to compile.**

- [ ] **Step 3: Implement** — add to `BeautyShop`:

```csharp
    //hover: at most one category at a time; the preview shows hovered ?? selected, money never does
    public int? HoveredHairStyle { get; private set; }
    public DisplayColor? HoveredHairColor { get; private set; }
    public BodyColor? HoveredBodyColor { get; private set; }
    public int? HoveredFaceSprite { get; private set; }

    public bool IsHovering => HoveredHairStyle.HasValue || HoveredHairColor.HasValue || HoveredBodyColor.HasValue || HoveredFaceSprite.HasValue;

    public int EffectiveHairStyle => HoveredHairStyle ?? HairStyle;
    public DisplayColor EffectiveHairColor => HoveredHairColor ?? HairColor;
    public BodyColor EffectiveBodyColor => HoveredBodyColor ?? BodyColor;
    public int EffectiveFaceSprite => HoveredFaceSprite ?? FaceSprite;

    public bool HasUnsavedChanges => GenderChanged || HairstyleChanged || HairColorChanged || BodyColorChanged || FaceChanged;

    /// <summary>The total if the hovered item were chosen; <see cref="Total" /> when nothing is hovered.</summary>
    public int HoverTotal
        => PriceOf(Gender, EffectiveHairStyle, EffectiveHairColor, EffectiveBodyColor, EffectiveFaceSprite);

    public void SetHoverHairStyle(int sprite) { ClearHover(); HoveredHairStyle = sprite; }
    public void SetHoverHairColor(DisplayColor color) { ClearHover(); HoveredHairColor = color; }
    public void SetHoverBodyColor(BodyColor color) { ClearHover(); HoveredBodyColor = color; }
    public void SetHoverFace(int sprite) { ClearHover(); HoveredFaceSprite = sprite; }

    public void ClearHover()
    {
        HoveredHairStyle = null;
        HoveredHairColor = null;
        HoveredBodyColor = null;
        HoveredFaceSprite = null;
    }

    /// <summary>Prices an arbitrary look against the current one with the same rules as <see cref="Total" />.</summary>
    private int PriceOf(Gender gender, int hairStyle, DisplayColor hairColor, BodyColor bodyColor, int faceSprite)
    {
        var genderChanged = gender != CurrentGender;
        var faceChanged = faceSprite != CurrentFaceSprite;

        var faceCharged = faceChanged
                          && !(genderChanged && !IsFaceAvailable(gender, CurrentFaceSprite) && (Faces.Count > 0) && (faceSprite == Faces[0].Sprite));

        var hairstylePrice = (gender == Gender.Male ? MaleHairstyles : FemaleHairstyles).FirstOrDefault(h => h.Sprite == hairStyle)?.Price ?? 0;
        var facePrice = Faces.FirstOrDefault(f => f.Sprite == faceSprite)?.Price ?? 0;

        return (genderChanged ? GenderPrice : 0)
               + (hairStyle != CurrentHairStyle ? hairstylePrice : 0)
               + (hairColor != CurrentHairColor ? HairDyePrice : 0)
               + (bodyColor != CurrentBodyColor ? BodyDyePrice : 0)
               + (faceCharged ? facePrice : 0);
    }
```

Then make `Total` delegate: `public int Total => PriceOf(Gender, HairStyle, HairColor, BodyColor, FaceSprite);` (keep `FaceCharged` public — the control still uses it), and call `ClearHover()` at the top of `Reset()`, `Clear()`, `SetGender()` (before the early return is fine either way — put it first), `StepHairstyle`, `StepFace`, `StepHairColor`, `StepBodyColor`.

- [ ] **Step 4: Run tests → 13 passed.**

- [ ] **Step 5: Commit** — `git add Chaos.Client/ViewModel/BeautyShop.cs Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs && git commit -m "BeautyShop v2: hover/effective look, hover total, unsaved flag"`

```json:metadata
{"files": ["Chaos.Client/ViewModel/BeautyShop.cs", "Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["one hover at a time", "effective = hovered ?? selected; money ignores hover", "HoverTotal prices hovered look", "HasUnsavedChanges", "selection changes clear hover"], "modelTier": "standard"}
```

---

### Task 2: View model — paging and Randomize

**Goal:** The strips can page through hairstyles/skins/faces and jump to the selection; Randomize picks a valid random look without touching gender.

**Files:**
- Modify: `Chaos.Client/ViewModel/BeautyShop.cs`
- Test: `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs`

**Acceptance Criteria:**
- [ ] `const int HAIRSTYLE_PAGE_SIZE = 6`, `FACE_PAGE_SIZE = 6`, `BODY_COLOR_PAGE_SIZE = 5`; `HairstylePage`, `FacePage`, `BodyColorPage` (0-based); `HairstylePageCount` etc. = ceil(count / size), min 1.
- [ ] `VisibleHairstyles/VisibleFaces/VisibleBodyColors` return the current page's slice; `StepHairstylePage(±1)` etc. wrap.
- [ ] Selecting via `SelectHairStyle(int)`, `SelectHairColor(DisplayColor)`, `SelectBodyColor(BodyColor)`, `SelectFace(int)` (new — the click path; ignore values not in the current gender's lists), `Step*`, `SetGender`, `Reset`, `ApplyOpen` and `Randomize` move each page to contain the selection.
- [ ] `Randomize(Random rng)` picks from `Hairstyles`, `HairColors`, `BodyColors`, `AvailableFaces` (uniform), leaves `Gender`, clears hover; a parameterless overload uses `Random.Shared`.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all passed (13 + 4 new)

**Steps:**

- [ ] **Step 1: Failing tests** — append (the test fixture's male list is {0, 1, 97}, faces {1, 10, 18}, hair colours {Default, Apple, Scarlet}, body colours {Brown, Tan, White}; add a bigger fixture for paging):

```csharp
    private static BeautyShop OpenedWithManyHairstyles()
    {
        var args = Open();
        args.MaleHairstyles = Enumerable.Range(0, 14).Select(i => new BeautyShopHairstyleEntry { Sprite = (ushort)i, Price = 1_000 }).ToList();
        var vm = new BeautyShop();
        vm.ApplyOpen(args);

        return vm;
    }

    [Test]
    public async Task Pages_slice_the_list_and_wrap()
    {
        var vm = OpenedWithManyHairstyles();            // 14 styles → 3 pages of 6

        vm.HairstylePageCount.Should().Be(3);
        vm.HairstylePage.Should().Be(0);                // selection (1) is on page 0
        vm.VisibleHairstyles.Select(h => (int)h.Sprite).Should().Equal(0, 1, 2, 3, 4, 5);

        vm.StepHairstylePage(+1);
        vm.VisibleHairstyles.Select(h => (int)h.Sprite).Should().Equal(6, 7, 8, 9, 10, 11);
        vm.StepHairstylePage(+1);
        vm.VisibleHairstyles.Select(h => (int)h.Sprite).Should().Equal(12, 13);
        vm.StepHairstylePage(+1);
        vm.HairstylePage.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Selecting_moves_the_page_to_the_selection()
    {
        var vm = OpenedWithManyHairstyles();

        vm.SelectHairStyle(13);
        vm.HairStyle.Should().Be(13);
        vm.HairstylePage.Should().Be(2);

        vm.StepHairstyle(+1);                           // wraps to 0
        vm.HairstylePage.Should().Be(0);

        vm.SelectHairStyle(999);                        // not in the list: ignored
        vm.HairStyle.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Randomize_picks_valid_values_and_keeps_gender()
    {
        var vm = Opened();
        var rng = new Random(1234);

        for (var i = 0; i < 50; i++)
        {
            vm.Randomize(rng);

            vm.Gender.Should().Be(Gender.Male);
            vm.Hairstyles.Select(h => (int)h.Sprite).Should().Contain(vm.HairStyle);
            vm.HairColors.Should().Contain(vm.HairColor);
            vm.BodyColors.Should().Contain(vm.BodyColor);
            vm.AvailableFaces.Select(f => (int)f.Sprite).Should().Contain(vm.FaceSprite);
            vm.FaceSprite.Should().NotBe(18);           // female-only never appears for a male
            vm.IsHovering.Should().BeFalse();
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Randomize_is_deterministic_for_a_seed()
    {
        var a = Opened();
        var b = Opened();

        a.Randomize(new Random(7));
        b.Randomize(new Random(7));

        (a.HairStyle, a.HairColor, a.BodyColor, a.FaceSprite).Should().Be((b.HairStyle, b.HairColor, b.BodyColor, b.FaceSprite));

        await Task.CompletedTask;
    }
```

- [ ] **Step 2: Implement** — add to `BeautyShop`:

```csharp
    public const int HAIRSTYLE_PAGE_SIZE = 6;
    public const int FACE_PAGE_SIZE = 6;
    public const int BODY_COLOR_PAGE_SIZE = 5;

    public int HairstylePage { get; private set; }
    public int FacePage { get; private set; }
    public int BodyColorPage { get; private set; }

    public int HairstylePageCount => PageCount(Hairstyles.Count, HAIRSTYLE_PAGE_SIZE);
    public int FacePageCount => PageCount(AvailableFaces.Count, FACE_PAGE_SIZE);
    public int BodyColorPageCount => PageCount(BodyColors.Count, BODY_COLOR_PAGE_SIZE);

    public IReadOnlyList<BeautyShopHairstyleEntry> VisibleHairstyles => Page(Hairstyles, HairstylePage, HAIRSTYLE_PAGE_SIZE);
    public IReadOnlyList<BeautyShopFaceEntry> VisibleFaces => Page(AvailableFaces, FacePage, FACE_PAGE_SIZE);
    public IReadOnlyList<BodyColor> VisibleBodyColors => Page(BodyColors, BodyColorPage, BODY_COLOR_PAGE_SIZE);

    public void StepHairstylePage(int delta) => HairstylePage = WrapIndex(HairstylePage, delta, HairstylePageCount);
    public void StepFacePage(int delta) => FacePage = WrapIndex(FacePage, delta, FacePageCount);
    public void StepBodyColorPage(int delta) => BodyColorPage = WrapIndex(BodyColorPage, delta, BodyColorPageCount);

    public void SelectHairStyle(int sprite)
    {
        if (Hairstyles.All(h => h.Sprite != sprite))
            return;

        ClearHover();
        HairStyle = sprite;
        SyncPages();
    }

    public void SelectHairColor(DisplayColor color)
    {
        if (!HairColors.Contains(color))
            return;

        ClearHover();
        HairColor = color;
    }

    public void SelectBodyColor(BodyColor color)
    {
        if (!BodyColors.Contains(color))
            return;

        ClearHover();
        BodyColor = color;
        SyncPages();
    }

    public void SelectFace(int sprite)
    {
        if (AvailableFaces.All(f => f.Sprite != sprite))
            return;

        ClearHover();
        FaceSprite = sprite;
        SyncPages();
    }

    public void Randomize() => Randomize(Random.Shared);

    /// <summary>A random valid hairstyle, dye, skin and face for the current gender. Gender is deliberately never randomized -- it is the expensive, gear-reshaping change.</summary>
    public void Randomize(Random rng)
    {
        ClearHover();

        if (Hairstyles.Count > 0)
            HairStyle = Hairstyles[rng.Next(Hairstyles.Count)].Sprite;

        if (HairColors.Count > 0)
            HairColor = HairColors[rng.Next(HairColors.Count)];

        if (BodyColors.Count > 0)
            BodyColor = BodyColors[rng.Next(BodyColors.Count)];

        var faces = AvailableFaces;

        if (faces.Count > 0)
            FaceSprite = faces[rng.Next(faces.Count)].Sprite;

        SyncPages();
    }

    /// <summary>Moves every page to the one holding its selection.</summary>
    private void SyncPages()
    {
        HairstylePage = PageOf(IndexOfCurrent(Hairstyles, h => h.Sprite == HairStyle), HAIRSTYLE_PAGE_SIZE);
        FacePage = PageOf(IndexOfCurrent(AvailableFaces, f => f.Sprite == FaceSprite), FACE_PAGE_SIZE);
        BodyColorPage = PageOf(IndexOfCurrent(BodyColors, c => c == BodyColor), BODY_COLOR_PAGE_SIZE);
    }

    private static int PageOf(int index, int pageSize) => index < 0 ? 0 : index / pageSize;

    private static int PageCount(int count, int pageSize) => Math.Max(1, (count + pageSize - 1) / pageSize);

    private static IReadOnlyList<T> Page<T>(IReadOnlyList<T> list, int page, int pageSize)
        => list.Skip(page * pageSize).Take(pageSize).ToList();
```

Call `SyncPages()` at the end of `Reset()` (so `ApplyOpen` gets it too), `SetGender()`, and each `Step*` (the colour step too). `Clear()` sets the three pages to 0.

- [ ] **Step 3: Run tests → 17 passed. Commit** — `git commit -m "BeautyShop v2: paging, click-select and Randomize in the view model"`

```json:metadata
{"files": ["Chaos.Client/ViewModel/BeautyShop.cs", "Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["page sizes 6/6/5, wrap", "Select* ignores invalid, moves page", "steps/gender/reset sync pages", "Randomize valid + gender kept + seeded"], "modelTier": "standard"}
```

---

### Task 3: Picker controls — HeadThumbnailRenderer, ThumbnailStrip, SwatchGrid, GenderSelector

**Goal:** Four self-contained controls the panel composes: a head-crop thumbnail renderer with a keyed cache, a paged strip of hoverable/selectable cells, a colour swatch grid, and a two-segment gender selector. Built and compiled, not yet used by the panel.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/Beauty/HeadThumbnailRenderer.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/ThumbnailStrip.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/SwatchGrid.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/GenderSelector.cs`

**Acceptance Criteria:**
- [ ] `HeadThumbnailRenderer.Get(in AislingAppearance)` returns a cached 30×30 head-crop texture per appearance (value-keyed), rendering front idle (frame 5, flipped) and cropping from `FindContentTop` like poker's `PortraitView`; `Clear()` disposes every texture; `Dispose` clears.
- [ ] `ThumbnailStrip<T>`: N cells of `CELL = 36`px with 4px gaps, ◀ ▶ page buttons and an `n / N` label; `SetItems(items, selectedIndexInItems, pageLabel, thumbFor)` repaints; cell hover raises `Hovered(T)`, leaving raises `HoverCleared`, click raises `Selected(T)`; page buttons raise `PageStepped(±1)`; selected cell draws a 2px gold border and a ✓ badge; hovered cell a 1px silver border.
- [ ] `SwatchGrid`: `SetColors(IReadOnlyList<DisplayColor>, DisplayColor selected, Func<DisplayColor, Color> swatchColor)` lays out 10 columns of 14px squares with 3px gaps; same hover/select events and borders as the strip.
- [ ] `GenderSelector`: two segments MALE | FEMALE; `Selected` gender drawn gold-filled with dark text, other dim; clicking the unselected segment raises `GenderChosen(Gender)`.
- [ ] `dotnet build Chaos.Client/Chaos.Client.csproj` succeeds with no new warnings.

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj` → Build succeeded

**Steps:**

- [ ] **Step 1: HeadThumbnailRenderer**

```csharp
#region
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     Renders head-and-shoulders crops of an appearance for the picker strips and caches them by value. Each
///     crop is a fresh composite from <see cref="AislingRenderer.Render" /> (which allocates per call and caches
///     only per world entity), so this class owns every texture it hands out; <see cref="Clear" /> is called
///     whenever the base look changes and on hide.
/// </summary>
public sealed class HeadThumbnailRenderer(AislingRenderer renderer) : IDisposable
{
    public const int CROP_SIZE = 30;
    private const int FRONT_IDLE_FRAME = 5;

    private readonly Dictionary<AislingAppearance, Texture2D> Cache = [];

    public Texture2D? Get(in AislingAppearance appearance)
    {
        if (Cache.TryGetValue(appearance, out var cached))
            return cached;

        using var figure = renderer.Render(in appearance, FRONT_IDLE_FRAME, AislingRenderer.IDLE_ANIM, true, true);

        if (figure is null)
            return null;

        var top = Math.Clamp(FindContentTop(figure), 0, Math.Max(0, figure.Height - 1));
        var left = Math.Max(0, AislingRenderer.CANVAS_CENTER_X - (CROP_SIZE / 2));
        var width = Math.Min(CROP_SIZE, figure.Width - left);
        var height = Math.Min(CROP_SIZE, figure.Height - top);

        if ((width <= 0) || (height <= 0))
            return null;

        //copy the crop out into its own texture so the full composite can be disposed right away
        var pixels = new Color[width * height];
        figure.GetData(0, new Rectangle(left, top, width, height), pixels, 0, pixels.Length);
        var crop = new Texture2D(figure.GraphicsDevice, width, height);
        crop.SetData(pixels);

        Cache[appearance] = crop;

        return crop;
    }

    /// <summary>First row with a visible pixel -- the top of the hair. Same threshold as PokerTableControl.PortraitView.</summary>
    private static int FindContentTop(Texture2D texture)
    {
        using var scope = new PixelBufferScope(texture);
        var pixels = scope.AsSpan();

        for (var y = 0; y < scope.Height; y++)
        {
            var row = y * scope.Width;

            for (var x = 0; x < scope.Width; x++)
                if (pixels[row + x].A > 16)
                    return y;
        }

        return 0;
    }

    public void Clear()
    {
        foreach (var texture in Cache.Values)
            texture.Dispose();

        Cache.Clear();
    }

    public void Dispose() => Clear();
}
```

(`PixelBufferScope`'s namespace: grep `class PixelBufferScope` under `Chaos.Client.Rendering` and adjust the using.)

- [ ] **Step 2: ThumbnailStrip**

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
///     A single page of picker cells with page arrows and an "n / N" label. Holds no state of its own beyond the
///     items it was last given: the panel calls <see cref="SetItems" /> on every refresh and reacts to the events.
/// </summary>
public sealed class ThumbnailStrip<T> : UIPanel where T : notnull
{
    public const int CELL = 36;
    public const int GAP = 4;
    private const int ARROW_WIDTH = 22;
    private const int LABEL_WIDTH = 60;

    private readonly Cell[] Cells;
    private readonly CustomButton Left;
    private readonly CustomButton Right;
    private readonly UILabel PageLabel;

    public event Action<T>? Hovered;
    public event Action? HoverCleared;
    public event Action<T>? Selected;
    public event Action<int>? PageStepped;

    public static int WidthFor(int cellCount) => ARROW_WIDTH + GAP + (cellCount * (CELL + GAP)) + ARROW_WIDTH + GAP + LABEL_WIDTH;

    public ThumbnailStrip(int cellCount)
    {
        Background = null;
        Width = WidthFor(cellCount);
        Height = CELL;

        Left = new CustomButton("<", ARROW_WIDTH) { X = 0, Y = (CELL - CustomButton.HEIGHT) / 2 };
        Left.Clicked += () => PageStepped?.Invoke(-1);
        AddChild(Left);

        Cells = new Cell[cellCount];

        for (var i = 0; i < cellCount; i++)
        {
            var cell = new Cell { X = ARROW_WIDTH + GAP + (i * (CELL + GAP)), Y = 0, Width = CELL, Height = CELL, Visible = false };
            cell.Hovered += item => Hovered?.Invoke((T)item);
            cell.HoverCleared += () => HoverCleared?.Invoke();
            cell.Clicked += item => Selected?.Invoke((T)item);
            Cells[i] = cell;
            AddChild(cell);
        }

        Right = new CustomButton(">", ARROW_WIDTH) { X = ARROW_WIDTH + GAP + (cellCount * (CELL + GAP)), Y = Left.Y };
        Right.Clicked += () => PageStepped?.Invoke(+1);
        AddChild(Right);

        PageLabel = new UILabel
        {
            X = Right.X + ARROW_WIDTH + GAP,
            Y = (CELL - TextRenderer.CHAR_HEIGHT) / 2,
            Width = LABEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };
        AddChild(PageLabel);
    }

    /// <summary>
    ///     Shows one page. <paramref name="selected" /> is compared with <see cref="object.Equals(object)" />;
    ///     <paramref name="thumbFor" /> may return null (cell draws its border only).
    /// </summary>
    public void SetItems(IReadOnlyList<T> items, T? selected, string pageLabel, Func<T, Texture2D?> thumbFor)
    {
        for (var i = 0; i < Cells.Length; i++)
        {
            var cell = Cells[i];

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
        }

        PageLabel.Text = pageLabel;
    }

    /// <summary>One thumbnail slot. Draws its texture fitted, then the state border and the ✓ badge.</summary>
    private sealed class Cell : UIElement
    {
        private static readonly Color HoverBorder = LegendColors.Silver;
        private static readonly Color SelectedBorder = LegendColors.Gold;
        private static readonly Color Slot = new(18, 16, 22);

        public object? Item;
        public Texture2D? Thumbnail;
        public bool IsSelected;
        private bool IsHovered;

        public event Action<object>? Hovered;
        public event Action? HoverCleared;
        public event Action<object>? Clicked;

        public override void OnMouseEnter()
        {
            IsHovered = true;

            if (Item is not null)
                Hovered?.Invoke(Item);
        }

        public override void OnMouseLeave()
        {
            IsHovered = false;
            HoverCleared?.Invoke();
        }

        public override void OnClick(ClickEvent e)
        {
            if (Item is not null)
                Clicked?.Invoke(Item);

            e.Handled = true;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
            DrawRect(spriteBatch, bounds, Slot);

            if (Thumbnail is not null)
                DrawTextureFitted(spriteBatch, Thumbnail, new Rectangle(bounds.X + 2, bounds.Y + 2, Width - 4, Height - 4), Color.White);

            if (IsSelected)
            {
                DrawBorder(spriteBatch, bounds, SelectedBorder);
                DrawBorder(spriteBatch, new Rectangle(bounds.X + 1, bounds.Y + 1, Width - 2, Height - 2), SelectedBorder);
                DrawRect(spriteBatch, new Rectangle(bounds.Right - 11, bounds.Y + 1, 10, 10), SelectedBorder);
                TextRenderer.Instance?.Draw(spriteBatch, "v", new Vector2(bounds.Right - 9, bounds.Y + 0), LegendColors.AlmostBlack);
            } else if (IsHovered)
                DrawBorder(spriteBatch, bounds, HoverBorder);
        }
    }
}
```

`TextRenderer.Instance?.Draw(...)`: use whatever `UILabel` uses to draw a string (grep `TextRenderer` in `UILabel.cs`) — a small "v" in the badge is enough for a checkmark; if there is no static text-draw helper, make the badge a solid gold square with a 4×4 dark square inside (still reads as "selected"). `LegendColors.Silver` and `AlmostBlack` exist.

- [ ] **Step 3: SwatchGrid**

```csharp
public sealed class SwatchGrid : UIPanel
{
    public const int SWATCH = 14;
    public const int GAP = 3;
    public const int COLUMNS = 10;

    private readonly List<Swatch> Swatches = [];

    public event Action<DisplayColor>? Hovered;
    public event Action? HoverCleared;
    public event Action<DisplayColor>? Selected;

    public static int WidthFor(int columns) => (columns * (SWATCH + GAP)) - GAP;
    public static int HeightFor(int count) => (((count + COLUMNS - 1) / COLUMNS) * (SWATCH + GAP)) - GAP;

    public SwatchGrid() => Background = null;

    public void SetColors(IReadOnlyList<DisplayColor> colors, DisplayColor selected, Func<DisplayColor, Color> swatchColor)
    {
        //rebuild only when the colour list changes (it doesn't after Open); otherwise just restyle
        if (Swatches.Count != colors.Count)
        {
            foreach (var old in Swatches) { Children.Remove(old); old.Dispose(); }
            Swatches.Clear();

            for (var i = 0; i < colors.Count; i++)
            {
                var swatch = new Swatch
                {
                    X = (i % COLUMNS) * (SWATCH + GAP),
                    Y = (i / COLUMNS) * (SWATCH + GAP),
                    Width = SWATCH,
                    Height = SWATCH
                };
                swatch.Hovered += c => Hovered?.Invoke(c);
                swatch.HoverCleared += () => HoverCleared?.Invoke();
                swatch.Clicked += c => Selected?.Invoke(c);
                Swatches.Add(swatch);
                AddChild(swatch);
            }

            Width = WidthFor(COLUMNS);
            Height = HeightFor(colors.Count);
        }

        for (var i = 0; i < colors.Count; i++)
        {
            Swatches[i].Color = colors[i];
            Swatches[i].Fill = swatchColor(colors[i]);
            Swatches[i].IsSelected = colors[i] == selected;
        }
    }

    private sealed class Swatch : UIElement
    {
        public DisplayColor Color;
        public Microsoft.Xna.Framework.Color Fill;
        public bool IsSelected;
        private bool IsHovered;

        public event Action<DisplayColor>? Hovered;
        public event Action? HoverCleared;
        public event Action<DisplayColor>? Clicked;

        public override void OnMouseEnter() { IsHovered = true; Hovered?.Invoke(Color); }
        public override void OnMouseLeave() { IsHovered = false; HoverCleared?.Invoke(); }
        public override void OnClick(ClickEvent e) { Clicked?.Invoke(Color); e.Handled = true; }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;

            var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
            DrawRect(spriteBatch, bounds, Fill);

            if (IsSelected)
            {
                DrawBorder(spriteBatch, bounds, LegendColors.Gold);
                DrawBorder(spriteBatch, new Rectangle(bounds.X + 1, bounds.Y + 1, Width - 2, Height - 2), LegendColors.Gold);
            } else if (IsHovered)
                DrawBorder(spriteBatch, bounds, LegendColors.Silver);
            else
                DrawBorder(spriteBatch, bounds, LegendColors.AlmostBlack);
        }
    }
}
```

(Same usings as the strip plus `Chaos.DarkAges.Definitions`; if `Children` is not directly mutable from a subclass, rebuild the whole grid once in `SetColors` on first call and treat a later count change as a no-op — the list never changes after Open.)

- [ ] **Step 4: GenderSelector**

```csharp
public sealed class GenderSelector : UIPanel
{
    public const int SEGMENT_WIDTH = 72;
    public const int HEIGHT = TextRenderer.CHAR_HEIGHT + 8;

    private Gender SelectedGender = Gender.Male;
    private Gender? HoveredGender;

    public event Action<Gender>? GenderChosen;

    public GenderSelector()
    {
        Background = null;
        Width = SEGMENT_WIDTH * 2;
        Height = HEIGHT;
    }

    public void SetSelected(Gender gender) => SelectedGender = gender;

    private Gender SegmentAt(int localX) => localX < SEGMENT_WIDTH ? Gender.Male : Gender.Female;

    public override void OnMouseMove(MouseMoveEvent e) => HoveredGender = SegmentAt(e.X - ScreenX);   //adjust to the event's coordinate fields
    public override void OnMouseLeave() => HoveredGender = null;

    public override void OnClick(ClickEvent e)
    {
        var chosen = SegmentAt(e.X - ScreenX);
        e.Handled = true;

        if (chosen != SelectedGender)
            GenderChosen?.Invoke(chosen);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible) return;

        DrawSegment(spriteBatch, Gender.Male, "MALE", 0);
        DrawSegment(spriteBatch, Gender.Female, "FEMALE", SEGMENT_WIDTH);
        DrawBorder(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), LegendColors.Gold);
    }

    private void DrawSegment(SpriteBatch spriteBatch, Gender gender, string caption, int offsetX)
    {
        var bounds = new Rectangle(ScreenX + offsetX, ScreenY, SEGMENT_WIDTH, Height);
        var selected = gender == SelectedGender;
        var fill = selected ? LegendColors.Gold : HoveredGender == gender ? LegendColors.DimGray : LegendColors.Charcoal;
        DrawRect(spriteBatch, bounds, fill);
        var text = selected ? LegendColors.AlmostBlack : LegendColors.LightGray;
        //centre the caption: use the same text-draw helper UILabel uses
        DrawCaption(spriteBatch, caption, bounds, text);
    }
}
```

For `DrawCaption`/text drawing and the mouse-event coordinate fields, copy the exact calls from `CustomButton.cs` (`Draw` draws its caption label; `OnMouseDown`/`OnClick` show the event shape). The simplest robust approach: give each segment a child `UILabel` (centred, `IsHitTestVisible = false`) whose `ForegroundColor` you flip on refresh, and draw only the fills/border in `Draw` before `base.Draw` paints the labels.

- [ ] **Step 5: Build** — `dotnet build Chaos.Client/Chaos.Client.csproj` → 0 errors, no new warnings (unused-class warnings do not exist in C#; nothing references these yet and that's fine).

- [ ] **Step 6: Commit** — `git add Chaos.Client/Controls/World/Popups/Beauty && git commit -m "BeautyShop v2: head-thumbnail renderer, thumbnail strip, swatch grid, gender selector"`

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/HeadThumbnailRenderer.cs", "Chaos.Client/Controls/World/Popups/Beauty/ThumbnailStrip.cs", "Chaos.Client/Controls/World/Popups/Beauty/SwatchGrid.cs", "Chaos.Client/Controls/World/Popups/Beauty/GenderSelector.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj", "acceptanceCriteria": ["head crop cached by appearance value, Clear disposes", "strip: cells, arrows, label, hover/select/page events, gold+check selected, silver hover", "swatch grid 10 columns, same states", "gender selector two segments, gold selected, GenderChosen on the other", "client builds"], "modelTier": "standard"}
```

---

### Task 4: BeautyShopControl rewrite — layout, preview, pickers, summary, header

**Goal:** The panel is rebuilt around the spec's layout using the Task 3 controls and the Task 1–2 view model, keeping the v1 public surface (`Show`, `Hide`, `OnRejected`, `ApplyRequested`, `Closed`) so `WorldScreen` needs no change.

**Files:**
- Rewrite: `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs`
- Reference (unchanged): `Chaos.Client/Screens/WorldScreen*.cs`, `Chaos.Client/ViewModel/BeautyShop.cs`

**Acceptance Criteria:**
- [ ] Panel 600×440 centred, `FramedDialogPanelBase("_nsett", false)`, `UsesControlStack`, close button + Escape → `Closed` (unchanged semantics); confirm dialog torn down on `Hide` (v1 rule).
- [ ] Header: "JOSEPHINE'S MIRROR" title, "Customize your appearance" subtitle, "● Unsaved changes" (gold, right-aligned) visible iff `vm.HasUnsavedChanges`.
- [ ] Preview column (x 20–220): "PREVIEW" caption; a recessed pedestal (`DialogFrame.BuildRecessedTexture`, 180×200) with the **effective** appearance drawn via `DrawTextureFitted` at 1× or 2× (2× default, `1× / 2×` `CustomButton` toggles); ◀ ▶ facings; Show gear; 🎲 Randomize (`vm.Randomize()` through the `Select` funnel).
- [ ] Appearance column (x 236–596): `GenderSelector`; "Hairstyle" `ThumbnailStrip<BeautyShopHairstyleEntry>`(6); "Hair dye" `SwatchGrid`; "Skin" `ThumbnailStrip<BodyColor>`(5); "Face" `ThumbnailStrip<BeautyShopFaceEntry>`(6). Thumbnails come from one `HeadThumbnailRenderer`, keyed by the effective look with the cell's item swapped in; the renderer is `Clear()`ed whenever gender/dye/skin/face/hairstyle selection changes and on `Hide`.
- [ ] Hover on any cell/swatch → `vm.SetHover*` → preview re-renders + summary shows a dimmed `if chosen: TOTAL n`; leaving → `vm.ClearHover()`; click → `vm.Select*` via the `Select` funnel; page arrows → `vm.Step*Page`.
- [ ] Purchase summary band (y 330–392): one line joining changed categories `Name price` with ` · ` (face line only when `FaceCharged`, gender line when `GenderChanged`), `TOTAL n` gold / red when unaffordable, `You have g`, red status line, **Discard Changes** (`Enabled == HasUnsavedChanges`, calls `Reset`) and **APPLY** (`Enabled == CanApply`; gender confirm as v1; OK re-validates).
- [ ] Every mutation goes through `Select(mutate)`: hide confirm, `vm.ClearHover()` is implied by the VM, clear status, `Refresh()`.
- [ ] `dotnet build Chaos.Client/Chaos.Client.csproj` → 0 errors, no new warnings; the old `OptionRow`, hair-swatch texture and `Reset` button are gone.

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj` → Build succeeded

**Steps:**

- [ ] **Step 1: Constants and skeleton** — replace the file's layout constants and fields:

```csharp
    private const int PANEL_WIDTH = 600;
    private const int PANEL_HEIGHT = 440;
    private const int TOP_MARGIN = 20;
    private const int TITLE_TOP = 10;
    private const int SUBTITLE_TOP = TITLE_TOP + TextRenderer.CHAR_HEIGHT + 2;
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;

    //preview column
    private const int PREVIEW_LEFT = 20;
    private const int PREVIEW_TOP = 44;
    private const int PREVIEW_WIDTH = 200;
    private const int PEDESTAL_WIDTH = 180;
    private const int PEDESTAL_HEIGHT = 200;
    private const int PEDESTAL_LEFT = PREVIEW_LEFT + ((PREVIEW_WIDTH - PEDESTAL_WIDTH) / 2);
    private const int PEDESTAL_TOP = PREVIEW_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int PREVIEW_CONTROLS_TOP = PEDESTAL_TOP + PEDESTAL_HEIGHT + 6;
    private const int ROTATE_BUTTON_WIDTH = 28;
    private const int ZOOM_BUTTON_WIDTH = 60;

    //appearance column
    private const int APPEARANCE_LEFT = 236;
    private const int APPEARANCE_TOP = 44;
    private const int APPEARANCE_WIDTH = PANEL_WIDTH - APPEARANCE_LEFT - 20;
    private const int SECTION_GAP = 6;

    //summary band
    private const int SUMMARY_TOP = 330;
```

Fields: `PreviewView Preview; CustomCheckBox GearToggle; CustomButton ZoomButton, RandomizeButton; GenderSelector GenderPicker; ThumbnailStrip<BeautyShopHairstyleEntry> HairstyleStrip; SwatchGrid DyeGrid; ThumbnailStrip<BodyColor> SkinStrip; ThumbnailStrip<BeautyShopFaceEntry> FaceStrip; HeadThumbnailRenderer Thumbnails; UILabel TitleLabel, SubtitleLabel, UnsavedLabel, PreviewCaption, HairstyleCaption, DyeCaption, SkinCaption, FaceCaption, SummaryLabel, TotalLabel, HoverTotalLabel, GoldLabel, StatusLabel; CustomButton DiscardButton, ApplyButton; OkPopupMessageControl ConfirmDialog; int FacingIndex; bool Zoomed = true;`. Keep the v1 `Closed`/`ApplyRequested` events, `Show/Hide/OnKeyDown/RequestDismissal/OnApplyClicked/OnRejected/Select`.

- [ ] **Step 2: PreviewView with pedestal + zoom** — extend the v1 nested class: constructor builds `Pedestal = DialogFrame.BuildRecessedTexture(new SKColor(24, 22, 30), PEDESTAL_WIDTH, PEDESTAL_HEIGHT)`; `public bool Zoomed { get; set; } = true;` `Draw`: draw the pedestal at `(ScreenX, ScreenY)`, then if `Figure` is set compute `scale = Zoomed ? 2 : 1`, `w = Figure.Width * scale`, `h = Figure.Height * scale`, dest `= new Rectangle(ScreenX + (Width - w) / 2 + (Width/2 - AislingRenderer.CANVAS_CENTER_X*scale) - (Width - w)/2 ... )` — simpler: anchor on the body centre: `x = ScreenX + Width / 2 - AislingRenderer.CANVAS_CENTER_X * scale; y = ScreenY + Height - h - 12;` then `DrawTextureFitted(spriteBatch, Figure, new Rectangle(x, y, w, h), Color.White)`. Dispose the pedestal in `Dispose`. The cache key stays (appearance, facing); zoom only changes the draw.

- [ ] **Step 3: Build the header, preview column and appearance column** in the constructor (helper `Caption(string, x, y, width, HorizontalAlignment = Left, Color? = null)` that adds a `UILabel`):

```csharp
        TitleLabel = Caption("JOSEPHINE'S MIRROR", 0, TITLE_TOP, PANEL_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        SubtitleLabel = Caption("Customize your appearance", PREVIEW_LEFT, SUBTITLE_TOP, 300, HorizontalAlignment.Left, LegendColors.LightGray);
        UnsavedLabel = Caption("● Unsaved changes", PANEL_WIDTH - 20 - 160, SUBTITLE_TOP, 160, HorizontalAlignment.Right, LegendColors.Gold);
        UnsavedLabel.Visible = false;

        PreviewCaption = Caption("PREVIEW", PREVIEW_LEFT, PREVIEW_TOP, PREVIEW_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        Preview = new PreviewView(renderer) { X = PEDESTAL_LEFT, Y = PEDESTAL_TOP, Width = PEDESTAL_WIDTH, Height = PEDESTAL_HEIGHT };
        AddChild(Preview);

        var rotateLeft = new CustomButton("<", ROTATE_BUTTON_WIDTH) { X = PEDESTAL_LEFT, Y = PREVIEW_CONTROLS_TOP };
        rotateLeft.Clicked += () => Rotate(-1);
        var rotateRight = new CustomButton(">", ROTATE_BUTTON_WIDTH) { X = PEDESTAL_LEFT + ROTATE_BUTTON_WIDTH + 4, Y = PREVIEW_CONTROLS_TOP };
        rotateRight.Clicked += () => Rotate(+1);
        ZoomButton = new CustomButton("2x", ZOOM_BUTTON_WIDTH) { X = PEDESTAL_LEFT + PEDESTAL_WIDTH - ZOOM_BUTTON_WIDTH, Y = PREVIEW_CONTROLS_TOP };
        ZoomButton.Clicked += () => { Zoomed = !Zoomed; Preview.Zoomed = Zoomed; ZoomButton.Caption = Zoomed ? "2x" : "1x"; };
        GearToggle = /* as v1, sized */ ; GearToggle.X = PEDESTAL_LEFT; GearToggle.Y = PREVIEW_CONTROLS_TOP + CustomButton.HEIGHT + 6;
        RandomizeButton = new CustomButton("Randomize", 100) { X = PEDESTAL_LEFT, Y = GearToggle.Y + CustomCheckBox.CHECKBOX_SIZE + 6 };
        RandomizeButton.Clicked += () => Select(v => v.Randomize());
```

Appearance column, stacking `y` from `APPEARANCE_TOP`:

```csharp
        Caption("Gender", APPEARANCE_LEFT, y, 80); GenderPicker = new GenderSelector { X = APPEARANCE_LEFT + 80, Y = y - 4 };
        GenderPicker.GenderChosen += g => Select(v => v.SetGender(g)); AddChild(GenderPicker); y += GenderSelector.HEIGHT + SECTION_GAP;

        HairstyleCaption = Caption("Hairstyle", APPEARANCE_LEFT, y, APPEARANCE_WIDTH); y += TextRenderer.CHAR_HEIGHT + 2;
        HairstyleStrip = new ThumbnailStrip<BeautyShopHairstyleEntry>(ViewModel.BeautyShop.HAIRSTYLE_PAGE_SIZE) { X = APPEARANCE_LEFT, Y = y };
        HairstyleStrip.Hovered += h => Hover(v => v.SetHoverHairStyle(h.Sprite));
        HairstyleStrip.HoverCleared += () => Hover(v => v.ClearHover());
        HairstyleStrip.Selected += h => Select(v => v.SelectHairStyle(h.Sprite));
        HairstyleStrip.PageStepped += d => Select(v => v.StepHairstylePage(d));
        AddChild(HairstyleStrip); y += ThumbnailStrip<BeautyShopHairstyleEntry>.CELL + SECTION_GAP;

        DyeCaption = Caption("Hair dye", ...); y += ...;
        DyeGrid = new SwatchGrid { X = APPEARANCE_LEFT, Y = y };
        DyeGrid.Hovered += c => Hover(v => v.SetHoverHairColor(c)); DyeGrid.HoverCleared += () => Hover(v => v.ClearHover());
        DyeGrid.Selected += c => Select(v => v.SelectHairColor(c)); AddChild(DyeGrid);
        y += SwatchGrid.HeightFor(80) + SECTION_GAP;      //~133px for 8 rows; recomputed in Refresh from vm.HairColors.Count

        SkinCaption ...; SkinStrip = new ThumbnailStrip<BodyColor>(ViewModel.BeautyShop.BODY_COLOR_PAGE_SIZE) ... hover → SetHoverBodyColor, select → SelectBodyColor, page → StepBodyColorPage
        FaceCaption ...; FaceStrip = new ThumbnailStrip<BeautyShopFaceEntry>(ViewModel.BeautyShop.FACE_PAGE_SIZE) ... hover → SetHoverFace(f.Sprite), select → SelectFace(f.Sprite), page → StepFacePage
```

Vertical budget check (must fit above `SUMMARY_TOP = 330` starting at 44): gender 20+6, hairstyle 14+36+6, dye 14+133+6, skin 14+36+6, face 14+36 = 341 → **too tall by ~55px**. Resolve by placing **Skin and Face side by side** on one row (spec mock shows them paired): the skin strip (5 cells, width `WidthFor(5)` ≈ 22+4+200+22+4+60 = 312) would not fit beside the face strip in 360px — so instead shrink the dye grid to **8 rows × 10 columns at 12px + 2px gaps** (`SWATCH = 12`, `GAP = 2` → 110px tall) and drop the strips' page label width to 40 (`n/N` in 6 chars): budget becomes 26 + 56 + 130 + 56 + 50 = 318 ≤ 286? Still over. **Decision:** put "Skin" and "Face" on one row by giving each a 3-cell strip? No — keep 5/6 cells but make the strip's page label sit *under* the arrows instead of beside them, and reduce `CELL` to 32 with 3px gaps. Final numbers the implementer must hit: `CELL = 32, GAP = 3, ARROW_WIDTH = 20, LABEL_WIDTH = 0` (label drawn below the strip in the caption row: e.g. caption text "Hairstyle   12 / 101"), `SWATCH = 12, GAP = 2`. Budget: gender 26 + hairstyle (14+32+6=52) + dye (14+110+6=130) + skin 52 + face 46 = 306 → panel rows end at 350. So `SUMMARY_TOP = 352`, summary band 352–412 (two text lines + buttons), buttons at `PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4 = 440-47-22-4 = 367`?? That collides. **Therefore: PANEL_HEIGHT = 460** (still inside 480 with `TOP_MARGIN = 10`): rows end 350, summary text lines at 356 and 372, status at 388, buttons at `460-47-22-4 = 387` → collide with status. Put the status line in the summary's first line's right half instead (or replace the hover line while a rejection is showing), and buttons at 387..409, bottom border from 413. Record the final constants in the report.

- [ ] **Step 4: `Select`, `Hover`, `Refresh`**

```csharp
    private void Select(Action<ViewModel.BeautyShop> mutate)
    {
        if (ConfirmDialog.Visible) ConfirmDialog.Hide();
        mutate(WorldState.BeautyShop);
        StatusLabel.Text = string.Empty;
        Thumbnails.Clear();          //the base look may have changed; visible cells re-render on Refresh
        Refresh();
    }

    private void Hover(Action<ViewModel.BeautyShop> mutate)
    {
        mutate(WorldState.BeautyShop);
        RefreshPreview();
        RefreshSummary();            //hover total line only; strips are untouched by hover
    }

    public void Refresh()
    {
        RefreshPreview();
        RefreshPickers();
        RefreshSummary();
        UnsavedLabel.Visible = WorldState.BeautyShop.HasUnsavedChanges;
    }
```

`BuildAppearance()` uses the **effective** values (`vm.EffectiveHairStyle` etc.) with `vm.Gender`. `RefreshPickers()`:

```csharp
        var vm = WorldState.BeautyShop;
        GenderPicker.SetSelected(vm.Gender);

        var baseLook = BareAppearance(vm.Gender, vm.HairStyle, vm.HairColor, vm.BodyColor, vm.FaceSprite);   //selected, not hovered: thumbnails must not flicker on hover
        HairstyleCaption.Text = $"Hairstyle   {IndexLabel(vm.Hairstyles, h => h.Sprite == vm.HairStyle)} / {vm.Hairstyles.Count}";
        HairstyleStrip.SetItems(vm.VisibleHairstyles, vm.Hairstyles.FirstOrDefault(h => h.Sprite == vm.HairStyle), $"{vm.HairstylePage + 1}/{vm.HairstylePageCount}",
            h => Thumbnails.Get(baseLook with { HeadSprite = h.Sprite }));
        DyeGrid.SetColors(vm.HairColors, vm.HairColor, SwatchColorFor);
        SkinStrip.SetItems(vm.VisibleBodyColors, vm.BodyColor, $"{vm.BodyColorPage + 1}/{vm.BodyColorPageCount}",
            c => Thumbnails.Get(baseLook with { BodyColor = (int)c }));
        FaceStrip.SetItems(vm.VisibleFaces, vm.Faces.FirstOrDefault(f => f.Sprite == vm.FaceSprite), $"{vm.FacePage + 1}/{vm.FacePageCount}",
            f => Thumbnails.Get(baseLook with { FaceSprite = f.Sprite }));
```

`RefreshSummary()`:

```csharp
        var vm = WorldState.BeautyShop;
        var parts = new List<string>();
        if (vm.GenderChanged) parts.Add($"Gender {vm.GenderPrice:N0}");
        if (vm.HairstyleChanged) parts.Add($"Hairstyle {vm.HairstylePrice:N0}");
        if (vm.HairColorChanged) parts.Add($"Hair dye {vm.HairDyePrice:N0}");
        if (vm.BodyColorChanged) parts.Add($"Skin {vm.BodyDyePrice:N0}");
        if (vm.FaceCharged) parts.Add($"Face {vm.FacePrice:N0}");
        SummaryLabel.Text = parts.Count == 0 ? "No changes" : string.Join("  ·  ", parts);
        TotalLabel.Text = $"TOTAL {vm.Total:N0}";
        TotalLabel.ForegroundColor = vm.CanAfford ? LegendColors.Gold : LegendColors.Red;
        HoverTotalLabel.Text = vm.IsHovering && (vm.HoverTotal != vm.Total) ? $"if chosen: TOTAL {vm.HoverTotal:N0}" : string.Empty;
        GoldLabel.Text = $"You have {vm.Gold:N0}";
        DiscardButton.Enabled = vm.HasUnsavedChanges;
        ApplyButton.Enabled = vm.CanApply;
```

`SwatchColorFor` is the v1 method (dye table, Default → lavender). `Hide()` additionally calls `Thumbnails.Clear()`; `Dispose` disposes `Thumbnails`.

- [ ] **Step 5: Build, close the running client if it is still open, and commit** — `dotnet build Chaos.Client/Chaos.Client.csproj`; `git add Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs` (+ any Task 3 file you had to adjust) `&& git commit -m "BeautyShop v2: preview-first panel with thumbnail pickers, swatch grid and purchase summary"`.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj", "acceptanceCriteria": ["600x(440-460) framed panel, v1 public surface kept", "header with unsaved indicator", "pedestal preview 2x default with zoom toggle, facings, gear, randomize", "gender selector + 3 strips + swatch grid fed by HeadThumbnailRenderer keyed on selected look", "hover previews + hover total; click selects; pages step", "summary band lists changed categories, TOTAL red when unaffordable, Discard/Apply enablement", "confirm torn down on hide; thumbnails cleared on hide", "client builds"], "modelTier": "standard"}
```

---

### Task 5: Walkthrough rows, build, relaunch

**Goal:** The walkthrough doc gains the v2 checks, the client is rebuilt and relaunched against the (still running) local server for the user to test.

**Files:**
- Modify: `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md`

**Acceptance Criteria:**
- [ ] Walkthrough has new rows (all `PENDING (user)`): pedestal preview at 2× and the 1×/2× toggle; hover a hairstyle thumbnail → sprite + "if chosen" total update, leave → revert; click thumbnail → gold border + ✓, page arrows + "n / N"; dye swatch grid hover/select; skin + face strips; gender segmented selector; "● Unsaved changes" appears/disappears; Discard Changes enabled only with changes; Randomize changes all four but never gender; purchase summary lines match the total; Apply still charges the total shown.
- [ ] `dotnet build Chaos.Client/Chaos.Client.csproj` clean; the client is launched (`Chaos.Client/bin/Debug/net10.0/Chaos.Client.exe`) and shows the "Unora" window connected to 127.0.0.1:4201.

**Verify:** `tasklist | findstr /i Chaos.Client` shows the client running after the build.

**Steps:**
- [ ] Add the rows; `git add -f docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md && git commit -m "BeautyShop v2: walkthrough rows"`.
- [ ] Ensure no `Chaos.Client.exe` is running (`tasklist | findstr /i Chaos.Client`; if one is, report it — do not kill without the controller's say-so), build, then `Start-Process` the exe with working directory `Chaos.Client/bin/Debug/net10.0`; confirm window title "Unora" and an Established connection to 127.0.0.1:4201 (`Get-NetTCPConnection -OwningProcess <pid>`).

```json:metadata
{"files": ["docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md"], "verifyCommand": "tasklist | findstr /i Chaos.Client", "acceptanceCriteria": ["v2 walkthrough rows added", "client rebuilt and relaunched, connected to the local server"], "modelTier": "mechanical"}
```
