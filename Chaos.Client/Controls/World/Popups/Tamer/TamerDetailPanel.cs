#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The Creatures detail: the selected monster's portrait, name, species line, level with its experience bar, the
///     five numbers at its level, its trait and its abilities, and the Rename, Feed and Release buttons. Everything
///     comes from the state and its lookups; a protected monster shows "Unknown creature" with every action off.
/// </summary>
public sealed class TamerDetailPanel : UIPanel
{
    public const int HEIGHT = 308;
    public const int WIDTH = 370;

    private const int ABILITY_FIRST_TOP = 186;
    private const int ABILITY_MAX = 3;
    private const int ABILITY_ROW_HEIGHT = 28;
    private const int ABILITY_TEXT_TOP = 14;
    private const int BUTTON_GAP = 16;
    private const int BUTTON_TOP = 276;
    private const int BUTTON_WIDTH = 90;
    private const int HEADER_LEFT = 120;
    private const int HEADER_WIDTH = 250;
    private const int STATS_TOP = 80;
    private const int TRAIT_NAME_TOP = 116;
    private const int TRAIT_TEXT_TOP = 132;
    private const int TRAIT_TEXT_HEIGHT = 48;
    private const int XP_BAR_HEIGHT = 10;
    private const int XP_BAR_TOP = 48;
    private const int XP_TEXT_TOP = 62;

    private readonly TamerCreaturePortrait Portrait;
    private readonly UILabel NameLabel;
    private readonly UILabel MetaLabel;
    private readonly UILabel LevelLabel;
    private readonly UIProgressBar XpBar;
    private readonly UILabel XpTextLabel;
    private readonly UILabel StatsLabel;
    private readonly UILabel TraitNameLabel;
    private readonly UILabel TraitTextLabel;
    private readonly AbilityRow[] AbilityRows = new AbilityRow[ABILITY_MAX];
    private readonly CustomButton RenameButton;
    private readonly CustomButton FeedButton;
    private readonly CustomButton ReleaseButton;

    public TamerDetailPanel(CreatureRenderer creatures)
    {
        Name = "TamerDetail";
        Width = WIDTH;
        Height = HEIGHT;

        Portrait = new TamerCreaturePortrait(creatures) { X = 0, Y = 0 };
        AddChild(Portrait);

        NameLabel = Label(HEADER_LEFT, 0, HEADER_WIDTH, LegendColors.Gold);
        MetaLabel = Label(HEADER_LEFT, 16, HEADER_WIDTH, LegendColors.White);
        LevelLabel = Label(HEADER_LEFT, 32, HEADER_WIDTH, LegendColors.White);
        XpBar = new UIProgressBar
        {
            X = HEADER_LEFT,
            Y = XP_BAR_TOP,
            Width = HEADER_WIDTH,
            Height = XP_BAR_HEIGHT,
            BackgroundColor = new Color(28, 20, 12),
            BorderColor = LegendColors.DimGray,
            FillColor = LegendColors.Gold,
            IsHitTestVisible = false
        };
        XpTextLabel = Label(HEADER_LEFT, XP_TEXT_TOP, HEADER_WIDTH, LegendColors.Silver);
        StatsLabel = new UILabel
        {
            X = HEADER_LEFT,
            Y = STATS_TOP,
            Width = HEADER_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT * 2,
            ForegroundColor = LegendColors.Silver,
            WordWrap = true,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(NameLabel);
        AddChild(MetaLabel);
        AddChild(LevelLabel);
        AddChild(XpBar);
        AddChild(XpTextLabel);
        AddChild(StatsLabel);

        TraitNameLabel = Label(0, TRAIT_NAME_TOP, WIDTH, LegendColors.Gold);
        TraitTextLabel = new UILabel
        {
            X = 0,
            Y = TRAIT_TEXT_TOP,
            Width = WIDTH,
            Height = TRAIT_TEXT_HEIGHT,
            ForegroundColor = LegendColors.Silver,
            WordWrap = true,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(TraitNameLabel);
        AddChild(TraitTextLabel);

        for (var i = 0; i < AbilityRows.Length; i++)
        {
            AbilityRows[i] = new AbilityRow(ABILITY_FIRST_TOP + (i * ABILITY_ROW_HEIGHT));
            AbilityRows[i].ChooseClicked += key => AbilityChosen?.Invoke(key);
            AddChild(AbilityRows[i]);
        }

        RenameButton = Button(TamerStableText.Rename, 0, BUTTON_TOP, () => RenameClicked?.Invoke());
        FeedButton = Button(TamerStableText.Feed, BUTTON_WIDTH + BUTTON_GAP, BUTTON_TOP, () => FeedClicked?.Invoke());
        ReleaseButton = Button(TamerStableText.Release, (2 * (BUTTON_WIDTH + BUTTON_GAP)), BUTTON_TOP, () => ReleaseClicked?.Invoke());
    }

    /// <summary>The player pressed Rename. The window checks the monster is still there, then opens the prompt.</summary>
    public event Action? RenameClicked;

    /// <summary>The player pressed Feed. The window checks the monster is still there, then opens the prompt.</summary>
    public event Action? FeedClicked;

    /// <summary>The player pressed Release. The window asks the yes/no question.</summary>
    public event Action? ReleaseClicked;

    /// <summary>The player pressed Choose on an ability row. Carries the ability key.</summary>
    public event Action<string>? AbilityChosen;

    /// <summary>Repaints the detail from the state and the selected monster, or hides it all when there is none.</summary>
    public void Refresh(TamerStableState state, TamerMonsterView? monster)
    {
        ArgumentNullException.ThrowIfNull(state);

        var species = monster is null ? null : state.SpeciesOf(monster);
        var known = (monster is not null) && (species is not null);

        Portrait.SpriteId = known ? species!.Sprite : (ushort)0;

        if (monster is null)
        {
            NameLabel.Text = string.Empty;
            MetaLabel.Text = string.Empty;
            LevelLabel.Text = string.Empty;
            XpBar.Percent = 0f;
            XpTextLabel.Text = string.Empty;
            StatsLabel.Text = string.Empty;
            TraitNameLabel.Text = string.Empty;
            TraitTextLabel.Text = string.Empty;
            StatsLabel.Visible = false;
            TraitNameLabel.Visible = false;
            TraitTextLabel.Visible = false;

            foreach (var row in AbilityRows)
                row.Visible = false;

            RenameButton.Enabled = false;
            FeedButton.Enabled = false;
            ReleaseButton.Enabled = false;

            return;
        }

        if (known)
        {
            NameLabel.ForegroundColor = LegendColors.Gold;
            NameLabel.Text = TamerStableText.FitLines(monster.Name, HEADER_WIDTH - 2, 1);
            MetaLabel.Text = TamerStableText.FitLines(MetaLine(state, species!), HEADER_WIDTH - 2, 1);
        } else
        {
            NameLabel.ForegroundColor = LegendColors.Gray;
            NameLabel.Text = TamerStableText.UnknownCreature;
            MetaLabel.Text = TamerStableText.UnknownCreature;
        }

        LevelLabel.Text = TamerStableText.DetailLevel(monster.Level);
        XpBar.Percent = (float)state.ExperienceFraction(monster);
        XpTextLabel.Text = state.ExperienceText(monster);

        var stats = state.StatsOf(monster);

        StatsLabel.Visible = stats is not null;
        TraitNameLabel.Visible = false;
        TraitTextLabel.Visible = false;

        if (stats is not null)
            StatsLabel.Text =
                $"Health {stats.Health} Attack {stats.Attack}\nArmor {stats.Armor} Speed {stats.Speed} Range {species!.Range}";

        var trait = state.TraitOf(monster);

        if ((trait is not null) && known)
        {
            TraitNameLabel.Visible = true;
            TraitTextLabel.Visible = true;
            TraitNameLabel.Text = TamerStableText.FitLines(trait.Name, WIDTH - 2, 1);
            TraitTextLabel.Text = TamerStableText.FitLines(trait.Text, WIDTH - 2, TRAIT_TEXT_HEIGHT / TextRenderer.CHAR_HEIGHT);
        }

        var rows = state.AbilityRows(monster);

        for (var i = 0; i < AbilityRows.Length; i++)
        {
            var row = AbilityRows[i];

            if ((i >= rows.Count) || !known)
            {
                row.Visible = false;
                continue;
            }

            row.Visible = true;
            row.Bind(rows[i]);
        }

        RenameButton.Enabled = state.CanRename(monster, out _);
        FeedButton.Enabled = state.CanFeed(monster, out _);
        ReleaseButton.Enabled = state.CanRelease(monster, out _);
    }

    private static string MetaLine(TamerStableState state, TamerSpeciesView species)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(species);

        var parts = new[]
        {
            species.Name,
            TamerStableText.RarityName(species.Rarity),
            TamerStableText.ElementName(species.Element),
            species.Family,
            TamerStableText.RoleName(species.Role)
        };

        return string.Join(" \u00b7 ", parts.Where(part => !string.IsNullOrEmpty(part)));
    }

    private CustomButton Button(string caption, int x, int y, ClickedHandler onClick)
    {
        var button = new CustomButton(caption, BUTTON_WIDTH) { X = x, Y = y };

        button.Clicked += onClick;
        AddChild(button);

        return button;
    }

    private static UILabel Label(int x, int y, int width, Color color)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = color,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };

    /// <summary>One species ability: its name, its text, and whether it is chosen, choosable or locked.</summary>
    private sealed class AbilityRow : UIPanel
    {
        private const int CHOOSE_BUTTON_WIDTH = 100;
        private const int CHOOSE_HEIGHT = CustomButton.HEIGHT;
        private const int NAME_WIDTH = 230;
        private const int STATE_LEFT = 250;
        private const int STATE_WIDTH = 120;

        private readonly UILabel NameLabel;
        private readonly UILabel TextLabel;
        private readonly UILabel StateLabel;
        private readonly CustomButton ChooseButton;

        public AbilityRow(int top)
        {
            Y = top;
            Width = TamerDetailPanel.WIDTH;
            Height = ABILITY_ROW_HEIGHT;

            NameLabel = Label(0, 0, NAME_WIDTH, LegendColors.Gold);
            TextLabel = Label(0, ABILITY_TEXT_TOP, TamerDetailPanel.WIDTH, LegendColors.Silver);
            StateLabel = new UILabel
            {
                X = STATE_LEFT,
                Y = (CHOOSE_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
                Width = STATE_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Right,
                ForegroundColor = LegendColors.Silver,
                ColorCodesEnabled = false,
                IsHitTestVisible = false
            };
            ChooseButton = new CustomButton(TamerStableText.Choose, CHOOSE_BUTTON_WIDTH)
            {
                X = TamerDetailPanel.WIDTH - CHOOSE_BUTTON_WIDTH,
                Y = 0
            };
            ChooseButton.Clicked += () => ChooseClicked?.Invoke(AbilityKey);

            AddChild(NameLabel);
            AddChild(TextLabel);
            AddChild(StateLabel);
            AddChild(ChooseButton);
        }

        public string AbilityKey { get; private set; } = string.Empty;

        public event Action<string>? ChooseClicked;

        public void Bind(TamerAbilityRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            AbilityKey = row.Ability.Key;
            NameLabel.Text = TamerStableText.FitLines(row.Ability.Name, NAME_WIDTH - 2, 1);
            TextLabel.Text = TamerStableText.FitLines(row.Ability.Text, TamerDetailPanel.WIDTH - 2, 1);

            var choosable = row.State == TamerAbilityRowState.CanChoose;

            ChooseButton.Visible = choosable;
            StateLabel.Visible = !choosable;

            if (!choosable)
            {
                StateLabel.ForegroundColor = row.State == TamerAbilityRowState.Chosen ? LegendColors.Gold : LegendColors.Silver;
                StateLabel.Text = row.State == TamerAbilityRowState.Chosen
                    ? TamerStableText.Chosen
                    : TamerStableText.UnlocksAtLevel(row.Ability.UnlockLevel);
            }
        }

        private static UILabel Label(int x, int y, int width, Color color)
            => new()
            {
                X = x,
                Y = y,
                Width = width,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = color,
                ColorCodesEnabled = false,
                IsHitTestVisible = false
            };
    }
}
