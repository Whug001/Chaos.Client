#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Data.Models;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Hud;

public sealed class ChatInputControl : UIPanel
{
    private const int SAY_MAX_LENGTH = 255;

    //the whisper packet the group/guild channels ride on is dropped server-side past this length.
    private const int WHISPER_MAX_LENGTH = 100;

    //the server routes a whisper aimed at these names into the group/guild channel.
    private const string GROUP_CHANNEL = "!group";
    private const string GUILD_CHANNEL = "!guild";

    private static readonly Color FocusedBackground = new(0, 0, 0, 160);

    private readonly int FullWidth;
    private readonly UILabel PrefixLabel;
    private readonly UITextBox TextBox;

    //a snapshot of the names up/down cycles — targets for /w, senders for /r. copied so an
    //incoming whisper reordering the live list can't shift the name out from under the index.
    private string[] WhisperNames = [];

    private Action<string>? PromptCallback;
    private int WhisperNameIndex;
    private string? WhisperTarget;

    public ChatMode Mode { get; private set; }
    public bool IsFocused => TextBox.IsFocused;

    public ChatInputControl(ControlPrefabSet prefabSet)
    {
        Name = "ChatInput";

        var rect = PrefabPanel.GetRect(prefabSet, "SAY");
        X = rect.X;
        Y = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
        FullWidth = rect.Width;

        PrefixLabel = new UILabel
        {
            Name = "ChatPrefix",
            X = 0,
            Y = 0,
            Width = 0,
            Height = rect.Height,
            BackgroundColor = Color.Black,
            PaddingLeft = 1,
            PaddingTop = 1,
            TruncateWithEllipsis = false,
            Visible = false
        };

        AddChild(PrefixLabel);

        TextBox = new UITextBox
        {
            Name = "ChatTextBox",
            X = 0,
            Y = 0,
            Width = rect.Width,
            Height = rect.Height,
            MaxLength = SAY_MAX_LENGTH,
            PaddingLeft = 1,
            PaddingRight = 1,
            PaddingTop = 1,
            PaddingBottom = 1,
            FocusedBackgroundColor = FocusedBackground
        };

        //backspace with nothing left to delete backs out of the current channel. driven by the box
        //rather than polled, so a held backspace that empties the line can't also drop the channel.
        TextBox.EmptyBackspace += _ =>
        {
            if (Mode is ChatMode.Group or ChatMode.Guild or ChatMode.Shout)
                EnterChannel(ChatMode.Normal, false);
        };

        AddChild(TextBox);

        //a server dialog focusing its own textbox (or a hide) blurs this box without ever reaching
        //HandleEnter/HandleEscape. prompt (white background, short MaxLength) and ignore-select
        //(read-only) mutate the box, so they must tear down on any blur or that state leaks into the
        //next chat session. plain chat modes leave nothing behind and keep their half-typed message.
        TextBox.LostFocus += _ =>
        {
            if (Mode is ChatMode.Prompt or ChatMode.IgnoreModeSelect)
                Unfocus();
        };

        //register the chat textbox so popups don't tear keyboard focus away while typing.
        if (InputDispatcher.Instance is { } dispatcher)
            dispatcher.ChatInputTextBox = TextBox;
    }

    //--- events ---

    public event MessageSentHandler? MessageSent;
    public event ShoutSentHandler? ShoutSent;
    public event WhisperSentHandler? WhisperSent;
    public event IgnoreAddedHandler? IgnoreAdded;
    public event IgnoreRemovedHandler? IgnoreRemoved;
    public event IgnoreListRequestedHandler? IgnoreListRequested;
    public event FocusChangedHandler? FocusChanged;

    //--- layout ---

    private void UpdateLayout(string prefix, Color color)
    {
        if (prefix.Length == 0)
        {
            PrefixLabel.Visible = false;
            TextBox.X = 0;
            TextBox.Width = FullWidth;

            return;
        }

        var prefixWidth = TextRenderer.MeasureWidth(prefix) + PrefixLabel.PaddingLeft;
        PrefixLabel.Text = prefix;
        PrefixLabel.ForegroundColor = color;
        PrefixLabel.Width = prefixWidth;
        PrefixLabel.Visible = true;

        TextBox.X = prefixWidth;
        TextBox.Width = FullWidth - prefixWidth;
    }

    //--- mode styling ---

    //the mode is the single input every other mode-specific detail is derived from. the screen names a
    //mode and the box dresses itself; nobody hands it a pre-built prefix, and it never reads one back apart.
    //Prompt is the one exception — its prefix is caller-supplied text, not a property of the mode.
    private (string Prefix, Color Color) ModeStyle(ChatMode mode)
        => mode switch
        {
            ChatMode.Group            => ("Group: ", TextColors.GroupChat),
            ChatMode.Guild            => ("Guild: ", TextColors.GuildChat),
            ChatMode.Shout            => ($"{WorldState.PlayerName}! ", TextColors.Shout),
            ChatMode.WhisperName      => ($"to [{SelectedWhisperName}]? ", TextColors.Whisper),
            ChatMode.WhisperMessage   => ($"-> {WhisperTarget}: ", TextColors.Whisper),
            ChatMode.IgnoreModeSelect => ("a: add, d: delete, ?: see list>", TextColors.Default),
            ChatMode.IgnoreAdd        => ("ID of people you wish to reject whisper >", TextColors.Default),
            ChatMode.IgnoreRemove     => ("ID of people you wish to cancel rejection of whisper >", TextColors.Default),
            _                         => ($"{WorldState.PlayerName}: ", Color.White)
        };

    //group, guild and whisper all ride the whisper packet, which the server drops past 100 chars.
    private static int MaxLengthFor(ChatMode mode)
        => mode is ChatMode.Group or ChatMode.Guild or ChatMode.WhisperMessage ? WHISPER_MAX_LENGTH : SAY_MAX_LENGTH;

    /// <summary>
    ///     Dresses the box for a mode — prefix, color and length limit all follow from it. Leaves the text alone, so it also
    ///     serves as a redraw when something the prefix is built from changes (the whisper name up/down is parked on).
    /// </summary>
    private void SetMode(ChatMode mode)
    {
        var (prefix, color) = ModeStyle(mode);

        Mode = mode;
        UpdateLayout(prefix, color);
        TextBox.ForegroundColor = color;
        TextBox.MaxLength = MaxLengthFor(mode);
    }

    /// <summary>
    ///     Switches the box to a mode, dropping whatever was typed in the previous one.
    /// </summary>
    private void EnterMode(ChatMode mode)
    {
        SetMode(mode);
        SetText(string.Empty, 0);
    }

    //--- focus methods ---

    private void FocusMode(ChatMode mode)
    {
        SetMode(mode);
        TextBox.IsFocused = true;
        FocusChanged?.Invoke(true);
    }

    /// <summary>
    ///     Opens the box for normal chat, or in whatever channel was locked in with a "/g&lt;enter&gt;"-style shortcut.
    /// </summary>
    //a channel lock persists until /s, even with no group/guild to talk to — parity with /gu, and the
    //server just drops the unreachable messages. no preemptive reset, or a fresh solo /g lock never sticks.
    public void Focus() => FocusMode(WorldState.Chat.StickyChannel);

    /// <summary>
    ///     Opens the box in shout, bypassing any locked channel.
    /// </summary>
    public void FocusShout() => FocusMode(ChatMode.Shout);

    /// <summary>
    ///     Opens the box composing a whisper to a known recipient — the world list and the aisling context menu.
    /// </summary>
    public void FocusWhisper(string name)
    {
        WhisperTarget = name;
        FocusMode(ChatMode.WhisperMessage);
    }

    /// <summary>
    ///     Opens the whisper target prompt, seeded with the people you last whispered.
    /// </summary>
    public void FocusWhisper()
    {
        BeginWhisper(WorldState.Chat.RecentWhisperTargets);
        TextBox.IsFocused = true;
        FocusChanged?.Invoke(true);
    }

    //--- channel shortcuts ---

    /// <summary>
    ///     Switches the already-focused box to another channel. Sticky switches also become the channel the box reopens in.
    /// </summary>
    private void EnterChannel(ChatMode mode, bool sticky)
    {
        if (sticky && (WorldState.Chat.StickyChannel != mode))
        {
            WorldState.Chat.StickyChannel = mode;

            //locking is invisible once the box closes, so say how to undo it.
            if (mode != ChatMode.Normal)
                WorldState.Chat.AddOrangeBarMessage($"Chat locked to {mode}. Type /s to return to say.");
        }

        EnterMode(mode);
    }

    /// <summary>
    ///     Opens the whisper target prompt. The names seed up/down: people you whispered for /w and the '#' hotkey, people
    ///     who whispered you for /r.
    /// </summary>
    private void BeginWhisper(IReadOnlyList<string> names)
    {
        WhisperNames = [..names];
        WhisperNameIndex = 0;

        EnterMode(ChatMode.WhisperName);
    }

    /// <summary>
    ///     Consumes a leading chat shortcut token. Sticky is true when the token was confirmed with enter rather than space.
    ///     <paramref name="carryBody" /> is false when the rest of the line must die with the token — a reply with nobody to
    ///     reply to would otherwise leave private text sitting in a say box.
    /// </summary>
    private bool TryEnterChannel(string token, bool sticky, out bool carryBody)
    {
        carryBody = true;

        //caps lock is how people shout, so the tokens can't be case-sensitive.
        switch (token.ToLowerInvariant())
        {
            case "/g":
                EnterChannel(ChatMode.Group, sticky);

                return true;

            case "/gu":
                EnterChannel(ChatMode.Guild, sticky);

                return true;

            case "/y":
                EnterChannel(ChatMode.Shout, sticky);

                return true;

            case "/s":
                EnterChannel(ChatMode.Normal, sticky);

                return true;

            case "/w":
                BeginWhisper(WorldState.Chat.RecentWhisperTargets);

                return true;

            case "/r":
                if (WorldState.Chat.RecentWhisperSenders.Count == 0)
                {
                    WorldState.Chat.AddOrangeBarMessage("No one has whispered you.");
                    SetText(string.Empty, 0);
                    carryBody = false;

                    return true;
                }

                BeginWhisper(WorldState.Chat.RecentWhisperSenders);

                return true;

            default:
                return false;
        }
    }

    public void FocusIgnore()
    {
        FocusMode(ChatMode.IgnoreModeSelect);
        TextBox.IsReadOnly = true;
    }

    public void ShowPrompt(string prefix, int maxLength, Action<string> onConfirm)
    {
        PromptCallback = onConfirm;

        TextBox.MaxLength = maxLength;
        TextBox.FocusedBackgroundColor = Color.White;
        TextBox.BackgroundColor = Color.White;
        TextBox.ForegroundColor = Color.Black;

        Mode = ChatMode.Prompt;
        PrefixLabel.BackgroundColor = Color.White;
        UpdateLayout(prefix, Color.Black);
        SetText(string.Empty, 0);
        TextBox.IsFocused = true;
        FocusChanged?.Invoke(true);
    }

    public void Unfocus()
    {
        //every exit path routes through here — a prompt left un-restored keeps the box white and length-capped.
        if (Mode == ChatMode.Prompt)
            RestoreFromPrompt();

        Mode = ChatMode.None;
        WhisperTarget = null;
        WhisperNames = [];
        TextBox.IsReadOnly = false;
        TextBox.IsFocused = false;
        TextBox.MaxLength = SAY_MAX_LENGTH;
        SetText(string.Empty, 0);
        TextBox.ForegroundColor = Color.White;
        UpdateLayout(string.Empty, Color.White);

        //only release keyboard routing if it's still ours — a blur caused by another textbox
        //taking focus must not clear the focus that box is about to claim.
        if (InputDispatcher.Instance?.ExplicitFocus == TextBox)
            InputDispatcher.Instance.ClearExplicitFocus();

        FocusChanged?.Invoke(false);
    }

    public void SetText(string text, int cursorPosition)
    {
        TextBox.Text = text;
        TextBox.CursorPosition = cursorPosition;
        TextBox.ClearSelection();
    }

    private void RestoreFromPrompt()
    {
        PromptCallback = null;
        TextBox.MaxLength = SAY_MAX_LENGTH;
        TextBox.FocusedBackgroundColor = FocusedBackground;
        TextBox.BackgroundColor = null;
        PrefixLabel.BackgroundColor = Color.Black;
    }

    //--- whisper history ---

    /// <summary>
    ///     The name up/down is currently parked on — the recipient a whisper is addressed to if nothing else is typed.
    /// </summary>
    private string SelectedWhisperName => WhisperNames.Length > 0 ? WhisperNames[WhisperNameIndex] : string.Empty;

    private void CycleWhisperTarget(int direction)
    {
        if ((WhisperNames.Length == 0) || (Mode != ChatMode.WhisperName))
            return;

        WhisperNameIndex = (WhisperNameIndex + direction + WhisperNames.Length) % WhisperNames.Length;
        SetMode(ChatMode.WhisperName);

        //the box already read up/down as home/end on the way here — put the caret back so a half-typed
        //name doesn't take the next keystroke at the front.
        SetText(TextBox.Text, TextBox.Text.Length);
    }

    //--- input handling ---

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Enter)
        {
            HandleEnter();
            e.Handled = true;

            return;
        }

        if (e.Keycode == Keycode.Escape)
        {
            HandleEscape();
            e.Handled = true;
        }
    }

    private void HandleEnter()
    {
        var message = TextBox.Text.Trim();

        //a shortcut alone on the line locks the box to that channel instead of sending it.
        if ((Mode is ChatMode.Normal or ChatMode.Group or ChatMode.Guild or ChatMode.Shout) && TryEnterChannel(message, true, out _))
            return;

        switch (Mode)
        {
            case ChatMode.Normal:
                MessageSent?.Invoke(message);
                Unfocus();

                break;

            case ChatMode.Group:
                SendToChannel(GROUP_CHANNEL, message);

                break;

            case ChatMode.Guild:
                SendToChannel(GUILD_CHANNEL, message);

                break;

            case ChatMode.Shout:
                ShoutSent?.Invoke(message);
                Unfocus();

                break;

            case ChatMode.IgnoreModeSelect:
                Unfocus();

                break;

            case ChatMode.IgnoreAdd:
                if (message.Length > 0)
                    IgnoreAdded?.Invoke(message);

                Unfocus();

                break;

            case ChatMode.IgnoreRemove:
                if (message.Length > 0)
                    IgnoreRemoved?.Invoke(message);

                Unfocus();

                break;

            case ChatMode.WhisperName:
                //a typed name wins over the one up/down is parked on.
                var targetName = message.Length > 0 ? message : SelectedWhisperName;

                if (targetName.Length > 0)
                {
                    WhisperTarget = targetName;
                    EnterMode(ChatMode.WhisperMessage);
                }

                break;

            case ChatMode.WhisperMessage:
                if (WhisperTarget is not null)
                {
                    WorldState.Chat.RecordWhisperTo(WhisperTarget);
                    WhisperSent?.Invoke(WhisperTarget, message);
                }

                Unfocus();

                break;

            case ChatMode.Prompt:
                var callback = PromptCallback;
                var text = TextBox.Text;
                Unfocus();
                callback?.Invoke(text);

                break;
        }
    }

    /// <summary>
    ///     Sends a composed line to the group or guild channel. Commands go out the public path instead — the server only
    ///     runs its command interceptor there, so a "/loc" typed in group chat would otherwise be broadcast verbatim.
    /// </summary>
    private void SendToChannel(string channel, string message)
    {
        if (message.StartsWith('/'))
            MessageSent?.Invoke(message);
        else if (message.Length > 0)
            WhisperSent?.Invoke(channel, message);

        Unfocus();
    }

    private void HandleEscape() => Unfocus();

    public override void OnTextInput(TextInputEvent e)
    {
        if (Mode != ChatMode.IgnoreModeSelect)
            return;

        switch (e.Character)
        {
            case 'a' or 'A':
                TextBox.IsReadOnly = false;
                EnterMode(ChatMode.IgnoreAdd);
                e.Handled = true;

                break;

            case 'd' or 'D':
                TextBox.IsReadOnly = false;
                EnterMode(ChatMode.IgnoreRemove);
                e.Handled = true;

                break;

            case '?':
                IgnoreListRequested?.Invoke();
                Unfocus();
                e.Handled = true;

                break;

            default:
                e.Handled = true;

                break;
        }
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!IsFocused)
            return;

        if (Mode is ChatMode.Normal or ChatMode.Group or ChatMode.Guild or ChatMode.Shout)
        {
            ConsumeShortcutToken();

            return;
        }

        if (Mode != ChatMode.WhisperName)
            return;

        if (InputBuffer.WasScancodePressed(Scancode.Up))
            CycleWhisperTarget(1);
        else if (InputBuffer.WasScancodePressed(Scancode.Down))
            CycleWhisperTarget(-1);
    }

    /// <summary>
    ///     Switches channel once a shortcut token has been closed with a space. The textbox consumes the space itself, so
    ///     the gesture is read back off the text rather than from the key event. Matching the token as the leading word (not
    ///     a trailing space) keeps it reliable when a frame delivers the space and the next character together.
    /// </summary>
    private void ConsumeShortcutToken()
    {
        var text = TextBox.Text;
        var space = text.IndexOf(' ');

        //only the leading word is ever a token, so "meet me /g" can't trip it.
        if ((space <= 0) || !TryEnterChannel(text[..space], false, out var carryBody))
            return;

        if (!carryBody)
            return;

        //whatever was typed past the token stays as the body of the message. SetText assigns Text
        //directly, so a pasted line has to be clipped to the new mode's limit by hand.
        var body = text[(space + 1)..];

        if (body.Length > TextBox.MaxLength)
            body = body[..TextBox.MaxLength];

        if (body.Length > 0)
            SetText(body, body.Length);
    }
}