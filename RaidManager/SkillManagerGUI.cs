using Oracle.Data;
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Oracle.RaidManager
{
    /// <summary>
    /// 技能管理器
    /// </summary>
    public class SkillManagerGUI
    {
        private Vector2 _scrollPos;
        private int _selectedSubTab = 0;

        private readonly string[] _tabKeys = { "text_skill_manager_skill_title", "text_skill_manager_mastering_title" };
        private string[] _tabs;

        public string _targetLevelStr = "51";

        // 直接保存选中项的引用
        private SkillClass _selectedSkill = null;
        private MasterSkillClass _selectedMastering = null;

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

        public void DrawPanel()
        {
            // ⭐ 空指针防御：只要拿不到玩家技能，直接不画或者给提示
            var playerSkills = PluginsCore.CorrectPlayer?.Skills;
            if (playerSkills == null)
            {
                GUILayout.Label("text_tab_skill_manager_no_result".i18n(), UIStyleManager.BoxStyle);
                return;
            }

            //标题
            _selectedSubTab = GUILayout.Toolbar(_selectedSubTab, _tabs, UIStyleManager.TabStyle, GUILayout.Height(25));
            GUILayout.Space(10);

            //输入
            if (_selectedSubTab == 0)
            {
                GUILayout.BeginVertical(UIStyleManager.BoxStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("text_skill_manager_skill_level_input".i18n(), GUILayout.Width(110));
                _targetLevelStr = GUILayout.TextField(_targetLevelStr, UIStyleManager.TextFieldStyle, GUILayout.Width(60));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }

            //选项绘制
            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, UIStyleManager.BoxStyle);

            if (_selectedSubTab == 0)
            {
                DrawSkillGrid(playerSkills.Skills);
            }
            else
            {
                DrawMasteringGrid(playerSkills.Mastering.Values);
            }

            GUILayout.EndScrollView();

            //执行
            GUILayout.Space(10);

            if (_selectedSubTab == 0)
            {
                // 常规技能：单按钮修改等级
                GUI.enabled = _selectedSkill != null;
                string btnText = _selectedSkill != null ? string.Format("text_skill_manager_skill_level_set".i18n(), _selectedSkill.Id.ToString().Localized()) : "text_skill_manager_skill_level_select".i18n();
                if (GUILayout.Button(btnText, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
                {
                    if (int.TryParse(_targetLevelStr, out int targetLevel))
                    {
                        _selectedSkill.SetLevel(targetLevel);
                    }
                }
                GUI.enabled = true;
            }
            else
            {
                //专精
                GUI.enabled = _selectedMastering != null;
                GUILayout.BeginHorizontal();

                string btnText1 = _selectedMastering != null ? "text_skill_manager_mastering_level_set_1".i18n() : "text_skill_manager_mastering_level_select".i18n();
                if (GUILayout.Button(btnText1, UIStyleManager.RedButtonStyle, GUILayout.Height(40)))
                {
                    _selectedMastering.SetCurrent(0f, false);
                }

                string btnText2 = _selectedMastering != null ? "text_skill_manager_mastering_level_set_2".i18n() : "text_skill_manager_mastering_level_select".i18n();
                if (GUILayout.Button(btnText2, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
                {
                    float expForLv2 = _selectedMastering.Int32_0;
                    _selectedMastering.SetCurrent(expForLv2, false);
                }

                string btnText3 = _selectedMastering != null ? "text_skill_manager_mastering_level_set_3".i18n() : "text_skill_manager_mastering_level_select".i18n();
                if (GUILayout.Button(btnText3, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
                {
                    float expForLv3 = _selectedMastering.Int32_0 + _selectedMastering.Int32_1;
                    _selectedMastering.SetCurrent(expForLv3, false);
                }

                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;
        }

        /// <summary>
        /// 绘制技能区域
        /// </summary>
        /// <param name="skills"></param>
        private void DrawSkillGrid(SkillClass[] skills)
        {
            if (skills == null || skills.Length == 0) return;

            int count = 0;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            foreach (var skill in skills)
            {
                bool isSelected = (_selectedSkill == skill);
                GUIStyle btnStyle = isSelected ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle;
                string btnText = string.Format("text_skill_manager_show_level".i18n(), skill.Id.ToString().Localized(), skill.Level);

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

        /// <summary>
        /// 绘制专精区域
        /// </summary>
        /// <param name="masterings"></param>
        private void DrawMasteringGrid(IEnumerable<MasterSkillClass> masterings)
        {
            if (masterings == null) return;

            int count = 0;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            foreach (var mastering in masterings)
            {
                bool isSelected = (_selectedMastering == mastering);
                GUIStyle btnStyle = isSelected ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle;
                string btnText = string.Format("text_skill_manager_show_level".i18n(), mastering.MasteringGroup.Id, mastering.Level);

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
    }
}