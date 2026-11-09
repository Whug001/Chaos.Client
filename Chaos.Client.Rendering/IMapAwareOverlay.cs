namespace Chaos.Client.Rendering;

public interface IMapAwareOverlay : IAmbientOverlay
{
    void SetSurface(MapSurface? surface);
}
