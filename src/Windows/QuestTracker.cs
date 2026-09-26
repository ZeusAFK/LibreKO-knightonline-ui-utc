using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class QuestTracker : Control
{
    private const string LayoutName = "re_quest_mini_tip";
    private const string BackgroundId = "img_bakground";
    private const string TitleCountId = "str_title";
    private const string QuestTitleId = "szText";
    private const string PageBarId = "MiniBar_Page";
    private const string ListBarId = "MiniBar_List";
    private const string TitleBarId = "MiniBar_Title";
    private const string PageNumberId = "str_pagenum";
    private const string QuestsWindow = "quests";
    private const float ScreenMargin = 8f;
    private const int ObjectiveFontSize = 11;
    private const float ObjectiveRowHeight = 16f;
    private const float ObjectiveLeft = 23f;
    private const float ObjectiveWidth = 178f;
    private const float BackgroundBottomInset = 2f;
    private static readonly Color ObjectiveColor = Colors.White;
    private static readonly Color DoneColor = new(0.6f, 1f, 0.6f);
    private static readonly Color ShadowColor = new(0, 0, 0, 0.75f);

    private readonly LayoutView _view;
    private readonly LayoutNode _layout;
    private readonly PluginGame _game;
    private readonly Control? _background;
    private readonly Control? _pageBar;
    private readonly Control? _listBar;
    private const string LayoutId = "theme_quest_tip";

    private readonly List<Label> _objectives = new();
    private HudLayout? _hudLayout;
    private readonly float _listBottom;
    private readonly float _backgroundTop;
    private readonly float _pageBarHeight;
    private readonly float _titleHeight;
    private int _page;
    private bool _closed;
    private bool _minimised;
    private bool _objectivesOpen = true;

    public QuestTracker()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, _layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = _layout.SizeVec;
        AddChild(_view);
        _background = _view.Get(BackgroundId);
        _pageBar = _view.Get(PageBarId);
        _listBar = _view.Get(ListBarId);
        var list = _layout.Find(ListBarId);
        var page = _layout.Find(PageBarId);
        var title = _layout.Find(TitleBarId);
        _listBottom = list != null ? list.Y + list.H - _layout.Y : 0f;
        _backgroundTop = _background?.Position.Y ?? 0f;
        _pageBarHeight = page?.H ?? 0f;
        _titleHeight = title != null ? title.Y + title.H - _layout.Y : 0f;

        _view.OnPressed("btn_close", () => { _closed = true; Visible = false; });
        _view.OnPressed("btn_mini", () => SetMinimised(true));
        _view.OnPressed("btn_max", () => SetMinimised(false));
        _view.OnPressed("btn_list_title_open", () => SetObjectivesOpen(true));
        _view.OnPressed("btn_list_title_close", () => SetObjectivesOpen(false));
        _view.OnPressed("btn_prevpage", () => Turn(-1));
        _view.OnPressed("btn_nextpage", () => Turn(1));
        _view.OnPressed("btn_change", () => _game.Windows.Toggle(QuestsWindow));
        _view.OnPressed("btn_seedopen", () => _game.Windows.Toggle(QuestsWindow));
        SetMinimised(false);
        SetObjectivesOpen(true);
    }

    public override void _EnterTree()
    {
        _game.Quests.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _view.Get(TitleBarId), DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Quests.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - _layout.W - ScreenMargin, ScreenMargin);
    }

    private void Turn(int delta)
    {
        int count = _game.Quests.Tracked.Count;
        if (count == 0) return;
        _page = ((_page + delta) % count + count) % count;
        Refresh();
    }

    private void SetMinimised(bool minimised)
    {
        _minimised = minimised;
        _view.Hide(minimised ? "btn_mini" : "btn_max");
        if (_view.Get(minimised ? "btn_max" : "btn_mini") is { } shown) shown.Visible = true;
        Refresh();
    }

    private void SetObjectivesOpen(bool open)
    {
        _objectivesOpen = open;
        _view.Hide(open ? "btn_list_title_open" : "btn_list_title_close");
        if (_view.Get(open ? "btn_list_title_close" : "btn_list_title_open") is { } shown) shown.Visible = true;
        Refresh();
    }

    private void Refresh()
    {
        var quests = _game.Quests.Tracked;
        Visible = !_closed && quests.Count > 0;
        foreach (var label in _objectives) label.QueueFree();
        _objectives.Clear();
        if (quests.Count == 0) return;
        _page = Mathf.Clamp(_page, 0, quests.Count - 1);
        var quest = quests[_page];
        _view.SetText(TitleCountId, quests.Count.ToString());
        _view.SetClippedText(QuestTitleId, quest.Title);
        _view.SetText(PageNumberId, (_page + 1).ToString());

        float bottom = _listBottom;
        if (_objectivesOpen && !_minimised)
        {
            foreach (var line in quest.Objectives)
            {
                var label = new Label
                {
                    Text = line,
                    Position = new Vector2(ObjectiveLeft, bottom),
                    Size = new Vector2(ObjectiveWidth, ObjectiveRowHeight),
                    ClipText = true,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                label.AddThemeFontSizeOverride("font_size", ObjectiveFontSize);
                label.AddThemeFontOverride("font", Plugin.Kit.Regular);
                label.AddThemeColorOverride("font_color", quest.ReadyToTurnIn ? DoneColor : ObjectiveColor);
                label.AddThemeColorOverride("font_shadow_color", ShadowColor);
                label.AddThemeConstantOverride("shadow_offset_x", 1);
                label.AddThemeConstantOverride("shadow_offset_y", 1);
                _view.AddChild(label);
                _objectives.Add(label);
                bottom += ObjectiveRowHeight;
            }
        }
        Layout(bottom);
    }

    private void Layout(float contentBottom)
    {
        bool body = !_minimised;
        if (_background != null) _background.Visible = body;
        if (_pageBar != null) _pageBar.Visible = body;
        if (_listBar != null) _listBar.Visible = body;
        if (!body)
        {
            Size = new Vector2(_layout.W, _titleHeight);
            return;
        }
        if (_pageBar != null) _pageBar.Position = new Vector2(_pageBar.Position.X, contentBottom);
        if (_background != null)
            _background.Size = new Vector2(_background.Size.X, contentBottom + _pageBarHeight - _backgroundTop - BackgroundBottomInset);
        Size = new Vector2(_layout.W, contentBottom + _pageBarHeight);
    }
}
