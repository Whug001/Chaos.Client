namespace Chaos.Client.Networking.Arena;

internal static class ArenaOpCodes
{
    public const byte ArenaPoll = 124; // server -> client
    public const byte ArenaVote = 122; // client -> server
}
