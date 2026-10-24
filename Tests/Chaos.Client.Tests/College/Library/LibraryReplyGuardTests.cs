using Chaos.Client.ViewModel.College.Library;
using FluentAssertions;

namespace Chaos.Client.Tests.College.Library;

public class LibraryReplyGuardTests
{
    [Test]
    public void A_reply_for_the_old_book_after_opening_a_new_one_is_ignored()
        => LibraryReplyGuard.ShouldTake(awaitingReplies: 0, sessionBookId: 2, replyId: 1).Should().BeFalse();

    [Test]
    public void A_reply_for_another_book_is_ignored_while_a_save_is_outstanding()
        => LibraryReplyGuard.ShouldTake(awaitingReplies: 1, sessionBookId: 2, replyId: 1).Should().BeFalse();

    [Test]
    public void A_reply_with_nothing_outstanding_is_ignored()
        => LibraryReplyGuard.ShouldTake(awaitingReplies: 0, sessionBookId: 2, replyId: 2).Should().BeFalse();

    [Test]
    public void A_reply_for_the_open_book_is_taken()
        => LibraryReplyGuard.ShouldTake(awaitingReplies: 1, sessionBookId: 2, replyId: 2).Should().BeTrue();

    [Test]
    public void A_reply_without_an_id_is_taken_while_a_save_is_outstanding()
        => LibraryReplyGuard.ShouldTake(awaitingReplies: 1, sessionBookId: 2, replyId: 0).Should().BeTrue();
}
