#region
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     Inventory item tooltip: the item name over its durability. Follows the cursor when visible.
/// </summary>
public sealed class ItemTooltipControl : TooltipPanelBase
{
    private const int MAX_CONTENT_WIDTH = 25 * TextRenderer.CHAR_WIDTH;

    private static readonly Color DurabilityColor = new(100, 149, 237);

    public ItemTooltipControl()
        : base("ItemTooltip", MAX_CONTENT_WIDTH, DurabilityColor) { }

    public void Show(
        string itemName,
        int currentDurability,
        int maxDurability,
        int mouseX,
        int mouseY)
    {
        HeadingLabel.Text = itemName;

        var hasDurability = maxDurability > 0;
        BodyLabel.Text = hasDurability ? $"{currentDurability}/{maxDurability}" : string.Empty;
        BodyLabel.Visible = hasDurability;

        Layout();
        UpdatePosition(mouseX, mouseY);

        Visible = true;
    }
}
