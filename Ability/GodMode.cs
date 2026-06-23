using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using HarmonyLib;
using Oracle.Data;
using Oracle.Utils;
using static Oracle.Data.OracleInterface;

namespace Oracle.Ability
{
    /// <summary>
    /// 无敌/锁血/不死
    /// </summary>
    public static class GodMode
    {
        //无敌Patch
        [HarmonyPatch(typeof(Player), "ApplyDamageInfo")]
        public class GodMode_ApplyDamageInfoPatch
        {
            public static bool Prefix(Player __instance)
            {
                if (!__instance.IsYourPlayer) return true;

                //无敌优先级最高
                if (GodModeCfg.Invincible.Value)
                {
                    return false;
                }

                //正常受伤
                return true;
            }

            public static void Postfix(Player __instance)
            {
                if (!__instance.IsYourPlayer) return;
                
                //没开无敌但是开了锁血
                if (!GodModeCfg.Invincible.Value && GodModeCfg.HealthLock.Value)
                {
                    var hc = __instance.ActiveHealthController;
                    if (hc != null)
                    {
                        //回满血(这个居然还会治疗流血
                        hc.RestoreFullHealth();
                    }
                }
            }
        }

        //阻止死亡Patch
        [HarmonyPatch(typeof(ActiveHealthController), "Kill")]
        public static class GodMode_AHCKillPatch
        {
            public static bool Prefix(ActiveHealthController __instance)
            {
                if (!__instance.Player.IsYourPlayer) return true;

                //开启任意一个则阻止死亡
                if (GodModeCfg.Invincible.Value || GodModeCfg.HealthLock.Value || GodModeCfg.Undying.Value)
                {
                    return false;
                }

                return true;
            }
        }

        //阻止部位损毁
        [HarmonyPatch(typeof(ActiveHealthController), "DestroyBodyPart")]
        public static class GodMode_AHCDestroyBodyPartPatch
        {
            public static bool Prefix(ActiveHealthController __instance)
            {
                if (!__instance.Player.IsYourPlayer) return true;

                if (GodModeCfg.Invincible.Value || GodModeCfg.HealthLock.Value || GodModeCfg.Undying.Value)
                {
                    return false;
                }

                return true;
            }
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    /// 
    [OracleCfgOrder(2)]
    public class GodModeCfg : IOracleCfg
    {
        public static ConfigEntry<bool> Invincible { get; set; }
        public static ConfigEntry<bool> HealthLock { get; set; }
        public static ConfigEntry<bool> Undying { get; set; }

        public void Initialize(ConfigFile config)
        {
            Invincible = config.Bind(
                "2. 生命之树 / Ability Module", 
                "无敌", 
                false,
                new ConfigDescription(
                    LocaleManager.Get("cfg_ability_module_gode_mode_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_ability_module_gode_mode_name"),
                        IsAdvanced = false,
                        Order = 220
                    }
                )
            );
            HealthLock = config.Bind(
                "2. 生命之树 / Ability Module", 
                "锁血", 
                false,
                new ConfigDescription(
                    LocaleManager.Get("cfg_ability_module_health_lock_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_ability_module_health_lock_name"),
                        IsAdvanced = false,
                        Order = 219
                    }
                )
            );
            Undying = config.Bind(
                "2. 生命之树 / Ability Module", 
                "不死", 
                false,
                new ConfigDescription(
                    LocaleManager.Get("cfg_ability_module_undead_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_ability_module_undead_name"),
                        IsAdvanced = false,
                        Order = 218
                    }
                )
            );
        }
    }
}