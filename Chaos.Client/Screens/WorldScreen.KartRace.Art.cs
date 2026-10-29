#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.Karting;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private const int TREAD_PHASES = 3;

    //tiles a kart travels per tread step: slower than true rolling, which would step faster than the frame rate and look backwards
    private const double TREAD_STEP_TILES = 0.6;

    private readonly Dictionary<byte, (double Travel, double Last)> KartWheelTravel = [];
    private KartHeadRenderer? KartHeads;

    private uint? KartHazardGoStep
        => KartRace.Stage is KartRaceStage.Waiting or KartRaceStage.Results or KartRaceStage.Done ? null : KartRace.GoStep;

    private static int KartArtFrame(string key, uint step)
        => (int)(step / 4 % (uint)KartArtAssets.ReadManifest()[key].Frames);

    private void DrawKartSprite(SpriteBatch batch, string key, Vector2 position, int frame, Color tint, float rotation = 0f)
    {
        var sheet = KartArtAssets.ReadManifest()[key];
        var texture = KartArtAssets.Texture(Device, key);
        batch.Draw(texture, position, sheet.Frame(frame), tint, rotation,
            new Vector2(sheet.AnchorX, sheet.AnchorY), 1f, SpriteEffects.None, 0f);
    }

    private void DrawKartHazardsGround(SpriteBatch batch)
    {
        var step = KartClock.Estimate(TumbleClock.Now);

        foreach (var hazard in KartRace.Hazards)
        {
            var phase = KartHazardVisuals.Phase(hazard, step, KartHazardGoStep);
            var tint = phase == KartHazardPhase.Dormant ? new Color(95, 95, 95) : Color.White;

            for (var y = hazard.Y0; y <= hazard.Y1; y++)
                for (var x = hazard.X0; x <= hazard.X1; x++)
                    DrawKartSprite(batch, "fire_warning", KartWorldToScreen(x + 0.5, y + 0.5), KartArtFrame("fire_warning", step), tint);
        }
    }

    private void DrawKartHazardsAtDepth(SpriteBatch batch, int depth)
    {
        var step = KartClock.Estimate(TumbleClock.Now);

        foreach (var hazard in KartRace.Hazards)
        {
            if (KartHazardVisuals.Phase(hazard, step, KartHazardGoStep) != KartHazardPhase.Active)
                continue;

            for (var y = hazard.Y0; y <= hazard.Y1; y++)
                for (var x = hazard.X0; x <= hazard.X1; x++)
                    if (x + y == depth)
                        DrawKartSprite(batch, "fire_active", KartWorldToScreen(x + 0.5, y + 0.5), KartArtFrame("fire_active", step), Color.White);
        }
    }
    private void DrawKartRacer(SpriteBatch batch, KartDraw draw, double now)
    {
        var latest = KartRace.Latest(draw.Index);
        var dye = KartRace.Racers.TryGetValue(draw.Index, out var info) ? KartLayerTint.Dye(info.Dye) : Color.White;
        var slowed = latest?.Flags.HasFlag(KartRacerFlags.Slowed) == true;
        var step = KartClock.Estimate(now);
        var mods = draw.Index == KartRace.LocalIndex && !KartRace.LocalFinished && KartPredictorInitialized && KartPredictor is not null
            ? KartPredictor.Mods
            : latest is not null ? KartRaceState.ToMods(latest) : default;
        var screenAngle = KartFrames.FrameFor(draw.Heading) * Math.Tau / KartFrames.FRAMES;
        var direction = new Vector2((float)Math.Cos(screenAngle), (float)Math.Sin(screenAngle));
        var head = KartRacerHead(info, draw.Frame);
        var boosting = mods.BoostSeconds > 0;

        //a kart facing the camera has its back further away, so the flame goes behind it
        var flameBehind = direction.Y >= 0;
        DrawKartSprite(batch, "shadow", draw.Center, 0, KartLayerTint.For(KartLayer.Shadow, dye) * 0.65f);

        if (latest?.Flags.HasFlag(KartRacerFlags.Invincible) == true)
            DrawKartSprite(batch, "mantle", draw.Center, KartArtFrame("mantle", step), Color.White * (0.55f + 0.2f * (float)Math.Sin(now * 8)));

        if (boosting && flameBehind)
            DrawKartBoostFlame(batch, draw.Center, direction, screenAngle, step);

        var spark = KartRace.Stats is { } stats && mods.Drifting ? DriftModel.SparkFor(mods.DriftCharge, stats) : SparkLevel.None;
        var sparkKey = spark switch { SparkLevel.Blue => "spark_blue", SparkLevel.Gold => "spark_gold", SparkLevel.Violet => "spark_violet", _ => null };

        if (sparkKey is not null)
        {
            var side = new Vector2(-direction.Y, direction.X) * (mods.DriftDirection >= 0 ? 1 : -1);
            DrawKartSprite(batch, sparkKey, draw.Center - direction * 15 + side * 9, KartArtFrame(sparkKey, step), Color.White);
        }

        var baseTint = KartSlowTint(KartLayerTint.For(KartLayer.Base, dye), slowed);
        DrawKartSprite(batch, head is null ? "kart_base" : "kart_base_bare", draw.Center, draw.Frame, baseTint);
        DrawKartSprite(batch, "kart_treads", draw.Center, KartTreadPhase(draw, now) * KartFrames.FRAMES + draw.Frame, baseTint);
        DrawKartSprite(batch, head is null ? "kart_driver" : "kart_driver_body", draw.Center, draw.Frame, KartSlowTint(KartLayerTint.For(KartLayer.Driver, dye), slowed));

        if (head is not null)
            batch.Draw(head.Texture, draw.Center + KartHead.NeckOffset(draw.Frame), null, KartSlowTint(Color.White, slowed), 0f, head.Origin, 1f, SpriteEffects.None, 0f);

        if (boosting && !flameBehind)
            DrawKartBoostFlame(batch, draw.Center, direction, screenAngle, step);

        if (draw.Index == KartRace.LocalIndex)
            return;

        var name = KartRacerName(draw.Index);
        var width = TextRenderer.MeasureWidth(name);
        var position = draw.Center - new Vector2(width / 2f, KartArtAssets.ReadManifest()["kart_base"].AnchorY + TextRenderer.CHAR_HEIGHT + 2);
        TextRenderer.DrawText(batch, position, name, draw.Index == KartFollowedDrawIndex ? LegendColors.Gold : Color.White, false);
    }

    /// <summary>The racer's own head, or null to keep the helmeted driver (no roster entry, no entity yet, or a creature form).</summary>
    private KartHeadSprite? KartRacerHead(KartRacerInfo? info, int frame)
    {
        if (info is null || WorldState.GetEntity(info.EntityId)?.Appearance is not { } look)
            return null;

        KartHeads ??= new KartHeadRenderer(Game.AislingRenderer);
        var (front, flip) = KartHead.Facing(frame);

        return KartHeads.Get(KartHeadRenderer.HeadLook(in look, info.HairStyle, info.HairColor), front, flip);
    }

    /// <summary>
    ///     The flame art points right with its hot core at (6, 16). It trails from the back of the kart's footprint, an
    ///     isometric ellipse, at exhaust height.
    /// </summary>
    private void DrawKartBoostFlame(SpriteBatch batch, Vector2 center, Vector2 direction, double screenAngle, uint step)
    {
        const float HALF_LENGTH = 26f;
        const float HALF_WIDTH = 13f;
        const float EXHAUST_HEIGHT = 12f;

        var reach = 1f / MathF.Sqrt(direction.X * direction.X / (HALF_LENGTH * HALF_LENGTH) + direction.Y * direction.Y / (HALF_WIDTH * HALF_WIDTH));
        var position = center - direction * reach - new Vector2(0, EXHAUST_HEIGHT);
        var sheet = KartArtAssets.ReadManifest()["boost_flame"];
        batch.Draw(KartArtAssets.Texture(Device, "boost_flame"), position, sheet.Frame(KartArtFrame("boost_flame", step)), Color.White,
            (float)(screenAngle + Math.PI), new Vector2(6, 16), 1f, SpriteEffects.None, 0f);
    }

    /// <summary>Which tread step the wheels show, from the distance the kart has rolled; driving backwards turns them back.</summary>
    private int KartTreadPhase(KartDraw draw, double now)
    {
        var (travel, last) = KartWheelTravel.GetValueOrDefault(draw.Index, (0, now));
        travel += draw.ForwardSpeed * Math.Clamp(now - last, 0, 0.1);
        KartWheelTravel[draw.Index] = (travel, now);
        var phase = (long)Math.Floor(travel / TREAD_STEP_TILES) % TREAD_PHASES;

        return (int)(phase < 0 ? phase + TREAD_PHASES : phase);
    }

    private static Color KartSlowTint(Color tint, bool slowed)
        => slowed ? new Color(tint.R * 150 / 255, tint.G * 150 / 255, tint.B * 170 / 255, tint.A) : tint;

    private void DrawKartItemHud(SpriteBatch batch, byte wisps)
    {
        var left = (Camera.ViewportWidth - 48) / 2f;
        UIElement.DrawRect(batch, new Rectangle((int)left + 8, 12, 32, 32), Color.Black * 0.65f);
        DrawKartSprite(batch, "hud_slot", new Vector2(left, 4), 0, Color.White);
        var shown = KartItemSlot.ShownKind(KartRace.HeldItem, KartRace.RollTicks, KartHudFrame);

        if (KartItemArt.IconKey(shown) is { } icon)
            DrawKartSprite(batch, icon, new Vector2(left + 24, 28), 0, Color.White);
        else
            TextRenderer.DrawText(batch, new Vector2(left + 21, 22), "-", Color.White, false);

        var caption = KartItemSlot.Text(KartRace.HeldItem, KartRace.RollTicks, KartHudFrame);
        TextRenderer.DrawText(batch, new Vector2(left + 24 - TextRenderer.MeasureWidth(caption) / 2f, 54), caption, Color.White, false);
        DrawKartSprite(batch, "wisp", new Vector2(left + 62, 28), KartArtFrame("wisp", KartClock.Estimate(TumbleClock.Now)), Color.White);
        TextRenderer.DrawText(batch, new Vector2(left + 74, 22), KartItemSlot.WispText(wisps), Color.White, false);
    }
}
