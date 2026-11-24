using System.Reflection;
using Chaos.Client.Rendering.CustomEmotes;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

//the badge test reflects over static PixelArt across the client assembly: keep these out of the parallel run.
[NotInParallel]
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
    public async Task The_send_helper_consults_the_unlock_state()
    {
        //TrySendEmote is the only sanctioned sender: it must refuse locked emotes itself, so a call site
        //that forgets the check cannot send. A helper that sends without consulting the unlock state fails here.
        var text = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "Chaos.Client/Screens/WorldScreen.InputHandlers.cs"));
        var helper = ExtractMethodBody(text, "TrySendEmote(BodyAnimation");

        helper.Should().Contain("EmoteSendRules.MaySend", "the helper must consult the unlock state before sending");
        helper.Should().Contain("Connection.SendEmote(", "the helper is the one place that sends");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Local_playback_only_follows_a_successful_send()
    {
        //the wheel release plays the emote locally for instant feedback; that call must stay conditional on
        //TrySendEmote's true return, or a locked emote would play over the refusal line without ever sending
        var root = RepoRoot();

        foreach (var file in SendSiteFiles(root))
        {
            var lines = await File.ReadAllLinesAsync(file);

            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("TryPlayLocalEmote(", StringComparison.Ordinal)
                    || lines[i].Contains("void TryPlayLocalEmote", StringComparison.Ordinal))
                    continue;

                //the call sits under a TrySendEmote guard on the same line or the line above
                var guarded = lines[i].Contains("TrySendEmote", StringComparison.Ordinal)
                    || ((i > 0) && lines[i - 1].Contains("TrySendEmote", StringComparison.Ordinal));

                guarded.Should()
                       .BeTrue($"{Path.GetFileName(file)}:{i + 1} plays an emote locally without a TrySendEmote guard");
            }
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_lock_badge_has_no_near_white_and_a_unique_key()
    {
        var art = LockBadge.Art;

        art.Key.Should().Be("ui:emote-lock");
        art.Width.Should().Be(7);
        art.Height.Should().Be(8);

        var opaque = art.Colors().ToList();

        //an all-transparent badge passes the size, key and near-white checks while drawing nothing
        opaque.Should().NotBeEmpty("the badge must paint visible pixels");
        opaque.Should()
              .HaveCountGreaterThanOrEqualTo(2, "the padlock reads as a shackle over a body, not one flat blob");

        foreach (var color in opaque)
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

    //returns one method body (definition anchor through its matching close brace), skipping over
    //strings, chars and comments so a brace inside them cannot end the scan early
    private static string ExtractMethodBody(string text, string anchor)
    {
        var at = text.IndexOf(anchor, StringComparison.Ordinal);
        at.Should().BeGreaterThanOrEqualTo(0, $"expected {anchor} to be defined");

        var end = FindBodyEnd(text, at);
        end.Should().BeGreaterThan(at);

        return text.Substring(at, end - at + 1);
    }

    //removes one method body (definition anchor through its matching close brace), skipping over
    //strings, chars and comments so a brace inside them cannot end the scan early
    private static string RemoveMethodBody(string text, string anchor)
    {
        var at = text.IndexOf(anchor, StringComparison.Ordinal);
        at.Should().BeGreaterThanOrEqualTo(0, $"expected {anchor} to be defined");

        var end = FindBodyEnd(text, at);
        end.Should().BeGreaterThan(at);

        return text.Remove(at, end - at + 1);
    }

    //index of the close brace matching the first open brace at or after at, skipping over
    //strings, chars and comments so a brace inside them cannot end the scan early
    private static int FindBodyEnd(string text, int at)
    {
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
                    return i;
            }

            i++;
        }

        throw new InvalidOperationException($"Could not find the end of the body at {at}");
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
