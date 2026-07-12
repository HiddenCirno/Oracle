using EFT.UI;
using EFT.UI.DragAndDrop;
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
        private Dictionary<string, Texture2D> _masteringIconCache = new Dictionary<string, Texture2D>();

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

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

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

        private void DrawSkillGrid(SkillClass[] skills)
        {
            if (skills == null || skills.Length == 0) return;

            const float itemWidth = 245f;

            for (int i = 0; i < skills.Length; i += 2)
            {
                GUILayout.BeginHorizontal();

                // 左列
                DrawSkillItem(skills[i], itemWidth);
                GUILayout.Space(10);

                // 右列
                if (i + 1 < skills.Length)
                {
                    DrawSkillItem(skills[i + 1], itemWidth);
                }
                else
                {
                    GUILayout.Space(itemWidth);
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(6);
            }
        }

        private void DrawSkillItem(SkillClass skill, float width)
        {
            bool isSelected = (_selectedSkill == skill);

            GUILayout.BeginHorizontal(
                isSelected ? UIStyleManager.SelectedBoxStyle : UIStyleManager.BoxStyle,
                GUILayout.Width(width),
                GUILayout.Height(80)
            );

            // 图标
            Texture2D skillIconTex = null;

            var sprite = EFTHardSettings.Instance.StaticIcons.SkillIdSprites
                .GetValueOrDefault(skill.Id);

            if (sprite != null)
                skillIconTex = sprite.texture;

            if (skillIconTex != null)
            {
                GUILayout.Label(
                    skillIconTex,
                    GUILayout.Width(64),
                    GUILayout.Height(64)
                );
            }
            else
            {
                GUILayout.Label(
                    "text_item_instance_manager_no_icon".i18n(),
                    GUILayout.Width(64),
                    GUILayout.Height(64)
                );
            }

            // 信息
            GUILayout.BeginVertical();

            GUILayout.Space(8);

            GUILayout.Label(
                $"<b>{skill.Id.ToString().Localized()}</b>"
            );

            GUILayout.Label(
                string.Format("text_skill_manager_show_level".i18n(), skill.Level)
            );

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            //按钮
            string btnText = isSelected
                ? "text_button_item_instance_manager_selected".i18n()
                : "text_button_item_instance_manager_select".i18n();

            GUIStyle btnStyle = isSelected
                ? UIStyleManager.RedButtonStyle
                : UIStyleManager.BlueButtonStyle;

            if (GUILayout.Button(
                btnText,
                btnStyle,
                GUILayout.Width(90),
                GUILayout.Height(30)))
            {
                _selectedSkill = isSelected ? null : skill;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawMasteringGrid(IEnumerable<MasterSkillClass> masterings)
        {
            if (masterings == null) return;

            foreach (var mastering in masterings)
            {
                bool isSelected = (_selectedMastering == mastering);

                GUILayout.BeginHorizontal(
                    isSelected ? UIStyleManager.SelectedBoxStyle : UIStyleManager.BoxStyle,
                    GUILayout.Height(80)
                );

                // 左侧图标
                Texture2D icon = GetMasteringCachedIcon(mastering);

                if (icon != null)
                {
                    DrawRotatedIcon(icon);
                }
                else
                {
                    GUILayout.Label(
                        "text_item_instance_manager_no_icon".i18n(),
                        GUILayout.Width(64),
                        GUILayout.Height(64)
                    );
                }

                // 中间信息
                GUILayout.BeginVertical();

                GUILayout.Space(8);

                GUILayout.Label(
                    $"<b>{mastering.MasteringGroup.Id}</b>"
                );

                GUILayout.Label(
                    string.Format("text_skill_manager_show_level".i18n(), mastering.Level)
                );

                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // 右侧选择按钮
                GUIStyle btnStyle = isSelected
                    ? UIStyleManager.RedButtonStyle
                    : UIStyleManager.BlueButtonStyle;

                string btnText = isSelected
                    ? "text_button_item_instance_manager_selected".i18n()
                    : "text_button_item_instance_manager_select".i18n();

                if (GUILayout.Button(
                    btnText,
                    btnStyle,
                    GUILayout.Width(100),
                    GUILayout.Height(35)))
                {
                    _selectedMastering = isSelected ? null : mastering;
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(6);
            }
        }

        private void DrawRotatedIcon(Texture2D tex)
        {
            const float size = 72f;

            Rect rect = GUILayoutUtility.GetRect(
                size,
                size
            );

            Matrix4x4 old = GUI.matrix;

            GUIUtility.RotateAroundPivot(
                45f,
                rect.center
            );

            GUI.DrawTexture(
                rect,
                tex,
                ScaleMode.ScaleToFit
            );

            GUI.matrix = old;
        }

        private Texture2D GetMasteringCachedIcon(MasterSkillClass mastering)
        {
            if (mastering == null)
                return null;

            if (mastering.MasteringGroup?.Templates == null ||
                mastering.MasteringGroup.Templates.Length == 0)
                return null;

            string templateId = mastering.MasteringGroup.Templates[0];

            //缓存
            if (_masteringIconCache.TryGetValue(templateId, out Texture2D cached))
            {
                return cached;
            }

            try
            {
                var item = Comfort.Common.Singleton<ItemFactoryClass>
                    .Instance
                    .GetPresetItem(templateId);

                if (item == null)
                    return null;

                var iconData = ItemViewFactory.LoadItemIcon(
                    item,
                    1,
                    false
                );

                if (iconData != null &&
                    iconData.Sprite != null &&
                    iconData.Sprite.texture != null)
                {
                    Texture2D tex = iconData.Sprite.texture;

                    _masteringIconCache[templateId] = tex;

                    return tex;
                }
            }
            catch
            {

            }

            return null;
        }
    }
}