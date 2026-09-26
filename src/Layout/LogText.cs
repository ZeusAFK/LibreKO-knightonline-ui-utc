using Godot;

namespace KnightOnlineUi.Layout;

public partial class LogText : Control
{
    private const int MaxLines = 200;
    private const float WheelStep = 24f;
    private static readonly Color ShadowColor = new(0, 0, 0, 0.75f);

    private readonly RichTextLabel _label;
    private readonly Queue<string> _lines = new();
    private float _scroll;

    public LogText(int fontSize, Color color, Font font)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        _label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _label.AddThemeFontOverride("normal_font", font);
        _label.AddThemeFontSizeOverride("normal_font_size", fontSize);
        _label.AddThemeFontSizeOverride("bold_font_size", fontSize);
        _label.AddThemeColorOverride("default_color", color);
        _label.AddThemeColorOverride("font_shadow_color", ShadowColor);
        _label.AddThemeConstantOverride("shadow_offset_x", 1);
        _label.AddThemeConstantOverride("shadow_offset_y", 1);
        foreach (var state in new[] { "normal", "focus" })
            _label.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        AddChild(_label);
        Resized += Relayout;
        _label.Resized += Relayout;
    }

    public void Set(IEnumerable<string> lines)
    {
        _lines.Clear();
        foreach (var line in lines) _lines.Enqueue(line);
        Trim();
        Apply();
    }

    public void Append(string bbcode)
    {
        _lines.Enqueue(bbcode);
        Trim();
        Apply();
    }

    public void Scroll(float pixels)
    {
        _scroll += pixels;
        Relayout();
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } mb) return;
        if (mb.ButtonIndex == MouseButton.WheelUp) { Scroll(WheelStep); AcceptEvent(); }
        else if (mb.ButtonIndex == MouseButton.WheelDown) { Scroll(-WheelStep); AcceptEvent(); }
    }

    private void Trim()
    {
        while (_lines.Count > MaxLines) _lines.Dequeue();
    }

    private void Apply()
    {
        _scroll = 0f;
        _label.Text = string.Join("\n", _lines);
        Relayout();
    }

    private void Relayout()
    {
        _label.CustomMinimumSize = new Vector2(Size.X, 0);
        _label.Size = new Vector2(Size.X, 0);
        float overflow = Mathf.Max(0f, _label.Size.Y - Size.Y);
        _scroll = Mathf.Clamp(_scroll, 0f, overflow);
        _label.Position = new Vector2(0, Size.Y - _label.Size.Y + _scroll);
    }
}
