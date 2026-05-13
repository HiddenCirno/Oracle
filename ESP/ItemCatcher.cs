using BepInEx.Configuration;
using EFT;
using EFT.HandBook;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Oracle.ESP
{
    //用于捕获物品实例
    public class ItemCatcher
    {
        public static Item selectedItem = null;
        public static Item savedItem = null;
        private static bool _copyKeyLastFrame = false;

        public static void KeyUpdate()
        {
            if (selectedItem == null)
                return;
            bool isCopyPressed = HotKeyManager.CopyItemKey.Value.IsPressed();
            if (isCopyPressed && !_copyKeyLastFrame)
            {
                string itemID = selectedItem.TemplateId;
                string itemName = selectedItem.Name.Localized();
                savedItem = selectedItem;

                // 游戏内右下角通知
                NotificationManagerClass.DisplayMessageNotification(
                    $"物品{itemName}已存储至内存区域: {itemID}",
                    EFT.Communications.ENotificationDurationType.Default,
                    EFT.Communications.ENotificationIconType.Default,
                    null
                );
            }
            _copyKeyLastFrame = isCopyPressed;
        }
    }
    //Patch
    //全都是用于捕获物品实例的Patch
    // ==========================================
    // 悬停获取 (Pointer Enter)
    // ==========================================
    [HarmonyPatch(typeof(ItemView), "OnPointerEnter")]
    internal static class ItemView_PointEnterPatch
    {
        private static void Prefix(ItemView __instance)
        {
            if (__instance.Item != null) ItemCatcher.selectedItem = __instance.Item;
        }
    }

    [HarmonyPatch(typeof(EntityIcon), "method_1")]
    internal static class EntityIcon_PointEnterPatch
    {
        private static void Prefix(EntityIcon __instance)
        {
            Item item = Traverse.Create(__instance).Field("item_0").GetValue<Item>();
            if (item != null) ItemCatcher.selectedItem = item;
        }
    }

    [HarmonyPatch(typeof(TradingRequisitePanel), "method_1")]
    internal static class TradingRequisitePanel_PointEnterPatch
    {
        private static void Prefix(TradingRequisitePanel __instance)
        {
            // 增加空检查，防止该面板当前没有上下文报错
            var context = Traverse.Create(__instance).Field("itemContextAbstractClass").GetValue();
            if (context != null)
            {
                Item item = Traverse.Create(context).Property("Item").GetValue<Item>();
                if (item != null) ItemCatcher.selectedItem = item;
            }
        }
    }

    [HarmonyPatch(typeof(GridItemView), "OnPointerEnter")]
    internal static class GridItemView_PointEnterPatch
    {
        private static void Prefix(GridItemView __instance)
        {
            if (__instance.Item != null) ItemCatcher.selectedItem = __instance.Item;
        }
    }
    [HarmonyPatch(typeof(HideoutItemView), "OnPointerEnter")]
    internal static class HideoutItemViewPointEnterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(HideoutItemView __instance)
        {
            if (__instance.Item != null) ItemCatcher.selectedItem = __instance.Item;
        }
    }
    // ==========================================
    // 移出清除 (Pointer Exit)
    // ==========================================
    [HarmonyPatch(typeof(ItemView), "OnPointerExit")]
    internal static class ItemView_PointOuterPatch
    {
        private static void Prefix() => ItemCatcher.selectedItem = null;
    }

    [HarmonyPatch(typeof(EntityIcon), "method_2")]
    internal static class EntityIcon_PointOuterPatch
    {
        private static void Prefix() => ItemCatcher.selectedItem = null;
    }

    [HarmonyPatch(typeof(TradingRequisitePanel), "method_2")]
    internal static class TradingRequisitePanel_PointOuterPatch
    {
        private static void Prefix() => ItemCatcher.selectedItem = null;
    }

    [HarmonyPatch(typeof(GridItemView), "OnPointerExit")]
    internal static class GridItemView_PointOuterPatch
    {
        private static void Prefix() => ItemCatcher.selectedItem = null;
    }
    //配置定义
    public class ItemCatcherCfg
    {

    }
}
