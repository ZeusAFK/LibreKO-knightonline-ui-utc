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

    private readonly UiKit kit;
    private readonly PluginGame game;
    private readonly PluginSettings settings;
    private readonly LayoutNode layout;
    private readonly LayoutNode hGroup;
    private readonly LayoutNode vGroup;
    private readonly List<Strip> strips = new();
    private readonly List<int> extraPages = new();
    private readonly List<TextureButton> lockButtons = new();
    private readonly Control grip;
    private bool vertical;
    private float mainTop;
    private HudLayout? hudLayout;

    public HotkeyBar()
    {
        kit = Plugin.Kit;
        game = kit.Game;
        settings = kit.Context.Settings;
        layout = kit.Layout(LayoutName);
        hGroup = layout.Find(HorizontalGroup) ?? layout;
        vGroup = layout.Find(VerticalGroup) ?? layout;
        vertical = settings.GetBool(SettingVertical, false);
        foreach (var part in settings.Get(SettingExtraPages).Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(part, out int page) && extraPages.Count < MaxBars - 1) extraPages.Add(page);
        MouseFilter = MouseFilterEnum.Ignore;
        grip = new Control { Name = "grip", MouseFilter = MouseFilterEnum.Stop };
        AddChild(grip);
        Build();
    }

    public override void _EnterTree()
    {
        game.Hotbar.Changed += Refresh;
        game.Hotbar.Ready += FlashReady;
        game.BecameAvailable += Refresh;
        hudLayout ??= HudLayout.Attach(this, LayoutId, grip, DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Hotbar.Changed -= Refresh;
        game.Hotbar.Ready -= FlashReady;
        game.BecameAvailable -= Refresh;
    }

    public override void _Process(double delta)
    {
        foreach (var strip in strips)
            foreach (var slot in strip.Slots)
                slot.TickCooldown();
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return vertical
            ? ColumnsAt
            : new Vector2(Mathf.Round((room.X - Size.X) * 0.5f), room.Y - BottomBarHeight - Size.Y - GapAboveBar);
    }

    private void Build()
    {
        foreach (var strip in strips) strip.View.QueueFree();
        strips.Clear();
        lockButtons.Clear();
        var group = vertical ? vGroup : hGroup;
        int extras = extraPages.Count;
        float previousMainTop = mainTop;
        mainTop = vertical ? 0f : extras * group.H;
        AddStrip(group, vertical ? Vector2.Zero : new Vector2(0, mainTop), () => game.Hotbar.Page, vertical ? 0 : HorizontalSlotIdBase, extra: -1);
        for (int i = 0; i < extras; i++)
        {
            int index = i;
            var at = vertical ? new Vector2((i + 1) * group.W, 0) : new Vector2(0, (extras - 1 - i) * group.H);
            AddStrip(group, at, () => extraPages[index], vertical ? 0 : HorizontalSlotIdBase, extra: index);
        }
        Size = vertical ? new Vector2((extras + 1) * group.W, group.H) : new Vector2(group.W, (extras + 1) * group.H);
        if (IsInsideTree())
        {
            if (!Config.HasWindowPos(LayoutId)) Position = DefaultPosition();
            else Position += new Vector2(0, previousMainTop - mainTop);
            var room = GetViewport().GetVisibleRect().Size;
            Position = new Vector2(Mathf.Clamp(Position.X, 0f, Mathf.Max(0f, room.X - Size.X)),
                Mathf.Clamp(Position.Y, 0f, Mathf.Max(0f, room.Y - Size.Y)));
        }
        Refresh();
        settings.SetBool(SettingVertical, vertical);
        settings.Set(SettingExtraPages, string.Join(",", extraPages));
    }

    private Rect2 GripRect(LayoutNode group)
    {
        if (group == hGroup && layout.HasDrag)
            return new Rect2(layout.DragX - group.X, layout.DragY - group.Y, layout.DragW, layout.DragH);
        var handle = group.Images.FirstOrDefault(i => i.Id.Length == 0) ?? group;
        return new Rect2(handle.X - group.X, handle.Y - group.Y, handle.W, handle.H);
    }

    private void AddStrip(LayoutNode group, Vector2 at, Func<int> pageOf, int slotIdBase, int extra)
    {
        var view = new LayoutView(kit, layout);
        view.Hide(group == hGroup ? VerticalGroup : HorizontalGroup);
        view.Position = at - new Vector2(group.X - layout.X, group.Y - layout.Y);
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
                grip.EmitSignal(Control.SignalName.GuiInput, press);
                relay.AcceptEvent();
            };
            groupControl.AddChild(relay);
            groupControl.MoveChild(relay, 0);
        }

        string pageId = group == hGroup ? "strpage1" : "strpage0";
        if (group.Find(pageId) is { } pageNode && view.ControlOf(pageNode) is { } pageLabel)
        {
            pageLabel.Visible = false;
            strip.PageNumber = new DrawnText(kit.FontFor(pageNode), UiKit.FontSize(pageNode), pageNode.Color)
            {
                Position = pageLabel.Position - new Vector2(PageNumberPad, 0),
                Size = pageNode.SizeVec + new Vector2(2 * PageNumberPad, 0),
            };
            pageLabel.GetParent().AddChild(strip.PageNumber);
        }

        int perPage = game.Hotbar.SlotsPerPage;
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
                Source = abs => game.Hotbar.Slot(abs),
                OnActivate = abs => game.Hotbar.ActivateAbs(abs),
                OnDrop = (abs, itemId, from) => game.Hotbar.Drop(abs, itemId, from),
                OnClear = abs => game.Hotbar.Clear(abs),
                OnSelect = abs => game.Hotbar.Select(abs),
                SelectedAbs = () => game.Hotbar.Selected,
                Locked = () => game.Hotbar.Locked,
                OnHover = (abs, over) => { if (over) game.Hotbar.ShowTooltip(abs); else game.Hotbar.HideTooltip(); },
            };
            control.GetParent().AddChild(cell);
            strip.Slots.Add(cell);
        }
        if (view.Get(group, SlotNumbers) is { } numbers) numbers.GetParent().MoveChild(numbers, -1);

        string expand = group == hGroup ? CollapseButton : ExpandButton;
        string collapse = group == hGroup ? ExpandButton : CollapseButton;
        if (extra < 0)
        {
            view.OnPressed(group, "btn_up", () => game.Hotbar.ChangePage(-1));
            view.OnPressed(group, "btn_down", () => game.Hotbar.ChangePage(1));
            view.OnPressed(group, expand, AddBar);
            view.OnPressed(group, "btn_change", ToggleOrientation);
            view.Hide(group, collapse);
            if (extraPages.Count >= MaxBars - 1) view.Hide(group, expand);
        }
        else
        {
            view.OnPressed(group, "btn_up", () => TurnExtra(extra, -1));
            view.OnPressed(group, "btn_down", () => TurnExtra(extra, 1));
            view.OnPressed(group, collapse, () => RemoveBar(extra));
            view.Hide(group, expand, "btn_change");
        }
        view.OnPressed(group, "btn_lock", () => game.Hotbar.SetLocked(!game.Hotbar.Locked));
        if (view.Get<TextureButton>(group, "btn_lock") is { } lockButton)
        {
            lockButton.ToggleMode = true;
            lockButtons.Add(lockButton);
        }
        strips.Add(strip);
    }

    private void AddBar()
    {
        if (extraPages.Count >= MaxBars - 1) return;
        int last = extraPages.Count > 0 ? extraPages[^1] : game.Hotbar.Page;
        extraPages.Add((last + 1) % game.Hotbar.Pages);
        Build();
    }

    private void RemoveBar(int index)
    {
        if (index < 0 || index >= extraPages.Count) return;
        extraPages.RemoveAt(index);
        Build();
    }

    private void TurnExtra(int index, int delta)
    {
        int pages = game.Hotbar.Pages;
        extraPages[index] = ((extraPages[index] + delta) % pages + pages) % pages;
        settings.Set(SettingExtraPages, string.Join(",", extraPages));
        Refresh();
    }

    private void ToggleOrientation()
    {
        vertical = !vertical;
        Build();
    }

    private void FlashReady(IReadOnlyList<int> skillIds)
    {
        foreach (var strip in strips)
            foreach (var slot in strip.Slots)
                if (skillIds.Contains(slot.ShownId)) slot.Flash();
    }

    private void Refresh()
    {
        foreach (var button in lockButtons)
            if (IsInstanceValid(button)) button.SetPressedNoSignal(!game.Hotbar.Locked);
        foreach (var strip in strips)
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
    private static readonly Color MissingTint = new(0.45f, 0.45f, 0.45f, 1f);

    public int SlotInPage { get; }
    public Func<int, int> AbsOf { get; set; } = s => s;
    public Func<int, HotSlotInfo> Source { get; set; } = abs => HotSlotInfo.Empty(abs);
    public Action<int>? OnActivate { get; set; }
    public Action<int, int, int>? OnDrop { get; set; }
    public Action<int>? OnClear { get; set; }
    public Action<int>? OnSelect { get; set; }
    public Action<int, bool>? OnHover { get; set; }
    public Func<int> SelectedAbs { get; set; } = () => -1;
    public Func<bool> Locked { get; set; } = () => false;

    private readonly TextureRect icon;
    private readonly HotSlotOverlay overlay;
    private readonly Label count;
    private HotSlotInfo current;
    private bool pressed;
    private bool dragging;

    private int Abs => AbsOf(SlotInPage);

    public int ShownId => current.Id;

    public HotSlot(int slotInPage)
    {
        SlotInPage = slotInPage;
        MouseFilter = MouseFilterEnum.Stop;
        icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(icon);
        overlay = new HotSlotOverlay();
        AddChild(overlay);
        count = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        count.SetAnchorsPreset(LayoutPreset.FullRect);
        count.OffsetRight = -CountInset;
        count.OffsetBottom = -CountInset;
        count.AddThemeFontSizeOverride("font_size", CountFontSize);
        count.AddThemeColorOverride("font_color", Colors.White);
        count.AddThemeColorOverride("font_outline_color", Colors.Black);
        count.AddThemeConstantOverride("outline_size", CountOutline);
        AddChild(count);
        MouseEntered += () => { if (!current.IsEmpty) OnHover?.Invoke(Abs, true); };
        MouseExited += () => OnHover?.Invoke(Abs, false);
    }

    public void Refresh()
    {
        current = Source(Abs);
        icon.Texture = current.IsEmpty ? null : current.Icon;
        icon.Modulate = current.Enough ? Colors.White : MissingTint;
        count.Text = current.Count >= 0 ? current.Count.ToString() : "";
        TooltipText = current.Tooltip.Length > 0 ? current.Tooltip : current.Name;
        overlay.Selected = !current.IsEmpty && SelectedAbs() == Abs;
        TickCooldown();
    }

    public void Flash() => overlay.Flash();

    public void TickCooldown()
    {
        if (current.IsEmpty) { overlay.Remain = 0f; return; }
        var live = Source(Abs);
        if (live.Id != current.Id) { Refresh(); return; }
        if (live.Count != current.Count || live.Enough != current.Enough)
        {
            current = live;
            icon.Modulate = live.Enough ? Colors.White : MissingTint;
            count.Text = live.Count >= 0 ? live.Count.ToString() : "";
        }
        overlay.Remain = live.Cooldown;
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton mb) return;
        if (mb.ButtonIndex == MouseButton.Right)
        {
            if (mb.Pressed) OnSelect?.Invoke(Abs);
            AcceptEvent();
            return;
        }
        if (mb.ButtonIndex != MouseButton.Left) return;
        if (mb.Pressed) pressed = true;
        else if (pressed) { pressed = false; OnActivate?.Invoke(Abs); }
        AcceptEvent();
    }

    public override void _Notification(int what)
    {
        switch ((long)what)
        {
            case NotificationDragEnd:
                if (!dragging) return;
                dragging = false;
                if (!IsDragSuccessful()) OnClear?.Invoke(Abs);
                return;
            case NotificationMouseExit:
                pressed = false;
                return;
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (current.IsEmpty || Locked()) return default;
        pressed = false;
        dragging = true;
        var preview = new TextureRect
        {
            Texture = current.Icon,
            CustomMinimumSize = Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary { { DragKeyItem, current.Id }, { DragKeyBar, current.Abs } };
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
