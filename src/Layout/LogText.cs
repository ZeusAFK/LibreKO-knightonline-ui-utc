using Godot;

namespace KnightOnlineUi.Layout;

public partial class LogText : Control
{
    private const int MaxLines = 200;
    private const float WheelStep = 24f;
    private static readonly Color ShadowColor = new(0, 0, 0, 0.75f);

    private readonly RichTextLabel label;
    private readonly Queue<string> lines = new();
    private float scroll;

    public LogText(int fontSize, Color color, Font font)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("normal_font", font);
        label.AddThemeFontSizeOverride("normal_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_font_size", fontSize);
        label.AddThemeColorOverride("default_color", color);
        label.AddThemeColorOverride("font_shadow_color", ShadowColor);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        foreach (var state in new[] { "normal", "focus" })
            label.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        AddChild(label);
        Resized += Relayout;
        label.Resized += Relayout;
    }

    public void EnableLinks(System.Action<string> clicked)
    {
        label.MouseFilter = MouseFilterEnum.Pass;
        label.MetaClicked += meta => clicked(meta.AsString());
    }

    public void Set(IEnumerable<string> lines)
    {
        this.lines.Clear();
        foreach (var line in lines) this.lines.Enqueue(line);
        Trim();
        Apply();
    }

    public void Append(string bbcode)
    {
        lines.Enqueue(bbcode);
        bool rebuild = false;
        while (lines.Count > MaxLines)
        {
            string dropped = lines.Dequeue();
            for (int n = dropped.Split('\n').Length; n > 0 && !rebuild; n--)
                rebuild = !label.RemoveParagraph(0, true);
        }
        scroll = 0f;
        if (rebuild) label.Text = string.Join("\n", lines);
        else
        {
            if (lines.Count > 1) label.AppendText("\n");
            label.AppendText(bbcode);
        }
        Relayout();
    }

    public void Scroll(float pixels)
    {
        scroll += pixels;
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
        while (lines.Count > MaxLines) lines.Dequeue();
    }

    private void Apply()
    {
        scroll = 0f;
        label.Text = string.Join("\n", lines);
        Relayout();
    }

    private void Relayout()
    {
        label.CustomMinimumSize = new Vector2(Size.X, 0);
        label.Size = new Vector2(Size.X, 0);
        float overflow = Mathf.Max(0f, label.Size.Y - Size.Y);
        scroll = Mathf.Clamp(scroll, 0f, overflow);
        label.Position = new Vector2(0, Size.Y - label.Size.Y + scroll);
    }
}
