using BepInEx.Configuration;
using EFT;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Oracle.ESP
{
    public class PlayerESP
    {// ⭐ 新增：定义警告颜色
        public static readonly Color ColorSafe = Color.green; // 安全：他没看你
        public static readonly Color ColorLooking = Color.yellow; // 警告：他在看你的方向
        public static readonly Color ColorWarning = Color.red; // 危险：他真的能看到你！

        public const float BotFovThreshold = 0.5f;
        // 高聚合/地形层掩码
        public static readonly int HighPolyWithTerrainMask =
            (1 << LayerMask.NameToLayer("Terrain")) |
            (1 << LayerMask.NameToLayer("HighPolyCollider"));
        public static void DrawPlayerBone(Camera cam)
        {
            if (!PlayerESPCfg.EnablePlayerESP.Value) return;
            foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
            {
                if (player == null || player == PluginsCore.CorrectPlayer || player.PlayerBones == null) continue;

                // ⭐ 优化 3：算出距离，如果超距，用 continue 跳过这“一个”玩家，而不是 break 整个循环！
                if (!IsInRange(PlayerESPCfg.ESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
                {
                    continue;
                }
                Color finalColor;
                bool weCanSeeHim = IsPlayerVisible(cam.transform.position, player, HighPolyWithTerrainMask);

                // 2. 再用预警方法判定他能不能看到我们
                bool heSeesUs = IsBotLookingAtUs(player, PluginsCore.CorrectPlayer, HighPolyWithTerrainMask);

                // 根据预警状态设定颜色
                if (heSeesUs)
                {
                    finalColor = ColorWarning; // 亮红色：他能看到你！最危险！
                }
                else if (weCanSeeHim)
                {
                    finalColor = ColorLooking; // 亮绿色：我们能看到他，但他没看我们。安全。
                }
                else
                {
                    // 双方都没看到，把颜色变暗（比如暗绿色或半透明绿色）
                    finalColor = ColorSafe;
                }
                GL.Color(finalColor);

                // 为了防止代码冗长，提取一下 bones 引用
                var bones = player.PlayerBones;

                // ==========================================
                // 阶段 1：数据提取 (获取所有骨骼的三维空间坐标)
                // ==========================================

                // --- 躯干 ---
                Vector3? head = GetBonePos(bones.Head);
                Vector3? neck = GetBonePos(bones.Neck);
                Vector3? spine3 = GetBonePos(bones.Spine3);
                Vector3? pelvis = GetBonePos(bones.Pelvis);

                // --- 手臂 ---
                Vector3? lShoulder = GetBonePos(bones.LeftShoulder);
                Vector3? rShoulder = GetBonePos(bones.RightShoulder);

                Vector3? lUpperarm = (bones.Upperarms != null && bones.Upperarms.Length > 0) ? GetBonePos(bones.Upperarms[0]) : null;
                Vector3? rUpperarm = (bones.Upperarms != null && bones.Upperarms.Length > 1) ? GetBonePos(bones.Upperarms[1]) : null;

                Vector3? lForearm = (bones.Forearms != null && bones.Forearms.Length > 0) ? GetBonePos(bones.Forearms[0]) : null;
                Vector3? rForearm = (bones.Forearms != null && bones.Forearms.Length > 1) ? GetBonePos(bones.Forearms[1]) : null;

                Vector3? lPalm = GetBonePos(bones.LeftPalm);
                Vector3? rPalm = GetBonePos(bones.RightPalm);

                // --- 左腿 (顺藤摸瓜抓取小腿和脚) ---
                Vector3? lThigh1 = GetBonePos(bones.LeftThigh1);
                Vector3? lKnee = GetBonePos(bones.LeftThigh2);
                Vector3? lCalf = null;
                Vector3? lFoot = null;

                // 简化后的安全检查，利用 ?. 运算符，功能完全等同于你之前的长判断
                if (bones.LeftThigh2?.Original?.childCount > 0)
                {
                    Transform calfT = bones.LeftThigh2.Original.GetChild(0);
                    lCalf = calfT.position;

                    if (calfT.childCount > 0)
                    {
                        lFoot = calfT.GetChild(0).position;
                    }
                }

                // --- 右腿 (顺藤摸瓜抓取小腿和脚) ---
                Vector3? rThigh1 = GetBonePos(bones.RightThigh1);
                Vector3? rKnee = GetBonePos(bones.RightThigh2);
                Vector3? rCalf = null;
                Vector3? rFoot = null;

                if (bones.RightThigh2?.Original?.childCount > 0)
                {
                    Transform calfT = bones.RightThigh2.Original.GetChild(0);
                    rCalf = calfT.position;

                    if (calfT.childCount > 0)
                    {
                        rFoot = calfT.GetChild(0).position;
                    }
                }

                // ==========================================
                // 阶段 2：疯狂连线 (根据获取到的坐标绘制骨架拓扑)
                // ==========================================

                // 躯干中轴线
                DrawBoneLine(cam, head, neck);
                DrawBoneLine(cam, neck, spine3);
                DrawBoneLine(cam, spine3, pelvis);

                // 左臂
                DrawBoneLine(cam, neck, lShoulder);
                DrawBoneLine(cam, lShoulder, lUpperarm);
                DrawBoneLine(cam, lUpperarm, lForearm);
                DrawBoneLine(cam, lForearm, lPalm);

                // 右臂
                DrawBoneLine(cam, neck, rShoulder);
                DrawBoneLine(cam, rShoulder, rUpperarm);
                DrawBoneLine(cam, rUpperarm, rForearm);
                DrawBoneLine(cam, rForearm, rPalm);

                // 左腿 (骨盆 -> 大腿根 -> 膝盖 -> 小腿 -> 脚)
                DrawBoneLine(cam, pelvis, lThigh1);
                DrawBoneLine(cam, lThigh1, lKnee);
                DrawBoneLine(cam, lKnee, lCalf);
                DrawBoneLine(cam, lCalf, lFoot);

                // 右腿 (骨盆 -> 大腿根 -> 膝盖 -> 小腿 -> 脚)
                DrawBoneLine(cam, pelvis, rThigh1);
                DrawBoneLine(cam, rThigh1, rKnee);
                DrawBoneLine(cam, rKnee, rCalf);
                DrawBoneLine(cam, rCalf, rFoot);
            }
        }
        public static void DrawPlayerText(Camera cam, GUIStyle textStyle)
        {
            if (!PlayerESPCfg.EnablePlayerESP.Value) return;
            foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
            {
                if (player == null || player == PluginsCore.CorrectPlayer || player.PlayerBones == null) continue;

                // ⭐ 优化 3：算出距离，如果超距，用 continue 跳过这“一个”玩家，而不是 break 整个循环！
                if (!IsInRange(PlayerESPCfg.ESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
                {
                    continue;
                }

                // 必须要拿到头部坐标，字才能悬浮在头上
                Vector3? headPos = GetBonePos(player.PlayerBones.Head);
                if (!headPos.HasValue) continue;

                // 向上偏移 0.3 米，防止和骨骼线重叠
                Vector3 textWorldPos = headPos.Value + new Vector3(0, 0.3f, 0);
                Vector3 textScreenPos = cam.WorldToScreenPoint(textWorldPos);

                // 深度防背身检查
                if (textScreenPos.z > 0.01f)
                {
                    // 计算与本地玩家的距离
                    int distance = Mathf.RoundToInt(Vector3.Distance(PluginsCore.CorrectPlayer.Transform.position, player.Transform.position));

                    // 安全提取玩家信息
                    string name = "Unknown";
                    string side = "Bot";

                    if (player.Profile != null && player.Profile.Info != null)
                    {
                        name = player.Profile.Info.Nickname;
                        side = player.Profile.Info.Side.ToString();

                        // 动态改变字体颜色以区分阵营
                        if (side == "Savage") // Scav / Boss / AI
                        {
                            textStyle.normal.textColor = Color.yellow;
                        }
                        else // Usec / Bear (PMC)
                        {
                            textStyle.normal.textColor = Color.red;
                        }
                    }

                    // 组装最终显示的字符串
                    string espText = $"[{side}] {name} [{distance}m]";

                    // 转换坐标系并绘制
                    float screenX = textScreenPos.x;
                    float screenY = Screen.height - textScreenPos.y;

                    // 使用 Rect 在坐标点周围画一个隐形的框，利用 textStyle 的居中属性让文字完美居中
                    GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), espText, textStyle);
                }
            }
        }
        // ================= 核心工具方法 =================

        // 安全提取 Transform 的世界坐标
        public static Vector3? GetBonePos(Transform t)
        {
            if (t == null) return null;
            return t.position;
        }

        // 安全提取 BifacialTransform 的世界坐标 (注意这里使用了 .Original)
        public static Vector3? GetBonePos(BifacialTransform bt)
        {
            if (bt == null || bt.Original == null) return null;
            return bt.Original.position;
        }

        // 终极连线工具：包含 3D 转 2D 逻辑和深度过滤
        public static void DrawBoneLine(Camera cam, Vector3? p1, Vector3? p2)
        {
            // 如果有任何一个骨骼节点缺失，放弃绘制这条线
            if (!p1.HasValue || !p2.HasValue) return;

            Vector3 s1 = cam.WorldToScreenPoint(p1.Value);
            Vector3 s2 = cam.WorldToScreenPoint(p2.Value);

            // 深度检查：只有当两个点都在摄像机前方 (z > 0.01f) 时才绘制
            // 如果不加这个，当你贴脸穿过一个 AI 时，会出现满屏乱飞的射线 (透视畸变)
            if (s1.z > 0.01f && s2.z > 0.01f)
            {
                // Y 轴反转，适配屏幕坐标系
                GL.Vertex3(s1.x, Screen.height - s1.y, 0);
                GL.Vertex3(s2.x, Screen.height - s2.y, 0);
            }
        }
        public static bool IsInRange(int maxDistance, Vector3 p1, Vector3 p2)
        {
            float maxDistanceSqr = maxDistance * maxDistance;
            return (p1 - p2).sqrMagnitude <= maxDistanceSqr;
        }
        // 传入摄像机位置、目标玩家、以及你找到的障碍物 LayerMask
        public static bool IsPlayerVisible(Vector3 camPosition, Player targetPlayer, int obstacleLayerMask)
        {
            // 防空检查
            if (targetPlayer == null || targetPlayer.PlayerBones == null) return false;

            var bones = targetPlayer.PlayerBones;

            // 提取最核心的三个骨骼节点：头、胸、骨盆
            Transform[] checkBones = {
        bones.Head?.Original,
        bones.Spine3?.Original,
        bones.Pelvis?.Original
    };

            // 遍历这三个点发射射线
            foreach (Transform bone in checkBones)
            {
                if (bone == null) continue;

                // Linecast 的逻辑是：如果在 camPosition 和 bone.position 之间撞到了 obstacleLayerMask
                // 它会返回 true。所以前面加个 "!"，表示“没有撞到障碍物” = “可见”
                if (!Physics.Linecast(camPosition, bone.position, obstacleLayerMask))
                {
                    // ⭐ 核心优化：只要发现露出了任何一个部位，立刻返回 true，终止后续射线的计算！
                    return true;
                }
            }

            // 三个点全被挡住了，彻底不可见
            return false;
        }
        public static bool IsBotLookingAtUs(Player bot, Player localPlayer, int obstacleLayerMask)
        {
            if (bot == null || localPlayer == null || bot.PlayerBones == null) return false;

            // 1. 获取 AI 眼睛（Head）的世界坐标
            Transform botEyePoint = bot.PlayerBones.Head?.Original;
            if (botEyePoint == null) return false;

            // 获取玩家胸口坐标（用于数学 FOV 计算基准）
            Transform localPlayerChest = localPlayer.PlayerBones.Spine3?.Original;
            if (localPlayerChest == null) return false;

            Vector3 targetDir = (localPlayerChest.position - botEyePoint.position).normalized;

            // ⭐ 修复陷阱：绝不使用不可靠的骨骼 Z 轴！
            // 直接向塔科夫底层请求这个 AI 真正的视线/瞄准方向
            Vector3 botLookDir = bot.LookDirection; // (或者 bot.MovementContext.LookDirection)
            if (botLookDir == Vector3.zero)
            {
                botLookDir = botEyePoint.forward; // 极小概率的兜底预案
            }

            // 点积 check：计算这两个向量的夹角
            // 1 = 完美正对，0 = 90度侧面，-1 = 完全背对
            if (Vector3.Dot(botLookDir, targetDir) < BotFovThreshold) return false;

            // ==========================================
            // ⭐ 阶段 2：反向多点物理检测 (修复版)
            // 理由：正如你所说，AI会锁头。我们必须检测我们自身的【所有核心部位】。
            // ==========================================

            // 3. 定义我们自身需要检测 LOS (Line of Sight) 的关键节点
            // 必须包含头、胸、骨盆
            Transform[] myCriticalParts = {
        localPlayer.PlayerBones.Head?.Original,   // 必须检测我们的头！防爆头！
        localPlayerChest,                           // 胸口
        localPlayer.PlayerBones.Pelvis?.Original    // 下盘/胯部
    };

            // 4. 从 AI 的眼睛，向我们的这些部位发射射线
            foreach (Transform myPart in myCriticalParts)
            {
                if (myPart == null) continue;

                // Linecast：从 AI 眼睛 指向 我们的部位。
                // 如果在障碍物层级没有撞到东西
                if (!Physics.Linecast(botEyePoint.position, myPart.position, obstacleLayerMask))
                {
                    // ⭐ 核心优化：只要这三个点里有任何一个能连通，判定为：危险！
                    // 只要他能看到我们的头（即使身体全挡住了），也是危险状态！
                    return true;
                }
            }

            // AI 的 FOV 数学上对着我们，但我们的头、胸、胯都被完美挡住了。
            // 这时候才是真正的“巧合向心，无实战危险”。
            return false;
        }
    }
    public class PlayerESPCfg
    {
        internal static ConfigEntry<bool> EnablePlayerESP { get; set; }
        internal static ConfigEntry<int> ESPMaxDistance { get; set; }
        public static void Initialize(ConfigFile config)
        {
            EnablePlayerESP = config.Bind<bool>(
                "透视设置",
                "启用玩家透视",
                true,
                "玩家透视总开关，包括骨骼，玩家信息等");
            ESPMaxDistance = config.Bind<int>(
                "透视设置",
                "透视范围",
                200,
                new ConfigDescription("透视可见的范围",
                    new AcceptableValueRange<int>(50, 1000),
                    Array.Empty<object>()));
        }
    }
}
