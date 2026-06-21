using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Communications;
using HarmonyLib;
using Oracle.Data;
using Oracle.Tools;
using Oracle.Utils;
using System;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    public static class GhostMode
    {
        // =======================================================
        // 拦截：阻断群组仇恨共享 (防止 Scav 互通有无、对讲机报点)
        // =======================================================
        [HarmonyPatch(typeof(BotsGroup), nameof(BotsGroup.AddEnemy))]
        public class BotGroupAddEnemyPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(IPlayer person)
            {
                // 如果开启了隐身，且 AI 试图添加的敌人是玩家本人 -> 直接掐断执行
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
    public class GhostModeCfg : IOracleCfg, IOracleKeyUpdate
    {
        internal static ConfigEntry<KeyCode> GhostModeKey { get; set; }
        internal static ConfigEntry<bool> EnableGhostMode { get; set; }

        public void Initialize(ConfigFile config)
        {
            GhostModeKey = config.Bind(
                "战斗修改",
                "隐身快捷键",
                KeyCode.F11,
                "按下切换隐身模式, AI不会对你产生仇恨"
            );
            EnableGhostMode = config.Bind(
                "战斗修改",
                "隐身模式",
                false,
                "启用后 AI (包括 SAIN) 将完全无视玩家，不会主动将其作为攻击目标"
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
                OracleNotify.Message($"隐身已{(value ? "启用" : "禁用")}!", value ? ENotificationIconType.Default : ENotificationIconType.Alert, GlobalCfg.MuteNotice.Value);
            }
        }
        private static void OnGhostModeChanged(object sender, EventArgs e)
        {
            // 1. 安全检查：战局或玩家没加载完时绝对不执行
            if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null) return;

            Player mainPlayer = PluginsCore.CorrectPlayer;

            // 获取战局内所有存活的玩家（包括真实玩家和 AI）
            var allPlayers = PluginsCore.CorrectGameWorld.AllAlivePlayersList;
            if (allPlayers == null || allPlayers.Count == 0) return;

            // 2. 根据最新的开关状态，遍历所有的实体
            if (EnableGhostMode.Value)
            {
                // 开启隐身：强制失忆
                foreach (Player player in allPlayers)
                {
                    // 过滤：只要活着的 AI，不要动真实玩家自己
                    if (player == null || !player.IsAI || player.AIData?.BotOwner == null) continue;

                    BotOwner bot = player.AIData.BotOwner;

                    // 从单兵记忆和小队仇恨列表中无情抹除
                    bot.Memory?.DeleteInfoAboutEnemy(mainPlayer);
                    bot.BotsGroup?.RemoveEnemy(mainPlayer, EBotEnemyCause.initial); // 传入一个默认的移除原因
                }

                //NotificationManagerClass.DisplayMessageNotification("隐身模式已启用：AI 已丢失你的目标。");
            }
            else
            {
                // 关闭隐身：恢复仇恨
                foreach (Player player in allPlayers)
                {
                    if (player == null || !player.IsAI || player.AIData?.BotOwner == null) continue;

                    BotOwner bot = player.AIData.BotOwner;
                    if (bot.BotsGroup == null) continue;

                    // 强行触发小队的敌人检查，让 AI 重新扫描你
                    bot.BotsGroup.CheckAndAddEnemy(mainPlayer, ignoreAI: true);
                }

                //NotificationManagerClass.DisplayMessageNotification("隐身模式已关闭：AI 重新锁定了你！");
            }
        }
    }
}