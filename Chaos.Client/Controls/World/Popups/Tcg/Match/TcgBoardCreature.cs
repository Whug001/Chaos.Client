#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

public sealed class TcgBoardCreature(CreatureRenderer creatures)
{
    public enum Pose : byte
    {
        Idle,
        Walk,
        Attack
    }

    private const int BOX = 48;
    private const int WALK_FRAME_MS = 120;
    private const int ATTACK_CYCLE_MS = 900;
    private const int MIN_FRAME_MS = 120;

    private static readonly Color Parchment = new(216, 196, 150);
    private static readonly Color Ink = new(60, 40, 20);

    /// <summary>Draws the creature's frame fitted into a 48x48 box whose bottom centre is at feet.</summary>
    public void Draw(
        SpriteBatch batch,
        TcgCatalogCardView card,
        Point feet,
        Direction facing,
        Pose pose,
        int clockMs,
        Color tint,
        float scale = 1f)
    {
        if (card.ArtKind == TcgArtKind.Item)
        {
            DrawItem(batch, card, feet, tint);

            return;
        }

        if ((card.ArtKind != TcgArtKind.Creature) || !DrawCreature(batch, card, feet, facing, pose, clockMs, tint, scale))
            DrawMissing(batch, card, feet, tint);
    }

    private bool DrawCreature(
        SpriteBatch batch,
        TcgCatalogCardView card,
        Point feet,
        Direction facing,
        Pose pose,
        int clockMs,
        Color tint,
        float scale)
    {
        if (creatures.GetAnimInfo(card.ArtId) is not { } info)
            return false;

        var (start, count, flip) = pose switch
        {
            Pose.Walk => AnimationSystem.GetCreatureWalkCycle(in info, facing),
            Pose.Attack => AnimationSystem.GetCreatureAttackCycle(in info, facing),
            _ => IdleCycle(in info, facing)
        };

        int frame;
        bool animatedFlip = flip;

        if (count <= 1)
        {
            (frame, animatedFlip) = AnimationSystem.GetCreatureIdleFrame(in info, facing);
        } else if (pose == Pose.Attack)
            frame = start + ((clockMs % ATTACK_CYCLE_MS) / Math.Max(1, ATTACK_CYCLE_MS / count));
        else
        {
            var interval = pose == Pose.Walk ? WALK_FRAME_MS : Math.Max(MIN_FRAME_MS, info.AnimationIntervalMs);
            frame = start + ((clockMs / interval) % count);
        }

        var sprite = creatures.GetFrame(card.ArtId, frame) ?? creatures.GetFrame(card.ArtId, start);

        if (sprite is not { } resolved || resolved.Texture is not { } texture)
            return false;

        var box = Math.Max(1, (int)(BOX * scale));
        var fit = Math.Min(box / (float)texture.Width, box / (float)texture.Height);
        var width = Math.Max(1, (int)(texture.Width * fit));
        var height = Math.Max(1, (int)(texture.Height * fit));
        var dest = new Rectangle(feet.X - (width / 2), feet.Y - height, width, height);

        DrawFitted(batch, texture, dest, tint, animatedFlip);

        return true;
    }

    private static (int StartFrame, int FrameCount, bool Flip) IdleCycle(in CreatureAnimInfo info, Direction facing)
    {
        if (info.OptionalAnimationFrameCount > 1)
        {
            var (idle, flip) = AnimationSystem.GetCreatureIdleFrame(in info, facing);

            return (idle, info.OptionalAnimationFrameCount, flip);
        }

        return AnimationSystem.GetCreatureWalkCycle(in info, facing);
    }

    private static void DrawItem(SpriteBatch batch, TcgCatalogCardView card, Point feet, Color tint)
    {
        if (UiRenderer.Instance is { } ui)
        {
            batch.Draw(ui.GetSpfTexture(TcgMatchArt.PEDESTAL), new Rectangle(feet.X - 20, feet.Y - 24, 40, 24), tint);

            var icon = ui.GetItemIcon(card.ArtId);

            if ((icon.Width > 0) && (icon.Height > 0))
                DrawFitted(batch, icon, new Rectangle(feet.X - (icon.Width / 2), feet.Y - 24 - icon.Height, icon.Width, icon.Height), tint, false);
        }
    }

    private static void DrawMissing(SpriteBatch batch, TcgCatalogCardView card, Point feet, Color tint)
    {
        var box = new Rectangle(feet.X - 16, feet.Y - 32, 32, 32);
        batch.Draw(UIElement.GetPixel(), box, MultiplyTint(Parchment, tint));

        if ((card.Name.Length > 0) && (TextRenderer.MeasureWidth(card.Name.AsSpan(0, 1)) is var letterWidth))
        {
            var at = new Vector2(box.X + ((box.Width - letterWidth) / 2f), box.Y + ((box.Height - TextRenderer.CHAR_HEIGHT) / 2f));
            TextRenderer.DrawText(batch, at, card.Name.AsSpan(0, 1).ToString(), MultiplyTint(Ink, tint), false);
        }
    }

    private static void DrawFitted(SpriteBatch batch, Texture2D texture, Rectangle dest, Color tint, bool flip)
    {
        Texture2D actual = texture;
        var source = new Rectangle(0, 0, texture.Width, texture.Height);

        if (texture is CachedTexture2D { AtlasRegion: { } region })
        {
            actual = region.Atlas;
            source = region.SourceRect;
        }

        batch.Draw(actual, dest, source, tint, 0f, Vector2.Zero, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

    private static Color MultiplyTint(Color color, Color tint)
        => new((byte)(color.R * tint.R / 255), (byte)(color.G * tint.G / 255), (byte)(color.B * tint.B / 255), color.A);
}
