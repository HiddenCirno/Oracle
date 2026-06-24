using System;
using UnityEngine;

namespace Oracle.Utils
{
    /// <summary>
    /// 通用工具类
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

        /// <summary>
        /// 全英文名判断
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsAllEnglish(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                // 允许大写 A-Z，小写 a-z，以及空格、连字符、单引号
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == ' '))// && c != '-' && c != '\'')
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// 错误报告
        /// </summary>
        /// <param name="err"></param>
        /// <param name="message"></param>
        public static void ShowError(Exception err, string message = "")
        {

            Console.WriteLine($"[Oracle] :{message}\n{err.Message}\n{err.StackTrace}");
        }
    }
}