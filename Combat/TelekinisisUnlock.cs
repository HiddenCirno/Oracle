using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using EFT.Interactive;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    public static class TelekinisisUnlock
    {
        // 拦截实体门生成 F 键菜单的方法
        [HarmonyPatch(typeof(GetActionsClass), "GetAvailableActions", new Type[] { typeof(GamePlayerOwner), typeof(GInterface177) })]
        public class GetActionsClassPatch
        {
            public static void Postfix(GamePlayerOwner owner, GInterface177 interactive, ref ActionsReturnClass __result)
            {
                if (interactive == null || __result == null) return;

                // 1. GInterface177 本质上是挂载在物体上的 Component
                Component comp = interactive as Component;
                if (comp == null) return;

                // 2. 顺藤摸瓜，不管你当前触发的是 Door、KeycardDoor 还是 NoPowerTip，
                // 只要同物体或父物体上有 WorldInteractiveObject (WIO)，我们就能掌控它！
                WorldInteractiveObject wio = comp.GetComponent<WorldInteractiveObject>() ?? comp.GetComponentInParent<WorldInteractiveObject>();
                if (wio == null) return;

                // ⭐ 场景 A：门还锁着（不管因为啥锁的）
                if (wio.DoorState == EDoorState.Locked)
                {
                    // 如果菜单里没有解锁选项，我们就造一个
                    bool hasUnlock = __result.Actions.Any(x => x.Name == "Unlock" || x.Name.Contains("Unlock"));
                    if (!hasUnlock)
                    {
                        __result.Actions.Insert(0, new ActionsTypesClass
                        {
                            Name = "Unlock",
                            Action = new Action(() =>
                            {
                                wio.DoorState = EDoorState.Shut;
                                NotificationManagerClass.DisplayMessageNotification("系统破解成功，锁芯已解除！", ENotificationDurationType.Default, ENotificationIconType.Quest);
                            }),
                            Disabled = false
                        });
                    }
                }
                /*
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
    }
    public class TelekinisisUnlockCfg : IOracleCfg
    {
        public static ConfigEntry<bool> EnableTelekinisisUnlock { get; set; }
        public void Initialize(ConfigFile config)
        {
            EnableTelekinisisUnlock = config.Bind("念力解锁", "启用念力解锁", false, "开启后可以无条件解锁任意上锁物体");
        }
    }
}