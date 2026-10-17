#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>
///     The pack reveal: the cards slide out face down after a short tear, glow by rarity and foil, and flip when clicked.
///     The cards are already in the binder when this opens; closing it changes nothing on the server. The timeline is
///     <see cref="TcgRevealState" />; this control only places and draws the faces.
/// </summary>
public sealed class TcgPackRevealControl : FramedDialogPanelBase
{
    public const int PANEL_HEIGHT = 460;
    public const int PANEL_WIDTH = 560;
    public const int PANEL_X = 40;
    public const int PANEL_Y = 6;

    private const int BUTTON_TOP = 386;
    private const int BUTTON_WIDTH = 90;
    private const int CARD_LEFT = 22;
    private const int CARD_STEP_X = 132;
    private const int CARD_STEP_Y = 180;
    private const int CARD_TOP = 30;
    private const int COLUMNS = 4;
    private const int MAX_CARDS = 8;
    private const int TITLE_TOP = 8;

    private readonly CustomButton DoneButton;
    private readonly TcgCardFace[] Faces = new TcgCardFace[MAX_CARDS];
    private readonly TcgRevealState Reveal = new();
    private readonly CustomButton RevealAllButton;

    public TcgPackRevealControl(CreatureRenderer creatures)
        : base("_nsett", false)
    {
        Name = "TcgPackReveal";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        X = PANEL_X;
        Y = PANEL_Y;

        AddChild(
            new UILabel
            {
                X = 0,
                Y = TITLE_TOP,
                Width = PANEL_WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                Text = "Card Pack",
                ForegroundColor = LegendColors.Gold,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            });

        for (var i = 0; i < Faces.Length; i++)
        {
            Faces[i] = new TcgCardFace(creatures, 2)
            {
                Visible = false,
                BadgeColor = LegendColors.Gold
            };

            Faces[i].Clicked += OnFaceClicked;
            AddChild(Faces[i]);
        }

        RevealAllButton = new CustomButton("Reveal all", BUTTON_WIDTH)
        {
            X = CARD_LEFT,
            Y = BUTTON_TOP
        };

        RevealAllButton.Clicked += () => Reveal.RevealAll();
        AddChild(RevealAllButton);

        DoneButton = new CustomButton("Done", BUTTON_WIDTH)
        {
            X = PANEL_WIDTH - CARD_LEFT - BUTTON_WIDTH,
            Y = BUTTON_TOP,
            Visible = false
        };

        DoneButton.Clicked += Hide;
        AddChild(DoneButton);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible || (Reveal.TearProgress >= 1) || (ClipRect.Width <= 0))
            return;

        //the pack tearing open: a white flash that grows and fades before the cards come out
        var progress = (float)Reveal.TearProgress;
        var size = (int)(40 + (100 * progress));
        var flash = new Rectangle(ScreenX + ((Width - size) / 2), ScreenY + ((Height - size) / 2), size, size);
        DrawRectClipped(spriteBatch, flash, Color.White * (1f - progress));
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();
        Reveal.Clear();

        foreach (var face in Faces)
            face.Visible = false;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode is Keycode.Escape or Keycode.Enter)
        {
            if (Reveal.AllFaceUp)
                Hide();
            else
                Reveal.RevealAll();

            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>The keyword under the cursor on a revealed card, for the hover box. Null when nothing qualifies.</summary>
    public string? HoveredKeyword(int mouseX, int mouseY)
    {
        if (!Visible)
            return null;

        foreach (var face in Faces)
            if (face.KeywordAt(mouseX, mouseY) is { } keyword)
                return keyword;

        return null;
    }

    /// <summary>Plays a pack, replacing any reveal still open.</summary>
    public void Start(TcgCollectionDisplayArgs args)
    {
        Reveal.Start(args);

        for (var i = 0; i < Faces.Length; i++)
        {
            var face = Faces[i];
            face.Visible = false;

            if (i >= Reveal.Cards.Count)
                continue;

            face.Card = Reveal.View(i);
            face.FaceDown = false;
            face.Owned = true;
            face.FlipProgress = 0f;
            face.Badge = string.Empty;
        }

        if (!Visible)
            Show();

        Layout();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible || !Reveal.IsActive)
            return;

        Reveal.Update(gameTime.ElapsedGameTime.TotalMilliseconds);
        Layout();
    }

    private void Layout()
    {
        var startX = (PANEL_WIDTH - (TcgCardFace.WIDTH * 2)) / 2;
        var startY = (PANEL_HEIGHT - (TcgCardFace.HEIGHT * 2)) / 2;

        for (var i = 0; i < Faces.Length; i++)
        {
            var face = Faces[i];

            if (i >= Reveal.Cards.Count)
            {
                face.Visible = false;

                continue;
            }

            var slide = Reveal.SlideProgress(i);
            var eased = 1 - Math.Pow(1 - slide, 3);
            var slotX = CARD_LEFT + ((i % COLUMNS) * CARD_STEP_X);
            var slotY = CARD_TOP + ((i / COLUMNS) * CARD_STEP_Y);

            face.Visible = slide > 0;
            face.X = (int)Math.Round(startX + ((slotX - startX) * eased));
            face.Y = (int)Math.Round(startY + ((slotY - startY) * eased));
            face.FlipProgress = (float)Reveal.FlipProgress(i);
            face.Glow = Reveal.Glow(i);
            face.Foil = Reveal.Cards[i].Foil && Reveal.IsFaceUp(i);
            face.Badge = Reveal.ShowsNew(i) ? "New!" : string.Empty;
        }

        RevealAllButton.Visible = !Reveal.AllFaceUp;
        DoneButton.Visible = Reveal.AllFaceUp;
    }

    private void OnFaceClicked(TcgCardFace face)
    {
        var index = Array.IndexOf(Faces, face);

        if (index >= 0)
            Reveal.TryFlip(index);
    }
}
