#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>One gallery picture: the drawing at its real size in a frame of its award colour, the title and the author below.</summary>
public sealed class GalleryArtCell : UIPanel
{
    public const int WIDTH = 117;
    public const int HEIGHT = PICTURE_HEIGHT + (2 * FRAME) + 2 + (2 * TextRenderer.CHAR_HEIGHT);
    private const int FRAME = 2;
    private const int PICTURE_HEIGHT = PixelDrawing.HEIGHT;
    private const int PICTURE_WIDTH = PixelDrawing.WIDTH;

    private static readonly Color SelectedFill = new(255, 255, 255, 36);

    private readonly UILabel AuthorLabel;
    private readonly UILabel TitleLabel;
    private Color Frame;

    public GalleryArtCell()
    {
        Width = WIDTH;
        Height = HEIGHT;
        Visible = false;

        var textTop = PICTURE_HEIGHT + (2 * FRAME) + 2;
        TitleLabel = Label(textTop, LegendColors.White);
        AuthorLabel = Label(textTop + TextRenderer.CHAR_HEIGHT, LegendColors.Gray);
    }

    public int Id { get; private set; }
    public bool Selected { get; set; }

    /// <summary>The thumbnail, owned by <c>CollegeDrawings</c>; null shows "Loading...".</summary>
    public Texture2D? Picture { get; set; }

    public event Action? Clicked;
    public event Action? DoubleClicked;

    public void Set(int id, string title, string author, Color frame)
    {
        Id = id;
        TitleLabel.Text = title;
        AuthorLabel.Text = author;
        Frame = frame;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        if (Selected)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), SelectedFill);

        var left = ScreenX + ((WIDTH - PICTURE_WIDTH - (2 * FRAME)) / 2);
        DrawRectClipped(spriteBatch, new Rectangle(left, ScreenY, PICTURE_WIDTH + (2 * FRAME), PICTURE_HEIGHT + (2 * FRAME)), Frame);

        if (Picture is not null)
            DrawTexture(spriteBatch, Picture, new Vector2(left + FRAME, ScreenY + FRAME), Color.White);
        else
        {
            DrawRectClipped(spriteBatch, new Rectangle(left + FRAME, ScreenY + FRAME, PICTURE_WIDTH, PICTURE_HEIGHT), Color.Black);
            DrawTextClipped(spriteBatch, new Vector2(left + FRAME + 18, ScreenY + FRAME + 30), "Loading...", LegendColors.Gray, false);
        }

        base.Draw(spriteBatch);
    }

    public override void OnClick(ClickEvent e)
    {
        e.Handled = true;
        Clicked?.Invoke();
    }

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        e.Handled = true;
        DoubleClicked?.Invoke();
    }

    private UILabel Label(int y, Color color)
    {
        var label = new UILabel
        {
            X = 0,
            Y = y,
            Width = WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = color,
            IsHitTestVisible = false
        };

        AddChild(label);

        return label;
    }
}
