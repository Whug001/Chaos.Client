#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Removes the map effects a player has chosen to hide in F4's "Triggering or Unsettling Map Effects" section.
///     The weather and ambient renderers get the filtered flags; the map's own flags are never changed, so darkness,
///     the map locks and the server's view of the map stay as they were.
/// </summary>
public static class MapEffectFilter
{
    /// <summary>The F4 settings that feed <see cref="HiddenByPlayer" />. A change to any of them re-applies the filter.</summary>
    public static IReadOnlyCollection<SettingKey> Keys { get; } =
    [
        SettingKey.HideLightning,
        SettingKey.HideRain,
        SettingKey.HideBloodMoon,
        SettingKey.HideSandstorm,
        SettingKey.HideFrost,
        SettingKey.HideBlizzard
    ];

    /// <summary>The effect flags the player has ticked off, read fresh from <see cref="ClientSettings" /> each call.</summary>
    public static MapFlags HiddenByPlayer
    {
        get
        {
            var hidden = MapFlags.None;

            if (ClientSettings.HideLightning)
                hidden |= MapFlags.Lightning;

            if (ClientSettings.HideRain)
                hidden |= MapFlags.Rain;

            if (ClientSettings.HideBloodMoon)
                hidden |= MapFlags.BloodMoon;

            if (ClientSettings.HideSandstorm)
                hidden |= MapFlags.Sandstorm;

            if (ClientSettings.HideFrost)
                hidden |= MapFlags.Frost;

            if (ClientSettings.HideBlizzard)
                hidden |= MapFlags.Blizzard;

            return hidden;
        }
    }

    /// <summary>The flags the renderers should draw, given the player's current settings.</summary>
    public static MapFlags Visible(MapFlags flags) => Visible(flags, HiddenByPlayer);

    /// <summary>
    ///     <paramref name="flags" /> without <paramref name="hidden" />. Rain is kept on a dark map: Darkness is stored as
    ///     Rain | Snow, so taking the rain bit out would leave Snow alone and start a snowfall. A dark map draws no rain
    ///     anyway.
    /// </summary>
    public static MapFlags Visible(MapFlags flags, MapFlags hidden)
    {
        if ((flags & MapFlags.Darkness) == MapFlags.Darkness)
            hidden &= ~MapFlags.Rain;

        return flags & ~hidden;
    }
}
