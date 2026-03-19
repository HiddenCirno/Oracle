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
using EFT.Ballistics;

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

        public static Dictionary<string, int> HandbookDict;

        public void Awake()
        {
            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();
            PlayerESPCfg.Initialize(Config);
            LootESPCfg.Initialize(Config);
            AimbotCfg.Initialize(Config);
            HotKeyManager.Initialize(Config);
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
            espTextStyle.richText = true;

            StartCoroutine(LootESP.LootScannerCoroutine());
        }
        public void Update()
        {
            HotKeyManager.KeyStatusUpdate();
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
            //全局绘制开关
            if (!HotKeyManager.UniGUI.Value) return;
            //ESP范围
            Oracle.ESP.LootESP.DrawLootFOVCircle(); 
            Oracle.ESP.Aimbot.DrawAimbotFOVCircle();
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
            PlayerESP.DrawAllPlayerHealthBars(cam);

            LootESP.DrawLootText(cam, espTextStyle); 
            Oracle.ESP.Aimbot.UpdateTarget(cam);
            Oracle.ESP.Aimbot.DrawTargetLine(cam);

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
