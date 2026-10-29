#if DEBUG
using Chaos.Client;
using Chaos.Client.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.TestHook;

[NotInParallel(nameof(InputBuffer))]
public sealed class InputInjectionTests
{
    [Before(Test)]
    public void Reset() => InputBuffer.ResetForTests();

    [Test]
    public void PinnedMouse_OverridesRealPosition()
    {
        InputBuffer.PinMouse(123, 45);
        InputBuffer.MouseX.Should().Be(123);
        InputBuffer.MouseY.Should().Be(45);

        InputBuffer.UnpinMouse();
        InputBuffer.IsMousePinned.Should().BeFalse();
    }

    [Test]
    public void InjectedKeyDown_IsSeenAsPressedAfterDrain()
    {
        InputBuffer.Inject(BufferedInputEvent.ForKeyDown(Scancode.Up, default, KeyModifiers.None, false));
        InputBuffer.DrainForTests();

        InputBuffer.WasScancodePressed(Scancode.Up).Should().BeTrue();
        InputBuffer.IsScancodeHeld(Scancode.Up).Should().BeTrue();
    }

    [Test]
    public void InjectedLeftPress_SetsHeldFlag_AndReleaseClearsIt()
    {
        InputBuffer.Inject(new BufferedInputEvent(BufferedInputKind.MouseButton, default, default, '\0',
            MouseButton.Left, true, 10, 10, 0, KeyModifiers.None));
        InputBuffer.IsLeftButtonHeld.Should().BeTrue();

        InputBuffer.Inject(new BufferedInputEvent(BufferedInputKind.MouseButton, default, default, '\0',
            MouseButton.Left, false, 10, 10, 0, KeyModifiers.None));
        InputBuffer.IsLeftButtonHeld.Should().BeFalse();
    }
}
#endif
