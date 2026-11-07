#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.QuestLog;

/// <summary>
///     One row in the quest log's list: an area heading in gold across the full width, or a quest title indented
///     beneath its heading. Headings are never clickable or selectable. Rows are recycled by the list, so
///     <see cref="Bind" /> refreshes the key the click handler reads.
/// </summary>
public sealed class QuestLogRow : UIPanel
{
    private const int TEXT_HEIGHT = 12;
    private const int HEADING_X = 2;
    private const int QUEST_X = 8;

    private static readonly Color SelectedColor = new(156, 96, 12, 180);

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
            X = HEADING_X,
            Y = 0,
            Width = width - HEADING_X,
            Height = TEXT_HEIGHT,
            PaddingLeft = 0,
            ForegroundColor = Color.White,
            IsHitTestVisible = false
        };

        AddChild(TitleLabel);
    }

    public void Bind(QuestLogState.QuestLogListRow row, bool selected)
    {
        if (row.IsHeading)
        {
            Key = null;
            TitleLabel.X = HEADING_X;
            TitleLabel.Width = Width - HEADING_X;
            TitleLabel.Text = row.Text;

            //read here at bind time (after LegendColors.Initialize at startup), not in a static field
            TitleLabel.ForegroundColor = LegendColors.Gold;
            BackgroundColor = null;
        } else
        {
            Key = row.Entry!.Key;
            TitleLabel.X = QUEST_X;
            TitleLabel.Width = Width - QUEST_X;
            TitleLabel.Text = row.Text;
            TitleLabel.ForegroundColor = Color.White;
            BackgroundColor = selected ? SelectedColor : null;
        }

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
