#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.LobbyLogin;

public sealed class ServerSelectControl : PrefabPanel
{
    private const int ROW_HEIGHT = 20;
    private const int FIRST_ROW_Y = 37;
    private const int NAME_X = 28;
    private const int DESC_X = 108;

    //retail (lsvr) box pieces and the first row's text slots; BoxTop is null on the current look
    private readonly Texture2D? BoxTop;
    private readonly Texture2D? BoxStrip;
    private readonly Texture2D? BoxBottom;
    private readonly int BoxWidth;
    private readonly int TopHeight;
    private readonly int StripHeight;
    private readonly int BottomHeight;
    private readonly Rectangle FirstName;
    private readonly Rectangle FirstDescription;
    private readonly int Pitch = ROW_HEIGHT;
    private Point BoxOrigin;
    private int PieceCount;

    private List<(UILabel Name, UILabel Description)> ServerLabels = [];

    private List<ServerTableEntry> Servers = [];

    public ServerSelectControl()
        : base(LoginLayouts.For("_nsvr"))
    {
        Name = "ServerSelect";
        Visible = false;

        if (!LoginLayouts.StacksServers(PrefabSet.Name) || !PrefabSet.Contains("ServerTopImage"))
            return;

        var cache = UiRenderer.Instance!;
        BoxTop = cache.GetPrefabTexture(PrefabSet.Name, "ServerTopImage", 0);
        BoxStrip = cache.GetPrefabTexture(PrefabSet.Name, "ServerMidImage", 0);
        BoxBottom = cache.GetPrefabTexture(PrefabSet.Name, "ServerBotImage", 0);

        var topRect = GetRect("ServerTopImage");
        BoxWidth = topRect.Width;
        TopHeight = topRect.Height;
        StripHeight = GetRect("ServerMidImage").Height;
        BottomHeight = GetRect("ServerBotImage").Height;
        FirstName = GetRect("ServerTop");
        FirstDescription = GetRect("ServerATop");

        var pitch = GetRect("ServerBottom").Y - FirstName.Y;

        if (pitch > 0)
            Pitch = pitch;
    }

    private bool Stacked => BoxTop is not null;

    public event ServerSelectedHandler? OnServerSelected;

    public void SetServers(List<ServerTableEntry> entries)
    {
        //remove previous server labels and box pieces
        foreach ((var nameLabel, var descLabel) in ServerLabels)
        {
            RemoveChild(nameLabel.Name);
            RemoveChild(descLabel.Name);
        }

        for (var i = 0; i < PieceCount; i++)
            RemoveChild($"ServerPiece{i}");

        Servers = entries;
        ServerLabels = [];
        PieceCount = 0;

        if (Stacked)
            AddBoxPieces(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            var server = entries[i];
            var nameRect = NameRect(i);
            var descRect = DescriptionRect(i);

            var nameLabel = new UILabel
            {
                Name = $"ServerName{i}",
                X = nameRect.X,
                Y = nameRect.Y,
                Width = nameRect.Width,
                Height = nameRect.Height,
                Text = server.Name,
                ForegroundColor = LegendColors.White,
                PaddingLeft = 0,
                PaddingTop = 0
            };

            var descLabel = new UILabel
            {
                Name = $"ServerDesc{i}",
                X = descRect.X,
                Y = descRect.Y,
                Width = descRect.Width,
                Height = descRect.Height,
                Text = server.Description,
                ForegroundColor = Color.LightGray,
                PaddingLeft = 0,
                PaddingTop = 0
            };

            AddChild(nameLabel);
            AddChild(descLabel);
            ServerLabels.Add((nameLabel, descLabel));
        }
    }

    private void AddBoxPieces(int serverCount)
    {
        var strips = ServerListLayout.StripCount(serverCount);
        var boxHeight = ServerListLayout.BoxHeight(TopHeight, StripHeight, BottomHeight, strips);
        BoxOrigin = ServerListLayout.BoxOrigin(Width, Height, BoxWidth, boxHeight);

        var y = BoxOrigin.Y;
        AddPiece(BoxTop!, y, TopHeight);
        y += TopHeight;

        for (var s = 0; s < strips; s++)
        {
            AddPiece(BoxStrip!, y, StripHeight);
            y += StripHeight;
        }

        AddPiece(BoxBottom!, y, BottomHeight);
    }

    private void AddPiece(Texture2D texture, int y, int height)
        => AddChild(
            new UIImage
            {
                Name = $"ServerPiece{PieceCount++}",
                X = BoxOrigin.X,
                Y = y,
                Width = BoxWidth,
                Height = height,
                Texture = texture
            });

    private Rectangle NameRect(int row)
        => Stacked
            ? InBox(ServerListLayout.Row(FirstName, Pitch, row))
            : new Rectangle(NAME_X, FIRST_ROW_Y + row * ROW_HEIGHT, DESC_X - NAME_X, ROW_HEIGHT);

    private Rectangle DescriptionRect(int row)
        => Stacked
            ? InBox(ServerListLayout.Row(FirstDescription, Pitch, row))
            : new Rectangle(DESC_X, FIRST_ROW_Y + row * ROW_HEIGHT, Width - DESC_X, ROW_HEIGHT);

    private Rectangle HitRect(int row)
        => Stacked
            ? InBox(ServerListLayout.HitRow(FirstName, FirstDescription, Pitch, row))
            : new Rectangle(NAME_X, FIRST_ROW_Y + row * ROW_HEIGHT, Width - NAME_X, ROW_HEIGHT);

    private Rectangle InBox(Rectangle rect) => new(rect.X + BoxOrigin.X, rect.Y + BoxOrigin.Y, rect.Width, rect.Height);

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        var localX = e.ScreenX - ScreenX;
        var localY = e.ScreenY - ScreenY;

        for (var i = 0; i < Servers.Count; i++)
        {
            if (!HitRect(i).Contains(localX, localY))
                continue;

            OnServerSelected?.Invoke(Servers[i].Id);
            e.Handled = true;

            return;
        }
    }
}
