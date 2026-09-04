using EFT;
using Oracle.Utils;
using UnityEngine;

namespace Oracle.Data
{
    

    /// <summary>
    /// 玩家数据总线
    /// </summary>
    public static class OraclePlayerDataManager
    {
        //FOV计算参数
        public const float BotFovThreshold = 0.5f;

        //核心参数, 射线遮挡的层级计算, 通过对掩码位运算得到
        public static readonly int HighPolyWithTerrainMask =
            (1 << LayerMask.NameToLayer("Terrain")) |
            (1 << LayerMask.NameToLayer("HighPolyCollider"));

        /// <summary>
        /// 取玩家名字
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public static string GetPlayerName(ProfileInfo info)
        {
            if (info == null) return "Nikita Buyanov";
            var role = info.Settings?.Role.ToString().ToLower() ?? "assault";
            //迷宫小弟、玩家和全英文名不经过转换，反向筛选西里尔字母
            return ((info.Side == EPlayerSide.Bear || info.Side == EPlayerSide.Usec) || (role == "tagillahelperagro") || OracleCommon.IsAllEnglish(info.Nickname)) ? info.Nickname : DebugGroupStruct.ConvertToLatinic(info.Nickname);
        }

        /// <summary>
        /// 敌我识别
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public static bool IsTeammate(ProfileInfo info)
        {
            if(info == null) return false;
            string targetGroupId = info?.GroupId ?? "";
            return !string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && targetGroupId == PluginsCore.CorrectGroupId;
        }

        /// <summary>
        /// 组合玩家数据
        /// </summary>
        /// <param name="player"></param>
        /// <param name="isTeammate"></param>
        /// <param name="includeName"></param>
        /// <returns></returns>

        public static EntityDisplayInfo GetEntityInfo(Player player, bool isTeammate, bool includeName = true)
        {
            var info = player.Profile?.Info;
            //默认Fallback
            string name = "Nikita Buyanov";
            string sideText = "Unheard";
            string level = "";

            //距离求解
            int distance = Mathf.RoundToInt(Vector3.Distance(PluginsCore.CorrectPlayer.Transform.position, player.Transform.position));

            if (info != null)
            {
                name = GetPlayerName(info);

                DeterminePlayerText(info, name, isTeammate, includeName, out sideText, out level);
            }

            return new EntityDisplayInfo(name, sideText, level, distance);
        }
        /// <summary>
        /// 叠加层数据桥专用：输出纯文本（无颜色标签）+ 独立颜色，供窗口原生绘制直接取用。
        /// 角色/阵营色逻辑与 DeterminePlayerText 保持一致（镜像，不修改 OnGUI 路径）。
        /// </summary>
        /// <param name="info">玩家信息</param>
        /// <param name="name">玩家名</param>
        /// <param name="isTeammate">是否友军</param>
        /// <param name="includeName">是否包含名字</param>
        /// <param name="levelText">纯文本等级 "Lv.45"（Scav 为空串）</param>
        /// <param name="levelColor">等级色</param>
        /// <param name="teammateText">纯文本友军前缀 "友军 "（非友军为空串）</param>
        /// <param name="teammateColor">友军色</param>
        /// <param name="sideText">纯文本阵营段 "USEC John" / "Boss Killa"</param>
        /// <param name="sideColor">阵营段颜色</param>
        public static void GetPlayerOverlayLabel(ProfileInfo info, string name, bool isTeammate, bool includeName,
            out string levelText, out OracleColor levelColor,
            out string teammateText, out OracleColor teammateColor,
            out string sideText, out OracleColor sideColor)
        {
            levelText = "";
            levelColor = OracleColorManager.PlayerLevel;
            teammateText = isTeammate ? "text_esp_player_tag_teammate".i18n() : "";
            teammateColor = OracleColorManager.AllyPlayer;
            sideText = name;
            sideColor = OracleColorManager.Scav;

            if (info == null) return;

            if (info.Side.ToString() == "Savage")
            {
                //取角色
                var role = info.Settings?.Role.ToString().ToLower() ?? "assault";

                string roleLabel = "text_esp_player_tag_scav".i18n();
                OracleColor colorHex = OracleColorManager.Scav;

                //镜像 DeterminePlayerText 的核心优先级逻辑
                if (role.Contains("boss") || IsSpecialBoss(role)) { roleLabel = "text_esp_player_tag_boss".i18n(); colorHex = OracleColorManager.Boss; }
                else if (role == "bossboarsniper" || role == "marksman") { roleLabel = "text_esp_player_tag_sniper".i18n(); colorHex = OracleColorManager.Sniper; }
                else if (role == "pmcbot") { roleLabel = "text_esp_player_tag_raider".i18n(); colorHex = OracleColorManager.Raider; }
                else if (role == "exusec") { roleLabel = "text_esp_player_tag_rogue".i18n(); colorHex = OracleColorManager.Raider; }
                else if (role.Contains("follower") || role == "tagillahelperagro") { roleLabel = "text_esp_player_tag_follower".i18n(); colorHex = OracleColorManager.Follower; }
                else if (role.Contains("sectant")) { roleLabel = "text_esp_player_tag_sectant".i18n(); colorHex = OracleColorManager.Sectant; }
                else if (role == "gifter") { roleLabel = "text_esp_player_tag_santa".i18n(); colorHex = OracleColorManager.Santa; }
                else if (role.Contains("btr")) { roleLabel = "text_esp_player_tag_btr".i18n(); colorHex = OracleColorManager.BTR; }
                else if (role.Contains("black")) { roleLabel = "text_esp_player_tag_bd".i18n(); colorHex = OracleColorManager.BlackDiv; }

                sideText = includeName ? $"{roleLabel} {name}" : roleLabel;
                sideColor = colorHex;
            }
            else
            {
                levelText = string.Format("text_esp_overlay_player_level".i18n(), info.Level);
                sideColor = info.Side.ToString() == "Usec" ? OracleColorManager.PMCUSEC : OracleColorManager.PMCBEAR;
                sideText = includeName ? $"{info.Side} {name}" : info.Side.ToString();
            }
        }

        /// <summary>
        /// 获取玩家数据富文本
        /// </summary>
        public static void DeterminePlayerText(ProfileInfo info, string name, bool isTeammate, bool includeName, out string sideText, out string levelText)
        {
            sideText = "Unheard";
            levelText = "";

            if (info.Side.ToString() == "Savage")
            {
                //取角色
                var role = info.Settings?.Role.ToString().ToLower() ?? "assault";

                string roleLabel = "text_esp_player_tag_scav".i18n();
                string colorHex = OracleColorManager.Scav;

                //这里是不是可以加上多角色适配？
                // 核心优先级逻辑
                if (role.Contains("boss") || IsSpecialBoss(role)) { roleLabel = "text_esp_player_tag_boss".i18n(); colorHex = OracleColorManager.Boss; }
                else if (role == "bossboarsniper" || role == "marksman") { roleLabel = "text_esp_player_tag_sniper".i18n(); colorHex = OracleColorManager.Sniper; }
                else if (role == "pmcbot") { roleLabel = "text_esp_player_tag_raider".i18n(); colorHex = OracleColorManager.Raider; }
                else if (role == "exusec") { roleLabel = "text_esp_player_tag_rogue".i18n(); colorHex = OracleColorManager.Raider; }
                else if (role.Contains("follower") || role == "tagillahelperagro") { roleLabel = "text_esp_player_tag_follower".i18n(); colorHex = OracleColorManager.Follower; }
                else if (role.Contains("sectant")) { roleLabel = "text_esp_player_tag_sectant".i18n(); colorHex = OracleColorManager.Sectant; }
                else if (role == "gifter") { roleLabel = "text_esp_player_tag_santa".i18n(); colorHex = OracleColorManager.Santa; }
                else if (role.Contains("btr")) { roleLabel = "text_esp_player_tag_btr".i18n(); colorHex = OracleColorManager.BTR; }
                else if (role.Contains("black")) { roleLabel = "text_esp_player_tag_bd".i18n(); colorHex = OracleColorManager.BlackDiv; }

                // 组合名称部分
                string displayString = includeName ? $"{roleLabel} {name}" : roleLabel;
                string finalRes = $"<color={colorHex}>{displayString}</color>";

                // 组合友军部分
                sideText = isTeammate ? $"<color={OracleColorManager.AllyPlayer}>{"text_esp_player_tag_teammate".i18n()} </color>{finalRes}" : finalRes;
            }
            else
            {
                levelText = string.Format("text_esp_player_level".i18n(), OracleColorManager.PlayerLevel, info.Level);
                string color = info.Side.ToString() == "Usec" ? OracleColorManager.PMCUSEC : OracleColorManager.PMCBEAR;

                string displayContent = includeName ? $"{info.Side} {name}" : info.Side.ToString();
                string baseText = $"<color={color}>{displayContent}</color>";

                sideText = isTeammate ? $"<color={OracleColorManager.AllyPlayer}>{"text_esp_player_tag_teammate".i18n()} </color>{baseText}" : baseText;
            }
        }

        //不含boss字符串的boss单位
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