using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TownImportPoolEditorTests
{
    private static TownImportItemInfo Item(string key, int price = 0) => new() { TemplateKey = key, Name = key, Price = price };

    [Test]
    public void Load_sets_the_saved_pool_and_clears_unsaved()
    {
        var editor = new TownImportPoolEditor();
        editor.Load([Item("a", 5_000), Item("b", 6_000)]);

        editor.Unsaved.Should().BeFalse();
        editor.Items.Count.Should().Be(2);
    }

    [Test]
    public void Add_uses_the_default_price_and_refuses_duplicates_and_a_full_pool()
    {
        var editor = new TownImportPoolEditor();
        editor.Load([]);

        editor.Add(Item("a")).Should().BeTrue();
        editor.Items[0].Price.Should().Be(TownImportProtocol.DEFAULT_PRICE);
        editor.Unsaved.Should().BeTrue();
        editor.Add(Item("A")).Should().BeFalse();

        for (var i = 1; i < TownImportProtocol.MAX_POOL; i++)
            editor.Add(Item($"k{i}"));

        editor.Add(Item("overflow")).Should().BeFalse();
        editor.Items.Count.Should().Be(TownImportProtocol.MAX_POOL);
    }

    [Test]
    public void Prices_parse_with_commas_and_refuse_bad_text()
    {
        var editor = new TownImportPoolEditor();
        editor.Load([Item("a", 5_000)]);

        editor.SetPrice(0, "150,000").Should().BeTrue();
        editor.Items[0].Price.Should().Be(150_000);
        editor.SetPrice(0, "").Should().BeFalse();
        editor.SetPrice(0, "0").Should().BeFalse();
        editor.SetPrice(0, "abc").Should().BeFalse();
        editor.SetPrice(0, "3000000000").Should().BeFalse();
        editor.Items[0].Price.Should().Be(150_000);
    }

    [Test]
    public void Move_remove_revert_and_entries()
    {
        var editor = new TownImportPoolEditor();
        editor.Load([Item("a", 1_000), Item("b", 2_000), Item("c", 3_000)]);

        editor.Move(2, -1).Should().Be(1);
        editor.Move(0, -1).Should().Be(0);
        editor.Remove(0);

        var entries = editor.ToEntries();
        entries.Select(entry => entry.TemplateKey).Should().Equal("c", "b");
        entries[0].Price.Should().Be(3_000);

        editor.Revert();
        editor.Unsaved.Should().BeFalse();
        editor.Items.Select(item => item.TemplateKey).Should().Equal("a", "b", "c");
    }
}
