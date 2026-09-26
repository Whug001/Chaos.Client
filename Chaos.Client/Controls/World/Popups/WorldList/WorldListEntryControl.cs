#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.WorldList;

/// <summary>
///     A single row in the world list panel: title + name + social status icon, which sits in the inner icon
///     cell (the emblem cell to its right stays empty until the emblem system fills it).
/// </summary>
public sealed class WorldListEntryControl : UIPanel
{
    //columns match the grid cells painted in _nusers.spf; x is relative to the list's left edge (x = 15 in the drawer)
    private const int ROW_HEIGHT = 15;
    private const int TEXT_HEIGHT = 12;
    private const int TEXT_Y = -1;
    private const int TITLE_WIDTH = 168;
    private const int NAME_X = 178;
    private const int NAME_WIDTH = 94;
    private const int STATUS_X = 278;
    private const int ICON_SIZE = 11;

    private readonly UIImage Icon;
    private readonly UILabel NameLabel;
    private readonly UILabel TitleLabel;

    public WorldListEntryControl(int rowWidth)
    {
        Width = rowWidth;
        Height = ROW_HEIGHT;

        TitleLabel = new UILabel
        {
            Name = "Title",
            X = 0,
            Y = TEXT_Y,
            Width = TITLE_WIDTH,
            Height = TEXT_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            PaddingLeft = 0
        };

        AddChild(TitleLabel);

        NameLabel = new UILabel
        {
            Name = "Name",
            X = NAME_X,
            Y = TEXT_Y,
            Width = NAME_WIDTH,
            Height = TEXT_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            PaddingLeft = 0
        };

        AddChild(NameLabel);

        //the emblem cell (x = 293) stays empty until the emblem system fills it
        Icon = new UIImage
        {
            Name = "StatusIcon",
            X = STATUS_X,
            Y = 0,
            Width = ICON_SIZE,
            Height = ICON_SIZE
        };

        AddChild(Icon);
    }

    public void Clear()
    {
        TitleLabel.Text = string.Empty;
        NameLabel.Text = string.Empty;
        Icon.Texture = null;
        Visible = false;
    }

    public string? PlayerName { get; private set; }

    public event WhisperRequestedHandler? OnWhisper;

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        if ((e.Button == MouseButton.Left) && PlayerName is not null)
        {
            OnWhisper?.Invoke(PlayerName);
            e.Handled = true;
        }
    }

    public void SetEntry(WorldListEntry entry, Texture2D? statusIcon, Color nameColor)
    {
        TitleLabel.Text = entry.Title ?? string.Empty;
        NameLabel.ForegroundColor = nameColor;
        NameLabel.Text = entry.Name;
        PlayerName = entry.Name;
        Icon.Texture = statusIcon;
        Visible = true;
    }
}