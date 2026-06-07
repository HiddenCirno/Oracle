using UnityEngine;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop; // 包含 ItemViewFactory
using Oracle.Utils;
using System.Collections.Generic;
using EFT;
using EFT.UI;

namespace Oracle.ESP
{
    public class ItemManagerGUI
    {
        // UI 状态
        public bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(20, 20, 460, 600); // 初始窗口位置和大小
        public Vector2 _scrollPos;
        public Vector2 itemScrollPos = Vector2.zero;
        private GameObject _inputManager;

        // 图标缓存池
        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();

        // --- 扁平化 UI 样式缓存 ---
        private GUIStyle flatWindowStyle;
        private GUIStyle flatBoxStyle;
        private GUIStyle flatBoxStyleActive;
        private GUIStyle flatButtonStyle;
        private GUIStyle redButtonStyle; // 【统一】红色按钮样式
        private GUIStyle flatTextFieldStyle;
        private GUIStyle flatScrollbarStyle;
        private GUIStyle flatScrollbarThumbStyle;
        private bool isStyleInitialized = false;

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
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

            _windowRect = GUI.Window(8848, _windowRect, DrawWindow, "虚空造物 - 内存实例管理器 (按 F10 隐藏)", flatWindowStyle);
        }

        public void DrawWindow(int windowID)
        {
            // ---- 右上角关闭按钮 (使用统一的 redButtonStyle) ----
            if (GUI.Button(new Rect(_windowRect.width - 45, 4, 40, 20), "关闭", redButtonStyle))
            {
                _isMenuOpen = false;
                ToggleCursor(false);
            }

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = flatScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = flatScrollbarThumbStyle;

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
                    bool isCurrent = (ItemCatcher.savedItem == item);

                    GUILayout.BeginHorizontal(isCurrent ? flatBoxStyleActive : flatBoxStyle);

                    // 1. 图标
                    Texture2D icon = GetCachedIcon(item);
                    if (icon != null) GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                    else GUILayout.Label("无图标", GUILayout.Width(64), GUILayout.Height(64));

                    // 2. 信息与输入框
                    GUILayout.BeginVertical();
                    GUILayout.Label($"<b>{item.Name.Localized()}</b>");
                    GUILayout.Label($"<color=grey>Tpl: {item.TemplateId}</color>");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<color=grey>堆叠数:</color>", GUILayout.Width(45));
                    string currentStackStr = item.StackObjectsCount.ToString();
                    string newStackStr = GUILayout.TextField(currentStackStr, 7, flatTextFieldStyle, GUILayout.Width(60));
                    if (newStackStr != currentStackStr)
                    {
                        if (string.IsNullOrEmpty(newStackStr)) item.StackObjectsCount = 0;
                        else if (int.TryParse(newStackStr, out int parsedStack)) item.StackObjectsCount = parsedStack;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();

                    // 3. 按钮区域：2x2 网格
                    GUILayout.BeginVertical();   // 不固定宽度，自适应内容

                    // 第一行：生成 + 设为当前
                    GUILayout.BeginHorizontal();
                    // 生成按钮
                    if (GUILayout.Button("生成", flatButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
                    {
                        if (item.StackObjectsCount <= 0) item.StackObjectsCount = 1;
                        Player mainPlayer = PluginsCore.CorrectPlayer;
                        if (mainPlayer != null) ItemSpawner.SpawnItemIntoInventory(mainPlayer, item);
                    }
                    // 设为当前 / 当前选中 按钮
                    GUI.enabled = !isCurrent;
                    if (GUILayout.Button(isCurrent ? "当前" : "选择", flatButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
                    {
                        ItemCatcher.savedItem = item;
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();

                    // 第二行：掉落世界 + 清除
                    GUILayout.BeginHorizontal();
                    // 新增：掉落世界按钮
                    if (GUILayout.Button("掉落", flatButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
                    {
                        if (item.StackObjectsCount <= 0) item.StackObjectsCount = 1;
                        Player mainPlayer = PluginsCore.CorrectPlayer;
                        if (mainPlayer != null)
                        {
                            // 请在此处实现将物品掉落到世界的逻辑
                            // 例如：ItemSpawner.DropItemToWorld(mainPlayer, item);
                            // 或者：mainPlayer.DropItem(item, item.StackObjectsCount);
                            //Debug.LogWarning("需要实现掉落世界的方法");
                            ItemSpawner.CloneAndDropItem(mainPlayer, item);
                        }
                    }
                    // 清除按钮（红色）
                    if (GUILayout.Button("删除", redButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
                    {
                        ItemCatcher.SavedItems.RemoveAt(i);
                        if (isCurrent) ItemCatcher.savedItem = null;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;

            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 50, 25));
        }

        public Texture2D GetCachedIcon(Item item)
        {
            if (item == null) return null;
            if (_iconCache.TryGetValue(item.TemplateId, out Texture2D cachedTex)) return cachedTex;

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

        private void InitFlatUI()
        {
            if (isStyleInitialized)
            {
                // 简单暴力清理：如果检测到背景丢失，说明是场景切换了
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

            flatBoxStyleActive = new GUIStyle(GUI.skin.box);
            flatBoxStyleActive.normal.background = MakeTex(1, 1, new Color(0.28f, 0.32f, 0.38f, 1f));
            flatBoxStyleActive.normal.textColor = Color.white;
            flatBoxStyleActive.border = new RectOffset(0, 0, 0, 0);

            flatButtonStyle = new GUIStyle(GUI.skin.button);
            flatButtonStyle.normal.background = MakeTex(1, 1, new Color(0.25f, 0.26f, 0.28f, 1f));
            flatButtonStyle.hover.background = MakeTex(1, 1, new Color(0.35f, 0.36f, 0.39f, 1f));
            flatButtonStyle.active.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            flatButtonStyle.normal.textColor = Color.white;
            flatButtonStyle.hover.textColor = Color.white;
            flatButtonStyle.active.textColor = Color.gray;
            flatButtonStyle.border = new RectOffset(0, 0, 0, 0);
            flatButtonStyle.margin = new RectOffset(2, 2, 2, 2);

            // 【重构】统一的危险操作按钮样式 (暗红色)
            redButtonStyle = new GUIStyle(flatButtonStyle);
            redButtonStyle.normal.background = MakeTex(1, 1, new Color(0.5f, 0.15f, 0.15f, 1f)); // 默认暗红
            redButtonStyle.hover.background = MakeTex(1, 1, new Color(0.6f, 0.2f, 0.2f, 1f));   // 悬停微亮
            redButtonStyle.active.background = MakeTex(1, 1, new Color(0.3f, 0.1f, 0.1f, 1f));  // 按下变深
            redButtonStyle.alignment = TextAnchor.MiddleCenter;

            flatTextFieldStyle = new GUIStyle(GUI.skin.textField);
            flatTextFieldStyle.normal.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            flatTextFieldStyle.focused.background = MakeTex(1, 1, new Color(0.18f, 0.20f, 0.22f, 1f));
            flatTextFieldStyle.normal.textColor = Color.white;
            flatTextFieldStyle.focused.textColor = Color.white;
            flatTextFieldStyle.border = new RectOffset(0, 0, 0, 0);
            flatTextFieldStyle.margin = new RectOffset(2, 2, 2, 2);
            flatTextFieldStyle.alignment = TextAnchor.MiddleLeft;

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