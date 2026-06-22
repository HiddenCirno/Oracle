using BepInEx.Configuration;
using CommonAssets.Scripts.Game.LabyrinthEvent;
using EFT;
using EFT.Communications;
using EFT.SynchronizableObjects;
using Oracle.Data;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{
    /// <summary>
    /// 绊雷透视部分
    /// </summary>
    public class TripwireESP : IOracleESP
    {
        public static readonly Color ColorDangerous = Color.red; //你看得到它并且它看得到你
        public void SubscribeEvent()
        {
            // 订阅统一的渲染频道
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

            // ================= 步骤 1：使用 GL 绘制绊线 =================
            if (Event.current.type == EventType.Repaint)
            {
                lineMaterial.SetPass(0);
                GL.PushMatrix();
                GL.LoadPixelMatrix();
                GL.Begin(GL.LINES);
                GL.Color(ColorDangerous); // 使用红色画线

                foreach (TripwireData trap in OracleTripwireManager.CachedTripwires)
                {
                    // 距离过滤 (用中点计算距离)
                    if (!OracleCommon.IsInRange(maxDistance, playerPos, trap.CenterPos)) continue;

                    // 转屏幕坐标
                    Vector3 screenPointA = cam.WorldToScreenPoint(trap.StartPos);
                    Vector3 screenPointB = cam.WorldToScreenPoint(trap.EndPos);

                    // 深度检查：确保线段的两端都在屏幕前方
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

            // ================= 步骤 2：使用 GUI 绘制文字标签 =================
            textStyle.richText = true;
            foreach (TripwireData trap in OracleTripwireManager.CachedTripwires)
            {
                if (!OracleCommon.IsInRange(maxDistance, playerPos, trap.CenterPos)) continue;

                Vector3 screenCenter = cam.WorldToScreenPoint(trap.CenterPos);

                if (screenCenter.z > 0.01f)
                {
                    int dist = Mathf.RoundToInt(Vector3.Distance(playerPos, trap.CenterPos));
                    string text = $"<color=#FF0000>绊雷</color> <color=#FFFF00>{dist}米</color>";

                    float screenX = screenCenter.x;
                    float screenY = Screen.height - screenCenter.y;

                    // 在中点上方偏移画字，完美居中
                    GUI.Label(new Rect(screenX - 50, screenY - 20, 100, 40), text, textStyle);
                }
            }
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
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
                "陷阱透视",
                "启用绊雷透视",
                true,
                "在屏幕上绘制出绊雷的触发实体线及距离"
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
