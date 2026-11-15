#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The Ranked tab: the standing, the saved defence and the top 10 on the left; the offered opponents and the
///     defence log on the right. It only draws what <see cref="TamerRankedState" /> and <see cref="TamerRankedText" />
///     answer, at the places <see cref="TamerRankedLayout" /> names. Refresh, Attack, Set from squad and Revenge ask
///     first, on the window's choice panel through <see cref="QuestionRequested" />; each question is a
///     <see cref="TamerRankedConfirmation" />, so a Yes after a new display or snapshot sends nothing.
/// </summary>
public sealed class TamerRankedPanel : UIPanel
{
    private static readonly Color BarFill = new(28, 20, 12);

    private readonly UILabel ReadingLabel;
    private readonly UILabel StepLabel;
    private readonly UIProgressBar PointsBar;
    private readonly UILabel PointsLabel;
    private readonly UILabel RatingLabel;
    private readonly UILabel BestLabel;
    private readonly UILabel SeasonLabel;
    private readonly TamerCreaturePortrait[] DefencePortraits = new TamerCreaturePortrait[TamerRankedLayout.DEFENCE_COUNT];
    private readonly UILabel DefenceGoneLabel;
    private readonly CustomButton[] SquadButtons = new CustomButton[TamerRankedLayout.SQUAD_BUTTON_COUNT];
    private readonly UILabel[] TopLabels = new UILabel[TamerRankedLayout.TOP_ROW_COUNT];
    private readonly UILabel AttacksLabel;
    private readonly UILabel DailyWinLabel;
    private readonly CustomButton RefreshButton;
    private readonly TamerRankedOfferRow[] OfferRows = new TamerRankedOfferRow[TamerRankedLayout.OFFER_COUNT];
    private readonly UILabel NoteLabel;
    private readonly VirtualizedRowList<TamerRankedLogView> LogList;
    private readonly CustomButton WatchButton;
    private readonly CustomButton RevengeButton;
    private readonly List<UIElement> ViewControls = [];

    public TamerRankedPanel(CreatureRenderer creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        Name = "TamerRanked";
        Width = TamerRankedLayout.WIDTH;
        Height = TamerRankedLayout.HEIGHT;

        ReadingLabel = Label(TamerRankedLayout.Reading, LegendColors.Gray, HorizontalAlignment.Center, TamerRankedText.Reading);
        AddChild(ReadingLabel);

        //the left column
        StepLabel = View(Label(TamerRankedLayout.Step, LegendColors.Gold));

        var bar = TamerRankedLayout.Bar;

        PointsBar = View(
            new UIProgressBar
            {
                X = bar.X,
                Y = bar.Y,
                Width = bar.Width,
                Height = bar.Height,
                BackgroundColor = BarFill,
                BorderColor = LegendColors.DimGray,
                FillColor = LegendColors.Gold,
                IsHitTestVisible = false
            });

        PointsLabel = View(Label(TamerRankedLayout.Points, LegendColors.White));
        RatingLabel = View(Label(TamerRankedLayout.Rating, LegendColors.Silver, HorizontalAlignment.Right));
        BestLabel = View(Label(TamerRankedLayout.Best, LegendColors.Silver));
        SeasonLabel = View(Label(TamerRankedLayout.Season, LegendColors.Silver));
        View(Label(TamerRankedLayout.YourDefence, LegendColors.Gold, text: TamerRankedText.YourDefence));

        for (var i = 0; i < DefencePortraits.Length; i++)
        {
            var box = TamerRankedLayout.DefencePortrait(i);

            DefencePortraits[i] = View(
                new TamerCreaturePortrait(creatures)
                {
                    X = box.X,
                    Y = box.Y,
                    Width = box.Width,
                    Height = box.Height,
                    Animate = false,
                    IsHitTestVisible = false
                });
        }

        var gone = TamerRankedLayout.DefenceGone;

        DefenceGoneLabel = View(
            new UILabel
            {
                X = gone.X,
                Y = gone.Y,
                Width = gone.Width,
                Height = gone.Height,
                ForegroundColor = LegendColors.Orange,
                VerticalAlignment = VerticalAlignment.Top,
                ColorCodesEnabled = false,
                IsHitTestVisible = false,

                //before Text: the label wraps when its text is set and afterwards only when its width changes
                WordWrap = true,
                Text = TamerRankedText.DefenceGone
            });

        View(Label(TamerRankedLayout.SetFrom, LegendColors.Silver, text: TamerRankedText.SetFromSquad));

        for (var i = 0; i < SquadButtons.Length; i++)
        {
            var squad = (byte)i;
            var box = TamerRankedLayout.SquadButton(i);

            SquadButtons[i] = View(new CustomButton(TamerRankedText.SquadButton(squad), box.Width) { X = box.X, Y = box.Y });
            SquadButtons[i].Clicked += () => OnSetDefenceClicked(squad);
        }

        View(Label(TamerRankedLayout.TopTitle, LegendColors.Gold, text: TamerRankedText.TopTen));

        for (var i = 0; i < TopLabels.Length; i++)
            TopLabels[i] = View(Label(TamerRankedLayout.TopRow(i), LegendColors.White));

        //the right column
        View(Label(TamerRankedLayout.Opponents, LegendColors.Gold, text: TamerRankedText.Opponents));
        DailyWinLabel = View(Label(TamerRankedLayout.DailyWin, LegendColors.Gold));
        AttacksLabel = View(Label(TamerRankedLayout.Attacks, LegendColors.Silver, HorizontalAlignment.Right));

        var refresh = TamerRankedLayout.Refresh;

        RefreshButton = View(new CustomButton(TamerRankedText.Refresh, refresh.Width) { X = refresh.X, Y = refresh.Y });
        RefreshButton.Clicked += OnRefreshClicked;

        for (var slot = 0; slot < OfferRows.Length; slot++)
        {
            var box = TamerRankedLayout.OfferRow(slot);

            OfferRows[slot] = View(new TamerRankedOfferRow(creatures, slot) { X = box.X, Y = box.Y });
            OfferRows[slot].AttackClicked += OnAttackClicked;
        }

        NoteLabel = View(Label(TamerRankedLayout.Note, LegendColors.Gray, HorizontalAlignment.Center));
        View(Label(TamerRankedLayout.LogTitle, LegendColors.Gold, text: TamerRankedText.DefenceLog));

        var list = TamerRankedLayout.LogList;

        LogList = new VirtualizedRowList<TamerRankedLogView>(
            TamerRankedLayout.LOG_ROW_WIDTH,
            list.Height,
            TamerRankedLogRow.HEIGHT,
            () =>
            {
                TamerRankedLogRow row = new(TamerRankedLayout.LOG_ROW_WIDTH);
                row.Clicked += OnLogRowClicked;

                return row;
            },
            (row, entry, _) => ((TamerRankedLogRow)row).Bind(WorldState.TamerRanked, entry));

        View(new ScrollViewerControl(LogList) { X = list.X, Y = list.Y, Width = list.Width, Height = list.Height });

        var watch = TamerRankedLayout.Watch;
        var revenge = TamerRankedLayout.Revenge;

        WatchButton = View(new CustomButton(TamerRankedText.Watch, watch.Width) { X = watch.X, Y = watch.Y });
        WatchButton.Clicked += OnWatchClicked;

        RevengeButton = View(new CustomButton(TamerRankedText.Revenge, revenge.Width) { X = revenge.X, Y = revenge.Y });
        RevengeButton.Clicked += OnRevengeClicked;

        View(Label(TamerRankedLayout.Hint, LegendColors.Gray, text: TamerRankedText.RevengeHint));
    }

    /// <summary>A ranked request (set defence, refresh, attack, revenge, watch).</summary>
    public event Action<TamerActionArgs>? ActionRequested;

    /// <summary>
    ///     A request needs a Yes first. The window asks the question on its own choice panel, which covers the whole
    ///     window, and calls the action on Yes.
    /// </summary>
    public event Action<string, Action>? QuestionRequested;

    private static UILabel Label(TamerBox box, Color color, HorizontalAlignment alignment = HorizontalAlignment.Left, string text = "")
        => new()
        {
            X = box.X,
            Y = box.Y,
            Width = box.Width,
            Height = box.Height,
            Text = text,
            ForegroundColor = color,
            HorizontalAlignment = alignment,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };

    //everything but the reading line: added as a child and hidden as one group until the first display arrives
    private T View<T>(T control) where T: UIElement
    {
        ViewControls.Add(control);
        AddChild(control);

        return control;
    }

    /// <summary>Shows the latest ranked display against the latest stable snapshot.</summary>
    public void Refresh(TamerRankedState ranked, TamerStableState stable)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        ArgumentNullException.ThrowIfNull(stable);

        if (!Visible)
            return;

        ReadingLabel.Visible = !ranked.HasView;

        foreach (var control in ViewControls)
            control.Visible = ranked.HasView;

        if (!ranked.HasView)
            return;

        StepLabel.Text = TamerRankedText.StepName(ranked.Step);
        PointsBar.Percent = (float)TamerRankedText.PointsFraction(ranked.Points);
        PointsLabel.Text = TamerRankedText.PointsLine(ranked.Points);
        RatingLabel.Text = TamerRankedText.RatingLine(ranked.Rating);
        BestLabel.Text = TamerRankedText.BestLine(ranked.BestStep);
        SeasonLabel.Text = TamerRankedText.SeasonLine(ranked.Season, ranked.SeasonSecondsLeft);

        //a defence the data can no longer build shows the line in the portraits' place
        DefenceGoneLabel.Visible = ranked.DefenceNeedsSaving;

        for (var i = 0; i < DefencePortraits.Length; i++)
        {
            var key = i < ranked.Defence.Count ? ranked.Defence[i] : null;

            DefencePortraits[i].Visible = !ranked.DefenceNeedsSaving;
            DefencePortraits[i].SpriteId = stable.GetSpecies(key)?.Sprite ?? 0;
        }

        for (var i = 0; i < SquadButtons.Length; i++)
            SquadButtons[i].Enabled = ranked.CanSetDefence(i, stable, out _);

        for (var i = 0; i < TopLabels.Length; i++)
        {
            var place = i + 1;

            TopLabels[i].Text = i < ranked.Top.Count ? TamerRankedText.TopRow(place, ranked.Top[i]) : string.Empty;
            TopLabels[i].ForegroundColor = place == ranked.LadderPosition ? LegendColors.Gold : LegendColors.White;
        }

        AttacksLabel.Text = TamerRankedText.AttacksLine(ranked.AttacksLeft, ranked.AttacksPerDay);

        //today's first win reward only means something once the player is on the ladder
        DailyWinLabel.Visible = ranked.HasDefence;
        DailyWinLabel.Text = TamerRankedText.DailyWinLine(ranked.DailyWinOpen);
        DailyWinLabel.ForegroundColor = ranked.DailyWinOpen ? LegendColors.Gold : LegendColors.Gray;
        RefreshButton.Enabled = ranked.CanRefresh;

        //no defence yet, or a ladder the server could not read, shows the note line and no offers, whatever was sent
        foreach (var row in OfferRows)
        {
            if (ranked.ShowsOffers)
                row.Bind(ranked, stable);
            else
                row.Visible = false;
        }

        var note = ranked.NoteLine;

        NoteLabel.Text = note;
        NoteLabel.Visible = note.Length > 0;

        LogList.ReplaceItems(ranked.Log);
        WatchButton.Enabled = ranked.CanWatch;
        RevengeButton.Enabled = ranked.CanRevenge(stable, out _);
    }

    //the question remembers the data it was asked against; a Yes after new data sends nothing and says so
    private void Ask(TamerRankedConfirmation? confirmation)
    {
        if (confirmation is null)
            return;

        QuestionRequested?.Invoke(
            confirmation.Question,
            () =>
            {
                var ranked = WorldState.TamerRanked;

                if (confirmation.Confirm(ranked, WorldState.TamerStable, out var answer) is { } action)
                    ActionRequested?.Invoke(action);
                else if (answer == TamerRankedAnswer.Changed)
                    ranked.ShowLocalMessage(TamerRankedText.Changed);
            });
    }

    private void OnSetDefenceClicked(byte squad)
    {
        if (Visible)
            Ask(TamerRankedConfirmation.SetDefence(WorldState.TamerRanked, WorldState.TamerStable, squad));
    }

    private void OnRefreshClicked()
    {
        if (Visible)
            Ask(TamerRankedConfirmation.Refresh(WorldState.TamerRanked, WorldState.TamerStable));
    }

    private void OnAttackClicked(int slot)
    {
        if (Visible)
            Ask(TamerRankedConfirmation.Attack(WorldState.TamerRanked, WorldState.TamerStable, slot));
    }

    private void OnLogRowClicked(TamerRankedLogRow row)
    {
        if (!Visible || (row.EntryId == Guid.Empty))
            return;

        WorldState.TamerRanked.Select(row.EntryId);
        Refresh(WorldState.TamerRanked, WorldState.TamerStable);
    }

    private void OnWatchClicked()
    {
        var ranked = WorldState.TamerRanked;

        if (!Visible || !ranked.CanWatch || (ranked.SelectedEntry is not { } entry))
            return;

        if (TamerRankedState.WatchAction(entry.Id) is { } action)
            ActionRequested?.Invoke(action);
    }

    private void OnRevengeClicked()
    {
        if (Visible)
            Ask(TamerRankedConfirmation.Revenge(WorldState.TamerRanked, WorldState.TamerStable));
    }
}
