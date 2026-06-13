using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace Oracle.Utils
{
    public class InfinityStaminaAndNoFallenDamage
    {
        /// <summary>
        /// 耐力锁定脚本
        /// </summary>
        public class PlayerStatusEditComponent : MonoBehaviour
        {
            private Player localPlayer;
            private void Awake()
            {
                //查找玩家组件
                localPlayer = gameObject.GetComponent<Player>();
            }
            private void Update()
            {
                //防御
                if (localPlayer == null) return;
                if (localPlayer.Physical != null)
                {
                    //定义开关
                    bool isInfinite = PlayerStatusEditCfg.EnableInfiniteStamina.Value;
                    //赋值
                    if (localPlayer.Physical.Stamina != null)
                    {
                        localPlayer.Physical.Stamina.ForceMode = isInfinite;
                    }

                    if (localPlayer.Physical.HandsStamina != null)
                    {
                        localPlayer.Physical.HandsStamina.ForceMode = isInfinite;
                    }

                    if (localPlayer.Physical.Oxygen != null)
                    {
                        localPlayer.Physical.Oxygen.ForceMode = isInfinite;
                    }
                }
            }
        }
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
            if (__instance == PluginsCore.CorrectPlayer&&PlayerStatusEditCfg.DisableFallenDamage.Value && (damageInfo.DamageType == EDamageType.Fall || damageInfo.DamageType == EDamageType.Impact))
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
            if (PlayerStatusEditCfg.DisableFallenDamage.Value && (damageInfo.DamageType == EDamageType.Fall || damageInfo.DamageType == EDamageType.Impact))
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
    [HarmonyPatch(typeof(InventoryEquipment), "smethod_1")]
    public class InfinityWeightPatch
    {
        public static bool Prefix(InventoryEquipment __instance, IEnumerable<Slot> slots, ref float __result)
        {
            if (PlayerStatusEditCfg.EnableInfiniteWeight.Value)
            {
                __result = 0f;
                return false;
            }
            return true;
            //}
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
    public class PlayerStatusEditCfg
    {

        internal static ConfigEntry<bool> EnableInfiniteStamina { get; set; }
        internal static ConfigEntry<bool> EnableInfiniteWeight { get; set; }
        internal static ConfigEntry<bool> DisableFallenDamage { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public static void Initialize(ConfigFile config)
        {
            EnableInfiniteStamina = config.Bind(
                "玩家属性",
                "无限体力",
                true,
                "锁定跑步、举枪体力和屏息氧气为全满状态"
            );
            EnableInfiniteWeight = config.Bind(
                "玩家属性",
                "无限负重",
                true,
                "启用时所有物品将不计入重量"
            );
            DisableFallenDamage = config.Bind(
                "玩家属性",
                "阻止摔落伤害",
                true,
                "防止玩家受到跌落伤害"
            );
        }
    }
}
