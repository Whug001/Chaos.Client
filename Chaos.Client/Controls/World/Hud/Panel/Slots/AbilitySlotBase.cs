#region
using Chaos.Client.Systems;
#endregion

namespace Chaos.Client.Controls.World.Hud.Panel.Slots;

/// <summary>
///     Slot control for skills and spells. Parses "Name (Lev:X/Y)" format into AbilityName and AbilityLevel. Adds
///     right-click event for chant editing.
/// </summary>
public abstract class AbilitySlotControl : PanelSlot
{
    /// <summary>
    ///     Parsed level string (e.g. "50/100"), or null if not present.
    /// </summary>
    public string? AbilityLevel { get; private set; }

    /// <summary>
    ///     Parsed ability name without the level suffix (e.g. "Ioc").
    /// </summary>
    public string? AbilityName { get; private set; }

    /// <summary>
    ///     Fired on right-click. Parameter is the 1-based slot number.
    /// </summary>
    public event AbilitySlotRightClickHandler? OnRightClick;

    /// <summary>
    ///     The text shown on hover. Hides the " (Lev:X/X)" suffix for full levels unless the player opted to show them;
    ///     partial or unparseable levels always show in full.
    /// </summary>
    public string? HoverText => FormatHoverText(SlotName, AbilityName, AbilityLevel, ClientSettings.ShowAbilityLevels);

    /// <summary>
    ///     Pure hover-text rule, split out so it can be tested without UI. Returns null for null/empty, the full name when
    ///     <paramref name="showLevels" /> is on, when the name carries no "(Lev:" suffix, or when the level is not full or
    ///     does not parse as "X/Y" integers; otherwise just the ability name.
    /// </summary>
    public static string? FormatHoverText(string? fullName, string? abilityName, string? abilityLevel, bool showLevels)
    {
        if (string.IsNullOrEmpty(fullName))
            return null;

        if (showLevels)
            return fullName;

        if (!fullName.Contains("(Lev:", StringComparison.Ordinal))
            return fullName;

        if (string.IsNullOrEmpty(abilityName) || string.IsNullOrEmpty(abilityLevel))
            return fullName;

        var slashIndex = abilityLevel.IndexOf('/');

        if (slashIndex <= 0)
            return fullName;

        if (!int.TryParse(abilityLevel[..slashIndex].Trim(), out var current))
            return fullName;

        if (!int.TryParse(abilityLevel[(slashIndex + 1)..].Trim(), out var maximum))
            return fullName;

        if (current != maximum)
            return fullName;

        return abilityName;
    }

    /// <summary>
    ///     Sets the slot name and parses out ability name and level from the "Name (Lev:X/Y)" format.
    /// </summary>
    public void SetAbilityName(string? name)
    {
        SlotName = name;

        if (string.IsNullOrEmpty(name))
        {
            AbilityName = null;
            AbilityLevel = null;

            return;
        }

        var levIndex = name.LastIndexOf("(Lev:", StringComparison.Ordinal);

        if (levIndex > 0)
        {
            AbilityName = name[..levIndex].TrimEnd();

            var levStart = levIndex + 5;
            var levEnd = name.IndexOf(')', levStart);

            AbilityLevel = levEnd > levStart ? name[levStart..levEnd] : null;
        } else
        {
            AbilityName = name;
            AbilityLevel = null;
        }
    }

    public override void OnClick(ClickEvent e)
    {
        if ((e.Button == MouseButton.Right) && NormalTexture is not null)
        {
            OnRightClick?.Invoke(Slot);
            e.Handled = true;

            return;
        }

        base.OnClick(e);
    }
}