using Diz.LanguageExtensions;
using Diz.Utils;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Oracle.Data;
using Oracle.ItemSpawn;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GetActionsClass;

namespace Oracle.RaidManager
{
    /// <summary>
    /// 战利品管理器
    /// </summary>
    public class LootManagerGUI
    {
        public Vector2 _scrollPos;
        public static bool ShowLooseLoot = true;
        public static bool ShowStaticLoot = true;

        //图标缓存
        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();

        //远程搜索的Patch
        [HarmonyPatch(typeof(InteractionsHandlerClass), "smethod_14")]
        public class InteractionsHandlerClassPatch
        {
            public static bool Prefix(Item item, out Error error, ref bool __result)
            {
                error = null;
                __result = false;

                return false;
            }
        }

        public void DrawPanel()
        {
            UIStyleManager.EnsureInitialized();

            if (GUI.Button(new Rect(RaidManagerGUI._windowRect.width - 130, 4, 70, 20), "text_button_loot_manager_ground".i18n(), ShowLooseLoot ? UIStyleManager.BlueButtonStyle : UIStyleManager.RedButtonStyle))
            {
                ShowLooseLoot = !ShowLooseLoot;
            }
            if (GUI.Button(new Rect(RaidManagerGUI._windowRect.width - 205, 4, 70, 20), "text_button_loot_manager_container".i18n(), ShowStaticLoot ? UIStyleManager.BlueButtonStyle : UIStyleManager.RedButtonStyle))
            {
                ShowStaticLoot = !ShowStaticLoot;
            }

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            GUILayout.Space(10);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            if (OracleLootDataManager.CachedLootList == null || OracleLootDataManager.CachedLootList.Count == 0)
            {
                GUILayout.Label("text_button_loot_manager_no_result".i18n(), UIStyleManager.BoxStyle);
            }
            else
            {
                //等级和价格排序
                var sortedLoot = OracleLootDataManager.CachedLootList
                    .OrderByDescending(l => l.ItemLevel)
                    .ThenByDescending(l => l.Price)
                    .ToList();

                foreach (LootData loot in sortedLoot)
                {
                    //遍历
                    if ((ShowStaticLoot && loot.Container != null) || (ShowLooseLoot && loot.Container == null))
                    {
                        GUILayout.BeginHorizontal(UIStyleManager.BoxStyle);
                        
                        //物品图标
                        Texture2D icon = GetCachedIcon(loot.ItemRef);
                        if (icon != null) GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                        else GUILayout.Label("text_loot_manager_no_icon".i18n(), GUILayout.Width(64), GUILayout.Height(64));

                        GUILayout.BeginVertical();

                        //物品信息
                        GUILayout.Label($"<b><color={loot.ItemColor}>{loot.ItemRef.Name.Localized()}</color></b>");
                        GUILayout.Label(string.Format("text_loot_manager_loot_item_info".i18n(), OracleColorManager.TextGray, loot.Price, loot.Distance));
                        GUILayout.Label(string.Format("text_loot_manager_loot_item_status".i18n(), OracleColorManager.TextGray, OracleLootDataManager.GetContainerName(loot.Container), loot.StackCount));
                        GUILayout.EndVertical();

                        //按钮
                        GUILayout.BeginVertical(GUILayout.Width(110));

                        if (GUILayout.Button("text_button_loot_manager_pick".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Height(30)))
                        {
                            Player mainPlayer = PluginsCore.CorrectPlayer;
                            if (mainPlayer != null)
                            {
                                PickupLootItemEx(mainPlayer, loot);
                            }
                        }

                        GUILayout.Space(4);

                        if (GUILayout.Button("text_button_loot_manager_copy".i18n(), UIStyleManager.BlueButtonStyle, GUILayout.Height(30)))
                        {
                            Item clonedItem = loot.ItemRef.CloneItem().ReassignAllIds();
                            ItemCatcher.SavedItems.Add(clonedItem);
                            ItemCatcher.savedItem = clonedItem;
                            //OracleNotify.Message($"已捕获 {loot.ItemRef.Name.Localized()} 的元数据！", EFT.Communications.ENotificationIconType.Default, GlobalCfg.MuteNotice.Value);
                        }

                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndScrollView();

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;
        }

        /// <summary>
        /// 获取物品图标
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public Texture2D GetCachedIcon(Item item)
        {
            if (item == null) return null;
            if (_iconCache.TryGetValue(item.TemplateId, out Texture2D cachedTex)) return cachedTex;

            try
            {
                var iconData = ItemViewFactory.LoadItemIcon(item, 1, false);
                if (iconData != null && iconData.Sprite != null && iconData.Sprite.texture != null)
                {
                    Texture2D tex = iconData.Sprite.texture;
                    _iconCache[item.TemplateId] = tex;
                    return tex;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 远程捡起物品
        /// </summary>
        /// <param name="player"></param>
        /// <param name="lootItem"></param>
        public static void PickupLootItem(Player player, LootItem lootItem)
        {
            if (player == null || lootItem == null) return;

            try
            {
                Item item = lootItem.Item;

                ItemAddress targetLocation = ItemSpawner.FindEmptyLocation(player, item);

                if (targetLocation == null)
                {
                    //NotificationManagerClass.DisplayWarningNotification("背包空间不足！");
                    return;
                }
                var controller = player.InventoryController;

                //组包
                var pickUpResult =
                    InteractionsHandlerClass.QuickFindAppropriatePlace(
                    item,
                    player.InventoryController,
                    player.Inventory.Equipment.ToEnumerable(),
                    InteractionsHandlerClass.EMoveItemOrder.PickUp,
                    true
                );

                if (pickUpResult.Succeeded && controller.CanExecute(pickUpResult.Value))
                {
                    //发包
                    controller.RunNetworkTransaction(
                        pickUpResult.Value,
                        result =>
                        {
                            if (result.Succeed)
                            {
                                player.UpdateInteractionCast();
                            }

                            var pickupState = player.CurrentState as PickupStateClass;
                            pickupState?.Pickup(false, null);
                        }
                    );

                    player.CurrentManagedState.Pickup(true, null);
                }
            }
            catch (Exception ex)
            {
                OracleCommon.ShowError(ex);
            }
        }

        /// <summary>
        /// 远程拾取+容器
        /// </summary>
        /// <param name="player"></param>
        /// <param name="loot"></param>
        public static void PickupLootItemEx(Player player, LootData loot)
        {
            if (player == null) return;

            if (loot.LootableItem != null)
            {
                //looseloot直接拾起
                PickupLootItem(player, loot.LootableItem);
                return;
            }
            else if (loot.Container != null)
            {
                try
                {
                    //查找容器
                    Item containerItem = loot.Container.ItemOwner.RootItem;

                    Player mainPlayer = PluginsCore.CorrectPlayer;
                    if (mainPlayer == null) return;

                    GamePlayerOwner myOwner = mainPlayer.GetComponent<GamePlayerOwner>();
                    if (myOwner == null)
                    {
                        //NotificationManagerClass.DisplayWarningNotification("无法获取本地 UI 控制器 (GamePlayerOwner)");
                        return;
                    }
                    if (myOwner == null) return;

                    //在内存中构建交互行为
                    Class1748 context = new Class1748
                    {
                        owner = myOwner,
                        rootItem = containerItem,
                        lootItemOwner = containerItem.Owner as TraderControllerClass,
                        controller = player.InventoryController
                    };

                    //射线伪造
                    player.SaveInteractionRayInfo();

                    //触发交互
                    context.method_3();
                    //NotificationManagerClass.DisplayMessageNotification($"已远程打开: {containerItem.Name.Localized()}");
                }
                catch (Exception ex)
                {
                    OracleCommon.ShowError(ex);
                }
            }
        }
    }
}