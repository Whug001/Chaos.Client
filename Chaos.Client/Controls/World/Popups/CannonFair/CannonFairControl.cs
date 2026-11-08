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
    private const int PANEL_PADDING = 16;
    private const int ROW_HEIGHT = 14;
    private const int POWER_BAR_HEIGHT = 8;
    private const int LOW_TIME_MS = 5000;
    private const int KEY_COLUMN_WIDTH = 66;
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
    private const int GOBLIN_SPRITE = 15;
    private const int KING_SPRITE = 17;
    private const float GOBLIN_DRAW_SCALE = 2.4f;
    private const float KING_DRAW_SCALE = 1.7f;
    private const float GOBLIN_SIZE = 0.8f;
    private const float KING_SIZE = 1.2f;
    private const float DEFAULT_BARREL_ANGLE = 45f;
    private const int BLAST_MS = 300;
    private const int BARREL_PIVOT_DROP = 4;
    private const int BARREL_PIVOT_Y = 4;

    private static readonly int[] BotCounts = [0, 7, 23];
    private static readonly TimeSpan SimulationLimit = TimeSpan.FromSeconds(1);
    private static readonly Color Gold = new(255, 211, 77);
    private static readonly Color ChromeDark = new(44, 28, 16);
    private static readonly Color Caption = new(214, 190, 150);
    private static readonly Color LowTime = new(255, 96, 64);
    private static readonly Color PowerShade = new(176, 120, 40);
    private static readonly Rectangle ScorePanel = new(6, 6, 150, 58);
    private static readonly Rectangle TimerPanel = new(288, 6, 64, 44);
    private static readonly Rectangle AimPanel = new(458, 6, 176, 84);
    private static readonly Rectangle ControlsPanel = new(6, 400, 236, 74);

    private readonly CannonFairAim Aim = new();
    private readonly List<UILabel> AimingOnly = [];
    private readonly UILabel AngleValue;
    private readonly UILabel BotsValue;
    private readonly UILabel PowerValue;
    private readonly UILabel ScoreValue;
    private readonly UILabel ShotValue;
    private readonly CreatureRenderer Creatures;
    private readonly UILabel BannerLabel;
    private readonly Camera Camera = new(WINDOW_WIDTH, WINDOW_HEIGHT) { Position = new Vector2(WINDOW_WIDTH / 2f, WINDOW_HEIGHT / 2f) };
    private readonly UILabel[] Popups = new UILabel[POPUP_COUNT];
    private readonly AislingRenderer Renderer;
    private readonly UILabel TimerLabel;

    private float CarryMs;
    private int AimMs;
    private int BotIndex;
    private AislingAppearance BotLook;
    private int ImpactMs;
    private RecordingPlayer? Player;
    private Phase CurrentPhase;
    private CannonFairArt? Art;
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

    public CannonFairControl(AislingRenderer renderer, CreatureRenderer creatures)
    {
        Renderer = renderer;
        Creatures = creatures;
        Name = "CannonFair";
        Visible = false;
        UsesControlStack = true;
        X = 0;
        Y = 0;
        Width = WINDOW_WIDTH;
        Height = WINDOW_HEIGHT;

        ScoreValue = AddStat(ScorePanel, 0, "Score", false);
        BotsValue = AddStat(ScorePanel, 1, "Bots", false);
        TimerLabel = AddLabel(TimerPanel.X, TimerPanel.Y + PANEL_PADDING, TimerPanel.Width, HorizontalAlignment.Center);
        AimingOnly.Add(TimerLabel);
        ShotValue = AddStat(AimPanel, 0, "Shot", true);
        AngleValue = AddStat(AimPanel, 1, "Angle", true);
        PowerValue = AddStat(AimPanel, 2, "Power", true);
        AddKeyRow(0, 0, "Up/Down", "Angle");
        AddKeyRow(0, 1, "Left/Right", "Power");
        AddKeyRow(0, 2, "Space", "Fire");
        AddKeyRow(1, 0, "1-5", "Shot");
        AddKeyRow(1, 1, "B", "Bots");
        AddKeyRow(1, 2, "Esc", "Leave");
        BannerLabel = AddLabel(ControlsPanel.Right, 456, WINDOW_WIDTH - ControlsPanel.Right, HorizontalAlignment.Center);

        for (var index = 0; index < POPUP_COUNT; index++)
            Popups[index] = AddLabel(0, 0, 80, HorizontalAlignment.Center);
    }

    public void Open(in AislingAppearance look, in AislingAppearance botLook)
    {
        Pixel ??= CreatePixel();
        Art ??= new CannonFairArt();
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

    public override void OnMouseDown(MouseDownEvent e) => e.Handled = true;

    public override void OnMouseUp(MouseUpEvent e) => e.Handled = true;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || Pixel is null || Art is null)
            return;

        spriteBatch.Draw(Art.Sky, new Vector2(ScreenX, ScreenY), Color.White);

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
        Art?.Dispose();
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
        BannerLabel.Text = string.Empty;
    }

    private void Fire(bool playerFires)
    {
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
        DrawPlaque(spriteBatch, ScorePanel);

        if (CurrentPhase == Phase.Aiming)
        {
            DrawPlaque(spriteBatch, TimerPanel);
            DrawPlaque(spriteBatch, AimPanel);
            DrawPlaque(spriteBatch, ControlsPanel);
            DrawPowerBar(spriteBatch);
        }

        FillRect(spriteBatch, 0, 0, WINDOW_WIDTH, 2, ChromeDark);
        FillRect(spriteBatch, 0, WINDOW_HEIGHT - 2, WINDOW_WIDTH, 2, ChromeDark);
        FillRect(spriteBatch, 0, 0, 2, WINDOW_HEIGHT, ChromeDark);
        FillRect(spriteBatch, WINDOW_WIDTH - 2, 0, 2, WINDOW_HEIGHT, ChromeDark);
    }

    private void DrawPlaque(SpriteBatch spriteBatch, Rectangle panel)
        => spriteBatch.Draw(Art!.Plaque(panel.Width, panel.Height), new Vector2(ScreenX + panel.X, ScreenY + panel.Y), Color.White);

    private void DrawPowerBar(SpriteBatch spriteBatch)
    {
        var x = AimPanel.X + PANEL_PADDING;
        var y = AimPanel.Y + PANEL_PADDING + (3 * ROW_HEIGHT) + 1;
        var width = AimPanel.Width - (2 * PANEL_PADDING);
        var fill = (int)((width - 2) * (Aim.Power / MAX_POWER));

        FillRect(spriteBatch, x, y, width, POWER_BAR_HEIGHT, ChromeDark);
        FillRect(spriteBatch, x + 1, y + 1, fill, POWER_BAR_HEIGHT - 2, Gold);
        FillRect(spriteBatch, x + 1, y + POWER_BAR_HEIGHT - 3, fill, 2, PowerShade);
    }

    private void DrawAiming(SpriteBatch spriteBatch)
    {
        DrawGround(spriteBatch, GROUND_Y);

        foreach (var block in FortLayouts.FeelTest.Blocks)
            DrawBlock(spriteBatch, block.X, block.Y, 0f, block.Width, block.Height, block.Material, 0f);

        foreach (var goblin in FortLayouts.FeelTest.Goblins)
            DrawGoblin(spriteBatch, goblin.X, goblin.Y, 0f, goblin.IsKing ? KING_SIZE : GOBLIN_SIZE, goblin.IsKing, 0f);

        for (var cannon = 1; cannon <= BotCounts[BotIndex] + 1; cannon++)
            if (cannon != (int)PlayerCannonX)
                DrawCannon(spriteBatch, cannon, DEFAULT_BARREL_ANGLE, false);

        DrawCannon(spriteBatch, (int)PlayerCannonX, Aim.AngleDegrees, true);

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
        DrawGround(spriteBatch, (int)groundY);

        foreach (var obj in Volley!.Objects)
        {
            var pose = Player!.Sample(obj.Id, ReplayMs);

            if (pose.Gone)
                continue;

            if (obj.Kind == ObjectKind.Body)
                DrawFlyer(spriteBatch, obj, pose, zoom);
            else if (obj.Kind is ObjectKind.Goblin or ObjectKind.King)
                DrawGoblin(spriteBatch, pose.X, pose.Y, pose.Rotation, obj.Width, obj.Kind == ObjectKind.King, zoom);
            else
                DrawBlock(spriteBatch, pose.X, pose.Y, pose.Rotation, obj.Width, obj.Height, obj.Material, zoom);
        }

        foreach (var blast in Volley.Blasts)
        {
            var age = ReplayMs - blast.Ms;

            if (age is >= 0 and < BLAST_MS)
                DrawBlast(spriteBatch, blast.X, blast.Y, blast.Radius, 1f - (age / (float)BLAST_MS), zoom);
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

    private void DrawGround(SpriteBatch spriteBatch, int y)
    {
        var height = Math.Min(CannonFairArtBaker.GROUND_HEIGHT, WINDOW_HEIGHT - y);

        if (height > 0)
            spriteBatch.Draw(Art!.Ground, new Rectangle(ScreenX, ScreenY + y, WINDOW_WIDTH, height), new Rectangle(0, 0, WINDOW_WIDTH, height), Color.White);
    }

    private void DrawBlock(SpriteBatch spriteBatch, float worldX, float worldY, float rotation, float width, float height, BlockMaterial material, float zoom)
    {
        var (x, y, scale) = CannonFairCamera.Project(worldX, worldY, zoom);
        var texture = Art!.Block(material, width, height);

        spriteBatch.Draw(
            texture,
            new Vector2(ScreenX + x, ScreenY + y),
            null,
            Color.White,
            rotation,
            new Vector2(texture.Width / 2f, texture.Height / 2f),
            scale / CannonFairArtBaker.TEXELS_PER_METRE,
            SpriteEffects.None,
            0f);
    }

    private void DrawGoblin(SpriteBatch spriteBatch, float worldX, float worldY, float rotation, float size, bool isKing, float zoom)
    {
        var sprite = isKing ? KING_SPRITE : GOBLIN_SPRITE;
        var frame = Creatures.GetFrame(sprite, Creatures.GetAnimInfo(sprite)?.StandingFrameIndex ?? 0);

        if (frame is null)
            return;

        var (x, y, scale) = CannonFairCamera.Project(worldX, worldY, zoom);
        var texture = frame.Value.Texture;
        var factor = size * (isKing ? KING_DRAW_SCALE : GOBLIN_DRAW_SCALE) * scale / texture.Height;

        spriteBatch.Draw(
            texture,
            new Vector2(ScreenX + x, ScreenY + y),
            null,
            Color.White,
            rotation,
            new Vector2(texture.Width / 2f, texture.Height - (size * scale * 0.5f / factor)),
            factor,
            SpriteEffects.None,
            0f);
    }

    private void DrawCannon(SpriteBatch spriteBatch, int cannon, float angleDegrees, bool own)
    {
        var (x, y, _) = CannonFairCamera.Project(cannon, CANNON_Y, 0f);

        spriteBatch.Draw(
            own ? Art!.BrassBarrel : Art!.IronBarrel,
            new Vector2(ScreenX + x, ScreenY + y + BARREL_PIVOT_DROP),
            null,
            Color.White,
            -MathF.PI * angleDegrees / 180f,
            new Vector2(0f, BARREL_PIVOT_Y),
            1f,
            SpriteEffects.None,
            0f);

        spriteBatch.Draw(Art.Carriage, new Vector2(ScreenX + x - (Art.Carriage.Width / 2f), ScreenY + GROUND_Y - Art.Carriage.Height), Color.White);
    }

    private void DrawBlast(SpriteBatch spriteBatch, float worldX, float worldY, float radius, float strength, float zoom)
    {
        var (x, y, scale) = CannonFairCamera.Project(worldX, worldY, zoom);
        var texture = Art!.Blast;

        spriteBatch.Draw(
            texture,
            new Vector2(ScreenX + x, ScreenY + y),
            null,
            Color.White * strength,
            0f,
            new Vector2(texture.Width / 2f, texture.Height / 2f),
            radius * 2f * scale / texture.Width,
            SpriteEffects.None,
            0f);
    }

    private void FillRect(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color)
        => spriteBatch.Draw(Pixel!, new Rectangle(ScreenX + x, ScreenY + y, width, height), color);

    private void RefreshLabels()
    {
        ScoreValue.Text = $"{Score}";
        BotsValue.Text = $"{BotCounts[BotIndex]}";
        TimerLabel.Text = $"{(AimMs + 999) / 1000}";
        TimerLabel.ForegroundColor = AimMs <= LOW_TIME_MS ? LowTime : Color.White;
        ShotValue.Text = ShotName(Shot);
        AngleValue.Text = $"{Aim.AngleDegrees:F0}";
        PowerValue.Text = $"{Aim.Power * 100f:F0}%";

        foreach (var label in AimingOnly)
            label.Visible = CurrentPhase == Phase.Aiming;

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

    private static string ShotName(ShotKind kind)
        => kind switch
        {
            ShotKind.RubberSuit => "Rubber Suit",
            ShotKind.PowderKeg  => "Powder Keg",
            _                   => kind.ToString()
        };

    private UILabel AddStat(Rectangle panel, int row, string caption, bool aimingOnly)
    {
        var x = panel.X + PANEL_PADDING;
        var y = panel.Y + PANEL_PADDING + (row * ROW_HEIGHT);
        var width = panel.Width - (2 * PANEL_PADDING);
        var name = AddLabel(x, y, width, HorizontalAlignment.Left);
        var value = AddLabel(x, y, width, HorizontalAlignment.Right);
        name.Text = caption;
        name.ForegroundColor = Caption;

        if (aimingOnly)
        {
            AimingOnly.Add(name);
            AimingOnly.Add(value);
        }

        return value;
    }

    private void AddKeyRow(int column, int row, string key, string action)
    {
        var x = ControlsPanel.X + PANEL_PADDING + (column * 126);
        var y = ControlsPanel.Y + PANEL_PADDING + (row * ROW_HEIGHT);
        var keyWidth = column == 0 ? KEY_COLUMN_WIDTH : 30;
        var keyLabel = AddLabel(x, y, keyWidth, HorizontalAlignment.Left);
        var actionLabel = AddLabel(x + keyWidth, y, 50, HorizontalAlignment.Left);
        keyLabel.Text = key;
        keyLabel.ForegroundColor = Gold;
        actionLabel.Text = action;
        AimingOnly.Add(keyLabel);
        AimingOnly.Add(actionLabel);
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
