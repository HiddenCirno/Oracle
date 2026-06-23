using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using HarmonyLib;
using Oracle.Data;
using Oracle.Utils;
using System;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    /// <summary>
    /// 隐身
    /// </summary>
    public static class GhostMode
    {
        //隐身Patch
        [HarmonyPatch(typeof(BotsGroup), nameof(BotsGroup.AddEnemy))]
        public class BotGroupAddEnemyPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(IPlayer person)
            {
                if (GhostModeCfg.EnableGhostMode.Value && person != null && person.IsYourPlayer)
                {
                    return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    [OracleCfgOrder(1)]
    public class GhostModeCfg : IOracleCfg, IOracleKeyUpdate
    {
        internal static ConfigEntry<KeyCode> GhostModeKey { get; set; }
        internal static ConfigEntry<bool> EnableGhostMode { get; set; }

        public void Initialize(ConfigFile config)
        {
            EnableGhostMode = config.Bind(
                "1. 天堂支点 / Combat Module",
                "隐身模式",
                false,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_ghost_mode_enable_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_ghost_mode_enable_name"),
                        IsAdvanced = false,
                        Order = 280
                    }
                )
            );
            GhostModeKey = config.Bind(
                "1. 天堂支点 / Combat Module",
                "隐身快捷键",
                KeyCode.F11,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_ghost_mode_enable_key_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_ghost_mode_enable_key_name"),
                        IsAdvanced = false,
                        Order = 279
                    }
                )
            );

            // ⭐ 核心：订阅配置值改变事件
            EnableGhostMode.SettingChanged += OnGhostModeChanged;
        }
        public void RegisterKeyUpdate()
        {
            OracleEvent.OnUpdate += KeyUpdate;
        }
        public static void KeyUpdate()
        {

            if (Input.GetKeyDown(GhostModeKey.Value))
            {
                EnableGhostMode.Value = !EnableGhostMode.Value;
                var value = EnableGhostMode.Value;
                OracleNotify.Message(
                    string.Format(
                        LocaleManager.Get("message_ghost_mode_enable"),
                        value ? LocaleManager.Get("text_enable") : LocaleManager.Get("text_disable")
                    ),
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    GlobalCfg.MuteNotice.Value
                );
            }
        }

        /// <summary>
        /// 切换隐身
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnGhostModeChanged(object sender, EventArgs e)
        {
            if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null) return;

            Player mainPlayer = PluginsCore.CorrectPlayer;

            //遍历玩家表
            var allPlayers = PluginsCore.CorrectGameWorld.AllAlivePlayersList;
            if (allPlayers == null || allPlayers.Count == 0) return;

            //开启隐身
            if (EnableGhostMode.Value)
            {
                foreach (Player player in allPlayers)
                {
                    //过滤掉自己
                    if (player == null || !player.IsAI || player.AIData?.BotOwner == null) continue;

                    BotOwner bot = player.AIData.BotOwner;

                    //清除仇恨
                    bot.Memory?.DeleteInfoAboutEnemy(mainPlayer);
                    bot.BotsGroup?.RemoveEnemy(mainPlayer, EBotEnemyCause.Unknown);
                }
            }
            else
            {
                //关闭隐身
                foreach (Player player in allPlayers)
                {
                    if (player == null || !player.IsAI || player.AIData?.BotOwner == null) continue;

                    BotOwner bot = player.AIData.BotOwner;
                    if (bot.BotsGroup == null) continue;

                    //立刻进行一次索敌
                    bot.BotsGroup.CheckAndAddEnemy(mainPlayer, ignoreAI: true);
                }
            }
        }
    }
}