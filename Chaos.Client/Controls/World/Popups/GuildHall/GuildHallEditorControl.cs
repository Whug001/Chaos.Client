#region
using System.Globalization;
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildHall;

/// <summary>
///     The guild hall editor's docked picker, at the right edge of the world viewport. It never takes the keyboard, so the
///     editor can keep walking with the arrow keys. Every action goes out through <see cref="EditRequested" />.
/// </summary>
public sealed class GuildHallEditorControl : UIPanel
{
    public const int PANEL_WIDTH = 222;
    public const int PANEL_HEIGHT = 290;
    public const int PAD = 6;
    public const int HEADER_Y = 2;
    public const int HEADER_H = 16;
    public const int TITLE_W = 140;
    public const int RESET_X = 150;
    public const int RESET_W = 46;
    public const int CHECK_X = 200;
    public const int CHECK_W = 16;
    public const int TAB_Y = 20;
    public const int TAB_H = 28;
    public const int TAB_WIDTH = 33;
    public const int TAB_PITCH = 35;
    public const int SET_Y = 50;
    public const int SET_H = 16;
    public const int ARROW_W = 16;
    public const int SET_TEXT_X = PAD + ARROW_W + 2;
    public const int SET_TEXT_W = PANEL_WIDTH - (2 * PAD) - (2 * (ARROW_W + 2));
    public const int GRID_Y = 70;
    public const int TOOL_LABEL_Y = 211;
    public const int TOOL_Y = 225;
    public const int TOOL_H = 28;
    public const int TOOL_WIDTH = 40;
    public const int TOOL_PITCH = 42;
    public const int GROUP_LABEL_Y = 255;
    public const int GROUP_Y = 269;
    public const int GROUP_H = 16;
    public const int SIDE_WIDTH = 68;
    public const int SIDE_PITCH = 71;
    public const int FACE_W = 100;
    public const int REMOVE_X = 110;
    public const int REMOVE_W = 106;
    public const int MAX_POINTS = 100;
    public const int DEFENSE_ROW_Y = 70;
    public const int DEFENSE_ROW_H = 14;
    public const int DEFENSE_MAX_ROWS = 8;
    public const int DEFENSE_TOOL_Y = 186;
    public const int DEFENSE_ACTION_Y = 204;
    public const int DEFENSE_BUTTON_H = 16;
    public const int DEFENSE_BUTTON_W = 105;
    public const int DEFENSE_BUTTON_X2 = PAD + DEFENSE_BUTTON_W + 6;
    public const int POINTS_Y = 226;

    internal static readonly string[] TabNames = ["Floors", "Walls", "Decor", "NPCs", "Prefabs", "Defenses"];
    internal static readonly string[] ToolNames = ["Paint", "Box", "Fill", "Erase", "Door"];
    public static readonly string[] TabIconKeys = ["tab_floors", "tab_walls", "tab_decor", "tab_npcs", "tab_rooms", "tab_walls"];
    private static readonly string[] ToolIconKeys = ["tool_paint", "tool_box", "tool_fill", "tool_erase", "tool_door"];
    private static readonly GuildHallTool[] Tools =
        [GuildHallTool.Paint, GuildHallTool.Rect, GuildHallTool.Fill, GuildHallTool.Erase, GuildHallTool.Entrance];
    private static readonly string[] SideNames = ["Both", "Left", "Right"];

    private readonly GuildHallEditorButton CheckButton;
    private readonly GuildHallEditorButton ResetButton;
    private readonly GuildHallEditorButton NextButton;
    private readonly GuildHallEditorButton PrevButton;
    private readonly GuildHallEditorButton RemoveButton;
    private readonly GuildHallEditorButton TurnButton;
    private readonly GuildHallIconCache Icons = new();
    private readonly UIImage LockImage;
    private readonly UILabel NpcGroupLabel;
    private readonly UILabel PriceLabel;
    private readonly UILabel SetNameLabel;
    private readonly UILabel SideGroupLabel;
    private readonly UILabel ToolLabel;
    private readonly ThumbnailGrid<GuildHallPick> Grid;
    private readonly GuildHallThumbnails Thumbnails;
    private readonly GuildHallEditorButton[] TabButtons = new GuildHallEditorButton[TabNames.Length];
    private readonly GuildHallEditorButton[] ToolButtons = new GuildHallEditorButton[ToolNames.Length];
    private readonly GuildHallEditorButton[] DefenseRows = new GuildHallEditorButton[DEFENSE_MAX_ROWS];
    private readonly GuildHallEditorButton AttackerButton;
    private readonly GuildHallEditorButton HeartButton;
    private readonly GuildHallEditorButton DefenseEraseButton;
    private readonly GuildHallEditorButton PracticeButton;
    private readonly UILabel PointsLabel;
    private readonly GuildHallEditorButton[] SideButtons = new GuildHallEditorButton[SideNames.Length];

    public GuildHallEditorControl(CreatureRenderer creatures, EffectRenderer effects)
    {
        Name = "GuildHallEditor";
        Visible = false;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;

        //dark wood at 85% alpha, so the hall stays faintly visible behind the panel
        BackgroundColor = new Color(28, 24, 20, 217);
        BorderColor = new Color(138, 109, 59);
        Thumbnails = new GuildHallThumbnails(creatures, effects);

        Label("GUILD HALL EDITOR", PAD, HEADER_Y + ((HEADER_H - TextRenderer.CHAR_HEIGHT) / 2), TITLE_W, LegendColors.Gold);
        CheckButton = Button(string.Empty, CHECK_X, HEADER_Y, CHECK_W, HEADER_H, Done);
        CheckButton.Green = true;
        ResetButton = Button("Reset", RESET_X, HEADER_Y, RESET_W, HEADER_H, SendReset);

        for (var i = 0; i < TabButtons.Length; i++)
        {
            var tab = (GuildHallTab)i;
            TabButtons[i] = Button(TabNames[i], PAD + (i * TAB_PITCH), TAB_Y, TAB_WIDTH, TAB_H, () => Act(() => State.SetTab(tab)));
            TabButtons[i].Recessed = true;
        }

        PrevButton = Button("<", PAD, SET_Y, ARROW_W, SET_H, () => Act(() => State.StepSet(-1)));
        NextButton = Button(">", PANEL_WIDTH - PAD - ARROW_W, SET_Y, ARROW_W, SET_H, () => Act(() => State.StepSet(1)));

        SetNameLabel = Label(string.Empty, SET_TEXT_X, SET_Y + ((SET_H - TextRenderer.CHAR_HEIGHT) / 2), SET_TEXT_W, LegendColors.White);
        LockImage = new UIImage
        {
            X = SET_TEXT_X,
            Y = SET_Y + ((SET_H - 10) / 2),
            Width = 8,
            Height = 10,
            Tint = LegendColors.Silver,
            IsHitTestVisible = false,
            Visible = false
        };
        AddChild(LockImage);
        PriceLabel = Label(string.Empty, SET_TEXT_X, SET_Y + ((SET_H - TextRenderer.CHAR_HEIGHT) / 2), SET_TEXT_W, LegendColors.Gold);
        PriceLabel.Visible = false;

        Grid = new ThumbnailGrid<GuildHallPick>(GuildHallEditorState.COLUMNS, GuildHallEditorState.ROWS) { X = PAD, Y = GRID_Y };
        Grid.Selected += Pick;
        Grid.PageStepped += delta => Act(() => State.StepPage(delta));
        AddChild(Grid);

        ToolLabel = Label("TOOL", PAD, TOOL_LABEL_Y, PANEL_WIDTH - (2 * PAD), LegendColors.Gold);

        for (var i = 0; i < ToolButtons.Length; i++)
        {
            var tool = Tools[i];
            ToolButtons[i] = Button(ToolNames[i], PAD + (i * TOOL_PITCH), TOOL_Y, TOOL_WIDTH, TOOL_H, () => Act(() => State.SetTool(tool)));
        }

        SideGroupLabel = Label("WALL SIDE", PAD, GROUP_LABEL_Y, PANEL_WIDTH - (2 * PAD), LegendColors.Gold);

        for (var i = 0; i < SideButtons.Length; i++)
        {
            var side = (GuildHallSide)i;
            SideButtons[i] = Button(SideNames[i], PAD + (i * SIDE_PITCH), GROUP_Y, SIDE_WIDTH, GROUP_H, () => Act(() => State.SetSide(side)));
        }

        NpcGroupLabel = Label("SELECTED NPC", PAD, GROUP_LABEL_Y, PANEL_WIDTH - (2 * PAD), LegendColors.Gold);
        TurnButton = Button("Face: Down", PAD, GROUP_Y, FACE_W, GROUP_H, () => Act(State.TurnNpc));
        RemoveButton = Button("Remove", REMOVE_X, GROUP_Y, REMOVE_W, GROUP_H, RemoveNpc);

        for (var i = 0; i < DefenseRows.Length; i++)
        {
            var index = i;
            DefenseRows[i] = Button(string.Empty, PAD, DEFENSE_ROW_Y + (i * DEFENSE_ROW_H), PANEL_WIDTH - (2 * PAD), DEFENSE_ROW_H - 1, () => PickDefense(index));
        }

        AttackerButton = Button("Attacker entrance", PAD, DEFENSE_TOOL_Y, DEFENSE_BUTTON_W, DEFENSE_BUTTON_H, () => Act(() => State.SetTool(GuildHallTool.AttackerEntrance)));
        HeartButton = Button("Guild Heart", DEFENSE_BUTTON_X2, DEFENSE_TOOL_Y, DEFENSE_BUTTON_W, DEFENSE_BUTTON_H, () => Act(() => State.SetTool(GuildHallTool.Heart)));
        DefenseEraseButton = Button("Erase", PAD, DEFENSE_ACTION_Y, DEFENSE_BUTTON_W, DEFENSE_BUTTON_H, () => Act(() => State.SetTool(GuildHallTool.Erase)));
        PracticeButton = Button("Start practice", DEFENSE_BUTTON_X2, DEFENSE_ACTION_Y, DEFENSE_BUTTON_W, DEFENSE_BUTTON_H, SendPractice);
        PointsLabel = Label(string.Empty, PAD, POINTS_Y, PANEL_WIDTH - (2 * PAD), LegendColors.White);
    }

    /// <summary>Raised with each action to send to the server.</summary>
    public event Action<GuildHallEditorArgs>? EditRequested;

    /// <summary>Raised with a one-line message for the orange bar.</summary>
    public event Action<string>? MessageRequested;

    private static GuildHallEditorState State => WorldState.GuildHallEditor;

    /// <summary>Top-left that puts the panel against the viewport's right edge and top, kept on screen.</summary>
    public static Point DockOrigin(Rectangle viewport)
        => new(
            Math.Clamp(viewport.Right - PANEL_WIDTH - 2, 0, ChaosGame.VIRTUAL_WIDTH - PANEL_WIDTH),
            Math.Clamp(viewport.Y + 2, 0, ChaosGame.VIRTUAL_HEIGHT - PANEL_HEIGHT));

    /// <summary>Every visible control's bounds for <paramref name="tab" />, for layout tests.</summary>
    public static IReadOnlyList<Rectangle> VisibleBounds(GuildHallTab tab)
    {
        var bounds = new List<Rectangle>
        {
            new(PAD, HEADER_Y, TITLE_W, HEADER_H),
            new(RESET_X, HEADER_Y, RESET_W, HEADER_H),
            new(CHECK_X, HEADER_Y, CHECK_W, HEADER_H)
        };

        for (var i = 0; i < TabNames.Length; i++)
            bounds.Add(new Rectangle(PAD + (i * TAB_PITCH), TAB_Y, TAB_WIDTH, TAB_H));

        if (tab == GuildHallTab.Defenses)
        {
            for (var i = 0; i < DEFENSE_MAX_ROWS; i++)
                bounds.Add(new Rectangle(PAD, DEFENSE_ROW_Y + (i * DEFENSE_ROW_H), PANEL_WIDTH - (2 * PAD), DEFENSE_ROW_H - 1));

            bounds.Add(new Rectangle(PAD, DEFENSE_TOOL_Y, DEFENSE_BUTTON_W, DEFENSE_BUTTON_H));
            bounds.Add(new Rectangle(DEFENSE_BUTTON_X2, DEFENSE_TOOL_Y, DEFENSE_BUTTON_W, DEFENSE_BUTTON_H));
            bounds.Add(new Rectangle(PAD, DEFENSE_ACTION_Y, DEFENSE_BUTTON_W, DEFENSE_BUTTON_H));
            bounds.Add(new Rectangle(DEFENSE_BUTTON_X2, DEFENSE_ACTION_Y, DEFENSE_BUTTON_W, DEFENSE_BUTTON_H));
            bounds.Add(new Rectangle(PAD, POINTS_Y, PANEL_WIDTH - (2 * PAD), TextRenderer.CHAR_HEIGHT));

            return bounds;
        }

        if (tab != GuildHallTab.Npcs)
        {
            bounds.Add(new Rectangle(PAD, SET_Y, ARROW_W, SET_H));
            bounds.Add(new Rectangle(PANEL_WIDTH - PAD - ARROW_W, SET_Y, ARROW_W, SET_H));
        }

        bounds.Add(new Rectangle(SET_TEXT_X, SET_Y, SET_TEXT_W, SET_H));
        bounds.Add(
            new Rectangle(
                PAD,
                GRID_Y,
                ThumbnailGrid<GuildHallPick>.WidthFor(GuildHallEditorState.COLUMNS),
                ThumbnailGrid<GuildHallPick>.HeightFor(GuildHallEditorState.ROWS)));

        if (tab != GuildHallTab.Npcs)
        {
            bounds.Add(new Rectangle(PAD, TOOL_LABEL_Y, PANEL_WIDTH - (2 * PAD), TextRenderer.CHAR_HEIGHT));

            for (var i = 0; i < ToolNames.Length; i++)
                bounds.Add(new Rectangle(PAD + (i * TOOL_PITCH), TOOL_Y, TOOL_WIDTH, TOOL_H));
        }

        if (tab is GuildHallTab.Walls or GuildHallTab.Decor)
        {
            bounds.Add(new Rectangle(PAD, GROUP_LABEL_Y, PANEL_WIDTH - (2 * PAD), TextRenderer.CHAR_HEIGHT));

            for (var i = 0; i < SideNames.Length; i++)
                bounds.Add(new Rectangle(PAD + (i * SIDE_PITCH), GROUP_Y, SIDE_WIDTH, GROUP_H));
        }

        if (tab == GuildHallTab.Npcs)
        {
            bounds.Add(new Rectangle(PAD, GROUP_LABEL_Y, PANEL_WIDTH - (2 * PAD), TextRenderer.CHAR_HEIGHT));
            bounds.Add(new Rectangle(PAD, GROUP_Y, FACE_W, GROUP_H));
            bounds.Add(new Rectangle(REMOVE_X, GROUP_Y, REMOVE_W, GROUP_H));
        }

        return bounds;
    }

    /// <summary>The locked set's row text: name, then the price with thousands separators.</summary>
    internal static string LockedSetRowText(GuildHallPaletteSet set)
        => $"{set.Name} {LockedSetPriceText(set.Price)}";

    /// <summary>A set price with thousands separators ("10,000,000"), whatever the client's culture.</summary>
    internal static string LockedSetPriceText(int price)
        => price.ToString("N0", CultureInfo.InvariantCulture);

    public void Dock(Rectangle viewport)
    {
        var origin = DockOrigin(viewport);
        X = origin.X;
        Y = origin.Y;
    }

    public void Show(Rectangle viewport)
    {
        Dock(viewport);
        ResetButton.Caption = State.ResetCaption;
        Visible = true;
        RefreshFromState();
    }

    public void Hide()
    {
        if (!Visible)
            return;

        Visible = false;
        Thumbnails.Clear();
        Icons.Clear();
    }

    private void SendReset()
    {
        if (State.RequestReset(out var args) && args is not null)
            EditRequested?.Invoke(args);

        ResetButton.Caption = State.ResetCaption;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!State.ResetArmed)
            return;

        State.ResetUpdate(gameTime.ElapsedGameTime);
        ResetButton.Caption = State.ResetCaption;
    }

    public override void Dispose()
    {
        Icons.Dispose();
        base.Dispose();
    }

    public void RefreshFromState()
    {
        var state = State;

        CheckButton.Icon = Icons.Get("check");

        for (var i = 0; i < TabButtons.Length; i++)
        {
            var selected = (int)state.Tab == i;
            TabButtons[i].Icon = Icons.Get(TabIconKeys[i]);
            TabButtons[i].Selected = selected;
            TabButtons[i].Recessed = !selected;
            TabButtons[i].JoinedBottom = selected;
        }

        RefreshDefenses(state);

        var defenseTab = state.Tab == GuildHallTab.Defenses;
        var npcTab = state.Tab == GuildHallTab.Npcs;
        PrevButton.Visible = !npcTab && !defenseTab;
        NextButton.Visible = !npcTab && !defenseTab;

        if (defenseTab)
        {
            SetNameLabel.Visible = false;
            LockImage.Visible = false;
            PriceLabel.Visible = false;
            Grid.Visible = false;
            ToolLabel.Visible = false;

            foreach (var button in ToolButtons)
                button.Visible = false;

            foreach (var button in SideButtons)
                button.Visible = false;

            NpcGroupLabel.Visible = false;
            TurnButton.Visible = false;
            RemoveButton.Visible = false;
            SideGroupLabel.Visible = false;

            return;
        }

        SetNameLabel.Visible = true;
        Grid.Visible = true;

        if (npcTab)
        {
            SetNameLabel.Text = "Unlocked NPCs";
            SetNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            SetNameLabel.X = SET_TEXT_X;
            SetNameLabel.Width = SET_TEXT_W;
            LockImage.Visible = false;
            PriceLabel.Visible = false;
        } else if (state.CurrentSet is { } set)
        {
            var locked = set.Owned == false;

            if (locked)
            {
                var priceText = LockedSetPriceText(set.Price);
                var nameWidth = TextRenderer.MeasureWidth(set.Name);
                var priceWidth = TextRenderer.MeasureWidth(priceText);
                const int gap = 4;
                var total = nameWidth + gap + LockImage.Width + gap + priceWidth;
                var startX = SET_TEXT_X + Math.Max(0, (SET_TEXT_W - total) / 2);

                SetNameLabel.Text = set.Name;
                SetNameLabel.HorizontalAlignment = HorizontalAlignment.Left;
                SetNameLabel.X = startX;
                SetNameLabel.Width = nameWidth;
                LockImage.Texture = Icons.Get("lock");
                LockImage.Visible = true;
                LockImage.X = startX + nameWidth + gap;
                PriceLabel.Text = priceText;
                PriceLabel.HorizontalAlignment = HorizontalAlignment.Left;
                PriceLabel.X = startX + nameWidth + gap + LockImage.Width + gap;
                PriceLabel.Width = priceWidth;
                PriceLabel.Visible = true;
            } else
            {
                SetNameLabel.Text = set.Name;
                SetNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
                SetNameLabel.X = SET_TEXT_X;
                SetNameLabel.Width = SET_TEXT_W;
                LockImage.Visible = false;
                PriceLabel.Visible = false;
            }
        } else
        {
            SetNameLabel.Text = "No tile sets";
            SetNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            SetNameLabel.X = SET_TEXT_X;
            SetNameLabel.Width = SET_TEXT_W;
            LockImage.Visible = false;
            PriceLabel.Visible = false;
        }

        for (var i = 0; i < ToolButtons.Length; i++)
        {
            ToolButtons[i].Icon = Icons.Get(ToolIconKeys[i]);
            ToolButtons[i].Selected = state.Tool == Tools[i];
            ToolButtons[i].Visible = !npcTab;
        }

        ToolLabel.Visible = !npcTab;

        var foregroundTab = state.Tab is GuildHallTab.Walls or GuildHallTab.Decor;
        SideGroupLabel.Visible = foregroundTab;

        for (var i = 0; i < SideButtons.Length; i++)
        {
            SideButtons[i].Selected = (int)state.Side == i;
            SideButtons[i].Visible = foregroundTab;
        }

        NpcGroupLabel.Visible = npcTab;
        TurnButton.Visible = npcTab;
        RemoveButton.Visible = npcTab;
        RemoveButton.Enabled = state.SelectedNpc is { Placed: true };
        TurnButton.Caption = $"Face: {state.NpcFacing}";

        var lockedSet = !npcTab && state.CurrentSetLocked;
        var npcs = state.Npcs;
        var prefabs = state.CurrentSet?.Prefabs ?? [];

        //GuildHallPick is a value type, so T? is just T: pass a real value and drive the highlight with isSelected
        Grid.SetItems(
            state.PageItems,
            state.Selected ?? default,
            state.PageCount,
            pick => Thumbnails.For(pick, npcs, prefabs),
            isSelected: pick => state.Selected is { } selected && pick.Equals(selected),
            isDimmed: _ => lockedSet,
            isMarked: npcTab ? pick => npcs.Any(n => (n.Key == pick.NpcKey) && n.Placed) : null);
    }

    private void RefreshDefenses(GuildHallEditorState state)
    {
        var defenseTab = state.Tab == GuildHallTab.Defenses;
        var practice = state.PracticeRunning;

        for (var i = 0; i < DefenseRows.Length; i++)
        {
            var row = DefenseRows[i];
            var option = i < state.Defenses.Count ? state.Defenses[i] : null;

            row.Visible = defenseTab && option is not null;

            if (option is null)
                continue;

            row.Caption = $"{option.Name}  {option.Points} pts";
            row.LeadingImage = Thumbnails.Defense(option.Kind, option.Icon);
            row.Selected = (state.Tool == GuildHallTool.Defense) && (state.SelectedDefense == option.Key);
            row.Enabled = !practice;
        }

        AttackerButton.Visible = defenseTab;
        AttackerButton.Selected = state.Tool == GuildHallTool.AttackerEntrance;
        AttackerButton.Enabled = !practice;
        HeartButton.Visible = defenseTab;
        HeartButton.Selected = state.Tool == GuildHallTool.Heart;
        HeartButton.Enabled = !practice;
        DefenseEraseButton.Visible = defenseTab;
        DefenseEraseButton.Selected = state.Tool == GuildHallTool.Erase;
        DefenseEraseButton.Enabled = !practice;
        PracticeButton.Visible = defenseTab;
        PracticeButton.Caption = practice ? "End practice" : "Start practice";
        PointsLabel.Visible = defenseTab;
        PointsLabel.Text = $"Points: {state.PointsUsed} / {MAX_POINTS}";
    }

    private void PickDefense(int index)
    {
        if ((index >= State.Defenses.Count) || State.PracticeRunning)
            return;

        State.SelectedDefense = State.Defenses[index].Key;
        Act(() => State.SetTool(GuildHallTool.Defense));
    }

    private void SendPractice()
        => EditRequested?.Invoke(
            new GuildHallEditorArgs { Action = State.PracticeRunning ? GuildHallEditorAction.EndPractice : GuildHallEditorAction.StartPractice });

    /// <summary>Frees the thumbnail textures. Called when the world screen unwires with the panel open.</summary>
    public void DisposeThumbnails() => Thumbnails.Clear();

    private void Act(Action change)
    {
        change();
        RefreshFromState();
    }

    private void Pick(GuildHallPick pick)
    {
        if (!State.Select(pick) && State.CurrentSet is { } set)
        {
            var message = $"Buy {set.Name} from Tibbs to use these tiles.";
            State.Refuse(message);
            MessageRequested?.Invoke(message);
        }

        RefreshFromState();
    }

    private void RemoveNpc()
    {
        if (State.SelectedNpc is not { Placed: true } npc)
            return;

        EditRequested?.Invoke(new GuildHallEditorArgs { Action = GuildHallEditorAction.RemoveNpc, NpcKey = npc.Key });
    }

    private void Done()
    {
        EditRequested?.Invoke(new GuildHallEditorArgs { Action = GuildHallEditorAction.End });
        State.Close();
        Hide();
    }

    private GuildHallEditorButton Button(string caption, int x, int y, int width, int height, Action onClick)
    {
        var button = new GuildHallEditorButton(caption, width, height) { X = x, Y = y };
        button.Clicked += onClick;
        AddChild(button);

        return button;
    }

    private UILabel Label(string text, int x, int y, int width, Color color)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            PaddingLeft = 0,
            PaddingRight = 0,
            PaddingTop = 0,
            PaddingBottom = 0,
            ForegroundColor = color,
            IsHitTestVisible = false,
            Text = text
        };

        AddChild(label);

        return label;
    }
}
