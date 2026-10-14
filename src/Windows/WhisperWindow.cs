using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class WhisperWindow : Control
{
    private static readonly string[] OpenLayouts = { "re_whisper_open_1", "re_whisper_open_2", "re_whisper_open" };
    private const string CollapsedLayout = "re_whisper_close";
    private const string ChatArea = "exit_chat";
    private const string MyLineTemplate = "exit_myself";
    private const string TheirLineTemplate = "exit_opponent";
    private const string ScrollGroup = "scroll";
    private const string TitleBarOn = "top_bar_on";
    private const string TitleBarNormal = "top_bar_normal";
    private const string CollapsedOn = "base_on";
    private const string CollapsedNormal = "base_normal";
    private const string OptionGroup = "base_option";
    private const float ScrollStep = 24f;
    private const float BlinkSeconds = 0.5f;
    private const int TitleBarHeight = 30;
    private const int DefaultFontSize = 12;
    private static readonly Color NoticeColour = new(1f, 1f, 0f);

    private readonly WindowHost host;
    private readonly PluginGame game;
    private readonly string name;
    private readonly Control drag = new() { MouseFilter = MouseFilterEnum.Stop };
    private LayoutView view = null!;
    private LogText? log;
    private LineEdit? input;
    private Color mine = Colors.White;
    private Color theirs = Colors.White;
    private bool collapsed;
    private int size = -1;
    private Tween? blink;

    public WhisperWindow(WindowHost host)
    {
        this.host = host;
        game = Plugin.Kit.Game;
        name = host.Title;
        collapsed = game.Whispers.StartsCollapsed(name);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(drag);
        host.SetDragHandle(drag);
        Build();
    }

    public override void _EnterTree()
    {
        game.Whispers.LineAdded += OnLine;
        game.Whispers.Changed += OnChanged;
    }

    public override void _ExitTree()
    {
        game.Whispers.LineAdded -= OnLine;
        game.Whispers.Changed -= OnChanged;
        game.Chat.SetTyping(false);
    }

    private void Build()
    {
        var kit = Plugin.Kit;
        blink?.Kill();
        blink = null;
        if (view != null) view.QueueFree();
        size = Mathf.Clamp(game.Whispers.Size, 0, OpenLayouts.Length - 1);
        var layout = kit.Layout(collapsed ? CollapsedLayout : OpenLayouts[size]);
        view = new LayoutView(kit, layout);
        AddChild(view);
        Size = view.Size;
        CustomMinimumSize = view.Size;
        Modulate = new Color(1f, 1f, 1f, game.Whispers.Alpha);
        drag.Size = new Vector2(view.Size.X, Mathf.Min(TitleBarHeight, view.Size.Y));
        Callable.From(host.Window.ResetSize).CallDeferred();
        view.SetText("exit_id", name);
        view.OnPressed("btn_close", host.Close);
        view.OnPressed("btn_block", () => game.Whispers.ToggleBlock());
        RefreshBlock();
        if (collapsed) BuildCollapsed();
        else BuildOpen(layout);
    }

    private void BuildCollapsed()
    {
        log = null;
        input = null;
        view.Hide(CollapsedOn);
        view.OnPressed("btn_open", () => SetCollapsed(false));
    }

    private void BuildOpen(LayoutNode layout)
    {
        var kit = Plugin.Kit;
        view.Hide(TitleBarOn, OptionGroup, "btn_emoticon", MyLineTemplate, TheirLineTemplate);
        view.OnPressed("btn_hide", () => SetCollapsed(true));
        view.OnPressed("btn_option", ToggleOptions);
        view.OnPressed("btn_small", () => SetSize(0));
        view.OnPressed("btn_middle", () => SetSize(1));
        view.OnPressed("btn_high", () => SetSize(2));
        view.OnPressed("btn_save", ToggleOptions);
        if (layout.Find(OptionGroup)?.Find("scroll_alpha") is { } alpha && view.ControlOf(alpha) is { } alphaControl)
            alphaControl.Visible = false;

        mine = layout.Find(MyLineTemplate)?.Color ?? Colors.White;
        theirs = layout.Find(TheirLineTemplate)?.Color ?? Colors.White;
        var area = view.Get(ChatArea);
        var areaNode = layout.Find(ChatArea);
        log = new LogText(areaNode != null ? UiKit.FontSize(areaNode) : DefaultFontSize, areaNode?.Color ?? Colors.White, kit.Regular)
        {
            Position = area?.Position ?? Vector2.Zero,
            Size = area?.Size ?? new Vector2(200, 150),
            MouseFilter = MouseFilterEnum.Stop,
        };
        if (area != null) area.Visible = false;
        (area?.GetParent() ?? (Node)view).AddChild(log);
        log.EnableLinks(link =>
        {
            if (WhisperLinks.IsWebLink(link)) OS.ShellOpen(link);
        });
        log.Set(game.Whispers.History(name).Select(Format));

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            var buttons = scroll.Children.Where(n => n.IsButton).ToList();
            if (buttons.Count > 0 && view.ControlOf(buttons[0]) is BaseButton up) up.Pressed += () => log.Scroll(ScrollStep);
            if (buttons.Count > 1 && view.ControlOf(buttons[^1]) is BaseButton down) down.Pressed += () => log.Scroll(-ScrollStep);
        }

        input = view.Get<LineEdit>("edit_chat");
        if (input != null)
        {
            input.AddThemeFontOverride("font", kit.Regular);
            input.MaxLength = 128;
            input.TextSubmitted += Submit;
            input.FocusEntered += () => game.Chat.SetTyping(true);
            input.FocusExited += () => game.Chat.SetTyping(false);
        }
    }

    private string Format(GameWhisperLine line)
    {
        var colour = line.Notice ? NoticeColour : line.Mine ? mine : theirs;
        if (line.Notice) return $"[color=#{colour.ToHtml(false)}]{WhisperLinks.Linkify(line.Text)}[/color]";
        string who = line.Mine ? game.Character.Name : name;
        return $"[color=#{colour.ToHtml(false)}]{Escape(who)} : {Escape(line.Text)}[/color]";
    }

    private static string Escape(string text) => text.Replace("[", "[lb]");

    private void Submit(string text)
    {
        if (text.Trim().Length == 0) return;
        game.Whispers.Send(name, text);
        input?.Clear();
    }

    private void OnLine(string name, GameWhisperLine line)
    {
        if (!string.Equals(name, this.name, System.StringComparison.OrdinalIgnoreCase)) return;
        if (log != null)
        {
            log.Append(Format(line));
            if (!line.Mine && input?.HasFocus() != true) Blink(TitleBarOn);
        }
        else if (!line.Mine) Blink(CollapsedOn);
    }

    private void OnChanged()
    {
        if (!collapsed && game.Whispers.Size != size) Build();
        Modulate = new Color(1f, 1f, 1f, game.Whispers.Alpha);
        RefreshBlock();
    }

    private void RefreshBlock()
    {
        if (view.Get<TextureButton>("btn_block") is not { } block) return;
        block.ToggleMode = true;
        block.SetPressedNoSignal(game.Whispers.Blocked);
        block.TooltipText = game.Whispers.Blocked ? "Private messages are blocked - click to allow them" : "Block private messages";
    }

    private void SetCollapsed(bool collapsed)
    {
        if (this.collapsed == collapsed) return;
        this.collapsed = collapsed;
        Build();
        if (!collapsed) input?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void SetSize(int size) => game.Whispers.SetLook(size, game.Whispers.Alpha);

    private void ToggleOptions()
    {
        if (view.Get(OptionGroup) is { } options) options.Visible = !options.Visible;
    }

    private void Blink(string barId)
    {
        if (view.Get(barId) is not { } bar) return;
        blink?.Kill();
        bar.Visible = true;
        bar.Modulate = Colors.White;
        blink = bar.CreateTween().SetLoops();
        blink.TweenProperty(bar, "modulate:a", 0.2f, BlinkSeconds);
        blink.TweenProperty(bar, "modulate:a", 1f, BlinkSeconds);
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton { Pressed: true } && blink != null)
        {
            blink.Kill();
            blink = null;
            view.Hide(collapsed ? CollapsedOn : TitleBarOn);
        }
    }
}
