using CommonAssets.Scripts.Game.LabyrinthEvent;
using EFT;
using EFT.Interactive;
using EFT.SynchronizableObjects;
using Oracle.ESP;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oracle.Data
{
    

    /// <summary>
    /// 玩家/实体数据引擎：处理所有的状态读取、射线检测、位置换算
    /// </summary>
    public static class OracleCommon
    {
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
}