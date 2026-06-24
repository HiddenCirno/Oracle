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
        public Vector3 Position;
        public string FormattedText;
        public int Distance;
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
