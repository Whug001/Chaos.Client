using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Systems;

/// <summary>Pure rules for sending an in-game bug report: when Send is allowed, and how the picture is split.</summary>
public static class BugReportUpload
{
    /// <summary>True when a category is picked, the trimmed title is 5 to 80 characters and the trimmed text is 10 to 1,000.</summary>
    public static bool CanSend(BugReportCategory? category, string title, string text)
    {
        if (category is null)
            return false;

        var titleLength = title.Trim().Length;

        if (titleLength is < BugReportProtocol.MIN_TITLE_CHARS or > BugReportProtocol.MAX_TITLE_CHARS)
            return false;

        var length = text.Trim().Length;

        return length is >= BugReportProtocol.MIN_DESCRIPTION_CHARS and <= BugReportProtocol.MAX_DESCRIPTION_CHARS;
    }

    /// <summary>Splits the picture into parts of at most <see cref="BugReportProtocol.PART_SIZE" /> bytes, in order.</summary>
    public static IReadOnlyList<byte[]> SplitPicture(byte[] picture) => picture.Chunk(BugReportProtocol.PART_SIZE).ToList();

    /// <summary>Cuts a client detail string to <see cref="BugReportProtocol.MAX_DETAIL_CHARS" /> characters.</summary>
    public static string Clip(string value)
        => value.Length <= BugReportProtocol.MAX_DETAIL_CHARS ? value : value[..BugReportProtocol.MAX_DETAIL_CHARS];
}
