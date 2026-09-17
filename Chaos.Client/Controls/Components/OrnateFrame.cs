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
///     inline in <c>PollPanel</c>, which says in its own comments that it is mirroring the other. Both now call in
///     here, so the frame has one definition and the texture set is loaded once for the whole client rather than
///     once per panel.
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
        DrawSideEdges(spriteBatch, x, y, w, h);

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
    ///     Top, left and right edges. The side edges stop above the ornate bottom border rather than running into
    ///     it.
    /// </summary>
    private static void DrawSideEdges(
        SpriteBatch spriteBatch,
        int x,
        int y,
        int w,
        int h)
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
                h - CORNER_TOP_HEIGHT - BORDER_BOTTOM_HEIGHT);

        if (EdgeRight is not null)
            TileTexture(
                spriteBatch,
                EdgeRight,
                x + w - EdgeRight.Width,
                y + CORNER_TOP_HEIGHT,
                EdgeRight.Width,
                h - CORNER_TOP_HEIGHT - BORDER_BOTTOM_HEIGHT);
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
