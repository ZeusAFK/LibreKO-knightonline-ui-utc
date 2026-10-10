using Godot;
using KnightOnlineUi.Layout;
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

    private readonly WindowHost _host;
    private readonly PluginGame _game;
    private readonly string _name;
    private readonly Control _drag = new() { MouseFilter = MouseFilterEnum.Stop };
    private LayoutView _view = null!;
    private LogText? _log;
    private LineEdit? _input;
    private Color _mine = Colors.White;
    private Color _theirs = Colors.White;
    private bool _collapsed;
    private int _size = -1;
    private Tween? _blink;

    public WhisperWindow(WindowHost host)
    {
        _host = host;
        _game = Plugin.Kit.Game;
        _name = host.Title;
        _collapsed = _game.Whispers.StartsCollapsed(_name);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_drag);
        host.SetDragHandle(_drag);
        Build();
    }

    public override void _EnterTree()
    {
        _game.Whispers.LineAdded += OnLine;
        _game.Whispers.Changed += OnChanged;
    }

    public override void _ExitTree()
    {
        _game.Whispers.LineAdded -= OnLine;
        _game.Whispers.Changed -= OnChanged;
        _game.Chat.SetTyping(false);
    }

    private void Build()
    {
        var kit = Plugin.Kit;
        _blink?.Kill();
        _blink = null;
        if (_view != null) _view.QueueFree();
        _size = Mathf.Clamp(_game.Whispers.Size, 0, OpenLayouts.Length - 1);
        var layout = kit.Layout(_collapsed ? CollapsedLayout : OpenLayouts[_size]);
        _view = new LayoutView(kit, layout);
        AddChild(_view);
        Size = _view.Size;
        CustomMinimumSize = _view.Size;
        Modulate = new Color(1f, 1f, 1f, _game.Whispers.Alpha);
        _drag.Size = new Vector2(_view.Size.X, Mathf.Min(TitleBarHeight, _view.Size.Y));
        Callable.From(_host.Window.ResetSize).CallDeferred();
        _view.SetText("exit_id", _name);
        _view.OnPressed("btn_close", _host.Close);
        _view.OnPressed("btn_block", () => _game.Whispers.ToggleBlock());
        RefreshBlock();
        if (_collapsed) BuildCollapsed();
        else BuildOpen(layout);
    }

    private void BuildCollapsed()
    {
        _log = null;
        _input = null;
        _view.Hide(CollapsedOn);
        _view.OnPressed("btn_open", () => SetCollapsed(false));
    }

    private void BuildOpen(LayoutNode layout)
    {
        var kit = Plugin.Kit;
        _view.Hide(TitleBarOn, OptionGroup, "btn_emoticon", MyLineTemplate, TheirLineTemplate);
        _view.OnPressed("btn_hide", () => SetCollapsed(true));
        _view.OnPressed("btn_option", ToggleOptions);
        _view.OnPressed("btn_small", () => SetSize(0));
        _view.OnPressed("btn_middle", () => SetSize(1));
        _view.OnPressed("btn_high", () => SetSize(2));
        _view.OnPressed("btn_save", ToggleOptions);
        if (layout.Find(OptionGroup)?.Find("scroll_alpha") is { } alpha && _view.ControlOf(alpha) is { } alphaControl)
            alphaControl.Visible = false;

        _mine = layout.Find(MyLineTemplate)?.Color ?? Colors.White;
        _theirs = layout.Find(TheirLineTemplate)?.Color ?? Colors.White;
        var area = _view.Get(ChatArea);
        var areaNode = layout.Find(ChatArea);
        _log = new LogText(areaNode != null ? UiKit.FontSize(areaNode) : DefaultFontSize, areaNode?.Color ?? Colors.White, kit.Regular)
        {
            Position = area?.Position ?? Vector2.Zero,
            Size = area?.Size ?? new Vector2(200, 150),
            MouseFilter = MouseFilterEnum.Stop,
        };
        if (area != null) area.Visible = false;
        (area?.GetParent() ?? (Node)_view).AddChild(_log);
        _log.Set(_game.Whispers.History(_name).Select(Format));

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            var buttons = scroll.Children.Where(n => n.IsButton).ToList();
            if (buttons.Count > 0 && _view.ControlOf(buttons[0]) is BaseButton up) up.Pressed += () => _log.Scroll(ScrollStep);
            if (buttons.Count > 1 && _view.ControlOf(buttons[^1]) is BaseButton down) down.Pressed += () => _log.Scroll(-ScrollStep);
        }

        _input = _view.Get<LineEdit>("edit_chat");
        if (_input != null)
        {
            _input.AddThemeFontOverride("font", kit.Regular);
            _input.MaxLength = 128;
            _input.TextSubmitted += Submit;
            _input.FocusEntered += () => _game.Chat.SetTyping(true);
            _input.FocusExited += () => _game.Chat.SetTyping(false);
        }
    }

    private string Format(GameWhisperLine line)
    {
        var colour = line.Notice ? NoticeColour : line.Mine ? _mine : _theirs;
        string who = line.Notice ? "" : line.Mine ? "" : $"{Escape(_name)} : ";
        return $"[color=#{colour.ToHtml(false)}]{who}{Escape(line.Text)}[/color]";
    }

    private static string Escape(string text) => text.Replace("[", "[lb]");

    private void Submit(string text)
    {
        if (text.Trim().Length == 0) return;
        _game.Whispers.Send(_name, text);
        _input?.Clear();
    }

    private void OnLine(string name, GameWhisperLine line)
    {
        if (!string.Equals(name, _name, System.StringComparison.OrdinalIgnoreCase)) return;
        if (_log != null)
        {
            _log.Append(Format(line));
            if (!line.Mine && _input?.HasFocus() != true) Blink(TitleBarOn);
        }
        else if (!line.Mine) Blink(CollapsedOn);
    }

    private void OnChanged()
    {
        if (!_collapsed && _game.Whispers.Size != _size) Build();
        Modulate = new Color(1f, 1f, 1f, _game.Whispers.Alpha);
        RefreshBlock();
    }

    private void RefreshBlock()
    {
        if (_view.Get<TextureButton>("btn_block") is not { } block) return;
        block.ToggleMode = true;
        block.SetPressedNoSignal(_game.Whispers.Blocked);
        block.TooltipText = _game.Whispers.Blocked ? "Private messages are blocked - click to allow them" : "Block private messages";
    }

    private void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed) return;
        _collapsed = collapsed;
        Build();
        if (!collapsed) _input?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void SetSize(int size) => _game.Whispers.SetLook(size, _game.Whispers.Alpha);

    private void ToggleOptions()
    {
        if (_view.Get(OptionGroup) is { } options) options.Visible = !options.Visible;
    }

    private void Blink(string barId)
    {
        if (_view.Get(barId) is not { } bar) return;
        _blink?.Kill();
        bar.Visible = true;
        bar.Modulate = Colors.White;
        _blink = bar.CreateTween().SetLoops();
        _blink.TweenProperty(bar, "modulate:a", 0.2f, BlinkSeconds);
        _blink.TweenProperty(bar, "modulate:a", 1f, BlinkSeconds);
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton { Pressed: true } && _blink != null)
        {
            _blink.Kill();
            _blink = null;
            _view.Hide(_collapsed ? CollapsedOn : TitleBarOn);
        }
    }
}
