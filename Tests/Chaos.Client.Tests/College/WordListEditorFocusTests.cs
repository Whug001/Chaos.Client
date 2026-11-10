using System.Reflection;
using System.Runtime.CompilerServices;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.College;
using Chaos.Client.Definitions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

//The word-list "Add" button created the new word but left keyboard focus in the hint box,
//so the word was typed into the hint. The fix moves focus to the word box after a
//successful add; WordListEditorControl needs a live prefab/GPU to construct, so the
//tests run the real compiled methods on uninitialized instances with stubbed fields
//(same pattern as PartyCardFocusTests). Tab order (Title -> Word -> Hint) is the
//generic UIPanel cycle sorted by Y; mirrored here with the control's coordinates.
[NotInParallel]
public sealed class WordListEditorFocusTests
{
    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void Set(object target, string name, object? value)
    {
        for (var type = target.GetType(); type is not null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is { } field)
            {
                field.SetValue(target, value);

                return;
            }

        throw new MissingFieldException(target.GetType().FullName, name);
    }

    private static (WordListEditorControl Control, WordListDocument Document, CustomTextBox WordBox, CustomTextBox HintBox) BuildControl()
    {
        var control = Bare<WordListEditorControl>();
        var document = new WordListDocument();
        document.New(WordListEditMode.Saved);

        var titleBox = new CustomTextBox { X = 16, Y = 28, Width = 170, Height = 22, IsSelectable = true, IsTabStop = true, Visible = true, Text = "Mileth" };
        var wordBox = new CustomTextBox { X = 200, Y = 44, Width = 344, Height = 22, IsSelectable = true, IsTabStop = true, Visible = true, Text = "FIRST" };
        var hintBox = new CustomTextBox { X = 200, Y = 96, Width = 344, Height = 22, IsSelectable = true, IsTabStop = true, Visible = true, Text = "hint one" };

        Set(control, "<Document>k__BackingField", document);
        Set(control, "TitleBox", titleBox);
        Set(control, "WordBox", wordBox);
        Set(control, "HintBox", hintBox);
        Set(control, "CaptionLabel", new UILabel());
        Set(control, "WordsLabel", new UILabel());
        Set(control, "WordLabel", new UILabel());
        Set(control, "HintLabel", new UILabel());
        Set(control, "WordRows", Enumerable.Range(0, 18).Select(_ => new CollegeListRow(170, [0])).ToArray());
        Set(control, "BackButton", Bare<CustomButton>());
        Set(control, "SaveButton", Bare<CustomButton>());
        Set(control, "CopyToMineButton", Bare<CustomButton>());
        Set(control, "StartButton", Bare<CustomButton>());
        Set(control, "CancelButton", Bare<CustomButton>());
        Set(control, "EditPage", new List<UIElement>());
        Set(control, "EditingPage", true);
        Set(control, "<Children>k__BackingField", new List<UIElement>());

        return (control, document, wordBox, hintBox);
    }

    private static void AddWord(WordListEditorControl control)
        => typeof(WordListEditorControl).GetMethod("AddWord", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, null);

    [Test]
    public void Add_word_moves_focus_from_hint_to_word()
    {
        var (control, document, wordBox, hintBox) = BuildControl();

        try
        {
            hintBox.IsFocused = true;
            hintBox.IsFocused.Should().BeTrue();
            wordBox.IsFocused.Should().BeFalse();

            AddWord(control);

            document.Words.Should().HaveCount(2);
            document.Selected.Should().Be(1);
            document.Words[0].Text.Should().Be("FIRST");
            document.Words[0].Hint.Should().Be("hint one");
            wordBox.IsFocused.Should().BeTrue();
            hintBox.IsFocused.Should().BeFalse();
        }
        finally
        {
            wordBox.IsFocused = false;
            hintBox.IsFocused = false;
        }
    }

    [Test]
    public void Add_at_max_keeps_existing_hint_focus()
    {
        var (control, document, wordBox, hintBox) = BuildControl();

        try
        {
            for (var i = 1; i < 30; i++)
                document.Add().Should().BeTrue();

            document.Words.Should().HaveCount(30);
            wordBox.Text = string.Empty;
            hintBox.Text = string.Empty;
            hintBox.IsFocused = true;

            AddWord(control);

            document.Words.Should().HaveCount(30);
            hintBox.IsFocused.Should().BeTrue();
            wordBox.IsFocused.Should().BeFalse();
        }
        finally
        {
            wordBox.IsFocused = false;
            hintBox.IsFocused = false;
        }
    }

    [Test]
    public void Tab_from_word_goes_to_hint_and_shift_tab_returns()
    {
        var panel = new UIPanel { Width = 560, Height = 464, Visible = true };
        var titleBox = new CustomTextBox { X = 16, Y = 28, Width = 170, Height = 22, IsSelectable = true, IsTabStop = true, Visible = true };
        var wordBox = new CustomTextBox { X = 200, Y = 44, Width = 344, Height = 22, IsSelectable = true, IsTabStop = true, Visible = true };
        var hintBox = new CustomTextBox { X = 200, Y = 96, Width = 344, Height = 22, IsSelectable = true, IsTabStop = true, Visible = true };
        panel.AddChild(titleBox);
        panel.AddChild(wordBox);
        panel.AddChild(hintBox);

        try
        {
            titleBox.IsFocused = true;
            panel.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Tab });
            titleBox.IsFocused.Should().BeFalse();
            wordBox.IsFocused.Should().BeTrue();

            panel.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Tab });
            wordBox.IsFocused.Should().BeFalse();
            hintBox.IsFocused.Should().BeTrue();

            panel.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Tab, Modifiers = KeyModifiers.Shift });
            hintBox.IsFocused.Should().BeFalse();
            wordBox.IsFocused.Should().BeTrue();
        }
        finally
        {
            titleBox.IsFocused = false;
            wordBox.IsFocused = false;
            hintBox.IsFocused = false;
        }
    }
}
