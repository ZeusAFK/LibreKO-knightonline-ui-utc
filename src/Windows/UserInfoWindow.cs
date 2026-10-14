using System.Collections.Generic;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class UserInfoWindow : Control
{
    private const string LayoutName = "re_map_info";
    private const string SummaryFormat = "icon_level_{0}";
    private const string TierFormat = "icon_level_{0}_{1}";
    private const string TitleFormat = "title_{0}";
    private const string PlaceFormat = "rank_{0}";
    private const string PodiumFormat = "img_background_{0}";
    private const string ClanMarkId = "img_clanmark";
    private const int NoClanPodium = 4;
    private const int UnrankedPodium = 0;
    private const int TitleBarHeight = 90;
    private const string RelationNone = "-";

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly Dictionary<Label, TextureRect> ribbons = new();
    private readonly Texture2D? noClanMark;

    public UserInfoWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        view.OnPressed("btn_ok", () => game.UserInfo.Close());
        noClanMark = view.Get<TextureRect>(ClanMarkId)?.Texture;
    }

    public override void _EnterTree()
    {
        game.UserInfo.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => game.UserInfo.Changed -= Refresh;

    private void Refresh()
    {
        if (game.UserInfo.Card is not { } card) return;
        view.SetText("str_clanname", card.ClanName);
        view.SetText("str_rank", card.ClanGrade);
        view.SetText("str_master", card.ClanChief);
        view.SetText("str_name", card.Name);
        if (card.NameColour is { } colour && view.Get<Label>("str_name") is { } name)
            name.AddThemeColorOverride("font_color", colour);
        view.SetText("str_lev", card.Level);
        view.SetText("str_job", card.Job);
        view.SetText("str_realmpoint", card.Loyalty);
        view.SetText("str_radder", card.MonthlyLoyalty);
        view.SetText("str_state", card.State);
        ShowRelation(card.Relation);
        ShowClan(card);

        for (int i = 0; i < UserInfoCard.SummaryCells; i++)
            ShowRibbon(string.Format(SummaryFormat, i), i < card.Summary.Count ? card.Summary[i] : null);

        for (int r = 0; r < UserInfoCard.RankRows; r++)
        {
            bool used = r < card.Rows.Count;
            var row = used ? card.Rows[r] : default;
            if (view.Get<Label>(string.Format(TitleFormat, r)) is { } title) title.Text = used ? row.Title : "";
            if (view.Get<Label>(string.Format(PlaceFormat, r)) is { } place)
            {
                place.Text = used ? row.Place : "";
                place.AddThemeColorOverride("font_color", used ? row.PlaceColour : Colors.White);
            }
            for (int t = 0; t < UserInfoCard.Tiers; t++)
            {
                GameRankRibbon? tier = used && row.Tiers != null && t < row.Tiers.Count && row.Tiers[t].Ribbon != null ? row.Tiers[t] : null;
                ShowRibbon(string.Format(TierFormat, r, t), tier);
            }
        }
    }

    private void ShowRelation(UserRelation relation)
    {
        if (view.Get<Label>("str_friend") is { } friend) friend.Visible = relation == UserRelation.Both;
        if (view.Get<Label>("str_guild") is { } guild) guild.Visible = relation == UserRelation.Both;
        if (view.Get<Label>("str_relation") is not { } line) return;
        line.Visible = true;
        line.Text = relation switch
        {
            UserRelation.Friend => view.Get<Label>("str_friend")?.Text ?? "Friend",
            UserRelation.Clan => view.Get<Label>("str_guild")?.Text ?? "Guild",
            _ => RelationNone,
        };
    }

    private void ShowClan(GameUserCard card)
    {
        int podium = !card.InClan ? NoClanPodium
            : card.ClanRanking is >= 1 and <= UserInfoCard.PodiumPlaces ? card.ClanRanking
            : UnrankedPodium;
        for (int i = 0; i <= NoClanPodium; i++)
            if (view.Get(string.Format(PodiumFormat, i)) is { } art) art.Visible = i == podium;
        view.SetText("rank", card.InClan && card.ClanRanking > UserInfoCard.PodiumPlaces ? card.ClanRanking.ToString() : "");
        if (view.Get<TextureRect>(ClanMarkId) is { } mark)
        {
            mark.Texture = card.InClan && card.ClanMark != null ? card.ClanMark : noClanMark;
            mark.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            mark.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        }
    }

    private void ShowRibbon(string id, GameRankRibbon? ribbon)
    {
        if (view.Get<Label>(id) is not { } label) return;
        label.Visible = ribbon != null;
        label.Text = ribbon?.Count ?? "";
        label.TooltipText = ribbon?.Tooltip ?? "";
        label.MouseFilter = ribbon != null && ribbon.Value.Tooltip.Length > 0 ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
        if (!ribbons.TryGetValue(label, out var image))
        {
            image = new TextureRect
            {
                Position = label.Position,
                Size = label.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            label.AddSibling(image);
            label.GetParent().MoveChild(image, label.GetIndex());
            ribbons[label] = image;
        }
        image.Texture = ribbon?.Ribbon;
        image.Visible = ribbon != null;
    }
}
