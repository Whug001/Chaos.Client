namespace Chaos.Client.ViewModel.College.Library;

public enum CoverKind { Picture, Drawing, Plain }

public enum CoverShown { Plain, Art, Loading }

public static class CoverChoice
{
    public static CoverKind Of(string coverHash, bool drawingCover)
        => coverHash.Length > 0 ? CoverKind.Picture : drawingCover ? CoverKind.Drawing : CoverKind.Plain;

    /// <summary>What the front cover shows: the art once it is here, plain if there is none or it failed, else loading.</summary>
    public static CoverShown Shows(CoverKind kind, bool artReady, bool artFailed)
        => kind == CoverKind.Plain ? CoverShown.Plain
            : artReady ? CoverShown.Art
            : artFailed ? CoverShown.Plain
            : CoverShown.Loading;
}
