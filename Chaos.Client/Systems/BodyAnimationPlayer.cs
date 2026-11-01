using Chaos.Client.Data.Utilities;
using Chaos.Client.Models;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Systems;

public static class BodyAnimationPlayer
{
    public static void Start(
        WorldEntity entity,
        BodyAnimationArgs args,
        bool hidden,
        CreatureRenderer? creatureRenderer = null,
        AislingRenderer? aislingRenderer = null)
    {
        if ((entity.AnimState == EntityAnimState.BodyAnim)
            || (entity.ActiveEmoteFrame >= 0)
            || entity.IsPlayingCustomEmote)
            return;

        if (CustomEmoteRegistry.TryGet((int)args.BodyAnimation, out var customEmote))
        {
            if (!entity.IsRenderedAsCreatureSprite)
                entity.StartCustomEmote(customEmote, hidden);

            return;
        }

        if (entity.IsRenderedAsCreatureSprite)
        {
            var animInfo = creatureRenderer?.GetAnimInfo(entity.SpriteId);

            if (animInfo is { } info)
                AnimationSystem.StartCreatureBodyAnimation(
                    entity,
                    args.BodyAnimation,
                    args.AnimationSpeed,
                    in info,
                    hidden);
        } else
        {
            (_, var framesPerDir, _, _) = AnimationSystem.ResolveBodyAnimParams(args.BodyAnimation);

            if (framesPerDir > 0)
            {
                if (entity.Appearance.HasValue && !(aislingRenderer?.HasArmorAnimation(entity.Appearance.Value, args.BodyAnimation) ?? true))
                    return;

                AnimationSystem.StartBodyAnimation(entity, args.BodyAnimation, args.AnimationSpeed, hidden);
            } else if (DataUtilities.IsEmote(args.BodyAnimation))
            {
                (var startFrame, var frameCount, var durationMs) = AnimationSystem.ResolveEmoteFrames(args.BodyAnimation);

                if (startFrame >= 0)
                    entity.StartEmoteOverlay(startFrame, frameCount, durationMs, hidden);
            }
        }
    }
}
