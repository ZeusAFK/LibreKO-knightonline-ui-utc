using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class ExitMenu : Control
{
    private const string LayoutName = "re_exitmenu";
    private const string ItemsPanel = "UI_0";
    private const string ButtonsPanel = "UI_1";
    private const string ScrollGroup = "scroll";
    private const int RowsShown = 3;
    private static readonly (string Button, string Key)[] Buttons =
    {
        ("btn_chr", "option"),
        ("btn_exit", "end_game"),
        ("btn_cancel", "cancel"),
    };

    private readonly ExitMenuRequest _request;
    private readonly LayoutView _view;
    private readonly LayoutNode? _items;
    private int _page;

    public ExitMenu(ExitMenuRequest request)
    {
        _request = request;
        var kit = Plugin.Kit;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        AddChild(_view);

        bool listed = request.TimedItems.Count > 0;
        _items = layout.Find(ItemsPanel);
        var shown = layout.Find(listed ? ItemsPanel : ButtonsPanel);
        var hidden = layout.Find(listed ? ButtonsPanel : ItemsPanel);
        if (hidden != null && _view.ControlOf(hidden) is { } off) off.Visible = false;
        if (shown == null) return;

        var rows = request.Rows.ToDictionary(r => r.Key);
        foreach (var (button, key) in Buttons)
        {
            if (!rows.TryGetValue(key, out var row)) continue;
            SetButtonText(shown, button, row.Caption);
            _view.OnPressed(shown, button, row.Run);
        }

        if (!listed || _items == null) return;
        if (_view.Get<Label>(_items, "title_notice") is { } notice)
        {
            notice.Text = request.Notice;
            notice.HorizontalAlignment = HorizontalAlignment.Center;
            notice.VerticalAlignment = VerticalAlignment.Center;
        }
        if (_items.Find(ScrollGroup) is { } scroll)
        {
            var arrows = scroll.Children.Where(n => n.IsButton).ToList();
            if (arrows.Count > 0 && _view.ControlOf(arrows[0]) is BaseButton up) up.Pressed += () => Page(-1);
            if (arrows.Count > 1 && _view.ControlOf(arrows[^1]) is BaseButton down) down.Pressed += () => Page(1);
        }
        Render();
    }

    private void SetButtonText(LayoutNode scope, string buttonId, string caption)
    {
        if (scope.Find(buttonId) is not { } button) return;
        foreach (var child in button.Children)
            if (_view.ControlOf(child) is Label label)
            {
                label.Text = caption;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                return;
            }
    }

    private void Page(int step)
    {
        int pages = Math.Max(1, (_request.TimedItems.Count + RowsShown - 1) / RowsShown);
        _page = Math.Clamp(_page + step, 0, pages - 1);
        Render();
    }

    private void Render()
    {
        if (_items == null) return;
        for (int i = 0; i < RowsShown; i++)
        {
            int index = _page * RowsShown + i;
            bool filled = index < _request.TimedItems.Count;
            if (_view.Get(_items, $"ItemInfo_{i}") is { } info) info.Visible = filled;
            if (!filled) continue;
            var item = _request.TimedItems[index];
            if (_view.Get(_items, $"redbar_{i}") is { } red) red.Visible = item.Urgent;
            if (_view.Get(_items, $"normalbar_{i}") is { } normal) normal.Visible = !item.Urgent;
            if (_view.Get<Label>(_items, $"str_name_{i}") is { } name) name.Text = item.Name;
            if (_view.Get<Label>(_items, $"str_pos_{i}") is { } place) place.Text = item.Place;
            if (_view.Get<Label>(_items, $"str_time_{i}") is { } time) time.Text = item.TimeLeft;
            if (_view.Get(_items, $"area_icon_{i}") is { } area) SetIcon(area, item.Icon);
        }
    }

    private static void SetIcon(Control area, Texture2D? icon)
    {
        var picture = area.GetNodeOrNull<TextureRect>("icon");
        if (picture == null)
        {
            picture = new TextureRect
            {
                Name = "icon",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            picture.SetAnchorsPreset(LayoutPreset.FullRect);
            area.AddChild(picture);
        }
        picture.Texture = icon;
    }

    public override void _EnterTree()
    {
        GetViewport().SizeChanged += Center;
        Center();
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Center;

    private void Center()
    {
        var room = GetViewport().GetVisibleRect().Size;
        _view.Position = ((room - _view.Size) * 0.5f).Round();
    }
}
