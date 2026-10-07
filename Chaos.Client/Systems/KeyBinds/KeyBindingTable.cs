#region
using Chaos.Client.Definitions;
#endregion

namespace Chaos.Client.Systems.KeyBinds;

/// <summary>What happened when a key was bound.</summary>
/// <param name="Bound">
///     False when the key is one that cannot be bound (see <see cref="KeyBindingTable.IsReserved" />), or when it is a song
///     note's key on another song note.
/// </param>
/// <param name="TakenFrom">The action that held the key before, if another one did. It loses the key.</param>
/// <param name="ClashesWith">The song note already on that key, when that is why it was refused.</param>
public readonly record struct BindResult(bool Bound, GameAction? TakenFrom, GameAction? ClashesWith = null);

/// <summary>
///     Which keys run which actions: a primary and a secondary key for every <see cref="GameAction" />. A key belongs to
///     one action at a time, so binding it takes it from whatever had it.
/// </summary>
public sealed class KeyBindingTable
{
    public const int SLOTS = 2;

    /// <summary>The prefix of a binding's line in the settings file: "Bind.MoveUp : Up, C".</summary>
    public const string SETTINGS_PREFIX = "Bind.";

    private static readonly Scancode[] NumberRow =
    [
        Scancode.D1,
        Scancode.D2,
        Scancode.D3,
        Scancode.D4,
        Scancode.D5,
        Scancode.D6,
        Scancode.D7,
        Scancode.D8,
        Scancode.D9,
        Scancode.D0,
        Scancode.OemMinus,
        Scancode.OemPlus
    ];

    private readonly KeyChord[,] Chords = new KeyChord[GameActions.All.Count, SLOTS];

    public KeyBindingTable() => ResetToDefaults();

    public KeyChord Get(GameAction action, int slot) => Chords[(int)action, slot];

    public IEnumerable<KeyChord> ChordsFor(GameAction action)
    {
        for (var slot = 0; slot < SLOTS; slot++)
            if (!Chords[(int)action, slot].IsNone)
                yield return Chords[(int)action, slot];
    }

    /// <summary>
    ///     Keys that never take a binding, so a player cannot lose them: Escape (closes windows and cancels a rebind), F11
    ///     (debug overlay), F12 (this window), Alt+Enter (window size) and the modifier keys on their own.
    /// </summary>
    public static bool IsReserved(KeyChord chord)
        => chord.IsNone
           || KeyChord.IsModifierKey(chord.Key)
           || chord.Key is Scancode.Escape or Scancode.F11 or Scancode.F12
           || ((chord.Key == Scancode.Enter) && chord.Alt);

    /// <summary>
    ///     <see cref="IsReserved" />, except that Cancel Targeting may have bare Escape. It only runs while a spell is armed,
    ///     where Escape has no other job, and it is that action's default.
    /// </summary>
    public static bool IsReservedFor(GameAction action, KeyChord chord)
        => IsReserved(chord) && !((action == GameAction.TargetCancel) && (chord == new KeyChord(Scancode.Escape)));

    /// <summary>
    ///     Whether two actions compete for a key. The targeting actions only run while a spell is armed, so they compete only
    ///     with each other: Space can be both Assail and Cast on Target.
    /// </summary>
    public static bool SameScope(GameAction a, GameAction b) => GameActions.IsTargeting(a) == GameActions.IsTargeting(b);

    /// <summary>
    ///     Puts <paramref name="chord" /> in <paramref name="action" />'s slot, taking it from whatever held it in the same
    ///     scope (see <see cref="SameScope" />).
    /// </summary>
    public BindResult Bind(GameAction action, int slot, KeyChord chord)
    {
        if (IsReservedFor(action, chord))
            return new BindResult(false, null);

        if (SongNoteSharingKey(action, chord.Key) is { } clash)
            return new BindResult(false, null, clash);

        GameAction? takenFrom = null;

        foreach (var other in GameActions.All)
            for (var otherSlot = 0; otherSlot < SLOTS; otherSlot++)
            {
                if ((Chords[(int)other, otherSlot] != chord) || !SameScope(action, other))
                    continue;

                Chords[(int)other, otherSlot] = KeyChord.None;

                if (other != action)
                    takenFrom = other;
            }

        Chords[(int)action, slot] = chord;

        return new BindResult(true, takenFrom);
    }

    public void Clear(GameAction action, int slot) => Chords[(int)action, slot] = KeyChord.None;

    /// <summary>
    ///     The other song note already on <paramref name="key" />, if <paramref name="action" /> is a song note. Each note
    ///     needs a key of its own whatever the modifiers: the song bar shows the call by key, so Ctrl+W and Alt+W would both
    ///     read "W" and the call could not be told apart.
    /// </summary>
    public GameAction? SongNoteSharingKey(GameAction action, Scancode key)
    {
        if (GameActions.SongNote(action) == 0)
            return null;

        for (var other = GameAction.SongNote1; other <= GameAction.SongNote4; other++)
        {
            if (other == action)
                continue;

            for (var slot = 0; slot < SLOTS; slot++)
                if (Chords[(int)other, slot].Key == key && !Chords[(int)other, slot].IsNone)
                    return other;
        }

        return null;
    }

    /// <summary>
    ///     How the song bar names the four notes: each note's key (its primary, or its secondary when it has no primary),
    ///     with the modifier said once in front when all four share it, e.g. ("Ctrl+", ["W", "A", "S", "D"]). A note with no
    ///     key at all shows "?".
    /// </summary>
    public (string Prefix, string[] Keys) SongNoteDisplay()
    {
        var chords = new KeyChord[4];

        for (var i = 0; i < 4; i++)
        {
            var action = GameAction.SongNote1 + i;
            chords[i] = Chords[(int)action, 0].IsNone ? Chords[(int)action, 1] : Chords[(int)action, 0];
        }

        var bound = chords.Where(chord => !chord.IsNone).ToList();
        var shared = (bound.Count == 4) && bound.All(chord => chord.Modifiers == bound[0].Modifiers);
        var prefix = shared ? bound[0].ModifierPrefix() : string.Empty;

        var keys = chords.Select(chord => chord.IsNone ? "?" : shared ? KeyNames.Get(chord.Key) : chord.DisplayName)
                         .ToArray();

        return (prefix, keys);
    }

    /// <summary>
    ///     The action bound to exactly this key and these modifiers, skipping any <paramref name="usable" /> turns down.
    ///     Searches the targeting actions when <paramref name="targeting" /> is true, and everything else otherwise.
    /// </summary>
    public GameAction? Find(KeyChord chord, Func<GameAction, bool>? usable = null, bool targeting = false)
    {
        if (chord.IsNone)
            return null;

        foreach (var action in GameActions.All)
        {
            if (GameActions.IsTargeting(action) != targeting)
                continue;

            for (var slot = 0; slot < SLOTS; slot++)
                if ((Chords[(int)action, slot] == chord) && (usable is null || usable(action)))
                    return action;
        }

        return null;
    }

    /// <summary>
    ///     The actions a key press could run, best first: the one bound to the exact chord, then the one bound to the bare
    ///     key. The bare-key fallback keeps the old client's habit of ignoring a held modifier nothing claims, so Shift+2
    ///     still uses slot 2 and Ctrl+B still picks up.
    /// </summary>
    public IEnumerable<GameAction> Candidates(KeyChord chord, Func<GameAction, bool>? usable = null, bool targeting = false)
    {
        var exact = Find(chord, usable, targeting);

        if (exact is not null)
            yield return exact.Value;

        if (chord.Modifiers == KeyModifiers.None)
            yield break;

        var bare = Find(chord.Bare, usable, targeting);

        if ((bare is not null) && (bare != exact))
            yield return bare.Value;
    }

    public bool IsDefault(GameAction action, int slot) => Chords[(int)action, slot] == DefaultChord(action, slot);

    public void ResetToDefaults()
    {
        foreach (var action in GameActions.All)
            for (var slot = 0; slot < SLOTS; slot++)
                Chords[(int)action, slot] = DefaultChord(action, slot);
    }

    public void CopyFrom(KeyBindingTable other) => Array.Copy(other.Chords, Chords, Chords.Length);

    /// <summary>One "Bind.Action : Primary, Secondary" line per action, for the settings file.</summary>
    public IEnumerable<string> ToSettingsLines()
    {
        foreach (var action in GameActions.All)
            yield return $"{SETTINGS_PREFIX}{action} : {Chords[(int)action, 0].Serialize()}, {Chords[(int)action, 1].Serialize()}";
    }

    /// <summary>
    ///     Reads one settings line's key and value. Returns false when the line is not a binding or cannot be read; an
    ///     unreadable line leaves the action as it was, and a reserved key is dropped.
    /// </summary>
    public bool TryLoadSetting(string key, string value)
    {
        if (!key.StartsWith(SETTINGS_PREFIX, StringComparison.Ordinal)
            || !Enum.TryParse<GameAction>(key[SETTINGS_PREFIX.Length..], out var action)
            || !Enum.IsDefined(action))
            return false;

        var parts = value.Split(',');
        var parsed = new KeyChord[SLOTS];

        for (var slot = 0; slot < SLOTS; slot++)
            if ((slot < parts.Length) && !KeyChord.TryParse(parts[slot], out parsed[slot]))
                return false;

        //written straight in rather than through Bind: lines load one action at a time, and stealing here would let
        //the order of the file decide who keeps a key the file gives to two actions. The last line to name it wins
        //below instead, once, by clearing the earlier holder
        for (var slot = 0; slot < SLOTS; slot++)
        {
            var chord = IsReservedFor(action, parsed[slot]) ? KeyChord.None : parsed[slot];

            if (!chord.IsNone)
                foreach (var other in GameActions.All)
                    for (var otherSlot = 0; otherSlot < SLOTS; otherSlot++)
                        if ((Chords[(int)other, otherSlot] == chord) && SameScope(action, other))
                            Chords[(int)other, otherSlot] = KeyChord.None;

            Chords[(int)action, slot] = chord;
        }

        return true;
    }

    public static KeyChord DefaultChord(GameAction action, int slot)
    {
        if (slot == 1)
            return action switch
            {
                GameAction.MoveUp    => new KeyChord(Scancode.C),
                GameAction.MoveRight => new KeyChord(Scancode.V),
                GameAction.MoveDown  => new KeyChord(Scancode.X),
                GameAction.MoveLeft  => new KeyChord(Scancode.Z),
                _                    => KeyChord.None
            };

        if (GameActions.SlotNumber(action) is var slotNumber and > 0)
            return new KeyChord(NumberRow[slotNumber - 1]);

        if (GameActions.SongNote(action) is var note and > 0)
            return new KeyChord(
                note switch
                {
                    1 => Scancode.U,
                    2 => Scancode.I,
                    3 => Scancode.O,
                    _ => Scancode.P
                });

        if (GameActions.TargetGroupIndex(action) is var member and >= 0)
            return new KeyChord(Scancode.F2 + member);

        if (GameActions.EmoteIndex(action) is var emote and >= 0)
        {
            var modifiers = emote switch
            {
                < 11 => KeyModifiers.Ctrl,
                < 22 => KeyModifiers.Ctrl | KeyModifiers.Alt,
                _    => KeyModifiers.Alt
            };

            return new KeyChord(NumberRow[emote % 11], modifiers);
        }

        return action switch
        {
            GameAction.MoveUp          => new KeyChord(Scancode.Up),
            GameAction.MoveRight       => new KeyChord(Scancode.Right),
            GameAction.MoveDown        => new KeyChord(Scancode.Down),
            GameAction.MoveLeft        => new KeyChord(Scancode.Left),
            GameAction.Assail          => new KeyChord(Scancode.Space),
            GameAction.PickUp          => new KeyChord(Scancode.B),
            GameAction.UnequipWeapons  => new KeyChord(Scancode.OemTilde),
            GameAction.Refresh         => new KeyChord(Scancode.F5),
            GameAction.GroupHighlight  => new KeyChord(Scancode.J),
            GameAction.Inventory       => new KeyChord(Scancode.A),
            GameAction.ExpandInventory => new KeyChord(Scancode.A, KeyModifiers.Shift),
            GameAction.Skills          => new KeyChord(Scancode.S),
            GameAction.SkillsAlt       => new KeyChord(Scancode.S, KeyModifiers.Shift),
            GameAction.Spells          => new KeyChord(Scancode.D),
            GameAction.SpellsAlt       => new KeyChord(Scancode.D, KeyModifiers.Shift),
            GameAction.Chat            => new KeyChord(Scancode.F),
            GameAction.MessageHistory  => new KeyChord(Scancode.F, KeyModifiers.Shift),
            GameAction.Stats           => new KeyChord(Scancode.G),
            GameAction.ExtendedStats   => new KeyChord(Scancode.G, KeyModifiers.Shift),
            GameAction.Tools           => new KeyChord(Scancode.H),
            GameAction.SwapHud         => new KeyChord(Scancode.OemQuestion),
            GameAction.Options         => new KeyChord(Scancode.Q),
            GameAction.Boards          => new KeyChord(Scancode.W),
            GameAction.WorldList       => new KeyChord(Scancode.E),
            GameAction.SocialStatus    => new KeyChord(Scancode.R),
            GameAction.Group           => new KeyChord(Scancode.Y),
            GameAction.TownMap         => new KeyChord(Scancode.T),
            GameAction.TabMap          => new KeyChord(Scancode.Tab),
            GameAction.MapZoomIn       => new KeyChord(Scancode.PageUp),
            GameAction.MapZoomOut      => new KeyChord(Scancode.PageDown),
            GameAction.Help            => new KeyChord(Scancode.F1),
            GameAction.Macros          => new KeyChord(Scancode.F3),
            GameAction.Settings        => new KeyChord(Scancode.F4),
            GameAction.BoardList       => new KeyChord(Scancode.F7),
            GameAction.Friends         => new KeyChord(Scancode.F10),
            GameAction.Screenshot      => new KeyChord(Scancode.PrintScreen),
            GameAction.FocusChat       => new KeyChord(Scancode.Enter),
            GameAction.Shout           => new KeyChord(Scancode.D1, KeyModifiers.Shift),
            GameAction.Whisper         => new KeyChord(Scancode.OemQuotes, KeyModifiers.Shift),
            GameAction.IgnoreList      => new KeyChord(Scancode.F9),
            GameAction.ScrollChatUp    => new KeyChord(Scancode.Up, KeyModifiers.Shift),
            GameAction.ScrollChatDown  => new KeyChord(Scancode.Down, KeyModifiers.Shift),
            GameAction.TargetNext          => new KeyChord(Scancode.Tab),
            GameAction.TargetPrevious      => new KeyChord(Scancode.Tab, KeyModifiers.Shift),
            GameAction.TargetCast          => new KeyChord(Scancode.Space),
            GameAction.TargetCancel        => new KeyChord(Scancode.Escape),
            GameAction.TargetSelf          => new KeyChord(Scancode.F1),
            GameAction.TargetNearestEnemy  => new KeyChord(Scancode.F7),
            GameAction.TargetFurthestEnemy => new KeyChord(Scancode.F8),
            _                          => KeyChord.None
        };
    }
}
