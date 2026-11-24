using System.Reflection;
using Chaos.Client.Rendering.CustomEmotes;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class EmoteSendRulesTests
{
    [Test]
    public async Task A_locked_emote_may_not_be_sent()
    {
        var state = new EmoteUnlockState();

        EmoteSendRules.MaySend(state, (BodyAnimation)50).Should().BeFalse("byte 50 is a prize emote nobody has won yet");

        await Task.CompletedTask;
    }

    [Test]
    public async Task An_unlocked_prize_emote_may_be_sent()
    {
        var state = new EmoteUnlockState();
        state.Replace([50]);

        EmoteSendRules.MaySend(state, (BodyAnimation)50).Should().BeTrue("byte 50 was just won");
        EmoteSendRules.MaySend(state, (BodyAnimation)51).Should().BeFalse("byte 51 is still locked");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Free_emotes_may_always_be_sent()
    {
        var state = new EmoteUnlockState();

        EmoteSendRules.MaySend(state, BodyAnimation.Smile).Should().BeTrue("retail emotes are never locked");
        EmoteSendRules.MaySend(state, (BodyAnimation)19).Should().BeTrue("older drawn emotes stay free");
        EmoteSendRules.MaySend(state, (BodyAnimation)47).Should().BeTrue("older drawn emotes stay free");

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_locked_emote_is_refused_at_every_send_site()
    {
        //the wheel release, the hotkey chord and the poker table must all refuse a locked emote
        //through TrySendEmote, which also plays nothing locally: they may not call SendEmote directly
        var root = RepoRoot();

        foreach (var file in SendSiteFiles(root))
        {
            var text = await File.ReadAllTextAsync(file);

            //only the file defining the guard still mentions SendEmote, inside its own body
            var withoutGuard = text.Contains("TrySendEmote(BodyAnimation", StringComparison.Ordinal)
                ? RemoveMethodBody(text, "TrySendEmote(BodyAnimation")
                : text;

            withoutGuard.Should()
                       .NotContain(
                           "Connection.SendEmote(",
                           $"{Path.GetFileName(file)} must send emotes only through TrySendEmote");
        }
    }

    [Test]
    public async Task The_lock_badge_has_no_near_white_and_a_unique_key()
    {
        var art = LockBadge.Art;

        art.Key.Should().Be("ui:emote-lock");
        art.Width.Should().Be(7);
        art.Height.Should().Be(8);

        foreach (var color in art.Colors())
            (color is { R: >= 220, G: >= 220, B: >= 220 }).Should()
                                                                .BeFalse($"a near-white badge would read as unlocked: {color}");

        AllPixelArtKeys().Where(key => key == art.Key)
                         .Should()
                         .HaveCount(1, "PixelArt keys name textures in the cache, so each must be unique");

        await Task.CompletedTask;
    }

    private static IEnumerable<string> SendSiteFiles(string root)
    {
        yield return Path.Combine(root, "Chaos.Client/Screens/WorldScreen.Update.cs");
        yield return Path.Combine(root, "Chaos.Client/Screens/WorldScreen.InputHandlers.cs");
        yield return Path.Combine(root, "Chaos.Client/Screens/WorldScreen.Wiring.cs");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Chaos.Client.slnx")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate Chaos.Client.slnx above {AppContext.BaseDirectory}");
    }

    //removes one method body (definition anchor through its matching close brace), skipping over
    //strings, chars and comments so a brace inside them cannot end the scan early
    private static string RemoveMethodBody(string text, string anchor)
    {
        var at = text.IndexOf(anchor, StringComparison.Ordinal);
        at.Should().BeGreaterThanOrEqualTo(0, $"expected {anchor} to be defined");

        var open = text.IndexOf('{', at);
        open.Should().BeGreaterThanOrEqualTo(0);

        var i = open;
        var depth = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if ((c == '/') && (i + 1 < text.Length) && (text[i + 1] == '/'))
            {
                while ((i < text.Length) && (text[i] != '\n'))
                    i++;

                continue;
            }

            if ((c == '/') && (i + 1 < text.Length) && (text[i + 1] == '*'))
            {
                i += 2;

                while ((i + 1 < text.Length) && ((text[i] != '*') || (text[i + 1] != '/')))
                    i++;

                i += 2;

                continue;
            }

            if ((c == '"') || (c == '\''))
            {
                var quote = c;
                i++;

                while (i < text.Length)
                {
                    if (text[i] == '\\')
                        i += 2;
                    else if (text[i] == quote)
                    {
                        i++;

                        break;
                    } else
                        i++;
                }

                continue;
            }

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;

                if (depth == 0)
                    return text.Remove(at, i - at + 1);
            }

            i++;
        }

        throw new InvalidOperationException($"Could not find the end of {anchor}");
    }

    private static IEnumerable<string> AllPixelArtKeys()
    {
        var assembly = typeof(LockBadge).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                //skip auto-property backing fields: the property below already reports the same art once
                if (field.Name.Contains("k__BackingField", StringComparison.Ordinal))
                    continue;

                if (field.FieldType == typeof(PixelArt) && field.GetValue(null) is PixelArt single)
                    yield return single.Key;
                else if (field.FieldType == typeof(PixelArt[]) && field.GetValue(null) is PixelArt[] many)
                {
                    foreach (var art in many)
                    {
                        if (art is not null)
                            yield return art.Key;
                    }
                }
            }

            foreach (var property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if ((property.PropertyType == typeof(PixelArt)) && (property.GetMethod is not null))
                {
                    if (property.GetMethod.Invoke(null, null) is PixelArt art)
                        yield return art.Key;
                }
            }
        }
    }
}
