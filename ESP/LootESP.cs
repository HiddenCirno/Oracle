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
        public int Price;        // ⭐ 新增：存储价格
        public Color ItemColor;  // ⭐ 新增：存储这个物品该用什么颜色画
    }

    public class LootESP
    {
        // 全局缓存列表，供 OnGUI 高频读取
        public static List<LootData> CachedLootList = new List<LootData>();
        
        public static EFT.Interactive.LootableContainer[] CachedContainers;
        public static Color GetColorByPrice(int price)
        {
            // 价格分级可以根据你的喜好随时调整
            if (price >= 100000) return Color.magenta;  // 极品 (紫装/红卡/显卡)：亮紫色
            if (price >= 50000) return Color.yellow;    // 高价值 (好钥匙/稀有配件)：黄色
            if (price >= 10000) return Color.cyan;      // 中等价值：青色
            return Color.white;                         // 垃圾/便宜货：白色
        }
        // ==========================================
        // 渲染管线 (由 PluginsCore 的 OnGUI 调用)
        // ==========================================
        public static void DrawLootText(Camera cam, GUIStyle textStyle)
        {
            if (CachedLootList == null || CachedLootList.Count == 0) return;

            foreach (LootData loot in CachedLootList)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(loot.Position);

                if (screenPos.z > 0.01f)
                {
                    float screenX = screenPos.x;
                    float screenY = Screen.height - screenPos.y;

                    // ⭐ 组装终极文本：[名称] [距离m] [价格₽]
                    // 为了美观，可以把价格除以 1000，加上 'k' (例如 150000 变成 150k)
                    string priceStr = loot.Price >= 1000 ? (loot.Price / 1000) + "k" : loot.Price.ToString();
                    string espText = $"{loot.Name} [{loot.Distance}m] {priceStr}₽";

                    // ⭐ 换上这件物品专属的颜色
                    textStyle.normal.textColor = loot.ItemColor;

                    GUI.Label(new Rect(screenX - 100, screenY - 20, 200, 40), espText, textStyle);
                }
            }
        }
    }
}