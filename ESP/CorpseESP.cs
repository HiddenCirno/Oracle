using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using EFT.Interactive;
using Oracle.Data;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{

    /// <summary>
    /// 独立的尸体透视部分
    /// </summary>
    public class CorpseESP : IOracleESP
    {

        public void SubscribeEvent()
        {
            OracleEvent.OnDrawESP += OnDrawESP;
        }

        // ⭐ 2. 独立的绘制入口
        private void OnDrawESP()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            DrawCorpseText(cam, OracleRendering.EspTextStyle);
        }

        /// <summary>
        /// 独立的尸体文本绘制方法（无约束范围，全局绘制）
        /// </summary>
        public static void DrawCorpseText(Camera cam, GUIStyle textStyle)
        {
            // 总开关
            if (!CorpseESPCfg.EnableCorpseESP.Value) return;
            if (OracleCorpseManager.CachedCorpseList == null || OracleCorpseManager.CachedCorpseList.Count == 0) return;

            // 样式状态保护
            textStyle.richText = true;
            textStyle.normal.textColor = Color.white;

            foreach (CorpseData corpse in OracleCorpseManager.CachedCorpseList)
            {
                // 世界坐标转屏幕坐标
                Vector3 screenPos = cam.WorldToScreenPoint(corpse.Position);

                // 确保在相机前方
                if (screenPos.z > 0.01f)
                {
                    float screenX = screenPos.x;
                    // 统一转换 Unity 坐标系并给予微小的固定的 Y 轴偏移（避免完全贴地被地面模型盖住字）
                    float screenY = Screen.height - screenPos.y - 10f;

                    // 每个人只有一行名字，无需进行堆叠偏移算法
                    GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), corpse.FormattedText, textStyle);
                }
            }
        }
    }

    /// <summary>
    /// 尸体透视配置项
    /// </summary>
    public class CorpseESPCfg : IOracleCfg, IOracleKeyUpdate
    {
        internal static ConfigEntry<KeyCode> CorpseESPKey { get; set; }
        internal static ConfigEntry<bool> EnableCorpseESP { get; set; }
        internal static ConfigEntry<int> CorpseESPMaxDistance { get; set; }

        public void Initialize(ConfigFile config)
        {
            CorpseESPKey = config.Bind<KeyCode>(
                "尸体透视",
                "尸体透视快捷键",
                KeyCode.F5,
                "切换尸体透视"
            );
            EnableCorpseESP = config.Bind<bool>(
                "尸体透视",
                "启用尸体透视",
                true,
                "是否在屏幕上显示死去的玩家/AI"
            );

            CorpseESPMaxDistance = config.Bind<int>(
                "尸体透视",
                "尸体透视最大距离",
                300,
                new ConfigDescription(
                    "透视死者的最远范围",
                    new AcceptableValueRange<int>(50, 1000)
                )
            );
        }
        public void RegisterKeyUpdate()
        {
            OracleEvent.OnUpdate += KeyUpdate;
        }
        public static void KeyUpdate()
        {
            if (Input.GetKeyDown(CorpseESPKey.Value))
            {
                EnableCorpseESP.Value = !EnableCorpseESP.Value;
                var value = EnableCorpseESP.Value;
                OracleNotify.Message($"尸体透视已{(value ? "启用" : "禁用")}!", value ? ENotificationIconType.Default : ENotificationIconType.Alert, GlobalCfg.MuteNotice.Value);
            }
        }
    }
}