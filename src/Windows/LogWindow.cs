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

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly PluginSettings _settings;
    private readonly LogText _log;
    private readonly ColorRect _shade;
    private readonly Control? _dragButton;
    private HudLayout? _hudLayout;
    private int _shadeIndex;
    private GameLogKind? _filter;

    public LogWindow()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(_view);
        _settings = kit.Context.Settings;
        _view.Hide("img_sizechange", "text_version");
        _view.FadeOut(OpaqueBackgroundId, BackgroundId);
        var background = _view.Get(BackgroundId);
        _shade = new ColorRect
        {
            Position = background?.Position ?? Vector2.Zero,
            Size = background?.Size ?? layout.SizeVec,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        (background?.GetParent() ?? (Node)_view).AddChild(_shade);
        if (background != null) _shade.GetParent().MoveChild(_shade, background.GetIndex() + 1);
        _shadeIndex = _settings.GetInt(SettingShadeIndex, 1);
        SetShade(_settings.GetFloat(SettingShade, DefaultShade));
        _dragButton = _view.Get(OptionButton);

        var area = _view.Get(TextId);
        var node = layout.Find(TextId);
        _log = new LogText(node != null ? UiKit.FontSize(node) : DefaultFontSize, node?.Color ?? Colors.White, kit.Regular)
        {
            Position = area?.Position ?? Vector2.Zero,
            Size = area?.Size ?? new Vector2(250, 133),
            MouseFilter = MouseFilterEnum.Stop,
        };
        if (area != null) area.Visible = false;
        (area?.GetParent() ?? (Node)_view).AddChild(_log);

        var group = new ButtonGroup();
        foreach (var (button, kind) in Tabs)
        {
            if (_view.Get<TextureButton>(button) is { } tab)
            {
                tab.ToggleMode = true;
                tab.ButtonGroup = group;
                tab.ButtonPressed = kind == null;
            }
            var which = kind;
            _view.OnPressed(button, () => { _filter = which; Reload(); });
        }
    }

    public override void _EnterTree()
    {
        _game.Log.LineAdded += OnLine;
        _game.BecameAvailable += Reload;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _dragButton, DefaultPosition, backgroundOpacityChanged: _ => CycleShade());
        Reload();
    }

    public override void _ExitTree()
    {
        _game.Log.LineAdded -= OnLine;
        _game.BecameAvailable -= Reload;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - Size.X - ScreenMargin, room.Y - BottomBarHeight - Size.Y - GapAboveBar);
    }

    private void CycleShade()
    {
        _shadeIndex = (_shadeIndex + 1) % ShadeLevels.Length;
        _settings.SetInt(SettingShadeIndex, _shadeIndex);
        SetShade(ShadeLevels[_shadeIndex]);
    }

    private void SetShade(float level)
    {
        _shade.Color = new Color(0, 0, 0, level);
        _settings.SetFloat(SettingShade, level);
    }

    private void Reload() => _log.Set(_game.Log.History.Where(Matches).Select(Format));

    private void OnLine(GameLogLine line)
    {
        if (Matches(line)) _log.Append(Format(line));
    }

    private bool Matches(GameLogLine line) => _filter == null || line.Kind == _filter;

    private static string Format(GameLogLine line) =>
        $"[color=#{line.Color.ToHtml(false)}]{line.Text.Replace("[", "[lb]")}[/color]";
}
