using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
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
        ("btn_shout", "!"),
        ("btn_party_force", "#"),
        ("btn_clan", "$"),
        ("btn_union", "&"),
        ("btn_chatroom", "\\"),
        ("btn_noah_knights", "|"),
    };
    private const string NoahButton = "btn_noah_knights";
    private const string NoahFilterBox = "btn_check_noah_knights";
    private static readonly string[] ClanChannelButtons = { "btn_clan", "btn_union" };
    private readonly Dictionary<string, TextureButton> _channels = new();

    private const string LayoutId = "theme_chat";
    private const string SettingShade = "chat.shade";
    private const string SettingShadeOn = "chat.shadeOn";
    private const string WhisperButton = "btn_private";
    private const string WhisperPrefix = "@";
    private const int WhisperButtonTipText = 3031;
    private const string OptionButton = "btn_option";
    private const string SettingShadeIndex = "chat.shadeIndex";
    private const string SettingHidden = "chat.hidden";
    private const string SettingLocked = "chat.locked";
    private const string FilterPanel = "base_filter";
    private const string FilterButton = "btn_filter";
    private const string LockButton = "btn_chat_lock";
    private const string BlockButton = "btn_check_block";
    private const string UnavailableTip = "Not available";
    private const ChatCategory AlwaysShown = ChatCategory.Notice | ChatCategory.Commander;

    private static readonly (string Button, ChatCategory Category)[] FilterBoxes =
    {
        ("btn_check_normal", ChatCategory.General),
        ("btn_check_private", ChatCategory.Whisper),
        ("btn_check_shout", ChatCategory.Shout),
        ("btn_check_party_force", ChatCategory.Party),
        ("btn_check_clan", ChatCategory.Clan),
        ("btn_check_union", ChatCategory.Alliance),
        ("btn_check_noah_knights", ChatCategory.NoahKnights),
        ("btn_check_chatroom", ChatCategory.ChatRoom),
        ("btn_check_userinfo", ChatCategory.System),
        ("btn_check_shop", ChatCategory.Trade),
    };
    private static readonly float[] ShadeLevels = { 0.75f, 0.5f, 0.25f, 0f };
    private const float DefaultShade = 0.45f;

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly PluginSettings _settings;
    private readonly LogText _log;
    private readonly LineEdit? _input;
    private readonly ColorRect _shade;
    private readonly Control? _dragButton;
    private readonly Vector2 _baseSize;
    private Control? _scrollDown;
    private VerticalGrow? _grow;
    private bool _relaying;
    private HudLayout? _hudLayout;

    private static readonly HashSet<string> StretchIds = new()
    {
        "base_main", "base_chat", "base_area", "base_scroll", "base_mapuserlist", "img_background", "img_background2",
        "img_scr_bg", "img_scr_bg2", "scroll", "scroll_bar", "img_scrollback", "text0",
    };

    private static readonly HashSet<string> ShiftIds = new()
    {
        "img_edit_bg", "img_edit1", "img_edit2", "edit0", "base_chat_top", "btn_option", "btn_chat_lock",
        "base_user_scroll", "btn_connect", "btn_location", "base_num0", "base_num1", "base_num2", "base_num3",
        "base_num4", "base_num5", "base_num6",
    };
    private float _shadeLevel;
    private int _shadeIndex;
    private bool _shadeOn;
    private string _prefix = "";
    private ChatCategory _hidden;

    public ChatWindow()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(_view);
        _view.Hide("img_edit2", "img_sizechange", "btn_knights", "btn_force", NoahButton, FilterPanel);
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
        _hidden = (ChatCategory)_settings.GetInt(SettingHidden, 0) & ~AlwaysShown;
        BuildFilterPanel();
        _dragButton = _view.Get(OptionButton);

        var textArea = _view.Get(TextArea);
        var textNode = layout.Find(TextArea);
        _log = new LogText(textNode != null ? UiKit.FontSize(textNode) : DefaultFontSize, textNode?.Color ?? Colors.White, kit.Regular)
        {
            Position = textArea?.Position ?? Vector2.Zero,
            Size = textArea?.Size ?? new Vector2(342, 111),
            MouseFilter = MouseFilterEnum.Pass,
        };
        if (textArea != null) textArea.Visible = false;
        (textArea?.GetParent() ?? (Node)_view).AddChild(_log);

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            _view.OnPressed(scroll, "btn_scroll_Up", () => _log.Scroll(ScrollStep));
            foreach (var node in scroll.Children)
                if (node.IsButton && node.Id.Length == 0 && _view.ControlOf(node) is BaseButton down)
                {
                    down.Pressed += () => _log.Scroll(-ScrollStep);
                    _scrollDown = down;
                }
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
            _channels[button] = tab;
        }

        if (_view.Get<BaseButton>(WhisperButton) is { } whisper)
        {
            whisper.TooltipText = ItemData.Text(WhisperButtonTipText,
                "You can click on the character and press the 'Private Message' button to start a private chat.");
            whisper.Pressed += StartWhisper;
        }

        _baseSize = Size;
        Resized += Relayout;
        Callable.From(CaptureGrow).CallDeferred();
    }

    private void BuildFilterPanel()
    {
        _view.OnPressed(FilterButton, () =>
        {
            if (_view.Get(FilterPanel) is { } panel) panel.Visible = !panel.Visible;
        });
        foreach (var (button, category) in FilterBoxes)
        {
            if (_view.Get<TextureButton>(button) is not { } box) continue;
            box.ToggleMode = true;
            box.ButtonPressed = (_hidden & category) == 0;
            var which = category;
            box.Toggled += shown => SetShown(which, shown);
        }
        if (_view.Get<BaseButton>(BlockButton) is { } block)
        {
            block.Disabled = true;
            block.TooltipText = UnavailableTip;
        }
        if (_view.Get<TextureButton>(LockButton) is { } lockButton)
        {
            lockButton.ToggleMode = true;
            lockButton.ButtonPressed = _settings.GetBool(SettingLocked, false);
            lockButton.Toggled += locked =>
            {
                _settings.SetBool(SettingLocked, locked);
                if (_hudLayout != null) _hudLayout.Locked = locked;
            };
        }
    }

    private void SetShown(ChatCategory category, bool shown)
    {
        _hidden = shown ? _hidden & ~category : _hidden | category;
        _settings.SetInt(SettingHidden, (int)_hidden);
        Reload();
    }

    private void RefreshChannels()
    {
        bool noah = _game.Character.NoahKnight;
        SetChannelShown(NoahButton, noah);
        foreach (var button in ClanChannelButtons) SetChannelShown(button, !noah);
        if (_view.Get<BaseButton>(NoahFilterBox) is { } box) box.Disabled = !noah;
    }

    private void SetChannelShown(string button, bool shown)
    {
        if (!_channels.TryGetValue(button, out var tab)) return;
        tab.Visible = shown;
        if (shown || !tab.ButtonPressed) return;
        _prefix = "";
        if (_channels.TryGetValue(ChannelTabs[0].Button, out var normal)) normal.ButtonPressed = true;
    }

    private bool Shown(GameChatLine line) => (line.Category & AlwaysShown) != 0 || (line.Category & _hidden) == 0;

    private void CaptureGrow()
    {
        var grow = new VerticalGrow();
        grow.Add(_view, VerticalGrow.Mode.Stretch);
        foreach (var (node, control) in _view.Where(_ => true))
            grow.Add(control, control == _scrollDown ? VerticalGrow.Mode.Shift
                : StretchIds.Contains(node.Id) ? VerticalGrow.Mode.Stretch
                : ShiftIds.Contains(node.Id) ? VerticalGrow.Mode.Shift
                : VerticalGrow.Mode.Stay);
        grow.Add(_shade, VerticalGrow.Mode.Stretch);
        grow.Add(_log, VerticalGrow.Mode.Stretch);
        foreach (var parent in grow.Stretched.Where(c => c != _shade && c != _log))
            grow.ShiftBottomBand(parent);
        _grow = grow;
        Relayout();
    }

    private void Relayout()
    {
        if (_grow == null || _relaying) return;
        _relaying = true;
        if (!Mathf.IsEqualApprox(Size.X, _baseSize.X)) Size = new Vector2(_baseSize.X, Size.Y);
        _grow.Apply(Mathf.Max(0f, Size.Y - _baseSize.Y));
        _relaying = false;
    }

    public override void _EnterTree()
    {
        _game.Chat.LineArrived += Append;
        _game.Chat.InputRequested += FocusInput;
        _game.BecameAvailable += Reload;
        _game.Character.Changed += RefreshChannels;
        _game.BecameAvailable += RefreshChannels;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _dragButton, DefaultPosition, resizable: true,
            defaultSize: _baseSize, minimumSize: _baseSize, resizeCorner: HudLayout.Corner.TopRight,
            backgroundOpacityChanged: _ => CycleShade());
        _hudLayout.Locked = _settings.GetBool(SettingLocked, false);
        Reload();
    }

    public override void _ExitTree()
    {
        _game.Chat.LineArrived -= Append;
        _game.Chat.InputRequested -= FocusInput;
        _game.BecameAvailable -= Reload;
        _game.Character.Changed -= RefreshChannels;
        _game.BecameAvailable -= RefreshChannels;
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
        if (_game.Target.Has && _game.Target.IsPlayer)
        {
            _game.Whispers.OpenWith(_game.Target.Name);
            return;
        }
        if (_input == null) return;
        FocusInput();
        _input.Text = WhisperPrefix;
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

    private void Reload() => _log.Set(_game.Chat.Lines.Where(Shown).Select(line => line.BbCode));

    private void Append(GameChatLine line)
    {
        if (Shown(line)) _log.Append(line.BbCode);
    }

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
