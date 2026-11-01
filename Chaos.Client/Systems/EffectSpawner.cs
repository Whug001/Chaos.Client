using Chaos.Client.Data;
using Chaos.Client.Data.Definitions;
using Chaos.Client.Models;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Systems;

public static class EffectSpawner
{
    public static Func<int, (int FrameCount, int FileIntervalMs, bool IsEfa, EffectBlendMode BlendMode)?> EffectInfoResolver { get; set; } =
        _ => null;

    public static void CreateEffect(
        List<Animation> effects,
        int effectId,
        ushort speed,
        uint? targetEntityId = null,
        int? tileX = null,
        int? tileY = null,
        bool isBackground = false)
    {
        var info = EffectInfoResolver(effectId);

        if (info is null)
            return;

        CreateEffect(effects, effectId, speed, info.Value, targetEntityId, tileX, tileY, isBackground);
    }

    public static void CreateEffect(
        List<Animation> effects,
        int effectId,
        ushort speed,
        (int FrameCount, int FileIntervalMs, bool IsEfa, EffectBlendMode BlendMode) resolved,
        uint? targetEntityId = null,
        int? tileX = null,
        int? tileY = null,
        bool isBackground = false)
    {
        (var frameCount, var fileIntervalMs, var isEfa, var blendMode) = resolved;

        float frameIntervalMs = isEfa
            ? fileIntervalMs > 0 ? fileIntervalMs : 50
            : speed > 0
                ? speed
                : 50;

        if (targetEntityId.HasValue)
        {
            effects.RemoveAll(e => (e.TargetEntityId == targetEntityId) && (e.EffectId == effectId));

            var maxPerEntity = ClientSettings.MaxEffectAnimationsPerEntity;

            if (maxPerEntity <= 0)
                return;

            while (effects.Count(e => e.TargetEntityId == targetEntityId) >= maxPerEntity)
            {
                var oldestIndex = effects.FindIndex(e => e.TargetEntityId == targetEntityId);

                if (oldestIndex < 0)
                    break;

                effects.RemoveAt(oldestIndex);
            }
        }

        effects.Add(
            new Animation
            {
                EffectId = effectId,
                TargetEntityId = targetEntityId,
                TileX = tileX,
                TileY = tileY,
                FrameCount = frameCount,
                FrameIntervalMs = frameIntervalMs,
                BlendMode = blendMode,
                IsBackground = isBackground
            });
    }

    public static void CreateProjectile(
        List<Projectile> projectiles,
        int meffectId,
        WorldEntity source,
        WorldEntity target,
        int mapHeight)
    {
        var record = DataContext.Effects.GetMeffectRecord(meffectId);

        if (record is null)
            return;

        var sourceWorld = Camera.TileToWorld(source.TileX, source.TileY, mapHeight);
        var targetWorld = Camera.TileToWorld(target.TileX, target.TileY, mapHeight);

        var srcX = sourceWorld.X + DaLibConstants.HALF_TILE_WIDTH;
        var srcY = sourceWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;
        var tgtX = targetWorld.X + DaLibConstants.HALF_TILE_WIDTH;
        var tgtY = targetWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;

        var dx = tgtX - srcX;
        var dy = tgtY - srcY;
        var distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance < 1f)
            return;

        var direction = GetProjectileDirection(target.TileX - source.TileX, target.TileY - source.TileY);

        projectiles.Add(
            new Projectile
            {
                TargetEntityId = target.Id,
                MeffectId = meffectId,
                CurrentX = srcX,
                CurrentY = srcY,
                LastKnownTargetX = tgtX,
                LastKnownTargetY = tgtY,
                Step = record.Step,
                StepDelayMs = record.StepDelay,
                InitialDistance = distance,
                ArcRatioV = record.ArcRatioV,
                ArcRatioH = record.ArcRatioH,
                FramesPerDirection = record.FramesPerDirection,
                Direction = direction,
                MapHeight = mapHeight
            });
    }

    public static void Advance(
        List<Animation> effects,
        List<Projectile> projectiles,
        float elapsedMs,
        Func<uint, WorldEntity?> lookup)
    {
        AdvanceEffects(effects, elapsedMs);
        AdvanceProjectiles(projectiles, elapsedMs, lookup);
    }

    public static void AdvanceEffects(List<Animation> effects, float elapsedMs)
    {
        for (var i = effects.Count - 1; i >= 0; i--)
        {
            var effect = effects[i];
            effect.ElapsedMs += elapsedMs;

            while (effect.ElapsedMs >= effect.FrameIntervalMs)
            {
                effect.CurrentFrame++;
                effect.ElapsedMs -= effect.FrameIntervalMs;
            }

            if (effect.IsComplete)
                effects.RemoveAt(i);
        }
    }

    public static void AdvanceProjectiles(List<Projectile> projectiles, float elapsedMs, Func<uint, WorldEntity?> lookup)
    {
        for (var i = projectiles.Count - 1; i >= 0; i--)
        {
            var proj = projectiles[i];
            proj.ElapsedMs += elapsedMs;

            if (proj.StepDelayMs <= 0f || proj.Step <= 0)
            {
                proj.IsComplete = true;
                projectiles.RemoveAt(i);

                continue;
            }

            const int MAX_STEPS_PER_FRAME = 64;
            var steps = 0;

            while ((proj.ElapsedMs >= proj.StepDelayMs) && !proj.IsComplete && (steps < MAX_STEPS_PER_FRAME))
            {
                proj.ElapsedMs -= proj.StepDelayMs;
                AdvanceProjectileStep(proj, lookup);
                steps++;
            }

            if (proj.IsComplete)
                projectiles.RemoveAt(i);
        }
    }

    private static void AdvanceProjectileStep(Projectile proj, Func<uint, WorldEntity?> lookup)
    {
        var targetEntity = lookup(proj.TargetEntityId);

        if (targetEntity is not null)
        {
            var targetWorld = Camera.TileToWorld(targetEntity.TileX, targetEntity.TileY, proj.MapHeight);
            proj.LastKnownTargetX = targetWorld.X + DaLibConstants.HALF_TILE_WIDTH;
            proj.LastKnownTargetY = targetWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;
        }

        var dx = proj.LastKnownTargetX - proj.CurrentX;
        var dy = proj.LastKnownTargetY - proj.CurrentY;
        var distSq = dx * dx + dy * dy;
        var stepSq = (float)proj.Step * proj.Step;

        if (distSq <= stepSq)
        {
            proj.IsComplete = true;

            if (targetEntity is not null)
                targetEntity.HitTintExpiryMs = HIT_TINT_FLASH_MS;

            return;
        }

        var remainingDistance = MathF.Sqrt(distSq);
        var unitX = dx / remainingDistance;
        var unitY = dy / remainingDistance;

        proj.CurrentX += unitX * proj.Step;
        proj.CurrentY += unitY * proj.Step;
        proj.DistanceTraveled += proj.Step;

        if (proj is { ArcRatioV: not null, ArcRatioH: not null, InitialDistance: > 0 })
        {
            var progress = Math.Clamp(proj.DistanceTraveled / proj.InitialDistance, 0f, 1f);
            var arcHeight = proj.InitialDistance * proj.ArcRatioV.Value / proj.ArcRatioH.Value / 2f;
            var arcOffset = MathF.Sin(MathF.PI * progress) * arcHeight;

            proj.ArcOffsetX = -unitY * arcOffset;
            proj.ArcOffsetY = unitX * arcOffset;
        }

        if (targetEntity is not null)
        {
            var projTile = Camera.WorldToTile(proj.CurrentX, proj.CurrentY, proj.MapHeight);

            proj.Direction = GetProjectileDirection(targetEntity.TileX - projTile.X, targetEntity.TileY - projTile.Y);
        }

        if (proj.FramesPerDirection > 1)
            proj.CurrentFrameCycle = (proj.CurrentFrameCycle + 1) % proj.FramesPerDirection;
    }

    private static int GetProjectileDirection(int dtx, int dty)
    {
        var absDtx = Math.Abs(dtx);
        var absDty = Math.Abs(dty);

        if (absDtx > absDty)
            return dtx > 0 ? 1 : 3;

        if (absDty > 0)
            return dty < 0 ? 0 : 2;

        return 0;
    }

    private const float HIT_TINT_FLASH_MS = 100f;
}
