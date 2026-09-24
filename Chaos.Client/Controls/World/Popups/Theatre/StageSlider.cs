#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     A thin slider for the Stage Lighting window, over any integer range. Raises <see cref="ValueChanged" /> while
///     dragging and <see cref="DragEnded" /> on release.
/// </summary>
public sealed class StageSlider : UIElement
{
    private const int HANDLE_WIDTH = 6;
    private const int TRACK_HEIGHT = 3;

    private static readonly Color Track = new(68, 68, 68);

    public StageSlider(int min, int max)
    {
        Min = min;
        Max = max;
        Value = min;
        Height = 12;
    }

    public int Min { get; }
    public int Max { get; }
    public int Value { get; private set; }

    /// <summary>True while the handle is held. The window doesn't overwrite a held slider from the server.</summary>
    public bool IsDragging { get; private set; }

    public event Action<int>? ValueChanged;
    public event Action? DragEnded;

    public void SetValue(int value) => Value = Math.Clamp(value, Min, Max);

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + ((Height - TRACK_HEIGHT) / 2), Width, TRACK_HEIGHT), Track);

        var usable = Width - HANDLE_WIDTH;
        var offset = Max == Min ? 0 : (int)MathF.Round(usable * (Value - Min) / (float)(Max - Min));

        DrawRectClipped(spriteBatch, new Rectangle(ScreenX + offset, ScreenY, HANDLE_WIDTH, Height), Enabled ? LegendColors.Silver : LegendColors.Gray);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if ((e.Button != MouseButton.Left) || !Enabled)
            return;

        IsDragging = true;
        SetFromMouse(e.ScreenX);
        e.Handled = true;
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!IsDragging)
            return;

        SetFromMouse(e.ScreenX);
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if ((e.Button != MouseButton.Left) || !IsDragging)
            return;

        IsDragging = false;
        DragEnded?.Invoke();
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        if (IsDragging)
        {
            IsDragging = false;
            DragEnded?.Invoke();
        }
    }

    private void SetFromMouse(int screenX)
    {
        var usable = Math.Max(1, Width - HANDLE_WIDTH);
        var ratio = Math.Clamp((screenX - ScreenX - (HANDLE_WIDTH / 2f)) / usable, 0f, 1f);
        var value = Min + (int)MathF.Round(ratio * (Max - Min));

        if (value == Value)
            return;

        Value = value;
        ValueChanged?.Invoke(value);
    }
}
