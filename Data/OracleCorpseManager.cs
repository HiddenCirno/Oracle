using CommonAssets.Scripts.Game.LabyrinthEvent;
using EFT;
using EFT.Interactive;
using EFT.SynchronizableObjects;
using Oracle.ESP;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static Oracle.ESP.CorpseESP;

namespace Oracle.Data
{
    

    /// <summary>
    /// 玩家/实体数据引擎：处理所有的状态读取、射线检测、位置换算
    /// </summary>
    public static class OracleCorpseManager
    {
        /// <summary>
        /// 唯一的全局尸体缓存表
        /// </summary>
        public static List<CorpseData> CachedCorpseList = new List<CorpseData>();


        /// <summary>
        /// 独立的尸体扫描协程
        /// </summary>
        public static System.Collections.IEnumerator CorpseScannerCoroutine()
        {
            // ⭐ 双缓冲预分配，给 200 的容量对于尸体来说已经管够了
            List<CorpseData> frontBuffer = new List<CorpseData>(200);
            List<CorpseData> backBuffer = new List<CorpseData>(200);
            CachedCorpseList = frontBuffer;

            while (true)
            {
                yield return new WaitForSeconds(2f);

                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null || PluginsCore.CorrectGameWorld.LootItems == null)
                {
                    backBuffer.Clear();
                    var tmp = frontBuffer;
                    frontBuffer = backBuffer;
                    backBuffer = tmp;
                    CachedCorpseList = frontBuffer;
                    continue;
                }

                // ⭐ 极速清空后台缓冲区
                backBuffer.Clear();

                Vector3 myPos = PluginsCore.CorrectPlayer.Transform.position;
                float maxDistance = CorpseESPCfg.CorpseESPMaxDistance.Value;

                foreach (var lootItem in PluginsCore.CorrectGameWorld.LootItems.GetValuesEnumerator())
                {
                    if (lootItem == null || !lootItem.gameObject.activeSelf) continue;

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

                        // 从尸体对象上直接拿它自带的底层数据
                        string profileId = corpse.PlayerProfileID;
                        EPlayerSide corpseSide = corpse.Side;

                        var result = "Scav Nikita Buyanov";

                        // 尝试通过 ID 查户口本拿真名
                        if (!string.IsNullOrEmpty(profileId))
                        {
                            Player deadPlayer = PluginsCore.CorrectGameWorld.GetEverExistedPlayerByID(profileId);
                            if (deadPlayer != null && deadPlayer.Profile != null)
                            {
                                InfoClass info = deadPlayer.Profile.Info;
                                if (info != null)
                                {
                                    OraclePlayerManager.DeterminePlayerText(info,OraclePlayerManager.GetPlayerName(info),OraclePlayerManager.IsTeammate(info),true,out result,out string levelText);

                                    // 如果你想在尸体上也显示 PMC 的等级，可以把 levelText 拼进去：
                                    if (!string.IsNullOrEmpty(levelText))
                                    {
                                        result = $"{levelText} {result}";
                                    }
                                }
                            }
                        }

                        string formattedText = $"<color={OracleColorManager.EnemyDangerous}>[已死亡]</color> {result}";// <color=#FFFF00>{dist}米</color>";

                        // ⭐ 写入后台缓冲区
                        backBuffer.Add(new CorpseData
                        {
                            Position = corpsePos,
                            FormattedText = formattedText,
                            Distance = dist
                        });
                    }
                }

                // ⭐ 瞬间交换指针
                var temp = frontBuffer;
                frontBuffer = backBuffer;
                backBuffer = temp;
                CachedCorpseList = frontBuffer;
            }
        }

    }
}