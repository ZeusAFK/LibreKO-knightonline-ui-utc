using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class MiniMapFrame : Control
{
    private const string LayoutName = "re_minimap";
    private const string MapArea = "Img_MiniMap";
    private const string StateBar = "btn_StateBar";
    private const string SettingCollapsed = "minimap.collapsed";
    private const int LocationFontSize = 12;
    private const float LocationTop = 4f;
    private const float LocationHeight = 16f;
    private const float FrameWidth = 240f;
    private static readonly string[] HiddenIds =
    {
        "Group_WarMap", "Group_Battle", "Group_Toggle", "Group_Mark", "Group_Note", "scroll_Alpah_line",
        "btn_battle_p", "btn_battle_n", "Btn_WarMap", "area_note",
        "str_zoneid", "Text_Position",
    };

    private const string WarMapGroup = "Group_WarMap";
    private const string WarScoreGroup = "Group_Toggle";
    private const string WarMapButton = "Btn_WarMap";
    private const string ElMoradScore = "elmo_score";
    private const string KarusScore = "Karu_score";
    private const string FlagSheet = "co_pat_ui.png";
    private const string FlagAreaFormat = "a{0:00}";
    private const string WarMapImage = "Img_WarMap";
    private const string WantedToggle = "btn_paper";
    private const string WantedOpen = "btn_paper_re";
    private static readonly Rect2[] FlagIcons = { new(275, 387, 35, 35), new(373, 388, 35, 35), new(321, 386, 35, 35) };

    private const string NpcListGroup = "Group_NpcList";
    private const string NpcListOpen = "btn_NpcListopen";
    private const string NpcListClose = "btn_Listminimize";
    private const string NpcRowChoice = "img_Choice";
    private const string NpcRowFormat = "str_NpcName_{0}";
    private const int NpcKinds = 12;
    private static readonly Color AbsentKind = new(0.5f, 0.5f, 0.5f);

    private readonly LayoutView view;
    private readonly Control? npcList;
    private readonly Control? npcChoice;
    private readonly List<Label> npcRows = new();
    private readonly List<Color> npcRowColours = new();
    private bool npcListOpen;
    private readonly LayoutNode layout;
    private readonly PluginGame game;
    private readonly MapCanvas canvas;
    private readonly Label location;
    private bool collapsed;
    private readonly PluginSettings settings;
    private string locationLine = "";
    private readonly TextureRect?[] flags = new TextureRect?[NereidMonuments.Count];
    private readonly AtlasTexture?[] flagArt = new AtlasTexture?[FlagIcons.Length];
    private WarArrow? warArrow;
    private bool warMapShown = true;
    private int zone = -1;

    public MiniMapFrame()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        settings = kit.Context.Settings;
        layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        Position = StatusHud.ScreenPosition + new Vector2(0, StatusHud.LocationBandTop);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(FrameWidth, layout.H);
        AddChild(view);
        view.Hide(HiddenIds);

        var area = view.Get(MapArea);
        canvas = new MapCanvas(game) { Position = area?.Position ?? Vector2.Zero, Size = area?.Size ?? new Vector2(200, 197) };
        (area?.GetParent() ?? (Node)view).AddChild(canvas);
        if (area != null) canvas.GetParent().MoveChild(canvas, area.GetIndex() + 1);

        view.OnPressed("Btn_ZoomIn", () => canvas.Zoom(-1));
        view.OnPressed("Btn_ZoomOut", () => canvas.Zoom(1));
        view.OnPressed("Btn_globalmap", () => game.Windows.Toggle("globalmap"));
        view.OnPressed(StateBar, () =>
        {
            SetCollapsed(!collapsed);
            settings.SetBool(SettingCollapsed, collapsed);
        });
        var band = layout.Find(StateBar);
        location = new Label
        {
            Position = new Vector2(0, LocationTop),
            Size = new Vector2(band?.W ?? FrameWidth, LocationHeight),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        location.AddThemeFontSizeOverride("font_size", LocationFontSize);
        location.AddThemeFontOverride("font", kit.Regular);
        location.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        location.AddThemeConstantOverride("shadow_offset_x", 1);
        location.AddThemeConstantOverride("shadow_offset_y", 1);
        view.AddChild(location);
        view.OnPressed("btn_battle_a", canvas.ToggleBlips);

        npcList = view.Get(NpcListGroup);
        npcChoice = view.Get(NpcRowChoice);
        for (int i = 0; i < NpcKinds; i++)
        {
            if (view.Get<Label>(string.Format(NpcRowFormat, i)) is not { } row) continue;
            int kind = i + 1;
            row.MouseFilter = MouseFilterEnum.Stop;
            row.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && game.Map.NpcKindPresent(kind))
                    game.Map.PickNpcKind(kind);
            };
            npcRows.Add(row);
            npcRowColours.Add(row.GetThemeColor("font_color"));
        }
        view.OnPressed(NpcListOpen, () => SetNpcListOpen(!npcListOpen));
        view.OnPressed(NpcListClose, () => SetNpcListOpen(false));
        SetNpcListOpen(false);
        BuildWarMap(kit);
        if (view.Get<TextureButton>(WantedToggle) is { } wanted)
        {
            wanted.ToggleMode = true;
            wanted.Toggled += _ => game.Map.ToggleWantedUi();
        }
        view.OnPressed(WantedOpen, () => game.Map.ShowWantedList());
        if (settings.GetBool(SettingCollapsed, false)) SetCollapsed(true);
        Refresh();
    }

    private void BuildWarMap(UiKit kit)
    {
        if (kit.Texture(FlagSheet) is { } sheet)
            for (int i = 0; i < FlagIcons.Length; i++)
                flagArt[i] = new AtlasTexture { Atlas = sheet, Region = FlagIcons[i], FilterClip = true };
        for (int i = 0; i < flags.Length; i++)
        {
            if (view.Get(string.Format(FlagAreaFormat, i)) is not { } spot) continue;
            var flag = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Size = spot.Size,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            spot.AddChild(flag);
            flags[i] = flag;
        }
        if (view.Get(WarMapImage) is { } image)
        {
            warArrow = new WarArrow(game) { Position = image.Position, Size = image.Size };
            image.GetParent().AddChild(warArrow);
        }
        view.OnPressed(WarMapButton, () =>
        {
            warMapShown = !warMapShown;
            RefreshWarMap();
        });
    }

    private void RefreshWarMap()
    {
        var map = game.Map;
        if (map.Zone != zone)
        {
            zone = map.Zone;
            warMapShown = true;
        }
        bool war = map.Zone == NereidMonuments.Zone && !collapsed;
        if (view.Get(WarMapGroup) is { } group) group.Visible = war && warMapShown;
        if (view.Get(WarScoreGroup) is { } scores) scores.Visible = war;
        if (view.Get(WarMapButton) is { } button) button.Visible = war;
        if (!war) return;
        var owners = map.MonumentOwners;
        for (int i = 0; i < flags.Length; i++)
        {
            if (flags[i] is not { } flag) continue;
            int owner = i < owners.Count ? owners[i] : NereidMonuments.NoOwner;
            flag.Texture = flagArt[owner < flagArt.Length ? owner : NereidMonuments.NoOwner];
        }
        warArrow?.QueueRedraw();
        view.SetText(ElMoradScore, NereidMonuments.Held(owners, Nations.ElMorad).ToString());
        view.SetText(KarusScore, NereidMonuments.Held(owners, Nations.Karus).ToString());
    }

    private void SetNpcListOpen(bool open)
    {
        npcListOpen = open;
        if (npcList != null) npcList.Visible = open && !collapsed;
        RefreshNpcRows();
    }

    private void RefreshNpcRows()
    {
        if (!npcListOpen) return;
        var map = game.Map;
        int kind = map.NpcKind;
        for (int i = 0; i < npcRows.Count; i++)
            npcRows[i].AddThemeColorOverride("font_color", map.NpcKindPresent(i + 1) ? npcRowColours[i] : AbsentKind);
        if (npcChoice == null) return;
        bool chosen = kind >= 1 && kind <= npcRows.Count;
        npcChoice.Visible = chosen;
        if (chosen)
        {
            var row = npcRows[kind - 1];
            npcChoice.Position = new Vector2(npcChoice.Position.X, row.Position.Y + (row.Size.Y - npcChoice.Size.Y) * 0.5f);
        }
    }

    public override void _EnterTree()
    {
        game.Map.Changed += Refresh;
        game.BecameAvailable += Refresh;
    }

    public override void _ExitTree()
    {
        game.Map.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
    }

    private void SetCollapsed(bool collapsed)
    {
        this.collapsed = collapsed;
        foreach (var child in layout.Children)
        {
            if (child.Id == StateBar) continue;
            if (view.ControlOf(child) is { } control) control.Visible = !collapsed;
        }
        canvas.Visible = !collapsed;
        if (!collapsed) view.Hide(HiddenIds);
        SetNpcListOpen(npcListOpen);
        RefreshWarMap();
    }

    private void Refresh()
    {
        var m = game.Map;
        string line = $"{m.ZoneName} {m.X:0}, {m.Z:0}";
        if (line != locationLine)
        {
            locationLine = line;
            location.Text = line;
        }
        RefreshNpcRows();
        RefreshWarMap();
        if (view.Get<TextureButton>(WantedToggle) is { } wanted) wanted.SetPressedNoSignal(m.WantedUiOn);
        canvas.QueueRedraw();
    }
}

public partial class WarArrow : Control
{
    private const float Arrow = 5f;
    private static readonly Color ArrowColor = new(0f, 1f, 0f);

    private readonly PluginGame game;

    public WarArrow(PluginGame game)
    {
        this.game = game;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var m = game.Map;
        if (m.WorldExtent <= 0f) return;
        var at = new Vector2(m.X / m.WorldExtent * Size.X, (1f - m.Z / m.WorldExtent) * Size.Y);
        float heading = Mathf.DegToRad(m.HeadingDegrees);
        var dir = new Vector2(Mathf.Sin(heading), -Mathf.Cos(heading));
        var side = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon(new[]
        {
            at + dir * Arrow,
            at - dir * Arrow * 0.7f + side * Arrow * 0.6f,
            at - dir * Arrow * 0.7f - side * Arrow * 0.6f,
        }, ArrowColor);
    }
}

public partial class MapCanvas : Control
{
    private static readonly float[] ZoomRadii = { 90f, 140f, 220f, 340f };
    private const int DefaultZoom = 1;
    private static readonly Color Background = new(0.03f, 0.035f, 0.045f);
    private static readonly Color PlayerColor = new(1f, 0.95f, 0.6f);
    private const float PlayerArrow = 6f;
    private const float EdgePin = 5f;
    private static readonly Color DiamondEdge = new(0f, 0f, 0f, 0.8f);

    private readonly PluginGame game;
    private int zoom = DefaultZoom;
    private bool showBlips = true;

    public MapCanvas(PluginGame game)
    {
        this.game = game;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void ToggleBlips()
    {
        showBlips = !showBlips;
        QueueRedraw();
    }

    public void Zoom(int dir)
    {
        zoom = Mathf.Clamp(zoom + dir, 0, ZoomRadii.Length - 1);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var m = game.Map;
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        float radius = ZoomRadii[zoom];
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
        foreach (var b in showBlips ? m.Blips : Array.Empty<MiniMap.Blip>())
        {
            var p = mid + new Vector2((b.X - m.X) * pxPerWorld, -(b.Z - m.Z) * pxPerWorld);
            if (b.Diamond)
            {
                DrawDiamond(new Vector2(Mathf.Clamp(p.X, EdgePin, Size.X - EdgePin), Mathf.Clamp(p.Y, EdgePin, Size.Y - EdgePin)),
                    b.Radius, b.Color);
                continue;
            }
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

    private void DrawDiamond(Vector2 at, float radius, Color color)
    {
        var points = new[]
        {
            at + new Vector2(0f, -radius), at + new Vector2(radius, 0f),
            at + new Vector2(0f, radius), at + new Vector2(-radius, 0f),
        };
        DrawColoredPolygon(points, color);
        DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, DiamondEdge, 1f, true);
    }
}
