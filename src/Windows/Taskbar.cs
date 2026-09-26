using Godot;
using KnightOnlineUi.Layout;
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
        ("btn_event", "eventquests"),
        ("btn_clanadvence", "clan"),
        ("btn_rank", "rank"),
    };

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly LayoutNode _layout;

    public Taskbar()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, _layout);
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_view);
        _view.Hide("base_state00", "base_uimenu", "btn_option2", "btn_option1", "base_Exp", "btn_01",
            "btn_seed_karu", "btn_question", "progress_Rexp", "progress_Sexp");
        foreach (var (button, window) in WindowButtons)
            _view.OnPressed(button, () => _game.Windows.Toggle(window));
        _view.OnPressed("btn_07", () => _game.Commands.GoTown());
        _view.OnPressed(TradeButton, () => _game.Commands.TradeWithTarget());
        _view.OnPressed("btn_00", () => _game.Commands.ToggleSit());
        _view.OnPressed("btn_option", () => LibreKO.SettingsPanel.Open(GetTree().Root));
        _view.SetTextAll("str_usernotice", "");
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
