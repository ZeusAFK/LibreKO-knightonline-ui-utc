using Godot;

namespace KnightOnlineUi.Layout;

public readonly record struct RetailListItem(string Key, string Text, Color Colour, int Icon, string Tooltip);

public sealed class RetailList
{
    private readonly LayoutView _view;
    private readonly List<LayoutNode> _rows;
    private readonly Action<LayoutNode, RetailListItem?> _paint;
    private readonly Control? _selectBar;
    private readonly Control? _thumb;
    private readonly Rect2 _track;
    private readonly float _selectTop;
    private readonly float _thumbTop;
    private readonly List<RetailListItem> _items = new();
    private int _first;

    public string? SelectedKey { get; private set; }

    public event Action<string>? Activated;
    public event Action<string, Vector2>? ContextRequested;
    public event Action<string?>? SelectionChanged;

    public RetailList(LayoutView view, IEnumerable<LayoutNode> rows, Action<LayoutNode, RetailListItem?> paint,
        Control? selectBar, LayoutNode? scrollbar)
    {
        _view = view;
        _rows = rows.ToList();
        _paint = paint;
        _selectBar = selectBar;
        _selectTop = selectBar?.Position.Y ?? 0f;
        for (int i = 0; i < _rows.Count; i++) AddRowHit(i);
        if (scrollbar == null) return;
        var buttons = scrollbar.Children.Where(n => n.IsButton).ToList();
        if (buttons.Count >= 2)
        {
            Hook(buttons[0], () => Scroll(-1));
            Hook(buttons[^1], () => Scroll(1));
        }
        if (scrollbar.Children.FirstOrDefault(n => n.Type == "trackbar") is { } track)
        {
            _track = new Rect2(track.X, track.Y, track.W, track.H);
            var images = track.Images.ToList();
            if (images.Count >= 2) _thumb = view.ControlOf(images[^1]);
            _thumbTop = _thumb?.Position.Y ?? 0f;
        }
    }

    private int Visible => _rows.Count;

    private int MaxFirst => Math.Max(0, _items.Count - Visible);

    public void SetItems(IReadOnlyList<RetailListItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        if (SelectedKey != null && !_items.Any(i => i.Key == SelectedKey))
        {
            SelectedKey = null;
            SelectionChanged?.Invoke(null);
        }
        _first = Math.Clamp(_first, 0, MaxFirst);
        Paint();
    }

    public void Scroll(int delta)
    {
        int first = Math.Clamp(_first + delta, 0, MaxFirst);
        if (first == _first) return;
        _first = first;
        Paint();
    }

    private void Paint()
    {
        int selectedRow = -1;
        for (int i = 0; i < _rows.Count; i++)
        {
            int index = _first + i;
            RetailListItem? item = index < _items.Count ? _items[index] : null;
            _paint(_rows[i], item);
            if (item is { } shown && shown.Key == SelectedKey) selectedRow = i;
            if (_view.ControlOf(_rows[i]) is { } row) row.TooltipText = item?.Tooltip ?? "";
        }
        if (_selectBar != null)
        {
            _selectBar.Visible = selectedRow >= 0;
            if (selectedRow >= 0)
                _selectBar.Position = new Vector2(_selectBar.Position.X, _selectTop + _rows[selectedRow].Y - _rows[0].Y);
        }
        if (_thumb != null && _track.Size.Y > 0)
        {
            float span = Math.Max(0f, _track.Size.Y - _thumb.Size.Y);
            float share = MaxFirst == 0 ? 0f : (float)_first / MaxFirst;
            _thumb.Position = new Vector2(_thumb.Position.X, _thumbTop + span * share);
        }
    }

    private void AddRowHit(int row)
    {
        if (_view.ControlOf(_rows[row]) is not { } control) return;
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
                int index = _first + row;
                if (index >= _items.Count) break;
                string key = _items[index].Key;
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
        if (_view.ControlOf(button) is BaseButton b) b.Pressed += action;
    }
}
