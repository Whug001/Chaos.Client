#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.Components;

/// <summary>
///     The ornate wooden 9-slice frame the settings window (F4) and every dialog sub-panel wear: a tiled
///     <c>DlgBack2.spf</c> fill inside <c>nd_f01</c>–<c>nd_f08</c> border pieces.
/// </summary>
/// <remarks>
///     Extracted because this had been written out twice already -- once in <c>FramedDialogPanelBase</c> and once
///     inline in <c>PollPanel</c>, which says in its own comments that it is mirroring the other -- and the group
///     panels would have made three. Both of those now call in here, so the frame has one definition and the
///     texture set is loaded once for the whole client rather than once per panel.
/// </remarks>
public static class OrnateFrame
{
    /// <summary>Width of all four corner pieces.</summary>
    public const int CORNER_WIDTH = 31;

    /// <summary>Height of the two top corner pieces, and of the whole top border.</summary>
    public const int CORNER_TOP_HEIGHT = 24;

    /// <summary>
    ///     Height of the two bottom corner pieces, and of the ornate bottom border they sit in. Deep because that
    ///     border is built to house an OK button.
    /// </summary>
    public const int BORDER_BOTTOM_HEIGHT = 47;

    private static Texture2D? BackgroundTile;
    private static Texture2D? CornerBl;
    private static Texture2D? CornerBr;
    private static Texture2D? CornerTl;
    private static Texture2D? CornerTr;
    private static Texture2D? EdgeBottomOk;
    private static Texture2D? EdgeBottomRivets;
    private static Texture2D? EdgeLeft;
    private static Texture2D? EdgeRight;
    private static Texture2D? EdgeTop;
    private static bool TexturesLoaded;

    /// <summary>
    ///     Draws the full frame -- ornate footer included -- filling
    ///     <paramref name="w" />x<paramref name="h" /> at screen position (<paramref name="x" />,
    ///     <paramref name="y" />).
    /// </summary>
    /// <param name="okAreaStartX">
    ///     Where along the bottom border the plain OK-button backing takes over from the rivets, in panel-local
    ///     pixels. Null runs the rivets all the way to the corner, for a panel with no OK button.
    /// </param>
    public static void Draw(
        SpriteBatch spriteBatch,
        int x,
        int y,
        int w,
        int h,
        int? okAreaStartX = null)
    {
        EnsureTextures();
        DrawFill(spriteBatch, x, y, w, h);
        DrawSideEdges(spriteBatch, x, y, w, h, BORDER_BOTTOM_HEIGHT);

        //bottom edge: rivets on the left, plain backing behind the ok button on the right
        var okAreaStart = (okAreaStartX ?? (w - CORNER_WIDTH)) - 8;
        var rivetsWidth = okAreaStart - CORNER_WIDTH;
        var okAreaWidth = w - CORNER_WIDTH - okAreaStart;

        if (EdgeBottomRivets is not null)
            TileTexture(
                spriteBatch,
                EdgeBottomRivets,
                x + CORNER_WIDTH,
                y + h - BORDER_BOTTOM_HEIGHT,
                rivetsWidth,
                EdgeBottomRivets.Height);

        if (EdgeBottomOk is not null)
            TileTexture(
                spriteBatch,
                EdgeBottomOk,
                x + okAreaStart,
                y + h - BORDER_BOTTOM_HEIGHT,
                okAreaWidth,
                EdgeBottomOk.Height);

        //corners last, to cover where the edges overlap them
        AtlasHelper.Draw(spriteBatch, CornerTl, new Vector2(x, y), Color.White);
        AtlasHelper.Draw(spriteBatch, CornerTr, new Vector2(x + w - CORNER_WIDTH, y), Color.White);
        AtlasHelper.Draw(spriteBatch, CornerBl, new Vector2(x, y + h - BORDER_BOTTOM_HEIGHT), Color.White);

        AtlasHelper.Draw(
            spriteBatch,
            CornerBr,
            new Vector2(x + w - CORNER_WIDTH, y + h - BORDER_BOTTOM_HEIGHT),
            Color.White);
    }

    /// <summary>
    ///     Draws the same frame with the bottom mirrored from the top instead of the ornate footer, so the border
    ///     costs <see cref="CORNER_TOP_HEIGHT" /> at the bottom rather than <see cref="BORDER_BOTTOM_HEIGHT" />.
    /// </summary>
    /// <remarks>
    ///     For panels too short to spend 47 pixels on a footer built to hold an OK button they do not have. A stack
    ///     of group panels is the case this exists for: at the full frame's proportions five of them would be taller
    ///     than the window. Same art, same palette, just turned over -- the corner and edge pieces are ornamental
    ///     scrollwork rather than anything with a fixed up.
    /// </remarks>
    public static void DrawCompact(
        SpriteBatch spriteBatch,
        int x,
        int y,
        int w,
        int h)
    {
        EnsureTextures();
        DrawFill(spriteBatch, x, y, w, h);
        DrawSideEdges(spriteBatch, x, y, w, h, CORNER_TOP_HEIGHT);

        if (EdgeTop is not null)
            TileFlippedVertically(
                spriteBatch,
                EdgeTop,
                x + CORNER_WIDTH,
                y + h - EdgeTop.Height,
                w - (CORNER_WIDTH * 2));

        AtlasHelper.Draw(spriteBatch, CornerTl, new Vector2(x, y), Color.White);
        AtlasHelper.Draw(spriteBatch, CornerTr, new Vector2(x + w - CORNER_WIDTH, y), Color.White);
        DrawFlippedVertically(spriteBatch, CornerTl, x, y + h - CORNER_TOP_HEIGHT);
        DrawFlippedVertically(spriteBatch, CornerTr, x + w - CORNER_WIDTH, y + h - CORNER_TOP_HEIGHT);
    }

    private static void DrawFill(
        SpriteBatch spriteBatch,
        int x,
        int y,
        int w,
        int h)
    {
        if (BackgroundTile is not null)
            TileTexture(
                spriteBatch,
                BackgroundTile,
                x,
                y,
                w,
                h);
    }

    /// <summary>
    ///     Top, left and right edges. <paramref name="bottomInset" /> is how much of the panel's bottom the caller's
    ///     own bottom border occupies, so the side edges stop above it.
    /// </summary>
    private static void DrawSideEdges(
        SpriteBatch spriteBatch,
        int x,
        int y,
        int w,
        int h,
        int bottomInset)
    {
        if (EdgeTop is not null)
            TileTexture(
                spriteBatch,
                EdgeTop,
                x + CORNER_WIDTH,
                y,
                w - (CORNER_WIDTH * 2),
                EdgeTop.Height);

        if (EdgeLeft is not null)
            TileTexture(
                spriteBatch,
                EdgeLeft,
                x,
                y + CORNER_TOP_HEIGHT,
                EdgeLeft.Width,
                h - CORNER_TOP_HEIGHT - bottomInset);

        if (EdgeRight is not null)
            TileTexture(
                spriteBatch,
                EdgeRight,
                x + w - EdgeRight.Width,
                y + CORNER_TOP_HEIGHT,
                EdgeRight.Width,
                h - CORNER_TOP_HEIGHT - bottomInset);
    }

    private static void DrawFlippedVertically(SpriteBatch spriteBatch, Texture2D? texture, int x, int y)
    {
        if (texture is null)
            return;

        AtlasHelper.Draw(
            spriteBatch,
            texture,
            new Vector2(x, y),
            null,
            Color.White,
            0f,
            Vector2.Zero,
            1f,
            SpriteEffects.FlipVertically,
            0f);
    }

    private static void TileFlippedVertically(
        SpriteBatch spriteBatch,
        Texture2D texture,
        int x,
        int y,
        int width)
    {
        //only ever used for the top edge turned over along the bottom, which is one texture tall, so this tiles in
        //one direction. Partial tiles are drawn from the texture's right-hand side rather than its left: a flip
        //reverses which column ends up against the corner, and taking the left columns would leave a seam there.
        for (var tx = 0; tx < width; tx += texture.Width)
        {
            var drawW = Math.Min(texture.Width, width - tx);

            AtlasHelper.Draw(
                spriteBatch,
                texture,
                new Vector2(x + tx, y),
                new Rectangle(
                    texture.Width - drawW,
                    0,
                    drawW,
                    texture.Height),
                Color.White,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.FlipVertically,
                0f);
        }
    }

    /// <summary>
    ///     Repeats <paramref name="texture" /> across the given region, cropping the last row and column rather
    ///     than stretching them.
    /// </summary>
    public static void TileTexture(
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

    private static void EnsureTextures()
    {
        if (TexturesLoaded)
            return;

        var renderer = UiRenderer.Instance;

        if (renderer is null)
            return;

        //only latched once the renderer exists, so a draw that happens before it is built retries rather than
        //caching a frame's worth of nulls for the life of the process
        TexturesLoaded = true;

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
}
