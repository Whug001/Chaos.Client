using Chaos.Client.Controls.LobbyLogin;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class LoginLayoutsTests
{
    [Test]
    [Arguments("_nstart", "lmain")]
    [Arguments("_nlogin", "llogin")]
    [Arguments("_nsvr", "lsvr")]
    [Arguments("_npw", "lpasswd")]
    [Arguments("_nagree", "lagre")]
    [Arguments("_ncreate", "lcreate")]
    public void Classic_look_maps_each_login_layout(string current, string retail)
        => LoginLayouts.Resolve(current, true).Should().Be(retail);

    [Test]
    [Arguments("_nstart")]
    [Arguments("_nlogin")]
    [Arguments("_nsvr")]
    [Arguments("_npw")]
    [Arguments("_nagree")]
    [Arguments("_ncreate")]
    public void Current_look_keeps_every_name(string current)
        => LoginLayouts.Resolve(current, false).Should().Be(current);

    [Test]
    [Arguments("_nchar")]
    [Arguments("_nbtn")]
    [Arguments("lmain")]
    public void Unknown_names_pass_through(string name)
        => LoginLayouts.Resolve(name, true).Should().Be(name);

    [Test]
    [Arguments("lsvr")]
    [Arguments("LSVR")]
    public void Stacked_box_runs_only_on_retail_layout(string layout)
        => LoginLayouts.StacksServers(layout).Should().BeTrue();

    [Test]
    [Arguments("_nsvr")]
    [Arguments("lmain")]
    public void Stacked_box_stays_off_other_layouts(string layout)
        => LoginLayouts.StacksServers(layout).Should().BeFalse();

    [Test]
    public void Matching_ignores_case()
    {
        LoginLayouts.Resolve("_NSTART", true).Should().Be("lmain");
        LoginLayouts.Resolve("_nCreate", true).Should().Be("lcreate");
    }
}
