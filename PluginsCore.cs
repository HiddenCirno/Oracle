using BepInEx;
using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
using Oracle.Combat;
using Oracle.Data;
using Oracle.ESP;
using Oracle.ItemSpawn;
using Oracle.RaidManager;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Oracle.Combat.InfinityStaminaAndNoFallenDamage;
using static Oracle.Data.OracleInterface;

namespace Oracle
{
    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class PluginsCore : BaseUnityPlugin
    {
        public static Player CorrectPlayer { get; set; }
        public static string CorrectGroupId { get; set; }
        public static GameWorld CorrectGameWorld { get; set; }
        public static string dllPath = Assembly.GetExecutingAssembly().Location;
        public static string pluginDir = Path.GetDirectoryName(dllPath);
        //价格字典定义
        public static Dictionary<string, int> HandbookDict;
        //透明图材质和图像流缓存
        public RenderTexture espRT;
        private byte[] pixelBuffer;
        //帧率限制
        private float espRefreshRate = 1f / 50f;
        private float lastEspDrawTime = 0f;
        private static bool isOverlayInitialized = false;
        public void Awake()
        {
            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();
            //配置初始化
            InitializeConfigs(Config);
            //价格字典拉取. 初始化
            var rawHandbookData = Data.HandbookClass.GetHandbookData("白昼和黑夜等同吗？义人和罪人等同吗？倘若人生来软弱，弱者们又该从哪位神明处寻求安宁？现在，我赐予各位直视太阳的权利，此时此地，尔等只需静听，此处再无神明，创造乐园的，乃是人之君王！");
            //var handbook = ;
            HandbookDict = JsonConvert.DeserializeObject<Data.HandbookClass.HandbookResponse>(rawHandbookData).Data.Items
                .GroupBy(x => x.Id) //防止原版数据有极其罕见的重复ID导致字典报错
                .ToDictionary(g => g.Key, g => g.First().Price);
            //Console.WriteLine($"我看看怎么个事: {handbook.Data.Categories.FirstOrDefault().Id}");
        }
        public void Start()
        {
            //拦截
            espRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            espRT.Create();
            //预分配缓存, 高效GC
            pixelBuffer = new byte[Screen.width * Screen.height * 4];
            //启动覆盖层
            NativeOverlay.Initialize(Screen.width, Screen.height);
            //战利品扫描协程
            StartCoroutine(OracleLootManager.LootScannerCoroutine());
            //绊雷扫描协程
            StartCoroutine(OracleTripwireManager.TripwireScannerCoroutine());
            //尸体扫描协程
            StartCoroutine(CorpseESP.CorpseScannerCoroutine());
            InitializeKeyUpdate();
            InitializeEventSubscribe();
            RenderUtils.Initialize();
        }
        public void Update()
        {
            OracleEvent.Update();
            //快捷键监听
            GlobalCfg.KeyUpdate();
            //窗口失焦自动隐藏
            //bool shouldShow = Application.isFocused && HotKeyManager.UniGUI.Value;
            UpdateNativeOverlay();

            //NativeOverlay.SetVisible(shouldShow);
        }
        public static void UpdateNativeOverlay()
        {
            // 1. 获取当前是否【应该启用】叠加层
            // 条件：配置项开启 且 游戏窗口处于聚焦状态
            // 2. 状态机：根据配置动态创建或摧毁
            // 1. 先判断【总开关】：玩家到底用不用过直播功能？
            if (NativeOverlayCfg.EnableNativeOverlay.Value)
            {
                // 如果配置是开启的，但叠加层之前被彻底干掉了，这里才重新初始化（低频操作）
                if (!isOverlayInitialized)
                {
                    NativeOverlay.Initialize(Screen.width, Screen.height);
                    isOverlayInitialized = true;
                }

                // 2. 在总开关开启的前提下，由【游戏聚焦】和【菜单快捷键】共同控制显隐（高频操作，无感隐藏）
                bool shouldShowOverlay = Application.isFocused && GlobalCfg.UniGUI.Value;
                NativeOverlay.SetVisible(shouldShowOverlay);
            }
            else
            {
                // 3. 只有当玩家【彻底取消勾选】了配置项时，才触发摧毁释放句柄（低频操作）
                if (isOverlayInitialized)
                {
                    NativeOverlay.Destroy();
                    isOverlayInitialized = false;
                }
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
            OracleEvent.DrawManagerGUI();
            OracleEvent.DrawCrosshair();
            //空指针防御
            if (CorrectGameWorld == null || CorrectPlayer == null || CorrectGameWorld.AllAlivePlayersList == null) return;
            //只在重绘调用
            if (Event.current.type != EventType.Repaint) return;
            if (!NativeOverlayCfg.EnableNativeOverlay.Value)
            {
                OracleEvent.Draw();
                //DrawESP();
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
            //绘制
            //开始绘制
            OracleEvent.Draw();
            //DrawESP();
            //窗口焦点归位
            RenderTexture.active = prevRT;
            //将图像流异步传输给窗口
            UnityEngine.Rendering.AsyncGPUReadback.Request(espRT, 0, TextureFormat.BGRA32, OnReadbackComplete);

        }
        private void OnReadbackComplete(UnityEngine.Rendering.AsyncGPUReadbackRequest req)
        {
            if (req.hasError || pixelBuffer == null) return;
            //GPU数据传输
            req.GetData<byte>().CopyTo(pixelBuffer);
            //将图像流传输给窗口
            NativeOverlay.UpdateFrame(pixelBuffer);
        }
        private void InitializeConfigs(ConfigFile config)
        {
            // 获取接口的类型
            Type targetInterface = typeof(IOracleCfg);

            // 获取当前运行的 DLL 中的所有类型
            Type[] allTypes = Assembly.GetExecutingAssembly().GetTypes();

            foreach (Type type in allTypes)
            {
                // ⭐ 核心判断：如果这个类型继承了 IOracleConfig，且它本身是个能被实例化的类（不是抽象类或接口本身）
                if (targetInterface.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    try
                    {
                        // 实例化它
                        IOracleCfg configInstance = (IOracleCfg)Activator.CreateInstance(type);

                        // 调用初始化方法
                        configInstance.Initialize(config);

                        Debug.Log($"[Oracle] 成功自动挂载配置模块: {type.Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Oracle] 自动挂载配置模块 {type.Name} 失败: {ex.Message}");
                    }
                }
            }
        }
        private void InitializeKeyUpdate()
        {
            // 获取接口的类型
            Type targetInterface = typeof(IOracleKeyUpdate);

            // 获取当前运行的 DLL 中的所有类型
            Type[] allTypes = Assembly.GetExecutingAssembly().GetTypes();

            foreach (Type type in allTypes)
            {
                // ⭐ 核心判断：如果这个类型继承了 IOracleConfig，且它本身是个能被实例化的类（不是抽象类或接口本身）
                if (targetInterface.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    try
                    {
                        // 实例化它
                        IOracleKeyUpdate configInstance = (IOracleKeyUpdate)Activator.CreateInstance(type);

                        // 调用初始化方法
                        configInstance.RegisterKeyUpdate();

                        Debug.Log($"[Oracle] 成功自动挂载配置模块: {type.Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Oracle] 自动挂载配置模块 {type.Name} 失败: {ex.Message}");
                    }
                }
            }
        }
        private void InitializeEventSubscribe()
        {
            // 获取接口的类型
            Type targetInterface = typeof(IOracleEventSubscribe);

            // 获取当前运行的 DLL 中的所有类型
            Type[] allTypes = Assembly.GetExecutingAssembly().GetTypes();

            foreach (Type type in allTypes)
            {
                // ⭐ 核心判断：如果这个类型继承了 IOracleConfig，且它本身是个能被实例化的类（不是抽象类或接口本身）
                if (targetInterface.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    try
                    {
                        // 实例化它
                        IOracleEventSubscribe configInstance = (IOracleEventSubscribe)Activator.CreateInstance(type);

                        // 调用初始化方法
                        configInstance.SubscribeEvent();

                        Debug.Log($"[Oracle] 成功自动挂载配置模块: {type.Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Oracle] 自动挂载配置模块 {type.Name} 失败: {ex.Message}");
                    }
                }
            }
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
            __instance.MainPlayer.gameObject.AddComponent<PlayerStatusEditComponent>();
            //缓存容器
            OracleLootManager.CachedContainers = UnityEngine.Object.FindObjectsOfType<EFT.Interactive.LootableContainer>();
        }
    }
}
