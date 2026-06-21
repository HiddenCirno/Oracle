using BepInEx.Configuration;
using CommonAssets.Scripts.Game.LabyrinthEvent;
using EFT;
using EFT.SynchronizableObjects;
using Oracle.Data;
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
        /// <summary>
        /// 用于缓存绊雷数据的结构体，避免在 OnGUI 中使用反射
        /// </summary>
        public struct TripwireData
        {
            public Vector3 StartPos;
            public Vector3 EndPos;
            public Vector3 CenterPos;
        }
        /// <summary>
        /// 全局缓存的绊雷数据列表
        /// </summary>
        public static List<TripwireData> CachedTripwires = new List<TripwireData>();

        // 缓存反射的 FieldInfo，避免每次循环都去查找
        private static FieldInfo _tripwireStartField;
        private static FieldInfo _tripwireEndField;
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
        public readonly struct EntityDisplayInfo
        {
            public readonly string Name;
            public readonly string SideText; // 已包含颜色标签的完整描述
            public readonly string LevelText; // 空字符串或带颜色的等级
            public readonly int Distance;

            public EntityDisplayInfo(string name, string sideText, string levelText, int distance)
            {
                Name = name;
                SideText = sideText;
                LevelText = levelText;
                Distance = distance;
            }

            // 格式化输出方便直接给GUI调用
            public string ToEspString() => $"{LevelText} {SideText} <color=#FFFF00>{Distance}米</color>".Trim();
        }
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
            PlayerESP.DrawPlayerText(cam, RenderUtils.EspTextStyle);
            PlayerESP.DrawAllPlayerHealthBars(cam);
            PlayerESP.DrawTripwireESP(cam, RenderUtils.EspTextStyle, RenderUtils.EspMaterial);

            // 2. 3D 骨骼线段，必须自己包裹 GL 状态！
            if (Event.current.type == EventType.Repaint)
            {
                RenderUtils.EspMaterial.SetPass(0);
                GL.PushMatrix();
                // GL.LoadPixelMatrix(); (如果有必要的话)
                GL.Begin(GL.LINES);

                // 画骨骼
                PlayerESP.DrawPlayerBone(cam);

                GL.End();
                GL.PopMatrix();
            }
        }
        public static EntityDisplayInfo GetEntityInfo(Player player, bool isTeammate, bool includeName = true)
        {
            var info = player.Profile?.Info;
            string name = "Unknown";
            string sideText = "Unknown";
            string level = "";

            // 距离计算
            int distance = Mathf.RoundToInt(Vector3.Distance(PluginsCore.CorrectPlayer.Transform.position, player.Transform.position));

            if (info != null)
            {
                var role = info.Settings?.Role.ToString().ToLower() ?? "assault";
                //迷宫小弟、玩家和全英文名不经过转换，反向筛选西里尔字母
                name = ((info.Side == EPlayerSide.Bear || info.Side == EPlayerSide.Usec) || (role == "tagillahelperagro") || IsAllEnglish(info.Nickname))  ? info.Nickname : GStruct21.ConvertToLatinic(info.Nickname);

                if (info.Side.ToString() == "Savage")
                {
                    sideText = DetermineSavageSideText(info, name, isTeammate, includeName);
                }
                else
                {
                    level = $"<color=#7FFF00>{info.Level}级</color>";
                    string color = info.Side.ToString() == "Usec" ? "#007CFF" : "#FF8C00";
                    string displayContent = includeName ? $"{info.Side} {name}" : info.Side.ToString();
                    string baseText = $"<color={color}>{displayContent}</color>";
                    sideText = isTeammate ? $"<color=#66CCFF>友军 </color>{baseText}" : baseText;
                }
            }

            return new EntityDisplayInfo(name, sideText, level, distance);
        }
        private static string DetermineSavageSideText(InfoClass info, string name, bool isTeammate, bool includeName = true)
        {
            var role = info.Settings?.Role.ToString().ToLower() ?? "assault";

            // 使用变量存储核心标识，不再重复拼接颜色标签
            string roleLabel = "Scav";
            string colorHex = "#FFFF8B";

            //这里是不是可以加上多角色适配？
            // 核心优先级逻辑
            if (role.Contains("boss") || IsSpecialBoss(role)) { roleLabel = "Boss"; colorHex = "#CE0000"; }
            else if (role == "bossboarsniper" || role == "marksman") { roleLabel = "狙击Scav"; colorHex = "#00FA9A"; }
            else if (role == "pmcbot" || role == "exusec") { roleLabel = "美军"; colorHex = "#7300A6"; }
            else if (role.Contains("follower") || role == "tagillahelperagro") { roleLabel = "护卫"; colorHex = "#FF2DE9"; }
            else if (role.Contains("sectant")) { roleLabel = "邪教徒"; colorHex = "#ADFF2F"; }
            else if (role == "gifter") { roleLabel = "圣诞老人"; colorHex = "#00FFFF"; }
            else if (role.Contains("btr")) { roleLabel = "BTR"; colorHex = "#228B22"; }
            else if (role.Contains("black")) { roleLabel = "黑狐"; colorHex = "#DC143C"; }

            // 组合名称部分
            string displayString = includeName ? $"{roleLabel} {name}" : roleLabel;
            string finalRes = $"<color={colorHex}>{displayString}</color>";

            // 组合友军部分
            return isTeammate ? $"<color=#66CCFF>友军 </color>{finalRes}" : finalRes;
        }

        private static bool IsSpecialBoss(string role)
        {
            return role == "followerbirdeye" || role == "followerbigpipe" ||
                   role == "infectedtagilla" || role == "sectantoni" ||
                   role == "sectantpredvestnik" || role == "sectantprizark";
        }

        /// <summary>
        /// 绊雷扫描协程
        /// </summary>
        public static System.Collections.IEnumerator TripwireScannerCoroutine()
        {
            // 初始化反射字段
            _tripwireStartField = typeof(TripwireProceduralMesh).GetField("vector3_0", BindingFlags.NonPublic | BindingFlags.Instance);
            _tripwireEndField = typeof(TripwireProceduralMesh).GetField("vector3_1", BindingFlags.NonPublic | BindingFlags.Instance);

            // ⭐ 双缓冲预分配
            List<TripwireData> frontBuffer = new List<TripwireData>(100);
            List<TripwireData> backBuffer = new List<TripwireData>(100);
            CachedTripwires = frontBuffer;

            while (true)
            {
                yield return new WaitForSeconds(2f); // 每2秒扫描一次

                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null || !PlayerESPCfg.EnableTripwireESP.Value)
                {
                    // 如果没开启或者不在战局，清空缓存并交换指针，防止上一局的残留画在屏幕上
                    backBuffer.Clear();
                    var tmp = frontBuffer;
                    frontBuffer = backBuffer;
                    backBuffer = tmp;
                    CachedTripwires = frontBuffer;
                    continue;
                }

                // ⭐ 极速清空后台缓冲区
                backBuffer.Clear();

                // ⚠️ 注：FindObjectsOfType 底层会 new 一个数组，这里会产生微量 GC。
                // 但因为是 2 秒一次，且不是在 OnGUI 里，所以完全可以接受。
                TripwireProceduralMesh[] tripwires = UnityEngine.Object.FindObjectsOfType<TripwireProceduralMesh>();

                foreach (TripwireProceduralMesh tripwire in tripwires)
                {
                    if (tripwire == null || !tripwire.gameObject.activeSelf) continue;

                    if (_tripwireStartField != null && _tripwireEndField != null)
                    {
                        try
                        {
                            // 通过反射提取起点和终点的世界坐标
                            Vector3 start = (Vector3)_tripwireStartField.GetValue(tripwire);
                            Vector3 end = (Vector3)_tripwireEndField.GetValue(tripwire);
                            Vector3 center = (start + end) / 2f;

                            // ⭐ 写入后台缓冲区
                            backBuffer.Add(new TripwireData
                            {
                                StartPos = start,
                                EndPos = end,
                                CenterPos = center
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Tripwire ESP] 读取坐标失败: {ex.Message}");
                        }
                    }
                }

                // ⭐ 瞬间交换指针
                var temp = frontBuffer;
                frontBuffer = backBuffer;
                backBuffer = temp;
                CachedTripwires = frontBuffer;
            }
        }

        /// <summary>
        /// 绘制绊雷 2D 实体线和距离信息
        /// </summary>
        public static void DrawTripwireESP(Camera cam, GUIStyle textStyle, Material lineMaterial)
        {
            if (!PlayerESPCfg.EnableTripwireESP.Value || CachedTripwires == null || CachedTripwires.Count == 0) return;

            Vector3 playerPos = PluginsCore.CorrectPlayer.Transform.position;
            int maxDistance = 25;

            // ================= 步骤 1：使用 GL 绘制绊线 =================
            if (Event.current.type == EventType.Repaint)
            {
                lineMaterial.SetPass(0);
                GL.PushMatrix();
                GL.LoadPixelMatrix();
                GL.Begin(GL.LINES);
                GL.Color(ColorDangerous); // 使用红色画线

                foreach (TripwireData trap in CachedTripwires)
                {
                    // 距离过滤 (用中点计算距离)
                    if (!IsInRange(maxDistance, playerPos, trap.CenterPos)) continue;

                    // 转屏幕坐标
                    Vector3 screenPointA = cam.WorldToScreenPoint(trap.StartPos);
                    Vector3 screenPointB = cam.WorldToScreenPoint(trap.EndPos);

                    // 深度检查：确保线段的两端都在屏幕前方
                    if (screenPointA.z > 0.01f && screenPointB.z > 0.01f)
                    {
                        // 绘制直线
                        GL.Vertex3(screenPointA.x, screenPointA.y, 0);
                        GL.Vertex3(screenPointB.x, screenPointB.y, 0);
                    }
                }
                GL.End();
                GL.PopMatrix();
            }

            // ================= 步骤 2：使用 GUI 绘制文字标签 =================
            textStyle.richText = true;
            foreach (TripwireData trap in CachedTripwires)
            {
                if (!IsInRange(maxDistance, playerPos, trap.CenterPos)) continue;

                Vector3 screenCenter = cam.WorldToScreenPoint(trap.CenterPos);

                if (screenCenter.z > 0.01f)
                {
                    int dist = Mathf.RoundToInt(Vector3.Distance(playerPos, trap.CenterPos));
                    string text = $"<color=#FF0000>绊雷</color> <color=#FFFF00>{dist}米</color>";

                    float screenX = screenCenter.x;
                    float screenY = Screen.height - screenCenter.y;

                    // 在中点上方偏移画字，完美居中
                    GUI.Label(new Rect(screenX - 50, screenY - 20, 100, 40), text, textStyle);
                }
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
                if (!IsInRange(PlayerESPCfg.PlayerESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
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
                if (!IsInRange(PlayerESPCfg.PlayerESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
                {
                    continue;
                }
                //过滤队友
                string targetGroupId = player.Profile?.Info?.GroupId ?? "";
                bool isTeammate = !string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && targetGroupId == PluginsCore.CorrectGroupId;
                //获取头部坐标, 这样信息才能悬浮于头顶
                Vector3? headPos = GetBonePos(player.PlayerBones.Head);
                if (!headPos.HasValue) continue;
                //向头顶偏移防止和骨骼绘制重叠
                Vector3 textWorldPos = headPos.Value + new Vector3(0, 0.3f, 0);
                Vector3 textScreenPos = cam.WorldToScreenPoint(textWorldPos);
                //深度检查
                if (textScreenPos.z > 0.01f)
                {
                    var info = GetEntityInfo(player, isTeammate);
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
                if (!IsInRange(PlayerESPCfg.PlayerESPMaxDistance.Value, PluginsCore.CorrectPlayer.Transform.position, player.Transform.position))
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
            GetPlayerTotalHealth(player, out float curHp, out float maxHp);
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
        /// 判断距离, O(1)单步搞定
        /// </summary>
        /// <param name="maxDistance">距离限制</param>
        /// <param name="p1">坐标1</param>
        /// <param name="p2">坐标2</param>
        /// <returns></returns>
        public static bool IsInRange(int maxDistance, Vector3 p1, Vector3 p2)
        {
            return (p1 - p2).sqrMagnitude <= maxDistance * maxDistance;
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
        /// <summary>
        /// 判断字符串是否全是英文字符
        /// </summary>
        /// <param name="str">输入字符</param>
        /// <returns></returns>
        public static bool IsAllEnglish(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                // 允许大写 A-Z，小写 a-z，以及空格、连字符、单引号
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == ' '))// && c != '-' && c != '\'')
                    return false;
            }
            return true;
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
    public class PlayerESPCfg : IOracleCfg
    {
        internal static ConfigEntry<bool> EnablePlayerESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerInfoESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerBoneESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerHealthBarESP { get; set; }
        internal static ConfigEntry<bool> EnablePlayerBoneESPHealthMode { get; set; }
        internal static ConfigEntry<int> PlayerESPMaxDistance { get; set; }

        internal static ConfigEntry<bool> EnableTripwireESP { get; set; } // 新增绊雷开关
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
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
            EnableTripwireESP = config.Bind<bool>(
                "陷阱透视", // 可以合并为一个大类，或者保持"玩家透视"
                "启用绊雷透视",
                true,
                "在屏幕上绘制出绊雷的触发实体线及距离"
            );
        }
    }
}
