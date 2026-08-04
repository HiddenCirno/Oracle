using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using JetBrains.Annotations;
using Oracle.Data;
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Oracle.RaidManager
{
    /// <summary>
    /// AI管理
    /// </summary>
    public class AIManagerGUI
    {
        public Vector2 _scrollPos;

        //头像缓存池
        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();

        //头像渲染队列
        public Dictionary<string, ItemIcon> _pendingIcons = new Dictionary<string, ItemIcon>();

        /// <summary>
        /// 绘制
        /// </summary>
        public void DrawPanel()
        {
            //全部杀死按钮
            if (GUI.Button(new Rect(RaidManagerGUI._windowRect.width - 145, 4, 85, 20), "text_button_ai_manager_kill_all".i18n(), UIStyleManager.RedButtonStyle))
            {
                if (PluginsCore.CorrectGameWorld != null && PluginsCore.CorrectGameWorld.AllAlivePlayersList != null)
                {
                    foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
                    {
                        //排除自己和队友和死人
                        if (player == null || player == PluginsCore.CorrectPlayer || !player.HealthController.IsAlive) continue;
                        string targetGroupId = player.Profile?.Info?.GroupId ?? "";
                        if (!string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && targetGroupId == PluginsCore.CorrectGroupId) continue;

                        //杀死
                        player.KillMe(EBodyPartColliderType.HeadCommon, 99999999);
                        //防止无敌
                        player?.OnDead(EDamageType.Environment);
                    }
                }
            }

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            GUILayout.Space(10);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            // 防御：确保游戏世界和玩家列表已加载
            if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectGameWorld.AllAlivePlayersList == null)
            {
                GUILayout.Label("text_button_ai_manager_no_result".i18n(), UIStyleManager.BoxStyle);
            }
            else
            {
                int aliveCount = 0;

                //遍历玩家表
                foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
                {
                    //过滤
                    if (player == null || player == PluginsCore.CorrectPlayer || !player.HealthController.IsAlive) continue;
                    var info = player.Profile?.Info;
                    if (info == null) continue;
                    string targetGroupId = info?.GroupId ?? "";
                    bool isTeammate = OraclePlayerDataManager.IsTeammate(info);
                    if (isTeammate) continue;

                    aliveCount++;

                    //读取信息
                    var entityInfo = OraclePlayerDataManager.GetEntityInfo(player, isTeammate, false);

                    //绘制
                    GUILayout.BeginHorizontal(UIStyleManager.BoxStyle);

                    //缓存头像
                    Texture2D icon = GetPlayerIcon(player);
                    if (icon != null)
                    {
                        GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                    }
                    else
                    {
                        GUILayout.Box("text_button_ai_manager_avatar_generating".i18n(), UIStyleManager.NormalButtonStyle, GUILayout.Width(64), GUILayout.Height(64));
                    }

                    GUILayout.BeginVertical();
                    
                    //结构体字段
                    GUILayout.Label($"<b>{entityInfo.Name}</b>  {entityInfo.LevelText}");
                    
                    GUILayout.Label(string.Format("text_ai_manager_ai_info".i18n(), OracleColorManager.TextGray, entityInfo.SideText, OracleColorManager.Distance, entityInfo.Distance));
                    GUILayout.EndVertical();

                    //绘制按钮
                    GUILayout.BeginVertical(GUILayout.Width(130));

                    BotOwner botOwner = player.AIData?.BotOwner;

                    //传送/搜索
                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("text_button_ai_manager_teleport".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Height(30), GUILayout.MinWidth(60)))
                    {
                        TeleportBotToMe(player);
                    }
                    if (GUILayout.Button("text_button_ai_manager_search".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Height(30), GUILayout.MinWidth(60)))
                    {
                        RemoteSearchPlayer(player);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(4);

                    //冻结/杀死
                    GUILayout.BeginHorizontal();

                    GUI.enabled = false;
                    //直接禁用按钮得了
                    //修不好, 已弃用
                    if (GUILayout.Button("text_button_ai_manager_freeze".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Height(30), GUILayout.MinWidth(60))){}
                    GUI.enabled = true;

                    if (GUILayout.Button("text_button_ai_manager_kill".i18n(), UIStyleManager.RedButtonStyle, GUILayout.Height(30), GUILayout.MinWidth(60)))
                    {
                        player.KillMe(EBodyPartColliderType.HeadCommon, 99999999);
                        player?.OnDead(EDamageType.Environment);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }

                if (aliveCount == 0)
                {
                    GUILayout.Label("text_ai_manager_no_target".i18n(), UIStyleManager.BoxStyle);
                }
            }

            GUILayout.EndScrollView();

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;
        }

        //传送方法
        //其实也有点问题, 但我懒得修了
        private void TeleportBotToMe(Player targetPlayer)
        {
            Player mainPlayer = PluginsCore.CorrectPlayer;
            if (mainPlayer == null || targetPlayer == null) return;

            Vector3 targetPos = mainPlayer.Position + mainPlayer.Transform.forward * 1f;
            targetPos.y += 0.2f;

            targetPlayer.Teleport(targetPos, true);
        }

        //远程搜索必要的Patch
        [HarmonyPatch(typeof(SearchController), "TryFindChangedContainer")]
        public class TryFindChangedContainerPatch
        {
            public static void Postfix(ItemAddress address, [CanBeNull] out ItemInfo changedContainer, ref bool __result)
            {
                changedContainer = null;
                __result = false;
            }
        }

        /// <summary>
        /// 远程打开玩家物品栏
        /// </summary>
        /// <param name="targetPlayer"></param>
        private void RemoteSearchPlayer(Player targetPlayer)
        {
            if (targetPlayer == null || targetPlayer.Profile == null) return;
            Player mainPlayer = PluginsCore.CorrectPlayer;
            if (mainPlayer == null) return;

            try
            {
                //取组件
                GamePlayerOwner myOwner = mainPlayer.GetComponent<GamePlayerOwner>();
                if (myOwner == null)
                {
                    //NotificationManagerClass.DisplayWarningNotification("无法获取本地 UI 控制器 (GamePlayerOwner)");
                    return;
                }

                Item aiRootItem = targetPlayer.Profile.Inventory.Equipment;
                var aiController = aiRootItem.Owner as ItemController;

                //查找物品栏
                if (aiRootItem == null || aiController == null)
                {
                    //NotificationManagerClass.DisplayWarningNotification("无法获取目标物品栏");
                    return;
                }

                //构建一个虚拟的搜索行为
                InteractionContextHelper.CG_GetAvailableInteractionState1 context = new InteractionContextHelper.CG_GetAvailableInteractionState1
                {
                    owner = myOwner,
                    rootItem = aiRootItem,
                    lootItemOwner = aiController,
                    controller = mainPlayer.InventoryController
                };

                //找到lastowner
                var targetBridge = Comfort.Common.Singleton<GameWorld>.Instance.GetEverExistedBridgeByProfileID(targetPlayer.ProfileId);
                context.lootItemLastOwner = targetBridge?.iPlayer;

                //绕过射线检查
                mainPlayer.SaveInteractionRayInfo();

                //触发搜索行为
                context.method_3();

                //NotificationManagerClass.DisplayMessageNotification($"已尝试开启物品栏: {targetPlayer.Profile.Nickname}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Oracle]: 打开物品栏时发生错误!\n {ex.Message}\n{ex.StackTrace}");
                //NotificationManagerClass.DisplayWarningNotification("搜身失败，请看控制台日志");
            }
        }

        /// <summary>
        /// 异步提取角色头像图
        /// </summary>
        public Texture2D GetPlayerIcon(Player player)
        {
            if (player == null || player.Profile == null) return null;
            string profileId = player.ProfileId;

            //优先缓存
            if (_iconCache.TryGetValue(profileId, out Texture2D cachedTex)) return cachedTex;

            try
            {
                //检查是否在队列
                if (_pendingIcons.TryGetValue(profileId, out ItemIcon pendingIcon))
                {
                    if (pendingIcon != null && pendingIcon.Sprite != null && pendingIcon.Sprite.texture != null)
                    {
                        Texture2D tex = pendingIcon.Sprite.texture;
                        _iconCache[profileId] = tex;
                        _pendingIcons.Remove(profileId);
                        return tex;
                    }
                    return null;
                }

                //从底层生成头图
                var equipment = player.Profile.Inventory.Equipment.CloneVisibleItem();
                var customization = player.Profile.Customization;
                var request = new PlayerIconRequest(equipment, customization);
                var iconData = Comfort.Common.Singleton<EFT.PlayerIcons.PlayerIconCreator>.Instance.GetIcon(request);

                if (iconData != null)
                {
                    if (iconData.Sprite != null && iconData.Sprite.texture != null)
                    {
                        Texture2D tex = iconData.Sprite.texture;
                        _iconCache[profileId] = tex;
                        return tex;
                    }
                    else
                    {
                        _pendingIcons[profileId] = iconData;
                    }
                }
            }
            catch(Exception err)
            {
                OracleCommon.ShowError(err);
            }

            return null;
        }
    }
}