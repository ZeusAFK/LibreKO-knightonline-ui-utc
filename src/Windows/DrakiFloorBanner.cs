using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class DrakiFloorBanner : Control
{
    private const string LayoutName = "ui_drakyfloor";
    private const string RightHalfId = "img_l";
    private const string LeftHalfId = "img_r";
    private const string FloorBackId = "img_back1";
    private const string NameBackId = "img_back2";
    private const string FloorId = "str_floor";
    private const string NameId = "str_Name";
    private const float FontShareOfBand = 0.1f;
    private const float FloorTopInBand = 0.5f;
    private const float NameTopInBand = 2f / 3f;
    private const float TextHalfWidthShare = 2f / 3f;
    private const int FloorLines = 3;

    private readonly PluginGame game;
    private readonly Control? rightHalf;
    private readonly Control? leftHalf;
    private readonly Control? floorBack;
    private readonly Control? nameBack;
    private readonly Label? floor;
    private readonly Label? floorName;
    private Tween? tween;

    public DrakiFloorBanner()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        AddChild(view);
        rightHalf = view.Get(RightHalfId);
        leftHalf = view.Get(LeftHalfId);
        floorBack = view.Get(FloorBackId);
        nameBack = view.Get(NameBackId);
        floor = view.Get<Label>(FloorId);
        floorName = view.Get<Label>(NameId);
        foreach (var label in new[] { floor, floorName })
        {
            if (label == null) continue;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    public override void _EnterTree() => game.Draki.FloorReached += OnFloor;

    public override void _ExitTree()
    {
        game.Draki.FloorReached -= OnFloor;
        tween?.Kill();
    }

    private void OnFloor(int stage, int subStage)
    {
        var texts = DrakiTower.FloorTexts(stage, subStage);
        if (floor != null) floor.Text = texts.Floor;
        if (floorName != null) floorName.Text = texts.Name;
        Arrange();
        tween?.Kill();
        Modulate = new Color(1f, 1f, 1f, 0f);
        Visible = true;
        tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 1f, DrakiTower.FloorFadeSeconds);
        tween.TweenInterval(DrakiTower.FloorHoldSeconds);
        tween.TweenProperty(this, "modulate:a", 0f, DrakiTower.FloorFadeSeconds);
        tween.TweenCallback(Callable.From(() => Visible = false));
    }

    private void Arrange()
    {
        var screen = GetViewport().GetVisibleRect().Size;
        float bandHeight = screen.Y * DrakiTower.FloorBandShare;
        float top = screen.Y - bandHeight;
        float centre = screen.X * 0.5f;
        Place(leftHalf, new Rect2(0, top, centre, bandHeight));
        Place(rightHalf, new Rect2(centre, top, centre, bandHeight));

        int fontSize = Mathf.Max(1, (int)(bandHeight * FontShareOfBand));
        var floorRect = TextRect(floor, fontSize, centre, top + bandHeight * FloorTopInBand, fontSize * FloorLines);
        var nameRect = TextRect(floorName, fontSize, centre, top + bandHeight * NameTopInBand, screen.Y - (top + bandHeight * NameTopInBand));
        Place(floor, floorRect);
        Place(floorBack, floorRect);
        Place(floorName, nameRect);
        Place(nameBack, nameRect);
    }

    private static Rect2 TextRect(Label? label, int fontSize, float centre, float top, float height)
    {
        if (label == null) return default;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        float width = label.GetThemeFont("font").GetStringSize(label.Text, fontSize: fontSize).X;
        float halfWidth = width * TextHalfWidthShare;
        return new Rect2(centre - halfWidth, top, halfWidth * 2f, height);
    }

    private static void Place(Control? control, Rect2 rect)
    {
        if (control == null) return;
        control.Position = rect.Position;
        control.Size = rect.Size;
    }
}
