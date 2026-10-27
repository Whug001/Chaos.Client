using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.Client.Controls.World.Popups.Options;
using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class SettingDefinitionsTests
{
    /// <summary>
    ///     <c>SettingDefinitions.ByKey</c> is looked up by every toggle, so a key without a definition is a crash on
    ///     click rather than a missing row.
    /// </summary>
    [Test]
    public void EveryKeyHasADefinition()
    {
        var defined = SettingDefinitions.All
                                        .Select(definition => definition.Key)
                                        .ToHashSet();

        Enum.GetValues<SettingKey>()
            .Should()
            .OnlyContain(key => defined.Contains(key));
    }

    [Test]
    public void NoKeyIsDefinedTwice()
        => SettingDefinitions.All
                             .Select(definition => definition.Key)
                             .Should()
                             .OnlyHaveUniqueItems();

    /// <summary>
    ///     A client-local toggle with no hooks would render as a checkbox that reads false and does nothing when
    ///     clicked.
    /// </summary>
    [Test]
    public void EveryClientLocalToggleCanBeReadAndWritten()
        => SettingDefinitions.All
                             .Where(
                                 definition => (definition.Category == SettingCategory.ClientLocal)
                                               && (definition.Choices is null)
                                               && (definition.GetSliderValue is null))
                             .Should()
                             .OnlyContain(definition => (definition.Get != null) && (definition.Set != null));

    /// <summary>A setting gated by another has to be gated by one that exists, or the row never enables.</summary>
    [Test]
    public void EveryGateNamesARealSetting()
    {
        var defined = SettingDefinitions.All
                                        .Select(definition => definition.Key)
                                        .ToHashSet();

        SettingDefinitions.All
                          .Where(definition => definition.GatedBy is not null)
                          .Should()
                          .OnlyContain(definition => defined.Contains(definition.GatedBy!.Value));
    }

    /// <summary>
    ///     The column defaults to a solid plate: it is a readout to be glanced at mid-fight, and solid is what
    ///     keeps it legible over map art. Transparency is the opt-in.
    /// </summary>
    [Test]
    public void GroupPanelsAreSolidUntilTheSettingIsTurnedOn()
    {
        var wasTransparent = ClientSettings.TransparentGroupPanels;

        try
        {
            ClientSettings.TransparentGroupPanels = false;

            var definition = SettingDefinitions.ByKey(SettingKey.TransparentGroupPanel);

            definition.Category.Should().Be(SettingCategory.ClientLocal);
            definition.Section.Should().Be(SettingSection.Display);

            //no point offering to restyle a column that is switched off
            definition.GatedBy.Should().Be(SettingKey.GroupPanel);

            definition.Get!()
                      .Should()
                      .BeFalse();

            definition.Set!(true);

            ClientSettings.TransparentGroupPanels.Should().BeTrue();

            definition.Get!()
                      .Should()
                      .BeTrue();
        } finally
        {
            ClientSettings.TransparentGroupPanels = wasTransparent;
        }
    }

    /// <summary>
    ///     Chat translation setting is in the Chat section right after chat filter with the correct six choices.
    /// </summary>
    [Test]
    public void ChatLanguageIsInChatSectionAfterChatFilter()
    {
        var chatDefinitions = SettingDefinitions.PanelRows(SettingSection.Chat).ToList();

        var filterModeIndex = chatDefinitions.FindIndex(d => d.Key == SettingKey.ChatFilterMode);
        filterModeIndex.Should().BeGreaterThanOrEqualTo(0, "chat filter mode should exist in Chat section");

        var chatLanguageIndex = chatDefinitions.FindIndex(d => d.Key == SettingKey.ChatLanguage);
        chatLanguageIndex.Should().Be(
            filterModeIndex + 1,
            "chat language should be right after chat filter (configured flag is hidden)");

        var chatLanguageDef = SettingDefinitions.ByKey(SettingKey.ChatLanguage);

        chatLanguageDef.Section.Should().Be(SettingSection.Chat);
        chatLanguageDef.Category.Should().Be(SettingCategory.ServerOption);
        chatLanguageDef.Span.Should().Be(SettingSpan.Full);
        chatLanguageDef.Choices.Should().HaveCount(3);
    }

    [Test]
    public void ChatLanguageChoicesMatchWireOrder()
        => SettingDefinitions.ByKey(SettingKey.ChatLanguage).Choices.Should().Equal("Off", "English", "Korean (한국어)");

    [Test]
    public void OnlyChatLanguageHasHelp()
    {
        SettingDefinitions.ByKey(SettingKey.ChatLanguage).Help.Should().Equal(
            "Pick English to read Korean chat in English, or Korean to read English chat in Korean. Translations can be wrong.",
            "영어를 고르면 한국어 채팅을 영어로, 한국어를 고르면 영어 채팅을 한국어로 봅니다. 번역은 틀릴 수 있습니다.");

        SettingDefinitions.All
                          .Where(definition => definition.Key != SettingKey.ChatLanguage)
                          .Should()
                          .OnlyContain(definition => definition.Help == null);
    }

    // F4 categories

    [Test]
    public void PanelCategories_AreInTheRequiredOrder()
        => SettingDefinitions.PanelCategories
                             .Should()
                             .Equal(
                                 SettingPanelCategory.Display,
                                 SettingPanelCategory.MapEffects,
                                 SettingPanelCategory.DamageNumbers,
                                 SettingPanelCategory.SoundAndFilm,
                                 SettingPanelCategory.Controls,
                                 SettingPanelCategory.Hotbars,
                                 SettingPanelCategory.Chat);

    [Test]
    public void CategoryDisplayNames_MatchTheSpec()
    {
        var expected = new Dictionary<SettingPanelCategory, string>
        {
            [SettingPanelCategory.Display] = "Display",
            [SettingPanelCategory.MapEffects] = "Map Effects",
            [SettingPanelCategory.DamageNumbers] = "Damage Numbers",
            [SettingPanelCategory.SoundAndFilm] = "Sound and Film",
            [SettingPanelCategory.Controls] = "Controls",
            [SettingPanelCategory.Hotbars] = "Hotbars",
            [SettingPanelCategory.Chat] = "Chat"
        };

        foreach ((var category, var name) in expected)
            SettingDefinitions.CategoryDisplayName(category)
                               .Should()
                               .Be(name, $"category {category} has a fixed display name");
    }

    [Test]
    public void CategorySections_MatchTheSpec()
    {
        SettingDefinitions.CategorySections(SettingPanelCategory.Display).Should().Equal(SettingSection.Display);

        SettingDefinitions.CategorySections(SettingPanelCategory.MapEffects).Should().Equal(SettingSection.MapEffects);

        SettingDefinitions.CategorySections(SettingPanelCategory.DamageNumbers).Should().Equal(SettingSection.DamageNumbers);

        SettingDefinitions.CategorySections(SettingPanelCategory.SoundAndFilm)
                          .Should()
                          .Equal(SettingSection.Sound, SettingSection.Cinema);

        SettingDefinitions.CategorySections(SettingPanelCategory.Controls).Should().Equal(SettingSection.Interaction);

        SettingDefinitions.CategorySections(SettingPanelCategory.Hotbars)
                          .Should()
                          .Equal(
                              SettingSection.Hotbars,
                              SettingSection.Hotbar1,
                              SettingSection.Hotbar2,
                              SettingSection.Hotbar3,
                              SettingSection.Hotbar4);

        SettingDefinitions.CategorySections(SettingPanelCategory.Chat).Should().Equal(SettingSection.Chat);
    }

    /// <summary>
    ///     Every section in PanelSections belongs to exactly one category, and categories hold no other sections: the
    ///     categorized sections are the same multiset as PanelSections.
    /// </summary>
    [Test]
    public void EveryPanelSectionBelongsToExactlyOneCategory()
        => SettingDefinitions.PanelCategories
                             .SelectMany(SettingDefinitions.CategorySections)
                             .Should()
                             .BeEquivalentTo(SettingDefinitions.PanelSections);

    /// <summary>Every non-hidden setting is reachable from some category (the Hotbars category covers all four bars).</summary>
    [Test]
    public void EveryNonHiddenSettingIsReachableFromSomeCategory()
    {
        var reachable = new HashSet<SettingKey>();

        foreach (var category in SettingDefinitions.PanelCategories)
            if (category == SettingPanelCategory.Hotbars)
            {
                for (var bar = 0; bar < 4; bar++)
                    foreach (var definition in SettingDefinitions.CategoryRows(category, bar))
                        reachable.Add(definition.Key);
            } else
            {
                foreach (var definition in SettingDefinitions.CategoryRows(category))
                    reachable.Add(definition.Key);
            }

        SettingDefinitions.All
                          .Where(definition => !definition.Hidden)
                          .Select(definition => definition.Key)
                          .Should()
                          .BeSubsetOf(reachable);

        reachable.Should()
                 .NotContain(
                     SettingDefinitions.All
                                       .Where(definition => definition.Hidden)
                                       .Select(definition => definition.Key));
    }

    [Test]
    public void HotbarCategoryRows_HoldSolidPlusThePickedBar()
        => SettingDefinitions.CategoryRows(SettingPanelCategory.Hotbars, 1)
                             .Select(definition => definition.Key)
                             .Should()
                             .Equal(
                                 SettingKey.SolidHotbars,
                                 SettingKey.Hotbar2Visible,
                                 SettingKey.Hotbar2Vertical,
                                 SettingKey.Hotbar2Slots,
                                 SettingKey.Hotbar2Locked,
                                 SettingKey.Hotbar2Dock);

    // F4 layout (pure SettingsLayout helper, no graphics device)

    [Test]
    public void RequiredContentWidth_CoversEveryRow()
    {
        var required = SettingsLayout.RequiredContentWidth();

        foreach (var definition in SettingDefinitions.All.Where(definition => !definition.Hidden))
            SettingsLayout.RequiredRowWidth(definition)
                          .Should()
                          .BeLessThanOrEqualTo(required, $"row {definition.Key} must fit the right column");
    }

    /// <summary>
    ///     The window that fits every category must itself fit the 640x480 viewport: otherwise F4 could never show a
    ///     category without scrolling.
    /// </summary>
    [Test]
    public void WindowNeeds_FitWithinThe640x480Cap()
    {
        var (width, height) = SettingsLayout.NeedsWindowSize();

        width.Should().BeLessThanOrEqualTo(SettingsLayout.MaxWindowWidth);
        height.Should().BeLessThanOrEqualTo(SettingsLayout.MaxWindowHeight);
    }

    /// <summary>
    ///     For each category (and each hotbar choice), the rows' total height fits the viewer height at the resolved
    ///     window size resolved with the 480 cap: the prefab size is kept when it fits, grown otherwise.
    ///     Production passes a smaller viewport-derived cap, so taller categories may still scroll there.
    /// </summary>
    [Test]
    public void EveryCategory_FitsTheViewerAtMaxWindowHeight()
    {
        var prefabs = new[] { (Width: 320, Height: 240), (Width: 560, Height: 360), (Width: 800, Height: 600) };

        foreach (var prefab in prefabs)
        {
            var (width, height) = SettingsLayout.ResolveWindowSize(prefab.Width, prefab.Height, SettingsLayout.MaxWindowHeight);

            width.Should().BeLessThanOrEqualTo(SettingsLayout.MaxWindowWidth, "resolved F4 width never passes 640");
            height.Should().BeLessThanOrEqualTo(Math.Max(prefab.Height, SettingsLayout.MaxWindowHeight), "a prefab taller than the limit keeps its own height");

            var contentWidth = SettingsLayout.RightContentWidth(width);
            var viewerHeight = SettingsLayout.ViewerHeight(height);

            foreach (var category in SettingDefinitions.PanelCategories)
                if (category == SettingPanelCategory.Hotbars)
                {
                    for (var bar = 0; bar < 4; bar++)
                        SettingsLayout.CategoryContentHeight(category, bar, contentWidth)
                                      .Should()
                                      .BeLessThanOrEqualTo(
                                          viewerHeight,
                                          $"Hotbars bar {bar} must fit the viewer at resolved {width}x{height} (prefab {prefab.Width}x{prefab.Height})");
                } else
                {
                    SettingsLayout.CategoryContentHeight(category, 0, contentWidth)
                                  .Should()
                                  .BeLessThanOrEqualTo(
                                      viewerHeight,
                                      $"{category} must fit the viewer at resolved {width}x{height} (prefab {prefab.Width}x{prefab.Height})");
                }
        }
    }

    [Test]
    public void ResolveWindowSize_KeepsAFittingWindowAndNeverShrinks()
    {
        var (needW, needH) = SettingsLayout.NeedsWindowSize();

        SettingsLayout.ResolveWindowSize(needW, needH, SettingsLayout.MaxWindowHeight).Should().Be((needW, needH));
        SettingsLayout.ResolveWindowSize(needW + 40, needH + 40, SettingsLayout.MaxWindowHeight).Should().Be((needW + 40, needH + 40));
        SettingsLayout.ResolveWindowSize(needW - 100, needH, SettingsLayout.MaxWindowHeight).Should().Be((needW, needH));
    }

    [Test]
    public void ResolveWindowSize_CapsWidthAt640ButKeepsATallerPrefab()
        => SettingsLayout.ResolveWindowSize(2000, 1000, SettingsLayout.MaxWindowHeight).Should().Be((640, 1000));

    /// <summary>
    ///     The F4 window hung below the orange bar once Display outgrew the prefab: a height limit from the
    ///     viewport caps it, and the taller categories scroll through the viewer instead.
    /// </summary>
    [Test]
    public void ResolveWindowSize_CapsHeightAtTheLimitWhenContentNeedsMore()
    {
        var (needW, needH) = SettingsLayout.NeedsWindowSize();
        var maxHeight = needH - 20;
        var prefabH = maxHeight - 40;

        var (width, height) = SettingsLayout.ResolveWindowSize(needW, prefabH, maxHeight);

        height.Should().Be(maxHeight);
        SettingsLayout.MaxCategoryContentHeight(SettingsLayout.RightContentWidth(width))
                          .Should()
                          .BeGreaterThan(SettingsLayout.ViewerHeight(height));
    }

    /// <summary>
    ///     The window looked right at prefab height before, so a limit below the prefab never shrinks it: the
    ///     prefab height is kept.
    /// </summary>
    [Test]
    public void ResolveWindowSize_KeepsPrefabHeightWhenTheLimitIsSmaller()
    {
        var (needW, needH) = SettingsLayout.NeedsWindowSize();

        var (_, height) = SettingsLayout.ResolveWindowSize(needW + 40, needH + 40, needH);

        height.Should().Be(needH + 40);
    }

    /// <summary>Width behaviour is unchanged by the height limit: the same prefab resolves to the same width.</summary>
    [Test]
    public void ResolveWindowSize_WidthIgnoresTheHeightLimit()
    {
        var (cappedW, _) = SettingsLayout.ResolveWindowSize(320, 240, 200);
        var (freeW, _) = SettingsLayout.ResolveWindowSize(320, 240, SettingsLayout.MaxWindowHeight);

        cappedW.Should().Be(freeW);
    }

    /// <summary>
    ///     SettingsControl needs a graphics device to construct, so the help block's layout is tested through its pure
    ///     static helper.
    /// </summary>
    [Test]
    public void HelpLayoutWrapsEveryParagraphWithinWidthAndGrowsHeight()
    {
        var help = SettingDefinitions.ByKey(SettingKey.ChatLanguage).Help!;
        const int WIDTH = 200;

        (var lines, var height) = SettingsControl.HelpLayout(help, WIDTH);

        lines.Count.Should().BeGreaterThan(help.Count);
        lines.Should().OnlyContain(line => TextRenderer.MeasureWidth(line) <= WIDTH);
        height.Should().Be((lines.Count * TextRenderer.CHAR_HEIGHT) + 6);
        SettingsControl.HelpLayout([], WIDTH).Height.Should().Be(0);
    }
}
