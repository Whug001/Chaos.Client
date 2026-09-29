using System.Globalization;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>
///     What the ballot window shows and which buttons it offers: the selected candidate, the page, the two-click vote and
///     the statement editor. The server checks every rule again; these are only the buttons' states.
/// </summary>
public sealed class TownBallotState
{
    public const int PAGE_SIZE = 8;
    public const int MAX_STATEMENT_LINE_BREAKS = 8;

    public TownBallotArgs Args { get; private set; } = new() { Type = TownBallotType.Open };
    public bool Confirming { get; private set; }
    public bool Editing { get; private set; }

    /// <summary>The statement last sent to the server, kept until the server's answer shows it was accepted.</summary>
    public string? PendingStatement { get; private set; }
    public int Page { get; private set; }
    public string? SelectedName { get; private set; }

    public TownBallotCandidateInfo? Selected
        => SelectedName is null ? null : Args.Candidates.FirstOrDefault(c => c.Name.Equals(SelectedName, StringComparison.OrdinalIgnoreCase));

    public int Pages => Math.Max(1, (Args.Candidates.Count + PAGE_SIZE - 1) / PAGE_SIZE);

    public IReadOnlyList<TownBallotCandidateInfo> PageItems => Args.Candidates.Skip(Page * PAGE_SIZE).Take(PAGE_SIZE).ToList();

    public bool ShowsVoteButton => (Args.Stage == TownBallotStage.Voting) && Args.CanVote && Selected is not null;

    public bool VoteEnabled => ShowsVoteButton && !IsMyVote(Selected!);

    public string VoteCaption
        => Selected is not { } selected ? string.Empty
            : IsMyVote(selected) ? "Your vote"
            : Confirming ? $"Confirm: {selected.Name}?"
            : $"Vote for {selected.Name}";

    public bool ShowsEditButton => (Args.Stage == TownBallotStage.Candidacy) && Selected is { IsViewer: true };

    /// <summary>The line under the buttons: the answer to the last action, else the vote note.</summary>
    public string Note => string.IsNullOrEmpty(Args.Status) ? Args.VoteNote : Args.Status;

    /// <summary>Fresh window: first page, first candidate.</summary>
    public void Open(TownBallotArgs args)
    {
        SelectedName = null;
        PendingStatement = null;
        Page = 0;
        Apply(args);
    }

    /// <summary>New data for an open window. The selection stays while its candidate is still listed.</summary>
    public void Apply(TownBallotArgs args)
    {
        Args = args;
        Confirming = false;
        Editing = false;

        if (Selected is null)
        {
            var own = args.Stage == TownBallotStage.Candidacy ? args.Candidates.FirstOrDefault(c => c.IsViewer) : null;
            SelectedName = (own ?? args.Candidates.FirstOrDefault())?.Name;
        }

        Page = Math.Clamp(Page, 0, Pages - 1);

        if (PendingStatement is not null)
        {
            //an accepted save shows up as the viewer's statement; a refused one keeps the draft open
            if (args.Candidates.FirstOrDefault(c => c.IsViewer)?.Statement == PendingStatement)
                PendingStatement = null;
            else if (ShowsEditButton)
                Editing = true;
            else
                PendingStatement = null;
        }
    }

    public void Select(string name)
    {
        if (Args.Candidates.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedName = name;
            Confirming = false;
            Editing = false;
        }
    }

    public void TurnPage(int delta) => Page = Math.Clamp(Page + delta, 0, Pages - 1);

    /// <summary>First click asks for confirmation; the second returns the vote to send.</summary>
    public TownBallotInteractionArgs? ClickVote()
    {
        if (!VoteEnabled)
            return null;

        if (!Confirming)
        {
            Confirming = true;

            return null;
        }

        Confirming = false;

        return new TownBallotInteractionArgs
        {
            Action = TownBallotAction.Vote,
            TownKey = Args.TownKey,
            Candidate = Selected!.Name
        };
    }

    public void StartEdit()
    {
        if (ShowsEditButton)
            Editing = true;
    }

    public void CancelEdit()
    {
        Editing = false;
        PendingStatement = null;
    }

    /// <summary>The statement to send, or null when not editing or the text is empty, too long or has too many lines.</summary>
    public TownBallotInteractionArgs? Save(string text)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

        if (!Editing
            || (text.Length == 0)
            || (text.Length > TownBallotProtocol.MAX_STATEMENT_CHARS)
            || (text.Count(c => c == '\n') > MAX_STATEMENT_LINE_BREAKS))
            return null;

        PendingStatement = text;

        return new TownBallotInteractionArgs
        {
            Action = TownBallotAction.SaveStatement,
            TownKey = Args.TownKey,
            Statement = text
        };
    }

    public static string ClassLine(TownBallotCandidateInfo candidate)
        => candidate.Level > 0 ? $"Level {candidate.Level} {candidate.ClassName}" : "Not seen yet";

    public static string CitizenLine(TownBallotCandidateInfo candidate)
        => candidate.CitizenSinceUtc is { } since
            ? $"Citizen since {since.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : "Citizen";

    public static string TermsLine(TownBallotCandidateInfo candidate)
    {
        var parts = new List<string>();

        if (candidate.MayorTerms > 0)
            parts.Add($"mayor {Times(candidate.MayorTerms)}");

        if (candidate.CouncilTerms > 0)
            parts.Add($"councillor {Times(candidate.CouncilTerms)}");

        if (parts.Count == 0)
            return "First run";

        var line = string.Join(", ", parts);

        return char.ToUpperInvariant(line[0]) + line[1..];
    }

    private static string Times(int count) => count switch { 1 => "once", 2 => "twice", _ => $"{count} times" };

    private bool IsMyVote(TownBallotCandidateInfo candidate) => candidate.Name.Equals(Args.MyVote, StringComparison.OrdinalIgnoreCase);
}
