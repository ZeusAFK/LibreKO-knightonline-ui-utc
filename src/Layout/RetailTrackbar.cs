using System;
using System.Linq;
using Godot;

namespace KnightOnlineUi.Layout;

public sealed class RetailTrackbar
{
    private readonly Control? track;
    private readonly Control? thumb;
    private readonly bool vertical;
    private readonly int min;
    private readonly int max;
    private readonly Action<int> changed;
    private int value;
    private bool dragging;

    public RetailTrackbar(LayoutView view, LayoutNode bar, int min, int max, int step, Action<int> changed)
    {
        this.min = min;
        this.max = Math.Max(min, max);
        this.changed = changed;
        vertical = bar.H > bar.W;
        var arrows = bar.Children.Where(n => n.IsButton).ToList();
        if (arrows.Count > 0 && view.ControlOf(arrows[0]) is BaseButton down) down.Pressed += () => Step(-step);
        if (arrows.Count > 1 && view.ControlOf(arrows[^1]) is BaseButton up) up.Pressed += () => Step(step);
        if (bar.Children.FirstOrDefault(c => c.Type == "trackbar") is not { } trackNode || view.ControlOf(trackNode) is not { } trackControl) return;
        track = trackControl;
        thumb = trackNode.Children.Count > 1 ? view.ControlOf(trackNode.Children[^1]) : null;
        trackControl.MouseFilter = Control.MouseFilterEnum.Stop;
        trackControl.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
            {
                dragging = button.Pressed;
                if (button.Pressed) SetFromMouse();
                trackControl.AcceptEvent();
            }
            else if (ev is InputEventMouseMotion && dragging)
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
        get => value;
        set
        {
            this.value = Math.Clamp(value, min, max);
            Place();
        }
    }

    private void Step(int delta) => Commit(value + delta);

    private void Commit(int value)
    {
        value = Math.Clamp(value, min, max);
        if (value == this.value) return;
        this.value = value;
        Place();
        changed(value);
    }

    private void SetFromMouse()
    {
        if (track == null) return;
        float thumbSize = thumb == null ? 0f : vertical ? thumb.Size.Y : thumb.Size.X;
        float length = vertical ? track.Size.Y : track.Size.X;
        float at = vertical ? track.GetLocalMousePosition().Y : track.GetLocalMousePosition().X;
        float fraction = Mathf.Clamp((at - thumbSize * 0.5f) / Mathf.Max(1f, length - thumbSize), 0f, 1f);
        Commit(min + Mathf.RoundToInt(fraction * (max - min)));
    }

    private void Place()
    {
        if (track == null || thumb == null) return;
        float fraction = max == min ? 0f : (value - min) / (float)(max - min);
        if (vertical)
            thumb.Position = new Vector2(thumb.Position.X, Mathf.Max(0f, track.Size.Y - thumb.Size.Y) * fraction);
        else
            thumb.Position = new Vector2(Mathf.Max(0f, track.Size.X - thumb.Size.X) * fraction, thumb.Position.Y);
    }
}
