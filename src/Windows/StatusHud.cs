using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Domain;
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
    private const string MailIcon = "img_mail_on";
    private const string MailWindow = "mail";
    private const int MailTipText = 10803;

    private readonly LayoutView view;
    private readonly PluginGame game;

    public StatusHud()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        Position = ScreenPosition;
        Size = view.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(view);
        view.Hide("progress_HP_slow", "progress_HP_drop", "progress_HP_undead", "progress_HP_lasting", "progress_VP",
            MailIcon, "img_Reporter", "img_mail_normal", MidBackground, LocationText);
        if (view.Get(MailIcon) is { } mail)
        {
            mail.MouseFilter = MouseFilterEnum.Stop;
            mail.TooltipText = ItemData.Text(MailTipText, "You can view the mail box by clicking the button.");
            mail.GuiInput += ev =>
            {
                if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
                game.Windows.Open(MailWindow);
                mail.AcceptEvent();
            };
        }

        float lift = layout.Find(MidBackground)?.H ?? 0;
        if (layout.Find(BottomBackground) is { } bottom && view.ControlOf(bottom) is { } bottomControl)
            bottomControl.Position -= new Vector2(0, lift);
        Refresh();
    }

    public override void _EnterTree()
    {
        game.Character.Changed += Refresh;
        game.BecameAvailable += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Character.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
    }

    private void Refresh()
    {
        var c = game.Character;
        view.SetProgress("progress_HP", Fraction(c.Hp, c.MaxHp));
        view.SetProgress("Progress_msp", Fraction(c.Mp, c.MaxMp));
        view.SetText("Text_HP", $"{c.Hp}/{c.MaxHp}");
        view.SetText("Text_MSP", $"{c.Mp}/{c.MaxMp}");
        view.SetText("text_level_id", c.Name.Length > 0 ? $"Lv{c.Level} {c.Name}" : "");
        if (view.Get(MailIcon) is { } mail) mail.Visible = c.MailUnread > 0;
    }

    private static float Fraction(int value, int max) => max > 0 ? Mathf.Clamp(value / (float)max, 0f, 1f) : 0f;
}
