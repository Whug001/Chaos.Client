using Chaos.Client.Controls.World.Popups.Beauty;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class MirrorPagesTests
{
    [Test]
    public async Task Pages_run_gender_hair_skin_face_review()
    {
        Enum.GetValues<MirrorPage>().Should().Equal(MirrorPage.Gender, MirrorPage.Hair, MirrorPage.Skin, MirrorPage.Face, MirrorPage.Review);
        MirrorPages.COUNT.Should().Be(5);
        MirrorPages.First.Should().Be(MirrorPage.Gender);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Previous_and_Next_stop_at_the_ends()
    {
        MirrorPages.Previous(MirrorPage.Gender).Should().BeNull();
        MirrorPages.Next(MirrorPage.Gender).Should().Be(MirrorPage.Hair);
        MirrorPages.Previous(MirrorPage.Skin).Should().Be(MirrorPage.Hair);
        MirrorPages.Next(MirrorPage.Face).Should().Be(MirrorPage.Review);
        MirrorPages.Previous(MirrorPage.Review).Should().Be(MirrorPage.Face);
        MirrorPages.Next(MirrorPage.Review).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Arrow_labels_name_the_page_they_lead_to()
    {
        MirrorPages.BackLabel(MirrorPage.Gender).Should().BeEmpty();
        MirrorPages.NextLabel(MirrorPage.Gender).Should().Be("Hair >");
        MirrorPages.BackLabel(MirrorPage.Skin).Should().Be("< Hair");
        MirrorPages.NextLabel(MirrorPage.Skin).Should().Be("Face >");
        MirrorPages.NextLabel(MirrorPage.Face).Should().Be("Review >");
        MirrorPages.BackLabel(MirrorPage.Review).Should().Be("< Face");
        MirrorPages.NextLabel(MirrorPage.Review).Should().Be("APPLY");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Page_text_is_plain_ascii_and_fits_the_column()
    {
        foreach (var page in Enum.GetValues<MirrorPage>())
        {
            MirrorPages.Title(page).Should().NotBeNullOrEmpty();

            var text = MirrorPages.Title(page) + MirrorPages.Name(page) + MirrorPages.Instruction(page) + MirrorPages.BackLabel(page) + MirrorPages.NextLabel(page);
            text.Should().MatchRegex("^[\\x20-\\x7E]*$");

            MirrorPages.Instruction(page).Length.Should().BeLessThanOrEqualTo(53);
        }

        await Task.CompletedTask;
    }
}
