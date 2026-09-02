#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     Hold-to-select radial emote wheel with six configurable slots and an in-wheel emote catalog.
///     Opened by holding middle mouse + E (driven from <see cref="Screens.WorldScreen" />); catalog mode opens on
///     right-clicking a segment or the center gear. In the catalog, use the slot picker (or keys 1–6) to choose
///     which wheel slot you are assigning.
/// </summary>
public sealed class EmoteWheelControl : UIPanel
{
    private const float OUTER_RADIUS = 97f;
    private const float INNER_RADIUS = 14f;
    private const float HIT_INNER_RADIUS = 6f;
    private const float HIT_OUTER_RADIUS = OUTER_RADIUS + 8f;
    private const float ICON_RADIUS = 61f;
    private const int ICON_SIZE = 34;
    private const int CATALOG_ICON_SIZE = 32;
    private const int PANEL_SIZE = 260;
    private const int CATALOG_WIDTH = 380;
    private const int CATALOG_HEIGHT = 328;
    private const int SLOT_PICKER_HEIGHT = 48;
    private const int SLOT_PICKER_ICON = 26;
    private const int CATALOG_COLUMNS = 4;
    private const int CATALOG_CELL_WIDTH = 88;
    private const int CATALOG_CELL_HEIGHT = 52;
    private const int CATALOG_PAD = 6;
    private const int CATALOG_SCROLL_STEP = CATALOG_CELL_HEIGHT;

    private readonly bool[] _slotHasEmote = new bool[EmoteCatalog.SLOT_COUNT];
    private readonly UILabel[] _slotPlaceholders = new UILabel[EmoteCatalog.SLOT_COUNT];
    private readonly BodyAnimation?[] _slots = new BodyAnimation?[EmoteCatalog.SLOT_COUNT];
    private readonly Texture2D _wheelBackground;
    private readonly Texture2D[] _wedgeHighlights = new Texture2D[EmoteCatalog.SLOT_COUNT];
    private readonly CustomButton _gearButton;
    private readonly UIPanel _catalogPanel;
    private readonly UIPanel _slotPickerRow;
    private readonly SlotPickerButton[] _slotPickerButtons = new SlotPickerButton[EmoteCatalog.SLOT_COUNT];
    private readonly UIPanel _catalogGrid;
    private readonly CatalogScrollHost _catalogScrollHost;
    private readonly ScrollViewerControl _catalogScroll;

    private int _centerX;
    private int _centerY;
    private int _hoveredSegment = -1;
    private int _editingSlot;
    private int _bodyColor = (int)BodyColor.White;
    private WheelMode _mode = WheelMode.Wheel;

    public EmoteWheelControl(GraphicsDevice device)
    {
        _ = device;
        Name = "EmoteWheel";
        Visible = false;
        UsesControlStack = true;
        ZIndex = 2;
        Width = PANEL_SIZE;
        Height = PANEL_SIZE;

        _wheelBackground = BuildWheelBackground(PANEL_SIZE);

        for (var i = 0; i < EmoteCatalog.SLOT_COUNT; i++)
            _wedgeHighlights[i] = BuildWedgeHighlight(PANEL_SIZE, i);

        for (var i = 0; i < EmoteCatalog.SLOT_COUNT; i++)
        {
            _slotPlaceholders[i] = new UILabel
            {
                Name = $"SlotPlaceholder{i}",
                Width = ICON_SIZE,
                Height = ICON_SIZE,
                Text = "+",
                HorizontalAlignment = HorizontalAlignment.Center,
                ForegroundColor = LegendColors.LightGray,
                IsHitTestVisible = false,
                Visible = false
            };

            AddChild(_slotPlaceholders[i]);
        }

        _gearButton = new CustomButton("...", 24);
        _gearButton.Clicked += () => EnterCatalogMode(_editingSlot >= 0 ? _editingSlot : 0);
        AddChild(_gearButton);

        _catalogGrid = new UIPanel { Name = "CatalogGrid" };
        _catalogScrollHost = new CatalogScrollHost(_catalogGrid, CATALOG_SCROLL_STEP);
        _catalogScroll = new ScrollViewerControl(_catalogScrollHost)
        {
            Name = "CatalogScroll",
            Visible = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        _catalogPanel = new UIPanel
        {
            Name = "CatalogPanel",
            Visible = false,
            Width = CATALOG_WIDTH,
            Height = CATALOG_HEIGHT,
            ZIndex = 1
        };

        _slotPickerRow = new UIPanel { Name = "SlotPicker" };

        for (var i = 0; i < EmoteCatalog.SLOT_COUNT; i++)
        {
            var picker = new SlotPickerButton(this, i);
            _slotPickerButtons[i] = picker;
            _slotPickerRow.AddChild(picker);
        }

        _catalogPanel.AddChild(_slotPickerRow);
        _catalogPanel.AddChild(_catalogScroll);
        AddChild(_catalogPanel);

        BuildCatalogGrid();
        LoadSlots();
        LayoutWheelIcons();
        LayoutGearButton();
    }

    public bool IsOpen => Visible;

    public bool IsCatalogOpen => _mode == WheelMode.Catalog;

    public void LoadSlots()
    {
        for (var i = 0; i < EmoteCatalog.SLOT_COUNT; i++)
            _slots[i] = ClientSettings.EmoteWheelSlots[i];

        RefreshSlotIcons();
    }

    public void ShowAt(int screenX, int screenY, int bodyColor = (int)BodyColor.White)
    {
        _bodyColor = bodyColor;
        _centerX = screenX;
        _centerY = screenY;
        ApplyWheelLayout();
        _hoveredSegment = -1;
        _mode = WheelMode.Wheel;
        _catalogPanel.Visible = false;
        _catalogScroll.Visible = false;
        LayoutWheelIcons();
        LayoutGearButton();
        LayoutCatalogPanel();
        RefreshSlotIcons();
        Visible = true;
        IsHitTestVisible = true;
        InputDispatcher.Instance?.PushControl(this);
    }

    public void UpdateHover(int mouseX, int mouseY)
    {
        if (_mode != WheelMode.Wheel)
            return;

        var dx = mouseX - _centerX;
        var dy = mouseY - _centerY;
        var seg = EmoteWheelGeometry.GetSegmentIndex(dx, dy, HIT_INNER_RADIUS, HIT_OUTER_RADIUS);

        //keep the last wedge when the cursor drifts over the center gear or outside the ring
        if (seg >= 0)
            _hoveredSegment = seg;
    }

    public BodyAnimation? GetHighlightedEmote()
        => _hoveredSegment >= 0 ? _slots[_hoveredSegment] : null;

    public void Hide()
    {
        _mode = WheelMode.Wheel;
        _catalogPanel.Visible = false;
        _catalogScroll.Visible = false;
        ApplyWheelLayout();
        Visible = false;
        InputDispatcher.Instance?.RemoveControl(this);
    }

    public override void Dispose()
    {
        _wheelBackground.Dispose();

        foreach (var wedge in _wedgeHighlights)
            wedge.Dispose();

        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        if (_mode == WheelMode.Wheel)
        {
            var origin = new Vector2(PANEL_SIZE / 2f, PANEL_SIZE / 2f);
            var pos = new Vector2(ScreenX + origin.X, ScreenY + origin.Y);
            spriteBatch.Draw(_wheelBackground, pos, null, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);

            if (_hoveredSegment >= 0)
                spriteBatch.Draw(_wedgeHighlights[_hoveredSegment], pos, null, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
        }

        base.Draw(spriteBatch);

        if (_mode == WheelMode.Wheel)
            DrawWheelSlotIcons(spriteBatch);

        if (_mode == WheelMode.Wheel && _hoveredSegment >= 0)
        {
            var (ix, iy) = SegmentIconPosition(_hoveredSegment);
            var highlight = new Rectangle(ScreenX + ix - 2, ScreenY + iy - 2, ICON_SIZE + 4, ICON_SIZE + 4);
            UIElement.DrawBorder(spriteBatch, highlight, LegendColors.Gold);
        }
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (_mode == WheelMode.Catalog)
        {
            if (e.Button == MouseButton.Right)
                ExitCatalogMode();

            e.Handled = true;
            return;
        }

        if (e.Button != MouseButton.Right)
            return;

        var dx = e.ScreenX - _centerX;
        var dy = e.ScreenY - _centerY;
        var seg = EmoteWheelGeometry.GetSegmentIndex(dx, dy, HIT_INNER_RADIUS, HIT_OUTER_RADIUS);

        if (seg >= 0)
            EnterCatalogMode(seg);
        else if ((dx * dx) + (dy * dy) < INNER_RADIUS * INNER_RADIUS)
            EnterCatalogMode(_editingSlot >= 0 ? _editingSlot : 0);

        e.Handled = true;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (_mode == WheelMode.Catalog)
        {
            var slot = e.Keycode switch
            {
                Keycode.D1 => 0,
                Keycode.D2 => 1,
                Keycode.D3 => 2,
                Keycode.D4 => 3,
                Keycode.D5 => 4,
                Keycode.D6 => 5,
                _          => -1
            };

            if (slot >= 0)
            {
                SelectEditingSlot(slot);
                e.Handled = true;

                return;
            }
        }

        if (e.Keycode != Keycode.Escape)
            return;

        if (_mode == WheelMode.Catalog)
        {
            ExitCatalogMode();
            e.Handled = true;

            return;
        }

        Hide();
        e.Handled = true;
    }

    public override void OnClick(ClickEvent e) => e.Handled = true;

    private void EnterCatalogMode(int slotIndex)
    {
        _editingSlot = Math.Clamp(slotIndex, 0, EmoteCatalog.SLOT_COUNT - 1);
        _mode = WheelMode.Catalog;
        RefreshCatalogIcons();
        RefreshSlotPicker();
        ApplyCatalogLayout();
        _catalogPanel.Visible = true;
        _catalogScroll.Visible = true;
        _catalogScrollHost.ResetScroll();
    }

    private void ExitCatalogMode()
    {
        _mode = WheelMode.Wheel;
        _catalogPanel.Visible = false;
        _catalogScroll.Visible = false;
        ApplyWheelLayout();
    }

    private void SaveSlot(int index, BodyAnimation animation)
    {
        _slots[index] = animation;
        ClientSettings.EmoteWheelSlots[index] = animation;
        ClientSettings.Save();
        RefreshSlotIcons();
        RefreshSlotPicker();
    }

    private void SelectEditingSlot(int index)
    {
        _editingSlot = Math.Clamp(index, 0, EmoteCatalog.SLOT_COUNT - 1);
        RefreshSlotPicker();
    }

    private void RefreshSlotPicker()
    {
        foreach (var picker in _slotPickerButtons)
            picker.Refresh(_bodyColor, _editingSlot, _slots);
    }

    private void BuildCatalogGrid()
    {
        var rowCount = (EmoteCatalog.All.Count + CATALOG_COLUMNS - 1) / CATALOG_COLUMNS;
        var contentHeight = (CATALOG_PAD * 2) + (rowCount * CATALOG_CELL_HEIGHT);
        _catalogGrid.Width = CATALOG_COLUMNS * CATALOG_CELL_WIDTH;
        _catalogGrid.Height = contentHeight;

        for (var i = 0; i < EmoteCatalog.All.Count; i++)
        {
            var entry = EmoteCatalog.All[i];
            var col = i % CATALOG_COLUMNS;
            var row = i / CATALOG_COLUMNS;

            var cell = new CatalogCell(entry, anim => SaveSlot(_editingSlot, anim))
            {
                X = CATALOG_PAD + col * CATALOG_CELL_WIDTH,
                Y = CATALOG_PAD + row * CATALOG_CELL_HEIGHT,
                Width = CATALOG_CELL_WIDTH - 2,
                Height = CATALOG_CELL_HEIGHT - 2
            };

            _catalogGrid.AddChild(cell);
        }
    }

    private void RefreshCatalogIcons()
    {
        foreach (var child in _catalogGrid.Children)
        {
            if (child is CatalogCell cell)
                cell.RefreshIcon(_bodyColor);
        }
    }

    private void RefreshSlotIcons()
    {
        for (var i = 0; i < EmoteCatalog.SLOT_COUNT; i++)
        {
            if (_slots[i] is { } anim && EmoteCatalog.TryGet(anim, out _))
            {
                _slotHasEmote[i] = true;
                _slotPlaceholders[i].Visible = false;
            } else
            {
                _slotHasEmote[i] = false;
                _slotPlaceholders[i].Visible = true;
            }
        }
    }

    private void DrawWheelSlotIcons(SpriteBatch spriteBatch)
    {
        var cache = UiRenderer.Instance;

        if (cache is null)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        for (var i = 0; i < EmoteCatalog.SLOT_COUNT; i++)
        {
            if (!_slotHasEmote[i] || _slots[i] is not { } anim || !EmoteCatalog.TryGet(anim, out var entry))
                continue;

            var texture = cache.GetEmoteHeadTexture(entry.PreviewFrame, _bodyColor);
            var content = cache.GetEmoteHeadContentRect(entry.PreviewFrame, _bodyColor);
            var (ix, iy) = SegmentIconPosition(i);
            var bounds = new Rectangle(ScreenX + ix, ScreenY + iy, ICON_SIZE, ICON_SIZE);
            DrawEmoteIconCentered(spriteBatch, ClipRect, bounds, texture, content);
        }
    }

    /// <summary>
    ///     Uniformly scales the visible emote pixels into <paramref name="bounds" /> and draws them centered.
    /// </summary>
    private static void DrawEmoteIconCentered(
        SpriteBatch spriteBatch,
        Rectangle clipRect,
        Rectangle bounds,
        Texture2D? texture,
        Rectangle contentRect)
    {
        if (texture is null || bounds is { Width: <= 0 } or { Height: <= 0 })
            return;

        Texture2D actualTexture;
        Rectangle sourceRect;

        if (texture is CachedTexture2D { AtlasRegion: { } region })
        {
            actualTexture = region.Atlas;

            if (contentRect is { Width: > 0, Height: > 0 })
            {
                sourceRect = new Rectangle(
                    region.SourceRect.X + contentRect.X,
                    region.SourceRect.Y + contentRect.Y,
                    contentRect.Width,
                    contentRect.Height);
            } else
                sourceRect = region.SourceRect;
        } else
        {
            actualTexture = texture;
            sourceRect = contentRect is { Width: > 0, Height: > 0 }
                ? contentRect
                : new Rectangle(0, 0, texture.Width, texture.Height);
        }

        if (sourceRect is { Width: <= 0 } or { Height: <= 0 })
            return;

        var scale = Math.Min(bounds.Width / (float)sourceRect.Width, bounds.Height / (float)sourceRect.Height);
        var w = Math.Max(1, (int)MathF.Round(sourceRect.Width * scale));
        var h = Math.Max(1, (int)MathF.Round(sourceRect.Height * scale));
        var dest = new Rectangle(
            bounds.X + (bounds.Width - w) / 2,
            bounds.Y + (bounds.Height - h) / 2,
            w,
            h);

        if (!dest.Intersects(clipRect))
            return;

        spriteBatch.Draw(actualTexture, dest, sourceRect, Color.White);
    }

    private void LayoutWheelIcons()
    {
        for (var i = 0; i < EmoteCatalog.SLOT_COUNT; i++)
        {
            var (x, y) = SegmentIconPosition(i);
            _slotPlaceholders[i].X = x;
            _slotPlaceholders[i].Y = y;
        }
    }

    private void LayoutGearButton()
    {
        _gearButton.X = (PANEL_SIZE - _gearButton.Width) / 2;
        _gearButton.Y = (PANEL_SIZE - _gearButton.Height) / 2;
    }

    private void ApplyWheelLayout()
    {
        Width = PANEL_SIZE;
        Height = PANEL_SIZE;
        X = _centerX - Width / 2;
        Y = _centerY - Height / 2;
    }

    private void ApplyCatalogLayout()
    {
        Width = CATALOG_WIDTH;
        Height = CATALOG_HEIGHT;
        X = _centerX - Width / 2;
        Y = _centerY - Height / 2;
        LayoutCatalogPanel();
    }

    private void LayoutCatalogPanel()
    {
        _catalogPanel.X = 0;
        _catalogPanel.Y = 0;
        _catalogPanel.Width = CATALOG_WIDTH;
        _catalogPanel.Height = CATALOG_HEIGHT;
        _catalogPanel.Background = DialogFrame.BuildRecessedTexture(new SKColor(10, 8, 5, 255), CATALOG_WIDTH, CATALOG_HEIGHT);

        var innerWidth = CATALOG_WIDTH - (CATALOG_PAD * 2);
        var btnWidth = innerWidth / EmoteCatalog.SLOT_COUNT;

        _slotPickerRow.X = CATALOG_PAD;
        _slotPickerRow.Y = CATALOG_PAD;
        _slotPickerRow.Width = innerWidth;
        _slotPickerRow.Height = SLOT_PICKER_HEIGHT;

        for (var i = 0; i < EmoteCatalog.SLOT_COUNT; i++)
        {
            _slotPickerButtons[i].X = i * btnWidth;
            _slotPickerButtons[i].Width = btnWidth - 2;
            _slotPickerButtons[i].Height = SLOT_PICKER_HEIGHT;
        }

        var scrollTop = CATALOG_PAD + SLOT_PICKER_HEIGHT + 4;
        _catalogScroll.X = CATALOG_PAD;
        _catalogScroll.Y = scrollTop;
        _catalogScroll.Width = innerWidth;
        _catalogScroll.Height = CATALOG_HEIGHT - scrollTop - CATALOG_PAD;
    }

    private (int X, int Y) SegmentIconPosition(int segment)
    {
        //segment centers are 30° into each 60° wedge (0° = top, clockwise) — matches GetSegmentIndex / wedge art
        var angleDeg = (segment * 60f) + 30f - 90f;
        var rad = angleDeg * MathF.PI / 180f;
        var cx = PANEL_SIZE / 2f;
        var cy = PANEL_SIZE / 2f;
        var ix = (int)(cx + MathF.Cos(rad) * ICON_RADIUS - ICON_SIZE / 2f);
        var iy = (int)(cy + MathF.Sin(rad) * ICON_RADIUS - ICON_SIZE / 2f);

        return (ix, iy);
    }

    private static Texture2D BuildWheelBackground(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var fill = new SKPaint { Color = new SKColor(0, 0, 0, 160), IsAntialias = true };
        canvas.DrawCircle(size / 2f, size / 2f, OUTER_RADIUS, fill);

        using var stroke = new SKPaint
        {
            Color = new SKColor(80, 70, 50, 200),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };

        canvas.DrawCircle(size / 2f, size / 2f, OUTER_RADIUS, stroke);

        return TextureConverter.ToTexture2D(surface.Snapshot());
    }

    private static Texture2D BuildWedgeHighlight(int size, int segment)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var cx = size / 2f;
        var cy = size / 2f;
        var startDeg = (segment * 60f) - 90f;
        const float sweep = 60f;
        var startRad = startDeg * MathF.PI / 180f;
        var endRad = (startDeg + sweep) * MathF.PI / 180f;

        using var path = new SKPath();
        path.MoveTo(cx + HIT_INNER_RADIUS * MathF.Cos(startRad), cy + HIT_INNER_RADIUS * MathF.Sin(startRad));
        path.ArcTo(new SKRect(cx - OUTER_RADIUS, cy - OUTER_RADIUS, cx + OUTER_RADIUS, cy + OUTER_RADIUS), startDeg, sweep, false);
        path.LineTo(cx + HIT_INNER_RADIUS * MathF.Cos(endRad), cy + HIT_INNER_RADIUS * MathF.Sin(endRad));
        path.ArcTo(new SKRect(cx - HIT_INNER_RADIUS, cy - HIT_INNER_RADIUS, cx + HIT_INNER_RADIUS, cy + HIT_INNER_RADIUS), startDeg + sweep, -sweep, false);
        path.Close();

        using var fill = new SKPaint
        {
            Color = new SKColor(220, 180, 60, 110),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        canvas.DrawPath(path, fill);

        using var stroke = new SKPaint
        {
            Color = new SKColor(255, 215, 80, 200),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };

        canvas.DrawPath(path, stroke);

        return TextureConverter.ToTexture2D(surface.Snapshot());
    }

    private enum WheelMode
    {
        Wheel,
        Catalog
    }

    private sealed class SlotPickerButton : UIPanel
    {
        private readonly EmoteWheelControl _owner;
        private readonly int _index;
        private readonly UILabel _numberLabel;
        private readonly UILabel _emptyLabel;
        private Texture2D? _iconTexture;
        private Rectangle _iconContent;

        public SlotPickerButton(EmoteWheelControl owner, int index)
        {
            _owner = owner;
            _index = index;
            Name = $"SlotPicker{index}";

            _numberLabel = new UILabel
            {
                Text = (index + 1).ToString(),
                Width = 12,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Center,
                ForegroundColor = LegendColors.LightGray,
                IsHitTestVisible = false
            };

            _emptyLabel = new UILabel
            {
                Text = "+",
                Width = SLOT_PICKER_ICON,
                Height = SLOT_PICKER_ICON,
                HorizontalAlignment = HorizontalAlignment.Center,
                ForegroundColor = LegendColors.LightGray,
                IsHitTestVisible = false,
                Visible = false
            };

            AddChild(_numberLabel);
            AddChild(_emptyLabel);
        }

        public void Refresh(int bodyColor, int editingSlot, BodyAnimation?[] slots)
        {
            BorderColor = _index == editingSlot ? LegendColors.Gold : null;
            _numberLabel.Width = Width;
            _numberLabel.X = 0;
            _numberLabel.Y = Height - TextRenderer.CHAR_HEIGHT - 1;

            var cache = UiRenderer.Instance;

            if (slots[_index] is { } anim && EmoteCatalog.TryGet(anim, out var entry) && cache is not null)
            {
                _iconTexture = cache.GetEmoteHeadTexture(entry.PreviewFrame, bodyColor);
                _iconContent = cache.GetEmoteHeadContentRect(entry.PreviewFrame, bodyColor);
                _emptyLabel.Visible = false;
            } else
            {
                _iconTexture = null;
                _iconContent = Rectangle.Empty;
                _emptyLabel.Visible = true;
                _emptyLabel.X = (Width - SLOT_PICKER_ICON) / 2;
                _emptyLabel.Y = (Height - SLOT_PICKER_ICON - TextRenderer.CHAR_HEIGHT - 2) / 2;
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (_iconTexture is null)
                return;

            UpdateClipRect();

            if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
                return;

            var iconY = (Height - SLOT_PICKER_ICON - TextRenderer.CHAR_HEIGHT - 2) / 2;
            var bounds = new Rectangle(ScreenX + (Width - SLOT_PICKER_ICON) / 2, ScreenY + iconY, SLOT_PICKER_ICON, SLOT_PICKER_ICON);
            DrawEmoteIconCentered(spriteBatch, ClipRect, bounds, _iconTexture, _iconContent);
        }

        public override void OnClick(ClickEvent e)
        {
            _owner.SelectEditingSlot(_index);
            e.Handled = true;
        }
    }

    private sealed class CatalogScrollHost : UIPanel, IVerticalScrollable
    {
        private readonly UIPanel _content;
        private readonly int _step;
        private int _offsetUnits;

        public CatalogScrollHost(UIPanel content, int step)
        {
            _content = content;
            _step = step;
            AddChild(content);
        }

        private int MaxScrollPx => Math.Max(0, _content.Height - Height);

        public void ResetScroll()
        {
            _offsetUnits = 0;
            _content.Y = 0;
        }

        int IVerticalScrollable.VerticalViewport => _step > 0 ? Height / _step : 0;

        int IVerticalScrollable.VerticalExtent
            => ((IVerticalScrollable)this).VerticalViewport + (_step > 0 ? (MaxScrollPx + _step - 1) / _step : 0);

        int IVerticalScrollable.VerticalOffset
        {
            get => _offsetUnits;
            set
            {
                _offsetUnits = value;
                _content.Y = -Math.Min(value * _step, MaxScrollPx);
            }
        }
    }

    private sealed class CatalogCell : UIPanel
    {
        private readonly BodyAnimation _animation;
        private readonly Action<BodyAnimation> _onPick;
        private readonly int _previewFrame;
        private readonly UILabel _label;
        private Texture2D? _iconTexture;
        private Rectangle _iconContent;

        public CatalogCell(EmoteCatalogEntry entry, Action<BodyAnimation> onPick)
        {
            _animation = entry.Animation;
            _onPick = onPick;
            _previewFrame = entry.PreviewFrame;

            _label = new UILabel
            {
                X = 0,
                Y = CATALOG_ICON_SIZE + 4,
                Width = CATALOG_CELL_WIDTH - 2,
                Height = TextRenderer.CHAR_HEIGHT,
                Text = entry.Name,
                HorizontalAlignment = HorizontalAlignment.Center,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false
            };

            AddChild(_label);
        }

        public void RefreshIcon(int bodyColor)
        {
            var cache = UiRenderer.Instance;

            if (cache is null)
            {
                _iconTexture = null;
                _iconContent = Rectangle.Empty;

                return;
            }

            _iconTexture = cache.GetEmoteHeadTexture(_previewFrame, bodyColor);
            _iconContent = cache.GetEmoteHeadContentRect(_previewFrame, bodyColor);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (_iconTexture is null)
                return;

            UpdateClipRect();

            if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
                return;

            var iconX = ScreenX + (Width - CATALOG_ICON_SIZE) / 2;
            var bounds = new Rectangle(iconX, ScreenY + 2, CATALOG_ICON_SIZE, CATALOG_ICON_SIZE);
            DrawEmoteIconCentered(spriteBatch, ClipRect, bounds, _iconTexture, _iconContent);
        }

        public override void OnClick(ClickEvent e)
        {
            _onPick(_animation);
            e.Handled = true;
        }
    }
}
