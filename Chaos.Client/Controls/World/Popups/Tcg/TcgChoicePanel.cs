#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>
///     A question inside a card window: a dimmed backdrop over the whole window (it swallows clicks, so nothing behind it
///     can be pressed) with the question and up to three answer buttons. Answers are reported by index; Escape or
///     <see cref="Cancel" /> reports -1. Added last to its window so it draws on top.
/// </summary>
public sealed class TcgChoicePanel : UIPanel
{
    private const int BOX_HEIGHT = 96;
    private const int BOX_WIDTH = 320;
    private const int BUTTON_GAP = 8;
    private const int BUTTON_WIDTH = 92;
    private const int MAX_ANSWERS = 3;
    private const int PADDING = 10;

    private readonly UIPanel Box;
    private readonly CustomButton[] Buttons = new CustomButton[MAX_ANSWERS];
    private readonly UILabel Question;
    private Action<int>? Answer;

    public TcgChoicePanel(int width, int height)
    {
        Name = "TcgChoice";
        Width = width;
        Height = height;
        Visible = false;
        ZIndex = 10;
        BackgroundColor = new Color(0, 0, 0, 150);

        Box = new UIPanel
        {
            X = (width - BOX_WIDTH) / 2,
            Y = (height - BOX_HEIGHT) / 2,
            Width = BOX_WIDTH,
            Height = BOX_HEIGHT,
            BackgroundColor = new Color(28, 20, 12),
            BorderColor = LegendColors.Gold
        };

        Question = new UILabel
        {
            X = PADDING,
            Y = PADDING,
            Width = BOX_WIDTH - (2 * PADDING),
            Height = BOX_HEIGHT - CustomButton.HEIGHT - (3 * PADDING),
            WordWrap = true,
            VerticalAlignment = VerticalAlignment.Top,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        Box.AddChild(Question);

        for (var i = 0; i < MAX_ANSWERS; i++)
        {
            var index = i;

            Buttons[i] = new CustomButton(string.Empty, BUTTON_WIDTH)
            {
                Y = BOX_HEIGHT - CustomButton.HEIGHT - PADDING
            };

            Buttons[i].Clicked += () => Choose(index);
            Box.AddChild(Buttons[i]);
        }

        AddChild(Box);
    }

    /// <summary>Shows the question. <paramref name="answer" /> gets the clicked answer's index, or -1 when cancelled.</summary>
    public void Ask(string question, IReadOnlyList<string> answers, Action<int> answer)
    {
        var count = Math.Clamp(answers.Count, 1, MAX_ANSWERS);
        var total = (count * BUTTON_WIDTH) + ((count - 1) * BUTTON_GAP);
        var left = (BOX_WIDTH - total) / 2;

        Question.Text = question;
        Answer = answer;

        for (var i = 0; i < MAX_ANSWERS; i++)
        {
            Buttons[i].Visible = i < count;

            if (i >= count)
                continue;

            Buttons[i].Caption = answers[i];
            Buttons[i].X = left + (i * (BUTTON_WIDTH + BUTTON_GAP));
        }

        Visible = true;
    }

    public void Cancel() => Choose(-1);

    /// <summary>Hides the question without answering, when its window closes.</summary>
    public void Dismiss()
    {
        Answer = null;
        Visible = false;
    }

    private void Choose(int index)
    {
        var answer = Answer;
        Answer = null;
        Visible = false;
        answer?.Invoke(index);
    }
}
