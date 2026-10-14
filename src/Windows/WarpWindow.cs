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

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly Label?[] rows = new Label?[Rows];
    private readonly Color[] rowColours = new Color[Rows];
    private TextureRect? zone;
    private Control? select;
    private int first;

    public WarpWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        view.OnPressed("btn_close", () => game.Warp.Close());
        view.OnPressed("Btn_Cancel", () => game.Warp.Close());
        view.OnPressed("Btn_Ok", () => game.Warp.Travel());
        if (view.Get(ZoneImage) is { } zoneArea)
        {
            zone = new TextureRect
            {
                Size = zoneArea.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
                ClipContents = true,
            };
            zoneArea.AddChild(zone);
        }
        select = view.Get(SelectBar);
        if (view.Get<Label>("Text_Agreement") is { } text) text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        for (int i = 0; i < Rows; i++)
        {
            var label = view.Get<Label>(RowPrefix + i);
            rows[i] = label;
            if (label == null) continue;
            rowColours[i] = label.GetThemeColor("font_color");
            int slot = i;
            label.MouseFilter = MouseFilterEnum.Stop;
            label.GuiInput += ev => OnRowInput(slot, ev);
        }
    }

    public override void _EnterTree()
    {
        game.Warp.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => game.Warp.Changed -= Refresh;

    private void OnRowInput(int slot, InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        int index = first + slot;
        if (index >= game.Warp.Rows.Count) return;
        game.Warp.Select(index);
        if (click.DoubleClick) game.Warp.Travel();
        AcceptEvent();
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } wheel) return;
        int max = Mathf.Max(0, game.Warp.Rows.Count - Rows);
        if (wheel.ButtonIndex == MouseButton.WheelUp) first = Mathf.Max(0, first - 1);
        else if (wheel.ButtonIndex == MouseButton.WheelDown) first = Mathf.Min(max, first + 1);
        else return;
        Refresh();
        AcceptEvent();
    }

    private void Refresh()
    {
        var warp = game.Warp;
        var warpRows = warp.Rows;
        int selected = warp.Selected;
        if (selected >= 0 && (selected < first || selected >= first + Rows))
            first = Mathf.Clamp(selected - Rows + 1, 0, Mathf.Max(0, warpRows.Count - Rows));
        for (int i = 0; i < Rows; i++)
        {
            if (rows[i] is not { } label) continue;
            int index = first + i;
            bool used = index < warpRows.Count;
            label.Visible = used;
            if (!used) continue;
            var row = warpRows[index];
            label.Text = $"{row.Name}   {row.Fee}";
            label.TooltipText = row.Blocked ?? row.Levels;
            label.AddThemeColorOverride("font_color", row.Blocked != null ? BlockedColour : rowColours[i]);
            label.Modulate = new Color(1f, 1f, 1f, row.Blocked != null ? BlockedAlpha : 1f);
        }
        if (select != null)
        {
            int slot = selected - first;
            select.Visible = slot >= 0 && slot < Rows && rows[slot] != null;
            if (select.Visible) select.Position = new Vector2(select.Position.X, rows[slot]!.Position.Y);
        }
        if (selected >= 0 && selected < warpRows.Count)
        {
            var row = warpRows[selected];
            view.SetText("str_level", row.Levels);
            view.SetText("Text_Agreement", row.Blocked ?? row.Description);
            if (zone != null) zone.Texture = row.Image;
            if (view.Get<BaseButton>("Btn_Ok") is { } ok) ok.Disabled = row.Blocked != null;
        }
    }
}
