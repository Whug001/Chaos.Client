#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Emblems;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Profile;

/// <summary>
///     Emblem tab page (_nui_ebl): the simplified Korean emblem book. The left page is a 5x5 grid of the player's emblems
///     at 2x (owned first, locked ones dimmed) with PREV/NEXT paging. The right page shows the selected emblem at 3x, its
///     name, time left, description and status, and a Show/Hide button that picks the emblem for the world list.
///     Everything comes from <see cref="WorldState.EmblemBook" />; the page asks for a fresh book each time it becomes
///     visible.
/// </summary>
public sealed class SelfProfileEmblemTab : PrefabPanel
{
    private const string BUTTON_FILE = "_nui_eblb.spf";
    private const int HIDE_FRAME = 2;
    private const int HIDE_PRESSED_FRAME = 3;
    private const int SHOW_FRAME = 0;
    private const int SHOW_PRESSED_FRAME = 1;
    private const int STATUS_HEIGHT = 12;
    private static readonly Color Gold = new(232, 196, 96);

    private readonly UILabel DescriptionLabel;
    private readonly UILabel NameLabel;
    private readonly UILabel PageLabel;
    private readonly EmblemIcon Preview;
    private readonly UIButton? ShowButton;
    private readonly EmblemIcon[] Slots = new EmblemIcon[EmblemBookLayout.PAGE_SIZE];
    private readonly UILabel StatusLabel;
    private readonly UILabel TimeLabel;

    private int Page;
    private string? SelectedKey;

    public SelfProfileEmblemTab(string prefabName)
        : base(prefabName, false)
    {
        Name = prefabName;
        Visible = false;

        var grid = GetRect("GRID");

        for (var i = 0; i < Slots.Length; i++)
        {
            (var dx, var dy) = EmblemBookLayout.SlotOffset(i);
            var slot = i;

            Slots[i] = new EmblemIcon
            {
                Name = $"Slot{i}",
                X = grid.X + dx,
                Y = grid.Y + dy,
                Width = EmblemBookLayout.SLOT_SIZE,
                Height = EmblemBookLayout.SLOT_SIZE,
                Scale = 2
            };

            Slots[i].Clicked += () => SelectSlot(slot);
            AddChild(Slots[i]);
        }

        var preview = GetRect("PREVIEW");

        Preview = new EmblemIcon
        {
            Name = "Preview",
            X = preview.X,
            Y = preview.Y,
            Width = preview.Width,
            Height = preview.Height,
            Scale = 3
        };

        AddChild(Preview);

        NameLabel = AddLabel("NAME", HorizontalAlignment.Left);
        TimeLabel = AddLabel("TIMELEFT", HorizontalAlignment.Left);
        PageLabel = AddLabel("PAGE", HorizontalAlignment.Center);

        var description = GetRect("DESC");

        DescriptionLabel = new UILabel
        {
            Name = "Description",
            X = description.X,
            Y = description.Y,
            Width = description.Width,
            Height = description.Height - STATUS_HEIGHT,
            WordWrap = true,
            PaddingLeft = 0
        };

        AddChild(DescriptionLabel);

        StatusLabel = new UILabel
        {
            Name = "Status",
            X = description.X,
            Y = description.Bottom - STATUS_HEIGHT,
            Width = description.Width,
            Height = STATUS_HEIGHT,
            PaddingLeft = 0
        };

        AddChild(StatusLabel);

        if (CreateButton("PREV") is { } prev)
            prev.Clicked += () => TurnPage(EmblemBookLayout.PrevPage(Page, Book.Entries.Count));

        if (CreateButton("NEXT") is { } next)
            next.Clicked += () => TurnPage(EmblemBookLayout.NextPage(Page, Book.Entries.Count));

        ShowButton = CreateButton("SHOW");

        if (ShowButton is not null)
            ShowButton.Clicked += OnShowClicked;

        WorldState.EmblemBook.Changed += Refresh;

        VisibilityChanged += visible =>
        {
            if (!visible)
                return;

            Refresh();
            OnBookRequested?.Invoke();
        };

        Refresh();
    }

    private static EmblemBook Book => WorldState.EmblemBook;

    /// <summary>Raised when the page wants a fresh emblem book from the server.</summary>
    public event Action? OnBookRequested;

    /// <summary>Raised with the key to show, or an empty key to hide.</summary>
    public event Action<string>? OnChoice;

    public override void Dispose()
    {
        WorldState.EmblemBook.Changed -= Refresh;

        //UIButton.Dispose disposes its textures, but these are UiRenderer's shared cached ones
        if (ShowButton is not null)
        {
            ShowButton.NormalTexture = null;
            ShowButton.PressedTexture = null;
            ShowButton.SelectedTexture = null;
            ShowButton.HoverTexture = null;
            ShowButton.DisabledTexture = null;
        }

        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        var entries = Book.Entries;
        var first = Page * EmblemBookLayout.PAGE_SIZE;
        var selected = EmblemBookLayout.IndexOf(entries, SelectedKey) - first;

        if (selected is >= 0 and < EmblemBookLayout.PAGE_SIZE)
            DrawOutline(spriteBatch, Slots[selected]);

        var shown = EmblemBookLayout.IndexOf(entries, Book.ShownKey) - first;

        if (shown is >= 0 and < EmblemBookLayout.PAGE_SIZE)
            DrawRectClipped(
                spriteBatch,
                new Rectangle(Slots[shown].ScreenX + EmblemBookLayout.SLOT_SIZE - 7, Slots[shown].ScreenY + 4, 3, 3),
                Gold);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible)
            UpdateTimeLeft();
    }

    private UILabel AddLabel(string rectName, HorizontalAlignment alignment)
    {
        var rect = GetRect(rectName);

        var label = new UILabel
        {
            Name = rectName,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            HorizontalAlignment = alignment,
            PaddingLeft = 0
        };

        AddChild(label);

        return label;
    }

    private void BindPage()
    {
        var entries = Book.Entries;

        for (var i = 0; i < Slots.Length; i++)
        {
            var index = Page * EmblemBookLayout.PAGE_SIZE + i;
            var entry = index < entries.Count ? entries[index] : null;

            Slots[i].Art = entry?.Art ?? 0;
            Slots[i].GuildEmblemId = entry?.GuildEmblemId ?? 0;
            Slots[i].Dimmed = entry is { Owned: false };
        }

        PageLabel.Text = EmblemBookLayout.PageText(Page, entries.Count);
    }

    private void BindSelection()
    {
        var entry = SelectedEntry();

        Preview.Art = entry?.Art ?? 0;
        Preview.GuildEmblemId = entry?.GuildEmblemId ?? 0;
        Preview.Dimmed = entry is { Owned: false };
        NameLabel.Text = entry?.Name ?? string.Empty;
        DescriptionLabel.Text = entry?.Description ?? string.Empty;
        StatusLabel.Text = entry is null ? string.Empty : EmblemBookLayout.StatusText(entry);
        UpdateTimeLeft();
        UpdateShowButton(entry);
    }

    private void DrawOutline(SpriteBatch spriteBatch, UIElement slot)
    {
        var r = new Rectangle(slot.ScreenX, slot.ScreenY, slot.Width, slot.Height);

        DrawRectClipped(spriteBatch, new Rectangle(r.X, r.Y, r.Width, 1), Gold);
        DrawRectClipped(spriteBatch, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), Gold);
        DrawRectClipped(spriteBatch, new Rectangle(r.X, r.Y, 1, r.Height), Gold);
        DrawRectClipped(spriteBatch, new Rectangle(r.Right - 1, r.Y, 1, r.Height), Gold);
    }

    private void OnShowClicked()
    {
        if (SelectedEntry() is not { Owned: true } entry)
            return;

        OnChoice?.Invoke(entry.Key.Equals(Book.ShownKey, StringComparison.OrdinalIgnoreCase) ? string.Empty : entry.Key);
    }

    /// <summary>
    ///     Re-binds from the current book. Keeps the page and selection while the selected key is still present;
    ///     otherwise selects the first entry on page 1.
    /// </summary>
    private void Refresh()
    {
        var entries = Book.Entries;

        if (EmblemBookLayout.IndexOf(entries, SelectedKey) < 0)
        {
            SelectedKey = entries.Count > 0 ? entries[0].Key : null;
            Page = 0;
        }

        Page = Math.Min(Page, EmblemBookLayout.PageCount(entries.Count) - 1);
        BindPage();
        BindSelection();
    }

    private EmblemBookEntry? SelectedEntry()
    {
        var index = EmblemBookLayout.IndexOf(Book.Entries, SelectedKey);

        return index >= 0 ? Book.Entries[index] : null;
    }

    private void SelectSlot(int slot)
    {
        var index = Page * EmblemBookLayout.PAGE_SIZE + slot;

        if (index >= Book.Entries.Count)
            return;

        SelectedKey = Book.Entries[index].Key;
        BindSelection();
    }

    private void TurnPage(int page)
    {
        Page = page;
        BindPage();
    }

    private void UpdateShowButton(EmblemBookEntry? entry)
    {
        if (ShowButton is null)
            return;

        ShowButton.Visible = entry is { Owned: true };

        var hide = entry is not null && entry.Key.Equals(Book.ShownKey, StringComparison.OrdinalIgnoreCase);
        var cache = UiRenderer.Instance!;

        ShowButton.NormalTexture = cache.GetSpfTexture(BUTTON_FILE, hide ? HIDE_FRAME : SHOW_FRAME);
        ShowButton.PressedTexture = cache.GetSpfTexture(BUTTON_FILE, hide ? HIDE_PRESSED_FRAME : SHOW_PRESSED_FRAME);
    }

    private void UpdateTimeLeft()
    {
        var entry = SelectedEntry();
        var text = entry is null ? string.Empty : EmblemBookLayout.TimeLeftText(entry, Book.SecondsLeftAt(entry, DateTime.UtcNow));

        if (TimeLabel.Text != text)
            TimeLabel.Text = text;
    }
}
