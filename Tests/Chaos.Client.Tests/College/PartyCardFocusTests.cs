using System.Reflection;
using System.Runtime.CompilerServices;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.College;
using Chaos.Client.Screens;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

//In Mileth College Sketch Guess, guessers lost keyboard focus in the party card answer box
//every few letters: each PartyCard server update ran SketchDrawCard.Apply for guessers, which
//called Hide() on an already-hidden card. PrefabPanel.Hide always calls
//InputDispatcher.RemoveControl, whose "focus follows stack top" branch then moved explicit
//focus to the top panel, away from the answer box. The fix guards Hide in SketchDrawCard
//and PartyCardControl so a hidden card never touches the dispatcher.
//
//The real cards build GPU textures in their constructors, so this test runs the real compiled
//methods on uninitialized instances with only the fields the covered paths touch injected:
//SketchDrawCard.Apply's guesser branch needs Countdown; PartyCardControl.Apply's Waiting
//branch needs State, Countdown, AnswerBox and Drawings; AddChild needs the Children list.
//Dismiss/Close/teacher/artist paths are not covered: they need the full GPU control tree.
[NotInParallel]
public sealed class PartyCardFocusTests
{
    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void Set(object target, string name, object? value)
    {
        //private instance fields are not visible on derived types: walk up to the declarer
        for (var type = target.GetType(); type is not null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is { } field)
            {
                field.SetValue(target, value);

                return;
            }

        throw new MissingFieldException(target.GetType().FullName, name);
    }

    private static (SketchDrawCard Sketch, PartyCardControl Party, CustomTextBox AnswerBox) HiddenCards()
    {
        var sketch = Bare<SketchDrawCard>();
        Set(sketch, "Countdown", new ToolCountdown());
        sketch.UsesControlStack = true;
        sketch.Visible = false;

        var party = Bare<PartyCardControl>();
        Set(party, "State", new PartyCardState());
        Set(party, "Countdown", new ToolCountdown());
        Set(party, "Drawings", new Dictionary<byte, byte[]>());
        Set(party, "<Children>k__BackingField", new List<UIElement>());

        var answerBox = new CustomTextBox
        {
            X = 16,
            Y = 100,
            Width = 338,
            Height = 20,
            Visible = true
        };

        Set(party, "AnswerBox", answerBox);
        party.AddChild(answerBox);
        party.UsesControlStack = true;
        party.Visible = false;

        return (sketch, party, answerBox);
    }

    private static CollegePartyInfo GuesserUpdate()
        => new()
        {
            Phase = PartyPhase.Writing,
            Round = 1,
            Rounds = 2,
            Item = 1,
            Items = 2
        };

    private static void Focus(CustomTextBox answerBox, InputDispatcher dispatcher)
    {
        answerBox.IsFocused = false;
        answerBox.IsFocused = true;
        dispatcher.ExplicitFocus.Should().BeSameAs(answerBox);
    }

    private static WorldScreen ScreenWith(PartyCardControl party, SketchDrawCard sketch)
    {
        var screen = new WorldScreen();
        typeof(WorldScreen).GetProperty(nameof(WorldScreen.Root))!.SetValue(screen, new UIPanel());

        var windowsType = typeof(WorldScreen).GetNestedType("CollegeWindows", BindingFlags.NonPublic)!;
        var windows = RuntimeHelpers.GetUninitializedObject(windowsType);
        windowsType.GetProperty("PartyCard")!.SetValue(windows, party);
        windowsType.GetProperty("SketchDraw")!.SetValue(windows, sketch);
        typeof(WorldScreen).GetField("College", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(screen, windows);

        return screen;
    }

    //Drives the real WorldScreen dispatch for a PartyCard update (lines 476-477).
    private static void HandlePartyCard(WorldScreen screen, CollegePartyInfo info)
    {
        var args = new CollegeDisplayArgs { Type = CollegeDisplayType.PartyCard, Party = info };

        typeof(WorldScreen).GetMethod("HandleCollegeDisplay", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(screen, [args]);
    }

    [Test]
    public void Repeated_party_updates_keep_guesser_answer_box_focus()
    {
        var (sketch, party, answerBox) = HiddenCards();

        try
        {
            //the reported bug: the guesser's party card is shown with the answer box focused
            //while the sketch card stays hidden; every update must leave focus alone
            var guessing = new InputDispatcher();
            party.Visible = true;
            guessing.PushControl(party);
            Focus(answerBox, guessing);

            for (var round = 0; round < 3; round++)
            {
                //guesser branch of SketchDrawCard.Apply: hides the already-hidden draw card
                sketch.Apply(GuesserUpdate(), reopen: false);

                answerBox.Text += (char)('a' + round);

                guessing.ExplicitFocus.Should().BeSameAs(answerBox);
                answerBox.IsFocused.Should().BeTrue();
            }

            answerBox.Text.Should().Be("abc");

            //WorldScreen line 472's statement: hides the already-hidden party card while
            //another window (here the shown sketch card) sits on top of the stack
            var drawing = new InputDispatcher();
            party.Visible = false;
            sketch.Visible = true;
            drawing.PushControl(sketch);
            Focus(answerBox, drawing);

            party.Hide();

            drawing.ExplicitFocus.Should().BeSameAs(answerBox);
            answerBox.IsFocused.Should().BeTrue();

            //the game-over update through the real WorldScreen dispatch: PartyCard.Apply
            //(Waiting branch) then SketchDrawCard.Apply, hiding already-hidden cards.
            //the box is unfocused by design, but focus must not move to either card panel.
            HandlePartyCard(ScreenWith(party, sketch), new CollegePartyInfo { Phase = PartyPhase.Waiting });

            drawing.ExplicitFocus.Should().NotBeSameAs(party);
            drawing.ExplicitFocus.Should().NotBeSameAs(sketch);
            party.Visible.Should().BeFalse();
            sketch.Visible.Should().BeFalse();
        }
        finally
        {
            answerBox.IsFocused = false;
            InputDispatcher.Instance?.ClearExplicitFocus();
        }
    }
}
