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
}
