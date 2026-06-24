using BepInEx.Configuration;
using Oracle.Data;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{
    /// <summary>
    /// 绊雷透视
    /// </summary>
    public class TripwireESP : IOracleESP
    {
        public void SubscribeEvent()
        {
            OracleEvent.OnDrawESP += OnDrawESP;
        }
        private void OnDrawESP()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            DrawTripwireESP(cam, OracleRendering.EspTextStyle, OracleRendering.EspMaterial);
        }

        /// <summary>
        /// 绘制绊雷 2D 实体线和距离信息
        /// </summary>
        public static void DrawTripwireESP(Camera cam, GUIStyle textStyle, Material lineMaterial)
        {
            if (!TripwireESPCfg.EnableTripwireESP.Value || OracleTripwireManager.CachedTripwires == null || OracleTripwireManager.CachedTripwires.Count == 0) return;

            Vector3 playerPos = PluginsCore.CorrectPlayer.Transform.position;
            int maxDistance = 25;

            if (Event.current.type == EventType.Repaint)
            {
                //画线
                lineMaterial.SetPass(0);
                GL.PushMatrix();
                GL.LoadPixelMatrix();
                GL.Begin(GL.LINES);
                GL.Color(OracleColorManager.Tripwire);

                foreach (TripwireData trap in OracleTripwireManager.CachedTripwires)
                {
                    //距离过滤
                    if (!OracleCommon.IsInRange(maxDistance, playerPos, trap.CenterPos)) continue;

                    //三转二
                    Vector3 screenPointA = cam.WorldToScreenPoint(trap.StartPos);
                    Vector3 screenPointB = cam.WorldToScreenPoint(trap.EndPos);

                    //深度检查
                    if (screenPointA.z > 0.01f && screenPointB.z > 0.01f)
                    {
                        // 绘制直线
                        GL.Vertex3(screenPointA.x, screenPointA.y, 0);
                        GL.Vertex3(screenPointB.x, screenPointB.y, 0);
                    }
                }
                GL.End();
                GL.PopMatrix();
            }
            //画字
            textStyle.richText = true;
            foreach (TripwireData trap in OracleTripwireManager.CachedTripwires)
            {
                if (!OracleCommon.IsInRange(maxDistance, playerPos, trap.CenterPos)) continue;

                Vector3 screenCenter = cam.WorldToScreenPoint(trap.CenterPos);

                if (screenCenter.z > 0.01f)
                {
                    int dist = Mathf.RoundToInt(Vector3.Distance(playerPos, trap.CenterPos));
                    
                    string text = string.Format("text_esp_tripwire".i18n(), OracleColorManager.Tripwire, OracleColorManager.Distance, dist);

                    float screenX = screenCenter.x;
                    float screenY = Screen.height - screenCenter.y;

                    //居中画字
                    GUI.Label(new Rect(screenX - 50, screenY - 20, 100, 40), text, textStyle);
                }
            }
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    [OracleCfgOrder(3)]
    public class TripwireESPCfg : IOracleCfg, IOracleKeyUpdate
    {
        internal static ConfigEntry<bool> EnableTripwireESP { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            EnableTripwireESP = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "启用绊雷透视",
                true,
                new ConfigDescription(
                    "cfg_esp_module_tripwire_esp_enable_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_esp_module_tripwire_esp_enable_name".i18n(),
                        IsAdvanced = false,
                        Order = 140
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
        }
    }
}
