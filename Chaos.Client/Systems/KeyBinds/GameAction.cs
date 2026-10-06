#region
using Chaos.Client.Definitions;
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
            <= GameAction.SongNote4      => GameActionCategory.Song,
            _                            => GameActionCategory.Emotes
        };

    public static IEnumerable<GameAction> InCategory(GameActionCategory category) => All.Where(action => CategoryOf(action) == category);

    public static string CategoryName(GameActionCategory category)
        => category switch
        {
            GameActionCategory.Song => "Song Notes",
            _                       => category.ToString()
        };

    /// <summary>1-12 for the slot actions, otherwise 0.</summary>
    public static int SlotNumber(GameAction action) => action is >= GameAction.Slot1 and <= GameAction.Slot12 ? action - GameAction.Slot1 + 1 : 0;

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
            _                          => action.ToString()
        };
    }
}
