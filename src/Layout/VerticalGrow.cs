using Godot;

namespace KnightOnlineUi.Layout;

public sealed class VerticalGrow
{
    public enum Mode { Stay, Shift, Stretch }

    public const float BottomBand = 30f;

    private readonly List<(Control Control, Vector2 Position, Vector2 Size, Mode Mode)> _parts = new();
    private readonly HashSet<Control> _placed = new();

    public void Add(Control control, Mode mode)
    {
        if (mode == Mode.Stay) return;
        _parts.Add((control, control.Position, control.Size, mode));
        _placed.Add(control);
    }

    public void ShiftBottomBand(Control parent)
    {
        float edge = parent.Size.Y - BottomBand;
        foreach (var child in parent.GetChildren().OfType<Control>())
            if (!_placed.Contains(child) && child.Position.Y >= edge)
                Add(child, Mode.Shift);
    }

    public IEnumerable<Control> Stretched => _parts.Where(p => p.Mode == Mode.Stretch).Select(p => p.Control).ToList();

    public void Apply(float extra)
    {
        foreach (var (control, position, size, mode) in _parts)
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
