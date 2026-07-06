using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using Oracle.Data;
using Oracle.ItemSpawn;
using Oracle.Utils;
using System.Collections.Generic;
using System.IO;
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
        public Vector2 _scrollPos; // 右侧物品列表滚动
        public Vector2 _fileScrollPos = Vector2.zero; // 左侧文件列表滚动
        public static bool SpawnedInSession = true;

        // ================== 多文件与视图状态 ==================
        public const string CURRENT_SESSION_ID = "::CURRENT_SESSION::"; // 标识当前战局实例的特殊ID
        public string _selectedView = CURRENT_SESSION_ID; // 当前左侧选中的高亮项
        public string _inputFileName = "Default"; // 顶部输入框中的文件名
        public List<string> _savedFiles = new List<string>();

        // ⭐ 独立缓存列表，用于读取和预览预设，实现数据隔离
        public List<Item> _cachedPresetItems = new List<Item>();

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
                    // 每次打开强制默认回到当前实例视图
                    _selectedView = CURRENT_SESSION_ID;
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

            if (GUILayout.Button("text_button_item_instance_manager_refresh".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Width(60)))
            {
                RefreshFileList();
            }
            if (GUILayout.Button("text_button_item_instance_manager_load_items".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Width(50)))
            {
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

            // ⭐ 1. 永远在最顶部绘制【当前实例】
            bool isCurrentView = (_selectedView == CURRENT_SESSION_ID);
            GUILayout.BeginHorizontal(isCurrentView ? UIStyleManager.SelectedBoxStyle : UIStyleManager.BoxStyle);

            // "text_item_instance_manager_current_session" 可以在多语言配成 ">>> 当前获取项 <<<" 之类的
            if (GUILayout.Button("text_item_instance_manager_current_session".i18n(), isCurrentView ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle, GUILayout.ExpandWidth(true)))
            {
                _selectedView = CURRENT_SESSION_ID;
                _inputFileName = "Default"; // 切换回当前时重置下输入框
            }
            // 当前实例不允许删除，所以这里不绘制 X 按钮
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // ⭐ 2. 绘制从文件夹读取的预设
            for (int i = 0; i < _savedFiles.Count; i++)
            {
                string fileName = _savedFiles[i];
                bool isThisFileSelected = (_selectedView == fileName);

                GUILayout.BeginHorizontal(isThisFileSelected ? UIStyleManager.SelectedBoxStyle : UIStyleManager.BoxStyle);

                // 点击预设，将其加载进缓存列表进行预览
                if (GUILayout.Button(fileName, isThisFileSelected ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle, GUILayout.ExpandWidth(true)))
                {
                    _selectedView = fileName;
                    _inputFileName = fileName;
                    LoadPresetIntoCache(fileName);
                }

                if (GUILayout.Button("X", UIStyleManager.RedButtonStyle, GUILayout.Width(25)))
                {
                    string pathToDelete = Path.Combine(GetSaveDirectory(), fileName + ".json");
                    if (File.Exists(pathToDelete))
                    {
                        File.Delete(pathToDelete);
                        RefreshFileList();
                        if (isThisFileSelected)
                        {
                            // 如果删掉了当前正在看的，强制切回【当前实例】防错
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

            // ⭐ 核心逻辑：根据左侧的选中状态，决定右边渲染哪个列表
            List<Item> activeList = (_selectedView == CURRENT_SESSION_ID) ? ItemCatcher.SavedItems : _cachedPresetItems;

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
                        // ⭐ 从当前活跃列表中移除
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

        /// <summary>
        /// 将文件读取并隔离存入 Cache 列表
        /// </summary>
        private void LoadPresetIntoCache(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName == CURRENT_SESSION_ID) return;

            string savePath = Path.Combine(GetSaveDirectory(), fileName + ".json");
            if (!File.Exists(savePath))
            {
                Debug.LogError($"Save file not found at: {savePath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(savePath, Encoding.UTF8);
                var flatItems = json.ParseJsonTo<FlatItemsDataClass[]>();

                if (flatItems == null) return;

                // ⭐ 仅清理并填充缓存列表
                _cachedPresetItems.Clear();

                var result = Comfort.Common.Singleton<ItemFactoryClass>.Instance.FlatItemsToTree(flatItems, false, null);

                if (result.Items != null)
                {
                    foreach (var item in result.Items.Values)
                    {
                        if (!(item is StashItemClass))
                        {
                            _cachedPresetItems.Add(item);
                        }
                    }
                }

                _selectedView = fileName;
                Debug.Log($"Loaded {_cachedPresetItems.Count} items into cache from {fileName}.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load items into cache: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存当前正在浏览的列表 (当前抓取 or 当前预览的预设)
        /// </summary>
        private void SaveSavedItemsToFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            try
            {
                // ⭐ 根据当前视图状态判断要保存的是哪个列表
                var itemsToSave = (_selectedView == CURRENT_SESSION_ID) ? ItemCatcher.SavedItems : _cachedPresetItems;

                if (itemsToSave == null || itemsToSave.Count == 0)
                {
                    Debug.Log("No items to save.");
                    return;
                }

                var flatItems = Comfort.Common.Singleton<ItemFactoryClass>.Instance.TreeToFlatItems(itemsToSave);
                string json = flatItems.ToPrettyJson();

                string savePath = Path.Combine(GetSaveDirectory(), fileName + ".json");
                File.WriteAllText(savePath, json, Encoding.UTF8);

                Debug.Log($"Items saved to: {savePath}");

                RefreshFileList();
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