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

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly Control opener;

    public TaskbarMenu(Control opener)
    {
        this.opener = opener;
        var kit = Plugin.Kit;
        game = kit.Game;
        view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Ignore;
        TopLevel = true;
        Size = view.Size;
        AddChild(view);
        Visible = false;

        foreach (var (button, action) in ActionRows)
            view.OnPressed(button, () => Run(() => game.Commands.Run(action)));
        foreach (var (button, window) in WindowRows)
            view.OnPressed(button, () => Run(() => game.Windows.Toggle(window)));
        view.OnPressed("btn_trade", () => Run(game.Commands.TradeWithTarget));
        view.OnPressed("btn_party", () => Run(game.Commands.InviteTarget));
        view.OnPressed("btn_option", () => Run(game.Commands.OpenSettings));
        view.OnPressed("btn_exit", () => Run(game.Commands.ShowExitMenu));
        foreach (var id in Unavailable)
            if (view.Get<BaseButton>(id) is { } off)
            {
                off.Disabled = true;
                off.TooltipText = UnavailableTip;
            }
    }

    public void Toggle()
    {
        if (Visible) { Visible = false; return; }
        bool karus = game.Character.Nation == KarusNation;
        if (view.Get(KarusQuests) is { } seed) seed.Visible = karus;
        if (view.Get(ElMoradQuests) is { } michelle) michelle.Visible = !karus;
        var room = GetViewport().GetVisibleRect().Size;
        var button = opener.GetGlobalRect();
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
            && !opener.GetGlobalRect().HasPoint(click.GlobalPosition))
            Visible = false;
    }
}
