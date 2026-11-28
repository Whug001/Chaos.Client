#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Hud;

/// <summary>
///     The pinned-quest tracker: a small overlay at the top right of the viewport showing the pinned quest's title
///     (gold, like the quest log), its current stage text wrapped and capped at 3 lines, and its progress lines.
///     Clicking the title opens the quest log; the X unpins. It is drawn under popups and over the map. The panel
///     is exactly its content bounds and the input dispatcher only routes in-bounds clicks, so clicks outside its
///     bounds fall through to the map underneath.
/// </summary>
public sealed class QuestTrackerControl : UIPanel
{
    private const int PADDING = 4;
    private const int X_SIZE = 18;
    private const int TITLE_GAP = 2;

    private readonly List<UIElement> Body = [];
    private readonly UIButton UnpinButton;

    public QuestTrackerControl()
    {
        Name = "QuestTracker";
        Width = QuestTrackerLayout.WIDTH;
        BackgroundColor = new Color(8, 6, 4, 200);

        var ui = UiRenderer.Instance!;
        UnpinButton = new UIButton
        {
            X = QuestTrackerLayout.WIDTH - PADDING - X_SIZE,
            Y = PADDING,
            Width = X_SIZE,
            Height = X_SIZE,
            NormalTexture = ui.GetSpfTexture("x_bn.spf"),
            PressedTexture = ui.GetSpfTexture("x_bn.spf", 1)
        };
        UnpinButton.Clicked += () => UnpinClicked?.Invoke();
        AddChild(UnpinButton);

        WorldState.QuestWatch.Changed += Refresh;
        Refresh();
    }

    /// <summary>The X button: unpins the quest.</summary>
    public event Action? UnpinClicked;

    /// <summary>The title: opens the quest log.</summary>
    public event Action? TitleClicked;

    /// <summary>Moves the tracker to <see cref="QuestTrackerLayout.Position" /> for the given viewport.</summary>
    public void Anchor(Rectangle viewport, int rightReserved)
    {
        var pos = QuestTrackerLayout.Position(viewport, rightReserved);
        X = pos.X;
        Y = pos.Y;
    }

    /// <summary>Rebuilds the content from the pinned quest and updates visibility.</summary>
    public void Refresh()
    {
        var pinned = ClientSettings.ShowQuestTracker ? WorldState.QuestWatch.Pinned : null;

        if (pinned is null)
        {
            Visible = false;

            return;
        }

        Visible = true;

        //read here at refresh time (after LegendColors.Initialize at startup), not in a static field
        BorderColor = LegendColors.Gold * 0.6f;
        Rebuild(pinned);
    }

    public override void Dispose()
    {
        WorldState.QuestWatch.Changed -= Refresh;

        //the X textures belong to UiRenderer's shared cache; drop them so the base Dispose doesn't dispose them
        UnpinButton.NormalTexture = null;
        UnpinButton.PressedTexture = null;

        base.Dispose();
    }

    private void Rebuild(QuestLogEntryInfo entry)
    {
        foreach (var child in Body)
        {
            Children.Remove(child);
            child.Dispose();
        }

        Body.Clear();

        var textWidth = QuestTrackerLayout.WIDTH - (PADDING * 2) - X_SIZE - TITLE_GAP;
        var y = PADDING;

        var title = new TitleLabel
        {
            X = PADDING,
            Y = y,
            Width = textWidth,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.Gold
        };
        title.Pressed += () => TitleClicked?.Invoke();
        title.Text = entry.Title;
        AddChild(title);
        Body.Add(title);
        y += TextRenderer.CHAR_HEIGHT;

        foreach (var line in QuestTrackerLayout.StageLines(entry.Text, s => TextRenderer.MeasureWidth(s), textWidth))
        {
            AddLine(line, y, Color.White);
            y += TextRenderer.CHAR_HEIGHT;
        }

        foreach (var progress in entry.Progress)
        {
            AddLine(QuestTrackerLayout.ProgressLine(progress), y, Color.White);
            y += TextRenderer.CHAR_HEIGHT;
        }

        Height = y + PADDING;
        ChildOrderDirty = true;
    }

    private void AddLine(string text, int y, Color color)
    {
        var label = new UILabel
        {
            X = PADDING,
            Y = y,
            Width = QuestTrackerLayout.WIDTH - (PADDING * 2),
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = color,
            IsHitTestVisible = false,
            Text = text
        };

        AddChild(label);
        Body.Add(label);
    }

    private sealed class TitleLabel : UILabel
    {
        public event Action? Pressed;

        public override void OnClick(ClickEvent e)
        {
            if (e.Button == MouseButton.Left)
            {
                Pressed?.Invoke();
                e.Handled = true;
            }
        }
    }
}
