using UnityEngine;
using EFT;
using EFT.InventoryLogic;
using Oracle.Utils;
using System.Collections.Generic;
using EFT.UI;

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

            InitFlatUI();
            GUI.backgroundColor = Color.white;

            _windowRect = GUI.Window(8849, _windowRect, DrawWindow, "系统指令 - 战局实体管理器 (按 F9 隐藏)", flatWindowStyle);
        }

        public void DrawWindow(int windowID)
        {
            // ---- 右上角区域 ----
            // 1. 全歼按钮 (放在关闭按钮左侧)
            GUI.backgroundColor = new Color(0.6f, 0.05f, 0.05f, 1f); // 极其深沉的警告红
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
            GUI.backgroundColor = Color.white;

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
                    // 过滤自己、空指针或已死亡的实体
                    if (player == null || player == PluginsCore.CorrectPlayer || !player.HealthController.IsAlive) continue;

                    // 过滤队友
                    string targetGroupId = player.Profile?.Info?.GroupId ?? "";
                    bool isTeammate = !string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && targetGroupId == PluginsCore.CorrectGroupId;
                    if (isTeammate) continue;

                    aliveCount++;

                    // 提取信息
                    ParsePlayerInfo(player, out string name, out string roleText, out string level, out Color factionColor);

                    // 计算距离
                    int distance = 0;
                    if (PluginsCore.CorrectPlayer != null)
                    {
                        distance = Mathf.RoundToInt(Vector3.Distance(PluginsCore.CorrectPlayer.Transform.position, player.Transform.position));
                    }

                    // 开始绘制行
                    GUILayout.BeginHorizontal(flatBoxStyle);

                    // 1. 绘制实体真实 3D 渲染头像
                    Texture2D icon = GetPlayerIcon(player);
                    if (icon != null)
                    {
                        GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                    }
                    else
                    {
                        // 还在后台渲染时，显示一个阵营色块作为加载占位符
                        GUI.backgroundColor = factionColor;
                        GUILayout.Box("生成中", flatButtonStyle, GUILayout.Width(64), GUILayout.Height(64));
                        GUI.backgroundColor = Color.white;
                    }

                    // 2. 实体信息
                    GUILayout.BeginVertical();
                    GUILayout.Label($"<b>{name}</b>  {level}");
                    GUILayout.Label($"<color=grey>{roleText} | 距离: {distance}m</color>");
                    GUILayout.EndVertical();

                    // 3. 操作按钮
                    GUILayout.BeginVertical(GUILayout.Width(80));

                    // 神罚按钮 (直接抹杀)
                    if (GUILayout.Button("杀死", redButtonStyle, GUILayout.Height(64)))
                    {
                        // 使用标准的塔科夫底层伤害处决方法，瞬间触发 Ragdoll
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
                    // 如果渲染完毕，提取 Sprite 的 Texture 并转入永久缓存
                    if (pendingIcon != null && pendingIcon.Sprite != null && pendingIcon.Sprite.texture != null)
                    {
                        Texture2D tex = pendingIcon.Sprite.texture;
                        _iconCache[profileId] = tex;
                        _pendingIcons.Remove(profileId);
                        return tex;
                    }
                    return null; // 仍在渲染中
                }

                // 3. 首次请求：利用游戏底层工厂生成 3D 预览图
                var equipment = player.Profile.Inventory.Equipment.CloneVisibleItem<InventoryEquipment>();
                var customization = player.Profile.Customization;
                var request = new GClass932(equipment, customization);
                var iconData = Comfort.Common.Singleton<GClass927>.Instance.GetIcon(request);

                if (iconData != null)
                {
                    // 万一极速生成完毕，直接缓存
                    if (iconData.Sprite != null && iconData.Sprite.texture != null)
                    {
                        Texture2D tex = iconData.Sprite.texture;
                        _iconCache[profileId] = tex;
                        return tex;
                    }
                    else
                    {
                        // 放入挂起队列，等待下几帧的检查
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

        /// <summary>
        /// 解析玩家信息，返回名称、角色文本、等级和用于生成色块头像的代表色
        /// </summary>
        private void ParsePlayerInfo(Player player, out string name, out string roleText, out string level, out Color factionColor)
        {
            name = "Unknown";
            roleText = "Bot";
            level = "";
            factionColor = new Color(0.2f, 0.2f, 0.2f); // 默认灰色

            if (player.Profile != null && player.Profile.Info != null)
            {
                var info = player.Profile.Info;

                if (PlayerESP.IsAllEnglish(info.Nickname))
                {
                    name = info.Nickname;
                }
                else
                {
                    name = GStruct21.ConvertToLatinic(info.Nickname);
                }

                string side = info.Side.ToString();
                level = $"<color=#7FFF00>[Lv.{info.Level}]</color>";

                if (side == "Savage")
                {
                    var role = info.Settings?.Role.ToString().ToLower() ?? "assault";

                    roleText = "Scav";
                    factionColor = new Color(0.6f, 0.6f, 0.2f); // 黯黄色

                    if (role.Contains("boss"))
                    {
                        roleText = "Boss";
                        factionColor = new Color(0.6f, 0.1f, 0.1f); // 鲜红色
                    }
                    else if (role == "bossboarsniper" || role == "marksman")
                    {
                        roleText = "狙击 Scav";
                        factionColor = new Color(0.1f, 0.6f, 0.4f); // 绿色
                    }
                    else if (role == "pmcbot" || role == "exusec")
                    {
                        roleText = "ROGUE (美军)";
                        factionColor = new Color(0.4f, 0.1f, 0.6f); // 紫色
                    }
                    else if (role.Contains("follower") || role == "tagillahelperagro")
                    {
                        roleText = "Boss 护卫";
                        factionColor = new Color(0.6f, 0.2f, 0.6f); // 粉色
                    }
                    else if (role.Contains("sectant"))
                    {
                        roleText = "邪教徒";
                        factionColor = new Color(0.5f, 0.8f, 0.2f); // 黄绿色
                    }
                    else if (role.Contains("black"))
                    {
                        roleText = "黑狐";
                        factionColor = new Color(0.8f, 0.1f, 0.2f); // 绯红
                    }

                    switch (role)
                    {
                        case "followerbirdeye":
                        case "followerbigpipe":
                        case "infectedtagilla":
                        case "sectantoni":
                        case "sectantpredvestnik":
                        case "sectantprizark":
                            roleText = "Boss";
                            factionColor = new Color(0.6f, 0.1f, 0.1f);
                            break;
                    }
                }
                else
                {
                    if (side == "Usec")
                    {
                        roleText = "PMC (USEC)";
                        factionColor = new Color(0.1f, 0.4f, 0.8f); // 蓝色
                    }
                    else
                    {
                        roleText = "PMC (BEAR)";
                        factionColor = new Color(0.8f, 0.4f, 0.1f); // 橙色
                    }
                }
            }
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
        // 样式初始化核心方法 (复用扁平化风格)
        // ==========================================
        private void InitFlatUI()
        {
            if (isStyleInitialized) return;

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