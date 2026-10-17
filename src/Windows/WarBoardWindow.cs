using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
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

    private readonly PluginGame game;
    private readonly UiKit kit;
    private readonly LayoutView view;
    private readonly LayoutNode?[] rows = new LayoutNode?[PartyWarBoard.MaxRows];
    private readonly TextureRect?[] icons = new TextureRect?[PartyWarBoard.MaxRows];
    private readonly Control drag;
    private HudLayout? hudLayout;

    public WarBoardWindow()
    {
        kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        drag = new Control { MouseFilter = MouseFilterEnum.Stop, Size = new Vector2(layout.W, TitleBarHeight) };
        AddChild(view);
        AddChild(drag);
        view.Hide("cooltime");

        for (int i = 0; i < PartyWarBoard.MaxRows; i++)
        {
            if (layout.Find(RowPrefix + i) is not { } row) continue;
            rows[i] = row;
            if (view.Get(row, "class") is { } area)
            {
                var icon = new TextureRect
                {
                    Size = area.Size,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                area.AddChild(icon);
                icons[i] = icon;
            }
        }

        view.OnPressed("tab_deal", () => game.Party.SetWarBoardMode(PartyBoardMeterMode.Damage));
        view.OnPressed("tab_heal", () => game.Party.SetWarBoardMode(PartyBoardMeterMode.Healing));
        view.OnPressed("close", () => game.Party.ToggleWarBoard());
        foreach (var id in new[] { "tab_deal", "tab_heal" })
            if (view.Get<TextureButton>(id) is { } tab) tab.ToggleMode = true;
        if (layout.Find("alpha") is { } alpha)
        {
            var arrows = alpha.Children.Where(n => n.IsButton).ToList();
            if (arrows.Count > 0 && view.ControlOf(arrows[0]) is BaseButton down)
                down.Pressed += () => game.Party.SetWarBoardAlpha(game.Party.WarBoardAlpha - AlphaStep);
            if (arrows.Count > 1 && view.ControlOf(arrows[^1]) is BaseButton up)
                up.Pressed += () => game.Party.SetWarBoardAlpha(game.Party.WarBoardAlpha + AlphaStep);
        }
    }

    public override void _EnterTree()
    {
        game.Party.Changed += Refresh;
        game.BecameAvailable += Refresh;
        hudLayout ??= HudLayout.Attach(this, LayoutId, drag, () => new Vector2(GetViewport().GetVisibleRect().Size.X * 0.5f - Size.X * 0.5f, DefaultTop));
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Party.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
    }

    private void Refresh()
    {
        var party = game.Party;
        Visible = party.InParty && party.WarBoardOpen;
        if (!Visible) return;
        if (view.Get<TextureButton>("tab_deal") is { } deal) deal.SetPressedNoSignal(party.WarBoardMode == PartyBoardMeterMode.Damage);
        if (view.Get<TextureButton>("tab_heal") is { } heal) heal.SetPressedNoSignal(party.WarBoardMode == PartyBoardMeterMode.Healing);
        if (view.Get(Background) is { } background) background.Modulate = new Color(1, 1, 1, party.WarBoardAlpha);

        var lines = party.WarBoardRows;
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] is not { } row || view.ControlOf(row) is not { } rowControl) continue;
            bool used = i < lines.Count;
            rowControl.Visible = used;
            if (!used) continue;
            var line = lines[i];
            if (view.Get<Label>(row, "name") is { } name) name.Text = PartyWarBoard.RowName(line.Rank, line.Name);
            if (view.Get<Label>(row, "per") is { } percent) percent.Text = PartyWarBoard.Percent(line.Share);
            if (view.Get<TextureProgressBar>(row, "bar") is { } bar)
            {
                bar.Value = PartyWarBoard.Fill(line.Share) * bar.MaxValue;
                if (ClassBar(line.Class) is { } fill) bar.TextureProgress = fill;
            }
            if (icons[i] is { } icon) icon.Texture = UiIcons.Get(LibreKO.Domain.PartyHud.ClassIcon(line.Class));
        }
    }

    private Texture2D? ClassBar(int classCode)
    {
        int family = CharacterClassCatalog.Family(classCode);
        return family <= CharacterClassCatalog.FamilyUnknown ? null : kit.Texture(string.Format(ClassBarFormat, family - 1));
    }
}
