#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public enum TcgCreatureBorder : byte
{
    None,
    Selectable,
    Selected
}

public enum TcgSelectionMode : byte
{
    Attack,
    Block
}

public readonly record struct TcgCreatureBorderChoice(TcgCreatureBorder Border, TcgSelectionMode Mode);

/// <summary>
///     Which selection border a battlefield creature gets. Only the viewer's own creatures during attack or block
///     selection ever get one; Mode names the selection in effect and is only meaningful when Border is not None.
/// </summary>
public static class TcgCreatureBorders
{
    public static TcgCreatureBorderChoice ForCreature(
        TcgDisplayArgs board,
        TcgMatchInput input,
        TcgCreatureView creature,
        Func<string, TcgCatalogCardView?> lookup)
    {
        if ((board.Players.Count >= 2)
            && (board.Players[(int)board.YourSeat].Battlefield.Find(own => own.Id == creature.Id) is { } own))
        {
            if (input.Mode == TcgInputMode.Attacking)
                return new TcgCreatureBorderChoice(
                    input.Attackers.Contains(creature.Id)
                        ? TcgCreatureBorder.Selected
                        : TcgMatchRules.CanAttack(board, own)
                            ? TcgCreatureBorder.Selectable
                            : TcgCreatureBorder.None,
                    TcgSelectionMode.Attack);

            if ((input.Mode == TcgInputMode.Blocking) && TcgMatchRules.YouDecide(board, TcgDecision.Block))
                return new TcgCreatureBorderChoice(
                    (input.Blocks.ContainsKey(creature.Id) || (input.SelectedBlocker == creature.Id))
                        ? TcgCreatureBorder.Selected
                        : TcgMatchRules.CanBlockAny(board, own, lookup)
                            ? TcgCreatureBorder.Selectable
                            : TcgCreatureBorder.None,
                    TcgSelectionMode.Block);
        }

        return new TcgCreatureBorderChoice(TcgCreatureBorder.None, TcgSelectionMode.Attack);
    }
}
