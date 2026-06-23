using BepInEx.Configuration;
using EFT.Communications;
using Oracle.Data;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{
    /// <summary>
    /// 物资透视
    /// </summary>
    public class LootESP : IOracleESP
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

            DrawLootFOVCircle();
            DrawLootText(cam, OracleRendering.EspTextStyle);
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

            OracleRendering.DrawCircle(screenCenter, fovRadius, OracleColorManager.LootCircle, 64);
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    [OracleCfgOrder(3)]
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
        internal static ConfigEntry<bool> HighlightLabyrinthSpecialItem { get; set; }
        internal static ConfigEntry<bool> HighlightBloodyKey { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            EnableLooseLootESP = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "启用松散物资透视",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_looseloot_esp_enable_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_looseloot_esp_enable_name"),
                        IsAdvanced = false,
                        Order = 180
                    }
                )
            );
            LooseLootESPKey = config.Bind<KeyCode>(
                "3. 巡天星轨 / ESP Module",
                "散落物资透视快捷键",
                KeyCode.F3,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_looseloot_esp_enable_key_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_looseloot_esp_enable_key_name"),
                        IsAdvanced = false,
                        Order = 179
                    }
                )
            );
            EnableContainerLootESP = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "启用容器物资透视",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_staticloot_esp_enable_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_staticloot_esp_enable_name"),
                        IsAdvanced = false,
                        Order = 178
                    }
                )
            );
            ContainerLootESPKey = config.Bind<KeyCode>(
                "3. 巡天星轨 / ESP Module",
                "容器物资透视快捷键",
                KeyCode.F4,
               new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_staticloot_esp_enable_key_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_staticloot_esp_enable_key_name"),
                        IsAdvanced = false,
                        Order = 177
                    }
                )
            );
            ShowItemFullName = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "显示物品全名",
                false,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_show_full_name_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_show_full_name_name"),
                        IsAdvanced = false,
                        Order = 176
                    }
                )
            );
            LootESPMaxDistance = config.Bind<int>(
                "3. 巡天星轨 / ESP Module",
                "透视范围",
                200,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_max_distance_desc"),
                    new AcceptableValueRange<int>(50, 1000),
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_max_distance_name"),
                        IsAdvanced = false,
                        Order = 175
                    }
                )
            );
            LootESPMinPrice = config.Bind<int>(
                "3. 巡天星轨 / ESP Module",
                "价格过滤",
                15000,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_min_price_desc"),
                    new AcceptableValueRange<int>(1, 1000000),
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_min_price_name"),
                        IsAdvanced = false,
                        Order = 174
                    }
                )
            );
            EnableLootESPFov = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "启用约束透视",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_fov_enable_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_fov_enable_name"),
                        IsAdvanced = false,
                        Order = 173
                    }
                )
            );
            ShowLootESPFov = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "显示约束透视范围",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_show_fov_enable_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_show_fov_enable_name"),
                        IsAdvanced = false,
                        Order = 172
                    }
                )
            );
            LootESPFovRange = config.Bind<int>(
                "3. 巡天星轨 / ESP Module",
                "约束透视范围",
                100,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_fov_radius_desc"),
                    new AcceptableValueRange<int>(0, 1000),
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_fov_radius_name"),
                        IsAdvanced = false,
                        Order = 171
                    }
                )
            );
            LootESPFovMinPrice = config.Bind<int>(
                "3. 巡天星轨 / ESP Module",
                "约束透视白名单价格",
                150000,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_fov_min_price_desc"),
                    new AcceptableValueRange<int>(1000, 10000000),
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_fov_min_price_name"),
                        IsAdvanced = false,
                        Order = 170
                    }
                )
            );
            HighlightWishListItem = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "高亮愿望单物品",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_highlight_wishlist_item_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_highlight_wishlist_item_name"),
                        IsAdvanced = false,
                        Order = 169
                    }
                )
            );
            HighlightQuestItem = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "高亮任务物品",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_highlight_quest_item_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_highlight_quest_item_name"),
                        IsAdvanced = false,
                        Order = 168
                    }
                )
            );
            HighlightLabyrinthSpecialItem = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "透视迷宫道具",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_highlight_labyrinth_item_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_highlight_labyrinth_item_name"),
                        IsAdvanced = false,
                        Order = 167
                    }
                )
            );
            HighlightBloodyKey = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "透视血色钥匙",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_esp_module_loot_esp_highlight_bloody_key_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_esp_module_loot_esp_highlight_bloody_key_name"),
                        IsAdvanced = false,
                        Order = 166
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
            if (Input.GetKeyDown(LooseLootESPKey.Value))
            {
                EnableLooseLootESP.Value = !EnableLooseLootESP.Value;
                var value = EnableLooseLootESP.Value;
                OracleNotify.Message(
                    string.Format(
                        LocaleManager.Get("message_loot_esp_looseloot_enable"),
                        value ? LocaleManager.Get("text_enable") : LocaleManager.Get("text_disable")
                    ),
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    GlobalCfg.MuteNotice.Value
                );
            }
            if (Input.GetKeyDown(ContainerLootESPKey.Value))
            {
                EnableContainerLootESP.Value = !EnableContainerLootESP.Value;
                var value = EnableContainerLootESP.Value;
                OracleNotify.Message(
                    string.Format(
                        LocaleManager.Get("message_loot_esp_staticloot_enable"),
                        value ? LocaleManager.Get("text_enable") : LocaleManager.Get("text_disable")
                    ),
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert,
                    GlobalCfg.MuteNotice.Value
                );
            }
        }
    }
}