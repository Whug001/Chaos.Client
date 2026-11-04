namespace Chaos.Client.Controls.LobbyLogin;

/// <summary>
///     Picks the layout a lobby screen loads: the retail one when the classic look is on, otherwise the current one.
/// </summary>
public static class LoginLayouts
{
    private static readonly Dictionary<string, string> CLASSIC = new(StringComparer.OrdinalIgnoreCase)
    {
        ["_nstart"] = "lmain",
        ["_nlogin"] = "llogin",
        ["_nsvr"] = "lsvr",
        ["_npw"] = "lpasswd",
        ["_nagree"] = "lagre",
        ["_ncreate"] = "lcreate"
    };

    public static string Resolve(string current, bool classic)
        => classic && CLASSIC.TryGetValue(current, out var retail) ? retail : current;

    public static string For(string current) => Resolve(current, GlobalSettings.ClassicLogin);

    /// <summary>True when the server list layout is the retail one that builds a stacked box.</summary>
    public static bool StacksServers(string layout) => string.Equals(layout, "lsvr", StringComparison.OrdinalIgnoreCase);
}
