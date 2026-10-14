using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class UserVoteSelectWindow : Control
{
    private const string LayoutName = "re_uservoteselect";
    private const string SelectGroup = "VoteSelect";
    private const string MessageGroup = "VoteMessage";
    private const int TitleBarHeight = 50;

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly LayoutNode? group;
    private int reason = 1;

    public UserVoteSelectWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        view.Hide(MessageGroup);
        group = layout.Find(SelectGroup);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        if (group == null) return;
        for (int i = 0; i < UserVote.Reasons; i++)
        {
            int reason = i + 1;
            if (view.Get<BaseButton>(group, "btn_" + i) is { } choice)
            {
                choice.ToggleMode = true;
                choice.Pressed += () => Choose(reason);
            }
        }
        if (view.Get<LineEdit>(group, "edit") is { } edit)
        {
            edit.MaxLength = UserVote.OtherTextMaxLetters;
            edit.TextChanged += _ => Choose(UserVote.ReasonOther);
        }
        view.OnPressed(group, "btn_ok", () => game.UserVote.Send(reason, view.Get<LineEdit>(group, "edit")?.Text ?? ""));
        view.OnPressed(group, "btn_cancel", () => game.UserVote.CancelSelect());
    }

    public override void _EnterTree()
    {
        game.UserVote.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => game.UserVote.Changed -= Refresh;

    private void Choose(int reason)
    {
        this.reason = reason;
        if (group == null) return;
        for (int i = 0; i < UserVote.Reasons; i++)
            if (view.Get<BaseButton>(group, "btn_" + i) is { } choice) choice.SetPressedNoSignal(i + 1 == reason);
    }

    private void Refresh()
    {
        if (group == null) return;
        var vote = game.UserVote;
        if (view.Get<Label>(group, "txt_title") is { } title) title.Text = vote.SelectPrompt;
        if (view.Get<Label>(group, "warning") is { } warning) warning.Text = vote.CostLine;
        var reasons = vote.Reasons;
        for (int i = 0; i < UserVote.Reasons; i++)
            if (view.Get<Label>(group, "text_" + i) is { } line) line.Text = i < reasons.Count ? reasons[i] : "";
        Choose(reason);
    }
}

public partial class UserVoteAskWindow : Control
{
    private const string LayoutName = "re_uservoteselect";
    private const string SelectGroup = "VoteSelect";
    private const string MessageGroup = "VoteMessage";
    private const int TitleBarHeight = 50;
    private const int Width = 365;
    private const int Height = 276;

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly LayoutNode? group;

    public UserVoteAskWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        view.Hide(SelectGroup);
        CustomMinimumSize = new Vector2(Width, Height);
        Size = CustomMinimumSize;
        view.CustomMinimumSize = CustomMinimumSize;
        view.Size = CustomMinimumSize;
        group = layout.Find(MessageGroup);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, Width, TitleBarHeight)));
        if (group == null) return;
        view.OnPressed(group, "btn_yes", () => game.UserVote.Answer(UserVote.AnswerYes));
        view.OnPressed(group, "btn_draw", () => game.UserVote.Answer(UserVote.AnswerDraw));
        view.OnPressed(group, "btn_no", () => game.UserVote.Answer(UserVote.AnswerNo));
    }

    public override void _EnterTree()
    {
        game.UserVote.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => game.UserVote.Changed -= Refresh;

    public override void _Process(double delta)
    {
        if (group == null || view.Get<TextureProgressBar>(group, "durationtime") is not { } bar) return;
        var vote = game.UserVote;
        bar.Value = vote.AskSeconds > 0 ? vote.AskSecondsLeft / vote.AskSeconds * bar.MaxValue : 0;
    }

    private void Refresh()
    {
        if (group == null) return;
        var vote = game.UserVote;
        if (view.Get<Label>(group, "txt_title") is { } title) title.Text = vote.Title;
        if (view.Get<Label>(group, "txt_message") is { } message)
        {
            message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            message.Text = vote.AskReason.Length > 0 ? $"{vote.AskQuestion}\n\n{vote.AskReason}" : vote.AskQuestion;
        }
    }
}

public partial class ReportNoticeWindow : Control
{
    private const string LayoutName = "re_reportnotice";
    private const int TitleBarHeight = 60;
    private static readonly string[] Lines = { "notice_0", "notice_1", "notice_2" };

    private readonly PluginGame game;
    private readonly LayoutView view;

    public ReportNoticeWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        view.OnPressed("Btn_Ok", () => game.UserVote.CloseNotice());
        foreach (var id in Lines)
            if (view.Get<Label>(id) is { } line) line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    public override void _EnterTree()
    {
        game.UserVote.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => game.UserVote.Changed -= Refresh;

    private void Refresh()
    {
        var vote = game.UserVote;
        view.SetText("default_0", vote.NoticeTitle);
        var lines = vote.NoticeLines;
        for (int i = 0; i < Lines.Length; i++) view.SetText(Lines[i], i < lines.Count ? lines[i] : "");
    }
}
