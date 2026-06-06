using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop; // 包含 ItemViewFactory
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Oracle.ESP
{
    public class ItemManagerGUI
    {
        // UI 状态
        public bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(20, 20, 450, 600); // 初始窗口位置和大小
        public Vector2 _scrollPos;
        public Vector2 itemScrollPos = Vector2.zero; 
        private GameObject _inputManager;

        // 图标缓存池
        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();

        // --- 扁平化 UI 样式缓存 ---
        private GUIStyle flatWindowStyle;
        private GUIStyle flatBoxStyle;
        private GUIStyle flatButtonStyle; 
        private GUIStyle flatScrollbarStyle;
        private GUIStyle flatScrollbarThumbStyle;
        private GUIStyle closeButtonStyle;
        private bool isStyleInitialized = false;

        public void Update()
        {
            // 假设使用 F10 作为呼出按键
            if (Input.GetKeyDown(KeyCode.F10))
            {
                _isMenuOpen = !_isMenuOpen;
                ToggleCursor(_isMenuOpen);
            }
        }

        public void OnGUI()
        {
            // 如果菜单没打开，直接返回
            if (!_isMenuOpen) return;

            // 确保样式被初始化
            InitFlatUI();

            // 为了防止底层皮肤干扰，强制重置一下全局背景色
            GUI.backgroundColor = Color.white;

            // 绘制扁平化窗口，传入 flatWindowStyle
            _windowRect = GUI.Window(8848, _windowRect, DrawWindow, "虚空造物 - 内存实例管理器 (按 F10 隐藏)", flatWindowStyle);
        }

        // 窗口内部的具体绘制逻辑
        // 窗口内部的具体绘制逻辑
        public void DrawWindow(int windowID)
        {
            // ---- 右上角关闭按钮 ----
            // 使用 GUI.Button (绝对坐标)，基于当前窗口的宽度向左偏移 25 像素
            if (GUI.Button(new Rect(_windowRect.width - 25, 4, 20, 20), "X", closeButtonStyle))
            {
                _isMenuOpen = false;
                ToggleCursor(false); // 关界面，锁鼠标，解冻玩家
            }

            // ---- 临时替换全局滚动条样式 ----
            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = flatScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = flatScrollbarThumbStyle;

            // ---- 列表区域开始 ----
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            if (ItemCatcher.SavedItems.Count == 0)
            {
                GUILayout.Label("当前内存中没有暂存的物品。", flatBoxStyle);
            }
            else
            {
                for (int i = ItemCatcher.SavedItems.Count - 1; i >= 0; i--)
                {
                    Item item = ItemCatcher.SavedItems[i];
                    GUILayout.BeginHorizontal(flatBoxStyle);

                    // 1. 图标
                    Texture2D icon = GetCachedIcon(item);
                    if (icon != null) GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                    else GUILayout.Label("无图标", GUILayout.Width(64), GUILayout.Height(64));

                    // 2. 信息
                    GUILayout.BeginVertical();
                    GUILayout.Label($"<b>{item.Name.Localized()}</b>");
                    GUILayout.Label($"<color=grey>Tpl: {item.TemplateId}</color>");
                    GUILayout.EndVertical();

                    // 3. 按钮
                    GUILayout.BeginVertical(GUILayout.Width(80));
                    if (GUILayout.Button("生成 (背包)", flatButtonStyle, GUILayout.Height(30)))
                    {
                        Player mainPlayer = PluginsCore.CorrectPlayer;
                        if (mainPlayer != null) ItemSpawner.CloneAndDropItem(mainPlayer, item);
                    }
                    GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 1f);
                    if (GUILayout.Button("清除记录", flatButtonStyle, GUILayout.Height(30)))
                    {
                        ItemCatcher.SavedItems.RemoveAt(i);
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            // ---- 恢复全局滚动条样式 ----
            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;

            // ---- 限制窗口拖拽区域 ----
            // 限制顶部只有左边区域可以拖拽，防止点击右上角 "X" 时意外触发窗口移动
            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 30, 25));
        }

        /// <summary>
        /// 从塔科夫底层提取原生贴图，并进行缓存
        /// </summary>
        public Texture2D GetCachedIcon(Item item)
        {
            if (item == null) return null;

            if (_iconCache.TryGetValue(item.TemplateId, out Texture2D cachedTex))
            {
                return cachedTex;
            }

            try
            {
                var iconData = ItemViewFactory.LoadItemIcon(item, 1, false);
                if (iconData != null && iconData.Sprite != null && iconData.Sprite.texture != null)
                {
                    Texture2D tex = iconData.Sprite.texture;
                    _iconCache[item.TemplateId] = tex;
                    return tex;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 控制鼠标指针的显示与解锁，并调用塔科夫原生鼠标 UI
        /// </summary>
        public void ToggleCursor(bool unlock)
        {
            if (_inputManager == null)
            {
                _inputManager = GameObject.Find("___Input");
            }

            Cursor.visible = unlock;

            if (unlock)
            {
                // 解锁系统鼠标限制
                Cursor.lockState = CursorLockMode.None;

                // 【新增】替换为塔科夫原生的黄色空闲指针
                CursorSettings.SetCursor(ECursorType.Idle);

                // 播放塔科夫原生打开菜单音效
                Comfort.Common.Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.MenuContextMenu);
            }
            else
            {
                // 锁定系统鼠标限制
                Cursor.lockState = CursorLockMode.Locked;

                // 【新增】将塔科夫原生指针设置为隐形模式
                CursorSettings.SetCursor(ECursorType.Invisible);

                // 播放塔科夫原生关闭菜单音效
                Comfort.Common.Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.MenuDropdown);
            }

            // 禁用输入管理器，防止玩家走动和视角强行锁中
            if (_inputManager != null)
            {
                _inputManager.SetActive(!unlock);
            }
        }

        // ==========================================
        // 样式初始化核心方法
        // ==========================================
        private void InitFlatUI()
        {
            if (isStyleInitialized) return;

            // 1. 窗口样式 (主背景：深蓝灰色)
            flatWindowStyle = new GUIStyle(GUI.skin.window);
            flatWindowStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.16f, 0.18f, 1f));
            flatWindowStyle.focused.background = flatWindowStyle.normal.background;
            flatWindowStyle.onNormal.background = flatWindowStyle.normal.background;
            flatWindowStyle.normal.textColor = Color.white;
            flatWindowStyle.border = new RectOffset(1, 1, 20, 1); // 顶留一点给标题栏

            // 2. 列表底框样式 (次背景：稍微亮一点的蓝灰)
            flatBoxStyle = new GUIStyle(GUI.skin.box);
            flatBoxStyle.normal.background = MakeTex(1, 1, new Color(0.20f, 0.21f, 0.23f, 1f));
            flatBoxStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            flatBoxStyle.border = new RectOffset(0, 0, 0, 0);

            // 3. 按钮样式 (普通、悬停、点击三种反馈)
            flatButtonStyle = new GUIStyle(GUI.skin.button);
            flatButtonStyle.normal.background = MakeTex(1, 1, new Color(0.25f, 0.26f, 0.28f, 1f));
            flatButtonStyle.hover.background = MakeTex(1, 1, new Color(0.35f, 0.36f, 0.39f, 1f));
            flatButtonStyle.active.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            flatButtonStyle.normal.textColor = Color.white;
            flatButtonStyle.hover.textColor = Color.white;
            flatButtonStyle.active.textColor = Color.gray;
            flatButtonStyle.border = new RectOffset(0, 0, 0, 0);
            flatButtonStyle.margin = new RectOffset(2, 2, 2, 2);

            // 4. 滚动条轨道样式 (深色底槽)
            flatScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
            flatScrollbarStyle.normal.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            flatScrollbarStyle.fixedWidth = 10f; // 调细一点更精致
            flatScrollbarStyle.border = new RectOffset(0, 0, 0, 0);

            // 5. 滚动条滑块样式 (浅色滑块，悬停变亮)
            flatScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            flatScrollbarThumbStyle.normal.background = MakeTex(1, 1, new Color(0.3f, 0.31f, 0.33f, 1f));
            flatScrollbarThumbStyle.hover.background = MakeTex(1, 1, new Color(0.4f, 0.41f, 0.43f, 1f));
            flatScrollbarThumbStyle.active.background = MakeTex(1, 1, new Color(0.5f, 0.51f, 0.53f, 1f));
            flatScrollbarThumbStyle.fixedWidth = 10f;
            flatScrollbarThumbStyle.border = new RectOffset(0, 0, 0, 0);

            // 6. 窗口关闭按钮样式 (红色小方块)
            closeButtonStyle = new GUIStyle(flatButtonStyle); // 继承自扁平按钮
            closeButtonStyle.normal.background = MakeTex(1, 1, new Color(0.8f, 0.2f, 0.2f, 1f));
            closeButtonStyle.hover.background = MakeTex(1, 1, new Color(0.9f, 0.3f, 0.3f, 1f));
            closeButtonStyle.active.background = MakeTex(1, 1, new Color(0.6f, 0.1f, 0.1f, 1f));
            closeButtonStyle.alignment = TextAnchor.MiddleCenter;

            isStyleInitialized = true;
        }

        /// <summary>
        /// 在内存中动态生成纯色贴图，洗掉玻璃质感
        /// </summary>
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