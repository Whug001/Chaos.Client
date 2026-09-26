namespace Chaos.Client.Controls.World.Popups.WorldList;

/// <summary>
///     Which world list button is lit and which face each button shows. Clicking the lit button turns it to its next
///     face. Clicking any other button lights it without turning it.
/// </summary>
public sealed class WorldListFilterState
{
    private readonly int[] FaceIndexes = new int[WorldListFaces.BUTTON_COUNT];

    public int ActiveButton { get; private set; }

    public WorldListFace ActiveFace => CurrentFace(ActiveButton);

    public void Click(int button)
    {
        if (button == ActiveButton)
            FaceIndexes[button] = (FaceIndexes[button] + 1) % WorldListFaces.Buttons[button].Count;
        else
            ActiveButton = button;
    }

    public WorldListFace CurrentFace(int button) => WorldListFaces.Buttons[button][FaceIndexes[button]];

    public int FaceOf(int button) => FaceIndexes[button];

    /// <summary>
    ///     Opening the drawer always shows everyone: Country lights on its first face. Other buttons keep their faces.
    /// </summary>
    public void ResetForOpen()
    {
        ActiveButton = 0;
        FaceIndexes[0] = 0;
    }
}
