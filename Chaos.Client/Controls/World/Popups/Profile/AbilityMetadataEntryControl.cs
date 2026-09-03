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
    /// <summary>
    ///     The right edge the prefab's NAME label runs to, used as a fallback when NAME is absent.
    /// </summary>
    private const int PREFAB_ROW_RIGHT_EDGE = 204;

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

        if (LevelLabel is not null)
        {
            //the prefab gives LEVEL a 60px box (x48 to x108), which was sized for "level 99". "ability 10" and
            //anything longer overflowed it and came back ellipsised as "abilit...". The rest of that second line
            //is free -- the prefab's E_BTN slot at x127 is never created here -- so run the label out to the same
            //right edge NAME uses. That is 156px, which holds "ability 999" with room to spare.
            var nameRight = NameLabel is not null ? NameLabel.X + NameLabel.Width : PREFAB_ROW_RIGHT_EDGE;

            LevelLabel.Width = Math.Max(LevelLabel.Width, nameRight - LevelLabel.X);
        }
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
            //an advanced-class ability carries both the master flag and an ability level. The ability level is the one
            //that tells the player where they actually get it, so it wins the label.
            LevelLabel.Text = entry.AbilityLevel > 0
                ? $"ability {entry.AbilityLevel}"
                : entry.RequiresMaster
                    ? "master"
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