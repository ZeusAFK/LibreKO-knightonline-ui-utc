using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class MiniMapFrame : Control
{
    private const string LayoutName = "re_minimap";
    private const string MapArea = "Img_MiniMap";
    private const string StateBar = "btn_StateBar";
    private const int LocationFontSize = 12;
    private const float LocationTop = 4f;
    private const float LocationHeight = 16f;
    private const float FrameWidth = 240f;
    private static readonly string[] HiddenIds =
    {
        "Group_WarMap", "Group_Battle", "Group_Toggle", "Group_Mark", "Group_Note", "Group_NpcList", "scroll_Alpah_line",
        "btn_battle_p", "btn_battle_n", "Btn_WarMap", "btn_NpcListopen", "btn_paper", "btn_paper_re", "area_note",
        "str_zoneid", "Text_Position",
    };

    private readonly LayoutView _view;
    private readonly LayoutNode _layout;
    private readonly PluginGame _game;
    private readonly MapCanvas _canvas;
    private readonly Label _location;
    private bool _collapsed;
    private string _locationLine = "";

    public MiniMapFrame()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, _layout);
        Position = StatusHud.ScreenPosition + new Vector2(0, StatusHud.LocationBandTop);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(FrameWidth, _layout.H);
        AddChild(_view);
        _view.Hide(HiddenIds);

        var area = _view.Get(MapArea);
        _canvas = new MapCanvas(_game) { Position = area?.Position ?? Vector2.Zero, Size = area?.Size ?? new Vector2(200, 197) };
        (area?.GetParent() ?? (Node)_view).AddChild(_canvas);
        if (area != null) _canvas.GetParent().MoveChild(_canvas, area.GetIndex() + 1);

        _view.OnPressed("Btn_ZoomIn", () => _canvas.Zoom(-1));
        _view.OnPressed("Btn_ZoomOut", () => _canvas.Zoom(1));
        _view.OnPressed("Btn_globalmap", () => _game.Windows.Toggle("globalmap"));
        _view.OnPressed(StateBar, () => SetCollapsed(!_collapsed));
        var band = _layout.Find(StateBar);
        _location = new Label
        {
            Position = new Vector2(0, LocationTop),
            Size = new Vector2(band?.W ?? FrameWidth, LocationHeight),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _location.AddThemeFontSizeOverride("font_size", LocationFontSize);
        _location.AddThemeFontOverride("font", kit.Regular);
        _location.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        _location.AddThemeConstantOverride("shadow_offset_x", 1);
        _location.AddThemeConstantOverride("shadow_offset_y", 1);
        _view.AddChild(_location);
        _view.OnPressed("btn_battle_a", _canvas.ToggleBlips);
        Refresh();
    }

    public override void _EnterTree()
    {
        _game.Map.Changed += Refresh;
        _game.BecameAvailable += Refresh;
    }

    public override void _ExitTree()
    {
        _game.Map.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
    }

    private void SetCollapsed(bool collapsed)
    {
        _collapsed = collapsed;
        foreach (var child in _layout.Children)
        {
            if (child.Id == StateBar) continue;
            if (_view.ControlOf(child) is { } control) control.Visible = !collapsed;
        }
        _canvas.Visible = !collapsed;
        if (!collapsed) _view.Hide(HiddenIds);
    }

    private void Refresh()
    {
        var m = _game.Map;
        string line = $"{m.ZoneName} {m.X:0}, {m.Z:0}";
        if (line != _locationLine)
        {
            _locationLine = line;
            _location.Text = line;
        }
        _canvas.QueueRedraw();
    }
}

public partial class MapCanvas : Control
{
    private static readonly float[] ZoomRadii = { 90f, 140f, 220f, 340f };
    private const int DefaultZoom = 1;
    private static readonly Color Background = new(0.03f, 0.035f, 0.045f);
    private static readonly Color PlayerColor = new(1f, 0.95f, 0.6f);
    private const float PlayerArrow = 6f;

    private readonly PluginGame _game;
    private int _zoom = DefaultZoom;
    private bool _showBlips = true;

    public MapCanvas(PluginGame game)
    {
        _game = game;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void ToggleBlips()
    {
        _showBlips = !_showBlips;
        QueueRedraw();
    }

    public void Zoom(int dir)
    {
        _zoom = Mathf.Clamp(_zoom + dir, 0, ZoomRadii.Length - 1);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var m = _game.Map;
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        float radius = ZoomRadii[_zoom];
        var tex = m.MapTexture;
        if (tex != null && m.WorldExtent > 0f)
        {
            float texPerWorld = tex.GetWidth() / m.WorldExtent;
            var centre = new Vector2(m.X * texPerWorld, (m.WorldExtent - m.Z) * texPerWorld);
            var srcSize = new Vector2(2f * radius * texPerWorld, 2f * radius * texPerWorld * (Size.Y / Size.X));
            var src = new Rect2(centre - srcSize * 0.5f, srcSize);
            DrawTextureRectRegion(tex, new Rect2(Vector2.Zero, Size), src);
        }
        float pxPerWorld = Size.X * 0.5f / radius;
        var mid = Size * 0.5f;
        foreach (var b in _showBlips ? m.Blips : Array.Empty<MiniMap.Blip>())
        {
            var p = mid + new Vector2((b.X - m.X) * pxPerWorld, -(b.Z - m.Z) * pxPerWorld);
            if (p.X < 0 || p.Y < 0 || p.X > Size.X || p.Y > Size.Y) continue;
            if (b.Hollow) DrawArc(p, b.Radius, 0f, Mathf.Tau, 16, b.Color, 1.5f, true);
            else DrawCircle(p, b.Radius, b.Color);
        }
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
