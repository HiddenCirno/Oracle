using EFT;
using EFT.Interactive;
using EFT.Interactive;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using Oracle.ESP;
using Oracle.Utils;
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
        /// <summary>富文本（OnGUI 模式专用，叠加层数据桥不拆解它）</summary>
        public string Name;
        public int Distance;
        public int Price;
        public OracleColor ItemColor;
        public int YOffset;
        public int StackCount;

        // ⭐ 叠加层数据桥并行字段（窗口原生绘制专用，纯文本不带任何颜色标签）
        /// <summary>纯文本："{容器前缀} {物品名} {价格}"，等级色段</summary>
        public string OverlayText;
        /// <summary>纯文本："{距离}米"，黄色段</summary>
        public string OverlayDistanceText;
    }
    /// <summary>
    /// 绊雷数据缓存
    /// </summary>
    public struct TripwireData
    {
        public Vector3 StartPos;
        public Vector3 EndPos;
        public Vector3 CenterPos;
        /// <summary>纯文本：绊雷标签（红色段）</summary>
        public string OverlayLabel;
    }

    /// <summary>
    /// 实体（玩家/尸体）身上的愿望单战利品条目。
    /// 数据桥并行字段与 LootData 同款：富文本（OnGUI）+ 纯文本+颜色（叠加层）。
    /// </summary>
    public struct WishlistItemData
    {
        /// <summary>条目锚点世界坐标（玩家头顶 / 尸体坐标，绘制时投影）</summary>
        public Vector3 Position;
        /// <summary>富文本（OnGUI 模式专用）："{颜色}物品名 {数量}x {价格}"</summary>
        public string FormattedText;
        /// <summary>距离</summary>
        public int Distance;
        /// <summary>叠加层数据桥：纯文本（无颜色标签）</summary>
        public string OverlayText;
        /// <summary>叠加层数据桥：物品等级色（愿望单=9 高亮）</summary>
        public OracleColor Color;
        /// <summary>多条目垂直堆叠偏移（第 N 条 = N * 间距）</summary>
        public int YOffset;
        /// <summary>堆叠数量</summary>
        public int StackCount;
    }

    /// <summary>
    /// 尸体透视数据定义
    /// </summary>
    public struct CorpseData
    {
        public Vector3 Position;
        /// <summary>富文本（OnGUI 模式专用，叠加层数据桥不拆解它）</summary>
        public string FormattedText;
        public int Distance;

        // ⭐ 叠加层数据桥并行字段（纯文本不带颜色标签，颜色单独用 OracleColor 传递）
        /// <summary>纯文本："[已死亡]"（Corpse 色段）</summary>
        public string OverlayTag;
        /// <summary>纯文本："友军 "（AllyPlayer 色段，非队友为空串）</summary>
        public string OverlayTeammateText;
        /// <summary>纯文本："Lv.45"（PlayerLevel 色段，Scav 为空串）</summary>
        public string OverlayLevelText;
        /// <summary>纯文本："USEC John" / "Boss Killa"（阵营色段）</summary>
        public string OverlaySideText;
        /// <summary>Side 段颜色（USEC/BEAR/Scav 角色色）</summary>
        public OracleColor OverlayColor;

        /// <summary>尸体身上的愿望单战利品（仅过滤愿望单，由愿望单扫描协程填充）</summary>
        public List<WishlistItemData> WishlistItems;
    }

    public readonly struct EntityDisplayInfo
    {
        public readonly string Name;
        public readonly string SideText;
        public readonly string LevelText;
        public readonly int Distance;

        public EntityDisplayInfo(string name, string sideText, string levelText, int distance)
        {
            Name = name;
            SideText = sideText;
            LevelText = levelText;
            Distance = distance;
        }

        /// <summary>
        /// 格式化输出结果
        /// </summary>
        /// <returns></returns>
        public string ToEspString() => string.Format("text_esp_player".i18n(), LevelText, SideText, OracleColorManager.Distance, Distance).Trim();
    }

    /// <summary>
    /// 二次封装的自定义颜色结构, 同时具备字符串和UnityColor隐式转换
    /// </summary>
    public readonly struct OracleColor
    {
        public readonly string HexColor;
        public readonly string HexColorNoHash;
        public readonly Color UnityColor;

        /// <summary>
        /// 从十六进制字符串构造颜色
        /// </summary>
        public OracleColor(string hex)
        {
            //防御
            HexColor = hex.StartsWith("#") ? hex.ToUpper() : $"#{hex}".ToUpper();
            HexColorNoHash = HexColor.Substring(1);

            //Color
            if (ColorUtility.TryParseHtmlString(HexColor, out Color parsedColor))
            {
                UnityColor = parsedColor;
            }
            else
            {
                //解析失败
                Debug.LogError($"[Oracle] 解析颜色失败，无效的代码: {hex}");
                UnityColor = Color.magenta;
            }
        }

        //隐式转换
        public static implicit operator Color(OracleColor oc) => oc.UnityColor;

        public static implicit operator string(OracleColor oc) => oc.HexColor;

        //覆盖ToString为文本拼接提供兼容
        public override string ToString() => HexColor;

        /// <summary>
        /// 打包为 ARGB 32 位色值（0xAARRGGBB），供叠加层数据桥原生绘制直接取用
        /// </summary>
        public uint ToArgb()
        {
            Color32 c = (Color32)UnityColor;
            return ((uint)c.a << 24) | ((uint)c.r << 16) | ((uint)c.g << 8) | c.b;
        }
    }

    /// <summary>
    /// 本地化文本结构定义
    /// </summary>
    public class LocaleData
    {
        [JsonProperty("Language")]
        public string Language { get; set; }

        [JsonProperty("Translate")]
        public Dictionary<string, string> Translate { get; set; }
    }

    /// <summary>
    /// 高亮拓展
    /// </summary>
    public static class ExtendWishlistItem
    {
        //O1字典查询
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
