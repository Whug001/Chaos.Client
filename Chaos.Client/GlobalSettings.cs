#region
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Chaos.Client.Data;
using Chaos.Client.Systems;
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
    private static ushort ClientVersion => 741;

    public static string DataPath
        => Environment.GetEnvironmentVariable("DA_PATH") ??
            @"C:\Users\Despe\Desktop\Unora\Unora";
            //Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
            //@"C:\Users\Despe\Desktop\Dark Ages";

    public static string LobbyHost
        => Environment.GetEnvironmentVariable("DA_LOBBY_HOST") ??
            //"chaotic-minds.dynu.net";
            "127.0.0.1";
            //"da0.kru.com";

    public static int LobbyPort
        => short.TryParse(Environment.GetEnvironmentVariable("DA_LOBBY_PORT"), out var val) ? val : //6900;
            4200;
            //2610;

    /// <summary>
    ///     When true, walking onto a water tile requires either the GM flag or the "Swimming" skill.
    ///     When false (default), any character can swim freely and pathfinding routes through water tiles.
    /// </summary>
    public static bool RequireSwimmingSkill => false;

    /// <summary>
    ///     Maximum number of effect animations that can play on a single entity at once. When a new effect
    ///     would exceed this cap, the oldest still-playing effect on that entity is evicted to make room.
    /// </summary>
    public const int MAX_EFFECTS_PER_ENTITY = 2;

    // --- Floating damage / heal numbers ---

    //per-creature-type toggles (creature type x number kind). flip any to false to disable.
    public const bool ShowDamageNumbersOnAislings  = true;
    public const bool ShowHealNumbersOnAislings    = true;
    public const bool ShowDamageNumbersOnMonsters  = true;
    public const bool ShowHealNumbersOnMonsters    = true;
    public const bool ShowDamageNumbersOnMerchants = true;
    public const bool ShowHealNumbersOnMerchants   = true;

    //animation tunables — virtual pixels / milliseconds. snapped to the 640x480 grid at draw time.
    public const float DamageNumberLifetimeMs     = 500f;  //total time before the number disappears
    public const float DamageNumberPeakHeight     = 10f;   //arc apex above the spawn point
    public const float DamageNumberTravel         = 14f;   //sideways drift over the lifetime
    public const float DamageNumberFadeStart      = 0.66f; //fraction of life where the fade begins
    public const int   MaxConcurrentDamageNumbers = 128;    //soft cap; oldest dropped when exceeded

    static GlobalSettings() => InitializeOthers();

    private static void InitializeOthers()
    {
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