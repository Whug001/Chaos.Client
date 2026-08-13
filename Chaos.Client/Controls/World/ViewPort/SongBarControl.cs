#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.ViewPort;

/// <summary>
///     Strip pinned directly above the bottom HUD, showing the Bard's playing song, its Harmony, the four-note
///     call to repeat, and the notes entered so far. It spans the same columns as the orange bar and the
///     spell/skill/chat/inventory panes below it, so it never covers the hp/mp orbs or the pane icons on either
///     side. <c>WorldScreen</c> calls <see cref="SetStripBounds" /> every frame with
///     <see cref="Chaos.Client.Controls.World.Hud.IWorldHud.OrangeBarBounds" /> from whichever HUD layout is
///     active. Hidden whenever <see cref="WorldState.Song" /> reports no song playing.
/// </summary>
public sealed class SongBarControl : UIPanel
{
    public const int STRIP_HEIGHT = 26;

    //only used until WorldScreen's first SetStripBounds call lines the strip up with the active hud
    private const int DEFAULT_WIDTH = 200;

    private const int ROW_HEIGHT = 13;

    //widest the Harmony readout is ever given, and the gap left between it and the song name
    private const int HARMONY_WIDTH = 100;
    private const int SIDE_PADDING = 4;

    //index 0 = no song playing (never displayed -- the strip is hidden in that state)
    private static readonly string[] SongNames =
    [
        string.Empty,
        "Song of Mending",
        "Song of the March",
        "Song of the Bulwark",
        "Dirge of Sorrow"
    ];

    public UILabel SongLabel { get; }
    public UILabel HarmonyLabel { get; }
    public UILabel CallLabel { get; }
    public UILabel AnswerLabel { get; }

    public SongBarControl()
    {
        Height = STRIP_HEIGHT;
        BackgroundColor = new Color(0, 0, 0, 128);

        SongLabel = new UILabel
        {
            Name = "SongLabel",
            X = SIDE_PADDING,
            Y = 0,
            Height = ROW_HEIGHT,
            Text = string.Empty,
            ForegroundColor = LegendColors.White,
            PaddingLeft = 2,
            PaddingRight = 0,
            PaddingTop = 0
        };
        AddChild(SongLabel);

        HarmonyLabel = new UILabel
        {
            Name = "HarmonyLabel",
            Y = 0,
            Height = ROW_HEIGHT,
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.Gold,
            PaddingLeft = 0,
            PaddingRight = 2,
            PaddingTop = 0
        };
        AddChild(HarmonyLabel);

        CallLabel = new UILabel
        {
            Name = "CallLabel",
            X = 0,
            Y = 0,
            Height = ROW_HEIGHT,
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            PaddingLeft = 0,
            PaddingRight = 0,
            PaddingTop = 0
        };
        AddChild(CallLabel);

        AnswerLabel = new UILabel
        {
            Name = "AnswerLabel",
            X = 0,
            Y = ROW_HEIGHT,
            Height = ROW_HEIGHT,
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.CanaryYellow,
            PaddingLeft = 0,
            PaddingRight = 0,
            PaddingTop = 0
        };
        AddChild(AnswerLabel);

        //a placeholder so the labels have sane widths before WorldScreen's first SetStripBounds call
        SetStripBounds((ChaosGame.VIRTUAL_WIDTH - DEFAULT_WIDTH) / 2, DEFAULT_WIDTH);
    }

    /// <summary>
    ///     Moves and resizes the strip to span the given screen column, and re-lays out the labels inside it.
    ///     Called every frame from <c>WorldScreen.Draw</c> with the active HUD's orange bar rect, so the strip
    ///     tracks a HUD layout swap without any extra wiring. Does nothing when the bounds are unchanged.
    /// </summary>
    public void SetStripBounds(int x, int width)
    {
        if ((X == x) && (Width == width))
            return;

        X = x;
        Width = width;

        //the Harmony readout is right-aligned in its own column and the song name takes whatever is left. On a
        //narrow strip Harmony gives up space first, since a clipped song name still reads.
        var harmonyWidth = Math.Min(HARMONY_WIDTH, Math.Max(0, (width / 2) - SIDE_PADDING));

        HarmonyLabel.Width = harmonyWidth;
        HarmonyLabel.X = Math.Max(0, width - harmonyWidth - SIDE_PADDING);
        SongLabel.Width = Math.Max(0, HarmonyLabel.X - SongLabel.X);
        CallLabel.Width = width;
        AnswerLabel.Width = width;
    }

    /// <summary>
    ///     Refreshes visibility and label text from <see cref="WorldState.Song" />. Does NOT drive the call
    ///     countdown itself -- <see cref="ViewModel.SongState.Update" /> is ticked once per game-update in
    ///     <c>WorldScreen.Update.cs</c> so a live call expires in real time even if a draw is skipped.
    /// </summary>
    public override void Update(GameTime gameTime)
    {
        var song = WorldState.Song;

        Visible = song.IsPlaying;

        if (!Visible)
            return;

        SongLabel.Text = NameFor(song.SongId);
        HarmonyLabel.Text = $"Harmony {song.Harmony}%";

        if (song.HasLiveCall)
        {
            CallLabel.Text = NotesToText(song.ExpectedNotes, song.ExpectedNotes.Length);
            AnswerLabel.Text = NotesToText(song.Entered, song.NotesEntered);
        } else
        {
            CallLabel.Text = string.Empty;
            AnswerLabel.Text = string.Empty;
        }

        base.Update(gameTime);
    }

    private static string NameFor(byte songId) => (songId > 0) && (songId < SongNames.Length) ? SongNames[songId] : string.Empty;

    private static string NotesToText(ReadOnlySpan<byte> notes, int enteredCount)
    {
        Span<char> chars = stackalloc char[7];
        var index = 0;

        for (var i = 0; i < 4; i++)
        {
            if (i > 0)
                chars[index++] = ' ';

            chars[index++] = i < enteredCount
                ? notes[i] switch
                {
                    1 => 'U',
                    2 => 'I',
                    3 => 'O',
                    4 => 'P',
                    _ => '_'
                }
                : '_';
        }

        return new string(chars);
    }
}
