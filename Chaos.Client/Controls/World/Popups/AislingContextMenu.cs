#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     A popup menu that appears when Ctrl+clicking a non-self aisling. Uses popupbox.epf as background. Top row shows the
///     player's name, bottom 5 rows are clickable options: info, group request, whisper, card challenge, tamer bout. The row
///     arithmetic is <see cref="ContextMenuRows" />.
/// </summary>
public sealed class AislingContextMenu : UIPanel
{
    private const string EPF_FILE = "popupbox.epf";
    private const int BOX_X = 4;
    private const int BOX_WIDTH = ContextMenuRows.ROW_WIDTH;
    private const int BOX_HEIGHT = ContextMenuRows.ROW_HEIGHT;
    private const int BOX_START_Y = ContextMenuRows.NAME_TOP;

    private static readonly Color BOX_FILL = new Color(68, 68, 68) * (140 / 255f);
    private static readonly Color BOX_HOVER = new Color(187, 187, 187) * (160 / 255f);

    private const int OPTION_COUNT = 5;

    private static Texture2D? TallFrame;

    private static readonly string[] OPTION_LABELS =
    [
        "info",
        "group request",
        "whisper",
        "card challenge",
        ContextMenuRows.TamerBout
    ];

    private readonly UIImage FrameImage;
    private readonly UIPanel HoverOverlay;
    private readonly UILabel NameLabel;
    private readonly Action[] OptionCallbacks = new Action[OPTION_COUNT];
    private readonly UILabel[] OptionLabels;
    private int HoveredIndex = -1;

    public AislingContextMenu()
    {
        Visible = false;
        UsesControlStack = true;
        BackgroundColor = BOX_FILL;

        HoverOverlay = new UIPanel
        {
            Name = "HoverOverlay",
            BackgroundColor = BOX_HOVER,
            Visible = false,
            IsHitTestVisible = false,
            X = BOX_X,
            Width = BOX_WIDTH,
            Height = BOX_HEIGHT
        };

        AddChild(HoverOverlay);

        FrameImage = new UIImage
        {
            Name = "Frame",
            X = 0,
            Y = 0,
            IsHitTestVisible = false
        };

        AddChild(FrameImage);

        NameLabel = new UILabel
        {
            Name = "Name",
            X = BOX_X,
            Y = BOX_START_Y,
            Width = BOX_WIDTH,
            Height = BOX_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            PaddingLeft = 0,
            TruncateWithEllipsis = false
        };

        AddChild(NameLabel);

        OptionLabels = new UILabel[OPTION_COUNT];

        for (var i = 0; i < OPTION_COUNT; i++)
        {
            OptionLabels[i] = new UILabel
            {
                Name = $"Option{i}",
                X = BOX_X,
                Y = ContextMenuRows.RowTop(i),
                Width = BOX_WIDTH,
                Height = BOX_HEIGHT,
                Text = OPTION_LABELS[i],
                PaddingLeft = 0,
                ForegroundColor = Color.White,
                TruncateWithEllipsis = false
            };

            AddChild(OptionLabels[i]);
        }
    }

    //popupbox.epf only fits 3 rows; repeat its middle band once for each row past the third
    private static Texture2D BuildTallFrame(Texture2D source)
    {
        var width = source.Width;
        var height = source.Height;
        var pixels = new Color[width * height];
        source.GetData(pixels);

        var extraBands = ContextMenuRows.ExtraBands(OPTION_COUNT);
        var tallHeight = ContextMenuRows.FrameHeight(height, OPTION_COUNT);
        var tall = new Color[width * tallHeight];

        for (var y = 0; y < tallHeight; y++)
        {
            //rows 0-63 stay put; rows 50-63 repeat once per added band; the rest moves lower
            var from = Math.Clamp(ContextMenuRows.SourceRow(y, extraBands), 0, height - 1);
            Array.Copy(pixels, from * width, tall, y * width, width);
        }

        var texture = new Texture2D(source.GraphicsDevice, width, tallHeight);
        texture.SetData(tall);

        return texture;
    }

    public override void Dispose()
    {
        //TallFrame is a shared static: detach it so UIImage.Dispose does not free it
        //(same pattern as CustomCheckBox, CommissionBoardRowView, WornRow, TownImportRow)
        FrameImage.Texture = null;
        base.Dispose();
    }

    private Texture2D? GetFrameTexture()
    {
        if (FrameImage.Texture is { IsDisposed: false } current)
            return current;

        if (UiRenderer.Instance is null)
            return null;

        var frameCount = UiRenderer.Instance.GetEpfFrameCount(EPF_FILE);

        if (frameCount <= 0)
            return null;

        var original = UiRenderer.Instance.GetEpfTexture(EPF_FILE, 0);
        if (TallFrame is null || TallFrame.IsDisposed)
            TallFrame = BuildTallFrame(original);
        var texture = TallFrame;
        FrameImage.Texture = texture;
        FrameImage.Width = texture.Width;
        FrameImage.Height = texture.Height;
        Width = texture.Width;
        Height = texture.Height;

        return texture;
    }

    public void Hide()
    {
        InputDispatcher.Instance?.RemoveControl(this);
        Visible = false;
        HoveredIndex = -1;
    }

    public void Show(
        int screenX,
        int screenY,
        string name,
        Action onInfo,
        Action onGroupRequest,
        Action onWhisper,
        Action onChallenge,
        Action onTamerBout)
    {
        var bg = GetFrameTexture();

        if (bg is null)
            return;

        NameLabel.Text = name;
        NameLabel.ForegroundColor = Color.White;
        OptionCallbacks[0] = onInfo;
        OptionCallbacks[1] = onGroupRequest;
        OptionCallbacks[2] = onWhisper;
        OptionCallbacks[3] = onChallenge;
        OptionCallbacks[4] = onTamerBout;

        X = screenX;
        Y = screenY;

        //clamp to screen bounds
        if ((X + Width) > ChaosGame.VIRTUAL_WIDTH)
            X = ChaosGame.VIRTUAL_WIDTH - Width;

        if ((Y + Height) > ChaosGame.VIRTUAL_HEIGHT)
            Y = ChaosGame.VIRTUAL_HEIGHT - Height;

        HoveredIndex = -1;
        InputDispatcher.Instance?.PushControl(this);
        Visible = true;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        var localX = e.ScreenX - ScreenX;
        var localY = e.ScreenY - ScreenY;

        HoveredIndex = localX is >= BOX_X and < BOX_X + BOX_WIDTH ? ContextMenuRows.RowAt(localY, OPTION_COUNT) : -1;

        if (HoveredIndex >= 0)
        {
            HoverOverlay.Y = ContextMenuRows.RowTop(HoveredIndex);
            HoverOverlay.Visible = true;
        } else
            HoverOverlay.Visible = false;
    }

    public override void OnMouseLeave()
    {
        HoveredIndex = -1;
        HoverOverlay.Visible = false;
    }

    public override void OnClick(ClickEvent e)
    {
        if (e.Button == MouseButton.Right)
        {
            Hide();
            e.Handled = true;

            return;
        }

        if (HoveredIndex is >= 0 and < OPTION_COUNT)
        {
            OptionCallbacks[HoveredIndex]();
            Hide();
        } else
            Hide();

        e.Handled = true;
    }

}
