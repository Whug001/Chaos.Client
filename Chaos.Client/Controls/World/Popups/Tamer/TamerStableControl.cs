#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The stable window: a framed 620 by 440 panel with the Creatures and Squads tabs, the essence, feed and count on
///     the top line, and the last refusal message over the Close button at the bottom. The Creatures tab holds the
///     scrolling monster list and the selected monster's detail; the Squads tab is an empty panel for Task 4's squad
///     editor. Opens on the first snapshot after the catalog, refreshes while visible, and hides without echoing Close
///     on a server close. Borrows the <c>_nsett</c> prefab for its OK button only, as the card binder does.
/// </summary>
public sealed class TamerStableControl : FramedDialogPanelBase
{
    public const int PANEL_HEIGHT = 440;
    public const int PANEL_WIDTH = 620;
    public const int PANEL_X = 10;
    public const int PANEL_Y = 6;

    private const int CONTENT_HEIGHT = 312;
    private const int CONTENT_TOP = 52;
    private const int CONTENT_WIDTH = PANEL_WIDTH - (2 * LEFT);
    private const int COUNT_LEFT = 495;
    private const int COUNT_WIDTH = 105;
    private const int DETAIL_LEFT = 210;
    private const int DETAIL_TOP = 0;
    private const int EMPTY_HEIGHT = TextRenderer.CHAR_HEIGHT * 2;
    private const int ESSENCE_LEFT = 250;
    private const int ESSENCE_WIDTH = 165;
    private const int FEED_LEFT = 420;
    private const int FEED_WIDTH = 70;
    private const int FOOTER_TOP = PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int INFO_TOP = TAB_TOP + 5;
    private const int LEFT = 20;
    private const int LIST_HEIGHT = 284;
    private const int LIST_LEFT = 0;
    private const int LIST_TOP = 0;
    private const int LIST_WIDTH = 200;
    private const int MESSAGE_LEFT = 20;
    private const int MESSAGE_TOP = FOOTER_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
    private const int MESSAGE_WIDTH = 460;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OPENING_TOP = 214;
    private const int ROW_HEIGHT = TamerMonsterRow.HEIGHT;
    private const int ROW_WIDTH = LIST_WIDTH - ScrollBarControl.DEFAULT_WIDTH;
    private const int SORT_TOP = 290;
    private const int TAB_STEP = 96;
    private const int TAB_TOP = 24;
    private const int TAB_WIDTH = 90;
    private const int TITLE_TOP = 8;

    private readonly CustomButton CreaturesTabButton;
    private readonly CustomButton SquadsTabButton;
    private readonly UILabel EssenceLabel;
    private readonly UILabel FeedLabel;
    private readonly UILabel CountLabel;
    private readonly UILabel StatusLabel;
    private readonly UIPanel CreaturesPanel;
    private readonly UIPanel SquadsPanel;
    private readonly VirtualizedRowList<TamerMonsterView> RowList;
    private readonly CustomButton SortButton;
    private readonly UILabel EmptyLabel;
    private readonly TamerDetailPanel DetailPanel;
    private readonly UILabel MessageLabel;
    private readonly TcgChoicePanel Choice;
    private readonly TamerRenamePrompt RenamePrompt;
    private readonly TamerFeedPrompt FeedPrompt;
    private bool ClosedByServer;
    private bool ShowingSquads;

    /// <summary>A stable window request (Close from the Close button or Escape, and every prompt send).</summary>
    public event Action<TamerActionArgs>? ActionRequested;

    /// <summary>The window was hidden, by the player or the server. The Close request is not sent on a server close.</summary>
    public event Action? Closed;

    public TamerStableControl(CreatureRenderer creatures)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        Name = "Stable";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        X = PANEL_X;
        Y = PANEL_Y;

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        AddChild(Label(0, TITLE_TOP, PANEL_WIDTH, TamerStableText.WindowTitle, LegendColors.Gold, HorizontalAlignment.Center));

        CreaturesTabButton = new CustomButton(TamerStableText.CreaturesTab, TAB_WIDTH) { X = LEFT, Y = TAB_TOP };
        CreaturesTabButton.Clicked += () => SelectTab(false);
        AddChild(CreaturesTabButton);

        SquadsTabButton = new CustomButton(TamerStableText.SquadsTab, TAB_WIDTH) { X = LEFT + TAB_STEP, Y = TAB_TOP };
        SquadsTabButton.Clicked += () => SelectTab(true);
        AddChild(SquadsTabButton);

        EssenceLabel = Label(ESSENCE_LEFT, INFO_TOP, ESSENCE_WIDTH, string.Empty, LegendColors.Gold, HorizontalAlignment.Right);
        FeedLabel = Label(FEED_LEFT, INFO_TOP, FEED_WIDTH, string.Empty, LegendColors.Gold, HorizontalAlignment.Right);
        CountLabel = Label(COUNT_LEFT, INFO_TOP, COUNT_WIDTH, string.Empty, LegendColors.Gold, HorizontalAlignment.Right);
        AddChild(EssenceLabel);
        AddChild(FeedLabel);
        AddChild(CountLabel);

        StatusLabel = Label(LEFT, OPENING_TOP, PANEL_WIDTH - (2 * LEFT), TamerStableText.Opening, LegendColors.Gray, HorizontalAlignment.Center);
        AddChild(StatusLabel);

        CreaturesPanel = TabPanel();
        AddChild(CreaturesPanel);

        RowList = new VirtualizedRowList<TamerMonsterView>(
            ROW_WIDTH,
            LIST_HEIGHT,
            ROW_HEIGHT,
            () =>
            {
                TamerMonsterRow row = new(ROW_WIDTH);
                row.Clicked += OnRowClicked;
                return row;
            },
            (row, monster, _) => ((TamerMonsterRow)row).Bind(WorldState.TamerStable, monster));
        CreaturesPanel.AddChild(new ScrollViewerControl(RowList) { X = LIST_LEFT, Y = LIST_TOP, Width = LIST_WIDTH, Height = LIST_HEIGHT });

        SortButton = new CustomButton(string.Empty, LIST_WIDTH) { X = LIST_LEFT, Y = SORT_TOP };
        SortButton.Clicked += OnSortClicked;
        CreaturesPanel.AddChild(SortButton);

        EmptyLabel = new UILabel
        {
            X = LIST_LEFT,
            Y = ((LIST_HEIGHT - EMPTY_HEIGHT) / 2),
            Width = LIST_WIDTH,
            Height = EMPTY_HEIGHT,
            Text = TamerStableText.EmptyStable,
            ForegroundColor = LegendColors.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            WordWrap = true,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        CreaturesPanel.AddChild(EmptyLabel);

        DetailPanel = new TamerDetailPanel(creatures) { X = DETAIL_LEFT, Y = DETAIL_TOP };
        DetailPanel.RenameClicked += OnRenameClicked;
        DetailPanel.FeedClicked += OnFeedClicked;
        DetailPanel.ReleaseClicked += OnReleaseClicked;
        DetailPanel.AbilityChosen += OnAbilityChosen;
        CreaturesPanel.AddChild(DetailPanel);

        //Task 4 adds the squad editor (TamerSquadPanel) as a child of this panel, shown when the Squads tab is
        //selected; Refresh already toggles it like the Creatures content.
        SquadsPanel = TabPanel();
        SquadsPanel.Visible = false;
        AddChild(SquadsPanel);

        MessageLabel = Label(MESSAGE_LEFT, MESSAGE_TOP, MESSAGE_WIDTH, string.Empty, LegendColors.Orange);
        AddChild(MessageLabel);

        //last, so they draw over everything and swallow clicks meant for what is behind them
        Choice = new TcgChoicePanel(PANEL_WIDTH, PANEL_HEIGHT);
        AddChild(Choice);

        RenamePrompt = new TamerRenamePrompt(PANEL_WIDTH, PANEL_HEIGHT);
        RenamePrompt.Confirmed += action => ActionRequested?.Invoke(action);
        AddChild(RenamePrompt);

        FeedPrompt = new TamerFeedPrompt(PANEL_WIDTH, PANEL_HEIGHT);
        FeedPrompt.Confirmed += action => ActionRequested?.Invoke(action);
        AddChild(FeedPrompt);
    }

    private static UIPanel TabPanel()
        => new() { Name = "TamerTab", X = LEFT, Y = CONTENT_TOP, Width = CONTENT_WIDTH, Height = CONTENT_HEIGHT };

    private static UILabel Label(int x, int y, int width, string text, Color color, HorizontalAlignment alignment = HorizontalAlignment.Left)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            Text = text,
            ForegroundColor = color,
            HorizontalAlignment = alignment,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };

    public void Open()
    {
        ShowingSquads = false;
        Show();
        Refresh();

        var state = WorldState.TamerStable;
        var index = state.Selected is { } selected ? IndexOf(selected.Id) : -1;

        if (index >= 0)
            RowList.ScrollToIndex(index);
    }

    /// <summary>Shows the latest state. Called after every catalog and snapshot while the window is open.</summary>
    public void Refresh()
    {
        if (!Visible)
            return;

        var state = WorldState.TamerStable;

        if (!state.HasCatalog || !state.HasSnapshot)
        {
            StatusLabel.Visible = true;
            StatusLabel.Text = TamerStableText.Opening;
            CreaturesTabButton.Visible = false;
            SquadsTabButton.Visible = false;
            EssenceLabel.Visible = false;
            FeedLabel.Visible = false;
            CountLabel.Visible = false;
            CreaturesPanel.Visible = false;
            SquadsPanel.Visible = false;
            MessageLabel.Visible = false;

            return;
        }

        StatusLabel.Visible = false;
        CreaturesTabButton.Visible = true;
        SquadsTabButton.Visible = true;
        EssenceLabel.Visible = true;
        FeedLabel.Visible = true;
        CountLabel.Visible = true;
        MessageLabel.Visible = true;
        MessageLabel.Text = state.LastMessage;

        CreaturesTabButton.Selected = !ShowingSquads;
        SquadsTabButton.Selected = ShowingSquads;
        EssenceLabel.Text = TamerStableText.EssenceLine(state.Essence);
        FeedLabel.Text = TamerStableText.FeedLine(state.FeedItemsHeld);
        CountLabel.Text = TamerStableText.CountLine(state.Monsters.Count, state.StableCapacity);

        if (ShowingSquads)
        {
            CreaturesPanel.Visible = false;
            SquadsPanel.Visible = true;

            return;
        }

        CreaturesPanel.Visible = true;
        SquadsPanel.Visible = false;

        RowList.ReplaceItems(state.Monsters);
        SortButton.Caption = TamerStableText.SortLabel(state.Sort);
        SortButton.Enabled = state.Monsters.Count > 0;
        EmptyLabel.Visible = state.Monsters.Count == 0;
        DetailPanel.Visible = state.Monsters.Count > 0;
        DetailPanel.Refresh(state, state.Selected);

        RenamePrompt.Refresh(state);
        FeedPrompt.Refresh(state);
    }

    /// <summary>The server closed the window: hide without sending Close back.</summary>
    public void CloseFromServer()
    {
        ClosedByServer = true;
        Hide();
        ClosedByServer = false;
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        Choice.Dismiss();
        RenamePrompt.Hide();
        FeedPrompt.Hide();
        base.Hide();

        if (!ClosedByServer)
            ActionRequested?.Invoke(TamerStableState.CloseAction());

        Closed?.Invoke();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            if (Choice.Visible)
                Choice.Cancel();
            else if (RenamePrompt.Visible)
                RenamePrompt.Hide();
            else if (FeedPrompt.Visible)
                FeedPrompt.Hide();
            else
                Hide();

            e.Handled = true;

            return;
        }

        if ((e.Keycode == Keycode.Enter) && RenamePrompt.Visible)
        {
            RenamePrompt.TrySubmit();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    private static int IndexOf(Guid monsterId)
    {
        var monsters = WorldState.TamerStable.Monsters;

        for (var i = 0; i < monsters.Count; i++)
            if (monsters[i].Id == monsterId)
                return i;

        return -1;
    }

    private void SelectTab(bool squads)
    {
        ShowingSquads = squads;
        Refresh();
    }

    private void OnRowClicked(TamerMonsterRow row)
    {
        if (!Visible || (row.MonsterId == Guid.Empty))
            return;

        WorldState.TamerStable.Select(row.MonsterId);
        Refresh();
    }

    private void OnSortClicked()
    {
        if (!Visible)
            return;

        WorldState.TamerStable.CycleSort();
        Refresh();
    }

    private void OnRenameClicked()
    {
        var state = WorldState.TamerStable;

        if (!Visible || (state.Selected is not { } monster) || !state.CanRename(monster, out _))
            return;

        RenamePrompt.Open(monster.Id, monster.Name);
    }

    private void OnFeedClicked()
    {
        var state = WorldState.TamerStable;

        if (!Visible || (state.Selected is not { } monster) || !state.CanFeed(monster, out _))
            return;

        FeedPrompt.Open(monster.Id);
        FeedPrompt.Refresh(state);
    }

    private void OnReleaseClicked()
    {
        var state = WorldState.TamerStable;

        if (!Visible || (state.Selected is not { } monster) || !state.CanRelease(monster, out _))
            return;

        var id = monster.Id;

        Choice.Ask(
            TamerStableText.ReleaseQuestion(monster.Name, monster.ReleaseEssence),
            [TamerStableText.Yes, TamerStableText.No],
            answer =>
            {
                if (answer != 0)
                    return;

                if (TamerStableState.ReleaseAction(id) is { } action)
                    ActionRequested?.Invoke(action);
            });
    }

    private void OnAbilityChosen(string abilityKey)
    {
        var state = WorldState.TamerStable;

        if (!Visible || (state.Selected is not { } monster))
            return;

        if (TamerStableState.SetAbilityAction(monster.Id, abilityKey) is { } action)
            ActionRequested?.Invoke(action);
    }
}
