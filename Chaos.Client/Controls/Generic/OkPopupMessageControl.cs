#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Data;
using Chaos.Client.Extensions;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.Generic;

/// <summary>
///     A popup message dialog with DlgBack2.spf tiled background (clipped to interior height), dlgframe.epf 16×16 border,
///     and butt001.epf OK (+ optional Cancel) button, or framed Yes and No buttons for a question.
/// </summary>
public sealed class OkPopupMessageControl : UIPanel
{
    private const int TILES_WIDE = 4;
    private const int INTERIOR_HEIGHT = 54;
    private const int CONTENT_PADDING = 6;
    private const int BUTTON_MARGIN = 1;

    //butt001.epf frame indices — 2 frames per button (normal/pressed)
    private const int OK_NORMAL = 15;
    private const int OK_PRESSED = 16;
    private const int CANCEL_NORMAL = 21;
    private const int CANCEL_PRESSED = 22;

    private readonly int ContentHeight;
    private readonly int ContentWidth;
    private readonly int ContentX;
    private readonly int ContentY;

    private readonly UILabel MessageLabel;

    //what Enter/Space and Escape press: the OK (or Yes) button, and the Cancel (or No) button when there is one
    private readonly Action PressOk;
    private readonly Action? PressCancel;

    /// <param name="extraLines">Text rows to add above the default height, for messages longer than three lines.</param>
    /// <param name="yesNo">Yes and No buttons instead of OK and Cancel, for questions; Yes raises OnOk and No OnCancel.</param>
    public OkPopupMessageControl(bool showCancel = false, int extraLines = 0, bool yesNo = false)
    {
        Visible = false;
        UsesControlStack = true;

        //load background tile
        using var bgTile = DataContext.UserControls.GetSpfImage("DlgBack2.spf");

        if (bgTile is null)
            throw new InvalidOperationException("Failed to load DlgBack2.spf");

        var borderSize = DialogFrame.BORDER_SIZE;
        var interiorWidth = bgTile.Width * TILES_WIDE - 23;
        var totalWidth = borderSize + interiorWidth + borderSize;
        var interiorHeight = INTERIOR_HEIGHT + extraLines * TextRenderer.CHAR_HEIGHT;
        var totalHeight = borderSize + interiorHeight + borderSize;

        //composite tiled background with border
        using var composite = DialogFrame.Composite(bgTile, totalWidth, totalHeight);

        if (composite is null)
            throw new InvalidOperationException("Failed to composite dialog background");

        Width = totalWidth;
        Height = totalHeight;
        this.CenterOnScreen();
        Background = TextureConverter.ToTexture2D(composite);

        //content area — text fills above the button row
        ContentX = borderSize + CONTENT_PADDING;
        ContentY = borderSize + CONTENT_PADDING;
        ContentWidth = interiorWidth - CONTENT_PADDING * 2;
        ContentHeight = interiorHeight - CONTENT_PADDING * 2;

        //button textures
        var cache = UiRenderer.Instance!;
        var okNormalTex = cache.GetEpfTexture("butt001.epf", OK_NORMAL);
        var okPressedTex = cache.GetEpfTexture("butt001.epf", OK_PRESSED);

        var okWidth = okNormalTex.Width;
        var okHeight = okNormalTex.Height;
        var buttonY = totalHeight - borderSize - okHeight - BUTTON_MARGIN + 5;
        var rightButtonX = totalWidth - borderSize - BUTTON_MARGIN + 4;

        //butt001.epf has no Yes or No faces, so a question gets framed text buttons the same width, set in the same place
        if (yesNo)
        {
            var noButton = new CustomButton("No", okWidth)
            {
                Name = "No",
                X = rightButtonX - okWidth,
                Y = buttonY + ((okHeight - CustomButton.HEIGHT) / 2)
            };
            noButton.Clicked += () => OnCancel?.Invoke();
            AddChild(noButton);

            var yesButton = new CustomButton("Yes", okWidth)
            {
                Name = "Yes",
                X = noButton.X - okWidth - BUTTON_MARGIN - 4,
                Y = noButton.Y
            };
            yesButton.Clicked += () => OnOk?.Invoke();
            AddChild(yesButton);

            PressOk = () => OnOk?.Invoke();
            PressCancel = () => OnCancel?.Invoke();
        } else
        {
            int okX;

            //cancel button (optional) — positioned on the right
            if (showCancel)
            {
                var cancelNormalTex = cache.GetEpfTexture("butt001.epf", CANCEL_NORMAL);
                var cancelPressedTex = cache.GetEpfTexture("butt001.epf", CANCEL_PRESSED);

                var cancelWidth = cancelNormalTex.Width;

                var cancelButton = new UIButton
                {
                    Name = "Cancel",
                    X = rightButtonX - cancelWidth,
                    Y = buttonY,
                    Width = cancelWidth,
                    Height = cancelNormalTex.Height,
                    NormalTexture = cancelNormalTex,
                    PressedTexture = cancelPressedTex
                };
                cancelButton.Clicked += () => OnCancel?.Invoke();
                AddChild(cancelButton);

                okX = cancelButton.X - okWidth - BUTTON_MARGIN;
                PressCancel = cancelButton.PerformClick;
            } else
                okX = rightButtonX - okWidth;

            //ok button — left of cancel, or slides right when cancel absent
            var okButton = new UIButton
            {
                Name = "OK",
                X = okX,
                Y = buttonY,
                Width = okWidth,
                Height = okHeight,
                NormalTexture = okNormalTex,
                PressedTexture = okPressedTex
            };
            okButton.Clicked += () => OnOk?.Invoke();
            AddChild(okButton);

            PressOk = okButton.PerformClick;
        }

        MessageLabel = new UILabel
        {
            X = ContentX - 9,
            Y = ContentY - 10,
            Width = ContentWidth,
            Height = ContentHeight,
            WordWrap = true,
            ForegroundColor = Color.White,
            VerticalAlignment = VerticalAlignment.Top
        };
        AddChild(MessageLabel);
    }

    public void Hide()
    {
        InputDispatcher.Instance!.RemoveControl(this);
        Visible = false;
        MessageLabel.Text = string.Empty;
    }

    public event CancelHandler? OnCancel;

    public event OkHandler? OnOk;

    public void Show(string message)
    {
        MessageLabel.Text = message;
        InputDispatcher.Instance!.PushControl(this);
        Visible = true;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode is Keycode.Enter or Keycode.Space)
        {
            PressOk();
            e.Handled = true;
        } else if (e.Keycode == Keycode.Escape)
        {
            (PressCancel ?? PressOk)();
            e.Handled = true;
        }
    }
}