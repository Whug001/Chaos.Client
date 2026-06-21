using System.Text;
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems;

/// <summary>Renders the player's own front-facing idle figure and writes it to
/// &lt;DA_CARD_AVATAR_DIR&gt;/&lt;username&gt;.png so the launcher's Quick Launch card can show it.
/// Gated on the card env vars; best-effort (never throws to the caller). Front-facing idle frame:
/// frameIndex 5 (RIGHT_IDLE_FRAME), animSuffix "04" (IDLE_ANIM).</summary>
public static class AvatarCapture
{
    private const int FrontIdleFrame = 5;
    private const string IdleAnim = "04";

    // MUST match the launcher's AvatarPathResolver.MaxLength (cross-repo contract).
    private const int MaxLength = 64;

    public static bool IsEnabled =>
        !string.IsNullOrEmpty(GlobalSettings.CardAvatarDir) &&
        !string.IsNullOrEmpty(GlobalSettings.AutoUsername);

    /// <summary>Mirrors the launcher's AvatarPathResolver.Sanitize BYTE-FOR-BYTE: any char outside
    /// [A-Za-z0-9_-] -> '_', length capped at MaxLength, empty -> "_" (so an empty username can't
    /// produce a hidden ".png" dotfile). Keep identical to the launcher side.</summary>
    public static string Sanitize(string username)
    {
        var sb = new StringBuilder(username.Length);
        foreach (var ch in username)
        {
            if (sb.Length >= MaxLength)
                break;
            sb.Append(char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_');
        }
        return sb.Length == 0 ? "_" : sb.ToString();
    }

    public static string BuildPath(string dir, string username) =>
        System.IO.Path.Combine(dir, Sanitize(username) + ".png");

    public static void CaptureAndSave(AislingRenderer renderer, in AislingAppearance appearance)
    {
        if (!IsEnabled)
            return;

        try
        {
            using var texture = renderer.Render(
                in appearance,
                frameIndex: FrontIdleFrame,
                animSuffix: IdleAnim,
                flipHorizontal: false,
                isFrontFacing: true,
                emotionFrame: -1);

            if (texture is null)
                return;

            var dir = GlobalSettings.CardAvatarDir!;
            System.IO.Directory.CreateDirectory(dir);

            var path = BuildPath(dir, GlobalSettings.AutoUsername!);
            var tmp = path + ".tmp";

            using (var fs = System.IO.File.Create(tmp))
                texture.SaveAsPng(fs, texture.Width, texture.Height);

            System.IO.File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // Best-effort: a capture failure must never affect login or gameplay.
        }
    }
}
