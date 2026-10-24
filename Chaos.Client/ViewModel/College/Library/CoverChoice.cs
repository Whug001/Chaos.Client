namespace Chaos.Client.ViewModel.College.Library;

public enum CoverKind { Picture, Drawing, Plain }

public static class CoverChoice
{
    public static CoverKind Of(string coverHash, bool drawingCover)
        => coverHash.Length > 0 ? CoverKind.Picture : drawingCover ? CoverKind.Drawing : CoverKind.Plain;
}
