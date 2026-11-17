#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.Learn;

/// <summary>The three answers the learn-ability popup can give.</summary>
public enum LearnAbilityButton
{
    Learn,
    GuildFunds,
    Cancel
}

/// <summary>
///     What the learn-ability popup is answering, kept apart from its drawing so the button-to-option mapping and the
///     closing rules can be tested. Each button answers with a 0-based position in the NPC dialog's option list, the same
///     index a click on that option gives; <see cref="LearnAbilityDisplayArgs.NoOption" /> means the button is not offered.
/// </summary>
public sealed class LearnAbilityPopupState
{
    private LearnAbilityDisplayArgs? Args;

    /// <summary>Whether the popup is up and its buttons may answer.</summary>
    public bool IsOpen => Args is not null;

    /// <summary>What the popup shows, or null when it is closed.</summary>
    public LearnAbilityView? View { get; private set; }

    /// <summary>Opens the popup for <paramref name="args" />, replacing whatever it showed.</summary>
    public LearnAbilityView Open(LearnAbilityDisplayArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Args = args;
        View = LearnAbilityView.From(args);

        return View;
    }

    /// <summary>Closes the popup without answering. Returns whether it was open.</summary>
    public bool Close()
    {
        var wasOpen = IsOpen;
        Args = null;
        View = null;

        return wasOpen;
    }

    /// <summary>Whether <paramref name="button" /> is offered by the open popup.</summary>
    public bool Offers(LearnAbilityButton button) => OptionFor(button) is not null;

    /// <summary>
    ///     Presses <paramref name="button" />: closes the popup and returns the dialog option to select, or null when the
    ///     popup is closed (a new dialog already replaced it) or the button is not offered. The popup is closed either way.
    /// </summary>
    public int? Press(LearnAbilityButton button)
    {
        var option = OptionFor(button);
        Close();

        return option;
    }

    private int? OptionFor(LearnAbilityButton button)
    {
        if (Args is null)
            return null;

        var option = button switch
        {
            LearnAbilityButton.Learn      => Args.LearnOption,
            LearnAbilityButton.GuildFunds => Args.GuildFundsOption,
            _                             => Args.CancelOption
        };

        return option == LearnAbilityDisplayArgs.NoOption ? null : option;
    }
}
