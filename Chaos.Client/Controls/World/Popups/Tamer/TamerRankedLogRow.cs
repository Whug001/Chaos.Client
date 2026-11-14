#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     One row of the Ranked tab's defence log: the line <see cref="TamerRankedText.LogRow" /> answers, with a gold
///     border when it is the selected entry. Rows are recycled by the hosting
///     <see cref="Controls.Scrolling.VirtualizedRowList{T}" />, so <see cref="Bind" /> stamps everything the click
///     handler reads; <see cref="Clicked" /> fires on a left click.
/// </summary>
public sealed class TamerRankedLogRow : UIPanel
{
    public const int HEIGHT = TamerRankedLayout.LOG_ROW_HEIGHT;

    private readonly UILabel TextLabel;

    public TamerRankedLogRow(int width)
    {
        Width = width;
        Height = HEIGHT;

        TextLabel = new UILabel
        {
            X = 0,
            Y = (HEIGHT - TamerRankedLayout.TEXT_HEIGHT) / 2,
            Width = width,
            Height = TamerRankedLayout.TEXT_HEIGHT,
            ForegroundColor = LegendColors.White,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(TextLabel);
    }

    /// <summary>The log entry this row is bound to; empty until the first bind.</summary>
    public Guid EntryId { get; private set; } = Guid.Empty;

    public bool Selected { get; private set; }

    public event Action<TamerRankedLogRow>? Clicked;

    /// <summary>Stamps the row from a log entry. Selection comes from the state's current pick.</summary>
    public void Bind(TamerRankedState state, TamerRankedLogView entry)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(entry);

        EntryId = entry.Id;
        Selected = entry.Id == state.SelectedEntry?.Id;
        TextLabel.Text = TamerRankedText.LogRow(entry);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        if (Selected)
            DrawBorder(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), LegendColors.Gold);

        foreach (var child in Children)
            if (child.Visible)
                child.Draw(spriteBatch);
    }

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke(this);
        e.Handled = true;
    }
}
