#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     The Stage Lighting window's picture of the stage: a diamond floor at the game's angle; each light as a soft
///     coloured circle with its number and a small dot where it is right now; a dotted line or circle to a square handle
///     for Sweep and Circle lights; people on the stage as blue squares (ringed in the light's colour when followed).
///     Press and drag a light or a handle to move it. While picking a follow target, click a person. Hover a person to
///     see their name.
/// </summary>
public sealed class StageView : UIElement
{
    public const int VIEW_WIDTH = 200;
    public const int VIEW_HEIGHT = 150;

    private const float HIT_RADIUS = 9f;
    private const int BLOB = 32;

    private static readonly Color Backdrop = new(13, 11, 9);
    private static readonly Color Wood = new(109, 74, 44);
    private static readonly Color Seam = new(74, 53, 36);
    private static readonly Color Person = new(127, 179, 255);

    private Texture2D? Blob;
    private Texture2D? Floor;
    private Rectangle FloorStage;
    private DragKind Dragging;
    private byte DragId;
    private Vector2 Mouse;
    private bool MouseInside;

    public StageView()
    {
        Width = VIEW_WIDTH;
        Height = VIEW_HEIGHT;
    }

    public byte? SelectedLightId { get; set; }
    public bool PickingFollow { get; set; }

    /// <summary>
    ///     A press on a person picks them even when not <see cref="PickingFollow" /> (the selected light already follows
    ///     someone, so clicking another person re-targets it). A press elsewhere still selects and drags lights.
    /// </summary>
    public bool AllowPersonPick { get; set; }

    public event Action<byte>? LightPressed;
    public event Action<byte, Vector2>? LightDragged;
    public event Action<byte, Vector2>? HandleDragged;
    public event Action<byte>? DragEnded;
    public event Action<uint>? PersonPicked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
        DrawRectClipped(spriteBatch, ScreenBounds, Backdrop);

        var setup = WorldState.StageLights;
        var stage = setup.Stage;

        if (stage.IsEmpty)
            return;

        EnsureTextures(stage);
        var origin = new Vector2(ScreenX, ScreenY);
        DrawTexture(spriteBatch, Floor, origin, Color.White);

        //paths and handles under the lights
        foreach (var light in setup.Lights)
        {
            var anchor = View(StageLightAnimator.ToTile(light.X, light.Y), stage);
            var handle = View(StageLightAnimator.ToTile(light.X2, light.Y2), stage);

            if (light.Motion == StageLightMotion.Sweep)
                DrawDots(spriteBatch, origin, [anchor, handle]);
            else if (light.Motion == StageLightMotion.Circle)
                DrawCircle(spriteBatch, origin, light, stage);

            if (light.Motion is StageLightMotion.Sweep or StageLightMotion.Circle)
            {
                var h = origin + handle;
                DrawRectClipped(spriteBatch, new Rectangle((int)h.X - 3, (int)h.Y - 3, 7, 7), new Color(light.R, light.G, light.B));
                UIElement.DrawBorder(spriteBatch, new Rectangle((int)h.X - 3, (int)h.Y - 3, 7, 7), Color.White);
            }
        }

        foreach (var light in setup.Lights)
        {
            var centre = origin + View(StageLightAnimator.ToTile(light.X, light.Y), stage);
            var radius = 7f + (3f * (int)light.Size);
            var rect = new Rectangle((int)(centre.X - (radius * 1.6f)), (int)(centre.Y - (radius * 0.8f)), (int)(radius * 3.2f), (int)(radius * 1.6f));

            DrawTextureFitted(spriteBatch, Blob, rect, new Color(light.R, light.G, light.B) * 0.9f);

            if (light.Id == SelectedLightId)
                UIElement.DrawBorder(spriteBatch, rect, Color.White);

            DrawTextClipped(spriteBatch, centre - new Vector2(3, 16), light.Id.ToString(), Color.White, false);
        }

        //where each light is right now (moving lights); WorldScreen's per-frame update already evaluated this
        foreach (var frame in setup.LatestFrames)
        {
            var p = origin + View(frame.Tile, stage);
            DrawRectClipped(spriteBatch, new Rectangle((int)p.X - 1, (int)p.Y - 1, 3, 3), Color.White * MathHelper.Clamp(frame.Strength, 0.3f, 1f));
        }

        //people on the stage
        string? hoverName = null;

        foreach (var entity in WorldState.GetEntities())
        {
            if (entity.Type != ClientEntityType.Aisling)
                continue;

            var tile = StageLightAnimator.EntityTile(entity.TileX, entity.TileY, entity.VisualOffset);

            if (!OnStage(tile, stage))
                continue;

            var p = View(tile, stage);
            var screen = origin + p;

            foreach (var light in setup.Lights)
                if ((light.Motion == StageLightMotion.Follow) && (light.FollowId == entity.Id))
                    UIElement.DrawBorder(spriteBatch, new Rectangle((int)screen.X - 5, (int)screen.Y - 5, 11, 11), new Color(light.R, light.G, light.B));

            DrawRectClipped(spriteBatch, new Rectangle((int)screen.X - 3, (int)screen.Y - 3, 7, 7), Person);
            UIElement.DrawBorder(spriteBatch, new Rectangle((int)screen.X - 3, (int)screen.Y - 3, 7, 7), Color.White);

            if (MouseInside && (Vector2.Distance(p, Mouse) <= 6f))
                hoverName = entity.Name;
        }

        if (hoverName is not null)
        {
            var width = TextRenderer.MeasureWidth(hoverName) + 6;
            var box = new Rectangle((int)(origin.X + Mouse.X + 8), (int)(origin.Y + Mouse.Y - 6), width, TextRenderer.CHAR_HEIGHT + 2);
            DrawRectClipped(spriteBatch, box, Color.Black * 0.8f);
            DrawTextClipped(spriteBatch, new Vector2(box.X + 3, box.Y + 1), hoverName, Color.White, false);
        }

        if (PickingFollow)
            DrawTextClipped(spriteBatch, origin + new Vector2(4, VIEW_HEIGHT - TextRenderer.CHAR_HEIGHT - 2), "Click a person", LegendColors.Gold, false);
    }

    public override void OnMouseEnter() => MouseInside = true;

    public override void OnMouseLeave() => MouseInside = false;

    public override void OnMouseMove(MouseMoveEvent e)
    {
        Mouse = new Vector2(e.ScreenX - ScreenX, e.ScreenY - ScreenY);

        if (Dragging == DragKind.None)
            return;

        var tile = StageViewGeometry.ViewToTile(Mouse, WorldState.StageLights.Stage, VIEW_WIDTH, VIEW_HEIGHT);

        if (Dragging == DragKind.Light)
            LightDragged?.Invoke(DragId, tile);
        else
            HandleDragged?.Invoke(DragId, tile);

        e.Handled = true;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        var point = new Vector2(e.ScreenX - ScreenX, e.ScreenY - ScreenY);

        if ((PickingFollow || AllowPersonPick) && PersonAt(point) is { } personId)
        {
            PersonPicked?.Invoke(personId);

            return;
        }

        if (PickingFollow)
            return;

        //handles sit on top, so they win over a light underneath
        if (HandleAt(point) is { } handleId)
        {
            Dragging = DragKind.Handle;
            DragId = handleId;
            LightPressed?.Invoke(handleId);

            return;
        }

        if (LightAt(point) is { } lightId)
        {
            Dragging = DragKind.Light;
            DragId = lightId;
            LightPressed?.Invoke(lightId);
        }
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if ((e.Button != MouseButton.Left) || (Dragging == DragKind.None))
            return;

        Dragging = DragKind.None;
        DragEnded?.Invoke(DragId);
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        MouseInside = false;

        if (Dragging == DragKind.None)
            return;

        Dragging = DragKind.None;
        DragEnded?.Invoke(DragId);
    }

    public override void Dispose()
    {
        Floor?.Dispose();
        Blob?.Dispose();
        base.Dispose();
    }

    private static Vector2 View(Vector2 tile, Rectangle stage) => StageViewGeometry.TileToView(tile, stage, VIEW_WIDTH, VIEW_HEIGHT);

    private static bool OnStage(Vector2 tile, Rectangle stage)
        => (tile.X >= stage.Left - 0.5f) && (tile.X < stage.Right - 0.5f) && (tile.Y >= stage.Top - 0.5f) && (tile.Y < stage.Bottom - 0.5f);

    private byte? LightAt(Vector2 point) => Nearest(WorldState.StageLights.Lights, l => StageLightAnimator.ToTile(l.X, l.Y), point);

    private byte? HandleAt(Vector2 point)
        => Nearest(
            WorldState.StageLights.Lights.Where(l => l.Motion is StageLightMotion.Sweep or StageLightMotion.Circle),
            l => StageLightAnimator.ToTile(l.X2, l.Y2),
            point);

    private static byte? Nearest(IEnumerable<StageLightInfo> lights, Func<StageLightInfo, Vector2> where, Vector2 point)
    {
        byte? best = null;
        var bestDistance = HIT_RADIUS;
        var stage = WorldState.StageLights.Stage;

        foreach (var light in lights)
        {
            var distance = Vector2.Distance(point, View(where(light), stage));

            if (distance <= bestDistance)
            {
                best = light.Id;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static uint? PersonAt(Vector2 point)
    {
        var stage = WorldState.StageLights.Stage;

        foreach (var entity in WorldState.GetEntities())
        {
            if (entity.Type != ClientEntityType.Aisling)
                continue;

            var tile = StageLightAnimator.EntityTile(entity.TileX, entity.TileY, entity.VisualOffset);

            if (OnStage(tile, stage) && (Vector2.Distance(point, View(tile, stage)) <= 6f))
                return entity.Id;
        }

        return null;
    }

    private void DrawDots(SpriteBatch spriteBatch, Vector2 origin, Vector2[] line)
    {
        var from = line[0];
        var to = line[1];
        var steps = Math.Max(1, (int)(Vector2.Distance(from, to) / 4f));

        for (var i = 0; i <= steps; i++)
        {
            var p = origin + Vector2.Lerp(from, to, i / (float)steps);
            DrawRectClipped(spriteBatch, new Rectangle((int)p.X, (int)p.Y, 2, 2), Color.White);
        }
    }

    private void DrawCircle(SpriteBatch spriteBatch, Vector2 origin, StageLightInfo light, Rectangle stage)
    {
        var centre = StageLightAnimator.ToTile(light.X, light.Y);
        var radius = Vector2.Distance(centre, StageLightAnimator.ToTile(light.X2, light.Y2));

        for (var i = 0; i < 32; i++)
        {
            var angle = MathHelper.TwoPi * i / 32f;
            var p = origin + View(centre + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius), stage);
            DrawRectClipped(spriteBatch, new Rectangle((int)p.X, (int)p.Y, 2, 2), Color.White * 0.8f);
        }
    }

    //the floor is rebuilt only when the stage rectangle changes; the blob once. UI textures are premultiplied.
    private void EnsureTextures(Rectangle stage)
    {
        if (Blob is null)
        {
            var blob = new Color[BLOB * BLOB];

            for (var py = 0; py < BLOB; py++)
                for (var px = 0; px < BLOB; px++)
                {
                    var dx = (px + 0.5f - (BLOB / 2f)) / (BLOB / 2f);
                    var dy = (py + 0.5f - (BLOB / 2f)) / (BLOB / 2f);
                    var d = MathF.Sqrt((dx * dx) + (dy * dy));
                    var a = d >= 1f ? 0f : d < 0.35f ? 1f : 1f - ((d - 0.35f) / 0.65f);
                    var v = (byte)(a * 255f);
                    blob[(py * BLOB) + px] = new Color(v, v, v, v);
                }

            Blob = new Texture2D(TextureConverter.Device, BLOB, BLOB);
            Blob.SetData(blob);
        }

        if (Floor is not null && (FloorStage == stage))
            return;

        var pixels = new Color[VIEW_WIDTH * VIEW_HEIGHT];

        for (var py = 0; py < VIEW_HEIGHT; py++)
            for (var px = 0; px < VIEW_WIDTH; px++)
            {
                var tile = StageViewGeometry.ViewToTile(new Vector2(px + 0.5f, py + 0.5f), stage, VIEW_WIDTH, VIEW_HEIGHT);

                if (!OnStage(tile, stage))
                {
                    pixels[(py * VIEW_WIDTH) + px] = Backdrop;

                    continue;
                }

                //a seam where either coordinate crosses a tile edge (x.5)
                var fx = MathF.Abs(((tile.X + 0.5f) % 1f) - 0.5f);
                var fy = MathF.Abs(((tile.Y + 0.5f) % 1f) - 0.5f);
                pixels[(py * VIEW_WIDTH) + px] = (fx > 0.45f) || (fy > 0.45f) ? Seam : Wood;
            }

        Floor ??= new Texture2D(TextureConverter.Device, VIEW_WIDTH, VIEW_HEIGHT);
        Floor.SetData(pixels);
        FloorStage = stage;
    }

    private enum DragKind
    {
        None,
        Light,
        Handle
    }
}
