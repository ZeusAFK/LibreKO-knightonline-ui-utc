using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class CharacterWindow : Control
{
    private const string PresetsWindow = "presets";
    private const string FrameLayout = "re_various_frame";
    private const string PageLayout = "re_page_state";
    private const int TitleBarHeight = 60;
    private const string ActiveTab = "btn_state";
    private const string FriendsTab = "btn_friends";
    private const string TitleTab = "btn_title";
    private const string TitlesWindow = "titles";
    private const string MarkFormat = "icon_level_{0}";
    private const int MarkCells = 20;

    private static readonly (string Button, int StatRow)[] StatButtons =
    {
        ("Btn_Strength", 0),
        ("Btn_Stamina", 1),
        ("Btn_Dexterity", 2),
        ("Btn_Intelligence", 3),
        ("Btn_MagicAttack", 4),
    };

    private static readonly (string Text, int StatRow)[] StatTexts =
    {
        ("Text_Strength", 0),
        ("Text_Stamina", 1),
        ("Text_Dexterity", 2),
        ("Text_Intelligence", 3),
        ("Text_MagicAttack", 4),
    };

    private readonly LayoutView page;
    private readonly LayoutView frame;
    private readonly FriendsPage friends;
    private readonly PluginGame game;
    private readonly WindowHost host;
    private readonly RankRibbonCells marks;

    public CharacterWindow(WindowHost host)
    {
        this.host = host;
        var kit = Plugin.Kit;
        game = kit.Game;
        var pageLayout = kit.Layout(PageLayout);
        var frameLayout = kit.Layout(FrameLayout);
        page = new LayoutView(kit, pageLayout);
        marks = new RankRibbonCells(page);
        frame = new LayoutView(kit, frameLayout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(page);
        friends = new FriendsPage(kit, game.Friends);
        var friendsLayout = kit.Layout(FriendsPage.LayoutName);
        friends.View.Position = new Vector2(friendsLayout.X - pageLayout.X, friendsLayout.Y - pageLayout.Y);
        friends.View.Visible = false;
        AddChild(friends.View);
        AddChild(frame);
        frame.Position = new Vector2(frameLayout.X - pageLayout.X, frameLayout.Y - pageLayout.Y);
        CustomMinimumSize = new Vector2(Mathf.Max(page.Size.X, frame.Size.X), Mathf.Max(page.Size.Y, frame.Size.Y));
        Size = CustomMinimumSize;

        host.SetDragHandle(frame.MakeDragHandle(new Rect2(0, 0, frame.Size.X, TitleBarHeight)));
        frame.OnPressed("btn_close", host.Close);
        frame.Hide("btn_knights", "btn_clan", "btn_force");
        frame.OnPressed(TitleTab, () => game.Windows.Toggle(TitlesWindow));
        var tabs = new ButtonGroup();
        foreach (var (id, friendsShown) in new[] { (ActiveTab, false), (FriendsTab, true) })
            if (frame.Get<TextureButton>(id) is { } tab)
            {
                tab.ToggleMode = true;
                tab.ButtonGroup = tabs;
                tab.ButtonPressed = !friendsShown;
                bool show = friendsShown;
                tab.Pressed += () => ShowFriends(show);
            }
        page.Hide("tooltip_rank");
        page.OnPressed("btn_preset", () => game.Windows.Toggle(PresetsWindow));
        foreach (var (button, row) in StatButtons)
        {
            int which = row;
            page.OnPressed(button, () => game.Character.AllocateStat(which));
        }
        Refresh();
    }

    private void ShowFriends(bool shown)
    {
        page.Visible = !shown;
        friends.View.Visible = shown;
        if (shown) friends.Refresh();
    }

    public override void _EnterTree()
    {
        game.Friends.Changed += friends.Refresh;
        game.Character.Changed += Refresh;
        game.BecameAvailable += Refresh;
        host.Shown += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Friends.Changed -= friends.Refresh;
        game.Character.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
        host.Shown -= Refresh;
    }

    private void Refresh()
    {
        var c = game.Character;
        page.SetText("Text_Id", c.Name);
        page.SetText("Text_Class", c.ClassName);
        page.SetText("Text_Level", c.Level.ToString());
        page.SetText("Text_Nation", c.NationName);
        page.SetText("Text_Exp", $"{c.Exp:n0} / {c.MaxExp:n0}");
        page.SetText("Text_RealmPoint", c.Np.ToString("n0"));
        page.SetText("Text_AP", c.Ap.ToString());
        page.SetText("Text_GP", c.Ac.ToString());
        page.SetText("Text_Manner", c.Manner.ToString("n0"));
        page.SetText("Text_Strength", c.Str.ToString());
        page.SetText("Text_Stamina", c.Sta.ToString());
        page.SetText("Text_Dexterity", c.Dex.ToString());
        page.SetText("Text_MagicAttack", c.Mag.ToString());
        page.SetText("Text_Intelligence", c.Intel.ToString());
        page.SetText("Text_BonusPoint", c.Points.ToString());
        page.SetText("Text_RegistFire", c.Resist(0).ToString());
        page.SetText("Text_RegistIce", c.Resist(1).ToString());
        page.SetText("Text_RegistLightR", c.Resist(2).ToString());
        page.SetText("Text_RegistMagic", c.Resist(3).ToString());
        page.SetText("Text_RegistCurse", c.Resist(4).ToString());
        page.SetText("Text_RegistPoison", c.Resist(5).ToString());
        var ranks = c.RankMarks;
        for (int i = 0; i < MarkCells; i++)
            marks.Show(string.Format(MarkFormat, i), i < ranks.Count ? ranks[i] : null);
        foreach (var (button, _) in StatButtons)
            if (page.Get(button) is { } b) b.Visible = c.Points > 0;
        foreach (var (text, row) in StatTexts)
            if (page.Get(text) is { } label)
            {
                label.MouseFilter = MouseFilterEnum.Pass;
                label.TooltipText = StatBonusTooltip(c.RebirthStat(row), c.StatBonus(row) - c.RebirthStat(row));
            }
    }

    private static string StatBonusTooltip(int rebirth, int other)
    {
        var lines = new List<string>();
        if (rebirth != 0) lines.Add($"Rebirth {rebirth:+#;-#}");
        if (other != 0) lines.Add($"Items and buffs {other:+#;-#}");
        return string.Join("\n", lines);
    }
}
