namespace Chaos.Client.Networking.Titles;

internal static class TitleOpCodes
{
    public const byte TitleListRequest = 125; // client -> server
    public const byte TitleSelect = 126;      // client -> server
    public const byte TitleList = 125;        // server -> client
}
