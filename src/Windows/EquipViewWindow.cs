using System.Collections.Generic;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class EquipViewWindow : Control
{
    private const string LayoutName = "re_targetinfo";
    private const string EquipGroup = "Equip";
    private const string LayeredGroup = "Layered";
    private const int EquipCells = 14;
    private const int TitleBarHeight = 100;
    private const int KarusNation = 1;
    private const int BuffListText = 44784;

    private static readonly (string Key, string Text, string Title)[] Values =
    {
        ("Level", "Text_Level", "Title_Level"),
        ("HP", "Text_HP", "Title_HP"),
        ("MP", "Text_MP", "Title_MP"),
        ("SP", "Text_SP", "Title_SP"),
        ("Attack", "Text_AP", "Title_AP"),
        ("Defence", "Text_AC", "Title_AC"),
        ("STR", "Text_Strength", "Title_Strength"),
        ("STA", "Text_Stamina", "Title_Stamina"),
        ("DEX", "Text_Dexterity", "Title_Dexterity"),
        ("MAG", "Text_MagicAttack", "Title_MagicAttack"),
        ("INT", "Text_Intelligence", "Title_Intelligence"),
        ("Fire", "Text_RegistFire", "Title_RegistFire"),
        ("Ice", "Text_RegistIce", "Title_RegistIce"),
        ("Lightning", "Text_RegistLightR", "Title_RegistLightR"),
        ("Magic", "Text_RegistMagic", "Title_RegistMagic"),
        ("Curse", "Text_RegistCurse", "Title_RegistCurse"),
        ("Poison", "Text_RegistPoison", "Title_RegistPoison"),
    };

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly Dictionary<int, TextureRect> _icons = new();

    public EquipViewWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        var layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        _view.OnPressed("btn_close", () => _game.EquipView.Close());
        if (layout.Find(EquipGroup) is { } equip)
            for (int slot = 0; slot < EquipCells; slot++)
                Wire(_view.Get(equip, slot.ToString()), slot);
        if (layout.Find(LayeredGroup) is { } layered)
            foreach (var cell in layered.Children)
                if (int.TryParse(cell.Id, out int position))
                    Wire(_view.ControlOf(cell), EquipViewLayout.SlotOfCostumePosition(position));
        _view.SetText("Title_Buff", ItemData.Text(BuffListText, "Buff List"));
    }

    public override void _EnterTree()
    {
        _game.EquipView.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _game.EquipView.Changed -= Refresh;

    private void Wire(Control? cell, int slot)
    {
        if (cell == null || slot < 0) return;
        var icon = new TextureRect
        {
            Size = cell.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        cell.AddChild(icon);
        cell.MouseFilter = MouseFilterEnum.Stop;
        cell.MouseEntered += () => _game.EquipView.ShowTooltip(slot);
        cell.MouseExited += () => _game.EquipView.HideTooltip();
        _icons[slot] = icon;
    }

    private void Refresh()
    {
        var equip = _game.EquipView;
        _view.SetText("Title_Regist", equip.ResistTitle);
        _view.SetText("Text_Notice", equip.Note);
        var model = equip.View;
        foreach (var icon in _icons.Values) icon.Texture = null;
        if (_view.Get("nation_ka") is { } karus) karus.Visible = model?.Nation == KarusNation;
        if (_view.Get("nation_el") is { } elmorad) elmorad.Visible = model != null && model.Nation != KarusNation;
        _view.SetText("Text_Id", model?.Name ?? "");
        if (model != null && _view.Get<Label>("Text_Id") is { } name) name.AddThemeColorOverride("font_color", model.NameColour);
        _view.SetText("Text_Class", model?.Class ?? "");
        var values = new Dictionary<string, GameEquipValue>();
        foreach (var value in model?.Values ?? System.Array.Empty<GameEquipValue>()) values[value.Key] = value;
        foreach (var (key, text, title) in Values)
        {
            bool known = values.TryGetValue(key, out var value);
            _view.SetText(text, known ? value.Value : "");
            if (known) _view.SetText(title, value.Caption);
        }
        if (model == null) return;
        foreach (var (slot, texture) in model.Items)
            if (_icons.TryGetValue(slot, out var icon)) icon.Texture = texture;
    }
}
