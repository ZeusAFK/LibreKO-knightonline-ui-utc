using System.Linq;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class Taskbar : Control
{
    private const string LayoutName = "re_taskbar_main";
    private const string Backboard = "img_backboard";
    private const string MenuGroup = "base_menu";
    private const string StateGroup = "base_state01";
    private const string ButtonsGroup = "base_TaskBar";
    private const float GroupGap = 8f;
    private const float RightMargin = 4f;
    private const string TradeButton = "btn_03";
    private const string MenuButton = "btn_menu";
    private const string EventsWindow = "events";
    private const string UiMenu = "base_uimenu";
    private const string CompactExp = "base_Exp";
    private const string StateOption = "btn_option1";
    private const string CompactOption = "btn_option2";
    private const string AlphaBar = "ui_alpha";
    private const string SettingIconsHidden = "taskbar.iconsHidden";
    private const string SettingBarHidden = "taskbar.barHidden";
    private const int TextShowIcon = 30820;
    private const int TextHideIcon = 30821;
    private const int TextShowBar = 30822;
    private const int TextHideBar = 30823;
    private const int AlphaStep = 5;

    private static readonly (string Button, string Window)[] WindowButtons =
    {
        ("btn_02", "seek_party"),
        ("btn_04", "skills"),
        ("btn_05", "character_info"),
        ("btn_06", "inventory"),
        ("btn_seed_elmo", "quests"),
        ("btn_seed_karu", "quests"),
        ("btn_question", "quests"),
        ("btn_powerup", "shoppingmall"),
        ("btn_clanadvence", "clan"),
        ("btn_rank", "rank"),
    };

    private readonly LayoutView view;
    private readonly PluginGame game;
    private readonly LayoutNode layout;
    private TaskbarMenu? menu;
    private readonly PluginSettings settings;
    private bool iconsHidden;
    private bool barHidden;
    private Control? alphaTrack;
    private Control? alphaThumb;

    public Taskbar()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        settings = kit.Context.Settings;
        layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(view);
        view.Hide("base_state00", UiMenu, CompactOption, StateOption, CompactExp, "btn_01",
            "btn_seed_karu", "btn_question", "progress_Rexp", "progress_Sexp");
        foreach (var (button, window) in WindowButtons)
            view.OnPressed(button, () => game.Windows.Toggle(window));
        view.OnPressed("btn_07", () => game.Commands.GoTown());
        view.OnPressed(TradeButton, () => game.Commands.TradeWithTarget());
        view.OnPressed("btn_00", () => game.Commands.ToggleSit());
        foreach (var option in new[] { "btn_option", StateOption, CompactOption })
            view.OnPressed(option, ToggleUiMenu);
        view.OnPressed("btn_close", () => ShowUiMenu(false));
        view.OnPressed("btn_change", () => SetIconsHidden(!iconsHidden));
        view.OnPressed("btn_uishow", () => SetBarHidden(!barHidden));
        BuildAlphaBar();
        view.OnPressed("btn_event", () => game.Windows.Toggle(EventsWindow));
        if (view.Get(MenuButton) is { } menuButton)
        {
            menu = new TaskbarMenu(menuButton);
            AddChild(menu);
            view.OnPressed(MenuButton, menu.Toggle);
        }
        view.SetTextAll("str_usernotice", "");
        SetIconsHidden(settings.GetBool(SettingIconsHidden, false));
        SetBarHidden(settings.GetBool(SettingBarHidden, false));
    }

    private void ToggleUiMenu() => ShowUiMenu(view.Get(UiMenu) is { Visible: false });

    private void ShowUiMenu(bool shown)
    {
        if (view.Get(UiMenu) is not { } uiMenu) return;
        uiMenu.Visible = shown;
        if (shown) RefreshAlphaBar();
    }

    private void SetIconsHidden(bool hidden)
    {
        iconsHidden = hidden;
        settings.SetBool(SettingIconsHidden, hidden);
        if (view.Get(ButtonsGroup) is { } buttons) buttons.Visible = !hidden;
        if (view.Get(StateOption) is { } option) option.Visible = hidden && !barHidden;
        if (view.Get("btn_change") is { } change)
            change.TooltipText = ItemData.Text(hidden ? TextShowIcon : TextHideIcon, hidden ? "Show Icon" : "Hide Icon");
    }

    private void SetBarHidden(bool hidden)
    {
        barHidden = hidden;
        settings.SetBool(SettingBarHidden, hidden);
        foreach (var id in new[] { Backboard, MenuGroup, StateGroup })
            if (view.Get(id) is { } part) part.Visible = !hidden;
        if (view.Get(CompactExp) is { } exp) exp.Visible = hidden;
        if (view.Get(CompactOption) is { } option) option.Visible = hidden;
        if (view.Get(StateOption) is { } stateOption) stateOption.Visible = iconsHidden && !hidden;
        if (view.Get("btn_uishow") is { } show)
            show.TooltipText = ItemData.Text(hidden ? TextShowBar : TextHideBar, hidden ? "Show Below Bar" : "Hide Below Bar");
        if (IsInsideTree()) Place();
    }

    private void BuildAlphaBar()
    {
        if (layout.Find(AlphaBar) is not { } bar) return;
        var arrows = bar.Children.Where(c => c.Type == "button").ToList();
        if (arrows.Count > 0 && view.ControlOf(arrows[0]) is BaseButton down) down.Pressed += () => StepAlpha(-AlphaStep);
        if (arrows.Count > 1 && view.ControlOf(arrows[^1]) is BaseButton up) up.Pressed += () => StepAlpha(AlphaStep);
        if (bar.Children.FirstOrDefault(c => c.Type == "trackbar") is not { } track || view.ControlOf(track) is not { } trackControl) return;
        alphaTrack = trackControl;
        alphaThumb = track.Children.Count > 1 ? view.ControlOf(track.Children[^1]) : null;
        trackControl.MouseFilter = MouseFilterEnum.Stop;
        trackControl.GuiInput += ev =>
        {
            bool press = ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
            bool drag = ev is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Left) != 0;
            if (!press && !drag) return;
            float thumb = alphaThumb?.Size.X ?? 0f;
            float span = Mathf.Max(1f, trackControl.Size.X - thumb);
            float fraction = Mathf.Clamp((trackControl.GetLocalMousePosition().X - thumb * 0.5f) / span, 0f, 1f);
            game.Windows.SetUiAlpha(UiOpacityRules.Lowest + Mathf.RoundToInt(fraction * (UiOpacityRules.Opaque - UiOpacityRules.Lowest)));
            RefreshAlphaBar();
            trackControl.AcceptEvent();
        };
    }

    private void StepAlpha(int step)
    {
        game.Windows.SetUiAlpha(game.Windows.UiAlpha + step);
        RefreshAlphaBar();
    }

    private void RefreshAlphaBar()
    {
        int alpha = game.Windows.UiAlpha;
        if (alphaTrack != null) alphaTrack.TooltipText = alpha.ToString();
        if (alphaTrack == null || alphaThumb == null) return;
        float span = Mathf.Max(0f, alphaTrack.Size.X - alphaThumb.Size.X);
        float fraction = (alpha - UiOpacityRules.Lowest) / (float)(UiOpacityRules.Opaque - UiOpacityRules.Lowest);
        alphaThumb.Position = new Vector2(span * fraction, alphaThumb.Position.Y);
    }

    public override void _EnterTree()
    {
        game.Character.Changed += RefreshExp;
        game.BecameAvailable += RefreshExp;
        GetViewport().SizeChanged += Place;
        Place();
        RefreshExp();
    }

    public override void _ExitTree()
    {
        game.Character.Changed -= RefreshExp;
        game.BecameAvailable -= RefreshExp;
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        var room = GetViewport().GetVisibleRect().Size;
        view.Position = new Vector2(0, room.Y - layout.H);
        if (view.Get(Backboard) is { } board)
            board.Size = new Vector2(room.X, board.Size.Y);

        float buttonsLeft = room.X;
        if (layout.Find(ButtonsGroup) is { } buttons && view.Get(ButtonsGroup) is { } buttonsControl)
        {
            buttonsLeft = room.X - buttons.W - RightMargin;
            buttonsControl.Position = new Vector2(buttonsLeft, buttons.Y - layout.Y);
        }

        if (layout.Find(UiMenu) is { } uiMenu && view.Get(UiMenu) is { } uiMenuControl)
            uiMenuControl.Position = new Vector2(buttonsLeft + (layout.Find(ButtonsGroup)?.W ?? 0) - uiMenu.W, uiMenu.Y - layout.Y);
        if (layout.Find(CompactOption) is { } compactOption && view.Get(CompactOption) is { } compactControl)
            compactControl.Position = new Vector2(room.X - compactOption.W - RightMargin, compactOption.Y - layout.Y);
        if (layout.Find(CompactExp) is { } compactExp && view.Get(CompactExp) is { } compactExpControl)
            compactExpControl.Position = new Vector2((room.X - compactExp.W) * 0.5f, compactExp.Y - layout.Y);

        if (layout.Find(StateGroup) is { } state && view.Get(StateGroup) is { } stateControl)
        {
            float left = state.X - layout.X;
            float width = Mathf.Max(state.W * 0.25f, buttonsLeft - GroupGap - left);
            stateControl.Position = new Vector2(left, state.Y - layout.Y);
            stateControl.Size = new Vector2(width, state.H);
            foreach (var child in state.Children)
            {
                if (view.ControlOf(child) is not { } c) continue;
                float relX = child.X - state.X;
                float centre = relX + child.W * 0.5f;
                if (centre < state.W / 3f) continue;
                if (centre > state.W * 2f / 3f)
                {
                    c.Position = new Vector2(width - (state.W - relX), c.Position.Y);
                    continue;
                }
                c.Position = new Vector2(relX, c.Position.Y);
                c.Size = new Vector2(width - (state.W - child.W), child.H);
            }
        }
    }

    private void RefreshExp()
    {
        var c = game.Character;
        float fraction = Mathf.Clamp((float)(c.ExpPercent / 100.0), 0f, 1f);
        foreach (var (node, control) in view.Where(n => n.Id == "progress_exp"))
            if (control is TextureProgressBar bar) bar.Value = fraction;
        view.SetTextAll("str_exp", $"EXP:{c.ExpPercent:0.00}%");
    }
}
