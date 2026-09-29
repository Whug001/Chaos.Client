#region
using Chaos.Client.Controls.World.Emblems;
using Chaos.Client.Controls.World.Popups.GuildEmblem;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Guild emblems: the leader's editor window, asking for the emblems the world list and the emblem book name, and
///     storing them as they arrive.
/// </summary>
public sealed partial class WorldScreen
{
    //built on first use
    private GuildEmblemEditorControl? GuildEmblemEditor;

    private void WireGuildEmblem()
    {
        Game.Connection.OnGuildEmblemDesign += HandleGuildEmblemDesign;
        Game.Connection.OnWorldList += RequestWorldListGuildEmblems;
        Game.Connection.OnEmblemBook += RequestBookGuildEmblems;
        Game.Connection.OnGuildEmblemEditor += HandleGuildEmblemEditor;
    }

    private void UnwireGuildEmblem()
    {
        Game.Connection.OnGuildEmblemDesign -= HandleGuildEmblemDesign;
        Game.Connection.OnWorldList -= RequestWorldListGuildEmblems;
        Game.Connection.OnEmblemBook -= RequestBookGuildEmblems;
        Game.Connection.OnGuildEmblemEditor -= HandleGuildEmblemEditor;

        if (GuildEmblemEditor is not null)
            GuildEmblemEditor.SaveRequested -= SendGuildEmblemEdit;
    }

    private void HandleGuildEmblemEditor(GuildEmblemEditorArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (GuildEmblemEditor is null)
        {
            GuildEmblemEditor = new GuildEmblemEditorControl
            {
                ZIndex = 2
            };

            GuildEmblemEditor.SaveRequested += SendGuildEmblemEdit;
            Root.AddChild(GuildEmblemEditor);
        }

        if (args.Type == GuildCloakEditorType.Open)
            GuildEmblemEditor.Open(args);
        else
            GuildEmblemEditor.ApplyStatus(args);
    }

    private void SendGuildEmblemEdit(GuildCloakEditorAction action, GuildEmblemDesign design)
        => Game.Connection.SendGuildEmblemEditorInteraction(
            new GuildEmblemEditorInteractionArgs
            {
                Action = action,
                Design = design
            });

    private void HandleGuildEmblemDesign(GuildEmblemDesignArgs args)
    {
        if (args.Design.IsValid())
            GuildEmblemTextures.Set(args.DesignId, args.Design);
    }

    private void RequestBookGuildEmblems(EmblemBookArgs args)
    {
        foreach (var entry in args.Entries)
            RequestGuildEmblem(entry.GuildEmblemId);
    }

    private void RequestGuildEmblem(int designId)
    {
        if (GuildEmblemTextures.ShouldRequest(designId, Environment.TickCount64))
            Game.Connection.SendGuildEmblemDesignRequest(designId);
    }

    private void RequestWorldListGuildEmblems(WorldListArgs args)
    {
        foreach (var member in args.CountryList)
            RequestGuildEmblem(member.GuildEmblemId);
    }
}
