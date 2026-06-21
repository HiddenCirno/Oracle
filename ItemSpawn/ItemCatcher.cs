using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using EFT.HandBook;
using EFT.HealthSystem;
using EFT.Hideout;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Oracle.Data;
using Oracle.Tools;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.ItemSpawn
{
    /// <summary>
    /// 用于捕获物品实例的工具类
    /// </summary>
    public class ItemCatcher
    {
        //变量缓存区
        //当前指针指向的物品实例
        public static Item selectedItem = null;
        //复制的物品实例指针
        //通过这种方式将物品实例保存到内存里以进行复制
        public static Item savedItem = null;
        public static List<Item> SavedItems = new List<Item>();
        public void RegisterKeyUpdate()
        {
            OracleEvent.OnUpdate += KeyUpdate;
        }
        /// <summary>
        /// 快捷键监听
        /// </summary>
        public void KeyUpdate()
        {
            if (selectedItem == null)
                return;
            if (ItemSpawnerCfg.CopyItemKey.Value.IsDown())
            {
                string itemID = selectedItem.TemplateId;
                string itemName = selectedItem.Name.Localized();
                //复制-清洗Id-清洗状态, 使用两个拓展方法一步搞定
                savedItem = selectedItem.CloneItem().ReassignAllIds();//.CleanAndResetItem(ItemSpawnerCfg.ForcedFiR.Value);//这里不能清洗状态, 它涉及到带勾机制, 由玩家自己决定
                SavedItems.Add(savedItem);
                //游戏内通知
                OracleNotify.Message($"物品{itemName}已存储至内存区域: {itemID}", ENotificationIconType.Default, GlobalCfg.MuteNotice.Value);
                NotificationManagerClass.DisplayMessageNotification(
                    $"物品{itemName}已存储至内存区域: {itemID}",
                    EFT.Communications.ENotificationDurationType.Default,
                    EFT.Communications.ENotificationIconType.Default,
                    null
                );
            }
        }
    }
    //Patch
    //全都是用于捕获物品实例的Patch
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
    //退出点
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
    /// <summary>
    /// 配置项定义, 留空了, 复制来的
    /// </summary>
    public class ItemCatcherCfg
    {

    }
}
