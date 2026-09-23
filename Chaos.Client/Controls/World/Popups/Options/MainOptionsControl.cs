#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Options;

/// <summary>
///     Options dialog using _noptdlg prefab. Positioned at bottom-left (X=2, Y=480-Height). Contains: Sound/Music volume
///     sliders (0-10 range), Friends/Macro/Setting/Chat buttons, ExitGame button, and Close button. Slider thumb from
///     option04.epf, tracks from SoundRect/MusicRect rects. The Chat button reads the prefab's Chat slot when the
///     data defines one, else a runtime-framed fallback below Close (panel-fit checked, never overlapping).
/// </summary>
public sealed class MainOptionsControl : PrefabPanel
{
    private readonly SliderControl MusicSlider;
    private readonly SliderControl SoundSlider;

    //slide animation
    private SlideAnimator Slide;

    public UIButton? ChatButton { get; }
    public UIButton? CloseButton { get; }
    public UIButton? ExitButton { get; }
    public UIButton? FriendsButton { get; }
    public UIButton? MacroButton { get; }
    public UIButton? SettingsButton { get; }

    public MainOptionsControl()
        : base("_noptdlg", false)
    {
        Name = "OptionsDialog";
        Visible = false;
        UsesControlStack = true;

        //right-aligned, slides in from right edge
        Slide.SetViewportBounds(
            new Rectangle(
                0,
                0,
                640,
                480),
            Width);
        X = Slide.OffScreenX;

        //slider controls — self-contained track + thumb with proper hit-testing
        var soundTrackRect = GetRect("SoundRect");
        var musicTrackRect = GetRect("MusicRect");

        //extract thumb texture from prefab, remove the dummy image child
        Texture2D? thumbTexture = null;
        var tickImage = CreateImage("Tick");

        if (tickImage is not null)
        {
            thumbTexture = tickImage.Texture;
            Children.Remove(tickImage);
        }

        SoundSlider = new SliderControl(soundTrackRect, thumbTexture);
        MusicSlider = new SliderControl(musicTrackRect, thumbTexture);

        SoundSlider.ValueChanged += volume => OnSoundVolumeChanged?.Invoke(volume);
        MusicSlider.ValueChanged += volume => OnMusicVolumeChanged?.Invoke(volume);

        AddChild(SoundSlider);
        AddChild(MusicSlider);

        //buttons
        MacroButton = CreateButton("Macro");
        SettingsButton = CreateButton("Setting");
        FriendsButton = CreateButton("Friends");
        ExitButton = CreateButton("ExitGame");
        CloseButton = CreateButton("CLOSE");

        if (MacroButton is not null)
            MacroButton.Clicked += () => OnMacro?.Invoke();

        if (SettingsButton is not null)
            SettingsButton.Clicked += () => OnSettings?.Invoke();

        if (FriendsButton is not null)
            FriendsButton.Clicked += () => OnFriends?.Invoke();

        if (ExitButton is not null)
            ExitButton.Clicked += () => OnExit?.Invoke();

        if (CloseButton is not null)
            CloseButton.Clicked += SlideClose;

        //Chat entry: the prefab slot when the data defines one, else the runtime fallback (null when no
        //safe rect exists — the entry is then absent, never misplaced).
        ChatButton = CreateButton("Chat") ?? BuildFallbackChatButton();

        if (ChatButton is not null)
            ChatButton.Clicked += () => OnChat?.Invoke();
    }

    private UIButton? BuildFallbackChatButton()
    {
        if (CloseButton is null)
            return null;

        var candidate = new Rectangle(
            CloseButton.X,
            CloseButton.Y + CloseButton.Height + 4,
            CloseButton.Width,
            CloseButton.Height);

        if ((candidate.Bottom > Height) || OverlapsAnyButton(candidate))
            return null;

        using var frame = DialogFrame.Composite(
            new SKColor(10, 8, 5, 255),
            candidate.Width,
            candidate.Height);

        if (frame is null)
            return null;

        var button = new UIButton
        {
            Name = "Chat",
            X = candidate.X,
            Y = candidate.Y,
            Width = candidate.Width,
            Height = candidate.Height,
            NormalTexture = TextureConverter.ToTexture2D(frame)
        };

        //sibling caption (drawn over the blank frame); hit-test invisible so clicks reach the button.
        var captionWidth = TextRenderer.MeasureWidth("Chat");
        AddChild(button);
        AddChild(
            new UILabel
            {
                Name = "ChatCaption",
                X = candidate.X + ((candidate.Width - captionWidth) / 2),
                Y = candidate.Y + ((candidate.Height - TextRenderer.CHAR_HEIGHT) / 2),
                Width = captionWidth,
                Height = TextRenderer.CHAR_HEIGHT,
                PaddingLeft = 0,
                PaddingRight = 0,
                PaddingTop = 0,
                PaddingBottom = 0,
                TruncateWithEllipsis = false,
                ForegroundColor = Color.White,
                Text = "Chat",
                IsHitTestVisible = false
            });

        return button;
    }

    private bool OverlapsAnyButton(Rectangle candidate)
        => Overlaps(candidate, MacroButton)
            || Overlaps(candidate, SettingsButton)
            || Overlaps(candidate, FriendsButton)
            || Overlaps(candidate, ExitButton)
            || Overlaps(candidate, CloseButton);

    private static bool Overlaps(Rectangle candidate, UIButton? button)
        => button is not null
            && candidate.Intersects(new Rectangle(button.X, button.Y, button.Width, button.Height));

    public int GetMusicVolume() => MusicSlider.Value;

    public int GetSoundVolume() => SoundSlider.Value;

    public override void Hide()
    {
        InputDispatcher.Instance?.RemoveControl(this);
        Slide.Hide(this);
    }

    public event ChatHandler? OnChat;
    public event CloseHandler? OnClose;
    public event ExitHandler? OnExit;
    public event FriendsHandler? OnFriends;
    public event MacroHandler? OnMacro;
    public event MusicVolumeChangedHandler? OnMusicVolumeChanged;
    public event SettingsHandler? OnSettings;
    public event SoundVolumeChangedHandler? OnSoundVolumeChanged;
    public void SetMusicVolume(int volume) => MusicSlider.SetValue(volume);

    public void SetSoundVolume(int volume) => SoundSlider.SetValue(volume);

    public void SetViewportBounds(Rectangle viewport)
    {
        Slide.SetViewportBounds(viewport, Width);
        Y = viewport.Y;
    }

    public override void Show()
    {
        if (!Visible)
        {
            InputDispatcher.Instance?.PushControl(this);
            Slide.SlideIn(this);
        }
    }

    public void SlideClose()
    {
        InputDispatcher.Instance?.RemoveControl(this);
        Slide.SlideOut();
    }

    public override void Update(GameTime gameTime)
    {
        if (!Visible || !Enabled)
            return;

        if (Slide.Update(gameTime, this))
        {
            OnClose?.Invoke();

            return;
        }

        base.Update(gameTime);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (Slide.Sliding)
            return;

        if (e.Keycode == Keycode.Escape)
        {
            InputDispatcher.Instance?.RemoveControl(this);
            Slide.SlideOut();
            e.Handled = true;
        } else if (e.Keycode == Keycode.X)
        {
            OnExit?.Invoke();
            e.Handled = true;
        }
    }

}