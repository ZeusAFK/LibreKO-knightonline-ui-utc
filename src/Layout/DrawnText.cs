using Godot;

namespace KnightOnlineUi.Layout;

public partial class DrawnText : Control
{
    private static readonly Color ShadowColor = new(0, 0, 0, 0.75f);
    private static readonly Vector2 ShadowOffset = new(1, 1);

    private readonly Font font;
    private readonly int size;
    private readonly Color color;
    private string text = "";

    public string Text
    {
        get => text;
        set
        {
            if (text == value) return;
            text = value;
            QueueRedraw();
        }
    }

    public DrawnText(Font font, int size, Color color)
    {
        this.font = font;
        this.size = size;
        this.color = color;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        if (text.Length == 0) return;
        var extent = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var at = new Vector2(Mathf.Round((Size.X - extent.X) * 0.5f), Mathf.Round((Size.Y - extent.Y) * 0.5f + font.GetAscent(size)));
        DrawString(font, at + ShadowOffset, text, HorizontalAlignment.Left, -1, size, ShadowColor);
        DrawString(font, at, text, HorizontalAlignment.Left, -1, size, color);
    }
}
