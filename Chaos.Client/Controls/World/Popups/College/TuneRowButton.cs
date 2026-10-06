#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     A gallery Music row's small Play / Wait / Stop button. It fits inside a list row, which is shorter than a
///     <c>CustomButton</c>. It sits over the row, so its clicks never select or open the row.
/// </summary>
public sealed class TuneRowButton : UIPanel
{
    public const int WIDTH = 40;
    public const int HEIGHT = CollegeListRow.HEIGHT - 2;

    private readonly UILabel Label;

    public TuneRowButton()
    {
        Width = WIDTH;
        Height = HEIGHT;
        BackgroundColor = new Color(10, 8, 5);
        BorderColor = new Color(138, 116, 72);

        Label = new UILabel
        {
            X = 0,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "Play"
        };

        AddChild(Label);
    }

    public int Id { get; set; }

    public string Caption { get => Label.Text; set => Label.Text = value; }

    public event Action<int>? Pressed;

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Pressed?.Invoke(Id);
        e.Handled = true;
    }

    public override void OnDoubleClick(DoubleClickEvent e) => e.Handled = true;
}
