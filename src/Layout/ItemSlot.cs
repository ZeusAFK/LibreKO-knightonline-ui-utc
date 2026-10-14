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

    private readonly TextureRect icon;
    private readonly Label count;
    private readonly ColorRect hover;
    private readonly UpgradeBadge badge;
    private bool suppressClick;

    public ItemSlot(int slot)
    {
        Slot = slot;
        MouseFilter = MouseFilterEnum.Stop;
        hover = new ColorRect { Color = HoverTint, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        hover.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(hover);
        icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsPreset(LayoutPreset.FullRect);
        icon.OffsetLeft = IconInset; icon.OffsetTop = IconInset;
        icon.OffsetRight = -IconInset; icon.OffsetBottom = -IconInset;
        AddChild(icon);
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
        count.AddThemeConstantOverride("outline_size", 3);
        AddChild(count);
        badge = UpgradeBadge.Attach(this);
        MouseEntered += () => { hover.Visible = true; if (!Current.IsEmpty) OnHover?.Invoke(Slot, true); };
        MouseExited += () => { hover.Visible = false; OnHover?.Invoke(Slot, false); };
    }

    public void Refresh()
    {
        Current = Source(Slot);
        icon.Texture = Current.IsEmpty ? null : Current.Icon;
        icon.Modulate = Colors.White;
        count.Text = Current.Count > 1 ? Current.Count.ToString() : "";
        if (Current.IsEmpty) badge.Clear(); else badge.Set(Current.ItemId);
    }

    public void SetGhost(Texture2D? texture)
    {
        if (!Current.IsEmpty) return;
        icon.Texture = texture;
        icon.Modulate = GhostTint;
    }

    public void SetCarried(bool carried) => icon.Modulate = carried ? CarriedTint : Colors.White;

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
                suppressClick = true;
                OnDoubleClick?.Invoke(Slot);
            }
            AcceptEvent();
            return;
        }
        if (suppressClick) { suppressClick = false; AcceptEvent(); return; }
        OnClick?.Invoke(Slot);
        AcceptEvent();
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Current.IsEmpty) return default;
        suppressClick = false;
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
        hover.Visible = false;
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
