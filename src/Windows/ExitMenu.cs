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

    private readonly ExitMenuRequest request;
    private readonly LayoutView view;
    private readonly LayoutNode? items;
    private int page;

    public ExitMenu(ExitMenuRequest request)
    {
        this.request = request;
        var kit = Plugin.Kit;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        AddChild(view);

        bool listed = request.TimedItems.Count > 0;
        items = layout.Find(ItemsPanel);
        var shown = layout.Find(listed ? ItemsPanel : ButtonsPanel);
        var hidden = layout.Find(listed ? ButtonsPanel : ItemsPanel);
        if (hidden != null && view.ControlOf(hidden) is { } off) off.Visible = false;
        if (shown == null) return;

        var rows = request.Rows.ToDictionary(r => r.Key);
        foreach (var (button, key) in Buttons)
        {
            if (!rows.TryGetValue(key, out var row)) continue;
            SetButtonText(shown, button, row.Caption);
            view.OnPressed(shown, button, row.Run);
        }

        if (!listed || items == null) return;
        if (view.Get<Label>(items, "title_notice") is { } notice)
        {
            notice.Text = request.Notice;
            notice.HorizontalAlignment = HorizontalAlignment.Center;
            notice.VerticalAlignment = VerticalAlignment.Center;
        }
        if (items.Find(ScrollGroup) is { } scroll)
        {
            var arrows = scroll.Children.Where(n => n.IsButton).ToList();
            if (arrows.Count > 0 && view.ControlOf(arrows[0]) is BaseButton up) up.Pressed += () => Page(-1);
            if (arrows.Count > 1 && view.ControlOf(arrows[^1]) is BaseButton down) down.Pressed += () => Page(1);
        }
        Render();
    }

    private void SetButtonText(LayoutNode scope, string buttonId, string caption)
    {
        if (scope.Find(buttonId) is not { } button) return;
        foreach (var child in button.Children)
            if (view.ControlOf(child) is Label label)
            {
                label.Text = caption;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                return;
            }
    }

    private void Page(int step)
    {
        int pages = Math.Max(1, (request.TimedItems.Count + RowsShown - 1) / RowsShown);
        page = Math.Clamp(page + step, 0, pages - 1);
        Render();
    }

    private void Render()
    {
        if (items == null) return;
        for (int i = 0; i < RowsShown; i++)
        {
            int index = page * RowsShown + i;
            bool filled = index < request.TimedItems.Count;
            if (view.Get(items, $"ItemInfo_{i}") is { } info) info.Visible = filled;
            if (!filled) continue;
            var item = request.TimedItems[index];
            if (view.Get(items, $"redbar_{i}") is { } red) red.Visible = item.Urgent;
            if (view.Get(items, $"normalbar_{i}") is { } normal) normal.Visible = !item.Urgent;
            if (view.Get<Label>(items, $"str_name_{i}") is { } name) name.Text = item.Name;
            if (view.Get<Label>(items, $"str_pos_{i}") is { } place) place.Text = item.Place;
            if (view.Get<Label>(items, $"str_time_{i}") is { } time) time.Text = item.TimeLeft;
            if (view.Get(items, $"area_icon_{i}") is { } area) SetIcon(area, item.Icon);
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
        view.Position = ((room - view.Size) * 0.5f).Round();
    }
}
