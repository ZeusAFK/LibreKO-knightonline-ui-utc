using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class GenieWindow : Control
{
    private const string LayoutName = "re_genie";
    private const int TitleBarHeight = 60;
    private const int OverlayFontSize = 10;
    private const int RowFontSize = 10;
    private const int RecoveryCellsFirstArea = 9;
    private const int PotionFirstArea = 17;
    private const int SupportFirstArea = 21;
    private const int PercentMax = 100;
    private const int TransformBandTop = 255;
    private const int TransformBandBottom = 290;
    private const int TransformBandLeft = 100;
    private static readonly string[] RateLines = { "str_hp_rate", "str_mp_rate", "str_pet_rate" };
    private static readonly string[] Tabs = { "btn_explan", "btn_attack", "btn_recovery", "btn_assist", "btn_etc" };
    private static readonly string[] Pages = { "explain", "attack", "recovery", "assist", "etc" };
    private static readonly (string Id, int Text, string Fallback)[] Texts =
    {
        ("str_genie_name", 31016, "Knight Genie"), ("str_explain_title", 31017, "Note"),
        ("str_att_mode", 31019, "Attack mode"), ("str_rc_mode", 31020, "Recovery mode"),
        ("str_add_skill", 31021, "Skills register"), ("str_skill_rate", 31022, "Use recovery skill at %"),
        ("str_att_range", 31023, "Attack range"), ("str_act_range", 31024, "Range of activities"),
        ("str_not_attack", 31070, "Basic Attack Off"), ("str_rapid_hit", 31074, "Combo"),
        ("Attack_actrange_str", 31071, "Hunting in place"), ("str_hide_attack_range", 31072, "Hide attack range"),
        ("str_hp", 31025, "HP"), ("str_mp", 31026, "MP"), ("str_pet", 31027, "Pet"),
        ("str_hp_recovery", 31028, "Recovery"), ("str_mp_recovery", 31028, "Recovery"), ("str_pet_recovery", 31028, "Recovery"),
        ("str_ass_use_set", 31030, "Set the automatic use of 2nd skill"), ("str_party_set", 31031, "Set skills to be applied"),
        ("str_removecurse_onoff", 31078, "Auto Curse Removal"), ("str_etc_title", 31033, "Set Others"),
    };
    private static readonly (string On, string Off, Func<GenieSetup, bool> Get, Action<GenieSetup, bool> Set)[] Switches =
    {
        ("btn_auto_attack_on", "btn_auto_attack_off", s => s.Has(GenieMode.Attack), (s, v) => s.Set(GenieMode.Attack, v)),
        ("btn_auto_recovery_on", "btn_auto_recovery_off", s => s.Has(GenieMode.Recovery), (s, v) => s.Set(GenieMode.Recovery, v)),
        ("btn_hp_recovery_on", "btn_hp_recovery_off", s => s.Has(GenieMode.HpPotion), (s, v) => s.Set(GenieMode.HpPotion, v)),
        ("btn_mp_recovery_on", "btn_mp_recovery_off", s => s.Has(GenieMode.MpPotion), (s, v) => s.Set(GenieMode.MpPotion, v)),
        ("btn_pet_recovery_on", "btn_pet_recovery_off", s => s.Has(GenieMoreMode.PetPotion), (s, v) => s.Set(GenieMoreMode.PetPotion, v)),
        ("btn_assist_on", "btn_assist_off", s => s.Has(GenieMode.Support), (s, v) => s.Set(GenieMode.Support, v)),
    };
    private static readonly (string Id, Func<GenieSetup, bool> Get, Action<GenieSetup, bool> Set)[] Boxes =
    {
        ("btn_not_attack", s => !s.Has(GenieMode.BasicAttack), (s, v) => s.Set(GenieMode.BasicAttack, !v)),
        ("btn_rapid_hit", s => s.Has(GenieMode.Combo), (s, v) => s.Set(GenieMode.Combo, v)),
        ("scr_act_range", s => s.Has(GenieExtra.HuntInPlace), (s, v) => s.Set(GenieExtra.HuntInPlace, v)),
        ("btn_hide_attack_range", s => s.Has(GenieExtra.HideRange), (s, v) => s.Set(GenieExtra.HideRange, v)),
        ("btn_no_enemy_stay_in_place", s => !s.Has(GenieExtra.StayInPlace), (s, v) => s.Set(GenieExtra.StayInPlace, !v)),
        ("btn_removecurse_onoff", s => s.Has(GenieExtra.CureCurse), (s, v) => s.Set(GenieExtra.CureCurse, v)),
    };
    private static readonly (string Area, string Count, GeniePotionSlot Slot)[] PotionCells =
    {
        ("17", "8", GeniePotionSlot.Hp), ("18", "9", GeniePotionSlot.Pet), ("19", "10", GeniePotionSlot.Mp),
    };

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly LayoutNode _layout;
    private readonly List<(TextureButton On, TextureButton Off, Func<GenieSetup, bool> Get)> _switches = new();
    private readonly List<(TextureButton Box, Func<GenieSetup, bool> Get)> _boxes = new();
    private readonly List<GenieCell> _attackCells = new();
    private readonly List<GenieCell> _recoveryCells = new();
    private readonly List<GenieCell> _supportCells = new();
    private readonly List<TextureButton> _partyBoxes = new();
    private readonly List<TextureButton> _otherBoxes = new();
    private readonly List<(GenieCell Cell, Label? Count, GeniePotionSlot Slot)> _potions = new();
    private readonly List<(RetailTrackbar Bar, Label? Text, Func<GenieSetup, int> Get, Func<int, string> Show)> _bars = new();
    private readonly LineEdit? _hpCount;
    private readonly LineEdit? _mpCount;
    private GenieSetup _setup = new();
    private int _page = 1;
    private bool _recoveryStyle;

    public GenieWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, _layout);
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        _view.OnPressed("btn_close", () => _game.Genie.Close());
        foreach (var (id, text, fallback) in Texts) _view.SetText(id, ItemData.Text(text, fallback));
        if (_view.Get<Label>("str_no_enemy_stay_in_place") is { } home)
            home.Text = ItemData.Text(31073, "When no monsters are in the attack range, move to Genie starting location.");
        if (_view.Get<TextureButton>("btn_rapid_hit") is { } combo) combo.TooltipText = ItemData.Text(31075, "Failed to execute certain skills in the combo.");
        for (int i = 0; i < GenieSetup.SupportCells; i++) _view.SetText($"str_{i + 1:00}", ItemData.Text(31032, "Party"));
        _view.Hide("btn_transform_onoff", "str_transform", "btn_transform_list", "grp_transform_list", "EtcHP_str", "EtcMp_str");
        HideTransformBand();
        foreach (var id in RateLines) _view.SetFittedText(id, ItemData.Text(31029, "at % consume potion automatically"));
        BuildExplanation();
        for (int i = 0; i < Tabs.Length; i++)
        {
            int page = i;
            if (_view.Get<TextureButton>(Tabs[i]) is { } tab) tab.ToggleMode = true;
            _view.OnPressed(Tabs[i], () => ShowPage(page));
        }
        _view.OnPressed("btn_attack_style", () => SetRecoveryStyle(false));
        _view.OnPressed("btn_recovery_style", () => SetRecoveryStyle(true));
        foreach (var id in new[] { "btn_attack_style", "btn_recovery_style" })
            if (_view.Get<TextureButton>(id) is { } style) style.ToggleMode = true;
        foreach (var (on, off, get, set) in Switches)
            if (_view.Get<TextureButton>(on) is { } onButton && _view.Get<TextureButton>(off) is { } offButton)
            {
                onButton.ToggleMode = offButton.ToggleMode = true;
                onButton.Pressed += () => Edit(s => set(s, true));
                offButton.Pressed += () => Edit(s => set(s, false));
                _switches.Add((onButton, offButton, get));
            }
        foreach (var (id, get, set) in Boxes)
            if (_view.Get<TextureButton>(id) is { } box)
            {
                box.ToggleMode = true;
                box.Toggled += on => Edit(s => set(s, on));
                _boxes.Add((box, get));
            }
        Bar("scr_autorecovery_rate", "Attack_autorecovery_str", GenieSetup.MinPercent, GenieSetup.MaxPercent,
            s => s.RecoveryPercent, (s, v) => s.RecoveryPercent = v, v => $"{v}%");
        Bar("scr_att_range", "Attack_range_str", GenieSetup.MinRange, GenieSetup.MaxRange,
            s => s.Range, (s, v) => s.Range = v, v => $"{v}m");
        Bar("scr_hp_recovery_rate", "RecoveryHp_str", GenieSetup.MinPercent, GenieSetup.MaxPercent,
            s => s.HpPercent, (s, v) => s.HpPercent = v, v => $"{v}%");
        Bar("scr_mp_recovery_rate", "RecoveryMp_str", GenieSetup.MinPercent, GenieSetup.MaxPercent,
            s => s.MpPercent, (s, v) => s.MpPercent = v, v => $"{v}%");
        Bar("scr_pet_recovery_rate", "RecoveryPet_str", GenieSetup.MinPercent, GenieSetup.MaxPercent,
            s => s.PetPercent, (s, v) => s.PetPercent = v, v => $"{v}%");
        for (int i = 0; i < GenieSetup.AttackCells; i++) _attackCells.Add(Cell((i + 1).ToString(), GenieCellKind.Attack, i));
        for (int i = 0; i < GenieSetup.RecoveryCells; i++)
            _recoveryCells.Add(Cell((RecoveryCellsFirstArea + i).ToString(), GenieCellKind.Recovery, i));
        for (int i = 0; i < GenieSetup.SupportCells; i++)
        {
            _supportCells.Add(Cell((SupportFirstArea + i).ToString(), GenieCellKind.Support, i));
            int index = i;
            if (_view.Get<TextureButton>($"btn_party_{i + 1}") is { } party)
            {
                party.ToggleMode = true;
                party.Toggled += on => Edit(s => s.SupportParty[index] = on);
                _partyBoxes.Add(party);
            }
        }
        foreach (var (area, count, slot) in PotionCells)
            if (Cell(area, GenieCellKind.Recovery, -1) is { } cell)
            {
                var potion = slot;
                cell.PotionClick = at => _game.Genie.PickPotion(potion, at);
                _potions.Add((cell, _view.Get<Label>(count), slot));
            }
        var rows = GenieTownReturn.OtherRows;
        for (int i = 0; i < rows.Length; i++)
        {
            var option = rows[i].Option;
            if (_view.Get<Label>($"str_etc_{i + 1:00}") is { } label)
            {
                label.Text = rows[i].Text > 0 ? ItemData.Text(rows[i].Text, rows[i].Label) : rows[i].Label;
                label.AddThemeFontSizeOverride("font_size", RowFontSize);
                label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                label.VerticalAlignment = VerticalAlignment.Center;
            }
            if (_view.Get<TextureButton>($"btn_etc_{i + 1}") is { } box && _layout.Find($"btn_etc_{i + 1}") is { } boxNode)
            {
                box.ToggleMode = true;
                NestedStates(box, boxNode);
                box.Toggled += on => Edit(s => s.Set(option, on));
                _otherBoxes.Add(box);
            }
        }
        _hpCount = Count("edit_hp_count", (s, v) => s.TownHpCount = v);
        _mpCount = Count("edit_mp_count", (s, v) => s.TownMpCount = v);
        BuildMoreLink();
        ShowPage(_page);
    }

    public override void _EnterTree()
    {
        _game.Genie.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _game.Genie.Changed -= Refresh;

    private void BuildExplanation()
    {
        if (_layout.Find("str_explain") is not { } node || _view.Get<Label>("str_explain") is not { } label) return;
        var clip = new Control { ClipContents = true, Position = label.Position, Size = node.SizeVec, MouseFilter = MouseFilterEnum.Ignore };
        label.GetParent().AddChild(clip);
        label.Reparent(clip, false);
        label.Position = Vector2.Zero;
        label.Text = ItemData.Text(31018, "");
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.Size = new Vector2(node.W, 0);
        label.CustomMinimumSize = new Vector2(node.W, 0);
        if (_layout.Find("scr_act_height") is not { } bar) return;
        var scroll = new RetailTrackbar(_view, bar, 0, PercentMax, 10, value =>
        {
            float hidden = Mathf.Max(0f, label.GetCombinedMinimumSize().Y - node.H);
            label.Position = new Vector2(0, -hidden * value / PercentMax);
        });
        scroll.Value = 0;
    }

    private void HideTransformBand()
    {
        if (_layout.Find("assist") is not { } assist) return;
        foreach (var image in assist.Images)
            if (image.Y > TransformBandTop && image.Y < TransformBandBottom && image.X >= TransformBandLeft && _view.ControlOf(image) is { } art)
                art.Visible = false;
    }

    private void NestedStates(TextureButton button, LayoutNode node)
    {
        var layers = new Dictionary<int, List<TextureRect>>();
        foreach (var state in node.Images)
        {
            var parts = new List<TextureRect>();
            foreach (var inner in state.Images)
            {
                if (_view.Atlas(inner) is not { } atlas) continue;
                var part = new TextureRect
                {
                    Texture = atlas,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    Position = new Vector2(inner.X - node.X, inner.Y - node.Y),
                    Size = inner.SizeVec,
                    MouseFilter = MouseFilterEnum.Ignore,
                    Visible = false,
                };
                button.AddChild(part);
                parts.Add(part);
            }
            layers[state.Tag] = parts;
        }
        bool hovering = false;
        void Apply()
        {
            int shown = button.ButtonPressed ? ButtonState.Pressed : hovering && layers.ContainsKey(ButtonState.Hover) ? ButtonState.Hover : ButtonState.Normal;
            foreach (var (state, parts) in layers)
                foreach (var part in parts) part.Visible = state == shown;
        }
        button.Toggled += _ => Apply();
        button.Draw += Apply;
        button.MouseEntered += () => { hovering = true; Apply(); };
        button.MouseExited += () => { hovering = false; Apply(); };
        Apply();
    }

    private void BuildMoreLink()
    {
        if (!_game.Genie.HasMore) return;
        var more = new LinkButton
        {
            Text = "More options",
            Underline = LinkButton.UnderlineMode.OnHover,
            Position = new Vector2(48, _layout.H - 34),
            FocusMode = FocusModeEnum.None,
            TooltipText = "Monster list, party code, scrolls, potion choice and the hammer setting",
        };
        more.AddThemeFontSizeOverride("font_size", OverlayFontSize);
        more.AddThemeColorOverride("font_color", new Color(0.93f, 0.92f, 0.45f));
        more.Pressed += () => _game.Genie.OpenMore();
        _view.AddChild(more);
    }

    private void Bar(string id, string textId, int min, int max, Func<GenieSetup, int> get, Action<GenieSetup, int> set, Func<int, string> show)
    {
        if (_layout.Find(id) is not { } node) return;
        var label = _view.Get<Label>(textId);
        var bar = new RetailTrackbar(_view, node, min, max, 1, v =>
        {
            if (label != null) label.Text = show(v);
            Edit(s => set(s, v));
        });
        _bars.Add((bar, label, get, show));
    }

    private LineEdit? Count(string id, Action<GenieSetup, int> set)
    {
        if (_view.Get<LineEdit>(id) is not { } edit) return null;
        edit.MaxLength = 3;
        edit.Alignment = HorizontalAlignment.Center;
        void Commit()
        {
            int value = int.TryParse(edit.Text, out int v) ? Math.Clamp(v, GenieTownReturn.MinCount, GenieTownReturn.MaxCount) : GenieTownReturn.DefaultCount;
            edit.Text = value.ToString();
            Edit(s => set(s, value));
        }
        edit.TextSubmitted += _ => Commit();
        edit.FocusExited += Commit;
        return edit;
    }

    private GenieCell? Cell(string area, GenieCellKind kind, int index)
    {
        if (_view.Get(area) is not { } host) return null;
        var cell = new GenieCell(_game, kind, index) { Size = host.Size };
        host.MouseFilter = MouseFilterEnum.Pass;
        host.AddChild(cell);
        return cell;
    }

    private void Edit(Action<GenieSetup> change)
    {
        change(_setup);
        _game.Genie.Apply(_setup);
    }

    private void ShowPage(int page)
    {
        _page = page;
        for (int i = 0; i < Pages.Length; i++)
        {
            if (_view.Get(Pages[i]) is { } pageControl) pageControl.Visible = i == page;
            if (_view.Get<TextureButton>(Tabs[i]) is { } tab) tab.SetPressedNoSignal(i == page);
        }
        SetRecoveryStyle(_recoveryStyle);
    }

    private void SetRecoveryStyle(bool recovery)
    {
        _recoveryStyle = recovery;
        foreach (var cell in _attackCells) cell.Visible = !recovery;
        foreach (var cell in _recoveryCells) cell.Visible = recovery;
        if (_view.Get<TextureButton>("btn_attack_style") is { } attack) attack.SetPressedNoSignal(!recovery);
        if (_view.Get<TextureButton>("btn_recovery_style") is { } heal) heal.SetPressedNoSignal(recovery);
    }

    private void Refresh()
    {
        var genie = _game.Genie;
        _setup = genie.Setup;
        foreach (var (on, off, get) in _switches)
        {
            bool value = get(_setup);
            on.SetPressedNoSignal(value);
            off.SetPressedNoSignal(!value);
        }
        foreach (var (box, get) in _boxes) box.SetPressedNoSignal(get(_setup));
        for (int i = 0; i < _partyBoxes.Count; i++) _partyBoxes[i].SetPressedNoSignal(_setup.SupportParty[i]);
        var rows = GenieTownReturn.OtherRows;
        for (int i = 0; i < _otherBoxes.Count && i < rows.Length; i++) _otherBoxes[i].SetPressedNoSignal(_setup.Has(rows[i].Option));
        foreach (var (bar, text, get, show) in _bars)
        {
            int value = get(_setup);
            bar.Value = value;
            if (text != null) text.Text = show(value);
        }
        if (_hpCount != null && !_hpCount.HasFocus()) _hpCount.Text = _setup.TownHpCount.ToString();
        if (_mpCount != null && !_mpCount.HasFocus()) _mpCount.Text = _setup.TownMpCount.ToString();
        foreach (var cell in _attackCells.Concat(_recoveryCells).Concat(_supportCells)) cell.Show(genie.Cell(cell.Kind, cell.Index));
        foreach (var (cell, count, slot) in _potions)
        {
            var potion = genie.Potion(slot);
            cell.ShowPotion(potion);
            if (count != null) count.Text = potion.ItemId > 0 ? potion.Count.ToString() : "";
        }
    }

    private sealed partial class GenieCell : Control
    {
        private const string DragIdKey = "id";
        private const string DragCellKey = "genieCell";
        private const int IconInset = 1;

        private readonly PluginGame _game;
        private readonly TextureRect _icon;
        private bool _dragging;

        public GenieCellKind Kind { get; }
        public int Index { get; }
        public Action<Vector2>? PotionClick { get; set; }

        public GenieCell(PluginGame game, GenieCellKind kind, int index)
        {
            _game = game;
            Kind = kind;
            Index = index;
            MouseFilter = MouseFilterEnum.Stop;
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
        }

        private bool IsPotion => Index < 0;

        public void Show(GenieCellView view)
        {
            _icon.Texture = view.Icon;
            TooltipText = view.Tooltip;
        }

        public void ShowPotion(GeniePotionView view)
        {
            _icon.Texture = view.Icon;
            TooltipText = view.ItemId > 0 ? $"{view.Name} ({view.Count})" : "Automatic (strongest)";
        }

        public override void _GuiInput(InputEvent ev)
        {
            if (ev is not InputEventMouseButton button) return;
            if (button.ButtonIndex == MouseButton.Right && button.Pressed)
            {
                if (!IsPotion) _game.Genie.SetCell(Kind, Index, 0);
                AcceptEvent();
                return;
            }
            if (button.ButtonIndex != MouseButton.Left) return;
            if (!button.Pressed)
            {
                if (IsPotion) PotionClick?.Invoke(GetGlobalMousePosition());
                else _game.Genie.PickSkill(Kind, Index, GetGlobalMousePosition());
            }
            AcceptEvent();
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            if (IsPotion || _icon.Texture == null || _game.Genie.Running) return default;
            var preview = new TextureRect
            {
                Texture = _icon.Texture,
                CustomMinimumSize = Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
            };
            SetDragPreview(preview);
            _dragging = true;
            return new Godot.Collections.Dictionary
            {
                { DragIdKey, _game.Genie.Cell(Kind, Index).SkillId }, { DragCellKey, $"{(int)Kind}:{Index}" },
            };
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data) =>
            !IsPotion && data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey(DragIdKey);

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            var d = data.AsGodotDictionary();
            if (d.ContainsKey(DragCellKey) && d[DragCellKey].AsString().Split(':') is [var kind, var index]
                && int.TryParse(kind, out int fromKind) && int.TryParse(index, out int fromIndex))
                _game.Genie.MoveCell((GenieCellKind)fromKind, fromIndex, Kind, Index);
            else
                _game.Genie.SetCell(Kind, Index, d[DragIdKey].AsInt32());
        }

        public override void _Notification(int what)
        {
            if (what != NotificationDragEnd || !_dragging) return;
            _dragging = false;
            if (!GetViewport().GuiIsDragSuccessful()) _game.Genie.SetCell(Kind, Index, 0);
        }
    }
}
