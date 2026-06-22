using CommonAssets.Scripts.Game.LabyrinthEvent;
using EFT;
using EFT.Interactive;
using EFT.SynchronizableObjects;
using Oracle.ESP;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine;

namespace Oracle.Data
{
    

    /// <summary>
    /// 玩家/实体数据引擎：处理所有的状态读取、射线检测、位置换算
    /// </summary>
    public static class OraclePlayerManager
    {
        //FOV计算参数
        public const float BotFovThreshold = 0.5f;

        //核心参数, 射线遮挡的层级计算, 通过对掩码位运算得到
        public static readonly int HighPolyWithTerrainMask =
            (1 << LayerMask.NameToLayer("Terrain")) |
            (1 << LayerMask.NameToLayer("HighPolyCollider"));

        public static string GetPlayerName(InfoClass info)
        {
            if (info == null) return "Nikita Buyanov";
            var role = info.Settings?.Role.ToString().ToLower() ?? "assault";
            //迷宫小弟、玩家和全英文名不经过转换，反向筛选西里尔字母
            return ((info.Side == EPlayerSide.Bear || info.Side == EPlayerSide.Usec) || (role == "tagillahelperagro") || OracleCommon.IsAllEnglish(info.Nickname)) ? info.Nickname : GStruct21.ConvertToLatinic(info.Nickname);
        }
        public static bool IsTeammate(InfoClass info)
        {
            if(info == null) return false;
            string targetGroupId = info?.GroupId ?? "";
            return !string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && targetGroupId == PluginsCore.CorrectGroupId;
        }

        public static EntityDisplayInfo GetEntityInfo(Player player, bool isTeammate, bool includeName = true)
        {
            var info = player.Profile?.Info;
            string name = "Nikita Buyanov";
            string sideText = "Unheard";
            string level = "";

            // 距离计算
            int distance = Mathf.RoundToInt(Vector3.Distance(PluginsCore.CorrectPlayer.Transform.position, player.Transform.position));

            if (info != null)
            {
                name = GetPlayerName(info);

                // ⭐ 一行代码，直接把 sideText 和 level 传进去让它处理好再吐出来
                DeterminePlayerText(info, name, isTeammate, includeName, out sideText, out level);
            }

            return new EntityDisplayInfo(name, sideText, level, distance);
        }
        /// <summary>
        /// 统一处理玩家与AI的文本和颜色标签
        /// </summary>
        public static void DeterminePlayerText(InfoClass info, string name, bool isTeammate, bool includeName, out string sideText, out string levelText)
        {
            sideText = "Unheard";
            levelText = ""; // 默认 AI 没有等级

            if (info.Side.ToString() == "Savage")
            {
                // ==========================================
                // AI / Scav 阵营处理逻辑
                // ==========================================
                var role = info.Settings?.Role.ToString().ToLower() ?? "assault";
                // 使用变量存储核心标识，不再重复拼接颜色标签
                string roleLabel = "Scav";
                string colorHex = OracleColorManager.Scav;

                //这里是不是可以加上多角色适配？
                // 核心优先级逻辑
                if (role.Contains("boss") || IsSpecialBoss(role)) { roleLabel = "Boss"; colorHex = OracleColorManager.Boss; }
                else if (role == "bossboarsniper" || role == "marksman") { roleLabel = "狙击Scav"; colorHex = OracleColorManager.Sniper; }
                else if (role == "pmcbot" || role == "exusec") { roleLabel = "美军"; colorHex = OracleColorManager.Raider; }
                else if (role.Contains("follower") || role == "tagillahelperagro") { roleLabel = "护卫"; colorHex = OracleColorManager.Follower; }
                else if (role.Contains("sectant")) { roleLabel = "邪教徒"; colorHex = OracleColorManager.Sectant; }
                else if (role == "gifter") { roleLabel = "圣诞老人"; colorHex = OracleColorManager.Santa; }
                else if (role.Contains("btr")) { roleLabel = "BTR"; colorHex = OracleColorManager.BTR; }
                else if (role.Contains("black")) { roleLabel = "黑狐"; colorHex = OracleColorManager.BlackDiv; }

                // 组合名称部分
                string displayString = includeName ? $"{roleLabel} {name}" : roleLabel;
                string finalRes = $"<color={colorHex}>{displayString}</color>";

                // 组合友军部分
                sideText = isTeammate ? $"<color={OracleColorManager.AllyPlayer}>友军 </color>{finalRes}" : finalRes;
            }
            else
            {
                // ==========================================
                // PMC (真实玩家) 阵营处理逻辑
                // ==========================================
                levelText = $"<color={OracleColorManager.PlayerLevel}>{info.Level}级</color>";
                string color = info.Side.ToString() == "Usec" ? OracleColorManager.PMCUSEC : OracleColorManager.PMCBEAR;

                string displayContent = includeName ? $"{info.Side} {name}" : info.Side.ToString();
                string baseText = $"<color={color}>{displayContent}</color>";

                sideText = isTeammate ? $"<color={OracleColorManager.AllyPlayer}>友军 </color>{baseText}" : baseText;
            }
        }

        private static bool IsSpecialBoss(string role)
        {
            return role == "followerbirdeye" || role == "followerbigpipe" ||
                   role == "infectedtagilla" || role == "sectantoni" ||
                   role == "sectantpredvestnik" || role == "sectantprizark";
        }

        /// <summary>
        /// 提取Transform的坐标
        /// </summary>
        /// <param name="t">transform</param>
        /// <returns></returns>
        public static Vector3? GetBonePos(Transform t)
        {
            if (t == null) return null;
            return t.position;
        }
        /// <summary>
        /// 使用.Original安全提取BifacialTransform坐标
        /// </summary>
        /// <param name="bt">部分骨骼特有的BifacialTransform</param>
        /// <returns></returns>
        public static Vector3? GetBonePos(BifacialTransform bt)
        {
            if (bt == null || bt.Original == null) return null;
            return bt.Original.position;
        }

        /// <summary>
        /// 判断AI是否在玩家可见范围
        /// </summary>
        /// <param name="camPosition">相机坐标</param>
        /// <param name="targetPlayer">目标玩家实例</param>
        /// <param name="obstacleLayerMask">射线判断层级</param>
        /// <returns></returns>
        public static bool IsPlayerVisible(Vector3 camPosition, Player targetPlayer, int obstacleLayerMask)
        {
            //空指针防御
            if (targetPlayer == null || targetPlayer.PlayerBones == null) return false;
            //提取节点
            var bones = targetPlayer.PlayerBones;
            //头腰臀三个关键点(好像是胸? 问题不大)
            Transform[] checkBones = {
                bones.Head?.Original,
                bones.Spine3?.Original,
                bones.Pelvis?.Original
            };
            //遍历三个关键点做射线检测
            foreach (Transform bone in checkBones)
            {
                //空指针防御
                if (bone == null) continue;
                //射线检测
                //三个点任有一个可见则返回true
                if (!Physics.Linecast(camPosition, bone.position, obstacleLayerMask))
                {
                    return true;
                }
            }
            //三个点均不可见, 返回false
            return false;
        }

        /// <summary>
        /// 判断AI是否可以看到玩家, 原理和上面的方法大致相同, 但更加严谨
        /// </summary>
        /// <param name="bot">AI玩家实例</param>
        /// <param name="localPlayer">玩家</param>
        /// <param name="obstacleLayerMask">射线层级</param>
        /// <returns></returns>
        public static bool IsBotVisible(Player bot, Player localPlayer, int obstacleLayerMask)
        {
            if (bot == null || localPlayer == null || bot.PlayerBones == null) return false;
            //取得AI的头部坐标
            Transform botEyePoint = bot.PlayerBones.Head?.Original;
            if (botEyePoint == null) return false;
            //取得玩家的胸部坐标
            Transform localPlayerChest = localPlayer.PlayerBones.Spine3?.Original;
            if (localPlayerChest == null) return false;
            //经过讨论, 虽然使用三点判断优化了遮挡判定, 但FOV模拟仍有价值, 因此保留
            //计算AI和玩家胸口的直线距离
            Vector3 targetDir = (localPlayerChest.position - botEyePoint.position).normalized;
            //获取AI的视野朝向
            Vector3 botLookDir = bot.LookDirection;
            if (botLookDir == Vector3.zero)
            {
                //完美正对
                botLookDir = botEyePoint.forward; // 极小概率的兜底预案
            }

            //点积计算向量, 传参为0.5f, 模拟120度视角的FOV
            if (Vector3.Dot(botLookDir, targetDir) < BotFovThreshold) return false;
            //三点检测, 原理和上面方法一致
            Transform[] myCriticalParts = {
                localPlayer.PlayerBones.Head?.Original,
                localPlayerChest,
                localPlayer.PlayerBones.Pelvis?.Original
            };
            //AI到玩家的射线检测
            foreach (Transform myPart in myCriticalParts)
            {
                if (myPart == null) continue;
                if (!Physics.Linecast(botEyePoint.position, myPart.position, obstacleLayerMask))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 安全获取玩家总血量和生命上限
        /// </summary>
        /// <param name="player">玩家实例</param>
        /// <param name="currentHp">当前生命值</param>
        /// <param name="maxHp">生命值上限</param>
        public static void GetPlayerTotalHealth(Player player, out float currentHp, out float maxHp)
        {
            currentHp = 0f;
            maxHp = 0f;
            //空指针防御
            if (player == null || player.HealthController == null) return;
            //部位定义
            EBodyPart[] parts = {
                EBodyPart.Head,
                EBodyPart.Chest,
                EBodyPart.Stomach,
                EBodyPart.LeftArm,
                EBodyPart.RightArm,
                EBodyPart.LeftLeg,
                EBodyPart.RightLeg
            };
            foreach (EBodyPart part in parts)
            {
                var partHealth = player.HealthController.GetBodyPartHealth(part, false);
                currentHp += partHealth.Current;
                maxHp += partHealth.Maximum;
            }
        }

    }
}