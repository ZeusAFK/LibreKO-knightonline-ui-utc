using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class DrakiPlate : Control
{
    private const string LayoutName = "re_draky_chapter";
    private const string LayoutId = "theme_draki_plate";
    private const string LargeForm = "base_L";
    private const string SmallForm = "base_s";
    private const string MoveHandle = "img_move";
    private const string ToSmall = "Btn_OnS";
    private const string ToLarge = "btn_OnL";
    private const string ChapterId = "str_Chapter";
    private const string LimitId = "str_Time";
    private const string ElapsedId = "str_Total";
    private const string TownTip = "Right-click to go back to town";
    private const float DefaultTop = 110f;

    private readonly record struct FormLines(Label? Chapter, Label? Limit, Label? Elapsed);

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly Control? largeForm;
    private readonly Control? smallForm;
    private readonly Control? handle;
    private readonly FormLines largeLines;
    private readonly FormLines smallLines;
    private HudLayout? hudLayout;
    private DrakiPlateLines shownLines;
    private bool large = true;

    public DrakiPlate()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        Visible = false;
        AddChild(view);

        largeLines = Lines(view, layout.Find(LargeForm));
        smallLines = Lines(view, layout.Find(SmallForm));
        largeForm = view.Get(LargeForm);
        smallForm = view.Get(SmallForm);
        foreach (var form in new[] { largeForm, smallForm })
        {
            if (form == null) continue;
            form.MouseFilter = MouseFilterEnum.Pass;
            form.TooltipText = TownTip;
            form.GuiInput += OnFormInput;
        }
        handle = view.Get(MoveHandle);
        if (handle != null)
        {
            handle.MouseFilter = MouseFilterEnum.Stop;
            handle.MouseDefaultCursorShape = CursorShape.Move;
        }
        view.OnPressed(ToSmall, () => SetForm(false));
        view.OnPressed(ToLarge, () => SetForm(true));
        SetForm(true);
    }

    private static FormLines Lines(LayoutView view, LayoutNode? form) => form == null
        ? default
        : new(view.Get<Label>(form, ChapterId), view.Get<Label>(form, LimitId), view.Get<Label>(form, ElapsedId));

    public override void _EnterTree() =>
        hudLayout ??= HudLayout.Attach(this, LayoutId, handle ?? view, DefaultPosition);

    public override void _Process(double delta)
    {
        bool shown = game.Draki.PlateShown;
        if (Visible != shown) Visible = shown;
        if (shown) Fill();
    }

    private Vector2 DefaultPosition() =>
        new((GetViewport().GetVisibleRect().Size.X - Size.X) * 0.5f, DefaultTop);

    private void SetForm(bool large)
    {
        this.large = large;
        if (largeForm != null) largeForm.Visible = large;
        if (smallForm != null) smallForm.Visible = !large;
        shownLines = default;
        if (Visible) Fill();
    }

    private void Fill()
    {
        var lines = DrakiTower.PlateLines(game.Draki.Plate, large);
        if (lines == shownLines) return;
        shownLines = lines;
        var form = large ? largeLines : smallLines;
        if (form.Chapter != null) form.Chapter.Text = lines.Chapter;
        if (form.Limit != null) form.Limit.Text = lines.Limit;
        if (form.Elapsed != null) form.Elapsed.Text = lines.Elapsed;
    }

    private void OnFormInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }) return;
        AcceptEvent();
        game.Draki.AskTown();
    }
}
