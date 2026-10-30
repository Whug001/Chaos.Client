using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Data;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.Crafting;

/// <summary>The recipe browser (Korean "produce" panel): recipe list, filter, ingredients, rank and success.</summary>
public sealed class CraftingBrowserControl : PrefabPanel
{
    public const string PREFAB = "produce";

    //the art's scroll track is wider than the scroll bar control, so rows stop short of it
    private const int SCROLL_BAR_GAP = 4;

    private readonly UILabel AbilityValue;
    private readonly UITextBox FilterBox;
    private readonly UILabel DescriptionLabel;
    private readonly UILabel LevelValue;
    private readonly UILabel MadeValue;
    private readonly UIProgressBar RankBar;
    private readonly UILabel RankBarLabel;
    private readonly UILabel RankValue;
    private readonly VirtualizedRowList<CraftingRecipeInfo> RecipeList;
    private readonly UILabel SuccessValue;
    private bool ClosedByServer;
    private ushort? DescriptionRecipeId;
    private string LastFilter = string.Empty;

    private static CraftingWindowState State => WorldState.Crafting;

    /// <summary>Each action to send, including Close when the player hides the window and Cancel while a batch runs.</summary>
    public event Action<CraftingActionArgs>? ActionRequested;

    /// <summary>OK or a double-click on a recipe: open the confirm window.</summary>
    public event Action? ConfirmRequested;

    private CraftingBrowserControl()
        : base(PREFAB)
    {
        Name = "CraftingBrowser";
        Visible = false;
        UsesControlStack = true;

        var listRect = GetRect("skill");
        var rowWidth = listRect.Width - ScrollBarControl.DEFAULT_WIDTH - SCROLL_BAR_GAP;

        RecipeList = new VirtualizedRowList<CraftingRecipeInfo>(
            rowWidth,
            listRect.Height,
            CraftingRecipeRow.HEIGHT,
            () => new CraftingRecipeRow(rowWidth),
            (row, recipe, selected) => ((CraftingRecipeRow)row).Bind(recipe, selected),
            selectable: true);

        RecipeList.SelectionChanged += OnRecipeSelected;
        RecipeList.RowActivated += _ => ConfirmRequested?.Invoke();

        AddChild(
            new ScrollViewerControl(RecipeList)
            {
                X = listRect.X,
                Y = listRect.Y,
                Width = listRect.Width,
                Height = listRect.Height
            });

        var elementRect = GetRect("element");

        DescriptionLabel = new UILabel
        {
            Width = elementRect.Width - ScrollBarControl.DEFAULT_WIDTH - SCROLL_BAR_GAP,
            Height = elementRect.Height,
            PaddingLeft = 2,
            PaddingTop = 2,
            WordWrap = true,
            VerticalAlignment = VerticalAlignment.Top,
            ForegroundColor = LegendColors.White
        };

        AddChild(
            new ScrollViewerControl(DescriptionLabel)
            {
                X = elementRect.X,
                Y = elementRect.Y,
                Width = elementRect.Width,
                Height = elementRect.Height
            });

        FilterBox = CreateTextBox("filter", 20)!;

        CreateLabel("rankcap", HorizontalAlignment.Center)!.Text = "Rank";
        CreateLabel("successcap", HorizontalAlignment.Center)!.Text = "Success";
        CreateLabel("madecap", HorizontalAlignment.Center)!.Text = "Made";

        RankValue = CreateLabel("rank", HorizontalAlignment.Center)!;
        SuccessValue = CreateLabel("success", HorizontalAlignment.Center)!;
        MadeValue = CreateLabel("made", HorizontalAlignment.Center)!;
        LevelValue = CreateLabel("level", HorizontalAlignment.Center)!;
        AbilityValue = CreateLabel("ability", HorizontalAlignment.Center)!;

        var barRect = GetRect("rankbar");

        RankBar = new UIProgressBar
        {
            X = barRect.X + 2,
            Y = barRect.Y + 2,
            Width = barRect.Width - 4,
            Height = barRect.Height - 4,
            FillColor = new Color(156, 96, 12)
        };

        AddChild(RankBar);

        RankBarLabel = new UILabel
        {
            X = barRect.X,
            Y = barRect.Y + ((barRect.Height - TextRenderer.CHAR_HEIGHT) / 2),
            Width = barRect.Width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };

        AddChild(RankBarLabel);

        CreateButton("ok")!.Clicked += () =>
        {
            //the selected recipe may be filtered out of view; only confirm what the player can see
            if (RecipeList.SelectedItem is { } recipe && (recipe.Id == State.SelectedId))
                ConfirmRequested?.Invoke();
        };

        //while a batch runs the window stays open and Cancel stops the batch; otherwise it closes the window
        CreateButton("cancel")!.Clicked += CancelOrHide;
    }

    /// <summary>Null when this client's setoa.dat has no produce.txt.</summary>
    public static CraftingBrowserControl? TryCreate()
        => DataContext.UserControls.Get(PREFAB) is null ? null : new CraftingBrowserControl();

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

        FilterBox.IsFocused = false;
        base.Hide();

        if (!ClosedByServer)
            ActionRequested?.Invoke(CraftingWindowState.CloseAction());
    }

    public void Open()
    {
        LastFilter = string.Empty;
        FilterBox.Text = string.Empty;
        State.Filter = string.Empty;
        LoadRecipes();
        Show();
        Refresh();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            CancelOrHide();
            e.Handled = true;

            return;
        }

        if ((e.Keycode == Keycode.Enter) && FilterBox.IsFocused)
        {
            FilterBox.IsFocused = false;

            //IsFocused alone leaves the dispatchers explicit focus on the box, which keeps swallowing
            //clicks outside the window: clear it too, like the confirm window does
            var dispatcher = InputDispatcher.Instance;

            if (dispatcher?.ExplicitFocus == FilterBox)
                dispatcher.ClearExplicitFocus();

            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public void Refresh()
    {
        if (!Visible)
            return;

        RecipeList.Invalidate();
        var detail = State.Detail;

        //every crafted item in a batch resends the detail; only a new recipe starts the description at the top
        if (detail?.RecipeId != DescriptionRecipeId)
            DescriptionLabel.ScrollOffset = 0;

        DescriptionLabel.Text = detail?.Description ?? string.Empty;
        DescriptionRecipeId = detail?.RecipeId;
        RankValue.Text = State.RankShortTitle;
        SuccessValue.Text = detail is null ? string.Empty : $"{detail.SuccessRate}%";
        MadeValue.Text = detail is null ? string.Empty : detail.TimesMade.ToString();
        LevelValue.Text = detail is null ? string.Empty : $"{detail.Level} / {detail.RequiredLevel}";

        AbilityValue.Text = detail is null ? string.Empty
            : detail.RequiredAbility == 0 ? "—"
            : $"{detail.AbilityLevel} / {detail.RequiredAbility}";

        RankBar.Percent = State.RankBarPercent;
        RankBarLabel.Text = State.Running ? State.ProgressText : State.RankBarText;
    }

    /// <summary>The list changed on the server (after a batch): keep the filter, reload rows.</summary>
    public void ReloadList()
    {
        LoadRecipes(keepScroll: true);
        Refresh();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible || (FilterBox.Text == LastFilter))
            return;

        LastFilter = FilterBox.Text;
        State.Filter = LastFilter;
        LoadRecipes();
    }

    private void CancelOrHide()
    {
        if (State.Running)
            ActionRequested?.Invoke(CraftingWindowState.CancelAction());
        else
            Hide();
    }

    //SetItems drops the highlight; put it back on the recipe the server still has selected, if it is in view
    private void LoadRecipes(bool keepScroll = false)
    {
        if (keepScroll)
            RecipeList.ReplaceItems(State.VisibleRecipes);
        else
            RecipeList.SetItems(State.VisibleRecipes);

        RecipeList.RestoreSelection(recipe => recipe.Id == State.SelectedId);
    }

    private void OnRecipeSelected()
    {
        if (RecipeList.SelectedItem is not { } recipe)
            return;

        ActionRequested?.Invoke(State.Select(recipe.Id));
        Refresh();
    }
}
