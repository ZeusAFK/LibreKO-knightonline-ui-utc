using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class CharacterWindow : Control
{
    private const string FrameLayout = "re_various_frame";
    private const string PageLayout = "re_page_state";
    private const int TitleBarHeight = 60;
    private const string ActiveTab = "btn_state";

    private static readonly (string Button, int StatRow)[] StatButtons =
    {
        ("Btn_Strength", 0),
        ("Btn_Stamina", 1),
        ("Btn_Dexterity", 2),
        ("Btn_Intelligence", 3),
        ("Btn_MagicAttack", 4),
    };

    private readonly LayoutView _page;
    private readonly LayoutView _frame;
    private readonly PluginGame _game;
    private readonly WindowHost _host;

    public CharacterWindow(WindowHost host)
    {
        _host = host;
        var kit = Plugin.Kit;
        _game = kit.Game;
        var pageLayout = kit.Layout(PageLayout);
        var frameLayout = kit.Layout(FrameLayout);
        _page = new LayoutView(kit, pageLayout);
        _frame = new LayoutView(kit, frameLayout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_page);
        AddChild(_frame);
        _frame.Position = new Vector2(frameLayout.X - pageLayout.X, frameLayout.Y - pageLayout.Y);
        CustomMinimumSize = new Vector2(Mathf.Max(_page.Size.X, _frame.Size.X), Mathf.Max(_page.Size.Y, _frame.Size.Y));
        Size = CustomMinimumSize;

        host.SetDragHandle(_frame.MakeDragHandle(new Rect2(0, 0, _frame.Size.X, TitleBarHeight)));
        _frame.OnPressed("btn_close", host.Close);
        _frame.Hide("btn_knights", "btn_clan", "btn_friends", "btn_force", "btn_title");
        if (_frame.Get<TextureButton>(ActiveTab) is { } tab)
        {
            tab.ToggleMode = true;
            tab.ButtonPressed = true;
        }
        _page.Hide("btn_preset", "tooltip_rank");
        foreach (var (node, control) in _page.Where(n => n.Id.StartsWith("icon_level_")))
            control.Visible = false;
        foreach (var (button, row) in StatButtons)
        {
            int which = row;
            _page.OnPressed(button, () => _game.Character.AllocateStat(which));
        }
        Refresh();
    }

    public override void _EnterTree()
    {
        _game.Character.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _host.Shown += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Character.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
        _host.Shown -= Refresh;
    }

    private void Refresh()
    {
        var c = _game.Character;
        _page.SetText("Text_Id", c.Name);
        _page.SetText("Text_Class", c.ClassName);
        _page.SetText("Text_Level", c.Level.ToString());
        _page.SetText("Text_Nation", c.NationName);
        _page.SetText("Text_Exp", $"{c.Exp:n0} / {c.MaxExp:n0}");
        _page.SetText("Text_RealmPoint", c.Np.ToString("n0"));
        _page.SetText("Text_AP", c.Ap.ToString());
        _page.SetText("Text_GP", c.Ac.ToString());
        _page.SetText("Text_Manner", "");
        _page.SetText("Text_Strength", c.Str.ToString());
        _page.SetText("Text_Stamina", c.Sta.ToString());
        _page.SetText("Text_Dexterity", c.Dex.ToString());
        _page.SetText("Text_MagicAttack", c.Mag.ToString());
        _page.SetText("Text_Intelligence", c.Intel.ToString());
        _page.SetText("Text_BonusPoint", c.Points.ToString());
        _page.SetText("Text_RegistFire", c.Resist(0).ToString());
        _page.SetText("Text_RegistIce", c.Resist(1).ToString());
        _page.SetText("Text_RegistLightR", c.Resist(2).ToString());
        _page.SetText("Text_RegistMagic", c.Resist(3).ToString());
        _page.SetText("Text_RegistCurse", c.Resist(4).ToString());
        _page.SetText("Text_RegistPoison", c.Resist(5).ToString());
        foreach (var (button, _) in StatButtons)
            if (_page.Get(button) is { } b) b.Visible = c.Points > 0;
    }
}
