#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     A read-only piece: headings in gold, word-wrapped text and pictures at their real size, top to bottom, scrolled by
///     the bar or the mouse wheel. Pictures come from <see cref="CollegePictureTransfers" /> by hash; until one arrives
///     its place shows "Loading picture...", and the view lays itself out again when it does.
/// </summary>
public sealed class PieceView : UIPanel
{
    private const int GAP = 6;
    private const int HEADING_GAP = 2;
    private const int BAR_GAP = 4;
    private const int SCROLL_STEP = 2 * TextRenderer.CHAR_HEIGHT;

    private readonly UIPanel Content;
    private readonly CollegePictureTransfers Transfers;
    private readonly ScrollViewerControl Viewer;
    private readonly PieceViewport Viewport;
    private readonly HashSet<string> Waiting = new(StringComparer.Ordinal);
    private CollegePieceInfo? Piece;

    public PieceView(CollegePictureTransfers transfers, int width, int height)
    {
        Transfers = transfers;
        Width = width;
        Height = height;

        Content = new UIPanel
        {
            Width = width - ScrollBarControl.DEFAULT_WIDTH - BAR_GAP,
            IsPassThrough = true
        };

        Viewport = new PieceViewport(Content, SCROLL_STEP) { IsPassThrough = true };
        Viewport.AddChild(Content);

        Viewer = new ScrollViewerControl(Viewport)
        {
            Width = width,
            Height = height,
            ContentRightPadding = BAR_GAP
        };

        AddChild(Viewer);

        Transfers.PictureReady += OnPictureReady;
    }

    public override void Dispose()
    {
        Transfers.PictureReady -= OnPictureReady;
        ClearContent();
        base.Dispose();
    }

    /// <summary>Lays out <paramref name="piece" /> and scrolls back to its top.</summary>
    public void Show(CollegePieceInfo piece)
    {
        Piece = piece;
        Layout();
        ((IVerticalScrollable)Viewport).VerticalOffset = 0;
    }

    /// <summary>Changes the visible height; the width (and so the layout) stays.</summary>
    public void SetHeight(int height)
    {
        Height = height;
        Viewer.Height = height;
    }

    private void OnPictureReady(string hash)
    {
        if (Piece is not null && Waiting.Contains(hash))
            Layout();
    }

    private void Layout()
    {
        ClearContent();

        var y = 0;

        foreach (var block in Piece!.Blocks)
        {
            switch (block.Kind)
            {
                case CollegeBlockKind.Heading:
                    y += AddText(block.Text, y, LegendColors.Gold) + HEADING_GAP;

                    break;
                case CollegeBlockKind.Text:
                    y += AddText(block.Text, y, LegendColors.White);

                    break;
                case CollegeBlockKind.Picture:
                    y += AddPicture(block.Hash, y);

                    break;
            }

            y += GAP;
        }

        Content.Height = Math.Max(0, y - GAP);
    }

    private int AddPicture(string hash, int y)
    {
        if (!Transfers.TryGetTexture(hash, out var texture))
        {
            Waiting.Add(hash);

            return AddText("Loading picture...", y, LegendColors.Gray);
        }

        Content.AddChild(
            new UIImage
            {
                Texture = texture,
                X = Math.Max(0, (Content.Width - texture.Width) / 2),
                Y = y,
                Width = texture.Width,
                Height = texture.Height,
                IsHitTestVisible = false
            });

        return texture.Height;
    }

    private int AddText(string text, int y, Color color)
    {
        var label = new UILabel
        {
            X = 0,
            Y = y,
            Width = Content.Width,
            WordWrap = true,
            VerticalAlignment = VerticalAlignment.Top,
            ForegroundColor = color,
            IsHitTestVisible = false
        };

        //the same wrap the label does at draw time (TextRenderer.WrapText keeps line breaks), so the height fits it
        var lines = TextRenderer.WrapText(text, label.Width - label.PaddingLeft - label.PaddingRight)
                                .Count;

        label.Height = (lines * TextRenderer.CHAR_HEIGHT) + label.PaddingTop + label.PaddingBottom;
        label.Text = text;
        Content.AddChild(label);

        return label.Height;
    }

    private void ClearContent()
    {
        foreach (var child in Content.Children)
        {
            //the textures belong to CollegePictureTransfers, and UIImage.Dispose would dispose them
            if (child is UIImage image)
                image.Texture = null;

            child.Dispose();
        }

        Content.Children.Clear();
        Waiting.Clear();
    }

    //the clip host the viewer scrolls: it moves Content up by whole steps
    private sealed class PieceViewport(UIPanel content, int step) : UIPanel, IVerticalScrollable
    {
        private int OffsetUnits;

        private int MaxScrollPx => Math.Max(0, content.Height - Height);

        int IVerticalScrollable.VerticalViewport => Height / step;

        int IVerticalScrollable.VerticalExtent => ((IVerticalScrollable)this).VerticalViewport + ((MaxScrollPx + step - 1) / step);

        int IVerticalScrollable.VerticalOffset
        {
            get => OffsetUnits;
            set
            {
                OffsetUnits = value;
                content.Y = -Math.Min(value * step, MaxScrollPx);
            }
        }
    }
}
