#if DEBUG
using Chaos.Client.Collections;
using Chaos.Client.Screens;
using SkiaSharp;

namespace Chaos.Client.Systems.TestHook;

public sealed class ChaosGameTestHost(ChaosGame game) : ITestHookHost
{
    private static readonly Dictionary<string, string> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Return"] = nameof(Scancode.Enter),
        ["Enter"] = nameof(Scancode.Enter),
        ["Esc"] = nameof(Scancode.Escape),
        ["Backspace"] = nameof(Scancode.Back),
        ["0"] = nameof(Scancode.D0),
        ["1"] = nameof(Scancode.D1),
        ["2"] = nameof(Scancode.D2),
        ["3"] = nameof(Scancode.D3),
        ["4"] = nameof(Scancode.D4),
        ["5"] = nameof(Scancode.D5),
        ["6"] = nameof(Scancode.D6),
        ["7"] = nameof(Scancode.D7),
        ["8"] = nameof(Scancode.D8),
        ["9"] = nameof(Scancode.D9),
    };

    private readonly List<(string Path, Action<string?> OnDone)> PendingShots = [];

    public bool TryResolveKey(string name, out BufferedInputEvent down, out BufferedInputEvent up)
    {
        down = default;
        up = default;

        if (KeyAliases.TryGetValue(name, out var aliased))
            name = aliased;

        if (!Enum.TryParse<Scancode>(name, true, out var sc)
            && !Enum.TryParse<Scancode>($"SDL_SCANCODE_{name}", true, out sc))
            return false;

        var keycode = NormalizeKeycode(Sdl.SDL_GetKeyFromScancode((int)sc));
        down = BufferedInputEvent.ForKeyDown(sc, keycode, KeyModifiers.None, false);
        up = BufferedInputEvent.ForKeyUp(sc, keycode, KeyModifiers.None);
        return true;
    }

    private static Keycode NormalizeKeycode(int rawKeycode) => rawKeycode == ((1 << 30) | 88) ? Keycode.Enter : (Keycode)rawKeycode;

    public void Inject(BufferedInputEvent evt) => InputBuffer.Inject(evt);
    public void PinMouse(int x, int y) => InputBuffer.PinMouse(x, y);

    public UiNode? GetWorldRoot()
        => game.Screens.ActiveScreen is WorldScreen { Root: { } root } ? UiNode.From(root) : null;

    public int MeasureText(string text) => TextRenderer.MeasureWidth(text);

    public IReadOnlyDictionary<string, object?> GetBaseState()
    {
        var world = game.Screens.ActiveScreen as WorldScreen;
        var player = WorldState.GetPlayerEntity();
        return new Dictionary<string, object?>
        {
            ["loggedIn"] = world is not null && WorldState.PlayerEntityId != 0,
            ["screen"] = game.Screens.ActiveScreen?.GetType().Name,
            ["connection"] = game.Connection.State.ToString(),
            ["player"] = WorldState.PlayerName,
            ["map"] = WorldState.CurrentZoneName,
            ["mapId"] = world?.DebugMapId,
            ["x"] = player?.TileX,
            ["y"] = player?.TileY,
        };
    }

    public void RequestScreenshot(string path, Action<string?> onDone) => PendingShots.Add((path, onDone));

    // Called from ChaosGame.Draw while the 640x480 render target is still bound.
    public void ServiceScreenshots(Func<SKImage> readFrame)
    {
        if (PendingShots.Count == 0)
            return;

        try
        {
            using var image = readFrame();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);

            foreach (var (path, onDone) in PendingShots)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var tmp = path + ".tmp";
                    using (var fs = File.Create(tmp))
                        png.SaveTo(fs);
                    File.Move(tmp, path, overwrite: true);
                    onDone(null);
                }
                catch (Exception ex)
                {
                    onDone(ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            foreach (var (_, onDone) in PendingShots)
                onDone(ex.Message);
        }
        finally
        {
            PendingShots.Clear();
        }
    }
}
#endif
