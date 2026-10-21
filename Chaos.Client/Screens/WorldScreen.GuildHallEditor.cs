#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.GuildHall;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The guild hall editor: server packets, live tile changes on the hall map, and (Task 12) the in-world tools.</summary>
public sealed partial class WorldScreen
{
    //built on first Open
    private GuildHallEditorControl? GuildHallEditorWindow;

    private static readonly Color HallRectOutline = LegendColors.Gold * 0.6f;
    private static readonly Color HallEntranceMark = new Color(80, 220, 120) * 0.5f;

    //a held Paint/Erase stroke or a Rect drag; ends when the left button is no longer held
    private bool HallStrokeActive;
    private GuildHallTool HallStrokeTool;
    private Point HallStrokeStart;
    private Point HallStrokeLast;

    private void WireGuildHallEditor() => Game.Connection.OnGuildHallEditorDisplay += HandleGuildHallEditorDisplay;

    private void UnwireGuildHallEditor()
    {
        Game.Connection.OnGuildHallEditorDisplay -= HandleGuildHallEditorDisplay;

        if (GuildHallEditorWindow is null)
            return;

        GuildHallEditorWindow.EditRequested -= SendGuildHallEditor;
        GuildHallEditorWindow.MessageRequested -= ShowGuildHallMessage;
    }

    private static void ShowGuildHallMessage(string message) => WorldState.Chat.AddOrangeBarMessage(message);

    private GuildHallEditorControl EnsureGuildHallEditorWindow()
    {
        if (GuildHallEditorWindow is not null)
            return GuildHallEditorWindow;

        GuildHallEditorWindow = new GuildHallEditorControl(Game.CreatureRenderer)
        {
            ZIndex = 2
        };

        GuildHallEditorWindow.EditRequested += SendGuildHallEditor;
        GuildHallEditorWindow.MessageRequested += ShowGuildHallMessage;
        Root!.AddChild(GuildHallEditorWindow);

        return GuildHallEditorWindow;
    }

    private void SendGuildHallEditor(GuildHallEditorArgs args) => Game.Connection.SendGuildHallEditor(args);

    private void HandleGuildHallEditorDisplay(GuildHallEditorDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        var state = WorldState.GuildHallEditor;

        switch (args.Kind)
        {
            case GuildHallEditorDisplayKind.Open:
                state.Open(args);

                if (GuildHallEditorWindow is { Visible: true } open)
                    open.RefreshFromState();
                else
                    EnsureGuildHallEditorWindow().Show(WorldHud.ViewportBounds);

                break;

            case GuildHallEditorDisplayKind.Close:
                ResetGuildHallEditor();

                break;

            case GuildHallEditorDisplayKind.Refused:
                state.Refuse(args.Reason);
                WorldState.Chat.AddOrangeBarMessage(args.Reason);
                GuildHallEditorWindow?.RefreshFromState();

                break;

            case GuildHallEditorDisplayKind.Tiles:
                ApplyGuildHallTiles(args.Tiles);

                break;

            case GuildHallEditorDisplayKind.Npcs:
                state.SetNpcs(args.Npcs);
                GuildHallEditorWindow?.RefreshFromState();

                break;
        }
    }

    /// <summary>Every aisling in the hall gets these, editor or not; only the hall map takes them.</summary>
    private void ApplyGuildHallTiles(IReadOnlyList<GuildHallTileInfo> tiles)
    {
        if (MapFile is null || (CurrentMapId != CONSTANTS.GUILD_HALL_MAP_ID))
            return;

        if (GuildHallTileApply.Apply(MapFile, tiles) == 0)
            return;

        TabMapRenderer.Generate(Device, MapFile);

        //still loading: FinalizeMapLoad builds the grid from the finished tiles
        if (!MapPreloaded)
            return;

        (MapPathfinder, MapWaterTiles, MapDoorTiles) = BuildPathfinder(MapFile);
    }

    /// <summary>Closes the editor. Called on a real map change and on the server's Close.</summary>
    private void ResetGuildHallEditor()
    {
        HallStrokeActive = false;
        WorldState.GuildHallEditor.Close();
        GuildHallEditorWindow?.Hide();
    }

    private bool GuildHallEditorActive => GuildHallEditorWindow is { Visible: true } && (MapFile is not null) && !CastingSystem.IsTargeting;

    /// <summary>The world tile under a screen point inside the viewport and off the panel, or null.</summary>
    private Point? HallTileAt(int screenX, int screenY)
    {
        if (MapFile is null || (GuildHallEditorWindow?.ContainsPoint(screenX, screenY) ?? false))
            return null;

        var viewport = WorldHud.ViewportBounds;

        if ((screenX < viewport.X) || (screenX >= viewport.Right) || (screenY < viewport.Y) || (screenY >= viewport.Bottom))
            return null;

        (var tileX, var tileY) = ScreenToTile(screenX, screenY);

        return (tileX < 0) || (tileX >= MapFile.Width) || (tileY < 0) || (tileY >= MapFile.Height) ? null : new Point(tileX, tileY);
    }

    /// <summary>HoveredTile from this frame's PopulateFrameState, minus the part of the viewport the panel covers.</summary>
    private Point? HoveredHallTile()
        => GuildHallEditorWindow?.ContainsPoint(InputBuffer.MouseX, InputBuffer.MouseY) ?? false ? null : WorldState.CurrentFrame.HoveredTile;

    private void SendHallTile(Point tile)
    {
        if (WorldState.GuildHallEditor.ArgsAt(tile) is { } args)
            SendGuildHallEditor(args);
        else
            WorldState.Chat.AddOrangeBarMessage("Pick a tile or an NPC in the hall editor first.");
    }

    /// <summary>Top of OnRootMouseDown. True when the press was the editor's.</summary>
    private bool TryGuildHallMouseDown(MouseDownEvent e)
    {
        if (!GuildHallEditorActive)
            return false;

        if (e.Button == MouseButton.Right)
        {
            if (!HallStrokeActive)
                return false;

            HallStrokeActive = false;
            e.Handled = true;

            return true;
        }

        if ((e.Button != MouseButton.Left) || e.Ctrl || e.Alt || (HallTileAt(e.ScreenX, e.ScreenY) is not { } tile))
            return false;

        e.Handled = true;
        WorldState.GuildHallEditor.ClaimPress();
        var tool = WorldState.GuildHallEditor.Tool;

        if (tool != GuildHallTool.Rect)
            SendHallTile(tile);

        if (tool is GuildHallTool.Paint or GuildHallTool.Erase or GuildHallTool.Rect)
        {
            HallStrokeActive = true;
            HallStrokeTool = tool;
            HallStrokeStart = tile;
            HallStrokeLast = tile;
        }

        return true;
    }

    /// <summary>
    ///     Top of OnRootClick and OnRootDoubleClick. The release of an editor-owned press is swallowed wherever the
    ///     mouse is released (a Paint/Erase/Rect drag can end over the editor panel), then the ownership clears.
    ///     While the editor is active, Alt+left on an editable tile is swallowed too and never reaches the world.
    /// </summary>
    private bool TryGuildHallClick(int screenX, int screenY, MouseButton button, bool ctrl, bool alt)
    {
        if ((button == MouseButton.Left) && WorldState.GuildHallEditor.ConsumePress())
            return true;

        return GuildHallEditorActive && (button == MouseButton.Left) && !ctrl && (HallTileAt(screenX, screenY) is not null);
    }

    /// <summary>
    ///     After PopulateFrameState. Root gets no mouse-move or mouse-up forward, so a held stroke follows HoveredTile each
    ///     frame and ends when the left button is no longer held.
    /// </summary>
    private void UpdateGuildHallEditor()
    {
        if (GuildHallEditorWindow is not { Visible: true } window)
        {
            HallStrokeActive = false;

            return;
        }

        window.Dock(WorldHud.ViewportBounds);

        if (!HallStrokeActive)
            return;

        if (HoveredHallTile() is { } tile && (tile != HallStrokeLast))
        {
            if (HallStrokeTool is GuildHallTool.Paint or GuildHallTool.Erase)
                foreach (var step in GuildHallStroke.Line(HallStrokeLast, tile))
                    SendHallTile(step);

            HallStrokeLast = tile;
        }

        if (InputBuffer.IsLeftButtonHeld)
            return;

        HallStrokeActive = false;

        if ((HallStrokeTool == GuildHallTool.Rect) && WorldState.GuildHallEditor.RectArgs(HallStrokeStart, HallStrokeLast) is { } rect)
            SendGuildHallEditor(rect);
    }

    /// <summary>In the background batch after the tile cursor: the rectangle being dragged and the hall entrance.</summary>
    private void DrawGuildHallEditorOverlay(SpriteBatch spriteBatch)
    {
        if (MapFile is null || TileHighlightTexture is null || GuildHallEditorWindow is not { Visible: true })
            return;

        DrawHallTileMark(spriteBatch, WorldState.GuildHallEditor.Entrance, HallEntranceMark);

        if (!HallStrokeActive || (HallStrokeTool != GuildHallTool.Rect))
            return;

        foreach (var tile in GuildHallStroke.Perimeter(HallStrokeStart, HallStrokeLast))
            DrawHallTileMark(spriteBatch, tile, HallRectOutline);
    }

    private void DrawHallTileMark(SpriteBatch spriteBatch, Point tile, Color color)
    {
        var tileWorld = Camera.TileToWorld(tile.X, tile.Y, MapFile!.Height);
        var tileScreen = Camera.WorldToScreen(new Vector2(tileWorld.X, tileWorld.Y));
        spriteBatch.Draw(TileHighlightTexture!, new Vector2((int)tileScreen.X, (int)tileScreen.Y), color);
    }
}
