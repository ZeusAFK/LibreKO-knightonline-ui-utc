using Godot;
using KnightOnlineUi.Layout;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class HairPaletteWindow : Control
{
    private const string LayoutName = "re_palette";
    private const string PaletteImage = "img_palette";
    private const string ClanPaintNotice = "notice";
    private const string ManualNotice = "notice2";
    private const string ApplyButton = "apply";
    private const int TitleBarHeight = 32;
    private const int PromptText = 18905;
    private const string ManualCaption = "Manual colour input";
    private const string ApplyCaption = "Apply";
    private const int CaptionSize = 12;
    private const float ChannelMax = 255f;
    private static readonly Color CaptionColour = new(0.95f, 0.88f, 0.62f);
    private static readonly string[] ChannelEdits = { "edit_r", "edit_g", "edit_b" };

    private readonly PluginGame game;
    private readonly WindowHost host;
    private readonly LayoutView view;
    private readonly LineEdit?[] channels = new LineEdit?[3];
    private Image? paletteImage;
    private Rect2I paletteRegion;

    public HairPaletteWindow(WindowHost host)
    {
        this.host = host;
        var kit = Plugin.Kit;
        game = kit.Game;
        view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(view);
        host.SetDragHandle(view.MakeDragHandle(new Rect2(0, 0, view.Size.X, TitleBarHeight)));
        view.SetText(ClanPaintNotice, ItemData.Text(PromptText, "Please select your hair color."));
        view.SetText(ManualNotice, ManualCaption);
        view.Recaption(ApplyButton, ApplyCaption, CaptionSize, CaptionColour);
        view.OnPressed(ApplyButton, Apply);
        for (int i = 0; i < ChannelEdits.Length; i++)
            if (view.Get<LineEdit>(ChannelEdits[i]) is { } edit)
            {
                edit.MaxLength = 3;
                edit.TextSubmitted += _ => Apply();
                channels[i] = edit;
            }
        if (view.Get<TextureRect>(PaletteImage) is { Texture: AtlasTexture atlas } palette)
        {
            paletteImage = atlas.Atlas?.GetImage();
            paletteRegion = new Rect2I((Vector2I)atlas.Region.Position, (Vector2I)atlas.Region.Size);
            palette.MouseFilter = MouseFilterEnum.Stop;
            palette.MouseDefaultCursorShape = CursorShape.Cross;
            palette.GuiInput += ev => PickFrom(palette, ev);
        }
        host.Shown += LoadColour;
    }

    private IGameAppearance Target => game.BeautyShop.IsOpen ? game.BeautyShop : game.GenderChange;

    public override void _EnterTree() => LoadColour();

    private void LoadColour() => ShowColour(Target.HairColour);

    private void ShowColour(Color colour)
    {
        float[] parts = { colour.R, colour.G, colour.B };
        for (int i = 0; i < channels.Length; i++)
            if (channels[i] is { } edit) edit.Text = Mathf.RoundToInt(parts[i] * ChannelMax).ToString();
    }

    private void PickFrom(TextureRect palette, InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click || paletteImage == null) return;
        var local = click.Position * (new Vector2(paletteRegion.Size.X, paletteRegion.Size.Y) / palette.Size);
        int x = Mathf.Clamp(paletteRegion.Position.X + (int)local.X, 0, paletteImage.GetWidth() - 1);
        int y = Mathf.Clamp(paletteRegion.Position.Y + (int)local.Y, 0, paletteImage.GetHeight() - 1);
        ShowColour(paletteImage.GetPixel(x, y));
        palette.AcceptEvent();
    }

    private void Apply()
    {
        float Channel(int i) => channels[i] is { } edit && int.TryParse(edit.Text, out int v) ? Mathf.Clamp(v, 0, (int)ChannelMax) / ChannelMax : 1f;
        Target.SetHairColour(new Color(Channel(0), Channel(1), Channel(2)));
        host.Close();
    }
}
