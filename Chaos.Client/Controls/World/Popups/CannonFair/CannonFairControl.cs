using Chaos.CannonFair;
using Chaos.Client.Controls.Components;
using Chaos.Client.Data;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static Chaos.CannonFair.CannonFairRules;

namespace Chaos.Client.Controls.World.Popups.CannonFair;

public sealed class CannonFairControl : UIPanel
{
    private const int WINDOW_WIDTH = 640;
    private const int WINDOW_HEIGHT = 480;
    private const int TOP_BAR_HEIGHT = 28;
    private const int BOTTOM_BAR_Y = 432;
    private const int BOTTOM_BAR_HEIGHT = 48;
    private const int GROUND_Y = 400;
    private const int PLAYER_ID = 1;
    private const int AIM_MS = 15000;
    private const int RESULT_HOLD_MS = 1000;
    private const int POPUP_MS = 900;
    private const int POPUP_COUNT = 12;
    private const int AIM_DOTS = 12;
    private const float AIM_DOT_SECONDS = 0.1f;
    private const float SPRITE_SCALE_PER_METRE = 0.025f;
    private const float ANGLE_STEP = 1f;
    private const float POWER_STEP = 0.02f;
    private const uint PLAYER_CACHE_ID = 0xFFCA0001;
    private const uint BOT_CACHE_ID = 0xFFCA0002;
    private const int NO_EMOTION = -1;
    private const string STAND_SUFFIX = "01";

    private static readonly int[] BotCounts = [0, 7, 23];
    private static readonly TimeSpan SimulationLimit = TimeSpan.FromSeconds(1);
    private static readonly Color Sky = new(127, 178, 217);
    private static readonly Color Ground = new(111, 143, 69);
    private static readonly Color Wood = new(168, 116, 60);
    private static readonly Color Stone = new(154, 154, 164);
    private static readonly Color Goblin = new(94, 154, 58);
    private static readonly Color King = new(122, 184, 74);
    private static readonly Color Gold = new(255, 211, 77);
    private static readonly Color Cannon = new(59, 59, 68);
    private static readonly Color ChromeLight = new(150, 110, 70);
    private static readonly Color ChromeDark = new(44, 28, 16);

    private readonly CannonFairAim Aim = new();
    private readonly UILabel BannerLabel;
    private readonly Camera Camera = new(WINDOW_WIDTH, WINDOW_HEIGHT) { Position = new Vector2(WINDOW_WIDTH / 2f, WINDOW_HEIGHT / 2f) };
    private readonly UILabel[] Popups = new UILabel[POPUP_COUNT];
    private readonly AislingRenderer Renderer;
    private readonly UILabel ScoreLabel;
    private readonly UILabel ShotLabel;
    private readonly UILabel TimerLabel;

    private float CarryMs;
    private float PressX;
    private float PressY;
    private int AimMs;
    private int BotIndex;
    private AislingAppearance BotLook;
    private bool Dragging;
    private int ImpactMs;
    private RecordingPlayer? Player;
    private Phase CurrentPhase;
    private Texture2D? ChromeTile;
    private Texture2D? Pixel;
    private AislingAppearance PlayerLook;
    private float PlayerCannonX;
    private int ReplayMs;
    private int Score;
    private ShotKind Shot;
    private Task<VolleyResult>? Simulating;
    private VolleyResult? Volley;

    private enum Phase
    {
        Aiming,
        Simulating,
        Replay
    }

    public CannonFairControl(AislingRenderer renderer)
    {
        Renderer = renderer;
        Name = "CannonFair";
        Visible = false;
        UsesControlStack = true;
        X = 0;
        Y = 0;
        Width = WINDOW_WIDTH;
        Height = WINDOW_HEIGHT;

        ScoreLabel = AddLabel(10, 8, 300, HorizontalAlignment.Left);
        TimerLabel = AddLabel(270, 8, 100, HorizontalAlignment.Center);
        ShotLabel = AddLabel(330, 436, 300, HorizontalAlignment.Right);
        BannerLabel = AddLabel(0, 456, WINDOW_WIDTH, HorizontalAlignment.Center);

        for (var index = 0; index < POPUP_COUNT; index++)
            Popups[index] = AddLabel(0, 0, 80, HorizontalAlignment.Center);
    }

    public void Open(in AislingAppearance look, in AislingAppearance botLook)
    {
        Pixel ??= CreatePixel();
        PlayerLook = look with { WeaponSprite = 0, ShieldSprite = 0 };
        BotLook = botLook with { WeaponSprite = 0, ShieldSprite = 0 };
        Score = 0;
        Simulating = null;
        BeginAiming();
        InputDispatcher.Instance?.PushControl(this);
        Visible = true;
    }

    public void Close()
    {
        if (!Visible)
            return;

        Simulating = null;
        Dragging = false;
        InputDispatcher.Instance?.RemoveControl(this);
        Visible = false;
    }

    public void Update(float seconds)
    {
        if (!Visible)
            return;

        var exactMs = (seconds * 1000f) + CarryMs;
        var elapsedMs = (int)exactMs;
        CarryMs = exactMs - elapsedMs;

        switch (CurrentPhase)
        {
            case Phase.Aiming:
                AimMs -= elapsedMs;

                if (AimMs <= 0)
                    Fire(false);

                break;
            case Phase.Simulating:
                CollectSimulation();

                break;
            case Phase.Replay:
                ReplayMs += elapsedMs;

                if (ReplayMs >= Player!.DurationMs + RESULT_HOLD_MS)
                    BeginAiming();

                break;
        }

        RefreshLabels();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        e.Handled = true;

        if (e.Keycode == Keycode.Escape)
        {
            Close();

            return;
        }

        if (CurrentPhase != Phase.Aiming)
            return;

        switch (e.Keycode)
        {
            case Keycode.Up:
                Aim.Nudge(ANGLE_STEP, 0f);

                break;
            case Keycode.Down:
                Aim.Nudge(-ANGLE_STEP, 0f);

                break;
            case Keycode.Right:
                Aim.Nudge(0f, POWER_STEP);

                break;
            case Keycode.Left:
                Aim.Nudge(0f, -POWER_STEP);

                break;
            case Keycode.Space when !e.IsRepeat:
                Fire(true);

                break;
            case Keycode.B when !e.IsRepeat:
                BotIndex = (BotIndex + 1) % BotCounts.Length;

                break;
            case Keycode.D1:
                Shot = ShotKind.Plain;

                break;
            case Keycode.D2:
                Shot = ShotKind.Anvil;

                break;
            case Keycode.D3:
                Shot = ShotKind.RubberSuit;

                break;
            case Keycode.D4:
                Shot = ShotKind.PowderKeg;

                break;
            case Keycode.D5:
                Shot = ShotKind.Triplets;

                break;
        }
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if ((e.Button == MouseButton.Left) && (CurrentPhase == Phase.Aiming))
        {
            Dragging = true;
            PressX = e.ScreenX - ScreenX;
            PressY = e.ScreenY - ScreenY;
        }
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (Dragging && (CurrentPhase == Phase.Aiming))
            Aim.Drag(PressX, PressY, e.ScreenX - ScreenX, e.ScreenY - ScreenY);
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        e.Handled = true;

        if (!Dragging)
            return;

        Dragging = false;

        if ((CurrentPhase == Phase.Aiming) && Aim.Drag(PressX, PressY, e.ScreenX - ScreenX, e.ScreenY - ScreenY))
            Fire(true);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || Pixel is null)
            return;

        FillRect(spriteBatch, 0, 0, WINDOW_WIDTH, WINDOW_HEIGHT, Sky);

        if ((CurrentPhase == Phase.Replay) && Volley is not null)
            DrawReplay(spriteBatch);
        else
            DrawAiming(spriteBatch);

        DrawChrome(spriteBatch);

        base.Draw(spriteBatch);
    }

    public override void Dispose()
    {
        Pixel?.Dispose();
        base.Dispose();
    }

    private void BeginAiming()
    {
        CurrentPhase = Phase.Aiming;
        AimMs = AIM_MS;
        Shot = (ShotKind)Random.Shared.Next(Enum.GetValues<ShotKind>().Length);
        PlayerCannonX = 1 + Random.Shared.Next(BotCounts[BotIndex] + 1);
        Volley = null;
        Player = null;
        BannerLabel.Text = "Drag back from the gold cannon. 1-5 shot, B bots, Esc quit.";
    }

    private void Fire(bool playerFires)
    {
        Dragging = false;
        var shots = new List<AimedShot>();

        if (playerFires)
            shots.Add(new AimedShot(PLAYER_ID, PlayerCannonX, Aim.AngleDegrees, Aim.Power, Shot));

        var nextCannon = 1;

        for (var bot = 0; bot < BotCounts[BotIndex]; bot++)
        {
            if (nextCannon == (int)PlayerCannonX)
                nextCannon++;

            shots.Add(
                new AimedShot(
                    PLAYER_ID + 1 + bot,
                    nextCannon,
                    30f + (35f * Random.Shared.NextSingle()),
                    0.6f + (0.35f * Random.Shared.NextSingle()),
                    (ShotKind)Random.Shared.Next(Enum.GetValues<ShotKind>().Length)));

            nextCannon++;
        }

        CurrentPhase = Phase.Simulating;
        BannerLabel.Text = "Firing...";
        Simulating = Task.Run(() => VolleySimulator.Run(FortLayouts.FeelTest, shots, SimulationLimit));
    }

    private void CollectSimulation()
    {
        if (Simulating is not { IsCompleted: true } task)
            return;

        Simulating = null;

        if (task.IsFaulted)
        {
            BeginAiming();
            BannerLabel.Text = $"Simulation failed: {task.Exception?.InnerException?.Message}";

            return;
        }

        Volley = task.Result;
        Player = new RecordingPlayer(Volley);
        ImpactMs = CannonFairCamera.ImpactMs(Volley);
        ReplayMs = 0;
        Score += Volley.Scores.Where(score => score.PlayerId == PLAYER_ID).Sum(score => score.Points);
        CurrentPhase = Phase.Replay;
        BannerLabel.Text = Volley.Truncated ? "Replay cut short: simulation hit its time limit" : string.Empty;
    }

    private void DrawChrome(SpriteBatch spriteBatch)
    {
        ChromeTile ??= UiRenderer.Instance?.GetSpfTexture("DlgBack2.spf");

        if (ChromeTile is not null)
        {
            OrnateFrame.TileTexture(spriteBatch, ChromeTile, ScreenX, ScreenY, WINDOW_WIDTH, TOP_BAR_HEIGHT);
            OrnateFrame.TileTexture(spriteBatch, ChromeTile, ScreenX, ScreenY + BOTTOM_BAR_Y, WINDOW_WIDTH, BOTTOM_BAR_HEIGHT);
        }

        FillRect(spriteBatch, 0, TOP_BAR_HEIGHT - 3, WINDOW_WIDTH, 1, ChromeLight);
        FillRect(spriteBatch, 0, TOP_BAR_HEIGHT - 2, WINDOW_WIDTH, 2, ChromeDark);
        FillRect(spriteBatch, 0, BOTTOM_BAR_Y, WINDOW_WIDTH, 2, ChromeDark);
        FillRect(spriteBatch, 0, BOTTOM_BAR_Y + 2, WINDOW_WIDTH, 1, ChromeLight);
        FillRect(spriteBatch, 0, 0, WINDOW_WIDTH, 2, ChromeDark);
        FillRect(spriteBatch, 0, WINDOW_HEIGHT - 2, WINDOW_WIDTH, 2, ChromeDark);
        FillRect(spriteBatch, 0, 0, 2, WINDOW_HEIGHT, ChromeDark);
        FillRect(spriteBatch, WINDOW_WIDTH - 2, 0, 2, WINDOW_HEIGHT, ChromeDark);
    }

    private void DrawAiming(SpriteBatch spriteBatch)
    {
        FillRect(spriteBatch, 0, GROUND_Y, WINDOW_WIDTH, WINDOW_HEIGHT - GROUND_Y, Ground);

        foreach (var block in FortLayouts.FeelTest.Blocks)
            DrawPiece(spriteBatch, block.X, block.Y, 0f, block.Width, block.Height, block.Material == BlockMaterial.Wood ? Wood : Stone, 0f);

        foreach (var goblin in FortLayouts.FeelTest.Goblins)
        {
            var size = goblin.IsKing ? 1.2f : 0.8f;
            DrawPiece(spriteBatch, goblin.X, goblin.Y, 0f, size, size, goblin.IsKing ? King : Goblin, 0f);
        }

        for (var cannon = 1; cannon <= BotCounts[BotIndex] + 1; cannon++)
            DrawPiece(spriteBatch, cannon, CANNON_Y, 0f, 1f, 0.5f, cannon == (int)PlayerCannonX ? Gold : Cannon, 0f);

        for (var dot = 1; dot <= AIM_DOTS; dot++)
        {
            var (worldX, worldY) = Aim.Path(PlayerCannonX, dot * AIM_DOT_SECONDS, Shot);
            var (x, y, _) = CannonFairCamera.Project(worldX, worldY, 0f);
            FillRect(spriteBatch, (int)x - 1, (int)y - 1, 3, 3, Gold);
        }
    }

    private void DrawReplay(SpriteBatch spriteBatch)
    {
        var zoom = CannonFairCamera.ZoomAt(ReplayMs, ImpactMs);
        var (_, groundY, _) = CannonFairCamera.Project(0f, 0f, zoom);
        FillRect(spriteBatch, 0, (int)groundY, WINDOW_WIDTH, Math.Max(0, WINDOW_HEIGHT - (int)groundY), Ground);

        foreach (var obj in Volley!.Objects)
        {
            var pose = Player!.Sample(obj.Id, ReplayMs);

            if (pose.Gone)
                continue;

            if (obj.Kind == ObjectKind.Body)
                DrawFlyer(spriteBatch, obj, pose, zoom);
            else
                DrawPiece(spriteBatch, pose.X, pose.Y, pose.Rotation, obj.Width, obj.Height, ColorOf(obj), zoom);
        }

        foreach (var blast in Volley.Blasts)
        {
            var age = ReplayMs - blast.Ms;

            if (age is >= 0 and < 300)
                DrawPiece(spriteBatch, blast.X, blast.Y, 0f, blast.Radius * 2f, blast.Radius * 2f, Color.Orange * 0.5f, zoom);
        }

        RefreshPopups(zoom);
    }

    private void DrawFlyer(SpriteBatch spriteBatch, RecordedObject obj, ObjectPose pose, float zoom)
    {
        var (x, y, scale) = CannonFairCamera.Project(pose.X, pose.Y, zoom);
        var own = obj.OwnerPlayerId == PLAYER_ID;

        var drawParams = new AislingDrawParams(
            own ? PLAYER_CACHE_ID : BOT_CACHE_ID,
            own ? PlayerLook : BotLook,
            0,
            false,
            true,
            STAND_SUFFIX,
            NO_EMOTION,
            0,
            default,
            x,
            y + (scale * obj.Height * 0.5f),
            Vector2.Zero,
            EntityTintType.None,
            default,
            false,
            1f,
            scale * obj.Width * SPRITE_SCALE_PER_METRE * 2f,
            pose.Rotation);

        Renderer.Draw(spriteBatch, Camera, in drawParams);

        if (own)
            FillRect(spriteBatch, (int)x - 3, (int)(y - (scale * obj.Height)) - 8, 7, 7, Gold);
    }

    private void DrawPiece(SpriteBatch spriteBatch, float worldX, float worldY, float rotation, float width, float height, Color color, float zoom)
    {
        var (x, y, scale) = CannonFairCamera.Project(worldX, worldY, zoom);

        spriteBatch.Draw(
            Pixel!,
            new Vector2(ScreenX + x, ScreenY + y),
            null,
            color,
            rotation,
            new Vector2(0.5f, 0.5f),
            new Vector2(width * scale, height * scale),
            SpriteEffects.None,
            0f);
    }

    private void FillRect(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color)
        => spriteBatch.Draw(Pixel!, new Rectangle(ScreenX + x, ScreenY + y, width, height), color);

    private static Color ColorOf(RecordedObject obj)
        => obj.Kind switch
        {
            ObjectKind.King   => King,
            ObjectKind.Goblin => Goblin,
            _                 => obj.Material == BlockMaterial.Wood ? Wood : Stone
        };

    private void RefreshLabels()
    {
        ScoreLabel.Text = $"Score {Score}   Bots {BotCounts[BotIndex]}";
        TimerLabel.Text = CurrentPhase == Phase.Aiming ? $"{(AimMs + 999) / 1000}" : string.Empty;
        ShotLabel.Text = CurrentPhase == Phase.Aiming ? $"{Shot}  angle {Aim.AngleDegrees:F0}  power {Aim.Power * 100f:F0}" : string.Empty;

        if ((CurrentPhase != Phase.Replay) || Volley is null)
            HidePopups();
    }

    private void RefreshPopups(float zoom)
    {
        var used = 0;

        foreach (var score in Volley!.Scores)
        {
            var age = ReplayMs - score.Ms;

            if (age is < 0 or >= POPUP_MS || (used >= POPUP_COUNT))
                continue;

            var (x, y, _) = CannonFairCamera.Project(score.X, score.Y, zoom);
            var popup = Popups[used++];
            popup.X = (int)x - (popup.Width / 2);
            popup.Y = (int)y - (age / 30);
            popup.Text = score.PlayerId == PLAYER_ID ? $"+{score.Points}" : $"+{score.Points} Bot";
            popup.ForegroundColor = score.PlayerId == PLAYER_ID ? Color.White : Color.LightSkyBlue;
            popup.Visible = true;
        }

        for (var index = used; index < POPUP_COUNT; index++)
            Popups[index].Visible = false;
    }

    private void HidePopups()
    {
        foreach (var popup in Popups)
            popup.Visible = false;
    }

    private static Texture2D CreatePixel()
    {
        var pixel = new Texture2D(ChaosGame.Device, 1, 1);
        pixel.SetData([Color.White]);

        return pixel;
    }

    private UILabel AddLabel(int x, int y, int width, HorizontalAlignment alignment)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = LegendColors.White,
            ShadowStyle = ShadowStyle.BottomRight,
            IsHitTestVisible = false
        };

        AddChild(label);

        return label;
    }
}
