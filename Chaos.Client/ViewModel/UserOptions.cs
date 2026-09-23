#region
using Chaos.Client.Chat;
using Chaos.Client.Systems;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Source of truth for the F4 toggle values, driven by <see cref="SettingDefinitions" />. Server-controlled settings
///     update via the 0x1B response; client-local settings persist to Darkages.cfg; the group-recruiting toggle is
///     server-authoritative. Replaces the old fixed 20-slot magic-index model.
/// </summary>
public sealed class UserOptions
{
    private readonly Dictionary<SettingKey, bool> Values = new();
    private readonly Dictionary<SettingKey, int> ChoiceValues = new();

    public UserOptions()
    {
        foreach (var def in SettingDefinitions.All)
        {
            Values[def.Key] = false;

            //choice defaults are Unfiltered-equivalent 0; the login sync Applies the stored values after.
            if (def.Choices is not null)
                ChoiceValues[def.Key] = 0;
        }
    }

    /// <summary>Fires on any value change (server response, init, or client toggle). Used by the UI to refresh checkboxes.</summary>
    public event SettingValueChangedHandler? ValueChanged;

    /// <summary>Fires only on user-initiated toggles. Used by WorldScreen to route server-controlled toggles to the network.</summary>
    public event SettingValueChangedHandler? UserToggled;

    /// <summary>Fires only on user-initiated dropdown selections. Used by WorldScreen to route server-controlled choices to the network.</summary>
    public event SettingChoiceSelectedHandler? UserChoiceSelected;

    /// <summary>
    ///     The stored chat filter mode. Task 5 call sites read this (never a hardcoded default), so the first
    ///     login-sync Apply flips every chat path at once. Unfiltered until the sync lands.
    /// </summary>
    public ChatFilterMode ChatFilterMode => (ChatFilterMode)ChoiceValue(SettingKey.ChatFilterMode);

    public bool Value(SettingKey key) => Values.TryGetValue(key, out var v) && v;

    public int ChoiceValue(SettingKey key) => ChoiceValues.GetValueOrDefault(key);

    /// <summary>
    ///     Sets a choice value from an external source (server response or init) and raises
    ///     <see cref="ValueChanged" />. Out-of-range indices are ignored, never stored: a wild sync
    ///     payload cannot corrupt the cache or crash a concurrent edit.
    /// </summary>
    public void ApplyChoice(SettingKey key, int index)
    {
        var def = SettingDefinitions.ByKey(key);

        if ((def.Choices is null) || (index < 0) || (index >= def.Choices.Count))
            return;

        ChoiceValues[key] = index;
        ValueChanged?.Invoke(key, index != 0);
    }

    /// <summary>
    ///     Handles a user dropdown selection. Server-controlled choices only raise
    ///     <see cref="UserChoiceSelected" /> — their value updates when the server responds.
    ///     Client-local choices apply immediately through the definition's SetChoice hook.
    /// </summary>
    public void SelectChoice(SettingKey key, int index)
    {
        var def = SettingDefinitions.ByKey(key);

        if (def.Category == SettingCategory.ServerOption)
        {
            UserChoiceSelected?.Invoke(key, index);

            return;
        }

        def.SetChoice?.Invoke(index);
    }

    /// <summary>Sets a value from an external source (server response or init) and raises <see cref="ValueChanged" />.</summary>
    public void Apply(SettingKey key, bool value)
    {
        Values[key] = value;
        ValueChanged?.Invoke(key, value);
    }

    /// <summary>
    ///     Handles a user click. Client-local settings flip + persist immediately; server-controlled settings only raise
    ///     <see cref="UserToggled" /> — their value updates when the server responds.
    /// </summary>
    public void Toggle(SettingKey key)
    {
        var def = SettingDefinitions.ByKey(key);

        if (def.Category == SettingCategory.ClientLocal)
        {
            var newValue = !Value(key);
            Values[key] = newValue;
            def.Set?.Invoke(newValue);
            ClientSettings.Save();
            ValueChanged?.Invoke(key, newValue);

            return;
        }

        //server-controlled / server-authoritative: don't flip locally; server response will Apply the new value
        UserToggled?.Invoke(key, !Value(key));
    }

    /// <summary>Seeds client-local and server-authoritative-local values from <see cref="ClientSettings" /> (call after ClientSettings.Load). The only caller of each definition's Get hook.</summary>
    public void SeedLocalDefaults()
    {
        foreach (var def in SettingDefinitions.All)
            if (def.Get is not null)
                Apply(def.Key, def.Get());
    }

    /// <summary>Clears only server-controlled settings, leaving client-local values intact.</summary>
    public void ClearServerSettings()
    {
        foreach (var def in SettingDefinitions.All)
            if (def.Category == SettingCategory.ServerOption)
            {
                Values[def.Key] = false;

                if (def.Choices is not null)
                    ChoiceValues[def.Key] = 0;
            }
    }
}
