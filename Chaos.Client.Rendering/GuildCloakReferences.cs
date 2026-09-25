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
        HiddenLining = new bool[Lining.Width * Lining.Height];

        //the Front canvas draws both frames at their own positions, so a collar cell sits on the lining cell this far away
        var dx = collarFrame.Left - liningFrame.Left;
        var dy = collarFrame.Top - liningFrame.Top;

        for (var y = 0; y < Collar.Height; y++)
            for (var x = 0; x < Collar.Width; x++)
                if (Collar.IsFilled(x, y) && Lining.IsFilled(x + dx, y + dy))
                    HiddenLining[((y + dy) * Lining.Width) + x + dx] = true;
    }

    public GuildCloakGrid Back { get; }
    public EpfFrame BackFrame { get; }
    public EpfFrame? BodyFront { get; }
    public GuildCloakGrid Collar { get; }
    public EpfFrame CollarFrame { get; }

    /// <summary>The lining cells a collar pixel covers on the Front canvas, row-major in the lining grid.</summary>
    public bool[] HiddenLining { get; }

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

    /// <summary>
    ///     Gives each lining cell the collar hides on the Front canvas the color of the nearest visible lining cell below it.
    ///     Those cells can't be painted, but they show when the cape swings out from under the collar. A cell with no visible
    ///     lining below it keeps its color.
    /// </summary>
    public void FillHiddenLining(byte[] lining)
    {
        var width = Lining.Width;

        for (var y = 0; y < Lining.Height; y++)
            for (var x = 0; x < width; x++)
            {
                if (!HiddenLining[(y * width) + x])
                    continue;

                for (var below = y + 1; below < Lining.Height; below++)
                {
                    if (!Lining.IsFilled(x, below) || HiddenLining[(below * width) + x])
                        continue;

                    lining[(y * width) + x] = lining[(below * width) + x];

                    break;
                }
            }
    }

    public GuildCloakGrid Grid(GuildCloakPart part)
        => part switch
        {
            GuildCloakPart.Back   => Back,
            GuildCloakPart.Lining => Lining,
            _                     => Collar
        };
}
