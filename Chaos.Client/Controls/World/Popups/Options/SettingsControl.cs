#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#endregion

namespace Chaos.Client.Controls.World.Popups.Options;

/// <summary>
///     Settings panel (F4). Re-skinned into the ornate dialog frame (FramedDialogPanelBase) with checkbox rows grouped into
///     Display / Sound / Interaction sections, driven by <see cref="SettingDefinitions" />. Same footprint as the legacy
///     _nsett panel (the prefab is still used for sizing + the OK button; its background is replaced by the frame). The rows
///     live inside a clipped <see cref="Viewport" />/<see cref="Content" /> pair scrolled by a right-edge
///     <see cref="ScrollBar" />; the scrollbar is always shown and renders dormant (no thumb, ignores input) while the
///     content fits, and becomes active automatically once the setting list grows past the viewport.
/// </summary>
public sealed class SettingsControl : FramedDialogPanelBase
{
    private const int CONTENT_LEFT = 22;
    private const int CONTENT_TOP = 28;
    private const int CONTENT_RIGHT = 22;
    private const int ROW_HEIGHT = 21;
    private const int HEADER_HEIGHT = 16;
    private const int SECTION_GAP = 4;
    private const int LABEL_GAP = 6;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    //height of the frame's bottom border (mirrors FramedDialogPanelBase.BORDER_BOTTOM, which is private); the
    //scrollable viewport ends above it so scrolled content never overlaps the OK/rivet edge.
    private const int FRAME_BOTTOM = 47;

    //spacer between the content columns and the scrollbar gutter.
    private const int GUTTER_GAP = 3;

    //pixels scrolled per wheel notch / arrow click.
    private const int SCROLL_STEP = ROW_HEIGHT;

    private readonly Dictionary<SettingKey, UICheckBox> Checkboxes = [];
    private readonly Dictionary<SettingKey, UIComboBox> Combos = [];
    private readonly UserOptions Options;

    //clip window (fixed) holding the scrolled content surface.
    private readonly UIPanel Viewport;

    //scrolled surface; its Y is offset negatively to scroll. Rows are children of this.
    private readonly UIPanel Content;

    //always-visible right-edge scrollbar; dormant while content fits.
    private readonly ScrollBarControl ScrollBar;

    //max pixel offset (content height - viewport height); clamps ApplyScrollOffset.
    private int MaxScrollOffset;

    private SlideAnimator Slide;
    private int SlideAnchorY;
    private bool SlideMode;

    public SettingsControl(UserOptions options)
        : base("_nsett", false)
    {
        Options = options;
        Name = "Settings";
        Visible = false;
        UsesControlStack = true;

        OkButton = CreateButton("OK");

        if (OkButton is not null)
            OkButton.Clicked += Close;

        Viewport = new UIPanel
        {
            Name = "SettingsViewport",
            IsPassThrough = true
        };

        Content = new UIPanel
        {
            Name = "SettingsContent",
            IsPassThrough = true
        };

        Viewport.AddChild(Content);
        AddChild(Viewport);

        ScrollBar = new ScrollBarControl
        {
            Name = "SettingsScrollBar"
        };

        ScrollBar.OnValueChanged += ApplyScrollOffset;
        AddChild(ScrollBar);

        BuildRows();
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

    private static string SectionTitle(SettingSection section)
        => section switch
        {
            SettingSection.Display     => "Display",
            SettingSection.Sound       => "Sound",
            SettingSection.Interaction => "Interaction",
            _                          => string.Empty
        };

    private void BuildRows()
    {
        var contentW = Width - CONTENT_LEFT - CONTENT_RIGHT - ScrollBarControl.DEFAULT_WIDTH - GUTTER_GAP;
        var viewportH = Height - CONTENT_TOP - FRAME_BOTTOM;
        var columnW = contentW / 2;
        var y = 0;

        foreach (var section in (ReadOnlySpan<SettingSection>)[SettingSection.Display, SettingSection.Sound, SettingSection.Interaction])
        {
            Content.AddChild(
                new UILabel
                {
                    Name = $"Header_{section}",
                    X = 0,
                    Y = y,
                    Width = contentW,
                    Height = HEADER_HEIGHT,
                    PaddingLeft = 0,
                    PaddingTop = 0,
                    ForegroundColor = TextColors.Default,
                    Text = SectionTitle(section)
                });

            y += HEADER_HEIGHT;

            var defs = SettingDefinitions.All
                                         .Where(d => d.Section == section)
                                         .ToList();

            //a Half cell holding the left column open, waiting for a right partner.
            UIPanel? pendingHalf = null;
            var pendingHalfHeight = 0;

            foreach (var def in defs)
            {
                var full = ResolveSpan(def, columnW) == SettingSpan.Full;
                var width = full ? contentW : columnW;
                (var cell, var height) = BuildSettingCell(def, width);
                Content.AddChild(cell);

                if (full)
                {
                    //close any half-open row before taking a full-width row.
                    if (pendingHalf is not null)
                    {
                        y += pendingHalfHeight;
                        pendingHalf = null;
                    }

                    cell.X = 0;
                    cell.Y = y;
                    y += height;
                } else if (pendingHalf is null)
                {
                    //left column — hold open until a right partner or the section ends.
                    cell.X = 0;
                    cell.Y = y;
                    pendingHalf = cell;
                    pendingHalfHeight = height;
                } else
                {
                    //right column — closes the row; advance by the taller of the pair.
                    cell.X = columnW;
                    cell.Y = y;
                    y += Math.Max(pendingHalfHeight, height);
                    pendingHalf = null;
                }
            }

            //flush a trailing unpaired Half.
            if (pendingHalf is not null)
                y += pendingHalfHeight;

            y += SECTION_GAP;
        }

        //bottom of the last row (drop the trailing section gap).
        var contentHeight = Math.Max(0, y - SECTION_GAP);

        Viewport.X = CONTENT_LEFT;
        Viewport.Y = CONTENT_TOP;
        Viewport.Width = contentW;
        Viewport.Height = viewportH;

        Content.X = 0;
        Content.Y = 0;
        Content.Width = contentW;
        Content.Height = contentHeight;

        ScrollBar.X = Width - CONTENT_RIGHT - ScrollBarControl.DEFAULT_WIDTH;
        ScrollBar.Y = CONTENT_TOP;
        ScrollBar.Height = viewportH;
        ScrollBar.Visible = true;

        MaxScrollOffset = Math.Max(0, contentHeight - viewportH);

        //ceil(maxOffset / step) so the final step reaches the exact bottom (ApplyScrollOffset clamps the overshoot).
        var maxValue = (MaxScrollOffset + SCROLL_STEP - 1) / SCROLL_STEP;
        var visibleItems = viewportH / SCROLL_STEP;

        //TotalItems > VisibleItems exactly when maxValue > 0 → the scrollbar self-activates; otherwise it draws dormant.
        ScrollBar.VisibleItems = visibleItems;
        ScrollBar.MaxValue = maxValue;
        ScrollBar.TotalItems = visibleItems + maxValue;
        ScrollBar.Value = 0;
        ApplyScrollOffset(0);

        if (OkButton is not null)
        {
            OkButton.X = Width - OkButton.Width - OK_RIGHT_MARGIN;
            OkButton.Y = Height - OkButton.Height - OK_BOTTOM_MARGIN;
        }
    }

    //Decides a setting's column span. Explicit Full always wins; an unmarked (Half) setting is
    //auto-promoted to Full when its label is too wide for the half-column label area, so it never
    //clips. TextRenderer.MeasureWidth is the same measurement UILabel uses for its own truncation,
    //so the threshold matches exactly what the label would clip at.
    private static SettingSpan ResolveSpan(SettingDefinition def, int columnW)
    {
        if (def.Span == SettingSpan.Full)
            return SettingSpan.Full;

        var halfLabelWidth = columnW - UICheckBox.CHECKBOX_SIZE - LABEL_GAP;

        return TextRenderer.MeasureWidth(def.Label) > halfLabelWidth
            ? SettingSpan.Full
            : SettingSpan.Half;
    }

    //Builds the control(s) for one setting into a pass-through cell container sized to `width`,
    //and returns the cell plus its height. This is the ONLY place that knows a setting renders as a
    //checkbox — a future slider/dropdown/keybinding becomes a new branch here and nowhere else.
    private (UIPanel cell, int height) BuildSettingCell(SettingDefinition def, int width)
    {
        if (def.Choices is not null)
            return BuildDropdownCell(def, width);

        var cell = new UIPanel
        {
            Name = $"cell_{def.Key}",
            Width = width,
            Height = ROW_HEIGHT,
            IsPassThrough = true
        };

        var key = def.Key;

        var checkbox = new UICheckBox
        {
            Name = $"cb_{def.Key}",
            X = 0,
            Y = (ROW_HEIGHT - UICheckBox.CHECKBOX_SIZE) / 2
        };
        checkbox.Clicked += () => Options.Toggle(key);

        Checkboxes[def.Key] = checkbox;
        cell.AddChild(checkbox);

        cell.AddChild(
            new UILabel
            {
                Name = $"lbl_{def.Key}",
                X = UICheckBox.CHECKBOX_SIZE + LABEL_GAP,
                Y = 0,
                Width = width - UICheckBox.CHECKBOX_SIZE - LABEL_GAP,
                Height = ROW_HEIGHT,
                PaddingLeft = 0,
                PaddingRight = 0,
                PaddingTop = 0,
                ForegroundColor = TextColors.Default,
                Text = def.Label
            });

        return (cell, ROW_HEIGHT);
    }

    //Builds a label + dropdown row for a multi-choice setting. The combobox is tracked in Combos so
    //RefreshAll can re-sync its selection from the model when the panel reopens. The combobox's open
    //list root-mounts to the screen (escaping the scroll-clip) and is modal by construction.
    private (UIPanel cell, int height) BuildDropdownCell(SettingDefinition def, int width)
    {
        //place the dropdown just to the right of the (text-width) label and size it to its widest
        //option (clamped to the row), rather than filling the whole row width.
        var labelW = TextRenderer.MeasureWidth(def.Label) + 2;
        var comboX = labelW + LABEL_GAP;
        var comboW = Math.Min(UIComboBox.MeasureRequiredWidth(def.Choices!), width - comboX);
        var combo = new UIComboBox(comboW)
        {
            Name = $"combo_{def.Key}"
        };

        var rowH = Math.Max(ROW_HEIGHT, combo.Height);
        combo.X = comboX;
        combo.Y = (rowH - combo.Height) / 2;
        combo.SetItems(def.Choices!, def.GetChoice?.Invoke() ?? 0);
        combo.SelectionChanged += i => def.SetChoice?.Invoke(i);
        Combos[def.Key] = combo;

        var cell = new UIPanel
        {
            Name = $"cell_{def.Key}",
            Width = width,
            Height = rowH,
            IsPassThrough = true
        };

        cell.AddChild(
            new UILabel
            {
                Name = $"lbl_{def.Key}",
                X = 0,
                Y = 0,
                Width = labelW,
                Height = rowH,
                PaddingLeft = 0,
                PaddingRight = 0,
                PaddingTop = 0,
                ForegroundColor = TextColors.Default,
                Text = def.Label
            });

        cell.AddChild(combo);

        return (cell, rowH);
    }

    private void ApplyScrollOffset(int value) => Content.Y = -Math.Min(value * SCROLL_STEP, MaxScrollOffset);

    private void OnValueChanged(SettingKey key, bool value)
    {
        if (Checkboxes.TryGetValue(key, out var checkbox))
            checkbox.Checked = value;
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

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        //dormant scrollbar: nothing to scroll.
        if (ScrollBar.TotalItems <= ScrollBar.VisibleItems)
            return;

        var newValue = Math.Clamp(ScrollBar.Value - e.Delta, 0, ScrollBar.MaxValue);

        if (newValue != ScrollBar.Value)
        {
            ScrollBar.Value = newValue;
            ApplyScrollOffset(newValue);
        }

        e.Handled = true;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (Slide.Sliding)
            return;

        if (e.Key is Keys.Escape or Keys.F4)
        {
            Close();
            e.Handled = true;
        }
    }
}