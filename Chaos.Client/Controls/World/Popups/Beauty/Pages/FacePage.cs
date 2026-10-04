#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>Page 4: every face the selected gender may wear, on one page while they fit.</summary>
public sealed class FacePage : MirrorPageView
{
    private const int COLUMNS = 8;
    private const int ROWS = (ViewModel.BeautyShop.FACE_PAGE_SIZE + COLUMNS - 1) / COLUMNS;
    private const int GRID_TOP = TextRenderer.CHAR_HEIGHT + 4;

    private readonly UILabel Caption;
    private readonly ThumbnailGrid<BeautyShopFaceEntry> Faces;

    public FacePage(MirrorActions actions, int width, int height)
        : base(actions, width, height)
    {
        Caption = AddLabel(0, 0, width);

        Faces = new ThumbnailGrid<BeautyShopFaceEntry>(COLUMNS, ROWS) { X = 0, Y = GRID_TOP };
        Faces.Hovered += f => Actions.Hover(v => v.SetHoverFace(f.Sprite));
        Faces.HoverCleared += () => Actions.Hover(v => v.ClearHover());
        Faces.Selected += f => Actions.Select(v => v.SelectFace(f.Sprite));
        Faces.PageStepped += d => Actions.Select(v => v.StepFacePage(d));
        AddChild(Faces);
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;
        var baseLook = BaseLook(vm);

        var face = vm.HoveredFaceSprite ?? vm.FaceSprite;
        Caption.Text = $"Face   {FaceName(vm, face)} - {CostText(face == vm.CurrentFaceSprite, vm.CostOfFace(face))}";

        Faces.SetItems(
            vm.VisibleFaces,
            vm.Faces.FirstOrDefault(f => f.Sprite == vm.FaceSprite),
            vm.FacePageCount,
            f => Actions.Thumbnails.Get(baseLook with { FaceSprite = f.Sprite }));
    }
}
