#if DEBUG
namespace Chaos.Client.Systems.TestHook;

public static class TestStateRegistry
{
    private static readonly Dictionary<string, Func<object?>> Providers = new();
    private static readonly Lock Gate = new();

    public static void Register(string key, Func<object?> getter)
    {
        using var scope = Gate.EnterScope();
        Providers[key] = getter;
    }

    public static Dictionary<string, object?> Snapshot()
    {
        using var scope = Gate.EnterScope();
        var result = new Dictionary<string, object?>();
        foreach (var (key, getter) in Providers)
        {
            try { result[key] = getter(); }
            catch (Exception ex) { result[key] = $"error: {ex.Message}"; }
        }
        return result;
    }

    internal static void ClearForTests()
    {
        using var scope = Gate.EnterScope();
        Providers.Clear();
    }
}
#endif
