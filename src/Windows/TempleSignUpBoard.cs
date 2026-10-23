using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class TempleSignUpBoard : Control
{
    private const string WarLayout = "re_war_borderdefence_enter";
    private const string ChaosLayout = "re_war_chaos_enter";
    private const string IconLayout = "re_war_icon";
    private const string LayoutId = "theme_temple_signup";
    private const string FullGroup = "grp_maximum";
    private const string FoldButton = "btn_maximum";
    private const string ApplyButton = "btn_enter";
    private const string CancelButton = "btn_cancel";
    private const string MinimiseButton = "icon";
    private const string TimeId = "txt_time";
    private const string WarTitleId = "str_title";
    private const string ChaosTitleId = "txt_title";
    private const string KarusId = "txt_karu_0";
    private const string ElMoradId = "txt_elmo_0";
    private const string ParticipantsId = "txt_participant";
    private const string WarIcon = "bnt_war";
    private const string ChaosIcon = "btn_chaos";
    private const string LatenoidIcon = "btn_latenoid";
    private const string MinimiseTip = "Hide to the icon bar";
    private const string FoldTip = "Click to fold, drag to move";
    private const float DefaultTop = 80f;
    private const float StripGap = 4f;
    private const double RefreshSeconds = 0.25;
    private static readonly string[] IconOrder = { WarIcon, ChaosIcon, "btn_pathos", "btn_manes", LatenoidIcon };

    private sealed class Board
    {
        public LayoutView View = null!;
        public Control? Full;
        public Control? Fold;
        public Label? Title;
        public Label? Time;
        public Label? Karus;
        public Label? ElMorad;
        public Label? Participants;
        public BaseButton? Apply;
        public BaseButton? Cancel;
    }

    private readonly PluginGame game;
    private readonly Board war;
    private readonly Board chaos;
    private readonly LayoutView strip;
    private readonly HashSet<TempleEventType> seen = new();
    private readonly HashSet<TempleEventType> minimised = new();
    private HudLayout? hudLayout;
    private TempleEventType shown;
    private bool folded;
    private double refreshIn;

    public TempleSignUpBoard()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        war = BuildBoard(kit, WarLayout, WarTitleId);
        chaos = BuildBoard(kit, ChaosLayout, ChaosTitleId);
        Size = war.View.Size;
        strip = new LayoutView(kit, kit.Layout(IconLayout));
        AddChild(strip);
        foreach (var icon in IconOrder)
            if (strip.Get(icon) is { } button) button.Visible = false;
        strip.OnPressed(WarIcon, () => Restore(WarIcon));
        strip.OnPressed(ChaosIcon, () => Restore(ChaosIcon));
        strip.OnPressed(LatenoidIcon, () => Restore(LatenoidIcon));
    }

    private Board BuildBoard(UiKit kit, string layoutName, string titleId)
    {
        var view = new LayoutView(kit, kit.Layout(layoutName)) { Visible = false };
        AddChild(view);
        var board = new Board
        {
            View = view,
            Full = view.Get(FullGroup),
            Fold = view.Get(FoldButton),
            Title = view.Get<Label>(titleId),
            Time = view.Get<Label>(TimeId),
            Karus = view.Get<Label>(KarusId),
            ElMorad = view.Get<Label>(ElMoradId),
            Participants = view.Get<Label>(ParticipantsId),
            Apply = view.Get<BaseButton>(ApplyButton),
            Cancel = view.Get<BaseButton>(CancelButton),
        };
        if (board.Fold != null) board.Fold.TooltipText = FoldTip;
        if (view.Get<BaseButton>(MinimiseButton) is { } minimise)
        {
            minimise.TooltipText = MinimiseTip;
            minimise.Pressed += Minimise;
        }
        if (board.Apply != null) board.Apply.Pressed += () => game.TempleSignUps.Join(shown);
        if (board.Cancel != null) board.Cancel.Pressed += () => game.TempleSignUps.Cancel();
        return board;
    }

    public override void _EnterTree()
    {
        game.TempleSignUps.Changed += Refresh;
        if (hudLayout == null)
        {
            hudLayout = HudLayout.Attach(this, LayoutId, war.Fold, DefaultPosition);
            if (chaos.Fold != null) hudLayout.AddDragHandle(chaos.Fold);
            hudLayout.Clicked += ToggleFold;
        }
        Refresh();
    }

    public override void _ExitTree() => game.TempleSignUps.Changed -= Refresh;

    public override void _Process(double delta)
    {
        refreshIn -= delta;
        if (refreshIn > 0) return;
        refreshIn = RefreshSeconds;
        Refresh();
    }

    private Vector2 DefaultPosition() =>
        new((GetViewport().GetVisibleRect().Size.X - Size.X) * 0.5f, DefaultTop);

    private static bool UsesWarBoard(TempleEventType type) => TempleJoin.PerNation(type);

    private static string IconFor(TempleEventType type) => type switch
    {
        TempleEventType.BorderDefenseWar or TempleEventType.JuraidMountain => WarIcon,
        TempleEventType.Chaos => ChaosIcon,
        _ => LatenoidIcon,
    };

    private void Refresh()
    {
        var open = game.TempleSignUps.Open;
        var openTypes = open.Select(s => s.Type).ToHashSet();
        seen.IntersectWith(openTypes);
        minimised.IntersectWith(openTypes);
        foreach (var signUp in open)
        {
            if (!seen.Add(signUp.Type)) continue;
            shown = signUp.Type;
            minimised.Clear();
        }
        if (open.FirstOrDefault(s => s.Registered) is { Registered: true } joined && !minimised.Contains(joined.Type))
            shown = joined.Type;
        if (!openTypes.Contains(shown) || minimised.Contains(shown)) shown = TempleEventType.None;

        bool boardShown = shown != TempleEventType.None;
        war.View.Visible = boardShown && UsesWarBoard(shown);
        chaos.View.Visible = boardShown && !UsesWarBoard(shown);
        if (boardShown) Fill(UsesWarBoard(shown) ? war : chaos, open.First(s => s.Type == shown));
        LayOutStrip(boardShown);
        Visible = boardShown || minimised.Count > 0;
    }

    private void Fill(Board board, GameTempleSignUp signUp)
    {
        if (board.Title != null) board.Title.Text = signUp.Title;
        if (board.Time != null) board.Time.Text = TempleJoin.Clock(signUp.SecondsLeft);
        if (board.Karus != null) board.Karus.Text = signUp.Karus.ToString();
        if (board.ElMorad != null) board.ElMorad.Text = signUp.ElMorad.ToString();
        if (board.Participants != null) board.Participants.Text = (signUp.Karus + signUp.ElMorad).ToString();
        if (board.Apply != null)
        {
            board.Apply.Visible = !signUp.Registered;
            board.Apply.Disabled = signUp.Hint != null;
            board.Apply.TooltipText = signUp.Hint ?? "";
        }
        if (board.Cancel != null) board.Cancel.Visible = signUp.Registered;
        if (board.Full != null) board.Full.Visible = !folded;
    }

    private void LayOutStrip(bool boardShown)
    {
        var icons = minimised.Select(IconFor).ToHashSet();
        float right = strip.Size.X;
        foreach (var icon in IconOrder)
        {
            if (strip.Get(icon) is not { } button) continue;
            button.Visible = icons.Contains(icon);
            if (!button.Visible) continue;
            right -= button.Size.X;
            button.Position = new Vector2(right, button.Position.Y);
        }
        strip.Visible = icons.Count > 0;
        strip.Position = new Vector2(Size.X - strip.Size.X, boardShown ? Size.Y + StripGap : 0f);
    }

    private void Minimise()
    {
        if (shown == TempleEventType.None) return;
        minimised.Add(shown);
        Refresh();
    }

    private void Restore(string icon)
    {
        var type = minimised.FirstOrDefault(t => IconFor(t) == icon);
        if (type == TempleEventType.None) return;
        minimised.Remove(type);
        shown = type;
        Refresh();
    }

    private void ToggleFold()
    {
        folded = !folded;
        Refresh();
    }
}
