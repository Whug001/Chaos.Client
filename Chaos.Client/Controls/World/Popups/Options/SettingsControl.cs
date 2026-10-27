#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Extensions;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Options;

/// <summary>
///     Settings panel (F4). Re-skinned into the ornate dialog frame (FramedDialogPanelBase) with a clickable category
///     list on the left (Display, Map Effects, Damage Numbers, Sound and Film, Controls, Hotbars, Chat) and the
///     selected category's rows on the right, driven by <see cref="SettingDefinitions" />. The right column is
///     single-column: every row is full width. Same footprint as the legacy _nsett panel (the prefab is still used
///     for sizing + the OK button; its background is replaced by the frame); the window is widened past the prefab
///     only when the list plus the longest one-column label needs it, never past the viewport height limit. Taller
///     categories scroll instead. The rows live inside a clipped <see cref="Viewport" />/<see cref="Content" /> pair
///     scrolled by a right-edge <see cref="ScrollBar" />; the scrollbar is always shown and renders dormant (no thumb,
///     ignores input) while the content fits, and becomes active automatically if a category overflows.
/// </summary>
public sealed class SettingsControl : FramedDialogPanelBase
{
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    //pixels scrolled per wheel notch / arrow click.
    private const int SCROLL_STEP = SettingsLayout.RowHeight;

    private readonly Dictionary<SettingKey, CustomCheckBox> Checkboxes = [];
    private readonly Dictionary<SettingKey, CustomComboBox> Combos = [];
    private readonly Dictionary<SettingKey, SliderControl> Sliders = [];
    private readonly Dictionary<SettingKey, UILabel> SliderReadouts = [];
    private readonly Dictionary<SettingPanelCategory, CustomButton> CategoryButtons = [];
    private readonly Dictionary<SettingPanelCategory, UIPanel> CategoryPanels = [];
    private readonly Dictionary<SettingPanelCategory, int> CategoryHeights = [];
    private readonly Dictionary<int, UIPanel> HotbarBarPanels = [];
    private readonly Dictionary<int, CustomButton> HotbarPickerButtons = [];
    private readonly UserOptions Options;

    //scrolled surface; its Y is offset negatively to scroll. One child panel per category, toggled on selection.
    private readonly UIPanel Content;

    //clip + scroll host wrapping Content; implements IVerticalScrollable in SCROLL_STEP units.
    private readonly SettingsViewport Viewport;

    //owns the always-visible scrollbar (dormant while content fits) + the mouse wheel; clips/sizes Viewport per frame.
    private readonly ScrollViewerControl Viewer;

    //the last-picked category and hotbar bar. Instance state, so reopening F4 shows them again; never saved to disk.
    private SettingPanelCategory SelectedCategory = SettingPanelCategory.Display;
    private int SelectedHotbar;

    private SlideAnimator Slide;
    private int SlideAnchorY;
    private bool SlideMode;

    public SettingsControl(UserOptions options, int maxHeight)
        : base("_nsett", false)
    {
        Options = options;
        Name = "Settings";
        Visible = false;
        UsesControlStack = true;

        OkButton = CreateButton("OK");

        if (OkButton is not null)
            OkButton.Clicked += Close;

        //keep the prefab size when the list plus the longest one-column label fits; widen otherwise, capped at maxHeight so taller categories scroll.
        (Width, Height) = SettingsLayout.ResolveWindowSize(Width, Height, maxHeight);

        BuildCategoryList();

        Content = new UIPanel
        {
            Name = "SettingsContent",
            IsPassThrough = true
        };

        Viewport = new SettingsViewport(Content, SCROLL_STEP)
        {
            Name = "SettingsViewport",
            IsPassThrough = true
        };
        Viewport.AddChild(Content);

        //the viewer sits right of the category list; it reserves the scrollbar gutter on the right, so the resolved
        //content width matches the rows and the bar lands at the same X as the pre-migration scrollbar.
        Viewer = new ScrollViewerControl(Viewport) { ContentRightPadding = SettingsLayout.GutterGap };
        Viewer.X = SettingsLayout.ContentLeft + SettingsLayout.ListWidth + SettingsLayout.ListGap;
        Viewer.Y = SettingsLayout.ContentTop;
        Viewer.Width = Width - Viewer.X - SettingsLayout.ContentRight;
        Viewer.Height = SettingsLayout.ViewerHeight(Height);
        AddChild(Viewer);

        BuildCategoryPanels();

        //Viewport's X/Y/Width/Height are set by the viewer each frame; seed Height now so the host reports a correct
        //viewport on the first frame (the wheel handler runs before the viewer's first Update sizes it).
        Viewport.Height = Viewer.Height;

        Content.X = 0;
        Content.Y = 0;
        Content.Width = SettingsLayout.RightContentWidth(Width);
        Content.Height = 0; //set by SelectCategory below

        SelectHotbar(SelectedHotbar);
        SelectCategory(SelectedCategory);

        if (OkButton is not null)
        {
            OkButton.X = Width - OkButton.Width - OK_RIGHT_MARGIN;
            OkButton.Y = Height - OkButton.Height - OK_BOTTOM_MARGIN;
        }

        RefreshAll();

        Options.ValueChanged += OnValueChanged;

        //UserOptions is the source of truth for every checkbox (server-synced for server/group options, persisted for
        //client-local ones). The checkboxes' visual state is intentionally cleared by the ResetInteractionState that
        //UIPanel fires on hide, so re-seed from UserOptions each time the panel opens.
        VisibilityChanged += visible =>
        {
            if (visible)
                RefreshAll();
        };
    }

    //The left list: one wooden button per category, the picked one lit gold.
    private void BuildCategoryList()
    {
        foreach (var category in SettingDefinitions.PanelCategories)
        {
            var index = CategoryButtons.Count;
            var button = new CustomButton(SettingDefinitions.CategoryDisplayName(category), SettingsLayout.ListWidth)
            {
                Name = $"cat_{category}",
                X = SettingsLayout.ContentLeft,
                Y = SettingsLayout.ContentTop + (index * SettingsLayout.CategoryStep)
            };

            button.Clicked += () => SelectCategory(category);
            CategoryButtons[category] = button;
            AddChild(button);
        }
    }

    //Builds one panel per category (all bars for Hotbars) once; switching only toggles visibility, so the
    //Checkboxes/Combos/Sliders dictionaries stay correct for RefreshAll.
    private void BuildCategoryPanels()
    {
        var contentWidth = SettingsLayout.RightContentWidth(Width);

        foreach (var category in SettingDefinitions.PanelCategories)
        {
            var panel = new UIPanel
            {
                Name = $"panel_{category}",
                X = 0,
                Y = 0,
                Width = contentWidth,
                IsPassThrough = true,
                Visible = false
            };

            panel.Height = category == SettingPanelCategory.Hotbars
                ? BuildHotbarPanel(panel, contentWidth)
                : BuildCategorySectionRows(panel, category, contentWidth);

            CategoryPanels[category] = panel;
            CategoryHeights[category] = panel.Height;
            Content.AddChild(panel);
        }
    }

    //Builds every section of a plain category into `panel`; returns the panel height. Matches
    //SettingsLayout.CategoryContentHeight so the no-scroll layout test holds by construction.
    private int BuildCategorySectionRows(UIPanel panel, SettingPanelCategory category, int contentWidth)
    {
        var y = 0;
        var first = true;

        foreach (var section in SettingDefinitions.CategorySections(category))
        {
            if (!first)
                y += SettingsLayout.SectionGap;

            first = false;

            if (SettingsLayout.ShowsSectionHeader(category, section))
            {
                AddHeader(panel, section, y, contentWidth);
                y += SettingsLayout.HeaderHeight;
            }

            foreach (var definition in SettingDefinitions.PanelRows(section))
            {
                (var cell, var height) = BuildSettingCell(definition, contentWidth);
                cell.X = 0;
                cell.Y = y;
                panel.AddChild(cell);
                y += height;

                if (definition.Help is not null)
                    y += AddHelp(panel, definition, y, contentWidth);
            }
        }

        return y;
    }

    private static void AddHeader(UIPanel panel, SettingSection section, int y, int contentWidth)
        => panel.AddChild(
            new UILabel
            {
                Name = $"Header_{section}",
                X = 0,
                Y = y,
                Width = contentWidth,
                Height = SettingsLayout.HeaderHeight,
                PaddingLeft = 0,
                PaddingTop = 0,
                ForegroundColor = LegendColors.White,
                Text = SettingsLayout.SectionTitle(section)
            });

    //The Hotbars category: the "Solid hotbars" row, a row of four small bar buttons ("1".."4", the picked one
    //lit), then only the picked bar's five rows under its "Hotbar N" header. Matches
    //SettingsLayout.CategoryContentHeight so the no-scroll layout test holds by construction.
    private int BuildHotbarPanel(UIPanel panel, int contentWidth)
    {
        var y = 0;

        (var solid, var solidHeight) = BuildSettingCell(SettingDefinitions.ByKey(SettingKey.SolidHotbars), contentWidth);
        solid.X = 0;
        solid.Y = y;
        panel.AddChild(solid);
        y += solidHeight + SettingsLayout.SectionGap;

        for (var bar = 0; bar < 4; bar++)
        {
            var picked = bar;
            var button = new CustomButton((bar + 1).ToString(), SettingsLayout.HotbarButtonWidth)
            {
                Name = $"hotbar_{bar}",
                X = bar * (SettingsLayout.HotbarButtonWidth + SettingsLayout.HotbarButtonGap),
                Y = y
            };

            button.Clicked += () => SelectHotbar(picked);
            HotbarPickerButtons[bar] = button;
            panel.AddChild(button);
        }

        y += SettingsLayout.HotbarPickerRowHeight + SettingsLayout.SectionGap;

        for (var bar = 0; bar < 4; bar++)
        {
            var section = SettingsLayout.HotbarSection(bar);
            var barPanel = new UIPanel
            {
                Name = $"barpanel_{bar}",
                X = 0,
                Y = y,
                Width = contentWidth,
                IsPassThrough = true,
                Visible = bar == SelectedHotbar
            };

            var barY = 0;
            AddHeader(barPanel, section, barY, contentWidth);
            barY += SettingsLayout.HeaderHeight;

            foreach (var definition in SettingDefinitions.PanelRows(section))
            {
                (var cell, var height) = BuildSettingCell(definition, contentWidth);
                cell.X = 0;
                cell.Y = barY;
                barPanel.AddChild(cell);
                barY += height;
            }

            barPanel.Height = barY;
            HotbarBarPanels[bar] = barPanel;
            panel.AddChild(barPanel);
        }

        //every bar holds the same five widget kinds, so every bar panel is the same height.
        return y + HotbarBarPanels[0].Height;
    }

    private void SelectCategory(SettingPanelCategory category)
    {
        SelectedCategory = category;

        foreach ((var other, var panel) in CategoryPanels)
            panel.Visible = other == category;

        foreach ((var other, var button) in CategoryButtons)
            button.Selected = other == category;

        Content.Height = CategoryHeights[category];
        ((IVerticalScrollable)Viewport).VerticalOffset = 0;
    }

    private void SelectHotbar(int bar)
    {
        SelectedHotbar = Math.Clamp(bar, 0, 3);

        foreach ((var other, var panel) in HotbarBarPanels)
            panel.Visible = other == SelectedHotbar;

        foreach ((var other, var button) in HotbarPickerButtons)
            button.Selected = other == SelectedHotbar;
    }

    /// <summary>
    ///     Word-wraps help paragraphs to <paramref name="width" /> pixels. Returns every wrapped line in order plus the
    ///     block's total height (a line per help line height plus the gap above and below); an empty list
    ///     is zero height.
    /// </summary>
    public static (IReadOnlyList<string> Lines, int Height) HelpLayout(IReadOnlyList<string> paragraphs, int width)
        => SettingsLayout.HelpBlock(paragraphs, width);

    //Mounts the setting's help lines (dim, one UILabel per wrapped line) on `parent` starting at `y`; returns the block height.
    private static int AddHelp(UIPanel parent, SettingDefinition def, int y, int width)
    {
        (var lines, var height) = HelpLayout(def.Help!, width);

        for (var i = 0; i < lines.Count; i++)
            parent.AddChild(
                new UILabel
                {
                    Name = $"help_{def.Key}_{i}",
                    X = 0,
                    Y = y + SettingsLayout.HelpGap + (i * SettingsLayout.HelpLineHeight),
                    Width = width,
                    Height = SettingsLayout.HelpLineHeight,
                    PaddingLeft = 0,
                    PaddingRight = 0,
                    PaddingTop = 0,
                    PaddingBottom = 0,
                    TruncateWithEllipsis = false,
                    ForegroundColor = LegendColors.Gray,
                    Text = lines[i]
                });

        return height;
    }

    //Builds the control(s) for one setting into a pass-through cell container sized to `width`,
    //and returns the cell plus its height. This is the ONLY place that knows a setting renders as a
    //checkbox — a future slider/dropdown/keybinding becomes a new branch here and nowhere else.
    private (UIPanel cell, int height) BuildSettingCell(SettingDefinition def, int width)
    {
        if (def.GetSliderValue is not null)
            return BuildSliderCell(def, width);

        if (def.Choices is not null)
            return BuildDropdownCell(def, width);

        var key = def.Key;

        var checkbox = new CustomCheckBox
        {
            Name = $"cb_{def.Key}",
            X = 0,
            Y = 0,
            Width = width,
            Height = SettingsLayout.RowHeight,
            Text = def.Label
        };
        checkbox.Clicked += () => Options.Toggle(key);

        Checkboxes[def.Key] = checkbox;

        return (checkbox, SettingsLayout.RowHeight);
    }

    //Builds a label + dropdown row for a multi-choice setting. The combobox is tracked in Combos so
    //RefreshAll can re-sync its selection from the model when the panel reopens. The combobox's open
    //list root-mounts to the screen (escaping the scroll-clip) and is modal by construction.
    private (UIPanel cell, int height) BuildDropdownCell(SettingDefinition def, int width)
    {
        //place the dropdown just to the right of the (text-width) label and size it to its widest
        //option (clamped to the row), rather than filling the whole row width.
        var labelW = TextRenderer.MeasureWidth(def.Label) + 2;
        var comboX = labelW + SettingsLayout.LabelGap;
        var comboW = Math.Max(1, Math.Min(CustomComboBox.MeasureRequiredWidth(def.Choices!), width - comboX));
        var combo = new CustomComboBox(comboW)
        {
            Name = $"combo_{def.Key}"
        };

        var rowH = Math.Max(SettingsLayout.RowHeight, combo.Height);
        combo.X = comboX;
        combo.Y = (rowH - combo.Height) / 2;
        combo.SetItems(def.Choices!, def.GetChoice?.Invoke() ?? 0);
        combo.SelectionChanged += i => def.SetChoice?.Invoke(i);
        Combos[def.Key] = combo;

        var cell = MakeLabeledCell(def, width, rowH, labelW);
        cell.AddChild(combo);

        return (cell, rowH);
    }

    //Creates a pass-through row cell of `height` with the setting's left-aligned label already mounted.
    //Shared by the dropdown and slider builders; callers mount their widget at labelW + LABEL_GAP.
    private static UIPanel MakeLabeledCell(SettingDefinition def, int width, int height, int labelW)
    {
        var cell = new UIPanel
        {
            Name = $"cell_{def.Key}",
            Width = width,
            Height = height,
            IsPassThrough = true
        };

        cell.AddChild(
            new UILabel
            {
                Name = $"lbl_{def.Key}",
                X = 0,
                Y = 0,
                Width = labelW,
                Height = height,
                PaddingLeft = 0,
                PaddingRight = 0,
                PaddingTop = 0,
                ForegroundColor = TextColors.Default,
                Text = def.Label
            });

        return cell;
    }

    //Builds a label + 0-10 slider + live numeric readout for an integer setting. The slider is tracked in
    //Sliders (its readout in SliderReadouts) so RefreshAll can re-sync from the model on reopen, mirroring Combos.
    private (UIPanel cell, int height) BuildSliderCell(SettingDefinition def, int width)
    {
        var labelW = TextRenderer.MeasureWidth(def.Label) + 2;
        var trackX = labelW + SettingsLayout.LabelGap;
        var initial = def.GetSliderValue!();

        var cell = MakeLabeledCell(def, width, SettingsLayout.RowHeight, labelW);

        var thumb = UiRenderer.Instance!.GetEpfTexture("option04.epf", 0);
        var trackRect = new Rectangle(trackX, (SettingsLayout.RowHeight - TRACK_HEIGHT) / 2, SettingsLayout.SliderTrackWidth, TRACK_HEIGHT);
        var slider = new SliderControl(trackRect, thumb, drawTrack: true) { Name = $"slider_{def.Key}" };
        slider.SetValue(initial);

        var readout = new UILabel
        {
            Name = $"val_{def.Key}",
            X = trackX + SettingsLayout.SliderTrackWidth + SettingsLayout.LabelGap + SettingsLayout.SliderReadoutGap,
            Y = 0,
            Width = SettingsLayout.SliderReadoutWidth,
            Height = SettingsLayout.RowHeight,
            PaddingLeft = 0,
            PaddingRight = 0,
            PaddingTop = 0,
            ForegroundColor = TextColors.Default,
            Text = initial.ToString()
        };

        slider.ValueChanged += v =>
        {
            def.SetSliderValue?.Invoke(v);
            readout.Text = v.ToString();
        };

        Sliders[def.Key] = slider;
        SliderReadouts[def.Key] = readout;

        cell.AddChild(slider);
        cell.AddChild(readout);

        return (cell, SettingsLayout.RowHeight);
    }

    private const int TRACK_HEIGHT = 4;

    private void OnValueChanged(SettingKey key, bool value)
    {
        if (Checkboxes.TryGetValue(key, out var checkbox))
            checkbox.Checked = value;

        //server-controlled choices (the chat filter) land here on the server's echo, after the pick.
        if (Combos.TryGetValue(key, out var combo) && SettingDefinitions.ByKey(key).GetChoice is { } getChoice)
            combo.SelectedIndex = getChoice();

        RefreshGatedStates();
    }

    private void RefreshAll()
    {
        foreach ((var key, var checkbox) in Checkboxes)
            checkbox.Checked = Options.Value(key);

        foreach ((var key, var combo) in Combos)
        {
            var def = SettingDefinitions.ByKey(key);

            if (def.GetChoice is not null)
                combo.SelectedIndex = def.GetChoice();
        }

        foreach ((var key, var slider) in Sliders)
        {
            var value = SettingDefinitions.ByKey(key).GetSliderValue!();
            slider.SetValue(value);

            if (SliderReadouts.TryGetValue(key, out var readout))
                readout.Text = value.ToString();
        }

        RefreshGatedStates();
    }

    //grey out + lock any setting whose GatedBy master is off. The UICheckBox dims its own box + caption
    //when Enabled is false; the input system already skips !Enabled elements, so it also becomes non-interactive.
    private void RefreshGatedStates()
    {
        foreach (var def in SettingDefinitions.All)
        {
            if (def.GatedBy is not { } master)
                continue;

            if (Checkboxes.TryGetValue(def.Key, out var checkbox))
                checkbox.Enabled = Options.Value(master);

            if (Combos.TryGetValue(def.Key, out var combo))
                combo.Enabled = Options.Value(master);
        }
    }

    private void Close()
    {
        if (SlideMode)
        {
            InputDispatcher.Instance?.RemoveControl(this);
            Slide.SlideOut();
        } else
        {
            Hide();
            OnClose?.Invoke();
        }
    }

    public override void Hide()
    {
        InputDispatcher.Instance?.RemoveControl(this);

        if (SlideMode)
            Slide.Hide(this);
        else
            Visible = false;
    }

    public override void Dispose()
    {
        Options.ValueChanged -= OnValueChanged;

        base.Dispose();
    }

    public event CloseHandler? OnClose;

    public void SetSlideAnchor(int anchorX, int anchorY)
    {
        Slide.SetSlideAnchor(anchorX, Width);
        SlideAnchorY = anchorY;
    }

    /// <summary>Shows immediately at top-center of screen (hotkey mode).</summary>
    public override void Show()
    {
        this.CenterHorizontallyOnScreen();
        Y = 0;
        InputDispatcher.Instance?.PushControl(this);
        Visible = true;
        SlideMode = false;
    }

    /// <summary>Slides out from the left edge of MainOptionsControl (button mode).</summary>
    public void SlideIn()
    {
        if (Visible)
            return;

        Y = SlideAnchorY;
        InputDispatcher.Instance?.PushControl(this);
        Slide.SlideIn(this);
        SlideMode = true;
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

        //Escape cancels (label key); F4 is the positional command hotkey that toggles settings.
        if ((e.Keycode == Keycode.Escape) || (e.Scancode == Scancode.F4))
        {
            Close();
            e.Handled = true;
        }
    }

    //-- scroll host --
    //The clip + scroll surface the ScrollViewerControl hosts. The viewer forces this element's X/Y to 0 and sizes it
    //to the viewport each frame, then drives scrolling through IVerticalScrollable; we translate the unit offset into
    //the inner Content surface's pixel Y (one unit = SCROLL_STEP px), preserving the pre-migration wheel/scrollbar
    //feel. Units mirror the old scrollbar exactly: VerticalViewport = Height/STEP visible units; the hidden remainder
    //is ceil(overflow / STEP); the extent is their sum (so extent - viewport = the old MaxValue).
    private sealed class SettingsViewport(UIPanel content, int step) : UIPanel, IVerticalScrollable
    {
        private int OffsetUnits;

        private int MaxScrollPx => Math.Max(0, content.Height - Height);

        int IVerticalScrollable.VerticalViewport => step > 0 ? Height / step : 0;

        int IVerticalScrollable.VerticalExtent
            => ((IVerticalScrollable)this).VerticalViewport + (step > 0 ? (MaxScrollPx + step - 1) / step : 0);

        int IVerticalScrollable.VerticalOffset
        {
            get => OffsetUnits;
            set
            {
                OffsetUnits = value;
                content.Y = -Math.Min(value * step, MaxScrollPx);
            }
        }
    }
}
