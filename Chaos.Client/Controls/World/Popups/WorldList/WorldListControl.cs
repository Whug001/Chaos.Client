#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Data.Models;
using Chaos.Client.Models;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Chaos.Extensions.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.WorldList;

/// <summary>
///     Online users list panel loaded from _nusers prefab.
///     Right-aligned, slides in from the right edge of the viewport.
///     Shows a scrollable user list and 9 filter buttons that turn through class and continent faces.
/// </summary>
public sealed class WorldListControl : PrefabPanel
{
    private const int ROW_HEIGHT = 15;
    private const int STATUS_ICON_COUNT = 8;
    private const string BUTTON_FRAMES = "_nusersb.spf";

    private readonly List<WorldListEntry> FilterBuffer = [];
    private readonly WorldListFilterState Filter = new();

    private readonly VirtualizedRowList<WorldListEntry> RowList;
    private readonly Texture2D?[] StatusIcons = new Texture2D?[STATUS_ICON_COUNT];

    //tab buttons
    private readonly UIButton[] TabButtons = new UIButton[WorldListFaces.BUTTON_COUNT];
    private readonly UILabel[] TabCountLabels = new UILabel[WorldListFaces.BUTTON_COUNT];
    private readonly UILabel TotalNumLabel;

    private readonly Rectangle UsersListRect;

    //player data
    private IReadOnlyList<WorldListEntry> AllEntries = [];
    private HashSet<string> FamilyNames = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> FriendNames = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<WorldListEntry> FilteredEntries = [];

    //slide animation
    private SlideAnimator Slide;
    private ushort TotalOnline;

    private static string PlayerName => WorldState.PlayerName;

    public WorldListControl()
        : base("_nusers", false)
    {
        Name = "WorldList";
        Visible = false;
        UsesControlStack = true;

        //position: right-aligned, starts off-screen
        Slide.SetViewportBounds(
            new Rectangle(
                0,
                0,
                640,
                480),
            Width);
        X = Slide.OffScreenX;

        //userslist rect
        UsersListRect = GetRect("UsersList");

        //count labels
        var totalNumRect = GetRect("TotalNum");
        var countryNumRect = GetRect("CountryNum");

        TotalNumLabel = new UILabel
        {
            Name = "TotalNumLabel",
            X = totalNumRect.X,
            Y = totalNumRect.Y,
            Width = totalNumRect.Width,
            Height = totalNumRect.Height,
            HorizontalAlignment = HorizontalAlignment.Right,
            PaddingLeft = 0,
            PaddingTop = 0
        };

        TotalNumLabel.ForegroundColor = Color.White;
        TotalNumLabel.Text = "0";
        AddChild(TotalNumLabel);

        //row entries hosted in a virtualized list (no selection; each row whispers on its own double-click). the row
        //width keeps the original 5px gap before the scrollbar gutter via ContentRightPadding.
        var rowWidth = UsersListRect.Width - ScrollBarControl.DEFAULT_WIDTH - 5;

        RowList = new VirtualizedRowList<WorldListEntry>(
            rowWidth,
            UsersListRect.Height,
            ROW_HEIGHT,
            () =>
            {
                var row = new WorldListEntryControl(rowWidth);
                row.OnWhisper += name => OnWhisperRequested?.Invoke(name);

                return row;
            },
            BindRow);

        var viewer = new ScrollViewerControl(RowList)
        {
            X = UsersListRect.X,
            Y = UsersListRect.Y,
            Width = UsersListRect.Width,
            Height = UsersListRect.Height,
            ContentRightPadding = 5
        };

        AddChild(viewer);

        //social status icons from _nemots.spf (frame 0 of each 3-frame group)
        LoadStatusIcons();

        //tab buttons — built from _nusersb.spf frames (9 tabs x 2 states)
        var countryBtnRect = GetRect("CountryBtn");
        var masterBtnRect = GetRect("MasterBtn");

        //spacing derived from the y gap between the first two prefab buttons
        var tabStride = masterBtnRect.Y - countryBtnRect.Y;

        if (tabStride <= 0)
            tabStride = 22;

        //label stride derived from the y gap between first two prefab labels (same stride)
        var labelStride = tabStride;

        for (var i = 0; i < WorldListFaces.BUTTON_COUNT; i++)
        {
            var button = i;

            TabButtons[i] = new UIButton
            {
                Name = $"Tab{i}",
                X = countryBtnRect.X,
                Y = countryBtnRect.Y + i * tabStride,
                Width = countryBtnRect.Width,
                Height = countryBtnRect.Height
            };

            TabButtons[i].Clicked += () => OnTabClicked(button);
            AddChild(TabButtons[i]);

            TabCountLabels[i] = new UILabel
            {
                Name = $"TabCount{i}",
                X = countryNumRect.X,
                Y = countryNumRect.Y + i * labelStride,
                Width = countryNumRect.Width,
                Height = countryNumRect.Height,
                HorizontalAlignment = HorizontalAlignment.Right,
                PaddingLeft = 0
            };

            TabCountLabels[i].ForegroundColor = Color.White;
            AddChild(TabCountLabels[i]);
        }

        RefreshTabs();

        //close button
        var closeButton = CreateButton("Close");

        if (closeButton is not null)
            closeButton.Clicked += SlideClose;

        WorldState.WorldList.Changed += OnWorldListChanged;
    }

    private void ApplyFilter()
    {
        var face = Filter.ActiveFace;

        if ((Filter.ActiveButton == 0) && (Filter.FaceOf(0) == 0))
            FilteredEntries = AllEntries;
        else
        {
            FilterBuffer.Clear();

            foreach (var entry in AllEntries)
                if (face.Matches(entry))
                    FilterBuffer.Add(entry);

            FilteredEntries = FilterBuffer;
        }

        RowList.SetItems(FilteredEntries);
    }

    private void OnTabClicked(int button)
    {
        Filter.Click(button);
        RefreshTabs();
        ApplyFilter();
        UpdateCountLabels();
    }

    //every button shows its current face's carved frames; only the active button is lit
    private void RefreshTabs()
    {
        var cache = UiRenderer.Instance!;

        for (var i = 0; i < WorldListFaces.BUTTON_COUNT; i++)
        {
            var face = Filter.CurrentFace(i);
            TabButtons[i].NormalTexture = cache.GetSpfTexture(BUTTON_FRAMES, face.NormalFrame);
            TabButtons[i].SelectedTexture = cache.GetSpfTexture(BUTTON_FRAMES, face.LitFrame);
            TabButtons[i].IsSelected = i == Filter.ActiveButton;
        }
    }

    private void AutoScrollToSelf()
    {
        if (PlayerName.Length == 0)
            return;

        for (var i = 0; i < FilteredEntries.Count; i++)
            if (FilteredEntries[i]
                .Name
                .EqualsI(PlayerName))
            {
                RowList.ScrollToIndex(i);

                return;
            }
    }

    public override void Dispose()
    {
        WorldState.WorldList.Changed -= OnWorldListChanged;

        foreach (var icon in StatusIcons)
            icon?.Dispose();

        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
    }

    public override void Hide()
    {
        InputDispatcher.Instance?.RemoveControl(this);
        Slide.Hide(this);
    }

    private void LoadStatusIcons()
    {
        var cache = UiRenderer.Instance!;

        for (var i = 0; i < STATUS_ICON_COUNT; i++)
            StatusIcons[i] = cache.GetSpfTexture("_nemots.spf", i);
    }

    private static Color MapWorldListColor(WorldListColor color)
    {
        if (color == WorldListColor.Invisble)
            return Color.Transparent;

        if (color == WorldListColor.White)
            return LegendColors.White;

        return LegendColors.Get((int)color);
    }

    public event CloseHandler? OnClose;
    public event WhisperRequestedHandler? OnWhisperRequested;

    public void SetFamilyNames(FamilyList? family)
    {
        FamilyNames.Clear();

        if (family is null)
            return;

        foreach (var name in new[]
                 {
                     family.Mother,
                     family.Father,
                     family.Son1,
                     family.Son2,
                     family.Brother1,
                     family.Brother2,
                     family.Brother3,
                     family.Brother4,
                     family.Brother5,
                     family.Brother6
                 })
            if (!string.IsNullOrWhiteSpace(name))
                FamilyNames.Add(name);
    }

    public void SetFriendNames(IReadOnlyList<string> friends)
    {
        FriendNames.Clear();

        foreach (var name in friends)
            if (!string.IsNullOrWhiteSpace(name))
                FriendNames.Add(name);
    }

    private void OnWorldListChanged() => Show(WorldState.WorldList.Entries, WorldState.WorldList.TotalOnline);

    private void BindRow(UIElement row, WorldListEntry entry, bool selected)
    {
        var nameColor = FamilyNames.Contains(entry.Name)
            ? LegendColors.HotPink
            : FriendNames.Contains(entry.Name)
                ? LegendColors.Lime
                : MapWorldListColor(entry.Color);

        var statusIdx = (int)entry.SocialStatus;
        var statusIcon = (statusIdx >= 0) && (statusIdx < StatusIcons.Length) ? StatusIcons[statusIdx] : null;

        ((WorldListEntryControl)row).SetEntry(entry, statusIcon, nameColor);
    }

    public void SetViewportBounds(Rectangle viewport)
    {
        Slide.SetViewportBounds(viewport, Width);
        Y = viewport.Y;
    }

    public void Show(IReadOnlyList<WorldListEntry> entries, ushort totalOnline)
    {
        AllEntries = entries;
        TotalOnline = totalOnline;
        Filter.ResetForOpen();
        RefreshTabs();

        ApplyFilter();
        UpdateCountLabels();
        AutoScrollToSelf();

        if (!Visible)
        {
            InputDispatcher.Instance?.PushControl(this);
            Slide.SlideIn(this);
        }
    }

    public void SlideClose()
    {
        InputDispatcher.Instance?.RemoveControl(this);
        Slide.SlideOut();
    }

    public override void Update(GameTime gameTime)
    {
        if (!Visible || !Enabled)
            return;

        if (Slide.Update(gameTime, this))
        {
            OnClose?.Invoke();

            return;
        }

        base.Update(gameTime);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (Slide.Sliding)
            return;

        if (e.Keycode == Keycode.Escape)
        {
            SlideClose();
            e.Handled = true;
        }
    }

    private void UpdateCountLabels()
    {
        TotalNumLabel.Text = $"{TotalOnline}";

        for (var i = 0; i < WorldListFaces.BUTTON_COUNT; i++)
            TabCountLabels[i].Text = $"{WorldListFaces.Count(AllEntries, Filter.CurrentFace(i))}";
    }
}