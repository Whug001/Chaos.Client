namespace Chaos.Client.ViewModel.College.Library;

public static class LibraryReplyGuard
{
    /// <summary>
    ///     Whether a Library WriterResult answers a request this window still waits for: a reply counts only while a
    ///     Library save or send is outstanding, and a reply naming another book (a late answer for the book open
    ///     before the current one) never does. A reply without an id answers the open book.
    /// </summary>
    public static bool ShouldTake(int awaitingReplies, int sessionBookId, int replyId)
        => (awaitingReplies > 0) && ((sessionBookId == 0) || (replyId == 0) || (replyId == sessionBookId));
}
