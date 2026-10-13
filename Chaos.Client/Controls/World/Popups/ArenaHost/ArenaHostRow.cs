#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.ArenaHost;

/// <summary>One clickable line in the host panel: a mode, a setting value or a roster name.</summary>
public sealed class ArenaHostRow : UIPanel
{
    public const int HEIGHT = TextRenderer.CHAR_HEIGHT + 2;

    //the bitmap font has no star glyph, so the vote winner gets an asterisk
    private const string WINNER_MARK = "* ";

    //the import window's selected-row colors, so the lists read alike
    private static readonly Color SelectedColor = new(156, 96, 12, 180);

    private readonly UILabel Label;

    public ArenaHostRow(int width)
    {
        Width = width;
        Height = HEIGHT;

        Label = new UILabel
        {
            X = 3,
            Y = 1,
            Width = width - 6,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        AddChild(Label);
    }

    public bool Selected
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            BackgroundColor = value ? SelectedColor : null;
        }
    }

    public string Text => Label.Text;

    public event Action? Clicked;

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public void Show(string text, Color color)
    {
        Visible = true;
        Label.Text = text;
        Label.ForegroundColor = color;
    }

    public static string ModeText(ArenaHostModeInfo mode, bool voteMode, bool picked)
    {
        var text = (mode.IsVoteWinner ? WINNER_MARK : string.Empty) + mode.Name;

        if (!voteMode)
            return text;

        return (picked ? "[x] " : "[ ] ") + text;
    }

    public static string RosterText(ArenaHostRosterInfo player)
        => player.Name + (player.IsHost ? " (host)" : string.Empty) + (player.IsCaptain ? " (captain)" : string.Empty);

    public static Color TeamColor(ArenaHostTeam team)
        => team switch
        {
            ArenaHostTeam.Gold  => LegendColors.Gold,
            ArenaHostTeam.Green => LegendColors.Lime,
            ArenaHostTeam.Red   => LegendColors.Red,
            ArenaHostTeam.Blue  => LegendColors.CornflowerBlue,
            _                   => LegendColors.Gray
        };
}
