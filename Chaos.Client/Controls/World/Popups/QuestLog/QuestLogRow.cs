#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.QuestLog;

/// <summary>
///     One row in the quest log's list: the title on the left and the area on the right in grey. Rows are recycled by
///     the list, so <see cref="Bind" /> refreshes the key the click handler reads.
/// </summary>
public sealed class QuestLogRow : UIPanel
{
    private const int TEXT_HEIGHT = 12;
    private const int AREA_WIDTH = 70;
    private const int GAP = 4;

    private static readonly Color SelectedColor = new(156, 96, 12, 180);
    private static readonly Color SelectedAreaColor = new(255, 230, 186);

    private readonly UILabel AreaLabel;
    private readonly Action<string> OnSelect;
    private readonly UILabel TitleLabel;
    private string? Key;

    public QuestLogRow(int width, Action<string> onSelect)
    {
        OnSelect = onSelect;
        Width = width;
        Height = TEXT_HEIGHT;

        TitleLabel = new UILabel
        {
            X = 2,
            Y = 0,
            Width = width - AREA_WIDTH - GAP - 2,
            Height = TEXT_HEIGHT,
            PaddingLeft = 0,
            ForegroundColor = Color.White,
            IsHitTestVisible = false
        };

        AddChild(TitleLabel);

        AreaLabel = new UILabel
        {
            X = width - AREA_WIDTH - 2,
            Y = 0,
            Width = AREA_WIDTH,
            Height = TEXT_HEIGHT,
            PaddingLeft = 0,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = Color.Gray,
            IsHitTestVisible = false
        };

        AddChild(AreaLabel);
    }

    public void Bind(QuestLogEntryInfo entry, bool selected)
    {
        Key = entry.Key;
        TitleLabel.Text = entry.Title;
        AreaLabel.Text = entry.Area;
        BackgroundColor = selected ? SelectedColor : null;
        AreaLabel.ForegroundColor = selected ? SelectedAreaColor : Color.Gray;
        Visible = true;
    }

    public override void OnClick(ClickEvent e)
    {
        if ((e.Button == MouseButton.Left) && Key is not null)
        {
            OnSelect(Key);
            e.Handled = true;
        }
    }
}
