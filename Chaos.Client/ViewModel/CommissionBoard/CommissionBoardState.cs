#region
using System.Globalization;
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel.CommissionBoard;

/// <summary>The commission board window's tabs.</summary>
public enum CommissionBoardTab
{
    Open,
    MyRequests,
    MyCommissions
}

/// <summary>
///     What the selected row offers. Everything but Review maps to a <see cref="CommissionBoardActionType" /> send;
///     Review opens the review pane.
/// </summary>
public enum CommissionBoardRowAction
{
    Claim,
    Cancel,
    Drop,
    Deliver,
    Review,
    Accept,
    Redo,
    CollectGold,
    CollectItem
}

/// <summary>One inventory portrait the deliver picker can send.</summary>
public readonly record struct CommissionBoardDeliverChoice(byte Slot, string Name, ushort Sprite, DisplayColor Color);

/// <summary>
///     The commission board window's state. Pure: the window binds to it and sends what it builds. The three lists are
///     shown as the server sent them, except Open requests naming another artist, which only that artist may see.
/// </summary>
public sealed class CommissionBoardState
{
    public const int MAX_DESCRIPTION_CHARS = 200;
    public const int MAX_NOTE_CHARS = 200;
    public const int MAX_PACKET_STRING_BYTES = 255;
    public const long MAX_GOLD = int.MaxValue;

    //the same encoding the client uses when it writes string8 fields; Korean characters take 2 bytes.
    //lazy so the code-pages provider registers before the lookup, even in a headless test process
    private static readonly Lazy<Encoding> PacketEncoding = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return Encoding.GetEncoding(949);
    });

    public string ViewerName { get; private set; } = string.Empty;
    public CommissionBoardTab SelectedTab { get; private set; } = CommissionBoardTab.Open;
    public string? SelectedId { get; private set; }

    public IReadOnlyList<CommissionBoardRow> OpenRequests { get; private set; } = [];
    public IReadOnlyList<CommissionBoardRow> MyRequests { get; private set; } = [];
    public IReadOnlyList<CommissionBoardRow> MyCommissions { get; private set; } = [];

    public event Action? Changed;

    public IReadOnlyList<CommissionBoardRow> CurrentRows
        => SelectedTab switch
        {
            CommissionBoardTab.MyRequests => MyRequests,
            CommissionBoardTab.MyCommissions => MyCommissions,
            _ => OpenRequests
        };

    public CommissionBoardRow? SelectedRow
        => SelectedId is null ? null : CurrentRows.FirstOrDefault(r => r.Id.Equals(SelectedId, StringComparison.Ordinal));

    private IEnumerable<CommissionBoardRow> AllRows => OpenRequests.Concat(MyRequests).Concat(MyCommissions);

    /// <summary>
    ///     Replaces all three tabs with a fresh display. Keeps the tab and the selected row by Id while it still exists.
    /// </summary>
    public void Apply(CommissionBoardDisplayArgs args, string viewerName)
    {
        ViewerName = viewerName ?? string.Empty;
        OpenRequests = args.Open.Where(r => IsVisibleOpenRow(r, ViewerName)).ToList();
        MyRequests = args.MyRequests.ToList();
        MyCommissions = args.MyCommissions.ToList();

        if ((SelectedId is not null) && AllRows.All(r => !r.Id.Equals(SelectedId, StringComparison.Ordinal)))
            SelectedId = null;

        Changed?.Invoke();
    }

    public void SelectTab(CommissionBoardTab tab)
    {
        if (SelectedTab == tab)
            return;

        SelectedTab = tab;

        if ((SelectedId is not null) && CurrentRows.All(r => !r.Id.Equals(SelectedId, StringComparison.Ordinal)))
            SelectedId = null;

        Changed?.Invoke();
    }

    public void Select(string? id)
    {
        if (SelectedId == id)
            return;

        SelectedId = id;
        Changed?.Invoke();
    }

    public void Clear()
    {
        ViewerName = string.Empty;
        OpenRequests = [];
        MyRequests = [];
        MyCommissions = [];
        SelectedId = null;
        SelectedTab = CommissionBoardTab.Open;
    }

    /// <summary>Defensive filter for the Open tab: a named request shows only for that artist. Nothing else is hidden.</summary>
    public static bool IsVisibleOpenRow(CommissionBoardRow row, string viewerName)
        => string.IsNullOrEmpty(row.NamedArtist) || row.NamedArtist.Equals(viewerName, StringComparison.OrdinalIgnoreCase);

    public bool IsBuyer(CommissionBoardRow row) => row.Buyer.Equals(ViewerName, StringComparison.OrdinalIgnoreCase);

    public bool IsClaimant(CommissionBoardRow row)
        => !string.IsNullOrEmpty(row.ClaimedBy) && row.ClaimedBy.Equals(ViewerName, StringComparison.OrdinalIgnoreCase);

    public bool IsItemOwner(CommissionBoardRow row)
        => !string.IsNullOrEmpty(row.ArtistItemOwner) && row.ArtistItemOwner.Equals(ViewerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Which actions the viewer is offered for a row, from its state and their role. Closed rows offer collects only.</summary>
    public IReadOnlyList<CommissionBoardRowAction> ActionsFor(CommissionBoardRow row)
    {
        var actions = new List<CommissionBoardRowAction>();
        var buyer = IsBuyer(row);
        var claimant = IsClaimant(row);
        var owner = IsItemOwner(row);

        if (row.State == CommissionBoardRowState.Closed)
        {
            if (IsItemCollectable(row, buyer, owner))
                actions.Add(CommissionBoardRowAction.CollectItem);

            return actions;
        }

        switch (row.State)
        {
            case CommissionBoardRowState.Open:
                //claiming your own request is refused server-side, so it is never offered
                actions.Add(buyer ? CommissionBoardRowAction.Cancel : CommissionBoardRowAction.Claim);

                break;
            case CommissionBoardRowState.Claimed when claimant:
                actions.Add(CommissionBoardRowAction.Drop);
                actions.Add(CommissionBoardRowAction.Deliver);

                break;
            case CommissionBoardRowState.Delivered when buyer:
                actions.Add(CommissionBoardRowAction.Review);

                break;
        }

        if ((row.ArtistGoldCollectable > 0) && claimant)
            actions.Add(CommissionBoardRowAction.CollectGold);

        if (IsItemCollectable(row, buyer, owner))
            actions.Add(CommissionBoardRowAction.CollectItem);

        return actions;
    }

    /// <summary>True when the buyer may still ask for a redo on a delivered row: once only, then accept only.</summary>
    public bool CanRedo(CommissionBoardRow row) => IsBuyer(row) && (row.State == CommissionBoardRowState.Delivered) && !row.RedoUsed;

    /// <summary>The list rows' right-hand note: the gold offer on Open requests, the short status elsewhere.</summary>
    public static string RowNote(CommissionBoardRow row, CommissionBoardTab tab)
        => tab == CommissionBoardTab.Open ? OfferText(row) : StatusText(row);

    public static string OfferText(CommissionBoardRow row)
        => string.Create(CultureInfo.InvariantCulture, $"{row.OfferGold:N0} gold");

    public static string StatusText(CommissionBoardRow row)
        => row.State switch
        {
            CommissionBoardRowState.Open => string.IsNullOrEmpty(row.NamedArtist) ? "Open" : $"For {row.NamedArtist}",
            CommissionBoardRowState.Claimed => string.IsNullOrEmpty(row.ClaimedBy)
                ? "Claimed"
                : $"Claimed by {row.ClaimedBy}",
            CommissionBoardRowState.Delivered => "Delivered",
            CommissionBoardRowState.Accepted => "Accepted",
            CommissionBoardRowState.Closed => "Closed",
            _ => string.Empty
        };

    /// <summary>The detail pane's lines for a row: the state first, then the redo note, the description and who and how much.</summary>
    public string DetailsText(CommissionBoardRow row)
    {
        var lines = new List<string>();
        var stateLine = StateLine(row);

        if (stateLine.Length > 0)
            lines.Add(stateLine);

        if (!string.IsNullOrEmpty(row.RedoNote))
            lines.Add($"Redo note: {row.RedoNote}");

        lines.Add(row.Description);
        lines.Add(BuyerLine(row));

        if ((row.ArtistGoldCollectable > 0) && IsClaimant(row))
            lines.Add("Gold waits for collection.");

        if (IsItemCollectable(row, IsBuyer(row), IsItemOwner(row)))
            lines.Add("Portrait waits for collection.");

        return string.Join('\n', lines);
    }

    public static string DeadlineText(ulong claimDeadline, DateTimeOffset now)
    {
        if (claimDeadline == 0)
            return string.Empty;

        var left = DateTimeOffset.FromUnixTimeSeconds((long)claimDeadline) - now;

        if (left <= TimeSpan.Zero)
            return "Expired";

        return left.TotalDays >= 1 ? $"{(int)left.TotalDays}d left" : left.TotalHours >= 1 ? $"{(int)left.TotalHours}h left" : $"{(int)left.TotalMinutes}m left";
    }

    /// <summary>How many bytes text takes in the packet string encoding.</summary>
    public static int PacketByteCount(string text) => PacketEncoding.Value.GetByteCount(text);

    public static bool FitsPacketString(string text) => PacketByteCount(text) <= MAX_PACKET_STRING_BYTES;

    /// <summary>Null when the post form sends; otherwise the window's error line.</summary>
    public static string? ValidatePost(string description, string goldText, out uint gold)
    {
        gold = 0;

        if (string.IsNullOrWhiteSpace(description))
            return "Describe the portrait first.";

        if ((description.Length > MAX_DESCRIPTION_CHARS) || !FitsPacketString(description))
            return "The description is too long.";

        if (!uint.TryParse(goldText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out gold)
            || (gold == 0)
            || (gold > int.MaxValue))
            return "Offer 1 to 2147483647 gold.";

        return null;
    }

    /// <summary>Null when the redo note sends; otherwise the window's error line. An empty note sends.</summary>
    public static string? ValidateRedoNote(string note)
    {
        if ((note.Length > MAX_NOTE_CHARS) || !FitsPacketString(note))
            return "The note is too long.";

        return null;
    }

    public static bool TryBuildPost(
        string description,
        string goldText,
        string artist,
        out CommissionBoardActionArgs args,
        out string error)
    {
        args = null!;
        error = string.Empty;

        if (ValidatePost(description, goldText, out var gold) is { } problem)
        {
            error = problem;

            return false;
        }

        args = new CommissionBoardActionArgs
        {
            Action = CommissionBoardActionType.Post,
            Description = description,
            Gold = gold,
            Artist = artist.Trim()
        };

        return true;
    }

    public static bool TryBuildRedo(string requestId, string note, out CommissionBoardActionArgs args, out string error)
    {
        args = null!;
        error = string.Empty;

        if (ValidateRedoNote(note) is { } problem)
        {
            error = problem;

            return false;
        }

        args = new CommissionBoardActionArgs
        {
            Action = CommissionBoardActionType.Redo,
            RequestId = requestId,
            Note = note
        };

        return true;
    }

    /// <summary>Builds a row send for every button action. Review is UI-only and throws.</summary>
    public static CommissionBoardActionArgs ActionArgs(CommissionBoardRowAction action, string requestId, byte slot = 0)
        => new()
        {
            Action = action switch
            {
                CommissionBoardRowAction.Claim => CommissionBoardActionType.Claim,
                CommissionBoardRowAction.Cancel => CommissionBoardActionType.Cancel,
                CommissionBoardRowAction.Drop => CommissionBoardActionType.Drop,
                CommissionBoardRowAction.Deliver => CommissionBoardActionType.Deliver,
                CommissionBoardRowAction.Accept => CommissionBoardActionType.Accept,
                CommissionBoardRowAction.Redo => CommissionBoardActionType.Redo,
                CommissionBoardRowAction.CollectGold => CommissionBoardActionType.CollectGold,
                CommissionBoardRowAction.CollectItem => CommissionBoardActionType.CollectItem,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Review opens the review pane and sends nothing.")
            },
            RequestId = requestId,
            Slot = slot
        };

    /// <summary>The deliver picker's rows: occupied slots holding a portrait the viewer painted.</summary>
    public static IReadOnlyList<CommissionBoardDeliverChoice> DeliverChoices(Inventory inventory, string painterName)
    {
        var choices = new List<CommissionBoardDeliverChoice>();

        for (byte slot = 1; slot <= Inventory.MAX_SLOTS; slot++)
        {
            ref readonly var item = ref inventory.GetSlot(slot);

            if (!item.IsOccupied || string.IsNullOrEmpty(item.PortraitHash))
                continue;

            if (!painterName.Equals(item.PortraitPainter, StringComparison.OrdinalIgnoreCase))
                continue;

            choices.Add(new CommissionBoardDeliverChoice(slot, Inventory.BaseName(in item) ?? item.Name ?? $"Slot {slot}", item.Sprite, item.Color));
        }

        return choices;
    }

    private static bool IsItemCollectable(CommissionBoardRow row, bool buyer, bool owner)
        => (row.BuyerItemWaiting && buyer) || (row.ArtistItemWaiting && owner);

    private string BuyerLine(CommissionBoardRow row)
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"Buyer {row.Buyer} - {row.OfferGold:N0} gold");

        return string.IsNullOrEmpty(row.NamedArtist) ? line : $"{line} for {row.NamedArtist}";
    }

    private string StateLine(CommissionBoardRow row)
        => row.State switch
        {
            CommissionBoardRowState.Open => StatusText(row),
            CommissionBoardRowState.Claimed => string.IsNullOrEmpty(row.ClaimedBy)
                ? "Claimed"
                : $"Claimed by {row.ClaimedBy} - {DeadlineText(row.ClaimDeadline, DateTimeOffset.UtcNow)}",
            CommissionBoardRowState.Delivered => IsBuyer(row) ? "Delivered - waiting review" : "Delivered",
            CommissionBoardRowState.Accepted => "Accepted",
            CommissionBoardRowState.Closed => "Closed",
            _ => string.Empty
        };
}
