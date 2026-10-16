using Chaos.Client.Systems.Portraits;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.Portraits;

public sealed class PortraitFileSyncTests
{
    [Test]
    public void Differing_bytes_need_a_write()
    {
        PortraitFileSync.ShouldWrite([1, 2, 3], [1, 2, 4]).Should().BeTrue();
        PortraitFileSync.ShouldWrite([1, 2, 3], [1, 2, 3]).Should().BeFalse();
        PortraitFileSync.ShouldWrite([], []).Should().BeFalse();
    }

    [Test]
    public void Nothing_writes_before_the_login_reply_is_sent()
    {
        var sync = new PortraitFileSync(loginReplySent: false);
        sync.ShouldSync(new PortraitPictureArgs { IsOwn = true, Hash = "aa", Picture = [1, 2, 3] }, fileBytes: [1, 2, 4]).Should().BeFalse();
    }

    [Test]
    public void A_differing_own_picture_writes_after_the_login_reply()
    {
        var sync = new PortraitFileSync(loginReplySent: true);
        sync.ShouldSync(new PortraitPictureArgs { IsOwn = true, Hash = "aa", Picture = [1, 2, 3] }, fileBytes: [1, 2, 4]).Should().BeTrue();
    }

    [Test]
    public void An_empty_own_picture_deletes_the_file_after_the_login_reply()
    {
        var sync = new PortraitFileSync(loginReplySent: true);
        sync.Decide(new PortraitPictureArgs { IsOwn = true, Hash = string.Empty, Picture = [] }, fileBytes: [1, 2, 4]).Should().Be(PortraitFileAction.Delete);
        sync.Decide(new PortraitPictureArgs { IsOwn = true, Hash = string.Empty, Picture = [] }, fileBytes: null).Should().Be(PortraitFileAction.None);
    }

    [Test]
    public void An_empty_own_picture_before_the_login_reply_does_nothing()
    {
        var sync = new PortraitFileSync(loginReplySent: false);
        sync.Decide(new PortraitPictureArgs { IsOwn = true, Hash = string.Empty, Picture = [] }, fileBytes: [1, 2, 4]).Should().Be(PortraitFileAction.None);
    }

    [Test]
    public void A_fetched_picture_never_syncs_the_file()
    {
        var sync = new PortraitFileSync(loginReplySent: true);
        sync.ShouldSync(new PortraitPictureArgs { IsOwn = false, Hash = "bb", Picture = [1, 2, 3] }, fileBytes: [1, 2, 4]).Should().BeFalse();
    }

    [Test]
    public void A_matching_own_picture_needs_nothing_after_the_login_reply()
    {
        var sync = new PortraitFileSync(loginReplySent: true);
        sync.Decide(new PortraitPictureArgs { IsOwn = true, Hash = "aa", Picture = [1, 2, 3] }, fileBytes: [1, 2, 3]).Should().Be(PortraitFileAction.None);
    }

    [Test]
    public void A_missing_file_is_written_after_the_login_reply()
    {
        var sync = new PortraitFileSync(loginReplySent: true);
        sync.Decide(new PortraitPictureArgs { IsOwn = true, Hash = "aa", Picture = [1, 2, 3] }, fileBytes: null).Should().Be(PortraitFileAction.Write);
    }
}
