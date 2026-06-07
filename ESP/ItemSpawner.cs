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
    /// <summary>
    /// 虚空造物部分
    /// </summary>
    public class ItemSpawner
    {
        /// <summary>
        /// 尝试异步添加物品到玩家物品栏
        /// </summary>
        /// <param name="player">玩家实例</param>
        /// <param name="templateId">指定的物品ID(模板id, 即tpl, 非唯一ID, 这两个东西都使用MongoId规范是真的害人....)</param>
        /// <returns></returns>
        public static async Task SpawnItemIntoInventoryAsync(Player player, string templateId)
        {
            //提取单例和当前实例
            var itemFactory = Singleton<ItemFactoryClass>.Instance;
            var gameWorld = PluginsCore.CorrectGameWorld;
            //防御性检查
            if (itemFactory == null || gameWorld == null) return;
            if (!itemFactory.ItemTemplates.ContainsKey(templateId)) return;
            //随机生成一个新的唯一ID
            string newId = MongoID.Generate();
            //构造物品
            Item newItem = itemFactory.CreateItem(newId, templateId, null);
            if (newItem == null) return;
            //带勾
            if (ItemSpawnerCfg.ForcedFiR.Value) newItem.SpawnedInSession = true;
            //设置物品堆叠
            newItem.StackObjectsCount = ItemSpawnerCfg.CustomStackSize.Value;
            if (newItem.Template.StackMaxSize > 1 && ItemSpawnerCfg.MaxStack.Value)
            {
                newItem.StackObjectsCount = newItem.Template.StackMaxSize;
            }
            //异步读取物品资产, 防止出现问题
            await LoadItemBundlesAsync(newItem);
            //自定义寻址
            ItemAddress targetLocation = FindEmptyLocation(player, newItem);
            //有效地址, 尝试发包
            if (targetLocation != null)
            {
                //配置网络包
                var addOperationResult = InteractionsHandlerClass.Add(
                    newItem,
                    targetLocation,
                    player.InventoryController,
                    false
                );
                //发包成功
                if (addOperationResult.Succeeded)
                {
                    try
                    {
                        //执行
                        //这里会弹出无源错误, 在战局内这个错误不影响实际使用, Fika环境下可能出现同步问题但问题不大, 因此直接捕获即可
                        player.InventoryController.TryRunNetworkTransaction(addOperationResult);
                        // Console.WriteLine($"成功将物品放入背包: {newItem.Name.Localized()}");
                    }
                    catch (Exception) { }
                }
                else
                {
                    //未知原因导致的发包失败, 转为掉落物品
                    DropItemToGround(player, newItem, gameWorld);
                }
            }
            else
            {
                //背包满了, 掉落物品
                DropItemToGround(player, newItem, gameWorld);
            }
        }
        /// <summary>
        /// 虚空造物并掉落物品(异步执行)
        /// </summary>
        /// <param name="player">玩家实例</param>
        /// <param name="originalItem">捕获并存储的物品实例</param>
        /// <param name="cam">摄像机</param>
        /// <returns></returns>
        public static async Task CloneAndDropItemAsync(Player player, Item originalItem, Camera cam)
        {
            //防御检查
            if (player == null || originalItem == null) return;
            var gameWorld = PluginsCore.CorrectGameWorld;
            if (gameWorld == null) return;
            try
            {
                //复制物品-清洗ID-清洗状态, 通过两个拓展方法一步完成
                Item clonedItem = originalItem.CloneItem().ReassignAllIds().CleanAndResetItem(ItemSpawnerCfg.ForcedFiR.Value);;
                //递归加载所有物品资产
                await LoadItemBundlesAsync(clonedItem);
                //生成掉落物
                DropItem(player, clonedItem, gameWorld, cam);
            }
            catch (Exception ex)
            {
                //捕获奇怪的错误
                Console.WriteLine($"物品生成失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
        /// <summary>
        /// 在世界上掉落物品
        /// </summary>
        /// <param name="player">玩家实例</param>
        /// <param name="item">物品实例</param>
        /// <param name="gameWorld">世界实例</param>
        private static void DropItemToGround(Player player, Item item, GameWorld gameWorld)
        {
            //在玩家脚底高一米, 向"前"0.5米的位置生成
            Vector3 spawnPosition = player.Transform.position + new Vector3(0f, 1f, 0f);
            spawnPosition += player.Transform.forward * 0.5f;
            //高度随机数
            spawnPosition.y += UnityEngine.Random.Range(-0.05f, 0.1f);
            //掉落一个物品
            LootItem spawnedLoot = gameWorld.ThrowItem(
                item,                   //物品实例
                null,                   //Owner为null表示刷新出而不是任何玩家实例丢弃
                spawnPosition,          //生成坐标
                Quaternion.identity,    //无旋转角
                Vector3.zero,           //无初速度
                Vector3.zero,           //无角速度
                true,                   //syncable
                true                    //performPickUpValidation
                //最后这俩是啥啊....
            );
            //预留Debug区
            if (spawnedLoot != null)
            {
            }
            else
            {
            }
        }
        /// <summary>
        /// 在玩家前方掉落物品
        /// </summary>
        /// <param name="player">玩家实例</param>
        /// <param name="item">物品实例</param>
        /// <param name="gameWorld">世界实例</param>
        /// <param name="cam">摄像机</param>
        private static void DropItem(Player player, Item item, GameWorld gameWorld, Camera cam)
        {
            //防御
            if (cam == null) return;
            //主摄像机朝向的方向
            Vector3 spawnPosition = cam.transform.position + (cam.transform.forward * 0.8f);
            //只保留水平角度
            Quaternion spawnRotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
            //生成物品
            LootItem spawnedLoot = gameWorld.ThrowItem(
                item,                   //物品实例
                null,                   //Owner为null表示刷新出而不是任何玩家实例丢弃
                spawnPosition,          //生成坐标
                spawnRotation,          //生成旋转角让物品永远朝向这个瞬间的玩家
                Vector3.zero,           //无初速度
                Vector3.zero,           //无角速度
                true,                   //syncable
                true                    //performPickUpValidation
            );
            //预留Debug
            if (spawnedLoot != null)
            {
            }
        }
        /// <summary>
        /// 异步加载涉及到的物品资产
        /// </summary>
        /// <param name="rootItem">物品树实例</param>
        /// <returns></returns>
        private static async Task LoadItemBundlesAsync(Item rootItem)
        {
            //全局实例
            var poolManager = Singleton<PoolManagerClass>.Instance;
            if (poolManager == null) return;
            //去重
            var keys = new HashSet<ResourceKey>();
            //遍历物品树
            foreach (var item in rootItem.GetAllItems())
            {
                if (item.Template?.Prefab != null) keys.Add(item.Template.Prefab);
                if (item.Template?.UsePrefab != null) keys.Add(item.Template.UsePrefab);
            }

            if (keys.Count > 0)
            {
                //异步加载
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
        /// <summary>
        /// 桥接方法, 用于直接使用
        /// </summary>
        /// <param name="player">玩家实例</param>
        /// <param name="templateId">物品ID</param>
        public static async void SpawnItemIntoInventory(Player player, string templateId)
        {
            try
            {
                await SpawnItemIntoInventoryAsync(player, templateId);
            }
            catch (Exception ex)
            {
                //捕获
                NotificationManagerClass.DisplayMessageNotification(
                    "生成物品失败！",
                    EFT.Communications.ENotificationDurationType.Default,
                    EFT.Communications.ENotificationIconType.Alert
                );
            }
        }
        /// <summary>
        /// 桥接方法, 用于直接使用
        /// </summary>
        /// <param name="player">玩家实例</param>
        /// <param name="item">物品实例</param>
        public static async void CloneAndDropItem(Player player, Item item)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            try
            {
                await CloneAndDropItemAsync(player, item, cam);
            }
            catch (Exception ex)
            {
                //捕获
                NotificationManagerClass.DisplayMessageNotification(
                    "生成物品失败！",
                    EFT.Communications.ENotificationDurationType.Default,
                    EFT.Communications.ENotificationIconType.Alert
                );
            }
        }
        //物品栏寻址算法
        public static ItemAddress FindEmptyLocation(Player player, Item newItem)
        {
            //划定物品栏有效区域(胸挂, 口袋, 背包)
            var equipment = player.Inventory.Equipment;
            EquipmentSlot[] slotsToCheck = {
                EquipmentSlot.Pockets,
                EquipmentSlot.TacticalVest,
                EquipmentSlot.Backpack
            };
            //遍历寻址
            foreach (var slotType in slotsToCheck)
            {
                var slot = equipment.GetSlot(slotType);
                if (slot.ContainedItem is CompoundItem containerItem)
                {
                    foreach (var grid in containerItem.Grids)
                    {
                        //原版判断方法
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
    /// <summary>
    /// 配置项定义
    /// </summary>
    public static class ItemSpawnerCfg
    {
        internal static ConfigEntry<string> TargetItemId { get; set; }
        internal static ConfigEntry<bool> MaxStack { get; set; }
        internal static ConfigEntry<bool> ForcedFiR { get; set; }
        internal static ConfigEntry<int> CustomStackSize { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
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