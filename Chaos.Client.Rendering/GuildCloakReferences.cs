#region
using Chaos.DarkAges.Definitions;
using DALib.Drawing;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>Which of a design's three grids a cell belongs to.</summary>
public enum GuildCloakPart
{
    Back,
    Lining,
    Collar
}

/// <summary>
///     The guild cloak's three paintable views, from its male walk sheet: the back (frame 0, <c>c</c> layer), the lining
///     (frame 5, <c>g</c> layer) and the collar (frame 5, <c>c</c> layer). Also the male body's front frame, which the editor
///     shows faintly for placement.
/// </summary>
public sealed class GuildCloakReferences
{
    public GuildCloakReferences(EpfFrame backFrame, EpfFrame liningFrame, EpfFrame collarFrame, EpfFrame? bodyFront)
    {
        BackFrame = backFrame;
        LiningFrame = liningFrame;
        CollarFrame = collarFrame;
        BodyFront = bodyFront;
        Back = GuildCloakGrid.FromFrame(backFrame);
        Lining = GuildCloakGrid.FromFrame(liningFrame);
        Collar = GuildCloakGrid.FromFrame(collarFrame);
    }

    public GuildCloakGrid Back { get; }
    public EpfFrame BackFrame { get; }
    public EpfFrame? BodyFront { get; }
    public GuildCloakGrid Collar { get; }
    public EpfFrame CollarFrame { get; }
    public GuildCloakGrid Lining { get; }
    public EpfFrame LiningFrame { get; }

    /// <summary>True when the three frames have the grid sizes <see cref="GuildCloakProtocol" /> expects.</summary>
    public bool MatchesProtocol
        => (Back.Width == GuildCloakProtocol.BACK_WIDTH)
           && (Back.Height == GuildCloakProtocol.BACK_HEIGHT)
           && (Lining.Width == GuildCloakProtocol.LINING_WIDTH)
           && (Lining.Height == GuildCloakProtocol.LINING_HEIGHT)
           && (Collar.Width == GuildCloakProtocol.COLLAR_WIDTH)
           && (Collar.Height == GuildCloakProtocol.COLLAR_HEIGHT);

    public static byte[] Cells(GuildCloakDesign design, GuildCloakPart part)
        => part switch
        {
            GuildCloakPart.Back   => design.Back,
            GuildCloakPart.Lining => design.Lining,
            _                     => design.Collar
        };

    public GuildCloakGrid Grid(GuildCloakPart part)
        => part switch
        {
            GuildCloakPart.Back   => Back,
            GuildCloakPart.Lining => Lining,
            _                     => Collar
        };
}
