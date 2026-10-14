using Godot;

namespace KnightOnlineUi.Layout;

public readonly record struct RetailListItem(string Key, string Text, Color Colour, int Icon, string Tooltip);

public sealed class RetailList
{
    private readonly LayoutView view;
    private readonly List<LayoutNode> rows;
    private readonly Action<LayoutNode, RetailListItem?> paint;
    private readonly Control? selectBar;
    private readonly Control? thumb;
    private readonly Rect2 track;
    private readonly float selectTop;
    private readonly float thumbTop;
    private readonly List<RetailListItem> items = new();
    private int first;

    public string? SelectedKey { get; private set; }

    public event Action<string>? Activated;
    public event Action<string, Vector2>? ContextRequested;
    public event Action<string?>? SelectionChanged;

    public RetailList(LayoutView view, IEnumerable<LayoutNode> rows, Action<LayoutNode, RetailListItem?> paint,
        Control? selectBar, LayoutNode? scrollbar)
    {
        this.view = view;
        this.rows = rows.ToList();
        this.paint = paint;
        this.selectBar = selectBar;
        selectTop = selectBar?.Position.Y ?? 0f;
        for (int i = 0; i < this.rows.Count; i++) AddRowHit(i);
        if (scrollbar == null) return;
        var buttons = scrollbar.Children.Where(n => n.IsButton).ToList();
        if (buttons.Count >= 2)
        {
            Hook(buttons[0], () => Scroll(-1));
            Hook(buttons[^1], () => Scroll(1));
        }
        if (scrollbar.Children.FirstOrDefault(n => n.Type == "trackbar") is { } trackNode)
        {
            track = new Rect2(trackNode.X, trackNode.Y, trackNode.W, trackNode.H);
            var images = trackNode.Images.ToList();
            if (images.Count >= 2) thumb = view.ControlOf(images[^1]);
            thumbTop = thumb?.Position.Y ?? 0f;
        }
    }

    private int Visible => rows.Count;

    private int MaxFirst => Math.Max(0, items.Count - Visible);

    public void SetItems(IReadOnlyList<RetailListItem> items)
    {
        this.items.Clear();
        this.items.AddRange(items);
        if (SelectedKey != null && !this.items.Any(i => i.Key == SelectedKey))
        {
            SelectedKey = null;
            SelectionChanged?.Invoke(null);
        }
        first = Math.Clamp(first, 0, MaxFirst);
        Paint();
    }

    public void Scroll(int delta)
    {
        int first = Math.Clamp(this.first + delta, 0, MaxFirst);
        if (first == this.first) return;
        this.first = first;
        Paint();
    }

    private void Paint()
    {
        int selectedRow = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            int index = first + i;
            RetailListItem? item = index < items.Count ? items[index] : null;
            paint(rows[i], item);
            if (item is { } shown && shown.Key == SelectedKey) selectedRow = i;
            if (view.ControlOf(rows[i]) is { } row) row.TooltipText = item?.Tooltip ?? "";
        }
        if (selectBar != null)
        {
            selectBar.Visible = selectedRow >= 0;
            if (selectedRow >= 0)
                selectBar.Position = new Vector2(selectBar.Position.X, selectTop + rows[selectedRow].Y - rows[0].Y);
        }
        if (thumb != null && track.Size.Y > 0)
        {
            float span = Math.Max(0f, track.Size.Y - thumb.Size.Y);
            float share = MaxFirst == 0 ? 0f : (float)first / MaxFirst;
            thumb.Position = new Vector2(thumb.Position.X, thumbTop + span * share);
        }
    }

    private void AddRowHit(int row)
    {
        if (view.ControlOf(rows[row]) is not { } control) return;
        control.MouseFilter = Control.MouseFilterEnum.Stop;
        control.GuiInput += ev => OnRowInput(control, row, ev);
    }

    private void OnRowInput(Control control, int row, InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } mb) return;
        switch (mb.ButtonIndex)
        {
            case MouseButton.WheelUp: Scroll(-1); break;
            case MouseButton.WheelDown: Scroll(1); break;
            case MouseButton.Left or MouseButton.Right:
                int index = first + row;
                if (index >= items.Count) break;
                string key = items[index].Key;
                if (key != SelectedKey)
                {
                    SelectedKey = key;
                    SelectionChanged?.Invoke(key);
                    Paint();
                }
                if (mb.ButtonIndex == MouseButton.Right) ContextRequested?.Invoke(key, mb.GlobalPosition);
                else if (mb.DoubleClick) Activated?.Invoke(key);
                break;
            default:
                return;
        }
        control.AcceptEvent();
    }

    private void Hook(LayoutNode button, Action action)
    {
        if (view.ControlOf(button) is BaseButton b) b.Pressed += action;
    }
}
