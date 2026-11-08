using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.DarkAges.BrassRing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static Chaos.DarkAges.BrassRing.BrassRingRules;

namespace Chaos.Client.Controls.World.Popups.BrassRing;

/// <summary>The fight window. In this stage it runs the fight locally against <see cref="BrassRingDummy" />.</summary>
public sealed class BrassRingControl : UIPanel
{
    private const int WINDOW_WIDTH = 640;
    private const int WINDOW_HEIGHT = 480;
    private const int PLAYER = 0;
    private const int DUMMY = 1;
    private const uint NEAR_CACHE_ID = 0xFFFFFF01;
    private const uint FAR_CACHE_ID = 0xFFFFFF02;
    private const float SCALE = 3f;
    private const int NO_EMOTION = -1;
    private const int FLASH_MS = 200;
    private const int SPARK_MS = 150;
    private const int RESULTS_MS = 5000;
    private const int HIT_SOUND = 125;
    private const int BLOCK_SOUND = 1;
    private const int BAR_WIDTH = 240;
    private const int STAMINA_WIDTH = 170;
    private const string KEY_NOTE = "A left   D right   S block   arrows dodge";

    private static readonly Vector2 NearFeet = new(262, 375);
    private static readonly Vector2 FarFeet = new(382, 295);
    private static readonly Color NearColor = new(217, 72, 59);
    private static readonly Color FarColor = new(59, 127, 217);
    private static readonly Color StaminaColor = new(224, 177, 62);
    private static readonly Color EmptyColor = new(50, 48, 58);
    private static readonly Color StripColor = new(20, 18, 27);

    private readonly UILabel BannerLabel;
    private readonly Camera Camera = new(WINDOW_WIDTH, WINDOW_HEIGHT) { Position = new Vector2(WINDOW_WIDTH / 2f, WINDOW_HEIGHT / 2f) };
    private readonly List<BrassRingEvent> Events = [];
    private readonly int[] FlashMs = new int[2];
    private readonly UILabel FarNameLabel;
    private readonly UILabel NearNameLabel;
    private readonly UILabel RoundLabel;
    private readonly AislingRenderer Renderer;
    private readonly SoundSystem Sounds;
    private readonly UILabel TimeLabel;

    private int CarryMs;
    private BrassRingDummy Dummy = new(Random.Shared.Next);
    private BrassRingFight Fight = new();
    private AislingAppearance NearLook;
    private AislingAppearance FarLook;
    private int ResultsMs = -1;
    private int SparkMs;
    private Vector2 SparkAt;

    public BrassRingControl(AislingRenderer renderer, SoundSystem sounds)
    {
        Renderer = renderer;
        Sounds = sounds;
        Name = "BrassRing";
        Visible = false;
        UsesControlStack = true;
        X = 0;
        Y = 0;
        Width = WINDOW_WIDTH;
        Height = WINDOW_HEIGHT;

        NearNameLabel = AddLabel(16, 6, BAR_WIDTH, HorizontalAlignment.Left);
        FarNameLabel = AddLabel(WINDOW_WIDTH - 16 - BAR_WIDTH, 6, BAR_WIDTH, HorizontalAlignment.Right);
        RoundLabel = AddLabel(WINDOW_WIDTH / 2 - 40, 6, 80, HorizontalAlignment.Center);
        TimeLabel = AddLabel(WINDOW_WIDTH / 2 - 40, 22, 80, HorizontalAlignment.Center);
        BannerLabel = AddLabel(0, 200, WINDOW_WIDTH, HorizontalAlignment.Center);
        AddLabel(0, WINDOW_HEIGHT - 22, WINDOW_WIDTH, HorizontalAlignment.Center).Text = KEY_NOTE;
    }

    public void Open(string playerName, in AislingAppearance look, in AislingAppearance dummyLook)
    {
        Fight = new BrassRingFight();
        Dummy = new BrassRingDummy(Random.Shared.Next);
        NearLook = look with { WeaponSprite = 0, ShieldSprite = 0 };
        FarLook = dummyLook with { WeaponSprite = 0, ShieldSprite = 0 };
        NearNameLabel.Text = playerName;
        FarNameLabel.Text = "Training Dummy";
        CarryMs = 0;
        ResultsMs = -1;
        SparkMs = 0;
        Array.Clear(FlashMs);
        BannerLabel.Text = string.Empty;
        InputDispatcher.Instance?.PushControl(this);
        Visible = true;
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

        CarryMs += (int)(seconds * 1000f);

        while (CarryMs >= STEP_MS)
        {
            CarryMs -= STEP_MS;
            StepFight();
        }

        RefreshLabels();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Close();
            e.Handled = true;

            return;
        }

        if (BrassRingKeys.Pressed(e.Keycode, e.IsRepeat) is not { } move)
            return;

        Fight.TryMove(PLAYER, move, Events);
        e.Handled = true;
    }

    public override void OnKeyUp(KeyUpEvent e)
    {
        if (BrassRingKeys.Released(e.Keycode) is not { } move)
            return;

        Fight.TryMove(PLAYER, move, Events);
        e.Handled = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        DrawSheet(spriteBatch, "arena", 0, Vector2.Zero);
        DrawFighter(spriteBatch, FarLook, FAR_CACHE_ID, Fight.Fighters[DUMMY], FarFeet, false, FlashMs[DUMMY] > 0);
        DrawFighter(spriteBatch, NearLook, NEAR_CACHE_ID, Fight.Fighters[PLAYER], NearFeet, true, FlashMs[PLAYER] > 0);
        DrawSheet(spriteBatch, "ropes", 0, Vector2.Zero);

        if (SparkMs > 0)
            DrawSheet(spriteBatch, "spark", 0, SparkAt);

        DrawBars(spriteBatch);
        base.Draw(spriteBatch);
    }

    private void StepFight()
    {
        if (Dummy.Tick(STEP_MS, Fight, DUMMY) is { } move)
            Fight.TryMove(DUMMY, move, Events);

        Fight.Step(STEP_MS, Events);
        FlashMs[0] = Math.Max(0, FlashMs[0] - STEP_MS);
        FlashMs[1] = Math.Max(0, FlashMs[1] - STEP_MS);
        SparkMs = Math.Max(0, SparkMs - STEP_MS);

        foreach (var fightEvent in Events)
            Apply(fightEvent);

        Events.Clear();

        if (ResultsMs < 0)
            return;

        ResultsMs -= STEP_MS;

        if (ResultsMs < 0)
            Close();
    }

    private void Apply(BrassRingEvent fightEvent)
    {
        switch (fightEvent.Type)
        {
            case BrassRingEventType.Hit:
                FlashMs[fightEvent.Fighter] = FLASH_MS;
                SparkMs = SPARK_MS;
                SparkAt = (fightEvent.Fighter == PLAYER ? NearFeet : FarFeet) - new Vector2(0, 100);
                Sounds.PlaySound(HIT_SOUND);

                break;
            case BrassRingEventType.Blocked:
                Sounds.PlaySound(BLOCK_SOUND);

                break;
            case BrassRingEventType.RoundStarted:
                BannerLabel.Text = string.Empty;

                break;
            case BrassRingEventType.RoundEnded:
                BannerLabel.Text = fightEvent.Fighter == NO_WINNER ? "Drawn round" : fightEvent.Fighter == PLAYER ? "You take the round" : "The dummy takes the round";

                break;
            case BrassRingEventType.MatchEnded:
                BannerLabel.Text = fightEvent.Fighter == NO_WINNER ? "A drawn match" : fightEvent.Fighter == PLAYER ? "Victory" : "Defeat";
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

        Renderer.Draw(spriteBatch, Camera, in drawParams);

        if (fighter.Pose == BrassRingPose.Down)
        {
            DrawSheet(spriteBatch, "stars", 0, feet - new Vector2(0, 40));

            return;
        }

        var gloves = BrassRingView.GloveOffsets(sprite, look.Gender);
        DrawSheet(spriteBatch, "glove", near ? 0 : 1, feet + gloves.Left);
        DrawSheet(spriteBatch, "glove", near ? 0 : 1, feet + gloves.Right);
    }

    private void DrawBars(SpriteBatch spriteBatch)
    {
        var near = Fight.Fighters[PLAYER];
        var far = Fight.Fighters[DUMMY];
        var white = BrassRingArtAssets.Texture(ChaosGame.Device, "white");

        Bar(spriteBatch, white, 16, 24, BAR_WIDTH, 16, near.Health / (float)MAX_HEALTH, NearColor, false);
        Bar(spriteBatch, white, 16, 44, STAMINA_WIDTH, 7, near.Stamina / (float)MAX_STAMINA, StaminaColor, false);
        Bar(spriteBatch, white, WINDOW_WIDTH - 16 - BAR_WIDTH, 24, BAR_WIDTH, 16, far.Health / (float)MAX_HEALTH, FarColor, true);
        spriteBatch.Draw(white, new Rectangle(ScreenX, ScreenY + WINDOW_HEIGHT - 30, WINDOW_WIDTH, 30), StripColor);
    }

    private void Bar(SpriteBatch spriteBatch, Texture2D white, int x, int y, int width, int height, float fraction, Color color, bool fromRight)
    {
        spriteBatch.Draw(white, new Rectangle(ScreenX + x, ScreenY + y, width, height), EmptyColor);
        var filled = (int)(width * fraction);
        var left = fromRight ? x + width - filled : x;
        spriteBatch.Draw(white, new Rectangle(ScreenX + left, ScreenY + y, filled, height), color);
    }

    private void DrawSheet(SpriteBatch spriteBatch, string key, int frame, Vector2 at)
    {
        var sheet = BrassRingArtAssets.ReadManifest()[key];
        var dest = new Rectangle(ScreenX + (int)at.X - sheet.AnchorX, ScreenY + (int)at.Y - sheet.AnchorY, sheet.FrameWidth, sheet.FrameHeight);
        spriteBatch.Draw(BrassRingArtAssets.Texture(ChaosGame.Device, key), dest, sheet.Frame(frame), Color.White);
    }

    private void RefreshLabels()
    {
        RoundLabel.Text = $"Round {Fight.Round}";
        var seconds = Fight.Phase == BrassRingPhase.Round ? (int)Math.Ceiling(Fight.PhaseMs / 1000f) : 0;
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
