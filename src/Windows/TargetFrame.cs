using System.Collections.Generic;
using System.Linq;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class TargetFrame : Control
{
    private const string LayoutName = "re_target_framebar";
    private const string FrameShown = "frame_3";
    private const string HpBarGroup = "base_mon_hp_level";
    private const string HpBar = "grpg_level_0";
    private const string StateGroup = "base_player_hp_state";
    private const string StateBarPrefix = "state_";
    private const int StateBars = 5;
    private const float TopMargin = 8f;
    private const int HpTextSize = 11;
    private const int HpTextOutline = 3;
    private const float HpTextOverhang = 4f;

    private static readonly Dictionary<TargetHpLook, int> StateOfLook = new()
    {
        [TargetHpLook.Undead] = 0,
        [TargetHpLook.Lasting] = 2,
        [TargetHpLook.Drop] = 3,
        [TargetHpLook.Slow] = 4,
    };

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly Label _hpText;

    public TargetFrame()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _view = new LayoutView(kit, kit.Layout(LayoutName));
        Size = _view.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_view);
        foreach (var (node, control) in _view.Where(n => n.Id.StartsWith("frame_") && n.Id != FrameShown))
            control.Visible = false;
        _view.Hide("base_mon_hp_level_long", "base_cover", "base_EventDropList", "Button_Board");
        foreach (var (node, control) in _view.Where(n => n.Id.StartsWith("grpg_level_") && n.Id != HpBar))
            control.Visible = false;
        _hpText = BuildHpText();
        ShowState(-1);
        Visible = false;
    }

    private Label BuildHpText()
    {
        var bar = _view.Where(n => n.Id == HpBarGroup).First().Node;
        var label = new Label
        {
            Position = new Vector2(bar.X, bar.Y - HpTextOverhang * 0.5f),
            Size = new Vector2(bar.W, bar.H + HpTextOverhang),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", HpTextSize);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        label.AddThemeConstantOverride("outline_size", HpTextOutline);
        _view.AddChild(label);
        return label;
    }

    public override void _EnterTree()
    {
        _game.Target.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _game.BecameUnavailable += Refresh;
        GetViewport().SizeChanged += Place;
        Place();
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Target.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
        _game.BecameUnavailable -= Refresh;
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        float width = GetViewport().GetVisibleRect().Size.X;
        Position = new Vector2(Mathf.Round((width - Size.X) * 0.5f), TopMargin);
    }

    private void ShowState(int state)
    {
        if (_view.Get(HpBarGroup) is { } normal) normal.Visible = state < 0;
        if (_view.Get(StateGroup) is { } group) group.Visible = state >= 0;
        for (int i = 0; i < StateBars; i++)
            if (_view.Get(StateBarPrefix + i) is { } bar) bar.Visible = i == state;
    }

    private void Refresh()
    {
        var t = _game.Target;
        Visible = _game.Available && t.Has;
        if (!Visible) return;
        _view.SetText("name", t.Level > 0 ? $"{t.Name}  Lv.{t.Level}" : t.Name);
        float fraction = t.MaxHp > 0 ? t.Hp / (float)t.MaxHp : 1f;
        int state = StateOfLook.TryGetValue(t.HpLook, out int s) ? s : -1;
        ShowState(state);
        _view.SetProgress(state < 0 ? HpBar : StateBarPrefix + state, fraction);
        _hpText.Text = t.MaxHp > 0 ? $"{t.Hp} / {t.MaxHp}" : "";
    }
}
