using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>Keeps the saved-boards selection on the same board when the list is sent again; row ids are positions, which shift after a delete.</summary>
public static class BoardSavedSelection
{
    /// <summary>The board id at row id <paramref name="rowId" /> (a position from 1), or null.</summary>
    public static string? IdAt(IReadOnlyList<CollegeSavedBoardRowInfo> rows, int rowId)
        => (rowId >= 1) && (rowId <= rows.Count) ? rows[rowId - 1].Id : null;

    /// <summary>The row id of <paramref name="boardId" /> in the new list, or 0 when it is gone.</summary>
    public static int PositionOf(IReadOnlyList<CollegeSavedBoardRowInfo> rows, string? boardId)
    {
        if (boardId is null)
            return 0;

        for (var i = 0; i < rows.Count; i++)
            if (rows[i].Id == boardId)
                return i + 1;

        return 0;
    }
}
