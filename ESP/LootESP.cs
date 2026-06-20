using BepInEx.Configuration;
using EFT;
using EFT.Hideout;
using EFT.Interactive;
using EFT.InventoryLogic;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Oracle.ESP
{
    /// <summary>
    /// 战利品数据定义
    /// </summary>
    public struct LootData
    {
        public Item ItemRef; // ⭐ 新增：直接保存底层物品引用，用于图标渲染和元数据捕获
        public LootItem? LootableItem; // ⭐ 新增：直接保存底层物品引用，用于图标渲染和元数据捕获
        public LootableContainer? Container; // ⭐ 新增：直接保存底层物品引用，用于图标渲染和元数据捕获
        public Vector3 Position;
        public int ItemLevel;//新增物品等级
        public string Name;
        public int Distance;
        public int Price;
        public Color ItemColor;
        public int YOffset;
    }

    /// <summary>
    /// 物资透视部分
    /// </summary>
    public class LootESP
    {
        /// <summary>
        /// 约束透视范围
        /// </summary>
        private static Material lineMaterial;

        /// <summary>
        /// 唯一的全局战利品表
        /// </summary>
        public static List<LootData> CachedLootList = new List<LootData>();

        /// <summary>
        /// 唯一的全局容器表
        /// </summary>
        public static LootableContainer[] CachedContainers;

        public static class PriceColor
        {
            //愿望单物品
            public static readonly Color TierEX = new Color(0.862f, 0.078f, 0.235f);
            public static readonly Color TierX = Color.gray;
            public static readonly Color Tier0 = Color.white;
            public static readonly Color Tier1 = new Color(0f, 0.666f, 0f);
            public static readonly Color Tier2 = new Color(0f, 0.627f, 1f);
            public static readonly Color Tier3 = new Color(0.666f, 0f, 0.666f);
            public static readonly Color Tier4 = new Color(1f, 0.666f, 0f);
            public static readonly Color Tier5 = new Color(0.666f, 0f, 0f);
            public static readonly Color Tier6 = new Color(1f, 0.333f, 1f);
        }
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
        /// 定义战利品等级
        /// </summary>
        /// <param name="price">价格</param>
        /// <returns></returns>
        public static Color GetColorByPrice(int price)
        {
            if (price >= PriceTier.Tier6) return PriceColor.Tier6;
            if (price >= PriceTier.Tier5) return PriceColor.Tier5;
            if (price >= PriceTier.Tier4) return PriceColor.Tier4;
            if (price >= PriceTier.Tier3) return PriceColor.Tier3;
            if (price >= PriceTier.Tier2) return PriceColor.Tier2;
            if (price >= PriceTier.Tier1) return PriceColor.Tier1;

            return PriceColor.Tier0;
        }
        public static Color GetColorByLevel(int level)
        {
            switch (level)
            {
                case 9: return PriceColor.TierEX;
                case 8: return PriceColor.TierX;
                case 7: return PriceColor.Tier6;
                case 6: return PriceColor.Tier5;
                case 5: return PriceColor.Tier4;
                case 4: return PriceColor.Tier3;
                case 3: return PriceColor.Tier2;
                case 2: return PriceColor.Tier1;
                case 1: return PriceColor.Tier0;
                default: return PriceColor.Tier0;
            }
        }

        public static Dictionary<MongoID, int?> ItemLevelCache = new Dictionary<MongoID, int?>();

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

        public static int GetItemLevel(Item item)
        {
            var template = item.Template;
            if(template == null ) return 0;
            if (template is AmmoTemplate ammoTemplate)
            {
                return GetAmmoLevel(item);
            }
            if(template is AmmoBoxTemplate ammoBoxTemplate)
            {
                var ammoItem = item.GetAllItems().FirstOrDefault(x => x.Template is AmmoTemplate);
                if (ammoItem == null) return 1; // 预防万一有空盒子
                return GetAmmoLevel(ammoItem);
            }
            if(template is BackpackTemplateClass backpackTemplate)
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
            if(template is VestTemplateClass vestTemplate)
            {
                var size = 0;
                vestTemplate.Grids.ExecuteForEach(x => size += (x.GridHeight * x.GridWidth));
                if (size >= 20) return 5;
                if (size >= 16) return 4;
                if (size >= 12) return 3;
                if (size >= 8) return 2;
                if (size >= 0) return 1;
            }
            if (template.QuestItem == true) return 8;
            //坏了, 客户端的ITemTemplate是不完整的
            //if(template.Catr) return 0;
            var price = GetItemPrice(item.TemplateId) ?? 0;
            return GetLevelByPrice(price);
        }

        public static int? GetItemPrice(MongoID itemid)
        {
            PluginsCore.HandbookDict.TryGetValue(itemid, out int itemPrice);
            return itemPrice;
        }
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
        /// 绘制文本
        /// </summary>
        /// <param name="cam">摄像机</param>
        /// <param name="textStyle">样式</param>
        public static void DrawLootText(Camera cam, GUIStyle textStyle)
        {
            if (CachedLootList == null || CachedLootList.Count == 0) return;
                //查找中心
                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                float fovRadius = LootESPCfg.LootESPFovRange.Value;
                //富文本防御, 避免问题
                foreach (LootData loot in CachedLootList)
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
                            //白名单绘制
                            if (loot.Price < LootESPCfg.LootESPFovMinPrice.Value)
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
        /// 扫描协程
        /// </summary>
        /// <returns></returns>
        public static System.Collections.IEnumerator LootScannerCoroutine()
        {
            while (true)
            {
                //等待1秒
                yield return new WaitForSeconds(1f);
                //空值检查和缓存清理
                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null)
                {
                    if (CachedContainers != null)
                    {
                        CachedContainers = null;
                    }
                    continue;
                }
                //两个分开, 因为可能存在LootItem为空的情况, 此时无法清理缓存
                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null || PluginsCore.CorrectGameWorld.LootItems == null)
                {
                    continue;
                }
                //存储扫描结果
                List<LootData> tempLootList = new List<LootData>();
                //读取玩家坐标
                Vector3 playerPos = PluginsCore.CorrectPlayer.Transform.position;
                //配置最大透视距离
                float maxLootDistance = LootESPCfg.LootESPMaxDistance.Value;
                Dictionary<Vector3, int> positionOffsets = new Dictionary<Vector3, int>();
                //遍历战利品
                foreach (var lootItem in PluginsCore.CorrectGameWorld.LootItems.GetValuesEnumerator())
                {
                    if (lootItem == null || lootItem.Item == null || lootItem.gameObject == null) continue;
                    if (!lootItem.gameObject.activeSelf) continue;
                    //熟悉的距离过滤
                    //从PlayerESP调一下单步方法节省开销
                    //他妈的节省不了一点, 我忘记dist有用了草
                    //if (PlayerESP.IsInRange((int)maxLootDistance, playerPos, lootItem.transform.position)) continue;
                    //其实可以, AI牛逼
                    if (!PlayerESP.IsInRange((int)maxLootDistance, playerPos, lootItem.transform.position)) continue;
                    float dist = Vector3.Distance(playerPos, lootItem.transform.position);
                    TryAddLootData(tempLootList, positionOffsets, lootItem.Item, lootItem, null, LootESPCfg.ShowItemFullName.Value ? lootItem.Item.Name.Localized() : lootItem.Item.ShortName.Localized(), lootItem.transform.position, (int)dist);
                    //TryAddLootData(tempLootList, lootItem.Item.TemplateId, lootItem.Item.ShortName.Localized(), lootItem.transform.position, (int)dist);
                }
                //容器透视
                //防御检查
                if (CachedContainers != null)
                {
                    foreach (var container in CachedContainers)
                    {
                        //过滤容器本身
                        if (container?.ItemOwner?.RootItem == null) continue;
                        //距离过滤
                        if (!PlayerESP.IsInRange((int)maxLootDistance, playerPos, container.transform.position)) continue;
                        int dist = Mathf.RoundToInt(Vector3.Distance(playerPos, container.transform.position));
                        //容器名字读取
                        string containerName = GetContainerName(container);
                        //加入缓存
                        foreach (var item in container.ItemOwner.RootItem.GetAllItems())
                        {
                            if (item == container.ItemOwner.RootItem) continue;
                            TryAddLootData(tempLootList, positionOffsets, item, null, container, LootESPCfg.ShowItemFullName.Value ? item.Name.Localized() : item.ShortName.Localized(), container.transform.position, dist, $"[{containerName}]");
                        }
                    }
                }
                //刷新缓存列表
                CachedLootList = tempLootList;
            }
        }

        public static string GetContainerName(LootableContainer container)
        {
            if (container == null) return "地面";
            string containerName = container.ItemOwner.RootItem.ShortName.Localized();
            return string.IsNullOrEmpty(containerName) ? "容器": containerName;
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
            if (itemPrice < minPriceThreshold && itemLevel < filterLevel)
            {
                return;
            }
            //价值格式化
            string priceStr = itemPrice >= 10000 ? (itemPrice / 10000f).ToString("0.#") + "万" : itemPrice.ToString();
            //string priceStr = itemPrice >= 10000 ? (itemPrice / 10000) + "万" : itemPrice.ToString();
            //颜色转码
            Color iColor = GetColorByLevel(itemLevel);
            string hexColor = ColorUtility.ToHtmlStringRGB(iColor);
            //富文本合并
            string fullName = string.IsNullOrEmpty(prefix) ? itemName : $"{prefix} {itemName}";
            string formattedName = $"<color=#{hexColor}>{fullName}</color> <color=#{hexColor}>{priceStr}</color> <color=#FFFF00>{dist}米</color>";
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
                YOffset = currentYOffset // ⭐ 存入算好的偏移量！
            });
        }

        /// <summary>
        /// 画圆方法
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="radius">半径</param>
        /// <param name="color">颜色</param>
        /// <param name="segments">圆的精度(分段数)</param>
        public static void DrawCircle(Vector2 center, float radius, Color color, int segments = 64)
        {
            //画圆
            if (Event.current.type != EventType.Repaint) return;
            //材质初始化
            if (!lineMaterial)
            {
                lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                lineMaterial.hideFlags = HideFlags.HideAndDontSave;
                lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                lineMaterial.SetInt("_ZWrite", 0);
            }
            //启用材质
            lineMaterial.SetPass(0);
            //矩阵绘制(在OnGUI内全局绘制的前面, 此处有End就无问题, 否则会炸掉队列)
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            //绘制
            GL.Begin(GL.LINES);
            GL.Color(color);
            float angleStep = 2f * Mathf.PI / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                float x1 = center.x + Mathf.Cos(angle1) * radius;
                float y1 = center.y + Mathf.Sin(angle1) * radius;
                float x2 = center.x + Mathf.Cos(angle2) * radius;
                float y2 = center.y + Mathf.Sin(angle2) * radius;

                GL.Vertex3(x1, y1, 0);
                GL.Vertex3(x2, y2, 0);
            }
            GL.End();
            GL.PopMatrix();
        }
        /// <summary>
        /// 绘制约束范围
        /// </summary>
        public static void DrawLootFOVCircle()
        {
            //是否可见
            if (!LootESPCfg.ShowLootESPFov.Value) return;
            //查找中心和半径
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float fovRadius = LootESPCfg.LootESPFovRange.Value;
            //绘制
            DrawCircle(screenCenter, fovRadius, new Color(0.8f, 1f, 1f, 0.4f), 64);
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
    public class LootESPCfg
    {
        internal static ConfigEntry<bool> EnableContainerLootESP { get; set; }
        internal static ConfigEntry<bool> EnableLooseLootESP { get; set; }
        internal static ConfigEntry<int> LootESPMaxDistance { get; set; }
        internal static ConfigEntry<int> LootESPMinPrice { get; set; }
        internal static ConfigEntry<bool> EnableLootESPFov { get; set; }
        internal static ConfigEntry<bool> ShowLootESPFov { get; set; }
        internal static ConfigEntry<int> LootESPFovRange { get; set; }
        internal static ConfigEntry<int> LootESPFovMinPrice { get; set; }
        internal static ConfigEntry<bool> ShowItemFullName { get; set; }
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public static void Initialize(ConfigFile config)
        {
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
    }
}