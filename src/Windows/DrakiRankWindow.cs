using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class DrakiRankWindow : Control
{
    private const string LayoutName = "re_draky_rank";
    private const string BoardFormat = "str_Board{0}{1}";
    private const string ChapterFormat = "Btn_Chapter{0}";
    private const string MyRankId = "str_MyRank";
    private const string EntrancesId = "str_EnterCount";
    private const string EnterCaption = "Enter";
    private const string ClosedCaption = "x";
    private const int MineRow = 5;
    private const int NameColumn = 0;
    private const int TimeColumn = 1;
    private const int StageColumn = 2;
    private const int TitleBarHeight = 44;

    private readonly PluginGame game;
    private readonly LayoutView view;

    public DrakiRankWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        Size = layout.SizeVec;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        view.OnPressed("btn_close", host.Close);
        for (int chapter = 1; chapter <= DrakiTower.Chapters; chapter++)
        {
            int picked = chapter;
            view.OnPressed(string.Format(ChapterFormat, chapter), () => game.Draki.EnterChapter(picked));
        }
    }

    public override void _EnterTree()
    {
        game.Draki.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => game.Draki.Changed -= Refresh;

    private void Refresh()
    {
        if (game.Draki.Ranks is not { } ranks) return;
        for (int row = 0; row < DrakiWire.TopRows; row++)
        {
            var line = row < ranks.Top.Count ? ranks.Top[row] : default;
            bool filled = !string.IsNullOrEmpty(line.Name);
            SetRow(row, filled ? line.Name : "", filled ? DrakiTower.FinishTime(line.FinishSeconds) : "", filled ? line.Stage.ToString() : "");
        }
        var mine = ranks.Mine;
        bool ranked = DrakiTower.Ranked(mine.Rank);
        view.SetText(MyRankId, ranked ? mine.Rank.ToString() : ItemData.Text(DrakiTower.OffrankedText, DrakiTower.OffrankedFallback));
        SetRow(MineRow, mine.Name ?? "", ranked ? DrakiTower.FinishTime(mine.FinishSeconds) : "", ranked ? mine.Stage.ToString() : "");
        view.SetText(EntrancesId, ranks.EntrancesLeft.ToString());
        for (int chapter = 1; chapter <= DrakiTower.Chapters; chapter++)
        {
            if (view.Get<TextureButton>(string.Format(ChapterFormat, chapter)) is not { } enter) continue;
            bool open = DrakiTower.SectorOpen(chapter, ranks.MaxStage);
            enter.Disabled = !open;
            foreach (var child in enter.GetChildren())
                if (child is Label caption) caption.Text = open ? EnterCaption : ClosedCaption;
        }
    }

    private void SetRow(int row, string name, string time, string stage)
    {
        view.SetText(string.Format(BoardFormat, row, NameColumn), name);
        view.SetText(string.Format(BoardFormat, row, TimeColumn), time);
        view.SetText(string.Format(BoardFormat, row, StageColumn), stage);
    }
}
