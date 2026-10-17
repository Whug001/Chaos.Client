#region
using System.Globalization;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>Challenge window logic with no drawing: builds the actions and checks what the player typed.</summary>
public static class TcgChallengeState
{
    public const int MINIMUM_GOLD = 1000;

    public static TcgActionArgs AcceptAction(string deckName)
        => new() { Type = TcgActionType.ChallengeAccept, DeckName = deckName };

    public static TcgActionArgs CancelAction() => new() { Type = TcgActionType.ChallengeCancel };

    public static TcgActionArgs DeclineAction() => new() { Type = TcgActionType.ChallengeDecline };

    /// <summary>Whole seconds left on an offer that had <paramref name="expiresMs" /> left when it arrived.</summary>
    public static int SecondsLeft(uint expiresMs, DateTime receivedAt, DateTime now)
        => (int)Math.Max(0, Math.Ceiling((expiresMs - (now - receivedAt).TotalMilliseconds) / 1000));

    public static TcgActionArgs SendAction(string targetName, string deckName, TcgStakes stakes, int gold)
        => new()
        {
            Type = TcgActionType.ChallengeSend,
            TargetName = targetName,
            DeckName = deckName,
            Stakes = stakes,
            Gold = stakes == TcgStakes.Gold ? gold : 0
        };

    public static string StakesText(TcgStakes stakes, int gold)
        => stakes switch
        {
            TcgStakes.Gold => $"{gold.ToString("N0", CultureInfo.InvariantCulture)} gold each",
            TcgStakes.Ante => "One card from each deck",
            _              => "No stakes"
        };

    public static TcgActionArgs StartAction(string targetName)
        => new() { Type = TcgActionType.ChallengeStart, TargetName = targetName };

    public static bool TryParseGold(string text, out int gold, out string error)
    {
        gold = 0;
        error = string.Empty;

        if (!int.TryParse(text.Trim().Replace(",", string.Empty), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            error = "Type the gold amount.";

            return false;
        }

        if (value < MINIMUM_GOLD)
        {
            error = $"A gold stake must be at least {MINIMUM_GOLD.ToString("N0", CultureInfo.InvariantCulture)} gold.";

            return false;
        }

        gold = value;

        return true;
    }
}
