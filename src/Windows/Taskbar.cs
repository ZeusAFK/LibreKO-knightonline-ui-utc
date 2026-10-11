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

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly LayoutNode _layout;
    private TaskbarMenu? _menu;
    private readonly PluginSettings _settings;
    private bool _iconsHidden;
    private bool _barHidden;
    private Control? _alphaTrack;
    private Control? _alphaThumb;

    public Taskbar()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _settings = kit.Context.Settings;
        _layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, _layout);
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_view);
        _view.Hide("base_state00", UiMenu, CompactOption, StateOption, CompactExp, "btn_01",
            "btn_seed_karu", "btn_question", "progress_Rexp", "progress_Sexp");
        foreach (var (button, window) in WindowButtons)
            _view.OnPressed(button, () => _game.Windows.Toggle(window));
        _view.OnPressed("btn_07", () => _game.Commands.GoTown());
        _view.OnPressed(TradeButton, () => _game.Commands.TradeWithTarget());
        _view.OnPressed("btn_00", () => _game.Commands.ToggleSit());
        foreach (var option in new[] { "btn_option", StateOption, CompactOption })
            _view.OnPressed(option, ToggleUiMenu);
        _view.OnPressed("btn_close", () => ShowUiMenu(false));
        _view.OnPressed("btn_change", () => SetIconsHidden(!_iconsHidden));
        _view.OnPressed("btn_uishow", () => SetBarHidden(!_barHidden));
        BuildAlphaBar();
        _view.OnPressed("btn_event", () => _game.Windows.Toggle(EventsWindow));
        if (_view.Get(MenuButton) is { } menuButton)
        {
            _menu = new TaskbarMenu(menuButton);
            AddChild(_menu);
            _view.OnPressed(MenuButton, _menu.Toggle);
        }
        _view.SetTextAll("str_usernotice", "");
        SetIconsHidden(_settings.GetBool(SettingIconsHidden, false));
        SetBarHidden(_settings.GetBool(SettingBarHidden, false));
    }

    private void ToggleUiMenu() => ShowUiMenu(_view.Get(UiMenu) is { Visible: false });

    private void ShowUiMenu(bool shown)
    {
        if (_view.Get(UiMenu) is not { } menu) return;
        menu.Visible = shown;
        if (shown) RefreshAlphaBar();
    }

    private void SetIconsHidden(bool hidden)
    {
        _iconsHidden = hidden;
        _settings.SetBool(SettingIconsHidden, hidden);
        if (_view.Get(ButtonsGroup) is { } buttons) buttons.Visible = !hidden;
        if (_view.Get(StateOption) is { } option) option.Visible = hidden && !_barHidden;
        if (_view.Get("btn_change") is { } change)
            change.TooltipText = ItemData.Text(hidden ? TextShowIcon : TextHideIcon, hidden ? "Show Icon" : "Hide Icon");
    }

    private void SetBarHidden(bool hidden)
    {
        _barHidden = hidden;
        _settings.SetBool(SettingBarHidden, hidden);
        foreach (var id in new[] { Backboard, MenuGroup, StateGroup })
            if (_view.Get(id) is { } part) part.Visible = !hidden;
        if (_view.Get(CompactExp) is { } exp) exp.Visible = hidden;
        if (_view.Get(CompactOption) is { } option) option.Visible = hidden;
        if (_view.Get(StateOption) is { } stateOption) stateOption.Visible = _iconsHidden && !hidden;
        if (_view.Get("btn_uishow") is { } show)
            show.TooltipText = ItemData.Text(hidden ? TextShowBar : TextHideBar, hidden ? "Show Below Bar" : "Hide Below Bar");
        if (IsInsideTree()) Place();
    }

    private void BuildAlphaBar()
    {
        if (_layout.Find(AlphaBar) is not { } bar) return;
        var arrows = bar.Children.Where(c => c.Type == "button").ToList();
        if (arrows.Count > 0 && _view.ControlOf(arrows[0]) is BaseButton down) down.Pressed += () => StepAlpha(-AlphaStep);
        if (arrows.Count > 1 && _view.ControlOf(arrows[^1]) is BaseButton up) up.Pressed += () => StepAlpha(AlphaStep);
        if (bar.Children.FirstOrDefault(c => c.Type == "trackbar") is not { } track || _view.ControlOf(track) is not { } trackControl) return;
        _alphaTrack = trackControl;
        _alphaThumb = track.Children.Count > 1 ? _view.ControlOf(track.Children[^1]) : null;
        trackControl.MouseFilter = MouseFilterEnum.Stop;
        trackControl.GuiInput += ev =>
        {
            bool press = ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
            bool drag = ev is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Left) != 0;
            if (!press && !drag) return;
            float thumb = _alphaThumb?.Size.X ?? 0f;
            float span = Mathf.Max(1f, trackControl.Size.X - thumb);
            float fraction = Mathf.Clamp((trackControl.GetLocalMousePosition().X - thumb * 0.5f) / span, 0f, 1f);
            _game.Windows.SetUiAlpha(UiOpacityRules.Lowest + Mathf.RoundToInt(fraction * (UiOpacityRules.Opaque - UiOpacityRules.Lowest)));
            RefreshAlphaBar();
            trackControl.AcceptEvent();
        };
    }

    private void StepAlpha(int step)
    {
        _game.Windows.SetUiAlpha(_game.Windows.UiAlpha + step);
        RefreshAlphaBar();
    }

    private void RefreshAlphaBar()
    {
        int alpha = _game.Windows.UiAlpha;
        if (_alphaTrack != null) _alphaTrack.TooltipText = alpha.ToString();
        if (_alphaTrack == null || _alphaThumb == null) return;
        float span = Mathf.Max(0f, _alphaTrack.Size.X - _alphaThumb.Size.X);
        float fraction = (alpha - UiOpacityRules.Lowest) / (float)(UiOpacityRules.Opaque - UiOpacityRules.Lowest);
        _alphaThumb.Position = new Vector2(span * fraction, _alphaThumb.Position.Y);
    }

    public override void _EnterTree()
    {
        _game.Character.Changed += RefreshExp;
        _game.BecameAvailable += RefreshExp;
        GetViewport().SizeChanged += Place;
        Place();
        RefreshExp();
    }

    public override void _ExitTree()
    {
        _game.Character.Changed -= RefreshExp;
        _game.BecameAvailable -= RefreshExp;
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        var room = GetViewport().GetVisibleRect().Size;
        _view.Position = new Vector2(0, room.Y - _layout.H);
        if (_view.Get(Backboard) is { } board)
            board.Size = new Vector2(room.X, board.Size.Y);

        float buttonsLeft = room.X;
        if (_layout.Find(ButtonsGroup) is { } buttons && _view.Get(ButtonsGroup) is { } buttonsControl)
        {
            buttonsLeft = room.X - buttons.W - RightMargin;
            buttonsControl.Position = new Vector2(buttonsLeft, buttons.Y - _layout.Y);
        }

        if (_layout.Find(UiMenu) is { } uiMenu && _view.Get(UiMenu) is { } uiMenuControl)
            uiMenuControl.Position = new Vector2(buttonsLeft + (_layout.Find(ButtonsGroup)?.W ?? 0) - uiMenu.W, uiMenu.Y - _layout.Y);
        if (_layout.Find(CompactOption) is { } compactOption && _view.Get(CompactOption) is { } compactControl)
            compactControl.Position = new Vector2(room.X - compactOption.W - RightMargin, compactOption.Y - _layout.Y);
        if (_layout.Find(CompactExp) is { } compactExp && _view.Get(CompactExp) is { } compactExpControl)
            compactExpControl.Position = new Vector2((room.X - compactExp.W) * 0.5f, compactExp.Y - _layout.Y);

        if (_layout.Find(StateGroup) is { } state && _view.Get(StateGroup) is { } stateControl)
        {
            float left = state.X - _layout.X;
            float width = Mathf.Max(state.W * 0.25f, buttonsLeft - GroupGap - left);
            stateControl.Position = new Vector2(left, state.Y - _layout.Y);
            stateControl.Size = new Vector2(width, state.H);
            foreach (var child in state.Children)
            {
                if (_view.ControlOf(child) is not { } c) continue;
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
        var c = _game.Character;
        float fraction = Mathf.Clamp((float)(c.ExpPercent / 100.0), 0f, 1f);
        foreach (var (node, control) in _view.Where(n => n.Id == "progress_exp"))
            if (control is TextureProgressBar bar) bar.Value = fraction;
        _view.SetTextAll("str_exp", $"EXP:{c.ExpPercent:0.00}%");
    }
}
