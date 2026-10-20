using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>One mounted figure: an aisling look to render, or for the Sleigh a creature sprite.</summary>
public readonly record struct MountFigure(AislingAppearance? Look, ushort CreatureSprite);

/// <summary>Builds the look the server gives a rider (MountEffect.OnApplied, SetArenaOutfit).</summary>
public static class MountedLook
{
    public static MountFigure Build(AislingAppearance baseLook, MountWindowMount mount, DisplayColor color)
        => mount.IsSleigh
            ? new MountFigure(null, mount.Sprite)
            : new MountFigure(
                baseLook with
                {
                    BodySpriteId = AislingRenderer.HEAD_BODY_ID,
                    OvercoatSprite = mount.Sprite,
                    OvercoatColor = color
                },
                0);
}
