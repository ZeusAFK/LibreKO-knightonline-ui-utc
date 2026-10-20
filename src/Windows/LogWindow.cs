using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class LogWindow : Control
{
    private const string LayoutName = "re_information_box";
    private const string TextId = "text_message";
    private const string OpaqueBackgroundId = "img_background2";
    private const float BottomBarHeight = 108f;
    private const float ScreenMargin = 8f;
    private const float GapAboveBar = 4f;
    private const int DefaultFontSize = 11;

    private static readonly (string Button, GameLogKind? Kind)[] Tabs =
    {
        ("btn_all", null),
        ("btn_state", GameLogKind.Status),
        ("btn_item", GameLogKind.Item),
        ("btn_system", GameLogKind.System),
    };

    private const string LayoutId = "theme_log";
    private const string SettingShade = "log.shade";
    private const string BackgroundId = "img_background";
    private const string OptionButton = "btn_option";
    private const string SettingShadeIndex = "log.shadeIndex";
    private static readonly float[] ShadeLevels = { 0.75f, 0.5f, 0.25f, 0f };
    private const float DefaultShade = 0.45f;
    private const string ScrollGroup = "scroll";
    private const string ScrollUpId = "btn_scroll_Up";
    private const string ScrollTrackId = "scroll_bar";
    private const string LockButtonId = "btn_msgwnd_lock";
    private const string SettingLocked = "log.locked";
    private const float ScrollStep = 32f;
    private const double RepeatDelay = 0.4;
    private const double RepeatEvery = 0.08;

    private readonly LayoutView view;
    private readonly PluginGame game;
    private readonly PluginSettings settings;
    private readonly LogText log;
    private readonly ColorRect shade;
    private readonly Control? dragButton;
    private readonly Vector2 baseSize;
    private readonly LayoutNode layoutRoot;
    private VerticalGrow? grow;
    private HorizontalGrow? widen;
    private bool relaying;
    private HudLayout? hudLayout;

    private static readonly HashSet<string> StretchIds = new() { "base_msgwnd", "img_background", "img_background2", "scroll", "scroll_bar", "img_scrollback", "text_message", "base_area" };

    private static readonly HashSet<string> ShiftIds = new() { "btn_msgwnd_lock", "btn_item", "btn_state", "btn_system", "btn_all", "text_version", "btn_option" };

    private static readonly HashSet<string> WidenIds = new() { "base_msgwnd", "img_background", "img_background2", "text_message", "base_area" };

    private static readonly HashSet<string> RightIds = new() { "scroll", "btn_msgwnd_lock", "btn_option" };

    private static readonly Vector2 MaximumScreenShare = new(0.3f, 0.7f);
    private int shadeIndex;
    private GameLogKind? filter;

    public LogWindow()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        layoutRoot = layout;
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(view);
        settings = kit.Context.Settings;
        view.Hide("img_sizechange", "text_version");
        view.FadeOut(OpaqueBackgroundId, BackgroundId);
        var background = view.Get(BackgroundId);
        shade = new ColorRect
        {
            Position = background?.Position ?? Vector2.Zero,
            Size = background?.Size ?? layout.SizeVec,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        (background?.GetParent() ?? (Node)view).AddChild(shade);
        if (background != null) shade.GetParent().MoveChild(shade, background.GetIndex() + 1);
        shadeIndex = settings.GetInt(SettingShadeIndex, 1);
        SetShade(settings.GetFloat(SettingShade, DefaultShade));
        dragButton = view.Get(OptionButton);

        var area = view.Get(TextId);
        var node = layout.Find(TextId);
        log = new LogText(node != null ? UiKit.FontSize(node) : DefaultFontSize, node?.Color ?? Colors.White, kit.Regular)
        {
            Position = area?.Position ?? Vector2.Zero,
            Size = area?.Size ?? new Vector2(250, 133),
            MouseFilter = MouseFilterEnum.Stop,
        };
        if (area != null) area.Visible = false;
        (area?.GetParent() ?? (Node)view).AddChild(log);

        if (layout.Find(ScrollGroup) is { } scroll)
        {
            foreach (var child in scroll.Children)
                if (child.IsButton && view.ControlOf(child) is BaseButton button)
                    HoldRepeat(button, child.Id == ScrollUpId ? ScrollStep : -ScrollStep);
        }
        if (view.Get(ScrollTrackId) is { } track)
        {
            track.MouseFilter = MouseFilterEnum.Stop;
            track.GuiInput += input =>
            {
                if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
                log.Scroll(click.Position.Y < track.Size.Y * 0.5f ? log.Size.Y : -log.Size.Y);
                track.AcceptEvent();
            };
        }
        if (view.Get<TextureButton>(LockButtonId) is { } lockButton)
        {
            lockButton.ToggleMode = true;
            lockButton.ButtonPressed = settings.GetBool(SettingLocked, false);
            lockButton.Toggled += locked =>
            {
                settings.SetBool(SettingLocked, locked);
                if (hudLayout != null) hudLayout.Locked = locked;
            };
        }

        var group = new ButtonGroup();
        foreach (var (button, kind) in Tabs)
        {
            if (view.Get<TextureButton>(button) is { } tab)
            {
                tab.ToggleMode = true;
                tab.ButtonGroup = group;
                tab.ButtonPressed = kind == null;
            }
            var which = kind;
            view.OnPressed(button, () => { filter = which; Reload(); });
        }

        baseSize = Size;
        Resized += Relayout;
        Callable.From(CaptureGrow).CallDeferred();
    }

    private void CaptureGrow()
    {
        var grow = new VerticalGrow();
        grow.Add(view, VerticalGrow.Mode.Stretch);
        var scrollDown = layoutRoot.Find("scroll") is { } scroll
            ? scroll.Children.Where(n => n.IsButton && n.Id.Length == 0).Select(n => view.ControlOf(n)).FirstOrDefault()
            : null;
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

        var widen = new HorizontalGrow();
        widen.Add(view, HorizontalGrow.Mode.Stretch);
        foreach (var (node, control) in view.Where(_ => true))
            widen.Add(control, WidenIds.Contains(node.Id) ? HorizontalGrow.Mode.Stretch
                : RightIds.Contains(node.Id) ? HorizontalGrow.Mode.Shift
                : HorizontalGrow.Mode.Stay);
        widen.Add(shade, HorizontalGrow.Mode.Stretch);
        widen.Add(log, HorizontalGrow.Mode.Stretch);
        this.widen = widen;
        Relayout();
    }

    private void Relayout()
    {
        if (grow == null || relaying) return;
        relaying = true;
        grow.Apply(Mathf.Max(0f, Size.Y - baseSize.Y));
        widen?.Apply(Mathf.Max(0f, Size.X - baseSize.X));
        relaying = false;
    }

    public override void _EnterTree()
    {
        game.Log.LineAdded += OnLine;
        game.BecameAvailable += Reload;
        hudLayout ??= HudLayout.Attach(this, LayoutId, dragButton, DefaultPosition, resizable: true,
            defaultSize: baseSize, minimumSize: baseSize, resizeCorner: HudLayout.Corner.TopLeft,
            backgroundOpacityChanged: _ => CycleShade());
        hudLayout.MaximumViewportShare = MaximumScreenShare;
        hudLayout.Locked = settings.GetBool(SettingLocked, false);
        Reload();
    }

    private void HoldRepeat(BaseButton button, float step)
    {
        var timer = new Godot.Timer { OneShot = false };
        AddChild(timer);
        button.ButtonDown += () =>
        {
            log.Scroll(step);
            timer.Start(RepeatDelay);
        };
        button.ButtonUp += timer.Stop;
        timer.Timeout += () =>
        {
            log.Scroll(step);
            timer.Start(RepeatEvery);
        };
    }

    public override void _ExitTree()
    {
        game.Log.LineAdded -= OnLine;
        game.BecameAvailable -= Reload;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - Size.X - ScreenMargin, room.Y - BottomBarHeight - Size.Y - GapAboveBar);
    }

    private void CycleShade()
    {
        shadeIndex = (shadeIndex + 1) % ShadeLevels.Length;
        settings.SetInt(SettingShadeIndex, shadeIndex);
        SetShade(ShadeLevels[shadeIndex]);
    }

    private void SetShade(float level)
    {
        shade.Color = new Color(0, 0, 0, level);
        settings.SetFloat(SettingShade, level);
    }

    private void Reload() => log.Set(game.Log.History.Where(Matches).Select(Format));

    private void OnLine(GameLogLine line)
    {
        if (Matches(line)) log.Append(Format(line));
    }

    private bool Matches(GameLogLine line) => filter == null || line.Kind == filter;

    private static string Format(GameLogLine line) =>
        $"[color=#{line.Color.ToHtml(false)}]{line.Text.Replace("[", "[lb]")}[/color]";
}
