using BepInEx.Configuration;
using EFT;
using Oracle.Data;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.RaidManager
{
    public class RaidManagerGUI : IOracleManagerGUI
    {
        // 全局唯一主菜单开关
        public static bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(480, 20, 550, 650);

        private int _selectedTab = 0;
        private readonly string[] _tabs = { "物资雷达", "实体管理", "虚空召唤", "技能覆写" };

        // 实例化各子面板，用于维持它们各自的滚动条和内部UI状态
        private readonly LootManagerGUI _lootPanel = new LootManagerGUI();
        private readonly AIManagerGUI _aiPanel = new AIManagerGUI();
        private readonly BotGeneratorGUI _botGenPanel = new BotGeneratorGUI();
        private readonly SkillManagerGUI _skillPanel = new SkillManagerGUI();

        public void SubscribeEvent()
        {
            OracleEvent.OnDrawManagerGUI += OnGUI;
            OracleEvent.OnUpdate += Update;
        }

        public void Update()
        {
            if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null) return;

            // 唯一的总快捷键 (F8)
            if (Input.GetKeyDown(RaidManagerGUICfg.RaidManagerKey.Value))
            {
                _isMenuOpen = !_isMenuOpen;
                MouseManager.ToggleCursor();

                // 菜单打开时，通知需要刷新的子面板（例如技能面板需要重新抓取最新等级）
                if (_isMenuOpen)
                {
                    //_skillPanel.RefreshCache();
                }
            }
        }

        public void OnGUI()
        {
            if (!_isMenuOpen) return;

            UIStyleManager.EnsureInitialized();
            GUI.backgroundColor = Color.white;

            // 统一弹出战局管理器主窗口
            _windowRect = GUI.Window(8855, _windowRect, DrawWindow, "Oracle 战局综合控制台 (按 F8 隐藏)", UIStyleManager.WindowStyle);
        }

        public void DrawWindow(int windowID)
        {
            // ---- 右上角关闭按钮 ----
            if (GUI.Button(new Rect(_windowRect.width - 45, 4, 40, 20), "关闭", UIStyleManager.RedButtonStyle))
            {
                _isMenuOpen = false;
                MouseManager.ToggleCursor();
            }

            GUILayout.Space(10);

            // ---- 统一渲染高度定制的扁平化 Tab 栏 ----
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs, UIStyleManager.TabStyle, GUILayout.Height(30));
            GUILayout.Space(10);

            // 劫持并统一滚动条皮肤
            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            // ⭐ 核心解耦点：根据选中的标签页，将绘制权分发给各自独立的类实例
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

    public class RaidManagerGUICfg : IOracleCfg
    {
        internal static ConfigEntry<KeyCode> RaidManagerKey { get; set; }

        public void Initialize(ConfigFile config)
        {
            RaidManagerKey = config.Bind(
                "快捷键设置",
                "打开战局综合控制台",
                KeyCode.F8,
                "一键呼出包含物资、AI、生成、技能的控制中心"
            );
        }
    }
}