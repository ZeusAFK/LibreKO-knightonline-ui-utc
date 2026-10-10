using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class PartyHud : Control
{
    private const string LayoutName = "re_party";
    private const string LayoutId = "theme_party";
    private const string RowPrefix = "Base_Party";
    private const string MenuGroup = "Base_menu";
    private const int MaxRows = 8;
    private const int PanelLeft = 173;
    private const int PanelRight = 313;
    private const int MenuRight = 422;
    private const int TitleBarHeight = 25;
    private const int MenuFontSize = 11;
    private const float DefaultTop = 380f;
    private static readonly Color MenuTextColor = new(0.96f, 0.93f, 0.80f);

    private static readonly string[] HiddenIds =
    {
        "base_option", "btn_voice", "btn_commander_zoom", "Base_Rank", "btn_metertoggle", "btn_metertotal",
        "str_Count", "auto_rejoin", "img_voice", "group_uservote", MenuGroup,
        "base_Voice0", "base_Voice1", "base_Voice2", "base_Voice3", "base_Voice4", "base_Voice5", "base_Voice6", "base_Voice7",
        "img_soundrev0", "img_soundrev1", "img_soundrev2", "img_soundrev3", "img_soundrev4", "img_soundrev5", "img_soundrev6",
        "img_soundrev7",
    };

    private static readonly string[] RowHiddenIds = { "bg_small", "btn_up", "btn_down", "meterbar", "img_voice_play", "BtnCheckTP", "area" };

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly LayoutNode[] _rows = new LayoutNode[MaxRows];
    private readonly int[] _rowMember = new int[MaxRows];
    private readonly Control _drag;
    private int _menuMember = -1;
    private HudLayout? _hudLayout;

    public PartyHud()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout) { Position = new Vector2(-PanelLeft, 0) };
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(MenuRight - PanelLeft, TitleBarHeight);
        _drag = new Control
        {
            MouseFilter = MouseFilterEnum.Stop,
            Size = new Vector2(PanelRight - PanelLeft, TitleBarHeight),
        };
        AddChild(_drag);
        AddChild(_view);
        _view.Hide(HiddenIds);
        foreach (var (node, control) in _view.Where(n => n.IsString && n.Text == "Friends Voice"))
            control.Visible = false;

        for (int i = 0; i < MaxRows; i++)
        {
            _rowMember[i] = -1;
            if (layout.Find(RowPrefix + i) is not { } row) continue;
            _rows[i] = row;
            _view.Hide(row, RowHiddenIds);
            if (_view.ControlOf(row) is { } frame)
            {
                int slot = i;
                frame.MouseFilter = MouseFilterEnum.Stop;
                frame.GuiInput += ev => OnRowInput(slot, ev);
            }
        }

        _view.Caption("btn_chief", "Make leader", MenuFontSize, MenuTextColor);
        _view.Caption("btn_out", "Kick out", MenuFontSize, MenuTextColor);
        _view.Caption("btn_commander", "Make commander", MenuFontSize, MenuTextColor);
        _view.OnPressed("btn_chief", () => RunMenu(_game.Party.MakeLeader));
        _view.OnPressed("btn_out", () => RunMenu(_game.Party.Kick));
        _view.OnPressed("btn_commander", () => RunMenu(_game.Party.MakeCommander));
        _view.OnPressed("btn_party", () => _game.Party.Leave());
        if (_view.Get("btn_party") is { } leave) leave.TooltipText = "Leave the party";
    }

    public override void _EnterTree()
    {
        _game.Party.Changed += Refresh;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _drag, () => new Vector2(HudAnchor.Edge, DefaultTop));
        Refresh();
    }

    public override void _ExitTree() => _game.Party.Changed -= Refresh;

    private void Refresh()
    {
        bool shown = _game.Party.InParty;
        Visible = shown;
        if (!shown) { ShowMenu(-1); return; }
        var members = _game.Party.Members;
        int bottom = TitleBarHeight;
        for (int i = 0; i < MaxRows; i++)
        {
            var row = _rows[i];
            if (row == null) continue;
            bool used = i < members.Count;
            if (_view.ControlOf(row) is { } frame) frame.Visible = used;
            _rowMember[i] = used ? members[i].Id : -1;
            if (!used) continue;
            var m = members[i];
            bottom = Mathf.Max(bottom, row.Y + row.H);
            if (_view.Get<Label>(row, "str_name") is { } name)
            {
                name.Text = m.Name;
                name.AddThemeColorOverride("font_color", m.NameColour);
            }
            if (_view.ControlOf(row) is { } rowControl) rowControl.TooltipText = m.Tooltip;
            SetBar(row, "progress_hp", m.MaxHp > 0 ? (float)m.Hp / m.MaxHp : 0f, m.HpColour);
            SetBar(row, "progress_mp", m.MaxMp > 0 ? (float)m.Mp / m.MaxMp : 0f, null);
            if (_view.Get(row, "base_choice") is { } choice) choice.Visible = m.Picked;
        }
        Size = new Vector2(MenuRight - PanelLeft, bottom);
        if (_menuMember >= 0 && !_game.Party.IsLeader) ShowMenu(-1);
    }

    private void SetBar(LayoutNode row, string id, float fraction, Color? tint)
    {
        if (_view.Get(row, id) is not TextureProgressBar bar) return;
        bar.Value = Mathf.Clamp(fraction, 0f, 1f) * bar.MaxValue;
        if (tint is { } colour) bar.SelfModulate = colour.Lightened(0.35f);
    }

    private void OnRowInput(int slot, InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } click || _rowMember[slot] < 0) return;
        int member = _rowMember[slot];
        if (click.ButtonIndex == MouseButton.Left)
        {
            ShowMenu(-1);
            _game.Party.Pick(member);
            AcceptEvent();
        }
        else if (click.ButtonIndex == MouseButton.Right && _game.Party.IsLeader)
        {
            ShowMenu(member);
            AcceptEvent();
        }
    }

    private void ShowMenu(int member)
    {
        _menuMember = member;
        if (_view.Get(MenuGroup) is { } menu) menu.Visible = member >= 0;
    }

    private void RunMenu(System.Action<int> action)
    {
        int member = _menuMember;
        ShowMenu(-1);
        if (member >= 0) action(member);
    }
}
