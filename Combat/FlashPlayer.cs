using BepInEx.Configuration;
using Oracle.Data;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    /// <summary>
    /// 闪现
    /// </summary>
    public static class FlashPlayer
    {
        /// <summary>
        /// 向前传送
        /// </summary>
        public static void TeleportPlayer()
        {
            var mainPlayer = PluginsCore.CorrectPlayer;
            if (mainPlayer == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 forwardDir = cam.transform.forward;

            Vector3 targetPos = mainPlayer.Position + forwardDir * FlashPlayerCfg.FlashDistance.Value;

            //执行
            mainPlayer.Teleport(targetPos, true);
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
    [OracleCfgOrder(1)]
    public class FlashPlayerCfg : IOracleCfg, IOracleKeyUpdate
    {
        internal static ConfigEntry<KeyCode> FlashKey { get; set; }
        internal static ConfigEntry<float> FlashDistance { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            FlashKey = config.Bind(
                "1. 天堂支点 / Combat Module",
                "闪现快捷键",
                KeyCode.Z,
                new ConfigDescription(
                    "cfg_combat_module_flash_key_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_combat_module_flash_key_name".i18n(),
                        IsAdvanced = false,
                        Order = 270
                    }
                )
            );
            FlashDistance = config.Bind(
                "1. 天堂支点 / Combat Module", 
                "闪现距离", 
                3f,
                new ConfigDescription(
                    "cfg_combat_module_flash_distance_desc".i18n(),
                    new AcceptableValueRange<float>(0f, 1000f),
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_combat_module_flash_distance_name".i18n(),
                        IsAdvanced = false,
                        Order = 269
                    }
                )
            );
        }

        public void RegisterKeyUpdate()
        {
            OracleEvent.OnUpdate += KeyUpdate;
        }

        public static void KeyUpdate()
        {
            if (Input.GetKeyDown(FlashKey.Value))
            {
                FlashPlayer.TeleportPlayer();
            }
        }
    }
}