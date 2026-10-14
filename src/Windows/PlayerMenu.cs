using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class PlayerMenu : Control
{
    private const string LayoutName = "re_minimenu";
    private const string HeadId = "img_head";
    private const int DynamicRows = 5;
    private const int CaptionFontSize = 11;
    private static readonly Color CaptionColor = new(0.96f, 0.93f, 0.80f);

    private static readonly (string Button, string Key)[] FixedRows =
    {
        ("btn_default00", "trade"),
        ("btn_default01", "whisper"),
        ("btn_default02", "friend"),
        ("btn_default03", "info"),
        ("btn_default04", "duel"),
        ("btn_default05", "equipment"),
    };

    private readonly PlayerMenuRequest request;
    private readonly LayoutView view;

    public PlayerMenu(PlayerMenuRequest request)
    {
        this.request = request;
        var kit = Plugin.Kit;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        GuiInput += ev => { if (ev is InputEventMouseButton { Pressed: true }) request.Close(); };

        view = new LayoutView(kit, kit.Layout(LayoutName));
        AddChild(view);

        var rows = request.Rows.ToDictionary(r => r.Key);
        foreach (var (button, key) in FixedRows)
        {
            if (view.Get<BaseButton>(button) is not { } b) continue;
            if (rows.Remove(key, out var row)) b.Pressed += row.Run;
            else b.Disabled = true;
        }

        var extra = request.Rows.Where(r => rows.ContainsKey(r.Key)).Take(DynamicRows).ToList();
        int first = DynamicRows - extra.Count;
        for (int i = 0; i < DynamicRows; i++)
        {
            string button = $"btn_dynamic{i:00}";
            string image = $"img_dynamic{i:00}";
            int index = i - first;
            if (index < 0)
            {
                view.Hide(button, image);
                continue;
            }
            var row = extra[index];
            if (view.Get<BaseButton>(button) is { } b)
            {
                HideLabels(b);
                view.Caption(button, row.Caption, CaptionFontSize, CaptionColor);
                b.Pressed += row.Run;
            }
        }
        if (view.Get(HeadId) is { } head)
        {
            if (extra.Count == 0) head.Visible = false;
            else if (view.Get($"btn_dynamic{first:00}") is { } top) head.Position = new Vector2(head.Position.X, top.Position.Y - head.Size.Y);
        }
    }

    private static void HideLabels(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) label.Visible = false;
            HideLabels(child);
        }
    }

    public override void _EnterTree() => Callable.From(Place).CallDeferred();

    private void Place()
    {
        var room = GetViewport().GetVisibleRect().Size;
        var at = request.Position;
        view.Position = new Vector2(Mathf.Clamp(at.X, 0, room.X - view.Size.X), Mathf.Clamp(at.Y, 0, room.Y - view.Size.Y)).Round();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            request.Close();
        }
    }
}
