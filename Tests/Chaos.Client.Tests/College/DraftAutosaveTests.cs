using Chaos.Client.ViewModel.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class DraftAutosaveTests
{
    private static readonly DateTime Start = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_changed_draft_is_saved_a_minute_after_its_first_unsaved_change()
    {
        var autosave = new DraftAutosave();

        autosave.IsDue(true, Start).Should().BeFalse();
        autosave.IsDue(true, Start.AddSeconds(59)).Should().BeFalse();
        autosave.IsDue(true, Start.AddSeconds(60)).Should().BeTrue();
        autosave.IsDue(true, Start.AddSeconds(61)).Should().BeFalse();
        autosave.IsDue(true, Start.AddSeconds(120)).Should().BeFalse();
        autosave.IsDue(true, Start.AddSeconds(121)).Should().BeTrue();
    }

    [Test]
    public void A_saved_draft_starts_the_minute_again_at_its_next_change()
    {
        var autosave = new DraftAutosave();
        autosave.IsDue(true, Start);

        autosave.IsDue(false, Start.AddSeconds(30)).Should().BeFalse();
        autosave.IsDue(true, Start.AddSeconds(70)).Should().BeFalse();
        autosave.IsDue(true, Start.AddSeconds(129)).Should().BeFalse();
        autosave.IsDue(true, Start.AddSeconds(130)).Should().BeTrue();
    }

    [Test]
    public void A_clean_draft_is_never_due()
    {
        var autosave = new DraftAutosave();

        autosave.IsDue(false, Start).Should().BeFalse();
        autosave.IsDue(false, Start.AddMinutes(10)).Should().BeFalse();
    }
}
