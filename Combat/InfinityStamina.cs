using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
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
    [HarmonyPatch(typeof(InventoryEquipment), "smethod_1")]
    public class InfinityWeightPatch
    {
        public static bool Prefix(InventoryEquipment __instance, IEnumerable<Slot> slots, ref float __result)
        {
            if (InfinityStaminaCfg.EnableInfiniteWeight.Value)
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
        }
    }
}
