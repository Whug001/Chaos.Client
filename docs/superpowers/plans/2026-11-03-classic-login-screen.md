# Classic (retail) login screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unora's login screens use the retail Dark Ages (2002) layouts and edited retail art, behind a fixed `GlobalSettings.ClassicLogin` switch that brings the current screens back when set to false.

**Architecture:** A pure name map (`LoginLayouts`) swaps the six `_n*` layout names for the retail `l*` names that already ship in `setoa.dat`. Each lobby control keeps its code and gains only the small pieces the retail layouts need: a random corner picture, a stacked server box, fallback agreement buttons and hair color arrows. All arithmetic lives in pure static helpers so tests never touch `GlobalSettings` or `DataContext`.

**Tech Stack:** C# 14 / .NET 10, MonoGame, TUnit + FluentAssertions. Art tools in Python with PIL (Unora repo).

**Spec:** `docs/superpowers/specs/2026-11-03-classic-login-screen-design.md` (same worktree, commit `c14c79af5`).

## Global Constraints

- Delete nothing. The `_n*` layouts and all current code paths stay and still work when `ClassicLogin` is false.
- `GlobalSettings.ClassicLogin => true;` is a fixed property, not an F4 option and not in `ClientSettings`.
- Layout map, exact: `_nstart`->`lmain`, `_nlogin`->`llogin`, `_nsvr`->`lsvr`, `_npw`->`lpasswd`, `_nagree`->`lagre`, `_ncreate`->`lcreate`. Matching ignores case.
- `GenderSelector` (in-game beauty window, `Controls/World/Popups/Beauty/GenderSelector.cs`) keeps its hard-coded `_ncreate`. Do not change it.
- No server, protocol or palette change.
- Tests must not touch `GlobalSettings` or `DataContext`. Their static constructors end the test host when `cious.dat` is missing. Tests call only the pure helpers.
- Server box numbers: strips = `max(1, servers - 5)`; height = `146 + 20 * strips + 17`; width 413; centered on the 640x480 panel; row pitch = `ServerBottom.Y - ServerTop.Y` (20).
- Agreement fallback faces: `butt001.epf` OK 15 normal / 16 pressed, Cancel 21 normal / 22 pressed, centered in the control's rectangle.
- Hair color arrows step through the existing 14 swatch colors, in swatch order, wrapping at both ends.
- `docs/` is gitignored in Chaos.Client. Any new file under `docs/` needs `git add -f`.
- Comments only where the code is not self-evident. No explanatory comments in test code.
- Commit strategy is "at-end": the final task squashes the work into one commit. Implementers working through Muse commit on their `muse/<name>` branch because that is how Muse returns work; the coordinator squashes those commits in Task 8.

**User decisions (already made):**
- "yes, get it done. ill be afk" — finish plan, build, in-game check and report without asking.
- The switch is a fixed `GlobalSettings.ClassicLogin => true`, not an F4 option.
- Screens in scope: start, login, server list, password change, agreement, character creation. Loading bars, goodbye screen and in-game beauty window are out of scope.
- Art approved as shipped in `Custom Client Mods/Classic Login Screen/Edited/` (medallion at 504,90 on `lod00`, 84,410 on `dlgcre00`, copyright lines and NEXON Club block removed, palette slot 255 fixed).
- Branches stay unmerged and unpushed until the user returns.

## Review Focus

1. Logging out, or losing the connection, brings back a start screen with a freshly picked corner picture and no crash. Every return builds a new `LobbyLoginScreen` (`ChaosGame.cs:585`, `LobbyLoginScreen.cs:229/246`, `WorldScreen.cs:700`, `WorldScreen.Update.cs:26`), so the pick in the constructor covers it. Removed images dispose cached textures, which is safe because `CachedTexture2D.Dispose` does nothing (`Chaos.Client.Rendering/CachedTexture2D.cs:28`).
2. The server list gets filled a second time (log in, cancel, log in again). The old box pieces and labels must all go; no doubled box. Task 3 removes pieces by name before adding new ones.
3. A click between two rows, under the last row or right of the description belongs to the row whose half-open rectangle contains it, or to none. Task 3 pins the edges with `HitRow(...).Contains` tests.
4. The agreement box answers Enter with OK and blocks Escape exactly as today, with the fallback faces. Task 4 keeps `OnKeyDown` untouched; the playtest presses Enter.
5. A hair color picked with the arrows survives a gender switch and is the one sent to the server (`LobbyLoginScreen.cs:619-622` reads `SelectedHairColor`). Reopening creation resets it to the default, as today (`Show()`). Task 5 tests the stepping; the playtest creates one character.

---

## File map

| File | Change | Task |
|---|---|---|
| `Chaos.Client/GlobalSettings.cs` | add `ClassicLogin` | 1 |
| `Chaos.Client/Controls/LobbyLogin/LoginLayouts.cs` | new: name map | 1 |
| `Chaos.Client/Controls/LobbyLogin/{LobbyLogin,Login,ServerSelect,PasswordChange,LoginNotice,CharacterCreation}Control.cs` | base constructor uses `LoginLayouts.For` | 1 |
| `Tests/Chaos.Client.Tests/LobbyLogin/LoginLayoutsTests.cs` | new | 1 |
| `Chaos.Client/Controls/LobbyLogin/CornerPicture.cs` | new: random `lodbk0N.epf` | 2 |
| `Chaos.Client/Controls/LobbyLogin/LobbyLoginControl.cs` | `ExtraBack` corner image | 2 |
| `Tests/Chaos.Client.Tests/LobbyLogin/CornerPictureTests.cs` | new | 2 |
| `Chaos.Client/Controls/LobbyLogin/ServerListLayout.cs` | new: box arithmetic | 3 |
| `Chaos.Client/Controls/LobbyLogin/ServerSelectControl.cs` | stacked box mode | 3 |
| `Tests/Chaos.Client.Tests/LobbyLogin/ServerListLayoutTests.cs` | new | 3 |
| `Chaos.Client/Controls/LobbyLogin/LoginNoticeControl.cs` | fallback button faces | 4 |
| `Chaos.Client/Controls/LobbyLogin/HairColorCycle.cs` | new: 14 colors + `Step` | 5 |
| `Chaos.Client/Controls/LobbyLogin/CharacterCreationControl.cs` | color arrows; swatch map moves to `HairColorCycle` | 5 |
| `Tests/Chaos.Client.Tests/LobbyLogin/HairColorCycleTests.cs` | new | 5 |
| Unora: `Custom Client Mods/Classic Login Screen/**`, `Tools/ClassicLogin/*.py` | commit art + tools + README | 6 |

Build and test commands (worktree root `C:\Users\Michael\Documents\GitHub\worktrees\classic-login-client`):

```bash
git submodule update --init --recursive   # once per fresh checkout
dotnet build Chaos.Client.slnx
dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build
```

`dotnet test` does not work with this SDK. To run one class: append `-- --treenode-filter "/*/*/LoginLayoutsTests/*"`.

---

### Task 1: ClassicLogin switch and layout name map

**Goal:** `GlobalSettings.ClassicLogin` exists and is true, and all six lobby controls load the retail layout through `LoginLayouts.For`.

**Files:**
- Modify: `Chaos.Client/GlobalSettings.cs` (after `RequireSwimmingSkill`, line 81)
- Create: `Chaos.Client/Controls/LobbyLogin/LoginLayouts.cs`
- Modify: `Chaos.Client/Controls/LobbyLogin/LobbyLoginControl.cs:20`, `LoginControl.cs:15`, `ServerSelectControl.cs:20`, `PasswordChangeControl.cs:17`, `LoginNoticeControl.cs:17`, `CharacterCreationControl.cs:83`
- Test: `Tests/Chaos.Client.Tests/LobbyLogin/LoginLayoutsTests.cs`

**Acceptance Criteria:**
- [ ] `LoginLayouts.Resolve(name, true)` returns the retail name for each of the six names, ignoring case.
- [ ] `LoginLayouts.Resolve(name, false)` returns `name` unchanged for every input.
- [ ] Unknown names (`_nchar`, `_nbtn`, `lmain`) pass through unchanged with classic on.
- [ ] Each of the six controls passes `LoginLayouts.For("<old name>")` to its base constructor with the same `center` argument as before.
- [ ] `GenderSelector.cs` is unchanged.
- [ ] Build has 0 errors; the full test run reports 0 failures.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build` → build succeeds, test summary shows `failed: 0`.

**Steps:**

- [ ] **Step 1: Write the failing test** — `Tests/Chaos.Client.Tests/LobbyLogin/LoginLayoutsTests.cs`:

```csharp
using Chaos.Client.Controls.LobbyLogin;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class LoginLayoutsTests
{
    [Test]
    [Arguments("_nstart", "lmain")]
    [Arguments("_nlogin", "llogin")]
    [Arguments("_nsvr", "lsvr")]
    [Arguments("_npw", "lpasswd")]
    [Arguments("_nagree", "lagre")]
    [Arguments("_ncreate", "lcreate")]
    public void Classic_look_maps_each_login_layout(string current, string retail)
        => LoginLayouts.Resolve(current, true).Should().Be(retail);

    [Test]
    [Arguments("_nstart")]
    [Arguments("_nlogin")]
    [Arguments("_nsvr")]
    [Arguments("_npw")]
    [Arguments("_nagree")]
    [Arguments("_ncreate")]
    public void Current_look_keeps_every_name(string current)
        => LoginLayouts.Resolve(current, false).Should().Be(current);

    [Test]
    [Arguments("_nchar")]
    [Arguments("_nbtn")]
    [Arguments("lmain")]
    public void Unknown_names_pass_through(string name)
        => LoginLayouts.Resolve(name, true).Should().Be(name);

    [Test]
    public void Matching_ignores_case()
    {
        LoginLayouts.Resolve("_NSTART", true).Should().Be("lmain");
        LoginLayouts.Resolve("_nCreate", true).Should().Be("lcreate");
    }
}
```

- [ ] **Step 2: Run it and see it fail** — `dotnet build Chaos.Client.slnx` → error CS0103/CS0246: `LoginLayouts` does not exist.

- [ ] **Step 3: Add the switch** — in `Chaos.Client/GlobalSettings.cs`, right after `public static bool RequireSwimmingSkill => false;`:

```csharp

    /// <summary>
    ///     When true, the login screens use the retail (2002) layouts and art: lmain, llogin, lsvr, lpasswd, lagre and
    ///     lcreate. When false, they use the current _n* layouts. Fixed on purpose: switching back ships with a client update.
    /// </summary>
    public static bool ClassicLogin => true;
```

- [ ] **Step 4: Add the map** — `Chaos.Client/Controls/LobbyLogin/LoginLayouts.cs`:

```csharp
namespace Chaos.Client.Controls.LobbyLogin;

/// <summary>
///     Picks the layout a lobby screen loads: the retail one when the classic look is on, otherwise the current one.
/// </summary>
public static class LoginLayouts
{
    private static readonly Dictionary<string, string> CLASSIC = new(StringComparer.OrdinalIgnoreCase)
    {
        ["_nstart"] = "lmain",
        ["_nlogin"] = "llogin",
        ["_nsvr"] = "lsvr",
        ["_npw"] = "lpasswd",
        ["_nagree"] = "lagre",
        ["_ncreate"] = "lcreate"
    };

    public static string Resolve(string current, bool classic)
        => classic && CLASSIC.TryGetValue(current, out var retail) ? retail : current;

    public static string For(string current) => Resolve(current, GlobalSettings.ClassicLogin);
}
```

- [ ] **Step 5: Swap the six base calls** (exact replacements):
  - `LobbyLoginControl.cs`: `: base("_nstart", false)` -> `: base(LoginLayouts.For("_nstart"), false)`
  - `LoginControl.cs`: `: base("_nlogin")` -> `: base(LoginLayouts.For("_nlogin"))`
  - `ServerSelectControl.cs`: `: base("_nsvr")` -> `: base(LoginLayouts.For("_nsvr"))`
  - `PasswordChangeControl.cs`: `: base("_npw")` -> `: base(LoginLayouts.For("_npw"))`
  - `LoginNoticeControl.cs`: `: base("_nagree")` -> `: base(LoginLayouts.For("_nagree"))`
  - `CharacterCreationControl.cs`: `: base("_ncreate", false)` -> `: base(LoginLayouts.For("_ncreate"), false)`

- [ ] **Step 6: Run build and all tests** — Verify command above → 0 errors, `failed: 0`; the 16 `LoginLayoutsTests` cases pass.

```json:metadata
{"files": ["Chaos.Client/GlobalSettings.cs", "Chaos.Client/Controls/LobbyLogin/LoginLayouts.cs", "Chaos.Client/Controls/LobbyLogin/LobbyLoginControl.cs", "Chaos.Client/Controls/LobbyLogin/LoginControl.cs", "Chaos.Client/Controls/LobbyLogin/ServerSelectControl.cs", "Chaos.Client/Controls/LobbyLogin/PasswordChangeControl.cs", "Chaos.Client/Controls/LobbyLogin/LoginNoticeControl.cs", "Chaos.Client/Controls/LobbyLogin/CharacterCreationControl.cs", "Tests/Chaos.Client.Tests/LobbyLogin/LoginLayoutsTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["Resolve maps all six names with classic on, ignoring case", "Resolve returns input unchanged with classic off", "Unknown names pass through", "Six controls pass LoginLayouts.For(old name) with the same center argument", "GenderSelector unchanged", "Build 0 errors, tests failed: 0"], "modelTier": "mechanical"}
```

---

### Task 2: Start screen corner picture

**Goal:** On the classic start screen, the `ExtraBack` slot shows one of `lodbk00.epf`..`lodbk08.epf`, picked at random each time the start screen is built.

**Files:**
- Create: `Chaos.Client/Controls/LobbyLogin/CornerPicture.cs`
- Modify: `Chaos.Client/Controls/LobbyLogin/LobbyLoginControl.cs` (constructor, before the buttons)
- Test: `Tests/Chaos.Client.Tests/LobbyLogin/CornerPictureTests.cs`

**Acceptance Criteria:**
- [ ] `CornerPicture.Pick` over 1,000 seeded picks returns exactly the nine names `lodbk00.epf`..`lodbk08.epf`, all of them.
- [ ] When the layout has `ExtraBack`, `LobbyLoginControl` adds a `UIImage` named `ExtraBack` at the layout rectangle, textured from `UiRenderer.Instance!.GetEpfTexture(CornerPicture.Pick(Random.Shared), 0)`, added before the buttons.
- [ ] When the layout has no `ExtraBack` (current `_nstart`), nothing is added.
- [ ] Build 0 errors; tests `failed: 0`.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build` → `failed: 0`.

**Steps:**

- [ ] **Step 1: Write the failing test** — `Tests/Chaos.Client.Tests/LobbyLogin/CornerPictureTests.cs`:

```csharp
using Chaos.Client.Controls.LobbyLogin;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class CornerPictureTests
{
    [Test]
    public void Picks_stay_within_the_nine_pictures_and_reach_all_of_them()
    {
        var random = new Random(1234);

        var picks = Enumerable.Range(0, 1000)
                              .Select(_ => CornerPicture.Pick(random))
                              .ToHashSet();

        picks.Should()
             .BeEquivalentTo(Enumerable.Range(0, 9).Select(i => $"lodbk0{i}.epf"));
    }
}
```

- [ ] **Step 2: Run it and see it fail** — build error: `CornerPicture` does not exist.

- [ ] **Step 3: Add the helper** — `Chaos.Client/Controls/LobbyLogin/CornerPicture.cs`:

```csharp
namespace Chaos.Client.Controls.LobbyLogin;

/// <summary>
///     Picks the retail start screen's corner picture, one of lodbk00.epf to lodbk08.epf.
/// </summary>
public static class CornerPicture
{
    public const int COUNT = 9;

    public static string Pick(Random random) => $"lodbk0{random.Next(COUNT)}.epf";
}
```

- [ ] **Step 4: Use it in `LobbyLoginControl`** — add `using Microsoft.Xna.Framework;` to the usings, then insert right after `Y = 0;` and before `//buttons`:

```csharp
        //the retail layout names only lodbk00.epf; the retail client showed one of nine at random
        var cornerRect = GetRect("ExtraBack");

        if (cornerRect != Rectangle.Empty)
            AddChild(
                new UIImage
                {
                    Name = "ExtraBack",
                    X = cornerRect.X,
                    Y = cornerRect.Y,
                    Width = cornerRect.Width,
                    Height = cornerRect.Height,
                    Texture = UiRenderer.Instance!.GetEpfTexture(CornerPicture.Pick(Random.Shared), 0)
                });
```

No re-pick on reuse is needed: every return to the start screen builds a new `LobbyLoginScreen`, which builds a new `LobbyLoginControl`. `GetEpfTexture` returns a cached `CachedTexture2D`, whose `Dispose` does nothing, so disposing the panel later is safe.

- [ ] **Step 5: Run build and all tests** → 0 errors, `failed: 0`.

```json:metadata
{"files": ["Chaos.Client/Controls/LobbyLogin/CornerPicture.cs", "Chaos.Client/Controls/LobbyLogin/LobbyLoginControl.cs", "Tests/Chaos.Client.Tests/LobbyLogin/CornerPictureTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["1000 seeded picks give exactly lodbk00..lodbk08", "ExtraBack UIImage added before buttons when layout has ExtraBack", "Nothing added without ExtraBack", "Build 0 errors, tests failed: 0"], "modelTier": "mechanical"}
```

---

### Task 3: Stacked server box

**Goal:** On the classic layout (`lsvr`), the server list draws a box stacked from `svrtop`, one or more `svrmid` strips and `svrbot`, centered on screen, with one row per server and clicks mapped to the same rows.

**Files:**
- Create: `Chaos.Client/Controls/LobbyLogin/ServerListLayout.cs`
- Modify: `Chaos.Client/Controls/LobbyLogin/ServerSelectControl.cs` (fields, constructor, `SetServers`, `OnClick`)
- Test: `Tests/Chaos.Client.Tests/LobbyLogin/ServerListLayoutTests.cs`

**Acceptance Criteria:**
- [ ] `StripCount`: 0->1, 1->1, 5->1, 6->1, 7->2, 10->5.
- [ ] `BoxHeight(146, 20, 17, 1)` = 183 and `BoxHeight(146, 20, 17, 5)` = 263.
- [ ] `BoxOrigin(640, 480, 413, 183)` = (113, 148).
- [ ] `Row(first name rect (21,50,70,12), 20, 5)` = (21,150,70,12); row 0 equals the first rect.
- [ ] `HitRow(name (21,50,70,12), description (102,50,286,12), 20, 0)` = (21,50,367,20); it contains (21,69) and not (21,70), (20,55) or (388,55).
- [ ] The control uses the stacked mode only when the layout contains `ServerTopImage`; the current `_nsvr` path keeps today's numbers.
- [ ] Calling `SetServers` twice leaves exactly one box and one label pair per server.
- [ ] Build 0 errors; tests `failed: 0`.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build` → `failed: 0`.

**Steps:**

- [ ] **Step 1: Write the failing test** — `Tests/Chaos.Client.Tests/LobbyLogin/ServerListLayoutTests.cs`:

```csharp
using Chaos.Client.Controls.LobbyLogin;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class ServerListLayoutTests
{
    private static readonly Rectangle NAME = new(21, 50, 70, 12);
    private static readonly Rectangle DESCRIPTION = new(102, 50, 286, 12);

    [Test]
    [Arguments(0, 1)]
    [Arguments(1, 1)]
    [Arguments(5, 1)]
    [Arguments(6, 1)]
    [Arguments(7, 2)]
    [Arguments(10, 5)]
    public void Strip_count_never_drops_below_one(int servers, int strips)
        => ServerListLayout.StripCount(servers).Should().Be(strips);

    [Test]
    public void Box_height_adds_the_pieces()
    {
        ServerListLayout.BoxHeight(146, 20, 17, 1).Should().Be(183);
        ServerListLayout.BoxHeight(146, 20, 17, 5).Should().Be(263);
    }

    [Test]
    public void Box_is_centered_on_the_screen()
        => ServerListLayout.BoxOrigin(640, 480, 413, 183).Should().Be(new Point(113, 148));

    [Test]
    public void Rows_move_down_by_the_pitch()
    {
        ServerListLayout.Row(NAME, 20, 0).Should().Be(NAME);
        ServerListLayout.Row(NAME, 20, 5).Should().Be(new Rectangle(21, 150, 70, 12));
    }

    [Test]
    public void Hit_row_spans_name_to_description_and_one_pitch_high()
    {
        var row = ServerListLayout.HitRow(NAME, DESCRIPTION, 20, 0);

        row.Should().Be(new Rectangle(21, 50, 367, 20));
        row.Contains(21, 69).Should().BeTrue();
        row.Contains(21, 70).Should().BeFalse();
        row.Contains(20, 55).Should().BeFalse();
        row.Contains(388, 55).Should().BeFalse();
        ServerListLayout.HitRow(NAME, DESCRIPTION, 20, 5).Should().Be(new Rectangle(21, 150, 367, 20));
    }
}
```

- [ ] **Step 2: Run it and see it fail** — build error: `ServerListLayout` does not exist.

- [ ] **Step 3: Add the helper** — `Chaos.Client/Controls/LobbyLogin/ServerListLayout.cs`:

```csharp
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.LobbyLogin;

/// <summary>
///     Arithmetic for the retail server box (lsvr): a top piece with five row slots, one middle strip per extra row, and a
///     bottom border.
/// </summary>
public static class ServerListLayout
{
    public const int ROWS_IN_TOP = 5;

    //the retail layout always stacks one middle strip, so the box never has fewer than six slots
    public static int StripCount(int serverCount) => Math.Max(1, serverCount - ROWS_IN_TOP);

    public static int BoxHeight(int topHeight, int stripHeight, int bottomHeight, int strips)
        => topHeight + stripHeight * strips + bottomHeight;

    public static Point BoxOrigin(int screenWidth, int screenHeight, int boxWidth, int boxHeight)
        => new((screenWidth - boxWidth) / 2, (screenHeight - boxHeight) / 2);

    public static Rectangle Row(Rectangle firstRow, int pitch, int row)
        => new(firstRow.X, firstRow.Y + row * pitch, firstRow.Width, firstRow.Height);

    public static Rectangle HitRow(Rectangle firstName, Rectangle firstDescription, int pitch, int row)
        => new(firstName.X, firstName.Y + row * pitch, firstDescription.Right - firstName.X, pitch);
}
```

- [ ] **Step 4: Run the tests** → the 11 `ServerListLayoutTests` cases pass.

- [ ] **Step 5: Stacked mode in `ServerSelectControl`** — replace the whole file body with this (keeps today's numbers for `_nsvr`; adds `using Microsoft.Xna.Framework.Graphics;`):

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.LobbyLogin;

public sealed class ServerSelectControl : PrefabPanel
{
    private const int ROW_HEIGHT = 20;
    private const int FIRST_ROW_Y = 37;
    private const int NAME_X = 28;
    private const int DESC_X = 108;

    //retail (lsvr) box pieces and the first row's text slots; BoxTop is null on the current look
    private readonly Texture2D? BoxTop;
    private readonly Texture2D? BoxStrip;
    private readonly Texture2D? BoxBottom;
    private readonly int BoxWidth;
    private readonly int TopHeight;
    private readonly int StripHeight;
    private readonly int BottomHeight;
    private readonly Rectangle FirstName;
    private readonly Rectangle FirstDescription;
    private readonly int Pitch = ROW_HEIGHT;
    private Point BoxOrigin;
    private int PieceCount;

    private List<(UILabel Name, UILabel Description)> ServerLabels = [];

    private List<ServerTableEntry> Servers = [];

    public ServerSelectControl()
        : base(LoginLayouts.For("_nsvr"))
    {
        Name = "ServerSelect";
        Visible = false;

        if (!PrefabSet.Contains("ServerTopImage"))
            return;

        var cache = UiRenderer.Instance!;
        BoxTop = cache.GetPrefabTexture(PrefabSet.Name, "ServerTopImage", 0);
        BoxStrip = cache.GetPrefabTexture(PrefabSet.Name, "ServerMidImage", 0);
        BoxBottom = cache.GetPrefabTexture(PrefabSet.Name, "ServerBotImage", 0);

        var topRect = GetRect("ServerTopImage");
        BoxWidth = topRect.Width;
        TopHeight = topRect.Height;
        StripHeight = GetRect("ServerMidImage").Height;
        BottomHeight = GetRect("ServerBotImage").Height;
        FirstName = GetRect("ServerTop");
        FirstDescription = GetRect("ServerATop");

        var pitch = GetRect("ServerBottom").Y - FirstName.Y;

        if (pitch > 0)
            Pitch = pitch;
    }

    private bool Stacked => BoxTop is not null;

    public event ServerSelectedHandler? OnServerSelected;

    public void SetServers(List<ServerTableEntry> entries)
    {
        //remove previous server labels and box pieces
        foreach ((var nameLabel, var descLabel) in ServerLabels)
        {
            RemoveChild(nameLabel.Name);
            RemoveChild(descLabel.Name);
        }

        for (var i = 0; i < PieceCount; i++)
            RemoveChild($"ServerPiece{i}");

        Servers = entries;
        ServerLabels = [];
        PieceCount = 0;

        if (Stacked)
            AddBoxPieces(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            var server = entries[i];
            var nameRect = NameRect(i);
            var descRect = DescriptionRect(i);

            var nameLabel = new UILabel
            {
                Name = $"ServerName{i}",
                X = nameRect.X,
                Y = nameRect.Y,
                Width = nameRect.Width,
                Height = nameRect.Height,
                Text = server.Name,
                ForegroundColor = LegendColors.White,
                PaddingLeft = 0,
                PaddingTop = 0
            };

            var descLabel = new UILabel
            {
                Name = $"ServerDesc{i}",
                X = descRect.X,
                Y = descRect.Y,
                Width = descRect.Width,
                Height = descRect.Height,
                Text = server.Description,
                ForegroundColor = Color.LightGray,
                PaddingLeft = 0,
                PaddingTop = 0
            };

            AddChild(nameLabel);
            AddChild(descLabel);
            ServerLabels.Add((nameLabel, descLabel));
        }
    }

    private void AddBoxPieces(int serverCount)
    {
        var strips = ServerListLayout.StripCount(serverCount);
        var boxHeight = ServerListLayout.BoxHeight(TopHeight, StripHeight, BottomHeight, strips);
        BoxOrigin = ServerListLayout.BoxOrigin(Width, Height, BoxWidth, boxHeight);

        var y = BoxOrigin.Y;
        AddPiece(BoxTop!, y, TopHeight);
        y += TopHeight;

        for (var s = 0; s < strips; s++)
        {
            AddPiece(BoxStrip!, y, StripHeight);
            y += StripHeight;
        }

        AddPiece(BoxBottom!, y, BottomHeight);
    }

    private void AddPiece(Texture2D texture, int y, int height)
        => AddChild(
            new UIImage
            {
                Name = $"ServerPiece{PieceCount++}",
                X = BoxOrigin.X,
                Y = y,
                Width = BoxWidth,
                Height = height,
                Texture = texture
            });

    private Rectangle NameRect(int row)
        => Stacked
            ? InBox(ServerListLayout.Row(FirstName, Pitch, row))
            : new Rectangle(NAME_X, FIRST_ROW_Y + row * ROW_HEIGHT, DESC_X - NAME_X, ROW_HEIGHT);

    private Rectangle DescriptionRect(int row)
        => Stacked
            ? InBox(ServerListLayout.Row(FirstDescription, Pitch, row))
            : new Rectangle(DESC_X, FIRST_ROW_Y + row * ROW_HEIGHT, Width - DESC_X, ROW_HEIGHT);

    private Rectangle HitRect(int row)
        => Stacked
            ? InBox(ServerListLayout.HitRow(FirstName, FirstDescription, Pitch, row))
            : new Rectangle(NAME_X, FIRST_ROW_Y + row * ROW_HEIGHT, Width - NAME_X, ROW_HEIGHT);

    private Rectangle InBox(Rectangle rect) => new(rect.X + BoxOrigin.X, rect.Y + BoxOrigin.Y, rect.Width, rect.Height);

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        var localX = e.ScreenX - ScreenX;
        var localY = e.ScreenY - ScreenY;

        for (var i = 0; i < Servers.Count; i++)
        {
            if (!HitRect(i).Contains(localX, localY))
                continue;

            OnServerSelected?.Invoke(Servers[i].Id);
            e.Handled = true;

            return;
        }
    }
}
```

Notes for the implementer:
- `RemoveChild` disposes the removed `UIImage`, which disposes its texture. That is safe: `GetPrefabTexture` returns `CachedTexture2D`, whose `Dispose` does nothing (`Chaos.Client.Rendering/CachedTexture2D.cs:28`).
- Pieces are added before labels so the text draws on top.
- The non-stacked rectangles reproduce today's math exactly (name `28..108`, description `108..Width`, hit `28..Width`, 20 high from y 37).
- Do not resize or re-center the panel: the `lsvr` base control is 640x480 at 0,0, and the box is centered inside it.

- [ ] **Step 6: Run build and all tests** → 0 errors, `failed: 0`.

```json:metadata
{"files": ["Chaos.Client/Controls/LobbyLogin/ServerListLayout.cs", "Chaos.Client/Controls/LobbyLogin/ServerSelectControl.cs", "Tests/Chaos.Client.Tests/LobbyLogin/ServerListLayoutTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["StripCount 0->1,1->1,5->1,6->1,7->2,10->5", "BoxHeight 183 and 263", "BoxOrigin (113,148)", "Row 5 = (21,150,70,12)", "HitRow(0) = (21,50,367,20) with half-open edges", "Stacked only when ServerTopImage exists; _nsvr numbers unchanged", "SetServers twice leaves one box and one label pair per server", "Build 0 errors, tests failed: 0"], "modelTier": "mechanical"}
```

---

### Task 4: Agreement box fallback buttons

**Goal:** On the classic agreement box (`lagre`), whose OK and CANCEL slots have no pictures, the buttons get the `butt001.epf` faces the popup uses, centered in their slots.

**Files:**
- Modify: `Chaos.Client/Controls/LobbyLogin/LoginNoticeControl.cs` (constants, constructor, one new private static method)

**Acceptance Criteria:**
- [ ] A button whose `NormalTexture` is null after `CreateButton` gets `butt001.epf` frame 15/16 (OK) or 21/22 (CANCEL) as normal/pressed.
- [ ] The face is centered in the layout rectangle: `X += (Width - tex.Width) / 2`, `Y += (Height - tex.Height) / 2`, then `Width`/`Height` set to the texture size.
- [ ] Buttons that already have a texture (current `_nagree`) are untouched.
- [ ] `OnKeyDown` is unchanged (Enter clicks OK, Escape is blocked).
- [ ] Build 0 errors; tests `failed: 0`.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build` → `failed: 0`. The faces themselves are checked in the Task 7 playtest (they need game data).

**Steps:**

- [ ] **Step 1: Add constants** at the top of the class body:

```csharp
    //butt001.epf frames, the same faces OkPopupMessageControl uses
    private const int OK_NORMAL = 15;
    private const int OK_PRESSED = 16;
    private const int CANCEL_NORMAL = 21;
    private const int CANCEL_PRESSED = 22;
```

- [ ] **Step 2: Call the fallback** — directly after `CancelButton = CreateButton("CANCEL");`:

```csharp

        //the retail agreement art (lagre) has blank OK and CANCEL slots
        UseFallbackFace(OkButton, OK_NORMAL, OK_PRESSED);
        UseFallbackFace(CancelButton, CANCEL_NORMAL, CANCEL_PRESSED);
```

- [ ] **Step 3: Add the method** below the constructor:

```csharp
    private static void UseFallbackFace(UIButton? button, int normalFrame, int pressedFrame)
    {
        if (button is null || button.NormalTexture is not null)
            return;

        var cache = UiRenderer.Instance!;
        var normal = cache.GetEpfTexture("butt001.epf", normalFrame);
        var pressed = cache.GetEpfTexture("butt001.epf", pressedFrame);

        button.X += (button.Width - normal.Width) / 2;
        button.Y += (button.Height - normal.Height) / 2;
        button.Width = normal.Width;
        button.Height = normal.Height;
        button.NormalTexture = normal;
        button.PressedTexture = pressed;
    }
```

- [ ] **Step 4: Run build and all tests** → 0 errors, `failed: 0`.

```json:metadata
{"files": ["Chaos.Client/Controls/LobbyLogin/LoginNoticeControl.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["Texture-less OK/CANCEL get butt001 15/16 and 21/22", "Face centered in the layout rectangle", "Buttons with textures untouched", "OnKeyDown unchanged", "Build 0 errors, tests failed: 0"], "modelTier": "mechanical"}
```

---

### Task 5: Hair color arrows on character creation

**Goal:** On the classic creation screen (`lcreate`), `ColorLeft` and `ColorRight` step the hair color through the 14 swatch colors, wrapping, and the preview shows the pick.

**Files:**
- Create: `Chaos.Client/Controls/LobbyLogin/HairColorCycle.cs`
- Modify: `Chaos.Client/Controls/LobbyLogin/CharacterCreationControl.cs` (remove `SWATCH_COLOR_MAP`, use `HairColorCycle.Colors`; add the two buttons)
- Test: `Tests/Chaos.Client.Tests/LobbyLogin/HairColorCycleTests.cs`

**Acceptance Criteria:**
- [ ] `HairColorCycle.Colors` equals today's 14 swatch colors in the same order (Teal, Green, Olive, Yellow, Pumpkin, Apple, Violet, Default, Navy, Blue, Gray, Carrot, Brown, Black).
- [ ] `Step(Black, +1)` = Teal; `Step(Teal, -1)` = Black; `Step(Default, +1)` = Navy; `Step(Default, -1)` = Violet.
- [ ] 14 steps of +1 from Default return to Default.
- [ ] A color not in the list (Pink) gives Teal for +1 and Black for -1; direction 0 returns the input.
- [ ] The swatch click still maps cells to the same colors (it now reads `HairColorCycle.Colors`).
- [ ] `ColorLeft` steps -1 and `ColorRight` steps +1, each setting `PreviewDirty = true`; on `_ncreate` neither button exists and nothing changes.
- [ ] Build 0 errors; tests `failed: 0`.

**Verify:** `dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build` → `failed: 0`.

**Steps:**

- [ ] **Step 1: Write the failing test** — `Tests/Chaos.Client.Tests/LobbyLogin/HairColorCycleTests.cs`:

```csharp
using Chaos.Client.Controls.LobbyLogin;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class HairColorCycleTests
{
    [Test]
    public void Offers_the_fourteen_swatch_colors_in_swatch_order()
        => HairColorCycle.Colors.Should()
                         .Equal(
                             DisplayColor.Teal,
                             DisplayColor.Green,
                             DisplayColor.Olive,
                             DisplayColor.Yellow,
                             DisplayColor.Pumpkin,
                             DisplayColor.Apple,
                             DisplayColor.Violet,
                             DisplayColor.Default,
                             DisplayColor.Navy,
                             DisplayColor.Blue,
                             DisplayColor.Gray,
                             DisplayColor.Carrot,
                             DisplayColor.Brown,
                             DisplayColor.Black);

    [Test]
    public void Steps_to_the_neighbours()
    {
        HairColorCycle.Step(DisplayColor.Default, 1).Should().Be(DisplayColor.Navy);
        HairColorCycle.Step(DisplayColor.Default, -1).Should().Be(DisplayColor.Violet);
    }

    [Test]
    public void Wraps_at_both_ends()
    {
        HairColorCycle.Step(DisplayColor.Black, 1).Should().Be(DisplayColor.Teal);
        HairColorCycle.Step(DisplayColor.Teal, -1).Should().Be(DisplayColor.Black);
    }

    [Test]
    public void Fourteen_steps_return_to_the_start()
    {
        var color = DisplayColor.Default;

        for (var i = 0; i < 14; i++)
            color = HairColorCycle.Step(color, 1);

        color.Should().Be(DisplayColor.Default);
    }

    [Test]
    public void A_color_outside_the_list_starts_at_an_end()
    {
        HairColorCycle.Step(DisplayColor.Pink, 1).Should().Be(DisplayColor.Teal);
        HairColorCycle.Step(DisplayColor.Pink, -1).Should().Be(DisplayColor.Black);
    }

    [Test]
    public void Direction_zero_keeps_the_color()
        => HairColorCycle.Step(DisplayColor.Navy, 0).Should().Be(DisplayColor.Navy);
}
```

- [ ] **Step 2: Run it and see it fail** — build error: `HairColorCycle` does not exist.

- [ ] **Step 3: Add the helper** — `Chaos.Client/Controls/LobbyLogin/HairColorCycle.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Controls.LobbyLogin;

/// <summary>
///     The 14 hair colors offered at character creation, in swatch order, and stepping through them with arrows.
/// </summary>
public static class HairColorCycle
{
    private static readonly DisplayColor[] COLORS =
    [
        DisplayColor.Teal,
        DisplayColor.Green,
        DisplayColor.Olive,
        DisplayColor.Yellow,
        DisplayColor.Pumpkin,
        DisplayColor.Apple,
        DisplayColor.Violet,
        DisplayColor.Default,
        DisplayColor.Navy,
        DisplayColor.Blue,
        DisplayColor.Gray,
        DisplayColor.Carrot,
        DisplayColor.Brown,
        DisplayColor.Black
    ];

    public static IReadOnlyList<DisplayColor> Colors => COLORS;

    /// <summary>
    ///     Returns the next color for a positive direction and the previous one for a negative direction, wrapping at both
    ///     ends. A color outside the list gives the first color going forward and the last going back.
    /// </summary>
    public static DisplayColor Step(DisplayColor current, int direction)
    {
        if (direction == 0)
            return current;

        var index = Array.IndexOf(COLORS, current);

        if (index < 0)
            return direction > 0 ? COLORS[0] : COLORS[^1];

        return COLORS[(index + Math.Sign(direction) + COLORS.Length) % COLORS.Length];
    }
}
```

- [ ] **Step 4: Run the tests** → the 6 `HairColorCycleTests` pass.

- [ ] **Step 5: Use it in `CharacterCreationControl`**:
  1. Delete the `SWATCH_COLOR_MAP` field (lines 17-33).
  2. In `HandleSwatchClick`, replace `if (cellIndex >= SWATCH_COLOR_MAP.Length)` with `if (cellIndex >= HairColorCycle.Colors.Count)` and `SelectedHairColor = SWATCH_COLOR_MAP[cellIndex];` with `SelectedHairColor = HairColorCycle.Colors[cellIndex];`.
  3. Add two properties next to `HairRightButton`:

```csharp
    public UIButton? ColorLeftButton { get; }
    public UIButton? ColorRightButton { get; }
```

  4. In the constructor, right after the `HairRightButton.Clicked += OnHairRightClicked;` block:

```csharp

        //the retail layout (lcreate) has color arrows instead of the swatch
        ColorLeftButton = CreateButton("ColorLeft");
        ColorRightButton = CreateButton("ColorRight");

        if (ColorLeftButton is not null)
            ColorLeftButton.Clicked += () => StepHairColor(-1);

        if (ColorRightButton is not null)
            ColorRightButton.Clicked += () => StepHairColor(1);
```

  5. Add the method next to `OnHairRightClicked`:

```csharp
    private void StepHairColor(int direction)
    {
        SelectedHairColor = HairColorCycle.Step(SelectedHairColor, direction);
        PreviewDirty = true;
    }
```

- [ ] **Step 6: Run build and all tests** → 0 errors, `failed: 0`.

```json:metadata
{"files": ["Chaos.Client/Controls/LobbyLogin/HairColorCycle.cs", "Chaos.Client/Controls/LobbyLogin/CharacterCreationControl.cs", "Tests/Chaos.Client.Tests/LobbyLogin/HairColorCycleTests.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["Colors equals the 14 swatch colors in order", "Step wraps both ends and steps neighbours", "14 steps return to start", "Outside color gives first/last; direction 0 keeps", "Swatch click reads HairColorCycle.Colors", "ColorLeft -1 / ColorRight +1 mark preview dirty; absent on _ncreate", "Build 0 errors, tests failed: 0"], "modelTier": "mechanical"}
```

---

### Task 6: Commit the Unora art and pack setoa.dat

**Goal:** The 12 retail EPFs, the 12 edited EPFs, the two art tools and a short README are committed on the Unora worktree branch, and `Desktop\Unora Dats\setoa.dat` holds the edited pictures. Done by the coordinator (Claude), not Muse.

**Files:**
- Create: `Custom Client Mods/Classic Login Screen/README.md` (Unora worktree `C:\Users\Michael\Documents\GitHub\worktrees\classic-login-unora`)
- Commit: `Custom Client Mods/Classic Login Screen/Retail/*.epf`, `Custom Client Mods/Classic Login Screen/Edited/*.epf`, `Tools/ClassicLogin/classic_art.py`, `Tools/ClassicLogin/pack_setoa.py`
- Output: `C:\Users\Michael\Desktop\Unora Dats\setoa.dat`

**Acceptance Criteria:**
- [ ] README names the switch (`GlobalSettings.ClassicLogin`), the 12 files, how to rebuild (`python Tools/ClassicLogin/classic_art.py`) and how to pack (`python Tools/ClassicLogin/pack_setoa.py`).
- [ ] One Unora commit on `feature/classic-login`, staged by explicit paths; `git show --stat HEAD` lists 24 EPFs, 2 scripts and the README, nothing else.
- [ ] `pack_setoa.py` prints 12 entries replaced; the output file exists in `Desktop\Unora Dats` and is about 34,008,628 bytes.

**Verify:** `git -C <unora wt> show --stat HEAD` → 27 files; `ls -l "C:/Users/Michael/Desktop/Unora Dats/setoa.dat"` → present.

**Steps:**

- [ ] **Step 1:** Write the README (plain English, under 30 lines).
- [ ] **Step 2:** `git add` the 27 paths explicitly; commit "Classic login screen: retail art, edits and tools".
- [ ] **Step 3:** `python Tools/ClassicLogin/pack_setoa.py` (plain `python`, PIL needed) → check its output line and file size.

```json:metadata
{"files": ["Custom Client Mods/Classic Login Screen/README.md", "Custom Client Mods/Classic Login Screen/Retail/", "Custom Client Mods/Classic Login Screen/Edited/", "Tools/ClassicLogin/classic_art.py", "Tools/ClassicLogin/pack_setoa.py"], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/classic-login-unora show --stat HEAD", "acceptanceCriteria": ["README covers switch, files, rebuild, pack", "One Unora commit with 27 files staged by explicit path", "pack_setoa.py reports 12 entries replaced; Desktop/Unora Dats/setoa.dat present"], "modelTier": "mechanical", "owner": "coordinator"}
```

---

### Task 7: In-game check

**Goal:** Every classic login screen works in a local game: start, login, server list, password change, agreement and character creation, ending with one created character.

> **USER-ORDERED GATE — NON-SKIPPABLE.** This task was requested by the user in the current conversation. It MUST NOT be closed by walking around it, by declaring it "verified inline", or by substituting a cheaper check. Close only after every item in `acceptanceCriteria` has been re-validated independently, with output captured.

**Files:**
- No source files. Test client data folder gets the new `setoa.dat` from Task 6 for the run.

**Acceptance Criteria:**
- [ ] Screenshot: start screen shows the edited `lod00` art, a corner picture, the version text, and no crystal ball; a hovered button shows its second frame.
- [ ] Two launches (or a logout) show a corner picture each time with no crash; differing pictures are noted when they differ.
- [ ] Screenshot: login box (`llogin`) accepts name and password.
- [ ] Screenshot: server list box (`lsvr`) with one row per server, if the local lobby lists more than one server. If it lists one, the screen is reported as not reachable rather than passed.
- [ ] Screenshot: password change box (`lpasswd`) opens and Cancel closes it.
- [ ] Screenshot: agreement box (`lagre`) shows OK and Cancel faces; Enter accepts.
- [ ] Screenshot: creation screen; the color arrows change the preview's hair color; gender, hair style and facing arrows work; Cancel returns.
- [ ] One character is created with a non-default hair color, and that color is visible in the world after login.

**Verify:** `/playtest classic login` with the Task 6 `setoa.dat` → screenshots for each criterion and a pass/fail list.

**Steps:**

- [ ] **Step 1:** Build the worktree client (Debug) and copy `Desktop\Unora Dats\setoa.dat` into the test client's data folder for the run (keep a backup of the one it replaces and restore it afterward).
- [ ] **Step 2:** Run the `playtest` skill through the screens in the order above, taking a screenshot for each criterion.
- [ ] **Step 3:** Route any small bug to Muse as a fix job on the same branch; report large ones.

```json:metadata
{"files": [], "verifyCommand": "/playtest classic login", "acceptanceCriteria": ["Start screen screenshot: lod00 art, corner picture, version text, no crystal ball, hover frame", "Repeat start screen shows a corner picture without crash", "Login box screenshot", "Server list screenshot or reported not reachable with one server", "Password change box opens and cancels", "Agreement box shows OK/Cancel faces; Enter accepts", "Creation: color arrows change preview; gender/hair/facing/cancel work", "Character created with non-default hair color visible in world"], "modelTier": "standard", "userGate": true, "tags": ["user-gate"], "owner": "coordinator"}
```

---

### Task 8: Commit the full implementation

**Goal:** The client branch `feature/classic-login` holds the spec, the plan and exactly one implementation commit.

**Files:**
- All files from Tasks 1-5.

**Acceptance Criteria:**
- [ ] Muse work commits after the plan commit are squashed with `git reset --soft <plan commit>` and committed once.
- [ ] `git log --oneline main..feature/classic-login` shows 3 commits: spec, plan, implementation.
- [ ] Build 0 errors and tests `failed: 0` on the final commit.
- [ ] Nothing is pushed or merged.

**Verify:** `git log --oneline main..HEAD` → 3 lines; build and test command → `failed: 0`.

**Steps:**

- [ ] **Step 1:** `git reset --soft <plan commit sha>` (recorded in the ledger when the plan is committed).
- [ ] **Step 2:** `git commit` with message "Classic (retail) login screen behind GlobalSettings.ClassicLogin" plus the attribution lines.
- [ ] **Step 3:** Run the build and tests once more.

```json:metadata
{"files": [], "verifyCommand": "git log --oneline main..HEAD", "acceptanceCriteria": ["Implementation squashed into one commit after the plan commit", "3 commits on branch past main", "Build 0 errors, tests failed: 0", "Nothing pushed or merged"], "modelTier": "mechanical", "owner": "coordinator"}
```
