#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Market;

/// <summary>
///     The Search tab page: a three-column filter form built from real controls (text boxes, dropdowns, a checkbox, and
///     removable stat-filter chips) plus a baked-caption Search button. Sized to the content <see cref="Rectangle" />
///     handed in by <see cref="MarketControl" />; all children use coordinates local to this panel.
/// </summary>
/// <remarks>
///     Column 1 (left): item name, seller, the "only items I can use" checkbox, equipment type, and Class + Advanced
///     Class side by side. Column 2 (middle): level / ability / price ranges. Column 3 (right): the stat-filter chip
///     list with its "+ add a stat" dropdown, on its own. The Search button is centered along the bottom and raises
///     <see cref="SearchRequested" />. Pixel positions are a first pass and expected to be nudged in-client.
/// </remarks>
public sealed class MarketSearchControl : UIPanel
{
    //field/control metrics. FIELD_HEIGHT matches UIComboBox's height (CHAR_HEIGHT + 2*INNER_PAD = 22) so the
    //text boxes and the dropdowns in the same column read at the same height.
    private const int FIELD_HEIGHT = 22;
    private const int LABEL_HEIGHT = TextRenderer.CHAR_HEIGHT;
    private const int ROW_GAP = 8; //vertical gap below a labeled field before the next label

    private const int LEFT_FIELD_WIDTH = 200;
    private const int COLUMN_GAP = 28;
    private const int DROPDOWN_PAIR_GAP = 8; //horizontal gap between the side-by-side Class / Advanced Class dropdowns

    private const int RANGE_FIELD_WIDTH = 56;
    private const int RANGE_FIELD_GAP = 6;

    //column origins (panel-local). Col1 = text fields, Col2 = numeric ranges, Col3 = stat bonuses.
    private const int COL1_X = 4;
    private const int COL2_X = COL1_X + LEFT_FIELD_WIDTH + COLUMN_GAP;
    private const int RANGE_TOTAL_WIDTH = RANGE_FIELD_WIDTH * 2 + RANGE_FIELD_GAP;
    private const int COL3_X = COL2_X + RANGE_TOTAL_WIDTH + COLUMN_GAP;

    private const int SEARCH_BTN_WIDTH = 61;
    private const int SEARCH_BTN_HEIGHT = 22;
    private const int SEARCH_BTN_BOTTOM_MARGIN = 2;

    //placeholder option lists — replaced with real game data when the search backend is wired (later task).
    private static readonly string[] EquipmentTypes =
        ["Any type", "Weapon", "Armor", "Shield", "Helmet", "Earrings", "Necklace", "Ring", "Gauntlet", "Belt", "Greaves", "Boots"];

    private static readonly string[] Classes = ["Any class", "Warrior", "Rogue", "Wizard", "Priest", "Monk"];
    private static readonly string[] AdvClasses =
        ["Any", "Berserker", "Warlord", "Druid", "Adept", "Archer", "Assassin", "Plague Doctor", "Bard", "Arcanist", "Elementalist"];
    private static readonly string[] StatOptions =
    [
        "+ add a stat", "HP", "MP", "STR", "INT", "WIS", "CON", "DEX", "AC", "HIT", "DMG", "Atk Speed", "Flat Skill Dmg",
        "Flat Spell Dmg", "Skill Damage Pct", "Spell Damage Pct", "CDR", "Heal Bonus", "Heal Bonus Pct", "Magic Resistance"
    ];

    //client dropdown index -> Chaos.DarkAges EquipmentType byte. UI list omits OverArmor(3)/OverHelmet(6)/Accessory(14).
    //order: [Any, Weapon, Armor, Shield, Helmet, Earrings, Necklace, Ring, Gauntlet, Belt, Greaves, Boots]
    private static readonly byte[] EquipTypeMap = [0, 1, 2, 4, 5, 7, 8, 9, 10, 11, 12, 13];

    private const int CHIP_GAP = 2;

    //the stat-filter chips are added/removed at runtime via the "+ add a stat" dropdown, so the list and the
    //dropdown's anchor are tracked here and reflowed by RelayoutStatSection.
    private readonly List<StatFilterChip> StatChips = [];
    private CustomComboBox AddStatCombo = null!;
    private int StatSectionX;
    private int StatSectionTop;

    // Promoted filter controls — populated during BuildLeftColumn / BuildMiddleColumn
    private CustomTextBox ItemNameBox = null!;
    private CustomTextBox SellerBox = null!;
    private CustomCheckBox OnlyUsableBox = null!;
    private CustomComboBox EquipTypeCombo = null!;
    private CustomComboBox ClassCombo = null!;
    private CustomComboBox AdvClassCombo = null!;
    private CustomTextBox LevelMinBox = null!;
    private CustomTextBox LevelMaxBox = null!;
    private CustomTextBox AbilityMinBox = null!;
    private CustomTextBox AbilityMaxBox = null!;
    private CustomTextBox PriceMinBox = null!;
    private CustomTextBox PriceMaxBox = null!;

    public MarketSearchControl(Rectangle rect)
    {
        X = rect.X;
        Y = rect.Y;
        Width = rect.Width;
        Height = rect.Height;

        BuildLeftColumn();
        BuildMiddleColumn();
        BuildStatColumn();
        BuildSearchButton();
    }

    /// <summary>Raised when the Search button is clicked with the current criteria. <see cref="MarketControl" /> switches to the Results tab.</summary>
    public event Action<MarketSearchCriteria>? SearchRequested;

    /// <summary>Builds a <see cref="MarketSearchCriteria" /> from the current state of all filter controls.</summary>
    /// <remarks>
    ///     Class/AdvClass: dropdown index maps directly (0 = Any).
    ///     EquipmentType: mapped via <see cref="EquipTypeMap" /> (UI list skips OverArmor/OverHelmet/Accessory).
    ///     StatFilters: one entry per chip; MinValue is 0 until per-chip threshold editing is implemented.
    /// </remarks>
    public MarketSearchCriteria GetCriteria()
    {
        var criteria = new MarketSearchCriteria
        {
            ItemName = NullIfBlank(ItemNameBox.Text),
            SellerName = NullIfBlank(SellerBox.Text),
            OnlyUsable = OnlyUsableBox.Checked,
            EquipmentType = MapEquip(EquipTypeCombo.SelectedIndex),
            Class = (byte)Math.Max(0, ClassCombo.SelectedIndex), // -1 = nothing selected → 0 (Any)
            AdvClass = (byte)Math.Max(0, AdvClassCombo.SelectedIndex), // -1 = nothing selected → 0 (Any)
            LevelMin = ParseU16(LevelMinBox.Text),
            LevelMax = ParseU16(LevelMaxBox.Text),
            AbilityMin = ParseU16(AbilityMinBox.Text),
            AbilityMax = ParseU16(AbilityMaxBox.Text),
            PriceMin = ParseI32(PriceMinBox.Text),
            PriceMax = ParseI32(PriceMaxBox.Text),
            Page = 0
        };

        foreach (var chip in StatChips)
            if (StatKeyToMarketStat(chip.StatKey) is { } stat)
                criteria.StatFilters.Add(new MarketStatFilter { Stat = stat, MinValue = 0 }); // TODO: use the chip's threshold once per-chip threshold editing exists (StatFilterChip has no value yet)

        return criteria;
    }

    private static byte MapEquip(int index) => ((index >= 0) && (index < EquipTypeMap.Length)) ? EquipTypeMap[index] : (byte)0;
    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static ushort ParseU16(string? s) => ushort.TryParse(s, out var v) ? v : (ushort)0;
    private static int ParseI32(string? s) => int.TryParse(s, out var v) ? v : 0;

    // IMPORTANT: these keys must match the StatOptions[] label strings EXACTLY.
    // A mismatch is silent — the chip is skipped (not an error). When adding/renaming a
    // stat, update BOTH StatOptions and this switch.
    private static MarketStat? StatKeyToMarketStat(string key)
        => key switch
        {
            "HP"               => MarketStat.Hp,
            "MP"               => MarketStat.Mp,
            "STR"              => MarketStat.Str,
            "INT"              => MarketStat.Int,
            "WIS"              => MarketStat.Wis,
            "CON"              => MarketStat.Con,
            "DEX"              => MarketStat.Dex,
            "AC"               => MarketStat.Ac,
            "HIT"              => MarketStat.Hit,
            "DMG"              => MarketStat.Dmg,
            "Atk Speed"        => MarketStat.AtkSpeed,
            "Flat Skill Dmg"   => MarketStat.FlatSkillDmg,
            "Flat Spell Dmg"   => MarketStat.FlatSpellDmg,
            "Skill Damage Pct" => MarketStat.SkillDmgPct,
            "Spell Damage Pct" => MarketStat.SpellDmgPct,
            "CDR"              => MarketStat.Cdr,
            "Heal Bonus"       => MarketStat.HealBonus,
            "Heal Bonus Pct"   => MarketStat.HealBonusPct,
            "Magic Resistance" => MarketStat.MagicResist,
            _                  => null
        };

    private void BuildLeftColumn()
    {
        var y = 4;

        //item name + seller text fields
        (y, ItemNameBox) = AddLabeledField("Item name", COL1_X, y, LEFT_FIELD_WIDTH);
        (y, SellerBox) = AddLabeledField("Seller", COL1_X, y, LEFT_FIELD_WIDTH);

        //"only items I can use" checkbox, directly below the seller field (self-labeled)
        var cb = new CustomCheckBox
        {
            X = COL1_X,
            Y = y,
            Width = LEFT_FIELD_WIDTH,
            Height = CustomCheckBox.CHECKBOX_SIZE,
            Text = "Only items I can use"
        };
        cb.Clicked += () => cb.Checked = !cb.Checked;
        OnlyUsableBox = cb;
        AddChild(cb);
        y += CustomCheckBox.CHECKBOX_SIZE + ROW_GAP;

        //equipment-type dropdown, then Class + Advanced Class side by side on one row.
        //AddLabeledDropdownInline returns only the control (not a next-Y) because the two inline dropdowns share a row;
        //advancing y is the caller's responsibility. y isn't advanced after these two — this is the last row in the left column.
        (y, EquipTypeCombo) = AddLabeledDropdown("Equipment type", COL1_X, y, EquipmentTypes);

        ClassCombo = AddLabeledDropdownInline("Class", COL1_X, y, Classes);
        AdvClassCombo = AddLabeledDropdownInline("Advanced class", COL1_X + ClassCombo.Width + DROPDOWN_PAIR_GAP, y, AdvClasses);
    }

    private void BuildMiddleColumn()
    {
        var y = 4;

        //numeric range filters: caption above the two min/max boxes — consistent with the left column's label-above layout
        (y, LevelMinBox, LevelMaxBox) = AddLabeledRange("Level", COL2_X, y);
        (y, AbilityMinBox, AbilityMaxBox) = AddLabeledRange("Ability", COL2_X, y);
        (_, PriceMinBox, PriceMaxBox) = AddLabeledRange("Price", COL2_X, y);
    }

    private void BuildStatColumn()
    {
        var y = 4;

        //stat bonuses: a header, a dynamic list of removable chips, then a "+ add a stat" dropdown beneath them.
        //picking a stat from the dropdown adds a chip and reflows the list; clicking a chip removes it.
        AddChild(
            new UILabel
            {
                X = COL3_X,
                Y = y,
                Width = 120,
                Height = LABEL_HEIGHT,
                ForegroundColor = LegendColors.White,
                Text = "Stats"
            });

        //control sits flush below the label (label_y + LABEL_HEIGHT), matching AddLabeledField/Range/Dropdown so the
        //Stats dropdown aligns with the other columns' first control (no extra gap).
        y += LABEL_HEIGHT;

        StatSectionX = COL3_X;
        StatSectionTop = y;

        var addStatWidth = CustomComboBox.MeasureRequiredWidth(StatOptions);
        AddStatCombo = new CustomComboBox(addStatWidth) { X = COL3_X };
        AddStatCombo.SetItems(StatOptions);
        AddStatCombo.SelectionChanged += OnStatSelected;
        AddChild(AddStatCombo);

        RelayoutStatSection();
    }

    private void BuildSearchButton()
    {
        var search = new UIButton
        {
            X = (Width - SEARCH_BTN_WIDTH) / 2,
            Y = Height - SEARCH_BTN_HEIGHT - SEARCH_BTN_BOTTOM_MARGIN,
            Width = SEARCH_BTN_WIDTH,
            Height = SEARCH_BTN_HEIGHT,
            NormalTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf", 51) //frame 51 = "Search"
        };
        search.Clicked += () => SearchRequested?.Invoke(GetCriteria());
        AddChild(search);
    }

    /// <summary>Adds a chip for the picked stat (ignoring the placeholder row), dedupes it, then resets the prompt.</summary>
    private void OnStatSelected(int index)
    {
        if ((index > 0) && (index < StatOptions.Length))
        {
            var stat = StatOptions[index];

            if (!StatChips.Exists(c => c.StatKey == stat))
                AddStatChip(stat);
        }

        //snap the dropdown back to the "+ add a stat" prompt so the same stat can be re-picked after removal
        AddStatCombo.SelectedIndex = 0;
    }

    private void AddStatChip(string stat)
    {
        var chip = new StatFilterChip(stat) { X = StatSectionX };
        chip.Removed += () => RemoveStatChip(chip);
        StatChips.Add(chip);
        AddChild(chip);
        RelayoutStatSection();
    }

    private void RemoveStatChip(StatFilterChip chip)
    {
        if (!StatChips.Remove(chip))
            return;

        Children.Remove(chip);
        chip.Dispose();
        RelayoutStatSection();
    }

    /// <summary>Stacks the chips under the header and parks the "+ add a stat" dropdown beneath the last chip.</summary>
    private void RelayoutStatSection()
    {
        var y = StatSectionTop;

        foreach (var chip in StatChips)
        {
            chip.X = StatSectionX;
            chip.Y = y;
            y += chip.Height + CHIP_GAP;
        }

        AddStatCombo.X = StatSectionX;
        AddStatCombo.Y = y; //flush under the header (or CHIP_GAP below the last chip) — no extra offset
    }

    /// <summary>Adds a caption label then a full-width <see cref="CustomTextBox" /> below it; returns the next free Y and the created text box.</summary>
    private (int NextY, CustomTextBox Field) AddLabeledField(string caption, int x, int y, int width)
    {
        AddChild(
            new UILabel
            {
                X = x,
                Y = y,
                Width = width,
                Height = LABEL_HEIGHT,
                ForegroundColor = LegendColors.White,
                Text = caption
            });

        var field = new CustomTextBox
        {
            X = x,
            Y = y + LABEL_HEIGHT,
            Width = width,
            Height = FIELD_HEIGHT,
            MaxLength = 32
        };
        AddChild(field);

        return (y + LABEL_HEIGHT + FIELD_HEIGHT + ROW_GAP, field);
    }

    /// <summary>Adds a caption label (full left-column width) then a <see cref="CustomComboBox" /> below it; returns the next free Y and the created combo box.</summary>
    private (int NextY, CustomComboBox Box) AddLabeledDropdown(string caption, int x, int y, IReadOnlyList<string> items)
    {
        AddChild(
            new UILabel
            {
                X = x,
                Y = y,
                Width = LEFT_FIELD_WIDTH,
                Height = LABEL_HEIGHT,
                ForegroundColor = LegendColors.White,
                Text = caption
            });

        var width = CustomComboBox.MeasureRequiredWidth(items);
        var box = new CustomComboBox(width)
        {
            X = x,
            Y = y + LABEL_HEIGHT
        };
        box.SetItems(items);
        AddChild(box);

        return (y + LABEL_HEIGHT + box.Height + ROW_GAP, box);
    }

    /// <summary>
    ///     Adds a labeled dropdown whose caption is sized to the dropdown (not the full column), for packing two
    ///     dropdowns onto one row. Returns the created <see cref="CustomComboBox" /> so the caller can read its Width/Height
    ///     to position the next control.
    /// </summary>
    private CustomComboBox AddLabeledDropdownInline(string caption, int x, int y, IReadOnlyList<string> items)
    {
        var width = CustomComboBox.MeasureRequiredWidth(items);

        //the caption can be wider than the dropdown (e.g. "Advanced class" over a short option list) — size the
        //label to whichever is wider so it isn't truncated. +4 covers UILabel's default 1px padding on each side.
        var labelWidth = Math.Max(width, TextRenderer.MeasureWidth(caption) + 4);

        AddChild(
            new UILabel
            {
                X = x,
                Y = y,
                Width = labelWidth,
                Height = LABEL_HEIGHT,
                ForegroundColor = LegendColors.White,
                Text = caption
            });

        var box = new CustomComboBox(width)
        {
            X = x,
            Y = y + LABEL_HEIGHT
        };
        box.SetItems(items);
        AddChild(box);

        return box;
    }

    /// <summary>
    ///     Adds a caption label then a "[min] [max]" pair of short <see cref="CustomTextBox" />es on the line below it
    ///     (label-above layout, matching <see cref="AddLabeledField" /> / <see cref="AddLabeledDropdown" />); returns the
    ///     next free Y and both text boxes.
    /// </summary>
    private (int NextY, CustomTextBox MinBox, CustomTextBox MaxBox) AddLabeledRange(string caption, int x, int y)
    {
        AddChild(
            new UILabel
            {
                X = x,
                Y = y,
                Width = RANGE_TOTAL_WIDTH,
                Height = LABEL_HEIGHT,
                ForegroundColor = LegendColors.White,
                Text = caption
            });

        var fieldsY = y + LABEL_HEIGHT;

        var minBox = new CustomTextBox
        {
            X = x,
            Y = fieldsY,
            Width = RANGE_FIELD_WIDTH,
            Height = FIELD_HEIGHT,
            MaxLength = 9,
            HintText = "min"
        };
        AddChild(minBox);

        var maxBox = new CustomTextBox
        {
            X = x + RANGE_FIELD_WIDTH + RANGE_FIELD_GAP,
            Y = fieldsY,
            Width = RANGE_FIELD_WIDTH,
            Height = FIELD_HEIGHT,
            MaxLength = 9,
            HintText = "max"
        };
        AddChild(maxBox);

        return (y + LABEL_HEIGHT + FIELD_HEIGHT + ROW_GAP, minBox, maxBox);
    }
}
