#region
using Chaos.Client.Networking;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

public enum SettingSection
{
    Display,
    DamageNumbers,
    Sound,
    Interaction
}

public enum SettingCategory
{
    /// <summary>Toggled on the server via opcode 0x1B; value updates when the server responds.</summary>
    ServerOption,

    /// <summary>Persisted locally to Darkages.cfg; flips immediately.</summary>
    ClientLocal,

    /// <summary>Server-authoritative via a dedicated opcode (group recruiting / ToggleGroup); value updates on server response.</summary>
    ServerAuthoritativeLocal
}

/// <summary>
///     How wide a setting renders in the F4 layout. <see cref="Half" /> cells pair into the two-column
///     grid; <see cref="Full" /> cells take a whole row (used by wide widgets and over-long labels).
/// </summary>
public enum SettingSpan
{
    /// <summary>Occupies one column; two Half cells share a row.</summary>
    Half,

    /// <summary>Occupies the full row width.</summary>
    Full
}

public enum SettingKey
{
    ShowBodyAnimations,
    PriorityAnimations,
    HideEnemyHealthBars,
    ShowFriendlyNametags,
    SmoothScrolling,
    MaxEffectAnimations,
    ListenToHitSounds,
    WhisperSound,
    LockHands,
    AllowExchanges,
    AutoAcceptGroupInvites,
    AllowGroupInvites,
    ClickCharacterProfile,
    DoubleTapForAltPanels,
    NpcRecordChat,
    Resolution,
    CooldownNumbers,
    DamageNumbersEnabled,
    DamageNumbersOnAislings,
    HealNumbersOnAislings,
    DamageNumbersOnNpcs,
    HealNumbersOnNpcs,
    DamageNumberSize,
    DamageNumbersMyOutputOnly,
    GroundTargetSnapToEntity
}

/// <summary>
///     Declarative description of a single F4 setting. For <see cref="SettingCategory.ServerOption" /> the
///     <see cref="UserOption" /> is set; for client-local settings the <see cref="Get" />/<see cref="Set" /> hooks read/write
///     <see cref="ClientSettings" />; for <see cref="SettingCategory.ServerAuthoritativeLocal" /> the
///     <see cref="OnServerToggle" /> hook sends the setting's dedicated packet (the value still arrives via the server's
///     response, never flipped locally).
/// </summary>
public sealed record SettingDefinition(
    SettingKey Key,
    string Label,
    SettingSection Section,
    SettingCategory Category,
    UserOption? UserOption = null,
    Func<bool>? Get = null,
    Action<bool>? Set = null,
    Action<ConnectionManager>? OnServerToggle = null,
    SettingSpan Span = SettingSpan.Half,
    IReadOnlyList<string>? Choices = null, //non-null ⇒ rendered as a dropdown instead of a checkbox
    Func<int>? GetChoice = null,           //current selected index (dropdown only)
    Action<int>? SetChoice = null,         //called with the new index on selection (dropdown only)
    SettingKey? GatedBy = null,            //non-null ⇒ this setting is enabled only while GatedBy's value is true
    Func<int>? GetSliderValue = null,      //non-null ⇒ rendered as a 0–10 slider instead of a checkbox
    Action<int>? SetSliderValue = null);   //called with the new value on slider change (slider only)

/// <summary>
///     The single ordered source of truth for the F4 settings, replacing the old fixed 20-slot magic-index model.
/// </summary>
public static class SettingDefinitions
{
    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        //── Display ──
        new(
            SettingKey.Resolution,
            "Resolution",
            SettingSection.Display,
            SettingCategory.ClientLocal,
            Span: SettingSpan.Full,
            Choices: DisplaySettings.OptionLabels,
            GetChoice: () => (int)ClientSettings.ScreenMode,
            SetChoice: DisplaySettings.Apply),
        new(SettingKey.ShowBodyAnimations, "Show body animations", SettingSection.Display, SettingCategory.ServerOption, UserOption.ShowBodyAnimations),
        new(SettingKey.PriorityAnimations, "Priority animations", SettingSection.Display, SettingCategory.ServerOption, UserOption.PriorityAnimations),
        new(SettingKey.HideEnemyHealthBars, "Hide enemy health bars", SettingSection.Display, SettingCategory.ServerOption, UserOption.HideEnemyHealthBars),
        new(SettingKey.ShowFriendlyNametags, "Always show friendly nametags", SettingSection.Display, SettingCategory.ServerOption, UserOption.AlwaysShowFriendlyNametags),
        new(
            SettingKey.SmoothScrolling,
            "Smooth screen scrolling",
            SettingSection.Display,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.ScrollLevel > 0,
            Set: v => ClientSettings.ScrollLevel = v ? 1 : 0),
        new(
            SettingKey.CooldownNumbers,
            "Cooldown numbers",
            SettingSection.Display,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.CooldownNumbersEnabled,
            Set: v => ClientSettings.CooldownNumbersEnabled = v),
        new(
            SettingKey.MaxEffectAnimations,
            "Max animations per entity",
            SettingSection.Display,
            SettingCategory.ClientLocal,
            Span: SettingSpan.Full,
            GetSliderValue: () => ClientSettings.MaxEffectAnimationsPerEntity,
            SetSliderValue: v =>
            {
                ClientSettings.MaxEffectAnimationsPerEntity = v;
                ClientSettings.Save();
            }),

        //── Damage Numbers ──
        new(
            SettingKey.DamageNumbersEnabled,
            "Enable damage numbers",
            SettingSection.DamageNumbers,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.DamageNumbersEnabled,
            Set: v => ClientSettings.DamageNumbersEnabled = v,
            Span: SettingSpan.Full),
        new(
            SettingKey.DamageNumberSize,
            "Damage number size",
            SettingSection.DamageNumbers,
            SettingCategory.ClientLocal,
            Span: SettingSpan.Full,
            Choices: ["Compact", "Normal", "Large"],
            GetChoice: () => (int)ClientSettings.DamageNumberSize,
            SetChoice: i =>
            {
                ClientSettings.DamageNumberSize = (DamageNumberSize)i;
                ClientSettings.Save();
            },
            GatedBy: SettingKey.DamageNumbersEnabled),
        new(
            SettingKey.DamageNumbersMyOutputOnly,
            "Only show my output",
            SettingSection.DamageNumbers,
            SettingCategory.ServerOption,
            UserOption.DamageNumbersMyOutputOnly,
            Span: SettingSpan.Full,
            GatedBy: SettingKey.DamageNumbersEnabled),
        new(
            SettingKey.DamageNumbersOnAislings,
            "Show Damage on Aislings",
            SettingSection.DamageNumbers,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.ShowDamageNumbersOnAislings,
            Set: v => ClientSettings.ShowDamageNumbersOnAislings = v,
            GatedBy: SettingKey.DamageNumbersEnabled),
        new(
            SettingKey.HealNumbersOnAislings,
            "Show Heals on Aislings",
            SettingSection.DamageNumbers,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.ShowHealNumbersOnAislings,
            Set: v => ClientSettings.ShowHealNumbersOnAislings = v,
            GatedBy: SettingKey.DamageNumbersEnabled),
        new(
            SettingKey.DamageNumbersOnNpcs,
            "Show Damage on NPCs",
            SettingSection.DamageNumbers,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.ShowDamageNumbersOnNpcs,
            Set: v => ClientSettings.ShowDamageNumbersOnNpcs = v,
            GatedBy: SettingKey.DamageNumbersEnabled),
        new(
            SettingKey.HealNumbersOnNpcs,
            "Show Heals on NPCs",
            SettingSection.DamageNumbers,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.ShowHealNumbersOnNpcs,
            Set: v => ClientSettings.ShowHealNumbersOnNpcs = v,
            GatedBy: SettingKey.DamageNumbersEnabled),

        //── Sound ──
        new(SettingKey.ListenToHitSounds, "Listen to hit sounds", SettingSection.Sound, SettingCategory.ServerOption, UserOption.ListenToHitSounds),
        new(SettingKey.WhisperSound, "Sound on whisper", SettingSection.Sound, SettingCategory.ServerOption, UserOption.WhisperSound),

        //── Interaction ──
        new(SettingKey.LockHands, "Lock hands", SettingSection.Interaction, SettingCategory.ServerOption, UserOption.LockHands),
        new(SettingKey.AllowExchanges, "Allow exchanges", SettingSection.Interaction, SettingCategory.ServerOption, UserOption.AllowExchange),
        new(
            SettingKey.AutoAcceptGroupInvites,
            "Auto accept group invites",
            SettingSection.Interaction,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.AutoAcceptGroupInvites,
            Set: v => ClientSettings.AutoAcceptGroupInvites = v),
        new(
            SettingKey.AllowGroupInvites,
            "Allow group invites",
            SettingSection.Interaction,
            SettingCategory.ServerAuthoritativeLocal,
            Get: () => ClientSettings.AllowGroupInvites,
            OnServerToggle: static c => c.ToggleGroup()),
        new(
            SettingKey.ClickCharacterProfile,
            "Click to open profile",
            SettingSection.Interaction,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.ClickToOpenProfile,
            Set: v => ClientSettings.ClickToOpenProfile = v),
        new(
            SettingKey.DoubleTapForAltPanels,
            "Double-tap for alt panels",
            SettingSection.Interaction,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.DoubleTapForAltPanels,
            Set: v => ClientSettings.DoubleTapForAltPanels = v),
        new(
            SettingKey.NpcRecordChat,
            "Show NPC messages in chat",
            SettingSection.Interaction,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.NpcRecordChat,
            Set: v => ClientSettings.NpcRecordChat = v),
        new(
            SettingKey.GroundTargetSnapToEntity,
            "Ground targeting snaps to entity",
            SettingSection.Interaction,
            SettingCategory.ClientLocal,
            Get: () => ClientSettings.GroundTargetSnapToEntity,
            Set: v => ClientSettings.GroundTargetSnapToEntity = v,
            Span: SettingSpan.Full)
    ];

    private static readonly Dictionary<SettingKey, SettingDefinition> ByKeyLookup =
        All.ToDictionary(d => d.Key);

    private static readonly Dictionary<UserOption, SettingDefinition> ByUserOptionLookup =
        All.Where(d => d.UserOption is not null)
           .ToDictionary(d => d.UserOption!.Value);

    public static SettingDefinition ByKey(SettingKey key) => ByKeyLookup[key];

    public static SettingDefinition? ByUserOption(UserOption opt)
        => ByUserOptionLookup.GetValueOrDefault(opt);
}