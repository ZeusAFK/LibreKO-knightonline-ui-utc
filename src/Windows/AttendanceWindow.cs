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

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly LayoutNode?[] _daily = new LayoutNode?[DailyCells];
    private readonly LayoutNode?[] _bonus = new LayoutNode?[BonusCells];
    private Control? _select;
    private readonly Label? _count;
    private const string CountId = "str_cumulative_days";

    public AttendanceWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        _view.OnPressed("btn_close", () => _game.Attendance.Close());
        _view.Hide("str_remaining_time");
        _view.SetText("str_cumulative_info", ItemData.Text(TextBonusTitle, "Cumulative Attendance Bonus"));
        _view.SetFittedText("str_notify", ItemData.Text(TextNotify, ""));
        _view.SetFittedText("str_guide", ItemData.Text(TextGuide, ""));
        _count = layout.Children.FirstOrDefault(n => n.Id == CountId) is { } countNode ? _view.ControlOf(countNode) as Label : null;
        _select = _view.Get(SelectGroup);
        if (_select != null)
        {
            _select.Visible = false;
            _select.MouseFilter = MouseFilterEnum.Ignore;
        }
        for (int i = 0; i < DailyCells; i++) _daily[i] = Wire(layout.Find(DailyPrefix + i), i);
        for (int i = 0; i < BonusCells; i++) _bonus[i] = Wire(layout.Find(BonusPrefix + i), DailyCells + i);
    }

    public override void _EnterTree()
    {
        _game.Attendance.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _game.Attendance.Changed -= Refresh;

    private LayoutNode? Wire(LayoutNode? group, int index)
    {
        if (group == null || _view.ControlOf(group) is not { } frame) return group;
        frame.MouseFilter = MouseFilterEnum.Stop;
        foreach (var child in group.Children)
            if (_view.ControlOf(child) is { } part) part.MouseFilter = MouseFilterEnum.Ignore;
        frame.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            _game.Attendance.Claim(index);
            frame.AcceptEvent();
        };
        frame.MouseEntered += () =>
        {
            _game.Attendance.ShowTooltip(index);
            if (index < DailyCells && _select != null)
            {
                _select.Position = frame.Position;
                _select.Visible = true;
            }
        };
        frame.MouseExited += () =>
        {
            _game.Attendance.HideTooltip();
            if (_select != null) _select.Visible = false;
        };
        return group;
    }

    private void Refresh()
    {
        var attendance = _game.Attendance;
        if (_count != null)
            _count.Text = ItemData.Text(TextCount, "Currently attended %d times").Replace("%d", attendance.Attended.ToString());
        var daily = attendance.Daily;
        for (int i = 0; i < DailyCells; i++)
            Show(_daily[i], i < daily.Count ? daily[i] : default, "str_days");
        var bonus = attendance.Bonus;
        for (int i = 0; i < BonusCells; i++)
            Show(_bonus[i], i < bonus.Count ? bonus[i] : default, CountId);
    }

    private void Show(LayoutNode? group, GameAttendanceCell cell, string captionId)
    {
        if (group == null) return;
        if (_view.Get(group, "img_accept") is { } accept) accept.Visible = cell.State == GameAttendanceState.Claimable;
        if (_view.Get(group, "img_complete") is { } complete) complete.Visible = cell.State == GameAttendanceState.Claimed;
        if (_view.Get<TextureRect>(group, "img_item_reward") is { } icon)
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
        if (_view.Get<Label>(group, "str_item_reward") is { } name) name.Text = cell.ItemName;
        if (_view.Get<Label>(group, "str_item_reward_count") is { } count)
        {
            count.Visible = cell.Count > 1;
            count.Text = cell.Count.ToString();
        }
        if (_view.Get<Label>(group, captionId) is { } caption) caption.Text = cell.Caption;
    }
}
