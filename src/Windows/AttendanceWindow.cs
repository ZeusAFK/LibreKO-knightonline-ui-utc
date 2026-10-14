using System.Linq;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class AttendanceWindow : Control
{
    private const string LayoutName = "re_attendance_check";
    private const string DailyPrefix = "group_daily_reward_";
    private const string BonusPrefix = "group_cumulative_reward_";
    private const string SelectGroup = "group_select";
    private const int DailyCells = 25;
    private const int BonusCells = 3;
    private const int TitleBarHeight = 80;
    private const int TextCount = 33606;
    private const int TextNotify = 33610;
    private const int TextGuide = 33611;
    private const int TextBonusTitle = 33612;
    private const float ClaimedAlpha = 0.45f;
    private const float LockedAlpha = 0.6f;

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly LayoutNode?[] daily = new LayoutNode?[DailyCells];
    private readonly LayoutNode?[] bonus = new LayoutNode?[BonusCells];
    private Control? select;
    private readonly Label? count;
    private const string CountId = "str_cumulative_days";

    public AttendanceWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        view.OnPressed("btn_close", () => game.Attendance.Close());
        view.Hide("str_remaining_time");
        view.SetText("str_cumulative_info", ItemData.Text(TextBonusTitle, "Cumulative Attendance Bonus"));
        view.SetFittedText("str_notify", ItemData.Text(TextNotify, ""));
        view.SetFittedText("str_guide", ItemData.Text(TextGuide, ""));
        count = layout.Children.FirstOrDefault(n => n.Id == CountId) is { } countNode ? view.ControlOf(countNode) as Label : null;
        select = view.Get(SelectGroup);
        if (select != null)
        {
            select.Visible = false;
            select.MouseFilter = MouseFilterEnum.Ignore;
        }
        for (int i = 0; i < DailyCells; i++) daily[i] = Wire(layout.Find(DailyPrefix + i), i);
        for (int i = 0; i < BonusCells; i++) bonus[i] = Wire(layout.Find(BonusPrefix + i), DailyCells + i);
    }

    public override void _EnterTree()
    {
        game.Attendance.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => game.Attendance.Changed -= Refresh;

    private LayoutNode? Wire(LayoutNode? group, int index)
    {
        if (group == null || view.ControlOf(group) is not { } frame) return group;
        frame.MouseFilter = MouseFilterEnum.Stop;
        foreach (var child in group.Children)
            if (view.ControlOf(child) is { } part) part.MouseFilter = MouseFilterEnum.Ignore;
        frame.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            game.Attendance.Claim(index);
            frame.AcceptEvent();
        };
        frame.MouseEntered += () =>
        {
            game.Attendance.ShowTooltip(index);
            if (index < DailyCells && select != null)
            {
                select.Position = frame.Position;
                select.Visible = true;
            }
        };
        frame.MouseExited += () =>
        {
            game.Attendance.HideTooltip();
            if (select != null) select.Visible = false;
        };
        return group;
    }

    private void Refresh()
    {
        var attendance = game.Attendance;
        if (count != null)
            count.Text = ItemData.Text(TextCount, "Currently attended %d times").Replace("%d", attendance.Attended.ToString());
        var dailyCells = attendance.Daily;
        for (int i = 0; i < DailyCells; i++)
            Show(daily[i], i < dailyCells.Count ? dailyCells[i] : default, "str_days");
        var bonusCells = attendance.Bonus;
        for (int i = 0; i < BonusCells; i++)
            Show(bonus[i], i < bonusCells.Count ? bonusCells[i] : default, CountId);
    }

    private void Show(LayoutNode? group, GameAttendanceCell cell, string captionId)
    {
        if (group == null) return;
        if (view.Get(group, "img_accept") is { } accept) accept.Visible = cell.State == GameAttendanceState.Claimable;
        if (view.Get(group, "img_complete") is { } complete) complete.Visible = cell.State == GameAttendanceState.Claimed;
        if (view.Get<TextureRect>(group, "img_item_reward") is { } icon)
        {
            icon.Texture = cell.Icon;
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.Modulate = new Color(1f, 1f, 1f, cell.State switch
            {
                GameAttendanceState.Claimed => ClaimedAlpha,
                GameAttendanceState.Locked => LockedAlpha,
                _ => 1f,
            });
        }
        if (view.Get<Label>(group, "str_item_reward") is { } name) name.Text = cell.ItemName;
        if (view.Get<Label>(group, "str_item_reward_count") is { } count)
        {
            count.Visible = cell.Count > 1;
            count.Text = cell.Count.ToString();
        }
        if (view.Get<Label>(group, captionId) is { } caption) caption.Text = cell.Caption;
    }
}
