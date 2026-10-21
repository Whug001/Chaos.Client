#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Popups.Effects;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The Effects window.</summary>
public sealed partial class WorldScreen
{
    //built on first use
    private EffectWindowControl? EffectWindow;

    private void WireEffects() => Game.Connection.OnEffectWindowDisplay += HandleEffectWindowDisplay;

    private void UnwireEffects()
    {
        Game.Connection.OnEffectWindowDisplay -= HandleEffectWindowDisplay;

        if (EffectWindow is not null)
            EffectWindow.Chosen -= SendEffectWindowChoose;
    }

    private void HandleEffectWindowDisplay(EffectWindowDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        if (args.Type == EffectWindowDisplayType.Update)
        {
            EffectWindow?.ApplyUpdate(args);

            return;
        }

        if (EffectWindow is null)
        {
            if (args.Type != EffectWindowDisplayType.Open)
                return;

            EffectWindow = new EffectWindowControl(Game.AislingRenderer, Game.EffectRenderer)
            {
                ZIndex = 2
            };

            EffectWindow.Chosen += SendEffectWindowChoose;
            Root.AddChild(EffectWindow);
        }

        if (args.Type == EffectWindowDisplayType.Open)
            EffectWindow.Open(args, WorldState.GetPlayerEntity()?.Appearance);
        else
            EffectWindow.CloseFromServer();
    }

    private void SendEffectWindowChoose(EffectWindowKind kind, byte effect) => Game.Connection.SendEffectWindowChoose(kind, effect);
}
