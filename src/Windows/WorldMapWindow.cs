using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class WorldMapWindow : Control
{
    private const string LayoutName = "re_map1024";
    private const string FrameGroup = "Group_uiform";
    private const string ClientArea = "area_client";
    private const string ZoneListGroup = "Group_list";
    private const string ZoneListToggle = "btn_togglelist";
    private const float TitleBarHeight = 44f;
    private const int ZoneNameFontSize = 12;
    private static readonly Color ZoneNameColor = new(0.97f, 0.90f, 0.62f);
    private static readonly string[] HiddenFrameIds = { "btn_togglefindlist", "btn_togglelist", "string_find_select", "string_select" };

    private static readonly Dictionary<int, string> ZoneGroups = new()
    {
        [1] = "Group_karubase",
        [2] = "Group_elmobase",
        [5] = "Group_karu_old",
        [7] = "Group_elmo_old",
        [11] = "Group_islant",
        [12] = "Group_islant",
        [13] = "Group_islant",
        [15] = "Group_islant",
        [21] = "Group_moradon",
        [22] = "Group_moradon",
        [30] = "Group_dellos",
        [66] = "Group_luna_F",
        [71] = "Group_cultivation_new_new",
        [72] = "Group_cultivation_old",
        [73] = "Group_ronak_base",
    };

    private readonly LayoutView view;
    private readonly LayoutNode layout;
    private readonly PluginGame game;
    private readonly WindowHost host;
    private readonly MapOverlay overlay;
    private readonly Label zoneName;
    private string shownGroup = "";

    public WorldMapWindow(WindowHost host)
    {
        this.host = host;
        var kit = Plugin.Kit;
        game = kit.Game;
        layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = layout.SizeVec;
        Size = layout.SizeVec;
        AddChild(view);
        foreach (var child in layout.Children)
            if (child.Type == "base" && child.Id != FrameGroup && view.ControlOf(child) is { } group)
                group.Visible = false;
        view.Hide(HiddenFrameIds);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, layout.W, TitleBarHeight)));
        view.OnPressed("btn_close", host.Close);

        var list = layout.Find(ZoneListGroup);
        var toggle = layout.Find(FrameGroup)?.Children.LastOrDefault(c => c.Id == ZoneListToggle);
        float left = (list?.X ?? 0) - layout.X;
        float right = (toggle?.X ?? layout.W) - layout.X;
        zoneName = new Label
        {
            Position = new Vector2(left, (toggle?.Y ?? 0) - layout.Y),
            Size = new Vector2(Mathf.Max(0f, right - left), toggle?.H ?? 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        zoneName.AddThemeFontSizeOverride("font_size", ZoneNameFontSize);
        zoneName.AddThemeFontOverride("font", kit.Regular);
        zoneName.AddThemeColorOverride("font_color", ZoneNameColor);
        view.AddChild(zoneName);

        overlay = new MapOverlay(game);
        view.AddChild(overlay);
        Refresh();
    }

    public override void _EnterTree()
    {
        game.Map.Changed += Refresh;
        game.BecameAvailable += Refresh;
        host.Shown += Refresh;
    }

    public override void _ExitTree()
    {
        game.Map.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
        host.Shown -= Refresh;
    }

    private void Refresh()
    {
        var m = game.Map;
        zoneName.Text = m.ZoneName;
        string groupId = ZoneGroups.TryGetValue(m.Zone, out var g) ? g : "";
        if (groupId != shownGroup)
        {
            if (shownGroup.Length > 0 && view.Get(shownGroup) is { } old) old.Visible = false;
            shownGroup = groupId;
            LayoutNode? area = null;
            if (groupId.Length > 0 && layout.Find(groupId) is { } group && view.ControlOf(group) is { } control)
            {
                control.Visible = true;
                area = group.Children.FirstOrDefault(c => c.IsArea);
            }
            area ??= layout.Find(ClientArea);
            overlay.Painted = groupId.Length > 0;
            overlay.Position = area != null ? new Vector2(area.X - layout.X, area.Y - layout.Y) : Vector2.Zero;
            overlay.Size = area?.SizeVec ?? layout.SizeVec;
        }
        overlay.QueueRedraw();
    }
}

public partial class MapOverlay : Control
{
    private static readonly Color PlayerColor = new(0.35f, 1f, 0.35f);
    private static readonly Color Background = new(0.05f, 0.05f, 0.06f);
    private const float PlayerArrow = 9f;
    private const float BlipScale = 1.4f;

    public bool Painted { get; set; }

    private readonly PluginGame game;

    public MapOverlay(PluginGame game)
    {
        this.game = game;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var m = game.Map;
        if (!Painted)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Background);
            if (m.MapTexture is { } tex) DrawTextureRect(tex, new Rect2(Vector2.Zero, Size), false);
        }
        if (m.WorldExtent <= 0f) return;
        var scale = new Vector2(Size.X / m.WorldExtent, Size.Y / m.WorldExtent);
        foreach (var b in m.Blips)
        {
            var p = new Vector2(b.X * scale.X, (m.WorldExtent - b.Z) * scale.Y);
            if (b.Hollow) DrawArc(p, b.Radius * BlipScale, 0f, Mathf.Tau, 16, b.Color, 1.5f, true);
            else DrawCircle(p, b.Radius * BlipScale, b.Color);
        }
        var mid = new Vector2(m.X * scale.X, (m.WorldExtent - m.Z) * scale.Y);
        float heading = Mathf.DegToRad(m.HeadingDegrees);
        var dir = new Vector2(Mathf.Sin(heading), -Mathf.Cos(heading));
        var side = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon(new[]
        {
            mid + dir * PlayerArrow,
            mid - dir * PlayerArrow * 0.7f + side * PlayerArrow * 0.6f,
            mid - dir * PlayerArrow * 0.7f - side * PlayerArrow * 0.6f,
        }, PlayerColor);
    }
}
