using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using System.Text;

namespace Oracle.ESP
{
    public class ItemSpawner
    {
        //核心方法
        public static void SpawnItemIntoInventory(Player player, string templateId)
        {
            //空指针防御和空物品防御
            var itemFactory = Singleton<ItemFactoryClass>.Instance;
            if (itemFactory == null || PluginsCore.CorrectGameWorld==null) return;
            if (!itemFactory.ItemTemplates.ContainsKey(templateId))
            {
                return;
            }
            //尝试读取本地化并通过log输出, 但是有问题, 这里创建的是ItemtemTemplate而不是Item, ItemTemplate似乎无法进行本地化, 因此注释掉
            if (itemFactory.ItemTemplates.TryGetValue(templateId, out var template))
            {
                //Console.WriteLine($"[ShowLootValue] 准备生成物品: {template.Name} (ID: {templateId})");
            }
            //生成唯一Id
            string newId = MongoID.Generate();
            //创建物品
            Item newItem = itemFactory.CreateItem(newId, templateId, null);
            //空值防御
            if (newItem == null) return;
            //强制物品为带勾状态
            if(ItemSpawnerCfg.ForcedFiR.Value) newItem.SpawnedInSession = true;
            //满堆叠
            //堆叠无检查, 因此似乎理论存在强制堆叠的可能?
            //需要验证
            //此法, 可行!
            newItem.StackObjectsCount = ItemSpawnerCfg.CustomStackSize.Value;
            if (newItem.Template.StackMaxSize > 1 && ItemSpawnerCfg.MaxStack.Value)
            {
                newItem.StackObjectsCount = newItem.Template.StackMaxSize;
            }
            //原生方法, 因为回传无法被插件调用, 已经抛弃
            //ItemAddress targetLocation = player.InventoryController.FindGridToPickUp(newItem);
            ItemAddress targetLocation = FindEmptyLocation(player, newItem);

            if (targetLocation != null)
            {
                //网络包参数
                var addOperationResult = InteractionsHandlerClass.Add(
                    newItem,
                    targetLocation,
                    player.InventoryController,
                    false
                );
                //检查回传状态
                if (addOperationResult.Succeeded)
                {
                    //发包并捕获异常
                    //由于物品无来源, 不进行捕获会导致控制台报错, 但不影响使用
                    try
                    {
                        player.InventoryController.TryRunNetworkTransaction(addOperationResult);
                    }
                    catch (System.Exception)
                    {
                    }
                }
                else
                {
                    //备用逻辑执行处
                }
            }
            else
            {
                //备用逻辑执行处
            }
        }
        //空位置查找算法
        private static ItemAddress FindEmptyLocation(Player player, Item newItem)
        {
            var equipment = player.Inventory.Equipment;
                EquipmentSlot[] slotsToCheck = {
                EquipmentSlot.Pockets,
                EquipmentSlot.TacticalVest,
                EquipmentSlot.Backpack
            };
            foreach (var slotType in slotsToCheck)
            {
                var slot = equipment.GetSlot(slotType);
                if (slot.ContainedItem is CompoundItem containerItem)
                {
                    foreach (var grid in containerItem.Grids)
                    {
                        var addressInGrid = grid.FindLocationForItem(newItem);
                        if (addressInGrid != null)
                        {
                            return (ItemAddress)addressInGrid;
                        }
                    }
                }
            }
            return null;
        }
    }
    public static class ItemSpawnerCfg
    {
        internal static ConfigEntry<string> TargetItemId { get; set; }
        internal static ConfigEntry<bool> MaxStack { get; set; }
        internal static ConfigEntry<bool> ForcedFiR { get; set; }
        internal static ConfigEntry<int> CustomStackSize { get; set; }

        public static void Initialize(ConfigFile config)
        {
            TargetItemId = config.Bind(
                "虚空造物",
                "物品 Template ID",
                "59faff1d86f7746c51718c9c",
                "请输入你想生成的物品的24位16进制ID"
            );
            MaxStack = config.Bind(
                "虚空造物",
                "强制最大堆叠",
                false,
                "刷出的物品为最大堆叠而不是单个"
            );
            ForcedFiR = config.Bind(
                "虚空造物",
                "强制物品带勾",
                true,
                "刷出的物品为战局中发现状态"
            );
            CustomStackSize = config.Bind(
                "虚空造物",
                "自定义堆叠数量(强制性)",
                1,
                "自定义刷出物品的堆叠数"
            );
        }
    }
}
