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
    private const float TopMargin = 8f;

    private readonly LayoutView _view;
    private readonly PluginGame _game;

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
        _view.Hide("base_mon_hp_level_long", "base_player_hp_state", "base_cover", "base_EventDropList", "Button_Board");
        foreach (var (node, control) in _view.Where(n => n.Id.StartsWith("grpg_level_") && n.Id != HpBar))
            control.Visible = false;
        Visible = false;
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

    private void Refresh()
    {
        var t = _game.Target;
        Visible = _game.Available && t.Has;
        if (!Visible) return;
        _view.SetText("name", t.Level > 0 ? $"{t.Name}  Lv.{t.Level}" : t.Name);
        _view.SetProgress(HpBar, t.MaxHp > 0 ? t.Hp / (float)t.MaxHp : 1f);
    }
}
