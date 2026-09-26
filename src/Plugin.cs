using KnightOnlineUi.Windows;
using LibreKO.Plugins;

namespace KnightOnlineUi;

public sealed class Plugin : IPlugin
{
    public static UiKit Kit { get; private set; } = null!;

    public void Initialize(PluginContext context)
    {
        Kit = new UiKit(context);
        var ui = context.Ui;
        ui.ReplaceHud(HudPart.StatusBars, () => new StatusHud());
        ui.ReplaceHud(HudPart.TargetFrame, () => new TargetFrame());
        ui.ReplaceHud(HudPart.Hotbar, () => new HotkeyBar());
        ui.ReplaceHud(HudPart.MiniMap, () => new MiniMapFrame());
        ui.ReplaceHud(HudPart.Chat, () => new ChatWindow());
        ui.ReplaceHud(HudPart.CombatLog, () => new LogWindow());
        ui.ReplaceHud(HudPart.QuestTracker, () => new QuestTracker());
        ui.HideHud(HudPart.Launcher);
        ui.HideHud(HudPart.ExpBar);
        ui.ReplaceWindow("inventory", host => new InventoryWindow(host));
        ui.ReplaceWindow("character_info", host => new CharacterWindow(host));
        ui.ReplaceWindow("skills", host => new SkillWindow(host));
        ui.ReplaceWindow("globalmap", host => new WorldMapWindow(host));
        ui.ReplaceDialogs(request => new MessageBox(request));
        ui.AddHud(() => new Taskbar());
        context.Log.Info($"{Kit.LayoutCount} layouts, {Kit.TextureCount} textures");
    }

    public void Shutdown()
    {
    }
}
