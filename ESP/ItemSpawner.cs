using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Oracle.Utils;

namespace Oracle.ESP
{
    public class ItemSpawner
    {
        //核心方法
        public static void SpawnItemIntoInventory(Player player, string templateId)
        {
            //空指针防御和空物品防御
            var itemFactory = Singleton<ItemFactoryClass>.Instance;
            if (itemFactory == null || PluginsCore.CorrectGameWorld == null) return;
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
            if (ItemSpawnerCfg.ForcedFiR.Value) newItem.SpawnedInSession = true;
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
        /// <summary>
        /// 虚空造物：在玩家脚上上方1米处静止生成物品，受重力自然掉落
        /// </summary>
        public static void CloneAndDropItem(Player player, Item originalItem)
        {
            if (player == null || originalItem == null) return;

            var gameWorld = PluginsCore.CorrectGameWorld;
            if (gameWorld == null) return;

            try
            {
                // 1. 深度克隆并彻底洗白子物品的ID (防坏档核心)
                Item clonedItem = originalItem.CloneItem();
                ItemIdHelper.ReassignAllIds(clonedItem);

                // 可选：带勾
                clonedItem.SpawnedInSession = true;

                // 2. 计算坐标：玩家脚下坐标 (Transform.position 通常位于脚底中心) 往上抬高 1 米
                Vector3 spawnPosition = player.Transform.position + new Vector3(0f, 1f, 0f);

                // 💡 防穿模小贴士：
                // 如果直接在玩家正中心生成，物品可能会卡在玩家自己的胶囊碰撞体（CapsuleCollider）里不停鬼畜。
                // 建议稍微往前偏移 0.5 米：
                spawnPosition += player.Transform.forward * 0.5f;

                // 3. 调用底层完全体方法，实现静止生成
                LootItem spawnedLoot = gameWorld.ThrowItem(
                    clonedItem,             // 生成的物品数据
                    player,                 // 归属玩家
                    spawnPosition,          // 生成坐标
                    Quaternion.identity,    // 默认初始角度 (不旋转)
                    Vector3.zero,           // 物理初速度设为 0
                    Vector3.zero,           // 角速度 (旋转力) 设为 0
                    true,                   // syncable (网络同步标识)
                    true                    // performPickUpValidation
                );

                if (spawnedLoot != null)
                {
                    Console.WriteLine($"成功在眼前 1 米处召唤了: {clonedItem.Name.Localized()}");
                }
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"召唤失败: {ex.Message}");
            }
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

