using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class WarBoardWindow : Control
{
    private const string LayoutName = "re_damagemeter_party";
    private const string LayoutId = "theme_war_board";
    private const string RowPrefix = "rank_";
    private const string ClassBarFormat = "prg_class_p_{0}.png";
    private const string Background = "bg";
    private const float AlphaStep = 0.05f;
    private const float TitleBarHeight = 26f;
    private const float DefaultTop = 220f;

    private readonly PluginGame _game;
    private readonly UiKit _kit;
    private readonly LayoutView _view;
    private readonly LayoutNode?[] _rows = new LayoutNode?[PartyWarBoard.MaxRows];
    private readonly TextureRect?[] _icons = new TextureRect?[PartyWarBoard.MaxRows];
    private readonly Control _drag;
    private HudLayout? _hudLayout;

    public WarBoardWindow()
    {
        _kit = Plugin.Kit;
        _game = _kit.Game;
        var layout = _kit.Layout(LayoutName);
        _view = new LayoutView(_kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        _drag = new Control { MouseFilter = MouseFilterEnum.Stop, Size = new Vector2(layout.W, TitleBarHeight) };
        AddChild(_view);
        AddChild(_drag);
        _view.Hide("cooltime");

        for (int i = 0; i < PartyWarBoard.MaxRows; i++)
        {
            if (layout.Find(RowPrefix + i) is not { } row) continue;
            _rows[i] = row;
            if (_view.Get(row, "class") is { } area)
            {
                var icon = new TextureRect
                {
                    Size = area.Size,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                area.AddChild(icon);
                _icons[i] = icon;
            }
        }

        _view.OnPressed("tab_deal", () => _game.Party.SetWarBoardMode(PartyWarBoard.ModeDamage));
        _view.OnPressed("tab_heal", () => _game.Party.SetWarBoardMode(PartyWarBoard.ModeHealing));
        _view.OnPressed("close", () => _game.Party.ToggleWarBoard());
        foreach (var id in new[] { "tab_deal", "tab_heal" })
            if (_view.Get<TextureButton>(id) is { } tab) tab.ToggleMode = true;
        if (layout.Find("alpha") is { } alpha)
        {
            var arrows = alpha.Children.Where(n => n.IsButton).ToList();
            if (arrows.Count > 0 && _view.ControlOf(arrows[0]) is BaseButton down)
                down.Pressed += () => _game.Party.SetWarBoardAlpha(_game.Party.WarBoardAlpha - AlphaStep);
            if (arrows.Count > 1 && _view.ControlOf(arrows[^1]) is BaseButton up)
                up.Pressed += () => _game.Party.SetWarBoardAlpha(_game.Party.WarBoardAlpha + AlphaStep);
        }
    }

    public override void _EnterTree()
    {
        _game.Party.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _drag, () => new Vector2(GetViewport().GetVisibleRect().Size.X * 0.5f - Size.X * 0.5f, DefaultTop));
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Party.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
    }

    private void Refresh()
    {
        var party = _game.Party;
        Visible = party.InParty && party.WarBoardOpen;
        if (!Visible) return;
        if (_view.Get<TextureButton>("tab_deal") is { } deal) deal.SetPressedNoSignal(party.WarBoardMode == PartyWarBoard.ModeDamage);
        if (_view.Get<TextureButton>("tab_heal") is { } heal) heal.SetPressedNoSignal(party.WarBoardMode == PartyWarBoard.ModeHealing);
        if (_view.Get(Background) is { } background) background.Modulate = new Color(1, 1, 1, party.WarBoardAlpha);

        var lines = party.WarBoardRows;
        for (int i = 0; i < _rows.Length; i++)
        {
            if (_rows[i] is not { } row || _view.ControlOf(row) is not { } rowControl) continue;
            bool used = i < lines.Count;
            rowControl.Visible = used;
            if (!used) continue;
            var line = lines[i];
            if (_view.Get<Label>(row, "name") is { } name) name.Text = PartyWarBoard.RowName(line.Rank, line.Name);
            if (_view.Get<Label>(row, "per") is { } percent) percent.Text = PartyWarBoard.Percent(line.Share);
            if (_view.Get<TextureProgressBar>(row, "bar") is { } bar)
            {
                bar.Value = PartyWarBoard.Fill(line.Share) * bar.MaxValue;
                if (ClassBar(line.Class) is { } fill) bar.TextureProgress = fill;
            }
            if (_icons[i] is { } icon) icon.Texture = UiIcons.Get(LibreKO.Domain.PartyHud.ClassIcon(line.Class));
        }
    }

    private Texture2D? ClassBar(int classCode)
    {
        int family = CharacterClassCatalog.Family(classCode);
        return family <= CharacterClassCatalog.FamilyUnknown ? null : _kit.Texture(string.Format(ClassBarFormat, family - 1));
    }
}
