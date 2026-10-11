using System.Collections.Generic;
using System.Linq;
using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class CapeShopWindow : Control
{
    private const string LayoutName = "re_knightsmantleshop";
    private const string ClothFormat = "area_color_{0}";
    private const string PatternFormat = "area_pattern_{0}";
    private const int CellsPerPage = 6;
    private const int TitleBarHeight = 20;
    private const float LockedAlpha = 0.4f;
    private static readonly Color Picked = new(1f, 0.85f, 0.3f);
    private static readonly Color Short = new(1f, 0.3f, 0.3f);
    private static readonly (string Button, int Text, string Fallback)[] Captions =
    {
        ("btn_purchase", 43864, "Purchase"), ("btn_cancel", 44430, "Cancel"), ("btn_purchase2", 200040, "Ticket Exchange"),
    };

    private readonly PluginGame _game;
    private readonly LayoutView _view;
    private readonly Control?[] _cloths = new Control?[CellsPerPage];
    private readonly Control?[] _patterns = new Control?[CellsPerPage];
    private readonly int[] _clothValues = new int[CellsPerPage];
    private readonly int[] _patternIds = new int[CellsPerPage];
    private Control? _chosen;
    private Color _gradeColour = Colors.White;
    private int _cloth = -1;
    private int _clothPage;
    private int _patternPage;

    public CapeShopWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        _view.Hide("btn_left", "btn_right", "btn_zoom");
        _view.OnPressed("btn_cancel", () => _game.CapeShop.Close());
        _view.OnPressed("btn_purchase", () => _game.CapeShop.Buy());
        _view.OnPressed("btn_purchase2", () => _game.CapeShop.Voucher());
        _view.OnPressed("btn_colordown", () => TurnClothPage(-1));
        _view.OnPressed("btn_colorup", () => TurnClothPage(1));
        _view.OnPressed("btn_patterndown", () => TurnPatternPage(-1));
        _view.OnPressed("btn_patternup", () => TurnPatternPage(1));
        for (int i = 0; i < CellsPerPage; i++)
        {
            int slot = i;
            _cloths[i] = Cell(string.Format(ClothFormat, i), () => PickCloth(_clothValues[slot]));
            _patterns[i] = Cell(string.Format(PatternFormat, i), () =>
            {
                if (_patternIds[slot] > 0) _game.CapeShop.Select(_patternIds[slot]);
            });
        }
        _chosen = _view.Get("area_cloak");
        _gradeColour = _view.Get<Label>("text_grade")?.GetThemeColor("font_color") ?? Colors.White;
        if (_view.Get<Label>("text_notice") is { } notice) notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var layout = kit.Layout(LayoutName);
        foreach (var (button, caption, fallback) in Captions)
            if (layout.Find(button) is { } node && _view.Get<Label>(node, "text") is { } text) text.Text = ItemData.Text(caption, fallback);
    }

    public override void _EnterTree()
    {
        _game.CapeShop.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => _game.CapeShop.Changed -= Refresh;

    private Control? Cell(string id, System.Action pressed)
    {
        if (_view.Get(id) is not { } cell) return null;
        cell.MouseFilter = MouseFilterEnum.Stop;
        cell.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            pressed();
            cell.AcceptEvent();
        };
        return cell;
    }

    private List<int> ClothList() => _game.CapeShop.Capes.Select(c => c.Cloth).Distinct().OrderBy(c => c).ToList();

    private List<GameCape> PatternList() =>
        _game.CapeShop.Capes.Where(c => c.Cloth == _cloth).OrderBy(c => c.Mark).ThenBy(c => c.Id).ToList();

    private void PickCloth(int cloth)
    {
        if (cloth < 0) return;
        _cloth = cloth;
        _patternPage = 0;
        Refresh();
    }

    private void TurnClothPage(int step)
    {
        int pages = Mathf.Max(1, (ClothList().Count + CellsPerPage - 1) / CellsPerPage);
        _clothPage = Mathf.Clamp(_clothPage + step, 0, pages - 1);
        Refresh();
    }

    private void TurnPatternPage(int step)
    {
        int pages = Mathf.Max(1, (PatternList().Count + CellsPerPage - 1) / CellsPerPage);
        _patternPage = Mathf.Clamp(_patternPage + step, 0, pages - 1);
        Refresh();
    }

    private void Refresh()
    {
        var shop = _game.CapeShop;
        var capes = shop.Capes;
        if (_cloth < 0 || capes.All(c => c.Cloth != _cloth))
        {
            var start = capes.FirstOrDefault(c => c.Id == shop.Choice);
            _cloth = start.Id > 0 ? start.Cloth : capes.Count > 0 ? capes[0].Cloth : -1;
        }
        var cloths = ClothList();
        for (int i = 0; i < CellsPerPage; i++)
        {
            int index = _clothPage * CellsPerPage + i;
            _clothValues[i] = index < cloths.Count ? cloths[index] : -1;
            Paint(_cloths[i], _clothValues[i], 0, false, _clothValues[i] == _cloth && _cloth >= 0);
        }
        var patterns = PatternList();
        for (int i = 0; i < CellsPerPage; i++)
        {
            int index = _patternPage * CellsPerPage + i;
            bool used = index < patterns.Count;
            _patternIds[i] = used ? patterns[index].Id : 0;
            Paint(_patterns[i], used ? patterns[index].Cloth : -1, used ? patterns[index].Mark : 0,
                used && patterns[index].Locked, used && patterns[index].Id == shop.Choice);
            if (_patterns[i] is { } cell) cell.TooltipText = used ? patterns[index].Name : "";
        }
        var chosen = capes.FirstOrDefault(c => c.Id == shop.Choice);
        Paint(_chosen, chosen.Id > 0 ? chosen.Cloth : -1, chosen.Mark, false, false);
        _view.SetText("text_colorpage", (_clothPage + 1).ToString());
        _view.SetText("text_patternpage", (_patternPage + 1).ToString());
        _view.SetText("text_money", shop.MoneyLine);
        _view.SetText("text_royalty", shop.ContLine);
        _view.SetFittedText("text_grade", shop.GradeLine);
        if (_view.Get<Label>("text_grade") is { } grade) grade.AddThemeColorOverride("font_color", shop.GradeShort ? Short : _gradeColour);
        _view.SetText("text_notice", shop.Notice.Length > 0 ? shop.Notice : shop.Status);
        if (_view.Get<BaseButton>("btn_purchase") is { } buy) buy.Disabled = !shop.CanBuy;
        if (_view.Get<BaseButton>("btn_purchase2") is { } voucher) voucher.Disabled = !shop.CanVoucher;
    }

    private static void Paint(Control? cell, int cloth, int mark, bool locked, bool picked)
    {
        if (cell == null) return;
        foreach (var child in cell.GetChildren()) child.QueueFree();
        if (cloth < 0) return;
        float alpha = locked ? LockedAlpha : 1f;
        foreach (var texture in new[] { Cape.ClothTexture(cloth), Cape.MarkTexture(mark) })
        {
            if (texture == null) continue;
            var art = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Texture = texture,
                Size = cell.Size,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(1f, 1f, 1f, alpha),
            };
            cell.AddChild(art);
        }
        if (!picked) return;
        var frame = new ReferenceRect
        {
            Size = cell.Size,
            BorderColor = Picked,
            BorderWidth = 2f,
            EditorOnly = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        cell.AddChild(frame);
    }
}
