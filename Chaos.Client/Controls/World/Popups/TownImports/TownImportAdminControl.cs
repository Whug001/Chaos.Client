#region
using System.Globalization;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Bank;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.TownImports;

/// <summary>
///     The admins' import window ("Town Imports - Mileth"): search the cosmetics, build and price a town's import pool, save
///     or revert it, preview any item on the admin's own figure, and end the running import early. Opened by the server's
///     TownImportAdmin Open from the admin trinket. The server checks every request; the window only edits a local copy of
///     the pool (<see cref="TownImportPoolEditor" />) until Save.
/// </summary>
/// <remarks>
///     Layout: three columns under the title. FIND (left): the search box and Find, the result list, its page row and Add.
///     POOL (middle): the price box and Set, the pool list, its page row with the item count, and Up/Down/Remove. PREVIEW
///     (right): the walking preview with its turn row, the running import and End import. The footer row puts the status
///     line under the two lists and Save pool/Revert under the preview column. 8 rows of 34 px leave no room for a separate
///     Save/Revert row under the status line inside 480 px, so they share the status line's row; the height still follows
///     the lowest element so it always clears <see cref="FramedDialogPanelBase.BORDER_BOTTOM_HEIGHT" />.
/// </remarks>
public sealed class TownImportAdminControl : GuildCloakDialogBase
{
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private const int LEFT = 20;
    private const int GAP = 8;
    private const int SEARCH_WIDTH = 190;
    private const int POOL_WIDTH = 210;
    private const int PREVIEW_WIDTH = 140;
    private const int PREVIEW_HEIGHT = 180;
    private const int ROW_HEIGHT = TownImportRow.HEIGHT;
    private const int PAGE_SIZE = 8;

    private const int TITLE_TOP = 10;
    private const int CAPTION_TOP = 30;
    private const int INPUT_TOP = 44;
    private const int LIST_TOP = 70;
    private const int SMALL_BUTTON = 28;
    private const int INPUT_BUTTON = 40;
    private const int FOOTER_BUTTON = 68;
    private const int RUNNING_LINES = 5;
    private const int STATUS_LINES = 2;
    private const int PRICE_CHARS = 11; //"50,000,000" and a spare
    private const int BORDER_GAP = 4;
    private const int CLOCK_MS = 1000;

    private const string BAD_PRICE = "Type a whole number above 0.";

    private readonly CustomButton AddItemButton;
    private readonly CustomButton DownButton;
    private readonly TownImportPoolEditor Editor = new();
    private readonly CustomButton EndButton;
    private readonly int FooterTop;
    private readonly CustomButton PoolNextButton;
    private readonly UILabel PoolCountLabel;
    private readonly UILabel PoolEmptyLabel;
    private readonly UILabel PoolPageLabel;
    private readonly CustomButton PoolPrevButton;
    private readonly TownImportRow[] PoolRows = new TownImportRow[PAGE_SIZE];
    private readonly GuildCloakPreview Preview;
    private readonly UILabel PreviewCaption;
    private readonly CustomTextBox PriceBox;
    private readonly CustomButton RemoveButton;
    private readonly UILabel ResultsEmptyLabel;
    private readonly CustomButton ResultsNextButton;
    private readonly UILabel ResultsPageLabel;
    private readonly CustomButton ResultsPrevButton;
    private readonly TownImportRow[] ResultRows = new TownImportRow[PAGE_SIZE];
    private readonly CustomButton RevertButton;
    private readonly UILabel[] RunningLabels = new UILabel[RUNNING_LINES];
    private readonly CustomButton SaveButton;
    private readonly CustomTextBox SearchBox;
    private readonly CustomButton SetPriceButton;
    private readonly UILabel StatusLabel;
    private readonly UILabel TitleLabel;
    private readonly CustomButton TownNextButton;
    private readonly CustomButton TownPrevButton;
    private readonly CustomButton UpButton;
    private readonly Func<AislingAppearance> ViewerLook;

    private double ClockElapsedMs;
    private bool ConfirmingEnd;

    //a note of the window's own (a dropped-edits warning) kept until the reply it waits for, which carries no status
    private string? PendingNote;
    private int PoolPage;
    private bool PreviewFromPool;
    private IReadOnlyList<TownImportItemInfo> Results = [];
    private int ResultsPage;
    private TownImportRunningInfo? Running;
    private bool Searched;
    private int SelectedPool = -1;
    private int SelectedResult = -1;
    private string TownKey = string.Empty;
    private IReadOnlyList<TownImportTownInfo> Towns = [];

    public TownImportAdminControl(AislingRenderer renderer, Func<AislingAppearance> viewerLook)
        : base("_nsett", false)
    {
        ViewerLook = viewerLook;
        Name = "TownImportAdmin";
        Visible = false;
        UsesControlStack = true;

        var poolLeft = LEFT + SEARCH_WIDTH + GAP;
        var previewLeft = poolLeft + POOL_WIDTH + GAP;
        var pageTop = LIST_TOP + (PAGE_SIZE * ROW_HEIGHT) + 4;
        var actionTop = pageTop + CustomButton.HEIGHT + 4;
        var turnTop = INPUT_TOP + PREVIEW_HEIGHT + 4;
        var runningCaptionTop = turnTop + CustomButton.HEIGHT + 6;
        var runningTop = runningCaptionTop + TextRenderer.CHAR_HEIGHT + 2;
        var statusHeight = (STATUS_LINES * TextRenderer.CHAR_HEIGHT) + 2; //+2 for UILabel's own padding

        //the footer: a two-line status under the lists, Save pool and Revert under the preview column
        FooterTop = actionTop + CustomButton.HEIGHT + 6;
        var footerBottom = Math.Max(FooterTop + CustomButton.HEIGHT, FooterTop - 2 + statusHeight);

        Width = previewLeft + PREVIEW_WIDTH + LEFT;
        Height = footerBottom + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = Caption("Town Imports", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        //the town switch flanks the title; RefreshTitle moves it to hug the title's text
        TownPrevButton = AddButton("<", SMALL_BUTTON, LEFT, TITLE_TOP - 5, () => SwitchTown(-1));
        TownNextButton = AddButton(">", SMALL_BUTTON, Width - LEFT - SMALL_BUTTON, TITLE_TOP - 5, () => SwitchTown(1));

        //── FIND ──
        Caption("FIND", LEFT, CAPTION_TOP, SEARCH_WIDTH, color: LegendColors.Gray);

        SearchBox = new CustomTextBox
        {
            X = LEFT,
            Y = INPUT_TOP,
            Width = SEARCH_WIDTH - INPUT_BUTTON - 4,
            Height = CustomButton.HEIGHT,
            MaxLength = TownImportProtocol.MAX_SEARCH_CHARS,
            HintText = "Item name"
        };

        AddChild(SearchBox);
        AddButton("Find", INPUT_BUTTON, LEFT + SEARCH_WIDTH - INPUT_BUTTON, INPUT_TOP, Find);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var slot = i;
            ResultRows[i] = AddRow(LEFT, SEARCH_WIDTH, i, () => SelectResult((ResultsPage * PAGE_SIZE) + slot));
        }

        ResultsEmptyLabel = Caption(string.Empty, LEFT, LIST_TOP + 60, SEARCH_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        ResultsPrevButton = AddButton("<", SMALL_BUTTON, LEFT, pageTop, () => TurnResultsPage(-1));
        ResultsNextButton = AddButton(">", SMALL_BUTTON, LEFT + SMALL_BUTTON + 4, pageTop, () => TurnResultsPage(1));
        ResultsPageLabel = PageCaption(LEFT, pageTop, SEARCH_WIDTH);

        AddItemButton = AddButton("Add", SEARCH_WIDTH, LEFT, actionTop, AddSelectedResult);

        //── POOL ──
        Caption("POOL", poolLeft, CAPTION_TOP, POOL_WIDTH, color: LegendColors.Gray);

        PriceBox = new CustomTextBox
        {
            X = poolLeft,
            Y = INPUT_TOP,
            Width = POOL_WIDTH - INPUT_BUTTON - 4,
            Height = CustomButton.HEIGHT,
            MaxLength = PRICE_CHARS,
            HintText = "Price"
        };

        AddChild(PriceBox);
        SetPriceButton = AddButton("Set", INPUT_BUTTON, poolLeft + POOL_WIDTH - INPUT_BUTTON, INPUT_TOP, SetPrice);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var slot = i;
            PoolRows[i] = AddRow(poolLeft, POOL_WIDTH, i, () => SelectPool((PoolPage * PAGE_SIZE) + slot));
        }

        PoolEmptyLabel = Caption("The pool is empty.", poolLeft, LIST_TOP + 60, POOL_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        PoolPrevButton = AddButton("<", SMALL_BUTTON, poolLeft, pageTop, () => TurnPoolPage(-1));
        PoolNextButton = AddButton(">", SMALL_BUTTON, poolLeft + SMALL_BUTTON + 4, pageTop, () => TurnPoolPage(1));
        PoolPageLabel = PageCaption(poolLeft, pageTop, POOL_WIDTH);

        PoolCountLabel = Caption(
            string.Empty,
            poolLeft + (2 * SMALL_BUTTON) + 50,
            pageTop + 5,
            POOL_WIDTH - (2 * SMALL_BUTTON) - 50,
            HorizontalAlignment.Right,
            LegendColors.Gray);

        //Up and Down share the left half, Remove takes the right half
        var moveWidth = (POOL_WIDTH - 8) / 4;
        UpButton = AddButton("Up", moveWidth, poolLeft, actionTop, () => MoveSelected(-1));
        DownButton = AddButton("Down", moveWidth, poolLeft + moveWidth + 4, actionTop, () => MoveSelected(1));
        var removeLeft = poolLeft + (2 * moveWidth) + 8;
        RemoveButton = AddButton("Remove", poolLeft + POOL_WIDTH - removeLeft, removeLeft, actionTop, RemoveSelected);

        //── PREVIEW ──
        PreviewCaption = Caption("PREVIEW", previewLeft, CAPTION_TOP, PREVIEW_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        Preview = new GuildCloakPreview(renderer, PREVIEW_WIDTH, PREVIEW_HEIGHT)
        {
            X = previewLeft,
            Y = INPUT_TOP
        };

        AddChild(Preview);

        AddButton("<", SMALL_BUTTON, previewLeft, turnTop, () => Preview.Turn(-1));
        AddButton(">", SMALL_BUTTON, previewLeft + PREVIEW_WIDTH - SMALL_BUTTON, turnTop, () => Preview.Turn(1));

        Caption("RUNNING IMPORT", previewLeft, runningCaptionTop, PREVIEW_WIDTH, color: LegendColors.Gray);

        for (var i = 0; i < RUNNING_LINES; i++)
            RunningLabels[i] = Caption(string.Empty, previewLeft, runningTop + (i * TextRenderer.CHAR_HEIGHT), PREVIEW_WIDTH);

        EndButton = AddButton(string.Empty, PREVIEW_WIDTH, previewLeft, actionTop, EndImport);

        //── footer ──
        StatusLabel = Caption(string.Empty, LEFT, FooterTop, poolLeft + POOL_WIDTH - LEFT);
        StatusLabel.Height = statusHeight;
        StatusLabel.WordWrap = true;

        SaveButton = AddButton("Save pool", FOOTER_BUTTON, previewLeft, FooterTop, Save);
        RevertButton = AddButton("Revert", FOOTER_BUTTON, previewLeft + PREVIEW_WIDTH - FOOTER_BUTTON, FooterTop, Revert);

        RefreshAll();
    }

    private TownImportItemInfo? SelectedPoolItem
        => (SelectedPool >= 0) && (SelectedPool < Editor.Items.Count) ? Editor.Items[SelectedPool] : null;

    private TownImportItemInfo? SelectedResultItem
        => (SelectedResult >= 0) && (SelectedResult < Results.Count) ? Results[SelectedResult] : null;

    /// <summary>Raised for every request the window makes: load a town, search, save the pool, or end the running import.</summary>
    public event Action<TownImportAdminInteractionArgs>? RequestSent;

    /// <summary>
    ///     Takes a message from the server. Open loads everything and shows the window (a closed window starts fresh); Pool
    ///     reloads the saved pool, dropping unsaved edits, and the running import; Results replaces the search results; Status
    ///     updates the running import only, keeping edits. Every type sets the status line. Messages other than Open are
    ///     ignored while the window is closed.
    /// </summary>
    public void Apply(TownImportAdminArgs args)
    {
        if ((args.Type != TownImportAdminType.Open) && !Visible)
            return;

        switch (args.Type)
        {
            case TownImportAdminType.Open:
                if (!Visible)
                    Reset();

                SetTown(args);
                SetResults(args.Results, false);
                LoadPool(args.Pool);
                Running = args.Running;

                break;

            case TownImportAdminType.Pool:
                SetTown(args);
                LoadPool(args.Pool);
                Running = args.Running;

                break;

            case TownImportAdminType.Results:
                SetResults(args.Results, true);

                break;

            case TownImportAdminType.Status:
                Running = args.Running;

                break;

            default:
                return;
        }

        SetStatus(string.IsNullOrEmpty(args.Status) ? PendingNote ?? string.Empty : args.Status);

        //only a full reload (Open, Pool) has shown the note; a search or status update must not eat it
        if (args.Type is TownImportAdminType.Open or TownImportAdminType.Pool)
            PendingNote = null;

        ConfirmingEnd = false;
        ClockElapsedMs = 0;
        RefreshAll();

        if (args.Type == TownImportAdminType.Open)
            Show();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        //hiding resets the children's interaction state, which drops the text boxes' focus
        base.Hide();
        Preview.ReleaseFrames();
        ConfirmingEnd = false;
        RefreshEnd();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        switch (e.Keycode)
        {
            case Keycode.Escape:
                Hide();
                e.Handled = true;

                break;

            //single-line boxes let Enter bubble up to here (UITextBox only takes it when multiline)
            case Keycode.Enter when SearchBox.IsFocused:
                SearchBox.IsFocused = false;
                Find();
                e.Handled = true;

                break;

            case Keycode.Enter when PriceBox.IsFocused:
                PriceBox.IsFocused = false;
                SetPrice();
                e.Handled = true;

                break;

            default:
                base.OnKeyDown(e);

                break;
        }
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        //the running import's time left counts down while the window is open
        ClockElapsedMs += gameTime.ElapsedGameTime.TotalMilliseconds;

        if (ClockElapsedMs < CLOCK_MS)
            return;

        ClockElapsedMs = 0;
        RefreshRunning();
    }

    private void AddSelectedResult()
    {
        if (SelectedResultItem is not { } result)
            return;

        if (!Editor.Add(result))
        {
            SetStatus(
                Editor.Items.Count >= TownImportProtocol.MAX_POOL
                    ? $"The pool holds at most {TownImportProtocol.MAX_POOL} items."
                    : $"{result.Name} is already in the pool.");

            return;
        }

        //the new item is selected so its price can be set straight away
        SelectedPool = Editor.Items.Count - 1;
        PoolPage = SelectedPool / PAGE_SIZE;
        ShowSelectedPrice();
        SetStatus($"Added {result.Name} at {TownImportText.PriceText(TownImportProtocol.DEFAULT_PRICE)} gold.");
        RefreshAll();
    }

    private TownImportRow AddRow(int x, int width, int index, Action onClick)
    {
        var row = new TownImportRow(width)
        {
            X = x,
            Y = LIST_TOP + (index * ROW_HEIGHT),
            Visible = false
        };

        row.Clicked += onClick;
        AddChild(row);

        return row;
    }

    private void EndImport()
    {
        if (Running is null)
            return;

        if (!ConfirmingEnd)
        {
            ConfirmingEnd = true;
            RefreshEnd();

            return;
        }

        ConfirmingEnd = false;
        RefreshEnd();
        Send(TownImportAdminAction.EndImport);
    }

    private void Find() => Send(TownImportAdminAction.Search, query: SearchBox.Text.Trim());

    /// <summary>Loads a saved pool into the editor. The selection follows its item when the item is still in the pool.</summary>
    private void LoadPool(IReadOnlyList<TownImportItemInfo> pool)
    {
        var selectedKey = SelectedPoolItem?.TemplateKey;

        Editor.Load(pool);

        SelectedPool = selectedKey is null
            ? -1
            : Editor.Items.FindIndex(item => item.TemplateKey.Equals(selectedKey, StringComparison.OrdinalIgnoreCase));

        if (SelectedPool >= 0)
            PoolPage = SelectedPool / PAGE_SIZE;

        ShowSelectedPrice();
    }

    private void MoveSelected(int delta)
    {
        if (SelectedPoolItem is null)
            return;

        SelectedPool = Editor.Move(SelectedPool, delta);
        PoolPage = SelectedPool / PAGE_SIZE;
        RefreshAll();
    }

    private UILabel PageCaption(int x, int y, int columnWidth)
        => Caption(string.Empty, x + (2 * SMALL_BUTTON) + 10, y + 5, columnWidth - (2 * SMALL_BUTTON) - 10, color: LegendColors.Gray);

    private void RefreshAll()
    {
        RefreshTitle();
        RefreshResults();
        RefreshPool();
        RefreshRunning();
        RefreshEnd();
        RefreshPreview();
    }

    private void RefreshEnd()
    {
        EndButton.Enabled = Running is not null;

        if (!EndButton.Enabled)
            ConfirmingEnd = false;

        EndButton.Caption = ConfirmingEnd ? "Confirm end?" : "End import";
    }

    private void RefreshPool()
    {
        var items = Editor.Items;
        var pages = RefreshRows(PoolRows, items, ref PoolPage, SelectedPool, _ => null);

        PoolEmptyLabel.Visible = items.Count == 0;
        PoolPageLabel.Text = $"{PoolPage + 1}/{pages}";
        PoolPrevButton.Enabled = PoolPage > 0;
        PoolNextButton.Enabled = PoolPage < (pages - 1);
        PoolCountLabel.Text = $"{items.Count} of {TownImportProtocol.MAX_POOL} items";

        var selected = SelectedPoolItem is not null;
        SetPriceButton.Enabled = selected;
        UpButton.Enabled = selected && (SelectedPool > 0);
        DownButton.Enabled = selected && (SelectedPool < (items.Count - 1));
        RemoveButton.Enabled = selected;
        SaveButton.Enabled = Editor.Unsaved;
        RevertButton.Enabled = Editor.Unsaved;
    }

    /// <summary>
    ///     Previews the item picked last, from either list; with none, the running import; with neither, the viewer as they
    ///     are.
    /// </summary>
    private void RefreshPreview()
    {
        var item = (PreviewFromPool ? SelectedPoolItem : SelectedResultItem) ?? Running?.Item;
        var look = ViewerLook();

        Preview.SetLook(item is null ? look : TownImportPreviewLook.Apply(in look, item.Look));
        PreviewCaption.Text = item is null ? "PREVIEW" : BankItemRow.FitToLabel(PreviewCaption, item.Name);
    }

    private void RefreshResults()
    {
        //a result already in the pool says so in place of its (zero) price
        var pages = RefreshRows(
            ResultRows,
            Results,
            ref ResultsPage,
            SelectedResult,
            item => Editor.Items.Any(each => each.TemplateKey.Equals(item.TemplateKey, StringComparison.OrdinalIgnoreCase))
                ? "in pool"
                : null);

        ResultsEmptyLabel.Visible = Results.Count == 0;
        ResultsEmptyLabel.Text = Searched ? "Nothing found." : "Search for a cosmetic.";
        ResultsPageLabel.Text = $"{ResultsPage + 1}/{pages}";
        ResultsPrevButton.Enabled = ResultsPage > 0;
        ResultsNextButton.Enabled = ResultsPage < (pages - 1);
        AddItemButton.Enabled = SelectedResultItem is not null;
    }

    /// <summary>Binds one page of <paramref name="items" /> to <paramref name="rows" />, clamping the page. Returns the page count.</summary>
    private static int RefreshRows(
        TownImportRow[] rows,
        IReadOnlyList<TownImportItemInfo> items,
        ref int page,
        int selected,
        Func<TownImportItemInfo, string?> note)
    {
        var pages = Math.Max(1, (items.Count + PAGE_SIZE - 1) / PAGE_SIZE);
        page = Math.Clamp(page, 0, pages - 1);

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var index = (page * PAGE_SIZE) + i;
            var item = index < items.Count ? items[index] : null;
            var row = rows[i];

            row.SetItem(item, item is null ? null : note(item));
            row.Selected = item is not null && (index == selected);
        }

        return pages;
    }

    private void RefreshRunning()
    {
        if (Running is not { } running)
        {
            RunningLabels[0].Text = "No import is running.";
            RunningLabels[0].ForegroundColor = LegendColors.Gray;

            for (var i = 1; i < RUNNING_LINES; i++)
                RunningLabels[i].Text = string.Empty;

            return;
        }

        RunningLabels[0].Text = BankItemRow.FitToLabel(RunningLabels[0], running.Item.Name);
        RunningLabels[0].ForegroundColor = LegendColors.White;
        RunningLabels[1].Text = $"Price: {TownImportText.PriceText(running.Item.Price)}";
        RunningLabels[2].Text = $"Left: {TownImportText.TimeLeft(running.EndsUtc, DateTime.UtcNow)}";
        RunningLabels[3].Text = string.Create(CultureInfo.InvariantCulture, $"Sold: {running.Sold:N0}");
        RunningLabels[4].Text = string.Create(CultureInfo.InvariantCulture, $"Gold: {running.Gold:N0}");
    }

    private void RefreshTitle()
    {
        var town = Towns.FirstOrDefault(each => each.Key.Equals(TownKey, StringComparison.OrdinalIgnoreCase));
        var name = town?.Name is { Length: > 0 } townName ? townName : TownKey;
        var title = name.Length == 0 ? "Town Imports" : $"Town Imports - {name}";

        TitleLabel.Text = Editor.Unsaved ? title + " (unsaved)" : title;

        //the town switch hugs the title's text
        var showSwitch = Towns.Count > 1;
        var halfText = (TextRenderer.MeasureWidth(TitleLabel.Text) + 1) / 2;

        TownPrevButton.Visible = showSwitch;
        TownNextButton.Visible = showSwitch;
        TownPrevButton.X = (Width / 2) - halfText - 6 - SMALL_BUTTON;
        TownNextButton.X = (Width / 2) + halfText + 6;
    }

    private void RemoveSelected()
    {
        if (SelectedPoolItem is null)
            return;

        Editor.Remove(SelectedPool);

        //the next item slides into the removed one's place and takes the selection, so several can go in a row
        SelectedPool = Math.Min(SelectedPool, Editor.Items.Count - 1);
        ShowSelectedPrice();
        SetStatus(string.Empty);
        RefreshAll();
    }

    /// <summary>Clears everything a closed window forgets before it opens again.</summary>
    private void Reset()
    {
        SelectedPool = -1;
        SelectedResult = -1;
        PoolPage = 0;
        ResultsPage = 0;
        PreviewFromPool = false;
        Searched = false;
        PendingNote = null;
        SearchBox.Text = string.Empty;
        PriceBox.Text = string.Empty;
    }

    private void Revert()
    {
        var selectedKey = SelectedPoolItem?.TemplateKey;

        Editor.Revert();

        //the saved order can differ, so follow the item by key; clear the selection when it is gone
        SelectedPool = selectedKey is null
            ? -1
            : Editor.Items.FindIndex(item => item.TemplateKey.Equals(selectedKey, StringComparison.OrdinalIgnoreCase));

        if (SelectedPool >= 0)
            PoolPage = SelectedPool / PAGE_SIZE;

        ShowSelectedPrice();
        SetStatus("Changes undone.");
        RefreshAll();
    }

    private void Save() => Send(TownImportAdminAction.Save, entries: Editor.ToEntries());

    private void SelectPool(int index)
    {
        if ((index < 0) || (index >= Editor.Items.Count))
            return;

        SelectedPool = index;
        PreviewFromPool = true;
        ShowSelectedPrice();
        RefreshAll();
    }

    private void SelectResult(int index)
    {
        if ((index < 0) || (index >= Results.Count))
            return;

        SelectedResult = index;
        PreviewFromPool = false;
        RefreshAll();
    }

    private void Send(TownImportAdminAction action, string? townKey = null, string query = "", IReadOnlyList<TownImportPoolEntry>? entries = null)
        => RequestSent?.Invoke(
            new TownImportAdminInteractionArgs
            {
                Action = action,
                TownKey = townKey ?? TownKey,
                Query = query,
                Entries = entries ?? []
            });

    /// <summary>Sets the selected pool item's price from the price box. Bad text changes nothing and says so.</summary>
    private void SetPrice()
    {
        if (SelectedPoolItem is null)
        {
            SetStatus("Select an item in the pool first.");

            return;
        }

        if (!Editor.SetPrice(SelectedPool, PriceBox.Text))
        {
            SetStatus(BAD_PRICE);

            return;
        }

        ShowSelectedPrice();
        SetStatus(string.Empty);
        RefreshAll();
    }

    private void SetResults(IReadOnlyList<TownImportItemInfo> results, bool searched)
    {
        Results = results;
        Searched = searched;
        SelectedResult = -1;
        ResultsPage = 0;
    }

    /// <summary>The status line: one line sits level with the footer buttons' captions, two start at the row's top.</summary>
    private void SetStatus(string text)
    {
        StatusLabel.Text = text;

        var innerWidth = StatusLabel.Width - StatusLabel.PaddingLeft - StatusLabel.PaddingRight;
        var oneLine = TextRenderer.WrapText(text, innerWidth).Count <= 1;
        StatusLabel.Y = oneLine ? FooterTop + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2) : FooterTop - 2;
    }

    private void SetTown(TownImportAdminArgs args)
    {
        Towns = args.Towns;
        TownKey = args.TownKey;
    }

    private void ShowSelectedPrice()
        => PriceBox.Text = SelectedPoolItem is { } item ? TownImportText.PriceText(item.Price) : string.Empty;

    /// <summary>Asks for the next or previous town's pool; the reply (Pool) switches the window over and drops any edits.</summary>
    private void SwitchTown(int delta)
    {
        if (Towns.Count < 2)
            return;

        var index = -1;

        for (var i = 0; i < Towns.Count; i++)
            if (Towns[i].Key.Equals(TownKey, StringComparison.OrdinalIgnoreCase))
                index = i;

        //the list wraps round; a town not in the list starts from the first
        var next = index < 0 ? 0 : (((index + delta) % Towns.Count) + Towns.Count) % Towns.Count;

        if (Editor.Unsaved)
        {
            PendingNote = "Unsaved changes were dropped.";
            SetStatus(PendingNote);
        }

        ConfirmingEnd = false;
        RefreshEnd();
        Send(TownImportAdminAction.Load, Towns[next].Key);
    }

    private void TurnPoolPage(int delta)
    {
        PoolPage += delta;
        RefreshPool();
    }

    private void TurnResultsPage(int delta)
    {
        ResultsPage += delta;
        RefreshResults();
    }
}
