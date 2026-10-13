#region
using Chaos.Client.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Systems.KeyBinds;

/// <summary>Everything a player can put on a key. The order here is the order the key bindings window lists them in.</summary>
public enum GameAction
{
    //movement
    MoveUp,
    MoveRight,
    MoveDown,
    MoveLeft,

    //actions
    Assail,
    PickUp,
    UnequipWeapons,
    Refresh,
    GroupHighlight,

    //slots (used on whichever panel is open)
    Slot1,
    Slot2,
    Slot3,
    Slot4,
    Slot5,
    Slot6,
    Slot7,
    Slot8,
    Slot9,
    Slot10,
    Slot11,
    Slot12,

    //panels
    Inventory,
    ExpandInventory,
    Skills,
    SkillsAlt,
    Spells,
    SpellsAlt,
    Chat,
    MessageHistory,
    Stats,
    ExtendedStats,
    Tools,
    SwapHud,

    //windows
    Options,
    Boards,
    WorldList,
    SocialStatus,
    Group,
    RaidView,
    TownMap,
    TabMap,
    MapZoomIn,
    MapZoomOut,
    Help,
    Macros,
    Settings,
    BoardList,
    Friends,
    Screenshot,

    //chat
    FocusChat,
    Shout,
    Whisper,
    IgnoreList,
    ScrollChatUp,
    ScrollChatDown,

    //song notes (only while a song call is live)
    SongNote1,
    SongNote2,
    SongNote3,
    SongNote4,

    //targeting (only while a spell is armed with F4's Tab targeting on; these share keys with the actions above)
    TargetNext,
    TargetPrevious,
    TargetCast,
    TargetCancel,
    TargetSelf,
    TargetGroup1,
    TargetGroup2,
    TargetGroup3,
    TargetGroup4,
    TargetGroup5,
    TargetNearestEnemy,
    TargetFurthestEnemy,

    //hotbars: fifteen slots for each of the four bars, unbound until the player picks keys
    Hotbar1Slot1,
    Hotbar1Slot2,
    Hotbar1Slot3,
    Hotbar1Slot4,
    Hotbar1Slot5,
    Hotbar1Slot6,
    Hotbar1Slot7,
    Hotbar1Slot8,
    Hotbar1Slot9,
    Hotbar1Slot10,
    Hotbar1Slot11,
    Hotbar1Slot12,
    Hotbar1Slot13,
    Hotbar1Slot14,
    Hotbar1Slot15,
    Hotbar2Slot1,
    Hotbar2Slot2,
    Hotbar2Slot3,
    Hotbar2Slot4,
    Hotbar2Slot5,
    Hotbar2Slot6,
    Hotbar2Slot7,
    Hotbar2Slot8,
    Hotbar2Slot9,
    Hotbar2Slot10,
    Hotbar2Slot11,
    Hotbar2Slot12,
    Hotbar2Slot13,
    Hotbar2Slot14,
    Hotbar2Slot15,
    Hotbar3Slot1,
    Hotbar3Slot2,
    Hotbar3Slot3,
    Hotbar3Slot4,
    Hotbar3Slot5,
    Hotbar3Slot6,
    Hotbar3Slot7,
    Hotbar3Slot8,
    Hotbar3Slot9,
    Hotbar3Slot10,
    Hotbar3Slot11,
    Hotbar3Slot12,
    Hotbar3Slot13,
    Hotbar3Slot14,
    Hotbar3Slot15,
    Hotbar4Slot1,
    Hotbar4Slot2,
    Hotbar4Slot3,
    Hotbar4Slot4,
    Hotbar4Slot5,
    Hotbar4Slot6,
    Hotbar4Slot7,
    Hotbar4Slot8,
    Hotbar4Slot9,
    Hotbar4Slot10,
    Hotbar4Slot11,
    Hotbar4Slot12,
    Hotbar4Slot13,
    Hotbar4Slot14,
    Hotbar4Slot15,

    //emotes: 11 Ctrl, then 11 Ctrl+Alt, then 11 Alt
    Emote01,
    Emote02,
    Emote03,
    Emote04,
    Emote05,
    Emote06,
    Emote07,
    Emote08,
    Emote09,
    Emote10,
    Emote11,
    Emote12,
    Emote13,
    Emote14,
    Emote15,
    Emote16,
    Emote17,
    Emote18,
    Emote19,
    Emote20,
    Emote21,
    Emote22,
    Emote23,
    Emote24,
    Emote25,
    Emote26,
    Emote27,
    Emote28,
    Emote29,
    Emote30,
    Emote31,
    Emote32,
    Emote33
}

public enum GameActionCategory
{
    Movement,
    Actions,
    Slots,
    Panels,
    Windows,
    Chat,
    Song,
    Targeting,
    Hotbars,
    Emotes
}

/// <summary>Names, categories and the slot, note and emote each action stands for.</summary>
public static class GameActions
{
    public const int EMOTE_COUNT = 33;

    public static IReadOnlyList<GameAction> All { get; } = Enum.GetValues<GameAction>();

    //the emote order of the original client: 9-17 then 21-22 on Ctrl (18-20 are not body animations), 23-33 on
    //Ctrl+Alt and 34-44 on Alt
    private static readonly BodyAnimation[] CtrlEmotes =
    [
        BodyAnimation.Smile,
        BodyAnimation.Cry,
        BodyAnimation.Frown,
        BodyAnimation.Wink,
        BodyAnimation.Surprise,
        BodyAnimation.Tongue,
        BodyAnimation.Pleasant,
        BodyAnimation.Snore,
        BodyAnimation.Mouth,
        BodyAnimation.BlowKiss,
        BodyAnimation.Wave
    ];

    public static GameActionCategory CategoryOf(GameAction action)
        => action switch
        {
            <= GameAction.MoveLeft       => GameActionCategory.Movement,
            <= GameAction.GroupHighlight => GameActionCategory.Actions,
            <= GameAction.Slot12         => GameActionCategory.Slots,
            <= GameAction.SwapHud        => GameActionCategory.Panels,
            <= GameAction.Screenshot     => GameActionCategory.Windows,
            <= GameAction.ScrollChatDown => GameActionCategory.Chat,
            <= GameAction.SongNote4           => GameActionCategory.Song,
            <= GameAction.TargetFurthestEnemy => GameActionCategory.Targeting,
            <= GameAction.Hotbar4Slot15       => GameActionCategory.Hotbars,
            _                                 => GameActionCategory.Emotes
        };

    /// <summary>
    ///     True for the tab-targeting actions. They only run while a spell is armed, so they share keys with everything
    ///     else: a targeting key only ever clashes with another targeting key.
    /// </summary>
    public static bool IsTargeting(GameAction action) => action is >= GameAction.TargetNext and <= GameAction.TargetFurthestEnemy;

    /// <summary>0-4 for the group-member targeting actions (the group panel they pick), otherwise -1.</summary>
    public static int TargetGroupIndex(GameAction action)
        => action is >= GameAction.TargetGroup1 and <= GameAction.TargetGroup5 ? action - GameAction.TargetGroup1 : -1;

    public static IEnumerable<GameAction> InCategory(GameActionCategory category) => All.Where(action => CategoryOf(action) == category);

    public static string CategoryName(GameActionCategory category)
        => category switch
        {
            GameActionCategory.Song => "Song Notes",
            _                       => category.ToString()
        };

    /// <summary>1-12 for the slot actions, otherwise 0.</summary>
    public static int SlotNumber(GameAction action) => action is >= GameAction.Slot1 and <= GameAction.Slot12 ? action - GameAction.Slot1 + 1 : 0;

    public static bool IsHotbar(GameAction action) => action is >= GameAction.Hotbar1Slot1 and <= GameAction.Hotbar4Slot15;

    /// <summary>0-3 for the hotbar actions (which bar), otherwise -1.</summary>
    public static int HotbarIndex(GameAction action) => IsHotbar(action) ? (action - GameAction.Hotbar1Slot1) / HotbarSet.MAX_SLOTS : -1;

    /// <summary>0-14 for the hotbar actions (which slot on the bar), otherwise -1.</summary>
    public static int HotbarSlot(GameAction action) => IsHotbar(action) ? (action - GameAction.Hotbar1Slot1) % HotbarSet.MAX_SLOTS : -1;

    /// <summary>The action that uses slot <paramref name="slot" /> (0-14) of hotbar <paramref name="bar" /> (0-3).</summary>
    public static GameAction HotbarAction(int bar, int slot) => GameAction.Hotbar1Slot1 + (bar * HotbarSet.MAX_SLOTS) + slot;

    /// <summary>1-4 for the song note actions, otherwise 0.</summary>
    public static byte SongNote(GameAction action) => action is >= GameAction.SongNote1 and <= GameAction.SongNote4 ? (byte)(action - GameAction.SongNote1 + 1) : (byte)0;

    public static bool IsEmote(GameAction action) => action >= GameAction.Emote01;

    /// <summary>0-32 for the emote actions, otherwise -1.</summary>
    public static int EmoteIndex(GameAction action) => IsEmote(action) ? action - GameAction.Emote01 : -1;

    public static BodyAnimation EmoteAnimation(GameAction action)
    {
        var index = EmoteIndex(action);

        return index switch
        {
            < 0  => BodyAnimation.None,
            < 11 => CtrlEmotes[index],
            < 22 => (BodyAnimation)(23 + (index - 11)),
            _    => (BodyAnimation)(34 + (index - 22))
        };
    }

    public static string NameOf(GameAction action)
    {
        if (IsEmote(action))
        {
            var animation = EmoteAnimation(action);

            return EmoteCatalog.TryGet(animation, out var entry) ? entry.Name : animation.ToString();
        }

        if (SlotNumber(action) is var slot and > 0)
            return $"Slot {slot}";

        if (SongNote(action) is var note and > 0)
            return $"Note {note}";

        if (HotbarIndex(action) is var bar and >= 0)
            return $"Hotbar {bar + 1} Slot {HotbarSlot(action) + 1}";

        if (TargetGroupIndex(action) is var member and >= 0)
            return $"Target Group Member {member + 1}";

        return action switch
        {
            GameAction.MoveUp          => "Move Up",
            GameAction.MoveRight       => "Move Right",
            GameAction.MoveDown        => "Move Down",
            GameAction.MoveLeft        => "Move Left",
            GameAction.Assail          => "Assail",
            GameAction.PickUp          => "Pick Up Item",
            GameAction.UnequipWeapons  => "Unequip Weapon",
            GameAction.Refresh         => "Refresh",
            GameAction.GroupHighlight  => "Highlight Group",
            GameAction.Inventory       => "Inventory",
            GameAction.ExpandInventory => "Expand Inventory",
            GameAction.Skills          => "Skills",
            GameAction.SkillsAlt       => "Skills (Page 2)",
            GameAction.Spells          => "Spells",
            GameAction.SpellsAlt       => "Spells (Page 2)",
            GameAction.Chat            => "Chat",
            GameAction.MessageHistory  => "Message History",
            GameAction.Stats           => "Stats",
            GameAction.ExtendedStats   => "Extended Stats",
            GameAction.Tools           => "Tools",
            GameAction.SwapHud         => "Swap HUD Size",
            GameAction.Options         => "Options",
            GameAction.Boards          => "Boards",
            GameAction.WorldList       => "World List",
            GameAction.SocialStatus    => "Social Status",
            GameAction.Group           => "Group",
            GameAction.RaidView        => "Raid View",
            GameAction.TownMap         => "Town Map",
            GameAction.TabMap          => "Tab Map",
            GameAction.MapZoomIn       => "Tab Map Zoom In",
            GameAction.MapZoomOut      => "Tab Map Zoom Out",
            GameAction.Help            => "Help",
            GameAction.Macros          => "Macros",
            GameAction.Settings        => "Settings",
            GameAction.BoardList       => "Board List",
            GameAction.Friends         => "Friends",
            GameAction.Screenshot      => "Screenshot",
            GameAction.FocusChat       => "Type in Chat",
            GameAction.Shout           => "Shout",
            GameAction.Whisper         => "Whisper",
            GameAction.IgnoreList      => "Ignore List",
            GameAction.ScrollChatUp    => "Scroll Chat Up",
            GameAction.ScrollChatDown  => "Scroll Chat Down",
            GameAction.TargetNext          => "Next Target",
            GameAction.TargetPrevious      => "Previous Target",
            GameAction.TargetCast          => "Cast on Target",
            GameAction.TargetCancel        => "Cancel Targeting",
            GameAction.TargetSelf          => "Target Self",
            GameAction.TargetNearestEnemy  => "Nearest Enemy",
            GameAction.TargetFurthestEnemy => "Furthest Enemy",
            _                          => action.ToString()
        };
    }
}
