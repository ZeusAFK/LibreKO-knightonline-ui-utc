using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class PartyHud : Control
{
    private const string LayoutName = "re_party";
    private const string LayoutId = "theme_party";
    private const string RowPrefix = "Base_Party";
    private const string MenuGroup = "Base_menu";
    private const int MaxRows = 8;
    private const int PanelLeft = 173;
    private const int PanelRight = 313;
    private const int MenuRight = 422;
    private const int TitleBarHeight = 25;
    private const string CommanderZoom = "btn_commander_zoom";
    private const float DefaultTop = 380f;

    private static readonly string[] HiddenIds =
    {
        "base_option", "btn_voice", "Base_Rank",
        "str_Count", "img_voice", MenuGroup,
        "base_Voice0", "base_Voice1", "base_Voice2", "base_Voice3", "base_Voice4", "base_Voice5", "base_Voice6", "base_Voice7",
        "img_soundrev0", "img_soundrev1", "img_soundrev2", "img_soundrev3", "img_soundrev4", "img_soundrev5", "img_soundrev6",
        "img_soundrev7",
    };

    private static readonly string[] RowHiddenIds = { "img_voice_play", "BtnCheckTP" };
    private const string ClassIconFormat = "class_{0}.png";
    private const string ClassArea = "area";
    private static readonly Dictionary<PartyHpLook, string> HpTextures = new()
    {
        [PartyHpLook.Normal] = "prg_party_red.png",
        [PartyHpLook.Lasting] = "prg_party_brown.png",
        [PartyHpLook.Drop] = "prg_party_purple.png",
        [PartyHpLook.Slow] = "prg_party_green.png",
        [PartyHpLook.Undead] = "prg_party_gray.png",
    };
    private readonly TextureRect?[] classIcons = new TextureRect?[MaxRows];
    private const string MeterToggle = "btn_metertoggle";
    private const string RejoinBox = "auto_rejoin";
    private const string VoteGroup = "group_uservote";
    private static readonly string[] LeaderGroups = { "group_chief", "group_out", "group_commander" };
    private readonly float[] menuTops = new float[LeaderGroups.Length + 1];
    private const string MeterBoard = "btn_metertotal";
    private static readonly string[] MeterTips = { "Party meter: off", "Party meter: damage dealt", "Party meter: healing" };

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly LayoutNode[] rows = new LayoutNode[MaxRows];
    private readonly int[] rowMember = new int[MaxRows];
    private readonly Control drag;
    private int menuMember = -1;
    private HudLayout? hudLayout;

    public PartyHud()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout) { Position = new Vector2(-PanelLeft, 0) };
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(MenuRight - PanelLeft, TitleBarHeight);
        drag = new Control
        {
            MouseFilter = MouseFilterEnum.Stop,
            Size = new Vector2(PanelRight - PanelLeft, TitleBarHeight),
        };
        AddChild(drag);
        AddChild(view);
        view.Hide(HiddenIds);
        foreach (var (node, control) in view.Where(n => n.IsString && n.Text == "Friends Voice"))
            control.Visible = false;

        for (int i = 0; i < MaxRows; i++)
        {
            rowMember[i] = -1;
            if (layout.Find(RowPrefix + i) is not { } row) continue;
            rows[i] = row;
            view.Hide(row, RowHiddenIds);
            int arrowSlot = i;
            view.OnPressed(row, "btn_up", () => MoveRow(arrowSlot, PartyOrder.Up));
            view.OnPressed(row, "btn_down", () => MoveRow(arrowSlot, PartyOrder.Down));
            if (view.Get(row, ClassArea) is { } area)
            {
                var icon = new TextureRect
                {
                    Size = area.Size,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                area.AddChild(icon);
                classIcons[i] = icon;
            }
            if (view.ControlOf(row) is { } frame)
            {
                int slot = i;
                frame.MouseFilter = MouseFilterEnum.Stop;
                frame.GuiInput += ev => OnRowInput(slot, ev);
            }
        }

        view.OnPressed("btn_chief", () => RunMenu(game.Party.MakeLeader));
        view.OnPressed("btn_out", () => RunMenu(game.Party.Kick));
        view.OnPressed("btn_commander", () => RunMenu(game.Party.MakeCommander));
        view.OnPressed("btn_uservote", () => RunMenu(game.Party.UserVote));
        for (int i = 0; i < menuTops.Length; i++)
            menuTops[i] = view.Get(i < LeaderGroups.Length ? LeaderGroups[i] : VoteGroup)?.Position.Y ?? 0f;
        view.OnPressed(CommanderZoom, () => game.Party.ShowCommanderBar());
        view.OnPressed(MeterToggle, () => game.Party.CycleRowMeter());
        if (view.Get<TextureButton>(RejoinBox) is { } rejoin)
        {
            rejoin.ToggleMode = true;
            rejoin.Toggled += _ => game.Party.ToggleAutoRejoin();
        }
        view.OnPressed(MeterBoard, () => game.Party.ToggleWarBoard());
        if (view.Get(MeterBoard) is { } board) board.TooltipText = "Party War Board";
        view.OnPressed("btn_party", () => game.Party.Leave());
        if (view.Get("btn_party") is { } leave) leave.TooltipText = "Leave the party";
    }

    public override void _EnterTree()
    {
        game.Party.Changed += Refresh;
        hudLayout ??= HudLayout.Attach(this, LayoutId, drag, () => new Vector2(HudAnchor.Edge, DefaultTop));
        Refresh();
    }

    public override void _ExitTree() => game.Party.Changed -= Refresh;

    private void Refresh()
    {
        bool shown = game.Party.InParty;
        Visible = shown;
        if (!shown) { ShowMenu(-1); return; }
        if (view.Get(CommanderZoom) is { } zoom) zoom.Visible = game.Party.CommanderBarHidden;
        if (view.Get<TextureButton>(RejoinBox) is { } rejoin)
        {
            rejoin.SetPressedNoSignal(game.Party.AutoRejoin);
            rejoin.TooltipText = game.Party.AutoRejoinTip;
        }
        var meterMode = game.Party.RowMeterMode;
        bool meterOn = meterMode != PartyBoardMeterMode.Off;
        if (view.Get(MeterToggle) is { } toggle && (int)meterMode < MeterTips.Length) toggle.TooltipText = MeterTips[(int)meterMode];
        var members = game.Party.Members;
        int bottom = TitleBarHeight;
        for (int i = 0; i < MaxRows; i++)
        {
            var row = rows[i];
            if (row == null) continue;
            bool used = i < members.Count;
            if (view.ControlOf(row) is { } frame) frame.Visible = used;
            rowMember[i] = used ? members[i].Id : -1;
            if (!used) continue;
            var m = members[i];
            bottom = Mathf.Max(bottom, row.Y + row.H);
            if (view.Get<Label>(row, "str_name") is { } name)
            {
                name.Text = m.Name;
                name.AddThemeColorOverride("font_color", m.NameColour);
            }
            if (view.ControlOf(row) is { } rowControl) rowControl.TooltipText = m.Tooltip;
            SetBar(row, "progress_hp", m.MaxHp > 0 ? (float)m.Hp / m.MaxHp : 0f, null);
            if (view.Get<TextureProgressBar>(row, "progress_hp") is { } hp && Plugin.Kit.Texture(HpTextures[m.HpLook]) is { } hpTexture)
                hp.TextureProgress = hpTexture;
            if (classIcons[i] is { } classIcon)
                classIcon.Texture = Plugin.Kit.Texture(string.Format(ClassIconFormat, LibreKO.Domain.PartyHud.ClassIconIndex(m.Class)));
            SetBar(row, "progress_mp", m.MaxMp > 0 ? (float)m.Mp / m.MaxMp : 0f, null);
            if (view.Get(row, "base_choice") is { } choice) choice.Visible = m.Picked;
            if (view.Get(row, "bg_normal") is { } normal) normal.Visible = !meterOn;
            if (view.Get(row, "bg_small") is { } small) small.Visible = meterOn;
            if (view.Get(row, "meterbar") is { } meter) meter.Visible = meterOn;
            if (meterOn) SetBar(row, "progress_meter", PartyWarBoard.Fill(Math.Max(0, game.Party.RowMeterShare(m.Id))), null);
        }
        Size = new Vector2(MenuRight - PanelLeft, bottom);
        if (menuMember >= 0 && !game.Party.IsLeader) ShowMenu(-1);
    }

    private void MoveRow(int slot, int step)
    {
        if (rowMember[slot] >= 0) game.Party.MoveRow(rowMember[slot], step);
    }

    private void SetBar(LayoutNode row, string id, float fraction, Color? tint)
    {
        if (view.Get(row, id) is not TextureProgressBar bar) return;
        bar.Value = Mathf.Clamp(fraction, 0f, 1f) * bar.MaxValue;
        if (tint is { } colour) bar.SelfModulate = colour.Lightened(0.35f);
    }

    private void OnRowInput(int slot, InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } click || rowMember[slot] < 0) return;
        int member = rowMember[slot];
        if (click.ButtonIndex == MouseButton.Left)
        {
            ShowMenu(-1);
            game.Party.Pick(member);
            AcceptEvent();
        }
        else if (click.ButtonIndex == MouseButton.Right && (game.Party.IsLeader || game.Party.UserVoteHere))
        {
            ShowMenu(member);
            AcceptEvent();
        }
    }

    private void ShowMenu(int member)
    {
        menuMember = member;
        if (view.Get(MenuGroup) is { } menu) menu.Visible = member >= 0;
        if (member < 0) return;
        int slot = 0;
        foreach (var id in LeaderGroups)
            Stack(id, game.Party.IsLeader, ref slot);
        Stack(VoteGroup, game.Party.UserVoteHere, ref slot);
    }

    private void Stack(string id, bool shown, ref int slot)
    {
        if (view.Get(id) is not { } group) return;
        group.Visible = shown;
        if (!shown) return;
        group.Position = group.Position with { Y = menuTops[slot] };
        slot++;
    }

    private void RunMenu(System.Action<int> action)
    {
        int member = menuMember;
        ShowMenu(-1);
        if (member >= 0) action(member);
    }
}
