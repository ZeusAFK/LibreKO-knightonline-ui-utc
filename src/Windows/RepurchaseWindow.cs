using System.Collections.Generic;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class RepurchaseWindow : Control
{
    private const string LayoutName = "re_rebuy";
    private const string RowFormat = "group{0:00}";
    private const string MessageGroup = "group_message";
    private const int Rows = 10;
    private const int TitleBarHeight = 34;
    private static readonly Color Warning = new(1f, 0.25f, 0.25f);

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly LayoutNode?[] _rows = new LayoutNode?[Rows];
    private readonly Color[] _textColours = new Color[Rows];
    private int _first;
    private int _asking = -1;

    public RepurchaseWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        _view.OnPressed("btn_close", () => _game.Repurchase.Close());
        _view.OnPressed("btn_refresh", () => _game.Repurchase.Refresh());
        _view.Hide(MessageGroup, "btn_non_action");
        if (layout.Find(MessageGroup) is { } message)
        {
            _view.OnPressed(message, "btn_ok", Confirm);
            _view.OnPressed(message, "btn_cancel", () => Ask(-1));
        }
        for (int i = 0; i < Rows; i++)
        {
            if (layout.Find(string.Format(RowFormat, i + 1)) is not { } row) continue;
            _rows[i] = row;
            int slot = i;
            _textColours[i] = _view.Get<Label>(row, "text_price")?.GetThemeColor("font_color") ?? Colors.White;
            _view.OnPressed(row, "btn_rebuy", () => Ask(_first + slot));
            if (_view.Get(row, "area_tooltip") is { } tip)
            {
                tip.MouseFilter = MouseFilterEnum.Pass;
                tip.MouseEntered += () => _game.Repurchase.ShowTooltip(_first + slot);
                tip.MouseExited += () => _game.Repurchase.HideTooltip();
            }
        }
    }

    public override void _EnterTree()
    {
        _game.Repurchase.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _game.Repurchase.Changed -= Refresh;

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } wheel) return;
        int max = Mathf.Max(0, _game.Repurchase.Rows.Count - Rows);
        if (wheel.ButtonIndex == MouseButton.WheelUp) _first = Mathf.Max(0, _first - 1);
        else if (wheel.ButtonIndex == MouseButton.WheelDown) _first = Mathf.Min(max, _first + 1);
        else return;
        Refresh();
        AcceptEvent();
    }

    private void Ask(int index)
    {
        var rows = _game.Repurchase.Rows;
        _asking = index >= 0 && index < rows.Count ? index : -1;
        if (_view.Get(MessageGroup) is not { } message) return;
        message.Visible = _asking >= 0;
        if (_asking < 0) return;
        var row = rows[_asking];
        _view.SetText("text_message1", _game.Repurchase.AskText);
        _view.SetText("text_message2", row.Name);
        _view.SetText("text_message3", row.Price);
    }

    private void Confirm()
    {
        int index = _asking;
        Ask(-1);
        if (index >= 0) _game.Repurchase.Buy(index);
    }

    private void Refresh()
    {
        var rows = _game.Repurchase.Rows;
        _first = Mathf.Clamp(_first, 0, Mathf.Max(0, rows.Count - Rows));
        for (int i = 0; i < Rows; i++)
        {
            if (_rows[i] is not { } row) continue;
            int index = _first + i;
            bool used = index < rows.Count;
            if (_view.ControlOf(row) is { } frame) frame.Visible = used;
            if (!used) continue;
            var entry = rows[index];
            if (_view.Get<Label>(row, "text_name") is { } name)
            {
                name.Text = entry.Name;
                name.AddThemeColorOverride("font_color", entry.NameColour);
            }
            if (_view.Get<Label>(row, "text_price") is { } price)
            {
                price.Text = entry.Price;
                price.AddThemeColorOverride("font_color", entry.TooDear ? Warning : _textColours[i]);
            }
            if (_view.Get<Label>(row, "text_time") is { } time)
            {
                time.Text = entry.Time;
                time.AddThemeColorOverride("font_color", entry.Expired ? Warning : _textColours[i]);
            }
        }
        if (_asking >= rows.Count) Ask(-1);
    }
}
