#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.ViewPort;

/// <summary>
///     Top-right wooden voting panel. Renders WorldState.ArenaPoll: title, countdown, one row per candidate (name + vote
///     count + flat tally bar), with the local player's pick highlighted. Clicking a row while the poll is open raises
///     <see cref="VoteCast" /> (pollId, optionIndex); the packet send is wired by the owning screen. The ornate 9-slice
///     wooden frame is drawn inline, mirroring FramedDialogPanelBase (DlgBack2.spf tiled fill + nd_f01–f08 border pieces).
/// </summary>
public sealed class ArenaVotePanel : UIPanel
{
    private const int PANEL_WIDTH = 178;
    private const int PAD = 12;
    private const int TITLE_Y = 7;
    private const int DIVIDER_Y = 23;
    private const int ROWS_TOP = 30;
    private const int ROW_H = 22;
    private const int ROWS_GAP = 5;

    //frame corner/edge dimensions (mirror FramedDialogPanelBase)
    private const int CORNER_TL_W = 31;
    private const int CORNER_TL_H = 24;
    private const int CORNER_TR_W = 31;
    private const int CORNER_BL_H = 47;
    private const int CORNER_BR_W = 31;
    private const int CORNER_BR_H = 47;
    private const int BORDER_BOTTOM = 47;

    private static readonly Color Gold = new(252, 215, 80);
    private static readonly Color White = new(255, 255, 255);
    private static readonly Color Silver = new(201, 198, 182);
    private static readonly Color Shadow = Color.Black;
    private static readonly Color Footer = new(214, 190, 146);
    private static readonly Color TimerLow = new(255, 120, 92);
    private static readonly Color TrackBg = new(18, 11, 5);
    private static readonly Color TrackBorder = new(54, 34, 16);
    private static readonly Color Bar = new(228, 158, 40);
    private static readonly Color BarHot = new(255, 201, 70);

    private Rectangle ViewportBounds;

    private Texture2D? BackgroundTile;
    private Texture2D? CornerBl;
    private Texture2D? CornerBr;
    private Texture2D? CornerTl;
    private Texture2D? CornerTr;
    private Texture2D? EdgeBottomOk;
    private Texture2D? EdgeBottomRivets;
    private Texture2D? EdgeLeft;
    private Texture2D? EdgeRight;
    private Texture2D? EdgeTop;
    private bool FrameTexturesLoaded;

    public event Action<byte, byte>? VoteCast;

    public ArenaVotePanel(Rectangle viewportBounds)
    {
        Name = "ArenaVotePanel";
        ViewportBounds = viewportBounds;
        Width = PANEL_WIDTH;
        //stay Visible=true so the parent UIPanel keeps invoking Update/Draw (it skips hidden
        //children). Rendering + hit-testing are gated on WorldState.ArenaPoll.ShouldShow instead.
        IsHitTestVisible = false;
        Visible = true;
    }

    public void SetViewportBounds(Microsoft.Xna.Framework.Rectangle bounds) => ViewportBounds = bounds;

    public override void Update(GameTime gameTime)
    {
        var poll = WorldState.ArenaPoll;
        poll.Tick(gameTime.ElapsedGameTime.TotalSeconds); //advance staleness timer (hides if updates stop)

        if (!poll.ShouldShow)
        {
            IsHitTestVisible = false;

            return;
        }

        //size to current option count; anchor top-right
        var rows = poll.Options.Count;
        Height = ROWS_TOP + (rows * ROW_H) + ROWS_GAP + BORDER_BOTTOM;
        X = ViewportBounds.Right - PANEL_WIDTH - 2;
        Y = ViewportBounds.Top + 2;
        IsHitTestVisible = true;
    }

    public override void OnClick(ClickEvent e)
    {
        var poll = WorldState.ArenaPoll;

        if (!poll.IsOpen)
            return;

        if (e.Button != MouseButton.Left)
            return;

        //e.ScreenX/e.ScreenY are screen-space cursor coords (ClickEvent : MouseEvent); ScreenY is this
        //panel's screen-space top. Map the cursor's Y into a row band beneath the rows-top offset.
        var localY = e.ScreenY - ScreenY - ROWS_TOP;

        if (localY < 0)
            return;

        var row = localY / ROW_H;

        if ((row < 0) || (row >= poll.Options.Count))
            return;

        poll.MyVoteIndex = row;
        VoteCast?.Invoke(poll.PollId, (byte)row);
        e.Handled = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!WorldState.ArenaPoll.ShouldShow)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        EnsureFrameTextures();

        var poll = WorldState.ArenaPoll;
        var sx = ScreenX;
        var sy = ScreenY;
        var w = Width;
        var h = Height;

        DrawFrame(spriteBatch, sx, sy, w, h);

        //── title (centered, gold, shadowed) ──
        const string TITLE = "Arena Vote";
        var titleX = sx + ((w - TextRenderer.MeasureWidth(TITLE)) / 2);
        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(titleX, sy + TITLE_Y),
            TITLE,
            Gold,
            Shadow);

        //── timer (top-right), or WINNER label when closed ──
        if (poll.IsClosed)
        {
            const string DONE = "WINNER";
            TextRenderer.DrawShadowedText(
                spriteBatch,
                new Vector2(sx + w - PAD - TextRenderer.MeasureWidth(DONE), sy + TITLE_Y),
                DONE,
                Gold,
                Shadow);
        } else
        {
            var timer = $"{poll.SecondsRemaining / 60}:{poll.SecondsRemaining % 60:D2}";
            var timerWidth = TextRenderer.MeasureWidth(timer);
            var tColor = poll.SecondsRemaining <= 10 ? TimerLow : Gold;

            DrawRectClipped(
                spriteBatch,
                new Rectangle(
                    sx + w - PAD - timerWidth - 4,
                    sy + 6,
                    timerWidth + 6,
                    12),
                new Color(0, 0, 0, 110));

            TextRenderer.DrawShadowedText(
                spriteBatch,
                new Vector2(sx + w - PAD - timerWidth, sy + TITLE_Y),
                timer,
                tColor,
                Shadow);
        }

        //── divider ──
        DrawRectClipped(
            spriteBatch,
            new Rectangle(
                sx + PAD,
                sy + DIVIDER_Y,
                w - (PAD * 2),
                1),
            new Color(0, 0, 0, 120));

        //── rows ──
        var maxVotes = 1;

        foreach (var o in poll.Options)
            if (o.Votes > maxVotes)
                maxVotes = o.Votes;

        for (var i = 0; i < poll.Options.Count; i++)
        {
            var o = poll.Options[i];
            var rowY = sy + ROWS_TOP + (i * ROW_H);
            var mine = i == poll.MyVoteIndex;
            var won = poll.IsClosed && (i == poll.WinningIndex);
            var nameColor = mine || won ? White : Silver;

            //selected/winning marker
            if (mine || won)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(
                        sx + PAD,
                        rowY + 2,
                        4,
                        7),
                    Gold);

            TextRenderer.DrawShadowedText(
                spriteBatch,
                new Vector2(sx + PAD + 7, rowY),
                o.Name,
                nameColor,
                Shadow);

            var cnt = o.Votes.ToString();
            TextRenderer.DrawShadowedText(
                spriteBatch,
                new Vector2(sx + w - PAD - TextRenderer.MeasureWidth(cnt), rowY),
                cnt,
                Gold,
                Shadow);

            //flat tally bar (track + fill ∝ votes/maxVotes)
            var trackX = sx + PAD;
            var trackY = rowY + 13;
            var trackW = w - (PAD * 2);

            DrawRectClipped(
                spriteBatch,
                new Rectangle(
                    trackX,
                    trackY,
                    trackW,
                    6),
                TrackBorder);

            DrawRectClipped(
                spriteBatch,
                new Rectangle(
                    trackX + 1,
                    trackY + 1,
                    trackW - 2,
                    4),
                TrackBg);

            var fillW = (int)Math.Round((trackW - 2) * (o.Votes / (double)maxVotes));

            if (fillW > 0)
                DrawRectClipped(
                    spriteBatch,
                    new Rectangle(
                        trackX + 1,
                        trackY + 1,
                        fillW,
                        4),
                    mine || won ? BarHot : Bar);
        }

        //── footer hint ──
        var hint = poll.IsClosed ? "Vote complete" : "Click a match to vote";
        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(sx + ((w - TextRenderer.MeasureWidth(hint)) / 2), sy + h - BORDER_BOTTOM + 6),
            hint,
            Footer,
            Shadow);
    }

    //── frame (9-slice, mirrors FramedDialogPanelBase.Draw exactly) ──
    private void DrawFrame(
        SpriteBatch spriteBatch,
        int sx,
        int sy,
        int w,
        int h)
    {
        //1. tile dlgback2.spf across entire panel as background fill
        if (BackgroundTile is not null)
            TileTexture(
                spriteBatch,
                BackgroundTile,
                sx,
                sy,
                w,
                h);

        //2. frame edges (tiled between corners)
        if (EdgeTop is not null)
            TileTexture(
                spriteBatch,
                EdgeTop,
                sx + CORNER_TL_W,
                sy,
                w - CORNER_TL_W - CORNER_TR_W,
                EdgeTop.Height);

        if (EdgeLeft is not null)
            TileTexture(
                spriteBatch,
                EdgeLeft,
                sx,
                sy + CORNER_TL_H,
                EdgeLeft.Width,
                h - CORNER_TL_H - CORNER_BL_H);

        if (EdgeRight is not null)
            TileTexture(
                spriteBatch,
                EdgeRight,
                sx + w - EdgeRight.Width,
                sy + CORNER_TL_H,
                EdgeRight.Width,
                h - CORNER_TL_H - CORNER_BR_H);

        //bottom edge: rivets on the left, plain background on the right (no OK button on this panel)
        var okAreaStart = w - CORNER_BR_W - 8;
        var rivetsWidth = okAreaStart - CORNER_TL_W;
        var okAreaWidth = w - CORNER_BR_W - okAreaStart;

        if ((EdgeBottomRivets is not null) && (rivetsWidth > 0))
            TileTexture(
                spriteBatch,
                EdgeBottomRivets,
                sx + CORNER_TL_W,
                sy + h - BORDER_BOTTOM,
                rivetsWidth,
                EdgeBottomRivets.Height);

        if ((EdgeBottomOk is not null) && (okAreaWidth > 0))
            TileTexture(
                spriteBatch,
                EdgeBottomOk,
                sx + okAreaStart,
                sy + h - BORDER_BOTTOM,
                okAreaWidth,
                EdgeBottomOk.Height);

        //3. corners (drawn last to cover edge overlap)
        if (CornerTl is not null)
            DrawTexture(
                spriteBatch,
                CornerTl,
                new Vector2(sx, sy),
                Color.White);

        if (CornerTr is not null)
            DrawTexture(
                spriteBatch,
                CornerTr,
                new Vector2(sx + w - CORNER_TR_W, sy),
                Color.White);

        if (CornerBl is not null)
            DrawTexture(
                spriteBatch,
                CornerBl,
                new Vector2(sx, sy + h - CORNER_BL_H),
                Color.White);

        if (CornerBr is not null)
            DrawTexture(
                spriteBatch,
                CornerBr,
                new Vector2(sx + w - CORNER_BR_W, sy + h - CORNER_BR_H),
                Color.White);
    }

    private void EnsureFrameTextures()
    {
        if (FrameTexturesLoaded)
            return;

        FrameTexturesLoaded = true;
        var renderer = UiRenderer.Instance;

        if (renderer is null)
            return;

        CornerTl = renderer.GetSpfTexture("nd_f01.spf");
        CornerTr = renderer.GetSpfTexture("nd_f02.spf");
        CornerBl = renderer.GetSpfTexture("nd_f03.spf");
        CornerBr = renderer.GetSpfTexture("nd_f04.spf");
        EdgeTop = renderer.GetSpfTexture("nd_f05.spf");
        EdgeLeft = renderer.GetSpfTexture("nd_f06.spf");
        EdgeRight = renderer.GetSpfTexture("nd_f07.spf");
        EdgeBottomOk = renderer.GetSpfTexture("nd_f08.spf");
        EdgeBottomRivets = renderer.GetSpfTexture("nd_f08_1.spf");
        BackgroundTile = renderer.GetSpfTexture("DlgBack2.spf");
    }

    //tiles a texture across a region, mirroring FramedDialogPanelBase.TileTexture exactly: uses
    //AtlasHelper.Draw so atlas-backed CachedTexture2D partial-tile source rects resolve correctly.
    private static void TileTexture(
        SpriteBatch spriteBatch,
        Texture2D texture,
        int x,
        int y,
        int width,
        int height)
    {
        if ((width <= 0) || (height <= 0))
            return;

        var texW = texture.Width;
        var texH = texture.Height;

        for (var ty = 0; ty < height; ty += texH)
        {
            var drawH = Math.Min(texH, height - ty);

            for (var tx = 0; tx < width; tx += texW)
            {
                var drawW = Math.Min(texW, width - tx);

                if ((drawW == texW) && (drawH == texH))
                    AtlasHelper.Draw(
                        spriteBatch,
                        texture,
                        new Vector2(x + tx, y + ty),
                        Color.White);
                else
                    AtlasHelper.Draw(
                        spriteBatch,
                        texture,
                        new Vector2(x + tx, y + ty),
                        new Rectangle(
                            0,
                            0,
                            drawW,
                            drawH),
                        Color.White);
            }
        }
    }
}
