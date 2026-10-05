using System.Runtime.InteropServices;

namespace Chaos.Client.Systems.College;

/// <summary>
///     The Windows open-file dialog for pictures. It runs on its own STA thread, because the dialog is modal and the game
///     loop must keep answering the server while the player browses.
/// </summary>
public static class PictureFilePicker
{
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_EXPLORER = 0x00080000;
    private const int MAX_PATH_CHARS = 1024;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

    /// <summary>Shows the dialog, owned by <paramref name="owner" /> (the game's Win32 window) so it opens in front of the game.</summary>
    public static Task<string?> PickAsync(nint owner)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult<string?>(null);

        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(
            () =>
            {
                try
                {
                    result.SetResult(Show(owner));
                } catch (Exception e)
                {
                    result.SetException(e);
                }
            })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return result.Task;
    }

    private static string? Show(nint owner)
    {
        var buffer = Marshal.AllocHGlobal(MAX_PATH_CHARS * 2);

        try
        {
            Marshal.WriteInt16(buffer, 0);

            var dialog = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = owner,
                lpstrFilter = "Pictures (*.png, *.jpg)\0*.png;*.jpg;*.jpeg\0\0",
                lpstrFile = buffer,
                nMaxFile = MAX_PATH_CHARS,
                lpstrTitle = "Insert picture",
                Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
            };

            return GetOpenFileNameW(ref dialog) ? Marshal.PtrToStringUni(buffer) : null;
        } finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
