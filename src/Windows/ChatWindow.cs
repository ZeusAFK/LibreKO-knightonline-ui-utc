using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class ChatWindow : Control
{
    private const string LayoutName = "re_chatting_box";
    private const string TextArea = "text0";
    private const string TextGroup = "base_chat";
    private const string InputId = "edit0";
    private const string InputBackgroundId = "img_edit_bg";
    private const string ScrollGroup = "scroll";
    private const string BackgroundId = "img_background";
    private const string OpaqueBackgroundId = "img_background2";
    private const string UserCheckPrefix = "btn_check_user_";
    private const float BottomBarHeight = 108f;
    private const float ScreenMargin = 4f;
    private const float ScrollStep = 32f;
    private const int DefaultFontSize = 12;
    private const string PrefixChars = "@!#$%&~|\\/+";
    private static readonly string[] UserRowIds = { "str_name", "img_clan", "img_userbg", "img_0", "img_1", "img_2", "img_3" };
    private static readonly string[] InputRowIds = { "img_edit1", "img_edit_bg" };

    private static readonly (string Button, string Prefix)[] ChannelTabs =
    {
        ("btn_normal", ""),
        ("btn_private", "@"),
        ("btn_shout", "!"),
        ("btn_party_force", "#"),
        ("btn_clan", "$"),
        ("btn_union", "&"),
        ("btn_chatroom", "|"),
    };

    private const string LayoutId = "theme_chat";
    private const string SettingShade = "chat.shade";
    private const string SettingShadeOn = "chat.shadeOn";
    private const string WhisperButton = "btn_private";
    private const string WhisperPrefix = "@";
    private const string OptionButton = "btn_option";
    private const string SettingShadeIndex = "chat.shadeIndex";
    private static readonly float[] ShadeLevels = { 0.75f, 0.5f, 0.25f, 0f };
    private const float DefaultShade = 0.45f;

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly PluginSettings _settings;
    private readonly LogText _log;
    private readonly LineEdit? _input;
    private readonly ColorRect _shade;
    private readonly Control? _dragButton;
    private HudLayout? _hudLayout;
    private float _shadeLevel;
    private int _shadeIndex;
    private bool _shadeOn;
    private string _prefix = "";

    public ChatWindow()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(_view);
        _view.Hide("img_edit2", "img_sizechange", "btn_knights", "btn_force", "btn_noah_knights", "btn_check_shop", "base_filter");
        _view.Hide(UserRowIds);
        _view.FadeOut(OpaqueBackgroundId);
        foreach (var (_, control) in _view.Where(n => n.Id.StartsWith(UserCheckPrefix)))
            control.Visible = false;

        _settings = kit.Context.Settings;
        var textBackground = layout.Find(TextGroup) is { } textGroup ? _view.Get(textGroup, BackgroundId) : null;
        if (textBackground != null) textBackground.Visible = false;
        _shade = new ColorRect
        {
            Position = textBackground?.Position ?? Vector2.Zero,
            Size = textBackground?.Size ?? Vector2.Zero,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        (textBackground?.GetParent() ?? (Node)_view).AddChild(_shade);
        if (textBackground != null) _shade.GetParent().MoveChild(_shade, textBackground.GetIndex() + 1);
        _shadeLevel = _settings.GetFloat(SettingShade, DefaultShade);
        _shadeIndex = _settings.GetInt(SettingShadeIndex, ShadeLevels.Length - 1);
        _shadeOn = _settings.GetBool(SettingShadeOn, false);
        ApplyShade();
        _view.OnPressed("btn_Color", () => { _shadeOn = !_shadeOn; ApplyShade(); });
        _dragButton = _view.Get(OptionButton);

        var textArea = _view.Get(TextArea);
        var textNode = layout.Find(TextArea);
        _log = new LogText(textNode != null ? UiKit.FontSize(textNode) : DefaultFontSize, textNode?.Color ?? Colors.White, kit.Regular)
        {
            Position = textArea?.Position ?? Vector2.Zero,
            Size = textArea?.Size ?? new Vector2(342, 111),
        };
        if (textArea != null) textArea.Visible = false;
        (textArea?.GetParent() ?? (Node)_view).AddChild(_log);

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            _view.OnPressed(scroll, "btn_scroll_Up", () => _log.Scroll(ScrollStep));
            foreach (var node in scroll.Children)
                if (node.IsButton && node.Id.Length == 0 && _view.ControlOf(node) is BaseButton down)
                    down.Pressed += () => _log.Scroll(-ScrollStep);
        }

        _input = _view.Get<LineEdit>(InputId);
        if (_input != null)
        {
            _input.AddThemeFontOverride("font", kit.Regular);
            _input.TextSubmitted += Submit;
            _input.FocusEntered += () => _game.Chat.SetTyping(true);
            _input.FocusExited += () => { _game.Chat.SetTyping(false); ShowInput(false); };
            if (layout.Find(InputBackgroundId) is { } row && layout.Find(InputId) is { } edit)
            {
                float rowCentre = edit.Y == 0 ? 0f : _input.Position.Y + (row.Y - edit.Y) + row.H * 0.5f;
                Callable.From(() => _input.Position = new Vector2(_input.Position.X, rowCentre - _input.Size.Y * 0.5f)).CallDeferred();
            }
        }
        ShowInput(false);

        var group = new ButtonGroup();
        foreach (var (button, prefix) in ChannelTabs)
        {
            if (_view.Get<TextureButton>(button) is not { } tab) continue;
            tab.ToggleMode = true;
            tab.ButtonGroup = group;
            tab.ButtonPressed = prefix.Length == 0;
            string which = prefix;
            tab.Pressed += () => _prefix = which;
            if (button == WhisperButton) tab.Pressed += StartWhisper;
        }
    }

    public override void _EnterTree()
    {
        _game.Chat.LineAdded += Append;
        _game.Chat.InputRequested += FocusInput;
        _game.BecameAvailable += Reload;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _dragButton, DefaultPosition, backgroundOpacityChanged: _ => CycleShade());
        Reload();
    }

    public override void _ExitTree()
    {
        _game.Chat.LineAdded -= Append;
        _game.Chat.InputRequested -= FocusInput;
        _game.BecameAvailable -= Reload;
    }

    public override void _Input(InputEvent ev)
    {
        if (_input == null || !_input.HasFocus() || ev is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode != Key.Escape) return;
        _input.Clear();
        _input.ReleaseFocus();
        GetViewport().SetInputAsHandled();
    }

    private void CycleShade()
    {
        _shadeIndex = (_shadeIndex + 1) % ShadeLevels.Length;
        _shadeLevel = ShadeLevels[_shadeIndex];
        _shadeOn = _shadeLevel > 0f;
        _settings.SetInt(SettingShadeIndex, _shadeIndex);
        ApplyShade();
    }

    private void ApplyShade()
    {
        _shade.Color = new Color(0, 0, 0, _shadeOn ? _shadeLevel : 0f);
        _settings.SetFloat(SettingShade, _shadeLevel);
        _settings.SetBool(SettingShadeOn, _shadeOn);
    }

    private void StartWhisper()
    {
        if (_input == null || !_game.Target.Has || !_game.Target.IsPlayer) return;
        FocusInput();
        _input.Text = WhisperPrefix + _game.Target.Name + " ";
        _input.CaretColumn = _input.Text.Length;
    }

    private void ShowInput(bool on)
    {
        if (_input != null) _input.Visible = on;
        foreach (var id in InputRowIds)
            foreach (var (_, control) in _view.Where(n => n.Id == id))
                control.Visible = on;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(ScreenMargin, room.Y - BottomBarHeight - Size.Y - ScreenMargin);
    }

    private void Reload() => _log.Set(_game.Chat.History);

    private void Append(string bbcode) => _log.Append(bbcode);

    private void FocusInput()
    {
        if (_input == null) return;
        ShowInput(true);
        _input.GrabFocus();
        _input.CaretColumn = _input.Text.Length;
    }

    private void Submit(string text)
    {
        if (_input == null) return;
        _input.Clear();
        _input.ReleaseFocus();
        text = text.Trim();
        if (text.Length == 0) return;
        bool hasPrefix = PrefixChars.Contains(text[0]);
        _game.Chat.Send(hasPrefix || _prefix.Length == 0 ? text : _prefix + text);
    }
}
