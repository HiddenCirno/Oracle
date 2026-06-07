using Diz.LanguageExtensions;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using JetBrains.Annotations;
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Oracle.ESP
{
    public class AIManagerGUI
    {
        // UI 状态
        public bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(480, 20, 500, 600); // 默认在物品管理器右侧
        public Vector2 _scrollPos;
        private GameObject _inputManager;

        // --- 头像异步缓存池 ---
        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();
        // 用于存储正在后台渲染中的头像请求
        public Dictionary<string, GClass929> _pendingIcons = new Dictionary<string, GClass929>();

        // --- 扁平化 UI 样式缓存 ---
        private GUIStyle flatWindowStyle;
        private GUIStyle flatBoxStyle;
        private GUIStyle flatButtonStyle;
        private GUIStyle redButtonStyle;
        private GUIStyle blueButtonStyle; // 新增蓝色按钮样式用于搜身
        private GUIStyle flatScrollbarStyle;
        private GUIStyle flatScrollbarThumbStyle;
        private GUIStyle closeButtonStyle;
        private bool isStyleInitialized = false;

        public void Update()
        {
            // 使用 F9 作为 AI 控制台的呼出按键
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _isMenuOpen = !_isMenuOpen;
                ToggleCursor(_isMenuOpen);
            }
        }

        public void OnGUI()
        {
            if (!_isMenuOpen) return;

            if (isStyleInitialized && (flatWindowStyle == null || flatWindowStyle.normal.background == null))
            {
                isStyleInitialized = false;
            }

            InitFlatUI();
            GUI.backgroundColor = Color.white;

            _windowRect = GUI.Window(8849, _windowRect, DrawWindow, "系统指令 - 战局实体管理器 (按 F9 隐藏)", flatWindowStyle);
        }

        public void DrawWindow(int windowID)
        {
            // ---- 右上角区域 ----
            // 1. 全歼按钮 (放在关闭按钮左侧)
            if (GUI.Button(new Rect(_windowRect.width - 135, 4, 85, 20), "全歼 AI", redButtonStyle))
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
                    }
                }
            }

            // ---- 右上角关闭按钮 ----
            if (GUI.Button(new Rect(_windowRect.width - 45, 4, 40, 20), "关闭", closeButtonStyle))
            {
                _isMenuOpen = false;
                ToggleCursor(false);
            }

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = flatScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = flatScrollbarThumbStyle;

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            // 防御：确保游戏世界和玩家列表已加载
            if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectGameWorld.AllAlivePlayersList == null)
            {
                GUILayout.Label("未进入战局或 AI 列表未初始化。", flatBoxStyle);
            }
            else
            {
                int aliveCount = 0;

                // 遍历当前存活的所有实体
                foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
                {
                    // 过滤逻辑保持不变...
                    if (player == null || player == PluginsCore.CorrectPlayer || !player.HealthController.IsAlive) continue;
                    string targetGroupId = player.Profile?.Info?.GroupId ?? "";
                    bool isTeammate = !string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && targetGroupId == PluginsCore.CorrectGroupId;
                    if (isTeammate) continue;

                    aliveCount++;

                    // --- 调用新的重构逻辑 ---
                    var entityInfo = PlayerESP.GetEntityInfo(player, isTeammate, false);

                    // --- 绘制 ---
                    GUILayout.BeginHorizontal(flatBoxStyle);

                    // 头像绘制逻辑不变
                    Texture2D icon = GetPlayerIcon(player);
                    if (icon != null)
                    {
                        GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                    }
                    else
                    {
                        GUILayout.Box("生成中", flatButtonStyle, GUILayout.Width(64), GUILayout.Height(64));
                    }

                    // --- 实体信息绘制 ---
                    GUILayout.BeginVertical();
                    // 使用新的结构体字段
                    GUILayout.Label($"<b>{entityInfo.Name}</b>  {entityInfo.LevelText}");
                    GUILayout.Label($"<color=grey>{entityInfo.SideText} | 距离: <color=#FFFF00>{entityInfo.Distance} 米</color></color>");
                    GUILayout.EndVertical();

                    // --- 操作按钮区域 (上下平分 64 的高度) ---
                    GUILayout.BeginVertical(GUILayout.Width(80));

                    // ⭐ 新增：搜身按钮
                    if (GUILayout.Button("搜身", blueButtonStyle, GUILayout.Height(30)))
                    {
                        RemoteSearchPlayer(player);
                    }

                    GUILayout.Space(4); // 间距

                    // 杀死按钮
                    if (GUILayout.Button("杀死", redButtonStyle, GUILayout.Height(30)))
                    {
                        player.KillMe(EBodyPartColliderType.HeadCommon, 999999999);
                    }

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }

                if (aliveCount == 0)
                {
                    GUILayout.Label("当前战局中没有可用的非友军实体。", flatBoxStyle);
                }
            }

            GUILayout.EndScrollView();

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;

            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 50, 25));
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
                _isMenuOpen = false;
                ToggleCursor(false);

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
                var equipment = player.Profile.Inventory.Equipment.CloneVisibleItem<InventoryEquipment>();
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

        public void ToggleCursor(bool unlock)
        {
            if (_inputManager == null) _inputManager = GameObject.Find("___Input");

            Cursor.visible = unlock;

            if (unlock)
            {
                Cursor.lockState = CursorLockMode.None;
                CursorSettings.SetCursor(ECursorType.Idle);
                Comfort.Common.Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.MenuContextMenu);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                CursorSettings.SetCursor(ECursorType.Invisible);
                Comfort.Common.Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.MenuDropdown);
            }

            if (_inputManager != null) _inputManager.SetActive(!unlock);
        }

        // ==========================================
        // 样式初始化核心方法
        // ==========================================
        private void InitFlatUI()
        {
            if (isStyleInitialized)
            {
                if (flatWindowStyle != null && flatWindowStyle.normal.background == null)
                {
                    isStyleInitialized = false;
                }
                else
                {
                    return;
                }
            }

            flatWindowStyle = new GUIStyle(GUI.skin.window);
            flatWindowStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.16f, 0.18f, 1f));
            flatWindowStyle.focused.background = flatWindowStyle.normal.background;
            flatWindowStyle.onNormal.background = flatWindowStyle.normal.background;
            flatWindowStyle.normal.textColor = Color.white;
            flatWindowStyle.border = new RectOffset(1, 1, 20, 1);

            flatBoxStyle = new GUIStyle(GUI.skin.box);
            flatBoxStyle.normal.background = MakeTex(1, 1, new Color(0.20f, 0.21f, 0.23f, 1f));
            flatBoxStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            flatBoxStyle.border = new RectOffset(0, 0, 0, 0);

            flatButtonStyle = new GUIStyle(GUI.skin.button);
            flatButtonStyle.normal.background = MakeTex(1, 1, new Color(0.25f, 0.26f, 0.28f, 1f));
            flatButtonStyle.hover.background = MakeTex(1, 1, new Color(0.35f, 0.36f, 0.39f, 1f));
            flatButtonStyle.active.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            flatButtonStyle.normal.textColor = Color.white;
            flatButtonStyle.hover.textColor = Color.white;
            flatButtonStyle.active.textColor = Color.gray;
            flatButtonStyle.border = new RectOffset(0, 0, 0, 0);
            flatButtonStyle.margin = new RectOffset(2, 2, 2, 2);

            redButtonStyle = new GUIStyle(flatButtonStyle);
            redButtonStyle.normal.background = MakeTex(1, 1, new Color(0.5f, 0.15f, 0.15f, 1f));
            redButtonStyle.hover.background = MakeTex(1, 1, new Color(0.6f, 0.2f, 0.2f, 1f));
            redButtonStyle.active.background = MakeTex(1, 1, new Color(0.3f, 0.1f, 0.1f, 1f));
            redButtonStyle.alignment = TextAnchor.MiddleCenter;

            // ⭐ 新增：搜身专属的蓝色按钮
            blueButtonStyle = new GUIStyle(flatButtonStyle);
            blueButtonStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.35f, 0.55f, 1f));
            blueButtonStyle.hover.background = MakeTex(1, 1, new Color(0.25f, 0.45f, 0.65f, 1f));
            blueButtonStyle.active.background = MakeTex(1, 1, new Color(0.1f, 0.25f, 0.4f, 1f));
            blueButtonStyle.alignment = TextAnchor.MiddleCenter;

            flatScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
            flatScrollbarStyle.normal.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            flatScrollbarStyle.fixedWidth = 10f;
            flatScrollbarStyle.border = new RectOffset(0, 0, 0, 0);

            flatScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            flatScrollbarThumbStyle.normal.background = MakeTex(1, 1, new Color(0.3f, 0.31f, 0.33f, 1f));
            flatScrollbarThumbStyle.hover.background = MakeTex(1, 1, new Color(0.4f, 0.41f, 0.43f, 1f));
            flatScrollbarThumbStyle.active.background = MakeTex(1, 1, new Color(0.5f, 0.51f, 0.53f, 1f));
            flatScrollbarThumbStyle.fixedWidth = 10f;
            flatScrollbarThumbStyle.border = new RectOffset(0, 0, 0, 0);

            closeButtonStyle = new GUIStyle(redButtonStyle);

            isStyleInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}