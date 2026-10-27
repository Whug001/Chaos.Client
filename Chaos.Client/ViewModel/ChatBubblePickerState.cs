using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

public enum BubbleColorField
{
    Border,
    Fill,
    Text
}

/// <summary>The Effects window's Chat bubble tab: tiles, selection, typed colors and the readability check. No graphics.</summary>
public sealed class ChatBubblePickerState
{
    public const int ALPHA_STEP = 20;
    public const string UNREADABLE = "Text is hard to read on this fill";
    public const string BAD_HEX = "Use six hex digits, like #3A7BD5";

    public static readonly ChatBubbleEntry DefaultEntry = new() { Key = 0, Name = "Default", Look = ChatBubbleLook.Default, Owned = true };

    private List<ChatBubbleEntry> Presets = [];
    private int SelectedIndex;

    public int Page { get; private set; }
    public byte Current { get; private set; }
    public bool LicenseOwned { get; private set; }
    public int LicensePrice { get; private set; }
    public string BorderHex { get; private set; } = "#FFFFFF";
    public string FillHex { get; private set; } = "#000000";
    public string TextHex { get; private set; } = "#FFFFFF";
    public byte FillAlpha { get; private set; } = 85;

    public ChatBubbleEntry CustomEntry
        => new()
        {
            Key = ChatBubbleLook.CUSTOM_STYLE,
            Name = "Custom",
            Price = LicensePrice,
            Look = DraftLook ?? ChatBubbleLook.Default,
            Owned = LicenseOwned
        };

    public IReadOnlyList<ChatBubbleEntry> Entries => [DefaultEntry, .. Presets, CustomEntry];
    public ChatBubbleEntry Selected => Entries[Math.Clamp(SelectedIndex, 0, Entries.Count - 1)];
    public bool IsCustomSelected => Selected.Key == ChatBubbleLook.CUSTOM_STYLE;
    public bool ShowsHexBoxes => IsCustomSelected && LicenseOwned;
    public int PageCount => Math.Max(1, (Entries.Count + EffectWindowState.PAGE_SIZE - 1) / EffectWindowState.PAGE_SIZE);
    public IReadOnlyList<ChatBubbleEntry> PageItems => Entries.Skip(Page * EffectWindowState.PAGE_SIZE).Take(EffectWindowState.PAGE_SIZE).ToList();

    /// <summary>The typed colors as a look, or null while any box holds a bad hex code.</summary>
    public ChatBubbleLook? DraftLook
    {
        get
        {
            if (!ChatBubbleColors.TryParseHex(BorderHex, out var border)
                || !ChatBubbleColors.TryParseHex(FillHex, out var fill)
                || !ChatBubbleColors.TryParseHex(TextHex, out var text))
                return null;

            return new ChatBubbleLook
            {
                Border = border,
                Fill = (fill & 0xFFFFFF00) | FillAlpha,
                Text = text,
                Accent = border
            };
        }
    }

    public string? Warning
        => !ShowsHexBoxes ? null
            : DraftLook is not { } draft ? BAD_HEX
            : ChatBubbleColors.IsReadableCustom(draft.Fill, draft.Text) ? null
            : UNREADABLE;

    public bool CanChoose => Selected.Owned && (!IsCustomSelected || ((DraftLook is not null) && (Warning is null)));

    public ChatBubbleLook PreviewLook => IsCustomSelected ? DraftLook ?? ChatBubbleLook.Default : Selected.Look;

    public (byte Key, ChatBubbleLook? Custom) ChoosePayload => (Selected.Key, IsCustomSelected ? DraftLook : null);

    public string StatusText
        => Selected.Owned ? $"Current: {NameOf(Current)}" : $"{Selected.Price:N0} gold - {EffectWindowState.SOLD_BY}";

    public bool IsCurrent(ChatBubbleEntry entry) => entry.Key == Current;

    public void Open(EffectWindowDisplayArgs args)
    {
        Presets = args.BubbleStyles.ToList();
        LicenseOwned = args.LicenseOwned;
        LicensePrice = args.LicensePrice;
        Update(args);
        SelectedIndex = Math.Max(0, Entries.ToList().FindIndex(e => e.Key == Current));
        Page = SelectedIndex / EffectWindowState.PAGE_SIZE;
    }

    public void Update(EffectWindowDisplayArgs args)
    {
        Current = args.CurrentBubble;
        BorderHex = ChatBubbleColors.ToHex(args.CustomLook.Border);
        FillHex = ChatBubbleColors.ToHex(args.CustomLook.Fill);
        TextHex = ChatBubbleColors.ToHex(args.CustomLook.Text);
        FillAlpha = Math.Clamp(ChatBubbleColors.Unpack(args.CustomLook.Fill).A, ChatBubbleColors.MIN_FILL_ALPHA, ChatBubbleColors.MAX_FILL_ALPHA);
    }

    public void Select(ChatBubbleEntry entry)
    {
        var index = Entries.ToList().FindIndex(e => e.Key == entry.Key);

        if (index >= 0)
            SelectedIndex = index;
    }

    public void SetHex(BubbleColorField field, string text)
    {
        switch (field)
        {
            case BubbleColorField.Border:
                BorderHex = text;

                break;
            case BubbleColorField.Fill:
                FillHex = text;

                break;
            case BubbleColorField.Text:
                TextHex = text;

                break;
        }
    }

    public string HexOf(BubbleColorField field)
        => field switch
        {
            BubbleColorField.Border => BorderHex,
            BubbleColorField.Fill   => FillHex,
            _                       => TextHex
        };

    public void StepAlpha(int delta)
        => FillAlpha = (byte)Math.Clamp(FillAlpha + delta, ChatBubbleColors.MIN_FILL_ALPHA, ChatBubbleColors.MAX_FILL_ALPHA);

    public void StepPage(int delta) => Page = Math.Clamp(Page + delta, 0, PageCount - 1);

    private string NameOf(byte key) => Entries.FirstOrDefault(e => e.Key == key)?.Name ?? DefaultEntry.Name;
}
