using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class SkillWindow : Control
{
    private const string LayoutName = "re_skill";
    private const int SlotAreaType = 7;
    private const int SlotsPerPage = 8;
    private const int TreeRows = 4;
    private const int FamilyTabs = 3;
    private const int ClassesPerFamilyCycle = 4;
    private const float TitleBarHeight = 60f;
    private const float ListNameWidth = 130f;
    private const string PresetsWindow = "presets";
    private static readonly string[] TabFamilies = { "blade", "ranger", "mage", "cleric" };
    private static readonly string[] LabelFamilies = { "berserker", "hunter", "sorcerer", "shaman" };
    private const string MasterLabelText = "Master";

    private readonly LayoutView _view;
    private readonly LayoutNode _layout;
    private readonly PluginGame _game;
    private readonly WindowHost _host;
    private readonly List<SkillCell> _cells = new();
    private readonly List<TextureButton> _tabButtons = new();
    private readonly List<GameSkill> _pageSkills = new();
    private int _family;
    private int _tab;
    private int _page;
    private int _selected = -1;
    private int _hovered = -1;
    private Rect2 _listRect;

    public SkillWindow(WindowHost host)
    {
        _host = host;
        var kit = Plugin.Kit;
        _game = kit.Game;
        _layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, _layout);
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = _layout.SizeVec;
        Size = _layout.SizeVec;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _layout.W, TitleBarHeight)));
        _view.OnPressed("btn_close", host.Close);
        _view.OnPressed("btn_left", () => TurnPage(-1));
        _view.OnPressed("btn_right", () => TurnPage(1));
        _view.OnPressed("btn_preset", () => _game.Windows.Toggle(PresetsWindow));
        _view.Hide("btn_preset_hotkey", "btn_reaper2", "img_reaper_2");
        for (int row = 0; row < TreeRows; row++)
        {
            int type = row;
            _view.OnPressed($"btn_{4 + row}", () => SpendOnRow(type));
        }

        foreach (var (node, control) in _view.Areas(SlotAreaType))
        {
            if (!int.TryParse(node.Id, out int index) || index >= SlotsPerPage) continue;
            var cell = new SkillCell(index)
            {
                Position = control.Position,
                Size = control.Size,
                OnSelect = Select,
                OnAdd = id => _game.Skills.AddToHotbar(id),
                OnHover = (id, over) => { _hovered = over ? id : -1; RefreshInfo(); },
            };
            control.GetParent().AddChild(cell);
            _cells.Add(cell);
            var rect = new Rect2(control.Position, control.Size + new Vector2(ListNameWidth, 0));
            _listRect = _listRect.Size == Vector2.Zero ? rect : _listRect.Merge(rect);
        }
        _cells.Sort((a, b) => a.Index.CompareTo(b.Index));
        Rebuild();
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } wheel) return;
        if (wheel.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown) || !_listRect.HasPoint(wheel.Position)) return;
        TurnPage(wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
        AcceptEvent();
    }

    public override void _EnterTree()
    {
        _game.Skills.Changed += Rebuild;
        _game.BecameAvailable += Rebuild;
        _host.Shown += Rebuild;
    }

    public override void _ExitTree()
    {
        _game.Skills.Changed -= Rebuild;
        _game.BecameAvailable -= Rebuild;
        _host.Shown -= Rebuild;
    }

    private void Rebuild()
    {
        _family = Math.Clamp((_game.Character.Class % 100 - 1) % ClassesPerFamilyCycle, 0, TabFamilies.Length - 1);
        var tabs = _game.Skills.Tabs;
        foreach (var b in _tabButtons) b.ToggleMode = false;
        _tabButtons.Clear();
        var group = new ButtonGroup();
        for (int f = 0; f < TabFamilies.Length; f++)
            for (int i = 0; i < FamilyTabs; i++)
                _view.Hide($"btn_{TabFamilies[f]}{i}");
        for (int f = 0; f < LabelFamilies.Length; f++)
            for (int i = 0; i < FamilyTabs; i++)
                _view.Hide($"img_{LabelFamilies[f]}_{i}");
        _view.Hide("btn_master", "btn_public");

        for (int t = 0; t < tabs.Count; t++)
        {
            string id = t == 0 ? "btn_public" : t <= FamilyTabs ? $"btn_{TabFamilies[_family]}{t - 1}" : "btn_master";
            if (_view.Get<TextureButton>(id) is not { } button) continue;
            button.Visible = true;
            button.ToggleMode = true;
            button.ButtonGroup = group;
            int which = t;
            button.Pressed += () => SelectTab(which);
            _tabButtons.Add(button);
        }
        if (_tab >= tabs.Count) _tab = 0;
        for (int i = 0; i < FamilyTabs; i++)
            if (_view.Get($"img_{LabelFamilies[_family]}_{i}") is { } label) label.Visible = true;

        RefreshTrees();
        RefreshPage();
    }

    private void RefreshTrees()
    {
        var trees = _game.Skills.Trees;
        _view.SetText("string_skillpoint", _game.Skills.MasteryPool.ToString());
        for (int row = 0; row < TreeRows; row++)
        {
            bool has = row < trees.Count;
            var tree = has ? trees[row] : default;
            bool shown = has && tree.Shown;
            _view.SetText($"string_{4 + row}", shown ? tree.Points.ToString() : "");
            if (_view.Get<TextureButton>($"btn_{4 + row}") is { } plus)
            {
                plus.Visible = shown;
                plus.Disabled = !tree.CanSpend;
                plus.TooltipText = tree.Hint;
            }
            if (row == TreeRows - 1)
                foreach (var (_, control) in _view.Where(n => n.IsString && n.Text == MasterLabelText))
                    control.Visible = shown;
        }
    }

    private void SelectTab(int tab)
    {
        _tab = tab;
        _page = 0;
        _selected = -1;
        RefreshPage();
    }

    private void TurnPage(int delta)
    {
        int pages = Math.Max(1, (CurrentSkills().Count + SlotsPerPage - 1) / SlotsPerPage);
        int page = Math.Clamp(_page + delta, 0, pages - 1);
        if (page == _page) return;
        _page = page;
        RefreshPage();
    }

    private IReadOnlyList<GameSkill> CurrentSkills()
    {
        var tabs = _game.Skills.Tabs;
        return _tab < tabs.Count ? _game.Skills.Skills(tabs[_tab].Category) : Array.Empty<GameSkill>();
    }

    private void RefreshPage()
    {
        if (_tab < _tabButtons.Count) _tabButtons[_tab].ButtonPressed = true;
        var skills = CurrentSkills();
        int first = _page * SlotsPerPage;
        _pageSkills.Clear();
        for (int i = 0; i < _cells.Count; i++)
        {
            var skill = first + i < skills.Count ? skills[first + i] : (GameSkill?)null;
            _cells[i].Bind(skill);
            _view.SetText($"string_list_{i}", skill?.Name ?? "");
            if (skill != null) _pageSkills.Add(skill.Value);
        }
        if (_selected < 0 && _pageSkills.Count > 0) _selected = _pageSkills[0].Id;
        foreach (var cell in _cells) cell.SetSelected(cell.SkillId == _selected);
        _view.SetText("string_page", (_page + 1).ToString());
        RefreshInfo();
    }

    private void Select(int skillId)
    {
        _selected = skillId;
        foreach (var cell in _cells) cell.SetSelected(cell.SkillId == _selected);
        RefreshInfo();
    }

    private void RefreshInfo()
    {
        int shown = _hovered >= 0 ? _hovered : _selected;
        var info = shown >= 0 ? _game.Skills.Info(shown) : default;
        bool has = shown >= 0 && info.Description != null;
        _view.SetText("string_info", has ? info.Description : "");
        _view.SetText("string_skill_mp", has ? $"MP consumed : {info.Mp}" : "");
        _view.SetText("string_skill_vp", "");
        _view.SetText("string_skill_point", has && info.UsesPoints ? $"Required Skill Point : {info.RequiredPoints}" : "");
        _view.SetText("string_skill_level", has ? $"Required Level : {info.RequiredLevel}" : "");
        _view.SetText("string_skill_item0", !has ? "" : info.BasicItem.Length > 0 ? $"Basic item : Required Item : {info.BasicItem}" : "No basic item");
        _view.SetText("string_skill_item1", !has ? "" : info.RequiredItem.Length > 0 ? $"Required item : {info.RequiredItem}" : "No required item");
        _view.SetText("string_skill_item2", !has ? "" : info.ConsumedItem.Length > 0 ? $"Item consumed : {info.ConsumedItem}" : "No item consumed");
    }

    private void SpendOnRow(int row)
    {
        var trees = _game.Skills.Trees;
        if (row < trees.Count) _game.Skills.SpendMastery(trees[row].Type);
    }
}

public partial class SkillCell : Control
{
    private const string DragKeyItem = "id";
    private static readonly Color UnavailableTint = new(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color SelectedFrame = new(0.95f, 0.80f, 0.35f, 1f);
    private const float FrameWidth = 2f;

    public int Index { get; }
    public int SkillId { get; private set; } = -1;
    public Action<int>? OnSelect { get; set; }
    public Action<int>? OnAdd { get; set; }
    public Action<int, bool>? OnHover { get; set; }

    private readonly TextureRect _icon;
    private GameSkill? _skill;
    private bool _selected;

    public SkillCell(int index)
    {
        Index = index;
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () => { if (_skill != null) OnHover?.Invoke(_skill.Value.Id, true); };
        MouseExited += () => { if (_skill != null) OnHover?.Invoke(_skill.Value.Id, false); };
        _icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _icon.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_icon);
    }

    public void Bind(GameSkill? skill)
    {
        _skill = skill;
        SkillId = skill?.Id ?? -1;
        Visible = skill != null;
        _icon.Texture = skill?.Icon;
        _icon.Modulate = skill is { Available: false } ? UnavailableTint : Colors.White;
        TooltipText = skill?.Tooltip ?? "";
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_selected) DrawRect(new Rect2(Vector2.Zero, Size), SelectedFrame, false, FrameWidth);
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (_skill == null || ev is not InputEventMouseButton { Pressed: true } mb) return;
        if (mb.ButtonIndex == MouseButton.Left) { OnSelect?.Invoke(_skill.Value.Id); AcceptEvent(); }
        else if (mb.ButtonIndex == MouseButton.Right && _skill.Value.Available) { OnAdd?.Invoke(_skill.Value.Id); AcceptEvent(); }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_skill is not { Available: true } skill) return default;
        var preview = new TextureRect
        {
            Texture = skill.Icon,
            CustomMinimumSize = Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary { { DragKeyItem, skill.Id } };
    }
}
