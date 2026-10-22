#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.Controls.World.Popups.Theatre;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
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
    public const int SET_Y = 20;
    public const int TAB_Y = 38;
    public const int TOOL_Y = 56;
    public const int TOOL2_Y = 74;
    public const int SIDE_Y = 94;
    public const int GRID_Y = 112;
    public const int STATUS_Y = 254;
    public const int DONE_Y = 270;
    private const int BUTTON_WIDTH = 51;
    private const int BUTTON_PITCH = 53;

    private static readonly string[] TabNames = ["Floors", "Walls", "Decor", "NPCs"];
    private static readonly string[] ToolNames = ["Paint", "Rect", "Fill", "Erase"];
    private static readonly string[] SideNames = ["Both", "Left", "Right"];

    private readonly StageButton EntranceButton;
    private readonly ThumbnailGrid<GuildHallPick> Grid;
    private readonly StageButton RemoveButton;
    private readonly UILabel SetLabel;
    private readonly UILabel StatusLabel;
    private readonly StageButton[] TabButtons = new StageButton[4];
    private readonly GuildHallThumbnails Thumbnails;
    private readonly StageButton[] ToolButtons = new StageButton[4];
    private readonly StageButton[] SideButtons = new StageButton[3];
    private readonly StageButton TurnButton;

    public GuildHallEditorControl(CreatureRenderer creatures)
    {
        Name = "GuildHallEditor";
        Visible = false;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        BackgroundColor = new Color(28, 24, 20);
        BorderColor = new Color(138, 109, 59);
        Thumbnails = new GuildHallThumbnails(creatures);

        Label("GUILD HALL EDITOR", PAD, 2, 160, LegendColors.Gold);

        Button("<", PAD, SET_Y, 16, () => Act(() => State.StepSet(-1)));
        SetLabel = Label(string.Empty, PAD + 18, SET_Y + 2, PANEL_WIDTH - (2 * PAD) - 36, LegendColors.White);
        Button(">", PANEL_WIDTH - PAD - 16, SET_Y, 16, () => Act(() => State.StepSet(1)));

        for (var i = 0; i < TabButtons.Length; i++)
        {
            var tab = (GuildHallTab)i;
            TabButtons[i] = Button(TabNames[i], PAD + (i * BUTTON_PITCH), TAB_Y, BUTTON_WIDTH, () => Act(() => State.SetTab(tab)));
        }

        for (var i = 0; i < ToolButtons.Length; i++)
        {
            var tool = (GuildHallTool)i;
            ToolButtons[i] = Button(ToolNames[i], PAD + (i * BUTTON_PITCH), TOOL_Y, BUTTON_WIDTH, () => Act(() => State.SetTool(tool)));
        }

        for (var i = 0; i < SideButtons.Length; i++)
        {
            var side = (GuildHallSide)i;
            SideButtons[i] = Button(SideNames[i], PAD + (i * BUTTON_PITCH), SIDE_Y, BUTTON_WIDTH, () => Act(() => State.SetSide(side)));
        }

        EntranceButton = Button("Entrance", PAD, TOOL2_Y, 70, () => Act(() => State.SetTool(GuildHallTool.Entrance)));
        TurnButton = Button("Face: Down", PAD + 74, TOOL2_Y, 70, () => Act(State.TurnNpc));
        RemoveButton = Button("Remove", PAD + 148, TOOL2_Y, 62, RemoveNpc);

        Grid = new ThumbnailGrid<GuildHallPick>(GuildHallEditorState.COLUMNS, GuildHallEditorState.ROWS) { X = PAD, Y = GRID_Y };
        Grid.Selected += Pick;
        Grid.PageStepped += delta => Act(() => State.StepPage(delta));
        AddChild(Grid);

        StatusLabel = Label(string.Empty, PAD, STATUS_Y, PANEL_WIDTH - (2 * PAD), LegendColors.Gray);
        Button("Done", PANEL_WIDTH - PAD - 64, DONE_Y, 64, Done);
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

    public void Dock(Rectangle viewport)
    {
        var origin = DockOrigin(viewport);
        X = origin.X;
        Y = origin.Y;
    }

    public void Show(Rectangle viewport)
    {
        Dock(viewport);
        Visible = true;
        RefreshFromState();
    }

    public void Hide()
    {
        if (!Visible)
            return;

        Visible = false;
        Thumbnails.Clear();
    }

    public void RefreshFromState()
    {
        var state = State;

        if (state.Tab == GuildHallTab.Npcs)
            SetLabel.Text = "Unlocked NPCs";
        else if (state.CurrentSet is { } set)
            SetLabel.Text = set.Name;
        else
            SetLabel.Text = "No tile sets";

        for (var i = 0; i < TabButtons.Length; i++)
            TabButtons[i].Selected = (int)state.Tab == i;

        for (var i = 0; i < ToolButtons.Length; i++)
            ToolButtons[i].Selected = (int)state.Tool == i;

        var foregroundTab = state.Tab is GuildHallTab.Walls or GuildHallTab.Decor;

        for (var i = 0; i < SideButtons.Length; i++)
        {
            SideButtons[i].Selected = (int)state.Side == i;
            SideButtons[i].Visible = foregroundTab;
        }

        EntranceButton.Selected = state.Tool == GuildHallTool.Entrance;

        var npcTab = state.Tab == GuildHallTab.Npcs;
        TurnButton.Visible = npcTab;
        RemoveButton.Visible = npcTab;
        RemoveButton.Enabled = state.SelectedNpc is { Placed: true };
        TurnButton.Caption = $"Face: {state.NpcFacing}";

        var locked = !npcTab && state.CurrentSetLocked;
        var npcs = state.Npcs;

        //GuildHallPick is a value type, so T? is just T: pass a real value and drive the highlight with isSelected
        Grid.SetItems(
            state.PageItems,
            state.Selected ?? default,
            state.PageCount,
            pick => Thumbnails.For(pick, npcs),
            isSelected: pick => state.Selected is { } selected && pick.Equals(selected),
            isDimmed: _ => locked,
            isMarked: npcTab ? pick => npcs.Any(n => (n.Key == pick.NpcKey) && n.Placed) : null);

        //server reasons run longer than the line fits, so they are shortened here; the orange bar keeps the full text
        StatusLabel.Text = ShortenStatus(
            state.ActiveRefusal
            ?? (locked && state.CurrentSet is { } lockedSet ? $"Locked: {lockedSet.Price:N0} gold" : HintFor(state.Tool)));
    }

    /// <summary>Shortens a status line to the width the panel fits, with an ellipsis.</summary>
    internal static string ShortenStatus(string text, int maxLength = 30)
        => text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength - 3), "...");

    /// <summary>Frees the thumbnail textures. Called when the world screen unwires with the panel open.</summary>
    public void DisposeThumbnails() => Thumbnails.Clear();

    private static string HintFor(GuildHallTool tool)
        => tool switch
        {
            GuildHallTool.Paint    => "Click or drag to paint.",
            GuildHallTool.Rect     => "Drag a rectangle.",
            GuildHallTool.Fill     => "Click to fill matching tiles.",
            GuildHallTool.Erase    => "Click or drag to clear walls.",
            GuildHallTool.Npc      => "Pick an NPC, click a tile.",
            GuildHallTool.Entrance => "Click the new entrance tile.",
            _                      => string.Empty
        };

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

    private StageButton Button(string caption, int x, int y, int width, Action onClick)
    {
        var button = new StageButton(caption, width) { X = x, Y = y };
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
