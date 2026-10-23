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
        ui.ReplaceTextInput(request => new TextBox(request));
        ui.ReplaceWindow("changehair", host => new LookWindow(host, LookWindow.BeautyShop, context.Game.BeautyShop));
        ui.ReplaceWindow("genderchange", host => new LookWindow(host, LookWindow.GenderChange, context.Game.GenderChange));
        ui.ReplaceWindow("hairpalette", host => new HairPaletteWindow(host));
        ui.ReplaceWindow("whisper_*", host => new WhisperWindow(host));
        ui.ReplaceWindow("exchange", host => new TradeWindow(host));
        ui.ReplaceWindow("warp", host => new WarpWindow(host));
        ui.ReplaceWindow("attendance", host => new AttendanceWindow(host));
        ui.ReplaceWindow("userinfo", host => new UserInfoWindow(host));
        ui.ReplaceWindow("repurchase", host => new RepurchaseWindow(host));
        ui.ReplaceWindow("user_vote", host => new UserVoteSelectWindow(host));
        ui.ReplaceWindow("user_vote_ask", host => new UserVoteAskWindow(host));
        ui.ReplaceWindow("user_vote_notice", host => new ReportNoticeWindow(host));
        ui.ReplaceWindow("equipview", host => new EquipViewWindow(host));
        ui.ReplaceWindow("cape", host => new CapeShopWindow(host));
        ui.ReplaceWindow("genie", host => new GenieWindow(host));
        ui.ReplaceWindow("draki_board", host => new DrakiRankWindow(host));
        ui.ReplaceHud(HudPart.GenieSub, () => new GenieSubWindow());
        ui.ReplaceHud(HudPart.Party, () => new PartyHud());
        ui.ReplaceHud(HudPart.WarBoard, () => new WarBoardWindow());
        ui.ReplaceHud(HudPart.DrakiPlate, () => new DrakiPlate());
        ui.ReplaceHud(HudPart.DrakiFloor, () => new DrakiFloorBanner());
        ui.ReplacePlayerMenu(request => new PlayerMenu(request));
        ui.ReplaceExitMenu(request => new ExitMenu(request));
        ui.AddHud(() => new Taskbar());
        ui.AddHud(() => new SchedulerWidget());
        context.Log.Info($"{Kit.LayoutCount} layouts, {Kit.TextureCount} textures");
    }

    public void Shutdown()
    {
    }
}
