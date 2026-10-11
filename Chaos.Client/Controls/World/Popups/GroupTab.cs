#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Chaos.Extensions.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     Group/party panel using _ngcdlg0 prefab. Displays group member slots with quit buttons.
///     When the player is the leader, quit buttons next to other members are enabled (kick).
///     During a raid the rows are grouped under party headings and scroll with the mouse wheel, and the leader gets a
///     Move button on each member row.
///     BTN_OK closes the panel.
/// </summary>
public sealed class GroupTab : PrefabPanel
{
    /// <summary>The most members the window lists: a full raid.</summary>
    private const int MAX_MEMBERS = GroupMembersState.MAX_RAID_SIZE;

    /// <summary>
    ///     Rows the dialog has room for. A raid is up to <see cref="MAX_MEMBERS" /> members plus four headings, so it
    ///     scrolls through these.
    /// </summary>
    private const int VISIBLE_ROWS = 13;

    private const int ROW_HEIGHT = 22;
    private const int NAME_X = 53;
    private const int NAME_START_Y = 47;
    private const int QUIT_BTN_X = 156;
    private const int QUIT_BTN_START_Y = 42;
    private const int QUIT_BTN_WIDTH = 30;
    private const int QUIT_BTN_HEIGHT = 20;
    private const int MOVE_BTN_X = 126;
    private const int MOVE_BTN_START_Y = 45;
    private const int MOVE_BTN_WIDTH = 28;
    private const int MOVE_BTN_HEIGHT = 14;

    private readonly UILabel?[] MemberLabels = new UILabel?[VISIBLE_ROWS];
    private readonly RaidMoveButton[] MoveButtons = new RaidMoveButton[VISIBLE_ROWS];
    private readonly UIButton[] QuitButtons = new UIButton[VISIBLE_ROWS];
    private readonly int[] RowMoveTargets = new int[VISIBLE_ROWS];
    private readonly string?[] RowNames = new string?[VISIBLE_ROWS];
    private bool Dirty;
    private int RowCount;
    private int Scroll;

    private GroupChangedHandler GroupChangedHandler { get; }
    private GroupMembersChangedHandler MembersChangedHandler { get; }

    public UIButton? OkButton { get; }

    public GroupTab()
        : base("_ngcdlg0", false)
    {
        Name = "Group";
        Visible = false;
        UsesControlStack = true;

        //position at top-left of screen
        X = 0;
        Y = 0;

        OkButton = CreateButton("BTN_OK");

        if (OkButton is not null)
            OkButton.Clicked += () =>
            {
                Hide();
                OnClose?.Invoke();
            };

        //quit button textures from b_btn0: 0=normal, 1=pressed, 2=disabled
        var cache = UiRenderer.Instance!;
        var pressedTexture = cache.GetPrefabTexture("_ngcdlg0", "B_BTN0", 1);
        var disabledTexture = cache.GetPrefabTexture("_ngcdlg0", "B_BTN0", 2);

        for (var i = 0; i < VISIBLE_ROWS; i++)
        {
            var controlName = $"USER{i}";
            var label = CreateLabel(controlName);

            if (label is not null)
                MemberLabels[i] = label;
            else
            {
                MemberLabels[i] = new UILabel
                {
                    Name = controlName,
                    X = NAME_X,
                    Y = NAME_START_Y + (i * ROW_HEIGHT),
                    Width = 72,
                    Height = 12
                };

                AddChild(MemberLabels[i]!);
            }

            var quitButton = new UIButton
            {
                Name = $"QUIT{i}",
                X = QUIT_BTN_X,
                Y = QUIT_BTN_START_Y + (i * ROW_HEIGHT),
                Width = QUIT_BTN_WIDTH,
                Height = QUIT_BTN_HEIGHT,
                NormalTexture = disabledTexture,
                PressedTexture = pressedTexture,
                Enabled = false
            };

            var moveButton = new RaidMoveButton
            {
                Name = $"MOVE{i}",
                X = MOVE_BTN_X,
                Y = MOVE_BTN_START_Y + (i * ROW_HEIGHT),
                Width = MOVE_BTN_WIDTH,
                Height = MOVE_BTN_HEIGHT,
                Visible = false
            };

            //capture the row for the click handlers
            var row = i;

            quitButton.Clicked += () =>
            {
                if (RowNames[row] is { } name)
                    OnKick?.Invoke(name);
            };

            moveButton.Clicked += () =>
            {
                if (RowNames[row] is { } name && (RowMoveTargets[row] > 0))
                    OnMove?.Invoke(name, RowMoveTargets[row]);
            };

            QuitButtons[i] = quitButton;
            MoveButtons[i] = moveButton;
            AddChild(quitButton);
            AddChild(moveButton);
        }

        GroupChangedHandler = () => Dirty = true;
        MembersChangedHandler = () => Dirty = true;
        WorldState.Group.Changed += GroupChangedHandler;
        WorldState.GroupMembers.Changed += MembersChangedHandler;
        Dirty = true;
    }

    public override void Dispose()
    {
        WorldState.Group.Changed -= GroupChangedHandler;
        WorldState.GroupMembers.Changed -= MembersChangedHandler;

        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
    }

    public event CloseHandler? OnClose;
    public event GroupKickHandler? OnKick;

    /// <summary>Raised with a member's name and the party to move them to.</summary>
    public event Action<string, int>? OnMove;

    /// <summary>
    ///     The window's rows: a raid's headings and members, or the plain group's names as the self-profile gave them.
    /// </summary>
    private static IReadOnlyList<RaidRosterLine> BuildRows()
    {
        if (WorldState.GroupMembers.IsRaid)
        {
            var self = WorldState.Attributes.Current;

            return RaidRoster.Lines(
                RaidRoster.Entries(
                    WorldState.GroupMembers,
                    WorldState.PlayerName,
                    WorldState.Group.IsLeader,
                    self?.CurrentHp ?? 0,
                    self?.MaximumHp ?? 0));
        }

        var leaderName = WorldState.Group.LeaderName;

        return WorldState.Group
                         .Members
                         .Take(MAX_MEMBERS)
                         .Select(name => new RaidRosterLine(1, name, (leaderName is not null) && name.EqualsI(leaderName), false))
                         .ToList();
    }

    private void Refresh()
    {
        Dirty = false;

        var rows = BuildRows();
        RowCount = rows.Count;
        Scroll = Math.Clamp(Scroll, 0, Math.Max(0, RowCount - VISIBLE_ROWS));

        var isRaid = WorldState.GroupMembers.IsRaid;
        var isLeader = WorldState.Group.IsLeader;
        var playerName = WorldState.PlayerName;

        var cache = UiRenderer.Instance!;
        var normalTexture = cache.GetPrefabTexture("_ngcdlg0", "B_BTN0", 0);
        var disabledTexture = cache.GetPrefabTexture("_ngcdlg0", "B_BTN0", 2);

        for (var i = 0; i < VISIBLE_ROWS; i++)
        {
            var row = (Scroll + i) < rows.Count ? rows[Scroll + i] : null;
            var isMember = row is { Name: not null };

            RowNames[i] = row?.Name;

            //an absent member may be offline, where the server cannot find them by name to move
            RowMoveTargets[i] = isRaid && isLeader && isMember && !row!.Absent
                ? WorldState.GroupMembers.NextPartyWithRoom(row.Party)
                : 0;

            if (MemberLabels[i] is { } label)
            {
                label.Text = row switch
                {
                    null             => string.Empty,
                    { Name: null }   => $"Party {row.Party}",
                    { Absent: true } => $"{row.Name} (away)",
                    _                => row.Name!
                };

                label.ForegroundColor = row is { Name: null } or { IsLeader: true } ? LegendColors.White : TextColors.Default;
            }

            //an absent member may be offline, where the server cannot find them by name to kick
            var canKick = isLeader && isMember && !row!.Absent && !row.Name!.EqualsI(playerName);

            QuitButtons[i].Visible = row is not ({ Name: null } or { Absent: true });
            QuitButtons[i].Enabled = canKick;
            QuitButtons[i].NormalTexture = canKick ? normalTexture : disabledTexture;

            MoveButtons[i].Visible = RowMoveTargets[i] > 0;
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (!Visible || !Enabled)
            return;

        if (Dirty)
            Refresh();

        base.Update(gameTime);
    }

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        if (RowCount <= VISIBLE_ROWS)
            return;

        Scroll = Math.Clamp(Scroll - e.Delta, 0, RowCount - VISIBLE_ROWS);
        Dirty = true;
        e.Handled = true;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            OnClose?.Invoke();
            e.Handled = true;
        }
    }
}
