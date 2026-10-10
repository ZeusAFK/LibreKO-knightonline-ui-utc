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

    private readonly PlayerMenuRequest _request;
    private readonly LayoutView _view;

    public PlayerMenu(PlayerMenuRequest request)
    {
        _request = request;
        var kit = Plugin.Kit;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        GuiInput += ev => { if (ev is InputEventMouseButton { Pressed: true }) request.Close(); };

        _view = new LayoutView(kit, kit.Layout(LayoutName));
        AddChild(_view);

        var rows = request.Rows.ToDictionary(r => r.Key);
        foreach (var (button, key) in FixedRows)
        {
            if (_view.Get<BaseButton>(button) is not { } b) continue;
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
                _view.Hide(button, image);
                continue;
            }
            var row = extra[index];
            if (_view.Get<BaseButton>(button) is { } b)
            {
                HideLabels(b);
                _view.Caption(button, row.Caption, CaptionFontSize, CaptionColor);
                b.Pressed += row.Run;
            }
        }
        if (_view.Get(HeadId) is { } head)
        {
            if (extra.Count == 0) head.Visible = false;
            else if (_view.Get($"btn_dynamic{first:00}") is { } top) head.Position = new Vector2(head.Position.X, top.Position.Y - head.Size.Y);
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
        var at = _request.Position;
        _view.Position = new Vector2(Mathf.Clamp(at.X, 0, room.X - _view.Size.X), Mathf.Clamp(at.Y, 0, room.Y - _view.Size.Y)).Round();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            _request.Close();
        }
    }
}
