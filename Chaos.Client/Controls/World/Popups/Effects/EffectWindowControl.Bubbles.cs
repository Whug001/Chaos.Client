#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.Controls.World.ViewPort;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Effects;

/// <summary>The Chat bubble tab: preset tiles, typed colors for Custom, and a normal and shout preview.</summary>
public sealed partial class EffectWindowControl
{
    private const int HEX_ROW_TOP = 72;
    private const int HEX_ROW_HEIGHT = 18;
    private const int HEX_LABEL_WIDTH = 46;
    private const int HEX_BOX_WIDTH = 64;
    private const int STEP_WIDTH = 22;

    private static readonly BubbleColorField[] Fields = [BubbleColorField.Border, BubbleColorField.Fill, BubbleColorField.Text];

    private readonly ChatBubblePickerState Bubbles = new();
    private readonly Dictionary<byte, Texture2D> BubbleThumbs = [];
    private readonly List<UIElement> BubbleOnly = [];
    private readonly UITextBox[] HexBoxes = new UITextBox[3];
    private readonly string[] SeenHex = new string[3];
    private ThumbnailGrid<ChatBubbleEntry> BubbleGrid = null!;
    private UILabel BubblePageLabel = null!;
    private UILabel AlphaLabel = null!;
    private UILabel WarningLabel = null!;
    private CustomButton BubbleTab = null!;
    private ChatBubble? PreviewNormal;
    private ChatBubble? PreviewShout;
    private ChatBubbleLook? ShownLook;
    private int BubblePreviewLeft;

    private void BuildBubbleControls(int previewLeft)
    {
        BubblePreviewLeft = previewLeft;

        BubbleGrid = new ThumbnailGrid<ChatBubbleEntry>(EffectWindowState.COLUMNS, EffectWindowState.ROWS, cellSize: CELL) { X = LEFT, Y = TOP };
        BubbleGrid.Selected += e => { Bubbles.Select(e); Refresh(); };
        BubbleGrid.PageStepped += d => { Bubbles.StepPage(d); Refresh(); };
        AddChild(BubbleGrid);
        BubbleOnly.Add(BubbleGrid);

        var gridHeight = ThumbnailGrid<ChatBubbleEntry>.HeightFor(EffectWindowState.ROWS, cellSize: CELL);
        var gridWidth = ThumbnailGrid<ChatBubbleEntry>.WidthFor(EffectWindowState.COLUMNS, cellSize: CELL);
        BubblePageLabel = Caption(string.Empty, LEFT, TOP + gridHeight + 4, gridWidth, HorizontalAlignment.Center, LegendColors.Gray);
        BubbleOnly.Add(BubblePageLabel);

        for (var i = 0; i < Fields.Length; i++)
        {
            var y = TOP + HEX_ROW_TOP + (i * HEX_ROW_HEIGHT);
            BubbleOnly.Add(Caption(Fields[i].ToString(), previewLeft, y + 2, HEX_LABEL_WIDTH));

            HexBoxes[i] = new UITextBox
            {
                X = previewLeft + HEX_LABEL_WIDTH,
                Y = y,
                Width = HEX_BOX_WIDTH,
                Height = 14,
                MaxLength = 7,
                IsTabStop = true
            };
            AddChild(HexBoxes[i]);
            BubbleOnly.Add(HexBoxes[i]);
        }

        var alphaY = TOP + HEX_ROW_TOP + (3 * HEX_ROW_HEIGHT);
        BubbleOnly.Add(Caption("Opacity", previewLeft, alphaY + 2, HEX_LABEL_WIDTH));
        BubbleOnly.Add(AddButton("-", STEP_WIDTH, previewLeft + HEX_LABEL_WIDTH, alphaY, () => { Bubbles.StepAlpha(-ChatBubblePickerState.ALPHA_STEP); Refresh(); }));
        AlphaLabel = Caption(string.Empty, previewLeft + HEX_LABEL_WIDTH + STEP_WIDTH, alphaY + 2, 30, HorizontalAlignment.Center);
        BubbleOnly.Add(AlphaLabel);
        BubbleOnly.Add(AddButton("+", STEP_WIDTH, previewLeft + HEX_LABEL_WIDTH + STEP_WIDTH + 30, alphaY, () => { Bubbles.StepAlpha(ChatBubblePickerState.ALPHA_STEP); Refresh(); }));

        WarningLabel = Caption(string.Empty, previewLeft, TOP + PREVIEW_SIZE + 6, PREVIEW_SIZE, HorizontalAlignment.Left, LegendColors.Red);
        BubbleOnly.Add(WarningLabel);

        SetBubbleControlsVisible(false);
    }

    private void OpenBubbles(EffectWindowDisplayArgs args)
    {
        Bubbles.Open(args);
        SyncHexBoxes();
    }

    private void UpdateBubbles(EffectWindowDisplayArgs args)
    {
        Bubbles.Update(args);
        SyncHexBoxes();
    }

    private void SyncHexBoxes()
    {
        for (var i = 0; i < Fields.Length; i++)
        {
            HexBoxes[i].Text = Bubbles.HexOf(Fields[i]);
            SeenHex[i] = HexBoxes[i].Text;
        }
    }

    //UITextBox raises no change event, so typing is picked up here once a frame
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible || (State.Tab != EffectWindowKind.ChatBubble) || !Bubbles.ShowsHexBoxes)
            return;

        var changed = false;

        for (var i = 0; i < Fields.Length; i++)
            if (HexBoxes[i].Text != SeenHex[i])
            {
                SeenHex[i] = HexBoxes[i].Text;
                Bubbles.SetHex(Fields[i], HexBoxes[i].Text);
                changed = true;
            }

        if (changed)
            Refresh();
    }

    private void RefreshBubbles()
    {
        SetBubbleControlsVisible(true);

        BubbleGrid.SetItems(Bubbles.PageItems, Bubbles.Selected, Bubbles.PageCount, BubbleThumbFor, isDimmed: e => !e.Owned, isMarked: Bubbles.IsCurrent);
        BubblePageLabel.Text = $"{Bubbles.Page + 1}/{Bubbles.PageCount}";

        var boxes = Bubbles.ShowsHexBoxes;

        foreach (var box in HexBoxes)
            box.Visible = boxes;

        foreach (var element in BubbleOnly.Where(e => e is not ThumbnailGrid<ChatBubbleEntry> && (e != BubblePageLabel) && (e != WarningLabel) && e is not UITextBox))
            element.Visible = boxes;

        AlphaLabel.Text = Bubbles.FillAlpha.ToString();
        WarningLabel.Text = Bubbles.Warning ?? string.Empty;
        StatusLabel.Text = Bubbles.IsCustomSelected && !Bubbles.LicenseOwned
            ? $"{Bubbles.LicensePrice:N0} gold - {EffectWindowState.SOLD_BY}"
            : Bubbles.StatusText;
        ChooseButton.Enabled = Bubbles.CanChoose;

        ShowPreview(Bubbles.PreviewLook);
    }

    private void ShowPreview(ChatBubbleLook look)
    {
        if (Equals(look, ShownLook) && PreviewNormal is not null)
            return;

        ReleasePreview();
        ShownLook = look;

        PreviewNormal = ChatBubble.Create(0, "You: Hello there!", false, "BubblePreviewNormal", look);
        PreviewNormal.X = BubblePreviewLeft;
        PreviewNormal.Y = TOP;
        AddChild(PreviewNormal);

        PreviewShout = ChatBubble.Create(0, "You! Hello there!", true, "BubblePreviewShout", look);
        PreviewShout.X = BubblePreviewLeft;
        PreviewShout.Y = TOP + PreviewNormal.Height + 4;
        AddChild(PreviewShout);
    }

    private Texture2D? BubbleThumbFor(ChatBubbleEntry entry)
    {
        //the custom tile follows the typed colors, so it is never cached
        if (entry.Key == ChatBubbleLook.CUSTOM_STYLE)
        {
            if (BubbleThumbs.Remove(entry.Key, out var old))
                old.Dispose();
        } else if (BubbleThumbs.TryGetValue(entry.Key, out var cached))
            return cached;

        var texture = ChatBubble.CreateSwatch(entry.Look);
        BubbleThumbs[entry.Key] = texture;

        return texture;
    }

    private void SetBubbleControlsVisible(bool visible)
    {
        foreach (var element in BubbleOnly)
            element.Visible = visible;

        if (PreviewNormal is not null)
            PreviewNormal.Visible = visible;

        if (PreviewShout is not null)
            PreviewShout.Visible = visible;
    }

    private void ReleasePreview()
    {
        foreach (var bubble in new[] { PreviewNormal, PreviewShout })
            if (bubble is not null)
            {
                RemoveChild(bubble.Name);
            }

        PreviewNormal = null;
        PreviewShout = null;
        ShownLook = null;
    }

    private void ReleaseBubbles()
    {
        ReleasePreview();

        foreach (var texture in BubbleThumbs.Values)
            texture.Dispose();

        BubbleThumbs.Clear();
    }
}
