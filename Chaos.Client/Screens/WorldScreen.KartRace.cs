#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
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
    private readonly KartInputSampler KartSampler = new();
    private KartPredictor? KartPredictor;
    private double KartAccumulator;
    private bool KartPredictorInitialized;
    private static readonly Color[] KartDyes = [Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.Orange, Color.Purple, Color.Cyan, Color.White];
    private static KartRaceState KartRace => WorldState.KartRace;

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
        KartRace.ApplyLayout(args, WorldState.PlayerEntityId);
        KartPredictor = KartRace.Stats is { } stats && KartRace.Surface is { } surface ? new KartPredictor(stats, surface) : null;
        KartPredictorInitialized = false;
    }

    private void HandleKartSnapshot(KartSnapshotArgs args)
    {
        var now = TumbleClock.Now;
        KartRace.ApplySnapshot(args, now);
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

    private void HandleKartEvent(KartRaceEventArgs args) => KartRace.ApplyEvent(args);

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
        var localFinished = KartRace.Finish(localIndex) is not null
                            || KartRace.Latest(localIndex)?.Status is KartRacerStatus.Finished or KartRacerStatus.Dnf;

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

        var position = index == KartRace.LocalIndex && KartPredictor is not null
            ? KartPredictor.DrawPosition
            : KartRace.Interpolated(index, now) is { } body ? (body.X, body.Y) : ((double X, double Y)?)null;

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

    private bool KartDrawsEntity(SpriteBatch spriteBatch, WorldEntity entity, int tileX, int tileY, float yOffset)
    {
        if (!KartRace.Active || KartRace.IndexOfEntity(entity.Id) is not { } index || MapFile is null)
            return false;

        var info = KartRace.Racers[index];
        var body = index == KartRace.LocalIndex && KartPredictor is not null
            ? KartPredictor.Body
            : KartRace.Interpolated(index, TumbleClock.Now);

        if (body is null)
            return false;

        var tileWorld = Camera.TileToWorld(tileX, tileY, MapFile.Height);
        var center = tileWorld + new Vector2(DaLibConstants.HALF_TILE_WIDTH, DaLibConstants.HALF_TILE_HEIGHT + yOffset) + entity.RideOffset;
        var texture = KartFrames.Texture(Device, KartFrames.FrameFor(body.Value.Heading));
        var tint = KartDyes[info.Dye % KartDyes.Length];
        spriteBatch.Draw(texture, center - new Vector2(texture.Width / 2f, texture.Height / 2f), tint);

        return true;
    }

    private bool KartRefusesStep()
        => KartRace.Active && KartRace.LocalIndex is not null && KartRace.Stage != KartRaceStage.Results;

    private void ResetKartRace()
    {
        KartRace.Clear();
        KartPredictor = null;
        KartPredictorInitialized = false;
        KartClock.Reset();
        KartSampler.Reset();
        KartAccumulator = 0;
    }

    private void DrawKartHud(BatchBlendScope scope)
    {
        if (!KartRace.Active || KartRace.LocalIndex is not { } index)
            return;

        var now = TumbleClock.Now;
        var estimate = KartClock.Estimate(now);
        var latest = KartRace.Latest(index);
        var lines = new List<string>();
        var stage = KartRace.StageAt(estimate);

        if (stage == KartRaceStage.Countdown)
        {
            if (estimate >= KartRace.StartStep)
            {
                var elapsed = estimate - KartRace.StartStep;
                lines.Add(elapsed < 30 ? "3" : elapsed < 60 ? "2" : "1");
            }
        } else if (stage == KartRaceStage.Racing && estimate < KartRace.GoStep + 30)
            lines.Add("GO!");

        if (latest is not null)
            lines.Add($"Lap {Math.Min(latest.LapsDone + 1, KartRace.Laps)}/{KartRace.Laps}");

        if (stage is KartRaceStage.Racing or KartRaceStage.Finishing or KartRaceStage.Results)
            lines.Add(FormatRaceTime((estimate - KartRace.GoStep) / 30.0));

        var finish = KartRace.Finish(index);

        if (finish is { } finishTime)
            lines.Add($"Finished {FormatRaceTime(finishTime.TotalMs / 1000.0)}");
        else if (latest?.Status == KartRacerStatus.Respawning && latest.Flags.HasFlag(KartRacerFlags.Held))
            lines.Add("Respawning");

        var sparkColor = Color.White;

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

        var y = 10f;

        foreach (var line in lines)
        {
            var color = line.StartsWith("Drift ", StringComparison.Ordinal) ? sparkColor : Color.White;
            TextRenderer.DrawText(scope.Batch, new Vector2(10, y), line, color, false, 1f, false);
            y += TextRenderer.CHAR_HEIGHT;
        }
    }

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
