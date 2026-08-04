using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Oracle.RaidManager;
using Oracle.Utils;
using System;

namespace Oracle.ItemSpawn
{
    public static class ItemSpawnStashPatch
    {
        //捕获invctrler
        [HarmonyPatch(typeof(InventoryScreen), nameof(InventoryScreen.Show), new Type[]
        {
        typeof(IHealthController),
        typeof(InventoryController),
        typeof(EFT.Quests.QuestController),
        typeof(EFT.Achievements.AchievementsController),
        typeof(EFT.Prestige.PrestigeController),
        typeof(CompoundItem),
        typeof(EInventoryTab),
        typeof(EFT.IEftSession),
        typeof(ItemContext),
        typeof(bool)
        })]
        public class InventoryScreen_Show_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(InventoryController controller)
            {
                if (controller != null)
                {
                    PluginsCore.StashController = controller;
                }
                else
                {
                    Console.WriteLine("[Oracle]由于未知原因，InventoryController为空！");
                }
            }
        }

        //桥接请求
        [HarmonyPatch(typeof(ItemController), "ConvertOperationResultToOperation")]
        public class Patch_ConvertOperation
        {
            [HarmonyPrefix]
            public static bool Prefix(ItemController __instance, IOperationResult operationResult, ref EFT.InventoryLogic.Operations.AbstractOperation __result)
            {
                try
                {
                    //没有物品直接跳过
                    if (ItemManagerGUI.generatedItem == null) return true;
                    
                    //确认物品
                    Item targetItem = ItemManagerGUI.generatedItem;

                    //类名检查
                    //3405是ADD
                    //你妈的这段4.1是不是得改
                    string operationTypeName = operationResult.GetType().Name;
                    if (targetItem != null && operationTypeName == "AddResult")
                    {
                        var method12 = AccessTools.Method(operationResult.GetType().BaseType, "method_12")
                                    ?? AccessTools.Method(__instance.GetType(), "method_12");

                        if (method12 != null)
                        {
                            ushort txId = (ushort)method12.Invoke(__instance, null);

                            //桥接到自定义路由
                            __result = new OracleAddOperationClass(txId, __instance, targetItem);
                            Console.WriteLine($"[Oracle] {operationTypeName}桥接成功");

                            //清空缓存
                            ItemManagerGUI.generatedItem = null;

                            //不再执行
                            return false;
                        }
                        else
                        {
                            Console.WriteLine("[Oracle] 警告： method_12 获取失败！");
                        }
                    }
                }
                catch (Exception ex)
                {
                    OracleCommon.ShowError(ex);
                }

                //正常路由
                return true;
            }
        }
    }
}