using BepInEx.Configuration;
using CommonAssets.Scripts.Game.LabyrinthEvent;
using EFT;
using EFT.Communications;
using EFT.SynchronizableObjects;
using Oracle.Data;
using Oracle.Tools;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ESP
{
    /// <summary>
    /// 玩家透视部分
    /// </summary>
    public class PlayerESP : IOracleESP
    {
        //颜色定义
        public static readonly Color ColorSafe = Color.green; //隔墙不可见
        public static readonly Color ColorWarning = Color.yellow; //你可以看到它, 而它没有看你
        public static readonly Color ColorDangerous = Color.red; //你看得到它并且它看得到你
        public void SubscribeEvent()
        {
            // 订阅统一的渲染频道
            OracleEvent.OnDrawESP += OnDrawESP;
        }
        private void OnDrawESP()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // 1. 2D 文本和 UI 直接画
            DrawPlayerText(cam, RenderUtils.EspTextStyle);
            DrawAllPlayerHealthBars(cam);

            // 2. 3D 骨骼线段，必须自己包裹 GL 状态！
            if (Event.current.type == EventType.Repaint)
            {
                RenderUtils.EspMaterial.SetPass(0);
                GL.PushMatrix();
                // GL.LoadPixelMatrix(); (如果有必要的话)
                GL.Begin(GL.LINES);

                // 画骨骼
                DrawPlayerBone(cam);

                GL.End();
                GL.PopMatrix();
            }
        }

        /// <summary>
        /// 绘制玩家骨骼
        /// </summary>
        /// <param name="cam">摄像机</param>
        public static void DrawPlayerBone(Camera cam)
        {
            //功能总开关
            if (!PlayerESPCfg.EnablePlayerESP.Value) return;
            //骨骼透视功能开关
            if (!PlayerESPCfg.EnablePlayerBoneESP.Value) return;
            //绘制每个玩家
            foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
            {   //过滤非自己和空玩家
                if (player == null || player == PluginsCore.CorrectPlayer || player.PlayerBones == null) continue;
                //距离计算, 如果超出透视范围则不绘制, 跳过
                if (!OracleCommon.IsInRange(PlayerESPCfg.PlayerESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
                {
                    continue;
                }
                //初始化透视颜色
                Color finalColor;
                //玩家能否看到AI(射线计算
                bool canPlayerSeeBot = OraclePlayerManager.IsPlayerVisible(cam.transform.position, player, OraclePlayerManager.HighPolyWithTerrainMask);
                //AI能否看到玩家(射线计算
                bool canBotSeePlayer = OraclePlayerManager.IsBotVisible(player, PluginsCore.CorrectPlayer, OraclePlayerManager.HighPolyWithTerrainMask);
                //根据状态决定绘制火柴人的颜色
                if (canBotSeePlayer)
                {
                    //AI能看到你并且无遮挡
                    finalColor = ColorDangerous;
                }
                else if (canPlayerSeeBot)
                {
                    //你能看到AI, AI看不到你并且无遮挡
                    finalColor = ColorWarning;
                }
                else
                {
                    //双方之间有障碍物
                    finalColor = ColorSafe;
                }
                //决定绘制火柴人的颜色
                //这里也许需要为后面的血条透视功能做修改
                //没错, 血量叠加层已完成, 这里不要了, 注释掉
                //GL.Color(finalColor);
                //提取Bones引用
                var bones = player.PlayerBones;
                //查找所有需要的骨骼节点的坐标
                //头颈腰臀
                Vector3? head = OraclePlayerManager.GetBonePos(bones.Head);
                Vector3? neck = OraclePlayerManager.GetBonePos(bones.Neck);
                Vector3? spine3 = OraclePlayerManager.GetBonePos(bones.Spine3);
                Vector3? pelvis = OraclePlayerManager.GetBonePos(bones.Pelvis);
                //肩膀
                Vector3? lShoulder = OraclePlayerManager.GetBonePos(bones.LeftShoulder);
                Vector3? rShoulder = OraclePlayerManager.GetBonePos(bones.RightShoulder);
                //大臂
                Vector3? lUpperarm = (bones.Upperarms != null && bones.Upperarms.Length > 0) ? OraclePlayerManager.GetBonePos(bones.Upperarms[0]) : null;
                Vector3? rUpperarm = (bones.Upperarms != null && bones.Upperarms.Length > 1) ? OraclePlayerManager.GetBonePos(bones.Upperarms[1]) : null;
                //小臂
                Vector3? lForearm = (bones.Forearms != null && bones.Forearms.Length > 0) ? OraclePlayerManager.GetBonePos(bones.Forearms[0]) : null;
                Vector3? rForearm = (bones.Forearms != null && bones.Forearms.Length > 1) ? OraclePlayerManager.GetBonePos(bones.Forearms[1]) : null;
                //手掌(手腕
                Vector3? lPalm = OraclePlayerManager.GetBonePos(bones.LeftPalm);
                Vector3? rPalm = OraclePlayerManager.GetBonePos(bones.RightPalm);
                //左腿
                Vector3? lThigh1 = OraclePlayerManager.GetBonePos(bones.LeftThigh1);
                Vector3? lKnee = OraclePlayerManager.GetBonePos(bones.LeftThigh2);
                Vector3? lCalf = null;
                Vector3? lFoot = null;
                //对左腿做Check, 顺位获取小腿和脚掌坐标(腿部结构和常规骨骼不同
                if (bones.LeftThigh2?.Original?.childCount > 0)
                {
                    Transform calfT = bones.LeftThigh2.Original.GetChild(0);
                    lCalf = calfT.position;

                    if (calfT.childCount > 0)
                    {
                        lFoot = calfT.GetChild(0).position;
                    }
                }
                //右腿
                Vector3? rThigh1 = OraclePlayerManager.GetBonePos(bones.RightThigh1);
                Vector3? rKnee = OraclePlayerManager.GetBonePos(bones.RightThigh2);
                Vector3? rCalf = null;
                Vector3? rFoot = null;
                //同左腿
                if (bones.RightThigh2?.Original?.childCount > 0)
                {
                    Transform calfT = bones.RightThigh2.Original.GetChild(0);
                    rCalf = calfT.position;

                    if (calfT.childCount > 0)
                    {
                        rFoot = calfT.GetChild(0).position;
                    }
                }
                //基于动态颜色叠加绘制
                //头部 (Head)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.Head, finalColor));
                DrawBoneLine(cam, head, neck);
                //胸部 (Chest)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.Chest, finalColor));
                DrawBoneLine(cam, neck, spine3);
                //胃部 (Stomach)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.Stomach, finalColor));
                DrawBoneLine(cam, spine3, pelvis);
                //左手 (LeftArm)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.LeftArm, finalColor));
                DrawBoneLine(cam, neck, lShoulder);
                DrawBoneLine(cam, lShoulder, lUpperarm);
                DrawBoneLine(cam, lUpperarm, lForearm);
                DrawBoneLine(cam, lForearm, lPalm);
                //右手 (RightArm)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.RightArm, finalColor));
                DrawBoneLine(cam, neck, rShoulder);
                DrawBoneLine(cam, rShoulder, rUpperarm);
                DrawBoneLine(cam, rUpperarm, rForearm);
                DrawBoneLine(cam, rForearm, rPalm);
                //左腿 (LeftLeg)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.LeftLeg, finalColor));
                DrawBoneLine(cam, pelvis, lThigh1);
                DrawBoneLine(cam, lThigh1, lKnee);
                DrawBoneLine(cam, lKnee, lCalf);
                DrawBoneLine(cam, lCalf, lFoot);
                //右腿 (RightLeg)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.RightLeg, finalColor));
                DrawBoneLine(cam, pelvis, rThigh1);
                DrawBoneLine(cam, rThigh1, rKnee);
                DrawBoneLine(cam, rKnee, rCalf);
                DrawBoneLine(cam, rCalf, rFoot);
            }
        }
        /// <summary>
        /// 绘制玩家信息
        /// </summary>
        /// <param name="cam">摄像机</param>
        /// <param name="textStyle">文本样式</param>
        public static void DrawPlayerText(Camera cam, GUIStyle textStyle)
        {
            //功能总开关
            if (!PlayerESPCfg.EnablePlayerESP.Value) return;
            //功能独立开关
            if (!PlayerESPCfg.EnablePlayerInfoESP.Value) return;
            //遍历绘制
            foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
            {
                //照常检查和计算距离, 同DrawPlayerBone
                if (player == null || player == PluginsCore.CorrectPlayer || player.PlayerBones == null) continue;
                if (!OracleCommon.IsInRange(PlayerESPCfg.PlayerESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
                {
                    continue;
                }
                //过滤队友
                string targetGroupId = player.Profile?.Info?.GroupId ?? "";
                bool isTeammate = !string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && targetGroupId == PluginsCore.CorrectGroupId;
                //获取头部坐标, 这样信息才能悬浮于头顶
                Vector3? headPos = OraclePlayerManager.GetBonePos(player.PlayerBones.Head);
                if (!headPos.HasValue) continue;
                //向头顶偏移防止和骨骼绘制重叠
                Vector3 textWorldPos = headPos.Value + new Vector3(0, 0.3f, 0);
                Vector3 textScreenPos = cam.WorldToScreenPoint(textWorldPos);
                //深度检查
                if (textScreenPos.z > 0.01f)
                {
                    var info = OraclePlayerManager.GetEntityInfo(player, isTeammate);
                    textStyle.richText = true;

                    float screenX = textScreenPos.x;
                    float screenY = Screen.height - textScreenPos.y;

                    GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), info.ToEspString(), textStyle);
                }
            }
        }
        /// <summary>
        /// 绘制玩家血条
        /// </summary>
        /// <param name="cam">摄像机</param>
        public static void DrawAllPlayerHealthBars(Camera cam)
        {
            //功能开关
            if (!PlayerESPCfg.EnablePlayerESP.Value) return;
            if (!PlayerESPCfg.EnablePlayerHealthBarESP.Value) return;
            //againandagainandagain....遍历和检查, 一模一样
            foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
            {
                if (player == null || player == PluginsCore.CorrectPlayer) continue;
                if (!OracleCommon.IsInRange(PlayerESPCfg.PlayerESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
                {
                    continue;
                }
                //绘制血条
                DrawPlayerHealthBar(cam, player);
            }
        }
        /// <summary>
        /// 绘制血条
        /// </summary>
        /// <param name="cam">摄像机</param>
        /// <param name="player">玩家实例</param>
        public static void DrawPlayerHealthBar(Camera cam, Player player)
        {
            //空指针防御
            if (player == null || player.HealthController == null) return;
            //读取脚底的坐标
            Vector3 feetWorldPos = player.Transform.position;
            Vector3 feetScreenPos = cam.WorldToScreenPoint(feetWorldPos);
            //深度检查
            if (feetScreenPos.z <= 0.01f) return;
            //血量获取
            OraclePlayerManager.GetPlayerTotalHealth(player, out float curHp, out float maxHp);
            //百分比变色
            if (maxHp <= 0) return;
            float hpPercent = curHp / maxHp;
            //反转Y轴适配坐标
            float screenX = feetScreenPos.x;
            float screenY = Screen.height - feetScreenPos.y;
            //排版
            float barWidth = 60f;
            float barHeight = 4f;
            float barX = screenX - (barWidth / 2f);
            float barY = screenY + 5f; // 放在脚底下边缘 5 像素的位置
            //绘制
            Color oldGuiColor = GUI.color;
            //暗灰色底槽背景
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), Texture2D.whiteTexture);
            //按百分比动态变化的前景色
            Color hpColor = Color.green;
            if (hpPercent < 0.5f) hpColor = Color.yellow;
            if (hpPercent < 0.25f) hpColor = Color.red;
            GUI.color = hpColor;
            GUI.DrawTexture(new Rect(barX, barY, barWidth * hpPercent, barHeight), Texture2D.whiteTexture);
            //还原颜色
            GUI.color = oldGuiColor;
        }
        
        /// <summary>
        /// 绘制骨骼连线
        /// </summary>
        /// <param name="cam">摄像机</param>
        /// <param name="p1">坐标1, 起始点</param>
        /// <param name="p2">坐标2, 结束点</param>
        public static void DrawBoneLine(Camera cam, Vector3? p1, Vector3? p2)
        {
            //如果有任何一个节点为空值, 则放弃绘制
            if (!p1.HasValue || !p2.HasValue) return;
            //三转二
            Vector3 s1 = cam.WorldToScreenPoint(p1.Value);
            Vector3 s2 = cam.WorldToScreenPoint(p2.Value);
            //深度检查
            //不加这个会导致贴脸的AI骨骼线满天飞
            if (s1.z > 0.01f && s2.z > 0.01f)
            {
                //反转Y轴适配坐标系
                GL.Vertex3(s1.x, Screen.height - s1.y, 0);
                GL.Vertex3(s2.x, Screen.height - s2.y, 0);
            }
        }
        
        /// <summary>
        /// 动态计算骨骼颜色
        /// </summary>
        /// <param name="player">玩家实例</param>
        /// <param name="part">肢体部位</param>
        /// <param name="baseColor">颜色</param>
        /// <returns></returns>
        public static Color GetDynamicLimbColor(Player player, EBodyPart part, Color baseColor)
        {
            //空指针防御和功能开关检查合并
            if (player == null || player.HealthController == null || !PlayerESPCfg.EnablePlayerBoneESPHealthMode.Value) return baseColor;
            //读取血量
            var bodyPartHealth = player.HealthController.GetBodyPartHealth(part, false);
            //肢体损毁
            if (bodyPartHealth.Current <= 0.01f)
            {
                return Color.magenta; // 肢体黑了，强制高亮紫
            }
            //计算
            float max = bodyPartHealth.Maximum;
            if (max <= 0) return baseColor;
            //计算血量百分比
            float healthPercent = bodyPartHealth.Current / max;
            //插值算法处理渐变
            //全蓝或许不太妥当, 这里可以用超上限颜色压制渐变色吗?
            //可以
            //还是有一点点问题
            //明天修改下方法定义, 根据骨骼原色传入渐变目标色试试
            //根据传入颜色确定目标颜色
            Color targetColor;
            if (baseColor == ColorSafe) // 当前是绿色
            {
                targetColor = Color.blue;
            }
            else if (baseColor == ColorWarning) // 当前是黄色
            {
                targetColor = Color.red;
            }
            else // 当前是红色 (ColorDangerous)
            {
                targetColor = new Color(0.35f, 0f, 0f);
            }
            float minLerp = 0.5f;
            float lerpFactor = Mathf.Lerp(minLerp, 1.0f, healthPercent);
            return Color.Lerp(targetColor, baseColor, lerpFactor);
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
    public class PlayerESPCfg : IOracleCfg, IOracleKeyUpdate
    {
        internal static ConfigEntry<bool> EnablePlayerESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerInfoESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerBoneESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerHealthBarESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerBoneESPHealthMode { get; set; }
        internal static ConfigEntry<int> PlayerESPMaxDistance { get; set; }
        internal static ConfigEntry<KeyCode> PlayerESPKey { get; set; }
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            PlayerESPKey = config.Bind<KeyCode>(
                "玩家透视",
                "玩家透视快捷键",
                KeyCode.F2,
                "按下切换玩家透视"
            );
            EnablePlayerESP = config.Bind<bool>(
                "玩家透视",
                "启用玩家透视",
                true,
                "玩家透视总开关，包括骨骼，玩家信息等"
            );
            EnablePlayerInfoESP = config.Bind<bool>(
                "玩家透视",
                "启用玩家信息透视",
                true,
                "可以透视玩家的信息，包括等级，阵营，名字等"
            );
            EnablePlayerHealthBarESP = config.Bind<bool>(
                "玩家透视",
                "启用玩家血条透视",
                true,
                "可以透视玩家的血条"
            );
            EnablePlayerBoneESPHealthMode = config.Bind<bool>(
                "玩家透视",
                "启用玩家骨骼透视血量叠加",
                true,
                "启用后透视骨骼会根据肢体的血量损耗向蓝色发生渐变，损毁的部位会变成紫色"
            );
            EnablePlayerBoneESP = config.Bind<bool>(
                "玩家透视",
                "启用玩家骨骼透视",
                true,
                "可以透视玩家骨骼，也就是经典的火柴人透视（真的有人会关掉它只启用别的功能吗……）"
            );
            PlayerESPMaxDistance = config.Bind<int>(
                "玩家透视",
                "透视范围",
                200,
                new ConfigDescription(
                    "透视可见的范围",
                    new AcceptableValueRange<int>(50, 2000)
                )
            );
        }
        public void RegisterKeyUpdate()
        {
            OracleEvent.OnUpdate += KeyUpdate;
        }
        public static void KeyUpdate()
        {
            if (Input.GetKeyDown(PlayerESPKey.Value))
            {
                EnablePlayerESP.Value = !EnablePlayerESP.Value;
                var value = EnablePlayerESP.Value;
                OracleNotify.Message($"玩家透视已{(value ? "启用" : "禁用")}!", value ? ENotificationIconType.Default : ENotificationIconType.Alert, GlobalCfg.MuteNotice.Value);
            }
        }
    }
}
