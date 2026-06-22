using EFT.Interactive;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Oracle.ESP;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oracle.Data
{
    /// <summary>
    /// 战利品数据结构
    /// </summary>
    public struct LootData
    {
        public Item ItemRef;
        public LootItem? LootableItem;
        public LootableContainer? Container;
        public Vector3 Position;
        public int ItemLevel;
        public string Name;
        public int Distance;
        public int Price;
        public OracleColor ItemColor;
        public int YOffset;
        public int StackCount;
    }
    /// <summary>
    /// 绊雷数据缓存
    /// </summary>
    public struct TripwireData
    {
        public Vector3 StartPos;
        public Vector3 EndPos;
        public Vector3 CenterPos;
    }

    /// <summary>
    /// 尸体透视数据定义
    /// </summary>
    public struct CorpseData
    {
        public Vector3 Position;      // 尸体三维坐标
        public string FormattedText;  // 富文本格式化后的显示文本
        public int Distance;          // 距离
    }

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
    public readonly struct OracleColor
    {
        public readonly string HexColor;       // 带 # 的富文本颜色 (例: "#FF8C00")
        public readonly string HexColorNoHash; // 不带 # 的纯代码 (例: "FF8C00")
        public readonly Color UnityColor;      // Unity 原生 Color 对象

        /// <summary>
        /// 从十六进制字符串构造颜色 (例如: "#FF0000" 或 "FF0000" 或带透明度 "#FF0000FF")
        /// </summary>
        public OracleColor(string hex)
        {
            // 防御性容错：自动补全 # 号
            HexColor = hex.StartsWith("#") ? hex.ToUpper() : $"#{hex}".ToUpper();
            HexColorNoHash = HexColor.Substring(1);

            // 一次性解析为 Unity 原生 Color，永久缓存
            if (ColorUtility.TryParseHtmlString(HexColor, out Color parsedColor))
            {
                UnityColor = parsedColor;
            }
            else
            {
                // 解析失败时，给个刺眼的洋红色作为错误提示
                Debug.LogError($"[OracleColor] 解析颜色失败，无效的代码: {hex}");
                UnityColor = Color.magenta;
            }
        }

        // ⭐ C# 黑魔法 1：隐式转换为 Unity Color
        // 当方法需要 Color 时，直接传 OracleColor 即可
        public static implicit operator Color(OracleColor oc) => oc.UnityColor;

        // ⭐ C# 黑魔法 2：隐式转换为 String
        // 当拼接富文本字符串时，直接传 OracleColor，它会自动变成 "#FFFFFF"
        public static implicit operator string(OracleColor oc) => oc.HexColor;
        public override string ToString() => HexColor;
    }
    public static class ExtendWishlistItem
    {
        public static Dictionary<string, string> LabyrinthSpecialItem = new Dictionary<string, string>()
        {
            {"679baa2c61f588ae2b062a24", "一号房钥匙"},
            {"679baa4f59b8961f370dd683", "二号房钥匙"},
            {"679baa5a59b8961f370dd685", "三号房钥匙"},
            {"679baa9091966fe40408f149", "四号房钥匙"},
            {"679baace4e9ca6b3d80586b2", "观察室钥匙"},
            {"679bab714e9ca6b3d80586b4", "停尸房钥匙"},
            {"678fa929819ddc4c350c0317", "阀门手轮"},
            {"67ab3d4b83869afd170fdd3f", "BBQ-S43 喷枪"}
        }; 
        public static Dictionary<string, string> StreetsSpecialItem = new Dictionary<string, string>()
        {
            {"64d4b23dc1b37504b41ac2b6", "生锈的带血钥匙"}
        };
    }
}
