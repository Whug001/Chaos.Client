#if DEBUG
using Chaos.Client;
using Chaos.Client.Definitions;
using Chaos.Client.Systems.TestHook;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.TestHook;

[NotInParallel(nameof(TestStateRegistry))]
public class TestHookExecutorTests
{
    private sealed class FakeHost : ITestHookHost
    {
        public List<BufferedInputEvent> Injected { get; } = [];
        public (int X, int Y)? Pinned;
        public UiNode? Root;
        public Action<string?>? PendingShot;

        public bool TryResolveKey(string name, out BufferedInputEvent down, out BufferedInputEvent up)
        {
            down = new BufferedInputEvent(BufferedInputKind.KeyDown, default, default, name[0], default, true, 0, 0, 0, default);
            up = down with { Kind = BufferedInputKind.KeyUp, IsPress = false };
            return name != "Nope";
        }

        public void Inject(BufferedInputEvent evt) => Injected.Add(evt);
        public void PinMouse(int x, int y) => Pinned = (x, y);
        public UiNode? GetWorldRoot() => Root;
        public int MeasureText(string text) => text.Length * 6;
        public IReadOnlyDictionary<string, object?> GetBaseState() => new Dictionary<string, object?> { ["loggedIn"] = Root is not null };
        public void RequestScreenshot(string path, Action<string?> onDone) => PendingShot = onDone;
    }

    [Before(Test)]
    public void Reset() => TestStateRegistry.ClearForTests();

    private static Task<string> Run(TestHookExecutor exec, string line)
    {
        TestCommandParser.TryParse(line, out var cmd, out _).Should().BeTrue();
        return exec.Enqueue(cmd!);
    }

    [Test]
    public async Task KeyTap_ReleasesAfterFrames()
    {
        var host = new FakeHost();
        var exec = new TestHookExecutor(host);
        var reply = Run(exec, "key Up tap 3");

        exec.Tick();
        host.Injected.Should().ContainSingle(e => e.Kind == BufferedInputKind.KeyDown);
        exec.Tick(); exec.Tick();
        reply.IsCompleted.Should().BeFalse();
        exec.Tick();

        host.Injected.Last().Kind.Should().Be(BufferedInputKind.KeyUp);
        (await reply).Should().Contain("\"ok\":true");
    }

    [Test]
    public async Task UnknownKey_ReturnsError()
    {
        var exec = new TestHookExecutor(new FakeHost());
        var reply = Run(exec, "key Nope");
        exec.Tick();
        (await reply).Should().Contain("\"ok\":false").And.Contain("Nope");
    }

    [Test]
    public async Task Click_PinsMouse_PressesThenReleases()
    {
        var host = new FakeHost();
        var exec = new TestHookExecutor(host);
        var reply = Run(exec, "click 50 60");

        exec.Tick();
        host.Pinned.Should().Be((50, 60));
        host.Injected.Should().ContainSingle(e => e.Kind == BufferedInputKind.MouseButton && e.IsPress);
        exec.Tick();

        host.Injected.Last().IsPress.Should().BeFalse();
        (await reply).Should().Contain("\"ok\":true");
    }

    [Test]
    public async Task Type_InjectsOneTextEventPerChar()
    {
        var host = new FakeHost();
        var exec = new TestHookExecutor(host);
        var reply = Run(exec, "type hi");
        exec.Tick();

        host.Injected.Select(e => e.Character).Should().Equal('h', 'i');
        (await reply).Should().Contain("\"ok\":true");
    }

    [Test]
    public async Task Screenshot_WaitsForDrawCallback()
    {
        var host = new FakeHost();
        var exec = new TestHookExecutor(host);
        var reply = Run(exec, @"screenshot C:\x\a.png");

        exec.Tick();
        reply.IsCompleted.Should().BeFalse();
        host.PendingShot!(null);
        exec.Tick();
        (await reply).Should().Contain("\"ok\":true");
    }

    [Test]
    public async Task StateAndPanels_BeforeLogin_AreEmptyNotErrors()
    {
        var exec = new TestHookExecutor(new FakeHost());
        var state = Run(exec, "state");
        var panels = Run(exec, "panels");
        exec.Tick(); exec.Tick();

        (await state).Should().Contain("\"loggedIn\":false");
        (await panels).Should().Be("{\"ok\":true,\"data\":[]}");
    }

    [Test]
    public async Task State_IncludesRegisteredProviders()
    {
        TestStateRegistry.Register("kart.lap", () => 2);
        var exec = new TestHookExecutor(new FakeHost());
        var reply = Run(exec, "state");
        exec.Tick();
        (await reply).Should().Contain("\"kart.lap\":2");
    }

    [Test]
    public async Task State_WithoutFilter_ExcludesKartTrack()
    {
        TestStateRegistry.Register("kart.lap", () => 2);
        TestStateRegistry.Register("kart.track", () => new { key = "t" });
        var exec = new TestHookExecutor(new FakeHost());
        var reply = Run(exec, "state");
        exec.Tick();
        var json = await reply;
        json.Should().Contain("\"kart.lap\":2");
        json.Should().NotContain("kart.track");
    }

    [Test]
    public async Task State_WithPrefixFilter_ReturnsOnlyMatches()
    {
        TestStateRegistry.Register("kart.lap", () => 2);
        TestStateRegistry.Register("kart.track", () => new { key = "t" });
        var exec = new TestHookExecutor(new FakeHost());
        var prefix = Run(exec, "state kart.");
        var exact = Run(exec, "state kart.track");
        exec.Tick(); exec.Tick();
        var prefixJson = await prefix;
        prefixJson.Should().Contain("\"kart.lap\":2");
        prefixJson.Should().NotContain("kart.track");
        prefixJson.Should().NotContain("loggedIn");
        var exactJson = await exact;
        exactJson.Should().Contain("kart.track");
        exactJson.Should().NotContain("kart.lap");
    }

    [Test]
    public async Task Panels_ReturnsInspectorReport()
    {
        var host = new FakeHost
        {
            Root = new UiNode("WorldRoot", "WorldRoot", new Rectangle(0, 0, 640, 480), true, UiNodeKind.Panel, null, false,
                [new UiNode("Shop", "ShopControl", new Rectangle(10, 10, 100, 100), true, UiNodeKind.Panel, null, false, [])])
        };
        var exec = new TestHookExecutor(host);
        var reply = Run(exec, "panels");
        exec.Tick();
        (await reply).Should().Contain("\"Path\":\"Shop\"");
    }

    [Test]
    public async Task WaitFrames_CompletesAfterCount()
    {
        var exec = new TestHookExecutor(new FakeHost());
        var reply = Run(exec, "wait-frames 2");
        exec.Tick();
        reply.IsCompleted.Should().BeFalse();
        exec.Tick();
        (await reply).Should().Contain("\"ok\":true");
    }

    [Test]
    public void Screenshot_ReadFrameFails_NotifiesAllAndClearsPending()
    {
        var host = new ChaosGameTestHost(null!);
        var errors = new List<string?>();
        host.RequestScreenshot(@"C:\x\a.png", errors.Add);
        host.RequestScreenshot(@"C:\x\b.png", errors.Add);

        host.ServiceScreenshots(() => throw new InvalidOperationException("frame failed"));

        errors.Should().Equal("frame failed", "frame failed");
        var readAgain = false;
        host.ServiceScreenshots(() => { readAgain = true; throw new InvalidOperationException("unexpected read"); });
        readAgain.Should().BeFalse();
        errors.Should().HaveCount(2);
    }

    [Test]
    public async Task Screenshot_TimesOutAfter600Frames_AndQueueRecovers()
    {
        var host = new FakeHost();
        var exec = new TestHookExecutor(host);
        var reply = Run(exec, @"screenshot C:\x\a.png");

        for (var i = 0; i < 600; i++)
            exec.Tick();

        (await reply).Should().Contain("screenshot timed out");

        var ping = Run(exec, "ping");
        exec.Tick();
        (await ping).Should().Contain("pong");

        ((Action)(() => host.PendingShot!(null))).Should().NotThrow();
        (await reply).Should().Contain("screenshot timed out");
    }
}
#endif
