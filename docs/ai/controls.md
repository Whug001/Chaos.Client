## Control File Reference

Control files (`.txt` + `.spf`/`.epf`) define UI panel layouts. Loaded via `DataContext.UserControls.Get("_name")` -> `ControlPrefabSet`. Full catalog of all control files, their image references, consuming classes, format specification, and ControlType enum is in `controlFileList.txt` at solution root.

### Pattern: Building a Panel from a ControlPrefabSet
```csharp
// Extend PrefabPanel -- constructor handles anchor dimensions, centering, and background
public sealed class MyPanel : PrefabPanel
{
    public UIButton? OkButton { get; }
    public UIButton? CancelButton { get; }

    public MyPanel(GraphicsDevice device) : base(device, "_name")
    {
        // Create controls by name from the prefab definition
        OkButton = CreateButton("OK");
        CancelButton = CreateButton("Cancel");
        var title = CreateLabel("Title", TextAlignment.Center);
        var icon = CreateImage("Icon");
        var inputRect = GetRect("InputArea");  // rect-only lookup, no child created
    }
}
```
