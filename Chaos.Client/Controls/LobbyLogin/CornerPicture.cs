namespace Chaos.Client.Controls.LobbyLogin;

/// <summary>
///     Picks the retail start screen's corner picture, one of lodbk00.epf to lodbk08.epf.
/// </summary>
public static class CornerPicture
{
    public const int COUNT = 9;

    public static string Pick(Random random) => $"lodbk0{random.Next(COUNT)}.epf";
}
