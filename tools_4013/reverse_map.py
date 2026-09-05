#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""4013 分支专用：Oracle 源码 4.1 API -> 4.0 混淆名 反向映射替换。

规则来源：git commit 40dfaca 的迁移 diff（4.0->4.1）逐条反推，并已用
I:\\TKFCoop 的 Assembly-CSharp.dll 元数据验证目标名存在。

用法: python tools_4013/reverse_map.py
"""
import io
import os
import re

# ---------------------------------------------------------------
# 类型/命名空间级替换（作用于 .cs 文件全文，先做长串/完全限定，后做扁平名）
# (4.1 写法, 4.0 写法) —— 长串优先
# ---------------------------------------------------------------
TYPE_MAP = [
    # 命名空间完全限定型
    ("EFT.InventoryLogic.Operations.CommandWithOwners", "GClass3473"),
    ("EFT.InventoryLogic.Operations.AbstractOperation", "BaseInventoryOperationClass"),
    ("EFT.InventoryLogic.Operations.BaseInventoryCommand", "GClass3471"),
    ("EFT.InventoryLogic.ItemManipulator", "InteractionsHandlerClass"),
    ("EFT.InventoryLogic.ItemController", "TraderControllerClass"),
    ("EFT.InventoryLogic.Ammo", "AmmoItemClass"),
    ("EFT.InventoryLogic.Magazine", "MagazineItemClass"),
    ("EFT.InventoryLogic.CylinderMagazine", "CylinderMagazineItemClass"),
    ("EFT.InventoryLogic.BackpackTemplate", "BackpackTemplateClass"),
    ("EFT.InventoryLogic.VestTemplate", "VestTemplateClass"),
    ("EFT.InventoryLogic.Stash", "StashItemClass"),
    ("EFT.Ballistics.Shot", "EftBulletClass"),
    ("EFT.Ballistics.DamageInfo", "DamageInfoStruct"),
    ("EFT.ItemFactory", "ItemFactoryClass"),
    ("EFT.ProfileInfo", "InfoClass"),
    ("EFT.Quests.QuestController", "AbstractQuestControllerClass"),
    ("EFT.Achievements.AchievementsController", "AbstractAchievementControllerClass"),
    ("EFT.Prestige.PrestigeController", "AbstractPrestigeControllerClass"),
    ("EFT.IEftSession", "ISession"),
    ("EFT.InventoryOperationDescriptor", "BaseDescriptorClass"),
    ("EFT.UI.AvailableInteractionState", "ActionsReturnClass"),
    ("EFT.UI.InteractionAction", "ActionsTypesClass"),
    ("EFT.PlayerIcons.PlayerIconCreator", "GClass927"),
    ("EFT.PlayerIcons.PlayerIconRequest", "GClass932"),
    ("EFT.PlayerIcons.ItemIcon", "GClass929"),
    ("EFT.Communications.NotificationManager", "NotificationManagerClass"),
    ("EFT.Communications.NotificationManagerClass", "NotificationManagerClass"),
    ("EFT.HandBook.HandbookClass", "HandbookClass"),
    # JsonType 命名空间
    ("JsonType.FlatItem", "FlatItemsDataClass"),
    # Diz 命名空间
    ("Diz.LanguageExtensions.OperationCreationResult", "GStruct152"),
    ("Diz.Jobs.JobYieldPriority", "JobPriorityClass"),
    # PoolManager
    ("PoolManagerClass.AssemblyType", "PoolManagerClass.AssemblyType"),  # 同名跳过
    # 扁平型（必须词边界）
    ("ItemManipulator", "InteractionsHandlerClass"),
    ("ItemController", "TraderControllerClass"),
    ("ProfileInfo", "InfoClass"),
    ("SearchController", "GClass2234"),
    ("ItemInfo", "GClass1802"),
    ("ItemContext", "ItemContextAbstractClass"),
    ("InteractionContextHelper", "GetActionsClass"),
    ("IInteractive", "GInterface177"),
    ("IOperationResult", "IRaiseEvents"),
    ("ObjectsFactory", "PoolManagerClass"),
    ("PickUpState", "PickupStateClass"),
    ("Skill", "SkillClass"),
    ("Mastering", "MasterSkillClass"),
    ("BotProfileData", "BotProfileDataClass"),
    ("GetProfileDataParams", "BotProfileDataClass"),
]

# ---------------------------------------------------------------
# 成员级替换（精确到文件，避免误伤同名字段）
# (文件, 旧串, 新串)
# ---------------------------------------------------------------
MEMBER_MAP = [
    ("Ability/InfinityStamina.cs", "nameof(InventoryEquipment.GetTotalWeight)", '"smethod_1"'),
    ("Ability/NoFallenDamage.cs", "ref EFT.Ballistics.DamageInfo damageInfo", "ref DamageInfoStruct damageInfo"),
    ("Ability/NoFallenDamage.cs", "ref DamageInfoStruct damageInfo", "ref DamageInfoStruct damageInfo"),  # 幂等
    ("Combat/Aimbot.cs", "EFT.InventoryLogic.Ammo ammo", "AmmoItemClass ammo"),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(ItemView.OnPointerEnter)', '"OnPointerEnter"'),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(EntityIcon.CG_Awake)', '"method_1"'),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(TraderRequirementPanel.CG_Awake)', '"method_1"'),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(TradingRequisitePanel.CG_Awake1)', '"method_2"'),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(GridItemView.OnPointerEnter)', '"OnPointerEnter"'),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(HideoutItemView.OnPointerEnter)', '"OnPointerEnter"'),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(ItemView.OnPointerExit)', '"OnPointerExit"'),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(EntityIcon.CG_Awake1)', '"method_2"'),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(TradingRequisitePanel.CG_Awake)', '"method_1"'),
    ("ItemSpawn/ItemCatcher.cs", 'nameof(GridItemView.OnPointerExit)', '"OnPointerExit"'),
    ("ItemSpawn/ItemCatcher.cs", 'Field("_item")', 'Field("item_0")'),
    ("ItemSpawn/ItemCatcher.cs", 'Field("_itemContext")', 'Field("itemContextAbstractClass")'),
    ("ItemSpawn/ItemInstanceHelper.cs", "repairKit._template", "repairKit.RepairKitsTemplateClass"),
    ("ItemSpawn/ItemSpawnStashPatch.cs", 'nameof(InventoryScreen.Show)', '"Show"'),
    ("ItemSpawn/ItemSpawnStashPatch.cs", '"AddResult"', '"GClass3405"'),
    ("ItemSpawn/ItemSpawner.cs", "ItemManipulator.Add", "InteractionsHandlerClass.Add"),
    ("ItemSpawn/ItemSpawner.cs", "JobPriorityClass.Immediate", "JobPriorityClass.Immediate"),  # 幂等
    ("PluginsCore.cs", 'nameof(GameWorld.OnGameStarted)', '"OnGameStarted"'),
    ("RaidManager/AIManagerGUI.cs", 'nameof(Player.ApplyDamageInfo)', '"ApplyDamageInfo"'),  # 若有
    ("RaidManager/AIManagerGUI.cs", '"GClass3405"', '"GClass3405"'),  # 幂等
    ("RaidManager/LootManagerGUI.cs", 'nameof(ItemManipulator.IsItemLocked)', '"smethod_14"'),
    ("RaidManager/LootManagerGUI.cs", "ItemManipulator.QuickFindAppropriatePlace", "InteractionsHandlerClass.QuickFindAppropriatePlace"),
    ("RaidManager/LootManagerGUI.cs", "ItemManipulator.EMoveItemOrder", "InteractionsHandlerClass.EMoveItemOrder"),
    ("RaidManager/SkillManagerGUI.cs", "_selectedMastering.Lvl1", "_selectedMastering.Int32_0"),
    ("RaidManager/SkillManagerGUI.cs", "_selectedMastering.Lvl2", "_selectedMastering.Int32_1"),
    ("RaidManager/StatsManagerGUI.cs", "CountersCollection.Identifier", "SessionCountersClass.SessionCounterIdentifierValueClass"),
    ("Utils/OracleNotify.cs", "NotificationManager.DisplayMessageNotification", "NotificationManagerClass.DisplayMessageNotification"),
]

WORD_BOUNDARY = re.compile(r"(?<![A-Za-z0-9_])%s(?![A-Za-z0-9_])")


def apply_type_map(txt):
    for old, new in TYPE_MAP:
        if "." in old:
            txt = txt.replace(old, new)
        else:
            txt = WORD_BOUNDARY % re.escape(old) and txt  # placeholder
    return txt


def main():
    # 收集所有 .cs
    files = []
    for root, dirs, fnames in os.walk("."):
        dirs[:] = [d for d in dirs if d not in (".git", "obj", "bin", "tools_4013", "locales")]
        for fn in fnames:
            if fn.endswith(".cs"):
                files.append(os.path.join(root, fn))

    # 先做类型级（全部文件）
    for fp in files:
        with io.open(fp, encoding="utf-8-sig") as f:
            txt = f.read()
        orig = txt
        for old, new in TYPE_MAP:
            if "." in old or old == "ItemContext":
                txt = txt.replace(old, new)
            else:
                txt = re.sub(r"(?<![A-Za-z0-9_])%s(?![A-Za-z0-9_])" % re.escape(old), new, txt)
        if txt != orig:
            with io.open(fp, "w", encoding="utf-8", newline="\n") as f:
                f.write(txt)
            print(f"[TYPE] {fp}")

    # 再做成员级（精确到文件）
    for fp, old, new in MEMBER_MAP:
        if not os.path.exists(fp):
            print(f"[SKIP 无文件] {fp}")
            continue
        with io.open(fp, encoding="utf-8-sig") as f:
            txt = f.read()
        n = txt.count(old)
        if n:
            txt = txt.replace(old, new)
            with io.open(fp, "w", encoding="utf-8", newline="\n") as f:
                f.write(txt)
            print(f"[MEMBER x{n}] {fp}: {old!r} -> {new!r}")
        else:
            print(f"[无匹配] {fp}: {old!r}")


if __name__ == "__main__":
    main()
