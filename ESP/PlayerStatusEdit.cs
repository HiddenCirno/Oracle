using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Oracle.ESP
{
    public class PlayerStatusEdit
    {
        //锁定脚本
        //在OnGameStartPatch里挂载到MainPlayer上
        public class PlayerStatusEditComponent : MonoBehaviour
        {
            private Player localPlayer;
            private void Awake()
            {
                //查找玩家组件
                localPlayer = this.gameObject.GetComponent<Player>();
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
            //仅自己判断
            //if (!__instance.IsYourPlayer)
            //{
            //    return true;
            //}
            //else
            //{
            //伤害类型过滤
            if (PlayerStatusEditCfg.DisableFallenDamage.Value && (damageInfo.DamageType == EDamageType.Fall || damageInfo.DamageType == EDamageType.Impact))
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
    //配置定义
    public class PlayerStatusEditCfg
    {

        internal static ConfigEntry<bool> EnableInfiniteStamina { get; set; }
        internal static ConfigEntry<bool> DisableFallenDamage { get; set; }
        public static void Initialize(ConfigFile config)
        {
            EnableInfiniteStamina = config.Bind(
                "玩家属性",
                "无限体力",
                true,
                "锁定跑步、举枪体力和屏息氧气为全满状态"
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
