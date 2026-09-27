#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Data.Models;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Profile;

/// <summary>
///     Events/quest tab page (_nui_ev). Four display pages, each with two columns (EV1 left, EV2 right).
///     Each column corresponds to one SEvent file (circle level). NEXT/PREV buttons cycle through pages.
///     Each row uses the _nui_ski prefab with a leicon.epf state icon (completed/available/unavailable).
///     Layout: Page 0 = SEvent1|SEvent2, Page 1 = SEvent3|SEvent4, Page 2 = SEvent5|SEvent6+7,
///     Page 3 = SEvent8 (Medenia quests)|SEvent9+ (Medenia dailies).
/// </summary>
/// <remarks>
///     The prefab only has three backgrounds, each with its column headings baked in. Every page paints those
///     headings over and draws its own as labels, so all four pages share one font. The Medenia page reuses the
///     third background.
/// </remarks>
public sealed class SelfProfileEventMetadataTab : PrefabPanel
{
    private const int ROW_HEIGHT = 45;
    private const int MAX_DISPLAY_PAGES = 4;
    private const int COLUMNS_PER_PAGE = 2;
    private const int MAX_DISPLAY_SLOTS = MAX_DISPLAY_PAGES * COLUMNS_PER_PAGE;
    private const int BACKGROUND_FRAMES = 3;

    //matches the baked-in heading text on the prefab backgrounds
    private static readonly Color HeadingColor = new(181, 162, 123);

    //the left and right column headings of each display page
    private static readonly (string Left, string Right)[] PageHeadings =
    [
        ("Quest Log", "11-40"),
        ("41-70", "71-98"),
        ("99+", "Master"),
        ("Medenia", "Medenia Daily")
    ];

    //per background frame, the baked-in headings to paint over: plain header texture is copied from sourceX onto
    //destinationX, over the same rows. Each area covers the heading text and its shadow
    private static readonly (int SourceX, int DestinationX, int Width, int Top, int Bottom)[][] HeadingPatches =
    [
        [(150, 30, 83, 9, 31), (420, 525, 43, 10, 27)], //"Quest Log", "11-40"
        [(150, 30, 44, 11, 28), (420, 523, 45, 11, 28)], //"41-70", "71-98"
        [(150, 30, 36, 11, 29), (420, 506, 62, 13, 30)] //"99+", "Master"
    ];

    //8 display slots (4 pages x 2 columns), each holding events for that slot
    private readonly List<EventMetadataEntry>[] DisplaySlots = new List<EventMetadataEntry>[MAX_DISPLAY_SLOTS];
    //the prefab backgrounds with their baked-in headings painted over, built on first use and owned here
    private readonly Texture2D?[] CleanBackgrounds = new Texture2D?[BACKGROUND_FRAMES];
    private readonly UILabel LeftHeading;
    private readonly VirtualizedRowList<EventMetadataEntry> LeftList;
    private readonly UIButton? NextButton;
    private readonly UIButton? PrevButton;
    private readonly UILabel RightHeading;
    private readonly VirtualizedRowList<EventMetadataEntry> RightList;

    private BaseClass BaseClass;
    private HashSet<string> CompletedEventIds = new(StringComparer.OrdinalIgnoreCase);
    private int CurrentPage;
    private bool EnableMasterQuests;

    public SelfProfileEventMetadataTab(string prefabName)
        : base(prefabName, false)
    {
        Name = prefabName;
        Visible = false;

        for (var i = 0; i < MAX_DISPLAY_SLOTS; i++)
            DisplaySlots[i] = [];

        var leftRect = GetRect("EV1");
        var rightRect = GetRect("EV2");

        if (leftRect == Rectangle.Empty)
            leftRect = new Rectangle(
                32,
                33,
                233,
                239);

        if (rightRect == Rectangle.Empty)
            rightRect = new Rectangle(
                331,
                33,
                233,
                239);

        LeftList = CreateColumn(leftRect);
        RightList = CreateColumn(rightRect);

        LeftHeading = CreateHeading("LeftHeading", 32, HorizontalAlignment.Left);
        RightHeading = CreateHeading("RightHeading", 365, HorizontalAlignment.Right);

        NextButton = CreateButton("NEXT");
        PrevButton = CreateButton("PREV");

        if (NextButton is not null)
            NextButton.Clicked += () =>
            {
                if (CurrentPage < (MAX_DISPLAY_PAGES - 1))
                {
                    CurrentPage++;
                    ShowCurrentPage();
                }
            };

        if (PrevButton is not null)
            PrevButton.Clicked += () =>
            {
                if (CurrentPage > 0)
                {
                    CurrentPage--;
                    ShowCurrentPage();
                }
            };

        //event state depends on the player's attrs/completed events, which can change while the tab is hidden, so
        //re-bind both columns whenever the tab becomes visible again.
        VisibilityChanged += visible =>
        {
            if (visible)
            {
                LeftList.Invalidate();
                RightList.Invalidate();
            }
        };
    }

    /// <summary>
    ///     Clears all event entries.
    /// </summary>
    public void ClearAll()
    {
        for (var i = 0; i < MAX_DISPLAY_SLOTS; i++)
            DisplaySlots[i] = [];

        CompletedEventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CurrentPage = 0;
        ShowCurrentPage();
    }

    private VirtualizedRowList<EventMetadataEntry> CreateColumn(Rectangle columnRect)
    {
        //overscanRows:1 gives the bottom "peek" row; the generic clips it to the column bounds.
        var list = new VirtualizedRowList<EventMetadataEntry>(
            columnRect.Width,
            columnRect.Height,
            ROW_HEIGHT,
            () =>
            {
                var row = new EventMetadataEntryControl();
                row.OnClicked += (entry, state) => OnEntryClicked?.Invoke(entry, state);

                return row;
            },
            BindRow,
            overscanRows: 1);

        var viewer = new ScrollViewerControl(list)
        {
            X = columnRect.X,
            Y = columnRect.Y,
            Width = columnRect.Width,
            Height = columnRect.Height
        };

        AddChild(viewer);

        return list;
    }

    private void BindRow(UIElement row, EventMetadataEntry entry, bool selected)
        => ((EventMetadataEntryControl)row).SetEntry(entry, ResolveEventState(entry));

    /// <summary>
    ///     Fired when any entry row is clicked. Passes the entry and its resolved state.
    /// </summary>
    public event EventMetadataClickedHandler? OnEntryClicked;

    private EventState ResolveEventState(EventMetadataEntry entry)
    {
        //completed: player has a legend mark with key matching this event's id
        if (!string.IsNullOrEmpty(entry.Id) && CompletedEventIds.Contains(entry.Id))
            return EventState.Completed;

        var attrs = WorldState.Attributes.Current;
        var playerLevel = attrs?.Level ?? 1;

        //derive player's circle from level; master flag overrides to circle 6
        var playerCircle = EnableMasterQuests
            ? 6
            : playerLevel switch
            {
                >= 99 => 5,
                >= 71 => 4,
                >= 41 => 3,
                >= 11 => 2,
                _     => 1
            };

        //check qualifying circles — player's circle must be in the list
        if (!string.IsNullOrEmpty(entry.QualifyingCircles))
        {
            var circleChar = (char)('0' + playerCircle);

            if (!entry.QualifyingCircles.Contains(circleChar))
                return EventState.Unavailable;
        }

        //check qualifying classes — player's class must be in the list
        if (!string.IsNullOrEmpty(entry.QualifyingClasses))
        {
            var classChar = (char)('0' + (int)BaseClass);

            if (!entry.QualifyingClasses.Contains(classChar))
                return EventState.Unavailable;
        }

        //check prerequisite event — must be completed
        if (!string.IsNullOrEmpty(entry.PreRequisiteId) && !CompletedEventIds.Contains(entry.PreRequisiteId))
            return EventState.Unavailable;

        return EventState.Available;
    }

    /// <summary>
    ///     Sets the event entries from parsed metadata, distributed into display slots.
    /// </summary>
    public void SetEvents(
        IReadOnlyList<EventMetadataEntry> events,
        HashSet<string> completedEventIds,
        BaseClass baseClass,
        bool enableMasterQuests)
    {
        CompletedEventIds = completedEventIds;
        BaseClass = baseClass;
        EnableMasterQuests = enableMasterQuests;

        for (var i = 0; i < MAX_DISPLAY_SLOTS; i++)
            DisplaySlots[i] = [];

        //distribute events to display slots
        //slot layout: 0=sevent1, 1=sevent2, 2=sevent3, 3=sevent4, 4=sevent5, 5=sevent6+7, 6=sevent8, 7=sevent9+
        foreach (var entry in events)
        {
            DisplaySlots[GetSlotIndex(entry.Page)]
                .Add(entry);
        }

        CurrentPage = 0;
        ShowCurrentPage();
    }

    //binds each column to its current page's display slot; SetItems resets that column's scroll to the top.
    private void ShowCurrentPage()
    {
        //the Medenia page has no background of its own and reuses the last one
        var frame = Math.Min(CurrentPage, BACKGROUND_FRAMES - 1);
        Background = CleanBackgrounds[frame] ??= BuildCleanBackground(frame);

        var (left, right) = PageHeadings[CurrentPage];
        LeftHeading.Text = left;
        RightHeading.Text = right;

        var leftSlot = CurrentPage * COLUMNS_PER_PAGE;
        var rightSlot = leftSlot + 1;

        LeftList.SetItems(leftSlot < MAX_DISPLAY_SLOTS ? DisplaySlots[leftSlot] : []);
        RightList.SetItems(rightSlot < MAX_DISPLAY_SLOTS ? DisplaySlots[rightSlot] : []);
    }

    //sevent6 and sevent7 share the master column, and anything past sevent9 lands in the Medenia daily column
    private static int GetSlotIndex(int page)
        => page switch
        {
            <= 5 => Math.Max(page - 1, 0),
            <= 7 => 5,
            8    => 6,
            _    => 7
        };

    private UILabel CreateHeading(string name, int x, HorizontalAlignment alignment)
    {
        var heading = new UILabel
        {
            Name = name,
            X = x,
            Y = 12,
            Width = 200,
            Height = 16,
            HorizontalAlignment = alignment,
            ShadowStyle = ShadowStyle.BothSides,
            IsHitTestVisible = false
        };

        heading.ForegroundColor = HeadingColor;
        AddChild(heading);

        return heading;
    }

    /// <summary>
    ///     Copies one prefab background and paints over both baked-in headings with plain header texture taken from
    ///     the same rows, so the page's headings can be drawn as labels in their place.
    /// </summary>
    private Texture2D BuildCleanBackground(int frame)
    {
        SetBackgroundFrame(frame);

        //the prefab texture is shared through the UiRenderer cache, so it is copied rather than changed in place
        var source = Background!;
        using var scope = new PixelBufferScope(source);

        if (scope.Width >= 568)
            foreach (var (sourceX, destinationX, width, top, bottom) in HeadingPatches[frame])
                CopyColumns(scope, sourceX, destinationX, width, top, bottom);

        var texture = new Texture2D(source.GraphicsDevice, scope.Width, scope.Height);
        scope.CommitTo(texture);

        return texture;
    }

    private static void CopyColumns(
        PixelBufferScope scope,
        int sourceX,
        int destinationX,
        int width,
        int top,
        int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            var row = y * scope.Width;
            Array.Copy(scope.Pixels, row + sourceX, scope.Pixels, row + destinationX, width);
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        //base.Dispose only disposes whichever background is showing; the painted-over ones are owned here either
        //way. Disposing a texture twice is harmless
        for (var i = 0; i < CleanBackgrounds.Length; i++)
        {
            CleanBackgrounds[i]?.Dispose();
            CleanBackgrounds[i] = null;
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (!Visible || !Enabled)
            return;

        base.Update(gameTime);
    }
}
