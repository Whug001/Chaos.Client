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
    private readonly List<WorldListEntryControl> Rows = [];
    private readonly UILabel EmblemTooltip;
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

        //the prefab rect is 252 px tall but the drawer art paints 17 rows of 15 px, so round up to whole rows or the
        //last painted row is never filled (the 3 extra px stay inside the art's scrollbar trough)
        var listHeight = (UsersListRect.Height + ROW_HEIGHT - 1) / ROW_HEIGHT * ROW_HEIGHT;

        RowList = new VirtualizedRowList<WorldListEntry>(
            rowWidth,
            listHeight,
            ROW_HEIGHT,
            () =>
            {
                var row = new WorldListEntryControl(rowWidth);
                Rows.Add(row);

                //double-clicking your own row opens your Emblem tab; any other row whispers
                row.OnWhisper += name =>
                {
                    if (name.EqualsI(PlayerName))
                        OnEmblemBookRequested?.Invoke();
                    else
                        OnWhisperRequested?.Invoke(name);
                };

                return row;
            },
            BindRow);

        var viewer = new ScrollViewerControl(RowList)
        {
            X = UsersListRect.X,
            Y = UsersListRect.Y,
            Width = UsersListRect.Width,
            Height = listHeight,
            ContentRightPadding = 5
        };

        AddChild(viewer);

        //names the emblem under the mouse; drawn above the list and never hit-tested, so it can't steal the hover
        EmblemTooltip = new UILabel
        {
            Name = "EmblemTooltip",
            Visible = false,
            IsHitTestVisible = false,
            PaddingLeft = 1,
            PaddingTop = 1,
            BackgroundColor = new Color(0, 0, 0, 128),
            BorderColor = LegendColors.White,
            ForegroundColor = LegendColors.White,
            ZIndex = 100
        };

        AddChild(EmblemTooltip);

        //social status icons from _nemots.spf (frame 0 of each 3-frame group)
        LoadStatusIcons();

        //tab buttons — built from _nusersb.spf frames (each of the 9 buttons rotates through its own
        //WorldListFaces.Buttons faces, each face a normal/lit frame pair)
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
                PaddingLeft = 0,
                PaddingTop = 0
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
        EmblemTooltip.Visible = false;
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

    /// <summary>Raised when the player double-clicks their own row.</summary>
    public event Action? OnEmblemBookRequested;

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
        UpdateEmblemTooltip();
    }

    //read every frame, not on hover events: rows are recycled, so a wheel scroll under a still mouse changes which
    //emblem sits in the hovered cell
    private void UpdateEmblemTooltip()
    {
        WorldListEntryControl? row = null;
        UIElement? cell = null;

        foreach (var candidate in Rows)
            if (candidate.HoveredEmblem is { } hovered)
            {
                row = candidate;
                cell = hovered;

                break;
            }

        if ((row is null) || (cell is null))
        {
            EmblemTooltip.Visible = false;

            return;
        }

        var text = row.EmblemName;

        if (EmblemTooltip.Text != text)
        {
            EmblemTooltip.Text = text;
            EmblemTooltip.Width = TextRenderer.MeasureWidth(text) + 4;
            EmblemTooltip.Height = TextRenderer.CHAR_HEIGHT + 4;
        }

        //right edge on the cell's right edge, just above it; below it when the top of the drawer would cut it off
        var cellX = cell.ScreenX - ScreenX;
        var cellY = cell.ScreenY - ScreenY;
        var x = Math.Clamp(cellX + cell.Width - EmblemTooltip.Width, 0, Math.Max(0, Width - EmblemTooltip.Width));
        var y = cellY - EmblemTooltip.Height - 1;

        if (y < 0)
            y = cellY + cell.Height + 1;

        EmblemTooltip.X = x;
        EmblemTooltip.Y = y;
        EmblemTooltip.Visible = true;
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