using UnityEngine;
using System.Collections.Generic;

namespace Oracle.ESP
{
    // ⭐ 精简的缓存结构：只存 UI 渲染必须的数据，绝不存整个 LootItem 对象
    public struct LootData
    {
        public Vector3 Position;
        public string Name;
        public int Distance;
    }

    public class LootESP
    {
        // 全局缓存列表，供 OnGUI 高频读取
        public static List<LootData> CachedLootList = new List<LootData>();

        // ==========================================
        // 渲染管线 (由 PluginsCore 的 OnGUI 调用)
        // ==========================================
        public static void DrawLootText(Camera cam, GUIStyle textStyle)
        {
            // 防空检查
            if (CachedLootList == null || CachedLootList.Count == 0) return;

            // 临时把字体颜色改成适合物资的颜色（比如青色）
            Color originalColor = textStyle.normal.textColor;
            textStyle.normal.textColor = Color.cyan;

            // 遍历缓存好的极少量物资数据
            foreach (LootData loot in CachedLootList)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(loot.Position);

                // 防背身检查
                if (screenPos.z > 0.01f)
                {
                    float screenX = screenPos.x;
                    float screenY = Screen.height - screenPos.y;

                    string espText = $"[Loot] {loot.Name} [{loot.Distance}m]";

                    // 绘制文字
                    GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), espText, textStyle);
                }
            }

            // 画完物资后，把颜色还原，以免影响其他管线
            textStyle.normal.textColor = originalColor;
        }
    }
}