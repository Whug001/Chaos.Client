#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     One row of the Creatures list: the element square, the creature's name and its level. A protected monster reads
///     as "Unknown creature" in grey with a grey square. Rows are recycled by the hosting
///     <see cref="Controls.Scrolling.VirtualizedRowList{T}" />, so <see cref="Bind" /> stamps everything the click
///     handler reads; <see cref="Clicked" /> fires on a left click.
/// </summary>
public sealed class TamerMonsterRow : UIPanel
{
    public const int HEIGHT = 16;

    private const int LEVEL_WIDTH = 52;
    private const int NAME_LEFT = 16;
    private const int NAME_RIGHT_GAP = 4;
    private const int SQUARE_LEFT = 4;
    private const int SQUARE_SIZE = 8;

    private readonly UILabel LevelLabel;
    private readonly UILabel NameLabel;
    private Color ElementColour = LegendColors.Gray;
    private Guid MonsterIdField = Guid.Empty;

    public TamerMonsterRow(int width)
    {
        Width = width;
        Height = HEIGHT;

        NameLabel = new UILabel
        {
            X = NAME_LEFT,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
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
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = LEVEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.Silver,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(LevelLabel);
    }

    /// <summary>The monster this row is bound to; empty until the first bind.</summary>
    public Guid MonsterId => MonsterIdField;

    public bool Selected { get; private set; }

    public event Action<TamerMonsterRow>? Clicked;

    /// <summary>The element square's colour for a species element, the card faces' colours.</summary>
    public static Color ElementColor(TamerWireElement element)
        => element switch
        {
            TamerWireElement.Fire => LegendColors.BurntOrange,
            TamerWireElement.Water => LegendColors.CornflowerBlue,
            TamerWireElement.Wind => LegendColors.DarkGreen,
            TamerWireElement.Earth => LegendColors.Brown,
            _ => LegendColors.Gray
        };

    /// <summary>Stamps the row from a snapshot monster. Selection comes from the state's current pick.</summary>
    public void Bind(TamerStableState state, TamerMonsterView monster)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(monster);

        MonsterIdField = monster.Id;
        Selected = monster.Id == state.Selected?.Id;
        var species = state.SpeciesOf(monster);

        if (monster.Protected || (species is null))
        {
            NameLabel.Text = TamerStableText.UnknownCreature;
            NameLabel.ForegroundColor = LegendColors.Gray;
            LevelLabel.ForegroundColor = LegendColors.Gray;
            ElementColour = LegendColors.Gray;
        } else
        {
            NameLabel.Text = monster.Name;
            NameLabel.ForegroundColor = LegendColors.White;
            LevelLabel.ForegroundColor = LegendColors.Silver;
            ElementColour = ElementColor(species.Element);
        }

        LevelLabel.Text = TamerStableText.ListLevel(monster.Level);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        if (Selected)
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
