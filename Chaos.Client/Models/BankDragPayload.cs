using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Models;

/// <summary>
///     A drag out of the bank window. A null <see cref="ItemName" /> is the gold bag — the bank's one non-item drag.
///     Name and count are snapshotted at drag start, so a refresh re-binding the row mid-drag cannot re-point the drop at
///     a different entry.
/// </summary>
public sealed class BankDragPayload : IDragGhost
{
    public string? ItemName { get; init; }
    public int Count { get; init; }

    //a shared UiRenderer item-icon texture — borrowed, never disposed.
    public required Texture2D? GhostTexture { get; init; }

    public bool IsGold => ItemName is null;
}
