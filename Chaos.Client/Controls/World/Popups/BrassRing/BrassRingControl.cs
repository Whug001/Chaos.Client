using Chaos.Client.Controls.Components;
using Chaos.Client.Data;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.DarkAges.BrassRing;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
using static Chaos.DarkAges.BrassRing.BrassRingRules;

namespace Chaos.Client.Controls.World.Popups.BrassRing;

/// <summary>The fight window. It shows the server's fight and sends the fighter's moves back.</summary>
public sealed class BrassRingControl : UIPanel
{
    private const int WINDOW_WIDTH = 640;
    private const int WINDOW_HEIGHT = 480;
    private const uint NEAR_CACHE_ID = 0xFFFFFF01;
    private const uint FAR_CACHE_ID = 0xFFFFFF02;
    private const float SCALE = 3f;
    private const int NO_EMOTION = -1;
    private const int FLASH_MS = 200;
    private const int SPARK_MS = 150;
    private const int HIT_SOUND = 125;
    private const int BLOCK_SOUND = 1;
    private const int BAR_WIDTH = 240;
    private const int STAMINA_WIDTH = 170;
    private const int BANNER_Y = 369;
    private const int PIP_SPACING = 20;
    private const float TRAIL_PER_STEP = 0.75f;
    private const float TRAIL_PER_MS = TRAIL_PER_STEP / STEP_MS;
    private const int KEY_ROW_Y = WINDOW_HEIGHT - 15;
    private const int KEY_LABEL_Y = WINDOW_HEIGHT - 22;
    private const int SIDE_PLATE_WIDTH = 264;
    private const int SIDE_PLATE_HEIGHT = 62;
    private const int CENTRE_PLATE_WIDTH = 96;
    private const int CENTRE_PLATE_HEIGHT = 44;
    private const int KEY_PLATE_HEIGHT = 32;
    private const int BANNER_PLATE_WIDTH = 280;
    private const int BANNER_PLATE_HEIGHT = 44;

    private static readonly Vector2 NearFeet = new(260, 290);
    private static readonly Vector2 FarFeet = new(380, 230);
    private static readonly Color NearColor = new(217, 72, 59);
    private static readonly Color FarColor = new(59, 127, 217);
    private static readonly Color StaminaColor = new(224, 177, 62);
    private static readonly Color TrailColor = new(235, 225, 200);
    private static readonly Color GrooveFill = new(30, 20, 13);
    private static readonly Color GrooveShadow = new(15, 9, 6);
    private static readonly Color GrooveLight = new(150, 112, 70);
    private static readonly (int X, int Frame)[] KeyIcons = [(149, 0), (237, 1), (333, 2), (429, 3), (455, 4)];

    private readonly UILabel BannerLabel;
    private readonly Camera Camera = new(WINDOW_WIDTH, WINDOW_HEIGHT) { Position = new Vector2(WINDOW_WIDTH / 2f, WINDOW_HEIGHT / 2f) };
    private readonly int[] FlashMs = new int[2];
    private readonly float[] TrailHealth = new float[2];
    private readonly UILabel FarNameLabel;
    private readonly UILabel NearNameLabel;
    private readonly UILabel RoundLabel;
    private readonly AislingRenderer Renderer;
    private readonly SoundSystem Sounds;
    private readonly UILabel TimeLabel;

    private Texture2D? SidePlate;
    private Texture2D? CentrePlate;
    private Texture2D? KeyPlate;
    private Texture2D? BannerPlate;
    private readonly BrassRingState State = new();
    private string BannerBeforeConfirm = string.Empty;
    private bool BlockHeld;
    private bool ConfirmShown;
    private float CarryMs;
    private string[] Names = ["", ""];
    private AislingAppearance NearLook;
    private AislingAppearance FarLook;
    private int ResultsMs = -1;
    private int SparkMs;
    private Vector2 SparkAt;

    public event Action<BrassRingMove>? MoveRequested;
    public event Action? ForfeitRequested;

    public BrassRingControl(AislingRenderer renderer, SoundSystem sounds)
    {
        Renderer = renderer;
        Sounds = sounds;
        Name = "BrassRing";
        Visible = false;
        X = 0;
        Y = 0;
        Width = WINDOW_WIDTH;
        Height = WINDOW_HEIGHT;

        NearNameLabel = AddLabel(16, 6, BAR_WIDTH, HorizontalAlignment.Left);
        FarNameLabel = AddLabel(WINDOW_WIDTH - 16 - BAR_WIDTH, 6, BAR_WIDTH, HorizontalAlignment.Right);
        RoundLabel = AddLabel(WINDOW_WIDTH / 2 - 40, 6, 80, HorizontalAlignment.Center);
        TimeLabel = AddLabel(WINDOW_WIDTH / 2 - 40, 22, 80, HorizontalAlignment.Center);
        BannerLabel = AddLabel(0, BANNER_Y, WINDOW_WIDTH, HorizontalAlignment.Center);
        AddLabel(165, KEY_LABEL_Y, 40, HorizontalAlignment.Left).Text = "Left";
        AddLabel(253, KEY_LABEL_Y, 44, HorizontalAlignment.Left).Text = "Right";
        AddLabel(349, KEY_LABEL_Y, 44, HorizontalAlignment.Left).Text = "Block";
        AddLabel(471, KEY_LABEL_Y, 48, HorizontalAlignment.Left).Text = "Dodge";
    }

    public void Open(BrassRingDisplayArgs args)
    {
        if ((args.YourIndex > 1) || (args.Names.Count < 2) || (args.Looks.Count < 2))
            return;

        EnsurePlates();
        State.Reset(args.YourIndex);
        Names = args.Names.ToArray();
        NearLook = TownBallotLook.ToAppearance(args.Looks[State.You]);
        FarLook = TownBallotLook.ToAppearance(args.Looks[1 - State.You]);
        NearNameLabel.Text = Names[State.You];
        FarNameLabel.Text = Names[1 - State.You];
        CarryMs = 0;
        ResultsMs = -1;
        SparkMs = 0;
        BlockHeld = false;
        ConfirmShown = false;
        Array.Clear(FlashMs);
        BannerLabel.Text = "Get ready";
        Array.Fill(TrailHealth, (float)MAX_HEALTH);
        InputDispatcher.Instance?.PushControl(this);
        Visible = true;
    }

    public void ApplyTick(BrassRingDisplayArgs args)
    {
        State.ApplyTick(args);

        foreach (var fightEvent in args.Events)
            Apply(fightEvent);
    }

    public void CloseFromServer(BrassRingCloseReason reason)
    {
        if (reason == BrassRingCloseReason.YouLeft)
        {
            Close();

            return;
        }

        BannerLabel.Text = reason == BrassRingCloseReason.OpponentLeft ? $"{Names[1 - State.You]} has left the ring" : "No contest";
        ConfirmShown = false;
        ResultsMs = RESULTS_MS;
    }

    public void Close()
    {
        if (!Visible)
            return;

        InputDispatcher.Instance?.RemoveControl(this);
        Visible = false;
    }

    public void Update(float seconds)
    {
        if (!Visible)
            return;

        CarryMs += seconds * 1000f;
        var ms = (int)CarryMs;
        CarryMs -= ms;
        State.Advance(ms);
        FlashMs[0] = Math.Max(0, FlashMs[0] - ms);
        FlashMs[1] = Math.Max(0, FlashMs[1] - ms);
        SparkMs = Math.Max(0, SparkMs - ms);

        for (var i = 0; i < TrailHealth.Length; i++)
            TrailHealth[i] = Math.Max(State.Fighters[i].Health, TrailHealth[i] - (TRAIL_PER_MS * ms));

        if (ConfirmShown && !State.ForfeitArmed)
        {
            ConfirmShown = false;
            BannerLabel.Text = BannerBeforeConfirm;
        }

        //popups that open mid-fight would steal the fighter's keys while hidden under the ring,
        //so the ring keeps the top of the stack until the result closes it and they surface after
        if ((ResultsMs < 0) && (InputDispatcher.Instance?.TopControl != this))
            InputDispatcher.Instance?.PushControl(this);

        RefreshLabels();

        if (ResultsMs < 0)
            return;

        ResultsMs -= ms;

        if (ResultsMs < 0)
            Close();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        e.Handled = true;

        if (e.Keycode == Keycode.Escape)
        {
            PressEscape();

            return;
        }

        if ((ResultsMs >= 0) || BrassRingKeys.Pressed(e.Keycode, e.IsRepeat) is not { } move)
            return;

        BlockHeld |= move == BrassRingMove.BlockStart;
        State.Preview(move);
        MoveRequested?.Invoke(move);
    }

    public override void OnKeyUp(KeyUpEvent e)
    {
        e.Handled = true;

        if ((ResultsMs >= 0) || BrassRingKeys.Released(e.Keycode) is not { } move)
            return;

        BlockHeld = false;
        State.Preview(move);
        MoveRequested?.Invoke(move);
    }

    private void PressEscape()
    {
        if (ResultsMs >= 0)
        {
            Close();

            return;
        }

        if (State.PressEscape())
        {
            ForfeitRequested?.Invoke();

            return;
        }

        BannerBeforeConfirm = BannerLabel.Text ?? string.Empty;
        BannerLabel.Text = "Press Esc again to give up";
        ConfirmShown = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        DrawSheet(spriteBatch, "arena", 0, Vector2.Zero);
        DrawFighter(spriteBatch, FarLook, FAR_CACHE_ID, State.Drawn(1 - State.You), FarFeet, false, FlashMs[1 - State.You] > 0);
        DrawFighter(spriteBatch, NearLook, NEAR_CACHE_ID, State.Drawn(State.You), NearFeet, true, FlashMs[State.You] > 0);
        DrawSheet(spriteBatch, "ropes", 0, Vector2.Zero);

        if (SparkMs > 0)
            DrawSheet(spriteBatch, "spark", 0, SparkAt);

        DrawBars(spriteBatch);
        base.Draw(spriteBatch);
    }


    private void Apply(BrassRingEvent fightEvent)
    {
        var yours = fightEvent.Fighter == State.You;

        switch (fightEvent.Type)
        {
            case BrassRingEventType.Hit:
                FlashMs[fightEvent.Fighter] = FLASH_MS;
                SparkMs = SPARK_MS;
                SparkAt = (yours ? NearFeet : FarFeet) - new Vector2(0, 100);
                Sounds.PlaySound(HIT_SOUND);

                break;
            case BrassRingEventType.Blocked:
                Sounds.PlaySound(BLOCK_SOUND);

                break;
            case BrassRingEventType.RoundStarted:
                BannerLabel.Text = string.Empty;
                ConfirmShown = false;
                State.DisarmForfeit();

                if (BlockHeld && (State.Fighters[State.You].Stamina > 0))
                {
                    State.Preview(BrassRingMove.BlockStart);
                    MoveRequested?.Invoke(BrassRingMove.BlockStart);
                }

                break;
            case BrassRingEventType.RoundEnded:
                var name = fightEvent.Fighter == NO_WINNER ? string.Empty : Names[fightEvent.Fighter];
                BannerLabel.Text = fightEvent.Fighter == NO_WINNER ? "Drawn round" : yours ? "You take the round" : $"{name} takes the round";
                ConfirmShown = false;
                State.DisarmForfeit();

                break;
            case BrassRingEventType.MatchEnded:
                BannerLabel.Text = fightEvent.Fighter == NO_WINNER ? "A drawn match" : yours ? "Victory" : "Defeat";
                ConfirmShown = false;
                State.DisarmForfeit();
                ResultsMs = RESULTS_MS;

                break;
        }
    }

    private void DrawFighter(SpriteBatch spriteBatch, in AislingAppearance look, uint cacheId, BrassRingFighter fighter, Vector2 feet, bool near, bool flash)
    {
        var sprite = BrassRingView.Fighter(fighter, near, flash);

        var drawParams = new AislingDrawParams(
            cacheId,
            look,
            sprite.Frame,
            sprite.Flip,
            !near,
            sprite.Suffix,
            NO_EMOTION,
            0,
            default,
            feet.X,
            feet.Y,
            sprite.Offset,
            sprite.Flash ? EntityTintType.HitTint : EntityTintType.None,
            default,
            false,
            1f,
            SCALE,
            sprite.Rotation);

        var gender = look.Gender;
        var leftBehind = BrassRingView.LeftGloveBehindBody(sprite, near) && (fighter.Pose != BrassRingPose.Down);
        var gloves = BrassRingView.GloveOffsets(sprite, gender);
        var gloveFrame = near ? 0 : 1;

        if (leftBehind)
            DrawSheet(spriteBatch, "glove", gloveFrame, feet + gloves.Left);

        Renderer.Draw(spriteBatch, Camera, in drawParams);

        if (fighter.Pose == BrassRingPose.Down)
        {
            DrawSheet(spriteBatch, "stars", 0, feet - new Vector2(0, 40));

            return;
        }

        if (!leftBehind)
            DrawSheet(spriteBatch, "glove", gloveFrame, feet + gloves.Left);

        DrawSheet(spriteBatch, "glove", gloveFrame, feet + gloves.Right);
    }

    private void DrawBars(SpriteBatch spriteBatch)
    {
        var near = State.Fighters[State.You];
        var far = State.Fighters[1 - State.You];
        var white = BrassRingArtAssets.Texture(ChaosGame.Device, "white");
        var stamina = near.Stamina / (float)MAX_STAMINA;
        var farBarX = WINDOW_WIDTH - 16 - BAR_WIDTH;

        DrawPlate(spriteBatch, SidePlate, 4, 0);
        DrawPlate(spriteBatch, SidePlate, WINDOW_WIDTH - 4 - SIDE_PLATE_WIDTH, 0);
        DrawPlate(spriteBatch, CentrePlate, (WINDOW_WIDTH - CENTRE_PLATE_WIDTH) / 2, 0);

        Groove(spriteBatch, white, 16, 24, BAR_WIDTH, 16);
        Groove(spriteBatch, white, 16, 44, STAMINA_WIDTH, 7);
        Groove(spriteBatch, white, farBarX, 24, BAR_WIDTH, 16);

        Bar(spriteBatch, white, 16, 24, BAR_WIDTH, 16, near.Health / (float)MAX_HEALTH, TrailHealth[State.You] / MAX_HEALTH, NearColor, false);
        Bar(spriteBatch, white, 16, 44, STAMINA_WIDTH, 7, stamina, stamina, StaminaColor, false);
        Bar(spriteBatch, white, farBarX, 24, BAR_WIDTH, 16, far.Health / (float)MAX_HEALTH, TrailHealth[1 - State.You] / MAX_HEALTH, FarColor, true);

        for (var i = 0; i < ROUNDS_TO_WIN; i++)
        {
            DrawSheet(spriteBatch, "pip", i < near.RoundWins ? 1 : 0, new Vector2(212 + i * PIP_SPACING, 48));
            DrawSheet(spriteBatch, "pip", i < far.RoundWins ? 1 : 0, new Vector2(608 - i * PIP_SPACING, 48));
        }

        DrawPlate(spriteBatch, KeyPlate, 0, WINDOW_HEIGHT - KEY_PLATE_HEIGHT);

        foreach (var (x, frame) in KeyIcons)
            DrawSheet(spriteBatch, "keys", frame, new Vector2(x, KEY_ROW_Y));

        if (!string.IsNullOrEmpty(BannerLabel.Text))
            DrawPlate(spriteBatch, BannerPlate, (WINDOW_WIDTH - BANNER_PLATE_WIDTH) / 2, BANNER_Y - 16);
    }

    private void DrawPlate(SpriteBatch spriteBatch, Texture2D? plate, int x, int y)
    {
        if (plate is not null)
            spriteBatch.Draw(plate, new Vector2(ScreenX + x, ScreenY + y), Color.White);
    }

    private void Groove(SpriteBatch spriteBatch, Texture2D white, int x, int y, int width, int height)
    {
        var left = ScreenX + x - 2;
        var top = ScreenY + y - 2;
        var outerWidth = width + 4;
        var outerHeight = height + 4;

        spriteBatch.Draw(white, new Rectangle(left, top, outerWidth, outerHeight), GrooveFill);
        spriteBatch.Draw(white, new Rectangle(left, top, outerWidth, 1), GrooveShadow);
        spriteBatch.Draw(white, new Rectangle(left, top, 1, outerHeight), GrooveShadow);
        spriteBatch.Draw(white, new Rectangle(left, top + outerHeight - 1, outerWidth, 1), GrooveLight);
        spriteBatch.Draw(white, new Rectangle(left + outerWidth - 1, top, 1, outerHeight), GrooveLight);
    }

    private void EnsurePlates()
    {
        if (SidePlate is not null)
            return;

        using var tile = DataContext.UserControls.GetSpfImage("DlgBack2.spf");

        if (tile is null)
            return;

        SidePlate = BuildPlate(tile, SIDE_PLATE_WIDTH, SIDE_PLATE_HEIGHT);
        CentrePlate = BuildPlate(tile, CENTRE_PLATE_WIDTH, CENTRE_PLATE_HEIGHT);
        KeyPlate = BuildPlate(tile, WINDOW_WIDTH, KEY_PLATE_HEIGHT);
        BannerPlate = BuildPlate(tile, BANNER_PLATE_WIDTH, BANNER_PLATE_HEIGHT);
    }

    private static Texture2D? BuildPlate(SKImage tile, int width, int height)
    {
        using var composite = DialogFrame.Composite(tile, width, height);

        return composite is null ? null : TextureConverter.ToTexture2D(composite);
    }

    public override void Dispose()
    {
        SidePlate?.Dispose();
        CentrePlate?.Dispose();
        KeyPlate?.Dispose();
        BannerPlate?.Dispose();

        base.Dispose();
    }

    private void Bar(SpriteBatch spriteBatch, Texture2D white, int x, int y, int width, int height, float fraction, float trail, Color color, bool fromRight)
    {
        var trailWidth = (int)(width * trail);
        spriteBatch.Draw(white, new Rectangle(ScreenX + (fromRight ? x + width - trailWidth : x), ScreenY + y, trailWidth, height), TrailColor);

        var filled = (int)(width * fraction);
        var left = ScreenX + (fromRight ? x + width - filled : x);
        var band = Math.Max(height / 4, 1);

        spriteBatch.Draw(white, new Rectangle(left, ScreenY + y, filled, height), color);
        spriteBatch.Draw(white, new Rectangle(left, ScreenY + y, filled, band), Color.Lerp(color, Color.White, 0.35f));
        spriteBatch.Draw(white, new Rectangle(left, ScreenY + y + height - band, filled, band), Color.Lerp(color, Color.Black, 0.3f));
    }

    private void DrawSheet(SpriteBatch spriteBatch, string key, int frame, Vector2 at)
    {
        var sheet = BrassRingArtAssets.ReadManifest()[key];
        var dest = new Rectangle(ScreenX + (int)at.X - sheet.AnchorX, ScreenY + (int)at.Y - sheet.AnchorY, sheet.FrameWidth, sheet.FrameHeight);
        spriteBatch.Draw(BrassRingArtAssets.Texture(ChaosGame.Device, key), dest, sheet.Frame(frame), Color.White);
    }

    private void RefreshLabels()
    {
        RoundLabel.Text = $"Round {State.Round}";
        var seconds = State.Phase == BrassRingPhase.Round ? (int)Math.Ceiling(State.PhaseMs / 1000f) : 0;
        TimeLabel.Text = $"{seconds / 60}:{seconds % 60:00}";
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
