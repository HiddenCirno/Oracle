using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oracle.ESP
{
    /// <summary>
    /// 尸体透视数据定义
    /// </summary>
    public struct CorpseData
    {
        public Player PlayerRef;      // 底层玩家引用
        public Vector3 Position;      // 尸体三维坐标
        public string FormattedText;  // 富文本格式化后的显示文本
        public int Distance;          // 距离
    }

    /// <summary>
    /// 独立的尸体透视部分
    /// </summary>
    public class CorpseESP
    {
        /// <summary>
        /// 唯一的全局尸体缓存表
        /// </summary>
        public static List<CorpseData> CachedCorpseList = new List<CorpseData>();

        /// <summary>
        /// 阵营颜色定义
        /// </summary>
        public static class CorpseColor
        {
            public static readonly Color PMC = new Color(1f, 0.22f, 0.22f);     // 亮红：死去的PMC
            public static readonly Color Scav = new Color(1f, 0.64f, 0f);      // 橙色：死去的Scav
            public static readonly Color Boss = new Color(0.78f, 0f, 0.78f);    // 紫色：死去的Boss/追随者
        }

        /// <summary>
        /// 独立的尸体扫描协程
        /// </summary>
        public static System.Collections.IEnumerator CorpseScannerCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(2f);

                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null || PluginsCore.CorrectGameWorld.LootItems == null)
                {
                    CachedCorpseList.Clear();
                    continue;
                }
                //Console.WriteLine(CachedCorpseList.Count);
                List<CorpseData> tempCorpseList = new List<CorpseData>();
                Vector3 myPos = PluginsCore.CorrectPlayer.Transform.position;
                float maxDistance = CorpseESPCfg.CorpseESPMaxDistance.Value;

                // ⭐ 回归你的最初思路：遍历地上的战利品
                foreach (var lootItem in PluginsCore.CorrectGameWorld.LootItems.GetValuesEnumerator())
                {
                    if (lootItem == null || !lootItem.gameObject.activeSelf) continue;

                    // ⭐ 终极杀招：直接判断这个 LootItem 在底层是不是一个 Corpse (尸体)
                    if (lootItem is Corpse corpse)
                    {
                        Vector3 corpsePos;
                        if (corpse.TrackableTransform != null)
                        {
                            corpsePos = corpse.TrackableTransform.position;
                        }
                        else
                        {
                            corpsePos = corpse.transform.position; // 极少数情况下的保底 fallback
                        }

                        // 距离过滤
                        float rawDist = Vector3.Distance(myPos, corpsePos);
                        if (rawDist > maxDistance) continue;
                        int dist = Mathf.RoundToInt(rawDist);

                        // 默认数据
                        string nickName = "未知";
                        Color textColor = CorpseColor.Scav;
                        string roleTag = "SCAV";

                        // 从尸体对象上直接拿它自带的底层数据
                        string profileId = corpse.PlayerProfileID;
                        EPlayerSide corpseSide = corpse.Side;

                        // 尝试通过 ID 查户口本拿真名
                        if (!string.IsNullOrEmpty(profileId))
                        {
                            Player deadPlayer = PluginsCore.CorrectGameWorld.GetEverExistedPlayerByID(profileId);
                            if (deadPlayer != null && deadPlayer.Profile != null)
                            {
                                nickName = deadPlayer.Profile.Nickname;

                                // Boss 判定 (只有通过真实 Player 数据才能判断是不是 Boss)
                                if (deadPlayer.Profile.Info?.Settings?.Role != WildSpawnType.assault && deadPlayer.Profile.Side == EPlayerSide.Savage)
                                {
                                    textColor = CorpseColor.Boss;
                                    roleTag = "BOSS";
                                }
                            }
                        }

                        // 分配基础阵营名称和颜色 (即使查不到真名，也能通过尸体自带的 Side 判断是啥阵营)
                        if (roleTag != "BOSS") // 没被鉴定为Boss的话，走常规阵营判定
                        {
                            if (corpseSide == EPlayerSide.Usec)
                            {
                                roleTag = "USEC";
                                textColor = CorpseColor.PMC;
                            }
                            else if (corpseSide == EPlayerSide.Bear)
                            {
                                roleTag = "BEAR";
                                textColor = CorpseColor.PMC;
                            }
                        }

                        string hexColor = ColorUtility.ToHtmlStringRGB(textColor);
                        string formattedText = $"<color=#{hexColor}>[{roleTag}] {nickName}</color> <color=#FFFF00>{dist}米</color>";

                        tempCorpseList.Add(new CorpseData
                        {
                            PlayerRef = null,
                            Position = corpsePos,
                            FormattedText = formattedText,
                            Distance = dist
                        });
                    }
                }

                // 原子级刷新
                CachedCorpseList = tempCorpseList;
            }
        }

        /// <summary>
        /// 独立的尸体文本绘制方法（无约束范围，全局绘制）
        /// </summary>
        public static void DrawCorpseText(Camera cam, GUIStyle textStyle)
        {
            // 总开关
            if (!CorpseESPCfg.EnableCorpseESP.Value) return;
            if (CachedCorpseList == null || CachedCorpseList.Count == 0) return;

            // 样式状态保护
            textStyle.richText = true;
            textStyle.normal.textColor = Color.white;

            foreach (CorpseData corpse in CachedCorpseList)
            {
                // 世界坐标转屏幕坐标
                Vector3 screenPos = cam.WorldToScreenPoint(corpse.Position);

                // 确保在相机前方
                if (screenPos.z > 0.01f)
                {
                    float screenX = screenPos.x;
                    // 统一转换 Unity 坐标系并给予微小的固定的 Y 轴偏移（避免完全贴地被地面模型盖住字）
                    float screenY = Screen.height - screenPos.y - 10f;

                    // 每个人只有一行名字，无需进行堆叠偏移算法
                    GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), corpse.FormattedText, textStyle);
                }
            }
        }
    }

    /// <summary>
    /// 尸体透视配置项
    /// </summary>
    public class CorpseESPCfg
    {
        internal static ConfigEntry<bool> EnableCorpseESP { get; set; }
        internal static ConfigEntry<int> CorpseESPMaxDistance { get; set; }

        public static void Initialize(ConfigFile config)
        {
            EnableCorpseESP = config.Bind<bool>(
                "尸体透视",
                "启用尸体透视",
                true,
                "是否在屏幕上显示死去的玩家/AI"
            );

            CorpseESPMaxDistance = config.Bind<int>(
                "尸体透视",
                "尸体透视最大距离",
                300,
                new ConfigDescription(
                    "透视死者的最远范围",
                    new AcceptableValueRange<int>(50, 1000)
                )
            );
        }
    }
}