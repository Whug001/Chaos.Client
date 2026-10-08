#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Town contests: the song composer opened for a contest entry (the College composer in Contest mode), the review
///     window where the mayor picks and an admin approves, and the town song the server plays on entering the town, at a
///     festival or the count, and from the Town Hall. Cape and banner entries use the guild editors, which the server opens
///     in contest mode with the ordinary editor packets.
/// </summary>
public sealed partial class WorldScreen
{
    //built on first use
    private TownContestReviewControl? TownContestReview;

    //the town song the shared tune player is playing, so a new one replaces it
    private readonly object TownSongOwner = new();

    private void WireTownContest() => Game.Connection.OnTownContestDisplay += HandleTownContestDisplay;

    private void UnwireTownContest()
    {
        Game.Connection.OnTownContestDisplay -= HandleTownContestDisplay;

        if (TownContestReview is not null)
            TownContestReview.ActionRequested -= SendTownContestAction;

        if (College is not null)
            College.Composer.ContestEntryRequested -= SendTownContestAction;
    }

    private void SendTownContestAction(TownContestActionArgs args) => Game.Connection.SendTownContestAction(args);

    private void HandleTownContestDisplay(TownContestDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        switch (args.Type)
        {
            case TownContestDisplayType.OpenComposer:
            {
                College ??= BuildCollege();
                BringToFront(College.Composer);
                College.Composer.OpenContest(args);

                break;
            }
            case TownContestDisplayType.Review:
            {
                var review = EnsureTownContestReview();

                if (review is null)
                    return;

                BringToFront(review);
                review.Open(args);

                break;
            }
            case TownContestDisplayType.PlaySong:
                PlayTownSong(args);

                break;
        }
    }

    /// <summary>
    ///     Plays a town song through the shared tune player, unless the player is already playing something of its own (a
    ///     composer or gallery tune the player started is never cut off by the town).
    /// </summary>
    private void PlayTownSong(TownContestDisplayArgs args)
    {
        if (args.Tune.Notes.Length == 0)
            return;

        var player = Game.TunePlayer;

        if ((player.Owner is not null) && !ReferenceEquals(player.Owner, TownSongOwner) && (player.IsPlaying || player.IsRendering))
            return;

        player.Play(TuneData.From(args.Tune), TownSongOwner);

        if (args.Title.Length > 0)
            WorldState.Chat.AddOrangeBarMessage($"The {args.Title} plays.");
    }

    private TownContestReviewControl? EnsureTownContestReview()
    {
        if (TownContestReview is not null)
            return TownContestReview;

        var references = Game.AislingRenderer.GetGuildCloakReferences();

        if (references is null || Root is null)
            return null;

        TownContestReview = new TownContestReviewControl(Game.AislingRenderer, references, Game.TunePlayer)
        {
            ZIndex = 2
        };

        TownContestReview.ActionRequested += SendTownContestAction;
        Root.AddChild(TownContestReview);

        return TownContestReview;
    }
}
