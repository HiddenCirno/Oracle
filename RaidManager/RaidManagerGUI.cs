using BepInEx.Configuration;
using Oracle.Data;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.RaidManager
{
    /// <summary>
    /// 战局管理器
    /// </summary>
    public class RaidManagerGUI : IOracleManagerGUI
    {
        //全局唯一主菜单开关
        public static bool _isMenuOpen = false;
        public static Rect _windowRect = new Rect(570, 20, 550, 650);

        private int _selectedTab = 0;
        //只读存key
        private readonly string[] _tabKeys = {
            "text_tab_loot_manager_title",
            "text_tab_ai_manager_title",
            "text_tab_bot_generator_title",
            "text_tab_skill_manager_title"
        };

        //渲染section组
        private string[] _tabs;

        //实例化子面板
        private readonly LootManagerGUI _lootPanel = new LootManagerGUI();
        private readonly AIManagerGUI _aiPanel = new AIManagerGUI();
        private readonly BotGeneratorGUI _botGenPanel = new BotGeneratorGUI();
        private readonly SkillManagerGUI _skillPanel = new SkillManagerGUI();

        public void SubscribeEvent()
        {
            OracleEvent.OnDrawManagerGUI += OnGUI;
            OracleEvent.OnUpdate += Update;
            LocaleManager.CurrentLanguage.SettingChanged += (sender, args) => RefreshLocalizedCache();

            //初始化时刷新一次语言
            //事件总线在意外的地方派上了用场....
            RefreshLocalizedCache();
        }

        //更新语言
        public void RefreshLocalizedCache()
        {
            if (_tabs == null || _tabs.Length != _tabKeys.Length)
            {
                _tabs = new string[_tabKeys.Length];
            }

            for (int i = 0; i < _tabKeys.Length; i++)
            {
                _tabs[i] = _tabKeys[i].i18n();
            }
        }

        public void Update()
        {
            if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null) return;

            if (Input.GetKeyDown(RaidManagerGUICfg.RaidManagerKey.Value))
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

            //绘制窗口
            _windowRect = GUI.Window(8855, _windowRect, DrawWindow, "text_raid_manager_title".i18n(), UIStyleManager.WindowStyle);
        }

        public void DrawWindow(int windowID)
        {
            //关闭按钮
            if (GUI.Button(new Rect(_windowRect.width - 55, 4, 50, 20), "text_button_manger_close".i18n(), UIStyleManager.RedButtonStyle))
            {
                _isMenuOpen = false;
                MouseManager.ToggleCursor();
            }

            GUILayout.Space(10);

            //tab绘制
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs, UIStyleManager.TabStyle, GUILayout.Height(30));
            GUILayout.Space(10);

            //滚动条
            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            //切换tab
            switch (_selectedTab)
            {
                case 0: _lootPanel.DrawPanel(); break;
                case 1: _aiPanel.DrawPanel(); break;
                case 2: _botGenPanel.DrawPanel(); break;
                case 3: _skillPanel.DrawPanel(); break;
            }

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;

            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 50, 25));
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    public class RaidManagerGUICfg : IOracleCfg
    {
        internal static ConfigEntry<KeyCode> RaidManagerKey { get; set; }

        public void Initialize(ConfigFile config)
        {
            RaidManagerKey = config.Bind(
                "5. 创世引擎 / Raid Manage Module",
                "打开战局综合控制台",
                KeyCode.F8,
                new ConfigDescription(
                    "cfg_raid_manage_module_open_manager_key_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_raid_manage_module_open_manager_key_name".i18n(),
                        IsAdvanced = false,
                        Order = 120
                    }
                )
            );
        }
    }
}