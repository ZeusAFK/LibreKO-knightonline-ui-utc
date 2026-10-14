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

    private readonly TextRequest request;
    private readonly LayoutView view;
    private readonly LineEdit? edit;

    public TextBox(TextRequest request)
    {
        this.request = request;
        var kit = Plugin.Kit;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var blocker = new ColorRect { Color = Dim, MouseFilter = MouseFilterEnum.Stop };
        blocker.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(blocker);

        view = new LayoutView(kit, kit.Layout(LayoutName));
        AddChild(view);
        view.Hide(UnusedIds);
        view.SetText(TitleId, request.Title);
        view.SetText(MessageId, request.Message);
        edit = view.Get<LineEdit>(EditId);
        if (view.Get(MessageId) is Label message)
        {
            message.HorizontalAlignment = HorizontalAlignment.Center;
            message.VerticalAlignment = VerticalAlignment.Center;
            message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (edit != null && message.Position.Y + message.Size.Y > edit.Position.Y - MessageGapAboveEdit)
                message.Size = new Vector2(message.Size.X, Mathf.Max(0f, edit.Position.Y - MessageGapAboveEdit - message.Position.Y));
        }
        if (edit != null)
        {
            edit.Text = request.Initial;
            edit.PlaceholderText = request.Placeholder;
            edit.MaxLength = request.MaxLength;
            edit.Secret = request.Secret;
            edit.TextSubmitted += this.request.Submit;
        }
        view.OnPressed(ConfirmId, () => this.request.Submit(edit?.Text ?? ""));
        view.OnPressed(CancelId, this.request.Cancel);
    }

    public override void _EnterTree()
    {
        GetViewport().SizeChanged += Center;
        Center();
        edit?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Center;

    private void Center()
    {
        var room = GetViewport().GetVisibleRect().Size;
        view.Position = ((room - view.Size) * 0.5f).Round();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            request.Cancel();
        }
        else if (k.Keycode is Key.Enter or Key.KpEnter && edit?.HasFocus() != true)
        {
            GetViewport().SetInputAsHandled();
            request.Submit(edit?.Text ?? "");
        }
    }
}
