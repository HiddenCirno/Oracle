using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Oracle.ESP
{
    public class PlayerESP
    {
        //颜色定义
        public static readonly Color ColorSafe = Color.green; //隔墙不可见
        public static readonly Color ColorWarning = Color.yellow; //你可以看到它, 而它没有看你
        public static readonly Color ColorDangerous = Color.red; //你看得到它并且它看得到你
        //FOV计算参数
        public const float BotFovThreshold = 0.5f;
        //核心参数, 射线遮挡的层级计算, 通过对掩码位运算得到
        public static readonly int HighPolyWithTerrainMask =
            (1 << LayerMask.NameToLayer("Terrain")) |
            (1 << LayerMask.NameToLayer("HighPolyCollider"));
        //核心绘制方法
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
                if (!IsInRange(PlayerESPCfg.ESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
                {
                    continue;
                }
                //初始化透视颜色
                Color finalColor;
                //玩家能否看到AI(射线计算
                bool canPlayerSeeBot = IsPlayerVisible(cam.transform.position, player, HighPolyWithTerrainMask);
                //AI能否看到玩家(射线计算
                bool canBotSeePlayer = IsBotVisible(player, PluginsCore.CorrectPlayer, HighPolyWithTerrainMask);
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
                Vector3? head = GetBonePos(bones.Head);
                Vector3? neck = GetBonePos(bones.Neck);
                Vector3? spine3 = GetBonePos(bones.Spine3);
                Vector3? pelvis = GetBonePos(bones.Pelvis);
                //肩膀
                Vector3? lShoulder = GetBonePos(bones.LeftShoulder);
                Vector3? rShoulder = GetBonePos(bones.RightShoulder);
                //大臂
                Vector3? lUpperarm = (bones.Upperarms != null && bones.Upperarms.Length > 0) ? GetBonePos(bones.Upperarms[0]) : null;
                Vector3? rUpperarm = (bones.Upperarms != null && bones.Upperarms.Length > 1) ? GetBonePos(bones.Upperarms[1]) : null;
                //小臂
                Vector3? lForearm = (bones.Forearms != null && bones.Forearms.Length > 0) ? GetBonePos(bones.Forearms[0]) : null;
                Vector3? rForearm = (bones.Forearms != null && bones.Forearms.Length > 1) ? GetBonePos(bones.Forearms[1]) : null;
                //手掌(手腕
                Vector3? lPalm = GetBonePos(bones.LeftPalm);
                Vector3? rPalm = GetBonePos(bones.RightPalm);
                //左腿
                Vector3? lThigh1 = GetBonePos(bones.LeftThigh1);
                Vector3? lKnee = GetBonePos(bones.LeftThigh2);
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
                Vector3? rThigh1 = GetBonePos(bones.RightThigh1);
                Vector3? rKnee = GetBonePos(bones.RightThigh2);
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
                // 1. 头部 (Head)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.Head, finalColor));
                DrawBoneLine(cam, head, neck);
                // 2. 胸部 (Chest)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.Chest, finalColor));
                DrawBoneLine(cam, neck, spine3);
                // 3. 胃部 (Stomach)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.Stomach, finalColor));
                DrawBoneLine(cam, spine3, pelvis);
                // 4. 左手 (LeftArm)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.LeftArm, finalColor));
                DrawBoneLine(cam, neck, lShoulder);
                DrawBoneLine(cam, lShoulder, lUpperarm);
                DrawBoneLine(cam, lUpperarm, lForearm);
                DrawBoneLine(cam, lForearm, lPalm);
                // 5. 右手 (RightArm)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.RightArm, finalColor));
                DrawBoneLine(cam, neck, rShoulder);
                DrawBoneLine(cam, rShoulder, rUpperarm);
                DrawBoneLine(cam, rUpperarm, rForearm);
                DrawBoneLine(cam, rForearm, rPalm);
                // 6. 左腿 (LeftLeg)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.LeftLeg, finalColor));
                DrawBoneLine(cam, pelvis, lThigh1);
                DrawBoneLine(cam, lThigh1, lKnee);
                DrawBoneLine(cam, lKnee, lCalf);
                DrawBoneLine(cam, lCalf, lFoot);
                // 7. 右腿 (RightLeg)
                GL.Color(GetDynamicLimbColor(player, EBodyPart.RightLeg, finalColor));
                DrawBoneLine(cam, pelvis, rThigh1);
                DrawBoneLine(cam, rThigh1, rKnee);
                DrawBoneLine(cam, rKnee, rCalf);
                DrawBoneLine(cam, rCalf, rFoot);
            }
        }
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
                if (!IsInRange(PlayerESPCfg.ESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
                {
                    continue;
                }
                //获取头部坐标, 这样信息才能悬浮于头顶
                Vector3? headPos = GetBonePos(player.PlayerBones.Head);
                if (!headPos.HasValue) continue;
                //向头顶偏移防止和骨骼绘制重叠
                Vector3 textWorldPos = headPos.Value + new Vector3(0, 0.3f, 0);
                Vector3 textScreenPos = cam.WorldToScreenPoint(textWorldPos);
                //深度检查
                if (textScreenPos.z > 0.01f)
                {
                    //计算直线距离
                    int distance = Mathf.RoundToInt(Vector3.Distance(PluginsCore.CorrectPlayer.Transform.position, player.Transform.position));
                    //提取玩家信息
                    string name = "Unknown";
                    string side = "Bot";
                    if (player.Profile != null && player.Profile.Info != null)
                    {
                        name = player.Profile.Info.Nickname; //需要一个Locale转换, 等会找找在哪, 应该在狗牌生成部分
                        side = player.Profile.Info.Side.ToString();
                        //动态改变字体颜色以区分阵营
                        if (side == "Savage")
                        //Scav/Boss/AI
                        {
                            textStyle.normal.textColor = Color.yellow;
                        }
                        else
                        //PMC
                        //塔科夫严格意义上的阵营只有PMC和Scav两种, PMC之外的所有类型的AI都是Savage靠botRole做区分的
                        {
                            textStyle.normal.textColor = Color.red;
                        }
                    }
                    //合并字符串
                    //其实想改改, 比如把阵营什么的颜色显示分开, 不知道能不能直接用<color>标签
                    string espText = $"[{side}] {name} [{distance}m]";
                    //转换坐标并绘制
                    float screenX = textScreenPos.x;
                    float screenY = Screen.height - textScreenPos.y;
                    //用Rect绘制一个不可见方框, 保证文本居中
                    GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), espText, textStyle);

                    DrawPlayerHealthBar(cam, player);

                }
            }
        }
        public static void DrawPlayerHealthBar(Camera cam, Player player)
        {
            // 防空检查
            if (player == null || player.HealthController == null) return;

            // 1. 获取脚底的物理 3D 坐标
            Vector3 feetWorldPos = player.Transform.position;
            Vector3 feetScreenPos = cam.WorldToScreenPoint(feetWorldPos);

            // 2. 深度/背身检查：如果人在背后，直接不画
            if (feetScreenPos.z <= 0.01f) return;

            // 3. 获取血量
            GetPlayerTotalHealth(player, out float curHp, out float maxHp);
            if (maxHp <= 0) return;

            float hpPercent = curHp / maxHp;

            // 4. 转换 GUI 坐标系 (反转 Y 轴)
            float screenX = feetScreenPos.x;
            float screenY = Screen.height - feetScreenPos.y;

            // 5. 排版设定：固定宽度，居中对齐，稍微向下偏移防止踩线
            float barWidth = 60f;
            float barHeight = 4f;
            float barX = screenX - (barWidth / 2f);
            float barY = screenY + 5f; // 放在脚底下边缘 5 像素的位置

            // 6. 开始绘制 (必须保存和还原 GUI.color)
            Color oldGuiColor = GUI.color;

            // 绘制背景 (暗灰色底槽)
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), Texture2D.whiteTexture);

            // 绘制前景 (按比例缩放，动态变色)
            Color hpColor = Color.green;
            if (hpPercent < 0.5f) hpColor = Color.yellow;
            if (hpPercent < 0.25f) hpColor = Color.red;

            GUI.color = hpColor;
            GUI.DrawTexture(new Rect(barX, barY, barWidth * hpPercent, barHeight), Texture2D.whiteTexture);

            // 还原环境颜色
            GUI.color = oldGuiColor;
        }
        //提取Transform的坐标
        public static Vector3? GetBonePos(Transform t)
        {
            if (t == null) return null;
            return t.position;
        }
        //安全提取BifacialTransform坐标
        //使用了.Original
        public static Vector3? GetBonePos(BifacialTransform bt)
        {
            if (bt == null || bt.Original == null) return null;
            return bt.Original.position;
        }

        //核心方法, 绘制骨骼连线
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
        //距离判断, O(1)单步搞定
        public static bool IsInRange(int maxDistance, Vector3 p1, Vector3 p2)
        {
            return (p1 - p2).sqrMagnitude <= maxDistance * maxDistance;
        }
        //判断AI是否在玩家可见范围
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
        //判断AI是否可以看到玩家, 原理和上面的方法大致相同, 但更加严谨
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
        // 安全获取玩家全身总血量和总生命上限
        public static void GetPlayerTotalHealth(Player player, out float currentHp, out float maxHp)
        {
            currentHp = 0f;
            maxHp = 0f;

            if (player == null || player.HealthController == null) return;

            // 塔科夫真实的 7 个受击判定区
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
        public static Color GetDynamicLimbColor(Player player, EBodyPart part, Color baseColor)
        {
            if (player == null || player.HealthController == null || !PlayerESPCfg.EnablePlayerHealthBarESP.Value) return baseColor;

            var bodyPartHealth = player.HealthController.GetBodyPartHealth(part, false);

            // 1. 损毁判定 (血量归零)
            if (bodyPartHealth.Current <= 0.01f)
            {
                return Color.magenta; // 肢体黑了，强制高亮紫
            }

            float max = bodyPartHealth.Maximum;
            if (max <= 0) return baseColor;

            // 2. 计算血量百分比 (0.0 到 1.0)
            float healthPercent = bodyPartHealth.Current / max;

            // 3. ⭐ 神奇的 Color.Lerp 渐变魔法
            // 参数1：目标颜色 (大残时的蓝色 Color.blue 或 Color.cyan)
            // 参数2：基础颜色 (满血时的红/黄/绿)
            // 参数3：插值比例 (血量百分比)
            // 当 healthPercent = 1 (满血) 时，完全显示 baseColor
            // 当 healthPercent 接近 0 (大残) 时，无限趋近于 Color.blue
            return Color.Lerp(Color.blue, baseColor, healthPercent);
        }
    }
    public class PlayerESPCfg
    {
        //Config定义
        internal static ConfigEntry<bool> EnablePlayerESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerInfoESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerBoneESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerHealthBarESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerBoneESPHealthMode { get; set; }
        internal static ConfigEntry<int> ESPMaxDistance { get; set; }
        public static void Initialize(ConfigFile config)
        {
            EnablePlayerESP = config.Bind<bool>(
                "透视设置",
                "启用玩家透视",
                true,
                "玩家透视总开关，包括骨骼，玩家信息等"
            );
            EnablePlayerInfoESP = config.Bind<bool>(
                "透视设置",
                "启用玩家信息透视",
                true,
                "可以透视玩家的信息，包括等级，阵营，名字等"
            );
            EnablePlayerHealthBarESP = config.Bind<bool>(
                "透视设置",
                "启用玩家血条透视",
                true,
                "可以透视玩家的血条"
            );
            EnablePlayerBoneESPHealthMode = config.Bind<bool>(
                "透视设置",
                "启用玩家骨骼透视血量叠加",
                true,
                "启用后透视骨骼会根据肢体的血量损耗向蓝色发生渐变，损毁的部位会变成紫色"
            );
            EnablePlayerBoneESP = config.Bind<bool>(
                "透视设置",
                "启用玩家骨骼透视",
                true,
                "可以透视玩家骨骼，也就是经典的火柴人透视（真的有人会关掉它只启用别的功能吗……）"
            );
            ESPMaxDistance = config.Bind<int>(
                "透视设置",
                "透视范围",
                200,
                new ConfigDescription(
                    "透视可见的范围",
                    new AcceptableValueRange<int>(50, 1000)
                )
            );
        }
    }
}
