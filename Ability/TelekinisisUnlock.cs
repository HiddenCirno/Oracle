using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using EFT.Interactive;
using HarmonyLib;
using Oracle.Data;
using Oracle.Utils;
using System;
using System.Linq;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Ability
{
    /// <summary>
    /// 念力开锁
    /// </summary>
    public static class TelekinisisUnlock
    {
        //拦截互动菜单
        [HarmonyPatch(typeof(GetActionsClass), "GetAvailableActions", new Type[] { typeof(GamePlayerOwner), typeof(GInterface177) })]
        public class GetActionsClassPatch
        {
            public static void Postfix(GamePlayerOwner owner, GInterface177 interactive, ref ActionsReturnClass __result)
            {
                if (interactive == null || __result == null || !TelekinisisUnlockCfg.EnableTelekinisisUnlock.Value) return;

                //查找Component
                Component comp = interactive as Component;
                if (comp == null) return;

                //可交互物体
                WorldInteractiveObject wio = comp.GetComponent<WorldInteractiveObject>() ?? comp.GetComponentInParent<WorldInteractiveObject>();
                if (wio == null) return;

                //门(只针对门)
                if (wio.DoorState == EDoorState.Locked)
                {
                    //如果没有解锁选项, 就加一个
                    bool hasUnlock = __result.Actions.Any(x => x.Name == "Unlock" || x.Name.Contains("Unlock"));
                    if (!hasUnlock)
                    {
                        //通过Insert让它变为首选项
                        __result.Actions.Insert(0, new ActionsTypesClass
                        {
                            Name = "Unlock",
                            Action = new Action(() =>
                            {
                                //防止Sain空指针
                                wio.SetUser(owner.Player);
                                wio.DoorState = EDoorState.Shut;
                            }),
                            Disabled = false
                        });
                    }
                }
                /* 归档代码
                // ⭐ 场景 B：门已解锁，但因为 NoPowerTip 导致没有推开选项！
                else if (wio.DoorState == EDoorState.Shut)
                {
                    bool hasOpen = __result.Actions.Any(x => x.Name == "OpenDoor" || x.Name.Contains("Open"));
                    if (!hasOpen)
                    {
                        __result.Actions.Add(new ActionsTypesClass
                        {
                            Name = "Touch", // 或者叫 "Force Open", "原力开启" 等
                            Action = new Action(() =>
                            {
                                // 告诉 SAIN 和底层逻辑，是“我”干的
                                wio.SetUser(owner.Player);

                                // 直接发送 Open 指令！
                                // 底层会自动判断：如果是锁着的，它会先解锁再开门；如果没锁，直接开！
                                wio.Interact(new InteractionResult(EInteractionType.Open));

                                NotificationManagerClass.DisplayMessageNotification("原力交互成功！", ENotificationDurationType.Default, ENotificationIconType.Quest);
                            }),
                            Disabled = false
                        });
                    }
                }
                */
            }
        }

        /* 归档Patch
        [HarmonyPatch(typeof(WorldInteractiveObject), "OnEnable")]
        public class VulcanCore_WIO_OnEnable_Patch
        {
            public static void Postfix(WorldInteractiveObject __instance)
            {
                if (__instance == null) return;

                // ⭐ 1. 撕掉“禁止交互”的封条
                // 只要它是门，就必须允许玩家跟它互动！
                if (__instance.NoInteractionsAllowed)
                {
                    //__instance.NoInteractionsAllowed = false;
                }

                // ⭐ 2. 强行接通电源 / 解除操作限制
                // 防止它以“没电”或“不可操作”为由拒绝生成开门选项
                if (!__instance.Operatable)
                {
                    //__instance.Operatable = true;
                }
            }
        }
        */
    }

    /// <summary>
    /// 配置定义
    /// </summary>
    [OracleCfgOrder(2)]
    public class TelekinisisUnlockCfg : IOracleCfg
    {
        public static ConfigEntry<bool> EnableTelekinisisUnlock { get; set; }
        public void Initialize(ConfigFile config)
        {
            EnableTelekinisisUnlock = config.Bind(
                "2. 生命之树 / Ability Module", 
                "念力解锁", 
                false,
                new ConfigDescription(
                    LocaleManager.Get("cfg_ability_module_telekinisis_unlock_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_ability_module_telekinisis_unlock_name"),
                        IsAdvanced = false,
                        Order = 190
                    }
                )
            );
        }
    }
}