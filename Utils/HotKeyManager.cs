using BepInEx.Configuration;
using EFT.Communications;
using Oracle.Combat;
using Oracle.ESP;
using Oracle.ItemSpawn;
using UnityEngine;

namespace Oracle.Utils
{
    /// <summary>
    /// 快捷键管理器和全局配置定义
    /// </summary>
    internal class HotKeyManager
    {
        //配置定义
        internal static ConfigEntry<KeyCode> PlayerESPKey { get; set; }
        internal static ConfigEntry<KeyCode> LootESPKey { get; set; }
        internal static ConfigEntry<KeyCode> LooseLootESPKey { get; set; }
        internal static ConfigEntry<KeyCode> ContainerLootESPKey { get; set; }
        internal static ConfigEntry<KeyCode> AimbotKey { get; set; }
        internal static ConfigEntry<KeyCode> ChangeAimTargetKey { get; set; }
        internal static ConfigEntry<KeyCode> LootESPNameModeKey { get; set; }
        internal static ConfigEntry<KeyCode> AddItemKey { get; set; }
        internal static ConfigEntry<KeyboardShortcut> CopyItemKey { get; set; }
        internal static ConfigEntry<KeyCode> ItemManagerKey { get; set; }
        internal static ConfigEntry<KeyCode> BotManagerKey { get; set; }
        internal static ConfigEntry<KeyCode> LootManagerKey { get; set; }
        internal static ConfigEntry<KeyCode> GhostModeKey { get; set; }
        internal static ConfigEntry<KeyCode> DropItemKey { get; set; }
        internal static ConfigEntry<KeyCode> UniGUIKey { get; set; }
        internal static ConfigEntry<bool> UniGUI { get; set; }
        internal static ConfigEntry<bool> MuteNotice { get; set; }
        internal static ConfigEntry<bool> FPSLimit { get; set; }
        /// <summary>
        /// 按键监听, 挂载到Update里
        /// </summary>
        public static void KeyStatusUpdate()
        {
            if (Input.GetKeyDown(PlayerESPKey.Value))
            {
                PlayerESPCfg.EnablePlayerESP.Value = !PlayerESPCfg.EnablePlayerESP.Value;
                var value = PlayerESPCfg.EnablePlayerESP.Value;
                if (!MuteNotice.Value)
                {
                    NotificationManagerClass.DisplayMessageNotification(
                        $"玩家透视已{(value ? "启用" : "禁用")}!",
                        ENotificationDurationType.Default,
                        value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                        null
                    );
                }
            }
            if (Input.GetKeyDown(LootESPKey.Value))
            {
                LootESPCfg.EnableLootESP.Value = !LootESPCfg.EnableLootESP.Value;
                var value = LootESPCfg.EnableLootESP.Value;
                if (!MuteNotice.Value)
                {
                    NotificationManagerClass.DisplayMessageNotification(
                    $"物资透视已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
                }
            }
            if (Input.GetKeyDown(LooseLootESPKey.Value))
            {
                LootESPCfg.EnableLooseLootESP.Value = !LootESPCfg.EnableLooseLootESP.Value;
                var value = LootESPCfg.EnableLooseLootESP.Value;
                if (!MuteNotice.Value)
                {
                    NotificationManagerClass.DisplayMessageNotification(
                    $"松散物资透视已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
                }
            }
            if (Input.GetKeyDown(ContainerLootESPKey.Value))
            {
                LootESPCfg.EnableContainerLootESP.Value = !LootESPCfg.EnableContainerLootESP.Value;
                var value = LootESPCfg.EnableContainerLootESP.Value;
                if (!MuteNotice.Value)
                {
                    NotificationManagerClass.DisplayMessageNotification(
                    $"容器物资透视已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
                }
            }
            if (Input.GetKeyDown(AimbotKey.Value))
            {
                AimbotCfg.EnableAimbot.Value = !AimbotCfg.EnableAimbot.Value;
                var value = AimbotCfg.EnableAimbot.Value;
                if (!MuteNotice.Value)
                {
                    NotificationManagerClass.DisplayMessageNotification(
                    $"自瞄已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
                }
            }
            if (Input.GetKeyDown(UniGUIKey.Value))
            {
                UniGUI.Value = !UniGUI.Value;
                var value = UniGUI.Value;
                if (!MuteNotice.Value)
                {
                    NotificationManagerClass.DisplayMessageNotification(
                    $"全局绘制已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
                }
            }
            if (Input.GetKeyDown(GhostModeKey.Value))
            {
                GhostModeCfg.EnableGhostMode.Value = !GhostModeCfg.EnableGhostMode.Value;
                var value = GhostModeCfg.EnableGhostMode.Value;
                if (!MuteNotice.Value)
                {
                    NotificationManagerClass.DisplayMessageNotification(
                    $"隐身已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
                }
            }
            if (Input.GetKeyDown(ChangeAimTargetKey.Value))
            {
                AimbotCfg.AimbotPartSetting.Value = AimbotCfg.AimbotPartSetting.Value == "头部" ? "胸口" : "头部";
                var value = AimbotCfg.AimbotPartSetting.Value;
                if (!MuteNotice.Value)
                {
                    NotificationManagerClass.DisplayMessageNotification(
                    $"锁定部位切换到{value}",
                    ENotificationDurationType.Default,
                    ENotificationIconType.Default,
                    null
                );
                }
            }
            if (Input.GetKeyDown(LootESPNameModeKey.Value))
            {
                LootESPCfg.ShowItemFullName.Value = !LootESPCfg.ShowItemFullName.Value;
            }
            if (Input.GetKeyDown(AddItemKey.Value))
            {
                //ItemSpawner.SpawnItemIntoInventory(PluginsCore.CorrectPlayer, ItemSpawnerCfg.TargetItemId.Value);
                ItemSpawner.AddItemToManager(ItemSpawnerCfg.TargetItemId.Value);
            }
            if (Input.GetKeyDown(DropItemKey.Value))
            {
                //ItemSpawner.SpawnItemIntoInventory(PluginsCore.CorrectPlayer, ItemSpawnerCfg.TargetItemId.Value);
                ItemSpawner.CloneAndDropItem(PluginsCore.CorrectPlayer, ItemCatcher.savedItem);
            }
        }
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public static void Initialize(ConfigFile config)
        {
            PlayerESPKey = config.Bind<KeyCode>(
                "快捷键设置",
                "玩家透视快捷键",
                KeyCode.F2,
                "按下切换玩家透视"
            );

            LootESPKey = config.Bind<KeyCode>(
                "快捷键设置",
                "全局物资透视快捷键",
                KeyCode.F3,
                "按下切换所有物资透视"
            );

            LooseLootESPKey = config.Bind<KeyCode>(
                "快捷键设置",
                "散落物资透视快捷键",
                KeyCode.F4,
                "按下切换地上的散落物资透视"
            );

            ContainerLootESPKey = config.Bind<KeyCode>(
                "快捷键设置",
                "容器物资透视快捷键",
                KeyCode.F5,
                "按下切换容器(如箱子/衣服/包)物资透视"
            );

            AimbotKey = config.Bind<KeyCode>(
                "快捷键设置",
                "自瞄快捷键",
                KeyCode.F6,
                "按下切换自瞄与魔法子弹功能"
            );
            LootESPNameModeKey = config.Bind<KeyCode>(
                "快捷键设置",
                "切换物资透视长短名字",
                KeyCode.F7,
                "切换物资透视长短名字"
            );
            UniGUIKey = config.Bind<KeyCode>(
                "快捷键设置",
                "切换全局绘制",
                KeyCode.Insert,
                "按下切换所有绘制状态"
            );
            UniGUI = config.Bind<bool>(
                "绘制设置",
                "启用绘制",
                true,
                "启用绘制"
            );
            FPSLimit = config.Bind<bool>(
                "绘制设置",
                "开启帧数限制",
                true,
                "启用后透视将以50帧为上限绘制而不是每帧绘制，关闭可能造成一定的帧数下降"
            );
            MuteNotice = config.Bind<bool>(
                "快捷键设置",
                "静默切换提示",
                true,
                "启用后切换功能开关将不会有任何提示"
            );
            AddItemKey = config.Bind<KeyCode>(
                "快捷键设置",
                "创建实例",
                KeyCode.KeypadDivide,
                "按下后将指定ID的物品作为实例添加到实例管理器"
            );
            DropItemKey = config.Bind<KeyCode>(
                "快捷键设置",
                "复制物品",
                KeyCode.KeypadDivide,
                "按下后生成并掉落当前选择的物品"
            );
            CopyItemKey = config.Bind(
                "快捷键设置",
                "保存物品",
                new KeyboardShortcut(KeyCode.C, KeyCode.LeftShift),
                new ConfigDescription("将鼠标指向物品并按下此快捷键将物品组保存到内存")
            );
            ChangeAimTargetKey = config.Bind<KeyCode>(
                "快捷键设置",
                "切换瞄准部位",
                KeyCode.KeypadMultiply,
                "按下切换瞄准的部位(头或胸)"
            );
            ItemManagerKey = config.Bind(
                "快捷键设置",
                "打开物品管理器",
                KeyCode.F10,
                "打开物品实例管理器"
            );
            BotManagerKey = config.Bind(
                "快捷键设置",
                "打开AI管理器",
                KeyCode.F9,
                "打开战局AI管理器"
            );
            LootManagerKey = config.Bind(
                "快捷键设置",
                "打开战利品管理器",
                KeyCode.F8,
                "打开战局战利品管理器"
            );
            GhostModeKey = config.Bind(
                "快捷键设置",
                "隐身快捷键",
                KeyCode.F11,
                "按下切换隐身模式, AI不会对你产生仇恨"
            );
        }
    }
}
