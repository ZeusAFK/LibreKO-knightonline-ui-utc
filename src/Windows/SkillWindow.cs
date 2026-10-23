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
    private const string QuickslotWindow = "quickslot_saves";
    private static readonly string[] TabFamilies = { "blade", "ranger", "mage", "cleric" };
    private static readonly string[] LabelFamilies = { "berserker", "hunter", "sorcerer", "shaman" };
    private const string MasterLabelText = "Master";
    private const string KurianThirdTab = "btn_reaper2";
    private const string KurianThirdLabel = "img_reaper_2";
    private const int KurianFirstJob = 13;
    private const int WarriorFamily = 0;

    private readonly LayoutView view;
    private readonly LayoutNode layout;
    private readonly PluginGame game;
    private readonly WindowHost host;
    private readonly List<SkillCell> cells = new();
    private readonly List<TextureButton> tabButtons = new();
    private readonly List<GameSkill> pageSkills = new();
    private int family;
    private int tab;
    private int page;
    private int selected = -1;
    private int hovered = -1;
    private Rect2 listRect;

    public SkillWindow(WindowHost host)
    {
        this.host = host;
        var kit = Plugin.Kit;
        game = kit.Game;
        layout = kit.Layout(LayoutName);
        view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = layout.SizeVec;
        Size = layout.SizeVec;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, layout.W, TitleBarHeight)));
        view.OnPressed("btn_close", host.Close);
        view.OnPressed("btn_left", () => TurnPage(-1));
        view.OnPressed("btn_right", () => TurnPage(1));
        view.OnPressed("btn_preset", () => game.Windows.Toggle(PresetsWindow));
        view.OnPressed("btn_preset_hotkey", () => game.Windows.Toggle(QuickslotWindow));
        for (int row = 0; row < TreeRows; row++)
        {
            int type = row;
            view.OnPressed($"btn_{4 + row}", () => SpendOnRow(type));
        }

        foreach (var (node, control) in view.Areas(SlotAreaType))
        {
            if (!int.TryParse(node.Id, out int index) || index >= SlotsPerPage) continue;
            var cell = new SkillCell(index)
            {
                Position = control.Position,
                Size = control.Size,
                OnSelect = Select,
                OnAdd = id => game.Skills.AddToHotbar(id),
                OnHover = (id, over) => { hovered = over ? id : -1; RefreshInfo(); },
            };
            control.GetParent().AddChild(cell);
            cells.Add(cell);
            var rect = new Rect2(control.Position, control.Size + new Vector2(ListNameWidth, 0));
            listRect = listRect.Size == Vector2.Zero ? rect : listRect.Merge(rect);
        }
        cells.Sort((a, b) => a.Index.CompareTo(b.Index));
        Rebuild();
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } wheel) return;
        if (wheel.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown) || !listRect.HasPoint(wheel.Position)) return;
        TurnPage(wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
        AcceptEvent();
    }

    public override void _EnterTree()
    {
        game.Skills.Changed += Rebuild;
        game.BecameAvailable += Rebuild;
        host.Shown += Rebuild;
    }

    public override void _ExitTree()
    {
        game.Skills.Changed -= Rebuild;
        game.BecameAvailable -= Rebuild;
        host.Shown -= Rebuild;
    }

    private void Rebuild()
    {
        int job = game.Character.Class % 100;
        bool kurian = job >= KurianFirstJob;
        family = kurian ? WarriorFamily : Math.Clamp((job - 1) % ClassesPerFamilyCycle, 0, TabFamilies.Length - 1);
        var tabs = game.Skills.Tabs;
        foreach (var b in tabButtons) b.ToggleMode = false;
        tabButtons.Clear();
        var group = new ButtonGroup();
        for (int f = 0; f < TabFamilies.Length; f++)
            for (int i = 0; i < FamilyTabs; i++)
                view.Hide($"btn_{TabFamilies[f]}{i}");
        for (int f = 0; f < LabelFamilies.Length; f++)
            for (int i = 0; i < FamilyTabs; i++)
                view.Hide($"img_{LabelFamilies[f]}_{i}");
        view.Hide("btn_master", "btn_public", KurianThirdTab, KurianThirdLabel);

        for (int t = 0; t < tabs.Count; t++)
        {
            string id = t == 0 ? "btn_public"
                : t == FamilyTabs && kurian ? KurianThirdTab
                : t <= FamilyTabs ? $"btn_{TabFamilies[family]}{t - 1}"
                : "btn_master";
            if (view.Get<TextureButton>(id) is not { } button) continue;
            button.Visible = true;
            button.ToggleMode = true;
            button.ButtonGroup = group;
            int which = t;
            button.Pressed += () => SelectTab(which);
            tabButtons.Add(button);
        }
        if (tab >= tabs.Count) tab = 0;
        for (int i = 0; i < FamilyTabs; i++)
        {
            string labelId = i == FamilyTabs - 1 && kurian ? KurianThirdLabel : $"img_{LabelFamilies[family]}_{i}";
            if (view.Get(labelId) is { } label) label.Visible = true;
        }

        RefreshTrees();
        RefreshPage();
    }

    private void RefreshTrees()
    {
        var trees = game.Skills.Trees;
        view.SetText("string_skillpoint", game.Skills.MasteryPool.ToString());
        for (int row = 0; row < TreeRows; row++)
        {
            bool has = row < trees.Count;
            var tree = has ? trees[row] : default;
            bool shown = has && tree.Shown;
            view.SetText($"string_{4 + row}", shown ? tree.Points.ToString() : "");
            if (view.Get<TextureButton>($"btn_{4 + row}") is { } plus)
            {
                plus.Visible = shown;
                plus.Disabled = !tree.CanSpend;
                plus.TooltipText = tree.Hint;
            }
            if (row == TreeRows - 1)
                foreach (var (_, control) in view.Where(n => n.IsString && n.Text == MasterLabelText))
                    control.Visible = shown;
        }
    }

    private void SelectTab(int tab)
    {
        this.tab = tab;
        page = 0;
        selected = -1;
        RefreshPage();
    }

    private void TurnPage(int delta)
    {
        int pages = Math.Max(1, (CurrentSkills().Count + SlotsPerPage - 1) / SlotsPerPage);
        int page = Math.Clamp(this.page + delta, 0, pages - 1);
        if (page == this.page) return;
        this.page = page;
        RefreshPage();
    }

    private IReadOnlyList<GameSkill> CurrentSkills()
    {
        var tabs = game.Skills.Tabs;
        return tab < tabs.Count ? game.Skills.Skills(tabs[tab].Category) : Array.Empty<GameSkill>();
    }

    private void RefreshPage()
    {
        if (tab < tabButtons.Count) tabButtons[tab].ButtonPressed = true;
        var skills = CurrentSkills();
        int first = page * SlotsPerPage;
        pageSkills.Clear();
        for (int i = 0; i < cells.Count; i++)
        {
            var skill = first + i < skills.Count ? skills[first + i] : (GameSkill?)null;
            cells[i].Bind(skill);
            view.SetText($"string_list_{i}", skill?.Name ?? "");
            if (skill != null) pageSkills.Add(skill.Value);
        }
        if (selected < 0 && pageSkills.Count > 0) selected = pageSkills[0].Id;
        foreach (var cell in cells) cell.SetSelected(cell.SkillId == selected);
        view.SetText("string_page", (page + 1).ToString());
        RefreshInfo();
    }

    private void Select(int skillId)
    {
        selected = skillId;
        foreach (var cell in cells) cell.SetSelected(cell.SkillId == selected);
        RefreshInfo();
    }

    private void RefreshInfo()
    {
        int shown = hovered >= 0 ? hovered : selected;
        var info = shown >= 0 ? game.Skills.Info(shown) : default;
        bool has = shown >= 0 && info.Description != null;
        view.SetText("string_info", info.Description ?? "");
        view.SetText("string_skill_mp", has ? $"MP consumed : {info.Mp}" : "");
        view.SetText("string_skill_vp", "");
        view.SetText("string_skill_point", has && info.UsesPoints ? $"Required Skill Point : {info.RequiredPoints}" : "");
        view.SetText("string_skill_level", has ? $"Required Level : {info.RequiredLevel}" : "");
        view.SetText("string_skill_item0", !has ? "" : info.BasicItem.Length > 0 ? $"Basic item : Required Item : {info.BasicItem}" : "No basic item");
        view.SetText("string_skill_item1", !has ? "" : info.RequiredItem.Length > 0 ? $"Required item : {info.RequiredItem}" : "No required item");
        view.SetText("string_skill_item2", !has ? "" : info.ConsumedItem.Length > 0 ? $"Item consumed : {info.ConsumedItem}" : "No item consumed");
    }

    private void SpendOnRow(int row)
    {
        var trees = game.Skills.Trees;
        if (row < trees.Count) game.Skills.SpendMastery(trees[row].Type);
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

    private readonly TextureRect icon;
    private GameSkill? skill;
    private bool selected;

    public SkillCell(int index)
    {
        Index = index;
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () => { if (skill != null) OnHover?.Invoke(skill.Value.Id, true); };
        MouseExited += () => { if (skill != null) OnHover?.Invoke(skill.Value.Id, false); };
        icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(icon);
    }

    public void Bind(GameSkill? skill)
    {
        this.skill = skill;
        SkillId = skill?.Id ?? -1;
        Visible = skill != null;
        icon.Texture = skill?.Icon;
        icon.Modulate = skill is { Available: false } ? UnavailableTint : Colors.White;
        TooltipText = skill?.Tooltip ?? "";
    }

    public void SetSelected(bool selected)
    {
        this.selected = selected;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (selected) DrawRect(new Rect2(Vector2.Zero, Size), SelectedFrame, false, FrameWidth);
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (skill == null || ev is not InputEventMouseButton { Pressed: true } mb) return;
        if (mb.ButtonIndex == MouseButton.Left) { OnSelect?.Invoke(skill.Value.Id); AcceptEvent(); }
        else if (mb.ButtonIndex == MouseButton.Right && skill.Value.Available) { OnAdd?.Invoke(skill.Value.Id); AcceptEvent(); }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (this.skill is not { Available: true } skill) return default;
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
