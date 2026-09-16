#region
using Chaos.Client.Collections;
using Chaos.Client.Data.Utilities;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public delegate void GroupMembersChangedHandler();

/// <summary>
///     One member's row on a group panel, as last reported by the server (<c>SetGroupState</c>, opcode 123).
/// </summary>
/// <remarks>
///     A snapshot rather than a live handle. None of this can be read off a world entity: a group member on another
///     map, or simply out of view, is not an entity this client has ever been told about, so their vitals, class and
///     look only exist here.
/// </remarks>
public sealed record GroupMemberSnapshot
{
    public required string Name { get; init; }
    public required BaseClass BaseClass { get; init; }
    public required uint CurrentHp { get; init; }
    public required uint MaximumHp { get; init; }
    public required uint CurrentMp { get; init; }
    public required uint MaximumMp { get; init; }

    /// <summary>What to draw in the portrait, already in the renderer's own shape.</summary>
    public required AislingAppearance Appearance { get; init; }

    /// <summary>Effect icons, soonest to expire first. At most five -- what the panel has room for.</summary>
    public required IReadOnlyList<byte> EffectIcons { get; init; }

    public double HealthPercent => MaximumHp == 0 ? 0 : Math.Clamp(CurrentHp / (double)MaximumHp, 0, 1);
    public double ManaPercent => MaximumMp == 0 ? 0 : Math.Clamp(CurrentMp / (double)MaximumMp, 0, 1);
}

/// <summary>
///     Everyone in the player's group, as last reported by the server. Never computed here -- the server owns every
///     number on a group panel and this only holds what it sent.
/// </summary>
/// <remarks>
///     Separate from <see cref="GroupState" />, which is parsed out of the self-profile's group string and knows
///     names and a leader and nothing else. That string is what the group tab and the recruit flow are built on and
///     it stays as it is; this is the panel feed, and the two are updated by different packets at different times.
///     <para />
///     Every packet replaces the whole list, so a member who left is gone the moment the next one lands and there is
///     no stale row to clean up. An empty list means the player is not grouped.
/// </remarks>
public sealed class GroupMembersState
{
    private static readonly IReadOnlyList<byte> NoEffects = [];

    public IReadOnlyList<GroupMemberSnapshot> Members { get; private set; } = [];

    /// <summary>True while there is anyone to draw a panel for.</summary>
    public bool HasMembers => Members.Count > 0;

    /// <summary>
    ///     Whether the player has folded the panels away with the collapse arrow. Survives group changes on purpose:
    ///     somebody who collapsed the panels does not want them thrown back open when a member joins.
    /// </summary>
    public bool Collapsed { get; set; }

    /// <summary>Raised whenever the member list is replaced, so the panels can rebuild.</summary>
    public event GroupMembersChangedHandler? Changed;

    /// <summary>Stores a server snapshot, replacing whatever was held before.</summary>
    public void Set(SetGroupStateArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var members = new List<GroupMemberSnapshot>(args.Members.Count);

        foreach (var member in args.Members)
            members.Add(ToSnapshot(member));

        Members = members;
        Changed?.Invoke();
    }

    /// <summary>Drops every member. Called on logout and on disconnect.</summary>
    public void Reset()
    {
        if (Members.Count == 0)
            return;

        Members = [];
        Changed?.Invoke();
    }

    private static GroupMemberSnapshot ToSnapshot(GroupMemberInfo member)
    {
        IReadOnlyList<byte> icons = NoEffects;

        if (member.Effects.Count > 0)
        {
            var list = new List<byte>(member.Effects.Count);

            foreach (var effect in member.Effects)
                list.Add(effect.Icon);

            icons = list;
        }

        return new GroupMemberSnapshot
        {
            Name = member.Name,
            BaseClass = member.BaseClass,
            CurrentHp = member.CurrentHp,
            MaximumHp = member.MaximumHp,
            CurrentMp = member.CurrentMp,
            MaximumMp = member.MaximumMp,
            Appearance = ToAppearance(member),
            EffectIcons = icons
        };
    }

    /// <summary>
    ///     Builds the renderer's appearance from the slice of it the packet carries.
    /// </summary>
    /// <remarks>
    ///     Mirrors the <c>DisplayAisling</c> mapping in <see cref="WorldState" />, including the ghost body-colour
    ///     substitution, and shares its body-sprite lookup rather than repeating the table. The fields the packet
    ///     leaves out -- boots, pants, weapon, shield -- are all below a head-and-shoulders crop, so they default.
    /// </remarks>
    private static AislingAppearance ToAppearance(GroupMemberInfo member)
        => new()
        {
            Gender = DataUtilities.DetermineGender(member.BodySprite),
            BodySpriteId = WorldState.GetBodySpriteId(member.BodySprite),
            BodyColor = member.BodySprite is BodySprite.MaleGhost or BodySprite.FemaleGhost
                ? WorldState.GHOST_BODY_COLOR
                : (int)member.BodyColor,
            HeadSprite = member.HeadSprite,
            HeadColor = member.HeadColor,
            FaceSprite = member.FaceSprite,
            ArmorSprite = member.ArmorSprite,
            ArmorColor = DisplayColor.Default,
            OvercoatSprite = member.OvercoatSprite,
            OvercoatColor = member.OvercoatColor,
            Accessory1Sprite = member.AccessorySprite1,
            Accessory1Color = member.AccessoryColor1,
            Accessory2Sprite = member.AccessorySprite2,
            Accessory2Color = member.AccessoryColor2,
            Accessory3Sprite = member.AccessorySprite3,
            Accessory3Color = member.AccessoryColor3
        };
}
