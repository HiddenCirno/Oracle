using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using Oracle.ItemSpawn;
using Oracle.Tools;
using System;
using System.Reflection;

namespace Oracle.Combat
{
    public static class NoWeaponDurabilityCost
    {
        // 使用纯原生 Harmony 注解，直接绑定目标方法
        [HarmonyPatch(typeof(Weapon), nameof(Weapon.GetDurabilityLossOnShot))]
        public class PlayerWeaponNeverJamPatch
        {
            // 注意：原方法有 out 参数，在 Harmony Prefix 中需要用 ref 关键字接收
            static bool Prefix(Weapon __instance, float ammoBurnRatio, float overheatFactor, float skillWeaponTreatmentFactor, out float modsBurnRatio, ref float __result)
            {
                modsBurnRatio = 1f;
                if (NoWeaponDurabilityCostCfg.EnableInfinityDurability.Value)
                {
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
    public class NoWeaponDurabilityCostCfg
    {

        internal static ConfigEntry<bool> EnableInfinityDurability { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public static void Initialize(ConfigFile config)
        {
            EnableInfinityDurability = config.Bind(
                "战斗修改",
                "无限耐久",
                false,
                "启用后开火将不消耗武器耐久"
            );
        }
    }
}