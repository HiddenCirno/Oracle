using EFT;
using Oracle.Tools;
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.RaidManager
{
    public class SkillManagerGUI
    {
        private Vector2 _scrollPos;
        private int _selectedSubTab = 0;
        private readonly string[] _tabs = { "常规技能", "武器熟练度" };

        public string _targetLevelStr = "51";

        // 直接保存选中项的引用
        private SkillClass _selectedSkill = null;
        private MasterSkillClass _selectedMastering = null;

        public void DrawPanel()
        {
            // ⭐ 空指针防御：只要拿不到玩家技能，直接不画或者给提示
            var playerSkills = PluginsCore.CorrectPlayer?.Skills;
            if (playerSkills == null)
            {
                GUILayout.Label("未获取到玩家技能数据，请确认是否已进入战局。", UIStyleManager.BoxStyle);
                return;
            }

            // =========================
            // 1. 顶部 Tab 切换区
            // =========================
            _selectedSubTab = GUILayout.Toolbar(_selectedSubTab, _tabs, UIStyleManager.TabStyle, GUILayout.Height(25));
            GUILayout.Space(10);

            // =========================
            // 2. 参数配置提示区
            // =========================
            GUILayout.BeginVertical(UIStyleManager.BoxStyle);
            GUILayout.BeginHorizontal();

            if (_selectedSubTab == 0)
            {
                // 常规技能：保留目标等级输入框
                GUILayout.Label("<b>目标等级 (0-51):</b>", GUILayout.Width(110));
                _targetLevelStr = GUILayout.TextField(_targetLevelStr, UIStyleManager.TextFieldStyle, GUILayout.Width(60));
            }
            else
            {
                // 武器熟练度：去掉输入框，直接展示升级所需的经验界限
                GUILayout.Label("<b>武器专精采用固定等级，无需手动输入。</b>");

                if (_selectedMastering != null)
                {
                    GUILayout.FlexibleSpace();
                    int lv2Exp = _selectedMastering.Int32_0;
                    int lv3Exp = _selectedMastering.Int32_0 + _selectedMastering.Int32_1;
                    GUILayout.Label($"<color=grey>(Lv.2 需要: {lv2Exp} | Lv.3 需要: {lv3Exp})</color>");
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // =========================
            // 3. 技能列表渲染区 (实时热读取)
            // =========================
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

            // =========================
            // 4. 执行按钮区 (动态分发)
            // =========================
            GUILayout.Space(10);

            if (_selectedSubTab == 0)
            {
                // 常规技能：单按钮修改等级
                GUI.enabled = _selectedSkill != null;
                string btnText = _selectedSkill != null ? $"修改技能 [{_selectedSkill.Id.ToString().Localized()}]" : "请先在上方选择一个常规技能";
                if (GUILayout.Button(btnText, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
                {
                    if (int.TryParse(_targetLevelStr, out int targetLevel))
                    {
                        _selectedSkill.SetLevel(targetLevel);
                        //OracleNotify.Success($"技能 [{_selectedSkill.Id.Localized()}] 已变更为 Lv.{targetLevel}！");
                    }
                }
                GUI.enabled = true;
            }
            else
            {
                // 武器熟练度：三按钮直接修改等级
                GUI.enabled = _selectedMastering != null;
                GUILayout.BeginHorizontal();

                // 1. 重置清零按钮 (红色预警)
                string btnText1 = _selectedMastering != null ? "降至 Lv.1 (重置)" : "请选择";
                if (GUILayout.Button(btnText1, UIStyleManager.RedButtonStyle, GUILayout.Height(40)))
                {
                    _selectedMastering.SetCurrent(0f, false);
                    //OracleNotify.Success($"武器专精 [{_selectedMastering.MasteringGroup.Id}] 已重置为 Lv.1 (0 Exp)！");
                }

                // 2. 升至 2 级
                string btnText2 = _selectedMastering != null ? "升至 Lv.2" : "请选择";
                if (GUILayout.Button(btnText2, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
                {
                    // 恰好达到 Lv.2 的经验值界限
                    float expForLv2 = _selectedMastering.Int32_0;
                    _selectedMastering.SetCurrent(expForLv2, false);
                    //OracleNotify.Success($"武器专精 [{_selectedMastering.MasteringGroup.Id}] 已升至 Lv.2！");
                }

                // 3. 升至满级
                string btnText3 = _selectedMastering != null ? "升至满级 Lv.3" : "请选择";
                if (GUILayout.Button(btnText3, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
                {
                    // 恰好达到 Lv.3 (满级) 的经验值界限
                    float expForLv3 = _selectedMastering.Int32_0 + _selectedMastering.Int32_1;
                    _selectedMastering.SetCurrent(expForLv3, false);
                    //OracleNotify.Success($"武器专精 [{_selectedMastering.MasteringGroup.Id}] 已升至满级 Lv.3！");
                }

                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;
        }

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
                string btnText = $"{skill.Id.ToString().Localized()}\n(Lv.{skill.Level})";

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
    }
}