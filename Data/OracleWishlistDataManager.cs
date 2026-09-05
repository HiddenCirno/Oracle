using EFT;
using EFT.InventoryLogic;
using Oracle.ESP;
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Oracle.Data
{
    /// <summary>
    /// 实体（玩家）身上的愿望单战利品数据总线。
    /// 协程 2s 扫描所有存活玩家的装备栏，过滤愿望单物品（跳过安全箱槽位及内容、
    /// PMC 刀鞘槽位），结果按玩家 ProfileId 缓存，供 OnGUI 与叠加层数据桥消费。
    /// 尸体侧愿望单由 CorpseScannerCoroutine 直接填充 CorpseData.WishlistItems。
    /// </summary>
    public static class OracleWishlistDataManager
    {
        /// <summary>
        /// 玩家身上的愿望单战利品缓存（key = player.ProfileId）
        /// </summary>
        public static Dictionary<string, List<WishlistItemData>> CachedPlayerWishlist = new Dictionary<string, List<WishlistItemData>>();

        /// <summary>
        /// 玩家愿望单扫描协程：2s 频率，分帧让出主线程
        /// </summary>
        public static System.Collections.IEnumerator WishlistScannerCoroutine()
        {
            //双缓冲分配
            Dictionary<string, List<WishlistItemData>> frontBuffer = new Dictionary<string, List<WishlistItemData>>();
            Dictionary<string, List<WishlistItemData>> backBuffer = new Dictionary<string, List<WishlistItemData>>();

            CachedPlayerWishlist = frontBuffer;

            const int BATCH_SIZE = 30; // 每处理 30 个物品让出主线程一帧
            int batchCounter = 0;

            while (true)
            {
                yield return new WaitForSeconds(2f);

                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null)
                {
                    backBuffer.Clear();
                    var tmp = frontBuffer;
                    frontBuffer = backBuffer;
                    backBuffer = tmp;
                    CachedPlayerWishlist = frontBuffer;
                    continue;
                }

                var alivePlayers = PluginsCore.CorrectGameWorld.AllAlivePlayersList;
                if (alivePlayers == null) continue;

                //清空后台缓存
                foreach (var kv in backBuffer) kv.Value.Clear();
                backBuffer.Clear();

                Vector3 myPos = PluginsCore.CorrectPlayer.Transform.position;
                float maxDist = PlayerESPCfg.PlayerESPMaxDistance.Value;

                foreach (Player player in alivePlayers)
                {
                    //排除自己
                    if (player == null || player == PluginsCore.CorrectPlayer) continue;
                    if (!OracleCommon.IsInRange((int)maxDist, myPos, player.Transform.position)) continue;

                    var equipment = player.Inventory?.Equipment;
                    if (equipment == null) continue;

                    //锚点：头顶（与玩家文字标签一致）
                    Vector3 anchorPos = player.Transform.position;
                    if (player.PlayerBones?.Head != null && player.PlayerBones.Head.Original != null)
                    {
                        anchorPos = player.PlayerBones.Head.Original.position;
                    }

                    var list = CollectWishlistItems(equipment, player.Profile?.Info?.Side ?? EPlayerSide.Savage,
                        anchorPos, (int)maxDist, myPos, ref batchCounter, BATCH_SIZE, null);

                    if (list != null && list.Count > 0)
                    {
                        backBuffer[player.ProfileId] = list;
                    }

                    //分帧让出主线程
                    if (batchCounter >= BATCH_SIZE)
                    {
                        batchCounter = 0;
                        yield return null;
                    }
                }

                //交换指针
                var temp = frontBuffer;
                frontBuffer = backBuffer;
                backBuffer = temp;
                CachedPlayerWishlist = frontBuffer;
            }
        }

        /// <summary>
        /// 遍历装备栏收集愿望单物品（跳过安全箱槽位、PMC 刀鞘槽位）。
        /// 供玩家/尸体扫描复用；尸体侧由 CorpseScannerCoroutine 调用（collector 回调填充 CorpseData）。
        /// </summary>
        /// <param name="equipment">实体装备栏（InventoryEquipment）</param>
        /// <param name="side">实体阵营（PMC 判断刀鞘过滤）</param>
        /// <param name="anchorPos">条目锚点世界坐标</param>
        /// <param name="maxDist">距离过滤</param>
        /// <param name="playerPos">本地玩家坐标</param>
        /// <param name="batchCounter">分帧计数器</param>
        /// <param name="batchSize">批次大小</param>
        /// <param name="collector">回调：每命中一个愿望单物品调用（供尸体直接塞 CorpseData）</param>
        /// <returns>收集到的条目列表（collector 非空时返回 null，走回调）</returns>
        public static List<WishlistItemData> CollectWishlistItems(InventoryEquipment equipment, EPlayerSide side,
            Vector3 anchorPos, int maxDist, Vector3 playerPos, ref int batchCounter, int batchSize,
            System.Action<WishlistItemData> collector)
        {
            if (equipment == null) return null;

            List<WishlistItemData> result = collector == null ? new List<WishlistItemData>() : null;

            //PMC：刀鞘（近战武器不可拾取）需过滤；Scav 无此限制
            bool isPmc = side == EPlayerSide.Usec || side == EPlayerSide.Bear;

            foreach (var slot in equipment.Slots)
            {
                if (slot == null) continue;
                //安全箱槽位及内容一律过滤（所有实体）
                if (slot.ID == "SecuredContainer") continue;
                //PMC 刀鞘槽位过滤
                if (isPmc && slot.ID == "Scabbard") continue;

                var contained = slot.ContainedItem;
                if (contained == null) continue;

                //递归取槽位内所有物品（含容器内容、武器改装件等）
                foreach (var item in contained.GetAllItems())
                {
                    if (item == null) continue;
                    string templateId = item.TemplateId;
                    if (string.IsNullOrEmpty(templateId)) continue;
                    //仅愿望单
                    if (!OracleLootDataManager.IsWishlistItem(templateId)) continue;

                    int dist = Mathf.RoundToInt(Vector3.Distance(playerPos, anchorPos));
                    var entry = BuildEntry(item, anchorPos, dist);

                    if (collector != null)
                    {
                        collector(entry);
                    }
                    else
                    {
                        result.Add(entry);
                    }

                    //分帧计数：只累加不清零，由外层调用方检查阈值并 yield（避免内部清零导致外层永假）
                    batchCounter++;
                }
            }

            return result;
        }

        /// <summary>
        /// 构建单条愿望单条目（富文本 + 叠加层纯文本 + 颜色）
        /// </summary>
        private static WishlistItemData BuildEntry(Item item, Vector3 anchorPos, int dist)
        {
            string templateId = item.TemplateId;
            string itemName = OracleLootDataManager.GetLocalizedItemName(item, LootESPCfg.ShowItemFullName.Value);
            int stackCount = item.StackObjectsCount;

            //价值/等级：愿望单最高优先级（等级 9 = LootTierEX 高亮）
            int price = OracleLootDataManager.GetItemPrice(templateId) ?? 0;
            int level = OracleLootDataManager.GetItemLevel(item);
            if (OracleLootDataManager.IsWishlistItem(templateId)) level = 9;
            OracleColor color = OracleLootDataManager.GetColorByLevel(level);

            //价值格式化（与 LootData 一致）
            string priceStr = price >= 1000000 ? (price / 1000000f).ToString("0.##") + "M" :
                price >= 10000 ? (price / 1000f).ToString("0.#") + "K" :
                price.ToString();

            //数量后缀（堆叠>1 时显示 xN）
            string countStr = stackCount > 1 ? $" x{stackCount}" : "";

            //富文本（OnGUI 专用）
            string formattedText = string.Format("text_esp_wishlist_item_format".i18n(), color, $"{itemName}{countStr}", priceStr);

            //叠加层纯文本（无颜色标签）
            string overlayText = $"{itemName}{countStr} {priceStr}";

            return new WishlistItemData
            {
                Position = anchorPos,
                FormattedText = formattedText,
                Distance = dist,
                OverlayText = overlayText,
                Color = color,
                YOffset = 0, // 由调用方在渲染时按条目索引堆叠
                StackCount = stackCount
            };
        }
    }
}
