#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.KartRacing;
using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.KeyBinds;
using Chaos.Client.Systems.Karting;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private readonly KartClock KartClock = new();
    private readonly KartInterpolator KartInterpolator = new();
    private readonly KartSpectator KartSpectator = new();
    private readonly List<KartDraw> KartDraws = [];
    private byte? KartFollowedDrawIndex;
    private Vector2? KartSpectatorCameraTarget;
    private readonly KartFinishHandover KartFinishHandover = new();
    private KartResultsControl? KartResults;
    private string? KartBannerText;
    private double KartBannerUntil;
    private readonly KartInputSampler KartSampler = new();
    private KartPredictor? KartPredictor;
    private double KartAccumulator;
    private bool KartPredictorInitialized;
    private static readonly Color[] KartDyes = [Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.Orange, Color.Purple, Color.Cyan, Color.White];
    private static KartRaceState KartRace => WorldState.KartRace;

    private readonly record struct KartDraw(int Depth, byte Index, Vector2 Center, int Frame);

    private void WireKartRace()
    {
        Game.Connection.KartTrackLayoutReceived += HandleKartLayout;
        Game.Connection.KartSnapshotReceived += HandleKartSnapshot;
        Game.Connection.KartRaceEventReceived += HandleKartEvent;
    }

    private void UnwireKartRace()
    {
        Game.Connection.KartTrackLayoutReceived -= HandleKartLayout;
        Game.Connection.KartSnapshotReceived -= HandleKartSnapshot;
        Game.Connection.KartRaceEventReceived -= HandleKartEvent;
    }

    private void HandleKartLayout(KartTrackLayoutArgs args)
    {
        KartInterpolator.Clear();
        KartSpectatorCameraTarget = null;
        KartFinishHandover.Reset();
        KartRace.ApplyLayout(args, WorldState.PlayerEntityId);
        KartPredictor = KartRace.Stats is { } stats && KartRace.Surface is { } surface ? new KartPredictor(stats, surface) : null;
        KartPredictorInitialized = false;
    }

    private void HandleKartSnapshot(KartSnapshotArgs args)
    {
        var now = TumbleClock.Now;
        var localFinish = KartRace.LocalIndex is { } previousLocalIndex
                          && !KartRace.LocalFinished
                          && args.Racers.FirstOrDefault(racer => racer.Index == previousLocalIndex)?.Status is KartRacerStatus.Finished or KartRacerStatus.Dnf;

        if (localFinish && KartPredictor is not null && KartPredictorInitialized)
        {
            var (x, y) = KartPredictor.DrawPosition;
            KartFinishHandover.Capture(KartPredictor.Body with { X = x, Y = y }, now);
        }

        KartRace.ApplySnapshot(args);
        KartInterpolator.Add(args.ServerTick, args.Racers);
        KartClock.OnSnapshot(args.ServerTick, now);

        if (KartPredictor is null || KartRace.LocalIndex is not { } localIndex)
            return;

        var local = args.Racers.FirstOrDefault(racer => racer.Index == localIndex);

        if (local is null)
            return;

        var reset = !KartPredictorInitialized
                    || local.Flags.HasFlag(KartRacerFlags.Held)
                    || local.Status is KartRacerStatus.Respawning or KartRacerStatus.Finished or KartRacerStatus.Dnf;

        if (reset)
            KartPredictor.Reset(KartRaceState.ToBody(local), KartRaceState.ToMods(local));
        else
            KartPredictor.Correct(args.AckSequence, KartRaceState.ToBody(local), KartRaceState.ToMods(local));

        KartPredictorInitialized = true;
    }

    private void HandleKartEvent(KartRaceEventArgs args)
    {
        KartRace.ApplyEvent(args);

        if (KartRaceState.BannerFor(args, KartRace.LocalIndex, KartRace.Laps) is { } banner)
        {
            KartBannerText = banner;
            KartBannerUntil = TumbleClock.Now + KartRaceState.BannerSeconds(banner);
        }

        if (args.Kind == KartRaceEventKind.Results)
            ShowKartResults();
    }

    private void ShowKartResults()
    {
        if (Root is null)
            return;

        if (KartResults is null)
        {
            KartResults = new KartResultsControl
            {
                ZIndex = 2
            };

            Root.AddChild(KartResults);
        }

        KartResults.Show(KartResultsView.Lines(KartRace.Results, KartRace.Racers), KartRace.Laps);
    }

    private void TickKartRace(double elapsedSeconds, double now)
    {
        KartPredictor?.Decay(elapsedSeconds);

        if (!KartRefusesStep() || KartRace.LocalIndex is not { } localIndex)
        {
            KartAccumulator = 0;
            return;
        }

        var estimate = KartClock.Estimate(now);
        var stage = KartRace.StageAt(estimate);
        var localFinished = KartRace.LocalFinished;

        var live = stage is KartRaceStage.Countdown or KartRaceStage.Racing || (stage == KartRaceStage.Finishing && !localFinished);

        if (!live || localFinished || KartPredictor is null)
        {
            KartAccumulator = 0;
            return;
        }

        const double stepSeconds = 1 / 30.0;
        KartAccumulator = Math.Min(KartAccumulator + elapsedSeconds, 4 * stepSeconds);
        var steps = Math.Min(4, (int)Math.Floor((KartAccumulator + 1e-9) / stepSeconds));

        for (var step = 0; step < steps; step++)
        {
            var input = KartSampler.Sample(
                KartClock.Estimate(now),
                KeyBindings.Current,
                (key, modifiers) => KartInputSampler.IsChordHeld(key, modifiers, InputBuffer.IsScancodeHeld, InputBuffer.CurrentModifiers),
                InputDispatcher.Instance?.ExplicitFocus is UITextBox);
            Game.Connection.SendKartInput(new KartInputArgs { Sequence = input.Sequence, ClientTick = input.ClientTick, Flags = input.Flags });
            KartPredictor.Step(input);
            KartAccumulator -= stepSeconds;
        }
    }

    private void ApplyKartRide(WorldEntity entity, double now)
    {
        if (!KartRace.Active || KartRace.IndexOfEntity(entity.Id) is not { } index)
            return;

        var position = KartBodyFor(index, now) is { } body ? (body.X, body.Y) : ((double X, double Y)?)null;

        if (position is not { } kartPosition)
            return;

        var offset = KartFrames.RideOffset(kartPosition.X, kartPosition.Y);
        var tileChanged = entity.TileX != offset.TileX || entity.TileY != offset.TileY;
        entity.TileX = offset.TileX;
        entity.TileY = offset.TileY;
        entity.RideOffset = new Vector2(offset.OffsetX, offset.OffsetY);

        if (tileChanged)
            WorldState.MarkSortDirty();
    }

    private KartBody? KartBodyFor(byte index, double now)
    {
        if (index != KartRace.LocalIndex)
            return KartInterpolator.Sample(index, KartClock.EstimateExact(now));

        if (!KartRace.LocalFinished)
        {
            if (KartPredictor is null || !KartPredictorInitialized)
                return KartInterpolator.Sample(index, KartClock.EstimateExact(now));

            var (x, y) = KartPredictor.DrawPosition;

            return KartPredictor.Body with { X = x, Y = y };
        }

        var sample = KartInterpolator.Sample(index, KartClock.EstimateExact(now));

        return KartFinishHandover.BodyFor(sample, now);
    }

    private byte? KartFollowedIndex
        => KartRace.Spectating ? KartSpectator.Resolve(KartRace.SpectateCandidates(), KartRace.LocalIndex) : null;

    private bool KartHidesEntity(WorldEntity entity)
        => KartRace.Active
           && KartRace.IndexOfEntity(entity.Id) is { } index
           && KartRace.Latest(index)?.Status != KartRacerStatus.Dnf;

    private bool PrepareKartDraws(double now)
    {
        KartDraws.Clear();
        KartFollowedDrawIndex = KartFollowedIndex;

        if (!KartRace.Active || MapFile is null)
            return false;

        foreach (var index in KartRace.DrawableIndexes())
        {
            if (KartBodyFor(index, now) is not { } body)
                continue;

            var ride = KartFrames.RideOffset(body.X, body.Y);
            var tileWorld = Camera.TileToWorld(ride.TileX, ride.TileY, MapFile.Height);
            var center = Camera.WorldToScreen(
                tileWorld
                + new Vector2(DaLibConstants.HALF_TILE_WIDTH, DaLibConstants.HALF_TILE_HEIGHT)
                + new Vector2(ride.OffsetX, ride.OffsetY));

            KartDraws.Add(new KartDraw(ride.TileX + ride.TileY, index, center, KartFrames.FrameFor(body.Heading)));
        }

        return KartDraws.Count > 0;
    }

    private void DrawKartsAtDepth(SpriteBatch spriteBatch, int depth)
    {
        foreach (var draw in KartDraws)
        {
            if (draw.Depth != depth)
                continue;

            var hasInfo = KartRace.Racers.TryGetValue(draw.Index, out var info);
            var texture = KartFrames.Texture(Device, draw.Frame);
            var tint = hasInfo ? KartDyes[info!.Dye % KartDyes.Length] : Color.White;
            spriteBatch.Draw(texture, draw.Center - new Vector2(texture.Width / 2f, texture.Height / 2f), tint);

            if (draw.Index == KartRace.LocalIndex)
                continue;

            var name = hasInfo ? info!.Name : $"Racer {draw.Index + 1}";
            var width = TextRenderer.MeasureWidth(name);
            var tagPosition = draw.Center - new Vector2(width / 2f, texture.Height / 2f + TextRenderer.CHAR_HEIGHT + 2);
            var tagColor = draw.Index == KartFollowedDrawIndex ? LegendColors.Gold : Color.White;
            TextRenderer.DrawText(spriteBatch, tagPosition, name, tagColor, false);
        }
    }

    private bool TryKartCameraTarget(out Vector2 position)
    {
        position = default;

        if (MapFile is null || !KartRace.Spectating)
            return false;

        if (KartFollowedIndex is { } index && KartBodyFor(index, TumbleClock.Now) is { } body)
        {
            var ride = KartFrames.RideOffset(body.X, body.Y);
            KartSpectatorCameraTarget = Camera.TileToWorld(ride.TileX, ride.TileY, MapFile.Height) + new Vector2(ride.OffsetX, ride.OffsetY);
        }

        if (KartSpectatorCameraTarget is not { } target)
            return false;

        position = target;

        return true;
    }

    private bool HandleKartSpectateKey(KeyChord chord, bool isRepeat)
    {
        if (!KartRace.Spectating || InputDispatcher.Instance?.ExplicitFocus is UITextBox)
            return false;

        var direction = 0;

        foreach (var action in KeyBindings.Current.Candidates(chord))
        {
            if (action == GameAction.KartSteerLeft)
                direction = -1;
            else if (action == GameAction.KartSteerRight)
                direction = 1;

            if (direction != 0)
                break;
        }

        if (direction == 0)
            return false;

        if (isRepeat)
            return true;

        KartSpectator.Cycle(KartRace.SpectateCandidates(), KartRace.LocalIndex, direction);

        return true;
    }

    private void DrawKartPads(SpriteBatch spriteBatch)
    {
        if (!KartRace.Active || KartRace.Surface is not { } surface || MapFile is null)
            return;

        var bounds = Camera.GetVisibleTileBounds(MapFile.Width, MapFile.Height);
        var tileTexture = KartPadArt.TileTexture(Device);

        foreach (var pad in surface.Pads)
            foreach (var (tileX, tileY) in KartPadArt.PadTiles(pad))
            {
                if ((tileX < bounds.MinX) || (tileX > bounds.MaxX) || (tileY < bounds.MinY) || (tileY > bounds.MaxY))
                    continue;

                var tileWorld = Camera.TileToWorld(tileX, tileY, MapFile.Height);
                var screenPos = Camera.WorldToScreen(tileWorld);

                if (((screenPos.X + 56) <= 0) || (screenPos.X >= Camera.ViewportWidth)
                    || ((screenPos.Y + 28) <= 0) || (screenPos.Y >= Camera.ViewportHeight))
                    continue;

                spriteBatch.Draw(tileTexture, screenPos, Color.White);
                var frame = KartPadArt.FrameForHeading(pad.Heading);
                var chevron = KartPadArt.ChevronTexture(Device, frame);
                var center = screenPos + new Vector2(28, 14);
                spriteBatch.Draw(chevron, center - new Vector2(chevron.Width / 2f, chevron.Height / 2f), Color.White);
            }
    }

    private bool KartRefusesStep()
        => KartRace.Active && KartRace.LocalIndex is not null;

    private void ResetKartRace()
    {
        KartRace.Clear();
        KartInterpolator.Clear();
        KartSpectator.Clear();
        KartDraws.Clear();
        KartFollowedDrawIndex = null;
        KartSpectatorCameraTarget = null;
        KartFinishHandover.Reset();
        KartPredictor = null;
        KartPredictorInitialized = false;
        KartClock.Reset();
        KartSampler.Reset();
        KartAccumulator = 0;
        KartBannerText = null;
        KartBannerUntil = 0;
    }

    private void DrawKartHud(BatchBlendScope scope)
    {
        if (!KartRace.Active)
            return;

        var now = TumbleClock.Now;
        var estimate = KartClock.Estimate(now);
        var stage = KartRace.StageAt(estimate);
        var sparkColor = Color.White;
        var lines = KartRace.Spectating ? KartSpectatorLines(estimate, stage) : KartRacerLines(estimate, stage, ref sparkColor);
        var y = 10f;

        foreach (var line in lines)
        {
            var color = line.StartsWith("Drift ", StringComparison.Ordinal) ? sparkColor : Color.White;
            TextRenderer.DrawText(scope.Batch, new Vector2(10, y), line, color, false);
            y += TextRenderer.CHAR_HEIGHT;
        }

        if (!KartRace.Spectating && stage is KartRaceStage.Racing or KartRaceStage.Finishing)
            DrawKartNeighbours(scope);

        DrawKartBanner(scope, estimate, stage, now);
    }

    private List<string> KartRacerLines(uint estimate, KartRaceStage stage, ref Color sparkColor)
    {
        var lines = new List<string>();

        if (KartRace.LocalIndex is not { } index)
            return lines;

        var latest = KartRace.Latest(index);

        if (latest is { Place: > 0 })
            lines.Add(KartRaceState.PlaceText(latest.Place, KartRace.Racers.Count));

        if (latest is not null)
            lines.Add($"Lap {Math.Min(latest.LapsDone + 1, KartRace.Laps)}/{KartRace.Laps}");

        if (stage is KartRaceStage.Racing or KartRaceStage.Finishing or KartRaceStage.Results)
            lines.Add(FormatRaceTime((estimate - KartRace.GoStep) / 30.0));

        if (KartRace.Finish(index) is { } finishTime)
            lines.Add($"Finished {FormatRaceTime(finishTime.TotalMs / 1000.0)}");
        else if (latest?.Status == KartRacerStatus.Respawning && latest.Flags.HasFlag(KartRacerFlags.Held))
            lines.Add("Respawning");

        if (KartPredictor is not null && KartRace.Stats is { } stats)
        {
            var spark = DriftModel.SparkFor(KartPredictor.Mods.DriftCharge, stats);
            sparkColor = spark switch { SparkLevel.Blue => Color.Blue, SparkLevel.Gold => Color.Yellow, SparkLevel.Violet => Color.Violet, _ => Color.White };

            if (spark != SparkLevel.None)
                lines.Add($"Drift {spark}");
        }

        var lapTimes = KartRace.LapTimes(index);

        if (lapTimes.Count > 0)
            lines.Add($"Best {FormatRaceTime(lapTimes.Min() / 1000.0)}");

        return lines;
    }

    private List<string> KartSpectatorLines(uint estimate, KartRaceStage stage)
    {
        var lines = new List<string>();

        if (KartFollowedIndex is { } followed)
        {
            var place = KartRace.Latest(followed)?.Place ?? 0;
            var watching = $"Watching {KartRacerName(followed)}";
            lines.Add(place > 0 ? $"{watching} - {KartTime.Ordinal(place)}" : watching);
            lines.Add("Left/Right: switch kart");
        }

        if (stage is KartRaceStage.Racing or KartRaceStage.Finishing or KartRaceStage.Results)
            lines.Add(FormatRaceTime((estimate - KartRace.GoStep) / 30.0));

        if (KartRace.LocalIndex is { } own && KartRace.Finish(own) is { } finishTime)
        {
            var ownPlace = KartRace.Latest(own)?.Place ?? 0;
            var placeText = ownPlace > 0 ? $"{KartTime.Ordinal(ownPlace)} " : string.Empty;
            lines.Add($"You finished {placeText}in {FormatRaceTime(finishTime.TotalMs / 1000.0)}");
        }

        return lines;
    }

    private void DrawKartNeighbours(BatchBlendScope scope)
    {
        if (KartRace.LocalIndex is not { } index || KartRace.Latest(index) is not { } mine)
            return;

        var (ahead, behind) = KartRace.Neighbours(index);
        var y = 10f;

        foreach (var other in new[] { ahead, behind })
        {
            if (other is null)
                continue;

            var text = $"{KartTime.Ordinal(other.Place)} {KartRacerName(other.Index)} {KartRaceState.GapText(mine.GapCs, other.GapCs)}";
            var width = TextRenderer.MeasureWidth(text);
            TextRenderer.DrawText(scope.Batch, new Vector2(Camera.ViewportWidth - 10 - width, y), text, Color.White, false);
            y += TextRenderer.CHAR_HEIGHT;
        }
    }

    private void DrawKartBanner(BatchBlendScope scope, uint estimate, KartRaceStage stage, double now)
    {
        string? text = null;
        var color = Color.White;

        if (stage == KartRaceStage.Countdown && estimate >= KartRace.StartStep)
        {
            var elapsed = estimate - KartRace.StartStep;
            text = elapsed < 30 ? "3" : elapsed < 60 ? "2" : "1";
        }
        else if (stage == KartRaceStage.Racing && estimate < KartRace.GoStep + 30)
            text = "GO!";
        else if (KartBannerText is not null && now < KartBannerUntil)
        {
            text = KartBannerText;
            color = LegendColors.Gold;
        }

        if (text is null)
            return;

        const int scale = 2;
        var width = TextRenderer.MeasureWidth(text, scale);
        var position = new Vector2((Camera.ViewportWidth - width) / 2f, Camera.ViewportHeight / 3f);
        TextRenderer.DrawText(scope.Batch, position, text, color, false, 1f, false, scale);
    }

    private string KartRacerName(byte index)
        => KartRace.Racers.TryGetValue(index, out var info) ? info.Name : $"Racer {index + 1}";

    private static string FormatRaceTime(double seconds)
    {
        var totalHundredths = (long)Math.Floor(seconds * 100);
        var minutes = totalHundredths / 6000;
        var remainderHundredths = totalHundredths % 6000;
        var wholeSeconds = remainderHundredths / 100;
        var hundredths = remainderHundredths % 100;

        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2:00}", minutes, wholeSeconds, hundredths);
    }
}
