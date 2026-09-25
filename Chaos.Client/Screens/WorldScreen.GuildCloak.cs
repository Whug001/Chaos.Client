#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>Guild cloaks: which design each player's cloak shows, the designs themselves, the editor window and the review window.</summary>
public sealed partial class WorldScreen
{
    private const string MISSING_ART = "Your game files are missing the guild cloak. Please update the game.";

    //built on first use: it needs the guild cloak art, which older game files lack
    private GuildCloakEditorControl? GuildCloakEditor;

    //built on first use, like the editor
    private GuildCloakReviewControl? GuildCloakReview;

    private void WireGuildCloak()
    {
        Game.Connection.OnGuildCloakLook += HandleGuildCloakLook;
        Game.Connection.OnGuildCloakDesign += HandleGuildCloakDesign;
        Game.Connection.OnGuildCloakEditor += HandleGuildCloakEditor;
        Game.Connection.OnGuildCloakReviewList += HandleGuildCloakReviewList;
    }

    private void UnwireGuildCloak()
    {
        Game.Connection.OnGuildCloakLook -= HandleGuildCloakLook;
        Game.Connection.OnGuildCloakDesign -= HandleGuildCloakDesign;
        Game.Connection.OnGuildCloakEditor -= HandleGuildCloakEditor;
        Game.Connection.OnGuildCloakReviewList -= HandleGuildCloakReviewList;

        if (GuildCloakEditor is not null)
            GuildCloakEditor.SaveRequested -= SendGuildCloakEdit;

        if (GuildCloakReview is not null)
            GuildCloakReview.DecisionRequested -= SendGuildCloakDecision;
    }

    private GuildCloakEditorControl? EnsureGuildCloakEditor(UIPanel root)
    {
        if (GuildCloakEditor is not null)
            return GuildCloakEditor;

        var references = Game.AislingRenderer.GetGuildCloakReferences();

        if (references is null)
            return null;

        GuildCloakEditor = new GuildCloakEditorControl(Game.AislingRenderer, references)
        {
            ZIndex = 2
        };

        GuildCloakEditor.SaveRequested += SendGuildCloakEdit;
        root.AddChild(GuildCloakEditor);

        return GuildCloakEditor;
    }

    private void HandleGuildCloakEditor(GuildCloakEditorArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        var editor = EnsureGuildCloakEditor(Root);

        if (editor is null)
        {
            WorldState.Chat.AddOrangeBarMessage(MISSING_ART);

            return;
        }

        if (args.Type == GuildCloakEditorType.Open)
            editor.Open(args);
        else
            editor.ApplyStatus(args);
    }

    private void SendGuildCloakEdit(GuildCloakEditorAction action, GuildCloakDesign design)
        => Game.Connection.SendGuildCloakEditorInteraction(
            new GuildCloakEditorInteractionArgs
            {
                Action = action,
                Design = design
            });

    private GuildCloakReviewControl? EnsureGuildCloakReview(UIPanel root)
    {
        if (GuildCloakReview is not null)
            return GuildCloakReview;

        var references = Game.AislingRenderer.GetGuildCloakReferences();

        if (references is null)
            return null;

        GuildCloakReview = new GuildCloakReviewControl(Game.AislingRenderer, references)
        {
            ZIndex = 2
        };

        GuildCloakReview.DecisionRequested += SendGuildCloakDecision;
        root.AddChild(GuildCloakReview);

        return GuildCloakReview;
    }

    private void HandleGuildCloakReviewList(GuildCloakReviewListArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        var review = EnsureGuildCloakReview(Root);

        if (review is null)
        {
            WorldState.Chat.AddOrangeBarMessage(MISSING_ART);

            return;
        }

        if (args.Type == GuildCloakReviewListType.Open)
            review.Open(args.Entries);
        else
            review.Refresh(args.Entries);
    }

    private void SendGuildCloakDecision(GuildCloakReviewInteractionArgs args) => Game.Connection.SendGuildCloakReviewInteraction(args);

    private void HandleGuildCloakLook(GuildCloakLookArgs args)
    {
        WorldState.ApplyGuildCloakLook(args.EntityId, args.DesignId);

        if (Game.AislingRenderer.GuildCloaks.ShouldRequest(args.DesignId, Environment.TickCount64))
            Game.Connection.SendGuildCloakDesignRequest(args.DesignId);
    }

    private void HandleGuildCloakDesign(GuildCloakDesignArgs args)
    {
        if (!args.Design.IsValid())
            return;

        Game.AislingRenderer.GuildCloaks.Set(args.DesignId, args.Design);

        //players already drawn plain while the design was on its way
        foreach ((var entityId, var designId) in WorldState.GuildCloakLooks)
            if (designId == args.DesignId)
                Game.AislingRenderer.RemoveCachedEntity(entityId);
    }
}
