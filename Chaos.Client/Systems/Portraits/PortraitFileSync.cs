#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Systems.Portraits;

public enum PortraitFileAction
{
    None,
    Write,
    Delete
}

/// <summary>
///     Decides whether an own-portrait picture from the server should touch the character-folder file. The file mirrors
///     the server only after the login <c>EditableProfile</c> reply has been sent this session, so a hand-edited file is
///     always read and sent first and a stale reply-sent flag from a previous character can never write or delete.
/// </summary>
public sealed class PortraitFileSync(bool loginReplySent)
{
    public static bool ShouldWrite(byte[] fileBytes, byte[] incoming)
        => (incoming is { Length: > 0 }) && !fileBytes.SequenceEqual(incoming);

    public static string? BackupSourceFileName(string name, bool jpgExists, bool extensionlessExists)
        => jpgExists ? $"{name}.jpg" : extensionlessExists ? name : null;

    public bool ShouldSync(PortraitPictureArgs args, byte[]? fileBytes)
        => Decide(args, fileBytes) == PortraitFileAction.Write;

    public PortraitFileAction Decide(PortraitPictureArgs args, byte[]? fileBytes)
    {
        if (!loginReplySent || !args.IsOwn)
            return PortraitFileAction.None;

        if (args.Picture is { Length: > 0 })
            return (fileBytes is null) || ShouldWrite(fileBytes, args.Picture)
                ? PortraitFileAction.Write
                : PortraitFileAction.None;

        return fileBytes is not null ? PortraitFileAction.Delete : PortraitFileAction.None;
    }
}
