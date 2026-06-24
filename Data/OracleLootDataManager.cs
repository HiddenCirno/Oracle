using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Oracle.ESP;
using Oracle.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oracle.Data
{
    /// <summary>
    /// 战利品数据总线
    /// </summary>
    public static class OracleLootDataManager
    {
        /// <summary>
        /// 全局缓存表
        /// </summary>
        public static List<LootData> CachedLootList = new List<LootData>();

        /// <summary>
        /// 容器缓存表
        /// </summary>
        public static LootableContainer[] CachedContainers;

        /// <summary>
        /// 物品等级缓存
        /// </summary>
        public static Dictionary<MongoID, int?> ItemLevelCache = new Dictionary<MongoID, int?>();

        /// <summary>
        /// 价值界限定义
        /// </summary>
        public static class PriceTier
        {
            public const int Tier1 = 10000;
            public const int Tier2 = 20000;
            public const int Tier3 = 50000;
            public const int Tier4 = 100000;
            public const int Tier5 = 200000;
            public const int Tier6 = 500000;
        }

        /// <summary>
        /// 扫描协程
        /// </summary>
        /// <returns></returns>
        public static System.Collections.IEnumerator LootScannerCoroutine()
        {
            //双缓冲分配
            List<LootData> frontBuffer = new List<LootData>(10000);
            List<LootData> backBuffer = new List<LootData>(10000);
            Dictionary<Vector3, int> positionOffsets = new Dictionary<Vector3, int>(2000);

            //初始指针
            CachedLootList = frontBuffer;

            while (true)
            {
                yield return new WaitForSeconds(1f);

                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null)
                {
                    if (CachedContainers != null)
                    {
                        CachedContainers = null;
                    }
                    continue;
                }

                if (PluginsCore.CorrectGameWorld.LootItems == null)
                {
                    continue;
                }

                //清空缓存
                backBuffer.Clear();
                positionOffsets.Clear();

                Vector3 playerPos = PluginsCore.CorrectPlayer.Transform.position;
                float maxLootDistance = LootESPCfg.LootESPMaxDistance.Value;

                foreach (var lootItem in PluginsCore.CorrectGameWorld.LootItems.GetValuesEnumerator())
                {
                    if (lootItem == null || lootItem.Item == null || lootItem.gameObject == null) continue;
                    if (!lootItem.gameObject.activeSelf) continue;

                    if (!OracleCommon.IsInRange((int)maxLootDistance, playerPos, lootItem.transform.position)) continue;

                    float dist = Vector3.Distance(playerPos, lootItem.transform.position);

                    //写入缓存
                    TryAddLootData(backBuffer, positionOffsets, lootItem.Item, lootItem, null,
                        LootESPCfg.ShowItemFullName.Value ? lootItem.Item.Name.Localized() : lootItem.Item.ShortName.Localized(),
                        lootItem.transform.position, (int)dist);
                }

                //容器透视
                if (CachedContainers != null)
                {
                    foreach (var container in CachedContainers)
                    {
                        if (container?.ItemOwner?.RootItem == null) continue;
                        if (!OracleCommon.IsInRange((int)maxLootDistance, playerPos, container.transform.position)) continue;

                        int dist = Mathf.RoundToInt(Vector3.Distance(playerPos, container.transform.position));
                        string containerName = GetContainerName(container);

                        foreach (var item in container.ItemOwner.RootItem.GetAllItems())
                        {
                            if (item == container.ItemOwner.RootItem) continue;

                            //写入缓存
                            TryAddLootData(backBuffer, positionOffsets, item, null, container,
                                LootESPCfg.ShowItemFullName.Value ? item.Name.Localized() : item.ShortName.Localized(),
                                container.transform.position, dist, string.Format("text_esp_container_tag".i18n(), containerName));
                        }
                    }
                }

                //交换指针
                var temp = frontBuffer;
                frontBuffer = backBuffer;
                backBuffer = temp;

                CachedLootList = frontBuffer;
            }
        }

        /// <summary>
        /// 维护战利品表
        /// </summary>
        /// <param name="targetList">目标列表</param>
        /// <param name="offsetDict">偏移距离</param>
        /// <param name="itemKey">物品key</param>
        /// <param name="itemName">物品名</param>
        /// <param name="pos">坐标</param>
        /// <param name="dist">距离</param>
        /// <param name="prefix">预修复</param>
        private static void TryAddLootData(List<LootData> targetList, Dictionary<Vector3, int> offsetDict, Item item, LootItem? lootItem, LootableContainer? lootContainer, string itemName, Vector3 pos, int dist, string prefix = "")
        {
            if (item == null) return;
            string itemKey = item.TemplateId;
            //过滤掉物品栏
            //尸体实际上是一个以物品栏和不可拾取形式存在的容器
            if (itemKey == "55d7217a4bdc2d86028b456d") return;
            //过滤掉无效名称, 内衬什么的没名字的东西
            if (string.IsNullOrEmpty(itemName)) return;
            //字典O(1)查价
            var price = GetItemPrice(itemKey);
            int itemPrice = price ?? 0;
            //价值过滤
            int minPriceThreshold = LootESPCfg.LootESPMinPrice.Value;
            int filterLevel = GetLevelByPrice(minPriceThreshold);
            //求等级
            ItemLevelCache.TryGetValue(itemKey, out var level);
            if (level == null)
            {
                level = GetItemLevel(item);
                ItemLevelCache[itemKey] = level;
            }
            int itemLevel = (int)level;
            if (LootESPCfg.HighlightWishListItem.Value && IsWishlistItem(itemKey))
            {
                itemLevel = 9; // 愿望单最高优先级
            }
            else if (LootESPCfg.HighlightQuestItem.Value && item.Template.QuestItem == true)
            {
                itemLevel = 8; // 其次是任务道具
            }
            ExtendWishlistItem.LabyrinthSpecialItem.TryGetValue(itemKey, out var labyrinthItem);
            if (LootESPCfg.HighlightLabyrinthSpecialItem.Value && labyrinthItem!=null)
            {
                itemLevel = 9;
            }
            ExtendWishlistItem.StreetsSpecialItem.TryGetValue(itemKey, out var streetItem);
            if (LootESPCfg.HighlightBloodyKey.Value && streetItem != null)
            {
                itemLevel = 9;
            }
            if (itemPrice < minPriceThreshold && itemLevel < filterLevel)
            {
                return;
            }
            //价值格式化
            string priceStr = itemPrice >= 1000000 ? (itemPrice / 1000000f).ToString("0.##") + "M" :
            itemPrice >= 10000 ? (itemPrice / 1000f).ToString("0.#") + "K" :
            itemPrice.ToString();
            //颜色转码
            OracleColor iColor = GetColorByLevel(itemLevel);
            //富文本合并
            string fullName = string.IsNullOrEmpty(prefix) ? itemName : $"{prefix} {itemName}";
            string formattedName = string.Format("text_esp_loot_format".i18n(), iColor, fullName, priceStr, OracleColorManager.Distance, dist);
            int currentYOffset = 0;

            // ⭐ 核心优化：只有容器/尸体（StaticLoot）才参与 YOffset 计算
            if (lootContainer != null)
            {
                if (!offsetDict.ContainsKey(pos))
                {
                    offsetDict[pos] = 0;
                }
                currentYOffset = offsetDict[pos];
                offsetDict[pos] += 20;
            }
            //生成数据
            targetList.Add(new LootData
            {
                ItemRef = item,
                LootableItem = lootItem,
                Container = lootContainer,
                ItemLevel = itemLevel,
                Position = pos,
                Name = formattedName,
                Distance = dist,
                Price = itemPrice,
                ItemColor = iColor,
                YOffset = currentYOffset,
                StackCount = item.StackObjectsCount
            });
        }

        /// <summary>
        /// 获取容器名称
        /// </summary>
        /// <param name="container"></param>
        /// <returns></returns>
        public static string GetContainerName(LootableContainer container)
        {
            if (container == null) return "text_esp_loot_on_the_ground".i18n();
            string containerName = container.ItemOwner.RootItem.ShortName.Localized();
            return string.IsNullOrEmpty(containerName) ? "text_esp_loot_in_the_container".i18n() : containerName;
        }

        /// <summary>
        /// 从等级返回颜色
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public static OracleColor GetColorByLevel(int level)
        {
            switch (level)
            {
                case 9: return OracleColorManager.LootTierEX;
                case 8: return OracleColorManager.LootTierX;
                case 7: return OracleColorManager.LootTier6;
                case 6: return OracleColorManager.LootTier5;
                case 5: return OracleColorManager.LootTier4;
                case 4: return OracleColorManager.LootTier3;
                case 3: return OracleColorManager.LootTier2;
                case 2: return OracleColorManager.LootTier1;
                case 1: return OracleColorManager.LootTier0;
                default: return OracleColorManager.LootTier0;
            }
        }

        /// <summary>
        /// 取物品价格
        /// </summary>
        /// <param name="itemid"></param>
        /// <returns></returns>
        public static int? GetItemPrice(MongoID itemid)
        {
            PluginsCore.HandbookDict.TryGetValue(itemid, out int itemPrice);
            return itemPrice;
        }

        /// <summary>
        /// 从价格返回等级
        /// </summary>
        /// <param name="price"></param>
        /// <returns></returns>
        public static int GetLevelByPrice(int price)
        {
            if (price >= PriceTier.Tier6) return 7; // 50万
            if (price >= PriceTier.Tier5) return 6; // 20万
            if (price >= PriceTier.Tier4) return 5; // 10万
            if (price >= PriceTier.Tier3) return 4; // 5万
            if (price >= PriceTier.Tier2) return 3; // 2万
            if (price >= PriceTier.Tier1) return 2; // 1万
            return 1; // 垃圾
        }

        /// <summary>
        /// 求物品等级
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int GetItemLevel(Item item)
        {
            var template = item.Template;
            if (template == null) return 0;
            if (template is AmmoTemplate ammoTemplate)
            {
                return GetAmmoLevel(item);
            }
            if (template is AmmoBoxTemplate ammoBoxTemplate)
            {
                var ammoItem = item.GetAllItems().FirstOrDefault(x => x.Template is AmmoTemplate);
                if (ammoItem == null) return 1; // 预防万一有空盒子
                return GetAmmoLevel(ammoItem);
            }
            if (template is BackpackTemplateClass backpackTemplate)
            {
                var size = 0;
                backpackTemplate.Grids.ExecuteForEach(x => size += (x.GridHeight * x.GridWidth));
                if (size >= 35) return 6;
                if (size >= 30) return 5;
                if (size >= 25) return 4;
                if (size >= 16) return 3;
                if (size >= 12) return 2;
                if (size >= 0) return 1;
            }
            if (template is VestTemplateClass vestTemplate)
            {
                var size = 0;
                vestTemplate.Grids.ExecuteForEach(x => size += (x.GridHeight * x.GridWidth));
                if (size >= 20) return 5;
                if (size >= 16) return 4;
                if (size >= 12) return 3;
                if (size >= 8) return 2;
                if (size >= 0) return 1;
            }
            //坏了, 客户端的ITemTemplate是不完整的
            //if(template.Catr) return 0;
            var price = GetItemPrice(item.TemplateId) ?? 0;
            return GetLevelByPrice(price);
        }

        /// <summary>
        /// 子方法, 求弹药等级
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static int GetAmmoLevel(Item item)
        {
            if (item.Template is AmmoTemplate ammoTemplate)
            {
                if (ammoTemplate.PenetrationPower >= 60) return 6;
                if (ammoTemplate.PenetrationPower >= 50) return 5;
                if (ammoTemplate.PenetrationPower >= 40) return 4;
                if (ammoTemplate.PenetrationPower >= 30) return 3;
                if (ammoTemplate.PenetrationPower >= 20) return 2;
                if (ammoTemplate.PenetrationPower >= 10) return 1;
            }
            return 1;
        }

        /// <summary>
        /// 检查物品是否在玩家的愿望单中
        /// </summary>
        /// <param name="templateId">物品的 TemplateId</param>
        public static bool IsWishlistItem(string templateId)
        {
            var player = PluginsCore.CorrectPlayer;
            if (player?.Profile?.WishlistManager == null) return false;

            //丢弃out
            //out out!
            //莫名其妙的笑点....
            return player.Profile.WishlistManager.IsInWishlist(templateId, true, out _);
        }
    }
}