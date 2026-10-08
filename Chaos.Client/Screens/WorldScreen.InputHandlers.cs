#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Hud;
using Chaos.Client.Controls.World.Hud.Panel;
using Chaos.Client.Controls.World.Hud.Panel.Slots;
using Chaos.Client.Controls.World.Popups.Market;
using Chaos.Client.Data;
using Chaos.Client.Data.Models;
using Chaos.Client.Extensions;
using Chaos.Client.Models;
using Chaos.Client.Systems;
using Chaos.Client.Systems.KeyBinds;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Pathfinder = Chaos.Client.Systems.Pathfinder;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    #region UI Event Handlers
    //--- inventory ---

    private void HandleInventorySlotClicked(byte slot)
    {
        Game.Connection.UseItem(slot);
        HoveredInventorySlot = null;
        ItemTooltip.Hide();
    }

    private void HandleInventoryHoverEnter(PanelSlot slot)
    {
        HoveredInventorySlot = slot;

        ItemTooltip.Show(
            slot.SlotName ?? string.Empty,
            slot.CurrentDurability,
            slot.MaxDurability,
            InputBuffer.MouseX,
            InputBuffer.MouseY);
    }

    private void HandleInventoryHoverExit()
    {
        HoveredInventorySlot = null;
        ItemTooltip.Hide();
    }

    /// <summary>
    ///     Returns true if the point is over any visible popup window, preventing drops from passing through.
    /// </summary>
    private bool IsOverVisiblePopup(int screenX, int screenY)
    {
        if (Root is null)
            return false;

        foreach (var child in Root.Children)
        {
            if (child is not UIPanel { Visible: true, IsPassThrough: false } panel)
                continue;

            if ((panel == SmallHud) || (panel == LargeHud))
                continue;

            if (panel.ContainsPoint(screenX, screenY))
                return true;
        }

        return false;
    }

    private void HandleInventoryDropInViewport(byte slot, int mouseX, int mouseY)
    {
        //registered popup/HUD drop targets get first refusal; each owns its eligibility + drop-zone test, and
        //WorldScreen owns the paired networking action. Drop coords are stored before the call so targets that
        //defer their action (e.g. Market's stackable ItemAmount popup) can still access the original drop point.
        foreach (var (target, onDrop) in InventoryDropTargets)
            if (target.AcceptsInventoryDrop(slot, mouseX, mouseY))
            {
                PendingMarketDropX = mouseX;
                PendingMarketDropY = mouseY;
                onDrop(slot);

                return;
            }

        //block drops that land on any visible popup window
        if (IsOverVisiblePopup(mouseX, mouseY))
            return;

        //only drop if released within the world viewport
        if (!IsInWorldViewport(mouseX, mouseY))
            return;

        if (MapFile is null)
            return;

        //check if dropped on an entity (give item/gold to npc/player) — skip self (drop on ground instead)
        (var tileX, var tileY) = ScreenToTile(mouseX, mouseY);
        var entity = GetEntityAtScreen(mouseX, mouseY);

        var droppedOnEntity = entity?.Type is ClientEntityType.Aisling or ClientEntityType.Creature
                              && (entity.Id != Game.Connection.AislingId);

        //gold bag (slot 0) — show the gold amount popup
        if (slot == 0)
        {
            GoldDrop.CenterVerticallyIn(WorldHud.ViewportBounds);

            GoldDrop.ShowFor(
                GoldAmountPurpose.Drop,
                droppedOnEntity ? entity!.Id : null,
                tileX,
                tileY);
            WorldHud.SetDescription($"Gold( {WorldState.Inventory.Gold} )");

            return;
        }

        if (droppedOnEntity)
        {
            var itemSlot = WorldState.Inventory.GetSlot(slot);
            Game.Connection.DropItemOnCreature(slot, entity!.Id, itemSlot.Stackable ? (byte)0 : (byte)1);

            return;
        }

        //stackable items with more than one — prompt for count before dropping
        var invSlot = WorldState.Inventory.GetSlot(slot);

        if (invSlot is { Stackable: true, Count: > 1 })
        {
            if (IsWholeStackHeld())
            {
                Game.Connection.DropItem(slot, tileX, tileY, (int)invSlot.Count);

                return;
            }

            var capturedSlot = slot;
            var capturedX = tileX;
            var capturedY = tileY;

            WorldHud.ChatInput.ShowPrompt(
                $"Number of items to drop [ 0 - {(int)invSlot.Count} ]: ",
                12,
                text =>
                {
                    if (int.TryParse(text, out var count) && (count > 0))
                        Game.Connection.DropItem(capturedSlot, capturedX, capturedY, count);
                });

            return;
        }

        Game.Connection.DropItem(slot, tileX, tileY);
    }

    //shift held on a stack drop moves the whole stack without asking how many — for the ground, the bank, the market and
    //a trade. read at drop time, because the modifier is what the player is holding as they let go
    private static bool IsWholeStackHeld() => (InputBuffer.CurrentModifiers & KeyModifiers.Shift) != 0;

    //add an inventory item dragged into the trade window. the server answers a stack with "how many?", so a Shift drop
    //is remembered by slot and answered with the whole stack in HandleExchangeAmountRequested
    private void BeginExchangeAdd(byte slot)
    {
        ExchangeWholeStackSlot = IsWholeStackHeld() ? slot : null;
        Game.Connection.SendExchangeInteraction(ExchangeRequestType.AddItem, Exchange.OtherUserId, slot);
    }

    //list an inventory item on the Market Sell tab: if the drop is on a matching row route to AddToListing; otherwise
    //create a draft (non-stackable immediately, stackable via the shared ItemAmountControl).
    //The cap check for new drafts is inside MarketSellControl.AddDraftListing so it does not block add-to-existing.
    //PendingMarketDropX/PendingMarketDropY are written by HandleInventoryDropInViewport before this call, and are
    //forwarded to the deferred ItemAmount confirm so Market.DropSellItem can still resolve the original row coords.
    private void BeginMarketListing(byte slot)
    {
        ref readonly var data = ref WorldState.Inventory.GetSlot(slot);

        if (!data.IsOccupied)
            return;

        if (data is { Stackable: true, Count: > 1 })
        {
            if (IsWholeStackHeld())
            {
                Market.DropSellItem(slot, (int)data.Count, PendingMarketDropX, PendingMarketDropY);

                return;
            }

            ItemAmount.X = Market.X + (Market.Width - ItemAmount.Width) / 2;
            ItemAmount.Y = Market.Y + (Market.Height - ItemAmount.Height) / 2;
            ItemAmount.ShowFor(ItemAmountPurpose.MarketListing, slot, data.Count);
        } else
            Market.DropSellItem(slot, 1, PendingMarketDropX, PendingMarketDropY);
    }

    //deposit an inventory item (or the gold bag) dragged into the bank window. every deposit is a mutation, so it is
    //followed by a refresh — the server pushes nothing back on success.
    private void BeginBankDeposit(byte slot)
    {
        //the HUD grids stay draggable while a prompt holds focus, so a second drop can land here mid-prompt. The amount
        //controls are shared singletons — re-purposing one would wipe the amount already typed into it. Swallow the drop.
        if (IsBankGoldPromptOpen || IsBankItemPromptOpen)
            return;

        //the bank is the one drop target that accepts slot 0: the gold bag deposits gold, not an item.
        if (slot == 0)
        {
            CenterOnBank(GoldDrop);
            GoldDrop.ShowFor(GoldAmountPurpose.BankDeposit);
            WorldHud.SetDescription($"Gold( {WorldState.Inventory.Gold} )");

            return;
        }

        ref readonly var data = ref WorldState.Inventory.GetSlot(slot);

        if (!data.IsOccupied)
            return;

        if (data is { Stackable: true, Count: > 1 })
        {
            if (IsWholeStackHeld())
            {
                Game.Connection.SendBankDepositItem(slot, (int)data.Count);
                RefreshBank();

                return;
            }

            CenterOnBank(ItemAmount);
            ItemAmount.ShowFor(ItemAmountPurpose.BankDeposit, slot, data.Count);

            //the prompt is a bare text box, so it is the only place the player can see what they have to spend
            WorldHud.SetDescription($"{data.Name} ( {data.Count} )");

            return;
        }

        Game.Connection.SendBankDepositItem(slot, 1);
        RefreshBank();
    }

    //take a banked item out by gesture (dragged onto the inventory, or double-clicked). A stack asks how many; a lone
    //item is unambiguous and just comes out.
    private void BeginBankWithdraw(string itemName, int count)
    {
        if (count > 1)
        {
            if (IsWholeStackHeld())
            {
                WithdrawBankItem(itemName, count);

                return;
            }

            CenterOnBank(ItemAmount);
            ItemAmount.ShowFor(ItemAmountPurpose.BankWithdraw, itemName, (uint)count);
            WorldHud.SetDescription($"{itemName} ( {count} )");

            return;
        }

        WithdrawBankItem(itemName, 1);
    }

    private void PromptBankGoldWithdraw()
    {
        CenterOnBank(GoldDrop);
        GoldDrop.ShowFor(GoldAmountPurpose.BankWithdraw);
    }

    //every mutation is followed by a self-issued refresh — the server pushes nothing back
    private void WithdrawBankItem(string itemName, int amount)
    {
        Game.Connection.SendBankWithdrawItem(itemName, amount);
        RefreshBank();
    }

    private void CenterOnBank(UIElement popup)
    {
        popup.X = Bank.X + (Bank.Width - popup.Width) / 2;
        popup.Y = Bank.Y + (Bank.Height - popup.Height) / 2;
    }

    //Purpose stays set after a confirm (the control hides, then reads it), so "acting on the bank" is Visible AND a bank
    //purpose — a bare Purpose test also matches a prompt that already closed.
    private bool IsBankGoldPromptOpen
        => GoldDrop is { Visible: true, Purpose: GoldAmountPurpose.BankDeposit or GoldAmountPurpose.BankWithdraw };

    private bool IsBankItemPromptOpen
        => ItemAmount is { Visible: true, Purpose: ItemAmountPurpose.BankDeposit or ItemAmountPurpose.BankWithdraw };

    /// <summary>
    ///     Closes any prompt acting on the bank. A bank prompt cannot outlive the bank window it was opened against:
    ///     <see cref="BankControl.Hide" /> clears <c>BankState</c>, so a
    ///     prompt confirmed afterwards would send the withdraw to the wrong bank.
    /// </summary>
    private void HideBankPrompts()
    {
        //Hide() on an already-hidden panel still pops the control stack, handing focus to whatever popup sits below.
        if (IsBankGoldPromptOpen)
            GoldDrop.Hide();

        if (IsBankItemPromptOpen)
            ItemAmount.Hide();
    }

    //the gold prompt parses a uint; the bank protocol takes an int.
    private static int ToGoldAmount(uint amount) => (int)Math.Min(amount, int.MaxValue);

    //--- skills / spells ---

    private void HandleSkillSlotClicked(byte slot)
    {
        var skillSlot = WorldHud.SkillBook.GetSkillSlot(slot)
                        ?? WorldHud.SkillBookAlt.GetSkillSlot(slot)
                        ?? WorldHud.Tools.WorldSkills.GetSkillSlot(slot);

        //gate on the authoritative book, not skillSlot.CooldownPercent: the control's copy only refreshes while its panel is
        //visibly updating, so a macro firing from a hidden pane would read a stale (frozen-on-cooldown or stale-ready) value
        if (WorldState.SkillBook.GetCooldownPercent(slot) > 0)
            return;

        //send chant line if one is set for this skill
        if (skillSlot is not null && !string.IsNullOrEmpty(skillSlot.Chant))
            Game.Connection.SendChant(skillSlot.Chant);

        Game.Connection.UseSkill(slot);
    }

    /// <summary>
    ///     Resolves the <see cref="SpellSlot" /> a 1-based slot number refers to, preferring the panel that is currently
    ///     shown. Shared by the click and drag-drop paths so both agree on which spell they are acting on.
    /// </summary>
    private SpellSlot? ResolveSpellSlot(byte slot)
        => WorldHud.ActiveTab switch
        {
            HudTab.Spells    => WorldHud.SpellBook.GetSpellSlot(slot),
            HudTab.SpellsAlt => WorldHud.SpellBookAlt.GetSpellSlot(slot),
            HudTab.Tools     => WorldHud.Tools.WorldSpells.GetSpellSlot(slot),
            _ => WorldHud.SpellBook.GetSpellSlot(slot)
                 ?? WorldHud.SpellBookAlt.GetSpellSlot(slot)
                 ?? WorldHud.Tools.WorldSpells.GetSpellSlot(slot)
        };

    private void HandleSpellSlotClicked(byte slot)
    {
        var spellSlot = ResolveSpellSlot(slot);

        if (spellSlot is not null)
            TryBeginCast(spellSlot);
    }

    /// <summary>
    ///     Puts a spell into cast mode, and reports whether it is now the armed spell. False means it never armed: the
    ///     slot is empty or on cooldown, or the spell is NoTarget, which casts outright instead of waiting for a target.
    /// </summary>
    private bool TryBeginCast(SpellSlot spellSlot)
    {
        //gate on the authoritative book, not spellSlot.CooldownPercent — see HandleSkillSlotClicked
        if (string.IsNullOrEmpty(spellSlot.AbilityName) || (WorldState.SpellBook.GetCooldownPercent(spellSlot.Slot) > 0))
            return false;

        //notarget spells cast immediately (no cast mode)
        if (spellSlot.SpellType == SpellType.NoTarget)
        {
            if (spellSlot.CastLines == 0)
                Game.Connection.UseSpell(spellSlot.Slot);
            else
            {
                //notarget with lines: begin chant sequence targeting self
                CastingSystem.BeginTargeting(spellSlot);

                var player = WorldState.GetPlayerEntity();

                CastingSystem.SelectTarget(
                    Game.Connection.AislingId,
                    player?.TileX ?? 0,
                    player?.TileY ?? 0,
                    Game.Connection);
            }

            return false;
        }

        //enter cast mode — wait for target selection
        CastingSystem.BeginTargeting(spellSlot);
        BeginTabTargeting();

        return true;
    }

    /// <summary>
    ///     The map tile under a screen point, clamped to the map edges. Null when no map is loaded. Shared by the
    ///     ground-target click and drop paths so an off-map aim resolves to the same tile either way.
    /// </summary>
    private (int X, int Y)? ClampedTileAt(int mouseX, int mouseY)
    {
        if (MapFile is null)
            return null;

        (var tileX, var tileY) = ScreenToTile(mouseX, mouseY);

        return (Math.Clamp(tileX, 0, MapFile.Width - 1), Math.Clamp(tileY, 0, MapFile.Height - 1));
    }

    /// <summary>
    ///     The tile a ground-targeted spell aimed at a screen point lands on. A creature's sprite is drawn above and behind
    ///     the tile it stands on, so aiming at its body would otherwise pick the tile behind it — meaning a ground spell and
    ///     an entity spell aimed at the same creature land on different tiles. Hovering an entity therefore snaps the aim to
    ///     that entity's tile. Shift inverts the snap, so <see cref="ClientSettings.GroundTargetSnapToEntity" /> really only
    ///     decides which of the two behaviours Shift selects. Shift is the modifier because it already means "ignore entity
    ///     hitboxes, aim at the tile" for shift+doubleclick item pickup. Shared by the aim highlight, the cast click and the
    ///     icon drop so all three agree on the target tile.
    /// </summary>
    private (int X, int Y)? GroundTargetTileAt(int mouseX, int mouseY)
        => SnapTargetTileAt(mouseX, mouseY) ?? ClampedTileAt(mouseX, mouseY);

    /// <summary>
    ///     The tile of the entity a ground-target aim snaps to, or null when snapping is off for this aim (option XOR Shift)
    ///     or nothing snappable is hovered. Hidden entities are excluded — snapping onto a tile with nothing drawn on it
    ///     would look like the reticle jumping at random.
    /// </summary>
    private (int X, int Y)? SnapTargetTileAt(int mouseX, int mouseY)
    {
        var shiftHeld = (InputBuffer.CurrentModifiers & KeyModifiers.Shift) != 0;

        if (ClientSettings.GroundTargetSnapToEntity == shiftHeld)
            return null;

        if (GetEntityAtScreen(mouseX, mouseY) is not
            {
                IsHidden: false,
                Type: ClientEntityType.Aisling or ClientEntityType.Creature
            } entity)
            return null;

        return (entity.TileX, entity.TileY);
    }

    /// <summary>
    ///     True when a screen point lies inside the world viewport. Drag-drop events are NOT absorbed by the panel they
    ///     land on (only click-family events are), so a drop released over the HUD still bubbles to the root handler with
    ///     HUD coordinates — every world-drop handler has to reject those itself.
    /// </summary>
    private bool IsInWorldViewport(int mouseX, int mouseY) => WorldHud.ViewportBounds.Contains(mouseX, mouseY);

    private void HandleSpellSlotDropped(byte slot, int mouseX, int mouseY)
    {
        if (IsOverVisiblePopup(mouseX, mouseY))
            return;

        //releasing the drag back onto the HUD (the panel background, the gutter between slots, another panel's slot) is
        //the natural "never mind" gesture, and it lands here with HUD coordinates. Without this, the tile clamp below
        //would turn a nowhere-drop into a valid edge tile and cast the spell there.
        if (!IsInWorldViewport(mouseX, mouseY))
            return;

        //the drag payload still holds the exact slot that was picked up, so there is no need to guess the panel back
        //from the active tab
        var spellSlot = (Game.Dispatcher.ActiveDragPayload as SlotDragPayload)?.Source as SpellSlot ?? ResolveSpellSlot(slot);

        if (spellSlot is null)
            return;

        uint targetId;
        int tileX;
        int tileY;

        //a ground-targeted spell has no entity to land on — the drop tile IS the target (entity id 0). Everything else
        //still requires an entity under the cursor.
        if (spellSlot.SpellType == SpellType.GroundTargeted)
        {
            if (GroundTargetTileAt(mouseX, mouseY) is not { } tile)
                return;

            (targetId, tileX, tileY) = (0u, tile.X, tile.Y);
        } else
        {
            var entity = GetEntityAtScreen(mouseX, mouseY);

            if (entity?.Type is not (ClientEntityType.Aisling or ClientEntityType.Creature))
                return;

            (targetId, tileX, tileY) = (entity.Id, entity.TileX, entity.TileY);
        }

        //arm first, and only fire if this spell actually took cast mode — an empty, cooling-down, or NoTarget slot never
        //arms, and firing anyway would cast on a spell the drop never selected
        if (!TryBeginCast(spellSlot))
            return;

        CastingSystem.SelectTarget(
            targetId,
            tileX,
            tileY,
            Game.Connection);
    }

    //--- hotkeys ---

    /// <summary>
    ///     Returns true when no mutually-exclusive options panel is currently visible. Used by the F3/F4/F10 shortcuts so
    ///     pressing one hotkey cannot overlap another options popup.
    /// </summary>
    private static bool CanShowOptionsPanel(params UIElement[] others)
    {
        foreach (var other in others)
            if (other.Visible)
                return false;

        return true;
    }

    /// <summary>
    ///     The actions a key may run right now. Song notes only while a call is live, so their keys (U/I/O/P by
    ///     default) stay with whatever else has them the rest of the time. While a popup holds the control stack only the
    ///     actions that worked over popups before keep working: song notes, typing in chat and the Q/W/E/R toggles, which
    ///     close their own panels.
    /// </summary>
    private bool IsActionUsable(GameAction action)
    {
        if ((GameActions.SongNote(action) > 0) && !WorldState.Song.HasLiveCall)
            return false;

        if (Game.Dispatcher.ControlStackCount == 0)
            return true;

        return (GameActions.SongNote(action) > 0)
               || action is GameAction.FocusChat or GameAction.Options or GameAction.Boards or GameAction.WorldList or GameAction.SocialStatus;
    }

    private void SendEmote(GameAction action)
    {
        var player = WorldState.GetPlayerEntity();

        //gate emote initiation on the same condition as movement: face emotes lock
        //the body while playing, so it shouldn't be possible to start one mid-walk
        //or while another emote/body anim is already running.
        if (player is null || !player.IsAtRest)
            return;

        Game.Connection.SendEmote(GameActions.EmoteAnimation(action));
    }

    /// <summary>Records a song note into the live call, and sends the answer once all four are in.</summary>
    private void EnterSongNote(byte note)
    {
        var song = WorldState.Song;

        if (!song.EnterNote(note))
            return;

        var (n1, n2, n3, n4) = song.TakeAnswer();
        Game.Connection.SendSongAnswer(
            song.CallId,
            n1,
            n2,
            n3,
            n4);

        song.ClearCall();
    }

    /// <summary>Uses slot <paramref name="slot" /> (1-12) on the open panel. False when the open panel has no slots.</summary>
    private bool UseSlotHotkey(int slot)
    {
        var byteSlot = (byte)slot;

        switch (WorldHud.ActiveTab)
        {
            case HudTab.Inventory:
                Game.Connection.UseItem(byteSlot);

                break;

            case HudTab.Skills:
                HandleSkillSlotClicked(byteSlot);

                break;

            case HudTab.SkillsAlt:
                HandleSkillSlotClicked((byte)(byteSlot + 36));

                break;

            case HudTab.Spells:
                HandleSpellSlotClicked(byteSlot);

                break;

            case HudTab.SpellsAlt:
                HandleSpellSlotClicked((byte)(byteSlot + 36));

                break;

            case HudTab.Tools:
                if (slot is >= 1 and <= 6)
                    HandleSkillSlotClicked((byte)(72 + slot));
                else
                    HandleSpellSlotClicked((byte)(66 + slot));

                break;

            case HudTab.Chat:
            case HudTab.MessageHistory:
            case HudTab.Stats:
            case HudTab.ExtendedStats:
                {
                    var macroText = MacrosList.GetMacroValue(slot - 1);

                    //focus regardless of whether the slot is bound — pressing a macro key
                    //is also the user's "start typing in chat" shortcut, so an empty slot
                    //should just open the input without prefilling text
                    WorldHud.ChatInput.Focus();

                    if (macroText.Length > 0)
                        WorldHud.ChatInput.SetText(macroText, macroText.Length);

                    break;
                }

            default:
                return false;
        }

        return true;
    }

    //--- chant editing ---

    private void WireAbilityRightClicks(PanelBase panel)
    {
        foreach (var slotControl in panel.Slots)
            if (slotControl is AbilitySlotControl ability)
                ability.OnRightClick += s => OpenChantEdit(panel, s);
    }

    private void OpenChantEdit(PanelBase source, byte slot)
    {
        var control = source.GetSlotControl(slot) as AbilitySlotControl;

        if (control is null || string.IsNullOrEmpty(control.AbilityName))
            return;

        var isSpell = control is SpellSlot;

        string[] currentChants;
        int lineCount;

        if (control is SpellSlot spell)
        {
            currentChants = spell.Chants;
            lineCount = spell.CastLines;
        } else if (control is SkillSlot skill)
        {
            currentChants = [skill.Chant];
            lineCount = 1;
        } else
            return;

        ChantEdit.Show(
            slot,
            control.AbilityName,
            control.AbilityLevel ?? string.Empty,
            control.NormalTexture,
            currentChants,
            lineCount,
            isSpell);
    }

    private void HandleChantSet(byte slot, string[] chantLines, bool isSpell)
    {
        if (isSpell)
        {
            foreach (var panel in new[]
                     {
                         WorldHud.SpellBook,
                         WorldHud.SpellBookAlt,
                         WorldHud.Tools.WorldSpells
                     })
            {
                var spellSlot = panel.GetSpellSlot(slot);

                if (spellSlot is null)
                    continue;

                for (var i = 0; i < Math.Min(chantLines.Length, spellSlot.Chants.Length); i++)
                    spellSlot.Chants[i] = chantLines[i];
            }

            SaveSpellChants();
            WorldState.ReloadChants();
        } else
        {
            foreach (var panel in new[]
                     {
                         WorldHud.SkillBook,
                         WorldHud.SkillBookAlt,
                         WorldHud.Tools.WorldSkills
                     })
            {
                var skillSlot = panel.GetSkillSlot(slot);

                skillSlot?.Chant = chantLines.Length > 0 ? chantLines[0] : string.Empty;
            }

            SaveSkillChants();
            WorldState.ReloadChants();
        }
    }

    //--- cache / persistence helpers ---

    private void LoadPlayerFamilyList()
    {
        var family = DataContext.LocalPlayerSettings.LoadFamilyList();
        StatusBook.SetFamilyMembers(family);
        WorldList.SetFamilyNames(family);
    }

    private void SavePlayerFamilyList()
    {
        var family = StatusBook.GetFamilyMembers();

        if (family is not null)
        {
            DataContext.LocalPlayerSettings.SaveFamilyList(family);
            WorldList.SetFamilyNames(family);
        }
    }

    private void LoadPlayerFriendList()
    {
        var names = DataContext.LocalPlayerSettings.LoadFriendList();

        FriendsList.SetFriends(names);
        WorldList.SetFriendNames(names);
    }

    private void SavePlayerFriendList()
    {
        var names = FriendsList.GetFriendNames();
        DataContext.LocalPlayerSettings.SaveFriendList(names);
        WorldList.SetFriendNames(names);
    }

    private void LoadPlayerMacros()
    {
        var macros = DataContext.LocalPlayerSettings.LoadMacros();
        MacrosList.SetMacros(macros);
    }

    private void SaveSkillChants()
    {
        var entries = new List<SkillChantEntry>();

        for (byte i = 1; i <= SkillBook.MAX_SLOTS; i++)
        {
            var slot = WorldHud.SkillBook.GetSkillSlot(i)
                       ?? WorldHud.SkillBookAlt.GetSkillSlot(i)
                       ?? WorldHud.Tools.WorldSkills.GetSkillSlot(i);

            if (slot is null || string.IsNullOrEmpty(slot.AbilityName))
                continue;

            entries.Add(
                new SkillChantEntry
                {
                    Name = slot.AbilityName,
                    Chant = slot.Chant
                });
        }

        DataContext.LocalPlayerSettings.SaveSkillChants(entries);
    }

    private void SaveSpellChants()
    {
        var entries = new List<SpellChantEntry>();

        for (byte i = 1; i <= SpellBook.MAX_SLOTS; i++)
        {
            var slot = WorldHud.SpellBook.GetSpellSlot(i)
                       ?? WorldHud.SpellBookAlt.GetSpellSlot(i)
                       ?? WorldHud.Tools.WorldSpells.GetSpellSlot(i);

            if (slot is null || string.IsNullOrEmpty(slot.AbilityName))
                continue;

            var entry = new SpellChantEntry
            {
                Name = slot.AbilityName
            };
            Array.Copy(slot.Chants, entry.Chants, 10);
            entries.Add(entry);
        }

        DataContext.LocalPlayerSettings.SaveSpellChants(entries);
    }
    #endregion

    #region Root Event Handlers

    /// <summary>
    ///     Handles keyboard input that bubbles up to the root panel (no focused element consumed it). The key is looked up
    ///     in the player's key bindings (F12) and runs the action bound to it; Alt+Enter, F12 and Escape are fixed.
    /// </summary>
    private void OnRootKeyDown(KeyDownEvent e)
    {
        //alt+enter — cycle window size
        if ((e.Scancode == Scancode.Enter) && e.Modifiers.HasFlag(KeyModifiers.Alt))
        {
            Game.CycleWindowSize();
            e.Handled = true;

            return;
        }

        //tab targeting takes Tab, the arrows, Enter, Escape and F1-F8 while a spell waits for a target
        if (HandleTabTargetKey(e))
        {
            e.Handled = true;

            return;
        }

        //every bound action the key could mean, exact chord first; an action that does not apply right now (no chat
        //panel to scroll, say) lets the next one have the key
        foreach (var action in KeyBindings.Current.Candidates(KeyChord.From(e), IsActionUsable))
            if (RunAction(action))
            {
                e.Handled = true;

                return;
            }

        //stack guard: the fixed keys below stay quiet while a popup is active
        if (Game.Dispatcher.ControlStackCount > 0)
            return;

        //f12 — key bindings
        if (e.Scancode == Scancode.F12)
        {
            KeyBindingsWindow.Show();
            e.Handled = true;

            return;
        }

        //escape — collapse any expanded HUD panel back to normal size. only consume
        //the key when something was actually collapsed so it stays a no-op otherwise.
        if (e.Scancode == Scancode.Escape)
        {
            if (WorldHud.CollapseExpanded())
                e.Handled = true;

            return;
        }

        //whisper on ISO boards, while whisper is still on its default Shift+' — the key left of Enter differs by board:
        //  ISO QWERTZ : the '#' key     — scancode OemPipe + keycode '#'.
        //  ISO AZERTY : the '*'/'µ' key — scancode OemPipe + keycode '*'.
        //the keycode guard keeps the ANSI backslash key (same scancode, but it types '|' under Shift) from being taken
        //for it. A player who rebinds whisper gets exactly the key they chose instead
        if (KeyBindings.Current.IsDefault(GameAction.Whisper, 0)
            && e is { Shift: true, Scancode: Scancode.OemPipe, Keycode: Keycode.Hash or Keycode.Asterisk })
        {
            WorldHud.ChatInput.FocusWhisper();
            e.Handled = true;
        }
    }

    /// <summary>Runs one bound action. False when it does not apply right now, so the key may mean something else.</summary>
    private bool RunAction(GameAction action)
    {
        if (GameActions.SongNote(action) is var note and > 0)
        {
            EnterSongNote(note);

            return true;
        }

        if (GameActions.IsEmote(action))
        {
            //consumed even when the emote cannot start, so the key never falls through to a slot
            SendEmote(action);

            return true;
        }

        if (GameActions.SlotNumber(action) is var slot and > 0)
            return UseSlotHotkey(slot);

        switch (action)
        {
            case GameAction.FocusChat:
                if (!WorldHud.ChatInput.IsFocused)
                    WorldHud.ChatInput.Focus();

                return true;

            //q/w/e/r toggle group — usable over popups because these panels use the control stack themselves and need
            //to toggle while open
            case GameAction.Options:
                ForceCloseOtherTogglePanels(GameAction.Options);

                if (MainOptions.Visible)
                {
                    SettingsDialog.Hide();
                    MacrosList.Hide();
                    FriendsList.Hide();
                    MainOptions.SlideClose();
                } else
                {
                    WorldHud.OptionButton?.IsSelected = true;

                    MainOptions.Show();
                }

                return true;

            case GameAction.Boards:
                ForceCloseOtherTogglePanels(GameAction.Boards);

                if (IsAnyBoardPanelVisible())
                {
                    if (BoardList.Visible)
                        BoardList.SlideClose();
                    else
                        WorldState.Board.CloseSession();
                } else
                {
                    WorldHud.BulletinButton?.IsSelected = true;

                    Game.Connection.SendBoardInteraction(BoardRequestType.BoardList);
                }

                return true;

            case GameAction.WorldList:
                //middle mouse + the world list key opens the emote wheel — don't toggle world list for that chord
                //or for key-repeat / late keydown while the key is still held after the chord ends
                if (InputBuffer.IsMiddleButtonHeld || EmoteWheel.IsOpen || _suppressWorldListUntilERelease)
                    return true;

                ForceCloseOtherTogglePanels(GameAction.WorldList);

                if (WorldList.Visible)
                    WorldList.SlideClose();
                else
                {
                    WorldHud.UsersButton?.IsSelected = true;

                    Game.Connection.RequestWorldList();
                }

                return true;

            case GameAction.SocialStatus:
                ForceCloseOtherTogglePanels(GameAction.SocialStatus);
                ToggleSocialStatusPicker();

                return true;

            //assail — fires on both initial press and os key-repeat keydowns while held. rate-limited to
            //SPACEBAR_INTERVAL_MS since os key-repeat rates vary wildly.
            case GameAction.Assail:
            {
                var now = Environment.TickCount64;

                if ((now - LastSpacebarMs) >= SPACEBAR_INTERVAL_MS)
                {
                    Game.Connection.Spacebar();
                    Pathfinding.Clear();
                    LastSpacebarMs = now;
                }

                return true;
            }

            case GameAction.Shout:
                WorldHud.ChatInput.FocusShout();

                return true;

            case GameAction.Whisper:
                WorldHud.ChatInput.FocusWhisper();

                return true;

            case GameAction.Inventory:
                return ActivateTab(HudTab.Inventory, false);

            case GameAction.ExpandInventory:
                return ActivateTab(HudTab.Inventory, true);

            case GameAction.Skills:
                return ActivateTab(HudTab.Skills, false);

            case GameAction.SkillsAlt:
                return ActivateTab(HudTab.Skills, true);

            case GameAction.Spells:
                return ActivateTab(HudTab.Spells, false);

            case GameAction.SpellsAlt:
                return ActivateTab(HudTab.Spells, true);

            case GameAction.Chat:
                return ActivateTab(HudTab.Chat, false);

            case GameAction.MessageHistory:
                return ActivateTab(HudTab.Chat, true);

            case GameAction.Stats:
                return ActivateTab(HudTab.Stats, false);

            case GameAction.ExtendedStats:
                return ActivateTab(HudTab.Stats, true);

            case GameAction.Tools:
                return ActivateTab(HudTab.Tools, false);

            //tab map overlay (suppressed by NoTabMap map flag)
            case GameAction.TabMap:
                if (!CurrentMapFlags.HasFlag(MapFlags.NoTabMap))
                    TabMapVisible = !TabMapVisible;

                return true;

            case GameAction.MapZoomIn:
                if (!TabMapVisible)
                    return false;

                TabMapRenderer.ZoomIn();

                return true;

            case GameAction.MapZoomOut:
                if (!TabMapVisible)
                    return false;

                TabMapRenderer.ZoomOut();

                return true;

            //help merchant (server-side)
            case GameAction.Help:
                Game.Connection.ClickEntity(uint.MaxValue);

                return true;

            case GameAction.Macros:
                if (CanShowOptionsPanel(SettingsDialog, FriendsList))
                    MacrosList.Show();

                return true;

            case GameAction.Settings:
                if (CanShowOptionsPanel(MacrosList, FriendsList))
                    SettingsDialog.Show();

                return true;

            case GameAction.Refresh:
                Game.Connection.RequestRefresh();

                return true;

            case GameAction.BoardList:
                Game.Connection.SendBoardInteraction(BoardRequestType.BoardList);

                return true;

            //ignore list management (toggle)
            case GameAction.IgnoreList:
                if (WorldHud.ChatInput.Mode != ChatMode.None)
                    WorldHud.ChatInput.Unfocus();
                else
                    WorldHud.ChatInput.FocusIgnore();

                return true;

            case GameAction.Friends:
                if (CanShowOptionsPanel(MacrosList, SettingsDialog))
                    FriendsList.Show();

                return true;

            //taken by ChaosGame, which reads it every frame on every screen; claimed here so the key does nothing else
            case GameAction.Screenshot:
                return true;

            //swap hud layout (small <-> large)
            case GameAction.SwapHud:
                SwapHudLayout();

                return true;

            //unequip weapon and shield
            case GameAction.UnequipWeapons:
                if (WorldState.Equipment.GetSlot(EquipmentSlot.Weapon) is not null)
                    Game.Connection.Unequip(EquipmentSlot.Weapon);

                if (WorldState.Equipment.GetSlot(EquipmentSlot.Shield) is not null)
                    Game.Connection.Unequip(EquipmentSlot.Shield);

                return true;

            //flash group member highlighting (1000ms, gated while pending or active)
            case GameAction.GroupHighlight:
                if (GroupHighlightRequested || (GroupHighlightedIds.Count > 0))
                    return false;

                GroupHighlightRequested = true;
                Game.Connection.RequestSelfProfile();

                return true;

            //pick up item from under player, or from the tile in front
            case GameAction.PickUp:
                TryPickupItem();

                return true;

            //town map toggle (suppressed by the NoTownMap map flag)
            case GameAction.TownMap:
                if (TownMapControl.Visible)
                    TownMapControl.Hide();
                else
                    ToggleTownMap();

                return true;

            //group panel (members tab)
            case GameAction.Group:
                Game.Connection.RequestSelfProfile();
                GroupPanel.ShowMembers();

                return true;

            //scrolls the active chat-style panel (F = chat, shift+F = message history)
            case GameAction.ScrollChatUp:
                return ScrollChatPanel(1);

            case GameAction.ScrollChatDown:
                return ScrollChatPanel(-1);

            case GameAction.MoveUp:
                Move(Direction.Up);

                return true;

            case GameAction.MoveRight:
                Move(Direction.Right);

                return true;

            case GameAction.MoveDown:
                Move(Direction.Down);

                return true;

            case GameAction.MoveLeft:
                Move(Direction.Left);

                return true;
        }

        return false;
    }

    //tab panel switching — blocked while dragging the orange bar
    private bool ActivateTab(HudTab tab, bool alternate)
    {
        if (WorldHud.IsOrangeBarDragging)
            return false;

        WorldHud.HandleTabActivation(tab, alternate);

        return true;
    }

    private bool ScrollChatPanel(int delta)
    {
        if (WorldHud.ChatDisplay.Visible)
        {
            WorldHud.ChatDisplay.Scroll(delta);

            return true;
        }

        if (WorldHud.MessageHistory.Visible)
        {
            WorldHud.MessageHistory.Scroll(delta);

            return true;
        }

        return false;
    }

    /// <summary>Turns to face <paramref name="direction" />, or walks that way when already facing it.</summary>
    private void Move(Direction direction)
    {
        Pathfinding.Clear();
        var player = WorldState.GetPlayerEntity();

        if (player is null)
            return;

        if (player.IsAtRest)
        {
            //fresh input at idle invalidates any direction queued from a prior walk —
            //the queue must not override what the user just pressed.
            QueuedWalkDirection = null;

            if (player.Direction != direction)
            {
                Game.Connection.Turn(direction);
                player.Direction = direction;
            } else
                PredictAndWalk(player, direction);
        } else if (player.AnimState == EntityAnimState.Walking)
        {
            var totalDuration = Math.Max(1f, player.AnimFrameCount * player.AnimFrameIntervalMs);
            var progress = player.AnimElapsedMs / totalDuration;

            if (progress >= WALK_QUEUE_THRESHOLD)
                QueuedWalkDirection = direction;
        }
    }

    /// <summary>
    ///     Handles mouse scroll that bubbles up to the root panel (no child consumed it). Forwards to whichever chat-style
    ///     HUD panel is currently visible so the player can scroll chat/system messages from anywhere on screen.
    /// </summary>
    private void OnRootMouseScroll(MouseScrollEvent e)
    {
        if (WorldHud.ChatDisplay.Visible)
        {
            WorldHud.ChatDisplay.OnMouseScroll(e);

            return;
        }

        if (WorldHud.MessageHistory.Visible)
            WorldHud.MessageHistory.OnMouseScroll(e);
    }

    /// <summary>
    ///     Handles right-mouse-button presses that bubble up to the root panel. Right-click pathfinding
    ///     fires on press (not release) for snappier response — a held right-click begins moving the
    ///     player immediately instead of waiting for the button to come back up.
    /// </summary>
    /// <remarks>
    ///     Only a press on empty ground moves. A press that lands on a creature or another aisling is the
    ///     opening half of the double-right-click that follows and assails it, so it starts no ground path:
    ///     doing so walked the player off on every attack attempt, which is the whole reason right-click felt
    ///     oversensitive, and it shifted the camera out from under the second click of the pair.
    /// </remarks>
    private void OnRootMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Right)
            return;

        //right-click backs out of cast mode rather than pathfinding under the targeting cursor. This is the only way to
        //cancel a ground-targeted spell — an entity-targeted one can be dropped by clicking empty ground, but for a
        //ground spell every tile is a valid target, so a left-click can never mean "never mind".
        if (CastingSystem.IsTargeting)
        {
            CastingSystem.CancelTargeting();
            e.Handled = true;

            return;
        }

        if (e.Shift)
        {
            HandleShiftRightClick(e.ScreenX, e.ScreenY);
            e.Handled = true;

            return;
        }

        //cache the hovered entity for the upcoming doubleclick — the entity can walk between the two clicks, and
        //a path still running from an earlier click can move the player, either of which makes the second click's
        //ScreenToTile resolve to a different world tile than the entity actually occupies
        var currentTick = Environment.TickCount;

        if ((currentTick - PendingDoubleClickTick) > DOUBLE_CLICK_CACHE_WINDOW_MS)
            PendingDoubleClickEntityId = null;

        var hoverEntity = GetEntityAtScreen(e.ScreenX, e.ScreenY);

        //exclude self — the player's own sprite has a hitbox, and a rapid right-click on the tile the
        //player is walking off of overlaps that hitbox, which would cache the player as a double-click
        //target and kick off a self-follow loop in OnRootDoubleClick
        //exclude hidden aislings for the same reason OnRootDoubleClick refuses to follow them: they carry a hitbox
        //only so spells can target them. Caching one would suppress the move below and leave the press doing
        //nothing at all, since the doubleclick then declines it too
        var isDoubleClickTarget = false;

        if (hoverEntity?.Type is ClientEntityType.Aisling or ClientEntityType.Creature
            && (hoverEntity.Id != Game.Connection.AislingId)
            && !hoverEntity.IsHidden)
        {
            PendingDoubleClickEntityId = hoverEntity.Id;
            PendingDoubleClickTick = currentTick;
            isDoubleClickTarget = true;
        }

        //neither of these presses starts a ground move, and both prime the same-tile tracker so the doubleclick's
        //fallback tile lookup still resolves when the entity cache misses. Ctrl is a UI modifier (ctrl+left-click
        //opens the aisling context menu); a press on a follow/assail target belongs to the pair, not to movement.
        if (e.Ctrl || isDoubleClickTarget)
        {
            if (MapFile is not null)
            {
                (var tileX, var tileY) = ScreenToTile(e.ScreenX, e.ScreenY);
                tileX = Math.Clamp(tileX, 0, MapFile.Width - 1);
                tileY = Math.Clamp(tileY, 0, MapFile.Height - 1);
                RightClickTracker.Click(tileX, tileY);
            }
        } else
            HandleWorldRightClick(e.ScreenX, e.ScreenY);

        e.Handled = true;
    }

    /// <summary>
    ///     Handles mouse clicks that bubble up to the root panel (no child element consumed them).
    ///     Contains cast-mode target selection, Ctrl/Alt-click, and left-click world interaction.
    ///     Right-click pathfinding is handled in OnRootMouseDown for faster response.
    /// </summary>
    private void OnRootClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        LastLeftClickWasCast = CastingSystem.IsTargeting;

        //exchange gold is now set via the inline editable money field (see ExchangeControl.MyMoneyTextBox),
        //which owns its own click/focus — no viewport-level coordination needed here.

        //cast mode — target selection or cancel
        if (CastingSystem.IsTargeting)
        {
            if (CastingSystem.IsGroundTargeting)
            {
                //ground-targeted spells land on the aimed tile, not an entity (id 0). clamp off-map clicks to the
                //nearest edge tile, mirroring right-click pathfinding.
                if (GroundTargetTileAt(e.ScreenX, e.ScreenY) is { } tile)
                    CastingSystem.SelectTarget(
                        0,
                        tile.X,
                        tile.Y,
                        Game.Connection);
                else
                    CastingSystem.CancelTargeting();
            } else
            {
                var hoverEntity = GetEntityAtScreen(e.ScreenX, e.ScreenY);

                if (hoverEntity?.Type is ClientEntityType.Aisling or ClientEntityType.Creature)
                {
                    //a clicked target is the last target too (for this spell's kind), so tab targeting goes back to it on
                    //the next spell of that kind. Before SelectTarget, which disarms the spell
                    RememberCastTarget(hoverEntity.Id);

                    CastingSystem.SelectTarget(
                        hoverEntity.Id,
                        hoverEntity.TileX,
                        hoverEntity.TileY,
                        Game.Connection);
                } else
                    CastingSystem.CancelTargeting();
            }

            e.Handled = true;

            return;
        }

        //ctrl+click — context menu on aisling entities
        if (e.Ctrl)
        {
            HandleCtrlClick(e.ScreenX, e.ScreenY);
            e.Handled = true;

            return;
        }

        //alt+click on self — open self profile
        if (e.Alt)
        {
            var hoverEntity = GetEntityAtScreen(e.ScreenX, e.ScreenY);

            if (hoverEntity is not null && (hoverEntity.Id == Game.Connection.AislingId))
            {
                SelfProfileRequested = true;
                Game.Connection.RequestSelfProfile();
            } else
                HandleWorldClick(e.ScreenX, e.ScreenY);

            e.Handled = true;

            return;
        }

        HandleWorldClick(e.ScreenX, e.ScreenY);
        e.Handled = true;
    }

    /// <summary>
    ///     Handles double-click events that bubble up to the root panel.
    ///     Left double-click: interact with entities (pickup ground items, click NPC/aisling).
    ///     Right double-click: follow and assail entity.
    ///     Uses TileClickTracker same-tile guard since the dispatcher only checks same-element (Root),
    ///     not same-tile.
    /// </summary>
    private void OnRootDoubleClick(DoubleClickEvent e)
    {
        if (MapFile is null)
            return;

        var viewport = WorldHud.ViewportBounds;

        if ((e.ScreenX < viewport.X)
            || (e.ScreenX >= (viewport.X + viewport.Width))
            || (e.ScreenY < viewport.Y)
            || (e.ScreenY >= (viewport.Y + viewport.Height)))
            return;

        (var tileX, var tileY) = ScreenToTile(e.ScreenX, e.ScreenY);

        if (e.Button == MouseButton.Left)
        {
            //a cast click produced this doubleclick — the second cast of a rapid pair, not a request to interact
            if (LastLeftClickWasCast)
                return;

            var sameTile = LeftClickTracker.Click(tileX, tileY);

            if (!sameTile)
                return;

            //shift+doubleclick bypasses hitboxes and only picks up ground items
            if (e.Shift)
            {
                var groundItem = WorldState.GetGroundItemAt(tileX, tileY);

                if (groundItem is not null)
                {
                    var firstEmptySlot = WorldState.Inventory.GetFirstEmptySlot();
                    Game.Connection.PickupItem(groundItem.TileX, groundItem.TileY, firstEmptySlot);
                }
            } else
            {
                var entity = GetEntityAtScreen(e.ScreenX, e.ScreenY);

                if (entity is not null && !entity.IsHidden)
                {
                    if (entity.Type == ClientEntityType.GroundItem)
                    {
                        var firstEmptySlot = WorldState.Inventory.GetFirstEmptySlot();
                        Game.Connection.PickupItem(entity.TileX, entity.TileY, firstEmptySlot);
                    } else if ((entity.Type != ClientEntityType.Aisling) || ClientSettings.ClickToOpenProfile)
                        Game.Connection.ClickEntity(entity.Id);
                }
            }

            e.Handled = true;
        } else if (e.Button == MouseButton.Right)
        {
            if (MapPathfinder is null)
                return;

            var player = WorldState.GetPlayerEntity();

            if (player is null)
                return;

            tileX = Math.Clamp(tileX, 0, MapFile.Width - 1);
            tileY = Math.Clamp(tileY, 0, MapFile.Height - 1);

            //tracker still updates so any consumers relying on the last-clicked tile stay accurate
            var sameTile = RightClickTracker.Click(tileX, tileY);

            //prefer the entity captured on the first single right-click — pathfinding started by that click will have
            //moved the player by now, shifting the camera and making ScreenToTile resolve to a different world tile
            WorldEntity? entity = null;

            if (PendingDoubleClickEntityId.HasValue
                && ((Environment.TickCount - PendingDoubleClickTick) <= DOUBLE_CLICK_CACHE_WINDOW_MS))
                entity = WorldState.GetEntity(PendingDoubleClickEntityId.Value);

            //fallback to the legacy tile-based lookup only when the cache miss AND the tiles line up
            if (entity is null)
            {
                if (!sameTile)
                {
                    PendingDoubleClickEntityId = null;

                    return;
                }

                entity = WorldState.GetEntityAt(tileX, tileY);
            }

            //reject self — following yourself produces a re-pathfinding loop that walks into walls or oscillates
            //reject hidden aislings — they have a hitbox for spell targeting but should not be followable
            if (entity?.Type is ClientEntityType.Aisling or ClientEntityType.Creature
                && (entity.Id != Game.Connection.AislingId)
                && !entity.IsHidden)
            {
                Pathfinding.SetEntityTarget(entity.Id);
                PathfindToEntity(player, entity);
            }

            PendingDoubleClickEntityId = null;
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Handles drag-drop events that bubble up to the root panel. A slot drop that no PanelSlot consumed landed in the
    ///     world viewport or on a non-slot UI element; a bank drop only means something over the inventory.
    /// </summary>
    private void OnRootDragDrop(DragDropEvent e)
    {
        switch (e.Payload)
        {
            case SlotDragPayload { Source.Parent: PanelBase { IsDragging: true } panel }:
                panel.CompleteDragOutside(e.ScreenX, e.ScreenY);
                e.Handled = true;

                break;

            //a bank drag only means something over the inventory; anywhere else it is a cancel. Escape can close the
            //bank mid-drag, and Hide() clears BankState — withdrawing then would act on a window that is gone.
            case BankDragPayload bank:
                e.Handled = true;

                if (!Bank.Visible || !IsOverInventory(e.DropTarget))
                    return;

                if (bank.IsGold)
                    PromptBankGoldWithdraw();
                else
                    BeginBankWithdraw(bank.ItemName!, bank.Count);

                break;
        }
    }

    //walk the hit-tested drop target's ancestors rather than testing the panel's rect: hit-testing already skips the
    //hidden HUD tabs (skills/spells share the inventory's rect) and anything a popup covers.
    private bool IsOverInventory(UIElement? dropTarget)
    {
        for (var current = dropTarget; current is not null; current = current.Parent)
            if (current == WorldHud.Inventory)
                return true;

        return false;
    }

    #endregion

    #region Click Handling
    /// <summary>
    ///     Converts screen mouse coordinates to tile coordinates, accounting for the HUD viewport offset. The world is
    ///     rendered with a translation matrix for the viewport origin, so mouse coords must be adjusted to match.
    /// </summary>
    private WorldEntity? GetEntityAtScreen(int mouseX, int mouseY)
    {
        if (MapFile is null)
            return null;

        //hitbox rects are stored in viewport-relative coords (the world spriteBatch applies a
        //viewport-origin translation at draw time), so mouse coords must be rebased to match.
        var viewport = WorldHud.ViewportBounds;
        var viewportMouseX = mouseX - viewport.X;
        var viewportMouseY = mouseY - viewport.Y;

        //iterate hitboxes back-to-front (last drawn = closest to camera = highest priority)
        for (var i = EntityHitBoxes.Count - 1; i >= 0; i--)
        {
            var hitbox = EntityHitBoxes[i];

            if (hitbox.ScreenRect.Contains(viewportMouseX, viewportMouseY))
                return WorldState.GetEntity(hitbox.EntityId);
        }

        //fallback: tile-based lookup for ground items
        (var tileX, var tileY) = ScreenToTile(mouseX, mouseY);

        return WorldState.GetGroundItemAt(tileX, tileY);
    }

    private (int TileX, int TileY) ScreenToTile(int mouseX, int mouseY)
    {
        var viewport = WorldHud.ViewportBounds;
        var worldPos = Camera.ScreenToWorld(new Vector2(mouseX - viewport.X, mouseY - viewport.Y));
        var tile = Camera.WorldToTile(worldPos.X, worldPos.Y, MapFile!.Height);

        return (tile.X, tile.Y);
    }

    private void TryPickupItem()
    {
        var player = WorldState.GetPlayerEntity();

        if (player is null)
            return;

        var slot = WorldState.Inventory.GetFirstEmptySlot();

        if (slot == 0)
            return;

        //first try the player's own tile
        if (WorldState.HasGroundItemAt(player.TileX, player.TileY))
        {
            Game.Connection.PickupItem(player.TileX, player.TileY, slot);

            return;
        }

        //then try the tile in front (direction the player is facing)
        (var dx, var dy) = player.Direction.ToTileOffset();
        var frontX = player.TileX + dx;
        var frontY = player.TileY + dy;

        if (WorldState.HasGroundItemAt(frontX, frontY))
            Game.Connection.PickupItem(frontX, frontY, slot);
    }

    private void HandleWorldClick(int mouseX, int mouseY)
    {
        if (MapFile is null)
            return;

        var viewport = WorldHud.ViewportBounds;

        if ((mouseX < viewport.X)
            || (mouseX >= (viewport.X + viewport.Width))
            || (mouseY < viewport.Y)
            || (mouseY >= (viewport.Y + viewport.Height)))
            return;

        (var tileX, var tileY) = ScreenToTile(mouseX, mouseY);

        //track tile for same-tile guard used by onrootdoubleclick
        LeftClickTracker.Click(tileX, tileY);

        //check group box text overlays first — they sit above entity hitboxes.
        //rects are viewport-relative, rebase mouse coords to match.
        var groupBoxViewport = WorldHud.ViewportBounds;
        var groupBoxHit = Overlays.GetGroupBoxAtScreen(mouseX - groupBoxViewport.X, mouseY - groupBoxViewport.Y);

        if (groupBoxHit.HasValue)
        {
            (_, var entityName) = groupBoxHit.Value;

            Game.Connection.SendGroupInvite(ClientGroupSwitch.ViewGroupBox, entityName);

            return;
        }

        //single click: check for entity at hitbox first, then tile interaction
        var entity = GetEntityAtScreen(mouseX, mouseY);

        //single-click on self opens own profile when the "click character profile" setting is enabled
        if (entity is not null
            && (entity.Type == ClientEntityType.Aisling)
            && (entity.Id == Game.Connection.AislingId)
            && ClientSettings.ClickToOpenProfile)
        {
            SelfProfileRequested = true;
            Game.Connection.RequestSelfProfile();

            return;
        }

        if (entity?.Type is ClientEntityType.Creature)
            Game.Connection.ClickEntity(entity.Id);
        else if (IsTileOnMap(tileX, tileY))
            //sent for every tile, not only ones with art, so the server can react to clicks on bare floor
            Game.Connection.ClickTile(tileX, tileY);
    }

    private void HandleCtrlClick(int mouseX, int mouseY)
    {
        if (MapFile is null)
            return;

        var viewport = WorldHud.ViewportBounds;

        if ((mouseX < viewport.X)
            || (mouseX >= (viewport.X + viewport.Width))
            || (mouseY < viewport.Y)
            || (mouseY >= (viewport.Y + viewport.Height)))
            return;

        var entity = GetEntityAtScreen(mouseX, mouseY);

        if (entity is null)
            return;

        if ((entity.Type == ClientEntityType.Aisling) && (entity.Id != Game.Connection.AislingId))
        {
            var name = entity.Name;
            var id = entity.Id;

            AislingContext.Show(
                mouseX,
                mouseY,
                name,
                () => Game.Connection.ClickEntity(id),
                () => Game.Connection.SendGroupInvite(ClientGroupSwitch.TryInvite, name),
                () => WorldHud.ChatInput.FocusWhisper(name));
        }
    }

    private void HandleWorldRightClick(int mouseX, int mouseY)
    {
        if (MapFile is null || MapPathfinder is null)
            return;

        var viewport = WorldHud.ViewportBounds;

        if ((mouseX < viewport.X)
            || (mouseX >= (viewport.X + viewport.Width))
            || (mouseY < viewport.Y)
            || (mouseY >= (viewport.Y + viewport.Height)))
            return;

        var player = WorldState.GetPlayerEntity();

        if (player is null)
            return;

        (var tileX, var tileY) = ScreenToTile(mouseX, mouseY);

        //clamp to map bounds
        tileX = Math.Clamp(tileX, 0, MapFile.Width - 1);
        tileY = Math.Clamp(tileY, 0, MapFile.Height - 1);

        //track tile for same-tile guard used by onrootdoubleclick
        RightClickTracker.Click(tileX, tileY);

        //don't pathfind to current position
        if ((tileX == player.TileX) && (tileY == player.TileY))
        {
            Pathfinding.Clear();

            return;
        }

        //reject right-clicks onto walls so we don't auto-walk into them. open doors pass this filter because
        //IsTileWallBlocked consults DoorTable. gms walk through walls so skip the filter for them.
        if (!IsGameMaster && IsTileWallBlocked(tileX, tileY))
            return;

        //single right-click — pathfind to ground tile
        Pathfinding.TargetEntityId = null;
        PathfindToTile(player, tileX, tileY);
    }

    /// <summary>
    ///     Shift+right-click: cancel pathfinding/auto-assailing, and if idle, turn toward the clicked tile.
    /// </summary>
    private void HandleShiftRightClick(int mouseX, int mouseY)
    {
        Pathfinding.Clear();

        if (MapFile is null)
            return;

        var player = WorldState.GetPlayerEntity();

        if (player is null || !player.IsAtRest)
            return;

        var viewport = WorldHud.ViewportBounds;

        if ((mouseX < viewport.X)
            || (mouseX >= (viewport.X + viewport.Width))
            || (mouseY < viewport.Y)
            || (mouseY >= (viewport.Y + viewport.Height)))
            return;

        (var tileX, var tileY) = ScreenToTile(mouseX, mouseY);

        tileX = Math.Clamp(tileX, 0, MapFile.Width - 1);
        tileY = Math.Clamp(tileY, 0, MapFile.Height - 1);

        if ((tileX == player.TileX) && (tileY == player.TileY))
            return;

        var dx = tileX - player.TileX;
        var dy = tileY - player.TileY;

        var direction = Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? Direction.Right : Direction.Left
            : dy > 0 ? Direction.Down : Direction.Up;

        if (player.Direction != direction)
        {
            Game.Connection.Turn(direction);
            player.Direction = direction;
        }
    }

    private void PathfindToTile(WorldEntity player, int tileX, int tileY)
    {
        if (MapPathfinder is null || MapFile is null)
            return;

        Pathfinding.Path = Pathfinder.FindPathToTile(
            MapPathfinder,
            player.TileX,
            player.TileY,
            tileX,
            tileY,
            MapFile.Width,
            MapFile.Height,
            GetPathfindingBlockedPoints(),
            IsGameMaster);
    }

    private void PathfindToEntity(WorldEntity player, WorldEntity target)
    {
        if (MapPathfinder is null || MapFile is null)
            return;

        var path = Pathfinder.FindPathToEntity(
            MapPathfinder,
            player.TileX,
            player.TileY,
            target.TileX,
            target.TileY,
            MapFile.Width,
            MapFile.Height,
            GetPathfindingBlockedPoints(),
            IsGameMaster,
            IsGameMaster ? null : IsTilePassable,
            out var alreadyAdjacent);

        //already adjacent: no path to walk, but keep TargetEntityId so the Update loop's auto-follow
        //branch turns and assails next tick. Pathfinding.Clear() here would wipe the target entity
        //that OnRootDoubleClick just set, breaking double-right-click follow on neighbors.
        if (alreadyAdjacent)
            Pathfinding.Path = null;
        else
            Pathfinding.Path = path;
    }
    #endregion
}