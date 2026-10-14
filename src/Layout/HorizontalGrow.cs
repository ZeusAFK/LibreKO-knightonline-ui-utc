using Godot;

namespace KnightOnlineUi.Layout;

public sealed class HorizontalGrow
{
    public enum Mode { Stay, Shift, Stretch }

    private readonly List<(Control Control, float X, float Width, Mode Mode)> parts = new();

    public void Add(Control control, Mode mode)
    {
        if (mode == Mode.Stay) return;
        parts.Add((control, control.Position.X, control.Size.X, mode));
    }

    public void Apply(float extra)
    {
        foreach (var (control, x, width, mode) in parts)
        {
            if (!GodotObject.IsInstanceValid(control)) continue;
            if (mode == Mode.Shift)
                control.Position = new Vector2(x + extra, control.Position.Y);
            else
            {
                control.Position = new Vector2(x, control.Position.Y);
                control.Size = new Vector2(width + extra, control.Size.Y);
            }
        }
    }
}
