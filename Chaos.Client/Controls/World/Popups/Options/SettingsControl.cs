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

    private readonly Dictionary<SettingKey, CustomCheckBox> Checkboxes = [];
    private readonly Dictionary<SettingKey, CustomComboBox> Combos = [];
    private readonly UserOptions Options;

    //scrolled surface; its Y is offset negatively to scroll. Rows are children of this.
    private readonly UIPanel Content;

    //clip + scroll host wrapping Content; implements IVerticalScrollable in SCROLL_STEP units.
    private readonly SettingsViewport Viewport;

    //owns the always-visible scrollbar (dormant while content fits) + the mouse wheel; clips/sizes Viewport per frame.
    private readonly ScrollViewerControl Viewer;

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

        //the viewer clips Viewport to its bounds, owns the always-visible scrollbar (dormant while content fits) and
        //the wheel, and drives scrolling via IVerticalScrollable. ContentRightPadding reproduces the original
        //GUTTER_GAP between the rows and the scrollbar.
        Viewer = new ScrollViewerControl(Viewport) { ContentRightPadding = GUTTER_GAP };
        AddChild(Viewer);

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
            SettingSection.Display       => "Display",
            SettingSection.DamageNumbers => "Damage Numbers",
            SettingSection.Sound         => "Sound",
            SettingSection.Interaction   => "Interaction",
            _                            => string.Empty
        };

    private void BuildRows()
    {
        var contentW = Width - CONTENT_LEFT - CONTENT_RIGHT - ScrollBarControl.DEFAULT_WIDTH - GUTTER_GAP;
        var viewportH = Height - CONTENT_TOP - FRAME_BOTTOM;
        var columnW = contentW / 2;
        var y = 0;

        foreach (var section in (ReadOnlySpan<SettingSection>)[SettingSection.Display, SettingSection.DamageNumbers, SettingSection.Sound, SettingSection.Interaction])
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
                    ForegroundColor = LegendColors.White,
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

        //the viewer spans from the left content edge to the right frame margin; it reserves the scrollbar gutter
        //(DEFAULT_WIDTH) + GUTTER_GAP on the right, so its resolved content width equals contentW (matching the rows),
        //and the bar lands at the same X as the pre-migration scrollbar.
        Viewer.X = CONTENT_LEFT;
        Viewer.Y = CONTENT_TOP;
        Viewer.Width = Width - CONTENT_LEFT - CONTENT_RIGHT;
        Viewer.Height = viewportH;

        //Viewport's X/Y/Width/Height are set by the viewer each frame; seed Height now so the host reports a correct
        //viewport on the first frame (the wheel handler runs before the viewer's first Update sizes it).
        Viewport.Height = viewportH;

        Content.X = 0;
        Content.Y = 0;
        Content.Width = contentW;
        Content.Height = contentHeight;

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

        var halfLabelWidth = columnW - CustomCheckBox.CHECKBOX_SIZE - CustomCheckBox.CAPTION_GAP;

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

        var key = def.Key;

        var checkbox = new CustomCheckBox
        {
            Name = $"cb_{def.Key}",
            X = 0,
            Y = 0,
            Width = width,
            Height = ROW_HEIGHT,
            Text = def.Label
        };
        checkbox.Clicked += () => Options.Toggle(key);

        Checkboxes[def.Key] = checkbox;

        return (checkbox, ROW_HEIGHT);
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
        var comboW = Math.Min(CustomComboBox.MeasureRequiredWidth(def.Choices!), width - comboX);
        var combo = new CustomComboBox(comboW)
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

    private void OnValueChanged(SettingKey key, bool value)
    {
        if (Checkboxes.TryGetValue(key, out var checkbox))
            checkbox.Checked = value;

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

        if (e.Key is Keys.Escape or Keys.F4)
        {
            Close();
            e.Handled = true;
        }
    }

    //── scroll host ─────────────────────────────────────────────────────────────────────────────────────────────
    //The clip + scroll surface the ScrollViewerControl hosts. The viewer forces this element's X/Y to 0 and sizes it
    //to the viewport each frame, then drives scrolling through IVerticalScrollable; we translate the unit offset into
    //the inner Content surface's pixel Y (one unit = SCROLL_STEP px), preserving the pre-migration wheel/scrollbar
    //feel. Units mirror the old scrollbar exactly: VerticalViewport = Height/STEP visible units; the hidden remainder
    //is ceil(overflow / STEP); the extent is their sum (so extent − viewport = the old MaxValue).
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