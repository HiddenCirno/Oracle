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
        public Color ItemColor;
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
}
