#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Scrolling;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.LobbyLogin;

public sealed class LoginNoticeControl : PrefabPanel
{
    //butt001.epf frames, the same faces OkPopupMessageControl uses
    private const int OK_NORMAL = 15;
    private const int OK_PRESSED = 16;
    private const int CANCEL_NORMAL = 21;
    private const int CANCEL_PRESSED = 22;
    private readonly UILabel? AgreementTextLabel;
    public UIButton? CancelButton { get; }

    public UIButton? OkButton { get; }

    public LoginNoticeControl()
        : base(LoginLayouts.For("_nagree"))
    {
        Name = "AgreePanel";
        Visible = false;
        UsesControlStack = true;

        //buttons — _nagree is one of the few prefabs with actual type 3 controls
        OkButton = CreateButton("OK");
        CancelButton = CreateButton("CANCEL");

        //the retail agreement art (lagre) has blank OK and CANCEL slots
        UseFallbackFace(OkButton, OK_NORMAL, OK_PRESSED);
        UseFallbackFace(CancelButton, CANCEL_NORMAL, CANCEL_PRESSED);

        if (OkButton is not null)
            OkButton.Clicked += () => OnOk?.Invoke();

        if (CancelButton is not null)
            CancelButton.Clicked += () => OnCancel?.Invoke();

        //agreement text display region — type 7, 0 images
        var textRect = GetRect("AGREEMENTTEXT");

        if (textRect != Rectangle.Empty)
        {
            AgreementTextLabel = new UILabel
            {
                Name = "AgreementText",
                Width = textRect.Width,
                Height = textRect.Height,
                PaddingLeft = 0,
                PaddingTop = 0,
                WordWrap = true,
                VerticalAlignment = VerticalAlignment.Top
            };

            //the prefab leaves a 9px gap between the text and the scrollbar column (the old label was inset
            //25px from the right while the viewer's bar gutter is 16px); ContentRightPadding reproduces it.
            var viewer = new ScrollViewerControl(AgreementTextLabel)
            {
                Name = "AgreementScroll",
                X = textRect.X,
                Y = textRect.Y,
                Width = textRect.Width,
                Height = textRect.Height,
                ContentRightPadding = 9
            };

            AddChild(viewer);
        }
    }

    private static void UseFallbackFace(UIButton? button, int normalFrame, int pressedFrame)
    {
        if (button is null || button.NormalTexture is not null)
            return;

        var cache = UiRenderer.Instance!;
        var normal = cache.GetEpfTexture("butt001.epf", normalFrame);
        var pressed = cache.GetEpfTexture("butt001.epf", pressedFrame);

        button.X += (button.Width - normal.Width) / 2;
        button.Y += (button.Height - normal.Height) / 2;
        button.Width = normal.Width;
        button.Height = normal.Height;
        button.NormalTexture = normal;
        button.PressedTexture = pressed;
    }

    public event CancelHandler? OnCancel;

    public event OkHandler? OnOk;

    public void Show(string agreementText)
    {
        if (AgreementTextLabel is not null)
        {
            AgreementTextLabel.ForegroundColor = TextColors.Default;
            AgreementTextLabel.ScrollOffset = 0;
            AgreementTextLabel.Text = agreementText;
        }

        base.Show();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        switch (e.Keycode)
        {
            case Keycode.Enter:
                OkButton?.PerformClick();
                e.Handled = true;

                break;

            case Keycode.Escape:
                //intentionally blocked — escape is a no-op during eula display
                e.Handled = true;

                break;
        }
    }
}