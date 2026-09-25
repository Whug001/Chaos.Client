# Guild Cloak Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A guild buys a cloak deed from Tibbs; the guild leader paints the guild cloak's front and back in an in-game editor; an admin approves it in an in-game review window; members buy the Guild Cloak from Quill, and everyone sees each wearer's current guild design.

**Architecture:** The server stores every guild's designs (`GuildCloakState`) and owns all rules in `GuildCloakService`. After it sends a player's look, it sends a `GuildCloakLook` naming the design id that player's cloak shows. The client asks for a design once (`GuildCloakDesignRequest`) and paints it onto each cloak frame itself (`GuildCloakPainter`). The painter maps each pixel onto a reference grid and keeps the frame's own shading. The editor and review windows are new client windows. The cloak art is a copy of sprite 127 under its own number.

**Tech Stack:** C# 14 / .NET 10, MonoGame SpriteBatch, SkiaSharp, DALib EPF frames, Chaos-Server packet converters (`SpanReader`/`SpanWriter`), Chaos `IStorage<T>` JSON storage, TUnit + FluentAssertions + Moq, Python 3 accessory tools (`Unora/Tools/Accessories`).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-09-25-guild-cloak-design.md` (committed on `main` as `502d539`). Approved mockups: `Unora/.superpowers/brainstorm/345065-1790308407/content/cloak-editor.html`. The throwaway mapping test the painter is based on: `Unora/.superpowers/brainstorm/345065-1790308407/spike/fullpaint.py`.

## Global Constraints

- **Work only in the three worktrees from Task 0.** Never edit, stage, stash, reset or switch branches in the shared checkouts (`C:/Users/Michael/Documents/GitHub/Chaos.Client`, its `Chaos-Server` submodule, `C:/Users/Michael/Documents/GitHub/Unora`). Other Claude sessions work in them.
  - `SRV` = `C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server` (Chaos-Server, branch `feat/guild-cloak` from `master`)
  - `CLI` = `C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client` (Chaos.Client, branch `feat/guild-cloak` from `main`)
  - `UNO` = `C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora` (Unora, branch `feat/guild-cloak` from `main`)
- **Do not commit** in Tasks 0–9. Leave all changes in the worktrees. Task 10 makes one commit per repo (at-end strategy).
- **Build the client against the server worktree:** `dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server`. Never run two builds at once; they share the protocol projects' output folders. If a build fails with MSB3027 (a file is locked), a local `Chaos.exe` or game client is running. Stop and report; don't kill it.
- **Test projects are TUnit executables.** Use `dotnet run --project ... -- --treenode-filter "..." --no-ansi`, never `dotnet test`. For the client, build first (command above), then `dotnet run --no-build --project .../Chaos.Client.Tests.csproj -- ...`.
- **Two server tests already fail on master:** `GiveAbility` and `OnItemDroppedOn` (stackable). Leave them alone; every other test must pass.
- **Serena for C#.** Read and edit C# files with Serena's tools, as the user's global CLAUDE.md requires. Serena works in the worktrees (they sit under `C:/Users/Michael/Documents/GitHub`). Serena has no Python server here, so Python and JSON files use the built-in Read/Edit/Write tools.
- **Never stage** `Chaos/appsettings.json` or `Chaos.Client/Properties/launchSettings.json`.
- **The game font is 6×12 ASCII.** Use only ASCII in UI captions and messages.
- **Accessory tools never patch the client folder** (`~/Documents/Unora/Unora Files`). Never pass `--apply` or `--apply-local`. The user copies batches in by hand.
- **Exact values** (from the spec and this plan):

  | Value | Setting |
  |---|---|
  | Guild Cloak sprite / icon | 328 / 5319 (from `ids.py next`; Task 1 confirms) |
  | Back grid | 27 × 41 (male walk sheet `01`, frame 0, `c` layer) |
  | Lining grid | 27 × 38 (frame 5, `g` layer) |
  | Collar grid | 18 × 8 (frame 5, `c` layer) |
  | Colors per design | 1 to 6, any RGB |
  | Default design | one color, RGB 40, 40, 48, on every cell |
  | Rejection reason | 1 to 200 characters |
  | Cloak deed | 5,000,000 gold (the existing `GOLD_UPGRADE_COST`) |
  | Guild Cloak | 50,000 gold |
  | Save or submit | at most once every 2 seconds per player |
  | Review list | at most 20 designs, oldest first |
  | Design re-request | at most once every 10 seconds per id |
  | Painter shades | edge 0.42; other pixels 0.78 + 0.75 × brightness / 99 |
  | Client opcodes | `GuildCloakEditorInteraction = 126`, `GuildCloakDesignRequest = 127`, `GuildCloakReviewInteraction = 128` |
  | Server opcodes | `GuildCloakEditor = 130`, `GuildCloakLook = 131`, `GuildCloakDesign = 132`, `GuildCloakReviewList = 133` |

**User decisions (already made):**
- Guild cloaks are the first guild hall idea to build; all 30 ideas are saved in `Unora/docs/guild-hall-ideas.md`.
- A full cloak editor: the leader paints a front view and a back view, and the game maps them onto every frame.
- The cloak art is the dyeable cape, sprite 127 (copied to its own number).
- After the guild buys the deed, members buy their own cloaks.
- Any color, up to 6 colors per design.
- Admins review in an in-game review window opened from the admin trinket.
- A cloak always shows its wearer's current guild's approved design; plain when there is none.
- Approach A: the client paints the cloak as it draws it. No per-guild sprite files.
- Admins get an option to clear a guild's approved design.
- The deed costs 5M, a cloak costs 50,000 gold, and submitting is free.

**Changes from the spec (decided while planning):**
1. The Guild Cloak gets its own icon slot, a copy of the Black Cape's icon 4266. The accessory tools require every item to own an icon.
2. The server message that opens the editor is `GuildCloakEditor`, with a type (`Open` or `Status`). The same message updates the status line after a save.
3. The editor and review messages have no `Close` action. The server has nothing to do when a window closes.
4. The review list holds at most 20 designs, oldest first, so the message stays under the 64 KB packet limit. Deciding one brings in the next.
5. Online admins get an orange-bar message when a leader submits a design, so they know one is waiting.
6. The windows use a new walking preview, `GuildCloakPreview`, modeled on the beauty shop's. The beauty shop's preview is private and only shows idle poses.
7. The client gives every changed local design a new negative id (`GuildCloakDesignStore.SetLocal`), instead of fixed ids −1 and −2. A new id is what makes the preview redraw.
8. When a reference row is empty, the painter takes the nearest row with pixels. The spec's "step toward the middle" can loop forever when two middle rows are empty.
9. Closing the editor with unsaved changes: the first press puts a warning on the status line, and the second press closes.
10. After a save or submit, the editor's Save and Submit buttons stay dim for 2 seconds (the server's rate limit).

---

## File map

| Repo | File | Responsibility |
|---|---|---|
| UNO | `Tools/Accessories/items/GuildCloak/*` | copy sprite 127's sheets and icon to the guild cloak's numbers |
| UNO | `Tools/Accessories/registry.json` | the guild cloak's registry entry (via `ids.py`) |
| UNO | `Data/Configuration/Templates/Items/Equipment/Temuair/Accessories/guildCloak.json` | the Guild Cloak item |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_cloaks*.json` | the deed purchase |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_*.json` | design and buy dialogs |
| UNO | `Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_*.json` | review and clear dialogs |
| SRV | `Chaos.DarkAges/Definitions/GuildCloakProtocol.cs`, `GuildCloakDesign.cs`, `Enums.cs`, `CONSTANTS.cs` | shared numbers, the design type, 5 enums, client version |
| SRV | `Chaos.Networking.Abstractions/Definitions/Enums.cs` | 7 opcodes |
| SRV | `Chaos.Networking/Entities/Server/GuildCloak*Args.cs`, `GuildCloakReviewEntry.cs` | 4 server messages |
| SRV | `Chaos.Networking/Entities/Client/GuildCloak*Args.cs` | 3 client messages |
| SRV | `Chaos.Networking/Converters/GuildCloakDesignCodec.cs`, `Converters/Server/GuildCloak*Converter.cs`, `Converters/Client/GuildCloak*Converter.cs` | wire format |
| SRV | `Chaos/Models/World/GuildCloakState.cs`, `Chaos/SerializationContext.cs` | stored designs |
| SRV | `Chaos/Services/GuildCloak/GuildCloakService.cs`, `GuildCloakRefresh.cs` | rules, rate limit, look ids, refreshes |
| SRV | `Chaos/Extensions/ServiceCollectionExtensions.cs` | service registration |
| SRV | `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, `Chaos/Networking/ChaosWorldClient.cs` | 3 send methods; look after display |
| SRV | `Chaos/Services/Servers/WorldServer.cs` | 3 handlers |
| SRV | `Chaos/Models/World/GuildHouseState.cs`, `Scripting/.../GuildUpdateHallScript.cs` | the `cloaks` deed |
| SRV | `Scripting/.../GuildScripts/GuildCloakScript.cs`, `Scripting/.../Generic/GuildCloakAdminScript.cs` | Quill and admin trinket |
| SRV | `Scripting/.../GuildScripts/GuildDisbandScript.cs`, `Chaos/Collections/Guild.cs` | cleanup and look refreshes |
| CLI | `Chaos.Client.Rendering/GuildCloakGrid.cs`, `GuildCloakPainter.cs`, `GuildCloakDesignStore.cs`, `GuildCloakReferences.cs` | painting |
| CLI | `Chaos.Client.Rendering/AislingRenderer.cs` | design id on the appearance; guild cloak layers |
| CLI | `Chaos.Client.Networking/ConnectionManager.cs`, `Definitions/Delegates.cs` | 4 events, 3 sends |
| CLI | `Chaos.Client/Collections/WorldState.cs` | which design each player shows |
| CLI | `Chaos.Client/Screens/WorldScreen.GuildCloak.cs` (new partial), `WorldScreen.cs` | wiring and the two windows |
| CLI | `Chaos.Client/ViewModel/GuildCloakEditorModel.cs` | editor state, tools, undo |
| CLI | `Chaos.Client/Controls/World/Popups/GuildCloak/*.cs` | canvas, swatches, preview, editor, review |
| CLI | `CLAUDE.md` | the new windows |

---

### Task 0: Create the three worktrees

**Goal:** Isolated `feat/guild-cloak` branches for the server, client and Unora, so no shared checkout is touched.

**Files:**
- Create: worktrees `SRV`, `CLI`, `UNO` (see Global Constraints)

**Acceptance Criteria:**
- [ ] `git -C <each worktree> branch --show-current` prints `feat/guild-cloak`
- [ ] The client test project builds against `SRV` before any change
- [ ] `CLI/docs/superpowers/plans/` holds this plan and its `.tasks.json`

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server branch --show-current` → `feat/guild-cloak`

**Steps:**

- [ ] **Step 1: Check for running game processes (they lock builds)**

Run (PowerShell): `Get-Process Chaos, Chaos.Client -ErrorAction SilentlyContinue | Select-Object Name, Id, Path`
Expected: nothing. If anything is listed, stop and report. Do not kill it.

- [ ] **Step 2: Create the worktrees**

```bash
cd /c/Users/Michael/Documents/GitHub
mkdir -p worktrees
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server -b feat/guild-cloak master
git -C Chaos.Client worktree add /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client -b feat/guild-cloak main
git -C Unora -c core.longpaths=true worktree add /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora -b feat/guild-cloak main
```

Pass `core.longpaths` with `-c` only. Never write it to the repo config. The client worktree's `Chaos-Server` submodule stays empty; builds use `UnoraServerPath` instead.

- [ ] **Step 3: Copy this plan into the client worktree**

The spec is already committed on `main`. The plan and its tasks file were written in the shared checkout and are not committed:

```bash
mkdir -p /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/docs/superpowers/plans
cp /c/Users/Michael/Documents/GitHub/Chaos.Client/docs/superpowers/plans/2026-09-25-guild-cloak.md /c/Users/Michael/Documents/GitHub/Chaos.Client/docs/superpowers/plans/2026-09-25-guild-cloak.md.tasks.json /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/docs/superpowers/plans/
ls /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/docs/superpowers/specs/2026-09-25-guild-cloak-design.md
```

Expected: the `ls` prints the spec path.

- [ ] **Step 4: Baseline builds and tests**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildHall/*/*" --no-ansi
```

Expected: `Build succeeded`, and the GuildHall tests pass.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server branch --show-current", "acceptanceCriteria": ["three worktrees on feat/guild-cloak", "baseline client build succeeds against SRV", "plan and tasks file copied into CLI"], "modelTier": "mechanical"}
```

---

### Task 1: Guild cloak sprite, icon and item template (Unora)

**Goal:** Sprite 127's sheets and the Black Cape icon copied to the guild cloak's own numbers by a registered accessory tool, plus the Guild Cloak item template.

**Files:**
- Create: `UNO/Tools/Accessories/items/GuildCloak/guildcloak_art.py`
- Create: `UNO/Tools/Accessories/items/GuildCloak/build_guildcloak.py`
- Create: `UNO/Tools/Accessories/items/GuildCloak/preview.py`
- Create: `UNO/Tools/Accessories/items/GuildCloak/test_guildcloak.py`
- Create: `UNO/Tools/Accessories/items/GuildCloak/README.md`
- Create: `UNO/Data/Configuration/Templates/Items/Equipment/Temuair/Accessories/guildCloak.json`
- Modify: `UNO/Tools/Accessories/registry.json` (through `ids.py` only)

**Acceptance Criteria:**
- [ ] `python Tools/Accessories/ids.py` reports no clashes, and the registry has `guildCloak` with a sprite and an icon
- [ ] Every `c` and `g` sheet of sprite 127, for both bodies, is copied byte for byte to the new number
- [ ] The new sprite's palette number equals sprite 127's, and the new icon frame equals icon 4266's
- [ ] `test_guildcloak.py` and `test_items.py` pass
- [ ] `guildCloak.json` has the assigned `displaySprite` and `panelSprite`

**Verify:** `cd C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora && python -m unittest Tools/Accessories/items/GuildCloak/test_guildcloak.py Tools/Accessories/test_items.py` → `OK`

**Steps:**

- [ ] **Step 1: Check the numbers and the template key are free**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora
python Tools/Accessories/ids.py next
grep -ril '"templateKey": "guildcloak"' Data/Configuration/Templates/Items || echo "templateKey free"
```

Expected: `next free accessory: sprite 328, icon 5319` and `templateKey free`. If the numbers differ, use the printed numbers everywhere this plan says 328 or 5319, including `GuildCloakProtocol.SPRITE` in Task 2.

- [ ] **Step 2: Write the item template**

Create `UNO/Data/Configuration/Templates/Items/Equipment/Temuair/Accessories/guildCloak.json`:

```json
{
  "accountBound": true,
  "buyCost": 50000,
  "category": "Cloak",
  "color": "Default",
  "displaySprite": 0,
  "equipmentType": "Accessory",
  "gender": "Unisex",
  "isDyeable": false,
  "maxStacks": 1,
  "modifiers": {},
  "noTrade": true,
  "sellValue": 1000,
  "advClass": "None",
  "class": "Peasant",
  "description": "A cloak in the colors of your guild. It shows your guild's approved design.",
  "name": "Guild Cloak",
  "panelSprite": 0,
  "scriptKeys": [
    "equipment",
    "pickupNotifier"
  ],
  "scriptVars": {
    "equipment": {}
  },
  "templateKey": "guildCloak"
}
```

- [ ] **Step 3: Write the art constants**

Create `UNO/Tools/Accessories/items/GuildCloak/guildcloak_art.py`:

```python
"""The Guild Cloak accessory: an exact copy of the Black Cape of Romance (accessory sprite 127, icon 4266).

The client paints each guild's approved design onto this sprite as it draws it (Chaos.Client
GuildCloakPainter), so the art itself stays the plain cape. A separate sprite number means any
accessory drawn with it is a guild cloak, and the Black Cape keeps its own look.

The server and the client read the sprite number from GuildCloakProtocol.SPRITE in Chaos.DarkAges.
If `ids.py assign` gives a different number, change that constant to match.
"""
from __future__ import annotations

SPRITE_ID = 328
ICON_ID = 5319

SOURCE_SPRITE = 127  # Black Cape of Romance: its c and g sheets for both body types
SOURCE_ICON = 4266  # Black Cape of Romance's inventory icon
```

- [ ] **Step 4: Write the failing tests**

Create `UNO/Tools/Accessories/items/GuildCloak/test_guildcloak.py`:

```python
"""What is special about the Guild Cloak: it is a byte-for-byte copy of the Black Cape of Romance."""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1]))  # Tools/Accessories: the shared acclib package

from acclib.daformats import DatArchive, Epf
from acclib.icons import icon_slot
from build_guildcloak import ARCHIVES, DEFAULT_DATA_DIR, LAYERS, build, copy_name, palette_number, source_sheets
from guildcloak_art import ICON_ID, SOURCE_ICON, SOURCE_SPRITE, SPRITE_ID

HAVE_DATA = all((DEFAULT_DATA_DIR / n).exists() for n in ARCHIVES)


class NameTests(unittest.TestCase):
    def test_copy_name_swaps_only_the_number(self):
        self.assertEqual(copy_name("mc12701.epf"), f"mc{SPRITE_ID:03d}01.epf")
        self.assertEqual(copy_name("wg127b.epf"), f"wg{SPRITE_ID:03d}b.epf")

    def test_palette_number_prefers_an_own_line_over_a_range(self):
        tbl = b"300 400 5\r\n328 0\r\n"
        self.assertEqual(palette_number(tbl, 328), 0)
        self.assertEqual(palette_number(tbl, 329), 5)
        self.assertEqual(palette_number(tbl, 127), 0)


@unittest.skipUnless(HAVE_DATA, "client archives not found")
class BuildTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.before = {n: DatArchive.load(DEFAULT_DATA_DIR / n) for n in ARCHIVES}
        cls.after = build({n: DatArchive.from_bytes(a.to_bytes()) for n, a in cls.before.items()})

    def test_every_source_sheet_is_copied_byte_for_byte(self):
        copied = 0
        for g in "mw":
            for letter, arc_name in LAYERS:
                name = arc_name.format(g=g)
                for sheet in source_sheets(self.before[name], g, letter):
                    self.assertEqual(self.after[name][copy_name(sheet)], self.after[name][sheet], sheet)
                    copied += 1
        self.assertGreaterEqual(copied, 32)  # 8 animations x 2 layers x 2 bodies

    def test_palette_matches_the_source(self):
        tbl = self.after["khanpal.dat"]["palc.tbl"]
        self.assertEqual(palette_number(tbl, SPRITE_ID), palette_number(tbl, SOURCE_SPRITE))

    def test_icon_is_the_source_icon(self):
        legend = self.after["Legend.dat"]

        def frame(icon: int):
            sheet, slot = icon_slot(icon)
            f = Epf.from_bytes(legend[sheet]).frames[slot]
            return f.left, f.top, f.width, f.height, bytes(f.data)

        self.assertEqual(frame(ICON_ID), frame(SOURCE_ICON))

    def test_build_is_repeatable(self):
        again = build({n: DatArchive.from_bytes(a.to_bytes()) for n, a in self.after.items()})
        for n in ARCHIVES:
            self.assertEqual(again[n].to_bytes(), self.after[n].to_bytes(), n)


if __name__ == "__main__":
    unittest.main()
```

Run: `python -m unittest Tools/Accessories/items/GuildCloak/test_guildcloak.py`
Expected: FAIL with `ModuleNotFoundError: No module named 'build_guildcloak'`.

If `Frame` has no `left`/`top`/`data` attributes, read `acclib/daformats.py` (class `Frame`) and use its field names; the check is "same position, size and pixels".

- [ ] **Step 5: Write the build**

Create `UNO/Tools/Accessories/items/GuildCloak/build_guildcloak.py`:

```python
"""Build the Guild Cloak accessory: copy sprite 127's sheets and icon 4266 to the guild cloak's numbers.

    python Tools/Accessories/items/GuildCloak/build_guildcloak.py
    python Tools/Accessories/items/GuildCloak/build_guildcloak.py --out-dir DIR

Safe to re-run: every entry it writes is replaced in place on the next run. Never pass --apply;
the user copies batches into the client folder by hand.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # Tools/Accessories: the shared acclib package
from acclib.archive import last_before
from acclib.cli import write_outputs
from acclib.daformats import DatArchive, Epf, set_table_line
from acclib.icons import icon_palette_number, icon_slot, put_icon
from guildcloak_art import ICON_ID, SOURCE_ICON, SOURCE_SPRITE, SPRITE_ID

DEFAULT_DATA_DIR = Path.home() / "Documents" / "Unora" / "Unora Files"
HERE = Path(__file__).resolve().parent

ARCHIVES = ["khanmad.dat", "khanwad.dat", "khanmeh.dat", "khanweh.dat", "khanpal.dat", "Legend.dat"]
LAYERS = (("c", "khan{g}ad.dat"), ("g", "khan{g}eh.dat"))


def source_sheets(arc: DatArchive, g: str, letter: str) -> list[str]:
    """Every sheet of the source sprite on one layer, in archive order."""
    prefix = f"{g}{letter}{SOURCE_SPRITE:03d}".lower()
    return [n for n in arc.names if n.lower().startswith(prefix)]


def copy_name(name: str) -> str:
    """mc12701.epf -> mc32801.epf: the same sheet under the guild cloak's number."""
    return f"{name[:2]}{SPRITE_ID:03d}{name[5:]}"


def palette_number(tbl: bytes, sprite: int) -> int:
    """The palette a khan table gives a sprite, as the client reads it: an "id pal" line wins over a
    "first last pal" range, and a later line of the same kind wins over an earlier one."""
    override = ranged = None
    for line in tbl.decode("latin1").split("\r\n"):
        parts = [int(p) for p in line.split()] if line.strip() else []
        if len(parts) == 2 and parts[0] == sprite:
            override = parts[1]
        elif len(parts) == 3 and parts[2] >= 0 and parts[0] <= sprite <= parts[1]:
            ranged = parts[2]
    return override if override is not None else ranged if ranged is not None else 0


def build(archives: dict[str, DatArchive]) -> dict[str, DatArchive]:
    for g in "mw":
        for letter, arc_name in LAYERS:
            arc = archives[arc_name.format(g=g)]
            after = last_before(arc, f"{g}{letter}", SPRITE_ID)
            for name in source_sheets(arc, g, letter):
                copy = copy_name(name)
                arc.put(copy, arc[name], after=after)
                after = copy

    # the copy must draw with the source's palette even if a range line covers the new number
    khanpal = archives["khanpal.dat"]
    wanted = palette_number(khanpal["palc.tbl"], SOURCE_SPRITE)
    if palette_number(khanpal["palc.tbl"], SPRITE_ID) != wanted:
        khanpal.put("palc.tbl", set_table_line(khanpal["palc.tbl"], str(SPRITE_ID), f"{SPRITE_ID} {wanted}"))

    legend = archives["Legend.dat"]
    sheet_name, slot = icon_slot(SOURCE_ICON)
    frame = Epf.from_bytes(legend[sheet_name]).frames[slot]
    pal_name = f"item{icon_palette_number(legend, SOURCE_ICON):03d}.pal"
    put_icon(legend, ICON_ID, frame, legend[pal_name])
    return archives


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--out-dir", type=Path)
    ap.add_argument("--preview-dir", type=Path)
    args = ap.parse_args()
    preview_dir = args.preview_dir or HERE / "preview"

    archives = {n: DatArchive.load(args.data_dir / n) for n in ARCHIVES}
    before = {n: a.to_bytes() for n, a in archives.items()}
    build(archives)

    from preview import write_previews
    write_previews(archives, preview_dir, args.data_dir)
    print(f"previews written to {preview_dir}")

    write_outputs(archives, before, args)


if __name__ == "__main__":
    main()
```

Create `UNO/Tools/Accessories/items/GuildCloak/preview.py`:

```python
"""Write the Guild Cloak's review picture from the patched in-memory archives."""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # Tools/Accessories: the shared acclib package
from acclib.daformats import DatArchive
from acclib.review import review_sheet
from guildcloak_art import ICON_ID, SPRITE_ID


def write_previews(archives: dict[str, DatArchive], preview_dir: Path, data_dir: Path) -> None:
    preview_dir.mkdir(parents=True, exist_ok=True)
    review_sheet(archives, data_dir, "accessory", SPRITE_ID, ICON_ID, preview_dir / "review.png")
```

Create `UNO/Tools/Accessories/items/GuildCloak/README.md`:

```markdown
# Guild Cloak

An exact copy of the Black Cape of Romance (accessory sprite 127, icon 4266) under its own
numbers. The client paints each guild's approved design onto it as it draws it
(`Chaos.Client.Rendering/GuildCloakPainter.cs`); see
`Chaos.Client/docs/superpowers/specs/2026-09-25-guild-cloak-design.md`.

The sprite number is also `GuildCloakProtocol.SPRITE` in `Chaos-Server/Chaos.DarkAges`. Keep them
the same.

    python Tools/Accessories/items/GuildCloak/build_guildcloak.py          # dry run + preview/review.png
    python -m unittest Tools/Accessories/items/GuildCloak/test_guildcloak.py
```

- [ ] **Step 6: Run the tests and look at the review picture**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora
python -m unittest Tools/Accessories/items/GuildCloak/test_guildcloak.py
python Tools/Accessories/items/GuildCloak/build_guildcloak.py
```

Expected: `OK`, then `previews written to ...` and `dry run: archives untouched`. Open `Tools/Accessories/items/GuildCloak/preview/review.png` with the Read tool: it must look like the Black Cape (black cape, purple rune on the back).

- [ ] **Step 7: Register and number the item**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora
python Tools/Accessories/ids.py add guildCloak --name "Guild Cloak" --kind accessory --tool GuildCloak --template Equipment/Temuair/Accessories/guildCloak.json
python Tools/Accessories/ids.py assign guildCloak
python Tools/Accessories/ids.py
python -m unittest Tools/Accessories/test_items.py
```

Expected: `assign` prints sprite 328 and icon 5319 (or the Step 1 numbers); `ids.py` reports no errors for `guildCloak`; `test_items.py` prints `OK`. Check that `guildCloak.json` now has `"displaySprite": 328` and `"panelSprite": 5319`, and that `guildcloak_art.py` still says `SPRITE_ID = 328` and `ICON_ID = 5319`.

Do not run `deploy_batch.py`. Shipping the batch is the user's step after review (Task 10's handover list).

```json:metadata
{"files": ["Unora/Tools/Accessories/items/GuildCloak/guildcloak_art.py", "Unora/Tools/Accessories/items/GuildCloak/build_guildcloak.py", "Unora/Tools/Accessories/items/GuildCloak/preview.py", "Unora/Tools/Accessories/items/GuildCloak/test_guildcloak.py", "Unora/Tools/Accessories/items/GuildCloak/README.md", "Unora/Data/Configuration/Templates/Items/Equipment/Temuair/Accessories/guildCloak.json", "Unora/Tools/Accessories/registry.json"], "verifyCommand": "cd C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora && python -m unittest Tools/Accessories/items/GuildCloak/test_guildcloak.py Tools/Accessories/test_items.py", "acceptanceCriteria": ["ids.py reports no clashes; registry has guildCloak with sprite and icon", "every c and g sheet of 127 copied byte for byte for both bodies", "palette number and icon frame match the source", "test_guildcloak.py and test_items.py pass", "guildCloak.json has the assigned displaySprite and panelSprite"], "modelTier": "standard"}
```

---
### Task 2: Shared protocol — design type, numbers, enums, messages, converters

**Goal:** The design type and fixed numbers both sides share, and seven guild cloak messages that round-trip every field.

**Files:**
- Create: `SRV/Chaos.DarkAges/Definitions/GuildCloakProtocol.cs`
- Create: `SRV/Chaos.DarkAges/Definitions/GuildCloakDesign.cs`
- Modify: `SRV/Chaos.DarkAges/Definitions/Enums.cs` (append 5 enums at the end of the file)
- Modify: `SRV/Chaos.DarkAges/Definitions/CONSTANTS.cs` (`CLIENT_VERSION` + 1)
- Modify: `SRV/Chaos.Networking.Abstractions/Definitions/Enums.cs` (end of `ClientOpCode` and of `ServerOpCode`)
- Create: `SRV/Chaos.Networking/Entities/Server/GuildCloakEditorArgs.cs`, `GuildCloakLookArgs.cs`, `GuildCloakDesignArgs.cs`, `GuildCloakReviewEntry.cs`, `GuildCloakReviewListArgs.cs`
- Create: `SRV/Chaos.Networking/Entities/Client/GuildCloakEditorInteractionArgs.cs`, `GuildCloakDesignRequestArgs.cs`, `GuildCloakReviewInteractionArgs.cs`
- Create: `SRV/Chaos.Networking/Converters/GuildCloakDesignCodec.cs`
- Create: `SRV/Chaos.Networking/Converters/Server/GuildCloakEditorConverter.cs`, `GuildCloakLookConverter.cs`, `GuildCloakDesignConverter.cs`, `GuildCloakReviewListConverter.cs`
- Create: `SRV/Chaos.Networking/Converters/Client/GuildCloakEditorInteractionConverter.cs`, `GuildCloakDesignRequestConverter.cs`, `GuildCloakReviewInteractionConverter.cs`
- Test: `SRV/Tests/Chaos.Tests/Networking/GuildCloakPacketConverterTests.cs`, `SRV/Tests/Chaos.Tests/GuildCloak/GuildCloakDesignTests.cs`

**Acceptance Criteria:**
- [ ] All seven messages round-trip unchanged
- [ ] An unknown editor type, review-list type, editor action or review action throws `ArgumentOutOfRangeException`
- [ ] Opcodes 126, 127, 128 (client) and 130–133 (server) are each defined exactly once
- [ ] The default design is valid, has one color and fills every cell of all three grids with 1
- [ ] `IsValid` refuses 0 or 7 colors, a wrong grid size, and a color number above the color count
- [ ] `CLIENT_VERSION` is one higher than before, and both solutions build

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildCloakPacketConverterTests/*" --no-ansi` and the same with `GuildCloakDesignTests` → all pass

**Steps:**

- [ ] **Step 1: Check the opcodes are still free**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server
grep -n "= 12[6-9],\|= 13[0-3]," Chaos.Networking.Abstractions/Definitions/Enums.cs
```

Expected: only `AcceptConnection = 126`, `BugReportOpen = 127`, `StageLightingState = 128` and `StageLightingBoard = 129`, which are all in `ServerOpCode`. If a new value was claimed since this plan was written, take the next free values and keep the same order.

- [ ] **Step 2: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildCloak/GuildCloakDesignTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildCloak;

public sealed class GuildCloakDesignTests
{
    [Test]
    public void CreateDefault_is_valid_with_one_color_on_every_cell()
    {
        var design = GuildCloakDesign.CreateDefault();

        design.IsValid().Should().BeTrue();
        design.Colors.Should().Equal(new GuildCloakColor(40, 40, 48));
        design.Back.Should().HaveCount(GuildCloakProtocol.BACK_SIZE).And.OnlyContain(b => b == 1);
        design.Lining.Should().HaveCount(GuildCloakProtocol.LINING_SIZE).And.OnlyContain(b => b == 1);
        design.Collar.Should().HaveCount(GuildCloakProtocol.COLLAR_SIZE).And.OnlyContain(b => b == 1);
    }

    [Test]
    public void IsValid_refuses_no_colors_and_too_many_colors()
    {
        var none = GuildCloakDesign.CreateDefault() with { Colors = [] };
        var seven = GuildCloakDesign.CreateDefault() with { Colors = Enumerable.Repeat(new GuildCloakColor(1, 2, 3), 7).ToList() };

        none.IsValid().Should().BeFalse();
        seven.IsValid().Should().BeFalse();
    }

    [Test]
    public void IsValid_refuses_a_wrong_grid_size()
    {
        var design = GuildCloakDesign.CreateDefault() with { Collar = new byte[GuildCloakProtocol.COLLAR_SIZE - 1] };

        design.IsValid().Should().BeFalse();
    }

    [Test]
    public void IsValid_refuses_a_color_number_above_the_color_count()
    {
        var design = GuildCloakDesign.CreateDefault();
        design.Back[10] = 2;

        design.IsValid().Should().BeFalse();

        design.Colors.Add(new GuildCloakColor(200, 0, 0));
        design.IsValid().Should().BeTrue();
    }

    [Test]
    public void DeepCopy_shares_no_lists_or_grids()
    {
        var original = GuildCloakDesign.CreateDefault();
        var copy = original.DeepCopy();

        copy.Colors.Add(new GuildCloakColor(1, 1, 1));
        copy.Back[0] = 0;
        copy.Lining[0] = 0;
        copy.Collar[0] = 0;

        original.Colors.Should().HaveCount(1);
        original.Back[0].Should().Be(1);
        original.Lining[0].Should().Be(1);
        original.Collar[0].Should().Be(1);
    }
}
```

Create `SRV/Tests/Chaos.Tests/Networking/GuildCloakPacketConverterTests.cs`:

```csharp
#region
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Converters.Client;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;
using FluentAssertions;
#endregion

namespace Chaos.Tests.Networking;

public sealed class GuildCloakPacketConverterTests
{
    private static readonly Encoding Enc = Encoding.GetEncoding(949);

    private static T RoundTrip<T>(PacketConverterBase<T> converter, T original) where T: class, IPacketSerializable
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        converter.Serialize(ref writer, original);

        var bytes = writer.ToSpan()
                          .ToArray();

        var reader = new SpanReader(Enc, bytes);

        return converter.Deserialize(ref reader);
    }

    /// <summary>Deserializes a message whose first byte is <paramref name="first" /> and reports whether it threw.</summary>
    private static bool ThrowsOnFirstByte<T>(PacketConverterBase<T> converter, byte first) where T: class, IPacketSerializable
    {
        try
        {
            var reader = new SpanReader(Enc, [first, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
            converter.Deserialize(ref reader);

            return false;
        } catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    private static GuildCloakDesign Sample()
    {
        var design = GuildCloakDesign.CreateDefault();
        design.Colors.Add(new GuildCloakColor(225, 175, 45));
        design.Back[100] = 2;
        design.Lining[0] = 0;
        design.Collar[GuildCloakProtocol.COLLAR_SIZE - 1] = 2;

        return design;
    }

    [Test]
    public void Editor_open_round_trips_the_design()
    {
        var original = new GuildCloakEditorArgs
        {
            Type = GuildCloakEditorType.Open,
            Status = GuildCloakStatus.Rejected,
            RejectionReason = "Too close to another guild's colors.",
            Design = Sample()
        };

        RoundTrip(new GuildCloakEditorConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Editor_status_round_trips_without_a_design()
    {
        var original = new GuildCloakEditorArgs
        {
            Type = GuildCloakEditorType.Status,
            Status = GuildCloakStatus.Waiting
        };

        RoundTrip(new GuildCloakEditorConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Look_round_trips()
    {
        var original = new GuildCloakLookArgs
        {
            EntityId = 4_000_000_001,
            DesignId = 77
        };

        RoundTrip(new GuildCloakLookConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Design_round_trips()
    {
        var original = new GuildCloakDesignArgs
        {
            DesignId = 12,
            Design = Sample()
        };

        RoundTrip(new GuildCloakDesignConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Review_list_round_trips_two_entries()
    {
        var original = new GuildCloakReviewListArgs
        {
            Type = GuildCloakReviewListType.Update,
            Entries =
            [
                new GuildCloakReviewEntry
                {
                    SubmissionId = 3,
                    GuildName = "Shinebox",
                    LeaderName = "Stahli",
                    SubmittedAtUtc = new DateTime(2026, 9, 25, 10, 30, 0, DateTimeKind.Utc),
                    Design = Sample()
                },
                new GuildCloakReviewEntry
                {
                    SubmissionId = 9,
                    GuildName = "Moonlit",
                    LeaderName = "Iglis",
                    SubmittedAtUtc = new DateTime(2026, 9, 25, 11, 0, 0, DateTimeKind.Utc),
                    Design = GuildCloakDesign.CreateDefault()
                }
            ]
        };

        RoundTrip(new GuildCloakReviewListConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    [Arguments(GuildCloakEditorAction.SaveDraft)]
    [Arguments(GuildCloakEditorAction.Submit)]
    public void Editor_interaction_round_trips(GuildCloakEditorAction action)
    {
        var original = new GuildCloakEditorInteractionArgs
        {
            Action = action,
            Design = Sample()
        };

        RoundTrip(new GuildCloakEditorInteractionConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Design_request_round_trips()
    {
        var original = new GuildCloakDesignRequestArgs { DesignId = 41 };

        RoundTrip(new GuildCloakDesignRequestConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    [Arguments(GuildCloakReviewAction.Approve, "")]
    [Arguments(GuildCloakReviewAction.Reject, "The emblem looks like a rude word.")]
    public void Review_interaction_round_trips(GuildCloakReviewAction action, string reason)
    {
        var original = new GuildCloakReviewInteractionArgs
        {
            Action = action,
            SubmissionId = 5,
            Reason = reason
        };

        RoundTrip(new GuildCloakReviewInteractionConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void Unknown_types_and_actions_throw()
    {
        ThrowsOnFirstByte(new GuildCloakEditorConverter(), 9).Should().BeTrue();
        ThrowsOnFirstByte(new GuildCloakReviewListConverter(), 9).Should().BeTrue();
        ThrowsOnFirstByte(new GuildCloakEditorInteractionConverter(), 9).Should().BeTrue();
        ThrowsOnFirstByte(new GuildCloakReviewInteractionConverter(), 9).Should().BeTrue();
    }

    [Test]
    public void Opcodes_are_defined_once()
    {
        foreach (var value in new byte[] { 126, 127, 128 })
            Enum.GetValues<ClientOpCode>().Count(code => (byte)code == value).Should().Be(1, $"client opcode {value}");

        foreach (var value in new byte[] { 130, 131, 132, 133 })
            Enum.GetValues<ServerOpCode>().Count(code => (byte)code == value).Should().Be(1, $"server opcode {value}");
    }
}
```

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildCloakPacketConverterTests/*" --no-ansi`
Expected: build FAILS with `The type or namespace name 'GuildCloakDesign' could not be found`.

- [ ] **Step 3: Write the shared numbers and the design type**

Create `SRV/Chaos.DarkAges/Definitions/GuildCloakProtocol.cs`:

```csharp
namespace Chaos.DarkAges.Definitions;

/// <summary>
///     Fixed numbers for guild cloaks, shared by the server and the client. The grid sizes are the frame sizes of the guild
///     cloak's male walk sheet (<c>01</c>): back = frame 0 of the <c>c</c> layer, lining = frame 5 of the <c>g</c> layer,
///     collar = frame 5 of the <c>c</c> layer.
/// </summary>
public static class GuildCloakProtocol
{
    /// <summary>The Guild Cloak's accessory sprite: an exact copy of sprite 127, the Black Cape of Romance.</summary>
    public const int SPRITE = 328;

    public const int BACK_WIDTH = 27;
    public const int BACK_HEIGHT = 41;
    public const int BACK_SIZE = BACK_WIDTH * BACK_HEIGHT;

    public const int LINING_WIDTH = 27;
    public const int LINING_HEIGHT = 38;
    public const int LINING_SIZE = LINING_WIDTH * LINING_HEIGHT;

    public const int COLLAR_WIDTH = 18;
    public const int COLLAR_HEIGHT = 8;
    public const int COLLAR_SIZE = COLLAR_WIDTH * COLLAR_HEIGHT;

    public const int MAX_COLORS = 6;
    public const int MAX_REASON_CHARS = 200;

    /// <summary>The most waiting designs one review list carries. 20 designs stay well under the 64 KB packet limit.</summary>
    public const int MAX_REVIEW_ENTRIES = 20;

    /// <summary>What Quill charges for one Guild Cloak.</summary>
    public const int CLOAK_PRICE = 50_000;

    /// <summary>The Guild Cloak item's template key.</summary>
    public const string CLOAK_ITEM_KEY = "guildCloak";

    /// <summary>The guild hall property that records the cloak deed.</summary>
    public const string DEED_PROPERTY = "cloaks";

    /// <summary>The shortest gap between two saves or submits from one player.</summary>
    public static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(2);
}
```

Create `SRV/Chaos.DarkAges/Definitions/GuildCloakDesign.cs`:

```csharp
namespace Chaos.DarkAges.Definitions;

/// <summary>One color of a guild cloak design.</summary>
public readonly record struct GuildCloakColor(byte R, byte G, byte B);

/// <summary>
///     A painted guild cloak: 1 to <see cref="GuildCloakProtocol.MAX_COLORS" /> colors and three grids of color numbers,
///     row-major, one byte per cell. <c>0</c> is unpainted; <c>1</c> up to the color count picks a color. The grid sizes
///     are in <see cref="GuildCloakProtocol" />.
/// </summary>
public sealed record GuildCloakDesign
{
    public List<GuildCloakColor> Colors { get; set; } = [];
    public byte[] Back { get; set; } = [];
    public byte[] Lining { get; set; } = [];
    public byte[] Collar { get; set; } = [];

    /// <summary>The editor's starting design: one dark color on every cell.</summary>
    public static GuildCloakDesign CreateDefault()
        => new()
        {
            Colors = [new GuildCloakColor(40, 40, 48)],
            Back = Filled(GuildCloakProtocol.BACK_SIZE),
            Lining = Filled(GuildCloakProtocol.LINING_SIZE),
            Collar = Filled(GuildCloakProtocol.COLLAR_SIZE)
        };

    public GuildCloakDesign DeepCopy()
        => new()
        {
            Colors = [..Colors],
            Back = (byte[])Back.Clone(),
            Lining = (byte[])Lining.Clone(),
            Collar = (byte[])Collar.Clone()
        };

    /// <summary>True when the color count, all three grid sizes and every color number are in range.</summary>
    public bool IsValid()
    {
        if (Colors is null || (Colors.Count < 1) || (Colors.Count > GuildCloakProtocol.MAX_COLORS))
            return false;

        return GridIsValid(Back, GuildCloakProtocol.BACK_SIZE, Colors.Count)
               && GridIsValid(Lining, GuildCloakProtocol.LINING_SIZE, Colors.Count)
               && GridIsValid(Collar, GuildCloakProtocol.COLLAR_SIZE, Colors.Count);
    }

    private static byte[] Filled(int size)
    {
        var grid = new byte[size];
        Array.Fill(grid, (byte)1);

        return grid;
    }

    private static bool GridIsValid(byte[]? grid, int size, int colorCount)
    {
        if (grid is null || (grid.Length != size))
            return false;

        foreach (var value in grid)
            if (value > colorCount)
                return false;

        return true;
    }
}
```

- [ ] **Step 4: Add the enums and bump the client version**

Append to the end of `SRV/Chaos.DarkAges/Definitions/Enums.cs`:

```csharp

/// <summary>Where a guild's cloak design stands, as the editor's status line shows it.</summary>
public enum GuildCloakStatus : byte
{
    /// <summary>Nothing is waiting and nothing has been decided yet.</summary>
    Draft = 0,

    /// <summary>A design is waiting for an admin.</summary>
    Waiting = 1,

    /// <summary>The newest decision was an approval.</summary>
    Approved = 2,

    /// <summary>The newest decision was a rejection. The editor shows the reason.</summary>
    Rejected = 3
}

/// <summary>What a <c>GuildCloakEditor</c> message does.</summary>
public enum GuildCloakEditorType : byte
{
    /// <summary>Load the design and show the editor.</summary>
    Open = 0,

    /// <summary>A save or submit went through: update the status line only.</summary>
    Status = 1
}

/// <summary>A leader's request from the guild cloak editor.</summary>
public enum GuildCloakEditorAction : byte
{
    SaveDraft = 0,
    Submit = 1
}

/// <summary>What a <c>GuildCloakReviewList</c> message does.</summary>
public enum GuildCloakReviewListType : byte
{
    /// <summary>Show the review window with this list.</summary>
    Open = 0,

    /// <summary>Replace the list if the window is open.</summary>
    Update = 1
}

/// <summary>An admin's decision in the guild cloak review window.</summary>
public enum GuildCloakReviewAction : byte
{
    Approve = 0,
    Reject = 1
}
```

In `SRV/Chaos.DarkAges/Definitions/CONSTANTS.cs`, raise `CLIENT_VERSION` by one (753 → 754 when this plan was written). New clients and the server must agree on the new messages, so older clients are refused at login.

- [ ] **Step 5: Add the opcodes**

In `SRV/Chaos.Networking.Abstractions/Definitions/Enums.cs`, after `StageLightingInteraction = 125,` at the end of `ClientOpCode`:

```csharp

    /// <summary>
    ///     A save or submit from the guild cloak editor. The server checks the leader rank, the deed, the rate and the design.
    ///     <br />
    ///     Hex value: 0x7E
    /// </summary>
    GuildCloakEditorInteraction = 126,

    /// <summary>
    ///     Asks for one approved guild cloak design by its id, after a <see cref="ServerOpCode.GuildCloakLook" /> named it.
    ///     <br />
    ///     Hex value: 0x7F
    /// </summary>
    GuildCloakDesignRequest = 127,

    /// <summary>
    ///     An admin's approve or reject from the guild cloak review window. The server checks that the sender is an admin.
    ///     <br />
    ///     Hex value: 0x80
    /// </summary>
    GuildCloakReviewInteraction = 128,
```

After `StageLightingBoard = 129,` at the end of `ServerOpCode`:

```csharp

    /// <summary>
    ///     Opens the guild cloak editor for the leader, or updates its status line after a save or submit.
    ///     <br />
    ///     Hex value: 0x82
    /// </summary>
    GuildCloakEditor = 130,

    /// <summary>
    ///     Which design a player's guild cloak shows (0 = plain). Sent after <see cref="DisplayAisling" /> for every player
    ///     who wears a guild cloak.
    ///     <br />
    ///     Hex value: 0x83
    /// </summary>
    GuildCloakLook = 131,

    /// <summary>
    ///     One approved guild cloak design, in reply to <see cref="ClientOpCode.GuildCloakDesignRequest" />.
    ///     <br />
    ///     Hex value: 0x84
    /// </summary>
    GuildCloakDesign = 132,

    /// <summary>
    ///     The designs waiting for review. Opens the admin's review window or updates it.
    ///     <br />
    ///     Hex value: 0x85
    /// </summary>
    GuildCloakReviewList = 133,
```

- [ ] **Step 6: Write the message types**

Create `SRV/Chaos.Networking/Entities/Server/GuildCloakEditorArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.GuildCloakEditor" /> packet. Open carries the design to
///     edit; Status carries only the status line.
/// </summary>
public sealed record GuildCloakEditorArgs : IPacketSerializable
{
    public required GuildCloakEditorType Type { get; set; }
    public GuildCloakStatus Status { get; set; }

    /// <summary>Rejected only: the admin's reason.</summary>
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>Open only: the draft, else the approved design, else the default design.</summary>
    public GuildCloakDesign Design { get; set; } = new();
}
```

Create `SRV/Chaos.Networking/Entities/Server/GuildCloakLookArgs.cs`:

```csharp
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.GuildCloakLook" /> packet: which design one player's guild
///     cloak shows. 0 means the plain cloak.
/// </summary>
public sealed record GuildCloakLookArgs : IPacketSerializable
{
    public uint EntityId { get; set; }
    public int DesignId { get; set; }
}
```

Create `SRV/Chaos.Networking/Entities/Server/GuildCloakDesignArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>Represents the serialization of the <see cref="ServerOpCode.GuildCloakDesign" /> packet: one approved design.</summary>
public sealed record GuildCloakDesignArgs : IPacketSerializable
{
    public int DesignId { get; set; }
    public GuildCloakDesign Design { get; set; } = new();
}
```

Create `SRV/Chaos.Networking/Entities/Server/GuildCloakReviewEntry.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Networking.Entities.Server;

/// <summary>One design waiting for review, as the review window lists it.</summary>
public sealed record GuildCloakReviewEntry
{
    public int SubmissionId { get; set; }
    public string GuildName { get; set; } = string.Empty;
    public string LeaderName { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public GuildCloakDesign Design { get; set; } = new();
}
```

Create `SRV/Chaos.Networking/Entities/Server/GuildCloakReviewListArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.GuildCloakReviewList" /> packet: up to
///     <see cref="GuildCloakProtocol.MAX_REVIEW_ENTRIES" /> waiting designs, oldest first.
/// </summary>
public sealed record GuildCloakReviewListArgs : IPacketSerializable
{
    public required GuildCloakReviewListType Type { get; set; }
    public IReadOnlyList<GuildCloakReviewEntry> Entries { get; set; } = [];
}
```

Create `SRV/Chaos.Networking/Entities/Client/GuildCloakEditorInteractionArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>
///     Represents the serialization of the <see cref="ClientOpCode.GuildCloakEditorInteraction" /> packet: a save or a submit
///     with the whole design. The server checks the sender and the design.
/// </summary>
public sealed record GuildCloakEditorInteractionArgs : IPacketSerializable
{
    public required GuildCloakEditorAction Action { get; set; }
    public GuildCloakDesign Design { get; set; } = new();
}
```

Create `SRV/Chaos.Networking/Entities/Client/GuildCloakDesignRequestArgs.cs`:

```csharp
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>Represents the serialization of the <see cref="ClientOpCode.GuildCloakDesignRequest" /> packet.</summary>
public sealed record GuildCloakDesignRequestArgs : IPacketSerializable
{
    public int DesignId { get; set; }
}
```

Create `SRV/Chaos.Networking/Entities/Client/GuildCloakReviewInteractionArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>
///     Represents the serialization of the <see cref="ClientOpCode.GuildCloakReviewInteraction" /> packet. The decision names
///     the exact submission it is about; a reject carries the reason.
/// </summary>
public sealed record GuildCloakReviewInteractionArgs : IPacketSerializable
{
    public required GuildCloakReviewAction Action { get; set; }
    public int SubmissionId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
```

- [ ] **Step 7: Write the codec and the converters**

Create `SRV/Chaos.Networking/Converters/GuildCloakDesignCodec.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;

namespace Chaos.Networking.Converters;

/// <summary>
///     Reads and writes one <see cref="GuildCloakDesign" />: a color count, 3 bytes per color, then the back, lining and
///     collar grids, each with a 16-bit length. Values are not checked here; the server calls
///     <see cref="GuildCloakDesign.IsValid" />.
/// </summary>
internal static class GuildCloakDesignCodec
{
    public static GuildCloakDesign Read(ref SpanReader reader)
    {
        var count = reader.ReadByte();
        var colors = new List<GuildCloakColor>(count);

        for (var i = 0; i < count; i++)
        {
            var r = reader.ReadByte();
            var g = reader.ReadByte();
            var b = reader.ReadByte();
            colors.Add(new GuildCloakColor(r, g, b));
        }

        return new GuildCloakDesign
        {
            Colors = colors,
            Back = reader.ReadData16(),
            Lining = reader.ReadData16(),
            Collar = reader.ReadData16()
        };
    }

    public static void Write(ref SpanWriter writer, GuildCloakDesign design)
    {
        var colors = design.Colors ?? [];

        if (colors.Count > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(design), colors.Count, "Too many guild cloak colors");

        writer.WriteByte((byte)colors.Count);

        foreach (var color in colors)
        {
            writer.WriteByte(color.R);
            writer.WriteByte(color.G);
            writer.WriteByte(color.B);
        }

        writer.WriteData16(design.Back ?? []);
        writer.WriteData16(design.Lining ?? []);
        writer.WriteData16(design.Collar ?? []);
    }
}
```

Create `SRV/Chaos.Networking/Converters/Server/GuildCloakEditorConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildCloakEditorArgs" /></summary>
public sealed class GuildCloakEditorConverter : PacketConverterBase<GuildCloakEditorArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.GuildCloakEditor;

    /// <inheritdoc />
    public override GuildCloakEditorArgs Deserialize(ref SpanReader reader)
    {
        var type = (GuildCloakEditorType)reader.ReadByte();

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown guild cloak editor type");

        var args = new GuildCloakEditorArgs
        {
            Type = type,
            Status = (GuildCloakStatus)reader.ReadByte(),
            RejectionReason = reader.ReadString16()
        };

        if (type == GuildCloakEditorType.Open)
            args.Design = GuildCloakDesignCodec.Read(ref reader);

        return args;
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildCloakEditorArgs args)
    {
        writer.WriteByte((byte)args.Type);
        writer.WriteByte((byte)args.Status);
        writer.WriteString16(args.RejectionReason ?? string.Empty);

        if (args.Type == GuildCloakEditorType.Open)
            GuildCloakDesignCodec.Write(ref writer, args.Design ?? new GuildCloakDesign());
    }
}
```

Create `SRV/Chaos.Networking/Converters/Server/GuildCloakLookConverter.cs`:

```csharp
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildCloakLookArgs" /></summary>
public sealed class GuildCloakLookConverter : PacketConverterBase<GuildCloakLookArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.GuildCloakLook;

    /// <inheritdoc />
    public override GuildCloakLookArgs Deserialize(ref SpanReader reader)
        => new()
        {
            EntityId = reader.ReadUInt32(),
            DesignId = reader.ReadInt32()
        };

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildCloakLookArgs args)
    {
        writer.WriteUInt32(args.EntityId);
        writer.WriteInt32(args.DesignId);
    }
}
```

Create `SRV/Chaos.Networking/Converters/Server/GuildCloakDesignConverter.cs`:

```csharp
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildCloakDesignArgs" /></summary>
public sealed class GuildCloakDesignConverter : PacketConverterBase<GuildCloakDesignArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.GuildCloakDesign;

    /// <inheritdoc />
    public override GuildCloakDesignArgs Deserialize(ref SpanReader reader)
    {
        var designId = reader.ReadInt32();

        return new GuildCloakDesignArgs
        {
            DesignId = designId,
            Design = GuildCloakDesignCodec.Read(ref reader)
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildCloakDesignArgs args)
    {
        writer.WriteInt32(args.DesignId);
        GuildCloakDesignCodec.Write(ref writer, args.Design ?? new());
    }
}
```

Create `SRV/Chaos.Networking/Converters/Server/GuildCloakReviewListConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildCloakReviewListArgs" /></summary>
public sealed class GuildCloakReviewListConverter : PacketConverterBase<GuildCloakReviewListArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.GuildCloakReviewList;

    /// <inheritdoc />
    public override GuildCloakReviewListArgs Deserialize(ref SpanReader reader)
    {
        var type = (GuildCloakReviewListType)reader.ReadByte();

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown guild cloak review list type");

        var count = reader.ReadByte();
        var entries = new List<GuildCloakReviewEntry>(count);

        for (var i = 0; i < count; i++)
        {
            var submissionId = reader.ReadInt32();
            var guildName = reader.ReadString8();
            var leaderName = reader.ReadString8();
            var ticks = (long)reader.ReadUInt64();

            entries.Add(
                new GuildCloakReviewEntry
                {
                    SubmissionId = submissionId,
                    GuildName = guildName,
                    LeaderName = leaderName,
                    SubmittedAtUtc = new DateTime(ticks, DateTimeKind.Utc),
                    Design = GuildCloakDesignCodec.Read(ref reader)
                });
        }

        return new GuildCloakReviewListArgs
        {
            Type = type,
            Entries = entries
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildCloakReviewListArgs args)
    {
        var entries = args.Entries ?? [];

        if (entries.Count > GuildCloakProtocol.MAX_REVIEW_ENTRIES)
            throw new ArgumentOutOfRangeException(nameof(args), entries.Count, "Too many guild cloak review entries");

        writer.WriteByte((byte)args.Type);
        writer.WriteByte((byte)entries.Count);

        foreach (var entry in entries)
        {
            writer.WriteInt32(entry.SubmissionId);
            writer.WriteString8(entry.GuildName ?? string.Empty);
            writer.WriteString8(entry.LeaderName ?? string.Empty);
            writer.WriteUInt64((ulong)entry.SubmittedAtUtc.Ticks);
            GuildCloakDesignCodec.Write(ref writer, entry.Design ?? new());
        }
    }
}
```

Create `SRV/Chaos.Networking/Converters/Client/GuildCloakEditorInteractionConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildCloakEditorInteractionArgs" /></summary>
public sealed class GuildCloakEditorInteractionConverter : PacketConverterBase<GuildCloakEditorInteractionArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.GuildCloakEditorInteraction;

    /// <inheritdoc />
    public override GuildCloakEditorInteractionArgs Deserialize(ref SpanReader reader)
    {
        var action = (GuildCloakEditorAction)reader.ReadByte();

        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(nameof(reader), action, "Unknown guild cloak editor action");

        return new GuildCloakEditorInteractionArgs
        {
            Action = action,
            Design = GuildCloakDesignCodec.Read(ref reader)
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildCloakEditorInteractionArgs args)
    {
        writer.WriteByte((byte)args.Action);
        GuildCloakDesignCodec.Write(ref writer, args.Design ?? new());
    }
}
```

Create `SRV/Chaos.Networking/Converters/Client/GuildCloakDesignRequestConverter.cs`:

```csharp
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildCloakDesignRequestArgs" /></summary>
public sealed class GuildCloakDesignRequestConverter : PacketConverterBase<GuildCloakDesignRequestArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.GuildCloakDesignRequest;

    /// <inheritdoc />
    public override GuildCloakDesignRequestArgs Deserialize(ref SpanReader reader) => new() { DesignId = reader.ReadInt32() };

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildCloakDesignRequestArgs args) => writer.WriteInt32(args.DesignId);
}
```

Create `SRV/Chaos.Networking/Converters/Client/GuildCloakReviewInteractionConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="GuildCloakReviewInteractionArgs" /></summary>
public sealed class GuildCloakReviewInteractionConverter : PacketConverterBase<GuildCloakReviewInteractionArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.GuildCloakReviewInteraction;

    /// <inheritdoc />
    public override GuildCloakReviewInteractionArgs Deserialize(ref SpanReader reader)
    {
        var action = (GuildCloakReviewAction)reader.ReadByte();

        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(nameof(reader), action, "Unknown guild cloak review action");

        return new GuildCloakReviewInteractionArgs
        {
            Action = action,
            SubmissionId = reader.ReadInt32(),
            Reason = reader.ReadString16()
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, GuildCloakReviewInteractionArgs args)
    {
        writer.WriteByte((byte)args.Action);
        writer.WriteInt32(args.SubmissionId);
        writer.WriteString16(args.Reason ?? string.Empty);
    }
}
```

Converters are found by reflection (`AddPacketSerializer` loads every `IPacketConverter` in the assembly). No registration is needed.

- [ ] **Step 8: Run the tests**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildCloakPacketConverterTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildCloakDesignTests/*" --no-ansi
```

Expected: all pass. If `ThrowsOnFirstByte` for the editor converter throws a different exception type, check the converter checks `Enum.IsDefined` before reading anything else.

- [ ] **Step 9: Build the client against the server worktree**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server
```

Expected: `Build succeeded` (the client only picks up the new version number here).

```json:metadata
{"files": ["Chaos-Server/Chaos.DarkAges/Definitions/GuildCloakProtocol.cs", "Chaos-Server/Chaos.DarkAges/Definitions/GuildCloakDesign.cs", "Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs", "Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos-Server/Chaos.Networking/Entities/Server/GuildCloakEditorArgs.cs", "Chaos-Server/Chaos.Networking/Entities/Server/GuildCloakLookArgs.cs", "Chaos-Server/Chaos.Networking/Entities/Server/GuildCloakDesignArgs.cs", "Chaos-Server/Chaos.Networking/Entities/Server/GuildCloakReviewEntry.cs", "Chaos-Server/Chaos.Networking/Entities/Server/GuildCloakReviewListArgs.cs", "Chaos-Server/Chaos.Networking/Entities/Client/GuildCloakEditorInteractionArgs.cs", "Chaos-Server/Chaos.Networking/Entities/Client/GuildCloakDesignRequestArgs.cs", "Chaos-Server/Chaos.Networking/Entities/Client/GuildCloakReviewInteractionArgs.cs", "Chaos-Server/Chaos.Networking/Converters/GuildCloakDesignCodec.cs", "Chaos-Server/Chaos.Networking/Converters/Server/GuildCloakEditorConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Server/GuildCloakLookConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Server/GuildCloakDesignConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Server/GuildCloakReviewListConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Client/GuildCloakEditorInteractionConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Client/GuildCloakDesignRequestConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Client/GuildCloakReviewInteractionConverter.cs", "Chaos-Server/Tests/Chaos.Tests/Networking/GuildCloakPacketConverterTests.cs", "Chaos-Server/Tests/Chaos.Tests/GuildCloak/GuildCloakDesignTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/GuildCloakPacketConverterTests/*\" --no-ansi", "acceptanceCriteria": ["all seven messages round-trip unchanged", "unknown types and actions throw ArgumentOutOfRangeException", "opcodes 126-128 and 130-133 defined once", "default design valid, one color, every cell 1", "IsValid refuses 0 or 7 colors, wrong grid size, color number above count", "CLIENT_VERSION raised by one; both solutions build"], "modelTier": "mechanical"}
```

---

### Task 3: Stored designs — `GuildCloakState`

**Goal:** Every guild's approved, waiting and draft designs plus the last rejection, with the id counter, the editor view and the review list, saved as JSON through `IStorage<GuildCloakState>`.

**Files:**
- Create: `SRV/Chaos/Models/World/GuildCloakState.cs`
- Modify: `SRV/Chaos/SerializationContext.cs` (two `JsonSerializable` lines next to `GuildHouseState`'s)
- Test: `SRV/Tests/Chaos.Tests/GuildCloak/GuildCloakStateTests.cs`

**Acceptance Criteria:**
- [ ] A new guild's editor view is the default design with status Draft
- [ ] Save, submit, approve, reject, clear and remove change the state as the spec says; stored designs are copies
- [ ] Submission ids start at 1 and never repeat; a decision on a replaced submission is refused
- [ ] The status is Waiting while a design waits, else Rejected or Approved by the newest decision, else Draft
- [ ] The waiting list is oldest first and capped
- [ ] After a real JSON round trip and `EnsureCaseInsensitive`, guild lookups ignore case

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildCloakStateTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildCloak/GuildCloakStateTests.cs`:

```csharp
#region
using System.Text.Json;
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using FluentAssertions;
#endregion

namespace Chaos.Tests.GuildCloak;

public sealed class GuildCloakStateTests
{
    private const string GUILD = "Shinebox";
    private static readonly DateTime T0 = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private static GuildCloakDesign Painted(byte colorOnBack = 2)
    {
        var design = GuildCloakDesign.CreateDefault();
        design.Colors.Add(new GuildCloakColor(225, 175, 45));
        design.Back[50] = colorOnBack;

        return design;
    }

    [Test]
    public void EditorView_of_a_new_guild_is_the_default_design_as_a_draft()
    {
        var view = new GuildCloakState().EditorView(GUILD);

        view.Design.Should().BeEquivalentTo(GuildCloakDesign.CreateDefault());
        view.Status.Should().Be(GuildCloakStatus.Draft);
        view.Reason.Should().BeEmpty();
    }

    [Test]
    public void SaveDraft_stores_a_copy_that_the_editor_opens()
    {
        var state = new GuildCloakState();
        var design = Painted();

        state.SaveDraft(GUILD, design);
        design.Back[50] = 1;

        state.EditorView(GUILD).Design.Back[50].Should().Be(2);
    }

    [Test]
    public void Submit_gives_increasing_ids_and_sets_waiting_and_the_draft()
    {
        var state = new GuildCloakState();

        state.Submit(GUILD, "Stahli", Painted(), T0).Should().Be(1);
        state.Submit("Moonlit", "Iglis", Painted(), T0).Should().Be(2);

        var view = state.EditorView(GUILD);
        view.Status.Should().Be(GuildCloakStatus.Waiting);
        view.Design.Back[50].Should().Be(2);
    }

    [Test]
    public void TryApprove_makes_the_waiting_design_the_approved_one()
    {
        var state = new GuildCloakState();
        var id = state.Submit(GUILD, "Stahli", Painted(), T0);

        state.TryApprove(id, "Admin", T0.AddMinutes(5), out var guildName).Should().BeTrue();

        guildName.Should().Be(GUILD);
        state.ApprovedIdOf(GUILD).Should().Be(id);
        state.FindApproved(id)!.Back[50].Should().Be(2);
        state.EditorView(GUILD).Status.Should().Be(GuildCloakStatus.Approved);
        state.WaitingList(20).Should().BeEmpty();
    }

    [Test]
    public void A_resubmit_replaces_the_waiting_design_and_the_old_id_is_refused()
    {
        var state = new GuildCloakState();
        var first = state.Submit(GUILD, "Stahli", Painted(), T0);
        var second = state.Submit(GUILD, "Stahli", Painted(colorOnBack: 1), T0.AddMinutes(1));

        state.TryApprove(first, "Admin", T0, out _).Should().BeFalse();
        state.TryReject(first, "Admin", "old", T0, out _).Should().BeFalse();
        state.TryApprove(second, "Admin", T0, out _).Should().BeTrue();
        state.ApprovedIdOf(GUILD).Should().Be(second);
    }

    [Test]
    public void TryReject_keeps_the_approved_design_and_shows_the_reason()
    {
        var state = new GuildCloakState();
        var approved = state.Submit(GUILD, "Stahli", Painted(), T0);
        state.TryApprove(approved, "Admin", T0.AddMinutes(1), out _);
        var rejected = state.Submit(GUILD, "Stahli", Painted(colorOnBack: 1), T0.AddMinutes(2));

        state.TryReject(rejected, "Admin", "Too bright.", T0.AddMinutes(3), out var guildName).Should().BeTrue();

        guildName.Should().Be(GUILD);
        state.ApprovedIdOf(GUILD).Should().Be(approved);
        var view = state.EditorView(GUILD);
        view.Status.Should().Be(GuildCloakStatus.Rejected);
        view.Reason.Should().Be("Too bright.");
    }

    [Test]
    public void An_approval_after_a_rejection_shows_approved()
    {
        var state = new GuildCloakState();
        var rejected = state.Submit(GUILD, "Stahli", Painted(), T0);
        state.TryReject(rejected, "Admin", "No.", T0.AddMinutes(1), out _);
        var approved = state.Submit(GUILD, "Stahli", Painted(), T0.AddMinutes(2));
        state.TryApprove(approved, "Admin", T0.AddMinutes(3), out _);

        state.EditorView(GUILD).Status.Should().Be(GuildCloakStatus.Approved);
    }

    [Test]
    public void ClearApproved_returns_the_look_to_plain()
    {
        var state = new GuildCloakState();
        state.ClearApproved(GUILD).Should().BeFalse();

        var id = state.Submit(GUILD, "Stahli", Painted(), T0);
        state.TryApprove(id, "Admin", T0, out _);

        state.ClearApproved(GUILD).Should().BeTrue();
        state.ApprovedIdOf(GUILD).Should().Be(0);
        state.FindApproved(id).Should().BeNull();
    }

    [Test]
    public void RemoveGuild_forgets_everything()
    {
        var state = new GuildCloakState();
        var id = state.Submit(GUILD, "Stahli", Painted(), T0);
        state.TryApprove(id, "Admin", T0, out _);

        state.RemoveGuild(GUILD);

        state.ApprovedIdOf(GUILD).Should().Be(0);
        state.EditorView(GUILD).Status.Should().Be(GuildCloakStatus.Draft);
    }

    [Test]
    public void ApprovedIdOf_no_guild_is_zero()
        => new GuildCloakState().ApprovedIdOf(null).Should().Be(0);

    [Test]
    public void WaitingList_is_oldest_first_and_capped()
    {
        var state = new GuildCloakState();
        state.Submit("Late", "A", Painted(), T0.AddMinutes(9));
        state.Submit("Early", "B", Painted(), T0);
        state.Submit("Middle", "C", Painted(), T0.AddMinutes(4));

        state.WaitingList(2).Select(w => w.GuildName).Should().Equal("Early", "Middle");
    }

    [Test]
    public void Guild_lookups_ignore_case_after_a_real_serializer_round_trip()
    {
        var state = new GuildCloakState();
        var id = state.Submit(GUILD, "Stahli", Painted(), T0);
        state.TryApprove(id, "Admin", T0, out _);

        var json = JsonSerializer.Serialize(state, SerializationContext.Default.GuildCloakState);
        var restored = JsonSerializer.Deserialize(json, SerializationContext.Default.GuildCloakState)!;
        restored.EnsureCaseInsensitive();

        restored.ApprovedIdOf("SHINEBOX").Should().Be(id);
        restored.NextDesignId.Should().Be(2);
        restored.FindApproved(id).Should().BeEquivalentTo(Painted());
    }
}
```

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildCloakStateTests/*" --no-ansi`
Expected: build FAILS with `The type or namespace name 'GuildCloakState' could not be found`.

- [ ] **Step 2: Write `GuildCloakState`**

Create `SRV/Chaos/Models/World/GuildCloakState.cs`:

```csharp
#region
using System.Diagnostics.CodeAnalysis;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Models.World;

/// <summary>
///     Every guild's cloak designs: the approved one, the one waiting for review, the leader's draft, and the last rejection.
///     Saved as <c>GuildCloakState.json</c> through <c>IStorage&lt;GuildCloakState&gt;</c>. Not thread safe:
///     <c>GuildCloakService</c> locks around every call and saves afterwards.
/// </summary>
public sealed class GuildCloakState
{
    /// <summary>The id the next submission gets. Ids are never reused, so a changed design always has a new id.</summary>
    public int NextDesignId { get; set; } = 1;

    public Dictionary<string, GuildCloakRecord> Guilds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int ApprovedIdOf(string? guildName)
        => (guildName is not null) && Guilds.TryGetValue(guildName, out var record) && record.Approved is not null
            ? record.Approved.Id
            : 0;

    public bool ClearApproved(string guildName)
    {
        if (!Guilds.TryGetValue(guildName, out var record) || record.Approved is null)
            return false;

        record.Approved = null;

        return true;
    }

    /// <summary>
    ///     What the editor opens with: the draft, else the approved design, else the default design; and the status line.
    ///     Waiting wins; otherwise the newer of the last approval and the last rejection decides.
    /// </summary>
    public (GuildCloakDesign Design, GuildCloakStatus Status, string Reason) EditorView(string guildName)
    {
        Guilds.TryGetValue(guildName, out var record);

        var design = (record?.Draft ?? record?.Approved?.Design ?? GuildCloakDesign.CreateDefault()).DeepCopy();

        if (record is null)
            return (design, GuildCloakStatus.Draft, string.Empty);

        if (record.Waiting is not null)
            return (design, GuildCloakStatus.Waiting, string.Empty);

        if (record.LastRejection is not null
            && ((record.Approved is null) || (record.LastRejection.RejectedAtUtc > record.Approved.DecidedAtUtc)))
            return (design, GuildCloakStatus.Rejected, record.LastRejection.Reason);

        return (design, record.Approved is not null ? GuildCloakStatus.Approved : GuildCloakStatus.Draft, string.Empty);
    }

    /// <summary>
    ///     System.Text.Json replaces <see cref="Guilds" /> on load with a case-sensitive dictionary. Call once after loading.
    /// </summary>
    public void EnsureCaseInsensitive()
        => Guilds = new Dictionary<string, GuildCloakRecord>(
            Guilds ?? new Dictionary<string, GuildCloakRecord>(),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>An approved design by its id, or null. Only approved designs are ever sent to other players.</summary>
    public GuildCloakDesign? FindApproved(int designId)
    {
        foreach (var record in Guilds.Values)
            if (record.Approved?.Id == designId)
                return record.Approved.Design;

        return null;
    }

    public void RemoveGuild(string guildName) => Guilds.Remove(guildName);

    public void SaveDraft(string guildName, GuildCloakDesign design) => GetOrAdd(guildName).Draft = design.DeepCopy();

    /// <summary>
    ///     Makes <paramref name="design" /> the guild's waiting design, replacing any, and its draft. Returns the new
    ///     submission id.
    /// </summary>
    public int Submit(string guildName, string leaderName, GuildCloakDesign design, DateTime nowUtc)
    {
        var record = GetOrAdd(guildName);
        var id = NextDesignId++;

        record.Waiting = new GuildCloakEntry
        {
            Id = id,
            Design = design.DeepCopy(),
            SubmittedBy = leaderName,
            SubmittedAtUtc = nowUtc
        };

        record.Draft = design.DeepCopy();

        return id;
    }

    /// <summary>Approves the waiting design with this exact id. False when no guild's waiting design has it.</summary>
    public bool TryApprove(int submissionId, string adminName, DateTime nowUtc, [MaybeNullWhen(false)] out string guildName)
    {
        if (!TryFindWaiting(submissionId, out guildName, out var record))
            return false;

        var entry = record.Waiting!;
        entry.DecidedBy = adminName;
        entry.DecidedAtUtc = nowUtc;
        record.Approved = entry;
        record.Waiting = null;

        return true;
    }

    /// <summary>Rejects the waiting design with this exact id. False when no guild's waiting design has it.</summary>
    public bool TryReject(
        int submissionId,
        string adminName,
        string reason,
        DateTime nowUtc,
        [MaybeNullWhen(false)] out string guildName)
    {
        if (!TryFindWaiting(submissionId, out guildName, out var record))
            return false;

        record.LastRejection = new GuildCloakRejection
        {
            Id = submissionId,
            Reason = reason,
            RejectedBy = adminName,
            RejectedAtUtc = nowUtc
        };

        record.Waiting = null;

        return true;
    }

    /// <summary>The waiting designs, oldest first, at most <paramref name="max" />.</summary>
    public IReadOnlyList<(string GuildName, GuildCloakEntry Entry)> WaitingList(int max)
        => Guilds.Where(pair => pair.Value.Waiting is not null)
                 .OrderBy(pair => pair.Value.Waiting!.SubmittedAtUtc)
                 .ThenBy(pair => pair.Value.Waiting!.Id)
                 .Take(max)
                 .Select(pair => (pair.Key, pair.Value.Waiting!))
                 .ToList();

    private GuildCloakRecord GetOrAdd(string guildName)
    {
        if (!Guilds.TryGetValue(guildName, out var record))
        {
            record = new GuildCloakRecord();
            Guilds[guildName] = record;
        }

        return record;
    }

    private bool TryFindWaiting(
        int submissionId,
        [MaybeNullWhen(false)] out string guildName,
        [MaybeNullWhen(false)] out GuildCloakRecord record)
    {
        foreach (var pair in Guilds)
            if (pair.Value.Waiting?.Id == submissionId)
            {
                guildName = pair.Key;
                record = pair.Value;

                return true;
            }

        guildName = null;
        record = null;

        return false;
    }

    /// <summary>One guild's designs.</summary>
    public sealed class GuildCloakRecord
    {
        public GuildCloakEntry? Approved { get; set; }
        public GuildCloakDesign? Draft { get; set; }
        public GuildCloakRejection? LastRejection { get; set; }
        public GuildCloakEntry? Waiting { get; set; }
    }

    /// <summary>A submitted design. <see cref="DecidedBy" /> and <see cref="DecidedAtUtc" /> are set when it is approved.</summary>
    public sealed class GuildCloakEntry
    {
        public DateTime DecidedAtUtc { get; set; }
        public string DecidedBy { get; set; } = string.Empty;
        public GuildCloakDesign Design { get; set; } = new();
        public int Id { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
    }

    /// <summary>The newest rejection: which submission, why, by whom and when.</summary>
    public sealed class GuildCloakRejection
    {
        public int Id { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime RejectedAtUtc { get; set; }
        public string RejectedBy { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 3: Register the type with the JSON context**

In `SRV/Chaos/SerializationContext.cs`, next to the two `GuildHouseState` lines (`[JsonSerializable(typeof(GuildHouseState))]` and `[JsonSerializable(typeof(Dictionary<string, GuildHouseState>))]`), add:

```csharp
[JsonSerializable(typeof(GuildCloakState))]
[JsonSerializable(typeof(Dictionary<string, GuildCloakState>))]
```

`IStorage<T>` is an open generic singleton and saves to `<storage directory>/GuildCloakState.json`; nothing else needs registering.

- [ ] **Step 4: Run the tests**

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildCloakStateTests/*" --no-ansi`
Expected: all pass. If the round-trip test fails on `GuildCloakColor`, the source generator cannot build the record struct from its constructor. Add `[method: JsonConstructor]` to the record struct in Chaos.DarkAges, or give it `{ get; init; }` properties and a parameterless constructor, and rerun Task 2's tests too.

```json:metadata
{"files": ["Chaos-Server/Chaos/Models/World/GuildCloakState.cs", "Chaos-Server/Chaos/SerializationContext.cs", "Chaos-Server/Tests/Chaos.Tests/GuildCloak/GuildCloakStateTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/GuildCloakStateTests/*\" --no-ansi", "acceptanceCriteria": ["new guild opens the default design as Draft", "save/submit/approve/reject/clear/remove behave as spec; stored designs are copies", "ids start at 1 and never repeat; replaced submissions refused", "status order: Waiting, else newest decision, else Draft", "waiting list oldest first and capped", "case-insensitive lookups after a real JSON round trip"], "modelTier": "mechanical"}
```

---
### Task 4: Server rules — `GuildCloakService`, the look message and the handlers

**Goal:** One service that checks every editor and review request, sends the editor and review windows, answers design requests, and gives every player's cloak its design id. Every sent player look is followed by a `GuildCloakLook`.

**Files:**
- Create: `SRV/Chaos/Services/GuildCloak/GuildCloakService.cs`
- Create: `SRV/Chaos/Services/GuildCloak/GuildCloakRefresh.cs`
- Modify: `SRV/Chaos/Extensions/ServiceCollectionExtensions.cs` (register the service after `CasinoLedgerService`)
- Modify: `SRV/Chaos/Networking/Abstractions/IChaosWorldClient.cs` (3 send methods after `SendStageLightingBoard`)
- Modify: `SRV/Chaos/Networking/ChaosWorldClient.cs` (constructor, `SendDisplayAisling`, 3 send methods)
- Modify: `SRV/Chaos/Services/Servers/WorldServer.cs` (constructor, 3 handlers, `IndexHandlers`)
- Test: `SRV/Tests/Chaos.Tests/GuildCloak/GuildCloakServiceTests.cs`

**Acceptance Criteria:**
- [ ] Only the current leader of a guild that owns the deed can open the editor, save or submit; everyone else gets the matching orange-bar message
- [ ] A second save or submit within 2 seconds is refused; an invalid design is refused and not stored
- [ ] A submit tells the leader and every online admin
- [ ] Only admins can decide or clear; a reject needs a 1–200 character reason; a decision on a replaced submission is refused and the admin gets a fresh list
- [ ] After an approval, `LookIdFor` gives every member the new id, and the leader is told
- [ ] Design requests are answered only for approved ids
- [ ] `SendDisplayAisling` sends a `GuildCloakLook` right after the look when any accessory sprite is the guild cloak
- [ ] The server solution builds and starts resolving `GuildCloakService` from DI

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildCloak/*/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `SRV/Tests/Chaos.Tests/GuildCloak/GuildCloakServiceTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Services.GuildCloak;
using Chaos.Storage.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
#endregion

namespace Chaos.Tests.GuildCloak;

public sealed class GuildCloakServiceTests
{
    private const string GUILD = "Shinebox";

    private static GuildCloakEditorInteractionArgs SubmitArgs()
        => new()
        {
            Action = GuildCloakEditorAction.Submit,
            Design = GuildCloakDesign.CreateDefault()
        };

    private static void ShouldGetOrangeBar(Aisling aisling, string message)
        => Mock.Get(aisling.Client)
               .Verify(c => c.SendServerMessage(ServerMessageType.AdminMessage, message), Times.Once());

    [Test]
    public void OpenEditor_refuses_a_member_who_is_not_the_leader()
    {
        var world = new World();

        world.Service.OpenEditor(world.Member);

        ShouldGetOrangeBar(world.Member, GuildCloakService.NOT_LEADER);
        Mock.Get(world.Member.Client).Verify(c => c.SendGuildCloakEditor(It.IsAny<GuildCloakEditorArgs>()), Times.Never());
    }

    [Test]
    public void OpenEditor_refuses_a_guild_without_the_deed()
    {
        var world = new World(false);

        world.Service.OpenEditor(world.Leader);

        ShouldGetOrangeBar(world.Leader, GuildCloakService.NO_DEED);
    }

    [Test]
    public void OpenEditor_sends_the_default_design_as_a_draft()
    {
        var world = new World();

        world.Service.OpenEditor(world.Leader);

        Mock.Get(world.Leader.Client)
            .Verify(
                c => c.SendGuildCloakEditor(
                    It.Is<GuildCloakEditorArgs>(a => (a.Type == GuildCloakEditorType.Open)
                                                     && (a.Status == GuildCloakStatus.Draft)
                                                     && (a.Design.Colors.Count == 1))),
                Times.Once());
    }

    [Test]
    public void Submit_stores_a_waiting_design_and_tells_the_leader_and_the_admins()
    {
        var world = new World();

        world.Service.HandleEditor(world.Leader, SubmitArgs());

        world.State.WaitingList(20).Should().ContainSingle();

        Mock.Get(world.Leader.Client)
            .Verify(
                c => c.SendGuildCloakEditor(
                    It.Is<GuildCloakEditorArgs>(a => (a.Type == GuildCloakEditorType.Status) && (a.Status == GuildCloakStatus.Waiting))),
                Times.Once());

        ShouldGetOrangeBar(world.Leader, GuildCloakService.SUBMITTED);
        ShouldGetOrangeBar(world.Admin, $"{GUILD} submitted a guild cloak design for review.");
    }

    [Test]
    public void A_second_save_within_two_seconds_is_refused()
    {
        var world = new World();

        var save = new GuildCloakEditorInteractionArgs
        {
            Action = GuildCloakEditorAction.SaveDraft,
            Design = GuildCloakDesign.CreateDefault()
        };

        world.Service.HandleEditor(world.Leader, save);
        world.Later(1);
        world.Service.HandleEditor(world.Leader, save);
        ShouldGetOrangeBar(world.Leader, GuildCloakService.TOO_FAST);

        world.Later(2);
        world.Service.HandleEditor(world.Leader, save);
        Mock.Get(world.Leader.Client).Verify(c => c.SendGuildCloakEditor(It.IsAny<GuildCloakEditorArgs>()), Times.Exactly(2));
    }

    [Test]
    public void An_invalid_design_is_refused_and_not_stored()
    {
        var world = new World();

        world.Service.HandleEditor(
            world.Leader,
            new GuildCloakEditorInteractionArgs
            {
                Action = GuildCloakEditorAction.Submit,
                Design = GuildCloakDesign.CreateDefault() with { Back = [] }
            });

        ShouldGetOrangeBar(world.Leader, GuildCloakService.INVALID_DESIGN);
        world.State.WaitingList(20).Should().BeEmpty();
    }

    [Test]
    public void An_approval_changes_the_look_of_every_member_and_tells_the_leader()
    {
        var world = new World();

        var id = world.SubmitAndApprove();

        world.Service.LookIdFor(world.Member).Should().Be(id);
        world.Service.LookIdFor(world.Leader).Should().Be(id);
        world.Service.LookIdFor(world.Admin).Should().Be(0);
        ShouldGetOrangeBar(world.Leader, GuildCloakService.APPROVED);

        Mock.Get(world.Admin.Client)
            .Verify(
                c => c.SendGuildCloakReviewList(
                    It.Is<GuildCloakReviewListArgs>(a => (a.Type == GuildCloakReviewListType.Update) && (a.Entries.Count == 0))),
                Times.Once());
    }

    [Test]
    public void A_decision_from_a_non_admin_is_ignored()
    {
        var world = new World();
        var id = world.Submit();

        world.Service.HandleReview(
            world.Member,
            new GuildCloakReviewInteractionArgs
            {
                Action = GuildCloakReviewAction.Approve,
                SubmissionId = id
            });

        world.State.ApprovedIdOf(GUILD).Should().Be(0);
        world.State.WaitingList(20).Should().ContainSingle();
    }

    [Test]
    public void A_reject_needs_a_reason_and_the_leader_sees_it()
    {
        var world = new World();
        var id = world.Submit();

        world.Service.HandleReview(
            world.Admin,
            new GuildCloakReviewInteractionArgs
            {
                Action = GuildCloakReviewAction.Reject,
                SubmissionId = id,
                Reason = "   "
            });

        ShouldGetOrangeBar(world.Admin, GuildCloakService.NEED_REASON);
        world.State.WaitingList(20).Should().ContainSingle();

        world.Service.HandleReview(
            world.Admin,
            new GuildCloakReviewInteractionArgs
            {
                Action = GuildCloakReviewAction.Reject,
                SubmissionId = id,
                Reason = "Too bright."
            });

        ShouldGetOrangeBar(world.Leader, "An admin rejected your guild cloak design: Too bright.");
        world.State.EditorView(GUILD).Status.Should().Be(GuildCloakStatus.Rejected);
    }

    [Test]
    public void A_decision_on_a_replaced_design_is_refused()
    {
        var world = new World();
        var first = world.Submit();
        world.Submit();

        world.Service.HandleReview(
            world.Admin,
            new GuildCloakReviewInteractionArgs
            {
                Action = GuildCloakReviewAction.Approve,
                SubmissionId = first
            });

        ShouldGetOrangeBar(world.Admin, GuildCloakService.STALE_DECISION);
        world.State.ApprovedIdOf(GUILD).Should().Be(0);
    }

    [Test]
    public void Design_requests_are_answered_only_for_approved_designs()
    {
        var world = new World();
        var id = world.Submit();

        world.Service.HandleDesignRequest(world.Member.Client, id);
        Mock.Get(world.Member.Client).Verify(c => c.SendGuildCloakDesign(It.IsAny<GuildCloakDesignArgs>()), Times.Never());

        world.Service.HandleReview(
            world.Admin,
            new GuildCloakReviewInteractionArgs
            {
                Action = GuildCloakReviewAction.Approve,
                SubmissionId = id
            });

        world.Service.HandleDesignRequest(world.Member.Client, id);
        Mock.Get(world.Member.Client).Verify(c => c.SendGuildCloakDesign(It.Is<GuildCloakDesignArgs>(a => a.DesignId == id)), Times.Once());
    }

    [Test]
    public void Clear_needs_an_admin_and_returns_members_to_the_plain_cloak()
    {
        var world = new World();
        world.SubmitAndApprove();

        world.Service.Clear(world.Member, GUILD).Should().BeFalse();
        world.Service.Clear(world.Admin, "shinebox").Should().BeTrue();

        world.Service.LookIdFor(world.Member).Should().Be(0);
    }

    [Test]
    public void RemoveGuild_forgets_the_designs()
    {
        var world = new World();
        world.SubmitAndApprove();

        world.Service.RemoveGuild(GUILD);

        world.Service.LookIdFor(world.Member).Should().Be(0);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>The guild "Shinebox" with a leader and a member, plus an online admin. The guild owns the deed unless told otherwise.</summary>
    private sealed class World
    {
        public readonly Aisling Admin;
        public readonly ClientRegistry<IChaosWorldClient> Clients = new();
        public readonly GuildHouseState House = new(new Mock<IStorage<GuildHouseState>>().Object);
        public readonly Aisling Leader;
        public readonly Aisling Member;
        public readonly GuildCloakService Service;
        public readonly GuildCloakState State = new();
        public readonly FixedTime Time = new(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
        private uint NextClientId = 1;

        public World(bool ownsDeed = true)
        {
            var guild = MockGuild.Create(GUILD);
            Leader = Online(MockAisling.Create(name: "Stahli"));
            Member = Online(MockAisling.Create(name: "Iglis"));
            Admin = Online(MockAisling.Create(name: "Sichi"));
            Admin.IsAdmin = true;

            guild.AddMember(Leader, Leader);
            guild.ChangeRank(Leader.Name, 0, Leader);
            guild.AddMember(Member, Leader);

            if (ownsDeed)
                House.EnableProperty(GUILD, GuildCloakProtocol.DEED_PROPERTY);

            var cloakStorage = new Mock<IStorage<GuildCloakState>>();
            cloakStorage.SetupGet(s => s.Value).Returns(State);

            var houseStorage = new Mock<IStorage<GuildHouseState>>();
            houseStorage.SetupGet(s => s.Value).Returns(House);

            Service = new GuildCloakService(
                cloakStorage.Object,
                houseStorage.Object,
                Clients,
                Time,
                NullLogger<GuildCloakService>.Instance);
        }

        public void Later(double seconds) => Time.Now = Time.Now.AddSeconds(seconds);

        /// <summary>Submits the default design as the leader and returns its submission id. Waits out the save limit.</summary>
        public int Submit()
        {
            Service.HandleEditor(Leader, SubmitArgs());
            Later(3);

            return State.WaitingList(1)[0].Entry.Id;
        }

        public int SubmitAndApprove()
        {
            var id = Submit();

            Service.HandleReview(
                Admin,
                new GuildCloakReviewInteractionArgs
                {
                    Action = GuildCloakReviewAction.Approve,
                    SubmissionId = id
                });

            return id;
        }

        private Aisling Online(Aisling aisling)
        {
            Mock.Get(aisling.Client).SetupGet(c => c.Id).Returns(NextClientId++);
            Clients.TryAdd(aisling.Client);

            return aisling;
        }
    }
}
```

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildCloakServiceTests/*" --no-ansi`
Expected: build FAILS with `The type or namespace name 'GuildCloakService' could not be found`.

- [ ] **Step 2: Add the send methods to the client interface**

In `SRV/Chaos/Networking/Abstractions/IChaosWorldClient.cs`, after `void SendStageLightingBoard(StageLightingBoardArgs args);`:

```csharp

    /// <summary>Opens the guild cloak editor, or updates its status line after a save or submit.</summary>
    void SendGuildCloakEditor(GuildCloakEditorArgs args);

    /// <summary>Sends one approved guild cloak design.</summary>
    void SendGuildCloakDesign(GuildCloakDesignArgs args);

    /// <summary>Opens or updates an admin's guild cloak review window.</summary>
    void SendGuildCloakReviewList(GuildCloakReviewListArgs args);
```

- [ ] **Step 3: Write the refresh helper and the service**

Create `SRV/Chaos/Services/GuildCloak/GuildCloakRefresh.cs`:

```csharp
#region
using Chaos.Models.World;
#endregion

namespace Chaos.Services.GuildCloak;

/// <summary>Re-sends a player's look after their guild, or their guild's cloak design, changes.</summary>
public static class GuildCloakRefresh
{
    /// <summary>
    ///     Re-sends <paramref name="aisling" />'s look to everyone who can see them, themselves included. It runs on the
    ///     aisling's own map thread, because the caller may be on another map. If they change maps first, the new map shows
    ///     them fresh anyway.
    /// </summary>
    public static void Redisplay(Aisling aisling)
    {
        if (aisling.MapInstance is not { } map)
            return;

        map.BeginInvoke(
            () =>
            {
                if (aisling.MapInstance == map)
                    aisling.Display();
            });
    }
}
```

Create `SRV/Chaos/Services/GuildCloak/GuildCloakService.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Storage.Abstractions;
using Microsoft.Extensions.Logging;
#endregion

namespace Chaos.Services.GuildCloak;

/// <summary>
///     Guild cloak rules: who may design and review, the save rate, and which design every guild cloak shows. It holds the
///     only lock around <see cref="GuildCloakState" /> and saves it after every change. Nothing from the client is trusted:
///     rank, deed, admin flag and design are checked on every request.
/// </summary>
public sealed class GuildCloakService
{
    public const string APPROVED = "An admin approved your guild cloak design.";
    public const string INVALID_DESIGN = "That design could not be saved.";
    public const string NEED_REASON = "A rejection needs a reason of 1 to 200 characters.";
    public const string NO_DEED = "Your guild does not own the cloak deed.";
    public const string NOT_IN_GUILD = "You are not part of a guild.";
    public const string NOT_LEADER = "Only the guild leader can design the guild cloak.";
    public const string STALE_DECISION = "This design changed. The list has been refreshed.";
    public const string SUBMITTED = "Your guild cloak design was sent to the admins for review.";
    public const string TOO_FAST = "Please wait a moment before saving again.";

    private readonly IStorage<GuildCloakState> CloakStorage;
    private readonly IClientRegistry<IChaosWorldClient> ClientRegistry;
    private readonly IStorage<GuildHouseState> HouseStorage;
    private readonly Dictionary<string, DateTime> LastSaveUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<GuildCloakService> Logger;
    private readonly Lock Sync = new();
    private readonly TimeProvider Time;

    public GuildCloakService(
        IStorage<GuildCloakState> cloakStorage,
        IStorage<GuildHouseState> houseStorage,
        IClientRegistry<IChaosWorldClient> clientRegistry,
        TimeProvider time,
        ILogger<GuildCloakService> logger)
    {
        CloakStorage = cloakStorage;
        HouseStorage = houseStorage;
        ClientRegistry = clientRegistry;
        Time = time;
        Logger = logger;

        lock (Sync)
            State.EnsureCaseInsensitive();
    }

    private DateTime NowUtc => Time.GetUtcNow().UtcDateTime;
    private GuildCloakState State => CloakStorage.Value;

    /// <summary>Removes an admin-chosen guild's approved design. Members' cloaks go plain. False for non-admins or no design.</summary>
    public bool Clear(Aisling admin, string guildName)
    {
        if (!admin.IsAdmin)
            return false;

        bool cleared;

        lock (Sync)
        {
            cleared = State.ClearApproved(guildName);

            if (cleared)
                CloakStorage.Save();
        }

        if (!cleared)
            return false;

        Logger.LogInformation("{Admin} cleared the guild cloak design of {Guild}", admin.Name, guildName);
        RedisplayMembers(guildName);

        return true;
    }

    /// <summary>Why <paramref name="source" /> cannot use the editor, or null when they can.</summary>
    public string? EditorRefusal(Aisling source)
    {
        if (source.Guild is null)
            return NOT_IN_GUILD;

        if (!IsLeader(source))
            return NOT_LEADER;

        return OwnsDeed(source.Guild.Name) ? null : NO_DEED;
    }

    /// <summary>Answers a client's request for one design. Only approved designs are ever sent.</summary>
    public void HandleDesignRequest(IChaosWorldClient client, int designId)
    {
        GuildCloakDesign? design;

        lock (Sync)
            design = State.FindApproved(designId)
                          ?.DeepCopy();

        if (design is not null)
            client.SendGuildCloakDesign(
                new GuildCloakDesignArgs
                {
                    DesignId = designId,
                    Design = design
                });
    }

    /// <summary>A leader's save or submit from the editor.</summary>
    public void HandleEditor(Aisling source, GuildCloakEditorInteractionArgs args)
    {
        var refusal = EditorRefusal(source);

        if (refusal is not null)
        {
            source.SendOrangeBarMessage(refusal);

            return;
        }

        if (args.Design is null || !args.Design.IsValid())
        {
            source.SendOrangeBarMessage(INVALID_DESIGN);

            return;
        }

        var guildName = source.Guild!.Name;
        var now = NowUtc;
        GuildCloakEditorArgs? reply = null;

        lock (Sync)
            if (!LastSaveUtc.TryGetValue(source.Name, out var last) || ((now - last) >= GuildCloakProtocol.SaveInterval))
            {
                LastSaveUtc[source.Name] = now;

                if (args.Action == GuildCloakEditorAction.Submit)
                    State.Submit(guildName, source.Name, args.Design, now);
                else
                    State.SaveDraft(guildName, args.Design);

                CloakStorage.Save();
                reply = EditorArgs(GuildCloakEditorType.Status, guildName);
            }

        if (reply is null)
        {
            source.SendOrangeBarMessage(TOO_FAST);

            return;
        }

        source.Client.SendGuildCloakEditor(reply);

        if (args.Action != GuildCloakEditorAction.Submit)
            return;

        Logger.LogInformation("{Leader} submitted a guild cloak design for {Guild}", source.Name, guildName);
        source.SendOrangeBarMessage(SUBMITTED);

        foreach (var admin in OnlineAislings()
                     .Where(aisling => aisling.IsAdmin))
            admin.SendOrangeBarMessage($"{guildName} submitted a guild cloak design for review.");
    }

    /// <summary>An admin's approve or reject from the review window. The admin always gets a fresh list back.</summary>
    public void HandleReview(Aisling admin, GuildCloakReviewInteractionArgs args)
    {
        if (!admin.IsAdmin)
        {
            Logger.LogWarning("{Name} sent a guild cloak review decision but is not an admin", admin.Name);

            return;
        }

        var reason = (args.Reason ?? string.Empty).Trim();

        if ((args.Action == GuildCloakReviewAction.Reject) && reason.Length is < 1 or > GuildCloakProtocol.MAX_REASON_CHARS)
        {
            admin.SendOrangeBarMessage(NEED_REASON);

            return;
        }

        var now = NowUtc;
        bool decided;
        string? guildName;

        lock (Sync)
        {
            decided = args.Action == GuildCloakReviewAction.Approve
                ? State.TryApprove(args.SubmissionId, admin.Name, now, out guildName)
                : State.TryReject(args.SubmissionId, admin.Name, reason, now, out guildName);

            if (decided)
                CloakStorage.Save();
        }

        if (!decided)
            admin.SendOrangeBarMessage(STALE_DECISION);
        else if (args.Action == GuildCloakReviewAction.Approve)
        {
            Logger.LogInformation("{Admin} approved guild cloak design {Id} for {Guild}", admin.Name, args.SubmissionId, guildName);
            admin.SendOrangeBarMessage($"Approved the guild cloak of {guildName}.");
            NotifyLeaders(guildName!, APPROVED);
            RedisplayMembers(guildName!);
        } else
        {
            Logger.LogInformation(
                "{Admin} rejected guild cloak design {Id} for {Guild}: {Reason}",
                admin.Name,
                args.SubmissionId,
                guildName,
                reason);

            admin.SendOrangeBarMessage($"Rejected the guild cloak of {guildName}.");
            NotifyLeaders(guildName!, $"An admin rejected your guild cloak design: {reason}");
        }

        admin.Client.SendGuildCloakReviewList(ReviewList(GuildCloakReviewListType.Update));
    }

    public static bool IsLeader(Aisling source)
        => source.Guild is { } guild && guild.HasMember(source.Name) && guild.RankOf(source.Name).IsLeaderRank;

    /// <summary>The design id <paramref name="aisling" />'s guild cloak shows: their current guild's approved design, or 0.</summary>
    public int LookIdFor(Aisling aisling)
    {
        var guildName = aisling.Guild?.Name;

        lock (Sync)
            return State.ApprovedIdOf(guildName);
    }

    public void OpenEditor(Aisling source)
    {
        var refusal = EditorRefusal(source);

        if (refusal is not null)
        {
            source.SendOrangeBarMessage(refusal);

            return;
        }

        GuildCloakEditorArgs args;

        lock (Sync)
            args = EditorArgs(GuildCloakEditorType.Open, source.Guild!.Name);

        source.Client.SendGuildCloakEditor(args);
    }

    public void OpenReview(Aisling admin)
    {
        if (admin.IsAdmin)
            admin.Client.SendGuildCloakReviewList(ReviewList(GuildCloakReviewListType.Open));
    }

    public bool OwnsDeed(string guildName) => HouseStorage.Value.HasProperty(guildName, GuildCloakProtocol.DEED_PROPERTY);

    /// <summary>Forgets a disbanded guild's designs.</summary>
    public void RemoveGuild(string guildName)
    {
        lock (Sync)
        {
            State.RemoveGuild(guildName);
            CloakStorage.Save();
        }
    }

    //the caller holds Sync
    private GuildCloakEditorArgs EditorArgs(GuildCloakEditorType type, string guildName)
    {
        (var design, var status, var reason) = State.EditorView(guildName);

        return new GuildCloakEditorArgs
        {
            Type = type,
            Status = status,
            RejectionReason = reason,
            Design = type == GuildCloakEditorType.Open ? design : new GuildCloakDesign()
        };
    }

    private void NotifyLeaders(string guildName, string message)
    {
        foreach (var member in OnlineMembers(guildName)
                     .Where(IsLeader))
            member.SendOrangeBarMessage(message);
    }

    private List<Aisling> OnlineAislings()
        => ClientRegistry.Select(client => client.Aisling)
                         .Where(aisling => aisling is not null)
                         .ToList();

    private List<Aisling> OnlineMembers(string guildName)
        => OnlineAislings()
           .Where(aisling => (aisling.Guild is not null)
                             && string.Equals(aisling.Guild.Name, guildName, StringComparison.OrdinalIgnoreCase))
           .ToList();

    private void RedisplayMembers(string guildName)
    {
        foreach (var member in OnlineMembers(guildName))
            GuildCloakRefresh.Redisplay(member);
    }

    private GuildCloakReviewListArgs ReviewList(GuildCloakReviewListType type)
    {
        lock (Sync)
            return new GuildCloakReviewListArgs
            {
                Type = type,
                Entries = State.WaitingList(GuildCloakProtocol.MAX_REVIEW_ENTRIES)
                               .Select(
                                   waiting => new GuildCloakReviewEntry
                                   {
                                       SubmissionId = waiting.Entry.Id,
                                       GuildName = waiting.GuildName,
                                       LeaderName = waiting.Entry.SubmittedBy,
                                       SubmittedAtUtc = waiting.Entry.SubmittedAtUtc,
                                       Design = waiting.Entry.Design.DeepCopy()
                                   })
                               .ToList()
            };
    }
}
```

- [ ] **Step 4: Register the service**

In `SRV/Chaos/Extensions/ServiceCollectionExtensions.cs`, right after `services.AddSingleton<CasinoLedgerService>();` (the same method registers `TimeProvider.System`):

```csharp

            //guild cloaks: designs, reviews and which design each cloak shows (IStorage<T> is an open generic)
            services.AddSingleton<GuildCloakService>();
```

Add `using Chaos.Services.GuildCloak;` to the file's usings.

- [ ] **Step 5: Send the look after every player look, and the three send methods**

In `SRV/Chaos/Networking/ChaosWorldClient.cs`:

1. Add `using Chaos.Services.GuildCloak;`, and `using Chaos.DarkAges.Definitions;` if it is missing.
2. Add a field `private readonly GuildCloakService GuildCloaks;`.
3. Add the constructor parameter `GuildCloakService guildCloaks` after `IPacketSerializer packetSerializer`, and `GuildCloaks = guildCloaks;` in the body. The client factory is `ActivatorUtilities.CreateFactory`, so the new parameter is resolved from DI.
4. In `SendDisplayAisling(Aisling aisling)`, after the final `Send(args);`:

```csharp

        //a guild cloak draws its wearer's current guild design; tell this viewer which one
        if ((args.AccessorySprite1 == GuildCloakProtocol.SPRITE)
            || (args.AccessorySprite2 == GuildCloakProtocol.SPRITE)
            || (args.AccessorySprite3 == GuildCloakProtocol.SPRITE))
            Send(
                new GuildCloakLookArgs
                {
                    EntityId = aisling.Id,
                    DesignId = GuildCloaks.LookIdFor(aisling)
                });
```

5. After `public void SendStageLightingBoard(StageLightingBoardArgs args) => Send(args);`:

```csharp

    /// <inheritdoc />
    public void SendGuildCloakEditor(GuildCloakEditorArgs args) => Send(args);

    /// <inheritdoc />
    public void SendGuildCloakDesign(GuildCloakDesignArgs args) => Send(args);

    /// <inheritdoc />
    public void SendGuildCloakReviewList(GuildCloakReviewListArgs args) => Send(args);
```

Checking the mapped `args` (not the equipment) covers outfits and hidden accessory slots: the look is sent only when the cloak is actually drawn.

- [ ] **Step 6: Add the handlers**

In `SRV/Chaos/Services/Servers/WorldServer.cs`:

1. Add `using Chaos.Services.GuildCloak;`.
2. Add a field `private readonly GuildCloakService GuildCloakService;` next to `BugReportService`.
3. Add the constructor parameter `GuildCloakService guildCloakService` after `BugReportService bugReportService`, and `GuildCloakService = guildCloakService;` after `BugReportService = bugReportService;`.
4. After `OnStageLightingInteraction`, add:

```csharp

    /// <summary>
    ///     Routes a guild cloak editor save or submit to <see cref="GuildCloakService" />, which checks the leader rank, the
    ///     deed, the rate and the design.
    /// </summary>
    public ValueTask OnGuildCloakEditorInteraction(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<GuildCloakEditorInteractionArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnGuildCloakEditorInteraction);

        ValueTask InnerOnGuildCloakEditorInteraction(IChaosWorldClient localClient, GuildCloakEditorInteractionArgs localArgs)
        {
            if (localClient.Connected)
                GuildCloakService.HandleEditor(localClient.Aisling, localArgs);

            return default;
        }
    }

    /// <summary>Answers a request for one approved guild cloak design. Unknown and unapproved ids get no answer.</summary>
    public ValueTask OnGuildCloakDesignRequest(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<GuildCloakDesignRequestArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnGuildCloakDesignRequest);

        ValueTask InnerOnGuildCloakDesignRequest(IChaosWorldClient localClient, GuildCloakDesignRequestArgs localArgs)
        {
            if (localClient.Connected)
                GuildCloakService.HandleDesignRequest(localClient, localArgs.DesignId);

            return default;
        }
    }

    /// <summary>Routes an admin's approve or reject to <see cref="GuildCloakService" />, which checks the admin flag.</summary>
    public ValueTask OnGuildCloakReviewInteraction(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<GuildCloakReviewInteractionArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnGuildCloakReviewInteraction);

        ValueTask InnerOnGuildCloakReviewInteraction(IChaosWorldClient localClient, GuildCloakReviewInteractionArgs localArgs)
        {
            if (localClient.Connected)
                GuildCloakService.HandleReview(localClient.Aisling, localArgs);

            return default;
        }
    }
```

5. In `IndexHandlers`, after `ClientHandlers[(byte)ClientOpCode.StageLightingInteraction] = OnStageLightingInteraction;`:

```csharp
        ClientHandlers[(byte)ClientOpCode.GuildCloakEditorInteraction] = OnGuildCloakEditorInteraction;
        ClientHandlers[(byte)ClientOpCode.GuildCloakDesignRequest] = OnGuildCloakDesignRequest;
        ClientHandlers[(byte)ClientOpCode.GuildCloakReviewInteraction] = OnGuildCloakReviewInteraction;
```

- [ ] **Step 7: Run the tests**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildCloak/*/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildTests/*" --no-ansi
```

Expected: all pass. If a mock `IChaosWorldClient` in another test fixture fails to compile, it implements the interface by hand: add the three methods there as empty bodies.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/GuildCloak/GuildCloakService.cs", "Chaos-Server/Chaos/Services/GuildCloak/GuildCloakRefresh.cs", "Chaos-Server/Chaos/Extensions/ServiceCollectionExtensions.cs", "Chaos-Server/Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos-Server/Chaos/Networking/ChaosWorldClient.cs", "Chaos-Server/Chaos/Services/Servers/WorldServer.cs", "Chaos-Server/Tests/Chaos.Tests/GuildCloak/GuildCloakServiceTests.cs"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildCloak/*/*\" --no-ansi", "acceptanceCriteria": ["only the leader of a deed-owning guild can open, save or submit", "saves within 2 s refused; invalid designs refused and not stored", "submit tells the leader and online admins", "only admins decide or clear; reject needs 1-200 chars; stale decisions refused with a fresh list", "after approval LookIdFor gives members the new id and the leader is told", "design requests answered only for approved ids", "SendDisplayAisling follows a guild cloak look with GuildCloakLook", "server builds and resolves GuildCloakService"], "modelTier": "standard"}
```

---

### Task 5: Guild scripts and dialogs — deed, Quill, admin trinket, disband, membership

**Goal:** Tibbs sells the cloak deed without changing the hall map; Quill opens the editor for the leader and sells the Guild Cloak; the admin trinket opens the review window and clears a design; disbanding forgets the designs; joining, leaving, removal and disband refresh the player's look.

**Files:**
- Modify: `SRV/Chaos/Models/World/GuildHouseState.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs`
- Create: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildCloakScript.cs`
- Create: `SRV/Chaos/Scripting/DialogScripts/Temuair/Generic/GuildCloakAdminScript.cs`
- Modify: `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs`
- Modify: `SRV/Chaos/Collections/Guild.cs`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_cloaks.json`, `tibbs_purchase_cloaks_confirm.json`
- Modify: `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_initial.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_initial.json`, `quill_guildcloak_design.json`, `quill_guildcloak_buy.json`, `quill_guildcloak_buy_confirm.json`
- Modify: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_initial.json`
- Create: `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_review.json`, `admintrinket_guildcloak_clear.json`, `admintrinket_guildcloak_clear_confirm.json`
- Test: `SRV/Tests/Chaos.Tests/GuildHall/GuildHouseStateTests.cs` (one new test)

**Acceptance Criteria:**
- [ ] `cloaks` is a guild hall property: `HasProperty`/`EnableProperty` accept it, and it plays no part in the hall's morph code
- [ ] Tibbs lists "Purchase Guild Cloaks" until the deed is owned; buying it takes 5,000,000 gold, tells the guild, and neither morphs the hall nor spawns NPCs
- [ ] Quill shows "Guild Cloak" only when the deed is owned; "Design the guild cloak" only for the leader; buying takes 50,000 gold and gives one Guild Cloak
- [ ] The admin trinket has "Review Guild Cloaks" and "Clear a Guild Cloak" (name, then confirm); non-admins get nothing
- [ ] Disbanding a guild removes it from `GuildCloakState` too
- [ ] `Guild.AddMember`, `TryKickMember`, `TryLeave` and `Disband` queue a look refresh for each affected online player
- [ ] All new and changed JSON files parse; server tests pass except the two known failures

**Verify:** `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildHall/*/*" --no-ansi` and the same with `/*/*/GuildTests/*` → all pass

**Steps:**

- [ ] **Step 1: Write the failing test**

In `SRV/Tests/Chaos.Tests/GuildHall/GuildHouseStateTests.cs`, add:

```csharp
    [Test]
    public void The_cloak_deed_is_a_property_like_the_rooms()
    {
        var state = BuildState();

        state.HasProperty(GUILD, "cloaks").Should().BeFalse();

        state.EnableProperty(GUILD, "cloaks");

        state.HasProperty(GUILD, "cloaks").Should().BeTrue();
    }
```

Run: `dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildHouseStateTests/*" --no-ansi`
Expected: FAIL with `ArgumentException: Unknown property: cloaks`.

- [ ] **Step 2: Add the `cloaks` property**

In `SRV/Chaos/Models/World/GuildHouseState.cs`:
- in `GetPropertyValue`'s switch, add `"cloaks"     => properties.Cloaks,` before the `_ =>` arm;
- in `SetPropertyValue`'s switch, add before `default:`:

```csharp
            case "cloaks":
                properties.Cloaks = value;

                break;
```

- in `HouseProperties`, add `public bool Cloaks { get; set; }` (keep the properties in alphabetical order: after `Bank`).

`GuildHallScript.GetMorphCode` and `GuildUpdateHallScript.GetMorphCode` only look at bank, armory, tailor and combat room, so the deed never changes the hall map. Leave them alone.

Run the Step 1 test again. Expected: PASS.

- [ ] **Step 3: Sell the deed from Tibbs**

In `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs` (add `using Chaos.DarkAges.Definitions;`):

1. In `OnDisplaying`, case `"tibbs_purchase_additions"`, after the `"tailor"` option and before the `Subject.Options.Count == 0` check:

```csharp
                if (!guildHouseState.HasProperty(guildName, GuildCloakProtocol.DEED_PROPERTY))
                    Subject.AddOption("Purchase Guild Cloaks", "tibbs_purchase_cloaks");
```

2. Add a case after `"tibbs_purchase_combatroom_confirm"`:

```csharp
            case "tibbs_purchase_cloaks_confirm":
                HandleUpgrade(
                    source,
                    guildName,
                    GuildCloakProtocol.DEED_PROPERTY,
                    "You do not have enough gold to purchase the guild cloak deed.");

                break;
```

3. In `HandleUpgrade`, replace the final `} else {` branch (the one that sends "has purchased a {property} for the guild hall!", enables the property, morphs the map and spawns NPCs) with:

```csharp
        } else if (property == GuildCloakProtocol.DEED_PROPERTY)
        {
            //the deed adds no room, so the hall map and its NPCs stay as they are
            foreach (var client in ClientRegistry)
                if (client.Aisling.Guild == source.Guild)
                    client.Aisling.SendActiveMessage($"{{=o{source.Name} has purchased the guild cloak deed! Ask Quill about the guild cloak.");

            guildHouseState.EnableProperty(guildName, property);
        } else
        {
            foreach (var client in ClientRegistry)
                if (client.Aisling.Guild == source.Guild)
                    client.Aisling.SendActiveMessage($"{{=o{source.Name} has purchased a {property} for the guild hall!");

            guildHouseState.EnableProperty(guildName, property);
            var morphCode = GetMorphCode(GuildHouseStateStorage.Value, guildName);
            source.MapInstance.Morph(morphCode);
            SpawnNPCsForMorphCode(source, morphCode);
        }
```

The rank check (council or leader) and the 5,000,000 cost are `HandleUpgrade`'s existing ones (`GOLD_UPGRADE_COST`).

- [ ] **Step 4: Write Quill's script**

Create `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildCloakScript.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Services.Factories.Abstractions;
using Chaos.Services.GuildCloak;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.GuildScripts;

/// <summary>
///     Quill's guild cloak options. Once the guild owns the cloak deed, the leader can open the cloak editor and any member
///     can buy a Guild Cloak.
/// </summary>
public class GuildCloakScript(Dialog subject, GuildCloakService guildCloaks, IItemFactory itemFactory) : DialogScriptBase(subject)
{
    public override void OnDisplaying(Aisling source)
    {
        switch (Subject.Template.TemplateKey.ToLowerInvariant())
        {
            case "quill_initial":
                if (OwnsDeed(source))
                    Subject.AddOption("Guild Cloak", "quill_guildcloak_initial");

                break;

            case "quill_guildcloak_initial":
                if (!OwnsDeed(source))
                {
                    Subject.Reply(source, GuildCloakService.NO_DEED);

                    return;
                }

                if (GuildCloakService.IsLeader(source))
                    Subject.AddOption("Design the guild cloak", "quill_guildcloak_design");

                Subject.AddOption("Buy a guild cloak", "quill_guildcloak_buy");

                break;

            case "quill_guildcloak_design":
                Subject.Close(source);
                guildCloaks.OpenEditor(source);

                break;

            case "quill_guildcloak_buy":
                Subject.InjectTextParameters(GuildCloakProtocol.CLOAK_PRICE.ToString("N0"));

                break;

            case "quill_guildcloak_buy_confirm":
                Buy(source);

                break;
        }
    }

    private void Buy(Aisling source)
    {
        if (!OwnsDeed(source))
        {
            Subject.Reply(source, GuildCloakService.NO_DEED);

            return;
        }

        var cloak = itemFactory.Create(GuildCloakProtocol.CLOAK_ITEM_KEY);

        if (!source.CanCarry(cloak))
        {
            Subject.Reply(source, "You cannot carry any more.");

            return;
        }

        if (!source.TryTakeGold(GuildCloakProtocol.CLOAK_PRICE))
        {
            Subject.Reply(source, "You do not have enough gold.");

            return;
        }

        source.GiveItemOrSendToBank(cloak);
    }

    private bool OwnsDeed(Aisling source) => source.Guild is not null && guildCloaks.OwnsDeed(source.Guild.Name);
}
```

- [ ] **Step 5: Write the admin trinket script**

Create `SRV/Chaos/Scripting/DialogScripts/Temuair/Generic/GuildCloakAdminScript.cs`:

```csharp
#region
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Services.GuildCloak;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.Generic;

/// <summary>
///     The admin trinket's guild cloak options: open the review window, or clear a guild's approved design after typing the
///     guild's name and confirming.
/// </summary>
public class GuildCloakAdminScript(Dialog subject, GuildCloakService guildCloaks) : DialogScriptBase(subject)
{
    public override void OnDisplaying(Aisling source)
    {
        if (!source.IsAdmin)
        {
            Subject.Close(source);

            return;
        }

        switch (Subject.Template.TemplateKey.ToLowerInvariant())
        {
            case "admintrinket_guildcloak_review":
                Subject.Close(source);
                guildCloaks.OpenReview(source);

                break;

            case "admintrinket_guildcloak_clear_confirm":
                if (Subject.Context is string guildName)
                    Subject.InjectTextParameters(guildName);

                break;
        }
    }

    public override void OnNext(Aisling source, byte? optionIndex = null)
    {
        if (!source.IsAdmin)
            return;

        switch (Subject.Template.TemplateKey.ToLowerInvariant())
        {
            case "admintrinket_guildcloak_clear":
            {
                if (!Subject.MenuArgs.TryGetNext<string>(out var typed) || string.IsNullOrWhiteSpace(typed))
                {
                    Subject.ReplyToUnknownInput(source);

                    return;
                }

                //carried into the contextual confirm dialog
                Subject.Context = typed.Trim();

                break;
            }

            //option 1 is "Yes, clear it"; option 2 just closes
            case "admintrinket_guildcloak_clear_confirm" when (optionIndex == 1) && Subject.Context is string name:
                source.SendOrangeBarMessage(
                    guildCloaks.Clear(source, name) ? $"Cleared the cloak design of {name}." : $"{name} has no approved cloak design.");

                break;
        }
    }
}
```

- [ ] **Step 6: Forget designs on disband, and refresh looks on membership changes**

In `SRV/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs`: add `using Chaos.Services.GuildCloak;`, a constructor parameter `GuildCloakService guildCloaks` stored in a field `GuildCloaks`, and right after `guildHouseState.RemoveGuild(guildName);`:

```csharp
        GuildCloaks.RemoveGuild(guildName);
```

In `SRV/Chaos/Collections/Guild.cs`: add `using Chaos.Services.GuildCloak;`, then add `GuildCloakRefresh.Redisplay(aisling);` directly after `aisling.Client.SendSelfProfile();` in each of these four places:
- `AddMember` (the admitted aisling),
- `TryKickMember` (inside `if (aisling is not null)`),
- `TryLeave`,
- `Disband` (inside the loop over online members).

Players wearing a guild cloak then switch to their new guild's design, or to plain, in front of everyone nearby. The refresh runs on each player's own map thread, because a kicked or disbanded member may be on another map.

- [ ] **Step 7: Write the Unora dialogs**

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_cloaks.json`:

```json
{
  "options": [
    {
      "dialogKey": "tibbs_purchase_cloaks_confirm",
      "optionText": "Yes, let's get the cloak deed."
    },
    {
      "dialogKey": "tibbs_initial",
      "optionText": "No thanks."
    }
  ],
  "scriptKeys": [],
  "scriptVars": {},
  "templateKey": "tibbs_purchase_cloaks",
  "text": "Would you like the guild cloak deed? Your leader designs the guild's cloak, and members buy it from Quill. It will be 5 million gold coins.",
  "type": "DialogMenu"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_cloaks_confirm.json`:

```json
{
  "nextDialogKey": "tibbs_initial",
  "options": [],
  "scriptKeys": [
    "guildupdatehall"
  ],
  "scriptVars": {},
  "templateKey": "tibbs_purchase_cloaks_confirm",
  "text": "(He flicks his wand around in a fancy sequence)\n Done! Ask Quill about the guild cloak.",
  "type": "Normal"
}
```

In `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_initial.json`, change `"scriptKeys"` to:

```json
  "scriptKeys": [
    "GuildManagement",
    "GuildCloak"
  ],
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_initial.json`:

```json
{
  "options": [],
  "scriptKeys": [
    "GuildCloak"
  ],
  "scriptVars": {},
  "templateKey": "quill_guildcloak_initial",
  "text": "Ah, the guild cloak. What would you like to do?",
  "type": "DialogMenu"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_design.json`:

```json
{
  "nextDialogKey": "Close",
  "options": [],
  "scriptKeys": [
    "GuildCloak"
  ],
  "scriptVars": {},
  "templateKey": "quill_guildcloak_design",
  "text": "Let me fetch the pattern book...",
  "type": "Normal"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_buy.json`:

```json
{
  "options": [
    {
      "dialogKey": "quill_guildcloak_buy_confirm",
      "optionText": "Yes, I'll take one."
    },
    {
      "dialogKey": "Close",
      "optionText": "No thanks."
    }
  ],
  "scriptKeys": [
    "GuildCloak"
  ],
  "scriptVars": {},
  "templateKey": "quill_guildcloak_buy",
  "text": "A guild cloak costs {0} gold. It always shows your guild's approved design. Would you like one?",
  "type": "DialogMenu"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_buy_confirm.json`:

```json
{
  "nextDialogKey": "Close",
  "options": [],
  "scriptKeys": [
    "GuildCloak"
  ],
  "scriptVars": {},
  "templateKey": "quill_guildcloak_buy_confirm",
  "text": "Here is your guild cloak. Wear it with pride!",
  "type": "Normal"
}
```

In `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_initial.json`, append two entries to `"options"` (after "Grant Positions"):

```json
    {
      "dialogKey": "admintrinket_guildcloak_review",
      "optionText": "Review Guild Cloaks"
    },
    {
      "dialogKey": "admintrinket_guildcloak_clear",
      "optionText": "Clear a Guild Cloak"
    }
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_review.json`:

```json
{
  "nextDialogKey": "Close",
  "options": [],
  "scriptKeys": [
    "GuildCloakAdmin"
  ],
  "scriptVars": {},
  "templateKey": "admintrinket_guildcloak_review",
  "text": "Opening the guild cloak review...",
  "type": "Normal"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear.json`:

```json
{
  "nextDialogKey": "admintrinket_guildcloak_clear_confirm",
  "options": [],
  "scriptKeys": [
    "GuildCloakAdmin"
  ],
  "scriptVars": {},
  "templateKey": "admintrinket_guildcloak_clear",
  "text": "Which guild's cloak design should be cleared?",
  "textBoxLength": 40,
  "type": "DialogTextEntry"
}
```

Create `UNO/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear_confirm.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "Close",
      "optionText": "Yes, clear it"
    },
    {
      "dialogKey": "Close",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "GuildCloakAdmin"
  ],
  "scriptVars": {},
  "templateKey": "admintrinket_guildcloak_clear_confirm",
  "text": "Clear the approved cloak design of {0}? Its members' cloaks go plain.",
  "type": "DialogMenu"
}
```

Script keys resolve to the class name without `Script` (`GuildCloak` → `GuildCloakScript`, `GuildCloakAdmin` → `GuildCloakAdminScript`), the same way `GuildManagement` resolves today.

- [ ] **Step 8: Check the JSON and run the tests**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora/Data/Configuration/Templates/Dialogs/Temauir
python -c "import json,sys; [json.load(open(f, encoding='utf-8')) for f in sys.argv[1:]]; print('json ok')" "Guild Hall/Tibbs/tibbs_purchase_cloaks.json" "Guild Hall/Tibbs/tibbs_purchase_cloaks_confirm.json" "Guild Hall/Quill/quill_initial.json" "Guild Hall/Quill/quill_guildcloak_initial.json" "Guild Hall/Quill/quill_guildcloak_design.json" "Guild Hall/Quill/quill_guildcloak_buy.json" "Guild Hall/Quill/quill_guildcloak_buy_confirm.json" generic/admin/admintrinket_initial.json generic/admin/admintrinket_guildcloak_review.json generic/admin/admintrinket_guildcloak_clear.json generic/admin/admintrinket_guildcloak_clear_confirm.json
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildHall/*/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/GuildTests/*" --no-ansi
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.GuildCloak/*/*" --no-ansi
```

Expected: `json ok`, then all tests pass.

```json:metadata
{"files": ["Chaos-Server/Chaos/Models/World/GuildHouseState.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildCloakScript.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/Generic/GuildCloakAdminScript.cs", "Chaos-Server/Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs", "Chaos-Server/Chaos/Collections/Guild.cs", "Chaos-Server/Tests/Chaos.Tests/GuildHall/GuildHouseStateTests.cs", "Unora/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_cloaks.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_cloaks_confirm.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_initial.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_initial.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_design.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_buy.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_buy_confirm.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_initial.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_review.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear_confirm.json"], "verifyCommand": "dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.GuildHall/*/*\" --no-ansi", "acceptanceCriteria": ["cloaks is a hall property outside the morph code", "Tibbs sells the deed for 5M without morphing or spawning", "Quill: Guild Cloak only with deed; Design only for leader; buy takes 50,000 and gives one cloak", "admin trinket review and clear (name then confirm); non-admins get nothing", "disband removes the guild from GuildCloakState", "join/kick/leave/disband queue a look refresh", "JSON parses; server tests pass except the two known failures"], "modelTier": "standard"}
```

---
### Task 6: Client painting — grids, painter, references and the design store

**Goal:** Pure, tested code that maps a design onto any cloak frame with the frame's own shading, turns the result into a layer image, and keeps designs by id.

**Files:**
- Create: `CLI/Chaos.Client.Rendering/GuildCloakGrid.cs`
- Create: `CLI/Chaos.Client.Rendering/GuildCloakPainter.cs`
- Create: `CLI/Chaos.Client.Rendering/GuildCloakReferences.cs`
- Create: `CLI/Chaos.Client.Rendering/GuildCloakDesignStore.cs`
- Test: `CLI/Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs`, `CLI/Tests/Chaos.Client.Tests/GuildCloakDesignStoreTests.cs`

**Acceptance Criteria:**
- [ ] A frame painted on its own grid takes each cell's color, shaded 0.78 + 0.75 × brightness / 99 inside and 0.42 on the outline
- [ ] A flipped draw reads the design mirrored; a frame of another size maps by fraction of its outline
- [ ] Dye pixels (slots 98–103) take the middle brightness of their non-dye neighbours
- [ ] An unpainted cell takes the nearest painted cell in its row, else color 1; an empty frame paints nothing
- [ ] `CellAt` skips empty rows to the nearest filled row and always ends
- [ ] `ToImage` places pixels at the frame's left/top, like `Graphics.RenderImage`
- [ ] The store asks for a server design at most once per 10 seconds, never for ids ≤ 0; local ids are new negatives and drop the replaced one

**Verify:** client build (Global Constraints), then `dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter "/*/*/GuildCloakPainterTests/*" --no-ansi` and the same with `GuildCloakDesignStoreTests` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `CLI/Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs`:

```csharp
using Chaos.Client.Rendering;
using DALib.Drawing;
using FluentAssertions;
using SkiaSharp;

namespace Chaos.Client.Tests;

public class GuildCloakPainterTests
{
    private static readonly SKColor Red = new(100, 0, 0);
    private static readonly SKColor Green = new(0, 100, 0);

    /// <summary>Every slot is gray 99 (the cape's lightest cloth), slot 29 is gray 31, and the dye slots are bright magenta.</summary>
    private static SKColor[] Palette()
    {
        var palette = Enumerable.Repeat(new SKColor(99, 99, 99), 256).ToArray();
        palette[29] = new SKColor(31, 31, 31);

        for (var i = 98; i <= 103; i++)
            palette[i] = new SKColor(255, 0, 255);

        return palette;
    }

    private static byte[] Solid(int width, int height, byte value) => Enumerable.Repeat(value, width * height).ToArray();

    /// <summary>A 5x5 grid whose two left columns are color 1 and the rest color 2.</summary>
    private static byte[] HalfAndHalf()
    {
        var cells = new byte[25];

        for (var y = 0; y < 5; y++)
            for (var x = 0; x < 5; x++)
                cells[(y * 5) + x] = (byte)(x < 2 ? 1 : 2);

        return cells;
    }

    private static float InnerShade(int brightness)
        => GuildCloakPainter.BASE_SHADE + (GuildCloakPainter.LIGHT_SHADE * brightness / GuildCloakPainter.FULL_BRIGHTNESS);

    [Test]
    public void A_frame_painted_on_its_own_grid_takes_each_cells_color()
    {
        var data = Solid(5, 5, 25);
        var reference = new GuildCloakGrid(5, 5, data);

        var pixels = GuildCloakPainter.Paint(5, 5, data, Palette(), reference, HalfAndHalf(), [Red, Green], false);

        pixels[(2 * 5) + 1].Should().Be(GuildCloakPainter.Shade(Red, InnerShade(99)));
        pixels[(2 * 5) + 3].Should().Be(GuildCloakPainter.Shade(Green, InnerShade(99)));
        pixels[0].Should().Be(GuildCloakPainter.Shade(Red, GuildCloakPainter.EDGE_SHADE));
    }

    [Test]
    public void A_flipped_draw_reads_the_design_mirrored()
    {
        var data = Solid(5, 5, 25);
        var reference = new GuildCloakGrid(5, 5, data);

        var pixels = GuildCloakPainter.Paint(5, 5, data, Palette(), reference, HalfAndHalf(), [Red, Green], true);

        pixels[(2 * 5) + 1].Should().Be(GuildCloakPainter.Shade(Green, InnerShade(99)));
        pixels[(2 * 5) + 3].Should().Be(GuildCloakPainter.Shade(Red, InnerShade(99)));
    }

    [Test]
    public void A_frame_of_another_size_maps_by_fraction_of_its_outline()
    {
        var reference = new GuildCloakGrid(5, 5, Solid(5, 5, 25));
        var data = Solid(9, 3, 25);

        var pixels = GuildCloakPainter.Paint(9, 3, data, Palette(), reference, HalfAndHalf(), [Red, Green], false);

        pixels[(1 * 9) + 1].Should().Be(GuildCloakPainter.Shade(Red, InnerShade(99)));
        pixels[(1 * 9) + 7].Should().Be(GuildCloakPainter.Shade(Green, InnerShade(99)));
    }

    [Test]
    public void Dye_pixels_take_the_brightness_of_the_cloth_around_them()
    {
        var data = Solid(5, 5, 29);
        data[12] = 100;
        var reference = new GuildCloakGrid(5, 5, data);

        var pixels = GuildCloakPainter.Paint(5, 5, data, Palette(), reference, Solid(5, 5, 1), [Red], false);

        pixels[12].Should().Be(GuildCloakPainter.Shade(Red, InnerShade(31)));
    }

    [Test]
    public void An_unpainted_cell_takes_the_nearest_painted_cell_in_its_row()
    {
        var reference = new GuildCloakGrid(5, 5, Solid(5, 5, 25));
        var cells = Solid(5, 5, 2);
        cells[(2 * 5) + 2] = 0;
        cells[(2 * 5) + 3] = 1;

        GuildCloakPainter.ColorNumberAt(reference, cells, 2, 2, 2).Should().Be(2);

        for (var x = 0; x < 5; x++)
            cells[(4 * 5) + x] = 0;

        GuildCloakPainter.ColorNumberAt(reference, cells, 2, 4, 2).Should().Be(1);
    }

    [Test]
    public void An_empty_frame_paints_nothing()
    {
        var reference = new GuildCloakGrid(5, 5, Solid(5, 5, 25));

        var pixels = GuildCloakPainter.Paint(3, 3, new byte[9], Palette(), reference, HalfAndHalf(), [Red], false);

        pixels.Should().OnlyContain(color => color == default);
    }

    [Test]
    public void CellAt_skips_empty_rows_to_the_nearest_filled_row()
    {
        var data = new byte[15];

        foreach (var row in new[] { 0, 1, 4 })
            for (var x = 0; x < 3; x++)
                data[(row * 3) + x] = 25;

        var grid = new GuildCloakGrid(3, 5, data);

        grid.CellAt(0.5f, 0.5f).Y.Should().Be(1);
        grid.CellAt(0.5f, 0.8f).Y.Should().Be(4);
        grid.CellAt(1f, 0f).Should().Be((2, 0));
    }

    [Test]
    public void ToImage_places_pixels_at_the_frame_position()
    {
        var frame = new EpfFrame
        {
            Left = 2,
            Top = 3,
            Right = 4,
            Bottom = 5,
            Data = [25, 25, 25, 25]
        };

        var pixels = new SKColor[4];
        pixels[0] = Red;

        using var image = GuildCloakPainter.ToImage(frame, pixels);
        using var bitmap = SKBitmap.FromImage(image);

        image.Width.Should().Be(4);
        image.Height.Should().Be(5);
        bitmap.GetPixel(2, 3).Should().Be(Red);
        bitmap.GetPixel(0, 0).Alpha.Should().Be(0);
    }
}
```

Create `CLI/Tests/Chaos.Client.Tests/GuildCloakDesignStoreTests.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildCloakDesignStoreTests
{
    [Test]
    public void Id_zero_is_never_a_design()
    {
        var store = new GuildCloakDesignStore();
        store.Set(0, GuildCloakDesign.CreateDefault());

        store.TryGet(0, out _).Should().BeFalse();
    }

    [Test]
    public void A_server_design_is_asked_for_at_most_once_every_ten_seconds()
    {
        var store = new GuildCloakDesignStore();

        store.ShouldRequest(7, 1_000).Should().BeTrue();
        store.ShouldRequest(7, 5_000).Should().BeFalse();
        store.ShouldRequest(7, 11_000).Should().BeTrue();
        store.ShouldRequest(0, 1_000).Should().BeFalse();
        store.ShouldRequest(-3, 1_000).Should().BeFalse();

        store.Set(7, GuildCloakDesign.CreateDefault());

        store.ShouldRequest(7, 50_000).Should().BeFalse();
        store.TryGet(7, out var design).Should().BeTrue();
        design!.Colors.Should().HaveCount(1);
    }

    [Test]
    public void SetLocal_gives_a_new_negative_id_and_drops_the_one_it_replaces()
    {
        var store = new GuildCloakDesignStore();

        var first = store.SetLocal(0, GuildCloakDesign.CreateDefault());
        var second = store.SetLocal(first, GuildCloakDesign.CreateDefault());

        first.Should().Be(-1);
        second.Should().Be(-2);
        store.TryGet(first, out _).Should().BeFalse();
        store.TryGet(second, out _).Should().BeTrue();
    }
}
```

Build (Global Constraints command). Expected: build FAILS with `The type or namespace name 'GuildCloakGrid' could not be found`.

- [ ] **Step 2: Write the grid**

Create `CLI/Chaos.Client.Rendering/GuildCloakGrid.cs`:

```csharp
#region
using DALib.Drawing;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     The outline of one cloak frame: which pixels it has, and the first and last pixel of each row. Coordinates are local
///     to the frame (0,0 is its top-left). Built for the three reference frames the design grids are painted on, and for
///     every frame the painter maps a design onto.
/// </summary>
public sealed class GuildCloakGrid
{
    private readonly bool[] Filled;
    private readonly int[] RowFirst;
    private readonly int[] RowLast;

    /// <param name="pixels">Palette indexes, row-major; 0 is empty.</param>
    public GuildCloakGrid(int width, int height, ReadOnlySpan<byte> pixels)
    {
        Width = width;
        Height = height;
        Filled = new bool[width * height];
        RowFirst = new int[height];
        RowLast = new int[height];
        TopRow = -1;
        BottomRow = -1;

        for (var y = 0; y < height; y++)
        {
            RowFirst[y] = -1;
            RowLast[y] = -1;

            for (var x = 0; x < width; x++)
            {
                if (pixels[(y * width) + x] == 0)
                    continue;

                Filled[(y * width) + x] = true;

                if (RowFirst[y] < 0)
                    RowFirst[y] = x;

                RowLast[y] = x;
            }

            if (RowFirst[y] < 0)
                continue;

            if (TopRow < 0)
                TopRow = y;

            BottomRow = y;
        }
    }

    public int BottomRow { get; }
    public int Height { get; }
    public bool IsEmpty => TopRow < 0;
    public int TopRow { get; }
    public int Width { get; }

    /// <summary>
    ///     The pixel at fractions (<paramref name="u" />, <paramref name="v" />) of this outline: v picks the row between the
    ///     top and bottom rows (an empty row moves to the nearest filled row), u picks the column between that row's first and
    ///     last pixel.
    /// </summary>
    public (int X, int Y) CellAt(float u, float v)
    {
        var y = (int)MathF.Round(TopRow + (v * (BottomRow - TopRow)));

        for (var d = 0; d <= BottomRow - TopRow; d++)
        {
            if ((y + d <= BottomRow) && (RowFirst[y + d] >= 0))
            {
                y += d;

                break;
            }

            if ((y - d >= TopRow) && (RowFirst[y - d] >= 0))
            {
                y -= d;

                break;
            }
        }

        var x = (int)MathF.Round(RowFirst[y] + (u * (RowLast[y] - RowFirst[y])));

        return (x, y);
    }

    public static GuildCloakGrid FromFrame(EpfFrame frame) => new(frame.PixelWidth, frame.PixelHeight, frame.Data);

    public static bool HasPixels(EpfFrame frame)
        => (frame.PixelWidth > 0) && (frame.PixelHeight > 0) && frame.Data.AsSpan().ContainsAnyExcept((byte)0);

    public bool IsFilled(int x, int y) => (x >= 0) && (y >= 0) && (x < Width) && (y < Height) && Filled[(y * Width) + x];

    /// <summary>The first and last pixel of a row, or (-1, -1) for an empty row.</summary>
    public (int First, int Last) Row(int y) => (RowFirst[y], RowLast[y]);
}
```

- [ ] **Step 3: Write the painter**

Create `CLI/Chaos.Client.Rendering/GuildCloakPainter.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using DALib.Drawing;
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Paints a guild cloak design onto one frame of the guild cloak sprite. Each pixel takes its color from the same place
///     (as a fraction of the outline) on a reference grid, and keeps the frame's own light and shadow. The numbers come from
///     the design session's mapping test (<c>Unora/.superpowers/brainstorm/345065-1790308407/spike/fullpaint.py</c>).
/// </summary>
public static class GuildCloakPainter
{
    /// <summary>Shade of an outline pixel (one with an empty neighbour above, below, left or right).</summary>
    public const float EDGE_SHADE = 0.42f;

    /// <summary>Shade of the darkest cloth. The frame pixel's brightness adds up to <see cref="LIGHT_SHADE" /> more.</summary>
    public const float BASE_SHADE = 0.78f;

    public const float LIGHT_SHADE = 0.75f;

    /// <summary>The brightness of the lightest cloth color in the cape's palette (slot 25).</summary>
    public const float FULL_BRIGHTNESS = 99f;

    /// <summary>The brightness a dye pixel gets when none of its neighbours is cloth.</summary>
    private const int DARK_BRIGHTNESS = 31;

    private const byte DYE_FIRST = 98;
    private const byte DYE_LAST = 103;

    /// <summary>
    ///     The brightness a pixel's shade comes from: the largest channel of its palette color. The old rune and hem are drawn
    ///     in the dye slots, so a dye pixel borrows the middle brightness of the cloth around it and the rune disappears.
    /// </summary>
    public static int Brightness(byte[] data, IList<SKColor> palette, int width, int height, int x, int y)
    {
        var index = data[(y * width) + x];

        if (index is < DYE_FIRST or > DYE_LAST)
            return MaxChannel(palette[index]);

        Span<int> near = stackalloc int[4];
        var count = 0;
        ReadOnlySpan<int> dx = [1, -1, 0, 0];
        ReadOnlySpan<int> dy = [0, 0, 1, -1];

        for (var i = 0; i < 4; i++)
        {
            var nx = x + dx[i];
            var ny = y + dy[i];

            if ((nx < 0) || (ny < 0) || (nx >= width) || (ny >= height))
                continue;

            var neighbour = data[(ny * width) + nx];

            if (neighbour is 0 or (>= DYE_FIRST and <= DYE_LAST))
                continue;

            near[count++] = MaxChannel(palette[neighbour]);
        }

        if (count == 0)
            return DARK_BRIGHTNESS;

        near[..count].Sort();

        return near[count / 2];
    }

    /// <summary>The color number at a cell. An unpainted cell takes the nearest painted cell in its row; an empty row gives 1.</summary>
    public static int ColorNumberAt(GuildCloakGrid reference, ReadOnlySpan<byte> cells, int x, int y, int colorCount)
    {
        var width = reference.Width;

        if (cells.Length != width * reference.Height)
            return 1;

        for (var d = 0; d < width; d++)
        {
            var left = x - d;
            var right = x + d;

            if ((left >= 0) && (left < width) && IsColor(cells[(y * width) + left], colorCount))
                return cells[(y * width) + left];

            if ((right >= 0) && (right < width) && IsColor(cells[(y * width) + right], colorCount))
                return cells[(y * width) + right];
        }

        return 1;
    }

    public static SKColor[] Paint(
        EpfFrame frame,
        IList<SKColor> palette,
        GuildCloakGrid reference,
        ReadOnlySpan<byte> cells,
        IReadOnlyList<SKColor> colors,
        bool flip)
        => Paint(
            frame.PixelWidth,
            frame.PixelHeight,
            frame.Data,
            palette,
            reference,
            cells,
            colors,
            flip);

    /// <summary>
    ///     The painted pixels of a frame, row-major, the frame's size. Empty pixels stay transparent. For a draw the renderer
    ///     will flip, <paramref name="flip" /> reads the design mirrored, so the flip turns it the right way round.
    /// </summary>
    public static SKColor[] Paint(
        int width,
        int height,
        byte[] data,
        IList<SKColor> palette,
        GuildCloakGrid reference,
        ReadOnlySpan<byte> cells,
        IReadOnlyList<SKColor> colors,
        bool flip)
    {
        var result = new SKColor[width * height];
        var target = new GuildCloakGrid(width, height, data);

        if (target.IsEmpty || reference.IsEmpty || (colors.Count == 0))
            return result;

        for (var y = target.TopRow; y <= target.BottomRow; y++)
        {
            (var first, var last) = target.Row(y);

            if (first < 0)
                continue;

            var v = target.BottomRow == target.TopRow ? 0.5f : (y - target.TopRow) / (float)(target.BottomRow - target.TopRow);

            for (var x = first; x <= last; x++)
            {
                if (data[(y * width) + x] == 0)
                    continue;

                var u = last == first ? 0.5f : (x - first) / (float)(last - first);

                if (flip)
                    u = 1f - u;

                (var cellX, var cellY) = reference.CellAt(u, v);
                var color = colors[ColorNumberAt(reference, cells, cellX, cellY, colors.Count) - 1];

                var shade = IsEdge(target, x, y)
                    ? EDGE_SHADE
                    : BASE_SHADE + (LIGHT_SHADE * Brightness(data, palette, width, height, x, y) / FULL_BRIGHTNESS);

                result[(y * width) + x] = Shade(color, shade);
            }
        }

        return result;
    }

    public static SKColor Shade(SKColor color, float shade)
        => new(
            Channel(color.Red, shade),
            Channel(color.Green, shade),
            Channel(color.Blue, shade),
            255);

    public static SKColor[] ToColors(IReadOnlyList<GuildCloakColor> colors)
    {
        var result = new SKColor[colors.Count];

        for (var i = 0; i < colors.Count; i++)
            result[i] = new SKColor(colors[i].R, colors[i].G, colors[i].B);

        return result;
    }

    /// <summary>Turns painted pixels into a layer image placed the way <c>Graphics.RenderImage</c> places a frame.</summary>
    public static SKImage ToImage(EpfFrame frame, SKColor[] pixels)
    {
        var offsetX = Math.Max(0, (int)frame.Left);
        var offsetY = Math.Max(0, (int)frame.Top);
        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        var bitmapWidth = width + offsetX;

        using var bitmap = new SKBitmap(
            bitmapWidth,
            height + offsetY,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        using var pixmap = bitmap.PeekPixels();
        var buffer = pixmap.GetPixelSpan<SKColor>();
        buffer.Clear();

        for (var y = 0; y < height; y++)
            pixels.AsSpan(y * width, width)
                  .CopyTo(buffer.Slice(((y + offsetY) * bitmapWidth) + offsetX, width));

        return SKImage.FromBitmap(bitmap);
    }

    private static byte Channel(byte value, float shade) => (byte)Math.Clamp((int)(value * shade), 0, 255);

    private static bool IsColor(byte value, int colorCount) => (value >= 1) && (value <= colorCount);

    private static bool IsEdge(GuildCloakGrid target, int x, int y)
        => !target.IsFilled(x + 1, y) || !target.IsFilled(x - 1, y) || !target.IsFilled(x, y + 1) || !target.IsFilled(x, y - 1);

    private static int MaxChannel(SKColor color) => Math.Max(color.Red, Math.Max(color.Green, color.Blue));
}
```

- [ ] **Step 4: Write the references and the store**

Create `CLI/Chaos.Client.Rendering/GuildCloakReferences.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using DALib.Drawing;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>Which of a design's three grids a cell belongs to.</summary>
public enum GuildCloakPart
{
    Back,
    Lining,
    Collar
}

/// <summary>
///     The guild cloak's three paintable views, from its male walk sheet: the back (frame 0, <c>c</c> layer), the lining
///     (frame 5, <c>g</c> layer) and the collar (frame 5, <c>c</c> layer). Also the male body's front frame, which the editor
///     shows faintly for placement.
/// </summary>
public sealed class GuildCloakReferences
{
    public GuildCloakReferences(EpfFrame backFrame, EpfFrame liningFrame, EpfFrame collarFrame, EpfFrame? bodyFront)
    {
        BackFrame = backFrame;
        LiningFrame = liningFrame;
        CollarFrame = collarFrame;
        BodyFront = bodyFront;
        Back = GuildCloakGrid.FromFrame(backFrame);
        Lining = GuildCloakGrid.FromFrame(liningFrame);
        Collar = GuildCloakGrid.FromFrame(collarFrame);
    }

    public GuildCloakGrid Back { get; }
    public EpfFrame BackFrame { get; }
    public EpfFrame? BodyFront { get; }
    public GuildCloakGrid Collar { get; }
    public EpfFrame CollarFrame { get; }
    public GuildCloakGrid Lining { get; }
    public EpfFrame LiningFrame { get; }

    /// <summary>True when the three frames have the grid sizes <see cref="GuildCloakProtocol" /> expects.</summary>
    public bool MatchesProtocol
        => (Back.Width == GuildCloakProtocol.BACK_WIDTH)
           && (Back.Height == GuildCloakProtocol.BACK_HEIGHT)
           && (Lining.Width == GuildCloakProtocol.LINING_WIDTH)
           && (Lining.Height == GuildCloakProtocol.LINING_HEIGHT)
           && (Collar.Width == GuildCloakProtocol.COLLAR_WIDTH)
           && (Collar.Height == GuildCloakProtocol.COLLAR_HEIGHT);

    public static byte[] Cells(GuildCloakDesign design, GuildCloakPart part)
        => part switch
        {
            GuildCloakPart.Back   => design.Back,
            GuildCloakPart.Lining => design.Lining,
            _                     => design.Collar
        };

    public GuildCloakGrid Grid(GuildCloakPart part)
        => part switch
        {
            GuildCloakPart.Back   => Back,
            GuildCloakPart.Lining => Lining,
            _                     => Collar
        };
}
```

Create `CLI/Chaos.Client.Rendering/GuildCloakDesignStore.cs`:

```csharp
#region
using System.Diagnostics.CodeAnalysis;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Guild cloak designs by id. Positive ids come from the server; the client asks for each one at most once every
///     <see cref="REQUEST_RETRY_MS" />. Negative ids are designs painted on this client (the editor's draft, a design under
///     review): every change gets a new id, so the preview never reuses a drawing of an older version. Game thread only.
/// </summary>
public sealed class GuildCloakDesignStore
{
    public const long REQUEST_RETRY_MS = 10_000;

    private readonly Dictionary<int, GuildCloakDesign> Designs = [];
    private readonly Dictionary<int, long> RequestedAtMs = [];
    private int NextLocalId = -1;

    public void Set(int designId, GuildCloakDesign design)
    {
        Designs[designId] = design;
        RequestedAtMs.Remove(designId);
    }

    /// <summary>Stores a design painted on this client under a new negative id, and drops the local design it replaces.</summary>
    public int SetLocal(int replacedId, GuildCloakDesign design)
    {
        if (replacedId < 0)
            Designs.Remove(replacedId);

        var id = NextLocalId--;
        Designs[id] = design;

        return id;
    }

    /// <summary>True when a server design is missing and was not asked for in the last <see cref="REQUEST_RETRY_MS" />.</summary>
    public bool ShouldRequest(int designId, long nowMs)
    {
        if ((designId <= 0) || Designs.ContainsKey(designId))
            return false;

        if (RequestedAtMs.TryGetValue(designId, out var askedAt) && ((nowMs - askedAt) < REQUEST_RETRY_MS))
            return false;

        RequestedAtMs[designId] = nowMs;

        return true;
    }

    public bool TryGet(int designId, [MaybeNullWhen(false)] out GuildCloakDesign design)
    {
        design = null;

        return (designId != 0) && Designs.TryGetValue(designId, out design);
    }
}
```

- [ ] **Step 5: Build and run the tests**

Build (Global Constraints command), then:

```bash
dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter "/*/*/GuildCloakPainterTests/*" --no-ansi
dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter "/*/*/GuildCloakDesignStoreTests/*" --no-ansi
```

Expected: all pass. If the test project cannot see `DALib.Drawing` or `SkiaSharp`, it already references SkiaSharp; add nothing else, and check `Chaos.Client.Rendering` is reachable through the `Chaos.Client` project reference (the existing `SpotlightMaskTests` use it the same way).

```json:metadata
{"files": ["Chaos.Client.Rendering/GuildCloakGrid.cs", "Chaos.Client.Rendering/GuildCloakPainter.cs", "Chaos.Client.Rendering/GuildCloakReferences.cs", "Chaos.Client.Rendering/GuildCloakDesignStore.cs", "Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs", "Tests/Chaos.Client.Tests/GuildCloakDesignStoreTests.cs"], "verifyCommand": "dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter \"/*/*/GuildCloakPainterTests/*\" --no-ansi", "acceptanceCriteria": ["own-grid paint takes each cell's color with inner and edge shades", "flip mirrors; other sizes map by fraction", "dye pixels take neighbours' middle brightness", "unpainted cell takes nearest painted in row, else 1; empty frame paints nothing", "CellAt skips empty rows and ends", "ToImage places pixels at left/top", "store: one request per 10 s, none for ids <= 0; local ids are new negatives and drop the replaced one"], "modelTier": "mechanical"}
```

---

### Task 7: Client plumbing — messages, the renderer hook and world state

**Goal:** The client receives looks and designs, asks for missing designs, and draws every guild cloak with its wearer's design (plain until the design arrives).

**Files:**
- Modify: `CLI/Chaos.Client.Networking/Definitions/Delegates.cs` (4 delegates after `StageLightingBoardHandler`)
- Modify: `CLI/Chaos.Client.Networking/ConnectionManager.cs` (4 events, 4 handlers, 3 send methods)
- Modify: `CLI/Chaos.Client.Rendering/AislingRenderer.cs`
- Modify: `CLI/Chaos.Client/Collections/WorldState.cs`
- Create: `CLI/Chaos.Client/Screens/WorldScreen.GuildCloak.cs`
- Modify: `CLI/Chaos.Client/Screens/WorldScreen.cs` (wire and unwire)

**Acceptance Criteria:**
- [ ] `GuildCloakLook` sets the player's appearance design id (kept across later `DisplayAisling`s) and asks for a missing design once
- [ ] `GuildCloakDesign` stores the design and clears the saved drawings of every player showing it
- [ ] A guild cloak accessory with a loaded design draws through `GuildCloakPainter`: lining grid for the `g` layer of front-facing frames, collar grid for their `c` layer, back grid otherwise; flipped draws read the design mirrored
- [ ] Layer cache keys include the design id and the flip, so no drawing is reused across designs or directions
- [ ] Without the art files or with the wrong frame sizes, the client draws the plain cloak and never throws
- [ ] The whole client test project passes

**Verify:** client build (Global Constraints), then `dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Delegates, events, handlers and sends**

In `CLI/Chaos.Client.Networking/Definitions/Delegates.cs`, after `StageLightingBoardHandler`:

```csharp

/// <summary>Handles the guild cloak editor opening or its status line changing.</summary>
public delegate void GuildCloakEditorHandler(GuildCloakEditorArgs args);

/// <summary>Handles which design one player's guild cloak shows.</summary>
public delegate void GuildCloakLookHandler(GuildCloakLookArgs args);

/// <summary>Handles one approved guild cloak design arriving.</summary>
public delegate void GuildCloakDesignHandler(GuildCloakDesignArgs args);

/// <summary>Handles the guild cloak review list opening or changing.</summary>
public delegate void GuildCloakReviewListHandler(GuildCloakReviewListArgs args);
```

In `CLI/Chaos.Client.Networking/ConnectionManager.cs`:

After `public event StageLightingBoardHandler? OnStageLightingBoard;`:

```csharp

    /// <summary>Fired when the server opens the guild cloak editor or updates its status line.</summary>
    public event GuildCloakEditorHandler? OnGuildCloakEditor;

    /// <summary>Fired when the server says which design a player's guild cloak shows.</summary>
    public event GuildCloakLookHandler? OnGuildCloakLook;

    /// <summary>Fired when an approved guild cloak design arrives.</summary>
    public event GuildCloakDesignHandler? OnGuildCloakDesign;

    /// <summary>Fired when the server opens or updates the guild cloak review window.</summary>
    public event GuildCloakReviewListHandler? OnGuildCloakReviewList;
```

After `public void SendStageLightingInteraction(StageLightingInteractionArgs args) => SendIfWorld(args);`:

```csharp

    /// <summary>Sends a save or submit from the guild cloak editor. The server checks the sender and the design.</summary>
    public void SendGuildCloakEditorInteraction(GuildCloakEditorInteractionArgs args) => SendIfWorld(args);

    /// <summary>Asks for one approved guild cloak design.</summary>
    public void SendGuildCloakDesignRequest(int designId) => SendIfWorld(new GuildCloakDesignRequestArgs { DesignId = designId });

    /// <summary>Sends an admin's approve or reject. The server checks the sender is an admin.</summary>
    public void SendGuildCloakReviewInteraction(GuildCloakReviewInteractionArgs args) => SendIfWorld(args);
```

After `PacketHandlers[(byte)ServerOpCode.StageLightingBoard] = HandleStageLightingBoard;`:

```csharp
        PacketHandlers[(byte)ServerOpCode.GuildCloakEditor] = HandleGuildCloakEditor;
        PacketHandlers[(byte)ServerOpCode.GuildCloakLook] = HandleGuildCloakLook;
        PacketHandlers[(byte)ServerOpCode.GuildCloakDesign] = HandleGuildCloakDesign;
        PacketHandlers[(byte)ServerOpCode.GuildCloakReviewList] = HandleGuildCloakReviewList;
```

After `HandleStageLightingBoard`:

```csharp

    private void HandleGuildCloakEditor(ServerPacket pkt)
    {
        var args = Client.Deserialize<GuildCloakEditorArgs>(in pkt);
        OnGuildCloakEditor?.Invoke(args);
    }

    private void HandleGuildCloakLook(ServerPacket pkt)
    {
        var args = Client.Deserialize<GuildCloakLookArgs>(in pkt);
        OnGuildCloakLook?.Invoke(args);
    }

    private void HandleGuildCloakDesign(ServerPacket pkt)
    {
        var args = Client.Deserialize<GuildCloakDesignArgs>(in pkt);
        OnGuildCloakDesign?.Invoke(args);
    }

    private void HandleGuildCloakReviewList(ServerPacket pkt)
    {
        var args = Client.Deserialize<GuildCloakReviewListArgs>(in pkt);
        OnGuildCloakReviewList?.Invoke(args);
    }
```

Add any missing `using Chaos.Networking.Entities.Client;` / `Server;` lines.

- [ ] **Step 2: The renderer hook**

In `CLI/Chaos.Client.Rendering/AislingRenderer.cs`:

1. In `AislingAppearance`, after `public int FaceSprite { get; init; }`:

```csharp
    /// <summary>The guild cloak design id this player's cloak shows (0 = plain). Negative ids are designs painted on this client.</summary>
    public int GuildCloakDesignId { get; init; }
```

2. Make the walk suffix public for the preview windows: `private const string WALK_ANIM = "01";` → `public const string WALK_ANIM = "01";`.

3. Add to the class (fields next to the other caches):

```csharp
    private GuildCloakReferences? CloakReferences;
    private bool CloakReferencesLoaded;

    /// <summary>Guild cloak designs by id, filled by the world screen from the server and by the cloak windows.</summary>
    public GuildCloakDesignStore GuildCloaks { get; } = new();

    /// <summary>
    ///     The guild cloak's three paintable views from its male walk sheet, loaded once. Null when the game files lack the
    ///     guild cloak sprite or its frames are not the sizes <see cref="GuildCloakProtocol" /> expects.
    /// </summary>
    public GuildCloakReferences? GetGuildCloakReferences()
    {
        if (CloakReferencesLoaded)
            return CloakReferences;

        CloakReferencesLoaded = true;

        var outside = DrawData.GetEquipmentEpf('c', true, $"mc{GuildCloakProtocol.SPRITE:D3}{WALK_ANIM}");
        var inside = DrawData.GetEquipmentEpf('g', true, $"mg{GuildCloakProtocol.SPRITE:D3}{WALK_ANIM}");
        var body = DrawData.GetEquipmentEpf('m', true, $"mm{BODY_ID:D3}{WALK_ANIM}");

        if (outside is null || inside is null || (outside.Count < 6) || (inside.Count < 6))
            return null;

        var references = new GuildCloakReferences(
            outside[UP_IDLE_FRAME],
            inside[RIGHT_IDLE_FRAME],
            outside[RIGHT_IDLE_FRAME],
            body is { Count: > RIGHT_IDLE_FRAME } ? body[RIGHT_IDLE_FRAME] : null);

        CloakReferences = references.MatchesProtocol ? references : null;

        return CloakReferences;
    }
```

(`UP_IDLE_FRAME` is 0 and `RIGHT_IDLE_FRAME` is 5, the back-facing and front-facing standing frames the grids were measured on.)

4. Give `LayerCacheKey` two optional members at the end:

```csharp
    private readonly record struct LayerCacheKey(
        char TypeLetter,
        int SpriteId,
        int ColorCode,
        bool IsMale,
        KhanPalOverrideType PaletteOverride,
        string AnimSuffix,
        int FrameIndex,
        int IdleFallbackFrame,
        int DesignId = 0,
        bool Flip = false);
```

5. Pass the flip into layer rendering. In `Render(in AislingAppearance appearance, int frameIndex, out int contentBottomY, ...)`, change the `RenderAllLayers(...)` call to pass `flipHorizontal` as a new last argument. Give `RenderAllLayers` a new last parameter `bool flip = false` (other callers keep the default).

6. In `RenderAllLayers`, replace each of the six accessory `RenderEquipLayer(` calls (Acc1C, Acc1G, Acc2C, Acc2G, Acc3C, Acc3G) with `RenderAccessoryLayer(` and add `flip` as the last argument. The Accessory1 block becomes:

```csharp
        if ((appearance.Accessory1Sprite > 0) && !isMounted)
        {
            layers[(int)LayerSlot.Acc1C] = RenderAccessoryLayer(
                'c',
                appearance.Accessory1Sprite,
                appearance.Accessory1Color,
                in appearance,
                frameIndex,
                anim,
                idleFallbackFrame,
                flip);

            layers[(int)LayerSlot.Acc1G] = RenderAccessoryLayer(
                'g',
                appearance.Accessory1Sprite,
                appearance.Accessory1Color,
                in appearance,
                frameIndex,
                anim,
                idleFallbackFrame,
                flip);
        }
```

and the same for Accessory2 and Accessory3.

7. Add after `RenderEquipLayer`:

```csharp
    /// <summary>An accessory layer: the guild cloak paints its wearer's design when that design is loaded; everything else dyes as usual.</summary>
    private LayerInfo? RenderAccessoryLayer(
        char typeLetter,
        int spriteId,
        DisplayColor dyeColor,
        in AislingAppearance appearance,
        int frameIndex,
        string anim,
        int idleFallbackFrame,
        bool flip)
    {
        if ((spriteId == GuildCloakProtocol.SPRITE) && GuildCloaks.TryGet(appearance.GuildCloakDesignId, out var design))
        {
            var painted = RenderGuildCloakLayer(
                typeLetter,
                spriteId,
                appearance.GuildCloakDesignId,
                design,
                in appearance,
                frameIndex,
                anim,
                idleFallbackFrame,
                flip);

            if (painted.HasValue)
                return painted;
        }

        return RenderEquipLayer(
            typeLetter,
            spriteId,
            dyeColor,
            in appearance,
            frameIndex,
            anim,
            idleFallbackFrame);
    }

    /// <summary>
    ///     One guild cloak layer with a design painted on. A frame faces the viewer when its behind-the-body (<c>g</c>) layer
    ///     has pixels: then the <c>g</c> layer uses the lining grid and the <c>c</c> layer the collar grid. Otherwise both use
    ///     the back grid. Null when the art or the design cannot be used, so the caller draws the plain cloak.
    /// </summary>
    private LayerInfo? RenderGuildCloakLayer(
        char typeLetter,
        int spriteId,
        int designId,
        GuildCloakDesign design,
        in AislingAppearance appearance,
        int frameIndex,
        string anim,
        int idleFallbackFrame,
        bool flip)
    {
        var references = GetGuildCloakReferences();

        if (references is null || !design.IsValid())
            return null;

        var cacheKey = new LayerCacheKey(
            typeLetter,
            spriteId,
            0,
            appearance.IsMale,
            appearance.OverrideType,
            anim,
            frameIndex,
            idleFallbackFrame,
            designId,
            flip);

        var cachedImage = TryGetCachedLayerImage(in cacheKey);

        if (cachedImage is not null)
            return new LayerInfo(cachedImage, typeLetter);

        (var epf, var resolvedFrame) = ResolveLayerEpf(
            typeLetter,
            spriteId,
            in appearance,
            frameIndex,
            anim,
            idleFallbackFrame);

        if (epf is null || (resolvedFrame < 0))
            return null;

        var frame = epf[resolvedFrame];

        if ((frame.PixelWidth == 0) || (frame.PixelHeight == 0))
            return null;

        (var behindEpf, var behindFrame) = ResolveLayerEpf(
            'g',
            spriteId,
            in appearance,
            frameIndex,
            anim,
            idleFallbackFrame);

        var frontFacing = behindEpf is not null && (behindFrame >= 0) && GuildCloakGrid.HasPixels(behindEpf[behindFrame]);

        var part = (typeLetter, frontFacing) switch
        {
            ('g', true) => GuildCloakPart.Lining,
            ('c', true) => GuildCloakPart.Collar,
            _           => GuildCloakPart.Back
        };

        var palette = ResolvePalette(
            DrawData.GetPaletteLookup(typeLetter),
            spriteId,
            DisplayColor.Default,
            appearance.OverrideType);

        if (palette is null)
            return null;

        var pixels = GuildCloakPainter.Paint(
            frame,
            palette,
            references.Grid(part),
            GuildCloakReferences.Cells(design, part),
            GuildCloakPainter.ToColors(design.Colors),
            flip);

        var image = GuildCloakPainter.ToImage(frame, pixels);
        CacheLayerImage(in cacheKey, image);

        return new LayerInfo(image, typeLetter);
    }
```

Add `using Chaos.DarkAges.Definitions;` if the file lacks it (it already uses `DisplayColor` from there).

- [ ] **Step 3: World state**

In `CLI/Chaos.Client/Collections/WorldState.cs`:

1. Add next to `StageLightingPanel`:

```csharp
    /// <summary>
    ///     Which guild cloak design each player's cloak shows (0 = plain), from the server's GuildCloakLook. Kept across
    ///     DisplayAisling updates; cleared with the entities.
    /// </summary>
    public static Dictionary<uint, int> GuildCloakLooks { get; } = [];
```

2. In `AddOrUpdateAisling`, in the `new AislingAppearance { ... }` initializer, add `GuildCloakDesignId = GuildCloakLooks.GetValueOrDefault(args.Id),`.

3. In `Clear()`, after `Entities.Clear();`, add `GuildCloakLooks.Clear();`. In `RemoveEntity(uint id)`, add `GuildCloakLooks.Remove(id);`.

4. Add:

```csharp
    /// <summary>Records which design a player's guild cloak shows, and updates their appearance so they are redrawn.</summary>
    public static void ApplyGuildCloakLook(uint entityId, int designId)
    {
        GuildCloakLooks[entityId] = designId;

        if (Entities.TryGetValue(entityId, out var entity)
            && entity.Appearance is { } appearance
            && (appearance.GuildCloakDesignId != designId))
            entity.Appearance = appearance with { GuildCloakDesignId = designId };
    }
```

- [ ] **Step 4: The world screen wiring**

Create `CLI/Chaos.Client/Screens/WorldScreen.GuildCloak.cs`:

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Guild cloaks: which design each player's cloak shows, and the designs themselves.</summary>
public sealed partial class WorldScreen
{
    private void WireGuildCloak()
    {
        Game.Connection.OnGuildCloakLook += HandleGuildCloakLook;
        Game.Connection.OnGuildCloakDesign += HandleGuildCloakDesign;
    }

    private void UnwireGuildCloak()
    {
        Game.Connection.OnGuildCloakLook -= HandleGuildCloakLook;
        Game.Connection.OnGuildCloakDesign -= HandleGuildCloakDesign;
    }

    private void HandleGuildCloakLook(GuildCloakLookArgs args)
    {
        WorldState.ApplyGuildCloakLook(args.EntityId, args.DesignId);

        if (Game.AislingRenderer.GuildCloaks.ShouldRequest(args.DesignId, Environment.TickCount64))
            Game.Connection.SendGuildCloakDesignRequest(args.DesignId);
    }

    private void HandleGuildCloakDesign(GuildCloakDesignArgs args)
    {
        if (!args.Design.IsValid())
            return;

        Game.AislingRenderer.GuildCloaks.Set(args.DesignId, args.Design);

        //players already drawn plain while the design was on its way
        foreach ((var entityId, var designId) in WorldState.GuildCloakLooks)
            if (designId == args.DesignId)
                Game.AislingRenderer.RemoveCachedEntity(entityId);
    }
}
```

In `CLI/Chaos.Client/Screens/WorldScreen.cs`, add `WireGuildCloak();` right after `WireStageLighting();`, and `UnwireGuildCloak();` right after `UnwireStageLighting();`.

- [ ] **Step 5: Build and run the whole client test project**

Build (Global Constraints command), then `dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`.
Expected: `Build succeeded`, all tests pass.

```json:metadata
{"files": ["Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client.Networking/ConnectionManager.cs", "Chaos.Client.Rendering/AislingRenderer.cs", "Chaos.Client/Collections/WorldState.cs", "Chaos.Client/Screens/WorldScreen.GuildCloak.cs", "Chaos.Client/Screens/WorldScreen.cs"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server", "acceptanceCriteria": ["GuildCloakLook sets the appearance id, kept across DisplayAisling, and requests a missing design once", "GuildCloakDesign stores and clears cached drawings of players showing it", "guild cloak layers paint through GuildCloakPainter with the right grid per layer and facing; flip mirrors", "layer cache keys include design id and flip", "missing art or wrong sizes draws the plain cloak without throwing", "whole client test project passes"], "modelTier": "standard"}
```

---
### Task 8: The cloak editor window

**Goal:** The leader's editor: front and back canvases, six colors with the Theatre color picker, pencil, fill, pick, mirror painting, undo and redo, a walking preview on both bodies, Save draft and Submit, opened by the server's `GuildCloakEditor`.

**Files:**
- Create: `CLI/Chaos.Client/ViewModel/GuildCloakEditorModel.cs`
- Create: `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakCanvas.cs`
- Create: `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakSwatches.cs`
- Create: `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakPreview.cs`
- Create: `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakEditorControl.cs`
- Modify: `CLI/Chaos.Client/Screens/WorldScreen.GuildCloak.cs`
- Test: `CLI/Tests/Chaos.Client.Tests/GuildCloakEditorModelTests.cs`

**Acceptance Criteria:**
- [ ] The model paints only cells inside the outline; pencil, fill (one connected color region), pick and mirror work as tested
- [ ] A stroke, a color drag and an added color each undo as one step; undo keeps at most 50 steps; redo restores
- [ ] At most 6 colors; the painted design is always valid
- [ ] The window opens with the server's design and status, and the status line shows Draft / Waiting for review / Approved / Rejected: reason, plus "unsaved changes"
- [ ] The preview walks in four facings on both bodies and shows the current paint (updated at most every 150 ms while painting)
- [ ] Save draft and Submit send a copy of the design, then stay dim for 2 seconds; Close with unsaved changes warns once, then closes
- [ ] Missing guild cloak art shows an orange-bar message instead of the window

**Verify:** client build (Global Constraints), then `dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter "/*/*/GuildCloakEditorModelTests/*" --no-ansi` → all pass; whole client test project passes

**Steps:**

- [ ] **Step 1: Write the failing model tests**

Create `CLI/Tests/Chaos.Client.Tests/GuildCloakEditorModelTests.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildCloakEditorModelTests
{
    /// <summary>Every cell is inside the outline except the back's top-left corner.</summary>
    private static GuildCloakEditorModel Model() => new((part, x, y) => !((part == GuildCloakPart.Back) && (x == 0) && (y == 0)));

    /// <summary>A model with a second color (red), which is selected.</summary>
    private static GuildCloakEditorModel TwoColors()
    {
        var model = Model();
        model.AddColor(new GuildCloakColor(200, 0, 0)).Should().BeTrue();

        return model;
    }

    [Test]
    public void Pencil_paints_one_cell_with_the_selected_color()
    {
        var model = TwoColors();

        model.Apply(GuildCloakPart.Back, 5, 5);

        model.CellAt(GuildCloakPart.Back, 5, 5).Should().Be(2);
        model.CellAt(GuildCloakPart.Back, 6, 5).Should().Be(1);
        model.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Cells_outside_the_outline_or_the_grid_are_never_painted()
    {
        var model = Model();
        model.AddColor(new GuildCloakColor(200, 0, 0));
        model.Load(model.Design);
        model.SelectColor(2);

        model.Apply(GuildCloakPart.Back, 0, 0);
        model.Apply(GuildCloakPart.Back, -1, 3);
        model.Apply(GuildCloakPart.Back, GuildCloakProtocol.BACK_WIDTH, 3);

        model.CellAt(GuildCloakPart.Back, 0, 0).Should().Be(1);
        model.CanUndo.Should().BeFalse();
    }

    [Test]
    public void Mirror_paints_the_matching_cell_on_the_other_half()
    {
        var model = TwoColors();
        model.Mirror = true;

        model.Apply(GuildCloakPart.Collar, 2, 3);

        model.CellAt(GuildCloakPart.Collar, 2, 3).Should().Be(2);
        model.CellAt(GuildCloakPart.Collar, GuildCloakProtocol.COLLAR_WIDTH - 3, 3).Should().Be(2);
    }

    [Test]
    public void Fill_changes_only_the_connected_cells_of_one_color()
    {
        var model = TwoColors();
        model.BeginStroke();

        for (var y = 0; y < GuildCloakProtocol.COLLAR_HEIGHT; y++)
            model.Apply(GuildCloakPart.Collar, 4, y);

        model.EndStroke();
        model.AddColor(new GuildCloakColor(0, 0, 200));
        model.Tool = GuildCloakTool.Fill;

        model.Apply(GuildCloakPart.Collar, 0, 0);

        model.CellAt(GuildCloakPart.Collar, 3, 7).Should().Be(3);
        model.CellAt(GuildCloakPart.Collar, 4, 0).Should().Be(2);
        model.CellAt(GuildCloakPart.Collar, 5, 0).Should().Be(1);
    }

    [Test]
    public void Pick_selects_the_color_of_a_cell()
    {
        var model = TwoColors();
        model.Apply(GuildCloakPart.Back, 5, 5);
        model.SelectColor(1);
        model.Tool = GuildCloakTool.Pick;

        model.Apply(GuildCloakPart.Back, 5, 5);

        model.SelectedColor.Should().Be(2);
    }

    [Test]
    public void A_stroke_undoes_as_one_step_and_redo_brings_it_back()
    {
        var model = TwoColors();
        model.BeginStroke();
        model.Apply(GuildCloakPart.Back, 5, 5);
        model.Apply(GuildCloakPart.Back, 6, 5);
        model.EndStroke();

        model.Undo();

        model.CellAt(GuildCloakPart.Back, 5, 5).Should().Be(1);
        model.CellAt(GuildCloakPart.Back, 6, 5).Should().Be(1);
        model.Design.Colors.Should().HaveCount(2);

        model.Redo();

        model.CellAt(GuildCloakPart.Back, 5, 5).Should().Be(2);
        model.CellAt(GuildCloakPart.Back, 6, 5).Should().Be(2);
    }

    [Test]
    public void A_color_drag_undoes_as_one_step()
    {
        var model = TwoColors();
        model.BeginStroke();
        model.SetColor(2, new GuildCloakColor(10, 10, 10));
        model.SetColor(2, new GuildCloakColor(20, 20, 20));
        model.EndStroke();

        model.Undo();

        model.Design.Colors[1].Should().Be(new GuildCloakColor(200, 0, 0));
    }

    [Test]
    public void Undo_keeps_at_most_fifty_steps()
    {
        var model = TwoColors();

        for (var i = 0; i < 60; i++)
        {
            model.SelectColor(i % 2 == 0 ? 1 : 2);
            model.Apply(GuildCloakPart.Back, 5, 5);
        }

        var undone = 0;

        while (model.CanUndo)
        {
            model.Undo();
            undone++;
        }

        undone.Should().Be(GuildCloakEditorModel.MAX_UNDO);
    }

    [Test]
    public void Six_colors_is_the_limit()
    {
        var model = Model();

        for (var i = 0; i < 5; i++)
            model.AddColor(new GuildCloakColor(1, 2, 3)).Should().BeTrue();

        model.AddColor(new GuildCloakColor(1, 2, 3)).Should().BeFalse();
        model.Design.Colors.Should().HaveCount(GuildCloakProtocol.MAX_COLORS);
    }

    [Test]
    public void Load_resets_the_history_the_selection_and_the_dirty_flag()
    {
        var model = TwoColors();
        model.Apply(GuildCloakPart.Back, 5, 5);

        model.Load(GuildCloakDesign.CreateDefault());

        model.IsDirty.Should().BeFalse();
        model.CanUndo.Should().BeFalse();
        model.SelectedColor.Should().Be(1);
        model.Design.Colors.Should().HaveCount(1);
    }

    [Test]
    public void The_painted_design_is_always_valid()
    {
        var model = TwoColors();
        model.Tool = GuildCloakTool.Fill;

        model.Apply(GuildCloakPart.Lining, 3, 3);

        model.Design.IsValid().Should().BeTrue();
    }

    [Test]
    [Arguments(GuildCloakStatus.Draft, "", false, "Draft")]
    [Arguments(GuildCloakStatus.Waiting, "", true, "Waiting for review - unsaved changes")]
    [Arguments(GuildCloakStatus.Approved, "", false, "Approved")]
    [Arguments(GuildCloakStatus.Rejected, "Too bright.", false, "Rejected: Too bright.")]
    public void StatusText_matches_the_spec(GuildCloakStatus status, string reason, bool dirty, string expected)
        => GuildCloakEditorModel.StatusText(status, reason, dirty).Should().Be(expected);
}
```

Build (Global Constraints command). Expected: build FAILS with `The type or namespace name 'GuildCloakEditorModel' could not be found`.

- [ ] **Step 2: Write the model**

Create `CLI/Chaos.Client/ViewModel/GuildCloakEditorModel.cs`:

```csharp
#region
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>The guild cloak editor's paint tools.</summary>
public enum GuildCloakTool
{
    Pencil,
    Fill,
    Pick
}

/// <summary>
///     The guild cloak editor's state: the design being painted, the chosen color and tool, mirror painting, and undo and
///     redo. It only paints cells inside the cloak's outline. <see cref="Version" /> changes on every visible change, so the
///     window knows when to redraw.
/// </summary>
public sealed class GuildCloakEditorModel
{
    public const int MAX_UNDO = 50;

    private readonly Func<GuildCloakPart, int, int, bool> IsPaintable;
    private readonly List<GuildCloakDesign> RedoSteps = [];
    private readonly List<GuildCloakDesign> UndoSteps = [];
    private GuildCloakDesign? StrokeStart;
    private bool StrokeRecorded;

    /// <param name="isPaintable">Whether a cell lies inside the cloak's outline (its reference frame has a pixel there).</param>
    public GuildCloakEditorModel(Func<GuildCloakPart, int, int, bool> isPaintable) => IsPaintable = isPaintable;

    public bool CanRedo => RedoSteps.Count > 0;
    public bool CanUndo => UndoSteps.Count > 0;
    public GuildCloakDesign Design { get; private set; } = GuildCloakDesign.CreateDefault();
    public bool IsDirty { get; private set; }
    public bool Mirror { get; set; }
    public int SelectedColor { get; private set; } = 1;
    public GuildCloakTool Tool { get; set; } = GuildCloakTool.Pencil;
    public int Version { get; private set; }

    /// <summary>Adds a color and selects it. False when the design already has <see cref="GuildCloakProtocol.MAX_COLORS" />.</summary>
    public bool AddColor(GuildCloakColor color)
    {
        if (Design.Colors.Count >= GuildCloakProtocol.MAX_COLORS)
            return false;

        Record();
        Design.Colors.Add(color);
        SelectedColor = Design.Colors.Count;
        Changed();

        return true;
    }

    /// <summary>
    ///     Uses the current tool on a cell. Pencil and fill change the design, and the mirrored cell too when mirror is on;
    ///     pick selects the cell's color.
    /// </summary>
    public void Apply(GuildCloakPart part, int x, int y)
    {
        if (!CanPaint(part, x, y))
            return;

        switch (Tool)
        {
            case GuildCloakTool.Pencil:
                SetCell(part, x, y);

                if (Mirror)
                    SetCell(part, MirrorX(part, x), y);

                break;

            case GuildCloakTool.Fill:
                Fill(part, x, y);

                if (Mirror)
                    Fill(part, MirrorX(part, x), y);

                break;

            case GuildCloakTool.Pick:
                SelectColor(CellAt(part, x, y));

                break;
        }
    }

    /// <summary>Starts a stroke: every change until <see cref="EndStroke" /> undoes as one step.</summary>
    public void BeginStroke()
    {
        StrokeStart = Design.DeepCopy();
        StrokeRecorded = false;
    }

    public byte CellAt(GuildCloakPart part, int x, int y) => GuildCloakReferences.Cells(Design, part)[(y * WidthOf(part)) + x];

    public void EndStroke() => StrokeStart = null;

    public static int HeightOf(GuildCloakPart part)
        => part switch
        {
            GuildCloakPart.Back   => GuildCloakProtocol.BACK_HEIGHT,
            GuildCloakPart.Lining => GuildCloakProtocol.LINING_HEIGHT,
            _                     => GuildCloakProtocol.COLLAR_HEIGHT
        };

    /// <summary>Starts over from a design sent by the server: no history, color 1 selected, nothing unsaved.</summary>
    public void Load(GuildCloakDesign design)
    {
        Design = design.DeepCopy();
        SelectedColor = 1;
        UndoSteps.Clear();
        RedoSteps.Clear();
        StrokeStart = null;
        IsDirty = false;
        Version++;
    }

    public void MarkSaved()
    {
        IsDirty = false;
        Version++;
    }

    public void Redo()
    {
        if (RedoSteps.Count == 0)
            return;

        UndoSteps.Add(Design);
        Design = Pop(RedoSteps);
        AfterHistoryStep();
    }

    public void SelectColor(int number)
    {
        if ((number < 1) || (number > Design.Colors.Count) || (number == SelectedColor))
            return;

        SelectedColor = number;
        Version++;
    }

    /// <summary>Changes one color. The color picker calls this while dragging, inside a stroke, so the whole drag undoes as one step.</summary>
    public void SetColor(int number, GuildCloakColor color)
    {
        if ((number < 1) || (number > Design.Colors.Count) || (Design.Colors[number - 1] == color))
            return;

        Record();
        Design.Colors[number - 1] = color;
        Changed();
    }

    /// <summary>The editor's status line. The reason shows only for a rejection.</summary>
    public static string StatusText(GuildCloakStatus status, string reason, bool dirty)
    {
        var text = status switch
        {
            GuildCloakStatus.Waiting  => "Waiting for review",
            GuildCloakStatus.Approved => "Approved",
            GuildCloakStatus.Rejected => $"Rejected: {reason}",
            _                         => "Draft"
        };

        return dirty ? $"{text} - unsaved changes" : text;
    }

    public void Undo()
    {
        if (UndoSteps.Count == 0)
            return;

        RedoSteps.Add(Design);
        Design = Pop(UndoSteps);
        AfterHistoryStep();
    }

    public static int WidthOf(GuildCloakPart part)
        => part switch
        {
            GuildCloakPart.Back   => GuildCloakProtocol.BACK_WIDTH,
            GuildCloakPart.Lining => GuildCloakProtocol.LINING_WIDTH,
            _                     => GuildCloakProtocol.COLLAR_WIDTH
        };

    private void AfterHistoryStep()
    {
        StrokeStart = null;
        SelectedColor = Math.Clamp(SelectedColor, 1, Design.Colors.Count);
        IsDirty = true;
        Version++;
    }

    private bool CanPaint(GuildCloakPart part, int x, int y)
        => (x >= 0) && (y >= 0) && (x < WidthOf(part)) && (y < HeightOf(part)) && IsPaintable(part, x, y);

    private void Changed()
    {
        IsDirty = true;
        Version++;
    }

    private void Fill(GuildCloakPart part, int x, int y)
    {
        if (!CanPaint(part, x, y))
            return;

        var cells = GuildCloakReferences.Cells(Design, part);
        var width = WidthOf(part);
        var from = cells[(y * width) + x];

        if (from == SelectedColor)
            return;

        Record();
        var pending = new Stack<(int X, int Y)>();
        pending.Push((x, y));

        while (pending.Count > 0)
        {
            (var cx, var cy) = pending.Pop();

            if (!CanPaint(part, cx, cy) || (cells[(cy * width) + cx] != from))
                continue;

            cells[(cy * width) + cx] = (byte)SelectedColor;
            pending.Push((cx + 1, cy));
            pending.Push((cx - 1, cy));
            pending.Push((cx, cy + 1));
            pending.Push((cx, cy - 1));
        }

        Changed();
    }

    private static int MirrorX(GuildCloakPart part, int x) => WidthOf(part) - 1 - x;

    private static GuildCloakDesign Pop(List<GuildCloakDesign> steps)
    {
        var last = steps[^1];
        steps.RemoveAt(steps.Count - 1);

        return last;
    }

    /// <summary>Saves the design as an undo step before it changes: once per stroke, or once per change outside a stroke.</summary>
    private void Record()
    {
        if (StrokeStart is not null)
        {
            if (StrokeRecorded)
                return;

            UndoSteps.Add(StrokeStart);
            StrokeRecorded = true;
        } else
            UndoSteps.Add(Design.DeepCopy());

        if (UndoSteps.Count > MAX_UNDO)
            UndoSteps.RemoveAt(0);

        RedoSteps.Clear();
    }

    private void SetCell(GuildCloakPart part, int x, int y)
    {
        if (!CanPaint(part, x, y))
            return;

        var cells = GuildCloakReferences.Cells(Design, part);
        var index = (y * WidthOf(part)) + x;

        if (cells[index] == SelectedColor)
            return;

        Record();
        cells[index] = (byte)SelectedColor;
        Changed();
    }
}
```

Build and run the model tests. Expected: all pass.

- [ ] **Step 3: Write the canvas, the swatches and the preview**

Create `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakCanvas.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>Which view a guild cloak canvas shows.</summary>
public enum GuildCloakView
{
    Front,
    Back
}

/// <summary>
///     One zoomed view of a guild cloak design in flat colors: the back, or the front (the lining, a faint body for placement,
///     and the collar on top). Unless <see cref="ReadOnly" />, left-button strokes raise cell events in grid coordinates.
/// </summary>
public sealed class GuildCloakCanvas : UIElement
{
    private const float GHOST_ALPHA = 0.3f;

    private static readonly Color Background = new(28, 28, 34);
    private static readonly Color GhostTint = new(200, 190, 170);

    //premultiplied: black at alpha 60
    private static readonly Color GridLine = new(0, 0, 0, 60);

    private readonly Point CollarAt;
    private readonly Point GhostAt;
    private readonly Point LiningAt;
    private readonly GuildCloakReferences References;
    private readonly GuildCloakView View;
    private readonly int ViewHeight;
    private readonly int ViewWidth;
    private readonly int Zoom;
    private GuildCloakDesign? Design;
    private bool Dirty = true;
    private bool Painting;
    private Texture2D? Texture;

    public GuildCloakCanvas(GuildCloakReferences references, GuildCloakView view, int zoom)
    {
        References = references;
        View = view;
        Zoom = zoom;

        if (view == GuildCloakView.Back)
        {
            ViewWidth = references.Back.Width;
            ViewHeight = references.Back.Height;
        } else
        {
            var lining = references.LiningFrame;
            var collar = references.CollarFrame;
            var left = Math.Min(lining.Left, collar.Left);
            var top = Math.Min(lining.Top, collar.Top);
            ViewWidth = Math.Max(lining.Right, collar.Right) - left;
            ViewHeight = Math.Max(lining.Bottom, collar.Bottom) - top;
            LiningAt = new Point(lining.Left - left, lining.Top - top);
            CollarAt = new Point(collar.Left - left, collar.Top - top);

            //accessory frames draw 27 px left of body frames on the character's canvas
            if (references.BodyFront is { } body)
                GhostAt = new Point(body.Left - AislingRenderer.GetLayerOffsetX('c') - left, body.Top - top);
        }

        Width = ViewWidth * zoom;
        Height = ViewHeight * zoom;
    }

    /// <summary>No strokes: the review window shows designs without editing them.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Raised for each cell a stroke touches, in the grid of the part it belongs to.</summary>
    public event Action<GuildCloakPart, int, int>? CellPainted;

    public event Action? StrokeEnded;
    public event Action? StrokeStarted;

    public override void Dispose()
    {
        Texture?.Dispose();
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        if (Dirty)
            RebuildTexture();

        var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
        DrawRectClipped(spriteBatch, bounds, Background);
        DrawTextureFitted(spriteBatch, Texture, bounds, Color.White);

        if (Zoom < 4)
            return;

        for (var x = 1; x < ViewWidth; x++)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (x * Zoom), ScreenY, 1, Height), GridLine);

        for (var y = 1; y < ViewHeight; y++)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + (y * Zoom), Width, 1), GridLine);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (ReadOnly || (e.Button != MouseButton.Left))
            return;

        Painting = true;
        StrokeStarted?.Invoke();
        PaintAt(e.ScreenX, e.ScreenY);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Painting)
            return;

        PaintAt(e.ScreenX, e.ScreenY);
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (!Painting)
            return;

        Painting = false;
        StrokeEnded?.Invoke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();

        if (!Painting)
            return;

        Painting = false;
        StrokeEnded?.Invoke();
    }

    public void SetDesign(GuildCloakDesign design)
    {
        Design = design;
        Dirty = true;
    }

    private void PaintAt(int screenX, int screenY)
    {
        if ((screenX < ScreenX) || (screenY < ScreenY))
            return;

        var x = (screenX - ScreenX) / Zoom;
        var y = (screenY - ScreenY) / Zoom;

        if ((x >= ViewWidth) || (y >= ViewHeight))
            return;

        if (View == GuildCloakView.Back)
        {
            CellPainted?.Invoke(GuildCloakPart.Back, x, y);

            return;
        }

        //the collar is on top: paint it where it has a pixel, else the lining underneath
        if (References.Collar.IsFilled(x - CollarAt.X, y - CollarAt.Y))
            CellPainted?.Invoke(GuildCloakPart.Collar, x - CollarAt.X, y - CollarAt.Y);
        else
            CellPainted?.Invoke(GuildCloakPart.Lining, x - LiningAt.X, y - LiningAt.Y);
    }

    private void RebuildTexture()
    {
        Dirty = false;
        var pixels = new Color[ViewWidth * ViewHeight];

        if (Design is { } design)
        {
            if (View == GuildCloakView.Back)
                Stamp(pixels, References.Back, design.Back, design.Colors, Point.Zero);
            else
            {
                Stamp(pixels, References.Lining, design.Lining, design.Colors, LiningAt);
                StampGhost(pixels);
                Stamp(pixels, References.Collar, design.Collar, design.Colors, CollarAt);
            }
        }

        Texture ??= new Texture2D(TextureConverter.Device, ViewWidth, ViewHeight);
        Texture.SetData(pixels);
    }

    private void Stamp(Color[] pixels, GuildCloakGrid grid, byte[] cells, List<GuildCloakColor> colors, Point at)
    {
        for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                if (!grid.IsFilled(x, y))
                    continue;

                var number = cells[(y * grid.Width) + x];
                var color = (number >= 1) && (number <= colors.Count) ? colors[number - 1] : colors[0];
                pixels[((y + at.Y) * ViewWidth) + x + at.X] = new Color(color.R, color.G, color.B);
            }
    }

    private void StampGhost(Color[] pixels)
    {
        if (References.BodyFront is not { } body)
            return;

        var width = body.PixelWidth;

        for (var y = 0; y < body.PixelHeight; y++)
            for (var x = 0; x < width; x++)
            {
                if (body.Data[(y * width) + x] == 0)
                    continue;

                var px = x + GhostAt.X;
                var py = y + GhostAt.Y;

                if ((px < 0) || (py < 0) || (px >= ViewWidth) || (py >= ViewHeight))
                    continue;

                var index = (py * ViewWidth) + px;

                pixels[index] = pixels[index].A == 0
                    ? GhostTint * GHOST_ALPHA
                    : Color.Lerp(pixels[index], GhostTint, GHOST_ALPHA);
            }
    }
}
```

Create `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakSwatches.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     The editor's six color boxes. Clicking a color selects it; clicking the selected color, or an empty box (which adds a
///     gray color), asks to open the color picker.
/// </summary>
public sealed class GuildCloakSwatches : UIElement
{
    public const int BOX = 22;
    public const int COLUMNS = 2;
    public const int GAP = 4;

    private readonly GuildCloakEditorModel Model;

    public GuildCloakSwatches(GuildCloakEditorModel model)
    {
        Model = model;
        var rows = GuildCloakProtocol.MAX_COLORS / COLUMNS;
        Width = (COLUMNS * BOX) + ((COLUMNS - 1) * GAP);
        Height = (rows * BOX) + ((rows - 1) * GAP);
    }

    /// <summary>Raised with a color number (1-based) when the picker should open for it.</summary>
    public event Action<int>? EditRequested;

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        var colors = Model.Design.Colors;

        for (var i = 0; i < GuildCloakProtocol.MAX_COLORS; i++)
        {
            var box = BoxBounds(i);

            if (i < colors.Count)
            {
                DrawRectClipped(spriteBatch, box, new Color(colors[i].R, colors[i].G, colors[i].B));
                DrawBorder(spriteBatch, box, i + 1 == Model.SelectedColor ? Color.White : LegendColors.Gray);
            } else
            {
                DrawBorder(spriteBatch, box, LegendColors.Gray);
                DrawTextClipped(spriteBatch, new Vector2(box.X + 8, box.Y + 5), "+", LegendColors.Gray, false);
            }
        }
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        for (var i = 0; i < GuildCloakProtocol.MAX_COLORS; i++)
        {
            if (!BoxBounds(i).Contains(e.ScreenX, e.ScreenY))
                continue;

            var number = i + 1;

            if (number > Model.Design.Colors.Count)
            {
                if (Model.AddColor(new GuildCloakColor(128, 128, 128)))
                    EditRequested?.Invoke(Model.SelectedColor);
            } else if (number == Model.SelectedColor)
                EditRequested?.Invoke(number);
            else
                Model.SelectColor(number);

            return;
        }
    }

    private Rectangle BoxBounds(int index)
        => new(
            ScreenX + ((index % COLUMNS) * (BOX + GAP)),
            ScreenY + ((index / COLUMNS) * (BOX + GAP)),
            BOX,
            BOX);
}
```

Create `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakPreview.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     A walking figure in a guild cloak for the editor and review windows, modeled on the beauty shop's preview. It turns
///     through four facings and switches body type. Each walk step is rendered once and kept until the design, the facing or
///     the body changes.
/// </summary>
public sealed class GuildCloakPreview : UIElement
{
    public const int FACING_COUNT = 4;
    private const int STEP_MS = 160;
    private const int WALK_STEPS = 5;

    //(frame, flip, isFront): Down, Right, Up, Left. The walk sheet holds up (0-4) and right (5-9); down = right flipped,
    //left = up flipped.
    private static readonly (int Frame, bool Flip, bool IsFront)[] Facings =
    [
        (5, true, true),
        (5, false, true),
        (0, false, false),
        (0, true, false)
    ];

    private readonly Texture2D?[] Frames = new Texture2D?[WALK_STEPS];
    private readonly Texture2D Pedestal;
    private readonly AislingRenderer Renderer;
    private Gender BodyGender = Gender.Male;
    private int DesignId;
    private int Facing;
    private int Step;
    private double StepElapsedMs;

    public GuildCloakPreview(AislingRenderer renderer, int width, int height)
    {
        Renderer = renderer;
        Width = width;
        Height = height;
        Pedestal = DialogFrame.BuildRecessedTexture(new SKColor(24, 22, 30), width, height);
        IsHitTestVisible = false;
    }

    public bool IsMale => BodyGender == Gender.Male;

    public override void Dispose()
    {
        ReleaseFrames();
        Pedestal.Dispose();
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        DrawTexture(spriteBatch, Pedestal, new Vector2(ScreenX, ScreenY), Color.White);

        var figure = Frames[Step] ??= RenderStep(Step);

        if (figure is null)
            return;

        var scale = ((figure.Height * 2) <= (Height - 12)) && ((figure.Width * 2) <= Width) ? 2 : 1;
        var x = ScreenX + (Width / 2) - (AislingRenderer.CANVAS_CENTER_X * scale);
        var y = ScreenY + (Height / 2) - (AislingRenderer.BODY_CENTER_Y * scale);
        DrawTextureFitted(spriteBatch, figure, new Rectangle(x, y, figure.Width * scale, figure.Height * scale), Color.White);
    }

    /// <summary>Drops the rendered steps. Called when the window hides, so a closed window holds no GPU memory.</summary>
    public void ReleaseFrames()
    {
        for (var i = 0; i < Frames.Length; i++)
        {
            Frames[i]?.Dispose();
            Frames[i] = null;
        }
    }

    public void SetDesignId(int designId)
    {
        if (designId == DesignId)
            return;

        DesignId = designId;
        ReleaseFrames();
    }

    public void ToggleBody()
    {
        BodyGender = IsMale ? Gender.Female : Gender.Male;
        ReleaseFrames();
    }

    public void Turn(int delta)
    {
        Facing = (Facing + delta + FACING_COUNT) % FACING_COUNT;
        ReleaseFrames();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        StepElapsedMs += gameTime.ElapsedGameTime.TotalMilliseconds;

        if (StepElapsedMs < STEP_MS)
            return;

        StepElapsedMs = 0;
        Step = (Step + 1) % WALK_STEPS;
    }

    private Texture2D? RenderStep(int step)
    {
        (var frame, var flip, var isFront) = Facings[Facing];

        var appearance = new AislingAppearance
        {
            Gender = BodyGender,
            BodySpriteId = AislingRenderer.BODY_ID,
            HeadSprite = 1,
            Accessory1Sprite = GuildCloakProtocol.SPRITE,
            GuildCloakDesignId = DesignId
        };

        return Renderer.Render(in appearance, frame + step, AislingRenderer.WALK_ANIM, flip, isFront);
    }
}
```

- [ ] **Step 4: Write the editor window**

Create `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakEditorControl.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Controls.World.Popups.Theatre;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     The guild leader's cloak editor (spec: Chaos.Client/docs/superpowers/specs/2026-09-25-guild-cloak-design.md): front and
///     back canvases, six colors with the Theatre color picker, pencil, fill, pick, mirror painting, undo and redo, a
///     walking preview, and Save draft / Submit. Opened by the server's GuildCloakEditor Open from Quill.
/// </summary>
public sealed class GuildCloakEditorControl : FramedDialogPanelBase
{
    private const int PANEL_HEIGHT = 360;
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;
    private const int LEFT = 16;
    private const int GAP = 8;
    private const int TITLE_TOP = 10;
    private const int CAPTION_TOP = 30;
    private const int CONTENT_TOP = 44;
    private const int ZOOM = 5;
    private const int TOOL_WIDTH = 84;
    private const int PREVIEW_WIDTH = 134;
    private const int PREVIEW_HEIGHT = 205;
    private const int TURN_WIDTH = 28;
    private const int BODY_WIDTH = 70;
    private const int SAVE_WIDTH = 90;
    private const int SUBMIT_WIDTH = 80;
    private const int BOTTOM_ROW_TOP = PANEL_HEIGHT - 60;
    private const long SEND_COOLDOWN_MS = 2_000;
    private const long PREVIEW_INTERVAL_MS = 150;

    private readonly GuildCloakCanvas BackCanvas;
    private readonly CustomButton BodyButton;
    private readonly CustomButton FillButton;
    private readonly GuildCloakCanvas FrontCanvas;
    private readonly CustomButton MirrorButton;
    private readonly GuildCloakEditorModel Model;
    private readonly CustomButton PencilButton;
    private readonly CustomButton PickButton;
    private readonly StageColorPicker Picker;
    private readonly GuildCloakPreview Preview;
    private readonly CustomButton RedoButton;
    private readonly AislingRenderer Renderer;
    private readonly CustomButton SaveButton;
    private readonly UILabel StatusLabel;
    private readonly CustomButton SubmitButton;
    private readonly CustomButton UndoButton;

    private bool CloseArmed;
    private int EditingColor;
    private long LastPreviewMs;
    private long LastSendMs = long.MinValue / 2;
    private bool Painting;
    private int PreviewDesignId;
    private bool PreviewStale;
    private int ShownVersion = -1;
    private GuildCloakStatus Status;
    private string StatusReason = string.Empty;

    public GuildCloakEditorControl(AislingRenderer renderer, GuildCloakReferences references)
        : base("_nsett", false)
    {
        Renderer = renderer;
        Model = new GuildCloakEditorModel((part, x, y) => references.Grid(part).IsFilled(x, y));
        Name = "GuildCloakEditor";
        Visible = false;
        UsesControlStack = true;

        FrontCanvas = new GuildCloakCanvas(references, GuildCloakView.Front, ZOOM)
        {
            X = LEFT + TOOL_WIDTH + GAP,
            Y = CONTENT_TOP
        };

        BackCanvas = new GuildCloakCanvas(references, GuildCloakView.Back, ZOOM)
        {
            X = FrontCanvas.X + FrontCanvas.Width + GAP,
            Y = CONTENT_TOP
        };

        var previewLeft = BackCanvas.X + BackCanvas.Width + GAP;
        Width = previewLeft + PREVIEW_WIDTH + LEFT;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(RequestClose, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Caption("Guild Cloak", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("COLORS", LEFT, CAPTION_TOP, TOOL_WIDTH, color: LegendColors.Gray);
        Caption("FRONT", FrontCanvas.X, CAPTION_TOP, FrontCanvas.Width, color: LegendColors.Gray);
        Caption("BACK", BackCanvas.X, CAPTION_TOP, BackCanvas.Width, color: LegendColors.Gray);
        Caption("PREVIEW", previewLeft, CAPTION_TOP, PREVIEW_WIDTH, color: LegendColors.Gray);

        var swatches = new GuildCloakSwatches(Model)
        {
            X = LEFT,
            Y = CONTENT_TOP
        };

        swatches.EditRequested += OpenPicker;
        AddChild(swatches);

        var toolTop = CONTENT_TOP + swatches.Height + 10;
        PencilButton = ToolButton("Pencil", ref toolTop, () => Model.Tool = GuildCloakTool.Pencil);
        FillButton = ToolButton("Fill", ref toolTop, () => Model.Tool = GuildCloakTool.Fill);
        PickButton = ToolButton("Pick color", ref toolTop, () => Model.Tool = GuildCloakTool.Pick);
        MirrorButton = ToolButton("Mirror", ref toolTop, () => Model.Mirror = !Model.Mirror);
        UndoButton = ToolButton("Undo", ref toolTop, Model.Undo);
        RedoButton = ToolButton("Redo", ref toolTop, Model.Redo);

        foreach (var canvas in new[] { FrontCanvas, BackCanvas })
        {
            canvas.StrokeStarted += () =>
            {
                Painting = true;
                Model.BeginStroke();
            };

            canvas.CellPainted += Model.Apply;

            canvas.StrokeEnded += () =>
            {
                Painting = false;
                Model.EndStroke();
                PreviewStale = true;
            };

            AddChild(canvas);
        }

        Preview = new GuildCloakPreview(renderer, PREVIEW_WIDTH, PREVIEW_HEIGHT)
        {
            X = previewLeft,
            Y = CONTENT_TOP
        };

        AddChild(Preview);

        var turnTop = CONTENT_TOP + PREVIEW_HEIGHT + 4;
        AddButton("<", TURN_WIDTH, previewLeft, turnTop, () => Preview.Turn(-1));
        AddButton(">", TURN_WIDTH, previewLeft + TURN_WIDTH + 4, turnTop, () => Preview.Turn(1));
        BodyButton = AddButton("Female", BODY_WIDTH, previewLeft + PREVIEW_WIDTH - BODY_WIDTH, turnTop, ToggleBody);

        SubmitButton = AddButton(
            "Submit",
            SUBMIT_WIDTH,
            Width - LEFT - SUBMIT_WIDTH,
            BOTTOM_ROW_TOP,
            () => Send(GuildCloakEditorAction.Submit));

        SaveButton = AddButton(
            "Save draft",
            SAVE_WIDTH,
            SubmitButton.X - GAP - SAVE_WIDTH,
            BOTTOM_ROW_TOP,
            () => Send(GuildCloakEditorAction.SaveDraft));

        StatusLabel = Caption(string.Empty, LEFT, BOTTOM_ROW_TOP, SaveButton.X - GAP - LEFT);
        StatusLabel.WordWrap = true;
        StatusLabel.Height = TextRenderer.CHAR_HEIGHT * 3;
        StatusLabel.VerticalAlignment = VerticalAlignment.Top;

        //added last so it draws over the canvases while open
        Picker = new StageColorPicker
        {
            X = LEFT + swatches.Width + 6,
            Y = CONTENT_TOP
        };

        Picker.ColorChanged += color => Model.SetColor(EditingColor, new GuildCloakColor(color.R, color.G, color.B));

        Picker.Closed += () =>
        {
            Model.EndStroke();
            PreviewStale = true;
        };

        AddChild(Picker);
    }

    /// <summary>Raised with the action and a copy of the design when the leader presses Save draft or Submit.</summary>
    public event Action<GuildCloakEditorAction, GuildCloakDesign>? SaveRequested;

    /// <summary>A save or submit went through: nothing is unsaved, and the status may have changed.</summary>
    public void ApplyStatus(GuildCloakEditorArgs args)
    {
        Model.MarkSaved();
        SetStatus(args.Status, args.RejectionReason);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();
        Picker.Visible = false;
        Preview.ReleaseFrames();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            RequestClose();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Loads the server's design and status and shows the window.</summary>
    public void Open(GuildCloakEditorArgs args)
    {
        Model.Load(args.Design);
        SetStatus(args.Status, args.RejectionReason);
        CloseArmed = false;
        Picker.Visible = false;
        PreviewStale = true;
        Show();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        var now = Environment.TickCount64;

        if (Model.Version != ShownVersion)
        {
            ShownVersion = Model.Version;
            CloseArmed = false;
            FrontCanvas.SetDesign(Model.Design);
            BackCanvas.SetDesign(Model.Design);
            RefreshControls();
            PreviewStale = true;
        }

        //while painting, redraw the walking preview at most every 150 ms
        if (PreviewStale && (!Painting || ((now - LastPreviewMs) >= PREVIEW_INTERVAL_MS)))
        {
            PreviewStale = false;
            LastPreviewMs = now;
            PreviewDesignId = Renderer.GuildCloaks.SetLocal(PreviewDesignId, Model.Design.DeepCopy());
            Preview.SetDesignId(PreviewDesignId);
        }

        var canSend = (now - LastSendMs) >= SEND_COOLDOWN_MS;
        SaveButton.Enabled = canSend;
        SubmitButton.Enabled = canSend;
    }

    private CustomButton AddButton(
        string caption,
        int width,
        int x,
        int y,
        Action onClick)
    {
        var button = new CustomButton(caption, width)
        {
            X = x,
            Y = y
        };

        button.Clicked += () => onClick();
        AddChild(button);

        return button;
    }

    private UILabel Caption(
        string text,
        int x,
        int y,
        int width,
        HorizontalAlignment alignment = HorizontalAlignment.Left,
        Color? color = null)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color ?? LegendColors.White,
            IsHitTestVisible = false,
            Text = text
        };

        AddChild(label);

        return label;
    }

    private void OpenPicker(int number)
    {
        EditingColor = number;
        var color = Model.Design.Colors[number - 1];
        Model.BeginStroke();
        Picker.Open(new Color(color.R, color.G, color.B));
    }

    private void RefreshControls()
    {
        PencilButton.Selected = Model.Tool == GuildCloakTool.Pencil;
        FillButton.Selected = Model.Tool == GuildCloakTool.Fill;
        PickButton.Selected = Model.Tool == GuildCloakTool.Pick;
        MirrorButton.Selected = Model.Mirror;
        UndoButton.Enabled = Model.CanUndo;
        RedoButton.Enabled = Model.CanRedo;
        StatusLabel.Text = GuildCloakEditorModel.StatusText(Status, StatusReason, Model.IsDirty);
    }

    private void RequestClose()
    {
        if (Model.IsDirty && !CloseArmed)
        {
            CloseArmed = true;
            StatusLabel.Text = "Unsaved changes. Press Close again to discard them.";

            return;
        }

        Hide();
    }

    private void Send(GuildCloakEditorAction action)
    {
        var now = Environment.TickCount64;

        if ((now - LastSendMs) < SEND_COOLDOWN_MS)
            return;

        LastSendMs = now;
        CloseArmed = false;
        SaveRequested?.Invoke(action, Model.Design.DeepCopy());
    }

    private void SetStatus(GuildCloakStatus status, string reason)
    {
        Status = status;
        StatusReason = reason ?? string.Empty;
        RefreshControls();
    }

    private void ToggleBody()
    {
        Preview.ToggleBody();
        BodyButton.Caption = Preview.IsMale ? "Female" : "Male";
    }

    private CustomButton ToolButton(string caption, ref int top, Action onClick)
    {
        var button = AddButton(
            caption,
            TOOL_WIDTH,
            LEFT,
            top,
            () =>
            {
                onClick();
                RefreshControls();
            });

        top += CustomButton.HEIGHT + 3;

        return button;
    }
}
```

- [ ] **Step 5: Wire the editor into the world screen**

In `CLI/Chaos.Client/Screens/WorldScreen.GuildCloak.cs`:

1. Add usings: `using Chaos.Client.Controls.World.Popups.GuildCloak;`, `using Chaos.DarkAges.Definitions;`, `using Chaos.Networking.Entities.Client;`.
2. Add inside the class:

```csharp
    private const string MISSING_ART = "Your game files are missing the guild cloak. Please update the game.";

    //built on first use: it needs the guild cloak art, which older game files lack
    private GuildCloakEditorControl? GuildCloakEditor;

    private GuildCloakEditorControl? EnsureGuildCloakEditor()
    {
        if (GuildCloakEditor is not null)
            return GuildCloakEditor;

        var references = Game.AislingRenderer.GetGuildCloakReferences();

        if (references is null)
            return null;

        GuildCloakEditor = new GuildCloakEditorControl(Game.AislingRenderer, references)
        {
            ZIndex = 2
        };

        GuildCloakEditor.SaveRequested += SendGuildCloakEdit;
        Root.AddChild(GuildCloakEditor);

        return GuildCloakEditor;
    }

    private void HandleGuildCloakEditor(GuildCloakEditorArgs args)
    {
        var editor = EnsureGuildCloakEditor();

        if (editor is null)
        {
            WorldState.Chat.AddOrangeBarMessage(MISSING_ART);

            return;
        }

        if (args.Type == GuildCloakEditorType.Open)
            editor.Open(args);
        else
            editor.ApplyStatus(args);
    }

    private void SendGuildCloakEdit(GuildCloakEditorAction action, GuildCloakDesign design)
        => Game.Connection.SendGuildCloakEditorInteraction(
            new GuildCloakEditorInteractionArgs
            {
                Action = action,
                Design = design
            });
```

3. In `WireGuildCloak`, add `Game.Connection.OnGuildCloakEditor += HandleGuildCloakEditor;`. In `UnwireGuildCloak`, add `Game.Connection.OnGuildCloakEditor -= HandleGuildCloakEditor;` and:

```csharp
        if (GuildCloakEditor is not null)
            GuildCloakEditor.SaveRequested -= SendGuildCloakEdit;
```

4. Update the partial's summary to "Guild cloaks: which design each player's cloak shows, the designs themselves, and the editor window."

- [ ] **Step 6: Build and run the whole client test project**

Build (Global Constraints command), then `dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`.
Expected: `Build succeeded`, all tests pass. The window itself is checked by hand in Task 10's handover list; its layout constants are starting values and may be nudged by eye.

```json:metadata
{"files": ["Chaos.Client/ViewModel/GuildCloakEditorModel.cs", "Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakCanvas.cs", "Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakSwatches.cs", "Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakPreview.cs", "Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakEditorControl.cs", "Chaos.Client/Screens/WorldScreen.GuildCloak.cs", "Tests/Chaos.Client.Tests/GuildCloakEditorModelTests.cs"], "verifyCommand": "dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --treenode-filter \"/*/*/GuildCloakEditorModelTests/*\" --no-ansi", "acceptanceCriteria": ["model paints only inside the outline; pencil, fill, pick, mirror as tested", "stroke, color drag and added color undo as one step; 50-step cap; redo", "max 6 colors; painted design always valid", "window opens with server design and status; status line text per spec", "preview walks 4 facings on both bodies, updated at most every 150 ms while painting", "Save/Submit send a copy then dim 2 s; Close with unsaved changes warns once", "missing art shows an orange-bar message"], "modelTier": "frontier"}
```

---

### Task 9: The review window

**Goal:** The admins' review window: waiting designs in a paged list, read-only front and back views, the walking preview, and Approve / Reject with a reason, opened by the server's `GuildCloakReviewList`.

**Files:**
- Create: `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakReviewControl.cs`
- Modify: `CLI/Chaos.Client/Screens/WorldScreen.GuildCloak.cs`
- Modify: `CLI/CLAUDE.md` (the popups list)

**Acceptance Criteria:**
- [ ] `Open` shows the list with the first design selected; `Update` replaces the list and keeps the selection when that submission is still waiting
- [ ] The list pages 8 at a time; an empty list shows "No designs are waiting."
- [ ] Approve sends the selected submission id; Reject is enabled only with a 1–200 character reason and sends it trimmed
- [ ] Missing guild cloak art shows an orange-bar message instead of the window
- [ ] `CLAUDE.md` lists the `GuildCloak/` popups
- [ ] The whole client test project passes

**Verify:** client build (Global Constraints), then `dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the review window**

Create `CLI/Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakReviewControl.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     The admins' guild cloak review window: the waiting designs (oldest first, 8 per page), the selected design's front and
///     back, a walking preview, and Approve / Reject with a reason. Opened by the server's GuildCloakReviewList Open from the
///     admin trinket.
/// </summary>
public sealed class GuildCloakReviewControl : FramedDialogPanelBase
{
    private const int PANEL_HEIGHT = 330;
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;
    private const int LEFT = 16;
    private const int GAP = 8;
    private const int TITLE_TOP = 10;
    private const int CAPTION_TOP = 30;
    private const int CONTENT_TOP = 44;
    private const int LIST_WIDTH = 120;
    private const int PAGE_SIZE = 8;
    private const int ZOOM = 4;
    private const int PREVIEW_WIDTH = 120;
    private const int PREVIEW_HEIGHT = 180;
    private const int SMALL_BUTTON = 28;
    private const int DECIDE_WIDTH = 80;

    private readonly CustomButton ApproveButton;
    private readonly GuildCloakCanvas BackCanvas;
    private readonly UILabel EmptyLabel;
    private readonly CustomButton[] EntryButtons = new CustomButton[PAGE_SIZE];
    private readonly GuildCloakCanvas FrontCanvas;
    private readonly UILabel InfoLabel;
    private readonly CustomButton NextPageButton;
    private readonly UILabel PageLabel;
    private readonly CustomButton PrevPageButton;
    private readonly GuildCloakPreview Preview;
    private readonly CustomTextBox ReasonBox;
    private readonly CustomButton RejectButton;
    private readonly AislingRenderer Renderer;

    private IReadOnlyList<GuildCloakReviewEntry> Entries = [];
    private int Page;
    private int PreviewDesignId;
    private int SelectedIndex = -1;

    public GuildCloakReviewControl(AislingRenderer renderer, GuildCloakReferences references)
        : base("_nsett", false)
    {
        Renderer = renderer;
        Name = "GuildCloakReview";
        Visible = false;
        UsesControlStack = true;

        FrontCanvas = new GuildCloakCanvas(references, GuildCloakView.Front, ZOOM)
        {
            X = LEFT + LIST_WIDTH + GAP,
            Y = CONTENT_TOP,
            ReadOnly = true
        };

        BackCanvas = new GuildCloakCanvas(references, GuildCloakView.Back, ZOOM)
        {
            X = FrontCanvas.X + FrontCanvas.Width + GAP,
            Y = CONTENT_TOP,
            ReadOnly = true
        };

        var previewLeft = BackCanvas.X + BackCanvas.Width + GAP;
        Width = previewLeft + PREVIEW_WIDTH + LEFT;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Caption("Guild Cloak Review", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("WAITING", LEFT, CAPTION_TOP, LIST_WIDTH, color: LegendColors.Gray);
        Caption("FRONT", FrontCanvas.X, CAPTION_TOP, FrontCanvas.Width, color: LegendColors.Gray);
        Caption("BACK", BackCanvas.X, CAPTION_TOP, BackCanvas.Width, color: LegendColors.Gray);
        Caption("PREVIEW", previewLeft, CAPTION_TOP, PREVIEW_WIDTH, color: LegendColors.Gray);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var slot = i;

            EntryButtons[i] = AddButton(
                string.Empty,
                LIST_WIDTH,
                LEFT,
                CONTENT_TOP + (i * (CustomButton.HEIGHT + 3)),
                () => Select((Page * PAGE_SIZE) + slot));
        }

        var pageTop = CONTENT_TOP + (PAGE_SIZE * (CustomButton.HEIGHT + 3)) + 4;
        PrevPageButton = AddButton("<", SMALL_BUTTON, LEFT, pageTop, () => TurnPage(-1));
        NextPageButton = AddButton(">", SMALL_BUTTON, LEFT + SMALL_BUTTON + 4, pageTop, () => TurnPage(1));
        PageLabel = Caption(string.Empty, LEFT + (2 * SMALL_BUTTON) + 10, pageTop + 5, LIST_WIDTH - (2 * SMALL_BUTTON) - 10, color: LegendColors.Gray);

        AddChild(FrontCanvas);
        AddChild(BackCanvas);

        Preview = new GuildCloakPreview(renderer, PREVIEW_WIDTH, PREVIEW_HEIGHT)
        {
            X = previewLeft,
            Y = CONTENT_TOP
        };

        AddChild(Preview);

        var turnTop = CONTENT_TOP + PREVIEW_HEIGHT + 4;
        AddButton("<", SMALL_BUTTON, previewLeft, turnTop, () => Preview.Turn(-1));
        AddButton(">", SMALL_BUTTON, previewLeft + SMALL_BUTTON + 4, turnTop, () => Preview.Turn(1));
        AddButton("Body", 50, previewLeft + PREVIEW_WIDTH - 50, turnTop, Preview.ToggleBody);

        EmptyLabel = Caption(
            "No designs are waiting.",
            FrontCanvas.X,
            CONTENT_TOP + 60,
            (BackCanvas.X + BackCanvas.Width) - FrontCanvas.X,
            HorizontalAlignment.Center,
            LegendColors.Gray);

        var infoTop = CONTENT_TOP + Math.Max(FrontCanvas.Height, BackCanvas.Height) + 6;
        var infoWidth = previewLeft - FrontCanvas.X - GAP;
        InfoLabel = Caption(string.Empty, FrontCanvas.X, infoTop, infoWidth);

        var reasonTop = infoTop + TextRenderer.CHAR_HEIGHT + 6;

        ReasonBox = new CustomTextBox
        {
            X = FrontCanvas.X,
            Y = reasonTop,
            Width = infoWidth,
            Height = CustomButton.HEIGHT,
            MaxLength = GuildCloakProtocol.MAX_REASON_CHARS,
            HintText = "Reason for rejecting"
        };

        AddChild(ReasonBox);

        var decideTop = reasonTop + CustomButton.HEIGHT + 6;
        ApproveButton = AddButton("Approve", DECIDE_WIDTH, FrontCanvas.X, decideTop, () => Decide(GuildCloakReviewAction.Approve));

        RejectButton = AddButton(
            "Reject",
            DECIDE_WIDTH,
            FrontCanvas.X + DECIDE_WIDTH + GAP,
            decideTop,
            () => Decide(GuildCloakReviewAction.Reject));
    }

    private GuildCloakReviewEntry? Selected
        => (SelectedIndex >= 0) && (SelectedIndex < Entries.Count) ? Entries[SelectedIndex] : null;

    /// <summary>Raised when the admin approves or rejects the selected design.</summary>
    public event Action<GuildCloakReviewInteractionArgs>? DecisionRequested;

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();
        Preview.ReleaseFrames();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Shows the window with a fresh list.</summary>
    public void Open(IReadOnlyList<GuildCloakReviewEntry> entries)
    {
        SetEntries(entries);
        Show();
    }

    /// <summary>Replaces the list after a decision, if the window is open.</summary>
    public void Refresh(IReadOnlyList<GuildCloakReviewEntry> entries)
    {
        if (Visible)
            SetEntries(entries);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        var hasEntry = Selected is not null;
        ApproveButton.Enabled = hasEntry;
        RejectButton.Enabled = hasEntry && ReasonBox.Text.Trim().Length is >= 1 and <= GuildCloakProtocol.MAX_REASON_CHARS;
    }

    private CustomButton AddButton(
        string caption,
        int width,
        int x,
        int y,
        Action onClick)
    {
        var button = new CustomButton(caption, width)
        {
            X = x,
            Y = y
        };

        button.Clicked += () => onClick();
        AddChild(button);

        return button;
    }

    private UILabel Caption(
        string text,
        int x,
        int y,
        int width,
        HorizontalAlignment alignment = HorizontalAlignment.Left,
        Color? color = null)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color ?? LegendColors.White,
            IsHitTestVisible = false,
            Text = text
        };

        AddChild(label);

        return label;
    }

    private void Decide(GuildCloakReviewAction action)
    {
        if (Selected is not { } entry)
            return;

        var reason = ReasonBox.Text.Trim();

        if ((action == GuildCloakReviewAction.Reject) && (reason.Length == 0))
            return;

        DecisionRequested?.Invoke(
            new GuildCloakReviewInteractionArgs
            {
                Action = action,
                SubmissionId = entry.SubmissionId,
                Reason = action == GuildCloakReviewAction.Reject ? reason : string.Empty
            });
    }

    private void RefreshList()
    {
        var pages = Math.Max(1, (Entries.Count + PAGE_SIZE - 1) / PAGE_SIZE);
        Page = Math.Clamp(Page, 0, pages - 1);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var index = (Page * PAGE_SIZE) + i;
            var button = EntryButtons[i];
            button.Visible = index < Entries.Count;

            if (!button.Visible)
                continue;

            button.Caption = Entries[index].GuildName;
            button.Selected = index == SelectedIndex;
        }

        PageLabel.Text = $"{Page + 1}/{pages}";
        PrevPageButton.Enabled = Page > 0;
        NextPageButton.Enabled = Page < (pages - 1);
    }

    private void Select(int index)
    {
        if (index >= Entries.Count)
            return;

        SelectedIndex = index;
        ReasonBox.Text = string.Empty;

        var entry = Selected;
        FrontCanvas.Visible = entry is not null;
        BackCanvas.Visible = entry is not null;
        Preview.Visible = entry is not null;
        EmptyLabel.Visible = entry is null;
        InfoLabel.Text = entry is null ? string.Empty : $"{entry.GuildName}, by {entry.LeaderName}, {entry.SubmittedAtUtc:yyyy-MM-dd HH:mm} UTC";

        if (entry is not null)
        {
            FrontCanvas.SetDesign(entry.Design);
            BackCanvas.SetDesign(entry.Design);
            PreviewDesignId = Renderer.GuildCloaks.SetLocal(PreviewDesignId, entry.Design.DeepCopy());
            Preview.SetDesignId(PreviewDesignId);
        }

        RefreshList();
    }

    private void SetEntries(IReadOnlyList<GuildCloakReviewEntry> entries)
    {
        var keep = Selected?.SubmissionId;
        Entries = entries;
        var index = -1;

        for (var i = 0; i < entries.Count; i++)
            if (entries[i].SubmissionId == keep)
                index = i;

        if ((index < 0) && (entries.Count > 0))
            index = 0;

        Page = index < 0 ? 0 : index / PAGE_SIZE;
        Select(index);
    }

    private void TurnPage(int delta)
    {
        Page += delta;
        RefreshList();
    }
}
```

- [ ] **Step 2: Wire the review window**

In `CLI/Chaos.Client/Screens/WorldScreen.GuildCloak.cs`, add inside the class:

```csharp
    //built on first use, like the editor
    private GuildCloakReviewControl? GuildCloakReview;

    private GuildCloakReviewControl? EnsureGuildCloakReview()
    {
        if (GuildCloakReview is not null)
            return GuildCloakReview;

        var references = Game.AislingRenderer.GetGuildCloakReferences();

        if (references is null)
            return null;

        GuildCloakReview = new GuildCloakReviewControl(Game.AislingRenderer, references)
        {
            ZIndex = 2
        };

        GuildCloakReview.DecisionRequested += SendGuildCloakDecision;
        Root.AddChild(GuildCloakReview);

        return GuildCloakReview;
    }

    private void HandleGuildCloakReviewList(GuildCloakReviewListArgs args)
    {
        var review = EnsureGuildCloakReview();

        if (review is null)
        {
            WorldState.Chat.AddOrangeBarMessage(MISSING_ART);

            return;
        }

        if (args.Type == GuildCloakReviewListType.Open)
            review.Open(args.Entries);
        else
            review.Refresh(args.Entries);
    }

    private void SendGuildCloakDecision(GuildCloakReviewInteractionArgs args) => Game.Connection.SendGuildCloakReviewInteraction(args);
```

In `WireGuildCloak`, add `Game.Connection.OnGuildCloakReviewList += HandleGuildCloakReviewList;`. In `UnwireGuildCloak`, add `Game.Connection.OnGuildCloakReviewList -= HandleGuildCloakReviewList;` and:

```csharp
        if (GuildCloakReview is not null)
            GuildCloakReview.DecisionRequested -= SendGuildCloakDecision;
```

Update the partial's summary to "Guild cloaks: which design each player's cloak shows, the designs themselves, the editor window and the review window."

- [ ] **Step 3: Document the windows in CLAUDE.md**

In `CLI/CLAUDE.md`, in the **Popups (`Popups/`)** paragraph, add after the `Theatre/` entry:

```
`GuildCloak/` (GuildCloakEditorControl — the guild leader's cloak editor, opened from Quill; GuildCloakReviewControl — the admins' review window, opened from the admin trinket; GuildCloakCanvas, GuildCloakSwatches, GuildCloakPreview; the painting itself is `Chaos.Client.Rendering/GuildCloakPainter`, and the editor state is `ViewModel/GuildCloakEditorModel`),
```

- [ ] **Step 4: Build and run the whole client test project**

Build (Global Constraints command), then `dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`.
Expected: `Build succeeded`, all tests pass.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/GuildCloak/GuildCloakReviewControl.cs", "Chaos.Client/Screens/WorldScreen.GuildCloak.cs", "CLAUDE.md"], "verifyCommand": "dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server", "acceptanceCriteria": ["Open selects the first design; Update keeps a still-waiting selection", "pages of 8; empty list message", "Approve sends the id; Reject needs a 1-200 char reason, sent trimmed", "missing art shows an orange-bar message", "CLAUDE.md lists GuildCloak/ popups", "whole client test project passes"], "modelTier": "standard"}
```

---

### Task 10: Commit the full implementation

**Goal:** One commit per repo on `feat/guild-cloak`, with the client pointing its submodule at the server branch commit, and a handover list for the user. Nothing is merged, pushed or deployed.

**Files:**
- Commit: every file created or changed in Tasks 1–9, plus this plan and its `.tasks.json` (the spec is already on `main`)

**Acceptance Criteria:**
- [ ] Server tests pass except the two known master failures; the whole client test project passes; the Python tests pass
- [ ] `SRV`, `UNO` and `CLI` each have exactly one new commit on `feat/guild-cloak`
- [ ] No `appsettings.json` or `launchSettings.json` in any commit
- [ ] The `CLI` commit records `Chaos-Server` at the `SRV` commit
- [ ] `git status --short` in each worktree shows nothing of this plan's left over

**Verify:** `git -C <each worktree> log --oneline -1` shows the new commit; `git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client ls-tree HEAD Chaos-Server` shows the `SRV` commit hash

**Steps:**

- [ ] **Step 1: Final test runs**

```bash
dotnet run --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server/Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server
dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora && python -m unittest Tools/Accessories/items/GuildCloak/test_guildcloak.py Tools/Accessories/test_items.py
```

Expected: the server run fails only `GiveAbility` and `OnItemDroppedOn` (stackable); everything else passes. Any other failure: stop and report.

- [ ] **Step 2: Review what will be committed**

Run `git -C <worktree> status --short` in all three. Expect only this plan's files. The Unora worktree may show `Custom Client Mods/.../obj` build output from the code-analysis server: leave it out of the commit, and run `git -C <UNO> checkout -- "Custom Client Mods"` before anyone removes the worktree. Anything else unexpected: stop and report.

- [ ] **Step 3: Commit the server**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server
git add Chaos.DarkAges/Definitions/GuildCloakProtocol.cs Chaos.DarkAges/Definitions/GuildCloakDesign.cs \
  Chaos.DarkAges/Definitions/Enums.cs Chaos.DarkAges/Definitions/CONSTANTS.cs \
  Chaos.Networking.Abstractions/Definitions/Enums.cs \
  Chaos.Networking/Entities/Server/GuildCloakEditorArgs.cs Chaos.Networking/Entities/Server/GuildCloakLookArgs.cs \
  Chaos.Networking/Entities/Server/GuildCloakDesignArgs.cs Chaos.Networking/Entities/Server/GuildCloakReviewEntry.cs \
  Chaos.Networking/Entities/Server/GuildCloakReviewListArgs.cs \
  Chaos.Networking/Entities/Client/GuildCloakEditorInteractionArgs.cs Chaos.Networking/Entities/Client/GuildCloakDesignRequestArgs.cs \
  Chaos.Networking/Entities/Client/GuildCloakReviewInteractionArgs.cs \
  Chaos.Networking/Converters/GuildCloakDesignCodec.cs \
  Chaos.Networking/Converters/Server/GuildCloakEditorConverter.cs Chaos.Networking/Converters/Server/GuildCloakLookConverter.cs \
  Chaos.Networking/Converters/Server/GuildCloakDesignConverter.cs Chaos.Networking/Converters/Server/GuildCloakReviewListConverter.cs \
  Chaos.Networking/Converters/Client/GuildCloakEditorInteractionConverter.cs Chaos.Networking/Converters/Client/GuildCloakDesignRequestConverter.cs \
  Chaos.Networking/Converters/Client/GuildCloakReviewInteractionConverter.cs \
  Chaos/Models/World/GuildCloakState.cs Chaos/Models/World/GuildHouseState.cs Chaos/SerializationContext.cs \
  Chaos/Services/GuildCloak Chaos/Extensions/ServiceCollectionExtensions.cs \
  Chaos/Networking/Abstractions/IChaosWorldClient.cs Chaos/Networking/ChaosWorldClient.cs Chaos/Services/Servers/WorldServer.cs \
  Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs \
  Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildCloakScript.cs \
  Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildDisbandScript.cs \
  Chaos/Scripting/DialogScripts/Temuair/Generic/GuildCloakAdminScript.cs Chaos/Collections/Guild.cs \
  Tests/Chaos.Tests/Networking/GuildCloakPacketConverterTests.cs Tests/Chaos.Tests/GuildCloak \
  Tests/Chaos.Tests/GuildHall/GuildHouseStateTests.cs
git commit -F- <<'EOF'
Add guild cloaks: stored designs, admin review and the cloak look message.

A guild that owns the new cloak deed (5M from Tibbs) has its leader paint a
cloak design; admins approve or reject it. The server keeps each guild's
approved, waiting and draft designs, checks rank, deed, rate and design on
every request, and after each player look tells the viewer which design that
player's guild cloak shows. Quill sells the Guild Cloak for 50,000 gold.
Joining, leaving, removal and disband refresh the look.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KqYVmRstoX73xS4oqebWL1
EOF
```

- [ ] **Step 4: Commit Unora**

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-unora
git add Tools/Accessories/items/GuildCloak/guildcloak_art.py Tools/Accessories/items/GuildCloak/build_guildcloak.py \
  Tools/Accessories/items/GuildCloak/preview.py Tools/Accessories/items/GuildCloak/test_guildcloak.py \
  Tools/Accessories/items/GuildCloak/README.md Tools/Accessories/registry.json \
  Data/Configuration/Templates/Items/Equipment/Temuair/Accessories/guildCloak.json \
  "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_cloaks.json" \
  "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Tibbs/tibbs_purchase_cloaks_confirm.json" \
  "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_initial.json" \
  "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_initial.json" \
  "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_design.json" \
  "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_buy.json" \
  "Data/Configuration/Templates/Dialogs/Temauir/Guild Hall/Quill/quill_guildcloak_buy_confirm.json" \
  Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_initial.json \
  Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_review.json \
  Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear.json \
  Data/Configuration/Templates/Dialogs/Temauir/generic/admin/admintrinket_guildcloak_clear_confirm.json
git commit -F- <<'EOF'
Add the Guild Cloak item, its sprite copy and the guild cloak dialogs.

The Guild Cloak is a copy of the Black Cape of Romance (sprite 127, icon
4266) under its own numbers; the client paints each guild's design on it.
Tibbs sells the cloak deed, Quill opens the editor and sells cloaks, and the
admin trinket opens the review window and clears a guild's design.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KqYVmRstoX73xS4oqebWL1
EOF
```

- [ ] **Step 5: Commit the client, pointing the submodule at the server commit**

`docs/superpowers/` is gitignored in Chaos.Client, so the plan files need `git add -f`.

```bash
cd /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client
SRV_SHA=$(git -C /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-server rev-parse HEAD)
git update-index --cacheinfo 160000,$SRV_SHA,Chaos-Server
git add Chaos.Client.Networking/ConnectionManager.cs Chaos.Client.Networking/Definitions/Delegates.cs \
  Chaos.Client.Rendering/AislingRenderer.cs Chaos.Client.Rendering/GuildCloakGrid.cs Chaos.Client.Rendering/GuildCloakPainter.cs \
  Chaos.Client.Rendering/GuildCloakReferences.cs Chaos.Client.Rendering/GuildCloakDesignStore.cs \
  Chaos.Client/Collections/WorldState.cs Chaos.Client/Screens/WorldScreen.GuildCloak.cs Chaos.Client/Screens/WorldScreen.cs \
  Chaos.Client/ViewModel/GuildCloakEditorModel.cs Chaos.Client/Controls/World/Popups/GuildCloak \
  Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs Tests/Chaos.Client.Tests/GuildCloakDesignStoreTests.cs \
  Tests/Chaos.Client.Tests/GuildCloakEditorModelTests.cs CLAUDE.md
git add -f docs/superpowers/plans/2026-09-25-guild-cloak.md docs/superpowers/plans/2026-09-25-guild-cloak.md.tasks.json
git commit -F- <<'EOF'
Add the guild cloak editor and review windows, and paint guild cloaks.

The client paints each player's guild cloak design onto every frame of the
cloak as it draws it: each pixel takes its color from the same place on the
painted front or back and keeps the frame's own shading. Designs are asked
for once and cached by id. The leader paints in a new editor window (front and
back canvases, six colors, pencil, fill, pick, mirror, undo, walking
preview); admins approve or reject in a new review window.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01KqYVmRstoX73xS4oqebWL1
EOF
```

- [ ] **Step 6: Check the commits**

```bash
for w in guild-cloak-server guild-cloak-unora guild-cloak-client; do git -C /c/Users/Michael/Documents/GitHub/worktrees/$w log --oneline -1; git -C /c/Users/Michael/Documents/GitHub/worktrees/$w show --stat HEAD | grep -i "appsettings\|launchSettings" && echo "STOP: settings file committed"; done
git -C /c/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client ls-tree HEAD Chaos-Server
```

Expected: three new commits, no "STOP" line, and the `ls-tree` hash equals `SRV_SHA`.

- [ ] **Step 7: Hand over to the user**

Report the three branch commits, then list what only the user can do:

1. **Review and merge** the three `feat/guild-cloak` branches (finishing-a-development-branch).
2. **Ship the art:** in Unora, `python Tools/Accessories/deploy_batch.py guildCloak`; upload the batch's `.dat` files and templates; copy the `.dat` files into `~/Documents/Unora/Unora Files`; `python Tools/Accessories/ids.py status guildCloak deployed`.
3. **Ship the client and server together.** `CLIENT_VERSION` went up by one, so older clients are refused at login.
4. **Check by hand in the game** (the spec's list):
   1. Buy the deed from Tibbs as a council member.
   2. As the leader, paint a design with an off-center emblem, save a draft, reopen it, then submit.
   3. From an admin character, reject it with a reason. Check the leader's message and status line.
   4. Resubmit, then approve.
   5. Buy cloaks for a male and a female member. Walk, attack and cast in all four directions. Check the emblem reads the right way round.
   6. Submit a new design while one is approved. Check members keep the old one until approval.
   7. Leave the guild while wearing the cloak. Check it goes plain for a nearby viewer at once.
   8. Clear the design from the admin trinket. Check the cloaks go plain.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/guild-cloak-client ls-tree HEAD Chaos-Server", "acceptanceCriteria": ["server tests pass except the two known failures; client and Python tests pass", "one new commit per worktree on feat/guild-cloak", "no appsettings.json or launchSettings.json committed", "CLI commit records Chaos-Server at the SRV commit", "nothing of this plan left uncommitted"], "modelTier": "mechanical"}
```
