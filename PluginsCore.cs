using BepInEx;
using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using Oracle.ESP;
using System;
using System.Collections.Generic;
using UnityEngine;

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

        public void Awake()
        {
            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();
            PlayerESPCfg.Initialize(Config);
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
                    // 过滤掉已经被捡走的、无效的空对象或者未启用的战利品
                    if (lootItem == null || lootItem.gameObject == null || lootItem.gameObject.activeSelf == false) continue;

                    // 尽早进行距离校验，避免对全图 3000 个物品做无效处理
                    float dist = Vector3.Distance(playerPos, lootItem.transform.position);
                    if (dist > maxLootDistance) continue;

                    // 提取物品名字 (SPT/EFT 源码里，物品名字的获取路径可能比较深)
                    // 通常在 lootItem.Item.Name 或者 lootItem.Name，先用最简单的顶住
                    string itemName = lootItem.Item != null ? lootItem.Item.ShortName.Localized() : lootItem.Name;

                    // 把符合条件的数据塞进临时列表
                    tempLootList.Add(new Oracle.ESP.LootData
                    {
                        Position = lootItem.transform.position,
                        Name = itemName,
                        Distance = Mathf.RoundToInt(dist)
                    });
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
            Console.WriteLine($"调试信息: {__instance.MainPlayer.gameObject.transform.localPosition.ToString()}");
            Console.WriteLine($"调试信息: {__instance.MainPlayer.PlayerBones}");
        }
    }
}
