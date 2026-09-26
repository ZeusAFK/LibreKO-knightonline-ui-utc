using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Domain;
using LibreKO.Plugins;
using ItemSlot = KnightOnlineUi.Layout.ItemSlot;

namespace KnightOnlineUi.Windows;

public partial class InventoryWindow : Control
{
    private const string LayoutName = "re_inventory";
    private const int EquipAreaType = 1;
    private const int GridAreaType = 2;
    private const int CospreAreaType = 17;
    private const int BagAreaType = 18;
    private const int TitleBarHeight = 44;
    private const string CospreGroup = "base_cos";
    private static readonly Vector2 CarrySize = new(36, 36);
    private const int BagTabFontSize = 11;
    private static readonly Color BagTabTextColor = new(0.96f, 0.93f, 0.80f);

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly WindowHost _host;
    private readonly List<ItemSlot> _slots = new();
    private readonly List<ItemSlot> _bagSlots = new();
    private readonly Dictionary<int, ItemSlot> _bySlot = new();
    private readonly TextureRect _carry;
    private readonly int _leftPanelWidth;
    private bool _cospreOpen;
    private int _bag;
    private int _carried = -1;

    public InventoryWindow(WindowHost host)
    {
        _host = host;
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        _leftPanelWidth = layout.Images.Where(i => i.Id.Length == 0).Select(i => i.X).DefaultIfEmpty(0).Min();
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        _view.Hide(CospreGroup, "base_samma", "base_bag", "base_inven_notice", "base_LockImg", "elmo_ecli666",
            "btn_helmetview", "btn_gold", "btn_trashlock", "btn_CosPreView", "btn_talisman", "btn_FairyView",
            "btn_emblePreView", "btn_WingPreView_0", "btn_WingPreView_1", "btn_WingPreView_2", "img_hide");

        host.SetDragHandle(_view.MakeDragHandle(new Rect2(_leftPanelWidth, 0, _view.Size.X - _leftPanelWidth, TitleBarHeight)));
        _view.OnPressed("btn_close", host.Close);
        _view.OnPressed("btn_open_cos", () => SetCospreOpen(!_cospreOpen));
        _view.OnPressed("btn_close_cos", () => SetCospreOpen(false));
        _view.OnPressed("btn_item_arrangement", () => _game.Inventory.Arrange());
        var bagTabs = new ButtonGroup();
        for (int bag = 0; bag < InventoryConstants.BagSlotMax; bag++)
        {
            int which = bag;
            string id = $"btn_bag{bag + 1}";
            if (_view.Get<TextureButton>(id) is { } tab)
            {
                tab.ToggleMode = true;
                tab.ButtonGroup = bagTabs;
                tab.ButtonPressed = bag == 0;
            }
            _view.OnPressed(id, () => SelectBag(which));
            _view.Caption(id, $"Bag{bag + 1}", BagTabFontSize, BagTabTextColor);
        }

        foreach (var (node, control) in _view.Areas(EquipAreaType))
            if (int.TryParse(node.Id, out int equip) && equip < InventoryConstants.SlotMax)
                _slots.Add(BindSlot(control, equip));
        foreach (var (node, control) in _view.Areas(GridAreaType))
            _slots.Add(BindSlot(control, InventoryConstants.InventoryStart + int.Parse(node.Id)));
        foreach (var (node, control) in _view.Areas(CospreAreaType))
            _slots.Add(BindSlot(control, InventoryConstants.CospreStart + int.Parse(node.Id)));
        foreach (var (node, control) in _view.Areas(BagAreaType))
            _bagSlots.Add(BindSlot(control, InventoryConstants.MagicBagStart + int.Parse(node.Id)));

        if (_view.Get("area_samma") is { } trash)
        {
            var target = new DropTarget
            {
                Position = trash.Position,
                Size = trash.Size,
                OnDrop = from => _game.Inventory.Drop(from),
                OnClick = () => { if (_carried < 0) return; int from = _carried; CancelCarry(); _game.Inventory.Drop(from); },
            };
            trash.GetParent().AddChild(target);
        }

        _carry = new TextureRect
        {
            Visible = false,
            Size = CarrySize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 10,
        };
        AddChild(_carry);
        SetProcess(false);
        SetCospreOpen(false);
        Refresh();
    }

    private ItemSlot BindSlot(Control area, int slot)
    {
        var cell = new ItemSlot(slot)
        {
            Position = area.Position,
            Size = area.Size,
            Source = s => _game.Inventory.At(s),
            OnDrop = (from, to) => { CancelCarry(); _game.Inventory.Move(from, to); },
            OnClick = SlotClicked,
            OnActivate = SlotActivated,
            OnDoubleClick = s => { CancelCarry(); _game.Inventory.Use(s); },
            OnHover = (s, over) => { if (over) _game.Inventory.ShowTooltip(s); else _game.Inventory.HideTooltip(); },
        };
        area.GetParent().AddChild(cell);
        _bySlot[slot] = cell;
        return cell;
    }

    private void SlotClicked(int slot)
    {
        if (_carried < 0)
        {
            if (_game.Inventory.At(slot).IsEmpty) return;
            _carried = slot;
            _carry.Texture = _game.Inventory.At(slot).Icon;
            _carry.Visible = true;
            _carry.Position = GetLocalMousePosition() - CarrySize * 0.5f;
            if (_bySlot.TryGetValue(slot, out var cell)) cell.SetCarried(true);
            _game.Inventory.HideTooltip();
            SetProcess(true);
            return;
        }
        int from = _carried;
        CancelCarry();
        if (from != slot) _game.Inventory.Move(from, slot);
    }

    private void SlotActivated(int slot)
    {
        if (_carried >= 0) { CancelCarry(); return; }
        _game.Inventory.Use(slot);
    }

    private void CancelCarry()
    {
        if (_carried >= 0 && _bySlot.TryGetValue(_carried, out var cell)) cell.SetCarried(false);
        _carried = -1;
        _carry.Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (_carried < 0) return;
        _carry.Position = GetLocalMousePosition() - CarrySize * 0.5f;
    }

    public override void _Input(InputEvent ev)
    {
        if (_carried < 0 || ev is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
        CancelCarry();
        GetViewport().SetInputAsHandled();
    }

    private void SetCospreOpen(bool open)
    {
        bool wasOpen = _cospreOpen;
        _cospreOpen = open;
        if (_view.Get(CospreGroup) is { } cos) cos.Visible = open;
        float width = open ? _view.Size.X : _view.Size.X - _leftPanelWidth;
        _view.Position = new Vector2(open ? 0 : -_leftPanelWidth, 0);
        CustomMinimumSize = new Vector2(width, _view.Size.Y);
        Size = CustomMinimumSize;
        if (wasOpen != open && IsInsideTree())
            _host.Window.Position += new Vector2(open ? -_leftPanelWidth : _leftPanelWidth, 0);
    }

    private void SelectBag(int bag)
    {
        _bag = Mathf.Clamp(bag, 0, InventoryConstants.BagSlotMax - 1);
        RefreshBags();
    }

    private void RefreshBags()
    {
        for (int i = 0; i < _bagSlots.Count; i++)
        {
            var cell = _bagSlots[i];
            int slot = InventoryConstants.MagicBagStart + _bag * InventoryConstants.MagicBagMax + i;
            cell.Source = _ => _game.Inventory.At(slot);
            cell.OnDrop = (from, _) => { CancelCarry(); _game.Inventory.Move(from, slot); };
            cell.OnClick = _ => SlotClicked(slot);
            cell.OnActivate = _ => SlotActivated(slot);
            cell.OnDoubleClick = _ => { CancelCarry(); _game.Inventory.Use(slot); };
            cell.OnHover = (_, over) => { if (over) _game.Inventory.ShowTooltip(slot); else _game.Inventory.HideTooltip(); };
            cell.Refresh();
        }
    }

    public override void _EnterTree()
    {
        _game.Inventory.Changed += Refresh;
        _game.Character.Changed += RefreshTotals;
        _game.BecameAvailable += Refresh;
        _host.Shown += Refresh;
        _host.Hidden += OnHidden;
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Inventory.Changed -= Refresh;
        _game.Character.Changed -= RefreshTotals;
        _game.BecameAvailable -= Refresh;
        _host.Shown -= Refresh;
        _host.Hidden -= OnHidden;
    }

    private void OnHidden()
    {
        CancelCarry();
        _game.Inventory.HideTooltip();
    }

    private void Refresh()
    {
        foreach (var cell in _slots) cell.Refresh();
        GhostOtherHand(InventoryConstants.RightHand, InventoryConstants.LeftHand);
        GhostOtherHand(InventoryConstants.LeftHand, InventoryConstants.RightHand);
        RefreshBags();
        RefreshTotals();
        if (_carried >= 0 && _game.Inventory.At(_carried).IsEmpty) CancelCarry();
    }

    private void GhostOtherHand(int hand, int other)
    {
        var item = _game.Inventory.At(hand);
        if (!item.TwoHanded || !_bySlot.TryGetValue(other, out var cell)) return;
        cell.SetGhost(item.Icon);
    }

    private void RefreshTotals()
    {
        var c = _game.Character;
        _view.SetFittedText("text_gold", c.Gold.ToString("n0"));
        _view.SetFittedText("text_weight", c.MaxWeight > 0 ? $"{c.Weight / 10f:0.0}/{c.MaxWeight / 10f:0.0}" : "");
        _view.SetText("text_id_weight", "Weight");
        _view.SetText("text_id_title", "INVENTORY");
    }
}
