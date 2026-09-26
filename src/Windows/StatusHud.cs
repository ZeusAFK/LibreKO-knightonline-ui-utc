using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class StatusHud : Control
{
    public const string LayoutName = "re_hpbar";
    public const int LocationBandTop = 74;
    public static readonly Vector2 ScreenPosition = new(8, 8);
    private const string MidBackground = "img_mid_bg";
    private const string BottomBackground = "img_bot_bg";
    private const string LocationText = "Text_VP";

    private readonly LayoutView _view;
    private readonly PluginGame _game;

    public StatusHud()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        Position = ScreenPosition;
        Size = _view.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_view);
        _view.Hide("progress_HP_slow", "progress_HP_drop", "progress_HP_undead", "progress_HP_lasting", "progress_VP",
            "img_mail_on", "img_Reporter", "img_mail_normal", MidBackground, LocationText);

        float lift = layout.Find(MidBackground)?.H ?? 0;
        if (layout.Find(BottomBackground) is { } bottom && _view.ControlOf(bottom) is { } bottomControl)
            bottomControl.Position -= new Vector2(0, lift);
        Refresh();
    }

    public override void _EnterTree()
    {
        _game.Character.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Character.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
    }

    private void Refresh()
    {
        var c = _game.Character;
        _view.SetProgress("progress_HP", Fraction(c.Hp, c.MaxHp));
        _view.SetProgress("Progress_msp", Fraction(c.Mp, c.MaxMp));
        _view.SetText("Text_HP", $"{c.Hp}/{c.MaxHp}");
        _view.SetText("Text_MSP", $"{c.Mp}/{c.MaxMp}");
        _view.SetText("text_level_id", c.Name.Length > 0 ? $"Lv{c.Level} {c.Name}" : "");
    }

    private static float Fraction(int value, int max) => max > 0 ? Mathf.Clamp(value / (float)max, 0f, 1f) : 0f;
}
