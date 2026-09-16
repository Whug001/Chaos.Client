#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.ViewPort;

/// <summary>
///     Top-right wooden voting panel. Renders WorldState.Poll: title, countdown, one row per option (text + vote
///     count + flat tally bar), with the local player's pick highlighted. Clicking a row while the poll is open raises
///     <see cref="VoteCast" /> (pollId, optionIndex); the packet send is wired by the owning screen. The ornate 9-slice
///     wooden frame comes from <see cref="OrnateFrame" /> (DlgBack2.spf tiled fill + nd_f01–f08 border pieces).
/// </summary>
public sealed class PollPanel : UIPanel
{
    private const int PANEL_WIDTH = 178;
    private const int PAD = 12;
    private const int TITLE_Y = 7;
    private const int DIVIDER_Y = 23;
    private const int ROWS_TOP = 30;
    private const int ROW_H = 22;
    private const int ROWS_GAP = 5;

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

    public event Action<byte, byte>? VoteCast;

    public PollPanel(Rectangle viewportBounds)
    {
        Name = "PollPanel";
        ViewportBounds = viewportBounds;
        Width = PANEL_WIDTH;
        //stay Visible=true so the parent UIPanel keeps invoking Update/Draw (it skips hidden
        //children). Rendering + hit-testing are gated on WorldState.Poll.ShouldShow instead.
        IsHitTestVisible = false;
        Visible = true;
    }

    public void SetViewportBounds(Microsoft.Xna.Framework.Rectangle bounds) => ViewportBounds = bounds;

    public override void Update(GameTime gameTime)
    {
        var poll = WorldState.Poll;
        poll.Tick(gameTime.ElapsedGameTime.TotalSeconds); //advance staleness timer (hides if updates stop)

        if (!poll.ShouldShow)
        {
            IsHitTestVisible = false;

            return;
        }

        //size to current option count; anchor top-right
        var rows = poll.Options.Count;
        Height = ROWS_TOP + (rows * ROW_H) + ROWS_GAP + OrnateFrame.BORDER_BOTTOM_HEIGHT;
        X = ViewportBounds.Right - PANEL_WIDTH - 2;
        Y = ViewportBounds.Top + 2;
        IsHitTestVisible = true;
    }

    public override void OnClick(ClickEvent e)
    {
        var poll = WorldState.Poll;

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
        if (!WorldState.Poll.ShouldShow)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var poll = WorldState.Poll;
        var sx = ScreenX;
        var sy = ScreenY;
        var w = Width;
        var h = Height;

        OrnateFrame.Draw(spriteBatch, sx, sy, w, h);

        //── title (centered, gold, shadowed) ──
        var title = poll.Title;
        var titleX = sx + ((w - TextRenderer.MeasureWidth(title)) / 2);
        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(titleX, sy + TITLE_Y),
            title,
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
                o.Text,
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
            new Vector2(sx + ((w - TextRenderer.MeasureWidth(hint)) / 2), sy + h - OrnateFrame.BORDER_BOTTOM_HEIGHT + 6),
            hint,
            Footer,
            Shadow);
    }
}
