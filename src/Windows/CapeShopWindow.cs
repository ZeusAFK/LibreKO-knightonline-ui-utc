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

    private readonly PluginGame game;
    private readonly LayoutView view;
    private readonly Control?[] cloths = new Control?[CellsPerPage];
    private readonly Control?[] patterns = new Control?[CellsPerPage];
    private readonly int[] clothValues = new int[CellsPerPage];
    private readonly int[] patternIds = new int[CellsPerPage];
    private Control? chosen;
    private Color gradeColour = Colors.White;
    private int cloth = -1;
    private int clothPage;
    private int patternPage;

    public CapeShopWindow(WindowHost host)
    {
        var kit = Plugin.Kit;
        game = kit.Game;
        view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        view.Hide("btn_left", "btn_right", "btn_zoom");
        view.OnPressed("btn_cancel", () => game.CapeShop.Close());
        view.OnPressed("btn_purchase", () => game.CapeShop.Buy());
        view.OnPressed("btn_purchase2", () => game.CapeShop.Voucher());
        view.OnPressed("btn_colordown", () => TurnClothPage(-1));
        view.OnPressed("btn_colorup", () => TurnClothPage(1));
        view.OnPressed("btn_patterndown", () => TurnPatternPage(-1));
        view.OnPressed("btn_patternup", () => TurnPatternPage(1));
        for (int i = 0; i < CellsPerPage; i++)
        {
            int slot = i;
            cloths[i] = Cell(string.Format(ClothFormat, i), () => PickCloth(clothValues[slot]));
            patterns[i] = Cell(string.Format(PatternFormat, i), () =>
            {
                if (patternIds[slot] > 0) game.CapeShop.Select(patternIds[slot]);
            });
        }
        chosen = view.Get("area_cloak");
        gradeColour = view.Get<Label>("text_grade")?.GetThemeColor("font_color") ?? Colors.White;
        if (view.Get<Label>("text_notice") is { } notice) notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var layout = kit.Layout(LayoutName);
        foreach (var (button, caption, fallback) in Captions)
            if (layout.Find(button) is { } node && view.Get<Label>(node, "text") is { } text) text.Text = ItemData.Text(caption, fallback);
    }

    public override void _EnterTree()
    {
        game.CapeShop.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => game.CapeShop.Changed -= Refresh;

    private Control? Cell(string id, System.Action pressed)
    {
        if (view.Get(id) is not { } cell) return null;
        cell.MouseFilter = MouseFilterEnum.Stop;
        cell.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            pressed();
            cell.AcceptEvent();
        };
        return cell;
    }

    private List<int> ClothList() => game.CapeShop.Capes.Select(c => c.Cloth).Distinct().OrderBy(c => c).ToList();

    private List<GameCape> PatternList() =>
        game.CapeShop.Capes.Where(c => c.Cloth == cloth).OrderBy(c => c.Mark).ThenBy(c => c.Id).ToList();

    private void PickCloth(int cloth)
    {
        if (cloth < 0) return;
        this.cloth = cloth;
        patternPage = 0;
        Refresh();
    }

    private void TurnClothPage(int step)
    {
        int pages = Mathf.Max(1, (ClothList().Count + CellsPerPage - 1) / CellsPerPage);
        clothPage = Mathf.Clamp(clothPage + step, 0, pages - 1);
        Refresh();
    }

    private void TurnPatternPage(int step)
    {
        int pages = Mathf.Max(1, (PatternList().Count + CellsPerPage - 1) / CellsPerPage);
        patternPage = Mathf.Clamp(patternPage + step, 0, pages - 1);
        Refresh();
    }

    private void Refresh()
    {
        var shop = game.CapeShop;
        var capes = shop.Capes;
        if (cloth < 0 || capes.All(c => c.Cloth != cloth))
        {
            var start = capes.FirstOrDefault(c => c.Id == shop.Choice);
            cloth = start.Id > 0 ? start.Cloth : capes.Count > 0 ? capes[0].Cloth : -1;
        }
        var availableCloths = ClothList();
        for (int i = 0; i < CellsPerPage; i++)
        {
            int index = clothPage * CellsPerPage + i;
            clothValues[i] = index < availableCloths.Count ? availableCloths[index] : -1;
            Paint(cloths[i], clothValues[i], 0, false, clothValues[i] == cloth && cloth >= 0);
        }
        var patternCapes = PatternList();
        for (int i = 0; i < CellsPerPage; i++)
        {
            int index = patternPage * CellsPerPage + i;
            bool used = index < patternCapes.Count;
            patternIds[i] = used ? patternCapes[index].Id : 0;
            Paint(patterns[i], used ? patternCapes[index].Cloth : -1, used ? patternCapes[index].Mark : 0,
                used && patternCapes[index].Locked, used && patternCapes[index].Id == shop.Choice);
            if (patterns[i] is { } cell) cell.TooltipText = used ? patternCapes[index].Name : "";
        }
        var chosenCape = capes.FirstOrDefault(c => c.Id == shop.Choice);
        Paint(chosen, chosenCape.Id > 0 ? chosenCape.Cloth : -1, chosenCape.Mark, false, false);
        view.SetText("text_colorpage", (clothPage + 1).ToString());
        view.SetText("text_patternpage", (patternPage + 1).ToString());
        view.SetText("text_money", shop.MoneyLine);
        view.SetText("text_royalty", shop.ContLine);
        view.SetFittedText("text_grade", shop.GradeLine);
        if (view.Get<Label>("text_grade") is { } grade) grade.AddThemeColorOverride("font_color", shop.GradeShort ? Short : gradeColour);
        view.SetText("text_notice", shop.Notice.Length > 0 ? shop.Notice : shop.Status);
        if (view.Get<BaseButton>("btn_purchase") is { } buy) buy.Disabled = !shop.CanBuy;
        if (view.Get<BaseButton>("btn_purchase2") is { } voucher) voucher.Disabled = !shop.CanVoucher;
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
