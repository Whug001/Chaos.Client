using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.Crafting;

/// <summary>One recipe in the browser list. Passive: the list handles clicks.</summary>
public sealed class CraftingRecipeRow : UIPanel
{
    public const int HEIGHT = 34;
    private static readonly Color SelectedBackground = new(156, 96, 12, 180);

    private readonly UIImage IconImage;
    private readonly UILabel NameLabel;

    public CraftingRecipeRow(int width)
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
            X = 38,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = width - 40,
            Height = TextRenderer.CHAR_HEIGHT,
            IsHitTestVisible = false
        };

        AddChild(IconImage);
        AddChild(NameLabel);
    }

    public string Text => NameLabel.Text;

    public void Bind(CraftingRecipeInfo recipe, bool selected)
    {
        if (UiRenderer.Instance is { } ui)
        {
            var icon = ui.GetItemIcon(recipe.Sprite, recipe.Color);

            IconImage.Texture = recipe.Makeable
                ? icon
                : ui.GetCooldownTintedTexture($"item:{recipe.Sprite}:{recipe.Color}", icon, LegendColors.DimGray);
        }

        NameLabel.Text = recipe.Name;
        NameLabel.ForegroundColor = recipe.Makeable ? LegendColors.White : LegendColors.DimGray;
        BackgroundColor = selected ? SelectedBackground : null;
    }

    public override void Dispose()
    {
        //shared cached texture
        IconImage.Texture = null;
        base.Dispose();
    }
}
