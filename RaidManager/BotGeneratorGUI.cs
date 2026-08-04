using Comfort.Common;
using EFT;
using Oracle.Data;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

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
            GUILayout.Label("text_bot_generator_generate_count".i18n(), GUILayout.Width(110));
            _spawnAmountStr = GUILayout.TextField(_spawnAmountStr, UIStyleManager.TextFieldStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10);

            //AI选择
            GUILayout.Label(string.Format("text_bot_generator_generate_type".i18n(), _allAvailableRoles.Count));

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            _rolesScrollPos = GUILayout.BeginScrollView(_rolesScrollPos, UIStyleManager.BoxStyle);

            foreach (WildSpawnType role in _allAvailableRoles)
            {
                bool isSelected = (_selectedRole == role);
                GUIStyle btnStyle = isSelected ? UIStyleManager.BlueButtonStyle : (UIStyleManager.NormalButtonStyle ?? GUI.skin.button);

                if (GUILayout.Button(role.ToString().i18n(), btnStyle, GUILayout.Height(35), GUILayout.ExpandWidth(true)))
                {
                    _selectedRole = role;
                }
                GUILayout.Space(4);
            }

            GUILayout.EndScrollView();
            GUILayout.Space(15);

            //生成按钮
            GUI.enabled = !_isSpawning;
            string spawnBtnText = _isSpawning ? "text_button_bot_generator_generating".i18n() : string.Format("text_button_bot_generator_generate".i18n(), _selectedRole.ToString().i18n());

            if (GUILayout.Button(spawnBtnText, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
            {
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

            int amount = 1;
            int.TryParse(_spawnAmountStr, out amount);
            amount = Mathf.Clamp(amount, 1, 20);

            try
            {
                _isSpawning = true;
                var gameWorld = Singleton<GameWorld>.Instance;
                var botGame = Singleton<IBotGame>.Instance;

                if (botGame?.BotsController == null || gameWorld?.MainPlayer == null)
                {
                    //OracleNotify.Message("刷怪器未就绪或玩家不存在", EFT.Communications.ENotificationIconType.Alert, GlobalCfg.MuteNotice.Value);
                    return;
                }

                // 阵营判定：保留 PMC 属性
                EPlayerSide side = EPlayerSide.Savage;
                string roleStr = _selectedRole.ToString().ToLower();
                if (roleStr.Contains("bear")) side = EPlayerSide.Bear;
                else if (roleStr.Contains("usec") || roleStr.Contains("pmc")) side = EPlayerSide.Usec;

                int successCount = 0;

                for (int i = 0; i < amount; i++)
                {
                    bool success = await AdvancedBotSpawner.SpawnBotPerfectly(
                        botGame.BotsController,
                        gameWorld.MainPlayer,
                        _selectedRole,
                        side
                    );
                    if (success) successCount++;
                }

                //OracleNotify.Message($"成功召唤 {successCount} 名 {_selectedRole} 目标", EFT.Communications.ENotificationIconType.Default, GlobalCfg.MuteNotice.Value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Oracle] Bot生成失败!\n{ex.Message}\n{ex.StackTrace}");
                OracleNotify.Message("text_bot_generator_generate_failed".i18n(), EFT.Communications.ENotificationIconType.Alert, GlobalCfg.MuteNotice.Value);
            }
            finally
            {
                _isSpawning = false;
            }
        }
    }

    /// <summary>
    /// 高级AI生成引擎 (视线焦点生成)
    /// </summary>
    public static class AdvancedBotSpawner
    {
        public static async Task<bool> SpawnBotPerfectly(BotsController botsController, Player mainPlayer, WildSpawnType role, EPlayerSide side)
        {
            try
            {
                var botSpawner = botsController.BotSpawner;

                // 1. 【核心更改】：取准星焦点坐标
                Vector3 targetPos = mainPlayer.Position + (mainPlayer.LookDirection * 15f); // 默认降级方案：玩家朝向正前方15米
                if (Camera.main != null)
                {
                    // 从摄像机中心发射一条长达300米的射线
                    if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit rayHit, 300f))
                    {
                        targetPos = rayHit.point;
                    }
                    else
                    {
                        // 如果对着天空看，没打中任何地形，就取空中30米的位置让其自行寻找地面
                        targetPos = Camera.main.transform.position + Camera.main.transform.forward * 30f;
                    }
                }

                // 2. 取合法的 NavMesh 坐标 (防止刷在墙里、石头里或虚空里)
                Vector3 safePos = targetPos;
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 30f, NavMesh.AllAreas))
                {
                    safePos = navHit.position;
                }
                else
                {
                    // 准星指向的地方实在太离谱（比如地图边界外），回退到玩家身边
                    if (NavMesh.SamplePosition(mainPlayer.Position, out NavMeshHit playerNavHit, 30f, NavMesh.AllAreas))
                    {
                        safePos = playerNavHit.position;
                    }
                }

                // 3. 找该坐标附近合法的 BotZone
                BotZone closestZone = botSpawner.GetClosestZone(safePos, out float dist);
                if (closestZone == null) throw new Exception("准星所指区域附近找不到合法的 BotZone");

                // 4. 窃取合法的 CorePointId 唤醒 AI 大脑
                int validCorePointId = 0;
                if (closestZone.SpawnPointMarkers != null && closestZone.SpawnPointMarkers.Count > 0)
                {
                    float minDist = float.MaxValue;
                    foreach (var marker in closestZone.SpawnPointMarkers)
                    {
                        float d = Vector3.Distance(safePos, marker.Position);
                        if (d < minDist)
                        {
                            minDist = d;
                            validCorePointId = marker.SpawnPoint.CorePointId;
                        }
                    }
                }

                // 5. 构造 Profile 数据 (完全使用游戏原生管线)
                var spawnParams = new BotSpawnParams { TriggerType = SpawnTriggerType.none };
                var profileData = new GetProfileDataParams(side, role, BotDifficulty.normal, 0f, null);

                BotCreationData botCreationData = await BotCreationData.Create(
                    profileData,
                    botSpawner._botCreator,
                    1,
                    botSpawner
                );

                if (botCreationData == null) throw new Exception("BotCreationData 创建失败");

                // 把我们计算好的准星安全坐标 + 偷来的合法 AI 节点 ID 一起塞进去！
                botCreationData.AddPosition(safePos, validCorePointId);

                // 6. 执行原生生成！(去掉了洗脑回调，直接传null)
                botSpawner.method_10(closestZone, botCreationData, null, CancellationToken.None);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AdvancedBotSpawner] 核心生成异常: {ex.Message}");
                return false;
            }
        }
    }
}