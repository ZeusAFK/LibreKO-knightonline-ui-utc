using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class ManesSurvivalHud : Control
{
    private const string EnterLayout = "re_survival_enter";
    private const string BarLayout = "re_survivalbosshpbar";
    private const string SelectLayout = "re_survivalskillselect";
    private const string SelectCloseLayout = "re_survivalskillselectclose";
    private const string PassiveViewLayout = "re_survivalpassiveview";
    private const string StripLayout = "re_survivalpassiveskill";
    private const string TooltipLayout = "re_passivetooltip";
    private const string RankLayout = "re_survivalrank";
    private const string IconLayout = "re_war_icon";
    private const string ManesIcon = "btn_manes";
    private const string PotionNoteId = "str_name";
    private const string Empty = "-";

    private const string EnterId = "theme_manes_enter";
    private const string BarId = "theme_manes_bar";
    private const string SelectId = "theme_manes_select";
    private const string SelectCloseId = "theme_manes_select_close";
    private const string PassiveViewId = "theme_manes_passive";
    private const string StripId = "theme_manes_strip";
    private const string RankId = "theme_manes_rank";

    private const int SkillRows = 4;
    private const int PotionRows = 2;
    private const int StripSlots = 10;
    private const double RefreshSeconds = 0.25;
    private const float EnterTop = 80f;
    private const float BarTop = 96f;
    private const float StripLeft = 12f;
    private const float StripTop = 0.42f;
    private const float SelectCloseRight = 24f;
    private const float TooltipGap = 6f;
    private const float FadedTick = 0.45f;

    private readonly PluginGame game;
    private readonly LayoutView enter;
    private readonly LayoutView bar;
    private readonly LayoutView select;
    private readonly LayoutView selectClose;
    private readonly LayoutView passiveView;
    private readonly LayoutView strip;
    private readonly LayoutView tooltip;
    private readonly LayoutView rank;
    private readonly LayoutView icons;
    private readonly List<HudLayout> layouts = new();
    private bool boardFolded;
    private bool boardMinimised;
    private bool upright;
    private int stripPage;
    private double refreshIn;

    public ManesSurvivalHud()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        enter = View(kit, EnterLayout);
        bar = View(kit, BarLayout);
        select = View(kit, SelectLayout);
        selectClose = View(kit, SelectCloseLayout);
        passiveView = View(kit, PassiveViewLayout);
        strip = View(kit, StripLayout);
        tooltip = View(kit, TooltipLayout);
        rank = View(kit, RankLayout);
        icons = View(kit, IconLayout);
        foreach (var (node, control) in icons.Where(n => n.IsButton))
            control.Visible = node.Id == ManesIcon;
        bar.MouseFilter = MouseFilterEnum.Ignore;
        rank.MouseFilter = MouseFilterEnum.Ignore;
        tooltip.MouseFilter = MouseFilterEnum.Ignore;

        enter.OnPressed("btn_click", () => game.ManesSurvival.SignUp(!game.ManesSurvival.Board.Applied));
        enter.OnPressed("btn_close", () => { boardMinimised = true; Refresh(); });
        icons.OnPressed(ManesIcon, () => { boardMinimised = false; Refresh(); });
        select.OnPressed("btn_close", () => game.ManesSurvival.Minimise());
        select.OnPressed("btn_complete", () => game.ManesSurvival.Complete());
        select.OnPressed("btn_complete_bonues", () => game.ManesSurvival.Complete());
        selectClose.OnPressed("btn_open", () => game.ManesSurvival.Reopen());
        passiveView.OnPressed("btn_complete", () => game.ManesSurvival.ClosePassiveWon());
        WireSkillRows("base_4");
        WireSkillRows("base_3");
        WirePotions();
        WireStrip("base_v");
        WireStrip("base_h");
    }

    private LayoutView View(UiKit kit, string layout)
    {
        var view = new LayoutView(kit, kit.Layout(layout)) { Visible = false };
        AddChild(view);
        return view;
    }

    public override void _EnterTree()
    {
        game.ManesSurvival.Changed += Refresh;
        if (layouts.Count == 0)
        {
            var fold = enter.Get("btn_minimum");
            var enterLayout = HudLayout.Attach(enter, EnterId, fold, () => TopCentre(enter, EnterTop));
            enterLayout.Clicked += () => { boardFolded = !boardFolded; Refresh(); };
            layouts.Add(enterLayout);
            layouts.Add(HudLayout.Attach(bar, BarId, null, () => TopCentre(bar, BarTop)));
            layouts.Add(HudLayout.Attach(select, SelectId, select.DragHandle, () => Centre(select)));
            layouts.Add(HudLayout.Attach(selectClose, SelectCloseId, null, () => MiddleRight(selectClose)));
            layouts.Add(HudLayout.Attach(passiveView, PassiveViewId, passiveView.DragHandle, () => Centre(passiveView)));
            layouts.Add(HudLayout.Attach(strip, StripId, null, () => MiddleLeft(strip)));
            layouts.Add(HudLayout.Attach(rank, RankId, null, () => Centre(rank)));
        }
        Refresh();
    }

    public override void _ExitTree() => game.ManesSurvival.Changed -= Refresh;

    public override void _Process(double delta)
    {
        refreshIn -= delta;
        if (refreshIn > 0) return;
        refreshIn = RefreshSeconds;
        Refresh();
    }

    private Vector2 Screen => GetViewport()?.GetVisibleRect().Size ?? Size;

    private Vector2 TopCentre(Control view, float top) => new((Screen.X - view.Size.X) * 0.5f, top);

    private Vector2 Centre(Control view) => (Screen - view.Size) * 0.5f;

    private Vector2 MiddleRight(Control view) => new(Screen.X - view.Size.X - SelectCloseRight, (Screen.Y - view.Size.Y) * 0.5f);

    private Vector2 MiddleLeft(Control view) => new(StripLeft, Screen.Y * StripTop);

    private void Refresh()
    {
        var manes = game.ManesSurvival;
        FillBoard(manes.Board);
        FillBar(manes.Bar);
        FillOffer(manes.Offer);
        FillPassiveWon(manes.PassiveWon);
        FillStrip(manes.Passives, manes.Totals, manes.Bar.Shown);
        FillRank(manes.Ranking);
    }

    private void FillBoard(GameManesBoard board)
    {
        if (!board.Open) boardMinimised = false;
        enter.Visible = board.Open && !boardMinimised;
        icons.Visible = board.Open && boardMinimised;
        if (icons.Visible) icons.Position = TopCentre(icons, EnterTop);
        if (!board.Open) return;
        enter.SetText("str_time", ManesSurvival.Clock(board.SecondsLeft));
        enter.SetText("str_num", board.Participants.ToString());
        enter.SetText("text", board.Applied ? ItemData.Text(ManesSurvival.CancelText, "Cancel") : ItemData.Text(ManesSurvival.ApplyText, "Apply"));
        if (enter.Get("base_window") is { } body) body.Visible = !boardFolded;
    }

    private void FillBar(GameManesBar state)
    {
        bar.Visible = state.Shown;
        if (!state.Shown) return;
        bar.SetText("str_name", state.BossName);
        bar.SetProgress("pg_hp", state.BossMaxHp > 0 ? (float)state.BossHp / state.BossMaxHp : 1f);
        bar.SetText("str_time", state.SecondsLeft <= ManesSurvival.EventSeconds ? ManesSurvival.Clock(state.SecondsLeft) : "");
        bar.SetText("str_score", TextTemplate.Fill(ItemData.Text(ManesSurvival.ScoreText, "My Score : %d"), state.Score));
        bool calm = state.NonBattleLeft > 0;
        if (bar.Get("base_battleTime") is { } battle) battle.Visible = calm;
        if (calm)
            bar.SetText("str_battletime", $"{ItemData.Text(ManesSurvival.NonBattleText, "Non-Battle Time:")} {ManesSurvival.Clock(state.NonBattleLeft)}");
    }

    private void WireSkillRows(string groupId)
    {
        if (select.Root.Find(groupId) is not { } group) return;
        for (int i = 0; i < SkillRows; i++)
        {
            if (group.Find($"base_skill_{i}") is not { } row) continue;
            int index = i;
            select.OnPressed(row, i.ToString(), () => game.ManesSurvival.Select(index));
        }
    }

    private void WirePotions()
    {
        if (select.Root.Find("base_shop") is not { } shop) return;
        for (int i = 0; i < PotionRows; i++)
        {
            if (shop.Find($"base_potion_{i}") is not { } row) continue;
            int index = i;
            select.OnPressed(row, "btn", () =>
            {
                if (game.ManesSurvival.Offer is { } offer && index < offer.Potions.Count) game.ManesSurvival.Buy(offer.Potions[index].Code);
            });
        }
    }

    private void FillOffer(GameManesOffer? offer)
    {
        select.Visible = offer is { Minimised: false };
        selectClose.Visible = offer is { Minimised: true };
        if (offer == null) return;
        if (selectClose.Visible)
        {
            bool faded = offer.Blinking && (int)(Time.GetTicksMsec() / 1000) % 2 == 0;
            if (selectClose.Get("base_tick0") is { } tick0) tick0.Visible = !faded;
            if (selectClose.Get("base_tick1") is { } tick1) tick1.Visible = faded;
            selectClose.TooltipText = TextTemplate.Fill(ItemData.Text(ManesSurvival.AcquirableText, "Acquirable Skill : %d"), offer.Queued);
        }
        if (!select.Visible) return;
        select.SetText("str_title", offer.Title);
        bool three = offer.Skills.Count == 3;
        FillSkillRows("base_4", offer, !three);
        FillSkillRows("base_3", offer, three);
        FillPotions(offer);
        if (select.Get("btn_complete") is { } complete) complete.Visible = !offer.Bonus;
        if (select.Get("btn_complete_bonues") is { } bonus) bonus.Visible = offer.Bonus;
    }

    private void FillSkillRows(string groupId, GameManesOffer offer, bool used)
    {
        if (select.Root.Find(groupId) is not { } group) return;
        if (select.ControlOf(group) is { } box) box.Visible = used;
        if (!used) return;
        for (int i = 0; i < SkillRows; i++)
        {
            if (group.Find($"base_skill_{i}") is not { } row || select.ControlOf(row) is not { } rowControl) continue;
            rowControl.Visible = i < offer.Skills.Count;
            if (!rowControl.Visible) continue;
            var choice = offer.Skills[i];
            SetLabel(select, row, "str_name", choice.Name);
            SetLabel(select, row, "str_info", choice.Description);
            SetIcon(select.Get(row, "img_skillicon"), choice.Icon);
            bool chosen = offer.Selected == i;
            if (select.Get(row, i.ToString()) is { } button) button.Visible = !choice.Held && !chosen;
            ShowComplete(row, choice.Held || chosen, enabled: chosen && !choice.Held);
        }
    }

    private void FillPotions(GameManesOffer offer)
    {
        foreach (var note in select.Root.Children)
            if (note.IsString && note.Id == PotionNoteId && select.ControlOf(note) is { } noteLabel) noteLabel.Visible = offer.Potions.Count > 0;
        if (select.Root.Find("base_shop") is not { } shop) return;
        if (select.ControlOf(shop) is { } box) box.Visible = offer.Potions.Count > 0;
        for (int i = 0; i < PotionRows; i++)
        {
            if (shop.Find($"base_potion_{i}") is not { } row || select.ControlOf(row) is not { } rowControl) continue;
            rowControl.Visible = i < offer.Potions.Count;
            if (!rowControl.Visible) continue;
            var potion = offer.Potions[i];
            SetLabel(select, row, "str_name", potion.Name);
            SetLabel(select, row, "str_noah", potion.Price);
            SetIcon(select.Get(row, "img_skillicon"), potion.Icon);
            if (select.Get(row, "btn") is { } buy) buy.Visible = !potion.Waiting;
            ShowComplete(row, potion.Waiting, enabled: false);
        }
    }

    private void ShowComplete(LayoutNode row, bool shown, bool enabled)
    {
        if (row.Find("base_complete") is not { } complete || select.ControlOf(complete) is not { } control) return;
        control.Visible = shown;
        if (select.Get(complete, "img_enable") is { } on) on.Visible = enabled;
        if (select.Get(complete, "img_disable") is { } off) off.Visible = !enabled;
    }

    private static void SetLabel(LayoutView view, LayoutNode scope, string id, string text)
    {
        if (view.Get<Label>(scope, id) is { } label) label.Text = text;
    }

    private static void SetIcon(Control? holder, Texture2D? texture)
    {
        if (holder is TextureRect rect)
        {
            rect.Texture = texture;
            return;
        }
        if (holder == null) return;
        if (holder.GetNodeOrNull<TextureRect>("icon") is not { } icon)
        {
            icon = new TextureRect
            {
                Name = "icon",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            icon.SetAnchorsPreset(LayoutPreset.FullRect);
            holder.AddChild(icon);
        }
        icon.Texture = texture;
    }

    private void FillPassiveWon(GameManesChoice? won)
    {
        passiveView.Visible = won != null;
        if (won is not { } passive) return;
        passiveView.SetText("str_name", passive.Name);
        passiveView.SetText("str_info", passive.Description);
        SetIcon(passiveView.Get("img_skillicon"), passive.Icon);
    }

    private void WireStrip(string groupId)
    {
        if (strip.Root.Find(groupId) is not { } group) return;
        strip.OnPressed(group, "btn_pageup", () => { stripPage--; Refresh(); });
        strip.OnPressed(group, "btn_pagedown", () => { stripPage++; Refresh(); });
        strip.OnPressed(group, "btn_rotation", () => { upright = !upright; Refresh(); });
        strip.Hide(group, "btn_up", "btn_down");
        if (strip.Get(group, "img_info") is { } info)
        {
            info.MouseEntered += () => ShowTooltip(info);
            info.MouseExited += () => tooltip.Visible = false;
        }
    }

    private void ShowTooltip(Control anchor)
    {
        var totals = game.ManesSurvival.Totals;
        tooltip.SetText("str_hp", totals.Hp);
        tooltip.SetText("str_hit", totals.AttackUp);
        tooltip.SetText("str_ac", totals.AttackDown);
        tooltip.Position = anchor.GlobalPosition - GlobalPosition + new Vector2(anchor.Size.X + TooltipGap, 0f);
        tooltip.Visible = true;
        MoveChild(tooltip, GetChildCount() - 1);
    }

    private void FillStrip(IReadOnlyList<GameManesPassive> passives, GameManesTotals totals, bool running)
    {
        strip.Visible = running && passives.Count > 0;
        if (!strip.Visible)
        {
            tooltip.Visible = false;
            return;
        }
        int pages = Math.Max(1, (passives.Count + StripSlots - 1) / StripSlots);
        stripPage = Math.Clamp(stripPage, 0, pages - 1);
        foreach (var groupId in new[] { "base_v", "base_h" })
        {
            if (strip.Root.Find(groupId) is not { } group || strip.ControlOf(group) is not { } box) continue;
            bool used = (groupId == "base_h") == upright;
            box.Visible = used;
            if (!used) continue;
            SetLabel(strip, group, "str_num", (stripPage + 1).ToString());
            for (int slot = 0; slot < StripSlots; slot++)
            {
                int index = stripPage * StripSlots + slot;
                var passive = index < passives.Count ? passives[index] : (GameManesPassive?)null;
                foreach (var node in group.Children)
                {
                    if (node.Id != slot.ToString() || strip.ControlOf(node) is not { } control) continue;
                    if (node.IsArea) SetIcon(control, passive?.Icon);
                    else if (control is Label count) count.Text = passive is { Count: > 1 } p ? p.Count.ToString() : "";
                    control.TooltipText = passive?.Name ?? "";
                }
            }
        }
    }

    private void FillRank(GameManesRanking ranking)
    {
        rank.Visible = ranking.Shown;
        if (!ranking.Shown) return;
        var root = rank.Root;
        for (int i = 0; i < ManesSurvival.RankRows; i++)
        {
            string key = i.ToString();
            var row = i < ranking.Rows.Count ? ranking.Rows[i] : new GameManesRankRow((i + 1).ToString(), Nations.NotSelected, Empty, Empty, Empty, Empty, false);
            SetColumn(root, "base_rank", key, row.Rank);
            SetColumn(root, "base_killcount", key, row.Kills);
            SetColumn(root, "base_clan", key, row.Clan);
            SetColumn(root, "base_name", key, row.Name);
            SetColumn(root, "base_score", key, row.Score);
            if (root.Find("base_nation")?.Find(key) is { } nation) ShowNation(nation, row.Nation);
            if (row.Own) MarkOwnRow(root, key);
        }
        if (root.Find("base_myrankisranker") is { } ranker && rank.ControlOf(ranker) is { } rankerControl)
            rankerControl.Visible = ranking.Rows.Any(r => r.Own);
        var mine = ranking.Mine;
        rank.SetText("str_myrank", mine.Rank);
        rank.SetText("str_mykillcount", mine.Kills);
        rank.SetText("str_myclan", mine.Clan);
        rank.SetText("str_myname", mine.Name);
        rank.SetText("str_myscore", mine.Score);
        if (root.Find("base_mynation") is { } myNation) ShowNation(myNation, mine.Nation);
    }

    private void MarkOwnRow(LayoutNode root, string key)
    {
        if (root.Find("base_myrankisranker") is not { } mark || rank.ControlOf(mark) is not { } marker) return;
        foreach (var node in root.Children)
            if (node.IsImage && node.Id == key && rank.ControlOf(node) is { } rowImage)
                marker.Position = new Vector2(marker.Position.X, rowImage.Position.Y);
    }

    private void SetColumn(LayoutNode root, string column, string key, string text)
    {
        if (root.Find(column) is not { } group) return;
        foreach (var node in group.Children)
            if (node.Id == key && rank.ControlOf(node) is Label label) label.Text = text;
    }

    private void ShowNation(LayoutNode scope, int nation)
    {
        if (rank.Get(scope, "img_ka") is { } karus) karus.Visible = nation == Nations.Karus;
        if (rank.Get(scope, "img_el") is { } elMorad) elMorad.Visible = nation == Nations.ElMorad;
    }
}
