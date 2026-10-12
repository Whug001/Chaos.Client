#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Bank;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.FittingRoom;

/// <summary>
///     One row of the fitting room's Wearing tab: the look's icon in its worn color, its name and an "x" that takes it
///     off. The window keeps five and re-binds them with <see cref="SetLook" />.
/// </summary>
public sealed class WornRow : UIPanel
{
    public const int HEIGHT = 34;

    private const int ICON_SIZE = 32;
    private const int ICON_X = 1;
    private const int NAME_X = 38;
    private const int REMOVE_WIDTH = 20;
    private const int REMOVE_RIGHT = 4;

    //the town import rows' selected color, so the lists read alike
    private static readonly Color SelectedColor = new(156, 96, 12, 180);

    private readonly UIImage IconImage;
    private readonly UILabel NameLabel;

    public WornRow(int width)
    {
        Width = width;
        Height = HEIGHT;

        IconImage = new UIImage
        {
            X = ICON_X,
            Y = (HEIGHT - ICON_SIZE) / 2,
            Width = ICON_SIZE,
            Height = ICON_SIZE,
            IsHitTestVisible = false
        };

        NameLabel = new UILabel
        {
            X = NAME_X,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = width - NAME_X - REMOVE_WIDTH - REMOVE_RIGHT - 4,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        var remove = new CustomButton("x", REMOVE_WIDTH)
        {
            X = width - REMOVE_WIDTH - REMOVE_RIGHT,
            Y = (HEIGHT - CustomButton.HEIGHT) / 2
        };
        remove.Clicked += () =>
        {
            if (Look is { } look)
                RemoveClicked?.Invoke(look);
        };

        AddChild(IconImage);
        AddChild(NameLabel);
        AddChild(remove);
    }

    /// <summary>The look this row shows, or null when the row is unused.</summary>
    public FittingRoomLook? Look { get; private set; }

    public bool Selected
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            BackgroundColor = value ? SelectedColor : null;
        }
    }

    /// <summary>Raised when the row (not its "x") is left-clicked.</summary>
    public event Action<FittingRoomLook>? Clicked;

    /// <summary>Raised when the row's "x" is clicked.</summary>
    public event Action<FittingRoomLook>? RemoveClicked;

    public override void Dispose()
    {
        //the icon is UiRenderer's shared cached texture: detach it so UIImage.Dispose does not free it
        IconImage.Texture = null;
        base.Dispose();
    }

    public override void OnClick(ClickEvent e)
    {
        if ((e.Button != MouseButton.Left) || Look is not { } look)
            return;

        Clicked?.Invoke(look);
        e.Handled = true;
    }

    /// <summary>Shows <paramref name="look" /> in <paramref name="color" />; null clears and hides the row.</summary>
    public void SetLook(FittingRoomLook? look, DisplayColor color)
    {
        Look = look;
        Visible = look is not null;

        if (look is null)
        {
            IconImage.Texture = null;
            NameLabel.Text = string.Empty;
            Selected = false;

            return;
        }

        IconImage.Texture = UiRenderer.Instance!.GetItemIcon(look.PanelSprite, color);
        NameLabel.Text = BankItemRow.FitToLabel(NameLabel, look.Name);
    }
}
