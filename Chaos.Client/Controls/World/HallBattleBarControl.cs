#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World;

/// <summary>
///     The in-match bar at the top centre of the world screen: the round, who defends and the clock, with the Heart's
///     health as a fill. The server sends a Bar display every so often and BarClear at the end; between them the clock
///     counts down here.
/// </summary>
public sealed class HallBattleBarControl : UIPanel
{
    private const int BAR_WIDTH = 320;
    private const int BAR_HEIGHT = 40;
    private const int FILL_HEIGHT = 8;
    private const int PADDING = 8;
    private const int TOP = 4;

    private readonly UIPanel Fill;
    private readonly UILabel Text;
    private HallBattleDisplayArgs? Current;
    private DateTime ReceivedAt;

    //the last text and fill actually assigned: the bar only rebuilds its strings on change, not every frame
    private string LastText = string.Empty;
    private int LastFill;

    public HallBattleBarControl(int screenWidth)
    {
        Name = "HallBattleBar";
        Width = BAR_WIDTH;
        Height = BAR_HEIGHT;
        X = (screenWidth - BAR_WIDTH) / 2;
        Y = TOP;
        Visible = false;
        IsHitTestVisible = false;
        BackgroundColor = new Color(28, 20, 12);
        BorderColor = LegendColors.Gold;

        Text = new UILabel
        {
            X = PADDING,
            Y = 6,
            Width = BAR_WIDTH - (2 * PADDING),
            Height = 20,
            ForegroundColor = LegendColors.Gold,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };

        Fill = new UIPanel
        {
            X = PADDING,
            Y = BAR_HEIGHT - FILL_HEIGHT - 6,
            Width = 0,
            Height = FILL_HEIGHT,
            BackgroundColor = LegendColors.Red,
            IsHitTestVisible = false
        };

        AddChild(Text);
        AddChild(Fill);
    }

    public static int FillWidth(int percent, int width) => width * Math.Clamp(percent, 0, 100) / 100;

    public static string TextFor(HallBattleDisplayArgs args)
    {
        var clock = $"{args.SecondsLeft / 60}:{args.SecondsLeft % 60:00}";

        return args.InBreak ? $"Break - Round {args.Round} in {clock}" : $"Round {args.Round} - {args.DefendingGuild} defends - {clock}";
    }

    public void Clear()
    {
        Current = null;
        LastText = string.Empty;
        LastFill = 0;
        Visible = false;
    }

    public void Show(HallBattleDisplayArgs args)
    {
        Current = args;
        ReceivedAt = DateTime.UtcNow;
        Visible = true;
        Refresh(DateTime.UtcNow);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible)
            Refresh(DateTime.UtcNow);
    }

    private void Refresh(DateTime now)
    {
        if (Current is null)
            return;

        var left = Math.Max(0, Current.SecondsLeft - (int)(now - ReceivedAt).TotalSeconds);
        var text = TextFor(Current with { SecondsLeft = (ushort)left });

        if (!string.Equals(text, LastText, StringComparison.Ordinal))
        {
            LastText = text;
            Text.Text = text;
        }

        var fill = FillWidth(Current.HeartPercent, BAR_WIDTH - (2 * PADDING));

        if (fill != LastFill)
        {
            LastFill = fill;
            Fill.Width = fill;
        }
    }
}
