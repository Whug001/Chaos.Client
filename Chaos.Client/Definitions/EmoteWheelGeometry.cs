namespace Chaos.Client.Definitions;

public static class EmoteWheelGeometry
{
    public static int GetSegmentIndex(float dx, float dy, float innerRadius, float outerRadius)
    {
        var distSq = (dx * dx) + (dy * dy);

        if (distSq < innerRadius * innerRadius || distSq > outerRadius * outerRadius)
            return -1;

        //atan2: 0 = east; rotate so 0 = top (12 o'clock), clockwise
        var angleDeg = MathF.Atan2(dy, dx) * (180f / MathF.PI);
        angleDeg = (angleDeg + 90f + 360f) % 360f;
        var segment = (int)(angleDeg / (360f / EmoteCatalog.SLOT_COUNT));

        return segment >= EmoteCatalog.SLOT_COUNT ? -1 : segment;
    }
}
