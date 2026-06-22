using System;
using System.Collections.Generic;
using System.Text;

namespace Oracle.Data
{
    public static class OracleColorManager
    {
        public static readonly OracleColor Safe = new OracleColor("#00FF00");
        public static readonly OracleColor Warning = new OracleColor("#FFFF00");
        public static readonly OracleColor Dangerous = new OracleColor("#FF0000");
        public static readonly OracleColor AimbotCycle = new OracleColor("#FF0000");

        public static readonly OracleColor LootTier0 = new OracleColor("#FFFFFF");
        public static readonly OracleColor LootTier1 = new OracleColor("#00AA00");
        public static readonly OracleColor LootTier2 = new OracleColor("#00A0FF");
        public static readonly OracleColor LootTier3 = new OracleColor("#AA00AA");
        public static readonly OracleColor LootTier4 = new OracleColor("#FFAA00");
        public static readonly OracleColor LootTier5 = new OracleColor("#AA0000");
        public static readonly OracleColor LootTier6 = new OracleColor("#FF55FF");
        public static readonly OracleColor LootTierX = new OracleColor("#808080");
        public static readonly OracleColor LootTierEX = new OracleColor("#DC143C");
    }
}
