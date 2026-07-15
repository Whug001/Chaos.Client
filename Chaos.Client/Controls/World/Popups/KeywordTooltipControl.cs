#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Data;
#endregion

namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     Explains the game term under the cursor -- a tag such as "[Fire]", or a status such as "Beag Suain" -- from the
///     glossary the server ships. The term is the heading, its explanation the body.
/// </summary>
public sealed class KeywordTooltipControl : TooltipPanelBase
{
    private const int MAX_CONTENT_WIDTH = 32 * TextRenderer.CHAR_WIDTH;

    /// <summary>
    ///     The keyword the panel is currently composited for. Survives <see cref="TooltipPanelBase.Hide" />, so a cursor
    ///     leaving a word and coming back re-shows what is already built instead of rebuilding it.
    /// </summary>
    private string? BuiltFor;

    public KeywordTooltipControl()
        : base("KeywordTooltip", MAX_CONTENT_WIDTH, LegendColors.Silver) { }

    /// <summary>
    ///     Explains the keyword under the cursor in <paramref name="source" />, or hides. A source is a wrapped label whose
    ///     text may contain game terms; a null source is one whose owner is closed.
    /// </summary>
    /// <remarks>
    ///     Polled rather than driven by mouse events on purpose. Scrolling the text under a stationary cursor changes which
    ///     word is beneath it and raises no mouse event at all, and neither does the owning panel closing -- an
    ///     event-driven tooltip would go on explaining a word that has moved or is gone.
    /// </remarks>
    public void TrackHover(int mouseX, int mouseY, RichUILabel? source)
    {
        var keyword = source?.KeywordAt(mouseX, mouseY);

        //resolve the glossary only once a word is actually under the cursor -- this is polled every frame, and
        //source is null on almost all of them (the owning popup is closed)
        if ((keyword is not null)
            && DataContext.MetaFiles.GetGlossaryMetadata() is { } glossary
            && glossary.TryGetExplanation(keyword, out var explanation))
        {
            Show(
                keyword,
                explanation!,
                mouseX,
                mouseY);

            return;
        }

        Hide();
    }

    /// <summary>
    ///     Shows <paramref name="explanation" /> under <paramref name="keyword" />. Cheap to call every frame: while the
    ///     cursor stays on one word this only repositions.
    /// </summary>
    private void Show(
        string keyword,
        string explanation,
        int mouseX,
        int mouseY)
    {
        if (!string.Equals(keyword, BuiltFor, StringComparison.Ordinal))
        {
            BuiltFor = keyword;
            HeadingLabel.Text = keyword;
            BodyLabel.Text = explanation;

            Layout();
        }

        UpdatePosition(mouseX, mouseY);
        Visible = true;
    }
}
