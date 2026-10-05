using Chaos.Client.Controls.World.Popups.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class CollegeTiersTests
{
    private static CollegeVoteInfo Vote(string judge, CollegeTierCode tier, string comment = "")
        => new()
        {
            Judge = judge,
            Tier = tier,
            Comment = comment
        };

    [Test]
    public void Names_cover_no_vote_and_no_award()
    {
        CollegeTiers.Name(CollegeProtocol.NO_TIER).Should().Be("-");
        CollegeTiers.Name((byte)CollegeTierCode.None).Should().Be("No award");
        CollegeTiers.Name((byte)CollegeTierCode.Kingdom).Should().Be("Kingdom");
    }

    [Test]
    public void The_summary_counts_votes_by_tier_highest_first()
    {
        List<CollegeVoteInfo> votes =
        [
            Vote("A", CollegeTierCode.Village),
            Vote("B", CollegeTierCode.Kingdom),
            Vote("C", CollegeTierCode.Village),
            Vote("D", CollegeTierCode.None)
        ];

        CollegeTiers.VoteSummary(votes).Should().Be("Kingdom x1, Village x2, No award x1");
        CollegeTiers.VoteSummary([]).Should().Be("none yet");
    }

    [Test]
    public void The_votes_text_puts_each_comment_under_its_vote()
    {
        List<CollegeVoteInfo> votes = [Vote("Aroha", CollegeTierCode.Clave, "Lovely."), Vote("Bryn", CollegeTierCode.Aisling)];

        CollegeTiers.VotesText(votes).Should().Be("Aroha: Clave\nLovely.\n\nBryn: Aisling");
        CollegeTiers.VotesText([]).Should().Be("No judge has voted.");
    }
}
