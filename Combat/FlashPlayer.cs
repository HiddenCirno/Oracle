using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using Oracle.ItemSpawn;
using Oracle.Tools;
using System;
using System.Reflection;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    public static class FlashPlayer
    {
        // 使用纯原生 Harmony 注解，直接绑定目标方法
        public static void TeleportPlayer()
        {
            var mainPlayer = PluginsCore.CorrectPlayer;
            if (mainPlayer == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 forwardDir = cam.transform.forward;

            Vector3 targetPos = mainPlayer.Position + forwardDir * FlashPlayerCfg.FlashDistance.Value;

            // 3. 执行传送
            mainPlayer.Teleport(targetPos, true);
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
    public class FlashPlayerCfg : IOracleCfg
    {

        internal static ConfigEntry<float> FlashDistance { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            FlashDistance = config.Bind(
                "闪现设置", "闪现距离", 3f,
                new ConfigDescription("闪现的距离", new AcceptableValueRange<float>(0f, 1000f))
            );
        }
    }
}