using EFT;
using EFT.Communications;
using EFT.HandBook;
using EFT.Hideout;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Oracle.Data;
using Oracle.Utils;
using System.Collections.Generic;
using static Oracle.Data.OracleInterface;

namespace Oracle.ItemSpawn
{
    /// <summary>
    /// 用于捕获物品实例的工具类
    /// </summary>
    public class ItemCatcher : IOracleKeyUpdate
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
            if (selectedItem == null) return;
            if (ItemSpawnerCfg.CopyItemKey.Value.IsDown())
            {
                string itemID = selectedItem.TemplateId;
                string itemName = selectedItem.Name.Localized();
                //复制-清洗Id-清洗状态, 使用两个拓展方法一步搞定
                savedItem = selectedItem.CloneItem().ReassignAllIds();
                //.CleanAndResetItem(ItemSpawnerCfg.ForcedFiR.Value);//这里不能清洗状态, 它涉及到带勾机制, 由玩家自己决定
                RaidManager.ItemManagerGUI.ActiveList.Add(savedItem);
                //SavedItems.Add(savedItem);
                //游戏内通知
                OracleNotify.Message(string.Format("text_item_instance_manager_item_saved".i18n(), itemName, itemID),  ENotificationIconType.Default, GlobalCfg.MuteNotice.Value);
            }
        }
    }

    //Patch
    //全都是用于捕获物品实例的Patch
    [HarmonyPatch(typeof(ItemView), nameof(ItemView.OnPointerEnter))]
    internal static class ItemView_PointEnterPatch
    {
        private static void Prefix(ItemView __instance)
        {
            if (__instance.Item != null) ItemCatcher.selectedItem = __instance.Item;
        }
    }

    [HarmonyPatch(typeof(EntityIcon), nameof(EntityIcon.CG_Awake))]
    internal static class EntityIcon_PointEnterPatch
    {
        private static void Prefix(EntityIcon __instance)
        {
            Item item = Traverse.Create(__instance).Field("_item").GetValue<Item>();
            if (item != null) ItemCatcher.selectedItem = item;
        }
    }

    [HarmonyPatch(typeof(TradingRequisitePanel), nameof(TraderRequirementPanel.CG_Awake))]
    internal static class TradingRequisitePanel_PointEnterPatch
    {
        private static void Prefix(TradingRequisitePanel __instance)
        {
            var context = Traverse.Create(__instance).Field("_itemContext").GetValue();
            if (context != null)
            {
                Item item = Traverse.Create(context).Property("Item").GetValue<Item>();
                if (item != null) ItemCatcher.selectedItem = item;
            }
        }
    }

    [HarmonyPatch(typeof(GridItemView), nameof(GridItemView.OnPointerEnter))]
    internal static class GridItemView_PointEnterPatch
    {
        private static void Prefix(GridItemView __instance)
        {
            if (__instance.Item != null) ItemCatcher.selectedItem = __instance.Item;
        }
    }

    [HarmonyPatch(typeof(HideoutItemView), nameof(HideoutItemView.OnPointerEnter))]
    internal static class HideoutItemViewPointEnterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(HideoutItemView __instance)
        {
            if (__instance.Item != null) ItemCatcher.selectedItem = __instance.Item;
        }
    }

    //退出点
    [HarmonyPatch(typeof(ItemView), nameof(ItemView.OnPointerExit))]
    internal static class ItemView_PointOuterPatch
    {
        private static void Prefix() => ItemCatcher.selectedItem = null;
    }

    [HarmonyPatch(typeof(EntityIcon), nameof(EntityIcon.CG_Awake1))]
    internal static class EntityIcon_PointOuterPatch
    {
        private static void Prefix() => ItemCatcher.selectedItem = null;
    }

    [HarmonyPatch(typeof(TradingRequisitePanel), nameof(TradingRequisitePanel.CG_Awake1))]
    internal static class TradingRequisitePanel_PointOuterPatch
    {
        private static void Prefix() => ItemCatcher.selectedItem = null;
    }

    [HarmonyPatch(typeof(GridItemView), nameof(ItemView.OnPointerExit))]
    internal static class GridItemView_PointOuterPatch
    {
        private static void Prefix() => ItemCatcher.selectedItem = null;
    }
}
