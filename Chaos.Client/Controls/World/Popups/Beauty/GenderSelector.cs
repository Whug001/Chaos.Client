#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     The character creation screen's male and female buttons, side by side. The selected one is drawn lit (the
///     creation screen's hover art). Clicking the other raises <see cref="GenderChosen" /> -- the panel decides
///     whether/when to call <see cref="SetSelected" /> in response.
/// </summary>
public sealed class GenderSelector : UIPanel
{
    private const string PREFAB_NAME = "_ncreate";

    /// <summary>Size of one gender button, from the character creation art (<c>_ncbg.spf</c>).</summary>
    public const int BUTTON_SIZE = 44;

    public const int GAP = 8;
    public const int HEIGHT = BUTTON_SIZE;

    //[normal, lit] per gender -- the creation prefab's own "Male"/"Female" controls, so the art matches it
    private readonly Texture2D MaleNormal;
    private readonly Texture2D MaleLit;
    private readonly Texture2D FemaleNormal;
    private readonly Texture2D FemaleLit;
    private Gender SelectedGender = Gender.Male;
    private Gender? HoveredGender;

    public event Action<Gender>? GenderChosen;

    public GenderSelector()
    {
        Background = null;
        Width = (BUTTON_SIZE * 2) + GAP;
        Height = HEIGHT;

        var cache = UiRenderer.Instance!;
        MaleNormal = cache.GetPrefabTexture(PREFAB_NAME, "Male", 0);
        MaleLit = cache.GetPrefabTexture(PREFAB_NAME, "Male", 1);
        FemaleNormal = cache.GetPrefabTexture(PREFAB_NAME, "Female", 0);
        FemaleLit = cache.GetPrefabTexture(PREFAB_NAME, "Female", 1);
    }

    /// <summary>Sets which button shows lit.</summary>
    public void SetSelected(Gender gender) => SelectedGender = gender;

    //null in the gap between the two buttons
    private static Gender? ButtonAt(int localX)
        => localX < BUTTON_SIZE ? Gender.Male
            : localX >= BUTTON_SIZE + GAP ? Gender.Female
            : null;

    public override void OnMouseMove(MouseMoveEvent e) => HoveredGender = ButtonAt(e.ScreenX - ScreenX);

    public override void OnMouseLeave() => HoveredGender = null;

    public override void OnClick(ClickEvent e)
    {
        e.Handled = true;

        if (ButtonAt(e.ScreenX - ScreenX) is { } chosen && (chosen != SelectedGender))
            GenderChosen?.Invoke(chosen);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        //DrawTexture clips to ClipRect, which only UpdateClipRect fills in -- base.Draw isn't called here
        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        DrawButton(spriteBatch, Gender.Male, MaleNormal, MaleLit, 0);
        DrawButton(spriteBatch, Gender.Female, FemaleNormal, FemaleLit, BUTTON_SIZE + GAP);
    }

    //selected: lit. Hovered but not selected: the lit art at part strength, so hover never reads as a selection.
    private void DrawButton(SpriteBatch spriteBatch, Gender gender, Texture2D normal, Texture2D lit, int offsetX)
    {
        var position = new Vector2(ScreenX + offsetX, ScreenY);

        if (gender == SelectedGender)
        {
            DrawTexture(spriteBatch, lit, position, Color.White);

            return;
        }

        DrawTexture(spriteBatch, normal, position, Color.White);

        if (HoveredGender == gender)
            DrawTexture(spriteBatch, lit, position, Color.White * 0.5f);
    }
}
