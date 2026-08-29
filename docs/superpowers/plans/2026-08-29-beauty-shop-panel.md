# Josephine's Mirror (Beauty Shop Panel) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Josephine's five one-at-a-time appearance dialogs with one client popup that previews the whole sprite, lets the player change gender / hairstyle / hair dye / skin / face together with a running gold total, and charges once on Apply.

**Architecture:** Same rail as the poker table: one `ServerOpCode`/`ClientOpCode` pair with args records + converters in the shared `Chaos.Networking` project (the client consumes them by project reference, so it has no packet code); a server-side catalog built from item templates that is sent on open and re-validated on apply; a `FramedDialogPanelBase` control on the client that renders the preview locally with `AislingRenderer.Render`. Spec: `docs/superpowers/specs/2026-08-29-beauty-shop-panel-design.md`.

**Tech Stack:** .NET 10 / C# 14, TUnit 1.1.10 + FluentAssertions 8.8.0 + Moq (server tests), MonoGame DesktopGL client, JSON content templates in the Unora data repo.

**User decisions (already made):**
- Preview starts with gear hidden; a "Show gear" checkbox layers the live equipment on.
- Josephine keeps a one-line greeting dialog (with the Riona quest hook) whose single option opens the panel.
- Approach A: client renders the preview locally; server sends catalog + prices on open and validates/charges once on Apply.
- Spec approved 2026-08-29.

**Deviation from spec (flagged, not silent):** the Skin row shows its name only — no colour swatch. The body palettes have no single entry that reads as "the skin tone", and the sprite beside the row is the swatch. The Hair dye row keeps its swatch (sampled from the dye colour table).

---

## Repos and conventions (read first)

Three git repos are touched; each gets its own commits on a `feature/beauty-shop` branch:

| Repo | Path | Notes |
| --- | --- | --- |
| Server | `C:\Users\mikeb\Documents\GitHub\Chaos.Client\Chaos-Server` (submodule) | `master` @ `9148f8500`. Uncommitted `Chaos/appsettings*.json` edits are the user's local boot config — never stage them. |
| Data | `C:\Users\mikeb\Documents\GitHub\Unora` | Server reads it via `appsettings.local.json` `StagingDirectory`. `Data/Saved` and `Data/Backups` get dirty after a local run; don't commit them. |
| Client | `C:\Users\mikeb\Documents\GitHub\Chaos.Client` | `docs/` is gitignored: `git add -f` plan/spec files. Use `git -c core.commitGraph=false` if a merge complains about the commit graph. |

- **Server tests:** `cd Chaos.Client/Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/<TestClass>/*"`. **Never `dotnet test`** (it runs nothing on this SDK). Don't pass `--nologo` after `--`. Four upstream tests fail regardless of our work: `GiveExperience_AddsExperienceToAisling`, `GiveExperience_ContinuesChain`, `GiveAbility_AddsAbilityToAisling`, `OnItemDroppedOn_ShouldAddStackableItem_WhenCountIsPositive`.
- **Client build:** `cd Chaos.Client && dotnet build Chaos.Client.slnx` (also builds the server; stop any running server/client first or the output is locked).
- **Script keys** are the class name minus `Script` (`ScriptBase.GetScriptKey`), matched case-insensitively: `BeautyShopScript` → `beautyShop`, `BeautyShopOpenScript` → `beautyShopOpen`.
- **Packet converters are discovered by reflection** (`Chaos.Packets/Extensions/PacketExtensions.cs:40`) — no registration.
- Commit messages end with the two trailer lines the session uses (`Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef`).

## File structure

**Server (`Chaos-Server`)**
- `Chaos.Networking.Abstractions/Definitions/Enums.cs` — two opcode entries.
- `Chaos.DarkAges/Definitions/Enums.cs` — `BeautyShopDisplayType`, `BeautyShopInteractionType`, `BeautyShopRejectReason`.
- `Chaos.Networking/Entities/Server/BeautyShopDisplayArgs.cs` — open/rejected/close payload + `BeautyShopHairstyleEntry`, `BeautyShopFaceEntry`.
- `Chaos.Networking/Entities/Client/BeautyShopInteractionArgs.cs` — apply/close payload.
- `Chaos.Networking/Converters/Server/BeautyShopDisplayConverter.cs`, `Converters/Client/BeautyShopInteractionConverter.cs`.
- `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, `Chaos/Networking/ChaosWorldClient.cs` — `SendBeautyShopOpen/Rejected/Close`.
- `Chaos/Services/BeautyShop/BeautyShopCatalog.cs` — pure catalog: lists, prices, validation, `BuildOpenArgs`.
- `Chaos/Services/BeautyShop/BeautyShopCatalogLoader.cs` — builds the catalog from `ISimpleCache<ItemTemplate>`.
- `Chaos/Services/BeautyShop/BeautyShopCatalogValidationService.cs` — hosted service that forces the load at startup.
- `Chaos/Services/BeautyShop/GenderSwapService.cs` — gear/inventory/bank swap lifted from `SwapGenderScript`.
- `Chaos/Services/BeautyShop/BeautyShopCheckout.cs` — validate → diff → charge once → apply.
- `Chaos/Scripting/MerchantScripts/BeautyShopScript.cs` — merchant script: open + route interactions.
- `Chaos/Scripting/DialogScripts/Temuair/Mileth/BeautyShopOpenScript.cs` — dialog script that closes the dialog and opens the panel.
- `Chaos/Services/Servers/WorldServer.cs` — `OnBeautyShopInteraction` + registration.
- `Chaos/Extensions/ServiceCollectionExtensions.cs` — DI.
- Deleted: `Chaos/Scripting/DialogScripts/Temuair/Generic/{HairStyleScript,HairDyeScript,BodyDyeScript,FaceShapeScript,SwapGenderScript}.cs`.
- Tests: `Tests/Chaos.Tests/Networking/BeautyShopPacketConverterTests.cs`, `Tests/Chaos.Tests/BeautyShop/{BeautyShopCatalogTests,BeautyShopCatalogLoaderTests,GenderSwapServiceTests,BeautyShopCheckoutTests}.cs`.

**Data (`Unora`)**
- `Data/Configuration/Templates/Dialogs/Temauir/mileth/josephine/josephine_initial.json` — one option.
- `.../josephine/josephine_mirror.json` — new.
- Deleted: `josephine_buyShop`, `josephine_changeHairDye`, `josephine_changeBodyDye`, `josephine_changeFaceShape`, `josephine_changeGender`, `josephine_swapGenderConfirm`.
- `Data/Configuration/Templates/Merchants/Temauir/mileth/josephine.json` — add `beautyShop` script key.

**Client (`Chaos.Client`)**
- `Chaos.Client.Networking/Definitions/Delegates.cs`, `Chaos.Client.Networking/ConnectionManager.cs` — event, handler, sends.
- `Chaos.Client/ViewModel/BeautyShop.cs` — pure view model (catalog, current, selected, totals).
- `Chaos.Client/Collections/WorldState.cs` — `BeautyShop` singleton + clear on logout.
- `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs` — the panel.
- `Chaos.Client/Screens/WorldScreen.cs`, `WorldScreen.Wiring.cs`, `WorldScreen.ServerHandlers.cs` — construct, wire, dispatch.
- `Chaos.Client.Rendering/AislingRenderer.cs` — make `BODY_ID` public.
- `Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj`, `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs` — new test project; `Directory.Packages.props` + `Chaos.Client.slnx` updated.
- `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md` — manual QA script.

---

### Task 0: Branches

**Goal:** Feature branches in all three repos so every later commit lands off `master`/`main`.

**Files:**
- none (git only)

**Acceptance Criteria:**
- [ ] `git branch --show-current` prints `feature/beauty-shop` in `Chaos.Client/Chaos-Server`, `Unora`, and `Chaos.Client`.
- [ ] The server's uncommitted `Chaos/appsettings.json` / `appsettings.local.json` edits are still present and unstaged.

**Verify:** `cd C:/Users/mikeb/Documents/GitHub && for r in Chaos.Client/Chaos-Server Unora Chaos.Client; do git -C $r branch --show-current; done` → three lines of `feature/beauty-shop`

**Steps:**

- [ ] **Step 1: Create the branches**

```bash
cd /c/Users/mikeb/Documents/GitHub
git -C Chaos.Client/Chaos-Server switch -c feature/beauty-shop
git -C Unora switch -c feature/beauty-shop
git -C Chaos.Client switch -c feature/beauty-shop
git -C Chaos.Client/Chaos-Server status --short   # appsettings edits stay unstaged; never add them
```

```json:metadata
{"files": [], "verifyCommand": "for r in Chaos.Client/Chaos-Server Unora Chaos.Client; do git -C $r branch --show-current; done", "acceptanceCriteria": ["all three repos on feature/beauty-shop", "server appsettings edits untouched"], "modelTier": "mechanical"}
```

---

### Task 1: Protocol — opcodes, enums, args, converters, send methods

**Goal:** Both packets exist end-to-end on the server side with round-trip tests, and `IChaosWorldClient` can send them.

**Files:**
- Modify: `Chaos.Networking.Abstractions/Definitions/Enums.cs` (after `PokerTableInteraction = 118` ≈ line 407; after `PokerTableDisplay = 120` ≈ line 964)
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (after `PokerRejectReason`, ≈ line 121)
- Create: `Chaos.Networking/Entities/Server/BeautyShopDisplayArgs.cs`
- Create: `Chaos.Networking/Entities/Client/BeautyShopInteractionArgs.cs`
- Create: `Chaos.Networking/Converters/Server/BeautyShopDisplayConverter.cs`
- Create: `Chaos.Networking/Converters/Client/BeautyShopInteractionConverter.cs`
- Modify: `Chaos/Networking/Abstractions/IChaosWorldClient.cs` (after `SendPokerTableClose()` ≈ line 251)
- Modify: `Chaos/Networking/ChaosWorldClient.cs` (after `SendPokerTableClose` ≈ line 892)
- Test: `Tests/Chaos.Tests/Networking/BeautyShopPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `ClientOpCode.BeautyShopInteraction == 119` and `ServerOpCode.BeautyShopDisplay == 121`.
- [ ] Open, Rejected, Close and Apply/Close payloads round-trip byte-for-byte equivalent (FluentAssertions `BeEquivalentTo`).
- [ ] An undefined display/interaction type byte throws `ArgumentOutOfRangeException` on deserialize.
- [ ] A list with more than 255 entries throws on serialize (never truncates).
- [ ] `SendBeautyShopOpen`, `SendBeautyShopRejected`, `SendBeautyShopClose` exist on `IChaosWorldClient` and compile.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShopPacketConverterTests/*"` → all passed, 0 failed

**Steps:**

- [ ] **Step 1: Opcodes**

In `Chaos.Networking.Abstractions/Definitions/Enums.cs`, directly after `PokerTableInteraction = 118,`:

```csharp
    /// <summary>
    ///     Sent when a player applies or closes Josephine's beauty shop mirror. Carries the chosen appearance
    ///     values only -- never a price; the server re-prices every category from its own catalog.
    /// </summary>
    BeautyShopInteraction = 119,
```

Directly after `PokerTableDisplay = 120,`:

```csharp
    /// <summary>
    ///     Opens, rejects an apply on, or closes the beauty shop mirror panel. Open carries the whole catalog
    ///     and the player's current appearance.
    /// </summary>
    BeautyShopDisplay = 121,
```

- [ ] **Step 2: Shape enums**

In `Chaos.DarkAges/Definitions/Enums.cs`, after the closing brace of `PokerRejectReason`:

```csharp
public enum BeautyShopDisplayType : byte
{
    /// <summary>Open the mirror with the catalog and the player's current look.</summary>
    Open = 0,

    /// <summary>The last Apply was refused; the panel stays open.</summary>
    Rejected = 1,

    /// <summary>Close the panel.</summary>
    Close = 2
}

public enum BeautyShopInteractionType : byte
{
    Apply = 0,
    Close = 1
}

public enum BeautyShopRejectReason : byte
{
    NothingChanged = 0,
    InsufficientGold = 1,
    InvalidSelection = 2,
    GenderSwapUnavailable = 3,
    NotNearShop = 4
}
```

- [ ] **Step 3: Server → client args**

Create `Chaos.Networking/Entities/Server/BeautyShopDisplayArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>One purchasable hairstyle: the head sprite the server will set, and what it costs.</summary>
public sealed record BeautyShopHairstyleEntry
{
    public required ushort Sprite { get; set; }
    public required int Price { get; set; }
}

/// <summary>One purchasable face shape.</summary>
public sealed record BeautyShopFaceEntry
{
    public required byte Sprite { get; set; }
    public required string Name { get; set; }
    public required int Price { get; set; }

    /// <summary>True when only a female character may wear this face (the server refuses it for males).</summary>
    public required bool FemaleOnly { get; set; }
}

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.BeautyShopDisplay" /> packet.
/// </summary>
/// <remarks>
///     Open carries everything the panel needs to browse offline: the player's current values, the full
///     catalog, and every price. The client never computes a price the server has not sent, and the server
///     never trusts a price the client sends back.
/// </remarks>
public sealed record BeautyShopDisplayArgs : IPacketSerializable
{
    public required BeautyShopDisplayType Type { get; set; }

    //current appearance (Open only)
    public Gender Gender { get; set; }
    public ushort HairStyle { get; set; }
    public DisplayColor HairColor { get; set; }
    public BodyColor BodyColor { get; set; }
    public byte FaceSprite { get; set; }

    //prices and purse (Open only)
    public int Gold { get; set; }
    public int GenderPrice { get; set; }
    public int HairDyePrice { get; set; }
    public int BodyDyePrice { get; set; }

    //catalog (Open only)
    public IReadOnlyList<BeautyShopHairstyleEntry> MaleHairstyles { get; set; } = [];
    public IReadOnlyList<BeautyShopHairstyleEntry> FemaleHairstyles { get; set; } = [];
    public IReadOnlyList<BeautyShopFaceEntry> Faces { get; set; } = [];
    public IReadOnlyList<DisplayColor> HairColors { get; set; } = [];
    public IReadOnlyList<BodyColor> BodyColors { get; set; } = [];

    //Rejected only
    public BeautyShopRejectReason Reason { get; set; }
}
```

- [ ] **Step 4: Client → server args**

Create `Chaos.Networking/Entities/Client/BeautyShopInteractionArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>
///     Represents the serialization of the <see cref="ClientOpCode.BeautyShopInteraction" /> packet. For
///     <see cref="BeautyShopInteractionType.Close" /> the appearance fields are ignored. Deliberately carries
///     no prices and no merchant id: the server resolves the shop by proximity and re-prices every category.
/// </summary>
public sealed record BeautyShopInteractionArgs : IPacketSerializable
{
    public required BeautyShopInteractionType Type { get; set; }
    public Gender Gender { get; set; }
    public ushort HairStyle { get; set; }
    public DisplayColor HairColor { get; set; }
    public BodyColor BodyColor { get; set; }
    public byte FaceSprite { get; set; }
}
```

- [ ] **Step 5: Server → client converter**

Create `Chaos.Networking/Converters/Server/BeautyShopDisplayConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="BeautyShopDisplayArgs" /></summary>
public sealed class BeautyShopDisplayConverter : PacketConverterBase<BeautyShopDisplayArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.BeautyShopDisplay;

    /// <inheritdoc />
    public override BeautyShopDisplayArgs Deserialize(ref SpanReader reader)
    {
        var type = (BeautyShopDisplayType)reader.ReadByte();

        switch (type)
        {
            case BeautyShopDisplayType.Open:
            {
                var args = new BeautyShopDisplayArgs
                {
                    Type = type,
                    Gender = (Gender)reader.ReadByte(),
                    HairStyle = reader.ReadUInt16(),
                    HairColor = (DisplayColor)reader.ReadByte(),
                    BodyColor = (BodyColor)reader.ReadByte(),
                    FaceSprite = reader.ReadByte(),
                    Gold = reader.ReadInt32(),
                    GenderPrice = reader.ReadInt32(),
                    HairDyePrice = reader.ReadInt32(),
                    BodyDyePrice = reader.ReadInt32()
                };

                args.MaleHairstyles = ReadHairstyles(ref reader);
                args.FemaleHairstyles = ReadHairstyles(ref reader);

                var faceCount = reader.ReadByte();
                var faces = new List<BeautyShopFaceEntry>(faceCount);

                for (var i = 0; i < faceCount; i++)
                    faces.Add(
                        new BeautyShopFaceEntry
                        {
                            Sprite = reader.ReadByte(),
                            Name = reader.ReadString8(),
                            Price = reader.ReadInt32(),
                            FemaleOnly = reader.ReadBoolean()
                        });

                args.Faces = faces;

                var hairColorCount = reader.ReadByte();
                var hairColors = new List<DisplayColor>(hairColorCount);

                for (var i = 0; i < hairColorCount; i++)
                    hairColors.Add((DisplayColor)reader.ReadByte());

                args.HairColors = hairColors;

                var bodyColorCount = reader.ReadByte();
                var bodyColors = new List<BodyColor>(bodyColorCount);

                for (var i = 0; i < bodyColorCount; i++)
                    bodyColors.Add((BodyColor)reader.ReadByte());

                args.BodyColors = bodyColors;

                return args;
            }

            case BeautyShopDisplayType.Rejected:
                return new BeautyShopDisplayArgs
                {
                    Type = type,
                    Reason = (BeautyShopRejectReason)reader.ReadByte()
                };

            case BeautyShopDisplayType.Close:
                return new BeautyShopDisplayArgs { Type = type };

            default:
                throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown beauty shop display type");
        }
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, BeautyShopDisplayArgs args)
    {
        writer.WriteByte((byte)args.Type);

        switch (args.Type)
        {
            case BeautyShopDisplayType.Open:
            {
                writer.WriteByte((byte)args.Gender);
                writer.WriteUInt16(args.HairStyle);
                writer.WriteByte((byte)args.HairColor);
                writer.WriteByte((byte)args.BodyColor);
                writer.WriteByte(args.FaceSprite);
                writer.WriteInt32(args.Gold);
                writer.WriteInt32(args.GenderPrice);
                writer.WriteInt32(args.HairDyePrice);
                writer.WriteInt32(args.BodyDyePrice);

                WriteHairstyles(ref writer, args.MaleHairstyles, "male hairstyles");
                WriteHairstyles(ref writer, args.FemaleHairstyles, "female hairstyles");

                var faces = args.Faces ?? [];
                writer.WriteByte(CountToByte(faces.Count, "faces"));

                foreach (var face in faces)
                {
                    writer.WriteByte(face.Sprite);
                    writer.WriteString8(face.Name ?? string.Empty);
                    writer.WriteInt32(face.Price);
                    writer.WriteBoolean(face.FemaleOnly);
                }

                var hairColors = args.HairColors ?? [];
                writer.WriteByte(CountToByte(hairColors.Count, "hair colors"));

                foreach (var color in hairColors)
                    writer.WriteByte((byte)color);

                var bodyColors = args.BodyColors ?? [];
                writer.WriteByte(CountToByte(bodyColors.Count, "body colors"));

                foreach (var color in bodyColors)
                    writer.WriteByte((byte)color);

                break;
            }

            case BeautyShopDisplayType.Rejected:
                writer.WriteByte((byte)args.Reason);

                break;

            case BeautyShopDisplayType.Close:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(args), args.Type, "Unknown beauty shop display type");
        }
    }

    private static List<BeautyShopHairstyleEntry> ReadHairstyles(ref SpanReader reader)
    {
        var count = reader.ReadByte();
        var list = new List<BeautyShopHairstyleEntry>(count);

        for (var i = 0; i < count; i++)
            list.Add(
                new BeautyShopHairstyleEntry
                {
                    Sprite = reader.ReadUInt16(),
                    Price = reader.ReadInt32()
                });

        return list;
    }

    private static void WriteHairstyles(ref SpanWriter writer, IReadOnlyList<BeautyShopHairstyleEntry>? entries, string what)
    {
        entries ??= [];
        writer.WriteByte(CountToByte(entries.Count, what));

        foreach (var entry in entries)
        {
            writer.WriteUInt16(entry.Sprite);
            writer.WriteInt32(entry.Price);
        }
    }

    /// <summary>
    ///     Narrows a collection count to a byte, throwing rather than silently truncating -- the same guard
    ///     <c>PokerTableDisplayConverter</c> uses. ~100 hairstyles, 18 faces and ~80 colors all fit.
    /// </summary>
    private static byte CountToByte(int count, string what)
    {
        if ((uint)count > byte.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"Beauty shop display packet cannot encode {count} {what}; the wire format allows at most {byte.MaxValue}.");

        return (byte)count;
    }
}
```

- [ ] **Step 6: Client → server converter**

Create `Chaos.Networking/Converters/Client/BeautyShopInteractionConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="BeautyShopInteractionArgs" /></summary>
public sealed class BeautyShopInteractionConverter : PacketConverterBase<BeautyShopInteractionArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.BeautyShopInteraction;

    /// <inheritdoc />
    public override BeautyShopInteractionArgs Deserialize(ref SpanReader reader)
    {
        var type = (BeautyShopInteractionType)reader.ReadByte();

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown beauty shop interaction type");

        return new BeautyShopInteractionArgs
        {
            Type = type,
            Gender = (Gender)reader.ReadByte(),
            HairStyle = reader.ReadUInt16(),
            HairColor = (DisplayColor)reader.ReadByte(),
            BodyColor = (BodyColor)reader.ReadByte(),
            FaceSprite = reader.ReadByte()
        };
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, BeautyShopInteractionArgs args)
    {
        writer.WriteByte((byte)args.Type);
        writer.WriteByte((byte)args.Gender);
        writer.WriteUInt16(args.HairStyle);
        writer.WriteByte((byte)args.HairColor);
        writer.WriteByte((byte)args.BodyColor);
        writer.WriteByte(args.FaceSprite);
    }
}
```

- [ ] **Step 7: Round-trip tests (write these before building the converters if you prefer strict TDD; they must fail to compile first)**

Create `Tests/Chaos.Tests/Networking/BeautyShopPacketConverterTests.cs`:

```csharp
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Converters.Client;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;
using FluentAssertions;

namespace Chaos.Tests.Networking;

public class BeautyShopPacketConverterTests
{
    private static readonly Encoding Enc = Encoding.GetEncoding(949);

    private static T RoundTrip<T>(PacketConverterBase<T> converter, T original) where T: class, IPacketSerializable
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        converter.Serialize(ref writer, original);
        var bytes = writer.ToSpan().ToArray();
        var reader = new SpanReader(Enc, bytes);

        return converter.Deserialize(ref reader);
    }

    private static BeautyShopDisplayArgs FullOpen()
        => new()
        {
            Type = BeautyShopDisplayType.Open,
            Gender = Gender.Female,
            HairStyle = 17,
            HairColor = DisplayColor.Scarlet,
            BodyColor = BodyColor.Tan,
            FaceSprite = 10,
            Gold = 61_230,
            GenderPrice = 50_000,
            HairDyePrice = 1_000,
            BodyDyePrice = 1_000,
            MaleHairstyles = [new BeautyShopHairstyleEntry { Sprite = 1, Price = 1_000 }, new BeautyShopHairstyleEntry { Sprite = 101, Price = 1_000 }],
            FemaleHairstyles = [new BeautyShopHairstyleEntry { Sprite = 0, Price = 1_000 }],
            Faces =
            [
                new BeautyShopFaceEntry { Sprite = 1, Name = "Default", Price = 50_000, FemaleOnly = false },
                new BeautyShopFaceEntry { Sprite = 18, Name = "Resting", Price = 50_000, FemaleOnly = true }
            ],
            HairColors = [DisplayColor.Default, DisplayColor.Apple, DisplayColor.Fern],
            BodyColors = [BodyColor.Brown, BodyColor.White]
        };

    [Test]
    public async Task Open_round_trips()
    {
        var original = FullOpen();

        RoundTrip(new BeautyShopDisplayConverter(), original).Should().BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Open_with_empty_lists_round_trips()
    {
        var original = new BeautyShopDisplayArgs { Type = BeautyShopDisplayType.Open, Gender = Gender.Male };

        RoundTrip(new BeautyShopDisplayConverter(), original).Should().BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Rejected_round_trips()
    {
        var original = new BeautyShopDisplayArgs { Type = BeautyShopDisplayType.Rejected, Reason = BeautyShopRejectReason.InsufficientGold };

        RoundTrip(new BeautyShopDisplayConverter(), original).Should().BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Close_round_trips()
    {
        var original = new BeautyShopDisplayArgs { Type = BeautyShopDisplayType.Close };

        RoundTrip(new BeautyShopDisplayConverter(), original).Should().BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Serialize_throws_when_a_list_exceeds_a_byte()
    {
        var original = FullOpen();
        original.HairColors = Enumerable.Repeat(DisplayColor.Apple, 256).ToList();
        var writer = new SpanWriter(Enc, usePooling: false);

        var act = () => new BeautyShopDisplayConverter().Serialize(ref writer, original);

        act.Should().Throw<ArgumentOutOfRangeException>();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Deserialize_throws_on_unknown_display_type()
    {
        var reader = new SpanReader(Enc, [200]);

        var act = () => new BeautyShopDisplayConverter().Deserialize(ref reader);

        act.Should().Throw<ArgumentOutOfRangeException>();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Apply_round_trips()
    {
        var original = new BeautyShopInteractionArgs
        {
            Type = BeautyShopInteractionType.Apply,
            Gender = Gender.Male,
            HairStyle = 44,
            HairColor = DisplayColor.Midnight,
            BodyColor = BodyColor.Purple,
            FaceSprite = 3
        };

        RoundTrip(new BeautyShopInteractionConverter(), original).Should().BeEquivalentTo(original);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Interaction_deserialize_throws_on_unknown_type()
    {
        var reader = new SpanReader(Enc, [9, 0, 0, 0, 0, 0, 0]);

        var act = () => new BeautyShopInteractionConverter().Deserialize(ref reader);

        act.Should().Throw<ArgumentOutOfRangeException>();

        await Task.CompletedTask;
    }
}
```

Note: `act` lambdas that capture a `ref` local won't compile — if the compiler complains, move the `SpanWriter`/`SpanReader` construction inside the lambda.

- [ ] **Step 8: Send methods**

In `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, after `void SendPokerTableClose();`:

```csharp
    /// <summary>
    ///     Opens the beauty shop mirror with the catalog and the player's current look. <paramref name="args" />
    ///     is built by <c>BeautyShopCatalog.BuildOpenArgs</c>; its Type is forced to Open here.
    /// </summary>
    void SendBeautyShopOpen(BeautyShopDisplayArgs args);

    /// <summary>Tells the client an Apply was refused and why. The panel stays open.</summary>
    void SendBeautyShopRejected(BeautyShopRejectReason reason);

    /// <summary>Closes the beauty shop mirror.</summary>
    void SendBeautyShopClose();
```

In `Chaos/Networking/ChaosWorldClient.cs`, after `SendPokerTableClose`:

```csharp
    /// <inheritdoc />
    public void SendBeautyShopOpen(BeautyShopDisplayArgs args) => Send(args with { Type = BeautyShopDisplayType.Open });

    /// <inheritdoc />
    public void SendBeautyShopRejected(BeautyShopRejectReason reason)
        => Send(new BeautyShopDisplayArgs { Type = BeautyShopDisplayType.Rejected, Reason = reason });

    /// <inheritdoc />
    public void SendBeautyShopClose() => Send(new BeautyShopDisplayArgs { Type = BeautyShopDisplayType.Close });
```

- [ ] **Step 9: Run the tests**

Run: `cd /c/Users/mikeb/Documents/GitHub/Chaos.Client/Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShopPacketConverterTests/*"`
Expected: 8 passed, 0 failed.

- [ ] **Step 10: Commit**

```bash
git add Chaos.Networking.Abstractions/Definitions/Enums.cs Chaos.DarkAges/Definitions/Enums.cs Chaos.Networking Chaos/Networking Tests/Chaos.Tests/Networking/BeautyShopPacketConverterTests.cs
git commit -m "BeautyShop: display/interaction packets, converters and send methods"
```

```json:metadata
{"files": ["Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/Enums.cs", "Chaos.Networking/Entities/Server/BeautyShopDisplayArgs.cs", "Chaos.Networking/Entities/Client/BeautyShopInteractionArgs.cs", "Chaos.Networking/Converters/Server/BeautyShopDisplayConverter.cs", "Chaos.Networking/Converters/Client/BeautyShopInteractionConverter.cs", "Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos/Networking/ChaosWorldClient.cs", "Tests/Chaos.Tests/Networking/BeautyShopPacketConverterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter \"/*/*/BeautyShopPacketConverterTests/*\"", "acceptanceCriteria": ["opcodes 119/121", "open/rejected/close/apply round-trip", "unknown type throws", ">255 entries throws", "send methods compile"], "modelTier": "standard"}
```

---

### Task 2: BeautyShopCatalog (pure catalog + pricing rules)

**Goal:** A dependency-free catalog object that knows what is for sale, what it costs, which entries are valid for which gender, and how to build the Open packet.

**Files:**
- Create: `Chaos/Services/BeautyShop/BeautyShopCatalog.cs`
- Test: `Tests/Chaos.Tests/BeautyShop/BeautyShopCatalogTests.cs`

**Acceptance Criteria:**
- [ ] `HairstylesFor(Gender.Male)` returns only the male list; `TryGetHairstyle(Gender.Male, 96, out _)` is false when 96 is absent from the male list.
- [ ] `TryGetFace(Gender.Male, 18, out _)` is false for a `FemaleOnly` face; true for `Gender.Female`.
- [ ] `HairColors` has `DisplayColor.Default` first and the rest ordered by name; `BodyColors` is every `BodyColor` ordered by name.
- [ ] `GenderPriceFor(false) == 50_000`, `GenderPriceFor(true) == 1_000_000`.
- [ ] `BuildOpenArgs` copies the aisling's current gender/hair/color/body/face and gold, and mirrors every catalog list.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShopCatalogTests/*"` → all passed

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Tests/Chaos.Tests/BeautyShop/BeautyShopCatalogTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.BeautyShop;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;

namespace Chaos.Tests.BeautyShop;

public class BeautyShopCatalogTests
{
    internal static BeautyShopCatalog Build()
        => new(
            maleHairstyles: [new BeautyShopHairstyle(0, 1_000), new BeautyShopHairstyle(1, 1_000), new BeautyShopHairstyle(97, 2_500)],
            femaleHairstyles: [new BeautyShopHairstyle(0, 1_000), new BeautyShopHairstyle(1, 1_000), new BeautyShopHairstyle(96, 1_000)],
            faces:
            [
                new BeautyShopFace(1, "Default", 50_000, false),
                new BeautyShopFace(10, "Beauty", 50_000, false),
                new BeautyShopFace(18, "Resting", 50_000, true)
            ],
            hairDyePrice: 1_000,
            bodyDyePrice: 1_000);

    [Test]
    public async Task Hairstyles_are_per_gender()
    {
        var catalog = Build();

        catalog.HairstylesFor(Gender.Male).Select(h => h.Sprite).Should().Equal(0, 1, 97);
        catalog.TryGetHairstyle(Gender.Male, 96, out _).Should().BeFalse();
        catalog.TryGetHairstyle(Gender.Female, 96, out var style).Should().BeTrue();
        style!.Price.Should().Be(1_000);
        catalog.TryGetHairstyle(Gender.Male, 97, out var pricey).Should().BeTrue();
        pricey!.Price.Should().Be(2_500);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Female_only_faces_are_refused_for_males()
    {
        var catalog = Build();

        catalog.TryGetFace(Gender.Male, 18, out _).Should().BeFalse();
        catalog.TryGetFace(Gender.Female, 18, out var face).Should().BeTrue();
        face!.Name.Should().Be("Resting");
        catalog.TryGetFace(Gender.Male, 10, out _).Should().BeTrue();
        catalog.TryGetFace(Gender.Male, 99, out _).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Color_lists_are_default_first_then_alphabetical()
    {
        var catalog = Build();

        catalog.HairColors[0].Should().Be(DisplayColor.Default);
        catalog.HairColors.Skip(1).Select(c => c.ToString()).Should().BeInAscendingOrder();
        catalog.HairColors.Should().HaveCount(Enum.GetValues<DisplayColor>().Length);
        catalog.BodyColors.Select(c => c.ToString()).Should().BeInAscendingOrder();
        catalog.BodyColors.Should().HaveCount(Enum.GetValues<BodyColor>().Length);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Gender_price_doubles_for_masters()
    {
        BeautyShopCatalog.GenderPriceFor(false).Should().Be(50_000);
        BeautyShopCatalog.GenderPriceFor(true).Should().Be(1_000_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task BuildOpenArgs_reflects_the_aisling_and_the_catalog()
    {
        var catalog = Build();

        var aisling = MockAisling.Create(
            setup: a =>
            {
                a.Gender = Gender.Female;
                a.HairStyle = 96;
                a.HairColor = DisplayColor.Scarlet;
                a.BodyColor = BodyColor.Tan;
                a.FaceSprite = 10;
                a.Gold = 61_230;
            });

        var args = catalog.BuildOpenArgs(aisling);

        args.Type.Should().Be(BeautyShopDisplayType.Open);
        args.Gender.Should().Be(Gender.Female);
        args.HairStyle.Should().Be(96);
        args.HairColor.Should().Be(DisplayColor.Scarlet);
        args.BodyColor.Should().Be(BodyColor.Tan);
        args.FaceSprite.Should().Be(10);
        args.Gold.Should().Be(61_230);
        args.HairDyePrice.Should().Be(1_000);
        args.BodyDyePrice.Should().Be(1_000);
        args.GenderPrice.Should().Be(50_000);
        args.MaleHairstyles.Select(h => (h.Sprite, h.Price)).Should().Equal((0, 1_000), (1, 1_000), (97, 2_500));
        args.FemaleHairstyles.Should().HaveCount(3);
        args.Faces.Select(f => (f.Sprite, f.Name, f.Price, f.FemaleOnly)).Should().Equal((1, "Default", 50_000, false), (10, "Beauty", 50_000, false), (18, "Resting", 50_000, true));
        args.HairColors.Should().Equal(catalog.HairColors);
        args.BodyColors.Should().Equal(catalog.BodyColors);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run to confirm they fail to compile**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShopCatalogTests/*"`
Expected: build error — `BeautyShopCatalog` not found.

- [ ] **Step 3: Implement the catalog**

Create `Chaos/Services/BeautyShop/BeautyShopCatalog.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Services.BeautyShop;

/// <summary>One purchasable hairstyle: the head sprite and its price (the item template's buyCost).</summary>
public sealed record BeautyShopHairstyle(ushort Sprite, int Price);

/// <summary>One purchasable face shape.</summary>
public sealed record BeautyShopFace(byte Sprite, string Name, int Price, bool FemaleOnly);

/// <summary>
///     Everything Josephine sells and what it costs. Built once at startup by <see cref="BeautyShopCatalogLoader" />
///     and shared by the merchant script (to open the panel) and <see cref="BeautyShopCheckout" /> (to validate
///     and price an Apply). Pure data -- no cache, no I/O -- so it is trivially testable.
/// </summary>
public sealed class BeautyShopCatalog
{
    public const int STANDARD_GENDER_PRICE = 50_000;
    public const int MASTER_GENDER_PRICE = 1_000_000;

    public IReadOnlyList<BeautyShopHairstyle> MaleHairstyles { get; }
    public IReadOnlyList<BeautyShopHairstyle> FemaleHairstyles { get; }
    public IReadOnlyList<BeautyShopFace> Faces { get; }
    public int HairDyePrice { get; }
    public int BodyDyePrice { get; }

    /// <summary>Default first (it is "no dye"), then every other <see cref="DisplayColor" /> by name -- the order the old dialog used.</summary>
    public IReadOnlyList<DisplayColor> HairColors { get; }

    /// <summary>Every <see cref="BodyColor" /> by name -- the order the old dialog used.</summary>
    public IReadOnlyList<BodyColor> BodyColors { get; }

    public BeautyShopCatalog(
        IReadOnlyList<BeautyShopHairstyle> maleHairstyles,
        IReadOnlyList<BeautyShopHairstyle> femaleHairstyles,
        IReadOnlyList<BeautyShopFace> faces,
        int hairDyePrice,
        int bodyDyePrice)
    {
        MaleHairstyles = maleHairstyles;
        FemaleHairstyles = femaleHairstyles;
        Faces = faces;
        HairDyePrice = hairDyePrice;
        BodyDyePrice = bodyDyePrice;

        HairColors = Enum.GetValues<DisplayColor>()
                         .Where(c => c != DisplayColor.Default)
                         .OrderBy(c => c.ToString(), StringComparer.Ordinal)
                         .Prepend(DisplayColor.Default)
                         .ToList();

        BodyColors = Enum.GetValues<BodyColor>()
                         .OrderBy(c => c.ToString(), StringComparer.Ordinal)
                         .ToList();
    }

    public static int GenderPriceFor(bool isMaster) => isMaster ? MASTER_GENDER_PRICE : STANDARD_GENDER_PRICE;

    public IReadOnlyList<BeautyShopHairstyle> HairstylesFor(Gender gender)
        => gender == Gender.Male ? MaleHairstyles : FemaleHairstyles;

    public bool TryGetHairstyle(Gender gender, int sprite, out BeautyShopHairstyle? hairstyle)
    {
        hairstyle = HairstylesFor(gender).FirstOrDefault(h => h.Sprite == sprite);

        return hairstyle is not null;
    }

    /// <summary>A face is valid for <paramref name="gender" /> when it exists and is not female-only worn by a male.</summary>
    public bool TryGetFace(Gender gender, int sprite, out BeautyShopFace? face)
    {
        face = Faces.FirstOrDefault(f => f.Sprite == sprite);

        if (face is not null && face.FemaleOnly && (gender == Gender.Male))
            face = null;

        return face is not null;
    }

    /// <summary>The Open packet for <paramref name="source" />: their current look, their purse, and the whole catalog.</summary>
    public BeautyShopDisplayArgs BuildOpenArgs(Aisling source)
        => new()
        {
            Type = BeautyShopDisplayType.Open,
            Gender = source.Gender,
            HairStyle = (ushort)source.HairStyle,
            HairColor = source.HairColor,
            BodyColor = source.BodyColor,
            FaceSprite = (byte)source.FaceSprite,
            Gold = source.Gold,
            GenderPrice = GenderPriceFor(source.UserStatSheet.Master),
            HairDyePrice = HairDyePrice,
            BodyDyePrice = BodyDyePrice,
            MaleHairstyles = MaleHairstyles.Select(h => new BeautyShopHairstyleEntry { Sprite = h.Sprite, Price = h.Price }).ToList(),
            FemaleHairstyles = FemaleHairstyles.Select(h => new BeautyShopHairstyleEntry { Sprite = h.Sprite, Price = h.Price }).ToList(),
            Faces = Faces.Select(f => new BeautyShopFaceEntry { Sprite = f.Sprite, Name = f.Name, Price = f.Price, FemaleOnly = f.FemaleOnly }).ToList(),
            HairColors = HairColors,
            BodyColors = BodyColors
        };
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShopCatalogTests/*"`
Expected: 5 passed. `Gender`, `HairStyle`, `HairColor`, `BodyColor`, `FaceSprite` are plain `{ get; set; }` on `Aisling` (`Chaos/Models/World/Aisling.cs:48-65`) and `Gold` is on `Creature`, so the `setup` lambda can set them.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/BeautyShop/BeautyShopCatalog.cs Tests/Chaos.Tests/BeautyShop/BeautyShopCatalogTests.cs
git commit -m "BeautyShop: catalog model with per-gender validation and open packet builder"
```

```json:metadata
{"files": ["Chaos/Services/BeautyShop/BeautyShopCatalog.cs", "Tests/Chaos.Tests/BeautyShop/BeautyShopCatalogTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter \"/*/*/BeautyShopCatalogTests/*\"", "acceptanceCriteria": ["per-gender hairstyle lookup", "female-only face refused for males", "color list ordering", "gender price rule", "BuildOpenArgs mirrors aisling + catalog"], "modelTier": "standard"}
```

---

### Task 3: Catalog loader, DI registration, startup validation

**Goal:** The catalog is built from the real item templates at startup, registered as a singleton, and a missing template fails the boot loudly.

**Files:**
- Create: `Chaos/Services/BeautyShop/BeautyShopCatalogLoader.cs`
- Create: `Chaos/Services/BeautyShop/BeautyShopCatalogValidationService.cs`
- Modify: `Chaos/Extensions/ServiceCollectionExtensions.cs` (next to `services.AddSingleton<CasinoLedgerService>();` ≈ line 308)
- Test: `Tests/Chaos.Tests/BeautyShop/BeautyShopCatalogLoaderTests.cs`

**Acceptance Criteria:**
- [ ] Hairstyles are discovered by probing `male_hairstyle_0..101` / `female_hairstyle_0..101` with `ISimpleCache<ItemTemplate>.Exists`; absent keys (male 96) are skipped; price = template `BuyCost`; sprite = `ItemSprite.DisplaySprite`.
- [ ] Faces come from the fixed 18-key list in the order `FaceShapeScript` used (default first, rest alphabetical by key); name = template `Name` with a trailing " Face Shape" stripped; `restingbitchfaceshape` is `FemaleOnly`.
- [ ] Hair and body dye prices are `hairdyeContainer`'s `BuyCost`.
- [ ] A missing face template or `hairdyeContainer` throws `InvalidOperationException` naming the key; a gender with zero hairstyles throws too.
- [ ] `BeautyShopCatalog` resolves from DI and the hosted service logs the counts at startup.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShopCatalogLoaderTests/*"` → all passed; `dotnet build Chaos/Chaos.csproj` succeeds.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Tests/Chaos.Tests/BeautyShop/BeautyShopCatalogLoaderTests.cs`. The template builder copies the required-property block from `Tests/Chaos.Testing.Infrastructure/Mocks/MockItem.cs:20-50`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Models.Data;
using Chaos.Models.Templates;
using Chaos.Scripting.Abstractions;
using Chaos.Services.BeautyShop;
using Chaos.Storage.Abstractions;
using FluentAssertions;
using Moq;

namespace Chaos.Tests.BeautyShop;

public class BeautyShopCatalogLoaderTests
{
    private static ItemTemplate Template(string key, string name, ushort displaySprite, int buyCost)
        => new()
        {
            Name = name,
            TemplateKey = key,
            ItemSprite = new ItemSprite(1, displaySprite),
            PanelSprite = 1,
            Color = DisplayColor.Default,
            PantsColor = DisplayColor.Default,
            MaxStacks = 1,
            BuyCost = buyCost,
            SellValue = 0,
            Category = "test",
            Description = null,
            EquipmentType = null,
            Gender = null,
            Class = null,
            AdvClass = null,
            IsDyeable = false,
            IsModifiable = false,
            NoTrade = false,
            AccountBound = false,
            PreventBanking = false,
            Level = 1,
            AbilityLevel = 0,
            MaxDurability = null,
            Modifiers = null,
            Weight = 1,
            Cooldown = null,
            RequiresMaster = false,
            ScriptKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ScriptVars = new Dictionary<string, IScriptVars>(StringComparer.OrdinalIgnoreCase)
        };

    private static ISimpleCache<ItemTemplate> Cache(params ItemTemplate[] templates)
    {
        //models ExpiringFileCache: Exists sees nothing until ForceLoad has run
        var byKey = templates.ToDictionary(t => t.TemplateKey, StringComparer.OrdinalIgnoreCase);
        var loaded = false;
        var mock = new Mock<ISimpleCache<ItemTemplate>>();
        mock.Setup(c => c.ForceLoad()).Callback(() => loaded = true);
        mock.Setup(c => c.Exists(It.IsAny<string>())).Returns((string k) => loaded && byKey.ContainsKey(k));
        mock.Setup(c => c.Get(It.IsAny<string>())).Returns((string k) => byKey[k]);

        return mock.Object;
    }

    private static IEnumerable<ItemTemplate> AllFaces()
        => BeautyShopCatalogLoader.FACE_KEYS.Select((key, i) => Template(key, key == "defaultfaceshape" ? "Default" : $"{key} Face Shape", (ushort)(i + 1), 50_000));

    private static ItemTemplate DyeContainer() => Template("hairdyeContainer", "Hair Dye Container", 1, 1_000);

    [Test]
    public async Task Loads_hairstyles_faces_and_dye_prices()
    {
        var cache = Cache(
        [
            Template("male_hairstyle_0", "Hairstyle 0", 0, 1_000),
            Template("male_hairstyle_1", "Hairstyle 1", 1, 1_000),
            Template("male_hairstyle_97", "Hairstyle 97", 97, 2_500),
            Template("female_hairstyle_0", "Hairstyle 0", 0, 1_000),
            Template("female_hairstyle_96", "Hairstyle 96", 96, 1_000),
            DyeContainer(),
            ..AllFaces()
        ]);

        var catalog = BeautyShopCatalogLoader.Load(cache);

        catalog.MaleHairstyles.Select(h => (h.Sprite, h.Price)).Should().Equal((0, 1_000), (1, 1_000), (97, 2_500));
        catalog.FemaleHairstyles.Select(h => h.Sprite).Should().Equal(0, 96);
        catalog.HairDyePrice.Should().Be(1_000);
        catalog.BodyDyePrice.Should().Be(1_000);
        catalog.Faces.Should().HaveCount(18);
        catalog.Faces[0].Should().Be(new BeautyShopFace(1, "Default", 50_000, false));
        catalog.Faces.Single(f => f.FemaleOnly).Name.Should().Be("restingbitchfaceshape");
        catalog.Faces.Should().OnlyContain(f => !f.Name.EndsWith("Face Shape"));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Missing_face_template_fails_loudly()
    {
        var cache = Cache([Template("male_hairstyle_0", "H", 0, 1), Template("female_hairstyle_0", "H", 0, 1), DyeContainer(), ..AllFaces().Skip(1)]);

        var act = () => BeautyShopCatalogLoader.Load(cache);

        act.Should().Throw<InvalidOperationException>().WithMessage("*defaultfaceshape*");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Gender_with_no_hairstyles_fails_loudly()
    {
        var cache = Cache([Template("male_hairstyle_0", "H", 0, 1), DyeContainer(), ..AllFaces()]);

        var act = () => BeautyShopCatalogLoader.Load(cache);

        act.Should().Throw<InvalidOperationException>().WithMessage("*female*");

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Implement the loader**

Create `Chaos/Services/BeautyShop/BeautyShopCatalogLoader.cs`:

```csharp
#region
using Chaos.Extensions.Common;
using Chaos.Models.Templates;
using Chaos.Storage.Abstractions;
#endregion

namespace Chaos.Services.BeautyShop;

/// <summary>
///     Builds the <see cref="BeautyShopCatalog" /> from item templates, so pricing stays exactly as data-driven as
///     the old dialogs: each hairstyle and face charges its own template's buyCost, both dyes charge the dye
///     container's.
/// </summary>
public static class BeautyShopCatalogLoader
{
    public const string DYE_CONTAINER_KEY = "hairdyeContainer";
    public const string FEMALE_ONLY_FACE_KEY = "restingbitchfaceshape";
    private const int MAX_HAIRSTYLE_INDEX = 101;

    /// <summary>Default first, the rest alphabetical -- the order <c>FaceShapeScript</c> displayed them in.</summary>
    public static readonly IReadOnlyList<string> FACE_KEYS =
    [
        "defaultfaceshape",
        "beautyfaceshape",
        "blushfaceshape",
        "bugfaceshape",
        "closedeyesfaceshape",
        "deepeyesfaceshape",
        "derpyfaceshape",
        "fakesmilefaceshape",
        "fishfaceshape",
        "meanfaceshape",
        "oldmanfaceshape",
        "possessedfaceshape",
        "restingbitchfaceshape",
        "sleepyeyesfaceshape",
        "soullessfaceshape",
        "squintfaceshape",
        "steppedonalegofaceshape",
        "surprisedfaceshape"
    ];

    public static BeautyShopCatalog Load(ISimpleCache<ItemTemplate> templates)
    {
        //ExpiringFileCache.Exists only reports entries already resident in memory -- nothing preloads the item
        //cache at boot, so every probe below would be false on a cold start without this
        templates.ForceLoad();

        var male = LoadHairstyles(templates, "male");
        var female = LoadHairstyles(templates, "female");

        var faces = FACE_KEYS.Select(key =>
                            {
                                var template = Require(templates, key);

                                return new BeautyShopFace(
                                    (byte)template.ItemSprite.DisplaySprite,
                                    TrimFaceName(template.Name),
                                    template.BuyCost,
                                    key.EqualsI(FEMALE_ONLY_FACE_KEY));
                            })
                            .ToList();

        var dye = Require(templates, DYE_CONTAINER_KEY);

        return new BeautyShopCatalog(male, female, faces, dye.BuyCost, dye.BuyCost);
    }

    private static List<BeautyShopHairstyle> LoadHairstyles(ISimpleCache<ItemTemplate> templates, string genderPrefix)
    {
        var list = new List<BeautyShopHairstyle>();

        for (var i = 0; i <= MAX_HAIRSTYLE_INDEX; i++)
        {
            var key = $"{genderPrefix}_hairstyle_{i}";

            if (!templates.Exists(key))
                continue;

            var template = templates.Get(key);
            list.Add(new BeautyShopHairstyle(template.ItemSprite.DisplaySprite, template.BuyCost));
        }

        if (list.Count == 0)
            throw new InvalidOperationException($"Beauty shop catalog: no {genderPrefix}_hairstyle_N item templates were found");

        return list;
    }

    private static ItemTemplate Require(ISimpleCache<ItemTemplate> templates, string key)
    {
        if (!templates.Exists(key))
            throw new InvalidOperationException($"Beauty shop catalog: required item template '{key}' is missing");

        return templates.Get(key);
    }

    private static string TrimFaceName(string name)
    {
        const string SUFFIX = " Face Shape";

        return name.EndsWith(SUFFIX, StringComparison.OrdinalIgnoreCase) ? name[..^SUFFIX.Length] : name;
    }
}
```

- [ ] **Step 3: Startup validation service**

Create `Chaos/Services/BeautyShop/BeautyShopCatalogValidationService.cs`:

```csharp
#region
using Chaos.NLog.Logging.Definitions;
using Chaos.NLog.Logging.Extensions;
using Microsoft.Extensions.Hosting;
#endregion

namespace Chaos.Services.BeautyShop;

/// <summary>
///     Forces the beauty shop catalog to load at startup. Unlike the casino validators this one deliberately
///     lets the loader's exception escape: the catalog is built from templates Josephine has always required,
///     and a boot that silently ships a broken mirror is worse than a boot that stops and names the missing key.
/// </summary>
public sealed class BeautyShopCatalogValidationService(BeautyShopCatalog catalog, ILogger<BeautyShopCatalogValidationService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.WithTopics(Topics.Entities.Merchant)
              .LogInformation(
                  "Beauty shop catalog loaded: {@MaleStyles} male styles, {@FemaleStyles} female styles, {@Faces} faces",
                  catalog.MaleHairstyles.Count,
                  catalog.FemaleHairstyles.Count,
                  catalog.Faces.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

- [ ] **Step 4: DI**

In `Chaos/Extensions/ServiceCollectionExtensions.cs`, directly after `services.AddSingleton<CasinoLedgerService>();`:

```csharp
            //beauty shop: the catalog is built from item templates on first resolve (the template cache is loaded by
            //then); the hosted service resolves it at startup so a missing template stops the boot with a named key
            services.AddSingleton(sp => BeautyShopCatalogLoader.Load(sp.GetRequiredService<ISimpleCache<ItemTemplate>>()));
            services.AddHostedService<BeautyShopCatalogValidationService>();
```

Add `using Chaos.Services.BeautyShop;` to the file's usings. (Tasks 4 and 5 add `services.AddSingleton<GenderSwapService>();` and `services.AddSingleton<BeautyShopCheckout>();` right below these lines.)

- [ ] **Step 5: Run tests and build**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShopCatalogLoaderTests/*"`
Expected: 3 passed. If the `ISimpleCache<ItemTemplate>` mock complains about `Get`, check `Chaos.Storage.Abstractions/ISimpleCache.cs` for the exact method name (it is `Get(string key)` around line 26-30).

- [ ] **Step 6: Commit**

```bash
git add Chaos/Services/BeautyShop Chaos/Extensions/ServiceCollectionExtensions.cs Tests/Chaos.Tests/BeautyShop/BeautyShopCatalogLoaderTests.cs
git commit -m "BeautyShop: load the catalog from item templates and validate it at startup"
```

```json:metadata
{"files": ["Chaos/Services/BeautyShop/BeautyShopCatalogLoader.cs", "Chaos/Services/BeautyShop/BeautyShopCatalogValidationService.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Tests/Chaos.Tests/BeautyShop/BeautyShopCatalogLoaderTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter \"/*/*/BeautyShopCatalogLoaderTests/*\"", "acceptanceCriteria": ["hairstyles probed 0..101 per gender, absent keys skipped", "18 faces in FaceShapeScript order, names trimmed, restingbitch female-only", "dye prices from hairdyeContainer", "missing template throws with key", "singleton + hosted service registered"], "modelTier": "standard"}
```

---

### Task 4: GenderSwapService (extracted from SwapGenderScript)

**Goal:** The gear/inventory/bank swap and the body/face change live in a reusable service with no dialog or gold logic, behaviourally identical to `SwapGenderScript`.

**Files:**
- Create: `Chaos/Services/BeautyShop/GenderSwapService.cs`
- Modify: `Chaos/Extensions/ServiceCollectionExtensions.cs` (add `services.AddSingleton<GenderSwapService>();` under the beauty shop block from Task 3)
- Reference (read, do not edit yet — deleted in Task 7): `Chaos/Scripting/DialogScripts/Temuair/Generic/SwapGenderScript.cs`
- Test: `Tests/Chaos.Tests/BeautyShop/GenderSwapServiceTests.cs`

**Acceptance Criteria:**
- [ ] `CanSwap(BaseClass.Peasant)` is false; `CanSwap` is true for Warrior, Monk, Priest, Rogue, Wizard.
- [ ] `FemaleToMale` is the exact inverse of `MaleToFemale` for every class.
- [ ] `Swap(aisling, Gender.Female)` sets `Gender`, `BodySprite = BodySprite.Female`; `Swap(aisling, Gender.Male)` with `FaceSprite == 18` resets the face to 1.
- [ ] Equipped / inventory / bank items whose template key is in the map are replaced via `IItemFactory.Create(replacementKey)`; equipped items of the old gender not in the map are unequipped (same code as the script, moved verbatim).
- [ ] The service never touches gold.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/GenderSwapServiceTests/*"` → all passed

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Tests/Chaos.Tests/BeautyShop/GenderSwapServiceTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.BeautyShop;
using Chaos.Services.Factories.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Chaos.Tests.BeautyShop;

public class GenderSwapServiceTests
{
    private static GenderSwapService Build(Mock<IItemFactory>? itemFactory = null)
        => new(itemFactory?.Object ?? new Mock<IItemFactory>().Object, new Mock<ILogger<GenderSwapService>>().Object);

    [Test]
    public async Task Only_classes_with_a_gear_map_can_swap()
    {
        var service = Build();

        service.CanSwap(BaseClass.Peasant).Should().BeFalse();

        foreach (var baseClass in new[] { BaseClass.Warrior, BaseClass.Monk, BaseClass.Priest, BaseClass.Rogue, BaseClass.Wizard })
            service.CanSwap(baseClass).Should().BeTrue(baseClass.ToString());

        await Task.CompletedTask;
    }

    [Test]
    public async Task Female_to_male_map_is_the_inverse_of_male_to_female()
    {
        foreach (var (baseClass, forward) in GenderSwapService.MaleToFemale)
        {
            var backward = GenderSwapService.FemaleToMale[baseClass];

            foreach (var (male, female) in forward)
                backward[female].Should().Be(male);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Swap_changes_gender_body_sprite_and_resets_a_female_only_face()
    {
        var service = Build();

        var aisling = MockAisling.Create(
            setup: a =>
            {
                a.Gender = Gender.Female;
                a.BodySprite = BodySprite.Female;
                a.FaceSprite = 18;
                a.Gold = 12_345;
            });

        service.Swap(aisling, Gender.Male);

        aisling.Gender.Should().Be(Gender.Male);
        aisling.BodySprite.Should().Be(BodySprite.Male);
        aisling.FaceSprite.Should().Be(1);
        aisling.Gold.Should().Be(12_345);

        service.Swap(aisling, Gender.Female);

        aisling.Gender.Should().Be(Gender.Female);
        aisling.BodySprite.Should().Be(BodySprite.Female);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Implement the service**

Create `Chaos/Services/BeautyShop/GenderSwapService.cs`. The four private methods and `GenderMasterEquipmentMap`'s two dictionaries are **moved verbatim** from `SwapGenderScript.cs` (`ApplyGenderChange`, `ReplaceItemInSlot`, `SwapBankItems`, `SwapEquippedGear`, `SwapInventoryItems`, and the `MaleToFemale` initializer with all five classes). Only the surrounding shell changes:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.NLog.Logging.Definitions;
using Chaos.NLog.Logging.Extensions;
using Chaos.Services.Factories.Abstractions;
#endregion

namespace Chaos.Services.BeautyShop;

/// <summary>
///     Changes an aisling's gender and reshapes their Master / Grandmaster gear (equipped, inventory and bank) to
///     the other gender's counterpart. Lifted out of the retired <c>SwapGenderScript</c>; the gold charge now lives
///     in <see cref="BeautyShopCheckout" />, so this service only ever mutates appearance and items.
/// </summary>
public sealed class GenderSwapService(IItemFactory itemFactory, ILogger<GenderSwapService> logger)
{
    /// <summary>Move the whole MaleToFemale dictionary literal here from SwapGenderScript.GenderMasterEquipmentMap.</summary>
    public static readonly Dictionary<BaseClass, Dictionary<string, string>> MaleToFemale = new()
    {
        /* verbatim from SwapGenderScript.cs lines 240-380 */
    };

    public static readonly Dictionary<BaseClass, Dictionary<string, string>> FemaleToMale = MaleToFemale.ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value.ToDictionary(pair => pair.Value, pair => pair.Key));

    /// <summary>Whether this class has a gear map -- the old script refused the swap without one.</summary>
    public bool CanSwap(BaseClass baseClass) => MaleToFemale.ContainsKey(baseClass);

    /// <summary>
    ///     Swaps <paramref name="source" /> to <paramref name="toGender" />. Caller must have checked
    ///     <see cref="CanSwap" /> and taken payment. A no-op when the aisling is already that gender.
    /// </summary>
    public void Swap(Aisling source, Gender toGender)
    {
        var fromGender = source.Gender;

        if (fromGender == toGender)
            return;

        var swapMap = GetSwapMap(fromGender, source.UserStatSheet.BaseClass) ?? new Dictionary<string, string>();

        SwapEquippedGear(source, swapMap, fromGender);
        SwapInventoryItems(source, swapMap);
        SwapBankItems(source, swapMap);
        ApplyGenderChange(source, toGender);
    }

    private static Dictionary<string, string>? GetSwapMap(Gender fromGender, BaseClass baseClass)
        => fromGender == Gender.Male
            ? MaleToFemale.GetValueOrDefault(baseClass)
            : FemaleToMale.GetValueOrDefault(baseClass);

    //ApplyGenderChange, ReplaceItemInSlot, SwapBankItems, SwapEquippedGear, SwapInventoryItems:
    //paste verbatim from SwapGenderScript.cs, replacing `.WithProperty(this)` with `.WithProperty(source)`
    //and `itemFactory` stays the primary-constructor parameter.
}
```

`ApplyGenderChange` in the script already does `source.Gender = toGender; source.BodySprite = ...; if (source is { FaceSprite: 18, Gender: Gender.Male }) source.FaceSprite = 1; source.Refresh(true); source.Display();` — keep it exactly.

- [ ] **Step 3: Register**

In `ServiceCollectionExtensions.cs`, under the Task 3 lines: `services.AddSingleton<GenderSwapService>();`

- [ ] **Step 4: Run tests**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/GenderSwapServiceTests/*"`
Expected: 3 passed. `MockAisling` has an empty equipment/inventory/bank, so no `IItemFactory` call happens; `Refresh`/`Display` go to the mocked client / mock map.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/BeautyShop/GenderSwapService.cs Chaos/Extensions/ServiceCollectionExtensions.cs Tests/Chaos.Tests/BeautyShop/GenderSwapServiceTests.cs
git commit -m "BeautyShop: extract GenderSwapService from SwapGenderScript"
```

```json:metadata
{"files": ["Chaos/Services/BeautyShop/GenderSwapService.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Tests/Chaos.Tests/BeautyShop/GenderSwapServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter \"/*/*/GenderSwapServiceTests/*\"", "acceptanceCriteria": ["CanSwap false for Peasant", "maps are inverses", "Swap sets gender/body sprite/face reset, gold untouched", "item swap code moved verbatim"], "modelTier": "standard"}
```

---

### Task 5: BeautyShopCheckout (validate → diff → charge once → apply)

**Goal:** One class that turns an Apply request into either a reject reason or an applied, paid-for appearance, with gold taken exactly once for exactly the changed categories.

**Files:**
- Create: `Chaos/Services/BeautyShop/BeautyShopCheckout.cs`
- Modify: `Chaos/Extensions/ServiceCollectionExtensions.cs` (add `services.AddSingleton<BeautyShopCheckout>();`)
- Test: `Tests/Chaos.Tests/BeautyShop/BeautyShopCheckoutTests.cs`

**Acceptance Criteria:**
- [ ] Selection equal to the current look → `NothingChanged`, gold unchanged.
- [ ] Hairstyle not in the requested gender's list, or a female-only face with male gender, or an undefined color byte → `InvalidSelection`, nothing applied.
- [ ] Gender change for a class with no gear map → `GenderSwapUnavailable`.
- [ ] Total exceeds gold → `InsufficientGold`, **no field changed, no gold taken**.
- [ ] Valid multi-category change → returns `null`, gold reduced by exactly the sum of changed categories, every field applied.
- [ ] Only changed categories are charged (a re-selected identical hair color costs 0).

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShopCheckoutTests/*"` → all passed

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Tests/Chaos.Tests/BeautyShop/BeautyShopCheckoutTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Services.BeautyShop;
using Chaos.Services.Factories.Abstractions;
using Chaos.Testing.Infrastructure.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Chaos.Tests.BeautyShop;

public class BeautyShopCheckoutTests
{
    private static BeautyShopCheckout Build()
        => new(
            BeautyShopCatalogTests.Build(),
            new GenderSwapService(new Mock<IItemFactory>().Object, new Mock<ILogger<GenderSwapService>>().Object),
            new Mock<ILogger<BeautyShopCheckout>>().Object);

    /// <summary>A male warrior with hairstyle 1, default hair, white skin, default face and 100k gold.</summary>
    private static Aisling Player(int gold = 100_000)
        => MockAisling.Create(
            setup: a =>
            {
                a.Gender = Gender.Male;
                a.BodySprite = BodySprite.Male;
                a.HairStyle = 1;
                a.HairColor = DisplayColor.Default;
                a.BodyColor = BodyColor.White;
                a.FaceSprite = 1;
                a.Gold = gold;
                a.UserStatSheet.SetBaseClass(BaseClass.Warrior);
            });

    private static BeautyShopSelection Current(Aisling a)
        => new(a.Gender, a.HairStyle, a.HairColor, a.BodyColor, a.FaceSprite);

    [Test]
    public async Task Nothing_changed_is_rejected_and_free()
    {
        var player = Player();

        Build().TryApply(player, Current(player)).Should().Be(BeautyShopRejectReason.NothingChanged);

        player.Gold.Should().Be(100_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hairstyle_from_the_other_gender_is_invalid()
    {
        var player = Player();

        //96 exists only in the female list of the test catalog
        Build().TryApply(player, Current(player) with { HairStyle = 96 }).Should().Be(BeautyShopRejectReason.InvalidSelection);

        player.HairStyle.Should().Be(1);
        player.Gold.Should().Be(100_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Female_only_face_on_a_male_is_invalid()
    {
        var player = Player();

        Build().TryApply(player, Current(player) with { FaceSprite = 18 }).Should().Be(BeautyShopRejectReason.InvalidSelection);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Undefined_color_bytes_are_invalid()
    {
        var player = Player();

        Build().TryApply(player, Current(player) with { HairColor = (DisplayColor)250 }).Should().Be(BeautyShopRejectReason.InvalidSelection);
        Build().TryApply(player, Current(player) with { BodyColor = (BodyColor)250 }).Should().Be(BeautyShopRejectReason.InvalidSelection);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Gender_swap_without_a_gear_map_is_unavailable()
    {
        var player = Player();
        player.UserStatSheet.SetBaseClass(BaseClass.Peasant);

        Build().TryApply(player, Current(player) with { Gender = Gender.Female, HairStyle = 0 }).Should().Be(BeautyShopRejectReason.GenderSwapUnavailable);

        player.Gender.Should().Be(Gender.Male);
        player.Gold.Should().Be(100_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Insufficient_gold_applies_nothing()
    {
        var player = Player(gold: 1_500);

        //hairstyle 0 (1,000) + Apple hair dye (1,000) = 2,000 > 1,500
        var result = Build().TryApply(player, Current(player) with { HairStyle = 0, HairColor = DisplayColor.Apple });

        result.Should().Be(BeautyShopRejectReason.InsufficientGold);
        player.HairStyle.Should().Be(1);
        player.HairColor.Should().Be(DisplayColor.Default);
        player.Gold.Should().Be(1_500);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Valid_change_charges_the_sum_once_and_applies_everything()
    {
        var player = Player();

        //hairstyle 97 (2,500) + Scarlet dye (1,000) + Tan skin (1,000) + Beauty face (50,000) = 54,500
        var selection = new BeautyShopSelection(Gender.Male, 97, DisplayColor.Scarlet, BodyColor.Tan, 10);

        Build().TryApply(player, selection).Should().BeNull();

        player.Gold.Should().Be(100_000 - 54_500);
        player.HairStyle.Should().Be(97);
        player.HairColor.Should().Be(DisplayColor.Scarlet);
        player.BodyColor.Should().Be(BodyColor.Tan);
        player.FaceSprite.Should().Be(10);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Only_changed_categories_are_charged()
    {
        var player = Player();

        //only the skin changes: 1,000
        Build().TryApply(player, Current(player) with { BodyColor = BodyColor.Brown }).Should().BeNull();

        player.Gold.Should().Be(99_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Gender_change_is_priced_and_applied()
    {
        var player = Player();

        //female hairstyle 0 is a change from male 1 (1,000) + gender (50,000) = 51,000
        Build().TryApply(player, new BeautyShopSelection(Gender.Female, 0, DisplayColor.Default, BodyColor.White, 1)).Should().BeNull();

        player.Gender.Should().Be(Gender.Female);
        player.BodySprite.Should().Be(BodySprite.Female);
        player.HairStyle.Should().Be(0);
        player.Gold.Should().Be(100_000 - 51_000);

        await Task.CompletedTask;
    }
}
```

If `UserStatSheet.SetBaseClass` does not exist, look in `Chaos/Models/Data/UserStatSheet.cs` for the method that sets `BaseClass` (grep `BaseClass =`) and use that; the Peasant test needs the class to be settable.

- [ ] **Step 2: Implement checkout**

Create `Chaos/Services/BeautyShop/BeautyShopCheckout.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.NLog.Logging.Definitions;
using Chaos.NLog.Logging.Extensions;
#endregion

namespace Chaos.Services.BeautyShop;

/// <summary>The five values a player asks for. Gender first because it decides which hairstyle and face lists apply.</summary>
public sealed record BeautyShopSelection(Gender Gender, int HairStyle, DisplayColor HairColor, BodyColor BodyColor, int FaceSprite);

/// <summary>
///     Turns a selection into a paid, applied look -- or a reason it was refused. The client sends values, never
///     prices: every price here comes from the catalog, and gold is taken exactly once, for exactly the categories
///     that differ from the aisling's current look, before anything is changed.
/// </summary>
public sealed class BeautyShopCheckout(BeautyShopCatalog catalog, GenderSwapService genderSwap, ILogger<BeautyShopCheckout> logger)
{
    /// <summary>Returns <see langword="null" /> on success, otherwise the reason nothing was changed.</summary>
    public BeautyShopRejectReason? TryApply(Aisling source, BeautyShopSelection selection)
    {
        //── validate against the catalog for the REQUESTED gender ──
        //Gender is a flags enum: None (0) and Unisex (3) are "defined" but must never be bought
        if (selection.Gender is not (Gender.Male or Gender.Female) || !Enum.IsDefined(selection.HairColor) || !Enum.IsDefined(selection.BodyColor))
            return BeautyShopRejectReason.InvalidSelection;

        if (!catalog.TryGetHairstyle(selection.Gender, selection.HairStyle, out var hairstyle))
            return BeautyShopRejectReason.InvalidSelection;

        if (!catalog.TryGetFace(selection.Gender, selection.FaceSprite, out var face))
            return BeautyShopRejectReason.InvalidSelection;

        //── diff ──
        var genderChanged = selection.Gender != source.Gender;
        var hairstyleChanged = selection.HairStyle != source.HairStyle;
        var hairColorChanged = selection.HairColor != source.HairColor;
        var bodyColorChanged = selection.BodyColor != source.BodyColor;
        var faceChanged = selection.FaceSprite != source.FaceSprite;

        if (!genderChanged && !hairstyleChanged && !hairColorChanged && !bodyColorChanged && !faceChanged)
            return BeautyShopRejectReason.NothingChanged;

        if (genderChanged && !genderSwap.CanSwap(source.UserStatSheet.BaseClass))
            return BeautyShopRejectReason.GenderSwapUnavailable;

        //── price ──
        var total = 0;

        if (genderChanged)
            total += BeautyShopCatalog.GenderPriceFor(source.UserStatSheet.Master);

        if (hairstyleChanged)
            total += hairstyle!.Price;

        if (hairColorChanged)
            total += catalog.HairDyePrice;

        if (bodyColorChanged)
            total += catalog.BodyDyePrice;

        if (faceChanged)
            total += face!.Price;

        //── charge once, before anything changes ──
        if (!source.TryTakeGold(total))
        {
            logger.WithTopics(Topics.Entities.Aisling, Topics.Entities.Gold)
                  .WithProperty(source)
                  .LogInformation(
                      "Aisling {@AislingName} could not afford {@GoldAmount} gold at the Beauty Shop (has {@GoldInv})",
                      source.Name,
                      total,
                      source.Gold);

            return BeautyShopRejectReason.InsufficientGold;
        }

        //── apply; gender first so the hairstyle/face lists it implies are the ones we validated against ──
        if (genderChanged)
            genderSwap.Swap(source, selection.Gender);

        if (hairstyleChanged)
            source.HairStyle = selection.HairStyle;

        if (hairColorChanged)
            source.HairColor = selection.HairColor;

        if (bodyColorChanged)
            source.BodyColor = selection.BodyColor;

        if (faceChanged)
            source.FaceSprite = selection.FaceSprite;

        source.Refresh(true);
        source.Display();

        var log = logger.WithTopics(Topics.Entities.Aisling, Topics.Entities.Gold).WithProperty(source);

        if (genderChanged)
            log.LogInformation("Aisling {@AislingName} paid for a gender swap to {@Gender} in the Beauty Shop", source.Name, selection.Gender);

        if (hairstyleChanged)
            log.LogInformation("Aisling {@AislingName} spent {@GoldAmount} for hairstyle {@HairStyle} in the Beauty Shop", source.Name, hairstyle!.Price, selection.HairStyle);

        if (hairColorChanged)
            log.LogInformation("Aisling {@AislingName} spent {@GoldAmount} to dye their hair {@Color} in the Beauty Shop", source.Name, catalog.HairDyePrice, selection.HairColor);

        if (bodyColorChanged)
            log.LogInformation("Aisling {@AislingName} spent {@GoldAmount} to dye their skin {@Color} in the Beauty Shop", source.Name, catalog.BodyDyePrice, selection.BodyColor);

        if (faceChanged)
            log.LogInformation("Aisling {@AislingName} spent {@GoldAmount} for the {@FaceShape} face in the Beauty Shop", source.Name, face!.Price, face.Name);

        log.LogInformation("Aisling {@AislingName} paid {@GoldAmount} gold total in the Beauty Shop", source.Name, total);

        return null;
    }
}
```

- [ ] **Step 3: Register**

`services.AddSingleton<BeautyShopCheckout>();` under the beauty shop block in `ServiceCollectionExtensions.cs`.

- [ ] **Step 4: Run tests**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShopCheckoutTests/*"`
Expected: 9 passed.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/BeautyShop/BeautyShopCheckout.cs Chaos/Extensions/ServiceCollectionExtensions.cs Tests/Chaos.Tests/BeautyShop/BeautyShopCheckoutTests.cs
git commit -m "BeautyShop: checkout validates, diffs, charges once and applies"
```

```json:metadata
{"files": ["Chaos/Services/BeautyShop/BeautyShopCheckout.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Tests/Chaos.Tests/BeautyShop/BeautyShopCheckoutTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter \"/*/*/BeautyShopCheckoutTests/*\"", "acceptanceCriteria": ["NothingChanged free", "InvalidSelection for wrong-gender hairstyle/face/undefined color", "GenderSwapUnavailable without gear map", "InsufficientGold applies nothing", "valid change charged once, all fields applied", "only changed categories charged"], "modelTier": "standard"}
```

---

### Task 6: Merchant script, dialog script, WorldServer handler

**Goal:** Josephine can open the panel from a dialog option, and the server routes Apply/Close packets to her script by proximity.

**Files:**
- Create: `Chaos/Scripting/MerchantScripts/BeautyShopScript.cs`
- Create: `Chaos/Scripting/DialogScripts/Temuair/Mileth/BeautyShopOpenScript.cs`
- Modify: `Chaos/Services/Servers/WorldServer.cs` (add `OnBeautyShopInteraction` after `OnPokerTableInteraction` ≈ line 2291; register in `IndexHandlers` after `ClientHandlers[(byte)ClientOpCode.PokerTableInteraction] = ...` ≈ line 2977)

**Acceptance Criteria:**
- [ ] `BeautyShopOpenScript.OnDisplaying` closes the dialog (so `Dialog.Display` sends nothing — it returns early when `ActiveDialog` changed) and sends the Open packet built from the catalog.
- [ ] `WorldServer.OnBeautyShopInteraction` ignores disconnected clients, resolves the nearest `Merchant` within 12 tiles whose script `As<BeautyShopScript>()` is non-null, replies `Rejected(NotNearShop)` when none, and otherwise calls `HandleInteraction`.
- [ ] `BeautyShopScript.HandleInteraction`: Apply → `checkout.TryApply`; on a reason → `SendBeautyShopRejected(reason)`; on success → orange-bar message + `SendBeautyShopClose()`. Close → no-op.
- [ ] `dotnet build Chaos/Chaos.csproj` succeeds.

**Verify:** `dotnet build Chaos/Chaos.csproj -c Debug` → Build succeeded, 0 errors

**Steps:**

- [ ] **Step 1: Merchant script**

Create `Chaos/Scripting/MerchantScripts/BeautyShopScript.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking.Entities.Client;
using Chaos.Scripting.MerchantScripts.Abstractions;
using Chaos.Services.BeautyShop;
#endregion

namespace Chaos.Scripting.MerchantScripts;

/// <summary>
///     Josephine's mirror. Opened by <c>BeautyShopOpenScript</c> from her greeting dialog; Apply/Close packets are
///     routed here by <c>WorldServer.OnBeautyShopInteraction</c>, which finds this script by proximity rather than
///     trusting anything in the packet. Sits beside the <c>showdialog</c> script on the merchant template.
/// </summary>
public sealed class BeautyShopScript(Merchant subject, BeautyShopCatalog catalog, BeautyShopCheckout checkout) : MerchantScriptBase(subject)
{
    /// <summary>How far a player may stand from the merchant and still apply. Matches how far away a dialog can be answered from.</summary>
    public const int INTERACTION_RANGE = 12;

    /// <summary>Sends the Open display built from the catalog and the player's current look.</summary>
    public void Open(Aisling source) => source.Client.SendBeautyShopOpen(catalog.BuildOpenArgs(source));

    public void HandleInteraction(Aisling source, BeautyShopInteractionArgs args)
    {
        switch (args.Type)
        {
            case BeautyShopInteractionType.Apply:
            {
                var selection = new BeautyShopSelection(
                    args.Gender,
                    args.HairStyle,
                    args.HairColor,
                    args.BodyColor,
                    args.FaceSprite);

                var reason = checkout.TryApply(source, selection);

                if (reason is not null)
                {
                    source.Client.SendBeautyShopRejected(reason.Value);

                    return;
                }

                source.SendOrangeBarMessage("Josephine works her magic. Enjoy your new look!");
                source.Client.SendBeautyShopClose();

                break;
            }

            case BeautyShopInteractionType.Close:
                //nothing is held open server-side; the panel was purely a client view of the catalog
                break;
        }
    }
}
```

- [ ] **Step 2: Dialog script**

Create `Chaos/Scripting/DialogScripts/Temuair/Mileth/BeautyShopOpenScript.cs`:

```csharp
#region
using Chaos.Models.Menu;
using Chaos.Models.World;
using Chaos.Scripting.DialogScripts.Abstractions;
using Chaos.Scripting.MerchantScripts;
#endregion

namespace Chaos.Scripting.DialogScripts.Temuair.Mileth;

/// <summary>
///     On the <c>josephine_mirror</c> dialog: closes the dialog before it is ever shown and opens the mirror panel
///     instead. <c>Dialog.Display</c> calls <c>OnDisplaying</c> first and returns without sending the dialog when
///     the active dialog changed, so the player never sees this dialog's text.
/// </summary>
public class BeautyShopOpenScript(Dialog subject) : DialogScriptBase(subject)
{
    public override void OnDisplaying(Aisling source)
    {
        Subject.Close(source);

        var script = (Subject.DialogSource as Merchant)?.Script.As<BeautyShopScript>();

        if (script is null)
        {
            source.SendOrangeBarMessage("The mirror is covered. Josephine is not ready.");

            return;
        }

        script.Open(source);
    }
}
```

If `Dialog` exposes the source entity under a different name than `DialogSource`, grep `Chaos/Models/Menu/Dialog.cs` for `IDialogSourceEntity` and use that property.

- [ ] **Step 3: WorldServer handler**

In `Chaos/Services/Servers/WorldServer.cs`, after the closing brace of `OnPokerTableInteraction`:

```csharp
    /// <summary>
    ///     Routes a beauty shop Apply/Close to Josephine. Like the casino handlers, the merchant is resolved by
    ///     proximity, never from the packet, so a modified client cannot pick a shop it is not standing at.
    /// </summary>
    public ValueTask OnBeautyShopInteraction(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<BeautyShopInteractionArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnBeautyShopInteraction);

        static ValueTask InnerOnBeautyShopInteraction(IChaosWorldClient localClient, BeautyShopInteractionArgs localArgs)
        {
            var aisling = localClient.Aisling;

            if (!localClient.Connected)
                return default;

            var script = aisling.MapInstance
                                .GetEntitiesWithinRange<Merchant>(aisling, BeautyShopScript.INTERACTION_RANGE)
                                .Select(m => m.Script.As<BeautyShopScript>())
                                .FirstOrDefault(s => s is not null);

            if (script is null)
            {
                localClient.SendBeautyShopRejected(BeautyShopRejectReason.NotNearShop);

                return default;
            }

            script.HandleInteraction(aisling, localArgs);

            return default;
        }
    }
```

Add `using Chaos.Scripting.MerchantScripts;` if the file lacks it (it already has the casino namespace for `PokerTableScript`). In `IndexHandlers`, after the poker line:

```csharp
        ClientHandlers[(byte)ClientOpCode.BeautyShopInteraction] = OnBeautyShopInteraction;
```

- [ ] **Step 4: Build**

Run: `cd /c/Users/mikeb/Documents/GitHub/Chaos.Client/Chaos-Server && dotnet build Chaos/Chaos.csproj -c Debug`
Expected: Build succeeded. Then run the whole beauty shop test set once: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi --treenode-filter "/*/*/BeautyShop*/*"` → all passed.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Scripting/MerchantScripts/BeautyShopScript.cs Chaos/Scripting/DialogScripts/Temuair/Mileth/BeautyShopOpenScript.cs Chaos/Services/Servers/WorldServer.cs
git commit -m "BeautyShop: merchant script, mirror dialog script and WorldServer routing"
```

```json:metadata
{"files": ["Chaos/Scripting/MerchantScripts/BeautyShopScript.cs", "Chaos/Scripting/DialogScripts/Temuair/Mileth/BeautyShopOpenScript.cs", "Chaos/Services/Servers/WorldServer.cs"], "verifyCommand": "dotnet build Chaos/Chaos.csproj -c Debug", "acceptanceCriteria": ["dialog script closes then sends Open", "handler resolves merchant by proximity within 12, NotNearShop otherwise", "Apply routes to checkout; reject or close+message", "server builds"], "modelTier": "standard"}
```

---

### Task 7: Data changes and retiring the old dialogs/scripts

**Goal:** Josephine's greeting has one option that opens the mirror; the five sub-dialogs and their scripts are gone; nothing else referenced them.

**Files:**
- Modify (Unora): `Data/Configuration/Templates/Dialogs/Temauir/mileth/josephine/josephine_initial.json`
- Create (Unora): `Data/Configuration/Templates/Dialogs/Temauir/mileth/josephine/josephine_mirror.json`
- Delete (Unora): `josephine_buyShop.json`, `josephine_changeHairDye.json`, `josephine_changeBodyDye.json`, `josephine_changeFaceShape.json`, `josephine_changeGender.json`, `josephine_swapGenderConfirm.json` in the same folder
- Modify (Unora): `Data/Configuration/Templates/Merchants/Temauir/mileth/josephine.json`
- Delete (server): `Chaos/Scripting/DialogScripts/Temuair/Generic/HairStyleScript.cs`, `HairDyeScript.cs`, `BodyDyeScript.cs`, `FaceShapeScript.cs`, `SwapGenderScript.cs`

**Acceptance Criteria:**
- [ ] `grep -ril "josephine_buyShop\|josephine_change\|josephine_swapgenderconfirm\|\"hairStyle\"\|\"hairDye\"\|\"bodyDye\"\|\"faceShape\"\|\"swapgender\"" Unora/Data` returns nothing.
- [ ] `grep -rn "SwapGenderScript\|HairstyleScript\|HairDyeScript\|BodyDyeScript\|FaceShapeScript\|GenderMasterEquipmentMap" Chaos-Server --include=*.cs` returns nothing.
- [ ] `josephine_initial.json` keeps its text and `josephineReward` script key and has exactly one option → `josephine_mirror`.
- [ ] Server builds; full server test suite has no new failures beyond the four known upstream ones.

**Verify:** `dotnet build Chaos/Chaos.csproj -c Debug` succeeds and both greps above are empty.

**Steps:**

- [ ] **Step 1: Rewrite `josephine_initial.json`**

```json
{
  "options": [
    {
      "dialogKey": "josephine_mirror",
      "optionText": "Show me the mirror"
    }
  ],
  "scriptKeys": [
    "josephineReward"
  ],
  "scriptVars": {},
  "templateKey": "josephine_initial",
  "text": "Through style and sorcery, I can reshape your look..your hair, color, face, or form. Even your essence, should you desire change.",
  "type": "DialogMenu"
}
```

- [ ] **Step 2: Create `josephine_mirror.json`**

```json
{
  "options": [],
  "scriptKeys": [
    "beautyShopOpen"
  ],
  "scriptVars": {},
  "templateKey": "josephine_mirror",
  "text": "Step up to the mirror.",
  "type": "Normal"
}
```

- [ ] **Step 3: Merchant template**

In `josephine.json` change `"scriptKeys"` to `[ "Trickortreatmerchant", "ChristmasCarolingMerchant", "showdialog", "beautyShop" ]`. Leave `scriptVars.showdialog.dialogKey` as `Josephine_initial`.

- [ ] **Step 4: Delete the six dialog templates and the five server scripts**

```bash
cd /c/Users/mikeb/Documents/GitHub/Unora/Data/Configuration/Templates/Dialogs/Temauir/mileth/josephine
git rm josephine_buyShop.json josephine_changeHairDye.json josephine_changeBodyDye.json josephine_changeFaceShape.json josephine_changeGender.json josephine_swapGenderConfirm.json
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client/Chaos-Server
git rm Chaos/Scripting/DialogScripts/Temuair/Generic/HairStyleScript.cs Chaos/Scripting/DialogScripts/Temuair/Generic/HairDyeScript.cs Chaos/Scripting/DialogScripts/Temuair/Generic/BodyDyeScript.cs Chaos/Scripting/DialogScripts/Temuair/Generic/FaceShapeScript.cs Chaos/Scripting/DialogScripts/Temuair/Generic/SwapGenderScript.cs
```

- [ ] **Step 5: Prove nothing else referenced them**

```bash
cd /c/Users/mikeb/Documents/GitHub
grep -ril "josephine_buyShop\|josephine_change\|josephine_swapgenderconfirm\|\"hairStyle\"\|\"hairDye\"\|\"bodyDye\"\|\"faceShape\"\|\"swapgender\"" Unora/Data || echo "data clean"
grep -rn "SwapGenderScript\|HairstyleScript\|HairDyeScript\|BodyDyeScript\|FaceShapeScript\|GenderMasterEquipmentMap" Chaos.Client/Chaos-Server --include=*.cs || echo "server clean"
grep -rn "josephine" Unora/Data/Configuration/Templates/Dialogs/Temauir/Quests/TutorialQuest/*.json   # Riona's dialogs must only reference josephine_initial or text
```

- [ ] **Step 6: Build and run the full server suite**

```bash
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client/Chaos-Server
dotnet build Chaos/Chaos.csproj -c Debug
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --no-ansi
```

Expected: build succeeded; only the four known upstream failures.

- [ ] **Step 7: Commit both repos**

```bash
cd /c/Users/mikeb/Documents/GitHub/Unora
git add Data/Configuration/Templates/Dialogs/Temauir/mileth/josephine Data/Configuration/Templates/Merchants/Temauir/mileth/josephine.json
git commit -m "Josephine: greeting opens the mirror panel; retire per-category dialogs"
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client/Chaos-Server
git commit -am "BeautyShop: retire the five per-category dialog scripts"   # only the five deletions are staged; verify with git status first
```

```json:metadata
{"files": ["Unora/Data/Configuration/Templates/Dialogs/Temauir/mileth/josephine/josephine_initial.json", "Unora/Data/Configuration/Templates/Dialogs/Temauir/mileth/josephine/josephine_mirror.json", "Unora/Data/Configuration/Templates/Merchants/Temauir/mileth/josephine.json", "Chaos/Scripting/DialogScripts/Temuair/Generic/SwapGenderScript.cs"], "verifyCommand": "dotnet build Chaos/Chaos.csproj -c Debug", "acceptanceCriteria": ["no data references to deleted dialogs/script keys", "no server references to deleted scripts", "initial dialog: one option, quest hook kept", "server builds, no new test failures"], "modelTier": "mechanical"}
```

---

### Task 8: Client networking (event, handler, sends)

**Goal:** The client raises an event when a `BeautyShopDisplay` packet arrives and can send Apply/Close.

**Files:**
- Modify: `Chaos.Client.Networking/Definitions/Delegates.cs` (after `PokerTableDisplayHandler` ≈ line 385)
- Modify: `Chaos.Client.Networking/ConnectionManager.cs` (event after `OnPokerTableDisplay` ≈ line 546; sends after `SendPokerClose` ≈ line 1195; registration after the `PokerTableDisplay` line in `IndexHandlers` ≈ line 1583; handler after `HandlePokerTableDisplay` ≈ line 1964)

**Acceptance Criteria:**
- [ ] `ConnectionManager.OnBeautyShopDisplay` event of type `BeautyShopDisplayHandler` exists and fires with the deserialized args.
- [ ] `SendBeautyShopApply(Gender, ushort, DisplayColor, BodyColor, byte)` and `SendBeautyShopClose()` send through `SendIfWorld`.
- [ ] `dotnet build Chaos.Client.Networking/Chaos.Client.Networking.csproj` succeeds.

**Verify:** `cd Chaos.Client && dotnet build Chaos.Client.Networking/Chaos.Client.Networking.csproj` → Build succeeded

**Steps:**

- [ ] **Step 1: Delegate**

In `Delegates.cs`, after the `PokerTableDisplayHandler` declaration:

```csharp
/// <summary>
///     Fired when a beauty shop display packet is received.
/// </summary>
public delegate void BeautyShopDisplayHandler(BeautyShopDisplayArgs args);
```

- [ ] **Step 2: ConnectionManager**

Event (after `OnPokerTableDisplay`):

```csharp
    /// <summary>
    ///     Fired when a beauty shop display packet is received from the server.
    /// </summary>
    public event BeautyShopDisplayHandler? OnBeautyShopDisplay;
```

Sends (after `SendPokerClose`):

```csharp
    /// <summary>
    ///     Asks Josephine to apply the chosen look. Values only -- the server prices and validates everything.
    /// </summary>
    public void SendBeautyShopApply(Gender gender, ushort hairStyle, DisplayColor hairColor, BodyColor bodyColor, byte faceSprite)
        => SendIfWorld(
            new BeautyShopInteractionArgs
            {
                Type = BeautyShopInteractionType.Apply,
                Gender = gender,
                HairStyle = hairStyle,
                HairColor = hairColor,
                BodyColor = bodyColor,
                FaceSprite = faceSprite
            });

    /// <summary>
    ///     Tells the server the mirror was closed without applying.
    /// </summary>
    public void SendBeautyShopClose() => SendIfWorld(new BeautyShopInteractionArgs { Type = BeautyShopInteractionType.Close });
```

Registration in `IndexHandlers` (after the `PokerTableDisplay` line):

```csharp
        PacketHandlers[(byte)ServerOpCode.BeautyShopDisplay] = HandleBeautyShopDisplay;
```

Handler (after `HandlePokerTableDisplay`):

```csharp
    private void HandleBeautyShopDisplay(ServerPacket pkt)
    {
        var args = Client.Deserialize<BeautyShopDisplayArgs>(in pkt);
        OnBeautyShopDisplay?.Invoke(args);
    }
```

- [ ] **Step 3: Build and commit**

```bash
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client
dotnet build Chaos.Client.Networking/Chaos.Client.Networking.csproj
git add Chaos.Client.Networking
git commit -m "BeautyShop: client event, handler and sends"
```

```json:metadata
{"files": ["Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client.Networking/ConnectionManager.cs"], "verifyCommand": "dotnet build Chaos.Client.Networking/Chaos.Client.Networking.csproj", "acceptanceCriteria": ["OnBeautyShopDisplay event fires with args", "SendBeautyShopApply/Close via SendIfWorld", "networking project builds"], "modelTier": "mechanical"}
```

---

### Task 9: Client test project + BeautyShop view model

**Goal:** A pure, unit-tested view model holding the catalog, the current look, the selection, and the arithmetic (total, affordability, gender remap), plus the first client test project.

**Files:**
- Create: `Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj`
- Create: `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs`
- Modify: `Directory.Packages.props` (add TUnit / FluentAssertions versions)
- Modify: `Chaos.Client.slnx` (add the test project)
- Modify: `CLAUDE.md` line 22 ("No test projects exist currently.") → describe the new project and the `dotnet run` rule
- Create: `Chaos.Client/ViewModel/BeautyShop.cs`
- Modify: `Chaos.Client/Collections/WorldState.cs` (singleton after `PokerTable` ≈ line 153; `BeautyShop.Clear()` after `PokerTable.Clear()` ≈ line 394)

**Acceptance Criteria:**
- [ ] `ApplyOpen` copies current values into both Current* and selected fields; `IsOpen` true; `Clear` resets.
- [ ] `Total` = sum of changed categories using per-entry hairstyle/face prices and flat dye prices; `CanAfford = Total <= Gold`; `CanApply = Total > 0 && CanAfford`.
- [ ] `SetGender` swaps the hairstyle list, keeps the same sprite id when the other list has it, else picks the first entry; a female-only face becomes the first non-female-only face when switching to male.
- [ ] `StepHairstyle/StepHairColor/StepBodyColor/StepFace(±1)` wrap around their lists; `AvailableFaces` hides female-only faces for males.
- [ ] `Reset` restores every selected value to current.
- [ ] `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` passes.

**Verify:** `cd Chaos.Client && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → all passed

**Steps:**

- [ ] **Step 1: Test project**

Add to `Directory.Packages.props` `<ItemGroup>`:

```xml
        <PackageVersion Include="TUnit" Version="1.1.10" />
        <PackageVersion Include="FluentAssertions" Version="8.8.0" />
```

Create `Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <IsPackable>false</IsPackable>
        <GenerateDocumentationFile>false</GenerateDocumentationFile>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="TUnit"/>
        <PackageReference Include="FluentAssertions"/>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\Chaos.Client\Chaos.Client.csproj"/>
    </ItemGroup>

</Project>
```

Add to `Chaos.Client.slnx` after the `Chaos.Client/Chaos.Client.csproj` line: `<Project Path="Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj" />`.

Replace CLAUDE.md's "No test projects exist currently." with: "Tests live in `Tests/Chaos.Client.Tests` (TUnit). Run with `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`; `dotnet test` does not work with this SDK."

- [ ] **Step 2: Write the failing tests**

Create `Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs`:

```csharp
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class BeautyShopViewModelTests
{
    private static BeautyShopDisplayArgs Open()
        => new()
        {
            Type = BeautyShopDisplayType.Open,
            Gender = Gender.Male,
            HairStyle = 1,
            HairColor = DisplayColor.Default,
            BodyColor = BodyColor.White,
            FaceSprite = 1,
            Gold = 60_000,
            GenderPrice = 50_000,
            HairDyePrice = 1_000,
            BodyDyePrice = 1_000,
            MaleHairstyles = [new BeautyShopHairstyleEntry { Sprite = 0, Price = 1_000 }, new BeautyShopHairstyleEntry { Sprite = 1, Price = 1_000 }, new BeautyShopHairstyleEntry { Sprite = 97, Price = 2_500 }],
            FemaleHairstyles = [new BeautyShopHairstyleEntry { Sprite = 0, Price = 1_000 }, new BeautyShopHairstyleEntry { Sprite = 1, Price = 1_000 }, new BeautyShopHairstyleEntry { Sprite = 96, Price = 1_000 }],
            Faces =
            [
                new BeautyShopFaceEntry { Sprite = 1, Name = "Default", Price = 50_000, FemaleOnly = false },
                new BeautyShopFaceEntry { Sprite = 10, Name = "Beauty", Price = 50_000, FemaleOnly = false },
                new BeautyShopFaceEntry { Sprite = 18, Name = "Resting", Price = 50_000, FemaleOnly = true }
            ],
            HairColors = [DisplayColor.Default, DisplayColor.Apple, DisplayColor.Scarlet],
            BodyColors = [BodyColor.Brown, BodyColor.Tan, BodyColor.White]
        };

    private static BeautyShop Opened()
    {
        var vm = new BeautyShop();
        vm.ApplyOpen(Open());

        return vm;
    }

    [Test]
    public async Task ApplyOpen_seeds_current_and_selected_and_Clear_resets()
    {
        var vm = Opened();

        vm.IsOpen.Should().BeTrue();
        vm.Gender.Should().Be(Gender.Male);
        vm.HairStyle.Should().Be(1);
        vm.Total.Should().Be(0);
        vm.CanApply.Should().BeFalse();

        vm.Clear();

        vm.IsOpen.Should().BeFalse();
        vm.Hairstyles.Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Total_sums_only_changed_categories()
    {
        var vm = Opened();

        vm.StepHairstyle(+1);          // 1 -> 97 (2,500)
        vm.StepHairColor(+1);          // Default -> Apple (1,000)
        vm.StepFace(+1);               // Default -> Beauty (50,000)

        vm.Total.Should().Be(53_500);
        vm.CanAfford.Should().BeTrue();
        vm.CanApply.Should().BeTrue();

        vm.StepHairColor(-1);          // back to Default: no longer charged

        vm.Total.Should().Be(52_500);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unaffordable_total_blocks_apply()
    {
        var vm = Opened();

        vm.SetGender(Gender.Female);   // 50,000
        vm.StepFace(+1);               // 50,000

        vm.Total.Should().Be(100_000);
        vm.CanAfford.Should().BeFalse();
        vm.CanApply.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Steps_wrap_around_their_lists()
    {
        var vm = Opened();

        vm.StepHairstyle(-1);
        vm.HairStyle.Should().Be(0);
        vm.StepHairstyle(-1);
        vm.HairStyle.Should().Be(97);
        vm.StepBodyColor(-1);
        vm.BodyColor.Should().Be(BodyColor.Tan);

        await Task.CompletedTask;
    }

    [Test]
    public async Task SetGender_keeps_the_hairstyle_when_the_other_list_has_it()
    {
        var vm = Opened();

        vm.SetGender(Gender.Female);

        vm.Gender.Should().Be(Gender.Female);
        vm.HairStyle.Should().Be(1);
        vm.Hairstyles.Select(h => h.Sprite).Should().Equal(0, 1, 96);
        vm.Total.Should().Be(50_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task SetGender_falls_back_to_the_first_hairstyle_and_drops_female_only_faces()
    {
        var vm = Opened();
        vm.SetGender(Gender.Female);
        vm.StepHairstyle(+1);          // 1 -> 96 (female only)
        vm.StepFace(-1);               // Default -> Resting (female only, last in list)

        vm.FaceSprite.Should().Be(18);

        vm.SetGender(Gender.Male);

        vm.HairStyle.Should().Be(0);
        vm.FaceSprite.Should().Be(1);
        vm.AvailableFaces.Should().OnlyContain(f => !f.FemaleOnly);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reset_restores_the_current_look()
    {
        var vm = Opened();
        vm.SetGender(Gender.Female);
        vm.StepHairColor(+1);
        vm.StepBodyColor(+1);

        vm.Reset();

        vm.Gender.Should().Be(Gender.Male);
        vm.HairColor.Should().Be(DisplayColor.Default);
        vm.BodyColor.Should().Be(BodyColor.White);
        vm.Total.Should().Be(0);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Implement the view model**

Create `Chaos.Client/ViewModel/BeautyShop.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Authoritative beauty shop state: the catalog and prices the server sent on Open, the player's current look,
///     and the look they are trying on. Pure -- no textures, no packets -- so the arithmetic is unit-tested and the
///     control only reads from it.
/// </summary>
public sealed class BeautyShop
{
    public bool IsOpen { get; private set; }

    //catalog
    public IReadOnlyList<BeautyShopHairstyleEntry> MaleHairstyles { get; private set; } = [];
    public IReadOnlyList<BeautyShopHairstyleEntry> FemaleHairstyles { get; private set; } = [];
    public IReadOnlyList<BeautyShopFaceEntry> Faces { get; private set; } = [];
    public IReadOnlyList<DisplayColor> HairColors { get; private set; } = [];
    public IReadOnlyList<BodyColor> BodyColors { get; private set; } = [];
    public int GenderPrice { get; private set; }
    public int HairDyePrice { get; private set; }
    public int BodyDyePrice { get; private set; }
    public int Gold { get; private set; }

    //current look (what the server says the player has now)
    public Gender CurrentGender { get; private set; }
    public int CurrentHairStyle { get; private set; }
    public DisplayColor CurrentHairColor { get; private set; }
    public BodyColor CurrentBodyColor { get; private set; }
    public int CurrentFaceSprite { get; private set; }

    //selection (what the preview shows)
    public Gender Gender { get; private set; }
    public int HairStyle { get; private set; }
    public DisplayColor HairColor { get; private set; }
    public BodyColor BodyColor { get; private set; }
    public int FaceSprite { get; private set; }

    public IReadOnlyList<BeautyShopHairstyleEntry> Hairstyles => Gender == Gender.Male ? MaleHairstyles : FemaleHairstyles;

    public IReadOnlyList<BeautyShopFaceEntry> AvailableFaces
        => Gender == Gender.Male ? Faces.Where(f => !f.FemaleOnly).ToList() : Faces;

    public bool GenderChanged => Gender != CurrentGender;
    public bool HairstyleChanged => HairStyle != CurrentHairStyle;
    public bool HairColorChanged => HairColor != CurrentHairColor;
    public bool BodyColorChanged => BodyColor != CurrentBodyColor;
    public bool FaceChanged => FaceSprite != CurrentFaceSprite;

    public int HairstylePrice => Hairstyles.FirstOrDefault(h => h.Sprite == HairStyle)?.Price ?? 0;
    public int FacePrice => Faces.FirstOrDefault(f => f.Sprite == FaceSprite)?.Price ?? 0;

    public int Total
        => (GenderChanged ? GenderPrice : 0)
           + (HairstyleChanged ? HairstylePrice : 0)
           + (HairColorChanged ? HairDyePrice : 0)
           + (BodyColorChanged ? BodyDyePrice : 0)
           + (FaceChanged ? FacePrice : 0);

    public bool CanAfford => Total <= Gold;
    public bool CanApply => (Total > 0) && CanAfford;

    public void ApplyOpen(BeautyShopDisplayArgs args)
    {
        MaleHairstyles = args.MaleHairstyles;
        FemaleHairstyles = args.FemaleHairstyles;
        Faces = args.Faces;
        HairColors = args.HairColors;
        BodyColors = args.BodyColors;
        GenderPrice = args.GenderPrice;
        HairDyePrice = args.HairDyePrice;
        BodyDyePrice = args.BodyDyePrice;
        Gold = args.Gold;

        CurrentGender = args.Gender;
        CurrentHairStyle = args.HairStyle;
        CurrentHairColor = args.HairColor;
        CurrentBodyColor = args.BodyColor;
        CurrentFaceSprite = args.FaceSprite;

        Reset();
        IsOpen = true;
    }

    public void Clear()
    {
        IsOpen = false;
        MaleHairstyles = [];
        FemaleHairstyles = [];
        Faces = [];
        HairColors = [];
        BodyColors = [];
        GenderPrice = HairDyePrice = BodyDyePrice = Gold = 0;
        CurrentGender = Gender = Gender.Male;
        CurrentHairStyle = HairStyle = 0;
        CurrentHairColor = HairColor = DisplayColor.Default;
        CurrentBodyColor = BodyColor = BodyColor.White;
        CurrentFaceSprite = FaceSprite = 0;
    }

    public void Reset()
    {
        Gender = CurrentGender;
        HairStyle = CurrentHairStyle;
        HairColor = CurrentHairColor;
        BodyColor = CurrentBodyColor;
        FaceSprite = CurrentFaceSprite;
    }

    /// <summary>
    ///     Switches gender. The hairstyle keeps its id if the other list has it (most ids exist for both), else
    ///     falls back to the first entry; a female-only face falls back to the first face a male may wear -- the
    ///     same rules the server applies, so the preview never shows something Apply would refuse.
    /// </summary>
    public void SetGender(Gender gender)
    {
        if (Gender == gender)
            return;

        Gender = gender;

        if (Hairstyles.All(h => h.Sprite != HairStyle))
            HairStyle = Hairstyles.Count > 0 ? Hairstyles[0].Sprite : 0;

        if (AvailableFaces.All(f => f.Sprite != FaceSprite))
            FaceSprite = AvailableFaces.Count > 0 ? AvailableFaces[0].Sprite : 0;
    }

    public void StepHairstyle(int delta)
        => HairStyle = Step(Hairstyles, h => h.Sprite == HairStyle, delta)?.Sprite ?? HairStyle;

    public void StepFace(int delta)
        => FaceSprite = Step(AvailableFaces, f => f.Sprite == FaceSprite, delta)?.Sprite ?? FaceSprite;

    public void StepHairColor(int delta)
    {
        var next = Step(HairColors, c => c == HairColor, delta);

        if (next.HasValue)
            HairColor = next.Value;
    }

    public void StepBodyColor(int delta)
    {
        var next = Step(BodyColors, c => c == BodyColor, delta);

        if (next.HasValue)
            BodyColor = next.Value;
    }

    /// <summary>Wrapping step through <paramref name="list" /> from the entry matching <paramref name="isCurrent" /> (or from the start when none matches).</summary>
    private static T? Step<T>(IReadOnlyList<T> list, Func<T, bool> isCurrent, int delta)
    {
        if (list.Count == 0)
            return default;

        var index = -1;

        for (var i = 0; i < list.Count; i++)
            if (isCurrent(list[i]))
            {
                index = i;

                break;
            }

        var next = index < 0 ? 0 : (index + delta) % list.Count;

        if (next < 0)
            next += list.Count;

        return list[next];
    }
}
```

`Step` for enums returns `T?` where `T` is a value type: declare two private overloads if the compiler rejects the nullable mix — `StepEntry<T>(...) where T : class` returning `T?` and `StepValue<T>(...) where T : struct` returning `T?` — with the same body.

- [ ] **Step 4: WorldState**

In `Chaos.Client/Collections/WorldState.cs` after the `PokerTable` property:

```csharp
    /// <summary>
    ///     Authoritative beauty shop state (catalog, prices, current look, and the look being tried on).
    /// </summary>
    public static BeautyShop BeautyShop { get; } = new();
```

and after `PokerTable.Clear();` in the logout reset: `BeautyShop.Clear();`.

- [ ] **Step 5: Run tests**

Run: `cd /c/Users/mikeb/Documents/GitHub/Chaos.Client && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: 7 passed. If the test project fails to reference the `WinExe` client, add `<UseAppHost>false</UseAppHost>` to the test csproj or, failing that, move `BeautyShop.cs` into `Chaos.Client.Networking` (it depends only on `Chaos.Networking`/`Chaos.DarkAges` types) and reference that project instead.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props Chaos.Client.slnx CLAUDE.md Tests Chaos.Client/ViewModel/BeautyShop.cs Chaos.Client/Collections/WorldState.cs
git commit -m "BeautyShop: view model with totals and gender remap, first client test project"
```

```json:metadata
{"files": ["Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj", "Tests/Chaos.Client.Tests/BeautyShopViewModelTests.cs", "Directory.Packages.props", "Chaos.Client.slnx", "CLAUDE.md", "Chaos.Client/ViewModel/BeautyShop.cs", "Chaos.Client/Collections/WorldState.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["ApplyOpen/Clear", "Total only changed categories", "CanAfford/CanApply", "SetGender remaps hairstyle and face", "steps wrap", "Reset restores current", "client tests run via dotnet run"], "modelTier": "standard"}
```

---

### Task 10: BeautyShopControl — frame, preview, rotate, gear toggle, close

**Goal:** The panel opens as an ornate framed window with a live full-sprite preview that re-renders only when the selection, facing or gear toggle changes, and closes via its button or Escape.

**Files:**
- Modify: `Chaos.Client.Rendering/AislingRenderer.cs` (`private const int BODY_ID` → `public const int BODY_ID`, ≈ line 88-91; `IDLE_ANIM` → `public const string IDLE_ANIM`)
- Create: `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs`

**Acceptance Criteria:**
- [ ] `BeautyShopControl : FramedDialogPanelBase` built with `base("_nsett", false)`, `UsesControlStack = true`, centred, `Visible = false` until `Show()`.
- [ ] `PreviewView` (nested) renders via `AislingRenderer.Render(in appearance, frame, AislingRenderer.IDLE_ANIM, flip, isFront)` into one cached `Texture2D`, disposing the previous one; `Refresh()` re-renders only when `(appearance, facingIndex)` differ from the last rendered pair.
- [ ] Four facings cycle with ◀ ▶: Down (frame 5, flipped, front) is the default, then Right (5, unflipped, front), Up (0, unflipped, back), Left (0, flipped, back).
- [ ] "Show gear" `CustomCheckBox`, unchecked by default: unchecked → appearance with only `Gender`, `BodySpriteId = AislingRenderer.BODY_ID`, `BodyColor`, `HeadSprite`, `HeadColor`, `FaceSprite`; checked → `WorldState.GetPlayerEntity()?.Appearance` with those five fields overridden (falls back to bare when the player entity or its appearance is null).
- [ ] Close button and Escape both raise `Closed` and hide; `Hide()` on an already-hidden panel is a no-op (same guard as poker).
- [ ] `dotnet build Chaos.Client/Chaos.Client.csproj` succeeds (the control is not yet wired into `WorldScreen`).

**Verify:** `cd Chaos.Client && dotnet build Chaos.Client/Chaos.Client.csproj` → Build succeeded

**Steps:**

- [ ] **Step 1: Expose the two renderer constants**

In `AislingRenderer.cs` change `private const int BODY_ID` and `private const string IDLE_ANIM` to `public const`. Nothing else changes.

- [ ] **Step 2: Create the control (skeleton — Task 11 adds the rows and footer)**

Create `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs`:

```csharp
#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     Josephine's mirror: a full-sprite preview on the left, one row per appearance category on the right, a
///     running total underneath, and a single Apply. Reads everything from <see cref="WorldState.BeautyShop" />;
///     nothing here costs gold until the server answers an Apply.
/// </summary>
public sealed class BeautyShopControl : FramedDialogPanelBase
{
    private const int PANEL_WIDTH = 520;
    private const int PANEL_HEIGHT = 330;
    private const int TOP_MARGIN = 60;
    private const int TITLE_TOP = 12;
    private const int OK_RIGHT_MARGIN = 14;
    private const int OK_BOTTOM_MARGIN = 10;

    //preview column
    private const int PREVIEW_LEFT = 24;
    private const int PREVIEW_TOP = 36;
    private const int PREVIEW_WIDTH = 150;
    private const int PREVIEW_HEIGHT = 170;
    private const int ROTATE_BUTTON_WIDTH = 28;
    private const int ROTATE_ROW_TOP = PREVIEW_TOP + PREVIEW_HEIGHT + 6;

    //rows column (Task 11)
    internal const int ROWS_LEFT = PREVIEW_LEFT + PREVIEW_WIDTH + 24;
    internal const int ROWS_TOP = 40;
    internal const int ROW_HEIGHT = 30;
    internal const int ROWS_WIDTH = PANEL_WIDTH - ROWS_LEFT - 24;

    private readonly AislingRenderer Renderer;
    private readonly PreviewView Preview;
    private readonly CustomCheckBox GearToggle;
    private readonly UILabel TitleLabel;
    private int FacingIndex;

    /// <summary>The player closed the panel (button or Escape). Fires once per hide.</summary>
    public event Action? Closed;

    public BeautyShopControl(AislingRenderer renderer)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        Renderer = renderer;

        Name = "BeautyShop";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        OkButton = CreateCloseButton(RequestDismissal, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = new UILabel
        {
            X = 0,
            Y = TITLE_TOP,
            Width = PANEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "Josephine's Mirror"
        };
        AddChild(TitleLabel);

        Preview = new PreviewView(renderer)
        {
            X = PREVIEW_LEFT,
            Y = PREVIEW_TOP,
            Width = PREVIEW_WIDTH,
            Height = PREVIEW_HEIGHT
        };
        AddChild(Preview);

        var rotateLeft = new CustomButton("<", ROTATE_BUTTON_WIDTH) { X = PREVIEW_LEFT, Y = ROTATE_ROW_TOP };
        rotateLeft.Clicked += () => Rotate(-1);
        AddChild(rotateLeft);

        var rotateRight = new CustomButton(">", ROTATE_BUTTON_WIDTH)
        {
            X = PREVIEW_LEFT + ROTATE_BUTTON_WIDTH + 4,
            Y = ROTATE_ROW_TOP
        };
        rotateRight.Clicked += () => Rotate(+1);
        AddChild(rotateRight);

        GearToggle = new CustomCheckBox
        {
            X = PREVIEW_LEFT + (ROTATE_BUTTON_WIDTH * 2) + 16,
            Y = ROTATE_ROW_TOP + ((CustomButton.HEIGHT - CustomCheckBox.CHECKBOX_SIZE) / 2),
            Text = "Show gear",
            Checked = false
        };
        GearToggle.Clicked += () => RefreshPreview();
        AddChild(GearToggle);

        BuildRows();      //Task 11
        BuildFooter();    //Task 11
    }

    /// <summary>Repaints the preview and (Task 11) every row from the view model.</summary>
    public void Refresh()
    {
        RefreshPreview();
        RefreshRows();    //Task 11
    }

    public override void Show()
    {
        FacingIndex = 0;
        GearToggle.Checked = false;
        base.Show();
        Refresh();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();
        Preview.Release();
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

    private void Rotate(int delta)
    {
        FacingIndex = (FacingIndex + delta + PreviewView.FACING_COUNT) % PreviewView.FACING_COUNT;
        RefreshPreview();
    }

    private void RefreshPreview() => Preview.Refresh(BuildAppearance(), FacingIndex);

    /// <summary>
    ///     Bare body + hair + face by default so every change is visible; with gear on, the player's live world
    ///     appearance (armor, helmet, weapon...) with the five editable fields overridden.
    /// </summary>
    private AislingAppearance BuildAppearance()
    {
        var vm = WorldState.BeautyShop;

        var bare = new AislingAppearance
        {
            Gender = vm.Gender,
            BodySpriteId = AislingRenderer.BODY_ID,
            BodyColor = (int)vm.BodyColor,
            HeadSprite = vm.HairStyle,
            HeadColor = vm.HairColor,
            FaceSprite = vm.FaceSprite
        };

        if (!GearToggle.Checked)
            return bare;

        var live = WorldState.GetPlayerEntity()?.Appearance;

        if (live is null)
            return bare;

        return live.Value with
        {
            Gender = vm.Gender,
            BodyColor = (int)vm.BodyColor,
            HeadSprite = vm.HairStyle,
            HeadColor = vm.HairColor,
            FaceSprite = vm.FaceSprite
        };
    }

    public override void Dispose()
    {
        Preview.Dispose();
        base.Dispose();
    }

    /// <summary>
    ///     The sprite. Owns exactly one composited texture at a time -- <see cref="AislingRenderer.Render" />
    ///     allocates a fresh texture per call and caches only per world entity, so this view caches by
    ///     (appearance, facing) and disposes the previous texture on every re-render (see PokerTableControl's
    ///     PortraitView for the same reasoning).
    /// </summary>
    private sealed class PreviewView(AislingRenderer renderer) : UIElement
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

        private Texture2D? Figure;
        private AislingAppearance? RenderedAppearance;
        private int RenderedFacing = -1;

        public void Refresh(AislingAppearance appearance, int facingIndex)
        {
            if (Nullable.Equals(appearance, RenderedAppearance) && (facingIndex == RenderedFacing))
                return;

            RenderedAppearance = appearance;
            RenderedFacing = facingIndex;

            Figure?.Dispose();
            var (frame, flip, isFront) = FACINGS[facingIndex];
            Figure = renderer.Render(in appearance, frame, AislingRenderer.IDLE_ANIM, flip, isFront);
        }

        /// <summary>Drops the texture on hide so a closed panel holds no GPU memory; the next Show re-renders.</summary>
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

            if (!Visible || (Figure is null))
                return;

            //centre the body (not the padded canvas) in the box, feet a little above the bottom edge
            var x = ScreenX + (Width / 2) - AislingRenderer.CANVAS_CENTER_X;
            var y = ScreenY + Height - AislingRenderer.COMPOSITE_HEIGHT - 12;

            DrawTexture(spriteBatch, Figure, new Vector2(x, y), Color.White);
        }

        public override void Dispose()
        {
            Release();
            base.Dispose();
        }
    }

    //── Task 11 fills these in ──
    private void BuildRows() { }
    private void BuildFooter() { }
    private void RefreshRows() { }
}
```

`UIElement` may not have a primary-constructor-friendly base (it has a parameterless ctor) — if `PreviewView(AislingRenderer renderer) : UIElement` does not compile, store the renderer in a field via an explicit constructor exactly like `PortraitView` does at `PokerTableControl.cs:2576`.

- [ ] **Step 3: Build**

Run: `cd /c/Users/mikeb/Documents/GitHub/Chaos.Client && dotnet build Chaos.Client/Chaos.Client.csproj`
Expected: Build succeeded (warnings about unused private methods are fine; they are filled in next task).

- [ ] **Step 4: Commit**

```bash
git add Chaos.Client.Rendering/AislingRenderer.cs Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs
git commit -m "BeautyShop: framed panel with cached sprite preview, facings and gear toggle"
```

```json:metadata
{"files": ["Chaos.Client.Rendering/AislingRenderer.cs", "Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj", "acceptanceCriteria": ["FramedDialogPanelBase skeleton with control stack", "PreviewView caches one texture keyed by (appearance, facing)", "four facings, Down default", "gear toggle overrides the live appearance", "close/Escape raise Closed", "client builds"], "modelTier": "standard"}
```

---

### Task 11: BeautyShopControl — option rows, total, Reset/Apply, gender confirm, rejection

**Goal:** The right-hand column and footer: five rows with arrows/prices/changed markers, a total that turns red when unaffordable, Reset and Apply, a gear-reshape confirmation for gender changes, and rejection text.

**Files:**
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs`

**Acceptance Criteria:**
- [ ] Rows in order: Gender (two `CustomButton`s "Male"/"Female", the selected one disabled), Hairstyle (◀ number ▶), Hair dye (◀ name ▶ + 14×14 swatch), Skin (◀ name ▶), Face (◀ name ▶). Each row shows its price at the right and a `*` marker when changed.
- [ ] Hair swatch colour comes from `DataContext.AislingDrawData.DyeColorTable[(int)color].Colors[2]` (Default → `LegendColors.DeepLavender`; a missing table entry → `LegendColors.Gray`).
- [ ] Footer: `Total: N gold` (red when `!CanAfford`, white otherwise), `You have: G`, a status line, and `Reset` / `Apply` buttons. `Apply.Enabled == vm.CanApply`.
- [ ] Apply with `GenderChanged` shows an `OkPopupMessageControl(true)` parented to the panel: "This will reshape your Master and Grandmaster gear — equipped, banked and in inventory — for {GenderPrice:N0} gold. Continue?"; OK raises `ApplyRequested`, Cancel just hides it. Without a gender change Apply raises `ApplyRequested` directly.
- [ ] `OnRejected(reason)` sets the status line to the mapped text and leaves the panel open; it is ignored while hidden.
- [ ] Every row/footer repaints from `WorldState.BeautyShop` in `RefreshRows()`; the preview repaints after every step.
- [ ] `dotnet build Chaos.Client/Chaos.Client.csproj` succeeds.

**Verify:** `cd Chaos.Client && dotnet build Chaos.Client/Chaos.Client.csproj` → Build succeeded

**Steps:**

- [ ] **Step 1: Fields, events and the confirm dialog**

Add to the class (beside the existing fields):

```csharp
    //rows
    private const int ARROW_WIDTH = 26;
    private const int LABEL_WIDTH = 74;
    private const int VALUE_WIDTH = 118;
    private const int PRICE_WIDTH = 70;
    private const int SWATCH_SIZE = 14;
    private const int GENDER_BUTTON_WIDTH = 64;

    //footer
    private const int FOOTER_TOP = ROWS_TOP + (ROW_HEIGHT * 5) + 12;
    private const int FOOTER_BUTTON_WIDTH = 70;

    private CustomButton MaleButton = null!;
    private CustomButton FemaleButton = null!;
    private UILabel GenderPriceLabel = null!;
    private OptionRow HairstyleRow = null!;
    private OptionRow HairColorRow = null!;
    private OptionRow BodyColorRow = null!;
    private OptionRow FaceRow = null!;
    private UIPanel HairSwatch = null!;
    private UILabel TotalLabel = null!;
    private UILabel GoldLabel = null!;
    private UILabel StatusLabel = null!;
    private CustomButton ResetButton = null!;
    private CustomButton ApplyButton = null!;
    private OkPopupMessageControl ConfirmDialog = null!;
    private Texture2D? SwatchTexture;

    /// <summary>The player pressed Apply (and confirmed, when a gender change was involved).</summary>
    public event Action? ApplyRequested;
```

- [ ] **Step 2: `OptionRow` nested class**

```csharp
    /// <summary>One "◀ value ▶  price *" line. The row owns no state; the control feeds it text on every refresh.</summary>
    private sealed class OptionRow : UIPanel
    {
        public readonly CustomButton Left;
        public readonly CustomButton Right;
        public readonly UILabel Caption;
        public readonly UILabel Value;
        public readonly UILabel Price;

        public OptionRow(string caption, Action<int> step, int width)
        {
            Width = width;
            Height = ROW_HEIGHT;
            Background = null;

            Caption = new UILabel
            {
                X = 0,
                Y = (ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
                Width = LABEL_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false,
                Text = caption
            };
            AddChild(Caption);

            Left = new CustomButton("<", ARROW_WIDTH) { X = LABEL_WIDTH, Y = (ROW_HEIGHT - CustomButton.HEIGHT) / 2 };
            Left.Clicked += () => step(-1);
            AddChild(Left);

            Value = new UILabel
            {
                X = LABEL_WIDTH + ARROW_WIDTH + 4,
                Y = Caption.Y,
                Width = VALUE_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Center,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false
            };
            AddChild(Value);

            Right = new CustomButton(">", ARROW_WIDTH) { X = Value.X + VALUE_WIDTH + 4, Y = Left.Y };
            Right.Clicked += () => step(+1);
            AddChild(Right);

            Price = new UILabel
            {
                X = width - PRICE_WIDTH,
                Y = Caption.Y,
                Width = PRICE_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Right,
                ForegroundColor = LegendColors.Gold,
                IsHitTestVisible = false
            };
            AddChild(Price);
        }

        public void Set(string value, int price, bool changed)
        {
            Value.Text = value;
            Price.Text = changed ? $"+{price:N0} *" : $"{price:N0}";
            Price.ForegroundColor = changed ? LegendColors.Gold : LegendColors.Gray;
        }
    }
```

- [ ] **Step 3: `BuildRows`**

Replace the empty `BuildRows` with:

```csharp
    private void BuildRows()
    {
        var vm = WorldState.BeautyShop;
        var y = ROWS_TOP;

        //── gender: two buttons, the selected one greyed ──
        var genderCaption = new UILabel
        {
            X = ROWS_LEFT,
            Y = y + ((ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            Width = LABEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "Gender"
        };
        AddChild(genderCaption);

        MaleButton = new CustomButton("Male", GENDER_BUTTON_WIDTH) { X = ROWS_LEFT + LABEL_WIDTH, Y = y + ((ROW_HEIGHT - CustomButton.HEIGHT) / 2) };
        MaleButton.Clicked += () => Select(v => v.SetGender(Gender.Male));
        AddChild(MaleButton);

        FemaleButton = new CustomButton("Female", GENDER_BUTTON_WIDTH) { X = MaleButton.X + GENDER_BUTTON_WIDTH + 6, Y = MaleButton.Y };
        FemaleButton.Clicked += () => Select(v => v.SetGender(Gender.Female));
        AddChild(FemaleButton);

        GenderPriceLabel = new UILabel
        {
            X = ROWS_LEFT + ROWS_WIDTH - PRICE_WIDTH,
            Y = genderCaption.Y,
            Width = PRICE_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };
        AddChild(GenderPriceLabel);
        y += ROW_HEIGHT;

        HairstyleRow = AddRow("Hairstyle", d => vm.StepHairstyle(d), ref y);
        HairColorRow = AddRow("Hair dye", d => vm.StepHairColor(d), ref y);

        HairSwatch = new UIPanel
        {
            X = HairColorRow.X + HairColorRow.Value.X - SWATCH_SIZE - 4,
            Y = HairColorRow.Y + ((ROW_HEIGHT - SWATCH_SIZE) / 2),
            Width = SWATCH_SIZE,
            Height = SWATCH_SIZE,
            IsHitTestVisible = false,
            ZIndex = 1
        };
        AddChild(HairSwatch);

        BodyColorRow = AddRow("Skin", d => vm.StepBodyColor(d), ref y);
        FaceRow = AddRow("Face", d => vm.StepFace(d), ref y);
    }

    private OptionRow AddRow(string caption, Action<int> step, ref int y)
    {
        var row = new OptionRow(caption, delta => Select(_ => step(delta)), ROWS_WIDTH) { X = ROWS_LEFT, Y = y };
        AddChild(row);
        y += ROW_HEIGHT;

        return row;
    }

    /// <summary>Every selection change goes through here: mutate the view model, then repaint everything.</summary>
    private void Select(Action<ViewModel.BeautyShop> mutate)
    {
        mutate(WorldState.BeautyShop);
        StatusLabel.Text = string.Empty;
        Refresh();
    }
```

- [ ] **Step 4: `BuildFooter`**

```csharp
    private void BuildFooter()
    {
        TotalLabel = FooterLabel(ROWS_LEFT, FOOTER_TOP, ROWS_WIDTH / 2);
        GoldLabel = FooterLabel(ROWS_LEFT + (ROWS_WIDTH / 2), FOOTER_TOP, ROWS_WIDTH / 2);
        GoldLabel.HorizontalAlignment = HorizontalAlignment.Right;
        StatusLabel = FooterLabel(ROWS_LEFT, FOOTER_TOP + TextRenderer.CHAR_HEIGHT + 4, ROWS_WIDTH);
        StatusLabel.ForegroundColor = LegendColors.Red;

        var buttonsTop = PANEL_HEIGHT - OK_BOTTOM_MARGIN - CustomButton.HEIGHT - 4;

        ResetButton = new CustomButton("Reset", FOOTER_BUTTON_WIDTH) { X = ROWS_LEFT, Y = buttonsTop };
        ResetButton.Clicked += () => Select(v => v.Reset());
        AddChild(ResetButton);

        ApplyButton = new CustomButton("Apply", FOOTER_BUTTON_WIDTH) { X = ROWS_LEFT + FOOTER_BUTTON_WIDTH + 8, Y = buttonsTop, Enabled = false };
        ApplyButton.Clicked += OnApplyClicked;
        AddChild(ApplyButton);

        //parented to the panel like poker's leave confirm, drawn above everything else in it
        ConfirmDialog = new OkPopupMessageControl(true) { Name = "BeautyShopGenderConfirm", ZIndex = 100 };
        ConfirmDialog.X = (PANEL_WIDTH - ConfirmDialog.Width) / 2;
        ConfirmDialog.Y = (PANEL_HEIGHT - ConfirmDialog.Height) / 2;

        ConfirmDialog.OnOk += () =>
        {
            ConfirmDialog.Hide();
            ApplyRequested?.Invoke();
        };

        ConfirmDialog.OnCancel += () => ConfirmDialog.Hide();
        AddChild(ConfirmDialog);
    }

    private UILabel FooterLabel(int x, int y, int width)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(label);

        return label;
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

    /// <summary>Server refused the Apply; keep the panel open and say why.</summary>
    public void OnRejected(BeautyShopRejectReason reason)
    {
        if (!Visible)
            return;

        StatusLabel.Text = reason switch
        {
            BeautyShopRejectReason.NothingChanged => "Nothing has changed.",
            BeautyShopRejectReason.InsufficientGold => "You can't afford that.",
            BeautyShopRejectReason.InvalidSelection => "Josephine can't do that one.",
            BeautyShopRejectReason.GenderSwapUnavailable => "Josephine can't reshape your class's gear.",
            BeautyShopRejectReason.NotNearShop => "Step closer to Josephine.",
            _ => "Josephine shakes her head."
        };
    }
```

- [ ] **Step 5: `RefreshRows`**

```csharp
    private void RefreshRows()
    {
        var vm = WorldState.BeautyShop;

        MaleButton.Enabled = vm.Gender != Gender.Male;
        FemaleButton.Enabled = vm.Gender != Gender.Female;
        GenderPriceLabel.Text = vm.GenderChanged ? $"+{vm.GenderPrice:N0} *" : $"{vm.GenderPrice:N0}";
        GenderPriceLabel.ForegroundColor = vm.GenderChanged ? LegendColors.Gold : LegendColors.Gray;

        HairstyleRow.Set($"Style {vm.HairStyle}", vm.HairstylePrice, vm.HairstyleChanged);
        HairColorRow.Set(vm.HairColor.ToString(), vm.HairDyePrice, vm.HairColorChanged);
        BodyColorRow.Set(vm.BodyColor.ToString(), vm.BodyDyePrice, vm.BodyColorChanged);

        var face = vm.Faces.FirstOrDefault(f => f.Sprite == vm.FaceSprite);
        FaceRow.Set(face?.Name ?? $"Face {vm.FaceSprite}", vm.FacePrice, vm.FaceChanged);

        RefreshSwatch(vm.HairColor);

        TotalLabel.Text = $"Total: {vm.Total:N0} gold";
        TotalLabel.ForegroundColor = vm.CanAfford ? LegendColors.White : LegendColors.Red;
        GoldLabel.Text = $"You have: {vm.Gold:N0}";
        ApplyButton.Enabled = vm.CanApply;
    }

    /// <summary>A flat 14x14 tile of the dye's mid-tone, rebuilt only when the colour changes.</summary>
    private void RefreshSwatch(DisplayColor color)
    {
        var rgb = SwatchColorFor(color);

        if (SwatchTexture is not null && (SwatchTexture.Tag is DisplayColor tagged) && (tagged == color))
            return;

        SwatchTexture?.Dispose();
        var device = UiRenderer.Instance!.GraphicsDevice;
        SwatchTexture = new Texture2D(device, SWATCH_SIZE, SWATCH_SIZE) { Tag = color };
        SwatchTexture.SetData(Enumerable.Repeat(rgb, SWATCH_SIZE * SWATCH_SIZE).ToArray());
        HairSwatch.Background = SwatchTexture;
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
```

Add `using Chaos.Client.Data;` for `DataContext`. If `UiRenderer.Instance.GraphicsDevice` is not exposed, use whatever `PokerTableControl.BuildRecessedPanel` (`PokerTableControl.cs:917`) uses to obtain the device. Dispose `SwatchTexture` in `Dispose()`.

- [ ] **Step 6: Build and commit**

Run: `cd /c/Users/mikeb/Documents/GitHub/Chaos.Client && dotnet build Chaos.Client/Chaos.Client.csproj`
Expected: Build succeeded, no warnings about unused members.

```bash
git add Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs
git commit -m "BeautyShop: option rows, running total, reset/apply, gender confirm and rejection text"
```

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj", "acceptanceCriteria": ["five rows with arrows/prices/changed markers", "hair swatch from DyeColorTable", "total red when unaffordable, Apply enabled == CanApply", "gender change confirms before ApplyRequested", "OnRejected sets status and keeps panel open", "client builds"], "modelTier": "standard"}
```

---

### Task 12: WorldScreen wiring and packet dispatch

**Goal:** The panel is constructed, mounted, wired to the connection, and driven by the server's display packets exactly like the poker panel.

**Files:**
- Modify: `Chaos.Client/Screens/WorldScreen.cs` (field after `Poker` ≈ line 171; construction after `WirePoker();` ≈ line 774; `Root.AddChild` after `Root.AddChild(Poker);` ≈ line 847; unsubscribe after `OnPokerTableDisplay -=` ≈ line 972)
- Modify: `Chaos.Client/Screens/WorldScreen.Wiring.cs` (new region after `#endregion` of Poker Wiring ≈ line 237)
- Modify: `Chaos.Client/Screens/WorldScreen.ServerHandlers.cs` (new handler after `HandlePokerTableDisplay` ≈ line 1557)

**Acceptance Criteria:**
- [ ] `Open` → `WorldState.BeautyShop.ApplyOpen(args)` then `BeautyShop.Show()`; `Rejected` → `BeautyShop.OnRejected(args.Reason)`; `Close` → `BeautyShop.Hide()` then `WorldState.BeautyShop.Clear()`.
- [ ] `ApplyRequested` → `SendBeautyShopApply(vm.Gender, (ushort)vm.HairStyle, vm.HairColor, vm.BodyColor, (byte)vm.FaceSprite)`; `Closed` → `SendBeautyShopClose()`.
- [ ] The event is unsubscribed in the screen's teardown.
- [ ] `dotnet build Chaos.Client.slnx` succeeds.

**Verify:** `cd Chaos.Client && dotnet build Chaos.Client.slnx` → Build succeeded

**Steps:**

- [ ] **Step 1: WorldScreen.cs**

Field:

```csharp
    //beauty shop mirror — opened by the server's BeautyShop Open display from Josephine's dialog
    private BeautyShopControl BeautyShop = null!;
```

Construction (after `WirePoker();`):

```csharp
        BeautyShop = new BeautyShopControl(Game.AislingRenderer)
        {
            ZIndex = 2
        };
        WireBeautyShop();
```

Mount: `Root.AddChild(BeautyShop);` after `Root.AddChild(Poker);`. Teardown: `Game.Connection.OnBeautyShopDisplay -= HandleBeautyShopDisplay;` after the poker unsubscribe. Add `using Chaos.Client.Controls.World.Popups.Beauty;`.

- [ ] **Step 2: WorldScreen.Wiring.cs**

```csharp
    #region Beauty Shop Wiring
    private void WireBeautyShop()
    {
        Game.Connection.OnBeautyShopDisplay += HandleBeautyShopDisplay;

        BeautyShop.ApplyRequested += () =>
        {
            var vm = WorldState.BeautyShop;

            Game.Connection.SendBeautyShopApply(
                vm.Gender,
                (ushort)vm.HairStyle,
                vm.HairColor,
                vm.BodyColor,
                (byte)vm.FaceSprite);
        };

        //nothing is escrowed, so Close is informational; the server holds no session for the mirror
        BeautyShop.Closed += () => Game.Connection.SendBeautyShopClose();
    }
    #endregion
```

- [ ] **Step 3: WorldScreen.ServerHandlers.cs**

```csharp
    /// <summary>
    ///     Dispatches a beauty shop display packet: apply to <see cref="WorldState.BeautyShop" /> first, then tell
    ///     <see cref="BeautyShop" /> to repaint from it -- one copy of the truth, same as poker.
    /// </summary>
    private void HandleBeautyShopDisplay(BeautyShopDisplayArgs args)
    {
        switch (args.Type)
        {
            case BeautyShopDisplayType.Open:
                WorldState.BeautyShop.ApplyOpen(args);
                BeautyShop.Show();

                break;

            case BeautyShopDisplayType.Rejected:
                BeautyShop.OnRejected(args.Reason);

                break;

            case BeautyShopDisplayType.Close:
                BeautyShop.Hide();
                WorldState.BeautyShop.Clear();

                break;
        }
    }
```

- [ ] **Step 4: Build the whole solution and commit**

Stop any running server/client first.

```bash
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client
dotnet build Chaos.Client.slnx
git add Chaos.Client/Screens
git commit -m "BeautyShop: wire the mirror panel into WorldScreen"
```

```json:metadata
{"files": ["Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/Screens/WorldScreen.Wiring.cs", "Chaos.Client/Screens/WorldScreen.ServerHandlers.cs"], "verifyCommand": "dotnet build Chaos.Client.slnx", "acceptanceCriteria": ["Open/Rejected/Close dispatch to view model then control", "ApplyRequested/Closed send packets", "unsubscribed on teardown", "solution builds"], "modelTier": "mechanical"}
```

---

### Task 13: Walkthrough doc, live check, branch integration

**Goal:** A written manual QA script, one real run through it against a local server, and the three feature branches ready to merge with the client's submodule pointer bumped.

**Files:**
- Create: `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md` (Chaos.Client)
- Modify: `Chaos.Client` submodule pointer (`Chaos-Server`) after the server branch is merged

**Acceptance Criteria:**
- [ ] The walkthrough lists every check below with a pass/fail column, and the run's results are recorded in it.
- [ ] Clicking Josephine shows the greeting with one option; choosing it opens the mirror with the player's current look and the correct gold.
- [ ] Stepping hairstyle/dye/skin/face changes the preview immediately; the total matches the sum of changed rows; rotating shows four facings; "Show gear" layers the equipped armor/weapon on.
- [ ] Apply with a hairstyle + dye change closes the panel, shows the orange-bar message, and the world sprite matches the preview; gold dropped by exactly the total.
- [ ] Apply with insufficient gold keeps the panel open with "You can't afford that." and changes nothing.
- [ ] Gender change shows the confirm; OK reshapes gear and the sprite; Cancel leaves the panel open.
- [ ] Escape / Close discards; reopening shows the current (unchanged) look.
- [ ] A fresh character on the Riona tutorial still receives the 1000g/1000exp/5 GP reward from Josephine's greeting.
- [ ] Server log contains one `Beauty Shop` line per changed category plus the total line.
- [ ] All three branches are committed; `Chaos.Client` `Chaos-Server` pointer references the merged server commit.

**Verify:** the walkthrough file has every row marked PASS, and `git -C Chaos.Client status --short` shows no unexpected changes.

**Steps:**

- [ ] **Step 1: Write the walkthrough**

Create `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md` with a table of the ten checks in the acceptance criteria (columns: #, Check, Expected, Result, Notes) and a "Setup" section reproducing the boot recipe below.

- [ ] **Step 2: Boot server and client**

Server (Debug — Release reads the wrong `StagingDirectory`): confirm `Chaos/appsettings.local.json` `StagingDirectory` points at `C:\Users\mikeb\Documents\GitHub\Unora` (a local, uncommitted edit), then:

```bash
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client/Chaos-Server/Chaos && dotnet run -c Debug
```

Wait for `WorldServer: Listening` and the `Beauty shop catalog loaded: 101 male styles, 102 female styles, 18 faces` line. Then launch the client per the `unora-client-run-config` memory (host/port/DA_PATH env vars), log in, and walk to the Mileth Beauty Shop (Josephine at (5,6)).

- [ ] **Step 3: Run every check, recording results in the walkthrough**

Use `/Unora/Data/Saved/<name>` (after logout) or the orange-bar gold to confirm the deduction. Grep the server log for `Beauty Shop` to confirm logging. Fix anything that fails in the task that owns it, commit, and re-run.

- [ ] **Step 4: Clean up the data checkout and commit the walkthrough**

```bash
cd /c/Users/mikeb/Documents/GitHub/Unora && git status --short   # Data/Saved and Data/Backups are test-play noise; stash or discard, do NOT commit
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client
git add -f docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md
git commit -m "BeautyShop: manual walkthrough with results"
```

- [ ] **Step 5: Integrate**

Follow `superpowers-extended-cc:finishing-a-development-branch`. Merge order: server (`feature/beauty-shop` → `master`), then Unora (`→ main`), then bump the client's submodule pointer to the merged server commit and merge the client branch (`→ main`). Do not push unless the user asks.

```json:metadata
{"files": ["docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md"], "verifyCommand": "grep -c PASS docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md", "acceptanceCriteria": ["walkthrough written with results", "greeting → mirror with current look", "preview/total/facings/gear toggle", "apply charges and closes", "insufficient gold rejected", "gender confirm", "escape discards", "Riona reward intact", "per-category logs", "branches merged, submodule bumped"], "modelTier": "standard"}
```
