#region
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>Award tier names, badge colours and the vote lines the College windows show.</summary>
public static class CollegeTiers
{
    /// <summary>The tiers a judge or the Director can pick, in button order.</summary>
    public static readonly CollegeTierCode[] Votable =
        [CollegeTierCode.None, CollegeTierCode.Clave, CollegeTierCode.Village, CollegeTierCode.Kingdom, CollegeTierCode.Aisling];

    public static string Name(CollegeTierCode tier) => tier == CollegeTierCode.None ? "No award" : tier.ToString();

    public static string Name(byte tier) => tier == CollegeProtocol.NO_TIER ? "-" : Name((CollegeTierCode)tier);

    public static Color Badge(CollegeTierCode tier)
        => tier switch
        {
            CollegeTierCode.Clave   => new Color(205, 140, 82),
            CollegeTierCode.Village => new Color(200, 204, 212),
            CollegeTierCode.Kingdom => new Color(236, 200, 80),
            CollegeTierCode.Aisling => new Color(156, 204, 240),
            _                       => LegendColors.Gray
        };

    /// <summary>The votes counted by tier, highest first ("Kingdom x1, Village x3"), or "none yet".</summary>
    public static string VoteSummary(IReadOnlyCollection<CollegeVoteInfo> votes)
        => votes.Count == 0
            ? "none yet"
            : string.Join(
                ", ",
                votes.GroupBy(v => v.Tier)
                     .OrderByDescending(g => g.Key)
                     .Select(g => $"{Name(g.Key)} x{g.Count()}"));

    /// <summary>Every vote as "Judge: Tier", each with its comment on the lines under it, for the votes popup.</summary>
    public static string VotesText(IReadOnlyCollection<CollegeVoteInfo> votes)
        => votes.Count == 0
            ? "No judge has voted."
            : string.Join(
                "\n\n",
                votes.Select(v => v.Comment.Length > 0 ? $"{v.Judge}: {Name(v.Tier)}\n{v.Comment}" : $"{v.Judge}: {Name(v.Tier)}"));
}
