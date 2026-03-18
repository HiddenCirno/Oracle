using BepInEx;
using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using System;
using UnityEngine;
using Oracle.ESP;

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
            PlayerESP.PlayerESPCfg.Initialize(Config);
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
            espTextStyle.fontSize = 14;
            espTextStyle.fontStyle = FontStyle.Bold;
            espTextStyle.alignment = TextAnchor.MiddleCenter;
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
