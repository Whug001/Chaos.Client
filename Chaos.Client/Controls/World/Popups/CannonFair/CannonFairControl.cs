using Chaos.CannonFair;
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
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
    private const int POPUP_MS = 900;
    private const int POPUP_COUNT = 12;
    private const int AIM_DOTS = 12;
    private const float AIM_DOT_SECONDS = 0.1f;
    private const float SPRITE_SCALE_PER_METRE = 0.025f;
    private const float ANGLE_STEP = 1f;
    private const float POWER_STEP = 0.02f;
    private const uint LOOK_CACHE_BASE = 0xFFCA0001;
    private const int NO_EMOTION = -1;
    private const string STAND_SUFFIX = "01";
    private const int GOBLIN_SPRITE = 15;
    private const int KING_SPRITE = 17;
    private const float GOBLIN_DRAW_SCALE = 2.4f;
    private const float KING_DRAW_SCALE = 1.7f;
    private const float DEFAULT_BARREL_ANGLE = 45f;
    private const int BLAST_MS = 300;
    private const int BARREL_PIVOT_DROP = 4;
    private const int BARREL_PIVOT_Y = 4;
    private const int STAND_ROWS = 4;
    private const int CARD_X = 200;
    private const int CARD_Y = 35;
    private const int CARD_WIDTH = 240;
    private const int CARD_HEIGHT = 410;
    private const string UNKNOWN_SCORER = "Rival";

    private static readonly Color Gold = new(255, 211, 77);
    private static readonly Color ChromeDark = new(44, 28, 16);
    private static readonly Color Caption = new(214, 190, 150);
    private static readonly Color LowTime = new(255, 96, 64);
    private static readonly Color PowerShade = new(176, 120, 40);
    private static readonly Rectangle ScorePanel = new(6, 6, 190, 120);
    private static readonly Rectangle TimerPanel = new(288, 6, 64, 44);
    private static readonly Rectangle AimPanel = new(458, 6, 176, 84);
    private static readonly Rectangle ControlsPanel = new(6, 400, 236, 74);

    private readonly CannonFairAim Aim = new();
    private readonly List<UILabel> AimingOnly = [];
    private readonly UILabel AngleValue;
    private readonly UILabel PowerValue;
    private readonly UILabel ShotValue;
    private readonly UILabel StandTitle;
    private readonly UILabel[] StandRows = new UILabel[STAND_ROWS];
    private readonly UILabel VolleyTotal;
    private readonly UILabel TabTitle;
    private readonly UILabel[] TabRows = new UILabel[MAX_PLAYERS];
    private readonly UILabel ResultsTitle;
    private readonly UILabel[] ResultsRows = new UILabel[MAX_PLAYERS];
    private readonly UILabel WinnersLabel;
    private readonly UILabel CountdownLabel;
    private readonly CreatureRenderer Creatures;
    private readonly UILabel BannerLabel;
    private readonly Camera Camera = new(WINDOW_WIDTH, WINDOW_HEIGHT) { Position = new Vector2(WINDOW_WIDTH / 2f, WINDOW_HEIGHT / 2f) };
    private readonly UILabel[] Popups = new UILabel[POPUP_COUNT];
    private readonly Dictionary<int, AislingAppearance> Looks = [];
    private readonly AislingRenderer Renderer;
    private readonly UILabel TimerLabel;

    private CannonFairLeaveGuard LeaveGuard = new();
    private CannonFairSession? Session;
    private VolleyResult? LastVolley;
    private CannonFairArt? Art;
    private Texture2D? Pixel;
    private int ImpactMs;
    private long NowMs;
    private bool TabHeld;

    public event Action? LeaveRequested;

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

        StandTitle = AddLabel(ScorePanel.X + PANEL_PADDING, ScorePanel.Y + PANEL_PADDING, ScorePanel.Width - (2 * PANEL_PADDING), HorizontalAlignment.Left);
        StandTitle.Text = "Scores";
        StandTitle.ForegroundColor = Caption;

        for (var index = 0; index < STAND_ROWS; index++)
            StandRows[index] = AddLabel(
                ScorePanel.X + PANEL_PADDING,
                ScorePanel.Y + PANEL_PADDING + ((index + 1) * ROW_HEIGHT),
                ScorePanel.Width - (2 * PANEL_PADDING),
                HorizontalAlignment.Left);

        VolleyTotal = AddLabel(
            ScorePanel.X + PANEL_PADDING,
            ScorePanel.Y + PANEL_PADDING + ((STAND_ROWS + 1) * ROW_HEIGHT),
            ScorePanel.Width - (2 * PANEL_PADDING),
            HorizontalAlignment.Left);
        VolleyTotal.ForegroundColor = Caption;

        TimerLabel = AddLabel(TimerPanel.X, TimerPanel.Y + PANEL_PADDING, TimerPanel.Width, HorizontalAlignment.Center);
        AimingOnly.Add(TimerLabel);
        ShotValue = AddStat(AimPanel, 0, "Shot", true);
        AngleValue = AddStat(AimPanel, 1, "Angle", true);
        PowerValue = AddStat(AimPanel, 2, "Power", true);
        AddKeyRow(0, 0, "Up/Down", "Angle");
        AddKeyRow(0, 1, "Left/Right", "Power");
        AddKeyRow(0, 2, "Space", "Lock in");
        AddKeyRow(1, 0, "Tab", "Scores");
        AddKeyRow(1, 1, "Esc", "Leave");
        BannerLabel = AddLabel(ControlsPanel.Right, 456, WINDOW_WIDTH - ControlsPanel.Right, HorizontalAlignment.Center);

        TabTitle = AddCardLabel(0, "The standings");
        TabTitle.ForegroundColor = Caption;

        for (var index = 0; index < MAX_PLAYERS; index++)
            TabRows[index] = AddCardLabel(index + 1, string.Empty);

        ResultsTitle = AddCardLabel(0, string.Empty);
        ResultsTitle.ForegroundColor = Caption;

        for (var index = 0; index < MAX_PLAYERS; index++)
            ResultsRows[index] = AddCardLabel(index + 1, string.Empty);

        WinnersLabel = AddCardLabel(MAX_PLAYERS + 1, string.Empty);
        WinnersLabel.ForegroundColor = Gold;
        CountdownLabel = AddCardLabel(MAX_PLAYERS + 2, string.Empty);

        for (var index = 0; index < POPUP_COUNT; index++)
            Popups[index] = AddLabel(0, 0, 80, HorizontalAlignment.Center);
    }

    public void Open(CannonFairSession session)
    {
        Session = session;
        Pixel ??= CreatePixel();
        Art ??= new CannonFairArt();
        Looks.Clear();

        foreach (var entry in session.Roster)
            Looks[entry.Slot] = entry.Look is null
                ? TownImportPreviewLook.Plain(Gender.Male)
                : TownBallotLook.ToAppearance(entry.Look) with { WeaponSprite = 0, ShieldSprite = 0 };

        LeaveGuard = new CannonFairLeaveGuard();
        LastVolley = null;
        TabHeld = false;
        NowMs = Environment.TickCount64;
        ShowWindow();
        RefreshLabels();
    }

    public void Close() => CloseFromServer();

    public void CloseFromServer()
    {
        if (!Visible)
            return;

        InputDispatcher.Instance?.RemoveControl(this);
        Visible = false;
    }

    public void Update(float seconds)
    {
        if (Session is null)
            return;

        NowMs = Environment.TickCount64;

        if (!Session.IsOpen)
        {
            if (Visible)
                CloseFromServer();

            return;
        }

        if (!Visible)
            ShowWindow();

        RefreshLabels();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        e.Handled = true;

        if (e.Keycode == Keycode.Escape)
        {
            if (Session is not null)
            {
                if (LeaveGuard.Press(Environment.TickCount64))
                    LeaveRequested?.Invoke();
                else
                    WorldState.Chat.AddOrangeBarMessage("Press Esc again to leave");
            }

            return;
        }

        if (e.Keycode == Keycode.Tab)
        {
            TabHeld = true;

            return;
        }

        if ((Session is null) || !Session.CanAim || Session.Locked)
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
                if (Session.TryLockIn(Aim.AngleDegrees, Aim.Power))
                    WorldState.Chat.AddOrangeBarMessage("Locked in");

                break;
        }
    }

    public override void OnKeyUp(KeyUpEvent e)
    {
        if (e.Keycode != Keycode.Tab)
            return;

        TabHeld = false;
        e.Handled = true;
    }

    public override void OnMouseDown(MouseDownEvent e) => e.Handled = true;

    public override void OnMouseUp(MouseUpEvent e) => e.Handled = true;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || Pixel is null || Art is null || Session is null)
            return;

        spriteBatch.Draw(Art.Sky, new Vector2(ScreenX, ScreenY), Color.White);

        var replaying = Session.IsReplaying && (Session.Volley is not null) && (Session.Player is not null);

        if (replaying)
            DrawReplay(spriteBatch);
        else if (!Session.ShowingResults)
            DrawAiming(spriteBatch);
        else if (Session.Volley is not null && Session.Player is not null)
            DrawReplay(spriteBatch);
        else
            DrawAiming(spriteBatch);

        if (Session.ShowingResults)
            DrawResultsCard(spriteBatch);

        if (TabHeld && (Session.Standings.Count > 0))
            DrawTabCard(spriteBatch);

        DrawChrome(spriteBatch);

        base.Draw(spriteBatch);
    }

    public override void Dispose()
    {
        Pixel?.Dispose();
        Art?.Dispose();
        base.Dispose();
    }

    private void ShowWindow()
    {
        InputDispatcher.Instance?.PushControl(this);
        Visible = true;
    }

    private void DrawAiming(SpriteBatch spriteBatch)
    {
        DrawGround(spriteBatch, GROUND_Y);

        foreach (var piece in Session!.Pieces)
        {
            var kind = (ObjectKind)piece.Kind;
            var x = piece.X / RecordingCodec.POSITION_SCALE;
            var y = piece.Y / RecordingCodec.POSITION_SCALE;
            var rotation = RecordingCodec.DecodeRotation(piece.Rotation);

            if (kind == ObjectKind.Block)
                DrawBlock(
                    spriteBatch,
                    x,
                    y,
                    rotation,
                    piece.Width / RecordingCodec.SIZE_SCALE,
                    piece.Height / RecordingCodec.SIZE_SCALE,
                    (BlockMaterial)piece.Material,
                    0f);
            else if (kind is ObjectKind.Goblin or ObjectKind.King)
                DrawGoblin(spriteBatch, x, y, rotation, piece.Width / RecordingCodec.SIZE_SCALE, kind == ObjectKind.King, 0f);
        }

        for (var cannon = 1; cannon <= Session.CannonCount; cannon++)
        {
            var own = cannon == Session.Cannon;
            DrawCannon(spriteBatch, cannon, own ? Aim.AngleDegrees : DEFAULT_BARREL_ANGLE, own);
        }

        if (!Session.CanAim)
            return;

        for (var dot = 1; dot <= AIM_DOTS; dot++)
        {
            var (worldX, worldY) = Aim.Path(Session.Cannon, dot * AIM_DOT_SECONDS, Session.Shot);
            var (x, y, _) = CannonFairCamera.Project(worldX, worldY, 0f);
            FillRect(spriteBatch, (int)x - 1, ((int)y - 1) + BARREL_PIVOT_DROP, 3, 3, Gold);
        }
    }

    private void DrawReplay(SpriteBatch spriteBatch)
    {
        var volley = Session!.Volley!;
        var player = Session.Player!;

        if (!ReferenceEquals(volley, LastVolley))
        {
            LastVolley = volley;
            ImpactMs = CannonFairCamera.ImpactMs(volley);
        }

        var replayMs = Session.ReplayMs(NowMs);
        var zoom = CannonFairCamera.ZoomAt(replayMs, ImpactMs);
        var (_, groundY, _) = CannonFairCamera.Project(0f, 0f, zoom);
        DrawGround(spriteBatch, (int)groundY);

        foreach (var obj in volley.Objects)
        {
            var pose = player.Sample(obj.Id, replayMs);

            if (pose.Gone)
                continue;

            if (obj.Kind == ObjectKind.Body)
                DrawFlyer(spriteBatch, obj, pose, zoom);
            else if (obj.Kind is ObjectKind.Goblin or ObjectKind.King)
                DrawGoblin(spriteBatch, pose.X, pose.Y, pose.Rotation, obj.Width, obj.Kind == ObjectKind.King, zoom);
            else
                DrawBlock(spriteBatch, pose.X, pose.Y, pose.Rotation, obj.Width, obj.Height, obj.Material, zoom);
        }

        foreach (var blast in volley.Blasts)
        {
            var age = replayMs - blast.Ms;

            if (age is >= 0 and < BLAST_MS)
                DrawBlast(spriteBatch, blast.X, blast.Y, blast.Radius, 1f - (age / (float)BLAST_MS), zoom);
        }

        RefreshPopups(zoom, replayMs);
    }

    private void DrawFlyer(SpriteBatch spriteBatch, RecordedObject obj, ObjectPose pose, float zoom)
    {
        var (x, y, scale) = CannonFairCamera.Project(pose.X, pose.Y, zoom);
        var own = obj.OwnerPlayerId == Session!.OwnSlot;

        if (!Looks.TryGetValue(obj.OwnerPlayerId, out var look))
            look = TownImportPreviewLook.Plain(Gender.Male);

        var drawParams = new AislingDrawParams(
            LOOK_CACHE_BASE + (uint)obj.OwnerPlayerId,
            look,
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

    private void DrawTabCard(SpriteBatch spriteBatch)
        => DrawPlaque(spriteBatch, new Rectangle(CARD_X, CARD_Y, CARD_WIDTH, CARD_HEIGHT));

    private void DrawResultsCard(SpriteBatch spriteBatch)
        => DrawPlaque(spriteBatch, new Rectangle(CARD_X, CARD_Y, CARD_WIDTH, CARD_HEIGHT));

    private void DrawChrome(SpriteBatch spriteBatch)
    {
        DrawPlaque(spriteBatch, ScorePanel);

        if (Session!.Phase == MatchPhase.Aiming)
        {
            DrawPlaque(spriteBatch, TimerPanel);
            DrawPlaque(spriteBatch, AimPanel);
            DrawPlaque(spriteBatch, ControlsPanel);

            if (Session.CanAim)
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
        if (Session is null)
            return;

        var aiming = (Session.Phase == MatchPhase.Aiming) && Session.CanAim;

        if (Session.ShowingResults)
            TimerLabel.Text = $"{(Session.ResultsMsLeft(NowMs) + 999) / 1000}";
        else if (Session.IsReplaying && (Session.Player is not null))
            TimerLabel.Text = $"{Math.Max(0, Session.Player.DurationMs + REPLAY_HOLD_MS - Session.ReplayMs(NowMs)) / 1000}";
        else
            TimerLabel.Text = $"{(Session.AimMsLeft(NowMs) + 999) / 1000}";

        TimerLabel.ForegroundColor = !Session.ShowingResults && !Session.IsReplaying && (Session.AimMsLeft(NowMs) <= LOW_TIME_MS) ? LowTime : Color.White;
        ShotValue.Text = ShotName(Session.Shot);
        AngleValue.Text = $"{Aim.AngleDegrees:F0}";
        PowerValue.Text = $"{Aim.Power * 100f:F0}%";

        foreach (var label in AimingOnly)
            label.Visible = aiming;

        var rows = StandingsRows();

        for (var index = 0; index < StandRows.Length; index++)
        {
            var label = StandRows[index];

            if (index >= rows.Count)
            {
                label.Visible = false;

                continue;
            }

            var row = rows[index];
            label.Text = StandingLine(row);
            label.ForegroundColor = row.Slot == Session.OwnSlot ? Gold : Color.White;
            label.Visible = true;
        }

        var replayMs = Session.ReplayMs(NowMs);
        var volleyTotal = Session.Volley?.Scores.Where(score => (score.Ms <= replayMs) && (score.PlayerId == Session.OwnSlot)).Sum(score => score.Points) ?? 0;
        VolleyTotal.Text = $"This volley +{volleyTotal}";

        var ordered = Session.Standings.OrderBy(row => row.Place).ToList();

        for (var index = 0; index < TabRows.Length; index++)
        {
            var label = TabRows[index];
            var show = TabHeld && (index < ordered.Count);

            if (!show)
            {
                label.Visible = false;

                continue;
            }

            var row = ordered[index];
            label.Text = StandingLine(row);
            label.ForegroundColor = row.Slot == Session.OwnSlot ? Gold : Color.White;
            label.Visible = true;
        }

        TabTitle.Visible = TabHeld && (ordered.Count > 0);

        for (var index = 0; index < ResultsRows.Length; index++)
        {
            var label = ResultsRows[index];
            var show = Session.ShowingResults && (index < ordered.Count);

            if (!show)
            {
                label.Visible = false;

                continue;
            }

            var row = ordered[index];
            label.Text = StandingLine(row);
            label.ForegroundColor = row.Slot == Session.OwnSlot ? Gold : Color.White;
            label.Visible = true;
        }

        ResultsTitle.Visible = Session.ShowingResults;
        WinnersLabel.Visible = Session.ShowingResults && Session.IsFinal;
        CountdownLabel.Visible = Session.ShowingResults;

        if (Session.ShowingResults)
        {
            ResultsTitle.Text = Session.IsFinal ? "The Final Standings" : $"Fort {Session.FortNumber + 1} Standings";

            var winners = ordered.Where(row => row.Place == 1).Select(row => row.Name).ToList();
            WinnersLabel.Text = winners.Count switch
            {
                0 => string.Empty,
                1 => $"The laurel goes to {winners[0]}",
                _ => $"Laurels: {string.Join(", ", winners)}"
            };

            var seconds = (Session.ResultsMsLeft(NowMs) + 999) / 1000;
            CountdownLabel.Text = Session.IsFinal ? $"The fair ends in {seconds}" : $"Next fort in {seconds}";
        }

        if (Session.ShowingResults)
            BannerLabel.Text = string.Empty;
        else if (Session.IsReplaying)
            BannerLabel.Text = (Session.Volley?.Truncated ?? false) ? "Replay cut short: simulation hit its time limit" : string.Empty;
        else if (Session.Locked)
            BannerLabel.Text = "Waiting for the volley...";
        else if (!Session.CanAim)
            BannerLabel.Text = "Watching the fair";
        else
            BannerLabel.Text = string.Empty;

        if (!Session.IsReplaying || (Session.Volley is null))
            HidePopups();
    }

    private List<CannonFairStandingInfo> StandingsRows()
    {
        var rows = Session!.TopThree.ToList();

        if (Session.Own is { } own && rows.All(row => row.Slot != own.Slot))
            rows.Add(own);

        return rows;
    }

    private static string StandingLine(CannonFairStandingInfo row) => $"{row.Place}. {row.Name} {row.Score}";

    private string ScorerName(int playerId)
        => Session!.Roster.FirstOrDefault(entry => entry.Slot == playerId)?.Name ?? UNKNOWN_SCORER;

    private void RefreshPopups(float zoom, int replayMs)
    {
        var used = 0;

        foreach (var score in Session!.Volley!.Scores)
        {
            var age = replayMs - score.Ms;

            if (age is < 0 or >= POPUP_MS || (used >= POPUP_COUNT))
                continue;

            var own = score.PlayerId == Session.OwnSlot;
            var (x, y, _) = CannonFairCamera.Project(score.X, score.Y, zoom);
            var popup = Popups[used++];
            popup.X = (int)x - (popup.Width / 2);
            popup.Y = (int)y - (age / 30);
            popup.Text = own ? $"+{score.Points}" : $"+{score.Points} {ScorerName(score.PlayerId)}";
            popup.ForegroundColor = own ? Color.White : Color.LightSkyBlue;
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

    private UILabel AddCardLabel(int row, string text)
    {
        var label = AddLabel(CARD_X + PANEL_PADDING, CARD_Y + PANEL_PADDING + (row * ROW_HEIGHT), CARD_WIDTH - (2 * PANEL_PADDING), HorizontalAlignment.Left);
        label.Text = text;
        label.Visible = false;

        return label;
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
