using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class TextBox : Control
{
    private const string LayoutName = "re_messagebox";
    private const string TitleId = "Text_title";
    private const string MessageId = "Text_Message";
    private const string EditId = "Edit_Common";
    private const string ConfirmId = "Btn_Yes";
    private const string CancelId = "Btn_No";
    private static readonly string[] UnusedIds = { "Btn_Ok", "Btn_Cancel", "Btn_ban", "base_item", "progress_durationtime" };
    private const float MessageGapAboveEdit = 6f;
    private static readonly Color Dim = new(0, 0, 0, 0.45f);

    private readonly TextRequest _request;
    private readonly LayoutView _view;
    private readonly LineEdit? _edit;

    public TextBox(TextRequest request)
    {
        _request = request;
        var kit = Plugin.Kit;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var blocker = new ColorRect { Color = Dim, MouseFilter = MouseFilterEnum.Stop };
        blocker.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(blocker);

        _view = new LayoutView(kit, kit.Layout(LayoutName));
        AddChild(_view);
        _view.Hide(UnusedIds);
        _view.SetText(TitleId, request.Title);
        _view.SetText(MessageId, request.Message);
        _edit = _view.Get<LineEdit>(EditId);
        if (_view.Get(MessageId) is Label message)
        {
            message.HorizontalAlignment = HorizontalAlignment.Center;
            message.VerticalAlignment = VerticalAlignment.Center;
            message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (_edit != null && message.Position.Y + message.Size.Y > _edit.Position.Y - MessageGapAboveEdit)
                message.Size = new Vector2(message.Size.X, Mathf.Max(0f, _edit.Position.Y - MessageGapAboveEdit - message.Position.Y));
        }
        if (_edit != null)
        {
            _edit.Text = request.Initial;
            _edit.PlaceholderText = request.Placeholder;
            _edit.MaxLength = request.MaxLength;
            _edit.Secret = request.Secret;
            _edit.TextSubmitted += _request.Submit;
        }
        _view.OnPressed(ConfirmId, () => _request.Submit(_edit?.Text ?? ""));
        _view.OnPressed(CancelId, _request.Cancel);
    }

    public override void _EnterTree()
    {
        GetViewport().SizeChanged += Center;
        Center();
        _edit?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Center;

    private void Center()
    {
        var room = GetViewport().GetVisibleRect().Size;
        _view.Position = ((room - _view.Size) * 0.5f).Round();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            _request.Cancel();
        }
        else if (k.Keycode is Key.Enter or Key.KpEnter && _edit?.HasFocus() != true)
        {
            GetViewport().SetInputAsHandled();
            _request.Submit(_edit?.Text ?? "");
        }
    }
}
