using BepInEx.Configuration;
using Diz.LanguageExtensions;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using JetBrains.Annotations;
using Oracle.Data;
using Oracle.ESP;
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Oracle.Data.OracleInterface;

namespace Oracle.RaidManager
{
    public class AIManagerGUI
    {
        // UI 状态
        public static bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(480, 20, 550, 650); // 默认在物品管理器右侧
        public Vector2 _scrollPos;

        // --- 头像异步缓存池 ---
        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();
        // 用于存储正在后台渲染中的头像请求
        public Dictionary<string, GClass929> _pendingIcons = new Dictionary<string, GClass929>();

        public void OnGUI()
        {
            if (!_isMenuOpen) return;

            UIStyleManager.EnsureInitialized();

            GUI.backgroundColor = Color.white;

            //_windowRect = GUI.Window(8849, _windowRect, DrawWindow, "系统指令 - 战局实体管理器 (按 F9 隐藏)", UIStyleManager.WindowStyle);
        }

        public void DrawPanel()
        {
            // ---- 右上角区域 ----
            // 1. 全歼按钮 (放在关闭按钮左侧)
            if (GUI.Button(new Rect(_windowRect.width - 135, 4, 85, 20), "全部杀死", UIStyleManager.RedButtonStyle))
            {
                if (PluginsCore.CorrectGameWorld != null && PluginsCore.CorrectGameWorld.AllAlivePlayersList != null)
                {
                    foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
                    {
                        // 过滤：排除自己、空指针、已死者和队友
                        if (player == null || player == PluginsCore.CorrectPlayer || !player.HealthController.IsAlive) continue;
                        string targetGroupId = player.Profile?.Info?.GroupId ?? "";
                        if (!string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && targetGroupId == PluginsCore.CorrectGroupId) continue;

                        // 执行处决
                        player.KillMe(EBodyPartColliderType.HeadCommon, 999999999);
                        //防止无敌
                        player?.OnDead(EDamageType.Environment);
                    }
                }
            }

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            GUILayout.Space(10);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            // 防御：确保游戏世界和玩家列表已加载
            if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectGameWorld.AllAlivePlayersList == null)
            {
                GUILayout.Label("未进入战局或 AI 列表未初始化。", UIStyleManager.BoxStyle);
            }
            else
            {
                int aliveCount = 0;

                // 遍历当前存活的所有实体
                foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
                {
                    // 过滤逻辑保持不变...
                    if (player == null || player == PluginsCore.CorrectPlayer || !player.HealthController.IsAlive) continue;
                    var info = player.Profile?.Info;
                    if (info == null) continue;
                    string targetGroupId = info?.GroupId ?? "";
                    bool isTeammate = OraclePlayerDataManager.IsTeammate(info);
                    if (isTeammate) continue;

                    aliveCount++;

                    // --- 调用新的重构逻辑 ---
                    var entityInfo = OraclePlayerDataManager.GetEntityInfo(player, isTeammate, false);

                    // --- 绘制 ---
                    GUILayout.BeginHorizontal(UIStyleManager.BoxStyle);

                    // 头像绘制逻辑不变
                    Texture2D icon = GetPlayerIcon(player);
                    if (icon != null)
                    {
                        GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                    }
                    else
                    {
                        GUILayout.Box("生成中", UIStyleManager.NormalButtonStyle, GUILayout.Width(64), GUILayout.Height(64));
                    }

                    // --- 实体信息绘制 ---
                    GUILayout.BeginVertical();
                    // 使用新的结构体字段
                    GUILayout.Label($"<b>{entityInfo.Name}</b>  {entityInfo.LevelText}");
                    GUILayout.Label($"<color=grey>{entityInfo.SideText} | 距离: <color=#FFFF00>{entityInfo.Distance} 米</color></color>");
                    GUILayout.EndVertical();

                    // --- 操作按钮区域 (上下平分 64 的高度) ---
                    GUILayout.BeginVertical(GUILayout.Width(130)); // 加宽以容纳左右两个按钮

                    BotOwner botOwner = player.AIData?.BotOwner;

                    // 第一行：传送 + 搜索
                    GUILayout.BeginHorizontal();

                    // 1. 传送按钮
                    if (GUILayout.Button("传送", UIStyleManager.BlueButtonStyle, GUILayout.Height(30), GUILayout.MinWidth(60)))
                    {
                        TeleportBotToMe(player);
                    }

                    // 2. 搜身按钮
                    if (GUILayout.Button("搜索", UIStyleManager.BlueButtonStyle, GUILayout.Height(30), GUILayout.MinWidth(60)))
                    {
                        RemoteSearchPlayer(player);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(4); // 上下间距

                    // 第二行：冻结 + 杀死
                    GUILayout.BeginHorizontal();

                    // 3. 冻结按钮 (判断状态)
                    //bool isFrozen = botOwner != null && botOwner.BotState == EBotState.ActiveFail;
                    //string freezeText = isFrozen ? "解冻" : "冻结";
                    //GUIStyle freezeStyle = "isFrozen" ? UIStyleManager.RedButtonStyle : UIStyleManager.BlueButtonStyle;

                    // 如果对方是真玩家 (botOwner == null)，为了美观保持按钮置灰
                    GUI.enabled = false;
                    //直接禁用按钮得了
                    if (GUILayout.Button("冻结", UIStyleManager.BlueButtonStyle, GUILayout.Height(30), GUILayout.MinWidth(60)))
                    {
                        ToggleFreezeBot(botOwner);
                    }
                    GUI.enabled = true;

                    // 4. 杀死按钮
                    if (GUILayout.Button("杀死", UIStyleManager.RedButtonStyle, GUILayout.Height(30), GUILayout.MinWidth(60)))
                    {
                        player.KillMe(EBodyPartColliderType.HeadCommon, 999999999);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }

                if (aliveCount == 0)
                {
                    GUILayout.Label("当前战局中没有可用的非友军实体。", UIStyleManager.BoxStyle);
                }
            }

            GUILayout.EndScrollView();

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;
        }

        // ==========================================
        // ⭐ 新增：将实体传送到面前
        // ==========================================
        private void TeleportBotToMe(Player targetPlayer)
        {
            Player mainPlayer = PluginsCore.CorrectPlayer;
            if (mainPlayer == null || targetPlayer == null) return;

            // 1. 计算坐标：自己正前方 2 米，稍微抬高一点防止卡地皮
            Vector3 targetPos = mainPlayer.Position + mainPlayer.Transform.forward * 2f;
            targetPos.y += 0.2f;

            // 2. 直接调用塔科夫原生的安全传送方法
            // 第二个参数 onServerToo 传 false 即可，SPT环境下单机生效
            targetPlayer.Teleport(targetPos, true);

            NotificationManagerClass.DisplayMessageNotification($"已将 {targetPlayer.Profile?.Nickname} 传送到面前！");
        }

        // ==========================================
        // ⭐ 新增：冻结 / 解冻 AI 逻辑
        // ==========================================
        private void ToggleFreezeBot(BotOwner botOwner)
        {
            if (botOwner.BotState == EBotState.ActiveFail)
            {
                ResumeBot(botOwner);
                NotificationManagerClass.DisplayMessageNotification("AI 已解冻。");
            }
            else
            {
                PauseBot(botOwner);
                NotificationManagerClass.DisplayMessageNotification("AI 已冻结，变成植物人靶子。");
            }
        }

        /// <summary>
        /// 纯净版冻结：利用 ActiveFail 漏洞彻底瘫痪大脑
        /// </summary>
        public void PauseBot(BotOwner bot)
        {
            if (bot == null || bot.BotState != EBotState.Active) return;

            // 1. 必须先踩一脚死刹车，并清空当前目标。
            // 如果不踩刹车，虽然大脑死了，但 Unity 底层的寻路组件(NavMeshAgent)还会因为惯性走到上一个终点。
            bot.StopMove();
            bot.MovementPause(99999f); // 物理锁死移动意图
            bot.AimingManager.CurrentAiming?.LoseTarget();
            bot.Memory.GoalEnemy = null;

            // 2. 卡系统的 Bug：设为 ActiveFail
            // 这样 UpdateManual() 会被彻底跳过，且游戏后台的自动激活机制也会无视它。
            // 它的肉体依然有呼吸，中枪依然会判定死亡掉落，但永远不会还击和索敌。
            bot.BotState = EBotState.ActiveFail;
        }

        /// <summary>
        /// 纯净版解冻：恢复激活
        /// </summary>
        public void ResumeBot(BotOwner bot)
        {
            if (bot == null || bot.BotState != EBotState.ActiveFail) return;

            // 解除物理移动限制
            bot.MovementResume();

            // 直接切回 Active，它的 UpdateManual 会在下一帧无缝接管，重新开始找掩体和索敌
            bot.BotState = EBotState.Active;
        }

        [HarmonyPatch(typeof(GClass2234), "TryFindChangedContainer")]
        public class TryFindChangedContainerPatch
        {
            // ⭐ 修复点1：去掉 __instance，因为这是静态方法！
            // ⭐ 修复点2：必须传入 (Item item, out Error error) 来完美对齐原方法的签名
            public static void Postfix(ItemAddress address, [CanBeNull] out GClass1802 changedContainer, ref bool __result)
            {
                changedContainer = null;
                __result = false;
            }
        }

        // ==========================================
        // ⭐ 核心逻辑：隔空活体搜身
        // ==========================================
        private void RemoteSearchPlayer(Player targetPlayer)
        {
            if (targetPlayer == null || targetPlayer.Profile == null) return;
            Player mainPlayer = PluginsCore.CorrectPlayer;
            if (mainPlayer == null) return;

            try
            {
                // ⭐ 关键修复：通过 Unity 组件系统获取 GamePlayerOwner (真正的 UI 控制器)
                GamePlayerOwner myOwner = mainPlayer.GetComponent<GamePlayerOwner>();
                if (myOwner == null)
                {
                    NotificationManagerClass.DisplayWarningNotification("无法获取本地 UI 控制器 (GamePlayerOwner)");
                    return;
                }

                Item aiRootItem = targetPlayer.Profile.Inventory.Equipment;
                var aiController = aiRootItem.Owner as TraderControllerClass;

                if (aiRootItem == null || aiController == null)
                {
                    NotificationManagerClass.DisplayWarningNotification("无法获取目标物品栏");
                    return;
                }

                // 构建原生上下文
                GetActionsClass.Class1748 context = new GetActionsClass.Class1748
                {
                    owner = myOwner,
                    rootItem = aiRootItem,
                    lootItemOwner = aiController,
                    controller = mainPlayer.InventoryController
                };

                // 尝试获取目标的 LastOwner (尽善尽美，防止底层报错)
                var targetBridge = Comfort.Common.Singleton<GameWorld>.Instance.GetEverExistedBridgeByProfileID(targetPlayer.ProfileId);
                context.lootItemLastOwner = targetBridge?.iPlayer;

                // 关闭自己的面板，释放鼠标控制权给游戏
                //_isMenuOpen = false;
                //ToggleCursor(false);

                // ⭐ 强行刷新视线，骗过底层的 InteractionRayInfo 检查
                mainPlayer.SaveInteractionRayInfo();

                // ⭐ 两种触发方式任选其一：

                // 方式 A：你刚才写的 Actions 注入法 (模拟按下 F 菜单)
                /*
                ActionsReturnClass actions = new ActionsReturnClass {
                    Actions = new List<ActionsTypesClass> {
                        new ActionsTypesClass { Name = "Search", TargetName = targetPlayer.Profile.Nickname, Action = context.method_3 }
                    }
                };
                myOwner.AvailableInteractionState.Value = actions;
                actions.InitSelected();
                myOwner.AvailableInteractionState.Value?.SelectedAction?.Action?.Invoke();
                */

                // 方式 B：最直接暴力的方法 (跳过 F 菜单，直接执行搜索网络请求与 UI 唤出)
                //道爷我成了!
                context.method_3();

                NotificationManagerClass.DisplayMessageNotification($"已尝试开启物品栏: {targetPlayer.Profile.Nickname}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[搜身异常]: {ex.Message}\n{ex.StackTrace}");
                NotificationManagerClass.DisplayWarningNotification("搜身失败，请看控制台日志");
            }
        }

        /// <summary>
        /// 异步提取角色的真实 3D 渲染头像
        /// </summary>
        public Texture2D GetPlayerIcon(Player player)
        {
            if (player == null || player.Profile == null) return null;
            string profileId = player.ProfileId;

            // 1. 优先从永久缓存中读取
            if (_iconCache.TryGetValue(profileId, out Texture2D cachedTex)) return cachedTex;

            try
            {
                // 2. 检查是否正在后台渲染队列中
                if (_pendingIcons.TryGetValue(profileId, out GClass929 pendingIcon))
                {
                    if (pendingIcon != null && pendingIcon.Sprite != null && pendingIcon.Sprite.texture != null)
                    {
                        Texture2D tex = pendingIcon.Sprite.texture;
                        _iconCache[profileId] = tex;
                        _pendingIcons.Remove(profileId);
                        return tex;
                    }
                    return null;
                }

                // 3. 首次请求：利用游戏底层工厂生成 3D 预览图
                var equipment = player.Profile.Inventory.Equipment.CloneVisibleItem();
                var customization = player.Profile.Customization;
                var request = new GClass932(equipment, customization);
                var iconData = Comfort.Common.Singleton<GClass927>.Instance.GetIcon(request);

                if (iconData != null)
                {
                    if (iconData.Sprite != null && iconData.Sprite.texture != null)
                    {
                        Texture2D tex = iconData.Sprite.texture;
                        _iconCache[profileId] = tex;
                        return tex;
                    }
                    else
                    {
                        _pendingIcons[profileId] = iconData;
                    }
                }
            }
            catch
            {
                // 捕获可能由于极个别 AI 装备破损导致的工厂渲染报错
            }

            return null;
        }
    }
    public class AIManagerGUICfg : IOracleCfg
    {
        internal static ConfigEntry<KeyCode> AIManagerKey { get; set; }
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            AIManagerKey = config.Bind(
                "快捷键设置",
                "打开AI管理器",
                KeyCode.F9,
                "打开战局AI管理器"
            );
        }
    }
}