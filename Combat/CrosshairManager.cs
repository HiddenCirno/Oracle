using BepInEx.Configuration;
using Oracle.Data;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{
    /// <summary>
    /// 屏幕准星
    /// </summary>
    public class CrosshairManager : IOracleCrosshair
    {
        /// <summary>
        /// 当前使用的准星图片缓存
        /// </summary>
        private static Texture2D _cachedCrosshairTex;

        /// <summary>
        /// 上一次加载的准星名字
        /// </summary>
        private static string _lastLoadedCrosshairName = "";

        /// <summary>
        /// 默认Fallback
        /// </summary>
        public const string FallbackImageName = "No Image";

        /// <summary>
        /// 准星目录
        /// </summary>
        public static string CrosshairDirectory => Path.Combine(PluginsCore.pluginDir, "crosshairs");

        public void SubscribeEvent()
        {
            OracleEvent.OnDrawCrosshair += DrawCrosshair;
        }

        /// <summary>
        /// 从本地流读取图片并生成Texture2D
        /// </summary>
        public static void LoadCrosshairTexture()
        {
            string fileName = CrosshairManagerCfg.SelectedCrosshair.Value;
            if (string.IsNullOrEmpty(fileName) || fileName == FallbackImageName) return;

            string fullPath = Path.Combine(CrosshairDirectory, fileName);
            if (!File.Exists(fullPath)) return;

            //防止重复加载
            if (_lastLoadedCrosshairName == fileName && _cachedCrosshairTex != null) return;

            try
            {
                //手动销毁旧内存
                if (_cachedCrosshairTex != null)
                {
                    UnityEngine.Object.Destroy(_cachedCrosshairTex);
                    _cachedCrosshairTex = null;
                }

                byte[] fileData = File.ReadAllBytes(fullPath);

                //覆盖大小和样式
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
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
        /// 绘制准星
        /// </summary>
        public static void DrawCrosshair()
        {
            if (PluginsCore.CorrectPlayer == null || !CrosshairManagerCfg.EnableCrosshair.Value || _cachedCrosshairTex == null) return;
            var pwa = PluginsCore.CorrectPlayer.ProceduralWeaponAnimation;
            bool isAiming = (pwa != null && pwa.IsAiming);

            if (isAiming) return;
            float texWidth = _cachedCrosshairTex.width;
            float texHeight = _cachedCrosshairTex.height;

            //居中
            float x = (Screen.width - texWidth) / 2f;
            float y = (Screen.height - texHeight) / 2f;

            //绘制
            GUI.DrawTexture(new Rect(x, y, texWidth, texHeight), _cachedCrosshairTex);
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    public class CrosshairManagerCfg : IOracleCfg
    {
        public static ConfigEntry<bool> EnableCrosshair;
        public static ConfigEntry<string> SelectedCrosshair;

        public void Initialize(ConfigFile config)
        {
            if (!Directory.Exists(CrosshairManager.CrosshairDirectory))
            {
                Directory.CreateDirectory(CrosshairManager.CrosshairDirectory);
            }

            //扫描目录
            List<string> availableImages = Directory.GetFiles(CrosshairManager.CrosshairDirectory, "*.png")
                                                    .Select(Path.GetFileName)
                                                    .ToList();

            if (availableImages.Count == 0)
            {
                availableImages.Add(CrosshairManager.FallbackImageName);
            }

            EnableCrosshair = config.Bind(
                "0. 联觉信标 / Draw Module",
                "启用自定义准星",
                true,
                new ConfigDescription(
                    "cfg_global_module_screen_crosshair_enable_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_global_module_screen_crosshair_enable_name".i18n(),
                        IsAdvanced = false,
                        Order = 395
                    }
                )
            );
            SelectedCrosshair = config.Bind(
                "0. 联觉信标 / Draw Module",
                "选择准星样式",
                availableImages[0],
                new ConfigDescription(
                    "cfg_global_module_choose_screen_crosshair_desc".i18n(),
                    new AcceptableValueList<string>(availableImages.ToArray()),
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_global_module_choose_screen_crosshair_name".i18n(),
                        IsAdvanced = false,
                        Order = 394
                    }
                )
            );

            //监听变更事件并重载准星
            SelectedCrosshair.SettingChanged += (sender, args) => CrosshairManager.LoadCrosshairTexture();

            //初始化加载准星
            CrosshairManager.LoadCrosshairTexture();
        }

    }
}