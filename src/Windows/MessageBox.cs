using Godot;
using KnightOnlineUi.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUi.Windows;

public partial class MessageBox : Control
{
    private const string OkLayout = "re_msgboxok";
    private const string OkCancelLayout = "re_msgboxokcancel";
    private static readonly Color Dim = new(0, 0, 0, 0.45f);

    private readonly DialogRequest request;
    private readonly LayoutView view;

    public MessageBox(DialogRequest request)
    {
        this.request = request;
        var kit = Plugin.Kit;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var blocker = new ColorRect { Color = Dim, MouseFilter = MouseFilterEnum.Stop };
        blocker.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(blocker);

        view = new LayoutView(kit, kit.Layout(request.HasCancel ? OkCancelLayout : OkLayout));
        AddChild(view);
        view.SetText("text_msg", request.Message);
        if (view.Get("text_msg") is Label msg)
        {
            msg.HorizontalAlignment = HorizontalAlignment.Center;
            msg.VerticalAlignment = VerticalAlignment.Center;
            msg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }
        view.OnPressed("btn_ok", () => { if (request.HasCancel) request.Confirm(); else request.Dismiss(); });
        view.OnPressed("btn_cancel", request.Cancel);
        if (!request.Dismissable && view.Get("btn_ok") is { } ok) ok.Visible = false;
        request.MessageChanged += text => view.SetText("text_msg", text);
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
        view.Position = ((room - view.Size) * 0.5f).Round();
    }

    public override void _Input(InputEvent ev)
    {
        if (!request.Dismissable || ev is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode is Key.Enter or Key.KpEnter)
        {
            GetViewport().SetInputAsHandled();
            if (request.HasCancel) request.Confirm(); else request.Dismiss();
        }
        else if (k.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            if (request.HasCancel) request.Cancel(); else request.Dismiss();
        }
    }
}
