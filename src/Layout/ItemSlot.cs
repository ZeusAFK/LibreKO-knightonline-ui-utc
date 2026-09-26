using Godot;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUi.Layout;

public partial class ItemSlot : Control
{
    public const string DragKeyFrom = "invFrom";
    public const string DragKeyItem = "id";
    private const int IconInset = 2;
    private const int CountFontSize = 11;
    private const int CountInset = 2;
    private static readonly Color HoverTint = new(1f, 1f, 0.8f, 0.12f);
    private static readonly Color CarriedTint = new(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Color GhostTint = new(1f, 1f, 1f, 0.35f);

    public int Slot { get; }
    public Func<int, GameItem> Source { get; set; } = s => GameItem.Empty(s);
    public Action<int, int>? OnDrop { get; set; }
    public Action<int>? OnClick { get; set; }
    public Action<int>? OnActivate { get; set; }
    public Action<int>? OnDoubleClick { get; set; }
    public Action<int, bool>? OnHover { get; set; }
    public GameItem Current { get; private set; }

    private readonly TextureRect _icon;
    private readonly Label _count;
    private readonly ColorRect _hover;
    private readonly UpgradeBadge _badge;
    private bool _suppressClick;

    public ItemSlot(int slot)
    {
        Slot = slot;
        MouseFilter = MouseFilterEnum.Stop;
        _hover = new ColorRect { Color = HoverTint, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _hover.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_hover);
        _icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _icon.SetAnchorsPreset(LayoutPreset.FullRect);
        _icon.OffsetLeft = IconInset; _icon.OffsetTop = IconInset;
        _icon.OffsetRight = -IconInset; _icon.OffsetBottom = -IconInset;
        AddChild(_icon);
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
        _count.AddThemeConstantOverride("outline_size", 3);
        AddChild(_count);
        _badge = UpgradeBadge.Attach(this);
        MouseEntered += () => { _hover.Visible = true; if (!Current.IsEmpty) OnHover?.Invoke(Slot, true); };
        MouseExited += () => { _hover.Visible = false; OnHover?.Invoke(Slot, false); };
    }

    public void Refresh()
    {
        Current = Source(Slot);
        _icon.Texture = Current.IsEmpty ? null : Current.Icon;
        _icon.Modulate = Colors.White;
        _count.Text = Current.Count > 1 ? Current.Count.ToString() : "";
        if (Current.IsEmpty) _badge.Clear(); else _badge.Set(Current.ItemId);
    }

    public void SetGhost(Texture2D? icon)
    {
        if (!Current.IsEmpty) return;
        _icon.Texture = icon;
        _icon.Modulate = GhostTint;
    }

    public void SetCarried(bool carried) => _icon.Modulate = carried ? CarriedTint : Colors.White;

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton mb) return;
        if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            OnActivate?.Invoke(Slot);
            AcceptEvent();
            return;
        }
        if (mb.ButtonIndex != MouseButton.Left) return;
        if (mb.Pressed)
        {
            if (mb.DoubleClick)
            {
                _suppressClick = true;
                OnDoubleClick?.Invoke(Slot);
            }
            AcceptEvent();
            return;
        }
        if (_suppressClick) { _suppressClick = false; AcceptEvent(); return; }
        OnClick?.Invoke(Slot);
        AcceptEvent();
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Current.IsEmpty) return default;
        _suppressClick = false;
        OnHover?.Invoke(Slot, false);
        var preview = new TextureRect
        {
            Texture = Current.Icon,
            CustomMinimumSize = Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary { { DragKeyItem, Current.ItemId }, { DragKeyFrom, Slot } };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (OnDrop == null || data.VariantType != Variant.Type.Dictionary) return false;
        var d = data.AsGodotDictionary();
        return d.ContainsKey(DragKeyFrom) && d[DragKeyFrom].AsInt32() != Slot;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _hover.Visible = false;
        OnDrop?.Invoke(data.AsGodotDictionary()[DragKeyFrom].AsInt32(), Slot);
    }
}

public partial class DropTarget : Control
{
    public Action<int>? OnDrop { get; set; }
    public Action? OnClick { get; set; }

    public DropTarget() => MouseFilter = MouseFilterEnum.Stop;

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) return;
        OnClick?.Invoke();
        AcceptEvent();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        OnDrop != null && data.VariantType == Variant.Type.Dictionary
        && data.AsGodotDictionary().ContainsKey(ItemSlot.DragKeyFrom);

    public override void _DropData(Vector2 atPosition, Variant data) =>
        OnDrop?.Invoke(data.AsGodotDictionary()[ItemSlot.DragKeyFrom].AsInt32());
}
