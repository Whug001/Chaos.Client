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
///     The stable window: a framed 620 by 440 panel with the Creatures, Squads and Ranked tabs, the essence, feed and
///     count on the top line (placed by <see cref="TamerStableLayout" />), and the last refusal message over the Close
///     button at the bottom. The Creatures tab holds the scrolling monster list and the selected monster's detail; the
///     Squads tab holds the squad editor; the Ranked tab holds the ladder and asks for it each time it is opened.
///     Opens on the first snapshot after the catalog, refreshes while visible, and hides without echoing Close on a
///     server close.
///     Borrows the <c>_nsett</c> prefab for its OK button only, as the card binder does.
/// </summary>
public sealed class TamerStableControl : FramedDialogPanelBase
{
    public const int PANEL_HEIGHT = TamerStableLayout.PANEL_HEIGHT;
    public const int PANEL_WIDTH = TamerStableLayout.PANEL_WIDTH;
    public const int PANEL_X = 10;
    public const int PANEL_Y = 6;

    public const int CONTENT_HEIGHT = TamerStableLayout.CONTENT_HEIGHT;
    public const int CONTENT_TOP = TamerStableLayout.CONTENT_TOP;
    public const int CONTENT_WIDTH = TamerStableLayout.CONTENT_WIDTH;
    private const int COUNT_LEFT = TamerStableLayout.COUNT_LEFT;
    private const int COUNT_WIDTH = TamerStableLayout.COUNT_WIDTH;
    private const int DETAIL_LEFT = 210;
    private const int DETAIL_TOP = 0;

    //the hint wraps to two lines of 33 characters in the list's 198 pixels; room for a third, plus the label's
    //1px padding top and bottom, so a reworded hint is not cut
    private const int EMPTY_HEIGHT = (TextRenderer.CHAR_HEIGHT * 3) + 2;
    private const int ESSENCE_LEFT = TamerStableLayout.ESSENCE_LEFT;
    private const int ESSENCE_WIDTH = TamerStableLayout.ESSENCE_WIDTH;
    private const int FEED_LEFT = TamerStableLayout.FEED_LEFT;
    private const int FEED_WIDTH = TamerStableLayout.FEED_WIDTH;
    private const int FOOTER_TOP = PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int INFO_TOP = TamerStableLayout.INFO_TOP;
    private const int LEFT = TamerStableLayout.LEFT;
    private const int LIST_HEIGHT = 284;
    private const int LIST_LEFT = 0;
    private const int LIST_TOP = 0;
    private const int LIST_WIDTH = 200;
    private const int MESSAGE_LEFT = TamerStableLayout.MESSAGE_LEFT;
    private const int MESSAGE_TOP = FOOTER_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
    private const int MESSAGE_WIDTH = TamerStableLayout.MESSAGE_WIDTH;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OPENING_TOP = 214;
    private const int ROW_HEIGHT = TamerMonsterRow.HEIGHT;
    private const int ROW_WIDTH = LIST_WIDTH - ScrollBarControl.DEFAULT_WIDTH;
    private const int SORT_TOP = 290;
    private const int TAB_TOP = TamerStableLayout.TAB_TOP;
    private const int TAB_WIDTH = TamerStableLayout.TAB_WIDTH;
    private const int TITLE_TOP = 8;

    private readonly CustomButton CreaturesTabButton;
    private readonly CustomButton SquadsTabButton;
    private readonly CustomButton RankedTabButton;
    private readonly UILabel EssenceLabel;
    private readonly UILabel FeedLabel;
    private readonly UILabel CountLabel;
    private readonly UILabel StatusLabel;
    private readonly UIPanel CreaturesPanel;
    private readonly TamerSquadPanel SquadsPanel;
    private readonly TamerRankedPanel RankedPanel;
    private readonly VirtualizedRowList<TamerMonsterView> RowList;
    private readonly CustomButton SortButton;
    private readonly UILabel EmptyLabel;
    private readonly TamerDetailPanel DetailPanel;
    private readonly UILabel MessageLabel;
    private readonly TcgChoicePanel Choice;
    private readonly TamerRenamePrompt RenamePrompt;
    private readonly TamerFeedPrompt FeedPrompt;
    private bool ClosedByServer;
    private TamerStableTab Tab = TamerStableTab.Creatures;

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

        CreaturesTabButton = new CustomButton(TamerStableText.CreaturesTab, TAB_WIDTH) { X = TamerStableLayout.CREATURES_TAB_LEFT, Y = TAB_TOP };
        CreaturesTabButton.Clicked += () => SelectTab(TamerStableTab.Creatures);
        AddChild(CreaturesTabButton);

        SquadsTabButton = new CustomButton(TamerStableText.SquadsTab, TAB_WIDTH) { X = TamerStableLayout.SQUADS_TAB_LEFT, Y = TAB_TOP };
        SquadsTabButton.Clicked += () => SelectTab(TamerStableTab.Squads);
        AddChild(SquadsTabButton);

        RankedTabButton = new CustomButton(TamerStableText.RankedTab, TAB_WIDTH) { X = TamerStableLayout.RANKED_TAB_LEFT, Y = TAB_TOP };
        RankedTabButton.Clicked += () => SelectTab(TamerStableTab.Ranked);
        AddChild(RankedTabButton);

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
            ForegroundColor = LegendColors.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorCodesEnabled = false,
            IsHitTestVisible = false,

            //before Text: the label wraps when its text is set and afterwards only when its width changes
            WordWrap = true,
            Text = TamerStableText.EmptyStable
        };
        CreaturesPanel.AddChild(EmptyLabel);

        DetailPanel = new TamerDetailPanel(creatures) { X = DETAIL_LEFT, Y = DETAIL_TOP };
        DetailPanel.RenameClicked += OnRenameClicked;
        DetailPanel.FeedClicked += OnFeedClicked;
        DetailPanel.ReleaseClicked += OnReleaseClicked;
        DetailPanel.AbilityChosen += OnAbilityChosen;
        CreaturesPanel.AddChild(DetailPanel);

        SquadsPanel = new TamerSquadPanel(creatures)
        {
            Name = "TamerTab",
            X = LEFT,
            Y = CONTENT_TOP,
            Width = CONTENT_WIDTH,
            Height = CONTENT_HEIGHT,
            Visible = false
        };
        SquadsPanel.ActionRequested += action => ActionRequested?.Invoke(action);
        SquadsPanel.DiscardQuestionRequested += OnSquadDiscardQuestion;
        AddChild(SquadsPanel);

        RankedPanel = new TamerRankedPanel(creatures)
        {
            X = LEFT,
            Y = CONTENT_TOP,
            Visible = false
        };
        RankedPanel.ActionRequested += action => ActionRequested?.Invoke(action);
        RankedPanel.QuestionRequested += OnRankedQuestion;
        AddChild(RankedPanel);

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
        Tab = TamerStableTab.Creatures;
        Show();
        Refresh();

        var state = WorldState.TamerStable;
        var index = state.Selected is { } selected ? IndexOf(selected.Id) : -1;

        if (index >= 0)
            RowList.ScrollToIndex(index);
    }

    /// <summary>Shows the latest state. Called after every catalog, snapshot and ranked display while the window is open.</summary>
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
            RankedTabButton.Visible = false;
            EssenceLabel.Visible = false;
            FeedLabel.Visible = false;
            CountLabel.Visible = false;
            CreaturesPanel.Visible = false;
            SquadsPanel.Visible = false;
            RankedPanel.Visible = false;
            MessageLabel.Visible = false;

            return;
        }

        StatusLabel.Visible = false;
        CreaturesTabButton.Visible = true;
        SquadsTabButton.Visible = true;
        RankedTabButton.Visible = true;
        EssenceLabel.Visible = true;
        FeedLabel.Visible = true;
        CountLabel.Visible = true;
        MessageLabel.Visible = true;

        //a ranked refusal travels in the Ranked display, every other one in the snapshot
        MessageLabel.Text = Tab == TamerStableTab.Ranked ? WorldState.TamerRanked.LastMessage : state.LastMessage;

        CreaturesTabButton.Selected = Tab == TamerStableTab.Creatures;
        SquadsTabButton.Selected = Tab == TamerStableTab.Squads;
        RankedTabButton.Selected = Tab == TamerStableTab.Ranked;
        EssenceLabel.Text = TamerStableText.EssenceLine(state.Essence);
        FeedLabel.Text = TamerStableText.FeedLine(state.FeedItemsHeld);
        CountLabel.Text = TamerStableText.CountLine(state.Monsters.Count, state.StableCapacity);

        CreaturesPanel.Visible = Tab == TamerStableTab.Creatures;
        SquadsPanel.Visible = Tab == TamerStableTab.Squads;
        RankedPanel.Visible = Tab == TamerStableTab.Ranked;

        if (Tab == TamerStableTab.Squads)
        {
            SquadsPanel.Refresh(state);

            return;
        }

        if (Tab == TamerStableTab.Ranked)
        {
            RankedPanel.Refresh(WorldState.TamerRanked, state);

            return;
        }

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
        HideNow();
        ClosedByServer = false;
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        if ((Tab == TamerStableTab.Squads) && SquadsPanel.HasUnsavedChanges)
        {
            Choice.Ask(
                TamerStableText.DiscardQuestion,
                [TamerStableText.Yes, TamerStableText.No],
                answer =>
                {
                    if (answer != 0)
                        return;

                    SquadsPanel.DiscardChanges();
                    HideNow();
                });
            return;
        }

        HideNow();
    }

    private void HideNow()
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
            else if ((Tab == TamerStableTab.Squads) && SquadsPanel.HasPick)
                SquadsPanel.CancelPick();
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

        //Up/Down (and PageUp/PageDown) walk the Creatures list while no prompt is open
        if ((Tab == TamerStableTab.Creatures) && !Choice.Visible && !RenamePrompt.Visible && !FeedPrompt.Visible
            && ArrowStep(e.Keycode) is { } step)
        {
            var index = WorldState.TamerStable.MoveSelection(step);

            if (index >= 0)
            {
                Refresh();
                RowList.ScrollIntoView(index);
            }

            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    private int? ArrowStep(Keycode key)
        => key switch
        {
            Keycode.Up => -1,
            Keycode.Down => 1,
            Keycode.PageUp => -Math.Max(1, RowList.VisibleRows - 1),
            Keycode.PageDown => Math.Max(1, RowList.VisibleRows - 1),
            _ => null
        };

    private static int IndexOf(Guid monsterId)
    {
        var monsters = WorldState.TamerStable.Monsters;

        for (var i = 0; i < monsters.Count; i++)
            if (monsters[i].Id == monsterId)
                return i;

        return -1;
    }

    private void SelectTab(TamerStableTab tab)
    {
        if ((Tab == TamerStableTab.Squads) && (tab != TamerStableTab.Squads) && SquadsPanel.HasUnsavedChanges)
        {
            Choice.Ask(
                TamerStableText.DiscardQuestion,
                [TamerStableText.Yes, TamerStableText.No],
                answer =>
                {
                    if (answer != 0)
                        return;

                    SquadsPanel.DiscardChanges();
                    ShowTab(tab);
                });
            return;
        }

        ShowTab(tab);
    }

    //opening the Ranked tab asks for the ladder; what is already held is drawn until the answer comes
    private void ShowTab(TamerStableTab tab)
    {
        var opensRanked = (tab == TamerStableTab.Ranked) && (Tab != TamerStableTab.Ranked);

        Tab = tab;
        Refresh();

        if (opensRanked && Visible)
            ActionRequested?.Invoke(TamerRankedState.InfoAction());
    }

    //a ranked request that needs a Yes first goes on the window's choice panel, so the tabs and Close are covered
    private void OnRankedQuestion(string question, Action onYes)
    {
        if (!Visible || (Tab != TamerStableTab.Ranked))
            return;

        Choice.Ask(
            question,
            [TamerStableText.Yes, TamerStableText.No],
            answer =>
            {
                //the window may have closed or switched tab while the question was up
                if ((answer != 0) || !Visible || (Tab != TamerStableTab.Ranked))
                    return;

                onYes();

                //a Yes that sent nothing because the data changed says so on the bottom line
                Refresh();
            });
    }

    //the squad tab's own question goes on the window's choice panel, so the tabs and Close are covered while it is up
    private void OnSquadDiscardQuestion(Action discard)
    {
        if (!Visible || (Tab != TamerStableTab.Squads))
            return;

        Choice.Ask(
            TamerStableText.DiscardQuestion,
            [TamerStableText.Yes, TamerStableText.No],
            answer =>
            {
                if (answer == 0)
                    discard();
            });
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

                if (state.ReleaseAction(id) is { } action)
                    ActionRequested?.Invoke(action);
            });
    }

    private void OnAbilityChosen(string abilityKey)
    {
        var state = WorldState.TamerStable;

        if (!Visible || (state.Selected is not { } monster))
            return;

        if (state.SetAbilityAction(monster.Id, abilityKey) is { } action)
            ActionRequested?.Invoke(action);
    }
}
