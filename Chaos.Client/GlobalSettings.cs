#region
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Chaos.Client.Data;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client;

/// <summary>
///     Static configuration for the client: version, data path, lobby host/port, and sampler state. Triggers all one-time
///     initialization (encoding providers, data archives, text colors) via the static constructor.
/// </summary>
public static class GlobalSettings
{
    private static readonly string[] PreLoadedAssemblies = ["Chaos.Networking"];
    private static readonly Type[] PreInitializedStatics = [typeof(DataContext), typeof(MachineIdentity)];
    public static readonly SamplerState Sampler = SamplerState.PointClamp; //SamplerState.LinearClamp;
    private static ushort ClientVersion => CONSTANTS.CLIENT_VERSION;

    /// <summary>
    ///     The game-data folder: the first candidate that holds the archives. A launcher can pass a folder that does not
    ///     exist (DA_PATH), so the folder next to the executable and its parent are also tried before the dev default.
    ///     When none holds the archives this is the first candidate, and <see cref="HasGameData" /> is false.
    /// </summary>
    public static string DataPath { get; } = ResolveDataPath();

    /// <summary>Whether <see cref="DataPath" /> holds the game archives.</summary>
    public static bool HasGameData => ContainsGameData(DataPath);

    private static string ResolveDataPath()
    {
        string?[] candidates =
        [
            Environment.GetEnvironmentVariable("DA_PATH"),
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..")),
            @"C:\Users\mewbb\Desktop\Chaos Launcher\Unora"
            //@"C:\Users\Despe\Desktop\Dark Ages";
        ];

        var valid = candidates.Where(path => !string.IsNullOrWhiteSpace(path))
                              .Select(path => path!)
                              .ToList();

        return valid.FirstOrDefault(ContainsGameData) ?? valid[0];
    }

    private static bool ContainsGameData(string path) => File.Exists(Path.Combine(path, "cious.dat"));

    public static string LobbyHost
        => Environment.GetEnvironmentVariable("DA_LOBBY_HOST") ??
            //"chaotic-minds.dynu.net";
            "127.0.0.1";
            //"da0.kru.com";

    public static int LobbyPort
        => short.TryParse(Environment.GetEnvironmentVariable("DA_LOBBY_PORT"), out var val) ? val : //6900;
            4200;
            //2610;

    /// <summary>Card auto-login: the username the launcher wants pre-filled/submitted (DA_AUTO_USERNAME).
    /// Null/empty when not launched from a Quick Launch card.</summary>
    public static string? AutoUsername => Environment.GetEnvironmentVariable("DA_AUTO_USERNAME");

    /// <summary>Card auto-login: the password to submit (DA_AUTO_PASSWORD). When present (with
    /// AutoUsername), the login screen submits automatically; when absent, only the name is pre-filled.</summary>
    public static string? AutoPassword => Environment.GetEnvironmentVariable("DA_AUTO_PASSWORD");

    /// <summary>Directory the launcher wants the captured character avatar written to (DA_CARD_AVATAR_DIR).
    /// Null/empty disables avatar capture.</summary>
    public static string? CardAvatarDir => Environment.GetEnvironmentVariable("DA_CARD_AVATAR_DIR");

    /// <summary>
    ///     When true, walking onto a water tile requires either the GM flag or the "Swimming" skill.
    ///     When false (default), any character can swim freely and pathfinding routes through water tiles.
    /// </summary>
    public static bool RequireSwimmingSkill => false;

    // --- Floating damage / heal numbers ---

    //animation tunables — virtual pixels / milliseconds. snapped to the 640x480 grid at draw time.
    public const float DamageNumberLifetimeMs     = 500f;  //total time before the number disappears
    public const float DamageNumberPeakHeight     = 10f;   //arc apex above the spawn point
    public const float DamageNumberTravel         = 14f;   //sideways drift over the lifetime
    public const float DamageNumberFadeStart      = 0.66f; //fraction of life where the fade begins
    public const int   MaxConcurrentDamageNumbers = 128;    //soft cap; oldest dropped when exceeded

    static GlobalSettings() => InitializeOthers();

    private static void InitializeOthers()
    {
        //without the archives every later step throws; tell the player where we looked instead of crashing
        if (!HasGameData)
        {
            Sdl.SDL_ShowSimpleMessageBox(
                Sdl.MESSAGEBOX_ERROR,
                "Unora",
                $"Could not find the game files (cious.dat) in:{Environment.NewLine}{DataPath}{Environment.NewLine}{Environment.NewLine}"
                + "Check the game folder in the launcher settings, or reinstall the game files.",
                0);

            Environment.Exit(1);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        DataContext.Initialize(
            ClientVersion,
            DataPath,
            LobbyHost,
            LobbyPort);

        LegendColors.Initialize();
        TextColors.Initialize();

        foreach (var name in PreLoadedAssemblies)
            Assembly.Load(name);

        foreach (var type in PreInitializedStatics)
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    }
}