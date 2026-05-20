using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.Ballistics;
using HarmonyLib;
using Newtonsoft.Json;
using Oracle.ESP;
using Oracle.Utils;
using SPT.Common.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Oracle.ESP.PlayerStatusEdit;

namespace Oracle
{

    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class PluginsCore : BaseUnityPlugin
    {
        public static Player CorrectPlayer { get; set; }
        public static string CorrectGroupId { get; set; }
        public static GameWorld CorrectGameWorld { get; set; }

        //绘制样式缓存
        public GUIStyle espTextStyle;

        public Material espMaterial;
        //价格字典定义
        public static Dictionary<string, int> HandbookDict;

        public RenderTexture espRT;
        private byte[] pixelBuffer;
        // [新增] 帧率限制器参数
        // 1f / 30f 代表把 ESP 限制在 30 帧。你可以根据需要改成 40 或 60。
        private float espRefreshRate = 1f / 50f;
        private float lastEspDrawTime = 0f;
        public void Awake()
        {
            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();
            //配置初始化
            PlayerESPCfg.Initialize(Config);
            LootESPCfg.Initialize(Config);
            AimbotCfg.Initialize(Config);
            ItemSpawnerCfg.Initialize(Config);
            PlayerStatusEditCfg.Initialize(Config);
            HotKeyManager.Initialize(Config);
            //价格字典拉取. 初始化
            var rawHandbookData = Oracle.Utils.HandbookClass.GetHandbookData("白昼和黑夜等同吗？义人和罪人等同吗？倘若人生来软弱，弱者们又该从哪位神明处寻求安宁？现在，我赐予各位直视太阳的权利，此时此地，尔等只需静听，此处再无神明，创造乐园的，乃是人之君王！");
            //var handbook = ;
            HandbookDict = JsonConvert.DeserializeObject<Oracle.Utils.HandbookClass.HandbookResponse>(rawHandbookData).Data.Items
                .GroupBy(x => x.Id) //防止原版数据有极其罕见的重复ID导致字典报错
                .ToDictionary(g => g.Key, g => g.First().Price);
            //Console.WriteLine($"我看看怎么个事: {handbook.Data.Categories.FirstOrDefault().Id}");
        }
        public void Start()
        {
            //初始化线条样式
            espMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            espMaterial.hideFlags = HideFlags.HideAndDontSave;
            espMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            espMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            espMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            espMaterial.SetInt("_ZWrite", 0);
            espMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            //初始化文本样式
            espTextStyle = new GUIStyle();
            espTextStyle.fontSize = 12;
            espTextStyle.fontStyle = FontStyle.Bold;
            espTextStyle.alignment = TextAnchor.MiddleCenter;
            espTextStyle.richText = true;
            //拦截
            espRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            espRT.Create();
            // 预分配像素缓冲，彻底消灭 GC (垃圾回收) 带来的掉帧！
            pixelBuffer = new byte[Screen.width * Screen.height * 4];
            // 启动原生纯净覆盖层
            Oracle.ESP.NativeOverlay.Initialize(Screen.width, Screen.height);
            //战利品扫描协程
            StartCoroutine(LootESP.LootScannerCoroutine());
        }
        public void Update()
        {
            //快捷键监听
            HotKeyManager.KeyStatusUpdate();

            ItemCatcher.KeyUpdate();
            bool shouldShow = Application.isFocused && HotKeyManager.UniGUI.Value;
            Oracle.ESP.NativeOverlay.SetVisible(shouldShow);
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
            //空指针防御
            if (CorrectGameWorld == null || CorrectPlayer == null || CorrectGameWorld.AllAlivePlayersList == null) return;
            //只在重绘调用
            if (Event.current.type != EventType.Repaint) return;
            //空指针防御
            Camera cam = Camera.main;
            if (cam == null) return;
            if (Time.time - lastEspDrawTime < espRefreshRate)
            {
                return;
            }
            // 更新最后绘制时间
            lastEspDrawTime = Time.time;
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = espRT;             // 将所有 GUI 绘制转移到我们的纹理上
            GL.Clear(false, true, Color.clear);       // 清空上一帧的画面，保持绝对透明
            //绘制
            //ESP范围
            Oracle.ESP.LootESP.DrawLootFOVCircle();
            Oracle.ESP.Aimbot.DrawAimbotFOVCircle();
            GL.PushMatrix(); 
            //AI说缺了这句, 真的假的?我用着没问题啊?
            //对你奶奶个腿, 计算方式不一样, AI又骗我
            //GL.LoadPixelMatrix();
            espMaterial.SetPass(0);
            //改为画线模式
            //不知道这里能不能改, 那就不改了
            //论屎山是怎么形成的
            GL.Begin(GL.LINES);
            GL.Color(Color.green); // 设定火柴人颜色为绿色
            //玩家透视
            PlayerESP.DrawPlayerBone(cam);
            //结束
            GL.End();
            GL.PopMatrix(); 
            //其他绘制
            PlayerESP.DrawPlayerText(cam, espTextStyle);
            PlayerESP.DrawAllPlayerHealthBars(cam);
            LootESP.DrawLootText(cam, espTextStyle); 
            Oracle.ESP.Aimbot.UpdateTarget(cam);
            Oracle.ESP.Aimbot.DrawTargetLine(cam);
            // 绘制完毕，把焦点还给塔科夫主屏幕
            RenderTexture.active = prevRT;

            // ==========================================
            // 【无损提取】：发起异步显存读取（绝对不掉帧）
            // ==========================================
            UnityEngine.Rendering.AsyncGPUReadback.Request(espRT, 0, TextureFormat.BGRA32, OnReadbackComplete);

        }
        private void OnReadbackComplete(UnityEngine.Rendering.AsyncGPUReadbackRequest req)
        {
            if (req.hasError || pixelBuffer == null) return;

            // 将 GPU 数据拷贝到预分配的缓冲池中 (零额外内存分配)
            req.GetData<byte>().CopyTo(pixelBuffer);

            // 将画面交给外部 Windows 原生悬浮窗
            Oracle.ESP.NativeOverlay.UpdateFrame(pixelBuffer);
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
            PluginsCore.CorrectGroupId = __instance.MainPlayer.Profile?.Info?.GroupId ?? "";
            //挂载脚本
            __instance.MainPlayer.gameObject.AddComponent<PlayerStatusEditComponent>();
            //缓存容器
            Oracle.ESP.LootESP.CachedContainers = UnityEngine.Object.FindObjectsOfType<EFT.Interactive.LootableContainer>();
        }
    }
}
