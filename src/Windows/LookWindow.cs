using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public sealed record LookLayout(
    string Name,
    string PreviewArea,
    string? RacePrev, string? RaceNext, string? RaceLabel,
    string FacePrev, string FaceNext, string FaceLabel,
    string HairPrev, string HairNext, string HairLabel,
    string Ok, string Cancel, string? Close,
    string? NameLabel,
    int TitleBarHeight,
    bool CaptionHairColour,
    (string Id, string Text)[] Labels);

public partial class LookWindow : Control
{
    public static readonly LookLayout BeautyShop = new(
        "re_hair", "area_face",
        null, null, null,
        "btn_faceprev", "btn_facenext", "str_face",
        "btn_hairprev", "btn_hairnext", "str_hair",
        "btn_ok", "btn_cancle", null, null, 24, true, System.Array.Empty<(string, string)>());

    public static readonly LookLayout GenderChange = new(
        "re_change_race", "area0",
        "btn_race_prev", "btn_race_next", "str_race",
        "btn_face_prev", "btn_face_next", "str_face",
        "btn_hair_prev", "btn_hair_next", "str_hair",
        "btn_ok", "btn_cancel", "btn_close", "str_ID", 60, false,
        new[] { ("uitext_0", "Gender Change"), ("uitext_3", "Current"), ("uitext_4", "Change to") });

    private const string LeftTurn = "btn_leftrotation";
    private const string RightTurn = "btn_rightrotation";
    private const string Zoom = "btn_zoom";
    private const string HairColour = "btn_haircolor";
    private const float TurnDegrees = 30f;
    private const int CaptionSize = 12;
    private const int HairColourText = 200025;
    private const string ConfirmCaption = "Confirm";
    private const string CancelCaption = "Cancel";
    private static readonly Color CaptionColour = new(0.95f, 0.88f, 0.62f);
    private static readonly string[] GenderInfoTexts = { "uitext_1", "uitext_2" };
    private static readonly int[] GenderInfoTextIds = { 40027, 40028 };
    private static readonly (string Race, string Class)[] ChoiceRows =
    {
        ("str_cur_race", "str_cur_class"), ("str_change0_race", "str_change0_class"), ("str_change1_race", "str_change1_class"),
    };

    private readonly LookLayout _layout;
    private readonly IGameAppearance _look;
    private readonly LayoutView _view;
    private readonly TextureRect? _preview;

    public LookWindow(WindowHost host, LookLayout layout, IGameAppearance look)
    {
        _layout = layout;
        _look = look;
        var kit = Plugin.Kit;
        _view = new LayoutView(kit, kit.Layout(layout.Name));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, layout.TitleBarHeight)));

        if (_view.Get(layout.PreviewArea) is { } area)
        {
            _preview = new TextureRect
            {
                Size = area.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            area.AddChild(_preview);
        }

        Step(layout.RacePrev, AppearancePart.Race, -1);
        Step(layout.RaceNext, AppearancePart.Race, 1);
        Step(layout.FacePrev, AppearancePart.Face, -1);
        Step(layout.FaceNext, AppearancePart.Face, 1);
        Step(layout.HairPrev, AppearancePart.Hair, -1);
        Step(layout.HairNext, AppearancePart.Hair, 1);
        _view.OnPressed(LeftTurn, () => _look.Turn(-TurnDegrees));
        _view.OnPressed(RightTurn, () => _look.Turn(TurnDegrees));
        _view.OnPressed(Zoom, _look.ToggleZoom);
        _view.OnPressed(HairColour, _look.OpenHairColour);
        _view.OnPressed(layout.Ok, _look.Confirm);
        _view.OnPressed(layout.Cancel, _look.Cancel);
        if (layout.Close != null) _view.OnPressed(layout.Close, _look.Cancel);
        if (layout.CaptionHairColour)
            _view.Recaption(HairColour, ItemData.Text(HairColourText, "Change hair color"), CaptionSize, CaptionColour);
        else if (_view.Get(HairColour) is { } colourButton)
            colourButton.TooltipText = ItemData.Text(HairColourText, "Change hair color");
        foreach (var (id, text) in layout.Labels) _view.SetText(id, text);
        _view.Recaption(layout.Ok, ConfirmCaption, CaptionSize, CaptionColour);
        _view.Recaption(layout.Cancel, CancelCaption, CaptionSize, CaptionColour);
        for (int i = 0; i < GenderInfoTexts.Length; i++)
            if (_view.Get<Label>(GenderInfoTexts[i]) is { } info)
            {
                info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                info.Text = ItemData.Text(GenderInfoTextIds[i], "");
            }
    }

    private void Step(string? button, AppearancePart part, int delta)
    {
        if (button != null) _view.OnPressed(button, () => _look.Step(part, delta));
    }

    public override void _EnterTree()
    {
        _look.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _look.Changed -= Refresh;

    private void Refresh()
    {
        if (_preview != null) _preview.Texture = _look.Preview;
        if (_layout.RaceLabel != null) _view.SetText(_layout.RaceLabel, _look.RaceName);
        _view.SetText(_layout.FaceLabel, $"Face {_look.Face + 1}");
        _view.SetText(_layout.HairLabel, $"Hair {_look.HairStyle + 1}");
        if (_layout.NameLabel != null) _view.SetText(_layout.NameLabel, _look.CharacterName);
        if (_view.Get<BaseButton>(_layout.Ok) is { } ok) ok.Disabled = _look.Busy;
        var choices = _look.RaceChoices.OrderByDescending(c => c.Current).ToList();
        for (int i = 0; i < ChoiceRows.Length; i++)
        {
            var (race, cls) = ChoiceRows[i];
            var choice = i < choices.Count ? choices[i] : default;
            _view.SetText(race, choice.Race ?? "");
            _view.SetText(cls, choice.Class ?? "");
        }
    }
}
