using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel;

public enum GuildHallTool
{
    Paint,
    Rect,
    Fill,
    Erase,
    Npc,
    Entrance
}

public enum GuildHallTab
{
    Floors,
    Walls,
    Decor,
    Npcs,
    Prefabs
}

/// <summary>One picker cell: a floor (Floor != 0), a foreground pair (Left/Right), or an NPC (NpcKey).</summary>
public readonly record struct GuildHallPick(ushort Floor, ushort Left, ushort Right, string? NpcKey, string? PrefabKey = null);

/// <summary>The guild hall editor's sets, NPCs, entrance, tool and selection. No graphics, so it is testable.</summary>
public sealed class GuildHallEditorState
{
    public const int COLUMNS = 5;
    public const int ROWS = 4;
    public const int PAGE_SIZE = COLUMNS * ROWS;

    private static readonly Direction[] Facings = [Direction.Down, Direction.Left, Direction.Up, Direction.Right];

    private int FacingIndex;

    public bool IsOpen { get; private set; }
    public IReadOnlyList<GuildHallPaletteSet> Sets { get; private set; } = [];
    public IReadOnlyList<GuildHallNpcOption> Npcs { get; private set; } = [];
    public Point Entrance { get; private set; }
    public GuildHallTool Tool { get; private set; } = GuildHallTool.Paint;
    public GuildHallTab Tab { get; private set; } = GuildHallTab.Floors;
    public GuildHallSide Side { get; private set; } = GuildHallSide.Both;
    public int SetIndex { get; private set; }
    public int Page { get; private set; }
    public ushort? SelectedFloor { get; private set; }
    public GuildHallForegroundInfo? SelectedForeground { get; private set; }
    public string? SelectedNpcKey { get; private set; }
    public string? LastRefusedReason { get; private set; }
    public DateTime RefusedAtUtc { get; private set; }
    public static readonly TimeSpan RefusalLifetime = TimeSpan.FromSeconds(5);

    /// <summary>Whether the editor owns the in-progress left press; its release click is swallowed, then cleared.</summary>
    public bool PressOwned { get; private set; }

    public Direction NpcFacing => Facings[FacingIndex];

    public GuildHallLayer Layer => Tab == GuildHallTab.Floors ? GuildHallLayer.Floor : GuildHallLayer.Foreground;

    public GuildHallPaletteSet? CurrentSet => (SetIndex >= 0) && (SetIndex < Sets.Count) ? Sets[SetIndex] : null;

    public bool CurrentSetLocked => CurrentSet is { Owned: false };

    public IReadOnlyList<GuildHallPick> Items
        => Tab switch
        {
            GuildHallTab.Floors => CurrentSet?.Floors.Select(f => new GuildHallPick(f, 0, 0, null)).ToList() ?? [],
            GuildHallTab.Walls  => CurrentSet?.Walls.Select(w => new GuildHallPick(0, w.Left, w.Right, null)).ToList() ?? [],
            GuildHallTab.Decor  => CurrentSet?.Decor.Select(d => new GuildHallPick(0, d.Left, d.Right, null)).ToList() ?? [],
            GuildHallTab.Prefabs => CurrentSet?.Prefabs.Select(p => new GuildHallPick(0, 0, 0, null, p.Key)).ToList() ?? [],
            _                   => Npcs.Select(n => new GuildHallPick(0, 0, 0, n.Key)).ToList()
        };

    public int PageCount => Math.Max(1, (Items.Count + PAGE_SIZE - 1) / PAGE_SIZE);

    public IReadOnlyList<GuildHallPick> PageItems => Items.Skip(Page * PAGE_SIZE).Take(PAGE_SIZE).ToList();

    public GuildHallPick? Selected
        => Tab switch
        {
            GuildHallTab.Floors => SelectedFloor is { } floor ? new GuildHallPick(floor, 0, 0, null) : null,
            GuildHallTab.Npcs   => SelectedNpcKey is { } key ? new GuildHallPick(0, 0, 0, key) : null,
            GuildHallTab.Prefabs => SelectedPrefab is { } prefab ? new GuildHallPick(0, 0, 0, null, prefab.Key) : null,
            _                   => SelectedForeground is { } fg ? new GuildHallPick(0, fg.Left, fg.Right, null) : null
        };

    public GuildHallNpcOption? SelectedNpc => Npcs.FirstOrDefault(n => n.Key == SelectedNpcKey);
    public GuildHallPrefabInfo? SelectedPrefab { get; private set; }

    public bool PlacesPrefab => Tab == GuildHallTab.Prefabs && Tool == GuildHallTool.Paint;

    public IReadOnlyList<Point> PrefabFootprint(Point tile) =>
        SelectedPrefab is { } prefab
            ? GuildHallStroke.Perimeter(tile, new Point(tile.X + prefab.Width - 1, tile.Y + prefab.Height - 1)).ToList()
            : [];

    /// <summary>First Open fills everything; a later Open (after a move or a purchase) keeps the tab, tool and selection.</summary>
    public void Open(GuildHallEditorDisplayArgs args)
    {
        var wasOpen = IsOpen;
        var setKey = CurrentSet?.Key;

        Sets = args.Sets;
        Npcs = args.Npcs;
        Entrance = new Point(args.EntranceX, args.EntranceY);
        IsOpen = true;
        LastRefusedReason = null;

        var keptSet = setKey is null ? -1 : Sets.ToList().FindIndex(s => s.Key == setKey);
        SetIndex = keptSet >= 0 ? keptSet : 0;

        if (wasOpen)
        {
            Page = Math.Clamp(Page, 0, PageCount - 1);
            SelectedPrefab = SelectedPrefab is { } kept
                ? args.Sets.SelectMany(s => s.Prefabs).FirstOrDefault(p => p.Key == kept.Key)
                : null;

            return;
        }

        Tool = GuildHallTool.Paint;
        Tab = GuildHallTab.Floors;
        Side = GuildHallSide.Both;
        Page = 0;
        FacingIndex = 0;
        SelectedFloor = Sets.FirstOrDefault(s => s.Owned)?.Floors.FirstOrDefault() is { } floor and not 0 ? floor : null;
        SelectedForeground = null;
        SelectedNpcKey = null;
        LastRefusedReason = null;
    }

    public void SetNpcs(IReadOnlyList<GuildHallNpcOption> npcs)
    {
        Npcs = npcs;
        LastRefusedReason = null;

        if (Tab == GuildHallTab.Npcs)
            Page = Math.Clamp(Page, 0, PageCount - 1);
    }

    public void Refuse(string reason)
    {
        LastRefusedReason = reason;
        RefusedAtUtc = DateTime.UtcNow;
    }

    /// <summary>The refusal to show, if one was recorded in the last few seconds.</summary>
    public string? ActiveRefusal => ActiveRefusalAt(DateTime.UtcNow);

    internal string? ActiveRefusalAt(DateTime now)
        => LastRefusedReason is null || (now - RefusedAtUtc) >= RefusalLifetime ? null : LastRefusedReason;

    /// <summary>Clears the refusal, e.g. when the player acts again or the server acknowledges an edit.</summary>
    public void ClearRefusal() => LastRefusedReason = null;

    private bool EmptyHintShown;

    /// <summary>True the first time a stroke step has nothing selected; later steps in the same stroke stay quiet.</summary>
    public bool TakeEmptyHint()
    {
        if (EmptyHintShown)
            return false;

        EmptyHintShown = true;
        return true;
    }

    /// <summary>Starts a new stroke's empty-selection hint.</summary>
    public void ResetEmptyHint() => EmptyHintShown = false;

    /// <summary>Marks the current left press as the editor's. Only sticks while open.</summary>
    public void ClaimPress()
    {
        if (IsOpen)
            PressOwned = true;
    }

    /// <summary>Takes the owned press if there is one; true means the release click is the editor's and is swallowed.</summary>
    public bool ConsumePress()
    {
        var owned = PressOwned;
        PressOwned = false;

        return owned;
    }

    public void Close()
    {
        IsOpen = false;
        PressOwned = false;
        Sets = [];
        Npcs = [];
        Entrance = Point.Zero;
        Tool = GuildHallTool.Paint;
        Tab = GuildHallTab.Floors;
        Side = GuildHallSide.Both;
        SetIndex = 0;
        Page = 0;
        FacingIndex = 0;
        SelectedFloor = null;
        SelectedForeground = null;
        SelectedNpcKey = null;
        SelectedPrefab = null;
        LastRefusedReason = null;
    }

    public void SetTab(GuildHallTab tab)
    {
        Tab = tab;
        LastRefusedReason = null;
        Page = 0;

        if (tab == GuildHallTab.Prefabs)
            Tool = GuildHallTool.Paint;
        else if (tab == GuildHallTab.Npcs)
            Tool = GuildHallTool.Npc;
        else if (Tool == GuildHallTool.Npc)
            Tool = GuildHallTool.Paint;
    }

    public void SetTool(GuildHallTool tool)
    {
        if (Tab == GuildHallTab.Prefabs && tool is GuildHallTool.Rect or GuildHallTool.Fill or GuildHallTool.Erase)
            return;

        Tool = tool;
        LastRefusedReason = null;

        if ((tool == GuildHallTool.Npc) && (Tab != GuildHallTab.Npcs))
        {
            Tab = GuildHallTab.Npcs;
            Page = 0;
        } else if ((tool != GuildHallTool.Npc) && (tool != GuildHallTool.Entrance) && (Tab == GuildHallTab.Npcs))
        {
            Tab = GuildHallTab.Floors;
            Page = 0;
        }
    }

    public void SetSide(GuildHallSide side)
    {
        if (!Enum.IsDefined(side))
            return;

        Side = side;
        LastRefusedReason = null;
    }

    public void StepSet(int delta)
    {
        LastRefusedReason = null;

        if (Sets.Count == 0)
            return;

        SetIndex = (SetIndex + delta + Sets.Count) % Sets.Count;
        Page = 0;
    }

    public void StepPage(int delta) => Page = Math.Clamp(Page + delta, 0, PageCount - 1);

    public void TurnNpc() => FacingIndex = (FacingIndex + 1) % Facings.Length;

    /// <summary>False for a tile from a set the guild does not own; NPCs are never locked.</summary>
    public bool Select(GuildHallPick pick)
    {
        if (pick.NpcKey is { } key)
        {
            SelectedNpcKey = key;
            LastRefusedReason = null;

            return true;
        }

        if (CurrentSetLocked)
            return false;

        if (Tab == GuildHallTab.Prefabs)
        {
            SelectedPrefab = CurrentSet?.Prefabs.FirstOrDefault(p => p.Key == pick.PrefabKey);

            return SelectedPrefab is not null;
        }

        if (pick.Floor != 0)
            SelectedFloor = pick.Floor;
        else
            SelectedForeground = new GuildHallForegroundInfo(pick.Left, pick.Right);

        LastRefusedReason = null;

        return true;
    }

    /// <summary>The args for a one-tile action of the current tool at <paramref name="tile" />, or null when nothing fits.</summary>
    public GuildHallEditorArgs? ArgsAt(Point tile)
    {
        if (!IsOpen || (tile.X < 0) || (tile.X > byte.MaxValue) || (tile.Y < 0) || (tile.Y > byte.MaxValue))
            return null;

        if (PlacesPrefab)
            return SelectedPrefab is { } prefab
                ? new GuildHallEditorArgs
                {
                    Action = GuildHallEditorAction.Prefab,
                    NpcKey = prefab.Key,
                    X = (byte)tile.X,
                    Y = (byte)tile.Y,
                    Layer = GuildHallLayer.Foreground,
                    Side = GuildHallSide.Both
                }
                : null;

        var args = new GuildHallEditorArgs
        {
            Layer = Layer,
            Side = Layer == GuildHallLayer.Foreground ? Side : GuildHallSide.Both,
            X = (byte)tile.X,
            Y = (byte)tile.Y
        };

        switch (Tool)
        {
            case GuildHallTool.Erase:
                args.Action = GuildHallEditorAction.Erase;
                args.Layer = GuildHallLayer.Foreground;

                return args;

            case GuildHallTool.Entrance:
                args.Action = GuildHallEditorAction.MoveEntrance;

                return args;

            case GuildHallTool.Npc:
                if (SelectedNpcKey is null)
                    return null;

                args.Action = GuildHallEditorAction.PlaceNpc;
                args.NpcKey = SelectedNpcKey;
                args.Direction = NpcFacing;

                return args;

            case GuildHallTool.Paint:
            case GuildHallTool.Fill:
            case GuildHallTool.Rect:
                args.Action = Tool == GuildHallTool.Fill ? GuildHallEditorAction.Fill : GuildHallEditorAction.Paint;

                return FillTile(args) ? args : null;

            default:
                return null;
        }
    }

    /// <summary>One Rect from corner <paramref name="a" /> to corner <paramref name="b" />, corners ordered; null without a tile.</summary>
    public GuildHallEditorArgs? RectArgs(Point a, Point b)
    {
        (var min, var max) = GuildHallStroke.Normalize(a, b);

        if (ArgsAt(min) is not { Action: GuildHallEditorAction.Paint } args || (max.X > byte.MaxValue) || (max.Y > byte.MaxValue))
            return null;

        args.Action = GuildHallEditorAction.Rect;
        args.X2 = (byte)max.X;
        args.Y2 = (byte)max.Y;

        return args;
    }

    private bool FillTile(GuildHallEditorArgs args)
    {
        if (Layer == GuildHallLayer.Floor)
        {
            if (SelectedFloor is not { } floor)
                return false;

            args.Background = floor;

            return true;
        }

        if (SelectedForeground is not { } fg)
            return false;

        args.Left = fg.Left;
        args.Right = fg.Right;

        return true;
    }
}
