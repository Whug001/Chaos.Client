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

    [Test]
    public async Task Hover_overrides_the_effective_look_but_not_the_selection()
    {
        var vm = Opened();

        vm.SetHoverHairStyle(97);

        vm.IsHovering.Should().BeTrue();
        vm.EffectiveHairStyle.Should().Be(97);
        vm.HairStyle.Should().Be(1);
        vm.Total.Should().Be(0);
        vm.HasUnsavedChanges.Should().BeFalse();

        vm.SetHoverHairColor(DisplayColor.Apple);

        vm.EffectiveHairStyle.Should().Be(1);          // only one hover at a time
        vm.EffectiveHairColor.Should().Be(DisplayColor.Apple);

        vm.ClearHover();

        vm.IsHovering.Should().BeFalse();
        vm.EffectiveHairColor.Should().Be(DisplayColor.Default);

        await Task.CompletedTask;
    }

    [Test]
    public async Task HoverTotal_prices_the_hovered_item_as_if_chosen()
    {
        var vm = Opened();
        vm.StepHairColor(+1);                          // Apple: 1,000 selected

        vm.HoverTotal.Should().Be(1_000);              // nothing hovered → Total

        vm.SetHoverHairStyle(97);                      // +2,500 if chosen
        vm.HoverTotal.Should().Be(3_500);
        vm.Total.Should().Be(1_000);

        vm.SetHoverHairColor(DisplayColor.Default);    // hovering "back to current" would drop the dye charge
        vm.HoverTotal.Should().Be(0);

        vm.SetHoverFace(10);                           // Beauty: +50,000
        vm.HoverTotal.Should().Be(51_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Selection_changes_clear_hover()
    {
        var vm = Opened();
        vm.SetHoverFace(10);

        vm.StepHairstyle(+1);
        vm.IsHovering.Should().BeFalse();

        vm.SetHoverFace(10);
        vm.SetGender(Gender.Female);
        vm.IsHovering.Should().BeFalse();

        vm.SetHoverFace(10);
        vm.Reset();
        vm.IsHovering.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task HasUnsavedChanges_tracks_any_difference()
    {
        var vm = Opened();

        vm.HasUnsavedChanges.Should().BeFalse();
        vm.StepBodyColor(+1);
        vm.HasUnsavedChanges.Should().BeTrue();
        vm.Reset();
        vm.HasUnsavedChanges.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hovering_the_selected_item_changes_nothing()
    {
        var vm = Opened();
        vm.StepFace(+1);                               // Beauty selected, 50,000

        vm.SetHoverFace(10);

        vm.HoverTotal.Should().Be(50_000);
        vm.EffectiveFaceSprite.Should().Be(10);

        await Task.CompletedTask;
    }

    private static BeautyShop OpenedWithManyHairstyles()
    {
        var args = Open();
        args.MaleHairstyles = Enumerable.Range(0, 14).Select(i => new BeautyShopHairstyleEntry { Sprite = (ushort)i, Price = 1_000 }).ToList();
        var vm = new BeautyShop();
        vm.ApplyOpen(args);

        return vm;
    }

    [Test]
    public async Task Pages_slice_the_list_and_wrap()
    {
        var vm = OpenedWithManyHairstyles();            // 14 styles → 3 pages of 6

        vm.HairstylePageCount.Should().Be(3);
        vm.HairstylePage.Should().Be(0);                // selection (1) is on page 0
        vm.VisibleHairstyles.Select(h => (int)h.Sprite).Should().Equal(0, 1, 2, 3, 4, 5);

        vm.StepHairstylePage(+1);
        vm.VisibleHairstyles.Select(h => (int)h.Sprite).Should().Equal(6, 7, 8, 9, 10, 11);
        vm.StepHairstylePage(+1);
        vm.VisibleHairstyles.Select(h => (int)h.Sprite).Should().Equal(12, 13);
        vm.StepHairstylePage(+1);
        vm.HairstylePage.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Selecting_moves_the_page_to_the_selection()
    {
        var vm = OpenedWithManyHairstyles();

        vm.SelectHairStyle(13);
        vm.HairStyle.Should().Be(13);
        vm.HairstylePage.Should().Be(2);

        vm.StepHairstyle(+1);                           // wraps to 0
        vm.HairstylePage.Should().Be(0);

        vm.SelectHairStyle(999);                        // not in the list: ignored
        vm.HairStyle.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Randomize_picks_valid_values_and_keeps_gender()
    {
        var vm = Opened();
        var rng = new Random(1234);

        for (var i = 0; i < 50; i++)
        {
            vm.Randomize(rng);

            vm.Gender.Should().Be(Gender.Male);
            vm.Hairstyles.Select(h => (int)h.Sprite).Should().Contain(vm.HairStyle);
            vm.HairColors.Should().Contain(vm.HairColor);
            vm.BodyColors.Should().Contain(vm.BodyColor);
            vm.AvailableFaces.Select(f => (int)f.Sprite).Should().Contain(vm.FaceSprite);
            vm.FaceSprite.Should().NotBe(18);           // female-only never appears for a male
            vm.IsHovering.Should().BeFalse();
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Randomize_is_deterministic_for_a_seed()
    {
        var a = Opened();
        var b = Opened();

        a.Randomize(new Random(7));
        b.Randomize(new Random(7));

        (a.HairStyle, a.HairColor, a.BodyColor, a.FaceSprite).Should().Be((b.HairStyle, b.HairColor, b.BodyColor, b.FaceSprite));

        await Task.CompletedTask;
    }
}
