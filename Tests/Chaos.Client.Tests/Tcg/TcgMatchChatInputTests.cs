#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Definitions;
using Chaos.Client.ViewModel;
using FluentAssertions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Tests;

/// <summary>
///     Drives the real <see cref="InputDispatcher" /> through the match-chat script: Enter,
///     type "hi", Enter, type "yo", Enter. All key and text events are queued into one frame,
///     the way fast input arrives between two game updates. The match window itself is a
///     stand-in: the real TcgMatchWindow cannot be constructed headless (its buttons build
///     GPU textures in their constructor), so <see cref="StandInMatchWindow" /> copies
///     TcgMatchWindow.OnKeyDown line for line and holds a real <see cref="TcgMatchInput" />.
///     A second "HUD" textbox registered as <see cref="InputDispatcher.ChatInputTextBox" />
///     stands in for the world chat box, and the root panel answers any Enter that bubbles
///     past the window the way FocusChat ("Type in Chat") used to when it stayed usable over
///     popups - so a bubble-through would focus the HUD box and show up there.
/// </summary>
[NotInParallel]
public sealed class TcgMatchChatInputTests
{
    private sealed class FakeRoot : UIPanel
    {
        public FakeRoot()
        {
            Width = 640;
            Height = 480;
            AddChild(HudBox);
        }

        public CustomTextBox HudBox { get; } = new()
        {
            X = 8,
            Y = 440,
            Width = 300,
            Height = 20
        };

        public int FocusChatHits { get; private set; }

        public override void OnKeyDown(KeyDownEvent e)
        {
            if ((e.Keycode == Keycode.Enter) && !e.Alt)
            {
                FocusChatHits++;
                HudBox.IsFocused = true;
                e.Handled = true;
            }
        }
    }

    private sealed class StandInMatchWindow : UIPanel
    {
        public StandInMatchWindow()
        {
            X = 0;
            Y = 0;
            Width = 640;
            Height = 480;
            ZIndex = 60;
            IsModal = true;
            UsesControlStack = true;
            Visible = false;
            Input = new TcgMatchInput(_ => null);
            SayBox = new CustomTextBox
            {
                X = 8,
                Y = 400,
                Width = 300,
                Height = 20,
                MaxLength = 80
            };
            AddChild(SayBox);
        }

        public CustomTextBox SayBox { get; }
        public TcgMatchInput Input { get; }

        public event Action<string>? WhisperSent;

        public void Show()
        {
            Visible = true;
            InputDispatcher.Instance?.PushControl(this);
        }

        //exact copy of TcgMatchWindow.OnKeyDown (post-fix): Esc unfocuses the chat box
        //first and only cancels once it is unfocused; Enter sends or focuses the box.
        public override void OnKeyDown(KeyDownEvent e)
        {
            if (e.Keycode == Keycode.Escape)
            {
                if (SayBox.IsFocused)
                {
                    SayBox.IsFocused = false;

                    if (InputDispatcher.Instance?.ExplicitFocus == SayBox)
                        InputDispatcher.Instance.ClearExplicitFocus();
                } else
                    Input.Cancel();

                e.Handled = true;

                return;
            }

            if (e.Keycode == Keycode.Enter)
            {
                if (SayBox.IsFocused && (SayBox.Text.Length > 0))
                {
                    WhisperSent?.Invoke(SayBox.Text);
                    SayBox.Text = string.Empty;
                } else
                    SayBox.IsFocused = true;

                e.Handled = true;

                return;
            }

            base.OnKeyDown(e);
        }
    }

    private static BufferedInputEvent KeyDown(Scancode scancode, Keycode keycode)
        => BufferedInputEvent.ForKeyDown(scancode, keycode, KeyModifiers.None, false);

    private static BufferedInputEvent KeyUp(Scancode scancode, Keycode keycode)
        => BufferedInputEvent.ForKeyUp(scancode, keycode, KeyModifiers.None);

    private static BufferedInputEvent Text(char c) => BufferedInputEvent.ForTextInput(c);

    private static void Press(InputDispatcher dispatcher, FakeRoot root, Scancode scancode, Keycode keycode)
    {
        InputBuffer.QueueForTests(KeyDown(scancode, keycode));
        InputBuffer.QueueForTests(KeyUp(scancode, keycode));
        InputBuffer.DrainForTests();
        dispatcher.ProcessInput(root, new GameTime());
    }

    private static (InputDispatcher Dispatcher, FakeRoot Root, StandInMatchWindow Window, List<string> Sends) OpenMatch()
    {
        InputBuffer.ResetForTests();

        var dispatcher = new InputDispatcher();
        var root = new FakeRoot();
        dispatcher.ChatInputTextBox = root.HudBox;

        var window = new StandInMatchWindow();
        root.AddChild(window);

        var sends = new List<string>();
        window.WhisperSent += sends.Add;
        window.Show();

        return (dispatcher, root, window, sends);
    }

    private static void CloseMatch(InputDispatcher dispatcher, FakeRoot root, StandInMatchWindow window)
    {
        window.SayBox.IsFocused = false;
        root.HudBox.IsFocused = false;
        dispatcher.ClearExplicitFocus();
        InputBuffer.ResetForTests();
    }

    [Test]
    public void TwoMessagesInARowBothSend_WithNoHudLeak_StandInWindow()
    {
        var (dispatcher, root, window, sends) = OpenMatch();

        try
        {
            //one frame on purpose: Enter focus plus the typed characters can share a
            //frame when they land within the same update, so the first character must
            //survive even then.
            InputBuffer.QueueForTests(KeyDown(Scancode.Enter, Keycode.Enter));
            InputBuffer.QueueForTests(KeyUp(Scancode.Enter, Keycode.Enter));
            InputBuffer.QueueForTests(KeyDown(Scancode.H, Keycode.H));
            InputBuffer.QueueForTests(Text('h'));
            InputBuffer.QueueForTests(KeyUp(Scancode.H, Keycode.H));
            InputBuffer.QueueForTests(KeyDown(Scancode.I, Keycode.I));
            InputBuffer.QueueForTests(Text('i'));
            InputBuffer.QueueForTests(KeyUp(Scancode.I, Keycode.I));
            InputBuffer.QueueForTests(KeyDown(Scancode.Enter, Keycode.Enter));
            InputBuffer.QueueForTests(KeyUp(Scancode.Enter, Keycode.Enter));
            InputBuffer.QueueForTests(KeyDown(Scancode.Y, Keycode.Y));
            InputBuffer.QueueForTests(Text('y'));
            InputBuffer.QueueForTests(KeyUp(Scancode.Y, Keycode.Y));
            InputBuffer.QueueForTests(KeyDown(Scancode.O, Keycode.O));
            InputBuffer.QueueForTests(Text('o'));
            InputBuffer.QueueForTests(KeyUp(Scancode.O, Keycode.O));
            InputBuffer.QueueForTests(KeyDown(Scancode.Enter, Keycode.Enter));
            InputBuffer.QueueForTests(KeyUp(Scancode.Enter, Keycode.Enter));
            InputBuffer.DrainForTests();
            dispatcher.ProcessInput(root, new GameTime());

            sends.Should().Equal("hi", "yo");
            window.SayBox.Text.Should().BeEmpty();
            window.SayBox.IsFocused.Should().BeTrue();
            root.HudBox.Text.Should().BeEmpty();
            root.HudBox.IsFocused.Should().BeFalse();
            root.FocusChatHits.Should().Be(0);
            dispatcher.ExplicitFocus.Should().BeSameAs(window.SayBox);
        } finally
        {
            CloseMatch(dispatcher, root, window);
        }
    }

    [Test]
    public void FirstEscUnfocuses_SecondEscCancels_StandInWindow()
    {
        var (dispatcher, root, window, _) = OpenMatch();

        try
        {
            window.Input.AskConcede();
            Press(dispatcher, root, Scancode.Enter, Keycode.Enter);
            dispatcher.ExplicitFocus.Should().BeSameAs(window.SayBox);

            Press(dispatcher, root, Scancode.Escape, Keycode.Escape);

            window.SayBox.IsFocused.Should().BeFalse();
            dispatcher.ExplicitFocus.Should().BeNull();
            window.Input.Mode.Should().Be(TcgInputMode.ConfirmConcede);

            Press(dispatcher, root, Scancode.Escape, Keycode.Escape);

            window.Input.Mode.Should().Be(TcgInputMode.Idle);
        } finally
        {
            CloseMatch(dispatcher, root, window);
        }
    }
}
