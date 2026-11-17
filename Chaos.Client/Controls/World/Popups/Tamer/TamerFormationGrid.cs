#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The Squads formation grid: 7 columns by 3 rows of 44 by 40 cells with "Front" above and "Back" below. A placed
///     monster shows its sprite and level; the picked-up monster is outlined. Clicks are reported by column and row;
///     the squad panel owns the draft and its rules. A missing sprite draws the empty cell, never a crash.
/// </summary>
public sealed class TamerFormationGrid : UIPanel
{
    public const int CELL_HEIGHT = 40;
    public const int CELL_WIDTH = 44;
    public const int COLUMNS = 7;
    public const int GAP_X = 4;
    public const int GAP_Y = 2;
    public const int GRID_WIDTH = (COLUMNS * CELL_WIDTH) + ((COLUMNS - 1) * GAP_X);
    public const int GRID_HEIGHT = (TamerSquadDraft.ROWS * CELL_HEIGHT) + ((TamerSquadDraft.ROWS - 1) * GAP_Y);
    public const int LABEL_HEIGHT = TextRenderer.CHAR_HEIGHT;
    public const int LEVEL_HEIGHT = TextRenderer.CHAR_HEIGHT;
    public const int ROWS = TamerSquadDraft.ROWS;
    public const int SPRITE_HEIGHT = CELL_HEIGHT - LEVEL_HEIGHT;
    public const int WIDTH = GRID_WIDTH;
    public const int HEIGHT = (2 * LABEL_HEIGHT) + GRID_HEIGHT;

    private readonly Cell[,] Cells = new Cell[TamerSquadDraft.COLUMNS, TamerSquadDraft.ROWS];

    public TamerFormationGrid(CreatureRenderer creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        Name = "TamerFormation";
        Width = WIDTH;
        Height = HEIGHT;

        var front = new UILabel
        {
            X = 0,
            Y = 0,
            Width = WIDTH,
            Height = LABEL_HEIGHT,
            Text = TamerStableText.Front,
            ForegroundColor = LegendColors.Silver,
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(front);

        for (var column = 0; column < TamerSquadDraft.COLUMNS; column++)
        {
            for (var row = 0; row < TamerSquadDraft.ROWS; row++)
            {
                var cell = new Cell(creatures, column, row)
                {
                    X = (column * (CELL_WIDTH + GAP_X)),
                    Y = LABEL_HEIGHT + (row * (CELL_HEIGHT + GAP_Y)),
                    Width = CELL_WIDTH,
                    Height = CELL_HEIGHT
                };
                cell.TileClicked += OnTileClicked;
                Cells[column, row] = cell;
                AddChild(cell);
            }
        }

        var back = new UILabel
        {
            X = 0,
            Y = LABEL_HEIGHT + GRID_HEIGHT,
            Width = WIDTH,
            Height = LABEL_HEIGHT,
            Text = TamerStableText.Back,
            ForegroundColor = LegendColors.Silver,
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(back);
    }

    /// <summary>The player clicked a tile. Carries the column and row.</summary>
    public event Action<int, int>? TileClicked;

    /// <summary>Repaints every cell from the draft and the state.</summary>
    public void Bind(TamerStableState state, TamerSquadDraft draft)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(draft);

        var byTile = new Dictionary<(int Column, int Row), Guid>();

        foreach (var member in draft.Members)
            byTile[(member.Column, member.Row)] = member.MonsterId;

        for (var column = 0; column < TamerSquadDraft.COLUMNS; column++)
        {
            for (var row = 0; row < TamerSquadDraft.ROWS; row++)
            {
                byTile.TryGetValue((column, row), out var monsterId);
                var monster = monsterId == Guid.Empty ? null : state.Monsters.FirstOrDefault(monster => monster.Id == monsterId);
                Cells[column, row].Bind(state, monster, (draft.Picked is { } picked) && (picked == monsterId) && (monsterId != Guid.Empty));
            }
        }
    }

    private void OnTileClicked(int column, int row) => TileClicked?.Invoke(column, row);

    /// <summary>One formation tile: the creature's sprite in the top 28 px, its level below, a gold outline when picked up.</summary>
    private sealed class Cell : UIPanel
    {
        private static readonly Color CellFill = new(20, 15, 10);

        private readonly TamerCreaturePortrait Portrait;
        private readonly UILabel LevelLabel;
        private readonly int Column;
        private readonly int Row;
        private bool Picked;

        public Cell(CreatureRenderer creatures, int column, int row)
        {
            Column = column;
            Row = row;
            BackgroundColor = CellFill;

            Portrait = new TamerCreaturePortrait(creatures)
            {
                X = 0,
                Y = 0,
                Width = TamerFormationGrid.CELL_WIDTH,
                Height = TamerFormationGrid.SPRITE_HEIGHT,
                IsHitTestVisible = false
            };
            AddChild(Portrait);

            LevelLabel = new UILabel
            {
                X = 0,
                Y = TamerFormationGrid.SPRITE_HEIGHT,
                Width = TamerFormationGrid.CELL_WIDTH,
                Height = TamerFormationGrid.LEVEL_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Center,
                ForegroundColor = LegendColors.Silver,
                ColorCodesEnabled = false,
                IsHitTestVisible = false
            };
            AddChild(LevelLabel);
        }

        public event Action<int, int>? TileClicked;

        public void Bind(TamerStableState state, TamerMonsterView? monster, bool picked)
        {
            ArgumentNullException.ThrowIfNull(state);

            Picked = picked;

            if (monster is null)
            {
                Portrait.SpriteId = 0;
                LevelLabel.Text = string.Empty;
                return;
            }

            Portrait.SpriteId = state.SpriteOf(monster);
            LevelLabel.Text = TamerStableText.ListLevel(monster.Level);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            UpdateClipRect();

            if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
                return;

            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), CellFill);

            foreach (var child in Children)
                if (child.Visible)
                    child.Draw(spriteBatch);

            //after the children: the portrait fills the top of the cell with its own box and border, and would paint
            //over the gold picked-up outline
            DrawBorder(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), Picked ? LegendColors.Gold : LegendColors.DimGray);
        }

        public override void OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left)
                return;

            TileClicked?.Invoke(Column, Row);
            e.Handled = true;
        }
    }
}
