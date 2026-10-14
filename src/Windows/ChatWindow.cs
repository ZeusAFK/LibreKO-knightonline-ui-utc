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
    private readonly Dictionary<string, TextureButton> channels = new();

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

    private readonly LayoutView view;
    private readonly PluginGame game;
    private readonly PluginSettings settings;
    private readonly LogText log;
    private readonly LineEdit? input;
    private readonly ColorRect shade;
    private readonly Control? dragButton;
    private readonly Vector2 baseSize;
    private Control? scrollDown;
    private VerticalGrow? grow;
    private bool relaying;
    private HudLayout? hudLayout;

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
    private float shadeLevel;
    private int shadeIndex;
    private bool shadeOn;
    private string prefix = "";
    private ChatCategory hidden;

    public ChatWindow()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(view);
        view.Hide("img_edit2", "img_sizechange", "btn_knights", "btn_force", NoahButton, FilterPanel);
        view.Hide(UserRowIds);
        view.FadeOut(OpaqueBackgroundId);
        foreach (var (_, control) in view.Where(n => n.Id.StartsWith(UserCheckPrefix)))
            control.Visible = false;

        settings = kit.Context.Settings;
        var textBackground = layout.Find(TextGroup) is { } textGroup ? view.Get(textGroup, BackgroundId) : null;
        if (textBackground != null) textBackground.Visible = false;
        shade = new ColorRect
        {
            Position = textBackground?.Position ?? Vector2.Zero,
            Size = textBackground?.Size ?? Vector2.Zero,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        (textBackground?.GetParent() ?? (Node)view).AddChild(shade);
        if (textBackground != null) shade.GetParent().MoveChild(shade, textBackground.GetIndex() + 1);
        shadeLevel = settings.GetFloat(SettingShade, DefaultShade);
        shadeIndex = settings.GetInt(SettingShadeIndex, ShadeLevels.Length - 1);
        shadeOn = settings.GetBool(SettingShadeOn, false);
        ApplyShade();
        view.OnPressed("btn_Color", () => { shadeOn = !shadeOn; ApplyShade(); });
        hidden = (ChatCategory)settings.GetInt(SettingHidden, 0) & ~AlwaysShown;
        BuildFilterPanel();
        dragButton = view.Get(OptionButton);

        var textArea = view.Get(TextArea);
        var textNode = layout.Find(TextArea);
        log = new LogText(textNode != null ? UiKit.FontSize(textNode) : DefaultFontSize, textNode?.Color ?? Colors.White, kit.Regular)
        {
            Position = textArea?.Position ?? Vector2.Zero,
            Size = textArea?.Size ?? new Vector2(342, 111),
            MouseFilter = MouseFilterEnum.Pass,
        };
        if (textArea != null) textArea.Visible = false;
        (textArea?.GetParent() ?? (Node)view).AddChild(log);

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            view.OnPressed(scroll, "btn_scroll_Up", () => log.Scroll(ScrollStep));
            foreach (var node in scroll.Children)
                if (node.IsButton && node.Id.Length == 0 && view.ControlOf(node) is BaseButton down)
                {
                    down.Pressed += () => log.Scroll(-ScrollStep);
                    scrollDown = down;
                }
        }

        input = view.Get<LineEdit>(InputId);
        if (input != null)
        {
            input.AddThemeFontOverride("font", kit.Regular);
            input.TextSubmitted += Submit;
            input.FocusEntered += () => game.Chat.SetTyping(true);
            input.FocusExited += () => { game.Chat.SetTyping(false); ShowInput(false); };
            if (layout.Find(InputBackgroundId) is { } row && layout.Find(InputId) is { } edit)
            {
                float rowCentre = edit.Y == 0 ? 0f : input.Position.Y + (row.Y - edit.Y) + row.H * 0.5f;
                Callable.From(() => input.Position = new Vector2(input.Position.X, rowCentre - input.Size.Y * 0.5f)).CallDeferred();
            }
        }
        ShowInput(false);

        var group = new ButtonGroup();
        foreach (var (button, prefix) in ChannelTabs)
        {
            if (view.Get<TextureButton>(button) is not { } tab) continue;
            tab.ToggleMode = true;
            tab.ButtonGroup = group;
            tab.ButtonPressed = prefix.Length == 0;
            string which = prefix;
            tab.Pressed += () => this.prefix = which;
            channels[button] = tab;
        }

        if (view.Get<BaseButton>(WhisperButton) is { } whisper)
        {
            whisper.TooltipText = ItemData.Text(WhisperButtonTipText,
                "You can click on the character and press the 'Private Message' button to start a private chat.");
            whisper.Pressed += StartWhisper;
        }

        baseSize = Size;
        Resized += Relayout;
        Callable.From(CaptureGrow).CallDeferred();
    }

    private void BuildFilterPanel()
    {
        view.OnPressed(FilterButton, () =>
        {
            if (view.Get(FilterPanel) is { } panel) panel.Visible = !panel.Visible;
        });
        foreach (var (button, category) in FilterBoxes)
        {
            if (view.Get<TextureButton>(button) is not { } box) continue;
            box.ToggleMode = true;
            box.ButtonPressed = (hidden & category) == 0;
            var which = category;
            box.Toggled += shown => SetShown(which, shown);
        }
        if (view.Get<BaseButton>(BlockButton) is { } block)
        {
            block.Disabled = true;
            block.TooltipText = UnavailableTip;
        }
        if (view.Get<TextureButton>(LockButton) is { } lockButton)
        {
            lockButton.ToggleMode = true;
            lockButton.ButtonPressed = settings.GetBool(SettingLocked, false);
            lockButton.Toggled += locked =>
            {
                settings.SetBool(SettingLocked, locked);
                if (hudLayout != null) hudLayout.Locked = locked;
            };
        }
    }

    private void SetShown(ChatCategory category, bool shown)
    {
        hidden = shown ? hidden & ~category : hidden | category;
        settings.SetInt(SettingHidden, (int)hidden);
        Reload();
    }

    private void RefreshChannels()
    {
        bool noah = game.Character.NoahKnight;
        SetChannelShown(NoahButton, noah);
        foreach (var button in ClanChannelButtons) SetChannelShown(button, !noah);
        if (view.Get<BaseButton>(NoahFilterBox) is { } box) box.Disabled = !noah;
    }

    private void SetChannelShown(string button, bool shown)
    {
        if (!channels.TryGetValue(button, out var tab)) return;
        tab.Visible = shown;
        if (shown || !tab.ButtonPressed) return;
        prefix = "";
        if (channels.TryGetValue(ChannelTabs[0].Button, out var normal)) normal.ButtonPressed = true;
    }

    private bool Shown(GameChatLine line) => (line.Category & AlwaysShown) != 0 || (line.Category & hidden) == 0;

    private void CaptureGrow()
    {
        var grow = new VerticalGrow();
        grow.Add(view, VerticalGrow.Mode.Stretch);
        foreach (var (node, control) in view.Where(_ => true))
            grow.Add(control, control == scrollDown ? VerticalGrow.Mode.Shift
                : StretchIds.Contains(node.Id) ? VerticalGrow.Mode.Stretch
                : ShiftIds.Contains(node.Id) ? VerticalGrow.Mode.Shift
                : VerticalGrow.Mode.Stay);
        grow.Add(shade, VerticalGrow.Mode.Stretch);
        grow.Add(log, VerticalGrow.Mode.Stretch);
        foreach (var parent in grow.Stretched.Where(c => c != shade && c != log))
            grow.ShiftBottomBand(parent);
        this.grow = grow;
        Relayout();
    }

    private void Relayout()
    {
        if (grow == null || relaying) return;
        relaying = true;
        if (!Mathf.IsEqualApprox(Size.X, baseSize.X)) Size = new Vector2(baseSize.X, Size.Y);
        grow.Apply(Mathf.Max(0f, Size.Y - baseSize.Y));
        relaying = false;
    }

    public override void _EnterTree()
    {
        game.Chat.LineArrived += Append;
        game.Chat.InputRequested += FocusInput;
        game.BecameAvailable += Reload;
        game.Character.Changed += RefreshChannels;
        game.BecameAvailable += RefreshChannels;
        hudLayout ??= HudLayout.Attach(this, LayoutId, dragButton, DefaultPosition, resizable: true,
            defaultSize: baseSize, minimumSize: baseSize, resizeCorner: HudLayout.Corner.TopRight,
            backgroundOpacityChanged: _ => CycleShade());
        hudLayout.Locked = settings.GetBool(SettingLocked, false);
        Reload();
    }

    public override void _ExitTree()
    {
        game.Chat.LineArrived -= Append;
        game.Chat.InputRequested -= FocusInput;
        game.BecameAvailable -= Reload;
        game.Character.Changed -= RefreshChannels;
        game.BecameAvailable -= RefreshChannels;
    }

    public override void _Input(InputEvent ev)
    {
        if (input == null || !input.HasFocus() || ev is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode != Key.Escape) return;
        input.Clear();
        input.ReleaseFocus();
        GetViewport().SetInputAsHandled();
    }

    private void CycleShade()
    {
        shadeIndex = (shadeIndex + 1) % ShadeLevels.Length;
        shadeLevel = ShadeLevels[shadeIndex];
        shadeOn = shadeLevel > 0f;
        settings.SetInt(SettingShadeIndex, shadeIndex);
        ApplyShade();
    }

    private void ApplyShade()
    {
        shade.Color = new Color(0, 0, 0, shadeOn ? shadeLevel : 0f);
        settings.SetFloat(SettingShade, shadeLevel);
        settings.SetBool(SettingShadeOn, shadeOn);
    }

    private void StartWhisper()
    {
        if (game.Target.Has && game.Target.IsPlayer)
        {
            game.Whispers.OpenWith(game.Target.Name);
            return;
        }
        if (input == null) return;
        FocusInput();
        input.Text = WhisperPrefix;
        input.CaretColumn = input.Text.Length;
    }

    private void ShowInput(bool on)
    {
        if (input != null) input.Visible = on;
        foreach (var id in InputRowIds)
            foreach (var (_, control) in view.Where(n => n.Id == id))
                control.Visible = on;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(ScreenMargin, room.Y - BottomBarHeight - Size.Y - ScreenMargin);
    }

    private void Reload() => log.Set(game.Chat.Lines.Where(Shown).Select(line => line.BbCode));

    private void Append(GameChatLine line)
    {
        if (Shown(line)) log.Append(line.BbCode);
    }

    private void FocusInput()
    {
        if (input == null) return;
        ShowInput(true);
        input.GrabFocus();
        input.CaretColumn = input.Text.Length;
    }

    private void Submit(string text)
    {
        if (input == null) return;
        input.Clear();
        input.ReleaseFocus();
        text = text.Trim();
        if (text.Length == 0) return;
        bool hasPrefix = PrefixChars.Contains(text[0]);
        game.Chat.Send(hasPrefix || prefix.Length == 0 ? text : prefix + text);
    }
}
