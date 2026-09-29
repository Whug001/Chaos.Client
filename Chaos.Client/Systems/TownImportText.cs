#region
using System.Globalization;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Systems;

/// <summary>Short text for the import windows.</summary>
public static class TownImportText
{
    /// <summary>How long until an import ends, in the two largest units.</summary>
    public static string TimeLeft(DateTime endsUtc, DateTime nowUtc)
    {
        var left = endsUtc - nowUtc;

        if (left < TimeSpan.FromMinutes(1))
            return "under a minute";

        return left.TotalDays >= 1 ? $"{(int)left.TotalDays}d {left.Hours}h" : $"{(int)left.TotalHours}h {left.Minutes}m";
    }

    /// <summary>One line describing a finished import.</summary>
    public static string PastLine(TownImportPastInfo past)
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"{past.Name}: {past.Sold:N0} sold, {past.Gold:N0} gold");

        return past.Reason switch
        {
            TownImportEndReason.Vetoed       => line + " (vetoed)",
            TownImportEndReason.EndedByAdmin => line + " (ended early)",
            _                                => line
        };
    }

    /// <summary>A gold price with thousands separators.</summary>
    public static string PriceText(int price) => price.ToString("N0", CultureInfo.InvariantCulture);
}
