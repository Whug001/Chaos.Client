#region
using Chaos.Client.Collections;
using Chaos.Client.Data.Models;
using Chaos.Client.ViewModel;
using Chaos.Extensions.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Extensions;

/// <summary>
///     Scores an ability the player is looking at in the metadata tab, and turns that score into an icon. Shared by the
///     list rows and the details popup — they render the same entry side by side, so they must agree.
/// </summary>
public static class AbilityMetadataExtensions
{
    private const string LEARNABLE_RAMP_NAME = "learnable";
    private const string LOCKED_RAMP_NAME = "locked";

    /// <summary>
    ///     Black outline, dark teal shadows, mint highlights — fitted to the retail "can learn" icon sheet.
    /// </summary>
    private static readonly Color[] LearnableRamp =
    [
        new(4, 2, 4),
        new(13, 89, 113),
        new(63, 140, 145),
        new(147, 183, 179),
        new(225, 228, 207),
        new(252, 254, 252)
    ];

    /// <summary>
    ///     A plain black-to-white ramp, i.e. a straight desaturation — matches the retail "cannot learn" icon sheet.
    /// </summary>
    private static readonly Color[] LockedRamp = [new(6, 5, 5), new(254, 255, 254)];

    /// <summary>
    ///     Whether the player knows <paramref name="name" /> at <paramref name="requiredLevel" /> or better. A null name
    ///     means the ability has no prerequisite in that slot, which counts as met.
    /// </summary>
    public static bool HasPreRequisite(string? name, byte requiredLevel)
    {
        if (name is null)
            return true;

        for (byte i = 1; i <= SpellBook.MAX_SLOTS; i++)
        {
            ref readonly var slot = ref WorldState.SpellBook.GetSlot(i);

            if (slot.IsOccupied && (slot.AbilityName?.EqualsI(name) == true) && (slot.CurrentLevel >= requiredLevel))
                return true;
        }

        for (byte i = 1; i <= SkillBook.MAX_SLOTS; i++)
        {
            ref readonly var slot = ref WorldState.SkillBook.GetSlot(i);

            if (slot.IsOccupied && (slot.AbilityName?.EqualsI(name) == true) && (slot.CurrentLevel >= requiredLevel))
                return true;
        }

        return false;
    }

    extension(AbilityMetadataEntry entry)
    {
        /// <summary>
        ///     Scores this ability against the player's books, stats, and prerequisites.
        /// </summary>
        public AbilityIconState ResolveIconState()
        {
            if (entry.IsKnown())
                return AbilityIconState.Known;

            if (WorldState.Attributes.Current is not { } attrs
                || (entry.RequiresMaster && !WorldState.IsMaster)
                || ((entry.AbilityLevel > 0) && (attrs.Ability < entry.AbilityLevel)))
                return AbilityIconState.Locked;

            //master and ability-gated abilities carry no meaningful level requirement, so only level-gated ones check it
            if (entry is { RequiresMaster: false, AbilityLevel: 0 } && (attrs.Level < entry.Level))
                return AbilityIconState.Locked;

            if ((attrs.Str < entry.Str)
                || (attrs.Int < entry.Int)
                || (attrs.Wis < entry.Wis)
                || (attrs.Dex < entry.Dex)
                || (attrs.Con < entry.Con)
                || !HasPreRequisite(entry.PreReq1Name, entry.PreReq1Level)
                || !HasPreRequisite(entry.PreReq2Name, entry.PreReq2Level))
                return AbilityIconState.Locked;

            return AbilityIconState.Learnable;
        }

        private bool IsKnown()
        {
            if (entry.IsSpell)
            {
                for (byte i = 1; i <= SpellBook.MAX_SLOTS; i++)
                {
                    ref readonly var slot = ref WorldState.SpellBook.GetSlot(i);

                    if (slot.IsOccupied && (slot.AbilityName?.EqualsI(entry.Name) == true))
                        return true;
                }

                return false;
            }

            for (byte i = 1; i <= SkillBook.MAX_SLOTS; i++)
            {
                ref readonly var slot = ref WorldState.SkillBook.GetSlot(i);

                if (slot.IsOccupied && (slot.AbilityName?.EqualsI(entry.Name) == true))
                    return true;
            }

            return false;
        }
    }

    extension(AbilityIconState state)
    {
        /// <summary>
        ///     Returns the icon to draw for <paramref name="entry" /> in this state. Every state is derived from the one
        ///     normal icon sheet by gradient-mapping it: mint for abilities the player can learn, grey for the rest.
        /// </summary>
        public Texture2D ResolveIcon(AbilityMetadataEntry entry)
        {
            var renderer = UiRenderer.Instance!;

            var key = entry.IsSpell ? $"spell:{entry.IconSprite}" : $"skill:{entry.IconSprite}";

            var icon = entry.IsSpell ? renderer.GetSpellIcon(entry.IconSprite) : renderer.GetSkillIcon(entry.IconSprite);

            return state switch
            {
                AbilityIconState.Learnable => renderer.GetGradientMappedTexture(key, icon, LEARNABLE_RAMP_NAME, LearnableRamp),
                AbilityIconState.Locked    => renderer.GetGradientMappedTexture(key, icon, LOCKED_RAMP_NAME, LockedRamp),
                _                          => icon
            };
        }
    }
}