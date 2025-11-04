using System;
using System.Collections;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using UnityEngine;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 装备工具类 - 提供创建和装备物品的便捷方法
    /// </summary>
    public static class EquipmentTools
    {
        /// <summary>
        /// 创建物品并装备到指定槽位（同步版本 - 使用 InstantiateSync）
        /// </summary>
        /// <param name="itemTypeId">物品类型ID</param>
        /// <param name="targetSlot">目标槽位</param>
        /// <param name="handleUnplugged">如何处理被替换的物品（null=销毁，否则调用回调）</param>
        /// <returns>是否成功装备</returns>
        public static bool CreateAndEquip(
            int itemTypeId, 
            Slot targetSlot, 
            Action<Item>? handleUnplugged = null)
        {
            if (targetSlot == null)
            {
                Debug.LogError("[EquipmentTools] 目标槽位为空");
                return false;
            }

            try
            {
                // 1. 创建物品实例（同步）
                Item newItem = ItemAssetsCollection.InstantiateSync(itemTypeId);
                if (newItem == null)
                {
                    Debug.LogError($"[EquipmentTools] 创建物品失败 - TypeID={itemTypeId}");
                    return false;
                }

                // 2. 插入槽位
                bool success = targetSlot.Plug(newItem, out Item unpluggedItem);

                if (success)
                {
                    Debug.Log($"[EquipmentTools] ✅ 成功装备物品: {newItem.DisplayName} 到槽位 {targetSlot.Key}");

                    // 处理被替换的物品
                    if (unpluggedItem != null)
                    {
                        Debug.Log($"[EquipmentTools] 槽位中原有物品被替换: {unpluggedItem.DisplayName}");
                        
                        if (handleUnplugged != null)
                        {
                            handleUnplugged(unpluggedItem);
                        }
                        else
                        {
                            // 默认：销毁被替换的物品
                            unpluggedItem.DestroyTree();
                        }
                    }

                    return true;
                }
                else
                {
                    Debug.LogError($"[EquipmentTools] ❌ 装备物品失败 - 可能不符合槽位要求");
                    newItem.DestroyTree();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentTools] 装备物品时发生异常: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 为角色装备物品到指定槽位（通过槽位Key）
        /// </summary>
        public static bool EquipToCharacter(
            int itemTypeId,
            Item characterItem,
            string slotKey,
            Action<Item>? handleUnplugged = null)
        {
            if (characterItem == null)
            {
                Debug.LogError("[EquipmentTools] 角色物品为空");
                return false;
            }

            Slot targetSlot = characterItem.Slots[slotKey];
            if (targetSlot == null)
            {
                Debug.LogError($"[EquipmentTools] 槽位不存在: {slotKey}");
                return false;
            }

            return CreateAndEquip(itemTypeId, targetSlot, handleUnplugged);
        }

        /// <summary>
        /// 为角色装备物品到指定槽位（通过槽位Hash）
        /// </summary>
        public static bool EquipToCharacter(
            int itemTypeId,
            Item characterItem,
            int slotHash,
            Action<Item>? handleUnplugged = null)
        {
            if (characterItem == null)
            {
                Debug.LogError("[EquipmentTools] 角色物品为空");
                return false;
            }

            Slot targetSlot = characterItem.Slots.GetSlot(slotHash);
            if (targetSlot == null)
            {
                Debug.LogError($"[EquipmentTools] 槽位不存在: Hash={slotHash}");
                return false;
            }

            return CreateAndEquip(itemTypeId, targetSlot, handleUnplugged);
        }

        /// <summary>
        /// 复制槽位内容到另一个槽位
        /// </summary>
        public static bool CopySlotContent(Slot sourceSlot, Slot targetSlot)
        {
            if (sourceSlot == null || targetSlot == null)
            {
                Debug.LogError("[EquipmentTools] 源槽位或目标槽位为空");
                return false;
            }

            if (sourceSlot.Content == null)
            {
                Debug.Log("[EquipmentTools] 源槽位为空，无需复制");
                return true;
            }

            // 获取源物品的 TypeID
            int itemTypeId = sourceSlot.Content.TypeID;
            
            // 创建相同类型的新物品并装备
            return CreateAndEquip(itemTypeId, targetSlot);
        }

        /// <summary>
        /// 复制角色的所有装备槽位到另一个角色
        /// </summary>
        public static int CopyAllEquipment(
            Item sourceCharacter, 
            Item targetCharacter,
            bool includeArmor = true,
            bool includeHelmet = true,
            bool includeFaceMask = true,
            bool includeBackpack = true,
            bool includeHeadset = true)
        {
            if (sourceCharacter == null || targetCharacter == null)
            {
                Debug.LogError("[EquipmentTools] 源角色或目标角色为空");
                return 0;
            }

            int successCount = 0;

            try
            {
                // 护甲
                if (includeArmor)
                {
                    var sourceSlot = sourceCharacter.Slots.GetSlot(CharacterEquipmentController.armorHash);
                    var targetSlot = targetCharacter.Slots.GetSlot(CharacterEquipmentController.armorHash);
                    if (CopySlotContent(sourceSlot, targetSlot))
                    {
                        successCount++;
                    }
                }

                // 头盔
                if (includeHelmet)
                {
                    var sourceSlot = sourceCharacter.Slots.GetSlot(CharacterEquipmentController.helmatHash);
                    var targetSlot = targetCharacter.Slots.GetSlot(CharacterEquipmentController.helmatHash);
                    if (CopySlotContent(sourceSlot, targetSlot))
                    {
                        successCount++;
                    }
                }

                // 面罩
                if (includeFaceMask)
                {
                    var sourceSlot = sourceCharacter.Slots.GetSlot(CharacterEquipmentController.faceMaskHash);
                    var targetSlot = targetCharacter.Slots.GetSlot(CharacterEquipmentController.faceMaskHash);
                    if (CopySlotContent(sourceSlot, targetSlot))
                    {
                        successCount++;
                    }
                }

                // 背包
                if (includeBackpack)
                {
                    var sourceSlot = sourceCharacter.Slots.GetSlot(CharacterEquipmentController.backpackHash);
                    var targetSlot = targetCharacter.Slots.GetSlot(CharacterEquipmentController.backpackHash);
                    if (CopySlotContent(sourceSlot, targetSlot))
                    {
                        successCount++;
                    }
                }

                // 耳机
                if (includeHeadset)
                {
                    var sourceSlot = sourceCharacter.Slots.GetSlot(CharacterEquipmentController.headsetHash);
                    var targetSlot = targetCharacter.Slots.GetSlot(CharacterEquipmentController.headsetHash);
                    if (CopySlotContent(sourceSlot, targetSlot))
                    {
                        successCount++;
                    }
                }

                Debug.Log($"[EquipmentTools] ✅ 装备复制完成: {successCount} 个槽位");
                return successCount;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentTools] 复制装备时发生异常: {ex.Message}\n{ex.StackTrace}");
                return successCount;
            }
        }

        /// <summary>
        /// 获取角色指定槽位的物品TypeID
        /// </summary>
        public static int? GetEquipmentTypeId(Item characterItem, int slotHash)
        {
            if (characterItem == null) return null;

            var slot = characterItem.Slots.GetSlot(slotHash);
            return slot?.Content?.TypeID;
        }

        /// <summary>
        /// 检查槽位是否为空
        /// </summary>
        public static bool IsSlotEmpty(Item characterItem, int slotHash)
        {
            if (characterItem == null) return true;

            var slot = characterItem.Slots.GetSlot(slotHash);
            return slot?.Content == null;
        }

        /// <summary>
        /// 卸下装备（移除槽位中的物品）
        /// 使用 Slot.Unplug() 方法正确移除装备
        /// </summary>
        /// <param name="characterItem">角色物品</param>
        /// <param name="slotHash">槽位Hash</param>
        /// <returns>被移除的物品（如果槽位为空则返回null）</returns>
        public static Item? UnequipSlot(Item characterItem, int slotHash)
        {
            if (characterItem == null) return null;

            var slot = characterItem.Slots.GetSlot(slotHash);
            if (slot == null)
            {
                Debug.LogWarning($"[EquipmentTools] 槽位不存在: Hash={slotHash}");
                return null;
            }

            if (slot.Content == null)
            {
                Debug.Log($"[EquipmentTools] 槽位已经为空: {slot.Key}");
                return null;
            }

            // 使用 Unplug() 方法移除装备
            // 这会触发 onSlotContentChanged 事件，您的补丁会捕获到
            Item removedItem = slot.Unplug();
            
            if (removedItem != null)
            {
                Debug.Log($"[EquipmentTools] ✅ 卸下装备: {removedItem.DisplayName} (从槽位 {slot.Key})");
            }
            
            return removedItem;
        }

        /// <summary>
        /// 卸下装备并销毁
        /// </summary>
        public static bool UnequipAndDestroy(Item characterItem, int slotHash)
        {
            var removedItem = UnequipSlot(characterItem, slotHash);
            if (removedItem != null)
            {
                removedItem.DestroyTree();
                Debug.Log($"[EquipmentTools] 装备已销毁: {removedItem.DisplayName}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清空所有装备槽位
        /// </summary>
        public static int ClearAllEquipment(Item characterItem, bool destroyItems = true)
        {
            if (characterItem == null) return 0;

            int count = 0;
            int[] slotHashes = new[]
            {
                CharacterEquipmentController.armorHash,
                CharacterEquipmentController.helmatHash,
                CharacterEquipmentController.faceMaskHash,
                CharacterEquipmentController.backpackHash,
                CharacterEquipmentController.headsetHash
            };

            foreach (var hash in slotHashes)
            {
                var item = UnequipSlot(characterItem, hash);
                if (item != null)
                {
                    if (destroyItems)
                    {
                        item.DestroyTree();
                    }
                    count++;
                }
            }

            Debug.Log($"[EquipmentTools] 清空装备完成: {count} 个槽位");
            return count;
        }
    }
}

