#region
using Chaos.Client.Collections;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.Systems.KeyBinds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Tab targeting (F4 → Interaction → Tab targeting). While an entity-targeted spell is armed, the starting target is
///     boxed in light blue. Next/Previous Target and the arrows move the box, the pick keys jump it (self, group panels
///     1-5, nearest and furthest enemy), Cast on Target casts on it, and Cancel Targeting backs out. While a ground-targeted
///     spell is armed, the ground target indicator starts on the same pick (or the player), the arrows move it a tile at a
///     time, the pick keys move it onto an entity, and Cast on Target casts there. Moving the mouse hands the ground aim
///     back to the mouse. Clicking still works as before for both. The keys are the player's F12 Targeting bindings
///     (defaults Tab, Shift+Tab, Space, Esc, F1-F8); only the arrows are fixed.
/// </summary>
public sealed partial class WorldScreen
{
    private const int TAB_TARGET_BOX_PADDING = 2;
    private const int TAB_TARGET_BOX_THICKNESS = 2;
    private static readonly Color TabTargetBoxColor = new(120, 200, 255);

    private readonly TabTargeting TabTarget = new();

    //where the mouse was when the keyboard took the ground aim; moving it from here gives the aim back to the mouse
    private Point AimMouse;

    /// <summary>True while the entity tab-targeting keys are live: the option is on and a non-ground spell is armed.</summary>
    private bool IsTabTargeting => ClientSettings.TabTargeting && CastingSystem is { IsTargeting: true, IsGroundTargeting: false };

    /// <summary>True while the ground-aim keys are live: the option is on and a ground-targeted spell is armed.</summary>
    private bool IsTabGroundAiming => ClientSettings.TabTargeting && CastingSystem.IsGroundTargeting;

    /// <summary>
    ///     The keyboard ground aim, for the indicator and the cast. Null while the mouse aims, or when no ground spell is
    ///     armed with tab targeting on.
    /// </summary>
    private (int X, int Y)? KeyboardAimTile => IsTabGroundAiming ? TabTarget.AimTile : null;

    /// <summary>
    ///     Sets the starting target as a spell is armed. Entity spells box the last target while it lives, else the nearest
    ///     enemy, else the nearest eligible target. Ground spells put the indicator on the nearest enemy, else the player.
    ///     Called from <see cref="TryBeginCast" />.
    /// </summary>
    private void BeginTabTargeting()
    {
        TabTarget.Clear();
        TabTarget.ClearAim();

        if (IsTabTargeting)
            TabTarget.SelectInitial(GatherTabCandidates(), IsArmedSpellOffensive, Game.Connection.AislingId);
        else if (IsTabGroundAiming && WorldState.GetPlayerEntity() is { } player)
        {
            //the same pick as an entity spell (last target, else nearest enemy), so Next Target and the F keys go on from it
            TabTarget.SelectInitial(GatherTabCandidates(), IsArmedSpellOffensive, Game.Connection.AislingId);

            if (TabTarget.SelectedId is { } id && WorldState.GetEntity(id) is { } picked)
                AimAt(picked.TileX, picked.TileY);
            else
                AimAt(player.TileX, player.TileY);
        }
    }

    /// <summary>
    ///     Whether the armed spell is offensive (its filter names hostile targets: suain, cradh, the wizard spells) rather
    ///     than supportive (heals, cures). Decides which last target the pick goes back to, and whether it prefers an enemy
    ///     or the player.
    /// </summary>
    private bool IsArmedSpellOffensive
        => CastingSystem.TargetingSlot is not { } slot || TabTargeting.IsOffensive(WorldState.SpellBook.GetTargetFilter(slot.Slot));

    /// <summary>
    ///     Records the entity the armed spell is about to be cast on as that kind's last target. Must run before
    ///     <c>CastingSystem.SelectTarget</c>, which disarms the spell.
    /// </summary>
    private void RememberCastTarget(uint id) => TabTarget.Remember(id, IsArmedSpellOffensive);

    /// <summary>Puts the keyboard ground aim on a tile and notes the mouse position it now overrides.</summary>
    private void AimAt(int tileX, int tileY)
    {
        TabTarget.Aim(tileX, tileY);
        AimMouse = new Point(InputBuffer.MouseX, InputBuffer.MouseY);
    }

    /// <summary>
    ///     Drops the box once the spell is cast or cancelled, or when the target leaves the screen or stops qualifying.
    ///     Hands the ground aim back to the mouse once the mouse moves, or when the ground spell ends.
    /// </summary>
    private void UpdateTabTargeting()
    {
        //a ground spell keeps the pick too (unboxed), so Next Target steps on from the entity the indicator jumped to
        if (IsTabTargeting || IsTabGroundAiming)
            TabTarget.Retarget(GatherTabCandidates(), IsArmedSpellOffensive, Game.Connection.AislingId);
        else
            TabTarget.Clear();

        if (!IsTabGroundAiming || ((InputBuffer.MouseX, InputBuffer.MouseY) != (AimMouse.X, AimMouse.Y)))
            TabTarget.ClearAim();
    }

    /// <summary>
    ///     Runs a tab-targeting key. True when the key was taken, so the normal binding for it (Tab map, walking, assail,
    ///     F-key windows) doesn't also run while a spell is waiting for a target.
    /// </summary>
    private bool HandleTabTargetKey(KeyDownEvent e)
    {
        var ground = IsTabGroundAiming;

        if (!ground && !IsTabTargeting)
            return false;

        //the player's targeting keys (F12 → Targeting) first: the exact chord, then the bare key
        foreach (var action in KeyBindings.Current.Candidates(KeyChord.From(e), targeting: true))
            return RunTargetingAction(action, ground);

        //the arrows are fixed: they step the ground aim a tile, or cycle the boxed target
        if (e.Ctrl || e.Alt || e.Shift)
            return false;

        (int X, int Y)? step = e.Scancode switch
        {
            Scancode.Up    => (0, -1),
            Scancode.Right => (1, 0),
            Scancode.Down  => (0, 1),
            Scancode.Left  => (-1, 0),
            _              => null
        };

        if (step is not { } move)
            return false;

        if (ground)
            StepGroundAim(move.X, move.Y);
        else
            TabTarget.Cycle(GatherTabCandidates(), move.X + move.Y);

        return true;
    }

    /// <summary>
    ///     Runs one targeting action. The pick actions box an entity, or with a ground spell armed put the indicator on that
    ///     entity's tile. True when the key was used up; Cast on Target with nothing to cast on is not, so its key goes on to
    ///     its normal job (Space assails).
    /// </summary>
    private bool RunTargetingAction(GameAction action, bool ground)
    {
        switch (action)
        {
            case GameAction.TargetCast:
                return ground ? CastOnGroundAim() : CastOnTabTarget();

            case GameAction.TargetCancel:
                CastingSystem.CancelTargeting();
                TabTarget.Clear();
                TabTarget.ClearAim();

                return true;
        }

        var candidates = GatherTabCandidates();

        switch (action)
        {
            case GameAction.TargetNext:
                TabTarget.Cycle(candidates, 1);

                break;

            case GameAction.TargetPrevious:
                TabTarget.Cycle(candidates, -1);

                break;

            case GameAction.TargetSelf:
            {
                var self = Game.Connection.AislingId;

                if (candidates.Any(c => c.Id == self))
                    TabTarget.Select(self);

                break;
            }

            case GameAction.TargetNearestEnemy or GameAction.TargetFurthestEnemy:
            {
                var hostiles = TabTargeting.Hostiles(candidates);

                if (hostiles.Count == 0)
                    break;

                if (action == GameAction.TargetNearestEnemy)
                    TabTarget.SelectFirst(hostiles);
                else
                    TabTarget.SelectLast(hostiles);

                break;
            }

            default:
            {
                if (TabTargeting.GroupMember(candidates, [..GroupVitals.OrderedNames], GameActions.TargetGroupIndex(action)) is { } member)
                    TabTarget.Select(member.Id);

                break;
            }
        }

        if (ground && TabTarget.SelectedId is { } id && WorldState.GetEntity(id) is { } picked)
            AimAt(picked.TileX, picked.TileY);

        //a pick key is used up even when it found nothing, so F3 doesn't open Macros in the middle of a cast
        return true;
    }

    /// <summary>
    ///     Casts the armed spell on the boxed target, exactly as clicking it would. False (the key goes on to its normal
    ///     job) when nothing is boxed.
    /// </summary>
    private bool CastOnTabTarget()
    {
        if (TabTarget.SelectedId is not { } id || WorldState.GetEntity(id) is not { } target)
            return false;

        RememberCastTarget(target.Id);

        CastingSystem.SelectTarget(
            target.Id,
            target.TileX,
            target.TileY,
            Game.Connection);
        TabTarget.Clear();

        return true;
    }

    /// <summary>
    ///     The entities on screen the armed spell can be cast on, nearest first. "On screen" is the entities drawn last
    ///     frame whose hit box overlaps the viewport.
    /// </summary>
    private List<TargetCandidate> GatherTabCandidates()
        => CastingSystem.TargetingSlot is { } slot
            ? GatherOnScreenCandidates(WorldState.SpellBook.GetTargetFilter(slot.Slot))
            : [];

    /// <summary>
    ///     The on-screen entities that pass <paramref name="filter" />, nearest first. "On screen" is the entities drawn
    ///     last frame whose hit box overlaps the viewport.
    /// </summary>
    private List<TargetCandidate> GatherOnScreenCandidates(SpellTargetFilter filter)
    {
        var player = WorldState.GetPlayerEntity();

        if (player is null)
            return [];

        var viewport = WorldHud.ViewportBounds;
        var screen = new Rectangle(0, 0, viewport.Width, viewport.Height);
        var seen = new HashSet<uint>();
        var onScreen = new List<TargetCandidate>();

        foreach (var hitBox in EntityHitBoxes)
        {
            if (!hitBox.ScreenRect.Intersects(screen) || !seen.Add(hitBox.EntityId))
                continue;

            if (WorldState.GetEntity(hitBox.EntityId) is not { IsHidden: false } entity)
                continue;

            onScreen.Add(
                new TargetCandidate(
                    entity.Id,
                    entity.Name,
                    entity.Type,
                    entity.CreatureType,
                    entity.IsDead,
                    entity.TileX,
                    entity.TileY));
        }

        var groupNames = new HashSet<string>(GroupVitals.OrderedNames, StringComparer.OrdinalIgnoreCase);

        foreach (var name in WorldState.Group.Members)
            if (!string.Equals(name, WorldState.PlayerName, StringComparison.OrdinalIgnoreCase))
                groupNames.Add(name);

        return TabTargeting.Candidates(
            filter,
            onScreen,
            Game.Connection.AislingId,
            player.TileX,
            player.TileY,
            groupNames);
    }

    /// <summary>
    ///     Steps the ground indicator a tile (the arrows, in walking directions). The first step after the mouse took over
    ///     starts from the tile the mouse is aiming at.
    /// </summary>
    private void StepGroundAim(int dx, int dy)
    {
        if (MapFile is null)
            return;

        if (TabTarget.AimTile is null)
        {
            if (GroundTargetTileAt(InputBuffer.MouseX, InputBuffer.MouseY) is { } mouseTile)
                AimAt(mouseTile.X, mouseTile.Y);
            else if (WorldState.GetPlayerEntity() is { } player)
                AimAt(player.TileX, player.TileY);
            else
                return;
        }

        TabTarget.MoveAim(dx, dy, MapFile.Width, MapFile.Height);
    }

    /// <summary>
    ///     Casts the armed ground spell on the keyboard aim, or where the mouse is aiming once the mouse has taken over (the
    ///     same tile a click would use). False when there is no tile to cast on.
    /// </summary>
    private bool CastOnGroundAim()
    {
        if ((TabTarget.AimTile ?? GroundTargetTileAt(InputBuffer.MouseX, InputBuffer.MouseY)) is not { } tile)
            return false;

        CastingSystem.SelectTarget(
            0,
            tile.X,
            tile.Y,
            Game.Connection);
        TabTarget.Clear();
        TabTarget.ClearAim();

        return true;
    }

    /// <summary>The light-blue box around the tab target. Drawn in the overlay pass, so darkness doesn't hide it.</summary>
    private void DrawTabTargetBox(SpriteBatch spriteBatch)
    {
        //a ground spell shows its pick with the ground indicator instead
        if (!IsTabTargeting || TabTarget.SelectedId is not { } id)
            return;

        foreach (var hitBox in EntityHitBoxes)
        {
            if (hitBox.EntityId != id)
                continue;

            var box = hitBox.ScreenRect;
            box.Inflate(TAB_TARGET_BOX_PADDING, TAB_TARGET_BOX_PADDING);

            RenderHelper.DrawRect(spriteBatch, new Rectangle(box.X, box.Y, box.Width, TAB_TARGET_BOX_THICKNESS), TabTargetBoxColor);

            RenderHelper.DrawRect(
                spriteBatch,
                new Rectangle(box.X, box.Bottom - TAB_TARGET_BOX_THICKNESS, box.Width, TAB_TARGET_BOX_THICKNESS),
                TabTargetBoxColor);
            RenderHelper.DrawRect(spriteBatch, new Rectangle(box.X, box.Y, TAB_TARGET_BOX_THICKNESS, box.Height), TabTargetBoxColor);

            RenderHelper.DrawRect(
                spriteBatch,
                new Rectangle(box.Right - TAB_TARGET_BOX_THICKNESS, box.Y, TAB_TARGET_BOX_THICKNESS, box.Height),
                TabTargetBoxColor);

            return;
        }
    }
}
