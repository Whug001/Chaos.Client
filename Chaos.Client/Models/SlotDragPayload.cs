using Chaos.Client.Controls.World.Hud.Panel.Slots;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Models;

public sealed class SlotDragPayload : IDragGhost
{
    public required PanelSlot Source { get; init; }
    public required byte SlotIndex { get; init; }
    public required HudTab SourcePanel { get; init; }

    public Texture2D? GhostTexture => Source.NormalTexture;
}
