using BepInEx;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
using Oracle.Combat;
using Oracle.ESP;
using Oracle.ItemSpawn;
using Oracle.RaidManager;
using Oracle.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Oracle.Combat.InfinityStaminaAndNoFallenDamage;

namespace Oracle
{
    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class PluginsCore : BaseUnityPlugin
    {
        public static Player CorrectPlayer { get; set; }
        public static string CorrectGroupId { get; set; }
        public static GameWorld CorrectGameWorld { get; set; }

        private ItemManagerGUI _itemManagerGUI = new ItemManagerGUI();
        private AIManagerGUI _aiManagerGUI = new AIManagerGUI();
        private LootManagerGUI _lootManagerGUI = new LootManagerGUI();
        private BotGeneratorGUI _botGeneratorGUI = new BotGeneratorGUI();
        //绘制样式缓存
        public GUIStyle espTextStyle;
        public Material espMaterial;
        //价格字典定义
        public static Dictionary<string, int> HandbookDict;
        //透明图材质和图像流缓存
        public RenderTexture espRT;
        private byte[] pixelBuffer;
        //帧率限制
        private float espRefreshRate = 1f / 50f;
        private float lastEspDrawTime = 0f;
        private bool isOverlayInitialized = false;
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
            InfiniteAmmoCfg.Initialize(Config);
            GhostModeCfg.Initialize(Config);    
            HotKeyManager.Initialize(Config);
            NativeOverlayCfg.Initialize(Config);
            //价格字典拉取. 初始化
            var rawHandbookData = Tools.HandbookClass.GetHandbookData("白昼和黑夜等同吗？义人和罪人等同吗？倘若人生来软弱，弱者们又该从哪位神明处寻求安宁？现在，我赐予各位直视太阳的权利，此时此地，尔等只需静听，此处再无神明，创造乐园的，乃是人之君王！");
            //var handbook = ;
            HandbookDict = JsonConvert.DeserializeObject<Tools.HandbookClass.HandbookResponse>(rawHandbookData).Data.Items
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
            //预分配缓存, 高效GC
            pixelBuffer = new byte[Screen.width * Screen.height * 4];
            //启动覆盖层
            NativeOverlay.Initialize(Screen.width, Screen.height);
            //战利品扫描协程
            StartCoroutine(LootESP.LootScannerCoroutine());
            //绊雷扫描协程
            StartCoroutine(PlayerESP.TripwireScannerCoroutine());
        }
        public void Update()
        {
            //快捷键监听
            HotKeyManager.KeyStatusUpdate();
            ItemCatcher.KeyUpdate();
            _itemManagerGUI.Update();
            _aiManagerGUI.Update();
            _lootManagerGUI.Update();
            _botGeneratorGUI.Update();
            //窗口失焦自动隐藏
            //bool shouldShow = Application.isFocused && HotKeyManager.UniGUI.Value;
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
                bool shouldShowOverlay = Application.isFocused && HotKeyManager.UniGUI.Value;
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

            //NativeOverlay.SetVisible(shouldShow);
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
        public void OnGUI()
        {
            //全局绘制开关
            if (!HotKeyManager.UniGUI.Value) return;
            _itemManagerGUI.OnGUI();
            //空指针防御
            if (CorrectGameWorld == null || CorrectPlayer == null || CorrectGameWorld.AllAlivePlayersList == null) return;
            _aiManagerGUI.OnGUI();
            _lootManagerGUI.OnGUI();
            _botGeneratorGUI.OnGUI();
            //只在重绘调用
            if (Event.current.type != EventType.Repaint) return;
            if (!NativeOverlayCfg.EnableNativeOverlay.Value)
            {
                DrawESP();
                return;
            }
            //空指针防御
            Camera cam = Camera.main;
            if (cam == null) return;
            //FPS限制, 仅在配置开启时启用, 能有一定的性能提升
            if (Time.time - lastEspDrawTime < espRefreshRate && HotKeyManager.FPSLimit.Value)
            {
                return;
            }
            lastEspDrawTime = Time.time;
            //将绘制目标设置为自定义纹理而不是主摄像机
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = espRT;
            GL.Clear(false, true, Color.clear);
            //绘制
            //ESP范围
            LootESP.DrawLootFOVCircle();
            Aimbot.DrawAimbotFOVCircle();
            //开始绘制
            DrawESP();
            //窗口焦点归位
            RenderTexture.active = prevRT;
            //将图像流异步传输给窗口
            UnityEngine.Rendering.AsyncGPUReadback.Request(espRT, 0, TextureFormat.BGRA32, OnReadbackComplete);

        }
        private void DrawESP()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            // 传统的直接绘制（不切换 RenderTexture，直接画在屏幕上）
            // 注意：如果是直接 GL 绘制，需要放到 EventType.Repaint 判定后面
            //if (Event.current.type != EventType.Repaint) return;

            //GL.Clear(false, true, Color.clear);
            //绘制
            //ESP范围
            LootESP.DrawLootFOVCircle();
            Aimbot.DrawAimbotFOVCircle();
            //开始绘制
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
            PlayerESP.DrawTripwireESP(cam, espTextStyle, espMaterial);
            LootESP.DrawLootText(cam, espTextStyle);
            Aimbot.UpdateTarget(cam);
            Aimbot.DrawTargetLine(cam);
        }

        private void OnReadbackComplete(UnityEngine.Rendering.AsyncGPUReadbackRequest req)
        {
            if (req.hasError || pixelBuffer == null) return;
            //GPU数据传输
            req.GetData<byte>().CopyTo(pixelBuffer);
            //将图像流传输给窗口
            NativeOverlay.UpdateFrame(pixelBuffer);
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
            LootESP.CachedContainers = Object.FindObjectsOfType<EFT.Interactive.LootableContainer>();
        }
    }
}
