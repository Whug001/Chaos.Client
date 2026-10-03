#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Draws the haunted-mirror scare: a frozen copy of the screen cracks and flies apart in shards, a creature behind
///     the glass lunges, then black. <see cref="Begin" /> takes the frozen frame; <see cref="End" /> frees its shards.
/// </summary>
public sealed class MirrorScareRenderer : IDisposable
{
    private static readonly Color ShardTint = new(225, 232, 255);
    private const float FLASH_ALPHA = 0.35f;

    private readonly Texture2D[][] CreatureFrames;
    private readonly GraphicsDevice Device;
    private readonly Texture2D Pixel;
    private ScareCreature Creature;
    private ShatterPattern? Pattern;
    private Texture2D?[] ShardTextures = [];

    public bool IsActive => Pattern is not null;

    public MirrorScareRenderer(GraphicsDevice device)
    {
        Device = device;
        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData([Color.White]);

        CreatureFrames = Enum.GetValues<ScareCreature>()
                             .Select(creature => Enumerable.Range(0, MirrorScareTimeline.FrameCount(creature))
                                                           .Select(frame => LoadFrame(device, MirrorScareTimeline.ResourceName(creature, frame)))
                                                           .ToArray())
                             .ToArray();
    }

    public void Begin(Color[] frozenFrame, int width, int height, ScareCreature creature, int seed)
    {
        End();

        Creature = creature;
        Pattern = ShatterPattern.Build(seed, width, height);

        var pieces = Pattern.Cut(frozenFrame);
        ShardTextures = new Texture2D?[pieces.Length];

        for (var i = 0; i < pieces.Length; i++)
        {
            var bounds = Pattern.Shards[i].Bounds;

            if (bounds.IsEmpty)
                continue;

            var texture = new Texture2D(Device, bounds.Width, bounds.Height);
            texture.SetData(pieces[i]);
            ShardTextures[i] = texture;
        }
    }

    public void End()
    {
        foreach (var texture in ShardTextures)
            texture?.Dispose();

        ShardTextures = [];
        Pattern = null;
    }

    /// <summary>Draws one frame of the scare into an open sprite batch, scaled to <paramref name="screen" />.</summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle screen, double secondsInto, Random shake)
    {
        if (Pattern is null)
            return;

        var phase = MirrorScareTimeline.PhaseAt(secondsInto);

        switch (phase)
        {
            case ScarePhase.Done:
                return;
            case ScarePhase.Black:
                spriteBatch.Draw(Pixel, screen, Color.Black);

                return;
            case ScarePhase.Shatter:
                spriteBatch.Draw(Pixel, screen, Color.Black);
                DrawCreature(spriteBatch, screen, secondsInto, shake);

                break;
        }

        var scale = new Vector2((float)screen.Width / Pattern.Width, (float)screen.Height / Pattern.Height);
        var sinceBreak = secondsInto - MirrorScareTimeline.CRACK_SECONDS;

        foreach (var shard in Pattern.Shards)
        {
            if (ShardTextures[shard.Index] is not { } texture)
                continue;

            var (offset, rotation) = phase == ScarePhase.Crack ? (Vector2.Zero, 0f) : ShatterPattern.Motion(shard, sinceBreak);
            var position = shard.Centroid + offset;

            if ((position.Y - shard.Bounds.Height > Pattern.Height) || (Math.Abs(offset.X) > Pattern.Width + shard.Bounds.Width))
                continue;

            spriteBatch.Draw(
                texture,
                new Vector2(screen.X, screen.Y) + position * scale,
                null,
                phase == ScarePhase.Crack ? Color.White : ShardTint,
                rotation,
                shard.Centroid - shard.Bounds.Location.ToVector2(),
                scale,
                SpriteEffects.None,
                0);
        }

        if (secondsInto < MirrorScareTimeline.FLASH_SECONDS)
            spriteBatch.Draw(Pixel, screen, Color.White * (FLASH_ALPHA * (1 - (float)(secondsInto / MirrorScareTimeline.FLASH_SECONDS))));
    }

    private void DrawCreature(SpriteBatch spriteBatch, Rectangle screen, double secondsInto, Random shake)
    {
        var frames = CreatureFrames[(int)Creature];
        var frame = frames[MirrorScareTimeline.FrameAt(Creature, secondsInto)];
        var size = MirrorScareTimeline.CreatureScale(secondsInto);
        var width = (int)(screen.Width * size);
        var height = (int)(screen.Height * size);
        var jitter = MirrorScareTimeline.IsShaking(secondsInto) ? shake.Next(-MirrorScareTimeline.SHAKE_PIXELS, MirrorScareTimeline.SHAKE_PIXELS + 1) : 0;

        spriteBatch.Draw(
            frame,
            new Rectangle(screen.Center.X - width / 2 + jitter, screen.Center.Y - height / 2 + jitter / 2, width, height),
            Color.White);
    }

    private static Texture2D LoadFrame(GraphicsDevice device, string resourceName)
    {
        using var stream = typeof(MirrorScareRenderer).Assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"missing embedded scare frame {resourceName}");

        return Texture2D.FromStream(device, stream);
    }

    public void Dispose()
    {
        End();
        Pixel.Dispose();

        foreach (var frames in CreatureFrames)
            foreach (var frame in frames)
                frame.Dispose();
    }
}
