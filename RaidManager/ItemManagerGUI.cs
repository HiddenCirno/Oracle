using UnityEngine;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop; // 包含 ItemViewFactory
using Oracle.Utils;
using System.Collections.Generic;
using EFT;
using EFT.UI;
using Oracle.ItemSpawn;

namespace Oracle.RaidManager
{
    public class ItemManagerGUI
    {
        // UI 状态
        public bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(20, 20, 460, 600); // 初始窗口位置和大小
        public Vector2 _scrollPos;
        public Vector2 itemScrollPos = Vector2.zero;
        public static bool SpawnedInSession = true;

        // 图标缓存池
        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();

        public void Update()
        {
            if (Input.GetKeyDown(HotKeyManager.ItemManagerKey.Value))
            {
                _isMenuOpen = !_isMenuOpen;
                MouseManager.ToggleCursor(_isMenuOpen);
            }
        }

        public void OnGUI()
        {
            if (!_isMenuOpen) return;

            UIStyleManager.EnsureInitialized();

            GUI.backgroundColor = Color.white;

            _windowRect = GUI.Window(8848, _windowRect, DrawWindow, "虚空造物 - 内存实例管理器 (按 F10 隐藏)", UIStyleManager.WindowStyle);
        }

        public void DrawWindow(int windowID)
        {
            //SpawnedInSession = GUI.Toggle(new Rect(_windowRect.width - 165, 4, 115, 20), SpawnedInSession, "战局内寻找(FIR)");

            //你妈个逼我用你妈的复选框
            //深色 按钮 方块 文本
            //按钮是按钮文本是文本按钮按了变透明
            //那他妈的也没有hover效果啊我操了
            //真该死
            //总之他妈的把按钮和文本分开
            //早该这么干了 ◪※
            if (GUI.Button(new Rect(_windowRect.width - 90, 4, 40, 20), "带勾", SpawnedInSession ? UIStyleManager.BlueButtonStyle : UIStyleManager.RedButtonStyle))
            {
                SpawnedInSession = !SpawnedInSession;
            }
            // ---- 右上角关闭按钮 (使用统一的 redButtonStyle) ----
            if (GUI.Button(new Rect(_windowRect.width - 45, 4, 40, 20), "关闭", UIStyleManager.RedButtonStyle))
            {
                _isMenuOpen = false;
                MouseManager.ToggleCursor(false);
            }

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            GUILayout.Space(10);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);


            if (ItemCatcher.SavedItems.Count == 0)
            {
                GUILayout.Label("当前内存中没有暂存的物品。", UIStyleManager.BoxStyle);
            }
            else
            {
                for (int i = ItemCatcher.SavedItems.Count - 1; i >= 0; i--)
                {
                    Item item = ItemCatcher.SavedItems[i];
                    bool isCurrent = ItemCatcher.savedItem == item;

                    GUILayout.BeginHorizontal(isCurrent ? UIStyleManager.SelectedBoxStyle : UIStyleManager.BoxStyle);

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
                    string newStackStr = GUILayout.TextField(currentStackStr, 7, UIStyleManager.TextFieldStyle, GUILayout.Width(60));
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
                    if (GUILayout.Button("生成", UIStyleManager.BlueButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
                    {
                        if (item.StackObjectsCount <= 0) item.StackObjectsCount = 1;
                        Player mainPlayer = PluginsCore.CorrectPlayer;
                        if (mainPlayer != null) ItemSpawner.CloneAndSpawnItemIntoInventory(mainPlayer, item);
                    }
                    // 设为当前 / 当前选中 按钮
                    GUI.enabled = !isCurrent;
                    if (GUILayout.Button(isCurrent ? "当前" : "选择", UIStyleManager.BlueButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
                    {
                        ItemCatcher.savedItem = item;
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();

                    // 第二行：掉落世界 + 清除
                    GUILayout.BeginHorizontal();
                    // 新增：掉落世界按钮
                    if (GUILayout.Button("掉落", UIStyleManager.BlueButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
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
                    if (GUILayout.Button("删除", UIStyleManager.RedButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
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

    }
}