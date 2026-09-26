using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class MessageBox : Control
{
    private const string OkLayout = "re_msgboxok";
    private const string OkCancelLayout = "re_msgboxokcancel";
    private static readonly Color Dim = new(0, 0, 0, 0.45f);

    private readonly DialogRequest _request;
    private readonly LayoutView _view;

    public MessageBox(DialogRequest request)
    {
        _request = request;
        var kit = Plugin.Kit;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var blocker = new ColorRect { Color = Dim, MouseFilter = MouseFilterEnum.Stop };
        blocker.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(blocker);

        _view = new LayoutView(kit, kit.Layout(request.HasCancel ? OkCancelLayout : OkLayout));
        AddChild(_view);
        _view.SetText("text_msg", request.Message);
        if (_view.Get("text_msg") is Label msg)
        {
            msg.HorizontalAlignment = HorizontalAlignment.Center;
            msg.VerticalAlignment = VerticalAlignment.Center;
            msg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }
        _view.OnPressed("btn_ok", () => { if (request.HasCancel) request.Confirm(); else request.Dismiss(); });
        _view.OnPressed("btn_cancel", request.Cancel);
        if (!request.Dismissable && _view.Get("btn_ok") is { } ok) ok.Visible = false;
        request.MessageChanged += text => _view.SetText("text_msg", text);
    }

    public override void _EnterTree()
    {
        GetViewport().SizeChanged += Center;
        Center();
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Center;

    private void Center()
    {
        var room = GetViewport().GetVisibleRect().Size;
        _view.Position = ((room - _view.Size) * 0.5f).Round();
    }

    public override void _Input(InputEvent ev)
    {
        if (!_request.Dismissable || ev is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode is Key.Enter or Key.KpEnter)
        {
            GetViewport().SetInputAsHandled();
            if (_request.HasCancel) _request.Confirm(); else _request.Dismiss();
        }
        else if (k.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            if (_request.HasCancel) _request.Cancel(); else _request.Dismiss();
        }
    }
}
