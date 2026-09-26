#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Every map-flag-driven ambient overlay — fog, lightning, the tints and mists, and the particle effects — switched
///     on and off together from the current <see cref="MapFlags" />. Several can be on at once (e.g. Leaves +
///     Fireflies, or Rain + Lightning + Fog). Snow and rain stay with <see cref="WeatherRenderer" /> and darkness with
///     <see cref="DarknessRenderer" />. Touched only on the game-loop thread.
/// </summary>
public sealed class AmbientEffects : IDisposable
{
    /// <summary>
    ///     The flag bits above the low byte. The map info packet carries only the low byte; these arrive separately, as
    ///     8 bytes, in the SetMapEffects packet.
    /// </summary>
    public const MapFlags EXTENDED_FLAGS = (MapFlags)0xFFFF_FFFF_FFFF_FF00UL;

    //draw order, back to front: cloud shadows first because they lie on the ground, then tints and mists, lightning
    //flashing through them, fog over the flash (as in the original storm), then particles last so glows and falling
    //things read on top of every haze
    private readonly (MapFlags Flag, IAmbientOverlay Overlay)[] Overlays =
    [
        (MapFlags.CloudShadows, new MistRenderer(MistStyle.CloudShadows)),
        (MapFlags.Underwater, new MistRenderer(MistStyle.Underwater)),
        (MapFlags.Heat, new MistRenderer(MistStyle.Heat)),
        (MapFlags.Gloom, new MistRenderer(MistStyle.Gloom)),
        (MapFlags.BloodMoon, new MistRenderer(MistStyle.BloodMoon)),
        (MapFlags.Miasma, new MistRenderer(MistStyle.Miasma)),
        (MapFlags.Radiance, new MistRenderer(MistStyle.Radiance)),
        (MapFlags.Arcane, new MistRenderer(MistStyle.Arcane)),
        (MapFlags.Frost, new MistRenderer(MistStyle.Frost)),
        (MapFlags.Dust, new MistRenderer(MistStyle.Dust)),
        (MapFlags.SeaSpray, new MistRenderer(MistStyle.SeaSpray)),
        (MapFlags.Sandstorm, new MistRenderer(MistStyle.Sandstorm)),
        (MapFlags.Blizzard, new MistRenderer(MistStyle.Blizzard)),
        (MapFlags.Lightning, new LightningRenderer()),
        (MapFlags.Fog, new MistRenderer(MistStyle.Fog)),
        (MapFlags.Sandstorm, new ParticleRenderer(ParticleStyle.SandGrains)),
        (MapFlags.Underwater, new ParticleRenderer(ParticleStyle.Bubbles)),
        (MapFlags.Ash, new ParticleRenderer(ParticleStyle.Ash)),
        (MapFlags.Ash, new ParticleRenderer(ParticleStyle.Embers)),
        (MapFlags.Leaves, new ParticleRenderer(ParticleStyle.Leaves)),
        (MapFlags.Petals, new ParticleRenderer(ParticleStyle.Petals)),
        (MapFlags.Fireflies, new ParticleRenderer(ParticleStyle.Fireflies)),
        (MapFlags.Radiance, new ParticleRenderer(ParticleStyle.RadianceMotes)),
        (MapFlags.Arcane, new ParticleRenderer(ParticleStyle.ArcaneSparks)),
        (MapFlags.Frost, new ParticleRenderer(ParticleStyle.FrostSparkles)),
        (MapFlags.Dust, new ParticleRenderer(ParticleStyle.DustMotes)),
        (MapFlags.SeaSpray, new ParticleRenderer(ParticleStyle.SeaSprayFlecks)),
        (MapFlags.Blizzard, new ParticleRenderer(ParticleStyle.BlizzardFlakes)),
        (MapFlags.Blizzard, new ParticleRenderer(ParticleStyle.BlizzardStreaks)),
        (MapFlags.Wisps, new ParticleRenderer(ParticleStyle.Wisps)),
        (MapFlags.Drips, new ParticleRenderer(ParticleStyle.Drips))
    ];

    /// <summary>Every map flag that switches on at least one overlay.</summary>
    public IReadOnlyCollection<MapFlags> CoveredFlags => Overlays.Select(entry => entry.Flag).ToHashSet();

    /// <summary>
    ///     Turns each overlay on or off to match <paramref name="flags" />. <paramref name="immediate" /> skips the
    ///     fades — use it on map change.
    /// </summary>
    public void Apply(MapFlags flags, bool immediate)
    {
        foreach (var (flag, overlay) in Overlays)
            overlay.SetActive(flags.HasFlag(flag), immediate);
    }

    /// <summary>Advances every overlay that is on or still fading out. Call once per frame.</summary>
    public void Update(GameTime gameTime, Rectangle viewport, Vector2 worldOrigin)
    {
        foreach (var (_, overlay) in Overlays)
            if (overlay.IsActive)
                overlay.Update(gameTime, viewport, worldOrigin);
    }

    /// <summary>
    ///     Draws every visible overlay in screen space, each in its own batch with the blend state it needs.
    /// </summary>
    public void Draw(
        SpriteBatch spriteBatch,
        Rectangle viewport,
        Vector2 worldOrigin,
        SamplerState samplerState,
        RasterizerState rasterizerState)
    {
        foreach (var (_, overlay) in Overlays)
        {
            if (!overlay.IsActive)
                continue;

            spriteBatch.Begin(blendState: overlay.BlendState, samplerState: samplerState, rasterizerState: rasterizerState);
            overlay.Draw(spriteBatch, viewport, worldOrigin);
            spriteBatch.End();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, overlay) in Overlays)
            overlay.Dispose();
    }
}
