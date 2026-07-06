using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using Oracle.Data;
using Oracle.ItemSpawn;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.RaidManager
{
    /// <summary>
    /// 物品实例管理器
    /// </summary>
    public class ItemManagerGUI : IOracleManagerGUI
    {
        //UI状态
        public static bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(20, 20, 800, 650);
        public Vector2 _scrollPos;
        public Vector2 _fileScrollPos = Vector2.zero;
        public static bool SpawnedInSession = true;

        // ================== 多文件与多工作区状态 ==================
        public const string CURRENT_SESSION_ID = "::CURRENT_SESSION::";

        public static string _selectedView = CURRENT_SESSION_ID;
        public static string _inputFileName = "Default";
        public static List<string> _savedFiles = new List<string>();

        // ⭐ 终极进化：多工作区缓存池！每个预设都有自己独立的内存房间
        public static Dictionary<string, List<Item>> _workspaces = new Dictionary<string, List<Item>>();

        // ⭐ 核心指针：根据当前选中的视图，智能返回对应的列表
        public static List<Item> ActiveList
        {
            get
            {
                if (_selectedView == CURRENT_SESSION_ID) return ItemCatcher.SavedItems;
                // 如果内存池里没有，就初始化一个空房间
                if (!_workspaces.ContainsKey(_selectedView))
                {
                    _workspaces[_selectedView] = new List<Item>();
                }
                return _workspaces[_selectedView];
            }
        }

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

                if (_isMenuOpen)
                {
                    RefreshFileList();

                    // 仅当选中的预设文件在后台被物理删除时，才安全回退到内存表
                    if (_selectedView != CURRENT_SESSION_ID && !_savedFiles.Contains(_selectedView))
                    {
                        ItemCatcher.savedItem = null; // 失焦
                        _selectedView = CURRENT_SESSION_ID;
                        _inputFileName = "Default";
                    }
                }
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
            // ================== 顶部全局状态栏 ==================

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

            GUILayout.Space(15);

            // ================== 文件操作次顶栏 ==================
            GUILayout.BeginHorizontal(UIStyleManager.BoxStyle);

            GUILayout.Label("text_item_instance_manager_file_name".i18n(), GUILayout.Width(60));

            string rawInput = GUILayout.TextField(_inputFileName, UIStyleManager.TextFieldStyle, GUILayout.ExpandWidth(true));
            if (rawInput != _inputFileName)
            {
                _inputFileName = SanitizeFileName(rawInput);
            }

            // 新增：清空按钮 (Clear) - 用于一键清空当前正在查看的表
            if (GUILayout.Button("text_button_item_instance_manager_clear".i18n(), UIStyleManager.RedButtonStyle, GUILayout.Width(50)))
            {
                // 清空当前活跃的列表，并立刻失焦
                ItemCatcher.savedItem = null;
                ActiveList.Clear();
            }

            if (GUILayout.Button("text_button_item_instance_manager_refresh".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Width(50)))
            {
                RefreshFileList();
            }
            if (GUILayout.Button("text_button_item_instance_manager_load_items".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Width(50)))
            {
                // 强制读取硬盘覆盖当前缓存
                ItemCatcher.savedItem = null;
                LoadPresetIntoCache(_inputFileName);
            }
            if (GUILayout.Button("text_button_item_instance_manager_save_items".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Width(50)))
            {
                SaveSavedItemsToFile(_inputFileName);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            // ================== 左右分栏区域 ==================
            GUILayout.BeginHorizontal();

            // ------- 左侧：本地文件列表 -------
            GUIStyle origHScroll = GUI.skin.horizontalScrollbar;
            GUIStyle origHThumb = GUI.skin.horizontalScrollbarThumb;

            GUI.skin.horizontalScrollbar = UIStyleManager.HScrollbarStyle;
            GUI.skin.horizontalScrollbarThumb = UIStyleManager.HScrollbarThumbStyle;

            _fileScrollPos = GUILayout.BeginScrollView(_fileScrollPos, UIStyleManager.BoxStyle, GUILayout.Width(200));

            bool isCurrentView = (_selectedView == CURRENT_SESSION_ID);
            GUILayout.BeginHorizontal(isCurrentView ? UIStyleManager.SelectedBoxStyle : UIStyleManager.BoxStyle);

            if (GUILayout.Button("text_item_instance_manager_current_session".i18n(), isCurrentView ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle, GUILayout.ExpandWidth(true)))
            {
                ItemCatcher.savedItem = null; // 切表失焦，丢掉旧指针
                _selectedView = CURRENT_SESSION_ID;
                _inputFileName = "Default";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            for (int i = 0; i < _savedFiles.Count; i++)
            {
                string fileName = _savedFiles[i];
                bool isThisFileSelected = (_selectedView == fileName);

                GUILayout.BeginHorizontal(isThisFileSelected ? UIStyleManager.SelectedBoxStyle : UIStyleManager.BoxStyle);

                if (GUILayout.Button(fileName, isThisFileSelected ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle, GUILayout.ExpandWidth(true)))
                {
                    ItemCatcher.savedItem = null; // 切表即失焦
                    _selectedView = fileName;
                    _inputFileName = fileName;

                    // 仅当缓存池中没有这个表时，才去读硬盘
                    if (!_workspaces.ContainsKey(fileName))
                    {
                        LoadPresetIntoCache(fileName);
                    }
                }

                if (GUILayout.Button("X", UIStyleManager.RedButtonStyle, GUILayout.Width(25)))
                {
                    string pathToDelete = Path.Combine(GetSaveDirectory(), fileName + ".json");
                    if (File.Exists(pathToDelete))
                    {
                        File.Delete(pathToDelete);
                        RefreshFileList();

                        // 从内存池里彻底销毁它
                        if (_workspaces.ContainsKey(fileName))
                            _workspaces.Remove(fileName);

                        if (isThisFileSelected)
                        {
                            ItemCatcher.savedItem = null;
                            _selectedView = CURRENT_SESSION_ID;
                            _inputFileName = "Default";
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.skin.horizontalScrollbar = origHScroll;
            GUI.skin.horizontalScrollbarThumb = origHThumb;

            // ------- 右侧：物品实例列表 -------
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, UIStyleManager.BoxStyle);

            List<Item> activeList = ActiveList;

            if (activeList.Count == 0)
            {
                GUILayout.Label("text_item_instance_manager_no_result".i18n(), UIStyleManager.BoxStyle);
            }
            else
            {
                for (int i = activeList.Count - 1; i >= 0; i--)
                {
                    Item item = activeList[i];
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
                        activeList.RemoveAt(i);
                        if (isCurrent) ItemCatcher.savedItem = null;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;

            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 50, 25));
        }

        private string GetSaveDirectory()
        {
            string dir = Path.Combine(PluginsCore.pluginDir, "itemsaves");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private void RefreshFileList()
        {
            _savedFiles.Clear();
            string dir = GetSaveDirectory();
            string[] files = Directory.GetFiles(dir, "*.json");
            foreach (string file in files)
            {
                _savedFiles.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            string invalidChars = new string(Path.GetInvalidFileNameChars());
            string regexSearch = string.Format("[{0}]", Regex.Escape(invalidChars));
            return Regex.Replace(fileName, regexSearch, "");
        }

        private void LoadPresetIntoCache(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName == CURRENT_SESSION_ID)
                return;

            string savePath = Path.Combine(GetSaveDirectory(), fileName + ".json");

            if (!File.Exists(savePath))
            {
                _workspaces[fileName] = new List<Item>();
                _selectedView = fileName;
                return;
            }

            try
            {
                string json = File.ReadAllText(savePath, Encoding.UTF8);
                var flatItems = json.ParseJsonTo<FlatItemsDataClass[]>();

                if (flatItems == null)
                    return;

                // 保存时没有 parentId 的就是根节点
                HashSet<MongoID> rootIds = flatItems
                    .Where(x => !x.parentId.HasValue)
                    .Select(x => x._id)
                    .ToHashSet();

                var result = Comfort.Common.Singleton<ItemFactoryClass>
                    .Instance
                    .FlatItemsToTree(flatItems, false, null);

                List<Item> loadedItems = new List<Item>();

                if (result.Items != null)
                {
                    foreach (string id in rootIds)
                    {
                        if (result.Items.TryGetValue(id, out Item item) &&
                            !(item is StashItemClass))
                        {
                            loadedItems.Add(item);
                        }
                    }
                }

                _workspaces[fileName] = loadedItems;
                _selectedView = fileName;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load items into cache: {ex}");
            }
        }

        private void SaveSavedItemsToFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            try
            {
                var itemsToSave = ActiveList;

                if (itemsToSave == null) return;

                var flatItems = Comfort.Common.Singleton<ItemFactoryClass>.Instance.TreeToFlatItems(itemsToSave);
                string json = flatItems.ToPrettyJson();

                string savePath = Path.Combine(GetSaveDirectory(), fileName + ".json");
                File.WriteAllText(savePath, json, Encoding.UTF8);

                RefreshFileList();

                if (_selectedView != fileName)
                {
                    ItemCatcher.savedItem = null;
                    LoadPresetIntoCache(fileName);
                    _inputFileName = fileName;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save items: {ex.Message}");
            }
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