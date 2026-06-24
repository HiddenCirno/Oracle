using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using Oracle.Data;
using Oracle.ItemSpawn;
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.RaidManager
{
    /// <summary>
    /// 物品实例
    /// </summary>
    public class ItemManagerGUI : IOracleManagerGUI
    {
        //UI状态
        public static bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(20, 20, 550, 650);
        public Vector2 _scrollPos;
        public Vector2 itemScrollPos = Vector2.zero;
        public static bool SpawnedInSession = true;
        
        //物品图标缓存
        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();
        
        public void SubscribeEvent()
        {
            OracleEvent.OnDrawManagerGUI += OnGUI;
            OracleEvent.OnUpdate += Update;
        }

        public void Update()
        {
            if (Input.GetKeyDown(ItemManagerGUICfg.ItemManagerKey.Value))
            {
                _isMenuOpen = !_isMenuOpen;
                MouseManager.ToggleCursor();
            }
        }

        public void OnGUI()
        {
            if (!_isMenuOpen) return;

            UIStyleManager.EnsureInitialized();

            GUI.backgroundColor = OracleColorManager.ManagerGUIBackground;

            _windowRect = GUI.Window(8848, _windowRect, DrawWindow, "text_item_instance_manager_title".i18n(), UIStyleManager.WindowStyle);
        }

        public void DrawWindow(int windowID)
        {   
            //你妈个逼我用你妈的复选框
            //深色 按钮 方块 文本
            //按钮是按钮文本是文本按钮按了变透明
            //那他妈的也没有hover效果啊我操了
            //真该死
            //总之他妈的把按钮和文本分开
            //早该这么干了 ◪※
            //带勾
            if (GUI.Button(new Rect(_windowRect.width - 110, 4, 50, 20), "text_button_item_instance_manager_fir".i18n(), SpawnedInSession ? UIStyleManager.BlueButtonStyle : UIStyleManager.RedButtonStyle))
            {
                SpawnedInSession = !SpawnedInSession;
            }

            //关闭
            if (GUI.Button(new Rect(_windowRect.width - 55, 4, 50, 20), "text_button_manger_close".i18n(), UIStyleManager.RedButtonStyle))
            {
                _isMenuOpen = false;
                MouseManager.ToggleCursor();
            }

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            GUILayout.Space(10);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);


            if (ItemCatcher.SavedItems.Count == 0)
            {
                GUILayout.Label("text_item_instance_manager_no_result".i18n(), UIStyleManager.BoxStyle);
            }
            else
            {
                for (int i = ItemCatcher.SavedItems.Count - 1; i >= 0; i--)
                {
                    Item item = ItemCatcher.SavedItems[i];
                    bool isCurrent = ItemCatcher.savedItem == item;

                    GUILayout.BeginHorizontal(isCurrent ? UIStyleManager.SelectedBoxStyle : UIStyleManager.BoxStyle);

                    //物品图标
                    Texture2D icon = GetCachedIcon(item);
                    if (icon != null) GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                    else GUILayout.Label("text_item_instance_manager_no_icon".i18n(), GUILayout.Width(64), GUILayout.Height(64));

                    //信息栏
                    GUILayout.BeginVertical();
                    GUILayout.Label($"<b>{item.Name.Localized()}</b>");
                    GUILayout.Label(string.Format("text_item_instance_manager_item_info".i18n(), OracleColorManager.TextGray, item.TemplateId));

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.Format("text_item_instance_manager_item_stack".i18n(), OracleColorManager.TextGray), GUILayout.Width(45));
                    string currentStackStr = item.StackObjectsCount.ToString();
                    string newStackStr = GUILayout.TextField(currentStackStr, 7, UIStyleManager.TextFieldStyle, GUILayout.Width(60));
                    if (newStackStr != currentStackStr)
                    {
                        if (string.IsNullOrEmpty(newStackStr)) item.StackObjectsCount = 0;
                        else if (int.TryParse(newStackStr, out int parsedStack)) item.StackObjectsCount = parsedStack;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    
                    //按钮
                    GUILayout.BeginVertical();

                    GUILayout.BeginHorizontal();

                    //生成和选择
                    if (GUILayout.Button("text_button_item_instance_manager_spawn".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
                    {
                        if (item.StackObjectsCount <= 0) item.StackObjectsCount = 1;
                        Player mainPlayer = PluginsCore.CorrectPlayer;
                        if (mainPlayer != null) ItemSpawner.CloneAndSpawnItemIntoInventory(mainPlayer, item);
                    }

                    GUI.enabled = !isCurrent;
                    if (GUILayout.Button(isCurrent ? "text_button_item_instance_manager_selected".i18n() : "text_button_item_instance_manager_select".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
                    {
                        ItemCatcher.savedItem = item;
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();

                    //掉落和删除
                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("text_button_item_instance_manager_drop".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
                    {
                        if (item.StackObjectsCount <= 0) item.StackObjectsCount = 1;
                        Player mainPlayer = PluginsCore.CorrectPlayer;
                        if (mainPlayer != null)
                        {
                            ItemSpawner.CloneAndDropItem(mainPlayer, item);
                        }
                    }

                    if (GUILayout.Button("text_button_item_instance_manager_delete".i18n(), UIStyleManager.RedButtonStyle, GUILayout.Height(22), GUILayout.MinWidth(70)))
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

        /// <summary>
        /// 获取物品图标
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public Texture2D GetCachedIcon(Item item)
        {
            if (item == null) return null;
            //缓存优先
            if (_iconCache.TryGetValue(item.TemplateId, out Texture2D cachedTex)) return cachedTex;

            try
            {
                //生成图标
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

    /// <summary>
    /// 配置项定义
    /// </summary>
    public class ItemManagerGUICfg : IOracleCfg
    {
        internal static ConfigEntry<KeyCode> ItemManagerKey { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            ItemManagerKey = config.Bind(
                "4. 奇迹之门 / Creation Module",
                "打开物品管理器",
                KeyCode.F10,
                new ConfigDescription(
                    "cfg_creation_module_item_open_manager_key_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_creation_module_item_open_manager_key_name".i18n(),
                        IsAdvanced = false,
                        Order = 130
                    }
                )
            );
        }
    }
}