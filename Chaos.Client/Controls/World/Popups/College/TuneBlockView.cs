#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     A tune inside the reader: Play/Stop and the "Dorian, Lively, Harp" line, then the three grids read-only at 6 x 4
///     cells with the playhead while this block's tune plays.
/// </summary>
public sealed class TuneBlockView : UIPanel
{
    private const int BUTTON_WIDTH = 54;
    private const int SUMMARY_GAP = 6;
    private const int GRID_TOP = CustomButton.HEIGHT + 4;

    private readonly TuneGrid Grid;
    private readonly CustomButton PlayButton;
    private readonly TunePlayer Player;
    private readonly TuneData Tune;

    public TuneBlockView(TuneData tune, TunePlayer player, int width)
    {
        Tune = tune;
        Player = player;
        Width = width;
        Height = GRID_TOP + TuneGridLayout.Reader.Height;
        IsPassThrough = true;

        PlayButton = new CustomButton("Play", BUTTON_WIDTH);
        PlayButton.Clicked += Toggle;
        AddChild(PlayButton);

        AddChild(
            new UILabel
            {
                X = BUTTON_WIDTH + SUMMARY_GAP,
                Y = (CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
                Width = width - BUTTON_WIDTH - SUMMARY_GAP,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = LegendColors.Gray,
                IsHitTestVisible = false,
                Text = TuneLabels.Summary(tune)
            });

        Grid = new TuneGrid(TuneGridLayout.Reader)
        {
            X = Math.Max(0, (width - TuneGridLayout.Reader.Width) / 2),
            Y = GRID_TOP,
            Data = tune,
            IsHitTestVisible = false
        };

        AddChild(Grid);
    }

    private bool IsMine => ReferenceEquals(Player.Owner, this);

    public void Play() => Player.Play(Tune, this);

    public void Stop() => Player.StopIfOwner(this);

    public override void Update(GameTime gameTime)
    {
        var mine = IsMine;
        var caption = !mine ? "Play" : Player.IsRendering ? "Wait..." : Player.IsPlaying ? "Stop" : "Play";

        if (PlayButton.Caption != caption)
            PlayButton.Caption = caption;

        Grid.Playhead = mine && Player.IsPlaying ? Player.Column : null;
        base.Update(gameTime);
    }

    private void Toggle()
    {
        if (IsMine && (Player.IsPlaying || Player.IsRendering))
            Player.Stop();
        else
            Play();
    }
}
