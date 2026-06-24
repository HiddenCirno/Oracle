using Comfort.Common;
using EFT;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using Oracle.Data;

namespace Oracle.RaidManager
{
    /// <summary>
    /// Bot生成
    /// </summary>
    public class BotGeneratorGUI
    {
        public Vector2 _scrollPos;

        //生成参数
        public string _spawnAmountStr = "1";

        //AI字典
        private WildSpawnType _selectedRole = WildSpawnType.assault;
        private List<WildSpawnType> _allAvailableRoles;
        private Vector2 _rolesScrollPos;

        //生成中
        private bool _isSpawning = false;
        private void EnsureRolesLoaded()
        {
            if (_allAvailableRoles != null) return;

            _allAvailableRoles = new List<WildSpawnType>();
            
            //遍历底层类型生成表
            foreach (WildSpawnType role in Enum.GetValues(typeof(WildSpawnType)))
            {
                _allAvailableRoles.Add(role);
            }
        }

        public void DrawPanel()
        {
            EnsureRolesLoaded();
            GUILayout.Space(10);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            //生成参数
            GUILayout.BeginVertical(UIStyleManager.BoxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label(LocaleManager.Get("text_bot_generator_generate_count"), GUILayout.Width(110));
            _spawnAmountStr = GUILayout.TextField(_spawnAmountStr, UIStyleManager.TextFieldStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(10);

            //AI选择
            GUILayout.Label(string.Format(LocaleManager.Get("text_bot_generator_generate_type"), _allAvailableRoles.Count));

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            _rolesScrollPos = GUILayout.BeginScrollView(_rolesScrollPos, UIStyleManager.BoxStyle);

            int count = 0;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            foreach (WildSpawnType role in _allAvailableRoles)
            {
                bool isSelected = (_selectedRole == role);
                GUIStyle btnStyle = isSelected ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle ?? GUI.skin.button;

                if (GUILayout.Button(role.ToString(), btnStyle, GUILayout.Height(25), GUILayout.Width(130)))
                {
                    _selectedRole = role;
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

            GUILayout.EndScrollView();

            GUILayout.Space(15);

            //生成按钮
            GUI.enabled = !_isSpawning;

            string spawnBtnText = _isSpawning ? LocaleManager.Get("text_button_bot_generator_generating") : string.Format(LocaleManager.Get("text_button_bot_generator_generate"), _selectedRole);
            if (GUILayout.Button(spawnBtnText, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
            {
                //DebugBotData.UseDebugData.MustBeTrue();
                SpawnBotTask();
            }

            GUI.enabled = true; // 恢复 GUI 启用状态

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// AI生成
        /// </summary>
        private async void SpawnBotTask()
        {
            if (_isSpawning) return;
            _isSpawning = true;

            int amount = 1;
            int.TryParse(_spawnAmountStr, out amount);
            amount = Mathf.Clamp(amount, 1, 20);
            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                var spawner = botGame?.BotsController?.BotSpawner;

                if (spawner == null)
                {
                    //NotificationManagerClass.DisplayWarningNotification("刷怪器未就绪 (BotSpawner is null)");
                    return;
                }
                var spawnParams = new BotSpawnParams
                {
                    TriggerType = SpawnTriggerType.none,
                    Id_spawn = ""
                };
                
                //强制生成
                await spawner.SpawnBotByTypeForce(amount, _selectedRole, BotDifficulty.normal, spawnParams);
                //NotificationManagerClass.DisplayMessageNotification($"成功召唤 {amount} 名 {_selectedRole}！");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Oracle]:Bot生成失败!\n {ex.Message}\n{ex.StackTrace}");
                OracleNotify.Message(LocaleManager.Get("text_bot_generator_generate_failed"), EFT.Communications.ENotificationIconType.Alert, GlobalCfg.MuteNotice.Value);
            }
            finally
            {
                _isSpawning = false;
            }
        }
    }
}