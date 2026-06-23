using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using Oracle.Data;
using Oracle.Utils;
using System.Collections.Generic;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Ability
{
    /// <summary>
    /// 无限耐力/无限负重
    /// </summary>
    public class InfinityStamina
    {
        /// <summary>
        /// 耐力锁定脚本
        /// </summary>
        public class InfinityStaminaComponent : MonoBehaviour
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
                    bool isInfinite = InfinityStaminaCfg.EnableInfiniteStamina.Value;
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

    //无限负重Patch
    [HarmonyPatch(typeof(InventoryEquipment), "smethod_1")]
    public class InfinityWeightPatch
    {
        public static bool Prefix(InventoryEquipment __instance, IEnumerable<Slot> slots, ref float __result)
        {
            if (InfinityStaminaCfg.EnableInfiniteWeight.Value)
            {
                //直接不计重量
                __result = 0f;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    [OracleCfgOrder(2)]
    public class InfinityStaminaCfg : IOracleCfg
    {

        internal static ConfigEntry<bool> EnableInfiniteStamina { get; set; }
        internal static ConfigEntry<bool> EnableInfiniteWeight { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            EnableInfiniteStamina = config.Bind(
                "2. 生命之树 / Ability Module",
                "无限体力",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_ability_module_infinity_stamina_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_ability_module_infinity_stamina_name"),
                        IsAdvanced = false,
                        Order = 210
                    }
                )
            );
            EnableInfiniteWeight = config.Bind(
                "2. 生命之树 / Ability Module",
                "无限负重",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_ability_module_infinity_weight_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_ability_module_infinity_weight_name"),
                        IsAdvanced = false,
                        Order = 209
                    }
                )
            );
        }
    }
}
