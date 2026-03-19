using BepInEx.Configuration;
using EFT.Interactive;
using System.Collections.Generic;
using UnityEngine;

namespace Oracle.ESP
{
    //战利品数据缓存
    public struct LootData
    {
        public Vector3 Position;
        public string Name;
        public int Distance;
        public int Price;        // ⭐ 新增：存储价格
        public Color ItemColor;  // ⭐ 新增：存储这个物品该用什么颜色画
        public int YOffset; // ⭐ 新增：记录这个文本需要向下偏移多少像素
    }
    public class LootESP
    {
        //约束范围
        private static Material lineMaterial;
        //全局缓存list, 唯一
        public static List<LootData> CachedLootList = new List<LootData>();
        //全局容器缓存, 唯一
        public static EFT.Interactive.LootableContainer[] CachedContainers;
        //物品价值分级
        public static Color GetColorByPrice(int price)
        {
            //价格区间
            if (price >= 500000) return new Color(1f, 0.333f, 1f);
            if (price >= 200000) return new Color(0.666f, 0f, 0f);
            if (price >= 100000) return new Color(1f, 0.666f, 0f);
            if (price >= 50000) return new Color(0.666f, 0f, 0.666f);
            if (price >= 20000) return new Color(0f, 0.627f, 1f);
            if (price >= 10000) return new Color(0f, 0.666f, 0f);
            return Color.white;
        }
        //绘制方法
        public static void DrawLootText(Camera cam, GUIStyle textStyle)
        {
            if (CachedLootList == null || CachedLootList.Count == 0 || !LootESPCfg.EnableLootESP.Value) return;
            //查找中心
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float fovRadius = LootESPCfg.LootESPFovRange.Value;
            //富文本防御, 避免问题
            textStyle.richText = true;
            textStyle.normal.textColor = Color.white;
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
                    // 绘制
                    GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), espText, textStyle);
                }
            }
        }
        //扫描
        public static System.Collections.IEnumerator LootScannerCoroutine()
        {
            while (true)
            {
                //等待1秒
                yield return new WaitForSeconds(1f);
                //空值检查和缓存清理
                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null)
                {
                    if (Oracle.ESP.LootESP.CachedContainers != null)
                    {
                        Oracle.ESP.LootESP.CachedContainers = null;
                    }
                    continue;
                }
                //两个分开, 因为可能存在LootItem为空的情况, 此时无法清理缓存
                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null || PluginsCore.CorrectGameWorld.LootItems == null)
                {
                    continue;
                }
                //存储扫描结果
                List<Oracle.ESP.LootData> tempLootList = new List<Oracle.ESP.LootData>();
                //读取玩家坐标
                Vector3 playerPos = PluginsCore.CorrectPlayer.Transform.position;
                //配置最大透视距离
                float maxLootDistance = LootESPCfg.LootESPMaxDistance.Value;
                Dictionary<Vector3, int> positionOffsets = new Dictionary<Vector3, int>();
                //遍历战利品
                foreach (var lootItem in PluginsCore.CorrectGameWorld.LootItems.GetValuesEnumerator())
                {
                    if (lootItem == null || lootItem.Item == null || lootItem.gameObject == null) continue;
                    //熟悉的距离过滤
                    //从PlayerESP调一下单步方法节省开销
                    //他妈的节省不了一点, 我忘记dist有用了草
                    //if (PlayerESP.IsInRange((int)maxLootDistance, playerPos, lootItem.transform.position)) continue;
                    //其实可以, AI牛逼
                    if (!PlayerESP.IsInRange((int)maxLootDistance, playerPos, lootItem.transform.position) || !LootESPCfg.EnableLooseLootESP.Value) continue;
                    float dist = Vector3.Distance(playerPos, lootItem.transform.position);
                    TryAddLootData(tempLootList, positionOffsets, lootItem.Item.TemplateId, LootESPCfg.ShowItemFullName.Value ? lootItem.Item.Name.Localized() : lootItem.Item.ShortName.Localized(), lootItem.transform.position, (int)dist);
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
                        string containerName = container.ItemOwner.RootItem.ShortName.Localized();
                        if (string.IsNullOrEmpty(containerName)) containerName = "容器";
                        //加入缓存
                        foreach (var item in container.ItemOwner.RootItem.GetAllItems())
                        {
                            if (item == container.ItemOwner.RootItem || !LootESPCfg.EnableContainerLootESP.Value) continue;
                            TryAddLootData(tempLootList, positionOffsets, item.TemplateId, LootESPCfg.ShowItemFullName.Value ? item.Name.Localized() : item.ShortName.Localized(), container.transform.position, dist, $"[{containerName}]");
                        }
                    }
                }
                //刷新缓存列表
                Oracle.ESP.LootESP.CachedLootList = tempLootList;
            }
        }
        private static void TryAddLootData(List<LootData> targetList, Dictionary<Vector3, int> offsetDict, string itemKey, string itemName, Vector3 pos, int dist, string prefix = "")
        {
            //字典O(1)查价
            if (!PluginsCore.HandbookDict.TryGetValue(itemKey, out int itemPrice)) return;
            //价值过滤
            int minPriceThreshold = LootESPCfg.LootESPMinPrice.Value;
            if (itemPrice < minPriceThreshold || itemKey == "55d7217a4bdc2d86028b456d") return;
            //价值格式化
            string priceStr = itemPrice >= 10000 ? (itemPrice / 10000) + "万" : itemPrice.ToString();
            //颜色转码
            Color iColor = GetColorByPrice(itemPrice);
            string hexColor = ColorUtility.ToHtmlStringRGB(iColor);
            //富文本合并
            string fullName = string.IsNullOrEmpty(prefix) ? itemName : $"{prefix} {itemName}";
            string richName = $"<color=#{hexColor}>{fullName}</color>";
            string richDist = $"<color=#FFFF00>{dist}米</color>";
            string richPrice = $"<color=#{hexColor}>{priceStr}</color>";
            string formattedName = $"{richName} {richPrice} {richDist}";
            if (!offsetDict.ContainsKey(pos))
            {
                offsetDict[pos] = 0;
            }
            //坐标偏移计算
            int currentYOffset = offsetDict[pos];
            offsetDict[pos] += 20;
            //生成数据
            targetList.Add(new LootData
            {
                Position = pos,
                Name = formattedName,
                Distance = dist,
                Price = itemPrice,
                ItemColor = GetColorByPrice(itemPrice),
                YOffset = currentYOffset // ⭐ 存入算好的偏移量！
            });
        }
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
        //画圆方法
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
    public class LootESPCfg
    {
        internal static ConfigEntry<bool> EnableLootESP { get; set; }
        internal static ConfigEntry<bool> EnableContainerLootESP { get; set; }
        internal static ConfigEntry<bool> EnableLooseLootESP { get; set; }
        internal static ConfigEntry<int> LootESPMaxDistance { get; set; }
        internal static ConfigEntry<int> LootESPMinPrice { get; set; }
        internal static ConfigEntry<bool> EnableLootESPFov { get; set; }
        internal static ConfigEntry<bool> ShowLootESPFov { get; set; }
        internal static ConfigEntry<int> LootESPFovRange { get; set; }
        internal static ConfigEntry<int> LootESPFovMinPrice { get; set; }
        internal static ConfigEntry<bool> ShowItemFullName { get; set; }
        public static void Initialize(ConfigFile config)
        {
            EnableLootESP = config.Bind<bool>(
                "物资透视",
                "启用物资透视",
                true,
                "物资透视总开关"
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
            LootESPFovMinPrice = config.Bind<int>(
                "物资透视",
                "约束透视白名单价格",
                150000,
                new ConfigDescription(
                    "显示在约束范围外的物品最低价格",
                    new AcceptableValueRange<int>(1000, 1000000)
                )
            );
        }
    }
}