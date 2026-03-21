using BepInEx.Configuration;
using EFT.Communications;
using Oracle.ESP;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static GClass2175;

namespace Oracle.Utils
{
    internal class HotKeyManager
    {
        internal static ConfigEntry<KeyCode> PlayerESPKey { get; set; }
        internal static ConfigEntry<KeyCode> LootESPKey { get; set; }
        internal static ConfigEntry<KeyCode> LooseLootESPKey { get; set; }
        internal static ConfigEntry<KeyCode> ContainerLootESPKey { get; set; }
        internal static ConfigEntry<KeyCode> AimbotKey { get; set; }
        internal static ConfigEntry<KeyCode> LootESPNameModeKey { get; set; }
        internal static ConfigEntry<KeyCode>SpawnItemKey { get; set; }
        internal static ConfigEntry<string> TargetItemId { get; set; }
        internal static ConfigEntry<KeyCode> UniGUIKey { get; set; }
        internal static ConfigEntry<bool> UniGUI { get; set; }
        public static void KeyStatusUpdate()
        {
            if (Input.GetKeyDown(PlayerESPKey.Value))
            {
                PlayerESPCfg.EnablePlayerESP.Value = !PlayerESPCfg.EnablePlayerESP.Value;
                var value = PlayerESPCfg.EnablePlayerESP.Value;
                NotificationManagerClass.DisplayMessageNotification(
                    $"玩家透视已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
            }
            if (Input.GetKeyDown(LootESPKey.Value))
            {
                LootESPCfg.EnableLootESP.Value = !LootESPCfg.EnableLootESP.Value;
                var value = LootESPCfg.EnableLootESP.Value;
                NotificationManagerClass.DisplayMessageNotification(
                    $"物资透视已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
            }
            if (Input.GetKeyDown(LooseLootESPKey.Value))
            {
                LootESPCfg.EnableLooseLootESP.Value = !LootESPCfg.EnableLooseLootESP.Value;
                var value = LootESPCfg.EnableLooseLootESP.Value;
                NotificationManagerClass.DisplayMessageNotification(
                    $"松散物资透视已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
            }
            if (Input.GetKeyDown(ContainerLootESPKey.Value))
            {
                LootESPCfg.EnableContainerLootESP.Value = !LootESPCfg.EnableContainerLootESP.Value;
                var value = LootESPCfg.EnableContainerLootESP.Value;
                NotificationManagerClass.DisplayMessageNotification(
                    $"容器物资透视已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
            }
            if (Input.GetKeyDown(AimbotKey.Value))
            {
                AimbotCfg.EnableAimbot.Value = !AimbotCfg.EnableAimbot.Value;
                var value = AimbotCfg.EnableAimbot.Value;
                NotificationManagerClass.DisplayMessageNotification(
                    $"自瞄已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
            }
            if (Input.GetKeyDown(UniGUIKey.Value))
            {
                UniGUI.Value = !UniGUI.Value;
                var value = UniGUI.Value;
                NotificationManagerClass.DisplayMessageNotification(
                    $"全局绘制已{(value ? "启用" : "禁用")}!",
                    ENotificationDurationType.Default,
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    null
                );
            }
            if (Input.GetKeyDown(LootESPNameModeKey.Value))
            {
                LootESPCfg.ShowItemFullName.Value = !LootESPCfg.ShowItemFullName.Value;
            }
            if (Input.GetKeyDown(SpawnItemKey.Value))
            {
                ItemSpawner.SpawnItemIntoInventory(PluginsCore.CorrectPlayer, TargetItemId.Value);
                //LootESPCfg.EnableContainerLootESP.Value = !LootESPCfg.EnableContainerLootESP.Value;
                //var value = LootESPCfg.EnableContainerLootESP.Value;
                //NotificationManagerClass.DisplayMessageNotification(
                //    $"容器物资透视已{(value ? "启用" : "禁用")}!",
                //    ENotificationDurationType.Default,
                //    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                //    null
                //);
            }
        }
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
            SpawnItemKey = config.Bind<KeyCode>(
                "快捷键设置",
                "生成物品",
                KeyCode.KeypadDivide,
                "按下后生成物品"
            );
            TargetItemId = config.Bind(
                "虚空造物 (Spawner)",                // 配置文件中的分类/区块名称
                "物品 Template ID",                  // 配置项的名称
                "59faff1d86f7746c51718c9c",          // 默认值 (比如这里填个比特币的ID)
                "请输入你想生成的物品的24位16进制ID" // 在F12菜单中显示的悬浮提示
            );
        }
    }
}
