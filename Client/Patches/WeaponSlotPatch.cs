using System;
using HarmonyLib;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 武器槽位补丁 - 监控本地玩家的武器槽位进入/退出
    /// 拦截 Slot.Plug 和 Slot.Unplug 方法
    /// 仅处理主角色（本地玩家）的武器变更，忽略其他玩家和NPC
    /// </summary>
    [HarmonyPatch]
    public static class WeaponSlotPatch
    {
        // 武器槽位的 Hash 值（与 CharacterMainControl 中定义的一致）
        private static readonly int PrimWeaponSlotHash = "PrimaryWeapon".GetHashCode();
        private static readonly int SecWeaponSlotHash = "SecondaryWeapon".GetHashCode();
        private static readonly int MeleeWeaponSlotHash = "MeleeWeapon".GetHashCode();

        // ==================== 拦截 Slot.Plug 方法 ====================

        /// <summary>
        /// 拦截物品插入槽位的操作
        /// </summary>
        [HarmonyPatch(typeof(Slot), "Plug")]
        [HarmonyPostfix]
        private static void Postfix_Plug(Slot __instance, Item otherItem, bool __result)
        {
            try
            {
                // 只处理成功的插入操作
                if (!__result || otherItem == null)
                    return;

                // 检查是否为武器槽位
                if (!IsWeaponSlot(__instance))
                    return;

                // 检查是否为主角色
                if (!IsMainCharacterSlot(__instance))
                    return;

                var slotType = GetWeaponSlotType(__instance);
                var slotTypeName = GetWeaponSlotTypeName(__instance);
                
                Debug.Log($"[武器补丁] {slotTypeName}已装备: {otherItem.name}");

                // 发布事件到 EventBus
                PublishWeaponSlotEvent(new WeaponSlotChangedEvent(
                    __instance,
                    otherItem,
                    slotType,
                    slotTypeName,
                    isEquipped: true
                ));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[武器补丁] Plug 处理失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        // ==================== 拦截 Slot.Unplug 方法 ====================

        /// <summary>
        /// 拦截物品从槽位移除的操作
        /// </summary>
        [HarmonyPatch(typeof(Slot), "Unplug")]
        [HarmonyPrefix]
        private static void Prefix_Unplug(Slot __instance)
        {
            try
            {
                // 检查槽位是否有内容
                if (__instance?.Content == null)
                    return;

                // 检查是否为武器槽位
                if (!IsWeaponSlot(__instance))
                    return;

                // 检查是否为主角色
                if (!IsMainCharacterSlot(__instance))
                    return;

                var slotType = GetWeaponSlotType(__instance);
                var slotTypeName = GetWeaponSlotTypeName(__instance);
                Item removedItem = __instance.Content;

                Debug.Log($"[武器补丁] {slotTypeName}已卸下: {removedItem.name}");

                // 发布事件到 EventBus
                PublishWeaponSlotEvent(new WeaponSlotChangedEvent(
                    __instance,
                    removedItem,
                    slotType,
                    slotTypeName,
                    isEquipped: false
                ));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[武器补丁] Unplug 处理失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 检查槽位是否为武器槽位
        /// </summary>
        private static bool IsWeaponSlot(Slot slot)
        {
            if (slot == null)
                return false;

            int slotHash = slot.Key.GetHashCode();
            return slotHash == PrimWeaponSlotHash ||
                   slotHash == SecWeaponSlotHash ||
                   slotHash == MeleeWeaponSlotHash;
        }

        /// <summary>
        /// 获取武器槽位类型枚举
        /// </summary>
        private static Shared.Data.WeaponSlotType GetWeaponSlotType(Slot slot)
        {
            if (slot == null)
                return Shared.Data.WeaponSlotType.PrimaryWeapon;

            int slotHash = slot.Key.GetHashCode();

            if (slotHash == PrimWeaponSlotHash)
                return Shared.Data.WeaponSlotType.PrimaryWeapon;
            if (slotHash == SecWeaponSlotHash)
                return Shared.Data.WeaponSlotType.SecondaryWeapon;
            if (slotHash == MeleeWeaponSlotHash)
                return Shared.Data.WeaponSlotType.MeleeWeapon;

            return Shared.Data.WeaponSlotType.PrimaryWeapon;
        }

        /// <summary>
        /// 获取武器槽位类型名称（中文）
        /// </summary>
        private static string GetWeaponSlotTypeName(Slot slot)
        {
            if (slot == null)
                return "未知";

            int slotHash = slot.Key.GetHashCode();

            if (slotHash == PrimWeaponSlotHash)
                return "主武器";
            if (slotHash == SecWeaponSlotHash)
                return "副武器";
            if (slotHash == MeleeWeaponSlotHash)
                return "近战武器";

            return "未知武器槽位";
        }

        /// <summary>
        /// 检查槽位是否属于主角色
        /// </summary>
        private static bool IsMainCharacterSlot(Slot slot)
        {
            if (slot == null)
                return false;

            // 通过槽位的 Master（所属 Item）找到角色
            var characterItem = slot.Master;
            if (characterItem == null)
                return false;

            // 检查是否为主角色的物品
            var mainCharacter = LevelManager.Instance?.MainCharacter;
            if (mainCharacter == null)
                return false;

            return characterItem == mainCharacter.CharacterItem;
        }

        /// <summary>
        /// 发布武器槽位事件到 EventBus 并同步到服务器
        /// </summary>
        private static void PublishWeaponSlotEvent(WeaponSlotChangedEvent weaponEvent)
        {
            if (!GameContext.IsInitialized ||
                GameContext.Instance == null ||
                GameContext.Instance.EventBus == null)
            {
                #if DEBUG || UNITY_EDITOR
                Debug.LogWarning("[武器补丁] EventBus 未初始化，跳过事件发布");
                #endif
                return;
            }

            try
            {
                // 1. 发布本地事件到 EventBus
                GameContext.Instance.EventBus.Publish(weaponEvent);

                // 2. 同步到服务器（异步，不等待）
                SendWeaponUpdateToServerAsync(weaponEvent).ConfigureAwait(false);

                #if DEBUG || UNITY_EDITOR
                string action = weaponEvent.IsEquipped ? "装备" : "卸下";
                string itemName = "无";

                if (weaponEvent.Weapon != null && weaponEvent.Weapon is UnityEngine.Object unityObj)
                {
                    itemName = !string.IsNullOrEmpty(unityObj.name)
                        ? unityObj.name
                        : unityObj.GetType().Name;
                }

                Debug.Log($"[武器补丁] 事件已发布: {weaponEvent.SlotTypeName} - {action} - {itemName}");
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[武器补丁] 事件发布失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 发送武器更新到服务器
        /// </summary>
        private static async System.Threading.Tasks.Task SendWeaponUpdateToServerAsync(WeaponSlotChangedEvent weaponEvent)
        {
            try
            {
                var rpcClient = GameContext.Instance?.RpcClient;
                if (rpcClient == null)
                {
                    Debug.LogWarning("[武器补丁] RPC 客户端未初始化，跳过服务器同步");
                    return;
                }

                var clientContext = new RPC.ClientServerContext(rpcClient);
                var weaponService = new Shared.Services.Generated.WeaponSyncServiceClientProxy(clientContext);

                if (weaponEvent.IsEquipped && weaponEvent.Weapon != null)
                {
                    // 装备武器 - 需要序列化完整物品数据
                    var weaponItem = weaponEvent.Weapon as Item;
                    if (weaponItem == null)
                    {
                        Debug.LogWarning("[武器补丁] ⚠️ 武器对象类型转换失败");
                        return;
                    }

                    var request = Services.WeaponSyncHelper.CreateWeaponSlotUpdateRequest(
                        weaponEvent.SlotType,
                        weaponItem
                    );

                    bool success = await weaponService.EquipWeaponAsync(request);

                    if (success)
                    {
                        Debug.Log($"[武器补丁] ✅ 武器装备已同步到服务器: {weaponEvent.SlotTypeName}");
                    }
                    else
                    {
                        Debug.LogWarning($"[武器补丁] ⚠️ 服务器拒绝武器装备");
                    }
                }
                else
                {
                    // 卸下武器
                    var request = new Shared.Data.WeaponSlotUnequipRequest
                    {
                        SlotType = weaponEvent.SlotType
                    };

                    bool success = await weaponService.UnequipWeaponAsync(request);

                    if (success)
                    {
                        Debug.Log($"[武器补丁] ✅ 武器卸下已同步到服务器: {weaponEvent.SlotTypeName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[武器补丁] 同步到服务器失败: {ex.Message}");
            }
        }
    }
}

