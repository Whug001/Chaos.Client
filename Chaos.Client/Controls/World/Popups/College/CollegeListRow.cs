#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>One row of a College list: up to four columns at fixed x offsets; click selects, double-click opens.</summary>
public sealed class CollegeListRow : UIPanel
{
    public const int HEIGHT = TextRenderer.CHAR_HEIGHT + 4;
    private const int TEXT_INSET = 4;

    //the ballot and import windows' selected-row colour, so the lists read alike
    private static readonly Color SelectedColor = new(156, 96, 12, 180);

    private readonly UILabel[] Columns;

    public CollegeListRow(int width, IReadOnlyList<int> columnX)
    {
        Width = width;
        Height = HEIGHT;
        Columns = new UILabel[columnX.Count];

        for (var i = 0; i < columnX.Count; i++)
        {
            var right = i + 1 < columnX.Count ? columnX[i + 1] : width;

            Columns[i] = new UILabel
            {
                X = columnX[i] + TEXT_INSET,
                Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
                Width = right - columnX[i] - (2 * TEXT_INSET),
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false
            };

            AddChild(Columns[i]);
        }
    }

    public int Id { get; private set; }

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

    public event Action? Clicked;
    public event Action? DoubleClicked;

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        if (e.Button == MouseButton.Left)
            DoubleClicked?.Invoke();

        e.Handled = true;
    }

    /// <summary>Shows a row: its id and each column's text and colour. Columns not given are left blank.</summary>
    public void Set(int id, params (string Text, Color Color)[] columns)
    {
        Id = id;

        for (var i = 0; i < Columns.Length; i++)
        {
            Columns[i].Text = i < columns.Length ? columns[i].Text : string.Empty;
            Columns[i].ForegroundColor = i < columns.Length ? columns[i].Color : LegendColors.White;
        }
    }
}
