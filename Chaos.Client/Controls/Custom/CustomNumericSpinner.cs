#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.Custom;

/// <summary>
///     A compact numeric stepper framed in dlgframe.epf (same border source as <see cref="CustomTextBox" />) with stacked
///     up/down arrow buttons baked into a right-hand column. The value is clamped to [<see cref="Min" />,
///     <see cref="Max" />]; clicking (or holding) an arrow steps by one, and the user can type digits directly — printable
///     non-digits are rejected and the typed value is parsed + clamped when the field loses focus or Enter is pressed.
///     When <see cref="Max" /> ≤ <see cref="Min" /> there is nothing to choose, so the spinner renders dimmed and ignores
///     input (it still shows the fixed value).
/// </summary>
/// <remarks>
///     The width is fixed at construction (the framed texture is pre-built for it); use <see cref="MeasureRequiredWidth" />
///     to size it for an expected maximum. A digits-only <see cref="UITextBox" /> child fills the area left of the arrow
///     column and owns text entry/focus; the panel itself owns the arrow column (hit-zone + hold-to-repeat) and the frame.
/// </remarks>
public sealed class CustomNumericSpinner : UIPanel
{
    private const int INNER_PAD = 5;               // text inset, matches CustomTextBox/CustomComboBox
    private const int ARROW_BOX = 11;              // right-hand arrow column width
    private const float REPEAT_INITIAL_MS = 450f;  // pause after the first step before auto-repeat begins
    private const float REPEAT_INTERVAL_MS = 130f; // cadence once auto-repeat has begun

    private static readonly SKColor FillColor = new(10, 8, 5, 255);

    private readonly UITextBox Field;

    private Texture2D? FrameTex;
    private Texture2D? FrameDimTex;

    private int ActiveStep; // +1 / -1 while an arrow is held, else 0
    private float RepeatTimer;
    private bool RepeatStarted; // true once the initial hold delay has elapsed and auto-repeat is running
    private bool WasFieldFocused;
    private int CurrentValue = 1;

    public CustomNumericSpinner(int width)
    {
        Width = width;
        Height = TextRenderer.CHAR_HEIGHT + INNER_PAD * 2;

        Field = new NumericTextBox
        {
            X = INNER_PAD,
            Y = (Height - TextRenderer.CHAR_HEIGHT) / 2,
            Width = width - ARROW_BOX - INNER_PAD,
            Height = TextRenderer.CHAR_HEIGHT,
            PaddingLeft = 0,
            PaddingTop = 0,
            PaddingRight = 0,
            PaddingBottom = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = TextColors.Default,
            MaxLength = 1,
            Text = "1"
        };
        AddChild(Field);

        RebuildTextures();
        Background = FrameTex;
        SyncField(); //prime IsReadOnly/text for the initial (Min==Max) disabled state
    }

    public int Min { get; private set; } = 1;
    public int Max { get; private set; } = 1;

    public int Value
    {
        get => CurrentValue;
        set => SetValue(value, true);
    }

    //interactive only when there's an actual range to pick from; otherwise the value is fixed and the spinner dims.
    private bool Interactive => Enabled && (Max > Min);

    public event Action<int>? ValueChanged;

    /// <summary>Width needed to show <paramref name="maxValue" />'s digits plus the text inset and the arrow column.</summary>
    public static int MeasureRequiredWidth(int maxValue)
    {
        var digits = Math.Max(1, maxValue.ToString().Length);

        return TextRenderer.MeasureWidth(new string('8', digits)) + INNER_PAD * 2 + ARROW_BOX + 2;
    }

    /// <summary>
    ///     Applies any typed-but-uncommitted text immediately (parse + clamp, raising <see cref="ValueChanged" /> if it
    ///     changes) and releases keyboard focus from the field. Call before reading <see cref="Value" /> from a button
    ///     outside the spinner — typed input otherwise commits only when the field loses focus or Enter is pressed.
    /// </summary>
    public void Commit()
    {
        OnFieldCommit();

        //release focus so a popup opened right after (e.g. the buy confirmation) doesn't stash this field as the
        //pre-popup focus and restore focus to it when it closes. The dispatcher's explicit-focus pointer is NOT cleared
        //by blurring the textbox alone, so clear it directly whenever it still targets our field.
        if (Field.IsFocused)
            Field.IsFocused = false;

        var dispatcher = InputDispatcher.Instance;

        if (dispatcher?.ExplicitFocus == Field)
            dispatcher.ClearExplicitFocus();
    }

    /// <summary>Sets the valid range and resets the value to <paramref name="min" /> (does not raise <see cref="ValueChanged" />).</summary>
    public void SetRange(int min, int max)
    {
        Min = min;
        Max = Math.Max(max, min);
        Field.MaxLength = Math.Max(1, Max.ToString().Length);
        SetValue(Min, false);
    }

    private void SetValue(int value, bool notify)
    {
        var clamped = Math.Clamp(value, Min, Max);

        if (clamped == CurrentValue)
        {
            SyncField(); //value unchanged but normalize the displayed text + read-only state

            return;
        }

        CurrentValue = clamped;
        SyncField();

        if (notify)
            ValueChanged?.Invoke(CurrentValue);
    }

    private void SyncField()
    {
        var text = CurrentValue.ToString();

        if (Field.Text != text)
            Field.Text = text;

        Field.IsReadOnly = !Interactive;
    }

    private void OnFieldCommit()
    {
        if (int.TryParse(Field.Text, out var typed))
            SetValue(typed, true);
        else
            SetValue(Min, true); //empty/invalid → floor
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        //commit typed input when the field loses focus (Enter blurs it via NumericTextBox).
        if (WasFieldFocused && !Field.IsFocused)
            OnFieldCommit();

        WasFieldFocused = Field.IsFocused;

        if (ActiveStep == 0)
            return;

        //an arrow is being held; stop cleanly if the spinner became non-interactive under it.
        if (!Interactive)
        {
            ActiveStep = 0;
            RepeatTimer = 0;
            RepeatStarted = false;

            return;
        }

        //a single press only does the one immediate step (on mousedown); auto-repeat begins after a longer initial hold
        //delay and then ticks at a slower interval, so a normal click never produces extra steps.
        RepeatTimer += (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        var threshold = RepeatStarted ? REPEAT_INTERVAL_MS : REPEAT_INITIAL_MS;

        if (RepeatTimer >= threshold)
        {
            RepeatTimer -= threshold;
            RepeatStarted = true;
            SetValue(CurrentValue + ActiveStep, true);
        }
    }

    //reset transient interaction state when the spinner is hidden/reset, so a held arrow can't resume auto-repeat after
    //the panel reappears, and the focus-loss poll doesn't fire a spurious commit on hide.
    public override void ResetInteractionState()
    {
        base.ResetInteractionState(); //blurs the child field
        ActiveStep = 0;
        RepeatTimer = 0;
        RepeatStarted = false;
        WasFieldFocused = false;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        //clicks on the text area hit the field child; only the arrow column reaches the panel.
        if ((e.Button != MouseButton.Left) || !Interactive)
            return;

        if ((e.ScreenX - ScreenX) < (Width - ARROW_BOX))
            return;

        var step = (e.ScreenY - ScreenY) < (Height / 2) ? 1 : -1;
        ActiveStep = step;
        RepeatTimer = 0;
        RepeatStarted = false;
        SetValue(CurrentValue + step, true);
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        ActiveStep = 0;
        RepeatTimer = 0;
        RepeatStarted = false;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        var bg = Interactive ? FrameTex : FrameDimTex;

        if (Background != bg)
            Background = bg;

        var color = Interactive ? TextColors.Default : Dim(TextColors.Default);

        if (Field.ForegroundColor != color)
            Field.ForegroundColor = color;

        base.Draw(spriteBatch);
    }

    public override void Dispose()
    {
        FrameTex?.Dispose();
        FrameDimTex?.Dispose();
        FrameTex = null;
        FrameDimTex = null;
        Background = null; //detach so the base doesn't double-dispose a frame texture
        base.Dispose();
    }

    private void RebuildTextures()
    {
        FrameTex?.Dispose();
        FrameDimTex?.Dispose();
        FrameTex = BuildFrame(Width, Height, false);
        FrameDimTex = BuildFrame(Width, Height, true);
    }

    private static Color Dim(Color c) => new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2), c.A);

    private static Texture2D BuildFrame(int w, int h, bool dim)
    {
        using var frame = DialogFrame.Composite(FillColor, w, h);

        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        if (frame is not null)
            canvas.DrawImage(frame, 0, 0);
        else
            canvas.Clear(FillColor);

        //bake the stacked up/down arrows into the right-hand column (up in the top half, down in the bottom), nudged 2px
        //left and 2px toward the vertical center for a tighter look.
        const int dx = 2;
        const int dy = 2;
        var ax = w - ARROW_BOX - dx;
        DrawArrow(canvas, ax, dy, ARROW_BOX, h / 2, true);
        DrawArrow(canvas, ax, h / 2 - dy, ARROW_BOX, h - h / 2, false);

        using var snapshot = surface.Snapshot();

        if (!dim)
            return TextureConverter.ToTexture2D(snapshot);

        //50%-brightness variant via an RGB-scaling color matrix (alpha row left at 1), same as CustomComboBox.
        using var dimSurface = SKSurface.Create(info);
        using var dimPaint = new SKPaint();

        //@formatter:off
        dimPaint.ColorFilter = SKColorFilter.CreateColorMatrix([
                                                                   0.5f, 0f, 0f, 0f, 0f,
                                                                   0f, 0.5f, 0f, 0f, 0f,
                                                                   0f, 0f, 0.5f, 0f, 0f,
                                                                   0f, 0f, 0f, 1f, 0f
                                                               ]);
        //@formatter:on

        dimSurface.Canvas.DrawImage(snapshot, 0, 0, dimPaint);

        using var dimSnapshot = dimSurface.Snapshot();

        return TextureConverter.ToTexture2D(dimSnapshot);
    }

    private static void DrawArrow(SKCanvas canvas, int x, int y, int w, int h, bool up)
    {
        var arrow = TextColors.Default;

        using var paint = new SKPaint
        {
            Color = new SKColor(arrow.R, arrow.G, arrow.B),
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        var cx = x + w / 2f;
        var cy = y + h / 2f;
        const float R = 2.5f;

        using var path = new SKPath();

        if (up)
        {
            path.MoveTo(cx - R, cy + R);
            path.LineTo(cx + R, cy + R);
            path.LineTo(cx, cy - R);
        } else
        {
            path.MoveTo(cx - R, cy - R);
            path.LineTo(cx + R, cy - R);
            path.LineTo(cx, cy + R);
        }

        path.Close();
        canvas.DrawPath(path, paint);
    }
}

/// <summary>A single-line text box that accepts only digit characters and blurs on Enter (the spinner commits on blur).</summary>
file sealed class NumericTextBox : UITextBox
{
    public override void OnTextInput(TextInputEvent e)
    {
        var c = e.Character;

        //reject any printable non-digit; let control chars (backspace etc.) reach the base for normal handling.
        if (!char.IsControl(c) && !char.IsDigit(c))
        {
            e.Handled = true;

            return;
        }

        base.OnTextInput(e);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if ((e.Key == Keys.Enter) && IsFocused)
        {
            IsFocused = false; //blur → the spinner's focus-loss poll commits the typed value
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        //a read-only field means the spinner is disabled; don't take keyboard focus into a dead field on click.
        if (IsReadOnly)
        {
            e.Handled = true;

            return;
        }

        base.OnMouseDown(e);
    }
}
