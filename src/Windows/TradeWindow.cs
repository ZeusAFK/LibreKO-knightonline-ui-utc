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

    private readonly WindowHost host;
    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly List<ItemSlot> bag = new();
    private readonly List<ItemSlot> mine = new();
    private readonly List<ItemSlot> theirs = new();

    public TradeWindow(WindowHost host)
    {
        this.host = host;
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        view.Hide("list_iteminfo", "btn_gold2");
        foreach (var (node, control) in view.Where(n => n.IsString && int.TryParse(n.Id, out _)))
            control.Visible = false;

        foreach (var (node, control) in view.Where(n => n.IsArea && int.TryParse(n.Id, out _)).ToList())
        {
            int index = int.Parse(node.Id);
            if (node.Y >= BagTop && index < BagCells)
                bag.Add(Cell(control, InventoryConstants.InventoryStart + index, s => game.Inventory.At(s), OfferFromBag,
                    (s, over) => { if (over) game.Inventory.ShowTooltip(s); else game.Inventory.HideTooltip(); }));
            else if (node.Y < BagTop && node.X < OfferSplitX)
                mine.Add(Cell(control, index, s => At(game.Trade.Mine, s), null,
                    (s, over) => { if (over) game.Trade.ShowTooltip(true, s); else game.Trade.HideTooltip(); }));
            else if (node.Y < BagTop)
                theirs.Add(Cell(control, index, s => At(game.Trade.Theirs, s), null,
                    (s, over) => { if (over) game.Trade.ShowTooltip(false, s); else game.Trade.HideTooltip(); }));
        }
        mine.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        theirs.Sort((a, b) => a.Slot.CompareTo(b.Slot));

        if (view.Get("area_my") is { } dropArea)
        {
            var drop = new DropTarget { Position = dropArea.Position, Size = dropArea.Size, OnDrop = from => game.Trade.Offer(from) };
            dropArea.GetParent().AddChild(drop);
            dropArea.GetParent().MoveChild(drop, 0);
        }

        view.OnPressed("btn_close", () => game.Trade.Cancel());
        view.OnPressed("btn_gold", () => game.Trade.AskGold());
        view.OnPressed("btn_trade_my", () => game.Trade.Confirm());
        if (view.Get("btn_gold") is { } gold) gold.TooltipText = "Offer gold";
        if (view.Get("btn_trade_my") is { } confirm) confirm.TooltipText = "Confirm the trade";
    }

    public override void _EnterTree()
    {
        game.Trade.Changed += Refresh;
        game.Inventory.Changed += Refresh;
        game.Character.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Trade.Changed -= Refresh;
        game.Inventory.Changed -= Refresh;
        game.Character.Changed -= Refresh;
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
        if (!game.Inventory.At(slot).IsEmpty) game.Trade.Offer(slot);
    }

    private void Refresh()
    {
        var trade = game.Trade;
        foreach (var cell in bag) cell.Refresh();
        foreach (var cell in mine) cell.Refresh();
        foreach (var cell in theirs) cell.Refresh();
        view.SetText("text_id_my", game.Character.Name);
        view.SetText("text_id_other", trade.Partner);
        view.SetText("string_money_my", trade.MyGold.ToString("n0"));
        view.SetText("string_money_other", trade.TheirGold.ToString("n0"));
        view.SetText("string_money_inv", game.Character.Gold.ToString("n0"));
        if (view.Get(TheirReady) is { } ready) ready.Visible = trade.TheirConfirmed;
        if (view.Get(TheirWaiting) is { } waiting) waiting.Visible = !trade.TheirConfirmed;
        if (view.Get<BaseButton>("btn_trade_my") is { } confirm) confirm.Disabled = trade.MyConfirmed;
    }
}
