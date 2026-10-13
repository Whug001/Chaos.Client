#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The live board, read-only (512 x 380): the board at zoom 3 and who holds the pen. The Teacher's copy picks a student to
///     give the pen to or takes it back; Edit shows while the player can draw.
/// </summary>
public sealed class BoardViewControl : GuildCloakDialogBase
{
    private const int WIDTH = 512;
    private const int HEIGHT = 380;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int BOARD_X = LEFT;
    private const int BOARD_Y = 30;
    private const int BOARD_WIDTH = CollegeProtocol.BOARD_WIDTH * CollegeProtocol.BOARD_ZOOM;
    private const int BOARD_HEIGHT = CollegeProtocol.BOARD_HEIGHT * CollegeProtocol.BOARD_ZOOM;
    private const int PEN_TOP = BOARD_Y + BOARD_HEIGHT + 8;
    private const int BUTTONS_TOP = HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int ARROW_WIDTH = 28;
    private const int NAME_X = LEFT + ARROW_WIDTH + 4;
    private const int NAME_WIDTH = 120;
    private const int NEXT_X = NAME_X + NAME_WIDTH + 4;
    private const int GIVE_X = NEXT_X + ARROW_WIDTH + 8;
    private const int GIVE_WIDTH = 80;
    private const int TAKE_X = GIVE_X + GIVE_WIDTH + 6;
    private const int TAKE_WIDTH = 120;
    private const int EDIT_WIDTH = 60;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly CustomButton EditButton;
    private readonly CustomButton GiveButton;
    private readonly UILabel NameLabel;
    private readonly CustomButton NextButton;
    private readonly UILabel PenLabel;
    private readonly CustomButton PrevButton;
    private readonly CustomButton TakeButton;

    private int ShownDrawingVersion = -1;
    private List<string> Students = [];
    private int StudentIndex;
    private Texture2D? Texture;

    public BoardViewControl()
        : base("_nsett", false)
    {
        Name = "BoardView";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        Caption("Live board", LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        PenLabel = Caption(string.Empty, LEFT, PEN_TOP, INNER_WIDTH, color: LegendColors.Gray);
        PrevButton = AddButton("<", ARROW_WIDTH, LEFT, BUTTONS_TOP, () => Step(-1));
        NameLabel = Caption(string.Empty, NAME_X, BUTTONS_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2), NAME_WIDTH, HorizontalAlignment.Center);
        NextButton = AddButton(">", ARROW_WIDTH, NEXT_X, BUTTONS_TOP, () => Step(1));
        GiveButton = AddButton("Give pen", GIVE_WIDTH, GIVE_X, BUTTONS_TOP, OnGiveClicked);
        TakeButton = AddButton("Take pen back", TAKE_WIDTH, TAKE_X, BUTTONS_TOP, OnTakeClicked);
        EditButton = AddButton("Edit", EDIT_WIDTH, LEFT + INNER_WIDTH - EDIT_WIDTH, BUTTONS_TOP, () => EditRequested?.Invoke());
    }

    /// <summary>Raised with BoardPen.</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    public event Action? EditRequested;

    public void Open(BoardState state)
    {
        Refresh(state);

        if (!Visible)
            Show();
    }

    public void Refresh(BoardState state)
    {
        if (ShownDrawingVersion != state.DrawingVersion)
        {
            Texture?.Dispose();
            Texture = DrawingTextures.BuildBoard(state.Drawing, CollegeProtocol.BOARD_ZOOM);
            ShownDrawingVersion = state.DrawingVersion;
        }

        var info = state.Info;
        var kept = StudentIndex < Students.Count ? Students[StudentIndex] : null;
        Students = info.Students;
        StudentIndex = kept is null ? 0 : Math.Max(0, Students.IndexOf(kept));

        PenLabel.Text = !info.Live ? "No class is running."
            : $"Pen: {(info.Holder.Length > 0 ? info.Holder : "the Teacher")}";

        var picking = info.IsTeacher && info.Live;
        PrevButton.Visible = picking;
        NextButton.Visible = picking;
        NameLabel.Visible = picking;
        GiveButton.Visible = picking;
        TakeButton.Visible = picking && (info.Holder.Length > 0);
        EditButton.Visible = info.CanDraw;
        ShowStudent();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        Texture?.Dispose();
        Texture = null;
        ShownDrawingVersion = -1;
        base.Hide();
    }

    public override void Dispose()
    {
        Texture?.Dispose();
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
        DrawTexture(spriteBatch, Texture, new Vector2(ScreenX + BOARD_X, ScreenY + BOARD_Y), Color.White);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    private void ShowStudent()
    {
        NameLabel.Text = StudentIndex < Students.Count ? Students[StudentIndex] : "No students";
        GiveButton.Enabled = StudentIndex < Students.Count;
    }

    private void Step(int by)
    {
        if (Students.Count == 0)
            return;

        StudentIndex = (StudentIndex + by + Students.Count) % Students.Count;
        ShowStudent();
    }

    private void OnGiveClicked()
    {
        if (StudentIndex < Students.Count)
            ActionRequested?.Invoke(
                new CollegeActionArgs
                {
                    Type = CollegeActionType.BoardPen,
                    Names = [Students[StudentIndex]]
                });
    }

    private void OnTakeClicked() => ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.BoardPen });
}
