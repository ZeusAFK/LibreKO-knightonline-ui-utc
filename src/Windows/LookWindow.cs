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

    private readonly LookLayout layout;
    private readonly IGameAppearance look;
    private readonly LayoutView view;
    private readonly TextureRect? preview;

    public LookWindow(WindowHost host, LookLayout layout, IGameAppearance look)
    {
        this.layout = layout;
        this.look = look;
        var kit = Plugin.Kit;
        view = new LayoutView(kit, kit.Layout(layout.Name));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, layout.TitleBarHeight)));

        if (view.Get(layout.PreviewArea) is { } area)
        {
            preview = new TextureRect
            {
                Size = area.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            area.AddChild(preview);
        }

        Step(layout.RacePrev, AppearancePart.Race, -1);
        Step(layout.RaceNext, AppearancePart.Race, 1);
        Step(layout.FacePrev, AppearancePart.Face, -1);
        Step(layout.FaceNext, AppearancePart.Face, 1);
        Step(layout.HairPrev, AppearancePart.Hair, -1);
        Step(layout.HairNext, AppearancePart.Hair, 1);
        view.OnPressed(LeftTurn, () => this.look.Turn(-TurnDegrees));
        view.OnPressed(RightTurn, () => this.look.Turn(TurnDegrees));
        view.OnPressed(Zoom, this.look.ToggleZoom);
        view.OnPressed(HairColour, this.look.OpenHairColour);
        view.OnPressed(layout.Ok, this.look.Confirm);
        view.OnPressed(layout.Cancel, this.look.Cancel);
        if (layout.Close != null) view.OnPressed(layout.Close, this.look.Cancel);
        if (layout.CaptionHairColour)
            view.Recaption(HairColour, ItemData.Text(HairColourText, "Change hair color"), CaptionSize, CaptionColour);
        else if (view.Get(HairColour) is { } colourButton)
            colourButton.TooltipText = ItemData.Text(HairColourText, "Change hair color");
        foreach (var (id, text) in layout.Labels) view.SetText(id, text);
        view.Recaption(layout.Ok, ConfirmCaption, CaptionSize, CaptionColour);
        view.Recaption(layout.Cancel, CancelCaption, CaptionSize, CaptionColour);
        for (int i = 0; i < GenderInfoTexts.Length; i++)
            if (view.Get<Label>(GenderInfoTexts[i]) is { } info)
            {
                info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                info.Text = ItemData.Text(GenderInfoTextIds[i], "");
            }
    }

    private void Step(string? button, AppearancePart part, int delta)
    {
        if (button != null) view.OnPressed(button, () => look.Step(part, delta));
    }

    public override void _EnterTree()
    {
        look.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => look.Changed -= Refresh;

    private void Refresh()
    {
        if (preview != null) preview.Texture = look.Preview;
        if (layout.RaceLabel != null) view.SetText(layout.RaceLabel, look.RaceName);
        view.SetText(layout.FaceLabel, $"Face {look.Face + 1}");
        view.SetText(layout.HairLabel, $"Hair {look.HairStyle + 1}");
        if (layout.NameLabel != null) view.SetText(layout.NameLabel, look.CharacterName);
        if (view.Get<BaseButton>(layout.Ok) is { } ok) ok.Disabled = look.Busy;
        var choices = look.RaceChoices.OrderByDescending(c => c.Current).ToList();
        for (int i = 0; i < ChoiceRows.Length; i++)
        {
            var (race, cls) = ChoiceRows[i];
            var choice = i < choices.Count ? choices[i] : default;
            view.SetText(race, choice.Race ?? "");
            view.SetText(cls, choice.Class ?? "");
        }
    }
}
