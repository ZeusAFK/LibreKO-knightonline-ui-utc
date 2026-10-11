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

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly LayoutNode? _group;
    private int _reason = 1;

    public UserVoteSelectWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        _view.Hide(MessageGroup);
        _group = layout.Find(SelectGroup);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        if (_group == null) return;
        for (int i = 0; i < UserVote.Reasons; i++)
        {
            int reason = i + 1;
            if (_view.Get<BaseButton>(_group, "btn_" + i) is { } choice)
            {
                choice.ToggleMode = true;
                choice.Pressed += () => Choose(reason);
            }
        }
        if (_view.Get<LineEdit>(_group, "edit") is { } edit)
        {
            edit.MaxLength = UserVote.OtherTextMaxLetters;
            edit.TextChanged += _ => Choose(UserVote.ReasonOther);
        }
        _view.OnPressed(_group, "btn_ok", () => _game.UserVote.Send(_reason, _view.Get<LineEdit>(_group, "edit")?.Text ?? ""));
        _view.OnPressed(_group, "btn_cancel", () => _game.UserVote.CancelSelect());
    }

    public override void _EnterTree()
    {
        _game.UserVote.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _game.UserVote.Changed -= Refresh;

    private void Choose(int reason)
    {
        _reason = reason;
        if (_group == null) return;
        for (int i = 0; i < UserVote.Reasons; i++)
            if (_view.Get<BaseButton>(_group, "btn_" + i) is { } choice) choice.SetPressedNoSignal(i + 1 == reason);
    }

    private void Refresh()
    {
        if (_group == null) return;
        var vote = _game.UserVote;
        if (_view.Get<Label>(_group, "txt_title") is { } title) title.Text = vote.SelectPrompt;
        if (_view.Get<Label>(_group, "warning") is { } warning) warning.Text = vote.CostLine;
        var reasons = vote.Reasons;
        for (int i = 0; i < UserVote.Reasons; i++)
            if (_view.Get<Label>(_group, "text_" + i) is { } line) line.Text = i < reasons.Count ? reasons[i] : "";
        Choose(_reason);
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

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly LayoutNode? _group;

    public UserVoteAskWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        _view.Hide(SelectGroup);
        CustomMinimumSize = new Vector2(Width, Height);
        Size = CustomMinimumSize;
        _view.CustomMinimumSize = CustomMinimumSize;
        _view.Size = CustomMinimumSize;
        _group = layout.Find(MessageGroup);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, Width, TitleBarHeight)));
        if (_group == null) return;
        _view.OnPressed(_group, "btn_yes", () => _game.UserVote.Answer(UserVote.AnswerYes));
        _view.OnPressed(_group, "btn_draw", () => _game.UserVote.Answer(UserVote.AnswerDraw));
        _view.OnPressed(_group, "btn_no", () => _game.UserVote.Answer(UserVote.AnswerNo));
    }

    public override void _EnterTree()
    {
        _game.UserVote.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _game.UserVote.Changed -= Refresh;

    public override void _Process(double delta)
    {
        if (_group == null || _view.Get<TextureProgressBar>(_group, "durationtime") is not { } bar) return;
        var vote = _game.UserVote;
        bar.Value = vote.AskSeconds > 0 ? vote.AskSecondsLeft / vote.AskSeconds * bar.MaxValue : 0;
    }

    private void Refresh()
    {
        if (_group == null) return;
        var vote = _game.UserVote;
        if (_view.Get<Label>(_group, "txt_title") is { } title) title.Text = vote.Title;
        if (_view.Get<Label>(_group, "txt_message") is { } message)
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

    private readonly PluginGame _game;
    private readonly LayoutView _view;

    public ReportNoticeWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        _view.OnPressed("Btn_Ok", () => _game.UserVote.CloseNotice());
        foreach (var id in Lines)
            if (_view.Get<Label>(id) is { } line) line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    public override void _EnterTree()
    {
        _game.UserVote.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _game.UserVote.Changed -= Refresh;

    private void Refresh()
    {
        var vote = _game.UserVote;
        _view.SetText("default_0", vote.NoticeTitle);
        var lines = vote.NoticeLines;
        for (int i = 0; i < Lines.Length; i++) _view.SetText(Lines[i], i < lines.Count ? lines[i] : "");
    }
}
