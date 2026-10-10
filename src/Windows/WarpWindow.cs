using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class WarpWindow : Control
{
    private const string LayoutName = "re_warp";
    private const string RowPrefix = "str_warp";
    private const string SelectBar = "img_select";
    private const string ZoneImage = "img_zone";
    private const int Rows = 9;
    private const int TitleBarHeight = 40;
    private const float BlockedAlpha = 0.5f;
    private static readonly Color BlockedColour = new(1f, 0.38f, 0.38f);

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly Label?[] _rows = new Label?[Rows];
    private readonly Color[] _rowColours = new Color[Rows];
    private TextureRect? _zone;
    private Control? _select;
    private int _first;

    public WarpWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        _view.OnPressed("btn_close", () => _game.Warp.Close());
        _view.OnPressed("Btn_Cancel", () => _game.Warp.Close());
        _view.OnPressed("Btn_Ok", () => _game.Warp.Travel());
        if (_view.Get(ZoneImage) is { } zoneArea)
        {
            _zone = new TextureRect
            {
                Size = zoneArea.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
                ClipContents = true,
            };
            zoneArea.AddChild(_zone);
        }
        _select = _view.Get(SelectBar);
        if (_view.Get<Label>("Text_Agreement") is { } text) text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        for (int i = 0; i < Rows; i++)
        {
            var label = _view.Get<Label>(RowPrefix + i);
            _rows[i] = label;
            if (label == null) continue;
            _rowColours[i] = label.GetThemeColor("font_color");
            int slot = i;
            label.MouseFilter = MouseFilterEnum.Stop;
            label.GuiInput += ev => OnRowInput(slot, ev);
        }
    }

    public override void _EnterTree()
    {
        _game.Warp.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _game.Warp.Changed -= Refresh;

    private void OnRowInput(int slot, InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        int index = _first + slot;
        if (index >= _game.Warp.Rows.Count) return;
        _game.Warp.Select(index);
        if (click.DoubleClick) _game.Warp.Travel();
        AcceptEvent();
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } wheel) return;
        int max = Mathf.Max(0, _game.Warp.Rows.Count - Rows);
        if (wheel.ButtonIndex == MouseButton.WheelUp) _first = Mathf.Max(0, _first - 1);
        else if (wheel.ButtonIndex == MouseButton.WheelDown) _first = Mathf.Min(max, _first + 1);
        else return;
        Refresh();
        AcceptEvent();
    }

    private void Refresh()
    {
        var warp = _game.Warp;
        var rows = warp.Rows;
        int selected = warp.Selected;
        if (selected >= 0 && (selected < _first || selected >= _first + Rows))
            _first = Mathf.Clamp(selected - Rows + 1, 0, Mathf.Max(0, rows.Count - Rows));
        for (int i = 0; i < Rows; i++)
        {
            if (_rows[i] is not { } label) continue;
            int index = _first + i;
            bool used = index < rows.Count;
            label.Visible = used;
            if (!used) continue;
            var row = rows[index];
            label.Text = $"{row.Name}   {row.Fee}";
            label.TooltipText = row.Blocked ?? row.Levels;
            label.AddThemeColorOverride("font_color", row.Blocked != null ? BlockedColour : _rowColours[i]);
            label.Modulate = new Color(1f, 1f, 1f, row.Blocked != null ? BlockedAlpha : 1f);
        }
        if (_select != null)
        {
            int slot = selected - _first;
            _select.Visible = slot >= 0 && slot < Rows && _rows[slot] != null;
            if (_select.Visible) _select.Position = new Vector2(_select.Position.X, _rows[slot]!.Position.Y);
        }
        if (selected >= 0 && selected < rows.Count)
        {
            var row = rows[selected];
            _view.SetText("str_level", row.Levels);
            _view.SetText("Text_Agreement", row.Blocked ?? row.Description);
            if (_zone != null) _zone.Texture = row.Image;
            if (_view.Get<BaseButton>("Btn_Ok") is { } ok) ok.Disabled = row.Blocked != null;
        }
    }
}
