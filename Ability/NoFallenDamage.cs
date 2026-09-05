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
    /// 无摔落伤害
    /// </summary>
    public class NoFallenDamage
    {
    }
    //Patch
    [HarmonyPatch(typeof(Player), "ApplyDamageInfo")]
    public class AntiFallenDamagePatch
    {
        public static bool Prefix(Player __instance, ref DamageInfoStruct damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType, float absorbed)
        {
            //不知道是速度太快还是过滤问题，总之都改了
            //奇怪, 为什么玩家Scav可以而玩家不行
            //Fika干了什么?
            //仅自己判断
            //if (!__instance.IsYourPlayer)
            //{
            //    return true;
            //}
            //else
            //{
            //伤害类型过滤
            if (__instance == PluginsCore.CorrectPlayer&& NoFallenDamageCfg.DisableFallenDamage.Value && (damageInfo.DamageType == EDamageType.Fall || damageInfo.DamageType == EDamageType.Impact))
            {
                //阻拦
                damageInfo.Damage = 0;
                damageInfo.DidBodyDamage = 0;
                damageInfo.DelayedDamage = false;
            }
            return true;
            //}
        }
    }

    //Patch
    [HarmonyPatch(typeof(ActiveHealthController), "ApplyDamage")]
    public class AntiFallenDamagePatch2
    {
        public static bool Prefix(ActiveHealthController __instance, EBodyPart bodyPart, ref float damage, ref DamageInfoStruct damageInfo)
        {
            if (NoFallenDamageCfg.DisableFallenDamage.Value && (damageInfo.DamageType == EDamageType.Fall || damageInfo.DamageType == EDamageType.Impact))
            {
                //阻拦
                damage = 0f;
                damageInfo.Damage = 0;
                damageInfo.DidBodyDamage = 0;
                damageInfo.DelayedDamage = false;
            }
            return true;
            //}
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    [OracleCfgOrder(2)]
    public class NoFallenDamageCfg : IOracleCfg
    {

        internal static ConfigEntry<bool> DisableFallenDamage { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            DisableFallenDamage = config.Bind(
                "2. 生命之树 / Ability Module",
                "阻止摔落伤害",
                true,
                new ConfigDescription(
                    "cfg_ability_module_feather_fall_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_ability_module_feather_fall_name".i18n(),
                        IsAdvanced = false,
                        Order = 200
                    }
                )
            );
        }
    }
}
