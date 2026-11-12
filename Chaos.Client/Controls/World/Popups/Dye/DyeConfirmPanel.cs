#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Dye;

/// <summary>
///     The Dye Vat's "are you sure" prompt: a dimmed sheet over the whole window with a Yes/No message box centred on
///     it. The sheet swallows every click that misses the box, so nothing beneath it can be used while the question
///     stands; the box goes on the input stack, so Enter answers Yes and Escape answers No.
/// </summary>
public sealed class DyeConfirmPanel : UIPanel
{
    private static readonly Color Dim = Color.Black * 0.45f;

    private readonly OkPopupMessageControl Box;

    /// <summary>The player answered Yes. The prompt has already closed.</summary>
    public event Action? Confirmed;

    /// <summary>The player answered No (button or Escape). The prompt has already closed.</summary>
    public event Action? Cancelled;

    /// <param name="width">The width of the window this prompt covers.</param>
    /// <param name="height">The height of the window this prompt covers.</param>
    public DyeConfirmPanel(int width, int height)
    {
        Name = "DyeConfirm";
        Visible = false;
        Background = null;
        BackgroundColor = Dim;
        Width = width;
        Height = height;

        Box = new OkPopupMessageControl(extraLines: 1, yesNo: true) { Name = "DyeConfirmBox" };
        Box.X = (width - Box.Width) / 2;
        Box.Y = (height - Box.Height) / 2;

        Box.OnOk += () =>
        {
            Hide();
            Confirmed?.Invoke();
        };

        Box.OnCancel += () =>
        {
            Hide();
            Cancelled?.Invoke();
        };

        AddChild(Box);
    }

    /// <summary>Shows the prompt with <paramref name="text" /> and Yes and No.</summary>
    public void Ask(string text)
    {
        Visible = true;
        Box.Show(text);
    }

    /// <summary>Takes the prompt down without answering it. Safe to call when it is already hidden.</summary>
    public void Hide()
    {
        if (!Visible)
            return;

        Box.Hide();
        Visible = false;
    }
}
