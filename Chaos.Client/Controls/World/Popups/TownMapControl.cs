#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Data;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     Display-only town map overlay composited from national.dat assets. Shows the player's current location on the
///     national map background with an animated player marker. Triggered by T key or HUD button, dismissed by Escape, T,
///     or any click.
/// </summary>
public sealed class TownMapControl : UIPanel
{
    private const int FRAME_WIDTH = 568;
    private const int FRAME_HEIGHT = 406;
    private const float MARKER_FRAME_INTERVAL = 0.083f;
    private const string PIN_FILE = "tmgroup.spf";
    private const int PIN_FRAME_COUNT = GroupMembersState.PARTY_SIZE * GroupMembersState.PARTY_COUNT;

    //a full raid less the player, who gets the marker instead
    private const int MAX_PINS = GroupMembersState.MAX_RAID_SIZE - 1;

    //the pin's tip is the bottom-centre pixel of its 7x25 frame
    private const int PIN_TIP_X = 3;
    private const int PIN_TIP_Y = 24;

    private record struct TownMapEntry(int MapId, int X, int Y, int TileWidth, int TileHeight);

    private List<TownMapEntry>? CoordEntries;

    //uiimage children — created on show, removed on hide
    private UIImage? BackgroundImage;
    private UIImage? IconBarImage;
    private UIImage? TownImageLayer;
    private UIImage? NameLabelImage;
    private UIImage? MarkerImage;

    //one image per possible pin, made on show and added before the marker so the marker draws over them
    private readonly List<UIImage> PinImages = [];
    private Texture2D[]? PinFrames;

    //the last pins actually laid out: rebuilt only when the group, the positions or the player change
    private IReadOnlyList<GroupPosition>? PinPositions;
    private IReadOnlyList<GroupMemberSnapshot>? PinMembers;
    private string? PinPlayerName;
    private IReadOnlyList<TownMapPin>? Pins;

    //marker animation frames (swapped onto markerimage.texture each tick)
    private Texture2D[]? MarkerFrames;

    //marker animation + projection state
    private int MarkerFrame;
    private float MarkerTimer;
    private TownMapEntry ActiveEntry;

    //click-to-dismiss tracking (requires down then up)
    private bool MouseDownReceived;

    public TownMapControl()
    {
        Width = FRAME_WIDTH;
        Height = FRAME_HEIGHT;
        X = (640 - FRAME_WIDTH) / 2;
        Y = (480 - FRAME_HEIGHT) / 2;
        Visible = false;
        UsesControlStack = true;
        ZIndex = 2;
    }

    /// <summary>
    ///     Shows the town map for the given map ID if a matching town image exists. Loads all assets from national.dat,
    ///     computes the player marker position, and sets Visible = true.
    /// </summary>
    public void Show(short mapId, int playerTileX, int playerTileY)
    {
        EnsureCoordsParsed();

        //find matching entry
        var entryIndex = -1;

        for (var i = 0; i < CoordEntries!.Count; i++)
            if (CoordEntries[i].MapId == mapId)
            {
                entryIndex = i;

                break;
            }

        if (entryIndex < 0)
            return;

        //check that the town spf exists before committing to show
        if (!DatArchives.National.TryGetValue($"_t{mapId}.spf", out _))
            return;

        var entry = CoordEntries[entryIndex];

        //clear previous children
        ClearLayers();

        //layer 1: background at (0, 0) — fills the panel
        BackgroundImage = LoadSpfAsImage("_t_back.spf");

        if (BackgroundImage is not null)
            AddChild(BackgroundImage);

        //layer 2: icon bar
        IconBarImage = LoadSpfAsImage("_t_icon.spf");

        if (IconBarImage is not null)
        {
            //center horizontally within the frame, position in lower area
            IconBarImage.X = (FRAME_WIDTH - IconBarImage.Width) / 2;
            IconBarImage.Y = 301;
            AddChild(IconBarImage);
        }

        //layer 3: town image — _tcoord.txt x is negated centering offset, y is vertical offset
        TownImageLayer = LoadSpfAsImage($"_t{mapId}.spf");

        if (TownImageLayer is not null)
        {
            TownImageLayer.X = -entry.X;
            TownImageLayer.Y = entry.Y;
            AddChild(TownImageLayer);
        }

        //layer 4: name label — centered horizontally, near top
        NameLabelImage = LoadSpfAsImage($"_t{mapId}n.spf");

        if (NameLabelImage is not null)
        {
            NameLabelImage.X = (FRAME_WIDTH - NameLabelImage.Width) / 2;
            NameLabelImage.Y = 24;
            AddChild(NameLabelImage);
        }

        //layer 5: group pins, under the player's marker
        ActiveEntry = entry;
        PinFrames = LoadPinFrames();

        if (PinFrames is not null && TownImageLayer is not null)
        {
            for (var i = 0; i < MAX_PINS; i++)
            {
                var pin = new UIImage
                {
                    Visible = false
                };

                PinImages.Add(pin);
                AddChild(pin);
            }

            UpdatePins();
        }

        //layer 6: player position marker
        ActiveEntry = entry;
        MarkerFrames = LoadEpfMarkerFrames("tmuser.epf");

        if (MarkerFrames is not null && TownImageLayer is not null)
        {
            MarkerImage = new UIImage
            {
                Texture = MarkerFrames[0],
                Width = MarkerFrames[0].Width,
                Height = MarkerFrames[0].Height
            };
            UpdateMarkerPosition(playerTileX, playerTileY);
            AddChild(MarkerImage);
        }

        MarkerFrame = 0;
        MarkerTimer = 0;
        MouseDownReceived = false;
        InputDispatcher.Instance?.PushControl(this);
        Visible = true;
    }

    /// <summary>
    ///     Hides the town map and disposes all loaded assets.
    /// </summary>
    public void Hide()
    {
        InputDispatcher.Instance?.RemoveControl(this);
        Visible = false;
        ClearLayers();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Hide();
        base.Dispose();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        //Escape cancels (label); T mirrors the world town-map hotkey, which is positional.
        if ((e.Keycode == Keycode.Escape) || (e.Scancode == Scancode.T))
        {
            Hide();
            e.Handled = true;
        }
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        MouseDownReceived = true;
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        base.OnMouseUp(e);

        if (MouseDownReceived)
            Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (!Visible)
            return;

        //update marker position to follow player
        var player = WorldState.GetPlayerEntity();

        if (player is not null)
            UpdateMarkerPosition(player.TileX, player.TileY);

        UpdatePins();

        //animate player marker
        MarkerTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (MarkerTimer >= MARKER_FRAME_INTERVAL)
        {
            MarkerTimer -= MARKER_FRAME_INTERVAL;
            MarkerFrame = (MarkerFrame + 1) % 7;

            if (MarkerImage is not null && MarkerFrames is not null)
                MarkerImage.Texture = MarkerFrames[MarkerFrame];
        }

        base.Update(gameTime);
    }

    #region Asset Loading
    private static UIImage LoadSpfAsImage(string fileName)
    {
        var texture = UiRenderer.Instance!.GetNationalSpfTexture(fileName);

        return new UIImage
        {
            Texture = texture,
            Width = texture.Width,
            Height = texture.Height
        };
    }

    private static Texture2D[]? LoadPinFrames()
    {
        //an older national.dat without the pins just shows the map without them
        if (!DatArchives.National.TryGetValue(PIN_FILE, out _))
            return null;

        var frames = new Texture2D[PIN_FRAME_COUNT];

        for (var i = 0; i < PIN_FRAME_COUNT; i++)
            frames[i] = UiRenderer.Instance!.GetNationalSpfTexture(PIN_FILE, i);

        return frames;
    }

    private static Texture2D[]? LoadEpfMarkerFrames(string fileName)
    {
        var frameCount = DataContext.UserControls.GetNationalEpfFrameCount(fileName);

        if (frameCount == 0)
            return null;

        var frames = new Texture2D[frameCount];

        for (var i = 0; i < frameCount; i++)
            frames[i] = UiRenderer.Instance!.GetNationalEpfTexture(fileName, i);

        return frames;
    }
    #endregion

    #region Coordinate Parsing
    private void EnsureCoordsParsed()
    {
        if (CoordEntries is not null)
            return;

        CoordEntries = [];

        if (!DatArchives.National.TryGetValue("_tcoord.txt", out var entry))
            return;

        var text = System.Text.Encoding.GetEncoding(949).GetString(entry.ToSpan());
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var numbers = ExtractNumbers(line);

            if (numbers.Count >= 5)
                CoordEntries.Add(
                    new TownMapEntry(
                        numbers[0],
                        numbers[1],
                        numbers[2],
                        numbers[3],
                        numbers[4]));
        }
    }

    private static List<int> ExtractNumbers(string line)
    {
        var numbers = new List<int>();
        var i = 0;

        while (i < line.Length)
        {
            if (!char.IsDigit(line[i]) && (line[i] != '-'))
            {
                i++;

                continue;
            }

            var start = i;

            if (line[i] == '-')
            {
                if (((i + 1) < line.Length) && char.IsDigit(line[i + 1]))
                    i++;
                else
                {
                    i++;

                    continue;
                }
            }

            while ((i < line.Length) && char.IsDigit(line[i]))
                i++;

            if (int.TryParse(line.AsSpan(start, i - start), out var value))
                numbers.Add(value);
        }

        return numbers;
    }
    #endregion

    #region Marker Projection
    private (int X, int Y)? Project(int tileX, int tileY)
        => TownImageLayer is null
            ? null
            : TownMapProjection.TileToPixel(
                TownImageLayer.X,
                TownImageLayer.Y,
                TownImageLayer.Width,
                TownImageLayer.Height,
                ActiveEntry.TileWidth,
                ActiveEntry.TileHeight,
                tileX,
                tileY);

    private void UpdateMarkerPosition(int playerTileX, int playerTileY)
    {
        if (MarkerImage is null || Project(playerTileX, playerTileY) is not { } point)
            return;

        MarkerImage.X = point.X - 6;
        MarkerImage.Y = point.Y - 19;
    }

    private void UpdatePins()
    {
        if (PinFrames is null || TownImageLayer is null)
            return;

        var members = WorldState.GroupMembers.Members;
        var positions = WorldState.GroupPositions.Members;
        var player = WorldState.PlayerName;

        if (!ReferenceEquals(members, PinMembers)
            || !ReferenceEquals(positions, PinPositions)
            || !string.Equals(player, PinPlayerName, StringComparison.Ordinal))
        {
            PinMembers = members;
            PinPositions = positions;
            PinPlayerName = player;
            Pins = TownMapPins.Build(WorldState.GroupMembers, positions, player);
        }

        var pins = Pins ?? [];
        var used = 0;

        foreach (var pin in pins)
        {
            if (used >= PinImages.Count)
                break;

            if (Project(pin.TileX, pin.TileY) is not { } point
                || !TownMapProjection.IsOnImage(
                    TownImageLayer.X,
                    TownImageLayer.Y,
                    TownImageLayer.Width,
                    TownImageLayer.Height,
                    point))
                continue;

            var texture = PinFrames[pin.Frame];
            var image = PinImages[used++];

            image.Texture = texture;
            image.Width = texture.Width;
            image.Height = texture.Height;
            image.X = point.X - PIN_TIP_X;
            image.Y = point.Y - PIN_TIP_Y;
            image.Visible = true;
        }

        for (var i = used; i < PinImages.Count; i++)
            PinImages[i].Visible = false;
    }
    #endregion

    #region Cleanup
    private void ClearLayers()
    {
        //all textures are cachedtexture2d from uirenderer — dispose is a no-op, just detach
        BackgroundImage = null;
        IconBarImage = null;
        TownImageLayer = null;
        NameLabelImage = null;
        MarkerImage = null;
        MarkerFrames = null;
        PinFrames = null;
        PinImages.Clear();

        //null out textures before clearing so uiimage.dispose doesn't try to release cached textures
        foreach (var child in Children)
            if (child is UIImage image)
                image.Texture = null;

        Children.Clear();
    }
    #endregion

}