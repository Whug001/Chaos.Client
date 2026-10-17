#region
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

public sealed partial class TcgMatchWindow
{
    private void DrawColumn(SpriteBatch batch)
    {
        var latest = State.Latest;

        if (latest is null)
            return;

        var now = DateTime.UtcNow;

        DrawHourglass(batch, latest, now);
        DrawTimers(batch, now);
        DrawPhaseLine(batch, latest);
        DrawLog(batch);
        DrawChatLines(batch);
    }

    private void DrawHourglass(SpriteBatch batch, TcgDisplayArgs latest, DateTime now)
    {
        var at = At(TcgMatchLayout.Hourglass);

        if (TcgMatchArt.ArtPresent() && (UiRenderer.Instance is { } ui))
            DrawTextureFitted(batch, ui.GetSpfTexture(TcgMatchArt.HOURGLASS, HourglassFrame(latest, now, State.ReceivedAt)), at, Color.White);
        else
            DrawRectClipped(batch, at, BoxBack);
    }

    private void DrawTimers(SpriteBatch batch, DateTime now)
    {
        var at = At(TcgMatchLayout.Timers);
        DrawTextClipped(batch, new Vector2(at.X, at.Y), ClockText(State.TurnSecondsLeft(now)), Color.White, false);
        DrawTextClipped(batch, new Vector2(at.X, at.Y + 14), $"+{ClockText(State.ReserveSecondsLeft(now))}", Color.White, false);
    }

    private void DrawPhaseLine(SpriteBatch batch, TcgDisplayArgs latest)
    {
        var at = At(TcgMatchLayout.PhaseLine);
        var line = Fit(
            PhaseOverride(latest, Input, State.OpponentName, State.NeedsRedraw, Catalog.Get)
            ?? TcgMatchText.PhaseLine(latest.Phase, latest.ActiveSeat, latest.YourSeat, State.OpponentName),
            at.Width);
        DrawTextClipped(batch, new Vector2(at.X, at.Y), line, LegendColors.Gold, false);
    }

    private void DrawLog(SpriteBatch batch)
    {
        var at = At(TcgMatchLayout.Log);
        var lines = new List<string>();

        foreach (var entry in State.Log)
            foreach (var wrapped in TextRenderer.WrapLines(entry, at.Width))
                lines.Add(wrapped);

        var first = Math.Max(0, lines.Count - (at.Height / TextRenderer.CHAR_HEIGHT));

        for (var i = first; i < lines.Count; i++)
            DrawTextClipped(batch, new Vector2(at.X, at.Y + ((i - first) * TextRenderer.CHAR_HEIGHT)), lines[i], Color.White, false);
    }

    public void AcceptWhisper(string rawMessage, string displayText)
    {
        Chat?.Accept(rawMessage, displayText);
    }

    private void DrawChatLines(SpriteBatch batch)
    {
        if (Chat is null)
            return;

        var at = At(TcgMatchLayout.Chat);
        var lines = new List<string>();

        foreach (var entry in Chat.Lines)
            foreach (var wrapped in TextRenderer.WrapLines(entry, at.Width))
                lines.Add(wrapped);

        var first = Math.Max(0, lines.Count - (at.Height / TextRenderer.CHAR_HEIGHT));

        for (var i = first; i < lines.Count; i++)
            DrawTextClipped(batch, new Vector2(at.X, at.Y + ((i - first) * TextRenderer.CHAR_HEIGHT)), lines[i], TextColors.Whisper, false);
    }

    public static int HourglassFrame(TcgDisplayArgs latest, DateTime now, DateTime receivedAt)
    {
        var elapsed = (now - receivedAt).TotalMilliseconds;
        var left = Math.Max(0, latest.DecisionMs - elapsed);
        var full = latest.DecisionMs <= 0 ? 1 : left / latest.DecisionMs;
        var frame = (int)((1 - full) * TcgMatchArt.HOURGLASS_FRAMES);

        return Math.Clamp(frame, 0, TcgMatchArt.HOURGLASS_FRAMES - 1);
    }

    private static string ClockText(int seconds) => $"{seconds / 60}:{seconds % 60:00}";
}
