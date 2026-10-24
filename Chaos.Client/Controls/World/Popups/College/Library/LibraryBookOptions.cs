#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel.College.Library;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College.Library;

public sealed class LibraryBookOptions : UIPanel
{
    private const int SHELF_WIDTH = 70;
    private const int GAP = 6;
    private const int SWATCH = 14;
    private const int SWATCH_GAP = 4;
    private const int COVER_WIDTH = 60;
    private const int SPINE_COUNT = 8;

    private static readonly LibraryShelfCode[] ShelfCodes =
    [
        LibraryShelfCode.Guides,
        LibraryShelfCode.Lore,
        LibraryShelfCode.ShortStories,
        LibraryShelfCode.PoemsAndSongs
    ];

    private static readonly string[] ShelfCaptions = ["Guides", "Lore", "Stories", "Poems"];

    private readonly CustomButton CoverButton;
    private readonly CustomButton[] ShelfButtons = new CustomButton[ShelfCodes.Length];

    private string CoverHashValue = string.Empty;
    private int SwatchLeft;

    public LibraryBookOptions()
    {
        Width = 528;
        Height = 24;

        var y = (Height - CustomButton.HEIGHT) / 2;

        for (var i = 0; i < ShelfCodes.Length; i++)
        {
            var code = ShelfCodes[i];
            var button = new CustomButton(ShelfCaptions[i], SHELF_WIDTH)
            {
                X = i * (SHELF_WIDTH + GAP),
                Y = y
            };

            button.Clicked += () =>
            {
                if (Shelf == code)
                    return;

                Shelf = code;
                RefreshShelf();
                Changed?.Invoke();
            };

            ShelfButtons[i] = button;
            AddChild(button);
        }

        CoverButton = new CustomButton("Cover", COVER_WIDTH)
        {
            X = Width - COVER_WIDTH,
            Y = y
        };

        CoverButton.Clicked += () => CoverRequested?.Invoke();
        AddChild(CoverButton);
        RefreshShelf();
    }

    public event Action? Changed;
    public event Action? CoverRequested;

    public LibraryShelfCode Shelf { get; private set; }
    public byte Spine { get; private set; }
    public bool FixedShelf { get; private set; }

    public string CoverHash
    {
        get => CoverHashValue;
        set
        {
            CoverHashValue = value;
            CoverButton.Caption = value.Length > 0 ? "Cover *" : "Cover";
        }
    }

    public void Load(LibraryShelfCode shelf, byte spine, string coverHash, bool fixedShelf)
    {
        FixedShelf = fixedShelf;
        Shelf = shelf;
        Spine = spine;
        CoverHash = coverHash;

        foreach (var button in ShelfButtons)
            button.Visible = !fixedShelf;

        SwatchLeft = fixedShelf ? 0 : (ShelfCodes.Length * SHELF_WIDTH) + ((ShelfCodes.Length - 1) * GAP) + 8;
        RefreshShelf();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        var top = ScreenY + ((Height - SWATCH) / 2);

        for (var i = 0; i < SPINE_COUNT; i++)
        {
            var bounds = new Rectangle(ScreenX + SwatchLeft + (i * (SWATCH + SWATCH_GAP)), top, SWATCH, SWATCH);
            DrawRectClipped(spriteBatch, bounds, LibraryLabels.Spines[i]);
            DrawBorder(spriteBatch, bounds, i == Spine ? LegendColors.Gold : Color.Black);
        }
    }

    public override void OnClick(ClickEvent e)
    {
        if ((e.Button != MouseButton.Left) || !Visible)
            return;

        var x = e.ScreenX - ScreenX - SwatchLeft;
        var y = e.ScreenY - ScreenY - ((Height - SWATCH) / 2);

        if ((x < 0) || (y < 0) || (y >= SWATCH))
            return;

        var index = x / (SWATCH + SWATCH_GAP);

        if ((index >= SPINE_COUNT) || ((x % (SWATCH + SWATCH_GAP)) >= SWATCH))
            return;

        e.Handled = true;

        if (Spine == (byte)index)
            return;

        Spine = (byte)index;
        Changed?.Invoke();
    }

    private void RefreshShelf()
    {
        for (var i = 0; i < ShelfButtons.Length; i++)
            ShelfButtons[i].Selected = ShelfCodes[i] == Shelf;
    }
}
