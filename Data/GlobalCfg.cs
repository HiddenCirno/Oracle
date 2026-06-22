using BepInEx.Configuration;
using EFT.Communications;
using Oracle.Combat;
using Oracle.ESP;
using Oracle.ItemSpawn;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Data
{
    /// <summary>
    /// 快捷键管理器和全局配置定义
    /// </summary>
    internal class GlobalCfg : IOracleCfg, IOracleKeyUpdate
    {
        //配置定义
        internal static ConfigEntry<KeyCode> UniGUIKey { get; set; }
        internal static ConfigEntry<bool> UniGUI { get; set; }
        internal static ConfigEntry<bool> MuteNotice { get; set; }
        internal static ConfigEntry<bool> FPSLimit { get; set; }
        public void RegisterKeyUpdate()
        {
            OracleEvent.OnUpdate += KeyUpdate;
        }
        /// <summary>
        /// 按键监听, 挂载到Update里
        /// </summary>
        public static void KeyUpdate()
        {
            if (Input.GetKeyDown(UniGUIKey.Value))
            {
                UniGUI.Value = !UniGUI.Value;
                var value = UniGUI.Value;
                OracleNotify.Message($"全局绘制已{(value ? "启用" : "禁用")}!", value ? ENotificationIconType.Default : ENotificationIconType.Alert, MuteNotice.Value);
            }
            
        }
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            UniGUIKey = config.Bind(
                "绘制设置",
                "切换全局绘制",
                KeyCode.Insert,
                "按下切换所有绘制状态"
            );
            UniGUI = config.Bind(
                "绘制设置",
                "启用绘制",
                true,
                "启用绘制"
            );
            FPSLimit = config.Bind(
                "绘制设置",
                "开启帧数限制",
                true,
                "启用后透视将以50帧为上限绘制而不是每帧绘制，关闭可能造成一定的帧数下降"
            );
            MuteNotice = config.Bind(
                "绘制设置",
                "静默提示",
                false,
                "启用后切换功能开关将不会有任何提示"
            );
        }
    }
}
