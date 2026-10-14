using Godot;

namespace KnightOnlineUi.Layout;

public sealed class VerticalGrow
{
    public enum Mode { Stay, Shift, Stretch }

    public const float BottomBand = 30f;

    private readonly List<(Control Control, Vector2 Position, Vector2 Size, Mode Mode)> parts = new();
    private readonly HashSet<Control> placed = new();

    public void Add(Control control, Mode mode)
    {
        if (mode == Mode.Stay) return;
        parts.Add((control, control.Position, control.Size, mode));
        placed.Add(control);
    }

    public void ShiftBottomBand(Control parent)
    {
        float edge = parent.Size.Y - BottomBand;
        foreach (var child in parent.GetChildren().OfType<Control>())
            if (!placed.Contains(child) && child.Position.Y >= edge)
                Add(child, Mode.Shift);
    }

    public IEnumerable<Control> Stretched => parts.Where(p => p.Mode == Mode.Stretch).Select(p => p.Control).ToList();

    public void Apply(float extra)
    {
        foreach (var (control, position, size, mode) in parts)
        {
            if (!GodotObject.IsInstanceValid(control)) continue;
            if (mode == Mode.Shift)
                control.Position = position + new Vector2(0f, extra);
            else
            {
                control.Position = position;
                control.Size = size + new Vector2(0f, extra);
            }
        }
    }
}
