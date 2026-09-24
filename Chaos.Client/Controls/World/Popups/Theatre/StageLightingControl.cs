#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Theatre;

/// <summary>
///     The director's Stage Lighting window (layout board-compact.html). Left: the diamond stage view and a chip per
///     light. Right: the selected light's colour, size, beam, brightness, effect and motion. Bottom: house lights,
///     Blackout, stage glow and scenes. Drag the title bar to move it; "-" shrinks it to a bar that keeps the dimmer and
///     Blackout. It never takes the keyboard (except the scene-name box), so the director can keep walking.
/// </summary>
public sealed class StageLightingControl : UIPanel
{
    public const int FULL_WIDTH = 380;
    public const int FULL_HEIGHT = 264;
    public const int MINI_WIDTH = 230;
    public const int MINI_HEIGHT = 20;

    //the minimized bar's rows, vertically centred: 12 px text and slider at y 4, 16 px buttons at y 2
    private const int MINI_ROW_Y = (MINI_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2;
    private const int MINI_BUTTON_Y = (MINI_HEIGHT - StageButton.HEIGHT) / 2;

    private const int TITLE_HEIGHT = 16;
    private const int PAD = 6;
    private const int RIGHT_X = 212;
    private const int FOOTER_Y = 198;
    private const int DELETE_CONFIRM_MS = 3000;
    private const int SCENE_NAME_MAX = 24;

    //wide enough for a 24-character name in the dropdown header and the name box (6 px a character plus their frames)
    private const int SCENE_BOX_WIDTH = 166;
    private const int SCENE_BUTTONS_X = PAD + SCENE_BOX_WIDTH + 6;

    private static readonly string[] EffectNames = ["None", "Pulse", "Flicker", "Cycle"];
    private static readonly string[] MotionNames = ["Still", "Sweep", "Circle", "Follow"];
    private static readonly string[] SizeNames = ["S", "M", "L"];

    private readonly StageButton AddButton;
    private readonly StageButton BeamButton;
    private readonly StageButton BlackoutButton;
    private readonly StageSlider Brightness;
    private readonly StageButton CancelButton;
    private readonly StageButton[] Chips = new StageButton[StageLightingPanelState.MAX_LIGHTS];
    private readonly StageButton CloseButton;
    private readonly UILabel CountLabel;
    private readonly StageButton CustomSwatch;
    private readonly StageButton DeleteButton;
    private readonly UILabel EffectLabel;
    private readonly StageSlider EffectSpeed;
    private readonly UIPanel Full;
    private readonly StageButton GlowButton;
    private readonly UILabel HintLabel;
    private readonly StageSlider House;
    private readonly UILabel HouseLabel;
    private readonly StageButton LoadButton;
    private readonly UIPanel Mini;
    private readonly StageSlider MiniHouse;
    private readonly UILabel MiniHouseLabel;
    private readonly StageButton MinimizeButton;
    private readonly UILabel MotionLabel;
    private readonly StageSlider MotionSpeed;
    private readonly StageColorPicker Picker;
    private readonly StageButton RemoveButton;
    private readonly StageButton SaveAsButton;
    private readonly StageButton SaveButton;
    private readonly CustomComboBox SceneList;
    private readonly CustomTextBox SceneName;
    private readonly UIPanel Settings;
    private readonly StageButton[] SizeButtons = new StageButton[3];
    private readonly StageButton[] Swatches;
    private readonly UIPanel TitleBand;
    private readonly UILabel TitleLabel;
    private readonly StageView View;

    private long DeleteArmedUntil;
    private string? DeleteArmedName;
    private bool DraggingWindow;
    private Point Grab;

    /// <summary>The name last sent by <see cref="SaveScene" />, selected in <see cref="SceneList" /> once it appears there.</summary>
    private string? PendingSceneSelection;

    private bool SaveMode;

    public StageLightingControl()
    {
        Name = "StageLighting";
        Visible = false;
        Width = FULL_WIDTH;
        Height = FULL_HEIGHT;
        BackgroundColor = new Color(28, 24, 20);
        BorderColor = new Color(138, 109, 59);

        //── title bar ──
        TitleBand = new UIPanel
        {
            X = 1,
            Y = 1,
            Width = FULL_WIDTH - 2,
            Height = TITLE_HEIGHT - 1,
            BackgroundColor = new Color(42, 34, 26),
            IsHitTestVisible = false,
            ZIndex = -1
        };

        AddChild(TitleBand);
        TitleLabel = Label(this, "STAGE LIGHTING", PAD, 2, 120, LegendColors.Gold);
        MinimizeButton = Button(this, "-", FULL_WIDTH - 40, 1, 16, () => SetMinimized(true));
        MinimizeButton.Height = 14;
        CloseButton = Button(this, "x", FULL_WIDTH - 21, 1, 16, Hide);
        CloseButton.Height = 14;

        //── full body (under the title) ──
        //the body takes Enter and Escape from the scene-name box: a single-line box leaves them to its parent,
        //and unhandled they would reach the world's Enter (open chat)
        Full = new BodyPanel
        {
            X = 0,
            Y = TITLE_HEIGHT,
            Width = FULL_WIDTH,
            Height = FULL_HEIGHT - TITLE_HEIGHT,
            KeyPressed = OnBodyKey
        };

        AddChild(Full);

        View = new StageView { X = PAD, Y = 4 };
        View.LightPressed += id => SelectLight(id);
        View.LightDragged += (id, tile) => MoveLight(id, tile, handle: false);
        View.HandleDragged += (id, tile) => MoveLight(id, tile, handle: true);
        View.DragEnded += EndContinuous;
        View.PersonPicked += PickFollowTarget;
        Full.AddChild(View);

        for (var i = 0; i < Chips.Length; i++)
        {
            var index = i;
            Chips[i] = Button(Full, string.Empty, PAD + (i * 20), 158, 18, () => SelectChip(index));
        }

        AddButton = Button(Full, "+ Add", PAD, 178, 44, AddLight);
        RemoveButton = Button(Full, "Remove", PAD + 48, 178, 48, RemoveLight);
        CountLabel = Label(Full, "0/8", PAD + 100, 180, 40, LegendColors.Gray);

        //── the selected light's settings ──
        Settings = new UIPanel
        {
            X = RIGHT_X,
            Y = 0,
            Width = FULL_WIDTH - RIGHT_X - PAD,
            Height = 190
        };

        Full.AddChild(Settings);

        Label(Settings, "COLOR", 0, 4, 60, LegendColors.Gray);
        Swatches = new StageButton[StageLightingPanelState.Swatches.Length];

        for (var i = 0; i < Swatches.Length; i++)
        {
            var colour = StageLightingPanelState.Swatches[i];
            Swatches[i] = Button(Settings, string.Empty, (i % 7) * 16, 18 + ((i / 7) * 16), 14, () => SetColour(colour, continuous: false));
            Swatches[i].Height = 14;
            Swatches[i].Fill = colour;
        }

        CustomSwatch = Button(Settings, string.Empty, 6 * 16, 34, 14, OpenPicker);
        CustomSwatch.Height = 14;
        CustomSwatch.Rainbow = true;

        for (var i = 0; i < SizeButtons.Length; i++)
        {
            var size = (StageLightSize)i;
            SizeButtons[i] = Button(Settings, SizeNames[i], i * 20, 54, 18, () => EditSelected(light => light with { Size = size }));
        }

        Label(Settings, "Beam", 70, 56, 30, LegendColors.Silver);
        BeamButton = Button(Settings, "On", 104, 54, 32, () => EditSelected(light => light with { Beam = !light.Beam }));

        Label(Settings, "Bright", 0, 76, 38, LegendColors.Silver);
        Brightness = Slider(Settings, 0, 100, 40, 76, 120);
        Brightness.ValueChanged += value => EditSelected(light => light with { Brightness = (byte)value }, continuous: true);
        Brightness.DragEnded += EndSelectedContinuous;

        Label(Settings, "EFFECT", 0, 92, 60, LegendColors.Gray);
        Button(Settings, "<", 0, 106, 16, () => StepEffect(-1));
        EffectLabel = Label(Settings, "None", 18, 108, 50, LegendColors.White, HorizontalAlignment.Center);
        Button(Settings, ">", 70, 106, 16, () => StepEffect(1));
        EffectSpeed = Slider(Settings, 1, 5, 92, 108, 70);
        EffectSpeed.ValueChanged += value => EditSelected(light => light with { EffectSpeed = (byte)value }, continuous: true);
        EffectSpeed.DragEnded += EndSelectedContinuous;

        Label(Settings, "MOTION", 0, 126, 60, LegendColors.Gray);
        Button(Settings, "<", 0, 140, 16, () => StepMotion(-1));
        MotionLabel = Label(Settings, "Still", 18, 142, 50, LegendColors.White, HorizontalAlignment.Center);
        Button(Settings, ">", 70, 140, 16, () => StepMotion(1));
        MotionSpeed = Slider(Settings, 1, 5, 92, 142, 70);
        MotionSpeed.ValueChanged += value => EditSelected(light => light with { MotionSpeed = (byte)value }, continuous: true);
        MotionSpeed.DragEnded += EndSelectedContinuous;

        //two lines: the longer hints ("Click a person on the stage.") are wider than the settings column
        HintLabel = Label(Settings, string.Empty, 0, 162, Settings.Width, LegendColors.Gray);
        HintLabel.WordWrap = true;
        HintLabel.Height = TextRenderer.CHAR_HEIGHT * 2;
        HintLabel.VerticalAlignment = VerticalAlignment.Top;

        //── footer: house lights and scenes ──
        var separator = new UIPanel
        {
            X = PAD,
            Y = FOOTER_Y - 4,
            Width = FULL_WIDTH - (2 * PAD),
            Height = 1,
            BackgroundColor = new Color(61, 51, 38),
            IsHitTestVisible = false
        };

        Full.AddChild(separator);

        Label(Full, "House", PAD, FOOTER_Y + 2, 34, LegendColors.Silver);
        House = Slider(Full, 0, 100, PAD + 36, FOOTER_Y + 2, 110);
        House.ValueChanged += value => SetHouse(value);
        House.DragEnded += FlushContinuous;
        HouseLabel = Label(Full, "100%", PAD + 150, FOOTER_Y + 2, 30, LegendColors.White);
        BlackoutButton = Button(Full, "Blackout", PAD + 184, FOOTER_Y, 56, Blackout);
        GlowButton = Button(Full, "[x] Stage glow", PAD + 246, FOOTER_Y, 116, ToggleGlow);

        SceneList = new CustomComboBox(SCENE_BOX_WIDTH) { X = PAD, Y = FOOTER_Y + 22 };
        Full.AddChild(SceneList);

        SceneName = new CustomTextBox
        {
            X = PAD,
            Y = FOOTER_Y + 22,
            Width = SCENE_BOX_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = SCENE_NAME_MAX,
            HintText = "Scene name",
            Visible = false
        };

        Full.AddChild(SceneName);

        LoadButton = Button(Full, "Load", SCENE_BUTTONS_X, FOOTER_Y + 25, 40, LoadScene);
        SaveAsButton = Button(Full, "Save as", SCENE_BUTTONS_X + 44, FOOTER_Y + 25, 52, EnterSaveMode);
        DeleteButton = Button(Full, "Delete", SCENE_BUTTONS_X + 100, FOOTER_Y + 25, 52, DeleteScene);
        SaveButton = Button(Full, "Save", SCENE_BUTTONS_X, FOOTER_Y + 25, 40, SaveScene);
        CancelButton = Button(Full, "Cancel", SCENE_BUTTONS_X + 44, FOOTER_Y + 25, 52, ExitSaveMode);
        SaveButton.Visible = false;
        CancelButton.Visible = false;

        Picker = new StageColorPicker
        {
            X = RIGHT_X - 30,
            Y = 4,
            ZIndex = 10
        };

        Picker.ColorChanged += colour => SetColour(colour, continuous: true);
        Picker.DragEnded += EndSelectedContinuous;
        Full.AddChild(Picker);

        //── minimized bar ──
        //pass-through: a press on the bar's empty space lands on the window itself, which drags it
        Mini = new UIPanel
        {
            X = 0,
            Y = 0,
            Width = MINI_WIDTH,
            Height = MINI_HEIGHT,
            Visible = false,
            IsPassThrough = true
        };

        AddChild(Mini);
        Label(Mini, "Lights", 4, MINI_ROW_Y, 36, LegendColors.Gold);
        MiniHouse = Slider(Mini, 0, 100, 42, MINI_ROW_Y, 60);
        MiniHouse.ValueChanged += value => SetHouse(value);
        MiniHouse.DragEnded += FlushContinuous;
        MiniHouseLabel = Label(Mini, "100%", 106, MINI_ROW_Y, 26, LegendColors.White);
        Button(Mini, "Blackout", 134, MINI_BUTTON_Y, 52, Blackout);
        Button(Mini, "+", 190, MINI_BUTTON_Y, 16, () => SetMinimized(false));
        Button(Mini, "x", 210, MINI_BUTTON_Y, 16, Hide);
    }

    /// <summary>Raised with each edit to send to the server.</summary>
    public event Action<StageLightingInteractionArgs>? EditRequested;

    private static StageLightingPanelState Panel => WorldState.StageLightingPanel;
    private static StageLightAnimator Setup => WorldState.StageLights;

    public void Show()
    {
        var position = Panel.Position
                       ?? new Point((ChaosGame.VIRTUAL_WIDTH - FULL_WIDTH) / 2, Math.Max(0, ((ChaosGame.VIRTUAL_HEIGHT - FULL_HEIGHT) / 2) - 40));

        X = position.X;
        Y = position.Y;
        ApplyMode();
        Visible = true;
        RefreshFromState();
    }

    public void Hide()
    {
        if (!Visible)
            return;

        Picker.Visible = false;
        SceneList.Close();
        ExitSaveMode();
        DisarmDelete();
        Panel.EndFollowPick();
        DraggingWindow = false;
        PendingSceneSelection = null;
        Visible = false;
    }

    /// <summary>Repaints every control from the setup and the panel state. Called on each setup and scene-list message.</summary>
    public void RefreshFromState()
    {
        var lights = Setup.Lights;
        Panel.Reconcile(lights, Environment.TickCount64);
        var selected = Selected();

        for (var i = 0; i < Chips.Length; i++)
        {
            var chip = Chips[i];
            chip.Visible = i < lights.Count;

            if (!chip.Visible)
                continue;

            var light = lights[i];
            chip.Caption = light.Id.ToString();
            chip.Dot = new Color(light.R, light.G, light.B);
            chip.Selected = light.Id == Panel.SelectedLightId;
        }

        AddButton.Enabled = lights.Count < StageLightingPanelState.MAX_LIGHTS;
        RemoveButton.Enabled = selected is not null;
        CountLabel.Text = $"{lights.Count}/{StageLightingPanelState.MAX_LIGHTS}";

        View.SelectedLightId = Panel.SelectedLightId;
        View.PickingFollow = Panel.AwaitingFollowPick;

        //a light already following someone re-targets on a click on another person; lights still drag otherwise
        View.AllowPersonPick = !Panel.AwaitingFollowPick && selected is { Motion: StageLightMotion.Follow };
        Settings.Enabled = selected is not null;

        if (selected is not null)
        {
            var colour = new Color(selected.R, selected.G, selected.B);

            for (var i = 0; i < Swatches.Length; i++)
                Swatches[i].Selected = StageLightingPanelState.Swatches[i] == colour;

            CustomSwatch.Selected = !StageLightingPanelState.Swatches.Contains(colour);

            for (var i = 0; i < SizeButtons.Length; i++)
                SizeButtons[i].Selected = (int)selected.Size == i;

            BeamButton.Caption = selected.Beam ? "On" : "Off";
            BeamButton.Selected = selected.Beam;

            if (!Brightness.IsDragging)
                Brightness.SetValue(selected.Brightness);

            EffectLabel.Text = NameOrDefault(EffectNames, (int)selected.Effect, selected.Effect);

            if (!EffectSpeed.IsDragging)
                EffectSpeed.SetValue(selected.EffectSpeed);

            var motion = Panel.AwaitingFollowPick ? StageLightMotion.Follow : selected.Motion;
            MotionLabel.Text = NameOrDefault(MotionNames, (int)motion, motion);

            if (!MotionSpeed.IsDragging)
                MotionSpeed.SetValue(selected.MotionSpeed);

            HintLabel.Text = motion switch
            {
                StageLightMotion.Sweep                               => "Drag the square: swing end.",
                StageLightMotion.Circle                              => "Drag the square: circle size.",
                StageLightMotion.Follow when Panel.AwaitingFollowPick => "Click a person on the stage.",
                StageLightMotion.Follow                              => "Following. Click a person to change.",
                _                                                    => "Drag the light to move it."
            };
        } else
        {
            HintLabel.Text = Setup.HasSetup ? "Add a light to start." : string.Empty;
            Picker.Visible = false;
        }

        //a held dimmer shows its own value (and keeps the other dimmer in step) until it is let go
        var house = House.IsDragging ? House.Value : MiniHouse.IsDragging ? MiniHouse.Value : Setup.HouseLevel;

        if (!House.IsDragging)
            House.SetValue(house);

        if (!MiniHouse.IsDragging)
            MiniHouse.SetValue(house);

        HouseLabel.Text = $"{house}%";
        MiniHouseLabel.Text = HouseLabel.Text;
        GlowButton.Caption = Setup.StageGlow ? "[x] Stage glow" : "[ ] Stage glow";

        //rebuilding the list closes it, so only when the names actually changed (this runs on every setup message)
        var scenes = Panel.Scenes;

        if (!SceneList.Items.SequenceEqual(scenes))
        {
            var sceneList = scenes.ToList();

            var pendingIndex = PendingSceneSelection is null
                ? -1
                : sceneList.FindIndex(name => string.Equals(name, PendingSceneSelection, StringComparison.OrdinalIgnoreCase));

            if (pendingIndex >= 0)
            {
                SceneList.SetItems(scenes, pendingIndex);
                PendingSceneSelection = null;
            } else
            {
                var current = SceneList.SelectedItem;
                SceneList.SetItems(scenes, Math.Max(0, sceneList.FindIndex(name => name == current)));
            }
        }

        LoadButton.Enabled = scenes.Count > 0;
        DeleteButton.Enabled = scenes.Count > 0;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        var now = Environment.TickCount64;

        //a drag that pauses still sends its latest value once the rate limit allows, not only on the next move
        if (Panel.TakeDue(now) is { } due)
            Send(due);

        if ((DeleteArmedUntil != 0) && (now > DeleteArmedUntil))
            DisarmDelete();
    }

    //── window dragging (title bar, or anywhere on the minimized bar) ──

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        if (!Panel.IsMinimized && ((e.ScreenY - ScreenY) >= TITLE_HEIGHT))
            return;

        DraggingWindow = true;
        Grab = new Point(e.ScreenX - X, e.ScreenY - Y);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!DraggingWindow)
            return;

        X = e.ScreenX - Grab.X;
        Y = e.ScreenY - Grab.Y;
        KeepOnScreen();
        e.Handled = true;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (!DraggingWindow)
            return;

        DraggingWindow = false;
        Panel.Position = new Point(X, Y);
        e.Handled = true;
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();
        DraggingWindow = false;
    }

    //── edits ──

    private StageLightInfo? Selected()
        => Panel.SelectedLightId is { } id ? Setup.Lights.FirstOrDefault(light => light.Id == id) : null;

    private void Send(StageLightingInteractionArgs edit) => EditRequested?.Invoke(edit);

    private void SendContinuous(StageLightingInteractionArgs edit)
    {
        if (Panel.Throttle(edit, Environment.TickCount64) is { } now)
            Send(now);
    }

    private void FlushContinuous()
    {
        if (Panel.Flush(Environment.TickCount64) is { } last)
            Send(last);
    }

    /// <summary>Ends a drag on a light's setting: sends the held edit and lets the server's copy take over once it matches.</summary>
    private void EndContinuous(byte lightId)
    {
        FlushContinuous();
        Setup.Release(lightId, Environment.TickCount64);
        RefreshFromState();
    }

    private void EndSelectedContinuous() => EndContinuous(Panel.SelectedLightId ?? 0);

    /// <summary>Applies <paramref name="change" /> to the selected light and sends it. Continuous edits show at once and are throttled.</summary>
    private void EditSelected(Func<StageLightInfo, StageLightInfo> change, bool continuous = false)
    {
        if (Selected() is not { } light)
            return;

        var edited = change(light);
        var edit = new StageLightingInteractionArgs
        {
            Action = StageLightingAction.UpdateLight,
            LightId = edited.Id,
            Light = edited
        };

        //either way the director sees the edit at once. A click edit is released straight away, so its local
        //copy gives way as soon as the server's copy matches (1 s at most); a continuous one is released on drag end.
        Setup.Hold(edited);

        if (continuous)
            SendContinuous(edit);
        else
        {
            Setup.Release(edited.Id, Environment.TickCount64);
            Send(edit);
        }

        RefreshFromState();
    }

    private void SelectLight(byte id)
    {
        Panel.Select(id);
        Picker.Visible = false;
        RefreshFromState();
    }

    private void SelectChip(int index)
    {
        if (index < Setup.Lights.Count)
            SelectLight(Setup.Lights[index].Id);
    }

    private void AddLight()
    {
        if (Setup.Stage.IsEmpty)
            return;

        Panel.ExpectNewLight(Environment.TickCount64);
        Send(new StageLightingInteractionArgs { Action = StageLightingAction.AddLight, Light = StageLightingPanelState.NewLight(Setup.Stage) });
    }

    private void RemoveLight()
    {
        if (Panel.SelectedLightId is not { } id)
            return;

        Send(new StageLightingInteractionArgs { Action = StageLightingAction.RemoveLight, LightId = id });
    }

    private void MoveLight(byte id, Vector2 tile, bool handle)
    {
        var light = Setup.Lights.FirstOrDefault(l => l.Id == id);

        if (light is null)
            return;

        var (x, y) = StageViewGeometry.ToUnits(tile, Setup.Stage);
        var moved = handle ? light with { X2 = x, Y2 = y } : light with { X = x, Y = y };

        //a followed light dragged by hand stops following
        if (!handle && (moved.Motion == StageLightMotion.Follow))
            moved = moved with { Motion = StageLightMotion.Still, FollowId = 0 };

        Setup.Hold(moved);
        SendContinuous(new StageLightingInteractionArgs { Action = StageLightingAction.UpdateLight, LightId = id, Light = moved });
        RefreshFromState();
    }

    private void SetColour(Color colour, bool continuous)
        => EditSelected(light => light with { R = colour.R, G = colour.G, B = colour.B }, continuous);

    private void OpenPicker()
    {
        if (Selected() is not { } light)
            return;

        Picker.Open(new Color(light.R, light.G, light.B));
    }

    private void StepEffect(int direction)
        => EditSelected(light => light with { Effect = StageLightingPanelState.Step(light.Effect, direction) });

    private void StepMotion(int direction)
    {
        if (Selected() is not { } light)
            return;

        var current = Panel.AwaitingFollowPick ? StageLightMotion.Follow : light.Motion;
        var next = StageLightingPanelState.Step(current, direction);

        if (next == StageLightMotion.Follow)
        {
            //nothing is sent until the director clicks a person
            Panel.BeginFollowPick();
            RefreshFromState();

            return;
        }

        Panel.EndFollowPick();
        EditSelected(l => StageLightingPanelState.WithMotion(l, next, Setup.Stage));
    }

    private void PickFollowTarget(uint entityId)
    {
        Panel.EndFollowPick();
        EditSelected(light => light with { Motion = StageLightMotion.Follow, FollowId = entityId });
    }

    private void SetHouse(int level)
    {
        HouseLabel.Text = $"{level}%";
        MiniHouseLabel.Text = HouseLabel.Text;
        SendContinuous(new StageLightingInteractionArgs { Action = StageLightingAction.SetHouseLevel, Level = (byte)level });
    }

    private void Blackout() => Send(new StageLightingInteractionArgs { Action = StageLightingAction.Blackout });

    private void ToggleGlow() => Send(new StageLightingInteractionArgs { Action = StageLightingAction.SetStageGlow, Flag = !Setup.StageGlow });

    //── scenes ──

    private void LoadScene()
    {
        if (SceneList.SelectedItem is { } name)
            Send(new StageLightingInteractionArgs { Action = StageLightingAction.LoadScene, SceneName = name });
    }

    private void EnterSaveMode()
    {
        SaveMode = true;
        SceneName.Text = SceneList.SelectedItem ?? string.Empty;
        ApplySaveMode();

        //the one control that takes the keyboard; typing replaces the suggested name
        SceneName.IsFocused = true;
        SceneName.SelectAll();
    }

    private void ExitSaveMode()
    {
        SaveMode = false;
        SceneName.IsFocused = false;
        ApplySaveMode();
    }

    /// <summary>Sends the typed name: 1-24 printable ASCII characters after trimming, the same rule the server applies.</summary>
    private void SaveScene()
    {
        var name = SceneName.Text.Trim();

        if ((name.Length is 0 or > SCENE_NAME_MAX) || name.Any(c => c is < ' ' or > '~'))
            return;

        Send(new StageLightingInteractionArgs { Action = StageLightingAction.SaveScene, SceneName = name });
        PendingSceneSelection = name;
        ExitSaveMode();
    }

    /// <summary>The first click arms Delete for the selected scene; a second click within 3 s on the same scene deletes it.</summary>
    private void DeleteScene()
    {
        if (SceneList.SelectedItem is not { } name)
            return;

        if ((DeleteArmedUntil == 0) || (DeleteArmedName != name))
        {
            DeleteArmedUntil = Environment.TickCount64 + DELETE_CONFIRM_MS;
            DeleteArmedName = name;
            DeleteButton.Caption = "Sure?";

            return;
        }

        DisarmDelete();
        Send(new StageLightingInteractionArgs { Action = StageLightingAction.DeleteScene, SceneName = name });
    }

    private void DisarmDelete()
    {
        DeleteArmedUntil = 0;
        DeleteArmedName = null;
        DeleteButton.Caption = "Delete";
    }

    private void ApplySaveMode()
    {
        SceneList.Visible = !SaveMode;
        LoadButton.Visible = !SaveMode;
        SaveAsButton.Visible = !SaveMode;
        DeleteButton.Visible = !SaveMode;
        SceneName.Visible = SaveMode;
        SaveButton.Visible = SaveMode;
        CancelButton.Visible = SaveMode;
    }

    /// <summary>Enter saves and Escape cancels while the scene-name box is up.</summary>
    private void OnBodyKey(KeyDownEvent e)
    {
        if (!SaveMode)
            return;

        switch (e.Keycode)
        {
            case Keycode.Enter:
                SaveScene();
                e.Handled = true;

                break;
            case Keycode.Escape:
                ExitSaveMode();
                e.Handled = true;

                break;
        }
    }

    //── layout ──

    private void SetMinimized(bool minimized)
    {
        Panel.IsMinimized = minimized;
        Picker.Visible = false;
        SceneList.Close();
        ExitSaveMode();
        ApplyMode();
    }

    private void ApplyMode()
    {
        var minimized = Panel.IsMinimized;
        Full.Visible = !minimized;
        Mini.Visible = minimized;
        TitleBand.Visible = !minimized;
        TitleLabel.Visible = !minimized;
        MinimizeButton.Visible = !minimized;
        CloseButton.Visible = !minimized;
        Width = minimized ? MINI_WIDTH : FULL_WIDTH;
        Height = minimized ? MINI_HEIGHT : FULL_HEIGHT;
        KeepOnScreen();
    }

    private void KeepOnScreen()
    {
        X = Math.Clamp(X, 0, ChaosGame.VIRTUAL_WIDTH - Width);
        Y = Math.Clamp(Y, 0, ChaosGame.VIRTUAL_HEIGHT - Height);
    }

    /// <summary>
    ///     The <paramref name="index" />'th entry of <paramref name="names" />, or <paramref name="value" />'s own
    ///     <see cref="object.ToString" /> when the index is out of range (an enum value the caption arrays don't cover).
    /// </summary>
    private static string NameOrDefault(string[] names, int index, Enum value)
        => ((index >= 0) && (index < names.Length)) ? names[index] : value.ToString();

    private static StageButton Button(UIPanel parent, string caption, int x, int y, int width, Action onClick)
    {
        var button = new StageButton(caption, width) { X = x, Y = y };
        button.Clicked += onClick;
        parent.AddChild(button);

        return button;
    }

    private static StageSlider Slider(UIPanel parent, int min, int max, int x, int y, int width)
    {
        var slider = new StageSlider(min, max) { X = x, Y = y, Width = width };
        parent.AddChild(slider);

        return slider;
    }

    private static UILabel Label(
        UIPanel parent,
        string text,
        int x,
        int y,
        int width,
        Color color,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        //no padding: the rows are laid out on the 6x12 font's exact cells
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            PaddingLeft = 0,
            PaddingRight = 0,
            PaddingTop = 0,
            PaddingBottom = 0,
            HorizontalAlignment = alignment,
            ForegroundColor = color,
            IsHitTestVisible = false,
            Text = text
        };

        parent.AddChild(label);

        return label;
    }

    /// <summary>The window body. Hands key presses to the window, which only wants Enter and Escape from the scene-name box.</summary>
    private sealed class BodyPanel : UIPanel
    {
        public Action<KeyDownEvent>? KeyPressed { get; init; }

        public override void OnKeyDown(KeyDownEvent e)
        {
            KeyPressed?.Invoke(e);

            if (!e.Handled)
                base.OnKeyDown(e);
        }
    }
}
