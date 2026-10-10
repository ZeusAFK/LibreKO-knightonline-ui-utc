using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public sealed class FriendsPage
{
    public const string LayoutName = "re_page_friends";
    private const string RowPrefix = "base_friend";
    private const string SelectBar = "img_choice";
    private const string Scrollbar = "scrollbar_list";
    private const string NameText = "strname";
    private const string Count = "str_num";
    private const string MailButton = "btn_sendmail";
    private const int IconOnline = 0;
    private const int IconParty = 1;
    private const int IconOffline = 2;
    private static readonly string[] IconIds = { "icon_on", "icon_party", "icon_off" };
    private static readonly (string Id, string Text)[] Legends = { ("uitext_0", "Online"), ("uitext_1", "Party"), ("uitext_2", "Offline") };
    private static readonly Color OnlineColour = new(1f, 1f, 1f);
    private static readonly Color OfflineColour = new(0.6f, 0.6f, 0.6f);

    private readonly LayoutView _view;
    private readonly IGameFriends _friends;
    private readonly RetailList _list;

    public LayoutView View => _view;

    public FriendsPage(UiKit kit, IGameFriends friends)
    {
        _friends = friends;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        var rows = layout.Children
            .Where(n => n.Id.StartsWith(RowPrefix, StringComparison.Ordinal) && int.TryParse(n.Id[RowPrefix.Length..], out _))
            .OrderBy(n => int.Parse(n.Id[RowPrefix.Length..]));
        _list = new RetailList(_view, rows, PaintRow, _view.Get(SelectBar), layout.Find(Scrollbar));
        _list.Activated += name => _friends.Whisper(name);
        _list.ContextRequested += (name, at) => _friends.ShowPlayerMenu(name, at);
        _view.OnPressed("btn_add", _friends.Add);
        _view.OnPressed("Btn_Delete", () => WithSelected(_friends.Remove));
        _view.OnPressed("btn_whisper", () => WithSelected(_friends.Whisper));
        _view.OnPressed("btn_party", () => WithSelected(_friends.InviteToParty));
        _view.OnPressed("btn_refresh", _friends.Refresh);
        _view.Hide(MailButton);
        foreach (var (id, text) in Legends) _view.SetText(id, text);
    }

    private void WithSelected(Action<string> action)
    {
        if (_list.SelectedKey is { } name) action(name);
    }

    public void Refresh()
    {
        var entries = _friends.Entries;
        var items = entries
            .OrderByDescending(f => f.Online)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new RetailListItem(f.Name, f.Name, f.Online ? OnlineColour : OfflineColour,
                f.InParty ? IconParty : f.Online ? IconOnline : IconOffline, Tooltip(f)))
            .ToList();
        _list.SetItems(items);
        _view.SetText(Count, $"{entries.Count(f => f.Online)}/{entries.Count}");
    }

    private static string Tooltip(GameFriend f)
    {
        var parts = new List<string>();
        if (f.Level > 0) parts.Add($"Lv {f.Level}");
        if (f.ClassName.Length > 0) parts.Add(f.ClassName);
        if (f.Zone.Length > 0) parts.Add(f.Zone);
        return string.Join(" · ", parts);
    }

    private void PaintRow(LayoutNode row, RetailListItem? item)
    {
        foreach (var child in row.Children)
        {
            if (_view.ControlOf(child) is not { } control) continue;
            int icon = Array.IndexOf(IconIds, child.Id);
            if (icon >= 0) control.Visible = item is { } shown && shown.Icon == icon;
            else if (child.Id == NameText && control is Label label)
            {
                label.Text = item?.Text ?? "";
                if (item is { } named) label.AddThemeColorOverride("font_color", named.Colour);
            }
        }
    }
}
