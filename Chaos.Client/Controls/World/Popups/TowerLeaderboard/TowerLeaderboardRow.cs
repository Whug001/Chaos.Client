#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.TowerLeaderboard;

/// <summary>
///     One lineup on the tower leaderboard: the rank, the floor and time stacked beside it, then up to six walking figures
///     with each member's name (gold) and class (gray) under them. Every other row is shaded so the rows read apart.
/// </summary>
public sealed class TowerLeaderboardRow : UIPanel
{
    public const int RANK_WIDTH = 26;
    public const int SCORE_WIDTH = 74;
    public const int SLOT_WIDTH = 76;
    public const int WIDTH = RANK_WIDTH + SCORE_WIDTH + (SLOT_COUNT * SLOT_WIDTH);
    public const int HEIGHT = FIGURE_HEIGHT + (2 * TextRenderer.CHAR_HEIGHT) + 8;

    private const int SLOT_COUNT = 6;
    private const int FIGURE_WIDTH = 64;
    private const int FIGURE_HEIGHT = 72;
    private const int FIGURE_TOP = 2;

    //the 85 px figure overhangs the 72 px pedestal when drawn at 1x, so its feet need room above the name
    private const int NAME_GAP = 4;
    private const int SCORE_GAP = 2;

    private static readonly Color ShadedColor = Color.Black * 0.25f;

    private readonly UILabel FloorLabel;
    private readonly UILabel RankLabel;
    private readonly MemberSlot[] Slots = new MemberSlot[SLOT_COUNT];
    private readonly UILabel TimeLabel;

    public TowerLeaderboardRow(AislingRenderer renderer, bool shaded)
    {
        Width = WIDTH;
        Height = HEIGHT;
        BackgroundColor = shaded ? ShadedColor : null;

        var scoreTop = (HEIGHT - ((2 * TextRenderer.CHAR_HEIGHT) + SCORE_GAP)) / 2;
        RankLabel = AddLabel(0, (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2, RANK_WIDTH, LegendColors.Gold);
        FloorLabel = AddLabel(RANK_WIDTH, scoreTop, SCORE_WIDTH, LegendColors.White);
        TimeLabel = AddLabel(RANK_WIDTH, scoreTop + TextRenderer.CHAR_HEIGHT + SCORE_GAP, SCORE_WIDTH, LegendColors.Gray);

        for (var i = 0; i < SLOT_COUNT; i++)
        {
            var slotLeft = RANK_WIDTH + SCORE_WIDTH + (i * SLOT_WIDTH);

            var preview = new GuildCloakPreview(renderer, FIGURE_WIDTH, FIGURE_HEIGHT)
            {
                X = slotLeft + ((SLOT_WIDTH - FIGURE_WIDTH) / 2),
                Y = FIGURE_TOP,
                Visible = false
            };

            AddChild(preview);

            var nameTop = FIGURE_TOP + FIGURE_HEIGHT + NAME_GAP;
            var name = AddLabel(slotLeft, nameTop, SLOT_WIDTH, LegendColors.Gold);
            var className = AddLabel(slotLeft, nameTop + TextRenderer.CHAR_HEIGHT, SLOT_WIDTH, LegendColors.Gray);

            Slots[i] = new MemberSlot(preview, name, className);
        }
    }

    /// <summary>Shows a lineup; null hides the row.</summary>
    public void SetEntry(TowerLeaderboardEntryInfo? entry)
    {
        Visible = entry is not null;

        if (entry is null)
            return;

        RankLabel.Text = $"{entry.Rank}";
        FloorLabel.Text = $"Floor {entry.Floor}";
        TimeLabel.Text = TowerLeaderboardState.TimeText(entry.Seconds);

        for (var i = 0; i < Slots.Length; i++)
        {
            var member = i < entry.Members.Count ? entry.Members[i] : null;
            Slots[i].Preview.Visible = member is not null;
            Slots[i].Preview.SetLook(member is null ? null : TownBallotLook.ToAppearance(member.Look));
            Slots[i].Name.Text = member?.Name ?? string.Empty;
            Slots[i].Class.Text = member?.ClassName ?? string.Empty;
        }
    }

    /// <summary>Drops every figure's rendered steps, so a closed window holds no GPU memory.</summary>
    public void ReleaseFrames()
    {
        foreach (var slot in Slots)
            slot.Preview.ReleaseFrames();
    }

    private UILabel AddLabel(int x, int y, int width, Color color)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = color,
            IsHitTestVisible = false
        };

        AddChild(label);

        return label;
    }

    private sealed record MemberSlot(GuildCloakPreview Preview, UILabel Name, UILabel Class);
}
