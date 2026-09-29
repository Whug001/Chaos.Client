using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildDesignReviewTextTests
{
    [Test]
    public void Entries_are_labeled_by_kind_and_only_emblems_show_the_emblem_view()
    {
        var cloak = new GuildCloakReviewEntry { Kind = GuildDesignKind.Cloak, GuildName = "Moonveil" };
        var emblem = new GuildCloakReviewEntry { Kind = GuildDesignKind.Emblem, GuildName = "Richards" };

        GuildDesignReviewText.EntryCaption(cloak).Should().Be("Cloak Moonveil");
        GuildDesignReviewText.EntryCaption(emblem).Should().Be("Emblem Richards");
        GuildDesignReviewText.ShowsEmblem(emblem).Should().BeTrue();
        GuildDesignReviewText.ShowsEmblem(cloak).Should().BeFalse();
        GuildDesignReviewText.ShowsEmblem(null).Should().BeFalse();
    }
}
