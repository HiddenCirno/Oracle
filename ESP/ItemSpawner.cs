using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Oracle.Utils;

namespace Oracle.ESP
{
    public class ItemSpawner
    {
        /// <summary>
        /// 核心方法：尝试生成物品到玩家背包，如果背包满了则自动掉落到地上
        /// 注意：因为包含模型加载，此方法已改为异步 (async Task)
        /// </summary>
        public static async Task SpawnItemIntoInventoryAsync(Player player, string templateId)
        {
            var itemFactory = Singleton<ItemFactoryClass>.Instance;
            var gameWorld = PluginsCore.CorrectGameWorld;

            if (itemFactory == null || gameWorld == null) return;
            if (!itemFactory.ItemTemplates.ContainsKey(templateId)) return;

            // 生成唯一Id
            string newId = MongoID.Generate();
            // 创建物品
            Item newItem = itemFactory.CreateItem(newId, templateId, null);
            if (newItem == null) return;

            // 强制物品为带勾状态
            if (ItemSpawnerCfg.ForcedFiR.Value) newItem.SpawnedInSession = true;

            // 满堆叠处理
            newItem.StackObjectsCount = ItemSpawnerCfg.CustomStackSize.Value;
            if (newItem.Template.StackMaxSize > 1 && ItemSpawnerCfg.MaxStack.Value)
            {
                newItem.StackObjectsCount = newItem.Template.StackMaxSize;
            }

            // 💡 核心修复：即使是生成到背包的物品，也必须预加载模型
            // 否则如果玩家之后将其丢弃或者在检视(Inspect)时，会因为找不到模型而消失或报错
            await LoadItemBundlesAsync(newItem);

            // 寻找空位置
            ItemAddress targetLocation = FindEmptyLocation(player, newItem);

            if (targetLocation != null)
            {
                // 网络包参数
                var addOperationResult = InteractionsHandlerClass.Add(
                    newItem,
                    targetLocation,
                    player.InventoryController,
                    false
                );

                if (addOperationResult.Succeeded)
                {
                    try
                    {
                        player.InventoryController.TryRunNetworkTransaction(addOperationResult);
                        // Console.WriteLine($"成功将物品放入背包: {newItem.Name.Localized()}");
                    }
                    catch (Exception) { }
                }
                else
                {
                    // 备用逻辑：操作失败，转为掉落
                    DropItemToGround(player, newItem, gameWorld);
                }
            }
            else
            {
                // 备用逻辑：背包满了，直接掉落在地上
                DropItemToGround(player, newItem, gameWorld);
            }
        }

        /// <summary>
        /// 完美虚空造物：深度克隆、洗白ID、加载所有配件模型并在玩家面前静止掉落
        /// </summary>
        public static async Task CloneAndDropItemAsync(Player player, Item originalItem, Camera cam)
        {
            if (player == null || originalItem == null) return;

            var gameWorld = PluginsCore.CorrectGameWorld;
            if (gameWorld == null) return;

            //Console.WriteLine($"尝试生成物品");
            try
            {
                //Console.WriteLine($"清洗ID");
                // 1. 深度克隆并彻底洗白所有子物品的ID (防坏档核心)
                Item clonedItem = originalItem.CloneItem();
                ItemIdHelper.ReassignAllIds(clonedItem);
                // 强制带勾
                ItemIdHelper.CleanAndResetItem(clonedItem, ItemSpawnerCfg.ForcedFiR.Value);
                //clonedItem.SpawnedInSession = true;

                // 2. 💡核心修复：递归加载主物品及所有配件的3D模型
                await LoadItemBundlesAsync(clonedItem);

                // 3. 在世界上掉落
                DropItem(player, clonedItem, gameWorld, cam);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"物品生成失败: {ex.Message}\n{ex.StackTrace}");
                //Console.WriteLine($"召唤失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 辅助方法：处理物理掉落逻辑
        /// </summary>
        private static void DropItemToGround(Player player, Item item, GameWorld gameWorld)
        {
            //Console.WriteLine($"开始生成物品");
            // 计算坐标：玩家脚底抬高 1 米
            Vector3 spawnPosition = player.Transform.position + new Vector3(0f, 1f, 0f);

            // 往前偏移 0.5 米防穿模
            spawnPosition += player.Transform.forward * 0.5f;

            // 给一点点高度随机数，防止连续多次掉落卡在完全相同的坐标
            spawnPosition.y += UnityEngine.Random.Range(-0.05f, 0.1f);

            //Console.WriteLine($"生成物品......");
            // 调用底层方法，实现静止生成 (Owner 设为 null 防止拾取 Bug)
            LootItem spawnedLoot = gameWorld.ThrowItem(
                item,                   // 物品数据
                null,                   // Owner设为null，作为世界刷新物
                spawnPosition,          // 坐标
                Quaternion.identity,    // 不旋转
                Vector3.zero,           // 物理初速度 0
                Vector3.zero,           // 角速度 0
                true,                   // syncable
                true                    // performPickUpValidation
            );

            if (spawnedLoot != null)
            {
                //Console.WriteLine($"物品已掉落在前方: {item.Name.Localized()}");
            }
            else
            {
                //Console.WriteLine($"物品生成失败......");
            }
        }

        private static void DropItem(Player player, Item item, GameWorld gameWorld, Camera cam)
        {
            // 防御空摄像机
            if (cam == null) return;

            // 1. 坐标：绝对视线！
            // cam.transform.position 就是真实的眼睛坐标（不需要再手动加1米高度了）
            // cam.transform.forward 就是屏幕正中心射出去的射线（包含了XYZ的完整空间朝向）
            Vector3 spawnPosition = cam.transform.position + (cam.transform.forward * 0.8f);

            // 给一点点高度随机数，防止连续多次掉落卡在完全相同的坐标
            //spawnPosition.y += UnityEngine.Random.Range(-0.05f, 0.1f);

            // 2. 角度：XZ 拍平，Y 轴与摄像机视线一致
            // 直接抓取摄像机的 Y 轴偏航角，忽略你看天还是看地，只保留水平旋转
            Quaternion spawnRotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);

            // 3. 调用底层生成
            LootItem spawnedLoot = gameWorld.ThrowItem(
                item,                   // 物品数据
                null,                   // Owner设为null，作为世界刷新物
                spawnPosition,          // 绝对视线前方坐标
                spawnRotation,          // XZ水平、Y轴一致的旋转
                Vector3.zero,           // 物理初速度 0
                Vector3.zero,           // 角速度 0
                true,                   // syncable
                true                    // performPickUpValidation
            );

            if (spawnedLoot != null)
            {
                // Console.WriteLine($"物品已生成在视线前方: {item.Name.Localized()}");
            }
        }

        /// <summary>
        /// 辅助方法：递归收集物品树中的所有 3D 模型 Prefab 并异步加载
        /// </summary>
        private static async Task LoadItemBundlesAsync(Item rootItem)
        {
            var poolManager = Singleton<PoolManagerClass>.Instance;
            if (poolManager == null) return;

            // 使用 HashSet 自动对相同的配件或子弹去重
            var keys = new HashSet<ResourceKey>();

            // GetAllItems 包含自身及所有内含物/配件
            foreach (var item in rootItem.GetAllItems())
            {
                if (item.Template?.Prefab != null) keys.Add(item.Template.Prefab);
                if (item.Template?.UsePrefab != null) keys.Add(item.Template.UsePrefab);
            }

            if (keys.Count > 0)
            {
                // 发起异步加载
                await poolManager.LoadBundlesAndCreatePools(
                    0,
                    PoolManagerClass.AssemblyType.Local,
                    keys.ToArray(),
                    JobPriorityClass.Immediate,
                    null,
                    CancellationToken.None
                );
            }
        }

        public static async void SpawnItemIntoInventory(Player player, string templateId)
        {
            try
            {
                // 直接调用并等待。
                // 因为没有 Task.Run，它会默认在 Unity 的主线程上开始执行，
                // 直到遇到内部的 await (加载模型时) 才会让出主线程，加载完后继续在主线程执行。
                await Oracle.ESP.ItemSpawner.SpawnItemIntoInventoryAsync(player, templateId);
            }
            catch (Exception ex)
            {
                // 如果异步过程中有任何报错，必须在这里手动拦截并打印，否则你永远看不到错误！
                //Logger.LogError($"[虚空造物] 异步生成掉落物时发生严重错误: {ex}");

                NotificationManagerClass.DisplayMessageNotification(
                    "生成物品失败！",
                    EFT.Communications.ENotificationDurationType.Default,
                    EFT.Communications.ENotificationIconType.Alert
                );
            }
        }
        public static async void CloneAndDropItem(Player player, Item item)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            try
            {
                // 直接调用并等待。
                // 因为没有 Task.Run，它会默认在 Unity 的主线程上开始执行，
                // 直到遇到内部的 await (加载模型时) 才会让出主线程，加载完后继续在主线程执行。
                await Oracle.ESP.ItemSpawner.CloneAndDropItemAsync(player, item, cam);
            }
            catch (Exception ex)
            {
                // 如果异步过程中有任何报错，必须在这里手动拦截并打印，否则你永远看不到错误！
                //Logger.LogError($"[虚空造物] 异步生成掉落物时发生严重错误: {ex}");

                NotificationManagerClass.DisplayMessageNotification(
                    "生成物品失败！",
                    EFT.Communications.ENotificationDurationType.Default,
                    EFT.Communications.ENotificationIconType.Alert
                );
            }
        }

        // 空位置查找算法 (保持不变)
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