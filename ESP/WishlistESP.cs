using BepInEx.Configuration;
using EFT;
using Oracle.Data;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{
    /// <summary>
    /// 实体愿望单战利品透视配置。
    /// 显示玩家身上和尸体上的愿望单物品（仅过滤愿望单，避免刷屏）。
    /// </summary>
    [OracleCfgOrder(4)]
    public class WishlistESPCfg : IOracleCfg
    {
        /// <summary>显示玩家身上的愿望单物品</summary>
        internal static ConfigEntry<bool> EnablePlayerWishlistESP { get; set; }
        /// <summary>显示尸体身上的愿望单物品</summary>
        internal static ConfigEntry<bool> EnableCorpseWishlistESP { get; set; }

        public void Initialize(ConfigFile config)
        {
            EnablePlayerWishlistESP = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "显示玩家身上愿望单物品",
                true,
                new ConfigDescription(
                    "cfg_esp_module_wishlist_player_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_esp_module_wishlist_player_name".i18n(),
                        IsAdvanced = false,
                        Order = 145
                    }
                )
            );
            EnableCorpseWishlistESP = config.Bind<bool>(
                "3. 巡天星轨 / ESP Module",
                "显示尸体身上愿望单物品",
                true,
                new ConfigDescription(
                    "cfg_esp_module_wishlist_corpse_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_esp_module_wishlist_corpse_name".i18n(),
                        IsAdvanced = false,
                        Order = 144
                    }
                )
            );
        }
    }

    /// <summary>
    /// 实体愿望单战利品透视（OnGUI 绘制）。
    /// 数据桥同款并行字段由 OverlayPrimitiveBuilder 消费，此处仅 OnGUI 富文本绘制。
    /// </summary>
    public class WishlistESP : IOracleESP
    {
        public void SubscribeEvent()
        {
            OracleEvent.OnDrawESP += OnDrawESP;
        }

        private void OnDrawESP()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            //玩家愿望单随动：与玩家透视联动（任一关闭都不显示）
            if (WishlistESPCfg.EnablePlayerWishlistESP.Value && PlayerESPCfg.EnablePlayerESP.Value)
            {
                DrawPlayerWishlist(cam, OracleRendering.EspTextStyle);
            }
            //尸体愿望单随动：与尸体透视联动（任一关闭都不显示）
            if (WishlistESPCfg.EnableCorpseWishlistESP.Value && CorpseESPCfg.EnableCorpseESP.Value)
            {
                DrawCorpseWishlist(cam, OracleRendering.EspTextStyle);
            }
        }

        /// <summary>
        /// 绘制玩家身上的愿望单物品（主标签下方逐行堆叠）
        /// </summary>
        public static void DrawPlayerWishlist(Camera cam, GUIStyle textStyle)
        {
            if (OracleWishlistDataManager.CachedPlayerWishlist == null) return;
            if (PluginsCore.CorrectGameWorld?.AllAlivePlayersList == null) return;
            if (PluginsCore.CorrectPlayer == null) return;

            Vector3 myPos = PluginsCore.CorrectPlayer.Transform.position;
            float maxDist = PlayerESPCfg.PlayerESPMaxDistance.Value;
            textStyle.richText = true;

            foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
            {
                if (player == null || player == PluginsCore.CorrectPlayer || player.PlayerBones == null) continue;
                if (!OracleCommon.IsInRange((int)maxDist, myPos, player.Transform.position)) continue;

                if (!OracleWishlistDataManager.CachedPlayerWishlist.TryGetValue(player.ProfileId, out var list) || list == null || list.Count == 0) continue;

                //主标签锚点（与 PlayerESP 一致：头顶 +0.3f）
                Vector3? headPos = OraclePlayerDataManager.GetBonePos(player.PlayerBones.Head);
                if (!headPos.HasValue) continue;
                Vector3 textScreenPos = cam.WorldToScreenPoint(headPos.Value + new Vector3(0, 0.3f, 0));
                if (textScreenPos.z <= 0.01f) continue;

                float screenX = textScreenPos.x;
                //从主标签下方开始逐行堆叠
                float baseY = Screen.height - textScreenPos.y + 20f;
                for (int i = 0; i < list.Count && i < 6; i++) // 上限 6 行防刷屏
                {
                    WishlistItemData item = list[i];
                    float screenY = baseY + i * 18f;
                    GUI.Label(new Rect(screenX - 100, screenY - 10, 200, 20), item.FormattedText, textStyle);
                }
            }
        }

        /// <summary>
        /// 绘制尸体身上的愿望单物品（尸体标签下方逐行堆叠）
        /// </summary>
        public static void DrawCorpseWishlist(Camera cam, GUIStyle textStyle)
        {
            if (OracleCorpseDataManager.CachedCorpseList == null || OracleCorpseDataManager.CachedCorpseList.Count == 0) return;
            textStyle.richText = true;

            foreach (CorpseData corpse in OracleCorpseDataManager.CachedCorpseList)
            {
                if (corpse.WishlistItems == null || corpse.WishlistItems.Count == 0) continue;

                Vector3 screenPos = cam.WorldToScreenPoint(corpse.Position);
                if (screenPos.z <= 0.01f) continue;

                float screenX = screenPos.x;
                //从尸体标签（screenY-10）下方开始堆叠
                float baseY = Screen.height - screenPos.y + 10f;
                for (int i = 0; i < corpse.WishlistItems.Count && i < 6; i++) // 上限 6 行防刷屏
                {
                    WishlistItemData item = corpse.WishlistItems[i];
                    float screenY = baseY + i * 18f;
                    GUI.Label(new Rect(screenX - 100, screenY - 10, 200, 20), item.FormattedText, textStyle);
                }
            }
        }
    }
}
