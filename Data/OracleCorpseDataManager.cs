using EFT;
using EFT.Interactive;
using Oracle.ESP;
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Oracle.Data
{
    /// <summary>
    /// 尸体数据总线
    /// </summary>
    public static class OracleCorpseDataManager
    {
        /// <summary>
        /// 唯一的全局尸体缓存表
        /// </summary>
        public static List<CorpseData> CachedCorpseList = new List<CorpseData>();


        /// <summary>
        /// 扫描协程
        /// </summary>
        public static System.Collections.IEnumerator CorpseScannerCoroutine()
        {
            //双缓存预分配地址
            List<CorpseData> frontBuffer = new List<CorpseData>(200);
            List<CorpseData> backBuffer = new List<CorpseData>(200);
            CachedCorpseList = frontBuffer;

            //两秒一循环
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

                //清空后台缓存
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
                            corpsePos = corpse.transform.position;
                        }

                        //过滤距离
                        float rawDist = Vector3.Distance(myPos, corpsePos);
                        if (rawDist > maxDistance) continue;
                        int dist = Mathf.RoundToInt(rawDist);

                        //拿到玩家uuid
                        string profileId = corpse.PlayerProfileID;
                        EPlayerSide corpseSide = corpse.Side;

                        //保底字符串
                        var result = "Scav Nikita Buyanov";

                        //叠加层数据桥并行字段默认值（镜像 OnGUI 的 fallback）
                        string overlayTeammate = "";
                        string overlayLevel = "";
                        string overlaySide = result;
                        OracleColor overlayColor = OracleColorManager.Corpse;

                        //非空&查找缓存引用
                        if (!string.IsNullOrEmpty(profileId))
                        {
                            Player deadPlayer = PluginsCore.CorrectGameWorld.GetEverExistedPlayerByID(profileId);
                            if (deadPlayer != null && deadPlayer.Profile != null)
                            {
                                ProfileInfo info = deadPlayer.Profile.Info;
                                if (info != null)
                                {
                                    string name = OraclePlayerDataManager.GetPlayerName(info);
                                    bool isTeammate = OraclePlayerDataManager.IsTeammate(info);

                                    OraclePlayerDataManager.DeterminePlayerText(info, name, isTeammate, true, out result, out string levelText);

                                    //拼接
                                    if (!string.IsNullOrEmpty(levelText))
                                    {
                                        result = $"{levelText} {result}";
                                    }

                                    //叠加层数据桥：纯文本 + 颜色（不拆解上面的富文本）
                                    //等级/友军色是固定常量（PlayerLevel / AllyPlayer），由绘制层直接取用，这里用弃元丢弃
                                    OraclePlayerDataManager.GetPlayerOverlayLabel(info, name, isTeammate, true,
                                        out overlayLevel, out _,
                                        out overlayTeammate, out _,
                                        out overlaySide, out overlayColor);
                                }
                            }
                        }

                        string formattedText = string.Format("text_esp_corpse_format".i18n(), OracleColorManager.Corpse, "text_esp_corpse_dead_tag".i18n(), result);

                        //写入后台缓存
                        backBuffer.Add(new CorpseData
                        {
                            Position = corpsePos,
                            FormattedText = formattedText,
                            Distance = dist,
                            //叠加层数据桥并行字段
                            OverlayTag = "text_esp_overlay_corpse_dead_tag".i18n(),
                            OverlayTeammateText = overlayTeammate,
                            OverlayLevelText = overlayLevel,
                            OverlaySideText = overlaySide,
                            OverlayColor = overlayColor
                        });
                    }
                }

                //交换前后台
                var temp = frontBuffer;
                frontBuffer = backBuffer;
                backBuffer = temp;
                CachedCorpseList = frontBuffer;
            }
        }

    }
}