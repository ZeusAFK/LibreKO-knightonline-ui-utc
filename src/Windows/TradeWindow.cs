using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Domain;
using LibreKO.Plugins;
using ItemSlot = KnightOnlineUi.Layout.ItemSlot;

namespace KnightOnlineUi.Windows;

public partial class TradeWindow : Control
{
    private const string LayoutName = "re_personaltrade";
    private const int BagCells = 28;
    private const int BagTop = 390;
    private const int OfferSplitX = 210;
    private const int TitleBarHeight = 44;
    private const string TheirReady = "img_trade_other_down";
    private const string TheirWaiting = "img_trade_other_normal";

    private readonly WindowHost _host;
    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly List<ItemSlot> _bag = new();
    private readonly List<ItemSlot> _mine = new();
    private readonly List<ItemSlot> _theirs = new();

    public TradeWindow(WindowHost host)
    {
        _host = host;
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        _view.Hide("list_iteminfo", "btn_gold2");
        foreach (var (node, control) in _view.Where(n => n.IsString && int.TryParse(n.Id, out _)))
            control.Visible = false;

        foreach (var (node, control) in _view.Where(n => n.IsArea && int.TryParse(n.Id, out _)).ToList())
        {
            int index = int.Parse(node.Id);
            if (node.Y >= BagTop && index < BagCells)
                _bag.Add(Cell(control, InventoryConstants.InventoryStart + index, s => _game.Inventory.At(s), OfferFromBag,
                    (s, over) => { if (over) _game.Inventory.ShowTooltip(s); else _game.Inventory.HideTooltip(); }));
            else if (node.Y < BagTop && node.X < OfferSplitX)
                _mine.Add(Cell(control, index, s => At(_game.Trade.Mine, s), null,
                    (s, over) => { if (over) _game.Trade.ShowTooltip(true, s); else _game.Trade.HideTooltip(); }));
            else if (node.Y < BagTop)
                _theirs.Add(Cell(control, index, s => At(_game.Trade.Theirs, s), null,
                    (s, over) => { if (over) _game.Trade.ShowTooltip(false, s); else _game.Trade.HideTooltip(); }));
        }
        _mine.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        _theirs.Sort((a, b) => a.Slot.CompareTo(b.Slot));

        if (_view.Get("area_my") is { } dropArea)
        {
            var drop = new DropTarget { Position = dropArea.Position, Size = dropArea.Size, OnDrop = from => _game.Trade.Offer(from) };
            dropArea.GetParent().AddChild(drop);
            dropArea.GetParent().MoveChild(drop, 0);
        }

        _view.OnPressed("btn_close", () => _game.Trade.Cancel());
        _view.OnPressed("btn_gold", () => _game.Trade.AskGold());
        _view.OnPressed("btn_trade_my", () => _game.Trade.Confirm());
        if (_view.Get("btn_gold") is { } gold) gold.TooltipText = "Offer gold";
        if (_view.Get("btn_trade_my") is { } confirm) confirm.TooltipText = "Confirm the trade";
    }

    public override void _EnterTree()
    {
        _game.Trade.Changed += Refresh;
        _game.Inventory.Changed += Refresh;
        _game.Character.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Trade.Changed -= Refresh;
        _game.Inventory.Changed -= Refresh;
        _game.Character.Changed -= Refresh;
    }

    private ItemSlot Cell(Control area, int slot, Func<int, GameItem> source, Action<int>? activate, Action<int, bool> hover)
    {
        var cell = new ItemSlot(slot)
        {
            Position = area.Position,
            Size = area.Size,
            Source = source,
            OnActivate = activate,
            OnDoubleClick = activate,
            OnHover = hover,
        };
        area.GetParent().AddChild(cell);
        return cell;
    }

    private static GameItem At(IReadOnlyList<GameItem> items, int index) =>
        index >= 0 && index < items.Count ? items[index] : GameItem.Empty(index);

    private void OfferFromBag(int slot)
    {
        if (!_game.Inventory.At(slot).IsEmpty) _game.Trade.Offer(slot);
    }

    private void Refresh()
    {
        var trade = _game.Trade;
        foreach (var cell in _bag) cell.Refresh();
        foreach (var cell in _mine) cell.Refresh();
        foreach (var cell in _theirs) cell.Refresh();
        _view.SetText("text_id_my", _game.Character.Name);
        _view.SetText("text_id_other", trade.Partner);
        _view.SetText("string_money_my", trade.MyGold.ToString("n0"));
        _view.SetText("string_money_other", trade.TheirGold.ToString("n0"));
        _view.SetText("string_money_inv", _game.Character.Gold.ToString("n0"));
        if (_view.Get(TheirReady) is { } ready) ready.Visible = trade.TheirConfirmed;
        if (_view.Get(TheirWaiting) is { } waiting) waiting.Visible = !trade.TheirConfirmed;
        if (_view.Get<BaseButton>("btn_trade_my") is { } confirm) confirm.Disabled = trade.MyConfirmed;
    }
}
