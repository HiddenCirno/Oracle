using BepInEx.Configuration;
using EFT.Communications;
using Oracle.Data;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{

    /// <summary>
    /// 尸体透视
    /// </summary>
    public class CorpseESP : IOracleESP
    {

        public void SubscribeEvent()
        {
            OracleEvent.OnDrawESP += OnDrawESP;
        }

        /// <summary>
        /// 绘制方法
        /// </summary>
        private void OnDrawESP()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            DrawCorpseText(cam, OracleRendering.EspTextStyle);
        }

        /// <summary>
        /// 绘制文本
        /// </summary>
        public static void DrawCorpseText(Camera cam, GUIStyle textStyle)
        {
            //开关&防空
            if (!CorpseESPCfg.EnableCorpseESP.Value) return;
            if (OracleCorpseDataManager.CachedCorpseList == null || OracleCorpseDataManager.CachedCorpseList.Count == 0) return;

            //样式保护
            textStyle.richText = true;
            textStyle.normal.textColor = Color.white;

            foreach (CorpseData corpse in OracleCorpseDataManager.CachedCorpseList)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(corpse.Position);

                //深度检查
                if (screenPos.z > 0.01f)
                {
                    float screenX = screenPos.x;
                    float screenY = Screen.height - screenPos.y - 10f;

                    //绘制
                    GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), corpse.FormattedText, textStyle);
                }
            }
        }
    }

    /// <summary>
    /// 配置项
    /// </summary>
    /// 
    [OracleCfgOrder(3)]
    public class CorpseESPCfg : IOracleCfg, IOracleKeyUpdate
    {
        internal static ConfigEntry<KeyCode> CorpseESPKey { get; set; }
        internal static ConfigEntry<bool> EnableCorpseESP { get; set; }
        internal static ConfigEntry<int> CorpseESPMaxDistance { get; set; }

        public void Initialize(ConfigFile config)
        {
            EnableCorpseESP = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "启用尸体透视",
                true,
                new ConfigDescription(
                    "cfg_esp_module_corpse_esp_enable_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_esp_module_corpse_esp_enable_name".i18n(),
                        IsAdvanced = false,
                        Order = 150
                    }
                )
            );
            CorpseESPKey = config.Bind<KeyCode>(
                "3. 巡天星轨 / ESP Module",
                "尸体透视快捷键",
                KeyCode.F5,
                new ConfigDescription(
                    "cfg_esp_module_corpse_esp_enable_key_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_esp_module_corpse_esp_enable_key_name".i18n(),
                        IsAdvanced = false,
                        Order = 149
                    }
                )
            );
            CorpseESPMaxDistance = config.Bind<int>(
                "3. 巡天星轨 / ESP Module",
                "尸体透视最大距离",
                300,
                new ConfigDescription(
                    "cfg_esp_module_corpse_esp_max_distance_desc".i18n(),
                    new AcceptableValueRange<int>(50, 1000),
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_esp_module_corpse_esp_max_distance_name".i18n(),
                        IsAdvanced = false,
                        Order = 148
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
            if (Input.GetKeyDown(CorpseESPKey.Value))
            {
                EnableCorpseESP.Value = !EnableCorpseESP.Value;
                var value = EnableCorpseESP.Value;
                OracleNotify.Message(
                    string.Format(
                        "message_esp_corpse_enable".i18n(),
                        value ? "text_enable".i18n() : "text_disable".i18n()
                    ),
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    GlobalCfg.MuteNotice.Value
                );
            }
        }
    }
}