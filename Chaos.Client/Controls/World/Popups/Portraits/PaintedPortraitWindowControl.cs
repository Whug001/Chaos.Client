#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.ViewModel.Portraits;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Portraits;

/// <summary>
///     The Painted Portrait use window: the 48 x 56 picture, "Painted by {painter}", Set as my portrait and Close.
///     Opens from the server's PaintedPortraitDisplay; an empty picture shows a blank frame.
/// </summary>
public sealed class PaintedPortraitWindowControl : GuildCloakDialogBase
{
    private const int WIDTH = 216;
    private const int LEFT = 16;
    private const int TITLE_TOP = 10;
    private const int CONTENT_TOP = 40;
    private const int SET_WIDTH = 124;
    private const int CLOSE_WIDTH = 52;
    private const int BUTTON_GAP = 6;
    private const int BUTTONS_TOP = CONTENT_TOP + PortraitCanvas.HEIGHT + TextRenderer.CHAR_HEIGHT + 12;

    private readonly UILabel PainterLabel;
    private readonly UIImage Picture;
    private readonly UIPanel PictureFrame;

    public PaintedPortraitWindowControl()
        : base("_nsett", false)
    {
        Name = "PaintedPortraitWindow";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = BUTTONS_TOP + CustomButton.HEIGHT + 8 + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        Caption("Painted Portrait", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        PictureFrame = new UIPanel
        {
            X = (WIDTH - PortraitCanvas.WIDTH - 2) / 2,
            Y = CONTENT_TOP,
            Width = PortraitCanvas.WIDTH + 2,
            Height = PortraitCanvas.HEIGHT + 2,
            BorderColor = Color.White,
            IsHitTestVisible = false
        };
        AddChild(PictureFrame);

        Picture = new UIImage
        {
            Name = "PortraitPicture",
            X = PictureFrame.X + 1,
            Y = PictureFrame.Y + 1,
            Width = PortraitCanvas.WIDTH,
            Height = PortraitCanvas.HEIGHT,
            IsHitTestVisible = false
        };
        AddChild(Picture);

        PainterLabel = Caption(
            string.Empty,
            LEFT,
            CONTENT_TOP + PortraitCanvas.HEIGHT + 4,
            WIDTH - (2 * LEFT),
            HorizontalAlignment.Center,
            LegendColors.Gray);

        var setX = (WIDTH - SET_WIDTH - BUTTON_GAP - CLOSE_WIDTH) / 2;
        AddButton("Set as my portrait", SET_WIDTH, setX, BUTTONS_TOP, OnSetClicked);
        AddButton("Close", CLOSE_WIDTH, setX + SET_WIDTH + BUTTON_GAP, BUTTONS_TOP, OnCloseClicked);
    }

    /// <summary>Raised with the item's unique id when Set as my portrait is clicked. The item is kept.</summary>
    public event Action<ulong>? SetRequested;

    /// <summary>The unique id of the item on display.</summary>
    public ulong UniqueId { get; private set; }

    /// <summary>Shows the window with the item's picture; an empty picture shows a blank frame.</summary>
    public void Open(ulong uniqueId, string painter, byte[] picture)
    {
        UniqueId = uniqueId;
        PainterLabel.Text = $"Painted by {painter}";

        Picture.Texture?.Dispose();
        Picture.Texture = Decode(picture);

        Show();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            OnCloseClicked();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    private void OnSetClicked() => SetRequested?.Invoke(UniqueId);

    private void OnCloseClicked() => Hide();

    public override void Dispose()
    {
        Picture.Texture?.Dispose();
        Picture.Texture = null;
        base.Dispose();
    }

    private static Texture2D? Decode(byte[] picture)
    {
        if (picture is not { Length: > 0 })
            return null;

        try
        {
            using var stream = new MemoryStream(picture);

            return Texture2D.FromStream(ChaosGame.Device, stream);
        } catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }
}
