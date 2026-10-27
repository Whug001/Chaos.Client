#region
using Chaos.Client.Chat;
using Chaos.Client.Collections;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.Market;
using Chaos.Client.Data;
using Chaos.Client.Data.Repositories;
using Chaos.Client.Data.Utilities;
using Chaos.Client.Extensions;
using Chaos.Client.Models;
using Chaos.Client.Networking;
using Chaos.Client.Networking.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Models;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Extensions.Common;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using DALib.Drawing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    #region Server Event Handlers
    /// <summary>
    ///     The character whose settings file is loaded, so this only runs when it actually changes. Null until
    ///     the first one lands.
    /// </summary>
    private string? SettingsCharacter;

    /// <summary>
    ///     Swaps to this character's own settings file and puts the result into effect.
    /// </summary>
    /// <remarks>
    ///     The settings screen is built before anyone has logged in, so it seeds itself from the install-wide
    ///     file. By the time the character's name is known, every client-local value on it may be wrong, and the
    ///     ones with a live effect -- volume, window size -- have already been applied from the wrong file. So
    ///     this re-seeds the view model and re-applies those, rather than only changing the numbers.
    ///     <para />
    ///     Server-owned settings are untouched. They were already per character, held on the aisling and sent
    ///     back by the server, and nothing here should be second-guessing them.
    ///     <para />
    ///     Runs once per character, not once per packet. Its caller fires on every <c>DisplayAisling</c> the
    ///     server sends for the player, which is not a login event at all -- equipping something, changing sprite
    ///     or lantern, or anything else that calls <c>Display()</c> sends one, because a player is inside their
    ///     own viewport. Re-applying live settings that often fought the player: a window they had dragged to a
    ///     size of their own snapped back to the dropdown's on their next swing, and every one of those packets
    ///     re-read the settings file off disk.
    /// </remarks>
    private void LoadCharacterSettings(string characterName)
    {
        if (string.Equals(SettingsCharacter, characterName, StringComparison.Ordinal))
            return;

        SettingsCharacter = characterName;
        ClientSettings.LoadForCharacter(characterName);

        //the checkbox states the f4 screen reads
        WorldState.UserOptions.SeedLocalDefaults();

        //what this character put on their hotbars. The per-character store is initialized just before this is called
        LoadHotbars();

        //volume is live: the sliders and the mixer were both set from the previous file
        MainOptions.SetSoundVolume(ClientSettings.SoundVolume);
        MainOptions.SetMusicVolume(ClientSettings.MusicVolume);
        Game.SoundSystem.SetSoundVolume(ClientSettings.SoundVolume);
        Game.SoundSystem.SetMusicVolume(ClientSettings.MusicVolume);

        //and so are the window and the frame rate. Applied straight through rather than through the settings'
        //Apply methods, which would save the value back out again for no reason
        DisplaySettings.Applier?.Invoke(ClientSettings.ScreenMode);
        FrameRateSettings.Applier?.Invoke(ClientSettings.FrameRateMode);
    }

    //--- entity display / removal ---

    private void HandleDisplayAisling(DisplayAislingArgs args)
    {
        //update player name in hud when the player's own aisling is displayed
        if (args.Id == Game.Connection.AislingId)
        {
            WorldState.PlayerName = args.Name;
            Game.Window.Title = args.Name;
            UpdateHuds(HudOps.SetPlayerName, args.Name);
            UpdateHuds(HudOps.SetServerName, Game.Connection.ServerName);
            DataContext.LocalPlayerSettings.Initialize(args.Name);
            LoadCharacterSettings(args.Name);
            LoadPlayerFamilyList();
            LoadPlayerFriendList();
            LoadPlayerMacros();
            WorldState.ReloadChants();
            StatusBook.SetProfileText(LoadProfileText());
        }

        //check for idle animation ("04") frames on this aisling's body
        var entity = WorldState.GetEntity(args.Id);

        if (entity?.Appearance is { } appearance)
        {
            entity.IdleAnimFrameCount = Game.AislingRenderer.GetIdleAnimFrameCount(in appearance);

            //start idle cycling if entity is currently idle
            if (entity.AnimState == EntityAnimState.Idle)
                AnimationSystem.ResetToIdle(entity);
        }
    }

    private void HandleRemoveEntity(uint id)
    {
        //capture creature sprite for death dissolve before removing from worldstate
        var entity = WorldState.GetEntity(id);

        if (entity is { Type: ClientEntityType.Creature })
            CreateDyingEffect(entity);

        //clean up aisling composited texture cache
        Game.AislingRenderer.RemoveCachedEntity(id);

        //clean up a carving pumpkin's painted frames
        Game.CreatureRenderer.RemoveCachedEntity(id);

        //clean up all overlay caches (name tag, chat bubble, health bar, chant)
        Overlays.RemoveEntity(id);

        //clean up cached debug label texture
        DebugRenderer.RemoveEntity(id);

        //remove entity from worldstate (chaosgame skips removal when worldscreen is active)
        WorldState.RemoveEntity(id);
    }

    private void CreateDyingEffect(WorldEntity entity)
    {
        var creatureRenderer = Game.CreatureRenderer;
        var animInfo = creatureRenderer.GetAnimInfo(entity.SpriteId);

        if (animInfo is null)
            return;

        var info = animInfo.Value;
        (var frameIndex, var flip) = AnimationSystem.GetCreatureFrame(entity, in info);

        var spriteFrame = creatureRenderer.GetFrame(entity.SpriteId, frameIndex);

        if (spriteFrame is null)
            return;

        var frame = spriteFrame.Value;

        var dyingEffect = new EntityRemovalAnimation(
            Device,
            frame.Texture,
            entity.TileX,
            entity.TileY,
            frame.CenterX,
            frame.CenterY,
            frame.Left,
            frame.Top,
            flip);

        WorldState.DyingEffects.Add(dyingEffect);
    }

    //--- movement ---

    /// <summary>
    ///     Client-side prediction: sends Walk packet and immediately starts the walk animation locally without waiting for
    ///     server confirmation. The server response reconciles position if needed.
    /// </summary>
    private void PredictAndWalk(WorldEntity player, Direction direction)
    {
        //bounds check — don't walk off the map edge
        (var dx, var dy) = direction.ToTileOffset();
        var newX = player.TileX + dx;
        var newY = player.TileY + dy;

        if (MapFile is null || (newX < 0) || (newY < 0) || (newX >= MapFile.Width) || (newY >= MapFile.Height))
            return;

        //swimming gate — retail behavior, off by default, toggled via GlobalSettings.RequireSwimmingSkill
        if (GlobalSettings.RequireSwimmingSkill
            && player.IsOnSwimmingTile
            && !IsGameMaster
            && !WorldState.SkillBook.HasSkillByName("swimming"))
        {
            WorldState.Chat.AddMessage("You need to learn how to swim.", Color.White);

            return;
        }

        //collision check — gm bypasses all collision
        if (!IsGameMaster && !IsTilePassable(newX, newY))
            return;

        //the server would refuse this step with a full refresh: the enemy start, your own bank, or a hold
        if (!IsGameMaster && CausewayRefusesStep(newX, newY))
            return;

        //the server holds Glass Way players on their ledge until "Go!"; a step now would only be snapped back
        if (!IsGameMaster && GlassRefusesStep())
            return;

        //the server refuses steps during a batch too; refusing here avoids a rubber-band
        if (WorldState.Crafting.Running)
            return;

        Game.Connection.Walk(direction);

        //one more ack outstanding from the server
        InFlightWalkAcks++;
        LastWalkSentMs = Environment.TickCount64;

        //predict position locally
        player.TileX = newX;
        player.TileY = newY;
        WorldState.MarkSortDirty();

        var walkFrames = player.UsesCreatureWalkTiming ? Game.CreatureRenderer.GetWalkFrameCount(player.SpriteId) : null;

        AnimationSystem.StartWalk(
            player,
            direction,
            player.UsesCreatureWalkTiming,
            true,
            walkFrames);
        UpdateHuds(HudOps.SetCoords, player.TileX, player.TileY);
    }

    private void HandleClientWalkResponse(Direction direction, int oldX, int oldY)
    {
        var player = WorldState.GetPlayerEntity();

        if (player is null)
            return;

        //if any predicted walk is still un-acked, this packet is the FIFO ack for one of them.
        //treat it as a silent confirmation: the predicted state already reflects the move,
        //and any later Location packet will override if there was a true divergence.
        if (InFlightWalkAcks > 0)
        {
            InFlightWalkAcks--;

            return;
        }

        //no prediction in flight — this is a genuine server-initiated walk (push tile, knockback,
        //admin teleport). snap to the server's source position and animate the walk to the destination.
        QueuedWalkDirection = null;

        (var dx, var dy) = direction.ToTileOffset();
        var serverX = oldX + dx;
        var serverY = oldY + dy;

        player.TileX = serverX;
        player.TileY = serverY;
        WorldState.MarkSortDirty();

        var walkFrames = player.UsesCreatureWalkTiming ? Game.CreatureRenderer.GetWalkFrameCount(player.SpriteId) : null;

        AnimationSystem.StartWalk(
            player,
            direction,
            player.UsesCreatureWalkTiming,
            true,
            walkFrames);

        UpdateHuds(HudOps.SetCoords, serverX, serverY);
        Pathfinding.Clear();
    }

    //--- attributes ---

    private void HandleAttributes(AttributesArgs args)
        => IsGameMaster = args.StatUpdateType.HasFlag(StatUpdateType.GameMasterA)
                          || args.StatUpdateType.HasFlag(StatUpdateType.GameMasterB);

    //--- chat / messages ---

    /// <summary>
    ///     Rewrites server UTC timestamps to local time for display. A line carrying filter tags keeps its
    ///     original text: tags index into its character positions, so a rewrite would shift them.
    /// </summary>
    private static string WithLocalTime(string message, IReadOnlyList<ChatTag> tags)
        => tags is { Count: > 0 } ? message : LocalTimeText.Rewrite(message, DateTime.UtcNow, TimeZoneInfo.Local);

    private void HandleDisplayPublicMessage(DisplayPublicMessageArgs args)
    {
        var entityExists = WorldState.GetEntity(args.SourceId) is not null;

        if (args.PublicMessageType == PublicMessageType.Chant)
        {
            if (entityExists)
                Overlays.AddChantOverlay(args.SourceId, args.Message);

            return;
        }

        var entity = WorldState.GetEntity(args.SourceId);
        var isNpc = entity is not null && entity.Type is not ClientEntityType.Aisling;

        var color = args.PublicMessageType switch
        {
            PublicMessageType.Shout => TextColors.Shout,
            _                       => LegendColors.White
        };

        //server UTC timestamps become local time, unless tags index into the original text
        var message = WithLocalTime(args.Message, args.Tags);

        //single render boundary: the body transforms once here; the chat log and both bubbles share it
        var publicMessage = ChatDisplayBuilder.BuildPublic(
            message,
            args.Tags,
            WorldState.UserOptions.ChatFilterMode,
            FantasyDictionary.Default);

        if (!isNpc || ClientSettings.NpcRecordChat)
            WorldState.Chat.AddMessage(
                publicMessage.DisplayText,
                color,
                publicMessage.OriginalText,
                publicMessage.Tags,
                args.LineId);

        var isShout = args.PublicMessageType == PublicMessageType.Shout;

        //channel lines (world shout) arrive with the max id and no entity: no bubble, an orange bar copy instead
        RememberTranslatableLine(
            args.LineId,
            args.SourceId == uint.MaxValue ? TranslatedLineKind.WorldChannel : TranslatedLineKind.Public,
            args.SourceId,
            isShout,
            color,
            message);

        //over the speaker's portrait if they are sitting at the poker table, whose panel is covering the floor
        //the bubble below would be drawn on. Ahead of the entity check on purpose: the panel resolves seats by
        //entity id, not by presence in WorldState, so a seated speaker whose entity is momentarily absent (the
        //list being rebuilt by a same-map refresh) still gets their bubble on the table. Ignored outright when
        //they are not seated there.
        Poker.ShowChatBubble(args.SourceId, publicMessage.DisplayText, isShout, args.LineId);

        if (entity is null)
            return;

        //off in F4: everyone's bubble draws as the default, the player's own included
        var look = ClientSettings.ShowCustomChatBubbles ? args.BubbleLook : null;

        Overlays.AddChatBubble(args.SourceId, publicMessage.DisplayText, isShout, args.LineId, look);
    }

    /// <summary>
    ///     Remembers who whispered you so /r can reply. A received whisper reads "[Name]: text"; the echo of one you sent
    ///     reads "[Name]&gt; text" and must not be recorded as a reply target. Anchoring on the name's own closing bracket
    ///     keeps a "]: " inside the message body from being read as the end of the name.
    /// </summary>
    private static void RecordWhisperSender(string message)
    {
        if (!message.StartsWith('['))
            return;

        var end = message.IndexOf(']');

        if ((end <= 1) || !message.AsSpan(end)
                                  .StartsWith("]: "))
            return;

        WorldState.Chat.RecordWhisperFrom(message[1..end]);
    }

    private void HandleServerMessage(ServerMessageArgs args)
    {
        //server UTC timestamps become local time, unless tags index into the original text
        var message = WithLocalTime(args.Message, args.Tags);

        switch (args.ServerMessageType)
        {
            case ServerMessageType.Whisper:
                RecordWhisperSender(args.Message);

                //single render boundary: the body transforms once here; the chat log, orange bar, and
                //message pane share the display string, with the original plus tags beside it
                var whisper = ChatDisplayBuilder.BuildWhisper(
                    message,
                    args.Tags,
                    WorldState.UserOptions.ChatFilterMode,
                    FantasyDictionary.Default);

                WorldState.Chat.AddMessage(whisper.DisplayText, TextColors.Whisper, whisper.OriginalText, whisper.Tags, args.LineId);
                RememberTranslatableLine(args.LineId, TranslatedLineKind.Whisper, 0, false, TextColors.Whisper, message);
                WorldState.Chat.AddOrangeBarMessage(whisper.DisplayText, TextColors.Whisper);
                SystemMessagePane.AddMessage(whisper.DisplayText, TextColors.Whisper);
                if (TcgMatchOpen && !TcgMatchWindow!.Spectating)
                    TcgMatchWindow!.AcceptWhisper(args.Message, whisper.DisplayText);

                break;

            case ServerMessageType.GroupChat:
                //single render boundary (group and guild share the [!channel] Name: wire shape)
                var groupChat = ChatDisplayBuilder.BuildGroupChat(
                    message,
                    args.Tags,
                    WorldState.UserOptions.ChatFilterMode,
                    FantasyDictionary.Default);

                WorldState.Chat.AddMessage(groupChat.DisplayText, TextColors.GroupChat, groupChat.OriginalText, groupChat.Tags, args.LineId);
                RememberTranslatableLine(args.LineId, TranslatedLineKind.Group, 0, false, TextColors.GroupChat, message);
                WorldState.Chat.AddOrangeBarMessage(groupChat.DisplayText, TextColors.GroupChat);
                SystemMessagePane.AddMessage(groupChat.DisplayText, TextColors.GroupChat);

                break;

            case ServerMessageType.GuildChat:
                //single render boundary (group and guild share the [!channel] Name: wire shape)
                var guildChat = ChatDisplayBuilder.BuildGroupChat(
                    message,
                    args.Tags,
                    WorldState.UserOptions.ChatFilterMode,
                    FantasyDictionary.Default);

                WorldState.Chat.AddMessage(guildChat.DisplayText, TextColors.GuildChat, guildChat.OriginalText, guildChat.Tags, args.LineId);
                RememberTranslatableLine(args.LineId, TranslatedLineKind.Guild, 0, false, TextColors.GuildChat, message);
                WorldState.Chat.AddOrangeBarMessage(guildChat.DisplayText, TextColors.GuildChat);
                SystemMessagePane.AddMessage(guildChat.DisplayText, TextColors.GuildChat);

                break;

            case ServerMessageType.ActiveMessage:
                WorldState.Chat.AddOrangeBarMessage(message);
                SystemMessagePane.AddMessage(message);

                break;

            case ServerMessageType.OrangeBar1
                 or ServerMessageType.OrangeBar2
                 or ServerMessageType.OrangeBar3
                 or ServerMessageType.AdminMessage
                 or ServerMessageType.OrangeBar5:
                //channel messages (world shout) put a tagged copy of the chat log line here; it filters the same way
                var orangeBar = ChatDisplayBuilder.BuildOrangeBar(
                    message,
                    args.Tags,
                    WorldState.UserOptions.ChatFilterMode,
                    FantasyDictionary.Default);

                WorldState.Chat.AddOrangeBarMessage(orangeBar.DisplayText);

                break;

            case ServerMessageType.PersistentMessage:
                UpdateHuds(HudOps.ShowPersistentMessage, message);

                break;

            case ServerMessageType.ScrollWindow:
                TextPopup.Show(message);

                break;

            case ServerMessageType.NonScrollWindow:
                TextPopup.Show(message, PopupStyle.NonScroll);

                break;

            case ServerMessageType.WoodenBoard:
                TextPopup.Show(message, PopupStyle.Wooden);

                break;

            case ServerMessageType.ClosePopup:
                TextPopup.Hide();

                break;

            default:
                WorldState.Chat.AddOrangeBarMessage(message);

                break;
        }
    }

    //--- user options ---

    /// <summary>
    ///     Applies the server's user options. Each option is read explicitly and pushed into the F4 model.
    /// </summary>
    private void HandleUserOptions(UserOptionsArgs args)
    {
        var userOptions = WorldState.UserOptions;

        userOptions.Apply(SettingKey.ShowBodyAnimations, args.ShowBodyAnimations);
        userOptions.Apply(SettingKey.ListenToHitSounds, args.ListenToHitSounds);
        userOptions.Apply(SettingKey.PriorityAnimations, args.PriorityAnimations);
        userOptions.Apply(SettingKey.LockGear, args.LockGear);
        userOptions.Apply(SettingKey.WhisperSound, args.WhisperSound);
        userOptions.Apply(SettingKey.AllowExchanges, args.AllowExchange);
        userOptions.Apply(SettingKey.HideEnemyHealthBars, args.HideEnemyHealthBars);
        userOptions.Apply(SettingKey.ShowFriendlyNametags, args.AlwaysShowFriendlyNametags);
        userOptions.Apply(SettingKey.DamageNumbersMyOutputOnly, args.DamageNumbersMyOutputOnly);

        //stored chat filter prefs (Task 6): the mode lands in the choice cache the Task 5 call sites
        //read, so the login sync flips every chat path at once; out-of-range indices are ignored.
        userOptions.ApplyChoice(SettingKey.ChatFilterMode, args.ChatFilterMode);
        userOptions.Apply(SettingKey.HasConfiguredChatFilter, args.HasConfiguredChatFilter);

        //stored chat language pref: only mirrored here so the F4 dropdown shows the player's choice;
        //the server uses the value to pick which lines get translated.
        userOptions.ApplyChoice(SettingKey.ChatLanguage, args.ChatLanguage);

        //first-run dialog (Task 7): the login sync is the only source of the stored flag, so an
        //unconfigured player lands here once; the session guard (fresh WorldScreen per world entry)
        //keeps later echoes — e.g. from opening F4 Settings — from re-showing it.
        if (ChatFilterDialog.ShouldShowDialog(
                userOptions.Value(SettingKey.HasConfiguredChatFilter),
                ChatFilterDialogShown))
        {
            ChatFilterDialogShown = true;
            ChatFilterDialog.Show();
        }
    }

    //--- npc dialog / menu ---

    private void HandleDialogChanged()
    {
        var dialog = WorldState.NpcInteraction.CurrentDialog;

        if (dialog is null || (dialog.DialogType == DialogType.CloseDialog))
        {
            NpcSession.HideAll();

            return;
        }

        NpcSession.ShowDialog(dialog);
        RenderNpcSessionPortrait();
    }

    private void HandleMenuChanged()
    {
        var menu = WorldState.NpcInteraction.CurrentMenu;

        if (menu is null)
            return;

        NpcSession.ShowMenu(menu);
        RenderNpcSessionPortrait();
    }

    private void RenderNpcSessionPortrait()
    {
        //phase 1: try full-art illustration spf. The original DA client attempts this unconditionally for every
        //dialog/menu packet — the only gate is whether the NPC name matches an entry in the merged illustration
        //metadata (npci.tbl inside npcbase.dat + server-pushed NPCIllust metafile). IllustrationIndex picks which
        //filename variant to load when a name has multiple.
        if (!string.IsNullOrEmpty(NpcSession.NpcName))
        {
            var illustTexture = TryLoadNpcIllustration(NpcSession.NpcName, NpcSession.IllustrationIndex);

            if (illustTexture is not null)
            {
                NpcSession.SetPortrait(illustTexture, true);

                return;
            }
        }

        //phase 2: fall back to entity sprite portrait based on entitytype
        if (NpcSession.PortraitSpriteId == 0)
        {
            NpcSession.SetPortrait(null, false);

            return;
        }

        switch (NpcSession.SourceEntityType)
        {
            case EntityType.Creature:
            {
                var frame = RenderCreaturePortrait(NpcSession.PortraitSpriteId);

                if (frame is not null)
                    NpcSession.SetPortrait(frame.Value);
                else
                    NpcSession.SetPortrait(null, false);

                break;
            }

            case EntityType.Item:
            {
                var sprite = Game.ItemRenderer.GetSprite(NpcSession.PortraitSpriteId, (byte)NpcSession.PortraitColor);
                NpcSession.SetPortrait(sprite?.Texture, false);

                break;
            }

            default:
                NpcSession.SetPortrait(null, false);

                break;
        }
    }

    /// <summary>
    ///     Attempts to load a full-art NPC illustration SPF from <c>npcbase.dat</c>. Looks up <paramref name="npcName" />
    ///     in the merged illustration metadata (npci.tbl + server NPCIllust metafile) and picks the filename at
    ///     <paramref name="variant" />. Returns null if the NPC has no entries, the variant index is out of range,
    ///     or the SPF file is missing.
    /// </summary>
    private static Texture2D? TryLoadNpcIllustration(string npcName, byte variant)
    {
        var illustMeta = DataContext.MetaFiles.GetNpcIllustrationMetadata();

        if (!illustMeta.Illustrations.TryGetValue(npcName, out var filenames) || (filenames.Count == 0))
            return null;

        if (variant >= filenames.Count)
            return null;

        var spfFileName = filenames[variant];

        if (!DatArchives.Npcbase.TryGetValue(spfFileName, out var entry))
            return null;

        var spf = SpfFile.FromEntry(entry);

        if (spf.Count == 0)
            return null;

        using var image = SpfRenderer.RenderFrame(spf, 0);

        return TextureConverter.ToTexture2D(image);
    }

    private SpriteFrame? RenderCreaturePortrait(ushort spriteId)
    {
        var info = Game.CreatureRenderer.GetAnimInfo(spriteId);

        if (info is null)
            return null;

        (var frameIndex, _) = AnimationSystem.GetCreatureIdleFrame(info.Value, Direction.Down);

        return Game.CreatureRenderer.GetFrame(spriteId, frameIndex);
    }

    private void HandleRefreshResponse()
        =>

            //server acknowledged the refresh request — re-center camera
            FollowPlayerCamera();

    //--- exchange ---

    private void HandleExchangeAmountRequested(byte fromSlot)
    {
        var stackCount = WorldState.Inventory.GetSlot(fromSlot).Count;
        var wholeStack = ExchangeWholeStackSlot == fromSlot;
        ExchangeWholeStackSlot = null;

        if (wholeStack)
        {
            Game.Connection.SendExchangeInteraction(
                ExchangeRequestType.AddStackableItem,
                Exchange.OtherUserId,
                fromSlot,
                (ushort)Math.Min(stackCount, ushort.MaxValue));

            return;
        }

        ItemAmount.X = Exchange.X + (Exchange.Width - ItemAmount.Width) / 2;
        ItemAmount.Y = Exchange.Y + (Exchange.Height - ItemAmount.Height) / 2;
        ItemAmount.ShowFor(ItemAmountPurpose.Exchange, fromSlot, stackCount);

        //surface the slot's hover description (e.g. "Apple[ 10 ]") in the HUD bar while the popup
        //is open — matches retail behavior of pinning the operated-on item's tooltip text.
        WorldHud.SetDescription(WorldState.Inventory.GetSlot(fromSlot).Name);
    }

    private void HandleExchangeClosed(string? message)
    {
        //closing the exchange also drops any portrait tooltip a hovered row left up.
        HidePortraitTooltip();

        if (!string.IsNullOrEmpty(message))
            ExchangeResultPopup.Show(message);
    }

    //--- board / mail ---

    private void HandleBoardResponse(string message, bool success)
    {
        if (success)
            PendingBoardSuccessAction?.Invoke();

        PendingBoardSuccessAction = null;
        BoardResponsePopup.Show(message);
    }

    private void HandleRedirectReceived(RedirectInfo _) => RedirectInProgress = true;

    private void HandleBoardListReceived()
    {
        var boards = WorldState.Board.AvailableBoards;

        if (boards is null or { Count: 0 })
            return;

        WorldState.Board.OpenSession();

        BoardList.ShowBoards(
            boards.Select(b => (b.BoardId, b.Name))
                  .ToList());
    }

    private void HandleBoardPostListChanged()
    {
        var board = WorldState.Board;
        var posts = board.Posts.ToList();

        //ensure session is open — server can send board data directly (e.g. tile click) without going through BoardList
        if (!board.IsSessionOpen)
            board.OpenSession();

        if (board.IsPublicBoard)
        {
            if (LoadingMoreBoardPosts && ArticleList.Visible && (ArticleList.BoardId == board.BoardId))
                ArticleList.AppendEntries(posts);
            else
            {
                HideAllBoardControls();
                ArticleList.ShowArticles(board.BoardId, posts);
                ArticleList.SetHighlightEnabled(IsGameMaster);
            }
        } else
        {
            if (LoadingMoreBoardPosts && MailList.Visible && (MailList.BoardId == board.BoardId))
                MailList.AppendEntries(posts);
            else
            {
                HideAllBoardControls();
                MailList.ShowMailList(board.BoardId, posts);
            }
        }

        LoadingMoreBoardPosts = false;
    }

    private void HandleBoardPostViewed()
    {
        var post = WorldState.Board.CurrentPost;

        if (post is not { } p)
            return;

        var board = WorldState.Board;

        //ensure session is open — server can send a post directly without going through BoardList
        if (!board.IsSessionOpen)
            board.OpenSession();

        HideAllBoardControls();

        if (board.IsPublicBoard)
        {
            ArticleRead.BoardId = board.BoardId;

            ArticleRead.ShowArticle(
                p.PostId,
                p.Author,
                p.MonthOfYear,
                p.DayOfMonth,
                p.Subject,
                p.Message,
                board.EnablePrevButton);
        } else
        {
            MailRead.BoardId = board.BoardId;

            MailRead.ShowMail(
                p.PostId,
                p.Author,
                p.MonthOfYear,
                p.DayOfMonth,
                p.Subject,
                p.Message,
                board.EnablePrevButton);
        }
    }

    //--- group ---

    private void HandleGroupInviteReceived()
    {
        var invite = WorldState.GroupInvite.Current;

        if (invite is null)
            return;

        var sourceName = invite.SourceName;

        switch (invite.ServerGroupSwitch)
        {
            case ServerGroupSwitch.Invite:
                OfferGroupInvite($"{sourceName} invites you to join a group.", sourceName);

                break;

            case ServerGroupSwitch.RaidInvite:
                OfferGroupInvite($"{sourceName} invites your group to a raid.", sourceName);

                break;

            case ServerGroupSwitch.RequestToJoin:
            {
                // Retail behavior: the leader's client silently auto-forwards as TryInvite
                // with no UI prompt. Ref: docs/research/group-ui-original-re.md §5.1 / §7.1
                // (verified round-2). The orange-bar notice is a QoL addition retail omits.
                WorldState.Chat.AddOrangeBarMessage($"{sourceName} wants to join your group.");
                Game.Connection.SendGroupInvite(ClientGroupSwitch.TryInvite, sourceName);

                break;
            }

            case ServerGroupSwitch.ShowGroupBox:
            {
                if (invite.GroupBoxInfo is not { } groupBoxInfo)
                {
                    WorldState.Chat.AddOrangeBarMessage($"{sourceName} has an open group box.");

                    break;
                }

                if (sourceName.EqualsI(WorldState.PlayerName))
                {
                    WorldState.Group.MarkGroupBoxActive();
                    GroupPanel.ShowRecruitOwnerEdit(groupBoxInfo);
                } else
                    GroupBoxViewer.ShowAsViewer(sourceName, groupBoxInfo);

                break;
            }
        }
    }

    /// <summary>
    ///     Posts an invite to the orange bar, then accepts it straight away or asks with a popup, as
    ///     <see cref="ClientSettings.AutoAcceptGroupInvites" /> says.
    /// </summary>
    private void OfferGroupInvite(string message, string sourceName)
    {
        WorldState.Chat.AddOrangeBarMessage(message);

        if (ClientSettings.AutoAcceptGroupInvites)
        {
            Game.Connection.SendGroupInvite(ClientGroupSwitch.AcceptInvite, sourceName);

            return;
        }

        ShowGroupInvitePopup(message, sourceName);
    }

    private void ShowGroupInvitePopup(string message, string sourceName)
    {
        if (Root is null)
            return;

        var popup = new OkPopupMessageControl(true)
        {
            Name = "GroupInvitePopup"
        };
        Root.AddChild(popup);

        popup.OnOk += () =>
        {
            Game.Connection.SendGroupInvite(ClientGroupSwitch.AcceptInvite, sourceName);
            CloseGroupInvitePopups();
        };

        popup.OnCancel += CloseGroupInvitePopups;

        popup.Show(message);
    }

    //answering one invite closes every invite still open; each is hidden first so none stays on the input
    //dispatcher's control stack after RemoveChild disposes it
    private void CloseGroupInvitePopups()
    {
        if (Root is null)
            return;

        foreach (var child in Root.Children)
            if (child is OkPopupMessageControl { Name: "GroupInvitePopup" } invite)
                invite.Hide();

        Root.RemoveChild("GroupInvitePopup");
    }

    //--- profiles ---

    //the server ignores the picture in the login reply, so the portrait is always sent empty
    private void HandleEditableProfileRequest() => Game.Connection.SendEditableProfile([], StatusBook.GetProfileText());

    private void HandlePortraitPicture(PortraitPictureArgs args)
    {
        //fetched pictures are for tooltips and never touch the book
        if (!args.IsOwn)
        {
            PortraitTransfers().OnPicture(args);

            return;
        }

        StatusBook.SetPortrait(args.Picture);
        OwnPortraitPicture = args.Picture;
        PortraitWindow?.OnOwnPicture(args.Hash, args.Picture);
    }

    private void HandlePaintedPortraitDisplay(PaintedPortraitDisplayArgs args) => OpenPaintedPortraitWindow(args);

    /// <summary>
    ///     The commission board. Opens the window when hidden; a display for an open window refreshes its tabs in place,
    ///     keeping the tab and the selected row by Id.
    /// </summary>
    private void HandleCommissionBoardDisplay(CommissionBoardDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        CommissionBoardWindow ??= BuildCommissionBoardWindow();
        WorldState.CommissionBoard.Apply(args, WorldState.PlayerName);

        if (CommissionBoardWindow.Visible)
            return;

        BringToFront(CommissionBoardWindow);
        CommissionBoardWindow.Open();
    }

    private void HandlePortraitPictureReady(string hash)
    {
        if ((TooltipPortraitHash == hash)
            && ItemTooltip.Visible
            && PortraitTransfers().TryGetTexture(hash, out var texture))
        {
            //the pre-load Hold was a no-op while the texture was missing, so hold the arrived texture now;
            //HidePortraitTooltip releases exactly this one hold
            PortraitTransfers().Hold(hash);
            ItemTooltip.SetPortraitTexture(texture);
        }
    }

    private void HandlePortraitPictureFailed(string hash)
    {
        if ((TooltipPortraitHash == hash) && ItemTooltip.Visible)
            ItemTooltip.SetPortraitTexture(null);
    }

    private static string LoadProfileText()
    {
        if (!DataContext.LocalPlayerSettings.IsInitialized)
            return string.Empty;

        var profilePath = DataContext.LocalPlayerSettings.GetFilePath("profile.txt");

        return File.Exists(profilePath) ? File.ReadAllText(profilePath) : string.Empty;
    }

    private static void SaveProfileText(string text)
    {
        if (!DataContext.LocalPlayerSettings.IsInitialized)
            return;

        var profilePath = DataContext.LocalPlayerSettings.GetFilePath("profile.txt");
        File.WriteAllText(profilePath, text);
    }

    private void HandleSelfProfile(SelfProfileArgs args)
    {
        WorldState.IsMaster = args.EnableMasterQuestMetaData;

        //the server sends the advanced class name as the display class once the player has advanced; anything else
        //(a base class name, or "Master") leaves them on AdvClass.None.
        //the server sends the class the way a player reads it, so "Plague Doctor" has a space the enum name does
        //not. Strip it before parsing -- without this the parse fails and the whole ability metadata panel is
        //empty for one class.
        WorldState.AdvClass = Enum.TryParse<AdvClass>(args.DisplayClass?.Replace(" ", string.Empty), true, out var advClass)
            ? advClass
            : AdvClass.None;


        //nation emblem and text
        StatusBook.SetNation((byte)args.Nation);

        //social status display
        var status = SocialStatusPicker.CurrentStatus;
        StatusBook.SetEmoticonState((byte)status, UiComponentRepository.GetSocialStatusName(status));

        //populate and show the status book
        StatusBook.SetPlayerInfo(
            WorldHud.PlayerName,
            args.DisplayClass ?? string.Empty,
            args.GuildName ?? string.Empty,
            args.GuildRank ?? string.Empty,
            args.Title ?? string.Empty);

        //the title list + active index now ride along on the self-profile packet itself
        StatusBook.SetTitles(args.ActiveTitleIndex, args.Titles);

        //equipment visibility dots — echo the server's authoritative hidden-slot flags onto the dots
        StatusBook.ApplyHiddenEquipmentFlags(args.HiddenEquipmentFlags);

        //legend marks
        var marks = args.LegendMarks
                        .Select(m => new LegendMarkEntry(
                            m.Text,
                            MapMarkColor(m.Color),
                            (byte)m.Icon,
                            m.Key))
                        .ToList();

        StatusBook.SetLegendMarks(marks);

        //ability metadata (skills/spells from sclass file) — the file carries every advanced class that branches off this
        //base class, so trim it to the player's own before it reaches the tab
        var abilityMetadata = DataContext.MetaFiles.GetAbilityMetadata((byte)args.BaseClass);

        if (abilityMetadata is not null)
            StatusBook.SetAbilityMetadata(abilityMetadata.ForAdvClass(WorldState.AdvClass));
        else
            StatusBook.ClearSkills();

        //event metadata (quests from sevent files)
        var eventMetadata = DataContext.MetaFiles.GetEventMetadata();

        if (eventMetadata.Count > 0)
        {
            //build a set of completed event ids from legend marks for o(1) lookup
            var completedEventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var mark in args.LegendMarks)
                completedEventIds.Add(mark.Key);

            StatusBook.SetEvents(
                eventMetadata,
                completedEventIds,
                args.BaseClass,
                args.EnableMasterQuestMetaData);
        } else
            StatusBook.ClearEvents();

        //the class is only knowable once this packet arrives, so refresh the launcher card now that
        //AdvClass is set. Cheap and best-effort; no-ops when the card env vars are absent.
        Chaos.Client.Systems.AvatarCapture.SaveStats();

        //family info
        StatusBook.SetFamilyInfo(args.SpouseName ?? string.Empty);
        LoadPlayerFamilyList();

        //paperdoll — render the player's full aisling at south-facing idle
        var playerEntity = WorldState.GetPlayerEntity();

        if (playerEntity?.Appearance is { } appearance)
            StatusBook.SetPaperdoll(Game.AislingRenderer, in appearance);

        //group open state — server is source of truth, sync all ui
        StatusBook.SetGroupOpen(args.GroupOpen);
        WorldState.UserOptions.Apply(SettingKey.AllowGroupInvites, args.GroupOpen);
        WorldHud.SetGroupOpen(args.GroupOpen);

        //group members — parse groupstring into state, ui subscribes via event
        if (!string.IsNullOrEmpty(args.GroupString))
        {
            if (args.GroupString.StartsWithI(GROUP_MEMBERS_PREFIX))
                WorldState.Group.ParseAndSet(args.GroupString);
            else if (args.GroupString.StartsWithI(SPOUSE_PREFIX))
            {
                var spouseName = args.GroupString[SPOUSE_PREFIX.Length..]
                                     .Trim();
                StatusBook.SetFamilyInfo(spouseName);
                WorldState.Group.Clear();
            } else
                WorldState.Group.Clear();
        } else
            WorldState.Group.Clear();

        if (GroupHighlightRequested)
        {
            GroupHighlightRequested = false;
            ApplyGroupHighlight();
        } else if (SelfProfileRequested)
        {
            SelfProfileRequested = false;
            ShowStatusBook(SelfProfileRequestedTab);
            SelfProfileRequestedTab = StatusBookTab.Equipment;
        }
    }

    private void ClearGroupHighlight()
    {
        GroupHighlightLatched = false;
        GroupHighlightedIds.Clear();
        Game.AislingRenderer.ClearGroupTintCache();
        Game.CreatureRenderer.ClearTintCaches();
    }

    private void ApplyGroupHighlight()
    {
        GroupHighlightedIds.Clear();
        Game.AislingRenderer.ClearGroupTintCache();
        Game.CreatureRenderer.ClearTintCaches();

        foreach (var id in FindVisibleGroupMemberIds())
            GroupHighlightedIds.Add(id);

        if (GroupHighlightedIds.Count > 0)
            GroupHighlightTimer = 1000f;
    }

    /// <summary>
    ///     Re-picks the highlighted members while the highlight is latched on, so members who walk into view, or join,
    ///     light up. The tint caches are only dropped when the set changes.
    /// </summary>
    private void RefreshLatchedGroupHighlight()
    {
        var ids = FindVisibleGroupMemberIds();

        if (GroupHighlightedIds.SetEquals(ids))
            return;

        GroupHighlightedIds.Clear();
        GroupHighlightedIds.UnionWith(ids);
        Game.AislingRenderer.ClearGroupTintCache();
        Game.CreatureRenderer.ClearTintCaches();
    }

    private HashSet<uint> FindVisibleGroupMemberIds()
    {
        var ids = new HashSet<uint>();

        var memberSet = new HashSet<string>(WorldState.Group.Members, StringComparer.OrdinalIgnoreCase);

        //the self profile's group list is cut at 255 bytes, so a full raid's later names are only in the group state
        if (WorldState.GroupMembers.IsRaid)
            foreach (var member in WorldState.GroupMembers.Members)
                memberSet.Add(member.Name);

        if (memberSet.Count == 0)
            return ids;

        foreach (var entity in WorldState.GetSortedEntities())
        {
            if (entity.Type != ClientEntityType.Aisling)
                continue;

            if ((entity.Id != WorldState.PlayerEntityId) && !string.IsNullOrEmpty(entity.Name) && memberSet.Contains(entity.Name))
                ids.Add(entity.Id);
        }

        return ids;
    }

    private void ShowStatusBook(StatusBookTab tab = StatusBookTab.Equipment)
    {
        StatusBook.RefreshEquipment();

        if (WorldState.Attributes.Current is { } attrs)
            StatusBook.UpdateEquipmentStats(
                attrs.Str,
                attrs.Int,
                attrs.Wis,
                attrs.Con,
                attrs.Dex,
                attrs.Ac);

        StatusBook.SwitchTab(tab);
        StatusBook.Show();
    }

    private void HandleOtherProfile(OtherProfileArgs args)
    {
        var marks = args.LegendMarks
                        .Select(m => new LegendMarkEntry(
                            m.Text,
                            MapMarkColor(m.Color),
                            (byte)m.Icon,
                            m.Key))
                        .ToList();

        OtherProfile.Show(args, marks, Game.AislingRenderer);
    }

    //--- animations / effects / sound ---

    private void HandleBodyAnimation(BodyAnimationArgs args)
    {
        var entity = WorldState.GetEntity(args.SourceId);

        if (entity is null)
            return;

        //emotes are body animations — ignore if any body anim or emote overlay is already playing
        if ((entity.AnimState == EntityAnimState.BodyAnim)
            || (entity.ActiveEmoteFrame >= 0)
            || entity.IsPlayingCustomEmote)
            return;

        //the client-side emotes ride on body-animation bytes the enum leaves unused, so they never reach
        //ResolveBodyAnimParams or the emot01 overlay below — they are drawn on top of the composite instead.
        //Always return here on a custom-emote byte so it never falls through to the creature attack
        //animation below. Only start it when the entity is NOT rendered as a creature sprite — creature
        //sprites have no aisling head to draw it on.
        var isOwn = args.SourceId == WorldState.PlayerEntityId;

        if (CustomEmoteRegistry.TryGet((int)args.BodyAnimation, out var customEmote))
        {
            if (!entity.IsRenderedAsCreatureSprite)
                entity.StartCustomEmote(customEmote, isOwn && OwnAnimationHidden(isEmote: true));

            return;
        }

        //your own hidden attack still runs for its full length so you can't move sooner than with it showing; the
        //server sends your own animations even while "Show body animations" is off for the same reason
        var hidden = isOwn && OwnAnimationHidden(DataUtilities.IsEmote(args.BodyAnimation));

        //creature sprites (native creatures AND aislings in monster form) use their mpf attack frame
        //counts; normal aislings use epf suffix-based frame counts. Gate on IsRenderedAsCreatureSprite
        //so a form attacks with its mns frames, not garbled aisling-epf frames. Emotes are no-ops here
        //(StartCreatureBodyAnimation skips them — a form has no aisling face overlay to composite).
        if (entity.IsRenderedAsCreatureSprite)
        {
            var animInfo = Game.CreatureRenderer.GetAnimInfo(entity.SpriteId);

            if (animInfo is { } info)
                AnimationSystem.StartCreatureBodyAnimation(
                    entity,
                    args.BodyAnimation,
                    args.AnimationSpeed,
                    in info,
                    hidden);
        } else
        {
            (_, var framesPerDir, _, _) = AnimationSystem.ResolveBodyAnimParams(args.BodyAnimation);

            if (framesPerDir > 0)
            {
                if (entity.Appearance.HasValue && !Game.AislingRenderer.HasArmorAnimation(entity.Appearance.Value, args.BodyAnimation))
                    return;

                AnimationSystem.StartBodyAnimation(entity, args.BodyAnimation, args.AnimationSpeed, hidden);
            } else if (DataUtilities.IsEmote(args.BodyAnimation))
            {
                //emote overlay — face/bubble icon composited into the aisling sprite
                (var startFrame, var frameCount, var durationMs) = AnimationSystem.ResolveEmoteFrames(args.BodyAnimation);

                if (startFrame >= 0)
                    entity.StartEmoteOverlay(startFrame, frameCount, durationMs, hidden);
            }
        }
    }

    /// <summary>Whether one of the player's own body animations or emotes is hidden by their F4 settings right now.</summary>
    private static bool OwnAnimationHidden(bool isEmote)
        => AnimationSystem.HidesOwnAnimation(
            isEmote,
            ClientSettings.HideOwnBodyAnimations,
            WorldState.UserOptions.Value(SettingKey.ShowBodyAnimations));

    //TargetAnimation values in [PROJECTILE_ANIMATION_BASE, PROJECTILE_ANIMATION_MAX_EXCLUSIVE) are MEFC projectiles;
    //the meffect id is recovered by subtracting the base.
    private const int PROJECTILE_ANIMATION_BASE = 10000;
    private const int PROJECTILE_ANIMATION_MAX_EXCLUSIVE = 12000;

    private void HandleAnimation(AnimationArgs args)
    {
        if (args is { SourceId: > 0, TargetId: > 0, TargetAnimation: >= PROJECTILE_ANIMATION_BASE and < PROJECTILE_ANIMATION_MAX_EXCLUSIVE })
        {
            var meffectId = args.TargetAnimation - PROJECTILE_ANIMATION_BASE;
            CreateProjectile(meffectId, args.SourceId.Value, args.TargetId.Value);

            if (args is { SourceAnimation: > 0 })
                CreateEffect(args.SourceAnimation, args.AnimationSpeed, args.SourceId.Value);

            return;
        }

        //ground-targeted effect
        if (args is { TargetPoint: not null, TargetAnimation: > 0 })
            CreateEffect(
                args.TargetAnimation,
                args.AnimationSpeed,
                targetTileX: args.TargetPoint.Value.X,
                targetTileY: args.TargetPoint.Value.Y,
                isBackground: args.IsBackground);

        //entity-targeted effect on target
        if (args is { TargetId: > 0, TargetAnimation: > 0 })
            CreateEffect(
                args.TargetAnimation,
                args.AnimationSpeed,
                args.TargetId.Value,
                isBackground: args.IsBackground);

        //source-side effect (caster visual)
        if (args is { SourceId: > 0, SourceAnimation: > 0 })
            CreateEffect(args.SourceAnimation, args.AnimationSpeed, args.SourceId.Value);
    }

    private void CreateProjectile(int meffectId, uint sourceEntityId, uint targetEntityId)
    {
        var record = DataContext.Effects.GetMeffectRecord(meffectId);

        if (record is null)
            return;

        var sourceEntity = WorldState.GetEntity(sourceEntityId);
        var targetEntity = WorldState.GetEntity(targetEntityId);

        if (sourceEntity is null || targetEntity is null)
            return;

        if (MapFile is null)
            return;

        var sourceWorld = Camera.TileToWorld(sourceEntity.TileX, sourceEntity.TileY, MapFile.Height);
        var targetWorld = Camera.TileToWorld(targetEntity.TileX, targetEntity.TileY, MapFile.Height);

        var srcX = sourceWorld.X + DaLibConstants.HALF_TILE_WIDTH;
        var srcY = sourceWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;
        var tgtX = targetWorld.X + DaLibConstants.HALF_TILE_WIDTH;
        var tgtY = targetWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;

        var dx = tgtX - srcX;
        var dy = tgtY - srcY;
        var distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance < 1f)
            return;

        //direction matches server's DirectionalRelationTo (tile space): Up=0, Right=1, Down=2, Left=3
        var direction = GetProjectileDirection(
            targetEntity.TileX - sourceEntity.TileX,
            targetEntity.TileY - sourceEntity.TileY);

        WorldState.ActiveProjectiles.Add(
            new Projectile
            {
                TargetEntityId = targetEntityId,
                MeffectId = meffectId,
                CurrentX = srcX,
                CurrentY = srcY,
                LastKnownTargetX = tgtX,
                LastKnownTargetY = tgtY,
                Step = record.Step,
                StepDelayMs = record.StepDelay,
                InitialDistance = distance,
                ArcRatioV = record.ArcRatioV,
                ArcRatioH = record.ArcRatioH,
                FramesPerDirection = record.FramesPerDirection,
                Direction = direction
            });
    }

    private void CreateEffect(
        int effectId,
        ushort animationSpeed,
        uint? targetEntityId = null,
        int? targetTileX = null,
        int? targetTileY = null,
        bool isBackground = false)
    {
        var info = Game.EffectRenderer.GetEffectInfo(effectId);

        if (info is null)
            return;

        (var frameCount, var fileIntervalMs, var isEfa, var blendMode) = info.Value;

        //efa effects use the interval from the file; epf effects use the packet's animation speed
        float frameIntervalMs = isEfa
            ? fileIntervalMs > 0 ? fileIntervalMs : 50
            : animationSpeed > 0
                ? animationSpeed
                : 50;

        //limit the number of effect animations playing on a single entity at once
        if (targetEntityId.HasValue)
        {
            //re-casting an effect that's still playing restarts it rather than stacking an
            //identical, pixel-for-pixel overlapping copy that would waste a slot.
            WorldState.ActiveEffects.RemoveAll(e => (e.TargetEntityId == targetEntityId) && (e.EffectId == effectId));

            var maxPerEntity = ClientSettings.MaxEffectAnimationsPerEntity;

            //a cap of 0 means no effect animations play on entities at all — drop the incoming one.
            if (maxPerEntity <= 0)
                return;

            //evict the oldest entity-attached effect(s) until there's room for the incoming one.
            //ActiveEffects is append-ordered (completed effects are removed in place), so the
            //lowest matching index is the oldest still-living effect — FIFO eviction.
            while (WorldState.ActiveEffects.Count(e => e.TargetEntityId == targetEntityId) >= maxPerEntity)
            {
                var oldestIndex = WorldState.ActiveEffects.FindIndex(e => e.TargetEntityId == targetEntityId);

                if (oldestIndex < 0)
                    break;

                WorldState.ActiveEffects.RemoveAt(oldestIndex);
            }
        }

        WorldState.ActiveEffects.Add(
            new Animation
            {
                EffectId = effectId,
                TargetEntityId = targetEntityId,
                TileX = targetTileX,
                TileY = targetTileY,
                FrameCount = frameCount,
                FrameIntervalMs = frameIntervalMs,
                BlendMode = blendMode,
                IsBackground = isBackground
            });
    }

    private void HandleSound(SoundArgs args)
    {
        if (args.IsMusic)
            Game.SoundSystem.PlayMusic(args.Sound);
        else
            Game.SoundSystem.PlaySound(args.Sound);
    }

    //--- world / map / doors ---

    private void HandleWorldMap(WorldMapArgs args) => WorldMap.Show(args);

    private void HandleDoor(DoorArgs args)
    {
        if (MapFile is null)
            return;

        foreach (var door in args.Doors)
        {
            if ((door.X < 0) || (door.X >= MapFile.Width) || (door.Y < 0) || (door.Y >= MapFile.Height))
                continue;

            var tile = MapFile.Tiles[door.X, door.Y];

            if (door.Closed)
            {
                //restore closed tile: find the open tile currently set and swap it back
                var closedLeft = DoorTable.GetClosedTileId(tile.LeftForeground);
                var closedRight = DoorTable.GetClosedTileId(tile.RightForeground);

                if (closedLeft.HasValue)
                    tile.LeftForeground = closedLeft.Value;

                if (closedRight.HasValue)
                    tile.RightForeground = closedRight.Value;
            } else
            {
                //open door: find the closed tile and swap to open
                var openLeft = DoorTable.GetOpenTileId(tile.LeftForeground);
                var openRight = DoorTable.GetOpenTileId(tile.RightForeground);

                if (openLeft.HasValue)
                    tile.LeftForeground = openLeft.Value;

                if (openRight.HasValue)
                    tile.RightForeground = openRight.Value;
            }
        }
    }

    private void HandleMapChangePending()
    {
        MapPreloaded = false;

        //stale pathfinder grid has the OLD map's dimensions; right-click guards key on MapPathfinder being null, so
        //null it here to prevent FindPath from indexing PathNodes[,] out of bounds before FinalizeMapLoad rebuilds it
        MapPathfinder = null;
        MapDoorTiles = [];
        QueuedWalkDirection = null;
        Pathfinding.Clear();
        TownMapControl.Hide();

        //a true map transition is the one place we discard outstanding walk acks: any walks that were
        //in flight on the old map will not be ack'd on the new one. (the same-map F5 refresh path,
        //which also runs ClearTransientState, must NOT touch this counter — its acks are still in flight.)
        InFlightWalkAcks = 0;
    }

    //--- health / effects / light ---

    private void HandleEffect(EffectArgs args) => UpdateHuds(HudOps.SetEffect, args);

    private void HandleSetEntityBarrier(SetEntityBarrierArgs args)
    {
        //entity may not be tracked yet (or at all) — the hud path below does not depend on the entity table
        WorldState.GetEntity(args.Id)
                  ?.Barrier.Set(args.Remaining);

        if (args.Id == Game.Connection.AislingId)
            UpdateHuds(HudOps.SetBarrier, (int)args.Remaining);
    }

    private void HandleSetSongState(SetSongStateArgs args) => WorldState.Song.SetState(args.SongId, args.Harmony);

    /// <summary>
    ///     Plague doctor Ichor, 0-100, reported by the server. Server-authoritative -- the client only stores
    ///     and displays this value, it never computes Ichor itself. Drawn by <see cref="ClassResourceBar" /> in
    ///     <c>WorldScreen.Draw.cs</c>.
    /// </summary>
    private void HandleSetIchorState(SetIchorStateArgs args) => WorldState.ClassResource.SetIchor(args.Ichor);

    /// <summary>
    ///     Berserker Rage, 0-100, reported by the server. Same shape and the same strip as
    ///     <see cref="HandleSetIchorState" /> -- a separate opcode purely so the two class resources can be told
    ///     apart on display. Zero means the rage is spent and the strip hides.
    /// </summary>
    private void HandleSetRageState(SetRageStateArgs args) => WorldState.ClassResource.SetRage(args.Rage);

    /// <summary>
    ///     Warlord Valor, 0-100, reported by the server. Same shape and the same strip as
    ///     <see cref="HandleSetRageState" />, coloured gold. Zero means the valor is spent and the strip hides.
    /// </summary>
    private void HandleSetValorState(SetValorStateArgs args) => WorldState.ClassResource.SetValor(args.Valor);

    /// <summary>
    ///     Assassin Malice, 0-100, reported by the server. Same shape and the same strip as
    ///     <see cref="HandleSetRageState" />, coloured violet rather than red so the two kill-fed resources are
    ///     never mistaken for each other. Zero means the malice is spent and the strip hides.
    /// </summary>
    private void HandleSetMaliceState(SetMaliceStateArgs args) => WorldState.ClassResource.SetMalice(args.Malice);

    /// <summary>
    ///     Air remaining underwater, 0-100, reported by the server. Its own bar rather than a fifth colour on the
    ///     class strip, since both can be carried at once. Zero is displayed as an empty bar rather than hiding it --
    ///     that bar is the drowning warning. Leaving the zone is what clears it, in <c>HandleMapInfo</c>.
    /// </summary>
    private void HandleSetOxygenState(SetOxygenStateArgs args) => WorldState.Oxygen.Set(args.Oxygen);

    /// <summary>
    ///     The map's ambient-effect flags — the bits above the byte map info carries. Arrives right after every map
    ///     info. Snaps the effects in after a full map change, fades them on a same-map refresh (e.g. a live
    ///     <c>/mapFlag</c> toggle).
    /// </summary>
    private void HandleSetMapEffects(SetMapEffectsArgs args)
    {
        CurrentMapFlags = (CurrentMapFlags & ~AmbientEffects.EXTENDED_FLAGS)
                          | ((MapFlags)args.ExtendedFlags & AmbientEffects.EXTENDED_FLAGS);

        AmbientEffects.Apply(MapEffectFilter.Visible(CurrentMapFlags), SnapAmbientEffects);
        SnapAmbientEffects = false;
    }

    private static void HandleSetGroupState(SetGroupStateArgs args) => WorldState.GroupMembers.Set(args);

    private static void HandleSetGroupPositions(SetGroupPositionsArgs args) => WorldState.GroupPositions.Set(args);

    private void HandleSongCall(SongCallArgs args)
        => WorldState.Song.BeginCall(
            args.CallId,
            args.Note1,
            args.Note2,
            args.Note3,
            args.Note4,
            args.WindowMs);

    private void HandleHealthBar(HealthBarArgs args)
    {
        Overlays.AddOrResetHealthBar(args.SourceId, args.HealthPercent);

        //anchor needs the map height + creature renderer; MapFile is always set while in-world (HealthBar is a world packet)
        if ((args.Amount != 0) && MapFile is not null)
            Overlays.AddDamageNumber(
                args.SourceId,
                args.Amount,
                MapFile.Height,
                Game.CreatureRenderer);

        if (args.Sound.HasValue)
            Game.SoundSystem.PlaySound(args.Sound.Value);
    }

    private void HandleLightLevel(LightLevelArgs args) => DarknessRenderer.OnLightLevel(args.LightLevel);

    private void HandleMetaDataSyncComplete()
    {
        DarknessRenderer.ReloadMetadata();
        DarknessRenderer.ReapplyLightLevel();
        DataContext.MetaFiles.BuildItemIndex();
    }

    //--- notepad ---

    private void HandleDisplayReadonlyNotepad(DisplayReadonlyNotepadArgs args)
    {
        HidePortraitTooltip();

        Notepad.ShowReadonly(
            (byte)args.NotepadType,
            args.Width,
            args.Height,
            args.Message);
    }

    private void HandleDisplayEditableNotepad(DisplayEditableNotepadArgs args)
    {
        HidePortraitTooltip();

        Notepad.ShowEditable(
            args.Slot,
            (byte)args.NotepadType,
            args.Width,
            args.Height,
            args.Message);
    }

    //--- exit / state ---

    private void HandleExitResponse(ExitResponseArgs args)
    {
        //server confirmed exit — send the actual logout (isrequest=false triggers server-side redirect to login)
        if (args.ExitConfirmed)
            Game.Connection.RequestExit(false);
    }

    private void HandleStateChanged(ConnectionState oldState, ConnectionState newState)
    {
        //server redirected us back to login (e.g., after logout)
        //state transitions go world → connecting → login, so just check for login arrival
        if (newState == ConnectionState.Login)
        {
            RedirectInProgress = false;
            PendingLoginSwitch = true;

            return;
        }

        //unexpected disconnect — show reconnect prompt (skip if this is part of a redirect sequence)
        if ((newState == ConnectionState.Disconnected) && !RedirectInProgress)
            DisconnectPopup.Show("The connection has been dropped. Do you wish to log on again?");
    }

    //--- helpers ---

    private static Color MapMarkColor(MarkColor color)
    {
        if (color == MarkColor.Invisible)
            return Color.Transparent;

        return LegendColors.Get((int)color);
    }

    //--- market ---

    private void HandleMarketDisplay(MarketDisplayArgs args)
    {
        switch (args.Type)
        {
            case MarketDisplayType.Open:
                Market.Show();

                break;

            case MarketDisplayType.SearchResults:
            {
                var listings = new List<MarketListing>(args.Results?.Count ?? 0);

                foreach (var entry in args.Results ?? [])
                    listings.Add(MapResultEntry(entry));

                Market.SetResults(args.Page, args.TotalResults, listings);

                break;
            }

            case MarketDisplayType.SellSnapshot:
            {
                var sell = new List<MarketSellListing>(args.SellListings?.Count ?? 0);

                foreach (var entry in args.SellListings ?? [])
                    sell.Add(
                        new MarketSellListing
                        {
                            ListingId = entry.ListingId,
                            Sprite = entry.Sprite,
                            Color = entry.Color,
                            Name = entry.Name,
                            Quantity = entry.Quantity,
                            UnitPrice = entry.IsPriced ? entry.UnitPrice : null
                        });

                Market.SetSellListings(sell);
                Market.SetSellPendingPayout(args.PendingPayout);

                break;
            }

            case MarketDisplayType.SalesLog:
            {
                var log = new List<MarketSaleLog>(args.SalesLog?.Count ?? 0);

                foreach (var entry in args.SalesLog ?? [])
                    log.Add(
                        new MarketSaleLog(
                            entry.ItemName,
                            entry.BuyerName,
                            entry.Quantity,
                            entry.UnitPrice,
                            entry.TotalPrice,
                            new DateTime((long)entry.SoldAtTicks, DateTimeKind.Utc).ToLocalTime()));

                Market.SetSalesLog(log);

                break;
            }
        }
    }

    //--- bank ---

    private void HandleBankDisplay(BankDisplayArgs args)
    {
        switch (args.Type)
        {
            //the server's one unsolicited push: the player asked a banker to open the window.
            case BankDisplayType.Open:

                //an Open can retarget an already-visible window (personal → guild), which fires no Closed — any prompt
                //still up was opened against the previous bank and would withdraw from this one.
                HideBankPrompts();
                WorldState.Bank.SetCategories(args);
                Bank.Show();
                RequestOpenCategory();

                break;

            //the answer to a Search this client sent. It must never OPEN the window: a refresh still in flight when the
            //player closes it — or changes map, where Hide() is what keeps the shared guild bank on one map — would
            //otherwise pop it back open.
            case BankDisplayType.Categories:
                if (!Bank.Visible)
                    break;

                WorldState.Bank.SetCategories(args);
                RequestOpenCategory();

                break;

            //terminal state: an Items display must send nothing, or the refresh cycle never stops.
            case BankDisplayType.Items:
                if (!Bank.Visible)
                    break;

                WorldState.Bank.SetItems(args);

                break;

            //the server ended the session — walked out of range, or lost guild access. The window would otherwise stay
            //up, fully painted, silently ignoring every click.
            case BankDisplayType.Close:
                Bank.Hide();

                break;
        }
    }

    /// <summary>
    ///     Asks for the category the window is on — or the first one if that category is gone (or none was selected), so
    ///     the window is never left empty. This is the single send a new rail may trigger; the Items reply it produces
    ///     triggers nothing, which is what stops the refresh cycle.
    /// </summary>
    private void RequestOpenCategory()
    {
        //SetCategories has already cleared SelectedCategory if it vanished from the new rail
        var category = !string.IsNullOrEmpty(WorldState.Bank.SelectedCategory)
            ? WorldState.Bank.SelectedCategory
            : WorldState.Bank
                        .Categories
                        .FirstOrDefault();

        //an empty rail (empty bank, or a search that matched nothing) has no category to open, and nothing to ask for
        if (string.IsNullOrEmpty(category))
            return;

        WorldState.Bank.SelectCategory(category);
        Game.Connection.SendBankRequestCategory(category);
    }

    /// <summary>
    ///     Refreshes the bank after a mutation. The server pushes nothing on success, so the client re-drives its own view:
    ///     this Search rebuilds the rail, and HandleBankDisplay then re-requests the open category. TCP ordering means the
    ///     mutation is already applied server-side by the time this is handled.
    /// </summary>
    private void RefreshBank() => Game.Connection.SendBankSearch(Bank.Query);

    //--- slots ---

    /// <summary>
    ///     Dispatches a slot machine display packet. Open/SpinResult/JackpotAlert first apply the packet to
    ///     <see cref="WorldState.SlotMachine" /> (the authoritative state) and then tell <see cref="Slots" /> to
    ///     repaint from it; Rejected and Close carry nothing worth storing in the view model, so they go straight
    ///     to the control. <see cref="WorldState.SlotMachine" /> has no Changed event (unlike
    ///     <see cref="ViewModel.BankState" />) — there is exactly one producer of these packets, so this direct
    ///     apply-then-repaint call is simpler than adding an event with a single subscriber.
    /// </summary>
    private void HandleSlotMachineDisplay(SlotMachineDisplayArgs args)
    {
        switch (args.Type)
        {
            case SlotDisplayType.Open:
                WorldState.SlotMachine.ApplyOpen(args);
                Slots.Show();

                break;

            case SlotDisplayType.SpinResult:
                WorldState.SlotMachine.ApplySpinResult(args);
                Slots.OnSpinResult();

                break;

            case SlotDisplayType.JackpotAlert:
                WorldState.SlotMachine.ApplyJackpot(args);
                Slots.RefreshJackpot();

                break;

            case SlotDisplayType.Rejected:
                Slots.OnRejected(args.Reason);

                break;

            //the server ended the session — the player stood up, closed the panel, or the occupancy backstop
            //released them. The window would otherwise stay up, fully painted, with every Spin press coming back
            //NotOccupant, and could be carried by the player onto a different machine entirely.
            case SlotDisplayType.Close:
                Slots.Hide();

                break;
        }
    }

    //--- poker ---

    /// <summary>
    ///     Dispatches a poker table display packet. Open and Snapshot apply to
    ///     <see cref="WorldState.PokerTable" /> (the authoritative state) first, then tell <see cref="Poker" />
    ///     to repaint from it -- the control reads the view model rather than the packet, so there is exactly
    ///     one copy of the truth.
    /// </summary>
    private void HandlePokerTableDisplay(PokerTableDisplayArgs args)
    {
        switch (args.Type)
        {
            case PokerDisplayType.Open:
                WorldState.PokerTable.ApplyOpen(args);
                Poker.Show();

                break;

            case PokerDisplayType.Snapshot:
                WorldState.PokerTable.ApplySnapshot(args);
                Poker.OnSnapshot();

                break;

            case PokerDisplayType.Rejected:
                Poker.OnRejected(args.Reason);

                break;

            case PokerDisplayType.Close:
                Poker.Hide();
                WorldState.PokerTable.Clear();

                break;
        }
    }

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

    /// <summary>
    ///     Dispatches a fitting room display packet: Open loads the catalog into <see cref="WorldState.FittingRoom" />,
    ///     starting from the player's live look, then shows the window; Close hides it and drops the catalog.
    /// </summary>
    private void HandleFittingRoomDisplay(FittingRoomDisplayArgs args)
    {
        switch (args.Type)
        {
            case FittingRoomDisplayType.Open:
                WorldState.FittingRoom.ApplyOpen(args, WorldState.GetPlayerEntity()?.Appearance);
                FittingRoom.Show();

                break;

            case FittingRoomDisplayType.Close:
                FittingRoom.Hide();
                WorldState.FittingRoom.Clear();

                break;
        }
    }


    /// <summary>
    ///     Terminus's "Report a bug". The NPC dialog is closed now, during Update, so the frame drawn this tick has no
    ///     dialog in it. That frame is captured, and the report window opens on the next Update (see
    ///     <see cref="Update" />), so it is not in the picture either. A newer open drops any report that is still
    ///     waiting to open (captured but not yet shown by Update), since the server has already replaced it.
    /// </summary>
    private void HandleBugReportOpen(BugReportOpenArgs args)
    {
        //a newer open drops any report that is still waiting to open (captured but not yet shown by Update) —
        //the server has already replaced it, so no Cancel is sent for it.
        PendingBugReport?.Frame?.Dispose();
        PendingBugReport = null;

        NpcSession.HideAll();
        BugReport.Hide();

        var reportId = args.ReportId;

        Game.RequestCapture(
            frame =>
            {
                PendingBugReport?.Frame?.Dispose();
                PendingBugReport = (reportId, frame);
            });
    }

    //--- gilded spindle wheel ---

    /// <summary>
    ///     Dispatches a wheel display packet. Mirrors <see cref="HandleSlotMachineDisplay" /> exactly: Open/SpinResult
    ///     /JackpotAlert first apply the packet to <see cref="WorldState.GildedSpindle" /> (the authoritative state)
    ///     and then tell <see cref="Spindle" /> to repaint from it; Rejected and Close carry nothing worth storing
    ///     in the view model, so they go straight to the control.
    /// </summary>
    private void HandleWheelDisplay(WheelDisplayArgs args)
    {
        switch (args.Type)
        {
            case WheelDisplayType.Open:
                WorldState.GildedSpindle.ApplyOpen(args);
                Spindle.Show();

                break;

            case WheelDisplayType.SpinResult:
                WorldState.GildedSpindle.ApplySpinResult(args);
                Spindle.OnSpinResult();

                break;

            case WheelDisplayType.JackpotAlert:
                WorldState.GildedSpindle.ApplyJackpot(args);
                Spindle.RefreshJackpot();

                break;

            case WheelDisplayType.Rejected:
                Spindle.OnRejected(args.Reason);

                break;

            //the server ended the session -- the player stood up or the occupancy backstop released them
            case WheelDisplayType.Close:
                Spindle.Hide();

                break;
        }
    }

    /// <summary>
    ///     Dispatches a lockpick display packet. Open and TurnResult update <see cref="WorldState.Lockpick" /> first,
    ///     then tell the window, the same way the Spindle is driven.
    /// </summary>
    private void HandleLockpickDisplay(LockpickDisplayArgs args)
    {
        switch (args.Type)
        {
            case LockpickDisplayType.Open:
                WorldState.Lockpick.ApplyOpen(args);
                LockpickWindow.Show();

                break;

            case LockpickDisplayType.TurnResult:
                WorldState.Lockpick.ApplyTurnResult(args);
                LockpickWindow.OnTurnResult(args.Outcome, args.TurnPercent);

                break;

            case LockpickDisplayType.Close:
                LockpickWindow.OnServerClose(args.Reason);

                break;
        }
    }

    /// <summary>Opens, moves, or closes the fishing window from the server's picture of the line, or poses a fisher.</summary>
    private void HandleFishingDisplay(FishingDisplayArgs args)
    {
        switch (args.Type)
        {
            case FishingDisplayType.Open:
                FishingWindow.Open(args);

                break;

            case FishingDisplayType.State:
                FishingWindow.ApplyState(args);

                break;

            case FishingDisplayType.Close:
                FishingWindow.OnServerClose(args.Reason);

                break;

            case FishingDisplayType.Pose:
                if (WorldState.GetEntity(args.EntityId) is not { } fisher)
                    break;

                //a cast always restarts; any other repeat keeps the running animation's clock
                if ((args.Pose == fisher.FishingPose) && (args.Pose is not FishingPose.Cast))
                    break;

                fisher.PreviousFishingPose = fisher.FishingPose;
                fisher.FishingPose = args.Pose;
                fisher.FishingPoseStartedMs = Environment.TickCount64;

                break;
        }
    }

    /// <summary>
    ///     The server's quest list. Always applied; the window only appears if this answers the player's own Open, so a
    ///     late update after Close doesn't reopen it.
    /// </summary>
    private void HandleQuestLogDisplay(QuestLogDisplayArgs args)
    {
        WorldState.QuestLog.Apply(args.Entries);

        if (!QuestLogOpenPending)
            return;

        QuestLogOpenPending = false;
        QuestLogWindow.Show();
    }

    /// <summary>
    ///     Terminus's "Hotkeys" option: opens the key bindings window, the same one F12 opens. The server has already
    ///     closed its dialog. (The packet keeps its old name from when this opened the fixed hotkey chart.)
    /// </summary>
    private void HandleHotkeyHelpOpen() => KeyBindingsWindow.Show();

    private static MarketListing MapResultEntry(MarketResultEntry e)
        => new(
            e.ListingId,
            e.Sprite,
            e.Color,
            e.Name,
            e.Price,
            e.LevelReq,
            e.ClassReq,
            e.Weight,
            e.CurrentDurability,
            e.MaxDurability,
            e.Description,
            new ItemStats(
                e.Hp,
                e.Mp,
                e.Str,
                e.Int,
                e.Wis,
                e.Con,
                e.Dex,
                e.Ac,
                e.Hit,
                e.Dmg,
                e.AtkSpeed,
                e.FlatSkillDmg,
                e.FlatSpellDmg,
                e.SkillDmgPct,
                e.SpellDmgPct,
                e.Cdr,
                e.HealBonus,
                e.HealBonusPct,
                e.MagicResist))
        {
            AvailableCount = e.AvailableCount,
            SellerName = e.SellerName
        };
    #endregion

    //Up=0, Right=1, Down=2, Left=3 — matches server DirectionalRelationTo in tile space
    private static int GetProjectileDirection(int dtx, int dty)
    {
        var absDtx = Math.Abs(dtx);
        var absDty = Math.Abs(dty);

        if (absDtx > absDty)
            return dtx > 0 ? 1 : 3;

        if (absDty > 0)
            return dty < 0 ? 0 : 2;

        return 0;
    }
}