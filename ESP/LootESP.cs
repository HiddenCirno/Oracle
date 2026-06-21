using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using EFT.Hideout;
using EFT.Interactive;
using EFT.InventoryLogic;
using Oracle.Data;
using Oracle.Tools;
using Oracle.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{
    /// <summary>
    /// 物资透视部分
    /// </summary>
    public class LootESP : IOracleESP
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

            DrawLootFOVCircle();
            DrawLootText(cam, RenderUtils.EspTextStyle); // 统一使用 RenderUtils 的样式
        }
        /// <summary>
        /// 绘制文本
        /// </summary>
        /// <param name="cam">摄像机</param>
        /// <param name="textStyle">样式</param>
        public static void DrawLootText(Camera cam, GUIStyle textStyle)
        {
            if (OracleLootManager.CachedLootList == null || OracleLootManager.CachedLootList.Count == 0) return;
            //查找中心
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float fovRadius = LootESPCfg.LootESPFovRange.Value;

            int fovMinPrice = LootESPCfg.LootESPFovMinPrice.Value;
            int fovMinLevel = OracleLootManager.GetLevelByPrice(fovMinPrice);
            //富文本防御, 避免问题
            foreach (LootData loot in OracleLootManager.CachedLootList)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(loot.Position);
                if (screenPos.z > 0.01f)
                {
                    float screenX = screenPos.x;
                    //展开容器战利品表
                    float screenY = Screen.height - screenPos.y + loot.YOffset;
                    //FOV计算
                    if (LootESPCfg.EnableLootESPFov.Value)
                    {
                        if (loot.Price < fovMinPrice && loot.ItemLevel < fovMinLevel)
                        {
                            Vector2 itemScreenPos = new Vector2(screenX, screenY);
                            float distToCenter = Vector2.Distance(screenCenter, itemScreenPos);
                            //脱离范围
                            if (distToCenter > fovRadius) continue;
                        }
                    }
                    string espText = $"{loot.Name}";
                    //绘制
                    if (loot.Container != null && LootESPCfg.EnableContainerLootESP.Value)
                    {
                        GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), espText, textStyle);
                    }
                    if (loot.Container == null && LootESPCfg.EnableLooseLootESP.Value)
                    {
                        GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), espText, textStyle);
                    }
                }
            }
        }
        
        /// <summary>
        /// 绘制约束范围
        /// </summary>
        public static void DrawLootFOVCircle()
        {
            if (!LootESPCfg.ShowLootESPFov.Value) return;
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float fovRadius = LootESPCfg.LootESPFovRange.Value;

            // ⭐ 3. 直接调用 RenderUtils 里的画圆方法
            RenderUtils.DrawCircle(screenCenter, fovRadius, new Color(0.8f, 1f, 1f, 0.4f), 64);
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
    public class LootESPCfg : IOracleCfg, IOracleKeyUpdate
    {
        internal static ConfigEntry<KeyCode> LooseLootESPKey { get; set; }
        internal static ConfigEntry<KeyCode> ContainerLootESPKey { get; set; }
        internal static ConfigEntry<bool> EnableContainerLootESP { get; set; }
        internal static ConfigEntry<bool> EnableLooseLootESP { get; set; }
        internal static ConfigEntry<int> LootESPMaxDistance { get; set; }
        internal static ConfigEntry<int> LootESPMinPrice { get; set; }
        internal static ConfigEntry<bool> EnableLootESPFov { get; set; }
        internal static ConfigEntry<bool> ShowLootESPFov { get; set; }
        internal static ConfigEntry<int> LootESPFovRange { get; set; }
        internal static ConfigEntry<int> LootESPFovMinPrice { get; set; }
        internal static ConfigEntry<bool> ShowItemFullName { get; set; }
        internal static ConfigEntry<bool> HighlightWishListItem { get; set; }
        internal static ConfigEntry<bool> HighlightQuestItem { get; set; }
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            LooseLootESPKey = config.Bind<KeyCode>(
                "物资透视",
                "散落物资透视快捷键",
                KeyCode.F3,
                "按下切换地上的散落物资透视"
            );

            ContainerLootESPKey = config.Bind<KeyCode>(
                "物资透视",
                "容器物资透视快捷键",
                KeyCode.F4,
                "按下切换容器(如箱子/衣服/包)物资透视"
            );
            EnableLooseLootESP = config.Bind<bool>(
                "物资透视",
                "启用松散物资透视",
                true,
                "透视地面上的物资"
            );
            EnableContainerLootESP = config.Bind<bool>(
                "物资透视",
                "启用容器物资透视",
                true,
                "透视容器里的物资"
            );
            EnableLootESPFov = config.Bind<bool>(
                "物资透视",
                "启用约束透视",
                true,
                "只透视准星一定半径内的物资"
            );
            ShowLootESPFov = config.Bind<bool>(
                "物资透视",
                "显示约束透视范围",
                true,
                "显示约束透视范围"
            );
            LootESPMaxDistance = config.Bind<int>(
                "物资透视",
                "透视范围",
                200,
                new ConfigDescription(
                    "透视可见的范围",
                    new AcceptableValueRange<int>(50, 1000)
                )
            );
            LootESPMinPrice = config.Bind<int>(
                "物资透视",
                "价格过滤",
                15000,
                new ConfigDescription(
                    "透视物资可见的最低价格",
                    new AcceptableValueRange<int>(1000, 1000000)
                )
            );
            LootESPFovRange = config.Bind<int>(
                "物资透视",
                "约束透视范围",
                100,
                new ConfigDescription(
                    "约束透视的半径",
                    new AcceptableValueRange<int>(0, 1000)
                )
            );
            ShowItemFullName = config.Bind<bool>(
                "物资透视",
                "显示物品全名",
                false,
                "使用物品全名显示透视"
            );
            HighlightWishListItem = config.Bind<bool>(
                "物资透视",
                "高亮愿望单物品",
                false,
                "启用后愿望单物品将以玫红色和高优先级绘制"
            );
            HighlightQuestItem = config.Bind<bool>(
                "物资透视",
                "高亮任务物品",
                false,
                "启用后任务物品将以灰色和高优先级绘制"
            );
            LootESPFovMinPrice = config.Bind<int>(
                "物资透视",
                "约束透视白名单价格",
                150000,
                new ConfigDescription(
                    "显示在约束范围外的物品最低价格",
                    new AcceptableValueRange<int>(1000, 10000000)
                )
            );
        }
        public void RegisterKeyUpdate()
        {
            OracleEvent.OnUpdate += KeyUpdate;
        }
        public static void KeyUpdate()
        {
            if (Input.GetKeyDown(LooseLootESPKey.Value))
            {
                EnableLooseLootESP.Value = !EnableLooseLootESP.Value;
                var value = EnableLooseLootESP.Value;
                OracleNotify.Message($"松散物资透视已{(value ? "启用" : "禁用")}!", value ? ENotificationIconType.Default : ENotificationIconType.Alert, GlobalCfg.MuteNotice.Value);
            }
            if (Input.GetKeyDown(ContainerLootESPKey.Value))
            {
                EnableContainerLootESP.Value = !EnableContainerLootESP.Value;
                var value = EnableContainerLootESP.Value;
                OracleNotify.Message($"容器物资透视已{(value ? "启用" : "禁用")}!", value ? ENotificationIconType.Default : ENotificationIconType.Alert, GlobalCfg.MuteNotice.Value);
            }
        }
    }
}