using EFT.SynchronizableObjects;
using Oracle.ESP;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oracle.Data
{
    

    /// <summary>
    /// 绊雷数据总线
    /// </summary>
    public static class OracleTripwireManager
    {
        /// <summary>
        /// 全局缓存表
        /// </summary>
        public static List<TripwireData> CachedTripwires = new List<TripwireData>();

        //反射存储
        private static FieldInfo _tripwireStartField = typeof(TripwireProceduralMesh).GetField("_fromPositionPivot", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo _tripwireEndField = typeof(TripwireProceduralMesh).GetField("_toPositionPivot", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// 扫描协程
        /// </summary>
        public static System.Collections.IEnumerator TripwireScannerCoroutine()
        {
            //反射
            _tripwireStartField = typeof(TripwireProceduralMesh).GetField("_fromPositionPivot", BindingFlags.NonPublic | BindingFlags.Instance);
            _tripwireEndField = typeof(TripwireProceduralMesh).GetField("_toPositionPivot", BindingFlags.NonPublic | BindingFlags.Instance);

            //双缓存
            List<TripwireData> frontBuffer = new List<TripwireData>(100);
            List<TripwireData> backBuffer = new List<TripwireData>(100);
            CachedTripwires = frontBuffer;

            while (true)
            {
                yield return new WaitForSeconds(2f);

                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null || !TripwireESPCfg.EnableTripwireESP.Value)
                {
                    backBuffer.Clear();
                    var tmp = frontBuffer;
                    frontBuffer = backBuffer;
                    backBuffer = tmp;
                    CachedTripwires = frontBuffer;
                    continue;
                }

                //清空缓存
                backBuffer.Clear();

                //寻找绊雷
                TripwireProceduralMesh[] tripwires = UnityEngine.Object.FindObjectsOfType<TripwireProceduralMesh>();

                foreach (TripwireProceduralMesh tripwire in tripwires)
                {
                    if (tripwire == null || !tripwire.gameObject.activeSelf) continue;

                    if (_tripwireStartField != null && _tripwireEndField != null)
                    {
                        try
                        {
                            //查找两端
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
                            Debug.LogError($"[Oracle] 绊雷坐标读取失败: {ex.Message}");
                        }
                    }
                }

                //交换指针
                var temp = frontBuffer;
                frontBuffer = backBuffer;
                backBuffer = temp;
                CachedTripwires = frontBuffer;
            }
        }

    }
}