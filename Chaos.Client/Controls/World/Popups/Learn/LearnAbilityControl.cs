#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Learn;

/// <summary>
///     The learn-ability popup: a skill or spell's icon, name, level, description and every requirement with a green tick
///     or red cross, over the NPC dialog that offered it. Its buttons stand in for that dialog's options: each one closes
///     the popup and then selects the matching option, so the server sees an ordinary menu answer.
/// </summary>
/// <remarks>
///     Layout, top to bottom: icon with title and Replaces line beside it, the description (scrolls past five lines), a
///     "Requirements" caption, the requirement rows (scroll when the window would not fit), the button row, then the frame's
///     bottom border. Sized to its content, about 480 wide, capped at the world viewport's height and centred on it.
/// </remarks>
public sealed class LearnAbilityControl : HandBuiltDialogBase
{
    private const int WIDTH = 480;
    private const int LEFT = 16;
    private const int INNER = WIDTH - (2 * LEFT);
    private const int TOP = 14;
    private const int ICON = 32;
    private const int GAP = 8;
    private const int DESCRIPTION_MAX_LINES = 5;
    private const int BORDER_GAP = 4;
    private const int BUTTON_GAP = 8;
    private const int LEARN_WIDTH = 90;
    private const int GUILD_WIDTH = 130;
    private const int CANCEL_WIDTH = 90;

    private readonly CustomButton CancelButton;
    private readonly UILabel DescriptionLabel;
    private readonly ScrollViewerControl DescriptionViewer;
    private readonly CustomButton GuildButton;
    private readonly CustomButton LearnButton;
    private readonly UILabel NoRequirementsLabel;
    private readonly UILabel ReplacesLabel;
    private readonly UILabel RequirementsCaption;
    private readonly LearnRequirementList RowList;
    private readonly ScrollViewerControl RowViewer;
    private readonly NpcSessionControl Session;
    private readonly LearnAbilityPopupState State = new();
    private readonly UILabel TitleLabel;
    private Texture2D? Icon;

    public LearnAbilityControl(NpcSessionControl session)
        : base("_nsett", false)
    {
        Session = session;
        Name = "LearnAbility";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;

        var textLeft = LEFT + ICON + GAP;
        TitleLabel = Caption(string.Empty, textLeft, TOP, WIDTH - textLeft - LEFT, color: LegendColors.Gold);
        ReplacesLabel = Caption(string.Empty, textLeft, TOP + 16, WIDTH - textLeft - LEFT, color: LegendColors.Silver);

        DescriptionLabel = new UILabel
        {
            Width = INNER,
            WordWrap = true,
            ColorCodesEnabled = true,
            VerticalAlignment = VerticalAlignment.Top,
            ForegroundColor = TextColors.Default
        };

        DescriptionViewer = new ScrollViewerControl(DescriptionLabel)
        {
            X = LEFT,
            Width = INNER,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        AddChild(DescriptionViewer);

        RequirementsCaption = Caption("Requirements", LEFT, 0, INNER, color: LegendColors.Gold);
        NoRequirementsLabel = Caption("None", LEFT + 4, 0, INNER, color: LegendColors.Silver);

        RowList = new LearnRequirementList();

        RowViewer = new ScrollViewerControl(RowList)
        {
            X = LEFT,
            Width = INNER,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        AddChild(RowViewer);

        LearnButton = AddButton("Learn", LEARN_WIDTH, 0, 0, () => Answer(LearnAbilityButton.Learn));
        GuildButton = AddButton("Use Guild Funds", GUILD_WIDTH, 0, 0, () => Answer(LearnAbilityButton.GuildFunds));
        CancelButton = AddButton("Cancel", CANCEL_WIDTH, 0, 0, () => Answer(LearnAbilityButton.Cancel));
    }

    /// <summary>
    ///     Opens the popup for <paramref name="args" /> over the NPC dialog, hiding the whole dialog while it is
    ///     up. <paramref name="area" /> is the world viewport: the popup is centred on it and never taller than it.
    /// </summary>
    public void Show(LearnAbilityDisplayArgs args, Rectangle area)
    {
        var view = State.Open(args);

        Icon = args.IsSkill ? UiRenderer.Instance!.GetSkillIcon(args.Sprite) : UiRenderer.Instance!.GetSpellIcon(args.Sprite);
        TitleLabel.Text = view.Title;
        ReplacesLabel.Text = view.ReplacesLine ?? string.Empty;
        ReplacesLabel.Visible = view.ReplacesLine is not null;

        //title alone sits level with the icon's middle; with a Replaces line the two share the icon's height
        TitleLabel.Y = view.ReplacesLine is null ? TOP + ((ICON - TextRenderer.CHAR_HEIGHT) / 2) : TOP;

        Layout(view, area);

        Session.SetHiddenBehindPopup(true);

        if (!Visible)
            Show();
    }

    /// <summary>
    ///     Takes the popup down without answering and shows the dialog again. Safe to call when it is
    ///     already hidden.
    /// </summary>
    public override void Hide()
    {
        State.Close();

        if (!Visible)
            return;

        Session.SetHiddenBehindPopup(false);
        base.Hide();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Answer(LearnAbilityButton.Cancel);
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        //the dialog under it went away (Top, a map change, a server close): nothing is left for the popup to answer
        if (Visible && !Session.Visible)
        {
            Hide();

            return;
        }

        base.Update(gameTime);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        //drawn here rather than through a UIImage: the icon is the renderer's cached texture, which a UIImage would
        //dispose with itself
        if (Icon is not null)
            DrawTexture(spriteBatch, Icon, new Vector2(ScreenX + LEFT, ScreenY + TOP), Color.White);
    }

    private void Answer(LearnAbilityButton button)
    {
        //read before Hide: Hide closes the state, and the dialog's options must be visible again before one is picked
        var option = State.Press(button);
        Hide();

        if (option is { } index)
            Session.SelectOption(index);
    }

    private void Layout(LearnAbilityView view, Rectangle area)
    {
        var y = TOP + ICON + GAP;

        //description: measured at the full width, shown up to five lines, scrolling beyond that
        DescriptionViewer.Y = y;
        DescriptionLabel.Width = INNER;
        DescriptionLabel.Text = view.Description;
        DescriptionLabel.ScrollOffset = 0;

        var descriptionLines = string.IsNullOrWhiteSpace(view.Description)
            ? 0
            : Math.Max(1, DescriptionLabel.ContentHeight / TextRenderer.CHAR_HEIGHT);

        var descriptionHeight = descriptionLines == 0
            ? 0
            : (Math.Min(descriptionLines, DESCRIPTION_MAX_LINES) * TextRenderer.CHAR_HEIGHT) + DescriptionLabel.PaddingTop
                                                                                            + DescriptionLabel.PaddingBottom;

        DescriptionViewer.Visible = descriptionLines > 0;
        DescriptionViewer.Height = descriptionHeight;
        DescriptionLabel.Height = descriptionHeight;

        if (descriptionLines > 0)
            y += descriptionHeight + GAP;

        RequirementsCaption.Y = y;
        y += TextRenderer.CHAR_HEIGHT + 4;

        //everything below the rows: a gap, the button row, and the frame's bottom border
        var footer = GAP + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;

        var rowCount = view.Rows.Count;
        NoRequirementsLabel.Visible = rowCount == 0;
        RowViewer.Visible = rowCount > 0;
        RowList.SetRows(view.Rows);

        int rowsHeight;

        if (rowCount == 0)
        {
            NoRequirementsLabel.Y = y;
            rowsHeight = TextRenderer.CHAR_HEIGHT;
        } else
        {
            //as many rows as fit in the viewport, at least two, scrolling the rest
            var fitting = Math.Max(2, (area.Height - y - footer) / LearnRequirementList.ROW_HEIGHT);
            var shown = Math.Min(rowCount, fitting);
            rowsHeight = shown * LearnRequirementList.ROW_HEIGHT;

            RowViewer.Y = y;
            RowViewer.Height = rowsHeight;
            RowList.Height = rowsHeight;
        }

        y += rowsHeight + GAP;

        //buttons, centred: Learn, Use Guild Funds when offered, Cancel when offered
        LearnButton.Visible = State.Offers(LearnAbilityButton.Learn);
        GuildButton.Visible = State.Offers(LearnAbilityButton.GuildFunds);
        CancelButton.Visible = State.Offers(LearnAbilityButton.Cancel);

        var buttons = new[] { LearnButton, GuildButton, CancelButton }.Where(b => b.Visible).ToList();
        var rowWidth = buttons.Sum(b => b.Width) + (Math.Max(0, buttons.Count - 1) * BUTTON_GAP);
        var x = (WIDTH - rowWidth) / 2;

        foreach (var button in buttons)
        {
            button.X = x;
            button.Y = y;
            x += button.Width + BUTTON_GAP;
        }

        y += CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;

        //the rows already shrank to fit the area; the window is never cut shorter than its content, or the buttons would go
        Height = y;
        X = area.X + ((area.Width - Width) / 2);
        Y = area.Y + Math.Max(0, (area.Height - Height) / 2);
    }

    /// <summary>
    ///     The requirement rows: name on the left, amount right-aligned, then a green tick or red cross. Scrolls a whole row
    ///     at a time through <see cref="IVerticalScrollable" />.
    /// </summary>
    private sealed class LearnRequirementList : UIElement, IVerticalScrollable
    {
        public const int ROW_HEIGHT = TextRenderer.CHAR_HEIGHT + 4;
        private const int MARK_SIZE = 9;
        private const int MARK_COLUMN = 16;

        private static readonly Color Stripe = Color.Black * 0.18f;

        private IReadOnlyList<LearnAbilityRow> Rows = [];
        private int Offset;

        int IVerticalScrollable.VerticalExtent => Rows.Count;
        int IVerticalScrollable.VerticalViewport => Math.Max(1, Height / ROW_HEIGHT);

        int IVerticalScrollable.VerticalOffset
        {
            get => Offset;
            set => Offset = Math.Clamp(value, 0, Math.Max(0, Rows.Count - (Height / ROW_HEIGHT)));
        }

        public void SetRows(IReadOnlyList<LearnAbilityRow> rows)
        {
            Rows = rows;
            Offset = 0;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            //primes ClipRect for the clipped draws below
            base.Draw(spriteBatch);

            var visible = (Height + ROW_HEIGHT - 1) / ROW_HEIGHT;

            for (var i = 0; i < visible; i++)
            {
                var index = Offset + i;

                if (index >= Rows.Count)
                    break;

                var row = Rows[index];
                var top = ScreenY + (i * ROW_HEIGHT);
                var textY = top + ((ROW_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);

                if (index % 2 == 1)
                    DrawRectClipped(spriteBatch, new Rectangle(ScreenX, top, Width, ROW_HEIGHT), Stripe);

                var textColor = row.Met ? TextColors.Default : LegendColors.Silver;
                DrawTextClipped(spriteBatch, new Vector2(ScreenX + 4, textY), row.Label, textColor, false);

                var valueRight = ScreenX + Width - MARK_COLUMN - 8;
                DrawTextClipped(spriteBatch, new Vector2(valueRight - TextRenderer.MeasureWidth(row.Value), textY), row.Value, textColor, false);

                var markX = ScreenX + Width - MARK_COLUMN + ((MARK_COLUMN - MARK_SIZE) / 2) - 2;
                var markY = top + ((ROW_HEIGHT - MARK_SIZE) / 2);

                if (row.Met)
                    DrawTick(spriteBatch, markX, markY);
                else
                    DrawCross(spriteBatch, markX, markY);
            }
        }

        //the bitmap font has no tick or cross glyph, so both are drawn as 2px pixel strokes
        private void DrawTick(SpriteBatch spriteBatch, int x, int y)
        {
            var color = LegendColors.Lime;

            //short arm down-right, then the long arm up-right
            for (var i = 0; i < 4; i++)
                DrawRectClipped(spriteBatch, new Rectangle(x + i, y + 4 + i, 2, 2), color);

            for (var i = 0; i < 6; i++)
                DrawRectClipped(spriteBatch, new Rectangle(x + 3 + i, y + 7 - i, 2, 2), color);
        }

        private void DrawCross(SpriteBatch spriteBatch, int x, int y)
        {
            var color = LegendColors.Red;

            for (var i = 0; i < MARK_SIZE - 1; i++)
            {
                DrawRectClipped(spriteBatch, new Rectangle(x + i, y + i, 2, 2), color);
                DrawRectClipped(spriteBatch, new Rectangle(x + MARK_SIZE - 2 - i, y + i, 2, 2), color);
            }
        }
    }
}
