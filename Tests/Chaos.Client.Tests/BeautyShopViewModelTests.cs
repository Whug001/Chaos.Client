using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class BeautyShopViewModelTests
{
    private static BeautyShopDisplayArgs Open()
        => new()
        {
            Type = BeautyShopDisplayType.Open,
            Gender = Gender.Male,
            HairStyle = 1,
            HairColor = DisplayColor.Default,
            BodyColor = BodyColor.White,
            FaceSprite = 1,
            Gold = 60_000,
            GenderPrice = 50_000,
            HairDyePrice = 1_000,
            BodyDyePrice = 1_000,
            MaleHairstyles = [new BeautyShopHairstyleEntry { Sprite = 0, Price = 1_000 }, new BeautyShopHairstyleEntry { Sprite = 1, Price = 1_000 }, new BeautyShopHairstyleEntry { Sprite = 97, Price = 2_500 }],
            FemaleHairstyles = [new BeautyShopHairstyleEntry { Sprite = 0, Price = 1_000 }, new BeautyShopHairstyleEntry { Sprite = 1, Price = 1_000 }, new BeautyShopHairstyleEntry { Sprite = 96, Price = 1_000 }],
            Faces =
            [
                new BeautyShopFaceEntry { Sprite = 1, Name = "Default", Price = 50_000, FemaleOnly = false },
                new BeautyShopFaceEntry { Sprite = 10, Name = "Beauty", Price = 50_000, FemaleOnly = false },
                new BeautyShopFaceEntry { Sprite = 18, Name = "Resting", Price = 50_000, FemaleOnly = true }
            ],
            HairColors = [DisplayColor.Default, DisplayColor.Apple, DisplayColor.Scarlet],
            BodyColors = [BodyColor.Brown, BodyColor.Tan, BodyColor.White]
        };

    private static BeautyShopDisplayArgs OpenAsFemaleWithRestingFace()
    {
        var args = Open();
        args.Gender = Gender.Female;
        args.FaceSprite = 18;
        args.HairStyle = 1;

        return args;
    }

    private static BeautyShop Opened()
    {
        var vm = new BeautyShop();
        vm.ApplyOpen(Open());

        return vm;
    }

    [Test]
    public async Task ApplyOpen_seeds_current_and_selected_and_Clear_resets()
    {
        var vm = Opened();

        vm.IsOpen.Should().BeTrue();
        vm.Gender.Should().Be(Gender.Male);
        vm.HairStyle.Should().Be(1);
        vm.Total.Should().Be(0);
        vm.CanApply.Should().BeFalse();

        vm.Clear();

        vm.IsOpen.Should().BeFalse();
        vm.Hairstyles.Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Total_sums_only_changed_categories()
    {
        var vm = Opened();

        vm.StepHairstyle(+1);          // 1 -> 97 (2,500)
        vm.StepHairColor(+1);          // Default -> Apple (1,000)
        vm.StepFace(+1);               // Default -> Beauty (50,000)

        vm.Total.Should().Be(53_500);
        vm.CanAfford.Should().BeTrue();
        vm.CanApply.Should().BeTrue();

        vm.StepHairColor(-1);          // back to Default: no longer charged

        vm.Total.Should().Be(52_500);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unaffordable_total_blocks_apply()
    {
        var vm = Opened();

        vm.SetGender(Gender.Female);   // 50,000
        vm.StepFace(+1);               // 50,000

        vm.Total.Should().Be(100_000);
        vm.CanAfford.Should().BeFalse();
        vm.CanApply.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Steps_wrap_around_their_lists()
    {
        var vm = Opened();

        vm.StepHairstyle(-1);
        vm.HairStyle.Should().Be(0);
        vm.StepHairstyle(-1);
        vm.HairStyle.Should().Be(97);
        vm.StepBodyColor(-1);
        vm.BodyColor.Should().Be(BodyColor.Tan);

        await Task.CompletedTask;
    }

    [Test]
    public async Task SetGender_keeps_the_hairstyle_when_the_other_list_has_it()
    {
        var vm = Opened();

        vm.SetGender(Gender.Female);

        vm.Gender.Should().Be(Gender.Female);
        vm.HairStyle.Should().Be(1);
        vm.Hairstyles.Select(h => h.Sprite).Should().Equal(0, 1, 96);
        vm.Total.Should().Be(50_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task SetGender_falls_back_to_the_first_hairstyle_and_drops_female_only_faces()
    {
        var vm = Opened();
        vm.SetGender(Gender.Female);
        vm.StepHairstyle(+1);          // 1 -> 96 (female only)
        vm.StepFace(-1);               // Default -> Resting (female only, last in list)

        vm.FaceSprite.Should().Be(18);

        vm.SetGender(Gender.Male);

        vm.HairStyle.Should().Be(0);
        vm.FaceSprite.Should().Be(1);
        vm.AvailableFaces.Should().OnlyContain(f => !f.FemaleOnly);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Forced_face_reset_on_gender_switch_is_free()
    {
        //female with the female-only "Resting" face; switching to male forces a reset to the default face
        //(sprite 1, the catalog's first entry) -- BeautyShopCheckout does this for free, so the VM total must
        //not charge for it either
        var vm = new BeautyShop();
        vm.ApplyOpen(OpenAsFemaleWithRestingFace());

        vm.SetGender(Gender.Male);

        vm.FaceSprite.Should().Be(1);
        vm.FaceCharged.Should().BeFalse();
        vm.Total.Should().Be(50_000);

        vm.StepFace(+1);                // 1 -> 10: a deliberate choice, not the forced reset -- it's a purchase

        vm.FaceSprite.Should().Be(10);
        vm.FaceCharged.Should().BeTrue();
        vm.Total.Should().Be(100_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reset_restores_the_current_look()
    {
        var vm = Opened();
        vm.SetGender(Gender.Female);
        vm.StepHairColor(+1);
        vm.StepBodyColor(+1);

        vm.Reset();

        vm.Gender.Should().Be(Gender.Male);
        vm.HairColor.Should().Be(DisplayColor.Default);
        vm.BodyColor.Should().Be(BodyColor.White);
        vm.Total.Should().Be(0);

        await Task.CompletedTask;
    }
}
