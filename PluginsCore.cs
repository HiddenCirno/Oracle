using BepInEx;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
using Oracle.Data;
using Oracle.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Oracle.Ability.InfinityStamina;

namespace Oracle
{
    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class PluginsCore : BaseUnityPlugin
    {
        //游戏变量
        public static Player CorrectPlayer { get; set; }
        public static string CorrectGroupId { get; set; }
        public static GameWorld CorrectGameWorld { get; set; }

        //mod路径
        public static string dllPath = Assembly.GetExecutingAssembly().Location;
        public static string pluginDir = Path.GetDirectoryName(dllPath);

        //价格字典定义
        public static Dictionary<string, int> HandbookDict;

        //叠加层材质
        public RenderTexture espRT;

        //帧率限制
        private float espRefreshRate = 1f / 50f;
        private float lastEspDrawTime = 0f;

        public void Awake()
        {
            //Patch所有补丁
            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();

            //配置初始化
            //永远先初始化语言部分
            LocaleManager.Initialize(Config);
            OracleEvent.InitializeConfigs(Config);

            //价格字典拉取. 初始化
            var rawHandbookData = Data.HandbookClass.GetHandbookData("白昼和黑夜等同吗？义人和罪人等同吗？倘若人生来软弱，弱者们又该从哪位神明处寻得安宁？现在，我赐予各位直视太阳的权利，在这十万七千三百三十六座基石上，全能大能的谐乐之弦——为我所用！");
            HandbookDict = JsonConvert.DeserializeObject<Data.HandbookClass.HandbookResponse>(rawHandbookData).Data.Items
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Price);
        }

        public void Start()
        {
            //叠加层材质初始化
            espRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            espRT.Create();
            //启动叠加层
            NativeOverlay.Initialize(Screen.width, Screen.height);

            //战利品扫描协程
            StartCoroutine(OracleLootDataManager.LootScannerCoroutine());
            //绊雷扫描协程
            StartCoroutine(OracleTripwireManager.TripwireScannerCoroutine());
            //尸体扫描协程
            StartCoroutine(OracleCorpseDataManager.CorpseScannerCoroutine());

            //初始化按键监听事件
            OracleEvent.InitializeKeyUpdate();

            //初始化事件订阅
            OracleEvent.InitializeEventSubscribe();

            //初始化绘制样式
            OracleRendering.Initialize();
        }

        public void Update()
        {
            //更新事件
            OracleEvent.Update();
            
            //更新叠加层
            NativeOverlay.UpdateNativeOverlay();
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
        //笑死, 我真的不需要吗?
        //现在就差无敌准星和传送AI了……
        //生成AI不太现实, 太麻烦了, 而且没法做到同步生成
        //所以PvELive里那些生成AI的外挂是咋做到的呢?
        //想不通, DenDevTool也是异步延迟生成的AI
        //也许应该再加一个闪现
        //AI传送和冻结修不好, 不整了
        public void OnGUI()
        {
            //全局绘制开关
            if (!GlobalCfg.UniGUI.Value) return;

            //不受叠加层影响的绘制事件
            OracleEvent.DrawManagerGUI();
            OracleEvent.DrawCrosshair();

            //空指针防御
            if (CorrectGameWorld == null || CorrectPlayer == null || CorrectGameWorld.AllAlivePlayersList == null) return;

            //只在重绘调用
            if (Event.current.type != EventType.Repaint) return;
            if (!NativeOverlayCfg.EnableNativeOverlay.Value)
            {
                //绘制事件
                OracleEvent.Draw();
                return;
            }

            //空指针防御
            Camera cam = Camera.main;
            if (cam == null) return;

            //FPS限制, 仅在配置开启时启用, 能有一定的性能提升
            if (Time.time - lastEspDrawTime < espRefreshRate && GlobalCfg.FPSLimit.Value)
            {
                return;
            }
            lastEspDrawTime = Time.time;

            //将绘制目标设置为自定义纹理而不是主摄像机
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = espRT;
            GL.Clear(false, true, Color.clear);

            //绘制事件
            OracleEvent.Draw();
            //恢复窗口焦点
            RenderTexture.active = prevRT;

            //将图像流异步传输给窗口
            UnityEngine.Rendering.AsyncGPUReadback.Request(espRT, 0, TextureFormat.BGRA32, NativeOverlay.OnReadbackComplete);
        }
    }

    //游戏启动Patch, 用于捕获关键实例
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
            __instance.MainPlayer.gameObject.AddComponent<InfinityStaminaComponent>();

            //缓存容器
            OracleLootDataManager.CachedContainers = UnityEngine.Object.FindObjectsOfType<EFT.Interactive.LootableContainer>();
        }
    }
}
