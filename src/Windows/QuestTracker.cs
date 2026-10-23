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
    private const string OpenQuestTip = "Open this quest in the journal";
    private const float ScreenMargin = 8f;
    private const float RowGap = 8f;
    private const int ObjectiveFontSize = 11;
    private const float ObjectiveRowHeight = 16f;
    private const float ObjectiveLeft = 23f;
    private const float ObjectiveWidth = 178f;
    private const float BackgroundBottomInset = 2f;
    private static readonly Color ObjectiveColor = Colors.White;
    private static readonly Color DoneColor = new(0.6f, 1f, 0.6f);
    private static readonly Color ShadowColor = new(0, 0, 0, 0.75f);

    private readonly LayoutView view;
    private readonly LayoutNode layout;
    private readonly PluginGame game;
    private readonly Control? background;
    private readonly Control? pageBar;
    private readonly Control? listBar;
    private const string LayoutId = "theme_quest_tip";

    private readonly List<Label> objectives = new();
    private HudLayout? hudLayout;
    private readonly float listBottom;
    private readonly float backgroundTop;
    private readonly float pageBarHeight;
    private readonly float titleHeight;
    private int page;
    private bool closed;
    private bool minimised;
    private bool objectivesOpen = true;

    public QuestTracker()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(view);
        background = view.Get(BackgroundId);
        pageBar = view.Get(PageBarId);
        listBar = view.Get(ListBarId);
        var list = layout.Find(ListBarId);
        var pageNode = layout.Find(PageBarId);
        var title = layout.Find(TitleBarId);
        listBottom = list != null ? list.Y + list.H - layout.Y : 0f;
        backgroundTop = background?.Position.Y ?? 0f;
        pageBarHeight = pageNode?.H ?? 0f;
        titleHeight = title != null ? title.Y + title.H - layout.Y : 0f;

        view.OnPressed("btn_close", () => { closed = true; Visible = false; });
        view.OnPressed("btn_mini", () => SetMinimised(true));
        view.OnPressed("btn_max", () => SetMinimised(false));
        view.OnPressed("btn_list_title_open", () => SetObjectivesOpen(true));
        view.OnPressed("btn_list_title_close", () => SetObjectivesOpen(false));
        view.OnPressed("btn_prevpage", () => Turn(-1));
        view.OnPressed("btn_nextpage", () => Turn(1));
        view.OnPressed("btn_change", () => game.Windows.Toggle(QuestsWindow));
        view.OnPressed("btn_seedopen", () => game.Windows.Toggle(QuestsWindow));
        if (view.Get(QuestTitleId) is { } questTitle)
        {
            questTitle.MouseFilter = MouseFilterEnum.Stop;
            questTitle.MouseDefaultCursorShape = CursorShape.PointingHand;
            questTitle.TooltipText = OpenQuestTip;
            questTitle.GuiInput += input =>
            {
                if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
                questTitle.AcceptEvent();
                OpenShownQuest();
            };
        }
        SetMinimised(false);
        SetObjectivesOpen(true);
    }

    public override void _EnterTree()
    {
        game.Quests.Changed += Refresh;
        game.BecameAvailable += Refresh;
        game.Windows.TopRightRowMoved += FollowRow;
        hudLayout ??= HudLayout.Attach(this, LayoutId, view.Get(TitleBarId), DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Quests.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
        game.Windows.TopRightRowMoved -= FollowRow;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        float left = room.X - layout.W - ScreenMargin;
        var row = game.Windows.TopRightRow;
        bool covered = row.HasArea() && row.Position.X < left + layout.W && row.End.X > left;
        return new Vector2(left, covered ? Mathf.Max(ScreenMargin, row.End.Y + RowGap) : ScreenMargin);
    }

    private void FollowRow() => hudLayout?.ReapplyDefault();

    private void OpenShownQuest()
    {
        var quests = game.Quests.Tracked;
        if (quests.Count == 0) return;
        game.Quests.Open(quests[Mathf.Clamp(page, 0, quests.Count - 1)].QuestId);
    }

    private void Turn(int delta)
    {
        int count = game.Quests.Tracked.Count;
        if (count == 0) return;
        page = ((page + delta) % count + count) % count;
        Refresh();
    }

    private void SetMinimised(bool minimised)
    {
        this.minimised = minimised;
        view.Hide(minimised ? "btn_mini" : "btn_max");
        if (view.Get(minimised ? "btn_max" : "btn_mini") is { } shown) shown.Visible = true;
        Refresh();
    }

    private void SetObjectivesOpen(bool open)
    {
        objectivesOpen = open;
        view.Hide(open ? "btn_list_title_open" : "btn_list_title_close");
        if (view.Get(open ? "btn_list_title_close" : "btn_list_title_open") is { } shown) shown.Visible = true;
        Refresh();
    }

    private void Refresh()
    {
        var quests = game.Quests.Tracked;
        Visible = !closed && quests.Count > 0;
        foreach (var label in objectives) label.QueueFree();
        objectives.Clear();
        if (quests.Count == 0) return;
        page = Mathf.Clamp(page, 0, quests.Count - 1);
        var quest = quests[page];
        view.SetText(TitleCountId, quests.Count.ToString());
        view.SetClippedText(QuestTitleId, quest.Title);
        view.SetText(PageNumberId, (page + 1).ToString());

        float bottom = listBottom;
        if (objectivesOpen && !minimised)
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
                view.AddChild(label);
                objectives.Add(label);
                bottom += ObjectiveRowHeight;
            }
        }
        Layout(bottom);
    }

    private void Layout(float contentBottom)
    {
        bool body = !minimised;
        if (background != null) background.Visible = body;
        if (pageBar != null) pageBar.Visible = body;
        if (listBar != null) listBar.Visible = body;
        if (!body)
        {
            Size = new Vector2(layout.W, titleHeight);
            return;
        }
        if (pageBar != null) pageBar.Position = new Vector2(pageBar.Position.X, contentBottom);
        if (background != null)
            background.Size = new Vector2(background.Size.X, contentBottom + pageBarHeight - backgroundTop - BackgroundBottomInset);
        Size = new Vector2(layout.W, contentBottom + pageBarHeight);
    }
}
