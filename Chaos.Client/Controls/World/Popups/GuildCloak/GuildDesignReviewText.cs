#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>The review window's per-entry choices, kept apart from the window so they can be tested.</summary>
public static class GuildDesignReviewText
{
    /// <summary>The list button's caption: the kind, then the guild.</summary>
    public static string EntryCaption(GuildCloakReviewEntry entry)
        => $"{(entry.Kind == GuildDesignKind.Emblem ? "Emblem" : "Cloak")} {entry.GuildName}";

    /// <summary>True when the entry is an emblem, so the window shows the emblem view instead of the cloak views.</summary>
    public static bool ShowsEmblem(GuildCloakReviewEntry? entry) => entry is { Kind: GuildDesignKind.Emblem };
}
