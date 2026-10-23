using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class SchedulerWidget : Control
{
    private const string LayoutName = "re_schedulerbutton";
    private const string LayoutId = "theme_scheduler";
    private const string QuestTrackerLayout = "re_quest_mini_tip";
    private const string ScrollBarType = "scrollbar";
    private const string EmptyText = "No more events today";
    private const string OpenTip = "Open the events window";
    private const string FoldTip = "Show or hide today's events";
    private const float ScreenMargin = 8f;
    private const float RowGap = 6f;
    private const double RefreshSeconds = 30;
    private static readonly Color OpenColor = new(1f, 0.85f, 0.35f);
    private static readonly Color LaterColor = Colors.White;

    private readonly LayoutNode layout;
    private readonly PluginGame game;
    private readonly Control? panel;
    private readonly Control? toggle;
    private readonly List<Label> rows = new();
    private HudLayout? hudLayout;
    private int first;
    private bool folded;
    private double refreshIn;

    public SchedulerWidget()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        layout = kit.Layout(LayoutName);
        var view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = layout.SizeVec;
        AddChild(view);

        var panelNode = layout.Children.FirstOrDefault(n => n.All().Any(c => c.IsString));
        panel = panelNode != null ? view.ControlOf(panelNode) : null;
        var toggleNode = layout.Children.FirstOrDefault(n => n.IsButton);
        toggle = toggleNode != null ? view.ControlOf(toggleNode) : null;
        if (toggle is BaseButton foldButton)
        {
            foldButton.TooltipText = FoldTip;
            foldButton.Pressed += () => SetFolded(!folded);
        }

        if (panelNode != null)
        {
            foreach (var node in panelNode.All().Where(n => n.IsString).OrderBy(n => n.Y))
                if (view.ControlOf(node) is Label row)
                {
                    row.Text = "";
                    row.ClipText = true;
                    row.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    row.MouseFilter = MouseFilterEnum.Stop;
                    row.MouseDefaultCursorShape = CursorShape.PointingHand;
                    row.TooltipText = OpenTip;
                    row.GuiInput += input =>
                    {
                        if (input is not InputEventMouseButton { Pressed: true } click) return;
                        if (click.ButtonIndex == MouseButton.Left) game.Events.OpenWindow();
                        else if (click.ButtonIndex == MouseButton.WheelUp) Scroll(-1);
                        else if (click.ButtonIndex == MouseButton.WheelDown) Scroll(1);
                        row.AcceptEvent();
                    };
                    rows.Add(row);
                }
            var arrows = panelNode.All().Where(n => n.Type == ScrollBarType).SelectMany(n => n.Children)
                .Where(n => n.IsButton).OrderBy(n => n.Y).ToList();
            if (arrows.Count == 2)
            {
                if (view.ControlOf(arrows[0]) is BaseButton up) up.Pressed += () => Scroll(-1);
                if (view.ControlOf(arrows[1]) is BaseButton down) down.Pressed += () => Scroll(1);
            }
        }
    }

    public override void _EnterTree()
    {
        game.Events.Changed += Refresh;
        game.BecameAvailable += Refresh;
        game.Windows.TopRightRowMoved += FollowRow;
        hudLayout ??= HudLayout.Attach(this, LayoutId, toggle, DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Events.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
        game.Windows.TopRightRowMoved -= FollowRow;
    }

    public override void _Process(double delta)
    {
        refreshIn -= delta;
        if (refreshIn > 0) return;
        refreshIn = RefreshSeconds;
        Refresh();
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        float trackerWidth = Plugin.Kit.Layout(QuestTrackerLayout).W;
        float left = Mathf.Max(ScreenMargin, room.X - ScreenMargin - trackerWidth - RowGap - layout.W);
        var row = game.Windows.TopRightRow;
        bool covered = row.HasArea() && row.Position.X < left + layout.W && row.End.X > left;
        return new Vector2(left, covered ? Mathf.Max(ScreenMargin, row.End.Y + RowGap) : ScreenMargin);
    }

    private void FollowRow() => hudLayout?.ReapplyDefault();

    private void SetFolded(bool folded)
    {
        this.folded = folded;
        if (panel != null) panel.Visible = !folded;
    }

    private void Scroll(int step)
    {
        first += step;
        Refresh();
    }

    private void Refresh()
    {
        var today = game.Events.Today;
        first = Mathf.Clamp(first, 0, Mathf.Max(0, today.Count - rows.Count));
        for (int i = 0; i < rows.Count; i++)
        {
            int index = first + i;
            if (index >= today.Count)
            {
                rows[i].Text = today.Count == 0 && i == 0 ? EmptyText : "";
                continue;
            }
            var line = today[index];
            rows[i].Text = $"{line.When}  {line.Name}";
            rows[i].AddThemeColorOverride("font_color", line.Open ? OpenColor : LaterColor);
        }
    }
}
