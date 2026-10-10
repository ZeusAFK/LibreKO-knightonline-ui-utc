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

    private readonly PluginGame _game;
    private readonly WindowHost _host;
    private readonly LayoutView _view;
    private readonly LineEdit?[] _channels = new LineEdit?[3];
    private Image? _paletteImage;
    private Rect2I _paletteRegion;

    public HairPaletteWindow(WindowHost host)
    {
        _host = host;
        var kit = Plugin.Kit;
        _game = kit.Game;
        _view = new LayoutView(kit, kit.Layout(LayoutName));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_view);
        host.SetDragHandle(_view.MakeDragHandle(new Rect2(0, 0, _view.Size.X, TitleBarHeight)));
        _view.SetText(ClanPaintNotice, ItemData.Text(PromptText, "Please select your hair color."));
        _view.SetText(ManualNotice, ManualCaption);
        _view.Recaption(ApplyButton, ApplyCaption, CaptionSize, CaptionColour);
        _view.OnPressed(ApplyButton, Apply);
        for (int i = 0; i < ChannelEdits.Length; i++)
            if (_view.Get<LineEdit>(ChannelEdits[i]) is { } edit)
            {
                edit.MaxLength = 3;
                edit.TextSubmitted += _ => Apply();
                _channels[i] = edit;
            }
        if (_view.Get<TextureRect>(PaletteImage) is { Texture: AtlasTexture atlas } palette)
        {
            _paletteImage = atlas.Atlas?.GetImage();
            _paletteRegion = new Rect2I((Vector2I)atlas.Region.Position, (Vector2I)atlas.Region.Size);
            palette.MouseFilter = MouseFilterEnum.Stop;
            palette.MouseDefaultCursorShape = CursorShape.Cross;
            palette.GuiInput += ev => PickFrom(palette, ev);
        }
        host.Shown += LoadColour;
    }

    private IGameAppearance Target => _game.BeautyShop.IsOpen ? _game.BeautyShop : _game.GenderChange;

    public override void _EnterTree() => LoadColour();

    private void LoadColour() => ShowColour(Target.HairColour);

    private void ShowColour(Color colour)
    {
        float[] parts = { colour.R, colour.G, colour.B };
        for (int i = 0; i < _channels.Length; i++)
            if (_channels[i] is { } edit) edit.Text = Mathf.RoundToInt(parts[i] * ChannelMax).ToString();
    }

    private void PickFrom(TextureRect palette, InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click || _paletteImage == null) return;
        var local = click.Position * (new Vector2(_paletteRegion.Size.X, _paletteRegion.Size.Y) / palette.Size);
        int x = Mathf.Clamp(_paletteRegion.Position.X + (int)local.X, 0, _paletteImage.GetWidth() - 1);
        int y = Mathf.Clamp(_paletteRegion.Position.Y + (int)local.Y, 0, _paletteImage.GetHeight() - 1);
        ShowColour(_paletteImage.GetPixel(x, y));
        palette.AcceptEvent();
    }

    private void Apply()
    {
        float Channel(int i) => _channels[i] is { } edit && int.TryParse(edit.Text, out int v) ? Mathf.Clamp(v, 0, (int)ChannelMax) / ChannelMax : 1f;
        Target.SetHairColour(new Color(Channel(0), Channel(1), Channel(2)));
        _host.Close();
    }
}
