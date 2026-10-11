#region
using Chaos.Client.Collections;
using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Drowned Causeway: rafts and shells drawn over the river from the shared schedule, the local player carried by
///     their raft at the same step the server carries them, and every rider drawn drifting with their raft.
/// </summary>
public sealed partial class WorldScreen
{
    private const long CAUSEWAY_LOST_ACK_MS = 1500;

    /// <summary>World pixels for one tile along +x on the isometric grid.</summary>
    private static readonly Vector2 CausewayTileStep = new(DaLibConstants.HALF_TILE_WIDTH, DaLibConstants.HALF_TILE_HEIGHT);

    private readonly Dictionary<int, long> CausewayAppliedSteps = [];
    private CausewayFrames? CausewayArt;

    private static CausewayState Causeway => WorldState.Causeway;

    private void WireCauseway() => Game.Connection.OnDrownedCausewayState += HandleDrownedCausewayState;

    private void UnwireCauseway()
    {
        Game.Connection.OnDrownedCausewayState -= HandleDrownedCausewayState;
        CausewayArt?.Dispose();
        CausewayArt = null;
    }

    private void HandleDrownedCausewayState(DrownedCausewayStateArgs args)
    {
        if (args.Kind == DrownedCausewayMessageKind.Position)
        {
            //sent while the server thinks we stand still; a step it has not received yet would be snapped back.
            //a walk the server refused never gets an ack, so acks still owed this long after the last send are lost
            if ((InFlightWalkAcks > 0) && (Environment.TickCount64 - LastWalkSentMs > CAUSEWAY_LOST_ACK_MS))
                InFlightWalkAcks = 0;

            if (InFlightWalkAcks == 0)
                HandleLocationChanged(args.X, args.Y);

            return;
        }

        Causeway.Apply(args, TumbleClock.Now, (LatencyMonitor.LatencyMs ?? 0) / 1000.0);

        if (args.Kind != DrownedCausewayMessageKind.State)
            CausewayAppliedSteps.Clear();
    }

    private void ResetCauseway()
    {
        Causeway.Clear();
        CausewayAppliedSteps.Clear();
    }

    /// <summary>
    ///     Carries the local player one tile when the raft under them steps. The server makes the same move at the same
    ///     schedule step and sends this client nothing for it.
    /// </summary>
    private void UpdateCausewayRides(double now)
    {
        //the server carries only players in the match, so a host watching from a raft stays put
        if (!Causeway.IsActive || Causeway.ViewerSide is null || (WorldState.GetPlayerEntity() is not { } player))
            return;

        var playMs = Causeway.PlayMs(now);

        foreach (var lane in Causeway.RiverLanes)
        {
            var steps = CausewaySchedule.Steps(lane, playMs);

            if (!CausewayAppliedSteps.TryGetValue(lane.Row, out var applied))
            {
                CausewayAppliedSteps[lane.Row] = steps;

                continue;
            }

            if (steps <= applied)
                continue;

            CausewayAppliedSteps[lane.Row] = steps;

            if (player.TileY != lane.Row)
                continue;

            //null means the raft carried them into the wall; the server sends them home with a Location
            if (CausewaySchedule.Ride(lane, player.TileX, applied, steps) is not { } x || (x == player.TileX))
                continue;

            player.TileX = x;
            WorldState.MarkSortDirty();
            Causeway.Rides.Shifted(player.Id, player.TileX, player.TileY, lane, playMs);
            UpdateHuds(HudOps.SetCoords, player.TileX, player.TileY);
        }
    }

    private void ApplyCausewayRide(WorldEntity entity, double now)
    {
        if (!Causeway.IsActive || (entity.Type is not (ClientEntityType.Aisling or ClientEntityType.GroundItem)))
        {
            if (entity.RideOffset != Vector2.Zero)
                entity.RideOffset = Vector2.Zero;

            return;
        }

        var drift = Causeway.Rides.Offset(
            entity.Id,
            entity.TileX,
            entity.TileY,
            WalkProgress(entity),
            Causeway.RiverLaneAt(entity.TileY),
            Causeway.ShellTiming,
            Causeway.PlayMs(now));

        entity.RideOffset = CausewayTileStep * (float)drift;
    }

    private static double WalkProgress(WorldEntity entity)
    {
        if ((entity.AnimState != EntityAnimState.Walking) || (entity.WalkStartOffset == Vector2.Zero))
            return 1;

        return Math.Clamp(1 - entity.WalkOffset.Length() / entity.WalkStartOffset.Length(), 0, 1);
    }

    /// <summary>True when the local player may not start a walk to (x, y) on the Causeway.</summary>
    private bool CausewayRefusesStep(int x, int y) => Causeway.IsActive && (Causeway.IsMovementLocked(TumbleClock.Now) || Causeway.BlocksStep(x, y));

    /// <summary>A raft or risen shell under an entity on a water tile: it stands, it does not swim.</summary>
    private bool CausewayFloats(WorldEntity entity)
        => Causeway.IsActive && Causeway.IsFloating(entity.TileX, entity.TileY, Causeway.PlayMs(TumbleClock.Now));

    /// <summary>Draws rafts and shells over the water rows, sliding between tiles, after the background tiles.</summary>
    private void DrawCausewayRafts(SpriteBatch batch)
    {
        if (!Causeway.IsActive || MapFile is null)
            return;

        CausewayArt ??= new CausewayFrames(Device);
        var playMs = Causeway.PlayMs(TumbleClock.Now);

        foreach (var lane in Causeway.RiverLanes)
            foreach (var piece in lane.Pieces)
            {
                var start = CausewaySchedule.VisualStart(lane, piece, playMs);

                var state = lane.Kind == CausewayLaneKind.Shell
                    ? CausewaySchedule.ShellState(Causeway.ShellTiming, piece, playMs)
                    : CausewayShellState.Risen;

                for (var k = 0; k < piece.Length; k++)
                {
                    var position = WrapLanePosition(start + k, lane.RingLength);
                    var alpha = EdgeAlpha(position);

                    if (alpha <= 0)
                        continue;

                    var whole = (int)Math.Floor(position);

                    var world = Camera.TileToWorld(CausewaySchedule.ToMapX(whole), lane.Row, MapFile.Height)
                                + CausewayTileStep * (float)(position - whole);

                    batch.Draw(CausewayArt.For(lane.Kind, state, k, piece.Length, playMs), Camera.WorldToScreen(world), Color.White * alpha);
                }
            }
    }

    /// <summary>Wraps into [-1, ring - 1) so a tile just leaving the low-x wall reads as slightly below 0.</summary>
    private static double WrapLanePosition(double position, int ring)
    {
        var m = position % ring;

        if (m < 0)
            m += ring;

        return m >= ring - 1 ? m - ring : m;
    }

    /// <summary>Fades tiles sliding behind the side walls.</summary>
    private static float EdgeAlpha(double position)
        => position switch
        {
            < 0                                   => (float)(1 + position),
            > CausewayLayout.LANE_WIDTH - 1        => (float)(CausewayLayout.LANE_WIDTH - position),
            _                                     => 1f
        };
}
