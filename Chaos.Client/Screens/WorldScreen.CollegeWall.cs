#region
using Chaos.Client.Collections;
using Chaos.Client.Rendering;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    /// <summary>During the stripe pass, right after wall tile (x, y)'s foreground: its College frames and board.</summary>
    private void DrawCollegeWallTile(BatchBlendScope scope, int x, int y)
    {
        var decals = College!.Wall.At(x, y);

        if (decals.Count == 0)
            return;

        var tile = Camera.WorldToScreen(Camera.TileToWorld(x, y, MapFile!.Height));
        var top = MathF.Floor(tile.Y) + WallDecalGeometry.StripTop(WallDecalGeometry.PICTURE_HEIGHT);
        var seconds = Environment.TickCount64 / 1000.0;

        foreach (var decal in decals)
        {
            var origin = new Vector2(MathF.Floor(tile.X) + MirrorGeometry.FaceLocalX(decal.Side), top);

            if (decal.Shimmer)
            {
                var glow = WallDecalPainter.ShimmerIntensity(seconds, decal.Strip, decal.Strips);

                if (glow <= 0)
                    continue;

                scope.Require(BlendState.Additive);
                scope.Batch.Draw(decal.Texture, origin, Color.White * glow);
            } else
            {
                scope.Require(BlendState.AlphaBlend);
                scope.Batch.Draw(decal.Texture, origin, Color.White);
            }
        }
    }

    private void ShowWallArt(CollegeDisplayArgs args)
    {
        College!.WallSlots = args.WallSlots;
        College.CanHang = args.CanHang;
        College.Wall.SetWall(Device, args.WallSlots);
        College.WallFrame.Refresh(args.WallSlots, args.CanHang);
    }

    private void OpenWallFrame(int slot)
    {
        if (!College!.WallFrame.Visible)
            BringToFront(College.WallFrame);

        College.WallFrame.Open(slot, College.WallSlots.ElementAtOrDefault(slot), College.CanHang);
    }

    private void HandleBoardDisplay(CollegeDisplayArgs args)
    {
        var college = College!;
        var info = args.Board;
        var board = college.Board;
        var editor = college.BoardEditor;
        var view = college.BoardView;
        var wasEditing = editor.Visible;

        board.Apply(info);

        if ((board.DrawingVersion != college.WallBoardDrawingVersion) || (info.Live != college.WallBoardLive))
        {
            college.Wall.SetBoard(Device, info.Live ? board.Drawing : null);
            college.WallBoardDrawingVersion = board.DrawingVersion;
            college.WallBoardLive = info.Live;
        }

        var reopen = info.Live && (college.ReopenBoardViewUntilMs > 0) && (Environment.TickCount64 <= college.ReopenBoardViewUntilMs);
        college.ReopenBoardViewUntilMs = 0;

        if (info.Open)
        {
            if (info.CanDraw && !info.Live)
            {
                BringToFront(editor);
                editor.Open(board);
            } else
            {
                BringToFront(view);
                view.Open(board);
            }
        } else
        {
            if (!info.Live)
            {
                view.Hide();

                if (editor is { Visible: true, IsLive: true })
                    editor.Hide();
            }

            if (editor.Visible)
                editor.Apply(board);

            if (view.Visible)
                view.Refresh(board);

            if (wasEditing && !editor.Visible && !info.CanDraw && info.Live)
            {
                BringToFront(view);
                view.Open(board);
            } else if (reopen && !view.Visible)
            {
                BringToFront(view);
                view.Open(board);
            }
        }

        if (args.Message.Length == 0)
            return;

        if (editor.Visible)
            editor.ShowStatus(args.Message);
        else
            WorldState.Chat.AddOrangeBarMessage(args.Message);
    }

    private void HandleBoardSaved(CollegeDisplayArgs args)
    {
        var college = College!;

        if (college.BoardSaved.Visible || college.SavedListAsked)
        {
            college.SavedListAsked = false;
            ShowList(college.BoardSaved, () => college.BoardSaved.Show(args));
        }

        if (college.BoardEditor.Visible)
            college.BoardEditor.ShowStatus(args.Message);
    }

    /// <summary>
    ///     A new map or layout, or logout: the wall, the board and their windows go. The room sends the wall and board
    ///     again on the way back in, so a board view that was open reopens for the next few seconds. Only a layout swap
    ///     (between College room layouts) can still deliver an unsent live board; after a room change the server has already moved the player.
    /// </summary>
    private void ResetCollegeWall(bool sendUnsent, bool reopenView = true)
    {
        if (College is not { } college)
            return;

        if (sendUnsent)
            college.BoardEditor.SendUnsent();

        var viewWasOpen = college.BoardView.Visible;

        college.WallFrame.Hide();
        college.WallPicker.Hide();
        college.BoardView.Hide();
        college.BoardEditor.Hide();
        college.BoardSaved.Hide();
        college.Wall.Clear();
        college.Board.Clear();
        college.WallSlots = [];
        college.CanHang = false;
        college.SavedListAsked = false;
        college.WallBoardDrawingVersion = -1;
        college.WallBoardLive = false;
        college.ReopenBoardViewUntilMs = reopenView && viewWasOpen ? Environment.TickCount64 + 3000 : 0;
    }
}
