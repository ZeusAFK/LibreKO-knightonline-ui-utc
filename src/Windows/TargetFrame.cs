using System.Collections.Generic;
using System.Linq;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class TargetFrame : Control
{
    private const string LayoutName = "re_target_framebar";
    private const string FrameShown = "frame_3";
    private const string HpBarGroup = "base_mon_hp_level";
    private const string HpBar = "grpg_level_0";
    private const string StateGroup = "base_player_hp_state";
    private const string StateBarPrefix = "state_";
    private const int StateBars = 5;
    private const float TopMargin = 8f;
    private const int HpTextSize = 11;
    private const int HpTextOutline = 3;
    private const float HpTextOverhang = 4f;

    private static readonly Dictionary<TargetHpLook, int> StateOfLook = new()
    {
        [TargetHpLook.Undead] = 0,
        [TargetHpLook.Lasting] = 2,
        [TargetHpLook.Drop] = 3,
        [TargetHpLook.Slow] = 4,
    };

    private readonly LayoutView view;
    private readonly PluginGame game;
    private readonly Label hpText;
    private const string DropRow = "base_EventDropList";
    private const string DropRowSuffix = "_";
    private const int DropRowStep = 25;
    private const int DropRows = 3;
    private const int DropTextSize = 11;
    private readonly LayoutNode?[] dropRows = new LayoutNode?[DropRows];
    private readonly TextureRect?[] dropIcons = new TextureRect?[DropRows];
    private readonly Label?[] dropLines = new Label?[DropRows];

    public TargetFrame()
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        var layout = kit.Layout(LayoutName);
        for (int i = 1; i < DropRows; i++)
            if (layout.Find(DropRow + DropRowSuffix + i) == null)
                layout.CloneBelow(DropRow, DropRowSuffix + i, DropRowStep * i);
        view = new LayoutView(kit, layout);
        Size = view.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(view);
        foreach (var (node, control) in view.Where(n => n.Id.StartsWith("frame_") && n.Id != FrameShown))
            control.Visible = false;
        view.Hide("base_mon_hp_level_long", "base_cover", "Button_Board");
        BuildDropRows(layout);
        foreach (var (node, control) in view.Where(n => n.Id.StartsWith("grpg_level_") && n.Id != HpBar))
            control.Visible = false;
        hpText = BuildHpText();
        ShowState(-1);
        Visible = false;
    }

    private void BuildDropRows(LayoutNode layout)
    {
        for (int i = 0; i < DropRows; i++)
        {
            string suffix = i == 0 ? "" : DropRowSuffix + i;
            if (layout.Find(DropRow + suffix) is not { } row) continue;
            dropRows[i] = row;
            if (view.Get(row, "icon" + suffix) is { } iconArea)
            {
                var icon = new TextureRect
                {
                    Size = iconArea.Size,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                iconArea.AddChild(icon);
                dropIcons[i] = icon;
            }
            if (view.Get(row, "name" + suffix) is { } nameArea)
            {
                var line = new Label
                {
                    Size = nameArea.Size,
                    VerticalAlignment = VerticalAlignment.Center,
                    ClipText = true,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                line.AddThemeFontSizeOverride("font_size", DropTextSize);
                line.AddThemeColorOverride("font_color", Colors.White);
                line.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
                line.AddThemeConstantOverride("outline_size", HpTextOutline);
                nameArea.AddChild(line);
                dropLines[i] = line;
            }
            if (view.ControlOf(row) is { } frame) frame.Visible = false;
        }
    }

    private void ShowDropRows(IReadOnlyList<GameTargetDrop> drops)
    {
        for (int i = 0; i < DropRows; i++)
        {
            bool used = i < drops.Count;
            if (dropRows[i] is { } row && view.ControlOf(row) is { } frame) frame.Visible = used;
            if (!used) continue;
            if (dropIcons[i] is { } icon) icon.Texture = drops[i].Icon;
            if (dropLines[i] is { } line) line.Text = drops[i].Line;
        }
    }

    private Label BuildHpText()
    {
        var bar = view.Where(n => n.Id == HpBarGroup).First().Node;
        var label = new Label
        {
            Position = new Vector2(bar.X, bar.Y - HpTextOverhang * 0.5f),
            Size = new Vector2(bar.W, bar.H + HpTextOverhang),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", HpTextSize);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        label.AddThemeConstantOverride("outline_size", HpTextOutline);
        view.AddChild(label);
        return label;
    }

    public override void _EnterTree()
    {
        game.Target.Changed += Refresh;
        game.BecameAvailable += Refresh;
        game.BecameUnavailable += Refresh;
        GetViewport().SizeChanged += Place;
        Place();
        Refresh();
    }

    public override void _ExitTree()
    {
        game.Target.Changed -= Refresh;
        game.BecameAvailable -= Refresh;
        game.BecameUnavailable -= Refresh;
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        float width = GetViewport().GetVisibleRect().Size.X;
        Position = new Vector2(Mathf.Round((width - Size.X) * 0.5f), TopMargin);
    }

    private void ShowState(int state)
    {
        if (view.Get(HpBarGroup) is { } normal) normal.Visible = state < 0;
        if (view.Get(StateGroup) is { } group) group.Visible = state >= 0;
        for (int i = 0; i < StateBars; i++)
            if (view.Get(StateBarPrefix + i) is { } bar) bar.Visible = i == state;
    }

    private void Refresh()
    {
        var t = game.Target;
        Visible = game.Available && t.Has;
        if (!Visible) return;
        view.SetText("name", t.Level > 0 ? $"{t.Name}  Lv.{t.Level}" : t.Name);
        float fraction = t.MaxHp > 0 ? t.Hp / (float)t.MaxHp : 1f;
        int state = StateOfLook.TryGetValue(t.HpLook, out int s) ? s : -1;
        ShowState(state);
        view.SetProgress(state < 0 ? HpBar : StateBarPrefix + state, fraction);
        hpText.Text = t.MaxHp > 0 ? $"{t.Hp} / {t.MaxHp}" : "";
        ShowDropRows(t.QuestDrops);
    }
}
