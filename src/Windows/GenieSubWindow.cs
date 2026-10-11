using System;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class GenieSubWindow : Control
{
    private const string LayoutName = "re_genie_sub";
    private const string LayoutId = "theme_genie_sub";
    private const float ScreenMargin = 12f;
    private const int TitleBarHeight = 34;
    private const int PointsFontSize = 9;
    private const int PointsOutline = 3;
    private const float PointsLift = 5f;
    private static readonly (string On, string Name, string? Add, string? Return)[] Rows =
    {
        ("btn_TargetOneOn", "str_NameOne", "btn_TargetOneAdd", "btn_TargetOneReturn"),
        ("btn_TargetTwoOn", "str_NameTwo", "btn_TargetTwoAdd", "btn_TargetTwoReturn"),
        ("btn_TargetSamOn", "str_NameSam", null, null),
    };

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly LayoutNode _layout;
    private HudLayout? _hudLayout;

    public GenieSubWindow()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, _layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = _layout.SizeVec;
        AddChild(_view);
        _view.MakeDragHandle(new Rect2(0, 0, _view.Size.X * 0.5f, TitleBarHeight));
        _view.OnPressed("btn_genie_start", () => _game.Genie.ToggleRun());
        _view.OnPressed("btn_genie_stop", () => _game.Genie.ToggleRun());
        _view.OnPressed("btn_genie_open", () => _game.Genie.ToggleWindow());
        if (_view.Get<Label>("str_progress") is { } points)
        {
            points.ZIndex = 1;
            points.Position -= new Vector2(0, PointsLift);
            points.AddThemeFontSizeOverride("font_size", PointsFontSize);
            points.AddThemeConstantOverride("outline_size", PointsOutline);
            points.AddThemeColorOverride("font_outline_color", Colors.Black);
        }
        if (_view.Get<TextureButton>("btn_genie_start") is { } start) start.TooltipText = ItemData.Text(31008, "Start Knight Genie");
        if (_view.Get<TextureButton>("btn_genie_stop") is { } stop) stop.TooltipText = ItemData.Text(31009, "Disable Knight Genie");
        for (int i = 0; i < Rows.Length; i++)
        {
            int row = i;
            var (on, _, add, back) = Rows[i];
            if (_view.Get<TextureButton>(on) is { } box)
            {
                box.ToggleMode = true;
                box.Toggled += pressed => ToggleRow(row, pressed);
            }
            if (add != null) _view.OnPressed(add, () => _game.Genie.AddTarget());
            if (back != null) _view.OnPressed(back, () => _game.Genie.RemoveTarget(row));
        }
    }

    public override void _EnterTree()
    {
        _game.Genie.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _view.DragHandle ?? (Control)_view, DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Genie.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - _layout.W - ScreenMargin, room.Y * 0.5f);
    }

    private void ToggleRow(int row, bool on)
    {
        var targets = _game.Genie.Targets;
        int last = Rows.Length - 1;
        if (row == last)
        {
            if (on && targets.Count == last) _game.Genie.AddTarget();
            else if (!on && row < targets.Count) _game.Genie.RemoveTarget(row);
            Refresh();
            return;
        }
        if (row < targets.Count) _game.Genie.SetTargetOn(row, on);
        else Refresh();
    }

    private void Refresh()
    {
        var genie = _game.Genie;
        Visible = genie.Points > 0 || genie.Running;
        bool running = genie.Running;
        if (_view.Get("btn_genie_start") is { } start) start.Visible = !running;
        if (_view.Get("btn_genie_stop") is { } stop) stop.Visible = running;
        _view.SetProgress("progress_sprits", genie.Points / (float)Math.Max(genie.PointsPerSpirit, Math.Max(1, genie.Points)));
        _view.SetText("str_progress", genie.Points.ToString());
        var targets = genie.Targets;
        for (int i = 0; i < Rows.Length; i++)
        {
            var (on, name, add, back) = Rows[i];
            bool filled = i < targets.Count;
            _view.SetText(name, filled ? targets[i].Name : "");
            if (_view.Get<TextureButton>(on) is { } box)
            {
                box.SetPressedNoSignal(filled && targets[i].On);
                box.Disabled = i == Rows.Length - 1 ? targets.Count < Rows.Length - 1 : !filled;
            }
            if (add != null && _view.Get(add) is { } addButton) addButton.Visible = !filled;
            if (back != null && _view.Get(back) is { } backButton) backButton.Visible = filled;
        }
    }
}
