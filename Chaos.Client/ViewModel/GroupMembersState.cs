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

    /// <summary>
    ///     The member's advanced class, or <see cref="DarkAges.Definitions.AdvClass.None" /> if they have not
    ///     advanced. What the panel names them when it is set.
    /// </summary>
    public required AdvClass AdvClass { get; init; }

    /// <summary>Whether this member leads the group. Marked with a star on their panel.</summary>
    public required bool IsLeader { get; init; }
    public required uint CurrentHp { get; init; }
    public required uint MaximumHp { get; init; }
    public required uint CurrentMp { get; init; }
    public required uint MaximumMp { get; init; }

    /// <summary>What to draw in the portrait, already in the renderer's own shape.</summary>
    public required AislingAppearance Appearance { get; init; }

    /// <summary>The member's effects, soonest to expire first. At most five -- what the panel has room for.</summary>
    public required IReadOnlyList<GroupMemberEffect> Effects { get; init; }

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
/// <summary>
///     One effect on a member's panel: which icon to draw, and the colour band that draws the bar under it.
/// </summary>
/// <remarks>
///     The band is carried rather than a time remaining because that is what the player's own effects bar draws
///     from, and the two readouts should not disagree about the same effect. See
///     <c>EffectSlotControl.GetBarPercent</c> for the seven steps.
/// </remarks>
public readonly record struct GroupMemberEffect(byte Icon, EffectColor Color);

public sealed class GroupMembersState
{
    private static readonly IReadOnlyList<GroupMemberEffect> NoEffects = [];

    public IReadOnlyList<GroupMemberSnapshot> Members { get; private set; } = [];

    /// <summary>
    ///     How many are in the group altogether, the player included, as the server reported it.
    /// </summary>
    /// <remarks>
    ///     Not <see cref="Members" />.Count. That list is the other members -- the player's own row is never sent,
    ///     because their vitals are already on the hud -- so it is one short of the group.
    /// </remarks>
    public int Size { get; private set; }

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

        //an older server that does not send the size leaves it 0; falling back keeps the count sane rather than
        //reporting an empty group to a player who can see five panels
        Size = args.GroupSize > 0 ? args.GroupSize : members.Count;

        Changed?.Invoke();
    }

    /// <summary>Drops every member. Called on logout and on disconnect.</summary>
    public void Reset()
    {
        if (Members.Count == 0)
            return;

        Members = [];
        Size = 0;
        Changed?.Invoke();
    }

    private static GroupMemberSnapshot ToSnapshot(GroupMemberInfo member)
    {
        var effects = NoEffects;

        if (member.Effects.Count > 0)
        {
            var list = new List<GroupMemberEffect>(member.Effects.Count);

            foreach (var effect in member.Effects)
                list.Add(new GroupMemberEffect(effect.Icon, effect.Color));

            effects = list;
        }

        return new GroupMemberSnapshot
        {
            Name = member.Name,
            BaseClass = member.BaseClass,
            AdvClass = member.AdvClass,
            IsLeader = member.IsLeader,
            CurrentHp = member.CurrentHp,
            MaximumHp = member.MaximumHp,
            CurrentMp = member.CurrentMp,
            MaximumMp = member.MaximumMp,
            Appearance = ToAppearance(member),
            Effects = effects
        };
    }

    /// <summary>
    ///     Builds the renderer's appearance from the slice of it the packet carries.
    /// </summary>
    /// <remarks>
    ///     Mirrors the <c>DisplayAisling</c> mapping in <see cref="WorldState" />, including the ghost body-colour
    ///     substitution, and shares its body-sprite lookup rather than repeating the table. The fields the packet
    ///     leaves out -- boots, pants, weapon, shield -- are all below a head-and-shoulders crop, so they default.
    ///     <para />
    ///     The three accessory layers are deliberately dropped, which is where helmets, overhelms and every other
    ///     piece of headwear are drawn. They are the layers that sit on top of the head, and at a portrait this
    ///     small a tall hat is most of what there is to see -- the point of the portrait is telling one member
    ///     from another, and a group all wearing the same helm cannot be told apart at all. Zero means skip the
    ///     layer, per <see cref="AislingAppearance" />.
    ///     <para />
    ///     The server still sends them. Dropping them here rather than at the packet keeps the decision next to
    ///     the portrait it is made for, and leaves them there if the panel ever wants them back.
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
            OvercoatColor = member.OvercoatColor
        };
}
