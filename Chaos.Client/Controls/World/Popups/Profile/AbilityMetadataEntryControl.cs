#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Data.Models;
using Chaos.Client.Extensions;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Profile;

/// <summary>
///     A single skill/spell row in the ability metadata tab. Uses the _nui_ski prefab template (211x43): 32x32 icon, name,
///     level text. The entire row is clickable to show details.
/// </summary>
public sealed class AbilityMetadataEntryControl : PrefabPanel
{
    private readonly UILabel? LevelLabel;
    private readonly UILabel? NameLabel;
    private readonly UIImage? TileImage;

    private Texture2D? IconTexture;
    public AbilityMetadataEntry? Entry { get; private set; }

    public AbilityMetadataEntryControl()
        : base("_nui_ski", false)
    {
        Height += 2;
        TileImage = CreateImage("TILE");
        NameLabel = CreateLabel("NAME");
        NameLabel?.ForegroundColor = LegendColors.White;
        LevelLabel = CreateLabel("LEVEL");
        LevelLabel?.ForegroundColor = LegendColors.White;
    }

    public void Clear()
    {
        Entry = null;
        IconTexture = null;

        TileImage?.Texture = null;

        NameLabel?.Text = string.Empty;

        LevelLabel?.Text = string.Empty;

        Visible = false;
    }

    /// <summary>
    ///     Fired when the row is clicked. Passes the bound entry.
    /// </summary>
    public event AbilityMetadataClickedHandler? OnClicked;

    public override void OnClick(ClickEvent e)
    {
        if (Entry is not null)
        {
            OnClicked?.Invoke(Entry);
            e.Handled = true;
        }
    }

    public void SetEntry(AbilityMetadataEntry entry, AbilityIconState iconState)
    {
        Entry = entry;

        NameLabel?.Text = entry.Name;

        if (LevelLabel is not null)
        {
            LevelLabel.Text = entry.RequiresMaster
                ? "master"
                : entry.AbilityLevel > 0
                    ? $"ability {entry.AbilityLevel}"
                    : $"level {entry.Level}";
            LevelLabel.ForegroundColor = LegendColors.White;
        }

        var newIcon = iconState.ResolveIcon(entry);

        if (newIcon != IconTexture)
        {
            IconTexture = newIcon;

            TileImage?.Texture = newIcon;
        }

        Visible = true;
    }

}