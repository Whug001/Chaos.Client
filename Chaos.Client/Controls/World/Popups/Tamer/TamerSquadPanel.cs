#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The Squads tab: the squad buttons, the formation grid, the bench, the set bonuses and the Save, Revert, Remove
///     and Make active buttons. Driven entirely by <see cref="TamerSquadDraft" />; no logic lives in the drawing code.
/// </summary>
public sealed class TamerSquadPanel : UIPanel
{
    public const int BENCH_HEIGHT = 272;
    public const int BENCH_LEFT = 344;
    public const int BENCH_TOP = 26;
    public const int BENCH_WIDTH = 236;
    public const int BONUS_COUNT = 4;
    public const int BONUS_LIST_HEIGHT = 96;
    public const int BONUS_LIST_LEFT = 0;
    public const int BONUS_LIST_TOP = 178;
    public const int BONUS_LIST_WIDTH = 332;
    public const int BONUS_ROW_HEIGHT = 24;
    public const int BUTTON_GAP = 8;
    public const int BUTTON_WIDTH = 90;
    public const int HEIGHT = TamerStableControl.CONTENT_HEIGHT;
    public const int MAKE_ACTIVE_LEFT = 198;
    public const int MAKE_ACTIVE_WIDTH = 110;
    public const int REMOVE_LEFT = 196;
    public const int REVERT_LEFT = 98;
    public const int SAVE_LEFT = 0;
    public const int SAVE_TOP = 276;
    public const int SLOTS_HEIGHT = TextRenderer.CHAR_HEIGHT;
    public const int SLOTS_LEFT = 314;
    public const int SLOTS_TOP = 5;
    public const int SLOTS_WIDTH = 266;
    public const int SQUAD_BUTTON_STEP = 66;
    public const int SQUAD_BUTTON_WIDTH = 60;
    public const int SQUAD_ROW_HEIGHT = CustomButton.HEIGHT;
    public const int SQUAD_ROW_TOP = 0;
    public const int WIDTH = TamerStableControl.CONTENT_WIDTH;

    private readonly CustomButton[] SquadButtons = new CustomButton[3];
    private readonly CustomButton MakeActiveButton;
    private readonly UILabel SlotsLabel;
    private readonly TamerFormationGrid Grid;
    private readonly VirtualizedRowList<TamerMonsterView> BenchList;
    private readonly UILabel[] BonusLabels = new UILabel[BONUS_COUNT];
    private readonly UILabel NoBonusLabel;
    private readonly CustomButton SaveButton;
    private readonly CustomButton RevertButton;
    private readonly CustomButton RemoveButton;
    private readonly TcgChoicePanel DiscardChoice;
    private TamerSquadDraft? Draft;
    private Action? PendingAfterDiscard;

    public TamerSquadPanel(CreatureRenderer creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        Name = "TamerSquads";
        Width = WIDTH;
        Height = HEIGHT;

        for (var i = 0; i < SquadButtons.Length; i++)
        {
            var index = i;
            SquadButtons[i] = new CustomButton(string.Empty, SQUAD_BUTTON_WIDTH)
            {
                X = (i * SQUAD_BUTTON_STEP),
                Y = SQUAD_ROW_TOP
            };
            SquadButtons[i].Clicked += () => ShowSquad((byte)index);
            AddChild(SquadButtons[i]);
        }

        MakeActiveButton = new CustomButton(TamerStableText.MakeActive, MAKE_ACTIVE_WIDTH)
        {
            X = MAKE_ACTIVE_LEFT,
            Y = SQUAD_ROW_TOP,
            Height = SQUAD_ROW_HEIGHT
        };
        MakeActiveButton.Clicked += OnMakeActiveClicked;
        AddChild(MakeActiveButton);

        SlotsLabel = new UILabel
        {
            X = SLOTS_LEFT,
            Y = SLOTS_TOP,
            Width = SLOTS_WIDTH,
            Height = SLOTS_HEIGHT,
            ForegroundColor = LegendColors.Silver,
            HorizontalAlignment = HorizontalAlignment.Right,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(SlotsLabel);

        Grid = new TamerFormationGrid(creatures) { X = 0, Y = BENCH_TOP };
        Grid.TileClicked += OnTileClicked;
        AddChild(Grid);

        var benchRowWidth = BENCH_WIDTH - ScrollBarControl.DEFAULT_WIDTH;

        BenchList = new VirtualizedRowList<TamerMonsterView>(
            benchRowWidth,
            BENCH_HEIGHT,
            TamerBenchRow.HEIGHT,
            () =>
            {
                TamerBenchRow row = new(benchRowWidth);
                row.Clicked += OnBenchClicked;
                return row;
            },
            (row, monster, _) => ((TamerBenchRow)row).Bind(WorldState.TamerStable, monster, Draft));
        AddChild(new ScrollViewerControl(BenchList) { X = BENCH_LEFT, Y = BENCH_TOP, Width = BENCH_WIDTH, Height = BENCH_HEIGHT });

        for (var i = 0; i < BonusLabels.Length; i++)
        {
            BonusLabels[i] = new UILabel
            {
                X = BONUS_LIST_LEFT,
                Y = BONUS_LIST_TOP + (i * BONUS_ROW_HEIGHT),
                Width = BONUS_LIST_WIDTH,
                Height = BONUS_ROW_HEIGHT,
                ForegroundColor = LegendColors.White,
                WordWrap = true,
                ColorCodesEnabled = false,
                IsHitTestVisible = false
            };
            AddChild(BonusLabels[i]);
        }

        NoBonusLabel = new UILabel
        {
            X = BONUS_LIST_LEFT,
            Y = BONUS_LIST_TOP,
            Width = BONUS_LIST_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            Text = TamerStableText.NoSetBonus,
            ForegroundColor = LegendColors.Silver,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(NoBonusLabel);

        SaveButton = new CustomButton(TamerStableText.Save, BUTTON_WIDTH) { X = SAVE_LEFT, Y = SAVE_TOP };
        SaveButton.Clicked += OnSaveClicked;
        AddChild(SaveButton);

        RevertButton = new CustomButton(TamerStableText.Revert, BUTTON_WIDTH) { X = REVERT_LEFT, Y = SAVE_TOP };
        RevertButton.Clicked += OnRevertClicked;
        AddChild(RevertButton);

        RemoveButton = new CustomButton(TamerStableText.Remove, BUTTON_WIDTH) { X = REMOVE_LEFT, Y = SAVE_TOP };
        RemoveButton.Clicked += OnRemoveClicked;
        AddChild(RemoveButton);

        DiscardChoice = new TcgChoicePanel(WIDTH, HEIGHT);
        AddChild(DiscardChoice);
    }

    /// <summary>A squad request (SaveSquad, SetActiveSquad).</summary>
    public event Action<TamerActionArgs>? ActionRequested;

    /// <summary>The squad being shown (0-based).</summary>
    public byte ShownSquad { get; private set; }

    /// <summary>True while a discard question is open over this tab.</summary>
    public bool HasOpenQuestion => DiscardChoice.Visible;

    /// <summary>True when the draft has unsaved changes.</summary>
    public bool HasUnsavedChanges => Draft?.IsChanged ?? false;

    /// <summary>True while a monster is picked up.</summary>
    public bool HasPick => Draft?.Picked is not null;

    /// <summary>Shows the latest state, reconciling the draft so unsaved changes survive a snapshot.</summary>
    public void Refresh(TamerStableState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!Visible)
            return;

        if (Draft is null)
        {
            ShownSquad = state.ActiveSquad > 2 ? (byte)0 : state.ActiveSquad;
            Draft = new TamerSquadDraft(SquadOf(state, ShownSquad));
        } else
        {
            Draft.Reconcile(state);
            Draft.Rebase(SquadOf(state, ShownSquad));
        }

        Paint(state);
    }

    /// <summary>Drops the pick (Escape).</summary>
    public void CancelPick()
    {
        Draft?.CancelPick();
        Refresh(WorldState.TamerStable);
    }

    /// <summary>Discards unsaved changes (the discard question's Yes).</summary>
    public void DiscardChanges()
    {
        Draft?.Revert();
        Refresh(WorldState.TamerStable);
    }

    /// <summary>Hides a discard question without answering (the window closes).</summary>
    public void DismissQuestion()
    {
        PendingAfterDiscard = null;
        DiscardChoice.Dismiss();
    }

    /// <summary>Answers a discard question as cancelled (Escape).</summary>
    public void CancelQuestion() => DiscardChoice.Cancel();

    private static IReadOnlyList<TamerDisplaySquadMember> SquadOf(TamerStableState state, byte squad)
        => (state.Squads.Count > squad) ? state.Squads[squad] : [];

    private void Paint(TamerStableState state)
    {
        if (Draft is not { } draft)
            return;

        for (var i = 0; i < SquadButtons.Length; i++)
        {
            var active = state.ActiveSquad == i;
            SquadButtons[i].Caption = active ? ($"{i + 1} *") : ($"{i + 1}");
            SquadButtons[i].Selected = ShownSquad == i;
        }

        MakeActiveButton.Enabled = ShownSquad != state.ActiveSquad;
        SlotsLabel.Text = TamerStableText.SlotsLine(draft.SlotsUsed, state.SquadSlots);

        Grid.Bind(state, draft);

        var bench = draft.Bench(state);
        BenchList.ReplaceItems(bench);

        var bonuses = draft.SetBonuses(state);

        for (var i = 0; i < BonusLabels.Length; i++)
        {
            var label = BonusLabels[i];

            if (i >= bonuses.Count)
            {
                label.Visible = false;
                label.Text = string.Empty;
                continue;
            }

            label.Visible = true;
            var bonus = bonuses[i];
            label.Text = TamerStableText.FitLines($"{bonus.Name}\n{bonus.Text}", BONUS_LIST_WIDTH - 2, BONUS_ROW_HEIGHT / TextRenderer.CHAR_HEIGHT);
        }

        NoBonusLabel.Visible = bonuses.Count == 0;
        SaveButton.Enabled = draft.IsChanged;
        RevertButton.Enabled = draft.IsChanged;
        RemoveButton.Enabled = draft.Picked is { } picked && draft.Members.Any(member => member.MonsterId == picked);
    }

    private void ShowSquad(byte squad)
    {
        if (squad > 2)
            return;

        if (squad == ShownSquad)
            return;

        if (HasUnsavedChanges)
        {
            PendingAfterDiscard = () =>
            {
                ShownSquad = squad;
                Draft = new TamerSquadDraft(SquadOf(WorldState.TamerStable, squad));
                Refresh(WorldState.TamerStable);
            };
            DiscardChoice.Ask(
                TamerStableText.DiscardQuestion,
                [TamerStableText.Yes, TamerStableText.No],
                answer =>
                {
                    var pending = PendingAfterDiscard;
                    PendingAfterDiscard = null;

                    if ((answer == 0) && (pending is not null))
                        pending();
                });
            return;
        }

        ShownSquad = squad;
        Draft = new TamerSquadDraft(SquadOf(WorldState.TamerStable, squad));
        Refresh(WorldState.TamerStable);
    }

    private void OnTileClicked(int column, int row)
    {
        var state = WorldState.TamerStable;

        if (!Visible || (Draft is not { } draft))
            return;

        var occupant = draft.Members.FirstOrDefault(member => (member.Column == column) && (member.Row == row));

        if (draft.Picked is null)
        {
            if (occupant is not null)
                draft.Pick(occupant.MonsterId);

            Paint(state);
            return;
        }

        var pickedPlaced = draft.Members.Any(member => member.MonsterId == draft.Picked);

        if (!pickedPlaced)
        {
            var monster = state.Monsters.FirstOrDefault(monster => monster.Id == draft.Picked);

            if (monster is null)
            {
                draft.CancelPick();
                Paint(state);
                return;
            }

            if (!draft.CanPlace(state, monster, out _))
                return;

            draft.PlaceAt(column, row);
            Paint(state);
            return;
        }

        draft.PlaceAt(column, row);
        Paint(state);
    }

    private void OnBenchClicked(TamerBenchRow row)
    {
        var state = WorldState.TamerStable;

        if (!Visible || (Draft is not { } draft) || (row.MonsterId == Guid.Empty))
            return;

        var monster = state.Monsters.FirstOrDefault(monster => monster.Id == row.MonsterId);

        if (monster is null)
            return;

        if (draft.Picked == monster.Id)
        {
            draft.CancelPick();
            Paint(state);
            return;
        }

        if (!draft.CanPlace(state, monster, out var reason))
        {
            if (!string.IsNullOrEmpty(reason))
                SlotsLabel.Text = reason;

            return;
        }

        draft.Pick(monster.Id);
        Paint(state);
    }

    private void OnSaveClicked()
    {
        var state = WorldState.TamerStable;

        if (!Visible || (Draft is not { } draft))
            return;

        if (draft.ToAction(state, ShownSquad) is { } action)
            ActionRequested?.Invoke(action);
    }

    private void OnRevertClicked()
    {
        if (!Visible || (Draft is null))
            return;

        Draft.Revert();
        Refresh(WorldState.TamerStable);
    }

    private void OnRemoveClicked()
    {
        var state = WorldState.TamerStable;

        if (!Visible || (Draft is not { } draft) || (draft.Picked is null))
            return;

        draft.Remove();
        Paint(state);
    }

    private void OnMakeActiveClicked()
    {
        if (TamerStableState.SetActiveSquadAction(ShownSquad) is { } action)
            ActionRequested?.Invoke(action);
    }

    /// <summary>One bench row: the creature's name, level and element square, grey when it cannot be placed, gold when picked up.</summary>
    private sealed class TamerBenchRow : UIPanel
    {
        public const int HEIGHT = TamerMonsterRow.HEIGHT;

        private const int LEVEL_WIDTH = 52;
        private const int NAME_LEFT = 16;
        private const int NAME_RIGHT_GAP = 4;
        private const int SQUARE_LEFT = 4;
        private const int SQUARE_SIZE = 8;

        private readonly UILabel LevelLabel;
        private readonly UILabel NameLabel;
        private Color ElementColour = LegendColors.Gray;
        private Guid MonsterIdField = Guid.Empty;
        private bool Picked;

        public TamerBenchRow(int width)
        {
            Width = width;
            Height = HEIGHT;

            NameLabel = new UILabel
            {
                X = NAME_LEFT,
                Y = ((HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
                Width = width - NAME_LEFT - LEVEL_WIDTH - NAME_RIGHT_GAP,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = LegendColors.White,
                ColorCodesEnabled = false,
                IsHitTestVisible = false
            };
            AddChild(NameLabel);

            LevelLabel = new UILabel
            {
                X = width - LEVEL_WIDTH,
                Y = ((HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
                Width = LEVEL_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Right,
                ForegroundColor = LegendColors.Silver,
                ColorCodesEnabled = false,
                IsHitTestVisible = false
            };
            AddChild(LevelLabel);
        }

        public Guid MonsterId => MonsterIdField;

        public event Action<TamerBenchRow>? Clicked;

        public void Bind(TamerStableState state, TamerMonsterView monster, TamerSquadDraft? draft)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(monster);

            MonsterIdField = monster.Id;
            Picked = (draft?.Picked == monster.Id);
            var species = state.SpeciesOf(monster);
            var duplicate = IsDuplicateSpecies(state, draft, monster);

            if (monster.Protected || (species is null) || duplicate)
            {
                NameLabel.Text = (monster.Protected || (species is null)) ? TamerStableText.UnknownCreature : monster.Name;
                NameLabel.ForegroundColor = LegendColors.Gray;
                LevelLabel.ForegroundColor = LegendColors.Gray;
                ElementColour = LegendColors.Gray;
            } else
            {
                NameLabel.Text = monster.Name;
                NameLabel.ForegroundColor = LegendColors.White;
                LevelLabel.ForegroundColor = LegendColors.Silver;
                ElementColour = TamerMonsterRow.ElementColor(species.Element);
            }

            LevelLabel.Text = TamerStableText.ListLevel(monster.Level);
        }

        private static bool IsDuplicateSpecies(TamerStableState state, TamerSquadDraft? draft, TamerMonsterView monster)
        {
            if (draft is null)
                return false;

            foreach (var member in draft.Members)
            {
                var held = state.Monsters.FirstOrDefault(current => current.Id == member.MonsterId);

                if ((held is not null) && held.SpeciesKey.Equals(monster.SpeciesKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            UpdateClipRect();

            if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
                return;

            if (Picked)
                DrawBorder(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), LegendColors.Gold);

            DrawRectClipped(
                spriteBatch,
                new Rectangle(ScreenX + SQUARE_LEFT, ScreenY + ((HEIGHT - SQUARE_SIZE) / 2), SQUARE_SIZE, SQUARE_SIZE),
                ElementColour);

            foreach (var child in Children)
                if (child.Visible)
                    child.Draw(spriteBatch);
        }

        public override void OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left)
                return;

            Clicked?.Invoke(this);
            e.Handled = true;
        }
    }
}
