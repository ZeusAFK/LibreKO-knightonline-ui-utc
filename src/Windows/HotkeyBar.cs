using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class HotkeyBar : Control
{
    private const string LayoutName = "re_hotkey";
    private const string HorizontalGroup = "Hotkey_Horizontal";
    private const string VerticalGroup = "Hotkey_vertical";
    private const string SlotNumbers = "Group_SlotNumber_Img";
    private const string ExpandButton = "btn_open";
    private const string CollapseButton = "btn_close";
    private const string LayoutId = "theme_hotbar";
    private const string SettingVertical = "hotbar.vertical";
    private const string SettingExtraPages = "hotbar.extras";
    private const int SlotAreaType = 8;
    private const int HorizontalSlotIdBase = 10;
    private const int MaxBars = 8;
    private const float PageNumberPad = 4f;
    private const float BottomBarHeight = 108f;
    private const float GapAboveBar = 2f;
    private static readonly Vector2 ColumnsAt = new(256f, 8f);

    private sealed class Strip
    {
        public LayoutView View = null!;
        public Func<int> PageOf = () => 0;
        public DrawnText? PageNumber;
        public readonly List<HotSlot> Slots = new();
    }

    private readonly UiKit _kit;
    private readonly PluginGame _game;
    private readonly PluginSettings _settings;
    private readonly LayoutNode _layout;
    private readonly LayoutNode _hGroup;
    private readonly LayoutNode _vGroup;
    private readonly List<Strip> _strips = new();
    private readonly List<int> _extraPages = new();
    private readonly Control _grip;
    private bool _vertical;
    private float _mainTop;
    private HudLayout? _hudLayout;

    public HotkeyBar()
    {
        _kit = Plugin.Kit;
        _game = _kit.Game;
        _settings = _kit.Context.Settings;
        _layout = _kit.Layout(LayoutName);
        _hGroup = _layout.Find(HorizontalGroup) ?? _layout;
        _vGroup = _layout.Find(VerticalGroup) ?? _layout;
        _vertical = _settings.GetBool(SettingVertical, false);
        foreach (var part in _settings.Get(SettingExtraPages).Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(part, out int page) && _extraPages.Count < MaxBars - 1) _extraPages.Add(page);
        MouseFilter = MouseFilterEnum.Ignore;
        _grip = new Control { Name = "grip", MouseFilter = MouseFilterEnum.Stop };
        AddChild(_grip);
        Build();
    }

    public override void _EnterTree()
    {
        _game.Hotbar.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _grip, DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Hotbar.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
    }

    public override void _Process(double delta)
    {
        foreach (var strip in _strips)
            foreach (var slot in strip.Slots)
                slot.TickCooldown();
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return _vertical
            ? ColumnsAt
            : new Vector2(Mathf.Round((room.X - Size.X) * 0.5f), room.Y - BottomBarHeight - Size.Y - GapAboveBar);
    }

    private void Build()
    {
        foreach (var strip in _strips) strip.View.QueueFree();
        _strips.Clear();
        var group = _vertical ? _vGroup : _hGroup;
        int extras = _extraPages.Count;
        float previousMainTop = _mainTop;
        _mainTop = _vertical ? 0f : extras * group.H;
        AddStrip(group, _vertical ? Vector2.Zero : new Vector2(0, _mainTop), () => _game.Hotbar.Page, _vertical ? 0 : HorizontalSlotIdBase, extra: -1);
        for (int i = 0; i < extras; i++)
        {
            int index = i;
            var at = _vertical ? new Vector2((i + 1) * group.W, 0) : new Vector2(0, (extras - 1 - i) * group.H);
            AddStrip(group, at, () => _extraPages[index], _vertical ? 0 : HorizontalSlotIdBase, extra: index);
        }
        Size = _vertical ? new Vector2((extras + 1) * group.W, group.H) : new Vector2(group.W, (extras + 1) * group.H);
        if (IsInsideTree())
        {
            if (!Config.HasWindowPos(LayoutId)) Position = DefaultPosition();
            else Position += new Vector2(0, previousMainTop - _mainTop);
            var room = GetViewport().GetVisibleRect().Size;
            Position = new Vector2(Mathf.Clamp(Position.X, 0f, Mathf.Max(0f, room.X - Size.X)),
                Mathf.Clamp(Position.Y, 0f, Mathf.Max(0f, room.Y - Size.Y)));
        }
        Refresh();
        _settings.SetBool(SettingVertical, _vertical);
        _settings.Set(SettingExtraPages, string.Join(",", _extraPages));
    }

    private Rect2 GripRect(LayoutNode group)
    {
        if (group == _hGroup && _layout.HasDrag)
            return new Rect2(_layout.DragX - group.X, _layout.DragY - group.Y, _layout.DragW, _layout.DragH);
        var handle = group.Images.FirstOrDefault(i => i.Id.Length == 0) ?? group;
        return new Rect2(handle.X - group.X, handle.Y - group.Y, handle.W, handle.H);
    }

    private void AddStrip(LayoutNode group, Vector2 at, Func<int> pageOf, int slotIdBase, int extra)
    {
        var view = new LayoutView(_kit, _layout);
        view.Hide(group == _hGroup ? VerticalGroup : HorizontalGroup);
        view.Position = at - new Vector2(group.X - _layout.X, group.Y - _layout.Y);
        AddChild(view);
        var strip = new Strip { View = view, PageOf = pageOf };

        if (view.ControlOf(group) is { } groupControl)
        {
            var rect = GripRect(group);
            var relay = new Control
            {
                Name = "drag",
                Position = rect.Position,
                Size = rect.Size,
                MouseFilter = MouseFilterEnum.Stop,
                MouseDefaultCursorShape = CursorShape.Move,
            };
            relay.GuiInput += ev =>
            {
                if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left } press) return;
                _grip.EmitSignal(Control.SignalName.GuiInput, press);
                relay.AcceptEvent();
            };
            groupControl.AddChild(relay);
            groupControl.MoveChild(relay, 0);
        }

        string pageId = group == _hGroup ? "strpage1" : "strpage0";
        if (group.Find(pageId) is { } pageNode && view.ControlOf(pageNode) is { } pageLabel)
        {
            pageLabel.Visible = false;
            strip.PageNumber = new DrawnText(_kit.FontFor(pageNode), UiKit.FontSize(pageNode), pageNode.Color)
            {
                Position = pageLabel.Position - new Vector2(PageNumberPad, 0),
                Size = pageNode.SizeVec + new Vector2(2 * PageNumberPad, 0),
            };
            pageLabel.GetParent().AddChild(strip.PageNumber);
        }

        int perPage = _game.Hotbar.SlotsPerPage;
        foreach (var node in group.All())
        {
            if (!node.IsArea || node.AreaType != SlotAreaType || !int.TryParse(node.Id, out int id)) continue;
            int slotInPage = id - slotIdBase;
            if (slotInPage < 0 || slotInPage >= perPage) continue;
            if (view.ControlOf(node) is not { } control) continue;
            var cell = new HotSlot(slotInPage)
            {
                Position = control.Position,
                Size = control.Size,
                AbsOf = s => pageOf() * perPage + s,
                Source = abs => _game.Hotbar.Slot(abs),
                OnActivate = abs => _game.Hotbar.ActivateAbs(abs),
                OnDrop = (abs, itemId, from) => _game.Hotbar.Drop(abs, itemId, from),
                OnClear = abs => _game.Hotbar.Clear(abs),
                OnHover = (abs, over) => { if (over) _game.Hotbar.ShowTooltip(abs); else _game.Hotbar.HideTooltip(); },
            };
            control.GetParent().AddChild(cell);
            strip.Slots.Add(cell);
        }
        if (view.Get(group, SlotNumbers) is { } numbers) numbers.GetParent().MoveChild(numbers, -1);

        string expand = group == _hGroup ? CollapseButton : ExpandButton;
        string collapse = group == _hGroup ? ExpandButton : CollapseButton;
        if (extra < 0)
        {
            view.OnPressed(group, "btn_up", () => _game.Hotbar.ChangePage(-1));
            view.OnPressed(group, "btn_down", () => _game.Hotbar.ChangePage(1));
            view.OnPressed(group, expand, AddBar);
            view.OnPressed(group, "btn_change", ToggleOrientation);
            view.Hide(group, collapse);
            if (_extraPages.Count >= MaxBars - 1) view.Hide(group, expand);
        }
        else
        {
            view.OnPressed(group, "btn_up", () => TurnExtra(extra, -1));
            view.OnPressed(group, "btn_down", () => TurnExtra(extra, 1));
            view.OnPressed(group, collapse, () => RemoveBar(extra));
            view.Hide(group, expand, "btn_change");
        }
        view.OnPressed(group, "btn_lock", () => _game.Hotbar.SetLocked(!_game.Hotbar.Locked));
        _strips.Add(strip);
    }

    private void AddBar()
    {
        if (_extraPages.Count >= MaxBars - 1) return;
        int last = _extraPages.Count > 0 ? _extraPages[^1] : _game.Hotbar.Page;
        _extraPages.Add((last + 1) % _game.Hotbar.Pages);
        Build();
    }

    private void RemoveBar(int index)
    {
        if (index < 0 || index >= _extraPages.Count) return;
        _extraPages.RemoveAt(index);
        Build();
    }

    private void TurnExtra(int index, int delta)
    {
        int pages = _game.Hotbar.Pages;
        _extraPages[index] = ((_extraPages[index] + delta) % pages + pages) % pages;
        _settings.Set(SettingExtraPages, string.Join(",", _extraPages));
        Refresh();
    }

    private void ToggleOrientation()
    {
        _vertical = !_vertical;
        Build();
    }

    private void Refresh()
    {
        foreach (var strip in _strips)
        {
            foreach (var slot in strip.Slots) slot.Refresh();
            if (strip.PageNumber != null) strip.PageNumber.Text = (strip.PageOf() + 1).ToString();
        }
    }
}

public partial class HotSlot : Control
{
    private const string DragKeyItem = "id";
    private const string DragKeyBar = "barAbs";
    private const int CountFontSize = 11;
    private const int CountInset = 2;
    private const int CountOutline = 3;
    private static readonly Color CooldownShade = new(0f, 0f, 0f, 0.62f);
    private static readonly Color MissingTint = new(0.45f, 0.45f, 0.45f, 1f);

    public int SlotInPage { get; }
    public Func<int, int> AbsOf { get; set; } = s => s;
    public Func<int, HotSlotInfo> Source { get; set; } = abs => HotSlotInfo.Empty(abs);
    public Action<int>? OnActivate { get; set; }
    public Action<int, int, int>? OnDrop { get; set; }
    public Action<int>? OnClear { get; set; }
    public Action<int, bool>? OnHover { get; set; }

    private readonly TextureRect _icon;
    private readonly ColorRect _shade;
    private readonly Label _count;
    private HotSlotInfo _current;

    private int Abs => AbsOf(SlotInPage);

    public HotSlot(int slotInPage)
    {
        SlotInPage = slotInPage;
        MouseFilter = MouseFilterEnum.Stop;
        _icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _icon.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_icon);
        _shade = new ColorRect { Color = CooldownShade, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _shade.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_shade);
        _count = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _count.SetAnchorsPreset(LayoutPreset.FullRect);
        _count.OffsetRight = -CountInset;
        _count.OffsetBottom = -CountInset;
        _count.AddThemeFontSizeOverride("font_size", CountFontSize);
        _count.AddThemeColorOverride("font_color", Colors.White);
        _count.AddThemeColorOverride("font_outline_color", Colors.Black);
        _count.AddThemeConstantOverride("outline_size", CountOutline);
        AddChild(_count);
        MouseEntered += () => { if (!_current.IsEmpty) OnHover?.Invoke(Abs, true); };
        MouseExited += () => OnHover?.Invoke(Abs, false);
    }

    public void Refresh()
    {
        _current = Source(Abs);
        _icon.Texture = _current.IsEmpty ? null : _current.Icon;
        _icon.Modulate = _current.Enough ? Colors.White : MissingTint;
        _count.Text = _current.Count >= 0 ? _current.Count.ToString() : "";
        TooltipText = _current.Tooltip.Length > 0 ? _current.Tooltip : _current.Name;
        TickCooldown();
    }

    public void TickCooldown()
    {
        if (_current.IsEmpty) { _shade.Visible = false; return; }
        float frac = Source(Abs).Cooldown;
        _shade.Visible = frac > 0.001f;
        if (_shade.Visible)
        {
            _shade.AnchorTop = 1f - Mathf.Clamp(frac, 0f, 1f);
            _shade.OffsetTop = 0f;
        }
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } mb) return;
        if (mb.ButtonIndex == MouseButton.Left) { OnActivate?.Invoke(Abs); AcceptEvent(); }
        else if (mb.ButtonIndex == MouseButton.Right) { OnClear?.Invoke(Abs); AcceptEvent(); }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_current.IsEmpty) return default;
        var preview = new TextureRect
        {
            Texture = _current.Icon,
            CustomMinimumSize = Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary { { DragKeyItem, _current.Id }, { DragKeyBar, _current.Abs } };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        OnDrop != null && data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey(DragKeyItem);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var d = data.AsGodotDictionary();
        int from = d.ContainsKey(DragKeyBar) ? d[DragKeyBar].AsInt32() : -1;
        OnDrop?.Invoke(Abs, d[DragKeyItem].AsInt32(), from);
    }
}
