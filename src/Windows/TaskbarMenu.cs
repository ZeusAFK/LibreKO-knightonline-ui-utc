using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class TaskbarMenu : Control
{
    private const string LayoutName = "re_taskbar_sub";
    private const string KarusQuests = "btn_seed_karu";
    private const string ElMoradQuests = "btn_seed_elmo";
    private const int KarusNation = 1;
    private const string UnavailableTip = "Not available";
    private static readonly string[] Unavailable = { "btn_hotkey", "btn_movie_record" };

    private static readonly (string Button, KeyAction Action)[] ActionRows =
    {
        (KarusQuests, KeyAction.Quests),
        (ElMoradQuests, KeyAction.Quests),
        ("btn_powerup", KeyAction.PowerUpStore),
        ("btn_mail", KeyAction.Mail),
        ("btn_chat_creat", KeyAction.ChatRooms),
        ("btn_inven", KeyAction.Inventory),
        ("btn_userinfo", KeyAction.Character),
        ("btn_skill", KeyAction.Skills),
        ("btn_globalmap", KeyAction.WorldMap),
        ("btn_minimap", KeyAction.MiniMap),
        ("btn_merchant", KeyAction.MyShop),
        ("btn_achieve", KeyAction.Achievements),
    };

    private static readonly (string Button, string Window)[] WindowRows =
    {
        ("btn_clan", "clan"),
        ("btn_partywanted", "seek_party"),
    };

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly Control _opener;

    public TaskbarMenu(Control opener)
    {
        _opener = opener;
        var kit = Plugin.Kit;
        _game = kit.Game;
        _view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Ignore;
        TopLevel = true;
        Size = _view.Size;
        AddChild(_view);
        Visible = false;

        foreach (var (button, action) in ActionRows)
            _view.OnPressed(button, () => Run(() => _game.Commands.Run(action)));
        foreach (var (button, window) in WindowRows)
            _view.OnPressed(button, () => Run(() => _game.Windows.Toggle(window)));
        _view.OnPressed("btn_trade", () => Run(_game.Commands.TradeWithTarget));
        _view.OnPressed("btn_party", () => Run(_game.Commands.InviteTarget));
        _view.OnPressed("btn_option", () => Run(() => SettingsPanel.Open(GetTree().Root)));
        _view.OnPressed("btn_exit", () => Run(_game.Commands.ShowExitMenu));
        foreach (var id in Unavailable)
            if (_view.Get<BaseButton>(id) is { } off)
            {
                off.Disabled = true;
                off.TooltipText = UnavailableTip;
            }
    }

    public void Toggle()
    {
        if (Visible) { Visible = false; return; }
        bool karus = _game.Character.Nation == KarusNation;
        if (_view.Get(KarusQuests) is { } seed) seed.Visible = karus;
        if (_view.Get(ElMoradQuests) is { } michelle) michelle.Visible = !karus;
        var room = GetViewport().GetVisibleRect().Size;
        var button = _opener.GetGlobalRect();
        GlobalPosition = new Vector2(Mathf.Clamp(button.Position.X, 0f, room.X - Size.X),
            Mathf.Clamp(button.Position.Y - Size.Y, 0f, room.Y - Size.Y)).Round();
        Visible = true;
    }

    private void Run(System.Action action)
    {
        Visible = false;
        action();
    }

    public override void _Input(InputEvent ev)
    {
        if (!Visible) return;
        if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (ev is InputEventMouseButton { Pressed: true } click
            && !GetGlobalRect().HasPoint(click.GlobalPosition)
            && !_opener.GetGlobalRect().HasPoint(click.GlobalPosition))
            Visible = false;
    }
}
