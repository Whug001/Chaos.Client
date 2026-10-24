using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College.Library;

public static class ReasonRule
{
    public static bool Allows(string text, bool required)
    {
        var length = text.Trim().Length;

        return (length <= CollegeProtocol.MAX_REASON_CHARS) && (!required || (length > 0));
    }
}
