using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Models;

/// <summary>
///     A hotbar slot being dragged. What a drop does depends on whether the bar it came from was locked.
/// </summary>
/// <remarks>
///     From an unlocked bar the slot itself moves: dropped on another slot it swaps with it, dropped anywhere else it is
///     emptied. From a locked bar the slot stays put and the drag is a use of what it holds, the same as dragging it out
///     of its own panel: an item dropped on someone is given (or offered in a trade), on the ground it is dropped; a spell
///     is cast on whatever it lands on. Dropped on an unlocked bar, it is copied there.
/// </remarks>
public sealed class HotbarDragPayload : IDragGhost
{
    /// <summary>The bar the drag started on, 0-3.</summary>
    public required int Bar { get; init; }

    /// <summary>The slot on that bar, 0-14.</summary>
    public required int Slot { get; init; }

    /// <summary>What the slot held when the drag started.</summary>
    public required HotbarEntry Entry { get; init; }

    /// <summary>True when the source bar was locked: the slot stays filled, and the drop uses what it holds.</summary>
    public required bool KeepsSource { get; init; }

    public Texture2D? GhostTexture { get; init; }
}
