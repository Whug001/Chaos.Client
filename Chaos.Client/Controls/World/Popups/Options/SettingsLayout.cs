#region
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
#endregion

namespace Chaos.Client.Controls.World.Popups.Options;

/// <summary>
///     Pure geometry for the F4 settings window: row heights, category content heights, and the keep/widen/cap
///     window-size rule. No graphics-device state, so the layout tests drive it directly and
///     <see cref="SettingsControl" /> builds from the same numbers.
/// </summary>
public static class SettingsLayout
{
    public const int ContentLeft = 22;
    public const int ContentTop = 28;
    public const int ContentRight = 22;

    //height of the frame's bottom border (mirrors FramedDialogPanelBase.BORDER_BOTTOM, which is private); the
    //scrollable viewer ends above it so scrolled content never overlaps the OK/rivet edge.
    public const int FrameBottom = 47;

    //spacer between the content rows and the scrollbar gutter.
    public const int GutterGap = 3;

    public const int ScrollBarWidth = ScrollBarControl.DEFAULT_WIDTH;
    public const int RowHeight = 21;
    public const int HeaderHeight = 16;
    public const int SectionGap = 4;
    public const int LabelGap = 6;
    public const int HelpGap = 3;

    //left category list: gap between the list and the right column.
    public const int ListGap = 10;

    //the Hotbars category's bar picker: four small buttons in one row.
    public const int HotbarButtonWidth = 36;
    public const int HotbarButtonGap = 6;

    //the F4 window never grows past the 640x480 viewport.
    public const int MaxWindowWidth = 640;
    public const int MaxWindowHeight = 480;

    //dropdown rows mirror CustomComboBox (CHAR_HEIGHT + 2 * its inner pad); the row takes the taller of the
    //label line and the combo header.
    private const int ComboHeight = TextRenderer.CHAR_HEIGHT + 10;

    //slider rows mirror BuildSliderCell: label, 90px track, readout.
    public const int SliderTrackWidth = 90;
    public const int SliderReadoutWidth = 18;
    public const int SliderReadoutGap = 6;

    public static readonly int HelpLineHeight = TextRenderer.CHAR_HEIGHT;
    public static readonly int DropdownRowHeight = Math.Max(RowHeight, ComboHeight);
    public static readonly int HotbarPickerRowHeight = CustomButton.HEIGHT;

    //vertical step of the left category list (mirrors KeyBindingsControl's category step).
    public static readonly int CategoryStep = CustomButton.HEIGHT + 4;

    //widest category name plus room for the centered caption and the frame.
    public static readonly int ListWidth = SettingDefinitions.PanelCategories
                                                              .Max(category => TextRenderer.MeasureWidth(SettingDefinitions.CategoryDisplayName(category)))
                                         + 24;

    public static string SectionTitle(SettingSection section)
        => section switch
        {
            SettingSection.Display       => "Display",
            SettingSection.MapEffects    => "Triggering or Unsettling Map Effects",
            SettingSection.DamageNumbers => "Damage Numbers",
            SettingSection.Sound         => "Sound",
            SettingSection.Cinema        => "Cinema",
            SettingSection.Interaction   => "Interaction",
            SettingSection.Hotbars       => "Hotbars",
            SettingSection.Hotbar1       => "Hotbar 1",
            SettingSection.Hotbar2       => "Hotbar 2",
            SettingSection.Hotbar3       => "Hotbar 3",
            SettingSection.Hotbar4       => "Hotbar 4",
            SettingSection.Chat          => "Chat",
            _                            => string.Empty
        };

    /// <summary>The hotbar section for a 0-3 bar index (clamped).</summary>
    public static SettingSection HotbarSection(int hotbar)
        => Math.Clamp(hotbar, 0, 3) switch
        {
            0 => SettingSection.Hotbar1,
            1 => SettingSection.Hotbar2,
            2 => SettingSection.Hotbar3,
            _ => SettingSection.Hotbar4
        };

    /// <summary>
    ///     Whether <paramref name="section" /> shows its header above its rows. A multi-section category shows each
    ///     section's header; a single-section category shows it only when it differs from the category name (so
    ///     Display hides "Display" while Map effects keeps "Triggering or Unsettling Map Effects"). The Hotbars
    ///     category lays out its sections around the bar picker and does not use this.
    /// </summary>
    public static bool ShowsSectionHeader(SettingPanelCategory category, SettingSection section)
    {
        var sections = SettingDefinitions.CategorySections(category);

        return (sections.Count > 1)
            || (SectionTitle(section) != SettingDefinitions.CategoryDisplayName(category));
    }

    /// <summary>Pixel height of one setting row (checkbox, dropdown, or slider).</summary>
    public static int SettingRowHeight(SettingDefinition definition)
    {
        if (definition.GetSliderValue is not null)
            return RowHeight;

        if (definition.Choices is not null)
            return DropdownRowHeight;

        return RowHeight;
    }

    /// <summary>Pixel width one full-width row needs: label plus its widget.</summary>
    public static int RequiredRowWidth(SettingDefinition definition)
    {
        if (definition.GetSliderValue is not null)
            return TextRenderer.MeasureWidth(definition.Label) + 2 + LabelGap + SliderTrackWidth + LabelGap + SliderReadoutGap + SliderReadoutWidth;

        if (definition.Choices is not null)
            return TextRenderer.MeasureWidth(definition.Label) + 2 + LabelGap + CustomComboBox.MeasureRequiredWidth(definition.Choices);

        return CustomCheckBox.CHECKBOX_SIZE + CustomCheckBox.CAPTION_GAP + TextRenderer.MeasureWidth(definition.Label);
    }

    /// <summary>Pixel width the right column needs so the longest one-column row fits.</summary>
    public static int RequiredContentWidth()
    {
        var widest = 0;

        foreach (var definition in SettingDefinitions.All.Where(definition => !definition.Hidden))
            widest = Math.Max(widest, RequiredRowWidth(definition));

        return widest;
    }

    /// <summary>Word-wraps help paragraphs to <paramref name="width" /> pixels plus the block's total height.</summary>
    public static (IReadOnlyList<string> Lines, int Height) HelpBlock(IReadOnlyList<string> paragraphs, int width)
    {
        var lines = paragraphs.SelectMany(paragraph => TextRenderer.WrapLines(paragraph, width))
                              .ToList();

        return (lines, lines.Count == 0 ? 0 : (lines.Count * HelpLineHeight) + (2 * HelpGap));
    }

    private static int HelpBlockHeight(IReadOnlyList<string>? help, int contentWidth)
        => help is null ? 0 : HelpBlock(help, contentWidth).Height;

    /// <summary>
    ///     Total pixel height of <paramref name="category" />'s rows at <paramref name="contentWidth" />: section
    ///     headers, rows, help blocks, and (for Hotbars) the bar picker plus the picked bar's section.
    /// </summary>
    public static int CategoryContentHeight(SettingPanelCategory category, int hotbar, int contentWidth)
    {
        if (category == SettingPanelCategory.Hotbars)
        {
            var height = SettingRowHeight(SettingDefinitions.ByKey(SettingKey.SolidHotbars));
            height += SectionGap + HotbarPickerRowHeight + SectionGap + HeaderHeight;

            foreach (var definition in SettingDefinitions.PanelRows(HotbarSection(hotbar)))
                height += SettingRowHeight(definition);

            return height;
        }

        var sections = SettingDefinitions.CategorySections(category);
        var total = 0;
        var first = true;

        foreach (var section in sections)
        {
            if (!first)
                total += SectionGap;

            first = false;

            if (ShowsSectionHeader(category, section))
                total += HeaderHeight;

            foreach (var definition in SettingDefinitions.PanelRows(section))
                total += SettingRowHeight(definition) + HelpBlockHeight(definition.Help, contentWidth);
        }

        return total;
    }

    /// <summary>Tallest category at <paramref name="contentWidth" />, checking every hotbar choice.</summary>
    public static int MaxCategoryContentHeight(int contentWidth)
    {
        var tallest = 0;

        foreach (var category in SettingDefinitions.PanelCategories)
            if (category == SettingPanelCategory.Hotbars)
            {
                for (var bar = 0; bar < 4; bar++)
                    tallest = Math.Max(tallest, CategoryContentHeight(category, bar, contentWidth));
            } else
            {
                tallest = Math.Max(tallest, CategoryContentHeight(category, 0, contentWidth));
            }

        return tallest;
    }

    /// <summary>Right-column width inside a <paramref name="windowWidth" />-wide window.</summary>
    public static int RightContentWidth(int windowWidth)
        => Math.Max(1, windowWidth - ContentLeft - ListWidth - ListGap - ContentRight - ScrollBarWidth - GutterGap);

    /// <summary>Scrollable height inside a <paramref name="windowHeight" />-tall window.</summary>
    public static int ViewerHeight(int windowHeight)
        => Math.Max(0, windowHeight - ContentTop - FrameBottom);

    /// <summary>Window width that leaves <paramref name="contentWidth" /> for the right column.</summary>
    public static int WindowWidthFor(int contentWidth)
        => contentWidth + ContentLeft + ListWidth + ListGap + ContentRight + ScrollBarWidth + GutterGap;

    /// <summary>
    ///     The window size that shows every category with no scrolling, before the prefab and the cap: wide enough
    ///     for the longest row, tall enough for the tallest category.
    /// </summary>
    public static (int Width, int Height) NeedsWindowSize()
    {
        var contentWidth = RequiredContentWidth();
        var width = WindowWidthFor(contentWidth);

        //help blocks wrap at the final content width; at full width they are at their shortest.
        var height = ContentTop + MaxCategoryContentHeight(contentWidth) + FrameBottom;

        return (Math.Min(width, MaxWindowWidth), Math.Min(height, MaxWindowHeight));
    }

    /// <summary>
    ///     The F4 window size for a <paramref name="prefabWidth" /> x <paramref name="prefabHeight" /> prefab: the
    ///     current size is kept when the list plus the longest one-column label fits, grown otherwise, and never
    ///     past 640 x 480 total. The window only grows, never shrinks.
    /// </summary>
    public static (int Width, int Height) ResolveWindowSize(int prefabWidth, int prefabHeight)
    {
        var (needWidth, needHeight) = NeedsWindowSize();

        return (
            Math.Min(Math.Max(prefabWidth, needWidth), MaxWindowWidth),
            Math.Min(Math.Max(prefabHeight, needHeight), MaxWindowHeight));
    }
}
