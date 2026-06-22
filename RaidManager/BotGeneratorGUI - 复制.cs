using BepInEx.Configuration;
using EFT;
using Oracle.Data;
using Oracle.Tools;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.RaidManager
{
    public class SkillManagerGUI : IOracleManagerGUI
    {
        public static bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(480, 20, 520, 420);
        public Vector2 _scrollPos;

        // UI 状态
        private int _selectedTab = 0;
        private string[] _tabs = { "技能 (Skills)", "武器熟练度 (Mastering)" };

        // 参数状态
        public string _targetLevelStr = "51";
        public string _targetExpStr = "5100";

        // 缓存的技能列表，防止每帧去反射或查找
        private SkillClass _selectedSkill = null;
        private MasterSkillClass _selectedMastering = null;

        private List<SkillClass> _cachedSkills;
        private List<MasterSkillClass> _cachedMastering;

        public void SubscribeEvent()
        {
            OracleEvent.OnDrawManagerGUI += OnGUI;
            OracleEvent.OnUpdate += Update;
        }

        public void Update()
        {
            if (PluginsCore.CorrectPlayer == null || PluginsCore.CorrectPlayer.Skills == null) return;

            // 监听快捷键
            if (Input.GetKeyDown(SkillManagerGUICfg.SkillManagerKey.Value))
            {
                _isMenuOpen = !_isMenuOpen;
                MouseManager.ToggleCursor();

                // 打开菜单时，主动缓存一次玩家身上的技能数据
                if (_isMenuOpen) CacheSkills();
            }
        }

        private void CacheSkills()
        {
            var skills = PluginsCore.CorrectPlayer.Skills;

            // 缓存所有常规技能
            _cachedSkills = new List<SkillClass>();
            if (skills.Skills != null)
            {
                _cachedSkills.AddRange(skills.Skills);
            }

            // 缓存所有武器熟练度
            _cachedMastering = new List<MasterSkillClass>();
            if (skills.Mastering != null)
            {
                _cachedMastering.AddRange(skills.Mastering.Values);
            }
        }

        public void OnGUI()
        {
            if (!_isMenuOpen) return;

            UIStyleManager.EnsureInitialized();
            GUI.backgroundColor = Color.white;

            _windowRect = GUI.Window(8852, _windowRect, DrawWindow, "战局技能修改器 (按 F11 隐藏)", UIStyleManager.WindowStyle);
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

            // =========================
            // 1. 顶部 Tab 切换区
            // =========================
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs, UIStyleManager.TabStyle, GUILayout.Height(30));
            GUILayout.Space(10);

            // =========================
            // 3. 参数配置区
            // =========================
            GUILayout.BeginVertical(UIStyleManager.BoxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>目标等级:</b>", GUILayout.Width(80));
            _targetLevelStr = GUILayout.TextField(_targetLevelStr, UIStyleManager.TextFieldStyle, GUILayout.Width(60));

            GUILayout.Space(20);

            GUILayout.Label("<b>目标经验值:</b>", GUILayout.Width(80));
            _targetExpStr = GUILayout.TextField(_targetExpStr, UIStyleManager.TextFieldStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // =========================
            // 4. 技能/熟练度列表区
            // =========================
            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, UIStyleManager.BoxStyle);

            if (_selectedTab == 0) DrawSkillGrid();
            else DrawMasteringGrid();

            GUILayout.EndScrollView();

            // =========================
            // 5. 单个执行区
            // =========================
            GUILayout.Space(10);
            string executeBtnText = "请先在上方选择一个项目";
            bool canExecute = false;

            if (_selectedTab == 0 && _selectedSkill != null)
            {
                executeBtnText = $"修改 [{_selectedSkill.Id}]";
                canExecute = true;
            }
            else if (_selectedTab == 1 && _selectedMastering != null)
            {
                executeBtnText = $"修改 [{_selectedMastering.MasteringGroup.Id}]";
                canExecute = true;
            }

            GUI.enabled = canExecute;
            if (GUILayout.Button(executeBtnText, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
            {
                ExecuteSingleModification();
            }
            GUI.enabled = true;

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;
            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 50, 25));
        }

        private void DrawSkillGrid()
        {
            if (_cachedSkills == null || _cachedSkills.Count == 0) return;

            int count = 0;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            foreach (var skill in _cachedSkills)
            {
                bool isSelected = (_selectedSkill == skill);
                GUIStyle btnStyle = isSelected ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle ?? GUI.skin.button;

                // 按钮上显示技能名字和当前等级
                string btnText = $"{skill.Id}\n(Lv.{skill.Level})";

                if (GUILayout.Button(btnText, btnStyle, GUILayout.Height(40), GUILayout.Width(140)))
                {
                    _selectedSkill = skill;
                }

                count++;
                if (count % 3 == 0)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawMasteringGrid()
        {
            if (_cachedMastering == null || _cachedMastering.Count == 0) return;

            int count = 0;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            foreach (var mastering in _cachedMastering)
            {
                bool isSelected = (_selectedMastering == mastering);
                GUIStyle btnStyle = isSelected ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle ?? GUI.skin.button;

                string btnText = $"{mastering.MasteringGroup.Id}\n(Lv.{mastering.Level})";

                if (GUILayout.Button(btnText, btnStyle, GUILayout.Height(40), GUILayout.Width(140)))
                {
                    _selectedMastering = mastering;
                }

                count++;
                if (count % 3 == 0)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void ExecuteSingleModification()
        {
            float targetExp = 0;
            // 优先解析用户输入的经验值，如果没填则通过等级换算(粗略)
            if (!float.TryParse(_targetExpStr, out targetExp))
            {
                if (int.TryParse(_targetLevelStr, out int targetLevel))
                {
                    targetExp = targetLevel * 100f; // 大部分技能 1级=100经验
                }
            }

            if (_selectedTab == 0 && _selectedSkill != null)
            {
                _selectedSkill.Current = targetExp;
                OracleNotify.Success($"技能 [{_selectedSkill.Id}] 已修改为 {targetExp} 经验值！");
            }
            else if (_selectedTab == 1 && _selectedMastering != null)
            {
                _selectedMastering.Current = targetExp;
                OracleNotify.Success($"武器熟练度 [{_selectedMastering.MasteringGroup.Id}] 已修改为 {targetExp} 经验值！");
            }
        }
    }

    public class SkillManagerGUICfg : IOracleCfg
    {
        internal static ConfigEntry<KeyCode> SkillManagerKey { get; set; }

        public void Initialize(ConfigFile config)
        {
            SkillManagerKey = config.Bind(
                "快捷键设置",
                "打开技能修改器",
                KeyCode.F11, // 假定为 F11，你可以自己在配置文件里改
                "打开战局技能修改面板"
            );
        }
    }
}