#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Hud;
using Chaos.Client.Systems;
using Chaos.Client.Systems.Cinema;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    private static readonly Dictionary<(int Row, int Col), CinemaSeat> SeatsByRowCol
        = CinemaSeats.All.ToDictionary(static seat => (seat.Row, seat.Col));

    private readonly SeatedViewState SeatedView = new();
    private readonly SeatedChatLines SeatedChat = new();
    private CinemaHeadRenderer? CinemaHeads;
    private Texture2D? SeatedBackdrop;
    private Texture2D? SeatedCurtains;
    private Texture2D? SeatBackTexture;
    private Texture2D? SeatPanelTexture;
    private Texture2D? SeatedWaitingText;
    private Texture2D? SeatedHiddenText;
    private IWorldHud? SeatedHud;
    private UIPanel? SeatedChatParent;
    private int SeatedChatX;
    private int SeatedChatY;

    private void UpdateSeatedView()
    {
        var player = WorldState.GetPlayerEntity();
        var on = player is not null && MapFile is not null && SeatedView.Update(CurrentMapId, player.TileX, player.TileY);

        if (!on)
        {
            LeaveSeatedUi();

            return;
        }

        if (SeatedHud != WorldHud)
        {
            LeaveSeatedUi();
            EnterSeatedUi();
        }
    }

    private void HandleSeatedChat(ViewModel.Chat.ChatMessage message)
    {
        if (SeatedView.IsOn)
            SeatedChat.Add(message.Text, message.Color, Environment.TickCount64);
    }

    private void EnterSeatedUi()
    {
        SeatedHud = WorldHud;
        ((UIPanel)WorldHud).Visible = false;

        var chat = WorldHud.ChatInput;
        SeatedChatParent = chat.Parent ?? (UIPanel)WorldHud;
        SeatedChatX = chat.X;
        SeatedChatY = chat.Y;

        //Children.Remove, not RemoveChild: RemoveChild disposes the chat input, which is only borrowed
        SeatedChatParent.Children.Remove(chat);

        var root = Root!;
        root.AddChild(chat);
        chat.X = (root.Width - chat.Width) / 2;
        chat.Y = root.Height - chat.Height - 4;
    }

    private void LeaveSeatedUi()
    {
        if (SeatedHud is null)
            return;

        var chat = SeatedHud.ChatInput;
        chat.Parent?.Children.Remove(chat);
        SeatedChatParent?.AddChild(chat);
        chat.X = SeatedChatX;
        chat.Y = SeatedChatY;
        ((UIPanel)WorldHud).Visible = true;

        SeatedChat.Clear();

        CinemaHeads?.Clear();
        SeatedHud = null;
    }

    private void DrawSeatedView(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(samplerState: GlobalSettings.Sampler);

        try
        {
            RenderHelper.DrawRect(
                spriteBatch,
                new Rectangle(0, 0, SeatedViewLayout.WIDTH, SeatedViewLayout.HEIGHT),
                Color.Black);

            var seat = SeatedView.Seat!.Value;
            var screen = SeatedViewLayout.Screen(seat.Row);

            var unavailable = Cinema?.Status == CinemaStatus.Unavailable;

            if (unavailable)
            {
                RenderHelper.DrawRect(spriteBatch, screen, new Color(40, 40, 40));
                CinemaUnavailableText ??= TextRenderer.BuildSmallText(Device, "Film unavailable");

                if (CinemaUnavailableText is { } unavailableText)
                    spriteBatch.Draw(
                        unavailableText,
                        new Vector2(
                            MathF.Floor(screen.Center.X - unavailableText.Width / 2f),
                            MathF.Floor(screen.Center.Y - unavailableText.Height / 2f)),
                        Color.White);
            } else if (ClientSettings.CinemaHideVideo)
            {
                if (SeatedCurtains is not null)
                    spriteBatch.Draw(SeatedCurtains, screen, Color.White);

                SeatedHiddenText ??= TextRenderer.BuildSmallText(Device, "Film hidden in settings");

                if (SeatedHiddenText is { } hiddenText)
                    spriteBatch.Draw(
                        hiddenText,
                        new Vector2(
                            MathF.Floor(screen.Center.X - hiddenText.Width / 2f),
                            MathF.Floor(screen.Center.Y - hiddenText.Height / 2f)),
                        Color.White);
            } else if (Cinema?.Video?.Frame is { } frame)
            {
                var fit = CinemaScreenLayout.FitPicture(screen, frame.Width, frame.Height);
                spriteBatch.Draw(frame, fit, Color.White);
            } else
            {
                if (SeatedCurtains is not null)
                    spriteBatch.Draw(SeatedCurtains, screen, Color.White);

                SeatedWaitingText ??= TextRenderer.BuildSmallText(Device, "Waiting for the film");

                if (SeatedWaitingText is { } waitingText)
                    spriteBatch.Draw(
                        waitingText,
                        new Vector2(
                            MathF.Floor(screen.Center.X - waitingText.Width / 2f),
                            MathF.Floor(screen.Center.Y - waitingText.Height / 2f)),
                        Color.White);
            }

            if (SeatedBackdrop is not null)
                spriteBatch.Draw(SeatedBackdrop, SeatedViewLayout.Backdrop(screen), Color.White);

            if (SeatBackTexture is not null || SeatPanelTexture is not null)
            {
                var occupants = new Dictionary<(int X, int Y), Models.WorldEntity>();

                foreach (var entity in WorldState.CurrentFrame.SortedEntities)
                {
                    if (entity.Type != ClientEntityType.Aisling
                        || (entity.Id == WorldState.PlayerEntityId)
                        || entity.IsHidden
                        || entity.Appearance is null
                        || CinemaSeats.At(entity.TileX, entity.TileY) is null)
                        continue;

                    occupants[(entity.TileX, entity.TileY)] = entity;
                }

                //rows in front come farthest first, so nearer rows overdraw farther rows;
                //each row's panel draws after its heads and seat backs, so the seats sit behind it
                foreach (var row in SeatedViewLayout.RowsInFront(seat.Row))
                {
                    if (SeatBackTexture is not null)
                        for (var col = 0; col < CinemaSeats.COLS; col++)
                        {
                            var back = SeatedViewLayout.SeatBack(seat.Row, seat.Col, row, col);

                            if (SeatsByRowCol.TryGetValue((row, col), out var front)
                                && occupants.TryGetValue((front.X, front.Y), out var occupant)
                                && occupant.Appearance is { } appearance
                                && CinemaHeads?.Get(in appearance) is { } head)
                                spriteBatch.Draw(head, SeatedViewLayout.Head(back, seat.Row - row), Color.White);

                            spriteBatch.Draw(SeatBackTexture, back, Color.White);
                        }

                    if (SeatPanelTexture is not null)
                        foreach (var tile in SeatedViewLayout.Panel(seat.Row - row))
                            spriteBatch.Draw(SeatPanelTexture, tile, Color.White);
                }
            }

            if (SeatedHud is not null)
            {
                var chat = SeatedHud.ChatInput;
                var lines = SeatedChat.Visible(Environment.TickCount64);

                for (var i = 0; i < lines.Count; i++)
                {
                    var (text, color, alpha) = lines[i];

                    TextRenderer.DrawText(
                        spriteBatch,
                        new Vector2(chat.X, chat.Y - TextRenderer.CHAR_HEIGHT * (lines.Count - i)),
                        text,
                        color * alpha);
                }
            }
        } finally
        {
            spriteBatch.End();
        }
    }
}
