#region
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Chaos.Client.Collections;
using DALib.Utility;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Networking;
using Chaos.Client.Networking.Definitions;
using Chaos.Client.Screens;
using Chaos.Client.Systems;
using Chaos.Cryptography;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using DALib.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SkiaSharp;
#endregion

namespace Chaos.Client;

public sealed class ChaosGame : Game
{
    public const int VIRTUAL_WIDTH = 640;
    public const int VIRTUAL_HEIGHT = 480;
    private const float ASPECT_RATIO = (float)VIRTUAL_WIDTH / VIRTUAL_HEIGHT;

    private readonly GraphicsDeviceManager Graphics;
    private readonly string MetaFilePath = Path.Combine(GlobalSettings.DataPath, "metafile");
    private readonly Dictionary<string, uint> MetaPendingChecksums = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ServerPacket> PacketBuffer = [];
    private MouseCursor? ArrowCursor;
    private MouseCursor? HandCursor;
    private Texture2D? ArrowBase;
    private Texture2D? HandBase;
    private float CursorScale;
    internal volatile bool GcRequested;
    private bool ScreenshotRequested;
    private bool MetaSyncStarted;
    private RenderTarget2D RenderTarget = null!;
    private bool ResizingInProgress;
    private ScreenMode CurrentScreenMode = ScreenMode.Windowed1x;
    private SpriteBatch SpriteBatch = null!;

    /// <summary>
    ///     Input dispatcher that routes mouse and keyboard events to UI elements via hit-testing and focus routing.
    /// </summary>
    public InputDispatcher Dispatcher { get; private set; } = null!;

    /// <summary>
    ///     The screen manager that owns the active screen stack.
    /// </summary>
    public ScreenManager Screens { get; private set; } = null!;

    public bool UseHandCursor
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            ApplyActiveCursor(); //swap arrow/hand on hover-state change
        }
    }

    /// <summary>
    ///     Shared aisling renderer for compositing player/NPC equipment layers.
    /// </summary>
    public AislingRenderer AislingRenderer { get; } = new();

    /// <summary>
    ///     The connection manager that orchestrates lobby, login, and world connections.
    /// </summary>
    public ConnectionManager Connection { get; }

    /// <summary>
    ///     Shared creature sprite renderer with per-frame texture cache.
    /// </summary>
    public CreatureRenderer CreatureRenderer { get; } = new();

    /// <summary>
    ///     Shared spell/effect animation renderer with per-frame texture cache.
    /// </summary>
    public EffectRenderer EffectRenderer { get; } = new();

    /// <summary>
    ///     Shared item sprite renderer with frame offset metadata. Evicted on map change.
    /// </summary>
    public ItemRenderer ItemRenderer { get; } = new();

    /// <summary>
    ///     Manages sound effect and music playback.
    /// </summary>
    public SoundSystem SoundSystem { get; } = new();

    public static GraphicsDevice Device => TextureConverter.Device;

    public ChaosGame()
    {
        //sdl by default is polling all possible input devices
        //some devices apparently don't like to always respond in a timely manner
        //when this occurs it causes the entire application to hang
        //to remedy this, we use this to disable polling of extraneous devices
        Sdl.SDL_QuitSubSystem(
            Sdl.SDL_INIT_JOYSTICK
            | Sdl.SDL_INIT_GAMECONTROLLER
            | Sdl.SDL_INIT_HAPTIC
            | Sdl.SDL_INIT_SENSOR);

        ClientSettings.Load();

        Graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = VIRTUAL_WIDTH,
            PreferredBackBufferHeight = VIRTUAL_HEIGHT,
            PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            SynchronizeWithVerticalRetrace = false
        };

        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        InactiveSleepTime = TimeSpan.Zero;

        Connection = new ConnectionManager();
        Directory.CreateDirectory(MetaFilePath);
        Connection.OnMetaData += HandleMetaData;
        Connection.OnWorldEntryComplete += () => Connection.SendMetaDataRequest(MetaDataRequestType.AllCheckSums);
        Connection.StateChanged += OnConnectionStateChanged;

        //wire state events to worldstate at startup so state is tracked
        //even during world entry (before worldscreen is created)
        WorldState.SubscribeTo(Connection);
        Connection.OnDisplayVisibleEntities += WorldState.AddOrUpdateVisibleEntities;
        Connection.OnDisplayAisling += WorldState.AddOrUpdateAisling;
        Connection.OnSetEntityTint += WorldState.SetEntityTint;

        //removeentity wired in worldscreen — it needs to capture the creature sprite for
        //the death dissolve animation before removing the entity from worldstate.
        //fallback for non-world screens (e.g., during world entry before worldscreen exists).
        Connection.OnRemoveEntity += id =>
        {
            if (Screens.ActiveScreen is not WorldScreen)
                WorldState.RemoveEntity(id);
        };

        Connection.OnCreatureWalk += (
            id,
            oldX,
            oldY,
            dir) =>
        {
            var entity = WorldState.GetEntity(id);
            var walkFrames = entity is not null && (entity.SpriteId > 0) ? CreatureRenderer.GetWalkFrameCount(entity.SpriteId) : null;

            WorldState.HandleCreatureWalk(
                id,
                oldX,
                oldY,
                dir,
                walkFrames);
        };
        Connection.OnCreatureTurn += (id, dir) => WorldState.HandleCreatureTurn(id, dir);

        Window.Title = "Unora";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }

    protected override void Draw(GameTime gameTime)
    {
        //render everything at virtual resolution
        GraphicsDevice.SetRenderTarget(RenderTarget);
        GraphicsDevice.Clear(Color.Black);
        Screens.Draw(SpriteBatch, gameTime);

        if (DebugOverlay.IsActive)
            DebugOverlay.DrawStats(SpriteBatch);

        //hardware cursor scales with the window; the rebuild only fires on an actual scale change
        RefreshCursorScale();

        //capture screenshot while the render target is still bound — DiscardContents may
        //invalidate pixel data after SetRenderTarget(null) on some drivers
        if (ScreenshotRequested)
        {
            ScreenshotRequested = false;
            SaveScreenshot();
        }

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black); //paint letterbox bars (and any unfilled backbuffer) black
        SpriteBatch.Begin(samplerState: GlobalSettings.Sampler);
        SpriteBatch.Draw(RenderTarget, GetPresentRect(), Color.White);
        SpriteBatch.End();

        base.Draw(gameTime);

        DebugOverlay.EndFrame();
    }

    protected override void EndDraw()
    {
        base.EndDraw();

        if (GcRequested)
        {
            GcRequested = false;

            GC.Collect(
                2,
                GCCollectionMode.Aggressive,
                true,
                true);

            GC.WaitForPendingFinalizers();
        }
    }

    public void RequestScreenshot() => ScreenshotRequested = true;

    private void SaveScreenshot()
    {
        var dataPath = GlobalSettings.DataPath;
        var highestNumber = 0;

        foreach (var file in Directory.EnumerateFiles(dataPath, "lod*.*"))
        {
            var name = Path.GetFileNameWithoutExtension(file);

            if ((name.Length >= 4) && int.TryParse(name.AsSpan(3), out var num) && (num > highestNumber))
                highestNumber = num;
        }

        var nextNumber = highestNumber + 1;
        var fileName = Path.Combine(dataPath, $"lod{nextNumber:D3}.png");

        var pixels = new Color[VIRTUAL_WIDTH * VIRTUAL_HEIGHT];
        RenderTarget.GetData(pixels);

        var imageInfo = new SKImageInfo(VIRTUAL_WIDTH, VIRTUAL_HEIGHT, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var sourceImage = SKImage.FromPixelCopy(
            imageInfo,
            MemoryMarshal.AsBytes(pixels.AsSpan()),
            VIRTUAL_WIDTH * 4);

        using var intermediary = ImageProcessor.PreserveNonTransparentBlacks(sourceImage);
        using var quantized = ImageProcessor.Quantize(QuantizerOptions.Default, intermediary);
        var palette = quantized.Palette;
        var indices = quantized.Entity.GetPalettizedPixelData(palette);

        var rgbPalette = new List<uint>(palette.Count);

        for (var i = 0; i < palette.Count; i++)
        {
            var c = palette[i];
            rgbPalette.Add(((uint)c.Red << 16) | ((uint)c.Green << 8) | c.Blue);
        }

        WritePalettizedPng(fileName, VIRTUAL_WIDTH, VIRTUAL_HEIGHT, indices, rgbPalette);
    }

    private static void WritePalettizedPng(string fileName, int width, int height, byte[] indices, List<uint> palette)
    {
        using var file = File.Create(fileName);

        //PNG signature
        file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        //IHDR — width, height, 8-bit indexed color
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; //bit depth
        ihdr[9] = 3; //color type: indexed
        WritePngChunk(file, "IHDR"u8, ihdr);

        //PLTE — RGB triplets
        var plte = new byte[palette.Count * 3];

        for (var i = 0; i < palette.Count; i++)
        {
            var rgb = palette[i];
            plte[i * 3] = (byte)(rgb >> 16);
            plte[i * 3 + 1] = (byte)(rgb >> 8);
            plte[i * 3 + 2] = (byte)rgb;
        }

        WritePngChunk(file, "PLTE"u8, plte);

        //IDAT — zlib-compressed scanlines with no-filter bytes
        using var idatBuffer = new MemoryStream();

        using (var zlib = new ZLibStream(idatBuffer, CompressionLevel.Optimal, true))
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0); //filter: none
                zlib.Write(indices, y * width, width);
            }

        WritePngChunk(file, "IDAT"u8, idatBuffer.ToArray());

        //IEND
        WritePngChunk(file, "IEND"u8, []);
    }

    private static void WritePngChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> buf = stackalloc byte[4];

        //chunk length (big-endian)
        BinaryPrimitives.WriteInt32BigEndian(buf, data.Length);
        stream.Write(buf);

        //chunk type
        stream.Write(type);

        //chunk data
        stream.Write(data);

        //CRC32 over type + data (PNG uses the standard CRC32 polynomial)
        var crc = 0xFFFFFFFFu;

        foreach (var b in type)
            crc = PngCrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);

        foreach (var b in data)
            crc = PngCrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);

        BinaryPrimitives.WriteUInt32BigEndian(buf, crc ^ 0xFFFFFFFF);
        stream.Write(buf);
    }

    private static readonly uint[] PngCrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];

        for (uint n = 0; n < 256; n++)
        {
            var c = n;

            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;

            table[n] = c;
        }

        return table;
    }

    private static (int X, int Y) FindCursorHotspot(Color[] pixels, int width, int height)
    {
        var hotX = width;
        var hotY = height;

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (pixels[(y * width) + x].A > 0)
                {
                    if (x < hotX)
                        hotX = x;

                    if (y < hotY)
                        hotY = y;
                }

        //clamp so a fully-transparent frame can't return an out-of-range origin (FromTexture2D would throw)
        return (Math.Min(hotX, width - 1), Math.Min(hotY, height - 1));
    }

    protected override void Initialize()
    {
        base.Initialize();

        Window.ClientSizeChanged += OnClientSizeChanged;

        DisplaySettings.Applier = ApplyScreenMode;
        ApplyScreenMode(ClientSettings.ScreenMode);
    }

    protected override void LoadContent()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice);

        RenderTarget = new RenderTarget2D(
            GraphicsDevice,
            VIRTUAL_WIDTH,
            VIRTUAL_HEIGHT,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24Stencil8);
        InputBuffer.Initialize();
        Dispatcher = new InputDispatcher();
        Screens = new ScreenManager(this);

        TextureConverter.Device = GraphicsDevice;
        FontAtlas.Initialize(GraphicsDevice);
        DamageNumberFont.Initialize(GraphicsDevice);
        CooldownNumberFont.Initialize(GraphicsDevice);
        UiRenderer.Instance = new UiRenderer(GraphicsDevice);

        //opt our hardware cursor out of Win11's DPI + accessibility "pointer size" scaling before we build it,
        //so the OS doesn't multiply our already-window-scaled cursor into an oversized one
        Win32Cursor.DisableOsScalingForThisThread();
        LoadCustomCursor();

        Screens.Switch(new LobbyLoginScreen());
    }

    private void LoadCustomCursor()
    {
        //Hardware cursor: OS-composited at pointer-poll latency. A missing mouse.epf frame resolves to UiRenderer's
        //checkerboard, so ResolveCursorFrame maps that to null and we keep the OS default instead of showing garbage.
        ArrowBase = ResolveCursorFrame(0);
        HandBase = ResolveCursorFrame(1);

        RefreshCursorScale();
    }

    //The mouse.epf frame, or null when the asset is missing (GetEpfTexture yields the shared checkerboard placeholder).
    private static Texture2D? ResolveCursorFrame(int frame)
    {
        var texture = UiRenderer.Instance!.GetEpfTexture("mouse.epf", frame);

        return ReferenceEquals(texture, UiRenderer.Instance.MissingTexture) ? null : texture;
    }

    /// <summary>
    ///     Rebuilds the hardware cursor(s) so the pointer scales with the window, matching the point-sampled game
    ///     content. Polled from Draw, but only the scale compare runs unless the scale actually changed.
    /// </summary>
    private void RefreshCursorScale()
    {
        if (ArrowBase is null)
            return;

        var present = GetPresentRect();
        var scale = MathF.Max(1f, MathF.Min((float)present.Width / VIRTUAL_WIDTH, (float)present.Height / VIRTUAL_HEIGHT));

        //ponytail: 1%-granularity gate; a live window-drag rebuilds a handful of times, each a tiny-texture op
        if (MathF.Abs(scale - CursorScale) < 0.01f)
            return;

        CursorScale = scale;

        ArrowCursor?.Dispose();
        ArrowCursor = BuildScaledCursor(ArrowBase, scale);

        if (HandBase is not null)
        {
            HandCursor?.Dispose();
            HandCursor = BuildScaledCursor(HandBase, scale);
        }

        ApplyActiveCursor();
    }

    //Sets the OS cursor to the active frame — hand while hovering an entity, else arrow. No-op until the arrow is
    //built, so a missing mouse.epf leaves the OS default in place.
    private void ApplyActiveCursor()
    {
        if (ArrowCursor is null)
            return;

        Mouse.SetCursor(UseHandCursor && HandCursor is not null ? HandCursor : ArrowCursor);
    }

    //Nearest-neighbor upscale of a cursor frame to the (possibly fractional) window scale, into a hardware cursor —
    //PointClamp parity with the content, so borderless/free-dragged sizes match too. The hotspot scales with the art
    //so the origin still lands on the click point; the temp texture is copied into the SDL cursor, then freed.
    private MouseCursor BuildScaledCursor(Texture2D baseTex, float scale)
    {
        var srcW = baseTex.Width;
        var srcH = baseTex.Height;
        var src = new Color[srcW * srcH];
        baseTex.GetData(src);

        var (hotX, hotY) = FindCursorHotspot(src, srcW, srcH);

        var dstW = Math.Max(1, (int)MathF.Round(srcW * scale));
        var dstH = Math.Max(1, (int)MathF.Round(srcH * scale));

        if ((dstW == srcW) && (dstH == srcH))
            return MouseCursor.FromTexture2D(baseTex, hotX, hotY);

        var dst = new Color[dstW * dstH];

        for (var y = 0; y < dstH; y++)
        {
            var srcRow = ((y * srcH) / dstH) * srcW;

            for (var x = 0; x < dstW; x++)
                dst[(y * dstW) + x] = src[srcRow + ((x * srcW) / dstW)];
        }

        using var scaled = new Texture2D(GraphicsDevice, dstW, dstH);
        scaled.SetData(dst);

        var scaledHotX = Math.Min(dstW - 1, (int)MathF.Round(hotX * scale));
        var scaledHotY = Math.Min(dstH - 1, (int)MathF.Round(hotY * scale));

        return MouseCursor.FromTexture2D(scaled, scaledHotX, scaledHotY);
    }

    #region Window Sizing
    /// <summary>
    ///     Applies a <see cref="ScreenMode" /> to the window: windowed integer multiples of 640x480
    ///     (clamped to the current monitor), or borderless fullscreen at the desktop resolution. The
    ///     letterbox-vs-stretch difference is applied later in the Draw present-rect, not here.
    /// </summary>
    internal void ApplyScreenMode(ScreenMode mode)
    {
        CurrentScreenMode = mode;
        ResizingInProgress = true;

        if (mode is ScreenMode.BorderlessLetterbox or ScreenMode.BorderlessStretch)
        {
            (var w, var h) = GetCurrentDisplaySize();
            Graphics.HardwareModeSwitch = false; //MonoGame idiom: borderless windowed fullscreen
            Graphics.IsFullScreen = true;
            Graphics.PreferredBackBufferWidth = w;
            Graphics.PreferredBackBufferHeight = h;
        } else
        {
            var multiplier = ClampMultiplierToDisplay(ModeToMultiplier(mode));

            if (Graphics.IsFullScreen)
            {
                Graphics.IsFullScreen = false;
                Graphics.HardwareModeSwitch = true;
            }

            //leave maximized state so the backbuffer resize actually shrinks the OS window
            if ((Sdl.SDL_GetWindowFlags(Window.Handle) & Sdl.SDL_WINDOW_MAXIMIZED) != 0)
                Sdl.SDL_RestoreWindow(Window.Handle);

            Graphics.PreferredBackBufferWidth = VIRTUAL_WIDTH * multiplier;
            Graphics.PreferredBackBufferHeight = VIRTUAL_HEIGHT * multiplier;
        }

        Graphics.ApplyChanges();
        ResizingInProgress = false;
    }

    private static int ModeToMultiplier(ScreenMode mode)
        => mode switch
        {
            ScreenMode.Windowed2x => 2,
            ScreenMode.Windowed3x => 3,
            ScreenMode.Windowed4x => 4,
            _                     => 1
        };

    //Bounds of the monitor the window currently sits on; falls back to the current backbuffer.
    private (int W, int H) GetCurrentDisplaySize()
    {
        var displayIndex = Sdl.SDL_GetWindowDisplayIndex(Window.Handle);

        if ((displayIndex >= 0) && (Sdl.SDL_GetDisplayBounds(displayIndex, out var bounds) >= 0))
            return (bounds.W, bounds.H);

        return (Graphics.PreferredBackBufferWidth, Graphics.PreferredBackBufferHeight);
    }

    //Largest integer multiple <= the requested one that still fits the current monitor (never below 1x).
    private int ClampMultiplierToDisplay(int multiplier)
    {
        (var w, var h) = GetCurrentDisplaySize();

        while ((multiplier > 1) && (((VIRTUAL_WIDTH * multiplier) > w) || ((VIRTUAL_HEIGHT * multiplier) > h)))
            multiplier--;

        return multiplier;
    }

    //The rectangle the 640x480 render target is presented into on the backbuffer. Borderless-letterbox
    //centers an aspect-fit (4:3) rect with black bars; every other mode fills the whole backbuffer
    //(windowed modes use an exact integer multiple, so "fill" is already pixel-perfect).
    private Rectangle GetPresentRect()
    {
        var ppt = GraphicsDevice.PresentationParameters;
        var bbW = ppt.BackBufferWidth;
        var bbH = ppt.BackBufferHeight;

        if (CurrentScreenMode != ScreenMode.BorderlessLetterbox)
            return new Rectangle(0, 0, bbW, bbH);

        var scale = Math.Min((float)bbW / VIRTUAL_WIDTH, (float)bbH / VIRTUAL_HEIGHT);
        var w = (int)MathF.Round(VIRTUAL_WIDTH * scale);
        var h = (int)MathF.Round(VIRTUAL_HEIGHT * scale);

        return new Rectangle((bbW - w) / 2, (bbH - h) / 2, w, h);
    }

    /// <summary>
    ///     Resize hotkey: advances through the windowed sizes (1x → 2x → 3x → 4x), skipping any that
    ///     don't fit the current monitor and wrapping back to 1x. Routes through
    ///     <see cref="DisplaySettings.Apply" /> so the F4 Resolution dropdown and the hotkey stay in
    ///     sync (and the choice persists). Borderless modes are dropdown-only.
    /// </summary>
    internal void CycleWindowSize()
    {
        var next = CurrentScreenMode switch
        {
            ScreenMode.Windowed1x => ScreenMode.Windowed2x,
            ScreenMode.Windowed2x => ScreenMode.Windowed3x,
            ScreenMode.Windowed3x => ScreenMode.Windowed4x,
            _                     => ScreenMode.Windowed1x //from 4x or either borderless → back to 1x
        };

        //if the next multiple doesn't fit this monitor, wrap to 1x.
        var multiplier = ModeToMultiplier(next);

        if (ClampMultiplierToDisplay(multiplier) != multiplier)
            next = ScreenMode.Windowed1x;

        DisplaySettings.Apply((int)next);
    }


    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        if (ResizingInProgress)
            return;

        var width = Window.ClientBounds.Width;
        var height = Window.ClientBounds.Height;

        if ((width <= 0) || (height <= 0))
            return;

        //maximize button → fill the full monitor work area; skip 4:3 correction and let the
        //Draw path letterbox the 640×480 render target inside the non-4:3 window.
        var flags = Sdl.SDL_GetWindowFlags(Window.Handle);

        if ((flags & Sdl.SDL_WINDOW_MAXIMIZED) != 0)
            return;

        //borderless fullscreen owns the whole monitor — skip 4:3 correction (it would fight fullscreen).
        if (CurrentScreenMode is ScreenMode.BorderlessLetterbox or ScreenMode.BorderlessStretch)
            return;

        //determine corrected dimensions preserving 4:3
        var correctedWidth = (int)(height * ASPECT_RATIO);
        var correctedHeight = (int)(width / ASPECT_RATIO);

        int newWidth,
            newHeight;

        if (correctedWidth <= width)
        {
            //height is the constraining dimension
            newWidth = correctedWidth;
            newHeight = height;
        } else
        {
            //width is the constraining dimension
            newWidth = width;
            newHeight = correctedHeight;
        }

        if ((newWidth == width) && (newHeight == height))
            return;

        ResizingInProgress = true;

        Graphics.PreferredBackBufferWidth = newWidth;
        Graphics.PreferredBackBufferHeight = newHeight;
        Graphics.ApplyChanges();

        ResizingInProgress = false;
    }
    #endregion Window Sizing

    /// <summary>
    ///     Fired when all metadata files are up to date with the server.
    /// </summary>
    public event MetaDataSyncCompleteHandler? OnMetaDataSyncComplete;

    private void OnConnectionStateChanged(ConnectionState oldState, ConnectionState newState)
    {
        if (newState == ConnectionState.World)
            LatencyMonitor.Start(Connection.Client);
        else if (oldState == ConnectionState.World)
            LatencyMonitor.Stop();
    }

    protected override void UnloadContent()
    {
        Window.ClientSizeChanged -= OnClientSizeChanged;
        DisplaySettings.Applier = null;
        ArrowCursor?.Dispose();
        HandCursor?.Dispose();
        RenderTarget.Dispose();
        Screens.Dispose();
        Connection.Dispose();
        InputBuffer.Shutdown();
        CreatureRenderer.Dispose();
        AislingRenderer.Dispose();
        EffectRenderer.Dispose();
        ItemRenderer.Dispose();
        SoundSystem.Dispose();
        UiRenderer.Instance?.Dispose();
        UiRenderer.Instance = null;
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        DebugOverlay.BeginFrame();

        //compute mouse coordinate transform from the same present-rect the render target draws into,
        //so cursor→virtual mapping is correct in every mode — including borderless letterbox, where the
        //rect is centered with black bars (non-zero offset) rather than filling the backbuffer.
        var present = GetPresentRect();
        var scaleX = (float)present.Width / VIRTUAL_WIDTH;
        var scaleY = (float)present.Height / VIRTUAL_HEIGHT;
        InputBuffer.SetVirtualScale(scaleX, scaleY, present.X, present.Y);

        //freeze buffered input for this frame before anything reads it
        InputBuffer.Update(IsActive);

        //f11 — toggle debug overlay (handled globally before screen update)
        if (InputBuffer.WasScancodePressed(Scancode.F11))
            DebugOverlay.Toggle();

        //f12 — screenshot
        if (InputBuffer.WasScancodePressed(Scancode.F12))
            RequestScreenshot();

        DebugOverlay.Update(gameTime);

        //pump audio decodes and reset the same-frame dedup window before any handler can trigger sounds
        SoundSystem.Update();

        //drain and process network packets each frame
        PacketBuffer.Clear();
        Connection.ProcessPackets(PacketBuffer);

        Screens.Update(gameTime);

        base.Update(gameTime);
    }

    #region Metadata Sync
    private uint ComputeLocalMetaCheckSum(string name)
    {
        var filePath = Path.Combine(MetaFilePath, name);

        if (!File.Exists(filePath))
            return 0;

        try
        {
            using var fileStream = File.OpenRead(filePath);
            using var zlibStream = new ZLibStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();

            zlibStream.CopyTo(memoryStream);

            return Crc.Generate32(memoryStream.ToArray());
        } catch
        {
            return 0;
        }
    }

    private void HandleMetaData(MetaDataArgs args)
    {
        switch (args.MetaDataRequestType)
        {
            case MetaDataRequestType.AllCheckSums:
                HandleMetaDataCheckSums(args.MetaDataCollection);

                break;

            case MetaDataRequestType.DataByName:
                HandleMetaDataFileData(args.MetaDataInfo);

                break;
        }
    }

    private void HandleMetaDataCheckSums(ICollection<MetaDataInfo>? collection)
    {
        if (collection is null || (collection.Count == 0))
        {
            OnMetaDataSyncComplete?.Invoke();

            return;
        }

        MetaPendingChecksums.Clear();
        MetaSyncStarted = true;

        foreach (var info in collection)
        {
            var localCheckSum = ComputeLocalMetaCheckSum(info.Name);

            if (localCheckSum != info.CheckSum)
                MetaPendingChecksums[info.Name] = info.CheckSum;
        }

        foreach (var name in MetaPendingChecksums.Keys)
            Connection.SendMetaDataRequest(MetaDataRequestType.DataByName, name);

        if (MetaPendingChecksums.Count == 0)
            OnMetaDataSyncComplete?.Invoke();
    }

    private void HandleMetaDataFileData(MetaDataInfo? info)
    {
        if (info is null || string.IsNullOrEmpty(info.Name) || (info.Data.Length == 0))
            return;

        File.WriteAllBytes(Path.Combine(MetaFilePath, info.Name), info.Data);
        MetaPendingChecksums.Remove(info.Name);

        if (MetaSyncStarted && (MetaPendingChecksums.Count == 0))
            OnMetaDataSyncComplete?.Invoke();
    }
    #endregion
}