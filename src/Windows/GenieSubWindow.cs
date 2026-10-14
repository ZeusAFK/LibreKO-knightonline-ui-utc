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

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly LayoutNode layout;
    private HudLayout? hudLayout;

    public GenieSubWindow()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(view);
        view.MakeDragHandle(new Rect2(0, 0, view.Size.X * 0.5f, TitleBarHeight));
        view.OnPressed("btn_genie_start", () => game.Genie.ToggleRun());
        view.OnPressed("btn_genie_stop", () => game.Genie.ToggleRun());
        view.OnPressed("btn_genie_open", () => game.Genie.ToggleWindow());
        if (view.Get<Label>("str_progress") is { } points)
        {
            points.ZIndex = 1;
            points.Position -= new Vector2(0, PointsLift);
            points.AddThemeFontSizeOverride("font_size", PointsFontSize);
            points.AddThemeConstantOverride("outline_size", PointsOutline);
            points.AddThemeColorOverride("font_outline_color", Colors.Black);
        }
        if (view.Get<TextureButton>("btn_genie_start") is { } start) start.TooltipText = ItemData.Text(31008, "Start Knight Genie");
        if (view.Get<TextureButton>("btn_genie_stop") is { } stop) stop.TooltipText = ItemData.Text(31009, "Disable Knight Genie");
        for (int i = 0; i < Rows.Length; i++)
        {
            int row = i;
            var (on, _, add, back) = Rows[i];
            if (view.Get<TextureButton>(on) is { } box)
            {
                box.ToggleMode = true;
                box.Toggled += pressed => ToggleRow(row, pressed);
            }
            if (add != null) view.OnPressed(add, () => game.Genie.AddTarget());
            if (back != null) view.OnPressed(back, () => game.Genie.RemoveTarget(row));
        }
    }

    public override void _EnterTree()
    {
        game.Genie.Changed += Refresh;
        game.BecameAvailable += Refresh;
        hudLayout ??= HudLayout.Attach(this, LayoutId, view.DragHandle ?? (Control)view, DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Genie.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - layout.W - ScreenMargin, room.Y * 0.5f);
    }

    private void ToggleRow(int row, bool on)
    {
        var targets = game.Genie.Targets;
        int last = Rows.Length - 1;
        if (row == last)
        {
            if (on && targets.Count == last) game.Genie.AddTarget();
            else if (!on && row < targets.Count) game.Genie.RemoveTarget(row);
            Refresh();
            return;
        }
        if (row < targets.Count) game.Genie.SetTargetOn(row, on);
        else Refresh();
    }

    private void Refresh()
    {
        var genie = game.Genie;
        Visible = genie.Points > 0 || genie.Running;
        bool running = genie.Running;
        if (view.Get("btn_genie_start") is { } start) start.Visible = !running;
        if (view.Get("btn_genie_stop") is { } stop) stop.Visible = running;
        view.SetProgress("progress_sprits", genie.Points / (float)Math.Max(genie.PointsPerSpirit, Math.Max(1, genie.Points)));
        view.SetText("str_progress", genie.Points.ToString());
        var targets = genie.Targets;
        for (int i = 0; i < Rows.Length; i++)
        {
            var (on, name, add, back) = Rows[i];
            bool filled = i < targets.Count;
            view.SetText(name, filled ? targets[i].Name : "");
            if (view.Get<TextureButton>(on) is { } box)
            {
                box.SetPressedNoSignal(filled && targets[i].On);
                box.Disabled = i == Rows.Length - 1 ? targets.Count < Rows.Length - 1 : !filled;
            }
            if (add != null && view.Get(add) is { } addButton) addButton.Visible = !filled;
            if (back != null && view.Get(back) is { } backButton) backButton.Visible = filled;
        }
    }
}
