using System;
using System.Linq;
using Godot;

namespace KnightOnlineUi.Layout;

public sealed class RetailTrackbar
{
    private readonly Control? _track;
    private readonly Control? _thumb;
    private readonly bool _vertical;
    private readonly int _min;
    private readonly int _max;
    private readonly Action<int> _changed;
    private int _value;
    private bool _dragging;

    public RetailTrackbar(LayoutView view, LayoutNode bar, int min, int max, int step, Action<int> changed)
    {
        _min = min;
        _max = Math.Max(min, max);
        _changed = changed;
        _vertical = bar.H > bar.W;
        var arrows = bar.Children.Where(n => n.IsButton).ToList();
        if (arrows.Count > 0 && view.ControlOf(arrows[0]) is BaseButton down) down.Pressed += () => Step(-step);
        if (arrows.Count > 1 && view.ControlOf(arrows[^1]) is BaseButton up) up.Pressed += () => Step(step);
        if (bar.Children.FirstOrDefault(c => c.Type == "trackbar") is not { } track || view.ControlOf(track) is not { } trackControl) return;
        _track = trackControl;
        _thumb = track.Children.Count > 1 ? view.ControlOf(track.Children[^1]) : null;
        trackControl.MouseFilter = Control.MouseFilterEnum.Stop;
        trackControl.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
            {
                _dragging = button.Pressed;
                if (button.Pressed) SetFromMouse();
                trackControl.AcceptEvent();
            }
            else if (ev is InputEventMouseMotion && _dragging)
            {
                SetFromMouse();
                trackControl.AcceptEvent();
            }
            else if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel)
            {
                Step(wheel.ButtonIndex == MouseButton.WheelUp ? -step : step);
                trackControl.AcceptEvent();
            }
        };
    }

    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, _min, _max);
            Place();
        }
    }

    private void Step(int delta) => Commit(_value + delta);

    private void Commit(int value)
    {
        value = Math.Clamp(value, _min, _max);
        if (value == _value) return;
        _value = value;
        Place();
        _changed(value);
    }

    private void SetFromMouse()
    {
        if (_track == null) return;
        float thumb = _thumb == null ? 0f : _vertical ? _thumb.Size.Y : _thumb.Size.X;
        float length = _vertical ? _track.Size.Y : _track.Size.X;
        float at = _vertical ? _track.GetLocalMousePosition().Y : _track.GetLocalMousePosition().X;
        float fraction = Mathf.Clamp((at - thumb * 0.5f) / Mathf.Max(1f, length - thumb), 0f, 1f);
        Commit(_min + Mathf.RoundToInt(fraction * (_max - _min)));
    }

    private void Place()
    {
        if (_track == null || _thumb == null) return;
        float fraction = _max == _min ? 0f : (_value - _min) / (float)(_max - _min);
        if (_vertical)
            _thumb.Position = new Vector2(_thumb.Position.X, Mathf.Max(0f, _track.Size.Y - _thumb.Size.Y) * fraction);
        else
            _thumb.Position = new Vector2(Mathf.Max(0f, _track.Size.X - _thumb.Size.X) * fraction, _thumb.Position.Y);
    }
}
