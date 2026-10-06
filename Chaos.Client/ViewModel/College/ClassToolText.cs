using System.Globalization;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

/// <summary>Every line of text the quiz card, the Teacher panel, the debate panel and the side markers show.</summary>
public static class ClassToolText
{
    public static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    public static string QuizCaption(CollegeQuizInfo quiz) => $"Quiz: {quiz.Title} ({quiz.Number}/{quiz.Count})";

    public static string RightLine(int right, int answered) => answered == 0 ? "No one answered." : $"{right} of {answered} got it right.";

    public static string MyResult(byte myAnswer, byte right, int myScore)
        => myAnswer == CollegeProtocol.NO_ANSWER ? $"No answer. Your score: {myScore}."
            : myAnswer == right ? $"Right! Your score: {myScore}."
            : $"Wrong. Your score: {myScore}.";

    public static string TopLine(IReadOnlyList<CollegeScoreInfo> top, string viewer)
        => top.Count == 0
            ? string.Empty
            : "Top: " + string.Join(", ", top.Select(t => $"{(t.Name.Equals(viewer, StringComparison.OrdinalIgnoreCase) ? "you" : t.Name)} {t.Score}"));

    public static string AnsweredLine(int answered, int students, int secondsLeft) => $"Answered {answered} of {students} - {Clock(secondsLeft)} left";

    public static string Counts(CollegeDebateInfo debate) => $"For {debate.For}  Against {debate.Against}  Undecided {debate.Undecided}";

    public static string CountsWithGains(CollegeDebateInfo debate)
        => $"For {debate.For} ({Gain(debate.For - debate.OpeningFor)})  Ag. {debate.Against} ({Gain(debate.Against - debate.OpeningAgainst)})  Und. {debate.Undecided}";

    public static string Opening(CollegeDebateInfo debate)
        => $"Opening: For {debate.OpeningFor}, Against {debate.OpeningAgainst}, Undecided {debate.OpeningUndecided}";

    public static string Gain(int gain) => gain > 0 ? $"+{gain}" : gain.ToString(CultureInfo.InvariantCulture);

    /// <summary>Who would win if the vote ended now.</summary>
    public static DebateResult Leading(CollegeDebateInfo debate)
    {
        var forGain = debate.For - debate.OpeningFor;
        var againstGain = debate.Against - debate.OpeningAgainst;

        return forGain > againstGain ? DebateResult.For : againstGain > forGain ? DebateResult.Against : DebateResult.Draw;
    }

    public static string ResultLine(DebateResult result, CollegeDebateInfo debate)
        => result switch
        {
            DebateResult.For     => $"For wins ({Gain(debate.For - debate.OpeningFor)})",
            DebateResult.Against => $"Against wins ({Gain(debate.Against - debate.OpeningAgainst)})",
            DebateResult.Draw    => "A draw.",
            _                    => string.Empty
        };

    public static string FloorLine(string floor, int secondsLeft) => floor.Length == 0 ? "Floor: open" : $"Floor: {floor} {Clock(secondsLeft)} left";

    public static string VoteCaption(DebatePhase phase, int secondsLeft)
        => phase == DebatePhase.Opening ? $"Opening vote: {Clock(secondsLeft)}" : $"Final vote: {Clock(secondsLeft)}";

    /// <summary>
    ///     The side a vote card shows as chosen. A final vote counts only when it is sent during the final vote, so the side
    ///     carried over from earlier shows only once one was sent.
    /// </summary>
    public static DebateSide ShownVote(DebatePhase phase, DebateSide mySide, bool sentInFinal)
        => (phase == DebatePhase.Final) && !sentInFinal ? DebateSide.None : mySide;

    public static string HandsMore(int shown, int total) => total > shown ? $"+{total - shown} more" : string.Empty;

    /// <summary>The label over a player's head. The game font has no star, so the floor holder gets " *".</summary>
    public static string Marker(DebateSide side, bool floor)
    {
        var text = side switch
        {
            DebateSide.For       => "For",
            DebateSide.Against   => "Against",
            DebateSide.Undecided => "?",
            _                    => string.Empty
        };

        return floor && (text.Length > 0) ? text + " *" : text;
    }
}
