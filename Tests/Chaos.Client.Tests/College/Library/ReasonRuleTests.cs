using Chaos.Client.ViewModel.College.Library;
using FluentAssertions;

namespace Chaos.Client.Tests.College.Library;

public class ReasonRuleTests
{
    [Test]
    [Arguments("", true, false)]
    [Arguments("   ", true, false)]
    [Arguments("", false, true)]
    [Arguments("Copied from another game", true, true)]
    public void Reasons_are_checked(string text, bool required, bool allowed)
        => ReasonRule.Allows(text, required).Should().Be(allowed);

    [Test]
    public void A_reason_of_exactly_three_hundred_characters_is_allowed()
        => ReasonRule.Allows(new string('a', 300), true).Should().BeTrue();

    [Test]
    public void A_reason_over_three_hundred_characters_is_refused()
        => ReasonRule.Allows(new string('a', 301), true).Should().BeFalse();

    [Test]
    public void A_padded_reason_is_measured_after_trimming()
        => ReasonRule.Allows($"  {new string('a', 300)}  ", true).Should().BeTrue();
}
