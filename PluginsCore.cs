using BepInEx;
using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using Oracle.ESP;
using SPT.Common.Http;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using Oracle.Utils;
using System.Linq;

namespace Oracle
{

    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class PluginsCore : BaseUnityPlugin
    {
        public static Player CorrectPlayer { get; set; }
        public static GameWorld CorrectGameWorld { get; set; }

        // ⭐ 新增：缓存 GUI 文本样式，拒绝 GC 卡顿
        public GUIStyle espTextStyle;

        public Material espMaterial;

        public Dictionary<string, int> HandbookDict;

        public void Awake()
        {
            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();
            PlayerESPCfg.Initialize(Config);
            var rawHandbookData = Oracle.Utils.HandbookClass.GetHandbookData("白昼和黑夜等同吗？义人和罪人等同吗？倘若人生来软弱，弱者们又该从哪位神明处寻求安宁？现在，我赐予各位直视太阳的权利，此时此地，尔等只需静听，此处再无神明，创造乐园的，乃是人之君王！");
            //var handbook = ;
            HandbookDict = JsonConvert.DeserializeObject<Oracle.Utils.HandbookClass.HandbookResponse>(rawHandbookData).Data.Items
                .GroupBy(x => x.Id) // 防止原版数据有极其罕见的重复ID导致字典报错
                .ToDictionary(g => g.Key, g => g.First().Price);
            //Console.WriteLine($"我看看怎么个事: {handbook.Data.Categories.FirstOrDefault().Id}");
        }
        public void Start()
        {
            // 初始化一段极其基础的着色器（Shader）材质，允许我们画出不受光照影响的纯色线条或色块
            espMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            espMaterial.hideFlags = HideFlags.HideAndDontSave;
            espMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            espMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            espMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            espMaterial.SetInt("_ZWrite", 0);
            espMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

            // ⭐ 新增：初始化文字样式
            espTextStyle = new GUIStyle();
            espTextStyle.fontSize = 12;
            espTextStyle.fontStyle = FontStyle.Bold;
            espTextStyle.alignment = TextAnchor.MiddleCenter;

            StartCoroutine(LootScannerCoroutine());
        }
        
        private System.Collections.IEnumerator LootScannerCoroutine()
        {
            while (true)
            {
                // 等待 1 秒
                yield return new WaitForSeconds(1f);

                // 如果没进战局，就跳过这次扫描
                // 如果没进战局 (或者已经撤离回到了主菜单)
                if (CorrectGameWorld == null || CorrectPlayer == null)
                {
                    // ⭐ 顺手把上一局的静态容器缓存清空，防止内存泄漏和空指针
                    if (Oracle.ESP.LootESP.CachedContainers != null)
                    {
                        Oracle.ESP.LootESP.CachedContainers = null;
                    }
                    continue; // 继续休眠等待下一局
                }
                if (CorrectGameWorld == null || CorrectPlayer == null || CorrectGameWorld.LootItems == null)
                {
                    continue;
                }

                // 创建一个临时列表来存放这次扫描的结果
                List<Oracle.ESP.LootData> tempLootList = new List<Oracle.ESP.LootData>();

                // 获取玩家当前坐标，用于计算距离
                Vector3 playerPos = CorrectPlayer.Transform.position;

                // 假设这是你未来从配置文件读取的最大物资透视距离
                float maxLootDistance = 100f;

                // 遍历塔科夫原生的物资列表 (GameWorld.LootList / LootItems)
                // 注意：具体的集合名称可能因 SPT 版本而异，用 VS 智能提示点出来
                foreach (var lootItem in CorrectGameWorld.LootItems.GetValuesEnumerator())
                {
                    if (lootItem == null || lootItem.Item == null || lootItem.gameObject == null) continue;

                    // 1. 距离过滤 (最快，先做)
                    float dist = Vector3.Distance(playerPos, lootItem.transform.position);
                    if (dist > maxLootDistance) continue; // maxLootDistance 可以绑定到你的 BepInEx 配置

                    // 2. ⭐ 提取键值 (根据你字典是用 TemplateId 还是 Name 存的，这里以 TemplateId 为例，最准)
                    string itemKey = lootItem.Item.TemplateId;
                    // 如果你的字典存的是名字，就换成 lootItem.Item.ShortName.Localized()

                    // 3. ⭐ 价格查询与过滤 (使用 TryGetValue 极其重要！)
                    int itemPrice = 0;
                    // 如果字典里有这个物品，就把价格赋给 itemPrice；如果没有，默认就是 0，绝不报错
                    if (HandbookDict.TryGetValue(itemKey, out int cachedPrice))
                    {
                        itemPrice = cachedPrice;
                    }

                    // 假设这是你 BepInEx 里设置的最低显示价格，比如 10000 卢布
                    // 如果连这个价格都不到，直接丢弃，根本不送去渲染！
                    if (itemPrice < 10000) continue;

                    // 4. 组装高级数据
                    string itemName = lootItem.Item.ShortName.Localized();

                    tempLootList.Add(new Oracle.ESP.LootData
                    {
                        Position = lootItem.transform.position,
                        Name = itemName,
                        Distance = Mathf.RoundToInt(dist),
                        Price = itemPrice,
                        ItemColor = LootESP.GetColorByPrice(itemPrice) // 动态分配颜色
                    });
                }
                // 确保 Patch 已经成功抓取到了全图容器
                if (Oracle.ESP.LootESP.CachedContainers != null)
                {
                    // 遍历我们存好的静态容器数组
                    foreach (var container in Oracle.ESP.LootESP.CachedContainers)
                    {
                        // 防空检查（有些容器可能在游戏中被特殊机制销毁）
                        if (container == null || container.ItemOwner == null || container.ItemOwner.RootItem == null) continue;

                        // 距离过滤
                        float dist = Vector3.Distance(playerPos, container.transform.position);
                        if (dist > maxLootDistance) continue;

                        // 拿到容器里的所有物品 (深层递归)
                        var itemsInside = container.ItemOwner.RootItem.GetAllItems();
                        string containerRealName = container.ItemOwner.RootItem.ShortName.Localized();

                        // 兜底防空：万一某些奇葩容器没名字，给个默认值
                        if (string.IsNullOrEmpty(containerRealName))
                        {
                            containerRealName = "容器";
                        }
                        // 遍历容器里的物品
                        foreach (var item in itemsInside)
                        {
                            // 排除容器本身的那个“壳子”
                            if (item == container.ItemOwner.RootItem) continue;

                            string itemKey = item.TemplateId; // 确保这和你字典的 Key 对得上

                            // 用你做好的字典查价格
                            if (HandbookDict.TryGetValue(itemKey, out int itemPrice))
                            {
                                // 容器透视的阈值建议设高一点（比如只看 10 万以上的），不然普通的箱子全在头上飘字会很挡视野
                                if (itemPrice >= 100000)
                                {
                                    // 直接塞进同一个临时列表里！
                                    tempLootList.Add(new Oracle.ESP.LootData
                                    {
                                        Position = container.transform.position, // 使用容器的物理 3D 坐标
                                        Name = $"[{containerRealName}] {item.ShortName.Localized()}", // 加个前缀方便辨认
                                        Distance = Mathf.RoundToInt(dist),
                                        Price = itemPrice,
                                        ItemColor = LootESP.GetColorByPrice(itemPrice)
                                    });
                                }
                            }
                        }
                    }
                }
                // ⭐ 原子级替换：用新的列表覆盖全局缓存，供 OnGUI 瞬间读取
                Oracle.ESP.LootESP.CachedLootList = tempLootList;
            }
        }
        //文本绘制
        //然后是遮挡检测射线检测和配置拆分
        //物品透视....想想就难搞
        //再说吧, 可能会先搞aimbot
        //毕竟物品价值也是个问题, 纯客户端绝对绝对不可能拿到物品价格
        //除非注入原版路由
        //再议
        //已经搞定, 现在透视有了就差自瞄和无后座了, 我其实就需要这几个功能, 尸体透视也不需要, 简单的透视自瞄搞定
        //拆分配置, 规划fov透视, 修改调整
        //现在是2026年3月18日, 18:03分, 开工!
        public void OnGUI()
        {
            if (CorrectGameWorld == null || CorrectPlayer == null || CorrectGameWorld.AllAlivePlayersList == null) return;

            // ⭐ 核心锁：只在重绘阶段调用，杜绝 GC 和延迟
            if (Event.current.type != EventType.Repaint) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            GL.PushMatrix();
            espMaterial.SetPass(0);
            // 改为画线模式
            GL.Begin(GL.LINES);
            GL.Color(Color.green); // 设定火柴人颜色为绿色

            PlayerESP.DrawPlayerBone(cam);

            GL.End();
            GL.PopMatrix(); 

            PlayerESP.DrawPlayerText(cam, espTextStyle);

            LootESP.DrawLootText(cam, espTextStyle);
        }

        
    }
    [HarmonyPatch(typeof(GameWorld), "OnGameStarted")]
    public class GameStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameWorld __instance)
        {
            PluginsCore.CorrectGameWorld = __instance;
            PluginsCore.CorrectPlayer = __instance.MainPlayer;
            Oracle.ESP.LootESP.CachedContainers = UnityEngine.Object.FindObjectsOfType<EFT.Interactive.LootableContainer>();
            //Console.WriteLine($"调试信息: {__instance.MainPlayer.gameObject.transform.localPosition.ToString()}");
            //Console.WriteLine($"调试信息: {__instance.MainPlayer.PlayerBones}");
        }
    }
}
