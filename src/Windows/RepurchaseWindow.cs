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

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly LayoutNode?[] rows = new LayoutNode?[Rows];
    private readonly Color[] textColours = new Color[Rows];
    private int first;
    private int asking = -1;

    public RepurchaseWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        view.OnPressed("btn_close", () => game.Repurchase.Close());
        view.OnPressed("btn_refresh", () => game.Repurchase.Refresh());
        view.Hide(MessageGroup, "btn_non_action");
        if (layout.Find(MessageGroup) is { } message)
        {
            view.OnPressed(message, "btn_ok", Confirm);
            view.OnPressed(message, "btn_cancel", () => Ask(-1));
        }
        for (int i = 0; i < Rows; i++)
        {
            if (layout.Find(string.Format(RowFormat, i + 1)) is not { } row) continue;
            rows[i] = row;
            int slot = i;
            textColours[i] = view.Get<Label>(row, "text_price")?.GetThemeColor("font_color") ?? Colors.White;
            view.OnPressed(row, "btn_rebuy", () => Ask(first + slot));
            if (view.Get(row, "area_tooltip") is { } tip)
            {
                tip.MouseFilter = MouseFilterEnum.Pass;
                tip.MouseEntered += () => game.Repurchase.ShowTooltip(first + slot);
                tip.MouseExited += () => game.Repurchase.HideTooltip();
            }
        }
    }

    public override void _EnterTree()
    {
        game.Repurchase.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => game.Repurchase.Changed -= Refresh;

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } wheel) return;
        int max = Mathf.Max(0, game.Repurchase.Rows.Count - Rows);
        if (wheel.ButtonIndex == MouseButton.WheelUp) first = Mathf.Max(0, first - 1);
        else if (wheel.ButtonIndex == MouseButton.WheelDown) first = Mathf.Min(max, first + 1);
        else return;
        Refresh();
        AcceptEvent();
    }

    private void Ask(int index)
    {
        var repurchaseRows = game.Repurchase.Rows;
        asking = index >= 0 && index < repurchaseRows.Count ? index : -1;
        if (view.Get(MessageGroup) is not { } message) return;
        message.Visible = asking >= 0;
        if (asking < 0) return;
        var row = repurchaseRows[asking];
        view.SetText("text_message1", game.Repurchase.AskText);
        view.SetText("text_message2", row.Name);
        view.SetText("text_message3", row.Price);
    }

    private void Confirm()
    {
        int index = asking;
        Ask(-1);
        if (index >= 0) game.Repurchase.Buy(index);
    }

    private void Refresh()
    {
        var repurchaseRows = game.Repurchase.Rows;
        first = Mathf.Clamp(first, 0, Mathf.Max(0, repurchaseRows.Count - Rows));
        for (int i = 0; i < Rows; i++)
        {
            if (rows[i] is not { } row) continue;
            int index = first + i;
            bool used = index < repurchaseRows.Count;
            if (view.ControlOf(row) is { } frame) frame.Visible = used;
            if (!used) continue;
            var entry = repurchaseRows[index];
            if (view.Get<Label>(row, "text_name") is { } name)
            {
                name.Text = entry.Name;
                name.AddThemeColorOverride("font_color", entry.NameColour);
            }
            if (view.Get<Label>(row, "text_price") is { } price)
            {
                price.Text = entry.Price;
                price.AddThemeColorOverride("font_color", entry.TooDear ? Warning : textColours[i]);
            }
            if (view.Get<Label>(row, "text_time") is { } time)
            {
                time.Text = entry.Time;
                time.AddThemeColorOverride("font_color", entry.Expired ? Warning : textColours[i]);
            }
        }
        if (asking >= repurchaseRows.Count) Ask(-1);
    }
}
