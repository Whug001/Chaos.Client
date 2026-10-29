#if DEBUG
using Chaos.Client;

namespace Chaos.Client.Systems.TestHook;

public interface ITestHookHost
{
    bool TryResolveKey(string name, out BufferedInputEvent down, out BufferedInputEvent up);
    void Inject(BufferedInputEvent evt);
    void PinMouse(int x, int y);
    UiNode? GetWorldRoot();
    int MeasureText(string text);
    IReadOnlyDictionary<string, object?> GetBaseState();
    void RequestScreenshot(string path, Action<string?> onDone);
}
#endif
