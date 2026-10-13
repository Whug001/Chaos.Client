#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The window a wall frame opens: the picture at zoom 4 with its title and artist, or "Empty frame". It offers Hang a
///     picture when the viewer may hang, and Take down when the frame is filled and the viewer may take it down.
/// </summary>
public sealed class WallFrameControl : GuildCloakDialogBase
{
    private const int WIDTH = 432;
    private const int HEIGHT = 404;
    private const int FRAME = 2;
    private const int PICTURE_WIDTH = PixelDrawing.WIDTH * CollegeProtocol.DRAWING_ZOOM;
    private const int PICTURE_HEIGHT = PixelDrawing.HEIGHT * CollegeProtocol.DRAWING_ZOOM;
    private const int TITLE_TOP = 8;
    private const int ARTIST_TOP = TITLE_TOP + TextRenderer.CHAR_HEIGHT + 2;
    private const int PICTURE_TOP = ARTIST_TOP + TextRenderer.CHAR_HEIGHT + 2;
    private const int BUTTONS_TOP = PICTURE_TOP + PICTURE_HEIGHT + (2 * FRAME) + 5;
    private const int BUTTON_WIDTH = 110;
    private const int BUTTON_GAP = 8;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly UILabel ArtistLabel;
    private readonly CustomButton HangButton;
    private readonly PictureView View;
    private readonly CustomButton TakeDownButton;
    private readonly UILabel TitleLabel;

    public WallFrameControl()
        : base("_nsett", false)
    {
        Name = "WallFrame";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        TitleLabel = Caption(string.Empty, 0, TITLE_TOP, WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        ArtistLabel = Caption(string.Empty, 0, ARTIST_TOP, WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        View = new PictureView
        {
            X = (WIDTH - PICTURE_WIDTH - (2 * FRAME)) / 2,
            Y = PICTURE_TOP,
            Width = PICTURE_WIDTH + (2 * FRAME),
            Height = PICTURE_HEIGHT + (2 * FRAME)
        };

        AddChild(View);

        var emptyLabel = Caption("Empty frame", 0, PICTURE_TOP + FRAME + ((PICTURE_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2), WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        View.EmptyLabel = emptyLabel;

        HangButton = AddButton("Hang a picture", BUTTON_WIDTH + 10, 16, BUTTONS_TOP, OnHang);
        TakeDownButton = AddButton("Take down", BUTTON_WIDTH, 16 + BUTTON_WIDTH + 10 + BUTTON_GAP, BUTTONS_TOP, OnTakeDown);
        HangButton.Visible = false;
        TakeDownButton.Visible = false;
    }

    public int Slot { get; private set; }

    public event Action<CollegeActionArgs>? ActionRequested;

    public event Action<int>? HangRequested;

    public void Open(int slot, CollegeWallSlotInfo? info, bool canHang)
    {
        Slot = slot;
        View.Picture?.Dispose();
        View.Picture = null;

        var filled = info is { Filled: true };

        if (filled && info!.Pixels.Length == CollegeProtocol.DRAWING_PIXELS)
            View.Picture = DrawingTextures.Build(PixelDrawing.From(info.Palette, info.Pixels), CollegeProtocol.DRAWING_ZOOM);

        TitleLabel.Text = filled ? info!.Title : string.Empty;
        ArtistLabel.Text = filled ? $"by {info!.Artist}" : string.Empty;
        View.EmptyLabel!.Visible = !filled;
        HangButton.Visible = canHang;
        TakeDownButton.Visible = filled && info!.CanTakeDown;

        if (!Visible)
            Show();
    }

    /// <summary>Re-lays out an open frame window for a new WallArt (someone hung or took down a picture).</summary>
    public void Refresh(IReadOnlyList<CollegeWallSlotInfo> slots, bool canHang)
    {
        if (Visible && (Slot < slots.Count))
            Open(Slot, slots[Slot], canHang);
    }

    public override void Hide()
    {
        ReleasePicture();
        base.Hide();
    }

    public override void Dispose()
    {
        ReleasePicture();
        base.Dispose();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    private void OnHang()
    {
        var slot = Slot;
        Hide();
        HangRequested?.Invoke(slot);
    }

    private void OnTakeDown()
    {
        var slot = Slot;
        Hide();

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.WallTakeDown,
                Number = (byte)slot
            });
    }

    private void ReleasePicture()
    {
        View.Picture?.Dispose();
        View.Picture = null;
    }

    private sealed class PictureView : UIPanel
    {
        public UILabel? EmptyLabel { get; set; }
        public Texture2D? Picture { get; set; }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), WallDecalPainter.FrameLine);

            if (Picture is not null)
                DrawTexture(spriteBatch, Picture, new Vector2(ScreenX + FRAME, ScreenY + FRAME), Color.White);
            else
                DrawRectClipped(spriteBatch, new Rectangle(ScreenX + FRAME, ScreenY + FRAME, PICTURE_WIDTH, PICTURE_HEIGHT), Color.Black);

            base.Draw(spriteBatch);
        }
    }
}
