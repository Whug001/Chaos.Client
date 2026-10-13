using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Controls.World.Popups.Crafting;

/// <summary>One ingredient in the browser: icon, name and "have / need" on the right, red when short.</summary>
public sealed class CraftingIngredientRow : UIPanel
{
    public const int HEIGHT = 34;
    private const int COUNT_WIDTH = 42;
    private const int NAME_GAP = 2;
    private const int NAME_X = 38;

    private readonly UILabel CountLabel;
    private readonly UIImage IconImage;
    private readonly UILabel NameLabel;

    public CraftingIngredientRow(int width)
    {
        Width = width;
        Height = HEIGHT;

        IconImage = new UIImage
        {
            X = 1,
            Y = 1,
            Width = 32,
            Height = 32,
            IsHitTestVisible = false
        };

        NameLabel = new UILabel
        {
            X = NAME_X,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = Math.Max(0, width - NAME_X - NAME_GAP - COUNT_WIDTH),
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        CountLabel = new UILabel
        {
            X = width - COUNT_WIDTH,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = COUNT_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ColorCodesEnabled = true,
            IsHitTestVisible = false
        };

        AddChild(IconImage);
        AddChild(NameLabel);
        AddChild(CountLabel);
    }

    public string CountText { get; private set; } = string.Empty;

    public void Bind(CraftingIngredientInfo ingredient, int quantity)
    {
        var need = ingredient.Need * Math.Max(quantity, 1);
        NameLabel.Text = ingredient.Name;
        CountText = (ingredient.Have >= need ? "{=u" : "{=b") + $"{ingredient.Have} / {need}";

        //the count takes only the room its text needs (plus the label's padding and the last glyph's overhang),
        //so the name gets the rest of the row
        var countWidth = Math.Min(TextRenderer.MeasureWidth(CountText) + 4, Math.Max(0, Width - NAME_X));
        CountLabel.X = Width - countWidth;
        CountLabel.Width = countWidth;
        NameLabel.Width = Math.Max(0, CountLabel.X - NAME_GAP - NAME_X);

        //color-code measuring needs the legend palette, which only exists once the client has loaded its data
        if (UiRenderer.Instance is not { } ui)
            return;

        IconImage.Texture = ui.GetItemIcon(ingredient.Sprite, ingredient.Color);
        CountLabel.Text = CountText;
    }

    public override void Dispose()
    {
        //shared cached texture
        IconImage.Texture = null;
        base.Dispose();
    }
}
