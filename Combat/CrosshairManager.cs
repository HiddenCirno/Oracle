using BepInEx;
using BepInEx.Configuration;
using Oracle.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{
    public class CrosshairManager: IOracleCrosshair
    {

        private static Texture2D _cachedCrosshairTex;
        private static string _lastLoadedCrosshairName = "";
        public const string FallbackImageName = "无图片";

        // 准星图片存放目录
        public static string CrosshairDirectory => Path.Combine(PluginsCore.pluginDir, "crosshairs");

        public void SubscribeEvent()
        {
            OracleEvent.OnDrawCrosshair += DrawCrosshair;
        }

        /// <summary>
        /// 从本地流读取图片并生成 Texture2D (严禁在 OnGUI 中调用)
        /// </summary>
        public static void LoadCrosshairTexture()
        {
            string fileName = CrosshairManagerCfg.SelectedCrosshair.Value;
            if (string.IsNullOrEmpty(fileName) || fileName == FallbackImageName) return;

            string fullPath = Path.Combine(CrosshairDirectory, fileName);
            if (!File.Exists(fullPath)) return;

            // 防止重复加载消耗性能
            if (_lastLoadedCrosshairName == fileName && _cachedCrosshairTex != null) return;

            try
            {
                // ⭐ 核心防泄漏：Unity不会自动回收通过 LoadImage 创建的 Texture2D，必须手动销毁旧的！
                if (_cachedCrosshairTex != null)
                {
                    UnityEngine.Object.Destroy(_cachedCrosshairTex);
                    _cachedCrosshairTex = null;
                }

                byte[] fileData = File.ReadAllBytes(fullPath);

                // 尺寸会被 LoadImage 自动覆盖为真实尺寸
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                // 解决某些图片边缘出现白边的问题
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;

                tex.LoadImage(fileData);

                _cachedCrosshairTex = tex;
                _lastLoadedCrosshairName = fileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Oracle Crosshair] 图片加载失败 ({fileName}): {ex.Message}");
            }
        }

        /// <summary>
        /// 在全局 OnGUI 中调用此方法进行绘制
        /// </summary>
        public static void DrawCrosshair()
        {
            if (!CrosshairManagerCfg.EnableCrosshair.Value || _cachedCrosshairTex == null) return;

            // 获取图片真实的像素宽高
            float texWidth = _cachedCrosshairTex.width;
            float texHeight = _cachedCrosshairTex.height;

            // ⭐ 绝对居中算法：屏幕中心点 - 图片尺寸的一半
            float x = (Screen.width - texWidth) / 2f;
            float y = (Screen.height - texHeight) / 2f;

            // 绘制
            GUI.DrawTexture(new Rect(x, y, texWidth, texHeight), _cachedCrosshairTex);
        }
    }
    public class CrosshairManagerCfg : IOracleCfg
    {
        public static ConfigEntry<bool> EnableCrosshair;
        public static ConfigEntry<string> SelectedCrosshair;
        /// <summary>
        /// 初始化配置与文件扫描
        /// </summary>
        public void Initialize(ConfigFile config)
        {
            if (!Directory.Exists(CrosshairManager.CrosshairDirectory))
            {
                Directory.CreateDirectory(CrosshairManager.CrosshairDirectory);
            }

            // 1. 扫描目录下所有 PNG 图片
            List<string> availableImages = Directory.GetFiles(CrosshairManager.CrosshairDirectory, "*.png")
                                                    .Select(Path.GetFileName)
                                                    .ToList();

            if (availableImages.Count == 0)
            {
                availableImages.Add(CrosshairManager.FallbackImageName);
            }

            // 2. 绑定总开关
            EnableCrosshair = config.Bind(
                "屏幕准星 / Overlay",
                "启用自定义准星/覆盖层",
                true,
                "在屏幕中心绘制自定义PNG图片"
            );

            // 3. 绑定下拉菜单
            SelectedCrosshair = config.Bind(
                "屏幕准星 / Overlay",
                "选择图片样式",
                availableImages[0],
                new ConfigDescription(
                    $"选择准星图片 (请将png文件放入 {CrosshairManager.CrosshairDirectory} 目录下)",
                    new AcceptableValueList<string>(availableImages.ToArray())
                )
            );

            // 4. 监听配置变更，玩家在 F12 菜单切换图片时动态重载
            SelectedCrosshair.SettingChanged += (sender, args) => CrosshairManager.LoadCrosshairTexture();

            // 首次初始化加载
            CrosshairManager.LoadCrosshairTexture();
        }

    }
}