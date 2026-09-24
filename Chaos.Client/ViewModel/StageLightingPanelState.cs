#region
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The Stage Lighting window's own state: the scene names, the selected light, a pending follow pick, edit
///     throttling while dragging, and where the window sits. The lighting setup itself lives in
///     <c>WorldState.StageLights</c>.
/// </summary>
public sealed class StageLightingPanelState
{
    public const int MAX_LIGHTS = 8;

    /// <summary>At most 8 edits a second while dragging.</summary>
    public const int SEND_INTERVAL_MS = 125;

    /// <summary>How long <see cref="ExpectNewLight" /> waits for the added light to show up in a reconcile.</summary>
    public const int NEW_LIGHT_WAIT_MS = 3000;

    /// <summary>The window's 13 colour swatches (the 14th swatch opens the custom picker).</summary>
    public static readonly Color[] Swatches =
    [
        new(255, 255, 255),
        new(255, 210, 122),
        new(255, 170, 40),
        new(255, 75, 60),
        new(255, 60, 200),
        new(160, 80, 255),
        new(60, 100, 255),
        new(60, 220, 255),
        new(60, 255, 140),
        new(180, 255, 60),
        new(255, 255, 60),
        new(140, 60, 40),
        new(30, 30, 60)
    ];

    private HashSet<byte> KnownIds = [];
    private long? LastSendMs;
    private StageLightingInteractionArgs? Pending;
    private long? SelectNewestUntil;

    public IReadOnlyList<string> Scenes { get; private set; } = [];
    public byte? SelectedLightId { get; private set; }

    /// <summary>True after the director picks Follow and before they click a person on the stage view.</summary>
    public bool AwaitingFollowPick { get; private set; }

    /// <summary>Whether the window is shrunk to its bar. Kept until logout.</summary>
    public bool IsMinimized { get; set; }

    /// <summary>Where the director dragged the window. Kept until logout.</summary>
    public Point? Position { get; set; }

    public void SetScenes(IReadOnlyList<string> names) => Scenes = names;

    public void Select(byte? id)
    {
        SelectedLightId = id;
        AwaitingFollowPick = false;
    }

    /// <summary>Reconciles within <see cref="NEW_LIGHT_WAIT_MS" /> select whichever light is new; after that the expectation lapses.</summary>
    public void ExpectNewLight(long nowMs) => SelectNewestUntil = nowMs + NEW_LIGHT_WAIT_MS;

    /// <summary>Keeps the selection valid after a new setup: a new light when one was expected, else the current one, else the first.</summary>
    public void Reconcile(IReadOnlyList<StageLightInfo> lights, long nowMs)
    {
        if (SelectNewestUntil is { } until)
        {
            if (nowMs <= until)
            {
                if (lights.FirstOrDefault(l => !KnownIds.Contains(l.Id)) is { } added)
                {
                    SelectNewestUntil = null;
                    Select(added.Id);
                }
            }
            else
                SelectNewestUntil = null;
        }

        KnownIds = lights.Select(l => l.Id).ToHashSet();

        if (SelectedLightId is { } id && KnownIds.Contains(id))
            return;

        Select(lights.Count > 0 ? lights[0].Id : null);
    }

    public void BeginFollowPick() => AwaitingFollowPick = SelectedLightId is not null;

    public void EndFollowPick() => AwaitingFollowPick = false;

    /// <summary>
    ///     Rate-limits a stream of edits (drags, sliders, the colour picker). Returns the edit to send now, or null to
    ///     hold it; the held edit goes out on the next allowed call or on <see cref="Flush" />.
    /// </summary>
    public StageLightingInteractionArgs? Throttle(StageLightingInteractionArgs edit, long nowMs)
    {
        if (LastSendMs is { } last && ((nowMs - last) < SEND_INTERVAL_MS))
        {
            Pending = edit;

            return null;
        }

        LastSendMs = nowMs;
        Pending = null;

        return edit;
    }

    /// <summary>The held edit when a drag ends, or null when nothing is waiting.</summary>
    public StageLightingInteractionArgs? Flush(long nowMs)
    {
        var pending = Pending;
        Pending = null;

        if (pending is not null)
            LastSendMs = nowMs;

        return pending;
    }

    /// <summary>
    ///     The held edit once <see cref="SEND_INTERVAL_MS" /> has passed since the last send, or null. Polled every frame
    ///     so a drag that pauses still sends its latest value without waiting for the next move or the release.
    /// </summary>
    public StageLightingInteractionArgs? TakeDue(long nowMs)
    {
        if (Pending is null || (LastSendMs is { } last && ((nowMs - last) < SEND_INTERVAL_MS)))
            return null;

        var due = Pending;
        Pending = null;
        LastSendMs = nowMs;

        return due;
    }

    /// <summary>Called on a real map change: the scene list and selection belong to the Theatre visit.</summary>
    public void ResetForNewMap()
    {
        Scenes = [];
        Select(null);
        KnownIds = [];
        SelectNewestUntil = null;
        Pending = null;
        LastSendMs = null;
    }

    /// <summary>Called on logout: also forgets where the window sat.</summary>
    public void Reset()
    {
        ResetForNewMap();
        IsMinimized = false;
        Position = null;
    }

    public static StageLightInfo NewLight(Rectangle stage)
    {
        var (x, y) = StageViewGeometry.ToUnits(StageViewGeometry.Centre(stage), stage);

        return new StageLightInfo
        {
            X = x,
            Y = y,
            X2 = x,
            Y2 = y
        };
    }

    /// <summary>
    ///     The light with a new motion. Switching into Sweep or Circle puts the second point 2 tiles along +X (or −X when
    ///     that is off the stage). Any motion but Follow drops the follow target.
    /// </summary>
    public static StageLightInfo WithMotion(StageLightInfo light, StageLightMotion motion, Rectangle stage)
    {
        var wasMoving = light.Motion is StageLightMotion.Sweep or StageLightMotion.Circle;
        var isMoving = motion is StageLightMotion.Sweep or StageLightMotion.Circle;

        if (isMoving && !wasMoving)
        {
            var here = StageLightAnimator.ToTile(light.X, light.Y);
            var (x2, y2) = StageViewGeometry.ToUnits(here + new Vector2(2, 0), stage);

            if ((x2 == light.X) && (y2 == light.Y))
                (x2, y2) = StageViewGeometry.ToUnits(here - new Vector2(2, 0), stage);

            light = light with { X2 = x2, Y2 = y2 };
        }

        return light with
        {
            Motion = motion,
            FollowId = motion == StageLightMotion.Follow ? light.FollowId : 0
        };
    }

    /// <summary>The enum value <paramref name="direction" /> steps away, wrapping at both ends.</summary>
    public static T Step<T>(T value, int direction) where T: struct, Enum
    {
        var values = Enum.GetValues<T>();
        var index = Array.IndexOf(values, value);

        return values[(((index + direction) % values.Length) + values.Length) % values.Length];
    }
}
