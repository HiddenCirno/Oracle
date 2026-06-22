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
    public static class OracleTripwireManager
    {
        public static List<TripwireData> CachedTripwires = new List<TripwireData>();

        private static FieldInfo _tripwireStartField = typeof(TripwireProceduralMesh).GetField("vector3_0", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo _tripwireEndField = typeof(TripwireProceduralMesh).GetField("vector3_1", BindingFlags.NonPublic | BindingFlags.Instance);

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

                if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null || !TripwireESPCfg.EnableTripwireESP.Value)
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

    }
}