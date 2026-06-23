using BepInEx.Configuration;
using EFT.InventoryLogic;
using HarmonyLib;
using Oracle.Data;
using Oracle.Utils;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    /// <summary>
    /// 无限耐久
    /// </summary>
    public static class NoWeaponDurabilityCost
    {
        //Patch
        [HarmonyPatch(typeof(Weapon), nameof(Weapon.GetDurabilityLossOnShot))]
        public class PlayerWeaponNeverJamPatch
        {
            static bool Prefix(Weapon __instance, float ammoBurnRatio, float overheatFactor, float skillWeaponTreatmentFactor, out float modsBurnRatio, ref float __result)
            {
                //正常发热动画
                modsBurnRatio = 1f;
                if (NoWeaponDurabilityCostCfg.EnableInfinityDurability.Value)
                {
                    //不掉耐久
                    __result = 0f;
                    return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    [OracleCfgOrder(1)]
    public class NoWeaponDurabilityCostCfg : IOracleCfg
    {

        internal static ConfigEntry<bool> EnableInfinityDurability { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            EnableInfinityDurability = config.Bind(
                "1. 天堂支点 / Combat Module",
                "无限耐久",
                false,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_infinity_durability_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_infinity_durability_name"),
                        IsAdvanced = false,
                        Order = 250
                    }
                )
            );
        }
    }
}